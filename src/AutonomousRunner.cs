namespace AnkaStudio;

public sealed class AutonomousRunner(Store store,Engine engine)
{
    int running;
    public bool IsRunning=>Volatile.Read(ref running)!=0;
    public async Task Run(Episode e,bool reviewText,bool resume=false)
    {
        var f=e.Creation??throw new InvalidOperationException("Proje akışı bulunamadı.");
        if(engine.Busy||Interlocked.CompareExchange(ref running,1,0)!=0)throw new InvalidOperationException("Önce çalışan işi tamamla veya durdur.");
        try{
            var settings=e.ProductionSettings??=CreationFlow.Copy(VideoSettings.For(store.Settings,e));
            if(!resume){
                f.Job.Mode=reviewText?"review":f.Job.Mode=="review"&&f.TextApproved?"review":"full";AutonomousProduction.Configure(e,f);AutonomousMediaPolicy.Configure(e,settings);
                AutonomousProduction.ValidatePublication(e,settings,store.Settings.YouTubeChannelId,starting:true);
                if(f.Job.PublishMode!="review"&&store.Key("YOUTUBE_REFRESH_TOKEN")=="")throw new InvalidOperationException("Otomatik yayın için YouTube oturumunu bağla.");
                f.Job.Channel=store.Settings.YouTubeChannelId;f.Job.Authorization=AutonomousProduction.Authorization(e,settings,f.Job.Channel);f.Job.Repairs=0;f.Job.Pending=true;f.Job.Mark(ProductionJobPhase.Preparing);e.Error="";e.RetryAt=null;store.Save();
            }
            AutonomousProduction.ValidatePublication(e,settings,store.Settings.YouTubeChannelId);
            bool ready=false;
            await engine.External(async ct=>{
                var service=new Services(store,message=>{e.Log(message);store.Save();engine.ReportActivity(message);});
                ready=await AutonomousProduction.Prepare(e,f,service,reviewText,ct,()=>{store.Save();engine.ReportActivity(e.CurrentStage);});
                if(!ready)return;
                WorkflowMusic.Apply(store,e,settings);WorkflowArtwork.Apply(e,settings,f.CoverChoice);
                if(f.CoverChoice=="file"&&!File.Exists(settings.CoverPng))throw new InvalidOperationException("Seçili kapak bulunamadı.");
                if(e.Plan==null||f.PlannedTextHash!=f.Signature)await WorkflowMedia.Build(store,e,service,ct,engine.ReportActivity,plan=>engine.ShowPreparingPlan(e,plan));
            },"Otonom üretim • hazırlanıyor");
            if(!ready){f.Job.Pending=false;store.Save();return;}
            for(int attempt=0;attempt<2;attempt++){
                f.Job.Mark(ProductionJobPhase.Producing);store.Save();
                if(!Reliability.OutputsCurrent(e,false)||e.Videos.Any(v=>!v.QualityPassed))await engine.Produce(e);
                if(e.Status=="Durduruldu"){f.Job.Pending=false;f.Job.Mark(ProductionJobPhase.Stopped);store.Save();return;}
                if(e.Error==""&&e.Videos.Count>0&&e.Videos.All(v=>v.QualityPassed)&&Reliability.OutputsCurrent(e,false))break;
                if(attempt==0&&e.RetryAt!=null){f.Job.Repairs++;e.Log("Otonom onarım • hazır aşamalar korunarak üretim bir kez yeniden deneniyor.");continue;}
                throw new InvalidOperationException(e.Error==""?"Çıktı kalite kontrolü tamamlanamadı; ayrıntılar proje kaydında.":e.Error);
            }
            f.Reached=Math.Max(f.Reached,3);e.WizardStep=3;store.Save();
            if(f.Job.PublishMode=="review"){f.Job.Pending=false;f.Job.Mark(ProductionJobPhase.Completed);store.Save();return;}
            await new AutonomousPublication(store,engine).Complete(e,settings);
            f.Job.Pending=false;f.Job.Mark(ProductionJobPhase.Completed);e.RetryAt=null;e.WizardStep=4;f.Reached=4;store.Save();
        }catch(OperationCanceledException){f.Job.Pending=false;f.Job.Mark(ProductionJobPhase.Stopped);e.Status="Durduruldu";e.CurrentStage="Otonom iş durduruldu • hazır aşamalar korundu";store.Save();}
        catch(Exception ex){Reliability.Failed(e,ex,"Autonomous");e.Error=SupportPrivacy.Clean(store,ex.Message);e.Log("Otonom iş: "+e.Error);if(e.RetryAt==null)f.Job.Pending=false;f.Job.Mark(e.RetryAt==null?ProductionJobPhase.Failed:ProductionJobPhase.RetryWaiting);store.Save();throw;}
        finally{Interlocked.Exchange(ref running,0);}
    }

}
