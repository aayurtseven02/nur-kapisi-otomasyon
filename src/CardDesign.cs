using System.Drawing.Drawing2D;

using System.Drawing.Imaging;



namespace AnkaStudio;



public sealed class CardTextStyle

{

    public bool Visible { get; set; } = true;

    public string Font { get; set; } = "Georgia";

    public int Size { get; set; } = 72;

    public string Color { get; set; } = "#FFF2CB";

    public bool Bold { get; set; } = true;

    public bool Italic { get; set; }

    public bool Underline { get; set; }

    public int X { get; set; } = 8;

    public int Y { get; set; } = 18;

    public int Width { get; set; } = 84;

    public int Height { get; set; } = 24;

    public string Align { get; set; } = "Orta";

    public string VerticalAlign { get; set; } = "Üst";

    public int Rotation { get; set; }

    public int Opacity { get; set; } = 100;

    public int Outline { get; set; }

    public string OutlineColor { get; set; } = "#06101A";

    public int Shadow { get; set; } = 0;

    public string ShadowColor { get; set; } = "#000000";

    public bool Box { get; set; }

    public string BoxColor { get; set; } = "#081C2C";

    public int BoxOpacity { get; set; } = 65;

}

public sealed class CardDesign

{

    public int Version { get; set; }

    public string Name { get; set; } = "Yeni kart";

    public string Title { get; set; } = "Kanalınız";

    public string Body { get; set; } = "Güzel dualara birlikte eşlik edelim.";

    public string Footer { get; set; } = "Desteğiniz için teşekkür ederiz.";

    // Legacy fields are retained so earlier editable cards continue to load.

    public string Font { get; set; } = "Georgia";

    public int TitleSize { get; set; } = 72;

    public int BodySize { get; set; } = 38;

    public int FooterSize { get; set; } = 28;

    public int TitleY { get; set; } = 18;

    public int BodyY { get; set; } = 43;

    public int FooterY { get; set; } = 80;

    public string Background { get; set; } = "#081C2C";

    public string Foreground { get; set; } = "#FFF2CB";

    public string Accent { get; set; } = "#D8AF59";

    public string BackgroundImage { get; set; } = "";

    public bool Vertical { get; set; }

    public bool Square { get; set; }

    public int ImportedWidth { get; set; }

    public int ImportedHeight { get; set; }

    public static CardDesign Blank(){var d=new CardDesign{Title="",Body="",Footer="",Background="#FFFFFF",Border=false};d.Upgrade();return d;}

    public bool Bold { get; set; } = true;

    public bool Border { get; set; } = true;

    public CardTextStyle TitleStyle { get; set; } = new();

    public CardTextStyle BodyStyle { get; set; } = new() { Size=38,Y=43,Height=32 };

    public CardTextStyle FooterStyle { get; set; } = new() { Size=28,Y=80,Height=15 };

    public string Frame { get; set; } = "İnce çizgi";

    public int FrameWidth { get; set; } = 5;

    public int FrameInset { get; set; } = 4;

    public int FrameRadius { get; set; } = 45;

    public string BackgroundMode { get; set; } = "Düz renk";

    public string BackgroundSecond { get; set; } = "#165E65";

    public int GradientAngle { get; set; } = 90;

    public string ImageFit { get; set; } = "Doldur / kırp";

    public int ImageShade { get; set; } = 40;

    public bool Transparent { get; set; }

    public string Logo { get; set; } = "";

    public int LogoX { get; set; } = 82;

    public int LogoY { get; set; } = 8;

    public int LogoWidth { get; set; } = 12;

    public int LogoOpacity { get; set; } = 100;

    public static readonly string[] Frames = ["Çerçevesiz","İnce çizgi","Çift çizgi","Yuvarlatılmış","Köşe süsleri","Geometrik","Kemer"];

    public void Upgrade()

    {

        if(Version >= 2) return;

        TitleStyle=new(){Font=Font,Size=TitleSize,Y=TitleY,Color=Foreground,Bold=Bold};

        BodyStyle=new(){Font=Font,Size=BodySize,Y=BodyY,Height=32,Color=Foreground,Bold=Bold};

        FooterStyle=new(){Font=Font,Size=FooterSize,Y=FooterY,Height=15,Color=Foreground,Bold=Bold};

        Frame=Border?"İnce çizgi":"Çerçevesiz";Version=2;

    }

