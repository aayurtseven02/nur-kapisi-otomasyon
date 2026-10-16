namespace AnkaStudio;

public sealed class StudioNumber : NumericUpDown
{
    public StudioNumber() { BorderStyle=BorderStyle.FixedSingle; }
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        var arrows=Controls.Cast<Control>().FirstOrDefault(c=>c.GetType().Name.Contains("UpDownButtons"));
        if(arrows!=null) { arrows.BackColor=Theme.Elevated; arrows.Paint+=PaintArrows; }
    }
    void PaintArrows(object? sender,PaintEventArgs e)
    {
        if(sender is not Control c)return;
        e.Graphics.Clear(Theme.Elevated);
        using var pen=new Pen(Theme.Teal,1.5f);
        int x=c.Width/2, upper=c.Height/4, lower=3*c.Height/4;
        e.Graphics.DrawLines(pen,[new Point(x-3,upper+2),new Point(x,upper-1),new Point(x+3,upper+2)]);
        e.Graphics.DrawLines(pen,[new Point(x-3,lower-2),new Point(x,lower+1),new Point(x+3,lower-2)]);
    }
}
