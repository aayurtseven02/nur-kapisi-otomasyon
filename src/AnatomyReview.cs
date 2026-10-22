using System.Text.Json;
namespace AnkaStudio;

public static class AnatomyReview
{
    public sealed class Configuration{public string Python{get;set;}="";public string Packages{get;set;}="";public string Model{get;set;}="";}
    public static async Task<int?> CountHands(Store store,string image,CancellationToken ct)
    {
        string config=Path.Combine(store.Root,"engines","anatomy.json");if(!File.Exists(config))return null;
        var settings=Json.Read(config,new Configuration());
        if(!File.Exists(settings.Python)||!Directory.Exists(settings.Packages)||!File.Exists(settings.Model))throw new InvalidOperationException("Yerel anatomi denetleyicisinin dosyaları bulunamadı.");
        using var budget=CancellationTokenSource.CreateLinkedTokenSource(ct);budget.CancelAfter(TimeSpan.FromMinutes(2));
        string raw=await Media.Exec(settings.Python,["-X","utf8",Path.Combine(AppContext.BaseDirectory,"tools","visual-quality","hands.py"),"--packages",settings.Packages,"--model",settings.Model,"--image",image],null,budget.Token);
        using var result=JsonDocument.Parse(raw);int count=result.RootElement.GetProperty("Count").GetInt32();if(count is <0 or >12)throw new InvalidOperationException("El tespit sonucu geçersiz.");
        return count;
    }
}
