using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace AnkaStudio;

public sealed record FreeMedia(string Name,string Kind,string Url,string Source,string Creator,string License,string LicenseUrl,string Attribution);
public sealed class FreeMediaLibrary(Store store,HttpClient? client=null)
{
    static readonly HttpClient Shared=new(){Timeout=TimeSpan.FromSeconds(45)};
    readonly HttpClient http=client??Shared;
    static string Plain(string value)=>WebUtility.HtmlDecode(Regex.Replace(value,"<[^>]*>"," ")).Trim();
    public static bool DownloadHost(Uri uri)=>uri.Scheme=="https"&&(uri.Host=="upload.wikimedia.org"||uri.Host=="images.pexels.com"||uri.Host=="videos.pexels.com"||uri.Host=="player.vimeo.com"||uri.Host.EndsWith(".pixabay.com",StringComparison.OrdinalIgnoreCase)||uri.Host=="pixabay.com");
    public async Task<List<FreeMedia>> Search(string query,string kind,CancellationToken ct)
    {
        if(string.IsNullOrWhiteSpace(query)||query.Length>200)throw new InvalidOperationException("Aranacak konuyu 1–200 karakterle yaz.");
        string cacheFolder=Path.Combine(store.Root,"cache","free-media-search");Directory.CreateDirectory(cacheFolder);string cache=Path.Combine(cacheFolder,TimedNarration.TextHash(query+"|"+kind+"|"+TimedNarration.TextHash(store.Key("PEXELS_API_KEY")+store.Key("PIXABAY_API_KEY")))+".json");if(File.Exists(cache)&&File.GetLastWriteTimeUtc(cache)>DateTime.UtcNow.AddHours(-24))return Json.Read(cache,new List<FreeMedia>());
        string type=kind=="Fotoğraf"?"bitmap":kind=="Video"?"video":kind is "Müzik" or "Ses efekti"?"audio":"";
        string search=query+(type==""?"":" filetype:"+type);
        string url="https://commons.wikimedia.org/w/api.php?action=query&generator=search&gsrnamespace=6&gsrlimit=24&prop=imageinfo&iiprop=url%7Cextmetadata%7Cmime%7Cmediatype&format=json&gsrsearch="+Uri.EscapeDataString(search);
        using var request=new HttpRequestMessage(HttpMethod.Get,url);request.Headers.UserAgent.ParseAdd("TubeFlow/0.4.29 (media-library; https://github.com/aayurtseven02/nur-kapisi-otomasyon)");
        var results=new List<FreeMedia>();var failures=new List<string>();
        try{using var budget=CancellationTokenSource.CreateLinkedTokenSource(ct);budget.CancelAfter(TimeSpan.FromSeconds(10));using var response=await http.SendAsync(request,budget.Token);response.EnsureSuccessStatusCode();string json=await response.Content.ReadAsStringAsync(ct);if(json.Length>4_000_000)throw new InvalidOperationException("Arama yanıtı çok büyük.");results.AddRange(ParseCommons(json,kind));}catch(Exception ex)when(ex is HttpRequestException or JsonException){failures.Add("Wikimedia bağlantısı tamamlanamadı.");}catch(OperationCanceledException)when(!ct.IsCancellationRequested){failures.Add("Wikimedia zaman aşımına uğradı.");}
        if(kind is "Tümü" or "Fotoğraf" or "Video")foreach(string provider in new[]{"Pexels","Pixabay"}){
            string key=store.Key(provider=="Pexels"?"PEXELS_API_KEY":"PIXABAY_API_KEY");if(key=="")continue;
            foreach(bool video in kind=="Tümü"?new[]{false,true}:new[]{kind=="Video"}){
                ct.ThrowIfCancellationRequested();try{using var budget=CancellationTokenSource.CreateLinkedTokenSource(ct);budget.CancelAfter(TimeSpan.FromSeconds(10));results.AddRange(await SearchStock(provider,key,query,video,budget.Token));}catch(Exception ex)when(ex is HttpRequestException or JsonException or ProviderException){failures.Add(provider+" araması tamamlanamadı.");}catch(OperationCanceledException)when(!ct.IsCancellationRequested){failures.Add(provider+" zaman aşımına uğradı.");}
            }
        }
        if(results.Count==0&&failures.Count>0)throw new InvalidOperationException(string.Join(" ",failures));var unique=results.DistinctBy(x=>x.Url).ToList();if(failures.Count==0)Json.Write(cache,unique);return unique;
    }
    async Task<List<FreeMedia>> SearchStock(string provider,string key,string query,bool video,CancellationToken ct)
    {
        bool pexels=provider=="Pexels";string address=pexels?"https://api.pexels.com/v1/"+(video?"videos/search":"search")+"?per_page=12&query="+Uri.EscapeDataString(query):"https://pixabay.com/api/"+(video?"videos/":"")+"?per_page=12&safesearch=true&key="+Uri.EscapeDataString(key)+"&q="+Uri.EscapeDataString(query);
        using var request=new HttpRequestMessage(HttpMethod.Get,address);if(pexels)request.Headers.TryAddWithoutValidation("Authorization",key);
        using var response=await http.SendAsync(request,ct);if(!response.IsSuccessStatusCode)throw new ProviderException(provider,(int)response.StatusCode);string json=await response.Content.ReadAsStringAsync(ct);if(json.Length>4_000_000)throw new InvalidOperationException("Stok arama yanıtı çok büyük.");using var doc=JsonDocument.Parse(json);var result=new List<FreeMedia>();
        foreach(var row in doc.RootElement.GetProperty(pexels?video?"videos":"photos":"hits").EnumerateArray()){
            string url,source,creator,name,license=pexels?"Pexels License":"Pixabay Content License",licenseUrl=pexels?"https://www.pexels.com/license/":"https://pixabay.com/service/license-summary/";
            if(pexels){source=row.GetProperty("url").GetString()!;creator=video?row.GetProperty("user").GetProperty("name").GetString()??"":row.GetProperty("photographer").GetString()??"";name=!video&&row.TryGetProperty("alt",out var alt)?alt.GetString()??query:query;
                if(video){var candidates=row.GetProperty("video_files").EnumerateArray().Where(x=>x.GetProperty("file_type").GetString()=="video/mp4"&&x.GetProperty("width").ValueKind==JsonValueKind.Number&&x.GetProperty("width").GetInt32()<=1920).OrderByDescending(x=>x.GetProperty("width").GetInt32()).ToArray();if(candidates.Length==0)continue;url=candidates[0].GetProperty("link").GetString()!;}else url=row.GetProperty("src").GetProperty("large2x").GetString()!;
            }else{source=row.GetProperty("pageURL").GetString()!;creator=row.GetProperty("user").GetString()??"";name=row.GetProperty("tags").GetString()??query;url=video?row.GetProperty("videos").GetProperty("medium").GetProperty("url").GetString()!:row.GetProperty("largeImageURL").GetString()!;}
            if(Uri.TryCreate(url,UriKind.Absolute,out var uri)&&DownloadHost(uri))result.Add(new(name,video?"Video":"Fotoğraf",url,source,creator,license,licenseUrl,$"{name} • {creator} on {provider} • {source} • {licenseUrl}"));
        }
        return result;
    }
    public static List<FreeMedia> ParseCommons(string json,string kind)
    {
        using var doc=JsonDocument.Parse(json);var result=new List<FreeMedia>();if(!doc.RootElement.TryGetProperty("query",out var query)||!query.TryGetProperty("pages",out var pages))return result;
        foreach(var page in pages.EnumerateObject()){
            if(!page.Value.TryGetProperty("imageinfo",out var info)||info.GetArrayLength()==0)continue;var image=info[0];string mime=image.GetProperty("mime").GetString()??"",url=image.GetProperty("url").GetString()??"";
            string mediaType=image.TryGetProperty("mediatype",out var media)?media.GetString()??"":"";
            string actual=mime.StartsWith("image/")?"Fotoğraf":(mime.StartsWith("video/")||mediaType=="VIDEO")?"Video":(mime.StartsWith("audio/")||mediaType=="AUDIO")?kind=="Ses efekti"?"Ses efekti":"Müzik":"";
            if(actual==""||mime.Contains("svg")||kind!="Tümü"&&kind!=actual||!Uri.TryCreate(url,UriKind.Absolute,out var uri)||!DownloadHost(uri))continue;
            var metadata=image.GetProperty("extmetadata");string Meta(string key)=>metadata.TryGetProperty(key,out var value)&&value.TryGetProperty("value",out var text)?Plain(text.GetString()??""):"";
            string license=Meta("LicenseShortName"),licenseUrl=Meta("LicenseUrl"),creator=Meta("Artist");if(license.Contains("NC",StringComparison.OrdinalIgnoreCase)||license.Contains("ND",StringComparison.OrdinalIgnoreCase)||license==""||!(license.Contains("CC",StringComparison.OrdinalIgnoreCase)||license.Contains("Public domain",StringComparison.OrdinalIgnoreCase)))continue;
            string source=image.GetProperty("descriptionurl").GetString()??"",name=Plain(page.Value.GetProperty("title").GetString()??"").Replace("File:","");
            if(!Uri.TryCreate(source,UriKind.Absolute,out var sourceUri)||sourceUri.Scheme!="https"||sourceUri.Host!="commons.wikimedia.org")continue;
            result.Add(new(name,actual,url,source,creator,license,licenseUrl,$"{name} • {creator} • {license} • {licenseUrl} • {source}"));
        }
        return result;
    }
    public async Task<string> Download(FreeMedia item,CancellationToken ct,bool archive=true)
    {
        var uri=new Uri(item.Url);if(!DownloadHost(uri))throw new InvalidOperationException("İndirme kaynağı doğrulanamadı.");
        string extension=Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();if(!new[]{".jpg",".jpeg",".png",".webp",".webm",".ogv",".ogg",".oga",".wav",".mp3",".flac",".mp4"}.Contains(extension))throw new InvalidOperationException("Bu medya biçimi desteklenmiyor.");
        string cache=Path.Combine(store.Root,"cache","free-media");Directory.CreateDirectory(cache);string file=Path.Combine(cache,TimedNarration.TextHash(item.Url)+extension);
        if(!File.Exists(file)){using var response=await http.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,ct);await Reliability.DownloadResponse(response,file,200_000_000,ct);}
        if(!archive)return file;
        if(item.Kind is "Müzik" or "Ses efekti"){
            var library=new AudioLibrary(store);var tracks=library.List();string hash=Reliability.FileHash(file);var old=tracks.FirstOrDefault(x=>x.Hash==hash);if(old!=null)return old.File;
            Directory.CreateDirectory(library.Folder);string target=Path.Combine(library.Folder,hash+extension);if(!File.Exists(target))File.Copy(file,target);tracks.Add(new AudioItem{Name=item.Name,File=target,Kind=item.Kind,Source=item.Source,License=item.LicenseUrl!=""?item.LicenseUrl:item.License,Attribution=item.Attribution,RightsConfirmed=true,Hash=hash});library.Save(tracks);return target;
        }
        if(item.Kind=="Video"){
            if(extension==".ogv"){string converted=Path.ChangeExtension(file,".mp4");if(!File.Exists(converted))await Media.Exec(store.Ffmpeg,["-y","-i",file,"-c:v","libx264","-preset","veryfast","-c:a","aac",converted],cache,ct);file=converted;}
            return new MediaPool(store).Add(file,item.Name,"Ücretsiz kaynak",item.Source,item.Creator,item.LicenseUrl!=""?item.LicenseUrl:item.License,item.Attribution).File;
        }
        using(var image=Image.FromFile(file)){if((long)image.Width*image.Height>40_000_000)throw new InvalidOperationException("Fotoğraf çok büyük.");}
        string folder=Path.Combine(store.Root,"photo-library");Directory.CreateDirectory(folder);string photo=Path.Combine(folder,Reliability.FileHash(file)+extension);if(!File.Exists(photo))File.Copy(file,photo);Json.Write(photo+".source.json",item);return photo;
    }
}

