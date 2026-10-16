using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
namespace AnkaStudio;
public static partial class EditorPreview
{
    public static async Task FastFrame(Store store,VideoScript script,int index,double local,bool vertical,string folder,CancellationToken ct)
    {
        Directory.CreateDirectory(folder);var scene=script.Scenes[index];int width=vertical?540:960,height=vertical?960:540;
        double offset=script.Scenes.Take(index).Sum(s=>s.Duration);local=Math.Clamp(local,0,Math.Max(0,scene.Duration-.05));
        string source=scene.StockFile;var info=File.Exists(source)?new FileInfo(source):null;
        string key=TimedNarration.TextHash((info?.FullName??"")+"|"+info?.Length+"|"+info?.LastWriteTimeUtc.Ticks+"|"+Media.Num(local)+"|"+vertical);
        string clean=Path.Combine(folder,"source-"+key+".png");
        if(!File.Exists(clean)){
            if(info!=null){var args=new List<string>{"-y"};if(!new[]{".png",".jpg",".jpeg",".bmp",".webp"}.Contains(Path.GetExtension(source).ToLowerInvariant()))args.AddRange(["-ss",Media.Num(local)]);args.AddRange(["-i",source,"-vf",$"scale={width}:{height}:force_original_aspect_ratio=increase,crop={width}:{height}","-frames:v","1",clean]);await Media.Exec(store.Ffmpeg,args,folder,ct);}
            else{using var blank=new Bitmap(width,height);using var canvas=Graphics.FromImage(blank);canvas.Clear(Color.FromArgb(24,40,60));blank.Save(clean,ImageFormat.Png);}
        }
        ct.ThrowIfCancellationRequested();using var input=Image.FromFile(clean);using var image=new Bitmap(input);using var g=Graphics.FromImage(image);g.SmoothingMode=SmoothingMode.AntiAlias;
        var layout=script.TextLayout??TextLayout.Defaults(store.Settings,vertical);float scale=width/(vertical?1080f:1920f);
        void Draw(string text,bool title){
            if(string.IsNullOrWhiteSpace(text))return;string family=title?layout.TitleFont:layout.SubtitleFont;if(family=="")family=layout.Font;
            using var font=new Font(family,Math.Max(8,(title?layout.TitleSize:layout.SubtitleSize)*scale),FontStyle.Bold,GraphicsUnit.Pixel);
            var rect=new RectangleF(width*.06f,0,width*.88f,height*.28f);float y=(float)((title?layout.TitleY:layout.SubtitleY)*height/100d);var measured=g.MeasureString(text,font,(int)rect.Width);rect.Height=measured.Height+12;rect.Y=Math.Clamp(y-rect.Height/2,0,height-rect.Height);
            float x=(float)((title?layout.TitleX:layout.SubtitleX)*width/100d);rect.X=Math.Clamp(x-rect.Width/2,0,width-rect.Width);
            if(title?layout.TitleBackground:layout.SubtitleBackground){using var background=new SolidBrush(Color.FromArgb(Math.Clamp(layout.BackgroundOpacity*255/100,0,255),ColorTranslator.FromHtml("#"+layout.BackgroundColor.TrimStart('#'))));g.FillRectangle(background,rect);}
            using var format=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center};using var path=new GraphicsPath();path.AddString(text,font.FontFamily,(int)font.Style,font.Size,rect,format);
            if(layout.Outline>0){using var outline=new Pen(ColorTranslator.FromHtml("#"+layout.OutlineColor.TrimStart('#')),Math.Max(1,layout.Outline*scale)){LineJoin=LineJoin.Round};g.DrawPath(outline,path);}
            using var fill=new SolidBrush(Color.FromArgb(Math.Clamp(layout.Opacity*255/100,0,255),ColorTranslator.FromHtml("#"+(title?layout.TitleColor:layout.SubtitleColor).TrimStart('#'))));g.FillPath(fill,path);
        }
        if(layout.TitleEnabled&&offset+local<=layout.TitleSeconds)Draw(script.Title,true);
        if(layout.SubtitleEnabled){var caption=Captions(script).FirstOrDefault(c=>offset+local>=c.Start&&offset+local<c.End);Draw(caption?.Text??(string.IsNullOrWhiteSpace(scene.SubtitleText)?null:scene.SubtitleText)??scene.Narration,false);}
        ct.ThrowIfCancellationRequested();image.Save(Path.Combine(folder,"preview.png"),ImageFormat.Png);
    }
}
