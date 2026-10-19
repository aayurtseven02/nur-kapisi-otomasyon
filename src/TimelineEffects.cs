namespace AnkaStudio;

public static class TimelineEffects
{
    public static string Filter(IReadOnlyList<MusicTrack> effects,double duration)
    {
        var chains=new List<string>();var labels=new List<string>{"[0:a]"};
        for(int i=0;i<effects.Count;i++){
            var effect=effects[i];effect.Validate();double length=Math.Min(effect.End>0?effect.End-effect.Start:duration-effect.Start,duration-effect.Start);if(length<=0)continue;
            string label="sfx"+i;string chain=$"[{i+1}:a]atrim=start={Media.Num(effect.SourceOffset)}:duration={Media.Num(length)},asetpts=PTS-STARTPTS,volume={Media.Num(effect.Volume)}";
            if(effect.FadeIn>0)chain+=$",afade=t=in:st=0:d={Media.Num(Math.Min(effect.FadeIn,length/2))}";
            if(effect.FadeOut>0)chain+=$",afade=t=out:st={Media.Num(Math.Max(0,length-effect.FadeOut))}:d={Media.Num(Math.Min(effect.FadeOut,length/2))}";
            chain+=$",adelay={Math.Round(effect.Start*1000)}:all=1[{label}]";chains.Add(chain);labels.Add("["+label+"]");
        }
        chains.Add(string.Join("",labels)+$"amix=inputs={labels.Count}:duration=first:normalize=0,alimiter=limit=0.95:level=0[mix]");return string.Join(";",chains);
    }
    public static async Task Apply(Store store,VideoScript script,string output,double duration,CancellationToken ct)
    {
        var effects=(script.SoundEffects??[]).Where(x=>x.Enabled&&x.Start<duration).ToList();if(effects.Count==0)return;
        var arguments=new List<string>{"-y","-i",output};foreach(var effect in effects){if(!File.Exists(effect.File))throw new InvalidOperationException("Ses efekti dosyası eksik: "+Path.GetFileName(effect.File));arguments.AddRange(["-i",effect.File]);}
        string target=Path.Combine(Path.GetDirectoryName(output)!,"video-with-effects.mp4");arguments.AddRange(["-filter_complex",Filter(effects,duration),"-map","0:v","-map","[mix]","-c:v","copy","-c:a","aac","-b:a","192k","-t",Media.Num(duration),"-movflags","+faststart",target]);await Media.Exec(store.Ffmpeg,arguments,Path.GetDirectoryName(output),ct);File.Move(target,output,true);
    }
}
