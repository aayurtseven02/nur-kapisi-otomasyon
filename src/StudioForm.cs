using System.Diagnostics;

using System.Drawing.Drawing2D;

using System.Globalization;

using System.Text.Json;



namespace AnkaStudio;



public sealed partial class StudioForm : Form

{

    static string ReleaseVersion => typeof(StudioForm).Assembly.GetName().Version?.ToString(3) ?? "unknown";

    Action? refreshRecent;

    readonly StudioHeader appHeader = new() { Dock = DockStyle.Top };

    readonly Dictionary<string, Action> packageUpdates = [];

    string packageShape = "";

    int refreshQueued;

    static IEnumerable<Control> CardChildren(Control parent) { foreach (Control child in parent.Controls) { yield return child; foreach (var nested in CardChildren(child)) yield return nested; } }

    string PackageShape() => string.Join("|", store.Episodes.OrderByDescending(x => x.Created).Select(e => e.Id + ":" + (e.Plan != null) + ":" + (e.Error != "") + ":" + PackageArchive.Group(e) + ":" + string.Join(',', e.Videos.Select(v => v.YouTubeState))));

    readonly HashSet<string> openArchiveGroups = [];

    readonly HashSet<string> openArchiveEditors = [];

    readonly Store store;

    readonly Engine engine;
    readonly AssistantActions assistantActions;
    Action? commitAssistantEditor;

    readonly Panel body = new StudioGradientPanel() { Dock = DockStyle.Fill, BackColor = Theme.Bg, Padding = new Padding(28, 18, 24, 16) };

    readonly FlowLayoutPanel page = Theme.Flow();

    readonly Label channelHeading = Theme.Label("", 13, true, Theme.Teal, 188, 34);

    readonly Label footer = Theme.Label("", 9, color: Theme.Muted, width: 190, height: 100);

    readonly Dictionary<string, Button> nav = [];

    string current = "Stüdyo";

    readonly HashSet<string> openPackageLogs = [];

    readonly ToolTip hints = new() { AutoPopDelay = 10000 };

    readonly System.Windows.Forms.Timer ticker = new() { Interval = 4000 };

    bool autoRunning;

    bool remoteRefreshing;

    string remoteSummary = "YouTube durumu henüz alınmadı.";

    ChatAssistant? assistant;

    readonly List<(Control Control, Rectangle Bounds, int ParentWidth)> responsive = [];

    public StudioForm(Store store)