    public CardTextStyle Layer(string name) => name=="Başlık"?TitleStyle:name=="Ana metin"?BodyStyle:FooterStyle;

    public (int Width,int Height) Dimensions => ImportedWidth>0 && ImportedHeight>0?(ImportedWidth,ImportedHeight):Square?(1080,1080):Vertical?(1080,1920):(1920,1080);

    public static List<string> LayoutIssues(CardDesign d)

    {

        d.Upgrade(); var issues = new List<string>(); var (w,h) = d.Dimensions;

        if (w < 1 || h < 1 || (long)w*h > 40_000_000) { issues.Add("Kart boyutu geçersiz."); return issues; }

        using var bitmap = new Bitmap(1,1); using var g = Graphics.FromImage(bitmap); float factor = Math.Min(w/1920f,h/1080f);

        foreach (var (name,text,s) in new[] { ("Başlık",d.Title,d.TitleStyle), ("Ana metin",d.Body,d.BodyStyle), ("Alt metin",d.Footer,d.FooterStyle) }) {

            if (!s.Visible || string.IsNullOrWhiteSpace(text)) continue;

            float x=w*s.X/100f,y=h*s.Y/100f,bw=w*s.Width/100f,bh=h*s.Height/100f;

            double angle=s.Rotation*Math.PI/180; float rw=(float)(Math.Abs(bw*Math.Cos(angle))+Math.Abs(bh*Math.Sin(angle))),rh=(float)(Math.Abs(bw*Math.Sin(angle))+Math.Abs(bh*Math.Cos(angle)));

            float edge=(s.Outline+s.Shadow+4)*factor;

            if (x+bw/2-rw/2-edge<0 || y+bh/2-rh/2-edge<0 || x+bw/2+rw/2+edge>w || y+bh/2+rh/2+edge>h) issues.Add(name+": yazı alanı/efekti kartın dışına taşıyor. Konumu, genişliği veya dönüşü düzelt.");

            using var family=SafeFamily(s.Font); var style=(s.Bold?FontStyle.Bold:FontStyle.Regular)|(s.Italic?FontStyle.Italic:FontStyle.Regular); if(!family.IsStyleAvailable(style))style=FontStyle.Regular;

            using var font=new Font(family,Math.Clamp(s.Size,12,220)*factor,style,GraphicsUnit.Pixel);

            using var format=new StringFormat { Trimming=StringTrimming.None };

            g.MeasureString(text,font,new SizeF(Math.Max(1,bw),Math.Max(1,bh)),format,out var chars,out _);

            var required=g.MeasureString(text,font,new SizeF(Math.Max(1,bw),100000),format);

            if(chars<text.Length || required.Height>bh+1 || required.Width>bw+1)issues.Add(name+": metnin tamamı alana sığmıyor. Yazıyı sığdır düğmesini kullan veya alanı büyüt.");

        }

        return issues;

    }

    public static void Validate(CardDesign d) { var issues=LayoutIssues(d); if(issues.Count>0)throw new InvalidOperationException(string.Join("\n",issues)); }

    public static void FitText(CardDesign d)

    {

        d.Upgrade();

        foreach(var s in new[]{d.TitleStyle,d.BodyStyle,d.FooterStyle}) { s.Rotation=0;s.X=Math.Clamp(s.X,2,93);s.Y=Math.Clamp(s.Y,2,93);s.Width=Math.Clamp(s.Width,5,98-s.X);s.Height=Math.Clamp(s.Height,5,98-s.Y); }

        for(int i=0;i<208 && LayoutIssues(d).Count>0;i++) foreach(var s in new[]{d.TitleStyle,d.BodyStyle,d.FooterStyle})s.Size=Math.Max(12,s.Size-1);

    }

    static FontFamily SafeFamily(string name){try{return new FontFamily(name);}catch(ArgumentException){return new FontFamily("Segoe UI");}}

    static Color ColorOf(string color,int opacity=100) => Color.FromArgb(Math.Clamp(opacity,0,100)*255/100,ColorTranslator.FromHtml(color));

    public static Bitmap Draw(CardDesign d)

