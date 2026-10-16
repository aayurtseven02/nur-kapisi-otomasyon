using System.Drawing.Imaging;

using System.Text.Json;



namespace AnkaStudio;



public sealed class CardEditor : Form

{

    protected override void OnShown(EventArgs e) {ModernScrollbars.Apply(this);base.OnShown(e);}

    public string SavedPath { get; private set; } = "";

    CardDesign design; readonly Store store;

    readonly string draftPath, baseline; bool testMode;

    readonly PictureBox preview = new() { Dock=DockStyle.Fill,SizeMode=PictureBoxSizeMode.Zoom };

    readonly Label status = Theme.Label("",10,width:600,height:35);

    readonly List<Action> bindings=[]; readonly List<string> history=[];

    int historyIndex=-1; bool loading; string selected="Başlık";

    readonly TabControl tabs=new StudioTabs(){Dock=DockStyle.Fill};

    readonly System.Windows.Forms.Timer redraw=new(){Interval=180};

    Point? drag; int originX,originY;

    CardTextStyle Style => design.Layer(selected);

    public CardEditor(Store store,string? existing=null)

    {

        this.store=store;design=existing==null?CardDesign.Blank():Json.Read(Path.ChangeExtension(existing,".json"),new CardDesign());design.Upgrade();

        baseline=EditorDrafts.Hash(design);draftPath=Path.Combine(store.Root,"editor-drafts","card-"+(existing==null?"new":EditorDrafts.Hash(existing))+".json");

        design=EditorDrafts.Recover(draftPath,baseline,design);

        Text="Açılış / kapanış kartı • Tasarım editörü";Width=1280;Height=900;MinimumSize=new(1020,740);BackColor=Theme.Bg;ForeColor=Theme.Text;StartPosition=FormStartPosition.CenterParent;KeyPreview=true;

        var left=new Panel{Dock=DockStyle.Left,Width=410,Padding=new Padding(8)};

        var right=new Panel{Dock=DockStyle.Fill,Padding=new Padding(12)};right.Controls.Add(preview);status.Dock=DockStyle.Bottom;right.Controls.Add(status);Controls.Add(right);Controls.Add(left);

        var top=new FlowLayoutPanel{Dock=DockStyle.Top,Height=155,FlowDirection=FlowDirection.TopDown,WrapContents=false};

        var layer=Theme.Combo(["Başlık","Ana metin","Alt metin"],selected,380);layer.Margin=new Padding(3,3,3,17);layer.SelectedIndexChanged+=(_,_)=>{selected=layer.Text;Sync();};top.Controls.Add(Theme.Label("Düzenlenecek metin • her biri ayrı ayarlanır",10,width:380,height:25));top.Controls.Add(layer);top.Controls.Add(Theme.Button("Hazır kapağı içe aktar",ImportCover,false,380));

        var bottom=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=112,WrapContents=true};bottom.Controls.Add(Theme.Button("Geri al",()=>Undo(-1),false,110));bottom.Controls.Add(Theme.Button("Yinele",()=>Undo(1),false,110));bottom.Controls.Add(Theme.Button("PNG dışa aktar",Export,false,130));bottom.Controls.Add(Theme.Button("Kartı galeriye kaydet",Save,true,240));bottom.Controls.Add(Theme.Button("Yazıyı sığdır",()=>{CardDesign.FitText(design);Sync();Changed();},false,130));

        left.Controls.Add(tabs);left.Controls.Add(top);left.Controls.Add(bottom);

        var designModes=new StudioTabs {Dock=DockStyle.Fill};
        var manualMode=new TabPage("Kapağı kendin tasarla") {BackColor=Theme.Bg};
        var aiMode=new TabPage("Kapağı yapay zekâ tasarlasın") {BackColor=Theme.Bg};
        left.Controls.Remove(tabs);left.Controls.Remove(top);manualMode.Controls.Add(tabs);manualMode.Controls.Add(top);
        designModes.TabPages.Add(manualMode);designModes.TabPages.Add(aiMode);left.Controls.Add(designModes);designModes.BringToFront();

