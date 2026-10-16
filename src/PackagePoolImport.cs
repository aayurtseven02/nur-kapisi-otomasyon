namespace AnkaStudio;

public sealed partial class StudioForm
{
    void ImportPackagesToPool()
    {
        if(engine.Busy || autoRunning || poolImportBusy) { Message("Çalışan işlem bitince aktarabilirsin."); return; }
        var scenes=store.Episodes.Where(e=>!e.Demo && e.Plan!=null).SelectMany(e=>Rules.Scripts(e).SelectMany(s=>s.Script.Scenes))
            .Where(s=>File.Exists(s.StockFile) && s.StockUrl!="" && s.LicenseUrl!="")
            .Select(s=>(File:s.StockFile,Query:s.VisualQuery,Url:s.StockUrl,Creator:s.Creator,License:s.LicenseUrl,Attribution:s.Attribution)).ToArray();
        using var dialog=Dialog("Videolar stok havuzuna alınıyor",800,350);
        var content=Theme.Flow(720); content.Dock=DockStyle.Fill; dialog.Controls.Add(content);
        var status=Theme.Label("Lütfen bekleyin • videolar havuza alınıyor…",12,true,Theme.Success,700,80); content.Controls.Add(status);
        var bar=new TurquoisePackageBar {Width=700,Height=32,Busy=true,Text="Aktarım hazırlanıyor…"}; content.Controls.Add(bar);
        content.Controls.Add(Theme.Label("Dosyalar arka planda kontrol edilip aktarılır. Aynı videolar birleştirilir; kaynak ve lisans bilgileri korunur.",10,width:700,height:60));
        using var cancellation=new CancellationTokenSource(); bool running=true; int done=0,failed=0;
        var close=Theme.Button("İptal",()=>{if(running){cancellation.Cancel();closeStatus();}else dialog.Close();},false,180);
        void closeStatus(){status.Text="Aktarım durduruluyor • alınan videolar korunacak…";}
        content.Controls.Add(close);
        var updates=new Progress<(int Done,string File,int Failed)>(x=>{
            if(dialog.IsDisposed || !running)return;
            done=x.Done; failed=x.Failed;
            bar.Value=scenes.Length==0?100:x.Done*100/scenes.Length;
            bar.Text=$"{x.Done}/{scenes.Length} video • %{bar.Value}";
            if(!cancellation.IsCancellationRequested)status.Text=$"Lütfen bekleyin • videolar havuza alınıyor…\n{x.Done}/{scenes.Length} • {Path.GetFileName(x.File)}";
        });
        poolImportBusy=true;
        dialog.FormClosing+=(_,e)=>{if(running){e.Cancel=true;cancellation.Cancel();closeStatus();}};
        dialog.Shown+=async(_,_)=>{
            try {
                var result=await Task.Run(async()=>{
                    var pool=new MediaPool(store); int processed=0,errors=0;
                    foreach(var scene in scenes){
                        cancellation.Token.ThrowIfCancellationRequested();
                        ((IProgress<(int Done,string File,int Failed)>)updates).Report((processed,scene.File,errors));
                        try {await pool.AddAsync(scene.File,scene.Query,MediaPool.CategoryFor(scene.Query),scene.Url,scene.Creator,scene.License,cancellation.Token,scene.Attribution);}
                        catch(Exception ex) when(ex is not OperationCanceledException){errors++;}
                        processed++;
                        ((IProgress<(int Done,string File,int Failed)>)updates).Report((processed,scene.File,errors));
                    }
                    return (processed,errors);
                });
                done=result.processed;failed=result.errors;
                bar.Value=100;bar.Text="Aktarım tamamlandı";
                status.Text=scenes.Length==0?"Aktarılacak uygun video bulunamadı.":$"Aktarım tamamlandı • {scenes.Length-failed} sahne görüntüsü işlendi.\nAynı dosyalar birleştirildi."+(failed>0?$"\n{failed} dosya aktarılamadı.":"");
            }catch(OperationCanceledException){bar.Text="Aktarım durduruldu";status.Text=$"İşlem iptal edildi • {done} dosya işlendi. Alınan videolar korunuyor.";}
            catch(Exception ex){status.Text="Aktarım tamamlanamadı: "+ex.Message;status.ForeColor=Theme.Warning;}
            finally {running=false;poolImportBusy=false;bar.Busy=false;close.Text="Kapat";}
        };
        try{dialog.ShowDialog(this);}finally{poolImportBusy=false;}
        poolOffset=0;Navigate("Stok havuzu");
    }
}
