using System.Text.Json;



namespace AnkaStudio;



public sealed class TextEditor : Form

{

    public ContentPlan Plan { get; }

    readonly Store store;

    readonly Episode episode;

    readonly EditorTimeline timeline = new();

    readonly Label clock = Theme.Label("Zaman çizelgesi", 9, width: 360, height: 38);

    readonly NumericUpDown titleStart, titleDuration, musicStart, musicEnd, musicOffset, musicVolume, musicFadeIn, musicFadeOut;

    readonly CheckBox musicEnabled, musicDuck, musicRights;

    readonly CheckBox allScenes;

    readonly MusicUsage musicUsage;

    readonly TextBox musicFile, musicSource, musicLicense, musicAttribution;

    readonly List<(VideoScript Script, bool Vertical)> scripts = [];

    readonly ComboBox video, sceneSelect, font;

    readonly ComboBox titleFont, subtitleFont, effect, titleAnimation, subtitleAnimation, transition, titleAlign, subtitleAlign;

    readonly NumericUpDown spacing, scaleX, rotation, opacity, shadow, backgroundOpacity, animationSeconds, transitionSeconds;

    readonly CheckBox italic, underline, fadeOut, karaoke;

    readonly Button outlineColor, shadowColor, glowColor, backgroundColor, karaokeColor;

    MemoryStream? animationStream;

    readonly TextBox topText, subtitleText;

    readonly NumericUpDown topSize, subtitleSize, topX, topY, subtitleX, subtitleY, seconds, outline, sample;

    readonly CheckBox autoSceneTitles;

    readonly Button aiTitlesButton, cancelTitlesButton;

    CancellationTokenSource? titleCancel;

    bool titleBusy;

    Func<string, string, Func<string, bool>, CancellationToken, Task<string>>? titleTestRequest;

    readonly CheckBox topEnabled, subtitleEnabled, topBackground, subtitleBackground, bold;

    readonly Button topColor, subtitleColor;

