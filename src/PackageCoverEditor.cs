namespace AnkaStudio;



public sealed class PackageCoverEditor : Form

{

    readonly Store store; readonly Episode episode; readonly Settings shared;

    readonly List<Action> readers = [];

    readonly List<(VideoScript Script, bool Vertical, Settings Settings, TextBox Title)> drafts = [];

    const int FieldWidth = 820;

    public PackageCoverEditor(Store store, Episode episode)

    {

        this.store = store; this.episode = episode;
        UiSound.Configure(store.Settings);

        Text = "Kapak editörü • " + episode.Topic; Width = 960; Height = 850; MinimumSize = new(900,650); StartPosition = FormStartPosition.CenterParent; BackColor = Theme.Bg; ForeColor = Theme.Text; Font = Theme.Font();

        shared = VideoSettings.For(store.Settings,episode);
        Width = 1400;
        var previewPanel = new Panel { Dock = DockStyle.Right, Width = 360, Padding = new Padding(12), BackColor = Theme.Panel };
        var previewTitle = Theme.Label("KAPAK ÖNİZLEMESİ",12,true,width:336,height:40); previewTitle.Dock = DockStyle.Top;
        var previewImage = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Theme.Bg };
        var previewActions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 195, FlowDirection = FlowDirection.TopDown };
        var lastDesignLog = new List<string>();
        Action? approveDraft = null, redesignDraft = null;
        var approveCover = Theme.Button("ONAYLA VE KULLAN",()=>approveDraft?.Invoke(),true,330); approveCover.Enabled = false;
        var redesignCover = Theme.Button("YENİDEN TASARLA",()=>redesignDraft?.Invoke(),false,330); redesignCover.Enabled = false;
        previewActions.Controls.Add(approveCover); previewActions.Controls.Add(redesignCover);
        previewActions.Controls.Add(Theme.Button("MODEL DENEMELERİNİ GÖSTER",()=> {
            using var dialog=StudioForm.Dialog("Kapak üretimi • model denemeleri",950,550);
            dialog.Controls.Add(new TextBox { Dock=DockStyle.Fill, Multiline=true, ReadOnly=true, ScrollBars=ScrollBars.Vertical, BackColor=Theme.Bg, ForeColor=Theme.Text, Font=Theme.Font(), Text=lastDesignLog.Count==0 ? "Henüz üretim denenmedi. TASARLA tuşuyla başlat." : string.Join(Environment.NewLine,lastDesignLog) });
            dialog.ShowDialog(this);
        },false,330));
        previewPanel.Controls.Add(previewImage); previewPanel.Controls.Add(previewTitle); previewPanel.Controls.Add(previewActions);
        FormClosed += (_,_) => previewImage.Image?.Dispose();

        var tabs = new StudioTabs { Dock = DockStyle.Fill };

        foreach (var (_,vertical,script) in Rules.Scripts(episode)) {

            var flow = Tab(tabs,vertical ? "Shorts " + (drafts.Count(d=>d.Vertical)+1) : "Normal video");

            flow.Controls.Add(Theme.Label(script.Title,10,true,Theme.Teal,FieldWidth,48));

            var settings = VideoSettings.For(store.Settings,episode,script);

            var title = Theme.Input(script.ThumbnailText == "" ? script.Title : script.ThumbnailText,width:FieldWidth); Field(flow,"Kapakta gösterilecek metin",title); drafts.Add((script,vertical,settings,title));

            var showChannel = Theme.Check("Kapakta kanal adını göster", settings.CoverShowChannelName, FieldWidth); flow.Controls.Add(showChannel);

            var channelName = Theme.Input(settings.ChannelName, width: FieldWidth); Field(flow, "Kapakta gösterilecek kanal adı • elle yaz", channelName);
            var channelButton = Theme.Button("Kanal İsmini Manuel Gir", () => { showChannel.Checked = true; channelName.Focus(); channelName.SelectAll(); }, false, 320); flow.Controls.Add(channelButton);

            var mode = Theme.Combo(["Şablon","Kendi PNG kapak"],settings.CoverMode,FieldWidth); Field(flow,"Kapak türü • YouTube küçük resmi",mode);

            var style = Theme.Combo(CoverDesigner.Styles,settings.CoverStyle,FieldWidth); Field(flow,"Tasarım tarzı",style);

            var auto = Theme.Check("Tarzı konuya göre otomatik seç",settings.CoverAutoStyle,FieldWidth); flow.Controls.Add(auto);
            var ai = Theme.Check("KONUYA GÖRE YAPAY ZEKÂ KAPAK TASARIMI",settings.CoverAiEnabled,FieldWidth); flow.Controls.Add(ai);
            var designInstructions = Theme.Input(settings.CoverDesignInstructions, multi:true, width:FieldWidth);
            Field(flow,"TASARIMA TALİMAT EKLE • isteğe bağlı",designInstructions);
            readers.Add(()=>settings.CoverDesignInstructions = designInstructions.Text.Trim());
            var reference=Theme.Input(settings.CoverReferenceImage,width:FieldWidth);reference.ReadOnly=true;Field(flow,"Önceki kapak • tasarım referansı",reference);
            void UseReference(string file){try{reference.Text=CoverReference.Import(store,file);ai.Checked=true;}catch(Exception ex){Theme.Alert(this,ex.Message);}}
            flow.Controls.Add(Theme.Button("Önceki kapağı referans olarak seç",()=>{using var picker=new OpenFileDialog{Filter="Kapak görselleri|*.png;*.jpg;*.jpeg;*.bmp"};if(picker.ShowDialog(this)==DialogResult.OK)UseReference(picker.FileName);},false,FieldWidth));
            flow.Controls.Add(Theme.Button("Arşivden referans kapak seç",()=>{var selected=CoverArchive.Pick(store,this);if(selected!=null)UseReference(selected);},false,FieldWidth));
            flow.Controls.Add(Theme.Button("Kapak referansını kaldır",()=>reference.Text="",false,300));
            flow.Controls.Add(Theme.Label("Önceki kapağın renk, ışık ve tarzı örnek alınır; yeni konuya göre tasarlanır. Eski yazı görselden kopyalanmaz, güncel başlık ayrıca eklenir. Referans normal video ve Shorts için ayrı kaydedilir.",9,width:FieldWidth,height:65));
            readers.Add(()=>settings.CoverReferenceImage=reference.Text);
            flow.Controls.Add(Theme.Label("Konu ve senaryo esas alınır; kapakta yalnızca başlık yazılır. NVIDIA, Gemini ve OpenAI görsel modelleri sırayla denenir. Üretim olmazsa konuya göre otomatik kapak hazırlanır.",9,width:FieldWidth,height:55));

            var png = ImageField(flow,"Hazır kapak görseli",settings.CoverPng,vertical);

            png.TextChanged += (_,_) => { if (!string.IsNullOrWhiteSpace(png.Text)) mode.SelectedItem="Kendi PNG kapak"; };

            var separateCards = Theme.Check("Bu video için açılış / kapanışı ayrı ayarla",script.CoverSettings?.ScriptCardsOverride == true,FieldWidth); flow.Controls.Add(separateCards);

            var ownOpening = Theme.Check("Açılış kapağını videonun başına ekle",settings.OpeningCardEnabled,FieldWidth); flow.Controls.Add(ownOpening);

            var ownOpeningFile = ImageField(flow,"Bu videonun açılış kapağı",settings.OpeningCardFile,vertical);
            flow.Controls.Add(Theme.Button("Açılış için arşivden seç",()=>{var selected=CoverArchive.Pick(store,this);if(selected!=null)ownOpeningFile.Text=selected;},false,300));

            var ownOpeningSeconds = Theme.Number((decimal)settings.OpeningCardSeconds,1,15,FieldWidth); Field(flow,"Açılış süresi • saniye",ownOpeningSeconds);

            var ownClosing = Theme.Check("Kapanış kapağını videonun sonuna ekle",settings.ClosingCardEnabled,FieldWidth); flow.Controls.Add(ownClosing);

            var ownClosingFile = ImageField(flow,"Bu videonun kapanış kapağı",settings.ClosingCardFile,vertical);
            flow.Controls.Add(Theme.Button("Kapanış için arşivden seç",()=>{var selected=CoverArchive.Pick(store,this);if(selected!=null)ownClosingFile.Text=selected;},false,300));

            var ownClosingSeconds = Theme.Number((decimal)settings.ClosingCardSeconds,1,15,FieldWidth); Field(flow,"Kapanış süresi • saniye",ownClosingSeconds);

            ownOpening.CheckedChanged += (_,_) => separateCards.Checked=true;

            ownClosing.CheckedChanged += (_,_) => separateCards.Checked=true;

            ownOpeningSeconds.ValueChanged += (_,_) => separateCards.Checked=true;

            ownClosingSeconds.ValueChanged += (_,_) => separateCards.Checked=true;

            ownOpeningFile.TextChanged += (_,_) => { separateCards.Checked=true; ownOpening.Checked=!string.IsNullOrWhiteSpace(ownOpeningFile.Text); };

            ownClosingFile.TextChanged += (_,_) => { separateCards.Checked=true; ownClosing.Checked=!string.IsNullOrWhiteSpace(ownClosingFile.Text); };

            flow.Controls.Add(Theme.Button("Seçtiğim hazır kapağı açılışta da kullan",()=>{try{MediaFormat.Image(png.Text.Trim(),vertical);ownOpeningFile.Text=png.Text.Trim();separateCards.Checked=ownOpening.Checked=true;}catch(Exception ex){Theme.Alert(this,ex.Message);}},false,400));

            flow.Controls.Add(Theme.Label(vertical ? "Shorts kapağı ve açılış/kapanış görselleri 9:16 olmalıdır. YouTube küçük resmi ile videonun açılışı ayrı seçimlerdir." : "Normal video kapağı ve açılış/kapanış görselleri 16:9 olmalıdır.",10,width:FieldWidth,height:55));

            var background = ImageField(flow,"Arka plan görseli",settings.CoverBackground);

            var logo = ImageField(flow,"Kapak üzerindeki logo",settings.CoverLogo);
            flow.Controls.Add(Theme.Button("Yapay zekâ ile logo tasarla",()=>{using var editor=new AiDesignDialog(store,true,settings.ChannelName);if(editor.ShowDialog(this)==DialogResult.OK)logo.Text=editor.SavedPath;},false,300));
            flow.Controls.Add(Theme.Button("Logo arşivinden seç",()=>{var path=CoverArchive.Pick(store,this,AiDesignDialog.LogoFiles(store),"Arşivden logo seç");if(path!=null)logo.Text=path;},false,300));

            var font = Theme.Combo(System.Drawing.FontFamily.Families.Select(f=>f.Name).Append(settings.CoverFont).Distinct().Order().ToArray(),settings.CoverFont,FieldWidth); Field(flow,"Yazı tipi",font);

            var position = Theme.Combo(["Sol üst","Sağ üst","Sol alt","Sağ alt"],settings.CoverLogoPosition,FieldWidth); Field(flow,"Kapak logosunun konumu",position);

            var accent = Theme.Input(settings.CoverAccent,width:FieldWidth); Field(flow,"Vurgu rengi • boşsa şablon rengi",accent);

            flow.Controls.Add(Theme.Button("Vurgu rengini seç",()=>{ using var color=new ColorDialog(); if(color.ShowDialog(this)==DialogResult.OK) accent.Text=ColorTranslator.ToHtml(color.Color); },false,240));


            var paid = Theme.Check("Gemini / OpenAI ücretli görsel API kullanımına izin ver • NVIDIA için gerekli değil",settings.CoverPaidAllowed,FieldWidth); flow.Controls.Add(paid);
            var local = Theme.Check("Bulut üretemezse yerel yapay zekâ motorunu kullan • ComfyUI", settings.CoverLocalEnabled, FieldWidth); flow.Controls.Add(local);
            var workflow = Theme.Input(settings.CoverLocalWorkflow,width:FieldWidth); Field(flow,"Yerel motor • ComfyUI API iş akışı dosyası",workflow);
            if (string.IsNullOrWhiteSpace(workflow.Text)) { var installed = LocalCoverEngine.FindInstalledWorkflow(); if (installed != "") { workflow.Text=installed; local.Checked=true; } }
            flow.Controls.Add(Theme.Button("Yerel iş akışı dosyasını seç",()=> { using var picker = new OpenFileDialog { Filter="ComfyUI API iş akışı|*.json" }; if (picker.ShowDialog(this)==DialogResult.OK) workflow.Text=picker.FileName; },false,300));
            var motorStatus = Theme.Label("Yerel motor • başlat tuşuyla açabilirsin.", 10, color:Theme.Success, width:FieldWidth, height:55); flow.Controls.Add(motorStatus);
            Button? startMotor = null;
            startMotor = Theme.Button("Yerel motoru başlat", async () => {
                startMotor!.Enabled = false;
                using var lifetime = new CancellationTokenSource();
                EventHandler closed = (_,_) => lifetime.Cancel(); Disposed += closed;
                try {
                    if (string.IsNullOrWhiteSpace(workflow.Text)) {
                        using var picker = new OpenFileDialog { Filter="ComfyUI API iş akışı|*.json", Title="Motor klasöründeki anka-sdxl.json dosyasını seç" };
                        if (picker.ShowDialog(this)!=DialogResult.OK) return; workflow.Text=picker.FileName;
                    }
                    await LocalCoverEngine.Start(workflow.Text.Trim(), message => { if (!motorStatus.IsDisposed) motorStatus.Text=message; }, lifetime.Token);
                    local.Checked=true;
                } catch (OperationCanceledException) { }
                catch (Exception ex) { if (!motorStatus.IsDisposed) motorStatus.Text=ex.Message; }
                finally { Disposed -= closed; if (!startMotor.IsDisposed) startMotor.Enabled=true; }
            }, true, 300); flow.Controls.Add(startMotor);
            flow.Controls.Add(Theme.Label("ComfyUI açık olmalı (127.0.0.1:8188). API iş akışındaki olumlu görsel talimatına {{ANKA_PROMPT}} ekle. Yerel üretim API kotasını tüketmez.",9,width:FieldWidth,height:55));

            var provider = Theme.Combo(new[]{"NVIDIA,Gemini,OpenAI","Gemini,NVIDIA,OpenAI","OpenAI,NVIDIA,Gemini",settings.CoverProviderOrder}.Distinct().ToArray(),settings.CoverProviderOrder.Contains("NVIDIA") ? settings.CoverProviderOrder : "NVIDIA,"+settings.CoverProviderOrder,FieldWidth); Field(flow,"API anahtarı / görsel sağlayıcı deneme sırası",provider);
            var nvidia = Theme.Combo(CoverFallback.ModelsFor("NVIDIA",settings),settings.CoverNvidiaModel,FieldWidth); Field(flow,"NVIDIA görsel modeli • kayıtlı NVIDIA API anahtarını kullanır",nvidia);

            var gemini = Theme.Combo(CoverFallback.ModelsFor("Gemini",settings),settings.CoverGeminiModel,FieldWidth); Field(flow,"Gemini görsel modeli",gemini);

            var openai = Theme.Combo(CoverFallback.ModelsFor("OpenAI",settings),settings.CoverOpenAiModel,FieldWidth); Field(flow,"OpenAI görsel modeli",openai);

            var limit = Theme.Number(settings.CoverMonthlyRequests,0,1000,FieldWidth); Field(flow,"Aylık kapak API istek sınırı",limit);

            Action read = ()=> { settings.CoverChannelName=settings.ChannelName=channelName.Text.Trim(); settings.CoverShowChannelName=showChannel.Checked; settings.CoverMode=mode.Text; settings.CoverStyle=style.Text; settings.CoverAutoStyle=auto.Checked; settings.CoverPng=png.Text.Trim(); settings.CoverBackground=background.Text.Trim(); settings.CoverLogo=logo.Text.Trim(); settings.CoverFont=font.Text; settings.CoverLogoPosition=position.Text; settings.CoverAccent=accent.Text.Trim(); settings.CoverAiEnabled=ai.Checked; settings.CoverPaidAllowed=paid.Checked; settings.CoverProviderOrder=provider.Text; settings.CoverGeminiModel=gemini.Text; settings.CoverOpenAiModel=openai.Text; settings.CoverMonthlyRequests=(int)limit.Value; };

            readers.Add(()=>{read();settings.ScriptCardsOverride=separateCards.Checked;if(separateCards.Checked){settings.OpeningCardEnabled=ownOpening.Checked;settings.OpeningCardFile=ownOpeningFile.Text.Trim();settings.OpeningCardSeconds=(double)ownOpeningSeconds.Value;settings.ClosingCardEnabled=ownClosing.Checked;settings.ClosingCardFile=ownClosingFile.Text.Trim();settings.ClosingCardSeconds=(double)ownClosingSeconds.Value;}});
            var aiStatus = Theme.Label("Hazır • TASARLA tuşuyla kapak üretimini başlat.",10,color:Theme.Success,width:FieldWidth,height:45);
            bool designing = false;
            Button? generate = null;
            generate = Theme.Button("TASARLA • Konuya göre yapay zekâ kapağı", () => _ = GenerateCover(), true, 600);
            async Task GenerateCover() {
                if (designing || IsDisposed) return;
                read();
                settings.CoverReferenceImage=reference.Text;
                settings.CoverLocalEnabled = local.Checked; settings.CoverLocalWorkflow = workflow.Text.Trim();
                if(settings.CoverLocalEnabled && !File.Exists(settings.CoverLocalWorkflow)){settings.CoverLocalWorkflow=LocalCoverEngine.FindInstalledWorkflow();workflow.Text=settings.CoverLocalWorkflow;}
                settings.CoverNvidiaModel = nvidia.Text;
                settings.CoverDesignInstructions = designInstructions.Text.Trim();
                designing = true;
                aiStatus.Text = "Kapak üretimi başlatılıyor • API bağlantısı ve model sırası kontrol ediliyor…";
                approveCover.Enabled = redesignCover.Enabled = false;
                previewTitle.Text = "KAPAK TASARLANIYOR…";
                try {
                    var copy = PackageArtwork.Snapshot(settings); copy.CoverAiEnabled = true; copy.CoverAutoStyle = false; copy.CoverStyle = "Nur Kapısı • Altın sinema"; copy.CoverMode = "Şablon"; copy.CoverBackground = ""; copy.CoverPng = ""; copy.CoverShowChannelName = false; copy.CoverLogo = "";
                    string context = episode.Topic + "\nVideo başlığı: " + script.Title + "\n" + string.Join(" ",script.Scenes.Take(4).Select(s=>s.Narration));
                    var directory = Path.Combine(store.Folder(episode), "kapak-taslaklari", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
                    var output = Path.Combine(directory, "kapak.png");
                    var designLog = lastDesignLog; designLog.Clear();

                    await new CoverFallback(store, message => { designLog.Add(DateTime.Now.ToString("HH:mm:ss") + " " + message); if (!aiStatus.IsDisposed) aiStatus.Text = message; }).Create(output, copy, context, title.Text.Trim(), vertical, CancellationToken.None);
                    File.WriteAllLines(Path.Combine(directory,"tasarim-islem-kaydi.txt"),designLog);
                    bool aiGenerated = File.Exists(Path.Combine(directory,"ai-cover-background.png"));
                    if (IsDisposed) return;
                    using var image = Image.FromFile(output); var oldImage = previewImage.Image; previewImage.Image = new Bitmap(image); oldImage?.Dispose();
                    previewTitle.Text = aiGenerated ? "ÖNİZLE VE ONAYLA" : "OTOMATİK KAPAK • ÖNİZLE VE ONAYLA"; aiStatus.Text = aiGenerated ? "Tasarım hazır. Sağdaki önizlemeyi onayla veya yeniden tasarla." : "Yapay zekâ üretimi tamamlanamadı. Nedenini MODEL DENEMELERİNİ GÖSTER tuşundan görebilirsin. Otomatik kapak onayına sunuldu.";
                    approveDraft = () => { png.Text = CoverArchive.StoreCover(store,output); ai.Checked = false; showChannel.Checked = false; approveCover.Enabled = false; previewTitle.Text = "KAPAK ONAYLANDI"; aiStatus.Text = "Onaylandı ve kapak arşivine eklendi. Kalıcı uygulamak için kapak ayarlarını kaydet."; };
                    redesignDraft = () => _ = GenerateCover(); approveCover.Enabled = redesignCover.Enabled = true;
                } catch (Exception ex) { Theme.Alert(this,ex.Message); }
                finally { designing = false; if (!IsDisposed && previewTitle.Text == "KAPAK TASARLANIYOR…") { previewTitle.Text = "TASARIM TAMAMLANAMADI"; redesignDraft = () => _ = GenerateCover(); redesignCover.Enabled = true; } }
            }
            flow.Controls.Add(generate); flow.Controls.SetChildIndex(generate,flow.Controls.IndexOf(ai)+1);
            flow.Controls.Add(aiStatus); flow.Controls.SetChildIndex(aiStatus,flow.Controls.IndexOf(generate));

            readers.Add(()=>settings.CoverNvidiaModel=nvidia.Text);
            readers.Add(()=> { settings.CoverLocalEnabled=local.Checked; settings.CoverLocalWorkflow=workflow.Text.Trim(); });
            flow.Controls.Add(Theme.Label("GOOGLE FLOW • Üyeliğindeki kredilerle tarayıcıda üret, indirilen görseli burada önizle ve onayla. Kredi bakiyesi Flow hesabında görülür.",12,width:FieldWidth,height:65));
            flow.Controls.Add(Theme.Button("FLOW’U AÇ • Kapak talimatını kopyala",()=> {
                string direction = "Create a cinematic YouTube thumbnail background. Subject: " + episode.Topic + ". Context: " + string.Join(" ",script.Scenes.Take(4).Select(s=>s.Narration)) + ". " + (vertical ? "Portrait 9:16." : "Landscape 16:9.") + " Match my uploaded reference covers: midnight navy, warm amber and gold, strong subject, readable dark negative space for title. No text, no logo, no lettering. " + designInstructions.Text;
                Clipboard.SetText(direction); Theme.Open("https://labs.google/fx/tools/flow"); aiStatus.Text="Flow açıldı; talimat panoya kopyalandı. Flow’da yapıştır, referans kapakları ekle ve üretilen görseli indir. Ardından FLOW GÖRSELİNİ AL tuşuna bas.";
            },false,600));
            flow.Controls.Add(Theme.Button("FLOW GÖRSELİNİ AL • Önizle ve onayla",()=> {
                if (designing) return;
                using var picker = new OpenFileDialog { Filter="Görseller|*.png;*.jpg;*.jpeg;*.webp" };
                if (picker.ShowDialog(this)!=DialogResult.OK) return;
                try {
                    read();
                    var directory=Path.Combine(store.Folder(episode),"kapak-taslaklari",Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
                    string imported=Path.Combine(directory,"flow-background.png"), output=Path.Combine(directory,"kapak.png");
                    using(var source=Image.FromFile(picker.FileName)) { if(source.Width<256 || source.Height<256 || (long)source.Width*source.Height>40_000_000)throw new InvalidOperationException("Görsel ölçüsü uygun değil."); source.Save(imported,System.Drawing.Imaging.ImageFormat.Png); }
                    var copy=PackageArtwork.Snapshot(settings); copy.CoverMode="Şablon"; copy.CoverBackground=imported; copy.CoverShowChannelName=false; copy.CoverLogo="";
                    CoverDesigner.Save(output,copy,title.Text.Trim(),vertical);
                    using(var ready=Image.FromFile(output)) { var old=previewImage.Image; previewImage.Image=new Bitmap(ready); old?.Dispose(); }
                    previewTitle.Text="FLOW KAPAĞI • ÖNİZLE VE ONAYLA"; aiStatus.Text="Flow görseli alındı; video başlığı eklendi. Sağdaki önizlemeyi onayla.";
                    approveDraft=()=> { png.Text=output; ai.Checked=false; showChannel.Checked=false; approveCover.Enabled=false; previewTitle.Text="KAPAK ONAYLANDI"; aiStatus.Text="Onaylandı. Kapak ayarlarını kaydet."; };
                    redesignDraft=()=> { Clipboard.SetText("Redesign this image, no lettering, no logos. Subject: " + episode.Topic + ". " + designInstructions.Text); Theme.Open("https://labs.google/fx/tools/flow"); aiStatus.Text="Yeniden tasarım talimatı kopyalandı. Yeni görseli FLOW GÖRSELİNİ AL ile getir."; };
                    approveCover.Enabled=redesignCover.Enabled=true;
                } catch(Exception ex) { Theme.Alert(this,ex.Message); }
            },false,600));
            flow.Controls.Add(Theme.Label("Flow video çıktısını Sahne editöründeki kendi video dosyanı seçme alanından kullanabilirsin. Bu bağlantı Flow’u tarayıcıda kullanır; TubeFlow otomatik kredi harcaması veya API üretimi yapmaz.",10,width:FieldWidth,height:60));
            flow.Controls.Add(Theme.Label("Seçili model önce denenir, başarısız olursa diğer görsel modellerine geçilir. Anahtarı olmayan servisler atlanır; aylık istek sınırı korunur. Tasarım sağda onayına sunulur.",10,width:FieldWidth,height:65));

            flow.Controls.Add(Theme.Button("Kapağı önizle",()=> { try { read(); var copy=PackageArtwork.Snapshot(settings); copy.ChannelName=settings.ChannelName; if(copy.CoverAutoStyle) copy.CoverStyle=CoverDesigner.Suggest(episode.Topic); using var image=CoverDesigner.Draw(copy,title.Text.Trim(),vertical); using var dialog=StudioForm.Dialog("Kapak önizlemesi",1000,730); dialog.Controls.Add(new PictureBox{Dock=DockStyle.Fill,Image=image,SizeMode=PictureBoxSizeMode.Zoom}); dialog.ShowDialog(this); } catch(Exception ex) { Theme.Alert(this,ex.Message); } },true,250));

            // Keep existing controls and save handlers, with each task on its own short page.
            var controls = flow.Controls.Cast<Control>().ToArray();
            int Index(Control control) => Array.IndexOf(controls,control);
            int readyStart = Index(png)-1, cardsStart=Index(separateCards), lookStart=Index(background)-1;
            int engineStart=Index(paid);
            int flowStart=Array.FindIndex(controls,c=>c.Text.StartsWith("GOOGLE FLOW •",StringComparison.Ordinal));
            var sections=new StudioTabs {Dock=DockStyle.Fill};
            var designPage=Tab(sections,"Tasarla");
            var readyPage=Tab(sections,"Hazır kapak");
            var lookPage=Tab(sections,"Görünüm");
            var videoPage=Tab(sections,"Video kartları");
            var enginePage=Tab(sections,"Motor / API");
            var flowPage=Tab(sections,"Google Flow");
            designPage.Controls.Add(Theme.Label("Konu ve senaryoya göre kapak üret. Sonucu sağda önizle ve onayla.",11,width:FieldWidth,height:55));
            foreach(var control in controls) {
                int i=Index(control);
                FlowLayoutPanel target;
                if(i>=flowStart) target=flowPage;
                else if(i>=engineStart) target=enginePage;
                else if(i>=lookStart) target=lookPage;
                else if(i>=cardsStart) target=videoPage;
                else if(i>=readyStart) target=readyPage;
                else if(i>=Index(showChannel) && i<Index(ai) && control!=generate && control!=aiStatus) target=lookPage;
                else target=designPage;
                target.Controls.Add(control);
            }
            ai.Visible=false;
            designPage.Controls.SetChildIndex(aiStatus,designPage.Controls.Count-1);
            designPage.Controls.SetChildIndex(generate,designPage.Controls.Count-1);
            var host=flow.Parent!; host.Controls.Remove(flow); host.Controls.Add(sections); flow.Dispose();

        }

        var cards = Tab(tabs,"Açılış / kapanış / video logosu");

        cards.Controls.Add(Theme.Label("Bu sekme ortak varsayılanları ve video logosunu ayarlar. Normal video / Shorts sekmelerinden ayrı açılış-kapanış seçebilirsin. Küçük resim ile videonun içindeki kartlar ayrıdır.",10,width:FieldWidth,height:65));

        var opening=Theme.Check("Açılış kapağını videonun başına ekle",shared.OpeningCardEnabled,FieldWidth); cards.Controls.Add(opening); var openingFile=ImageField(cards,"Açılış kartı görseli",shared.OpeningCardFile); var openingSeconds=Theme.Number((decimal)shared.OpeningCardSeconds,1,15,FieldWidth); Field(cards,"Açılış süresi • saniye",openingSeconds);
        cards.Controls.Add(Theme.Button("Açılış için arşivden seç",()=>{var selected=CoverArchive.Pick(store,this);if(selected!=null)openingFile.Text=selected;},false,300));

        var closing=Theme.Check("Kapanış kapağını videonun sonuna ekle",shared.ClosingCardEnabled,FieldWidth); cards.Controls.Add(closing); var closingFile=ImageField(cards,"Kapanış kartı görseli",shared.ClosingCardFile); var closingSeconds=Theme.Number((decimal)shared.ClosingCardSeconds,1,15,FieldWidth); Field(cards,"Kapanış süresi • saniye",closingSeconds);
        cards.Controls.Add(Theme.Button("Kapanış için arşivden seç",()=>{var selected=CoverArchive.Pick(store,this);if(selected!=null)closingFile.Text=selected;},false,300));

        openingFile.TextChanged += (_,_) => opening.Checked = !string.IsNullOrWhiteSpace(openingFile.Text);

        closingFile.TextChanged += (_,_) => closing.Checked = !string.IsNullOrWhiteSpace(closingFile.Text);

        void DesignCard(TextBox field, CheckBox enabled, string heading) { using var editor=new CardEditor(store,File.Exists(field.Text)?field.Text:null);editor.Text=heading;if(editor.ShowDialog(this)==DialogResult.OK && editor.SavedPath!=""){field.Text=editor.SavedPath;enabled.Checked=true;} }

        cards.Controls.Add(Theme.Button("Açılış kapağını tasarla / düzenle",()=>DesignCard(openingFile,opening,"Açılış kapağı • videonun başı"),false,350));

        cards.Controls.Add(Theme.Button("Kapanış kapağını tasarla / düzenle",()=>DesignCard(closingFile,closing,"Kapanış kapağı • videonun sonu"),false,350));

        cards.Controls.Add(Theme.Label("Açılış süresince anlatım başlamaz. Konuşma bittikten sonra kapanış görünür. Bu görseller MP4 dosyasının içine eklenir.",10,width:FieldWidth,height:60));

        var videoLogo=Theme.Check("Videonun üzerinde kanal logosunu göster",shared.VideoLogoEnabled,FieldWidth); cards.Controls.Add(videoLogo); var videoLogoFile=ImageField(cards,"Video logosu",shared.VideoLogo); var videoLogoPosition=Theme.Combo(["Sol üst","Sağ üst","Sol alt","Sağ alt"],shared.VideoLogoPosition,FieldWidth); Field(cards,"Video logosunun konumu",videoLogoPosition); var videoLogoWidth=Theme.Number(shared.VideoLogoWidthPercent,5,20,FieldWidth); Field(cards,"Logo genişliği • görüntünün yüzdesi",videoLogoWidth);
        cards.Controls.Add(Theme.Button("Yapay zekâ ile logo tasarla",()=>{using var editor=new AiDesignDialog(store,true,shared.ChannelName);if(editor.ShowDialog(this)==DialogResult.OK){videoLogoFile.Text=editor.SavedPath;videoLogo.Checked=true;}},false,300));
        cards.Controls.Add(Theme.Button("Logo arşivinden seç",()=>{var path=CoverArchive.Pick(store,this,AiDesignDialog.LogoFiles(store),"Arşivden logo seç");if(path!=null){videoLogoFile.Text=path;videoLogo.Checked=true;}},false,300));

        readers.Add(()=>{shared.OpeningCardEnabled=opening.Checked;shared.OpeningCardFile=openingFile.Text.Trim();shared.OpeningCardSeconds=(double)openingSeconds.Value;shared.ClosingCardEnabled=closing.Checked;shared.ClosingCardFile=closingFile.Text.Trim();shared.ClosingCardSeconds=(double)closingSeconds.Value;shared.VideoLogoEnabled=videoLogo.Checked;shared.VideoLogo=videoLogoFile.Text.Trim();shared.VideoLogoPosition=videoLogoPosition.Text;shared.VideoLogoWidthPercent=(int)videoLogoWidth.Value;});

        var buttons=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=65,Padding=new Padding(12)};

        buttons.Controls.Add(Theme.Button("Paketin kapak ayarlarını kaydet",()=>{try{Save();DialogResult=DialogResult.OK;Close();}catch(Exception ex){Theme.Alert(this,ex.Message);}},true,350)); buttons.Controls.Add(Theme.Button("Vazgeç",Close,false,170));

        Controls.Add(tabs);Controls.Add(previewPanel);Controls.Add(buttons); ModernScrollbars.Apply(this);

    }

    static FlowLayoutPanel Tab(TabControl tabs,string title) { var tab=new TabPage(title){BackColor=Theme.Bg,Padding=new Padding(12)};var flow=Theme.Flow(FieldWidth+25);if(flow is StudioFlowPanel stableFlow)stableFlow.KeepScrollOnFocus=true;flow.Dock=DockStyle.Fill;flow.AutoScroll=true;flow.WrapContents=false;flow.FlowDirection=FlowDirection.TopDown;tab.Controls.Add(flow);tabs.TabPages.Add(tab);return flow; }

    static void Field(FlowLayoutPanel flow,string name,Control control) {flow.Controls.Add(Theme.Label(name,10,true,width:FieldWidth,height:28));control.Margin=new Padding(0,0,0,12);flow.Controls.Add(control);}

    TextBox ImageField(FlowLayoutPanel flow,string title,string value,bool? vertical=null) {var field=Theme.Input(value,width:FieldWidth);Field(flow,title,field);flow.Controls.Add(Theme.Button(title+" seç",()=>{using var dialog=new OpenFileDialog{Title=title,Filter="Görseller|*.png;*.jpg;*.jpeg;*.webp;*.bmp",RestoreDirectory=true,InitialDirectory=Path.Combine(store.Root,"cover-assets")};if(dialog.ShowDialog(this)==DialogResult.OK){try{if(vertical.HasValue)MediaFormat.Image(dialog.FileName,vertical.Value);field.Text=dialog.FileName;}catch(Exception ex){Theme.Alert(this,ex.Message);}}},false,320));return field;}

    public void Save()

    {

        foreach(var read in readers)read();

        foreach(var draft in drafts) {

            if(!draft.Settings.ScriptCardsOverride) PackageArtwork.Apply(draft.Settings,shared,PackageArtwork.CardFields);

            ClosingCards.Validate(draft.Settings,draft.Vertical);

            if(draft.Settings.CoverMode=="Kendi PNG kapak" && draft.Settings.CoverPng!="")MediaFormat.Image(draft.Settings.CoverPng,draft.Vertical);

        }

        var files=drafts.SelectMany(d=>new[]{d.Settings.CoverMode=="Kendi PNG kapak"?d.Settings.CoverPng:"",d.Settings.CoverBackground,d.Settings.CoverLogo}).Concat(new[]{shared.VideoLogoEnabled?shared.VideoLogo:""}).Where(p=>p!="").Distinct().ToArray();

        foreach(var path in files){if(!File.Exists(path))throw new InvalidOperationException("Görsel bulunamadı: "+Path.GetFileName(path));using var image=Image.FromFile(path);if((long)image.Width*image.Height>40_000_000)throw new InvalidOperationException("Görsel çok büyük.");}

        foreach(var draft in drafts){if(draft.Settings.CoverMode=="Kendi PNG kapak" && draft.Settings.CoverPng=="")throw new InvalidOperationException("Hazır kapak görselini seç.");using var image=CoverDesigner.Draw(draft.Settings,draft.Title.Text,draft.Vertical);}

        var folder=Path.Combine(store.Folder(episode),"cover-assets");Directory.CreateDirectory(folder);

        string Keep(string path){if(path==""||!File.Exists(path))return path;var target=Path.Combine(folder,Reliability.FileHash(path)+Path.GetExtension(path));if(!Path.GetFullPath(path).Equals(Path.GetFullPath(target),StringComparison.OrdinalIgnoreCase))File.Copy(path,target,true);return target;}

        shared.OpeningCardFile=Keep(shared.OpeningCardFile);shared.ClosingCardFile=Keep(shared.ClosingCardFile);shared.VideoLogo=Keep(shared.VideoLogo);

        foreach(var draft in drafts){draft.Settings.CoverPng=Keep(draft.Settings.CoverPng);draft.Settings.CoverBackground=Keep(draft.Settings.CoverBackground);draft.Settings.CoverLogo=Keep(draft.Settings.CoverLogo);draft.Settings.OpeningCardFile=Keep(draft.Settings.OpeningCardFile);draft.Settings.ClosingCardFile=Keep(draft.Settings.ClosingCardFile);}

        episode.ArtworkSettings=PackageArtwork.Snapshot(shared);

        foreach(var draft in drafts){draft.Script.CoverSettings=PackageArtwork.Snapshot(draft.Settings);draft.Script.ThumbnailText=draft.Title.Text.Trim();}

        episode.ArtworkPending=episode.TextDesignPending=episode.Videos.Count>0;episode.Approved=false;episode.ApprovalHash="";

        episode.Log("Paketin kapak ayarları kaydedildi. Açılış: " + (shared.OpeningCardEnabled ? shared.OpeningCardSeconds + " saniye • videonun başına eklenecek" : "kapalı") + "; kapanış: " + (shared.ClosingCardEnabled ? shared.ClosingCardSeconds + " saniye • videonun sonuna eklenecek" : "kapalı") + ". Mevcut video varsa yeni üretimde uygulanır.");store.Save();

    }

    public void CaptureTest(string folder)

    {

        Directory.CreateDirectory(folder);var tabs=Controls.OfType<TabControl>().Single();

        for(int i=0;i<tabs.TabPages.Count;i++){tabs.SelectedIndex=i;PerformLayout();Application.DoEvents();using var image=new Bitmap(Width,Height);DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(folder,"cover-tab-"+i+".png"));}

    }

}

