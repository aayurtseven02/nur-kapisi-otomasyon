namespace AnkaStudio;

public sealed partial class StudioForm
{
    internal static string SocialHelp(string platform) => platform switch {
        "Instagram" => "Instagram kurulumu\n\n1. Hesabı İşletme veya İçerik Üreticisi hesabına çevir.\n2. developers.facebook.com/apps üzerinden Meta uygulamasına Instagram API / Instagram Login ürününü ekle. Instagram uygulama kimliği ve sırrı bağlantı sunucusunda saklanır.\n3. instagram_business_basic ve instagram_business_content_publish izinlerini yapılandır; kendi hesabını test kullanıcısı olarak ekle. Başka müşteriler için gereken App Review / Advanced Access sürecini tamamla.\n4. Sunucunun /callback/Instagram adresini izin verilen dönüş adresine ekle.\n5. Anka'da Hesabı Bağla ile tarayıcıda izin ver. Instagram Login yolunda Facebook Sayfası zorunlu değildir.\n\nReels yerel video dosyasından yüklenir. Fotoğraf ve çoklu görsel için herkese erişilebilir HTTPS görsel bağlantıları gerekir. Instagram'ın işleme yanıtı FINISHED olduktan sonra, verdiğin paylaşım onayıyla yayın tamamlanır. Durumu Kontrol Et işlemi kaldığı yerden devam eder.\n\nBelge: https://developers.facebook.com/docs/instagram-platform/instagram-api-with-instagram-login/content-publishing",
        "Facebook" => "Facebook kurulumu\n\n1. Yayın yapacağın Facebook Sayfasında yönetim / içerik oluşturma yetkin olmalı. Kişisel profile otomatik yayın desteklenmez.\n2. developers.facebook.com/apps üzerinden Meta uygulamasına Facebook Login ve Pages API erişimini ekle.\n3. pages_show_list, pages_read_engagement ve pages_manage_posts izinlerini yapılandır. Kendi hesabını uygulama test rolüne ekle; müşterilerin kullanımı için gerekli Meta incelemelerini tamamla.\n4. Sunucunun /callback/Facebook adresini izin verilen dönüş adresine ekle.\n5. Anka'da Hesabı Bağla ile izin ver, yönetilen Sayfalar arasından yayın yapılacak Sayfayı seç.\n\nBu sürüm Facebook Sayfasına normal video, Reels videosu veya tek fotoğraf paylaşır. Video yerel dosyadan, fotoğraf herkese erişilebilir HTTPS bağlantısından gönderilir. Platformun işleme ve yayın durumu ayrıca kontrol edilir.\n\nBelge: https://www.postman.com/meta/facebook/documentation/r56bjfd/facebook-api",
        _ => "TikTok kurulumu\n\n1. developers.tiktok.com üzerinden geliştirici uygulaması oluştur; Login Kit ve Content Posting API / Direct Post ürünlerini ekle.\n2. user.info.basic ve video.publish izinlerini yapılandır. Uygulama kimliği ve sırrı yalnız bağlantı sunucusunda saklanır.\n3. Sunucunun /callback/TikTok adresini izin verilen dönüş adresine ekle. Fotoğrafların bulunduğu alan adı / URL önekini ayrıca doğrula.\n4. Anka'da Hesabı Bağla ile izin ver.\n5. Herkese açık doğrudan yayın için TikTok uygulama incelemesini tamamla. İncelenmemiş uygulamalarda özel görünürlük / test kullanıcı sınırları uygulanır.\n\nHer paylaşımda önizleme, görünürlük seçimi, etkileşim seçenekleri ve müzik kullanım onayı gerekir. Görünürlük varsayılan seçilmez; seçenekler hesabından alınır. Süre sınırı da hesabından alınır. Video yerel dosyadan parçalar halinde aktarılır. Fotoğraf için herkese erişilebilir ve doğrulanmış HTTPS bağlantıları gerekir. TikTok'a ayrıca uygulama filigranı eklenmemelidir.\n\nBelge: https://developers.tiktok.com/docs/en/content-sharing-guidelines"
    };
    void SocialConnectionsDialog()
    {
        using var dialog = Dialog("Instagram / TikTok / Facebook Bağlantıları", 870, 760);
        var flow = Theme.Flow(770); flow.Dock = DockStyle.Fill; flow.AutoScroll = true; dialog.Controls.Add(flow);
        flow.Controls.Add(Theme.Label("Sosyal hesap bağlantıları", 19, true, width: 740, height: 45));
        flow.Controls.Add(Theme.Label("Önce platform geliştirici kayıtları ve bağlantı hizmeti hazırlanır. Her platformun (i) tuşunda adımlar yazılıdır. Hesap şifren Anka'ya girilmez; tarayıcıda izin verirsin.", 10, width: 740, height: 80));
        var broker = Theme.Input(store.Settings.SocialBrokerUrl, width: 740);
        Field(flow, "Sosyal bağlantı hizmeti adresi • kurulum tamamlanınca", broker);
        flow.Controls.Add(Theme.Button("Bağlantı Adresini Kaydet", () => { try { if (broker.Text.Trim() != "") SocialPublisher.Https(broker.Text.Trim()); store.Settings.SocialBrokerUrl = broker.Text.Trim().TrimEnd('/'); store.Save(); Message("Bağlantı adresi kaydedildi. Bu işlem hesap bağlamaz."); } catch (Exception ex) { Message(ex.Message); } }, false, 300));
        var connected = Theme.Label("", 10, width: 740, height: 130);
        void Refresh() => connected.Text = store.Settings.SocialAccounts.Count == 0 ? "Henüz bağlı sosyal hesap yok." : string.Join("\n", store.Settings.SocialAccounts.Select(a => a.ToString()));
        foreach (var name in new[] { "Instagram", "TikTok", "Facebook" }) {
            string platform = name;
            flow.Controls.Add(Actions(Theme.Button(platform + " Hesabını Bağla", () => _ = Safe(async () => { if (engine.Busy) throw new InvalidOperationException("Önce çalışan işlemi bitir."); await engine.External(ct => new SocialConnections(store).Connect(platform, ct)); Refresh(); Message(platform + " hesabı bağlandı."); }), true, 310), Theme.Button("(i)", () => Theme.Alert(dialog, SocialHelp(platform), platform + " Bağlantı Rehberi", icon: MessageBoxIcon.Information), false, 50)));
        }
        Refresh(); flow.Controls.Add(connected);
        var list = Theme.Combo(store.Settings.SocialAccounts.Select(a => a.ToString()).ToArray(), "", 740);
        Field(flow, "Bağlantısı kaldırılacak hesap", list);
        flow.Controls.Add(Theme.Button("Seçili Hesabın Bağlantısını Kaldır", () => { if (engine.Busy || list.SelectedIndex < 0 || list.SelectedIndex >= store.Settings.SocialAccounts.Count) return; new SocialConnections(store).Remove(store.Settings.SocialAccounts[list.SelectedIndex]); list.Items.RemoveAt(list.SelectedIndex); Refresh(); }, false, 350));
        dialog.ShowDialog(this);
    }
    void SocialPublishDialog(Episode episode)
    {
        using var dialog = Dialog("Sosyal Platformlarda Paylaş • " + episode.Topic, 900, 850);
        var flow = Theme.Flow(800); flow.Dock = DockStyle.Fill; flow.AutoScroll = true; dialog.Controls.Add(flow);
        bool socialRunning = false;
        var accounts = store.Settings.SocialAccounts.ToArray();
        var account = Theme.Combo(accounts.Select(a => a.ToString()).ToArray(), "", 770); account.SelectedIndex = -1;
        Field(flow, "Paylaşılacak platform ve hesap", account);
        flow.Controls.Add(Actions(Theme.Button("Hesap Bağlantıları", SocialConnectionsDialog, false, 260), Theme.Button("(i)", () => Theme.Alert(dialog, SocialHelp(account.SelectedIndex >= 0 ? accounts[account.SelectedIndex].Platform : "Instagram"), "Paylaşım Rehberi"), false, 50)));
        flow.Controls.Add(Theme.Label("Hesap yeni bağlandıysa bu pencereyi kapatıp tekrar aç. Videolar için mevcut MP4 dosyan kullanılır; fotoğraf bağlantıları girilirse fotoğraf paylaşımı seçilir.", 10, width: 770, height: 65));
        var videos = episode.Videos.ToArray();
        var video = Theme.Combo(videos.Select(v => (v.Short ? "Shorts • " : "Normal • ") + v.Title).ToArray(), "", 770); video.SelectedIndex = videos.Length > 0 ? 0 : -1;
        Field(flow, "Üretilmiş video", video);
        var photos = Theme.Input("", true, 770); photos.Height = 85; photos.PlaceholderText = "Fotoğraf paylaşımı: her satıra bir HTTPS görsel bağlantısı. Video için boş bırak.";
        Field(flow, "Fotoğraf / çoklu görsel bağlantıları • isteğe bağlı", photos);
        var caption = Theme.Input(episode.Topic, true, 770); caption.Height = 90; Field(flow, "Paylaşım açıklaması • değiştirebilirsin", caption);
        string[] PhotoUrls() => photos.Text.Split(['\r', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        flow.Controls.Add(Theme.Button("Paylaşılacak İçeriği Önizle", () => { try { if (PhotoUrls().Length > 0) foreach (var url in PhotoUrls()) Theme.Open(SocialPublisher.Https(url).AbsoluteUri); else if (video.SelectedIndex >= 0) Theme.Open(videos[video.SelectedIndex].File); else Message("Önizleme için video veya fotoğraf seç."); } catch (Exception ex) { Message(ex.Message); } }, false, 330));
        var privacy = Theme.Combo([], "", 770); privacy.SelectedIndex = -1; Field(flow, "TikTok görünürlüğü • önce Hesap Seçeneklerini Al", privacy);
        var comments = Theme.Check("Yorumlara izin ver", false, 770); var duet = Theme.Check("TikTok Duet'e izin ver", false, 770); var stitch = Theme.Check("TikTok Stitch'e izin ver", false, 770);
        flow.Controls.Add(comments); flow.Controls.Add(duet); flow.Controls.Add(stitch);
        var ai = Theme.Check("İçerik yapay zekâ ile oluşturulmuş / değiştirilmiş", false, 770); flow.Controls.Add(ai);
        var ownBrand = Theme.Check("TikTok: Kendi markamı tanıtıyorum", false, 770); var paidBrand = Theme.Check("TikTok: Ücretli marka iş birliği", false, 770); flow.Controls.Add(ownBrand); flow.Controls.Add(paidBrand);
        var status = Theme.Label("Hesap seç. Hazır olmayan bağlantılar paylaşım başlatmaz.", 10, width: 770, height: 100);
        flow.Controls.Add(Theme.Button("TikTok Hesap Seçeneklerini Al", () => _ = Safe(async () => {
            if (account.SelectedIndex < 0 || accounts[account.SelectedIndex].Platform != "TikTok") throw new InvalidOperationException("TikTok hesabı seç.");
            await engine.External(async ct => { var creator = await new SocialPublisher(store).Creator(accounts[account.SelectedIndex], ct); privacy.Items.Clear(); privacy.Items.AddRange(creator.Privacy); privacy.SelectedIndex = -1; comments.Checked = duet.Checked = stitch.Checked = false; comments.Enabled = !creator.CommentDisabled; duet.Enabled = !creator.DuetDisabled; stitch.Enabled = !creator.StitchDisabled; status.Text = creator.Name + " • video süre sınırı: " + creator.MaxSeconds + " saniye. Görünürlüğü seç."; });
        }), false, 330));
        var consent = Theme.Check("İçeriği önizledim; bu hesaba paylaşılmasını ve müzik kullanım haklarını onaylıyorum", false, 770); flow.Controls.Add(consent);
        flow.Controls.Add(Theme.Label("TikTok müzik kullanım koşulları: https://www.tiktok.com/legal/page/global/music-usage-confirmation/en", 9, width: 770, height: 55));
        account.SelectedIndexChanged += (_, _) => { privacy.Items.Clear(); privacy.SelectedIndex = -1; consent.Checked = comments.Checked = duet.Checked = stitch.Checked = ownBrand.Checked = paidBrand.Checked = false; };
        video.SelectedIndexChanged += (_, _) => consent.Checked = false; photos.TextChanged += (_, _) => consent.Checked = false;
        caption.TextChanged += (_, _) => consent.Checked = false; privacy.SelectedIndexChanged += (_, _) => consent.Checked = false;
        foreach (var option in new[] { comments, duet, stitch, ai, ownBrand, paidBrand }) option.CheckedChanged += (_, _) => consent.Checked = false;
        flow.Controls.Add(Theme.Button("Paylaşım İşlemini İptal Et", () => { if (socialRunning) engine.Stop(); }, false, 330));
        flow.Controls.Add(Theme.Button("Seçili Platformda Paylaş", () => _ = Safe(async () => {
            if (engine.Busy) throw new InvalidOperationException("Çalışan işlem bitmeden paylaşım başlatılamaz.");
            if (account.SelectedIndex < 0) throw new InvalidOperationException("Platform ve hesap seç.");
            var selected = accounts[account.SelectedIndex]; var asset = video.SelectedIndex >= 0 ? videos[video.SelectedIndex] : null;
            if (PhotoUrls().Length == 0 && (asset == null || !episode.Approved || !Reliability.OutputsCurrent(episode))) throw new InvalidOperationException("Videoyu önce İncele / Onayla adımında onayla.");
            var urls = PhotoUrls(); var selectedPrivacy = privacy.Text;
            var inputs = new Control[] { account, video, photos, caption, privacy, comments, duet, stitch, ai, consent, ownBrand, paidBrand };
            var enabled = inputs.Select(c => c.Enabled).ToArray(); foreach (var c in inputs) c.Enabled = false; socialRunning = true;
            try { await engine.External(async ct => { var publisher = new SocialPublisher(store); var record = await publisher.Publish(episode, selected, asset?.File ?? "", asset?.Duration ?? 0, caption.Text, selectedPrivacy, comments.Checked, duet.Checked, stitch.Checked, ai.Checked, consent.Checked, urls, ct, ownBrand.Checked, paidBrand.Checked, asset?.Short ?? true);
                for (int attempt = 0; attempt < 8 && record.State == "PROCESSING"; attempt++) { await Task.Delay(TimeSpan.FromSeconds(3), ct); await publisher.Check(selected, record, ct); }
                status.Text = record.State == "PUBLISHED" ? "Platform yayını doğruladı." : "Gönderim alındı; platform işliyor. Durumu Kontrol Et ile takip et."; }); }
            finally { socialRunning = false; for (int i = 0; i < inputs.Length; i++) inputs[i].Enabled = enabled[i]; }
        }), true, 330));
        flow.Controls.Add(Theme.Button("Paylaşımların Durumunu Kontrol Et", () => _ = Safe(async () => {
            if (engine.Busy) throw new InvalidOperationException("Önce çalışan işlemi bitir.");
            await engine.External(async ct => { foreach (var post in episode.SocialPosts.Where(p => p.State is not ("PUBLISHED" or "FAILED"))) { var a = store.Settings.SocialAccounts.FirstOrDefault(a => a.Platform == post.Platform && a.Id == post.AccountId); if (a != null && post.RemoteId != "") await new SocialPublisher(store).Check(a, post, ct); } });
            status.Text = episode.SocialPosts.Count == 0 ? "Henüz gönderim yok." : string.Join("\n", episode.SocialPosts.TakeLast(8).Select(p => p.Platform + " • " + p.AccountName + " • " + p.State));
        }), false, 350));
        flow.Controls.Add(status); dialog.ShowDialog(this);
    }
}
