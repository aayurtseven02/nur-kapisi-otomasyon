using System.Text.Json.Nodes;
namespace AnkaStudio;

public static class LocalImageProfiles
{
    public static readonly string[] Labels=["Hafif","Orta","Yüksek"];
    public static int Rank(string? value)=>value is "Hafif" or "Hızlı" or "Minimum"?0:value is "Yüksek" or "Ultra sinematik" or "Maksimum"?2:1;
    public static string Label(string? value)=>Labels[Rank(value)];
    public static string Legacy(string? value)=>new[]{"Hızlı","Dengeli","Ultra sinematik"}[Rank(value)];
    public static string ForMemory(string requested,long? free)=>Labels[Math.Min(Rank(requested),free is not long bytes||bytes<4L*1024*1024*1024?0:bytes<8L*1024*1024*1024?1:2)];
    public static int Edge(string quality)=>new[]{768,1024,1280}[Rank(quality)];
    public static string DeviceProfile(JsonNode? device)
    {
        string backend=device?["type"]?.GetValue<string>()??"";
        long total=device?["vram_total"]?.GetValue<long>()??0,free=device?["vram_free"]?.GetValue<long>()??0;
        if(backend is "" or "cpu"||total<=0)return "Hafif";
        int capacity=total<6L*1024*1024*1024?0:total<10L*1024*1024*1024?1:2;
        return Labels[Math.Min(capacity,Rank(ForMemory("Yüksek",Math.Min(total,free))))];
    }
    public static void ApplyImported(JsonObject graph,string quality,int width,int height)
    {
        int rank=Rank(quality);
        ImportedFluxWorkflow.StableDetailing(graph,width,height);
        foreach(var node in graph.Select(x=>x.Value).OfType<JsonObject>()){
            var type=node["class_type"]?.GetValue<string>();var inputs=node["inputs"];
            if(type=="KSampler")inputs!["steps"]=Math.Min(inputs["steps"]!.GetValue<int>(),new[]{8,16,25}[rank]);
            if(type=="FaceDetailer"){
                inputs!["steps"]=Math.Min(inputs["steps"]!.GetValue<int>(),new[]{4,8,12}[rank]);
                inputs["guide_size"]=Math.Min(inputs["guide_size"]!.GetValue<double>(),new[]{256,384,512}[rank]);
                inputs["max_size"]=Math.Min(inputs["max_size"]!.GetValue<double>(),new[]{512,768,1024}[rank]);
            }
        }
    }
}
