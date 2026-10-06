using System.ComponentModel;

namespace KbLight;

sealed class TrayApp : ApplicationContext
{
    public const string ShowSignalName = @"Local\KbLight.Show";
    const int FirstUpdateCheckMs = 60_000;
    const int UpdateCheckPeriodMs = 24 * 60 * 60 * 1000;

    readonly SynchronizationContext _ui = SynchronizationContext.Current!;
    readonly LightSettings _settings;
    readonly NotifyIcon _tray;
    readonly ToolStripMenuItem _autostartItem;
    readonly SystemWatcher _watcher = new();
    readonly System.Windows.Forms.Timer _arrivalDelay = new() { Interval = 1500 };
    readonly System.Windows.Forms.Timer _resumeDelay = new() { Interval = 3000 };
    readonly System.Windows.Forms.Timer _editDelay = new() { Interval = 300 };
    readonly System.Windows.Forms.Timer _updateTimer = new() { Interval = FirstUpdateCheckMs };
    readonly EventWaitHandle _showSignal = new(false, EventResetMode.AutoReset, ShowSignalName);
    readonly RegisteredWaitHandle _showWait;
    SettingsForm? _form;
    bool _autostart;
    // No settings.json yet: requests read the keyboard's lighting instead of writing ours over it.
    bool _firstRun;
    string _status = Text.Applying;

    // Update check (spec update-check): runs on a pool thread, results come back through _ui.
    ReleaseInfo? _update;
    Version? _announced;
    bool _checking, _updateErrorLogged;

    // Applies run one at a time on a pool thread; a request queued meanwhile replaces older queued ones.
    readonly object _gate = new();
    readonly ManualResetEventSlim _idle = new(true);
    ApplyRequest? _pending;

    sealed record ApplyRequest(LightSettings Settings, bool Force, string Reason, int Attempts, bool Adopt);

    public TrayApp(bool showSettings)
    {
        _firstRun = !LightSettings.Exists;
        _settings = LightSettings.Load();

        _autostartItem = new ToolStripMenuItem(Text.StartWithWindows, null, (_, _) => SetAutostart(!_autostart));
        var menu = new ContextMenuStrip();
        var settingsItem = new ToolStripMenuItem(Text.SettingsMenu, null, (_, _) => ShowSettings());
        settingsItem.Font = new Font(settingsItem.Font, FontStyle.Bold);
        menu.Items.Add(settingsItem);
        menu.Items.Add(Text.ApplyNowMenu, null, (_, _) => RequestApply(force: true, Text.ReasonManual));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_autostartItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Text.ExitMenu, null, (_, _) => ExitThread());

