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
        if(hover||Active){using var shape=Shapes.Round(new Rectangle(1,1,Math.Max(1,Width-3),Math.Max(1,Height-3)),8);using var fill=new SolidBrush(Active?Theme.Selection:Theme.Hover);using var line=new Pen(Active?Theme.Selection:Theme.Hover);e.Graphics.FillPath(fill,shape);e.Graphics.DrawPath(line,shape);}
        if(Glyph=="warning"){
            float top=(Height-23)/2f;PointF[] triangle=[new(20.5f,top),new(9,top+23),new(32,top+23)];
            using var yellow=new SolidBrush(Color.FromArgb(255,211,31));using var edge=new Pen(Color.Black,1.7f){LineJoin=LineJoin.Round};e.Graphics.FillPolygon(yellow,triangle);e.Graphics.DrawPolygon(edge,triangle);
            using var mark=new Pen(Color.Black,3){StartCap=LineCap.Round,EndCap=LineCap.Round};e.Graphics.DrawLine(mark,20.5f,top+8,20.5f,top+14.5f);e.Graphics.FillEllipse(Brushes.Black,19,top+18,3,3);
        }else{using var icon=new Font("Segoe UI Symbol",11);TextRenderer.DrawText(e.Graphics,Glyph,icon,new Rectangle(9,0,23,Height),Theme.Text,TextFormatFlags.VerticalCenter|TextFormatFlags.HorizontalCenter);}
        if(Width>70)TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(38,0,Math.Max(1,Width-44),Height),Theme.Text,TextFormatFlags.VerticalCenter|TextFormatFlags.Left|TextFormatFlags.EndEllipsis);
    }
    protected override void OnClick(EventArgs e){UiSound.Click();base.OnClick(e);}
}