    {

        this.store = store; engine = new Engine(store); engine.ReviewStoryImage = ReviewStoryPicture; UiSound.Configure(store.Settings);
        assistantActions=new AssistantActions(store,engine,()=>store.Episodes.FirstOrDefault(e=>e.Id==selectedWorkspaceId),Navigate){FlushEditor=()=>commitAssistantEditor?.Invoke()};
        assistantActions.SettingsChanged+=()=>{if(IsHandleCreated&&!IsDisposed)BeginInvoke(()=>{if(!IsDisposed)RefreshCurrentPage();});};

        Shown += async (_, _) => { if (!Environment.GetCommandLineArgs().Any(a => a is "--worker" or "--screenshot" || a.StartsWith("--")&&(a.Contains("test")||a.Contains("preview")))) { VideoQueue.Reconcile(store); _ = NvidiaModels.Start(store); await RefreshChannelName(); await RefreshRemote();if(store.Settings.SetupCompleted!=true&&store.Episodes.Count==0&&store.Settings.YouTubeChannelId=="")ShowWelcomeSetup(); } };

        FormClosed += (_, _) => { NvidiaModels.Cancel(store); hints.Dispose(); ticker.Dispose(); assistant?.Dispose(); Icon?.Dispose(); };

        page.AutoSize = false;

        Text = "TubeFlow " + ReleaseVersion + " • YouTube Üretim ve Yönetim Merkezi"; Width = Math.Min(1440, Screen.PrimaryScreen!.WorkingArea.Width - 32); Height = Math.Min(950, Screen.PrimaryScreen!.WorkingArea.Height - 32); MinimumSize = new(900, 600); StartPosition = FormStartPosition.CenterScreen; BackColor = Theme.Bg; Font = Theme.Font(); ForeColor = Theme.Text; AutoScaleMode = AutoScaleMode.Dpi;

        using var iconStream = typeof(StudioForm).Assembly.GetManifestResourceStream("AnkaStudio.Assets.anka-studio.ico");

        if (iconStream != null) { using var appIcon = new Icon(iconStream); Icon = (Icon)appIcon.Clone(); }

        var side = new Panel { Dock = DockStyle.Left, Width = 268, BackColor = Theme.Sidebar, Padding = new Padding(0, 20, 0, 8) };

        using var logoStream=typeof(StudioForm).Assembly.GetManifestResourceStream("AnkaStudio.Assets.tubeflow-dark.png");using var logoSource=Image.FromStream(logoStream!);var brand=new PictureBox{Image=Icon?.ToBitmap() ?? new Bitmap(logoSource),Location=new(14,10),Size=new(56,56),SizeMode=PictureBoxSizeMode.Zoom,AccessibleName="TubeFlow logosu"};side.Controls.Add(brand);var brandTitle=Theme.Label("TubeFlow",17,true,width:170,height:48);brandTitle.Location=new(78,15);side.Controls.Add(brandTitle);var sectionLabel=Theme.Label("Çalışma alanı",10,true,Theme.Muted,240,32);sectionLabel.Location=new(16,92);side.Controls.Add(sectionLabel);FormClosed+=(_,_)=>brand.Image?.Dispose();

        channelHeading.Location = new(20, 6); channelHeading.AutoEllipsis = true; channelHeading.Visible = false;

        var subtitle = Theme.Label("Versiyon: " + ReleaseVersion, 9, true, Theme.Warning, 188, 26);
        subtitle.TextAlign = ContentAlignment.MiddleCenter;
        subtitle.Dock = DockStyle.Bottom;

        var navigation = new StudioScrollPanel { Location = new(8, 132), Size = new(252, 220), Anchor = AnchorStyles.Top | AnchorStyles.Left, AutoScroll = true }; side.Controls.Add(navigation);

        var items = new[] { (Route: "Video hazırla", Label: "Stüdyo • Üret"), (Route: "Videolarım", Label: "Projelerim"), (Route: "Ayarlar", Label: "Merkez") };

        int y=0;

        foreach(var item in items) {

            var button=new SidebarButton{Text=item.Label,Width=248,Glyph=item.Route=="Video hazırla"?"＋":item.Route=="Videolarım"?"◷":"⚙",Location=new Point(0,y),AccessibleName="Ana bölüm: "+item.Label};button.Click+=(_,_)=>Navigate(item.Route);navigation.Controls.Add(button);nav[item.Route]=button;y+=42;

        }

        var guide=new SidebarButton{Text="Rehber",Glyph="?",Width=248};guide.Click+=(_,_)=>UserGuide.Show(this);guide.Location=new(0,y+12);navigation.Controls.Add(guide);

        var recent=new StudioScrollPanel { Location=new Point(8,380), Width=252, Height=180, AutoScroll=true, BackColor=Theme.Sidebar, AccessibleName="Son projeler" };side.Controls.Add(recent);
        refreshRecent=()=>{
            foreach(Control child in recent.Controls.Cast<Control>().ToArray()) child.Dispose();
            var heading=Theme.Label("Son projeler",10,true,Theme.Muted,240,30);heading.Location=new Point(8,0);recent.Controls.Add(heading);
            int row=38;foreach(var episode in store.Episodes.TakeLast(6).Reverse()) {
                var target=episode;var link=new SidebarButton {Text=episode.Topic,Glyph="",Location=new Point(0,row),Width=248,Height=36,Active=selectedWorkspaceId==episode.Id,AccessibleName="Projeyi aç: "+episode.Topic};
                link.Click+=(_,_)=>{selectedWorkspaceId=target.Id;wizardDraft=null;Navigate("Video hazırla");};recent.Controls.Add(link);row+=38;
            }
            if(row==38){var empty=Theme.Label("Henüz proje yok",10,color:Theme.Muted,width:240,height:30);empty.Location=new Point(8,row);recent.Controls.Add(empty);}
        };
        side.SizeChanged+=(_,_)=>{recent.Height=Math.Max(0,side.ClientSize.Height-500);recent.Visible=side.Width>70&&recent.Height>60;};

        var sideFooter = new Panel { Dock = DockStyle.Bottom, Height = 100, BackColor = side.BackColor, Padding=new Padding(8,4,8,4) };
        footer.Visible=false; sideFooter.Controls.Add(subtitle); side.Controls.Add(sideFooter);

bool compact=false;var chat=new SidebarButton{Text="Sohbet",Glyph="💬",Width=248,Height=42,Dock=DockStyle.Top,AccessibleName="TubeFlow sohbet yardımcısını aç"};chat.Click+=(_,_)=>ToggleAssistant();sideFooter.Controls.Add(chat);sideFooter.BringToFront();hints.SetToolTip(chat,"Sohbeti aç / kapat");
        var scroll = new StudioScrollPanel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.Transparent }; page.Location = Point.Empty; scroll.Controls.Add(page); body.Controls.Add(scroll);
        var collapse=new SidebarButton{Text="",Glyph="☰",Width=36};collapse.AccessibleName="Yan menüyü aç veya kapat";collapse.Location=new Point(8,4);var rail=new Panel{Dock=DockStyle.Left,Width=56,BackColor=Theme.Shell,Padding=new Padding(4,12,4,8)};rail.Controls.Add(collapse);collapse.Location=new Point(5,8);collapse.Width=46;collapse.Height=42;collapse.BringToFront();int railY=66;foreach(var item in items){string route=item.Route;var shortcut=new SidebarButton{Glyph=route=="Video hazırla"?"⌂":route=="Videolarım"?"▤":"⚙",Location=new Point(5,railY),Width=46,Height=42,Tag="rail:"+route,AccessibleName="Hızlı bölüm: "+item.Label};shortcut.Click+=(_,_)=>Navigate(route);rail.Controls.Add(shortcut);railY+=54;}var railChat=new SidebarButton{Glyph="💬",Dock=DockStyle.Bottom,Height=44,AccessibleName="Hızlı sohbet"};railChat.Click+=(_,_)=>ToggleAssistant();rail.Controls.Add(railChat);collapse.Click+=(_,_)=>{compact=!compact;side.Width=compact?0:268;brand.Visible=!compact;brandTitle.Visible=!compact;sectionLabel.Visible=!compact;sideFooter.Height=compact?60:100;subtitle.Visible=!compact;chat.Text=compact?"":"Sohbet";navigation.Width=compact?56:252;foreach(var (route,b) in nav){b.Width=compact?46:248;b.Text=compact?(route=="Video hazırla"?"＋":route=="Videolarım"?"▤":"⚙"):items.First(i=>i.Route==route).Label;}guide.Visible=!compact;Navigate(current);};
        var sidebarDivider = new Panel { Dock = DockStyle.Left, Width = 1, BackColor = Theme.Border, AccessibleName = "Sol menü ayırıcı çizgisi" };
        var topDivider = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Theme.Border, AccessibleName = "Üst kenar çizgisi" };
        Controls.Add(body); Controls.Add(appHeader); Controls.Add(sidebarDivider); Controls.Add(side); Controls.Add(rail); Controls.Add(topDivider);

        HandleCreated += (_, _) => Shapes.DarkTitle(Handle);

        Resize += (_, _) => Reflow();

        engine.Changed += () => { if (IsHandleCreated && !IsDisposed && Interlocked.Exchange(ref refreshQueued, 1) == 0) BeginInvoke(() => { Interlocked.Exchange(ref refreshQueued, 0); if (IsDisposed) return; UpdateFooter(); appHeader.Busy=engine.Busy; appHeader.Invalidate(); if (current is "İçerik paketleri" or "Video hazırla" && packageShape == PackageShape()) { foreach (var update in packageUpdates.Values) update(); } else if (current is "İçerik paketleri" or "Video hazırla" or "Videolarım" or "İşlem kayıtları" or "Üretim ve yayın planı" or "Stüdyo") RefreshCurrentPage(); }); };

        ticker.Tick += async (_, _) => await Tick(); ticker.Start();

        bool closing = false;

        FormClosing += async (_, e) => { if (closing) return;

            if (queueRunning) { foreach (var item in VideoQueue.Items(store)) item.QueueAuthorized = false; queueCancellation?.Cancel(); }

            if (engine.Busy) { e.Cancel = true; if (Theme.Alert("Çalışan işi durdurup çıkılsın mı? Tamamlanan aşamalar saklanır.", "TubeFlow", MessageBoxButtons.YesNo) != DialogResult.Yes) return; ticker.Stop(); await engine.StopAndWait(); closing = true; Close(); }

            else { ticker.Stop(); store.Save(); }

        };

        Shown += (_, _) => { while (Json.RecoveryMessages.TryDequeue(out var recovery)) Message(recovery); };

        Navigate("Video hazırla");



    }

    void RememberLayout(Control parent)

    {

        foreach (Control child in parent.Controls) { responsive.Add((child, child.Bounds, Math.Max(1, parent.Width))); RememberLayout(child); }

    }

    void Reflow()

    {

        if (responsive.Count == 0) return;

        page.SuspendLayout();

        page.Width = Math.Max(620, body.ClientSize.Width - 90 - (assistantHost?.Visible==true && ClientSize.Width>=1720 ? assistantWidth : 0));

        foreach (var item in responsive)

        {

            var control = item.Control; if (control.IsDisposed || control.Parent == null) continue;

            if (control.Dock == DockStyle.None && control.Parent is not TableLayoutPanel)

            {

                var ratio = (double)control.Parent.Width / item.ParentWidth;

                control.Width = Math.Max(control is Button && control.Parent is FlowLayoutPanel && !Equals(control.Parent.Tag, "grid3") ? item.Bounds.Width : 24, (int)Math.Round(item.Bounds.Width * ratio));

                if (control.Parent is not FlowLayoutPanel) control.Left = (int)Math.Round(item.Bounds.X * ratio);

            }

            if(Equals(control.Tag,"settings-container"))control.Height=Math.Max(180,body.ClientSize.Height-body.Padding.Vertical-page.Controls.Cast<Control>().Where(c=>c!=control).Sum(c=>c.Height+c.Margin.Vertical)-36);

            if(Equals(control.Tag,"package-tool") && control.Parent!=null)control.Width=Math.Max(170,(control.Parent.Width-12*control.Parent.Controls.Count)/control.Parent.Controls.Count);

            if(Equals(control.Tag,"package-action")) {control.Width=300;control.Height=46;}

            control.Invalidate();

        }

        foreach (var grid in responsive.Select(x => x.Control).OfType<FlowLayoutPanel>()) {

            if(Equals(grid.Tag,"phase-tabs")) {foreach(Label arrow in grid.Controls.OfType<Label>())arrow.Width=24;int count=grid.Controls.OfType<Button>().Count();int reserved=grid.Controls.OfType<Label>().Count()*24+grid.Controls.OfType<Button>().Sum(b=>b.Margin.Horizontal);foreach(Button button in grid.Controls.OfType<Button>())button.Width=Math.Max(80,(grid.Width-reserved)/Math.Max(1,count));continue;}

            int columns=Equals(grid.Tag,"grid3")?3:Equals(grid.Tag,"grid2")?2:Equals(grid.Tag,"equal-tabs")?grid.Controls.Count:0;

            if(columns>0)foreach(Button button in grid.Controls)button.Width=Math.Max(80,(grid.Width-columns*12)/columns);

        }

        var compactHeaderTab=responsive.Select(x=>x.Control).OfType<Button>().FirstOrDefault(b=>!b.IsDisposed&&b.AccessibleName=="Bölüm: Hesaplar ve AI");
        if(compactHeaderTab!=null)foreach(var action in responsive.Select(x=>x.Control).OfType<StudioButton>().Where(b=>!b.IsDisposed&&Equals(b.Tag,"connection-action")&&b.Parent!=null)){action.Width=Math.Min(compactHeaderTab.Width,Math.Max(100,action.Parent!.ClientSize.Width-action.Parent.Padding.Horizontal));action.Height=48;}
        if(compactHeaderTab!=null)foreach(var section in responsive.Select(x=>x.Control).OfType<StudioDisclosure>().Where(x=>!x.IsDisposed))section.LimitHeadingWidth(compactHeaderTab.Width);
        page.ResumeLayout(true);

        page.Height = page.Controls.Cast<Control>().Sum(c => c.Height + c.Margin.Vertical) + 24;

        appHeader.Invalidate(); body.Invalidate(true);

    }

    void UpdateFooter() { footer.Text = (engine.Busy ? "● Üretim çalışıyor" : ToolHealth.Summary(store)) + "\n" + (store.Settings.FreeOnly ? "Ücretsiz öncelikli" : "Ücretli bağlantılar açık") + "\nBu bilgisayar → YouTube";
        var totals=PublicationStatistics.Count(store.Episodes);
        appHeader.PublicationSummary=$"Yayınlanan toplam proje: {totals.Projects} • Shorts: {totals.Shorts} • Normal video: {totals.Normal}";
        appHeader.PublicationSummaryShort=$"Yayında • Proje: {totals.Projects} • Shorts: {totals.Shorts} • Normal: {totals.Normal}";
        appHeader.AccessibleDescription=appHeader.PublicationSummary;appHeader.Invalidate();
    }

    public static string ChannelHeading(Settings settings)

    {

        if (settings.YouTubeChannelId != "" && !string.IsNullOrWhiteSpace(settings.YouTubeChannelTitle)) return settings.YouTubeChannelTitle.Trim();

        return settings.YouTubeChannelId == "" ? "YouTube kanalı bağlı değil" : "Kanal adı doğrulanıyor";

    }

    void UpdateChannelHeading()

    {

        appHeader.Channel = ChannelHeading(store.Settings); channelHeading.Text = appHeader.Channel;

        hints.SetToolTip(channelHeading, appHeader.Channel); Text = appHeader.Channel + " • TubeFlow " + ReleaseVersion; appHeader.Invalidate();

    }

    async Task RefreshChannelName()

    {

        if (store.Key("YOUTUBE_REFRESH_TOKEN") == "") return;

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        try { await new YouTube(store).Channel(timeout.Token); if (!IsDisposed) UpdateChannelHeading(); }

        catch {

            if (IsDisposed) return;

            UpdateChannelHeading();

            hints.SetToolTip(channelHeading, store.Settings.YouTubeChannelTitle == "" ? "Kanal adı alınamadı. Bağlantı Merkezi'nden YouTube kanalını doğrula." : "Son doğrulanmış YouTube kanal adı. Bu açılışta bağlantı yenilenemedi.");

            if (store.Settings.YouTubeChannelTitle == "") { channelHeading.Text = appHeader.Channel = "Kanal adı alınamadı"; Text = "TubeFlow " + ReleaseVersion; appHeader.Invalidate(); }

        }

    }

    void Navigate(string name)

    {
        if(name is "Stüdyo" or "Klasik çalışma alanı")name="Video hazırla";
        if(name=="İçerik paketleri")name="Videolarım";
        if(name=="Stok havuzu"){mediaKind="Video";name="Medya arşivleri";}
        if(name=="Ses arşivi"){mediaKind=audioKind;name="Medya arşivleri";}
        if(name=="Kapak tasarımları"){if(mediaKind is not ("Kapak" or "Kart" or "Logo"))mediaKind="Kapak";name="Medya arşivleri";}
        using var redraw = new StudioRedrawScope(body);
        page.SuspendLayout();
        try {

        packageUpdates.Clear();

        refreshRecent?.Invoke();
        current = name; appHeader.PageName = SectionHeading(PageCaption(name)); UpdateChannelHeading(); appHeader.Busy = engine.Busy; appHeader.Invalidate(); SuspendLayout(); foreach (var old in page.Controls.Cast<Control>().ToArray()) old.Dispose(); page.Controls.Clear(); page.Width = Math.Max(620, body.ClientSize.Width - 90 - (assistantHost?.Visible==true && ClientSize.Width>=1720 ? assistantWidth : 0));

        foreach (var (key, button) in nav) { if(button is SidebarButton entry)entry.Active=key==NavigationRoot(name);button.Invalidate(); }

        foreach(var entry in Controls.Cast<Control>().SelectMany(c=>c.Controls.OfType<SidebarButton>()).Where(b=>b.Tag is string t&&t.StartsWith("rail:"))) {entry.Active=Equals(entry.Tag,"rail:"+NavigationRoot(name));entry.Invalidate();}
        UpdateFooter();

        ContextNavigation(name);

        switch (name) { case "Video hazırla": ProductionWizard(); break; case "Klasik çalışma alanı": ProductionWizard(); break; case "Videolarım": VideoLibrary(); break; case "Ayarlar": Connections(); break; case "Gelişmiş": AdvancedPage(); break; case "Medya arşivleri": MediaHome(); break; case "Stüdyo": Dashboard(); break; case "İçerik paketleri": Packages(); break; case "Üretim ve yayın planı": QueuePage(); break; case "Yayın takvimi": Calendar(); break; case "Bağlantı Merkezi": Connections(); break; case "Program kaynakları": GitHubPage(); break; case "Stok havuzu": PoolPage(); break; case "Ses arşivi": AudioPage(); break; case "Kapak tasarımları": CoverPage(); break; case "Yönetim ayarları": SettingsPage(); break; case "İşlem kayıtları": Logs(); break; }

        page.Height = page.Controls.Cast<Control>().Sum(c => c.Height + c.Margin.Vertical) + 24;

        page.ResumeLayout(true); ResumeLayout(true);

        ModernScrollbars.Apply(this); responsive.Clear(); RememberLayout(page); Reflow();
        } finally { page.ResumeLayout(true); ResumeLayout(true); }

    }

    void RefreshCurrentPage()

    {

        var scroll = page.Parent as ScrollableControl;

        var position = scroll?.AutoScrollPosition ?? Point.Empty;

        Navigate(current);

        if (scroll != null) scroll.AutoScrollPosition = new Point(-position.X, -position.Y);

    }

    public void CaptureSceneEditor(string directory)

    {

        Directory.CreateDirectory(directory); var episode = new Episode { Topic = "Sahne seçimi testi", Demo = true, IncludeLong = true, ShortCount = 1, Plan = Engine.DemoPlan(new Episode { IncludeLong = true, ShortCount = 1 }) };

        using var editor = new SceneEditor(episode, store); editor.Show(); Application.DoEvents();

        using var bitmap = new Bitmap(editor.Width, editor.Height); editor.DrawToBitmap(bitmap, new Rectangle(Point.Empty, editor.Size)); bitmap.Save(Path.Combine(directory, "scene-video-buttons.png")); editor.Close();

    }

    public void CaptureConnections(string directory)

    {

        Directory.CreateDirectory(directory);

        IEnumerable<Control> Children(Control c) => c.Controls.Cast<Control>().SelectMany(x => new[] { x }.Concat(Children(x)));

        var rows = new List<object>();

        foreach (var (width, scale) in new[] { (1440,1f), (1120,1f), (1920,1f), (1440,1.25f), (1440,1.5f) }) {

            Width=width; Navigate("Bağlantı Merkezi"); Children(this).OfType<Button>().Single(b=>b.Text=="YouTube / Program kaynakları").PerformClick(); PerformLayout(); Application.DoEvents();

            var heading=Children(this).OfType<Label>().Single(l=>l.Text=="GitHub"); var card=heading.Parent!;

            if (scale!=1f) { card.Scale(new SizeF(scale,scale)); foreach(var control in Children(card)) control.Font=new Font(control.Font.FontFamily,control.Font.Size*scale,control.Font.Style); PerformLayout(); Application.DoEvents(); }

            foreach (var action in Children(card).OfType<Button>().Where(b=>b.Text!="i")) {

                var rectangle=action.RectangleToScreen(action.ClientRectangle);

                if (!action.Visible || action.Width<100 || action.Height<40) throw new InvalidOperationException("Bağlantı düğmesi görünür veya okunabilir değil: "+action.Text);

                for(Control? parent=action.Parent;parent!=null;parent=parent.Parent) if(!parent.RectangleToScreen(parent.ClientRectangle).Contains(rectangle)) throw new InvalidOperationException("Bağlantı düğmesi kırpılıyor: "+action.Text);

            }

            rows.Add(new { Width=width, Scale=scale, Card=card.Bounds.ToString(), Controls=Children(card).Select(c=>new { Kind=c.GetType().Name,c.Text,c.Visible,Bounds=c.Bounds.ToString(), Parent=c.Parent?.ClientRectangle.ToString() }).ToArray() });

            using var image=new Bitmap(card.Width,card.Height);card.DrawToBitmap(image,new Rectangle(Point.Empty,card.Size));image.Save(Path.Combine(directory,$"github-{width}-{scale.ToString(System.Globalization.CultureInfo.InvariantCulture)}.png"));

        }

        Json.Write(Path.Combine(directory,"controls.json"),rows);

    }

    public void CapturePages(string directory)

    {

        Directory.CreateDirectory(directory);

        string[] screens = ["Stüdyo", "İçerik paketleri", "Yayın takvimi", "Bağlantı Merkezi", "Program kaynakları", "Stok havuzu", "Ses arşivi", "Kapak tasarımları", "Yönetim ayarları", "İşlem kayıtları"];

        for (int i = 0; i < screens.Length; i++) { Navigate(screens[i]); PerformLayout(); Application.DoEvents(); using var bitmap = new Bitmap(Width, Height); DrawToBitmap(bitmap, new Rectangle(Point.Empty, Size)); bitmap.Save(Path.Combine(directory, $"{i + 1:00}.png")); var initialWidth = Width; foreach (var target in new[] { 2000, 1120, initialWidth }) { Width = target; PerformLayout(); Application.DoEvents(); using var resized = new Bitmap(Width, Height); DrawToBitmap(resized, new Rectangle(Point.Empty, Size)); resized.Save(Path.Combine(directory, $"{i + 1:00}-{target}.png")); } }

        if (store.Episodes.FirstOrDefault(e => e.Plan != null) is Episode episode)

        {

            Review(episode, Path.Combine(directory, "review.png"));

            using var editor = new SceneEditor(episode, store); editor.Show(); Application.DoEvents(); using var shot = new Bitmap(editor.Width, editor.Height); editor.DrawToBitmap(shot, new Rectangle(Point.Empty, editor.Size)); shot.Save(Path.Combine(directory, "08-editor.png")); editor.Close();

        }

        using (var preview = Dialog("Sohbet yardımcısı",550,720)) {var chatPreview=new ChatAssistant(store){Dock=DockStyle.Fill};preview.Controls.Add(chatPreview);preview.Show(this); Application.DoEvents(); using var shot = new Bitmap(preview.Width, preview.Height); preview.DrawToBitmap(shot, new Rectangle(Point.Empty, preview.Size)); shot.Save(Path.Combine(directory, "09-chat.png"));preview.Close();}

        Navigate("Stüdyo");

    }

    public void CaptureStockSettings(string directory)

    {

        Directory.CreateDirectory(directory);

        IEnumerable<Control> Descendants(Control parent) => parent.Controls.Cast<Control>().SelectMany(c => new[] { c }.Concat(Descendants(c)));

        void Capture(Control target, string name) {

            for (Control? c = target.Parent; c != null; c = c.Parent) if (c is ScrollableControl scroll && scroll.AutoScroll) { var point = scroll.PointToClient(target.PointToScreen(Point.Empty)); scroll.AutoScrollPosition = new Point(0, Math.Max(0, -scroll.AutoScrollPosition.Y + point.Y - 35)); break; }

            PerformLayout(); Application.DoEvents();

            if (!target.Visible) throw new InvalidOperationException("Stok bağlantı / ayar alanı görünür değil.");

            using var bitmap = new Bitmap(Width, Height); DrawToBitmap(bitmap, new Rectangle(Point.Empty, Size)); bitmap.Save(Path.Combine(directory, name));

        }

        Navigate("Bağlantı Merkezi"); Descendants(this).OfType<Button>().Single(b => b.Text == "Bulut AI anahtarları").PerformClick(); PerformLayout(); Application.DoEvents();

        Capture(Descendants(this).OfType<Label>().Single(l => l.Text == "Vecteezy / stok video"), "vecteezy-connections.png");

        Navigate("Yönetim ayarları"); Descendants(this).OfType<Button>().Single(b => b.Text == "Görüntü / altyazı").PerformClick(); PerformLayout(); Application.DoEvents();

        Capture(Descendants(this).OfType<CheckBox>().Single(c => c.Text == "Vecteezy stok alternatifi etkin"), "stock-settings.png");

    }

    public void CapturePackageLogs(string directory)

    {

        Directory.CreateDirectory(directory);

        var episode = new Episode { Topic = "Küçük Adımların Gücü • İşlem paneli önizlemesi", Status = "Kontrol gerekli", Progress = 100, Demo = true, ShortCount = 1 };

        episode.Plan = Engine.DemoPlan(episode);

        foreach (var (key, vertical, script) in Rules.Scripts(episode)) { var file = Path.Combine(directory, key + "-fixture.mp4"); File.WriteAllText(file, "UI fixture only"); episode.Videos.Add(new AssetVideo { Key = key, Short = vertical, Title = script.Title, File = file, QualityPassed = false }); }

        episode.Log("Senaryo ve sahne planı hazır.");

        episode.Log("Vecteezy stok araması: sea; nature");

        episode.Log("Görüntü kalıcı stok havuzuna kaydedildi.");

        episode.Log("Türkçe seslendirme hazırlanıyor...");

        store.Episodes.Add(episode);

        IEnumerable<Control> Descendants(Control parent) => parent.Controls.Cast<Control>().SelectMany(c => new[] { c }.Concat(Descendants(c)));

        Button Toggle() => Descendants(this).OfType<Button>().Single(b => b.AccessibleName == "Paket işlem kayıtları " + episode.Id);

        TextBox? LogBox() => Descendants(this).OfType<TextBox>().SingleOrDefault(t => t.AccessibleName == "Canlı işlem kayıtları " + episode.Id);

        Navigate("İçerik paketleri"); PerformLayout(); Application.DoEvents();

        if (Descendants(this).OfType<StudioButton>().Single(b => b.Text == "5. Videoları üret").Completed == true) throw new Exception("Kalite denetimi geçmeyen örnek dosyalar yeşil gösterildi");

        if (!Descendants(this).OfType<Button>().Any(b => b.Text == "7. Yayın ayarları")) throw new Exception("Doğrudan yayın ayarları eksik");

        if (LogBox() != null) throw new InvalidOperationException("Kayıt kutusu başlangıçta kapalı değil.");

        Toggle().PerformClick(); PerformLayout(); Application.DoEvents();

        if (LogBox()?.ReadOnly != true) throw new InvalidOperationException("Kayıt kutusu açılmadı.");

        episode.Log("Canlı güncelleme testi: yeni kayıt eklendi."); engine.Notify(); Application.DoEvents();

        if (LogBox()?.Text.Contains("Canlı güncelleme testi") != true) throw new InvalidOperationException("Canlı güncellemede kayıt veya açık tercihi kayboldu.");

        foreach (var width in new[] { 1440, 1120, 2000 }) {

            Width = width; PerformLayout(); Application.DoEvents();

            var log = LogBox()!; if (page.Parent is ScrollableControl scroll) { var point = scroll.PointToClient(log.PointToScreen(Point.Empty)); scroll.AutoScrollPosition = new Point(0, Math.Max(0, -scroll.AutoScrollPosition.Y + point.Y - 390)); }

            Application.DoEvents(); using var bitmap = new Bitmap(Width, Height); DrawToBitmap(bitmap, new Rectangle(Point.Empty, Size)); bitmap.Save(Path.Combine(directory, "package-logs-" + width + ".png"));

        }

        Toggle().PerformClick(); Application.DoEvents();

        if (LogBox() != null) throw new InvalidOperationException("Kayıt kutusu kapanmadı.");

        File.WriteAllText(Path.Combine(directory, "ui-check.txt"), "PASS kapalı başlangıç\nPASS aç / kapat\nPASS salt okunur kayıtlar\nPASS canlı güncellemede açık tercih korunur\nPASS üç genişlikte görünüm\n");

    }

    public void CaptureAiSettings(string directory)

    {

        Directory.CreateDirectory(directory);

        Navigate("Yönetim ayarları");

        IEnumerable<Control> Descendants(Control parent) => parent.Controls.Cast<Control>().SelectMany(c => new[] { c }.Concat(Descendants(c)));

        Descendants(this).OfType<Button>().Single(b => b.Text == "Yerel AI / ücretsiz").PerformClick(); PerformLayout(); Application.DoEvents();

        using (var localShot = new Bitmap(Width, Height)) { DrawToBitmap(localShot, new Rectangle(Point.Empty, Size)); localShot.Save(Path.Combine(directory, "local-ai-settings.png")); }

        var section = Descendants(this).OfType<Button>().Single(b => b.Text == "Bulut AI / isteğe bağlı");

        section.PerformClick(); PerformLayout(); Application.DoEvents();

        using (var cloudShot = new Bitmap(Width, Height)) { DrawToBitmap(cloudShot, new Rectangle(Point.Empty, Size)); cloudShot.Save(Path.Combine(directory, "cloud-ai-settings.png")); }

        var fetch = Descendants(this).OfType<Button>().Single(b => b.Text == "Groq model listesini yenile");

        for (Control? c = fetch.Parent; c != null; c = c.Parent) if (c is ScrollableControl scroll && scroll.AutoScroll) { scroll.ScrollControlIntoView(fetch); break; }

        PerformLayout(); Application.DoEvents();

        if (!fetch.Visible || !fetch.Enabled) throw new InvalidOperationException("Groq model düğmesi görünür ve kullanılabilir değil.");

        using var bitmap = new Bitmap(Width, Height); DrawToBitmap(bitmap, new Rectangle(Point.Empty, Size)); bitmap.Save(Path.Combine(directory, "groq-settings.png"));

        var kimi = Descendants(this).OfType<Button>().Single(b => b.Text == "Kimi model listesini yenile");

        for (Control? c = kimi.Parent; c != null; c = c.Parent) if (c is ScrollableControl scroll && scroll.AutoScroll) { scroll.ScrollControlIntoView(kimi); }

        PerformLayout(); Application.DoEvents();

        if (!kimi.Visible || !kimi.Enabled) throw new InvalidOperationException("Kimi model düğmesi görünür değil.");

        using var kimiShot = new Bitmap(Width, Height); DrawToBitmap(kimiShot, new Rectangle(Point.Empty, Size)); kimiShot.Save(Path.Combine(directory, "kimi-settings.png"));

        Navigate("Bağlantı Merkezi"); PerformLayout(); Application.DoEvents();

        if (!Descendants(this).OfType<Label>().Any(l => l.Text == "Kimi / Moonshot")) throw new InvalidOperationException("Kimi bağlantı kartı eksik.");

        using var connectionShot = new Bitmap(Width, Height); DrawToBitmap(connectionShot, new Rectangle(Point.Empty, Size)); connectionShot.Save(Path.Combine(directory, "kimi-connections.png"));

        File.WriteAllText(Path.Combine(directory, "ui-check.txt"), "PASS sürüm " + ReleaseVersion + "\nPASS Groq modellerini getir düğmesi görünür ve etkin\nPASS Kimi model seçimi ve bağlantı kartı\n");

    }

    void Title(string title, string description)

    {

        page.Controls.Add(Theme.Label(title, 24, true, width: page.Width, height: 52)); page.Controls.Add(Theme.Label(description, 10, color: Theme.Muted, width: page.Width, height: 45));

    }

    FlowLayoutPanel Actions(params Button[] buttons) { var f = new FlowLayoutPanel { Width = page.Width, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Color.Transparent, WrapContents = true, Margin = new Padding(0, 0, 0, 14), Tag = "wide" }; f.Controls.AddRange(buttons); return f; }

    void Message(string message) => Theme.Alert(this, message, "TubeFlow", MessageBoxButtons.OK, MessageBoxIcon.Information);

    async Task Safe(Func<Task> task) { try { await task(); } catch (Exception e) { Message(e.Message); } }

    int ReadyPieces() => new[] { "NVIDIA_API_KEY", "GEMINI_API_KEY", "GROQ_API_KEY", "OPENROUTER_API_KEY", "PEXELS_API_KEY", "PIXABAY_API_KEY", "TAVILY_API_KEY", "GITHUB_TOKEN", "YOUTUBE_REFRESH_TOKEN" }.Count(k => store.Key(k) != "") + (new Vecteezy(store).Ready ? 1 : 0);

    void Dashboard()

    {

        page.Controls.Add(new Hero { Width = page.Width, Margin = new Padding(0, 0, 0, 22), Tag = "wide" });

        var stats = new FlowLayoutPanel { Width = page.Width, Height = 124, WrapContents = false, Margin = Padding.Empty };

        void Stat(string label, string count, string sub, Color color)

        {

            var card = Theme.Card((page.Width - 48) / 4, 108); var l = Theme.Label(label, 9, color: Theme.Muted, width: card.Width - 30); l.Location = new(16, 10); var v = Theme.Label(count, 23, true, color, card.Width - 30, 42); v.Location = new(16, 36); var hint = Theme.Label(sub, 8, color: Theme.Muted, width: card.Width - 30, height: 22); hint.Location = new(16, 79); card.Controls.AddRange([l, v, hint]); stats.Controls.Add(card);

        }

        Stat("İÇERİK PAKETİ", store.Episodes.Count.ToString(), "Bir konu, iki üretim döngüsü", Theme.Text);

        Stat("NORMAL VIDEO", store.Episodes.Count(e => e.IncludeLong).ToString(), "16:9 • kapsamlı anlatım", Theme.Teal);

        Stat("SHORTS", store.Episodes.Sum(e => e.ShortCount).ToString(), "9:16 • bağlantılı veya bağımsız", Theme.Orange);

        Stat("YOUTUBE KANALI", store.Settings.YouTubeChannelId==""?"BAĞLA":"BAĞLI", "Hesaplar bölümünden doğrula", store.Settings.YouTubeChannelId=="" ? Theme.Text : Theme.Success); page.Controls.Add(stats);

        var homeActions = Actions(Theme.Button("+ Yeni video", NewPackage, true), Theme.Button("Videolarım", () => Navigate("Videolarım")), Theme.Button("Yayın sırası", () => Navigate("Üretim ve yayın planı")));
        homeActions.WrapContents = false;
        homeActions.AutoSize = false; homeActions.Height = 46; homeActions.Padding = Padding.Empty;
        void SizeHomeActions() {
            const int gap = 12;
            int available = Math.Max(1, homeActions.ClientSize.Width - gap * (homeActions.Controls.Count - 1));
            double[] shares = [.34, .33, .33];
            int used = 0;
            for (int i = 0; i < homeActions.Controls.Count; i++) {
                var button = homeActions.Controls[i];
                button.Margin = new Padding(0, 0, i == homeActions.Controls.Count - 1 ? 0 : gap, 0);
                button.Width = i == homeActions.Controls.Count - 1 ? available - used : (int)(available * shares[i]);
                used += button.Width;
            }
        }
        homeActions.SizeChanged += (_, _) => SizeHomeActions(); SizeHomeActions(); page.Controls.Add(homeActions);

        var intro = Theme.Card(page.Width, 147); intro.Tag = "wide";

        var heading = Theme.Label("ÜRETİM HATTI", 10, true, Theme.Teal, page.Width - 40); heading.Location = new(20, 14);

        var flow = new Pipeline { Width = page.Width - 40, Location = new(20, 48) };

        var note = Theme.Label("Bir sağlayıcı durursa uygun alternatif denenir. Üretim ve yayın kararları işlem geçmişine kaydedilir.", 9, color: Theme.Muted, width: page.Width - 40); note.Location = new(20, 113); intro.Controls.AddRange([heading, flow, note]); page.Controls.Add(intro);

        page.Controls.Add(Theme.Label("Son içerik paketleri", 16, true, width: page.Width, height: 45));

        if (store.Episodes.Count == 0) Empty("İlk fikrini ekle. Normal video ve Shorts birlikte planlansın.", "Anahtar eklemeden sessiz grafik örneğiyle video motorunu deneyebilirsin.");

        else foreach (var e in store.Episodes.OrderByDescending(e => e.Created).Take(3)) EpisodeCard(e, false);

    }

    void Empty(string title, string description)

    {

        var c = Theme.Card(page.Width, 120); c.Tag = "wide"; var a = Theme.Label(title, 12, true, width: page.Width - 40); a.Location = new(20, 18); var b = Theme.Label(description, 10, color: Theme.Muted, width: page.Width - 40, height: 50); b.Location = new(20, 55); c.Controls.AddRange([a, b]); page.Controls.Add(c);

    }

    void NewPackage() => StartWizardProject();

    void DemoPackage()

    {

        var ep = new Episode { Topic = "Bir fikirden içerik evrenine", Demo = true, ShortCount = 1, IncludeLong = true, Brief = "Sessiz grafik örneği. API kullanmaz; gerçek araştırma, stok görüntü ve ses üretimi değildir.", Plan = null };

        ep.Plan = Engine.DemoPlan(ep); ep.Status = "Senaryo hazır"; store.Episodes.Add(ep); store.Save(); OpenWorkspace(ep);

    }

    void Packages()

    {

        if(!compactLibrary) Title("İçerik paketleri", "Konu → normal video + Shorts. Her paketin senaryosu, medyası, denetimi ve yayın durumu burada.");

        if(!compactLibrary)page.Controls.Add(Actions(Theme.Button(compactLibrary?"+ Yeni video":"+ Yeni paket", compactLibrary?StartWizardProject:NewPackage, true), Theme.Button(compactLibrary?"Yayın sırasını aç":"Üretim ve yayın planı", () => Navigate("Üretim ve yayın planı"), false, 240), Theme.Button("Örnek paket", DemoPackage), Theme.Button("Tüm üretimi durdur", () => engine.Stop(), false, 190)));

        var refreshProjects = Theme.Button(remoteRefreshing?"Liste güncelleniyor…":"YouTube ve projeleri güncelle", () => _ = Safe(()=>RefreshRemote(true)), true, 360); refreshProjects.Enabled = !remoteRefreshing && !engine.Busy; if(compactLibrary)page.Controls.Add(Actions(Theme.Button("+ Yeni video",StartWizardProject,true,180),refreshProjects));else page.Controls.Add(refreshProjects);

        remoteProgressLabel=Theme.Label(remoteRefreshing?remoteProgress:remoteSummary,10,color:Theme.Muted,width:page.Width,height:65);page.Controls.Add(remoteProgressLabel);
        if(remoteRefreshing){page.Controls.Add(new ProgressBar{Width=page.Width,Height=16,Style=ProgressBarStyle.Marquee,MarqueeAnimationSpeed=30});page.Controls.Add(Theme.Button("Taramayı durdur",()=>remoteScanStop?.Cancel(),false,200));}
        if(compactLibrary)RemoteLibraryList();

        if (store.Episodes.Count == 0) Empty("Henüz paket yok", "Yeni paket oluştur veya örnek paketi dene.");

        var episodes = store.Episodes.Where(e=>!compactLibrary||(libraryQuery==""||e.Topic.Contains(libraryQuery,StringComparison.CurrentCultureIgnoreCase))&&LibraryMatches(e,libraryFilter)).OrderByDescending(x => x.Created).ToList();

        if(compactLibrary && episodes.Count==0 && store.Episodes.Count>0) Empty("Aramaya uygun video bulunamadı","Başka bir konu veya video adı ara.");

        var pending=episodes.Where(e=>PackageArchive.Group(e)=="").ToList();
        if(compactLibrary)page.Controls.Add(Theme.Label("Hazırlanan ve Bekleyen Projeler • "+pending.Count,16,true,width:page.Width,height:46));
        foreach (var e in pending) EpisodeCard(e, !compactLibrary);

        foreach (var group in new[] { "Tamamlanmış projeler", "Planlanmış projeler", "Yayına alınmış projeler" }) {

            var items = episodes.Where(e => PackageArchive.Group(e) == group).ToList();

            var open = compactLibrary&&libraryFilter!="Tümü"||openArchiveGroups.Contains(group);

            var groupButton=Theme.Button((compactLibrary&&group=="Tamamlanmış projeler"?"Tamamlanan yerel projeler":group) + " (" + items.Count + ")", () => { if (!openArchiveGroups.Add(group)) openArchiveGroups.Remove(group); Navigate(current); }, false, page.Width);groupButton.TextAlign=ContentAlignment.MiddleLeft;groupButton.Font=Theme.Font(10,true);((StudioButton)groupButton).LeadingChevron=open?"⌄":"›";groupButton.AccessibleName="Proje grubu: "+group;page.Controls.Add(groupButton);

            if (!open) continue;

            foreach (var episode in items) {

                var card = Theme.Card(page.Width, 82); card.Tag = "wide";

                var title = Theme.Label(episode.Topic, 12, true, width: page.Width - 390, height: 32); title.Location = new(20, 8); title.AutoEllipsis = true;

                var dates = string.Join(" • ", episode.Videos.Where(v => v.RemotePublishAt != null || v.PublishAt != null).Select(v => (v.RemotePublishAt ?? v.PublishAt)!.Value.ToOffset(TimeSpan.FromHours(3)).ToString("dd.MM.yyyy HH:mm")).Distinct());

                var details = Theme.Label(episode.Videos.Count + " video" + (dates == "" ? "" : " • " + dates), 9, color: Theme.Muted, width: page.Width - 390); details.Location = new(20, 42);

                var play = Theme.Button("▶ İzle", () => PlayArchivedPackage(episode), true, 100); play.Location = new(card.Width - 120, 17); play.AccessibleName = episode.Topic + " videosunu izle";

                var edit = Theme.Button(openArchiveEditors.Contains(episode.Id) ? "Düzenlemeyi kapat" : "Düzenle", () => { if(compactLibrary || current=="Videolarım") {OpenWorkspace(episode);return;} if (!openArchiveEditors.Add(episode.Id)) openArchiveEditors.Remove(episode.Id); Navigate("İçerik paketleri"); }, false, 150);

                edit.Location = new(card.Width - 370, 17); edit.AccessibleName = episode.Topic + " projesini düzenle";

                var delete = Theme.Button("Sil", () => DeletePackage(episode), false, 80); delete.Location = new(card.Width - 210, 17); delete.ForeColor = Theme.Warning; delete.AccessibleName = episode.Topic + " projesini sil";

                hints.SetToolTip(delete,"Projeyi ve yerel dosyalarını onaydan sonra siler. YouTube'daki videoyu silmez.");

                card.Controls.AddRange([title, details, edit, delete, play]); page.Controls.Add(card);

                if (!compactLibrary && openArchiveEditors.Contains(episode.Id)) EpisodeCard(episode, true);

            }

        }

        packageShape = PackageShape();

    }

    void PlayArchivedPackage(Episode episode)

    {

        void Play(AssetVideo video) { if (File.Exists(video.File)) Theme.Open(video.File); else if (video.YouTubeId != "") Theme.Open("https://www.youtube.com/watch?v=" + Uri.EscapeDataString(video.YouTubeId)); else Message("Bu videonun dosyası bulunamadı."); }

        if (episode.Videos.Count == 1) { Play(episode.Videos[0]); return; }

        using var dialog = new Form { Text = episode.Topic + " • videolar", Width = 620, Height = 400, StartPosition = FormStartPosition.CenterParent, BackColor = Theme.Bg };

        var list = Theme.Flow(); list.Dock = DockStyle.Fill; list.AutoScroll = true;

        foreach (var video in episode.Videos) list.Controls.Add(Theme.Button("▶ " + (video.Short ? "Shorts • " : "Normal video • ") + video.Title, () => Play(video), false, 555));

        dialog.Controls.Add(list); dialog.ShowDialog(this);

    }

    void EpisodeCard(Episode e, bool expanded)

    {

        bool logsOpen = expanded && openPackageLogs.Contains(e.Id);

        var card = Theme.Card(page.Width, expanded ? logsOpen ? 892 : 622 : 142); card.Tag = "wide";

        var type = Theme.Label((e.Demo ? "ÖRNEK / " : "") + (e.MusicClip != null ? "MÜZİK KLİBİ" : e.IncludeLong ? "NORMAL VIDEO" : "") + (e.ShortCount > 0 ? $"  +  {e.ShortCount} SHORTS" : ""), 8, true, Theme.Teal, page.Width - 240); type.Location = new(20, 14);

        var title = Theme.Label(e.Topic, 15, true, width: page.Width - 240, height: 38); title.Location = new(20, 38);

        var status = new AnimatedPackageStatus { Width = 195, Height = 32, Font = Theme.Font(10, true), BackColor = Color.Transparent }; status.Location = new(card.Width - 215, 20); status.TextAlign = ContentAlignment.MiddleRight;

        var details = Theme.Label($"{e.Style}   •   {e.LongMinutes} dk / {e.ShortSeconds} sn   •   {(e.Music ? "Müzik açık" : "Müzik kapalı")}   •   r{e.Revision}", 9, color: Theme.Muted, width: page.Width - (expanded ? 40 : 200)); details.Location = new(20, 78);

        card.Controls.AddRange([type, title, status, details]);

        if (expanded)

        {

            var progress = new PackageStageBar { Width = page.Width - 40, Location = new(20, 113), Font = Theme.Font(8) }; card.Controls.Add(progress);

            var liveProgress = new TurquoisePackageBar { Width = page.Width - 288, Location = new(20, 397), Font = Theme.Font(9) }; card.Controls.Add(liveProgress);

            var remaining = new AnimatedTimeLabel { Text = "Tahmini kalan süre\nHesaplanıyor…", Font = Theme.Font(8), ForeColor = Theme.Text, Width = 232, Height = 44 }; remaining.Location = new(page.Width - 252, 391); remaining.BackColor = Theme.Panel; remaining.TextAlign = ContentAlignment.MiddleCenter; remaining.AccessibleName = "Tahmini kalan süre"; card.Controls.Add(remaining);

            hints.SetToolTip(remaining, "Ölçülen ilerleme hızına göre tahmin edilir. Ses, görüntü işleme ve bağlantı hızına göre değişebilir.");

            var estimate = new PackageTimeEstimate(); var operationClock = System.Diagnostics.Stopwatch.StartNew();

            var row = new FlowLayoutPanel { BackColor = Color.Transparent, Location = new(20, 162), Width = page.Width - 40, Height = 180, WrapContents = true, Tag = "grid3" };

            row.Controls.Add(Theme.Button(e.MusicClip != null ? "1. Klip planını hazırla" : UserNarration.Active(e) ? "1. Hazır metni kullan" : "1. Araştır + yaz", () => _ = Safe(() => engine.Plan(e, resume: e.ResumeStage == "Proofread" && e.Plan != null)), e.Plan == null, 130));

            row.Controls.Add(Theme.Button("2. Sahne editörü", () => Edit(e), false, 135));

            row.Controls.Add(Theme.Button("3. Kapak editörü", () => EditPackageCovers(e), false, 180));

            row.Controls.Add(Theme.Button("4. Başlık / altyazı editörü", () => EditText(e), false, 180));

            row.Controls.Add(Theme.Button("5. Videoları üret", () => _ = Safe(() => engine.Produce(e)), e.Plan != null && e.Videos.Count == 0, 135));

            row.Controls.Add(Theme.Button("6. İncele / onayla", () => Review(e), false, 145));

            row.Controls.Add(Theme.Button("7. Yayın ayarları", () => ScheduleDialog(e), false, 135));

            row.Controls.Add(Theme.Button("8. Dosyalar", () => Theme.Open(store.Folder(e)), false, 100)); card.Controls.Add(row);

            foreach (Button action in row.Controls) { action.Width = (row.Width - 36) / 3; action.Margin = new Padding(0, 0, 12, 12); }

            var message = new AnimatedPackageStatus { Font = Theme.Font(12), Width = page.Width - 40, Height = 45, BackColor = Color.Transparent, TextAlign = ContentAlignment.MiddleLeft }; message.UpdateActivity(ProjectState.Message(e), false); message.Location = new(20, 349); card.Controls.Add(message);
            var errorMessage = new TextBox { Multiline = true, ReadOnly = true, WordWrap = true, ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.None, Font = Theme.Font(12), ForeColor = Theme.Warning, BackColor = Theme.Panel,
                Location = message.Location, Size = message.Size, Visible = false, AccessibleName = "Üretim uyarısı" };
            card.Controls.Add(errorMessage);
            message.SizeChanged += (_, _) => errorMessage.Size = new Size(message.Width, Math.Min(message.Height, errorMessage.Font.Height * 2));
            errorMessage.Height = Math.Min(message.Height, errorMessage.Font.Height * 2);


            void UpdateCard() {

                if (card.IsDisposed) return;

                status.UpdateStatus(e.Status, e.Error != "");

                int value = ProjectState.Progress(e);

                bool transfer = e.Status == "Yükleniyor";

                liveProgress.Value = value;

                liveProgress.Busy = e.Error == "" && e.Status is "Araştırılıyor" or "Üretiliyor" or "Yükleniyor";
                remaining.SetRunning(liveProgress.Busy);

                liveProgress.Text = e.Error != "" ? "Üretim durdu • hata düzeltme bekleniyor • %" + value : (transfer ? "YouTube aktarımı" : e.Status == "Üretiliyor" ? "Video üretimi" : e.Status == "Araştırılıyor" ? "İçerik hazırlanıyor" : e.Plan!=null && e.Videos.Count==0 ? "Hazırlık tamamlandı • üretim başlamadı" : "Üretim ilerlemesi") + (e.Plan!=null && e.Videos.Count==0 && !liveProgress.Busy ? "" : " • %" + value);

                liveProgress.AccessibleDescription = liveProgress.Text;

                var timeLeft = ProductionStages.Estimate(e,estimate.Observe(e.Status+":"+e.CurrentStage, value, liveProgress.Busy, operationClock.Elapsed.TotalSeconds));

                remaining.Text = "Tahmini kalan süre\n" + (e.Error != "" ? "İşlem durdu" : liveProgress.Busy ? PackageTimeEstimate.Display(timeLeft) : value == 100 ? "İşlem tamamlandı" : e.Plan!=null ? "Üretim başlamadı" : "İşlem bekliyor");

                var nextMessage = ProjectState.Message(e);

                message.Visible = e.Error == "";
                errorMessage.Visible = e.Error != "";
                errorMessage.Width = message.Width;
                if (errorMessage.Text != e.Error) { errorMessage.Text = e.Error; errorMessage.SelectionStart = 0; errorMessage.ScrollToCaret(); }
                message.UpdateActivity(nextMessage, liveProgress.Busy);
                if (e.Status == "Araştırılıyor" || e.Status == "Senaryo hazır" || e.ResumeStage is "Plan" or "Proofread") message.UpdateContentSteps(e);

                var expected = e.Plan == null ? new List<string>() : Rules.Scripts(e).Select(x => x.Key).ToList();

                bool[] complete = ProjectState.CompletedStages(e, ScenesAreReviewed(e));

                progress.UpdateStages(complete, e.Status == "Araştırılıyor" ? 0 : e.Status == "Üretiliyor" ? 4 : e.Status == "Yükleniyor" ? 6 : -1);

                for (int i = 0; i < row.Controls.Count; i++) if (row.Controls[i] is StudioButton button && button.Completed != complete[i]) { button.Completed = complete[i]; button.BackColor = complete[i] ? Theme.SuccessSurface : Theme.Elevated; button.ForeColor = Theme.Text; button.AccessibleDescription = complete[i] ? "Bu aşama tamamlandı" : "Bu aşama tamamlanmadı"; button.Invalidate(); }



                var live = CardChildren(card).OfType<TextBox>().FirstOrDefault(c => c.AccessibleName == "Canlı işlem kayıtları " + e.Id);

                if (live != null) { var text = string.Join(Environment.NewLine, e.Events.TakeLast(250)); if (live.Text != text) { bool atEnd = live.SelectionStart + live.SelectionLength >= live.TextLength; var position = live.SelectionStart; live.Text = text; live.SelectionStart = atEnd ? live.TextLength : Math.Min(position, live.TextLength); if (atEnd) live.ScrollToCaret(); } }

            }

            packageUpdates[e.Id] = UpdateCard; UpdateCard();

            var mini = new FlowLayoutPanel { BackColor = Color.Transparent, Location = new(20, 440), Width = page.Width - 40, Height = 54, Tag = "grid3" };

            var edit = Theme.Button("Paket ayarları", () => EditPackage(e), false, 130); edit.Height = 46;

            var clone = Theme.Button("Yeni sürüm / kopya", () => Clone(e), false, 170); clone.Height = 46;

            mini.Tag = "grid2";

            mini.Controls.AddRange([edit, clone]); foreach (Button action in mini.Controls) { action.Width = (mini.Width - 24) / 2; action.Margin = new Padding(0, 0, 12, 0); } card.Controls.Add(mini);

        }

        else { status.UpdateStatus(e.Status, e.Error != ""); var b = Theme.Button("Düzenle", () => OpenWorkspace(e), false, 135); b.Location = new(card.Width - 155, 65); card.Controls.Add(b); }

        if (expanded) {

            var tools = new FlowLayoutPanel { Location = new(20, 507), Width = page.Width - 40, Height = 54, WrapContents = false, Tag = "package-actions" };

            var delete = Theme.Button("⚠ Projeyi tamamen sil", () => DeletePackage(e)); delete.BackColor = Theme.Warning; delete.ForeColor = Theme.Text; tools.Controls.Add(delete);

            if (e.Error != "") { var detail = Theme.Button("Hatanın tüm ayrıntıları", () => Message(e.Error)); detail.ForeColor = Theme.Warning; tools.Controls.Add(detail); }

            var portable=Theme.Button("Tam proje yedeği",()=>_=Safe(()=>ExportProject(e)),false,300);portable.Location=new(340,564);portable.Tag="package-action";card.Controls.Add(portable);

            foreach (Button button in tools.Controls) { button.Width = 300; button.Height = 46; button.Tag = "package-action"; button.Margin = new Padding(0, 0, 12, 0); } card.Controls.Add(tools);

        }

        if (expanded)

        {

            var toggle = Theme.Button(logsOpen ? "İşlem kayıtlarını kapat ▲" : "İşlem kayıtlarını göster ▼", () => {

                if (!openPackageLogs.Add(e.Id)) openPackageLogs.Remove(e.Id);

                RefreshCurrentPage();

            }, logsOpen, 300);

            toggle.Location = new(20, 564); toggle.Height = 46; toggle.Width = 300; toggle.Tag = "package-action"; toggle.AccessibleName = "Paket işlem kayıtları " + e.Id; card.Controls.Add(toggle);

            if (logsOpen)

            {

                var heading = Theme.Label("CANLI İŞLEM KAYITLARI • Bu paketin son 250 kaydı", 9, true, Theme.Teal, page.Width - 40, 28); heading.Location = new(20, 620);

                var log = Theme.Input(string.Join(Environment.NewLine, e.Events.TakeLast(250)), true, page.Width - 40);

                log.ReadOnly = true; log.WordWrap = false; log.ScrollBars = ScrollBars.Both; log.Location = new(20, 653); log.Height = 218;

                log.AccessibleName = "Canlı işlem kayıtları " + e.Id;

                if (e.Events.Count == 0) log.Text = "Henüz işlem kaydı yok. Üretim başladığında burada görünecek.";

                card.Controls.AddRange([heading, log]); log.SelectionStart = log.TextLength; log.ScrollToCaret();

                log.HandleCreated += (_, _) => { log.SelectionStart = log.TextLength; log.ScrollToCaret(); };

            }

        }

        page.Controls.Add(card);

    }

    void EditPackageCovers(Episode episode)

    {

        if(engine.Busy){Message("Üretim tamamlanınca kapakları düzenle.");return;}

        if(episode.Plan==null){Message("Önce senaryoyu ve sahneleri hazırla; sonra her videonun kapağını düzenleyebilirsin.");return;}

        using var editor=new PackageCoverEditor(store,episode);

        if(editor.ShowDialog(this)==DialogResult.OK)RefreshCurrentPage();

    }

    void ImportTopics()

    {

        using var form = Dialog("Konu programı • toplu paket oluşturma", 900, 810); var f = Theme.Flow(810); f.Dock = DockStyle.Fill; f.AutoScroll = true; form.Controls.Add(f);

        f.Controls.Add(Theme.Label("Konu programını yapıştır", 20, true, Theme.Teal, 780, 50));

        f.Controls.Add(Theme.Label("Her satıra bir konu yaz. Tarihli program için: 2026-10-10 20:00 | Konu başlığı | İçerik talimatı. Tarihler Türkiye saati. Tarihsiz satırlar takvimsiz taslak olarak eklenir.", 10, color: Theme.Muted, width: 780, height: 70));

        var text = Theme.Input("", true, 780); text.Height = 240; f.Controls.Add(text);

        var normal = Theme.Check("Her konu için normal video üret", true); f.Controls.Add(normal); var minutes = Theme.Number(6, 1, 30, 450); Field(f, "Normal video süresi / dakika", minutes); var count = Theme.Number(2, 0, 6, 450); Field(f, "Konu başına Shorts sayısı", count);

        var seconds = Theme.Number(45, 15, 180, 450); Field(f, "Shorts süresi / saniye", seconds);

        f.Controls.Add(Theme.Button("Programı paketlere dönüştür", () =>

        {

            try { var result = TopicProgram.Parse(text.Text, normal.Checked, (int)minutes.Value, (int)count.Value, (int)seconds.Value); if (result.Count == 0) throw new InvalidOperationException("En az bir konu gir."); store.Episodes.AddRange(result); store.Save(); form.DialogResult = DialogResult.OK; form.Close(); }

            catch (Exception ex) { Message(ex.Message); }

        }, true, 300)); if (form.ShowDialog(this) == DialogResult.OK) Navigate("İçerik paketleri");

    }

    void EditText(Episode episode)

    {

        if (engine.Busy) { Message("Üretim sürerken yazı düzeni değiştirilemez. Üretim bitince editörü açabilirsin."); return; }

        if (episode.Plan == null) { Message("Önce senaryo ve sahne planı oluştur."); return; }

        if (episode.Videos.Any(v => v.YouTubeId != "")) { Message("Yüklenmiş paket için Yeni sürüm / kopya oluştur."); return; }

        using var editor = new TextEditor(store, episode);

        if (editor.ShowDialog(this) != DialogResult.OK) return;

        if (engine.Busy) { Message("Başka işlem başladı; düzenleme kaydedilmedi. Yeniden açıp dene."); return; }

        TextEditSave.Apply(store, episode, editor.Plan); RefreshCurrentPage();

    }

    void Clone(Episode e)

    {

        var clone = JsonSerializer.Deserialize<Episode>(JsonSerializer.Serialize(e, Json.Options), Json.Options)!; clone.Id = Guid.NewGuid().ToString("N"); clone.Created = DateTimeOffset.UtcNow; clone.Videos = []; clone.SocialPosts = []; clone.Events = []; clone.ProductionDays = []; clone.RetryAt = null; clone.RetryAttempts = 0; clone.ResumeStage = ""; clone.Approved = false; clone.ApprovalHash = ""; clone.RemoteUploadJob = ""; clone.RemoteUploadRepo = ""; clone.RemoteUploadState = ""; clone.RemoteUploadNote = ""; clone.RemoteUploadRelease = 0; clone.RemoteUploadAsset = 0; clone.RemoteRunId = ""; clone.RemoteRepo = ""; clone.RemoteState = ""; clone.ScenesReviewed = false; clone.Error = ""; clone.Status = clone.Plan == null ? "Taslak" : "Senaryo hazır"; clone.Revision++;

        store.Episodes.Add(clone); store.Save(); OpenWorkspace(clone);

    }

    void EditPackage(Episode e)
    {
        if(engine.Busy){Message("Çalışan işlem bitince projeyi düzenleyebilirsin.");return;}
        wizardDraft=null;selectedWorkspaceId=e.Id;e.WizardStep=0;Navigate("Video hazırla");
    }
    void Edit(Episode e)

    {

        if (engine.Busy) { Message("Çalışan işi durdurduktan sonra düzenleyebilirsin."); return; }

        if (e.Plan == null) { Message("Önce araştırma ve senaryo oluştur."); return; }

        if (e.Videos.Any(v => v.YouTubeId != "")) { Message("Yüklenmiş içerik için yeni sürüm oluştur."); return; }

        using var editor = new SceneEditor(e, store); if (editor.ShowDialog(this) != DialogResult.OK) return;

        if (engine.Busy) { Message("Başka işlem başladı; düzenleme kaydedilmedi. Yeniden açıp dene."); return; }

        var mediaChanged = System.Text.Json.JsonSerializer.Serialize(new { e.Plan.Long, e.Plan.Shorts }) != System.Text.Json.JsonSerializer.Serialize(new { editor.Plan.Long, editor.Plan.Shorts });

        if (!mediaChanged) { e.Plan = editor.Plan; e.ScenesReviewed = true; e.Approved = false; e.ApprovalHash = ""; e.Log("Sahne incelemesi kaydedildi; medya korunuyor."); store.Save(); Navigate(current); return; }

        e.Plan = editor.Plan; e.ScenesReviewed = true; e.Revision++; e.Approved = false; e.ApprovalHash = ""; e.Videos = []; e.Status = "Senaryo hazır"; e.Error = ""; e.Log("Sahneler düzenlendi; bağlı normal video / Shorts çıktıları yeniden denetlenecek."); store.Save(); Navigate(current);

    }

    void ReviewLegacy(Episode e)

    {

        using var form = Dialog("Denetim • " + e.Topic, 980, 740); var f = Theme.Flow(880); f.Dock = DockStyle.Fill; f.AutoScroll = true; form.Controls.Add(f);

        f.Controls.Add(Theme.Label(e.Demo ? "ÖRNEK PAKET • yayın kapalı" : "Yayın öncesi denetim", 20, true, Theme.Teal, 850, 50));

        foreach (var asset in e.Videos)

        {

            f.Controls.Add(Theme.Label(asset.Title + $"  •  {asset.Duration:0} sn", 13, true, width: 850, height: 40));

            f.Controls.Add(Theme.Label(string.Join("\n", asset.Checks), 10, color: asset.QualityPassed ? Theme.Muted : Theme.Warning, width: 850, height: asset.Checks.Count * 25));

            f.Controls.Add(Actions(Theme.Button("Videoyu oynat", () => Theme.Open(asset.File)), Theme.Button("Altyazıyı aç", () => Theme.Open(Path.Combine(Path.GetDirectoryName(asset.File)!, "subtitles.srt"))), Theme.Button("Kapağı aç", () => Theme.Open(asset.Thumbnail))));

        }

        if (e.Plan != null) f.Controls.Add(Theme.Label("Editoryal uyarılar: " + (e.Plan.Warnings.Count == 0 ? "yok" : string.Join(" • ", e.Plan.Warnings)), 10, color: Theme.Warning, width: 850, height: 80));

        f.Controls.Add(Theme.Label("Otomatik kontroller, bilgilerin doğruluğu ve lisansların insan denetiminin yerini tutmaz. Videoları izleyip kaynakları kontrol et.", 10, color: Theme.Muted, width: 850, height: 65));

        f.Controls.Add(Theme.Button("İçeriği ve medyayı onayla", () => { try { engine.Approve(e); form.Close(); Navigate(current); } catch (Exception ex) { Message(ex.Message); } }, true, 270)); form.ShowDialog(this);

    }

    public static Form Dialog(string title, int width = 850, int height = 730) { var dialog=new Form { Text = title, Width = width, Height = height, StartPosition = FormStartPosition.CenterParent, BackColor = Theme.Bg, ForeColor = Theme.Text, Font = Theme.Font(), Padding = new Padding(24), MinimizeBox = false, MaximizeBox = false }; ModernScrollbars.Apply(dialog); return dialog; }

    void ScheduleDialog(Episode e, Action<Form>? preview = null)

    {

        using var form = Dialog("Yayın kontrolü • " + e.Topic, 840, 770); var f = Theme.Flow(770); f.Dock = DockStyle.Fill; f.AutoScroll = true; form.Controls.Add(f);

        f.Controls.Add(Theme.Label("Yayın takvimi ve kanal ayarları", 19, true, width: 740, height: 50));

        f.Controls.Add(Theme.Label("Tüm saatler Türkiye saati (UTC+3). Önce özel yükleyebilir veya doğrudan ileri bir tarihe planlayabilirsin.", 10, color: Theme.Muted, width: 740, height: 60));

        f.Controls.Add(Theme.Button("Video önizleme", () => {
            var videos = e.Videos.Where(v => File.Exists(v.File)).OrderBy(v => v.Short).ThenBy(v => v.Key).ToArray();
            if (videos.Length == 0) { Message("Önizleme için hazır video bulunamadı. Önce video üretimini tamamla."); return; }
            if (videos.Length == 1) { Theme.Open(videos[0].File); return; }
            using var preview = Dialog("Yayın öncesi video önizleme", 840, 540);
            var list = Theme.Flow(740); list.Dock = DockStyle.Fill; list.AutoScroll = true; preview.Controls.Add(list);
            list.Controls.Add(Theme.Label("İzlemek istediğin videoyu seç; ses, görüntü ve altyazıları kontrol et.", 11, width: 700, height: 65));
            foreach (var video in videos) {
                var selected = video;
                list.Controls.Add(Theme.Button((selected.Short ? "SHORTS" : "NORMAL VİDEO") + " • " + selected.Title, () => Theme.Open(selected.File), false, 700));
            }
            preview.ShowDialog(form);
        }, false, 450));

        var date = new DateTimePicker { Width = 450, Format = DateTimePickerFormat.Custom, CustomFormat = "dd MMMM yyyy HH:mm", Value = e.PublishAt?.ToOffset(TimeSpan.FromHours(3)).DateTime ?? DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(3)).AddDays(1).DateTime, Font = Theme.Font(12) };

        var spacing = Theme.Number(e.ShortSpacingHours, 1, 168); var kids = Theme.Check("Çocuklara özel hazırlanmış içerik", e.MadeForKids); var synthetic = Theme.Check("Gerçekçi değiştirilmiş / sentetik içerik açıklaması gerekli", e.DiscloseSynthetic);

        Field(f, "Ana yayın tarihi", date); Field(f, "Shorts arası saat (normal videodan sonraki ilk Shorts dahil)", spacing); f.Controls.Add(kids); f.Controls.Add(synthetic);

        bool SaveDates()

        {

            if (engine.Busy) { Message("Çalışan işlem sırasında takvim değiştirilemez."); return false; }

            e.PublishAt = new DateTimeOffset(DateTime.SpecifyKind(date.Value, DateTimeKind.Unspecified), TimeSpan.FromHours(3)); e.ShortSpacingHours = (int)spacing.Value; e.MadeForKids = kids.Checked; e.DiscloseSynthetic = synthetic.Checked; int i = 0;

            foreach (var asset in e.Videos.OrderBy(v => v.Short).ThenBy(v => v.Key)) { asset.PublishAt = Rules.VideoDate(e, asset.Short, i); if (asset.Short) i++; }

            try { store.Save(); return true; } catch (Exception ex) { Message(ex.Message); return false; }

        }

        f.Controls.Add(Theme.Button("Takvimi kaydet", () => { if (!SaveDates()) return; Message("Takvim kaydedildi. Henüz YouTube'da değişiklik yapılmadı."); }, true, 230));

        f.Controls.Add(Theme.Button("Instagram / TikTok / Facebook’ta Paylaş", () => SocialPublishDialog(e), true, 450));

        f.Controls.Add(Theme.Label("YouTube yükleme ve yayınlama", 15, true, width: 740, height: 55));

        foreach (var cover in e.Videos.Where(v => v.Short)) {

            f.Controls.Add(Theme.Button("Shorts tasarım kapağını aç", () => Theme.Open(cover.Thumbnail), false, 300));

            f.Controls.Add(Theme.Label(cover.ThumbnailUploaded ? "Shorts kapağı API tarafından kabul edildi; Studio'da görünümünü kontrol et." : "Shorts için tasarım PNG'si hazırdır. API kabul etmezse Studio → Küçük resim → Dosya yükle bölümünde bu dosyayı seç. " + cover.ThumbnailNote, 10, color: Theme.Warning, width: 740, height: 85));

        }

        f.Controls.Add(Theme.Label("Onaylanmış gerçek çıktılar gerekir. Videolar doğrudan YouTube’a yüklenir. Planlama doğrulandıktan sonra YouTube seçilen saatte yayınlar; bilgisayar açık kalmak zorunda değildir.", 10, color: Theme.Muted, width: 740, height: 85));

        f.Controls.Add(Theme.Button("YouTube'a özel yükle", () => { if (!SaveDates()) return; _ = Safe(async () => { await engine.Upload(e, false); Message(e.Error != "" ? e.Error : e.Status); }); }, false, 270));

        f.Controls.Add(Theme.Button("Yüklenen açıklamaları güncelle", () => _ = Safe(async () => { if (engine.Busy) throw new InvalidOperationException("Önce çalışan işlemi bitir."); await engine.External(async ct => { foreach (var asset in e.Videos.Where(v => v.YouTubeId != "")) { await new YouTube(store).UpdateDescription(asset, Rules.Scripts(e).First(x => x.Key == asset.Key).Script, ct); e.Log(asset.Title + ": açıklama güncellendi; video ve görünürlük korundu."); store.Save(); } }); Message("Yüklenen açıklamalar güncellendi. Video dosyaları ve görünürlük değiştirilmedi."); }), false, 320));

        f.Controls.Add(Theme.Button("Yüklenen videoları şimdi yayına al", () => _ = Safe(async () => {

            if (engine.Busy) throw new InvalidOperationException("Önce çalışan işlemi bitir.");

            if (!e.Approved || e.ApprovalHash != Rules.HashPlan(e) || !Reliability.OutputsCurrent(e)) throw new InvalidOperationException("Güncel videoları önce inceleyip onayla.");

            if (e.Videos.Count == 0 || e.Videos.Any(v => v.YouTubeId == "")) throw new InvalidOperationException("Önce tüm videoların YouTube yüklemesini tamamla.");

            if (Theme.Alert(this, e.Topic + " paketindeki " + e.Videos.Count + " video şimdi herkese açık yayınlanacak. Devam edilsin mi?", "Şimdi yayınla", MessageBoxButtons.YesNo) != DialogResult.Yes) return;

            await engine.External(async ct => {

                var yt = new YouTube(store); var channel = await yt.ChannelIdentity(ct);

                Engine.VerifyChannel(e.ApprovedChannelId, store.Settings.YouTubeChannelId, channel.Id);

                foreach (var asset in e.Videos) { await yt.PublishNow(asset, ct); e.Log(asset.Title + ": YouTube herkese açık yayını doğruladı."); store.Save(); }

                e.Status = "YouTube’da yayında"; store.Save();

            });

            Message("YouTube yayını başarıyla açıldı.");

        }), true, 340));

        f.Controls.Add(Theme.Button("YouTube'da yayını planla", () => { if (!SaveDates()) return; _ = Safe(async () => { await engine.Upload(e, true); Message(e.Error != "" ? e.Error : e.Status); }); }, true, 270));

        f.Controls.Add(Theme.Button("Silinen YouTube bağlantılarını sıfırla", () => { if (engine.Busy) { Message("Önce çalışan işlemi bitir."); return; } if (Theme.Alert(this, "Bu paketin videolarını YouTube'dan sildiysen yerel yükleme bağlantılarını sıfırlayabilirsin. Video ve senaryo dosyaları korunur. Devam edilsin mi?", "Silinen yüklemeler", MessageBoxButtons.YesNo) != DialogResult.Yes) return; Engine.ResetDeletedUploads(e); store.Save(); Message("Bağlantılar sıfırlandı; paketi düzenleyip yeniden üretebilir ve yükleyebilirsin."); }, false, 340));

        f.Controls.Add(Theme.Button("YouTube yayın planını kaldır", () => _ = Safe(async () => { if (engine.Busy) throw new InvalidOperationException("Çalışan işlemi durdur."); await engine.External(async ct => { foreach (var a in e.Videos.Where(v => v.YouTubeId != "")) await new YouTube(store).Unschedule(a, ct); }); e.Status = "YouTube'da özel"; store.Save(); Message("Yayın planı kaldırıldı, videolar özel yapıldı."); }), false, 270));

        f.Controls.Add(Theme.Label("Yüklenmiş videoların çocuklara özel / sentetik içerik ayarlarını değiştirmek bu sürümde YouTube Studio üzerinden yapılır.", 9, color: Theme.Warning, width: 740, height: 60));

        using var help = new ToolTip { AutoPopDelay = 15000, InitialDelay = 350, ReshowDelay = 100, ShowAlways = true };

        foreach (var action in f.Controls.OfType<Button>().ToArray()) {

            var explanation = PublicationExplanation(action.Text);

            var index = f.Controls.GetChildIndex(action);

            var row = new FlowLayoutPanel { Width = action.Width + 64, Height = 46, WrapContents = false, Margin = Padding.Empty, BackColor = Color.Transparent };

            var info = Theme.Button("ⓘ", () => Theme.Alert(form, explanation, action.Text, icon: MessageBoxIcon.Information), false, 46);

            info.AccessibleName = action.Text + " hakkında bilgi"; info.AccessibleDescription = explanation; info.Tag = "publication-info";

            help.SetToolTip(action, explanation); help.SetToolTip(info, explanation);

            f.Controls.Remove(action); row.Controls.AddRange([action, info]); f.Controls.Add(row); f.Controls.SetChildIndex(row, index);

        }

        if (preview != null) form.Shown += (_, _) => form.BeginInvoke(() => preview(form));

        form.ShowDialog(this); Navigate(current);

    }

    void Calendar()

    {

        Title("Yayın takvimi", "Türkiye saati / UTC+3 • Normal video ve Shorts aynı takvimde, bağımsız yayın kontrolleriyle.");

        page.Controls.Add(Theme.Label("Anlık üretim: İçerik paketlerinden üret ve yükle. Planlı stok: videoları üretip onayla, her videonun tarihini belirle ve topluca YouTube’a gönder. Yükleme tamamlandıktan sonra bilgisayar kapalı olabilir.", 10, color: Theme.Muted, width: page.Width, height: 75));

        page.Controls.Add(Actions(Theme.Button("+ Konu planla", NewPackage, true), Theme.Button("Stok videoları YouTube’a planlı yükle", () => _ = Safe(UploadPlannedStock), true, 360), Theme.Button("Yenile", () => Navigate(current))));

        var assets = store.Episodes.SelectMany(e => e.Videos.Select(v => (Episode: e, Asset: v))).OrderBy(x => x.Asset.PublishAt ?? DateTimeOffset.MaxValue).ToList();

        if (assets.Count == 0) Empty("Takvim henüz boş", "İçerik paketlerinden yayın tarihi belirle. Üretim tamamlandığında normal video ve Shorts burada görünür.");

        foreach (var (episode, asset) in assets)

        {

            var card = Theme.Card(page.Width, 110); card.Tag = "wide";

            var date = (asset.Scheduled ? asset.RemotePublishAt ?? asset.PublishAt : asset.PublishAt)?.ToOffset(TimeSpan.FromHours(3)).ToString("dd MMM • HH:mm", new CultureInfo("tr-TR")) ?? "Tarih yok";

            var a = Theme.Label(date, 14, true, Theme.Teal, 190, 38); a.Location = new(20, 20); var b = Theme.Label((asset.Short ? "SHORTS / " : "NORMAL / ") + asset.Title, 12, true, width: page.Width - 410); b.Location = new(210, 20);

            var c = Theme.Label(asset.YouTubeState != "" ? asset.YouTubeState + " • Son kontrol: " + asset.YouTubeCheckedAt?.ToOffset(TimeSpan.FromHours(3)).ToString("dd.MM HH:mm") : asset.Scheduled ? "YouTube'da zamanlandı" + (asset.RemotePublishAt != null && asset.PublishAt != asset.RemotePublishAt ? " • Yerel tarih değişti; uzak tarih korunuyor" : "") : asset.YouTubeId != "" ? "YouTube'da özel" : "Yerel • yüklenmedi", 9, color: Theme.Muted, width: page.Width - 410); c.Location = new(210, 60);

            var button = Theme.Button("Tarih / saat", () => AssetDateDialog(episode, asset), false, 110); button.Location = new(card.Width - 130, 30); card.Controls.AddRange([a, b, c, button]); page.Controls.Add(card);

        }

        foreach (var e in store.Episodes.Where(e => e.PublishAt != null && e.Videos.Count == 0)) Empty(e.PublishAt!.Value.ToOffset(TimeSpan.FromHours(3)).ToString("dd MMM HH:mm") + " • " + e.Topic, "Konu planlandı; videolar henüz üretilmedi.");

    }

    void AssetDateDialog(Episode episode, AssetVideo asset)

    {

        if (engine.Busy) { Message("Önce çalışan işlemi bitir."); return; }

        using var dialog = Dialog("Yayın tarihi • " + asset.Title, 680, 300);

        var flow = Theme.Flow(620); flow.Dock = DockStyle.Fill; dialog.Controls.Add(flow);

        flow.Controls.Add(Theme.Label("Yalnızca bu videonun yayın tarihi • Türkiye saati (UTC+3)", 11, width: 590, height: 45));

        var picker = new DateTimePicker { Width = 460, Format = DateTimePickerFormat.Custom, CustomFormat = "dd MMMM yyyy HH:mm", Value = asset.PublishAt?.ToOffset(TimeSpan.FromHours(3)).DateTime ?? DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(3)).AddDays(1).DateTime };

        flow.Controls.Add(picker);

        flow.Controls.Add(Theme.Button("Tarihi kaydet", () => {

            if (engine.Busy) { Message("Önce çalışan işlemi bitir."); return; }

            var value = new DateTimeOffset(DateTime.SpecifyKind(picker.Value, DateTimeKind.Unspecified), TimeSpan.FromHours(3));

            if (value <= DateTimeOffset.UtcNow.AddMinutes(10)) { Message("Yayın tarihi en az 10 dakika ileride olmalı."); return; }

            asset.PublishAt = value; store.Save(); dialog.Close();

        }, true, 230));

        flow.Controls.Add(Theme.Label("Kaydetmek YouTube’u değiştirmez. Toplu planlı yükleme ile yeni tarih gönderilir.", 10, color: Theme.Muted, width: 590, height: 60));

        dialog.ShowDialog(this); Navigate(current);

    }

    async Task UploadPlannedStock()

    {

        if (engine.Busy) throw new InvalidOperationException("Önce çalışan işlemi bitir.");

        var pending = PlannedStock.Pending(store.Episodes);

        if (pending.Count == 0) { Message("Planlanacak stok video yok. Videoları üret, onayla ve yayın tarihlerini belirle."); return; }

        PlannedStock.Validate(pending, DateTimeOffset.UtcNow);

        int complete = 0;

        foreach (var episode in pending)

        {

            await engine.Upload(episode, true);

            if (episode.Error != "" || episode.Status == "Durduruldu") { Message($"{complete} paket tamamlandı. İşlem durdu: {episode.Error} {episode.Status}. Tamamlanan yüklemeler korunur; yeniden başlatabilirsin."); Navigate(current); return; }

            complete++;

        }

        Message($"{complete} paketin videoları YouTube’da planlandı. Yayın saatinde bilgisayarın açık kalması gerekmez."); Navigate(current);

    }

    static void Field(FlowLayoutPanel f, string label, Control control)

    {

        f.Controls.Add(Theme.Label(label, 9, true, Theme.Muted, Math.Max(450, f.Width - 30), 30)); control.Margin = new Padding(0, 0, 0, 12); f.Controls.Add(control);

    }

    void Connections()

    {

        Title("Hesaplar ve AI motorları", "Bağlantını, modelini ve kullanım tercihini aynı bölümden yönet.");

        page.Controls.Add(Theme.Label("Anahtar kayıtlı olması, hizmetin çalıştığının doğrulanması değildir. Ücretsiz hesapların kotalarını sağlayıcı panelinden sınırla.", 10, color: Theme.Warning, width: page.Width, height: 50));

        var socials=Theme.Button("Instagram / TikTok / Facebook", SocialConnectionsDialog, false, 380);

        var android=Theme.Button("Android’e anahtar aktar", AndroidKeyTransfer, false, 330);

        var localGroup = Theme.Flow(page.Width - 50); LocalAiPanel(localGroup); LocalVisualPanel(localGroup);

        var cloudGroup = Theme.Flow(page.Width - 50); cloudGroup.Controls.Add(Theme.Label(CloudAiNotice, 10, color: Theme.Warning, width: page.Width - 50, height: 100));

        var stockGroup = Theme.Flow(page.Width - 50); var soundGroup = Theme.Flow(page.Width - 50); var accountGroup = Theme.Flow(page.Width - 50);

        var cards = new FlowLayoutPanel { Width = page.Width, AutoSize = true, WrapContents = true, FlowDirection = FlowDirection.LeftToRight };

        (string Name, string Key, string Description, string Url)[] services = [

            ("Gemini", "GEMINI_API_KEY", "İsteğe bağlı bulut • API kotası/bakiyesi gerekir", "https://aistudio.google.com/apikey"),

            ("NVIDIA", "NVIDIA_API_KEY", "Bulut metin modelleri • kullanım hesabının API kotasına bağlıdır", "https://build.nvidia.com/"),
            ("Groq", "GROQ_API_KEY", "İsteğe bağlı bulut • API anahtarı ve hesap kotası gerekir", "https://console.groq.com/keys"),

            ("OpenRouter", "OPENROUTER_API_KEY", "openrouter/free ile ücretsiz modeller • kota sınırlıdır", "https://openrouter.ai/settings/keys"),

            ("Kimi / Moonshot", "KIMI_API_KEY", "İsteğe bağlı metin üretimi • API ücretli olabilir", "https://platform.kimi.ai"),

            ("Tavily / araştırma", "TAVILY_API_KEY", "Web kaynakları ve özetleri • basic arama kullanılır", "https://app.tavily.com"),

            ("Pexels / stok video", "PEXELS_API_KEY", "Konuyla ilişkili stok görüntüler • kaynak kayıtları tutulur", "https://www.pexels.com/api/"),

            ("Pixabay / stok video", "PIXABAY_API_KEY", "Pexels alternatifi • arama sonuçları 24 saat önbelleklenir", "https://pixabay.com/api/docs/"),

            ("Vecteezy / stok video", "VECTEEZY_ACCOUNT_ID", "", "https://www.vecteezy.com/account/developer-tools"),

            ("GitHub", "GITHUB_TOKEN", "İsteğe bağlı program kaynak deposu • video işlemleri için gerekmez", "https://github.com/settings/tokens"),

            ("Google Cloud TTS", "GOOGLE_TTS_API_KEY", "Türkçe seslendirme • aylık yerel karakter sınırı ve ses önizlemesi", "https://console.cloud.google.com/apis/credentials"),

            ("ElevenLabs / isteğe bağlı", "ELEVENLABS_API_KEY", "Zamanlamalı Türkçe ses • ücretsiz öncelik modunda kapalı", "https://elevenlabs.io/app/settings/api-keys"),

            ("OpenAI / isteğe bağlı", "OPENAI_API_KEY", "Metin üretimi alternatifi • ücretsiz öncelik modunda kapalı", "https://platform.openai.com/api-keys")];

        var cloudSaves=new List<Action>();CloudAiOptions(cloudGroup,store.Settings,cloudSaves);
        var order=Theme.Input(store.Settings.ProviderOrder,width:650);Field(cloudGroup,"Model alternatif sırası",order);
        cloudGroup.Controls.Add(Theme.Button("Bulut kullanım tercihlerini kaydet",()=>{if(engine.Busy){Message("Üretimin bitmesini bekle.");return;}foreach(var apply in cloudSaves)apply();store.Settings.ProviderOrder=order.Text.Trim();foreach(var provider in store.Settings.EnabledCloudProviders.Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries))if(!store.Settings.ProviderOrder.Split(',',StringSplitOptions.TrimEntries).Contains(provider))store.Settings.ProviderOrder+=","+provider;store.Save();store.ProviderBlocked.Clear();store.PreferredProvider="";Message("Bulut tercihleri kaydedildi.");},true,360));
        var cloudCards = new FlowLayoutPanel { Width = page.Width - 50, MaximumSize=new Size(page.Width-50,0), AutoSize = true, AutoSizeMode=AutoSizeMode.GrowAndShrink, WrapContents = true }; cloudGroup.Controls.Add(cloudCards);

        var stockCards = new FlowLayoutPanel { Width = page.Width - 50, MaximumSize=new Size(page.Width-50,0), AutoSize = true, AutoSizeMode=AutoSizeMode.GrowAndShrink, WrapContents = true }; stockGroup.Controls.Add(stockCards);

        var soundCards = new FlowLayoutPanel { Width = page.Width - 50, MaximumSize=new Size(page.Width-50,0), AutoSize = true, AutoSizeMode=AutoSizeMode.GrowAndShrink, WrapContents = true }; soundGroup.Controls.Add(soundCards);

        var accountCards = new FlowLayoutPanel { Width = page.Width - 50, MaximumSize=new Size(page.Width-50,0), AutoSize = true, AutoSizeMode=AutoSizeMode.GrowAndShrink, WrapContents = true }; accountGroup.Controls.Add(accountCards);

        foreach (var (name, key, description, url) in services)

        {

            var serviceCards = key is "PEXELS_API_KEY" or "PIXABAY_API_KEY" or "VECTEEZY_ACCOUNT_ID" or "TAVILY_API_KEY" ? stockCards : key is "GOOGLE_TTS_API_KEY" or "ELEVENLABS_API_KEY" ? soundCards : key == "GITHUB_TOKEN" ? accountCards : cloudCards;

            int width = (page.Width - 90) / 2;

            if (key == "VECTEEZY_ACCOUNT_ID") { serviceCards.Controls.Add(VecteezyCard(width)); continue; }

            var c = Theme.Card(width, 285); var h = Theme.Label(name, 14, true, width: width - 90); h.Location = new(18, 13); var d = Theme.Label(description, 9, color: Theme.Muted, width: width - 40, height: 44); d.Location = new(18, 48);

            var field = Theme.Input("", false, width - 40); field.UseSystemPasswordChar = key != "VECTEEZY_ACCOUNT_ID"; field.PlaceholderText = store.Key(key) == "" ? key + " yapıştır" : "Anahtar kayıtlı • değiştirmek için yenisini yapıştır"; field.Location = new(18, 99);

            var saved = Theme.Label((store.Key(key) == "" ? "Eksik parça • " : "Anahtar kayıtlı • ") + key, 8, true, store.Key(key) == "" ? Theme.Warning : Theme.Teal, width - 40, 22); saved.Location = new(18, 251);

            var row = new TableLayoutPanel { Location = new(18, 139), Width = width - 36, Height = 104, ColumnCount = 2, RowCount = 2, Margin = Padding.Empty, Padding = Padding.Empty };

            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            row.RowStyles.Add(new RowStyle(SizeType.Percent, 50)); row.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

            var save = Theme.Button("Kaydet", () => { if (engine.Busy) { Message("Çalışan işlem tamamlanınca bağlantıyı değiştir."); return; } if (field.Text.Trim() == "") { Message("Anahtarı yapıştır."); return; } if (key == "VECTEEZY_ACCOUNT_ID" && !System.Text.RegularExpressions.Regex.IsMatch(field.Text.Trim(), @"^[0-9]{1,20}$")) { Message("Account Id yalnızca rakamlardan oluşmalı."); return; } store.Vault.Set(key, field.Text.Trim()); if (key == "NVIDIA_API_KEY") _ = NvidiaModels.Start(store); store.ProviderBlocked.Clear(); store.PreferredProvider = ""; field.Clear(); field.PlaceholderText = "Anahtar kayıtlı"; saved.Text = "Şifreli kasaya kaydedildi"; saved.ForeColor = Theme.Text; UpdateFooter(); }, true, 88); save.Height = 46;

            var test = Theme.Button("Dene", () => _ = Safe(async () => { saved.Text = "Kontrol ediliyor..."; await TestConnection(key); saved.Text = "Anahtar/erişim doğrulandı; üretim bakiyesi ve seçili model ayrıca denetlenmeli"; saved.ForeColor = Theme.Text; }), false, 75); test.Height = 46;

            var portal = Theme.Button("Anahtar al", () => Theme.Open(url), false, 108); portal.Height = 46;

            var remove = Theme.Button("Sil", () => { store.Vault.Set(key, ""); if (key == "NVIDIA_API_KEY") NvidiaModels.Cancel(store); field.Clear(); saved.Text = "Anahtar silindi"; saved.ForeColor = Theme.Warning; }, false, 58); remove.Height = 46;

            var help = Theme.Button("i", () => ShowConnectionHelp(key), false, 46); help.Height = 40; help.Location = new(width - 64, 12); help.AccessibleName = name + " bağlantı yardımı"; c.Controls.Add(help);

            row.Controls.Add(save, 0, 0); row.Controls.Add(test, 1, 0); row.Controls.Add(portal, 0, 1); row.Controls.Add(remove, 1, 1);

            foreach (Button action in row.Controls) { action.Dock = DockStyle.Fill; action.Margin = new Padding(0, 0, 8, 4); } c.Controls.AddRange([h, d, field, row, saved]); var provider = key switch {"GEMINI_API_KEY"=>"Gemini","NVIDIA_API_KEY"=>"NVIDIA","GROQ_API_KEY"=>"Groq","OPENROUTER_API_KEY"=>"OpenRouter","OPENAI_API_KEY"=>"OpenAI","KIMI_API_KEY"=>"Kimi",_=>""};
            if(provider==""){serviceCards.Controls.Add(c);}else{
                var providerBox=new StudioDisclosure(name+" • anahtar ve model",Math.Max(350,page.Width-90));
                providerBox.Content.Controls.Add(c);
                var modelSettings=Theme.Flow(Math.Max(350,page.Width-110));var modelSaves=new List<Action>();
                var selectedModel=provider switch{"Gemini"=>store.Settings.GeminiModel,"NVIDIA"=>store.Settings.NvidiaModel,"Groq"=>store.Settings.GroqModel,"OpenRouter"=>store.Settings.OpenRouterModel,"Kimi"=>store.Settings.KimiModel,_=>store.Settings.Model};
                CloudModelPicker(modelSettings,provider,selectedModel,v=>{switch(provider){case "Gemini":store.Settings.GeminiModel=v;break;case "NVIDIA":store.Settings.NvidiaModel=v;break;case "Groq":store.Settings.GroqModel=v;break;case "OpenRouter":store.Settings.OpenRouterModel=v;break;case "Kimi":store.Settings.KimiModel=v;break;default:store.Settings.Model=v;break;}},modelSaves);
                modelSettings.Controls.Add(Theme.Button("Model tercihini kaydet",()=>{if(engine.Busy){Message("Üretimin bitmesini bekle.");return;}foreach(var apply in modelSaves)apply();store.Save();Message(provider+" model tercihi kaydedildi.");},true,300));
                providerBox.Content.Controls.Add(modelSettings);serviceCards.Controls.Add(providerBox);
            }

        }



        var g = Theme.Flow(page.Width); g.BackColor = Theme.Panel; g.Padding = new Padding(20);

        g.Controls.Add(Theme.Button("ⓘ YouTube bağlantı rehberi", () => ShowConnectionHelp("YOUTUBE"), false, 310));

        g.Controls.Add(Theme.Label("YouTube / Google OAuth", 16, true, Theme.Teal, page.Width - 40, 45));

        g.Controls.Add(Theme.Label("Google Cloud'da YouTube Data API'yi etkinleştir; OAuth uygulama türü Desktop app olmalı. Kanal erişimi Google'ın resmî izin ekranında verilir.", 10, color: Theme.Muted, width: page.Width - 40, height: 60));

        var client = Theme.Input(store.Settings.GoogleClientId, width: 650); Field(g, "Google istemci kimliği", client); var secret = Theme.Input("", width: 650); secret.UseSystemPasswordChar = true; secret.PlaceholderText = store.Key("GOOGLE_CLIENT_SECRET") != "" ? "Kayıtlı • değiştirmek için yeni değeri yapıştır" : "İstemci gizli anahtarı"; Field(g, "Google istemci gizli anahtarı", secret);

        g.Controls.Add(Actions(Theme.Button("OAuth bilgilerini kaydet", () => { store.Settings.GoogleClientId = client.Text.Trim(); if (secret.Text != "") { store.Vault.Set("GOOGLE_CLIENT_SECRET", secret.Text); secret.Clear(); } store.Save(); Message("Google OAuth bilgileri kaydedildi."); }, true, 235), Theme.Button("YouTube kanalını bağla", () => _ = Safe(async () => { store.Settings.GoogleClientId = client.Text.Trim(); if (secret.Text != "") { store.Vault.Set("GOOGLE_CLIENT_SECRET", secret.Text); secret.Clear(); } store.Save(); await new YouTube(store).Connect(CancellationToken.None); store.Settings.ChannelName = await new YouTube(store).Channel(CancellationToken.None); store.Save(); Message("Bağlanan kanal: " + store.Settings.ChannelName); Navigate(current); }), false, 230)));

        g.Controls.Add(Theme.Button("YouTube kanalını doğrula", () => _ = Safe(async () => { store.Settings.ChannelName = await new YouTube(store).Channel(CancellationToken.None); store.Save(); Message("Doğrulanan kanal: " + store.Settings.ChannelName + " • " + store.Settings.YouTubeChannelId); Navigate(current); }), false, 310));

        accountGroup.Controls.Add(socials);accountGroup.Controls.Add(android);accountGroup.Controls.Remove(accountCards);var youtube=new StudioDisclosure("YouTube / Google OAuth",page.Width-50);youtube.Content.Controls.Add(g);accountGroup.Controls.Add(youtube);
        var developer=new StudioDisclosure("Geliştirici bağlantısı • isteğe bağlı",page.Width-50);developer.Content.Controls.Add(accountCards);accountGroup.Controls.Add(developer);

        var local = Theme.Flow(page.Width); local.BackColor = Theme.Panel; local.Padding = new Padding(20); local.Controls.Add(Theme.Label("Anahtarsız parçalar", 16, true, Theme.Teal, page.Width - 40, 45));

        local.Controls.Add(Theme.Button("ⓘ Ollama / Windows / Piper kurulumu", () => ShowConnectionHelp("OLLAMA"), false, 380));

        var voiceNames = LocalVoice.List(); string voiceText = voiceNames.Count > 0 ? string.Join(", ", voiceNames.Select(v => v.Name)) : "Türkçe Windows sesi bulunamadı. Türkçe konuşma bileşenini Windows dil ayarlarından yükle.";

        local.Controls.Add(Theme.Label("Windows sesleri: " + voiceText, 10, color: Theme.Muted, width: page.Width - 40, height: 60));

        local.Controls.Add(Theme.Label("Ollama: bilgisayarında kurulu ve model indirilmiş olmalı. Kullanım için API anahtarı gerekmez; bilgisayarın kaynaklarını kullanır.", 10, color: Theme.Muted, width: page.Width - 40, height: 50));

        local.Controls.Add(Theme.Button("Ollama bağlantısını dene", () => _ = Safe(async () => { using var h = new HttpClient { Timeout = TimeSpan.FromSeconds(8) }; using var res = await h.GetAsync("http://127.0.0.1:11434/api/tags"); res.EnsureSuccessStatusCode(); using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync()); Message("Kurulu modeller:\n" + string.Join("\n", doc.RootElement.GetProperty("models").EnumerateArray().Select(m => m.GetProperty("name").GetString()))); }), false, 250)); soundGroup.Controls.Add(local);

        var connectionSections=new List<StudioDisclosure>();
        foreach(var section in new[]{("YouTube ve hesaplar",accountGroup),("Yerel motorlar",localGroup),("Bulut yapay zekâ",cloudGroup),("Ses servisleri",soundGroup),("Görüntü ve araştırma kaynakları",stockGroup)}){
            var group=new StudioDisclosure(section.Item1,page.Width,connectionSections.Count==0);group.Content.Controls.Add(section.Item2);group.Controls.OfType<Button>().First().Click+=(_,_)=>{if(group.Expanded)foreach(var other in connectionSections)if(other!=group)other.SetExpanded(false);};connectionSections.Add(group);page.Controls.Add(group);
        }
        int headerWidth=CardChildren(page).OfType<Button>().First(b=>b.AccessibleName=="Bölüm: Hesaplar ve AI").Width;
        foreach(var section in CardChildren(page).OfType<StudioDisclosure>())section.LimitHeadingWidth(headerWidth);
        foreach(var action in CardChildren(page).OfType<StudioButton>().Where(b=>b.Parent is FlowLayoutPanel flow && flow is not StudioDisclosure && !Equals(flow.Tag,"equal-tabs") && b.Text is not ("i" or "ⓘ"))) {
            action.Tag="connection-action";action.Width=Math.Min(headerWidth,Math.Max(100,action.Parent!.Width-action.Parent.Padding.Horizontal));action.Height=48;action.Margin=new Padding(0,0,12,8);action.TextAlign=ContentAlignment.MiddleLeft;
        }

    }

    Control VecteezyCard(int width)

    {

        const string idKey = "VECTEEZY_ACCOUNT_ID", secretKey = "VECTEEZY_SECRET_KEY";

        var card = Theme.Card(width, 390);

        var title = Theme.Label("Vecteezy / stok video", 14, true, width: width - 90); title.Location = new(18, 13);

        var note = Theme.Label("İki bilgiyi burada kaydet • kota indirmeden önce kontrol edilir", 9, width: width - 40, height: 36); note.Location = new(18, 50);

        var idLabel = Theme.Label("Hesap kimliği (Account Id)", 9, true, width: width - 40, height: 24); idLabel.Location = new(18, 88);

        var id = Theme.Input(width: width - 40); id.Location = new(18, 114);

        var keyLabel = Theme.Label("Gizli anahtar (Secret key)", 9, true, width: width - 40, height: 24); keyLabel.Location = new(18, 157);

        var secret = Theme.Input(width: width - 40); secret.UseSystemPasswordChar = true; secret.Location = new(18, 183);

        var status = Theme.Label("", 9, true, width: width - 40, height: 28); status.Location = new(18, 351);

        void RefreshStatus() {

            bool hasId = store.Key(idKey) != "", hasSecret = store.Key(secretKey) != "";

            id.PlaceholderText = hasId ? "Hesap kimliği kayıtlı • değiştirmek için yenisini gir" : "Account Id gir";

            secret.PlaceholderText = hasSecret ? "Gizli anahtar kayıtlı • değiştirmek için yenisini gir" : "Secret key gir";

            status.Text = hasId && hasSecret ? "Bilgiler kayıtlı • Dene ile bağlantıyı kontrol et" : hasId ? "Gizli anahtar eksik" : hasSecret ? "Hesap kimliği eksik" : "Hesap kimliği ve gizli anahtar gerekli";

            status.ForeColor = hasId && hasSecret ? Theme.Text : Theme.Warning;

        }

        RefreshStatus();

        var actions = new TableLayoutPanel { Location = new(18,231), Width = width-36, Height=110, ColumnCount=2, RowCount=2, Margin=Padding.Empty, Padding=Padding.Empty };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50)); actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
        actions.RowStyles.Add(new RowStyle(SizeType.Percent,50)); actions.RowStyles.Add(new RowStyle(SizeType.Percent,50));

        var save = Theme.Button("Kaydet", () => _ = Safe(async () => {

            var account = id.Text.Trim() == "" ? store.Key(idKey).Trim() : id.Text.Trim();

            var key = secret.Text.Trim() == "" ? store.Key(secretKey).Trim() : secret.Text.Trim();

            if (!System.Text.RegularExpressions.Regex.IsMatch(account, @"^[0-9]{1,20}$")) throw new InvalidOperationException("Hesap kimliği yalnızca rakamlardan oluşmalı.");

            if (key == "") throw new InvalidOperationException("Gizli anahtarı gir.");

            if (id.Text.Trim() != "") store.Vault.Set(idKey, account);

            if (secret.Text.Trim() != "") store.Vault.Set(secretKey, key);

            id.Clear(); secret.Clear(); RefreshStatus(); UpdateFooter(); await Task.CompletedTask;

        }), true);

        var test = Theme.Button("Dene", () => _ = Safe(async () => {

            if (id.Text.Trim() != "" || secret.Text.Trim() != "") throw new InvalidOperationException("Önce değişiklikleri Kaydet ile kaydet.");

            status.Text = "Bağlantı kontrol ediliyor...";

            try { await TestConnection(idKey); status.Text = "Bağlantı doğrulandı"; status.ForeColor = Theme.Text; }

            catch { status.Text = "Bağlantı doğrulanamadı"; status.ForeColor = Theme.Warning; throw; }

        }));

        var portal = Theme.Button("Paneli aç", () => Theme.Open("https://www.vecteezy.com/account/developer-tools"));

        var remove = Theme.Button("Sil", () => _ = Safe(async () => {

            store.Vault.Set(idKey, ""); store.Vault.Set(secretKey, ""); id.Clear(); secret.Clear(); RefreshStatus(); UpdateFooter(); await Task.CompletedTask;

        }));

        actions.Controls.Add(save,0,0);actions.Controls.Add(test,1,0);actions.Controls.Add(portal,0,1);actions.Controls.Add(remove,1,1);
        foreach(Button button in actions.Controls){button.Dock=DockStyle.Fill;button.Margin=new Padding(0,0,8,5);}

        var help = Theme.Button("i", () => ShowConnectionHelp(secretKey), false, 46); help.Height = 40; help.Location = new(width - 64, 12); help.AccessibleName = "Vecteezy bağlantı yardımı";

        card.Controls.AddRange([title, note, idLabel, id, keyLabel, secret, actions, status, help]); return card;

    }

    async Task TestConnection(string key)

    {

        var value = store.Key(key); if (value == "") throw new InvalidOperationException("Önce anahtarı kaydet.");

        if (key is "VECTEEZY_ACCOUNT_ID" or "VECTEEZY_SECRET_KEY") { using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25)); var quota = await new Vecteezy(store).AccountInfo(timeout.Token); Message($"Vecteezy hesabı doğrulandı.\nArama: {quota.GeneralUsed} / {quota.GeneralLimit}\nİndirme: {quota.DownloadUsed} / {quota.DownloadLimit}\nProgram indirme sınırı: {store.Settings.VecteezyMonthlyDownloads}\nBu kontrol video indirmez."); return; }

        var aiProvider = key switch { "GEMINI_API_KEY" => "Gemini", "NVIDIA_API_KEY" => "NVIDIA", "GROQ_API_KEY" => "Groq", "OPENROUTER_API_KEY" => "OpenRouter", "OPENAI_API_KEY" => "OpenAI", "KIMI_API_KEY" => "Kimi", _ => "" };

        if (aiProvider != "") { await engine.External(ct => new Services(store, _ => {}).VerifyModel(aiProvider, ct)); Message(aiProvider + " seçili modeli küçük JSON yanıtını üretti. Bu kontrol az miktarda API kotası tüketir; gelecekteki bakiyeyi garanti etmez."); return; }

        if (key == "GOOGLE_TTS_API_KEY") { var voices = await new GoogleTts(store).Voices(CancellationToken.None); Message("Google TTS bağlantısı doğrulandı. Türkçe ses sayısı: " + voices.Length); return; }

        if (key == "GITHUB_TOKEN") { Message("GitHub hesabı: " + await new GitHub(store).User(CancellationToken.None)); return; }

        if (key == "TAVILY_API_KEY") throw new InvalidOperationException("Tavily anahtarı kayıtlı; canlı bağlantı ilk araştırmada doğrulanacak. Bu test arama kredisi tüketmez.");

        string url = key switch { "GEMINI_API_KEY" => "https://generativelanguage.googleapis.com/v1beta/models", "GROQ_API_KEY" => "https://api.groq.com/openai/v1/models", "OPENROUTER_API_KEY" => "https://openrouter.ai/api/v1/auth/key", "OPENAI_API_KEY" => "https://api.openai.com/v1/models", "KIMI_API_KEY" => "https://api.moonshot.ai/v1/models", "ELEVENLABS_API_KEY" => "https://api.elevenlabs.io/v1/voices", "PEXELS_API_KEY" => "https://api.pexels.com/v1/videos/search?query=nature&per_page=1", "PIXABAY_API_KEY" => "https://pixabay.com/api/videos/?per_page=3&q=nature&key=" + Uri.EscapeDataString(value), _ => "" };

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) }; using var req = new HttpRequestMessage(HttpMethod.Get, url);

        if (key == "GEMINI_API_KEY") req.Headers.Add("x-goog-api-key", value); else if (key == "ELEVENLABS_API_KEY") req.Headers.Add("xi-api-key", value); else if (key == "PEXELS_API_KEY") req.Headers.Add("Authorization", value); else if (key != "PIXABAY_API_KEY") req.Headers.Authorization = new("Bearer", value);

        using var res = await http.SendAsync(req); if (!res.IsSuccessStatusCode) throw new ProviderException(key.Replace("_API_KEY", ""), (int)res.StatusCode);

    }

    void ShowConnectionHelp(string key)

    {

        if (!ConnectionHelp.Guides.TryGetValue(key, out var guide)) return;

        using var dialog = Dialog("Bağlantı yardımı • " + guide.Title, 930, 780);

        var content = Theme.Input(guide.Steps.Replace("\n", "\r\n") + "\r\n\r\nAnahtarları sohbetlerde veya ekran görüntülerinde paylaşma. Menü adları değişebilir. Kaydet sonrasında anahtar kutusunun boşalması normaldir; kayıtlı bilgisi görünür.", true, 850); content.Dock = DockStyle.Fill; content.ReadOnly = true;

        var open = Theme.Button("Hizmet sayfasını aç", () => Theme.Open(guide.Url), true, 240); open.Dock = DockStyle.Bottom; dialog.Controls.Add(content); dialog.Controls.Add(open); dialog.ShowDialog(this);

    }

    bool poolImportBusy;

    int poolOffset;

    void PoolPage()

    {

        Title("Stok havuzu", "Kalıcı video kütüphanesi • önce yerel görüntü, sonra internet • kaynak ve kullanım izni korunur.");

        var pool = new MediaPool(store);

        page.Controls.Add(Theme.Button("Havuz videolarının kullanım haklarını onayla", () => { if (engine.Busy) { Message("Çalışan işlem bitince onayla."); return; } if (Theme.Alert(this, "Havuzdaki videoları bu kanalda kullanma hakkına sahip olduğunu doğruluyor musun? Kaynak ve lisansları önce kontrol et.", "Görüntü hakları", MessageBoxButtons.YesNo) != DialogResult.Yes) return; pool.ConfirmRights(); store.Save(); Navigate("Stok havuzu"); }, false, 420));

        page.Controls.Add(Actions(Theme.Button("+ Video ekle", () => _ = Safe(async () => {

            if (engine.Busy) throw new InvalidOperationException("Havuzu değiştirmeden önce üretimin bitmesini bekle.");

            using var files = new OpenFileDialog { Filter = "Videolar|*.mp4;*.mov;*.mkv;*.webm", Multiselect = true };

            if (files.ShowDialog(this) != DialogResult.OK) return;

            if (files.FileNames.Length > 20) { Message("Tek seferde en fazla 20 video eklenebilir. " + files.FileNames.Length + " video seçtin. Arşivini 20'şer videoluk gruplar halinde ekle; bu seçim henüz işlenmedi."); return; }

            using var dialog = Dialog("Videoları havuza ekle", 850, 700); var f = Theme.Flow(770); f.Dock = DockStyle.Fill; dialog.Controls.Add(f);

            var tags = Theme.Input("", width: 700); Field(f, "Arama etiketleri • İngilizce ve Türkçe • örnek: forest; nature; doğa", tags);

            var category = Theme.Combo(["Doğa", "Aile", "Sofra", "Yardımlaşma", "İbadet", "Şehir", "Genel"], "Genel", 700); Field(f, "Kategori", category);

            var source = Theme.Input("Kendi çekimim", width: 700); Field(f, "Kaynak URL veya açıklama", source);

            var creator = Theme.Input("", width: 700); Field(f, "Üretici / hak sahibi", creator);

            var license = Theme.Input("Kendi çekimim • kullanım hakkı bende", width: 700); Field(f, "Lisans URL veya kullanım hakkı açıklaması", license);

            var credit = Theme.Input("", true, 700); Field(f, "Kaynak yazısı • lisans gerektiriyorsa • YouTube açıklamasına eklenir", credit);

            var rights = Theme.Check("Bu videoları kanalımda kullanma hakkına sahibim", false); f.Controls.Add(rights);

            f.AutoSize = false; f.AutoScroll = true;

            using var cancellation = new CancellationTokenSource(); bool importing = false; bool finished = false;

            var status = Theme.Label($"{files.FileNames.Length} video seçildi. Dosyalar kopyalanır; kaynak dosyalar korunur.", 10, color: Theme.Teal, width: 700, height: 55); f.Controls.Add(status);

            var progress = new ProgressBar { Width = 700, Height = 22, Minimum = 0, Maximum = files.FileNames.Length }; f.Controls.Add(progress);

            Button? add = null; var cancel = Theme.Button("İptal", () => { cancellation.Cancel(); status.Text = "İptal ediliyor; tamamlanan dosyalar korunacak..."; }, false, 140);

            add = Theme.Button("Videoları ekle", () => _ = Safe(async () => {

                if (importing || finished) return;

                if (!rights.Checked || string.IsNullOrWhiteSpace(tags.Text) || string.IsNullOrWhiteSpace(source.Text) || string.IsNullOrWhiteSpace(license.Text)) { Message("Etiket, kaynak, kullanım izni ve hak onayı gerekli."); return; }

                var selected = files.FileNames; var tagText = tags.Text.Trim(); var categoryText = category.Text; var sourceText = source.Text.Trim(); var creatorText = creator.Text.Trim(); var licenseText = license.Text.Trim(); var creditText = credit.Text.Trim();

                importing = true; poolImportBusy = true; add!.Enabled = false; foreach (var control in new Control[] { tags, category, source, creator, license, credit, rights }) control.Enabled = false;

                int done = 0, failed = 0; var errors = new List<string>();

                var updates = new Progress<(int Count, string Name)>(x => { if (!dialog.IsDisposed) { progress.Value = x.Count; status.Text = $"{x.Count}/{selected.Length} • {x.Name}"; } });

                try {

                    await Task.Run(async () => {

                        foreach (var file in selected) {

                            cancellation.Token.ThrowIfCancellationRequested(); ((IProgress<(int, string)>)updates).Report((done + failed, "Kontrol ediliyor: " + Path.GetFileName(file)));

                            try { using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token); timeout.CancelAfter(TimeSpan.FromSeconds(45)); await Media.Duration(store, file, timeout.Token); await pool.AddAsync(file, tagText + "; " + Path.GetFileNameWithoutExtension(file), categoryText, sourceText, creatorText, licenseText, cancellation.Token, creditText); done++; }

                            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }

                            catch (Exception ex) { failed++; errors.Add(Path.GetFileName(file) + ": " + ex.Message); }

                            ((IProgress<(int, string)>)updates).Report((done + failed, Path.GetFileName(file)));

                        }

                    }, cancellation.Token);

                    status.Text = $"Tamamlandı: {done} işlendi, {failed} başarısız.";

                } catch (OperationCanceledException) { status.Text = $"İptal edildi: {done} işlendi, {failed} başarısız. Tamamlananlar korundu."; }

                finally { importing = false; poolImportBusy = false; finished = true; cancel.Enabled = false; add.Text = "Tamamlandı"; }

                if (errors.Count > 0) { var report = Path.Combine(store.Root, "pool-import-errors.txt"); File.WriteAllLines(report, errors); Message("Bazı dosyalar atlandı. Rapor: " + report); }

                Message(status.Text + " Pencereyi kapatınca havuz listesi yenilenecek.");

            }), true, 220);

            f.Controls.Add(Actions(add, cancel));

            dialog.FormClosing += (_, e) => { if (importing) { e.Cancel = true; cancellation.Cancel(); status.Text = "İptal ediliyor; işlem durunca pencereyi kapatabilirsin."; } };

            dialog.ShowDialog(this); Navigate("Stok havuzu"); await Task.CompletedTask;

        }), true, 190), Theme.Button("Klasörü tara / listeyi güncelle", ScanPoolFolder, false, 320), Theme.Button("Paketlerden havuza al", ImportPackagesToPool, false, 200), Theme.Button("Havuz klasörünü aç", () => { var folder = Path.Combine(store.Root, "media-pool"); Directory.CreateDirectory(folder); Theme.Open(folder); }, false, 220)));

        var items = pool.List(); page.Controls.Add(Theme.Label($"{items.Count} video • {items.Where(x => File.Exists(x.File)).Sum(x => new FileInfo(x.File).Length) / 1048576:N0} MB", 12, true, Theme.Teal, page.Width, 40));

        if (poolOffset >= items.Count) poolOffset = Math.Max(0, ((items.Count - 1) / 40) * 40);

        page.Controls.Add(Actions(Theme.Button("Önceki 40", () => { poolOffset = Math.Max(0, poolOffset - 40); Navigate("Stok havuzu"); }), Theme.Button("Sonraki 40", () => { if (poolOffset + 40 < items.Count) poolOffset += 40; Navigate("Stok havuzu"); })));

        page.Controls.Add(Theme.Label($"Gösterilen: {Math.Min(poolOffset + 1, items.Count)}–{Math.Min(poolOffset + 40, items.Count)} / {items.Count}", 10, color: Theme.Muted, width: page.Width));

        foreach (var item in items.OrderByDescending(x => x.Added).Skip(poolOffset).Take(40)) {

            var card = Theme.Card(page.Width, 190);

            var title = Theme.Label(item.Category + " • " + item.Tags, 12, true, width: page.Width - 45, height: 38); title.Location = new(18, 10);

            var info = Theme.Label($"Kullanım: {item.Uses} • Son kullanım: {item.LastUsed?.ToLocalTime().ToString("g") ?? "Henüz kullanılmadı"}\nKaynak: {item.Source}\nÜretici: {item.Creator} • İzin: {item.License}", 9, color: Theme.Muted, width: page.Width - 45, height: 72); info.Location = new(18, 47);

            var row = new FlowLayoutPanel { Location = new(18, 124), Width = page.Width - 40, Height = 56 };

            row.Controls.Add(Theme.Button("Önizle", () => { if (File.Exists(item.File)) Theme.Open(item.File); else Message("Havuz dosyası bulunamadı."); }, false, 120));

            row.Controls.Add(Theme.Button("Etiket / lisans", () => {

                if (engine.Busy) { Message("Üretim bitince düzenleyebilirsin."); return; }

                using var d = Dialog("Havuz bilgilerini düzenle", 800, 730); var f = Theme.Flow(730); f.Dock = DockStyle.Fill; d.Controls.Add(f);

                var tags = Theme.Input(item.Tags, width: 680); Field(f, "Etiketler • noktalı virgülle ayır", tags); var category = Theme.Input(item.Category, width: 680); Field(f, "Kategori", category); var general = Theme.Check("Genel fon seçiminde kullanılabilsin", item.GeneralBackground); f.Controls.Add(general);

                var source = Theme.Input(item.Source, width: 680); Field(f, "Kaynak URL / açıklama", source);

                var license = Theme.Input(item.License, width: 680); Field(f, "Lisans URL / kullanım izni", license);

                var credit = Theme.Input(item.Attribution, true, 680); Field(f, "Kaynak yazısı • YouTube açıklamasına eklenir", credit);

                f.AutoSize = false; f.AutoScroll = true;

                f.Controls.Add(Theme.Button("Kaydet", () => { if (tags.Text.Trim() == "" || source.Text.Trim() == "" || license.Text.Trim() == "") { Message("Etiket, kaynak ve lisans gerekli."); return; } var entries = pool.List(); var entry = entries.Single(x => x.Id == item.Id); entry.Tags = tags.Text.Trim(); entry.Category = category.Text.Trim(); entry.GeneralBackground = general.Checked; entry.Source = source.Text.Trim(); entry.License = license.Text.Trim(); entry.Attribution = credit.Text.Trim(); pool.Save(entries); d.DialogResult = DialogResult.OK; }, true)); d.ShowDialog(this); Navigate("Stok havuzu");

            }, false, 180));

            row.Controls.Add(Theme.Button("Havuzdan sil", () => { if (engine.Busy) { Message("Üretim bitince silebilirsin."); return; } if (Theme.Alert(this, "Bu video havuzdan silinsin mi? Paketlerdeki kopyalar korunur.", "Stok havuzu", MessageBoxButtons.YesNo) == DialogResult.Yes) { pool.Delete(item); Navigate("Stok havuzu"); } }, false, 160));

            card.Controls.AddRange([title, info, row]); page.Controls.Add(card);

        }

    }

    void SettingsPage()

    {

        Title("Kanal ve program", "Yeni videoların varsayılanları, güvenlik ve bakım. Videoya özel değişiklikleri Stüdyo’da yap.");
        page.Controls.Add(Actions(Theme.Button("Yedek ve bakım",Maintenance,false,220),Theme.Button("Çalışma klasörü",()=>Theme.Open(store.Root),false,220),Theme.Button("İşlem kayıtları",()=>Navigate("İşlem kayıtları"),false,220),Theme.Button("Program ve güncelleme",()=>Navigate("Program kaynakları"),false,240),Theme.Button("Kolay kurulum",ShowWelcomeSetup,false,220),Theme.Button("Eski program dosyalarını temizle",CleanOldProgramFiles,false,330)));

        var s = store.Settings; var saves = new List<Action>();

        var bar = Actions(Theme.Button("Tüm ayarları kaydet", () => { if (engine.Busy) { Message("Çalışan üretimi durdurduktan sonra ayarları değiştir."); return; } var before = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(s, Json.Options), Json.Options)!; try { foreach (var save in saves) save(); SettingsValidation.Check(s); store.Save(); _ = NvidiaModels.Start(store); store.ProviderBlocked.Clear(); store.PreferredProvider = ""; Message("Ayarlar kaydedildi."); UpdateFooter(); } catch (Exception ex) { foreach (var property in typeof(Settings).GetProperties().Where(p => p.CanWrite)) property.SetValue(s, property.GetValue(before)); Message("Ayarlar kaydedilmedi: " + ex.Message); } }, true, 230)); page.Controls.Add(bar);

        var tabs=Theme.Flow(page.Width);tabs.Tag="wide";tabs.SuspendLayout();
        var settingGroups=new Dictionary<string,StudioDisclosure>();
        var builders=new Dictionary<StudioDisclosure,Action<FlowLayoutPanel>>();
        var loaded=new HashSet<StudioDisclosure>();
        var fittedWidths=new Dictionary<StudioDisclosure,int>();
        bool fittingSettings=false;
        void BuildSection(StudioDisclosure section){
            if(loaded.Contains(section))return;
            section.SuspendLayout();section.Content.SuspendLayout();
            try{builders[section](section.Content);loaded.Add(section);}
            finally{section.Content.ResumeLayout(false);section.ResumeLayout(false);}
        }
        void AddSection(string name,Action<FlowLayoutPanel> builder){
            var section=new StudioDisclosure(name,page.Width);settingGroups[name]=section;builders[section]=builder;tabs.Controls.Add(section);
            section.Controls.OfType<Button>().First().Click+=(_,_)=>{
                if(!section.Expanded)return;
                tabs.SuspendLayout();
                try{foreach(var other in settingGroups.Values)if(other!=section)other.SetExpanded(false);BuildSection(section);FitSettings();}
                finally{tabs.ResumeLayout(true);}
            };
        }

        TextBox TextSetting(FlowLayoutPanel f, string title, string value, Action<string> set, bool multi = false) { var input = Theme.Input(value, multi, 650); Field(f, title, input); saves.Add(() => set(input.Text.Trim())); return input; }

        void Numeric(FlowLayoutPanel f, string title, decimal value, decimal min, decimal max, Action<decimal> set, int decimals = 0) { var n = Theme.Number(value, min, max, 450, decimals); Field(f, title, n); saves.Add(() => set(n.Value)); }

        void Check(FlowLayoutPanel f, string title, bool value, Action<bool> set) { var c = Theme.Check(title, value); f.Controls.Add(c); saves.Add(() => set(c.Checked)); }

        void Combo(FlowLayoutPanel f, string title, string value, string[] items, Action<string> set) { var c = Theme.Combo(items, value, 650); Field(f, title, c); saves.Add(() => set(c.Text)); }

        AddSection("Arayüz / ses", interfaceSettings=>{

        Check(interfaceSettings, "Tuşlarda klik sesi", s.UiSounds, v => { s.UiSounds = v; UiSound.Enabled = v; });

        Check(interfaceSettings,"Araştırma kaynaklarını YouTube açıklamasına ekle",s.IncludeResearchSources,v=>s.IncludeResearchSources=v);

        var dictionary=Theme.Input(s.PronunciationDictionary,true,650);Field(interfaceSettings,"Telaffuz sözlüğü • her satır: yazılış=okunuş",dictionary);saves.Add(()=>s.PronunciationDictionary=dictionary.Text.Trim());

        var clickVolume = Theme.Number((decimal)s.UiSoundVolume, 0, 1, 450, 2); Field(interfaceSettings, "Klik ses seviyesi • 0 sessiz, 1 en yüksek", clickVolume); saves.Add(() => { s.UiSoundVolume = (double)clickVolume.Value; UiSound.Volume = s.UiSoundVolume; });

        interfaceSettings.Controls.Add(Theme.Button("Klik sesini dene", () => { UiSound.Volume = (double)clickVolume.Value; UiSound.Click(true); }, false, 240));

        interfaceSettings.Controls.Add(Theme.Label("Kısa arayüz sesi yalnızca bilgisayar hoparlöründe çalar; video dosyalarına veya seslendirmeye eklenmez. Sistem sesi kapalıysa duyulmaz.", 10, color: Theme.Muted, width: 760, height: 70));

        var backup = Theme.Input(s.VideoBackupFolder, width: 650); Field(interfaceSettings, "Video yedek klasörü • harici disk / OneDrive önerilir", backup); saves.Add(() => s.VideoBackupFolder = backup.Text.Trim());

        interfaceSettings.Controls.Add(Theme.Button("Yedek klasörü seç", () => { using var picker = new FolderBrowserDialog(); if (picker.ShowDialog(this) == DialogResult.OK) backup.Text = picker.SelectedPath; }, false, 260));

        interfaceSettings.Controls.Add(Theme.Label("Seçildiğinde yüklemeden önce video, kapak, senaryo ve takvim kopyalanıp doğrulanır. Kopya başarısızsa yükleme durur. Boş bırakırsan ikinci yedek oluşturulmaz; yerel asıllar korunur. OneDrive bulut senkronizasyonunu kendi uygulaması yapar.", 10, color: Theme.Muted, width: 760, height: 110));

        
        });

        AddSection("Güvenlik", security=>{

        security.Controls.Add(Theme.Label("Açılışta uygulama şifresi zorunludur. Şifre PBKDF2 ile doğrulanır; düz metin saklanmaz. API anahtarları ayrı şifreli kasada tutulur. Bu yerel uygulama kilididir; e-posta doğrulaması veya Windows hesabı güvenliği değildir.", 10, color: Theme.Muted, width: 760, height: 100));

        security.Controls.Add(Theme.Button("Açılış şifresini değiştir", () => AppLock.Change(store.Root), true, 290));

        
        });

        AddSection("Kanal / araştırma", editorial=>{ TextSetting(editorial, "Kanal adı", s.ChannelName, v => s.ChannelName = v); TextSetting(editorial, "Hedef kitle", s.Audience, v => s.Audience = v);

        TextSetting(editorial, "Ek kaynak adresleri • her satıra bir HTTPS adresi • konuya uygun olmalı", s.SourceUrls, v => s.SourceUrls = v, true);

        Combo(editorial, "YouTube kategorisi", s.CategoryId, ["27", "28", "22", "24", "25"], v => s.CategoryId = v); editorial.Controls.Add(Theme.Label("27 Eğitim • 28 Bilim ve teknoloji • 22 Kişiler • 24 Eğlence • 25 Haber", 9, color: Theme.Muted, width: 760, height: 50));

        
        });

        AddSection("Google TTS kota", quota=>{

        Check(quota, "Google Cloud TTS API açma / kapatma yönetimine izin ver", s.CloudApiControl, v => s.CloudApiControl = v);

        Check(quota, "Sınır / ücret tespitinde Cloud TTS API'sini otomatik kapat", s.CloudAutoDisable, v => s.CloudAutoDisable = v);

        TextSetting(quota, "Yönetilecek Cloud proje NUMARASI • rakamlardan oluşur", s.CloudControlProject, v => s.CloudControlProject = v);

        quota.Controls.Add(Theme.Button("İsteğe bağlı API yönetim JSON'u ekle", () => { using var file = new OpenFileDialog { Filter = "Google hizmet hesabı|*.json" }; if (file.ShowDialog() != DialogResult.OK) return; var raw = File.ReadAllText(file.FileName); using var json = JsonDocument.Parse(raw); if (!json.RootElement.TryGetProperty("private_key", out _)) { Message("Hizmet hesabı JSON'u gerekli."); return; } store.Vault.Set("GOOGLE_CONTROL_JSON", raw); Message("API yönetim kimliği ayrı şifreli kayda alındı."); }, false, 360));

        quota.Controls.Add(Theme.Label("Cloud kapatma, seçili projede TTS kullanan diğer uygulamaları da etkiler. Bu izin varsayılan kapalıdır. Yönetim için yalnızca hedef projede Service Usage Admin rolü veya enable/disable izinli özel rol verilen ayrı hizmet hesabı kullan. Service Usage API etkin olmalı. Rapor kimliğine yönetim izni vermek zorunlu değildir.", 10, color: Theme.Warning, width: 760, height: 100));

        quota.Controls.Add(Actions(Theme.Button("Cloud projesinde TTS API KAPAT", () => _ = Safe(async () => { engine.Stop(); s.GoogleTtsEnabled = false; store.Save(); await new CloudUsage(store).Control(false, CancellationToken.None); Message("Google Cloud projesindeki TTS API kapatma işlemi tamamlandı."); }), false, 350), Theme.Button("Cloud projesinde TTS API AÇ", () => _ = Safe(async () => { if (engine.Busy) throw new InvalidOperationException("Çalışan işi durdur."); await new CloudUsage(store).Control(true, CancellationToken.None); s.GoogleTtsEnabled = true; store.Save(); Message("Cloud TTS API açıldı; kota koruması devam ediyor."); }), false, 320)));



        Check(quota, "Google TTS bu programda etkin", s.GoogleTtsEnabled, v => s.GoogleTtsEnabled = v);

        Check(quota, "Hesap raporu yoksa / eskiyse Google TTS üretimini engelle", s.CloudUsageRequired, v => s.CloudUsageRequired = v);

        TextSetting(quota, "Cloud Billing BigQuery tablosu • project.dataset.table", s.CloudBillingTable, v => s.CloudBillingTable = v);

        TextSetting(quota, "BigQuery veri konumu • EU veya US veya bölge", s.CloudBillingLocation, v => s.CloudBillingLocation = v);

        quota.Controls.Add(Theme.Label("Google fatura verisi gecikmeli gelir; anlık birebir sayaç değildir. Varsayılan koruma eksik/eski veride üretimi durdurur. Rapor + yerel sayım ihtiyatlı biçimde toplanır. Ücretsiz sınırın aşılmayacağı garanti edilemez; başka uygulamaları da kontrol et. BigQuery depolama/sorguları ayrıca ücret oluşturabilir; sorgu başına 100 MB tarama sınırı var.", 10, color: Theme.Warning, width: 760, height: 120));

        var reportText = Theme.Input("", true, 700); reportText.Height = 150; reportText.ReadOnly = true;

        void ShowReport(CloudUsage.Report report) { reportText.Text = report.Fetched == default ? "Hesap raporu henüz bağlı değil. Yerel sayaç Google faturası değildir." : "Son çekim: " + report.Fetched.ToLocalTime().ToString("g") + "\r\n" + string.Join("\r\n", report.Rows.Select(x => $"{x.Sku}: {x.Amount:N0} {x.Unit} • {x.Cost:N4} {x.Currency}")); }

        ShowReport(new CloudUsage(store).Read()); quota.Controls.Add(reportText);

        quota.Controls.Add(Theme.Button("Salt okunur hizmet hesabı JSON ekle", () => { using var file = new OpenFileDialog { Filter = "Google hizmet hesabı|*.json" }; if (file.ShowDialog() != DialogResult.OK) return; var raw = File.ReadAllText(file.FileName); using var json = JsonDocument.Parse(raw); if (!json.RootElement.TryGetProperty("private_key", out _) || !json.RootElement.TryGetProperty("client_email", out _)) { Message("Hizmet hesabı JSON dosyası gerekli; OAuth istemci JSON'u değil."); return; } store.Vault.Set("GOOGLE_BILLING_JSON", raw); Message("Kimlik dosyası şifreli kasaya kaydedildi."); }, false, 370));

        quota.Controls.Add(Theme.Button("Google hesap kullanımını güncelle", () => _ = Safe(async () => { if (engine.Busy) throw new InvalidOperationException("Çalışan işi durdur; önce tablo/konum ayarını kaydet."); ShowReport(await new CloudUsage(store).Refresh(CancellationToken.None)); }), true, 360));

        quota.Controls.Add(Theme.Button("Google TTS'yi durdur", () => { engine.Stop(); s.GoogleTtsEnabled = false; store.Save(); Message("Google TTS bu programda kapatıldı. Gönderilmiş isteği veya diğer uygulamaları geri alamaz. Yeniden açmak için bu ekrandaki etkin ayarını kaydet."); Navigate("Yönetim ayarları"); }, false, 290));

        quota.Controls.Add(Theme.Button("ⓘ Kota takip kurulumu", () => Message("Google Cloud Billing → Billing export → Standard usage cost BigQuery export aç. Veri tablosu oluştuktan sonra project.dataset.table adını ve konumunu buraya yaz. Monitoring için ayrı hizmet hesabı oluştur: sorgu projesinde BigQuery Job User, yalnızca ilgili dataset üzerinde BigQuery Data Viewer ver. JSON anahtarını buradan şifreli kasaya içe aktar. BigQuery API etkin olmalı. Owner/Editor izni verme. Veri aktarımı tamamlanmadan rapor boş olabilir; program bunu sıfır kullanım saymaz. Tabloyu bütün Cloud Billing hesabının verisini içerecek şekilde seç. Aylık rapor ses/SKU bazında gösterilir. Veriler gecikmeli olduğundan bu sistem ücret sıfır garantisi değildir."), false, 290));

        
        });

        AddSection("Ses / müzik", voice=>{ Combo(voice, "Ses sağlayıcısı", s.VoiceProvider, ["Windows / ücretsiz", "Piper / ücretsiz", "XTTS v2 / harici", "Google Cloud TTS", "ElevenLabs / isteğe bağlı"], v => s.VoiceProvider = v);
        var xttsUrl = Theme.Input(s.XttsUrl, width: 760); Field(voice, "XTTS v2 sunucu adresi", xttsUrl); saves.Add(() => s.XttsUrl = xttsUrl.Text.Trim());
        var xttsSpeaker = Theme.Input(s.XttsSpeaker, width: 760); Field(voice, "XTTS konuşmacı adı / sunucudaki referans WAV yolu", xttsSpeaker); saves.Add(() => s.XttsSpeaker = xttsSpeaker.Text.Trim());
        voice.Controls.Add(Theme.Button("XTTS v2 Kurulum Bilgisi (i)", () => Theme.Alert(this, XttsTts.Help, "Harici XTTS v2"), false, 320));
        voice.Controls.Add(Theme.Button("XTTS örnek sesini dinle", () => _ = Safe(async () => { var copy = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(s))!; copy.XttsUrl = xttsUrl.Text.Trim(); copy.XttsSpeaker = xttsSpeaker.Text.Trim(); copy.VoiceProvider = "XTTS v2 / harici"; var output = Path.Combine(store.Root, "xtts-preview.wav"); using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3)); await LocalVoice.SpeakWithFallback("Merhaba. Bu, Türkçe XTTS seslendirme denemesidir.", output, copy, timeout.Token); Process.Start(new ProcessStartInfo(output) { UseShellExecute = true }); }), false, 320));


        var googleVoice = Theme.Combo([s.GoogleTtsVoice], s.GoogleTtsVoice, 650); Field(voice, "Google TTS Türkçe sesi", googleVoice); saves.Add(() => s.GoogleTtsVoice = googleVoice.Text);

        Numeric(voice, "Google TTS aylık yerel karakter sınırı", s.GoogleTtsMonthlyLimit, 1000, 4000000, v => s.GoogleTtsMonthlyLimit = (int)v);

        Check(voice, "Google TTS hata / sınır durumunda Windows ve Piper denensin", s.GoogleTtsFallback, v => s.GoogleTtsFallback = v);

        var google = new GoogleTts(store); var usage = google.ReadUsage();

        voice.Controls.Add(Theme.Label($"Bu cihazda bu ay ayrılan karakter: {usage.Characters:N0}. Sayaç başka programları / cihazları kapsamaz; Google faturası değildir. Varsayılan sınır 900.000. Google Cloud faturalandırması açık olmalı.", 9, color: Theme.Warning, width: 760, height: 80));

        var googleStatus = Theme.Label("Türkçe sesleri getirmek için düğmeye bas. Liste ayrı seçim penceresinde açılır.", 10, color: Theme.Muted, width: 760, height: 55);

        voice.Controls.Add(googleStatus);

        Button? fetchGoogle = null;

        fetchGoogle = Theme.Button("Google Türkçe seslerini getir", () => _ = Safe(async () => {

            fetchGoogle!.Enabled = false; fetchGoogle.Text = "Sesler getiriliyor..."; googleStatus.Text = "Google'a bağlanılıyor..."; googleStatus.ForeColor = Theme.Text;

            try {

                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));

                var list = await google.Voices(timeout.Token);

                if (list.Length == 0) throw new InvalidOperationException("Google Türkçe ses listesi boş döndü.");

                googleVoice.BeginUpdate(); googleVoice.Items.Clear(); googleVoice.Items.AddRange(list);

                googleVoice.SelectedItem = list.Contains(s.GoogleTtsVoice) ? s.GoogleTtsVoice : list[0]; googleVoice.EndUpdate(); googleVoice.Invalidate();

                googleStatus.Text = $"{list.Length} Türkçe ses bulundu. Listeden seçebilirsin."; googleStatus.ForeColor = Theme.Text;

                using var dialog = Dialog("Google TTS • Türkçe ses seç", 780, 560);

                var picker = new ListBox { Dock = DockStyle.Fill, BackColor = Theme.Elevated, ForeColor = Theme.Text, Font = Theme.Font(12), IntegralHeight = false };

                picker.Items.AddRange(list); picker.SelectedItem = googleVoice.SelectedItem;

                var select = Theme.Button("Seçili sesi kullan", () => { if (picker.SelectedItem is string name) { googleVoice.SelectedItem = name; googleVoice.Invalidate(); s.GoogleTtsVoice = name; store.Save(); googleStatus.Text = "Seçildi ve kaydedildi: " + name; dialog.DialogResult = DialogResult.OK; } }, true, 230); select.Dock = DockStyle.Bottom;

                dialog.Controls.Add(picker); dialog.Controls.Add(select); picker.DoubleClick += (_, _) => select.PerformClick(); dialog.ShowDialog(this);

            }

            catch (Exception ex) { googleStatus.Text = ex is OperationCanceledException ? "Google ses listesi 25 saniyede alınamadı. Bağlantıyı tekrar dene." : "Ses listesi alınamadı: " + ex.Message; googleStatus.ForeColor = Theme.Text; googleStatus.ForeColor = Theme.Warning; Message(googleStatus.Text); }

            finally { fetchGoogle.Enabled = true; fetchGoogle.Text = "Google Türkçe seslerini getir"; }

        }), false, 270);

        voice.Controls.Add(Actions(Theme.Button("Google Cloud kullanım paneli", () => Theme.Open("https://console.cloud.google.com/apis/api/texttospeech.googleapis.com/metrics"), false, 310), Theme.Button("Google Cloud fatura raporu", () => Theme.Open("https://console.cloud.google.com/billing"), false, 280)));

        voice.Controls.Add(Actions(fetchGoogle, Theme.Button("Seçili Google sesini dinle", () => _ = Safe(async () => { if (engine.Busy) throw new InvalidOperationException("Üretimin bitmesini bekle."); var settings = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(s, Json.Options), Json.Options)!; settings.GoogleTtsVoice = googleVoice.Text; var path = Path.Combine(store.Root, "google-tts-preview.wav"); await google.Speak("Merhaba. Nur Kapısı kanalına hoş geldiniz. Bu, Türkçe seslendirme örneğidir.", path, settings, CancellationToken.None); Theme.Open(path); }), false, 260)));

        var voiceList = LocalVoice.List(); var voiceCombo = Theme.Combo(voiceList.Count == 0 ? ["Türkçe ses kurulu değil"] : voiceList.Select(v => v.Name).ToArray(), voiceList.FirstOrDefault(v => v.Id == s.WindowsVoice).Name ?? voiceList.FirstOrDefault().Name ?? "Türkçe ses kurulu değil", 650); Field(voice, "Windows Türkçe sesi", voiceCombo); saves.Add(() => s.WindowsVoice = voiceList.FirstOrDefault(v => v.Name == voiceCombo.Text).Id ?? "");

        voice.Controls.Add(Theme.Button("ⓘ Piper Modeli Nedir?", () => Theme.Alert(this, ButtonHelp.Explain("Piper ses modelini seç"), "Piper Modeli"), false, 290));
        var piperExe = Theme.Input(s.PiperPath, width: 650); Field(voice, "Piper programı • Windows sesi yoksa yerel alternatif", piperExe); saves.Add(() => s.PiperPath = piperExe.Text.Trim());

        voice.Controls.Add(Theme.Button("Piper programını seç", () => { using var d = new OpenFileDialog { Filter = "Piper|*.exe" }; if (d.ShowDialog() == DialogResult.OK) piperExe.Text = d.FileName; }, false, 240));

        var piperModel = Theme.Input(s.PiperModel, width: 650); Field(voice, "Türkçe Piper ONNX modeli • .onnx.json aynı klasörde olmalı", piperModel); saves.Add(() => s.PiperModel = piperModel.Text.Trim());

        voice.Controls.Add(Theme.Button("Piper ses modelini seç", () => { using var d = new OpenFileDialog { Filter = "Ses modeli|*.onnx" }; if (d.ShowDialog() == DialogResult.OK) piperModel.Text = d.FileName; }, false, 240));

        voice.Controls.Add(Theme.Label("Piper modeli ayrı indirilir. Her modelin kullanım lisansı farklı olabilir; para kazanılan içerikte kullanmadan önce model lisansını doğrula. Windows sesi çalışmazsa kayıtlı Piper modeli denenir.", 9, color: Theme.Muted, width: 760, height: 75));

        voice.Controls.Add(Theme.Button("Shorts için normal video sesini kullan", () => { if (engine.Busy) { Message("Önce üretimi durdur."); return; } s.ShortVoiceOverride = false; s.ShortVoiceProvider = "Normal videoyla aynı"; store.Save(); Navigate("Yönetim ayarları"); Message("Shorts artık normal videonun ses sağlayıcısını ve ses seçimini kullanacak. Kaydedilmiş diğer ayarlar korunur."); }, true, 390));

        voice.Controls.Add(Theme.Label("Normal video Google TTS kullanırken Shorts ayrı Windows/Piper seçiliyse Google denenmez. Ayrı seçim istemiyorsan yukarıdaki tuşla normal ses ayarını kullan.", 10, color: Theme.Warning, width: 760, height: 65));

        Check(voice, "Shorts için ayrı ses ayarlarını kullan", s.ShortVoiceOverride, v => s.ShortVoiceOverride = v);

        Combo(voice, "Shorts ses sağlayıcısı", s.ShortVoiceProvider, ["Normal videoyla aynı", "Windows / ücretsiz", "Piper / ücretsiz", "XTTS v2 / harici", "Google Cloud TTS", "ElevenLabs / isteğe bağlı"], v => s.ShortVoiceProvider = v);

        TextSetting(voice, "Shorts ElevenLabs ses kimliği • ayrı ses seçilebilir", s.ShortVoiceId, v => s.ShortVoiceId = v);

        Numeric(voice, "Shorts konuşma hızı", (decimal)s.ShortVoiceSpeed, .7m, 1.2m, v => s.ShortVoiceSpeed = (double)v, 2);

        Numeric(voice, "Shorts müzik seviyesi", (decimal)s.ShortMusicVolume, 0, .5m, v => s.ShortMusicVolume = (double)v, 2);

        var elevenVoice = TextSetting(voice, "ElevenLabs ses kimliği", s.VoiceId, v => s.VoiceId = v); TextSetting(voice, "ElevenLabs ses modeli", s.VoiceModel, v => s.VoiceModel = v); Numeric(voice, "Konuşma hızı", (decimal)s.VoiceSpeed, .7m, 1.2m, v => s.VoiceSpeed = (double)v, 2);

        voice.Controls.Add(Theme.Button("ElevenLabs seslerini listele", () => _ = Safe(async () => { var list = await new Services(store, _ => { }).Voices(CancellationToken.None); using var d = Dialog("Ses çeşitleri", 800, 650); var text = Theme.Input(string.Join("\r\n", list.Select(v => v.Name + "  →  " + v.Id)), true, 720); text.Dock = DockStyle.Fill; text.ReadOnly = true; d.Controls.Add(text); d.ShowDialog(this); }), false, 270));

        var music = Theme.Input(s.MusicPath, width: 650); Field(voice, "Müzik dosyası", music); saves.Add(() => s.MusicPath = music.Text.Trim()); voice.Controls.Add(Theme.Button("Müzik dosyası seç", () => { using var d = new OpenFileDialog { Filter = "Ses|*.mp3;*.wav;*.m4a;*.ogg" }; if (d.ShowDialog() == DialogResult.OK) music.Text = d.FileName; }, false, 240));

        Combo(voice, "Müzik kitaplığı etiketi • seçilen dosyayı sınıflandırır", s.MusicStyle, ["Sakin / ambient", "Sinematik", "Belgesel", "Elektronik", "Lo-fi", "Gerilim"], v => s.MusicStyle = v); Numeric(voice, "Müzik seviyesi • konuşmada otomatik kısılır", (decimal)s.MusicVolume, 0, .5m, v => s.MusicVolume = (double)v, 2);

        
        var googleLast=voice.Controls.OfType<FlowLayoutPanel>().First(p=>p.Controls.Contains(fetchGoogle));
        CompactUi.Group(voice,"Google sesleri ve kullanım",CompactUi.LabelFor(voice,googleVoice),googleLast,s.VoiceProvider.Contains("Google"));
        var piperLast=voice.Controls.OfType<Label>().First(l=>l.Text.StartsWith("Piper modeli ayrı indirilir",StringComparison.Ordinal));
        CompactUi.Group(voice,"Piper kurulumu",CompactUi.LabelFor(voice,piperExe),piperLast,s.VoiceProvider.Contains("Piper"));
        var shortStart=voice.Controls.OfType<Button>().First(b=>b.Text=="Shorts için normal video sesini kullan");
        var elevenStart=CompactUi.LabelFor(voice,elevenVoice);
        var shortEnd=voice.Controls[voice.Controls.IndexOf(elevenStart)-1];
        CompactUi.Group(voice,"Shorts ses ayarları",shortStart,shortEnd,s.ShortVoiceOverride);


        });

        AddSection("Stok havuzu", poolSettings=>{

        Check(poolSettings, "Önce yerel stok havuzunda ara", s.PoolEnabled, v => s.PoolEnabled = v);

        Check(poolSettings, "Stok servisi hata verirse yerel havuza dön • tekrar beklemesini esnet", s.PoolFallback, v => s.PoolFallback = v);

        Check(poolSettings, "Özel görüntü bulunamazsa sade doğa / deniz / uzay fonlarını kullan", s.StockGeneralFallback, v => s.StockGeneralFallback = v);

        poolSettings.Controls.Add(Theme.Button("Türkçe / İngilizce fon kelimelerini göster", () => { using var dialog = Dialog("Genel fon arama kütüphanesi", 720, 700); var text = Theme.Input(StockSearch.Catalog, true, 640); text.Dock = DockStyle.Fill; text.ReadOnly = true; dialog.Controls.Add(text); dialog.ShowDialog(this); }, false, 420));

        Check(poolSettings, "İndirilen görüntüleri kalıcı havuza kaydet", s.PoolAutoSave, v => s.PoolAutoSave = v);

        Numeric(poolSettings, "Aynı görüntünün tekrar kullanımı için bekleme • saat", s.PoolRepeatHours, 0, 720, v => s.PoolRepeatHours = (int)v);

        poolSettings.Controls.Add(Theme.Label("Aynı üretimde görüntü tekrarı engellenir. Servis hatasında havuza dönüş açıksa paketler arası bekleme esnetilebilir. Genel fon açıksa doğa / deniz / uzay veya genel fon olarak işaretlenen arşiv görüntüsü seçilebilir. Yerel havuz bu bilgisayardaki üretimde kullanılır.", 10, color: Theme.Muted, width: 760, height: 100));

        
        });

        AddSection("Görüntü / altyazı", visuals=>{ TextSetting(visuals, "Stok sağlayıcı sırası • Pexels,Pixabay,Vecteezy", s.StockOrder, v => s.StockOrder = v);

        Check(visuals, "Vecteezy stok alternatifi etkin", s.VecteezyEnabled, v => s.VecteezyEnabled = v);

        Numeric(visuals, "Vecteezy aylık indirme sınırı", s.VecteezyMonthlyDownloads, 1, 5000, v => s.VecteezyMonthlyDownloads = (int)v);

        visuals.Controls.Add(Theme.Label("Vecteezy: hesap kotası her aramadan ve indirmeden önce okunur. Varsayılan sınır 450 indirmedir; hesabındaki daha düşük sınır önceliklidir. Kota yoksa yerel havuza / diğer sağlayıcıya geçilir. Ücretsiz API planını panelinden doğrula; ücretli hesapların faturası uygulama tarafından değiştirilemez.", 10, color: Theme.Warning, width: 760, height: 100));

        var ffmpeg = Theme.Input(s.FfmpegPath, width: 650); Field(visuals, "FFmpeg yolu • paket motoru otomatik bulunur", ffmpeg); saves.Add(() => s.FfmpegPath = ffmpeg.Text.Trim()); visuals.Controls.Add(Theme.Button("Video motoru seç", () => { using var d = new OpenFileDialog { Filter = "FFmpeg|ffmpeg.exe" }; if (d.ShowDialog() == DialogResult.OK) ffmpeg.Text = d.FileName; }, false, 240));

        Numeric(visuals, "Normal video altyazı boyutu", s.LongFontSize, 24, 80, v => s.LongFontSize = (int)v); Numeric(visuals, "Shorts altyazı boyutu", s.ShortFontSize, 36, 110, v => s.ShortFontSize = (int)v);

        Combo(visuals, "Altyazı rengi", s.SubtitleColor, ["FFFFFF", "CB E0 54".Replace(" ", ""), "6BB4FF"], v => s.SubtitleColor = v);

        Combo(visuals, "Kodlama hızı", s.EncodingPreset, ["ultrafast", "veryfast", "fast", "medium", "slow"], v => s.EncodingPreset = v); Numeric(visuals, "Görüntü sıkıştırma • düşük değer daha yüksek kalite", s.Crf, 16, 28, v => s.Crf = (int)v); Numeric(visuals, "Kare hızı", s.FramesPerSecond, 24, 60, v => s.FramesPerSecond = (int)v);

        visuals.Controls.Add(Theme.Label("Sahneleri tek tek değiştirmek, stok sorgularını düzenlemek ve yerel görüntü seçmek için içerik paketindeki Sahne editörü'nü aç.", 10, color: Theme.Muted, width: 760, height: 70));

        
        });

        AddSection("Otonomi / denetim", autonomy=>{ Numeric(autonomy, "Günlük tamamlanan paket sınırı", s.DailyProductionLimit, 1, 30, v => s.DailyProductionLimit = (int)v);

        Check(autonomy, "Taslak paketleri otomatik araştır ve yaz", s.AutoResearch, v => s.AutoResearch = v); Check(autonomy, "Senaryosu hazır paketleri otomatik üret", s.AutoProduce, v => s.AutoProduce = v); Check(autonomy, "Kullanıcının ONAYLADIĞI paketleri otomatik yükle / planla", s.AutoPublish, v => s.AutoPublish = v);

        autonomy.Controls.Add(Theme.Label("Bu sürümde yayın onayı kullanıcıdan gelir. Kalite veya kaynak uyarısı olan paket otomatik yayınlanamaz. Başarısız iş beklemeye alınır; sınırsız yeniden deneme yapılmaz. Yerel otonomi için Windows uygulaması açık kalmalıdır.", 10, color: Theme.Muted, width: 760, height: 100));

        autonomy.Controls.Add(Theme.Button("Üretimi hemen durdur", () => engine.Stop(), false, 240));
        });

        page.Controls.Add(tabs);

        void FitSettings(){
            if(fittingSettings)return;fittingSettings=true;
            try{foreach(var section in settingGroups.Values){
                int width=page.Width-12;
                if(section.Width!=width)section.FitWidth(width);
                if(!section.Expanded||!loaded.Contains(section)||fittedWidths.GetValueOrDefault(section)==width)continue;
                fittedWidths[section]=width;section.Content.SuspendLayout();
                try{FitFormFields(section.Content,page.Width-36);}finally{section.Content.ResumeLayout(true);}
            }}finally{fittingSettings=false;}
        }
        tabs.SizeChanged+=(_,_)=>FitSettings();
        var first=settingGroups.Values.First();first.SetExpanded(true);BuildSection(first);FitSettings();tabs.ResumeLayout(true);

    }

    void GitHubPage()

    {

        Title("Program kaynakları", "GitHub yalnızca programın kaynak kodunu saklamak ve uygulamayı derlemek için kullanılır. Video üretimi ve YouTube yüklemesi bu bilgisayarda yapılır.");

        var f = Theme.Flow(page.Width); var status = Theme.Label("Video işlemleri için GitHub hesabı gerekmez.", 11, color: Theme.Teal, width: page.Width - 30, height: 65);

        var repo = Theme.Input(store.Settings.Repo, width: 650); Field(f, "Program kaynak deposu • kullanıcı/depo", repo);

        var branch = Theme.Input(store.Settings.GitHubRef, width: 650); Field(f, "Kaynak dalı", branch);

        bool Save() { store.Settings.Repo = repo.Text.Trim(); store.Settings.GitHubRef = branch.Text.Trim(); store.Save(); return true; }

        f.Controls.Add(Theme.Button("Kaynak depo ayarlarını kaydet", () => { Save(); status.Text = "Program kaynak deposu kaydedildi."; }, false, 330));

        f.Controls.Add(Theme.Button("Program kaynaklarını GitHub’a gönder", () => _ = Safe(async () => {

            if (!Save()) return;

            if (Theme.Alert("Program kaynakları seçili depoya gönderilecek. Devam edilsin mi?", "Program kaynakları", MessageBoxButtons.YesNo) != DialogResult.Yes) return;

            await engine.External(ct => new GitHub(store).PushSource(ct)); status.Text = "Program kaynakları gönderildi.";

        }), false, 370));

        f.Controls.Add(status); page.Controls.Add(f);

    }

    void Logs()

    {

        Title("İşlem kayıtları", "Kullanılan sağlayıcı, üretim aşamaları, durdurma ve yayın kararları paket bazında kaydedilir. Anahtar değerleri kayda yazılmaz.");

        foreach (var ep in store.Episodes.OrderByDescending(x => x.Created))

        {

            page.Controls.Add(Theme.Label(ep.Topic, 14, true, Theme.Teal, page.Width, 40)); var text = Theme.Input(string.Join("\r\n", ep.Events), true, page.Width - 12); text.Height = 210; text.ReadOnly = true; page.Controls.Add(text);

        }

        if (store.Episodes.Count == 0) Empty("Henüz işlem yok", "İlk içerik paketini oluşturduğunda aşamalar burada görünür.");

    }

    public void CaptureUploadStatus(string folder) { Directory.CreateDirectory(folder); ShowUploadStatus(Path.Combine(folder, "upload-status.png")); }

    void ShowUploadStatus() => ShowUploadStatus(null);

    void ShowUploadStatus(string? screenshot)

    {

        using var dialog = Dialog("Video yükleme durum kontrol", 1120, 700);

        var summary = Theme.Label("Son durum alınıyor…", 10, color: Theme.Teal, width: 1040, height: 100); summary.Dock = DockStyle.Top;

        var grid = Grid(); grid.ReadOnly = true; grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells; grid.Dock = DockStyle.Fill;

        grid.ColumnHeadersVisible = true;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.EnableResizing;
        grid.ColumnHeadersHeight = 58;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Theme.Text;
        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Theme.Text;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Theme.Elevated;
        grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
        foreach (var column in new[] { "İÇERİK\nPaket / video adı", "DURUM\nYouTube yayın durumu", "TARİHLER\nYayın tarihi (Türkiye)", "TARİHLER\nSon YouTube kontrolü (Türkiye)" }) grid.Columns.Add(column, column);
        var explanations = Theme.Label("İÇERİK: Videonun adı.  DURUM: Yayında, planlı, özel veya yüklenmemiş.\nTARİHLER: Yayın ve son kontrol zamanı (Türkiye saati). — işareti: Tarih yok veya henüz alınmadı.", 9, width: 1040, height: 52);
        explanations.Dock = DockStyle.Top;

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 65 };

        var refresh = Theme.Button("Durumu yenile", () => { }, true, 210);

        buttons.Controls.Add(refresh); buttons.Controls.Add(Theme.Button("Kapat", dialog.Close, false, 150));

        async Task LoadStatus()

        {

            refresh.Enabled = false;

            try {

                await RefreshRemote(); if (dialog.IsDisposed) return;

                summary.Text = remoteSummary; grid.Rows.Clear();

                foreach (var episode in store.Episodes.OrderByDescending(e => e.Created)) {

                    if (episode.Videos.Count == 0) grid.Rows.Add(episode.Topic, "Video henüz üretilmedi", "—", "—");

                    foreach (var video in episode.Videos) grid.Rows.Add(video.Title,

                        video.YouTubeId == "" ? "Yerelde / yüklenmedi" : video.YouTubeState == "" ? "Son durum alınmadı" : video.YouTubeState,

                        (video.RemotePublishAt ?? video.PublishAt)?.ToOffset(TimeSpan.FromHours(3)).ToString("dd.MM.yyyy HH:mm") ?? "—",

                        video.YouTubeCheckedAt?.ToOffset(TimeSpan.FromHours(3)).ToString("dd.MM.yyyy HH:mm") ?? "—");

                }

            } catch (Exception ex) { if (!dialog.IsDisposed) summary.Text = "Durum alınamadı: " + ex.Message; }

            finally { if (!refresh.IsDisposed) refresh.Enabled = true; }

        }

        refresh.Click += async (_, _) => await LoadStatus(); dialog.Shown += async (_, _) => {

            await LoadStatus();

            if (screenshot != null) { await Task.Delay(200); using var bitmap = new Bitmap(dialog.Width, dialog.Height); dialog.DrawToBitmap(bitmap, new Rectangle(Point.Empty, dialog.Size)); bitmap.Save(screenshot); dialog.Close(); }

        };

        dialog.Controls.Add(grid); dialog.Controls.Add(explanations); dialog.Controls.Add(summary); dialog.Controls.Add(buttons);

        dialog.ShowDialog(this);

    }

    Task RefreshRemote()=>RefreshRemote(false);
    async Task RefreshRemote(bool fullScan)

    {

        if (remoteRefreshing || engine.Busy) return;

        remoteRefreshing = true; remoteProgress="Lütfen bekleyin • YouTube ve yerel projeler taranıyor…";using var timeout = new CancellationTokenSource(fullScan?TimeSpan.FromMinutes(5):TimeSpan.FromSeconds(45));remoteScanStop=timeout;

        if (!IsDisposed && current is "İçerik paketleri" or "Videolarım") Navigate(current);

        var notes = new List<string>();

        try {

            try { if (store.Key("YOUTUBE_REFRESH_TOKEN") != "") {
                    if(fullScan){var catalog=await new YouTubeStatistics(store).Fetch(28,text=>{remoteProgress=text;if(remoteProgressLabel?.IsDisposed==false)remoteProgressLabel.Text="Lütfen bekleyin • "+text;},timeout.Token,false);Json.Write(Path.Combine(store.Root,"cache","youtube-library.json"),catalog);notes.Add(catalog.Videos.Count+" YouTube videosu listelendi");}
                    await new YouTube(store).RefreshVideoStates(timeout.Token); notes.Add("YouTube durumları güncellendi");
                } else notes.Add("YouTube bağlantısı eksik • Merkez’den kanalını bağla"); }

            catch (OperationCanceledException) {notes.Add("Tarama iptal edildi veya zaman sınırı doldu; son başarılı liste korundu.");}
              catch (Exception ex) { notes.Add("YouTube güncellenemedi; önceki liste korundu: " + ex.Message); }

            VideoQueue.Reconcile(store);

            var videos = store.Episodes.SelectMany(e => e.Videos).ToList();

            remoteSummary = $"Kayıtlı videolar: {videos.Count(v => v.YouTubeId != "")} yüklendi • {videos.Count(v => v.YouTubeState == "Yayında")} yayında • {videos.Count(v => v.YouTubeState == "Planlı bekliyor")} planlı bekliyor • {videos.Count(v => v.YouTubeState.StartsWith("Sorun") || v.YouTubeState.StartsWith("Bulunamadı"))} sorun • {videos.Count(v => v.YouTubeId == "")} yerelde.\n" + string.Join(" • ", notes);

        } finally { remoteRefreshing = false;remoteScanStop=null; if (!IsDisposed && current is "Stüdyo" or "Yayın takvimi" or "İçerik paketleri" or "Videolarım" or "Video hazırla" or "Üretim ve yayın planı") Navigate(current); }

    }

    async Task Tick()

    {
        assistantActions.Observe();

        if (queueRunning) return;

        if (VideoQueue.Items(store).Any(e => e.QueueAuthorized && e.QueueState != "Planlandı") && !engine.Busy && !autoRunning && !remoteRefreshing && !Application.OpenForms.Cast<Form>().Any(f => f != this && f.Modal)) { await RunQueue(); return; }

        if (remoteRefreshing || engine.Busy || autoRunning || poolImportBusy || deletingPackage || Application.OpenForms.Cast<Form>().Any(f => f != this && f.Modal)) return;

        autoRunning = true;

        Episode? active = null;

        try

        {

            var s = store.Settings;

            foreach (var pending in store.Episodes.Where(x => x.Status is "Bekliyor" or "Kesildi" && x.RetryAt <= DateTimeOffset.UtcNow)) {

                if (pending.ResumeStage is "Plan" or "Proofread" && s.AutoResearch) pending.Status = "Taslak";

                else if (pending.ResumeStage == "Proofread" && s.AutoProduce && pending.Plan != null) pending.Status = "Senaryo hazır";

                if (pending.ResumeStage == "Produce" && s.AutoProduce) pending.Status = "Senaryo hazır";

                if (pending.ResumeStage == "Upload" && s.AutoPublish && pending.Approved) pending.Status = "Onaylandı";

            }

            var ep = active = store.Episodes.Where(x => x.QueueOrder == 0).OrderBy(x => x.PublishAt ?? DateTimeOffset.MaxValue).FirstOrDefault(x => s.AutoResearch && x.Status == "Taslak" || s.AutoProduce && x.Status == "Senaryo hazır" || s.AutoPublish && x.Status == "Onaylandı" && x.PublishAt != null);

            if (ep == null) return;

            if (ep.Status == "Taslak") await engine.Plan(ep, resume: true);

            if (ep.Status == "Senaryo hazır" && s.AutoProduce) await engine.Produce(ep);

            if (ep.Status == "Onaylandı" && s.AutoPublish && ep.PublishAt != null) await engine.Upload(ep, ep.ResumeStage == "Upload" ? ep.ResumeSchedule ?? false : true);

        }

        catch (Exception ex) { if (active != null) { Reliability.Failed(active, ex, active.ResumeStage != "" ? active.ResumeStage : active.Plan == null ? "Plan" : "Produce"); active.Log(ex.Message); store.Save(); } }

        finally { autoRunning = false; UpdateFooter(); }

    }

    public static DataGridView Grid() => new() { BackgroundColor = Theme.Panel, BorderStyle = BorderStyle.None, GridColor = Theme.Border, ForeColor = Theme.Text, Font = Theme.Font(), RowHeadersVisible = false, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, EnableHeadersVisualStyles = false, ColumnHeadersDefaultCellStyle = new() { BackColor = Theme.Elevated, ForeColor = Theme.Text, Font = Theme.Font(10, true), Padding = new Padding(8) }, DefaultCellStyle = new() { BackColor = Theme.Panel, ForeColor = Theme.Text, SelectionBackColor = Theme.Elevated, SelectionForeColor = Theme.Text, Padding = new Padding(6), WrapMode = DataGridViewTriState.True }, RowTemplate = { Height = 55 } };

}



