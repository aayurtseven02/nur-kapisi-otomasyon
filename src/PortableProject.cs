using System.IO.Compression;

using System.Security.Cryptography;

using System.Text.Json;

using System.Text.Json.Nodes;

namespace AnkaStudio;



public static class PortableProject

{

    public sealed record FileRecord(string Original, string Entry, string Hash, long Length);

    public sealed class Manifest { public int Version {get;set;}=1; public Episode Episode {get;set;}=new(); public List<FileRecord> Files {get;set;}=[]; }

    static readonly HashSet<string> PathFields=new(StringComparer.OrdinalIgnoreCase){"AiStoryReferenceFile","NarrationFile","NarrationTimingFile","File","StockFile","Thumbnail","MusicPath","CoverReferenceImage","CoverPng","CoverBackground","CoverLogo","OpeningCardFile","ClosingCardFile","VideoLogo","AudioFile","MusicFile","RightsDocument"};

    internal static IEnumerable<string> Paths(JsonNode? node,string field="")

    {

        if(node is JsonObject obj)foreach(var property in obj)foreach(var path in Paths(property.Value,property.Key))yield return path;

        else if(node is JsonArray array)foreach(var child in array)foreach(var path in Paths(child,field))yield return path;

        else if(node is JsonValue value && value.TryGetValue<string>(out var text) && text!="" && (PathFields.Contains(field) || field is "LongFiles" or "ShortFiles" or "Files" or "LocalVideos"))yield return text;

    }

    internal static void Remap(JsonNode? node,Dictionary<string,string> paths)

    {

        if(node is JsonObject obj)foreach(var property in obj.ToList()) {

            if(property.Value is JsonValue v && v.TryGetValue<string>(out var text) && paths.TryGetValue(text,out var path))obj[property.Key]=path;

            else Remap(property.Value,paths);

        }

        if(node is JsonArray arr)for(int i=0;i<arr.Count;i++) {

            if(arr[i] is JsonValue v && v.TryGetValue<string>(out var text) && paths.TryGetValue(text,out var path))arr[i]=path;

            else Remap(arr[i],paths);

        }

    }

    public static async Task Export(Store store,Episode episode,string target,CancellationToken ct)

