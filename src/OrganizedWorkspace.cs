using System.Text.Json;

namespace AnkaStudio;



internal sealed class PhaseArrow : Label

{

    protected override void OnPaint(PaintEventArgs e)

    {

        e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        using var pen=new Pen(ForeColor,2.2f) {StartCap=System.Drawing.Drawing2D.LineCap.Round,EndCap=System.Drawing.Drawing2D.LineCap.Round};

        int y=Height/2;int end=Width-4;

        e.Graphics.DrawLine(pen,3,y,end,y);e.Graphics.DrawLines(pen,[new Point(end-5,y-5),new Point(end,y),new Point(end-5,y+5)]);

    }

}



public sealed partial class StudioForm

{

    static string SectionHeading(string text)=>text;

    string selectedWorkspaceId = "";

    int workspaceStep;

    string libraryQuery = "";

    bool compactLibrary;

    static readonly string[] WorkspaceSteps = ["İçerik", "Sahneler", "Ses ve tasarım", "Üret ve incele", "Yayın"];

    public static string NavigationRoot(string name) => name switch {

        "Stüdyo" or "Ana ekran" => "Video hazırla",
        "Yüklenmesi gerekenler" => "Yüklenmesi gerekenler",

        "Video hazırla" or "Klasik çalışma alanı" => "Video hazırla",

        "Üretim ve yayın planı" or "Yayın takvimi" or "Yayın sırası" => "Videolarım",

        "İçerik paketleri" or "Videolarım" => "Videolarım",

        _ => "Ayarlar"

    };

    static string PageCaption(string name) => name switch { "Stüdyo" => "Ana ekran", "Üretim ve yayın planı" => "Yayın sırası", "İçerik paketleri" => "Videolarım", "Yönetim ayarları" => "Varsayılan ayarlar", "Bağlantı Merkezi" => "Hesaplar ve bağlantılar", _ => name };

    void OpenWorkspace(Episode episode, int? step = null)

    {

        if (selectedWorkspaceId != episode.Id) workspaceStep = 0;

        wizardDraft=null;selectedWorkspaceId = episode.Id;

        if (step != null) workspaceStep = Math.Clamp(step.Value, 0, 4);

        Navigate("Video hazırla");

    }

    void ContextNavigation(string name)

    {

        if (NavigationRoot(name) == "Ayarlar") {

            var root=name is "Stok havuzu" or "Ses arşivi" or "Kapak tasarımları" or "Medya arşivleri" ? "Medya arşivleri" : name is "Ayarlar" or "Bağlantı Merkezi" ? "Bağlantı Merkezi" : "Yönetim ayarları";
            ContextTabs(root,[("Hesaplar ve AI", "Bağlantı Merkezi"),("Medya havuzu","Medya arşivleri"),("Kanal ve program","Yönetim ayarları")]);
        } else if (NavigationRoot(name) == "Videolarım") {

            ContextTabs(name, [("Projeler", "Videolarım"), ("Çalışma sırası", "Üretim ve yayın planı"), ("Takvim görünümü", "Yayın takvimi")]);

        }

    }

    void ContextTabs(string selected, (string Label, string Route)[] entries)

    {

        var tabs = new FlowLayoutPanel { Width = page.Width, Height = 54, WrapContents = false, Margin = new Padding(0,0,0,12), Tag = "equal-tabs" };

        foreach (var entry in entries) {

            bool active = entry.Route == selected || entry.Route == "Ayarlar" && selected == "Yönetim ayarları";

            var button = Theme.Button(SectionHeading(entry.Label), () => Navigate(entry.Route), active, Math.Max(140,(page.Width - entries.Length*10)/entries.Length));

            button.AccessibleName = "Bölüm: " + entry.Label; tabs.Controls.Add(button);

        }

        page.Controls.Add(tabs);

    }

    internal static bool ScenesAreReviewed(Episode episode)

    {

        if(episode.ScenesReviewed==true)return true;

        var edited=episode.Events.FindLastIndex(x=>x.Contains("Sahneler düzenlendi;"));

        var regenerated=episode.Events.FindLastIndex(x=>x.Contains("Senaryo ve sahne planı hazır.")||x.Contains("Sahne incelemesi sıfırlandı."));

        return edited>=0 && edited>regenerated;

    }

    static void FitSection(Control control,int width)

