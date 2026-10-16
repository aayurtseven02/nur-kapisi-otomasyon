namespace AnkaStudio;
public sealed partial class StudioForm
{
    public void CapturePackageProgress(string directory)
    {
        Directory.CreateDirectory(directory);
        var e = new Episode { Topic = "Hazreti Yusuf — Kuyudan Saraya, Sabır ve Affetmenin Kıssası", ShortCount = 1, Status = "Üretiliyor", Progress = 58, ScenesReviewed = true, ArtworkSettings = new Settings() };
        e.Plan = Engine.DemoPlan(e); foreach (var item in Rules.Scripts(e)) item.Script.TextLayout = new TextLayout();
        e.Log("Normal video • sahne 6/10: ses ve görüntü hazırlanıyor."); store.Episodes.Add(e);
        Navigate("İçerik paketleri"); PerformLayout(); Application.DoEvents();
        var card = page.Controls.Cast<Control>().Single(c => c.Controls.OfType<PackageStageBar>().Any());
        var segmented = card.Controls.OfType<PackageStageBar>().Single(); var bar = card.Controls.OfType<TurquoisePackageBar>().Single(); var status = card.Controls.OfType<AnimatedPackageStatus>().Single();
        var eta = card.Controls.OfType<Label>().Single(c => c.AccessibleName == "Tahmini kalan süre");
        if (segmented.Active != 4 || segmented.Completed.Take(4).Any(x => !x) || !bar.Busy || bar.Value != 58 || !status.Animating || bar.Bounds.IntersectsWith(eta.Bounds)) throw new InvalidOperationException("Paket ilerleme görünümü hatalı.");
        int first = status.AnimationFrame; status.AdvanceFrame(); if (status.AnimationFrame == first || status.Text != "Üretiliyor") throw new InvalidOperationException("Harf animasyonu çalışmıyor veya noktalar kaldırılmadı.");
        using (var bitmap = new Bitmap(card.Width, card.Height)) { card.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(directory, "package-progress.png")); }
        e.Status = "Denetim bekliyor"; e.Progress = 100; packageUpdates[e.Id]();
        if (status.Animating || bar.Busy || !eta.Text.Contains("İşlem tamamlandı")) throw new InvalidOperationException("Tamamlanınca hareket durmadı.");
        File.WriteAllText(Path.Combine(directory, "verified.txt"), "PASS: bölümlü gösterge, turkuaz çubuk ve yüzde, süre kutusu, hareketli durum yazısı, tamamlandığında durma.");
    }
}