    {

        d.Upgrade(); var (width,height)=d.Dimensions;if(width<1||height<1||(long)width*height>40_000_000)throw new InvalidOperationException("Kart boyutu geçersiz veya çok büyük.");var bitmap=new Bitmap(width,height);

        try {

            using var g=Graphics.FromImage(bitmap);g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;g.Clear(d.Transparent?Color.Transparent:ColorOf(d.Background));

            if(!d.Transparent && d.BackgroundMode=="Renk geçişi"){using var gradient=new LinearGradientBrush(new Rectangle(0,0,width,height),ColorOf(d.Background),ColorOf(d.BackgroundSecond),d.GradientAngle);g.FillRectangle(gradient,0,0,width,height);}

            if(d.BackgroundImage!="") { using var image=Image.FromFile(d.BackgroundImage);if((long)image.Width*image.Height>40_000_000)throw new InvalidOperationException("Arka plan çok büyük.");

                if(d.ImageFit=="Esnet")g.DrawImage(image,0,0,width,height);else{float scale=d.ImageFit=="Sığdır"?Math.Min((float)width/image.Width,(float)height/image.Height):Math.Max((float)width/image.Width,(float)height/image.Height);float iw=image.Width*scale,ih=image.Height*scale;g.DrawImage(image,(width-iw)/2,(height-ih)/2,iw,ih);}

                using var shade=new SolidBrush(Color.FromArgb(Math.Clamp(d.ImageShade,0,100)*255/100,Color.Black));g.FillRectangle(shade,0,0,width,height);

            }

            float factor=width/1920f;float inset=Math.Min(width,height)*Math.Clamp(d.FrameInset,1,20)/100f;

            using var pen=new Pen(ColorOf(d.Accent),Math.Clamp(d.FrameWidth,1,24)*factor);var rect=new RectangleF(inset,inset,width-2*inset,height-2*inset);

            if(d.Frame!="Çerçevesiz") {

                if(d.Frame=="Yuvarlatılmış") { using var path=Rounded(rect,Math.Clamp(d.FrameRadius,5,150)*factor);g.DrawPath(pen,path); }

                else if(d.Frame=="Köşe süsleri") { float size=Math.Min(rect.Width,rect.Height)*.14f;foreach(var (x,y,sx,sy) in new[]{(rect.Left,rect.Top,1,1),(rect.Right,rect.Top,-1,1),(rect.Left,rect.Bottom,1,-1),(rect.Right,rect.Bottom,-1,-1)}) {g.DrawLine(pen,x,y,x+sx*size,y);g.DrawLine(pen,x,y,x,y+sy*size);} }

                else if(d.Frame=="Kemer") {float arch=Math.Min(rect.Width*.45f,rect.Height*.35f);g.DrawArc(pen,rect.Left,rect.Top,rect.Width,arch*2,180,180);g.DrawLine(pen,rect.Left,rect.Top+arch,rect.Left,rect.Bottom);g.DrawLine(pen,rect.Right,rect.Top+arch,rect.Right,rect.Bottom);g.DrawLine(pen,rect.Left,rect.Bottom,rect.Right,rect.Bottom);}

                else {g.DrawRectangle(pen,rect.X,rect.Y,rect.Width,rect.Height);if(d.Frame=="Çift çizgi"||d.Frame=="Geometrik"){float gap=16*factor;g.DrawRectangle(pen,rect.X+gap,rect.Y+gap,rect.Width-2*gap,rect.Height-2*gap);}

                    if(d.Frame=="Geometrik")foreach(var point in new[]{new PointF(rect.Left,rect.Top),new PointF(rect.Right,rect.Top),new PointF(rect.Left,rect.Bottom),new PointF(rect.Right,rect.Bottom)}){float size=22*factor;g.DrawPolygon(pen,new PointF[]{new(point.X,point.Y-size),new(point.X+size,point.Y),new(point.X,point.Y+size),new(point.X-size,point.Y)});}}

            }

            void Text(string text,CardTextStyle style) {

                if(!style.Visible||text=="")return;

                var box=new RectangleF(width*Math.Clamp(style.X,0,95)/100f,height*Math.Clamp(style.Y,0,95)/100f,width*Math.Clamp(style.Width,5,100)/100f,height*Math.Clamp(style.Height,5,100)/100f);

                var state=g.Save();g.TranslateTransform(box.X+box.Width/2,box.Y+box.Height/2);g.RotateTransform(style.Rotation);g.TranslateTransform(-box.X-box.Width/2,-box.Y-box.Height/2);

                if(style.Box){using var b=new SolidBrush(ColorOf(style.BoxColor,style.BoxOpacity));using var bp=Rounded(box,12*factor);g.FillPath(b,bp);}

                using var format=new StringFormat{Alignment=style.Align=="Sol"?StringAlignment.Near:style.Align=="Sağ"?StringAlignment.Far:StringAlignment.Center,LineAlignment=style.VerticalAlign=="Orta"?StringAlignment.Center:style.VerticalAlign=="Alt"?StringAlignment.Far:StringAlignment.Near,Trimming=StringTrimming.EllipsisWord};

                using var family=SafeFamily(style.Font);var fontStyle=(style.Bold?FontStyle.Bold:FontStyle.Regular)|(style.Italic?FontStyle.Italic:FontStyle.Regular);if(!family.IsStyleAvailable(fontStyle))fontStyle=FontStyle.Regular;

                using var path=new GraphicsPath();path.AddString(text,family,(int)fontStyle,Math.Clamp(style.Size,12,220)*factor,box,format);

                if(style.Shadow>0){var shadowState=g.Save();g.TranslateTransform(style.Shadow*factor,style.Shadow*factor);using var shadow=new SolidBrush(ColorOf(style.ShadowColor,style.Opacity*3/4));g.FillPath(shadow,path);g.Restore(shadowState);}

                if(style.Outline>0){using var outline=new Pen(ColorOf(style.OutlineColor,style.Opacity),style.Outline*factor){LineJoin=LineJoin.Round};g.DrawPath(outline,path);}

                using var brush=new SolidBrush(ColorOf(style.Color,style.Opacity));g.FillPath(brush,path);

                if(style.Underline && path.PointCount>0){var bounds=path.GetBounds();using var underline=new Pen(ColorOf(style.Color,style.Opacity),Math.Max(1,style.Size*factor/22));g.DrawLine(underline,bounds.Left,bounds.Bottom+4*factor,bounds.Right,bounds.Bottom+4*factor);}

                g.Restore(state);

            }

            Text(d.Title,d.TitleStyle);Text(d.Body,d.BodyStyle);Text(d.Footer,d.FooterStyle);

            if(d.Logo!=""){using var image=Image.FromFile(d.Logo);if((long)image.Width*image.Height>40_000_000)throw new InvalidOperationException("Logo çok büyük.");float w=width*Math.Clamp(d.LogoWidth,3,40)/100f;float h=w*image.Height/image.Width;using var attributes=new ImageAttributes();attributes.SetColorMatrix(new ColorMatrix{Matrix33=Math.Clamp(d.LogoOpacity,0,100)/100f});g.DrawImage(image,new Rectangle((int)(width*d.LogoX/100f),(int)(height*d.LogoY/100f),(int)w,(int)h),0,0,image.Width,image.Height,GraphicsUnit.Pixel,attributes);}

            return bitmap;

        }catch{bitmap.Dispose();throw;}

    }