    {

        width=Math.Max(180,width-control.Margin.Horizontal);

        if(control.Dock==DockStyle.None && control.Width>width)control.Width=width;

        if(control is FlowLayoutPanel flow) { flow.MaximumSize=new Size(width,0); if(flow.WrapContents) { flow.AutoSize=true; flow.AutoSizeMode=AutoSizeMode.GrowAndShrink; } }

        if(control is Label label && label.Text.Length>60)label.Height=Math.Max(label.Height,TextRenderer.MeasureText(label.Text,label.Font,new Size(Math.Max(80,label.Width),int.MaxValue),TextFormatFlags.WordBreak).Height+6);

        if(control is CheckBox check)check.Height=Math.Max(check.Height,TextRenderer.MeasureText(check.Text,check.Font,new Size(Math.Max(80,check.Width-28),int.MaxValue),TextFormatFlags.WordBreak).Height+8);

        foreach(Control child in control.Controls)FitSection(child,Math.Max(180,Math.Min(width,control.Width)-control.Padding.Horizontal));

    }

    void AdvancedPage()

    {

        Title("Gelişmiş araçlar", "Günlük video hazırlama akışından ayrı teknik araçlar ve kayıtlar.");

        page.Controls.Add(Actions(Theme.Button("Çalışma klasörü", () => Theme.Open(store.Root), false,240), Theme.Button("Yükleme durumunu kontrol et", ShowUploadStatus, false,270), Theme.Button("Örnek video oluştur", DemoPackage, false,240)));

        Empty("Hesap ve proje verileri", "Program kaynakları isteğe bağlıdır; video üretimi ve yayın için gerekmez. Yedekleme, güvenlik ve otomatik çalışma seçenekleri Varsayılanlar bölümünde bulunur.");

    }

    void MediaHome()

    {

        MediaHub();
    }

    void VideoLibrary()

    {

        Title("Videolarım", "Taslaklarını, hazır videolarını, planlanan ve yayınlanan projelerini burada bul.");

        var summary=$"Toplam {store.Episodes.Count} proje • Yayında {store.Episodes.Count(e=>PackageArchive.PublicationGroup(e)=="Yayına alınmış projeler")} • Planlı {store.Episodes.Count(e=>PackageArchive.PublicationGroup(e)=="Planlanmış projeler")}";
        var overview=new FlowLayoutPanel{Width=page.Width,Height=48,WrapContents=false};overview.Controls.Add(Theme.Label(summary,10,true,width:Math.Max(300,page.Width-350),height:40));overview.Controls.Add(Theme.Button("Kanal istatistikleri ve öneriler",ShowChannelStatistics,false,330));page.Controls.Add(overview);
        page.Controls.Add(Actions(new[]{"Tümü","Taslak ve sırada","Hazır","Planlı","Yayında"}.Select(filter=>Theme.Button(filter,()=>{libraryFilter=filter;Navigate("Videolarım");},libraryFilter==filter,150)).ToArray()));
        var search = Theme.Input(libraryQuery, width: Math.Max(240,page.Width-170)); search.AccessibleName = "Video adı veya konu ara";

        var searchRow = new FlowLayoutPanel { Width=page.Width, Height=54, WrapContents=false, Margin=new Padding(0,0,0,12) };

        var searchButton = Theme.Button("Ara", () => { libraryQuery = search.Text.Trim(); Navigate("Videolarım"); }, false,130);

        search.KeyDown += (_,e) => { if(e.KeyCode==Keys.Enter) { searchButton.PerformClick(); e.SuppressKeyPress=true; } };

        searchRow.Controls.AddRange([search,searchButton]); page.Controls.Add(searchRow);

        compactLibrary = true; try { Packages(); } finally { compactLibrary=false; }

    }

    void Workspace()

