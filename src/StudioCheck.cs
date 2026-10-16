using System.Drawing.Drawing2D;

namespace AnkaStudio;

public sealed class StudioCheck : CheckBox
{
    public StudioCheck() { SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.Clear(BackColor == Color.Transparent ? Theme.Panel : BackColor); g.SmoothingMode = SmoothingMode.AntiAlias;
        var box = new Rectangle(1, Math.Max(1, (Height - 17) / 2), 17, 17);
        using var shape = Shapes.Round(box, 3); using var fill = new SolidBrush(Checked ? (Enabled ? Theme.Teal : Theme.Muted) : Theme.Elevated); using var border = new Pen(Focused ? Theme.Teal : Theme.Border);
        g.FillPath(fill, shape); g.DrawPath(border, shape);
        if (Checked) { using var tick = new Pen(Theme.Bg, 2) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round }; g.DrawLines(tick, [new Point(box.X + 4, box.Y + 8), new Point(box.X + 7, box.Y + 11), new Point(box.X + 13, box.Y + 5)]); }
        TextRenderer.DrawText(g, Text, Font, new Rectangle(27, 0, Math.Max(1, Width - 29), Height), Enabled ? ForeColor : Theme.Muted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(g, new Rectangle(24, 2, Math.Max(1, Width - 27), Math.Max(1, Height - 4)), ForeColor, Theme.Panel);
    }
}
