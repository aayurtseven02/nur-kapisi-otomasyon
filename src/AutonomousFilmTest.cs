namespace AnkaStudio;

// Exercises the same topic-only runner used by the UI; no prepared images or narration.
public static class AutonomousFilmTest
{
    public static async Task CheckScenario(Store store,string scenario)
    {
        using var stop=new CancellationTokenSource(TimeSpan.FromMinutes(8));
        var flow=new CreationFlow{Normal=false,TextMode="ai",Prompt=File.ReadAllText(scenario),Job=new ProductionJob{Mode="full",PublishMode="review"}};
        var e=new Episode{Topic="Kullanıcının senaryo taslağı",ShortSeconds=60,IncludeLong=false,Creation=flow};
        var service=new Services(store,message=>File.AppendAllText(Path.Combine(store.Root,"scenario-check.log"),message+Environment.NewLine));
        var draft=await AutonomousProduction.Draft(e,flow,service,stop.Token,(name,value)=>File.WriteAllText(Path.Combine(store.Root,"scenario-"+name+".txt"),value));
        Json.Write(Path.Combine(store.Root,"scenario-check-result.json"),new{Passed=true,Words=ScriptLength.Count(draft.Text),draft.Text,draft.Sources});
    }
    public static async Task CheckVisual(Store store,string image)
    {
        using var stop=new CancellationTokenSource(TimeSpan.FromMinutes(6));
        bool accepted=await StoryVisualReview.Check(store,Path.GetFullPath(image),"A stylized 3D cartoon scene with people. Visible people have coherent anatomy: two arms and two hands each, with natural occlusion allowed; no duplicated limbs or obviously malformed eyes.",stop.Token,message=>File.AppendAllText(Path.Combine(store.Root,"visual-check.log"),message+Environment.NewLine),expectedPeople:1);
        Json.Write(Path.Combine(store.Root,"visual-check-result.json"),new{Accepted=accepted,Image=Path.GetFullPath(image)});
    }
    public static async Task CheckSpeech(Store store)
    {
        using var stop=new CancellationTokenSource(TimeSpan.FromMinutes(5));string path=Path.Combine(store.Root,"speech-check.wav");
        await LocalVoice.SpeakWithFallback("Merhaba. Bu kısa deneme, seçili Türkçe anlatıcının otomatik olarak çalıştığını kontrol ediyor.",path,store.Settings,stop.Token,message=>File.AppendAllText(Path.Combine(store.Root,"speech-check.log"),message+Environment.NewLine));
        double duration=await Media.Duration(store,path,stop.Token);if(duration<1||new FileInfo(path).Length<1000)throw new InvalidOperationException("Ses testi boş veya çok kısa.");
        Json.Write(Path.Combine(store.Root,"speech-result.json"),new{Passed=true,File=path,Duration=duration,Voice=store.Settings.EdgeTtsVoice});
    }
    public static async Task Run(Store store,string topic,string? resumeId=null)
    {
        var e=resumeId==null?new Episode{Topic=topic,IncludeLong=false,ShortCount=1,ShortSeconds=45,Music=false,Creation=new CreationFlow{Normal=false,TextMode="ai",Prompt="Konuyu kısa bir 3D çizgi film kıssası olarak anlat. Rivayetleri kesin tarihsel gerçek gibi sunma.",CoverChoice="none",Job=new ProductionJob{PublishMode="review"}}}:store.Episodes.Single(x=>x.Id==resumeId);
        if(resumeId==null){store.Episodes.Add(e);store.Save();}
        try{
            var runner=new AutonomousRunner(store,new Engine(store));bool resume=resumeId!=null;
            for(int attempt=0;;attempt++){
                try{await runner.Run(e,false,resume);break;}
                catch(Exception)when(attempt<5&&e.RetryAt!=null&&e.Creation!.Job.Pending&&e.Status!="Durduruldu"){
                    while(e.RetryAt>DateTimeOffset.UtcNow)await Task.Delay(TimeSpan.FromSeconds(Math.Clamp((e.RetryAt.Value-DateTimeOffset.UtcNow).TotalSeconds,.05,15)));
                    resume=true;
                }
            }
        }
        finally{Json.Write(Path.Combine(store.Root,"autonomous-result.json"),new{e.Id,e.Status,e.Error,References=e.AiStoryReferenceFile,Scenes=Rules.Scripts(e).SelectMany(x=>x.Script.Scenes).Select(s=>new{s.Narration,s.StockFile,s.AiVideoFile,s.VideoFailure}),Videos=e.Videos.Select(v=>new{v.File,v.QualityPassed,v.Checks})});}
    }
}
