using System.Diagnostics;

namespace KbLight;

sealed class SettingsForm : Form
{
    readonly LightSettings _settings;
    readonly LightingSwitch _enabled = new() { AccessibleName = KbLight.Text.KeysEnabled };
    readonly LightingSwitch _boxEnabled = new() { AccessibleName = KbLight.Text.BoxEnabled };
    readonly Label _model = new() { AutoSize = true, Anchor = AnchorStyles.Right };
    readonly Label _device = new() { AutoSize = true, MaximumSize = new Size(245, 0), Margin = new Padding(0, 0, 0, 8) };
    readonly ComboBox _effect = EffectList();
    readonly LightingSlider _brightness = new();
    readonly Label _brightnessValue = ValueLabel();
    readonly LightingSlider _speed = new() { Maximum = 4 };
    readonly Label _speedValue = ValueLabel();
    readonly LightingColorPicker _color = new(KbLight.Text.KeysCaption);
    readonly CheckBox _multicolor = Check(KbLight.Text.Multicolor);
    readonly ComboBox _boxMode = EffectList();
    readonly LightingSlider _boxBrightness = new();
    readonly Label _boxBrightnessValue = ValueLabel();
    readonly LightingSlider _boxSpeed = new() { Maximum = 4 };
    readonly Label _boxSpeedValue = ValueLabel();
    readonly LightingColorPicker _boxColor = new(KbLight.Text.LightBoxCaption);
    readonly CheckBox _boxMulticolor = Check(KbLight.Text.Multicolor);
    readonly CheckBox _reverse = Check(KbLight.Text.ReverseDirection);
    readonly CheckBox _autostart = Check(KbLight.Text.StartWithWindows);
    readonly CheckBox _checkUpdates = Check(KbLight.Text.CheckUpdates);
    readonly LightingSwitch _quietEnabled = new() { AccessibleName = KbLight.Text.QuietHoursEnabled, Width = 42, ShowStateCaption = false };
    readonly LightingTimePicker _quietStart = TimePicker();
    readonly LightingTimePicker _quietEnd = TimePicker();
    readonly Label _quietState = new() { AutoSize = true, MaximumSize = new Size(240, 0), Margin = new Padding(0, 6, 0, 0) };
    readonly Label _version = new() { Text = AppVersion.Text, AutoSize = true, Margin = new Padding(0, 12, 0, 0) };
    readonly LinkLabel _updateLink = new() { AutoSize = true, Visible = false, Margin = new Padding(0, 4, 0, 0) };
    readonly Label _status = new() { AutoSize = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 0, 12, 0) };
    readonly Panel _body = new() { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(16, 0, 16, 12) };
    readonly TableLayoutPanel _columns = new() { ColumnCount = 3, RowCount = 1, Margin = Padding.Empty, Padding = Padding.Empty };
    readonly TableLayoutPanel _boxPanel;
    readonly TableLayoutPanel _footer;
    ReleaseInfo? _update;
    bool _loading;
    bool _fitting;
    readonly List<Font> _ownedFonts = [];

    public event Action? SettingsChanged;
    public event Action<bool>? AutostartToggled;
    public event Action<bool>? UpdatesToggled;

    public SettingsForm(LightSettings settings, bool autostart)
    {
        _settings = settings;
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = OwnFont(9);
        Text = KbLight.Text.AppTitle;
        Icon = AppFiles.LoadIcon(32);
        BackColor = LightingTheme.Background;
        ForeColor = LightingTheme.Foreground;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1000, 632);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Padding = new Padding(22, 8, 22, 8), Margin = Padding.Empty };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 44));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.Controls.Add(new LightingIcon { Glyph = LightingGlyph.Brand, Size = new Size(34, 36), Anchor = AnchorStyles.Left }, 0, 0);
        var brand = Stack();
        brand.Controls.Add(new Label { Text = "KbLight", AutoSize = true, Font = OwnFont(17, FontStyle.Bold), Margin = Padding.Empty });
        brand.Controls.Add(new Label { Text = KbLight.Text.LightingControl, AutoSize = true, Margin = Padding.Empty });
        header.Controls.Add(brand, 1, 0); header.Controls.Add(_model, 2, 0);
        layout.Controls.Add(header, 0, 0);

        _columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
        _columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
        _columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
        _columns.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var keys = Zone(KbLight.Text.KeysCaption, LightingGlyph.Keys, _enabled, _effect, _brightness, _brightnessValue, _speed, _speedValue, _color, _multicolor, _reverse);
        _boxPanel = Zone(KbLight.Text.LightBoxCaption, LightingGlyph.Box, _boxEnabled, _boxMode, _boxBrightness, _boxBrightnessValue, _boxSpeed, _boxSpeedValue, _boxColor, _boxMulticolor, null);
        _columns.Controls.Add(keys, 0, 0); _columns.Controls.Add(_boxPanel, 1, 0); _columns.Controls.Add(ProgramPanel(), 2, 0);
        _body.Controls.Add(_columns); layout.Controls.Add(_body, 0, 1);
        _footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(20, 9, 20, 9), Margin = Padding.Empty, BackColor = LightingTheme.Panel };
        _footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        _footer.Controls.Add(_status, 0, 0);
        var close = new Button { Text = KbLight.Text.Close, Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, BackColor = LightingTheme.ButtonSurface, UseVisualStyleBackColor = false, Margin = new Padding(0, 4, 0, 4) };
        close.FlatAppearance.BorderColor = LightingTheme.Border;
        close.Click += (_, _) => Close(); CancelButton = close;
        _footer.Controls.Add(close, 1, 0); layout.Controls.Add(_footer, 0, 2);
        Controls.Add(layout);
        LightingTheme.Apply(this);
        foreach (var l in new[] { _brightnessValue, _speedValue, _boxBrightnessValue, _boxSpeedValue }) l.ForeColor = LightingTheme.Foreground;
        FillEffects(); LoadValues(autostart);
        _enabled.CheckedChanged += (_, _) => Edit(() => _settings.Enabled = _enabled.Checked);
        _boxEnabled.CheckedChanged += (_, _) => Edit(() => _settings.LightBoxEnabled = _boxEnabled.Checked);
        _quietEnabled.CheckedChanged += (_, _) => Edit(() => _settings.QuietHours = _settings.QuietHours with { Enabled = _quietEnabled.Checked });
        _quietStart.ValueChanged += (_, _) => Edit(() => _settings.QuietHours = _settings.QuietHours with { Start = FormatTime(_quietStart) });
        _quietEnd.ValueChanged += (_, _) => Edit(() => _settings.QuietHours = _settings.QuietHours with { End = FormatTime(_quietEnd) });
        _effect.SelectedIndexChanged += (_, _) => { if (_effect.SelectedItem is Effect e) Edit(() => _settings.Effect = e.Id); };
        _brightness.ValueChanged += (_, _) => Edit(() => _settings.Brightness = _brightness.Value);
        _speed.ValueChanged += (_, _) => Edit(() => _settings.Speed = _speed.Value);
        _multicolor.CheckedChanged += (_, _) => Edit(() => _settings.Multicolor = _multicolor.Checked);
        _reverse.CheckedChanged += (_, _) => Edit(() => _settings.ReverseDirection = _reverse.Checked);
        _color.ColorChanged += (_, _) => Edit(() => _settings.SetColor(_color.SelectedColor));
        _boxMode.SelectedIndexChanged += (_, _) => { if (_boxMode.SelectedItem is LightBoxMode m) EditBox(b => b with { Mode = m.Id }); };
        _boxBrightness.ValueChanged += (_, _) => EditBox(b => b with { Brightness = _boxBrightness.Value });
        _boxSpeed.ValueChanged += (_, _) => EditBox(b => b with { Speed = _boxSpeed.Value });
        _boxMulticolor.CheckedChanged += (_, _) => EditBox(b => b with { Multicolor = _boxMulticolor.Checked });
        _boxColor.ColorChanged += (_, _) => EditBox(b => b with { Rgb = LightSettings.FormatColor(_boxColor.SelectedColor) });
        _autostart.CheckedChanged += (_, _) => { if (!_loading) AutostartToggled?.Invoke(_autostart.Checked); };
        _checkUpdates.CheckedChanged += (_, _) => { if (!_loading) UpdatesToggled?.Invoke(_checkUpdates.Checked); };
        _updateLink.LinkClicked += (_, _) => { if (_update is not null) UpdateCheck.Open(_update); };
        Shown += (_, _) => FitWorkingArea();
        DpiChanged += (_, _) => BeginInvoke(FitWorkingArea);
        _body.SizeChanged += (_, _) => SizeColumns();
        ResumeLayout(true);
        SizeColumns();
    }

    static ComboBox EffectList() => new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, Margin = Padding.Empty };
    static CheckBox Check(string caption) => new LightingCheckBox() { Text = caption, AccessibleName = caption, AutoSize = true, MaximumSize = new Size(280, 0), Margin = new Padding(0, 4, 0, 4), UseVisualStyleBackColor = false };
    static Label ValueLabel() => new() { AutoSize = true, Anchor = AnchorStyles.Right, Margin = Padding.Empty };
    static LightingTimePicker TimePicker() => new() { Width = 112, Margin = new Padding(0, 4, 0, 0) };
    static FlowLayoutPanel Stack() => new() { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = Padding.Empty, Dock = DockStyle.Top };
    static Label Caption(string text) => new() { Text = text, AutoSize = true, ForeColor = LightingTheme.Muted, Margin = new Padding(0, 0, 0, 5) };

    TableLayoutPanel Zone(string title, LightingGlyph glyph, LightingSwitch toggle, ComboBox effect, LightingSlider brightness, Label brightnessValue, LightingSlider speed, Label speedValue, LightingColorPicker picker, CheckBox multi, CheckBox? reverse)
    {
        var panel = new TableLayoutPanel { ColumnCount = 1, RowCount = 8, Dock = DockStyle.Fill, BackColor = LightingTheme.Panel, Padding = new Padding(16, 12, 16, 10), Margin = new Padding(0, 0, 12, 0) };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (int h in new[] { 40, 50, 49, 55, 29, 207, 28 }) panel.RowStyles.Add(new RowStyle(SizeType.Absolute, h));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var head = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Margin = Padding.Empty };
        head.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 38)); head.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); head.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        head.Controls.Add(new LightingIcon { Glyph = glyph, Anchor = AnchorStyles.Left }, 0, 0);
        head.Controls.Add(new Label { Text = title, AutoSize = true, Font = OwnFont(10, FontStyle.Bold), Anchor = AnchorStyles.Left, Margin = Padding.Empty }, 1, 0);
        toggle.Anchor = AnchorStyles.Right; head.Controls.Add(toggle, 2, 0); panel.Controls.Add(head, 0, 0);
        var effectRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
        effectRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        effectRow.RowStyles.Add(new RowStyle(SizeType.Absolute, 20)); effectRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        effectRow.Controls.Add(Caption(glyph == LightingGlyph.Keys ? KbLight.Text.EffectCaption : KbLight.Text.LightBoxModeCaption), 0, 0);
        effectRow.Controls.Add(effect, 0, 1);
        panel.Controls.Add(effectRow, 0, 1);
        panel.Controls.Add(SliderRow(title, KbLight.Text.BrightnessCaption, brightness, brightnessValue), 0, 2);
        panel.Controls.Add(SliderRow(title, KbLight.Text.SpeedCaption, speed, speedValue), 0, 3);
        var colors = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
        colors.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); colors.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        colors.Controls.Add(Caption(KbLight.Text.ColorCaption), 0, 0); colors.Controls.Add(multi, 1, 0);
        multi.AccessibleName = title + ": " + KbLight.Text.Multicolor;
        panel.Controls.Add(colors, 0, 4); picker.Dock = DockStyle.Fill; picker.Margin = Padding.Empty; panel.Controls.Add(picker, 0, 5);
        if (reverse is not null) panel.Controls.Add(reverse, 0, 6);
        return panel;
    }

    static TableLayoutPanel SliderRow(string zone, string caption, LightingSlider slider, Label value)
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Margin = Padding.Empty };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.RowStyles.Add(new RowStyle(SizeType.Absolute, 20)); row.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        row.Controls.Add(Caption(caption), 0, 0); row.Controls.Add(value, 1, 0);
        slider.Dock = DockStyle.Fill; slider.Margin = Padding.Empty; slider.AccessibleName = zone + ": " + caption;
        row.Controls.Add(slider, 0, 1); row.SetColumnSpan(slider, 2);
        return row;
    }

    TableLayoutPanel ProgramPanel()
    {
        var panel = new TableLayoutPanel { ColumnCount = 1, Dock = DockStyle.Fill, BackColor = LightingTheme.SecondaryPanel, Padding = new Padding(16, 14, 16, 10), Margin = Padding.Empty };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        void Add(Control c) { panel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); panel.Controls.Add(c, 0, panel.RowCount++); }
        Add(new Label { Text = KbLight.Text.ProgramCaption, AutoSize = true, Font = OwnFont(10, FontStyle.Bold), Margin = new Padding(0, 0, 0, 20) });
        var quiet = new TableLayoutPanel { Dock = DockStyle.Top, Height = 30, ColumnCount = 2, Margin = Padding.Empty };
        quiet.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); quiet.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42));
        quiet.Controls.Add(new Label { Text = KbLight.Text.QuietHoursCaption, AutoSize = true, Font = OwnFont(9, FontStyle.Bold), Anchor = AnchorStyles.Left, Margin = Padding.Empty }, 0, 0);
        quiet.Controls.Add(_quietEnabled, 1, 0); Add(quiet);
        var times = new TableLayoutPanel { ColumnCount = 2, Dock = DockStyle.Top, Height = 58, Margin = new Padding(0, 4, 0, 0) };
        times.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); times.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        times.Controls.Add(Caption(KbLight.Text.QuietOffAt), 0, 0); times.Controls.Add(Caption(KbLight.Text.QuietOnAt), 1, 0);
        _quietStart.AccessibleName = KbLight.Text.QuietOffAt; _quietEnd.AccessibleName = KbLight.Text.QuietOnAt;
        times.Controls.Add(_quietStart, 0, 1); times.Controls.Add(_quietEnd, 1, 1); Add(times);
        Add(new Label { Text = KbLight.Text.QuietHoursHelp, AutoSize = true, MaximumSize = new Size(245, 0), Margin = new Padding(0, 5, 0, 0) });
        Add(_quietState);
        Add(Section(KbLight.Text.SettingsCaption)); Add(_autostart); Add(_checkUpdates);
        Add(Section(KbLight.Text.DeviceCaption));
        Add(_device);
        var links = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        links.Controls.Add(NewLink(KbLight.Text.LogLink, OpenLog)); links.Controls.Add(NewLink(KbLight.Text.ModelsLink, OpenModels)); Add(links);
        Add(_version); Add(_updateLink);
        return panel;
    }
    Label Section(string title) => new() { Text = title, AutoSize = true, Font = OwnFont(9, FontStyle.Bold), Margin = new Padding(0, 18, 0, 9) };

    Font OwnFont(float size, FontStyle style = FontStyle.Regular)
    {
        var font = new Font("Segoe UI", size, style);
        _ownedFonts.Add(font);
        return font;
    }
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        LightingTheme.DarkTitleBar(Handle);
    }
    protected override void Dispose(bool disposing)
    {
        var icon = disposing ? Icon : null;
        base.Dispose(disposing);
        if (!disposing) return;
        icon?.Dispose();
        foreach (var font in _ownedFonts) font.Dispose();
        _ownedFonts.Clear();
    }

    void SizeColumns()
    {
        float s = _boxPanel.RowStyles[0].Height / 40f;
        _columns.Size = new Size(Math.Max((int)(950 * s), _body.ClientSize.Width - _body.Padding.Horizontal), (int)(490 * s));
        _columns.Location = new Point(_body.Padding.Left + _body.AutoScrollPosition.X, _body.Padding.Top + _body.AutoScrollPosition.Y);
    }
    void FitWorkingArea()
    {
        if (_fitting) return;
        _fitting = true;
        try
        {
            var area = Screen.FromControl(this).WorkingArea;
            MaximumSize = area.Size;
            Size = new Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height));
            Location = new Point(Math.Clamp(Left, area.Left, Math.Max(area.Left, area.Right - Width)), Math.Clamp(Top, area.Top, Math.Max(area.Top, area.Bottom - Height)));
            SizeColumns();
        }
        finally { _fitting = false; }
    }
    public void SetStatus(string text) => _status.Text = text;
    public void SetQuietHoursState(QuietHoursState state)
    {
        _quietState.Text = !state.IsValid ? KbLight.Text.QuietHoursInvalid : state.SameTime ? KbLight.Text.QuietHoursSameTime : state.Active ? KbLight.Text.QuietHoursActive(state.End) : "";
    }
    public void SetUpdate(ReleaseInfo? release)
    {
        _update = release;
        _version.Text = AppVersion.Text;
        _updateLink.Text = release is null ? "" : KbLight.Text.UpdateLink(release.Version);
        _updateLink.Visible = release is not null;
    }
    public void SetAutostart(bool enabled) { _loading = true; _autostart.Checked = enabled; _loading = false; }

    static string FormatTime(LightingTimePicker picker) => picker.Value.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);

    static void LoadTime(LightingTimePicker picker, string? text, int fallbackHour)
    {
        var time = QuietHoursState.TryTime(text, out var parsed) ? parsed : new TimeOnly(fallbackHour, 0);
        picker.Value = DateTime.Today.Add(time.ToTimeSpan());
    }

    /// <summary>
    /// Shows settings that changed outside the window: lighting taken from the keyboard on first run,
    /// or another keyboard model with its own effect list.
    /// </summary>
    public void Reload()
    {
        _loading = true;
        FillEffects();
        LoadValues(_autostart.Checked);
    }

    void FillEffects()
    {
        var model = KeyboardModels.Find(_settings.Model);
        _model.Text = KbLight.Text.KeyboardLine(model?.Name);
        _device.Text = KbLight.Text.KeyboardLine(model?.Name) + (model is null ? "" : Environment.NewLine + model.Id);
        _effect.Items.Clear();
        foreach (var effect in (model ?? KeyboardModels.Default)?.Effects ?? []) _effect.Items.Add(effect);
        if (_boxMode.Items.Count == 0) _boxMode.Items.AddRange([.. LightBoxMode.All]);
        bool lightBox = (model ?? KeyboardModels.Default)?.LightBox == true;
        _boxPanel.Visible = lightBox;
        _columns.ColumnStyles[1].Width = lightBox ? 34 : 0;
        _columns.ColumnStyles[0].Width = lightBox ? 34 : 68;
        _columns.PerformLayout();
    }

    /// <summary>The light box the window shows: the saved one, or the defaults until one is taken or edited.</summary>
    LightBoxSettings Box => _settings.LightBox ?? new();

    Effect? CurrentEffect() => _effect.Items.Cast<Effect>().FirstOrDefault(e => e.Id == _settings.Effect);

    void LoadValues(bool autostart)
    {
        _loading = true;
        _enabled.Checked = _settings.Enabled;
        _boxEnabled.Checked = _settings.LightBoxEnabled;
        _quietEnabled.Checked = _settings.QuietHours.Enabled;
        LoadTime(_quietStart, _settings.QuietHours.Start, 23);
        LoadTime(_quietEnd, _settings.QuietHours.End, 9);
        _effect.SelectedItem = CurrentEffect();
        _brightness.Value = Math.Clamp(_settings.Brightness, 0, 100);
        _speed.Value = Math.Clamp(_settings.Speed, 0, 4);
        _multicolor.Checked = _settings.Multicolor;
        _reverse.Checked = _settings.ReverseDirection;
        _boxMode.SelectedItem = LightBoxMode.Find(Box.Mode);
        _boxBrightness.Value = Math.Clamp(Box.Brightness, 0, 100);
        _boxSpeed.Value = Math.Clamp(Box.Speed, 0, 4);
        _boxMulticolor.Checked = Box.Multicolor;
        _autostart.Checked = autostart;
        _checkUpdates.Checked = _settings.CheckUpdates;
        _loading = false;
        UpdateControls();
    }

    void Edit(Action change)
    {
        if (_loading) return;
        change();
        UpdateControls();
        SettingsChanged?.Invoke();
    }

    void EditBox(Func<LightBoxSettings, LightBoxSettings> change) => Edit(() => _settings.LightBox = change(Box));

    void UpdateControls()
    {
        _effect.Enabled = true;
        var options = CurrentEffect()?.Options ?? 0;
        _brightness.Enabled = options.HasFlag(EffectOptions.Brightness);
        _speed.Enabled = options.HasFlag(EffectOptions.Speed);
        _multicolor.Enabled = options.HasFlag(EffectOptions.Multicolor);
        _color.Enabled = options.HasFlag(EffectOptions.Color) && !(_multicolor.Enabled && _multicolor.Checked);
        _reverse.Enabled = (options & EffectOptions.AnyDirection) != 0;

        _brightnessValue.Text = $"{_brightness.Value}%";
        _speedValue.Text = KbLight.Text.SpeedValue(_speed.Value + 1);
        _color.SelectedColor = _settings.GetColor();

        _boxMode.Enabled = true;
        var boxOptions = LightBoxMode.Find(Box.Mode).Options;
        _boxBrightness.Enabled = boxOptions.HasFlag(EffectOptions.Brightness);
        _boxSpeed.Enabled = boxOptions.HasFlag(EffectOptions.Speed);
        _boxMulticolor.Enabled = boxOptions.HasFlag(EffectOptions.Multicolor);
        _boxColor.Enabled = boxOptions.HasFlag(EffectOptions.Color) && !(_boxMulticolor.Enabled && _boxMulticolor.Checked);
        _boxBrightnessValue.Text = $"{_boxBrightness.Value}%";
        _boxSpeedValue.Text = KbLight.Text.SpeedValue(_boxSpeed.Value + 1);
        _boxColor.SelectedColor = Box.GetColor();
        _quietStart.Enabled = _quietEnd.Enabled = _settings.QuietHours.Enabled;
        SetQuietHoursState(QuietHoursState.At(_settings.QuietHours, LocalClock.Now));
    }

    static LinkLabel NewLink(string text, Action open)
    {
        var link = new LinkLabel { Text = text, AutoSize = true, Margin = new Padding(3, 3, 12, 3) };
        link.LinkColor = link.ActiveLinkColor = link.VisitedLinkColor = LightingTheme.Link;
        link.LinkClicked += (_, _) => open();
        return link;
    }

    static void OpenModels()
    {
        Directory.CreateDirectory(KeyboardModels.Folder);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{KeyboardModels.Folder}\"") { UseShellExecute = true });
    }

    static void OpenLog()
    {
        if (!File.Exists(AppFiles.Log)) File.WriteAllText(AppFiles.Log, "");
        Process.Start(new ProcessStartInfo("notepad.exe", $"\"{AppFiles.Log}\"") { UseShellExecute = true });
    }
}
