using System.Drawing.Drawing2D;

using System.Drawing.Imaging;



namespace AnkaStudio;



public static class CoverDesigner

{

    public static readonly string[] Styles = ["Minimal", "Sinematik", "Belgesel", "Teknoloji", "Eğitim", "Haber", "Manevi", "Nur Kapısı • Altın sinema", "Eğlence"];

    public static string Suggest(string topic)

    {

        var text = topic.ToLowerInvariant();

        foreach (var (style, words) in new[] { ("Manevi", "kuran kur'an sure ayet hadis islam şükür dua"), ("Teknoloji", "teknoloji yazılım bilgisayar yapay robot"), ("Haber", "haber gündem seçim ekonomi"), ("Eğitim", "eğitim ders öğren matematik nasıl"), ("Belgesel", "tarih doğa bilim belgesel evren"), ("Eğlence", "oyun eğlence komedi mizah") }) if (words.Split(' ').Any(text.Contains)) return style;

        return "Sinematik";

    }

    public static Bitmap Draw(Settings settings, string title, bool vertical = false)

    {

        if(settings.CoverMode=="Kendi PNG kapak" && File.Exists(settings.CoverPng)) MediaFormat.Image(settings.CoverPng,vertical);

        int width = vertical ? 720 : 1280, height = vertical ? 1280 : 720;

        var bitmap = new Bitmap(width, height); using var g = Graphics.FromImage(bitmap);

        g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

        var (background, accent) = settings.CoverStyle switch {

            "Sinematik" => (Color.FromArgb(18, 26, 48), Color.FromArgb(242, 181, 77)),

            "Belgesel" => (Color.FromArgb(21, 46, 38), Color.FromArgb(223, 197, 132)),

            "Teknoloji" => (Color.FromArgb(17, 18, 49), Color.FromArgb(72, 229, 235)),

            "Eğitim" => (Color.FromArgb(24, 53, 87), Color.FromArgb(255, 207, 66)),

            "Haber" => (Color.FromArgb(29, 33, 45), Color.FromArgb(241, 67, 69)),

            "Manevi" => (Color.FromArgb(12, 34, 61), Color.FromArgb(244, 197, 86)),

            "Nur Kapısı • Altın sinema" => (Color.FromArgb(7, 22, 43), Color.FromArgb(255, 207, 87)),

            "Eğlence" => (Color.FromArgb(79, 25, 109), Color.FromArgb(255, 120, 54)),

            _ => (Color.FromArgb(18, 27, 40), Color.FromArgb(84, 224, 203)) };

        if (settings.CoverAccent != "") accent = ColorTranslator.FromHtml(settings.CoverAccent);

        using var fill = new LinearGradientBrush(new Rectangle(0, 0, width, height), background, ControlPaint.Light(background, .3f), 25f); g.FillRectangle(fill, 0, 0, width, height);

        var fullPng = settings.CoverMode == "Kendi PNG kapak";

        var imagePath = fullPng ? settings.CoverPng : settings.CoverBackground;

        if (!fullPng && imagePath == "" && settings.CoverStyle.StartsWith("Nur Kapısı")) { var bundled = ChannelPack.Asset("nur-kapisi-background.png"); if (File.Exists(bundled)) imagePath = bundled; }

        if (fullPng && !File.Exists(imagePath)) { bitmap.Dispose(); throw new InvalidOperationException("Kendi PNG kapağını seç veya şablon moduna geç."); }

        if (imagePath != "") {

            if (!File.Exists(imagePath)) { bitmap.Dispose(); throw new InvalidOperationException("Kapak arka plan dosyası bulunamadı."); }

            using var image = Image.FromFile(imagePath); var scale = Math.Max((float)width / image.Width, (float)height / image.Height);

            g.DrawImage(image, (width - image.Width * scale) / 2, (height - image.Height * scale) / 2, image.Width * scale, image.Height * scale);

        }

        if (fullPng || settings.CoverTitleStyle=="Yazısız") return bitmap;
        if(settings.CoverTitleRendered && imagePath!=""){
            if((settings.CoverSubtitleText??"").Trim()!=""){using var captionFont=new Font("Georgia",vertical?22:20,FontStyle.Bold);using var captionBrush=new SolidBrush(Color.FromArgb(255,218,111));using var captionFormat=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center};g.DrawString(settings.CoverSubtitleText,captionFont,captionBrush,new RectangleF(width*.04f,height*.73f,width*.48f,height*.1f),captionFormat);}
            var badge=settings.CoverLogo;if(badge==""&&ChannelPack.Matches(settings))badge=ChannelPack.Asset("nur-kapisi-kanal-logo.png");
            if(File.Exists(badge)){using var logo=Image.FromFile(badge);var badgeSize=width*.11f;var circle=new RectangleF(width-badgeSize-width*.025f,height-badgeSize-height*.025f,badgeSize,badgeSize);var saved=g.Save();using var clip=new GraphicsPath();clip.AddEllipse(circle);g.SetClip(clip);g.DrawImage(logo,circle);g.Restore(saved);using var outline=new Pen(Color.Gold,2);g.DrawEllipse(outline,circle);}
            return bitmap;
        }