    static GraphicsPath Rounded(RectangleF r,float radius){var path=new GraphicsPath();float d=Math.Min(radius*2,Math.Min(r.Width,r.Height));path.AddArc(r.Left,r.Top,d,d,180,90);path.AddArc(r.Right-d,r.Top,d,d,270,90);path.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);path.AddArc(r.Left,r.Bottom-d,d,d,90,90);path.CloseFigure();return path;}

    public void Layout(string name)

    {

        foreach(var style in new[]{TitleStyle,BodyStyle,FooterStyle}){style.X=8;style.Width=84;style.Align="Orta";style.VerticalAlign="Üst";style.Rotation=0;}

        TitleStyle.Y=18;BodyStyle.Y=43;FooterStyle.Y=80;

        if(name=="Sola hizalı"){foreach(var style in new[]{TitleStyle,BodyStyle,FooterStyle}){style.X=12;style.Width=76;style.Align="Sol";}}

        if(name=="Başlık üstte"){TitleStyle.Y=10;BodyStyle.Y=34;FooterStyle.Y=76;}

        if(name=="Alıntı odaklı"){TitleStyle.Y=12;TitleStyle.Size=48;BodyStyle.Y=32;BodyStyle.Height=44;BodyStyle.Size=68;FooterStyle.Y=82;}

        if(name=="Logo ve başlık"){TitleStyle.X=25;TitleStyle.Width=65;TitleStyle.Y=14;LogoX=8;LogoY=10;LogoWidth=14;BodyStyle.Y=43;}

    }

}

