using System.Text.Json;

namespace AnkaStudio;

public sealed partial class StudioForm
{
    Episode? wizardDraft;
    void StartWizardProject(){if(engine.Busy){Message("Çalışan işlemi tamamla veya durdur.");return;}wizardDraft=new Episode{IncludeLong=false,ShortCount=1,ShortSeconds=90,Music=false};selectedWorkspaceId=wizardDraft.Id;Navigate("Video hazırla");}
    void ProductionWizard()
    {
        Title("Yeni içerik üret","Konunu veya metnini gir; tasarımını seç, üret, incele ve yayınla.");
        var existing=store.Episodes.OrderByDescending(e=>e.Created).ToList();
        var episode=wizardDraft??existing.FirstOrDefault(e=>e.Id==selectedWorkspaceId)??existing.FirstOrDefault();
        if(episode==null){wizardDraft=new Episode{IncludeLong=false,ShortCount=1,ShortSeconds=90,Music=false};episode=wizardDraft;}
        selectedWorkspaceId=episode.Id;
        var picker=Theme.Combo(existing.Select(e=>e.Topic+" • "+e.Id[..6]).ToArray(),"",Math.Max(260,page.Width-205));picker.AccessibleName="Üretilecek proje";
        picker.SelectedIndex=existing.FindIndex(e=>e.Id==episode.Id);
        picker.SelectedIndexChanged+=(_,_)=>{if(picker.SelectedIndex<0)return;wizardDraft=null;selectedWorkspaceId=existing[picker.SelectedIndex].Id;Navigate("Video hazırla");};
        var selection=new FlowLayoutPanel{Width=page.Width,Height=54,WrapContents=false};selection.Controls.Add(picker);selection.Controls.Add(Theme.Button("+ Yeni içerik",StartWizardProject,false,180));page.Controls.Add(selection);
        var card=Theme.Card(page.Width,590);card.AccessibleName="Beş adımlı üretim";page.Controls.Add(card);
        var tabs=new FlowLayoutPanel{Location=new(16,16),Size=new(card.Width-32,50),WrapContents=false,Tag="phase-tabs"};card.Controls.Add(tabs);
        var content=Theme.Flow(card.Width-48);content.AutoSize=false;content.AutoScroll=true;content.Location=new(24,82);content.Size=new(card.Width-48,400);card.Controls.Add(content);
        var footer=new Panel{Location=new(24,505),Size=new(card.Width-48,60)};card.Controls.Add(footer);
        TextBox? topic=null,bodyText=null;CheckBox? readyText=null;
        string preset=episode.WizardPreset==""?"story":episode.WizardPreset;
        var status=Theme.Label("",11,width:content.Width-20,height:62);var progress=new TurquoisePackageBar{Width=content.Width-20,Height=30};
        var warning=Theme.Input("",true,content.Width-20);warning.ReadOnly=true;warning.Height=90;warning.WordWrap=true;warning.ScrollBars=ScrollBars.Vertical;warning.AccessibleName="Üretim uyarısı";
        Button previous=null!,primary=null!;
        previous=Theme.Button("← Önceki adım",()=>Select(Math.Max(0,episode.WizardStep-1)),false,180);previous.Location=new(0,6);footer.Controls.Add(previous);
        primary=Theme.Button("Devam Et →",()=>_=Safe(Advance),true,Math.Min(400,footer.Width-205));primary.Location=new(footer.Width-primary.Width,6);primary.BackColor=Theme.SuccessSurface;primary.ForeColor=Color.Black;primary.Height=48;primary.AccessibleName="Ana işlem";footer.Controls.Add(primary);
        void Save(){if(!store.Episodes.Contains(episode))store.Episodes.Add(episode);wizardDraft=null;store.Save();}
        void Heading(string title,string description){content.Controls.Add(Theme.Label(title,18,true,width:content.Width-20,height:44));content.Controls.Add(Theme.Label(description,10,color:Theme.Muted,width:content.Width-20,height:55));}
        void Secondary(string text,Action action){content.Controls.Add(Theme.Button(text,action,false,Math.Min(540,content.Width-20)));}
        void Select(int step){episode.WizardStep=Math.Clamp(step,0,4);if(store.Episodes.Contains(episode))store.Save();Build();}
        void Build()
        {
            content.Controls.Remove(status);content.Controls.Remove(progress);content.Controls.Remove(warning);foreach(var c in content.Controls.Cast<Control>().ToArray())c.Dispose();content.Controls.Clear();foreach(var c in tabs.Controls.Cast<Control>().ToArray())c.Dispose();tabs.Controls.Clear();
            string[] names=["Kaynak ve Metin","Tasarım","Üretim","Düzenle ve Kontrol","Yayına Al"];
            bool[] complete=ProductionPresets.Complete(episode);
            for(int i=0;i<5;i++){int step=i;var b=Theme.Button((complete[i]?"✓ ":(i+1)+". ")+names[i],()=>Select(step),episode.WizardStep==i,Math.Max(110,(tabs.Width-20)/5));b.Enabled=!engine.Busy&&(step==0||episode.Topic.Trim()!=""&&(step<=1||episode.Plan!=null||episode.MusicClip!=null))&&(step<4||complete[3]);((StudioButton)b).Completed=complete[i];b.BackColor=complete[i]?Theme.SuccessSurface:episode.WizardStep==i?Theme.Teal:Theme.Panel;b.ForeColor=complete[i]?Color.Black:episode.WizardStep==i?Theme.Bg:Theme.Text;((StudioButton)b).CurrentStep=episode.WizardStep==i;b.Tag="wizard-step";b.Height=46;b.Margin=new Padding(0,0,4,0);b.Font=Theme.Font(9,true);((StudioButton)b).WrapCaption=true;b.AccessibleName="Üretim adımı: "+names[i];tabs.Controls.Add(b);}
            topic=null;bodyText=null;readyText=null;previous.Visible=episode.WizardStep>0;
            switch(episode.WizardStep){
                case 0:
                    Heading("1. Bugün ne anlatmak istiyorsun?","Konunu yaz veya hazır anlatımını yapıştır. Kaynak araştırması ve sahne hazırlığı seçtiğin üretim biçimiyle yapılır.");
                    topic=Theme.Input(episode.Topic,width:content.Width-24);topic.AccessibleName="Video konusu";content.Controls.Add(topic);
                    readyText=Theme.Check("Hazır metnim var • metnimi koru",UserNarration.Active(episode),content.Width-24);content.Controls.Add(readyText);
                    bodyText=Theme.Input(UserNarration.Active(episode)?episode.UserNarration!.LongText!=""?episode.UserNarration.LongText:episode.UserNarration.Shorts.FirstOrDefault()??"":episode.Brief,true,content.Width-24);bodyText.Height=160;bodyText.AccessibleName="Hazır metin veya içerik talimatı";content.Controls.Add(bodyText);
                    bool locked=episode.Plan!=null||episode.Videos.Count>0;topic.ReadOnly=bodyText.ReadOnly=locked;readyText.Enabled=!locked;
                    if(locked)Secondary("Metni / sahneleri düzenle",()=>{Edit(episode);});
                    break;
                case 1:
                    Heading("2. Videonun tarzını seç","Ses ve hesap bağlantıların korunur. Seçtiğin kalıp görüntü türünü, oranı, yazı ve kapak düzenini ayarlar.");
                    var presetCards=new FlowLayoutPanel{Width=content.Width-24,Height=250,WrapContents=true};
                    foreach(var p in ProductionPresets.All){var chosen=p;var b=Theme.Button((preset==p.Id?"✓ ":"")+p.Name+"\n"+p.Description,()=>{preset=chosen.Id;Build();},preset==p.Id,(presetCards.Width-16)/2);b.Height=112;b.Margin=new Padding(0,0,8,8);((StudioButton)b).WrapCaption=true;b.Enabled=episode.Plan==null&&episode.Videos.Count==0;presetCards.Controls.Add(b);}content.Controls.Add(presetCards);
                    Secondary("Ses, altyazı ve görsel tercihleri",()=>{Save();EditText(episode);});
                    Secondary("Kapak ve kart tercihleri",()=>{Save();EditPackageCovers(episode);});
                    if(episode.Plan!=null)content.Controls.Add(Theme.Label("Metin planı hazır. Üretim türünü değiştirmek için yeni proje aç; mevcut metni sahne editöründe düzenleyebilirsin.",10,width:content.Width-24,height:60));
                    break;
                case 2:
                    Heading("3. Otomatik üretim","Ses, konuşmayla zamanlanan altyazı, görüntüler ve kapak hazırlanır. İlerlemeyi burada takip edebilirsin.");
                    content.Controls.Add(status);content.Controls.Add(warning);content.Controls.Add(progress);
                    if(episode.Plan!=null){int scenes=Rules.Scripts(episode).Sum(s=>s.Script.Scenes.Count);content.Controls.Add(Theme.Label($"{scenes} sahne • "+(episode.AiStoryImages?"AI görselleri + sinematik hareket":"Seçili görüntü üretimi")+"\n"+(episode.AiStoryAutonomous?"Görseller bitince videoda incelenir.":"Seçili görsel onay düzeni korunur."),11,width:content.Width-24,height:74));}
                    Secondary("İşlemi durdur",CancelWorkspaceOperation);
                    if(episode.MusicClip!=null)Secondary("Müzik ve klip ayarlarını seç",()=>QueueEditor(episode));
                    break;
                case 3:
                    Heading("4. İzle, düzenle ve kontrol et","Videoyu izle. Görseli veya cümleyi değiştirdiğinde hazır sahne önbellekleri kullanılır; yeni çıktı yine kontrol edilir.");
                    Secondary("▶ Videoyu izle",()=>WatchEpisode(episode));
                    var tools=new FlowLayoutPanel{Width=content.Width-24,Height=54,WrapContents=false};
                    foreach(var entry in new (string,Action)[]{("Sahneler",()=>{Save();Edit(episode);}),("Kapak ve kart",()=>{Save();EditPackageCovers(episode);}),("Ses ve altyazı",()=>{Save();EditText(episode);})})tools.Controls.Add(Theme.Button(entry.Item1,entry.Item2,false,(tools.Width-30)/3));content.Controls.Add(tools);
                    if(episode.Plan!=null){var sceneRows=new FlowLayoutPanel{Width=content.Width-24,Height=52,WrapContents=false,AutoScroll=true};int n=0;foreach(var item in Rules.Scripts(episode))foreach(var scene in item.Script.Scenes){int number=++n;var selected=scene;sceneRows.Controls.Add(Theme.Button("Sahne "+number,()=>QuickWizardScene(episode,selected),false,115));}content.Controls.Add(sceneRows);}
                    bool technical=episode.Videos.Count>0&&episode.Videos.All(v=>v.QualityPassed)&&Reliability.OutputsCurrent(episode,false);
                    bool rights=episode.Plan!=null&&Rules.Scripts(episode).SelectMany(s=>s.Script.Scenes).Any()&&Rules.Scripts(episode).SelectMany(s=>s.Script.Scenes).All(s=>s.RightsConfirmed);
                    content.Controls.Add(Theme.Label((technical?"✓":"○")+" Teknik video ve altyazı kontrolü: "+(technical?"geçti":"bekliyor")+"\n"+(rights?"✓":"○")+" Görüntü kullanım hakkı teyidi: "+(rights?"kaydedildi":"bekliyor")+"\nKaynaklar: "+(episode.Plan?.Sources.Count??0)+" • Editoryal uyarılar: "+(episode.Plan?.Warnings.Count??0),11,width:content.Width-24,height:90));
                    break;
                case 4:
                    Heading("5. Yayına al","Güncel ve onaylanmış videoları şimdi yayınla veya tarih seç. Yayın işlemi ayrıca onayını ister.");
                    content.Controls.Add(Theme.Label(episode.Topic,14,true,width:content.Width-24,height:65));
                    content.Controls.Add(Theme.Label("Bağlı kanal: "+store.Settings.YouTubeChannelTitle+"\n"+(episode.PublishAt is {} at?"Seçili tarih: "+at.ToOffset(TimeSpan.FromHours(3)).ToString("dd.MM.yyyy HH:mm"):"Yayın tarihi seçilmedi."),11,width:content.Width-24,height:65));
                    Secondary("Tarih seç ve planla",()=>ScheduleDialog(episode));
                    Secondary("Dosyalarımı aç",()=>Theme.Open(store.Folder(episode)));
                    break;
            }
            Update();content.PerformLayout();
        }
        void Update(){if(card.IsDisposed)return;status.Text=episode.Error!=""?"Üretim durdu • ayrıntılar aşağıda.":ProjectState.Message(episode);warning.Visible=episode.Error!="";warning.Text=episode.Error;progress.Value=ProjectState.Progress(episode);progress.Busy=engine.Busy&&episode.Status is "Araştırılıyor" or "Üretiliyor" or "Yükleniyor";progress.Text=episode.CurrentStage;primary.Enabled=!engine.Busy;previous.Enabled=!engine.Busy;int tabIndex=0;foreach(var tab in tabs.Controls.OfType<Button>()){int step=tabIndex++;var done=ProductionPresets.Complete(episode)[step];((StudioButton)tab).Completed=done;((StudioButton)tab).CurrentStep=episode.WizardStep==step;tab.BackColor=done?Theme.SuccessSurface:episode.WizardStep==step?Theme.Teal:Theme.Panel;tab.ForeColor=done?Color.Black:episode.WizardStep==step?Theme.Bg:Theme.Text;tab.Invalidate();tab.Enabled=!engine.Busy&&(step==0||episode.Topic.Trim()!=""&&(step<=1||episode.Plan!=null||episode.MusicClip!=null))&&(step<4||ProductionPresets.Complete(episode)[3]);}
            primary.Text=episode.WizardStep switch{0=>"Devam Et →",1=>episode.Plan==null?"Metni ve Kaynakları Hazırla →":"Üretime Geç →",2=>Reliability.OutputsCurrent(episode,false)?"Önizlemeye Geç →":"Videoyu Üret",3=>"İncele, Onayla ve Devam Et →",_=>"Şimdi YouTube’da Yayınla"};
            if(episode.WizardStep==4)primary.Enabled=!engine.Busy&&ProductionPresets.Complete(episode)[3]&&!episode.Demo;
        }
        async Task Advance(){
            if(engine.Busy)return;
            switch(episode.WizardStep){
                case 0:
                    if(episode.Plan==null){if(string.IsNullOrWhiteSpace(topic?.Text))throw new InvalidOperationException("Önce video konusunu yaz.");episode.Topic=topic!.Text.Trim();
                        if(readyText!.Checked){if(string.IsNullOrWhiteSpace(bodyText!.Text))throw new InvalidOperationException("Hazır anlatımını yapıştır.");episode.UserNarration=new UserNarrationOptions{Enabled=true,LongText=bodyText.Text,Shorts=[bodyText.Text],VisualTheme=episode.Topic};}
                        else{episode.UserNarration??=new();episode.UserNarration.Enabled=false;episode.Brief=bodyText!.Text.Trim();}}
                    Save();Select(1);break;
                case 1:
                    if(episode.Plan==null){ProductionPresets.Apply(episode,store.Settings,preset);Save();if(episode.MusicClip!=null){Select(2);return;}await engine.Plan(episode);if(episode.Error!=""){Select(2);return;}}
                    Select(2);break;
                case 2:
                    if(episode.Plan==null&&episode.MusicClip!=null){await engine.Plan(episode);if(episode.Error!=""){Update();return;}}
                    if(episode.Plan==null)throw new InvalidOperationException("Önce kaynak ve metin hazırlığını tamamla.");
                    if(!Reliability.OutputsCurrent(episode,false)){await engine.Produce(episode);if(episode.Error!=""){Update();return;}}
                    Select(3);break;
                case 3:
                    if(!ProductionPresets.Complete(episode)[2]){Select(2);return;}Review(episode);if(episode.Approved&&episode.ApprovalHash==Rules.HashPlan(episode))Select(4);break;
                case 4:await PublishPackageNow(episode);break;
            }
        }
        var fine=new StudioDisclosure("Proje ve süre ayarları",page.Width);
        fine.Content.Controls.Add(Theme.Button("Proje / süre / hazır metin",()=>{Save();EditPackage(episode);},false,300));
        page.Controls.Add(fine);
        packageShape=PackageShape();packageUpdates[episode.Id]=Update;Build();
    }
    void QuickWizardScene(Episode episode,Scene scene)
    {
        if(engine.Busy||episode.Videos.Any(v=>v.YouTubeId!="")){Message("Çalışan veya yayınlanmış içerikte yeni sürüm oluştur.");return;}
        using var dialog=Dialog("Sahneyi düzenle",820,560);var flow=Theme.Flow(730);flow.Dock=DockStyle.Fill;dialog.Controls.Add(flow);
        var text=Theme.Input(scene.Narration,true,700);text.Height=180;flow.Controls.Add(text);
        flow.Controls.Add(Theme.Label("Cümleyi değiştirirsen ses yeniden hazırlanır. Görsel yenileme talebinde mevcut görüntü, üretim tamamlanana kadar saklanır.",10,width:700,height:65));
        var refresh=Theme.Check("Bu sahnenin görselini yeniden üret",false,700);flow.Controls.Add(refresh);
        flow.Controls.Add(Theme.Button("Kaydet • üretim adımına dön",()=>{
            if(string.IsNullOrWhiteSpace(text.Text)){Message("Sahne metni boş olamaz.");return;}
            if(text.Text==scene.Narration&&!refresh.Checked){dialog.Close();return;}
            File.WriteAllText(Path.Combine(store.Folder(episode),"wizard-edit-backup-"+DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()+".json"),JsonSerializer.Serialize(episode,Json.Options));
            scene.Narration=text.Text;scene.SpeechTextHash="";scene.SpeechCaptions=[];
            if(refresh.Checked){scene.AiImagePrompt += ". New composition variation "+Guid.NewGuid().ToString("N")[..8];scene.AiImageHash="";scene.StockFile="";scene.RightsConfirmed=false;}
            episode.Approved=false;episode.ApprovalHash="";episode.TextDesignPending=true;episode.Status="Senaryo hazır";episode.WizardStep=2;store.Save();dialog.Close();Navigate("Video hazırla");
        },true,420));dialog.ShowDialog(this);
    }
    public void CaptureProductionWizard(string folder)
    {
        Directory.CreateDirectory(folder);var episode=new Episode{Topic="Bir Gencin İmanı, Bir Hükümdarın Korkusu",IncludeLong=false,ShortCount=1,WizardPreset="story",Demo=true};
        store.Episodes.Add(episode);wizardDraft=null;selectedWorkspaceId=episode.Id;
        foreach(int width in new[]{1120,1440}){
            Width=width;
            for(int step=0;step<5;step++){
                episode.WizardStep=step;episode.Plan=step<2?null:Engine.DemoPlan(episode);Navigate("Video hazırla");Application.DoEvents();
                if(nav.Count!=3)throw new InvalidOperationException("Ana menü üç bölüm değil.");
                if(CardChildren(page).OfType<Button>().Count(b=>b.AccessibleName=="Ana işlem")!=1)throw new InvalidOperationException("Tek ana işlem bulunamadı.");
                if(CardChildren(page).OfType<Button>().Count(b=>b.AccessibleName?.StartsWith("Üretim adımı:")==true)!=5)throw new InvalidOperationException("Beş adım eksik.");
                if(step==4&&CardChildren(page).OfType<Button>().Single(b=>b.AccessibleName=="Ana işlem").Enabled)throw new InvalidOperationException("Onaysız yayın düğmesi açık.");
                using var image=new Bitmap(Width,Height);DrawToBitmap(image,new Rectangle(Point.Empty,Size));image.Save(Path.Combine(folder,$"{width}-adim-{step+1}.png"));
            }
        }
    }
}