    {

        using var lease=new OperationLease(store);

        var copy=JsonSerializer.Deserialize<Episode>(JsonSerializer.Serialize(episode,Json.Options),Json.Options)!;

        copy.ProductionSettings=VideoQueue.Snapshot(VideoSettings.For(store.Settings,episode));

        // Credentials and account identity never belong to a portable render snapshot.

        copy.ApprovedChannelId="";

        foreach(var video in copy.Videos) {

            var directory=Path.GetDirectoryName(video.File);if(directory==null)continue;

            if(video.NarrationFile=="" && File.Exists(Path.Combine(directory,"narration-full.wav")))video.NarrationFile=Path.Combine(directory,"narration-full.wav");

            if(video.NarrationTimingFile=="" && File.Exists(Path.Combine(directory,"narration-timing.json")))video.NarrationTimingFile=Path.Combine(directory,"narration-timing.json");

        }

        var node=JsonSerializer.SerializeToNode(copy,Json.Options)!;

        var raw=Paths(node).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        var selected=raw.Concat(raw.SelectMany(p=>new[]{p+".hash",p+".sha",p+".json"}).Where(File.Exists)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        var missing=selected.Where(p=>!File.Exists(p)).ToArray();

        if(missing.Length>0)throw new InvalidOperationException("Yedek tamamlanamadı; önce eksik kaynakları yeniden bağla:\n"+string.Join("\n",missing.Select(Path.GetFileName)));

        var temp=target+"."+Guid.NewGuid().ToString("N")+".part";

        var manifest=new Manifest{Episode=copy};

        try {

            using(var archive=ZipFile.Open(temp,ZipArchiveMode.Create)) {

                int index=0;

                foreach(var path in selected) {

                    ct.ThrowIfCancellationRequested();

                    var entryName="assets/"+(index++).ToString("D5")+Path.GetExtension(path);

                    await using var input=File.OpenRead(path);

                    var hash=Convert.ToHexString(await SHA256.HashDataAsync(input,ct));input.Position=0;

                    var entry=archive.CreateEntry(entryName,CompressionLevel.NoCompression);

                    await using(var output=entry.Open())await input.CopyToAsync(output,ct);

                    manifest.Files.Add(new(path,entryName,hash,input.Length));

                }

                var metadata=archive.CreateEntry("project.json");await using var meta=metadata.Open();await JsonSerializer.SerializeAsync(meta,manifest,Json.Options,ct);

            }

            using(var verify=ZipFile.OpenRead(temp))foreach(var item in manifest.Files){await using var stream=verify.GetEntry(item.Entry)!.Open();if(Convert.ToHexString(await SHA256.HashDataAsync(stream,ct))!=item.Hash)throw new IOException("Proje yedeği doğrulanamadı.");}

            File.Move(temp,target,true);

        } finally {if(File.Exists(temp))File.Delete(temp);}

    }

    public static async Task<Episode> Restore(Store store,string source,CancellationToken ct)

    {

        using var lease=new OperationLease(store);using var archive=ZipFile.OpenRead(source);

        var metadata=archive.GetEntry("project.json") ?? throw new InvalidOperationException("TubeFlow proje yedeği değil.");

        if(metadata.Length>20*1024*1024)throw new InvalidOperationException("Proje kaydı aşırı büyük.");

        Manifest manifest;await using(var stream=metadata.Open())manifest=await JsonSerializer.DeserializeAsync<Manifest>(stream,Json.Options,ct)??throw new InvalidOperationException("Yedek kaydı boş.");

        if(manifest.Version!=1 || manifest.Files.Count>10000)throw new InvalidOperationException("Yedek sürümü / dosya sayısı desteklenmiyor.");

        if(manifest.Files.Any(x=>!System.Text.RegularExpressions.Regex.IsMatch(x.Entry,@"^assets/[0-9]{5}\.[A-Za-z0-9]{1,12}$") || x.Length<0 || x.Length>100L*1024*1024*1024) || manifest.Files.Select(x=>x.Entry).Distinct().Count()!=manifest.Files.Count || manifest.Files.Select(x=>x.Original).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=manifest.Files.Count)throw new InvalidOperationException("Yedek dosya listesi geçersiz.");

        long total=manifest.Files.Sum(x=>x.Length);if(new DriveInfo(Path.GetPathRoot(store.Root)!).AvailableFreeSpace<total+512L*1024*1024)throw new IOException("Yedeği açmak için yeterli boş alan yok.");

        var episode=manifest.Episode;episode.Id=Guid.NewGuid().ToString("N");var folder=store.Folder(episode);var assetFolder=Path.Combine(folder,"restored-assets");Directory.CreateDirectory(assetFolder);

        bool committed=false;

        try {

            var paths=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);

            foreach(var file in manifest.Files) {

                var entry=archive.GetEntry(file.Entry)??throw new InvalidOperationException("Yedek dosyası eksik.");if(entry.Length!=file.Length)throw new IOException("Yedek dosya boyutu uyuşmuyor.");

                var target=Path.Combine(assetFolder,Path.GetFileName(file.Entry));

                await using(var input=entry.Open())await using(var output=File.Create(target))await Reliability.CopyBounded(input,output,file.Length,ct);

                if(Reliability.FileHash(target)!=file.Hash)throw new IOException("Yedek dosya özeti uyuşmuyor.");paths.Add(file.Original,target);

            }

            foreach(var item in paths.ToList())foreach(var suffix in new[]{".hash",".sha"})if(paths.TryGetValue(item.Key+suffix,out var marker))File.Copy(marker,item.Value+suffix,true);

            var node=JsonSerializer.SerializeToNode(episode,Json.Options)!;Remap(node,paths);episode=node.Deserialize<Episode>(Json.Options)!;

            if(Paths(node).Any(p=>!paths.Values.Contains(p,StringComparer.OrdinalIgnoreCase)))throw new InvalidOperationException("Yedekte taşınmamış dosya başvurusu var.");

            episode.QueueAuthorized=false;episode.QueueOrder=0;episode.QueueState="";episode.Approved=false;episode.ApprovalHash="";episode.ApprovedChannelId="";episode.Error="";episode.Status="Geri yüklendi";

            LegacyVideoState.Clear(episode);

            foreach(var video in episode.Videos){video.YouTubeId="";video.UploadSession="";video.YouTubeState="";video.Scheduled=false;video.ThumbnailUploaded=false;video.ThumbnailAttempted=false;video.PublishAt=null;video.YouTubeCheckedAt=null;video.PlanHash=Reliability.MediaPlanHash(episode,video.Key);video.RenderFingerprint="";}

            episode.Log("Taşınabilir yedekten ayrı proje olarak geri yüklendi. Yeniden inceleyip onayla; eski YouTube bağlantıları taşınmadı.");

            store.Episodes.Add(episode);try{store.Save();}catch{store.Episodes.Remove(episode);throw;}committed=true;return episode;

        } finally {if(!committed && Path.GetFullPath(folder).StartsWith(Path.GetFullPath(Path.Combine(store.Root,"projects"))+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))Directory.Delete(folder,true);}

    }

}

