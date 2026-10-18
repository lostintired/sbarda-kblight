using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace KbLight;

static class LightingTheme
{
    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int size);

    public static void DarkTitleBar(nint window)
    {
        int enabled = 1;
        _ = DwmSetWindowAttribute(window, 20, ref enabled, sizeof(int));
    }
    public static readonly Color Background = Color.FromArgb(0x14, 0x13, 0x16);
    public static readonly Color Panel = Color.FromArgb(0x20, 0x1d, 0x24);
    public static readonly Color Field = Color.FromArgb(0x17, 0x15, 0x1b);
    public static readonly Color Border = Color.FromArgb(0x3f, 0x38, 0x47);
    public static readonly Color Foreground = Color.FromArgb(0xf3, 0xf1, 0xf5);
    public static readonly Color Muted = Color.FromArgb(0xb5, 0xad, 0xbc);
    public static readonly Color Accent = Color.FromArgb(0xfa, 0x2d, 0x4c);
    public static readonly Color SecondaryAccent = Color.FromArgb(0xbf, 0x2f, 0xdf);
    public static readonly Color Link = Color.FromArgb(0xd8, 0xa3, 0xf0);
    public static readonly Color SecondaryPanel = Color.FromArgb(0x1a, 0x18, 0x1e);
    public static readonly Color ButtonSurface = Color.FromArgb(0x31, 0x2c, 0x36);
    public static readonly Color Error = Color.FromArgb(0xf4, 0xa6, 0xa6);

    public static void Apply(Control parent)
    {
        foreach (Control c in parent.Controls)
        {
            c.ForeColor = c.ForeColor == Error ? Error : c is LinkLabel ? Link : c is Label && !c.Font.Bold ? Muted : Foreground;
            if (c is TextBox or ComboBox or DateTimePicker) c.BackColor = Field;
            else if (c is not Button && c is not System.Windows.Forms.Panel && c is not UserControl) c.BackColor = parent.BackColor;
            if (c is CheckBox cb) cb.UseVisualStyleBackColor = false;
            if (c is LinkLabel link) link.LinkColor = link.ActiveLinkColor = link.VisitedLinkColor = Link;
            Apply(c);
        }
    }
}

sealed class LightingSwitch : CheckBox
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShowStateCaption { get; init; } = true;
    public LightingSwitch()
    {
        FlatStyle = FlatStyle.Flat;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        AutoSize = false;
        Size = new Size(80, 28);
        Cursor = Cursors.Hand;
        AccessibleRole = AccessibleRole.CheckButton;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        float s = DeviceDpi / 96f;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var track = new RectangleF(Width - 34 * s, (Height - 18 * s) / 2, 32 * s, 18 * s);
        using var path = new GraphicsPath();
        path.AddRoundedRectangle(track, new SizeF(9 * s, 9 * s));
        using Brush brush = Enabled && Checked ? new LinearGradientBrush(track, LightingTheme.Accent, LightingTheme.SecondaryAccent, 0f) : new SolidBrush(LightingTheme.Border);
        e.Graphics.FillPath(brush, path);
        using var knob = new SolidBrush(Checked ? LightingTheme.Field : LightingTheme.Muted);
        e.Graphics.FillEllipse(knob, track.X + (Checked ? 17 : 3) * s, track.Y + 3 * s, 12 * s, 12 * s);
        if (ShowStateCaption) TextRenderer.DrawText(e.Graphics, Checked ? KbLight.Text.SwitchOn : KbLight.Text.SwitchOff, Font,
            new Rectangle(0, 0, Math.Max(0, Width - (int)(40 * s)), Height), LightingTheme.Muted,
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, ClientRectangle, LightingTheme.Accent, BackColor);
    }

    protected override void OnCheckedChanged(EventArgs e) { base.OnCheckedChanged(e); Invalidate(); }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
}

