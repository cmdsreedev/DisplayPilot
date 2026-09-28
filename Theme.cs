using System.Drawing.Drawing2D;

namespace DisplayPilot;

internal static class Theme
{
    public static readonly Color Canvas = Color.FromArgb(244, 246, 249);
    public static readonly Color Ink = Color.FromArgb(25, 39, 57);
    public static readonly Color Muted = Color.FromArgb(104, 117, 134);
    public static readonly Color Border = Color.FromArgb(225, 231, 237);
    public static readonly Color Accent = Color.FromArgb(12, 124, 107);
    public static readonly Color Tint = Color.FromArgb(229, 245, 239);
    public static readonly Color Sidebar = Color.FromArgb(20, 31, 47);
    public static readonly Color Danger = Color.FromArgb(166, 53, 53);
    public static Font Font(float size = 14, bool bold = false) => new("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
    public static GraphicsPath Rounded(RectangleF r, float radius)
    {
        var path = new GraphicsPath();
        float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
    public static Label Text(string text, float size = 14, bool bold = false, Color? color = null) => new()
    {
        Text = text,
        Font = Font(size, bold),
        ForeColor = color ?? Ink,
        AutoSize = false,
        Dock = DockStyle.Top,
        Height = (int)(size * 1.7),
        BackColor = Color.Transparent,
        AutoEllipsis = true,
        TextAlign = ContentAlignment.MiddleLeft
    };
    public static TextBox Input(string value = "") => new()
    {
        Text = value,
        Font = Font(),
        ForeColor = Ink,
        BackColor = Color.White,
        BorderStyle = BorderStyle.None,
        Dock = DockStyle.Fill
    };
    public static ComboBox Select() => new ModernCombo();
}

internal sealed class Surface : Panel
{
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Color Fill { get; set; } = Color.White;
    public Surface()
    {
        DoubleBuffered = true; BackColor = Theme.Canvas;
        Padding = new Padding(22); Margin = new Padding(0, 0, 0, 16);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Theme.Rounded(new RectangleF(.5f, .5f, Width - 1, Height - 1), 14);
        using var fill = new SolidBrush(Fill); using var border = new Pen(Theme.Border);
        e.Graphics.FillPath(fill, path); e.Graphics.DrawPath(border, path);
        base.OnPaint(e);
    }
}

internal sealed class ModernButton : Button
{
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool Primary { get; set; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool Selected { get; set; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool Navigation { get; set; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string Glyph { get; set; } = "";
    bool hover;
    public ModernButton()
    {
        FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0;
        Font = Theme.Font(14, true); Height = 40; Width = 150; Cursor = Cursors.Hand;
        Margin = new Padding(0, 0, 10, 0);
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
    }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.Clear(Parent?.BackColor ?? Theme.Canvas); g.SmoothingMode = SmoothingMode.AntiAlias;
        Color fill = Navigation ? (Selected ? Color.FromArgb(39, 59, 76) : hover ? Color.FromArgb(30, 45, 64) : Theme.Sidebar)
            : Primary ? (hover ? Color.FromArgb(9, 103, 90) : Theme.Accent) : (hover ? Theme.Canvas : Color.White);
        Color ink = Navigation ? (Selected ? Color.FromArgb(114, 232, 201) : Color.FromArgb(195, 205, 216)) : Primary ? Color.White : Theme.Ink;
        if (!Enabled) { fill = Theme.Border; ink = Theme.Muted; }
        using var shape = Theme.Rounded(new RectangleF(.5f, .5f, Width - 1, Height - 1), 8);
        using var brush = new SolidBrush(fill); g.FillPath(brush, shape);
        if (!Navigation && !Primary) { using var border = new Pen(Theme.Border); g.DrawPath(border, shape); }
        var textBounds = new Rectangle(12, 0, Width - 24, Height);
        if (Glyph.Length > 0)
        {
            using var font = new Font("Segoe MDL2 Assets", 18, FontStyle.Regular, GraphicsUnit.Pixel);
            TextRenderer.DrawText(g, Glyph, font, new Rectangle(16, 0, 24, Height), ink, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
            textBounds = new Rectangle(52, 0, Width - 60, Height);
        }
        TextRenderer.DrawText(g, Text, Font, textBounds, ink, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | (Navigation ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter));
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(g, new Rectangle(5, 5, Width - 10, Height - 10), ink, fill);
    }
}

internal sealed class Toggle : CheckBox
{
    public Toggle()
    {
        AutoSize = false; Width = 56; Height = 32; Text = ""; Cursor = Cursors.Hand;
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? Color.White); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new RectangleF(2, 4, Width - 4, Height - 8);
        using var p = Theme.Rounded(r, r.Height / 2);
        using var track = new SolidBrush(Checked ? Theme.Accent : Color.FromArgb(173, 185, 199));
        e.Graphics.FillPath(track, p); float diameter = r.Height - 6;
        e.Graphics.FillEllipse(Brushes.White, Checked ? r.Right - diameter - 3 : r.X + 3, r.Y + 3, diameter, diameter);
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, ClientRectangle);
    }
    protected override void OnCheckedChanged(EventArgs e) { Invalidate(); base.OnCheckedChanged(e); }
}

internal sealed class ModernCombo : ComboBox
{
    public ModernCombo()
    {
        DropDownStyle = ComboBoxStyle.DropDownList; FlatStyle = FlatStyle.Flat;
        Font = Theme.Font(14); BackColor = Color.White; ForeColor = Theme.Ink;
        DrawMode = DrawMode.OwnerDrawFixed; ItemHeight = 28;
        IntegralHeight = false; DropDownHeight = 230; Dock = DockStyle.Fill;
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? Color.White);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var shape = Theme.Rounded(new RectangleF(.5f, .5f, Width - 1, Height - 1), 6);
        e.Graphics.FillPath(Brushes.White, shape);
        using var border = new Pen(Focused ? Theme.Accent : Theme.Border);
        e.Graphics.DrawPath(border, shape);
        TextRenderer.DrawText(e.Graphics, SelectedItem as string ?? "Choose a profile", Font,
            new Rectangle(10, 0, Width - 36, Height), Theme.Ink, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        using var arrow = new Pen(Theme.Muted, 1.5f);
        e.Graphics.DrawLines(arrow, [new Point(Width - 22, Height / 2 - 2), new Point(Width - 17, Height / 2 + 3), new Point(Width - 12, Height / 2 - 2)]);
    }
    protected override void OnSelectedIndexChanged(EventArgs e) { base.OnSelectedIndexChanged(e); Invalidate(); }
    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        bool selected = (e.State & DrawItemState.Selected) != 0 && (e.State & DrawItemState.ComboBoxEdit) == 0;
        using var brush = new SolidBrush(selected ? Theme.Tint : Color.White); e.Graphics.FillRectangle(brush, e.Bounds);
        string text = e.Index >= 0 ? GetItemText(Items[e.Index]) ?? "" : Text;
        TextRenderer.DrawText(e.Graphics, text, Font, new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 16, e.Bounds.Height), Theme.Ink, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        e.DrawFocusRectangle();
    }
}

internal sealed class InputFrame : Panel
{
    readonly TextBox input;
    public InputFrame(TextBox textBox)
    {
        input = textBox; BackColor = Color.White; DoubleBuffered = true;
        Width = input.Width; Height = 36; Margin = new Padding(0, 0, 10, 0);
        input.Dock = DockStyle.None; Controls.Add(input);
        input.GotFocus += (_, _) => Invalidate(); input.LostFocus += (_, _) => Invalidate();
        Click += (_, _) => input.Focus();
    }
    protected override void OnLayout(LayoutEventArgs e)
    {
        input.SetBounds(10, Math.Max(4, (Height - input.PreferredHeight) / 2), Math.Max(30, Width - 20), input.PreferredHeight);
        base.OnLayout(e);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var shape = Theme.Rounded(new RectangleF(.5f, .5f, Width - 1, Height - 1), 6);
        using var pen = new Pen(input.Focused ? Theme.Accent : Theme.Border);
        e.Graphics.DrawPath(pen, shape); base.OnPaint(e);
    }
}

internal sealed class CardStack : FlowLayoutPanel
{
    public CardStack()
    {
        Dock = DockStyle.Fill; AutoScroll = true; WrapContents = false;
        FlowDirection = FlowDirection.TopDown; BackColor = Theme.Canvas; Padding = new Padding(0, 0, 4, 0);
    }
    protected override void OnLayout(LayoutEventArgs e)
    {
        int width = Math.Max(100, ClientSize.Width - Padding.Horizontal - SystemInformation.VerticalScrollBarWidth);
        foreach (Control child in Controls) if (child.Width != width) child.Width = width;
        base.OnLayout(e);
    }
}

internal sealed class DisplayArtwork : Control
{
    public DisplayArtwork() { DoubleBuffered = true; BackColor = Theme.Tint; }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        float s = Math.Min(Width / 210f, Height / 130f);
        g.TranslateTransform((Width - 210 * s) / 2, (Height - 130 * s) / 2); g.ScaleTransform(s, s);
        using var pen = new Pen(Theme.Accent, 3);
        using var monitor = Theme.Rounded(new RectangleF(8, 12, 132, 82), 7);
        g.FillPath(Brushes.White, monitor); g.DrawPath(pen, monitor);
        g.DrawLine(pen, 74, 94, 74, 112); g.DrawLine(pen, 48, 113, 100, 113);
        using var screen = Theme.Rounded(new RectangleF(110, 59, 90, 58), 6);
        using var fill = new SolidBrush(Theme.Sidebar); g.FillPath(fill, screen); g.DrawPath(pen, screen);
        using var arrow = new Pen(Color.FromArgb(109, 223, 190), 3);
        g.DrawLines(arrow, [new PointF(134, 85), new PointF(176, 85), new PointF(167, 76)]);
        g.DrawLine(arrow, 176, 85, 167, 94);
    }
}