    {

        Title("Video hazırla", "Bir proje seç; içerik, sahneler, ses ve tasarım, üretim ve yayın adımlarını aynı yerde tamamla.");

        var episodes = store.Episodes.OrderByDescending(e=>e.Created).ToList();

        var picker = Theme.Combo(episodes.Select(e=>e.Topic+" • "+e.Created.ToOffset(TimeSpan.FromHours(3)).ToString("dd.MM HH:mm")+" • "+e.Id[..6]).ToArray(), "", Math.Max(260,page.Width-430));

        picker.AccessibleName="Çalışılacak video projesi";

        int index = episodes.FindIndex(e=>e.Id==selectedWorkspaceId);

        if(index<0 && episodes.Count>0) { index=0; selectedWorkspaceId=episodes[0].Id; workspaceStep=0; }

        picker.SelectedIndex=index;

        picker.SelectedIndexChanged += (_,_)=> { if(picker.SelectedIndex>=0) OpenWorkspace(episodes[picker.SelectedIndex]); };

        var selection = new FlowLayoutPanel {Width=page.Width,Height=54,WrapContents=false,Margin=new Padding(0,0,0,14)};

        selection.Controls.Add(picker); selection.Controls.Add(Theme.Button("+ Yeni video",NewPackage,true,190)); selection.Controls.Add(Theme.Button("İşlemi İptal Et",CancelWorkspaceOperation,false,190)); page.Controls.Add(selection);

        if(index<0) { Empty("İlk videonu hazırlayalım", "Yeni video ile konu veya hazır metnini gir. Normal video, Shorts ve müzik klibi seçenekleri korunur."); return; }

        var episode=episodes[index];

        packageShape=PackageShape();

        EpisodeCard(episode,true);

        var card=page.Controls.Cast<Control>().Last();

        var row=card.Controls.OfType<FlowLayoutPanel>().Single(p=>p.Controls.Count==8 && p.Controls[0] is Button);

        row.Location=new(20,265); row.Height=74; row.Tag="grid2";

        var instruction=Theme.Label("",10,color:Theme.Muted,width:card.Width-40,height:50); instruction.Location=new(20,209); card.Controls.Add(instruction);

        var tabs=new FlowLayoutPanel {Location=new(20,162),Width=card.Width-40,Height=45,WrapContents=false,BackColor=Color.Transparent,Tag="phase-tabs"};

        int[] groups=[0,1,2,2,3,3,4,4];

        var descriptions=new[]{

            "İçerik talimatını veya hazır metnini Proje ayarları’ndan gir. Hazır metin kullanıldığında yapay zekâ metnini yeniden yazmaz.",

            "Sahne metinlerini ve görüntülerini düzenle. Stok havuzundan veya kendi dosyalarından video seçebilirsin.",

            "Kapak editöründe küçük resim, açılış/kapanış ve logo; başlık/altyazı editöründe ses önizlemesi, müzik, yazı ve geçiş ayarlarını düzenle.",

            "Videoları üret, özgün sesle eşlenen altyazıyı önizle ve içeriği onayla. Çıktı dosyaları Yayın adımında bulunur.",

            "Yayın ayarlarından tarih/saat seç veya onaylanmış videoları şimdi yayınla. Dosyalar doğrudan YouTube’a yüklenir."

        };

        var optionalDesign = Theme.Flow(card.Width - 40);
        optionalDesign.AutoSize = false; optionalDesign.Height = 100; optionalDesign.Location = new(20, 340);
        foreach (Control child in card.Controls) if (child.Top >= 349) child.Top += 110;
        card.Height += 110;
        var skipArtwork = Theme.Check("Kapak ayarlarını değiştirmek istemiyorum • mevcut ayarlarla devam et", episode.SkipArtworkEditing);
        var skipText = Theme.Check("Başlık / altyazı ayarlarını değiştirmek istemiyorum • mevcut ayarlarla devam et", episode.SkipTextDesignEditing);
        skipArtwork.Width = skipText.Width = Math.Max(260, card.Width - 60);
        optionalDesign.Controls.Add(skipArtwork); optionalDesign.Controls.Add(skipText);
        card.Controls.Add(optionalDesign);
        void SaveOptionalDesign() {
            if (engine.Busy) return;
            episode.SkipArtworkEditing = skipArtwork.Checked; episode.SkipTextDesignEditing = skipText.Checked;
            store.Save(); packageUpdates[episode.Id](); UpdatePhaseTabs();
        }
        skipArtwork.CheckedChanged += (_, _) => SaveOptionalDesign();
        skipText.CheckedChanged += (_, _) => SaveOptionalDesign();

        bool designSpaceReserved=true;
        void SelectStep(int step) {

            workspaceStep=step; instruction.Text=descriptions[step];
            bool showDesign=step==2;
            if(designSpaceReserved!=showDesign) {
                int delta=showDesign?110:-110;
                foreach(Control child in card.Controls) if(child!=optionalDesign && child.Top>=optionalDesign.Top) child.Top+=delta;
                card.Height+=delta; designSpaceReserved=showDesign;
            }
            optionalDesign.Visible=showDesign;

            UpdatePhaseTabs();

            for(int i=0;i<row.Controls.Count;i++) row.Controls[i].Visible=groups[i]==step;

            row.PerformLayout();

        }

        void UpdatePhaseTabs() {

            foreach(var button in tabs.Controls.OfType<StudioButton>()) {

                int step=(int)button.Tag!;

                bool complete=Enumerable.Range(0,groups.Length).Where(i=>groups[i]==step).All(i=>((StudioButton)row.Controls[i]).Completed);

                button.Completed=complete;button.BackColor=complete?Theme.SuccessSurface:step==workspaceStep?Theme.Teal:Theme.Elevated;

                button.ForeColor= !complete && step==workspaceStep ? Theme.Bg : Theme.Text;button.AccessibleDescription=complete?"Bu adım tamamlandı":"Bu adım tamamlanmadı";button.Invalidate();

            }

        }

        for(int i=0;i<WorkspaceSteps.Length;i++) {

            if(i>0) {var arrow=new PhaseArrow {Text="→",ForeColor=Theme.Warning,Width=24,Height=42,BackColor=Color.Transparent,Margin=Padding.Empty};tabs.Controls.Add(arrow);}

            int step=i;var button=Theme.Button(((i+1)+". "+WorkspaceSteps[i]).ToUpper(System.Globalization.CultureInfo.GetCultureInfo("tr-TR")),()=>SelectStep(step),false,(tabs.Width-96)/5);button.Margin=Padding.Empty;button.Font=Theme.Font(9,true);button.Height=42;button.Tag=i;button.AccessibleName="Hazırlık adımı: "+WorkspaceSteps[i];tabs.Controls.Add(button);

        }

        foreach(Button button in row.Controls) { int dot=button.Text.IndexOf(". ",StringComparison.Ordinal); if(dot>=0)button.Text=button.Text[(dot+2)..]; button.Width=(row.Width-24)/2; button.Height=46; }

        card.Controls.Add(tabs); SelectStep(Math.Clamp(workspaceStep,0,4));
        foreach (var phaseButton in tabs.Controls.OfType<StudioButton>()) phaseButton.WrapCaption = true;
        foreach (var stageBar in card.Controls.OfType<PackageStageBar>()) stageBar.Visible = false;
        foreach (Control child in card.Controls) if (child.Top >= 162) child.Top -= 49;
        card.Height -= 49;
        var watchAction = Theme.Button("Videoyu izle • yayın öncesi önizleme", () => WatchEpisode(episode), true, 450);
        watchAction.Tag = "package-action"; page.Controls.Add(watchAction);
        var socialAction = Theme.Button("Instagram / TikTok / Facebook’ta Paylaş", () => SocialPublishDialog(episode), true, 300); socialAction.Tag = "package-action"; page.Controls.Add(socialAction);
        page.Controls.Add(Theme.Button("Hatayı Düzelt ve Devam Et", () => _ = Safe(async () => {
            if (engine.Busy) { Message("Çalışan işlemin bitmesini bekle veya iptal et."); return; }
            if (episode.Error == "" && episode.ResumeStage == "") { Message("Bu projede devam ettirilecek hata yok."); return; }
            if (episode.ResumeStage == "Upload") { Message("Yükleme hatasını Yayın ekranından tekrar dene."); return; }
            foreach (var key in store.ProviderBlocked.Where(x => x.Value <= DateTimeOffset.UtcNow.AddMinutes(5)).Select(x => x.Key).ToArray()) store.ProviderBlocked.Remove(key);
            episode.RetryAt = null; episode.RetryAttempts = 0;
            episode.Log("Hata düzeltme istendi; kayıtlı aşamalar ve kaynaklar korunarak yeniden deneniyor."); store.Save();
            if (episode.ResumeStage == "Proofread") await engine.Plan(episode, resume: true);
            else if (episode.Plan != null) await engine.Produce(episode);
            else await engine.Plan(episode);
            if (episode.Error != "") Message("Otomatik deneme tamamlandı; çözülmesi gereken sorun: " + episode.Error);
        }), true, 300));
        page.Controls[page.Controls.Count - 1].Tag = "package-action";

        var projectSettings=CardChildren(card).OfType<Button>().Single(b=>b.Text=="Paket ayarları"); projectSettings.Text="Proje ayarları • konu / metin / süre";

        var actions=new FlowLayoutPanel {Width=page.Width,Height=64,WrapContents=false,Margin=new Padding(0,12,0,12),Tag="equal-tabs"};

        var save=Theme.Button("Kaydet",()=>{store.Save();Message("Proje kaydedildi. Editörlerdeki değişiklikleri ilgili Kaydet düğmesiyle onayla.");},false,180);

        var publish=Theme.Button("Şimdi yayınla",()=>_=Safe(()=>PublishPackageNow(episode)),true,260);

        var schedule=Theme.Button("Tarih seçerek yayınla",()=>ScheduleDialog(episode),false,290);

        bool Ready()=>!engine.Busy&&!episode.Demo&&episode.Approved&&episode.ApprovalHash==Rules.HashPlan(episode)&&Reliability.OutputsCurrent(episode);

        var previous=packageUpdates[episode.Id]; packageUpdates[episode.Id]=()=> {previous();UpdatePhaseTabs();skipArtwork.Enabled=skipText.Enabled=!engine.Busy;publish.Enabled=Ready();schedule.Enabled=!engine.Busy;save.Enabled=!engine.Busy;};packageUpdates[episode.Id]();

        hints.SetToolTip(publish,"Onaylı çıktıları doğrudan YouTube’a yükleyip herkese açık yayınlar. Yayınlamadan önce onay sorulur.");

        actions.Controls.AddRange([save,publish,schedule]);page.Controls.Add(actions);
        if(episode.Error.Contains("kelime",StringComparison.OrdinalIgnoreCase) && episode.Plan!=null && ScriptLength.Problem(episode,episode.Plan)!=null) {
            var repair=Theme.Button("Eksik Kelimeleri Tamamla ve Devam Et",()=>_=Safe(()=>CompleteWordsAndResume(episode)),true,440);
            repair.Enabled=!engine.Busy;page.Controls.Add(repair);
        }

    }