sealed class LightingSlider : Control
{
    int _value;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Minimum { get; init; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Maximum { get; init; } = 100;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Value
    {
        get => _value;
        set { int next = Math.Clamp(value, Minimum, Maximum); if (_value == next) return; _value = next; Invalidate(); ValueChanged?.Invoke(this, EventArgs.Empty); }
    }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Hue { get; init; }
    public event EventHandler? ValueChanged;
    public LightingSlider()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
        Size = new Size(260, 26);
        TabStop = true;
        Cursor = Cursors.Hand;
        AccessibleRole = AccessibleRole.Slider;
    }
    protected override AccessibleObject CreateAccessibilityInstance() => new SliderAccessibility(this);
    sealed class SliderAccessibility(LightingSlider owner) : ControlAccessibleObject(owner)
    {
        public override string? Value { get => owner.Value.ToString(); set { if (int.TryParse(value, out int n)) owner.Value = n; } }
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        float s = DeviceDpi / 96f;
        float left = 7 * s, right = Math.Max(left + 1, Width - 7 * s), y = Height / 2f;
        float x = left + (right - left) * (Value - Minimum) / Math.Max(1, Maximum - Minimum);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        if (Hue)
        {
            for (int n = 0; n < 6; n++)
            {
                var r = new RectangleF(left + (right - left) * n / 6, y - 3 * s, (right - left) / 6 + 1, 6 * s);
                using var brush = new LinearGradientBrush(r, LightingColorPicker.FromHsv(n * 60, 1, 1), LightingColorPicker.FromHsv((n + 1) * 60, 1, 1), 0f);
                e.Graphics.FillRectangle(brush, r);
            }
        }
        else
        {
            using var track = new Pen(LightingTheme.Border, 4 * s);
            e.Graphics.DrawLine(track, left, y, right, y);
            using Brush ink = Enabled ? new LinearGradientBrush(new PointF(left, y), new PointF(right, y), LightingTheme.Accent, LightingTheme.SecondaryAccent) : new SolidBrush(LightingTheme.Muted);
            using var fill = new Pen(ink, 4 * s);
            e.Graphics.DrawLine(fill, left, y, x, y);
        }
        using var knob = new SolidBrush(Enabled ? LightingTheme.Foreground : LightingTheme.Muted);
        e.Graphics.FillEllipse(knob, x - 5 * s, y - 5 * s, 10 * s, 10 * s);
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, ClientRectangle, LightingTheme.Accent, BackColor);
    }
    void Pick(int x) => Value = Minimum + (int)Math.Round(Math.Clamp((x - 7 * DeviceDpi / 96f) / Math.Max(1, Width - 14 * DeviceDpi / 96f), 0, 1) * (Maximum - Minimum));
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button != MouseButtons.Left) return; Focus(); Capture = true; Pick(e.X); }
    protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if (Capture) Pick(e.X); }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); Capture = false; }
    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Home or Keys.End || base.IsInputKey(keyData);
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.KeyCode)
        {
            case Keys.Left: case Keys.Down: Value--; break;
            case Keys.Right: case Keys.Up: Value++; break;
            case Keys.Home: Value = Minimum; break;
            case Keys.End: Value = Maximum; break;
            default: return;
        }
        e.Handled = true;
    }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
}

enum LightingGlyph { Brand, Keys, Box }

sealed class LightingSwatch : Button
{
    public LightingSwatch() => SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? LightingTheme.Panel);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        float scale = DeviceDpi / 96f;
        var r = new RectangleF(2 * scale, 2 * scale, Width - 5 * scale, Height - 5 * scale);
        using var fill = new SolidBrush(Enabled ? BackColor : LightingTheme.Border);
        e.Graphics.FillEllipse(fill, r);
        using var edge = new Pen(Focused ? LightingTheme.Accent : LightingTheme.Muted, (Focused ? 2 : 1) * scale);
        e.Graphics.DrawEllipse(edge, r);
    }
}

