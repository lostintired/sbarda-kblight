using System.ComponentModel;

namespace KbLight;

sealed class TrayApp : ApplicationContext
{
    public const string ShowSignalName = @"Local\KbLight.Show";

    readonly SynchronizationContext _ui = SynchronizationContext.Current!;
    readonly LightSettings _settings;
    readonly NotifyIcon _tray;
    readonly ToolStripMenuItem _autostartItem;
    readonly SystemWatcher _watcher = new();
    readonly System.Windows.Forms.Timer _arrivalDelay = new() { Interval = 1500 };
    readonly System.Windows.Forms.Timer _resumeDelay = new() { Interval = 3000 };
    readonly System.Windows.Forms.Timer _editDelay = new() { Interval = 300 };
    readonly EventWaitHandle _showSignal = new(false, EventResetMode.AutoReset, ShowSignalName);
    readonly RegisteredWaitHandle _showWait;
    SettingsForm? _form;
    bool _autostart;
    // No settings.json yet: requests read the keyboard's lighting instead of writing ours over it.
    bool _firstRun;
    string _status = "применяю…";

    // Applies run one at a time on a pool thread; a request queued meanwhile replaces older queued ones.
    readonly object _gate = new();
    readonly ManualResetEventSlim _idle = new(true);
    ApplyRequest? _pending;

    sealed record ApplyRequest(LightSettings Settings, bool Force, string Reason, int Attempts, bool Adopt);

    public TrayApp(bool showSettings)
    {
        _firstRun = !LightSettings.Exists;
        _settings = LightSettings.Load();

        _autostartItem = new ToolStripMenuItem("Запускать при входе в Windows", null, (_, _) => SetAutostart(!_autostart));
        var menu = new ContextMenuStrip();
        var settingsItem = new ToolStripMenuItem("Настройки подсветки…", null, (_, _) => ShowSettings());
        settingsItem.Font = new Font(settingsItem.Font, FontStyle.Bold);
        menu.Items.Add(settingsItem);
        menu.Items.Add("Применить сейчас", null, (_, _) => RequestApply(force: true, "вручную"));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_autostartItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Выход", null, (_, _) => ExitThread());

        _tray = new NotifyIcon
        {
            Icon = AppFiles.LoadIcon(SystemInformation.SmallIconSize.Width),
            ContextMenuStrip = menu,
            Visible = true,
        };
        _tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) ShowSettings(); };
        UpdateTooltip();

        _watcher.KeyboardArrived += () => Restart(_arrivalDelay);
        _watcher.Resumed += () => Restart(_resumeDelay);
        // A replugged keyboard has been powered off, so it gets the lighting unconditionally;
        // after sleep it usually still has it, so only a mismatch is rewritten.
        _arrivalDelay.Tick += (_, _) => { _arrivalDelay.Stop(); RequestApply(force: true, "клавиатура подключена", attempts: 3); };
        _resumeDelay.Tick += (_, _) => { _resumeDelay.Stop(); RequestApply(force: false, "выход из сна", attempts: 3); };
        _editDelay.Tick += (_, _) => FlushEdits();

        _showWait = ThreadPool.RegisterWaitForSingleObject(_showSignal,
            (_, _) => _ui.Post(_ => ShowSettings(), null), null, Timeout.Infinite, executeOnlyOnce: false);

        Log.Write($"запуск {Environment.ProcessPath}{(showSettings ? "" : " --tray")}");
        RequestApply(force: true, "запуск программы", attempts: 6);
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
        _form?.Close();
        _tray.Visible = false;
        _tray.Dispose();
        _watcher.Dispose();
        _showWait.Unregister(null);
        _showSignal.Dispose();
        Log.Write("выход");
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
        RequestApply(force: true, "изменены настройки");
    }

    void SaveSettings()
    {
        try { _settings.Save(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.Write("настройки не сохранены: " + e.Message);
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
        var want = LightState.From(request.Settings);
        byte[]? fallback = request.Settings.GetLastGoodBlock();
        for (int attempt = 1; ; attempt++)
        {
            var result = request.Adopt ? Keyboard.Read() : Keyboard.Apply(want, request.Force, fallback);
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
            _settings.SetFromBlock(result.Block);
            _settings.Model = result.Model?.Id;
            _form?.Reload();
        }
        if (request.Adopt && !adopted && result.Status == ApplyStatus.AlreadySet)
            return; // the user edited meanwhile; the edit's own write reports the status

        _status = result.Status switch
        {
            ApplyStatus.Written or ApplyStatus.AlreadySet =>
                $"{(adopted ? _settings : request.Settings).Describe(result.Model)} — применено в {DateTime.Now:HH:mm}",
            ApplyStatus.NotFound => "Клавиатура не найдена. Подсветка применится, когда она подключится.",
            ApplyStatus.Unsupported => $"Клавиатура {result.UnknownId} не знакома программе. Нужен файл модели — ссылка «Модели» в окне.",
            _ => "Не удалось применить: " + result.Message,
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
        if (adopted || modelChanged || result.Block is { } block && Convert.ToHexString(block) != _settings.LastGoodBlock)
        {
            if (result.Block is not null) _settings.LastGoodBlock = Convert.ToHexString(result.Block);
            SaveSettings();
        }
    }

    void UpdateTooltip()
    {
        string text = "Подсветка клавиатуры\n" + _status;
        _tray.Text = text.Length > 127 ? text[..127] : text;
    }

    void ShowSettings()
    {
        if (_form is null)
        {
            _form = new SettingsForm(_settings, _autostart);
            _form.SettingsChanged += () => Restart(_editDelay);
            _form.AutostartToggled += SetAutostart;
            _form.FormClosed += (_, _) =>
            {
                FlushEdits();
                _form = null;
            };
            _form.SetStatus(_status);
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
            Log.Write(enable ? "автозапуск включён" : "автозапуск выключен");
            ShowAutostart(enable);
        }
        catch (Exception e) when (e is InvalidOperationException or IOException or Win32Exception)
        {
            Log.Write("автозапуск: " + e.Message);
            ShowAutostart(_autostart);
            MessageBox.Show("Не удалось изменить автозапуск:\n" + e.Message, "Подсветка клавиатуры",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    void ShowAutostart(bool enabled)
    {
        _autostart = enabled;
        _autostartItem.Checked = enabled;
        _form?.SetAutostart(enabled);
    }
}
