using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Globalization;

namespace KbLight;

sealed class LightingColorPicker : UserControl
{
    readonly ColorField _field;
    readonly LightingSlider _hue = new() { Hue = true, Maximum = 360, Dock = DockStyle.Fill };
    readonly TextBox _hex = new() { BorderStyle = BorderStyle.FixedSingle, Dock = DockStyle.Fill, MaxLength = 7 };
    readonly Panel _sample = new() { Size = new Size(28, 26), Margin = new Padding(0, 0, 6, 0) };
    readonly Label _rgb = new() { AutoSize = true, ForeColor = LightingTheme.Muted, Margin = new Padding(0, 3, 0, 0) };
    readonly Label _error = new() { AutoSize = true, ForeColor = LightingTheme.Error, Margin = Padding.Empty };
    Color _color = Color.Red;
    double _h, _s = 1, _v = 1;
    bool _loading;
    public event EventHandler? ColorChanged;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color SelectedColor { get => _color; set => SetColor(value, false); }
    public LightingColorPicker(string zone)
    {
        BackColor = LightingTheme.Panel;
        Size = new Size(280, 207);
        AutoScaleMode = AutoScaleMode.Inherit;
        _field = new ColorField(this) { Dock = DockStyle.Fill, AccessibleName = zone + ": " + KbLight.Text.ColorCaption };
        _hue.AccessibleName = zone + ": " + KbLight.Text.HueCaption;
        _hex.AccessibleName = zone + ": HEX";
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Margin = Padding.Empty, BackColor = BackColor };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (int h in new[] { 64, 25, 27, 29, 22, 36 }) grid.RowStyles.Add(new RowStyle(SizeType.Absolute, h));
        grid.Controls.Add(_field, 0, 0); grid.Controls.Add(_hue, 0, 1);
        var presets = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty };
        foreach (string hex in new[] { "#FFFFFF", "#FF5252", "#FF8000", "#F2CE60", "#78D694", "#52C7D4", "#729BEB", "#C78ADB" })
        {
            var c = ColorTranslator.FromHtml(hex);
            var button = new LightingSwatch { Size = new Size(22, 22), Margin = new Padding(0, 2, 7, 0), FlatStyle = FlatStyle.Flat, BackColor = c, AccessibleName = zone + ": " + KbLight.Text.ColorPreset(hex), UseVisualStyleBackColor = false };
            button.FlatAppearance.BorderColor = LightingTheme.Muted;
            button.Click += (_, _) => SetColor(c, true);
            presets.Controls.Add(button);
        }
        grid.Controls.Add(presets, 0, 2);
        var hexRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Margin = Padding.Empty };
        hexRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34));
        hexRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34));
        hexRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        hexRow.Controls.Add(_sample, 0, 0);
        hexRow.Controls.Add(new Label { Text = "HEX", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = LightingTheme.Muted, Margin = Padding.Empty }, 1, 0);
        hexRow.Controls.Add(_hex, 2, 0);
        grid.Controls.Add(hexRow, 0, 3); grid.Controls.Add(_rgb, 0, 4);
        _error.MaximumSize = new Size(275, 0);
        grid.Controls.Add(_error, 0, 5);
        Controls.Add(grid);
        _hue.ValueChanged += (_, _) => { if (_loading) return; _h = _hue.Value; CommitHsv(); };
        _hex.TextChanged += (_, _) =>
        {
            if (_loading) return;
            string t = _hex.Text.Trim();
            if (t.StartsWith('#')) t = t[1..];
            if (t.Length == 6 && int.TryParse(t, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int n))
            {
                _hex.AccessibleDescription = "";
                SetColor(Color.FromArgb((n >> 16) & 255, (n >> 8) & 255, n & 255), true);
            }
            else { _error.Text = KbLight.Text.HexInvalid; _hex.AccessibleDescription = _error.Text; }
        };
        _hex.Leave += (_, _) => Sync();
        LightingTheme.Apply(this);
        _error.ForeColor = LightingTheme.Error;
        Sync();
    }

    void SetColor(Color color, bool notify)
    {
        bool changed = color.ToArgb() != _color.ToArgb();
        if (!changed) { Sync(); return; }
        _color = color;
        double max = Math.Max(color.R, Math.Max(color.G, color.B)) / 255d;
        double min = Math.Min(color.R, Math.Min(color.G, color.B)) / 255d;
        if (max > min) _h = color.GetHue();
        _s = max == 0 ? 0 : (max - min) / max;
        _v = max;
        Sync();
        if (notify && changed) ColorChanged?.Invoke(this, EventArgs.Empty);
    }
    void Sync()
    {
        _loading = true;
        _hex.Text = LightSettings.FormatColor(_color);
        _hex.SelectionStart = _hex.Text.Length;
        _hue.Value = (int)Math.Round(_h);
        _sample.BackColor = Enabled ? _color : Color.FromArgb((_color.R + 128) / 2, (_color.G + 128) / 2, (_color.B + 128) / 2);
        _rgb.Text = $"RGB   {_color.R} / {_color.G} / {_color.B}";
        _error.Text = ""; _hex.AccessibleDescription = "";
        _field.Invalidate();
        _loading = false;
    }
    void CommitHsv()
    {
        Color next = FromHsv(_h, _s, _v);
        bool changed = next.ToArgb() != _color.ToArgb();
        _color = next;
        Sync();
        if (changed) ColorChanged?.Invoke(this, EventArgs.Empty);
    }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); if (_field is not null) Sync(); }

    internal static Color FromHsv(double hue, double saturation, double value)
    {
        double h = (hue % 360) / 60, c = value * saturation, x = c * (1 - Math.Abs(h % 2 - 1)), m = value - c;
        (double r, double g, double b) = h switch { < 1 => (c, x, 0d), < 2 => (x, c, 0d), < 3 => (0d, c, x), < 4 => (0d, x, c), < 5 => (x, 0d, c), _ => (c, 0d, x) };
        return Color.FromArgb((int)Math.Round((r + m) * 255), (int)Math.Round((g + m) * 255), (int)Math.Round((b + m) * 255));
    }

    sealed class ColorField(LightingColorPicker owner) : Control
    {
        protected override void OnHandleCreated(EventArgs e)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
            TabStop = true; Cursor = Cursors.Cross; AccessibleRole = AccessibleRole.Slider;
            base.OnHandleCreated(e);
        }
        protected override AccessibleObject CreateAccessibilityInstance() => new FieldAccessibility(this, owner);
        sealed class FieldAccessibility(ColorField field, LightingColorPicker picker) : ControlAccessibleObject(field)
        {
            public override string? Value => $"{KbLight.Text.SaturationCaption}: {picker._s:P0}; {KbLight.Text.ColorValueCaption}: {picker._v:P0}";
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            if (ClientSize.Width < 2 || ClientSize.Height < 2) return;
            var r = ClientRectangle;
            using var a = new LinearGradientBrush(r, Color.White, FromHsv(owner._h, 1, 1), 0f);
            e.Graphics.FillRectangle(a, r);
            using var b = new LinearGradientBrush(r, Color.Transparent, Color.Black, 90f);
            e.Graphics.FillRectangle(b, r);
            if (!Enabled) { using var veil = new SolidBrush(Color.FromArgb(170, LightingTheme.Panel)); e.Graphics.FillRectangle(veil, r); }
            float x = (float)owner._s * (Width - 1), y = (float)(1 - owner._v) * (Height - 1);
            float scale = DeviceDpi / 96f;
            using var outline = new Pen(Color.Black, 3 * scale); using var marker = new Pen(Color.White, scale);
            e.Graphics.DrawEllipse(outline, x - 3 * scale, y - 3 * scale, 6 * scale, 6 * scale); e.Graphics.DrawEllipse(marker, x - 3 * scale, y - 3 * scale, 6 * scale, 6 * scale);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, r);
        }
        void Pick(Point p)
        {
            owner._s = Math.Clamp(p.X / (double)Math.Max(1, Width - 1), 0, 1);
            owner._v = 1 - Math.Clamp(p.Y / (double)Math.Max(1, Height - 1), 0, 1);
            owner.CommitHsv();
        }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button != MouseButtons.Left) return; Focus(); Capture = true; Pick(e.Location); }
        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if (Capture) Pick(e.Location); }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); Capture = false; }
        protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Up or Keys.Down || base.IsInputKey(keyData);
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            switch (e.KeyCode)
            {
                case Keys.Left: owner._s = Math.Max(0, owner._s - .01); break;
                case Keys.Right: owner._s = Math.Min(1, owner._s + .01); break;
                case Keys.Up: owner._v = Math.Min(1, owner._v + .01); break;
                case Keys.Down: owner._v = Math.Max(0, owner._v - .01); break;
                default: return;
            }
            owner.CommitHsv(); e.Handled = true;
        }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    }
}