        using var veil = new LinearGradientBrush(new Rectangle(0, 0, width, height), Color.FromArgb(220, background), Color.Transparent, 0f);
        veil.InterpolationColors=new ColorBlend{Colors=[Color.FromArgb(220,background),Color.FromArgb(100,background),Color.Transparent,Color.Transparent],Positions=[0f,.4f,.58f,1f]};g.FillRectangle(veil, 0, 0, width, height);

        using var color = new SolidBrush(accent); using var white = new SolidBrush(Color.White); using var line = new Pen(accent, 4);

        int margin = 70;

        if (settings.CoverStyle == "Teknoloji") for (int x = width / 2; x < width; x += 60) g.DrawLine(line, x, 0, x, height);

        else if (settings.CoverStyle == "Eğlence") { g.FillEllipse(color, width - 270, -120, 430, 430); g.FillEllipse(color, width - 120, height - 200, 220, 220); }

        else if (settings.CoverStyle == "Haber") g.FillRectangle(color, 0, 0, width, 20);

        else if (settings.CoverStyle is "Manevi" or "Nur Kapısı • Altın sinema") { g.DrawRectangle(line, 24, 24, width - 48, height - 48); g.DrawLine(line, margin, 130, width - margin, 130); if (settings.CoverStyle.StartsWith("Nur Kapısı")) { using var glow = new LinearGradientBrush(new Rectangle(0, height * 2 / 3, width, height / 3), Color.Transparent, Color.FromArgb(100, 238, 163, 44), 90); if (imagePath == "") g.FillRectangle(glow, 0, height * 2 / 3, width, height / 3); g.DrawRectangle(line, 35, 35, width - 70, height - 70); } }

        else g.FillRectangle(color, margin, 135, 120, 9);

        var typeface = settings.CoverStyle.StartsWith("Nur Kapısı") && settings.CoverFont == "Segoe UI" ? "Georgia" : settings.CoverFont;

        using var small = new Font(typeface, 22, FontStyle.Bold); if (settings.CoverShowChannelName) g.DrawString(settings.ChannelName, small, color, margin, 68);

        // Reserve the upper title-safe area for portrait covers; do not paint over a centered face.
        var area = new RectangleF(margin, vertical ? 55 : 205, width - margin * 2, vertical ? 240 : 340);

        using var format = new StringFormat { Trimming = StringTrimming.EllipsisWord, LineAlignment = StringAlignment.Center };

        var text = title.Trim() == "" ? "İÇERİĞİN BAŞLIĞI" : title.Trim(); float size = vertical ? 54 : 66;

        if (settings.CoverStyle.StartsWith("Nur Kapısı")) text = text.ToUpper(System.Globalization.CultureInfo.GetCultureInfo("tr-TR"));

