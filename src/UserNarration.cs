using System.Text.Json;

namespace AnkaStudio;



public sealed class UserNarrationOptions

{

    public Dictionary<string,List<string>> TextVersions {get;set;}=[];
    public bool Enabled { get; set; }

    public string LongText { get; set; } = "";

    public List<string> Shorts { get; set; } = [];

    public string VisualTheme { get; set; } = "doğa manzarası; orman; gün batımı";

    public bool ManualVisuals { get; set; }

    public List<string> LocalVideos { get; set; } = [];

    public List<ManualVideoSelection> ShortVideos { get; set; } = [];

    public string AppliedVideoSelection { get; set; } = "";

    public bool LocalVideoRightsConfirmed { get; set; }

}

public sealed class ManualVideoSelection

{

    public bool UseSeparate { get; set; }

    public List<string> Files { get; set; } = [];

}

public static class UserNarration

{

    public static bool Active(Episode e) => e.UserNarration?.Enabled == true;

    public static void Validate(Episode e)

    {

        if (!Active(e)) return;

        if (e.MusicClip != null) throw new InvalidOperationException("Müzik klibinde hazır anlatım metni kullanılmaz.");

        var own = e.UserNarration!;

        if (!own.ManualVisuals && (string.IsNullOrWhiteSpace(own.VisualTheme) || own.VisualTheme.Length > 1000)) throw new InvalidOperationException("Hazır metnin stok görüntü temasını gir.");

        ManualVideos.Validate(e);

        var texts = (e.IncludeLong ? new[] { own.LongText } : Array.Empty<string>()).Concat(own.Shorts.Take(e.ShortCount)).ToArray();

        if (own.Shorts.Count < e.ShortCount || texts.Any(t => string.IsNullOrWhiteSpace(t) || t.Length > 30000)) throw new InvalidOperationException("Üretilecek her video için hazır metin gir (en fazla 30.000 karakter). Metin editörünü aç.");

        foreach (var text in texts) if (ScriptLength.Count(text) > 3900) throw new InvalidOperationException("Hazır metin yaklaşık 30 dakikayı aşıyor; ayrı videolara böl.");

        if (own.Shorts.Take(e.ShortCount).Any(t => ScriptLength.Count(t) > 360)) throw new InvalidOperationException("Shorts metni çok uzun; en fazla 360 kelime kullan. Gerçek ses süresi de üretimde denetlenir.");

    }

    public static List<string> Scenes(string text)

    {

        var result = new List<string>(); int start = 0;

        while (start < text.Length) {

            int end = Math.Min(start + 700, text.Length);

            if (end < text.Length) { int cut = text.LastIndexOfAny([' ', '\n', '\t'], end - 1, end - start); if (cut > start) end = cut + 1; }

            var chunk = text[start..end];

            if (string.IsNullOrWhiteSpace(chunk) && result.Count > 0) result[^1] += chunk; else result.Add(chunk);

            start = end;

        }

        return result;

    }

    public static ContentPlan Plan(Episode e)

    {

        Validate(e); var own = e.UserNarration!;

        var queries = own.ManualVisuals ? new[] { "Kullanıcının seçtiği videolar" } : own.VisualTheme.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(q => StockSearch.Normalize(StockSearch.Translate(q))).Where(q => q != "").ToArray();

        if (queries.Length == 0) throw new InvalidOperationException("Stok görüntü teması geçersiz.");

        VideoScript Script(string text, string title) => new() { Title = title.Length > 100 ? title[..100] : title, ThumbnailText = e.Topic.Length > 100 ? e.Topic[..100] : e.Topic, Description = e.Topic, Tags = [],

            Scenes = Scenes(text).Select((chunk, index) => new Scene { Narration = chunk, VisualQuery = queries[index % queries.Length], Duration = Math.Max(1, ScriptLength.Count(chunk) * 60.0 / 130) }).ToList() };

        var plan = new ContentPlan { Long = e.IncludeLong ? Script(own.LongText, e.Topic) : null, Shorts = own.Shorts.Take(e.ShortCount).Select((t, i) => Script(t, e.Topic + " • Shorts " + (i + 1))).ToList(), Research = "Kullanıcının hazır metni. Yapay zekâ ile içerik üretimi ve otomatik metin düzeltmesi uygulanmadı." };

        ManualVideos.Apply(e, plan); return plan;

    }

}

