using System.Net;

using System.Reflection;

using System.Text;

using System.Text.Json;



namespace AnkaStudio;



public static class ReliabilityTests

{
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    struct ComboInfo { public int Size; public NativeRect Item, Button; public int State; public IntPtr Combo, Edit, List; }
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    struct NativeRect { public int Left, Top, Right, Bottom; }
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool GetComboBoxInfo(IntPtr combo,ref ComboInfo info);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window,out NativeRect rect);

    [System.Runtime.InteropServices.DllImport("user32.dll",EntryPoint="SendMessageW")] static extern IntPtr SendHelpMessage(IntPtr hwnd,int msg,IntPtr wparam,IntPtr lparam);

    public static int Run(string[] args)

    {

        WindowsFormsSynchronizationContext.AutoInstall = false;
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);

        var root = Path.GetFullPath(Program.Arg(args, "--data") ?? throw new InvalidOperationException("Test veri klasörü gerekli."));

        if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any() && !File.Exists(Path.Combine(root, ".anka-reliability-test-root"))) throw new InvalidOperationException("Testler gerçek veri klasöründe çalıştırılamaz. Boş, ayrı test klasörü seç.");

        Directory.CreateDirectory(root); File.WriteAllText(Path.Combine(root, ".anka-reliability-test-root"), "isolated test data");

        var ffmpeg = Program.Arg(args, "--ffmpeg") ?? Path.Combine(AppContext.BaseDirectory, "tools", "ffmpeg.exe");

        var rows = new List<object>(); int failed = 0;

        Store New(string name) { var st = new Store(Path.Combine(root, name)); st.Settings.FfmpegPath = ffmpeg; st.Settings.LocalAiFirst = false; st.Settings.CloudAiEnabled = true; st.Settings.EnabledCloudProviders = "Gemini,Groq,OpenRouter,OpenAI,Kimi"; return st; }

        void Check(bool value, string message) { if (!value) throw new Exception(message); }

        void Reject(Action action) { bool rejected = false; try { action(); } catch (InvalidOperationException) { rejected = true; } Check(rejected, "İşlem reddedilmedi."); }

        void Test(string id, string name, Action action) { if(Program.Arg(args,"--only-test") is string only && id!=only)return; try { action(); rows.Add(new { Id = id, Name = name, Outcome = "PASS" }); } catch (Exception ex) { failed++; rows.Add(new { Id = id, Name = name, Outcome = "FAIL", Error = ex.ToString() }); } Json.Write(Path.Combine(root, "reliability-test-report.json"), new { Passed = rows.Count - failed, Failed = failed, Tests = rows }); }

        Test("BATCH-0432-SETTINGS", "Merkez alt menüleri dar ve geniş pencerelerde kırpılmaz",()=>{
            using var form=new StudioForm(New("batch-settings"));form.AuditSettingsLayout(Path.Combine(root,"settings-layout"));form.Close();
        });
        Test("BATCH-0432-VOICE", "Ses satırlarında dinle/kaydet; geçiş ve durdurma bekleyen üretimi iptal eder",()=>{
            var st=New("batch-voice");using var form=new Form{Width=800,Height=500};var tokens=new List<CancellationToken>();int saved=0;
            using var list=new VoiceSelectionList(st,st.Settings,["tr-TR-Test-A","tr-TR-Test-B"],_=>saved++,()=>false,(_,ct)=>{tokens.Add(ct);return Task.Delay(Timeout.Infinite,ct).ContinueWith<string>(_=>throw new OperationCanceledException(ct),ct);}){Dock=DockStyle.Fill};form.Controls.Add(list);form.Show();Application.DoEvents();
            IEnumerable<Control> All(Control c)=>c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(All(x)));
            var buttons=All(list).OfType<Button>().Where(x=>x.Visible).ToArray();Check(buttons.Length==4&&buttons.Count(x=>x.Text=="Kaydet")==2,"Ses satırları tek yerde değil");
            var a=buttons.Single(x=>x.AccessibleName=="tr-TR-Test-A sesini dinle veya durdur");var b=buttons.Single(x=>x.AccessibleName=="tr-TR-Test-B sesini dinle veya durdur");a.PerformClick();Application.DoEvents();Check(a.Text=="■ Durdur"&&tokens.Count==1,"Hazırlanan ses durdurulamıyor");b.PerformClick();Application.DoEvents();Check(tokens[0].IsCancellationRequested&&a.Text=="▶ Dinle"&&b.Text=="■ Durdur","Önceki ses iptal edilmedi");b.PerformClick();Check(tokens[1].IsCancellationRequested&&b.Text=="▶ Dinle","Durdur düğmesi çalışmadı");buttons.First(x=>x.Text=="Kaydet").PerformClick();Check(saved==1,"Satır seçimi kaydedilmedi");form.Close();
        });
        Test("BATCH-0432-ARCHIVE", "Taranmamış müzik hem sayaca hem doluluk rengine ilk açılışta yansır",()=>{
            var st=New("batch-archive");var library=new AudioLibrary(st);Directory.CreateDirectory(library.Folder);File.WriteAllText(Path.Combine(library.Folder,"new-music.mp3"),"inventory fixture");
            using var form=new StudioForm(st);form.Show();Application.DoEvents();typeof(StudioForm).GetMethod("Navigate",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(form,["Medya arşivleri"]);Application.DoEvents();IEnumerable<Control> All(Control c)=>c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(All(x)));
            var music=All(form).OfType<StudioButton>().Single(x=>x.Text=="Müzik");Check(music.AccessibleDescription=="1 kayıt"&&music.GlassAccent==StudioGlass.Purple,"Müzik sayacı veya doluluk rengi eski");Check(All(form).OfType<Button>().Any(x=>x.Text=="Müzik • 1 dosya"),"Arşiv kartı başka sayı gösteriyor");form.Close();
        });
        Test("BATCH-0432-RECOVERY", "Motor yalnız bir kez yeniden denenir; kullanıcı iptali yeniden başlatmaz",()=>{
            int attempts=0,restarts=0;var retry=new LocalVideoRecovery();bool failed=false;
            try{retry.Run(_=>{attempts++;throw new InvalidOperationException("local inference failed");},()=>{restarts++;return Task.CompletedTask;},CancellationToken.None).GetAwaiter().GetResult();}catch(InvalidOperationException){failed=true;}
            Check(failed&&attempts==2&&restarts==1,"Tek yeniden deneme sınırı yok");using var stop=new CancellationTokenSource();stop.Cancel();attempts=restarts=0;try{new LocalVideoRecovery().Run(_=>Task.FromCanceled(stop.Token),()=>{restarts++;return Task.CompletedTask;},stop.Token).GetAwaiter().GetResult();}catch(OperationCanceledException){}Check(restarts==0,"Kullanıcının durdurması yok sayıldı");
        });
        Test("BATCH-0432-CORTEX", "Cortex yerel seçeneği üretim sırasını değiştirmez; bulut tercihi korunur",()=>{
            var settings=new Settings{UseOllama=true,LocalAiFirst=true,ProviderOrder="NVIDIA,Gemini"};Check(CortexProvider.Preferred(settings)=="Ollama","Cortex hâlâ NVIDIA kullanıyor");Check(settings.ProviderOrder=="NVIDIA,Gemini","Üretim sırası değişti");settings.UseOllama=false;Check(CortexProvider.Preferred(settings)==null,"Yerel devre dışı tercihi korunmadı");
        });
        Test("BATCH-0432-VIDEO-QUALITY", "Manuel LTX kalitesi çözünürlüğü ve önbelleği değiştirir",()=>{
            int prior=0;foreach(string quality in LocalImageProfiles.Labels){var graph=LocalLtxVideo.Graph("m","t","r.png","The same fountain water flows naturally.",5,false,12,quality:quality);int width=graph["reference"]!["inputs"]!["width"]!.GetValue<int>();Check(width>prior,"Video kalite seçimi uygulanmadı");prior=width;}var scene=new Scene();Check(SceneMotion.Signature(scene,"",5,false,"Hafif")!=SceneMotion.Signature(scene,"",5,false,"Orta"),"Video önbelleği kaliteden bağımsız");
        });
        Test("BATCH-0432-VLC", "Gömülü VLC yerel videoyu oynatır, duraklatır, konuma gider ve temiz kapanır",()=>{
            var st=New("batch-vlc");string video=Path.Combine(st.Root,"sample.mp4");Media.Exec(st.Ffmpeg,["-y","-f","lavfi","-i","color=c=blue:s=160x90:r=24","-t","3","-c:v","libx264","-preset","ultrafast",video],st.Root,CancellationToken.None).GetAwaiter().GetResult();using var form=new Form{Width=600,Height=400};using var player=new WorkflowPlayer{Dock=DockStyle.Fill};form.Controls.Add(player);form.Show();Application.DoEvents();player.Open(video);player.Volume=0;player.Play();var deadline=DateTime.UtcNow.AddSeconds(8);while(!player.IsPlaying&&DateTime.UtcNow<deadline){Application.DoEvents();Thread.Sleep(50);}Check(player.IsPlaying,"VLC oynatmaya geçmedi");player.Pause();player.Seek(1);Check(player.Volume==0,"Ses kontrolü uygulanmadı");form.Close();
        });

        Test("COMFY-DESKTOP-ENGINE", "Comfy Desktop venv/standalone ve taşınabilir motor aynı keşif mekanizmasını kullanır",()=>{
            string directory=Path.Combine(root,"desktop-engine");string python=Path.Combine(directory,"ComfyUI",".venv","Scripts","python.exe");Directory.CreateDirectory(Path.GetDirectoryName(python)!);File.WriteAllText(python,"");File.WriteAllText(Path.Combine(directory,"ComfyUI","main.py"),"");
            Check(EngineHardware.Python(directory)==python&&LocalCoverEngine.FindEngine(directory)==directory,"Desktop motoru bulunamadı");
            File.Delete(python);string standalone=Path.Combine(directory,"standalone-env","python.exe");Directory.CreateDirectory(Path.GetDirectoryName(standalone)!);File.WriteAllText(standalone,"");Check(EngineHardware.Python(directory)==standalone,"Bağımsız Desktop Python ortamı bulunamadı");
        });
        Test("LTX-GRAPH", "LTX görsel referansı, düşük çözünürlük, 5–6 saniye ve sessiz video çıkışı doğru bağlanır",()=>{
            foreach(int seconds in new[]{5,6})foreach(bool vertical in new[]{true,false}){
                var graph=LocalLtxVideo.Graph("ltx-model","t5","ref.png","Water flows naturally in front of the seated man.",seconds,vertical,12);
                Check(graph["reference"]!["inputs"]!["length"]!.GetValue<int>()==seconds*24+1&&graph["reference"]!["inputs"]!["image"]![0]!.GetValue<string>()=="image","Süre veya referans kopuk");
                Check(graph["text"]!["inputs"]!["device"]!.GetValue<string>()=="cpu"&&graph["sample"]!["inputs"]!["cfg"]!.GetValue<double>()==1,"Distilled veya bellek ayarı yanlış");
                Check(graph["reference"]!["inputs"]!["width"]!.GetValue<int>()==(vertical?288:512)&&!graph.ToJsonString().Contains("http"),"Yanlış oran veya bulut düğümü");
            }
            Reject(()=>LocalLtxVideo.Graph("m","t","r","Valid motion instruction",7,false,12));
        });
        Test("LIBRARY-REFRESH-LAYOUT", "Liste yeniden kurulunca sayfa ve arama alanı daralmaz",()=>{
            var st=New("library-refresh-layout");using var form=new StudioForm(st){Width=1500,Height=1000};form.Show();Application.DoEvents();var navigate=typeof(StudioForm).GetMethod("Navigate",BindingFlags.NonPublic|BindingFlags.Instance)!;
            foreach(string route in new[]{"Videolarım","Videolarım","Video hazırla","Videolarım","Videolarım"}){
                navigate.Invoke(form,[route]);Application.DoEvents();if(route!="Videolarım")continue;
                var workspace=(FlowLayoutPanel)typeof(StudioForm).GetField("page",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(form)!;
                IEnumerable<Control> All(Control control)=>control.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(All(x)));
                var search=All(workspace).Single(x=>x.AccessibleName=="Video adı veya konu ara");Check(!workspace.AutoSize&&workspace.Width>650&&search.Width>300,"Yenileme sayfayı dar şeride sıkıştırdı");
            }form.Close();
        });
        Test("SCENE-VISUAL-UNIQUENESS", "Otomatik kurguda aynı içerik ikinci sahneye yerleşmez; yeni sahne talimatı arka planı değiştirir",()=>{
            var st=New("unique-scenes");string first=Path.Combine(st.Root,"one.png"),copy=Path.Combine(st.Root,"copy.png");File.WriteAllText(first,"same visual content");File.Copy(first,copy);
            var e=new Episode{IncludeLong=false,ShortCount=1,Creation=new CreationFlow{Mode="auto",AiVisuals=true},Plan=new ContentPlan{Shorts=[new VideoScript{Scenes=[new Scene{StockFile=first},new Scene{StockFile=copy}]}]}};
            Reject(()=>WorkflowMedia.UniqueSceneVisuals(e));WorkflowMedia.UniqueSceneVisuals(e,true);Check(e.Plan.Shorts[0].Scenes[0].StockFile==first&&e.Plan.Shorts[0].Scenes[1].StockFile=="","Tekrarlanan içerik yeni üretime ayrılmadı");
            e.AiStoryLocal=true;var scene=e.Plan.Shorts[0].Scenes[0];scene.AiImagePrompt="Two distinct people talking in an orchard.";Check(StoryImages.Prompt(e,e.Plan.Shorts[0],scene,true).Contains("Replace the original background"),"Referans arka planını değiştiren sahne talimatı eksik");
        });
        Test("SCENE-COMPOSITION", "AI kompozisyonu metni korur, resim/video karışımı üretir ve bozuk planı reddeder",()=>{
            var defaultFlow=new CreationFlow{AiVisuals=true,Mode="auto",CoverChoice="none"};defaultFlow.Job.Mode="full";Check(!SceneComposition.Enabled(new Episode{Creation=defaultFlow}),"Otonom üretim LTX seçeneğini kendiliğinden açtı");
            var st=New("composition");st.Settings.ProviderOrder="Pollinations";st.Settings.EnabledCloudProviders="Pollinations";
            string response="{\"Scenes\":[{\"Id\":0,\"Kind\":\"video\",\"Motion\":\"The man stays seated while the fountain water flows naturally.\"},{\"Id\":1,\"Kind\":\"image\",\"Motion\":\"\"}]}";
            using var http=new HttpClient(new Handler(_=>Task.FromResult(Ok(JsonSerializer.Serialize(new{choices=new[]{new{message=new{content=response},finish_reason="stop"}}})))));
            var first=new Scene{Narration="Adam çeşmenin önünde oturuyor."};var second=new Scene{Narration="Taş duvarlarda geçmişin izleri var."};var plan=new ContentPlan{Long=new VideoScript{Scenes=[first,second]}};var e=new Episode{Topic="Çeşmenin hikâyesi",Creation=new CreationFlow{AiVisuals=true,Mode="auto",CoverChoice="none",MixedSceneVideos=true}};
            SceneComposition.Plan(e,plan,new Services(st,_=>{},http),CancellationToken.None).GetAwaiter().GetResult();
            Check(first.VisualKind=="video"&&second.VisualKind=="image"&&first.Narration=="Adam çeşmenin önünde oturuyor.","Kompozisyon metni bozdu veya video seçmedi");
            Reject(()=>SceneComposition.Read(response.Replace("\"Kind\":\"image\"","\"Kind\":\"video\"").Replace("\"Motion\":\"\"","\"Motion\":\"A slow view of the same stone wall.\""),2));
            e.Creation.MixedSceneVideos=false;Check(!SceneComposition.Enabled(e),"Kapalı tercih aşıldı");
        });
        Test("LTX-MEASURED-CUT", "18 saniyelik anlatım üç ayrı kliple dolar; önbellek korunur, video döngüsü kullanılmaz",()=>{
            var st=New("ltx-cut");var e=new Episode();var scene=new Scene{VideoMotionPrompt="Water flows softly in the fountain while the same man remains seated."};string reference=Path.Combine(st.Root,"reference.png");using(var bitmap=new Bitmap(64,64))bitmap.Save(reference);
            int calls=0;var seeds=new HashSet<long>();
            async Task Generate(string image,string prompt,string path,int seconds,bool vertical,long seed,CancellationToken ct){calls++;Check(image==reference&&seconds==6&&seeds.Add(seed),"Referans, süre veya ayrı üretim tohumu yanlış");await Media.Exec(st.Ffmpeg,["-y","-f","lavfi","-i","color=c="+(calls==1?"red":calls==2?"green":"blue")+":s=128x72:r=24","-t",seconds.ToString(),"-an","-c:v","libx264","-preset","ultrafast",path],st.Root,ct);}
            string output=SceneMotion.Prepare(st,e,scene,reference,18,false,CancellationToken.None,_=>{},Generate).GetAwaiter().GetResult();Check(calls==3&&Math.Abs(Media.Duration(st,output,CancellationToken.None).GetAwaiter().GetResult()-18)<.1,"Anlatım süresi karşılanmadı");
            SceneMotion.Prepare(st,e,scene,reference,18,false,CancellationToken.None,_=>{},Generate).GetAwaiter().GetResult();Check(calls==3,"Hazır video yeniden üretildi");Check(SceneMotion.ShotCount(6)==1&&SceneMotion.ShotCount(18)==3,"Parça sayısı yanlış");Reject(()=>SceneMotion.ShotCount(73));
        });
        Test("FILM-CALM-CUT", "Otonom film ölçülü yavaşlar, kamera efekti kaldırılır ve sonraki parça son kareden devam eder",()=>{
            var st=New("film-calm");var e=new Episode{AiStoryAutonomous=true};var scene=new Scene{VideoMotionPrompt="The man lowers the apple. The camera slowly zooms into his eyes."};string reference=Path.Combine(st.Root,"reference.png");using(var bitmap=new Bitmap(64,64))bitmap.Save(reference);
            int calls=0;
            async Task Generate(string image,string prompt,string path,int seconds,bool vertical,long seed,CancellationToken ct){calls++;Check(seconds==6&&!prompt.Contains("slowly zooms"),"Kamera efekti veya üretim süresi yanlış");Check(calls==1?image==reference:Path.GetFileName(image)=="continue-2.png"&&File.Exists(image),"Sahne başlangıç karesine döndü");await Media.Exec(st.Ffmpeg,["-y","-f","lavfi","-i","color=c=green:s=128x72:r=24","-t","6","-an","-c:v","libx264","-preset","ultrafast",path],st.Root,ct);}
            string output=SceneMotion.Prepare(st,e,scene,reference,14,false,CancellationToken.None,_=>{},Generate).GetAwaiter().GetResult();Check(calls==2&&Math.Abs(Media.Duration(st,output,CancellationToken.None).GetAwaiter().GetResult()-14)<.12,"Ölçülü yavaşlatma anlatıma uymadı");
            Check(FilmMotionPolicy.Shots(7.2)==1,"Kısa boşluk için gereksiz ikinci parça üretildi");
        });
        Test("LTX-CANCEL-OWN-JOB", "LTX durdurma yalnız kendi işini kuyruktan siler; başka çalışan işi kesmez",()=>{
            var st=New("ltx-cancel");string image=Path.Combine(st.Root,"ref.png");File.WriteAllBytes(image,[1,2,3]);using var stop=new CancellationTokenSource();bool removed=false,interrupted=false;
            using var http=new HttpClient(new Handler(async request=>{
                string path=request.RequestUri!.AbsolutePath;
                if(path=="/object_info/CheckpointLoaderSimple")return Ok("{\"CheckpointLoaderSimple\":{\"input\":{\"required\":{\"ckpt_name\":[[\"ltxv-2b-0.9.8-distilled-fp8.safetensors\"]]}}}}");
                if(path=="/object_info/CLIPLoader")return Ok("{\"CLIPLoader\":{\"input\":{\"required\":{\"clip_name\":[[\"t5xxl_fp8_e4m3fn.safetensors\"]]}}}}");
                if(path.StartsWith("/object_info/"))return Ok("{\""+path.Split('/')[^1]+"\":{}}");
                if(path=="/upload/image")return Ok("{\"name\":\"ref.png\"}");
                if(path=="/prompt")return Ok("{\"prompt_id\":\"owned-ltx-job\"}");
                if(path.StartsWith("/history/")){stop.Cancel();return Ok("{}");}
                if(path=="/queue"&&request.Method==HttpMethod.Post){string body=await request.Content!.ReadAsStringAsync();removed=body.Contains("owned-ltx-job");return Ok("{}");}
                if(path=="/queue")return Ok("{\"queue_running\":[[0,\"another-users-job\"]]}");
                if(path=="/interrupt"){interrupted=true;return Ok("{}");}
                throw new Exception("Beklenmeyen bağlantı: "+path);
            }));
            bool canceled=false;try{LocalLtxVideo.Generate(image,"A slow natural movement at the fountain.",Path.Combine(st.Root,"scene.mp4"),5,false,1,_=>{},stop.Token,http).GetAwaiter().GetResult();}catch(OperationCanceledException){canceled=true;}
            Check(canceled&&removed&&!interrupted,"İptal başka işe müdahale etti veya kendi işi silinmedi");
        });
        Test("LTX-EDIT-INVALIDATION", "LTX planı yayın yetkisi ve çıktı kaydına dahildir; manuel dosya eski LTX bağını temizler",()=>{
            var scene=new Scene{VisualKind="video",VideoMotionPrompt="The water flows in the same fountain.",AiVideoFile="old.mp4",AiVideoHash="old"};var f=new CreationFlow{AiVisuals=true,Mode="auto",CoverChoice="none",MixedSceneVideos=true};var e=new Episode{Creation=f,Plan=new ContentPlan{Long=new VideoScript{Scenes=[scene]}}};string before=Reliability.MediaPlanHash(e);scene.VideoMotionPrompt+=" Gentle movement.";Check(before!=Reliability.MediaPlanHash(e),"Hareket değişimi eski çıktıyı geçerli saydı");SceneVideoChoice.Apply(scene,"manual.png",null);Check(scene.VisualKind=="image"&&scene.AiVideoFile==""&&scene.VideoMotionPrompt=="","Manuel görüntü eski LTX planını kullanıyor");ProductionStages.Observe(e,"LTX • parça 2/3");Check(e.CurrentStage.Contains("2/3"),"Arka plan işlemi görünmüyor");
        });
        Test("AUTO-PROFILE-UI", "Yerel resim stüdyosunda otomatik ve manuel kalite seçimi ile cihaz bilgisi gösterilir",()=>{
            using var dialog=new AiDesignDialog(New("automatic-profile-ui"),false,"Örnek konu");
            IEnumerable<Control> All(Control c)=>c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(All(x)));
            Check(All(dialog).OfType<ComboBox>().Any(x=>x.AccessibleName=="Kapak üretim kalitesi"&&x.Items.Cast<string>().SequenceEqual(new[]{"Otomatik","Hafif","Orta","Yüksek"})),"Otomatik ve manuel kalite seçenekleri eksik");
            Check(All(dialog).Any(x=>x.AccessibleName=="Cihaza önerilen görsel profili"),"Otomatik cihaz bilgisi görünmüyor");
            using var image=new Bitmap(dialog.Width,dialog.Height);dialog.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(root,"automatic-image-studio.png"),System.Drawing.Imaging.ImageFormat.Png);
        });
        Test("IMAGE-DEVICE-PROFILES", "Üç profil cihaza göre kısıtlanır; örnekleme ve yüz işleme yükü birlikte değişir",()=>{
            Check(LocalImageProfiles.ForMemory("Yüksek",2L*1024*1024*1024)=="Hafif"&&LocalImageProfiles.ForMemory("Yüksek",6L*1024*1024*1024)=="Orta"&&LocalImageProfiles.ForMemory("Yüksek",10L*1024*1024*1024)=="Yüksek","Bellek profili yanlış");
            Check(LocalImageProfiles.ForMemory("Hafif",12L*1024*1024*1024)=="Hafif"&&LocalImageProfiles.ForMemory("Yüksek",null)=="Hafif","Seçilen üst sınır veya bilinmeyen cihaz koruması yok");
            System.Text.Json.Nodes.JsonObject Device(string type,int total,int free)=>new(){["type"]=type,["vram_total"]=total*1024L*1024*1024,["vram_free"]=free*1024L*1024*1024};
            Check(LocalImageProfiles.DeviceProfile(Device("cpu",64,32))=="Hafif"&&LocalImageProfiles.DeviceProfile(Device("cuda",8,8))=="Orta"&&LocalImageProfiles.DeviceProfile(Device("cuda",12,10))=="Yüksek"&&LocalImageProfiles.DeviceProfile(Device("cuda",12,2))=="Hafif","Cihaz kapasitesi veya CPU koruması uygulanmadı");
            string file=Path.Combine(AppContext.BaseDirectory,"tools","workflows",ImportedFluxWorkflow.FileName);int prior=0;
            foreach(string quality in LocalImageProfiles.Labels){var graph=LocalCoverEngine.Prepare(file,"test",false);LocalImageProfiles.ApplyImported(graph,quality,LocalImageProfiles.Edge(quality),432);int steps=graph["5"]!["inputs"]!["steps"]!.GetValue<int>();Check(steps>prior,"Profiller aynı örnekleme ayarını kullanıyor");prior=steps;Check(graph["13"]!["inputs"]!["provider"]!.GetValue<string>()=="CPU"&&graph["16"]!["inputs"]!["tiled_decode"]!.GetValue<bool>(),"Güvenli yüz işleme uygulanmadı");}
        });
        Test("IMPORTED-FLUX-CLEAN", "İçe aktarılan FLUX boş şablondur; yüz detayı ve ayarlanmış adımlar korunur",()=>{
            string file=Path.Combine(AppContext.BaseDirectory,"tools","workflows",ImportedFluxWorkflow.FileName);
            var graph=LocalCoverEngine.Prepare(file,"A mountain at sunrise",false,quality:"Hızlı");
            Check(graph["14"]!["inputs"]!["text"]!.GetValue<string>()=="A mountain at sunrise","Prompt aktarılmadı");
            Check(graph["10"]!["inputs"]!["image"]!.GetValue<string>()=="{{ANKA_REFERENCE}}","Şablonda eski resim var");
            Check(graph["5"]!["inputs"]!["steps"]!.GetValue<int>()==25&&graph["16"]!["inputs"]!["steps"]!.GetValue<int>()==25,"Kullanıcının örnekleme ayarları kayboldu");
            ImportedFluxWorkflow.WithoutReference(graph);
            Check(!graph.Any(x=>x.Value?["class_type"]?.GetValue<string>() is "LoadImage" or "ApplyPulidFlux"),"Referanssız üretim eski görseli arıyor");
            Check(graph["16"]?["class_type"]?.GetValue<string>()=="FaceDetailer"&&graph["7"]!["inputs"]!["images"]![0]!.GetValue<string>()=="16","Yüz iyileştirme zinciri kayboldu");
            Check(graph["5"]!["inputs"]!["model"]![0]!.GetValue<string>()=="1"&&graph["16"]!["inputs"]!["model"]![0]!.GetValue<string>()=="1","Referanssız model bağlantısı kopuk");
        });
        Test("IMPORTED-FLUX-REFERENCE", "Yeni PuLID referansı örnek fotoğrafı değiştirir; tüm yüz zinciri aynı kimliği kullanır",()=>{
            string file=Path.Combine(AppContext.BaseDirectory,"tools","workflows",ImportedFluxWorkflow.FileName);
            foreach(int count in new[]{1,2}){
                var graph=LocalCoverEngine.Prepare(file,"Portrait",false);
                LocalCoverEngine.ApplyReference(graph,"new-reference.png",123,count,referenceWidth:1024);
                Check(graph["10"]!["inputs"]!["image"]!.GetValue<string>()=="new-reference.png","Yeni referans bağlanmadı");
                Check(graph.Count(x=>x.Value?["class_type"]?.GetValue<string>()=="ApplyPulidFlux")==count,"Kimlik zinciri kopyalandı veya eksik");
                Check(graph["5"]!["inputs"]!["model"]!.ToJsonString()==graph["16"]!["inputs"]!["model"]!.ToJsonString(),"Yüz detayı farklı modeli kullanıyor");
                Check(graph["tf_user_pulid_0"]!["inputs"]!["weight"]!.GetValue<double>()==1&&graph["16"]!["inputs"]!["denoise"]!.GetValue<double>()==.4,"Gelişmiş ayarlar değiştirildi");
            }
        });
        Test("AUTONOMOUS-DRAFT-CHECKPOINT", "Yarı otonom senaryoda bekler; tam otonom kaynakları saklar ve hazır metni tekrar üretmez",()=>{
            foreach(bool review in new[]{true,false}){
                var st=New("autonomy-draft-"+review);st.Settings.SourceUrls="";st.Settings.ProviderOrder="Pollinations";st.Settings.EnabledCloudProviders="Pollinations";int calls=0;
                using var http=new HttpClient(new Handler(req=>{calls++;if(req.Method==HttpMethod.Get)return Task.FromResult(Ok(JsonSerializer.Serialize(new{query=new{pages=new Dictionary<string,object>{["1"]=new{title="Musa",fullurl="https://tr.wikipedia.org/wiki/Musa",extract=new string('a',200)},["2"]=new{title="Mısır",fullurl="https://tr.wikipedia.org/wiki/Mısır",extract="Musa ve Mısır hakkında kaynak. "+new string('b',200)}}}})));if(req.Content!.ReadAsStringAsync().Result.Contains("NarrationGrounding"))return Task.FromResult(Ok(JsonSerializer.Serialize(new{choices=new[]{new{finish_reason="stop",message=new{content="{\"Accept\":true,\"Reason\":\"Kaynakla uyumlu.\"}"}}}})));return Task.FromResult(Ok(JsonSerializer.Serialize(new{choices=new[]{new{finish_reason="stop",message=new{content="{\"Text\":\"Geçmişi anlamak için farklı kaynakları birlikte okumak önemlidir. İnsanlar sorular sorar, bilgileri karşılaştırır ve belirsiz noktaları açık bırakır. Böylece dinleyen kişi acele hüküm vermeden düşünür, farklı görüşleri tanır ve araştırmaya devam eder.\"}"}}}})));}));
                var flow=new CreationFlow{Normal=false};var e=new Episode{Topic="Musa",IncludeLong=false,ShortCount=1,ShortSeconds=15,Creation=flow};int checkpoints=0;
                bool ready=AutonomousProduction.Prepare(e,flow,new Services(st,_=>{},http),review,CancellationToken.None,()=>checkpoints++).GetAwaiter().GetResult();Check(ready==!review&&flow.TextApproved==!review&&flow.Sources.Count==2&&checkpoints>=2,"Onay sınırı veya kaynak kaydı hatalı");int prior=calls;
                flow.ApprovedHash=TimedNarration.TextHash(flow.Text);Check(AutonomousProduction.Prepare(e,flow,new Services(st,_=>{},http),false,CancellationToken.None,()=>{}).GetAwaiter().GetResult()&&calls==prior,"Hazır senaryo gereksiz yeniden istendi");
            }
        });
        Test("AUTONOMOUS-PUBLISH-AUTHORITY", "Başlangıç yetkisi kanal, konu, ses ve takvim değişince geçersizleşir",()=>{
            var e=new Episode{Topic="Dağlar",PublishAt=DateTimeOffset.UtcNow.AddDays(1),Creation=new CreationFlow{Job=new ProductionJob{Mode="full",PublishMode="scheduled"},Normal=true}};var f=e.Creation;var settings=new Settings();f.Job.Channel="channel";f.Job.Authorization=AutonomousProduction.Authorization(e,settings,"channel");AutonomousProduction.ValidatePublication(e,settings,"channel");
            Reject(()=>AutonomousProduction.ValidatePublication(e,settings,"different"));settings.WindowsVoice="changed";Reject(()=>AutonomousProduction.ValidatePublication(e,settings,"channel"));settings.WindowsVoice="";e.Topic="Başka konu";Reject(()=>AutonomousProduction.ValidatePublication(e,settings,"channel"));e.Topic="Dağlar";e.PublishAt=DateTimeOffset.UtcNow.AddMinutes(-1);Reject(()=>AutonomousProduction.ValidatePublication(e,settings,"channel",starting:true));
        });
        Test("AUTONOMOUS-OWN-TEXT-VOICE", "Hazır metin aynen korunur; yerel görsel ayarı ses seçimini değiştirmez",()=>{
            var st=New("autonomy-own");var flow=new CreationFlow{Normal=false,TextMode="own",OwnText="Hazır metin.\nDağlar ve ışık, bize düşünmek için bir fırsat verir.",StockVisuals=true,CoverChoice="none",Job=new ProductionJob{PublishMode="review"}};var e=new Episode{Topic="Dağlar",IncludeLong=false,ShortCount=1,Creation=flow};string before=flow.OwnText;using var http=new HttpClient(new Handler(_=>throw new Exception("Hazır metin için AI çağrıldı")));Check(AutonomousProduction.Prepare(e,flow,new Services(st,_=>{},http),false,CancellationToken.None,()=>{}).GetAwaiter().GetResult()&&flow.OwnText==before,"Hazır metin değişti");
            var settings=new Settings{VoiceProvider="Google Cloud TTS",GoogleTtsVoice="tr-TR-Standard-B"};AutonomousMediaPolicy.Configure(e,settings);Check(settings.VoiceProvider=="Google Cloud TTS"&&settings.GoogleTtsVoice=="tr-TR-Standard-B","Ses tercihi değişti");
        });
        Test("AI-ACTIVITY-ROW-BOUNDS", "Uzun işlem yazısı durdur tuşuyla çakışmaz ve mor zemin çizilmez",()=>{
            using var host=new Form{BackColor=Theme.Bg,ClientSize=new Size(900,90)};using var row=new OperationActivityRow(()=>{}){Dock=DockStyle.Top,Height=52};host.Controls.Add(row);row.Activity.UpdateActivity(new string('W',400)+" NVIDIA • geçen süre 00:44",true);host.Show();
            foreach(int width in new[]{360,900,1800}){host.ClientSize=new Size(width,90);host.PerformLayout();row.PerformLayout();Application.DoEvents();Check(row.Activity.Right+4<=row.Stop.Left&&row.Stop.Right<=row.ClientSize.Width,"İşlem yazısı durdur tuşunun alanına taştı");Check(row.Activity.BackColor.A==0,"İşlem yazısının zemini saydam değil");}
            host.ClientSize=new Size(1100,90);host.PerformLayout();row.PerformLayout();using var bitmap=new Bitmap(row.Width,row.Height);row.DrawToBitmap(bitmap,row.ClientRectangle);bitmap.Save(Path.Combine(root,"activity-row.png"));host.Close();
        });
        Test("RESEARCH-SCENARIO-QUERY", "Kaynak araması tanıtım başlığı yerine senaryodaki atıfla yapılır",()=>{
            var st=New("research-query");st.Vault.Set("TAVILY_API_KEY","fixture-only");st.Settings.ProviderOrder="Pollinations";st.Settings.EnabledCloudProviders="Pollinations";var flow=new CreationFlow{Prompt=new string('x',180)+" Esma binti Umeys, Ebû Dâvûd Vitir26, Allâhu Allâhu Rabbî"};var e=new Episode{Topic="Bu duayı okuyun",Creation=flow};int search=0;
            using var http=new HttpClient(new Handler(async request=>{if(request.RequestUri!.Host=="api.tavily.com"){search++;using var body=JsonDocument.Parse(await request.Content!.ReadAsStringAsync());Check(body.RootElement.GetProperty("query").GetString()!.Contains("Esma"),"Araştırma kullanıcı atfını kullanmadı");return Ok("{\"results\":[{\"url\":\"https://example.org/one\",\"title\":\"Kaynak bir\",\"content\":\"Kaynak metni bir\"},{\"url\":\"https://example.org/two\",\"title\":\"Kaynak iki\",\"content\":\"Kaynak metni iki\"}]}");}return Ok(JsonSerializer.Serialize(new{choices=new[]{new{message=new{content="{\"Queries\":[\"Esma binti Umeys Ebu Davud sıkıntı duası\"]}"},finish_reason="stop"}}}));}));
            var result=new Services(st,_=>{},http).Research(e,CancellationToken.None).GetAwaiter().GetResult();Check(search==1&&result.Sources.Count==2,"Senaryo kaynakları alınmadı");
        });
        Test("SCENARIO-DRAFT-REPAIR", "Zaman kodlu taslak kaybolmaz; eksik Text onarılır ve yalnız seslendirme alınır",()=>{
            var st=New("scenario-draft");st.Settings.ProviderOrder="Pollinations";st.Settings.EnabledCloudProviders="Pollinations";
            var flow=new CreationFlow{TextMode="ai",Normal=false,Prompt="[00:00 - 00:09] Kanca: Bazen dünya üstüne gelir. (Görsel: fırtınalı deniz / Müzik: ney)\n[00:23] Dua: Allâhu Allâhu Rabbî, lâ üşrikü bihî şey’â.\nKapanış: Nur Kapısı kanalına abone ol."};var e=new Episode{Topic="Sıkıntı anında dua",ShortSeconds=50,Creation=flow};string before=flow.Prompt;int calls=0;
            const string narration="Bazen dünya üstüne gelir; insan nefes almakta zorlanır. Böyle anlarda dua ile Rabbimize yöneliriz. Allâhu Allâhu Rabbî, lâ üşrikü bihî şey’â. Allah’ım, gönlümüze ferahlık ver. Nur Kapısı kanalına abone olmayı unutmayın.";
            using var http=new HttpClient(new Handler(async request=>{calls++;string body=await request.Content!.ReadAsStringAsync();Check(body.Contains("UserScenario")&&body.Contains("zaman kodlar"),"Kullanıcı taslağı veya yorumlama kuralları gönderilmedi");return Ok(JsonSerializer.Serialize(new{choices=new[]{new{message=new{content=calls==1?"{\"Scenes\":[]}":JsonSerializer.Serialize(new{Text=narration})},finish_reason="stop"}}}));}));
            string result=ScriptDraft.Generate(e,flow,"Kaynakta kısa dua ve anlamı açıklanıyor.",new Services(st,_=>{},http),CancellationToken.None).GetAwaiter().GetResult();Check(calls==2&&result==narration&&flow.Prompt==before,"Eksik yanıt düzeltilmedi veya taslak değişti");
            foreach(string malformed in new[]{"{}","[]","{\"Text\":null}","{\"Text\":[]}"}){bool rejected=false;try{AutonomousProduction.ReadDraft(malformed);}catch(AiContentException){rejected=true;}Check(rejected,"Eksik alan açık içerik hatası olarak reddedilmedi");}
        });
        Test("RESEARCH-RELEVANCE", "Arama yedeği konu dışı sayfaları kaynak saymaz; Türkçe işaretler eşleşir",()=>{
            Check(KeylessWebResearch.Relevant("Ebu Davud ve Vitir bölümü", "Ebû Dâvûd Vitir 26 hadis metni"),"Türkçe atıf eşleşmedi");
            Check(!KeylessWebResearch.Relevant("Google Chrome indirme ve yükleme", "Allâhu Allâhu Rabbî lâ üşrikü bihî şeyâ"),"İlgisiz indirme sayfası kabul edildi");
            Check(!KeylessWebResearch.Relevant("Baba filmi ve aktörler", "Esma binti Umeys sıkıntı duası"),"İlgisiz film kabul edildi");
        });
        Test("SCENARIO-OPTIONAL-BRIEF", "Boş talimatta AI konudan senaryo yazar; taslak verilince talimat korunur",()=>{
            var st=New("optional-brief");st.Settings.ProviderOrder="Pollinations";st.Settings.EnabledCloudProviders="Pollinations";
            foreach(string brief in new[]{"", "  ", "Yaşlı bir adamın yardımseverliğini anlat; sakin bir kapanış yaz."}){
                var flow=new CreationFlow{Normal=false,TextMode="ai",Prompt=brief};var e=new Episode{Topic="Yardımseverlik",Creation=flow};
                using var http=new HttpClient(new Handler(async req=>{string body=await req.Content!.ReadAsStringAsync();Check(body.Contains("Topic")&&body.Contains("UserScenario")&&body.Contains("Senaryoyu sen yaz"),"Konu veya AI yazarlık talimatı eksik");return Ok(JsonSerializer.Serialize(new{choices=new[]{new{message=new{content=JsonSerializer.Serialize(new{Text="Küçük bir yardım, bazen bir insanın bütün gününü değiştirir."})},finish_reason="stop"}}}));}));
                string text=ScriptDraft.Generate(e,flow,"",new Services(st,_=>{},http),CancellationToken.None).GetAwaiter().GetResult();Check(text.Length>10&&flow.Prompt==brief,"Boş talimat reddedildi veya kullanıcı metni değişti");
            }
        });
        Test("WIZARD-CONDITIONAL-MENUS", "Tasarım alt menüleri seçime göre görünür ve önceki değerler korunur",()=>{
            var st=New("conditional-menus");var flow=new CreationFlow{Normal=false,TextMode="ai",AiVisuals=false,OwnVisuals=false,Voice="file",Subtitles=false,CoverChoice="none",Reached=1};var e=new Episode{Topic="Menü denemesi",WizardStep=1,Creation=flow,Music=false};st.Episodes.Add(e);
            using var form=new StudioForm(st){Width=1440,Height=1000};form.Show();Application.DoEvents();
            IEnumerable<Control> All(Control c)=>c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(All(x)));
            foreach(var disclosure in All(form).OfType<StudioDisclosure>().ToArray())disclosure.SetExpanded(true);Application.DoEvents();
            Control Named(string name)=>All(form).Single(x=>x.AccessibleName==name);
            Check(!Named("AI görsel tarzı • isteğe bağlı").Visible&&!Named("Ses sağlayıcısı").Visible&&!Named("Hazır altyazı kalıbı").Visible,"Seçilmemiş alt menü görünüyor");
            var voice=All(form).OfType<CheckBox>().Single(x=>x.Text=="AI seslendirmesi");voice.Checked=true;Application.DoEvents();Check(Named("Ses sağlayıcısı").Visible,"AI sesi seçince sağlayıcı açılmadı");
            var ai=All(form).OfType<CheckBox>().Single(x=>x.Text=="AI ile görsel oluştur");ai.Checked=true;Application.DoEvents();var style=(TextBox)Named("AI görsel tarzı • isteğe bağlı");Check(style.Visible,"AI görsel ayarları açılmadı");style.Text="Yumuşak ışık";ai.Checked=false;Application.DoEvents();Check(!style.Visible&&style.Text=="Yumuşak ışık","Gizli ayar değeri kayboldu");ai.Checked=true;Check(style.Visible,"Tekrar seçim ayarları açmadı");
            var on=All(form).OfType<CheckBox>().Single(x=>x.Text=="Altyazı açık");on.Checked=true;Application.DoEvents();Check(Named("Hazır altyazı kalıbı").Visible,"Altyazı kalıpları açılmadı");
            using var shot=new Bitmap(form.Width,form.Height);form.DrawToBitmap(shot,form.ClientRectangle);shot.Save(Path.Combine(root,"conditional-menus.png"));form.Close();
        });
        Test("AI-VALIDATOR-MISSING-FIELD", "Doğrulayıcının eksik JSON alanı sağlayıcı düzeltmesine girer",()=>{
            var st=New("missing-field");st.Settings.ProviderOrder="Pollinations";st.Settings.EnabledCloudProviders="Pollinations";int calls=0;
            using var http=new HttpClient(new Handler(_=>{calls++;return Task.FromResult(Ok(JsonSerializer.Serialize(new{choices=new[]{new{message=new{content=calls==1?"{}":"{\"Required\":true}"},finish_reason="stop"}}})));}));
            new Services(st,_=>{},http).Generate("Return JSON with Required","data",CancellationToken.None,validate:value=>{using var d=JsonDocument.Parse(value);return d.RootElement.GetProperty("Required").GetBoolean();},repairInvalidContent:true).GetAwaiter().GetResult();Check(calls==2,"Eksik alan yeniden istenmedi");
        });
        Test("AUTONOMOUS-TEXT-GUARDS", "İngilizce veya yarım AI anlatımı üretime verilmez; Türkçe özel isimleri korunur",()=>{
            Reject(()=>AutonomousProduction.ReadDraft("{\"Text\":\"Bu cümle yarım kaldı\"}"));Reject(()=>AutonomousProduction.ReadDraft("{\"Text\":\"What if the world was different and the people were there with their friends and they would have the chance to change the world with you.\"}"));Check(AutonomousProduction.ReadDraft("{\"Text\":\"Musa ve onun hayatı, insan için sabır ve sorumluluk üzerine düşünme fırsatıdır.\"}").StartsWith("Musa"),"Türkçe metin reddedildi");
        });
        Test("AUTONOMOUS-SETUP-UI", "Otonom başlangıç seçimleri tek panelde; yayın ve referans seçenekleri doğru bölümdedir",()=>{
            var st=New("autonomy-ui");using var form=new StudioForm(st){Width=1280,Height=900};var flow=new CreationFlow{Job=new ProductionJob{Mode="full",PublishMode="scheduled"},AiVisuals=true,StockVisuals=true};var e=new Episode{Topic="Dağlar",Creation=flow};var control=(Control)typeof(StudioForm).GetMethod("AutonomousSetup",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(form,[e,flow,st.Settings,900,(Action)(()=>{}),(Action)(()=>{})])!;form.Controls.Clear();control.Dock=DockStyle.Fill;form.Controls.Add(control);form.Show();Application.DoEvents();
            IEnumerable<Control> All(Control c)=>c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(All(x)));Check(All(form).OfType<DateTimePicker>().Single().Visible&&All(form).OfType<CheckBox>().Any(x=>x.Text.Contains("Yerel motorda")),"Başlangıç planı eksik");using var shot=new Bitmap(form.Width,form.Height);form.DrawToBitmap(shot,form.ClientRectangle);shot.Save(Path.Combine(root,"autonomous-setup-live.png"));form.Close();
        });
        Test("POLLINATIONS-LAST-RESORT", "Anahtarsız yedek en son denenir; kimlik bilgisi göndermez ve izinleri korur",()=>{
            var st=New("pollinations-fallback");st.Settings.FreeOnly=true;st.Settings.EnabledCloudProviders="Groq,Pollinations";st.Settings.ProviderOrder="Pollinations,Groq";st.Vault.Set("GROQ_API_KEY","fixture-key");
            Check(Services.ProviderSequence(st.Settings,null,"").SequenceEqual(new[]{"Groq","Pollinations"}),"Anahtarsız yedek sona taşınmadı");int calls=0;
            using var http=new HttpClient(new Handler(req=>{calls++;if(calls==1){Check(req.RequestUri!.Host=="api.groq.com","Önce mevcut sağlayıcı denenmedi");return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable){Content=new StringContent("{}")});}
                Check(req.RequestUri!.AbsoluteUri=="https://text.pollinations.ai/openai"&&req.Headers.Authorization==null,"Yedeğe anahtar gönderildi veya ücretli adres kullanıldı");return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new{choices=new[]{new{finish_reason="stop",message=new{content="{\"Text\":\"Türkçe tam metin.\"}"}}}}))});}));
            var result=new Services(st,_=>{},http).Generate("Yalnız JSON","Deneme",CancellationToken.None).GetAwaiter().GetResult();Check(calls==2&&result.Contains("Türkçe tam metin"),"Yedek sonucu alınamadı");
            st.Settings.CloudAiEnabled=false;Reject(()=>new Services(st,_=>{},http).Generate("JSON","Deneme",CancellationToken.None,onlyProvider:"Pollinations").GetAwaiter().GetResult());Check(calls==2,"Kapalı bulut izni aşıldı");
            st.Settings.CloudAiEnabled=true;st.Settings.EnabledCloudProviders="Groq";Reject(()=>new Services(st,_=>{},http).Generate("JSON","Deneme",CancellationToken.None,onlyProvider:"Pollinations").GetAwaiter().GetResult());Check(calls==2,"Sağlayıcı izni aşıldı");SettingsValidation.Check(st.Settings);
        });
        Test("POLLINATIONS-INCOMPLETE", "Anahtarsız yedek yarım veya boş yanıtı kabul etmez; başarısızsa ücretli adrese geçmez",()=>{
            foreach(string content in new[]{"", "{\"Text\":\"yarım"}){
                var st=New("pollinations-invalid-"+content.Length);st.Settings.ProviderOrder="Pollinations";st.Settings.EnabledCloudProviders="Pollinations";int calls=0;
                using var http=new HttpClient(new Handler(req=>{calls++;return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new{choices=new[]{new{finish_reason="stop",message=new{content}}}}))});}));
                Reject(()=>new Services(st,_=>{},http).Generate("JSON","Deneme",CancellationToken.None).GetAwaiter().GetResult());Check(calls==1,"Eksik yanıt kabul edildi veya yeni servis çağrıldı");
            }
        });
        Test("TIMELINE-SFX-REAL-RENDER", "Efektler gerçek videoya doğru zamanla karışır; görüntü yeniden kodlanmaz ve süre korunur",()=>{
            var st=New("timeline-sfx-render");string movie=Path.Combine(st.Root,"video.mp4"),sound=Path.Combine(st.Root,"effect.wav");
            Media.Exec(ffmpeg,["-y","-f","lavfi","-i","color=c=navy:s=320x180:r=25:d=2","-f","lavfi","-i","sine=frequency=300:duration=2","-c:v","libx264","-pix_fmt","yuv420p","-c:a","aac","-shortest",movie],st.Root,CancellationToken.None).GetAwaiter().GetResult();Media.Exec(ffmpeg,["-y","-f","lavfi","-i","sine=frequency=900:duration=0.5",sound],st.Root,CancellationToken.None).GetAwaiter().GetResult();string Hash()=>Media.Exec(ffmpeg,["-v","error","-i",movie,"-map","0:v","-c","copy","-f","hash","-"],st.Root,CancellationToken.None).GetAwaiter().GetResult();string before=Hash();var script=new VideoScript{SoundEffects=[new MusicTrack{Enabled=true,File=sound,Start=.8,End=1.3,Volume=.5,FadeIn=0,FadeOut=.05,RightsConfirmed=true,Attribution="Fixture effect credit"}]};TimelineEffects.Apply(st,script,movie,2,CancellationToken.None).GetAwaiter().GetResult();Check(before==Hash(),"Efekt görüntüyü yeniden kodladı");Check(Math.Abs(Media.Duration(st,movie,CancellationToken.None).GetAwaiter().GetResult()-2)<.1,"Efekt video süresini bozdu");Check(YouTube.BuildDescription(script,[]).Contains("Fixture effect credit"),"Efekt kaynak bilgisi kayboldu");
        });
        Test("SCENE-USER-REFERENCE", "Seçilen ortak referans proje içinde korunur ve yalnız AI görsellerini geçersiz kılar",()=>{
            var st=New("scene-reference");string reference=Path.Combine(st.Root,"reference.png");using(var image=new Bitmap(320,320))image.Save(reference);var flow=new CreationFlow();var e=new Episode{Topic="Konu",Creation=flow,IncludeLong=true,Plan=new ContentPlan{Long=new VideoScript{Scenes=[new Scene{StockFile="old-ai.png",AiImageHash="old"},new Scene{StockFile="user-photo.png"}]}}};string before=StoryImages.Identity(e);StoryImages.SelectReference(st,e,flow,reference);Check(File.Exists(flow.SceneReferenceFile)&&flow.SceneReferenceHash==Reliability.FileHash(reference)&&StoryImages.Identity(e)!=before,"Referans proje içinde saklanmadı veya kimliğe katılmadı");Check(e.Plan.Long.Scenes[0].StockFile==""&&e.Plan.Long.Scenes[1].StockFile=="user-photo.png","Referans değişikliği kullanıcı dosyasını sildi veya AI önbelleğini korudu");
        });
        Test("VOICE-STUDIO-API", "Ses klonlamada izin, hesap ve dosya koşulları; kelime zamanlı konuşma çözümlemesi",()=>{
            var st=New("voice-studio-api");st.Settings.FreeOnly=false;st.Vault.Set("ELEVENLABS_API_KEY","fixture-only");string file=Path.Combine(st.Root,"voice.wav");File.WriteAllText(file,"fixture audio");int calls=0;
            using var http=new HttpClient(new Handler(async request=>{calls++;Check(request.Headers.GetValues("xi-api-key").Single()=="fixture-only","API anahtarı başlıkta değil");string body=await request.Content!.ReadAsStringAsync();if(request.RequestUri!.AbsolutePath.EndsWith("/voices/add")){Check(body.Contains("fixture audio")&&body.Contains("files"),"Ses örneği gönderilmedi");return Ok("{\"voice_id\":\"clone-fixture\",\"requires_verification\":true}");}Check(body.Contains("scribe_v2")&&body.Contains("timestamps_granularity"),"Zamanlı STT modeli seçilmedi");return Ok("{\"words\":[{\"text\":\"Merhaba\",\"start\":0,\"end\":0.5,\"type\":\"word\"},{\"text\":\"dünya.\",\"start\":0.6,\"end\":1.2,\"type\":\"word\"}]}");}));
            var service=new Services(st,_=>{},http);Reject(()=>service.CloneVoice("Deneme",[file],false,CancellationToken.None).GetAwaiter().GetResult());Check(calls==0,"İzinsiz örnek gönderildi");var clone=service.CloneVoice("Deneme",[file],true,CancellationToken.None).GetAwaiter().GetResult();Check(clone.Id=="clone-fixture"&&clone.Verify,"Doğrulama gerekliliği kayboldu");var captions=service.TranscribeEleven(file,CancellationToken.None).GetAwaiter().GetResult();Check(captions.Count==1&&captions[0].End==1.2&&captions[0].Text=="Merhaba dünya.","Metin zamanları bozuldu");st.Settings.FreeOnly=true;Reject(()=>service.TranscribeEleven(file,CancellationToken.None).GetAwaiter().GetResult());Check(calls==2,"Ücretsiz modda bulut çağrısı yapıldı");
        });
        Test("COMMONS-LICENSED-MEDIA", "Kütüphane ses/video türünü ve lisansı ayırır; NC ve güvenilmeyen indirme adresleri alınmaz",()=>{
            string Payload(string license,string url)=>JsonSerializer.Serialize(new{query=new{pages=new Dictionary<string,object>{{"1",new{title="File:Rain.ogg",imageinfo=new[]{new{mime="application/ogg",mediatype="AUDIO",url,descriptionurl="https://commons.wikimedia.org/wiki/File:Rain.ogg",extmetadata=new{LicenseShortName=new{value=license},LicenseUrl=new{value="https://creativecommons.org/licenses/by-sa/4.0/"},Artist=new{value="<b>Creator</b>"}}}}}}}}});
            var rows=FreeMediaLibrary.ParseCommons(Payload("CC BY-SA 4.0","https://upload.wikimedia.org/rain.ogg"),"Ses efekti");Check(rows.Count==1&&rows[0].Kind=="Ses efekti"&&rows[0].Creator=="Creator"&&rows[0].Attribution.Contains("CC BY-SA"),"Lisans veya ses türü kayboldu");Check(FreeMediaLibrary.ParseCommons(Payload("CC BY-NC 4.0","https://upload.wikimedia.org/rain.ogg"),"Tümü").Count==0,"NC içerik serbest stok gibi listelendi");Check(!FreeMediaLibrary.DownloadHost(new Uri("https://upload.wikimedia.org.evil.test/file")),"İndirme kaynağı kontrolü aşıldı");
        });
        Test("LOCAL-SCHNELL-RESOLUTION", "Schnell modeli dört adımda ve kendi referans yöntemiyle hazırlanır; GPU belleği çözünürlüğü sınırlar",()=>{
            string workflow=Path.Combine(Program.Arg(args,"--source-bundle")??"outputs/AnkaStudio","tools","workflows","anka-flux-schnell.json");var graph=LocalCoverEngine.Prepare(workflow,"Snowy mountain",false,quality:"Hızlı");Check(LocalCoverEngine.ModelOf(workflow)=="FLUX Schnell"&&graph["5"]!["inputs"]!["steps"]!.GetValue<int>()==4,"Schnell hazır ayarı yanlış");var referenced=LocalCoverEngine.ApplyReference(graph,"ref.png",4,styleReference:true);Check(referenced["tf_reference_encode"]!=null&&!referenced.ToJsonString().Contains("IPAdapter"),"Schnell'e SDXL referans yöntemi uygulandı");var safe=LocalImagePolicy.Resolve("1536x864","Ultra sinematik",false,false,2L*1024*1024*1024);Check(safe==(768,432),"Düşük bellek sınırı uygulanmadı");Reject(()=>LocalImagePolicy.Resolve("1024x768","Dengeli",false,false));
        });
        Test("MANUAL-ASSET-LIBRARY-UI", "Manuel kurguda görüntü ve müzik listesi görünür; seçilen dosyalar gerçek sahne ve müzik şeridine uygulanır",()=>{
            var st=New("manual-assets-ui");string picture=Path.Combine(st.Root,"mountain.png"),music=Path.Combine(st.Root,"music.wav");using(var image=new Bitmap(320,180)){using var g=Graphics.FromImage(image);g.Clear(Color.Cyan);image.Save(picture);}File.WriteAllText(music,"audio fixture");new AudioLibrary(st).Save([new AudioItem{Name="Deneme müziği",File=music}]);var flow=new CreationFlow{Mode="manual",OwnVisuals=true,Files=[picture],Subtitles=false};var episode=new Episode{Creation=flow,IncludeLong=true,Plan=new ContentPlan{Long=new VideoScript{Scenes=[new Scene{Narration="Bir dağ görünür.",Duration=18},new Scene{Narration="Işık değişir.",Duration=12}]}}};st.Episodes.Add(episode);
            using var form=new StudioForm(st){Width=1440,Height=960};var host=Theme.Flow(1200);host.Dock=DockStyle.Fill;host.AutoScroll=true;form.Controls.Clear();form.Controls.Add(host);typeof(StudioForm).GetMethod("WorkflowScenes",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(form,[host,episode,false,flow,null,false,null]);form.Show();Application.DoEvents();
            IEnumerable<Control> All(Control control)=>control.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(All(x)));var files=All(form).OfType<ListView>().Single();Check(files.Visible&&files.Items.Count==1,"Görüntü listesi açılmadı");files.Items[0].Selected=true;files.Select();Application.DoEvents();All(form).OfType<Button>().Single(x=>x.Text=="Seçili sahneye yerleştir").PerformClick();Check(episode.Plan.Long.Scenes[0].StockFile==picture,"Dosya sahneye atanmadı: selected="+files.SelectedItems.Count+" files="+string.Join("|",episode.Plan.Long.Scenes.Select(x=>x.StockFile))+" button="+All(form).OfType<Button>().Single(x=>x.Text=="Seçili sahneye yerleştir").Enabled);var musicList=All(form).OfType<ListBox>().Single();All(form).OfType<Button>().Single(x=>x.Text=="Müzik").PerformClick();musicList.SelectedIndex=0;Application.DoEvents();All(form).OfType<Button>().Single(x=>x.Text=="Müziği yerleştir").PerformClick();Check(episode.Music&&episode.Plan.Long.MusicTrack?.File==music&&All(form).OfType<WorkflowTimeline>().Single().AllowDrop,"Müzik şeridi veya sürükleme bağlanmadı");All(form).OfType<Button>().Single(x=>x.Text=="Resim ve videolar").PerformClick();Application.DoEvents();using(var shot=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(shot,form.ClientRectangle);shot.Save(Path.Combine(root,"manual-timeline-live.png"));}form.Close();
        });
        Test("MANUAL-REVIEW-PLAYER-LAYOUT", "Manuel incelemede kaynak listesi monitörün yanında, timeline altında ve oynatıcı program içindedir",()=>{
            var st=New("manual-review-layout");string movie=Path.Combine(st.Root,"preview.mp4"),picture=Path.Combine(st.Root,"scene.png");Media.Exec(ffmpeg,["-y","-f","lavfi","-i","color=c=navy:s=320x180:r=25:d=2","-f","lavfi","-i","sine=frequency=300:duration=2","-c:v","libx264","-pix_fmt","yuv420p","-c:a","aac","-shortest",movie],st.Root,CancellationToken.None).GetAwaiter().GetResult();
            string reference=Path.GetFullPath("work/0429-local-live/results/mountain-reference.png");if(File.Exists(reference))File.Copy(reference,picture);else using(var image=new Bitmap(320,180)){using var g=Graphics.FromImage(image);g.Clear(Color.SteelBlue);image.Save(picture);}
            var flow=new CreationFlow{Normal=true,TextMode="own",OwnText="Dağda gün doğuyor. Işık vadiye yayılıyor.",Mode="manual",Split=true,OwnVisuals=true,Files=[picture],Subtitles=false,CoverChoice="none",Reached=3};flow.ApprovedHash=TimedNarration.TextHash(flow.Text);flow.PlannedTextHash=flow.Signature;var episode=new Episode{Topic="Manuel kurgu • kaynaklar ve zaman çizelgesi",Creation=flow,WizardStep=3,IncludeLong=true,Plan=new ContentPlan{Long=new VideoScript{Title="Dağda gün doğuyor",Scenes=[new Scene{Narration="Dağda gün doğuyor.",Duration=1,StockFile=picture,UserLocal=true},new Scene{Narration="Işık vadiye yayılıyor.",Duration=1,StockFile=picture,UserLocal=true}]}},Videos=[new AssetVideo{Key="long",File=movie}]};st.Episodes.Add(episode);st.Save();using var form=new StudioForm(st){Width=1500,Height=1080,TopMost=true};form.Show();Application.DoEvents();typeof(StudioForm).GetMethod("Navigate",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(form,["Video hazırla"]);Application.DoEvents();IEnumerable<Control> All(Control control)=>control.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(All(x)));var timeline=All(form).OfType<WorkflowTimeline>().Single();var player=All(form).OfType<WorkflowPlayer>().Single();var library=All(form).OfType<ListView>().Single(x=>x.AccessibleName=="Yüklenen resim ve videolar");Check(library.Visible&&timeline.PointToScreen(Point.Empty).Y>player.PointToScreen(Point.Empty).Y+player.Height,"Timeline monitörün altına yerleşmedi");Check(library.PointToScreen(Point.Empty).X<player.PointToScreen(Point.Empty).X,"Kaynak listesi monitörün yanında değil");Check(All(form).OfType<Button>().Any(x=>x.Text=="▶ Oynat")&&All(form).OfType<Button>().Any(x=>x.Text=="Test et / videoyu güncelle"),"Oynatma/test kontrolleri eksik");var workspace=(FlowLayoutPanel)typeof(StudioForm).GetField("page",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(form)!;Check(workspace.Parent is ScrollableControl{AutoScroll:false},"Gereksiz dış kaydırma çubuğu açık");var phase=All(form).OfType<FlowLayoutPanel>().Single(x=>Equals(x.Tag,"phase-tabs"));Check(phase.BackColor==Theme.Bg,"Adım tuşlarının arkasında renk şeridi kaldı");
            var viewport=All(form).OfType<WorkflowTimelineViewport>().Single();var monitor=(TableLayoutPanel)player.Parent!.Parent!.Parent!;Check(Math.Abs(viewport.Width-monitor.Width)<=2&&viewport.Left==monitor.Left,"Oynatıcı ve timeline kenarları farklı hizada");
            var reviewButtons=All(form).OfType<Button>().Where(b=>b.Text is "Değişiklikleri uygula • videoyu güncelle" or "Kapağı düzenle" or "Kapak / kart önizlemesi").ToArray();Check(reviewButtons.Length==3&&reviewButtons.Select(b=>b.Parent).Distinct().Count()==1,"Kapak ve üretim düğmeleri tek araç satırında değil");foreach(var button in reviewButtons)Check(button.Size==new Size(180,34)&&button.Margin==new Padding(0,0,8,8),"Düğme standardı bozuldu: "+button.Text);form.Refresh();Application.DoEvents();using(var shot=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(shot,new Rectangle(Point.Empty,form.Size));shot.Save(Path.Combine(root,"manual-review-live.png"));}form.Close();
        });
        Test("MANUAL-MODE-PLAN-PRESERVED", "Otomatikten manuele geçiş hazırlanmış çok sahneli planı geçersiz kılmaz",()=>{
            var flow=new CreationFlow{Mode="auto",OwnVisuals=true,Files=["a.png","b.png"],OwnText="Birinci cümle. İkinci cümle.",TextMode="own",Split=true};flow.PlannedTextHash=flow.Signature;flow.ChangeMode("manual");Check(flow.PlannedTextHash==flow.Signature&&flow.Split==true,"Mod geçişi planı bozdu");flow.OwnText+=" Yeni konu.";flow.ChangeMode("auto");Check(flow.PlannedTextHash!=flow.Signature,"Metin değişikliği yanlışlıkla geçerli sayıldı");
        });
        Test("MANUAL-SWAP-VISUALS", "Timeline görsel taşıması konuşma sırasını ve sürelerini korur, lisans ve AI bilgilerini taşır",()=>{
            var script=new VideoScript{Scenes=[new Scene{Narration="Birinci",Duration=18,StockFile="a.png",LicenseUrl="license-a",AiImageHash="hash-a"},new Scene{Narration="İkinci",Duration=9,StockFile="b.mp4",LicenseUrl="license-b"}]};TimelineEditing.SwapVisuals(script,0,1);Check(script.Scenes[0].StockFile=="b.mp4"&&script.Scenes[1].StockFile=="a.png"&&script.Scenes[1].AiImageHash=="hash-a"&&script.Scenes[0].LicenseUrl=="license-b","Görsel bilgisi kayboldu");Check(script.Scenes[0].Duration==18&&script.Scenes[0].Narration=="Birinci"&&script.Scenes[1].Duration==9,"Konuşma düzeni değişti");
        });
        Test("COVER-SELECTION-IMMEDIATE-SAVE", "Kapak seçimi akış, üretim ve video ayarlarına birlikte geçer",()=>{
            var st=New("cover-selection");var file=Path.Combine(st.Root,"cover.jpeg");using(var image=new Bitmap(128,72))image.Save(file,System.Drawing.Imaging.ImageFormat.Jpeg);var flow=new CreationFlow();var episode=new Episode{Creation=flow,Plan=new ContentPlan{Long=new VideoScript{CoverSettings=new Settings{CoverPng="old.png"}}}};var settings=new Settings();WorkflowArtwork.SelectCover(episode,settings,flow,file);Check(flow.CoverChoice=="file"&&File.Exists(VideoSettings.For(st.Settings,episode,episode.Plan.Long).CoverPng)&&episode.ProductionSettings!.CoverPng==file,"Kapak seçimi katmanlar arasında kayboldu");Reject(()=>WorkflowArtwork.SelectCover(episode,settings,flow,file+"missing"));Check(settings.CoverPng==file,"İptal/bozuk seçim hazır kapağı sildi");
        });
        Test("UPLOAD-CREDITS-NO-DIAGNOSTICS", "50 sahnenin AI eşleşme gerekçesi yayın kredisi sayılmaz; gerçek lisanslar korunur",()=>{
            var script=new VideoScript{Description="Konu",Scenes=Enumerable.Range(0,50).Select(i=>new Scene{UserLocal=true,Attribution="Yüklenen dosya • AI metin eşleşmesi: "+new string('x',200)+i}).ToList()};script.Scenes.Add(new Scene{Attribution="Real provider credit CC-BY",LicenseUrl="https://creativecommons.org/licenses/by/4.0/"});var description=YouTube.BuildDescription(script,[]);Check(description.Length<1000&&!description.Contains("AI metin eşleşmesi")&&description.Contains("Real provider credit CC-BY"),"İç tanı açıklamaları yayın sınırını doldurdu veya lisans silindi");
        });
        Test("SPEECH-BLOCK-REVISION-REUSE", "Önceki düzenlemenin sağlam ses bloğu taşınır; bozuk veya farklı ses bloğu alınmaz",()=>{
            var st=New("speech-revision");var e=new Episode();string text="Hazır ses korunur.",key=TimedNarration.TextHash("voice-one"),oldDirectory=Path.Combine(st.Folder(e),"r4-long","audio-cache","blocks");Directory.CreateDirectory(oldDirectory);string old=Path.Combine(oldDirectory,key+".wav");File.WriteAllText(old,"measured-audio-fixture");File.WriteAllText(old+".sha",Reliability.FileHash(old));string target=TimedNarration.ReusableBlock(st,e,key,text);Check(File.ReadAllText(target)==File.ReadAllText(old)&&target!=old,"Hazır ses bloğu önceki revizyondan alınmadı.");string different=TimedNarration.ReusableBlock(st,e,TimedNarration.TextHash("voice-two"),text);Check(!File.Exists(different),"Başka ses seçimi eski bloğu kullandı.");File.Delete(target);File.WriteAllText(old,"changed");string rejected=TimedNarration.ReusableBlock(st,e,key,text);Check(!File.Exists(rejected),"Bozuk ses bloğu tekrar kullanıldı.");
        });
        Test("ALIGNMENT-DETERMINISTIC-RETRY", "Aynı ses/metin doğrulama hatası geçici ağ hatası gibi tekrar edilmez",()=>{
            var e=new Episode();Reliability.Failed(e,new TimedNarration.TimingUnavailableException("Metin doğrulanamadı; özgün ses korundu."),"Produce");Check(e.RetryAt==null&&e.ResumeStage=="Produce","Deterministik ses hatası yeniden deneme döngüsüne girdi.");var transient=new Episode();Reliability.Failed(transient,new HttpRequestException("Geçici bağlantı hatası"),"Produce");Check(transient.RetryAt!=null,"Geçici bağlantının tekrar hakkı kayboldu.");
        });
        Test("ASSISTANT-NO-OVERLAP", "Dar pencerede sohbet çalışma alanının yerine açılır; alttaki kontrollerle çakışmaz",()=>{
            var st=New("assistant-no-overlap");using var form=new StudioForm(st);form.Show();Application.DoEvents();var body=(Control)typeof(StudioForm).GetField("body",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(form)!;var page=(Control)typeof(StudioForm).GetField("page",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(form)!;
            foreach(var size in new[]{new Size(900,600),new Size(1120,800),new Size(1920,960)}){form.Size=size;typeof(StudioForm).GetMethod("ToggleAssistant",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(form,null);Application.DoEvents();var host=(Control)typeof(StudioForm).GetField("assistantHost",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(form)!;Check(form.ClientRectangle.Contains(host.Bounds),"Sohbet pencere sınırından taşıyor: "+size+" "+host.Bounds);if(form.ClientSize.Width<1720)Check(!page.Parent!.Visible&&host.Left>=body.Left,"Sohbet alttaki düzenleme panelini kapatmadan üstüne bindi.");else Check(page.Parent!.Visible&&page.PointToScreen(Point.Empty).X+page.Width<=host.PointToScreen(Point.Empty).X,"Geniş pencerede paneller çakışıyor.");typeof(StudioForm).GetMethod("ToggleAssistant",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(form,null);Application.DoEvents();Check(page.Parent!.Visible,"Sohbet kapanınca çalışma alanı geri gelmedi.");}
            form.Close();
        });
        Test("WIZARD-BUSY-UNLOCK-RESIZE", "İşlem sırasında açılan sahne paneli bitince düzenlemeye açılır; alt düğmeler içerikle çakışmaz",()=>{
            var previousContext=SynchronizationContext.Current;SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());try{
                var st=New("wizard-unlock");var e=new Episode{Topic="Sahne düzenleme",Demo=true,IncludeLong=true,ShortCount=0,WizardStep=2};e.Plan=Engine.DemoPlan(e);var f=CreationFlow.Migrate(e);f.Reached=2;st.Episodes.Add(e);using var form=new StudioForm(st);form.Show();Application.DoEvents();var engine=(Engine)typeof(StudioForm).GetField("engine",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(form)!;var gate=new TaskCompletionSource();var task=engine.External(async ct=>await gate.Task.WaitAsync(ct),"Test işlemi");typeof(StudioForm).GetMethod("Navigate",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(form,["Video hazırla"]);Application.DoEvents();IEnumerable<Control> All(Control c)=>c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(All(x)));Check(All(form).OfType<NumericUpDown>().Any(x=>x.AccessibleName=="Seçili sahne süresi"&&!x.Enabled),"Çalışan işlemin sahneleri kilitlenmedi.");gate.SetResult();var until=DateTime.UtcNow.AddSeconds(5);while((!task.IsCompleted||!All(form).OfType<NumericUpDown>().Any(x=>x.AccessibleName=="Seçili sahne süresi"&&x.Enabled))&&DateTime.UtcNow<until){Application.DoEvents();Thread.Sleep(10);}task.GetAwaiter().GetResult();Check(All(form).OfType<NumericUpDown>().Any(x=>x.AccessibleName=="Seçili sahne süresi"&&x.Enabled),"Bitince sahne düzenleme kilidi kalkmadı.");foreach(var size in new[]{new Size(900,600),new Size(1440,960),new Size(1120,700)}){form.Size=size;Application.DoEvents();var primary=All(form).OfType<Button>().Single(x=>x.AccessibleName=="Ana işlem");var content=All(form).OfType<WorkflowTimeline>().Single().Parent!.Parent!;Check(content.Bottom<=primary.Parent!.Top,"İçerik alt işlem düğmelerinin üstüne taşıyor.");}form.Close();
            }finally{SynchronizationContext.SetSynchronizationContext(previousContext);}
        });
        Test("SETUP-API-STATUS", "Kayıt, doğrulama, kota ve eksik anahtar ayrılır; değerler çıktıya sızmaz",()=>{
            var st=New("setup-api");int requests=0;using var http=new HttpClient(new Handler(_=>{requests++;return Task.FromResult(Ok("{\"models\":[]}"));}));var missing=SetupChecks.Account(st,"GEMINI_API_KEY",CancellationToken.None,http).GetAwaiter().GetResult();Check(missing.Good==false&&requests==0,"Eksik anahtarla istek gönderildi.");
            st.Vault.Set("GEMINI_API_KEY","fixture-private-key");var valid=SetupChecks.Account(st,"GEMINI_API_KEY",CancellationToken.None,http).GetAwaiter().GetResult();Check(valid.Good==true&&requests==1&&!valid.Detail.Contains("fixture-private-key"),"Kimlik doğrulama veya gizlilik hatalı.");
            using var denied=new HttpClient(new Handler(_=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized){Content=new StringContent("fixture-private-key")})));var invalid=SetupChecks.Account(st,"GEMINI_API_KEY",CancellationToken.None,denied).GetAwaiter().GetResult();Check(invalid.Good==false&&invalid.Detail.Contains("401")&&!invalid.Detail.Contains("fixture-private-key"),"401 yanıtı yanlış / gizli içerik görünür.");
            Check(SetupChecks.HttpFailure(429).Status.Contains("Kota"),"Kota hatası anahtar yok gibi gösterildi.");st.Vault.Set("NVIDIA_API_KEY","fixture-nvidia");using var catalog=new HttpClient(new Handler(_=>Task.FromResult(Ok("{\"data\":[]}"))));var nvidia=SetupChecks.Account(st,"NVIDIA_API_KEY",CancellationToken.None,catalog).GetAwaiter().GetResult();Check(nvidia.Good==null,"Herkese açık olabilen katalog model doğrulaması sayıldı.");
        });
        Test("SETUP-MODEL-CAPABILITY", "Kurulu metin modeli görsel model diye işaretlenmez",()=>{
            var st=New("setup-model");st.Settings.OllamaVisionModel="qwen3:8b";using var http=new HttpClient(new Handler(req=>Task.FromResult(Ok(req.RequestUri!.AbsolutePath=="/api/tags"?"{\"models\":[{\"name\":\"qwen3:8b\"}]}":"{\"capabilities\":[\"completion\"]}"))));var result=SetupChecks.Program(st,"vision",CancellationToken.None,http).GetAwaiter().GetResult();Check(result.Good==false&&result.Status.Contains("uygun"),"Metin modeli görsel hazır sayıldı.");
        });
        Test("SETUP-CHECKLIST-UI", "Sol menüde kurulum listesi, amblemler, güncelle ve durdur kontrolleri bulunur",()=>{
            var previousContext=SynchronizationContext.Current;SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());try{var st=New("setup-ui");using var form=new StudioForm(st);form.Width=1440;form.Height=960;form.Show();Application.DoEvents();IEnumerable<Control> All(Control c)=>c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(All(x)));var menu=All(form).OfType<Button>().Single(x=>x.AccessibleName=="Ana bölüm: Yüklenmesi gerekenler");menu.PerformClick();Application.DoEvents();var refresh=All(form).OfType<Button>().Single(x=>x.AccessibleName=="Kurulum ve API listesini güncelle");Check(!refresh.Enabled,"İlk tarama başlamadı.");All(form).OfType<Button>().Single(x=>x.Text=="Taramayı durdur").PerformClick();var until=DateTime.UtcNow.AddSeconds(3);while(!refresh.Enabled&&DateTime.UtcNow<until){Application.DoEvents();Thread.Sleep(10);}if(!refresh.Enabled){using var failedShot=new Bitmap(form.Width,form.Height);form.DrawToBitmap(failedShot,form.ClientRectangle);failedShot.Save(Path.Combine(root,"setup-failure.png"));}Check(refresh.Enabled,"Tarama iptali sonrası Güncelle açılmadı. Stop: "+All(form).OfType<Button>().Single(x=>x.Text=="Taramayı durdur").Visible+" / "+All(form).OfType<Button>().Single(x=>x.Text=="Taramayı durdur").Enabled);Check(All(form).OfType<PictureBox>().Count(x=>x.AccessibleName?.EndsWith("amblemi")==true)==SetupChecks.Programs.Length+SetupChecks.Accounts.Length,"Bileşen amblemleri eksik.");using var shot=new Bitmap(form.Width,form.Height);form.DrawToBitmap(shot,form.ClientRectangle);shot.Save(Path.Combine(root,"setup-checklist-0425.png"));form.Close();}finally{SynchronizationContext.SetSynchronizationContext(previousContext);}
        });
        Test("TIMELINE-UI-CORTEX", "Çizelgede sahne zamanı, şeritler ve manuel kontroller; Cortex şeffaf maskotu görünür",()=>{
            var st=New("timeline-ui");var e=new Episode{IncludeLong=true,Creation=new CreationFlow{Subtitles=true},Plan=new ContentPlan{Long=new VideoScript{Scenes=[new Scene{Narration="Karlı dağlar görünür.",Duration=18},new Scene{Narration="Kitabın sayfaları açılır.",Duration=12}]}}};
            using var form=new StudioForm(st);form.Width=1400;form.Height=960;var host=Theme.Flow(1120);host.Dock=DockStyle.Fill;host.AutoScroll=true;form.Controls.Clear();form.Controls.Add(host);typeof(StudioForm).GetMethod("WorkflowScenes",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(form,[host,e,false,e.Creation,null,false,null]);form.Show();Application.DoEvents();
            IEnumerable<Control> All(Control c)=>c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(All(x)));var grid=All(form).OfType<DataGridView>().Single();Check(grid.Rows.Count==2&&grid.Rows[0].Cells[2].Value?.ToString()=="00:00:18.00","Bölüm zamanları listelenmedi.");grid.ClearSelection();grid.Rows[1].Selected=true;Application.DoEvents();var timeline=All(form).OfType<WorkflowTimeline>().Single();Check(timeline.Selected==1&&All(form).OfType<NumericUpDown>().Any(x=>x.AccessibleName=="Seçili sahne süresi"&&x.Value==12),"Manuel bölüm seçimi hatalı.");
            using(var shot=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(shot,form.ClientRectangle);shot.Save(Path.Combine(root,"timeline-0425.png"));}using var mascot=CortexMascot.Load();Check(mascot!=null&&mascot.GetPixel(0,0).A==0,"Cortex şeffaf görseli bulunamadı.");form.Close();
        });
        Test("TIMELINE-MANUAL-EDIT", "Süre, bölme ve birleştirme toplam konuşma süresini ve metni korur",()=>{
            var script=new VideoScript{Scenes=[new Scene{Narration="Bir yolcu dağa çıktı.",SubtitleText="Bir yolcu dağa çıktı.",Duration=18},new Scene{Narration="Sonra eve döndü.",Duration=12}]};
            TimelineEditing.Duration(script,0,20);Check(script.Scenes[0].Duration==20&&script.Scenes.Sum(x=>x.Duration)==30,"Toplam süre değişti.");Reject(()=>TimelineEditing.Duration(script,0,31));Reject(()=>TimelineEditing.Duration(script,0,double.NaN));
            TimelineEditing.Split(script,0,2);Check(script.Scenes.Count==3&&script.Scenes.Sum(x=>x.Duration)==30&&string.Concat(script.Scenes.Take(2).Select(x=>x.Narration))=="Bir yolcu dağa çıktı.","Bölme metni veya süreyi değiştirdi.");TimelineEditing.Merge(script,0);Check(script.Scenes.Count==2&&script.Scenes[0].Narration=="Bir yolcu dağa çıktı."&&script.Scenes.Sum(x=>x.Duration)==30,"Birleştirme hatalı.");
            using var timeline=new WorkflowTimeline(script);int selected=-1;double seek=-1;timeline.SelectedChanged+=i=>selected=i;timeline.Seek+=t=>seek=t;timeline.SelectScene(1);Check(selected==1&&seek==20&&WorkflowTimeline.Clock(18)=="00:00:18.00","Bölüm seçimi veya zaman gösterimi hatalı.");
        });
        Test("VISION-REQUEST-RETRY", "Geçici kota yeniden denenir; günlük kota ve iptal aşılmaz",()=>{
            int calls=0,waits=0;using var http=new HttpClient(new Handler(_=>{calls++;if(calls==1){var fail=new HttpResponseMessage(HttpStatusCode.TooManyRequests){Content=new StringContent("temporary")};fail.Headers.RetryAfter=new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(1));return Task.FromResult(fail);}return Task.FromResult(Ok("{}"));}));
            using var response=VisionRequests.Send(http,()=>new HttpRequestMessage(HttpMethod.Post,"https://example.test/vision"),"Gemini",_=>{},CancellationToken.None,(t,c)=>{waits++;return Task.CompletedTask;}).GetAwaiter().GetResult();Check(calls==2&&waits==1&&response.IsSuccessStatusCode,"Geçici kota yeniden denenmedi.");
            calls=0;using var daily=new HttpClient(new Handler(_=>{calls++;return Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests){Content=new StringContent("daily quota exceeded")});}));try{VisionRequests.Send(daily,()=>new HttpRequestMessage(HttpMethod.Post,"https://example.test/vision"),"Gemini",_=>{},CancellationToken.None,(_,_)=>Task.CompletedTask).GetAwaiter().GetResult();throw new Exception("Günlük kota kabul edildi.");}catch(HttpRequestException ex){Check(ex.StatusCode==HttpStatusCode.TooManyRequests&&calls==1,"Günlük kota tekrarlandı.");}
            using var stop=new CancellationTokenSource();stop.Cancel();try{VisionRequests.Send(http,()=>new HttpRequestMessage(HttpMethod.Post,"https://example.test/vision"),"Gemini",_=>{},stop.Token).GetAwaiter().GetResult();throw new Exception("İptal yok sayıldı.");}catch(OperationCanceledException){}
        });
        Test("LOCAL-VISION-FIRST", "Yerel görsel model önce seçilir; metin modeli ve bulut çağrısı atlanır",()=>{
            var st=New("local-vision-first");st.Settings.LocalAiFirst=true;st.Settings.UseOllama=true;st.Settings.FreeOnly=true;string image=Path.Combine(st.Root,"frame.jpg");File.WriteAllBytes(image,[1,2,3]);int chats=0;
            using var http=new HttpClient(new Handler(async request=>{Check(request.RequestUri!.Host=="127.0.0.1","Ücretsiz yerel analiz buluta çıktı.");string path=request.RequestUri.AbsolutePath;if(path=="/api/tags")return Ok("{\"models\":[{\"name\":\"qwen3:8b\"},{\"name\":\"qwen3-vl:4b-instruct\"}]}");string body=await request.Content!.ReadAsStringAsync();if(path=="/api/show")return Ok(body.Contains("qwen3-vl")?"{\"capabilities\":[\"completion\",\"vision\"]}":"{\"capabilities\":[\"completion\"]}");chats++;Check(body.Contains("qwen3-vl:4b-instruct")&&body.Contains("images"),"Yanlış model veya görsel girdi.");return Ok(JsonSerializer.Serialize(new{message=new{content="{\"Description\":\"Karlı dağlar ve açık bir gökyüzü.\"}"},done_reason="stop"}));}));
            string description=UploadedMediaMatch.Describe(st,image,http,_=>{},CancellationToken.None).GetAwaiter().GetResult();Check(chats==1&&description.Contains("dağlar"),"Yerel görsel okunamadı.");
        });
        Test("PREVIEW-PLAN-ISOLATION", "Hazırlık çizelgesi gerçek planı değiştirmez; bitince temizlenir",()=>{
            var st=New("preview-plan");var e=new Episode{Id="preview",Plan=new ContentPlan{Long=new VideoScript{Scenes=[new Scene{Narration="Eski metin"}]}}};var engine=new Engine(st);var preview=new ContentPlan{Long=new VideoScript{Scenes=[new Scene{Narration="Yeni metin",Duration=18}]}};
            engine.External(async ct=>{engine.ShowPreparingPlan(e,preview);Check(engine.PreparingPlan!.Long!.Scenes[0].Narration=="Yeni metin"&&e.Plan.Long!.Scenes[0].Narration=="Eski metin","Önizleme gerçek planı değiştirdi.");preview.Long!.Scenes[0].Narration="Dış değişiklik";Check(engine.PreparingPlan.Long.Scenes[0].Narration=="Yeni metin","Önizleme kopya değil.");await Task.CompletedTask;},"Test").GetAwaiter().GetResult();Check(engine.PreparingPlan==null&&engine.PreparingProjectId=="","Geçici çizelge temizlenmedi.");
        });
        Test("LOCAL-ENGINE-GPU-PREFERENCE", "NVIDIA kurulumu varken kökteki uyumsuz Intel motoru önce seçilmez; diğer motorlar yedekte kalır",()=>{
            string folder=Path.Combine(root,"engine-hardware"),nvidia=Path.Combine(folder,"NVIDIA","ComfyUI_windows_portable"),intel=Path.Combine(folder,"ComfyUI_windows_portable");foreach(var enginePath in new[]{intel,nvidia}){Directory.CreateDirectory(Path.Combine(enginePath,"python_embeded"));Directory.CreateDirectory(Path.Combine(enginePath,"ComfyUI"));File.WriteAllText(Path.Combine(enginePath,"python_embeded","python.exe"),"fixture");File.WriteAllText(Path.Combine(enginePath,"ComfyUI","main.py"),"fixture");}Check(LocalCoverEngine.FindEngine(folder)==nvidia,"Intel kök motoru NVIDIA'nın önüne geçti.");Check(LocalCoverEngine.FindEngines(folder).SequenceEqual(new[]{nvidia,intel}),"Alternatif motorlar korunmadı.");
        });
        Test("WORKFLOW-AI-PLAN-RETRY-CACHE", "Görsel denemesi başarısız olsa da karakter ve sahne planı korunur; yeniden metin isteği gönderilmez",()=>{
            var st=New("visual-retry-cache");var e=new Episode{Topic="Önbellek",IncludeLong=true,Plan=new ContentPlan{Long=new VideoScript{Scenes=[new Scene{Narration="Bir yolcu."}]}}};var story=CreationFlow.Copy(e);story.AiStoryImages=true;story.AiStoryCastPrompt="A traveler in brown clothes";story.AiStoryCastCount=1;story.AiStoryIdentityHash=StoryImages.Identity(story);story.AiStoryPromptHash=TimedNarration.TextHash(StoryImages.Context(story));story.Plan!.Long!.Scenes[0].AiImagePrompt="A traveler walking";story.Plan.Long.Scenes[0].AiCharacterIds=[0];WorkflowMedia.KeepVisualPlan(story,e);var retry=CreationFlow.Copy(e);retry.AiStoryImages=true;int calls=0;using var http=new HttpClient(new Handler(_=>{calls++;throw new InvalidOperationException("Yeni metin isteği gönderilmemeli.");}));StoryImages.Plan(st,retry,new Services(st,_=>{},http),CancellationToken.None,_=>{}).GetAwaiter().GetResult();Check(calls==0&&retry.AiStoryCastPrompt==story.AiStoryCastPrompt&&retry.Plan!.Long!.Scenes[0].AiImagePrompt=="A traveler walking","AI planı tekrar kullanılamadı.");Reject(()=>WorkflowMedia.KeepVisualPlan(story,new Episode()));
        });
        Test("AUTOMATIC-CLOUD-VISUAL-PERMISSIONS", "Otomatik bulut yedeği ücret izni, anahtar ve ücretsiz önceliğe uyar; yalnız izinli sağlayıcıları çağırır",()=>{
            var st=New("visual-fallback-permissions");var settings=new Settings{FreeOnly=true,CoverPaidAllowed=true,CoverAiEnabled=true,CoverMonthlyRequests=3,CoverProviderOrder="NVIDIA,Gemini"};st.Vault.Set("GEMINI_API_KEY","fixture");Check(!WorkflowMedia.CloudAlternativeAllowed(st,settings),"Ücretsiz öncelik aşıldı.");settings.FreeOnly=false;settings.CoverPaidAllowed=false;Check(!WorkflowMedia.CloudAlternativeAllowed(st,settings),"Ücret izni aşıldı.");settings.CoverPaidAllowed=true;Check(WorkflowMedia.CloudAlternativeAllowed(st,settings),"İzinli alternatif bulunamadı.");using var image=new Bitmap(256,256);using var memory=new MemoryStream();image.Save(memory,System.Drawing.Imaging.ImageFormat.Png);string data=Convert.ToBase64String(memory.ToArray());var hosts=new List<string>();using var http=new HttpClient(new Handler(req=>{hosts.Add(req.RequestUri!.Host);return Task.FromResult(Ok(JsonSerializer.Serialize(new{candidates=new[]{new{content=new{parts=new[]{new{inlineData=new{mimeType="image/png",data}}}}}}})));}));new CoverFallback(st,_=>{},http,new HashSet<string>{"Gemini","OpenAI"}).Create(Path.Combine(st.Root,"result.png"),settings,"Örnek","",false,CancellationToken.None,"A peaceful landscape").GetAwaiter().GetResult();Check(hosts.Count==1&&hosts[0]=="generativelanguage.googleapis.com","İzin verilmeyen sağlayıcı çağrıldı.");
        });
        Test("TUBEFLOW-BRANDING-MIGRATION", "Masaüstü kısayolu yeni EXE ve büyük simgeye geçerken eski veri yolu korunur",()=>{
            string apps=Path.Combine(root,"brand-app"),desktop=Path.Combine(root,"brand-desktop");Directory.CreateDirectory(apps);Directory.CreateDirectory(desktop);File.WriteAllText(Path.Combine(apps,"TubeFlow.exe"),"fixture");dynamic shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;dynamic shortcut=shell.CreateShortcut(Path.Combine(desktop,"AnkaStudio.lnk"));try{shortcut.TargetPath=Path.Combine(apps,"AnkaStudio.exe");shortcut.Save();}finally{System.Runtime.InteropServices.Marshal.FinalReleaseComObject((object)shortcut);}Check(AppBranding.RefreshShortcuts(apps,[desktop])==1,"Eski kısayol taşınmadı.");dynamic updated=shell.CreateShortcut(Path.Combine(desktop,"TubeFlow.lnk"));try{Check((string)updated.TargetPath==Path.Combine(apps,"TubeFlow.exe")&&((string)updated.IconLocation).StartsWith(Path.Combine(apps,"TubeFlow.exe")),"Kısayol yeni EXE veya simgeyi kullanmıyor.");}finally{System.Runtime.InteropServices.Marshal.FinalReleaseComObject((object)updated);System.Runtime.InteropServices.Marshal.FinalReleaseComObject((object)shell);}using var stream=typeof(StudioForm).Assembly.GetManifestResourceStream("AnkaStudio.Assets.anka-studio.ico")!;using var icon=new Icon(stream,48,48);using var bitmap=icon.ToBitmap();int left=bitmap.Width,right=0;for(int x=0;x<bitmap.Width;x++)for(int y=0;y<bitmap.Height;y++)if(bitmap.GetPixel(x,y).A>128){left=Math.Min(left,x);right=Math.Max(right,x);}Check(right-left>=bitmap.Width*.8,"Amblem simgede hâlâ çok küçük.");Check(typeof(StudioForm).Assembly.GetName().Name=="TubeFlow","Ana derleme adı değişmedi.");
        });
        Test("WORKFLOW-MUSIC-PROJECT-SELECTION", "Projenin seçili müziği genel ayardan bağımsız kaydolur, başlıkta görünür ve üretime aktarılır",()=>{
            var st=New("music-project");string song=Path.Combine(st.Root,"selected.wav");File.WriteAllText(song,"fixture");st.Settings.MusicPath="";new AudioLibrary(st).Save([new AudioItem{File=song,Name="Seçtiğim parça",License="CC0",RightsConfirmed=true}]);var settings=VideoQueue.Snapshot(st.Settings);settings.MusicPath=song;settings.MusicVolume=.23;var e=new Episode{Music=true,IncludeLong=true,ProductionSettings=settings,Plan=new ContentPlan{Long=new VideoScript()}};WorkflowMusic.Validate(e,settings);WorkflowMusic.Apply(st,e,settings);Check(e.Plan.Long!.MusicTrack!.File==song&&Math.Abs(e.Plan.Long.MusicTrack.Volume-.23)<.001,"Seçim sahne müziğine geçmedi.");st.Episodes.Add(e);st.Save();Check(GlobalMusic.Resolve(st,false,VideoSettings.For(st.Settings,e)).File==song&&st.Settings.MusicPath=="","Üretim genel ayardaki boş müziğe döndü.");Check(WorkflowMusic.Caption(st,settings).Contains("Seçtiğim parça"),"Seçili parça adı görünmüyor.");e.Music=false;WorkflowMusic.Apply(st,e,settings);Check(!e.Plan.Long.MusicTrack.Enabled,"Müziği kapatınca önceki parça çalmaya devam ediyor.");e.Music=true;settings.MusicPath="";Reject(()=>WorkflowMusic.Validate(e,settings));
        });
        Test("DESIGN-MODEL-QUALITY-CHOICES", "Üç hazır kapak motoru ve üç kalite seçimi gerçek iş akışına uygulanır",()=>{
            var st=New("design-model-choices");using var dialog=new AiDesignDialog(st,false,"Test kapağı");IEnumerable<Control> All(Control c)=>c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(All(x)));var model=All(dialog).OfType<ComboBox>().Single(x=>x.AccessibleName=="Kapak hazır motoru");var quality=All(dialog).OfType<ComboBox>().Single(x=>x.AccessibleName=="Kapak üretim kalitesi");Check(model.Items.Count==LocalCoverEngine.Models.Length-1&&quality.Items.Count==4,"Motor/kalite listesi eksik.");foreach(string engineName in model.Items){model.SelectedItem=engineName;var workflow=LocalCoverEngine.ResolveWorkflow(engineName);Check(LocalCoverEngine.ModelOf(workflow)==engineName,"Motor yanlış iş akışını seçti.");int last=0;foreach(string label in quality.Items.Cast<string>().Where(x=>x!="Otomatik")){quality.SelectedItem=label;var graph=LocalCoverEngine.Prepare(workflow,"test",false,quality:AiDesignDialog.EngineQuality(label));var node=graph.First(x=>x.Value?["class_type"]?.ToString() is "KSampler" or "Flux2Scheduler");if(engineName==ImportedFluxWorkflow.Model)LocalImageProfiles.ApplyImported(graph,label,1024,576);int steps=node.Value!["inputs"]!["steps"]!.GetValue<int>();Check(steps>last,"Kalite artınca işlem adımları artmıyor.");last=steps;}}
        });
        Test("EMBEDDED-VOICE-PREVIEW", "Ses örneği dış program açmadan gömülü oynatılır, duraklatılır ve durdurulur",()=>{
            var file=Path.Combine(root,"silent-preview.wav");using(var writer=new BinaryWriter(File.Create(file))){writer.Write(Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+16000*2*5);writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(16000);writer.Write(32000);writer.Write((short)2);writer.Write((short)16);writer.Write(Encoding.ASCII.GetBytes("data"));writer.Write(16000*2*5);writer.Write(new byte[16000*2*5]);}
            using var form=new Form{Text="TubeFlow — yerleşik ses önizleme testi (sessiz)",Width=760,Height=230,TopMost=true};var preview=new ArchiveMusicPlayer(true){Width=700,Location=new Point(16,20)};form.Controls.Add(preview);form.Show();preview.LoadAndPlay(file);var until=DateTime.UtcNow.AddSeconds(3);while(DateTime.UtcNow<until&&preview.PlaybackState!=3){Application.DoEvents();System.Threading.Thread.Sleep(20);}Check(preview.IsLoaded&&preview.PlaybackState==3,"Yerleşik medya bileşeni sesi başlatamadı.");preview.Controls.OfType<Button>().Single(x=>x.Text=="Oynat / Duraklat").PerformClick();Application.DoEvents();Check(preview.PlaybackState==2,"Ses duraklatılamadı.");preview.Controls.OfType<Button>().Single(x=>x.Text=="Durdur").PerformClick();Application.DoEvents();Check(preview.PlaybackState==1,"Ses durdurulamadı.");form.Refresh();Application.DoEvents();System.Threading.Thread.Sleep(250);using var shot=new Bitmap(form.Width,form.Height);using(var g=Graphics.FromImage(shot))g.CopyFromScreen(form.Location,Point.Empty,shot.Size);shot.Save(Path.Combine(root,"embedded-voice-live.png"));
        });
        Test("AI-TEXT-ACTIVITY", "AI yükleme simgesi çalışırken döner, bitince durur ve görünür alan boş kalmaz",()=>{
            using var form=new Form{Text="TubeFlow — yükleme göstergesi testi",Width=650,Height=160,TopMost=true,BackColor=Theme.Bg};using var activity=new AiTextActivity{Width=580,Height=44,Location=new Point(20,40),Font=Theme.Font(11)};form.Controls.Add(activity);form.Show();activity.UpdateActivity("Kaynaklar araştırılıyor • Metin hazırlanıyor…",true);Application.DoEvents();int start=activity.Frame;var until=DateTime.UtcNow.AddMilliseconds(300);while(DateTime.UtcNow<until){Application.DoEvents();System.Threading.Thread.Sleep(10);}Check(activity.Busy&&activity.Frame!=start,"Yükleme simgesi ilerlemiyor.");using var shot=new Bitmap(form.Width,form.Height);using(var g=Graphics.FromImage(shot))g.CopyFromScreen(form.Location,Point.Empty,shot.Size);shot.Save(Path.Combine(root,"ai-text-loading-live.png"));activity.UpdateActivity("",false);Check(!activity.Busy,"İş bitince gösterge durmadı.");
        });
        Test("DARK-CHAT-STREAMING-LAYOUT", "Kullanıcı balonları, uzun mesajlar ve geçici yanıt silinirken yerel işlem bildirimi korunur",()=>{
            using var form=new Form{ClientSize=new Size(480,600),BackColor=Theme.Bg};using var transcript=new ChatTranscript{Dock=DockStyle.Fill};form.Controls.Add(transcript);form.Show();Application.DoEvents();
            transcript.Append("Sen","Bir projeyi hazırla.");transcript.Partial("Yanıt hazırlanıyor");transcript.Append("İşlem","Yerel işlem bildirimi");transcript.EndPartial();
            var cards=transcript.Controls.OfType<StudioCard>().ToArray();Check(cards.Length==2&&cards[0].BackColor==Theme.AccentSurface,"Kullanıcı balonu veya geçici yanıt temizliği hatalı.");Check(cards.Any(c=>c.Controls.OfType<RichTextBox>().Any(t=>t.Text=="Yerel işlem bildirimi")),"Geçici yanıt kalıcı bildirimi sildi.");
            transcript.Append("TubeFlow",string.Join(" ",Enumerable.Repeat("Uzun yanıt satırı",300)));Application.DoEvents();Check(transcript.AutoScrollMinSize.Height>transcript.ClientSize.Height,"Uzun mesaj kaydırılabilir değil.");transcript.Clear();Check(transcript.Controls.Count==0,"Eski sohbet balonları temizlenmedi.");
        });
        Test("DARK-REFERENCE-ALL-SECTIONS", "Koyu referans görünümü altı ana bölümde ve sohbet panelinde iki pencere boyunda veri değiştirmeden açılır",()=>{
            var st=New("dark-reference");using var form=new StudioForm(st);form.CaptureDarkReference(Path.Combine(st.Root,"screenshots"));form.Close();
            using var heading=Theme.Font(12,true);Check(heading.Bold&&!heading.Italic,"Başlık fontu referans stile uymuyor.");
            using var menu=new ContextMenuStrip();SoftSurfaces.Apply(menu);Check(menu.BackColor==Theme.Panel&&menu.ForeColor==Theme.Text,"Açılır menü açık renkte kaldı.");
        });
        Test("NUMBER-ROUNDED-FRAME", "Süre kutularının çerçevesi ve okları kesilmez",()=>{
            using var form=new Form{Text="TubeFlow — süre çerçevesi otomatik testi",Width=640,Height=200,BackColor=Theme.Bg,TopMost=true,StartPosition=FormStartPosition.CenterScreen};
            var normal=Theme.Number(6,1,120,240);normal.Location=new Point(24,48);var shorts=Theme.Number(60,1,180,240);shorts.Location=new Point(300,48);form.Controls.AddRange([normal,shorts]);SoftSurfaces.Apply(normal);SoftSurfaces.Apply(shorts);form.Show();form.Activate();Application.DoEvents();
            foreach(var number in new[]{normal,shorts}){Check(number.BorderStyle==BorderStyle.None,"Eski kare çerçeve duruyor.");foreach(Control child in number.Controls)Check(child.Region==null,"Sayı metni veya oklar özel bölgeyle kırpılıyor.");number.UpButton();Check(number.Value==(number==normal?7:61),"Artırma çalışmıyor.");number.DownButton();}
            form.Refresh();Application.DoEvents();System.Threading.Thread.Sleep(250);Application.DoEvents();using var shot=new Bitmap(form.Width,form.Height);using(var g=Graphics.FromImage(shot))g.CopyFromScreen(form.Location,Point.Empty,shot.Size);shot.Save(Path.Combine(root,"number-frame-live.png"));
        });
        Test("CONNECTIONS-HEADER-CARDS", "Merkez başlıkları ilk sekmeyle eşit genişliktedir; alt başlıklar hizalı ve kartların altı kesilmez",()=>{
            var st=New("connections-header-cards");using var form=new StudioForm(st);form.TopMost=true;form.Width=1440;form.Show();form.Activate();Application.DoEvents();typeof(StudioForm).GetMethod("Navigate",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(form,["Bağlantı Merkezi"]);Application.DoEvents();IEnumerable<Control> All(Control c)=>c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(All(x)));var tab=All(form).OfType<Button>().Single(b=>b.AccessibleName=="Bölüm: Hesaplar ve AI");var sections=All(form).OfType<StudioDisclosure>().ToArray();foreach(var section in sections){var heading=section.Controls.OfType<Button>().First();Check(heading.Width==Math.Min(section.Width,tab.Width)&&heading.TextAlign==ContentAlignment.MiddleLeft,"Merkez başlığı genişlik veya hizası yanlış: "+heading.AccessibleName+" "+heading.Width+" / "+tab.Width);}
            var youtube=sections.Single(x=>x.Controls.OfType<Button>().First().AccessibleName=="YouTube / Google OAuth bölümünü aç veya kapat");Check(!youtube.Expanded,"YouTube ayrıntıları başlangıçta açık kaldı.");youtube.SetExpanded(true);Application.DoEvents();Check(All(youtube).OfType<Button>().Any(b=>b.Text=="OAuth bilgilerini kaydet"&&b.Visible),"OAuth işlemleri açılmadı.");youtube.SetExpanded(false);
            var local=sections.Single(x=>x.Controls.OfType<Button>().First().AccessibleName=="Yerel motorlar bölümünü aç veya kapat");local.SetExpanded(true);Application.DoEvents();var parentHeading=local.Controls.OfType<Button>().First();foreach(var child in All(local).OfType<StudioDisclosure>().Where(x=>x.Visible)){var heading=child.Controls.OfType<Button>().First();Check(heading.PointToScreen(Point.Empty).X==parentHeading.PointToScreen(Point.Empty).X,"Alt başlık girintili kaldı.");}
            var stock=sections.Single(x=>x.Controls.OfType<Button>().First().AccessibleName=="Görüntü ve araştırma kaynakları bölümünü aç veya kapat");foreach(var section in sections.Where(x=>x.Parent==stock.Parent))section.SetExpanded(section==stock);form.PerformLayout();Application.DoEvents();var cards=All(stock).OfType<StudioCard>().ToArray();Check(cards.Length>=3,"Hizmet kartları bulunamadı.");foreach(var card in cards){Check(card.Region==null,"Kapsayıcı kartın içeriği pencere bölgesiyle kırpılıyor.");foreach(Control child in card.Controls)Check(child.Bottom<=card.ClientSize.Height,"Kart düğmesi veya durum satırı kart altından taşıyor.");for(Control? child=card;child?.Parent is Control parent&&parent!=stock;child=parent)Check(child.Bottom<=parent.ClientSize.Height,"Kartın altı kapsayıcı tarafından kesiliyor: "+parent.GetType().Name+" "+child.Bottom+"/"+parent.ClientSize.Height);}
            form.BringToFront();form.Activate();form.Refresh();Application.DoEvents();Thread.Sleep(100);using var shot=new Bitmap(form.Width,form.Height);using(var g=Graphics.FromImage(shot))g.CopyFromScreen(form.Location,Point.Empty,form.Size);shot.Save(Path.Combine(root,"connections-stock-live.png"));
            typeof(StudioForm).GetMethod("Navigate",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(form,["Videolarım"]);Application.DoEvents();var groups=All(form).OfType<Button>().Where(b=>b.AccessibleName?.StartsWith("Proje grubu:")==true).ToArray();Check(groups.Length==3&&groups.All(b=>b.TextAlign==ContentAlignment.MiddleLeft&&b.Font.Bold&&!b.Font.Italic),"Eski proje grubu başlığı ortak stile bağlı değil.");form.Close();
        });

        Test("BUTTON-ROUNDED-LIVE-CORNERS", "Gerçek pencere çiziminde düğme köşeleri siyah olmaz; durumlar ve şifre gözü aynı zemini kullanır",()=>{
            using var form=new Form{ClientSize=new Size(720,420),StartPosition=FormStartPosition.Manual,Location=new Point(40,40),TopMost=true,BackColor=Theme.Bg};var board=new Panel{BackColor=Theme.Sidebar,Dock=DockStyle.Fill};var flow=new FlowLayoutPanel{BackColor=Color.Transparent,Dock=DockStyle.Fill,Padding=new Padding(22),WrapContents=true};board.Controls.Add(flow);form.Controls.Add(board);var primary=(StudioButton)Theme.Button("Giriş yap",()=>{},true,210);primary.BackColor=Theme.Teal;flow.Controls.Add(primary);var secondary=(StudioButton)Theme.Button("Bölümleri hazırla",()=>{},false,260);flow.Controls.Add(secondary);var field=Theme.Input("test");field.UseSystemPasswordChar=true;flow.Controls.Add(field);var eye=new PasswordEye(field){Size=new Size(44,36)};flow.Controls.Add(eye);ModernScrollbars.Apply(form);form.Show();form.Activate();Application.DoEvents();
            void Capture(string name){form.Refresh();Application.DoEvents();Thread.Sleep(60);using var frame=new Bitmap(form.Width,form.Height);using(var g=Graphics.FromImage(frame))g.CopyFromScreen(form.Location,Point.Empty,form.Size);frame.Save(Path.Combine(root,"rounded-live-"+name+".png"));foreach(var b in new[]{primary,secondary,eye}){var location=b.PointToScreen(Point.Empty);foreach(var point in new[]{new Point(0,0),new Point(2,2),new Point(b.Width-2,1),new Point(1,b.Height-2),new Point(b.Width-2,b.Height-2)}){int x=location.X-form.Left+point.X,y=location.Y-form.Top+point.Y;var pixel=frame.GetPixel(x,y);Check(pixel.GetBrightness()>.06f&&pixel.GetBrightness()<.65f,"Siyah köşe: "+name+" "+b.Text+" "+point+" "+pixel);}}}
            Capture("normal");typeof(StudioButton).GetMethod("OnMouseEnter",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(primary,[EventArgs.Empty]);Capture("hover");primary.Completed=true;primary.CurrentStep=true;Capture("completed-current");primary.Enabled=false;Capture("disabled");Check(primary.Region!=null&&!primary.Region.IsVisible(0,0),"Düğmenin keskin köşesi tıklama bölgesinde kaldı.");form.Close();
        });

        Test("AUDIT-VOICE-FALLBACK-CACHE", "Yedek anlatıcı seçili sesin önbelleğine dönüşmez; kurulu olmayan Windows sesi sessizce değiştirilmez",()=>{
            var st=New("audit-fallback-voice");var audio=Path.Combine(st.Root,"narration.wav");var timing=Path.Combine(st.Root,"timing.json");File.WriteAllText(audio,"fixture-audio");File.WriteAllText(timing,"[]");File.WriteAllText(audio+".actual-provider","Windows / ücretsiz");var settings=new Settings{VoiceProvider="Google Cloud TTS",GoogleTtsVoice="selected-voice"};var e=new Episode{Creation=new CreationFlow()};var script=new VideoScript{Scenes=[new Scene{Narration="Anlatım değişmedi."}]};e.Plan=new ContentPlan{Long=script};WorkflowMedia.FreezeAudio(e,script,new AssetVideo{NarrationFile=audio,NarrationTimingFile=timing},settings);Check(!WorkflowMedia.CanReuseAudio(e,script,settings),"Yedek ses seçili Google sesi gibi donduruldu.");var f=e.Creation!;f.FrozenAudio=audio;f.FrozenTiming=timing;f.FrozenTextHash=WorkflowMedia.SpokenHash(script);f.FrozenVoiceHash=NarrationFlow.Fingerprint(new VideoScript(),settings);f.FrozenAudioHash=Reliability.FileHash(audio);Check(!WorkflowMedia.CanReuseAudio(e,script,settings),"Önceki sürümün dondurulmuş yedek sesi tekrar kullanıldı.");File.Delete(audio+".actual-provider");Check(WorkflowMedia.CanReuseAudio(e,script,settings),"Seçili sesin geçerli önbelleği kayboldu.");IReadOnlyList<(string Id,string Name)> voices=new[]{("female","Kadın"),("male","Erkek")};Check(LocalVoice.SelectVoiceId("male",voices)=="male"&&LocalVoice.SelectVoiceId("",voices)=="female","Windows ses seçimi yanlış.");Reject(()=>LocalVoice.SelectVoiceId("not-installed",voices));Reject(()=>LocalVoice.SelectVoiceId("male",Array.Empty<(string Id,string Name)>()));
        });
        Test("AUDIT-SOURCE-SECRET-GATE", "Kaynak aktarımı kişisel tokenı engeller; Firebase istemci yapılandırmasını özel anahtarla karıştırmaz",()=>{
            string token="gh"+"p_"+new string('A',36);Reject(()=>SourceSafety.ValidateText("src/config.cs","var credential=\""+token+"\";"));string google="AI"+"za"+new string('B',35);Reject(()=>SourceSafety.ValidateText("src/config.cs",google));SourceSafety.ValidateText("src/Assets/pairing-firebase.json",JsonSerializer.Serialize(new{apiKey=google,project="fixture-project"}));Reject(()=>SourceSafety.ValidateText("src/Assets/pairing-firebase.json",JsonSerializer.Serialize(new{apiKey=google,project="fixture-project"}),[google]));Reject(()=>SourceSafety.ValidateText("src/Assets/pairing-firebase.json",JsonSerializer.Serialize(new{apiKey=google,project="fixture-project",private_key="not-allowed"})));string secret="personal-credential-value";try{SourceSafety.ValidateText("src/example.cs",secret,[secret]);throw new Exception("Gizli bilgi geçişi engellenmedi.");}catch(InvalidOperationException ex){Check(!ex.Message.Contains(secret),"Hata anahtarı çıktıya yazdı.");}
            string pem="-----BEGIN PRIVATE KEY-----\n"+new string('A',96)+"\n-----END PRIVATE KEY-----";Reject(()=>SourceSafety.ValidateText("src/account.cs",JsonSerializer.Serialize(new{private_key=pem})));
            var dir=Path.Combine(root,"source-gate-fixture");Directory.CreateDirectory(Path.Combine(dir,"src"));File.WriteAllText(Path.Combine(dir,"src","safe.cs"),"class Safe {}");File.WriteAllText(Path.Combine(dir,"src","bad.cs"),token);Reject(()=>SourceSafety.ValidateFiles(dir));File.WriteAllText(Path.Combine(dir,"src","bad.cs"),"class Clean {}");SourceSafety.ValidateFiles(dir);
        });
        Test("AUDIT-ASSISTANT-PRODUCTION-INTENT", "Üretim hakkında soru veya olumsuz istek üretim başlatma izni sayılmaz",()=>{
            foreach(string text in new[]{"Üretimi başlatma","Hatayı nasıl düzeltebilirim?","Yeniden deneme","Devam etme","Bu hata düzeltilebilir mi?","Üretimi neden başlatamıyorum?"})Check(!AssistantActions.ProductionRequested(text),"Üretim izni yanlış algılandı: "+text);
            foreach(string text in new[]{"Hatayı düzelt ve yeniden dene","Üretimi başlat","Devam et","Yeniden dener misin?"})Check(AssistantActions.ProductionRequested(text),"Açık üretim isteği anlaşılamadı: "+text);
        });
        Test("AUDIT-MANUAL-VISUAL-AFTER-AI", "AI yerine seçilen kullanıcı görseli eski AI işaretiyle kalite değişiminde silinmez",()=>{
            var scene=new Scene{StockFile="old-ai.png",AiImageHash="old-hash",AiImageQuality="Hızlı"};SceneVideoChoice.Apply(scene,"user-picture.png",null);var e=new Episode{Creation=new CreationFlow{SceneImageQuality="Hızlı"},AiStoryLocal=true,Plan=new ContentPlan{Long=new VideoScript{Scenes=[scene]}}};WorkflowMedia.ChangeImageQuality(e,"Ultra sinematik");Check(scene.StockFile=="user-picture.png"&&scene.AiImageHash==""&&scene.AiImageQuality=="","Kullanıcı görseli AI sanılarak kaldırıldı.");
        });
        Test("AUDIT-VISUAL-CACHE-QUALITY", "Genel kalite değişimi ve bozuk AI dosyası eski görseli hazır saymaz; kullanıcı dosyası korunur",()=>{
            var st=New("audit-visual-cache");st.Settings.CoverLocalQuality="Ultra sinematik";var e=new Episode{Creation=new CreationFlow{AiVisuals=true,Mode="auto"},ArtworkSettings=new Settings{CoverLocalQuality="Hızlı"},AiStoryLocal=true};var file=Path.Combine(st.Root,"scene.png");File.WriteAllText(file,"original-fixture");File.WriteAllText(file+".sha",Reliability.FileHash(file));var scene=new Scene{StockFile=file,AiImageHash="prompt-hash",AiImageQuality="Hızlı"};Check(StoryImages.LocalSettings(e,VideoSettings.For(st.Settings,e),st.Settings).CoverLocalQuality=="Ultra sinematik","Genel kalite eski proje ayarına takıldı.");Check(!WorkflowMedia.VisualReady(e,scene,st.Settings),"Eski kalite hazır sayıldı.");scene.AiImageQuality="Ultra sinematik";Check(WorkflowMedia.VisualReady(e,scene,st.Settings),"Geçerli önbellek reddedildi.");File.WriteAllText(file,"changed-fixture");Check(!WorkflowMedia.VisualReady(e,scene,st.Settings),"Bozuk AI dosyası hazır sayıldı.");scene.AiImageHash="";Check(WorkflowMedia.VisualReady(e,scene,st.Settings),"Kullanıcının dosyası AI kalite filtresiyle kaldırıldı.");
        });
        Test("AUDIT-ASSISTANT-UPLOAD-UNDO", "Asistan eski geri alma kaydıyla yüklenmiş projeyi değiştiremez",()=>{
            var st=New("audit-upload-undo");var e=new Episode{Creation=new CreationFlow{Mode="manual",CoverChoice="none"}};st.Episodes.Add(e);var agent=new AssistantActions(st,new Engine(st),()=>e,_=>{});using var args=JsonDocument.Parse("{}");agent.Execute("set_storyboard",args.RootElement,e.Id,false,"Sahneleri ayarla",CancellationToken.None).GetAwaiter().GetResult();e.Videos.Add(new AssetVideo{YouTubeId="already-uploaded"});string before=JsonSerializer.Serialize(e,Json.Options);Reject(()=>agent.Execute("undo",args.RootElement,e.Id,false,"Geri al",CancellationToken.None).GetAwaiter().GetResult());Check(JsonSerializer.Serialize(e,Json.Options)==before,"Yüklenmiş proje değişti.");
        });
        Test("AUDIT-STORYBOARD-CANCEL-ATOMIC", "Ek Shorts bölümlemesi iptal olursa önceki plan ve anlatım aynen korunur",()=>{
            var st=New("audit-plan-atomic");st.Settings.ProviderOrder="OpenRouter";st.Vault.Set("OPENROUTER_API_KEY","fixture-only");string text=string.Join(" ",Enumerable.Range(1,60).Select(i=>"kelime"+i));var f=new CreationFlow{Normal=true,TextMode="own",OwnText=text,ApprovedHash=TimedNarration.TextHash(text),AiVisuals=true,Mode="auto",Split=true,CoverChoice="none",Subtitles=false,ExtraShortCount=1,ExtraShortTexts=[text]};f.ExtraApprovalHash=f.ExtrasHash;var e=new Episode{Topic="Önceki proje",Creation=f,IncludeLong=false,ShortCount=1,UserNarration=new UserNarrationOptions{Shorts=["Önceki metin"]},Plan=new ContentPlan{Shorts=[new VideoScript{Scenes=[new Scene{Narration="Önceki metin"}]}]}};st.Episodes.Add(e);string before=JsonSerializer.Serialize(new{e.Plan,e.UserNarration,e.IncludeLong,e.ShortCount,f.PlannedTextHash},Json.Options);using var stop=new CancellationTokenSource();int calls=0;using var http=new HttpClient(new Handler(_=>{calls++;if(calls>1){stop.Cancel();throw new OperationCanceledException(stop.Token);}return Task.FromResult(Ok("{\"choices\":[{\"message\":{\"content\":\"{\\\"Ends\\\":[12,24,36,48,60]}\"}}]}"));}));bool cancelled=false;try{WorkflowMedia.Build(st,e,new Services(st,_=>{},http),stop.Token).GetAwaiter().GetResult();}catch(OperationCanceledException){cancelled=true;}Check(cancelled&&calls==2,"İptal senaryosu çalışmadı.");Check(JsonSerializer.Serialize(new{e.Plan,e.UserNarration,e.IncludeLong,e.ShortCount,f.PlannedTextHash},Json.Options)==before,"İptal önceki plan/anlatımı kısmen ezdi.");
        });
        Test("AUDIT-ASSISTANT-ERROR-PRIVACY", "Araç hatası paylaşım kapalıyken özel proje metnini model isteğine sızdırmaz",()=>{
            var st=New("audit-error-privacy");st.Settings.ProviderOrder="OpenRouter";st.Vault.Set("OPENROUTER_API_KEY","fixture-only");var e=new Episode{Creation=new CreationFlow{Mode="manual",CoverChoice="none"}};st.Episodes.Add(e);var agent=new AssistantActions(st,new Engine(st),()=>e,_=>{}){FlushEditor=()=>throw new InvalidOperationException("PRIVATE-ERROR-CONTENT")};int calls=0;bool leaked=false;using var http=new HttpClient(new Handler(async req=>{var body=await req.Content!.ReadAsStringAsync();leaked|=body.Contains("PRIVATE-ERROR-CONTENT");string answer=++calls==1?JsonSerializer.Serialize(new{answer="Ayarları deniyorum.",tool=new{name="set_storyboard",args=new{}}}):JsonSerializer.Serialize(new{answer="İşlem uygulanamadı."});return Ok(JsonSerializer.Serialize(new{choices=new[]{new{message=new{content=answer}}}}));}));using var form=new Form{Width=560,Height=750};using var chat=new ChatAssistant(st,()=>e,()=>"Stüdyo",agent,()=>new Services(st,_=>{},http)){Dock=DockStyle.Fill};form.Controls.Add(chat);form.Show();Application.DoEvents();((TextBox)typeof(ChatAssistant).GetField("input",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(chat)!).Text="Sahneleri ayarla";var task=(Task)typeof(ChatAssistant).GetMethod("Send",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(chat,null)!;var until=DateTime.UtcNow.AddSeconds(10);while(!task.IsCompleted&&DateTime.UtcNow<until){Application.DoEvents();Thread.Sleep(10);}Check(task.IsCompleted,"Sohbet tamamlanmadı.");task.GetAwaiter().GetResult();Check(calls==2&&!leaked,"Özel hata metni paylaşım kapalıyken modele gönderildi.");var conversation=(RichTextBox)typeof(ChatAssistant).GetField("conversation",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(chat)!;Check(conversation.Text.Contains("PRIVATE-ERROR-CONTENT"),"Yerel hata açıklaması sohbet yanıtıyla silindi.");
        });
        Test("SCENE-IMAGE-QUALITY", "Proje kalite seçimi motora gider; AI görseli yenilenirken kullanıcı dosyası ve anlatım korunur",()=>{
            var st=New("scene-image-quality");var f=new CreationFlow{SceneImageQuality="Dengeli",FrozenAudio="korunacak.wav"};var ai=new Scene{Narration="Birinci sahne",StockFile="eski-ai.png",AiImageHash="eski-hash"};var own=new Scene{Narration="İkinci sahne",StockFile="kullanici.png",UserLocal=true};var e=new Episode{Creation=f,AiStoryLocal=true,Plan=new ContentPlan{Long=new VideoScript{Scenes=[ai,own]}}};WorkflowMedia.ChangeImageQuality(e,"Ultra sinematik");Check(StoryImages.LocalSettings(e,new Settings{CoverLocalQuality="Hızlı"}).CoverLocalQuality=="Ultra sinematik","Projenin kalite seçimi uygulanmadı.");Check(ai.StockFile==""&&ai.AiImageHash==""&&own.StockFile=="kullanici.png"&&f.FrozenAudio=="korunacak.wav"&&ai.Narration=="Birinci sahne","Kalite değişimi kullanıcı dosyasını/anlatımı bozdu veya eski AI görselini kullandı.");Reject(()=>WorkflowMedia.ChangeImageQuality(e,"uydurma"));
        });
        Test("ASSISTANT-SETTINGS-TOOLS", "Asistan ayarı uygular/geri alır, bilinmeyen kod-yayın araçlarını ve yanlış projeyi reddeder",()=>{
            var st=New("assistant-settings");var e=new Episode{Topic="Gizli proje başlığı",Creation=new CreationFlow{Normal=false,TextMode="own",OwnText="Gizli anlatım",Mode="manual",AiVisuals=false,Subtitles=true,SubtitleStyle="Sade beyaz",CoverChoice="none"}};st.Episodes.Add(e);Episode? selected=e;var engine=new Engine(st);var agent=new AssistantActions(st,engine,()=>selected,_=>{});string original=JsonSerializer.Serialize(e.Creation);
            string Run(string name,string json="{}",string human="Ayarları düzelt"){using var args=JsonDocument.Parse(json);return agent.Execute(name,args.RootElement,e.Id,false,human,CancellationToken.None).GetAwaiter().GetResult();}
            Check(!agent.Inspect(e,false).Contains("Gizli")&&agent.Inspect(e,true).Contains("Gizli"),"Paylaşım kapalıyken metin/başlık sızdı.");Run("set_storyboard");Check(e.Creation!.AutomaticStoryboard&&e.Creation.Split==true,"Sahne ayarı uygulanmadı.");Run("undo");Check(JsonSerializer.Serialize(e.Creation)==original,"Asistan ayarı geri alınmadı.");
            Run("set_subtitles","{\"enabled\":true,\"style\":\"Koyu kutulu\"}");Check(e.Creation.SubtitleStyle=="Koyu kutulu","Altyazı ayarı uygulanmadı.");e.Creation.OwnText+=" değişti";Reject(()=>Run("undo"));
            foreach(string tool in new[]{"write_file","run_shell","delete_project","publish","upload","set_api_key"})Reject(()=>Run(tool));Check(st.Episodes.Contains(e),"Proje silindi.");Reject(()=>Run("set_music","{\"enabled\":true,\"volume\":101}"));Reject(()=>Run("set_voice","{\"provider\":\"Google Cloud TTS\",\"id\":\"uydurma\"}"));Reject(()=>Run("retry_production","{}","Durum nedir?"));
            selected=new Episode();Reject(()=>Run("set_storyboard"));selected=e;engine.External(_=>{Reject(()=>Run("set_storyboard"));return Task.CompletedTask;}).GetAwaiter().GetResult();
            using var stop=new CancellationTokenSource();stop.Cancel();bool cancelled=false;try{using var args=JsonDocument.Parse("{}");agent.Execute("set_storyboard",args.RootElement,e.Id,false,"",stop.Token).GetAwaiter().GetResult();}catch(OperationCanceledException){cancelled=true;}Check(cancelled,"İptal edilen işlem ayar değiştirdi.");
        });
        Test("ASSISTANT-EDITOR-SYNC", "Asistan açık editördeki kaydedilmemiş ayarı korur ve ekran yenilemesi yeni ayarı ezmez",()=>{
            var st=New("assistant-editor-sync");string text="Onaylı özgün anlatım.";var f=new CreationFlow{Normal=false,TextMode="own",OwnText=text,ApprovedHash=TimedNarration.TextHash(text),AiVisuals=true,Mode="auto",Subtitles=true,SubtitleStyle="Sade beyaz",CoverChoice="none",Reached=1};var e=new Episode{Topic="Editör eşleme",Creation=f,WizardStep=1};st.Episodes.Add(e);using var form=new StudioForm(st);form.Show();Application.DoEvents();IEnumerable<Control> All(Control c)=>c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(All(x)));var group=All(form).OfType<StudioDisclosure>().Single(x=>x.Controls.OfType<Button>().Any(b=>b.AccessibleName=="Ses ve müzik bölümünü aç veya kapat"));group.SetExpanded(true);var volume=All(form).OfType<NumericUpDown>().Single(x=>x.AccessibleName=="Müzik seviyesi • yüzde");volume.Value=23;
            var agent=(AssistantActions)typeof(StudioForm).GetField("assistantActions",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(form)!;using var args=JsonDocument.Parse("{\"enabled\":true,\"style\":\"Koyu kutulu\"}");agent.Execute("set_subtitles",args.RootElement,e.Id,false,"Altyazıyı koyu kutulu yap",CancellationToken.None).GetAwaiter().GetResult();Application.DoEvents();Check(e.Creation!.SubtitleStyle=="Koyu kutulu"&&Math.Abs(e.ProductionSettings!.MusicVolume-.23)<.001,"Editör yenilemesi asistan ayarını veya kaydedilmemiş müzik ayarını ezdi.");form.Close();
        });
        Test("ASSISTANT-AUTO-REPAIR", "Asistan hata halinde ayarları düzeltir; kotayı, metin onayını ve çıktı dosyalarını değiştirmez",()=>{
            var st=New("assistant-auto-repair");st.Settings.GoogleTtsVoice="seçili-ses";var f=new CreationFlow{Normal=false,TextMode="own",OwnText="Anlatım",Mode="auto",AiVisuals=true,Split=false,Subtitles=true,SubtitleStyle="bozuk",CoverChoice="none"};var e=new Episode{Topic="Ayar onarımı",Creation=f,Error="Motor bağlantısı hatası",ProductionSettings=new Settings{GoogleTtsVoice="eski-ses"}};st.Episodes.Add(e);var engine=new Engine(st);var agent=new AssistantActions(st,engine,()=>e,_=>{});int notices=0;agent.Notice+=_=>notices++;string settings=JsonSerializer.Serialize(st.Settings);agent.Observe();Check(f.Split==true&&f.SubtitleStyle=="Sade beyaz"&&e.ProductionSettings.GoogleTtsVoice=="seçili-ses","Bilinen ayar hatası onarılmadı.");Check(!f.TextApproved&&e.Videos.Count==0&&!engine.Busy&&JsonSerializer.Serialize(st.Settings)==settings,"Onarım izin veya çıktıları değiştirdi.");agent.Observe();int after=notices;agent.Observe();Check(after==notices,"Hata gözleminde sonsuz döngü var.");agent.SetAutoRepair(false);f.SubtitleStyle="bozuk";e.Error="yeni hata";agent.Observe();Check(f.SubtitleStyle=="bozuk","Otomatik onarım kapalıyken çalıştı.");
        });
        Test("ASSISTANT-CHAT-ACTION-LOOP", "Sohbet gerçek araç sonucu alıp ayarı değiştirir ve paylaşılmayan metni AI'ya göndermez",()=>{
            var st=New("assistant-chat-loop");st.Settings.ProviderOrder="OpenRouter";st.Vault.Set("OPENROUTER_API_KEY","fixture-only");var e=new Episode{Topic="PRIVATE-TOPIC-SECRET",Creation=new CreationFlow{Normal=false,TextMode="own",OwnText="PRIVATE-NARRATION-SECRET",Mode="manual",CoverChoice="none"}};st.Episodes.Add(e);var engine=new Engine(st);var agent=new AssistantActions(st,engine,()=>e,_=>{});int calls=0;
            using var http=new HttpClient(new Handler(async req=>{string body=await req.Content!.ReadAsStringAsync();Check(!body.Contains("PRIVATE-NARRATION-SECRET")&&!body.Contains("PRIVATE-TOPIC-SECRET"),"Sohbette paylaşım kapalıyken içerik gönderildi.");calls++;string answer=calls==1?JsonSerializer.Serialize(new{answer="Sahne ayarını uyguluyorum.",tool=new{name="set_storyboard",args=new{}}}):JsonSerializer.Serialize(new{answer="5–10 sahneli ayar uygulandı; mevcut video değişmedi."});return Ok(JsonSerializer.Serialize(new{choices=new[]{new{message=new{content=answer}}}}));}));
            using var form=new Form{Width=580,Height=760,Text="TubeFlow — uygulama asistanı testi",TopMost=true};var chat=new ChatAssistant(st,()=>e,()=>"Stüdyo",agent,()=>new Services(st,_=>{},http)){Dock=DockStyle.Fill};form.Controls.Add(chat);form.Show();Application.DoEvents();var input=(TextBox)typeof(ChatAssistant).GetField("input",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(chat)!;input.Text="5–10 sahne ayarla";var task=(Task)typeof(ChatAssistant).GetMethod("Send",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(chat,null)!;var until=DateTime.UtcNow.AddSeconds(15);while(!task.IsCompleted&&DateTime.UtcNow<until){Application.DoEvents();Thread.Sleep(10);}Check(task.IsCompleted,"Sohbet araç döngüsü bitmedi.");task.GetAwaiter().GetResult();Check(calls==2&&e.Creation!.AutomaticStoryboard,"Sohbet ayarı uygulamadan yanıt verdi.");form.Refresh();Application.DoEvents();Thread.Sleep(250);using var shot=new Bitmap(form.Width,form.Height);using(var g=Graphics.FromImage(shot))g.CopyFromScreen(form.Location,Point.Empty,shot.Size);shot.Save(Path.Combine(st.Root,"assistant-actions-live.png"));
        });
        Test("LOCAL-ENGINE-HARDWARE-ADAPTATION", "Intel, NVIDIA, AMD ve CPU seçimi klasör adına değil kullanılabilir aygıta uyar",()=>{
            var intel=EngineHardware.Order(new[]{("nvidia-folder",new[]{new EngineHardware.Capability("CPU","CPU")}), ("intel-folder",new[]{new EngineHardware.Capability("XPU","Intel Arc"),new EngineHardware.Capability("CPU","CPU")})});Check(intel[0].Folder=="intel-folder"&&intel[0].Backend=="XPU","Intel GPU yerine NVIDIA klasörü seçildi.");
            var hybrid=EngineHardware.Order(new[]{("intel",new[]{new EngineHardware.Capability("XPU","Intel")}), ("nvidia",new[]{new EngineHardware.Capability("CUDA","RTX")})});Check(hybrid[0].Backend=="CUDA","Intel işlemcili NVIDIA bilgisayarda GPU sırası yanlış.");
            Check(EngineHardware.Order(new[]{("amd",new[]{new EngineHardware.Capability("ROCm","AMD")})})[0].Backend=="ROCm","AMD kayboldu.");Check(EngineHardware.Arguments("DirectML").Contains("--directml")&&EngineHardware.Arguments("CPU").SequenceEqual(new[]{"--cpu"})&&EngineHardware.Arguments("XPU").Contains("--lowvram"),"Motor başlatma parametreleri donanıma uymuyor.");Check(intel.Last().Backend=="CPU","CPU yedeği GPU önüne geçti.");
        });
        Test("STORYBOARD-SCENES-AND-FALLBACK", "AI 5–10 sahne planlar; tek sahne, bozuk yanıt ve bağlantı hatası anlatımı kaybetmez",()=>{
            string text=string.Join(" ",Enumerable.Range(1,207).Select(i=>"kelime"+i+(i%20==0?".":"")));
            var seven=StoryboardSections.Plan(text,(_,_,_)=>Task.FromResult("{\"Ends\":[30,60,90,120,150,180,207]}"),CancellationToken.None,_=>{}).GetAwaiter().GetResult();Check(seven.Count==7&&seven[^1]==207,"Yedi sahne planlanmadı.");
            foreach(string raw in new[]{"{\"Ends\":[207]}","{}","{\"Ends\":[30,20,207]}"}){var ends=StoryboardSections.Plan(text,(_,_,_)=>Task.FromResult(raw),CancellationToken.None,_=>{}).GetAwaiter().GetResult();Check(ends.Count is >=5 and <=10&&ends[^1]==207&&ends.SequenceEqual(ends.Distinct().Order()),"Bozuk AI yanıtı tek sahneye düştü.");}
            foreach(int words in new[]{1,4,5,6,20,207,500}){var ends=StoryboardSections.Fallback(string.Join(" ",Enumerable.Range(1,words)));Check(ends.Count==StoryboardSections.Target(words)&&ends[^1]==words,"Kısa/uzun metin bölümleme bozuk.");}
            using var stop=new CancellationTokenSource();stop.Cancel();bool cancelled=false;try{StoryboardSections.Plan(text,(_,_,ct)=>Task.FromCanceled<string>(ct),stop.Token,_=>{}).GetAwaiter().GetResult();}catch(OperationCanceledException){cancelled=true;}Check(cancelled,"İptal yedek plana dönüştü.");
        });
        Test("WORKFLOW-AUTO-STORYBOARD", "Eski tek bölüm AI projesi metin korunarak yedi ayrı sahneye dönüştürülür",()=>{
            var st=New("auto-storyboard");st.Settings.ProviderOrder="OpenRouter";st.Vault.Set("OPENROUTER_API_KEY","fixture-only");var text=string.Join(" ",Enumerable.Range(1,207).Select(i=>"kelime"+i));var f=new CreationFlow{Normal=false,TextMode="own",OwnText=text,ApprovedHash=TimedNarration.TextHash(text),AiVisuals=true,Mode="auto",Split=false,CoverChoice="none",Subtitles=false};var e=new Episode{Topic="Sahne testi",Creation=f};
            using var http=new HttpClient(new Handler(_=>Task.FromResult(Ok("{\"choices\":[{\"message\":{\"content\":\"{\\\"Ends\\\":[30,60,90,120,150,180,207]}\"}}]}"))));WorkflowMedia.Build(st,e,new Services(st,_=>{},http),CancellationToken.None).GetAwaiter().GetResult();var scenes=e.Plan!.Shorts[0].Scenes;Check(scenes.Count==7&&string.Concat(scenes.Select(s=>s.Narration))==text,"Metin kayboldu veya tek sahne kaldı.");Check(scenes.Select(s=>s.Narration).Distinct().Count()==7,"Sahne anlatımları tekrar ediyor.");
        });
        Test("PROJECT-VOICE-CHOICE", "Eski proje genel sesin son seçimini kullanır, projedeki açık seçim korunur ve önbellek geçersizleşir",()=>{
            var defaults=new Settings{VoiceProvider="Google Cloud TTS",GoogleTtsVoice="tr-TR-Chirp3-HD-Algieba"};var e=new Episode{Creation=new CreationFlow(),ProductionSettings=new Settings{VoiceProvider="Google Cloud TTS",GoogleTtsVoice="tr-TR-Chirp3-HD-Achernar"}};
            var actual=VideoSettings.For(defaults,e);Check(actual.GoogleTtsVoice==defaults.GoogleTtsVoice,"Genel ses eski proje tarafından ezildi.");Check(NarrationFlow.Fingerprint(new VideoScript(),actual)!=NarrationFlow.Fingerprint(new VideoScript(),e.ProductionSettings),"Eski anlatıcı önbelleği geçerli kaldı.");e.Creation.UseProjectVoice=true;Check(VideoSettings.For(defaults,e).GoogleTtsVoice=="tr-TR-Chirp3-HD-Achernar","Projede seçilen ses değiştirildi.");Check(e.ProductionSettings.GoogleTtsVoice=="tr-TR-Chirp3-HD-Achernar","Çözümleme kayıtlı projeyi değiştirdi.");
        });
        Test("COMPLETED-STEP-DARK-FILL", "Onaylı adım koyu temada devre dışı olsa da ayırt edilir dolgusunu korur",()=>{
            using var form=new Form{Text="TubeFlow — onay rengi testi",ClientSize=new Size(600,170),TopMost=true,StartPosition=FormStartPosition.CenterScreen,BackColor=Theme.Bg};
            var done=new StudioButton{Text="✓ Kaynak ve Metin",Completed=true,Enabled=false,Tag="wizard-step",Location=new Point(20,45),Size=new Size(260,58)};var pending=new StudioButton{Text="3. Üretim",Location=new Point(300,45),Size=new Size(260,58)};form.Controls.AddRange([done,pending]);form.Show();form.Activate();Application.DoEvents();form.Refresh();Thread.Sleep(250);
            using var shot=new Bitmap(form.Width,form.Height);using(var g=Graphics.FromImage(shot))g.CopyFromScreen(form.Location,Point.Empty,shot.Size);foreach(int y in new[]{10,done.Height-10}){var p=done.PointToScreen(new Point(done.Width-25,y));var color=shot.GetPixel(p.X-form.Left,p.Y-form.Top);Check(color.ToArgb()==Theme.SuccessSurface.ToArgb(),"Onaylı adımın koyu yeşil dolgusu korunmadı: "+color);}shot.Save(Path.Combine(root,"completed-neon-live.png"));
        });
        Test("SIDEBAR-CHAT-VISIBILITY", "Sohbet gerçek pencerede her boyutta ve dar menüde görünür, açılıp kapanır",()=>{
            var st=New("sidebar-chat");using var form=new StudioForm(st);form.Text="TubeFlow — sohbet görünürlük testi";form.TopMost=true;form.Show();form.Activate();Application.DoEvents();
            IEnumerable<Control> All(Control c)=>c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(All(x)));
            var sideChat=All(form).OfType<Button>().Single(x=>x.AccessibleName=="TubeFlow sohbet yardımcısını aç");var railChat=All(form).OfType<Button>().Single(x=>x.AccessibleName=="Hızlı sohbet");Button chat=sideChat;var toggle=All(form).OfType<Button>().Single(x=>x.AccessibleName=="Yan menüyü aç veya kapat");
            void VisibleChat(string state){Application.DoEvents();Check(chat.Visible,"Sohbet gizli: "+state);var point=chat.PointToScreen(new Point(chat.Width/2,chat.Height/2));for(Control child=chat;child.Parent is Control parent;child=parent){Check(parent.ClientRectangle.Contains(child.Bounds),"Sohbet kapsayıcı dışında: "+state+" "+child.Bounds+" / "+parent.ClientRectangle);Check(parent.GetChildAtPoint(parent.PointToClient(point))==child,"Başka denetim sohbeti örtüyor: "+state);}Check(Screen.FromControl(form).WorkingArea.Contains(point),"Sohbet ekran dışında: "+state);}
            foreach(var size in new[]{new Size(1440,950),new Size(900,600),new Size(1200,800)}){form.Size=size;VisibleChat(size.ToString());toggle.PerformClick();chat=railChat;VisibleChat("dar menü");Check(chat is CortexButton,"Dar menü Cortex simgesine geçmedi.");toggle.PerformClick();chat=sideChat;VisibleChat("açık menü");Check(chat is CortexButton&&chat.Text=="Cortex","Cortex düğmesi geri gelmedi.");}
            form.WindowState=FormWindowState.Maximized;VisibleChat("tam ekran");chat.PerformClick();Application.DoEvents();var host=typeof(StudioForm).GetField("assistantHost",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(form) as Control;Check(host?.Visible==true,"Sohbet açılmadı.");chat.PerformClick();Application.DoEvents();Check(!host!.Visible,"Sohbet kapanmadı.");VisibleChat("sohbet kapandı");
            form.Refresh();Application.DoEvents();Thread.Sleep(250);using var shot=new Bitmap(form.Width,form.Height);using(var g=Graphics.FromImage(shot))g.CopyFromScreen(form.Location,Point.Empty,shot.Size);shot.Save(Path.Combine(st.Root,"sidebar-chat-live.png"));form.Close();
        });
        Test("WORKFLOW-SOURCE-CHOICES", "İki metin bölümü, tipe bağlı süreler ve açık tema/yan bar",()=>{
            var st=New("workflow-source");var episode=new Episode{Topic="Yeni akış testi",IncludeLong=false,ShortCount=1,Creation=new CreationFlow{OwnText="Kendi özgün metnim",Prompt="Kuşların yardımlaşmasını anlat"}};st.Episodes.Add(episode);
            using var form=new StudioForm(st);form.Width=1440;form.Show();Application.DoEvents();IEnumerable<Control> All(Control c)=>c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(All(x)));
            CheckBox CheckNamed(string name)=>All(form).OfType<CheckBox>().Single(x=>x.AccessibleName==name);
            TextBox TextNamed(string name)=>All(form).OfType<TextBox>().Single(x=>x.AccessibleName==name);
            var own=CheckNamed("KENDİ İÇERİĞİNİ EKLE");var ai=CheckNamed("İÇERİĞİ AI MODELE HAZIRLAT");var ownText=TextNamed("Kendi içerik metnin");var prompt=TextNamed("AI içerik talimatı");Check(!own.Checked&&!ai.Checked&&!ownText.Enabled&&!prompt.Enabled,"Metin yöntemi varsayılan seçildi.");
            own.Checked=true;Check(ownText.Enabled&&!prompt.Enabled&&!ai.Checked,"Kendi metni seçimi diğer bölümü kapatmadı.");ai.Checked=true;Check(!own.Checked&&!ownText.Enabled&&prompt.Enabled&&ownText.Text=="Kendi özgün metnim","AI seçimi önceki metni kaybetti.");own.Checked=true;Check(!ai.Checked&&prompt.Text=="Kuşların yardımlaşmasını anlat","Prompt korunmadı.");
            var numbers=All(form).OfType<NumericUpDown>().ToArray();Check(numbers.Length==2&&!numbers[0].Enabled&&!numbers[1].Enabled,"Tip seçilmeden süre aktif.");CheckNamed("Normal video • yatay 16:9").Checked=true;Check(numbers[0].Enabled&&!numbers[1].Enabled,"Normal video süre ilişkisi yanlış.");CheckNamed("Kısa video • dikey 9:16").Checked=true;Check(!numbers[0].Enabled&&numbers[1].Enabled,"Kısa video süre ilişkisi yanlış.");
            Check(Theme.Bg.GetBrightness()<.15&&Theme.Text.GetBrightness()>.85&&Theme.PreferredFont=="Inter"&&Theme.PreferredFontAvailable&&Theme.BodyFont=="Inter","Koyu tema veya yazı tipi uygulanmadı.");var toggle=All(form).OfType<Button>().Single(b=>b.AccessibleName=="Yan menüyü aç veya kapat");var side=All(form).OfType<SidebarButton>().First(b=>b.AccessibleName=="Ana bölüm: Stüdyo • Üret").Parent!.Parent!;int before=side.Width;toggle.PerformClick();Application.DoEvents();Check(side.Width<before,"Yan menü kapanmadı.");toggle.PerformClick();Application.DoEvents();Check(side.Width==before,"Yan menü açılmadı.");
            using var bitmap=new Bitmap(form.Width,form.Height);form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(Path.Combine(st.Root,"source-light.png"));
        });
        Test("UPLOAD-SECTION-RECOVERY", "Geçersiz AI sınırlarında onaylı metin eksiksiz korunur",()=>{
            const string text="Kur’an okunuyor. Dağlar yükseliyor! Yol devam ediyor.";
            var ends=UploadedSections.Plan(text,(_,_,_)=>Task.FromResult("{\"Ends\":[999]}"),CancellationToken.None,_=>{}).GetAwaiter().GetResult();
            Check(ends.SequenceEqual(UploadedSections.SentenceEnds(text)),"Cümle sınırlarına dönülmedi.");
            var words=System.Text.RegularExpressions.Regex.Matches(text,@"\S+");int begin=0;string joined="";foreach(int end in ends){joined+=text[words[begin].Index..(end<words.Count?words[end].Index:text.Length)];begin=end;}Check(joined==text,"Metin kayboldu.");
            Check(UploadedSections.Fallback(string.Join(" ",Enumerable.Repeat("Bir cümle.",100))).Count==50,"Uzun metin sınırı bozuk.");
            bool cancelled=false;try{UploadedSections.Plan(text,(_,_,_)=>throw new OperationCanceledException(),CancellationToken.None,_=>{}).GetAwaiter().GetResult();}catch(OperationCanceledException){cancelled=true;}Check(cancelled,"İptal yutuldu.");
        });
        Test("UPLOAD-SEMANTIC-MATCH", "Görüntü eşleşmesi dosya sırasından bağımsız, eksik eşleşme reddedilir",()=>{
            var catalog=new List<UploadedMediaMatch.Item>{new(){Id=0,File="mountain.jpg",Description="Açık bir kitap, Kur’an sayfaları"},new(){Id=1,File="book.jpg",Description="Karlı dağlar"}};
            var chosen=UploadedMediaMatch.Parse("{\"Matches\":[{\"Scene\":1,\"File\":0,\"Confidence\":0.95,\"Reason\":\"Kitap anlatımı\"},{\"Scene\":0,\"File\":1,\"Confidence\":0.9,\"Reason\":\"Dağ anlatımı\"}]}",2,catalog);
            Check(chosen[0].File==1&&chosen[1].File==0,"Dosya sırası anlamsal eşleşmeyi ezdi.");
            Reject(()=>UploadedMediaMatch.Parse("{\"Matches\":[{\"Scene\":0,\"File\":-1,\"Confidence\":0.9,\"Reason\":\"Yok\"}]}",1,catalog));
            Reject(()=>UploadedMediaMatch.Parse("{\"Matches\":[{\"Scene\":0,\"File\":0,\"Confidence\":0.2,\"Reason\":\"Zayıf\"}]}",1,catalog));
            var flow=new CreationFlow{Mode="auto",OwnVisuals=true,Files=["a.jpg"]};string hash=flow.Signature;flow.Files.Add("b.jpg");Check(flow.Signature!=hash,"Yeni dosya eski eşleşmeyi geçersiz kılmadı.");
        });
        Test("UPLOAD-BEST-AVAILABLE", "Eksik ve düşük güvenli eşleşmeler mevcut katalog karşılaştırılarak tamamlanır",()=>{
            var catalog=new List<UploadedMediaMatch.Item>{new(){Id=4,File="book.jpg",Description="Karlı dağ"},new(){Id=9,File="mountain.jpg",Description="Açık kitap"},new(){Id=12,File="sky.jpg",Description="Yıldızlı gökyüzü"}};
            var scenes=new List<Scene>{new(){Narration="Dağ",Duration=18},new(){Narration="Bilgi",Duration=7},new(){Narration="Tefekkür",Duration=12}};int calls=0;
            var result=UploadedMediaMatch.Resolve("{\"Matches\":[{\"Scene\":0,\"File\":4,\"Confidence\":0.95,\"Reason\":\"Dağ\"},{\"Scene\":1,\"File\":-1,\"Confidence\":0,\"Reason\":\"Soyut\"}]}",scenes,catalog,(system,prompt,ct)=>{
                calls++;Check(system.Contains("BEST AVAILABLE")&&prompt.Contains("Catalog")&&prompt.Contains("PreviousFile"),"Göreli seçim veya bağlam yok.");
                return Task.FromResult("{\"Matches\":[{\"Scene\":0,\"File\":"+(calls==1?9:12)+",\"Confidence\":0.3,\"Reason\":\"Mevcutlar içinde en yakın sembolik görüntü\"}]}");
            },_=>{},CancellationToken.None).GetAwaiter().GetResult();
            Check(calls==2&&result.Select(x=>x.File).SequenceEqual(new[]{4,9,12}),"İyi seçim korumadı veya eksikler tamamlanmadı.");Check(scenes.Select(x=>x.Duration).SequenceEqual(new[]{18d,7d,12d}),"Konuşma süreleri değişti.");
        });
        Test("UPLOAD-BEST-AVAILABLE-VALIDATION", "Katalog dışı dosya ve bozuk yanıt rastgele seçimle gizlenmez; iptal korunur",()=>{
            var catalog=new List<UploadedMediaMatch.Item>{new(){Id=2,File="a.jpg",Description="Dağ"}};var scenes=new List<Scene>{new(){Narration="Dağ"}};int calls=0;
            Reject(()=>UploadedMediaMatch.Resolve("{}",scenes,catalog,(_,_,_)=>{calls++;return Task.FromResult("{\"Matches\":[{\"Scene\":0,\"File\":99,\"Confidence\":0.9,\"Reason\":\"Yok\"}]}");},_=>{},CancellationToken.None).GetAwaiter().GetResult());Check(calls==2,"Düzeltme denemesi sınırsız veya eksik.");
            using var stop=new CancellationTokenSource();stop.Cancel();bool cancelled=false;try{UploadedMediaMatch.Resolve("{}",scenes,catalog,(_,_,_)=>throw new Exception("Çağrılmamalı"),_=>{},stop.Token).GetAwaiter().GetResult();}catch(OperationCanceledException){cancelled=true;}Check(cancelled,"İptal yutuldu.");
        });
        Test("UPLOAD-UNIQUE-ACROSS-BATCHES", "67 görüntüden 50 sahneye seçimde dosya ikinci kez kullanılamaz",()=>{
            var catalog=Enumerable.Range(0,67).Select(i=>new UploadedMediaMatch.Item{Id=i,File=i+".jpg",Description="İçerik "+i}).ToList();var used=new HashSet<int>();var chosen=new List<UploadedMediaMatch.Choice>();int corrections=0;
            for(int start=0;start<50;start+=8){var scenes=Enumerable.Range(start,Math.Min(8,50-start)).Select(i=>new Scene{Narration="Konu "+i,Duration=18}).ToArray();string raw=JsonSerializer.Serialize(new{Matches=scenes.Select((_,i)=>new{Scene=i,File=0,Confidence=.9,Reason="Model yinelenmiş ID verdi"})});
                chosen.AddRange(UploadedMediaMatch.Resolve(raw,scenes,catalog,(_,prompt,_)=>{corrections++;using var doc=JsonDocument.Parse(prompt);var ids=doc.RootElement.GetProperty("Catalog").EnumerateArray().Select(x=>x.GetProperty("Id").GetInt32()).ToArray();Check(ids.All(i=>!used.Contains(i)),"Kullanılmış dosya yeniden AI'a sunuldu.");return Task.FromResult(JsonSerializer.Serialize(new{Matches=new[]{new{Scene=0,File=ids[0],Confidence=.4,Reason="Kalanlar arasında anlatıma en yakın"}}}));},_=>{},CancellationToken.None,used).GetAwaiter().GetResult());}
            Check(chosen.Count==50&&chosen.Select(x=>x.File).Distinct().Count()==50&&used.Count==50&&corrections==49,"Tek kullanım kuralı bölüm gruplarında aşıldı.");
            Reject(()=>UploadedMediaMatch.Resolve("{}",new[]{new Scene()},catalog,(_,_,_)=>throw new Exception("Yetersiz kaynakta AI çağrılmamalı"),_=>{},CancellationToken.None,new HashSet<int>(Enumerable.Range(0,67))).GetAwaiter().GetResult());
        });
        Test("TIMELINE-SCROLL-RETURN", "50 sahneli çizelge kaydırılıp başa dönünce ve yakınlaştırılınca görünür kalır",()=>{
            var script=new VideoScript{Scenes=Enumerable.Range(0,50).Select(i=>new Scene{Narration="Sahne "+i,Duration=18}).ToList()};
            using var form=new Form{ClientSize=new Size(900,340)};var timeline=new WorkflowTimeline(script);var viewport=new WorkflowTimelineViewport(timeline,7750){Dock=DockStyle.Fill};form.Controls.Add(viewport);form.Show();Application.DoEvents();
            foreach(int width in new[]{7750,28000,1000}){viewport.SetContentWidth(width);foreach(bool end in new[]{true,false}){viewport.Scrollbar.Value=end?Math.Max(0,viewport.Scrollbar.Maximum-viewport.Scrollbar.LargeChange+1):0;Application.DoEvents();Check(timeline.Left==0&&timeline.Width==viewport.ClientSize.Width&&timeline.ViewOffset==viewport.Scrollbar.Value,"Çizelge pencereden taşındı.");using var shot=new Bitmap(timeline.Width,timeline.Height);timeline.DrawToBitmap(shot,new Rectangle(Point.Empty,shot.Size));int painted=0;for(int x=100;x<shot.Width-10;x+=10)if(shot.GetPixel(x,150).ToArgb()!=Theme.Panel.ToArgb())painted++;Check(painted>20,"Kaydırmadan sonra sahneler boş kaldı.");if(width==28000&&!end)shot.Save(Path.Combine(root,"timeline-return-left.png"));}}
            timeline.SelectScene(49);Check(viewport.Scrollbar.Value>0,"Listeden seçilen son sahne görünür alana alınmadı.");
        });
        Test("GLASS-GRID-VISIBILITY", "Cam blok altında zaman ızgarası görünür kalır; aktif kenar ve tutamaçlar çizilir",()=>{
            using var image=new Bitmap(220,100);using(var graphics=Graphics.FromImage(image)){graphics.Clear(Color.FromArgb(11,19,36));using var line=new Pen(Color.FromArgb(100,130,160));graphics.DrawLine(line,80,0,80,100);StudioGlass.Clip(graphics,new Rectangle(20,20,180,60),StudioGlass.Emerald,true);}
            Check(image.GetPixel(80,55).ToArgb()!=image.GetPixel(81,55).ToArgb(),"Cam dolgu arkasındaki ızgarayı kapattı.");Check(image.GetPixel(25,50).GetBrightness()>image.GetPixel(35,50).GetBrightness(),"Tutma kolu görünmüyor.");image.Save(Path.Combine(root,"glass-grid-verified.png"));
        });
        Test("ASSISTANT-LOCAL-DIAGNOSIS", "Hazırlık hatası saklanır, bağlantısız çözüm seçenekleri sunulur ve kurulum açılır",()=>{
            var st=New("assistant-local-diagnosis");var e=new Episode{Topic="PRIVATE-TOPIC",Creation=new CreationFlow()};st.Episodes.Add(e);var engine=new Engine(st);string page="";var agent=new AssistantActions(st,engine,()=>e,p=>page=p);int notices=0;agent.Notice+=_=>notices++;
            agent.PreparationFailed(e,new InvalidOperationException("Uygun yüklenmiş görüntü bulunamadı; eşleşmeyen bölüm"));Check(e.Error!=""&&notices==1,"Hazırlık hatası sessiz kaldı.");string answer=agent.Diagnose(e);Check(answer.Contains("en uygun")&&answer.Contains("yeniden dene")&&!answer.Contains("PRIVATE-TOPIC")&&!engine.Busy,"Tanı seçenekleri veya gizlilik bozuk.");using var args=JsonDocument.Parse("{}");agent.Execute("open_setup",args.RootElement,e.Id,false,"Kurulum listesini aç",CancellationToken.None).GetAwaiter().GetResult();Check(page=="Yüklenmesi gerekenler","Kurulum seçeneği işlemedi.");string prior=e.Error;agent.PreparationFailed(e,new OperationCanceledException());Check(e.Error==prior,"İptal yeni hata diye kaydedildi.");
        });
        Test("UPLOAD-VISION-INTEGRATION", "Gerçek dosyalar görsel API girdisiyle incelenir, metne eşleşir ve önbellek kullanılır",()=>{
            var st=New("upload-vision");st.Settings.EnabledCloudProviders="Gemini,OpenRouter";st.Settings.ProviderOrder="OpenRouter";st.Settings.FreeOnly=false;st.Vault.Set("GEMINI_API_KEY","fixture-vision");st.Vault.Set("OPENROUTER_API_KEY","fixture-text");
            string first=Path.Combine(st.Root,"mountain.png"),second=Path.Combine(st.Root,"book.png");using(var image=new Bitmap(64,64)){using var g=Graphics.FromImage(image);g.Clear(Color.Red);image.Save(first);g.Clear(Color.Blue);image.Save(second);}
            int vision=0,mapping=0;using var http=new HttpClient(new Handler(async request=>{
                string body=await request.Content!.ReadAsStringAsync();string answer;
                if(request.RequestUri!.Host.Contains("google")){vision++;Check(body.Contains("data:image/jpeg;base64,")&&!body.Contains("mountain.png")&&!body.Contains("book.png"),"Görsel yerine dosya adı incelendi.");answer=JsonSerializer.Serialize(new{Description=vision==1?"Açık bir kitap ve Kur’an sayfaları":"Yüksek karlı dağlar ve kayalar"});}
                else{mapping++;Check(body.Contains("Catalog")&&body.Contains("Scenes"),"Eşleştirme kataloğu eksik.");answer="{\"Matches\":[{\"Scene\":0,\"File\":1,\"Confidence\":0.95,\"Reason\":\"Dağ anlatımı dağ görüntüsüyle eşleşti\"},{\"Scene\":1,\"File\":0,\"Confidence\":0.95,\"Reason\":\"Kitap anlatımı kitapla eşleşti\"}]}";}
                return Ok(JsonSerializer.Serialize(new{choices=new[]{new{message=new{content=answer}}}}));
            }));
            var e=new Episode{Creation=new CreationFlow{Files=[first,second]}};var script=new VideoScript{Scenes=[new(){Narration="Dağlar yükselir.",Duration=18},new(){Narration="Kur’an okunur.",Duration=7}]};
            UploadedMediaMatch.Apply(st,e,script,new Services(st,_=>{},http),_=>{},CancellationToken.None).GetAwaiter().GetResult();
            Check(vision==2&&mapping==1&&script.Scenes[0].StockFile==second&&script.Scenes[1].StockFile==first,"Görüntüler içeriğe göre eşleşmedi.");Check(script.Scenes[0].Duration==18&&script.Scenes[1].Duration==7,"Eşleştirme konuşma süresini değiştirdi.");
            UploadedMediaMatch.Analyze(st,[first,second],_=>{},_=>{},CancellationToken.None,http).GetAwaiter().GetResult();Check(vision==2,"Görüntü açıklaması önbelleği kullanılmadı.");
        });
        Test("ACTIVITY-BEGIN-END", "Arka plan işi başlamadan görünür olur ve bitince temizlenir",()=>{
            var st=New("activity");var engine=new Engine(st);var gate=new TaskCompletionSource();bool visible=false;
            engine.Changed+=()=>{if(engine.Busy&&engine.ActivityName=="Dosyalar inceleniyor"&&engine.BusySince!=null)visible=true;};
            var task=engine.External(_=>gate.Task,"Dosyalar inceleniyor");Check(visible&&engine.Busy,"Başlangıç bildirimi eksik.");
            Reject(()=>engine.External(_=>Task.CompletedTask).GetAwaiter().GetResult());gate.SetResult();task.GetAwaiter().GetResult();Check(!engine.Busy&&engine.BusySince==null&&engine.ActivityName=="","Bitiş durumu temizlenmedi.");
        });
        Test("UPLOAD-PHOTO-TIMING", "18 saniyelik fotoğraf iki yönde yavaş hareketle gerçek videoya dönüşür",()=>{
            var st=New("photo-timing");string image=Path.Combine(st.Root,"input.png");using(var bitmap=new Bitmap(320,180)){using var g=Graphics.FromImage(bitmap);g.Clear(Color.Navy);g.FillRectangle(Brushes.Gold,110,40,100,100);bitmap.Save(image);}
            foreach(bool outward in new[]{false,true}){string output=Path.Combine(st.Root,outward?"out.mp4":"in.mp4");Media.Exec(st.Ffmpeg,["-y","-loop","1","-i",image,"-vf",WorkflowMedia.PhotoFilter(320,180,15,new CreationFlow(),outward,18),"-t","18","-an","-c:v","libx264","-preset","ultrafast","-pix_fmt","yuv420p",output],st.Root,CancellationToken.None).GetAwaiter().GetResult();Check(Math.Abs(Media.Duration(st,output,CancellationToken.None).GetAwaiter().GetResult()-18)<.1,"Fotoğraf süresi anlatımdan saptı.");}
        });
        Test("ACTIVITY-WIZARD-VISIBLE", "Hazırlık boyunca gösterge, süre ve durdur düğmesi görünür; tekrar başlatma kapalıdır",()=>{
            var st=New("activity-wizard");var flow=new CreationFlow{Normal=true,TextMode="own",OwnText="Bir metin.",OwnVisuals=true,Files=["fixture.png"],Subtitles=false,CoverChoice="none",Reached=2,Mode="auto",Split=true};flow.ApprovedHash=TimedNarration.TextHash(flow.Text);var e=new Episode{Topic="Çalışma",Creation=flow,WizardStep=2};st.Episodes.Add(e);using var form=new StudioForm(st);form.Show();Application.DoEvents();
            var engine=(Engine)typeof(StudioForm).GetField("engine",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(form)!;var gate=new TaskCompletionSource();var task=engine.External(_=>gate.Task,"Görüntüler inceleniyor");
            IEnumerable<Control> All(Control c)=>c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(All(x)));var until=DateTime.UtcNow.AddSeconds(3);while(DateTime.UtcNow<until){Application.DoEvents();Thread.Sleep(10);}
            Check(All(form).Single(x=>x.AccessibleName=="Çalışan işlem").Visible,"Çalışma göstergesi görünmüyor.");Check(All(form).Single(x=>x.AccessibleName=="Çalışan işlemi durdur").Visible,"Durdur görünmüyor.");Check(!All(form).Single(x=>x.AccessibleName=="Ana işlem").Enabled,"Çalışırken yeniden üretim açık.");
            gate.SetResult();until=DateTime.UtcNow.AddSeconds(3);while(!task.IsCompleted&&DateTime.UtcNow<until){Application.DoEvents();Thread.Sleep(10);}task.GetAwaiter().GetResult();Application.DoEvents();
        });
        Test("WORKFLOW-GATES-PLAN", "Metin/altyazı kararları zorunlu, bölümleme metni değiştirmez",()=>{
            var st=New("workflow-plan");var text="Bir yolcu yürür. Diğer yolcu ona yardım eder.";var flow=new CreationFlow{Normal=false,TextMode="own",OwnText=text};var e=new Episode{Topic="Yardımlaşma",Creation=flow};Reject(()=>flow.ValidateSource(e));flow.ApprovedHash=TimedNarration.TextHash(text);flow.CommitText(e);Check(e.UserNarration!.Shorts[0]==text&&!e.IncludeLong&&e.ShortCount==1,"Kaynak tipi/metin aktarımı hatalı.");Reject(flow.ValidateDesign);flow.OwnVisuals=true;flow.Files.Add("fixture.png");flow.CoverChoice="none";Reject(flow.ValidateDesign);flow.Subtitles=false;flow.ValidateDesign();flow.Subtitles=true;Reject(flow.ValidateDesign);flow.SubtitleStyle=CreationFlow.SubtitleStyles[0];flow.ValidateDesign();flow.Mode="manual";flow.Split=false;
            WorkflowMedia.Build(st,e,new Services(st,_=>{}),CancellationToken.None).GetAwaiter().GetResult();Check(e.Plan!.Shorts.Single().Scenes.Single().Narration==text,"Tek bölüm metni değişti.");var script=e.Plan.Shorts[0];script.Scenes=[new(){Narration="Bir yolcu yürür.",Duration=2},new(){Narration="Diğer yolcu ona yardım eder.",Duration=3}];WorkflowTimeline.ResizeBoundary(script,0,1);Check(script.Scenes[0].Duration==3&&script.Scenes[1].Duration==2&&script.Scenes.Sum(s=>s.Duration)==5,"Sınır değişiminde toplam süre değişti.");WorkflowTimeline.ResizeBoundary(script,0,1000);Check(script.Scenes.All(s=>s.Duration>=.099),"Bölüm eksi/boş süreye indi.");
            string hash=flow.ApprovedHash;flow.OwnText+=" Yeni cümle.";Reject(()=>flow.ValidateSource(e));Check(flow.ApprovedHash==hash,"Eski onay habersiz yeni metne verildi.");
        });
        Test("WORKFLOW-IMPORTED-RENDER", "Hazır konuşma korunarak altyazısız gerçek MP4 üretilir",()=>{
            var st=New("workflow-imported-render");st.Settings.EncodingPreset="ultrafast";st.Settings.CoverAiEnabled=false;st.Settings.OpeningCardEnabled=st.Settings.ClosingCardEnabled=false;st.Settings.UiSounds=false;
            var audio=Path.Combine(st.Root,"input.wav");Media.Exec(st.Ffmpeg,["-y","-f","lavfi","-i","sine=frequency=440:duration=3","-ar","44100",audio],st.Root,CancellationToken.None).GetAwaiter().GetResult();string original=Reliability.FileHash(audio);
            var image=Path.Combine(st.Root,"input.png");using(var picture=new Bitmap(640,360)){using var g=Graphics.FromImage(picture);g.Clear(Color.Navy);picture.Save(image);}
            var text="Bu ses dosyası değişmeden videoda kullanılacak.";var f=new CreationFlow{Normal=true,TextMode="own",OwnText=text,ApprovedHash=TimedNarration.TextHash(text),Subtitles=false,OwnVisuals=true,Files=[image],Mode="manual",Split=false,CoverChoice="none"};var e=new Episode{Topic="Hazır ses testi",Creation=f,Music=false};st.Episodes.Add(e);WorkflowMedia.ImportAudio(st,e,audio,CancellationToken.None).GetAwaiter().GetResult();WorkflowMedia.Build(st,e,new Services(st,_=>{}),CancellationToken.None).GetAwaiter().GetResult();new Engine(st).Produce(e).GetAwaiter().GetResult();Check(e.Error==""&&e.Videos.Count==1&&e.Videos[0].QualityPassed,"Hazır ses üretimi başarısız: "+e.Error+" "+string.Join(";",e.Videos.FirstOrDefault()?.Checks??[]));Check(Reliability.FileHash(audio)==original&&Math.Abs(e.Videos[0].Duration-3)<.3,"Özgün ses değişti veya video süresi sesi izlemedi.");
            var replacement=Path.Combine(st.Root,"new-cover.png");using(var picture=new Bitmap(1280,720)){using var g=Graphics.FromImage(picture);g.Clear(Color.Red);picture.Save(replacement);}var oldMovie=e.Videos[0].File;var settings=VideoQueue.Snapshot(VideoSettings.For(st.Settings,e));settings.CoverMode="Kendi PNG kapak";settings.CoverPng=replacement;f.CoverChoice="file";WorkflowArtwork.Apply(e,settings,f.CoverChoice);e.ProductionSettings=settings;new Engine(st).Produce(e).GetAwaiter().GetResult();Check(e.Error==""&&!e.ArtworkPending&&e.Videos[0].File!=oldMovie,"Kapak değişikliği yeniden işlenmedi: "+e.Error);using(var cover=new Bitmap(e.Videos[0].Thumbnail))Check(cover.GetPixel(640,360).R>240&&cover.GetPixel(640,360).B<15,"Önceki küçük resim yeni kapağın yerine kullanıldı");Check(Reliability.FileHash(audio)==original,"Kapak düzenlemesi özgün sesi değiştirdi");
            var script=e.Plan!.Long!;script.Scenes=[new(){Narration="Bu ses",Duration=1},new(){Narration="dosyası korunur",Duration=2}];var folder=Path.Combine(st.Root,"recut");WorkflowMedia.PrepareImportedAudio(st,e,script,folder,CancellationToken.None).GetAwaiter().GetResult();WorkflowMedia.Recut(st,script,folder,[2,1],CancellationToken.None).GetAwaiter().GetResult();Check(Math.Abs(script.Scenes[0].Duration-2)<.05&&Math.Abs(script.Scenes.Sum(s=>s.Duration)-3)<.05,"Görsel sınırı sese uymadı.");
        });

        Test("WORKFLOW-LEGACY-SHORTS", "Eski çoklu Shorts projesi yeni metin adımından geçince ikinci videoyu kaybetmez",()=>{
            var st=New("workflow-legacy-shorts");var e=new Episode{Topic="Eski Shorts paketi",IncludeLong=false,ShortCount=2,Plan=new ContentPlan{Shorts=[new VideoScript{Title="Bir",Scenes=[new Scene{Narration="Birinci kısa metin.",VisualQuery="forest",Duration=3}]},new VideoScript{Title="İki",Scenes=[new Scene{Narration="İkinci kısa metin.",VisualQuery="forest",Duration=3}]}]}};var f=CreationFlow.Migrate(e);e.Creation=f;f.CommitText(e);Check(e.ShortCount==2&&e.UserNarration!.Shorts.Count==2,"Geçiş diğer Shorts metnini kaybetti.");WorkflowMedia.Build(st,e,new Services(st,_=>{}),CancellationToken.None).GetAwaiter().GetResult();Check(e.Plan!.Shorts.Count==2&&e.Plan.Shorts[1].Title=="İki","Bölüm hazırlama ikinci videoyu kaybetti.");Rules.ValidatePlan(e);
        });
        Test("AI-DESIGN-FIXED-ACTIONS", "AI tasarımında oluştur ve kaydet düğmeleri kısa pencerede kaybolmaz",()=>{
            var st=New("ai-design-fixed-actions");using var dialog=new AiDesignDialog(st,false,"Su kabı",coverSettings:new Settings(),coverTitle:"Bir Tas Su");dialog.Show();Application.DoEvents();IEnumerable<Control> All(Control c)=>c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(All(x)));
            foreach(int height in new[]{650,760}){dialog.Height=height;Application.DoEvents();foreach(var text in new[]{"Tasarla","Onayla ve arşive kaydet"}){var button=All(dialog).OfType<Button>().Single(x=>x.Text==text);var bounds=button.RectangleToScreen(button.ClientRectangle);for(Control? parent=button.Parent;parent!=null;parent=parent.Parent)Check(parent.RectangleToScreen(parent.ClientRectangle).Contains(bounds),"Tasarım işlemi pencere dışında: "+text);}}
        });
        Test("WIZARD-COVER-INVALIDATION", "Kapak değişikliği eski küçük resmi geçersiz kılar, video kartlarını korur ve yeniden kayıtta kararlı kalır",()=>{
            var st=New("wizard-cover-invalidation");var old=VideoQueue.Snapshot(st.Settings);old.CoverMode="Şablon";var settings=VideoQueue.Snapshot(old);settings.CoverMode="Kendi PNG kapak";settings.CoverPng="new-cover.png";
            var script=new VideoScript{Scenes=[new Scene{Narration="Metin"}],CoverSettings=PackageArtwork.Snapshot(old)};script.CoverSettings.ScriptCardsOverride=true;script.CoverSettings.OpeningCardFile="separate-card.png";
            var e=new Episode{IncludeLong=true,Plan=new ContentPlan{Long=script},ProductionSettings=old,ArtworkSettings=PackageArtwork.Snapshot(old),Videos=[new AssetVideo()],Approved=true,ApprovalHash="old"};
            Check(WorkflowArtwork.Apply(e,settings,"file"),"Kapak değişikliği bulunamadı");Check(e.ArtworkPending&&e.TextDesignPending&&!e.Approved&&e.ApprovalHash=="","Eski kapak yeniden kullanılabilir kaldı");
            Check(VideoSettings.For(settings,e,script).CoverPng=="new-cover.png","Eski video kapak ayarı yeni seçimi ezdi");Check(script.CoverSettings.ScriptCardsOverride&&script.CoverSettings.OpeningCardFile=="separate-card.png","Ayrı açılış kartı kayboldu");Check(!WorkflowArtwork.Apply(e,settings,"file"),"Değişmeyen kaydetme yeniden değişiklik üretti");script.CoverSettings.CoverPng="editor-cover.png";Check(!WorkflowArtwork.Apply(e,settings,"file")&&script.CoverSettings.CoverPng=="editor-cover.png","Kapak editörünün özel seçimi sıradan kaydetmede ezildi");
            var hash=Reliability.RenderHash(script,e,settings);settings.CoverAiEnabled=!settings.CoverAiEnabled;WorkflowArtwork.Apply(e,settings,"ai");Check(hash!=Reliability.RenderHash(script,e,settings),"AI kapak tercihi çıktı parmak izine katılmadı");
        });
        Test("WIZARD-LIVE-VISUAL-COUNT", "Hazır sahne sayısı çizimden sonra canlı veriye göre güncellenebilir",()=>{
            var st=New("wizard-live-count");string image=Path.Combine(st.Root,"scene.png");File.WriteAllText(image,"fixture");var e=new Episode{IncludeLong=true,Plan=new ContentPlan{Long=new VideoScript{Scenes=[new Scene(),new Scene()]}}};Check(WorkflowArtwork.ReadyText(e,st.Settings).StartsWith("0/2"),"İlk sayı yanlış");e.Plan.Long.Scenes[0].StockFile=image;Check(WorkflowArtwork.ReadyText(e,st.Settings).StartsWith("1/2"),"Hazır sahne canlı sayılmadı");
        });
        Test("COVER-EXACT-ASPECT-RATIOS", "İki motorun bütün kalite seçimleri kapak doğrulamasından geçen yatay/dikey oran üretir",()=>{
            foreach(var model in new[]{"FLUX.2","Stable Diffusion XL"})foreach(var quality in LocalCoverEngine.Qualities)foreach(bool vertical in new[]{false,true}){
                var graph=LocalCoverEngine.Prepare(LocalCoverEngine.ResolveWorkflow(model),"water bowl",vertical,quality:quality);var latent=graph.First(x=>x.Value?["class_type"]?.ToString() is "EmptyLatentImage" or "EmptyFlux2LatentImage").Value!["inputs"]!;int w=latent["width"]!.GetValue<int>(),h=latent["height"]!.GetValue<int>();Check(vertical?w*16==h*9:w*9==h*16,"Kapak oranı geçersiz: "+model+" "+quality+" "+w+"x"+h);
            }
        });
        Test("WORKFLOW-DESIGN-SAVE", "Yeniden açılan projede tasarım ayarı gerçek üretim ayarına kaydedilir",()=>{
            var st=New("workflow-design-save");var f=new CreationFlow{Normal=true,TextMode="own",OwnText="Özgün metin burada.",OwnVisuals=true,Files=["fixture.png"],Subtitles=false,CoverChoice="none",Reached=1};f.ApprovedHash=TimedNarration.TextHash(f.Text);var e=new Episode{Topic="Tasarım kaydı",Creation=f,WizardStep=1,ProductionSettings=VideoQueue.Snapshot(st.Settings)};st.Episodes.Add(e);using var form=new StudioForm(st);form.Show();Application.DoEvents();IEnumerable<Control> All(Control c)=>c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(All(x)));var group=All(form).OfType<StudioDisclosure>().Single(x=>x.Controls.OfType<Button>().Any(b=>b.AccessibleName=="Ses ve müzik bölümünü aç veya kapat"));group.SetExpanded(true);var volume=All(form).OfType<NumericUpDown>().Single(x=>x.AccessibleName=="Müzik seviyesi • yüzde");volume.Value=23;All(form).OfType<Button>().Single(x=>x.AccessibleName=="Ana işlem").PerformClick();Application.DoEvents();Check(Math.Abs(VideoSettings.For(st.Settings,e).MusicVolume-.23)<.001&&e.WizardStep==2,"Yeni tasarım eski ayar tarafından ezildi.");
        });
        Test("WORKFLOW-EXTRA-SHORTS", "Onaylı normal videoya ek Shorts ayrı metin ve dikey plan olarak eklenir",()=>{
            var st=New("workflow-extra-shorts");var f=new CreationFlow{Normal=true,TextMode="own",OwnText="Ana videonun onaylı özgün metni.",ExtraShortCount=2,ExtraShortTexts=["İlk kısa anlatım.","İkinci kısa anlatım."],Subtitles=false,OwnVisuals=true,Files=["fixture.png"],Mode="manual",Split=false,CoverChoice="none"};f.ApprovedHash=TimedNarration.TextHash(f.Text);f.ExtraApprovalHash=f.ExtrasHash;var e=new Episode{Topic="Ana konu",Creation=f};WorkflowMedia.Build(st,e,new Services(st,_=>{}),CancellationToken.None).GetAwaiter().GetResult();Check(e.IncludeLong&&e.ShortCount==2&&e.Plan!.Shorts.Count==2&&e.Plan.Shorts[1].Scenes[0].Narration==f.ExtraShortTexts[1]&&e.UserNarration!.Shorts.Count==2,"Ek Shorts planı/metinleri eksik.");var migrated=CreationFlow.Migrate(e);migrated.CommitText(e);Check(e.ShortCount==2&&e.UserNarration!.Shorts.Count==2,"Eski çoklu video metinleri geçişte kayboldu.");f.OwnText+=" Yeni bilgi.";Reject(f.ValidateDesign);
        });
        Test("WORKFLOW-EDIT-UNDO", "Bölüm kaldırma altyazıyı tekrarlamaz; ardışık geri alma yeniden açılışta da korunur",()=>{
            var st=New("workflow-edit-undo");var text="Birinci bölüm. İkinci bölüm. Üçüncü bölüm.";var model=new CreationFlow{Normal=true,TextMode="own",OwnText=text,ApprovedHash=TimedNarration.TextHash(text),Subtitles=true,SubtitleStyle="Sade beyaz",CoverChoice="none",Mode="manual",Split=true,Reached=3};var episode=new Episode{Topic="Bölüm geri alma",Creation=model,WizardStep=2,IncludeLong=true,ShortCount=0,Plan=new ContentPlan{Long=new VideoScript{Scenes=[new(){Narration="Birinci bölüm.",Duration=1},new(){Narration="İkinci bölüm.",Duration=2},new(){Narration="Üçüncü bölüm.",Duration=3}]}}};st.Episodes.Add(episode);using var form=new StudioForm(st);form.Show();Application.DoEvents();IEnumerable<Control> All(Control c)=>c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(All(x)));void Click(string name){All(form).OfType<Button>().Single(x=>x.Text==name).PerformClick();Application.DoEvents();}
            Click("Bölümü kaldır • sesi koru");Check(episode.Plan.Long.Scenes.Count==2&&episode.Plan.Long.Scenes[0].SubtitleText=="Birinci bölüm. İkinci bölüm.","Altyazı tekrarlandı veya bölüm silinmedi.");Click("Bölümü kaldır • sesi koru");Check(episode.Plan.Long.Scenes.Count==1&&Math.Abs(episode.Plan.Long.Scenes[0].Duration-6)<.001,"Süre kayboldu.");Click("Geri al");Check(episode.Plan.Long.Scenes.Count==2,"İlk geri alma başarısız.");Click("Geri al");Check(episode.Plan.Long.Scenes.Count==3&&episode.Plan.Long.Scenes[1].Narration=="İkinci bölüm.","İkinci geri alma geçmişi kayboldu.");
        });
        Test("WORKFLOW-VOICE-CACHE", "Görsel sınırı değişince ses korunur; ses veya telaffuz tercihi değişince yenilenir",()=>{
            var st=New("workflow-voice-cache");var audio=Path.Combine(st.Root,"voice.wav");var timing=Path.Combine(st.Root,"timing.json");File.WriteAllText(audio,"fixture audio bytes");File.WriteAllText(timing,"[]");var e=new Episode{Creation=new CreationFlow()};var script=new VideoScript{Scenes=[new(){Narration="Birinci cümle. İkinci cümle."}]};e.Plan=new ContentPlan{Long=script};var settings=VideoQueue.Snapshot(st.Settings);WorkflowMedia.FreezeAudio(e,script,new AssetVideo{NarrationFile=audio,NarrationTimingFile=timing},settings);Check(WorkflowMedia.CanReuseAudio(e,script,settings),"Aynı ses önbelleği bulunamadı.");script.Scenes=[new(){Narration="Birinci cümle.",Duration=2},new(){Narration="İkinci cümle.",Duration=1}];Check(WorkflowMedia.CanReuseAudio(e,script,settings),"Sadece görsel bölümleme ses önbelleğini bozdu.");settings.VoiceSpeed+=.1;Check(!WorkflowMedia.CanReuseAudio(e,script,settings),"Ses hızı değiştiğinde eski ses kullanılıyor.");settings=VideoQueue.Snapshot(st.Settings);settings.PronunciationDictionary+="\nAllah'ım=Allahım";Check(!WorkflowMedia.CanReuseAudio(e,script,settings),"Telaffuz değiştiğinde eski ses kullanılıyor.");
        });

        Test("WIZARD-DELETE-ACTION", "Kalıcı silme her üretim adımında görünür, çalışan iş ve kayıtsız taslakta kapalıdır",()=>{
            var st=New("wizard-delete-action");var episode=new Episode{Topic="Silme düğmesi testi",Demo=true};episode.Plan=Engine.DemoPlan(episode);st.Episodes.Add(episode);st.Save();using var form=new StudioForm(st);form.Show();Application.DoEvents();
            IEnumerable<Control> Children(Control c)=>c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(Children(x)));
            void Open()=>typeof(StudioForm).GetMethod("Navigate",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(form,["Video hazırla"]);
            Button Delete()=>Children(form).OfType<Button>().Single(b=>b.AccessibleName=="Seçili projeyi kalıcı sil");
            foreach(int width in new[]{1120,1440})foreach(int step in Enumerable.Range(0,5)){form.Width=width;episode.WizardStep=step;Open();Application.DoEvents();var button=Delete();Check(button.Visible&&button.Enabled,"Silme düğmesi üretim adımında yok veya kapalı.");var visible=button.RectangleToScreen(button.ClientRectangle);for(Control? parent=button.Parent;parent!=null;parent=parent.Parent)Check(parent.RectangleToScreen(parent.ClientRectangle).Contains(visible),"Silme düğmesi görünür alan dışına taşıyor.");}
            typeof(StudioForm).GetField("autoRunning",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(form,true);Open();Check(!Delete().Enabled,"Çalışan sırada silme açık.");typeof(StudioForm).GetField("autoRunning",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(form,false);
            typeof(StudioForm).GetMethod("StartWizardProject",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(form,null);Check(!Delete().Enabled&&st.Episodes.Count==1,"Kayıtsız taslak silinebilir veya gezinme projeyi değiştirdi.");
        });

        Test("COMBO-POPUP-BOUNDS", "Açılır listeler pencere daraltıldıktan sonra alanı aşmaz",()=>{
            using var form=new Form{ClientSize=new Size(850,550),StartPosition=FormStartPosition.Manual,Location=new Point(50,50)};
            var combo=Theme.Combo(["3D Telkari Altın","3D Metalik Altın",new string('M',240)],"3D Telkari Altın",1500);combo.Location=new Point(25,30);form.Controls.Add(combo);form.Show();Application.DoEvents();
            foreach(int width in new[]{650,300,520}){
                combo.DropDownWidth=1600;combo.Width=width;combo.DroppedDown=true;Application.DoEvents();
                var info=new ComboInfo{Size=System.Runtime.InteropServices.Marshal.SizeOf<ComboInfo>()};Check(GetComboBoxInfo(combo.Handle,ref info)&&GetWindowRect(info.List,out var unused),"Açılır liste penceresi okunamadı.");GetWindowRect(info.List,out var popup);GetWindowRect(combo.Handle,out var face);
                var bounds=form.RectangleToScreen(form.ClientRectangle);Check(popup.Right-popup.Left<=face.Right-face.Left+4&&popup.Left>=bounds.Left&&popup.Right<=bounds.Right,"Açılır liste kontrol veya pencere sınırlarını aşıyor.");Check(combo.Items.Count==3&&combo.SelectedIndex==0,"Liste seçenekleri veya seçim değişti.");combo.DroppedDown=false;
            }
        });

        Test("COMPACT-0322-FIXES", "Hızlı önizleme, model/kalite aktarımı, kolay kurulum ve güvenli temizlik",()=>{
            var st=New("compact-0322-fixes");var settings=st.Settings;settings.CoverLocalModel="FLUX.2";settings.CoverLocalQuality="Hızlı";settings.CoverTitleStyle="3D Telkari Altın";
            var copy=PackageArtwork.Snapshot(settings);Check(copy.CoverLocalModel=="FLUX.2"&&copy.CoverLocalQuality=="Hızlı"&&copy.CoverTitleStyle=="3D Telkari Altın","Model/kalite proje ayarına aktarılmadı.");
            Check(!JsonSerializer.Serialize(new Settings(),Json.Options).Contains("SetupCompleted"),"Eski ayarlara yeni kurulum alanı zorla eklendi.");
            var source=Path.Combine(st.Root,"source.png");using(var image=new Bitmap(480,270)){using var g=Graphics.FromImage(image);g.Clear(Color.Navy);image.Save(source);}
            var scene=new Scene{StockFile=source,Narration="Hızlı önizleme",Duration=2};var script=new VideoScript{Scenes=[scene],TextLayout=new TextLayout{TitleEnabled=false,SubtitleColor="FFFFFF"}};var folder=Path.Combine(st.Root,"preview");
            EditorPreview.FastFrame(st,script,0,.3,false,folder,CancellationToken.None).GetAwaiter().GetResult();string original=Reliability.FileHash(Path.Combine(folder,"preview.png"));var clean=Directory.GetFiles(folder,"source-*.png").Single();var stamp=File.GetLastWriteTimeUtc(clean);
            script.TextLayout.SubtitleColor="FF0000";EditorPreview.FastFrame(st,script,0,.3,false,folder,CancellationToken.None).GetAwaiter().GetResult();Check(original!=Reliability.FileHash(Path.Combine(folder,"preview.png"))&&stamp==File.GetLastWriteTimeUtc(clean),"Yazı değişiminde görüntü tekrar üretildi veya yazı değişmedi.");
            var target=Path.Combine(st.Root,"program");Directory.CreateDirectory(target);File.WriteAllText(Path.Combine(target,"AnkaStudio.pdb"),"debug");File.WriteAllText(Path.Combine(target,"settings.json"),"private-data");var backup=Path.Combine(st.Root,"backup");Check(InstallCleanup.Clean(target,backup)==1&&File.Exists(Path.Combine(backup,"AnkaStudio.pdb"))&&File.Exists(Path.Combine(target,"settings.json")),"Temizlik kullanıcı verisine dokundu.");
            using var form=new StudioForm(st);using var welcome=(Form)typeof(StudioForm).GetMethod("WelcomeSetup",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(form,null)!;
            welcome.Show();Application.DoEvents();IEnumerable<Control> All(Control c)=>c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(All(x)));for(int step=0;step<2;step++)All(welcome).OfType<Button>().Single(b=>b.Text=="Devam et").PerformClick();Check(All(welcome).OfType<Button>().Any(b=>b.Text=="Stüdyo’yu aç"),"Kolay kurulumun üç adımı eksik.");
        });
        Test("SETTINGS-NAV-PERFORMANCE", "Kanal ve program açılışı ve tüm ayar gruplarının korunması",()=>{
            var st=New("settings-speed");UiSound.Configure(st.Settings);using var form=new StudioForm(st);form.Show();Application.DoEvents();
            var nav=typeof(StudioForm).GetMethod("Navigate",BindingFlags.NonPublic|BindingFlags.Instance)!;
            var before=JsonSerializer.Serialize(st.Settings,Json.Options);var timings=new List<long>();
            for(int repeat=0;repeat<3;repeat++){nav.Invoke(form,["Video hazırla"]);Application.DoEvents();var clock=System.Diagnostics.Stopwatch.StartNew();nav.Invoke(form,["Yönetim ayarları"]);Application.DoEvents();clock.Stop();timings.Add(clock.ElapsedMilliseconds);}
            Json.Write(Path.Combine(st.Root,"timings.json"),new{Milliseconds=timings});
            Check(before==JsonSerializer.Serialize(st.Settings,Json.Options),"Ekranı açmak ayarları değiştirdi.");
            if(!args.Contains("--baseline"))Check(timings.Max()<1800,"Ayar ekranı 1.8 saniye sınırını aştı: "+string.Join(',',timings));
        });
        Test("COMPACT-CENTER-PORTABLE", "Kompakt merkez, yeşil adımlar, ayrı arşivler ve taşınabilir motor keşfi",()=>{
            var st=New("compact-center");UiSound.Configure(st.Settings);
            var engineRoot=Path.Combine(st.Root,"portable-engine");Directory.CreateDirectory(Path.Combine(engineRoot,"python_embeded"));Directory.CreateDirectory(Path.Combine(engineRoot,"ComfyUI"));File.WriteAllText(Path.Combine(engineRoot,"python_embeded","python.exe"),"");File.WriteAllText(Path.Combine(engineRoot,"ComfyUI","main.py"),"");
            string workflow=Path.Combine(engineRoot,"anka-sdxl.json");File.WriteAllText(workflow,"{}");
            Check(LocalCoverEngine.FindInstalledWorkflow([], [engineRoot])==workflow,"Codex dışında motor bulunamadı.");
            var linked=Path.Combine(st.Root,"binding");Directory.CreateDirectory(linked);File.WriteAllText(Path.Combine(linked,"engine-folder.txt"),engineRoot);File.WriteAllText(Path.Combine(linked,"connected-workflow.json"),"{}");
            Check(LocalCoverEngine.FindInstalledWorkflow([],[linked])==Path.Combine(linked,"connected-workflow.json"),"Bağlı motor yeniden bulunamadı.");
            var defaults=new Settings{ChannelName="Doğa Kanalı",CoverStyle="Minimal"};var ep=new Episode{Topic="Doğa"};ProductionPresets.Apply(ep,defaults,"story");Check(ep.ProductionSettings!.CoverStyle=="Minimal","Başka kanala kişisel tasarım dayatıldı.");
            using var form=new StudioForm(st);form.CaptureCompactCenter(Path.Combine(st.Root,"screens"));
        });
        Test("YOUTUBE-LIBRARY-PAGES", "Tüm kanal sayfaları okunur; başlık, planlı tarih ve izlenmeler korunur",()=>{
            var st=New("youtube-library-pages");st.Settings.GoogleClientId="fixture";st.Vault.Set("YOUTUBE_REFRESH_TOKEN","fixture");st.Vault.Set("GOOGLE_CLIENT_SECRET","fixture");int pages=0;
            using var client=new HttpClient(new Handler(req=>{
                string url=req.RequestUri!.AbsolutePath;string json;
                Check(req.Method==HttpMethod.Get||url.EndsWith("/token"),"Listeleme uzak videoyu değiştirdi.");
                if(url.EndsWith("/token"))json="{\"access_token\":\"fixture\"}";
                else if(url.EndsWith("/channels"))json=JsonSerializer.Serialize(new{items=new[]{new{id="channel",snippet=new{title="Kanal"},statistics=new{viewCount="10"},contentDetails=new{relatedPlaylists=new{uploads="list"}}}}});
                else if(url.EndsWith("/playlistItems")){pages++;json=pages==1?JsonSerializer.Serialize(new{items=new[]{new{contentDetails=new{videoId="one"}}},nextPageToken="second"}):JsonSerializer.Serialize(new{items=new[]{new{contentDetails=new{videoId="two"}},new{contentDetails=new{videoId="one"}}}});}
                else if(url.EndsWith("/videos"))json=JsonSerializer.Serialize(new{items=new[]{new{id="one",snippet=new{title="Yayındaki kıssa",publishedAt="2026-01-01T00:00:00Z"},statistics=new{viewCount="123"},status=new{privacyStatus="public",publishAt=(string?)null}},new{id="two",snippet=new{title="Planlı kıssa",publishedAt="2026-01-02T00:00:00Z"},statistics=new{viewCount="0"},status=new{privacyStatus="private",publishAt=(string?)"2026-12-01T09:00:00Z"}}}});
                else throw new InvalidOperationException("Listeleme için Analytics çağrısı yapılmamalı.");
                return Task.FromResult(Ok(json));
            }));
            var result=new YouTubeStatistics(st,client).Fetch(28,_=>{},CancellationToken.None,false).GetAwaiter().GetResult();
            Check(pages==2&&result.Videos.Count==2,"Kanalın tüm sayfaları okunmadı veya tekrar kayıt oluştu.");Check(result.Videos[0].Title=="Yayındaki kıssa"&&result.Videos[0].Views==123&&result.Videos[1].ScheduledAt.HasValue,"Başlık, izlenme veya plan bilgisi kayboldu.");
        });
        Test("STARTUP-UPDATE-CHAIN", "GitHub güncelleme zinciri sırayla ilerler; paket özeti, sürüm ve güvenli ZIP yolu denetlenir",()=>{
            string catalog=JsonSerializer.Serialize(new{Updates=new[]{new StartupUpdates.Manifest("1.2.0","https://updates.example.com/two.zip",new string('A',64)),new StartupUpdates.Manifest("1.1.0","https://updates.example.com/one.zip",new string('B',64))}});
            Check(StartupUpdates.Next(catalog,new Version(1,0,0))!.Version=="1.1.0","İlk yama atlandı");Check(StartupUpdates.Next(catalog,new Version(1,1,0))!.Version=="1.2.0","İkinci yama atlandı");Check(StartupUpdates.Next(catalog,new Version(1,2,0))==null,"Zincir bitince yeni yama bulundu");
            Reject(()=>StartupUpdates.Next(JsonSerializer.Serialize(new StartupUpdates.Manifest("1.3.0","https://updates.example.com/x.zip",new string('A',64),"1.2.0")),new Version(1,0,0)));
            string version=typeof(StartupUpdates).Assembly.GetName().Version!.ToString(3);int requests=0;
            byte[] Package(string? extra=null){using var ms=new MemoryStream();using(var zip=new System.IO.Compression.ZipArchive(ms,System.IO.Compression.ZipArchiveMode.Create,true)){
                foreach(string file in new[]{"AnkaStudio.exe","AnkaStudio.dll"}){var entry=zip.CreateEntry(file);using var stream=entry.Open();using var input=File.OpenRead(Path.Combine(AppContext.BaseDirectory,file));input.CopyTo(stream);}
                using(var stream=zip.CreateEntry("coreclr.dll").Open())stream.Write(new byte[32]);
                if(extra!=null){using var stream=zip.CreateEntry(extra).Open();stream.Write(new byte[12]);}
            }return ms.ToArray();}
            foreach(string scenario in new[]{"valid","hash","traversal","user-data","version"}){
                byte[] bytes=Package(scenario=="traversal"?"../escape.txt":scenario=="user-data"?"settings.json":null);
                string sha=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
                var manifest=new StartupUpdates.Manifest(scenario=="version"?"99.0.0":version,"https://updates.example.com/app.zip",scenario=="hash"?new string('0',64):sha);
                using var http=new HttpClient(new Handler(req=>{requests++;return Task.FromResult(req.RequestUri!.AbsolutePath.EndsWith(".zip")?new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(bytes)}:Ok(JsonSerializer.Serialize(manifest)));}));
                string cache=Path.Combine(root,"update-"+scenario);var logs=new List<string>();bool rejected=false;
                try{var result=StartupUpdates.Stage(http,new Uri("https://updates.example.com/feed.json"),new Version(0,0,1),cache,(s,_)=>logs.Add(s),CancellationToken.None).GetAwaiter().GetResult();Check(scenario=="valid"&&result!=null&&File.Exists(Path.Combine(result.Folder,"files.json")),"Güvensiz paket kabul edildi");}
                catch(InvalidOperationException){rejected=true;}
                Check(rejected==(scenario!="valid"),"Paket denetimi yanlış: "+scenario);Check(!File.Exists(Path.Combine(cache,"escape.txt")),"ZIP dışarı yazdı");
            }
            Check(requests==10,"İstek akışı beklenenden farklı");
        });
        Test("PRODUCTION-PRESETS", "Kalıplar kullanıcı bağlantılarını ve hazır metni korur, tamamlanmamış projeye onay vermez",()=>{
            var settings=new Settings{GoogleTtsVoice="tr-TR-Chirp3-HD-Algieba",ChannelName="Test kanal"};string original=JsonSerializer.Serialize(settings);
            foreach(var preset in ProductionPresets.All){var e=new Episode{Topic="Deneme"};ProductionPresets.Apply(e,settings,preset.Id);Check(e.WizardPreset==preset.Id&&e.ProductionSettings!.GoogleTtsVoice==settings.GoogleTtsVoice,"Ses tercihi veya kalıp kayboldu");Check(!ProductionPresets.Complete(e)[2]&&!ProductionPresets.Complete(e)[3],"Hazır olmayan çıktı onaylandı");Check(preset.Id!="story"||e.AiStoryImages&&e.AiStoryCinematic&&e.AiStoryAutonomous&&!e.IncludeLong&&e.ShortCount==1,"Hikâye kalıbı eksik");Check(preset.Id!="package"||e.IncludeLong&&e.ShortCount==2,"Paket türü yanlış");}
            Check(original==JsonSerializer.Serialize(settings),"Kanal varsayılanları değişti");
            var own=new Episode{Topic="Kıssa",UserNarration=new(){Enabled=true,LongText="Yolcu bir kuşa su verir."}};ProductionPresets.Apply(own,settings,"story");Check(own.UserNarration.Shorts.Single()=="Yolcu bir kuşa su verir.","Hazır metin değişti");
            own.Plan=new ContentPlan();string before=JsonSerializer.Serialize(own);Reject(()=>ProductionPresets.Apply(own,settings,"documentary"));Check(before==JsonSerializer.Serialize(own),"Hazır plan değiştirildi");
            var tooLong=new Episode{UserNarration=new(){Enabled=true,LongText=string.Join(" ",Enumerable.Repeat("kelime",400))}};before=JsonSerializer.Serialize(tooLong);Reject(()=>ProductionPresets.Apply(tooLong,settings,"story"));Check(before==JsonSerializer.Serialize(tooLong),"Reddedilen işlem projeyi değiştirdi");
        });
        Test("STORY-CINEMA-RENDER", "Sinematik sahneler gerçek video üretir; önbellek, iptal ve telaffuz korunur",()=>{
            var st=New("story-cinema");st.Settings.FramesPerSecond=12;st.Settings.EncodingPreset="ultrafast";
            var image=Path.Combine(st.Root,"source.png");Media.Poster(image,"HİKÂYE",true,"TEST",true);
            var rows=new[]{new Scene{Narration="Yolcu dinlenir."},new Scene{Narration="Dağ sarsıldı."},new Scene{Narration="Tekne denizde ilerler."},new Scene{Narration="Kalabalık meydanda bekler."},new Scene{Narration="Gökyüzü aydınlanır."}};
            Check(rows.Select(StoryCinema.Profile).Distinct().Count()==5,"Sahne hareketleri ayrışmadı");
            var logs=new List<string>();foreach(var scene in rows){
                string movie=StoryCinema.Render(st,image,scene,180,320,1.5,st.Settings,st.Root,CancellationToken.None,logs.Add,[new Caption(.5,1.2,scene.Narration)]).GetAwaiter().GetResult();
                Check(File.Exists(movie)&&new FileInfo(movie).Length>1000,"Animasyon yok");Check(Math.Abs(Media.Duration(st,movie,CancellationToken.None).GetAwaiter().GetResult()-1.5)<.15,"Sahne süresi sesle uyumsuz");
                Media.Exec(ffmpeg,["-v","error","-i",movie,"-f","null","-"],st.Root,CancellationToken.None).GetAwaiter().GetResult();
                StoryCinema.Render(st,image,scene,180,320,1.5,st.Settings,st.Root,CancellationToken.None,logs.Add,[new Caption(.5,1.2,scene.Narration)]).GetAwaiter().GetResult();
            }
            Check(logs.Count(x=>x.Contains("tekrar kullanıldı"))==5&&logs.Any(x=>x.Contains('%')),"Önbellek veya ilerleme yok");
            using var stop=new CancellationTokenSource();stop.Cancel();bool cancelled=false;try{StoryCinema.Render(st,image,rows[0],180,320,1.5,st.Settings,st.Root,stop.Token,_=>{}).GetAwaiter().GetResult();}catch(OperationCanceledException){cancelled=true;}Check(cancelled,"İptal göz ardı edildi");
            Check(NarrationFlow.SpokenText("Allah’ım, Âmin.")=="Allahım, Aamin.","Telaffuz ayrımı bozuldu");
            var e=new Episode{AiStoryImages=true};string hash=Reliability.MediaPlanHash(e);e.AiStoryCinematic=false;Check(hash!=Reliability.MediaPlanHash(e),"Animasyon ayarı çıktı onayını geçersiz kılmadı");
        });
        Test("STORY-IMAGE-APPROVAL-GATE", "Görsel onayı olmadan sonraki sahneye geçilmez; yeniden üretme ve iptal çalışır",()=>{
            var st=New("story-approval");st.Vault.Set("GEMINI_API_KEY","fixture-only");st.Settings.FreeOnly=false;st.Settings.CoverPaidAllowed=true;st.Settings.CoverProviderOrder="Gemini";st.Settings.CoverMonthlyRequests=50;
            var script=new VideoScript{Scenes=[new(){Narration="Bir kuş dinlenir."},new(){Narration="Kuşa yardım edilir."}]};
            var e=new Episode{Topic="Kuş",IncludeLong=false,ShortCount=1,AiStoryImages=true,AiStoryLocal=false,Plan=new(){Shorts=[script]}};
            byte[] png;using(var bitmap=new Bitmap(360,640)){using var ms=new MemoryStream();bitmap.Save(ms,System.Drawing.Imaging.ImageFormat.Png);png=ms.ToArray();}
            int calls=0;using var http=new HttpClient(new Handler(req=>{Interlocked.Increment(ref calls);return Task.FromResult(Ok(JsonSerializer.Serialize(new{candidates=new[]{new{content=new{parts=new[]{new{inlineData=new{mimeType="image/png",data=Convert.ToBase64String(png)}}}}}}})));}));
            var gates=new System.Collections.Concurrent.ConcurrentQueue<TaskCompletionSource<bool>>();
            Task<bool> Review(string path,string text,CancellationToken ct){Check(File.Exists(path),"Önizleme dosyası eksik");var gate=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);gates.Enqueue(gate);return gate.Task.WaitAsync(ct);}
            TaskCompletionSource<bool> Next(){Check(SpinWait.SpinUntil(()=>!gates.IsEmpty,TimeSpan.FromSeconds(10)),"Onay beklenmedi");gates.TryDequeue(out var gate);return gate!;}
            var first=StoryImages.Prepare(st,e,script,script.Scenes[0],true,st.Settings,CancellationToken.None,_=>{},http,Review);
            var reference=Next();Check(calls==1&&!first.IsCompleted,"Referans onayı atlandı");reference.SetResult(true);
            var scene=Next();Check(calls==2&&!first.IsCompleted,"Sahne onayı atlandı");scene.SetResult(false);
            var retry=Next();Check(calls==3,"Reddedilen resim yeniden üretilmedi");retry.SetResult(true);first.GetAwaiter().GetResult();
            StoryImages.Prepare(st,e,script,script.Scenes[0],true,st.Settings,CancellationToken.None,_=>{},http,Review).GetAwaiter().GetResult();Check(calls==3&&gates.IsEmpty,"Onaylı resim tekrar soruldu");
            using var cancel=new CancellationTokenSource();var second=StoryImages.Prepare(st,e,script,script.Scenes[1],true,st.Settings,cancel.Token,_=>{},http,Review);Next();Check(calls==4&&!second.IsCompleted,"İkinci sahne onay beklemiyor");cancel.Cancel();bool cancelled=false;try{second.GetAwaiter().GetResult();}catch(OperationCanceledException){cancelled=true;}Check(cancelled&&calls==4,"İptal sonrası üretim devam etti");
            Check(StoryImages.Prompt(e,script,script.Scenes[0],true).Contains(script.Scenes[0].Narration),"Görsel talimatı sahne anlatımını kaybetti");
        });
        Test("STORY-REFERENCE-CONTINUITY", "Bütün sahneler aynı referansı gönderir; devam, bozulma, tarz değişimi ve MP4 hareketi doğrulanır", () => {
            var st=New("story-reference");st.Vault.Set("GEMINI_API_KEY","fixture-only");
            st.Settings.FreeOnly=false;st.Settings.CoverPaidAllowed=true;st.Settings.CoverProviderOrder="Gemini";st.Settings.CoverMonthlyRequests=50;
            var script=new VideoScript{Title="İki yolcu",Scenes=[new(){Narration="Hasan çölde yürür.",VisualQuery="desert"},new(){Narration="Hasan arkadaşıyla konuşur.",VisualQuery="two travellers"}]};
            var e=new Episode{Topic="İki yolcu",IncludeLong=false,ShortCount=1,AiStoryImages=true,AiStoryLocal=false,AiStoryCharacters="Hasan: kırk yaşında, kısa siyah sakal, mavi cübbe. Ali: beyaz sakal, kahverengi cübbe.",Plan=new(){Shorts=[script]}};
            byte[] png;using(var bitmap=new System.Drawing.Bitmap(360,640)){using var g=System.Drawing.Graphics.FromImage(bitmap);g.Clear(System.Drawing.Color.DarkBlue);g.FillRectangle(System.Drawing.Brushes.Gold,100,150,80,200);using var ms=new MemoryStream();bitmap.Save(ms,System.Drawing.Imaging.ImageFormat.Png);png=ms.ToArray();}
            string encoded=Convert.ToBase64String(png);int calls=0,referenced=0; string? fixedReference=null;
            using var http=new HttpClient(new Handler(async req=>{
                calls++;Check(req.RequestUri!.Host=="generativelanguage.googleapis.com","Referans desteklemeyen sağlayıcı çağrıldı");
                using var body=JsonDocument.Parse(await req.Content!.ReadAsStringAsync());var parts=body.RootElement.GetProperty("contents")[0].GetProperty("parts");
                if(parts.GetArrayLength()>1){referenced++;string data=parts[1].GetProperty("inlineData").GetProperty("data").GetString()!; if(referenced==1 || referenced==5)fixedReference=data; Check(data==fixedReference,"Karakter referansı sahneler arasında değişti");Check(parts[0].GetProperty("text").GetString()!.Contains("mavi cübbe"),"Karakter tanımı eksik");}
                return Ok(JsonSerializer.Serialize(new {candidates=new[]{new {content=new {parts=new[]{new {inlineData=new {mimeType="image/png",data=encoded}}}}}}}));
            }));
            var storyLogs=new List<string>(); void Prepare(Scene scene)=>StoryImages.Prepare(st,e,script,scene,true,st.Settings,CancellationToken.None,storyLogs.Add,http).GetAwaiter().GetResult();
            Prepare(script.Scenes[0]);Prepare(script.Scenes[1]);Check(calls==3&&referenced==2,"Ortak referans bir kez üretilmedi: calls="+calls+", referenced="+referenced+" logs="+string.Join(" | ",storyLogs));
            Check(script.Scenes.All(s=>File.Exists(s.StockFile)&&!s.RightsConfirmed&&s.AiImageHash!=""),"Sahne kaydı veya onay koruması yanlış");
            script.Scenes[0].RightsConfirmed=true;Prepare(script.Scenes[0]);Check(calls==3&&script.Scenes[0].RightsConfirmed,"Sağlam önbellek yeniden üretildi veya hak onayı kayboldu: calls="+calls+", rights="+script.Scenes[0].RightsConfirmed+" logs="+string.Join(" | ",storyLogs));
            File.WriteAllText(script.Scenes[1].StockFile,"corrupt");Prepare(script.Scenes[1]);Check(calls==4&&referenced==3,"Bozuk görsel tekrar hazırlanmadı");
            string oldHash=Reliability.MediaPlanHash(e);e.AiStoryMotion=false;Check(oldHash!=Reliability.MediaPlanHash(e),"Hareket ayarı eski çıktı onayını geçersiz kılmadı");
            script.Scenes[1].Narration="Hasan arkadaşına su verir.";Prepare(script.Scenes[1]);Check(calls==5&&referenced==4,"Metin değişiminde karakter referansı yenilendi");
            e.AiStoryStyle="Suluboya";e.AiStoryIdentityHash=StoryImages.Identity(e);Prepare(script.Scenes[0]);Check(calls==7&&referenced==5,"Yeni tarz yeni referans oluşturmadı");
            using var stopped=new CancellationTokenSource();stopped.Cancel();bool cancelled=false;
            try{StoryImages.Prepare(st,e,script,script.Scenes[1],true,st.Settings,stopped.Token,_=>{},http).GetAwaiter().GetResult();}catch(OperationCanceledException){cancelled=true;}Check(cancelled&&calls==7,"İptal yeni istek gönderdi");
            var movie=Path.Combine(st.Root,"story-motion.mp4");Media.Exec(ffmpeg,["-y","-loop","1","-i",script.Scenes[0].StockFile,"-t","2","-vf",StoryImages.PhotoFilter(360,640,12,true),"-an","-c:v","libx264","-preset","ultrafast","-pix_fmt","yuv420p",movie],st.Root,CancellationToken.None).GetAwaiter().GetResult();
            MediaFormat.Video(st,movie,true,CancellationToken.None).GetAwaiter().GetResult();Check(Math.Abs(Media.Duration(st,movie,CancellationToken.None).GetAwaiter().GetResult()-2)<.2,"Hareket video süresini değiştirdi");
            e.PhotosOnly=true;Reject(()=>StoryImages.Validate(e));e.PhotosOnly=false;st.Settings.FreeOnly=true;Reject(()=>StoryImages.RequireReady(st,st.Settings));
        });
        Test("MUSIC-USER-CONTROL", "Müzik lisans formu ve onay gerektirmeden kullanılabilir",()=>{
            var st=New("music-user-control");var song=Path.Combine(st.Root,"song.wav");
            Media.Exec(st.Ffmpeg,["-y","-f","lavfi","-i","sine=frequency=440:duration=2",song],st.Root,CancellationToken.None).GetAwaiter().GetResult();
            var track=new MusicTrack{Enabled=true,File=song};track.Validate();track.License="Telifli • kullanım iznim yok";track.Validate();
            st.Settings.MusicPath=song;GlobalMusic.Resolve(st,false)!.Validate();
            using var usage=new MusicUsage("","","",false,350);Check(usage.Controls.Count==1&&usage.Controls[0] is Label,"Lisans formu hâlâ görünür");
            var copy=new MusicTrack{Enabled=true,File=song,Volume=double.NaN};Reject(copy.Validate);
            var plan=new ContentPlan{Long=new VideoScript{MusicTrack=track},Shorts=[new VideoScript{MusicTrack=new MusicTrack{Enabled=true}}]};TextEditSave.CompletePackageMusic(plan);Check(plan.Shorts[0].MusicTrack!.File==song,"Müzik onaysız Shorts için devralınmadı");
            Media.Exec(st.Ffmpeg,["-y","-f","lavfi","-i","anullsrc=r=44100:cl=mono","-i",song,"-filter_complex",EditorAudio.Mix(track,2,"0","1"),"-map","[a]","-t","2",Path.Combine(st.Root,"mixed.wav")],st.Root,CancellationToken.None).GetAwaiter().GetResult();
        });
        Test("AUDIO-ARCHIVE-AUTO-REFRESH", "Arşiv açılırken yeni müzikler bulunur ve kayıtlı bilgiler korunur",()=>{
            var st=New("audio-refresh");var library=new AudioLibrary(st);Directory.CreateDirectory(library.Folder);
            var original=Path.Combine(library.Folder,"old.mp3");File.WriteAllText(original,"fixture");
            library.Save([new AudioItem{File=original,Name="Kayıtlı",Source="Kaynak",License="Lisans",RightsConfirmed=true}]);
            Directory.CreateDirectory(Path.Combine(library.Folder,"alt"));File.WriteAllText(Path.Combine(library.Folder,"alt","new.WAV"),"fixture");File.WriteAllText(Path.Combine(library.Folder,"ignore.txt"),"fixture");
            var items=library.List(refresh:true);Check(items.Count==2,"Yeni dosya bulunamadı");Check(items[0].RightsConfirmed&&items[0].License=="Lisans","Kayıtlı izin kayboldu");Check(!items[1].RightsConfirmed,"Yeni dosyaya izin uyduruldu");
            Check(library.List(refresh:true).Count==2,"Tekrar açılış müziği çoğalttı");
        });
        Test("FLUX2-NATIVE-REFERENCE", "FLUX.2 boyutları ve yerel referansları IPAdapter olmadan doğru bağlanır",()=>{
            var st=New("flux2-reference");var workflow=Path.Combine(st.Root,"workflow.json");
            File.WriteAllText(workflow,"{\"1\":{\"class_type\":\"UNETLoader\",\"inputs\":{\"unet_name\":\"flux.safetensors\"}},\"2\":{\"class_type\":\"CLIPLoader\",\"inputs\":{\"clip_name\":\"qwen.safetensors\",\"type\":\"flux2\"}},\"3\":{\"class_type\":\"VAELoader\",\"inputs\":{\"vae_name\":\"vae.safetensors\"}},\"4\":{\"class_type\":\"CLIPTextEncode\",\"inputs\":{\"text\":\"{{ANKA_PROMPT}}\",\"clip\":[\"2\",0]}},\"5\":{\"class_type\":\"CLIPTextEncode\",\"inputs\":{\"text\":\"\",\"clip\":[\"2\",0]}},\"6\":{\"class_type\":\"EmptyFlux2LatentImage\",\"inputs\":{\"width\":1024,\"height\":1024,\"batch_size\":1}},\"8\":{\"class_type\":\"CFGGuider\",\"inputs\":{\"model\":[\"1\",0],\"positive\":[\"4\",0],\"negative\":[\"5\",0],\"cfg\":5}},\"7\":{\"class_type\":\"RandomNoise\",\"inputs\":{\"noise_seed\":0}},\"10\":{\"class_type\":\"Flux2Scheduler\",\"inputs\":{\"steps\":20,\"width\":1024,\"height\":1024}}}");
            var graph=LocalCoverEngine.Prepare(workflow,"two travellers",true);Check(graph["6"]!["inputs"]!["width"]!.GetValue<int>()==576&&graph["10"]!["inputs"]!["height"]!.GetValue<int>()==1024,"Flux boyutları uyuşmuyor");
            Check(!graph["10"]!["inputs"]!.AsObject().ContainsKey("batch_size"),"Scheduler'a desteklenmeyen parametre gönderildi");
            LocalCoverEngine.ApplyReference(graph,"cast.png",99,2,new[]{0,1});
            Check(graph["8"]!["inputs"]!["positive"]![0]!.GetValue<string>()=="anka_flux_reference_positive"&&graph["anka_flux_reference_positive"]!["inputs"]!["conditioning"]![0]!.GetValue<string>()=="4","Native referans bağlanmadı");
            Check(graph["anka_reference_scale"]!["inputs"]!["resolution_steps"]!.GetValue<int>()==1&&graph["7"]!["inputs"]!["noise_seed"]!.GetValue<long>()==99,"Referans ölçeği / tohum eksik");
            Check(!graph.Any(x=>x.Value?["class_type"]?.GetValue<string>()=="IPAdapterAdvanced"),"FLUX.2 yanlış IPAdapter motoruna gönderildi");
            StoryImages.RequireReady(st,new Settings{CoverLocalEnabled=true,CoverLocalWorkflow=workflow});
        });
        Test("COVER-REFERENCE-STYLE", "Önceki kapak referansı saklanır, Gemini'ye gönderilir ve yerel stil koşulu kullanılır",()=>{
            var st=New("cover-reference");var source=Path.Combine(st.Root,"previous.jpg");using(var b=new System.Drawing.Bitmap(512,320))b.Save(source,System.Drawing.Imaging.ImageFormat.Jpeg);
            string reference=CoverReference.Import(st,source);Check(reference!=source&&File.Exists(reference)&&File.Exists(source),"Önceki kapak korunmadı");
            st.Settings.FreeOnly=false;st.Settings.CoverPaidAllowed=true;st.Settings.CoverAiEnabled=true;st.Settings.CoverProviderOrder="Gemini";st.Settings.CoverReferenceImage=reference;st.Vault.Set("GEMINI_API_KEY","fixture-only");
            int calls=0;using var http=new HttpClient(new Handler(async req=>{
                calls++;Check(req.RequestUri!.Host=="generativelanguage.googleapis.com","Referanssız sağlayıcı kullanıldı");using var input=JsonDocument.Parse(await req.Content!.ReadAsStringAsync());var parts=input.RootElement.GetProperty("contents")[0].GetProperty("parts");
                Check(parts.GetArrayLength()==2&&parts[1].GetProperty("inlineData").GetProperty("data").GetString()==Convert.ToBase64String(File.ReadAllBytes(reference)),"Seçilen referans gönderilmedi");
                Check(parts[0].GetProperty("text").GetString()!.Contains("NEW topic"),"Yeni konuya göre üretim talimatı yok");
                return Ok(JsonSerializer.Serialize(new {candidates=new[]{new {content=new {parts=new[]{new {inlineData=new {data=Convert.ToBase64String(File.ReadAllBytes(reference))}}}}}}}));
            }));new CoverFallback(st,_=>{},http).Create(Path.Combine(st.Root,"new-cover.png"),st.Settings,"Yeni kıssa","Veren Kuş",true,CancellationToken.None).GetAwaiter().GetResult();Check(calls==1,"Başarılı bulut referansı tekrar üretildi");
            var copied=PackageArtwork.Snapshot(st.Settings);Check(copied.CoverReferenceImage==reference&&VideoQueue.Snapshot(st.Settings).CoverReferenceImage==reference,"Referans video ayarına taşınmadı");
            var graph=System.Text.Json.Nodes.JsonNode.Parse("{\"3\":{\"class_type\":\"KSampler\",\"inputs\":{}},\"4\":{\"class_type\":\"CheckpointLoaderSimple\",\"inputs\":{}}}")!.AsObject();
            LocalCoverEngine.ApplyReference(graph,"cover.png",12,styleReference:true);Check(graph["anka_identity"]!["inputs"]!["weight_type"]!.GetValue<string>()=="style transfer","Kapak yerine karakter kimliği koşulu seçildi");
            var e=new Episode{Topic="Test",IncludeLong=true,ShortCount=0,Plan=new(){Long=new(){Title="Test",Scenes=[new(){Narration="Test."}]}}};string before=Reliability.RenderHash(e.Plan.Long!,e,st.Settings);File.WriteAllText(reference,"changed");Check(before!=Reliability.RenderHash(e.Plan.Long!,e,st.Settings),"Değişmiş referans eski render önbelleğini kullanabilir");
        });
        Test("LOCAL-WORKFLOW-REDIRECTED-DOCUMENTS", "Yönlendirilmiş Belgeler boşken fiziksel Documents altındaki motor bulunur",()=>{
            var st=New("workflow-discovery");var redirected=Path.Combine(st.Root,"OneDrive","Belgeler","Codex");var physical=Path.Combine(st.Root,"Documents","Codex");Directory.CreateDirectory(redirected);
            var folder=Path.Combine(physical,"2026-10-06","y","work","local-cover-engine");Directory.CreateDirectory(Path.Combine(folder,"ComfyUI_windows_portable","python_embeded"));
            var workflow=Path.Combine(folder,"anka-sdxl.json");File.WriteAllText(workflow,"{}");File.WriteAllText(Path.Combine(folder,"ComfyUI_windows_portable","python_embeded","python.exe"),"fixture");Directory.CreateDirectory(Path.Combine(folder,"ComfyUI_windows_portable","ComfyUI"));File.WriteAllText(Path.Combine(folder,"ComfyUI_windows_portable","ComfyUI","main.py"),"fixture");
            Check(LocalCoverEngine.FindInstalledWorkflow(new[]{redirected,physical})==workflow,"Belgeler yönlendirmesi motorun bulunmasını engelledi");
        });
        Test("COVER-CLOUD-LOCAL-ORDER", "Seçili yerel kapak motoru bulut kotasından önce denenir; başarısında bulut çağrılmaz",()=>{
            foreach(string scenario in new[]{"failure","success","quota"}) {
                var st=New("cover-order-"+scenario);st.Vault.Set("NVIDIA_API_KEY","fixture-only");var settings=st.Settings;settings.CoverAiEnabled=settings.CoverLocalEnabled=true;settings.CoverProviderOrder="NVIDIA";settings.CoverMonthlyRequests=scenario=="quota"?0:50;
                var workflow=Path.Combine(st.Root,"workflow.json");File.WriteAllText(workflow,"{\"6\":{\"class_type\":\"CLIPTextEncode\",\"inputs\":{\"text\":\"{{ANKA_PROMPT}}\"}}}");settings.CoverLocalWorkflow=workflow;
                byte[] png;using(var b=new System.Drawing.Bitmap(320,320)){using var ms=new MemoryStream();b.Save(ms,System.Drawing.Imaging.ImageFormat.Png);png=ms.ToArray();}int cloud=0,local=0;
                using var http=new HttpClient(new Handler(req=>{
                    if(req.RequestUri!.Host=="ai.api.nvidia.com"){cloud++;Check(local==1,"Yerel motor önce denenmedi");return Task.FromResult(scenario=="success"?Ok(JsonSerializer.Serialize(new{artifacts=new[]{new{base64=Convert.ToBase64String(png)}}})):new HttpResponseMessage(HttpStatusCode.Forbidden));}
                    if(req.RequestUri.AbsolutePath=="/prompt"){local++;Check(cloud==0,"Seçili yerel motor bulut kotasının arkasında kaldı");return Task.FromResult(Ok("{\"prompt_id\":\"local-cover\"}"));}
                    if(req.RequestUri.AbsolutePath=="/history/local-cover")return Task.FromResult(Ok("{\"local-cover\":{\"outputs\":{\"9\":{\"images\":[{\"filename\":\"cover.png\",\"subfolder\":\"\",\"type\":\"output\"}]}}}}"));
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(png)});
                }));
                var output=Path.Combine(st.Root,"cover.png");new CoverFallback(st,_=>{},http).Create(output,settings,"Kıssa","Veren Kuş Ol",true,CancellationToken.None).GetAwaiter().GetResult();
                Check(File.Exists(output)&&File.Exists(Path.Combine(st.Root,"ai-cover-background.png")),"AI kapak yerine şablon kullanıldı");Check(local==1&&cloud==0,"Yerel üretim gereksiz bulut isteği yaptı");
                var episode=new Episode{AiStoryImages=true,AiStoryLocal=true};var inherited=PackageArtwork.Effective(new Settings{CoverLocalWorkflow=workflow},episode);Check(inherited.CoverLocalEnabled&&inherited.CoverAiEnabled,"Hikâye yerel tercihi kapağa aktarılmadı");
                episode.ArtworkSettings=new Settings{CoverLocalEnabled=false};Check(!PackageArtwork.Effective(settings,episode).CoverLocalEnabled,"Kaydedilmiş kapak tercihi ezildi");
            }
        });
        Test("STORY-UI-PERSISTENCE", "Proje ekranından yerel hikâye seçimi ve karakter tanımı kaydedilir",()=>{
            var st=New("story-ui");var e=new Episode{Topic="Kıssa testi",IncludeLong=false,ShortCount=1};
            using var dialog=new PackageDialog(e,st);IEnumerable<Control> Children(Control c)=>c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(Children(x)));
            var controls=Children(dialog).ToList();var story=controls.OfType<CheckBox>().Single(x=>x.Text.StartsWith("Hikâyeyi AI"));var local=controls.OfType<CheckBox>().Single(x=>x.Text.StartsWith("Yerel motoru kullan"));
            Check(!story.Checked&&local.Checked,"Eski projede AI açıldı veya yerel varsayılan kapandı");story.Checked=true; controls.OfType<CheckBox>().Single(x=>x.Text.StartsWith("Durmadan üret")).Checked=true;
            var style=controls.OfType<TextBox>().Single(x=>x.Text==e.AiStoryStyle);style.Text="Sinematik tarihî resim";
            dialog.Show();Application.DoEvents();controls.OfType<Button>().Single(x=>x.Text=="Paketi kaydet").PerformClick();Application.DoEvents();
            Check(dialog.Episode.AiStoryImages&&dialog.Episode.AiStoryLocal&&dialog.Episode.AiStoryStyle=="Sinematik tarihî resim"&&dialog.DialogResult==DialogResult.OK,"Hikâye seçenekleri kayboldu");
            var json=JsonSerializer.Serialize(dialog.Episode,Json.Options);var reopened=JsonSerializer.Deserialize<Episode>(json,Json.Options)!;Check(reopened.AiStoryImages&&reopened.AiStoryMotion&&reopened.AiStoryCinematic&&reopened.AiStoryAutonomous&&reopened.AiStoryLocal,"Yeniden açılışta seçenekler korunmadı");
        });
        Test("STORY-VISUAL-PLAN", "Görsel plan eksik sahneyi düzeltir, metni korur, önbellek ve sabit karakter tanımını kullanır",()=>{
            var st=New("story-visual-plan");st.Settings.ProviderOrder="Groq";st.Settings.GroqModel="openai/gpt-oss-120b";st.Vault.Set("GROQ_API_KEY","fixture-only");
            var script=new VideoScript{Scenes=[new(){Narration="Yolcu kuşu besler.",VisualQuery="bird"},new(){Narration="Dostuyla konuşur.",VisualQuery="travellers"}]};
            var e=new Episode{Topic="İyilik",IncludeLong=false,ShortCount=1,AiStoryImages=true,Plan=new(){Shorts=[script]}};st.Episodes.Add(e);
            int calls=0;using var http=new HttpClient(new Handler(async req=>{
                calls++;using var input=JsonDocument.Parse(await req.Content!.ReadAsStringAsync());Check(input.RootElement.GetProperty("response_format").GetProperty("json_schema").GetProperty("schema").GetProperty("properties").TryGetProperty("Cast",out _),"Görsel plan yerine anlatım şeması gönderildi");
                string content=JsonSerializer.Serialize(new {Cast="old traveller white beard brown robe",Scenes=calls==1?new[]{new{Id=0,Prompt="traveller feeds bird"}}:new[]{new{Id=0,Prompt="traveller feeds bird"},new{Id=1,Prompt="travellers talking"}}});
                return Ok(JsonSerializer.Serialize(new {choices=new[]{new {finish_reason="stop",message=new {content}}}}));
            }));
            var services=new Services(st,_=>{},http);StoryImages.Plan(st,e,services,CancellationToken.None,_=>{}).GetAwaiter().GetResult();Check(calls==2&&script.Scenes.All(s=>s.AiImagePrompt!=""),"Eksik görsel plan düzeltilmedi");
            StoryImages.Plan(st,e,services,CancellationToken.None,_=>{}).GetAwaiter().GetResult();Check(calls==2&&script.Scenes[0].Narration=="Yolcu kuşu besler.","Önbellek veya hazır metin koruması bozuldu");
            string cast=e.AiStoryCastPrompt;script.Scenes[1].Narration="Dostuna su verir.";StoryImages.Plan(st,e,services,CancellationToken.None,_=>{}).GetAwaiter().GetResult();Check(calls==3&&e.AiStoryCastPrompt==cast,"Metin değişiminde karakter tanımı değişti");
        });
        Test("STORY-SHORT-RENDER", "AI sahnesi gerçek dikey MP4, özgün ses ve işlenmiş altyazıyla üretilir; tekrar görsel çağrısı yoktur",()=>{
            var st=New("story-short-render");st.Settings.EncodingPreset="ultrafast";st.Settings.FramesPerSecond=12;st.Settings.CoverAiEnabled=false;st.Settings.OpeningCardEnabled=st.Settings.ClosingCardEnabled=false;
            st.Settings.VoiceProvider="Piper / ücretsiz";st.Settings.PiperPath="missing.exe";st.Settings.PiperModel="missing.onnx";st.Settings.FreeOnly=false;st.Settings.CoverPaidAllowed=true;st.Vault.Set("GEMINI_API_KEY","fixture-only");
            var e=new Episode{Topic="Çölde iyilik",IncludeLong=false,ShortCount=1,Music=false,AiStoryImages=true,AiStoryAutonomous=true,AiStoryLocal=false,UserNarration=new(){Enabled=true,Shorts=["İki yolcu çölde birbirine yardım eder."],VisualTheme="desert travellers"}};
            st.Episodes.Add(e);e.Plan=UserNarration.Plan(e);var script=e.Plan.Shorts[0];e.AiStoryCastPrompt="two Persian travellers, brown and blue robes";foreach(var s in script.Scenes)s.AiImagePrompt="two travellers helping each other in desert";e.AiStoryPromptHash=TimedNarration.TextHash(StoryImages.Context(e));e.AiStoryIdentityHash=StoryImages.Identity(e);
            byte[] png;using(var b=new System.Drawing.Bitmap(360,640)){using var g=System.Drawing.Graphics.FromImage(b);g.Clear(System.Drawing.Color.SandyBrown);using var ms=new MemoryStream();b.Save(ms,System.Drawing.Imaging.ImageFormat.Png);png=ms.ToArray();}
            int images=0;using var http=new HttpClient(new Handler(req=>{images++;return Task.FromResult(Ok(JsonSerializer.Serialize(new {candidates=new[]{new {content=new {parts=new[]{new {inlineData=new {data=Convert.ToBase64String(png)}}}}}}})));}));
            foreach(var scene in script.Scenes)StoryImages.Prepare(st,e,script,scene,true,st.Settings,CancellationToken.None,_=>{},http).GetAwaiter().GetResult();
            int prepared=images;var seed=Path.Combine(st.Folder(e),"r"+e.Revision+"-short-1","seed");Directory.CreateDirectory(seed);
            Task Speak(string text,string path,Settings voice,CancellationToken ct)=>Media.Exec(ffmpeg,["-y","-f","lavfi","-i","sine=frequency=440:duration=3",path],st.Root,ct);
            NarrationFlow.Prepare(st,e,script,st.Settings,seed,new Services(st,_=>{}),CancellationToken.None,Speak).GetAwaiter().GetResult();
            new Engine(st).Produce(e).GetAwaiter().GetResult();Check(e.Error==""&&e.Videos.Count==1&&e.Videos[0].QualityPassed,"AI Shorts üretimi başarısız: "+e.Error);
            var asset=e.Videos[0];MediaFormat.Video(st,asset.File,true,CancellationToken.None).GetAwaiter().GetResult();
            Check(images==prepared&&Math.Abs(asset.Duration-3)<.4,"Görsel yeniden üretildi veya doğal ses süresi değişti");
            Check(File.ReadAllText(Path.Combine(Path.GetDirectoryName(asset.File)!,"subtitles.srt")).Contains("İki yolcu"),"Türkçe altyazı eksik");
            Check(!e.Approved&&script.Scenes.All(s=>!s.RightsConfirmed),"AI içerik kendiliğinden yayın onayı aldı");
        });
        Test("STORY-LOCAL-REFERENCE", "Yerel SDXL referansı yükler, sabit tohum ve IPAdapter kimlik koşulunu kullanır; bulut çağrısı yoktur",()=>{
            var st=New("story-local");var workflow=Path.Combine(st.Root,"workflow.json");
            File.WriteAllText(workflow,"{\"3\":{\"class_type\":\"KSampler\",\"inputs\":{\"seed\":0,\"latent_image\":[\"5\",0],\"denoise\":1}},\"4\":{\"class_type\":\"CheckpointLoaderSimple\",\"inputs\":{\"ckpt_name\":\"sd_xl_base_1.0.safetensors\"}},\"5\":{\"class_type\":\"EmptyLatentImage\",\"inputs\":{\"width\":576,\"height\":1024}},\"6\":{\"class_type\":\"CLIPTextEncode\",\"inputs\":{\"text\":\"{{ANKA_PROMPT}}\"}}}");
            byte[] png;using(var b=new System.Drawing.Bitmap(320,320)){using var ms=new MemoryStream();b.Save(ms,System.Drawing.Imaging.ImageFormat.Png);png=ms.ToArray();}
            var reference=Path.Combine(st.Root,"reference.png");File.WriteAllBytes(reference,png);int uploads=0,submitted=0;long seed=0;
            using var http=new HttpClient(new Handler(async req=>{
                Check(req.RequestUri!.Host=="127.0.0.1","Yerel mod buluta çıktı");
                if(req.RequestUri.AbsolutePath=="/upload/image"){uploads++;Check(req.Content is MultipartFormDataContent,"Referans yüklemesi eksik");return Ok("{\"name\":\"characters.png\"}");}
                if(req.RequestUri.AbsolutePath=="/prompt"){submitted++;using var doc=JsonDocument.Parse(await req.Content!.ReadAsStringAsync());var graph=doc.RootElement.GetProperty("prompt");var sampler=graph.GetProperty("3").GetProperty("inputs");Check(sampler.GetProperty("latent_image")[0].GetString()=="5"&&sampler.GetProperty("model")[0].GetString()=="anka_identity"&&sampler.GetProperty("denoise").GetDouble()==1,"Karakter referansı yeni sahneye bağlanmadı");long value=sampler.GetProperty("seed").GetInt64();if(submitted==1)seed=value;Check(value==seed,"Referans tohumu değişti");Check(graph.GetProperty("anka_reference").GetProperty("inputs").GetProperty("image").GetString()=="characters.png","Yüklenen referans bağlanmadı");return Ok("{\"prompt_id\":\"test\"}");}
                if(req.RequestUri.AbsolutePath=="/history/test")return Ok("{\"test\":{\"outputs\":{\"9\":{\"images\":[{\"filename\":\"scene.png\",\"subfolder\":\"\",\"type\":\"output\"}]}}}}");
                Check(req.RequestUri.AbsolutePath=="/view","Bilinmeyen yerel çağrı");return new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(png)};
            }));
            for(int i=0;i<2;i++){var result=LocalCoverEngine.Generate(workflow,"same scene",true,_=>{},CancellationToken.None,http,referenceImage:reference).GetAwaiter().GetResult();Check(result.SequenceEqual(png),"Yerel çıktı bozuldu");}
            Check(uploads==2&&submitted==2,"Her sahne aynı referansla gönderilmedi");
            var regional=LocalCoverEngine.ApplyReference(LocalCoverEngine.Prepare(workflow,"two people",true),"cast.png",7,2,new[]{0,1},768);
            Check(regional["anka_face_0"]!["inputs"]!["x"]!.GetValue<int>()==0&&regional["anka_face_1"]!["inputs"]!["x"]!.GetValue<int>()==384,"Karakter referansları ayrılmadı");
            Check(regional["anka_mask_1"]!["inputs"]!["x"]!.GetValue<int>()==288&&regional["anka_identity"]!["inputs"]!["attn_mask"]![0]!.GetValue<string>()=="anka_mask_1","İkinci karakter ayrı sahne alanına bağlanmadı");
            var settings=new Settings{CoverLocalEnabled=true,CoverLocalWorkflow=workflow,FreeOnly=true};StoryImages.RequireReady(st,settings);
            File.WriteAllText(workflow,"{}");Reject(()=>StoryImages.RequireReady(st,settings));
        });
        Test("STORY-LOCAL-CANCEL", "İptal yalnız ilgili yerel iş kimliğini durdurur; genel motor kesilmez",()=>{
            var st=New("story-local-cancel");var workflow=Path.Combine(st.Root,"workflow.json");File.WriteAllText(workflow,"{\"6\":{\"class_type\":\"CLIPTextEncode\",\"inputs\":{\"text\":\"{{ANKA_PROMPT}}\"}}}");
            using var cancel=new CancellationTokenSource();int cleanup=0;using var http=new HttpClient(new Handler(async req=>{
                if(req.RequestUri!.AbsolutePath=="/prompt"){cancel.CancelAfter(80);return Ok("{\"prompt_id\":\"our-story-job\"}");}
                if(req.Method==HttpMethod.Get&&req.RequestUri.AbsolutePath=="/queue")return Ok("{\"queue_running\":[[1,\"our-story-job\"]]}");
                using var body=JsonDocument.Parse(await req.Content!.ReadAsStringAsync());
                if(req.RequestUri.AbsolutePath=="/queue")Check(body.RootElement.GetProperty("delete")[0].GetString()=="our-story-job","Başka kuyruk işi silindi");
                else{Check(req.RequestUri.AbsolutePath=="/interrupt"&&body.RootElement.GetProperty("prompt_id").GetString()=="our-story-job","Motor genel olarak kesildi");}
                cleanup++;return Ok("{}");
            }));
            bool stopped=false;try{LocalCoverEngine.Generate(workflow,"scene",true,_=>{},cancel.Token,http).GetAwaiter().GetResult();}catch(OperationCanceledException){stopped=true;}
            Check(stopped&&cleanup==2,"İptalde yerel hesaplama durdurulmadı");
        });
        Test("STORY-STRICT-FAILURE", "Görsel API başarısızken stok veya kapak şablonu kullanılmaz; OpenAI referansı taşınır",()=>{
            var st=New("story-failure");st.Settings.FreeOnly=false;st.Settings.CoverPaidAllowed=true;st.Settings.CoverProviderOrder="OpenAI";st.Vault.Set("OPENAI_API_KEY","fixture-only");
            var reference=Path.Combine(st.Root,"reference.png");using(var b=new System.Drawing.Bitmap(320,320))b.Save(reference,System.Drawing.Imaging.ImageFormat.Png);
            int calls=0;using var http=new HttpClient(new Handler(async req=>{calls++;Check(req.RequestUri!.AbsolutePath=="/v1/images/edits","Referans düzenleme yerine yeni üretim çağrıldı");Check(req.Content is MultipartFormDataContent,"Referans multipart değil");string body=await req.Content!.ReadAsStringAsync();Check(body.Contains("characters.png")&&body.Contains("image/png"),"Referans istekte yok");return new HttpResponseMessage(HttpStatusCode.Forbidden);}));
            var output=Path.Combine(st.Root,"failed.png");Reject(()=>new CoverFallback(st,_=>{},http).Create(output,st.Settings,"story","",true,CancellationToken.None,"scene",reference).GetAwaiter().GetResult());
            Check(calls==3&&!File.Exists(output),"Hatalı AI sahnesi şablonla başarılı gösterildi");
        });
        Test("OAUTH-LOOPBACK-CALLBACK", "Boş ön bağlantı ve geçersiz state onay sayfasını kilitlemez", () => {
            Task.Run(async()=>{
                using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(20));
                var listener=new System.Net.Sockets.TcpListener(IPAddress.Loopback,0);listener.Start();
                try{
                    int port=((IPEndPoint)listener.LocalEndpoint).Port;string redirect=$"http://127.0.0.1:{port}/";
                    var pending=OAuthLoopback.Receive(listener,redirect,"expected-state",deadline.Token);
                    using var http=new HttpClient {Timeout=TimeSpan.FromSeconds(12)};
                    using(var rejected=await http.GetAsync(redirect+"?state=wrong&code=fixture",deadline.Token))Check(rejected.StatusCode==HttpStatusCode.BadRequest,"Geçersiz state kabul edildi");
                    using var idle=new System.Net.Sockets.TcpClient();await idle.ConnectAsync(IPAddress.Loopback,port,deadline.Token);
                    var body=await http.GetStringAsync(redirect+"?state=expected-state&code=fixture-code&scope=scope-one%20scope-two",deadline.Token);
                    var query=await pending;
                    Check(query["code"]=="fixture-code"&&query["scope"]=="scope-one scope-two","Google dönüşü ayrıştırılamadı");
                    Check(body.Contains("Google onayı")&&!body.Contains("fixture-code"),"Onay sayfası boş veya onay kodunu gösteriyor");
                }finally{deadline.Cancel();listener.Stop();}
            }).GetAwaiter().GetResult();
        });
        Test("YOUTUBE-STATISTICS", "Video türleri, toplamlar ve analitik izin hatasında izlenmeler korunur", () => {
            var st=New("youtube-statistics");st.Settings.GoogleClientId="fixture-client";st.Vault.Set("YOUTUBE_REFRESH_TOKEN","fixture-refresh");st.Vault.Set("GOOGLE_CLIENT_SECRET","fixture-secret");
            bool allowAnalytics=false;
            using var client=new HttpClient(new Handler(req=>{
                var uri=req.RequestUri!;string body;
                if(uri.Host=="oauth2.googleapis.com")body="{\"access_token\":\"fixture-token\"}";
                else if(uri.AbsolutePath.EndsWith("/channels"))body="{\"items\":[{\"id\":\"channel\",\"snippet\":{\"title\":\"Test Kanal\"},\"statistics\":{\"viewCount\":\"3000\",\"subscriberCount\":\"900\",\"hiddenSubscriberCount\":false},\"contentDetails\":{\"relatedPlaylists\":{\"uploads\":\"uploads\"}}}]}";
                else if(uri.AbsolutePath.EndsWith("/playlistItems"))body="{\"items\":[{\"contentDetails\":{\"videoId\":\"normal\"}},{\"contentDetails\":{\"videoId\":\"short\"}}]}";
                else if(uri.AbsolutePath.EndsWith("/videos"))body="{\"items\":[{\"id\":\"normal\",\"snippet\":{\"title\":\"Uzun hikâye\",\"publishedAt\":\"2026-01-01T00:00:00Z\"},\"statistics\":{\"viewCount\":\"2000\",\"likeCount\":\"20\",\"commentCount\":\"4\"},\"status\":{\"privacyStatus\":\"public\"}},{\"id\":\"short\",\"snippet\":{\"title\":\"Kısa hikâye\",\"publishedAt\":\"2026-02-01T00:00:00Z\"},\"statistics\":{\"viewCount\":\"1000\"},\"status\":{\"privacyStatus\":\"public\"}}]}";
                else if(uri.Host=="youtubeanalytics.googleapis.com"){
                    if(!allowAnalytics)return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden){Content=new StringContent("{}")});
                    var query=Uri.UnescapeDataString(uri.Query);
                    Check(!query.Contains("filters=creatorContentType")&&!query.Contains("dimensions=video,creatorContentType"),"Desteklenmeyen içerik türü sorgusu gönderildi");
                    body=query.Contains("filters=video==")?query.Contains("video==short")?"{\"rows\":[[\"SHORTS\",100]]}":"{\"rows\":[[\"VIDEO_ON_DEMAND\",200]]}":query.Contains("metrics=engagedViews")?"{\"rows\":[[\"SHORTS\",400]]}":!query.Contains("dimensions=video")?"{\"rows\":[[\"VIDEO_ON_DEMAND\",1200]]}":"{\"rows\":[[\"short\",100,60,80],[\"normal\",200,120,50]]}";
                }else throw new InvalidOperationException("Beklenmeyen istatistik isteği");
                return Task.FromResult(Ok(body));
            }));
            var fallback=new YouTubeStatistics(st,client).Fetch(28,_=>{},CancellationToken.None).GetAwaiter().GetResult();
            Check(fallback.Videos.Count==2 && fallback.ChannelViews==3000 && fallback.NormalWatchHours365==null && fallback.Note.Contains("alınamadı"),"Analitik izin hatası temel verileri kaybettirdi");
            Check(fallback.Videos.All(v=>v.Kind=="Belirlenmedi"),"Süreye bakarak Shorts türü uyduruldu");
            allowAnalytics=true;var full=new YouTubeStatistics(st,client).Fetch(28,_=>{},CancellationToken.None).GetAwaiter().GetResult();
            Check(full.Videos.Single(v=>v.Id=="short").Kind=="Shorts" && full.NormalWatchHours365==20 && full.ShortsEngagedViews90==400,"Analitik tür veya saat hesabı hatalı");
            Check(YouTubeStatistics.Recommend(full).Contains("Uzun hikâye"),"Veriye dayalı öneri üretilmedi");
            st.Settings.YouTubeChannelId=full.ChannelId;Json.Write(Path.Combine(st.Root,"cache","youtube-channel-statistics.json"),full);
            using(var form=new StudioForm(st)){
                var method=typeof(StudioForm).GetMethod("ShowChannelStatistics",BindingFlags.NonPublic|BindingFlags.Instance,null,[typeof(Action<Form>)],null)!;
                method.Invoke(form,[new Action<Form>(dialog=>{try{var tab=dialog.Controls.OfType<TabControl>().Single();Check(tab.TabPages.Count==3,"İstatistik sekmeleri eksik");var grid=tab.TabPages[0].Controls.OfType<DataGridView>().Single();Check(grid.Rows.Count==2,"İstatistik tablosu dolmadı");dialog.PerformLayout();using var image=new Bitmap(dialog.Width,dialog.Height);dialog.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(st.Root,"statistics-preview.png"));}finally{dialog.Close();}})]);
            }
            var ep=new Episode {Videos=[new AssetVideo {YouTubeId="public",YouTubeState="Yayında"},new AssetVideo{YouTubeId="private",YouTubeState="Gizli",Short=true},new AssetVideo{YouTubeId="future",YouTubeState="Planlı bekliyor",Short=true}]};
            var totals=PublicationStatistics.Count([ep]);Check(totals==(1,0,1),"Gizli veya planlı videolar yayınlanan sayısına katıldı");
        });
        Test("COMPACT-MENUS-LOCAL-MOTOR", "Sade menüler ve yerel motor ayarlarının üretime aktarılması", () => {
            var st=New("compact-menus");
            var configured=new Settings {CoverLocalEnabled=true,CoverLocalWorkflow="fixture-workflow.json",CoverMonthlyRequests=5};
            var snapshot=PackageArtwork.Snapshot(configured);
            Check(snapshot.CoverLocalEnabled && snapshot.CoverLocalWorkflow==configured.CoverLocalWorkflow,"Yerel motor ayarları kopyalanırken kayboldu");
            var ep=new Episode {Topic="Sade menü",Plan=new ContentPlan {Long=new VideoScript {Title="Sade menü"}},ArtworkSettings=snapshot};
            var effective=PackageArtwork.Effective(st.Settings,ep);
            Check(effective.CoverLocalEnabled && effective.CoverLocalWorkflow==configured.CoverLocalWorkflow,"Kayıtlı motor ayarları uygulanmadı");
            using var editor=new PackageCoverEditor(st,ep);
            IEnumerable<Control> All(Control c)=>c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(All(x)));
            Check(All(editor).OfType<TabPage>().Any(t=>t.Text=="Motor / API"),"Motor ayarları bölümü bulunamadı");
            using var project=new PackageDialog(ep,st);
            Check(All(project).OfType<StudioDisclosure>().Any(d=>d.Controls.Count>0),"Proje ayrıntıları gruplanmadı");
            st.Episodes.Add(ep);
            using var form=new StudioForm(st);
            var navigate=typeof(StudioForm).GetMethod("Navigate",BindingFlags.NonPublic|BindingFlags.Instance)!;
            foreach(var route in new[]{"Stüdyo","Video hazırla","Videolarım","Ayarlar","Gelişmiş","Medya arşivleri","Stok havuzu","Ses arşivi","Kapak tasarımları","Bağlantı Merkezi","Yayın takvimi","Üretim ve yayın planı"}) navigate.Invoke(form,[route]);
            using var logoDesigner=new AiDesignDialog(st,true,"Test kanal");
            Check(logoDesigner.SavedPath=="" && AiDesignDialog.LogoFolder(st)!=CoverArchive.Folder(st),"Logo ve kapak arşivleri ayrılmadı");
            var source=new Episode {Topic="Tasarım denemesi",IncludeLong=true,ShortCount=0}; source.Plan=Engine.DemoPlan(source);
            using var text=new TextEditor(st,source);
        });
        Test("COVER-STATUS-SCROLL-SOUND", "Kapak durum satırı tuşun üstündedir; odak kaydırmaz ve klik sesi etkinleşir", () => {
            var st = New("cover-status-scroll-sound"); st.Settings.UiSounds=false; st.Settings.UiSoundVolume=0;
            var ep = new Episode { Topic="Kapak testi", Plan=new ContentPlan { Long=new VideoScript { Title="TEST" } } };
            using var editor = new PackageCoverEditor(st,ep); editor.Show(); Application.DoEvents();
            IEnumerable<Control> All(Control c) => c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(All(x)));
            var generate = All(editor).OfType<Button>().First(x=>x.Text.StartsWith("TASARLA"));
            var flow = (StudioFlowPanel)generate.Parent!;
            var index=flow.Controls.IndexOf(generate);
            Check(index>0 && flow.Controls[index-1] is Label status && status.Text.Contains("TASARLA"),"Durum satırı tuşun üstünde değil");
            flow.AutoScrollPosition=new Point(0,0); Application.DoEvents(); var before=flow.AutoScrollPosition;
            flow.Controls.OfType<Button>().Last().Focus(); Application.DoEvents();
            Check(flow.AutoScrollPosition==before,"Odak en alta kaydırdı");
            Check(UiSound.Enabled && UiSound.Volume>=.5,"Klik sesi kapalı kaldı");
            st.Settings.UiSounds=false; UiSound.Configure(st.Settings); Check(!UiSound.Enabled,"Sonraki ses tercihi korunmadı");
            editor.Close();
        });
        Test("COVER-MODEL-FALLBACK", "NVIDIA modelleri sırayla denenir; başarıda durur, tümü başarısızsa otomatik kapak oluşur", () => {
            var st = New("cover-model-fallback"); st.Vault.Set("NVIDIA_API_KEY", "fixture-cover-nvidia");
            var settings = new Settings { FreeOnly = true, CoverPaidAllowed = false, CoverAiEnabled = true, CoverBackground = "old-stock-image.png", CoverAutoStyle = false, CoverProviderOrder = "NVIDIA", CoverMonthlyRequests = 50, CoverShowChannelName = false };
            using var bitmap = new Bitmap(512,512); using var memory = new MemoryStream(); bitmap.Save(memory,System.Drawing.Imaging.ImageFormat.Png);
            var payload = JsonSerializer.Serialize(new { artifacts = new[] { new { base64 = Convert.ToBase64String(memory.ToArray()) } } });
            var requests = new List<string>();
            using var http = new HttpClient(new Handler(async req => {
                requests.Add(req.RequestUri!.AbsolutePath);
                Check(req.Headers.Authorization?.Parameter == st.Key("NVIDIA_API_KEY"), "NVIDIA anahtarı gönderilmedi");
                using var body = JsonDocument.Parse(await req.Content!.ReadAsStringAsync());
                Check(body.RootElement.GetProperty("width").GetInt32()==1024,"NVIDIA ölçüsü yanlış");
                if (requests.Count==1) return new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{}") };
                Check(body.RootElement.TryGetProperty("text_prompts",out _),"SDXL isteği yanlış");
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(payload) };
            }));
            var successDir = Path.Combine(st.Root,"success"); Directory.CreateDirectory(successDir);
            var successLogs = new List<string>();
            new CoverFallback(st,successLogs.Add,http).Create(Path.Combine(successDir,"cover.png"),settings,"Sabır","SABIR",false,CancellationToken.None).GetAwaiter().GetResult();
            Check(requests.Count==2 && requests[0].EndsWith("flux.1-schnell") && requests[1].EndsWith("stable-diffusion-xl"),"Model sırası veya başarıda durma yanlış");
            Check(File.Exists(Path.Combine(successDir,"ai-cover-background.png")),"Başarılı yapay zekâ görseli kayboldu: " + string.Join(";",successLogs));
            requests.Clear(); var logs = new List<string>();
            settings.FreeOnly = false; settings.CoverPaidAllowed = true;
            st.Vault.Set("GEMINI_API_KEY", "fixture-cover-gemini"); st.Vault.Set("OPENAI_API_KEY", "fixture-cover-openai");
            using var failedHttp = new HttpClient(new Handler(req=> { requests.Add(req.RequestUri!.AbsolutePath); return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("{}") }); }));
            var failDir = Path.Combine(st.Root,"failure"); Directory.CreateDirectory(failDir);
            new CoverFallback(st,logs.Add,failedHttp).Create(Path.Combine(failDir,"cover.png"),settings,"Sabır","SABIR",false,CancellationToken.None).GetAwaiter().GetResult();
            Check(requests.Count==8 && File.Exists(Path.Combine(failDir,"cover.png")) && !File.Exists(Path.Combine(failDir,"ai-cover-background.png")),"Tüm modeller denenmeden otomatik kapak oluştu");
            Check(logs.Any(x=>x.Contains("otomatik kapak seçiliyor")),"Otomatik kapak geçişi açıklanmadı");
        });
        var fixtureStore = New("fixture"); var video = Path.Combine(fixtureStore.Root, "sea.mp4");

        Media.Exec(ffmpeg, ["-y", "-f", "lavfi", "-i", "color=c=blue:s=160x90:r=10", "-f", "lavfi", "-i", "anullsrc=r=44100:cl=mono", "-t", "1", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-c:a", "aac", video], fixtureStore.Root, CancellationToken.None).GetAwaiter().GetResult();

        Test("DISCLOSURE-NAV", "Kanal ve program grupları ihtiyaç duyulduğunda hazırlanır; gezinme veri değiştirmez",()=>{
            var st=New("disclosure-nav");UiSound.Configure(st.Settings);using var form=new StudioForm(st);form.Show();
            var before=JsonSerializer.Serialize(new{st.Settings,st.Episodes},Json.Options);typeof(StudioForm).GetMethod("Navigate",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(form,["Yönetim ayarları"]);
            IEnumerable<Control> All(Control c)=>c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(All(x)));
            var groups=All(form).OfType<StudioDisclosure>().Where(d=>d.Controls.OfType<Button>().Any(b=>new[]{"Arayüz / ses","Güvenlik","Kanal / araştırma","Google TTS kota","Ses / müzik","Stok havuzu","Görüntü / altyazı","Otonomi / denetim"}.Any(n=>b.AccessibleName==n+" bölümünü aç veya kapat"))).ToArray();
            Check(groups.Length==8&&groups.Count(g=>g.Expanded)==1,"Kompakt ayar grupları eksik.");Check(groups.Count(g=>g.Content.Controls.Count>0)==1,"Kapalı ayar grupları açılışta hazırlanıyor.");
            foreach(var group in groups){All(form).OfType<ComboBox>().Single(x=>x.AccessibleName=="Ayar bölümü").SelectedItem=group.Controls.OfType<Button>().First().Text;Application.DoEvents();Check(group.Expanded&&group.Content.Controls.Count>0&&groups.Count(g=>g.Expanded)==1,"Grup hazırlanmadı veya diğerleri kapanmadı.");}
            Check(before==JsonSerializer.Serialize(new{st.Settings,st.Episodes},Json.Options),"Ayarları açmak veriyi değiştirdi.");
        });
        Test("SOCIAL-PUBLISH", "Sosyal paylaşım onayı, aktarım, durum ve tekrar gönderim koruması", () => {
            var st = New("social"); var ep = new Episode(); st.Episodes.Add(ep);
            var a = new SocialAccount { Platform = "TikTok", Id = "test", Name = "fixture" }; st.Settings.SocialAccounts.Add(a); st.Vault.Set(SocialPublisher.TokenKey(a), "private-test-token");
            int init = 0, uploads = 0;
            using var client = new HttpClient(new Handler(async req => {
                var path = req.RequestUri!.AbsolutePath;
                if (path.EndsWith("creator_info/query/")) return Ok("{\"error\":{\"code\":\"ok\"},\"data\":{\"creator_nickname\":\"fixture\",\"privacy_level_options\":[\"SELF_ONLY\"],\"max_video_post_duration_sec\":60,\"comment_disabled\":true,\"duet_disabled\":true,\"stitch_disabled\":true}}");
                if (path.EndsWith("video/init/")) { init++; using var body = JsonDocument.Parse(await req.Content!.ReadAsStringAsync()); Check(body.RootElement.GetProperty("post_info").GetProperty("disable_comment").GetBoolean(), "Hesap kısıtlaması uygulanmadı"); return Ok("{\"error\":{\"code\":\"ok\"},\"data\":{\"publish_id\":\"receipt\",\"upload_url\":\"https://upload.tiktokapis.com/fixture\"}}"); }
                if (req.Method == HttpMethod.Put) { uploads++; Check(req.Headers.Authorization == null && req.Content!.Headers.ContentRange!.From == 0, "Aktarım başlığı yanlış"); return new HttpResponseMessage(HttpStatusCode.OK); }
                if (path.EndsWith("status/fetch/")) return Ok("{\"error\":{\"code\":\"ok\"},\"data\":{\"status\":\"PUBLISH_COMPLETE\"}}");
                throw new Exception("Beklenmeyen istek: " + path);
            }));
            var publisher = new SocialPublisher(st, client);
            Reject(() => publisher.Publish(ep, a, video, 1, "test", "SELF_ONLY", true, true, true, false, false, [], CancellationToken.None).GetAwaiter().GetResult());
            Reject(() => publisher.Publish(ep, a, video, 1, "test", "PUBLIC_TO_EVERYONE", true, true, true, false, true, [], CancellationToken.None).GetAwaiter().GetResult());
            Check(init == 0 && ep.SocialPosts.Count == 0, "Onaysız gönderim başladı");
            var record = publisher.Publish(ep, a, video, 1, "test", "SELF_ONLY", true, true, true, false, true, [], CancellationToken.None).GetAwaiter().GetResult();
            Check(init == 1 && uploads == 1 && record.State == "PROCESSING", "Gönderim sonucu yanlış");
            publisher.Check(a, record, CancellationToken.None).GetAwaiter().GetResult(); Check(record.State == "PUBLISHED", "Durum doğrulanmadı");
            Reject(() => publisher.Publish(ep, a, video, 1, "test", "SELF_ONLY", false, false, false, false, true, [], CancellationToken.None).GetAwaiter().GetResult());
            Check(init == 1, "İkinci paylaşım oluşturuldu");
            var chunks = SocialPublisher.Chunks(21L * 1024 * 1024); Check(chunks.Count == 2 && chunks.Size == 10L * 1024 * 1024, "Son parça hesabı yanlış");
            Check(!JsonSerializer.Serialize(st.Settings).Contains("private-test-token"), "Anahtar ayarlara sızdı");
            new SocialConnections(st).Remove(a); Check(st.Vault.Get(SocialPublisher.TokenKey(a)) == "", "Bağlantı anahtarı silinmedi");
        });
        Test("SOCIAL-FACEBOOK", "Facebook normal video aktarımı, sonuç takibi ve anahtar yenileme", () => {
            var st = New("facebook"); var ep = new Episode(); st.Episodes.Add(ep); var a = new SocialAccount { Platform = "Facebook", Id = "page" }; st.Vault.Set(SocialPublisher.TokenKey(a), "fixture");
            using var client = new HttpClient(new Handler(async req => {
                if (req.Method == HttpMethod.Post) { Check(req.RequestUri!.Host == "graph-video.facebook.com" && req.Content is MultipartFormDataContent, "Normal video yanlış akışa gönderildi"); Check((await req.Content!.ReadAsStringAsync()).Contains("source"), "Dosya gönderilmedi"); return Ok("{\"id\":\"video\"}"); }
                return Ok("{\"status\":{\"publishing_phase\":{\"status\":\"complete\"}}}");
            }));
            var publisher = new SocialPublisher(st, client); var record = publisher.Publish(ep, a, video, 1, "caption", "", false, false, false, false, true, [], CancellationToken.None, reel: false).GetAwaiter().GetResult();
            Check(record.State == "PROCESSING", "İşleme yayına eşitlendi"); publisher.Check(a, record, CancellationToken.None).GetAwaiter().GetResult(); Check(record.State == "PUBLISHED", "Facebook sonucu yanlış");
            a.Platform = "TikTok"; a.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1); st.Settings.SocialBrokerUrl = "https://example.com"; st.Vault.Set(SocialPublisher.RefreshKey(a), "old-refresh");
            using var renew = new HttpClient(new Handler(_ => Task.FromResult(Ok("{\"access_token\":\"new-token\",\"refresh_token\":\"new-refresh\",\"expires_in\":3600}"))));
            Check(new SocialPublisher(st, renew).Token(a, CancellationToken.None).GetAwaiter().GetResult() == "new-token" && st.Vault.Get(SocialPublisher.RefreshKey(a)) == "new-refresh", "Yenilenen anahtar kayboldu");
        });
        Test("SOCIAL-INSTAGRAM", "Instagram işleme tamamlanmadan yayınlamaz ve aynı kabı yeniden yayınlamaz", () => {
            var st = New("instagram"); var ep = new Episode(); st.Episodes.Add(ep); var a = new SocialAccount { Platform = "Instagram", Id = "account" }; st.Vault.Set(SocialPublisher.TokenKey(a), "fixture"); int published = 0;
            using var client = new HttpClient(new Handler(req => {
                Check(req.RequestUri!.Host == "graph.instagram.com", "Instagram yanlış sunucuya yönlendi");
                if (req.RequestUri.AbsolutePath.EndsWith("/media_publish")) { published++; return Task.FromResult(Ok("{\"id\":\"published\"}")); }
                if (req.Method == HttpMethod.Get) return Task.FromResult(Ok("{\"status_code\":\"FINISHED\"}"));
                return Task.FromResult(Ok("{\"id\":\"container\"}"));
            }));
            var publisher = new SocialPublisher(st, client); var record = publisher.Publish(ep, a, "", 0, "caption", "", false, false, false, false, true, ["https://example.com/photo.jpg"], CancellationToken.None).GetAwaiter().GetResult();
            Check(record.State == "PROCESSING" && published == 0, "Hazır olmayan içerik yayınlandı");
            publisher.Check(a, record, CancellationToken.None).GetAwaiter().GetResult(); publisher.Check(a, record, CancellationToken.None).GetAwaiter().GetResult(); Check(published == 1 && record.State == "PUBLISHED", "Tek yayın koruması yanlış");
        });
        Test("XTTS-EXTERNAL", "Harici XTTS Türkçe WAV, güvenli adres, hata ve önbellek ayrımı", () => {
            var st = New("xtts"); var cfg = st.Settings; cfg.XttsSpeaker = "my-voice"; cfg.VoiceProvider = "XTTS v2 / harici";
            var wav = Path.Combine(st.Root, "fixture.wav");
            Media.Exec(ffmpeg, ["-y", "-f", "lavfi", "-i", "sine=frequency=300:duration=0.2", wav], st.Root, CancellationToken.None).GetAwaiter().GetResult();
            var data = File.ReadAllBytes(wav); int calls = 0;
            using var client = new HttpClient(new Handler(async req => {
                calls++; Check(req.RequestUri!.AbsolutePath == "/tts_to_audio/", "XTTS endpoint yanlış");
                using var doc = JsonDocument.Parse(await req.Content!.ReadAsStringAsync());
                Check(doc.RootElement.GetProperty("language").GetString() == "tr" && doc.RootElement.GetProperty("speaker_wav").GetString() == "my-voice", "Dil / ses yanlış");
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(data) };
            }));
            var output = Path.Combine(st.Root, "result.wav");
            new XttsTts(client).Speak("Türkçe deneme", output, cfg, CancellationToken.None).GetAwaiter().GetResult();
            Check(File.ReadAllBytes(output).SequenceEqual(data) && calls == 1, "Ses kaydı değişti");
            cfg.XttsUrl = "http://example.com"; Reject(() => XttsTts.Endpoint(cfg)); cfg.XttsUrl = "http://127.0.0.1:8020";
            using var bad = new HttpClient(new Handler(_ => Task.FromResult(Ok("{\"error\":\"secret\"}"))));
            Reject(() => new XttsTts(bad).Speak("test", output + ".bad", cfg, CancellationToken.None).GetAwaiter().GetResult());
            Check(!File.Exists(output + ".bad"), "Hatalı yanıt ses diye kaydedildi");
            var script = new VideoScript(); var first = NarrationFlow.Fingerprint(script, cfg); cfg.XttsSpeaker = "another-voice";
            Check(first != NarrationFlow.Fingerprint(script, cfg), "Ses değişince önbellek yenilenmiyor");
            var saved = VideoQueue.Effective(new Settings(), cfg); Check(saved.XttsSpeaker == cfg.XttsSpeaker && saved.XttsUrl == cfg.XttsUrl, "Proje ses ayarı kayboldu");
        });

        Test("BUTTON-INFO-ACTION", "Bilgi simgesine gerçek fare tıklaması açıklama açar, asıl işlemi başlatmaz",()=>{

            foreach(var name in new[]{"Tamam","Kapat","Vazgeç","Evet","Hayır","Kopyala","Yapıştır","Kaydet","Rehber","Ana ekran","İşlem kayıtları","İzle","Düzenle","Aç","Ayarlar","+ Yeni video","Proje durumlarını kontrol et / güncelle","Yüklenen açıklamaları güncelle","Shorts tasarım kapağını aç"}) {

                using var plain=(StudioButton)Theme.Button(name,()=>{});Check(!plain.HasHelp,"Gereksiz bilgi simgesi: "+name);

            }

            using var form=new Form{Width=450,Height=200};int actions=0,popups=0;

            var button=(StudioButton)Theme.Button("Şimdi yayınla",()=>actions++,true,300);form.Controls.Add(button);form.Show();Application.DoEvents();

            using var close=new System.Windows.Forms.Timer{Interval=50};close.Tick+=(_,_)=>{var popup=Application.OpenForms.Cast<Form>().FirstOrDefault(f=>f.Text=="Şimdi yayınla • bilgi");if(popup!=null){popups++;popup.Close();}};close.Start();

            var bounds=button.HelpBounds;int point=((bounds.Y+bounds.Height/2)<<16)|(bounds.X+bounds.Width/2);

            SendHelpMessage(button.Handle,0x0201,new IntPtr(1),new IntPtr(point));SendHelpMessage(button.Handle,0x0202,IntPtr.Zero,new IntPtr(point));close.Stop();

            Check(popups==1 && actions==0,"Bilgi tıklaması işlem başlattı veya açıklama açmadı.");button.PerformClick();Check(actions==1,"Asıl tuş çalışmıyor.");

        });

        Test("GUIDE-MENU-CONTENT", "Rehber çalışma klasörünün altında; kapsamlı içerik ve arama mevcut",()=>{

            Check(UserGuide.Sections.Count>=27 && UserGuide.FullText.Length>18000,"Rehber kapsamı eksik.");

            var st=New("guide-menu");using var form=new StudioForm(st);

            IEnumerable<Control> Children(Control c)=>c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(Children(x)));

            var guide=Children(form).OfType<Button>().Single(b=>b.Text=="Rehber");Check(guide.Parent!=null,"Rehber ana menüden erişilemiyor.");

            using var window=UserGuide.Create();var search=Children(window).OfType<TextBox>().Single(t=>t.AccessibleName=="Rehberde ara");var list=Children(window).OfType<ListBox>().Single();search.Text="müzik";Check(list.Items.Count>0 && list.Items.Count<UserGuide.Sections.Count,"Rehber filtrelemesi hatalı.");

            Check(Children(form).OfType<StudioButton>().All(b=>b.HelpText.Length>20),"Bilgi açıklaması eksik.");

        });

        ContentPlan Plan() => new() { Sources = [new() { Title = "Kaynak A", Url = "https://example.com/a" }, new() { Title = "Kaynak B", Url = "https://example.com/b" }], Long = new() { Title = "Yeni senaryo", Description = "Konu özeti", Scenes = [new() { Narration = "Herkes burada.", VisualQuery = "sea", StockFile = video, RightsConfirmed = true }] } };

        Episode Episode(Store st) { var e = new Episode { Topic = "Test paketi", ShortCount = 0, Plan = Plan() }; var file = Path.Combine(st.Root, "video.mp4"); File.Copy(video, file, true); e.Videos.Add(new() { Key = "long", File = file, QualityPassed = true, FileHash = Reliability.FileHash(file), PlanHash = Reliability.MediaPlanHash(e, "long") }); st.Episodes.Add(e); st.Settings.YouTubeChannelId = "fixture-channel"; return e; }

        if (Program.Arg(args,"--alignment-fixture") is string fixture) Test("LOCAL-REAL-AUDIO-SYNC", "Gerçek 49 saniyelik ses kısa satırlara kelime zamanlarıyla eşlenir; ses değişmez ve sonuç önbellekten gelir", () => {

            var st=New("real-audio-alignment");using var request=JsonDocument.Parse(File.ReadAllText(fixture));string audio=Path.Combine(st.Root,new string('a',90),new string('b',90),"voice.wav");Directory.CreateDirectory(Path.GetDirectoryName(audio)!);Check(audio.Length>260,"Uzun yol testi gerçekten uzun değil");File.Copy(request.RootElement.GetProperty("Audio").GetString()!,audio,true);

            var text=request.RootElement.GetProperty("Texts").EnumerateArray().Select(x=>x.GetString()!).ToArray();double duration=request.RootElement.GetProperty("Duration").GetDouble();string before=Reliability.FileHash(audio);

            var captions=LocalAudioAlignment.Align(audio,text,duration,_=>{},CancellationToken.None).GetAwaiter().GetResult();

            Check(captions.Count==text.Length && captions[0].End<5 && captions[^1].End>duration-2 && captions.Select(c=>c.Text).SequenceEqual(text),"Gerçek ses zamanları veya özgün metin korunmadı");

            Check(before==Reliability.FileHash(audio),"Ses verisi değiştirildi");var again=LocalAudioAlignment.Align(audio,text,duration,_=>{},CancellationToken.None).GetAwaiter().GetResult();Check(again.SequenceEqual(captions),"Kelime zamanı önbelleği değişti");

        });

        Test("JSON-TRANSIENT-READ-LOCK", "Kısa süreli dış okuma kilidi atomik kaydı bozmaz; önceki yedek korunur", () => {
            var path=Path.Combine(root,"transient-read-lock.json");Json.Write(path,new { Value=1 });
            var reader=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
            var release=Task.Run(()=>{Thread.Sleep(120);reader.Dispose();});
            try { Json.Write(path,new { Value=2 }); } finally { release.GetAwaiter().GetResult();reader.Dispose(); }
            using var current=JsonDocument.Parse(File.ReadAllText(path));using var previous=JsonDocument.Parse(File.ReadAllText(path+".bak"));
            Check(current.RootElement.GetProperty("Value").GetInt32()==2 && previous.RootElement.GetProperty("Value").GetInt32()==1,"Kayıt veya önceki yedek değişti");
        });

        Test("COMBO-VIEWPORT-CLIP", "Kaydırılan açılır kutu kapsayıcı dışında çizilmez; görünür alanda boyanır", () => {
            using var window = new Form { Width=480, Height=360, BackColor=Theme.Bg };
            var viewport=new Panel { Location=new Point(30,60), Size=new Size(300,120), BackColor=Theme.Panel };
            var combo=Theme.Combo(["Örnek seçim"],"Örnek seçim",200);combo.Location=new Point(10,150);viewport.Controls.Add(combo);window.Controls.Add(viewport);
            window.Show();Application.DoEvents();using(var image=new Bitmap(window.Width,window.Height)){window.DrawToBitmap(image,new Rectangle(Point.Empty,window.Size));var point=window.PointToClient(combo.PointToScreen(new Point(150,12)));point.Offset(window.PointToScreen(Point.Empty).X-window.Left,window.PointToScreen(Point.Empty).Y-window.Top);Check(image.GetPixel(point.X,point.Y).ToArgb()==Theme.Bg.ToArgb(),"Açılır kutu panel dışına boyandı");}
            combo.Top=30;Application.DoEvents();using(var image=new Bitmap(combo.Width,combo.Height)){combo.DrawToBitmap(image,new Rectangle(Point.Empty,combo.Size));Check(image.GetPixel(140,12).ToArgb()==Theme.Elevated.ToArgb(),"Görünür kutunun yüzü kayboldu");}
            window.Close();
        });

        Test("WORKSPACE-PHASE-STATUS", "Beş adımda tamamlanan tasarım yeşildir; plan silindiğinde sıfırlanır",()=>{
            var st=New("phase-state");var e=Episode(st);using var form=new StudioForm(st);
            IEnumerable<Control> Children(Control c)=>c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(Children(x)));
            void Open()=>typeof(StudioForm).GetMethod("Navigate",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(form,["Video hazırla"]);
            StudioButton Design()=>Children(form).OfType<StudioButton>().Single(b=>b.AccessibleName=="Üretim adımı: Tasarım");
            Open();Check(Design().Completed&&Design().BackColor==Theme.SuccessSurface&&Design().ForeColor==Theme.Text,"Tamamlanan adım yeşil/siyah değil.");
            Check(Children(form).OfType<Button>().Count(b=>b.AccessibleName?.StartsWith("Üretim adımı:")==true)==5,"Beş üretim adımı eksik.");e.Plan=null;Open();Check(!Design().Completed,"Plan silinince tasarım tamamlandı kaldı.");
        });

        Test("MODERN-SCROLL-PRESERVES-CONTENT", "Kaydırma görünümü yatay/dikey konumu ve metin içeriğini korur", () => {

            using var form=new Form {Width=440,Height=340};

            var panel=new Panel {Dock=DockStyle.Fill,AutoScroll=true};var content=new Panel {Width=900,Height=1200};panel.Controls.Add(content);form.Controls.Add(panel);

            form.Show();form.PerformLayout();panel.AutoScrollPosition=new Point(100,180);var before=panel.AutoScrollPosition;

            ModernScrollbars.Apply(form);using var image=new System.Drawing.Bitmap(form.Width,form.Height);form.DrawToBitmap(image,new Rectangle(Point.Empty,form.Size));

            Check(before.X<0 && before.Y<0 && before==panel.AutoScrollPosition,"Kaydırma konumu değişti");
            Check(ModernScrollbars.CornerUsesTheme(panel),"Yatay/dikey kaydırma köşesinde siyah blok kaldı.");

            Check(ModernScrollbars.HoverLeaveRetainsTheme(panel),"Fare ayrılınca veya animasyon beklenince gri çubuk görünümü korunmadı");

            var box=Theme.Input(string.Join("\r\n",Enumerable.Repeat("Özgün içerik",80)),true,300);box.Height=100;content.Controls.Add(box);box.Select(3,5);var text=box.Text;ModernScrollbars.Apply(box);box.DrawToBitmap(image,new Rectangle(0,0,box.Width,box.Height));Check(box.Text==text && box.SelectionStart==3 && box.SelectionLength==5,"Metin veya seçim değişti");form.Close();

        });

        Test("ORGANIZED-NAV-DATA", "Üç ana menüde gezinme mevcut proje ve varsayılan ayarları değiştirmez", () => {

            var st=New("organized-nav"); var e=Episode(st); e.ScenesReviewed=null;UiSound.Configure(st.Settings);

            string before=JsonSerializer.Serialize(new {st.Settings,st.Episodes},Json.Options);

            using var form=new StudioForm(st);

            foreach(string route in new[]{"Stüdyo","Video hazırla","Videolarım","Üretim ve yayın planı","Yayın takvimi","Ayarlar","Bağlantı Merkezi","Medya arşivleri","Gelişmiş","İşlem kayıtları","Program kaynakları"})typeof(StudioForm).GetMethod("Navigate",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(form,[route]);

            var afterNavigation=JsonSerializer.Serialize(new {st.Settings,st.Episodes},Json.Options);if(before!=afterNavigation){File.WriteAllText(Path.Combine(st.Root,"before.json"),before);File.WriteAllText(Path.Combine(st.Root,"after.json"),afterNavigation);}
            Check(before==JsonSerializer.Serialize(new {st.Settings,st.Episodes},Json.Options),"Gezinme proje verisini değiştirdi");

            foreach(var pair in new[]{("Yönetim ayarları","Ayarlar"),("Kapak tasarımları","Ayarlar"),("Yayın takvimi","Videolarım"),("İçerik paketleri","Videolarım")})Check(StudioForm.NavigationRoot(pair.Item1)==pair.Item2,"Eski bölüm kayboldu");

        });

        Test("SCENE-REVIEW-READONLY", "Eski sahne inceleme kaydı gösterilirken veri değişmez ve yeniden üretim incelemeyi sıfırlar", () => {

            var e=new Episode();e.Events.Add("Sahneler düzenlendi;");Check(StudioForm.ScenesAreReviewed(e),"Eski inceleme gösterilmedi");Check(e.ScenesReviewed==null,"Görüntüleme kayıt değiştirdi");

            e.Events.Add("Sahne incelemesi sıfırlandı.");Check(!StudioForm.ScenesAreReviewed(e),"Yeniden üretim eski incelemeyi korudu");

        });

        Test("DIRECT-QUEUE-NO-GITHUB", "Yayın sırası yalnız YouTube bağlantısı ister; GitHub hesabı/deposu gerekmez", () => {

            var st = New("direct-queue"); var old = Environment.GetEnvironmentVariable("YOUTUBE_REFRESH_TOKEN");

            try {

                Environment.SetEnvironmentVariable("YOUTUBE_REFRESH_TOKEN", "fixture"); st.Settings.YouTubeChannelId = "fixture-channel"; st.Settings.Repo = "";

                VideoQueue.RequireYouTube(st);

                st.Settings.YouTubeChannelId = ""; Reject(() => VideoQueue.RequireYouTube(st)); st.Settings.YouTubeChannelId = "fixture-channel";

                Environment.SetEnvironmentVariable("YOUTUBE_REFRESH_TOKEN", ""); Reject(() => VideoQueue.RequireYouTube(st));

            } finally { Environment.SetEnvironmentVariable("YOUTUBE_REFRESH_TOKEN", old); }

        });

        Test("DIRECT-VIDEO-UI", "Video ekranlarında bulut üretim/yükleme tuşu yok; doğrudan yayın akışı gösterilir", () => {

            var st = New("direct-video-ui"); var e = Episode(st); using var form = new StudioForm(st);

            IEnumerable<Control> Children(Control c) => c.Controls.Cast<Control>().SelectMany(x => new[] { x }.Concat(Children(x)));

            void Open(string name) => typeof(StudioForm).GetMethod("Navigate", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(form, [name]);

            Open("İçerik paketleri"); Check(!Children(form).OfType<Button>().Any(b => b.Text.Contains("GitHub'da üret")), "Bulut üretim tuşu kaldı");

            Open("Üretim ve yayın planı"); Check(Children(form).OfType<Label>().Any(l => l.Text.Contains("doğrudan YouTube’a planlı yüklenir")), "Doğrudan kuyruk açıklaması yok");

            Open("Program kaynakları");var sourcePage=(Control)typeof(StudioForm).GetField("page",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(form)!; Check(!Children(sourcePage).OfType<Button>().Any(b => b.Text.Contains("videoyu") || b.Text.Contains("paketi") || b.Text.Contains("anahtarı")), "Kaynak ekranında video veya hizmet anahtarı aktarımı kaldı");

            Check(e.Videos.Count == 1 && e.Videos[0].File != "", "Mevcut proje değişti");

        });

        Test("WORD-SYNC-INVALIDATES-OLD-VIDEO", "Yeni kelime eşleme eski tamamlanmış MP4 önbelleğini geçersiz sayar; ses kimliği korunur",()=> {

            var st=New("word-sync-render-version");var e=Episode(st);var script=e.Plan!.Long!;string voice= NarrationFlow.Fingerprint(script,st.Settings);

            string old=Reliability.RenderHash(script,e,st.Settings,"reliable-render-v3"), current=Reliability.RenderHash(script,e,st.Settings);

            Check(old!=current,"Eski video güncel sayıldı; ses eşleme atlanacak");

            Check(voice==NarrationFlow.Fingerprint(script,st.Settings),"Render sürümü ses önbelleğini değiştirdi");

        });

        Test("PACKAGE-ARCHIVE-GROUPS", "Üretilen planlanan yayınlanan paketler ayrılır; sorunlu ve eksik paketler aktif kalır", () => {

            var e = Episode(New("archive-groups"));

            Check(PackageArchive.Group(e) == "Tamamlanmış projeler", "Tamamlanmış ayrılmadı");

            e.Videos[0].YouTubeId = "fixture"; e.Videos[0].Scheduled = true; e.Videos[0].YouTubeState = "Planlı bekliyor";

            Check(PackageArchive.Group(e) == "Planlanmış projeler", "Planlanan ayrılmadı");

            e.Videos[0].Scheduled = false; e.Videos[0].YouTubeState = "Yayında";

            Check(PackageArchive.Group(e) == "Yayına alınmış projeler", "Yayınlanan ayrılmadı");

            e.Videos[0].YouTubeState = "Sorun: failed"; Check(PackageArchive.Group(e) == "", "Sorun arşive gizlendi");

            e.Videos.Clear(); Check(PackageArchive.Group(e) == "", "Eksik çıktı arşive gizlendi");

        });

        Test("PUBLISHED-QUEUE-RECONCILE", "Doğrulanmış yayınlanan ve planlanan video bekleyen kuyruğundan çıkar", () => {

            var st = New("published-queue"); var e = Episode(st); e.QueueOrder=1; e.QueueState="Bekliyor"; e.QueueAuthorized=true; e.PublishAt=DateTimeOffset.UtcNow.AddHours(-2);

            Check(VideoQueue.Items(st).Count()==1,"Sadece tarih geçti diye kuyruk gizlendi");

            e.Videos[0].YouTubeId="fixture"; e.Videos[0].YouTubeState="Yayında";

            Check(!VideoQueue.Items(st).Any(),"Yayınlanan yeniden üretim kuyruğunda kaldı");

            VideoQueue.Reconcile(st); Check(e.QueueState=="Yayına alındı" && !e.QueueAuthorized && e.Status=="Yayında" && PackageArchive.Group(e)=="Yayına alınmış projeler","Yayın durumu uzlaştırılmadı");

            e.Videos[0].YouTubeState="Planlı bekliyor"; VideoQueue.Reconcile(st); Check(e.QueueState=="Planlandı" && !VideoQueue.Items(st).Any(),"Planlanan tekrar kuyruğa girdi");

            e.Videos[0].YouTubeState="Bulunamadı"; Check(VideoQueue.Items(st).Count()==1,"Silinen video saklandı");

        });

        Test("AUDIO-REAL-FOUR-LINE-RESPONSE", "Gerçek hatadaki dört parça grubu sesin süresinde kalır ve yerel kayıttan kurtarılır", () => {

            var st=New("real-four-line"); string audio=Path.Combine(st.Root,"voice.wav"); File.WriteAllBytes(audio,[1,2,3]); string[] texts=["a","b","c","d","e","f","g"];

            const string raw="{\"Captions\":[{\"Id\":0,\"LastId\":2,\"Start\":0.0,\"End\":10.662},{\"Id\":3,\"LastId\":6,\"Start\":10.662,\"End\":21.579}]}";

            var cues=AudioSubtitleAlignment.Parse(raw,texts,21.6); Check(cues.Count==2 && cues[1].Text=="d e f g" && cues[1].End==21.579,"Gerçek geçerli grup reddedildi");

            string cache=audio+".alignment-"+TimedNarration.TextHash("audio-alignment-v2"+Reliability.FileHash(audio)+string.Join('\n',texts)+st.Settings.GeminiModel)+".json";

            File.WriteAllText(cache+".last-response.json",raw); int calls=0; using var http=new HttpClient(new Handler(req=>{calls++;throw new Exception("API çağrılmamalı");}));

            string before=Reliability.FileHash(audio); var recovered=AudioSubtitleAlignment.Align(st,audio,texts,21.6,http,_=>{},CancellationToken.None).GetAwaiter().GetResult();

            Check(calls==0 && recovered.Count==2 && before==Reliability.FileHash(audio) && File.Exists(cache),"Ses veya ağ kullanılarak gereksiz yeniden işlem yapıldı");

            Reject(()=>AudioSubtitleAlignment.Parse("{\"Captions\":[{\"Id\":0,\"LastId\":7,\"Start\":0,\"End\":21.579}]}",texts,21.6));

            Reject(()=>AudioSubtitleAlignment.Parse("{\"Captions\":[{\"Id\":0,\"LastId\":6,\"Start\":0,\"End\":24}]}",texts,21.6));

        });

        Test("AUDIO-COMPRESSED-TIMES-REJECT", "49 saniyelik sesin bütün altyazısının 16 saniyede bitmesi başarı sayılmaz",()=> {

            Reject(()=>AudioSubtitleAlignment.Parse("{\"Captions\":[{\"Id\":0,\"Start\":0,\"End\":8},{\"Id\":1,\"Start\":8,\"End\":16.54}]}",["ilk bölüm","son bölüm"],49.32));

        });

        Test("AUDIO-ALIGNMENT-RANGES", "Birleşen altyazı tüm özgün metni kapsar; eksik veya çakışan aralık reddedilir", () => {

            string[] text=["Birinci satır","ikinci satır","üçüncü satır"];

            var cues=AudioSubtitleAlignment.Parse("{\"Captions\":[{\"Id\":0,\"LastId\":1,\"Start\":0.2,\"End\":3},{\"Id\":2,\"Start\":3.3,\"End\":5}]}",text,5);

            Check(cues.Count==2 && cues[0].Text=="Birinci satır ikinci satır" && cues[1].Text==text[2] && cues[1].Start==3.3,"Metin veya gerçek zaman değişti");

            Reject(()=>AudioSubtitleAlignment.Parse("{\"Captions\":[{\"Id\":0,\"Start\":0,\"End\":2}]}",text,5));

            Reject(()=>AudioSubtitleAlignment.Parse("{\"Captions\":[{\"Id\":0,\"LastId\":1,\"Start\":0,\"End\":3},{\"Id\":2,\"Start\":2.9,\"End\":5}]}",text,5));

        });

        Test("FORMAT-GUARDS", "Yanlış oran kapak ve videoda reddedilir; döndürme ve piksel oranı okunur", () => {

            var st=New("format-guards");var portrait=Path.Combine(st.Root,"portrait.mp4");

            Media.Exec(ffmpeg,["-y","-f","lavfi","-i","color=c=red:s=90x160:r=10","-t","1","-c:v","libx264","-pix_fmt","yuv420p",portrait],st.Root,CancellationToken.None).GetAwaiter().GetResult();

            MediaFormat.Video(st,video,false,CancellationToken.None).GetAwaiter().GetResult();

            MediaFormat.Video(st,portrait,true,CancellationToken.None).GetAwaiter().GetResult();

            Reject(()=>MediaFormat.Video(st,video,true,CancellationToken.None).GetAwaiter().GetResult());

            Reject(()=>MediaFormat.Video(st,portrait,false,CancellationToken.None).GetAwaiter().GetResult());

            foreach(var vertical in new[]{false,true}) {var file=Path.Combine(st.Root,vertical?"vertical.png":"horizontal.png");using(var image=new Bitmap(vertical?941:1672,vertical?1672:941))image.Save(file);MediaFormat.Image(file,vertical);Reject(()=>MediaFormat.Image(file,!vertical));}

            Reject(()=>MediaFormat.Dimensions(1024,1024,true,"Kare"));Reject(()=>MediaFormat.Dimensions(1536,1024,false,"3:2"));

            using var rotated=JsonDocument.Parse("{\"width\":1920,\"height\":1080,\"side_data_list\":[{\"rotation\":-90}]}");MediaFormat.Probe(rotated.RootElement,true,"Döndürülmüş");Reject(()=>MediaFormat.Probe(rotated.RootElement,false,"Döndürülmüş"));

            using var anamorphic=JsonDocument.Parse("{\"width\":720,\"height\":576,\"sample_aspect_ratio\":\"64:45\"}");MediaFormat.Probe(anamorphic.RootElement,false,"Anamorfik");

            var e=new Episode{IncludeLong=true,ShortCount=1,UserNarration=new(){ManualVisuals=true,LocalVideos=[video]}};Reject(()=>MediaFormat.Manual(st,e,CancellationToken.None).GetAwaiter().GetResult());

            e.UserNarration.ShortVideos=[new(){UseSeparate=true,Files=[portrait]}];MediaFormat.Manual(st,e,CancellationToken.None).GetAwaiter().GetResult();

            var shared=new Settings{OpeningCardEnabled=true,OpeningCardFile=Path.Combine(st.Root,"horizontal.png")};

            var shortScript=new VideoScript{CoverSettings=PackageArtwork.Snapshot(new Settings{CoverMode="Kendi PNG kapak",CoverPng=Path.Combine(st.Root,"vertical.png")})};

            var episode=new Episode{ArtworkSettings=PackageArtwork.Snapshot(shared)};

            Check(PackageArtwork.Effective(new Settings(),episode,shortScript).OpeningCardEnabled,"Eski kayıtta açılış sessizce kapandı");

            Reject(()=>ClosingCards.Validate(PackageArtwork.Effective(new Settings(),episode,shortScript),true));

            shortScript.CoverSettings.ScriptCardsOverride=true;shortScript.CoverSettings.OpeningCardEnabled=true;shortScript.CoverSettings.OpeningCardFile=shortScript.CoverSettings.CoverPng;

            var effective=PackageArtwork.Effective(new Settings(),episode,shortScript);ClosingCards.Validate(effective,true);using(var cover=CoverDesigner.Draw(effective,"Test",true))Check(cover.Height>cover.Width,"Shorts kapağı yatay");

            Reject(()=>CoverDesigner.Draw(new Settings{CoverMode="Kendi PNG kapak",CoverPng=shared.OpeningCardFile},"Test",true));

            var saved=JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(PackageArtwork.Snapshot(effective),Json.Options),Json.Options)!;

            // Effective output inherits the shared gate; persist the per-script snapshot, not the resolved output.

            saved=JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(PackageArtwork.Snapshot(shortScript.CoverSettings),Json.Options),Json.Options)!;

            Check(saved.ScriptCardsOverride && saved.OpeningCardFile==shortScript.CoverSettings.CoverPng,"Ayrı kart seçimi kayıtta kayboldu");

            episode.Plan=new ContentPlan{Long=new VideoScript{Title="Normal",Scenes=[new(){Narration="Metin"}]},Shorts=[shortScript]};shortScript.Title="Shorts";shortScript.Scenes=[new(){Narration="Kısa metin"}];episode.ShortCount=1;st.Episodes.Add(episode);

            using(var editor=new PackageCoverEditor(st,episode))editor.Save();st.Save();var loaded=new Store(st.Root).Episodes.Single();

            Check(loaded.Plan!.Shorts[0].CoverSettings!.ScriptCardsOverride,"Editör ayrı kart seçimini kaydetmedi");

            var shortArt=PackageArtwork.Effective(st.Settings,loaded,loaded.Plan.Shorts[0]);ClosingCards.Validate(shortArt,true);Check(shortArt.OpeningCardFile!=loaded.ArtworkSettings!.OpeningCardFile,"Normal ve Shorts açılış dosyaları karıştı");

        });

        Test("PACKAGE-COVER-EDITOR", "Paket kapakları ayrı kaydedilir; ses ayarı korunur ve render güncellenir", () => {

            var st=New("package-covers");var e=Episode(st);e.ShortCount=1;e.Plan!.Shorts.Add(new VideoScript{Title="Kısa kapak",ThumbnailText="Kısa başlık",Scenes=[new(){Narration="Kısa metin"}]});

            st.Settings.CoverStyle="Minimal";st.Settings.VoiceSpeed=1.1;

            e.Plan.Long!.CoverSettings=PackageArtwork.Snapshot(new Settings{CoverStyle="Manevi",CoverAutoStyle=false});

            e.Plan.Shorts[0].CoverSettings=PackageArtwork.Snapshot(new Settings{CoverStyle="Belgesel",CoverAutoStyle=false});

            var before=Reliability.MediaPlanHash(e);using var editor=new PackageCoverEditor(st,e);editor.Save();

            Check(e.ArtworkSettings!=null && e.ArtworkPending && e.TextDesignPending && !e.Approved,"Paket değişikliği kaydedilmedi");

            Check(st.Settings.CoverStyle=="Minimal" && PackageArtwork.Effective(st.Settings,e,e.Plan.Long).VoiceSpeed==1.1 && PackageArtwork.Effective(st.Settings,e,e.Plan.Long).CoverStyle=="Manevi" && PackageArtwork.Effective(st.Settings,e,e.Plan.Shorts[0]).CoverStyle=="Belgesel","Kapaklar veya genel ses ayarı birbirine karıştı");

            Check(before!=Reliability.MediaPlanHash(e) && PackageArchive.Group(e)=="","Eski çıktı güncel sayıldı");

        });

        Test("NATURAL-CHIRP-AUDIO", "Chirp anlatımı bir kez doğal üretilir; altyazı gerçek sese eşlenir ve hız değişmez", () => {

            var st = New("natural-chirp"); st.Settings.VoiceProvider = "Google Cloud TTS"; st.Settings.GoogleTtsEnabled = true; st.Settings.CloudUsageRequired = false; st.Settings.GoogleTtsVoice = "tr-TR-Chirp3-HD-Algieba"; st.Settings.VoiceSpeed = 1.4; st.Settings.FreeOnly = false; st.Vault.Set("GOOGLE_TTS_API_KEY", "fixture-only"); st.Vault.Set("GEMINI_API_KEY", "fixture-only");

            string raw = Path.Combine(st.Root,"original.wav"); Media.Exec(ffmpeg,["-y","-f","lavfi","-i","sine=frequency=440:sample_rate=44100","-t","5","-c:a","pcm_s16le",raw],st.Root,CancellationToken.None).GetAwaiter().GetResult();

            const string narration = "Hz.Musa kıssasının başında ilk cümle anlatılıyor. Sonrasında ikinci cümle doğal ahenk ile devam ediyor.";

            var script = new VideoScript { Scenes = [new() { Narration = narration }] }; int tts = 0, alignment = 0;

            using var http = new HttpClient(new Handler(async req => {

                using var body = JsonDocument.Parse(await req.Content!.ReadAsStringAsync());

                if (req.RequestUri!.Host == "texttospeech.googleapis.com") { tts++; Check(body.RootElement.GetProperty("input").GetProperty("text").GetString() == NarrationFlow.SpokenText(narration), "Ses altyazı satırlarına bölündü"); return Ok(JsonSerializer.Serialize(new { audioContent = Convert.ToBase64String(File.ReadAllBytes(raw)) })); }

                alignment++; var content = body.RootElement.GetProperty("messages")[1].GetProperty("content"); Check(content[1].GetProperty("type").GetString() == "input_audio", "Gerçek ses incelemeye gönderilmedi");

                return Ok(JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = "{\"Captions\":[{\"Id\":0,\"Start\":0.4,\"End\":2.2},{\"Id\":1,\"Start\":2.6,\"End\":4.8},{\"Id\":2,\"Start\":4.8,\"End\":5}]}" } } } }));

            }));

            // SplitText decides display lines; the mock supplies matching measured intervals.

            Check(Engine.SplitText(narration,48).Count == 3, "Test altyazı örneği değişti");

            var services = new Services(st,_=>{},http); NarrationFlow.Prepare(st,new Episode(),script,st.Settings,st.Root,services,CancellationToken.None).GetAwaiter().GetResult();

            Check(tts == 1 && alignment == 1 && Math.Abs(script.Scenes[0].Duration-5)<.01 && script.Scenes[0].SpeechCaptions[0].Start == .4 && script.Scenes[0].SpeechCaptions[1].Start == 2.6, "Doğal ses değişti veya altyazı oranla tahmin edildi");

            string pcm = Path.Combine(st.Root,"original.pcm"), result = Path.Combine(st.Root,"result.pcm");

            Media.Exec(ffmpeg,["-y","-i",raw,"-f","s16le",pcm],st.Root,CancellationToken.None).GetAwaiter().GetResult(); Media.Exec(ffmpeg,["-y","-i",Path.Combine(st.Root,"narration-full.wav"),"-f","s16le",result],st.Root,CancellationToken.None).GetAwaiter().GetResult();

            Check(Reliability.FileHash(pcm)==Reliability.FileHash(result),"Özgün ses örnekleri değiştirildi");

            NarrationFlow.Prepare(st,new Episode(),script,st.Settings,st.Root,services,CancellationToken.None).GetAwaiter().GetResult(); Check(tts==1 && alignment==1,"Sağlam ses yeniden üretildi");

            Reject(()=>AudioSubtitleAlignment.Parse("{\"Captions\":[{\"Id\":0,\"Start\":0,\"End\":6}]}",["metin"],5));

        });

        Test("NATURAL-CHIRP-REPAIR", "Chirp anlatımı bir kez doğal üretilir; altyazı gerçek sese eşlenir ve hız değişmez", () => {

            var st = New("natural-chirp-repair"); st.Settings.VoiceProvider = "Google Cloud TTS"; st.Settings.GoogleTtsEnabled = true; st.Settings.CloudUsageRequired = false; st.Settings.GoogleTtsVoice = "tr-TR-Chirp3-HD-Algieba"; st.Settings.VoiceSpeed = 1.4; st.Settings.FreeOnly = false; st.Vault.Set("GOOGLE_TTS_API_KEY", "fixture-only"); st.Vault.Set("GEMINI_API_KEY", "fixture-only");

            string raw = Path.Combine(st.Root,"original.wav"); Media.Exec(ffmpeg,["-y","-f","lavfi","-i","sine=frequency=440:sample_rate=44100","-t","5","-c:a","pcm_s16le",raw],st.Root,CancellationToken.None).GetAwaiter().GetResult();

            const string narration = "Hz.Musa kıssasının başında ilk cümle anlatılıyor. Sonrasında ikinci cümle doğal ahenk ile devam ediyor.";

            string render = Path.Combine(st.Root,"render"); var script = new VideoScript { Scenes = [new() { Narration = narration }] }; int tts = 0, alignment = 0;

            using var http = new HttpClient(new Handler(async req => {

                using var body = JsonDocument.Parse(await req.Content!.ReadAsStringAsync());

                if (req.RequestUri!.Host == "texttospeech.googleapis.com") { tts++; Check(body.RootElement.GetProperty("input").GetProperty("text").GetString() == NarrationFlow.SpokenText(narration), "Ses altyazı satırlarına bölündü"); return Ok(JsonSerializer.Serialize(new { audioContent = Convert.ToBase64String(File.ReadAllBytes(raw)) })); }

                alignment++; if (alignment==1) return Ok(JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = "{\"Captions\":[{\"Id\":0,\"Start\":0.4,\"End\":2.2}]}" } } } })); var content = body.RootElement.GetProperty("messages")[1].GetProperty("content"); Check(content[1].GetProperty("type").GetString() == "input_audio", "Gerçek ses incelemeye gönderilmedi");

                return Ok(JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = "{\"Captions\":[{\"Id\":0,\"Start\":0.4,\"End\":2.2},{\"Id\":1,\"Start\":2.6,\"End\":4.8},{\"Id\":2,\"Start\":4.8,\"End\":5}]}" } } } }));

            }));

            // SplitText decides display lines; the mock supplies matching measured intervals.

            Check(Engine.SplitText(narration,48).Count == 3, "Test altyazı örneği değişti");

            var services = new Services(st,_=>{},http); NarrationFlow.Prepare(st,new Episode(),script,st.Settings,render,services,CancellationToken.None).GetAwaiter().GetResult();

            Check(tts == 1 && alignment == 2 && Math.Abs(script.Scenes[0].Duration-5)<.01 && script.Scenes[0].SpeechCaptions[0].Start == .4 && script.Scenes[0].SpeechCaptions[1].Start == 2.6, "Doğal ses değişti veya altyazı oranla tahmin edildi");

            string pcm = Path.Combine(st.Root,"original.pcm"), result = Path.Combine(st.Root,"result.pcm");

            Media.Exec(ffmpeg,["-y","-i",raw,"-f","s16le",pcm],st.Root,CancellationToken.None).GetAwaiter().GetResult(); Media.Exec(ffmpeg,["-y","-i",Path.Combine(render,"narration-full.wav"),"-f","s16le",result],st.Root,CancellationToken.None).GetAwaiter().GetResult();

            Check(Reliability.FileHash(pcm)==Reliability.FileHash(result),"Özgün ses örnekleri değiştirildi");

            NarrationFlow.Prepare(st,new Episode(),script,st.Settings,render,services,CancellationToken.None).GetAwaiter().GetResult(); Check(tts==1 && alignment==2,"Sağlam ses yeniden üretildi");

            Reject(()=>AudioSubtitleAlignment.Parse("{\"Captions\":[{\"Id\":0,\"Start\":0,\"End\":6}]}",["metin"],5));

        });

        Test("MEASURED-NARRATION-SYNC", "Aynı uzunlukta farklı hızdaki konuşmalar gerçek ses süresiyle sahnelere ayrılır", () => {

            var st = New("measured-narration-sync"); st.Settings.VoiceProvider = "Piper / ücretsiz";

            var script = new VideoScript { Scenes = [new() { Narration = "Birinci cümle ağır okunuyor." }, new() { Narration = "İkinci cümle hızlı okunuyor." }] }; int calls = 0;

            Task Speak(string text, string path, Settings settings, CancellationToken ct) { calls++; return Media.Exec(ffmpeg, ["-y", "-f", "lavfi", "-i", "sine=frequency=440", "-t", text.StartsWith("Birinci") ? "4" : "1", path], st.Root, ct); }

            var services = new Services(st, _ => {}); NarrationFlow.Prepare(st, new Episode(), script, st.Settings, st.Root, services, CancellationToken.None, Speak).GetAwaiter().GetResult();

            Check(Math.Abs(script.Scenes[0].Duration - 4) < .1 && Math.Abs(script.Scenes[1].Duration - 1) < .1 && Math.Abs(script.Scenes[0].SpeechCaptions[0].End - 4) < .1, "Sahne süreleri karakter oranıyla tahmin edildi");

            NarrationFlow.Prepare(st, new Episode(), script, st.Settings, st.Root, services, CancellationToken.None, Speak).GetAwaiter().GetResult(); Check(calls == 2, "Ölçülen ses önbelleği yeniden üretildi");

        });

        Test("GOOGLE-MARKED-NARRATION", "Google zaman işaretleri özgün hız korunarak altyazı ve sahne kesimini belirler", () => {

            var st = New("google-marked-narration"); st.Settings.VoiceProvider = "Google Cloud TTS"; st.Settings.GoogleTtsEnabled = true; st.Settings.CloudUsageRequired = false; st.Settings.GoogleTtsVoice = "tr-TR-Wavenet-A"; st.Settings.VoiceSpeed = 1.25; st.Vault.Set("GOOGLE_TTS_API_KEY", "fixture-only");

            string raw = Path.Combine(st.Root, "fixture.wav"); Media.Exec(ffmpeg, ["-y", "-f", "lavfi", "-i", "sine=frequency=440", "-t", "5", raw], st.Root, CancellationToken.None).GetAwaiter().GetResult(); int calls = 0;

            using var http = new HttpClient(new Handler(async req => { calls++; using var doc = JsonDocument.Parse(await req.Content!.ReadAsStringAsync()); var ssml = doc.RootElement.GetProperty("input").GetProperty("ssml").GetString()!; var xml = System.Xml.Linq.XDocument.Parse(ssml); var marks = xml.Descendants("mark").Select(x => x.Attribute("name")!.Value).ToArray(); Check(req.RequestUri!.AbsolutePath.Contains("v1beta1") && doc.RootElement.GetProperty("enableTimePointing")[0].GetString() == "SSML_MARK", "Zaman işaretleri istenmedi"); return Ok(JsonSerializer.Serialize(new { audioContent = Convert.ToBase64String(File.ReadAllBytes(raw)), timepoints = marks.Select(m => new { markName = m, timeSeconds = m == "end" ? 4.5 : m == "c0" ? .6 : 3.2 }) })); }));

            var script = new VideoScript { Scenes = [new() { Narration = "Birinci altyazı cümlesi burada oldukça uzun anlatılır. İkinci kısa cümle." }, new() { Narration = "Hz.Musa ikinci sahnede." }] };

            NarrationFlow.Prepare(st, new Episode(), script, st.Settings, st.Root, new Services(st, _ => {}, http), CancellationToken.None).GetAwaiter().GetResult();

            Check(script.Scenes[0].SpeechCaptions.Count == 2 && Math.Abs(script.Scenes[0].SpeechCaptions[0].Start - .6) < .02 && Math.Abs(script.Scenes[0].SpeechCaptions[1].Start - 3.2) < .02 && script.Scenes[1].SpeechCaptions[0].Text == "Hz.Musa ikinci sahnede." && Math.Abs(script.Scenes[0].Duration - 5) < .15, "Konuşma işaretleri veya hız ayarı altyazıya uygulanmadı");

            NarrationFlow.Prepare(st, new Episode(), script, st.Settings, st.Root, new Services(st, _ => {}, http), CancellationToken.None).GetAwaiter().GetResult(); Check(calls == 2, "İşaretli anlatım yeniden istendi");

            Reject(() => TimedNarration.MarkedCaptions(["A", "B"], new Dictionary<string,double> { ["c0"] = 1, ["c1"] = .5 }, 2, 1));

            var altered = TimedNarration.Redisplay(script.Scenes[0].SpeechCaptions, "Değiştirilen gösterim metni özgün zamanları korur"); Check(altered[0].Start == .6 && altered[1].Start == 3.2, "Altyazı düzenlemesi zamanı yeniden tahmin etti");

        });

        Test("TITLE-FIT-REAL-RENDER", "Sol başlık gerçek çıktı karesinde ekran dışına taşmaz", () => {

            var st = New("title-fit-render"); var scene = new Scene { OnScreen = "Çıkış Yok Sanırken Açılan Yeni Yol", Narration = "Altyazı kapalı", StockFile = video };

            var layout = new TextLayout { TitleFont = "Georgia", TitleSize = 72, TitleAlignment = 1, TitleX = 5, TitleY = 5, SubtitleEnabled = false, TitleBackground = false, Outline = 0, Shadow = 0 };

            TextDesign.Preview(st, scene, layout, false, 0, st.Root, CancellationToken.None).GetAwaiter().GetResult();

            using var bitmap = new System.Drawing.Bitmap(Path.Combine(st.Root, "preview.png")); int min = bitmap.Width, max = 0, count = 0;

            for (int y = 0; y < bitmap.Height; y += 2) for (int x = 0; x < bitmap.Width; x += 2) { var c = bitmap.GetPixel(x,y); if (c.R > 220 && c.G > 220 && c.B > 220) { min = Math.Min(min,x); max = Math.Max(max,x); count++; } }

            Check(count > 100 && min > 70 && max < bitmap.Width - 70, "Yazı gerçek karede kırpılıyor veya çizilmedi");

        });

        Test("TITLE-SAFE-POSITIONS", "Sol orta sağ başlıklar font ve stil değişmeden güvenli sınırda kalır", () => {

            var layout = new TextLayout { Font = "Georgia", TitleFont = "Arial", TitleSize = 72, TitleX = 5, TitleY = 5, ScaleX = 125, Spacing = 3, Rotation = 20, TitleAnimation = "Vurgu / pop", AutoSceneTitles = true };

            string before = JsonSerializer.Serialize(layout); foreach (var (w,h) in new[] { (1920,1080), (1080,1920) }) foreach (var pos in TitlePlacement.Choices) {

                var fit = TitlePlacement.Fit("Çıkış Yok Sanırken Açılan Yeni Yol", layout, w, h, pos); double margin = Math.Max(24, w * .055);

                Check(fit.X + fit.MinX >= margin - .01 && fit.X + fit.MaxX <= w - margin + .01 && fit.Y + fit.MinY >= margin - .01 && fit.Y + fit.MaxY <= h - margin + .01, "Başlık ekran dışına taşıyor");

                var script = new VideoScript { TextLayout = layout, Scenes = [new() { Duration = 5, OnScreen = fit.Text, TitlePlacement = pos }] }; var ass = TextDesign.ForVideo([], script, w, h); Check(ass.Contains("Arial,72,") && ass.Contains("\\an" + fit.Alignment), "Yazı tipi veya konum değişti");

            }

            Check(JsonSerializer.Serialize(layout) == before, "Otomatik sığdırma seçili stili değiştirdi");

            Reject(() => SceneHeadings.Parse("{\"Titles\":[{\"Scene\":1,\"Title\":\"Başlık\",\"Position\":\"Dışarı\"}]}", [1]));

        });

        Test("SCENE-VIDEO-SOURCES", "Havuz düğmesi havuz klasöründe açılır, dış video seçimi ayrı ve onaysızdır", () => {

            var st = New("scene-video-sources"); using var poolDialog = SceneVideoChoice.Dialog(st, true); using var outsideDialog = SceneVideoChoice.Dialog(st, false);

            Check(poolDialog.InitialDirectory == new MediaPool(st).Folder && poolDialog.FileName.StartsWith(poolDialog.InitialDirectory) && outsideDialog.Title == "Farklı kaynaktan video seç", "Video havuzu doğrudan açılmıyor");

            var scene = new Scene(); SceneVideoChoice.Apply(scene, video, new PoolItem { RightsConfirmed = true, Source = "Havuz kaynağı", Creator = "Sanatçı", License = "CC0", Tags = "sea" }); Check(scene.UserLocal && scene.RightsConfirmed && scene.LicenseUrl == "CC0", "Havuz lisansı taşınmadı"); SceneVideoChoice.Apply(scene, video, null); Check(!scene.RightsConfirmed && scene.UserLocal, "Dış video otomatik onaylandı");

        });

        Test("DESIGN-OPTIONS-UI", "Kanal seçenekleri iki kapak ekranında, istemiyorum kutuları proje kartındadır", () => {
            var st = New("design-options-ui"); var e = Episode(st); e.Plan = Engine.DemoPlan(e);
            IEnumerable<Control> Children(Control c) => c.Controls.Cast<Control>().SelectMany(x => new[] { x }.Concat(Children(x)));
            using var form = new StudioForm(st);
            typeof(StudioForm).GetMethod("Navigate", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(form, ["Kapak tasarımları"]);
            Check(Children(form).OfType<CheckBox>().Any(c => c.Text == "Kapakta kanal adını göster") && Children(form).OfType<TextBox>().Any(), "Genel kapak ekranında seçenekler yok");
            using var editor = new PackageCoverEditor(st, e);
            Check(Children(editor).OfType<CheckBox>().Any(c => c.Text == "Kapakta kanal adını göster") && Children(editor).OfType<Button>().Any(c => c.Text == "Kanal İsmini Manuel Gir"), "Proje kapak ekranında seçenekler yok");
            typeof(StudioForm).GetMethod("Navigate", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(form, ["Video hazırla"]);
            Check(Children(form).OfType<Button>().Count(b=>b.AccessibleName?.StartsWith("Üretim adımı:")==true)==5,"Proje beş adımlı üretime ulaşmıyor.");
        });

        Test("COVER-CHANNEL-OPTION", "Kapaktaki kanal yazısı gizlenir ve video ayarına taşınır", () => {
            var settings = new Settings { CoverShowChannelName = false, CoverChannelName = "NUR KAPISI", ChannelName = "NUR KAPISI", CoverAutoStyle = false, CoverStyle = "Minimal" };
            var e = new Episode { ArtworkSettings = PackageArtwork.Snapshot(settings) };
            var effective = VideoSettings.For(new Settings(), e);
            Check(!effective.CoverShowChannelName && effective.ChannelName == "NUR KAPISI", "Kanal yazısı tercihi kayboldu");
            using var hidden = CoverDesigner.Draw(effective, "Müzik klibi");
            effective.ChannelName = ""; using var blank = CoverDesigner.Draw(effective, "Müzik klibi");
            for (int y = 60; y < 125; y++) for (int x = 60; x < 500; x++) Check(hidden.GetPixel(x,y) == blank.GetPixel(x,y), "Gizli kanal adı kapağa çizildi");
        });

        Test("CONTENT-STAGE-PENDING", "Taslak oluşsa da araştırma ve yazım denetimi bitmeden içerik tamamlanmaz", () => {
            var e = new Episode { Plan = new ContentPlan(), Status = "Araştırılıyor", ResumeStage = "Proofread" };
            Check(!ProjectState.CompletedStages(e, true).Any(x => x), "Denetim sürerken tamamlandı sayıldı");
            e.Status = "Kontrol gerekli";
            Check(!ProjectState.CompletedStages(e, true)[0], "Kesilen denetim tamamlandı sayıldı");
            e.Status = "Senaryo hazır"; e.ResumeStage = "";
            Check(ProjectState.CompletedStages(e, false)[0], "Bitmiş içerik tamamlanmadı");
        });
        Test("DESIGN-STAGE-OPTIONAL", "İsteğe bağlı editörler geçildiğinde adım tamamlanır; tercih proje kaydında korunur", () => {
            var st = New("optional-design"); var e = new Episode { Plan = new ContentPlan(), SkipArtworkEditing = true, SkipTextDesignEditing = true };
            st.Episodes.Add(e); st.Save();
            var saved = JsonSerializer.Deserialize<Episode>(JsonSerializer.Serialize(e, Json.Options), Json.Options)!;
            var stages = ProjectState.CompletedStages(saved, false);
            Check(stages[2] && stages[3], "İstemiyorum tercihleri tamamlanmadı");
            saved.SkipArtworkEditing = saved.SkipTextDesignEditing = false;
            stages = ProjectState.CompletedStages(saved, false);
            Check(!stages[2] && !stages[3], "Kaldırılan tercihler tamamlandı sayıldı");
        });

        Test("DESIGN-STAGE-FROZEN", "Üretim için sabitlenen tasarım, kapak editörüne ayrıca girilmeden tamamlandı görünür", () => {
            var e = new Episode { Plan = new ContentPlan(), ProductionSettings = new Settings(), Status = "Üretiliyor" };
            Check(ProjectState.CompletedStages(e, false)[2], "Üretim tasarımı eksik göründü");
            e.ProductionSettings = null;
            Check(!ProjectState.CompletedStages(e, false)[2], "Kaydedilmemiş tasarım tamamlandı sayıldı");
        });

        Test("STOCK-RELEVANCE-FILTER", "Silah ve konu dışı adaylar elenir, deniz sorgusu uzay fonuna geçmez", () => {

            Check(!StockSearch.Appropriate("sea", "sea gun rifle shooting") && !StockSearch.Appropriate("sea", "pizza restaurant city") && StockSearch.Appropriate("sea", "ocean waves beach") && !StockSearch.GeneralQueries("sea").Contains("space"), "Konu dışı stok kabul edildi");

            Check(StockSearch.PossibleCandidate("sea", "miscellaneous collection", true) && !StockSearch.PossibleCandidate("sea", "miscellaneous collection", false), "Görsel denetim olmadan başlık filtresi gevşetildi");
            Check(!StockSearch.PossibleCandidate("sea", "gun shooting", true), "Önizleme yolu silah filtresini aştı");
            Check(StockSearch.GeneralQueries("city street").Contains("urban"), "Şehir için ilişkili alternatif aranmadı");
            var st = New("pool-weapons"); var pool = new MediaPool(st); var bad = pool.Add(video, "sea gun rifle", "Doğa", "Kullanıcı arşivi", "Kullanıcı", "CC0"); var items = pool.List(); items[0].GeneralBackground = true; pool.Save(items); Check(!pool.CanUse("sea", [], true, true), "Genel fon etiketi silah filtresini aştı");

            Check(StockSearch.Normalize("deniz; kıyı; gün batımı").Contains("sea") && StockSearch.Normalize("deniz; kıyı; gün batımı").Contains("coast"), "Türkçe tema çevrilmedi");

        });

        Test("STOCK-NVIDIA-ORDER", "Tüm NVIDIA modelleri denenir, çalışan hatırlanır; sonra Gemini ve yerel görsel Ollama kullanılır", () => {
            foreach (var mode in new[] { "nvidia-success", "nvidia-next", "gemini-retry", "ollama-success", "all-down" }) {
                var st = New("stock-order-" + mode); st.Settings.FreeOnly = false;
                st.Settings.EnabledCloudProviders = st.Settings.ProviderOrder = "Gemini,NVIDIA";
                st.Settings.NvidiaModel = "fixture-first";
                st.Vault.Set("NVIDIA_API_KEY", "fixture-nvidia-vision"); st.Vault.Set("GEMINI_API_KEY", "fixture-gemini-vision");
                var called = new List<string>(); int geminiCalls = 0;
                using var http = new HttpClient(new Handler(async req => {
                    if (req.Method == HttpMethod.Get && req.RequestUri!.AbsolutePath == "/v1/models")
                        return Ok("{\"data\":[{\"id\":\"fixture-first\"},{\"id\":\"fixture-second\"},{\"id\":\"fixture-third\"}]}");
                    if (req.RequestUri!.AbsolutePath == "/api/tags") return Ok("{\"models\":[{\"name\":\"text-only\"},{\"name\":\"vision-local\"}]}");
                    using var body = JsonDocument.Parse(await req.Content!.ReadAsStringAsync());
                    string model = body.RootElement.GetProperty("model").GetString()!;
                    if (req.RequestUri.AbsolutePath == "/api/show") return Ok(model == "text-only" ? "{\"capabilities\":[\"completion\"]}" : "{\"capabilities\":[\"completion\",\"vision\"]}");
                    bool ollama = req.RequestUri.AbsolutePath == "/api/chat";
                    if (ollama) Check(body.RootElement.GetProperty("messages")[1].GetProperty("images")[0].GetString()!.Length > 100, "Ollama görseli eksik");
                    else Check(body.RootElement.GetProperty("messages")[1].GetProperty("content")[1].GetProperty("image_url").GetProperty("url").GetString()!.StartsWith("data:image/jpeg;base64,"), "Denetim görseli eksik");
                    if (req.RequestUri.Host == "integrate.api.nvidia.com") {
                        called.Add(model);
                        if (!(mode == "nvidia-success" || mode == "nvidia-next" && model == "fixture-second")) return new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = new StringContent("{}") };
                    } else if (ollama) {
                        called.Add("Ollama");
                        if (mode == "all-down") return new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("{}") };
                    } else {
                        called.Add("Gemini"); geminiCalls++;
                        if (mode != "gemini-retry" || geminiCalls == 1) return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("{}") };
                    }
                    string result = "{\"Accept\":true,\"Relevant\":true,\"HasWeapons\":false,\"Reason\":\"Uyumlu\"}";
                    return Ok(ollama ? JsonSerializer.Serialize(new { message = new { content = result }, done = true }) : JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = result } } } }));
                }));
                bool Run(string query) => StockVisualReview.Accept(st, query, video, http, _ => {}, CancellationToken.None, (_, _) => Task.CompletedTask).GetAwaiter().GetResult();
                bool failed = false; try { Check(Run("sea " + mode), "Uygun kare reddedildi"); } catch (StockVisualReview.UnavailableException) { failed = true; }
                Check(failed == (mode == "all-down"), "Denetlenmeyen kare kabul edildi");
                string[] expected = mode switch {
                    "nvidia-success" => ["fixture-first"], "nvidia-next" => ["fixture-first", "fixture-second"],
                    "gemini-retry" => ["fixture-first", "fixture-second", "fixture-third", "Gemini", "Gemini"],
                    _ => ["fixture-first", "fixture-second", "fixture-third", "Gemini", "Gemini", "Gemini", "Ollama"] };
                Check(called.SequenceEqual(expected), "Model sırası yanlış: " + string.Join(",", called));
                if (mode == "nvidia-next") { called.Clear(); Check(Run("sea remembered"), "Hatırlanan model başarısız"); Check(called.SequenceEqual(new[] { "fixture-second" }), "Çalışan görsel model öncelikli değil"); }
            }
            var uiStore = New("stock-error-layout");
            var episode = new Episode { WizardStep=2, Topic = "Hata testi", ShortCount = 0, Plan = Plan(), ResumeStage = "Produce", Error = string.Join("\n", Enumerable.Repeat("Uzun Türkçe uyarı; denetim tamamlanamadı.", 20)) }; uiStore.Episodes.Add(episode);
            using var form = new StudioForm(uiStore); form.Show(); typeof(StudioForm).GetMethod("Navigate", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(form, ["Video hazırla"]); Application.DoEvents();
            IEnumerable<Control> Children(Control c) => c.Controls.Cast<Control>().SelectMany(x => new[] { x }.Concat(Children(x)));
            var warning = Children(form).OfType<TextBox>().Single(c => c.AccessibleName == "Üretim uyarısı");
            var bar = warning.Parent!.Controls.OfType<TurquoisePackageBar>().Single();
            Check(warning.Visible && warning.Multiline && warning.WordWrap && warning.ScrollBars == ScrollBars.Vertical && warning.Bottom <= bar.Top, "Uzun uyarı çubuğun üzerine taşıyor");
            using var bitmap = new Bitmap(warning.Parent.Width, warning.Parent.Height); warning.Parent.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(root, "warning-layout.png"));
        });

        Test("STOCK-HISTORY-SPEED", "Tarihî sahnede sınırlı adaydan sonra denetlenmiş temsili aramaya geçilir", () => {
            var st = New("stock-history-speed"); st.Settings.FreeOnly = false; st.Settings.EnabledCloudProviders = "NVIDIA"; st.Settings.StockOrder = "Pexels";
            st.Settings.PoolEnabled = st.Settings.PoolFallback = st.Settings.PoolAutoSave = false; st.Settings.StockGeneralFallback = true;
            st.Vault.Set("PEXELS_API_KEY", "fixture-stock-speed"); st.Vault.Set("NVIDIA_API_KEY", "fixture-history-speed");
            int searches = 0, reviewed = 0, downloads = 0;
            using var http = new HttpClient(new Handler(async req => {
                if (req.RequestUri!.Host == "fixture.example") { downloads++; return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(File.ReadAllBytes(video)) }; }
                if (req.RequestUri.AbsolutePath == "/v1/models") return Ok("{\"data\":[]}");
                if (req.RequestUri.Host == "api.pexels.com") {
                    searches++;
                    return Ok(JsonSerializer.Serialize(new { videos = Enumerable.Range(1,12).Select(id => new { id, url = "https://www.pexels.com/video/" + id, user = new { name = "Fixture" }, video_files = new[] { new { file_type = "video/mp4", width = 160, link = "https://fixture.example/" + id + ".mp4" } } }).ToArray() }));
                }
                reviewed++; using var body = JsonDocument.Parse(await req.Content!.ReadAsStringAsync());
                bool accept = body.RootElement.GetProperty("messages")[1].GetProperty("content")[0].GetProperty("text").GetString()!.Contains("reading book");
                return Ok(JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = JsonSerializer.Serialize(new { Accept = accept, Relevant = accept, HasWeapons = false, Reason = "Tema denetlendi" }) } } } }));
            }));
            var scene = new Scene { VisualQuery = "Ottoman scholar teaching" };
            var service = new Services(st, _ => {}, http); service.Stock(scene, false, st.Root, [], CancellationToken.None).GetAwaiter().GetResult();
            Check(searches == 2 && downloads == 3 && reviewed == 2 && scene.StockReviewQuery == "reading book" && scene.VisualQuery == "Ottoman scholar teaching", "Arama sınırlanmadı veya özgün tema değişti: " + searches + "/" + downloads + "/" + reviewed);
            Check(service.ReviewStock(scene, CancellationToken.None).GetAwaiter().GetResult() && reviewed == 2, "Temsili sahne yeniden yanlış temayla denetlendi");
        });

        Test("STOCK-USED-CANDIDATES", "Kullanılmış ilk üç stok adayı yeni adayların aranmasını engellemez", () => {
            var st = New("stock-used-candidates"); st.Settings.FreeOnly = false; st.Settings.EnabledCloudProviders = "NVIDIA"; st.Settings.NvidiaModel = "fixture-vision";
            st.Settings.PoolEnabled = st.Settings.PoolFallback = st.Settings.PoolAutoSave = st.Settings.StockGeneralFallback = false;
            st.Vault.Set("PEXELS_API_KEY", "fixture-stock"); st.Vault.Set("NVIDIA_API_KEY", "fixture-vision"); long downloaded = 0;
            using var http = new HttpClient(new Handler(req => {
                if (req.RequestUri!.Host == "fixture.example") { downloaded = long.Parse(Path.GetFileNameWithoutExtension(req.RequestUri.AbsolutePath)); return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(File.ReadAllBytes(video)) }); }
                if (req.RequestUri.Host == "api.pexels.com") {
                    Check(req.RequestUri.Query.Contains("per_page=40"), "Arama kapsamı genişletilmedi");
                    return Task.FromResult(Ok(JsonSerializer.Serialize(new { videos = Enumerable.Range(1,12).Select(id => new { id, title = "reading book library", url = "https://www.pexels.com/video/" + id, user = new { name = "Fixture" }, video_files = new[] { new { file_type = "video/mp4", width = 160, link = "https://fixture.example/" + id + ".mp4" } } }).ToArray() })));
                }
                if (req.Method == HttpMethod.Get) return Task.FromResult(Ok("{\"data\":[]}"));
                return Task.FromResult(Ok(JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = "{\"Accept\":true,\"Relevant\":true,\"HasWeapons\":false,\"Reason\":\"Kitap\"}" } } } })));
            }));
            var scene = new Scene { VisualQuery = "reading book" }; new Services(st, _ => {}, http).Stock(scene, false, st.Root, new HashSet<long> { 1, 2, 3 }, CancellationToken.None).GetAwaiter().GetResult();
            Check(downloaded == 4 && scene.RightsConfirmed, "İlk üç kullanılmış aday aramayı durdurdu");
        });

        Test("STOCK-GENERAL-FALLBACK", "Açık genel fon izni havuzda uygulanır ve fon kendi temasıyla görsel denetlenir", () => {
            var st = New("stock-general-fallback"); st.Settings.FreeOnly = false; st.Settings.EnabledCloudProviders = "NVIDIA"; st.Settings.NvidiaModel = "fixture-vision";
            st.Settings.PoolFallback = st.Settings.PoolEnabled = st.Settings.StockGeneralFallback = true; st.Vault.Set("NVIDIA_API_KEY", "fixture-general");
            new MediaPool(st).Add(video, "forest trees", "Doğa", "Fixture archive", "Fixture", "Fixture license"); bool checkedBackground = false;
            using var http = new HttpClient(new Handler(async req => {
                if (req.Method == HttpMethod.Get) return Ok("{\"data\":[]}");
                using var body = JsonDocument.Parse(await req.Content!.ReadAsStringAsync());
                checkedBackground = body.RootElement.GetProperty("messages")[1].GetProperty("content")[0].GetProperty("text").GetString()!.Contains("Neutral background");
                return Ok(JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = JsonSerializer.Serialize(new { Accept = checkedBackground, Relevant = checkedBackground, HasWeapons = false, Reason = "Fon denetlendi" }) } } } }));
            }));
            var scene = new Scene { VisualQuery = "historical documents; sultan and advisor; old painting" };
            new Services(st, _ => {}, http).Stock(scene, false, st.Root, [], CancellationToken.None).GetAwaiter().GetResult();
            Check(checkedBackground && scene.StockReviewQuery == StockSearch.NeutralReviewQuery && scene.RightsConfirmed, "Genel fon izni yok sayıldı veya görsel denetimi atlandı");
            Check(StockSearch.GeneralQueries(scene.VisualQuery).First() == "old book", "Belge sahnesi için ilgili alternatif yok");
        });

        Test("STOCK-SPEED-CACHE", "Başarısız NVIDIA isteği ve model listesi her karede tekrarlanmaz", () => {
            var st = New("stock-speed-cache"); st.Settings.FreeOnly = false; st.Settings.EnabledCloudProviders = "NVIDIA,Gemini"; st.Settings.NvidiaModel = "fixture-unavailable";
            st.Vault.Set("NVIDIA_API_KEY", "fixture-speed"); st.Vault.Set("GEMINI_API_KEY", "fixture-speed-gemini"); int lists = 0, nvidia = 0, gemini = 0;
            using var http = new HttpClient(new Handler(req => {
                if (req.Method == HttpMethod.Get) { lists++; return Task.FromResult(Ok("{\"data\":[{\"id\":\"fixture-unavailable\"}]}")); }
                if (req.RequestUri!.Host == "integrate.api.nvidia.com") { nvidia++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("{}") }); }
                gemini++; return Task.FromResult(Ok(JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = "{\"Accept\":true,\"Relevant\":true,\"HasWeapons\":false,\"Reason\":\"Denetlendi\"}" } } } })));
            }));
            for (int i = 0; i < 2; i++) Check(StockVisualReview.Accept(st, "sea " + i, video, http, _ => {}, CancellationToken.None).GetAwaiter().GetResult(), "Yedek denetim başarısız");
            Check(lists == 1 && nvidia == 1 && gemini == 2, "Başarısız istek veya liste tekrarlandı");
            var scene = new Scene { VisualQuery = "Ottoman scholar teaching", StockReviewTheme = "Ottoman scholar teaching", StockReviewQuery = "reading book" };
            Check(StockSearch.GeneralQueries(scene.VisualQuery).First() == "reading book" && StockSearch.ReviewQuery(scene) == "reading book", "İlgili temsili tema korunmadı");
            scene.VisualQuery = "sea"; Check(StockSearch.ReviewQuery(scene) == "sea", "Değişen tema eski denetimi kullanıyor");
        });

        Test("STOCK-VISUAL-REVIEW", "Etiketi deniz olan silah görüntüsü kare denetiminde reddedilir; belirsiz sonuç kabul edilmez", () => {

            var st = New("stock-visual-review"); st.Settings.FreeOnly = false; st.Settings.GeminiModel = "fixture-vision"; st.Vault.Set("GEMINI_API_KEY", "fixture-only"); int calls = 0;

            using var http = new HttpClient(new Handler(async req => { calls++; using var doc = JsonDocument.Parse(await req.Content!.ReadAsStringAsync()); Check(doc.RootElement.GetProperty("messages")[1].GetProperty("content")[1].GetProperty("image_url").GetProperty("url").GetString()!.StartsWith("data:image/jpeg;base64,"), "Gerçek denetim karesi gönderilmedi"); return Ok("{\"choices\":[{\"message\":{\"content\":\"{\\\"Accept\\\":true,\\\"Relevant\\\":true,\\\"HasWeapons\\\":true,\\\"Reason\\\":\\\"Silah var\\\"}\"}}]}"); }));

            Check(!StockVisualReview.Accept(st, "sea", video, http, _ => {}, CancellationToken.None).GetAwaiter().GetResult(), "Silah içeren kare kabul edildi"); Check(!StockVisualReview.Accept(st, "sea", video, http, _ => {}, CancellationToken.None).GetAwaiter().GetResult() && calls == 1, "Reddedilen video yeniden API isteği gönderdi");

            Reject(() => StockVisualReview.Parse("{\"Accept\":\"true\"}"));

        });

        Test("CONNECTED-CHANNEL-HEADING", "Başlık elle yazılan addan değil bağlı YouTube kanalından alınır ve çevrimdışı saklanır", () => {

            var st = New("connected-channel-heading"); st.Settings.ChannelName = "Elle yazılan farklı ad"; st.Settings.GoogleClientId = "fixture-client"; st.Vault.Set("GOOGLE_CLIENT_SECRET", "fixture-secret"); st.Vault.Set("YOUTUBE_REFRESH_TOKEN", "fixture-refresh");

            using var http = new HttpClient(new Handler(req => Task.FromResult(Ok(req.RequestUri!.Host == "oauth2.googleapis.com" ? "{\"access_token\":\"fixture-token\"}" : "{\"items\":[{\"id\":\"fixture-channel\",\"snippet\":{\"title\":\"Gerçek Kanal Adı\"}}]}"))));

            new YouTube(st, http).Channel(CancellationToken.None).GetAwaiter().GetResult();

            Check(StudioForm.ChannelHeading(st.Settings) == "Gerçek Kanal Adı" && st.Settings.ChannelName == "Elle yazılan farklı ad", "Başlık gerçek kanal adı değil");

            var reopened = new Store(st.Root); Check(StudioForm.ChannelHeading(reopened.Settings) == "Gerçek Kanal Adı", "Çevrimdışı kanal adı kayboldu");

            Check(StudioForm.ChannelHeading(new Settings { ChannelName = "Elle yazılan ad" }) == "YouTube kanalı bağlı değil", "Elle yazılan ad bağlı kanal sayıldı");

        });

        Test("LOGIN-REMEMBER-RECOVERY", "Şifre Windows kasasında hatırlanır, unutma ve şifre değişimi eski kaydı temizler", () => {

            var st = New("login-recovery"); string first = " initial-password-123 ", next = "replacement-password-456";

            AppLock.Set(st.Root, first); Check(!AppLock.CanRecover(st.Root), "Kurtarma girişten önce açıldı"); Reject(() => AppLock.ResetWithWindows(st.Root, next));

            AppLock.EnrollRecovery(st.Root); Check(AppLock.CanRecover(st.Root), "Windows kurtarma kaydı çalışmıyor");

            AppLock.Remember(st.Root, first, true); Check(AppLock.Remembered(st.Root) == first && !Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(st.Root, "login-remember.dat"))).Contains(first), "Şifre düz metin veya hatalı kaydedildi");

            Reject(() => AppLock.Remember(st.Root, "wrong-password-123", true)); AppLock.Remember(st.Root, "", false); Check(AppLock.Remembered(st.Root) == "", "Hatırlama kapatılamadı");

            AppLock.Remember(st.Root, first, true); var project = Path.Combine(st.Root, "project.txt"); File.WriteAllText(project, "İçeriğim"); AppLock.ResetWithWindows(st.Root, next);

            Check(AppLock.Verify(st.Root, next) && !AppLock.Verify(st.Root, first) && AppLock.Remembered(st.Root) == "" && AppLock.CanRecover(st.Root) && File.ReadAllText(project) == "İçeriğim", "Sıfırlama eski şifreyi veya içeriği hatalı işledi");

            File.WriteAllText(Path.Combine(st.Root, "login-recovery.dat"), "tampered"); Check(!AppLock.CanRecover(st.Root), "Bozuk kurtarma kabul edildi"); Reject(() => AppLock.ResetWithWindows(st.Root, first));

        });

        Test("TIMELINE-NARROW-TITLE", "Uzun videoda dar başlığın sağ kenarı uzar; sol kenarı ayrı ayarlanır", () => {

            var script = new VideoScript { TextLayout = new TextLayout { TitleEnabled = true }, Scenes = [new Scene { Duration = 300, OnScreen = "Başlık", TitleStart = 30, TitleDuration = 4 }] };

            using var timeline = new EditorTimeline { Width = 720, Script = script };

            timeline.TestDrag(34, 50, 95); Check(Math.Abs(script.Scenes[0].TitleDuration - 20) < 1 && script.Scenes[0].TitleStart == 30, "Dar başlık sağ kenar yerine sol kenardan taşındı");

            timeline.TestDrag(30, 35, 95); Check(Math.Abs(script.Scenes[0].TitleStart - 35) < 1 && Math.Abs(script.Scenes[0].TitleStart + script.Scenes[0].TitleDuration - 50) < 1, "Sol kenar bitişi değiştirdi");

        });

        Test("EDITOR-EMPTY-SHORT-MUSIC", "Genel müzik boşken eski paketin onaylı parçası boş Shorts kaydına geçer", () => {

            var plan = Plan(); plan.Long!.MusicTrack = new MusicTrack { Enabled = true, File = video, Source = "A.YURTSEVEN", License = "Müzikler bana aittir", RightsConfirmed = true };

            plan.Shorts = [new(), new() { MusicTrack = new MusicTrack { Enabled = true } }, new() { MusicTrack = new MusicTrack { Enabled = false } }];

            foreach (var script in plan.Shorts) TextEditSave.InheritPackageMusic(plan, script, true, "", .12);

            plan.Shorts[0].MusicTrack!.Validate(); plan.Shorts[1].MusicTrack!.Validate();

            Check(plan.Shorts[0].MusicTrack!.File == video && plan.Shorts[1].MusicTrack!.RightsConfirmed && !plan.Shorts[2].MusicTrack!.Enabled && !ReferenceEquals(plan.Long.MusicTrack, plan.Shorts[0].MusicTrack), "Boş Shorts devralma hatalı");

        });

        Test("EDITOR-SAVE-LATE-MUSIC", "Normal videoda sonradan seçilen müzik boş açık Shorts'a geçer; kapalı ve farklı parça korunur", () => {

            var plan = Plan(); var normal = new MusicTrack { Enabled = true, File = video, Source = "AHMET ANIL", License = "Müzikler bana aittir", RightsConfirmed = true, FadeIn = 30, Volume = .18 }; plan.Long!.MusicTrack = normal;

            var empty = new MusicTrack { Enabled = true, Volume = .12, FadeIn = .5 }; var disabled = new MusicTrack { Enabled = false }; var other = new MusicTrack { Enabled = true, File = "another.mp3" };

            plan.Shorts = [new() { MusicTrack = empty }, new() { MusicTrack = disabled }, new() { MusicTrack = other }];

            TextEditSave.CompletePackageMusic(plan); normal.Validate(); empty.Validate(); disabled.Validate(); other.Validate();

            Check(empty.File == video && empty.Source == normal.Source && empty.RightsConfirmed && empty.Volume == .12 && empty.FadeIn == .5 && normal.FadeIn == 30 && disabled.File == "" && !disabled.Enabled && other.File == "another.mp3" && !other.RightsConfirmed, "Kaydetme sırasında müzik eşleme ayarları bozdu");

            var before = JsonSerializer.Serialize(plan); TextEditSave.CompletePackageMusic(plan); Check(before == JsonSerializer.Serialize(plan), "Tekrar kayıt ayarları değiştirdi");

            normal.RightsConfirmed = false; empty.File = ""; empty.RightsConfirmed = false; TextEditSave.CompletePackageMusic(plan); Check(empty.File == video && !empty.RightsConfirmed, "Müzik devralınmadı veya kayıtlı hak bilgisi değiştirildi");

        });

        Test("EDITOR-MUSIC-RIGHTS", "Aynı parçanın onayı Shorts'a aktarılır; farklı parça ve ses ayarları korunur", () => {

            var own = new MusicTrack { Enabled = true, File = video, Source = "A.YURTSEVEN", License = "Müzikler bana aittir", RightsConfirmed = true };

            var shortTrack = new MusicTrack { Enabled = true, File = video, Volume = .17 };

            var other = new MusicTrack { Enabled = true, File = Path.Combine(root, "other.mp3") };

            var plan = Plan(); plan.Long!.MusicTrack = own; plan.Shorts = [new() { MusicTrack = shortTrack }, new() { MusicTrack = other }];

            TextEditSave.ShareMusicRights(plan, own); shortTrack.Validate();

            Check(shortTrack.Source == "A.YURTSEVEN" && shortTrack.Volume == .17 && !other.RightsConfirmed, "Parça onayı veya ses ayarları hatalı");

            var ep = new Episode { IncludeLong = true, ShortCount = 2, Plan = plan, QueueMusic = other, QueueLayout = new TextLayout() };

            plan.Long.TextLayout = new TextLayout { AutoSceneTitles = true }; VideoQueue.PrepareDesign(ep, new Settings());

            Check(ReferenceEquals(plan.Long.MusicTrack, own) && plan.Long.TextLayout.AutoSceneTitles, "Sıra hazırlığı editörde kaydedilen onayı ezdi");

        });

        Test("AI-SCENE-TITLES", "Bütün sahneler başlık alır; anlatım, altyazı, dosya ve video adı korunur; cache tekrar çağırmaz", () => {

            var st = New("ai-scene-titles"); var script = new VideoScript { Title = "Özgün video adı", Description = "Özgün açıklama", Scenes = Enumerable.Range(1, 10).Select(i => new Scene { Narration = "Özgün anlatım " + i, SubtitleText = "Altyazı " + i, StockFile = "scene-" + i + ".mp4", OnScreen = "Eski başlık" }).ToList() }; var before = script.Scenes.Select(s => new { s.Narration, s.SubtitleText, s.StockFile }).ToArray(); int calls = 0;

            Task<string> Request(string system, string prompt, Func<string,bool> validate, CancellationToken ct) { calls++; using var doc = JsonDocument.Parse(prompt); var raw = JsonSerializer.Serialize(new { Titles = doc.RootElement.GetProperty("Scenes").EnumerateArray().Select(s => new { Scene = s.GetProperty("Scene").GetInt32(), Title = "Sahne " + s.GetProperty("Scene").GetInt32() + " kısa başlığı" }) }); validate(raw); return Task.FromResult(raw); }

            SceneHeadings.Generate(new Services(st, _ => {}), script, CancellationToken.None, request: Request).GetAwaiter().GetResult();

            Check(calls == 2 && script.Scenes.Select(s => s.OnScreen).Distinct().Count() == 10 && JsonSerializer.Serialize(before) == JsonSerializer.Serialize(script.Scenes.Select(s => new { s.Narration, s.SubtitleText, s.StockFile })) && script.Title == "Özgün video adı" && script.Description == "Özgün açıklama", "Başlıklar eksik veya anlatım değişti");

            SceneHeadings.Generate(new Services(st, _ => {}), script, CancellationToken.None, request: Request).GetAwaiter().GetResult(); Check(calls == 2, "Başlık önbelleği kullanılmadı");

            script.TextLayout = new TextLayout { TitleSeconds = 7.5 }; foreach (var scene in script.Scenes) { scene.Duration = 20; scene.TitleDuration = 1; }

            SceneHeadings.Generate(new Services(st, _ => {}), script, CancellationToken.None, force: true, request: Request).GetAwaiter().GetResult(); Check(script.Scenes.All(s => s.TitleDuration == 0) && script.TextLayout.TitleSeconds == 7.5, "Eski sahne süreleri seçilen süreyi ezdi");

            var ass = TextDesign.ForVideo([], script, 1920, 1080); Check(ass.Contains("0:00:00.00,0:00:07.50,Title"), "Başlık seçilen süre boyunca görünmedi");

            script.Scenes[0].Duration = 2; ass = TextDesign.ForVideo([], script, 1080, 1920); Check(ass.Contains("0:00:00.00,0:00:02.00,Title"), "Başlık sahne sonunu aştı");

            script.Scenes[0].Narration += " değişti"; SceneHeadings.Generate(new Services(st, _ => {}), script, CancellationToken.None, request: Request).GetAwaiter().GetResult(); Check(calls == 6, "Metin değişince başlık yenilenmedi");

        });

        Test("AI-TITLE-PROVIDERS", "Gemini ve Groq başlık isteği doğru JSON biçimini ve hazır metin korumasını kullanır", () => {

            foreach (var provider in new[] { "Gemini", "Groq" }) {

                var st = New("ai-title-provider-" + provider); st.Settings.ProviderOrder = provider; st.Settings.GroqModel = "openai/gpt-oss-120b"; int calls = 0; var key = provider == "Groq" ? "GROQ_API_KEY" : "GEMINI_API_KEY"; var old = Environment.GetEnvironmentVariable(key);

                try {

                    Environment.SetEnvironmentVariable(key, "fixture");

                    using var client = new HttpClient(new Handler(async req => {

                        using var body = JsonDocument.Parse(await req.Content!.ReadAsStringAsync()); var payload = body.RootElement;

                        Check(payload.GetProperty("max_tokens").GetInt32() == 2048, "Başlık bütçesi yanlış");

                        if (provider == "Groq") { var schema = payload.GetProperty("response_format").GetProperty("json_schema").GetProperty("schema"); Check(schema.GetProperty("properties").TryGetProperty("Titles", out _) && !schema.GetProperty("properties").TryGetProperty("Long", out _), "Başlık yerine senaryo şeması gönderildi"); }

                        var content = ++calls == 1 ? "{\"Titles\":[]}" : "{\"Titles\":[{\"Scene\":1,\"Title\":\"Denizin Önünde Güven\"}]}";

                        return Ok(JsonSerializer.Serialize(new { choices = new[] { new { message = new { content } } } }));

                    }));

                    var e = new Episode { Topic = "Hazır metin", IncludeLong = false, ShortCount = 1, UserNarration = new UserNarrationOptions { Enabled = true, Shorts = ["Kullanıcının özgün anlatımı."] }, QueueLayout = new TextLayout { AutoSceneTitles = true }, Plan = new ContentPlan { Shorts = [new() { Title = "Kıssanın adı", Scenes = [new() { Narration = "Kullanıcının özgün anlatımı.", SubtitleText = "Özgün altyazı", OnScreen = "Eski başlık", VisualQuery = "sea" }] }] } };

                    var services = new Services(st, _ => {}, client); services.PrepareSceneHeadings(e, CancellationToken.None).GetAwaiter().GetResult();

                    Check(calls == 2 && e.Plan.Shorts[0].Scenes[0].OnScreen == "Denizin Önünde Güven" && e.Plan.Shorts[0].Scenes[0].Narration == e.UserNarration.Shorts[0] && e.Plan.Shorts[0].Scenes[0].SubtitleText == "Özgün altyazı" && e.Plan.Shorts[0].TextLayout!.AutoSceneTitles, "Başlık düzeltmesi veya hazır metin koruması çalışmadı");

                    services.PrepareSceneHeadings(e, CancellationToken.None).GetAwaiter().GetResult(); Check(calls == 2, "Üretimde aynı başlık tekrar istendi");

                } finally { Environment.SetEnvironmentVariable(key, old); }

            }

        });

        Test("AI-TITLE-GUARDS", "Eksik, yinelenen ve hatalı başlık reddedilir; geç hata ve iptal eski başlıkları korur", () => {

            foreach (var raw in new[] { "{\"Titles\":[]}", "{\"Titles\":[{\"Scene\":1,\"Title\":\"Başlık\"},{\"Scene\":1,\"Title\":\"Başlık\"}]}", "{\"Titles\":[{\"Scene\":\"1\",\"Title\":\"Başlık\"}]}", "{\"Titles\":[{\"Scene\":1,\"Title\":null}]}", "{\"Titles\":[{\"Scene\":1,\"Title\":\"http://x\"}]}", "{\"Titles\":[{\"Scene\":1,\"Title\":\"Başlık\",\"Narration\":\"Yeni anlatım\"}]}" }) Reject(() => SceneHeadings.Parse(raw, [1]));

            var st = New("ai-title-guards"); var script = new VideoScript { Scenes = Enumerable.Range(0,9).Select(i => new Scene { Narration = "Metin " + i, OnScreen = "Korunan " + i }).ToList() }; var before = JsonSerializer.Serialize(script); int calls = 0;

            Task<string> FailLate(string system, string prompt, Func<string,bool> validate, CancellationToken ct) { if (++calls == 2) throw new InvalidOperationException("fixture failure"); using var doc = JsonDocument.Parse(prompt); return Task.FromResult(JsonSerializer.Serialize(new { Titles = doc.RootElement.GetProperty("Scenes").EnumerateArray().Select(s => new { Scene = s.GetProperty("Scene").GetInt32(), Title = "Yeni başlık" }) })); }

            Reject(() => SceneHeadings.Generate(new Services(st, _ => {}), script, CancellationToken.None, request: FailLate).GetAwaiter().GetResult()); Check(JsonSerializer.Serialize(script) == before, "Geç hata kısmi başlık yazdı");

            using var cancelled = new CancellationTokenSource(); Task<string> Cancel(string system, string prompt, Func<string,bool> validate, CancellationToken ct) { cancelled.Cancel(); ct.ThrowIfCancellationRequested(); return Task.FromResult(""); }

            bool stopped = false; try { SceneHeadings.Generate(new Services(st, _ => {}), script, cancelled.Token, request: Cancel).GetAwaiter().GetResult(); } catch (OperationCanceledException) { stopped = true; } Check(stopped && JsonSerializer.Serialize(script) == before, "İptal mevcut başlıkları değiştirdi");

        });

        Test("SERIES-NUMBERS", "Hadis serisi yinelenmez; eski tekrarlar, yeniden deneme ve yeniden açılış hesaba katılır", () => {

            var st = New("series-numbers");

            for (int i = 0; i < 2; i++) { var old = new Episode { Topic = "Hadis Sohbetleri 1", ShortCount = 0, Plan = Plan() }; old.Plan.Long!.Title = "Hadis Sohbetleri 1"; old.Videos.Add(new() { Key = "long", Title = "Hadis Sohbetleri 1", YouTubeId = "uploaded-" + i }); st.Episodes.Add(old); }

            var e = new Episode { Topic = "Hadis Sohbetleri", ShortCount = 1, Plan = Plan() }; e.Plan.Long!.Title = "Hadis Sohbetleri 1"; e.Plan.Shorts.Add(new() { Title = "Hadis Sohbetleri 1", ThumbnailText = "Hadis Sohbetleri 1", Scenes = [new() { Narration = "Özgün metin", VisualQuery = "sea" }] }); st.Episodes.Add(e);

            SeriesTitles.Apply(st, e); Check(e.Plan.Long.Title == "Hadis Sohbetleri 3" && e.Plan.Shorts[0].Title == "Hadis Sohbetleri 4", "Geçmiş sayısı veya video numarası yanlış");

            e.Plan.Long.Title = "Hadis Sohbetleri 1"; SeriesTitles.Apply(st, e); Check(e.Plan.Long.Title == "Hadis Sohbetleri 3", "Yeniden denemede yeni numara tüketildi"); st.Save();

            var reopened = new Store(st.Root); reopened.Episodes.Clear(); var next = new Episode { Topic = "Hadis Sohbetleri", ShortCount = 0, Plan = Plan() }; next.Plan.Long!.Title = "Başka başlık"; reopened.Episodes.Add(next); SeriesTitles.Apply(reopened, next); Check(next.Plan.Long.Title.StartsWith("Hadis Sohbetleri 5 •"), "Kalıcı sayaç sıfırlandı");

            var unrelated = new Episode { Topic = "Başka konu", ShortCount = 0, Plan = Plan() }; unrelated.Plan.Long!.Title = "Yıl 2026"; SeriesTitles.Apply(reopened, unrelated); Check(unrelated.Plan.Long.Title == "Yıl 2026", "Seri dışı başlık değişti");

        });

        Test("OWN-TEXT-PLAN", "Hazır metin AI çağırmadan aynen planlanır; süre düzeltmesi ve yazım AI'sı atlanır", () => {

            var st = New("own-text-plan"); st.Settings.CloudAiEnabled = false; st.Settings.LocalAiFirst = false;

            string text = "Hz.Muhammet hakkında kullanıcı metni.\r\n" + string.Join(" ", Enumerable.Repeat("Özgün anlatım aynen kalacak.", 45));

            var e = new Episode { Topic = "Kendi metnim", IncludeLong = true, ShortCount = 1, UserNarration = new UserNarrationOptions { Enabled = true, LongText = text, Shorts = ["Kendi kısa metnim. Hz. Ali."], VisualTheme = "orman; deniz" } }; st.Episodes.Add(e);

            new Engine(st).Plan(e).GetAwaiter().GetResult(); Check(e.Error == "" && e.Plan != null, "Hazır metin planı hazırlanamadı: " + e.Error);

            Check(string.Concat(e.Plan!.Long!.Scenes.Select(x => x.Narration)) == text && string.Concat(e.Plan.Shorts[0].Scenes.Select(x => x.Narration)) == e.UserNarration.Shorts[0], "Kullanıcı metni değiştirildi");

            Check(ScriptLength.Problem(e, e.Plan) == null && !e.Events.Any(x => x.Contains("ile hazırlanıyor") || x.Contains("Web kaynakları taranıyor")), "AI üretimi veya kelime düzeltmesi çalıştı");

            var prior = Reliability.MediaPlanHash(e); e.UserNarration.LongText += " Değişiklik."; Check(prior != Reliability.MediaPlanHash(e), "Metin değişikliği çıktıyı geçersiz kılmadı");

            var file = Path.Combine(st.Root, "user.txt"); File.WriteAllText(file, text, new UTF8Encoding(false)); Check(UserNarrationEditor.ReadText(file) == text, "TXT içeriği değişti");

        });

        Test("OWN-TEXT-RENDER", "Hazır metin gerçek ses süresiyle kodlanır; Hazreti telaffuzu altyazıyı değiştirmez", () => {

            var st = New("own-text-render"); st.Settings.EncodingPreset = "ultrafast"; st.Settings.FramesPerSecond = 12; st.Settings.CoverAiEnabled = false; st.Settings.OpeningCardEnabled = st.Settings.ClosingCardEnabled = false; st.Settings.VoiceProvider = "Piper / ücretsiz"; st.Settings.PiperPath = "missing.exe"; st.Settings.PiperModel = "missing.onnx";

            var e = new Episode { Topic = "Hazır anlatım", IncludeLong = true, ShortCount = 0, LongMinutes = 5, Music = false, UserNarration = new UserNarrationOptions { Enabled = true, LongText = "Hz.Muhammet hakkında kendi yazdığım metin.", VisualTheme = "", ManualVisuals = true, LocalVideos = [video], LocalVideoRightsConfirmed = true } }; st.Episodes.Add(e); var engine = new Engine(st); engine.Plan(e).GetAwaiter().GetResult(); Check(e.Error == "", e.Error);

            foreach (var scene in e.Plan!.Long!.Scenes) { scene.StockFile = video; scene.LicenseUrl = "https://example.com/test-license"; scene.RightsConfirmed = true; }

            var seed = Path.Combine(st.Folder(e), "r" + e.Revision + "-long", "seed"); Directory.CreateDirectory(seed); int calls = 0;

            Task Speak(string text, string path, Settings voice, CancellationToken ct) { calls++; Check(text.Contains("Hazreti Muhammet") && !text.Contains("Hz."), "TTS kısaltmayı aldı"); return Media.Exec(ffmpeg, ["-y", "-f", "lavfi", "-i", "sine=frequency=440:duration=3", path], st.Root, ct); }

            NarrationFlow.Prepare(st, e, e.Plan.Long!, st.Settings, seed, new Services(st, _ => {}), CancellationToken.None, Speak).GetAwaiter().GetResult();

            engine.Produce(e).GetAwaiter().GetResult(); Check(e.Error == "" && e.Videos.Count == 1 && e.Videos[0].QualityPassed && Math.Abs(e.Videos[0].Duration - 3) < .4 && calls == 1, "Hazır metin render başarısız: " + e.Error);

            Check(string.Concat(e.Plan.Long.Scenes.Select(s => s.Narration)) == e.UserNarration.LongText && File.ReadAllText(Path.Combine(Path.GetDirectoryName(e.Videos[0].File)!, "subtitles.srt")).Contains("Hz.Muhammet"), "Metin / altyazı değişti");

            st.Settings.YouTubeChannelId = "fixture-channel"; engine.Approve(e); Check(e.Approved, "Hazır metin onaylanmadı");

        });

        Test("MANUAL-VIDEOS", "Manuel videolar sıralı dağıtılır; eski stok ve kayıp dosya otomatik aramaya geçemez", () => {

            var st = New("manual-videos"); var second = Path.Combine(st.Root, "second.mp4"); File.Copy(video, second);

            var e = new Episode { Topic = "Manuel", ShortCount = 1, UserNarration = new UserNarrationOptions { Enabled = true, ManualVisuals = true, VisualTheme = "", LongText = string.Join(" ", Enumerable.Repeat("Kendi hazır anlatım metnim.", 150)), Shorts = ["Kısa anlatım."], LocalVideos = [video, second], LocalVideoRightsConfirmed = true } };

            var plan = UserNarration.Plan(e); Check(plan.Long!.Scenes.Count > 2, "Çok sahneli plan eksik");

            Check(plan.Long.Scenes[0].StockFile == video && plan.Long.Scenes[1].StockFile == second && plan.Long.Scenes[2].StockFile == video && plan.Shorts[0].Scenes[0].StockFile == video, "Sıralama / tekrar bozuldu");

            Check(plan.Long.Scenes.All(s => s.UserLocal && s.RightsConfirmed), "Manuel metadata eksik");

            var first = plan.Long.Scenes[0]; SceneVideoChoice.Apply(first, second, null); ManualVideos.Apply(e, plan); Check(first.StockFile == second, "Editör tercihi ezildi");

            first.StockFile = "old-automatic.mp4"; first.UserLocal = false; ManualVideos.Apply(e, plan); Check(first.StockFile == video && first.UserLocal, "Eski stok kaldı");

            e.Plan = plan; e.UserNarration.ShortVideos = [new() { UseSeparate = true, Files = [second] }]; ManualVideos.Apply(e, plan);

            Check(plan.Shorts[0].Scenes[0].StockFile == second && plan.Long.Scenes[0].StockFile == video, "Shorts seçimi normal videoya karıştı");

            Check(ManualVideos.Files(e, plan.Shorts[0]).SequenceEqual(new[] { second }), "Shorts üretimi ortak listeye döndü");

            e.UserNarration.LocalVideos.Reverse(); ManualVideos.Apply(e, plan); Check(plan.Long.Scenes[0].StockFile == second && plan.Shorts[0].Scenes[0].StockFile == second, "Sıra değişikliği plana uygulanmadı"); e.UserNarration.LocalVideos.Reverse(); ManualVideos.Apply(e, plan);

            e.UserNarration.ShortVideos[0].Files.Clear(); Reject(() => ManualVideos.Validate(e)); e.UserNarration.ShortVideos[0].Files.Add(second);

            e.IncludeLong = false; e.UserNarration.LocalVideos.Clear(); ManualVideos.Validate(e); Check(ManualVideos.AllFiles(e).SequenceEqual(new[] { second }), "Yalnız Shorts modu ortak liste istedi");

            e.IncludeLong = true; e.UserNarration.LocalVideos = [video, second];

            var restored = JsonSerializer.Deserialize<UserNarrationOptions>(JsonSerializer.Serialize(e.UserNarration))!; Check(restored.ManualVisuals && restored.LocalVideos.SequenceEqual(e.UserNarration.LocalVideos), "Kayıt yeniden açılmadı");

            File.Delete(second); Reject(() => ManualVideos.Apply(e, plan)); e.UserNarration.LocalVideos.Clear(); Reject(() => ManualVideos.Validate(e));

            e.UserNarration.ManualVisuals = false; ManualVideos.Validate(e);

            using var editor = new UserNarrationEditor(restored, true, 1); editor.Show(); Application.DoEvents();

            var tabs = editor.Controls.OfType<StudioTabs>().Single(); tabs.SelectedIndex = tabs.TabCount - 1; Application.DoEvents();

            using var capture = new Bitmap(editor.Width, editor.Height); editor.DrawToBitmap(capture, new Rectangle(Point.Empty, editor.Size)); capture.Save(Path.Combine(st.Root, "manual-videos-editor.png"));

            var nestedTabs = tabs.TabPages[tabs.TabCount - 1].Controls[0].Controls.OfType<StudioTabs>().Single(); nestedTabs.SelectedIndex = 1; Application.DoEvents(); editor.DrawToBitmap(capture, new Rectangle(Point.Empty, editor.Size)); capture.Save(Path.Combine(st.Root, "manual-shorts-editor.png")); editor.Close();

        });

        Test("MANUAL-RENDER", "Yatay ve dikey seçili videolar ayrı üretilir; bozuk dosya seslendirmeden önce durur", () => {

            var st = New("manual-render"); st.Settings.EncodingPreset = "ultrafast"; st.Settings.FramesPerSecond = 12; st.Settings.CoverAiEnabled = false; st.Settings.OpeningCardEnabled = st.Settings.ClosingCardEnabled = false; st.Settings.VoiceProvider = "Piper / ücretsiz"; st.Settings.PiperPath = "missing.exe"; st.Settings.PiperModel = "missing.onnx";

            var portrait = Path.Combine(st.Root, "portrait.mp4"); Media.Exec(ffmpeg, ["-y", "-f", "lavfi", "-i", "color=c=red:s=90x160:r=10", "-t", "1", "-c:v", "libx264", "-pix_fmt", "yuv420p", portrait], st.Root, CancellationToken.None).GetAwaiter().GetResult();

            var e = new Episode { Topic = "Manuel format testi", ShortCount = 1, Music = false, UserNarration = new() { Enabled = true, ManualVisuals = true, LongText = "Normal video için özgün anlatım.", Shorts = ["Dikey video için özgün anlatım."], VisualTheme = "", LocalVideos = [video], ShortVideos = [new() { UseSeparate = true, Files = [portrait] }], LocalVideoRightsConfirmed = true } }; st.Episodes.Add(e);

            var engine = new Engine(st); engine.Plan(e).GetAwaiter().GetResult(); Check(e.Error == "", e.Error);

            Task Speak(string text, string path, Settings voice, CancellationToken ct) => Media.Exec(ffmpeg, ["-y", "-f", "lavfi", "-i", "sine=frequency=440:duration=3", path], st.Root, ct);

            foreach (var (key, _, script) in Rules.Scripts(e)) { var seed = Path.Combine(st.Folder(e), "r" + e.Revision + "-" + key, "seed"); Directory.CreateDirectory(seed); NarrationFlow.Prepare(st, e, script, st.Settings, seed, new Services(st, _ => {}), CancellationToken.None, Speak).GetAwaiter().GetResult(); }

            engine.Produce(e).GetAwaiter().GetResult(); Check(e.Error == "" && e.Videos.Count == 2 && e.Videos.All(v => v.QualityPassed), "İki format üretilemedi: " + e.Error);

            Check(e.Plan!.Long!.Scenes.All(s => s.StockFile == video) && e.Plan.Shorts[0].Scenes.All(s => s.StockFile == portrait), "Format listeleri karıştı");

            File.WriteAllText(portrait, "invalid video"); engine.Produce(e).GetAwaiter().GetResult(); Check(e.Error.Contains("Seçtiğin video okunamadı") && e.Error.Contains("Otomatik arama yapılmadı"), "Bozuk dosya otomatik aramaya geçti: " + e.Error);

            File.Delete(portrait); Reject(() => engine.Produce(e).GetAwaiter().GetResult());

        });

        Test("SPOKEN-APOSTROPHES", "Kelime içi kesme işaretleri seslendirmede durak oluşturmaz; özgün metin korunur", () => {

            const string original="Nur Kapısı’ndaki Allah'ın rahmeti, Türkiye’nin ve Musa’ya anlatılanlar.";

            Check(NarrationFlow.SpokenText(original)=="Nur Kapısındaki Allahın rahmeti, Türkiyenin ve Musaya anlatılanlar.","Ekler bölünerek okunuyor");

            foreach(var apostrophe in new[]{"'","’","‘","ʼ","´","`","＇"})Check(NarrationFlow.SpokenText("Nur Kapısı"+apostrophe+"ndaki Allah"+apostrophe+"ın")=="Nur Kapısındaki Allahın","Kesme çeşidi dönüştürülmedi: "+apostrophe);

            Check(NarrationFlow.SpokenText("Allah’ın, Ankara'dakiler, 2026'nın")=="Allahın, Ankaradakiler, 2026nın","Uzun ek kaldı");

            Check(NarrationFlow.SpokenText("‘Merhaba’ dedi. Dur, sonra devam et!")=="‘Merhaba’ dedi. Dur, sonra devam et!","Alıntı işareti veya cümle noktalaması değişti");

            var script=new VideoScript{Scenes=[new(){Narration=original}]};NarrationFlow.SpokenText(script.Scenes[0].Narration);Check(script.Scenes[0].Narration==original,"Ekran metni değişti");

        });

        Test("OWN-TEXT-GUARDS", "Eksik kullanıcı metni reddedilir; yalnız bu modda kaynak sayısı muaf olur", () => {

            var e = new Episode { Topic = "Kendi metnim", IncludeLong = false, ShortCount = 1, UserNarration = new UserNarrationOptions { Enabled = true } }; Reject(() => Rules.Validate(e));

            e.UserNarration.Shorts = ["Hazır ve kısa metnim."]; Rules.Validate(e); e.Plan = UserNarration.Plan(e); Rules.ValidatePlan(e);

            e.UserNarration.Enabled = false; Reject(() => Rules.ValidatePlan(e)); e.UserNarration.Enabled = true;

            e.UserNarration.Shorts = [string.Join(" ", Enumerable.Repeat("kelime", 361))]; Reject(() => Rules.Validate(e));

            var track = new MusicTrack { Enabled = true, File = "song.wav", Source = "Sanatçı", License = "Telifli • kullanım iznim yok", RightsConfirmed = true }; track.Validate();

            Check(NarrationFlow.SpokenText("Hz.Muhammet, hz. Ali, HZ Ömer ve 50Hz.") == "Hazreti Muhammet, Hazreti Ali, Hazreti Ömer ve 50Hz.", "Hazreti dönüşümü yanlış");

            foreach(var abbreviation in new[]{"(s.a.v)","(s.a.v.)","s.a.v.","S.A.V.","( S. A. V. )"})Check(NarrationFlow.SpokenText("Hz. Muhammed "+abbreviation)=="Hazreti Muhammed sallallahu aleyhi vessellem","Salavat dönüşümü hatalı: "+abbreviation);

            foreach(var abbreviation in new[]{"Ra","(Ra)","r.a.","(r.a.)","R.A."})Check(NarrationFlow.SpokenText("Hz Ali "+abbreviation)=="Hazreti Ali radıyallahu anh","Ra dönüşümü hatalı: "+abbreviation);

            foreach(var abbreviation in new[]{"cc","(cc)","c.c.","(c.c.)","C.C."})Check(NarrationFlow.SpokenText("Allah "+abbreviation)=="Allah celle celaluhu","Cc dönüşümü hatalı: "+abbreviation);

            Check(NarrationFlow.SpokenText("Ra, radar ve 50Hz.")=="Ra, radar ve 50Hz.","Benzer kelimeler yanlış dönüştü.");

        });

        Test("MUSIC-CLIP-RENDER", "Parça ana ses; stok görüntü, sabit kredi, sıfır TTS ve şarkı altyazısı", () => {

            var st = New("music-clip-render"); st.Settings.EncodingPreset = "ultrafast"; st.Settings.FramesPerSecond = 12; st.Settings.CoverAiEnabled = false; st.Settings.OpeningCardEnabled = st.Settings.ClosingCardEnabled = false; st.Settings.VoiceProvider = "Piper / ücretsiz"; st.Settings.PiperPath = "missing.exe"; st.Settings.PiperModel = "missing.onnx";

            var music = Path.Combine(st.Root, "song.wav"); Media.Exec(ffmpeg, ["-y", "-f", "lavfi", "-i", "sine=frequency=880:sample_rate=44100:duration=8", "-c:a", "pcm_s16le", music], st.Root, CancellationToken.None).GetAwaiter().GetResult();

            var e = new Episode { Topic = "Klip testi", IncludeLong = true, ShortCount = 0, Music = true, PublishAt = DateTimeOffset.UtcNow.AddDays(1), QueueMusic = new MusicTrack { Enabled = true, File = music, Volume = 1, Duck = false, FadeIn = 0, FadeOut = 0, RightsConfirmed = true, Source = "Kendi kaydım", License = "Kendi eserim", Attribution = "Kayıt sahibi" }, MusicClip = new MusicClipOptions { TrackTitle = "Test Parçası", Artist = "Sanatçı", LyricsBy = "Söz Yazarı", MusicBy = "Besteci", Duration = 8 }, QueueSettings = VideoQueue.Snapshot(st.Settings) };

            st.Episodes.Add(e); VideoQueue.Validate(e, st.Settings); var engine = new Engine(st); engine.Plan(e).GetAwaiter().GetResult(); Check(e.Error == "" && e.Plan != null && e.Plan.Sources.Count == 0, "Klip planı araştırma / AI istedi: " + e.Error);

            foreach (var scene in e.Plan!.Long!.Scenes) { scene.StockFile = video; scene.LicenseUrl = "https://example.com/test-license"; scene.RightsConfirmed = true; }

            VideoQueue.PrepareDesign(e, st.Settings); engine.Produce(e).GetAwaiter().GetResult();

            Check(e.Error == "" && e.Videos.Count == 1 && e.Videos[0].QualityPassed && Math.Abs(e.Videos[0].Duration - 8) < .3, "Klip üretimi başarısız: " + e.Error);

            var folder = Path.GetDirectoryName(e.Videos[0].File)!; Check(string.IsNullOrWhiteSpace(File.ReadAllText(Path.Combine(folder, "subtitles.srt"))) && File.ReadAllText(Path.Combine(folder, "subtitles.ass")).Contains("Söz: Söz Yazarı"), "Altyazı / sabit kredi yanlış");

            Check(!e.Events.Any(x => x.Contains("ile hazırlanıyor") || x.Contains("ses sağlayıcısı:")) && YouTube.BuildDescription(e.Plan.Long!, []).Contains("Kayıt sahibi"), "Seslendirme çağrısı veya eksik atıf");

            st.Settings.YouTubeChannelId = "fixture-channel"; engine.Approve(e); Check(e.Approved && Reliability.OutputsCurrent(e), "Klip onaylanamadı");

            var hash = Reliability.MediaPlanHash(e); File.Move(music, music + ".away"); try { Check(Reliability.MediaPlanHash(e) == hash, "Hazır videonun bulut yükleme imzası ana dosya olmadan değişti"); } finally { File.Move(music + ".away", music); }

            var pcm = Path.Combine(st.Root, "clip-audio.pcm"); Media.Exec(ffmpeg, ["-y", "-i", e.Videos[0].File, "-vn", "-ar", "44100", "-ac", "1", "-f", "s16le", pcm], st.Root, CancellationToken.None).GetAwaiter().GetResult();

            var bytes = File.ReadAllBytes(pcm); double real = 0, imaginary = 0; for (int i = 0; i < 44100; i++) { double sample = BitConverter.ToInt16(bytes, (i + 44100) * 2) / 32768d; double angle = 2 * Math.PI * 880 * i / 44100; real += sample * Math.Cos(angle); imaginary += sample * Math.Sin(angle); }

            Check(2 * Math.Sqrt(real * real + imaginary * imaginary) / 44100 > .06, "Ana parça fon gibi kısıldı veya kayboldu");

        });

        Test("MUSIC-CLIP-GUARDS", "Klip hak onayı, süre ve ana parça kontrolü zorunludur", () => {

            var st = New("music-clip-guards"); var e = new Episode { Topic = "Klip", IncludeLong = true, ShortCount = 0, MusicClip = new MusicClipOptions { TrackTitle = "Parça", Duration = 8 }, QueueMusic = new MusicTrack { Enabled = true, File = video, Volume = 1, Duck = false, Source = "Kayıt sahibi", License = "İzinli", RightsConfirmed = true } };

            MusicClips.Validate(e);e.QueueMusic.RightsConfirmed=false;MusicClips.Validate(e);var original=e.QueueMusic.File;e.QueueMusic.File="missing.wav";Reject(()=>MusicClips.Validate(e));e.QueueMusic.File=original;e.MusicClip.Duration=0;Reject(()=>MusicClips.Validate(e));e.MusicClip.Duration=8;

            e.MusicClip.Duration = double.NaN; Reject(() => MusicClips.Validate(e)); e.MusicClip.Duration = 8; e.QueueMusic.Duck = true; Reject(() => MusicClips.Validate(e)); e.QueueMusic.Duck = false;

            e.MusicClip.TrackTitle = ""; Reject(() => MusicClips.Validate(e));

        });

        Test("CLIP-OPTIONS-PHOTOS", "Klip altyazı / ses seçenekleri ve fotoğraf kaynağı gerçek medya çıktısıyla doğrulanır",()=>{
            var st=New("clip-options-photos");st.Settings.EncodingPreset="ultrafast";st.Settings.FramesPerSecond=12;st.Settings.OpeningCardEnabled=st.Settings.ClosingCardEnabled=st.Settings.CoverAiEnabled=false;
            var music=Path.Combine(st.Root,"song.wav");Media.Exec(ffmpeg,["-y","-f","lavfi","-i","sine=frequency=880:duration=8","-c:a","pcm_s16le",music],st.Root,CancellationToken.None).GetAwaiter().GetResult();
            var e=new Episode {Topic="Deniz klibi",IncludeLong=true,ShortCount=0,Music=true,PhotosOnly=true,QueueMusic=new MusicTrack {Enabled=true,File=music,Volume=.3,Duck=false,Source="Test",License="Test izni",RightsConfirmed=true},MusicClip=new MusicClipOptions {TrackTitle="Deniz",VisualTheme="sea",Duration=8,SubtitlesEnabled=true,Text="Deniz ve huzur."},QueueSettings=VideoQueue.Snapshot(st.Settings)};
            var plan=MusicClips.Plan(st,e,CancellationToken.None).GetAwaiter().GetResult();e.Plan=plan;var script=plan.Long!;
            Check(script.TextLayout!.SubtitleEnabled && script.Scenes[0].Narration=="Deniz ve huzur.","Klip altyazı seçimi kayboldu.");
            var photo=Path.Combine(st.Root,"photo.jpg");using(var bitmap=new System.Drawing.Bitmap(640,360)){using(var g=System.Drawing.Graphics.FromImage(bitmap))g.Clear(System.Drawing.Color.Blue);bitmap.Save(photo,System.Drawing.Imaging.ImageFormat.Jpeg);}
            st.Vault.Set("PEXELS_API_KEY","fixture-only");int calls=0;
            using var http=new HttpClient(new Handler(req=>{
                calls++;
                if(req.RequestUri!.Host=="api.pexels.com"){Check(req.RequestUri.AbsolutePath=="/v1/search","Video API çağrıldı");return Task.FromResult(Ok("{\"photos\":[{\"id\":123,\"alt\":\"sea ocean\",\"url\":\"https://www.pexels.com/photo/123/\",\"photographer\":\"Fixture\",\"src\":{\"large2x\":\"https://images.pexels.com/fixture.jpg\"}}]}"));}
                Check(req.RequestUri.Host=="images.pexels.com","Bilinmeyen indirme adresi");return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(File.ReadAllBytes(photo))});
            }));
            var services=new Services(st,_=>{},http);services.StockPhoto(script.Scenes[0],false,st.Root,new HashSet<long>(),CancellationToken.None).GetAwaiter().GetResult();Check(calls==2 && script.Scenes[0].Attribution.Contains("Fixture"),"Fotoğraf / kaynak kaydı eksik");
            VideoQueue.PrepareDesign(e,st.Settings);var engine=new Engine(st);st.Episodes.Add(e);engine.Produce(e).GetAwaiter().GetResult();Check(e.Error=="" && e.Videos.Count==1,"Fotoğraf video üretimi: "+e.Error);
            var outputFolder=Path.GetDirectoryName(e.Videos[0].File)!;Check(File.ReadAllText(Path.Combine(outputFolder,"subtitles.srt")).Contains("Deniz ve huzur"),"Altyazı videoya aktarılmadı");
            var voiceFolder=Path.Combine(st.Root,"optional-voice");Directory.CreateDirectory(voiceFolder);e.MusicClip.NarrationEnabled=true;
            Task Speak(string text,string file,Settings settings,CancellationToken ct)=>Media.Exec(ffmpeg,["-y","-f","lavfi","-i","sine=frequency=440:duration=2","-c:a","pcm_s16le",file],st.Root,ct);
            MusicClips.PrepareAudio(st,e,script,voiceFolder,CancellationToken.None,st.Settings,services,Speak).GetAwaiter().GetResult();Check(Math.Abs(Media.Duration(st,Path.Combine(voiceFolder,"narration-full.wav"),CancellationToken.None).GetAwaiter().GetResult()-8)<.1,"Seslendirme müziğin süresini değiştirdi");
            Check(Json.Read(Path.Combine(voiceFolder,"speech-1.json"),new List<Caption>()).Max(c=>c.End)<3,"Altyazı müzik sonuna uzatıldı");
            e.MusicClip.Text="Bu anlatım müzikten uzundur.";var tooLong=MusicClips.Plan(st,e,CancellationToken.None).GetAwaiter().GetResult().Long!;
            Task LongSpeech(string text,string file,Settings settings,CancellationToken ct)=>Media.Exec(ffmpeg,["-y","-f","lavfi","-i","sine=frequency=440:duration=9","-c:a","pcm_s16le",file],st.Root,ct);
            var longFolder=Path.Combine(st.Root,"too-long-voice");Directory.CreateDirectory(longFolder);
            Reject(()=>MusicClips.PrepareAudio(st,e,tooLong,longFolder,CancellationToken.None,st.Settings,services,LongSpeech).GetAwaiter().GetResult());
            e.MusicClip.Text="";Reject(()=>MusicClips.Validate(e));
            e.MusicClip.SubtitlesEnabled=false;e.MusicClip.NarrationEnabled=false;MusicClips.Validate(e);
        });

        Test("MUSIC-CLIP-CREDITS", "Parça bilgileri isteğe bağlıdır; altyazı ve ASS talimatı eklenmez", () => {

            var e = new Episode { MusicClip = new MusicClipOptions { TrackTitle = "Parça {test}", Artist = "Yorumcu", ShowCredits = true } }; var script = new VideoScript { TextLayout = new TextLayout { SubtitleEnabled = true } };

            string ass = MusicClips.Ass(e, script, 1920, 1080, 90); Check(ass.Contains("Parça (test)") && ass.Contains("0:01:30.00") && !ass.Split('\n').Any(x => x.StartsWith("Dialogue:") && x.Contains(",Subtitle,")), "Kredi süresi / kaçış / şarkı altyazısı yanlış");

            e.MusicClip.ShowCredits = false; Check(!MusicClips.Ass(e, script, 1920, 1080, 90).Contains("Dialogue:"), "Kredi kapatma çalışmadı");

        });

        Test("QUEUE-SETTINGS", "Video ayarları ayrı saklanır; genel izinler ve bağlantılar korunur", () => {

            var st = New("queue-settings"); st.Settings.VoiceProvider = "Google Cloud TTS"; st.Settings.GoogleTtsVoice = "tr-TR-Wavenet-A"; st.Settings.Repo = "owner/current";

            var first = new Episode { QueueOrder = 1, IncludeLong = false, ShortCount = 1, QueueSettings = VideoQueue.Snapshot(st.Settings), QueueMusic = new MusicTrack { Enabled = false } };

            st.Settings.VoiceProvider = "Windows / ücretsiz"; st.Settings.Repo = "owner/new"; st.Settings.FreeOnly = true;

            var second = new Episode { QueueOrder = 2, IncludeLong = true, ShortCount = 0, QueueSettings = VideoQueue.Snapshot(st.Settings) };

            st.Episodes.AddRange([second, first]); st.Save(); var restored = new Store(st.Root);

            var saved = VideoQueue.Items(restored).ToList(); var effective = VideoQueue.Effective(restored.Settings, saved[0].QueueSettings);

            Check(saved[0].Id == first.Id && saved[1].Id == second.Id && effective.VoiceProvider == "Google Cloud TTS" && effective.Repo == "owner/new" && effective.FreeOnly && saved[1].QueueSettings!.VoiceProvider == "Windows / ücretsiz", "Sıra / bağlantılar / ayarlar karıştı");

            effective.GoogleTtsVoice = "changed"; Check(saved[0].QueueSettings!.GoogleTtsVoice == "tr-TR-Wavenet-A", "Snapshot değişti");

        });

        Test("QUEUE-VALIDATION", "Her satır tek video; eksik müzik ve geçmiş yayın kabul edilmez", () => {

            var st = New("queue-validation"); var e = new Episode { Topic = "Planlı konu", IncludeLong = false, ShortCount = 1, PublishAt = DateTimeOffset.UtcNow.AddDays(1), QueueSettings = VideoQueue.Snapshot(st.Settings) };

            VideoQueue.Validate(e, st.Settings); e.ShortCount = 2; Reject(() => VideoQueue.Validate(e, st.Settings)); e.ShortCount = 1;

            e.PublishAt = DateTimeOffset.UtcNow.AddMinutes(-1); Reject(() => VideoQueue.Validate(e, st.Settings)); e.PublishAt = DateTimeOffset.UtcNow.AddDays(1);

            e.Music = true; Reject(() => VideoQueue.Validate(e, st.Settings)); e.Music = false; e.QueueSettings!.VideoLogoEnabled = true; e.QueueSettings.VideoLogo = "missing.png"; Reject(() => VideoQueue.Validate(e, st.Settings));

        });

        Test("QUEUE-ASSETS", "Seçilen dosyaların bağımsız kopyası ve tasarım parmak izi korunur", () => {

            var st = New("queue-assets"); var music = Path.Combine(st.Root, "music.mp3"); File.WriteAllText(music, "original");

            var e = Episode(st); e.QueueSettings = VideoQueue.Snapshot(st.Settings); e.QueueMusic = new MusicTrack { Enabled = true, File = music, RightsConfirmed = true, Source = "Arşiv", License = "İzin var", Volume = .25 }; e.QueueLayout = new TextLayout { Font = "Arial", SubtitleColor = "FF8800" };

            VideoQueue.CopyAssets(st, e); File.WriteAllText(music, "changed"); Check(File.ReadAllText(e.QueueMusic.File) == "original", "Kaynak değişikliği sıraya yansıdı");

            VideoQueue.PrepareDesign(e, st.Settings); Check(e.Plan!.Long!.MusicTrack!.Volume == .25 && e.Plan.Long.TextLayout!.Font == "Arial", "Kayıtlı tasarım uygulanmadı");

            var hash = Reliability.MediaPlanHash(e); e.QueueSettings.VoiceSpeed = 1.2; Check(hash != Reliability.MediaPlanHash(e), "Ses ayarı çıktı kontrolüne yansımadı");

            e.Plan.Long.MusicTrack.Volume = .5; Check(e.QueueMusic.Volume == .25, "Plan müziği sonraki ayara karıştı");

        });

        Test("MUSIC-AUDIBLE", "Konuşma sırasında fon müziği kaybolmaz", () => {

            var st = New("music-audible"); var output = Path.Combine(st.Root, "mixed.pcm");

            var track = new MusicTrack { Enabled = true, Volume = .12, Duck = true };

            Media.Exec(ffmpeg, ["-y", "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=44100:duration=3", "-f", "lavfi", "-i", "sine=frequency=880:sample_rate=44100:duration=3", "-filter_complex", EditorAudio.Mix(track, 3, "0", "1"), "-map", "[a]", "-ar", "44100", "-ac", "1", "-f", "s16le", output], st.Root, CancellationToken.None).GetAwaiter().GetResult();

            var bytes = File.ReadAllBytes(output); double real = 0, imaginary = 0; int count = 44100;

            for (int i = 0; i < count; i++) { double sample = BitConverter.ToInt16(bytes, (i + 44100) * 2) / 32768.0; double angle = 2 * Math.PI * 880 * i / 44100; real += sample * Math.Cos(angle); imaginary += sample * Math.Sin(angle); }

            double amplitude = 2 * Math.Sqrt(real * real + imaginary * imaginary) / count;

            Check(amplitude > .005, "Fon konuşma sırasında aşırı kısıldı: " + amplitude);

        });

        Test("DURATION-STANDARD", "Shorts ve normal video ortak süre bütçesini kullanır", () => {

            var minute = DurationStandard.Budget(60); Check(minute.Minimum == 120 && minute.Target == 130 && minute.Maximum == 140, "Bir dakika standardı yanlış");

            var longVideo = DurationStandard.Budget(600); Check(longVideo.Minimum == 1200 && longVideo.Target == 1300 && longVideo.Maximum == 1400 && DurationStandard.SceneCount(600) == 20, "Uzun video bütçesi yanlış");

        });

        Test("LONG-SECTIONS", "Uzun anlatım bağımsız bölüm bütçeleriyle yazılır", () => {

            var st = New("long-sections"); st.Settings.LocalAiFirst = true; st.Settings.CloudAiEnabled = false;

            var e = Episode(st); e.LongMinutes = 2; int calls = 0;

            using var client = new HttpClient(new Handler(req => {

                if (req.RequestUri!.AbsolutePath.EndsWith("/show")) return Task.FromResult(Ok("{}")); calls++;

                return Task.FromResult(Ok(JsonSerializer.Serialize(new { message = new { content = JsonSerializer.Serialize(new { Narration = string.Join(" ", Enumerable.Repeat("bölüm" + calls, 65)) }) }, done = true })));

            }));

            new Services(st, _ => {}, client).EnsureScriptLength(e, CancellationToken.None).GetAwaiter().GetResult();

            Check(calls == 4 && ScriptLength.Words(e.Plan!.Long!) == 260 && ScriptLength.Problem(e, e.Plan!) == null, "Bölüm bütçeleri uygulanmadı");

        });

        foreach (var initialWords in new[] { 48, 56 }) Test("LONG-SECTION-REPAIR-" + initialWords, "Kısa bölüm toplam süre bütçesiyle dengelenir", () => {

            var st = New("long-section-repair-" + initialWords); st.Settings.LocalAiFirst = true; st.Settings.CloudAiEnabled = false;

            var e = Episode(st); e.LongMinutes = 2; int calls = 0;

            using var client = new HttpClient(new Handler(req => {

                if (req.RequestUri!.AbsolutePath.EndsWith("/show")) return Task.FromResult(Ok("{}")); calls++;

                object reply = new { Narration = string.Join(" ", Enumerable.Repeat("bölüm" + calls, calls == 1 ? initialWords : 65)) };

                return Task.FromResult(Ok(JsonSerializer.Serialize(new { message = new { content = JsonSerializer.Serialize(reply) }, done = true })));

            }));

            new Services(st, _ => {}, client).EnsureScriptLength(e, CancellationToken.None).GetAwaiter().GetResult();

            Check(calls == 4 && ScriptLength.Words(e.Plan!.Long!) == initialWords + 195 && ScriptLength.Problem(e, e.Plan!) == null, "Toplam süre bütçesi uygulanmadı");

            Check(e.Plan!.Long!.Scenes[0].Narration.StartsWith(string.Join(" ", Enumerable.Repeat("bölüm1", initialWords))), "Mevcut bölüm yeniden yazıldı");

        });

        Test("LONG-SECTION-RESUME", "Kesilen uzun anlatım tamamlanan bölümleri yeniden yazdırmaz", () => {

            var st = New("long-section-resume"); st.Settings.LocalAiFirst = true; st.Settings.CloudAiEnabled = false;

            var e = Episode(st); e.LongMinutes = 2; int calls = 0; using var stop = new CancellationTokenSource();

            using var client = new HttpClient(new Handler(req => {

                if (req.RequestUri!.AbsolutePath.EndsWith("/show")) return Task.FromResult(Ok("{}")); calls++;

                if (calls == 2) { stop.Cancel(); throw new OperationCanceledException(stop.Token); }

                return Task.FromResult(Ok(JsonSerializer.Serialize(new { message = new { content = JsonSerializer.Serialize(new { Narration = string.Join(" ", Enumerable.Repeat("kayıtlı", 65)) }) }, done = true })));

            }));

            try { new Services(st, _ => {}, client).EnsureScriptLength(e, stop.Token).GetAwaiter().GetResult(); } catch (OperationCanceledException) { }

            Check(ScriptLength.Words(e.Plan!.Long!) < 240, "Yarım senaryo uygulanmış"); int resumed = 0;

            using var next = new HttpClient(new Handler(req => {

                if (req.RequestUri!.AbsolutePath.EndsWith("/show")) return Task.FromResult(Ok("{}")); resumed++;

                return Task.FromResult(Ok(JsonSerializer.Serialize(new { message = new { content = JsonSerializer.Serialize(new { Narration = string.Join(" ", Enumerable.Repeat("devam" + resumed, 65)) }) }, done = true })));

            }));

            new Services(st, _ => {}, next).EnsureScriptLength(e, CancellationToken.None).GetAwaiter().GetResult();

            Check(resumed == 3 && ScriptLength.Words(e.Plan!.Long!) == 260 && e.Plan.Long!.Scenes[0].Narration.StartsWith("kayıtlı"), "Tamamlanan bölüm yeniden üretildi");

        });

        Test("NARRATION-NOVELTY", "Tam metinle dönen devamda tekrar çıkarılır; ortak konu kelimeleri korunur", () => {

            string existing = "Sabır insanın kararlarına yön verir. Küçük adımlar önemlidir.";

            Check(ScriptLength.NovelContinuation(existing, [existing]) == "", "Tam tekrar kabul edildi");

            Check(ScriptLength.NovelContinuation(existing + " Günlük alışkanlıklarımızı dikkatle gözden geçirmek yeni bir başlangıç sağlar.", [existing]) == "Günlük alışkanlıklarımızı dikkatle gözden geçirmek yeni bir başlangıç sağlar.", "Tekrarlı giriş çıkarılmadı");

            Check(ScriptLength.NovelContinuation("Sabır düşünerek hareket etmeye yardımcı olur.", [existing]) != "", "Ortak konu kelimesi yeni açıklamayı reddetti");

            Check(ScriptLength.NovelContinuation("  SABIR insanın kararlarına yön verir!", [existing]) == "", "Harf/noktalama tekrarı kaçtı");

        });

        foreach (string response in new[] { "empty", "repeat", "with-prefix" }) Test("NARRATION-CONTINUATION-" + response, "Boş veya tekrarlı devam sağlayıcıyı engellemeden kurtarılır", () => {

            var st = New("continuation-" + response); st.Settings.LocalAiFirst = true; st.Settings.CloudAiEnabled = false;

            var e = Episode(st); e.IncludeLong = false; e.ShortCount = 1; e.ShortSeconds = 60; e.Plan!.Shorts = [e.Plan.Long!]; e.Plan.Long = null;

            string initial = string.Join(" ", Enumerable.Repeat("başlangıç", 82)) + ".";

            string extra = string.Join(" ", Enumerable.Repeat("açıklama", 48)) + "."; int calls = 0;

            using var client = new HttpClient(new Handler(req => {

                if (req.RequestUri!.AbsolutePath.EndsWith("/show")) return Task.FromResult(Ok("{}")); calls++;

                object reply = calls == 1 ? new { Narrations = new[] { initial } } : calls == 2 ? (object)new { Continuation = response == "empty" ? "" : response == "repeat" ? initial : initial + " " + extra } : new { Narrations = new[] { initial + " " + extra } };

                return Task.FromResult(Ok(JsonSerializer.Serialize(new { message = new { content = JsonSerializer.Serialize(reply) }, done = true })));

            }));

            new Services(st, _ => {}, client).EnsureScriptLength(e, CancellationToken.None).GetAwaiter().GetResult();

            Check(calls == (response == "with-prefix" ? 2 : 3) && ScriptLength.Words(e.Plan.Shorts[0]) == 130 && !st.ProviderBlocked.ContainsKey("Ollama"), "Düzenlenebilir içerik hatası sağlayıcı arızası sayıldı");

        });

        Test("NARRATION-TINY-GAP", "Az kelimelik eksik için kopuk devam yerine tam anlatım dengelenir", () => {

            var st = New("tiny-gap"); st.Settings.LocalAiFirst = true; st.Settings.CloudAiEnabled = false;

            var e = Episode(st); e.IncludeLong = false; e.ShortCount = 1; e.ShortSeconds = 15; e.Plan!.Shorts = [e.Plan.Long!]; e.Plan.Long = null; int calls = 0;

            using var client = new HttpClient(new Handler(async req => {

                if (req.RequestUri!.AbsolutePath.EndsWith("/show")) return Ok("{}"); calls++;

                string body = await req.Content!.ReadAsStringAsync(); Check(!body.Contains("Continuation"), "Kopuk mikro devam istendi");

                return Ok(JsonSerializer.Serialize(new { message = new { content = JsonSerializer.Serialize(new { Narrations = new[] { string.Join(" ", Enumerable.Repeat("metin", calls == 1 ? 29 : 33)) } }) }, done = true }));

            }));

            new Services(st, _ => {}, client).EnsureScriptLength(e, CancellationToken.None).GetAwaiter().GetResult(); Check(calls == 2 && ScriptLength.Words(e.Plan.Shorts[0]) == 33, "Küçük eksik düzeltilemedi");

        });

        Test("NARRATION-BOUNDED-RESUME", "İyileşmeyen modelde çağrılar sınırlı; son taslak ve eski senaryo korunur", () => {

            var st = New("bounded-resume"); st.Settings.LocalAiFirst = true; st.Settings.CloudAiEnabled = false;

            var e = Episode(st); e.IncludeLong = false; e.ShortCount = 1; e.ShortSeconds = 60; e.Plan!.Shorts = [e.Plan.Long!]; e.Plan.Long = null; string original = e.Plan.Shorts[0].Scenes[0].Narration; int calls = 0;

            using var client = new HttpClient(new Handler(async req => {

                if (req.RequestUri!.AbsolutePath.EndsWith("/show")) return Ok("{}"); calls++; string body = await req.Content!.ReadAsStringAsync();

                object reply = body.Contains("Continuation") ? new { Continuation = "" } : (object)new { Narrations = new[] { string.Join(" ", Enumerable.Repeat("taslak", 82)) } };

                return Ok(JsonSerializer.Serialize(new { message = new { content = JsonSerializer.Serialize(reply) }, done = true }));

            }));

            Reject(() => new Services(st, _ => {}, client).EnsureScriptLength(e, CancellationToken.None).GetAwaiter().GetResult());

            Check(calls == 5 && e.Plan.Shorts[0].Scenes[0].Narration == original && !st.ProviderBlocked.ContainsKey("Ollama"), "Sınırsız tekrar veya yarım senaryo uygulaması"); int resumed = 0;

            using var next = new HttpClient(new Handler(req => {

                if (req.RequestUri!.AbsolutePath.EndsWith("/show")) return Task.FromResult(Ok("{}")); resumed++;

                return Task.FromResult(Ok(JsonSerializer.Serialize(new { message = new { content = JsonSerializer.Serialize(new { Continuation = string.Join(" ", Enumerable.Repeat("yeni", 48)) }) }, done = true })));

            }));

            new Services(st, _ => {}, next).EnsureScriptLength(e, CancellationToken.None).GetAwaiter().GetResult(); Check(resumed == 1 && ScriptLength.Words(e.Plan.Shorts[0]) == 130, "Kayıtlı taslak yeniden yazdırıldı");

        });

        Test("NARRATION-CHECKPOINT-INVALIDATE", "Konu isteği değişince eski taslak kullanılmaz", () => {

            var st = New("checkpoint-invalidate"); st.Settings.LocalAiFirst = true; st.Settings.CloudAiEnabled = false;

            var e = Episode(st); e.IncludeLong = false; e.ShortCount = 1; e.ShortSeconds = 60; e.Plan!.Shorts = [e.Plan.Long!]; e.Plan.Long = null; using var stop = new CancellationTokenSource(); int calls = 0;

            using var first = new HttpClient(new Handler(req => {

                if (req.RequestUri!.AbsolutePath.EndsWith("/show")) return Task.FromResult(Ok("{}")); calls++;

                if (calls == 2) { stop.Cancel(); throw new OperationCanceledException(stop.Token); }

                return Task.FromResult(Ok(JsonSerializer.Serialize(new { message = new { content = JsonSerializer.Serialize(new { Narrations = new[] { string.Join(" ", Enumerable.Repeat("eski", 82)) } }) }, done = true })));

            }));

            try { new Services(st, _ => {}, first).EnsureScriptLength(e, stop.Token).GetAwaiter().GetResult(); } catch (OperationCanceledException) { }

            e.Brief = "Farklı bir açıklama istiyorum";

            using var next = new HttpClient(new Handler(async req => {

                if (req.RequestUri!.AbsolutePath.EndsWith("/show")) return Ok("{}"); string body = await req.Content!.ReadAsStringAsync(); Check(!body.Contains("Continuation"), "Eski taslak kullanıldı");

                return Ok(JsonSerializer.Serialize(new { message = new { content = JsonSerializer.Serialize(new { Narrations = new[] { string.Join(" ", Enumerable.Repeat("yeni", 130)) } }) }, done = true }));

            }));

            new Services(st, _ => {}, next).EnsureScriptLength(e, CancellationToken.None).GetAwaiter().GetResult(); Check(ScriptLength.Words(e.Plan.Shorts[0]) == 130, "Yeni istek uygulanmadı");

        });

        foreach (int seconds in new[] { 15, 30, 60, 180 }) Test("NARRATION-SHORT-MATRIX-" + seconds, "Shorts süre bütçesi " + seconds + " saniye", () => {

            var st = New("short-matrix-" + seconds); st.Settings.LocalAiFirst = true; st.Settings.CloudAiEnabled = false;

            var e = Episode(st); e.IncludeLong = false; e.ShortCount = 1; e.ShortSeconds = seconds; e.Plan!.Shorts = [e.Plan.Long!]; e.Plan.Long = null;

            using var client = new HttpClient(new Handler(req => Task.FromResult(Ok(req.RequestUri!.AbsolutePath.EndsWith("/show") ? "{}" : JsonSerializer.Serialize(new { message = new { content = JsonSerializer.Serialize(new { Narrations = new[] { string.Join(" ", Enumerable.Repeat("anlatım", DurationStandard.Budget(seconds).Target)) } }) }, done = true })))));

            new Services(st, _ => {}, client).EnsureScriptLength(e, CancellationToken.None).GetAwaiter().GetResult(); Check(ScriptLength.Problem(e, e.Plan!) == null, "Shorts bütçesi karşılanmadı");

        });

        foreach (int minutes in new[] { 1, 5, 10, 30 }) Test("NARRATION-LONG-MATRIX-" + minutes, "Normal video süre bütçesi " + minutes + " dakika", () => {

            var st = New("long-matrix-" + minutes); st.Settings.LocalAiFirst = true; st.Settings.CloudAiEnabled = false;

            var e = Episode(st); e.LongMinutes = minutes; int count = DurationStandard.SceneCount(minutes * 60), calls = 0;

            e.Plan!.Long!.Scenes = Enumerable.Range(0, count).Select(i => new Scene { Narration = "Alt konu " + i, StockFile = video, RightsConfirmed = true }).ToList();

            using var client = new HttpClient(new Handler(req => {

                if (req.RequestUri!.AbsolutePath.EndsWith("/show")) return Task.FromResult(Ok("{}")); calls++;

                return Task.FromResult(Ok(JsonSerializer.Serialize(new { message = new { content = JsonSerializer.Serialize(new { Narration = string.Join(" ", Enumerable.Repeat("altkonu" + calls, 65)) }) }, done = true })));

            }));

            new Services(st, _ => {}, client).EnsureScriptLength(e, CancellationToken.None).GetAwaiter().GetResult();

            Check(calls == count && ScriptLength.Problem(e, e.Plan!) == null && e.Plan.Long.Scenes.All(x => x.StockFile == video), "Normal video bütçesi / sahne korunması yanlış");

        });

        Test("NARRATION-LONG-BOUNDED-EDIT", "Uzun video düzeltmesinde yanıt boyutu ve sahne grubu sınırlıdır", () => {

            var st = New("long-bounded-edit"); st.Settings.LocalAiFirst = true; st.Settings.CloudAiEnabled = false;

            var e = Episode(st); e.LongMinutes = 10; int count = 20, sections = 0, continuations = 0, rewrites = 0;

            e.Plan!.Long!.Scenes = Enumerable.Range(0, count).Select(i => new Scene { Narration = "Alt konu " + i, StockFile = video, RightsConfirmed = true }).ToList();

            using var client = new HttpClient(new Handler(async req => {

                if (req.RequestUri!.AbsolutePath.EndsWith("/show")) return Ok("{}"); var body = await req.Content!.ReadAsStringAsync(); using var payload = JsonDocument.Parse(body);

                var format = payload.RootElement.GetProperty("format"); object reply;

                if (format.GetProperty("properties").TryGetProperty("Narration", out _)) { sections++; reply = new { Narration = string.Join(" ", Enumerable.Repeat("bölüm" + sections, 56)) }; }

                else if (format.GetProperty("properties").TryGetProperty("Continuation", out _)) { continuations++; reply = new { Continuation = "" }; }

                else { rewrites++; int window = format.GetProperty("properties").GetProperty("Narrations").GetProperty("minItems").GetInt32(); Check(window <= 8 && payload.RootElement.GetProperty("options").GetProperty("num_predict").GetInt32() <= 8192, "Uzun senaryo tek yanıt olarak istendi"); reply = new { Narrations = Enumerable.Range(0, window).Select(i => string.Join(" ", Enumerable.Repeat("düzenleme" + i, 78))).ToArray() }; }

                return Ok(JsonSerializer.Serialize(new { message = new { content = JsonSerializer.Serialize(reply) }, done = true }));

            }));

            new Services(st, _ => {}, client).EnsureScriptLength(e, CancellationToken.None).GetAwaiter().GetResult(); Check(sections == 20 && continuations == 1 && rewrites == 1 && ScriptLength.Problem(e, e.Plan!) == null, "Uzun düzeltme başarıyla sınırlandırılmadı");

        });

        if (Program.Arg(args, "--narration-fixture") is string narrationFixture) Test("NARRATION-REAL-EXISTING", "Gerçek 538 kelimelik 5 dakika taslağı yeniden yazdırılmadan tamamlanır", () => {

            var st = New("real-existing"); st.Settings.LocalAiFirst = true; st.Settings.CloudAiEnabled = false;

            var texts = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(narrationFixture))!;

            var e = Episode(st); e.Topic = "Ameller Niyetlere Göredir"; e.LongMinutes = 5; e.Plan!.Long!.Scenes = texts.Select(x => new Scene { Narration = x, StockFile = video, RightsConfirmed = true }).ToList(); int calls = 0;

            string extra = "Niyetimizi gözden geçirmek, günlük kararlarımızı daha bilinçli vermemize yardımcı olabilir. Bunun için büyük bir olay yaşanmasını beklemek gerekmez. Bir işe başlamadan önce, bu davranışın kime yarar sağlayacağını ve hangi amacı taşıdığını kendimize sorabiliriz. Örneğin bir yakınımıza yardım ederken, karşılığında övgü bekleyip beklemediğimizi düşünmek faydalıdır. Cevabımızdan hoşlanmadığımızda kendimizi suçlamak yerine, davranışımızı daha samimi bir amaca yöneltmeye çalışabiliriz. Bu değerlendirme tek seferlik bir karar değildir. Gün içinde değişen koşullar ve duygular, niyetimizi yeniden hatırlamamızı gerektirebilir. Böylece içimizdeki amaç ile dışarıya yansıyan davranış arasında daha tutarlı bir ilişki kurmak için küçük ve somut adımlar atabiliriz.";

            using var client = new HttpClient(new Handler(async req => {

                if (req.RequestUri!.AbsolutePath.EndsWith("/show")) return Ok("{}"); calls++; string body = await req.Content!.ReadAsStringAsync(); Check(body.Contains("Continuation"), "Mevcut sahneler yeniden yazdırıldı");

                return Ok(JsonSerializer.Serialize(new { message = new { content = JsonSerializer.Serialize(new { Continuation = extra }) }, done = true }));

            }));

            new Services(st, _ => {}, client).EnsureScriptLength(e, CancellationToken.None).GetAwaiter().GetResult();

            Check(calls == 1 && ScriptLength.Problem(e, e.Plan!) == null && e.Plan.Long.Scenes.Select((x, i) => x.Narration.StartsWith(texts[i], StringComparison.Ordinal)).All(x => x), "Mevcut anlatım korunmadı veya süre tamamlanmadı");

        });

        Test("SCRIPT-LENGTH", "Kısa ve uzun anlatımlar üretim öncesi tespit edilir", () => {

            var e = new Episode { IncludeLong = false, ShortCount = 1, ShortSeconds = 30, Plan = new ContentPlan { Shorts = [new VideoScript { Scenes = [new Scene { Narration = "Dua ederken sabırlı olmak bize huzur ve umut verir." }] }] } };

            Check(ScriptLength.Problem(e, e.Plan) != null, "9 kelimelik Shorts kabul edildi");

            e.Plan.Shorts[0].Scenes[0].Narration = string.Join(" ", Enumerable.Repeat("kelime", 60)); Check(ScriptLength.Problem(e, e.Plan) == null, "Hedef anlatım reddedildi");

            e.Plan.Shorts[0].Scenes[0].Narration = string.Join(" ", Enumerable.Repeat("kelime", 90)); Check(ScriptLength.Problem(e, e.Plan) != null, "Uzun anlatım kabul edildi");

        });

        Test("SCRIPT-REPAIR", "Otomatik süre düzeltmesi stok ve tasarımı korur, rapor kaydeder", () => {

            var st = New("length-repair"); st.Settings.LocalAiFirst = true; st.Settings.CloudAiEnabled = false;

            var e = Episode(st); e.IncludeLong = false; e.ShortCount = 1; e.ShortSeconds = 30;

            e.Plan!.Shorts = [e.Plan.Long!]; e.Plan.Long = null; var scene = e.Plan.Shorts[0].Scenes[0]; scene.SubtitleText = "Eski metin";

            var candidate = JsonSerializer.Deserialize<ContentPlan>(JsonSerializer.Serialize(e.Plan, Json.Options), Json.Options)!;

            candidate.Shorts[0].Scenes[0].Narration = string.Join(" ", Enumerable.Repeat("sabır", 60));

            int lengthCalls = 0; bool schemaUsed = false;

            using var client = new HttpClient(new Handler(async req => {

                if (req.RequestUri!.AbsolutePath.EndsWith("/show")) return Ok("{}");

                var body = await req.Content!.ReadAsStringAsync(); schemaUsed |= body.Contains("Narrations") && body.Contains("minItems"); lengthCalls++;

                object reply = lengthCalls == 1 ? new { Narrations = new[] { string.Join(" ", Enumerable.Repeat("sabır", 38)) } } : lengthCalls == 2 ? (object)new { Continuation = string.Join(" ", Enumerable.Repeat("umut", 40)) } : new { Narrations = new[] { string.Join(" ", Enumerable.Repeat("anlatım", 65)) } };

                return Ok(JsonSerializer.Serialize(new { message = new { content = JsonSerializer.Serialize(reply) }, done = true }));

            }));

            new Services(st, _ => {}, client).EnsureScriptLength(e, CancellationToken.None).GetAwaiter().GetResult();

            Check(lengthCalls == 3 && schemaUsed, "Kısa yanıt yeniden düzeltilmedi veya özel şema kullanılmadı");

            Check(ScriptLength.Words(e.Plan.Shorts[0]) == 65 && scene.StockFile == video && scene.SubtitleText == "" && e.TextDesignPending, "Süre düzeltmesi uygulanmadı veya stok kayboldu");

            Check(File.Exists(Path.Combine(st.Folder(e), "script-length-before.json")) && File.Exists(Path.Combine(st.Folder(e), "script-length-after.json")), "Raporlar eksik");

        });

        Test("YOUTUBE-CONFIRMED-PROGRESS", "Devam eden oturum ve parça ilerlemesi yalnızca sunucunun doğruladığı baytlardan hesaplanır", () => {

            var st = New("youtube-confirmed-progress"); var e = Episode(st); var asset = e.Videos[0]; long total = 17 * 1024 * 1024;

            using (var file = new FileStream(asset.File, FileMode.Create)) file.SetLength(total);

            asset.UploadSession = "https://www.googleapis.com/upload-session"; var reports = new List<long>(); int chunks = 0;

            using var client = new HttpClient(new Handler(req => {

                if (req.RequestUri!.Host == "oauth2.googleapis.com") return Task.FromResult(Ok("{\"access_token\":\"fixture\"}"));

                if (req.Content!.Headers.ContentRange!.From == null) { var resumed = new HttpResponseMessage((HttpStatusCode)308); resumed.Headers.TryAddWithoutValidation("Range", "bytes=0-8388607"); return Task.FromResult(resumed); }

                chunks++; if (chunks == 1) { var next = new HttpResponseMessage((HttpStatusCode)308); next.Headers.TryAddWithoutValidation("Range", "bytes=0-16777215"); return Task.FromResult(next); }

                return Task.FromResult(Ok("{\"id\":\"fixture-id\"}"));

            }));

            var old = Environment.GetEnvironmentVariable("YOUTUBE_REFRESH_TOKEN");

            try { Environment.SetEnvironmentVariable("YOUTUBE_REFRESH_TOKEN", "fixture"); new YouTube(st, client).Upload(e, asset, e.Plan!.Long!, CancellationToken.None, (sent, size) => { Check(size == total, "Toplam bayt değişti"); reports.Add(sent); }).GetAwaiter().GetResult(); }

            finally { Environment.SetEnvironmentVariable("YOUTUBE_REFRESH_TOKEN", old); }

            Check(reports.SequenceEqual(new long[] { 0, 8388608, 16777216, total }) && asset.YouTubeId == "fixture-id" && chunks == 2, "Sunucu onayları yanlış raporlandı");

        });

        Test("TRANSFER-PROGRESS", "Aktarılan byte sayısı doğru raporlanır", () => {

            using var input = new MemoryStream(new byte[200000]); using var output = new MemoryStream(); long sent = 0, total = 0; int reports = 0;

            using var content = new TransferContent(input, (s, t) => { sent = s; total = t; reports++; }); content.CopyToAsync(output).GetAwaiter().GetResult();

            Check(sent == 200000 && total == sent && output.Length == sent && reports > 1, "Gerçek aktarım ilerlemesi raporlanmadı");

        });

        Test("GEMINI-RETRY", "503 sonrası aynı istek iki kez beklenerek yeniden denenir", () => {

            var st = New("gemini-retry"); st.Settings.ProviderOrder = "Gemini"; var saved = Environment.GetEnvironmentVariable("GEMINI_API_KEY");

            try {

                Environment.SetEnvironmentVariable("GEMINI_API_KEY", "fixture"); int calls = 0; var waits = new List<double>();

                using var client = new HttpClient(new Handler(req => Task.FromResult(++calls < 3 ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("{}") } : Ok("{\"choices\":[{\"message\":{\"content\":\"{}\"}}]}"))));

                new Services(st, _ => {}, client, (time, ct) => { waits.Add(time.TotalSeconds); return Task.CompletedTask; }).Generate("JSON", "test", CancellationToken.None, onlyProvider: "Gemini").GetAwaiter().GetResult();

                Check(calls == 3 && waits.SequenceEqual(new double[] { 5, 10 }), "503 tekrarları yanlış");

            } finally { Environment.SetEnvironmentVariable("GEMINI_API_KEY", saved); }

        });

        Test("GEMINI-DISCOVERY", "Model listesi filtrelenir; yoğun model yerine listedeki metin modeli denenir", () => {

            var st = New("gemini-discovery"); st.Settings.ProviderOrder = "Gemini"; st.Settings.GeminiModel = "gemini-busy-flash";

            var saved = Environment.GetEnvironmentVariable("GEMINI_API_KEY");

            try {

                Environment.SetEnvironmentVariable("GEMINI_API_KEY", "fixture"); int busy = 0, lists = 0, alternatives = 0;

                using var client = new HttpClient(new Handler(async req => {

                    if (req.Method == HttpMethod.Get) { lists++; Check(req.Headers.Contains("x-goog-api-key") && !req.RequestUri!.Query.Contains("fixture"), "Anahtar URL içinde"); return Ok("{\"models\":[{\"name\":\"models/gemini-image-flash\",\"supportedGenerationMethods\":[\"generateContent\"]},{\"name\":\"models/gemini-new-flash\",\"supportedGenerationMethods\":[\"generateContent\"]}]}"); }

                    using var body = JsonDocument.Parse(await req.Content!.ReadAsStringAsync()); var model = body.RootElement.GetProperty("model").GetString();

                    if (model == "gemini-busy-flash") { busy++; return new(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("{}") }; }

                    Check(model == "gemini-new-flash", "Listede olmayan model denendi"); alternatives++; return Ok("{\"choices\":[{\"message\":{\"content\":\"{}\"}}]}");

                }));

                new Services(st, _ => {}, client, (_, _) => Task.CompletedTask).Generate("JSON", "test", CancellationToken.None, onlyProvider: "Gemini").GetAwaiter().GetResult();

                Check(busy == 3 && lists == 1 && alternatives == 1, "Model geçişi yanlış");

            } finally { Environment.SetEnvironmentVariable("GEMINI_API_KEY", saved); }

        });

        Test("GEMINI-PERMANENT", "Bakiye hatasında yoğunluk döngüsü ve model keşfi yapılmaz", () => {

            var st = New("gemini-permanent"); st.Settings.ProviderOrder = "Gemini"; var saved = Environment.GetEnvironmentVariable("GEMINI_API_KEY");

            try {

                Environment.SetEnvironmentVariable("GEMINI_API_KEY", "fixture"); int calls = 0, waits = 0;

                using var client = new HttpClient(new Handler(req => { calls++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.PaymentRequired) { Content = new StringContent("{}") }); }));

                Reject(() => new Services(st, _ => {}, client, (_, _) => { waits++; return Task.CompletedTask; }).Generate("JSON", "test", CancellationToken.None, onlyProvider: "Gemini").GetAwaiter().GetResult());

                Check(calls == 1 && waits == 0, "Bakiye hatası tekrarlandı");

            } finally { Environment.SetEnvironmentVariable("GEMINI_API_KEY", saved); }

        });

        Test("CLOUD-UPLOAD-01", "Hazır video AI olmadan gönderilir; OAuth Secrets kullanılır; ikinci iş engellenir", () => {

            var st = New("cloud-upload-send"); st.Settings.Repo = "fixture/repo"; st.Settings.CloudAiEnabled = false; st.Settings.GoogleClientId = "fixture-client";

            var e = Episode(st); e.Approved = true; e.ApprovalHash = Rules.HashPlan(e); e.ApprovedChannelId = "fixture-channel";

            int dispatches=0, mediaUploads=0; var secrets = new List<string>(); byte[]? package=null;

            var pair = Sodium.PublicKeyBox.GenerateKeyPair();

            using var client = new HttpClient(new Handler(async req => {

                var path=req.RequestUri!.AbsolutePath;

                if(req.RequestUri.Host=="uploads.github.com") { mediaUploads++; package=await req.Content!.ReadAsByteArrayAsync(); return Ok("{\"id\":66}"); }

                if(path.EndsWith("/actions/secrets/public-key")) return Ok(JsonSerializer.Serialize(new {key=Convert.ToBase64String(pair.PublicKey),key_id="fixture"}));

                if(path.Contains("/actions/secrets/")) { secrets.Add(path.Split('/').Last()); return Ok("{}"); }

                if(path.EndsWith("/dispatches")) {dispatches++;return new HttpResponseMessage(HttpStatusCode.NoContent);}

                if(path.EndsWith("/releases")) return Ok("{\"id\":55}");

                if(path.Contains("/contents/")) return new HttpResponseMessage(HttpStatusCode.NotFound);

                if(path.EndsWith("/runs")) return Ok("{\"workflow_runs\":[]}");

                return path=="/repos/fixture/repo" ? Ok("{\"private\":true}") : Ok("{}");

            }));

            var names=new[]{"GITHUB_TOKEN","GOOGLE_CLIENT_SECRET","YOUTUBE_REFRESH_TOKEN"};var old=names.ToDictionary(n=>n,Environment.GetEnvironmentVariable);

            try {

                foreach(var name in names) Environment.SetEnvironmentVariable(name,"fixture-key");

                var github=new GitHub(st,client);github.SendYouTubeUpload(e,false,CancellationToken.None).GetAwaiter().GetResult();

                Check(dispatches==1 && mediaUploads==1 && secrets.Order().SequenceEqual(new[]{"GOOGLE_CLIENT_ID","GOOGLE_CLIENT_SECRET","YOUTUBE_REFRESH_TOKEN"}.Order()),"İş veya Secrets yanlış");

                using var zip=new System.IO.Compression.ZipArchive(new MemoryStream(package!));using var reader=new StreamReader(zip.GetEntry("upload.json")!.Open());var bundle=reader.ReadToEnd();

                Check(!bundle.Contains("fixture-key") && !bundle.Contains("fixture-client") && zip.GetEntry("videos/0.mp4")!=null,"Anahtar pakete girdi veya video eksik");

                Reject(()=>github.SendYouTubeUpload(e,false,CancellationToken.None).GetAwaiter().GetResult()); Check(dispatches==1 && mediaUploads==1,"Çift iş gönderildi");

                Check(new Store(st.Root).Episodes[0].RemoteUploadAsset==66,"Uzak iş kaydı kalıcı değil");

            } finally {foreach(var name in names) Environment.SetEnvironmentVariable(name,old[name]);}

        });

        Test("CLOUD-UPLOAD-02", "Bulut işçisi videoyu yükler, kalıcı oturum yazar; aynı iş tekrarında kopya oluşmaz", () => {

            var source=New("cloud-worker-source");var e=Episode(source);e.Approved=true;e.ApprovalHash=Rules.HashPlan(e);e.ApprovedChannelId="fixture-channel";e.Videos[0].ThumbnailUploaded=true;e.Videos[0].File="video.mp4";

            var job=Guid.NewGuid().ToString("N");var bundleFile=Path.Combine(source.Root,"upload.json");Json.Write(bundleFile,new UploadBundle{JobId=job,ChannelId="fixture-channel",Episode=e});

            string? stateJson=null;int chunks=0;bool savedBeforeBytes=false;

            using var gh=new HttpClient(new Handler(async req=>{

                if(req.Method==HttpMethod.Get) return stateJson==null ? new HttpResponseMessage(HttpStatusCode.NotFound) : Ok(JsonSerializer.Serialize(new{sha="fixture-sha",content=Convert.ToBase64String(Encoding.UTF8.GetBytes(stateJson))}));

                using var doc=JsonDocument.Parse(await req.Content!.ReadAsStringAsync());stateJson=Encoding.UTF8.GetString(Convert.FromBase64String(doc.RootElement.GetProperty("content").GetString()!));return Ok("{}");

            }));

            using var yt=new HttpClient(new Handler(req=>{

                if(req.RequestUri!.Host=="oauth2.googleapis.com")return Task.FromResult(Ok("{\"access_token\":\"fixture\"}"));

                if(req.RequestUri.AbsolutePath.EndsWith("/channels"))return Task.FromResult(Ok("{\"items\":[{\"id\":\"fixture-channel\",\"snippet\":{\"title\":\"Test\"}}]}"));

                if(req.Method==HttpMethod.Post){var response=Ok("{}");response.Headers.Location=new Uri("https://www.googleapis.com/upload-session");return Task.FromResult(response);}

                chunks++;savedBeforeBytes=stateJson!.Contains("upload-session");return Task.FromResult(Ok("{\"id\":\"uploaded-video\"}"));

            }));

            var names=new[]{"GITHUB_TOKEN","GITHUB_REPOSITORY","ANKA_UPLOAD_JOB","ANKA_EPISODE_ID","GOOGLE_CLIENT_ID","GOOGLE_CLIENT_SECRET","YOUTUBE_REFRESH_TOKEN","ANKA_BRANCH"};var old=names.ToDictionary(n=>n,Environment.GetEnvironmentVariable);

            try {

                foreach(var n in names)Environment.SetEnvironmentVariable(n,"fixture");Environment.SetEnvironmentVariable("GITHUB_REPOSITORY","fixture/repo");Environment.SetEnvironmentVariable("ANKA_UPLOAD_JOB",job);Environment.SetEnvironmentVariable("ANKA_EPISODE_ID",e.Id);Environment.SetEnvironmentVariable("ANKA_BRANCH","main");

                var worker=New("cloud-worker");RemoteYouTubeWorker.Run(worker,["--upload-bundle",bundleFile],gh,yt).GetAwaiter().GetResult();

                var state=JsonSerializer.Deserialize<UploadCheckpoint>(stateJson!,Json.Options)!;Check(state.State=="completed" && state.Videos.Single().YouTubeId=="uploaded-video" && chunks==1 && savedBeforeBytes,"Yükleme veya kalıcı oturum yanlış");

                var second=New("cloud-worker-repeat");RemoteYouTubeWorker.Run(second,["--upload-bundle",bundleFile],gh,yt).GetAwaiter().GetResult();Check(chunks==1,"Tamamlanmış video yeniden yüklendi");

                source.Settings.Repo="fixture/repo";e.RemoteUploadJob=job;e.RemoteUploadRepo="fixture/repo";new GitHub(source,gh).SyncUpload(e,CancellationToken.None).GetAwaiter().GetResult();Check(e.Videos[0].YouTubeId=="uploaded-video" && e.RemoteUploadState=="completed","Program uzak sonucu alamadı");

                e.Videos[0].FileHash="changed";Reject(()=>new GitHub(source,gh).SyncUpload(e,CancellationToken.None).GetAwaiter().GetResult());

            } finally {foreach(var n in names)Environment.SetEnvironmentVariable(n,old[n]);}

        });

        Test("CLOUD-UPLOAD-03", "Bulut yolu ve onaysız video reddedilir", () => {

            Reject(()=>RemoteYouTubeWorker.Inside(root,"../escape.mp4"));Reject(()=>RemoteYouTubeWorker.Inside(root,"file:stream"));

            var st=New("cloud-reject");var e=Episode(st);Reject(()=>GitHub.ValidateUpload(e,false,DateTimeOffset.UtcNow));

            e.Approved=true;e.ApprovalHash=Rules.HashPlan(e);e.ApprovedChannelId="fixture-channel";Reject(()=>GitHub.ValidateUpload(e,true,DateTimeOffset.UtcNow));

        });

        Test("CLOUD-UPLOAD-04", "Durdurulan bulut işi devam edilebilir hata olarak gösterilir", () => {

            var st=New("cloud-cancel");st.Settings.Repo="fixture/repo";var e=Episode(st);e.ApprovedChannelId="fixture-channel";e.RemoteUploadJob=Guid.NewGuid().ToString("N");e.RemoteUploadRepo=st.Settings.Repo;

            var state=new UploadCheckpoint {JobId=e.RemoteUploadJob,EpisodeId=e.Id,ChannelId=e.ApprovedChannelId,State="uploading",Videos=e.Videos};

            using var client=new HttpClient(new Handler(req=>Task.FromResult(req.RequestUri!.AbsolutePath.EndsWith("/runs") ? Ok(JsonSerializer.Serialize(new{workflow_runs=new[]{new{display_title="Anka YouTube "+e.RemoteUploadJob,status="completed",conclusion="cancelled"}}})) : Ok(JsonSerializer.Serialize(new{content=Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(state,Json.Options))})) )));

            var old=Environment.GetEnvironmentVariable("GITHUB_TOKEN");try{Environment.SetEnvironmentVariable("GITHUB_TOKEN","fixture");new GitHub(st,client).SyncUpload(e,CancellationToken.None).GetAwaiter().GetResult();Check(e.RemoteUploadState=="failed" && e.RemoteUploadNote.Contains("tekrar"),"Durdurulan iş yükleniyor kaldı");}finally{Environment.SetEnvironmentVariable("GITHUB_TOKEN",old);}

        });

        Test("CLOUD-UPLOAD-05", "Oturum kalıcı kaydedilemezse video baytları gönderilmez", () => {

            var st=New("cloud-checkpoint-fail");var e=Episode(st);int chunks=0;

            using var client=new HttpClient(new Handler(req=>{

                if(req.RequestUri!.Host=="oauth2.googleapis.com")return Task.FromResult(Ok("{\"access_token\":\"fixture\"}"));

                if(req.Method==HttpMethod.Post){var response=Ok("{}");response.Headers.Location=new Uri("https://www.googleapis.com/upload-session");return Task.FromResult(response);}

                chunks++;return Task.FromResult(Ok("{\"id\":\"unexpected\"}"));

            }));

            var old=Environment.GetEnvironmentVariable("YOUTUBE_REFRESH_TOKEN");try{Environment.SetEnvironmentVariable("YOUTUBE_REFRESH_TOKEN","fixture");bool failed=false;try{new YouTube(st,client,ct=>throw new IOException("Test checkpoint failure")).Upload(e,e.Videos[0],e.Plan!.Long!,CancellationToken.None).GetAwaiter().GetResult();}catch(IOException){failed=true;}Check(failed && chunks==0 && e.Videos[0].UploadSession!="","Oturum kaydı yokken baytlar gönderildi");}finally{Environment.SetEnvironmentVariable("YOUTUBE_REFRESH_TOKEN",old);}

        });

        Test("PROOF-REPLY-01", "Değişmeyen alan durdurmaz; tekil sözcük tam metne güvenle bağlanır", () => {

            var plan=Plan();plan.Long!.Scenes[0].Narration="Herkez burada.";

            var unchanged=Proofreading.Parse(JsonSerializer.Serialize(new SpellingResult{Changes=[new(){Field="Long.Title",Before=plan.Long.Title,After=plan.Long.Title}]}),plan);

            Check(unchanged.Changes.Count==0 && unchanged.ProtocolNotes.Count==1,"Değişmeyen alan reddedildi");

            var partial=Proofreading.Parse(JsonSerializer.Serialize(new SpellingResult{Changes=[new(){Field="long.scenes[0].narration",Before="Herkez",After="Herkes"}]}),plan);

            Check(partial.Changes.Single().Before=="Herkez burada." && partial.Changes.Single().After=="Herkes burada." && partial.Changes.Single().Reason!="","Parça veya rapor bağlamı yanlış");

            Check(Proofreading.Apply(plan,partial).Long!.Scenes[0].Narration=="Herkes burada." && plan.Long.Scenes[0].Narration=="Herkez burada.","Asıl metin bozuldu veya düzeltme uygulanmadı");

        });

        Test("PROOF-REPLY-02", "Belirsiz, ilgisiz ve sayı değiştiren parçalar reddedilir", () => {

            var plan=Plan();plan.Long!.Scenes[0].Narration="Herkez geldi. Herkez gitti. 3 adım.";

            foreach(var pair in new[]{("Herkez","Herkes"),("başka","Herkes"),("3","37")}) Reject(()=>Proofreading.Parse(JsonSerializer.Serialize(new SpellingResult{Changes=[new(){Field="Long.Scenes[0].Narration",Before=pair.Item1,After=pair.Item2,Reason="yazım"}]}),plan));

        });

        Test("PROOF-BATCH-CACHE", "Tamamlanan denetim grupları yeniden istek göndermeden kullanılır", () => {
            var st = New("proof-batch-cache"); st.Settings.ProviderOrder = "OpenRouter"; var e = Episode(st);
            var original = JsonSerializer.Serialize(e.Plan, Json.Options); int calls = 0;
            using var client = new HttpClient(new Handler(req => {
                Interlocked.Increment(ref calls);
                return Task.FromResult(Ok(JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = "{\"Changes\":[]}" } } } })));
            }));
            var old = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
            try {
                Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", "fixture");
                var service = new Services(st, _ => {}, client);
                service.Proofread(e, CancellationToken.None).GetAwaiter().GetResult(); int firstCalls = calls;
                Check(firstCalls > 0, "İlk denetim isteği gönderilmedi");
                e.Plan = JsonSerializer.Deserialize<ContentPlan>(original, Json.Options);
                service.Proofread(e, CancellationToken.None).GetAwaiter().GetResult();
                Check(calls == firstCalls, "Tamamlanan gruplar tekrar gönderildi");
            } finally { Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", old); }
        });
        Test("PROOF-REPLY-03", "Parçalı AI yazım önerisi raporlanır; değiştirilmemiş başlık üretimi durdurmaz", () => {

            var st=New("proof-protocol");st.Settings.ProviderOrder="OpenRouter";var e=Episode(st);e.Plan!.Long!.Scenes[0].Narration="Herkez burada.";

            using var client=new HttpClient(new Handler(req=>Task.FromResult(Ok(JsonSerializer.Serialize(new{choices=new[]{new{message=new{content=JsonSerializer.Serialize(new SpellingResult{Changes=[new(){Field="Long.Title",Before=e.Plan.Long.Title,After=e.Plan.Long.Title},new(){Field="Long.Scenes[0].Narration",Before="Herkez",After="Herkes"}]})}}}})) )));

            var old=Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");try{Environment.SetEnvironmentVariable("OPENROUTER_API_KEY","fixture");new Services(st,_=>{},client).Proofread(e,CancellationToken.None).GetAwaiter().GetResult();Check(e.Plan.Long.Scenes[0].Narration=="Herkes burada." && e.Plan.SpellingHash==Proofreading.Fingerprint(e.Plan),"Denetim tamamlanamadı");var report=Directory.GetFiles(st.Folder(e),"*-rapor.json",SearchOption.AllDirectories).Single();Check(File.ReadAllText(report).Contains("ProtocolNotes"),"Protokol uyarlaması raporlanmadı");}finally{Environment.SetEnvironmentVariable("OPENROUTER_API_KEY",old);}

        });

        Test("FIX-F01", "Hasarlı ana kayıt sağlam paket aynasını korur; kurtarılamayan veri ezilmez", () => {

            var st=New("fix-recovery");Episode(st);st.Save();var path=Path.Combine(st.Root,"state.json");File.Delete(path+".bak");File.WriteAllText(path,"{broken");Check(new Store(st.Root).Episodes.Count==1,"Sağlam paket kayboldu");

            File.Delete(path+".bak");File.WriteAllText(path,"{bad");File.WriteAllText(Path.Combine(st.Root,"episodes.json"),"{bad");Reject(()=>new Store(st.Root));Check(File.ReadAllText(path)=="{bad","Hasarlı kaydın üzerine yazıldı");

        });

        Test("FIX-F02", "İşçi ayarları ana kayda uygulanır ve yeniden açılışta korunur", () => {

            var st=New("fix-worker");var settings=new Settings{ChannelName="PAYLOAD CHANNEL",LocalAiPolicyVersion=1,CloudAiEnabled=true,EnabledCloudProviders="OpenRouter",VoiceProvider="Google TTS"};

            var payload=Path.Combine(st.Root,"payload.txt");File.WriteAllText(payload,Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new WorkerPayload{Settings=settings,Episode=new Episode()},Json.Options))));

            Reject(()=>Worker.Run(st,["--payload",payload]).GetAwaiter().GetResult());var reopened=new Store(st.Root);Check(reopened.Settings.ChannelName=="PAYLOAD CHANNEL"&&reopened.Settings.CloudAiEnabled,"İşçi ayarları yok sayıldı");

        });

        Test("FIX-F03", "Bulut kaynak filtresi logo ve bütün destek kartlarını alır", () => {

            foreach(var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory,"Assets"),"nur-kapisi-*.png"))Check(GitHub.AllowedSource("src/Assets/"+Path.GetFileName(file)),"Varlık dışlandı: "+file);

            Check(!GitHub.AllowedSource("src/Assets/nur-kapisi-destek-x/private.png"),"Alt klasör sızdı");

        });

        Test("FIX-F04", "Editörde yerel görüntü değişince eski hak onayı sıfırlanır", () => {

            var st=New("fix-local-rights");var e=Episode(st);using var editor=new TextEditor(st,e);typeof(TextEditor).GetMethod("SetVideo",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(editor,[video]);var scene=editor.Plan.Long!.Scenes[0];Check(scene.UserLocal&&!scene.RightsConfirmed&&scene.Attribution=="","Eski hak onayı kaldı");Check(e.Plan!.Long!.Scenes[0].RightsConfirmed,"Asıl plan erken değişti");

        });

        Test("FIX-F05", "Başlığı sağlam ama sonu bozuk video tam denetimde reddedilir", () => {

            var st=New("fix-tail-video");var file=Path.Combine(st.Root,"tail.mp4");Media.Exec(ffmpeg,["-y","-f","lavfi","-i","testsrc2=s=160x90:r=15","-f","lavfi","-i","anullsrc=r=44100:cl=mono","-t","5","-c:v","libx264","-c:a","aac","-movflags","+faststart",file],st.Root,CancellationToken.None).GetAwaiter().GetResult();

            var bytes=File.ReadAllBytes(file);File.WriteAllBytes(file,bytes[..(int)(bytes.Length*.8)]);bool rejected=false;try{Reliability.ValidateVideo(st,file,CancellationToken.None,complete:true).GetAwaiter().GetResult();}catch(InvalidOperationException){rejected=true;}Check(rejected,"Bozuk video geçti");Reliability.ValidateVideo(st,video,CancellationToken.None,complete:true).GetAwaiter().GetResult();

        });

        Test("FIX-F06", "Yazım düzeltmesi işaret, yüzde ve tarih değerlerini değiştiremez", () => {

            foreach(var pair in new[]{("Sıcaklık -5 derece.","Sıcaklık 5 derece."),("Oran %5.","Oran 5."),("Tarih 01/02/2026.","Tarih 01/03/2026."),("Değer +5.","Değer -5.")})Check(!Proofreading.SafeCorrection(pair.Item1,pair.Item2),"Korunan değer değişti");Check(Proofreading.SafeCorrection("Herkez %5 gördü.","Herkes %5 gördü."),"Yazım düzeltmesi engellendi");

        });

        Test("FIX-F07", "Genel müzik hak ve atıf bilgisi senaryoya taşınır", () => {

            var st=New("fix-global-music");st.Settings.MusicPath=video;st.Settings.LibraryMusic=new MusicTrack{File=video,Source="Besteci",License="CC BY",Attribution="Besteci — CC BY",RightsConfirmed=true};var script=Plan().Long!;script.MusicTrack=GlobalMusic.Resolve(st,false);Check(YouTube.BuildDescription(script,[]).Contains("Besteci — CC BY"),"Atıf kayboldu");st.Settings.LibraryMusic.RightsConfirmed=false;GlobalMusic.Resolve(st,false)!.Validate();

        });

        Test("FIX-F08", "500 dosyalık tarama sonrası yeni dosyalara ilerlenir", () => {

            var st=New("fix-scan-progress");var pool=new MediaPool(st);Directory.CreateDirectory(Path.Combine(st.Root,"media-pool"));var stamps=new Dictionary<string,string>();for(int i=0;i<500;i++){var f=Path.Combine(Path.Combine(st.Root,"media-pool"),$"a{i:000}.mp4");File.WriteAllText(f,"invalid");var info=new FileInfo(f);stamps[f]=info.Length+":"+info.LastWriteTimeUtc.Ticks;}Json.Write(Path.Combine(Path.Combine(st.Root,"media-pool"),"scan-progress.json"),stamps);File.Copy(video,Path.Combine(Path.Combine(st.Root,"media-pool"),"z-new.mp4"));var result=pool.ScanFolder(CancellationToken.None).GetAwaiter().GetResult();Check(result.Added==1&&result.Remaining==0,"Tarama ilk 500 dosyada kaldı");

        });

        Test("FIX-F09", "Araştırma bütçesi her kaynağa okunabilir pay ayırır", () => {

            var text=Services.BalancedResearch([(new SourceRef{Title="Bir",Url="https://example.com/1"},new string('a',9000)),(new SourceRef{Title="İki",Url="https://example.com/2"},"İKİNCİ KAYNAK METNİ")],4000);Check(text.Length<=4000&&text.Contains("İKİNCİ KAYNAK METNİ")&&text.Contains("KAYNAK 1")&&text.Contains("KAYNAK 2"),"Kaynak dışlandı");

        });

        Test("FIX-F10", "Değişen yayın tarihi uzak güncelleme gerektirir", () => {

            var date=DateTimeOffset.UtcNow.AddDays(1);var asset=new AssetVideo{Scheduled=true,RemotePublishAt=date,PublishAt=date};Check(!Engine.NeedsSchedule(asset),"Aynı tarih tekrarlandı");asset.PublishAt=date.AddDays(1);Check(Engine.NeedsSchedule(asset),"Yeni tarih atlandı");asset.RemotePublishAt=null;Check(Engine.NeedsSchedule(asset),"Bilinmeyen uzak tarih atlandı");

        });

        Test("FIX-F11", "Editör taslağı yeniden açılır; farklı kaynak taslağı uygulanmaz", () => {

            var st=New("fix-drafts");using(var editor=new CardEditor(st)){editor.LoadCover(ChannelPack.Asset("nur-kapisi-kanal-logo.png"));}using(var reopened=new CardEditor(st)){var d=(CardDesign)typeof(CardEditor).GetField("design",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(reopened)!;Check(d.BackgroundImage!="","Kart taslağı kayboldu");}

            var file=Path.Combine(st.Root,"draft.json");EditorDrafts.Save(file,"base",Plan());Check(EditorDrafts.Recover(file,"other",new ContentPlan()).Long==null,"Başka kaynağın taslağı uygulandı");EditorDrafts.Remove(file);Check(!File.Exists(file),"Kaydedilen taslak temizlenmedi");

        });

        Test("FIX-F12", "Taşan veya kesilen kart metni kaydedilemez ve sığdırılabilir", () => {

            var d=CardDesign.Blank();d.Title="Başlık";d.TitleStyle.X=95;d.TitleStyle.Width=84;Reject(()=>CardDesign.Validate(d));d.TitleStyle.X=8;d.TitleStyle.Width=84;d.TitleStyle.Size=220;d.Title=new string('a',400);Reject(()=>CardDesign.Validate(d));CardDesign.FitText(d);CardDesign.Validate(d);

        });

        Test("RESCAN-SOURCE-BUDGET", "Çok uzun başlık ve adresler araştırma bütçesini aşmaz", () => {

            var excerpts=Enumerable.Range(0,14).Select(i=>(new SourceRef{Title=new string('t',500),Url="https://example.com/"+i+new string('u',500)},new string('c',5000)));var text=Services.BalancedResearch(excerpts,4000);Check(text.Length<=4000 && text.Contains("KAYNAK 14"),"Kaynak bütçesi aşıldı");

        });

        Test("RESCAN-CORRUPT-DRAFT", "Bozuk taslak özgün içeriğin açılmasını engellemez", () => {

            var st=New("rescan-draft");var file=Path.Combine(st.Root,"draft.json");File.WriteAllText(file,"{bad");var recovered=EditorDrafts.Recover(file,"base",new ContentPlan());Check(recovered.Long==null && File.ReadAllText(file)=="{bad","Özgün içerik veya taslak kayboldu");

        });

        Test("RESCAN-CLOUD-LOCAL", "Bulut üretimi yerel model ve dosyaları önceden bildirir", () => {

            Reject(()=>GitHub.CheckRemoteVoice(new Settings(),new Episode()));var settings=new Settings{CloudAiEnabled=true,EnabledCloudProviders="OpenRouter",VoiceProvider="ElevenLabs",ClosingCardEnabled=true};Reject(()=>GitHub.CheckRemoteVoice(settings,new Episode()));settings.ClosingCardEnabled=false;settings.CoverMode="Kendi PNG kapak";Reject(()=>GitHub.CheckRemoteVoice(settings,new Episode()));settings.CoverMode="Şablon";var local=new Episode{Plan=Plan(),ShortCount=0};local.Plan.Long!.Scenes[0].UserLocal=true;Reject(()=>GitHub.CheckRemoteVoice(settings,local));settings.VoiceProvider="Piper / ücretsiz";Reject(()=>GitHub.CheckRemoteVoice(settings,new Episode()));

        });

        Test("RESCAN-SOURCE-BUNDLE", "Aktarım filtresinden geçen kaynak paketi eksiksizdir", () => {

            var source=Program.Arg(args,"--source-bundle")??Path.Combine(AppContext.BaseDirectory,"repository");GitHub.ValidateSourceBundle(source);var target=Path.Combine(root,"filtered-repository");foreach(var file in Directory.GetFiles(source,"*",SearchOption.AllDirectories)){var relative=Path.GetRelativePath(source,file).Replace('\\','/');if(!GitHub.AllowedSource(relative))continue;var output=Path.Combine(target,relative);Directory.CreateDirectory(Path.GetDirectoryName(output)!);File.Copy(file,output,true);}GitHub.ValidateSourceBundle(target);

        });

        Test("A01", "Yeni senaryo eski çıktıyla onaylanamaz; yinelenen çıktı reddedilir", () => { var st = New("approval"); var e = Episode(st); e.Plan!.Long!.Title = "Değişen başlık"; Reject(() => new Engine(st).Approve(e)); Check(!e.Approved, "Onay verildi"); e.Videos[0].PlanHash = Reliability.MediaPlanHash(e, "long"); e.Videos.Add(e.Videos[0]); Reject(() => new Engine(st).Approve(e)); });

        Test("A02", "Değişmiş medya hash kontrolünü geçemez", () => { var st = New("corrupt-video"); var e = Episode(st); File.WriteAllText(e.Videos[0].File, "corrupt"); Check(!Reliability.OutputsCurrent(e), "Bozuk dosya geçerli sayıldı"); Reject(() => new Engine(st).Approve(e)); });

        Test("A03", "Kota log kırpılması ve paket silinmesinden etkilenmez", () => { var st = New("quota"); var e = Episode(st); st.RecordProduction(e); for (int i = 0; i < 300; i++) e.Log("Kayıt"); Check(st.ProducedToday == 1, "Kota unutuldu"); st.RecordProduction(e); Check(st.ProducedToday == 1, "Aynı paket iki kez sayıldı"); st.Episodes.Remove(e); st.Save(); Check(new Store(st.Root).ProducedToday == 1, "Silme/yeni açılış kotayı sıfırladı"); });

        Test("A04", "Bozuk kayıt sağlam yedekten kurtarılır", () => { var st = New("recovery"); Episode(st); st.Save(); st.Save(); File.WriteAllText(Path.Combine(st.Root, "state.json"), "{broken"); var recovered = new Store(st.Root); Check(recovered.Episodes.Count == 1, "Paket kurtarılamadı"); Check(Directory.GetFiles(st.Root, "state.json.damaged-*").Length == 1, "Hasarlı kayıt korunmadı"); });

        Test("A05", "Sayı/anlam/alıntı korunur; kesin yazım hatası düzeltilir", () => { var plan = Plan(); plan.Long!.Scenes[0].Narration = "3 adım var."; Reject(() => Proofreading.Apply(plan, new() { Changes = [new() { Field = "Long.Scenes[0].Narration", Before = "3 adım var.", After = "37 adım var.", Reason = "yazım" }] })); Check(!Proofreading.SafeCorrection("Bu bir kul.", "Bu bir kıl."), "Anlam değiştiren yakın sözcük geçti"); Check(Proofreading.SafeCorrection("Herkez burada.", "Herkes burada."), "Bilinen yazım hatası reddedildi"); Check(!Proofreading.SafeCorrection("Ayet: “metin”", "Ayet: “başka”"), "Alıntı değişti"); });

        Test("A06", "Bozuk stok taranmaz ve seçilmez", () => { var st = New("invalid-pool"); var folder = Path.Combine(st.Root, "media-pool"); Directory.CreateDirectory(folder); File.WriteAllText(Path.Combine(folder, "sea.mp4"), "not-video"); var pool = new MediaPool(st); var scan = pool.ScanFolder(CancellationToken.None).GetAwaiter().GetResult(); Check(scan.Added == 0 && scan.Failed == 1 && !pool.TryUse(new(), "sea", st.Root, [], true), "Bozuk stok kabul edildi"); });

        Test("A07", "Aynı yoldaki değişmiş stok yeniden indekslenir", () => { var st = New("changed-pool"); var folder = Path.Combine(st.Root, "media-pool"); Directory.CreateDirectory(folder); var file = Path.Combine(folder, "sea.mp4"); File.Copy(video, file); var pool = new MediaPool(st); pool.ScanFolder(CancellationToken.None).GetAwaiter().GetResult(); var original=pool.List()[0];original.RightsConfirmed=true;pool.Save([original]);var hash = original.Hash; using (var stream = new FileStream(file, FileMode.Append)) stream.Write([1,2,3,4]); pool.ScanFolder(CancellationToken.None).GetAwaiter().GetResult(); Check(!pool.List()[0].RightsConfirmed && pool.List()[0].Hash != hash && pool.List()[0].Hash == Reliability.FileHash(file), "İndeks güncellenmedi"); });

        Test("A08", "Boş ve yinelenen kaynaklar reddedilir", () => { var plan = Plan(); plan.Sources = [new(), new()]; Reject(() => Rules.ValidatePlan(new() { ShortCount = 0, Plan = plan })); plan.Sources = [new() { Title = "A", Url = "https://example.com/a" }, new() { Title = "B", Url = "https://example.com/a/" }]; Reject(() => Rules.ValidatePlan(new() { ShortCount = 0, Plan = plan })); });

        Test("A09", "NaN, Infinity ve negatif kullanım reddedilir", () => { foreach (var n in new[] { double.NaN, double.PositiveInfinity, -1 }) Reject(() => CloudUsage.Check(new() { Month = DateTimeOffset.UtcNow.ToString("yyyy-MM"), Fetched = DateTimeOffset.UtcNow, Rows = [new() { Unit = "characters", Amount = n }] }, 0, 100, 900000)); });

        Test("A10", "Uzun açıklamada abonelik çağrısı ve kredi korunur", () => { var text = YouTube.BuildDescription(new() { Description = new string('a', 5000), Scenes = [new() { Attribution = "Zorunlu kredi" }] }, []); Check(text.Length <= 4500 && text.Contains("abone ol") && text.EndsWith("Zorunlu kredi"), "Açıklama yanlış kesildi"); });

        Test("STOCK-REGISTERED-SOURCE", "Sırada unutulmuş kayıtlı Pexels kaynağı aranır", () => { var st = New("registered-source"); st.Settings.StockOrder = "Pixabay"; st.Settings.PoolEnabled = st.Settings.PoolFallback = st.Settings.PoolAutoSave = st.Settings.StockGeneralFallback = false; var old = Environment.GetEnvironmentVariable("PEXELS_API_KEY"); try { Environment.SetEnvironmentVariable("PEXELS_API_KEY", "fixture"); var queries = new List<string>(); using var client = new HttpClient(new Handler(async req => { if (req.RequestUri!.Host == "fixture.example") return new(HttpStatusCode.OK) { Content = new ByteArrayContent(File.ReadAllBytes(video)) }; queries.Add(req.RequestUri.Query); return Ok(req.RequestUri.Query.Contains("seventh") ? "{\"videos\":[{\"id\":7,\"url\":\"https://www.pexels.com/video/7\",\"user\":{\"name\":\"Test\"},\"video_files\":[{\"file_type\":\"video/mp4\",\"width\":160,\"link\":\"https://fixture.example/video.mp4\"}]}]}" : "{\"videos\":[]}"); })); var scene = new Scene { VisualQuery = "a; b; c; d; e; f; seventh" }; new Services(st, _ => {}, client).Stock(scene, false, st.Root, [], CancellationToken.None).GetAwaiter().GetResult(); Check(queries.Count == 7 && scene.RightsConfirmed && scene.Attribution.Contains("Pexels"), "Yedinci terim/kredi doğrulanmadı"); } finally { Environment.SetEnvironmentVariable("PEXELS_API_KEY", old); } });
        Test("A11", "Yedinci stok terimi denenir ve bulunan video doğrulanır", () => { var st = New("queries"); st.Settings.StockOrder = "Pexels"; st.Settings.PoolEnabled = st.Settings.PoolFallback = st.Settings.PoolAutoSave = st.Settings.StockGeneralFallback = false; var old = Environment.GetEnvironmentVariable("PEXELS_API_KEY"); try { Environment.SetEnvironmentVariable("PEXELS_API_KEY", "fixture"); var queries = new List<string>(); using var client = new HttpClient(new Handler(async req => { if (req.RequestUri!.Host == "fixture.example") return new(HttpStatusCode.OK) { Content = new ByteArrayContent(File.ReadAllBytes(video)) }; queries.Add(req.RequestUri.Query); return Ok(req.RequestUri.Query.Contains("seventh") ? "{\"videos\":[{\"id\":7,\"url\":\"https://www.pexels.com/video/7\",\"user\":{\"name\":\"Test\"},\"video_files\":[{\"file_type\":\"video/mp4\",\"width\":160,\"link\":\"https://fixture.example/video.mp4\"}]}]}" : "{\"videos\":[]}"); })); var scene = new Scene { VisualQuery = "a; b; c; d; e; f; seventh" }; new Services(st, _ => {}, client).Stock(scene, false, st.Root, [], CancellationToken.None).GetAwaiter().GetResult(); Check(queries.Count == 7 && scene.RightsConfirmed && scene.Attribution.Contains("Pexels"), "Yedinci terim/kredi doğrulanmadı"); } finally { Environment.SetEnvironmentVariable("PEXELS_API_KEY", old); } });

        Test("NVIDIA-SCAN", "NVIDIA taraması JSON doğrular, hesap önbelleğini ayırır ve bozuk modelden geçer", () => {
            var old = Environment.GetEnvironmentVariable("NVIDIA_API_KEY");
            try {
                Environment.SetEnvironmentVariable("NVIDIA_API_KEY", "fixture-nvidia-scan");
                var st = New("nvidia-scan"); st.Settings.EnabledCloudProviders = st.Settings.ProviderOrder = "NVIDIA"; st.Settings.NvidiaModel = "bad-json";
                int gets = 0, posts = 0;
                using var client = new HttpClient(new Handler(async req => {
                    if (req.Method == HttpMethod.Get) { gets++; return Ok("{\"data\":[{\"id\":\"bad-json\"},{\"id\":\"blocked\"},{\"id\":\"good-a\"},{\"id\":\"z-ai/glm-5.3-flash\"}]}"); }
                    posts++; using var b = JsonDocument.Parse(await req.Content!.ReadAsStringAsync()); var model = b.RootElement.GetProperty("model").GetString();
                    if (model == "z-ai/glm-5.3-flash") Check(b.RootElement.GetProperty("reasoning_effort").GetString() == "low" && b.RootElement.GetProperty("max_tokens").GetInt32() == 2048, "GLM kısa kontrol bütçesi yanlış");
                    if (model == "blocked") return new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = new StringContent("{}") };
                    return Ok(JsonSerializer.Serialize(new { choices = new[] { new { finish_reason = "stop", message = new { content = model == "bad-json" ? "{\"Ready\":true,\"Text\":\"anka\",\"Count\":\"2\"}" : "{\"Ready\":true,\"Text\":\"anka\",\"Count\":2}" } } } }));
                }));
                NvidiaModels.Start(st, true, client).GetAwaiter().GetResult();
                var report = NvidiaModels.Read(st); Check(report.Complete && report.Models.Count == 4 && NvidiaModels.Working(st).Length == 2, "Yanlış modeller hazır sayıldı");
                Check(!File.ReadAllText(Path.Combine(st.Root, "nvidia-model-health.json")).Contains("fixture-nvidia-scan"), "Anahtar önbelleğe yazıldı");
                report.FinishedAt = DateTimeOffset.UtcNow.AddDays(-40); foreach (var entry in report.Models) entry.CheckedAt = DateTimeOffset.UtcNow.AddDays(-40);
                Json.Write(Path.Combine(st.Root, "nvidia-model-health.json"), report);
                Check(NvidiaModels.Working(st).Length == 2, "Eski çalışan kayıtlar gereksiz silindi");
                NvidiaModels.Start(st, false, client).GetAwaiter().GetResult(); Check(gets == 1 && posts == 4, "Önbellek gereksiz kota tüketti");
                st.Settings.NvidiaModel = "good-a"; var called = new List<string>();
                using var production = new HttpClient(new Handler(async req => { using var b = JsonDocument.Parse(await req.Content!.ReadAsStringAsync()); var model = b.RootElement.GetProperty("model").GetString()!; called.Add(model);
                    return model == "good-a" ? new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{}") } : Ok("{\"choices\":[{\"message\":{\"content\":\"{\\\"Ready\\\":true}\"}}]}"); }));
                new Services(st, _ => {}, production).VerifyModel("NVIDIA", CancellationToken.None).GetAwaiter().GetResult();
                Check(called.SequenceEqual(new[] { "good-a", "z-ai/glm-5.3-flash" }) && !NvidiaModels.Working(st).Contains("good-a"), "Model hatası tüm NVIDIA'yı durdurdu");
                Environment.SetEnvironmentVariable("NVIDIA_API_KEY", "other-account"); Check(NvidiaModels.Working(st).Length == 0 && NvidiaModels.Read(st).Models.Count == 0, "Farklı anahtar eski erişimi kullandı");
                st.Settings.NvidiaAutoSelect = false; NvidiaModels.Start(st, true, client).GetAwaiter().GetResult(); Check(gets == 1, "Kapalı otomatik seçim tarama yaptı");
            } finally { Environment.SetEnvironmentVariable("NVIDIA_API_KEY", old); }
        });
        Test("NVIDIA-GEMINI-FALLBACK", "Tüm kayıtlı NVIDIA modelleri denenmeden Gemini çağrılmaz", () => {
            var old = Environment.GetEnvironmentVariable("NVIDIA_API_KEY"); var oldGemini = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
            try {
                Environment.SetEnvironmentVariable("NVIDIA_API_KEY", "fixture-fallback"); Environment.SetEnvironmentVariable("GEMINI_API_KEY", "fixture-gemini");
                var st = New("nvidia-gemini-fallback"); st.Settings.ProviderOrder = st.Settings.EnabledCloudProviders = "NVIDIA,Gemini"; st.Settings.NvidiaModel = "model-a";
                var report = NvidiaModels.Read(st); report.KeyId = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes("fixture-fallback"))); report.Complete = true; report.Total = 2;
                report.Models = new[] { "model-a", "model-b" }.Select(m => new NvidiaModelCheck { Model = m, Status = "Hazır", CheckedAt = DateTimeOffset.UtcNow.AddDays(-40) }).ToList();
                Json.Write(Path.Combine(st.Root, "nvidia-model-health.json"), report); var called = new List<string>();
                using var client = new HttpClient(new Handler(async req => { using var body = JsonDocument.Parse(await req.Content!.ReadAsStringAsync());
                    if (req.RequestUri!.Host == "integrate.api.nvidia.com") { called.Add(body.RootElement.GetProperty("model").GetString()!); return new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{}") }; }
                    called.Add("Gemini"); return Ok("{\"choices\":[{\"message\":{\"content\":\"{\\\"Ready\\\":true}\"}}]}");
                }));
                new Services(st, _ => {}, client).Generate("Ready JSON", "test", CancellationToken.None).GetAwaiter().GetResult();
                Check(called.SequenceEqual(new[] { "model-a", "model-b", "Gemini" }), "Gemini erkenden çağrıldı veya NVIDIA modeli atlandı");
            } finally { Environment.SetEnvironmentVariable("NVIDIA_API_KEY", old); Environment.SetEnvironmentVariable("GEMINI_API_KEY", oldGemini); }
        });

        Test("NVIDIA-SCAN-LIMIT", "NVIDIA genel kota sınırı taramayı durdurur; iptal sonuç uydurmaz", () => {
            var old = Environment.GetEnvironmentVariable("NVIDIA_API_KEY");
            try {
                Environment.SetEnvironmentVariable("NVIDIA_API_KEY", "fixture-limit"); var st = New("nvidia-scan-limit"); st.Settings.EnabledCloudProviders = "NVIDIA"; int posts = 0;
                using var client = new HttpClient(new Handler(req => {
                    if (req.Method == HttpMethod.Get) return Task.FromResult(Ok("{\"data\":[{\"id\":\"model-a\"},{\"id\":\"model-b\"},{\"id\":\"model-c\"},{\"id\":\"model-d\"}]}"));
                    posts++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = new StringContent("{}") });
                }));
                NvidiaModels.Start(st, true, client).GetAwaiter().GetResult(); var report = NvidiaModels.Read(st);
                Check(posts == 3 && !report.Complete && report.RetryAt > DateTimeOffset.UtcNow && NvidiaModels.Candidates(st).Length == 0, "Kota sınırında kontrolsüz tarama veya sahte model seçimi");
                NvidiaModels.Start(st, false, client).GetAwaiter().GetResult(); Check(posts == 3, "Kota beklemesi atlandı");
                var cancelled = New("nvidia-scan-cancel"); cancelled.Settings.EnabledCloudProviders = "NVIDIA";
                using var cancelClient = new HttpClient(new Handler(req => { NvidiaModels.Cancel(cancelled); return Task.FromResult(Ok("{\"data\":[{\"id\":\"model-a\"}]}")); }));
                NvidiaModels.Start(cancelled, true, cancelClient).GetAwaiter().GetResult(); Check(NvidiaModels.Working(cancelled).Length == 0 && NvidiaModels.Progress(cancelled).Contains("durduruldu"), "İptal başarısız");
            } finally { Environment.SetEnvironmentVariable("NVIDIA_API_KEY", old); }
        });

        Test("NVIDIA-JSON", "NVIDIA JSON modu, şema alternatifi, düşünme ayrımı ve hatalı yanıt reddi", () => {
            var old = Environment.GetEnvironmentVariable("NVIDIA_API_KEY");
            try {
                Environment.SetEnvironmentVariable("NVIDIA_API_KEY", "fixture-nvidia-json");
                foreach (var mode in new[] { "format-fallback", "repair", "invalid", "truncated" }) {
                    var st = New("nvidia-json-" + mode); st.Settings.EnabledCloudProviders = st.Settings.ProviderOrder = "NVIDIA"; st.Settings.NvidiaModel = "qwen/qwen3-test"; int calls = 0;
                    using var client = new HttpClient(new Handler(async req => {
                        calls++; using var body = JsonDocument.Parse(await req.Content!.ReadAsStringAsync()); var b = body.RootElement;
                        Check(!b.GetProperty("chat_template_kwargs").GetProperty("enable_thinking").GetBoolean(), "Düşünme kapatılmadı");
                        Check(b.GetProperty("messages")[0].GetProperty("content").GetString()!.Contains("valid JSON"), "JSON talimatı eksik");
                        if (mode == "format-fallback" && calls <= 2) {
                            Check(b.GetProperty("response_format").GetProperty("type").GetString() == (calls == 1 ? "json_schema" : "json_object"), "Biçim alternatifi yanlış");
                            return new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("{\"error\":{\"message\":\"response_format unsupported\"}}") };
                        }
                        if (mode == "format-fallback") Check(!b.TryGetProperty("response_format", out _), "Desteklenmeyen biçim tekrarlandı");
                        var content = mode == "invalid" || mode == "repair" && calls == 1 ? "JSON yerine açıklama" : "<think>{\"draft\":false}</think>{\"Ready\":true}";
                        return Ok(JsonSerializer.Serialize(new { choices = new[] { new { finish_reason = mode == "truncated" && calls == 1 ? "length" : "stop", message = new { content } } } }));
                    }));
                    bool rejected = false;
                    try {
                        var result = new Services(st, _ => {}, client).Generate("Ready JSON", "test", CancellationToken.None,
                            raw => { using var doc = JsonDocument.Parse(Services.ExtractJson(raw)); return doc.RootElement.GetProperty("Ready").GetBoolean(); },
                            onlyProvider: "NVIDIA", responseSchema: mode == "format-fallback" ? new { type = "object" } : null).GetAwaiter().GetResult();
                        Check(!result.Contains("draft"), "Düşünme JSON'u sonuca karıştı");
                    } catch (AiUnavailableException) { rejected = true; }
                    Check(rejected == (mode == "invalid") && calls == (mode == "format-fallback" ? 3 : 2), "Deneme sınırı veya doğrulama yanlış");
                }
            } finally { Environment.SetEnvironmentVariable("NVIDIA_API_KEY", old); }
        });

        Test("NVIDIA-PATCH", "NVIDIA model listesi, seçili model, izin ve anahtar gizliliği", () => {
            var st = New("nvidia-patch"); st.Settings.EnabledCloudProviders = "NVIDIA"; st.Settings.ProviderOrder = "NVIDIA";
            var old = Environment.GetEnvironmentVariable("NVIDIA_API_KEY");
            try {
                Environment.SetEnvironmentVariable("NVIDIA_API_KEY", "nvidia-fixture-secret"); st.Save();
                Check(!SupportPrivacy.Clean(st, "nvidia-fixture-secret").Contains("fixture-secret"), "Destek kaydında anahtar gizlenmedi");
                Check(new Store(st.Root).Settings.NvidiaModel == st.Settings.NvidiaModel, "Model kaydı bozuldu");
                int calls = 0; var logs = new List<string>();
                using var client = new HttpClient(new Handler(async req => {
                    Check(req.RequestUri!.Host == "integrate.api.nvidia.com" && req.Headers.Authorization?.Parameter == "nvidia-fixture-secret", "Adres veya kimlik doğrulama yanlış");
                    if (req.Method == HttpMethod.Get) return Ok("{\"data\":[{\"id\":\"meta/llama-3.1-70b-instruct\"},{\"id\":\"nvidia/nv-embed-v1\"},{\"id\":\"nvidia/rerank\"}]}");
                    calls++; using var body = JsonDocument.Parse(await req.Content!.ReadAsStringAsync());
                    Check(body.RootElement.GetProperty("model").GetString() == st.Settings.NvidiaModel && body.RootElement.GetProperty("response_format").GetProperty("type").GetString() == "json_object", "Model veya JSON uyumu yanlış");
                    return Ok("{\"choices\":[{\"message\":{\"content\":\"{\\\"Ready\\\":true}\"}}]}");
                }));
                Check(ModelDiscovery.Nvidia(st, CancellationToken.None, client).GetAwaiter().GetResult().SequenceEqual(new[] { st.Settings.NvidiaModel }), "Metin dışı model listede kaldı");
                new Services(st, logs.Add, client).VerifyModel("NVIDIA", CancellationToken.None).GetAwaiter().GetResult();
                Check(calls == 1 && !string.Join(" ", logs).Contains("fixture-secret"), "İstek veya gizlilik hatası");
                st.Settings.CloudAiEnabled = false; Reject(() => new Services(st, _ => {}, client).VerifyModel("NVIDIA", CancellationToken.None).GetAwaiter().GetResult());
                Check(calls == 1, "İzin kapalıyken istek gönderildi");
                st.Settings.CloudAiEnabled = true;
                using var denied = new HttpClient(new Handler(req => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("{}") })));
                Reject(() => new Services(st, _ => {}, denied).VerifyModel("NVIDIA", CancellationToken.None).GetAwaiter().GetResult());
                Environment.SetEnvironmentVariable("NVIDIA_API_KEY", null);
                using var form = new StudioForm(st);
                typeof(StudioForm).GetMethod("Navigate", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(form, ["Bağlantı Merkezi"]);
                IEnumerable<Control> Children(Control c) => c.Controls.Cast<Control>().SelectMany(x => new[] { x }.Concat(Children(x)));
                Check(Children(form).OfType<TextBox>().Any(t => t.PlaceholderText.Contains("NVIDIA_API_KEY") && t.UseSystemPasswordChar), "NVIDIA anahtar kartı eksik");
                typeof(StudioForm).GetMethod("Navigate", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(form, ["Bağlantı Merkezi"]);
                Check(new[] { "NVIDIA Modellerini Tara", "Taramayı Durdur", "Model Kontrol Sonuçları" }.All(name => Children(form).OfType<Button>().Any(b => b.Text == name)), "Tarama arayüzü eksik");
            } finally { Environment.SetEnvironmentVariable("NVIDIA_API_KEY", old); }
        });

        Test("A12", "Guard modelleri senaryo listesinden çıkarılır", () => { var st = New("models"); var old = Environment.GetEnvironmentVariable("GROQ_API_KEY"); try { Environment.SetEnvironmentVariable("GROQ_API_KEY", "fixture"); using var client = new HttpClient(new Handler(req => Task.FromResult(Ok("{\"data\":[{\"id\":\"meta-llama/llama-prompt-guard-2-22m\"},{\"id\":\"openai/gpt-oss-120b\"}]}")))); var models = ModelDiscovery.Groq(st, CancellationToken.None, client).GetAwaiter().GetResult(); Check(models.SequenceEqual(new[] { "openai/gpt-oss-120b" }), "Guard listede kaldı"); } finally { Environment.SetEnvironmentVariable("GROQ_API_KEY", old); } });

        Test("A13", "Sorgu değişimi eski otomatik dosyayı yerel diye bağlamaz", () => { var e = new Episode { ShortCount = 0, Plan = Plan() }; e.Plan.Long!.Scenes[0].StockUrl = "https://stock.example/video"; using var editor = new SceneEditor(e, New("scene-editor")); var grid = (DataGridView)typeof(SceneEditor).GetField("grid", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(editor)!; grid.Rows[0].Cells[1].Value = "forest"; typeof(SceneEditor).GetMethod("Commit", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(editor, null); Check(editor.Plan.Long!.Scenes[0].StockFile == "", "Eski dosya yeniden bağlandı"); });

        Test("YT-SYNC-01", "YouTube yayın durumu alınır, bağlantı hatasında son kayıt korunur", () => {

            var st = New("yt-sync"); var a = new AssetVideo { YouTubeId = "a", PublishAt = DateTimeOffset.UtcNow.AddDays(2) }; var b = new AssetVideo { YouTubeId = "b" }; st.Episodes.Add(new Episode { Videos = [a,b] }); bool fail = false;

            using var client = new HttpClient(new Handler(req => Task.FromResult(req.RequestUri!.Host == "oauth2.googleapis.com" ? Ok("{\"access_token\":\"fixture\"}") : fail ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) : Ok("{\"items\":[{\"id\":\"a\",\"status\":{\"privacyStatus\":\"public\",\"uploadStatus\":\"processed\"}},{\"id\":\"b\",\"status\":{\"privacyStatus\":\"private\",\"publishAt\":\"2099-01-01T12:00:00Z\"}}]}"))));

            var old = Environment.GetEnvironmentVariable("YOUTUBE_REFRESH_TOKEN"); try {

                Environment.SetEnvironmentVariable("YOUTUBE_REFRESH_TOKEN", "fixture"); var yt = new YouTube(st,client); yt.RefreshVideoStates(CancellationToken.None).GetAwaiter().GetResult();

                Check(a.YouTubeState == "Yayında" && !Engine.NeedsSchedule(a) && b.YouTubeState == "Planlı bekliyor" && b.Scheduled && b.RemotePublishAt != null && a.PublishAt != null, "Durum veya yerel tarih hatalı");

                fail = true; bool rejected = false; try { yt.RefreshVideoStates(CancellationToken.None).GetAwaiter().GetResult(); } catch (ProviderException) { rejected = true; } Check(rejected, "HTTP hatası kayboldu"); Check(a.YouTubeState == "Yayında" && b.Scheduled, "Son kayıt kayboldu");

            } finally { Environment.SetEnvironmentVariable("YOUTUBE_REFRESH_TOKEN",old); }

        });

        Test("BACKUP-01", "Video yedeği doğrulanır, bozulmuş kopya onarılır, değişmiş asıl reddedilir", () => {

            var st = New("backup"); st.Settings.VideoBackupFolder = Path.Combine(st.Root,"external"); var file = Path.Combine(st.Root,"video.mp4"); File.WriteAllText(file,"fixture");

            var video = new AssetVideo { Key = "v", File = file, FileHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(file))) }; var e = new Episode { Videos = [video] };

            VideoBackup.Save(st,e,CancellationToken.None).GetAwaiter().GetResult(); var copy = Directory.GetFiles(st.Settings.VideoBackupFolder,"*.mp4",SearchOption.AllDirectories).Single(); Check(File.ReadAllText(copy)=="fixture", "Yedek yok");

            File.WriteAllText(copy,"broken"); VideoBackup.Save(st,e,CancellationToken.None).GetAwaiter().GetResult(); Check(File.ReadAllText(copy)=="fixture", "Bozuk yedek onarılmadı");

            File.WriteAllText(file,"changed"); Reject(() => VideoBackup.Save(st,e,CancellationToken.None).GetAwaiter().GetResult()); Check(File.ReadAllText(copy)=="fixture", "Sağlam yedek bozuldu");

        });

        Test("STOCK-01", "Planlı stok tamamlananı atlar, değişen tarihi tekrar gönderir", () => {

            var date = DateTimeOffset.UtcNow.AddDays(1);

            var finished = new Episode { Videos = [new AssetVideo { Scheduled = true, YouTubeId = "uploaded", PublishAt = date, RemotePublishAt = date }] };

            var pending = new Episode { Videos = [new AssetVideo { PublishAt = date }] };

            var demo = new Episode { Demo = true, Videos = [new AssetVideo { PublishAt = date }] };

            Check(PlannedStock.Pending([finished, pending, demo]).SequenceEqual([pending]), "Tamamlanan veya örnek kuyrukta");

            finished.Videos[0].PublishAt = date.AddDays(1);

            Check(PlannedStock.Pending([finished]).Count == 1, "Tarih değişimi atlandı");

        });

        Test("SYNC-01", "GitHub çalışma durumu doğru depo ve paketle eşleşir", () => {

            var st = New("run-sync"); st.Settings.Repo = "fixture/repo";

            var episode = new Episode { Id = "package", RemoteRepo = "fixture/repo", RemoteRunId = "gönderildi", Status = "GitHub'da" };

            var other = new Episode { Id = "package", RemoteRepo = "other/repo", RemoteRunId = "gönderildi" }; st.Episodes.AddRange([episode, other]);

            using var client = new HttpClient(new Handler(req => Task.FromResult(Ok("{\"workflow_runs\":[{\"id\":123,\"display_title\":\"Anka paket package\",\"status\":\"completed\",\"conclusion\":\"failure\",\"html_url\":\"https://github.com/fixture/repo/actions/runs/123\"}]}"))));

            var old = Environment.GetEnvironmentVariable("GITHUB_TOKEN"); try {

                Environment.SetEnvironmentVariable("GITHUB_TOKEN", "fixture"); new GitHub(st, client).Runs(CancellationToken.None).GetAwaiter().GetResult();

                Check(episode.RemoteRunId == "123" && episode.RemoteState == "completed / failure" && other.RemoteRunId == "gönderildi" && episode.Status == "GitHub'da", "Başarısız iş tamamlandı sayıldı veya başka depo değişti");

                Check(new Store(st.Root).Episodes[0].RemoteState == "completed / failure", "Durum kalıcı değil");

            } finally { Environment.SetEnvironmentVariable("GITHUB_TOKEN", old); }

        });

        Test("A14", "Hemen yayınla/planla/kaldır mevcut YouTube status alanlarını korur", () => { var st = New("youtube"); var old = Environment.GetEnvironmentVariable("YOUTUBE_REFRESH_TOKEN"); try { Environment.SetEnvironmentVariable("YOUTUBE_REFRESH_TOKEN", "fixture"); var updates = new List<JsonElement>(); using var client = new HttpClient(new Handler(async req => { if (req.RequestUri!.Host == "oauth2.googleapis.com") return Ok("{\"access_token\":\"fixture\"}"); if (req.Method == HttpMethod.Get) return Ok("{\"items\":[{\"status\":{\"selfDeclaredMadeForKids\":true,\"containsSyntheticMedia\":true,\"embeddable\":false,\"license\":\"creativeCommon\",\"publicStatsViewable\":false}}]}"); using var doc = JsonDocument.Parse(await req.Content!.ReadAsStringAsync()); updates.Add(doc.RootElement.GetProperty("status").Clone()); return Ok(JsonSerializer.Serialize(new { status = updates[^1] })); })); var asset = new AssetVideo { YouTubeId = "fixture", PublishAt = DateTimeOffset.UtcNow.AddHours(1) }; var yt = new YouTube(st, client); yt.Schedule(asset, CancellationToken.None).GetAwaiter().GetResult(); Check(asset.RemotePublishAt == asset.PublishAt, "Uzak tarih kaydedilmedi"); yt.Unschedule(asset, CancellationToken.None).GetAwaiter().GetResult(); Check(updates.All(s => s.GetProperty("selfDeclaredMadeForKids").GetBoolean() && s.GetProperty("containsSyntheticMedia").GetBoolean() && !s.GetProperty("embeddable").GetBoolean() && s.GetProperty("license").GetString() == "creativeCommon") && !updates[1].TryGetProperty("publishAt", out _) && asset.PublishAt != null && asset.RemotePublishAt == null, "Mevcut ayar veya yerel tarih kayboldu"); yt.PublishNow(asset, CancellationToken.None).GetAwaiter().GetResult(); Check(updates[2].GetProperty("privacyStatus").GetString() == "public" && !updates[2].TryGetProperty("publishAt", out _) && asset.YouTubeState == "Yayında" && asset.PublishAt != null && !asset.Scheduled, "Hemen yayınlama doğrulanmadı"); } finally { Environment.SetEnvironmentVariable("YOUTUBE_REFRESH_TOKEN", old); } });

        Test("A15", "AI kazananı ve kalıcı hata sonraki adımda hatırlanır", () => { var st = New("health"); st.Settings.ProviderOrder = "Gemini,OpenRouter"; var g = Environment.GetEnvironmentVariable("GEMINI_API_KEY"); var o = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY"); try { Environment.SetEnvironmentVariable("GEMINI_API_KEY", "fixture"); Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", "fixture"); int google = 0; using var client = new HttpClient(new Handler(req => { if (req.RequestUri!.Host.Contains("google")) { google++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.PaymentRequired) { Content = new StringContent("{}") }); } return Task.FromResult(Ok("{\"choices\":[{\"message\":{\"content\":\"{}\"}}]}")); })); new Services(st, _ => {}, client).Generate("JSON", "test", CancellationToken.None).GetAwaiter().GetResult(); new Services(st, _ => {}, client).Generate("JSON", "test", CancellationToken.None).GetAwaiter().GetResult(); Check(google == 1 && st.PreferredProvider == "OpenRouter", "Engelli sağlayıcı tekrar çağrıldı"); } finally { Environment.SetEnvironmentVariable("GEMINI_API_KEY", g); Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", o); } });

        Test("A16", "Geçici hata devam zamanı verir; kalıcı ve elle durdurulan iş zorlanmaz", () => { var e = new Episode(); Reliability.Failed(e, new HttpRequestException("network"), "Produce"); Check(e.RetryAt > DateTimeOffset.UtcNow && e.ResumeStage == "Produce", "Devam zamanı yok"); var saved = new Store(New("resume").Root); saved.Episodes.Add(new() { Status = "Üretiliyor" }); saved.Save(); var reopened = new Store(saved.Root); Check(reopened.Episodes[0].Status == "Kesildi" && reopened.Episodes[0].ResumeStage == "Produce" && reopened.Episodes[0].RetryAt != null, "Yeniden açılış aşaması kayboldu"); Reliability.Failed(e, new ProviderException("Gemini", 402), "Plan"); Check(e.RetryAt == null, "Kalıcı hata tekrarlandı"); });

        Test("A17", "Gezinme eski kontrolleri dispose eder", () => { using var form = new StudioForm(New("ui")); var page = (FlowLayoutPanel)typeof(StudioForm).GetField("page", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(form)!; var controls = page.Controls.Cast<Control>().ToArray(); typeof(StudioForm).GetMethod("Navigate", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(form, ["İçerik paketleri"]); Check(controls.All(c => c.IsDisposed), "Eski kontroller dispose edilmedi"); });

        Test("A18", "İki eski havuz yazıcısı farklı kayıtları kaybettirmez", () => { var st = New("pool-writers"); var a = new MediaPool(st); var b = new MediaPool(st); var first = a.List(); var second = b.List(); first.Add(new() { Id = "a", File = video }); second.Add(new() { Id = "b", File = video }); a.Save(first); b.Save(second); Check(a.List().Count == 2, "Kayıt kayboldu"); });

        Test("S01/S02", "Ortak işlem kapısı iki işi ve kayıt değişikliğini ayırır", () => { var st = New("lease"); using var lease = new OperationLease(st); Check(new Engine(st).Busy, "Dış işlem Busy görünmüyor"); Reject(() => { using var second = new OperationLease(st); }); Reject(() => new Engine(st).Plan(new() { Topic = "Test", Demo = true }).GetAwaiter().GetResult()); });

        Test("S03", "İlk ses bloğu ikinci blok hatasından sonra tekrar üretilmez", () => { var st = New("audio-resume"); var e = new Episode(); var script = new VideoScript { Scenes = [new() { Narration = "Birinci ses parçası." }, new() { Narration = "İkinci ses parçası." }] }; var services = new Services(st, _ => {}); int calls = 0; bool fail = true; Task Speak(string text, string path, Settings voice, CancellationToken ct) { calls++; if (fail && calls == 2) throw new InvalidOperationException("fixture failure"); return Media.Exec(ffmpeg, ["-y", "-f", "lavfi", "-i", "sine=frequency=400:sample_rate=44100", "-t", "1", "-c:a", "pcm_s16le", path], st.Root, ct); } var folder = Path.Combine(st.Root, "r1-long", "attempt"); Directory.CreateDirectory(folder); Reject(() => NarrationFlow.Prepare(st, e, script, st.Settings, folder, services, CancellationToken.None, Speak).GetAwaiter().GetResult()); fail = false; var before = calls; NarrationFlow.Prepare(st, e, script, st.Settings, folder, services, CancellationToken.None, Speak).GetAwaiter().GetResult(); Check(calls - before == 1, "Sağlam ses bloğu yeniden üretildi"); });

        Test("S04", "Geç render hatası önceki sağlam videoyu korur", () => { var st = New("render-stage"); var e = Episode(st); e.Demo = true; var original = Reliability.FileHash(e.Videos[0].File); new Engine(st, stage => { if (stage == "encoded") throw new InvalidOperationException("Geç render hatası"); }).Produce(e).GetAwaiter().GetResult(); Check(e.Status == "Bekliyor" && e.Error.Contains("Geç render hatası") && Reliability.FileHash(e.Videos[0].File) == original, "Önceki video korunmadı veya geç hata denenmedi"); });

        Test("S05", "Önbellek yalnız üretim girdileri ve dosya içeriğiyle değişir", () => { var st = New("render-hash"); var e = Episode(st); var script = e.Plan!.Long!; var hash = Reliability.RenderHash(script, e, st.Settings); st.Settings.UiSounds = !st.Settings.UiSounds; Check(hash == Reliability.RenderHash(script, e, st.Settings), "Arayüz sesi renderı bozdu"); st.Settings.MusicPath = Path.Combine(st.Root, "music.wav"); File.WriteAllText(st.Settings.MusicPath, "first"); hash = Reliability.RenderHash(script, e, st.Settings); File.WriteAllText(st.Settings.MusicPath, "second"); Check(hash != Reliability.RenderHash(script, e, st.Settings), "Müzik içeriği dikkate alınmadı"); });

        Test("S06", "Teknik kalite hak onayı yerine geçmez", () => { var st = New("rights"); var e = Episode(st); e.Plan!.Long!.Scenes[0].RightsConfirmed = false; Reject(() => new Engine(st).Approve(e)); e.Plan.Long.Scenes[0].RightsConfirmed = true; new Engine(st).Approve(e); Check(e.Approved, "Hak teyidiyle onay verilemedi"); });

        Test("S07", "Boyut sınırı ve iptal uygulanır", () => { Reject(() => Reliability.CopyBounded(new MemoryStream(new byte[100]), new MemoryStream(), 10, CancellationToken.None).GetAwaiter().GetResult()); using var cancel = new CancellationTokenSource(); cancel.Cancel(); bool stopped = false; try { Reliability.CopyBounded(new MemoryStream(new byte[100]), new MemoryStream(), 200, cancel.Token).GetAwaiter().GetResult(); } catch (OperationCanceledException) { stopped = true; } Check(stopped, "İndirme iptal edilmedi"); });

        Test("S08", "Yerel tarih uzakta doğrulanan tarihten ayrıdır", () => { var e = new AssetVideo { Scheduled = true, RemotePublishAt = DateTimeOffset.UtcNow.AddDays(1) }; e.PublishAt = DateTimeOffset.UtcNow.AddDays(2); Check(e.Scheduled && e.RemotePublishAt != e.PublishAt, "Yerel ve uzak tarih karıştı"); });

        Test("S09", "İptal video alt sürecini bitirir", () => { using var cancel = new CancellationTokenSource(500); bool stopped = false; try { Media.Exec(ffmpeg, ["-re", "-f", "lavfi", "-i", "color=s=160x90", "-t", "60", "-f", "null", "-"], root, cancel.Token).GetAwaiter().GetResult(); } catch (OperationCanceledException) { stopped = true; } Check(stopped, "Alt süreç iptal edilmedi"); });

        Test("S10", "Groq sıkı şema ve çıktı bütçesi gönderilir", () => { var st = New("schema"); st.Settings.ProviderOrder = "Groq"; var old = Environment.GetEnvironmentVariable("GROQ_API_KEY"); try { Environment.SetEnvironmentVariable("GROQ_API_KEY", "fixture"); using var client = new HttpClient(new Handler(async req => { using var body = JsonDocument.Parse(await req.Content!.ReadAsStringAsync()); Check(body.RootElement.GetProperty("max_tokens").GetInt32() == 4096 && body.RootElement.GetProperty("response_format").GetProperty("json_schema").GetProperty("strict").GetBoolean(), "Şema/bütçe eksik"); return Ok("{\"choices\":[{\"message\":{\"content\":\"{\\\"Changes\\\":[]}\"}}]}"); })); new Services(st, _ => {}, client).Generate("Changes", "test", CancellationToken.None, _ => true, true).GetAwaiter().GetResult(); } finally { Environment.SetEnvironmentVariable("GROQ_API_KEY", old); } });

        Test("UI-PRODUCED", "Üretilen video kalite onayından ayrı gösterilir; eski ve eksik çıktı tamamlanmaz", () => {

            var st = New("produced-stage"); var e = Episode(st); e.Videos[0].QualityPassed = false;

            Check(Reliability.OutputsProduced(e), "Hazır çıktı üretim adımında tamamlanmadı");

            Check(!Reliability.OutputsCurrent(e, false), "Kalite kontrolü atlandı");

            e.TextDesignPending = true; Check(!Reliability.OutputsProduced(e), "Bekleyen düzen tamamlandı sayıldı");

            e.TextDesignPending = false; e.Plan!.Long!.Title = "Değişti"; Check(!Reliability.OutputsProduced(e), "Eski çıktı tamamlandı sayıldı");

            e.Videos[0].PlanHash = Reliability.MediaPlanHash(e, "long"); File.Delete(e.Videos[0].File); Check(!Reliability.OutputsProduced(e), "Eksik dosya tamamlandı sayıldı");

        });

        Test("S11", "Kalite geçmeyen video tamamlandı sayılmaz", () => { var st = New("quality"); var e = Episode(st); e.Videos[0].QualityPassed = false; Check(!Reliability.OutputsCurrent(e, false), "Kalite geçmeyen dosya yeşil sayıldı"); });

        Test("S12", "Seçili model testi başka sağlayıcının başarısını kullanamaz", () => { var st = New("preflight"); st.Settings.ProviderOrder = "Groq,OpenRouter"; st.PreferredProvider = "OpenRouter"; var old = Environment.GetEnvironmentVariable("GROQ_API_KEY"); try { Environment.SetEnvironmentVariable("GROQ_API_KEY", "fixture"); int calls = 0; using var client = new HttpClient(new Handler(req => { calls++; Check(req.RequestUri!.Host == "api.groq.com", "Yanlış sağlayıcı denendi"); return Task.FromResult(new HttpResponseMessage(HttpStatusCode.PaymentRequired) { Content = new StringContent("{}") }); })); Reject(() => new Services(st, _ => {}, client).VerifyModel("Groq", CancellationToken.None).GetAwaiter().GetResult()); Check(calls == 1, "Seçili model hatası alternatifle gizlendi"); } finally { Environment.SetEnvironmentVariable("GROQ_API_KEY", old); } });

        Test("A16-TICK", "Otomatik kuyruk süresi dolan geçici işi sürdürür", () => { var st = New("tick-resume"); st.Settings.AutoResearch = true; var pending = new Episode { Topic = "Kuyruk testi", Demo = true, ShortCount = 0, Status = "Bekliyor", ResumeStage = "Plan", RetryAt = DateTimeOffset.UtcNow.AddSeconds(-1) }; st.Episodes.Add(pending); var manual = new Episode { Topic = "Elle durdurulan", Demo = true, ShortCount = 0, Status = "Durduruldu", ResumeStage = "Plan", RetryAt = DateTimeOffset.UtcNow.AddSeconds(-1) }; st.Episodes.Add(manual); using var form = new StudioForm(st); ((Task)typeof(StudioForm).GetMethod("Tick", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(form, null)!).GetAwaiter().GetResult(); Check(pending.Status == "Senaryo hazır" && pending.Plan != null && manual.Status == "Durduruldu", "Geçici/elle durdurulan iş ayrılmadı"); });

        Test("S01-MODAL", "Modal editör sırasında otomatik kuyruk başlamaz", () => { var st = New("modal"); st.Settings.AutoResearch = true; var pending = new Episode { Topic = "Modal test", Demo = true, ShortCount = 0 }; st.Episodes.Add(pending); using var form = new StudioForm(st); using var dialog = new Form { Opacity = 0, ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new(-30000,-30000) }; dialog.Shown += (_, _) => { try { ((Task)typeof(StudioForm).GetMethod("Tick", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(form, null)!).GetAwaiter().GetResult(); } finally { dialog.Close(); } }; dialog.ShowDialog(form); Check(pending.Plan == null && pending.Status == "Taslak", "Modal editör sırasında üretim başladı"); });

        Test("A18-CONFLICT", "Aynı kaydın eşzamanlı düzenlemesi sessizce ezilmez", () => { var st = New("pool-conflict"); var a = new MediaPool(st); a.Save([new() { Id = "same", Tags = "ilk" }]); var b = new MediaPool(st); var first = a.List(); var second = b.List(); first[0].Tags = "A güncellemesi"; second[0].Tags = "B güncellemesi"; a.Save(first); Reject(() => b.Save(second)); Check(a.List()[0].Tags == "A güncellemesi", "İlk güncelleme kayboldu"); });

        Test("S13", "Silme kaydı başarısızsa paket ve dosyalar geri döner", () => { var st = New("delete"); var e = Episode(st); st.Save(); var folder = st.Folder(e); File.WriteAllText(Path.Combine(folder, "keep.txt"), "recoverable"); var state = Path.Combine(st.Root, "state.json"); File.Move(state, state + ".saved"); Directory.CreateDirectory(state); bool failedSave = false; try { PackageDeletion.Delete(st, e); } catch (IOException) { failedSave = true; } finally { Directory.Delete(state); File.Move(state + ".saved", state); } Check(failedSave && st.Episodes.Contains(e) && File.Exists(Path.Combine(folder, "keep.txt")), "Silme geri alınamadı"); PackageDeletion.Delete(st, e); Check(!st.Episodes.Contains(e) && !Directory.Exists(folder), "Başarılı silme tamamlanmadı"); });

        Test("S07-ATOMIC", "Sınırı aşan ve iptal edilen indirme eski dosyayı korur", () => { var target = Path.Combine(root, "download.bin"); File.WriteAllText(target, "original"); using var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new MemoryStream(new byte[100])) }; Reject(() => Reliability.DownloadResponse(response, target, 10, CancellationToken.None).GetAwaiter().GetResult()); Check(File.ReadAllText(target) == "original" && Directory.GetFiles(root, "*.part").Length == 0, "Eski dosya değişti veya parça kaldı"); using var cancel = new CancellationTokenSource(); cancel.Cancel(); using var next = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[5]) }; try { Reliability.DownloadResponse(next, target, 10, cancel.Token).GetAwaiter().GetResult(); } catch (OperationCanceledException) {} Check(File.ReadAllText(target) == "original" && Directory.GetFiles(root, "*.part").Length == 0, "İptal temizliği başarısız"); using var text = Ok(new string('x', 100)); Reject(() => Reliability.ReadText(text, 10, CancellationToken.None).GetAwaiter().GetResult()); });

        Test("A16-MIXED", "Bir sağlayıcının bakiye hatası diğerinin geçici tekrarını engellemez", () => { var e = new Episode { ResumeSchedule = false }; Reliability.Failed(e, new AiUnavailableException("Gemini HTTP 402; OpenRouter geçici bağlantı hatası", true), "Upload"); Check(e.RetryAt != null && e.ResumeSchedule == false, "Özel yükleme niyeti veya tekrar kayboldu"); var st = New("upload-intent"); st.Episodes.Add(e); st.Save(); Check(new Store(st.Root).Episodes[0].ResumeSchedule == false, "Yükleme niyeti kaydedilmedi"); });

        Test("A04-NULL", "Eksik veya null durum kaydı sağlam yedekten kurtarılır", () => { var st = New("null-state"); Episode(st); st.Save(); st.Save(); File.WriteAllText(Path.Combine(st.Root, "state.json"), "{\"SchemaVersion\":2,\"Settings\":null,\"Episodes\":null}"); Check(new Store(st.Root).Episodes.Count == 1, "Null kayıt yedeğe dönmedi"); });

        Test("AI-LENGTH", "Boş ve kesilmiş AI çıktısı artırılmış bütçeyle yeniden denenir", () => { var st = New("length-retry"); st.Settings.ProviderOrder = "OpenRouter"; var old = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY"); try { Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", "fixture"); int calls = 0; using var client = new HttpClient(new Handler(async req => { using var body = JsonDocument.Parse(await req.Content!.ReadAsStringAsync()); calls++; Check(body.RootElement.GetProperty("max_tokens").GetInt32() == (calls == 1 ? 4096 : 8192), "Çıktı bütçesi artırılmadı"); return Ok(calls == 1 ? "{\"choices\":[{\"finish_reason\":\"length\",\"message\":{\"content\":\"\"}}]}" : "{\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"content\":\"{}\"}}]}"); })); new Services(st, _ => {}, client).Generate("Changes", "test", CancellationToken.None, _ => true, true).GetAwaiter().GetResult(); Check(calls == 2, "Kesilmiş yanıt tekrar denenmedi"); } finally { Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", old); } });

        Test("PACKAGE-PROGRESS-TIME", "Süre ölçülen hızdan hesaplanır; yeni aşama, hata ve gerilemede eski tahmin kullanılmaz", () => {

            var estimate = new PackageTimeEstimate(); Check(estimate.Observe("Üretiliyor", 28, true, 0) == null, "İlk anda süre uyduruldu");

            var time = estimate.Observe("Üretiliyor", 48, true, 40); Check(time != null && Math.Abs(time.Value.TotalSeconds - 104) < .01, "Hızdan kalan süre yanlış");

            Check(estimate.Observe("Yükleniyor", 0, true, 45) == null && estimate.Observe("Yükleniyor", 1, true, 46) == null, "Yetersiz ölçümden tahmin üretildi");

            Check(estimate.Observe("Yükleniyor", 100, true, 60) == TimeSpan.Zero && estimate.Observe("Hata", 50, false, 61) == null && estimate.Observe("Üretiliyor", 28, true, 62) == null, "Tamamlanma/yeniden başlama hatalı");

            using var status = new AnimatedPackageStatus { Size = new Size(195,32), BackColor = Theme.Bg, Font = Theme.Font(10,true), TextAlign = ContentAlignment.MiddleRight }; status.UpdateStatus("Üretiliyor", false); int first = status.AnimationFrame;

            using var beforeImage = new Bitmap(195,32); status.DrawToBitmap(beforeImage,new Rectangle(Point.Empty,status.Size));

            for (int i=0;i<10;i++) status.AdvanceFrame();

            using var afterImage = new Bitmap(195,32); status.DrawToBitmap(afterImage,new Rectangle(Point.Empty,status.Size)); bool different = false; for(int x=0;x<195;x++) for(int y=0;y<32;y++) if(beforeImage.GetPixel(x,y)!=afterImage.GetPixel(x,y))different=true;

            Check(status.Animating && status.AnimationFrame != first && status.Text == "Üretiliyor" && different, "Harfler hareket etmiyor veya noktalar kaldı");

            status.UpdateStatus("Denetim bekliyor", false); var before = status.Text; first = status.AnimationFrame; status.AdvanceFrame(); Check(!status.Animating && status.Text == before && status.AnimationFrame == first, "Bittiğinde hareket sürüyor");

            status.UpdateStatus("Üretiliyor", true); Check(!status.Animating, "Hatada hareket sürüyor");

        });

        Test("UI-PROGRESS", "Yüz ilerleme güncellemesi beş adımlı paneli yeniden oluşturmaz",()=>{var st=New("ui-progress");var e=new Episode{Topic="Canlı ilerleme",WizardStep=2,Status="Üretiliyor",Progress=0};st.Episodes.Add(e);using var form=new StudioForm(st);var page=(FlowLayoutPanel)typeof(StudioForm).GetField("page",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(form)!;IEnumerable<Control> All(Control c)=>c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(All(x)));var bar=All(page).OfType<TurquoisePackageBar>().Single();var updates=(Dictionary<string,Action>)typeof(StudioForm).GetField("packageUpdates",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(form)!;for(int i=1;i<=100;i++){e.Progress=i;e.Log("İlerleme "+i);updates[e.Id]();Check(!bar.IsDisposed&&All(page).Contains(bar)&&bar.Value==i,"İlerleme paneli yeniden oluşturuldu.");}});

        Test("CARD-MIGRATION", "Eski kartlar renk ve yerleşimini korur", () => { var d=new CardDesign{Font="Arial",TitleSize=91,BodyY=51,Foreground="#123456",Border=false};d.Upgrade();Check(d.TitleStyle.Size==91&&d.BodyStyle.Y==51&&d.FooterStyle.Color=="#123456"&&d.Frame=="Çerçevesiz","Eski tasarım değişti"); });

        Test("CARD-LAYERS", "Bağımsız metin efektleri kayıt sonrası korunur; şeffaf PNG çizilir", () => { var d=new CardDesign();d.Upgrade();d.Frame="Çerçevesiz";d.Transparent=true;d.BodyStyle.Font="missing-font-fixture";d.BodyStyle.Outline=3;d.BodyStyle.Shadow=7;d.BodyStyle.X=14;d.BodyStyle.Rotation=10;var path=Path.Combine(root,"card-rich.json");Json.Write(path,d);var copy=Json.Read(path,new CardDesign());Check(copy.BodyStyle.X==14&&copy.BodyStyle.Shadow==7&&copy.TitleStyle.Shadow==0,"Katman ayarı kayboldu");using var image=CardDesign.Draw(copy);Check(image.GetPixel(0,0).A==0,"Şeffaflık kayboldu"); });

        Test("CARD-BLANK-IMPORT", "Yeni kart boş açılır; hazır kapak yazısız ve karartılmadan saklanır", () => {var st=New("card-import");using var editor=new CardEditor(st);var field=typeof(CardEditor).GetField("design",BindingFlags.NonPublic|BindingFlags.Instance)!;var blank=(CardDesign)field.GetValue(editor)!;Check(blank.Title+blank.Body+blank.Footer==""&&blank.Frame=="Çerçevesiz"&&blank.BackgroundImage=="","Editör boş değil");var source=Path.Combine(st.Root,"external.png");using(var original=new System.Drawing.Bitmap(321,213)){using(var g=System.Drawing.Graphics.FromImage(original))g.Clear(System.Drawing.Color.Coral);original.Save(source,System.Drawing.Imaging.ImageFormat.Png);}editor.LoadCover(source);typeof(CardEditor).GetMethod("Save",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(editor,null);using var saved=System.Drawing.Image.FromFile(editor.SavedPath);using var bitmap=new System.Drawing.Bitmap(saved);Check(bitmap.Width==321&&bitmap.Height==213&&bitmap.GetPixel(160,106).ToArgb()==System.Drawing.Color.Coral.ToArgb(),"Kapak boyutu veya rengi değişti");using var reopened=new CardEditor(st,editor.SavedPath);var d=(CardDesign)field.GetValue(reopened)!;Check(d.Title+d.Body+d.Footer==""&&d.ImageShade==0&&File.Exists(d.BackgroundImage),"İçe aktarılan kapak yeniden açılamadı");});

        Test("CARD-EDIT-SAVE", "Kart editörü düzenlenebilir tasarımı saklar ve yeni kopya üretir", () => { var st = New("card-edit"); using var editor = new CardEditor(st); var draft=(CardDesign)typeof(CardEditor).GetField("design",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(editor)!;draft.Title="Test kartı"; typeof(CardEditor).GetMethod("Save", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(editor, null); Check(File.Exists(editor.SavedPath) && File.Exists(Path.ChangeExtension(editor.SavedPath, ".json")), "Kart veya tasarım eksik"); using var copy = new CardEditor(st, editor.SavedPath); typeof(CardEditor).GetMethod("Save", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(copy, null); Check(copy.SavedPath != editor.SavedPath && File.Exists(editor.SavedPath), "Özgün kart ezildi"); var d = Json.Read(Path.ChangeExtension(copy.SavedPath, ".json"), new CardDesign()); d.Vertical = true; using var image = CardDesign.Draw(d); Check(image.Width == 1080 && image.Height == 1920, "Dikey kart yanlış boyutta"); });

        Test("CARD-VIDEO-MONTAGE", "Açılış başta, kapanış sonda MP4 içine eklenir; ana videonun süresi korunur",()=> {

            var st=New("card-video-montage");string opening=Path.Combine(st.Root,"opening.png"),closing=Path.Combine(st.Root,"closing.png"),output=Path.Combine(st.Root,"video.mp4");

            foreach(var item in new[]{(opening,Color.Red),(closing,Color.Green)}){using var image=new Bitmap(160,90);using var graphics=Graphics.FromImage(image);graphics.Clear(item.Item2);image.Save(item.Item1);}

            Media.Exec(ffmpeg,["-y","-f","lavfi","-i","color=c=blue:s=160x90:r=10","-f","lavfi","-i","sine=frequency=440:sample_rate=44100","-t","3","-c:v","libx264","-pix_fmt","yuv420p","-c:a","aac",output],st.Root,CancellationToken.None).GetAwaiter().GetResult();

            var settings=new Settings{ClosingCardFile=opening,ClosingCardSeconds=1,FramesPerSecond=10,EncodingPreset="ultrafast"};

            ClosingCards.Append(st,settings,output,st.Root,160,90,CancellationToken.None,prepend:true).GetAwaiter().GetResult();settings.ClosingCardFile=closing;

            ClosingCards.Append(st,settings,output,st.Root,160,90,CancellationToken.None).GetAwaiter().GetResult();

            Check(Math.Abs(Media.Duration(st,output,CancellationToken.None).GetAwaiter().GetResult()-5)<.3,"Kart veya ana video süresi değişti");

            foreach(var point in new[]{(.5,0),(2.5,1),(4.5,2)}){string frame=Path.Combine(st.Root,"frame-"+point.Item2+".png");Media.Exec(ffmpeg,["-y","-ss",Media.Num(point.Item1),"-i",output,"-frames:v","1",frame],st.Root,CancellationToken.None).GetAwaiter().GetResult();using var image=new Bitmap(frame);var c=image.GetPixel(80,45);Check(point.Item2==0?c.R>c.G+100:point.Item2==1?c.B>c.R+100:c.G>c.R+60,"Açılış/ana video/kapanış sırası yanlış");}

        });

        Test("CARD-PREFLIGHT", "Eksik kart veya geçersiz süre üretim başlamadan engellenir", () => { var settings = new Settings { OpeningCardEnabled = true, OpeningCardFile = Path.Combine(root, "missing.png") }; Reject(() => ClosingCards.Validate(settings)); settings.OpeningCardEnabled = false; settings.ClosingCardEnabled = true; settings.ClosingCardFile = Path.Combine(AppContext.BaseDirectory, "Assets", "nur-kapisi-destek-gece.png"); settings.ClosingCardSeconds = double.NaN; Reject(() => ClosingCards.Validate(settings)); });

        Test("EDITOR-ALL-SCENES", "Toplu zamanlama metin ve sahne görüntülerini korur", () => { var first = new Scene { Narration = "Birinci metin", OnScreen = "Birinci başlık", StockFile = "first.mp4", TitleStart = 1.25, TitleDuration = 2.5 }; var second = new Scene { Narration = "İkinci metin", OnScreen = "İkinci başlık", StockFile = "second.mp4", TitleStart = 0, TitleDuration = 4 }; var script = new VideoScript { Scenes = [first, second] }; TextEditor.ApplyAllScenes(script, first); Check(second.TitleStart == 1.25 && second.TitleDuration == 2.5, "Zamanlar aktarılmadı"); Check(second.Narration == "İkinci metin" && second.OnScreen == "İkinci başlık" && second.StockFile == "second.mp4", "Sahne içeriği değişti"); });

        Test("SHORT-DRAFT-REPAIR", "82 kelimelik ilk yanıt reddedilmez; program alanları temizlenir ve 130 kelimeye tamamlanır", () => {

            var st = New("short-draft-flow"); st.Settings.LocalAiFirst = true; st.Settings.CloudAiEnabled = false;

            var e = new Episode { IncludeLong = false, ShortCount = 1, ShortSeconds = 60 };

            var plan = new ContentPlan { Shorts = [new VideoScript { Title = "Sabır", Scenes = [new Scene { Narration = string.Join(" ", Enumerable.Repeat("sabır", 82)), VisualQuery = "forest" }] }] };

            var node = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(plan, Json.Options))!;

            node["Shorts"]![0]!["MusicTrack"] = "modelin yanlış müzik alanı"; node["Shorts"]![0]!["TextLayout"] = "modelin yanlış tasarım alanı";

            var text = node.ToJsonString(); Services.ValidateGeneratedPlan(text, e, Plan().Sources);

            e.Plan = Services.ParseGeneratedPlan(text); e.Plan.Sources = Plan().Sources; st.Episodes.Add(e);

            Check(e.Plan.Shorts[0].MusicTrack == null && e.Plan.Shorts[0].TextLayout == null, "Model program ayarlarını belirledi");

            int calls = 0;

            using var client = new HttpClient(new Handler(req => {

                if (req.RequestUri!.AbsolutePath.EndsWith("/show")) return Task.FromResult(Ok("{}")); calls++;

                object reply = calls == 1 ? new { Narrations = new[] { string.Join(" ", Enumerable.Repeat("sabır", 82)) } } : (object)new { Continuation = string.Join(" ", Enumerable.Repeat("umut", 48)) };

                return Task.FromResult(Ok(JsonSerializer.Serialize(new { message = new { content = JsonSerializer.Serialize(reply) }, done = true })));

            }));

            new Services(st, _ => {}, client).EnsureScriptLength(e, CancellationToken.None).GetAwaiter().GetResult();

            Check(calls == 2 && ScriptLength.Words(e.Plan.Shorts[0]) == 130 && ScriptLength.Problem(e, e.Plan) == null, "Kısa ilk plan tamamlama aşamasına geçemedi");

            var saved = new Store(st.Root).Episodes.Single(); Check(ScriptLength.Words(saved.Plan!.Shorts[0]) == 130, "Tamamlanan plan saklanmadı");

        });

        Test("PLAN-DUPLICATES", "Tekrarlanan JSON alanları açıklanır ve düzeltme isteğine geçebilir", () => {

            foreach (var text in new[] { "{\"Sources\":[],\"Sources\":[]}", "{\"Shorts\":[{\"Title\":\"a\",\"title\":\"b\"}]}" }) {

                try { Services.ParseGeneratedPlan(text); throw new Exception("Tekrarlanan alan kabul edildi"); }

                catch (AiContentException ex) { Check(ex.Message.Contains("JSON alanı tekrarlandı"), "Hata açıklaması eksik"); }

            }

        });

        Test("PLAN-SOURCES", "Modelin hatalı kaynak biçimi atılır; doğrulanmış kaynaklar kullanılır", () => {

            var e = new Episode { IncludeLong = false, ShortCount = 1, ShortSeconds = 60 };

            var plan = new ContentPlan { Shorts = [new VideoScript { Title = "Sabır", Scenes = [new Scene { Narration = string.Join(" ", Enumerable.Repeat("sabır", 130)), VisualQuery = "forest" }] }] };

            var node = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(plan, Json.Options))!;

            node["Sources"] = new System.Text.Json.Nodes.JsonArray("uydurma kaynak");

            var text = node.ToJsonString(); var sources = Plan().Sources;

            Services.ValidateGeneratedPlan(text, e, sources);

            var parsed = Services.ParseGeneratedPlan(text); Check(parsed.Sources.Count == 0 && parsed.Shorts.Count == 1, "Model kaynakları plana taşındı");

        });

        Test("PROVIDER-CHOICE", "Açık sağlayıcı sırası yerel öncelik ve önceki kazanan tarafından değiştirilemez", () => {

            var settings = new Settings { ProviderOrder = "Gemini", LocalAiFirst = true, UseOllama = true };

            Check(Services.ProviderSequence(settings, null, "Ollama").SequenceEqual(new[] { "Gemini" }), "Ollama seçilmediği halde eklendi");

            settings.ProviderOrder = "Gemini,Groq"; settings.LocalAiFirst = false;

            Check(Services.ProviderSequence(settings, null, "Groq").SequenceEqual(new[] { "Gemini", "Groq" }), "Önceki kazanan açık sırayı değiştirdi");

            settings.ProviderOrder = "Gemini,Ollama"; settings.LocalAiFirst = true;

            Check(Services.ProviderSequence(settings, null, "Gemini").First() == "Ollama", "Seçili yerel öncelik uygulanmadı");

        });

        Test("LOCAL-DEFAULT", "Yeni ve taşınan ayarlarda yerel AI önce gelir; bulut izinleri kapalıdır", () => { var st = new Store(Path.Combine(root, "local-default")); Check(st.Settings.UseOllama && st.Settings.LocalAiFirst && !st.Settings.CloudAiEnabled && st.Settings.EnabledCloudProviders == "", "Yerel varsayılanlar yanlış"); Check(Services.ProviderSequence(st.Settings, null, "OpenRouter").First() == "Ollama", "Kazanan bulut yerel önceliği ezdi"); st.Settings.CloudAiEnabled = true; st.Settings.EnabledCloudProviders = "Groq"; st.Save(); Check(new Store(st.Root).Settings.EnabledCloudProviders == "Groq", "Kaydedilen izin yeniden sıfırlandı"); });

        Test("LOCAL-CLOUD-OFF", "Kapalı bulut AI anahtar bulunsa da istek göndermez", () => { var st = New("cloud-off"); st.Settings.UseOllama = false; st.Settings.CloudAiEnabled = false; st.Settings.ProviderOrder = "OpenRouter"; var old = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY"); try { Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", "fixture"); int calls = 0; using var client = new HttpClient(new Handler(req => { calls++; return Task.FromResult(Ok("{}")); })); Reject(() => new Services(st, _ => {}, client).Generate("JSON", "test", CancellationToken.None).GetAwaiter().GetResult()); Check(calls == 0, "İzin kapalıyken bulut çağrıldı"); } finally { Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", old); } });

        Test("LOCAL-NATIVE", "Yerel model düşünmeden sıkı şema ve ayarlı bütçeyle yanıt verir", () => { var st = New("native"); st.Settings.ProviderOrder = "Ollama"; st.Settings.LocalAiFirst = true; int chats = 0; using var client = new HttpClient(new Handler(async req => { if (req.RequestUri!.AbsolutePath == "/api/show") return Ok("{}"); Check(req.RequestUri.AbsolutePath == "/api/chat", "Yerel API kullanılmadı"); using var doc = JsonDocument.Parse(await req.Content!.ReadAsStringAsync()); var body = doc.RootElement; Check(!body.GetProperty("think").GetBoolean() && !body.GetProperty("stream").GetBoolean() && body.GetProperty("format").GetProperty("properties").TryGetProperty("Changes", out _) && body.GetProperty("options").GetProperty("num_predict").GetInt32() == 4096, "Yerel ayarlar eksik"); chats++; return Ok("{\"done\":true,\"done_reason\":\"stop\",\"message\":{\"content\":\"{\\\"Changes\\\":[]}\"}}"); })); var response = new Services(st, _ => {}, client).Generate("Changes", "test", CancellationToken.None, _ => true, true).GetAwaiter().GetResult(); Check(response.Contains("Changes") && chats == 1, "Yerel içerik okunamadı"); });

        Test("LOCAL-REMOTE-ALIAS", "Yerel adı altında bulut modeline istek gönderilmez", () => { var st = New("alias"); using var client = new HttpClient(new Handler(req => Task.FromResult(Ok("{\"remote_model\":\"cloud-provider\"}")))); Reject(() => new LocalAi(st, client).EnsureLocal("alias:latest", CancellationToken.None).GetAwaiter().GetResult()); Check(!LocalAi.IsLocalName("qwen3:cloud") && !LocalAi.IsLocalName("gpt-oss:120b-cloud"), "Bulut model adı kabul edildi"); Reject(() => LocalAi.ValidateModel("unknown:huge")); });

        Test("LOCAL-DIGEST", "Motor arşivinde resmi SHA doğrulanır", () => { var path = Path.Combine(root, "digest.bin"); File.WriteAllText(path, "verified"); LocalAi.ValidateDigest(path, "sha256:" + Reliability.FileHash(path).ToLowerInvariant()); Reject(() => LocalAi.ValidateDigest(path, "sha256:" + new string('0', 64))); });

        Test("LOCAL-ZIP", "Motor arşivi klasör dışına veya alternatif akışa yazamaz", () => { foreach (var entry in new[] { "../outside.txt", "file.txt:stream" }) { var zip = Path.Combine(root, Guid.NewGuid() + ".zip"); using (var archive = System.IO.Compression.ZipFile.Open(zip, System.IO.Compression.ZipArchiveMode.Create)) { using var writer = new StreamWriter(archive.CreateEntry(entry).Open()); writer.Write("invalid"); } Reject(() => LocalAi.ExtractRuntime(zip, Path.Combine(root, "unpack"), CancellationToken.None)); } Check(!File.Exists(Path.Combine(root, "outside.txt")), "Klasör dışına yazıldı"); });

        Test("LOCAL-ALREADY", "Kurulu model tekrar indirilmez; bulut etiketleri listelenmez", () => { var st = New("already"); int pulls = 0; using var client = new HttpClient(new Handler(req => { if (req.RequestUri!.AbsolutePath == "/api/pull") pulls++; return Task.FromResult(Ok("{\"models\":[{\"name\":\"qwen3:4b\"},{\"name\":\"qwen3:cloud\"}]}")); })); var local = new LocalAi(st, client); var state = local.Status(CancellationToken.None).GetAwaiter().GetResult(); Check(state.Running && state.Models.SequenceEqual(new[] { "qwen3:4b" }), "Bulut modeli listelendi"); local.Pull("qwen3:4b", new InlineProgress(), CancellationToken.None).GetAwaiter().GetResult(); Check(pulls == 0, "Kurulu model yeniden indirildi"); });

        Test("CHARACTER-REFERENCE-CATALOG", "Onaylı karakter uygun yaş/üslup için seçilir; farklı yaş, tarz ve çoklu kadroya uygulanmaz",()=>{
            var st=New("character-reference-catalog");var references=CharacterReferences.List(st);Check(references.Any(x=>x.Id=="elderly-man-cartoon-v1"),"Paket karakter referansı eksik");
            Check(CharacterReferences.Match(references,"A friendly elderly man with white beard","3D cartoon",1)!=null,"Yaşlı adam seçilmedi");Check(CharacterReferences.Match(references,"An elderly woman","3D cartoon",1)==null&&CharacterReferences.Match(references,"An elderly man","photorealistic",1)==null&&CharacterReferences.Match(references,"An elderly man and a child","3D cartoon",2)==null,"Yanlış kişiye veya stile referans atandı");
            var episode=new Episode{Topic="Yaşlı adamın hikâyesi",AiStoryStyle="3D cartoon",Creation=new CreationFlow()};CharacterReferences.AutoSelect(st,episode,"An elderly man",1,_=>{});Check(episode.Creation.SceneReferenceCatalogId=="elderly-man-cartoon-v1"&&File.Exists(episode.Creation.SceneReferenceFile),"Referans hikâyeye kopyalanmadı");var file=episode.Creation.SceneReferenceFile;CharacterReferences.AutoSelect(st,episode,"An elderly man",1,_=>{});Check(episode.Creation.SceneReferenceFile==file,"Aynı karakter yeniden seçildi");episode.Creation.SceneReferenceCatalogId="";CharacterReferences.AutoSelect(st,episode,"An elderly woman",1,_=>{});Check(episode.Creation.SceneReferenceFile==file,"Elle seçilmiş referans değiştirildi");
        });
        Test("LTX23-REFERENCE-GRAPH", "LTX 2.3 ilk seçenek; referans üretime bağlı, 5 saniye 24 FPS ayarı korunur",()=>{
            var graph=ImportedLtx23.Graph("new-reference.png","A person walks slowly in the reference location.",123);
            Check(new Settings().LocalVideoModel==AnimationVideo.Model&&ImportedLtx23.Choices[0]==AnimationVideo.Model,"LTX 2.5 ilk seçenek değil");
            Check(graph["2004"]!["inputs"]!["image"]!.GetValue<string>()=="new-reference.png"&&graph["2483"]!["inputs"]!["text"]!.GetValue<string>().StartsWith("A person"),"Eski referans veya prompt kaldı");
            Check(graph["4977"]!["inputs"]!["value"]!.GetValue<bool>()==false&&graph["3159"]!["inputs"]!["strength"]!.GetValue<double>()==1,"Referans atlanıyor veya zayıflatılıyor");
            Check(graph["4528"]!["inputs"]!["video_latent"]![0]!.GetValue<string>()=="3159","Referans sampler zincirine girmiyor");
            Check(graph["3059"]!["inputs"]!["width"]!.GetValue<int>()==768&&graph["3059"]!["inputs"]!["height"]!.GetValue<int>()==512&&graph["4979"]!["inputs"]!["value"]!.GetValue<int>()==121&&graph["4978"]!["inputs"]!["value"]!.GetValue<int>()==24,"Sabit üretim ayarı değişti");
            var smaller=ImportedLtx23.Graph("reference.png","A stylized cartoon man laughs happily.",42,736,480);Check(smaller["3059"]!["inputs"]!["height"]!.GetValue<int>()==480&&!smaller["2612"]!["inputs"]!["text"]!.GetValue<string>().Contains("childish"),"480 ayarı veya cartoon negatif promptu yanlış");
            Check(graph.Count(x=>x.Value!["class_type"]!.GetValue<string>()=="SaveVideo")==1&&!graph.Any(x=>x.Value!["class_type"]!.GetValue<string>().Contains("GemmaAPI")),"İkinci üretim veya bulut/API düğümü kaldı");
            var visited=new HashSet<string>();void Visit(string id){if(!visited.Add(id))return;foreach(var input in graph[id]!["inputs"]!.AsObject())if(input.Value is System.Text.Json.Nodes.JsonArray link&&link.Count==2&&link[0] is System.Text.Json.Nodes.JsonValue value&&value.TryGetValue<string>(out var parent))Visit(parent);}
            Visit("4852");Check(visited.Contains("3159")&&visited.Contains("2004")&&visited.Count==graph.Count,"Çıktıya bağlı olmayan düğüm kaldı");
        });
        Test("MANUAL-EXTRA-TRACKS", "Ek görüntü, ses ve altyazı satırları gerçek videoya işlenir ve kayıtta korunur",()=>{
            var st=New("manual-extra-tracks");string output=Path.Combine(st.Root,"base.mp4"),image=Path.Combine(st.Root,"green.png"),tone=Path.Combine(st.Root,"tone.wav");
            Media.Exec(ffmpeg,["-y","-f","lavfi","-i","color=c=red:s=320x240:r=24:d=2","-f","lavfi","-i","sine=frequency=200:duration=2","-af","volume=0.02","-c:v","libx264","-pix_fmt","yuv420p","-c:a","aac","-shortest",output],st.Root,CancellationToken.None).GetAwaiter().GetResult();
            using(var bitmap=new Bitmap(320,240)){using var g=Graphics.FromImage(bitmap);g.Clear(Color.Lime);bitmap.Save(image);}
            Media.Exec(ffmpeg,["-y","-f","lavfi","-i","sine=frequency=1000:duration=2",tone],st.Root,CancellationToken.None).GetAwaiter().GetResult();
            var script=new VideoScript{Scenes=[new Scene{Duration=2}],ExtraTracks=[new TimelineLayer{Kind="Görüntü",Clips=[new TimelineClip{File=image,Start=.4,Duration=.6,RightsConfirmed=true}]},new TimelineLayer{Kind="Ses",Clips=[new TimelineClip{File=tone,Start=.4,Duration=.6,Volume=.8,RightsConfirmed=true}]},new TimelineLayer{Kind="Altyazı",Clips=[new TimelineClip{Text="Elle eklenen metin",Start=1.2,Duration=.5}]}]};
            TimelineLayers.Apply(st,script,output,2,320,240,new Settings{EncodingPreset="ultrafast",Crf=20},CancellationToken.None).GetAwaiter().GetResult();
            Color Pixel(double second){string file=Path.Combine(st.Root,"frame-"+Media.Num(second)+".png");Media.Exec(ffmpeg,["-y","-ss",Media.Num(second),"-i",output,"-frames:v","1",file],st.Root,CancellationToken.None).GetAwaiter().GetResult();using var bitmap=new Bitmap(file);return bitmap.GetPixel(160,100);}
            var inside=Pixel(.7);var outside=Pixel(1.1);Check(inside.G>inside.R+80&&outside.R>outside.G+80,"Görüntü klibi zaman aralığı dışında görünüyor veya uygulanmadı");
            double Energy(double second){string file=Path.Combine(st.Root,"audio-"+Media.Num(second)+".raw");Media.Exec(ffmpeg,["-y","-ss",Media.Num(second),"-i",output,"-t","0.1","-vn","-ac","1","-ar","8000","-f","f32le",file],st.Root,CancellationToken.None).GetAwaiter().GetResult();var bytes=File.ReadAllBytes(file);double total=0;for(int i=0;i+3<bytes.Length;i+=4)total+=Math.Pow(BitConverter.ToSingle(bytes,i),2);return total/Math.Max(1,bytes.Length/4);}
            Check(Energy(.7)>Energy(1.1)*10,"Ek ses doğru zamanda mikslenmedi");
            string caption=Path.Combine(st.Root,"caption.png");Media.Exec(ffmpeg,["-y","-ss","1.4","-i",output,"-frames:v","1",caption],st.Root,CancellationToken.None).GetAwaiter().GetResult();using(var bitmap=new Bitmap(caption)){int white=0;for(int y=100;y<240;y++)for(int x=0;x<320;x++){var pixel=bitmap.GetPixel(x,y);if(pixel.R>180&&pixel.G>180&&pixel.B>180)white++;}Check(white>20,"Ek altyazı işlenmedi");}
            var cut=new VideoScript{ExtraTracks=[new TimelineLayer{Kind="Ses",Clips=[new TimelineClip{Start=0,Duration=5,SourceOffset=2}]}]};TimelineLayers.Cut(cut,1,3);Check(cut.ExtraTracks[0].Clips.Count==2&&cut.ExtraTracks[0].Clips[1].Start==1&&cut.ExtraTracks[0].Clips[1].SourceOffset==5,"Kesilen zaman ek satırlara uygulanmadı");
            var copy=CreationFlow.Copy(script);Check(copy.ExtraTracks?.Count==3&&copy.ExtraTracks[0].Clips[0].File==image,"Ek satırlar kayboldu");
            using var timeline=new WorkflowTimeline(script);Check(timeline.RequiredHeight==570&&timeline.MusicRowTop+60<324,"Timeline satırları veya alt bilgi üst üste biniyor");
            int add=0;timeline.AddTrackRequested+=()=>add++;typeof(Control).GetMethod("OnMouseDown",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(timeline,[new MouseEventArgs(MouseButtons.Left,1,135,12,0)]);Check(add==1,"Sol + satır ekleme olayı çalışmadı");
        });
        Test("GLASS-SLIDER", "Neon sürgü görünümü değeri ve klavye kontrolünü korur",()=>{
            using var form=new Form{Width=450,Height=150};using var slider=new StudioSlider{Width=350,Height=34,Minimum=0,Maximum=100,Value=40,Location=new Point(20,20)};form.Controls.Add(slider);form.Show();Application.DoEvents();slider.Focus();int before=slider.Value;SendHelpMessage(slider.Handle,0x100,(IntPtr)Keys.Right,IntPtr.Zero);SendHelpMessage(slider.Handle,0x101,(IntPtr)Keys.Right,IntPtr.Zero);Application.DoEvents();Check(slider.Value>before,"Sürgü klavye denetimi bozuldu");
            using var image=new Bitmap(slider.Width,slider.Height);slider.DrawToBitmap(image,new Rectangle(Point.Empty,slider.Size));var colors=new HashSet<int>();for(int x=12;x<slider.Width-12;x++)colors.Add(image.GetPixel(x,17).ToArgb());Check(colors.Count>12,"Neon dolgu ve camsı başlık çizilmedi");image.Save(Path.Combine(root,"neon-slider.png"));
            slider.Value=0;slider.DrawToBitmap(image,new Rectangle(Point.Empty,slider.Size));slider.Value=100;slider.DrawToBitmap(image,new Rectangle(Point.Empty,slider.Size));
        });
        Test("HOVER-CAPTION", "Taşan yazı fare üzerinde kayar; düğme sabit kalır ve çıkışta başa döner",()=>{
            using var form=new Form{Size=new Size(600,260)};
            Button[] buttons=[new SidebarButton{Text="Tek bir ısırığın doğadaki büyük etkileri ve bilinmeyen ayrıntıları",Width=248},Theme.Button("Uzun işlem düğmesi bütün metni kaydırarak gösterecek",()=>{},false,240),Theme.Button("Kaydet",()=>{},false,240)];
            int y=10;foreach(var button in buttons){button.Location=new Point(10,y);form.Controls.Add(button);y+=48;}form.Show();Application.DoEvents();
            foreach(var button in buttons){var bounds=button.Bounds;string text=button.Text;var field=button.GetType().GetField("caption",BindingFlags.Instance|BindingFlags.NonPublic)!;var caption=(HoverCaption)field.GetValue(button)!;
                typeof(Control).GetMethod("OnMouseEnter",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(button,[EventArgs.Empty]);using(var image=new Bitmap(button.Width,button.Height))button.DrawToBitmap(image,new Rectangle(Point.Empty,button.Size));
                var until=DateTime.UtcNow.AddMilliseconds(1050);while(DateTime.UtcNow<until){Application.DoEvents();Thread.Sleep(10);}Check(button.Text==text&&button.Bounds==bounds,"Kayan yazı düğmeyi değiştirdi");Check(button.Text=="Kaydet"?caption.Offset==0:caption.Offset>0,"Taşma veya kayan yazı davranışı yanlış: "+button.Text);
                typeof(Control).GetMethod("OnMouseLeave",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(button,[EventArgs.Empty]);Check(caption.Offset==0,"Fare çıkışında başa dönmedi");}
        });
        Test("COMPACT-CONNECTION-BUTTONS", "Bağlantı işlemleri sabit eşit boyut ve boşlukla yerleşir",()=>{
            using var form=new StudioForm(New("compact-connection-buttons"));form.Size=new Size(1366,900);form.Show();Application.DoEvents();typeof(StudioForm).GetMethod("Navigate",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(form,["Bağlantı Merkezi"]);Application.DoEvents();
            IEnumerable<Control> All(Control c)=>c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(All(x)));
            var group=All(form).OfType<StudioDisclosure>().Single(x=>x.Controls.OfType<Button>().Any(b=>b.AccessibleName=="Yerel motorlar bölümünü aç veya kapat"));group.Controls.OfType<Button>().First().PerformClick();Application.DoEvents();var textEngine=All(group).OfType<StudioDisclosure>().Single(x=>x.Controls.OfType<Button>().Any(b=>b.Text=="Metin motoru • Ollama"));textEngine.SetExpanded(true);Application.DoEvents();
            var buttons=All(group).OfType<StudioButton>().Where(b=>Equals(b.Tag,"connection-action")).ToArray();Check(buttons.Length>=6,"İşlem düğmeleri eksik");
            foreach(int width in new[]{1366,1100}){form.Width=width;Application.DoEvents();foreach(var b in buttons){Check(b.Width<=240&&b.Height==36&&b.Margin==new Padding(0,0,8,8),"Düğme ölçüsü veya boşluğu değişti: "+b.Text);if(b.Visible&&b.Parent is FlowLayoutPanel parent)Check(b.Bottom<=parent.ClientSize.Height+2,"Düğme satırı kırpıldı: "+b.Text);}}
            using var image=new Bitmap(form.Width,form.Height);form.DrawToBitmap(image,new Rectangle(Point.Empty,form.Size));image.Save(Path.Combine(root,"compact-connections.png"));
        });
        Test("LOCAL-TABS", "Metin ve görsel yerel motorları aynı merkezde; bulut ayrı gruptadır",()=>{using var form=new StudioForm(New("local-tabs"));typeof(StudioForm).GetMethod("Navigate",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(form,["Bağlantı Merkezi"]);IEnumerable<Control> All(Control c)=>c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(All(x)));var buttons=All(form).OfType<Button>().ToArray();Check(buttons.Any(b=>b.AccessibleName=="Yerel motorlar bölümünü aç veya kapat")&&buttons.Any(b=>b.AccessibleName=="Bulut yapay zekâ bölümünü aç veya kapat")&&buttons.Any(b=>b.Text=="Yerel motoru yükle"),"Yerel/bulut grupları eksik.");});



        Test("FIX-024-POOL", "Değiştirilen havuz dosyasının eski hakları ve kaynağı aktarılmaz", () => {

            var st=New("replacement");var pool=new MediaPool(st);var item=pool.Add(video,"sea","Genel","https://example.com/original","Old creator","CC BY","Old credit");

            var folder=Path.Combine(st.Root,"target");Directory.CreateDirectory(folder);var original=new Scene();Check(pool.TryUse(original,"sea",folder,[],true),"Özgün dosya seçilemedi");Check(original.RightsConfirmed&&original.Attribution=="Old credit","Özgün onay kayboldu");

            var replacement=Path.Combine(st.Root,"red.mp4");Media.Exec(ffmpeg,["-y","-f","lavfi","-i","color=c=red:s=160x90:r=10","-t","1","-c:v","libx264","-pix_fmt","yuv420p",replacement],st.Root,CancellationToken.None).GetAwaiter().GetResult();File.Copy(replacement,item.File,true);

            var scene=new Scene();Check(pool.TryUse(scene,"sea",folder,[],true),"Yeni dosya seçilemedi");Check(!scene.RightsConfirmed&&scene.Attribution==""&&scene.StockUrl!="https://example.com/original","Eski hak bilgisi aktarıldı");var saved=pool.List().Single();Check(!saved.RightsConfirmed&&saved.Hash==Reliability.FileHash(replacement),"Güncellenen onay kalıcı kaydedilmedi");

        });

        Test("FIX-024-CLOUD", "GitHub ön kontrolü boş, izinsiz ve ücret engelli AI sırasını reddeder", () => {

            var settings=new Settings{CloudAiEnabled=true,EnabledCloudProviders="OpenRouter",ProviderOrder="Ollama",VoiceProvider="ElevenLabs",OpeningCardEnabled=false,ClosingCardEnabled=false,VideoLogoEnabled=false,CoverBackground="",CoverLogo="",CoverMode="Şablon"};var e=new Episode{ShortCount=0};SettingsValidation.Check(settings);Reject(()=>GitHub.CheckRemoteVoice(settings,e));

            settings.ProviderOrder="Ollama,Groq";Reject(()=>GitHub.CheckRemoteVoice(settings,e));settings.ProviderOrder="Ollama,OpenRouter";GitHub.CheckRemoteVoice(settings,e);

            settings.ProviderOrder="OpenAI";settings.EnabledCloudProviders="OpenAI";settings.FreeOnly=true;Reject(()=>GitHub.CheckRemoteVoice(settings,e));settings.FreeOnly=false;GitHub.CheckRemoteVoice(settings,e);

            settings.ProviderOrder="Kimi";settings.EnabledCloudProviders="Kimi";settings.UseKimi=false;Reject(()=>GitHub.CheckRemoteVoice(settings,e));settings.UseKimi=true;GitHub.CheckRemoteVoice(settings,e);

            settings.ProviderOrder="OpenRouter";settings.EnabledCloudProviders="OpenRouter";settings.FreeOnly=true;settings.OpenRouterModel="paid/model";Reject(()=>GitHub.CheckRemoteVoice(settings,e));settings.OpenRouterModel="provider/model:free";GitHub.CheckRemoteVoice(settings,e);

        });

        Test("FIX-024-IMPORT", "Tekrar indirmede eksik kayıt eski başarılı çıktıyı kullanmaz veya bozmaz", () => {

            var st=New("remote-import");st.Settings.Repo="fixture/repo";var e=new Episode{Topic="İzole bulut paketi",ShortCount=0,Plan=Plan()};e.Plan!.Long!.Scenes[0].StockFile="";

            byte[] Archive(int exports){using var memory=new MemoryStream();using(var zip=new System.IO.Compression.ZipArchive(memory,System.IO.Compression.ZipArchiveMode.Create,true)){using(var writer=new StreamWriter(zip.CreateEntry("partial.txt").Open()))writer.Write("partial");for(int i=0;i<exports;i++){using var writer=new StreamWriter(zip.CreateEntry($"copy{i}/episode-export.json").Open());writer.Write(JsonSerializer.Serialize(e,Json.Options));}}return memory.ToArray();}

            var bytes=Archive(0);int calls=0;using var client=new HttpClient(new Handler(req=>{calls++;return Task.FromResult(req.RequestUri!.AbsolutePath.EndsWith("/zip")?new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(bytes)}:Ok("{\"artifacts\":[{\"id\":1,\"name\":\"anka-package-fixture\",\"expired\":false}]}"));}));var old=Environment.GetEnvironmentVariable("GITHUB_TOKEN");Environment.SetEnvironmentVariable("GITHUB_TOKEN","fixture");try{var github=new GitHub(st,client);Reject(()=>github.Import("101",CancellationToken.None).GetAwaiter().GetResult());bytes=Archive(1);github.Import("202",CancellationToken.None).GetAwaiter().GetResult();var original=st.Episodes.Single();var saved=File.ReadAllBytes(Path.Combine(st.Root,"state.json"));var export=Directory.GetFiles(Path.Combine(st.Root,"imports","202"),"episode-export.json",SearchOption.AllDirectories).Single();bytes=Archive(0);Reject(()=>github.Import("202",CancellationToken.None).GetAwaiter().GetResult());Check(ReferenceEquals(original,st.Episodes.Single())&&saved.SequenceEqual(File.ReadAllBytes(Path.Combine(st.Root,"state.json")))&&File.Exists(export),"Önceki başarılı çıktı veya kayıt değişti");bytes=Archive(2);Reject(()=>github.Import("202",CancellationToken.None).GetAwaiter().GetResult());bytes=Archive(1);github.Import("202",CancellationToken.None).GetAwaiter().GetResult();Check(st.Episodes.Count==1&&st.Episodes[0].RemoteRunId=="202"&&calls==10,"Tekrar geçerli indirme başarısız");}finally{Environment.SetEnvironmentVariable("GITHUB_TOKEN",old);}

        });



        Test("BULK-SECRETS", "Toplu aktarım kayıtlı hizmetleri şifreler, eksikleri atlar ve hatadan sonra devam eder", () => {

            var st=New("bulk-secrets");st.Settings.Repo="fixture/repo";var names=GitHub.ServiceSecretNames.Concat(new[]{"GITHUB_TOKEN","YOUTUBE_REFRESH_TOKEN"}).ToArray();var old=names.ToDictionary(x=>x,Environment.GetEnvironmentVariable);using var pair=Sodium.PublicKeyBox.GenerateKeyPair();var sent=new List<string>();

            try {foreach(var name in names)Environment.SetEnvironmentVariable(name,null);Environment.SetEnvironmentVariable("GITHUB_TOKEN","github-fixture");Environment.SetEnvironmentVariable("YOUTUBE_REFRESH_TOKEN","youtube-private");Environment.SetEnvironmentVariable("OPENROUTER_API_KEY","openrouter-private");Environment.SetEnvironmentVariable("PIXABAY_API_KEY","pixabay-private");

                using var client=new HttpClient(new Handler(async req=>{var path=req.RequestUri!.AbsolutePath;if(req.Method==HttpMethod.Get){Check(path.EndsWith("/actions/secrets/public-key"),"Yanlış uç nokta");return Ok(JsonSerializer.Serialize(new{key=Convert.ToBase64String(pair.PublicKey),key_id="fixture"}));}var name=path.Split('/').Last();Check(req.Method==HttpMethod.Put&&new[]{"OPENROUTER_API_KEY","PIXABAY_API_KEY"}.Contains(name),"İzinsiz anahtar gönderildi");using var doc=JsonDocument.Parse(await req.Content!.ReadAsStringAsync());var restored=Encoding.UTF8.GetString(Sodium.SealedPublicKeyBox.Open(Convert.FromBase64String(doc.RootElement.GetProperty("encrypted_value").GetString()!),pair));Check(restored==Environment.GetEnvironmentVariable(name),"Anahtar şifrelemesi bozuldu");sent.Add(name);return new HttpResponseMessage(name=="OPENROUTER_API_KEY"?HttpStatusCode.Forbidden:HttpStatusCode.NoContent);}));var github=new GitHub(st,client);var result=github.SendRegisteredSecrets(CancellationToken.None).GetAwaiter().GetResult();Check(result.Sent.SequenceEqual(new[]{"PIXABAY_API_KEY"})&&result.Failed.ContainsKey("OPENROUTER_API_KEY")&&result.Missing.Count==11&&sent.Count==2,"Kısmi başarı raporu yanlış");Check(!JsonSerializer.Serialize(result).Contains("private"),"Rapora anahtar değeri yazıldı");using var cancel=new CancellationTokenSource();cancel.Cancel();bool cancelled=false;try{github.SendRegisteredSecrets(cancel.Token).GetAwaiter().GetResult();}catch(OperationCanceledException){cancelled=true;}Check(cancelled&&sent.Count==2,"İptal yeni istek gönderdi");Environment.SetEnvironmentVariable("OPENROUTER_API_KEY",null);Environment.SetEnvironmentVariable("PIXABAY_API_KEY",null);Reject(()=>github.SendRegisteredSecrets(CancellationToken.None).GetAwaiter().GetResult());

            } finally {foreach(var item in old)Environment.SetEnvironmentVariable(item.Key,item.Value);}

        });

        Test("PACKAGE-NUMBERS", "Paket düğmeleri işlem sırasındadır; kaynak panelinde hizmet anahtarları gönderilmez",()=>{

            var st=New("numbering");st.Episodes.Add(new Episode{Topic="Test"});using var form=new StudioForm(st);IEnumerable<Control> Children(Control c)=>c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(Children(x)));var nav=typeof(StudioForm).GetMethod("Navigate",BindingFlags.NonPublic|BindingFlags.Instance)!;nav.Invoke(form,["Video hazırla"]);var buttons=Children(form).OfType<Button>().Where(b=>b.AccessibleName?.StartsWith("Üretim adımı:")==true).ToArray();Check(buttons.Length==5&&buttons.All(b=>b.Parent==buttons[0].Parent),"Üretim adımları bir arada değil.");nav.Invoke(form,["Program kaynakları"]);Check(!Children(form).OfType<Button>().Any(b=>b.Text=="Kayıtlı hizmet anahtarlarını topluca gönder"),"Toplu gizli anahtar aktarımı var.");

        });

        return failed == 0 ? 0 : 1;

    }

    sealed class InlineProgress : IProgress<string> { public void Report(string value) {} }

    static HttpResponseMessage Ok(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> action) : HttpMessageHandler

    {

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => action(request);

    }

}
