        while (size > 24) { using var measure = new Font(typeface, size, FontStyle.Bold); if (g.MeasureString(text, measure, (int)area.Width).Height <= area.Height) break; size -= 2; }

        using var font = new Font(typeface, size, FontStyle.Bold); var shadow = area; shadow.Offset(3, 4); using var dark = new SolidBrush(Color.FromArgb(170, 0, 0, 0));

        if (settings.CoverStyle.StartsWith("Nur Kapısı") || settings.CoverTitleStyle?.StartsWith("3D")==true) {

            using var glyph = new GraphicsPath(); glyph.AddString(text, font.FontFamily, (int)FontStyle.Bold, font.SizeInPoints * g.DpiY / 72, area, format);

            for(int layer=9;layer>=1;layer--){using var depth=(GraphicsPath)glyph.Clone();using var move=new Matrix();move.Translate(layer*.65f,layer);depth.Transform(move);using var bronze=new SolidBrush(Color.FromArgb(255,65+layer*4,37+layer*2,8));g.FillPath(bronze,depth);}
            using(var glow=new Pen(Color.FromArgb(75,255,181,54),7){LineJoin=LineJoin.Round})g.DrawPath(glow,glyph);

            using var rim = new Pen(Color.FromArgb(22, 15, 5), 5) { LineJoin = LineJoin.Round }; g.DrawPath(rim, glyph);

            using var gold = new LinearGradientBrush(area, Color.FromArgb(255, 249, 208), Color.FromArgb(229, 149, 24), 90); g.FillPath(gold, glyph);

            using var shine = new Pen(Color.FromArgb(255, 218, 111), 1); g.DrawPath(shine, glyph);

        } else { g.DrawString(text, font, dark, shadow, format); g.DrawString(text, font, settings.CoverStyle is "Manevi" or "Sinematik" ? color : white, area, format); }

        if (settings.CoverLogo != "") {

            if (!File.Exists(settings.CoverLogo)) { bitmap.Dispose(); throw new InvalidOperationException("Kanal logosu bulunamadı."); }

            using var logo = Image.FromFile(settings.CoverLogo); var scale = Math.Min(150f / logo.Width, 105f / logo.Height); int w = (int)(logo.Width * scale), h = (int)(logo.Height * scale);

            int x = settings.CoverLogoPosition.StartsWith("Sol") ? margin : width - margin - w;

            int y = settings.CoverLogoPosition.EndsWith("üst") ? margin : height - margin - h; g.DrawImage(logo, x, y, w, h);

        }

        return bitmap;

    }

    public static void Save(string path, Settings settings, string title, bool vertical) { using var image = Draw(settings, title, vertical); image.Save(path, ImageFormat.Png); }

}



public sealed partial class StudioForm

{

    void CoverPage()

