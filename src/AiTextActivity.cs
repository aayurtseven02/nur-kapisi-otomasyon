using System.Drawing.Drawing2D;
namespace AnkaStudio;

public sealed class AiTextActivity : Control
{
    readonly System.Windows.Forms.Timer timer=new(){Interval=80};
    internal int Frame{get;private set;}
    public bool Busy{get;private set;}
    public AiTextActivity(){DoubleBuffered=true;timer.Tick+=(_,_)=>{Frame=(Frame+1)%24;Invalidate();};VisibleChanged+=(_,_)=>timer.Enabled=Busy&&Visible;}
    public void UpdateActivity(string message,bool busy){Text=message;Busy=busy;timer.Enabled=busy&&Visible;AccessibleDescription=message;Invalidate();}
    protected override void OnPaint(PaintEventArgs e){
        base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
        if(Busy){using var track=new Pen(Color.FromArgb(218,222,228),2);using var pen=new Pen(Theme.Text,2){StartCap=LineCap.Round,EndCap=LineCap.Round};var circle=new Rectangle(5,(Height-18)/2,18,18);g.DrawEllipse(track,circle);g.DrawArc(pen,circle,Frame*15,110);}
        TextRenderer.DrawText(g,Text,Font,new Rectangle(Busy?34:5,0,Math.Max(0,Width-39),Height),Theme.Text,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
    }
    protected override void Dispose(bool disposing){if(disposing)timer.Dispose();base.Dispose(disposing);}
}
