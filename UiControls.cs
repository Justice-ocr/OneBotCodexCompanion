using System.Drawing.Drawing2D;

namespace OneBotCodexCompanion;

public sealed class RoundedPanel : Panel
{
    public int CornerRadius { get; set; } = 16;
    public Color BorderColor { get; set; } = Color.Transparent;

    public RoundedPanel()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = RoundedPath(bounds, CornerRadius);
        using var fill = new SolidBrush(BackColor);
        e.Graphics.FillPath(fill, path);
        if (BorderColor != Color.Transparent)
        {
            using var pen = new Pen(BorderColor);
            e.Graphics.DrawPath(pen, path);
        }
        base.OnPaint(e);
    }

    private static GraphicsPath RoundedPath(Rectangle bounds, int radius)
    {
        var diameter = Math.Min(Math.Min(bounds.Width, bounds.Height), radius * 2);
        var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

public sealed class RoundedButton : Button
{
    public int CornerRadius { get; set; } = 12;
    public Color FillColor { get; set; }
    public Color HoverColor { get; set; }
    public Color TextColor { get; set; }
    private bool _hovered;

    public RoundedButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        DoubleBuffered = true;
        ResizeRedraw = true;
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hovered = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = RoundedPath(bounds, CornerRadius);
        using var fill = new SolidBrush(_hovered && HoverColor != Color.Empty ? HoverColor : FillColor);
        e.Graphics.FillPath(fill, path);
        TextRenderer.DrawText(e.Graphics, Text, Font, bounds, TextColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.LeftAndRightPadding);
    }

    private static GraphicsPath RoundedPath(Rectangle bounds, int radius)
    {
        var diameter = Math.Min(Math.Min(bounds.Width, bounds.Height), radius * 2);
        var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
