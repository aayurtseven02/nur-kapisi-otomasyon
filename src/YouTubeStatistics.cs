using System.Net.Http.Headers;
using System.Text.Json;

namespace AnkaStudio;

public sealed class ChannelVideoStatistics
{
    public string Id {get;set;}="";
    public string Title {get;set;}="";
    public string Kind {get;set;}="Belirlenmedi";
    public string Visibility {get;set;}="";
    public DateTimeOffset Published {get;set;}
    public DateTimeOffset? ScheduledAt {get;set;}
    public long Views {get;set;}
    public long Likes {get;set;}
    public long Comments {get;set;}
    public long? PeriodViews {get;set;}
    public double? WatchHours {get;set;}
    public double? Retention {get;set;}
    public double DailyAverage=>Views/Math.Max(1,(DateTimeOffset.UtcNow-Published).TotalDays);
}
public sealed class ChannelStatistics
{
    public string ChannelId {get;set;}="";
    public string ChannelName {get;set;}="";
    public DateTimeOffset Updated {get;set;}
    public int Days {get;set;}
    public long? Subscribers {get;set;}
    public long ChannelViews {get;set;}
    public double? NormalWatchHours365 {get;set;}
    public long? ShortsEngagedViews90 {get;set;}
    public string Note {get;set;}="";
    public List<ChannelVideoStatistics> Videos {get;set;}=[];
}
public sealed class YouTubeStatistics(Store store,HttpClient? client=null)
{
    static readonly HttpClient Shared=new(){Timeout=TimeSpan.FromSeconds(45)};
    readonly HttpClient http=client??Shared;
    static long Number(JsonElement root,string name)=>root.TryGetProperty(name,out var value)&&long.TryParse(value.ToString(),out var number)?number:0;
    static string ContentKind(string? value)=>(value??"").Replace("_","").ToUpperInvariant() switch {"SHORTS"=>"Shorts","VIDEOONDEMAND"=>"Normal video","LIVESTREAM"=>"Canlı yayın",_=>"Belirlenmedi"};
    async Task<JsonDocument> Get(string url,string token,CancellationToken ct)
    {
        using var request=new HttpRequestMessage(HttpMethod.Get,url);request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
        using var response=await http.SendAsync(request,ct);
        if(!response.IsSuccessStatusCode){
            var error=await response.Content.ReadAsStringAsync(ct);
            string detail=error.Contains("SERVICE_DISABLED",StringComparison.OrdinalIgnoreCase)||error.Contains("accessNotConfigured",StringComparison.OrdinalIgnoreCase)
                ?"Google Cloud projesinde "+(url.Contains("youtubeanalytics")?"YouTube Analytics API":"YouTube Data API")+" etkin değil. Google Cloud → API kitaplığı bölümünden etkinleştir."
                :error.Contains("insufficientPermissions",StringComparison.OrdinalIgnoreCase)||error.Contains("ACCESS_TOKEN_SCOPE_INSUFFICIENT",StringComparison.OrdinalIgnoreCase)
                ?"Google istatistik okuma izni eksik. İstatistik izni ver tuşuyla yeniden bağlan.":"";
            throw new ProviderException("YouTube istatistik",(int)response.StatusCode,detail);
        }
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
    }
    public async Task<ChannelStatistics> Fetch(int days,Action<string> report,CancellationToken ct,bool includeAnalytics=true)
    {
        var token=await new YouTube(store,http).Token(ct);
        report("Kanal bilgileri alınıyor…");
        using var channel=await Get("https://www.googleapis.com/youtube/v3/channels?part=snippet,statistics,contentDetails&mine=true",token,ct);
        var item=channel.RootElement.GetProperty("items").EnumerateArray().FirstOrDefault();
        if(item.ValueKind!=JsonValueKind.Object)throw new InvalidOperationException("Bağlı kanal bulunamadı.");
        var result=new ChannelStatistics {ChannelId=item.GetProperty("id").GetString()!,ChannelName=item.GetProperty("snippet").GetProperty("title").GetString()!,Days=days};
        var channelStats=item.GetProperty("statistics");result.ChannelViews=Number(channelStats,"viewCount");
        if(!channelStats.TryGetProperty("hiddenSubscriberCount",out var hidden)||!hidden.GetBoolean())result.Subscribers=Number(channelStats,"subscriberCount");
        var playlist=item.GetProperty("contentDetails").GetProperty("relatedPlaylists").GetProperty("uploads").GetString()!;
        var ids=new HashSet<string>();var pages=new HashSet<string>();string page="";
        do{
            if(!pages.Add(page))throw new InvalidOperationException("YouTube aynı liste sayfasını tekrar döndürdü; önceki liste korundu.");
            report($"Kanalın video listesi alınıyor • {ids.Count} video…");
            using var list=await Get("https://www.googleapis.com/youtube/v3/playlistItems?part=contentDetails&maxResults=50&playlistId="+Uri.EscapeDataString(playlist)+"&pageToken="+Uri.EscapeDataString(page),token,ct);
            foreach(var video in list.RootElement.GetProperty("items").EnumerateArray())ids.Add(video.GetProperty("contentDetails").GetProperty("videoId").GetString()!);
            page=list.RootElement.TryGetProperty("nextPageToken",out var next)?next.GetString()!:"";
        }while(page!="");
        var local=store.Episodes.SelectMany(e=>e.Videos).Where(v=>v.YouTubeId!="").GroupBy(v=>v.YouTubeId).ToDictionary(g=>g.Key,g=>g.First());
        foreach(var batch in ids.Chunk(50)){
            report($"İzlenme sayıları alınıyor • {result.Videos.Count}/{ids.Count}…");
            using var videos=await Get("https://www.googleapis.com/youtube/v3/videos?part=snippet,statistics,status&id="+Uri.EscapeDataString(string.Join(',',batch)),token,ct);
            foreach(var video in videos.RootElement.GetProperty("items").EnumerateArray()){
                var id=video.GetProperty("id").GetString()!;var snippet=video.GetProperty("snippet");var stats=video.GetProperty("statistics");
                result.Videos.Add(new ChannelVideoStatistics {Id=id,Title=snippet.GetProperty("title").GetString()!,Published=DateTimeOffset.Parse(snippet.GetProperty("publishedAt").GetString()!),Visibility=video.GetProperty("status").GetProperty("privacyStatus").GetString()!,ScheduledAt=video.GetProperty("status").TryGetProperty("publishAt",out var scheduled)&&DateTimeOffset.TryParse(scheduled.GetString(),out var scheduleDate)?scheduleDate:null,Views=Number(stats,"viewCount"),Likes=Number(stats,"likeCount"),Comments=Number(stats,"commentCount"),Kind=local.TryGetValue(id,out var saved)?saved.Short?"Shorts":"Normal video":"Belirlenmedi"});
            }
        }
        if(!includeAnalytics){
            var prior=Json.Read(Path.Combine(store.Root,"cache","youtube-channel-statistics.json"),new ChannelStatistics());
            if(prior.ChannelId==result.ChannelId)foreach(var video in result.Videos.Where(v=>v.Kind=="Belirlenmedi")){var saved=prior.Videos.FirstOrDefault(v=>v.Id==video.Id);if(saved!=null)video.Kind=saved.Kind;}
            result.Updated=DateTimeOffset.UtcNow;return result;
        }
        async Task<JsonDocument> Analytics(int period,string metrics,string dimensions="",string filters=""){
            var end=DateTime.UtcNow.Date.AddDays(-1);var start=end.AddDays(1-period);
            return await Get("https://youtubeanalytics.googleapis.com/v2/reports?ids=channel==MINE&startDate="+start.ToString("yyyy-MM-dd")+"&endDate="+end.ToString("yyyy-MM-dd")+"&metrics="+Uri.EscapeDataString(metrics)+(dimensions!=""?"&dimensions="+Uri.EscapeDataString(dimensions):"")+(filters!=""?"&filters="+Uri.EscapeDataString(filters):"")+(dimensions.Split(',').Contains("video")?"&sort=-views&maxResults=200":""),token,ct);
        }
        try{
            report("Dönem raporu ve izlenme saatleri alınıyor…");
            {
                using var data=await Analytics(days,"views,estimatedMinutesWatched,averageViewPercentage","video");
                if(data.RootElement.TryGetProperty("rows",out var rows))foreach(var row in rows.EnumerateArray()){
                    var video=result.Videos.Find(v=>v.Id==row[0].GetString());if(video==null)continue;
                    video.PeriodViews=row[1].GetInt64();video.WatchHours=row[2].GetDouble()/60;video.Retention=row[3].GetDouble();
                }
            }
            var previous=Json.Read(Path.Combine(store.Root,"cache","youtube-channel-statistics.json"),new ChannelStatistics());
            if(previous.ChannelId==result.ChannelId)foreach(var video in result.Videos.Where(v=>v.Kind=="Belirlenmedi")){
                var saved=previous.Videos.FirstOrDefault(v=>v.Id==video.Id);if(saved!=null&&saved.Kind!="Belirlenmedi")video.Kind=saved.Kind;
            }
            var unknown=result.Videos.Where(v=>v.Kind=="Belirlenmedi").Take(200).ToArray();int classified=0;
            var updates=new Progress<string>(report);
            await Parallel.ForEachAsync(unknown,new ParallelOptions{MaxDegreeOfParallelism=4,CancellationToken=ct},async(video,_)=>{
                try{using var type=await Analytics(365,"views","creatorContentType","video=="+video.Id);
                    if(type.RootElement.TryGetProperty("rows",out var rows)&&rows.GetArrayLength()>0){var row=rows.EnumerateArray().OrderByDescending(r=>r[1].GetDouble()).First();video.Kind=ContentKind(row[0].GetString());}
                }catch(Exception ex)when(ex is not OperationCanceledException){}
                ((IProgress<string>)updates).Report($"Video türleri doğrulanıyor • {Interlocked.Increment(ref classified)}/{unknown.Length}…");
            });
            using var normal=await Analytics(365,"estimatedMinutesWatched","creatorContentType");
            if(normal.RootElement.TryGetProperty("rows",out var normalRows))foreach(var row in normalRows.EnumerateArray())if(ContentKind(row[0].GetString())=="Normal video")result.NormalWatchHours365=row[1].GetDouble()/60;
            using var shorts=await Analytics(90,"engagedViews","creatorContentType");
            if(shorts.RootElement.TryGetProperty("rows",out var shortRows))foreach(var row in shortRows.EnumerateArray())if(ContentKind(row[0].GetString())=="Shorts")result.ShortsEngagedViews90=row[1].GetInt64();
            result.Note+="Dönem raporu en çok izlenen 200 videoyu kapsar; boş alan veri yok/raporda yok anlamındadır. Analitik veriler gecikmeli olabilir.";
        }catch(Exception ex) when(ex is not OperationCanceledException){result.Note+="Dönem analitiği alınamadı. "+(ex is ProviderException?ex.Message:"Rapor yanıtı kullanılamadı.")+" Toplam izlenmeler korundu.";}
        result.Updated=DateTimeOffset.UtcNow;return result;
    }
    public static string Recommend(ChannelStatistics data)
    {
        var notes=new List<string>();
        foreach(var kind in new[]{"Normal video","Shorts"}){
            var group=data.Videos.Where(v=>v.Visibility=="public"&&v.Kind==kind).ToArray();if(group.Length==0)continue;
            var most=group.OrderByDescending(v=>v.Views).First();notes.Add($"{kind}: En çok izlenen “{most.Title}” ({most.Views:N0}). Bu konunun devam bölümünü küçük bir denemeyle test edebilirsin.");
            var mature=group.Where(v=>(data.Updated-v.Published).TotalDays>=2).OrderByDescending(v=>v.DailyAverage).ToArray();
            if(mature.Length>1)notes.Add($"{kind}: Yayında geçen süreye göre “{mature[0].Title}” önde (günde ortalama {mature[0].DailyAverage:N1}). Bu, son günlerdeki hız değil; yayın tarihinden bugüne ortalamadır.");
            var retention=group.Where(v=>v.PeriodViews>=100&&v.Retention.HasValue).OrderByDescending(v=>v.Retention).FirstOrDefault();
            if(retention!=null)notes.Add($"{kind}: Dönem raporunda en yüksek izlenme oranı “{retention.Title}” (%{retention.Retention:N1}). Açılışını ve anlatım akışını sonraki videoda örnek alabilirsin.");
        }
        if(notes.Count==0)notes.Add("Öneri için türü belirlenmiş, yayındaki video verisi gerekiyor.");
        notes.Add("İzlenme sayısı tek başına başlığın veya kapağın başarı nedeni olduğunu kanıtlamaz. Öneriler garanti değildir; türler kendi içinde karşılaştırılır.");return string.Join("\r\n\r\n",notes);
    }
}
