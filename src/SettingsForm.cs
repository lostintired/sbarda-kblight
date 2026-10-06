using System.Diagnostics;

namespace KbLight;

/// <summary>Edits the shared <see cref="LightSettings"/> in place; every change raises <see cref="SettingsChanged"/>.</summary>
sealed class SettingsForm : Form
{
    readonly LightSettings _settings;
    readonly Label _model = new() { AutoSize = true, Margin = new Padding(3, 3, 3, 8) };
    readonly ComboBox _effect = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260, Anchor = AnchorStyles.Left };
    readonly TrackBar _brightness = BrightnessSlider();
    readonly Label _brightnessValue = ValueLabel();
    readonly TrackBar _speed = SpeedSlider();
    readonly Label _speedValue = ValueLabel();
    readonly Button _color = ColorButton();
    readonly CheckBox _multicolor = MulticolorBox();
    // Light box group (spec light-box), shown only for models that have one.
    readonly Label _boxTitle = new()
    {
        Text = KbLight.Text.LightBoxCaption, AutoSize = true, Font = new Font(DefaultFont, FontStyle.Bold), Margin = new Padding(3, 12, 3, 3),
    };
    readonly ComboBox _boxMode = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260, Anchor = AnchorStyles.Left };
    readonly TrackBar _boxBrightness = BrightnessSlider();
    readonly Label _boxBrightnessValue = ValueLabel();
    readonly TrackBar _boxSpeed = SpeedSlider();
    readonly Label _boxSpeedValue = ValueLabel();
    readonly Button _boxColor = ColorButton();
    readonly CheckBox _boxMulticolor = MulticolorBox();
    readonly List<Control> _boxRows = [];
    readonly CheckBox _reverse = new() { Text = KbLight.Text.ReverseDirection, AutoSize = true };
    readonly CheckBox _autostart = new() { Text = KbLight.Text.StartWithWindows, AutoSize = true, Margin = new Padding(3, 12, 3, 3) };
    readonly CheckBox _checkUpdates = new() { Text = KbLight.Text.CheckUpdates, AutoSize = true };
    readonly Label _version = new() { Text = AppVersion.Text, AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(3, 3, 0, 3) };
    readonly LinkLabel _updateLink = new() { AutoSize = true, Visible = false, Margin = new Padding(0, 3, 3, 3) };
    ReleaseInfo? _update;
    readonly Label _status = new() { AutoSize = true, MaximumSize = new Size(380, 0), Margin = new Padding(3, 8, 3, 3) };
    bool _loading;

    public event Action? SettingsChanged;
    public event Action<bool>? AutostartToggled;
    public event Action<bool>? UpdatesToggled;

    public SettingsForm(LightSettings settings, bool autostart)
    {
        _settings = settings;

        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = KbLight.Text.AppTitle;
        Icon = AppFiles.LoadIcon(32);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);

        var close = new Button { Text = KbLight.Text.Close, AutoSize = true, Anchor = AnchorStyles.Right };
        close.Click += (_, _) => Close();
        CancelButton = close;
        var log = NewLink(KbLight.Text.LogLink, OpenLog);
        var models = NewLink(KbLight.Text.ModelsLink, OpenModels);
        var links = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty, Anchor = AnchorStyles.Left };
        links.Controls.AddRange([log, models]);

        var colorRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        colorRow.Controls.AddRange([_color, _multicolor]);
        var boxColorRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        boxColorRow.Controls.AddRange([_boxColor, _boxMulticolor]);
        var bottom = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Dock = DockStyle.Fill, Margin = new Padding(0, 8, 0, 0) };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.Controls.Add(links, 0, 0);
        bottom.Controls.Add(close, 1, 0);

        var grid = new TableLayoutPanel { AutoSize = true, ColumnCount = 3 };
        for (int i = 0; i < 3; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.Controls.Add(_model, 0, grid.RowCount);
        grid.SetColumnSpan(_model, 3);
        grid.RowCount++;
        AddRow(grid, KbLight.Text.EffectCaption, _effect, span: 2);
        AddRow(grid, KbLight.Text.BrightnessCaption, _brightness, _brightnessValue);
        AddRow(grid, KbLight.Text.SpeedCaption, _speed, _speedValue);
        AddRow(grid, KbLight.Text.ColorCaption, colorRow, span: 2);
        AddRow(grid, "", _reverse, span: 2);
        grid.Controls.Add(_boxTitle, 0, grid.RowCount);
        grid.SetColumnSpan(_boxTitle, 3);
        grid.RowCount++;
        _boxRows.Add(_boxTitle);
        _boxRows.AddRange(AddRow(grid, KbLight.Text.LightBoxModeCaption, _boxMode, span: 2));
        _boxRows.AddRange(AddRow(grid, KbLight.Text.BrightnessCaption, _boxBrightness, _boxBrightnessValue));
        _boxRows.AddRange(AddRow(grid, KbLight.Text.SpeedCaption, _boxSpeed, _boxSpeedValue));
        _boxRows.AddRange(AddRow(grid, KbLight.Text.ColorCaption, boxColorRow, span: 2));
        AddRow(grid, "", _autostart, span: 2);
        AddRow(grid, "", _checkUpdates, span: 2);
        grid.Controls.Add(_status, 0, grid.RowCount);
        grid.SetColumnSpan(_status, 3);
        grid.RowCount++;
        var versionRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        versionRow.Controls.AddRange([_version, _updateLink]);
        grid.Controls.Add(versionRow, 0, grid.RowCount);
        grid.SetColumnSpan(versionRow, 3);
        grid.RowCount++;
        grid.Controls.Add(bottom, 0, grid.RowCount);
        grid.SetColumnSpan(bottom, 3);
        grid.RowCount++;
        Controls.Add(grid);
        ResumeLayout();

        FillEffects();
        LoadValues(autostart);

        _effect.SelectedIndexChanged += (_, _) => Edit(() => _settings.Effect = ((Effect)_effect.SelectedItem!).Id);
        _brightness.ValueChanged += (_, _) => Edit(() => _settings.Brightness = _brightness.Value);
        _speed.ValueChanged += (_, _) => Edit(() => _settings.Speed = _speed.Value);
        _multicolor.CheckedChanged += (_, _) => Edit(() => _settings.Multicolor = _multicolor.Checked);
        _reverse.CheckedChanged += (_, _) => Edit(() => _settings.ReverseDirection = _reverse.Checked);
        _color.Click += (_, _) => PickColor(_settings.GetColor(), _settings.SetColor);
        _boxMode.SelectedIndexChanged += (_, _) => EditBox(b => b with { Mode = ((LightBoxMode)_boxMode.SelectedItem!).Id });
        _boxBrightness.ValueChanged += (_, _) => EditBox(b => b with { Brightness = _boxBrightness.Value });
        _boxSpeed.ValueChanged += (_, _) => EditBox(b => b with { Speed = _boxSpeed.Value });
        _boxMulticolor.CheckedChanged += (_, _) => EditBox(b => b with { Multicolor = _boxMulticolor.Checked });
        _boxColor.Click += (_, _) => PickColor(Box.GetColor(), c => _settings.LightBox = Box with { Rgb = LightSettings.FormatColor(c) });
        _autostart.CheckedChanged += (_, _) => { if (!_loading) AutostartToggled?.Invoke(_autostart.Checked); };
        _checkUpdates.CheckedChanged += (_, _) => { if (!_loading) UpdatesToggled?.Invoke(_checkUpdates.Checked); };
        _updateLink.LinkClicked += (_, _) => { if (_update is not null) UpdateCheck.Open(_update); };
        if (Application.IsDarkModeEnabled)
            _updateLink.LinkColor = _updateLink.ActiveLinkColor = _updateLink.VisitedLinkColor = Color.FromArgb(0x60, 0xCD, 0xFF);
    }

    public void SetStatus(string text) => _status.Text = text;

    /// <summary>Shows "version N is available" after the version line, or hides it.</summary>
    public void SetUpdate(ReleaseInfo? release)
    {
        _update = release;
        _version.Text = release is null ? AppVersion.Text : AppVersion.Text + " ·";
        _updateLink.Text = release is null ? "" : KbLight.Text.UpdateLink(release.Version);
        _updateLink.Visible = release is not null;
    }

    public void SetAutostart(bool enabled)
    {
        _loading = true;
        _autostart.Checked = enabled;
        _loading = false;
    }

    /// <returns>the controls of the row, to show or hide it as a whole</returns>
    static List<Control> AddRow(TableLayoutPanel grid, string caption, Control control, Control? extra = null, int span = 1)
    {
        int row = grid.RowCount++;
        var controls = new List<Control> { control };
        if (caption.Length > 0)
        {
            var label = new Label { Text = caption, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 3, 12, 3) };
            grid.Controls.Add(label, 0, row);
            controls.Add(label);
        }
        grid.Controls.Add(control, 1, row);
        if (span > 1) grid.SetColumnSpan(control, span);
        if (extra is not null)
        {
            grid.Controls.Add(extra, 2, row);
            controls.Add(extra);
        }
        return controls;
    }

    static TrackBar BrightnessSlider() => new()
    {
        Maximum = 100, SmallChange = 5, LargeChange = 25, TickStyle = TickStyle.None, AutoSize = false, Size = new Size(260, 30),
    };

    static TrackBar SpeedSlider() => new()
    {
        Maximum = 4, LargeChange = 1, TickStyle = TickStyle.None, AutoSize = false, Size = new Size(260, 30),
    };

    static Label ValueLabel() => new() { AutoSize = true, Anchor = AnchorStyles.Left };

    static Button ColorButton() => new() { FlatStyle = FlatStyle.Flat, Size = new Size(56, 26), Margin = new Padding(3, 3, 12, 3) };

    static CheckBox MulticolorBox() => new() { Text = KbLight.Text.Multicolor, AutoSize = true, Anchor = AnchorStyles.Left };

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
        _effect.Items.Clear();
        foreach (var effect in (model ?? KeyboardModels.Default)?.Effects ?? []) _effect.Items.Add(effect);
        if (_boxMode.Items.Count == 0) _boxMode.Items.AddRange([.. LightBoxMode.All]);
        bool lightBox = (model ?? KeyboardModels.Default)?.LightBox == true;
        foreach (var control in _boxRows) control.Visible = lightBox;
    }

    /// <summary>The light box the window shows: the saved one, or the defaults until one is taken or edited.</summary>
    LightBoxSettings Box => _settings.LightBox ?? new();

    Effect? CurrentEffect() => _effect.Items.Cast<Effect>().FirstOrDefault(e => e.Id == _settings.Effect);

    void LoadValues(bool autostart)
    {
        _loading = true;
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
        var options = CurrentEffect()?.Options ?? 0;
        _brightness.Enabled = options.HasFlag(EffectOptions.Brightness);
        _speed.Enabled = options.HasFlag(EffectOptions.Speed);
        _multicolor.Enabled = options.HasFlag(EffectOptions.Multicolor);
        _color.Enabled = options.HasFlag(EffectOptions.Color) && !(_multicolor.Enabled && _multicolor.Checked);
        _reverse.Enabled = (options & EffectOptions.AnyDirection) != 0;

        _brightnessValue.Text = $"{_brightness.Value}%";
        _speedValue.Text = KbLight.Text.SpeedValue(_speed.Value + 1);
        ShowSwatch(_color, _settings.GetColor());

        var boxOptions = LightBoxMode.Find(Box.Mode).Options;
        _boxBrightness.Enabled = boxOptions.HasFlag(EffectOptions.Brightness);
        _boxSpeed.Enabled = boxOptions.HasFlag(EffectOptions.Speed);
        _boxMulticolor.Enabled = boxOptions.HasFlag(EffectOptions.Multicolor);
        _boxColor.Enabled = boxOptions.HasFlag(EffectOptions.Color) && !(_boxMulticolor.Enabled && _boxMulticolor.Checked);
        _boxBrightnessValue.Text = $"{_boxBrightness.Value}%";
        _boxSpeedValue.Text = KbLight.Text.SpeedValue(_boxSpeed.Value + 1);
        ShowSwatch(_boxColor, Box.GetColor());
    }

    /// <summary>Paints the color sample; a disabled button gets a muted one.</summary>
    static void ShowSwatch(Button button, Color color)
    {
        button.BackColor = button.Enabled ? color : Color.FromArgb(color.A, (color.R + 128) / 2, (color.G + 128) / 2, (color.B + 128) / 2);
        button.FlatAppearance.BorderColor = SystemColors.ControlDark;
    }

    void PickColor(Color current, Action<Color> set)
    {
        using var dialog = new ColorDialog { Color = current, FullOpen = true };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            Edit(() => set(dialog.Color));
    }

    static LinkLabel NewLink(string text, Action open)
    {
        var link = new LinkLabel { Text = text, AutoSize = true, Margin = new Padding(3, 3, 12, 3) };
        if (Application.IsDarkModeEnabled)
            link.LinkColor = link.ActiveLinkColor = link.VisitedLinkColor = Color.FromArgb(0x60, 0xCD, 0xFF);
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