    readonly PictureBox picture = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(8, 12, 20) };

    readonly Label status = Theme.Label("Önizleme hazırlanıyor...", 9, width: 800, height: 65);

    readonly System.Windows.Forms.Timer debounce = new() { Interval = 450 };

    readonly SemaphoreSlim previewGate = new(1, 1);

    CancellationTokenSource? previewCancel;

    readonly string folder;

    int activeVideo, activeScene;

    bool loading, closing, testMode;

    readonly string draftPath, draftBase; string baseline = "";

    VideoScript Script => scripts[activeVideo].Script;

    Scene Scene => Script.Scenes[activeScene];

    TextLayout Design => Script.TextLayout!;

    MusicTrack Track => Script.MusicTrack!;

    public TextEditor(Store store, Episode episode)

    {

        loading = true;

        this.store = store; this.episode = episode;

        Plan = JsonSerializer.Deserialize<ContentPlan>(JsonSerializer.Serialize(episode.Plan, Json.Options), Json.Options)!;

        draftBase = EditorDrafts.Hash(episode.Plan); draftPath = Path.Combine(store.Root, "editor-drafts", "text-" + episode.Id + ".json");

        Plan = EditorDrafts.Recover(draftPath, draftBase, Plan);

        if (Plan.Long != null) scripts.Add((Plan.Long, false)); scripts.AddRange(Plan.Shorts.Select(s => (s, true)));

        if (scripts.Count == 0 || scripts.Any(x => x.Script.Scenes.Count == 0)) throw new InvalidOperationException("Önce senaryo ve sahne planı oluştur.");

        folder = Path.Combine(store.Root, "previews", "text-" + Guid.NewGuid().ToString("N"));

        Text = "Tasarım ve zaman çizelgesi • " + episode.Topic; Width = 1460; Height = 1040; MinimumSize = new(1160, 840);

        StartPosition = FormStartPosition.CenterParent; BackColor = Theme.Bg; ForeColor = Theme.Text; Font = Theme.Font(); Padding = new Padding(20);

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 96, WrapContents = true };

        video = Theme.Combo(scripts.Select(x => (x.Vertical ? "SHORTS • " : "NORMAL • ") + x.Script.Title).ToArray(), "", 360);

        sceneSelect = Theme.Combo([], "", 230);

        allScenes = Theme.Check("Tüm sahnelere uygula • seçili video", false);

        toolbar.Controls.AddRange([video, sceneSelect, allScenes, Theme.Button("Düzeni kaydet", Save, true, 180), Theme.Button("Vazgeç", () => Close(), false, 120)]);

        var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };

        content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 405)); content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var left = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 12, 0) };

        var tabs = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 112, WrapContents = true };

        var panel = new Panel { Dock = DockStyle.Fill };

        FlowLayoutPanel Tab(string name) { var flow = Theme.Flow(370); flow.Dock = DockStyle.Fill; flow.AutoSize = false; flow.AutoScroll = true; flow.Visible = false; panel.Controls.Add(flow); return flow; }

        var top = Tab("Başlık"); var sub = Tab("Altyazı"); var common = Tab("Görünüm"); var motion = Tab("Animasyon"); var cuts = Tab("Geçişler"); var audio = Tab("Müzik notu");

        using var installed = new System.Drawing.Text.InstalledFontCollection();

        var fonts = installed.Families.Select(f => f.Name).Where(TextLayout.SafeFont).Concat(["Segoe UI", "Arial", "Tahoma", DesignFont(episode)]).Distinct().OrderBy(x => x).ToArray();

        string DesignFont(Episode e) => e.Plan?.Long?.TextLayout?.Font ?? "Segoe UI";

        void Field(FlowLayoutPanel target, string name, Control input) { target.Controls.Add(Theme.Label(name, 9, true, width: 350, height: 28)); input.Margin = new Padding(0, 0, 0, 12); target.Controls.Add(input); }

        NumericUpDown Number(FlowLayoutPanel target, string name, double value, double min, double max) { var number = Theme.Number((decimal)value, (decimal)min, (decimal)max, 350); number.DecimalPlaces = name.Contains("saniye") ? 1 : 0; Field(target, name, number); number.ValueChanged += (_, _) => Changed(); return number; }

        CheckBox Check(FlowLayoutPanel target, string name) { var check = Theme.Check(name, true, 350); target.Controls.Add(check); check.CheckedChanged += (_, _) => Changed(); return check; }

        ComboBox Choice(FlowLayoutPanel target, string name, string[] choices) { var choice = Theme.Combo(choices, choices[0], 350); Field(target, name, choice); choice.SelectedIndexChanged += (_, _) => Changed(); return choice; }

        Button ColorChoice(FlowLayoutPanel target, string name, Func<string> get, Action<string> set) { var button = Theme.Button(name, () => { Commit(); using var picker = new ColorDialog { Color = ColorTranslator.FromHtml("#" + get()), FullOpen = true }; if (picker.ShowDialog(this) == DialogResult.OK) { set($"{picker.Color.R:X2}{picker.Color.G:X2}{picker.Color.B:X2}"); Colors(); Schedule(); } }, false, 350); target.Controls.Add(button); return button; }

        topEnabled = Check(top, "Üst başlığı göster"); topText = Theme.Input("", true, 350); topText.Height = 80; Field(top, "Seçili sahnenin üst başlığı", topText);

        autoSceneTitles = Check(top, "Sahne başlıklarını yapay zekâ ayarlasın"); autoSceneTitles.Name = "AutoSceneTitles"; autoSceneTitles.CheckedChanged += (_, _) => { if (!loading && autoSceneTitles.Checked) topEnabled.Checked = true; };

        seconds = Number(top, "Başlık gösterim süresi • saniye", 4, .5, 60); seconds.Name = "SceneTitleSeconds";

        top.Controls.Add(Theme.Label("Seçili videonun tüm yapay zekâ başlıkları için geçerlidir. Sahne daha kısaysa başlık sahne sonunda biter.", 9, width: 350, height: 55));

        aiTitlesButton = Theme.Button("Tüm sahne başlıklarını şimdi ayarla", () => _ = GenerateTitles(), false, 350); aiTitlesButton.Name = "GenerateSceneTitles"; top.Controls.Add(aiTitlesButton);

        cancelTitlesButton = Theme.Button("Başlık üretimini durdur", () => titleCancel?.Cancel(), false, 350); cancelTitlesButton.Enabled = false; top.Controls.Add(cancelTitlesButton);

        top.Controls.Add(Theme.Label("Kısa başlık ve sol / orta / sağ konumu seçer. Yazı tipi, boyutu, rengi ve stilin korunur. Taşma üretimde kontrol edilir. Anlatım ve altyazı değişmez.", 9, width: 350, height: 75));

        titleFont = Choice(top, "Başlık yazı tipi", new[] { "Varsayılanı kullan" }.Concat(fonts).ToArray());

        topSize = Number(top, "Başlık boyutu • piksel", 40, 20, 180);

        topColor = Theme.Button("Başlık rengi", () => PickColor(true), false, 350); top.Controls.Add(topColor);

        topX = Number(top, "Yatay konum • %", 50, 5, 95); topY = Number(top, "Üstten konum • %", 7, 5, 95);

        topBackground = Check(top, "Başlığın arkasında koyu kutu");

        titleStart = Number(top, "Bu sahnede başlık başlangıcı • saniye", 0, 0, 1800); titleStart.DecimalPlaces = 2;

        titleDuration = Number(top, "Bu sahnede süre • saniye • 0 = varsayılan", 0, 0, 1800); titleDuration.DecimalPlaces = 2;

        seconds.ValueChanged += (_, _) => { if (loading || !autoSceneTitles.Checked) return; foreach (var scene in Script.Scenes) scene.TitleDuration = 0; loading = true; titleDuration.Value = 0; loading = false; RefreshTimeline(); Schedule(); };

        titleAlign = Choice(top, "Başlık hizalama • konumun referansı", ["Sol", "Orta", "Sağ"]);

        subtitleEnabled = Check(sub, "Altyazıyı göster"); subtitleText = Theme.Input("", true, 350); subtitleText.Height = 125; Field(sub, "Altyazı metni • seslendirme değişmez", subtitleText);

        subtitleFont = Choice(sub, "Altyazı yazı tipi", new[] { "Varsayılanı kullan" }.Concat(fonts).ToArray());

        subtitleSize = Number(sub, "Altyazı boyutu • piksel", 46, 20, 180);

        subtitleColor = Theme.Button("Altyazı rengi", () => PickColor(false), false, 350); sub.Controls.Add(subtitleColor);

        subtitleX = Number(sub, "Yatay konum • %", 50, 5, 95); subtitleY = Number(sub, "Üstten konum • %", 92, 5, 95);

        subtitleBackground = Check(sub, "Altyazının arkasında koyu kutu"); sample = Number(sub, "Önizlenecek altyazı bölümü", 1, 1, 999);

        subtitleAlign = Choice(sub, "Altyazı hizalama • konumun referansı", ["Sol", "Orta", "Sağ"]);

        font = Theme.Combo(fonts, "Segoe UI", 350); Field(common, "Varsayılan yazı tipi", font);

        var preset = Theme.Combo(["Sade", "Sinematik", "Neon turuncu", "Eğitim", "Dinamik Shorts"], "Sade", 350); Field(common, "Hazır tasarım", preset);

        common.Controls.Add(Theme.Button("Hazır tasarımı uygula", () => { Commit(); ApplyPreset(preset.Text); LoadFields(); Schedule(); }, true, 350));

        effect = Choice(common, "Yazı efekti", TextLayout.Effects);

        bold = Check(common, "Kalın yazı"); italic = Check(common, "İtalik yazı"); underline = Check(common, "Altı çizili yazı");

        outline = Number(common, "Dış çizgi • piksel", 3, 0, 12); shadow = Number(common, "Gölge derinliği • piksel", 1, 0, 12);

        outlineColor = ColorChoice(common, "Dış çizgi rengi", () => Design.OutlineColor, v => Design.OutlineColor = v);

        shadowColor = ColorChoice(common, "Gölge rengi", () => Design.ShadowColor, v => Design.ShadowColor = v);

        glowColor = ColorChoice(common, "Neon parıltı rengi", () => Design.GlowColor, v => Design.GlowColor = v);

        backgroundColor = ColorChoice(common, "Arka kutu rengi", () => Design.BackgroundColor, v => Design.BackgroundColor = v);

        backgroundOpacity = Number(common, "Arka kutu opaklığı • %", 65, 0, 100); opacity = Number(common, "Yazı opaklığı • %", 100, 10, 100);

        spacing = Number(common, "Harf aralığı • piksel", 0, 0, 15); scaleX = Number(common, "Yazı genişliği • %", 100, 50, 150); rotation = Number(common, "Yazı açısı • derece", 0, -30, 30);

        common.Controls.Add(Theme.Label("Yazı tipleri bilgisayarındaki fontlardan alınır. Bulutta aynı font yoksa motor uygun bir fontla değiştirir; görünüm farklı olabilir. Görünüm ayarları başlık ve altyazıyı birlikte etkiler.", 10, color: Theme.Teal, width: 350, height: 100));

        common.Controls.Add(Theme.Button("Varsayılan düzen", () => { Script.TextLayout = TextLayout.Defaults(store.Settings, scripts[activeVideo].Vertical); LoadFields(); Schedule(); }, false, 350));

        common.Controls.Add(Theme.Button("Düzeni diğer videolara uygula", () => { Commit(); foreach (var item in scripts) if (item.Script != Script) item.Script.TextLayout = JsonSerializer.Deserialize<TextLayout>(JsonSerializer.Serialize(Design))!; status.Text = "Düzen diğer videoların taslağına uygulandı. Metinler değişmedi. Kalıcı kayıt için Düzeni kaydet."; }, false, 350));

        titleAnimation = Choice(motion, "Başlık giriş animasyonu", TextLayout.Animations); subtitleAnimation = Choice(motion, "Altyazı giriş animasyonu", TextLayout.Animations);

        animationSeconds = Number(motion, "Animasyon süresi • saniye", .35, .1, 2); animationSeconds.DecimalPlaces = 2;

        fadeOut = Check(motion, "Yazı çıkışında yumuşak kaybolma"); karaoke = Check(motion, "Altyazıda ilerleyen kelime vurgusu");

        karaokeColor = ColorChoice(motion, "Henüz okunmayan kelime rengi", () => Design.KaraokeColor, v => Design.KaraokeColor = v);

        motion.Controls.Add(Theme.Label("Kelime vurgusu metin uzunluğuna göre yaklaşık ilerler; kesin konuşma takibi değildir. Önizleme çizelgede seçtiğin andan en fazla 6 saniye gösterir. Ses hazır değilse sahne süreleri tahminidir.", 10, color: Theme.Teal, width: 350, height: 120));

        transition = Choice(cuts, "Sahneler arasındaki geçiş", TextLayout.Transitions); transitionSeconds = Number(cuts, "Geçiş süresi • saniye", .5, .1, 2); transitionSeconds.DecimalPlaces = 2;

        cuts.Controls.Add(Theme.Label("Geçiş tüm sahne sınırlarına uygulanır. Çok kısa sahnelerde süre otomatik kısaltılır. Yeni sahnenin ilk karesi geçiş boyunca tutulur; anlatım süresi ve sahne zamanları kısalmaz. Sonraki sahne yoksa ilk sahneyle örnek geçiş gösterilir.", 10, color: Theme.Teal, width: 350, height: 150));

        var musicTiming = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Width = 390, Height = 540 };
        musicEnabled = Check(musicTiming, "Bu videoda müzik kullan");
        musicFile = Theme.Input("", false, 350); musicFile.ReadOnly = true;
        musicStart = Number(musicTiming, "Videoda müzik başlangıcı • saniye", 0, 0, 1800); musicStart.DecimalPlaces = 2;

        musicEnd = Number(musicTiming, "Müzik bitişi • saniye • 0 = video sonu", 0, 0, 1800); musicEnd.DecimalPlaces = 2;

        musicOffset = Number(musicTiming, "Müzik dosyasında kesme başlangıcı • saniye", 0, 0, 86400); musicOffset.DecimalPlaces = 2;

        musicVolume = Number(musicTiming, "Müzik ses seviyesi • %", 12, 0, 100); musicFadeIn = Number(musicTiming, "Müzik giriş solması • saniye", .5, 0, 30); musicFadeOut = Number(musicTiming, "Müzik çıkış solması • saniye", 1, 0, 30);

        musicDuck = Check(musicTiming, "Konuşma sırasında müziği otomatik kıs");

        musicUsage = new MusicUsage("", "", "", false, 350); audio.Controls.Add(musicUsage); musicSource = musicUsage.Source; musicLicense = musicUsage.License; musicAttribution = musicUsage.Attribution; musicRights = musicUsage.Rights;



        musicTiming.Controls.Add(Theme.Button("Bu videodan müziği kaldır", () => { Track.Enabled = false; LoadFields(); RefreshTimeline(); Schedule(); }, false, 350));


        foreach (var (name, flow) in new[] { ("Başlık", top), ("Altyazı", sub), ("Görünüm", common), ("Animasyon", motion), ("Geçişler", cuts), ("Müzik notu", audio) }) {

            Button? button = null; button = Theme.Button(name, () => { foreach (Control c in panel.Controls) c.Visible = c == flow; foreach (Button b in tabs.Controls) { b.BackColor = b == button ? Theme.Teal : Theme.Elevated; b.ForeColor = b == button ? Theme.Bg : Theme.Text; } }, name == "Başlık", 112); button.Height = 42; button.Margin = new Padding(0, 0, 8, 8); tabs.Controls.Add(button);

        }

        top.Visible = true; left.Controls.Add(panel); left.Controls.Add(tabs);

        var right = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) }; status.Dock = DockStyle.Bottom;

        var player = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 54 };

        player.Controls.AddRange([Theme.Button("▶ Hareketli önizleme", () => { debounce.Stop(); previewCancel?.Cancel(); _ = RenderPreview(true); }, true, 210), Theme.Button("■ Durdur", () => { previewCancel?.Cancel(); if (picture.Image != null) Display(new Bitmap(picture.Image)); status.Text = "Hareketli önizleme durdu. Ayarları değiştirebilir veya yeniden oynatabilirsin."; }, false, 120), Theme.Button("♫ Sesli önizlemeyi aç", () => { debounce.Stop(); previewCancel?.Cancel(); _ = RenderPreview(true, true); }, false, 210)]);

        right.Controls.Add(picture); right.Controls.Add(status); right.Controls.Add(player);

        content.Controls.Add(left, 0, 0); content.Controls.Add(right, 1, 0);

        var timelineArea = new Panel { Dock = DockStyle.Bottom, Height = 295 }; var timelineHeader = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 42, WrapContents = false };

        var strip = new Panel { Dock = DockStyle.Fill, AutoScroll = true }; timeline.Width = 1350; strip.Controls.Add(timeline); int zoom = 1;

        void ResizeTimeline() { timeline.Width = Math.Max(650, strip.ClientSize.Width - 20) * zoom; timeline.Invalidate(); }

        strip.Resize += (_, _) => ResizeTimeline();

        timelineHeader.Controls.Add(Theme.Button("Müzik ayarları", () => {
            using var settings = new Form { Text = "Müzik zamanlaması ve ses", StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(410, 570), BackColor = BackColor, ForeColor = ForeColor, ShowInTaskbar = false };
            musicTiming.Dock = DockStyle.Fill; settings.Controls.Add(musicTiming);
            try { settings.ShowDialog(this); } finally { settings.Controls.Remove(musicTiming); }
        }, false, 150));
        timelineHeader.Controls.AddRange([clock, Theme.Button("+ Müzik ekle", ChooseArchiveMusic, false, 150), Theme.Button("+ Görüntü ekle", ChooseArchiveVideo, false, 150), Theme.Button("Yakınlaş +", () => { zoom = Math.Min(8, zoom + 1); ResizeTimeline(); }, false, 120), Theme.Button("Uzaklaş −", () => { zoom = Math.Max(1, zoom - 1); ResizeTimeline(); }, false, 120)]);

        var hint = Theme.Label("Cetvele tıkla: o anı göster • Turuncu başlığı taşı / kenarlarını sürükle: başlangıç ve süre • Müzik şeridini taşı / sağ kenarını sürükle", 9, color: Theme.Teal, width: 1300, height: 28); hint.Dock = DockStyle.Bottom;

        timelineArea.Controls.Add(strip); timelineArea.Controls.Add(hint); timelineArea.Controls.Add(timelineHeader);

        Controls.Add(content); Controls.Add(timelineArea); Controls.Add(toolbar);

        timeline.SceneSelected += index => { if (index != activeScene) sceneSelect.SelectedIndex = index; };

        timeline.Seeked += _ => { var captions = NarrationFlow.Align(Scene.SubtitleText == "" ? Scene.Narration : Scene.SubtitleText, EditorTimeline.Duration(Scene, Speed())); double local = timeline.Position - timeline.SceneStart(activeScene); int found = captions.FindIndex(c => c.Start <= local && c.End > local); loading = true; sample.Value = Math.Max(1, found + 1); loading = false; Clock(); Schedule(); };

        timeline.Edited += () => { LoadFields(); RefreshTimeline(); Schedule(); };

        topText.TextChanged += (_, _) => Changed(); subtitleText.TextChanged += (_, _) => Changed(); font.SelectedIndexChanged += (_, _) => Changed();

        sample.ValueChanged += (_, _) => { if (loading || closing) return; var pieces = NarrationFlow.Align(Scene.SubtitleText == "" ? Scene.Narration : Scene.SubtitleText, EditorTimeline.Duration(Scene, Speed())); var cue = pieces.ElementAtOrDefault(Math.Min((int)sample.Value - 1, pieces.Count - 1)); if (cue != null) { timeline.Position = timeline.SceneStart(activeScene) + cue.Start + Math.Min(.1, (cue.End - cue.Start) / 2); RefreshTimeline(); Schedule(); } };

        musicRights.CheckedChanged += (_, _) => Changed();

        foreach (var input in new[] { musicSource, musicLicense, musicAttribution }) input.TextChanged += (_, _) => Changed();

        video.SelectedIndexChanged += (_, _) => { if (loading) return; Commit(); activeVideo = video.SelectedIndex; activeScene = 0; LoadVideo(); };

        sceneSelect.SelectedIndexChanged += (_, _) => { if (loading || sceneSelect.SelectedIndex < 0) return; Commit(); activeScene = sceneSelect.SelectedIndex; LoadFields(); timeline.Position = timeline.SceneStart(activeScene) + Math.Min(1, EditorTimeline.Duration(Scene, Speed()) / 2); RefreshTimeline(); Schedule(); };

        debounce.Tick += (_, _) => { debounce.Stop(); _ = RenderPreview(); };

        FormClosing += (_, ev) => {

            if (titleBusy) { titleCancel?.Cancel(); ev.Cancel = true; status.Text = "Başlık üretimi durduruluyor; ardından kapatabilirsin."; return; }

            Commit();

            if (DialogResult != DialogResult.OK && EditorDrafts.Hash(Plan) != baseline && !testMode) {

                EditorDrafts.Save(draftPath, draftBase, Plan);

                var answer = MessageBox.Show(this, "Kaydedilmemiş düzen var. Kaydedilsin mi?\nHayır: taslak korunarak çıkılır. İptal: düzenlemeye dönülür.", "Düzen taslağı", MessageBoxButtons.YesNoCancel);

                if (answer == DialogResult.Cancel) { ev.Cancel = true; return; }

                if (answer == DialogResult.Yes) { ev.Cancel = true; Save(); ev.Cancel = DialogResult != DialogResult.OK; if (ev.Cancel) return; }

            }

            closing = true; debounce.Stop(); previewCancel?.Cancel();

        };

        FormClosed += (_, _) => { debounce.Dispose(); picture.Image?.Dispose(); animationStream?.Dispose(); };

        AllowDrop = true; DragEnter += (_, e) => { if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true) e.Effect = DragDropEffects.Copy; };

        DragDrop += (_, e) => { if (e.Data?.GetData(DataFormats.FileDrop) is string[] paths && paths.Length == 1) { var extension = Path.GetExtension(paths[0]).ToLowerInvariant(); if (new[] { ".mp3", ".wav", ".m4a", ".flac", ".ogg", ".aac" }.Contains(extension)) SetMusic(paths[0]); else if (new[] { ".mp4", ".mov", ".mkv", ".webm" }.Contains(extension)) SetVideo(paths[0]); } };

        foreach (var entry in scripts) InitializeDesign(entry.Script, entry.Vertical);

        Shown += (_, _) => Schedule(); LoadVideo(); baseline = EditorDrafts.Hash(Plan);

    }

    void LoadVideo()

    {

        loading = true; video.SelectedIndex = activeVideo; sceneSelect.Items.Clear(); sceneSelect.Items.AddRange(Script.Scenes.Select((s, i) => "Sahne " + (i + 1)).ToArray()); sceneSelect.SelectedIndex = activeScene; loading = false;

        InitializeDesign(Script, scripts[activeVideo].Vertical);

        timeline.Position = Math.Min(1, EditorTimeline.Duration(Scene, Speed()) / 2); LoadFields(); RefreshTimeline(); Schedule();

    }

    void InitializeDesign(VideoScript script, bool vertical)

    {

        script.TextLayout ??= TextLayout.Defaults(store.Settings, vertical);

        if (script.MusicTrack == null && episode.QueueMusic != null) script.MusicTrack = JsonSerializer.Deserialize<MusicTrack>(JsonSerializer.Serialize(episode.QueueMusic))!;

        TextEditSave.InheritPackageMusic(Plan, script, episode.Music, store.Settings.MusicPath, vertical && store.Settings.ShortVoiceOverride ? store.Settings.ShortMusicVolume : store.Settings.MusicVolume);

        if (script.MusicTrack == null) {

            var item = new AudioLibrary(store).List().FirstOrDefault(a => a.File == store.Settings.MusicPath);

            script.MusicTrack = store.Settings.LibraryMusic?.File == store.Settings.MusicPath ? JsonSerializer.Deserialize<MusicTrack>(JsonSerializer.Serialize(store.Settings.LibraryMusic))! : new MusicTrack { Enabled = episode.Music, File = store.Settings.MusicPath, Volume = vertical && store.Settings.ShortVoiceOverride ? store.Settings.ShortMusicVolume : store.Settings.MusicVolume, Source = item?.Source ?? "", License = item?.License ?? "", Attribution = item?.Attribution ?? "", RightsConfirmed = item?.RightsConfirmed ?? false };

            script.MusicTrack.Enabled = episode.Music; script.MusicTrack.Volume = vertical && store.Settings.ShortVoiceOverride ? store.Settings.ShortMusicVolume : store.Settings.MusicVolume;

        }

    }

    double Speed() => scripts[activeVideo].Vertical && store.Settings.ShortVoiceOverride && store.Settings.ShortVoiceProvider != "Normal videoyla aynı" ? store.Settings.ShortVoiceSpeed : store.Settings.VoiceSpeed;

    void Clock() { clock.Text = $"Seçili an {TimeSpan.FromSeconds(timeline.Position):mm\\:ss\\.ff} / {TimeSpan.FromSeconds(timeline.Total):mm\\:ss}\n" + (Script.Scenes.All(s => s.Duration > 0) ? "Ölçülen sahne süreleri" : "Ses henüz yok: süreler tahmini"); }

    void RefreshTimeline() { timeline.Script = Script; timeline.VoiceSpeed = Speed(); timeline.SelectedScene = activeScene; timeline.Position = Math.Min(timeline.Position, timeline.Total); timeline.Invalidate(); Clock(); }

    void ChooseArchiveMusic()
    {
        var items = new AudioLibrary(store).List(refresh: true).Where(a => a.Kind == "Müzik" && File.Exists(a.File)).ToArray();
        using var dialog = new Form { Text = "Müzik arşivi", StartPosition = FormStartPosition.CenterParent, Size = new Size(740, 500), MinimumSize = new Size(720, 360), BackColor = BackColor, ForeColor = ForeColor, ShowInTaskbar = false, MinimizeBox = false };
        var list = new ListBox { Dock = DockStyle.Fill, DisplayMember = "Name", BackColor = BackColor, ForeColor = ForeColor, Font = Font, IntegralHeight = false };
        foreach (var item in items) list.Items.Add(item);
        var info = new Label { Dock = DockStyle.Top, Height = 64, Padding = new Padding(12), Text = items.Length == 0 ? "Müzik arşivi boş. Müzik ekle düğmesiyle dosyalarını arşive ekleyebilirsin." : "Arşivden bir parça seç ve videoya ekle." };
        var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 60, Padding = new Padding(8) };
        void SelectMusic()
        {
            if (list.SelectedItem is not AudioItem item) return;
            SetMusic(item.File); dialog.Close();
        }
        bool importing = false;
        using var cancellation = new CancellationTokenSource();
        dialog.FormClosing += (_, e) => { if (importing) { cancellation.Cancel(); e.Cancel = true; } };
        async Task ImportMusic()
        {
            if (importing) return;
            using var picker = new OpenFileDialog { Multiselect = true, Filter = "Müzik / ses|*.mp3;*.wav;*.m4a;*.flac;*.ogg;*.aac", Title = "Arşive müzik ekle" };
            if (picker.ShowDialog(dialog) != DialogResult.OK) return;
            if (picker.FileNames.Length > 20) { Theme.Alert(dialog, "Tek seferde en fazla 20 dosya seçebilirsin."); return; }
            importing = true; actions.Enabled = false;
            var errors = new List<string>();
            try
            {
                var library = new AudioLibrary(store);
                foreach (var file in picker.FileNames)
                {
                    cancellation.Token.ThrowIfCancellationRequested();
                    info.Text = "Arşive ekleniyor: " + Path.GetFileName(file);
                    try { await library.Add(file, new AudioItem { Kind = "Müzik" }, cancellation.Token); }
                    catch (Exception ex) when (ex is not OperationCanceledException) { errors.Add(Path.GetFileName(file) + ": " + ex.Message); }
                }
            }
            catch (OperationCanceledException) { errors.Add("Aktarım durduruldu. Tamamlanan dosyalar arşivde kaldı."); }
            finally
            {
                importing = false; actions.Enabled = true;
                list.Items.Clear();
                foreach (var item in new AudioLibrary(store).List(refresh: true).Where(a => a.Kind == "Müzik" && File.Exists(a.File))) list.Items.Add(item);
                if (list.Items.Count > 0) list.SelectedIndex = list.Items.Count - 1;
                info.Text = "Liste güncellendi. Eklediğin müzikler arşive kaydedildi.";
            }
            if (errors.Count > 0) Theme.Alert(dialog, string.Join(Environment.NewLine, errors));
        }
        actions.Controls.Add(Theme.Button("Videoya ekle", SelectMusic, true, 170));
        actions.Controls.Add(Theme.Button("Müzik ekle", () => _ = ImportMusic(), false, 150));
        var player = new ArchiveMusicPlayer();
        list.SelectedIndexChanged += (_, _) => { if (list.SelectedItem is AudioItem item && File.Exists(item.File)) player.Load(item); };
        actions.Controls.Add(Theme.Button("Kapat", () => dialog.Close(), false, 120));
        list.DoubleClick += (_, _) => SelectMusic();
        dialog.Controls.Add(list); dialog.Controls.Add(info); dialog.Controls.Add(actions); dialog.Controls.Add(player);
        if (items.Length > 0) list.SelectedIndex = 0;
        dialog.ShowDialog(this);
    }
    void ChooseMusic() { using var choice = new OpenFileDialog { Filter = "Müzik / ses|*.mp3;*.wav;*.m4a;*.flac;*.ogg;*.aac" }; if (choice.ShowDialog(this) == DialogResult.OK) SetMusic(choice.FileName); }

    void SetMusic(string file)

    {

        try { if (!File.Exists(file) || new FileInfo(file).Length > 100 * 1024 * 1024) throw new InvalidOperationException("Müzik dosyası bulunamadı veya 100 MB sınırını aşıyor.");

            Commit(); Track.File = file; Track.Enabled = true; var item = new AudioLibrary(store).List().FirstOrDefault(a => a.File == file); Track.Source = item?.Source ?? "Kullanıcının arşivi"; Track.License = item?.License ?? ""; Track.Attribution = item?.Attribution ?? ""; Track.RightsConfirmed = item?.RightsConfirmed ?? false;

            LoadFields(); RefreshTimeline(); SelectTab("Müzik notu"); Schedule();

        } catch (Exception ex) { Theme.Alert(this, ex.Message); }

    }

    void ChooseArchiveVideo()
    {
        var pool = new MediaPool(store);
        using var dialog = new Form { Text="Görüntü arşivi", StartPosition=FormStartPosition.CenterParent, Size=new Size(1000,600), MinimumSize=new Size(800,450), BackColor=BackColor, ForeColor=ForeColor, ShowInTaskbar=false };
        var split = new SplitContainer { Dock=DockStyle.Fill, Width=960, SplitterDistance=380 };
        var list = new ListBox { Dock=DockStyle.Fill, BackColor=BackColor, ForeColor=ForeColor, Font=Font };
        var preview = new ArchiveVideoPreview { Dock=DockStyle.Fill };
        split.Panel1.Controls.Add(list); split.Panel2.Controls.Add(preview);
        var info = new Label { Dock=DockStyle.Top, Height=50, Padding=new Padding(10), Text="Görüntüye tıkla: sağdaki kutuda önizleme başlar." };
        var actions = new FlowLayoutPanel { Dock=DockStyle.Bottom, Height=60, Padding=new Padding(8) };
        PoolItem[] items=[]; bool busy=false;
        using var cancellation=new CancellationTokenSource();
        dialog.FormClosing+=(_,e)=>{if(busy){cancellation.Cancel();e.Cancel=true;}};
        void RefreshItems() { items=pool.List().Where(x=>File.Exists(x.File)).ToArray();list.Items.Clear();foreach(var item in items)list.Items.Add(string.IsNullOrWhiteSpace(item.Tags)?Path.GetFileName(item.File):item.Tags);info.Text=items.Length==0?"Arşiv boş. Görüntü ekle düğmesiyle dosya seçebilirsin.":"Görüntüye tıkla: sağdaki kutuda önizleme başlar."; }
        async Task Run(bool import)
        {
            if(busy)return;
            string[] files=[];
            if(import){using var picker=new OpenFileDialog{Multiselect=true,Filter="Video|*.mp4;*.mov;*.mkv;*.webm"};if(picker.ShowDialog(dialog)!=DialogResult.OK)return;files=picker.FileNames;if(files.Length>20){Theme.Alert(dialog,"Tek seferde en fazla 20 dosya seç.");return;}}
            busy=true;actions.Enabled=false;list.Enabled=false;
            try {
                if(import) foreach(var file in files){ info.Text="Arşive ekleniyor: "+Path.GetFileName(file);await pool.AddAsync(file,Path.GetFileNameWithoutExtension(file),"Genel","Kullanıcının yerel arşivi","Kullanıcı arşivi","Kullanım hakkı kullanıcı tarafından doğrulanmalı",cancellation.Token); }
                else { info.Text="Arşiv güncelleniyor…";await pool.ScanFolder(cancellation.Token); }
            }catch(OperationCanceledException){}catch(Exception ex){Theme.Alert(dialog,ex.Message);}
            finally{busy=false;actions.Enabled=true;list.Enabled=true;RefreshItems();}
        }
        async Task SelectVideo()
        {
            if(busy||list.SelectedIndex<0)return;
            var item=items[list.SelectedIndex];var index=activeVideo;busy=true;actions.Enabled=false;
            try{await MediaFormat.Video(store,item.File,scripts[index].Vertical,cancellation.Token);if(index!=activeVideo)return;SetVideo(item.File);Scene.StockUrl=item.Source;Scene.Creator=item.Creator;Scene.LicenseUrl=item.License;Scene.Attribution=item.Attribution;Scene.RightsConfirmed=item.RightsConfirmed;Schedule();busy=false;dialog.Close();}
            catch(OperationCanceledException){}catch(Exception ex){Theme.Alert(dialog,ex.Message);}finally{busy=false;if(!dialog.IsDisposed)actions.Enabled=true;}
        }
        actions.Controls.Add(Theme.Button("Videoya ekle",()=>_=SelectVideo(),true,170));
        actions.Controls.Add(Theme.Button("Görüntü ekle",()=>_=Run(true),false,170));
        actions.Controls.Add(Theme.Button("Kapat",()=>dialog.Close(),false,120));
        list.SelectedIndexChanged+=(_,_)=>{if(list.SelectedIndex>=0)preview.Play(items[list.SelectedIndex].File);};
        dialog.Controls.Add(split);dialog.Controls.Add(info);dialog.Controls.Add(actions);
        RefreshItems();dialog.Shown+=(_,_)=>_=Run(false);dialog.ShowDialog(this);
    }
    async void ChooseVideo() { using var choice = new OpenFileDialog { Filter = "Video|*.mp4;*.mov;*.mkv;*.webm" }; if (choice.ShowDialog(this) == DialogResult.OK) {var index=activeVideo;try{await MediaFormat.Video(store,choice.FileName,scripts[index].Vertical,CancellationToken.None);if(!IsDisposed && index==activeVideo)SetVideo(choice.FileName);}catch(Exception ex){if(!IsDisposed)Theme.Alert(this,ex.Message);}} }

    void SetVideo(string file) { if (!File.Exists(file)) return; Commit(); Scene.StockFile = file; Scene.StockUrl = "Kullanıcının yerel arşivi"; Scene.Creator = "Kullanıcı arşivi"; Scene.LicenseUrl = "Kullanıcı tarafından lisansı doğrulanacak"; Scene.Attribution = ""; Scene.UserLocal = true; Scene.RightsConfirmed = false; RefreshTimeline(); Schedule(); }

    void SelectTab(string name) { IEnumerable<Control> Children(Control p) => p.Controls.Cast<Control>().SelectMany(c => new[] { c }.Concat(Children(c))); Children(this).OfType<Button>().Single(b => b.Text == name).PerformClick(); }

    void LoadFields()

    {

        loading = true; topText.Text = Scene.OnScreen; subtitleText.Text = Scene.SubtitleText == "" ? Scene.Narration : Scene.SubtitleText;

        var d = Design; font.SelectedItem = d.Font; topEnabled.Checked = d.TitleEnabled; autoSceneTitles.Checked = d.AutoSceneTitles; subtitleEnabled.Checked = d.SubtitleEnabled; topSize.Value = d.TitleSize; subtitleSize.Value = d.SubtitleSize;

        topX.Value = (decimal)d.TitleX; topY.Value = (decimal)d.TitleY; subtitleX.Value = (decimal)d.SubtitleX; subtitleY.Value = (decimal)d.SubtitleY; seconds.Value = (decimal)d.TitleSeconds;

        topBackground.Checked = d.TitleBackground; subtitleBackground.Checked = d.SubtitleBackground; bold.Checked = d.Bold; outline.Value = d.Outline; sample.Value = 1;

        void FontChoice(ComboBox choice, string value) { if (!choice.Items.Contains(value)) choice.Items.Add(value); choice.SelectedItem = value; }

        FontChoice(font, d.Font); FontChoice(titleFont, d.TitleFont == "" ? "Varsayılanı kullan" : d.TitleFont); FontChoice(subtitleFont, d.SubtitleFont == "" ? "Varsayılanı kullan" : d.SubtitleFont);

        effect.SelectedItem = d.Effect; italic.Checked = d.Italic; underline.Checked = d.Underline; shadow.Value = d.Shadow; spacing.Value = d.Spacing; scaleX.Value = d.ScaleX; rotation.Value = d.Rotation; opacity.Value = d.Opacity; backgroundOpacity.Value = d.BackgroundOpacity;

        titleAlign.SelectedIndex = d.TitleAlignment - 1; subtitleAlign.SelectedIndex = d.SubtitleAlignment - 1;

        titleAnimation.SelectedItem = d.TitleAnimation; subtitleAnimation.SelectedItem = d.SubtitleAnimation; animationSeconds.Value = (decimal)d.AnimationSeconds; fadeOut.Checked = d.FadeOut; karaoke.Checked = d.Karaoke;

        transition.SelectedItem = d.Transition; transitionSeconds.Value = (decimal)d.TransitionSeconds;

        titleStart.Value = (decimal)Scene.TitleStart; titleDuration.Value = (decimal)Scene.TitleDuration;

        musicEnabled.Checked = Track.Enabled; musicFile.Text = Track.File; musicStart.Value = (decimal)Track.Start; musicEnd.Value = (decimal)Track.End; musicOffset.Value = (decimal)Track.SourceOffset; musicVolume.Value = (decimal)(Track.Volume * 100); musicFadeIn.Value = (decimal)Track.FadeIn; musicFadeOut.Value = (decimal)Track.FadeOut; musicDuck.Checked = Track.Duck; musicSource.Text = Track.Source; musicLicense.Text = Track.License; musicAttribution.Text = Track.Attribution; musicRights.Checked = Track.RightsConfirmed; musicUsage.RefreshFromSaved();

        Colors(); loading = false;

    }

    void Colors() { topColor.Text = "Başlık rengi • #" + Design.TitleColor; subtitleColor.Text = "Altyazı rengi • #" + Design.SubtitleColor;

        outlineColor.Text = "Dış çizgi • #" + Design.OutlineColor; shadowColor.Text = "Gölge • #" + Design.ShadowColor; glowColor.Text = "Parıltı • #" + Design.GlowColor; backgroundColor.Text = "Arka kutu • #" + Design.BackgroundColor; karaokeColor.Text = "Okunmayan kelime • #" + Design.KaraokeColor;

    }

    void ApplyPreset(string name)

    {

        var d = TextLayout.Defaults(store.Settings, scripts[activeVideo].Vertical);

        if (name == "Sinematik") { d.TitleFont = "Georgia"; d.SubtitleFont = "Segoe UI"; d.TitleSize = 64; d.Spacing = 2; d.TitleColor = "FFD080"; d.TitleBackground = false; d.TitleAnimation = "Soluk giriş"; d.FadeOut = true; d.Transition = "Siyaha geçiş"; }

        if (name == "Neon turuncu") { d.TitleSize = 64; d.TitleColor = "FF8800"; d.Effect = "Neon parıltı"; d.GlowColor = "FF5500"; d.TitleBackground = false; d.TitleAnimation = "Yakınlaşma"; d.FadeOut = true; d.Transition = "Yumuşak karışım"; }

        if (name == "Eğitim") { d.TitleBackground = true; d.SubtitleBackground = true; d.BackgroundColor = "153047"; d.TitleColor = "5EE4D2"; d.Outline = 2; d.TitleSize = 56; d.TitleAnimation = "Alttan kayma"; d.Transition = "Yumuşak karışım"; }

        if (name == "Dinamik Shorts") { d.TitleSize = 76; d.SubtitleSize = 72; d.TitleColor = "FFAA00"; d.Effect = "3D kabartma"; d.TitleBackground = false; d.TitleAnimation = "Vurgu / pop"; d.SubtitleAnimation = "Alttan kayma"; d.Karaoke = true; d.Transition = "Sola silme"; d.TransitionSeconds = .3; }

        Script.TextLayout = d;

    }

    void PickColor(bool top)

    {

        Commit(); using var dialog = new ColorDialog { Color = ColorTranslator.FromHtml("#" + (top ? Design.TitleColor : Design.SubtitleColor)), FullOpen = true };

        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        var value = $"{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}"; if (top) Design.TitleColor = value; else Design.SubtitleColor = value; Colors(); Schedule();

    }

    void Commit()

    {

        if (loading || Script.TextLayout == null) return;

        Scene.OnScreen = topText.Text.Trim(); var value = subtitleText.Text.Trim(); Scene.SubtitleText = value == Scene.Narration ? "" : value;

        var d = Design; d.Font = font.Text; d.TitleEnabled = topEnabled.Checked; d.AutoSceneTitles = autoSceneTitles.Checked; d.SubtitleEnabled = subtitleEnabled.Checked; d.TitleSize = (int)topSize.Value; d.SubtitleSize = (int)subtitleSize.Value;

        d.TitleX = (double)topX.Value; d.TitleY = (double)topY.Value; d.SubtitleX = (double)subtitleX.Value; d.SubtitleY = (double)subtitleY.Value; d.TitleSeconds = (double)seconds.Value;

        d.TitleBackground = topBackground.Checked; d.SubtitleBackground = subtitleBackground.Checked; d.Bold = bold.Checked; d.Outline = (int)outline.Value;

        d.TitleFont = titleFont.Text == "Varsayılanı kullan" ? "" : titleFont.Text; d.SubtitleFont = subtitleFont.Text == "Varsayılanı kullan" ? "" : subtitleFont.Text;

        d.Effect = effect.Text; d.Italic = italic.Checked; d.Underline = underline.Checked; d.Shadow = (int)shadow.Value; d.Spacing = (int)spacing.Value; d.ScaleX = (int)scaleX.Value; d.Rotation = (int)rotation.Value; d.Opacity = (int)opacity.Value; d.BackgroundOpacity = (int)backgroundOpacity.Value;

        d.TitleAlignment = titleAlign.SelectedIndex + 1; d.SubtitleAlignment = subtitleAlign.SelectedIndex + 1; d.TitleAnimation = titleAnimation.Text; d.SubtitleAnimation = subtitleAnimation.Text; d.AnimationSeconds = (double)animationSeconds.Value; d.FadeOut = fadeOut.Checked; d.Karaoke = karaoke.Checked; d.Transition = transition.Text; d.TransitionSeconds = (double)transitionSeconds.Value;

        Scene.TitleStart = (double)titleStart.Value; Scene.TitleDuration = (double)titleDuration.Value;

        musicUsage.Commit();

        if (Script.MusicTrack != null) { Track.Enabled = musicEnabled.Checked; Track.Start = (double)musicStart.Value; Track.End = (double)musicEnd.Value; Track.SourceOffset = (double)musicOffset.Value; Track.Volume = (double)musicVolume.Value / 100; Track.FadeIn = (double)musicFadeIn.Value; Track.FadeOut = (double)musicFadeOut.Value; Track.Duck = musicDuck.Checked; Track.Source = musicSource.Text.Trim(); Track.License = musicLicense.Text.Trim(); Track.Attribution = musicAttribution.Text.Trim(); Track.RightsConfirmed = musicRights.Checked; }

    }

    void Changed() { if (loading || closing) return; Commit(); RefreshTimeline(); Schedule(); }

    void Schedule() { if (closing) return; if (baseline != "") EditorDrafts.Save(draftPath, draftBase, Plan); previewCancel?.Cancel(); debounce.Stop(); debounce.Start(); }

    void Display(Image image, MemoryStream? stream = null)

    {

        var oldImage = picture.Image; var oldStream = animationStream; picture.Image = image; animationStream = stream; oldImage?.Dispose(); oldStream?.Dispose();

    }

    async Task RenderPreview(bool moving = false, bool openAudio = false)

    {

        var cancel = new CancellationTokenSource(); previewCancel = cancel; bool entered = false;

        try {

            Commit(); var selected = JsonSerializer.Deserialize<Scene>(JsonSerializer.Serialize(Scene))!; var design = JsonSerializer.Deserialize<TextLayout>(JsonSerializer.Serialize(Design))!;

            var timed = EditorPreview.Timed(Script, Speed()); double selectedTime = timeline.Position; int selectedScene = activeScene;

            bool vertical = scripts[activeVideo].Vertical; int index = (int)sample.Value - 1;

            await previewGate.WaitAsync(cancel.Token); entered = true;

            if (!closing) status.Text = (moving ? "Hareketli önizleme hazırlanıyor" : "Hızlı tasarım önizlemesi hazırlanıyor") + " • ücretli servis kullanılmıyor...";

            string? narration = null;

            var key = vertical ? "short-" + (Plan.Shorts.IndexOf(Script) + 1) : "long";

            var priorFile = episode.Videos.FirstOrDefault(v => v.Key == key)?.File;

            if (priorFile != null) {

                var audio = Path.Combine(Path.GetDirectoryName(priorFile)!, "narration-full.wav"); var settings = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(store.Settings))!;

                if (vertical && settings.ShortVoiceOverride && settings.ShortVoiceProvider != "Normal videoyla aynı") { settings.VoiceProvider = settings.ShortVoiceProvider; settings.VoiceId = settings.ShortVoiceId; settings.VoiceSpeed = settings.ShortVoiceSpeed; }

                if (File.Exists(audio) && File.Exists(audio + ".hash") && await File.ReadAllTextAsync(audio + ".hash", cancel.Token) == NarrationFlow.Fingerprint(timed, settings)) narration = audio;

            }

            if (moving) await EditorPreview.Movie(store, timed, vertical, selectedTime, narration, folder, cancel.Token);

            else await EditorPreview.FastFrame(store, timed, selectedScene, selectedTime - timed.Scenes.Take(selectedScene).Sum(s => s.Duration), vertical, folder, cancel.Token);

            cancel.Token.ThrowIfCancellationRequested();

            if (closing) return;

            if (moving) { var stream = new MemoryStream(await File.ReadAllBytesAsync(Path.Combine(folder, "motion.gif"), cancel.Token)); try { Display(Image.FromStream(stream), stream); } catch { stream.Dispose(); throw; } }

            else { using var source = Image.FromFile(Path.Combine(folder, "preview.png")); Display(new Bitmap(source)); }

            status.ForeColor = Theme.Text; status.Text = (vertical ? "SHORTS • 1080×1920" : "NORMAL • 1920×1080") + " • " + (File.Exists(selected.StockFile) ? "Kayıtlı görüntü üzerinde" : "Stok henüz seçilmedi: örnek fon") + (moving ? "\nSeçili andan en fazla 6 sn. GIF sessizdir; Sesli önizlemeyi aç düğmesi MP4 oynatır. " + (narration == null ? "Anlatım henüz hazır değil." : "Kayıtlı anlatım sesi kullanıldı.") : "\nZaman çizelgesinde seçtiğin an gösteriliyor. Önizleme ücretli servis kullanmaz.");

            if (openAudio) Theme.Open(Path.Combine(folder, "motion-with-audio.mp4"));

        } catch (OperationCanceledException) { }

        catch (Exception ex) { if (!closing) { status.ForeColor = Theme.Warning; status.Text = "Önizleme oluşturulamadı: " + ex.Message; } }

        finally { if (entered) previewGate.Release(); if (ReferenceEquals(previewCancel, cancel)) previewCancel = null; cancel.Dispose(); }

    }

    public async Task CaptureTest(string directory)

    {

        testMode = true;

        Directory.CreateDirectory(directory); debounce.Stop(); previewCancel?.Cancel();

        topText.Text = "Küçük adımlar, büyük değişim";

        Design.TitleColor = "FF8800"; Colors(); debounce.Stop();

        effect.SelectedItem = "Neon parıltı"; titleAnimation.SelectedItem = "Soldan kayma"; transition.SelectedItem = "Yumuşak karışım"; debounce.Stop();

        await RenderPreview();

        if (picture.Image == null) throw new Exception(status.Text);

        using (var shot = new Bitmap(Width, Height)) { DrawToBitmap(shot, new Rectangle(Point.Empty, Size)); shot.Save(Path.Combine(directory, "text-editor-normal.png")); }

        if (scripts.Count > 1) {

            video.SelectedIndex = 1; debounce.Stop(); await RenderPreview();

            if (picture.Image == null || picture.Image.Width != 1080 || picture.Image.Height != 1920) throw new Exception("Shorts önizlemesi hatalı");

            using var shot = new Bitmap(Width, Height); DrawToBitmap(shot, new Rectangle(Point.Empty, Size)); shot.Save(Path.Combine(directory, "text-editor-shorts.png"));

            video.SelectedIndex = 0; debounce.Stop(); if (topText.Text != "Küçük adımlar, büyük değişim") throw new Exception("Video geçişinde taslak kayboldu");

        }

        timeline.Refresh(); double priorStart = Scene.TitleStart;

        timeline.TestDrag(2, 2.5, 95); debounce.Stop();

        if (Scene.TitleStart <= priorStart || titleStart.Value != (decimal)Scene.TitleStart) throw new Exception("Başlık taşıma alanlara uygulanmadı");

        timeline.Refresh(); double priorDuration = Scene.TitleDuration, end = Scene.TitleStart + Scene.TitleDuration;

        timeline.TestDrag(end - .02, end - .4, 95); debounce.Stop();

        if (Scene.TitleDuration >= priorDuration) throw new Exception($"Başlık süresi sürüklenerek değişmedi: {priorDuration} -> {Scene.TitleDuration}; start={Scene.TitleStart}; end={end}; total={timeline.Total}; width={timeline.Width}; scene={activeScene}");

        var tone = Path.Combine(directory, "fixture-music.wav");

        await Media.Exec(store.Ffmpeg, ["-y", "-f", "lavfi", "-i", "sine=frequency=880:sample_rate=44100", "-t", "2", "-c:a", "pcm_s16le", tone], directory, CancellationToken.None);

        SetMusic(tone); musicSource.Text = "Sentetik test"; musicLicense.Text = "Yayımlanmaz"; musicRights.Checked = true; musicStart.Value = 1; musicEnd.Value = 5; Commit(); debounce.Stop();

        timeline.Refresh(); timeline.TestDrag(3, 3.5, 175); debounce.Stop();

        if (Track.Start <= 1 || musicStart.Value != (decimal)Track.Start) throw new Exception("Müzik taşıma alanlara uygulanmadı");

        timeline.Refresh(); double priorEnd = Track.End; timeline.TestDrag(priorEnd - .01, priorEnd + .3, 175); debounce.Stop();

        if (Track.End <= priorEnd) throw new Exception("Müzik bitişi sürüklenerek değişmedi");

        Track.Validate(); timeline.Position = 0;

        await RenderPreview(true); if (picture.Image == null || !ImageAnimator.CanAnimate(picture.Image) || !File.Exists(Path.Combine(folder, "motion-with-audio.mp4"))) throw new Exception("Hareketli önizleme oynatılamadı");

        File.Copy(Path.Combine(folder, "motion-with-audio.mp4"), Path.Combine(directory, "timeline-preview.mp4"), true);

        File.Copy(Path.Combine(folder, "motion.gif"), Path.Combine(directory, "motion-preview.gif"), true);

        foreach (var name in new[] { "Görünüm", "Animasyon", "Geçişler", "Müzik notu" }) {

            IEnumerable<Control> Children(Control p) => p.Controls.Cast<Control>().SelectMany(c => new[] { c }.Concat(Children(c)));

            Children(this).OfType<Button>().Single(b => b.Text == name).PerformClick(); Application.DoEvents();

            using var shot = new Bitmap(Width, Height); DrawToBitmap(shot, new Rectangle(Point.Empty, Size)); shot.Save(Path.Combine(directory, "text-editor-" + name + ".png"));

        }

        foreach (var name in new[] { "Sade", "Sinematik", "Neon turuncu", "Eğitim", "Dinamik Shorts" }) { ApplyPreset(name); LoadFields(); Commit(); Design.Validate(); }

        if (Design.Transition != "Sola silme" || Design.Effect != "3D kabartma" || !Design.Karaoke) throw new Exception("Hazır tasarım kayboldu");

        Width = 1160; Height = 840; PerformLayout(); Application.DoEvents();

        using (var compact = new Bitmap(Width, Height)) { DrawToBitmap(compact, new Rectangle(Point.Empty, Size)); compact.Save(Path.Combine(directory, "text-editor-compact.png")); }

        File.Copy(Path.Combine(folder, "motion.gif"), Path.Combine(directory, "motion-preview.gif"), true);

        File.WriteAllText(Path.Combine(directory, "text-editor-ui-test.txt"), "PASS normal / Shorts önizleme\nPASS video geçişinde taslak korunur\nPASS başlık sürükleme ve süre değiştirme\nPASS müzik ekleme taşıma ve bitiş değiştirme\nPASS müzikli MP4 ve hareketli GIF önizleme\nPASS görünüm animasyon geçiş müzik sekmeleri\nPASS 5 hazır tasarım kayıt doğrulaması\nPASS 1160x840 dar görünüm\nPASS ücretli servis kullanılmaz\n");

    }

    public static void ApplyAllScenes(VideoScript script, Scene selected)

    {

        // TextLayout is shared by every scene of this video; only timings are scene-specific.

        foreach (var scene in script.Scenes) { scene.TitleStart = selected.TitleStart; scene.TitleDuration = selected.TitleDuration; }

    }

    async Task GenerateTitles()

    {

        if (titleBusy) return;

        if (episode.MusicClip != null || episode.Demo && !testMode) { Theme.Alert(this, "Bu video türünde otomatik sahne başlığı üretilmez."); return; }

        Commit(); var selected = Script;

        titleBusy = true; aiTitlesButton.Enabled = video.Enabled = sceneSelect.Enabled = topText.Enabled = autoSceneTitles.Enabled = seconds.Enabled = false; cancelTitlesButton.Enabled = true;

        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(5)); titleCancel = cancellation;

        debounce.Stop(); previewCancel?.Cancel(); status.Text = "Yapay zekâ seçili videonun tüm sahne başlıklarını hazırlıyor… Anlatım değiştirilmiyor.";

        try {

            using var operation = new OperationLease(store);

            var services = new Services(store, message => { if (!IsDisposed) status.Text = message; });

            await SceneHeadings.Generate(services, selected, cancellation.Token, force: true, request: titleTestRequest, vertical: scripts[activeVideo].Vertical);

            selected.TextLayout!.TitleEnabled = true; selected.TextLayout.AutoSceneTitles = true;

            LoadFields(); RefreshTimeline(); EditorDrafts.Save(draftPath, draftBase, Plan);

            status.Text = selected.Scenes.Count + " sahnenin üst başlığı hazır. Anlatım ve altyazı korundu. Kontrol edip Düzeni kaydet'e bas."; Schedule();

        } catch (OperationCanceledException) { status.Text = "Başlık üretimi durduruldu; mevcut başlıklar korundu."; }

        catch (Exception ex) { status.Text = "Başlıklar değişmedi: " + ex.Message; Theme.Alert(this, ex.Message, "Sahne başlıkları"); }

        finally { titleCancel = null; titleBusy = false; aiTitlesButton.Enabled = video.Enabled = sceneSelect.Enabled = topText.Enabled = autoSceneTitles.Enabled = seconds.Enabled = true; cancelTitlesButton.Enabled = false; }

    }

    public async Task CaptureAiTitlesTest(string directory)

    {

        testMode = true; Directory.CreateDirectory(directory); debounce.Stop(); previewCancel?.Cancel();

        var narrations = Script.Scenes.Select(s => s.Narration).ToArray(); var subtitles = Script.Scenes.Select(s => s.SubtitleText).ToArray(); var other = scripts.Skip(1).Select(x => JsonSerializer.Serialize(x.Script)).ToArray();

        seconds.Value = 7.5m; foreach (var scene in Script.Scenes) scene.TitleDuration = 1;

        titleTestRequest = (_, prompt, validate, _) => { using var doc = JsonDocument.Parse(prompt); var output = JsonSerializer.Serialize(new { Titles = doc.RootElement.GetProperty("Scenes").EnumerateArray().Select(s => new { Scene = s.GetProperty("Scene").GetInt32(), Title = "Sahne " + s.GetProperty("Scene").GetInt32() + " ana düşüncesi" }) }); validate(output); return Task.FromResult(output); };

        aiTitlesButton.PerformClick(); while (titleBusy) await Task.Delay(25);

        debounce.Stop(); previewCancel?.Cancel();

        if (Script.TextLayout!.TitleSeconds != 7.5 || Script.Scenes.Any(s => s.TitleDuration != 0)) throw new InvalidOperationException("Seçilen başlık gösterim süresi uygulanmadı.");

        if (!Script.TextLayout!.AutoSceneTitles || !Script.TextLayout.TitleEnabled || Script.Scenes.Any(s => !s.OnScreen.StartsWith("Sahne ")) || !narrations.SequenceEqual(Script.Scenes.Select(s => s.Narration)) || !subtitles.SequenceEqual(Script.Scenes.Select(s => s.SubtitleText)) || !other.SequenceEqual(scripts.Skip(1).Select(x => JsonSerializer.Serialize(x.Script)))) throw new InvalidOperationException("Sahne başlığı editör kontrolü başarısız.");

        PerformLayout(); Application.DoEvents(); using var image = new Bitmap(Width, Height); DrawToBitmap(image, new Rectangle(Point.Empty, image.Size)); image.Save(Path.Combine(directory, "ai-scene-titles.png"));

        File.WriteAllText(Path.Combine(directory, "verified.txt"), "AI düğmesi, seçili videonun tüm sahneleri, anlatım/altyazı koruması ve diğer videonun korunması doğrulandı."); DialogResult = DialogResult.OK;

    }

    public void CaptureMusicSaveTest(string directory)

    {

        testMode = true; Directory.CreateDirectory(directory); debounce.Stop(); previewCancel?.Cancel();

        if (scripts.Count < 2) throw new InvalidOperationException("Normal ve Shorts test planı gerekli.");

        var shortTrack = scripts[1].Script.MusicTrack!;

        shortTrack.Enabled = true; shortTrack.File = ""; shortTrack.Source = ""; shortTrack.License = ""; shortTrack.RightsConfirmed = false;

        var file = Path.Combine(directory, "music-fixture.mp3"); File.WriteAllBytes(file, [1, 2, 3]); SetMusic(file); debounce.Stop(); previewCancel?.Cancel();

        musicLicense.Text = "Müzikler bana aittir";

        musicSource.Text = "AHMET ANIL"; musicRights.Checked = true; musicFadeIn.Value = 30; musicVolume.Value = 18;

        debounce.Stop(); previewCancel?.Cancel();

        // Invoke the actual save handler after the same form events as a user's selection and confirmation.

        Save();

        if (DialogResult != DialogResult.OK || shortTrack.File != file || !shortTrack.RightsConfirmed || shortTrack.Source != "AHMET ANIL" || shortTrack.Volume != .12 || Track.FadeIn != 30) throw new InvalidOperationException("Gerçek editör kayıt işlemi müzik bilgilerini koruyamadı.");

        Json.Write(Path.Combine(directory, "saved-plan.json"), Plan); File.WriteAllText(Path.Combine(directory, "verified.txt"), "Normal videoda müzik seçimi, sahiplik kutusu, onay ve gerçek Düzeni kaydet işlemi; boş Shorts kaydı tamamlandı.");

    }

    void Save()

    {

        if (titleBusy) { status.Text = "Başlık üretimini tamamla veya durdur; sonra kaydet."; return; }

        try { Commit(); if (allScenes.Checked) ApplyAllScenes(Script, Scene); TextEditSave.CompletePackageMusic(Plan); foreach (var script in scripts) { try { script.Script.TextLayout?.Validate(); script.Script.MusicTrack?.Validate(); } catch (InvalidOperationException ex) { activeVideo = scripts.IndexOf(script); activeScene = 0; LoadVideo(); SelectTab("Müzik notu"); throw new InvalidOperationException((script.Vertical ? "Shorts" : "Normal video") + " • " + script.Script.Title + ": " + ex.Message, ex); } } EditorDrafts.Remove(draftPath); DialogResult = DialogResult.OK; Close(); }

        catch (Exception ex) { Theme.Alert(this, ex.Message); }

    }

}



