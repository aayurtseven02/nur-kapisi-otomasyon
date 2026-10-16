using System.Diagnostics;
namespace AnkaStudio;
public static class InstallCleanup
{
    public static readonly string[] KnownFiles=["AnkaStudio.pdb","NuGet.Config",".gitignore","Sign-Release.ps1","TEST-RAPORU.md","test-sonuclari.json","0288-DEGISIKLIKLER.txt","0290-DEGISIKLIKLER.txt","BES-ADIM-OKU.txt","Sinematik-Hikaye-OKU.txt"];
    public static int Clean(string program,string backup)
    {
        program=Path.GetFullPath(program);backup=Path.GetFullPath(backup);int count=0;
        foreach(var name in KnownFiles){var file=Path.Combine(program,name);if(!File.Exists(file))continue;Directory.CreateDirectory(backup);File.Move(file,Path.Combine(backup,name));count++;}
        return count;
    }
}
public sealed partial class StudioForm
{
    void CleanOldProgramFiles()
    {
        if(engine.Busy){Message("Önce çalışan üretimi tamamla.");return;}
        if(Theme.Alert(this,"Eski geliştirme dosyaları yedek klasörüne taşınacak. Projeler, görseller ve hesap verileri korunur. Devam edilsin mi?","Program bakımı",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
        try{var start=new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory,"AnkaStudio.exe")){UseShellExecute=true,Verb="runas"};start.ArgumentList.Add("--cleanup-program-files");Process.Start(start);}
        catch(Exception ex){Message("Temizlik başlatılamadı: "+ex.Message);}
    }
}
