using System.Text.Json;

namespace AnkaStudio;



public sealed partial class StudioForm

{

    bool queueRunning;

    CancellationTokenSource? queueCancellation;

    public void CaptureQueue(string directory)

    {

        Directory.CreateDirectory(directory);

        for (int i = 1; i <= 2; i++) store.Episodes.Add(new Episode { Topic = i == 1 ? "Dua Ederken Sabırlı Olmak" : "Küçük Adımların Gücü", IncludeLong = i == 2, ShortCount = i == 1 ? 1 : 0, ShortSeconds = 60, QueueOrder = i, QueueState = "Bekliyor", PublishAt = DateTimeOffset.UtcNow.AddDays(i), QueueSettings = VideoQueue.Snapshot(store.Settings) });

        Navigate("Üretim ve yayın planı"); PerformLayout(); Application.DoEvents();

        using (var bitmap = new Bitmap(Width, Height)) { DrawToBitmap(bitmap, new Rectangle(Point.Empty, Size)); bitmap.Save(Path.Combine(directory, "queue.png")); }

        int countBefore = store.Episodes.Count;

        QueueEditor(null, (dialog, tabs) => {

            try {

                for (int i = 0; i < tabs.TabPages.Count; i++) {

                    tabs.SelectedIndex = i; dialog.PerformLayout(); Application.DoEvents();

                    var save = dialog.Controls.OfType<Button>().Single(b => b.Text.StartsWith("Ekle"));

                    if (!save.Visible || save.Bottom > dialog.ClientSize.Height || save.Bounds.IntersectsWith(tabs.Bounds)) throw new InvalidOperationException("Ekle düğmesi görünür değil.");

                    using var bitmap = new Bitmap(dialog.Width, dialog.Height); dialog.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(directory, "queue-editor-" + i + ".png"));

                }

                tabs.SelectedIndex = 0;

                var flow = tabs.TabPages[0].Controls.OfType<FlowLayoutPanel>().Single();

                flow.Controls.OfType<TextBox>().First().Text = "Tek tek ekleme testi / Shorts";

                dialog.Controls.OfType<Button>().Single(b => b.Text.StartsWith("Ekle")).PerformClick();

                if (store.Episodes.Count != countBefore + 1 || VideoQueue.Items(store).Last().QueueOrder != 3 || VideoQueue.Items(store).Last().ShortCount != 1) throw new InvalidOperationException("İlk video sıraya eklenmedi.");

            } catch (Exception ex) { File.WriteAllText(Path.Combine(directory, "error.txt"), ex.ToString()); Environment.ExitCode = 1; }

            finally { dialog.Close(); }

        });

        if (Environment.ExitCode != 0) return;

        QueueEditor(null, (dialog, tabs) => {

            try {

                var flow = tabs.TabPages[0].Controls.OfType<FlowLayoutPanel>().Single();

                flow.Controls.OfType<TextBox>().First().Text = "Tek tek ekleme testi / Normal";

                flow.Controls.OfType<ComboBox>().First().SelectedItem = "Normal video";

                dialog.Controls.OfType<Button>().Single(b => b.Text.StartsWith("Ekle")).PerformClick();

                var last = VideoQueue.Items(store).Last();

                if (store.Episodes.Count != countBefore + 2 || last.QueueOrder != 4 || !last.IncludeLong || last.ShortCount != 0 || last.LongMinutes != 6) throw new InvalidOperationException("İkinci normal video sıraya eklenmedi.");

            } catch (Exception ex) { File.WriteAllText(Path.Combine(directory, "error.txt"), ex.ToString()); Environment.ExitCode = 1; }

            finally { dialog.Close(); }

        });

        if (Environment.ExitCode != 0) return;

        var testSong = Path.Combine(store.Root, "ui-clip.wav");

        Task.Run(() => Media.Exec(store.Ffmpeg, ["-y", "-f", "lavfi", "-i", "sine=frequency=880:duration=8", testSong], store.Root, CancellationToken.None)).GetAwaiter().GetResult();

