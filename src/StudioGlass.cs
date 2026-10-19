using System.Drawing.Drawing2D;
namespace AnkaStudio;

// Palette and translucent surfaces taken from the supplied StreamForge design.
// Paint over the existing grid/background so content behind a clip remains visible.
internal static class StudioGlass
{
    public static readonly Color Indigo=Color.FromArgb(99,102,241), Cyan=Color.FromArgb(34,211,238), Emerald=Color.FromArgb(52,211,153), Amber=Color.FromArgb(251,191,36), Purple=Color.FromArgb(192,132,252), Rose=Color.FromArgb(251,113,133);
    public static Color Blend(Color a,Color b,double amount)=>Color.FromArgb((int)(a.R+(b.R-a.R)*amount),(int)(a.G+(b.G-a.G)*amount),(int)(a.B+(b.B-a.B)*amount));
    public static void Surface(Graphics g,Rectangle rect,Color surface,Color accent,int radius=10,bool active=false)
    {
        if(rect.Width<4||rect.Height<4)return;
        using var path=Shapes.Round(rect,Math.Min(radius,Math.Min(rect.Width,rect.Height)/2));
        using(var fill=new LinearGradientBrush(rect,Blend(surface,accent,active?.24:.10),Blend(surface,Theme.Shell,.30),90f))g.FillPath(fill,path);
        var state=g.Save();g.SetClip(path,System.Drawing.Drawing2D.CombineMode.Intersect);
        var reflection=new Rectangle(rect.Left,rect.Top,rect.Width,Math.Max(2,rect.Height/2));
        using(var gloss=new LinearGradientBrush(reflection,Color.FromArgb(active?47:26,255,255,255),Color.Transparent,90f))g.FillRectangle(gloss,reflection);
        g.Restore(state);
        using(var edge=new Pen(Color.FromArgb(active?210:92,accent),active?1.5f:1f))g.DrawPath(edge,path);
        if(rect.Width>24){using var light=new Pen(Color.FromArgb(active?155:74,255,255,255));g.DrawLine(light,rect.Left+10,rect.Top+1,rect.Right-10,rect.Top+1);}
    }
    public static void Clip(Graphics g,Rectangle rect,Color accent,bool selected=false,bool handles=true)
    {
        if(rect.Width<3||rect.Height<3)return;
        using var path=Shapes.Round(rect,Math.Min(8,Math.Min(rect.Width,rect.Height)/2));
        var shadow=rect;shadow.Offset(0,2);using(var shadowPath=Shapes.Round(shadow,8))using(var brush=new SolidBrush(Color.FromArgb(45,0,0,0)))g.FillPath(brush,shadowPath);
        using(var brush=new SolidBrush(Color.FromArgb(selected?92:55,accent)))g.FillPath(brush,path);
        using(var gloss=new LinearGradientBrush(rect,Color.FromArgb(52,255,255,255),Color.FromArgb(10,0,0,0),90f))g.FillPath(gloss,path);
        using(var edge=new Pen(Color.FromArgb(selected?245:160,accent),selected?1.8f:1f))g.DrawPath(edge,path);
        using(var light=new Pen(Color.FromArgb(selected?95:45,255,255,255)))g.DrawLine(light,rect.Left+8,rect.Top+1,rect.Right-8,rect.Top+1);
        using(var handle=new Pen(Color.FromArgb(180,255,255,255),2){StartCap=LineCap.Round,EndCap=LineCap.Round}){
            int y=rect.Top+rect.Height/2;if(handles&&rect.Width>24){g.DrawLine(handle,rect.Left+5,y-7,rect.Left+5,y+7);g.DrawLine(handle,rect.Right-5,y-7,rect.Right-5,y+7);}
        }
    }
}
