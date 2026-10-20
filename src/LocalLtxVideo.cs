using System.Text;
using System.Text.Json.Nodes;
namespace AnkaStudio;

// This adapter owns only local ComfyUI video inference, never publication or narration.
public static class LocalLtxVideo
{
    public const string Version="ltx-2b-i2v-v1";
    static readonly Uri Root=new("http://127.0.0.1:8188/");
    static readonly HttpClient Http=new(){Timeout=TimeSpan.FromSeconds(45)};
    static Uri Url(string path)=>new(Root,path);
    static async Task<JsonNode> Read(HttpClient http,string path,CancellationToken ct){using var response=await http.GetAsync(Url(path),ct);response.EnsureSuccessStatusCode();return JsonNode.Parse(await response.Content.ReadAsStringAsync(ct))!;}
    public static async Task<(string Model,string Encoder)> Models(CancellationToken ct,HttpClient? client=null)
    {
        var http=client??Http;using var budget=CancellationTokenSource.CreateLinkedTokenSource(ct);budget.CancelAfter(TimeSpan.FromSeconds(12));
        var checkpoints=await Read(http,"object_info/CheckpointLoaderSimple",budget.Token);
        var encoders=await Read(http,"object_info/CLIPLoader",budget.Token);
        string model=checkpoints["CheckpointLoaderSimple"]?["input"]?["required"]?["ckpt_name"]?[0]?.AsArray().Select(x=>x!.GetValue<string>()).FirstOrDefault(x=>Path.GetFileName(x)=="ltxv-2b-0.9.8-distilled-fp8.safetensors")??"";
        string encoder=encoders["CLIPLoader"]?["input"]?["required"]?["clip_name"]?[0]?.AsArray().Select(x=>x!.GetValue<string>()).FirstOrDefault(x=>Path.GetFileName(x)=="t5xxl_fp8_e4m3fn.safetensors")??"";
        if(model==""||encoder=="")throw new InvalidOperationException("LTX kısa video için ltxv-2b-0.9.8-distilled-fp8.safetensors ve t5xxl_fp8_e4m3fn.safetensors gerekli. Yüklenmesi gerekenler listesinden yerel motoru kontrol et.");
        foreach(string type in new[]{"LTXVImgToVideo","LTXVConditioning","ManualSigmas","SamplerCustom","VAEDecodeTiled","VHS_VideoCombine"})if((await Read(http,"object_info/"+type,budget.Token))[type]==null)throw new InvalidOperationException("LTX iş akışı düğümü eksik: "+type);
        return(model,encoder);
    }
    public static JsonObject Graph(string model,string encoder,string image,string prompt,int seconds,bool vertical,long seed,bool smaller=false)
    {
        if(seconds is <5 or >6||prompt.Length<10||prompt.Length>2400||seed<0)throw new InvalidOperationException("LTX sahnesi 5–6 saniye ve geçerli hareket talimatı gerektirir.");
        int width=smaller?384:512,height=smaller?224:288;if(vertical)(width,height)=(height,width);
        JsonObject Node(string type,object inputs)=>new(){["class_type"]=type,["inputs"]=System.Text.Json.JsonSerializer.SerializeToNode(inputs)};
        return new JsonObject{
            ["model"]=Node("CheckpointLoaderSimple",new{ckpt_name=model}),
            ["text"]=Node("CLIPLoader",new{clip_name=encoder,type="ltxv",device="cpu"}),
            ["image"]=Node("LoadImage",new{image}),
            ["positive"]=Node("CLIPTextEncode",new{text=prompt,clip=new object[]{"text",0}}),
            ["negative"]=Node("CLIPTextEncode",new{text="deformed face, changing identity, changing clothes, flickering, distorted hands, text, subtitles, watermark, camera shake, fast motion, speaking, lip movements",clip=new object[]{"text",0}}),
            ["reference"]=Node("LTXVImgToVideo",new{positive=new object[]{"positive",0},negative=new object[]{"negative",0},vae=new object[]{"model",2},image=new object[]{"image",0},width,height,length=seconds*24+1,batch_size=1,strength=.9}),
            ["conditioning"]=Node("LTXVConditioning",new{positive=new object[]{"reference",0},negative=new object[]{"reference",1},frame_rate=24}),
            ["sigmas"]=Node("ManualSigmas",new{sigmas="1.0, 0.9937, 0.9875, 0.9812, 0.9750, 0.9094, 0.7250, 0.4219, 0.0"}),
            ["sampler"]=Node("KSamplerSelect",new{sampler_name="euler"}),
            ["sample"]=Node("SamplerCustom",new{model=new object[]{"model",0},add_noise=true,noise_seed=seed,cfg=1.0,positive=new object[]{"conditioning",0},negative=new object[]{"conditioning",1},sampler=new object[]{"sampler",0},sigmas=new object[]{"sigmas",0},latent_image=new object[]{"reference",2}}),
            ["decode"]=Node("VAEDecodeTiled",new{samples=new object[]{"sample",0},vae=new object[]{"model",2},tile_size=256,overlap=64,temporal_size=32,temporal_overlap=8}),
            ["save"]=Node("VHS_VideoCombine",new{images=new object[]{"decode",0},frame_rate=24,loop_count=0,filename_prefix="TubeFlow/LTX",format="video/h264-mp4",pingpong=false,save_output=true,pix_fmt="yuv420p",crf=20,save_metadata=false,trim_to_audio=false})
        };
    }
    public static async Task Generate(string reference,string prompt,string output,int seconds,bool vertical,long seed,Action<string> log,CancellationToken ct,HttpClient? client=null,bool smaller=false)
    {
        var http=client??Http;var models=await Models(ct,http);
        using var budget=CancellationTokenSource.CreateLinkedTokenSource(ct);budget.CancelAfter(TimeSpan.FromMinutes(12));var token=budget.Token;
        using var upload=new MultipartFormDataContent();upload.Add(new ByteArrayContent(await File.ReadAllBytesAsync(reference,token)),"image","tubeflow-ltx-"+Reliability.FileHash(reference)[..16]+".png");upload.Add(new StringContent("input"),"type");upload.Add(new StringContent("true"),"overwrite");
        using var uploaded=await http.PostAsync(Url("upload/image"),upload,token);uploaded.EnsureSuccessStatusCode();
        string image=JsonNode.Parse(await uploaded.Content.ReadAsStringAsync(token))?["name"]?.GetValue<string>()??throw new InvalidOperationException("LTX referansı yüklenemedi.");
        if(image.Contains('/')||image.Contains('\\')||image.Contains(".."))throw new InvalidOperationException("Geçersiz LTX referans adı.");
        var graph=Graph(models.Model,models.Encoder,image,prompt,seconds,vertical,seed,smaller);
        using var body=new StringContent(new JsonObject{["prompt"]=graph,["client_id"]=Guid.NewGuid().ToString("N")}.ToJsonString(),Encoding.UTF8,"application/json");
        using var queued=await http.PostAsync(Url("prompt"),body,token);
        if(!queued.IsSuccessStatusCode)throw new InvalidOperationException("LTX iş akışı motor tarafından kabul edilmedi: "+(int)queued.StatusCode+". Yerel motor düğümlerini kontrol et.");
        string id=JsonNode.Parse(await queued.Content.ReadAsStringAsync(token))?["prompt_id"]?.GetValue<string>()??throw new InvalidOperationException("LTX iş kimliği alınamadı.");
        if(id.Length>200||id.Length==0)throw new InvalidOperationException("LTX iş kimliği geçersiz.");
        string temporary=output+".pending";int ticks=0;bool completed=false;
        try{
            log("LTX • "+seconds+" saniyelik referanslı sahne sıraya alındı.");
            while(true){
                await Task.Delay(1500,token);var history=(await Read(http,"history/"+Uri.EscapeDataString(id),token))[id];
                if(history?["status"]?["status_str"]?.GetValue<string>()=="error"){
                    string details=history["status"]!.ToJsonString();
                    throw new InvalidOperationException(details.Contains("out of memory",StringComparison.OrdinalIgnoreCase)||details.Contains("OutOfMemory",StringComparison.OrdinalIgnoreCase)?"GPU_MEMORY: LTX için ekran kartı belleği yetmedi.":"LTX sahnesi üretilemedi; yerel motor işlem kaydını kontrol et.");
                }
                if(history?["outputs"]?["save"]?["gifs"] is JsonArray videos&&videos.FirstOrDefault() is JsonObject video){
                    string Part(string field)=>Uri.EscapeDataString(video[field]?.GetValue<string>()??"");
                    using var response=await http.GetAsync(Url("view?filename="+Part("filename")+"&subfolder="+Part("subfolder")+"&type="+Part("type")),HttpCompletionOption.ResponseHeadersRead,token);response.EnsureSuccessStatusCode();
                    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
                    await using(var input=await response.Content.ReadAsStreamAsync(token))await using(var file=File.Create(temporary)){byte[] buffer=new byte[65536];long total=0;int count;while((count=await input.ReadAsync(buffer,token))>0){total+=count;if(total>100_000_000)throw new InvalidOperationException("LTX sahne dosyası boyut sınırını aştı.");await file.WriteAsync(buffer.AsMemory(0,count),token);}if(total<100)throw new InvalidOperationException("LTX boş video döndürdü.");}
                    File.Move(temporary,output,true);completed=true;return;
                }
                if(++ticks%20==0)log("LTX • sahne hesaplanıyor • geçen süre "+(ticks*1.5).ToString("0")+" saniye.");
            }
        }catch(OperationCanceledException){ct.ThrowIfCancellationRequested();throw new InvalidOperationException("LTX sahne üretimi zaman sınırını aştı; hazır işler korundu.");}
        finally{
            if(!completed){
            using var cleanup=new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try{using var remove=new StringContent(new JsonObject{["delete"]=new JsonArray(id)}.ToJsonString(),Encoding.UTF8,"application/json");using var removed=await http.PostAsync(Url("queue"),remove,cleanup.Token);var state=await Read(http,"queue",cleanup.Token);if(state["queue_running"] is JsonArray running&&running.Any(x=>x is JsonArray row&&row.Count>1&&row[1]?.GetValue<string>()==id)){using var interrupt=new StringContent(new JsonObject{["prompt_id"]=id}.ToJsonString(),Encoding.UTF8,"application/json");using var stopped=await http.PostAsync(Url("interrupt"),interrupt,cleanup.Token);}}catch(Exception){log("LTX durdurma yanıtı alınamadı; yalnız bu işin durumu kontrol edilmeli.");}
            }
            if(File.Exists(temporary))File.Delete(temporary);
        }
    }
}
