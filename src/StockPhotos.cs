using System.Net.Http;
using System.Text.Json;
namespace AnkaStudio;

public sealed partial class Services
{
    public async Task StockPhoto(Scene scene,bool vertical,string folder,HashSet<long> used,CancellationToken ct)
    {
        Directory.CreateDirectory(folder);
        var errors=new List<string>();
        foreach(var provider in new[]{"Pexels","Pixabay"}) {
            var key=store.Key(provider=="Pexels"?"PEXELS_API_KEY":"PIXABAY_API_KEY");if(key=="")continue;
            foreach(var query in StockQueries(scene.VisualQuery).Take(4)) {
                ct.ThrowIfCancellationRequested();
                try {
                    string address=provider=="Pexels" ? "https://api.pexels.com/v1/search?query="+Uri.EscapeDataString(query)+"&per_page=40&orientation="+(vertical?"portrait":"landscape") : "https://pixabay.com/api/?key="+Uri.EscapeDataString(key)+"&q="+Uri.EscapeDataString(query)+"&image_type=photo&safesearch=true&per_page=40&orientation="+(vertical?"vertical":"horizontal");
                    var cache=Path.Combine(folder,"photo-search-"+TimedNarration.TextHash(provider+query+vertical)+".json");
                    string raw;
                    if(File.Exists(cache) && File.GetLastWriteTimeUtc(cache)>DateTime.UtcNow.AddHours(-24))raw=await File.ReadAllTextAsync(cache,ct);
                    else {
                        using var req=new HttpRequestMessage(HttpMethod.Get,address);if(provider=="Pexels")req.Headers.TryAddWithoutValidation("Authorization",key);
                        using var response=await http.SendAsync(req,ct);if(!response.IsSuccessStatusCode)throw new ProviderException(provider,(int)response.StatusCode);
                        raw=await response.Content.ReadAsStringAsync(ct);if(raw.Length>2_000_000)throw new InvalidOperationException("Fotoğraf yanıtı çok büyük.");await File.WriteAllTextAsync(cache,raw,ct);
                    }
                    using var document=JsonDocument.Parse(raw);
                    foreach(var item in document.RootElement.GetProperty(provider=="Pexels"?"photos":"hits").EnumerateArray()) {
                        if(StockSearch.Blocked(store.Settings,item.GetProperty(provider=="Pexels"?"url":"pageURL").GetString()??""))continue;
                        long id=item.GetProperty("id").GetInt64();long token=provider=="Pexels"?id:-id;if(used.Contains(token))continue;
                        string metadata=provider=="Pexels"?item.GetProperty("alt").GetString()??"":item.GetProperty("tags").GetString()??"";
                        if(metadata!="" && !StockSearch.Appropriate(query,metadata))continue;
                        string url=provider=="Pexels"?item.GetProperty("src").GetProperty("large2x").GetString()!:item.GetProperty("largeImageURL").GetString()!;
                        if(!Uri.TryCreate(url,UriKind.Absolute,out var uri)||uri.Scheme!="https"||!(uri.Host=="images.pexels.com" || uri.Host=="pixabay.com" || uri.Host.EndsWith(".pixabay.com",StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException("Fotoğraf adresi geçersiz.");
                        var file=Path.Combine(folder,$"photo-{provider}-{id}.jpg");
                        if(!File.Exists(file)){using var download=await http.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,ct);await Reliability.DownloadResponse(download,file,20L*1024*1024,ct);}
                        using(var image=System.Drawing.Image.FromFile(file)){if(image.Width<320||image.Height<320||(long)image.Width*image.Height>40_000_000)continue;}
                        scene.StockFile=file;scene.StockUrl=item.GetProperty(provider=="Pexels"?"url":"pageURL").GetString()??"";
                        scene.Creator=item.GetProperty(provider=="Pexels"?"photographer":"user").GetString()??"";
                        scene.LicenseUrl=provider=="Pexels"?"https://www.pexels.com/license/":"https://pixabay.com/service/license-summary/";
                        scene.Attribution=$"Photo by {scene.Creator} on {provider} • {scene.StockUrl}";scene.StockMetadata=metadata;scene.RightsConfirmed=true;scene.UserLocal=false;used.Add(token);
                        log(provider+" fotoğrafı seçildi: "+query);return;
                    }
                } catch(Exception ex) when(ex is not OperationCanceledException) {errors.Add(provider+": "+(ex is ProviderException?ex.Message:"Fotoğraf alınamadı."));}
            }
        }
        throw new InvalidOperationException("Konuya uygun fotoğraf bulunamadı. Pexels / Pixabay anahtarlarını ve sahne arama temasını kontrol et. "+string.Join(" • ",errors.Distinct()));
    }
}
