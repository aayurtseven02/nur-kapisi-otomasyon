using System.Text.Json;
namespace AnkaStudio;
public sealed class StoryboardRequest{public string Title{get;set;}="";public List<SourceRef> Sources{get;set;}=[];public List<StoryboardRequestScene> Scenes{get;set;}=[];}
public sealed class StoryboardRequestScene{public string Narration{get;set;}="";public string Image{get;set;}="";public string Motion{get;set;}="";}
public static class StoryboardProduction
{
    public static Episode Create(Store store,string path)
    {
            var request=JsonSerializer.Deserialize<StoryboardRequest>(File.ReadAllText(path),Json.Options)??throw new InvalidOperationException("Sahne planı okunamadı.");
            if(request.Scenes.Count is <2 or >30||request.Title.Length is <3 or >100)throw new InvalidOperationException("Sahne sayısı veya başlık geçersiz.");
            if(request.Scenes.Any(s=>!File.Exists(s.Image)||!WorkflowMedia.ImageFile(s.Image)||s.Narration.Length<10||s.Motion.Length<20))throw new InvalidOperationException("Eksik sahne görseli, anlatım veya hareket planı.");
            if(request.Scenes.Select(s=>Reliability.FileHash(s.Image)).Distinct().Count()!=request.Scenes.Count)throw new InvalidOperationException("Her sahneye ayrı görsel gerekli.");
            string text=string.Join(" ",request.Scenes.Select(s=>s.Narration));
            var settings=CreationFlow.Copy(store.Settings);settings.LocalVideoModel=AnimationVideo.Model;settings.AutoPublish=false;settings.AutoProduce=false;settings.AutoResearch=false;settings.MusicPath="";settings.ClosingCardEnabled=false;settings.VideoLogoEnabled=false;settings.CoverMode="Şablon";settings.CoverAiEnabled=false;
            var e=new Episode{Topic=request.Title,IncludeLong=false,ShortCount=1,ShortSeconds=Math.Clamp((int)Math.Round(ScriptLength.Count(text)/(130d/60)),15,180),Music=false,PhotosOnly=true,AiStoryImages=false,AiStoryLocal=false,AiStoryAutonomous=true,Status="Senaryo hazır",ProductionSettings=settings,WizardStep=2};
            var f=new CreationFlow{Normal=false,TextMode="own",OwnText=text,Mode="auto",AiVisuals=true,StockVisuals=false,MixedSceneVideos=true,SceneVideoModel=AnimationVideo.Model,Subtitles=true,SubtitleStyle="Sade beyaz",CoverChoice="template",Reached=2,Split=true};e.Creation=f;f.ApprovedHash=TimedNarration.TextHash(text);f.Job.PublishMode="review";
            string folder=Path.Combine(store.Folder(e),"scene-references");Directory.CreateDirectory(folder);
            var script=new VideoScript{Title=request.Title,Description="Bir elma üzerinden dürüstlük ve vicdanı anlatan bir kıssanın 3D animasyon uyarlaması.",ThumbnailText="BİR ELMA",TextLayout=f.Layout(settings)};
            if(script.TextLayout!=null)script.TextLayout.AutoSceneTitles=false;
            for(int i=0;i<request.Scenes.Count;i++){var row=request.Scenes[i];string image=Path.Combine(folder,$"scene-{i+1:00}"+Path.GetExtension(row.Image));File.Copy(row.Image,image);string hash=Reliability.FileHash(image);File.WriteAllText(image+".sha",hash);script.Scenes.Add(new Scene{Narration=row.Narration,StockFile=image,UserLocal=true,RightsConfirmed=true,AiImageHash=hash,VisualKind="video",VideoMotionPrompt=row.Motion,VisualQuery=row.Motion,Attribution="AI sahne görseli • Nano Banana 2.1",Duration=ScriptLength.Count(row.Narration)/(130d/60)});}
            e.Plan=new ContentPlan{Shorts=[script],Sources=request.Sources};e.Plan.SpellingHash=Proofreading.Fingerprint(e.Plan);f.PlannedTextHash=f.Signature;f.OriginalSections=JsonSerializer.Serialize(script.Scenes,Json.Options);store.Episodes.Add(e);store.Save();return e;
    }
    public static async Task Run(Store store,string path,string? resumeId=null)
    {
        var e=resumeId==null?Create(store,path):store.Episodes.Single(x=>x.Id==resumeId);var engine=new Engine(store);await engine.Produce(e);
        Json.Write(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path))!,"production-result.json"),new{e.Id,e.Status,e.Error,Videos=e.Videos.Select(v=>new{v.File,v.QualityPassed})});
        if(e.Error!=""||e.Videos.Count==0)throw new InvalidOperationException(e.Error==""?"Üretim tamamlanmadı":e.Error);
    }
}
public sealed partial class StudioForm
{
 public async Task ProduceStoryboard(string path){try{if(engine.Busy)throw new InvalidOperationException("Önce çalışan işi tamamla.");var e=StoryboardProduction.Create(store,path);selectedWorkspaceId=e.Id;Navigate("Video hazırla");await engine.Produce(e);e.WizardStep=e.Videos.Count>0?3:2;e.Creation!.Reached=e.WizardStep;store.Save();Navigate("Video hazırla");}catch(Exception ex){Message(ex.Message);}}
}
