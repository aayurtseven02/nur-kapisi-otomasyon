using System.Text.RegularExpressions;
namespace AnkaStudio;

public sealed class CharacterReference
{
    public string Id {get;set;}="";
    public string Name {get;set;}="";
    public string Role {get;set;}="Yaşlı erkek";
    public string Style {get;set;}="3D çizgi film";
    public string File {get;set;}="";
    public string Description {get;set;}="";
}

public static class CharacterReferences
{
    public static List<CharacterReference> List(Store store)
    {
        string file=Path.Combine(AppContext.BaseDirectory,"Assets","Characters","elderly-man-cartoon.png");
        var list=Json.Read<List<CharacterReference>>(Path.Combine(store.Root,"character-references","index.json"),[]);
        if(System.IO.File.Exists(file))list.Insert(0,new CharacterReference{Id="elderly-man-cartoon-v1",Name="Yaşlı adam • 3D çizgi film",File=file,Description="Kind elderly man, brown eyes, short white beard and moustache, cream head covering with black agal, long beige garment and brown sash. Polished warm 3D cartoon style."});
        return list.Where(x=>System.IO.File.Exists(x.File)).ToList();
    }
    public static CharacterReference? Match(IEnumerable<CharacterReference> entries,string cast,string style,int count)
    {
        if(count!=1)return null;
        string text=cast.ToLowerInvariant(),look=style.ToLowerInvariant();
        if(!Regex.IsMatch(look,@"3d|cartoon|çizgi|animasyon|animated"))return null;
        if(!Regex.IsMatch(text,@"\b(elderly|old|aged|senior)\b|yaşlı")||!Regex.IsMatch(text,@"\b(man|male|adam|erkek)\b"))return null;
        return entries.FirstOrDefault(x=>x.Role=="Yaşlı erkek"&&x.Style=="3D çizgi film");
    }
    public static void AutoSelect(Store store,Episode episode,string cast,int count,Action<string> log)
    {
        var flow=episode.Creation;if(flow==null)return;
        if(flow.SceneReferenceFile!=""&&flow.SceneReferenceCatalogId=="")return;
        var reference=Match(List(store),cast,episode.AiStoryStyle,count);
        if(reference==null){
            if(flow.SceneReferenceCatalogId!=""){StoryImages.InvalidateReference(episode);flow.SceneReferenceFile="";flow.SceneReferenceHash="";flow.SceneReferenceCatalogId="";}
            return;
        }
        if(flow.SceneReferenceCatalogId==reference.Id&&System.IO.File.Exists(flow.SceneReferenceFile))return;
        StoryImages.SelectReference(store,episode,flow,reference.File);flow.SceneReferenceCatalogId=reference.Id;
        log("Karakter arşivinden seçildi: "+reference.Name+" • bu hikâyenin sahnelerinde aynı referans kullanılacak.");
    }
}
