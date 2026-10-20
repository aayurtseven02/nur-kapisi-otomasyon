using System.Text.Json;
namespace AnkaStudio;

public static class KeylessResearch
{
    public static async Task<List<(SourceRef Source,string Text)>> Collect(HttpClient http,string topic,CancellationToken ct)
    {
        var rows=new List<(SourceRef Source,string Text)>();
        if(string.IsNullOrWhiteSpace(topic))return rows;
        foreach(string language in new[]{"tr","en"}){
            ct.ThrowIfCancellationRequested();
            string url=$"https://{language}.wikipedia.org/w/api.php?action=query&generator=search&gsrnamespace=0&gsrlimit=3&prop=extracts%7Cinfo&exintro=1&explaintext=1&inprop=url&format=json&gsrsearch="+Uri.EscapeDataString(topic[..Math.Min(200,topic.Length)]);
            try{
                using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(15));
                using var request=new HttpRequestMessage(HttpMethod.Get,url);request.Headers.UserAgent.ParseAdd("TubeFlow/0.4.30 (research; https://github.com/aayurtseven02/nur-kapisi-otomasyon)");
                using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,timeout.Token);response.EnsureSuccessStatusCode();
                using var doc=JsonDocument.Parse(await Reliability.ReadText(response,2_000_000,timeout.Token));
                if(!doc.RootElement.TryGetProperty("query",out var query)||!query.TryGetProperty("pages",out var pages))continue;
                foreach(var page in pages.EnumerateObject()){
                    var row=page.Value;
                    if(!row.TryGetProperty("extract",out var extract)||extract.ValueKind!=JsonValueKind.String)continue;
                    string text=extract.GetString()?.Trim()??"",link=row.TryGetProperty("fullurl",out var full)?full.GetString()??"":"";
                    if(text.Length<150||!Uri.TryCreate(link,UriKind.Absolute,out var uri)||uri.Scheme!="https"||uri.Host!=$"{language}.wikipedia.org")continue;
                    rows.Add((new SourceRef{Title=row.GetProperty("title").GetString()??topic,Url=link},text[..Math.Min(6000,text.Length)]));
                }
            }catch(HttpRequestException){}catch(JsonException){}catch(OperationCanceledException)when(!ct.IsCancellationRequested){}
            if(rows.Count>=2)break;
        }
        return rows.DistinctBy(x=>x.Source.Url).ToList();
    }
}
