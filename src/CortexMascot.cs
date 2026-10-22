using System.Drawing.Drawing2D;
namespace AnkaStudio;

public static class CortexMascot
{
    public static Bitmap Load(){using var stream=typeof(CortexMascot).Assembly.GetManifestResourceStream("AnkaStudio.Assets.cortex-assistant.png")??throw new InvalidOperationException("Cortex görseli bulunamadı.");using var image=new Bitmap(stream);int left=image.Width,top=image.Height,right=0,bottom=0;for(int y=0;y<image.Height;y++)for(int x=0;x<image.Width;x++)if(image.GetPixel(x,y).A>12){left=Math.Min(left,x);top=Math.Min(top,y);right=Math.Max(right,x);bottom=Math.Max(bottom,y);}return right>left&&bottom>top?image.Clone(Rectangle.FromLTRB(left,top,right+1,bottom+1),System.Drawing.Imaging.PixelFormat.Format32bppArgb):new Bitmap(image);}
    public static PictureBox Corner(int size=58){var image=Load();var box=new PictureBox{Image=image,Size=new Size(size,size),SizeMode=PictureBoxSizeMode.Zoom,BackColor=Color.Transparent,AccessibleName="Cortex uygulama asistanı"};box.Disposed+=(_,_)=>image.Dispose();return box;}
}

public sealed class CortexButton : Button
{
    readonly Bitmap mascot=CortexMascot.Load();bool hover;
    public CortexButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);Height=78;FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;BackColor=Theme.Sidebar;ForeColor=Theme.Text;Cursor=Cursors.Hand;Text="Cortex";AccessibleRole=AccessibleRole.PushButton;}
    protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.Clear(Parent?.BackColor??Theme.Sidebar);g.SmoothingMode=SmoothingMode.AntiAlias;if(hover){using var shape=Shapes.Round(new Rectangle(1,1,Math.Max(1,Width-3),Math.Max(1,Height-3)),12);using var brush=new SolidBrush(Theme.Hover);g.FillPath(brush,shape);}int size=Math.Min(Height-8,Width>75?68:Width-4);int drawWidth=(int)(size*(float)mascot.Width/mascot.Height);g.DrawImage(mascot,new Rectangle(Width>75?5:(Width-drawWidth)/2,(Height-size)/2,drawWidth,size));if(Width>100){using var font=Theme.Font(12,true);using var small=Theme.Font(9);TextRenderer.DrawText(g,"Cortex",font,new Rectangle(82,12,Width-86,28),Theme.Text,TextFormatFlags.Left|TextFormatFlags.VerticalCenter);TextRenderer.DrawText(g,"Asistanı aç",small,new Rectangle(82,40,Width-86,22),Theme.Muted,TextFormatFlags.Left|TextFormatFlags.VerticalCenter);}}
    protected override void OnClick(EventArgs e){UiSound.Click();base.OnClick(e);}
    protected override void Dispose(bool disposing){if(disposing)mascot.Dispose();base.Dispose(disposing);}
}
