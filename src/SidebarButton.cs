using System.Drawing.Drawing2D;
namespace AnkaStudio;

public sealed class SidebarButton : Button
{
    public bool Active;
    public string Glyph="";
    readonly HoverCaption caption;
    bool hover;
    public SidebarButton(){caption=new HoverCaption(this);SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);Height=36;Font=Theme.Font(10);FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;Cursor=Cursors.Hand;BackColor=Color.Transparent;}
    protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor??Theme.Panel);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        if(hover||Active){StudioGlass.Clip(e.Graphics,new Rectangle(1,1,Math.Max(1,Width-3),Math.Max(1,Height-3)),Active?StudioGlass.Indigo:Theme.Muted,Active,false);}
        if(Glyph=="warning"){
            float top=(Height-23)/2f;float center=Width>70?20.5f:Width/2f;PointF[] triangle=[new(center,top),new(center-11.5f,top+23),new(center+11.5f,top+23)];
            using var yellow=new SolidBrush(Color.FromArgb(255,211,31));using var edge=new Pen(Color.Black,1.7f){LineJoin=LineJoin.Round};e.Graphics.FillPolygon(yellow,triangle);e.Graphics.DrawPolygon(edge,triangle);
            using var mark=new Pen(Color.Black,3){StartCap=LineCap.Round,EndCap=LineCap.Round};e.Graphics.DrawLine(mark,center,top+8,center,top+14.5f);e.Graphics.FillEllipse(Brushes.Black,center-1.5f,top+18,3,3);
        }else{using var icon=new Font("Segoe UI Symbol",11);TextRenderer.DrawText(e.Graphics,Glyph,icon,new Rectangle(Width>70?9:(Width-23)/2,0,23,Height),Theme.Text,TextFormatFlags.VerticalCenter|TextFormatFlags.HorizontalCenter);}
        if(Width>70){var text=Text.ToUpper(System.Globalization.CultureInfo.GetCultureInfo("tr-TR"));var bounds=new Rectangle(38,0,Math.Max(1,Width-44),Height);if(!caption.Draw(e.Graphics,text,Font,bounds,Theme.Text,Height>=48))TextRenderer.DrawText(e.Graphics,text,Font,bounds,Theme.Text,TextFormatFlags.NoPrefix|TextFormatFlags.VerticalCenter|TextFormatFlags.Left|(Height>=48?TextFormatFlags.WordBreak:TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis));}

    }
    protected override void OnClick(EventArgs e){UiSound.Click();base.OnClick(e);}
}