    void CancelWorkspaceOperation() {
        if (!engine.Busy && !queueRunning) { Message("Çalışan üretim veya yükleme işlemi yok."); return; }
        foreach (var item in VideoQueue.Items(store)) item.QueueAuthorized=false;
        queueCancellation?.Cancel(); engine.Stop();
        store.Save(); UpdateFooter();
        // Cancellation is immediate; do not wait on a potentially stalled provider.
    }

    async Task CompleteWordsAndResume(Episode episode) {
        if(engine.Busy || queueRunning)throw new InvalidOperationException("Önce çalışan işlemi durdur.");
        if(episode.Plan==null || ScriptLength.Problem(episode,episode.Plan)==null)return;
        episode.WordRepairResumeProduction |= episode.ResumeStage=="Produce";
        episode.ResumeStage="Proofread";store.Save();
        await engine.Plan(episode,resume:true);
        if(episode.Error=="" && episode.WordRepairResumeProduction) {episode.WordRepairResumeProduction=false;store.Save();await engine.Produce(episode);}
        RefreshCurrentPage();
    }

    async Task PublishPackageNow(Episode episode)

    {

        if(engine.Busy) throw new InvalidOperationException("Önce çalışan işlemi tamamla veya durdur.");

        if(episode.Demo||!episode.Approved||episode.ApprovalHash!=Rules.HashPlan(episode)||!Reliability.OutputsCurrent(episode)) throw new InvalidOperationException("Güncel videoları önce üret, incele ve onayla.");

        if(Theme.Alert(this,episode.Topic+" paketindeki "+episode.Videos.Count+" video şimdi herkese açık yayınlanacak. Devam edilsin mi?","Şimdi yayınla",MessageBoxButtons.YesNo)!=DialogResult.Yes) return;

        if(episode.Videos.Any(v=>v.YouTubeId=="")) {

            await engine.Upload(episode,false);

            if(episode.Error!="") throw new InvalidOperationException(episode.Error);

            if(episode.Status=="Durduruldu") return;

        }

        if(episode.Videos.Count==0||episode.Videos.Any(v=>v.YouTubeId=="")) throw new InvalidOperationException("Tüm videoların yüklemesi tamamlanmalı.");

        await engine.External(async ct=>{

            var yt=new YouTube(store);var channel=await yt.ChannelIdentity(ct);Engine.VerifyChannel(episode.ApprovedChannelId,store.Settings.YouTubeChannelId,channel.Id);

            foreach(var video in episode.Videos) {await yt.PublishNow(video,ct);episode.Log(video.Title+": YouTube herkese açık yayını doğruladı.");store.Save();}

            episode.Status="YouTube’da yayında";VideoQueue.Reconcile(store);store.Save();

        });

        Message("YouTube yayını başarıyla açıldı.");RefreshCurrentPage();

    }