sealed class LightingCheckBox : CheckBox
{
    public LightingCheckBox()
    {
        FlatStyle = FlatStyle.Flat;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        UseVisualStyleBackColor = false;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        int box = (int)(13 * DeviceDpi / 96f), y = (Height - box) / 2;
        using var pen = new Pen(Enabled ? LightingTheme.Muted : LightingTheme.Border);
        using var fill = new SolidBrush(Checked && Enabled ? LightingTheme.Accent : LightingTheme.Field);
        var bounds = new Rectangle(0, y, box, box);
        e.Graphics.FillRectangle(fill, bounds); e.Graphics.DrawRectangle(pen, bounds);
        if (Checked)
        {
            using var tick = new Pen(Enabled ? LightingTheme.Field : LightingTheme.Muted, 1.7f * DeviceDpi / 96f);
            e.Graphics.DrawLines(tick, [new PointF(box * .2f, y + box * .5f), new PointF(box * .43f, y + box * .73f), new PointF(box * .83f, y + box * .25f)]);
        }
        int gap = (int)(7 * DeviceDpi / 96f);
        TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(box + gap, 0, Math.Max(1, Width - box - gap), Height), Enabled ? LightingTheme.Foreground : LightingTheme.Muted,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, ClientRectangle, LightingTheme.Accent, BackColor);
    }
    public override Size GetPreferredSize(Size proposedSize)
    {
        int padding = (int)(24 * DeviceDpi / 96f);
        var text = TextRenderer.MeasureText(Text, Font, new Size(Math.Max(30, MaximumSize.Width - padding), 0), TextFormatFlags.WordBreak);
        return new Size(text.Width + padding, Math.Max(text.Height, (int)(17 * DeviceDpi / 96f)));
    }
    protected override void OnCheckedChanged(EventArgs e) { base.OnCheckedChanged(e); Invalidate(); }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
}

sealed class LightingTimePicker : Control
{
    DateTime _value = DateTime.Today;
    bool _minute;
    string _digits = "";
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public DateTime Value
    {
        get => _value;
        set { if (_value == value) return; _value = value; Invalidate(); ValueChanged?.Invoke(this, EventArgs.Empty); }
    }
    public event EventHandler? ValueChanged;
    public LightingTimePicker()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
        TabStop = true; Size = new Size(112, 30); AccessibleRole = AccessibleRole.SpinButton;
        BackColor = LightingTheme.Field;
    }
    protected override AccessibleObject CreateAccessibilityInstance() => new TimeAccessibility(this);
    sealed class TimeAccessibility(LightingTimePicker owner) : ControlAccessibleObject(owner)
    {
        public override string? Value
        {
            get => owner.Value.ToString("HH:mm");
            set { if (QuietHoursState.TryTime(value, out var t)) owner.Value = DateTime.Today.Add(t.ToTimeSpan()); }
        }
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        using var bg = new SolidBrush(LightingTheme.Field); e.Graphics.FillRectangle(bg, ClientRectangle);
        using var border = new Pen(Focused ? LightingTheme.Accent : LightingTheme.Border);
        e.Graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
        int unit = (int)(DeviceDpi / 96f * 8);
        var textBounds = new Rectangle(unit, 0, Width - 3 * unit, Height);
        TextRenderer.DrawText(e.Graphics, Value.ToString("HH:mm"), Font, textBounds, Enabled ? LightingTheme.Foreground : LightingTheme.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        if (Focused)
        {
            int w = TextRenderer.MeasureText("00", Font, Size.Empty, TextFormatFlags.NoPadding).Width;
            using var mark = new Pen(LightingTheme.Accent, 2);
            int x = unit + (_minute ? TextRenderer.MeasureText("00:", Font, Size.Empty, TextFormatFlags.NoPadding).Width : 0) + 2;
            e.Graphics.DrawLine(mark, x, Height - 6, x + w, Height - 6);
        }
        int cx = Width - unit;
        using var arrow = new SolidBrush(Enabled ? LightingTheme.Muted : LightingTheme.Border);
        e.Graphics.FillPolygon(arrow, [new Point(cx - 3, Height / 2 - 3), new Point(cx + 3, Height / 2 - 3), new Point(cx, Height / 2 - 6)]);
        e.Graphics.FillPolygon(arrow, [new Point(cx - 3, Height / 2 + 3), new Point(cx + 3, Height / 2 + 3), new Point(cx, Height / 2 + 6)]);
    }
    void Step(int delta) => Value = _minute ? Value.AddMinutes(delta) : Value.AddHours(delta);
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e); if (e.Button != MouseButtons.Left) return; Focus(); _digits = "";
        if (e.X > Width - 20 * DeviceDpi / 96f) Step(e.Y < Height / 2 ? 1 : -1);
        else _minute = e.X > 33 * DeviceDpi / 96f;
        Invalidate();
    }
    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Up or Keys.Down || base.IsInputKey(keyData);
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.KeyCode)
        {
            case Keys.Left: _minute = false; _digits = ""; break;
            case Keys.Right: _minute = true; _digits = ""; break;
            case Keys.Up: Step(1); _digits = ""; break;
            case Keys.Down: Step(-1); _digits = ""; break;
            default: return;
        }
        Invalidate(); e.Handled = true;
    }
    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        base.OnKeyPress(e);
        if (!char.IsAsciiDigit(e.KeyChar)) return;
        if (_digits.Length == 2) _digits = "";
        _digits += e.KeyChar;
        int n = int.Parse(_digits);
        if (n < (_minute ? 60 : 24)) Value = Value.Date.AddHours(_minute ? Value.Hour : n).AddMinutes(_minute ? n : Value.Minute);
        if (_digits.Length == 2 && !_minute) { _minute = true; _digits = ""; }
        Invalidate(); e.Handled = true;
    }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); _digits = ""; Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
}