public sealed partial class StudioForm
{
    void FreeMediaDialog(Func<string,string,Task>? add=null)
    {
        using var dialog=Dialog("Ücretsiz medya kütüphanesi",1050,780);using var cancellation=new CancellationTokenSource();dialog.FormClosed+=(_,_)=>cancellation.Cancel();var flow=Theme.Flow(950);flow.Dock=DockStyle.Fill;flow.AutoScroll=true;dialog.Controls.Add(flow);
        var query=Theme.Input("",width:900);query.AccessibleName="Ücretsiz medya araması";flow.Controls.Add(query);var kind=Theme.Combo(["Tümü","Fotoğraf","Video","Müzik","Ses efekti"],"Tümü",900);flow.Controls.Add(kind);
        var status=Theme.Label("Wikimedia Commons • Pexels / Pixabay kayıtlı anahtarlarla • Her dosyanın kendi lisansı korunur",10,width:900,height:45);flow.Controls.Add(status);var providers=new FlowLayoutPanel{Width=900,Height=52};foreach(var source in new[]{("Wikimedia Commons","https://commons.wikimedia.org"),("Pexels","https://www.pexels.com"),("Pixabay","https://pixabay.com")}){string link=source.Item2;providers.Controls.Add(Theme.Button(source.Item1,()=>Theme.Open(link),false,220));}flow.Controls.Add(providers);var activity=new TurquoisePackageBar{Width=900,Height=28,Visible=false};flow.Controls.Add(activity);var results=new ListBox{Width=900,Height=240,BackColor=Theme.Shell,ForeColor=Theme.Text,DisplayMember="Name",AccessibleName="Ücretsiz medya arama sonuçları"};
        var service=new FreeMediaLibrary(store);bool busy=false;async Task Run(Func<Task> action){if(busy)return;busy=true;activity.Visible=activity.Busy=true;status.Text="İşlem sürüyor…";try{await action();}catch(OperationCanceledException){}catch(Exception ex){if(!dialog.IsDisposed)status.Text="Tamamlanamadı: "+ex.Message;}finally{busy=false;if(!dialog.IsDisposed)activity.Visible=activity.Busy=false;}}
        flow.Controls.Add(Theme.Button("Ara",()=>_=Run(async()=>{var list=await service.Search(query.Text,kind.Text,cancellation.Token);if(dialog.IsDisposed)return;results.Items.Clear();foreach(var item in list)results.Items.Add(item);status.Text=list.Count+" sonuç • lisansı görüntüleyip önizleyebilirsin.";}),true,180));flow.Controls.Add(results);
        var credit=Theme.Label("",10,width:900,height:65);flow.Controls.Add(credit);results.SelectedIndexChanged+=(_,_)=>credit.Text=results.SelectedItem is FreeMedia item?item.Attribution:"";
        var audio=new ArchiveMusicPlayer(compact:true){Width=900,Visible=false};var picture=new PictureBox{Width=900,Height=180,SizeMode=PictureBoxSizeMode.Zoom,Visible=false};flow.Controls.Add(audio);flow.Controls.Add(picture);dialog.FormClosed+=(_,_)=>picture.Image?.Dispose();
        flow.Controls.Add(Theme.Button("Önizle / dinle",()=>_=Run(async()=>{if(results.SelectedItem is not FreeMedia item)return;string file=await service.Download(item,cancellation.Token,false);if(dialog.IsDisposed)return;if(item.Kind=="Fotoğraf"){using var image=Image.FromFile(file);var old=picture.Image;picture.Image=new Bitmap(image);old?.Dispose();picture.Visible=true;audio.Visible=false;}else if(item.Kind is "Müzik" or "Ses efekti"){if(Path.GetExtension(file) is ".ogg" or ".oga"){string wav=file+".wav";await Media.Exec(store.Ffmpeg,["-y","-i",file,"-t","30",wav],store.Root,cancellation.Token);file=wav;}audio.Visible=true;audio.LoadAndPlay(file);picture.Visible=false;}else{using var video=Dialog("Video önizleme",850,550);var player=new WorkflowPlayer{Dock=DockStyle.Fill};var play=Theme.Button("Oynat",()=>player.Play(),true,150);play.Dock=DockStyle.Bottom;video.Controls.Add(player);video.Controls.Add(play);video.Shown+=(_,_)=>{player.Open(file);player.Play();};video.ShowDialog(dialog);}status.Text="Önizleme hazır.";}),false,220));
        flow.Controls.Add(Theme.Button(add==null?"İndir ve arşive ekle":"İndir ve projeye ekle",()=>_=Run(async()=>{if(results.SelectedItem is not FreeMedia item)return;string file=await service.Download(item,cancellation.Token);if(dialog.IsDisposed)return;if(add!=null)await add(file,item.Kind);status.Text="✓ İndirildi ve arşive eklendi • kaynak ve lisans saklandı.";}),true,300));dialog.ShowDialog(this);
    }
}