    {

        Title(current=="Medya arşivleri"?mediaKind+" arşivi":"Kapak tasarımları", "Tasarla, arşive kaydet ve videolarında yeniden kullan.");

        if(current!="Medya arşivleri"||mediaKind=="Kapak")page.Controls.Add(Actions(Theme.Button("Kapak ekle",AddArchiveCovers,true,200),Theme.Button("Klasörü tara / sayıları güncelle",()=>Navigate("Kapak tasarımları"),false,320),Theme.Button("Arşiv klasörünü aç",()=>{Directory.CreateDirectory(CoverArchive.Folder(store));Theme.Open(CoverArchive.Folder(store));},false,230)));
        if(current!="Medya arşivleri"||mediaKind=="Kapak")page.Controls.Add(Theme.Label($"Kapak arşivi • {CoverArchive.Files(store).Length} kapak",12,true,width:page.Width,height:40));
        if(current!="Medya arşivleri"||mediaKind=="Kart")page.Controls.Add(Actions(Theme.Button("Açılış kartı seç",()=>{if(engine.Busy){Message("Üretimin tamamlanmasını bekle.");return;}var selected=CoverArchive.Pick(store,this);if(selected==null)return;store.Settings.OpeningCardFile=selected;store.Settings.OpeningCardEnabled=true;store.Save();Navigate("Kapak tasarımları");},false,280),Theme.Button("Kapanış kartı seç",()=>{if(engine.Busy){Message("Üretimin tamamlanmasını bekle.");return;}var selected=CoverArchive.Pick(store,this);if(selected==null)return;store.Settings.ClosingCardFile=selected;store.Settings.ClosingCardEnabled=true;store.Save();Navigate("Kapak tasarımları");},false,280)));

        var cardSection = Theme.Flow(page.Width - 50); var editorSection = Theme.Flow(page.Width - 50); var logoSection = Theme.Flow(page.Width - 50); var coverSection = Theme.Flow(page.Width - 50);

        editorSection.Controls.Add(Theme.Label("Kendi açılış / kapanış kartını oluştur", 16, color: Theme.Teal, width: page.Width - 50, height: 45));

        editorSection.Controls.Add(Theme.Label("Başlık, açıklama, alt metin, yazı tipi, renk ve arka planı önizleyerek düzenle. Yatay veya Shorts biçiminde kaydet. Kart galeriden açılış ve kapanış için ayrı seçilir.", 10, width: page.Width - 50, height: 90));

        editorSection.Controls.Add(Theme.Button("Yeni kart oluştur", () => { if (engine.Busy) { Message("Üretim tamamlanınca oluştur."); return; } using var editor = new CardEditor(store); if (editor.ShowDialog(this) == DialogResult.OK) Navigate("Kapak tasarımları"); }, true, 300));
        editorSection.Controls.Add(Theme.Button("Kart arşivini aç",()=>{var folder=Path.Combine(store.Root,"card-assets");Directory.CreateDirectory(folder);Theme.Open(folder);},false,250));
        var cardArchive=Path.Combine(store.Root,"card-assets");
        cardSection.Controls.Add(Theme.Label($"Kart arşivi • {(Directory.Exists(cardArchive)?Directory.GetFiles(cardArchive,"*.png").Length:0)} kart",12,true,width:page.Width-50,height:40));

        var closingStatus = Theme.Label(store.Settings.ClosingCardEnabled ? "Seçili kapanış kartı: " + Path.GetFileName(store.Settings.ClosingCardFile) + " • " + store.Settings.ClosingCardSeconds + " sn" : "Kapanış kartı kapalı. Aşağıdan bir kart seçebilirsin.", 11, color: Theme.Teal, width: page.Width - 50, height: 50);

        cardSection.Controls.Add(closingStatus);

        cardSection.Controls.Add(Theme.Button("Kapanış kartını kapat", () => { if (engine.Busy) { Message("Üretim tamamlanınca değiştir."); return; } store.Settings.ClosingCardEnabled = false; store.Save(); closingStatus.Text = "Kapanış kartı kapalı."; }, false, 250));

        var openingStatus = Theme.Label(store.Settings.OpeningCardEnabled ? "Seçili açılış kartı: " + Path.GetFileName(store.Settings.OpeningCardFile) + " • " + store.Settings.OpeningCardSeconds + " sn" : "Açılış kartı kapalı.", 11, color: Theme.Teal, width: page.Width - 50, height: 45);

        cardSection.Controls.Add(openingStatus);

        cardSection.Controls.Add(Actions(Theme.Button("Açılış kartını kapat", () => { if (engine.Busy) { Message("Üretim tamamlanınca değiştir."); return; } store.Settings.OpeningCardEnabled = false; store.Save(); openingStatus.Text = "Açılış kartı kapalı."; }, false, 250), Theme.Button("Yeni açılış / kapanış kartı oluştur", () => { if (engine.Busy) { Message("Üretim tamamlanınca oluştur."); return; } using var editor = new CardEditor(store); if (editor.ShowDialog(this) == DialogResult.OK) Navigate("Kapak tasarımları"); }, true, 350)));

        var settings = JsonSerializerCopy(store.Settings);

        var readyLogo = ChannelPack.Asset("nur-kapisi-kanal-logo.png");
        logoSection.Controls.Add(Theme.Label($"Logo arşivi • {AiDesignDialog.LogoFiles(store).Length} logo",12,true,width:page.Width-50,height:40));
        logoSection.Controls.Add(Actions(Theme.Button("Yapay zekâ ile logo tasarla",()=>{if(engine.Busy){Message("Üretimin tamamlanmasını bekle.");return;}using var editor=new AiDesignDialog(store,true,store.Settings.ChannelName);if(editor.ShowDialog(this)==DialogResult.OK){store.Settings.VideoLogo=editor.SavedPath;store.Settings.VideoLogoEnabled=true;store.Save();Navigate("Kapak tasarımları");}},true,280),Theme.Button("Logo arşivinden seç",()=>{if(engine.Busy)return;var path=CoverArchive.Pick(store,this,AiDesignDialog.LogoFiles(store),"Arşivden logo seç");if(path!=null){store.Settings.VideoLogo=path;store.Settings.VideoLogoEnabled=true;store.Save();Navigate("Kapak tasarımları");}},false,240),Theme.Button("Logo arşivini aç",()=>{Directory.CreateDirectory(AiDesignDialog.LogoFolder(store));Theme.Open(AiDesignDialog.LogoFolder(store));},false,230)));

        if (ChannelPack.Matches(store.Settings) && File.Exists(readyLogo)) {

            var logos = Theme.Flow(page.Width); logos.Controls.Add(Theme.Label("Kanal logoları • Nur Kapısı", 16, color: Theme.Teal, width: page.Width - 50, height: 38));

            var picture = new PictureBox { Width = 180, Height = 180, SizeMode = PictureBoxSizeMode.Zoom }; using (var original = Image.FromFile(readyLogo)) picture.Image = new Bitmap(original); picture.Disposed += (_, _) => picture.Image?.Dispose(); logos.Controls.Add(picture);

            var corner = Theme.Combo(["Sağ üst", "Sol üst", "Sağ alt", "Sol alt"], store.Settings.VideoLogoPosition, 250);

            var logoSize = Theme.Number(store.Settings.VideoLogoWidthPercent, 5, 20, 250);

            Field(logos, "Videodaki köşe", corner); Field(logos, "Logo genişliği • video genişliğinin yüzdesi", logoSize);

            logos.Controls.Add(Actions(Theme.Button("Bu logoyu videolarda kullan", () => { if (engine.Busy) { Message("Üretim tamamlanınca kaydet."); return; } var folder = Path.Combine(store.Root, "cover-assets"); Directory.CreateDirectory(folder); var saved = Path.Combine(folder, "nur-kapisi-kanal-logo.png"); File.Copy(readyLogo, saved, true); store.Settings.VideoLogo = saved; store.Settings.VideoLogoEnabled = true; store.Settings.VideoLogoPosition = corner.Text; store.Settings.VideoLogoWidthPercent = (int)logoSize.Value; store.Save(); Message("Logo kaydedildi. Yeni üretilecek videolarda seçilen köşeye eklenir. Mevcut videolar değişmez."); }, true, 300), Theme.Button("Video logosunu kapat", () => { if (engine.Busy) { Message("Üretim tamamlanınca kaydet."); return; } store.Settings.VideoLogoEnabled = false; store.Save(); Message("Yeni üretimlerde video logosu kapatıldı."); }, false, 230)));

            logos.Controls.Add(Theme.Label("Gönderdiğin özgün logo kullanılır. Arka planı korunur. Altyazının üstüne gelmemesi için üst köşe önerilir.", 10, width: page.Width - 50, height: 48)); logoSection.Controls.Add(logos);

        }

        var sampleTitle = Theme.Input("YENİ BİR BAKIŞ AÇISI", width: 700); var channel = Theme.Input(settings.ChannelName, width: 700);

        var showChannel = Theme.Check("Kapakta kanal adını göster", settings.CoverShowChannelName, 700);
        var channelControls = Theme.Flow(page.Width);
        channelControls.Controls.Add(showChannel);
        Field(channelControls, "Kapakta gösterilecek kanal adı • elle yaz", channel);

        coverSection.Controls.Add(channelControls);
        coverSection.Controls.SetChildIndex(channelControls, 0);

        var mode = Theme.Combo(["Şablon", "Kendi PNG kapak"], settings.CoverMode, 700);

        var font = Theme.Combo(["Segoe UI", "Arial", "Georgia", "Impact", "Verdana"], settings.CoverFont, 700);

        var position = Theme.Combo(["Sağ alt", "Sol alt", "Sağ üst", "Sol üst"], settings.CoverLogoPosition, 700);

        var auto = Theme.Check("Konuya göre tarzı otomatik seç", settings.CoverAutoStyle);



        var aiEnabled = Theme.Check("Yapay zekâ arka plan tasarımını dene", settings.CoverAiEnabled);

        var paidAllowed = Theme.Check("Gemini / OpenAI ücretli görsel servislerine izin ver • NVIDIA için gerekli değil", settings.CoverPaidAllowed);

        var providerOrder = Theme.Input(settings.CoverProviderOrder, width: 700);

        var nvidiaModel = Theme.Combo(CoverFallback.ModelsFor("NVIDIA", settings), settings.CoverNvidiaModel, 700);
        var geminiModel = Theme.Input(settings.CoverGeminiModel, width: 700);

        var openAiModel = Theme.Input(settings.CoverOpenAiModel, width: 700);

        var requestLimit = Theme.Number(settings.CoverMonthlyRequests, 0, 1000, 450);

        var gallery = new FlowLayoutPanel { Width = page.Width, AutoSize = true, WrapContents = true };

        var previews = new List<(string Name, Button Button, PictureBox Picture)>();

        var styleChoice=Theme.Combo(CoverDesigner.Styles,settings.CoverStyle,700);
        gallery.Controls.Add(styleChoice);
        styleChoice.SelectedIndexChanged+=(_,_)=>{settings.CoverStyle=styleChoice.Text;auto.Checked=false;Refresh();};
        gallery.Controls.Add(Theme.Button("Kapağı önizle",()=>{Read();using var image=CoverDesigner.Draw(settings,sampleTitle.Text);using var dialog=Dialog("Kapak önizlemesi",1000,680);dialog.Controls.Add(new PictureBox{Dock=DockStyle.Fill,SizeMode=PictureBoxSizeMode.Zoom,Image=image});dialog.ShowDialog(this);},false,250));

        void Read() { settings.CoverShowChannelName = showChannel.Checked; settings.CoverChannelName = settings.ChannelName = channel.Text.Trim(); settings.CoverMode = mode.Text; settings.CoverFont = font.Text; settings.CoverLogoPosition = position.Text; settings.CoverAutoStyle = auto.Checked; settings.CoverAiEnabled = aiEnabled.Checked; settings.CoverPaidAllowed = paidAllowed.Checked; settings.CoverProviderOrder = providerOrder.Text.Trim(); settings.CoverNvidiaModel = nvidiaModel.Text; settings.CoverGeminiModel = geminiModel.Text.Trim(); settings.CoverOpenAiModel = openAiModel.Text.Trim(); settings.CoverMonthlyRequests = (int)requestLimit.Value; }

        void Refresh() { Read(); foreach (var preview in previews) { var copy = JsonSerializerCopy(settings); copy.CoverStyle = preview.Name; copy.CoverMode = "Şablon"; var image = CoverDesigner.Draw(copy, sampleTitle.Text); var old = preview.Picture.Image; preview.Picture.Image = image; old?.Dispose(); preview.Button.BackColor = settings.CoverStyle == preview.Name ? Theme.Teal : Theme.Elevated; preview.Button.ForeColor = settings.CoverStyle == preview.Name ? Theme.Bg : Theme.Text; } }
        coverSection.Controls.Add(Theme.Button("Kapağı yapay zekâ tasarlasın",()=>{if(engine.Busy){Message("Üretimin tamamlanmasını bekle.");return;}Read();using var editor=new AiDesignDialog(store,false,sampleTitle.Text,coverSettings:settings,coverTitle:sampleTitle.Text);if(editor.ShowDialog(this)==DialogResult.OK){settings.CoverPng=editor.SavedPath;mode.SelectedItem="Kendi PNG kapak";Refresh();}},true,350));

        coverSection.Controls.Add(gallery);

        var fields = Theme.Flow(page.Width); fields.Controls.Add(auto); fields.Controls.Add(aiEnabled); fields.Controls.Add(paidAllowed); Field(fields, "Görsel alternatif sırası • NVIDIA,Gemini,OpenAI • ardından otomatik kapak", providerOrder); Field(fields, "NVIDIA görsel modeli • kayıtlı NVIDIA API anahtarı", nvidiaModel); Field(fields, "Gemini görsel modeli", geminiModel); Field(fields, "OpenAI görsel modeli", openAiModel); Field(fields, "Aylık UTC görsel istek sınırı • hatalı/iptal istekler de sayılır", requestLimit); fields.Controls.Add(Theme.Label("Kota veya hız sınırı yaşayabilirsiniz. Ücretli görsel servisleri ücret oluşturabilir. Sorunda sıradaki alternatif, ardından yerel tasarım kullanılır. Ücretsiz öncelik açıkken Gemini ve OpenAI atlanır; NVIDIA denemesi devam eder. İstek sınırı para bütçesi değildir; sağlayıcı panelindeki bütçeyi ayrıca sınırla.", 10, color: Theme.Warning, width: 760, height: 110)); Field(fields, "Örnek başlık • gerçek üretimde senaryonun kapak metni kullanılır", sampleTitle); Field(fields, "Kapak yöntemi", mode); Field(fields, "Yazı tipi", font); Field(fields, "Logo konumu", position);

        void Pick(Action<string> set, string label) { using var dialog = new OpenFileDialog { Title = label, Filter = "Görsel|*.png;*.jpg;*.jpeg" }; if (dialog.ShowDialog(this) == DialogResult.OK) { using var image = Image.FromFile(dialog.FileName); if ((long)image.Width * image.Height > 40_000_000) { Message("Görsel en fazla 40 milyon piksel olabilir."); return; } var folder = label.Contains("logo",StringComparison.OrdinalIgnoreCase)?AiDesignDialog.LogoFolder(store):CoverArchive.Folder(store); Directory.CreateDirectory(folder); var target = Path.Combine(folder, Guid.NewGuid().ToString("N") + Path.GetExtension(dialog.FileName)); File.Copy(dialog.FileName, target); set(target); Refresh(); } }

        fields.Controls.Add(Actions(Theme.Button("Arka plan seç", () => Pick(x => settings.CoverBackground = x, "Arka plan"), false, 180), Theme.Button("Kanal logosu seç", () => Pick(x => settings.CoverLogo = x, "Kanal logosu"), false, 200), Theme.Button("Kendi kapağını seç", () => Pick(x => { settings.CoverPng = x; mode.SelectedItem = "Kendi PNG kapak"; }, "Hazır kapak"), false, 200)));

        fields.Controls.Add(Actions(Theme.Button("Vurgu rengi", () => { using var dialog = new ColorDialog(); if (dialog.ShowDialog(this) == DialogResult.OK) { settings.CoverAccent = ColorTranslator.ToHtml(dialog.Color); Refresh(); } }, false, 180), Theme.Button("Görselleri temizle", () => { settings.CoverBackground = settings.CoverLogo = settings.CoverPng = ""; settings.CoverAccent = ""; mode.SelectedItem = "Şablon"; Refresh(); }, false, 210)));

        fields.Controls.Add(Actions(Theme.Button("Önizle", () => { Read(); var previewSettings = JsonSerializerCopy(settings); if (settings.CoverAutoStyle) previewSettings.CoverStyle = CoverDesigner.Suggest(sampleTitle.Text); using var image = CoverDesigner.Draw(previewSettings, sampleTitle.Text); using var dialog = Dialog("Kapak önizlemesi", 1000, 640); dialog.Controls.Add(new PictureBox { Dock = DockStyle.Fill, Image = image, SizeMode = PictureBoxSizeMode.Zoom }); dialog.ShowDialog(this); }, false, 180), Theme.Button("Tasarımı kaydet", () => { if (engine.Busy) { Message("Üretim tamamlanınca kaydet."); return; } Read(); using var validation = CoverDesigner.Draw(settings, sampleTitle.Text); var target = store.Settings; target.CoverAiEnabled = settings.CoverAiEnabled; target.CoverPaidAllowed = settings.CoverPaidAllowed; target.CoverProviderOrder = settings.CoverProviderOrder; target.CoverNvidiaModel = settings.CoverNvidiaModel; target.CoverGeminiModel = settings.CoverGeminiModel; target.CoverOpenAiModel = settings.CoverOpenAiModel; target.CoverMonthlyRequests = settings.CoverMonthlyRequests; target.CoverAutoStyle = settings.CoverAutoStyle; target.ChannelName = settings.ChannelName; target.CoverChannelName = settings.CoverChannelName; target.CoverShowChannelName = settings.CoverShowChannelName; target.CoverStyle = settings.CoverStyle; target.CoverMode = settings.CoverMode; target.CoverBackground = settings.CoverBackground; target.CoverLogo = settings.CoverLogo; target.CoverPng = settings.CoverPng; target.CoverFont = settings.CoverFont; target.CoverLogoPosition = settings.CoverLogoPosition; target.CoverAccent = settings.CoverAccent; store.Save(); Message("Kapak tasarımı kaydedildi. Yeni üretilen videolarda uygulanır. Mevcut çıktıların kapağı değişmez."); }, true, 230)));

        fields.Controls.Add(Theme.Label("Bu örnekler yerel şablonlardır. Sinematik fotoğraf için kendi lisanslı arka planını ekle. Yapay zekâ görselleri üretim sırasında isteğe bağlı servis sırasıyla hazırlanır; bu örnek galerisi yerel tasarımı gösterir. Kendi hazır kapağın seçildiğinde metin ve logo yeniden eklenmez. Normal videoda 16:9, Shorts'ta 9:16 kırpılır; önizlemeyi kontrol et.", 10, color: Theme.Warning, width: page.Width - 30, height: 90)); var advanced=new StudioDisclosure("Gelişmiş kapak tercihleri",page.Width-35);advanced.Content.Controls.Add(fields);coverSection.Controls.Add(advanced); Refresh();

        if(current=="Medya arşivleri"){
            if(mediaKind=="Logo")page.Controls.Add(logoSection);
            else if(mediaKind=="Kart"){page.Controls.Add(cardSection);page.Controls.Add(Theme.Button("Kart arşivi klasörünü aç",()=>{var folder=Path.Combine(store.Root,"card-assets");Directory.CreateDirectory(folder);Theme.Open(folder);},false,300));}
            else page.Controls.Add(coverSection);
        }else PageSections(("Açılış / kapanış kartları", cardSection), ("Kart oluşturma editörü", editorSection), ("Kanal logoları", logoSection), ("Video kapak tasarımı", coverSection));

    }

    static Settings JsonSerializerCopy(Settings settings) => System.Text.Json.JsonSerializer.Deserialize<Settings>(System.Text.Json.JsonSerializer.Serialize(settings))!;

}







