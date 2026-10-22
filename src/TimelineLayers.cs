namespace AnkaStudio;

public sealed class TimelineLayer
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Kind { get; set; } = "Görüntü";
    public string Name { get; set; } = "Yeni satır";
    public bool Enabled { get; set; } = true;
    public List<TimelineClip> Clips { get; set; } = [];
}

public sealed class TimelineClip
{
    public string File { get; set; } = "";
    public string Text { get; set; } = "";
    public double Start { get; set; }
    public double Duration { get; set; } = 5;
    public double SourceOffset { get; set; }
    public double Volume { get; set; } = .5;
    public int WidthPercent { get; set; } = 100;
    public int XPercent { get; set; } = 50;
    public int YPercent { get; set; } = 50;
    public bool RightsConfirmed { get; set; }
    public void Validate(string kind, double total)
    {
        if (kind is not ("Görüntü" or "Ses" or "Altyazı") ||
            new[] { Start, Duration, SourceOffset, Volume }.Any(x => !double.IsFinite(x)) ||
            Start < 0 || Start >= total || Duration < .1 || Duration > 1800 ||
            SourceOffset < 0 || SourceOffset > 86400 || Volume < 0 || Volume > 1 ||
            WidthPercent is < 5 or > 100 || XPercent is < 0 or > 100 || YPercent is < 0 or > 100)
            throw new InvalidOperationException("Ek timeline satırındaki klip ayarları geçersiz.");
        if (kind == "Altyazı") { if (string.IsNullOrWhiteSpace(Text)) throw new InvalidOperationException("Altyazı metni boş."); }
        else if (!System.IO.File.Exists(File) || !RightsConfirmed) throw new InvalidOperationException("Ek satırdaki dosyayı ve kullanım iznini kontrol et.");
    }
}

public static class TimelineLayers
{
    public static void Cut(VideoScript script,double start,double end)
    {
        if(!double.IsFinite(start)||!double.IsFinite(end)||start<0||end<=start)throw new InvalidOperationException("Kesilecek zaman aralığı geçersiz.");
        foreach(var layer in script.ExtraTracks??[]){
            var clips=new List<TimelineClip>();
            foreach(var clip in layer.Clips){
                double clipEnd=clip.Start+clip.Duration;
                if(clipEnd<=start){clips.Add(clip);continue;}
                if(clip.Start>=end){clip.Start-=end-start;clips.Add(clip);continue;}
                if(clip.Start<start){var before=CreationFlow.Copy(clip);before.Duration=start-clip.Start;if(before.Duration>=.1)clips.Add(before);}
                if(clipEnd>end){var after=CreationFlow.Copy(clip);after.Start=start;after.Duration=clipEnd-end;after.SourceOffset+=end-clip.Start;if(after.Duration>=.1)clips.Add(after);}
            }
            layer.Clips=clips;
        }
    }

    public static async Task Apply(Store store, VideoScript script, string output, double total, int width, int height, Settings settings, CancellationToken ct)
    {
        var layers = (script.ExtraTracks ?? []).Where(t => t.Enabled).ToArray();
        foreach (var layer in layers) foreach (var clip in layer.Clips) clip.Validate(layer.Kind, total);
        var audio = layers.Where(t => t.Kind == "Ses").SelectMany(t => t.Clips).Select(c => new MusicTrack {
            Enabled=true, File=c.File, Start=c.Start, End=Math.Min(total,c.Start+c.Duration),
            SourceOffset=c.SourceOffset, Volume=c.Volume, FadeIn=0, FadeOut=0, RightsConfirmed=c.RightsConfirmed
        }).ToList();
        if (audio.Count > 0) await TimelineEffects.Apply(store, new VideoScript { SoundEffects=audio }, output, total, ct);
        int index=0;
        foreach (var layer in layers.Where(t => t.Kind == "Görüntü")) foreach (var clip in layer.Clips) {
            string target=Path.Combine(Path.GetDirectoryName(output)!,"layer-"+(index++)+".mp4");
            var args=new List<string>{"-y","-i",output};
            if (WorkflowMedia.ImageFile(clip.File)) args.AddRange(["-loop","1"]);
            else args.AddRange(["-ss",Media.Num(clip.SourceOffset)]);
            args.AddRange(["-i",clip.File]);
            double end=Math.Min(total,clip.Start+clip.Duration);
            string filter=$"[1:v]trim=duration={Media.Num(end-clip.Start)},setpts=PTS-STARTPTS+{Media.Num(clip.Start)}/TB,scale={Math.Max(2,width*clip.WidthPercent/100/2*2)}:{height}:force_original_aspect_ratio=decrease,setsar=1[layer];[0:v][layer]overlay=x=(W-w)*{Media.Num(clip.XPercent/100d)}:y=(H-h)*{Media.Num(clip.YPercent/100d)}:eof_action=pass:repeatlast=0:enable='gte(t,{Media.Num(clip.Start)})*lt(t,{Media.Num(end)})'[v]";
            args.AddRange(["-filter_complex",filter,"-map","[v]","-map","0:a?","-c:v","libx264","-preset",settings.EncodingPreset,"-crf",settings.Crf.ToString(),"-pix_fmt","yuv420p","-c:a","copy","-t",Media.Num(total),"-movflags","+faststart",target]);
            await Media.Exec(store.Ffmpeg,args,Path.GetDirectoryName(output),ct);File.Move(target,output,true);
        }
        var captions=layers.Where(t=>t.Kind=="Altyazı").SelectMany(t=>t.Clips).Select(c=>new Caption(c.Start,Math.Min(total,c.Start+c.Duration),c.Text)).OrderBy(c=>c.Start).ToList();
        if(captions.Count>0){
            string folder=Path.GetDirectoryName(output)!,ass=Path.Combine(folder,"manual-layers.ass"),target=Path.Combine(folder,"manual-captions.mp4");
            await File.WriteAllTextAsync(ass,Captions.Ass(captions,width,height,height>width?48:36,"FFFFFF",height>width),ct);
            await Media.Exec(store.Ffmpeg,["-y","-i",output,"-vf","ass=manual-layers.ass","-c:v","libx264","-preset",settings.EncodingPreset,"-crf",settings.Crf.ToString(),"-c:a","copy","-t",Media.Num(total),target],folder,ct);File.Move(target,output,true);
        }
    }
}