        QueueEditor(null, (dialog, tabs) => {

            try {

                var flow = tabs.TabPages[0].Controls.OfType<FlowLayoutPanel>().Single(); flow.Controls.OfType<TextBox>().First().Text = "Klip ekleme testi"; flow.Controls.OfType<ComboBox>().First().SelectedItem = "Müzik klibi";

                var clipPage = tabs.TabPages[1].Controls.OfType<FlowLayoutPanel>().Single(); clipPage.Controls.OfType<TextBox>().First().Text = "Test Parçası";

                var audioPage = tabs.TabPages[2].Controls.OfType<FlowLayoutPanel>().Single(); ((TextBox)audioPage.Controls.Find("QueueMusicFile", false).Single()).Text = testSong;

                var usage = audioPage.Controls.OfType<MusicUsage>().Single(); usage.License.Text = "Müzikler bana aittir"; usage.Rights.Checked = true; usage.Commit();

                tabs.SelectedIndex = 1; dialog.PerformLayout(); Application.DoEvents(); using (var bitmap = new Bitmap(dialog.Width, dialog.Height)) { dialog.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(directory, "music-clip-editor.png")); }

                dialog.Controls.OfType<Button>().Single(b => b.Text.StartsWith("Ekle")).PerformClick(); var last = VideoQueue.Items(store).Last();

                if (store.Episodes.Count != countBefore + 3 || last.MusicClip == null || Math.Abs(last.MusicClip.Duration - 8) > .2 || last.QueueLayout!.SubtitleEnabled || last.QueueMusic!.Duck || last.QueueMusic.Volume != 1) throw new InvalidOperationException("Müzik klibi sıraya doğru eklenmedi.");

            } catch (Exception ex) { File.WriteAllText(Path.Combine(directory, "error.txt"), ex.ToString()); Environment.ExitCode = 1; }

            finally { dialog.Close(); }

        });

    }

    void QueuePage()

    {

        Title("Yayın sırası", "Her satır bir video. Ayrıntıları seç → sıraya ekle → hepsini yayına yolla.");

        var items = VideoQueue.Items(store).ToList();

        page.Controls.Add(Theme.Label($"{items.Count(e => !e.IncludeLong)} Shorts • {items.Count(e => e.IncludeLong)} normal video • Saatler Türkiye saati", 12, true, width: page.Width, height: 38));

        page.Controls.Add(Theme.Label("Videolar şimdi sırayla bu bilgisayarda üretilir, denetlenir ve doğrudan YouTube’a planlı yüklenir. Yüklemeler tamamlanana kadar program açık kalmalı. Sonrasında YouTube tarih ve saatinde yayınlar.", 10, width: page.Width - 30, height: 70));

        page.Controls.Add(Actions(Theme.Button("+ Bir video ayarla ve ekle", () => QueueEditor(null), true, 290), Theme.Button("Hepsini yayına yolla", () => _ = Safe(StartQueue), true, 260), Theme.Button("Sırayı duraklat", PauseQueue, false, 190), Theme.Button("Durumu yenile", () => _ = Safe(RefreshRemote), false, 190)));

        var daily = Theme.Number(store.Settings.DailyProductionLimit, 1, 100, 260);

        page.Controls.Add(Theme.Label("Günlük üretim sınırı • bugün üretilen: " + store.ProducedToday + " • sıra bu sınıra uyar", 10, width: page.Width, height: 38));

        page.Controls.Add(daily);

        page.Controls.Add(Actions(Theme.Button("Günlük sınırı kaydet", () => { if (queueRunning || engine.Busy) { Message("Önce sırayı duraklat."); return; } store.Settings.DailyProductionLimit = (int)daily.Value; store.Save(); }, false, 240)));

        if (items.Count == 0) Empty("Yayın sırası boş", "Bir video ayarla ve Ekle’ye bas. Her yeni video bir sonraki sıraya alınır.");

        foreach (var episode in items) {

            var card = Theme.Card(page.Width, 195); card.Tag = "wide";

            var saved = VideoQueue.Effective(store.Settings, episode.QueueSettings);

            var title = Theme.Label($"{episode.QueueOrder}. {(episode.MusicClip != null ? "MÜZİK KLİBİ" : episode.IncludeLong ? "NORMAL" : "SHORTS")} • {episode.Topic}", 13, true, width: card.Width - 45, height: 36); title.Location = new(18, 12);

            var summary = Theme.Label($"Yayın: {episode.PublishAt?.ToOffset(TimeSpan.FromHours(3)):dd.MM.yyyy HH:mm} • {(episode.IncludeLong ? episode.LongMinutes + " dk" : episode.ShortSeconds + " sn")} • {saved.VoiceProvider}\nMüzik: {(episode.Music ? Path.GetFileName(episode.QueueMusic?.File) : "kapalı")} • Logo: {(saved.VideoLogoEnabled ? "açık" : "kapalı")} • Açılış/kapanış: {(saved.OpeningCardEnabled ? "açık" : "kapalı")} / {(saved.ClosingCardEnabled ? "açık" : "kapalı")}", 10, width: card.Width - 45, height: 57); summary.Location = new(18, 50);

            var status = Theme.Label(episode.QueueState + (episode.QueueAuthorized ? " • %" + episode.Progress : "") + (episode.Error == "" ? "" : " • " + episode.Error), 10, color: episode.Error == "" ? Theme.Teal : Theme.Warning, width: card.Width - 45, height: 42); status.Location = new(18, 108);

            var edit = Theme.Button("Ayrıntıları düzenle", () => QueueEditor(episode), false, 220); edit.Location = new(18, 151);

            var open = Theme.Button("Paketi incele", () => OpenWorkspace(episode), false, 180); open.Location = new(250, 151);

            var remove = Theme.Button("Sıradan çıkar", () => { if (queueRunning || engine.Busy) { Message("Önce sırayı duraklat."); return; } episode.QueueAuthorized = false; episode.QueueOrder = 0; episode.QueueState = ""; episode.Status = "Plan dışı"; int number = 0; foreach (var item in VideoQueue.Items(store).ToList()) item.QueueOrder = ++number; store.Save(); Navigate("Üretim ve yayın planı"); }, false, 180); remove.Location = new(440, 151);

            var schedule = Theme.Button("Tarih / saat ayarla", () => QueueSchedule(episode), false, 200); schedule.Location = new(630, 151);

            card.Controls.AddRange([title, summary, status, edit, open, remove, schedule]); page.Controls.Add(card);

        }

    }

    void QueueSchedule(Episode episode)

    {

        if (queueRunning || engine.Busy) { Message("Önce sırayı duraklat."); return; }

        if (!string.IsNullOrEmpty(episode.RemoteUploadJob) || episode.Videos.Any(v => v.YouTubeId != "")) { Navigate("Yayın takvimi"); return; }

        using var dialog = Dialog("YouTube yayın tarihi ve saati", 620, 430);

        var flow = Theme.Flow(570); flow.Padding = new Padding(18); dialog.Controls.Add(flow);

        var initial = episode.PublishAt?.ToOffset(TimeSpan.FromHours(3)).DateTime ?? DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(3)).AddDays(1).DateTime;

        var date = new DateTimePicker { Width = 510, Format = DateTimePickerFormat.Custom, CustomFormat = "dd.MM.yyyy", Value = initial };

        var time = new DateTimePicker { Width = 510, Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, Value = initial };

        var defer=Theme.Check("Üretim uzarsa yayını en az 30 dakika ileri ertele",episode.DeferMissedPublication,510);flow.Controls.Add(defer);

        Field(flow, "Yayın tarihi • Türkiye", date); Field(flow, "Yayın saati • saat ve dakikayı seç", time);

        flow.Controls.Add(Theme.Button("Yayın tarihini ve saatini kaydet", () => {

            try {

                var selected = new DateTimeOffset(DateTime.SpecifyKind(date.Value.Date.AddHours(time.Value.Hour).AddMinutes(time.Value.Minute), DateTimeKind.Unspecified), TimeSpan.FromHours(3));

                if (selected < DateTimeOffset.UtcNow.AddMinutes(10)) throw new InvalidOperationException("Yayın zamanı en az 10 dakika ileride olmalı.");

                episode.DeferMissedPublication=defer.Checked; episode.PublishAt = selected; store.Save(); dialog.Close(); Navigate("Üretim ve yayın planı");

            } catch (Exception ex) { Message(ex.Message); }

        }, true, 510));

        dialog.ShowDialog(this);

    }

    void PauseQueue()

    {

        foreach (var episode in VideoQueue.Items(store)) episode.QueueAuthorized = false;

        queueCancellation?.Cancel(); engine.Stop(); store.Save();

        Message("Sıra duraklatıldı. Tamamlanan işler ve yükleme ilerlemesi korunur; devam ettirebilirsin.");

    }

    async Task StartQueue()

    {

        if (queueRunning || engine.Busy || autoRunning) throw new InvalidOperationException("Önce çalışan işlemi bitir veya duraklat.");

        var pending = VideoQueue.Items(store).Where(e => e.QueueState != "Planlandı").ToList();

        if (pending.Count == 0) throw new InvalidOperationException("İşlenecek video yok. Yeni video ekle.");

        VideoQueue.RequireYouTube(store);

        foreach (var episode in pending) { VideoQueue.DeferExpired(episode,DateTimeOffset.UtcNow); VideoQueue.Validate(episode, store.Settings); }

        foreach (var episode in pending) { episode.QueueAuthorized = true; episode.QueueState = "Bekliyor"; }

        store.Save(); await RunQueue();

    }

    async Task RunQueue()

    {

        if (queueRunning || engine.Busy) return;

        queueRunning = true; queueCancellation = new(); var ct = queueCancellation.Token;

        Episode? active = null;

        try {

            foreach (var episode in VideoQueue.Items(store).Where(e => e.QueueAuthorized && e.QueueState != "Planlandı").ToList()) {

                active = episode; ct.ThrowIfCancellationRequested(); VideoQueue.DeferExpired(episode,DateTimeOffset.UtcNow); VideoQueue.Validate(episode, store.Settings); episode.Error = "";

                {

                    if (episode.Plan == null || !Reliability.OutputsCurrent(episode)) {

                        episode.QueueState = "Senaryo hazırlanıyor"; store.Save();

                        if (store.ProducedToday >= store.Settings.DailyProductionLimit) throw new InvalidOperationException("Günlük üretim sınırına ulaşıldı. Plan ekranından sınırı güncelle veya ertesi gün devam ettir.");

                        if (episode.Plan == null || episode.ResumeStage is "Plan" or "Proofread") await engine.Plan(episode, resume: true);

                        if (episode.Plan == null || episode.Error != "") throw new InvalidOperationException(episode.Error == "" ? "Senaryo tamamlanamadı." : episode.Error);

                        ct.ThrowIfCancellationRequested(); VideoQueue.PrepareDesign(episode, store.Settings);

                        episode.QueueState = "Video üretiliyor"; store.Save(); await engine.Produce(episode);

                        if (!Reliability.OutputsCurrent(episode) || episode.Error != "") throw new InvalidOperationException(episode.Error == "" ? "Video denetimi tamamlanamadı." : episode.Error);

                    }

                    ct.ThrowIfCancellationRequested();

                    if(episode.PublishAt<=DateTimeOffset.UtcNow.AddMinutes(10)) {

                        if(!episode.DeferMissedPublication)throw new InvalidOperationException("Üretim sırasında yayın zamanı geçti. Tarih / saat ayarla ile ileri tarih seç; üretilmiş videolar korunuyor.");

                        VideoQueue.DeferExpired(episode,DateTimeOffset.UtcNow);

                        episode.Log("Önceden seçilmiş erteleme kuralıyla yayın 30 dakika ileri alındı.");store.Save();

                    }

                    episode.QueueState = "Kalite ve kaynak denetimi"; engine.Approve(episode, planned: true);

                }

                ct.ThrowIfCancellationRequested(); episode.QueueState = "Doğrudan YouTube’a yükleniyor"; store.Save();

                await engine.Upload(episode, true);

                ct.ThrowIfCancellationRequested();

                if (episode.Error != "") throw new InvalidOperationException(episode.Error);

                if (episode.Status == "Durduruldu") throw new OperationCanceledException(ct);

                await engine.External(token => new YouTube(store).RefreshVideoStates(token));

                if (episode.Videos.Any(v => v.YouTubeId == "" || v.YouTubeState != "Yayında" && (!v.Scheduled || v.RemotePublishAt != v.PublishAt))) throw new InvalidOperationException("YouTube yüklemesi tamamlandı fakat istenen yayın takvimi doğrulanmadı. Paketin yayın ayarlarını kontrol et.");

                episode.QueueState = "Planlandı"; episode.QueueAuthorized = false; episode.Error = ""; store.Save();

            }

            if (active != null) Message("Sıradaki videolar YouTube’a yüklendi ve yayın tarihleri doğrulandı.");

        }

        catch (OperationCanceledException) { if (active != null) active.QueueState = "Duraklatıldı"; }

        catch (Exception ex) { if (active != null) { active.QueueState = "Kontrol gerekli"; active.Error = ex.Message; active.Log(ex.Message); } Message("Yayın sırası durdu. Tamamlanan işler korundu.\n" + ex.Message); }

        finally {

            foreach (var episode in VideoQueue.Items(store)) episode.QueueAuthorized = false;

            store.Save(); queueCancellation.Dispose(); queueCancellation = null; queueRunning = false;

            if (!IsDisposed && current == "Üretim ve yayın planı") Navigate(current);

        }

    }

    void QueueEditor(Episode? original, Action<Form, TabControl>? preview = null, string? initialKind=null)

    {

        if (queueRunning || engine.Busy) { Message("Önce çalışan sırayı duraklat."); return; }

        if (original?.RemoteUploadJob != "" && original?.RemoteUploadJob != null || original?.Videos.Any(v => v.YouTubeId != "") == true) { Message("Yüklenmiş video ayarları korunur. Yeni video olarak ekle; yayın tarihini Yayın takvimi’nden yönet."); return; }

        var episode = original == null ? new Episode { IncludeLong = false, ShortCount = 1, ShortSeconds = 60, ShortMode = "Bağımsız Shorts" } : JsonSerializer.Deserialize<Episode>(JsonSerializer.Serialize(original, Json.Options), Json.Options)!;

        if(original==null && initialKind=="Müzik klibi") {episode.IncludeLong=true;episode.ShortCount=0;episode.MusicClip=new MusicClipOptions();}
        if(original==null && initialKind=="Fotoğraflardan video") {episode.IncludeLong=true;episode.ShortCount=0;episode.PhotosOnly=true;}
        var settings = VideoQueue.Effective(store.Settings, original?.QueueSettings); settings.ShortVoiceOverride = false;

        using var dialog = Dialog(original == null ? "Bir video ayarla ve sıraya ekle" : "Sıradaki videoyu düzenle", 950, 850);

        var tabs = new StudioTabs { Dock = DockStyle.Fill, Font = Theme.Font(11) }; dialog.Controls.Add(tabs);

        FlowLayoutPanel Tab(string name) { var tab = new TabPage(name) { BackColor = Theme.Bg, AutoScroll = true }; var flow = Theme.Flow(820); flow.Padding = new Padding(18); tab.Controls.Add(flow); tabs.TabPages.Add(tab); return flow; }

        var content = Tab("Konu / yayın"); var musicPage = Tab("Müzik klibi"); var voice = Tab("Ses / müzik"); var art = Tab("Kapak / kart / logo"); var detail = Tab("Altyazı / ayrıntılar");

        var initialPublish = episode.PublishAt?.ToOffset(TimeSpan.FromHours(3)).DateTime ?? DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(3)).AddDays(1).DateTime;

        var date = new DateTimePicker { Width = 760, Format = DateTimePickerFormat.Custom, CustomFormat = "dd.MM.yyyy", Value = initialPublish }; Field(content, "YouTube yayın tarihi • Türkiye", date);

        var time = new DateTimePicker { Width = 760, Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, Value = initialPublish }; Field(content, "YouTube yayın saati • saat ve dakikayı seç", time);

        var topic = Theme.Input(episode.Topic, width: 760); Field(content, "Video konusu / başlığı", topic);

        var brief = Theme.Input(episode.Brief, true, 760); brief.Height = 125; Field(content, "İçerik talimatı • anlatılacaklar / kaçınılacaklar", brief);

        var kind = Theme.Combo(["Shorts", "Normal video", "Müzik klibi"], episode.MusicClip != null ? "Müzik klibi" : episode.IncludeLong ? "Normal video" : "Shorts", 760); Field(content, "Bir satırda tek video türü", kind);
        var photosOnly=Theme.Check("Fotoğraflardan video oluştur • Pexels / Pixabay fotoğrafları",episode.PhotosOnly);content.Controls.Add(photosOnly);
        var story=Theme.Check("Hikâyeyi AI görselleriyle canlandır • aynı karakter referansı",episode.AiStoryImages);content.Controls.Add(story);
        story.CheckedChanged+=(_,_)=>{if(story.Checked)photosOnly.Checked=false;};
        photosOnly.CheckedChanged+=(_,_)=>{if(photosOnly.Checked)story.Checked=false;};
        var storyLocal=Theme.Check("Yerel motoru kullan • bulut görsel API çağrısı yapılmaz",episode.AiStoryLocal);content.Controls.Add(storyLocal);
        var storyStyle=Theme.Input(episode.AiStoryStyle,width:760);Field(content,"Hikâye görsel tarzı",storyStyle);
        var characters=Theme.Input(episode.AiStoryCharacters,true,760);Field(content,"Sabit karakterler • ad, yüz, yaş, saç/sakal, kıyafet ve renkler",characters);
        var storyMotion=Theme.Check("Görselleri hareketlendir",episode.AiStoryMotion);content.Controls.Add(storyMotion);
        var storyCinema=Theme.Check("Sinematik animasyon • sahneye göre kamera ve atmosfer",episode.AiStoryCinematic);content.Controls.Add(storyCinema);
        var storyAutonomous=Theme.Check("Durmadan üret • görselleri bitince videoda incele",episode.AiStoryAutonomous);content.Controls.Add(storyAutonomous);
        var storyHelp=Theme.Label("Karakter referansı korunur; hazır sahneler tekrar kullanılır. Sinematik hareket fotoğrafı canlandırır. Durmadan üret seçilirse görselleri son videoda inceleyip onaylarsın.",9,width:760,height:65);content.Controls.Add(storyHelp);
        var storyGroup=CompactUi.Group(content,"Hikâye görselleri ve animasyon",storyLocal,storyHelp,episode.AiStoryImages);
        void StoryOptions(){storyGroup.Enabled=story.Checked;if(story.Checked)storyGroup.SetExpanded(true);storyCinema.Enabled=storyMotion.Checked;}
        story.CheckedChanged+=(_,_)=>StoryOptions();storyMotion.CheckedChanged+=(_,_)=>StoryOptions();StoryOptions();
        content.Controls.Add(Theme.Label("Fotoğraflar sahne konusuna göre indirilir. Kaynak / fotoğrafçı bilgisi tutulur. Pexels: https://www.pexels.com • Pixabay: https://pixabay.com",9,width:760,height:54));

        var own = episode.UserNarration ?? new UserNarrationOptions();

        var useOwn = Theme.Check("Hazır metnimi kullan • yapay zekâ içerik yazmasın", own.Enabled); content.Controls.Add(useOwn);

        var ownEditor = Theme.Button("Hazır metin editörünü aç", () => { if (kind.Text == "Müzik klibi") { Message("Müzik klibinde anlatım kullanılmaz."); return; } using var editor = new UserNarrationEditor(own, kind.Text == "Normal video", kind.Text == "Shorts" ? 1 : 0); if (editor.ShowDialog(dialog) == DialogResult.OK) { own = editor.Value; useOwn.Checked = true; } }, false, 300); content.Controls.Add(ownEditor);

        var duration = Theme.Number(episode.IncludeLong ? episode.LongMinutes : episode.ShortSeconds, 1, 1800, 760); Field(content, "Süre • Shorts: saniye / normal video: dakika", duration);

        kind.SelectedIndexChanged += (_, _) => { useOwn.Enabled = kind.Text != "Müzik klibi"; ownEditor.Enabled = useOwn.Enabled; if (!useOwn.Enabled) useOwn.Checked = false; duration.Value = kind.Text == "Shorts" ? 60 : 6; duration.Enabled = kind.Text != "Müzik klibi"; };

        duration.Enabled = episode.MusicClip == null; useOwn.Enabled = ownEditor.Enabled = episode.MusicClip == null;

        var style = Theme.Combo(["Belgesel", "Hikâye anlatımı", "Eğitim", "Bilgilendirici", "Motivasyon"], episode.Style, 760); Field(content, "Anlatım tarzı", style);



        var kids = Theme.Check("Çocuklara özel hazırlanmış içerik", episode.MadeForKids); content.Controls.Add(kids);

        var synthetic = Theme.Check("Gerçekçi değiştirilmiş / sentetik içerik bildirimi", episode.DiscloseSynthetic); content.Controls.Add(synthetic);

        var clipOptions = episode.MusicClip ?? new MusicClipOptions();

        musicPage.Controls.Add(Theme.Label("Konu / yayın bölümünden Müzik klibi seç. Parça ana ses olur. Altyazı ve seslendirme isteğe bağlıdır; başlangıçta kapalıdır. Süre dosyadan otomatik okunur. Dosyayı Ses / müzik sekmesinden seç.", 11, width: 760, height: 90));

        var clipTitle = Theme.Input(clipOptions.TrackTitle, width: 760); Field(musicPage, "Parça adı", clipTitle);

        var clipArtist = Theme.Input(clipOptions.Artist, width: 760); Field(musicPage, "Sanatçı / yorumcu", clipArtist);

        var clipLyrics = Theme.Input(clipOptions.LyricsBy, width: 760); Field(musicPage, "Söz yazarı • enstrümantalse boş bırak", clipLyrics);

        var clipComposer = Theme.Input(clipOptions.MusicBy, width: 760); Field(musicPage, "Müzik / besteci", clipComposer);

        var clipTheme = Theme.Input(clipOptions.VisualTheme, true, 760); clipTheme.Height = 90; Field(musicPage, "Stok görüntü temaları • noktalı virgülle ayır", clipTheme);

        var clipThemePreset = Theme.Combo(["Özel tema", "Doğa / deniz", "Şehir / gece", "Uzay / yıldızlar", "Soyut / ışık"], "Özel tema", 760); Field(musicPage, "Hazır görüntü teması", clipThemePreset);

        clipThemePreset.SelectedIndexChanged += (_, _) => { var preset = clipThemePreset.Text; if (preset != "Özel tema") clipTheme.Text = preset switch { "Doğa / deniz" => "doğa manzarası; sakin deniz; gün batımı; orman", "Şehir / gece" => "city night; urban lights; city street", "Uzay / yıldızlar" => "uzay; yıldızlı gece; galaksi", _ => "soyut fon; ışık parçacıkları; renk geçişi" }; };

        var clipNarration=Theme.Check("Seslendirme ekle • aşağıdaki metni oku",clipOptions.NarrationEnabled);musicPage.Controls.Add(clipNarration);
        var clipSubtitles=Theme.Check("Altyazı ekle",clipOptions.SubtitlesEnabled);musicPage.Controls.Add(clipSubtitles);
        var clipText=Theme.Input(clipOptions.Text,true,760);clipText.Height=160;Field(musicPage,"Altyazı / seslendirme metni • her satır ayrı sahne",clipText);
        musicPage.Controls.Add(Theme.Label("Seslendirme açıkken altyazı konuşmaya eşlenir. Yalnız altyazı seçilirse satırlar müzik süresine eşit bölünür; şarkı sözü otomatik tanınmaz. Seslendirme müzikten uzun olursa üretim durur, ses hızlandırılmaz.",10,width:760,height:90));
        var clipCredits = Theme.Check("Parça bilgilerini videonun alt bölümünde göster", clipOptions.ShowCredits); musicPage.Controls.Add(clipCredits);

        var provider = Theme.Combo(VoiceProviders.Choices(settings.VoiceProvider), settings.VoiceProvider, 760); Field(voice, "Ses sağlayıcısı", provider);

        var edgeVoice=Theme.Combo(EdgeTts.TurkishVoices.Select(x=>x.Id).ToArray(),settings.EdgeTtsVoice,760);Field(voice,"Edge Türkçe sesi",edgeVoice);
        var speed = Theme.Number((decimal)settings.VoiceSpeed, .5m, 2, 760); speed.DecimalPlaces = 2; speed.Increment = .05m; Field(voice, "Konuşma hızı", speed);

        var google = Theme.Combo(new[] { settings.GoogleTtsVoice, "tr-TR-Standard-A", "tr-TR-Standard-B", "tr-TR-Wavenet-A", "tr-TR-Wavenet-B", "tr-TR-Wavenet-C", "tr-TR-Wavenet-D", "tr-TR-Wavenet-E" }.Distinct().ToArray(), settings.GoogleTtsVoice, 760); Field(voice, "Google TTS sesi", google);

        var windows = LocalVoice.List(); var local = Theme.Combo(windows.Count == 0 ? ["Türkçe ses kurulu değil"] : windows.Select(v => v.Name).ToArray(), windows.FirstOrDefault(v => v.Id == settings.WindowsVoice).Name ?? windows.FirstOrDefault().Name ?? "Türkçe ses kurulu değil", 760); Field(voice, "Windows Türkçe sesi", local);

        voice.Controls.Add(Theme.Button("Google TTS ses listesini yenile", () => _ = Safe(async () => { using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45)); var names = await new GoogleTts(store).Voices(timeout.Token); var previous = google.Text; google.Items.Clear(); google.Items.AddRange(names); if (names.Contains(previous)) google.SelectedItem = previous; else if (names.Length > 0) google.SelectedIndex = 0; }), false, 320));

        var elevenVoice = Theme.Input(settings.VoiceId, width: 760); Field(voice, "ElevenLabs ses kimliği • yalnız bu sağlayıcı için", elevenVoice);

        var xttsUrl = Theme.Input(settings.XttsUrl, width: 760); Field(voice, "XTTS v2 sunucu adresi", xttsUrl);
        var xttsSpeaker = Theme.Input(settings.XttsSpeaker, width: 760); Field(voice, "XTTS konuşmacı adı / sunucudaki WAV yolu", xttsSpeaker);
        voice.Controls.Add(Theme.Button("XTTS v2 Kurulum Bilgisi (i)", () => Theme.Alert(dialog, XttsTts.Help, "Harici XTTS v2"), false, 320));
        var piperPath = Theme.Input(settings.PiperPath, width: 760); Field(voice, "Piper programı • yalnız Piper için", piperPath);

        var piperModel = Theme.Input(settings.PiperModel, width: 760); Field(voice, "Piper Türkçe ONNX modeli", piperModel);

        var music = Theme.Check("Bu videoda fon müziği kullan", episode.Music); voice.Controls.Add(music);

        var track = original?.QueueMusic ?? settings.LibraryMusic ?? new MusicTrack { File = settings.MusicPath, Volume = settings.MusicVolume };

        var song = Theme.Input(track.File, width: 760); Field(voice, "Müzik dosyası", song);

        var library = new AudioLibrary(store).List().Where(x => x.Kind == "Müzik" && File.Exists(x.File)).ToList();

        var libraryCombo = Theme.Combo(new[] { "Arşivden seç…" }.Concat(library.Select(x => x.Name + " [" + x.Id[..6] + "]")).ToArray(), "Arşivden seç…", 760); Field(voice, "Kayıtlı müzik arşivi", libraryCombo);

        var usage = new MusicUsage(track.Source, track.License, track.Attribution, track.RightsConfirmed, 760); voice.Controls.Add(usage);

        var source = usage.Source; var license = usage.License; var attribution = usage.Attribution; var rights = usage.Rights;

        libraryCombo.SelectedIndexChanged += (_, _) => { int i = libraryCombo.SelectedIndex - 1; if (i >= 0) { var item = library[i]; song.Text = item.File; usage.ApplySaved(item.Source, item.License, item.Attribution, item.RightsConfirmed); music.Checked = true; } };

        void Browse(FlowLayoutPanel flow, TextBox input, string title, string filter) { flow.Controls.Add(Theme.Button(title, () => { using var picker = new OpenFileDialog { Filter = filter, CheckFileExists = true }; if (picker.ShowDialog(dialog) == DialogResult.OK) input.Text = picker.FileName; }, false, 250)); }

        voice.Controls.Add(Theme.Button("ⓘ Piper Modeli Nedir?", () => Theme.Alert(dialog, ButtonHelp.Explain("Piper modeli seç"), "Piper Modeli"), false, 290));
        Browse(voice, piperPath, "Piper programı seç", "Program|*.exe"); Browse(voice, piperModel, "Piper modeli seç", "Ses modeli|*.onnx");

        Browse(voice, song, "Müzik dosyası seç", "Ses dosyaları|*.mp3;*.wav;*.flac;*.m4a;*.ogg;*.aac");

        var volume = Theme.Number((decimal)(track.Volume * 100), 1, 100, 760); Field(voice, "Müzik ses seviyesi / yüzde", volume);

        var duck = Theme.Check("Konuşmada müziği dengeli kıs", track.Duck); voice.Controls.Add(duck);

        var fadeIn = Theme.Number((decimal)track.FadeIn, 0, 30, 760); fadeIn.DecimalPlaces = 1; Field(voice, "Müzik giriş geçişi / saniye", fadeIn);

        var fadeOut = Theme.Number((decimal)track.FadeOut, 0, 30, 760); fadeOut.DecimalPlaces = 1; Field(voice, "Müzik çıkış geçişi / saniye", fadeOut);

        var start = Theme.Number((decimal)track.Start, 0, 1800, 760); Field(voice, "Videoda müzik başlangıcı / saniye", start);

        var end = Theme.Number((decimal)track.End, 0, 1800, 760); Field(voice, "Videoda müzik bitişi / saniye • 0: sonuna kadar", end);

        var seek = Theme.Number((decimal)track.SourceOffset, 0, 86400, 760); Field(voice, "Müzik dosyasında başlangıç / saniye", seek);

        var coverMode = Theme.Combo(["Şablon", "Kendi PNG kapak"], settings.CoverMode, 760); Field(art, "YouTube küçük resmi • videonun içine eklenmez", coverMode);

        var coverStyle = Theme.Combo(["Minimal", "Sinematik", "Belgesel", "Manevi", "Nur Kapısı • Altın sinema", "Eğitim", "Teknoloji", "Haber", "Eğlence"], settings.CoverStyle, 760); Field(art, "Küçük resim tasarım stili", coverStyle);

        var aiCover = Theme.Check("Küçük resmi AI ile üret • kayıtlı API ücret izinlerine uyar", settings.CoverAiEnabled); art.Controls.Add(aiCover);

        var autoCover = Theme.Check("Konuya göre küçük resim stili seç", settings.CoverAutoStyle); art.Controls.Add(autoCover);

        TextBox ImageField(string label, string value) { var input = Theme.Input(value, width: 760); Field(art, label, input); Browse(art, input, label + " seç", "Görsel|*.png;*.jpg;*.jpeg;*.webp;*.bmp"); return input; }

        var cover = ImageField("Hazır küçük resim", settings.CoverPng);

        var background = ImageField("Şablon arka planı • isteğe bağlı", settings.CoverBackground);

        var openingEnabled = Theme.Check("Videonun başına açılış kartı ekle", settings.OpeningCardEnabled); art.Controls.Add(openingEnabled); var opening = ImageField("Açılış kartı", settings.OpeningCardFile); var openingSeconds = Theme.Number((decimal)settings.OpeningCardSeconds, 1, 15, 760); Field(art, "Açılış süresi / saniye", openingSeconds);

        var closingEnabled = Theme.Check("Videonun sonuna kapanış kartı ekle", settings.ClosingCardEnabled); art.Controls.Add(closingEnabled); var closing = ImageField("Kapanış kartı", settings.ClosingCardFile); var closingSeconds = Theme.Number((decimal)settings.ClosingCardSeconds, 1, 15, 760); Field(art, "Kapanış süresi / saniye", closingSeconds);

        var logoEnabled = Theme.Check("Video üzerinde kanal logosu göster", settings.VideoLogoEnabled); art.Controls.Add(logoEnabled); var logo = ImageField("Video logosu", settings.VideoLogo); var position = Theme.Combo(["Sol üst", "Sağ üst", "Sol alt", "Sağ alt"], settings.VideoLogoPosition, 760); Field(art, "Logo konumu", position); var logoSize = Theme.Number(settings.VideoLogoWidthPercent, 5, 20, 760); Field(art, "Logo genişliği / video genişliğinin yüzdesi", logoSize);

        var coverLogo = ImageField("Küçük resim logosu • isteğe bağlı", settings.CoverLogo);

        var layout = original?.QueueLayout ?? TextLayout.Defaults(settings, !episode.IncludeLong);

        var titleEnabled = Theme.Check("Sahne başlıklarını göster", layout.TitleEnabled); detail.Controls.Add(titleEnabled);

        var autoTitles = Theme.Check("Sahne başlıklarını yapay zekâ ayarlasın", layout.AutoSceneTitles); detail.Controls.Add(autoTitles);

        autoTitles.CheckedChanged += (_, _) => { if (autoTitles.Checked) titleEnabled.Checked = true; };

        titleEnabled.CheckedChanged += (_, _) => { if (!titleEnabled.Checked) autoTitles.Checked = false; };

        var subtitles = Theme.Check("Altyazıları göster", layout.SubtitleEnabled); detail.Controls.Add(subtitles);

        var textFont = Theme.Combo(["Segoe UI", "Arial", "Tahoma", "Verdana", "Times New Roman"], layout.Font, 760); Field(detail, "Yazı tipi", textFont);

        var titleSize = Theme.Number(layout.TitleSize, 20, 180, 760); Field(detail, "Başlık yazı boyutu", titleSize);

        var subtitleY = Theme.Number((decimal)layout.SubtitleY, 5, 95, 760); Field(detail, "Altyazı dikey konumu / yüzde", subtitleY);

        var titleY = Theme.Number((decimal)layout.TitleY, 5, 95, 760); Field(detail, "Başlık dikey konumu / yüzde", titleY);

        var effect = Theme.Combo(TextLayout.Effects, layout.Effect, 760); Field(detail, "Yazı efekti", effect);

        var animation = Theme.Combo(TextLayout.Animations, layout.SubtitleAnimation, 760); Field(detail, "Altyazı hareketi", animation);

        var transition = Theme.Combo(TextLayout.Transitions, layout.Transition, 760); Field(detail, "Sahne geçişi", transition);

        var font = Theme.Number(layout.SubtitleSize, 20, 180, 760); Field(detail, "Altyazı yazı boyutu", font);

        var color = Theme.Input(settings.SubtitleColor, width: 760); Field(detail, "Altyazı rengi • örnek FFFFFF", color);

        var encoding = Theme.Combo(["veryfast", "fast", "medium", "slow"], settings.EncodingPreset, 760); Field(detail, "Video kodlama hızı", encoding);

        var quality = Theme.Number(settings.Crf, 16, 30, 760); Field(detail, "Görüntü sıkıştırması • düşük değer daha net", quality);

        detail.Controls.Add(Theme.Label("Araştırma ve yapay zekâ Yönetim ayarlarında seçili sağlayıcılardan çalışır. Ücret izinleri korunur. Kaynak, anlatım süresi, medya dosyası ve kalite denetiminde sorun çıkarsa sıra durur; eksik video yayınlanmaz.", 10, width: 760, height: 85));

        bool enteredClip = original?.MusicClip != null;

        song.Name = "QueueMusicFile"; source.Name = "QueueMusicSource"; license.Name = "QueueMusicLicense"; rights.Name = "QueueMusicRights";

        var ttsControls = voice.Controls.Cast<Control>().TakeWhile(x => x != music).ToList();

        void ModeChanged() {

            bool isClip = kind.Text == "Müzik klibi";

            foreach (var control in ttsControls) control.Enabled = !isClip || clipNarration.Checked;

            foreach (var control in new Control[] { titleEnabled, duck, start, end, seek, duration }) control.Enabled = !isClip;

            duration.Enabled = !isClip && !useOwn.Checked;

            autoTitles.Enabled = !isClip; if (isClip) autoTitles.Checked = false;

            music.Enabled = !isClip;

            if (isClip) { music.Checked = true; if (!enteredClip) { volume.Value = 100; fadeIn.Value = 0; fadeOut.Value = 0; transition.SelectedItem = "Yumuşak karışım"; enteredClip = true; } duck.Checked = false; subtitles.Checked = clipSubtitles.Checked; subtitles.Enabled=false; titleEnabled.Checked = false; }

        }

        useOwn.CheckedChanged += (_, _) => { duration.Enabled = !useOwn.Checked && kind.Text != "Müzik klibi"; };

        clipNarration.CheckedChanged+=(_,_)=>ModeChanged();clipSubtitles.CheckedChanged+=(_,_)=>ModeChanged();
        kind.SelectedIndexChanged += (_, _) => ModeChanged(); ModeChanged();

        void Save() {

            try {

                if (queueRunning || engine.Busy) throw new InvalidOperationException("Önce çalışan işlemi duraklat.");

                own.Enabled = useOwn.Checked; episode.UserNarration = own;

                episode.AiStoryImages=story.Checked; episode.AiStoryLocal=storyLocal.Checked; episode.AiStoryStyle=storyStyle.Text.Trim(); episode.AiStoryCharacters=characters.Text.Trim(); episode.AiStoryMotion=storyMotion.Checked; episode.AiStoryCinematic=storyCinema.Checked; episode.AiStoryAutonomous=storyAutonomous.Checked; episode.PhotosOnly=photosOnly.Checked; episode.Topic = topic.Text.Trim(); episode.Brief = brief.Text.Trim(); episode.Style = style.Text; episode.IncludeLong = kind.Text != "Shorts"; episode.ShortCount = episode.IncludeLong ? 0 : 1; episode.LongMinutes = episode.IncludeLong ? (int)duration.Value : 6; episode.ShortSeconds = episode.IncludeLong ? 60 : (int)duration.Value;

                episode.PublishAt = new DateTimeOffset(DateTime.SpecifyKind(date.Value.Date.AddHours(time.Value.Hour).AddMinutes(time.Value.Minute), DateTimeKind.Unspecified), TimeSpan.FromHours(3)); episode.MadeForKids = kids.Checked; episode.DiscloseSynthetic = synthetic.Checked; episode.Music = music.Checked;

                settings.XttsUrl = xttsUrl.Text.Trim(); settings.XttsSpeaker = xttsSpeaker.Text.Trim(); settings.VoiceId = elevenVoice.Text.Trim(); settings.PiperPath = piperPath.Text.Trim(); settings.PiperModel = piperModel.Text.Trim(); settings.VoiceProvider = provider.Text; settings.EdgeTtsVoice=edgeVoice.Text; settings.VoiceSpeed = (double)speed.Value; settings.GoogleTtsVoice = google.Text; settings.WindowsVoice = windows.FirstOrDefault(v => v.Name == local.Text).Id ?? "";

                settings.CoverAiEnabled = aiCover.Checked; settings.CoverMode = coverMode.Text; settings.CoverStyle = coverStyle.Text; settings.CoverAutoStyle = autoCover.Checked; settings.CoverPng = cover.Text; settings.CoverBackground = background.Text; settings.CoverLogo = coverLogo.Text;

                settings.OpeningCardEnabled = openingEnabled.Checked; settings.OpeningCardFile = opening.Text; settings.OpeningCardSeconds = (double)openingSeconds.Value; settings.ClosingCardEnabled = closingEnabled.Checked; settings.ClosingCardFile = closing.Text; settings.ClosingCardSeconds = (double)closingSeconds.Value;

                settings.VideoLogoEnabled = logoEnabled.Checked; settings.VideoLogo = logo.Text; settings.VideoLogoPosition = position.Text; settings.VideoLogoWidthPercent = (int)logoSize.Value;

                settings.ShortFontSize = settings.LongFontSize = (int)font.Value; settings.SubtitleColor = color.Text.Trim().TrimStart('#'); if (!System.Text.RegularExpressions.Regex.IsMatch(settings.SubtitleColor, "^[a-fA-F0-9]{6}$")) throw new InvalidOperationException("Altyazı rengi 6 haneli renk kodu olmalı."); settings.EncodingPreset = encoding.Text; settings.Crf = (int)quality.Value;

                usage.Commit();

                episode.QueueMusic = new MusicTrack { Enabled = music.Checked, File = song.Text.Trim(), Volume = (double)volume.Value / 100, Duck = duck.Checked, FadeIn = (double)fadeIn.Value, FadeOut = (double)fadeOut.Value, Start = (double)start.Value, End = (double)end.Value, SourceOffset = (double)seek.Value, Source = source.Text.Trim(), License = license.Text.Trim(), Attribution = attribution.Text.Trim(), RightsConfirmed = rights.Checked };

                layout.AutoSceneTitles = autoTitles.Checked; layout.TitleEnabled = titleEnabled.Checked; layout.SubtitleEnabled = subtitles.Checked; layout.Font = textFont.Text; layout.TitleSize = (int)titleSize.Value; layout.SubtitleSize = (int)font.Value; layout.SubtitleColor = settings.SubtitleColor; layout.TitleY = (double)titleY.Value; layout.SubtitleY = (double)subtitleY.Value; layout.Effect = effect.Text; layout.SubtitleAnimation = animation.Text; layout.Transition = transition.Text; episode.QueueLayout = layout;

                episode.MusicClip = kind.Text == "Müzik klibi" ? new MusicClipOptions { TrackTitle = clipTitle.Text.Trim(), Artist = clipArtist.Text.Trim(), LyricsBy = clipLyrics.Text.Trim(), MusicBy = clipComposer.Text.Trim(), VisualTheme = clipTheme.Text.Trim(), ShowCredits = clipCredits.Checked, NarrationEnabled=clipNarration.Checked, SubtitlesEnabled=clipSubtitles.Checked, Text=clipText.Text.Trim() } : null;

                if (episode.MusicClip != null) {

                    episode.Music = true; episode.QueueMusic.Enabled = true; episode.QueueMusic.Duck = false; episode.QueueMusic.Start = episode.QueueMusic.End = episode.QueueMusic.SourceOffset = 0;

                    episode.MusicClip.Duration = Task.Run(() => MusicClips.ReadDuration(store, episode.QueueMusic.File, CancellationToken.None)).GetAwaiter().GetResult();

                    episode.MusicClip.AudioHash = Reliability.FileHash(episode.QueueMusic.File); episode.LongMinutes = Math.Max(1, (int)Math.Ceiling(episode.MusicClip.Duration / 60));

                    episode.QueueLayout.SubtitleEnabled = episode.MusicClip.SubtitlesEnabled; episode.QueueLayout.TitleEnabled = false;

                    if (episode.Topic == "") episode.Topic = episode.MusicClip.TrackTitle;

                }

                episode.QueueSettings = VideoQueue.Snapshot(settings); VideoQueue.Validate(episode, store.Settings);

                VideoQueue.CopyAssets(store, episode);

                if (original == null) { episode.QueueOrder = VideoQueue.Items(store).Select(e => e.QueueOrder).DefaultIfEmpty().Max() + 1; store.Episodes.Add(episode); }

                else { episode.Plan = null; episode.Videos.Clear(); episode.Approved = false; episode.ApprovalHash = ""; episode.Revision++; store.Episodes[store.Episodes.IndexOf(original)] = episode; }

                episode.QueueState = "Bekliyor"; episode.QueueAuthorized = false; episode.Error = ""; episode.Status = "Taslak"; episode.ResumeStage = ""; store.Save(); dialog.DialogResult = DialogResult.OK; dialog.Close();

            } catch (Exception ex) { Message(ex.Message); }

        }

        var save = Theme.Button(initialKind!=null ? "Kaydet ve Düzenlemeye Geç" : original == null ? "Ekle • sonraki sıraya al" : "Değişiklikleri kaydet", Save, true, 300); save.Dock = DockStyle.Bottom; save.Height = 48; dialog.Controls.Add(save); save.SendToBack();

        var providerFields = new[] { (google,"Google"), (edgeVoice,"Edge"), (local,"Windows"), (elevenVoice as Control,"ElevenLabs"), (xttsUrl as Control,"XTTS"), (xttsSpeaker as Control,"XTTS"), (piperPath as Control,"Piper"), (piperModel as Control,"Piper") };
        var providerControls=providerFields.Select(pair=>(Field:pair.Item1!,Label:CompactUi.LabelFor(voice,pair.Item1!),Provider:pair.Item2)).ToArray();
        var providerButtons=voice.Controls.OfType<Button>().Where(b=>b.Text.Contains("Piper")||b.Text.Contains("XTTS")||b.Text.StartsWith("Google TTS",StringComparison.Ordinal)).ToArray();
        void ShowProvider() {
            foreach(var pair in providerControls) pair.Field.Visible=pair.Label.Visible=provider.Text.Contains(pair.Provider,StringComparison.OrdinalIgnoreCase);
            foreach(var button in providerButtons) button.Visible=provider.Text.Contains(button.Text.Contains("Piper")?"Piper":button.Text.Contains("XTTS")?"XTTS":"Google",StringComparison.OrdinalIgnoreCase);
        }
        provider.SelectedIndexChanged+=(_,_)=>ShowProvider(); ShowProvider();
        CompactUi.Group(voice,"Müzik zamanlaması ve geçişler",CompactUi.LabelFor(voice,fadeIn),seek);
        CompactUi.Group(art,"Açılış ve kapanış kartları",openingEnabled,closingSeconds,settings.OpeningCardEnabled||settings.ClosingCardEnabled);
        CompactUi.Group(art,"Video logosu",logoEnabled,logoSize,settings.VideoLogoEnabled);
        var musicTab=musicPage.Parent as TabPage;
        void ShowVideoKind() {
            if(musicTab==null)return;
            if(kind.Text=="Müzik klibi") { if(!tabs.TabPages.Contains(musicTab)) tabs.TabPages.Insert(1,musicTab); }
            else if(tabs.TabPages.Contains(musicTab)) tabs.TabPages.Remove(musicTab);
        }
        kind.SelectedIndexChanged+=(_,_)=>ShowVideoKind(); ShowVideoKind();

        ModernScrollbars.Apply(dialog);

        if (preview != null) dialog.Shown += (_, _) => BeginInvoke(() => preview(dialog, tabs));

        if (dialog.ShowDialog(this) == DialogResult.OK) {if(initialKind!=null)OpenWorkspace(episode);else Navigate("Üretim ve yayın planı");}

    }

}

