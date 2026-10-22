using System.Drawing.Drawing2D;
namespace AnkaStudio;

public sealed class StudioSlider : TrackBar
{
    bool hover;
    public StudioSlider()
    {
        SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
        AutoSize=false;Height=34;TickStyle=TickStyle.None;BackColor=Theme.Bg;
        ValueChanged+=(_,_)=>Invalidate();EnabledChanged+=(_,_)=>Invalidate();
    }
    protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnPaint(PaintEventArgs e)
    {
        var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
        var surface=BackColor;for(Control? p=Parent;surface.A!=255&&p!=null;p=p.Parent)surface=p.BackColor;
        g.Clear(surface.A==255?surface:Theme.Bg);
        bool vertical=Orientation==Orientation.Vertical;
        int length=vertical?Height:Width,across=vertical?Width:Height;if(length<28||across<14)return;
        float fraction=(Value-Minimum)/(float)Math.Max(1,Maximum-Minimum);
        int position=12+(int)((length-24)*(vertical?1-fraction:fraction));
        var track=vertical?new Rectangle(across/2-4,12,8,length-24):new Rectangle(12,across/2-4,length-24,8);
        using(var path=Shapes.Round(track,4)){using var recessed=new SolidBrush(Theme.Shell);g.FillPath(recessed,path);using var edge=new Pen(Color.FromArgb(90,Theme.Muted));g.DrawPath(edge,path);}
        var active=vertical?new Rectangle(track.X,position,8,Math.Max(1,length-12-position)):new Rectangle(12,track.Y,Math.Max(1,position-12),8);
        Color accent=Enabled?StudioGlass.Cyan:Theme.Muted;
        if(active.Width>0&&active.Height>0){using var fill=new LinearGradientBrush(active,StudioGlass.Indigo,accent,vertical?90:0);using var path=Shapes.Round(active,Math.Max(1,Math.Min(active.Width,active.Height)/2));g.FillPath(fill,path);}
        var thumb=vertical?new Rectangle(across/2-10,position-8,20,16):new Rectangle(position-8,across/2-10,16,20);
        for(int i=3;i>=1;i--){var glow=thumb;glow.Inflate(i,i);using var shape=Shapes.Round(glow,6);using var pen=new Pen(Color.FromArgb(hover?26:12,accent),2);g.DrawPath(pen,shape);}
        StudioGlass.Clip(g,thumb,accent,hover||Focused,false);
        using var glint=new Pen(Color.FromArgb(170,255,255,255));g.DrawLine(glint,thumb.Left+4,thumb.Top+3,thumb.Right-4,thumb.Top+3);
    }
}
