namespace AnkaStudio;

// Uses measured narration duration. Cached shots survive cancellation and retries.
public static class SceneMotion
{
    public static int ShotCount(double duration){if(!double.IsFinite(duration)||duration<=0||duration>72)throw new InvalidOperationException("LTX için anlatım bölümünü 72 saniyeden kısa sahnelere ayır.");return Math.Max(1,(int)Math.Ceiling(duration/6));}
    public static string Signature(Scene scene,string reference,double duration,bool vertical)=>TimedNarration.TextHash(LocalLtxVideo.Version+"|"+Reliability.FileHash(reference)+"|"+scene.VideoMotionPrompt+"|"+Media.Num(duration)+"|"+vertical);
    static bool Valid(string path)=>File.Exists(path)&&File.Exists(path+".sha")&&File.ReadAllText(path+".sha").Trim()==Reliability.FileHash(path);
    public static async Task<string> Prepare(Store store,Episode episode,Scene scene,string reference,double duration,bool vertical,CancellationToken ct,Action<string> log,Func<string,string,string,int,bool,long,CancellationToken,Task>? generate=null)
    {
        int count=ShotCount(duration);if(scene.VideoMotionPrompt.Length<20)throw new InvalidOperationException("Bu sahnenin konuya uygun hareket planı yok.");
        string hash=Signature(scene,reference,duration,vertical),folder=Path.Combine(store.Folder(episode),"scene-videos",hash);Directory.CreateDirectory(folder);
        string output=Path.Combine(folder,"composition.mp4");
        if(Valid(output)){await Reliability.ValidateVideo(store,output,ct);scene.AiVideoFile=output;scene.AiVideoHash=hash;scene.VideoFailure="";return output;}
        var clips=new List<string>();double consumed=0;
        for(int i=0;i<count;i++){
            ct.ThrowIfCancellationRequested();double target=Math.Min(6,duration-consumed);int seconds=target>5?6:5;
            string raw=Path.Combine(folder,$"shot-{i+1}.mp4"),trimmed=Path.Combine(folder,$"edit-{i+1}.mp4");
            string prompt=scene.VideoMotionPrompt+" Preserve the reference person's exact face, age, hair, beard and clothes, and the reference location. Natural restrained motion, no speaking. Keep a locked medium-wide camera and the entire head and shoulders within frame throughout. Do not zoom, tilt or cut off the head. Maintain the established action in this shot, with no new objects or people.";
            long seed=BitConverter.ToInt64(Convert.FromHexString(TimedNarration.TextHash(hash+"|"+i)),0)&long.MaxValue;
            if(!Valid(raw)){
                log($"LTX • anlatıma uygun kısa video {i+1}/{count} • {seconds} saniye");
                if(generate!=null)await generate(reference,prompt,raw,seconds,vertical,seed,ct);
                else try{await LocalLtxVideo.Generate(reference,prompt,raw,seconds,vertical,seed,log,ct);}
                catch(InvalidOperationException ex)when(ex.Message.Contains("GPU_MEMORY",StringComparison.Ordinal)){log("LTX belleği yetmedi; daha düşük çözünürlükle bir kez yeniden deneniyor.");await LocalLtxVideo.Generate(reference,prompt,raw,seconds,vertical,seed,log,ct,smaller:true);}
                await Reliability.ValidateVideo(store,raw,ct);double actual=await Media.Duration(store,raw,ct);if(actual<target-.08)throw new InvalidOperationException("LTX kısa video süresi anlatım parçasını karşılamıyor; aynı video döngüye alınmadı.");
                File.WriteAllText(raw+".sha",Reliability.FileHash(raw));
            }
            await Media.Exec(store.Ffmpeg,["-y","-i",raw,"-t",Media.Num(target),"-an","-vf","fps=24,setsar=1","-c:v","libx264","-preset","fast","-crf","20","-pix_fmt","yuv420p",trimmed],folder,ct);
            clips.Add(trimmed);consumed+=target;
        }
        string list=Path.Combine(folder,"clips.txt");File.WriteAllLines(list,clips.Select(x=>"file '"+Path.GetFileName(x)+"'"));string pending=Path.Combine(folder,"composition-pending.mp4");
        await Media.Exec(store.Ffmpeg,["-y","-f","concat","-safe","1","-i",list,"-t",Media.Num(duration),"-an","-c","copy",pending],folder,ct);
        await Reliability.ValidateVideo(store,pending,ct);if(Math.Abs(await Media.Duration(store,pending,ct)-duration)>.12)throw new InvalidOperationException("LTX kurgu süresi konuşmayla eşleşmedi.");
        File.Move(pending,output,true);File.WriteAllText(output+".sha",Reliability.FileHash(output));
        Json.Write(output+".json",new{Version=LocalLtxVideo.Version,ReferenceHash=Reliability.FileHash(reference),scene.VideoMotionPrompt,Duration=duration,Shots=count,Audio="Anlatıcı ve altyazı mevcut üretim hattından gelir",Created=DateTimeOffset.UtcNow});
        scene.AiVideoFile=output;scene.AiVideoHash=hash;scene.VideoFailure="";return output;
    }
}