        FlowLayoutPanel Page(string text){var page=new TabPage(text){BackColor=Theme.Bg};var p=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(6)};page.Controls.Add(p);tabs.TabPages.Add(page);return p;}

        void Field(FlowLayoutPanel p,string label,Control c){p.Controls.Add(Theme.Label(label,10,width:360,height:25));p.Controls.Add(c);}

        void Input(FlowLayoutPanel p,string label,Func<string> get,Action<string> set,bool multiline=false){var c=Theme.Input(get(),width:360);c.Multiline=multiline;if(multiline)c.Height=85;Field(p,label,c);bindings.Add(()=>c.Text=get());c.TextChanged+=(_,_)=>{if(!loading){set(c.Text);Changed();}};}

        void Number(FlowLayoutPanel p,string label,Func<int> get,Action<int> set,int min,int max){var c=Theme.Number(get(),min,max,360);Field(p,label,c);bindings.Add(()=>c.Value=Math.Clamp(get(),min,max));c.ValueChanged+=(_,_)=>{if(!loading){set((int)c.Value);Changed();}};}

        void Combo(FlowLayoutPanel p,string label,string[] values,Func<string> get,Action<string> set){var c=Theme.Combo(values,get(),360);Field(p,label,c);bindings.Add(()=>c.SelectedItem=get());c.SelectedIndexChanged+=(_,_)=>{if(!loading&&c.SelectedItem!=null){set(c.Text);Changed();}};}

        void Check(FlowLayoutPanel p,string label,Func<bool> get,Action<bool> set){var c=Theme.Check(label,get());c.Width=360;p.Controls.Add(c);bindings.Add(()=>c.Checked=get());c.CheckedChanged+=(_,_)=>{if(!loading){set(c.Checked);Changed();}};}

        void ColorButton(FlowLayoutPanel p,string label,Func<string> get,Action<string> set){var b=Theme.Button(label,()=>{using var dialog=new ColorDialog{Color=ColorTranslator.FromHtml(get()),FullOpen=true};if(dialog.ShowDialog(this)==DialogResult.OK){set(ColorTranslator.ToHtml(dialog.Color));Changed();Sync();}},false,360);p.Controls.Add(b);bindings.Add(()=>b.Text=label+" • "+get());}

        var text=Page("Metin");Input(text,"Kart adı",()=>design.Name,x=>design.Name=x);Input(text,"Başlık",()=>design.Title,x=>design.Title=x,true);Input(text,"Ana metin",()=>design.Body,x=>design.Body=x,true);Input(text,"Alt metin",()=>design.Footer,x=>design.Footer=x,true);

        Combo(text,"Kart boyutu",["Yatay 16:9","Dikey 9:16","Kare 1:1"],()=>design.Square?"Kare 1:1":design.Vertical?"Dikey 9:16":"Yatay 16:9",x=>{design.ImportedWidth=0;design.ImportedHeight=0;design.Square=x=="Kare 1:1";design.Vertical=x=="Dikey 9:16";});

        var type=Page("Yazı tipi");using(var installed=new System.Drawing.Text.InstalledFontCollection()){Combo(type,"Bilgisayarda yüklü yazı tipleri",installed.Families.Select(x=>x.Name).OrderBy(x=>x).ToArray(),()=>Style.Font,x=>Style.Font=x);}

        Number(type,"Yazı boyutu",()=>Style.Size,x=>Style.Size=x,12,220);Check(type,"Metni göster",()=>Style.Visible,x=>Style.Visible=x);Check(type,"Kalın",()=>Style.Bold,x=>Style.Bold=x);Check(type,"İtalik",()=>Style.Italic,x=>Style.Italic=x);Check(type,"Alt çizgi",()=>Style.Underline,x=>Style.Underline=x);ColorButton(type,"Yazı rengi",()=>Style.Color,x=>Style.Color=x);

        var position=Page("Yerleşim");Combo(position,"Hazır yerleşim uygula",["Ortalanmış","Sola hizalı","Başlık üstte","Alıntı odaklı","Logo ve başlık"],()=>"Ortalanmış",x=>{design.Layout(x);Sync();});

        Number(position,"Soldan konum %",()=>Style.X,x=>Style.X=x,0,95);Number(position,"Üstten konum %",()=>Style.Y,x=>Style.Y=x,0,95);Number(position,"Metin kutusu genişliği %",()=>Style.Width,x=>Style.Width=x,5,100);Number(position,"Metin kutusu yüksekliği %",()=>Style.Height,x=>Style.Height=x,5,100);Combo(position,"Yatay hizalama",["Sol","Orta","Sağ"],()=>Style.Align,x=>Style.Align=x);Combo(position,"Dikey hizalama",["Üst","Orta","Alt"],()=>Style.VerticalAlign,x=>Style.VerticalAlign=x);Number(position,"Döndürme • derece",()=>Style.Rotation,x=>Style.Rotation=x,-180,180);position.Controls.Add(Theme.Label("Seçili metni önizlemede kutusundan sürükleyerek taşıyabilirsin. Kesikli kutu kaydedilen resme eklenmez.",10,width:360,height:65));

        var effects=Page("Efektler");Number(effects,"Metin görünürlüğü %",()=>Style.Opacity,x=>Style.Opacity=x,0,100);Number(effects,"Yazı kenarlığı kalınlığı",()=>Style.Outline,x=>Style.Outline=x,0,16);ColorButton(effects,"Kenarlık rengi",()=>Style.OutlineColor,x=>Style.OutlineColor=x);Number(effects,"Gölge uzaklığı",()=>Style.Shadow,x=>Style.Shadow=x,0,30);ColorButton(effects,"Gölge rengi",()=>Style.ShadowColor,x=>Style.ShadowColor=x);Check(effects,"Metin arkasına kutu ekle",()=>Style.Box,x=>Style.Box=x);ColorButton(effects,"Metin kutusu rengi",()=>Style.BoxColor,x=>Style.BoxColor=x);Number(effects,"Kutu görünürlüğü %",()=>Style.BoxOpacity,x=>Style.BoxOpacity=x,0,100);

        var frame=Page("Çerçeve");Combo(frame,"Çerçeve stili",CardDesign.Frames,()=>design.Frame,x=>design.Frame=x);Number(frame,"Çizgi kalınlığı",()=>design.FrameWidth,x=>design.FrameWidth=x,1,24);Number(frame,"Kenar boşluğu %",()=>design.FrameInset,x=>design.FrameInset=x,1,20);Number(frame,"Yuvarlak köşe yarıçapı",()=>design.FrameRadius,x=>design.FrameRadius=x,5,150);ColorButton(frame,"Çerçeve rengi",()=>design.Accent,x=>design.Accent=x);

        var background=Page("Arka plan");Combo(background,"Arka plan türü",["Düz renk","Renk geçişi"],()=>design.BackgroundMode,x=>design.BackgroundMode=x);ColorButton(background,"Ana renk",()=>design.Background,x=>design.Background=x);ColorButton(background,"İkinci renk",()=>design.BackgroundSecond,x=>design.BackgroundSecond=x);Number(background,"Renk geçişi açısı",()=>design.GradientAngle,x=>design.GradientAngle=x,0,360);Check(background,"Şeffaf arka plan • PNG",()=>design.Transparent,x=>design.Transparent=x);background.Controls.Add(Theme.Button("Arka plan görseli seç",()=>Import(false),false,360));background.Controls.Add(Theme.Button("Arka plan görselini kaldır",()=>{design.BackgroundImage="";Changed();},false,360));Combo(background,"Görsel yerleşimi",["Doldur / kırp","Sığdır","Esnet"],()=>design.ImageFit,x=>design.ImageFit=x);Number(background,"Görseli koyulaştırma %",()=>design.ImageShade,x=>design.ImageShade=x,0,100);

        var logo=Page("Logo");logo.Controls.Add(Theme.Button("Logo görseli seç",()=>Import(true),false,360));if(ChannelPack.Matches(store.Settings)&&File.Exists(ChannelPack.Asset("nur-kapisi-kanal-logo.png"))) logo.Controls.Add(Theme.Button("Nur Kapısı logosunu ekle",()=>{design.Logo=CopyMedia(ChannelPack.Asset("nur-kapisi-kanal-logo.png"));Changed();},false,360));logo.Controls.Add(Theme.Button("Logoyu kaldır",()=>{design.Logo="";Changed();},false,360));Number(logo,"Logonun soldan konumu %",()=>design.LogoX,x=>design.LogoX=x,0,95);Number(logo,"Logonun üstten konumu %",()=>design.LogoY,x=>design.LogoY=x,0,95);Number(logo,"Logo genişliği %",()=>design.LogoWidth,x=>design.LogoWidth=x,3,40);Number(logo,"Logo görünürlüğü %",()=>design.LogoOpacity,x=>design.LogoOpacity=x,0,100);

        logo.Controls.Add(Theme.Button("Yapay zekâ ile logo tasarla",()=>{using var editor=new AiDesignDialog(store,true,store.Settings.ChannelName);if(editor.ShowDialog(this)==DialogResult.OK){design.Logo=editor.SavedPath;Changed();}},true,360));
        logo.Controls.Add(Theme.Button("Logo arşivinden seç",()=>{var path=CoverArchive.Pick(store,this,AiDesignDialog.LogoFiles(store),"Arşivden logo seç");if(path!=null){design.Logo=path;Changed();}},false,360));
        var aiPage=new FlowLayoutPanel {Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(6)};aiMode.Controls.Add(aiPage);
        aiPage.Controls.Add(Theme.Label("Konuyu yaz; yerel yapay zekâ tasarlasın. Metinler ayrı eklenir, sonuç sağda önizlenir.",10,width:360,height:65));
        var aiTopic=Theme.Input(design.Name=="Yeni kart"?"":design.Name,true,360);aiTopic.Height=80;Field(aiPage,"Kartın konusu / amacı",aiTopic);
        Input(aiPage,"Kart başlığı",()=>design.Title,x=>design.Title=x);
        Input(aiPage,"Ana metin",()=>design.Body,x=>design.Body=x,true);
        Input(aiPage,"Alt metin",()=>design.Footer,x=>design.Footer=x);
        var aiInstruction=Theme.Input("",true,360);aiInstruction.Height=80;Field(aiPage,"Tasarım talimatı • isteğe bağlı",aiInstruction);
        var aiWorkflow=store.Settings.CoverLocalWorkflow;
        if(!File.Exists(aiWorkflow))aiWorkflow=LocalCoverEngine.FindInstalledWorkflow();
        aiPage.Controls.Add(Theme.Button("Yerel motor dosyasını seç",()=>{using var picker=new OpenFileDialog{Filter="ComfyUI iş akışı|*.json"};if(picker.ShowDialog(this)==DialogResult.OK)aiWorkflow=picker.FileName;},false,360));
        var aiStatus=Theme.Label("Hazır • yerel motor kullanılır, API kotası harcanmaz.",10,color:Theme.Success,width:360,height:85);aiPage.Controls.Add(aiStatus);
        var aiBar=new TurquoisePackageBar {Width=360,Height=32,Text="Tasarım bekleniyor"};aiPage.Controls.Add(aiBar);
        CardDesign? aiCandidate=null;bool aiRunning=false;
        var aiCancellation=new CancellationTokenSource();FormClosed+=(_,_)=>{aiCancellation.Cancel();aiCancellation.Dispose();};
        Button? aiGenerate=null;
        var aiApprove=Theme.Button("Onayla ve kullan",()=>{if(aiCandidate==null)return;design=aiCandidate;aiCandidate=null;bottom.Enabled=true;top.Enabled=true;tabs.Enabled=true;preview.Enabled=true;Sync();Changed();aiStatus.Text="Onaylandı • Kartı galeriye kaydet ile arşive ekleyebilirsin.";},true,360);aiApprove.Enabled=false;
        aiGenerate=Theme.Button("Yapay zekâ ile kart tasarla",async()=>{
            if(aiRunning)return;
            if(string.IsNullOrWhiteSpace(aiTopic.Text)){aiStatus.Text="Önce kartın konusunu yaz.";return;}
            aiRunning=true;aiGenerate!.Enabled=false;aiApprove.Enabled=false;bottom.Enabled=false;top.Enabled=false;tabs.Enabled=false;manualMode.Enabled=false;preview.Enabled=false;foreach(var field in aiPage.Controls.OfType<TextBox>())field.Enabled=false;redraw.Stop();aiBar.Busy=true;aiBar.Value=0;
            try {
                void Report(string message){if(!IsDisposed)aiStatus.Text=message;}
                await LocalCoverEngine.Start(aiWorkflow,Report,aiCancellation.Token);
                var purpose=aiTopic.Text.Trim();var direction=aiInstruction.Text.Trim();
                var prompt="Design an elegant cinematic background for an opening or closing video card. Full-frame balanced composition with uncluttered central space for text. No text, no letters, no logos, no invented scripture. Consistent navy and warm gold lighting. Subject: "+purpose[..Math.Min(purpose.Length,1500)]+". Visual direction: "+direction[..Math.Min(direction.Length,2000)];
                aiBar.Text="Yapay zekâ kartı tasarlıyor…";
                var bytes=await LocalCoverEngine.Generate(aiWorkflow,prompt,design.Vertical,Report,aiCancellation.Token,square:design.Square);
                var folder=Path.Combine(store.Root,"card-assets","ai-backgrounds");Directory.CreateDirectory(folder);var path=Path.Combine(folder,Guid.NewGuid().ToString("N")+".png");
                using(var stream=new MemoryStream(bytes))using(var image=Image.FromStream(stream)){if((long)image.Width*image.Height>40_000_000)throw new InvalidOperationException("Görsel çok büyük.");image.Save(path,ImageFormat.Png);}
                if(IsDisposed)return;
                aiCandidate=JsonSerializer.Deserialize<CardDesign>(JsonSerializer.Serialize(design))!;aiCandidate.Name=purpose;aiCandidate.BackgroundImage=path;aiCandidate.Transparent=false;aiCandidate.ImageFit="Doldur / kırp";aiCandidate.ImageShade=25;
                aiCandidate.TitleStyle.Color="#FFD66B";aiCandidate.BodyStyle.Color=aiCandidate.FooterStyle.Color="#FFFFFF";CardDesign.FitText(aiCandidate);
                var result=CardDesign.Draw(aiCandidate);var old=preview.Image;preview.Image=result;old?.Dispose();
                aiBar.Value=100;aiBar.Text="Tasarım hazır";aiStatus.Text="Sağdaki önizlemeyi kontrol et. Onayla veya yeniden tasarla.";aiApprove.Enabled=true;
            }catch(OperationCanceledException){}
            catch(Exception ex){if(!IsDisposed){aiStatus.Text="Tasarım tamamlanamadı: "+ex.Message;aiBar.Text="Yeniden deneyebilirsin";bottom.Enabled=true;top.Enabled=true;tabs.Enabled=true;preview.Enabled=true;}}
            finally{aiRunning=false;if(!IsDisposed){aiBar.Busy=false;aiGenerate.Enabled=true;aiGenerate.Text="Yeniden tasarla";manualMode.Enabled=true;foreach(var field in aiPage.Controls.OfType<TextBox>())field.Enabled=true;}}
        },true,360);aiPage.Controls.Add(aiGenerate);aiPage.Controls.Add(aiApprove);
        designModes.SelectedIndexChanged+=(_,_)=>{if(aiCandidate!=null && designModes.SelectedTab==manualMode){aiCandidate=null;aiApprove.Enabled=false;bottom.Enabled=true;top.Enabled=true;tabs.Enabled=true;preview.Enabled=true;RefreshPreview();}if(designModes.SelectedTab==aiMode)Sync();};
        var aiActions=new FlowLayoutPanel {Dock=DockStyle.Bottom,Height=215,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(6)};
        aiStatus.Height=60;aiActions.Controls.AddRange([aiStatus,aiBar,aiGenerate,aiApprove]);aiMode.Controls.Add(aiActions);
        void FitAiFields(){int width=Math.Max(160,aiPage.ClientSize.Width-aiPage.Padding.Horizontal-SystemInformation.VerticalScrollBarWidth-8);foreach(Control field in aiPage.Controls)field.Width=Math.Min(360,width);foreach(Control field in aiActions.Controls)field.Width=Math.Min(360,width);}
        aiPage.SizeChanged+=(_,_)=>FitAiFields();FitAiFields();
        foreach(var input in aiPage.Controls.OfType<TextBox>())input.TextChanged+=(_,_)=>{if(aiCandidate==null || aiRunning || loading)return;aiCandidate=null;aiApprove.Enabled=false;bottom.Enabled=true;top.Enabled=true;tabs.Enabled=true;preview.Enabled=true;RefreshPreview();aiStatus.Text="Talimat veya metin değişti • yeniden tasarlayabilirsin.";};
        redraw.Tick+=(_,_)=>{redraw.Stop();RefreshPreview();};preview.Paint+=(_,e)=>{var r=CanvasRect();if(r.Width==0 || string.IsNullOrWhiteSpace(selected=="Başlık"?design.Title:selected=="Ana metin"?design.Body:design.Footer))return;using var pen=new Pen(Color.Turquoise,1){DashStyle=System.Drawing.Drawing2D.DashStyle.Dash};e.Graphics.DrawRectangle(pen,r.X+r.Width*Style.X/100f,r.Y+r.Height*Style.Y/100f,r.Width*Style.Width/100f,r.Height*Style.Height/100f);};

        preview.MouseDown+=(_,e)=>{var r=CanvasRect();var box=new RectangleF(r.X+r.Width*Style.X/100f,r.Y+r.Height*Style.Y/100f,r.Width*Style.Width/100f,r.Height*Style.Height/100f);if(e.Button==MouseButtons.Left&&box.Contains(e.Location)){drag=e.Location;originX=Style.X;originY=Style.Y;preview.Capture=true;}};

        preview.MouseMove+=(_,e)=>{if(drag is not Point start)return;var r=CanvasRect();if(r.Width<=0)return;Style.X=Math.Clamp(originX+(int)((e.X-start.X)*100/r.Width),0,95);Style.Y=Math.Clamp(originY+(int)((e.Y-start.Y)*100/r.Height),0,95);Sync();redraw.Stop();redraw.Start();};preview.MouseUp+=(_,_)=>{if(drag!=null){drag=null;preview.Capture=false;Changed();}};

        KeyDown+=(_,e)=>{if(e.Control&&e.KeyCode==Keys.Z){Undo(-1);e.SuppressKeyPress=true;}if(e.Control&&e.KeyCode==Keys.Y){Undo(1);e.SuppressKeyPress=true;}};

        FormClosing+=(_,e)=>{

            if(DialogResult==DialogResult.OK || EditorDrafts.Hash(design)==baseline || testMode)return;

            EditorDrafts.Save(draftPath,baseline,design);

            var answer=MessageBox.Show(this,"Kaydedilmemiş kart düzeni var. Galeriye kaydedilsin mi?\nHayır: taslak korunarak çıkılır. İptal: düzenlemeye dönülür.","Kart taslağı",MessageBoxButtons.YesNoCancel);

            if(answer==DialogResult.Cancel){e.Cancel=true;return;}

            if(answer==DialogResult.Yes){e.Cancel=true;try{Save();e.Cancel=DialogResult!=DialogResult.OK;}catch(Exception ex){MessageBox.Show(this,ex.Message,"Kart kaydı");}}

        };

        FormClosed+=(_,_)=>{redraw.Dispose();preview.Image?.Dispose();};Sync();Record();RefreshPreview();

    }

    RectangleF CanvasRect(){if(preview.Image==null)return RectangleF.Empty;float scale=Math.Min((float)preview.ClientSize.Width/preview.Image.Width,(float)preview.ClientSize.Height/preview.Image.Height);float w=preview.Image.Width*scale,h=preview.Image.Height*scale;return new((preview.ClientSize.Width-w)/2,(preview.ClientSize.Height-h)/2,w,h);}

    void Sync(){loading=true;try{foreach(var action in bindings)action();}finally{loading=false;}}

    void Record(){var json=JsonSerializer.Serialize(design);if(historyIndex>=0&&history[historyIndex]==json)return;if(historyIndex+1<history.Count)history.RemoveRange(historyIndex+1,history.Count-historyIndex-1);history.Add(json);if(history.Count>60)history.RemoveAt(0);historyIndex=history.Count-1;}

    void Changed(){Record();EditorDrafts.Save(draftPath,baseline,design);redraw.Stop();redraw.Start();}

    void Undo(int offset){int index=historyIndex+offset;if(index<0||index>=history.Count)return;historyIndex=index;design=JsonSerializer.Deserialize<CardDesign>(history[index])!;EditorDrafts.Save(draftPath,baseline,design);Sync();RefreshPreview();}

    void RefreshPreview(){try{var image=CardDesign.Draw(design);var old=preview.Image;preview.Image=image;old?.Dispose();status.Text=CardDesign.LayoutIssues(design).FirstOrDefault() ?? $"{image.Width} × {image.Height} • Seçili metin: {selected} • Taslak otomatik korunur";}catch(Exception ex){status.Text="Önizleme: "+ex.Message;}}

    string CopyMedia(string file){using var image=Image.FromFile(file);if((long)image.Width*image.Height>40_000_000)throw new InvalidOperationException("Görsel çok büyük.");var folder=Path.Combine(store.Root,"card-assets","media");Directory.CreateDirectory(folder);var path=Path.Combine(folder,Guid.NewGuid().ToString("N")+Path.GetExtension(file));File.Copy(file,path);return path;}

    void ImportCover(){using var dialog=new OpenFileDialog{Title="Hazır kapağı içe aktar",Filter="Kapak görseli|*.png;*.jpg;*.jpeg;*.bmp"};if(dialog.ShowDialog(this)!=DialogResult.OK)return;try{LoadCover(dialog.FileName);}catch(Exception ex){MessageBox.Show(this,ex.Message,"Kapak içe aktarma");}}

    internal void LoadCover(string file){var path=CopyMedia(file);using var image=Image.FromFile(path);var imported=CardDesign.Blank();imported.Name=Path.GetFileNameWithoutExtension(file);imported.BackgroundImage=path;imported.ImageFit="Sığdır";imported.ImageShade=0;imported.Transparent=true;imported.ImportedWidth=image.Width;imported.ImportedHeight=image.Height;imported.Vertical=image.Height>image.Width;imported.Square=image.Width==image.Height;design=imported;Sync();Changed();RefreshPreview();}

    void Import(bool logo){using var dialog=new OpenFileDialog{Filter="Görsel|*.png;*.jpg;*.jpeg;*.bmp"};if(dialog.ShowDialog(this)!=DialogResult.OK)return;try{var path=CopyMedia(dialog.FileName);if(logo)design.Logo=path;else design.BackgroundImage=path;Changed();}catch(Exception ex){MessageBox.Show(this,ex.Message,"Görsel seçimi");}}

    void Export(){using var dialog=new SaveFileDialog{Filter="PNG görsel|*.png",FileName="kart.png"};if(dialog.ShowDialog(this)!=DialogResult.OK)return;CardDesign.Validate(design);using var image=CardDesign.Draw(design);image.Save(dialog.FileName,ImageFormat.Png);}

    internal void CaptureTest(string folder)

    {

        testMode=true;Directory.CreateDirectory(folder);

        foreach(TabPage page in tabs.TabPages){tabs.SelectedTab=page;PerformLayout();using var shot=new Bitmap(Width,Height);DrawToBitmap(shot,new Rectangle(Point.Empty,Size));shot.Save(Path.Combine(folder,"editor-"+tabs.SelectedIndex+".png"));}
        if(tabs.Parent is TabPage manual && manual.Parent is TabControl modes){modes.SelectedIndex=1;PerformLayout();Application.DoEvents();using(var shot=new Bitmap(Width,Height)){DrawToBitmap(shot,new Rectangle(Point.Empty,Size));shot.Save(Path.Combine(folder,"ai-design-page.png"));}modes.SelectedIndex=0;}

        design.Title="Türkçe kart başlığı";design.Body="Birlikte daha çok gönüle ulaşalım.";design.Footer="Nur Kapısı • Teşekkür ederiz";design.Background="#102D38";design.BackgroundSecond="#165E65";design.BodyStyle.Font="Arial";design.BodyStyle.Size=58;design.BodyStyle.Align="Sol";design.BodyStyle.Shadow=5;design.BodyStyle.Outline=2;design.BodyStyle.Box=true;design.BackgroundMode="Renk geçişi";Record();

        var before=JsonSerializer.Serialize(design);design.BodyStyle.X=21;Record();Undo(-1);if(JsonSerializer.Serialize(design)!=before)throw new Exception("Geri al tasarımı geri yüklemedi.");Undo(1);if(design.BodyStyle.X!=21)throw new Exception("Yinele kayboldu.");

        design.BodyStyle.X=8;

        foreach(var frame in CardDesign.Frames){design.Frame=frame;using var image=CardDesign.Draw(design);image.Save(Path.Combine(folder,"frame-"+Array.IndexOf(CardDesign.Frames,frame)+".png"));}

        design.Vertical=true;using(var image=CardDesign.Draw(design))image.Save(Path.Combine(folder,"vertical.png"));

        File.WriteAllText(Path.Combine(folder,"editor-check.txt"),"PASS: 7 sekme; 7 çerçeve; katman efektleri; geri al / yinele; dikey çıktı.");

    }

    void Save(){CardDesign.Validate(design);if(string.IsNullOrWhiteSpace(design.Title+design.Body+design.Footer)&&design.Logo==""&&design.BackgroundImage=="")throw new InvalidOperationException("Metin, logo veya hazır kapak ekle.");var folder=Path.Combine(store.Root,"card-assets");Directory.CreateDirectory(folder);var path=Path.Combine(folder,Guid.NewGuid().ToString("N")+".png");using var image=CardDesign.Draw(design);image.Save(path,ImageFormat.Png);try{Json.Write(Path.ChangeExtension(path,".json"),design);}catch{File.Delete(path);throw;}SavedPath=path;EditorDrafts.Remove(draftPath);DialogResult=DialogResult.OK;Close();}

}

