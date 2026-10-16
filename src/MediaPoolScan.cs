namespace AnkaStudio;

public sealed partial class StudioForm
{
    void ScanPoolFolder()
    {
        if (engine.Busy || autoRunning || poolImportBusy) { Message("Çalışan işlem bitince havuz klasörünü tarayabilirsin."); return; }
        using var dialog = Dialog("Stok klasörünü tara / listeyi güncelle", 800, 470);
        var content = Theme.Flow(720); content.Dock = DockStyle.Fill; dialog.Controls.Add(content);
        var status = Theme.Label("Havuz klasöründeki yeni videolar taranıyor...", 12, true, Theme.Teal, 700, 90); content.Controls.Add(status);
        var progress = new ProgressBar { Width = 700, Height = 22 }; content.Controls.Add(progress);
        content.Controls.Add(Theme.Label("MP4, MOV, MKV ve WEBM dosyaları arka planda kontrol edilir. Dosyalar yeniden kopyalanmaz. Aynı içeriğe sahip videolar bir kez listelenir. Tek taramada en fazla 500 yeni dosya işlenir.", 10, width: 700, height: 90));
        content.Controls.Add(Theme.Label("Dosya adları etiket olur. Doğrudan eklenen videolar genel fon seçiminde kullanılabilir; etiketlerini düzenleyebilir ve bu tercihi kapatabilirsin. Kullanım hakkını yayın öncesinde kontrol et.", 10, color: Theme.Warning, width: 700, height: 75));
        using var cancellation = new CancellationTokenSource(); bool running = true; poolImportBusy = true;
        var cancel = Theme.Button("İptal", () => { if (running) { cancellation.Cancel(); status.Text = "Tarama durduruluyor; tamamlanan kayıtlar korunacak..."; } else dialog.Close(); }, false, 180); content.Controls.Add(cancel);
        var updates = new Progress<(int Done, int Total, string Name)>(x => {
            if (dialog.IsDisposed) return; progress.Maximum = Math.Max(1, x.Total); progress.Value = x.Done;
            status.Text = $"{x.Done}/{x.Total} • {x.Name}";
        });
        dialog.FormClosing += (_, e) => { if (running) { e.Cancel = true; cancellation.Cancel(); } };
        dialog.Shown += async (_, _) => {
            try {
                var result = await Task.Run(() => new MediaPool(store).ScanFolder(cancellation.Token, updates));
                status.Text = $"Tarama tamamlandı.\nYeni: {result.Added} • Kayıtlı: {result.Existing} • Aynı içerik: {result.Duplicate} • Okunamadı: {result.Failed}" + (result.Remaining > 0 ? "\nDaha fazla dosya var; tarama düğmesine yeniden bas." : "");
            } catch (OperationCanceledException) { status.Text = "Tarama iptal edildi. Tamamlanan kayıtlar korunuyor."; }
            catch (Exception ex) { status.Text = "Tarama tamamlanamadı: " + ex.Message; status.ForeColor = Theme.Warning; }
            finally { running = false; poolImportBusy = false; cancel.Text = "Kapat"; }
        };
        try { dialog.ShowDialog(this); } finally { poolImportBusy = false; }
        poolOffset = 0; Navigate("Stok havuzu");
    }
}