    public void CaptureOrganizedUi(string folder)

    {

        Directory.CreateDirectory(folder);

        var fixture=new Episode {Topic="Hazreti Yusuf — Sabır ve Affetmenin Kıssası",Demo=true,ShortCount=1,IncludeLong=true,Status="Senaryo hazır"};fixture.Plan=Engine.DemoPlan(fixture);store.Episodes.Add(fixture);selectedWorkspaceId=fixture.Id;

        string before=JsonSerializer.Serialize(new {store.Settings,store.Episodes},Json.Options);

        string[] routes=["Stüdyo","Video hazırla","Videolarım","Üretim ve yayın planı","Yayın takvimi","Ayarlar","Bağlantı Merkezi","Medya arşivleri","Gelişmiş","İşlem kayıtları","Program kaynakları"];

        foreach(int width in new[]{1440,1120}) {

            Width=width;

            foreach(var route in routes) {

                Navigate(route);PerformLayout();Application.DoEvents();

                if(nav.Count!=3)throw new InvalidOperationException("Ana menü üç bölümden oluşmuyor.");

                foreach(var button in nav.Values) if(button.Bottom>button.Parent!.ClientSize.Height)throw new InvalidOperationException("Ana menü tuşu görünür alanı aşıyor.");

                using var image=new Bitmap(Width,Height);DrawToBitmap(image,new Rectangle(Point.Empty,Size));image.Save(Path.Combine(folder,width+"-"+route.Replace('/','-')+".png"));

            }

            Navigate("Ayarlar");

            foreach(var item in new[]{"Yerel AI / ücretsiz","Bulut AI / isteğe bağlı","Kanal / araştırma","Stok havuzu","Ses / müzik","Google TTS kota","Görüntü / altyazı","Otonomi / denetim","Arayüz / ses","Güvenlik"}) {

                var sectionButton = CardChildren(page).OfType<Button>().Single(b=>b.Text==item); if (sectionButton.Parent?.Parent is StudioDisclosure disclosure) disclosure.SetExpanded(true); sectionButton.PerformClick();PerformLayout();Application.DoEvents();

                using var shot=new Bitmap(Width,Height);DrawToBitmap(shot,new Rectangle(Point.Empty,Size));shot.Save(Path.Combine(folder,width+"-settings-"+item.Replace('/','-')+".png"));

            }

            OpenWorkspace(fixture);

            foreach(int step in Enumerable.Range(0,5)) {

                CardChildren(page).OfType<Button>().Single(b=>b.AccessibleName=="Hazırlık adımı: "+WorkspaceSteps[step]).PerformClick();PerformLayout();Application.DoEvents();

                using var image=new Bitmap(Width,Height);DrawToBitmap(image,new Rectangle(Point.Empty,Size));image.Save(Path.Combine(folder,width+"-step-"+step+".png"));

            }

        }

        foreach(int width in new[]{1120,1920}) {Width=width;ToggleAssistant();PerformLayout();Application.DoEvents();using var image=new Bitmap(Width,Height);DrawToBitmap(image,new Rectangle(Point.Empty,Size));image.Save(Path.Combine(folder,width+"-assistant.png"));using var panelImage=new Bitmap(assistantHost!.Width,assistantHost.Height);assistantHost.DrawToBitmap(panelImage,new Rectangle(Point.Empty,assistantHost.Size));panelImage.Save(Path.Combine(folder,width+"-assistant-panel.png"));Json.Write(Path.Combine(folder,width+"-assistant-layout.json"),new{assistantHost.Visible,assistantHost.Bounds,assistantVisible=assistant!.Visible,index=Controls.GetChildIndex(assistantHost),header=appHeader.Bounds});ToggleAssistant();}

        if(before!=JsonSerializer.Serialize(new {store.Settings,store.Episodes},Json.Options)) { File.WriteAllText(Path.Combine(folder,"before.json"),before); File.WriteAllText(Path.Combine(folder,"after.json"),JsonSerializer.Serialize(new {store.Settings,store.Episodes},Json.Options)); throw new InvalidOperationException("Ekran gezinmesi proje veya varsayılan ayarları değiştirdi."); }

        File.WriteAllText(Path.Combine(folder,"ui-result.txt"),"Beş ana bölüm, tüm hazırlık adımları ve teknik ekranlar açıldı. Gezinme proje verilerini değiştirmedi.");

    }

}

