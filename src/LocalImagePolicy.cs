using System.Text.Json.Nodes;
namespace AnkaStudio;

public static class LocalImagePolicy
{
    public static (int W,int H) Resolve(string resolution,string quality,bool vertical,bool square,long? freeBytes=null)
    {
        int width=quality=="Hızlı"?1024:quality=="Dengeli"?1280:1536,height=width*9/16;
        if(square){width=height=768;}
        else if(vertical){(width,height)=(height,width);}
        if(resolution!="auto"&&!string.IsNullOrWhiteSpace(resolution)){
            string[] values=resolution.Replace('×','x').Replace(" ","").Split('x');
            if(values.Length!=2||!int.TryParse(values[0],out width)||!int.TryParse(values[1],out height)||width<256||height<256||width>2048||height>2048)throw new InvalidOperationException("Geçerli piksel çözünürlüğü seç.");
            if(square&&width!=height||!square&&(vertical?Math.Abs(width/(double)height-9d/16):Math.Abs(width/(double)height-16d/9))>.025)throw new InvalidOperationException("Çözünürlük video oranına uymuyor.");
        }
        if(freeBytes is long free){
            int max=free<3L*1024*1024*1024?768:free<6L*1024*1024*1024?1024:free<10L*1024*1024*1024?1280:1536;
            if(Math.Max(width,height)>max){if(square)width=height=max;else if(vertical){height=max;width=max*9/16;}else{width=max;height=max*9/16;}}
        }
        return(width,height);
    }
    public static void Resize(JsonObject graph,int width,int height){foreach(var node in graph.Select(x=>x.Value).OfType<JsonObject>())if(node["class_type"]?.GetValue<string>() is "EmptyLatentImage" or "EmptySD3LatentImage" or "EmptyFlux2LatentImage" or "Flux2Scheduler" or "ImageScale"){node["inputs"]!["width"]=width;node["inputs"]!["height"]=height;}}
}
