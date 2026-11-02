using System.Drawing.Drawing2D;

namespace DriveSyncPortable;

internal static class Theme
{
    public static readonly Color Page = Color.FromArgb(245, 247, 251);
    public static readonly Color White = Color.White;
    public static readonly Color Ink = Color.FromArgb(27, 42, 62);
    public static readonly Color Muted = Color.FromArgb(111, 126, 147);
    public static readonly Color Line = Color.FromArgb(225, 233, 242);
    public static readonly Color Blue = Color.FromArgb(48, 114, 217);
    public static readonly Color PaleBlue = Color.FromArgb(236, 244, 255);
    public static readonly Color Green = Color.FromArgb(31, 148, 102);
    public static readonly Color PaleGreen = Color.FromArgb(235, 248, 243);

    public static GraphicsPath Rounded(Rectangle r, int radius)
    {
        var p = new GraphicsPath();
        int d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        if (d <= 0) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static Label Text(string text, int x, int y, int w, int h, float size = 10,
                             bool bold = false, Color? color = null)
    {
        return new Label
        {
            AutoSize = false,
            Text = text,
            Location = new Point(x, y),
            Size = new Size(w, h),
            BackColor = Color.Transparent,
            ForeColor = color ?? Ink,
            Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };
    }
}

internal sealed class RoundedPanel : Panel
{
    public Color FillColor { get; set; } = Theme.White;
    public Color StrokeColor { get; set; } = Theme.Line;
    public int CornerRadius { get; set; } = 13;

    public RoundedPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer, true);
        Padding = new Padding(0);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(Parent?.BackColor ?? Theme.Page);
        using var p = Theme.Rounded(new Rectangle(1, 1, Math.Max(1, Width - 3), Math.Max(1, Height - 3)), CornerRadius);
        using var fill = new SolidBrush(FillColor);
        using var border = new Pen(StrokeColor, 1);
        e.Graphics.FillPath(fill, p);
        e.Graphics.DrawPath(border, p);
    }
}

internal sealed class RoundedButton : Button
{
    public Color FillColor { get; set; } = Theme.White;
    public Color TextColor { get; set; } = Theme.Ink;
    public Color StrokeColor { get; set; } = Theme.Line;
    public bool Outlined { get; set; } = true;
    public int CornerRadius { get; set; } = 9;

    public RoundedButton(string label)
    {
        Text = label;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Font = new Font("Segoe UI", 9, FontStyle.Bold);
        TabStop = true;
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(Parent?.BackColor ?? Theme.White);
        var rect = new Rectangle(1, 1, Math.Max(1, Width - 3), Math.Max(1, Height - 3));
        using var path = Theme.Rounded(rect, CornerRadius);
        using var fill = new SolidBrush(FillColor);
        e.Graphics.FillPath(fill, path);
        if (Outlined)
        {
            using var pen = new Pen(StrokeColor);
            e.Graphics.DrawPath(pen, path);
        }
        var textRect = new Rectangle(6, 1, Math.Max(1, Width - 12), Math.Max(1, Height - 2));
        TextRenderer.DrawText(e.Graphics, Text, Font, textRect, Enabled ? TextColor : Theme.Muted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        if (Focused && ShowFocusCues)
        {
            using var pen = new Pen(Theme.Blue) { DashStyle = DashStyle.Dot };
            e.Graphics.DrawRectangle(pen, 4, 4, Math.Max(1, Width - 9), Math.Max(1, Height - 9));
        }
    }

    protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
}

internal sealed class ToggleSwitch : CheckBox
{
    public ToggleSwitch()
    {
        AutoSize = false;
        Size = new Size(47, 27);
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(Parent?.BackColor ?? Theme.White);
        using (var path = Theme.Rounded(new Rectangle(0, 0, 46, 26), 13))
        using (var background = new SolidBrush(Checked ? Theme.Blue : Color.FromArgb(198, 208, 222)))
            e.Graphics.FillPath(background, path);
        using (var brush = new SolidBrush(Color.White))
            e.Graphics.FillEllipse(brush, Checked ? 23 : 3, 3, 20, 20);
    }

    protected override void OnCheckedChanged(EventArgs e)
    {
        base.OnCheckedChanged(e);
        Invalidate();
    }
}