public sealed class UserNarrationEditor : Form

{

    protected override void OnShown(EventArgs e) {ModernScrollbars.Apply(this);base.OnShown(e);}

    public UserNarrationOptions Value { get; private set; }

    public UserNarrationEditor(UserNarrationOptions original, bool normal, int shortCount, Store? store = null)

    {

        Value = JsonSerializer.Deserialize<UserNarrationOptions>(JsonSerializer.Serialize(original))!;

        Text = "Hazır içerik metni • yaz / yapıştır / dosyadan yükle"; Width = 900; Height = 750; StartPosition = FormStartPosition.CenterParent; BackColor = Theme.Bg; Padding = new Padding(20);

        var tabs = new StudioTabs { Dock = DockStyle.Fill }; var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 65 }; var intro = Theme.Label("Metnin aynen seslendirilir. Yapay zekâ yeniden yazmaz. Süre okuma hızına bağlıdır. Her videonun metnini ayrı gir.", 10, width: 830, height: 60); intro.Dock = DockStyle.Top;

        Controls.Add(tabs); Controls.Add(bottom); Controls.Add(intro);

        var editors = new List<(TextBox Box, bool Normal, int Index)>();

        void Add(string name, string text, bool isNormal, int index) {

            var tab = new TabPage(name) { BackColor = Theme.Bg }; var box = Theme.Input(text, true, 800); box.Dock = DockStyle.Fill; box.AcceptsReturn = true; box.AcceptsTab = true; box.MaxLength = 30000; box.ScrollBars = ScrollBars.Vertical; box.Name = "OwnNarrationText";

            var tools = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 70 }; var count = Theme.Label("", 10, width: 330, height: 45);

            void Update() { int words = ScriptLength.Count(box.Text); count.Text = $"{words} kelime • yaklaşık {Math.Ceiling(words * 60.0 / 130):0} sn"; }

            string versionKey=isNormal?"long":"short-"+index;
            var versions=Value.TextVersions.TryGetValue(versionKey,out var savedVersions)?savedVersions.TakeLast(20).ToList():new List<string>();
            if(versions.Count==0 || versions[^1]!=box.Text)versions.Add(box.Text);Value.TextVersions[versionKey]=versions;

            box.Leave+=(_,_)=>{if(versions[^1]!=box.Text){versions.Add(box.Text);if(versions.Count>20)versions.RemoveAt(0);}};

            box.TextChanged += (_, _) => Update(); Update();

            tools.Controls.Add(Theme.Button("TXT dosyasından yükle", () => { using var picker = new OpenFileDialog { Filter = "Metin dosyası (*.txt)|*.txt", CheckFileExists = true }; if (picker.ShowDialog(this) != DialogResult.OK) return; try { box.Text = ReadText(picker.FileName); } catch (Exception ex) { Theme.Alert(this, ex.Message); } }, false, 220)); tools.Controls.Add(Theme.Button("Kopyala", () => box.Copy(), false, 95)); tools.Controls.Add(Theme.Button("Yapıştır", () => { try { box.Paste(); } catch (Exception ex) { Theme.Alert(this, ex.Message); } }, false, 95)); tools.Controls.Add(Theme.Button("Önceki metin",()=>{if(versions.Count>1){box.Text=versions[^2];versions.RemoveAt(versions.Count-1);}},false,130));

            if(store!=null)tools.Controls.Add(Theme.Button("Telaffuz önizlemesi",()=>Theme.Alert(this,NarrationFlow.SpokenText(box.Text[..Math.Min(1000,box.Text.Length)],store.Settings)),false,190));