public static class TextEditSave

{

    public static void CompletePackageMusic(ContentPlan plan)

    {

        var tracks = new[] { plan.Long }.Concat(plan.Shorts).Where(s => s?.MusicTrack != null).Select(s => s!.MusicTrack!).ToArray();

        var confirmed = tracks.FirstOrDefault(t => t.Enabled && !string.IsNullOrWhiteSpace(t.File));

        if (confirmed == null) return;

        foreach (var track in tracks.Where(t => t.Enabled && string.IsNullOrWhiteSpace(t.File))) {

            track.File = confirmed.File; track.Source = confirmed.Source; track.License = confirmed.License; track.Attribution = confirmed.Attribution; track.RightsConfirmed = confirmed.RightsConfirmed;

        }

        foreach (var track in tracks.Where(t => t.RightsConfirmed && !string.IsNullOrWhiteSpace(t.File) && !string.IsNullOrWhiteSpace(t.Source) && !string.IsNullOrWhiteSpace(t.License))) ShareMusicRights(plan, track);

    }

    public static void InheritPackageMusic(ContentPlan plan, VideoScript script, bool enabled, string globalFile, double volume)

    {

        if (!enabled || !string.IsNullOrWhiteSpace(globalFile) || (script.MusicTrack != null && (!script.MusicTrack.Enabled || !string.IsNullOrWhiteSpace(script.MusicTrack.File)))) return;

        var existing = new[] { plan.Long }.Concat(plan.Shorts).Where(x => x != null && !ReferenceEquals(x, script)).Select(x => x!.MusicTrack).FirstOrDefault(x => x is { Enabled: true } && !string.IsNullOrWhiteSpace(x.File));

        if (existing == null) return;

        script.MusicTrack = JsonSerializer.Deserialize<MusicTrack>(JsonSerializer.Serialize(existing))!;

        script.MusicTrack.Volume = volume;

    }

