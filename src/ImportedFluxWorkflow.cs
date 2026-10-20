using System.Text.Json.Nodes;
namespace AnkaStudio;

// The imported Nur Kapisi graph keeps its tuned PuLID and FaceDetailer chain.
// A reference is supplied per request; an empty template never loads an old photo.
public static class ImportedFluxWorkflow
{
    public const string Model = "FLUX Schnell • PuLID + FaceDetailer";
    public const string FileName = "anka-flux-pulid-detailer.json";
    static KeyValuePair<string,JsonNode?>[] IdentityNodes(JsonObject graph)=>graph.Where(x=>x.Value?["class_type"]?.GetValue<string>()=="ApplyPulidFlux"&&x.Value?["_meta"]?["tubeflow_user_reference"]?.GetValue<bool>()==true).ToArray();
    public static void StableDetailing(JsonObject graph,int width,int height)
    {
        // The face detector's ONNX provider is independent of FLUX/CUDA.
        // Keep it off scarce GPU memory; generation remains on the GPU.
        foreach(var node in graph.Select(x=>x.Value).OfType<JsonObject>()){
            if(node["class_type"]?.GetValue<string>()=="PulidFluxInsightFaceLoader")node["inputs"]!["provider"]="CPU";
            if(node["class_type"]?.GetValue<string>()!="FaceDetailer")continue;
            int maximum=Math.Max(width,height)<=768?512:Math.Max(width,height)<=1024?768:1024;
            var inputs=node["inputs"]!;
            inputs["guide_size"]=Math.Min(inputs["guide_size"]!.GetValue<double>(),maximum==512?384:512);
            inputs["max_size"]=Math.Min(inputs["max_size"]!.GetValue<double>(),maximum);
            inputs["tiled_encode"]=true;inputs["tiled_decode"]=true;
        }
    }
    public static void WithoutReference(JsonObject graph)
    {
        foreach(var identity in IdentityNodes(graph)){
            var model=identity.Value!["inputs"]!["model"]!.DeepClone();
            foreach(var node in graph.Where(x=>x.Key!=identity.Key).Select(x=>x.Value).OfType<JsonObject>())
                foreach(var input in node["inputs"]!.AsObject().ToArray())
                    if(input.Value is JsonArray link&&link[0]?.GetValue<string>()==identity.Key)node["inputs"]![input.Key]=model.DeepClone();
            graph.Remove(identity.Key);
        }
        // ComfyUI validates unused nodes too: remove the now unused blank loader.
        var reachable=new HashSet<string>();
        void Visit(string id){if(!reachable.Add(id)||graph[id] is not JsonObject node)return;foreach(var input in node["inputs"]!.AsObject())if(input.Value is JsonArray link&&link.Count==2&&link[0] is JsonValue value&&value.TryGetValue<string>(out var parent))Visit(parent);}
        foreach(var output in graph.Where(x=>x.Value?["class_type"]?.GetValue<string>() is "SaveImage" or "PreviewImage").ToArray())Visit(output.Key);
        foreach(string id in graph.Select(x=>x.Key).Where(x=>!reachable.Contains(x)).ToArray())graph.Remove(id);
    }
    public static bool Bind(JsonObject graph,string uploaded,long seed,int castCount,IReadOnlyList<int>? characterIds,int referenceWidth,bool styleReference)
    {
        var originals=IdentityNodes(graph);if(originals.Length==0)return false;
        var subjects=characterIds??Enumerable.Range(0,castCount).ToArray();
        if(styleReference||subjects.Count==0){WithoutReference(graph);return false;}
        if(originals.Length!=1||castCount<1||castCount>4||subjects.Distinct().Count()!=subjects.Count||subjects.Any(x=>x<0||x>=castCount))throw new InvalidOperationException("PuLID karakter referansı seçimi geçersiz.");
        var original=originals[0];var inputs=original.Value!["inputs"]!.AsObject();
        string imageId=inputs["image"]![0]!.GetValue<string>();
        if(graph[imageId]?["class_type"]?.GetValue<string>()!="LoadImage")throw new InvalidOperationException("PuLID referans yükleyicisi bulunamadı.");
        graph[imageId]!["inputs"]!["image"]=uploaded;
        JsonNode previous=inputs["model"]!.DeepClone();
        var consumers=graph.Where(x=>x.Key!=original.Key).SelectMany(x=>x.Value!["inputs"]!.AsObject().Where(p=>p.Value is JsonArray link&&link[0]?.GetValue<string>()==original.Key).Select(p=>(Node:x.Key,Field:p.Key))).ToArray();
        foreach(int subject in subjects){
            string id="tf_user_pulid_"+subject,reference=imageId;
            if(castCount>1){int slot=referenceWidth/castCount;if(slot<1)throw new InvalidOperationException("Karakter referansı görseli çok küçük.");reference="tf_user_face_"+subject;graph[reference]=new JsonObject{["class_type"]="ImageCrop",["inputs"]=new JsonObject{["image"]=new JsonArray(imageId,0),["width"]=slot,["height"]=slot,["x"]=slot*subject,["y"]=0}};}
            graph[id]=original.Value.DeepClone();graph[id]!["inputs"]!["model"]=previous.DeepClone();graph[id]!["inputs"]!["image"]=new JsonArray(reference,0);previous=new JsonArray(id,0);
        }
        foreach(var consumer in consumers)graph[consumer.Node]!["inputs"]![consumer.Field]=previous.DeepClone();
        graph.Remove(original.Key);
        foreach(var node in graph.Select(x=>x.Value).OfType<JsonObject>())if(node["class_type"]?.GetValue<string>() is "KSampler" or "FaceDetailer")node["inputs"]!["seed"]=seed;
        return true;
    }
}