            if(store!=null)tools.Controls.Add(Theme.Button("Kısa ses denemesi",async()=>{try{using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(2));string path=await SpeechSample.Create(store,box.Text,store.Settings,timeout.Token);Theme.Open(path);}catch(Exception ex){Theme.Alert(this,ex.Message);}},false,190));

            tools.WrapContents=true;tools.Height=165;tools.Controls.Add(count);

            tab.Controls.Add(box); tab.Controls.Add(tools); tabs.TabPages.Add(tab); editors.Add((box, isNormal, index));

        }

        if (normal) Add("Normal video metni", Value.LongText, true, 0);

        for (int i = 0; i < shortCount; i++) Add("Shorts " + (i + 1), Value.Shorts.ElementAtOrDefault(i) ?? "", false, i);

        var visuals = new TabPage("Stok görüntü teması") { BackColor = Theme.Bg };

        var visualPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(12) };

        var manual = new CheckBox { Text = "Yalnızca eklediğim videoları kullan • otomatik stok araması kapalı", Checked = Value.ManualVisuals, Width = 790, Height = 36, ForeColor = Theme.Text };

        var hint = Theme.Label("Normal video: 16:9 yatay • Shorts: 9:16 dikey. Her video için aşağıdaki sekmeden görüntü seç. Yanlış formatlı dosyalar kabul edilmez. Liste bitince baştan döner; kısa görüntüler sahne boyunca tekrarlanır. Başka kaynaktan video aranmaz.", 10, width: 790, height: 75);

        var videoTabs = new StudioTabs { Width = 790, Height = 350 };

        var shortLists = new List<(CheckBox Separate, ListBox Files)>();

        ListBox AddVideoList(string name, IEnumerable<string> originalFiles, ManualVideoSelection? selection = null) {

            var tab = new TabPage(name) { BackColor = Theme.Bg };

            var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(8) };

            var separate = new CheckBox { Text = "Bu Shorts için ayrı videolar seç • 9:16 dikey zorunlu", Checked = selection?.UseSeparate == true, Width = 740, Height = 32, ForeColor = Theme.Text };

            if (selection != null) panel.Controls.Add(separate);

            var files = new ListBox { Width = 740, Height = 150, BackColor = Theme.Panel, ForeColor = Theme.Text, HorizontalScrollbar = true, BorderStyle = BorderStyle.None };

            foreach (var file in originalFiles) files.Items.Add(file);

            var videoTools = new FlowLayoutPanel { Width = 740, Height = 95, WrapContents = true };

            videoTools.Controls.Add(Theme.Button("+ Videolarımı ekle", async () => { using var picker = new OpenFileDialog { Title = name + " için videoları seç", Filter = "Video|*.mp4;*.mov;*.mkv;*.webm", Multiselect = true, CheckFileExists = true, RestoreDirectory = true }; if (picker.ShowDialog(this) != DialogResult.OK) return; try { if(store==null)throw new InvalidOperationException("Video motoru bulunamadı; editörü proje ayarlarından aç."); foreach(var file in picker.FileNames)await MediaFormat.Video(store,file,selection!=null || !normal,CancellationToken.None); }catch(Exception ex){if(!IsDisposed)Theme.Alert(this,ex.Message);return;} if(IsDisposed)return; foreach (var file in picker.FileNames) if (!files.Items.Cast<string>().Contains(file, StringComparer.OrdinalIgnoreCase)) files.Items.Add(file); if (selection != null) separate.Checked = true; manual.Checked = true; }, true, 240));

            videoTools.Controls.Add(Theme.Button("Seçileni kaldır", () => { if (files.SelectedIndex >= 0) files.Items.RemoveAt(files.SelectedIndex); }, false, 170));

            void Move(int delta) { var index = files.SelectedIndex; var target = index + delta; if (index < 0 || target < 0 || target >= files.Items.Count) return; var file = files.Items[index]; files.Items.RemoveAt(index); files.Items.Insert(target, file); files.SelectedIndex = target; }

            videoTools.Controls.Add(Theme.Button("Yukarı taşı", () => Move(-1), false, 150)); videoTools.Controls.Add(Theme.Button("Aşağı taşı", () => Move(1), false, 150));

            if (selection != null) {

                var shared = Theme.Label("Shorts için 9:16 videolar seç. Ortak liste yalnızca tamamı 9:16 ise kullanılabilir.", 9, width: 740, height: 35);

                panel.Controls.Add(shared); shortLists.Add((separate, files));

                void SeparateMode() { files.Enabled = separate.Checked; shared.Visible = !separate.Checked; }

                separate.CheckedChanged += (_, _) => SeparateMode(); SeparateMode();

            }

            panel.Controls.Add(files); panel.Controls.Add(videoTools); tab.Controls.Add(panel); videoTabs.TabPages.Add(tab); return files;

        }

        var commonFiles = AddVideoList(normal ? "Normal Video • 16:9" : "Ortak Görüntüler", Value.LocalVideos);

        for (int i = 0; i < shortCount; i++) { var selection = Value.ShortVideos.ElementAtOrDefault(i) ?? new ManualVideoSelection(); AddVideoList("Shorts " + (i + 1) + " • 9:16", selection.Files, selection); }

        var rights = new CheckBox { Text = "Eklediğim videoları kullanma hakkımı doğruladım", Checked = Value.LocalVideoRightsConfirmed, Width = 790, Height = 36, ForeColor = Theme.Text };

        var theme = Theme.Input(Value.VisualTheme, true, 790); theme.Height = 100;

        var searchHint = Theme.Label("Otomatik arama teması • örnek: doğa manzarası; orman; gün batımı. Manuel modda kullanılmaz.", 10, width: 790, height: 45);

        void Mode() { theme.Enabled = !manual.Checked; searchHint.Enabled = !manual.Checked; }

        manual.CheckedChanged += (_, _) => Mode(); Mode();

        visualPanel.Controls.AddRange([manual, hint, videoTabs, rights, searchHint, theme]); visuals.Controls.Add(visualPanel); tabs.TabPages.Add(visuals);

        bottom.Controls.Add(Theme.Button("Kaydet", async () => { Value.ManualVisuals = manual.Checked; Value.LocalVideos = commonFiles.Items.Cast<string>().ToList(); Value.ShortVideos = shortLists.Select(s => new ManualVideoSelection { UseSeparate = s.Separate.Checked, Files = s.Files.Items.Cast<string>().ToList() }).ToList(); Value.LocalVideoRightsConfirmed = rights.Checked; try { var check=new Episode { UserNarration = Value, IncludeLong = normal, ShortCount = shortCount }; ManualVideos.Validate(check); if(store!=null)await MediaFormat.Manual(store,check,CancellationToken.None); } catch (Exception ex) { Theme.Alert(this, ex.Message); return; } foreach (var (box, isNormal, index) in editors) {var key=isNormal?"long":"short-"+index;var versions=Value.TextVersions[key];if(versions.Count==0 || versions[^1]!=box.Text)versions.Add(box.Text);Value.TextVersions[key]=versions.TakeLast(20).ToList(); if (isNormal) Value.LongText = box.Text; else { while (Value.Shorts.Count <= index) Value.Shorts.Add(""); Value.Shorts[index] = box.Text; } } Value.VisualTheme = theme.Text.Trim(); DialogResult = DialogResult.OK; Close(); }, true, 220));

        bottom.Controls.Add(Theme.Button("Vazgeç", () => { DialogResult = DialogResult.Cancel; Close(); }));

    }

    public static string ReadText(string path) { if (new FileInfo(path).Length > 150000) throw new InvalidOperationException("TXT dosyası çok büyük (en fazla 150 KB)."); var text = File.ReadAllText(path, new System.Text.UTF8Encoding(false, true)); if (text.Length > 30000) throw new InvalidOperationException("Metin en fazla 30.000 karakter olmalı."); return text; }

}

