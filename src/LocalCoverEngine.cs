using System.Text;
using System.Text.Json.Nodes;

namespace AnkaStudio;

// Workflows are exported using ComfyUI's Export (API) command. Only loopback is used.
public static class LocalCoverEngine
{
    static Uri LocalUrl(string path)=>new(new Uri("http://127.0.0.1:8188/"),path);
    static readonly HttpClient Http = new() { BaseAddress = new Uri("http://127.0.0.1:8188/"), Timeout = TimeSpan.FromSeconds(30) };
    public static string[] StandardRoots() => [
        Path.Combine(AppContext.BaseDirectory,"tools","local-cover-engine"),
        Path.Combine(AppLock.DefaultDataRoot(),"local-cover-engine"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"AnkaStudio","local-cover-engine"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Documents","AnkaStudio","local-cover-engine"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Belgeler","AnkaStudio","local-cover-engine"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"ComfyUI_windows_portable"),
        @"C:\ComfyUI_windows_portable"];
    public static string[] FindEngines(string root)
    {
        var candidates=new List<string>();
        try{var binding=Path.Combine(root,"engine-folder.txt");if(File.Exists(binding)){var linked=File.ReadAllText(binding).Trim();if(Path.IsPathFullyQualified(linked))candidates.Add(linked);}}catch(IOException){}
        candidates.AddRange([Path.Combine(root,"NVIDIA","ComfyUI_windows_portable"),Path.Combine(root,"NVIDIA-Eski","ComfyUI_windows_portable"),Path.Combine(root,"AMD","ComfyUI_windows_portable"),Path.Combine(root,"Intel","ComfyUI_windows_portable"),Path.Combine(root,"ComfyUI_windows_portable"),root]);
        return candidates.Distinct(StringComparer.OrdinalIgnoreCase).Where(p=>File.Exists(Path.Combine(p,"python_embeded","python.exe"))&&File.Exists(Path.Combine(p,"ComfyUI","main.py"))).ToArray();
    }
    public static string? FindEngine(string root)=>FindEngines(root).FirstOrDefault();
    public static string FindInstalledWorkflow(IEnumerable<string>? codexRoots=null,IEnumerable<string>? engineRoots=null)
    {
        var roots = new List<string>();
        if(engineRoots!=null)roots.AddRange(engineRoots);else if(codexRoots==null)roots.AddRange(StandardRoots());
        var profile=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var locations=codexRoots??new[]{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"Codex"),Path.Combine(profile,"Documents","Codex"),Path.Combine(profile,"OneDrive","Documents","Codex"),Path.Combine(profile,"Belgeler","Codex"),Path.Combine(profile,"OneDrive","Belgeler","Codex")};
        // Historical installations remain discoverable after upgrading.
        foreach(var codex in locations.Distinct(StringComparer.OrdinalIgnoreCase)) {
            try{if(Directory.Exists(codex))foreach(var date in Directory.EnumerateDirectories(codex).Take(60)){
                roots.Add(Path.Combine(date,"work","local-cover-engine"));
                foreach(var child in Directory.EnumerateDirectories(date).Take(30))roots.Add(Path.Combine(child,"work","local-cover-engine"));
            }}catch(Exception ex)when(ex is IOException or UnauthorizedAccessException){}
        }
        foreach(var root in roots.Distinct(StringComparer.OrdinalIgnoreCase)) {
            if(FindEngine(root)==null)continue;
            foreach(var name in new[]{"connected-workflow.json","anka-flux2.json","anka-sdxl.json"}){var path=Path.Combine(root,name);if(File.Exists(path))return path;}
        }
        return "";
    }
    public static async Task<string> Check(string workflow,CancellationToken ct)
    {
        var graph=Prepare(workflow,"connection test",false);
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(8));
        using var response=await Http.GetAsync("object_info",timeout.Token);response.EnsureSuccessStatusCode();
        var available=JsonNode.Parse(await response.Content.ReadAsStringAsync(timeout.Token))!.AsObject();
        foreach(var entry in graph){var type=entry.Value!["class_type"]!.GetValue<string>();
            if(available[type] is not JsonObject node)throw new InvalidOperationException("Motor bu düğümü desteklemiyor: "+type);
            foreach(var input in entry.Value!["inputs"]!.AsObject()){
                var choices=node["input"]?["required"]?[input.Key]?[0] as JsonArray;
                if(choices!=null&&input.Value is JsonValue value&&value.TryGetValue<string>(out var text)&&!choices.Any(x=>x?.ToString()==text))throw new InvalidOperationException("Motorda bulunamayan model/seçenek: "+text+". İş akışını kurulu modellerine göre seç.");
            }
        }
        return "Bağlantı ve iş akışı uygun • gerekli model adları motorda bulundu. Görsel üretimi henüz yapılmadı.";
    }
    public static async Task Start(string workflow,Action<string> report,CancellationToken ct,string? logDirectory=null)
    {
        async Task<bool> Ready(){try{using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(2));using var response=await Http.GetAsync("system_stats",timeout.Token);return response.IsSuccessStatusCode;}catch(Exception ex)when(ex is HttpRequestException||ex is OperationCanceledException&&!ct.IsCancellationRequested){return false;}}
        if(await Ready()){report("Yerel motor zaten açık • hazır.");return;}
        if(!File.Exists(workflow))throw new InvalidOperationException("Yerel görsel iş akışı bulunamadı.");
        var discovered=FindInstalledWorkflow();var roots=new[]{Path.GetDirectoryName(Path.GetFullPath(workflow))!}.Concat(StandardRoots()).Concat(discovered!=""?[Path.GetDirectoryName(discovered)!]:Array.Empty<string>());
        var engines=roots.SelectMany(FindEngines).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if(engines.Length==0)throw new InvalidOperationException("Kullanılabilir yerel motor kurulumu bulunamadı.");
        logDirectory??=Path.Combine(AppLock.DefaultDataRoot(),"logs","local-engine");Directory.CreateDirectory(logDirectory);var failures=new List<string>();
        var detected=new List<(string Folder,EngineHardware.Capability[] Devices)>();
        foreach(var installed in engines){ct.ThrowIfCancellationRequested();report("Bilgisayardaki GPU ve motor uyumu algılanıyor…");try{var devices=await EngineHardware.Probe(installed,ct);detected.Add((installed,devices));foreach(var device in devices)report("Algılandı: "+device.Backend+" • "+device.Device);}catch(Exception ex)when(ex is not OperationCanceledException||!ct.IsCancellationRequested){failures.Add("Donanım denetimi: "+ex.Message);}}
        foreach(var candidate in EngineHardware.Order(detected)){
            string engine=candidate.Folder;ct.ThrowIfCancellationRequested();string backend=candidate.Backend+" • "+candidate.Device;
            string log=Path.Combine(logDirectory,DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")[..6]+".log");
            var info=new System.Diagnostics.ProcessStartInfo(Path.Combine(engine,"python_embeded","python.exe")){WorkingDirectory=engine,UseShellExecute=false,CreateNoWindow=true,WindowStyle=System.Diagnostics.ProcessWindowStyle.Hidden};
            foreach(var arg in new[]{"-s","-u","-c","import sys,runpy; log=open(sys.argv.pop(1),'a',buffering=1,encoding='utf-8'); sys.stdout=log; sys.stderr=log; sys.argv[0]='ComfyUI/main.py'; runpy.run_path(sys.argv[0],run_name='__main__')",log,"--windows-standalone-build","--listen","127.0.0.1","--port","8188","--preview-method","none"}.Concat(EngineHardware.Arguments(candidate.Backend)))info.ArgumentList.Add(arg);
            report(backend+" görsel motoru başlatılıyor • bağlantı otomatik kontrol ediliyor…");
            System.Diagnostics.Process? process=null;bool ready=false;
            try{
                process=System.Diagnostics.Process.Start(info)??throw new InvalidOperationException("Motor işlemi başlatılamadı.");
                for(int i=0;i<45;i++){ct.ThrowIfCancellationRequested();if(await Ready()){ready=true;report(backend+" görsel motoru hazır.");return;}if(process.HasExited)throw new InvalidOperationException("Motor kapandı (kod "+process.ExitCode+"). "+(File.Exists(log)?string.Join(" ",File.ReadLines(log).Where(x=>!string.IsNullOrWhiteSpace(x)).TakeLast(3)):""));await Task.Delay(2000,ct);}
                throw new InvalidOperationException("Motor hazır olma süresini aştı.");
            }catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
            catch(Exception ex){string reason=backend+": "+ex.Message;failures.Add(reason);report(backend+" motoru kullanılamadı; başka kurulu AI motoru deneniyor. "+ex.Message);}
            finally{if(process!=null){if(!ready&&!process.HasExited)try{process.Kill(true);}catch{}process.Dispose();}}
        }
        throw new InvalidOperationException("Kurulu AI motorlarının tamamı denendi. "+string.Join(" | ",failures)+" Kayıtlar: "+logDirectory);
    }
    // Kolay menü: kullanıcı JSON düzenlemez; kalite ve model seçimi burada iş akışına uygulanır.
    public static readonly string[] Qualities = ["Hızlı", "Dengeli", "Ultra sinematik"];
    public static readonly string[] Models = ["Otomatik", "FLUX.2", "Stable Diffusion XL"];

    static bool IsFlux(JsonObject graph) => graph.Any(x => x.Value?["class_type"]?.GetValue<string>() == "EmptyFlux2LatentImage");
    static (int W, int H) Size(JsonObject graph, bool square, bool vertical,string quality)
    {
        if (square) return (768, 768);
        if (vertical) return (576, 1024);
        return quality switch { "Hızlı" => (1024, 576), "Dengeli" => (1280, 720), _ => IsFlux(graph) ? (1536, 864) : (1344, 768) };
    }
    static int? Steps(JsonObject graph,string quality) => quality switch { "Hızlı" => IsFlux(graph) ? 16 : 20, "Dengeli" => IsFlux(graph) ? 26 : 28, _ => IsFlux(graph) ? 36 : 38 };
    public static string Describe(string quality, string model) => quality switch {
        "Hızlı" => "Hızlı • 1024×576 • daha az işlem",
        "Dengeli" => "Dengeli • 1280×720 • iyi kalite",
        _ => model.StartsWith("FLUX") ? "Ultra sinematik • 1536×864 • daha fazla bellek ve süre" : "Ultra sinematik • 1344×768 • daha fazla bellek ve süre" };
    // Motor klasörünü ve modele uygun iş akışını kendisi bulur; bulunamazsa paketle gelen hazır iş akışına döner.
    public static string ResolveWorkflow(string model, string current = "")
    {
        string wanted = model.StartsWith("FLUX") ? "anka-flux2.json" : model.StartsWith("Stable") ? "anka-sdxl.json" : "";
        string found = File.Exists(current) ? current : FindInstalledWorkflow();
        if (wanted == "") return found != "" ? found : Bundled("anka-flux2.json");
        if (found != "") { var sibling = Path.Combine(Path.GetDirectoryName(found)!, wanted); if (File.Exists(sibling)) return sibling; }
        return Bundled(wanted);
    }
    static string Bundled(string name) { var path = Path.Combine(AppContext.BaseDirectory, "tools", "workflows", name); return File.Exists(path) ? path : ""; }
    public static string ModelOf(string workflow) { try { return File.Exists(workflow) && File.ReadAllText(workflow).Contains("EmptyFlux2LatentImage") ? "FLUX.2" : "Stable Diffusion XL"; } catch (IOException) { return "Stable Diffusion XL"; } }
    public static JsonObject Prepare(string file, string prompt, bool vertical, bool square=false,string quality="Dengeli")
    {
        if (!File.Exists(file)) throw new InvalidOperationException("Yerel motor için ComfyUI API iş akışı dosyasını seç.");
        if (new FileInfo(file).Length > 2_000_000) throw new InvalidOperationException("İş akışı dosyası çok büyük.");
        var graph = JsonNode.Parse(File.ReadAllText(file)) as JsonObject ?? throw new InvalidOperationException("Geçersiz API iş akışı.");
        bool found = false;
        foreach (var entry in graph)
        {
            if (entry.Value is not JsonObject node || node["inputs"] is not JsonObject inputs) continue;
            var type = node["class_type"]?.GetValue<string>() ?? "";
            if (type == "VAEDecodeTiled") { inputs["temporal_size"] ??= 64; inputs["temporal_overlap"] ??= 8; }
            string[] localTypes = ["CheckpointLoaderSimple", "CLIPTextEncode", "EmptyLatentImage", "EmptySD3LatentImage", "KSampler", "KSamplerAdvanced", "VAEDecode", "VAEDecodeTiled", "SaveImage", "PreviewImage", "UNETLoader", "CLIPLoader", "DualCLIPLoader", "VAELoader", "ModelSamplingAuraFlow", "ModelSamplingFlux", "FluxGuidance", "ConditioningZeroOut", "CLIPSetLastLayer", "LoraLoader", "ModelSamplingDiscrete"];
            if (!localTypes.Contains(type) && !new[]{"EmptyFlux2LatentImage","Flux2Scheduler","CFGGuider","KSamplerSelect","SamplerCustomAdvanced","RandomNoise","ReferenceLatent"}.Contains(type)) throw new InvalidOperationException("Bu iş akışı desteklenmeyen veya bulut bağlantılı bir düğüm içeriyor: " + type + ". Yerel kapak iş akışını kullan.");
            foreach (var input in inputs.ToArray())
                if (input.Value is JsonValue v && v.TryGetValue<string>(out var value) && value.Contains("{{ANKA_PROMPT}}"))
                { inputs[input.Key] = value.Replace("{{ANKA_PROMPT}}", prompt); found = true; }
            if (inputs["seed"] is JsonValue) inputs["seed"] = Random.Shared.NextInt64(0, long.MaxValue);
            if (inputs["noise_seed"] is JsonValue) inputs["noise_seed"] = Random.Shared.NextInt64(0, long.MaxValue);
            if (node["class_type"]?.GetValue<string>() is "EmptyLatentImage" or "EmptySD3LatentImage" or "EmptyFlux2LatentImage" or "Flux2Scheduler")
            { var (w,h)=Size(graph,square,vertical,quality); inputs["width"] = w; inputs["height"] = h; if(type!="Flux2Scheduler")inputs["batch_size"] = 1; }
            if (type is "KSampler" or "Flux2Scheduler" && inputs["steps"] is JsonValue && Steps(graph,quality) is int steps) inputs["steps"] = steps;
        }
        if (!found) throw new InvalidOperationException("API iş akışının olumlu görsel talimatına {{ANKA_PROMPT}} eklenmeli.");
        return graph;
    }
    public static JsonObject ApplyReference(JsonObject graph,string uploaded,long seed,int castCount=1,IReadOnlyList<int>? characterIds=null,int referenceWidth=768,bool styleReference=false)
    {
        if(graph.Any(x=>x.Value?["class_type"]?.GetValue<string>()=="EmptyFlux2LatentImage")) {
            var vae=graph.First(x=>x.Value?["class_type"]?.GetValue<string>()=="VAELoader");
            graph["anka_reference"]=new JsonObject{["class_type"]="LoadImage",["inputs"]=new JsonObject{["image"]=uploaded}};
            graph["anka_reference_scale"]=new JsonObject{["class_type"]="ImageScaleToTotalPixels",["inputs"]=new JsonObject{["image"]=new JsonArray("anka_reference",0),["upscale_method"]="lanczos",["megapixels"]=.6,["resolution_steps"]=1}};
            graph["anka_reference_latent"]=new JsonObject{["class_type"]="VAEEncode",["inputs"]=new JsonObject{["pixels"]=new JsonArray("anka_reference_scale",0),["vae"]=new JsonArray(vae.Key,0)}};
            foreach(var entry in graph.Where(x=>x.Value?["class_type"]?.GetValue<string>()=="CFGGuider").ToArray()) {
                var inputs=entry.Value!["inputs"]!.AsObject();
                foreach(string field in new[]{"positive","negative"}){string id="anka_flux_reference_"+field;var original=inputs[field]!.DeepClone();graph[id]=new JsonObject{["class_type"]="ReferenceLatent",["inputs"]=new JsonObject{["conditioning"]=original,["latent"]=new JsonArray("anka_reference_latent",0)}};inputs[field]=new JsonArray(id,0);}
            }
            foreach(var noise in graph.Where(x=>x.Value?["class_type"]?.GetValue<string>()=="RandomNoise"))noise.Value!["inputs"]!["noise_seed"]=seed;
            return graph;
        }
        // Condition identity on the reference while keeping a fresh scene latent.
        var checkpoint=graph.FirstOrDefault(x=>x.Value?["class_type"]?.GetValue<string>()=="CheckpointLoaderSimple");
        var samplers=graph.Where(x=>x.Value?["class_type"]?.GetValue<string>()=="KSampler").ToArray();
        if(checkpoint.Key==null || samplers.Length==0)throw new InvalidOperationException("Yerel hikâye için SDXL CheckpointLoaderSimple ve KSampler iş akışı gerekli.");
        var ids=characterIds??Enumerable.Range(0,castCount).ToArray();
        if(castCount<1||castCount>4||ids.Distinct().Count()!=ids.Count||ids.Any(x=>x<0||x>=castCount))throw new InvalidOperationException("Karakter referansı kimlikleri geçersiz.");
        if(ids.Count==0)return graph;
        var latent=graph.FirstOrDefault(x=>x.Value?["class_type"]?.GetValue<string>()=="EmptyLatentImage").Value?["inputs"];
        int width=latent?["width"]?.GetValue<int>()??576,height=latent?["height"]?.GetValue<int>()??1024;
        string imageId="anka_reference",adapterId="anka_identity";
        graph[imageId]=new JsonObject { ["class_type"]="LoadImage",["inputs"]=new JsonObject { ["image"]=uploaded } };
        graph["anka_ipadapter_loader"]=new JsonObject { ["class_type"]="IPAdapterModelLoader",["inputs"]=new JsonObject { ["ipadapter_file"]="ip-adapter-plus_sdxl_vit-h.safetensors" } };
        graph["anka_clip_vision"]=new JsonObject { ["class_type"]="CLIPVisionLoader",["inputs"]=new JsonObject { ["clip_name"]="CLIP-ViT-H-14-laion2B-s32B-b79K.safetensors" } };
        string previous=checkpoint.Key;
        for(int position=0;position<ids.Count;position++) {
            string crop="anka_face_"+position,mask="anka_mask_"+position,node=position==ids.Count-1?adapterId:"anka_identity_"+position;
            string subjectImage=imageId;
            if(castCount>1){int slot=referenceWidth/castCount;graph[crop]=new JsonObject{["class_type"]="ImageCrop",["inputs"]=new JsonObject{["image"]=new JsonArray(imageId,0),["width"]=slot,["height"]=slot,["x"]=ids[position]*slot,["y"]=0}};subjectImage=crop;}
            var inputs=new JsonObject {
                ["model"]=new JsonArray(previous,0),["ipadapter"]=new JsonArray("anka_ipadapter_loader",0),["clip_vision"]=new JsonArray("anka_clip_vision",0),["image"]=new JsonArray(subjectImage,0),
                ["weight"]=.65,["weight_type"]=styleReference?"style transfer":"linear",["combine_embeds"]="concat",["start_at"]=0,["end_at"]=.8,["embeds_scaling"]="V only"
            };
            if(ids.Count>1){
                graph[mask+"_base"]=new JsonObject{["class_type"]="SolidMask",["inputs"]=new JsonObject{["value"]=0.0,["width"]=width,["height"]=height}};
                graph[mask+"_subject"]=new JsonObject{["class_type"]="SolidMask",["inputs"]=new JsonObject{["value"]=1.0,["width"]=width/ids.Count,["height"]=height}};
                graph[mask]=new JsonObject{["class_type"]="MaskComposite",["inputs"]=new JsonObject{["destination"]=new JsonArray(mask+"_base",0),["source"]=new JsonArray(mask+"_subject",0),["x"]=position*(width/ids.Count),["y"]=0,["operation"]="add"}};
                inputs["attn_mask"]=new JsonArray(mask,0);
            }
            graph[node]=new JsonObject{["class_type"]="IPAdapterAdvanced",["inputs"]=inputs};previous=node;
        }
        foreach(var sampler in samplers){var inputs=sampler.Value!["inputs"]!.AsObject();inputs["model"]=new JsonArray(adapterId,0);inputs["denoise"]=1;inputs["seed"]=seed;}
        return graph;
    }
    public static async Task CheckStorySupport(CancellationToken ct,string? workflow=null)
    {
        if(workflow!=null&&File.Exists(workflow)&&File.ReadAllText(workflow).Contains("EmptyFlux2LatentImage")) {
            using var response=await Http.GetAsync("object_info/ReferenceLatent",ct);response.EnsureSuccessStatusCode();
            var info=JsonNode.Parse(await response.Content.ReadAsStringAsync(ct));if(info?["ReferenceLatent"]==null)throw new InvalidOperationException("FLUX.2 yerel referans desteği bulunamadı. NVIDIA motorunu güncelle.");return;
        }
        foreach(var (node,field,model) in new[]{("IPAdapterAdvanced","",""),("IPAdapterModelLoader","ipadapter_file","ip-adapter-plus_sdxl_vit-h.safetensors"),("CLIPVisionLoader","clip_name","CLIP-ViT-H-14-laion2B-s32B-b79K.safetensors")}) {
            using var response=await Http.GetAsync("object_info/"+node,ct);response.EnsureSuccessStatusCode();
            var info=JsonNode.Parse(await response.Content.ReadAsStringAsync(ct));
            if(info?[node]==null || field!="" && !(info[node]?["input"]?["required"]?[field]?[0]?.AsArray().Any(x=>x?.GetValue<string>()==model)??false))
                throw new InvalidOperationException("Yerel hikâye karakter desteği eksik: "+node+" "+model+". Hikâye motoru kurulumunu tamamla ve ComfyUI'yi yeniden başlat.");
        }
    }
    public static async Task<byte[]> Generate(string file, string prompt, bool vertical, Action<string> log, CancellationToken ct, HttpClient? client = null, bool square=false,string? referenceImage=null,int castCount=1,IReadOnlyList<int>? characterIds=null,bool styleReference=false,string quality="Dengeli")
    {
        var transport = client ?? Http;
        var graph = Prepare(file, prompt, vertical,square,quality);
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct); budget.CancelAfter(TimeSpan.FromMinutes(15));
        var token = budget.Token;
        if(referenceImage!=null) {
            using var upload=new MultipartFormDataContent();var bytes=new ByteArrayContent(await File.ReadAllBytesAsync(referenceImage,token));bytes.Headers.ContentType=new("image/png");
            upload.Add(bytes,"image","anka-reference-"+Reliability.FileHash(referenceImage)[..16]+".png");upload.Add(new StringContent("input"),"type");upload.Add(new StringContent("true"),"overwrite");
            using var response=await transport.PostAsync(LocalUrl("upload/image"),upload,token);response.EnsureSuccessStatusCode();
            var uploaded=JsonNode.Parse(await response.Content.ReadAsStringAsync(token))?["name"]?.GetValue<string>()??throw new InvalidOperationException("Referans görsel yüklenemedi.");
            if(uploaded.Contains('/')||uploaded.Contains('\\')||uploaded.Contains(".."))throw new InvalidOperationException("Geçersiz yerel referans adı.");
            long seed=BitConverter.ToInt64(Convert.FromHexString(TimedNarration.TextHash(Reliability.FileHash(referenceImage)+prompt)),0)&long.MaxValue;
            using var referenceStream=File.OpenRead(referenceImage);using var referenceBitmap=Image.FromStream(referenceStream);
            ApplyReference(graph,uploaded,seed,castCount,characterIds,referenceBitmap.Width,styleReference);log(styleReference?"YEREL MOTOR • Önceki kapak tasarım tarzı referans alınıyor.":"YEREL MOTOR • Karakter kimlikleri ayrı referans ve alanlarla sahneye bağlanıyor.");
        }
        log("YEREL MOTOR • ComfyUI bağlantısı kontrol ediliyor…");
        using var body = new StringContent(new JsonObject { ["prompt"] = graph, ["client_id"] = Guid.NewGuid().ToString("N") }.ToJsonString(), Encoding.UTF8, "application/json");
        using var queued = await transport.PostAsync(LocalUrl("prompt"), body, token); queued.EnsureSuccessStatusCode();
        var result = JsonNode.Parse(await queued.Content.ReadAsStringAsync(token));
        var id = result?["prompt_id"]?.GetValue<string>() ?? throw new InvalidOperationException("Yerel motor işi kabul etmedi.");
        if(string.IsNullOrWhiteSpace(id)||id.Length>200)throw new InvalidOperationException("Yerel motor geçerli iş kimliği döndürmedi.");
        log("YEREL MOTOR • Görsel sıraya alındı; model yükleniyor ve tasarım üretiliyor…");
        int ticks = 0;
        try { while (true)
        {
            await Task.Delay(1500, token);
            using var response = await transport.GetAsync(LocalUrl("history/" + Uri.EscapeDataString(id)), token); response.EnsureSuccessStatusCode();
            var history = JsonNode.Parse(await response.Content.ReadAsStringAsync(token))?[id];
            if (history?["status"]?["status_str"]?.GetValue<string>() == "error") throw new InvalidOperationException("Yerel model üretim hatası; ComfyUI işlem kaydını kontrol et.");
            if (history?["outputs"] is JsonObject outputs)
                foreach (var output in outputs)
                    if (output.Value?["images"] is JsonArray images && images.FirstOrDefault() is JsonObject image)
                    {
                        string Part(string name) => Uri.EscapeDataString(image[name]?.GetValue<string>() ?? "");
                        using var download = await transport.GetAsync(LocalUrl($"view?filename={Part("filename")}&subfolder={Part("subfolder")}&type={Part("type")}"), HttpCompletionOption.ResponseHeadersRead, token);
                        download.EnsureSuccessStatusCode();
                        await using var stream = await download.Content.ReadAsStreamAsync(token); using var data = new MemoryStream(); var buffer = new byte[81920]; int count;
                        while ((count = await stream.ReadAsync(buffer, token)) > 0) { if (data.Length + count > 32_000_000) throw new InvalidOperationException("Yerel görsel çok büyük."); data.Write(buffer, 0, count); }
                        return data.ToArray();
                    }
            if (++ticks % 20 == 0) log("YEREL MOTOR • Tasarım sürüyor; geçen süre yaklaşık " + (ticks * 1.5).ToString("0") + " saniye.");
        } } catch(OperationCanceledException) {
            using var cleanup=new CancellationTokenSource(TimeSpan.FromSeconds(4));
            try {
                using var remove=new StringContent(new JsonObject{["delete"]=new JsonArray(id)}.ToJsonString(),Encoding.UTF8,"application/json");
                using var removed=await transport.PostAsync(LocalUrl("queue"),remove,cleanup.Token);
                using var interrupt=new StringContent(new JsonObject{["prompt_id"]=id}.ToJsonString(),Encoding.UTF8,"application/json");
                using var stopped=await transport.PostAsync(LocalUrl("interrupt"),interrupt,cleanup.Token);
                log("Yerel motorun yalnız bu hikâye işine durdurma isteği gönderildi; hazır sahneler korunuyor.");
            }catch(Exception){log("Yerel motor durdurma yanıtı alınamadı; motor hâlâ hesaplıyor olabilir. Hazır dosyalar korunuyor.");}
            throw;
        }
    }
}
