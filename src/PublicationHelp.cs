namespace AnkaStudio;



public sealed partial class StudioForm

{

    static string PublicationExplanation(string action) => action switch {

        "Video önizleme" => "Hazır video dosyasını oynatıcıda açarak yayın öncesinde görüntü, ses ve altyazıları kontrol etmeni sağlar. Birden fazla video varsa izlemek istediğini seçersin. Yayın veya yükleme başlatmaz.",

        "Instagram / TikTok / Facebook’ta Paylaş" => "Sosyal paylaşım panelini açar. Hesabı ve paylaşılacak içeriği seçip önizleme ve paylaşım onayını bu panelde tamamlarsın. Bu düğmeye basmak tek başına yayın başlatmaz.",

        "Takvimi kaydet" => "Seçtiğin tarih, saat ve ayarları programda kaydeder. YouTube'da yayın başlatmaz. Saatler Türkiye saatidir (UTC+3).",

        "Shorts tasarım kapağını aç" => "Hazırlanan kapak resmini açar. Bu düğme resmi YouTube'a yüklemez.",

        "YouTube'a özel yükle" => "Onaylanan videoları YouTube'a gizli olarak yükler. İzleyicilere açmaz. Daha sonra hemen yayınlayabilir veya tarih belirleyebilirsin.",

        "Yüklenen açıklamaları güncelle" => "YouTube'a yüklenmiş videoların açıklamalarını günceller. Video dosyasını ve görünürlüğünü değiştirmez.",

        "Yüklenen videoları şimdi yayına al" => "Bu paketin YouTube'a yüklenmiş tüm videolarını hemen herkese açık yapar. Önce tüm videoların yüklenmiş ve güncel çıktılarının onaylanmış olması gerekir. Yayına almadan önce onay sorulur.",

        "YouTube'da yayını planla" => "Eksik videoları yükler veya mevcut yüklemeleri seçtiğin tarih ve saatte yayınlanacak şekilde ayarlar. Tarih en az 10 dakika ileride olmalıdır. Normal videodan sonraki ilk Shorts ve sonraki Shorts videoları, seçtiğin saat aralığıyla planlanır. Yüklemeler tamamlandıktan sonra bilgisayar kapalı olabilir.",

        "Silinen YouTube bağlantılarını sıfırla" => "Videoları YouTube'dan zaten sildiysen programdaki eski yükleme bağlantılarını temizler; yeniden yükleyebilmeni sağlar. Yerel video ve senaryo dosyalarını korur. YouTube'dan video silmez.",

        "YouTube yayın planını kaldır" => "YouTube'daki yayın tarihini kaldırır ve yüklenmiş videoları gizli yapar. Yayında olan videolar da gizli olur. Videoyu silmez.",

        _ => throw new InvalidOperationException("Yayın düğmesi açıklaması eksik: " + action)

    };



    public void CapturePublicationHelp(string directory)

    {

        Directory.CreateDirectory(directory);

        ScheduleDialog(new Episode { Topic = "Yayın tuşları bilgi görünümü" }, dialog => {

            try {

                var flow = dialog.Controls.OfType<FlowLayoutPanel>().Single();

                var infos = flow.Controls.OfType<FlowLayoutPanel>().SelectMany(r => r.Controls.OfType<Button>()).Where(b => Equals(b.Tag, "publication-info")).ToArray();

                if (infos.Length < 9 || infos.Any(b => string.IsNullOrWhiteSpace(b.AccessibleDescription))) throw new InvalidOperationException("Bilgi düğmeleri eksik.");

                dialog.PerformLayout(); Application.DoEvents();

                using (var bitmap = new Bitmap(dialog.Width, dialog.Height)) { dialog.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(directory, "publication-help.png")); }

                using var close = new System.Windows.Forms.Timer { Interval = 300 };

                close.Tick += (_, _) => { var popup = Application.OpenForms.Cast<Form>().FirstOrDefault(x => x.Text == "Takvimi kaydet"); if (popup == null) return; close.Stop(); using var image = new Bitmap(popup.Width, popup.Height); popup.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size)); image.Save(Path.Combine(directory, "publication-info-popup.png")); popup.Close(); };

                close.Start(); infos[0].PerformClick();

                File.WriteAllText(Path.Combine(directory, "verified.txt"), "7 bilgi düğmesi ve açıklama penceresi doğrulandı. Yayın işlemi başlatılmadı.");

            } finally { dialog.Close(); }

        });

    }

}

