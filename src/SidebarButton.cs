using System.Drawing.Drawing2D;
namespace AnkaStudio;

public sealed class SidebarButton : Button
{
    public bool Active;
    public string Glyph="";
    bool hover;
    public SidebarButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);Height=36;Font=Theme.Font(10);FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;Cursor=Cursors.Hand;BackColor=Color.Transparent;}
    protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor??Theme.Panel);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        if(hover||Active){using var shape=Shapes.Round(new Rectangle(1,1,Math.Max(1,Width-3),Math.Max(1,Height-3)),8);using var fill=new LinearGradientBrush(new Rectangle(1,1,Width-3,Height-3),Active?Color.FromArgb(247,248,250):Color.White,Active?Color.FromArgb(235,238,242):Color.FromArgb(244,246,248),90f);using var line=new Pen(Active?Color.FromArgb(179,188,200):Theme.Border);e.Graphics.FillPath(fill,shape);e.Graphics.DrawPath(line,shape);}
        using var icon=new Font("Segoe UI Symbol",11);TextRenderer.DrawText(e.Graphics,Glyph,icon,new Rectangle(9,0,23,Height),Theme.Text,TextFormatFlags.VerticalCenter|TextFormatFlags.HorizontalCenter);
        if(Width>70)TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(38,0,Math.Max(1,Width-44),Height),Theme.Text,TextFormatFlags.VerticalCenter|TextFormatFlags.Left|TextFormatFlags.EndEllipsis);
    }
    protected override void OnClick(EventArgs e){UiSound.Click();base.OnClick(e);}
}
