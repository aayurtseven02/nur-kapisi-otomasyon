namespace AnkaStudio;

public sealed partial class StudioForm
{
    void ScanAudioFolder()
    {
        if(engine.Busy || autoRunning || poolImportBusy){Message("Çalışan işlem bitince ses arşivini tarayabilirsin.");return;}
        using var dialog=Dialog("Ses arşivini güncelle",800,360);
        var content=Theme.Flow(720);content.Dock=DockStyle.Fill;dialog.Controls.Add(content);
        var status=Theme.Label("Lütfen bekleyin • ses dosyaları taranıyor…",12,true,Theme.Success,700,80);content.Controls.Add(status);
        var bar=new TurquoisePackageBar {Width=700,Height=32,Busy=true,Text="Tarama hazırlanıyor…"};content.Controls.Add(bar);
        content.Controls.Add(Theme.Label("Yeni dosyalar seçili kategoriye eklenir. Kaynak ve lisans bilgilerini düzenleyip doğruladıktan sonra üretimde kullanabilirsin.",10,width:700,height:60));
        using var cancellation=new CancellationTokenSource();bool running=true;
        var close=Theme.Button("İptal",()=>{if(running)cancellation.Cancel();else dialog.Close();},false,180);content.Controls.Add(close);
        var updates=new Progress<(int Done,int Total,string Name)>(x=>{if(dialog.IsDisposed || !running)return;bar.Value=x.Total==0?0:x.Done*100/x.Total;bar.Text=$"{x.Done}/{x.Total} dosya • %{bar.Value}";status.Text="Lütfen bekleyin • ses dosyaları taranıyor…\n"+x.Name;});
        poolImportBusy=true;
        dialog.FormClosing+=(_,e)=>{if(running){e.Cancel=true;cancellation.Cancel();status.Text="Tarama durduruluyor…";}};
        var kind=audioKind;
        dialog.Shown+=async(_,_)=>{
            try{var result=await Task.Run(()=>new AudioLibrary(store).Scan(kind,updates,cancellation.Token));bar.Value=100;bar.Text="Tarama tamamlandı";status.Text=$"Liste güncellendi • {result.Added} yeni dosya eklendi."+(result.Failed>0?$"\n{result.Failed} dosya okunamadı.":"");}
            catch(OperationCanceledException){status.Text="Tarama iptal edildi. Eklenen dosyalar korunuyor.";bar.Text="Tarama durduruldu";}
            catch(Exception ex){status.Text="Tarama tamamlanamadı: "+ex.Message;status.ForeColor=Theme.Warning;}
            finally{running=false;poolImportBusy=false;bar.Busy=false;close.Text="Kapat";}
        };
        try{dialog.ShowDialog(this);}finally{poolImportBusy=false;}
        audioOffset=0;Navigate("Ses arşivi");
    }
}
