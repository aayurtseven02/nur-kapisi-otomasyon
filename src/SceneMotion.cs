namespace AnkaStudio;

// Uses measured narration duration. Cached shots survive cancellation and retries.
public static class SceneMotion
{
    public static int ShotCount(double duration){if(!double.IsFinite(duration)||duration<=0||duration>72)throw new InvalidOperationException("LTX için anlatım bölümünü 72 saniyeden kısa sahnelere ayır.");return Math.Max(1,(int)Math.Ceiling(duration/6));}
    public static string Signature(Scene scene,string reference,double duration,bool vertical,string quality="Hafif")=>TimedNarration.TextHash(LocalLtxVideo.Version+"|"+Reliability.FileHash(reference)+"|"+scene.VideoMotionPrompt+"|"+Media.Num(duration)+"|"+vertical+"|"+quality);
    static bool Valid(string path)=>File.Exists(path)&&File.Exists(path+".sha")&&File.ReadAllText(path+".sha").Trim()==Reliability.FileHash(path);
    public static async Task<string> Prepare(Store store,Episode episode,Scene scene,string reference,double duration,bool vertical,CancellationToken ct,Action<string> log,Func<string,string,string,int,bool,long,CancellationToken,Task>? generate=null)
    {
        ShotCount(duration);var videoSettings=VideoSettings.For(store.Settings,episode);string engine=episode.Creation?.SceneVideoModel is {Length:>0} choice?choice:videoSettings.LocalVideoModel;if(!ImportedLtx23.Choices.Contains(engine))throw new InvalidOperationException("Seçili yerel video motoru geçersiz.");log("Video motoru: "+engine);if(engine==AnimationVideo.Model&&generate==null)generate=(r,p,o,s,v,seed,token)=>AnimationVideo.Generate(store,r,p,o,v,seed,log,token);bool calm=engine==AnimationVideo.Model&&episode.AiStoryAutonomous;bool modern=ImportedLtx23.IsModern(engine)&&generate==null;int count=calm?FilmMotionPolicy.Shots(duration):modern?Math.Max(1,(int)Math.Ceiling(duration/5)):ShotCount(duration);if(scene.VideoMotionPrompt.Length<20)throw new InvalidOperationException("Bu sahnenin konuya uygun hareket planı yok.");
        string quality=episode.Creation?.SceneImageQuality??"";if(quality=="")quality=VideoSettings.For(store.Settings,episode).CoverLocalQuality??"Otomatik";bool automatic=quality=="Otomatik";if(automatic)quality=generate!=null?"Hafif":await LocalCoverEngine.RecommendedProfile(ct);else quality=LocalImageProfiles.Label(quality);log("LTX görsel kalitesi: "+quality);
        string signature=TimedNarration.TextHash(Signature(scene,reference,duration,vertical,quality)+"|"+engine+"|"+AnimationVideo.Version+(calm?"|"+FilmMotionPolicy.Version:""));string hash=modern?TimedNarration.TextHash(signature+"|"+engine+"|"+Reliability.FileHash(ImportedLtx23.PathName)):signature;string folder=Path.Combine(store.Folder(episode),"scene-videos",hash);Directory.CreateDirectory(folder);
        string output=Path.Combine(folder,"composition.mp4");
        if(Valid(output)){await Reliability.ValidateVideo(store,output,ct);scene.AiVideoFile=output;scene.AiVideoHash=hash;scene.VideoFailure="";return output;}
        var clips=new List<string>();double consumed=0;var recovery=new LocalVideoRecovery();
        for(int i=0;i<count;i++){
            ct.ThrowIfCancellationRequested();double target=Math.Min(calm?duration/count:modern?5:6,duration-consumed);int seconds=calm?6:modern?5:target>5?6:5;
            string raw=Path.Combine(folder,$"shot-{i+1}.mp4"),trimmed=Path.Combine(folder,$"edit-{i+1}.mp4");
            string prompt=(calm?FilmMotionPolicy.Prompt(scene.VideoMotionPrompt):scene.VideoMotionPrompt)+" Preserve the reference person's exact face, age, hair, beard and clothes, and the reference location. Natural coherent motion. Follow the camera movement explicitly requested above, smoothly and continuously; do not add abrupt zooms or cuts. Keep the face within frame and preserve the same identity throughout any requested close-up. Maintain the established action in this shot, with no new objects or people.";
            long seed=BitConverter.ToInt64(Convert.FromHexString(TimedNarration.TextHash(hash+"|"+i)),0)&long.MaxValue;
            if(!Valid(raw)){
                log($"LTX • anlatıma uygun kısa video {i+1}/{count} • {seconds} saniye");
                string shotReference=reference;
                if(calm&&i>0){shotReference=Path.Combine(folder,$"continue-{i+1}.png");await Media.Exec(store.Ffmpeg,["-y","-sseof","-0.08","-i",clips.Last(),"-frames:v","1",shotReference],folder,ct);}
                if(generate!=null)await generate(shotReference,prompt,raw,seconds,vertical,seed,ct);
                else await recovery.Run(error=>LocalLtxVideo.Generate(reference,prompt,raw,seconds,vertical,seed,log,ct,smaller:automatic&&error?.Message.Contains("GPU_MEMORY",StringComparison.Ordinal)==true,quality:quality,engine:engine),async()=>{
                    log("LTX • video tamamlanamadı; yerel motor bir kez kontrol edilip yeniden deneniyor. Hazır sahneler korunuyor.");
                    var settings=VideoSettings.For(store.Settings,episode);string workflow=modern?ImportedLtx23.PathName:settings.CoverLocalWorkflow;
                    if(!File.Exists(workflow))workflow=LocalCoverEngine.FindInstalledWorkflow();
                    await LocalCoverEngine.Start(workflow,log,ct,Path.Combine(store.Root,"logs","local-engine"));
                    if(modern){using var client=new HttpClient{Timeout=TimeSpan.FromSeconds(20)};await ImportedLtx23.Check(client,ct);}else await LocalLtxVideo.Models(ct);
                },ct);
                await Reliability.ValidateVideo(store,raw,ct);double actual=await Media.Duration(store,raw,ct);if(actual*(calm?1.25:1)<target-.08)throw new InvalidOperationException("LTX kısa video süresi anlatım parçasını karşılamıyor; aynı video döngüye alınmadı.");
                File.WriteAllText(raw+".sha",Reliability.FileHash(raw));
            }
            double playback=calm?Math.Max(1,target/await Media.Duration(store,raw,ct)):1;
            await Media.Exec(store.Ffmpeg,["-y","-i",raw,"-t",Media.Num(target),"-an","-vf",$"setpts={Media.Num(playback)}*(PTS-STARTPTS),fps=24,setsar=1","-c:v","libx264","-preset","fast","-crf","20","-pix_fmt","yuv420p",trimmed],folder,ct);
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
