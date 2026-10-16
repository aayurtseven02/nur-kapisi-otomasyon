namespace AnkaStudio;

public sealed partial class StudioForm
{
    void NvidiaScanPanel(FlowLayoutPanel panel, List<Action> saves)
    {
        var automatic = Theme.Check("NVIDIA: çalışan JSON modelini otomatik seç ve hata olursa diğerine geç", store.Settings.NvidiaAutoSelect, 740);
        saves.Add(() => store.Settings.NvidiaAutoSelect = automatic.Checked);
        panel.Controls.Add(automatic);
        panel.Controls.Add(Theme.Label("İzin açıksa ilk açılışta modeller arka planda sınanır. Kısa kontroller kota tüketir; tamamlanmış sonuçlar sonraki açılışlarda yeniden taranmaz. Üretim kotası veya her modelde aynı metin kalitesi garanti edilmez.", 9, color: Theme.Muted, width: 740, height: 70));
        var status = Theme.Label(NvidiaModels.Progress(store), 9, color: Theme.Teal, width: 740, height: 55);
        panel.Controls.Add(Actions(Theme.Button("NVIDIA Modellerini Tara", () => {
            if (!NvidiaModels.Enabled(store)) { Message("Önce NVIDIA anahtarını, bulut kullanım iznini ve otomatik seçimi kaydet."); return; }
            _ = NvidiaModels.Start(store, true); status.Text = "Model taraması başlatıldı…";
        }, false, 290), Theme.Button("Taramayı Durdur", () => NvidiaModels.Cancel(store), false, 220)));
        panel.Controls.Add(status);
        panel.Controls.Add(Theme.Button("Model Kontrol Sonuçları", () => {
            using var dialog = Dialog("NVIDIA Model Kontrol Sonuçları", 850, 650);
            var report = NvidiaModels.Read(store);
            var text = Theme.Input(string.Join("\r\n", report.Models.Select(m => $"{(m.RetryAt > DateTimeOffset.UtcNow ? "Bekliyor: " + m.Status : m.Status)} • {m.Model} • {m.Milliseconds / 1000.0:0.0} sn")), true, 750);
            text.ReadOnly = true; text.Dock = DockStyle.Fill; dialog.Controls.Add(text); dialog.ShowDialog(this);
        }, false, 290));
        var timer = new System.Windows.Forms.Timer { Interval = 1000 };
        timer.Tick += (_, _) => { if (!status.IsDisposed) { var current = NvidiaModels.Progress(store); if (status.Text != current) status.Text = current; } };
        panel.Disposed += (_, _) => timer.Dispose(); timer.Start();
    }
}