sealed class LightingIcon : Control
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public LightingGlyph Glyph { get; init; }
    public LightingIcon() { Size = new Size(30, 28); SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true); TabStop = false; }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        float scale = Math.Min(Width / 32f, Height / 32f);
        var state = e.Graphics.Save();
        e.Graphics.TranslateTransform((Width - 32 * scale) / 2, (Height - 32 * scale) / 2);
        e.Graphics.ScaleTransform(scale, scale);
        using Brush ink = Glyph == LightingGlyph.Brand ? new LinearGradientBrush(new PointF(3, 0), new PointF(29, 0), LightingTheme.Accent, LightingTheme.SecondaryAccent) : new SolidBrush(LightingTheme.Muted);
        using var pen = new Pen(ink, 1.6f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
        void Rect(float x, float y, float w, float h) => e.Graphics.DrawRoundedRectangle(pen, new RectangleF(x, y, w, h), new SizeF(2, 2));
        if (Glyph == LightingGlyph.Keys)
        {
            Rect(11.5f, 4, 9, 9); Rect(1.5f, 16, 8, 9); Rect(12, 16, 8, 9); Rect(22.5f, 16, 8, 9);
        }
        else if (Glyph == LightingGlyph.Box)
        {
            Rect(3, 12, 26, 7);
            e.Graphics.DrawLine(pen, 7, 15.5f, 25, 15.5f);
            e.Graphics.DrawLine(pen, 16, 3, 16, 7); e.Graphics.DrawLine(pen, 16, 24, 16, 28);
            e.Graphics.DrawLine(pen, 5, 6, 8, 9); e.Graphics.DrawLine(pen, 24, 9, 27, 6);
            e.Graphics.DrawLine(pen, 5, 26, 8, 23); e.Graphics.DrawLine(pen, 24, 23, 27, 26);
        }
        else
        {
            e.Graphics.DrawRoundedRectangle(pen, new RectangleF(3, 2, 26, 28), new SizeF(5, 5));
            pen.Width = 2.5f;
            e.Graphics.DrawLine(pen, 11, 9, 11, 23); e.Graphics.DrawLine(pen, 11, 16, 15, 16);
            e.Graphics.DrawLines(pen, [new PointF(22, 9), new PointF(15, 16), new PointF(22, 23)]);
        }
        e.Graphics.Restore(state);
    }
}
