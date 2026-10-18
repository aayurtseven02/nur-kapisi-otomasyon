using System.Drawing.Drawing2D;

namespace AnkaStudio;

public sealed class StudioCheck : CheckBox
{
    public StudioCheck() { SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.Clear(BackColor == Color.Transparent ? Theme.Panel : BackColor); g.SmoothingMode = SmoothingMode.AntiAlias;
        var box = new Rectangle(1, Math.Max(1, (Height - 17) / 2), 17, 17);
        using var shape = Shapes.Round(box, 5); using var fill = new SolidBrush(Checked ? (Enabled ? Theme.Teal : Theme.Selection) : Theme.Elevated); using var border = new Pen(Checked ? Theme.Teal : Theme.Muted,1);
        g.FillPath(fill, shape); g.DrawPath(border, shape);
        if (Checked) { using var tick = new Pen(Enabled?Color.White:Theme.Text, 2) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round }; g.DrawLines(tick, [new Point(box.X + 4, box.Y + 8), new Point(box.X + 7, box.Y + 11), new Point(box.X + 13, box.Y + 5)]); }
        TextRenderer.DrawText(g, Text, Font, new Rectangle(27, 0, Math.Max(1, Width - 29), Height), Enabled ? ForeColor : Theme.Muted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
        if (Focused && ShowFocusCues&&Width>40&&Height>8){using var focus=Shapes.Round(new Rectangle(24,2,Width-27,Height-4),5);using var pen=new Pen(Theme.Border);g.DrawPath(pen,focus);}
    }
}
