using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
namespace AnkaStudio;

public static class AnimationVideo
{
    public const string Model="LTX 2.5 • Animasyon • Wan 14B yedek";
    public const string Version="ltx25-wan14-two-attempts-v1";
    static readonly HttpClient Http=new(){Timeout=TimeSpan.FromSeconds(45)};
    static readonly Uri Root=new("http://127.0.0.1:8189/");
    static Process? owned;
    static string Profile(string name)=>Path.Combine(AppContext.BaseDirectory,"tools","workflows",name);
    static async Task<JsonNode> Read(string path,CancellationToken ct){using var response=await Http.GetAsync(new Uri(Root,path),ct);response.EnsureSuccessStatusCode();return JsonNode.Parse(await response.Content.ReadAsStringAsync(ct))!;}
    public static JsonObject Graph(bool wan,string image,string prompt,long seed,bool vertical)
    {
        var graph=JsonNode.Parse(File.ReadAllText(Profile(wan?"anka-wan22-14b-rtx3060.json":"anka-ltx25-rtx3060.json")))!.AsObject();
        string imageId=wan?"7":"8",positive=wan?"5":"5",save=wan?"16":"21",decode=wan?"15":"20",create=wan?"17":"22";
        graph[imageId]!["inputs"]!["image"]=image;graph[positive]!["inputs"]!["text"]=prompt;
        if(wan){graph["13"]!["inputs"]!["noise_seed"]=seed;graph["14"]!["inputs"]!["noise_seed"]=seed;graph["8"]!["inputs"]!["width"]=vertical?480:832;graph["8"]!["inputs"]!["height"]=vertical?832:480;}
        else{graph["14"]!["inputs"]!["noise_seed"]=seed;graph["10"]!["inputs"]!["width"]=vertical?480:832;graph["10"]!["inputs"]!["height"]=vertical?832:480;}
        graph[create]=new JsonObject{["class_type"]="CreateVideo",["inputs"]=new JsonObject{["images"]=new JsonArray(decode,0),["fps"]=wan?16:24}};
        graph[save]=new JsonObject{["class_type"]="SaveVideo",["inputs"]=new JsonObject{["video"]=new JsonArray(create,0),["filename_prefix"]="TubeFlow/Animation",["format"]="mp4",["format.codec"]="h264"}};
        return graph;
    }
    static async Task Ensure(Store store,CancellationToken ct,Action<string> log)
    {
        try{await Read("system_stats",ct);return;}catch(HttpRequestException){}
        string config=Path.Combine(store.Root,"engines","animation-engine.json");
        if(!File.Exists(config))config=Profile("animation-engine.json");
        if(!File.Exists(config))throw new InvalidOperationException("Animasyon motoru açık değil. Yerel animasyon motorunu başlat.");
        var settings=JsonNode.Parse(File.ReadAllText(config))!;string python=settings["Python"]!.GetValue<string>(),script=settings["Launcher"]?.GetValue<string>()??Path.Combine(AppContext.BaseDirectory,"tools","animation-engine","launch.py");
        if(!File.Exists(python)||!File.Exists(script))throw new InvalidOperationException("Animasyon motorunun Python veya başlatıcı dosyası bulunamadı.");
        var start=new ProcessStartInfo(python){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};start.Environment.Remove("SSLKEYLOGFILE");start.ArgumentList.Add("-X");start.ArgumentList.Add("utf8");start.ArgumentList.Add(script);
        if(settings["Launcher"]==null){foreach(string value in new[]{"--comfy-root",settings["ComfyRoot"]!.GetValue<string>(),"--model-paths",settings["ModelPaths"]!.GetValue<string>(),"--runtime",Path.Combine(store.Root,"engines","animation-runtime")})start.ArgumentList.Add(value);}
        owned=Process.Start(start);
        log("Animasyon motoru açılıyor…");
        for(int i=0;i<90;i++){ct.ThrowIfCancellationRequested();await Task.Delay(1000,ct);try{await Read("system_stats",ct);return;}catch(HttpRequestException){}}
        throw new InvalidOperationException("Animasyon motoru başlatılamadı.");
    }
    public static async Task Generate(Store store,string reference,string prompt,string output,bool vertical,long seed,Action<string> log,CancellationToken ct)
    {
        await LocalGpuMemory.ReleaseText(store,ct);await LocalGpuMemory.FreeIdleComfy(8188,ct);
        await RunFallback(async(wan,attempt)=>{await Ensure(store,ct,log);await Render(wan,reference,prompt,output,vertical,(seed+attempt)&long.MaxValue,ct);await Reliability.ValidateVideo(store,output,ct);if(await Media.Duration(store,output,ct)<5.95)throw new InvalidOperationException("Animasyon klibi beklenenden kısa.");},async()=>{
            using var cleanup=new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try{using var body=new StringContent("{\"unload_models\":true,\"free_memory\":true}",Encoding.UTF8,"application/json");using var response=await Http.PostAsync(new Uri(Root,"free"),body,cleanup.Token);}catch{}
            if(owned is {HasExited:false}){owned.Kill();await owned.WaitForExitAsync(cleanup.Token);owned=null;}
        },log,ct);
    }
    internal static async Task RunFallback(Func<bool,int,Task> render,Func<Task> recover,Action<string> log,CancellationToken ct)
    {
        Exception? last=null;
        foreach(bool wan in new[]{false,true})for(int attempt=0;attempt<2;attempt++){
            ct.ThrowIfCancellationRequested();string label=wan?"Wan 14B":"LTX 2.5";log($"{label} • deneme {attempt+1}/2");
            try{await render(wan,attempt);log(label+" • sahne tamamlandı");return;}
            catch(Exception error)when(error is not OperationCanceledException||!ct.IsCancellationRequested){
                ct.ThrowIfCancellationRequested();last=error;log(label+" • deneme tamamlanamadı: "+error.Message);
                try{await recover();}catch(Exception cleanupError)when(!ct.IsCancellationRequested){log("Motor temizliği tamamlanamadı: "+cleanupError.Message);}
            }
        }
        throw new InvalidOperationException("LTX ve Wan ikişer denemede tamamlanamadı; sahnenin kendi görseli kullanılacak.",last);
    }
    static async Task Render(bool wan,string reference,string prompt,string output,bool vertical,long seed,CancellationToken ct)
    {
        using var budget=CancellationTokenSource.CreateLinkedTokenSource(ct);budget.CancelAfter(TimeSpan.FromMinutes(30));var token=budget.Token;
        using var upload=new MultipartFormDataContent();upload.Add(new ByteArrayContent(await File.ReadAllBytesAsync(reference,token)),"image","animation-"+Reliability.FileHash(reference)[..16]+Path.GetExtension(reference));upload.Add(new StringContent("input"),"type");upload.Add(new StringContent("true"),"overwrite");
        using var uploaded=await Http.PostAsync(new Uri(Root,"upload/image"),upload,token);uploaded.EnsureSuccessStatusCode();string image=JsonNode.Parse(await uploaded.Content.ReadAsStringAsync(token))!["name"]!.GetValue<string>();
        if(image.Contains('/')||image.Contains('\\')||image.Contains(".."))throw new InvalidOperationException("Referans dosyası adı geçersiz.");
        using var body=new StringContent(new JsonObject{["prompt"]=Graph(wan,image,prompt,seed,vertical),["client_id"]="tubeflow-animation-"+Guid.NewGuid().ToString("N")}.ToJsonString(),Encoding.UTF8,"application/json");
        using var queued=await Http.PostAsync(new Uri(Root,"prompt"),body,token);if(!queued.IsSuccessStatusCode)throw new InvalidOperationException("Animasyon iş akışı reddedildi: "+(int)queued.StatusCode);
        string id=JsonNode.Parse(await queued.Content.ReadAsStringAsync(token))!["prompt_id"]!.GetValue<string>();bool complete=false;string pending=output+".pending";
        try{
            while(true){await Task.Delay(1500,token);var history=(await Read("history/"+Uri.EscapeDataString(id),token))[id];
                if(history?["status"]?["status_str"]?.GetValue<string>()=="error")throw new InvalidOperationException("Yerel animasyon hesaplaması başarısız oldu.");
                var video=history?["outputs"]?[wan?"16":"21"]?["videos"]?.AsArray().FirstOrDefault();
                // Current ComfyUI SaveVideo reports its MP4 in the images collection.
                video??=history?["outputs"]?[wan?"16":"21"]?["images"]?.AsArray().FirstOrDefault();
                if(video==null)continue;
                string query=string.Join("&",new[]{"filename","subfolder","type"}.Select(k=>k+"="+Uri.EscapeDataString(video[k]?.GetValue<string>()??"")));
                using var response=await Http.GetAsync(new Uri(Root,"view?"+query),HttpCompletionOption.ResponseHeadersRead,token);response.EnsureSuccessStatusCode();Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
                await using(var input=await response.Content.ReadAsStreamAsync(token))await using(var file=File.Create(pending)){await input.CopyToAsync(file,token);}
                if(new FileInfo(pending).Length<100)throw new InvalidOperationException("Motor boş video döndürdü.");
                File.Move(pending,output,true);complete=true;return;
            }
        }finally{
            if(!complete){using var cleanup=new CancellationTokenSource(TimeSpan.FromSeconds(5));try{var queue=await Read("queue",cleanup.Token);if(queue["queue_running"] is JsonArray rows&&rows.Any(x=>x is JsonArray row&&row.Count>1&&row[1]?.GetValue<string>()==id)){using var stop=new StringContent(new JsonObject{["prompt_id"]=id}.ToJsonString(),Encoding.UTF8,"application/json");using var response=await Http.PostAsync(new Uri(Root,"interrupt"),stop,cleanup.Token);}using var remove=new StringContent(new JsonObject{["delete"]=new JsonArray(id)}.ToJsonString(),Encoding.UTF8,"application/json");using var removed=await Http.PostAsync(new Uri(Root,"queue"),remove,cleanup.Token);}catch{}}
            if(File.Exists(pending))File.Delete(pending);
        }
    }
}
