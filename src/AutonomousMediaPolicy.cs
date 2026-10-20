namespace AnkaStudio;

public static class AutonomousMediaPolicy
{
    public static void Configure(Episode e,Settings settings)
    {
        var f=e.Creation!;
        if(f.AiVisuals||f.CoverChoice=="ai"){
            e.AiStoryLocal=true;settings.CoverLocalEnabled=true;settings.CoverPaidAllowed=false;
            if(!File.Exists(settings.CoverLocalWorkflow))settings.CoverLocalWorkflow=LocalCoverEngine.FindInstalledWorkflow();
            if(!File.Exists(settings.CoverLocalWorkflow))throw new InvalidOperationException("Yerel görsel iş akışı bulunamadı. Aİ Resim Stüdyosu'ndan ComfyUI modelini ve iş akışını seç.");
        }
        if(f.CoverChoice=="ai"){settings.CoverMode="Şablon";settings.CoverAiEnabled=true;}
        WorkflowMusic.Validate(e,settings);
        if(f.OwnVisuals&&f.Files.Count>0&&!f.Job.OwnMediaConsent)throw new InvalidOperationException("Otonom üretimde eklediğin dosyaların kullanım hakkını başlangıç panelinde onayla.");
    }

}
