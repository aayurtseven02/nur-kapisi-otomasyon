namespace AnkaStudio;

public sealed partial class StudioForm
{
    public void CaptureCompactCenter(string folder)
    {
        Directory.CreateDirectory(folder);if(!Visible){Show();Application.DoEvents();}
        void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
        var fixture=new Episode{Topic="Kompakt arayüz kontrolü",Demo=true,IncludeLong=false,ShortCount=1,WizardStep=2};fixture.Plan=Engine.DemoPlan(fixture);store.Episodes.Add(fixture);wizardDraft=null;selectedWorkspaceId=fixture.Id;
        store.Settings.YouTubeChannelId="fixture-channel";
        Json.Write(Path.Combine(store.Root,"cache","youtube-library.json"),new ChannelStatistics{ChannelId="fixture-channel",Updated=DateTimeOffset.UtcNow,Videos=[new(){Id="fixture-one",Title="Bir Gencin İmanı",Kind="Shorts",Visibility="public",Views=1234,Published=DateTimeOffset.UtcNow.AddDays(-3)},new(){Id="fixture-two",Title="Sabır ve Şükür",Visibility="private",ScheduledAt=DateTimeOffset.UtcNow.AddDays(1),Published=DateTimeOffset.UtcNow}]});
        var before=System.Text.Json.JsonSerializer.Serialize(new{store.Settings,store.Episodes},Json.Options);
        foreach(var width in new[]{1120,1440}){
            Width=width;
            foreach(var route in new[]{"Video hazırla","Videolarım","Bağlantı Merkezi","Yönetim ayarları","Medya arşivleri"}){
                Navigate(route);PerformLayout();Application.DoEvents();
                Check(nav.Count==4&&nav.ContainsKey("Yüklenmesi gerekenler"),"Kurulum listesi ana menüde değil.");
                Check(!CardChildren(page).OfType<Button>().Any(b=>b.Text=="Klasik çalışma alanı"),"İkinci üretim kapısı görünür.");
                if(route=="Video hazırla"){
                    var steps=CardChildren(page).OfType<StudioButton>().Where(b=>b.AccessibleName?.StartsWith("Üretim adımı:")==true).ToArray();
                    Check(steps.Length==5&&steps[0].Completed&&steps[1].Completed&&!steps[2].Completed,"Tamamlanan adımlar doğru işaretlenmedi.");
                    Check(steps[0].BackColor==Theme.SuccessSurface&&steps[0].ForeColor==Theme.Text,"Tamamlanan adımlar yeşil/siyah değil.");
                }
                if(route=="Videolarım"){var table=CardChildren(page).OfType<DataGridView>().Single();Check(table.Rows.Count==2,"Kanal videoları tablosu eksik.");}
                using var image=new Bitmap(Width,Height);DrawToBitmap(image,new Rectangle(Point.Empty,Size));image.Save(Path.Combine(folder,width+"-"+route+".png"));
            }
            Navigate("Bağlantı Merkezi");
            foreach(var caption in new[]{"Yerel motorlar","Bulut yapay zekâ"}){
                var section=CardChildren(page).OfType<StudioDisclosure>().Single(d=>d.Controls.OfType<Button>().First().AccessibleName==caption+" bölümünü aç veya kapat");section.Controls.OfType<Button>().First().PerformClick();PerformLayout();Application.DoEvents();
                using var screen=new Bitmap(Width,Height);DrawToBitmap(screen,new Rectangle(Point.Empty,Size));screen.Save(Path.Combine(folder,width+"-"+caption+".png"));
            }
            foreach(var kind in new[]{"Video","Fotoğraf","Müzik","Ses efekti","Seslendirme","Kapak","Kart","Logo"}){
                mediaKind=kind;Navigate("Medya arşivleri");PerformLayout();Application.DoEvents();
                using var image=new Bitmap(Width,Height);DrawToBitmap(image,new Rectangle(Point.Empty,Size));image.Save(Path.Combine(folder,width+"-arsiv-"+kind+".png"));
            }
        }
        Check(before==System.Text.Json.JsonSerializer.Serialize(new{store.Settings,store.Episodes},Json.Options),"Gezinme proje veya ayarları değiştirdi.");
        using var button=new StudioButton{Completed=true,Enabled=false,Size=new(180,46),Text="Tamamlandı"};using var shot=new Bitmap(180,46);button.DrawToBitmap(shot,new Rectangle(Point.Empty,shot.Size));var fill=shot.GetPixel(160,36);Check(fill.ToArgb()==Theme.SuccessSurface.ToArgb(),"Devre dışı tamamlanmış adımın yeşili kayboldu.");
    }
    static void FitFormFields(Control parent,int width)
    {
        width=Math.Max(220,width);
        foreach(Control child in parent.Controls){
            if(child is StudioDisclosure disclosure){disclosure.FitWidth(width);if(disclosure.Expanded)FitFormFields(disclosure.Content,width-20);continue;}
            if(child is FlowLayoutPanel flow){flow.Width=width;flow.MaximumSize=new Size(width,0);FitFormFields(flow,width-flow.Padding.Horizontal-12);}
            else if(child is TextBox or ComboBox or CheckBox or Label){child.Width=Math.Min(650,width);if(child is Label or CheckBox){int measured=TextRenderer.MeasureText(child.Text,child.Font,new Size(Math.Max(100,child.Width-28),int.MaxValue),TextFormatFlags.WordBreak).Height;child.Height=Math.Max(child is CheckBox?34:26,measured+10);}}
            else if(child is NumericUpDown)child.Width=Math.Min(450,width);
            else if(child is Button)child.Width=Math.Min(320,width);
        }
    }
    string remoteProgress="";
    Label? remoteProgressLabel;
    CancellationTokenSource? remoteScanStop;
    void RemoteLibraryList()
    {
        var data=Json.Read(Path.Combine(store.Root,"cache","youtube-library.json"),new ChannelStatistics());
        if(data.ChannelId!=store.Settings.YouTubeChannelId)data=new ChannelStatistics();
        var items=data.Videos.Where(v=>libraryQuery==""||v.Title.Contains(libraryQuery,StringComparison.CurrentCultureIgnoreCase)).Where(v=>libraryFilter switch{"Yayında"=>v.Visibility=="public","Planlı"=>v.ScheduledAt.HasValue,"Taslak ve sırada" or "Hazır"=>false,_=>true}).OrderByDescending(v=>v.Published).ToArray();
        var group=new StudioDisclosure("YouTube’daki Videolar • "+items.Length,page.Width,true);page.Controls.Add(group);
        group.Content.Controls.Add(Theme.Label(data.Updated==default?"Kanalının tüm videolarını getirmek için YouTube ve projeleri güncelle düğmesine bas.":"Son başarılı tarama: "+data.Updated.ToLocalTime().ToString("dd.MM.yyyy HH:mm")+" • Başlığa çift tıklayarak videoyu aç.",10,width:page.Width-30,height:50));
        if(items.Length==0)return;
        var grid=new DataGridView{Width=page.Width-30,Height=Math.Min(260,48+items.Length*32),ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,BackgroundColor=Theme.Panel,BorderStyle=BorderStyle.None,EnableHeadersVisualStyles=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false};
        grid.DefaultCellStyle.BackColor=Theme.Panel;grid.DefaultCellStyle.ForeColor=Theme.Text;grid.DefaultCellStyle.SelectionBackColor=Theme.AccentSurface;grid.DefaultCellStyle.SelectionForeColor=Color.White;grid.DefaultCellStyle.Font=Theme.Font(10);grid.ColumnHeadersDefaultCellStyle.BackColor=Theme.Elevated;grid.ColumnHeadersDefaultCellStyle.ForeColor=Theme.Text;grid.RowTemplate.Height=32;
        foreach(var column in new[]{"Video Başlığı","Tür","Durum","İzlenme","Tarih"})grid.Columns.Add(column,column);
        grid.Columns[0].FillWeight=45;grid.Columns[1].FillWeight=13;grid.Columns[2].FillWeight=13;grid.Columns[3].FillWeight=12;grid.Columns[4].FillWeight=17;grid.Columns[3].ValueType=typeof(long);grid.Columns[3].DefaultCellStyle.Format="N0";
        foreach(var video in items){int row=grid.Rows.Add(video.Title,video.Kind,video.ScheduledAt.HasValue?"Planlı":video.Visibility switch{"public"=>"Yayında","unlisted"=>"Liste dışı",_=>"Gizli"},video.Views,(video.ScheduledAt??video.Published).ToLocalTime().ToString("dd.MM.yyyy"));grid.Rows[row].Tag=video.Id;}
        grid.CellDoubleClick+=(_,e)=>{if(e.RowIndex>=0&&grid.Rows[e.RowIndex].Tag is string id)Theme.Open("https://www.youtube.com/watch?v="+Uri.EscapeDataString(id));};
        group.Content.Controls.Add(grid);
    }
    string mediaKind="Tümü";
    string libraryFilter="Tümü";
    internal static bool LibraryMatches(Episode episode,string filter)=>filter switch{
        "Yayında"=>PackageArchive.PublicationGroup(episode)=="Yayına alınmış projeler",
        "Planlı"=>PackageArchive.PublicationGroup(episode)=="Planlanmış projeler",
        "Hazır"=>PackageArchive.Group(episode)=="Tamamlanmış projeler",
        "Taslak ve sırada"=>PackageArchive.Group(episode)=="",
        _=>true};
    void MediaHub()
    {
        page.Controls.Add(Theme.Button("Ücretsiz medya • resim, video, müzik ve SFX ara",()=>FreeMediaDialog(),true,600));
        page.Controls.Add(Actions(new[]{"Tümü","Video","Fotoğraf","Müzik","Ses efekti","Seslendirme","Kapak","Kart","Logo"}.Select(kind=>Theme.Button(kind,()=>{mediaKind=kind;audioOffset=0;Navigate("Medya arşivleri");},mediaKind==kind,130)).ToArray()));
        switch(mediaKind){
            case "Video":PoolPage();break;
            case "Müzik":case "Ses efekti":case "Seslendirme":audioKind=mediaKind;AudioPage();break;
            case "Kapak":case "Kart":case "Logo":CoverPage();break;
            case "Fotoğraf":PhotoArchive();break;
            default:
                Title("Medya havuzu","Türünü seç; dosyaların kendi arşiv klasöründe saklanır. Kaynak ve kullanım bilgileri korunur.");
                var audio=new AudioLibrary(store).List(refresh:true);
                foreach(var item in new[]{("Video",new MediaPool(store).List().Count), ("Fotoğraf",PhotoFiles().Length),("Müzik",audio.Count(x=>x.Kind=="Müzik")),("Ses efekti",audio.Count(x=>x.Kind=="Ses efekti")),("Seslendirme",audio.Count(x=>x.Kind=="Seslendirme")),("Kapak",CoverArchive.Files(store,false).Length),("Kart",Directory.Exists(Path.Combine(store.Root,"card-assets"))?Directory.GetFiles(Path.Combine(store.Root,"card-assets"),"*.png").Length:0),("Logo",AiDesignDialog.LogoFiles(store).Length)}){
                    var selected=item.Item1;page.Controls.Add(Theme.Button(selected+" • "+item.Item2+" dosya",()=>{mediaKind=selected;Navigate("Medya arşivleri");},false,page.Width));
                }
                break;
        }
    }
    string PhotoFolder=>Path.Combine(store.Root,"photo-library");
    string[] PhotoFiles()=>Directory.Exists(PhotoFolder)?Directory.GetFiles(PhotoFolder).Where(p=>new[]{".png",".jpg",".jpeg",".webp"}.Contains(Path.GetExtension(p).ToLowerInvariant())).ToArray():[];
    void PhotoArchive()
    {
        Title("Fotoğraf arşivi","Fotoğraflar ayrı klasörde saklanır; sahne editöründen videona ekleyebilirsin.");
        page.Controls.Add(Actions(Theme.Button("Fotoğraf ekle",()=>_=Safe(()=>{if(engine.Busy)throw new InvalidOperationException("Üretimin bitmesini bekle.");using var files=new OpenFileDialog{Filter="Fotoğraflar|*.png;*.jpg;*.jpeg;*.webp",Multiselect=true};if(files.ShowDialog(this)!=DialogResult.OK)return Task.CompletedTask;Directory.CreateDirectory(PhotoFolder);foreach(var path in files.FileNames){var target=Path.Combine(PhotoFolder,Reliability.FileHash(path)+Path.GetExtension(path).ToLowerInvariant());if(!File.Exists(target))File.Copy(path,target);}Navigate("Medya arşivleri");return Task.CompletedTask;}),true,210),Theme.Button("Klasörü aç",()=>{Directory.CreateDirectory(PhotoFolder);Theme.Open(PhotoFolder);},false,210),Theme.Button("Listeyi güncelle",()=>Navigate("Medya arşivleri"),false,210)));
        var items=PhotoFiles();page.Controls.Add(Theme.Label(items.Length+" fotoğraf",12,true,width:page.Width,height:40));
        page.Controls.Add(Theme.Label("Arşive eklemek kullanım izni vermez. Videoda kullanmadan önce kaynak ve lisansı sahne editöründe doğrula.",10,width:page.Width,height:60));
        foreach(var file in items.Take(100)){var selected=file;page.Controls.Add(Theme.Button(Path.GetFileName(file),()=>Theme.Open(selected),false,page.Width));}
        if(items.Length>100)page.Controls.Add(Theme.Label("İlk 100 dosya gösteriliyor. Tümünü Klasörü aç ile görebilirsin.",10,width:page.Width,height:40));
    }
    void LocalVisualPanel(FlowLayoutPanel target)
    {
        var box=new StudioDisclosure("Görsel motoru • ComfyUI",Math.Max(350,target.Width));
        var flow=box.Content;
        flow.Controls.Add(Theme.Label("Motoru ve kaliteyi seç; başlat düğmesi kurulumu bulup kontrol eder. Teknik dosyalar Gelişmiş bölümündedir.",10,width:650,height:65));
        var model=Theme.Combo(LocalCoverEngine.Models,store.Settings.CoverLocalModel??"Otomatik",650);Field(flow,"Görsel motoru",model);
        var quality=Theme.Combo(LocalCoverEngine.Qualities,store.Settings.CoverLocalQuality??"Dengeli",650);Field(flow,"Üretim kalitesi",quality);
        var titleStyle=Theme.Combo(["3D Telkari Altın","3D Metalik Altın","Yazısız"],store.Settings.CoverTitleStyle??"3D Metalik Altın",650);Field(flow,"Kapak başlık ve çerçeve stili",titleStyle);
        var enabled=Theme.Check("Yerel görsel motorunu kullan",store.Settings.CoverLocalEnabled,650);flow.Controls.Add(enabled);
        var status=Theme.Label("Henüz denetlenmedi • Başlat ve dene ile motoru kontrol et.",11,true,Theme.Text,width:650,height:75);status.AccessibleName="Görsel motor durumu";flow.Controls.Add(status);
        var advanced=new StudioDisclosure("Gelişmiş • motor bağlantısı",650);
        var path=Theme.Input(File.Exists(store.Settings.CoverLocalWorkflow)?store.Settings.CoverLocalWorkflow:LocalCoverEngine.FindInstalledWorkflow(),width:620);path.AccessibleName="Yerel görsel iş akışı";Field(advanced.Content,"İş akışı",path);
        void Save(){if(engine.Busy)throw new InvalidOperationException("Üretimin bitmesini bekle.");var resolved=LocalCoverEngine.ResolveWorkflow(model.Text,path.Text.Trim());if(enabled.Checked)LocalCoverEngine.Prepare(resolved,"test",false,quality:quality.Text);path.Text=resolved;store.Settings.CoverLocalModel=model.Text;store.Settings.CoverLocalQuality=quality.Text;store.Settings.CoverTitleStyle=titleStyle.Text;store.Settings.CoverLocalWorkflow=resolved;store.Settings.CoverLocalEnabled=enabled.Checked;store.Save();}
        advanced.Content.Controls.Add(Actions(Theme.Button("Motor klasörünü bağla",()=>_=Safe(()=>{
            using var folder=new FolderBrowserDialog{Description="ComfyUI portable motor klasörü"};if(folder.ShowDialog(this)!=DialogResult.OK)return Task.CompletedTask;
            var found=LocalCoverEngine.FindEngine(folder.SelectedPath)??throw new InvalidOperationException("Bu klasörde ComfyUI ve taşınabilir Python bulunamadı.");
            var selectedWorkflow=LocalCoverEngine.ResolveWorkflow(model.Text,path.Text);
            LocalCoverEngine.Prepare(selectedWorkflow,"test",false,quality:quality.Text);
            var bound=Path.Combine(store.Root,"local-cover-engine");Directory.CreateDirectory(bound);
            var destination=Path.Combine(bound,"connected-workflow.json");if(!Path.GetFullPath(selectedWorkflow).Equals(Path.GetFullPath(destination),StringComparison.OrdinalIgnoreCase))File.Copy(selectedWorkflow,destination,true);File.WriteAllText(Path.Combine(bound,"engine-folder.txt"),found);
            path.Text=destination;enabled.Checked=true;Save();status.Text="Motor bağlandı. Başlat ve dene ile modelleri kontrol et.";return Task.CompletedTask;
        }),false,240),Theme.Button("İş akışı seç",()=>{using var file=new OpenFileDialog{Filter="ComfyUI API iş akışı|*.json"};if(file.ShowDialog(this)==DialogResult.OK)path.Text=file.FileName;},false,220)));
        flow.Controls.Add(Actions(Theme.Button("Başlat ve dene",()=>_=Safe(async()=>{Save();status.Text="Motor kontrol ediliyor…";await engine.External(async ct=>{await LocalCoverEngine.Start(path.Text,text=>{if(!status.IsDisposed)status.Text=text;},ct);var result=await LocalCoverEngine.Check(path.Text,ct);if(!status.IsDisposed)status.Text=result;});}),true,240),Theme.Button("Tercihi kaydet",()=>_=Safe(()=>{Save();status.Text="Yerel görsel tercihi kaydedildi.";return Task.CompletedTask;}),false,220)));
        advanced.Content.Controls.Add(Theme.Button("Hazır iş akışları klasörü",()=>Theme.Open(Path.Combine(AppContext.BaseDirectory,"tools","workflows")),false,320));
        flow.Controls.Add(advanced);target.Controls.Add(box);
    }
}
