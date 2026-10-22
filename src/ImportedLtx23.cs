using System.Text.Json.Nodes;
namespace AnkaStudio;

public static class ImportedLtx23
{
    public const string Model="LTX 2.3 • RTX 3060 • 5 sn • 768×512";
    public const string Model480="LTX 2.3 • 480p deneme • 736×480";
    public static bool IsModern(string engine)=>engine is Model or Model480;
    public const string Legacy="LTX 0.9.8 • Hafif";
    public const string FileName="anka-ltx23-rtx3060.json";
    public static readonly string[] Choices=[AnimationVideo.Model,Model,Model480,Legacy];
    public static string PathName=>Path.Combine(AppContext.BaseDirectory,"tools","workflows",FileName);
    public static JsonObject Graph(string image,string prompt,long seed,int width=768,int height=512)
    {
        if(string.IsNullOrWhiteSpace(image)||prompt.Length<10||prompt.Length>2400||seed<0)throw new InvalidOperationException("LTX 2.3 referansı veya sahne talimatı geçersiz.");
        if(width<64||height<64||width%32!=0||height%32!=0)throw new InvalidOperationException("LTX çözünürlüğü 32 piksel adımına uygun olmalı.");
        var graph=JsonNode.Parse(File.ReadAllText(PathName))!.AsObject();
        graph["2004"]!["inputs"]!["image"]=image;
        graph["2483"]!["inputs"]!["text"]=prompt;
        graph["3059"]!["inputs"]!["width"]=width;graph["3059"]!["inputs"]!["height"]=height;graph["4981"]!["inputs"]!["resize_type.longer_size"]=Math.Max(width,height);
        if(prompt.Contains("cartoon",StringComparison.OrdinalIgnoreCase)||prompt.Contains("animated",StringComparison.OrdinalIgnoreCase))graph["2612"]!["inputs"]!["text"]="photorealistic, live action, text, subtitles, watermark, malformed hands, changing face, changing clothes, flickering, cuts";
        graph["4832"]!["inputs"]!["noise_seed"]=seed;
        graph["4977"]!["inputs"]!["value"]=false;
        graph["3159"]!["inputs"]!["strength"]=1.0;
        graph["4528"]!["inputs"]!["video_latent"]=new JsonArray("3159",0);
        return graph;
    }
    public static async Task Check(HttpClient http,CancellationToken ct)
    {
        using var response=await http.GetAsync("http://127.0.0.1:8188/object_info",ct);response.EnsureSuccessStatusCode();
        var metadata=JsonNode.Parse(await response.Content.ReadAsStringAsync(ct))!;
        var graph=Graph("reference.png","A person moves naturally.",42);
        foreach(var node in graph.Select(x=>x.Value!)){
            string type=node["class_type"]!.GetValue<string>();var schema=metadata[type];
            if(schema==null)throw new InvalidOperationException("LTX 2.3 düğümü eksik: "+type);
            foreach(var input in node["inputs"]!.AsObject()){
                if(input.Value is not JsonValue value||!value.TryGetValue<string>(out var name)||!name.EndsWith(".safetensors")&&!name.EndsWith(".gguf"))continue;
                var spec=schema["input"]?["required"]?[input.Key]??schema["input"]?["optional"]?[input.Key];
                var options=spec?[0] as JsonArray??spec?[1]?["options"] as JsonArray;
                if(options!=null&&!options.Any(x=>x?.GetValue<string>()==name))throw new InvalidOperationException("LTX 2.3 model dosyası eksik: "+name);
            }
        }
    }
}
