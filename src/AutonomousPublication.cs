namespace AnkaStudio;

public sealed class AutonomousPublication(Store store,Engine engine)
{
    public async Task Complete(Episode e,Settings settings)
    {
        var f=e.Creation!;AutonomousProduction.ValidatePublication(e,settings,store.Settings.YouTubeChannelId);
        engine.Approve(e,planned:true);store.Save();bool scheduled=f.Job.PublishMode=="scheduled";
        foreach(var (asset,index) in e.Videos.Select((v,i)=>(v,i)))asset.PublishAt=scheduled?Rules.VideoDate(e,asset.Short,e.Videos.Take(index).Count(x=>x.Short)):null;
        if(scheduled&&e.DeferMissedPublication&&e.Videos.Any(v=>v.PublishAt<=DateTimeOffset.UtcNow.AddMinutes(10))){
            var baseDate=DateTimeOffset.UtcNow.AddMinutes(30);
            foreach(var (asset,index) in e.Videos.Select((v,i)=>(v,i)))asset.PublishAt=baseDate.AddHours(asset.Short?(e.Videos.Take(index).Count(x=>x.Short)+(e.IncludeLong?1:0))*e.ShortSpacingHours:0);
            e.Log("Başlangıçta izin verilen gecikme kuralı uygulandı: yayın en az 30 dakika ileri planlandı.");
        }
        f.Job.Mark(ProductionJobPhase.Uploading);store.Save();await engine.Upload(e,scheduled);
        if(e.Status=="Durduruldu")throw new OperationCanceledException();
        if(e.Error!="")throw new InvalidOperationException(e.Error);
        if(scheduled){e.Log("Başlangıçta seçilen takvimle otonom yayın planlandı.");return;}
        await engine.External(async ct=>{
            f.Job.Mark(ProductionJobPhase.Publishing);store.Save();
            AutonomousProduction.ValidatePublication(e,settings,store.Settings.YouTubeChannelId);
            var yt=new YouTube(store);var channel=await yt.ChannelIdentity(ct);Engine.VerifyChannel(f.Job.Channel,store.Settings.YouTubeChannelId,channel.Id);
            foreach(var asset in e.Videos){ct.ThrowIfCancellationRequested();await yt.PublishNow(asset,ct);e.Log(asset.Title+": otonom herkese açık yayın doğrulandı.");store.Save();}
            e.Status="YouTube’da yayında";VideoQueue.Reconcile(store);store.Save();
        },"Otonom yayın • herkese açık yayın doğrulanıyor");
    }
}
