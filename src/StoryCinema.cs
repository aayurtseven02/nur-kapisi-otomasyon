using System.Diagnostics;
using System.Drawing.Imaging;
using System.Globalization;
using System.Text.Json;

namespace AnkaStudio;

// Still-image animation only: no invented character/body motion or external footage.
public static class StoryCinema
{
    public const string Version="story-cinema-v1";
    public static string Profile(Scene scene)
    {
        string text=(scene.Narration+" "+scene.VisualQuery).ToLower(new CultureInfo("tr-TR"));
        bool Has(params string[] words)=>words.Any(text.Contains);
        if(Has("sarsıl","sarsıldı","deprem","earthquake","quake"))return "Sarsıntı";
        if(Has("deniz","tekne","okyanus","dalga","ocean","boat","sea ","waves"))return "Dalga";
        if(Has("kalabalık","meydan","crowd","market"))return "Yatay geçiş";
        if(Has("gökyüz","kule","kapı","sky","tower","gate"))return "Yükseliş";
        return "Yaklaşma";
    }
    public static string Camera(Scene scene,int w,int h,int fps,double duration,IReadOnlyList<Caption>? captions=null)
    {
        int frames=Math.Max(2,(int)Math.Ceiling(duration*fps));
        string u=$"min(1,on/{frames-1})",ease=$"({u})*({u})*(3-2*({u}))";
        string z=$"1.06+0.07*({ease})",x="iw/2-iw/zoom/2",y="ih/2-ih/zoom/2";
        switch(Profile(scene)){
            case "Yatay geçiş":z="1.14";x=$"(iw-iw/zoom)*({ease})";break;
            case "Yükseliş":z="1.12";y=$"(ih-ih/zoom)*(1-({ease}))";break;
            case "Dalga":z="1.12";x+=$"+iw*0.006*sin(on/{fps}*1.3)";y+=$"+ih*0.003*cos(on/{fps}*1.7)";break;
            case "Sarsıntı":
                var cue=captions?.FirstOrDefault(c=>c.Text.Contains("sars",StringComparison.OrdinalIgnoreCase)||c.Text.Contains("deprem",StringComparison.OrdinalIgnoreCase));
                double at=Math.Clamp(cue?.Start??duration*.45,0,Math.Max(0,duration-.4));
                string gate=$"between(on/{fps},{Media.Num(at)},{Media.Num(Math.Min(duration,at+1.2))})";
                x+=$"+iw*0.0025*sin(on*2.7)*{gate}";y+=$"+ih*0.0015*sin(on*3.1)*{gate}";break;
        }
        return $"scale={w*2}:{h*2}:force_original_aspect_ratio=increase,crop={w*2}:{h*2},setsar=1,zoompan=z='{z}':x='max(0,min(iw-iw/zoom,{x}))':y='max(0,min(ih-ih/zoom,{y}))':d=1:s={w}x{h}:fps={fps}";
    }
    public static async Task<string> Render(Store store,string source,Scene scene,int w,int h,double duration,Settings settings,string folder,CancellationToken ct,Action<string> log,IReadOnlyList<Caption>? captions=null)
    {
        if(!double.IsFinite(duration)||duration<=0||duration>3600)throw new InvalidOperationException("Sahne süresi geçersiz.");
        int fps=settings.FramesPerSecond;
        string key=TimedNarration.TextHash(JsonSerializer.Serialize(new {Version,Source=Reliability.FileHash(source),scene.Narration,scene.VisualQuery,w,h,duration,fps,settings.EncodingPreset,settings.Crf,captions}));
        string output=Path.Combine(folder,"cinema-"+key+".mp4"),marker=output+".sha";
        ct.ThrowIfCancellationRequested();
        if(File.Exists(output)&&File.Exists(marker)&&File.ReadAllText(marker)==Reliability.FileHash(output)){log("Hazır animasyon sahnesi tekrar kullanıldı.");return output;}
        string atmosphere=Path.Combine(folder,$"cinema-atmosphere-{w}-{h}.png");
        if(!File.Exists(atmosphere)){
            using var bitmap=new Bitmap(w+100,h+100,PixelFormat.Format32bppArgb);using var g=Graphics.FromImage(bitmap);g.Clear(Color.Transparent);
            var random=new Random(191);for(int i=0;i<42;i++){using var brush=new SolidBrush(Color.FromArgb(random.Next(30,95),235,225,205));float radius=random.Next(1,4);g.FillEllipse(brush,random.Next(w+100),random.Next(h+100),radius,radius);}
            bitmap.Save(atmosphere,ImageFormat.Png);
        }
        string graph=$"[0:v]{Camera(scene,w,h,fps,duration,captions)}[camera];[camera][1:v]overlay=x='-50+20*sin(t/4)':y='-50+25*cos(t/6)',format=yuv420p[v]";
        string pending=output+".pending.mp4";
        var args=new List<string>{"-y","-filter_complex_threads","1","-loop","1","-i",source,"-loop","1","-i",atmosphere,"-filter_complex",graph,"-map","[v]","-t",Media.Num(duration),"-an","-c:v","libx264","-preset",settings.EncodingPreset,"-crf",settings.Crf.ToString(),"-pix_fmt","yuv420p","-threads","2","-progress","pipe:1","-nostats",pending};
        log("Sinematik sahne • "+Profile(scene)+" • hazırlanıyor…");
        var info=new ProcessStartInfo(store.Ffmpeg){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=folder};foreach(var arg in args)info.ArgumentList.Add(arg);
        using var process=Process.Start(info)??throw new InvalidOperationException("Animasyon motoru başlatılamadı.");
        var errors=process.StandardError.ReadToEndAsync(ct);
        try{
            int last=-1;while(await process.StandardOutput.ReadLineAsync(ct) is string line){
                if(line.StartsWith("out_time_us=")&&double.TryParse(line[12..],NumberStyles.Float,CultureInfo.InvariantCulture,out double time)){
                    int percent=Math.Clamp((int)(time/1_000_000/duration*100),0,99);if(percent/20>last){last=percent/20;log($"Sinematik sahne • {Profile(scene)} • %{percent}");}
                }
            }
            await process.WaitForExitAsync(ct);string error=await errors;
            if(process.ExitCode!=0)throw new InvalidOperationException("Animasyon oluşturulamadı: "+error[^Math.Min(1800,error.Length)..]);
            ct.ThrowIfCancellationRequested();File.Move(pending,output,true);File.WriteAllText(marker,Reliability.FileHash(output));
            Json.Write(output+".json",new{Version,Profile=Profile(scene),duration,w,h,fps});log("Sinematik sahne • tamamlandı.");return output;
        }catch{try{if(!process.HasExited)process.Kill(true);}catch{}throw;}
        finally{if(File.Exists(pending))File.Delete(pending);}
    }
}