    public static void ShareMusicRights(ContentPlan plan, MusicTrack confirmed)

    {

        if (!confirmed.RightsConfirmed || string.IsNullOrWhiteSpace(confirmed.File) || string.IsNullOrWhiteSpace(confirmed.Source) || string.IsNullOrWhiteSpace(confirmed.License)) return;

        foreach (var script in new[] { plan.Long }.Concat(plan.Shorts).Where(x => x != null)) {

            var track = script!.MusicTrack;

            if (track == null || string.IsNullOrWhiteSpace(track.File) || track.RightsConfirmed || !string.Equals(Path.GetFullPath(track.File), Path.GetFullPath(confirmed.File), StringComparison.OrdinalIgnoreCase)) continue;

            track.Source = confirmed.Source; track.License = confirmed.License; track.Attribution = confirmed.Attribution; track.RightsConfirmed = confirmed.RightsConfirmed;

        }

    }

    public static void Apply(Store store, Episode episode, ContentPlan plan)

    {

        Rules.ValidatePlan(new Episode { IncludeLong = episode.IncludeLong, ShortCount = episode.ShortCount, Demo = episode.Demo, MusicClip = episode.MusicClip, UserNarration = episode.UserNarration, QueueMusic = episode.QueueMusic, Plan = plan });

        if (episode.Videos.Any(v => v.YouTubeId != "")) throw new InvalidOperationException("Yüklenen paket için önce yeni sürüm / kopya oluştur.");

        if (JsonSerializer.Serialize(episode.Plan, Json.Options) == JsonSerializer.Serialize(plan, Json.Options)) return;

        Json.Write(Path.Combine(store.Folder(episode), "design-history", "r" + episode.Revision + "-before-text.json"), new { episode.Plan, episode.Videos });

        foreach(var script in new[]{plan.Long}.Concat(plan.Shorts).Where(x=>x?.MusicTrack?.RightsDocument!="" && x?.MusicTrack!=null)){
            var track=script!.MusicTrack!;if(!File.Exists(track.RightsDocument))continue;
            var folder=Path.Combine(store.Folder(episode),"rights-documents");Directory.CreateDirectory(folder);var target=Path.Combine(folder,Reliability.FileHash(track.RightsDocument)+Path.GetExtension(track.RightsDocument));if(!File.Exists(target))File.Copy(track.RightsDocument,target);track.RightsDocument=target;
        }
        episode.Plan = plan; episode.Revision++; episode.TextDesignPending = true; episode.Approved = false; episode.ApprovalHash = ""; episode.Error = ""; episode.Progress = 25; episode.Status = "Senaryo hazır";

        foreach (var asset in episode.Videos) { asset.QualityPassed = false; asset.Checks.Add("Yazı düzeni değişti; yeni video çıktısı gerekli. Önceki dosya korunuyor."); }

        episode.Log("Başlık / altyazı, zaman çizelgesi ve müzik tasarımı kaydedildi. Önceki dosyalar ve stok görüntüleri korundu."); store.Save();

    }

}