        _tray = new NotifyIcon
        {
            Icon = AppFiles.LoadIcon(SystemInformation.SmallIconSize.Width),
            ContextMenuStrip = menu,
            Visible = true,
        };
        _tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) ShowSettings(); };
        _tray.BalloonTipClicked += (_, _) => { if (_update is not null) UpdateCheck.Open(_update); };
        UpdateTooltip();

        _watcher.KeyboardArrived += () => Restart(_arrivalDelay);
        _watcher.Resumed += () => Restart(_resumeDelay);
        // A replugged keyboard has been powered off, so it gets the lighting unconditionally;
        // after sleep it usually still has it, so only a mismatch is rewritten.
        _arrivalDelay.Tick += (_, _) => { _arrivalDelay.Stop(); RequestApply(force: true, Text.ReasonArrived, attempts: 3); };
        _resumeDelay.Tick += (_, _) => { _resumeDelay.Stop(); RequestApply(force: false, Text.ReasonResumed, attempts: 3); };
        _editDelay.Tick += (_, _) => FlushEdits();
        _updateTimer.Tick += (_, _) =>
        {
            _updateTimer.Interval = UpdateCheckPeriodMs;
            CheckForUpdates();
        };

        _showWait = ThreadPool.RegisterWaitForSingleObject(_showSignal,
            (_, _) => _ui.Post(_ => ShowSettings(), null), null, Timeout.Infinite, executeOnlyOnce: false);

        Log.Write(Text.LogStart(Environment.ProcessPath) + (showSettings ? "" : " --tray"));
        Log.Write(AppVersion.Text);
        RequestApply(force: true, Text.ReasonStartup, attempts: 6);
        if (_settings.CheckUpdates) _updateTimer.Start();
        Task.Run(() =>
        {
            bool enabled = Autostart.IsEnabled();
            _ui.Post(_ => ShowAutostart(enabled), null);
        });
        if (showSettings) ShowSettings();
    }

    /// <summary>Waits for an apply in progress, so exiting never cuts a keyboard session in half.</summary>
    public bool WaitIdle(TimeSpan timeout) => _idle.Wait(timeout);

    protected override void ExitThreadCore()
    {
        FlushEdits();
        _updateTimer.Stop();
        _form?.Close();
        _tray.Visible = false;
        _tray.Dispose();
        _watcher.Dispose();
        _showWait.Unregister(null);
        _showSignal.Dispose();
        Log.Write(Text.LogExit);
        base.ExitThreadCore();
    }

    static void Restart(System.Windows.Forms.Timer timer)
    {
        timer.Stop();
        timer.Start();
    }

    void FlushEdits()
    {
        if (!_editDelay.Enabled) return;
        _editDelay.Stop();
        _firstRun = false;
        SaveSettings();
        RequestApply(force: true, Text.ReasonEdited);
    }

    void SaveSettings()
    {
        try { _settings.Save(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.Write(Text.SettingsNotSaved(e.Message));
        }
    }

    void RequestApply(bool force, string reason, int attempts = 1)
    {
        var request = new ApplyRequest(_settings.Clone(), force, reason, attempts, _firstRun);
        lock (_gate)
        {
            bool busy = _pending is not null || !_idle.IsSet;
            // Adopt comes from the newer request: an edit cancels a pending first-run read.
            _pending = _pending is null ? request
                : request with { Force = force || _pending.Force, Attempts = Math.Max(attempts, _pending.Attempts) };
            if (busy) return;
            _idle.Reset();
        }
        Task.Run(Worker);
    }

    void Worker()
    {
        while (true)
        {
            ApplyRequest request;
            lock (_gate)
            {
                if (_pending is null)
                {
                    _idle.Set();
                    return;
                }
                request = _pending;
                _pending = null;
            }
            var result = Apply(request);
            _ui.Post(_ => OnApplied(request, result), null);
        }
    }

    ApplyResult Apply(ApplyRequest request)
    {
        byte[]? fallback = request.Settings.GetLastGoodBlock();
        for (int attempt = 1; ; attempt++)
        {
            var result = request.Adopt ? Keyboard.Read() : Keyboard.Apply(request.Settings, request.Force, fallback);
            Log.Write($"{request.Reason}: {result.Status} — {result.Message}");
            bool retry = (result.Status is ApplyStatus.Failed or ApplyStatus.NotFound) && attempt < request.Attempts;
            if (!retry) return result;
            lock (_gate)
            {
                if (_pending is not null) return result; // a newer request takes over
            }
            Thread.Sleep(2000);
        }
    }

    void OnApplied(ApplyRequest request, ApplyResult result)
    {
        bool adopted = request.Adopt && _firstRun && result is { Status: ApplyStatus.AlreadySet, Block: not null };
        if (adopted)
        {
            _firstRun = false;
            _settings.SetFromBlock(result.Block, result.Model);
            _settings.Model = result.Model?.Id;
            _form?.Reload();
        }
        if (request.Adopt && !adopted && result.Status == ApplyStatus.AlreadySet)
            return; // the user edited meanwhile; the edit's own write reports the status

        _status = result.Status switch
        {
            ApplyStatus.Written or ApplyStatus.AlreadySet =>
                Text.AppliedAt((adopted ? _settings : request.Settings).Describe(result.Model), DateTime.Now),
            ApplyStatus.NotFound => Text.StatusNotFound,
            ApplyStatus.Unsupported => Text.StatusUnsupported(result.UnknownId),
            _ => Text.StatusFailed(result.Message),
        };
        UpdateTooltip();
        _form?.SetStatus(_status);

        bool modelChanged = false;
        if (result is { Status: ApplyStatus.Written or ApplyStatus.AlreadySet, Model: { } model } && model.Id != _settings.Model)
        {
            _settings.Model = model.Id;
            modelChanged = true;
            _form?.Reload();
        }

        if (_firstRun) return; // nothing chosen yet, so no settings.json to keep the block in

        // Settings from before the light box (or another model's): take what the keyboard shows now. The write left
        // bytes 24..31 alone, since the request had no light box; an edit made meanwhile has set one and wins.
        bool boxTaken = false;
        if (_settings.LightBox is null
            && result is { Status: ApplyStatus.Written or ApplyStatus.AlreadySet, Block: { } read, Model.LightBox: true })
        {
            _settings.LightBox = LightBoxSettings.FromBlock(read);
            boxTaken = true;
            _form?.Reload();
        }

        if (adopted || modelChanged || boxTaken || result.Block is { } block && Convert.ToHexString(block) != _settings.LastGoodBlock)
        {
            if (result.Block is not null) _settings.LastGoodBlock = Convert.ToHexString(result.Block);
            SaveSettings();
        }
    }

    void UpdateTooltip()
    {
        string text = Text.AppTitle + "\n" + _status;
        _tray.Text = text.Length > 127 ? text[..127] : text;
    }

    void ShowSettings()
    {
        if (_form is null)
        {
            _form = new SettingsForm(_settings, _autostart);
            _form.SettingsChanged += () => Restart(_editDelay);
            _form.AutostartToggled += SetAutostart;
            _form.UpdatesToggled += SetCheckUpdates;
            _form.FormClosed += (_, _) =>
            {
                FlushEdits();
                _form = null;
            };
            _form.SetStatus(_status);
            _form.SetUpdate(_update);
        }
        _form.Show();
        if (_form.WindowState == FormWindowState.Minimized) _form.WindowState = FormWindowState.Normal;
        _form.Activate();
    }

    void SetAutostart(bool enable)
    {
        try
        {
            if (enable) Autostart.Enable(Environment.ProcessPath!);
            else Autostart.Disable();
            Log.Write(enable ? Text.AutostartOn : Text.AutostartOff);
            ShowAutostart(enable);
        }
        catch (Exception e) when (e is InvalidOperationException or IOException or Win32Exception)
        {
            Log.Write(Text.AutostartFailed(e.Message));
            ShowAutostart(_autostart);
            MessageBox.Show(Text.AutostartError(e.Message), Text.AppTitle,
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    void SetCheckUpdates(bool enable)
    {
        _settings.CheckUpdates = enable;
        // On first run nothing is saved yet: the choice goes into settings.json together with the lighting.
        if (!_firstRun) SaveSettings();
        _updateTimer.Stop();
        if (!enable) return;
        _updateTimer.Interval = UpdateCheckPeriodMs;
        _updateTimer.Start();
        CheckForUpdates();
    }

    void CheckForUpdates()
    {
        if (_checking) return;
        _checking = true;
        Task.Run(async () =>
        {
            ReleaseInfo? release = null;
            string? error = null;
            try { release = await UpdateCheck.FindNewer(); }
            catch (Exception e) { error = e.Message; }
            _ui.Post(_ => OnUpdateChecked(release, error), null);
        });
    }

    void OnUpdateChecked(ReleaseInfo? release, string? error)
    {
        _checking = false;
        if (!_settings.CheckUpdates) return; // turned off while the request was running
        if (error is not null)
        {
            if (!_updateErrorLogged) Log.Write(Text.UpdateFailed(error));
            _updateErrorLogged = true;
            return;
        }
        _updateErrorLogged = false;
        if (release is null) return;

        _update = release;
        _form?.SetUpdate(release);
        if (_announced == release.Version) return;
        _announced = release.Version;
        Log.Write(Text.UpdateFound(release.Version, release.Url));
        _tray.ShowBalloonTip(10_000, Text.UpdateTitle(release.Version), Text.UpdateBody, ToolTipIcon.Info);
    }

    void ShowAutostart(bool enabled)
    {
        _autostart = enabled;
        _autostartItem.Checked = enabled;
        _form?.SetAutostart(enabled);
    }
}
