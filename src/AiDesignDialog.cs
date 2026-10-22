using System.Drawing.Imaging;

namespace AnkaStudio;

internal sealed class AiDesignDialog : Form
{
    internal static string SelectedWorkflow(string model,string current)=>File.Exists(current)&&LocalCoverEngine.ModelOf(current)==model?current:LocalCoverEngine.ResolveWorkflow(model,current);
    internal static string EngineQuality(string label)=>LocalImageProfiles.Label(label);
    internal static string QualityLabel(string? quality)=>LocalImageProfiles.Label(quality);
    public string SavedPath {get;private set;}="";
    public static string LogoFolder(Store store)=>Path.Combine(store.Root,"logo-library");
    public static string[] LogoFiles(Store store)=>Directory.Exists(LogoFolder(store))?Directory.EnumerateFiles(LogoFolder(store)).Where(f=>new[]{".png",".jpg",".jpeg",".bmp"}.Contains(Path.GetExtension(f).ToLowerInvariant())).ToArray():[];
    public AiDesignDialog(Store store,bool logo,string initialTopic="",bool vertical=false,Settings? coverSettings=null,string coverTitle="")
    {
        Text="Aİ Resim Stüdyosu";Width=1080;Height=760;MinimumSize=new(900,650);BackColor=Theme.Bg;ForeColor=Theme.Text;StartPosition=FormStartPosition.CenterParent;
        var leftFrame=new Panel{Dock=DockStyle.Left,Width=390};
        var left=new FlowLayoutPanel {Dock=DockStyle.Fill,Width=390,Padding=new Padding(16),AutoScroll=true,WrapContents=false,FlowDirection=FlowDirection.TopDown};
        var right=new Panel {Dock=DockStyle.Fill,Padding=new Padding(12)};
        var picture=new PictureBox {Dock=DockStyle.Fill,SizeMode=PictureBoxSizeMode.Zoom,BackColor=Theme.Panel};right.Controls.Add(picture);Controls.Add(right);Controls.Add(leftFrame);leftFrame.Controls.Add(left);
        var commands=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=122,Padding=new Padding(16,8,16,8),FlowDirection=FlowDirection.TopDown,WrapContents=false};leftFrame.Controls.Add(commands);commands.BringToFront();
        left.Controls.Add(Theme.Label(logo?"LOGO TASARLA":"KAPAK TASARLA",16,true,width:350,height:40));
        left.Controls.Add(Theme.Label(logo?"Kanalın adı ve istediğin sembolü yaz.":"Kapakta görmek istediğin konuyu yaz.",10,width:350,height:45));
        var topic=Theme.Input(initialTopic,true,350);topic.Height=80;left.Controls.Add(topic);
        left.Controls.Add(Theme.Label("Tasarım talimatı • isteğe bağlı",10,width:350,height:28));
        var instruction=Theme.Input("",true,350);instruction.Height=100;left.Controls.Add(instruction);
        var preferences=coverSettings??store.Settings;
        left.Controls.Add(Theme.Label("Hazır motor",10,true,width:350,height:28));var model=Theme.Combo(LocalCoverEngine.Models.Where(x=>x!="Otomatik").ToArray(),LocalCoverEngine.Models.Contains(preferences.CoverLocalModel??"")?preferences.CoverLocalModel!:LocalCoverEngine.ModelOf(preferences.CoverLocalWorkflow),350);model.AccessibleName="Kapak hazır motoru";left.Controls.Add(model);
        var quality=Theme.Combo(new[]{"Otomatik","Hafif","Orta","Yüksek"},preferences.CoverLocalQuality??"Otomatik",350);quality.AccessibleName="Kapak üretim kalitesi";quality.Visible=true;left.Controls.Add(quality);
        left.Controls.Add(Theme.Label("Çözünürlük • piksel",10,true,width:350,height:28));
        string[] dimensions=logo?["768x768","1024x1024","1536x1536"]:vertical?["576x1024","720x1280","864x1536"]:["1024x576","1280x720","1536x864"];
        var resolution=Theme.Combo(new[]{"Otomatik • ekran kartına göre"}.Concat(dimensions.Select(x=>x.Replace("x"," × ")+" piksel")).ToArray(),"",350);resolution.AccessibleName="Yerel görsel çözünürlüğü";int savedResolution=Array.IndexOf(dimensions,preferences.CoverLocalResolution);resolution.SelectedIndex=savedResolution<0?0:savedResolution+1;left.Controls.Add(resolution);
        var recommendation=Theme.Label("Otomatik cihaz ayarı: motor denetleniyor…",10,true,Theme.Success,width:350,height:48);recommendation.AccessibleName="Cihaza önerilen görsel profili";left.Controls.Add(recommendation);
        var qualityInfo=Theme.Label("",9,width:350,height:60);left.Controls.Add(qualityInfo);
        var workflow=Theme.Input(File.Exists(preferences.CoverLocalWorkflow)?preferences.CoverLocalWorkflow:LocalCoverEngine.FindInstalledWorkflow(),width:320);
        var details=new StudioDisclosure("Gelişmiş • motor dosyası",350);details.Content.Controls.Add(workflow);details.Content.Controls.Add(Theme.Button("Motor dosyasını seç",()=>{using var picker=new OpenFileDialog {Filter="ComfyUI iş akışı|*.json"};if(picker.ShowDialog(this)==DialogResult.OK)workflow.Text=picker.FileName;},false,320));left.Controls.Add(details);
        void RefreshEngine(){workflow.Text=SelectedWorkflow(model.Text,workflow.Text.Trim());qualityInfo.Text=LocalCoverEngine.Describe(EngineQuality(quality.Text),model.Text);qualityInfo.Text=(quality.Text=="Otomatik"?"Kalite cihazdan otomatik belirlenir. ":"Seçilen kalite: "+quality.Text+". ")+model.Text+" • "+(resolution.SelectedIndex==0?"Piksel boyutu boş ekran kartı belleğine göre ayarlanır.":resolution.Text)+" • "+(logo?"Kare logo":vertical?"Dikey kapak":"Yatay kapak");}model.SelectedIndexChanged+=(_,_)=>RefreshEngine();quality.SelectedIndexChanged+=(_,_)=>RefreshEngine();resolution.SelectedIndexChanged+=(_,_)=>RefreshEngine();RefreshEngine();
        var status=Theme.Label("Hazır • yerel motor kullanılır.",10,color:Theme.Success,width:350,height:90);left.Controls.Add(status);
        var bar=new TurquoisePackageBar {Width=350,Height=32,Text="Tasarım bekleniyor"};left.Controls.Add(bar);
        var cancellation=new CancellationTokenSource();FormClosed+=(_,_)=>{cancellation.Cancel();cancellation.Dispose();picture.Image?.Dispose();};
        Shown+=async(_,_)=>{try{string recommended=await LocalCoverEngine.RecommendedProfile(cancellation.Token);if(!IsDisposed)recommendation.Text="Otomatik cihaz ayarı: "+recommended;}catch(OperationCanceledException){}};
        string candidate="";bool running=false;
        var approve=Theme.Button("Onayla ve arşive kaydet",()=>{
            if(candidate==""||!File.Exists(candidate))return;
            try{var folder=logo?LogoFolder(store):CoverArchive.Folder(store);Directory.CreateDirectory(folder);SavedPath=Path.Combine(folder,Guid.NewGuid().ToString("N")+".png");File.Copy(candidate,SavedPath);DialogResult=DialogResult.OK;Close();}
            catch(Exception ex){status.Text="Arşive kaydedilemedi: "+ex.Message;}
        },true,350);approve.Enabled=false;
        Button? generate=null;
        generate=Theme.Button("Tasarla",async()=>{
            if(running)return;if(string.IsNullOrWhiteSpace(topic.Text)){status.Text="Önce konu veya kanal adını yaz.";return;}
            running=true;generate!.Enabled=false;approve.Enabled=false;topic.Enabled=instruction.Enabled=details.Enabled=model.Enabled=quality.Enabled=resolution.Enabled=false;bar.Busy=true;bar.Value=0;
            try{
                void Report(string message){if(!IsDisposed)status.Text=message;}
                string selectedWorkflow=SelectedWorkflow(model.Text,workflow.Text.Trim()),selectedQuality=quality.Text;workflow.Text=selectedWorkflow;LocalCoverEngine.Prepare(selectedWorkflow,"test",vertical,square:logo,quality:selectedQuality);
                preferences.CoverLocalResolution=resolution.SelectedIndex<=0?"auto":dimensions[resolution.SelectedIndex-1];preferences.CoverLocalModel=model.Text;preferences.CoverLocalQuality=selectedQuality;preferences.CoverLocalWorkflow=selectedWorkflow;preferences.CoverLocalEnabled=true;if(coverSettings==null)store.Save();
                await LocalCoverEngine.Start(selectedWorkflow,Report,cancellation.Token);await LocalCoverEngine.Check(selectedWorkflow,cancellation.Token);
                string subject=topic.Text.Trim(),direction=instruction.Text.Trim();
                string prompt=subject[..Math.Min(subject.Length,1500)]+". "+direction[..Math.Min(direction.Length,2000)]+(logo?". Professional centered logo symbol, simple silhouette, isolated background, no letters or watermark.":". Cinematic thumbnail background, clear subject, title space on left, no letters, no watermark, no invented scripture.");
                bar.Text="Tasarım üretiliyor…";
                var bytes=await LocalCoverEngine.Generate(selectedWorkflow,prompt,vertical,Report,cancellation.Token,square:logo,quality:selectedQuality,resolution:preferences.CoverLocalResolution);
                var cache=Path.Combine(store.Root,"cache","ai-design-drafts");Directory.CreateDirectory(cache);candidate=Path.Combine(cache,Guid.NewGuid().ToString("N")+".png");
                using var stream=new MemoryStream(bytes);using var image=Image.FromStream(stream);if((long)image.Width*image.Height>40_000_000)throw new InvalidOperationException("Görsel çok büyük.");image.Save(candidate,ImageFormat.Png);
                if(!logo && coverSettings!=null){var copy=PackageArtwork.Snapshot(coverSettings);copy.CoverMode="Şablon";copy.CoverBackground=candidate;copy.CoverPng="";var complete=candidate+".cover.png";CoverDesigner.Save(complete,copy,coverTitle,vertical);candidate=complete;}
                if(IsDisposed)return;using var completed=Image.FromFile(candidate);var old=picture.Image;picture.Image=new Bitmap(completed);old?.Dispose();bar.Value=100;bar.Text="Tasarım hazır";status.Text="Önizlemeyi kontrol et; onayla veya yeniden tasarla.";approve.Enabled=true;
            }catch(OperationCanceledException){}
            catch(Exception ex){if(!IsDisposed){status.Text="Tasarım tamamlanamadı: "+ex.Message;bar.Text="Yeniden deneyebilirsin";}}
            finally{running=false;if(!IsDisposed){bar.Busy=false;generate.Enabled=true;generate.Text="Yeniden tasarla";topic.Enabled=instruction.Enabled=details.Enabled=model.Enabled=quality.Enabled=resolution.Enabled=true;}}
        },true,350);commands.Controls.Add(generate);commands.Controls.Add(approve);
        left.Controls.Add(Theme.Label(logo?"Onaylanan logolar ayrı logo arşivine kaydedilir. Üretilen PNG'nin arka planı olabilir.":"Onaylanan görseller ayrı kapak arşivine kaydedilir. Başlık, proje tasarımından açıldığında otomatik eklenir.",9,width:350,height:70));
    }
}
