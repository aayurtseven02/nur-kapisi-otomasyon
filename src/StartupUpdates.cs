using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AnkaStudio;

public static class StartupUpdates
{
    public sealed record Manifest(string Version,string DownloadUrl,string Sha256,string? MinimumVersion=null);
    public sealed record Staged(string Folder,string Version,string Sha256);
    public static readonly string DefaultFeed="https://api.github.com/repos/aayurtseven02/nur-kapisi-otomasyon/releases/latest";
    static Version Normal(Version v)=>new(v.Major,v.Minor,Math.Max(0,v.Build),Math.Max(0,v.Revision));
    public static Manifest? Next(string raw,Version current)
    {
        using var doc=JsonDocument.Parse(raw.TrimStart('\uFEFF'));var options=new JsonSerializerOptions{PropertyNameCaseInsensitive=true};
        var entries=doc.RootElement.TryGetProperty("Updates",out var updates)?JsonSerializer.Deserialize<List<Manifest>>(updates.GetRawText(),options)??[]:[JsonSerializer.Deserialize<Manifest>(raw,options)??throw new InvalidOperationException("Güncelleme bilgisi boş.")];
        var candidates=entries.Select(m=>(Manifest:m,Version:Normal(System.Version.Parse(m.Version.TrimStart('v'))))).Where(x=>x.Version>Normal(current)).OrderBy(x=>x.Version).ToList();
        if(candidates.Count==0)return null;
        var next=candidates[0];
        if(next.Manifest.MinimumVersion is string minimum&&Normal(System.Version.Parse(minimum))>Normal(current))throw new InvalidOperationException("Güncelleme zincirinde gerekli önceki sürüm eksik.");
        return next.Manifest;
    }
    public static async Task<Staged?> Stage(HttpClient http,Uri feed,Version current,string cache,Action<string,int?> report,CancellationToken ct)
    {
        if(feed.Scheme!="https")throw new InvalidOperationException("Güncelleme kaynağı HTTPS olmalı.");
        report("Güncellemeler kontrol ediliyor…",null);
        using var check=CancellationTokenSource.CreateLinkedTokenSource(ct);check.CancelAfter(TimeSpan.FromSeconds(6));
        using var req=new HttpRequestMessage(HttpMethod.Get,feed);req.Headers.UserAgent.ParseAdd("AnkaStudio/"+current);
        using var response=await http.SendAsync(req,check.Token);
        if(response.StatusCode==HttpStatusCode.NotFound)throw new InvalidOperationException("Güncelleme yayınına erişilemedi; mevcut sürüm açılacak.");
        response.EnsureSuccessStatusCode();string raw=await response.Content.ReadAsStringAsync(check.Token);
        if(raw.Length>1_000_000)throw new InvalidOperationException("Güncelleme bilgisi çok büyük.");
        using(var doc=JsonDocument.Parse(raw)){
            // GitHub release discovery; the update manifest is an explicit release asset.
            if(doc.RootElement.TryGetProperty("assets",out var assets)){
                if(doc.RootElement.TryGetProperty("prerelease",out var pre)&&pre.GetBoolean())return null;
                var asset=assets.EnumerateArray().FirstOrDefault(a=>a.GetProperty("name").GetString()=="anka-update.json");
                if(asset.ValueKind==JsonValueKind.Undefined)throw new InvalidOperationException("Bu yayında otomatik güncelleme paketi bulunmuyor.");
                var uri=new Uri(asset.GetProperty("browser_download_url").GetString()!);
                if(uri.Scheme!="https"||uri.Host!="github.com")throw new InvalidOperationException("Güncelleme bilgisi kaynağı geçersiz.");
                using var assetReq=new HttpRequestMessage(HttpMethod.Get,uri);assetReq.Headers.UserAgent.ParseAdd("AnkaStudio/"+current);
                using var assetResponse=await http.SendAsync(assetReq,check.Token);assetResponse.EnsureSuccessStatusCode();raw=await assetResponse.Content.ReadAsStringAsync(check.Token);
            }
        }
        if(raw.Length>1_000_000)throw new InvalidOperationException("Güncelleme listesi çok büyük.");
        var manifest=Next(raw,current);if(manifest==null)return null;
        if(!System.Version.TryParse(manifest.Version.TrimStart('v'),out var next))throw new InvalidOperationException("Güncelleme sürümü geçersiz.");
        if(Normal(next)<=Normal(current))return null;
        var download=new Uri(manifest.DownloadUrl);
        if(download.Scheme!="https"||(download.Host!=feed.Host&&download.Host!="github.com"))throw new InvalidOperationException("Paket kaynağı güvenilen sunucuyla eşleşmiyor.");
        if(manifest.Sha256.Length!=64||!manifest.Sha256.All(Uri.IsHexDigit))throw new InvalidOperationException("Paket doğrulama özeti eksik.");
        string stage=Path.Combine(cache,"stage-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);
        string zip=Path.Combine(stage,"download.zip");
        report("Yeni sürüm indiriliyor • "+next,0);
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromMinutes(10));
        using var package=await http.GetAsync(download,HttpCompletionOption.ResponseHeadersRead,timeout.Token);package.EnsureSuccessStatusCode();
        long limit=600_000_000,total=package.Content.Headers.ContentLength??0,read=0;if(total>limit)throw new InvalidOperationException("Paket boyutu sınırı aşıldı.");
        await using(var input=await package.Content.ReadAsStreamAsync(timeout.Token))await using(var output=File.Create(zip)){
            byte[] buffer=new byte[65536];int count,last=-1;while((count=await input.ReadAsync(buffer,timeout.Token))>0){read+=count;if(read>limit)throw new InvalidOperationException("Paket boyutu sınırı aşıldı.");await output.WriteAsync(buffer.AsMemory(0,count),timeout.Token);int percent=total>0?(int)(read*100/total):0;if(percent/5!=last){last=percent/5;report("Güncelleme indiriliyor…",Math.Min(99,percent));}}
        }
        if(!Reliability.FileHash(zip).Equals(manifest.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Paket doğrulanamadı; kurulum yapılmadı.");
        report("Paket doğrulanıyor ve hazırlanıyor…",100);
        string payload=Path.Combine(stage,"Program");Directory.CreateDirectory(payload);long expanded=0;int files=0;
        var hashes=new Dictionary<string,string>();
        using(var archive=ZipFile.OpenRead(zip))foreach(var entry in archive.Entries){
            ct.ThrowIfCancellationRequested();if(entry.FullName.EndsWith('/')||entry.FullName.EndsWith('\\'))continue;
            string relative=entry.FullName.Replace('\\','/');
            if(relative.Split('/').Any(x=>x is ".." or "." or ""||x.Contains(':'))||relative.StartsWith('/')||relative.Split('/').Any(x=>x.Equals("Data",StringComparison.OrdinalIgnoreCase)||x.Equals("projects",StringComparison.OrdinalIgnoreCase))||new[]{"secrets.dat","settings.json","episodes.json"}.Contains(Path.GetFileName(relative),StringComparer.OrdinalIgnoreCase))throw new InvalidOperationException("Paket proje veya hesap verisini değiştiremez.");
            expanded+=entry.Length;if(++files>2000||expanded>1_200_000_000)throw new InvalidOperationException("Açılan paket sınırı aşıldı.");
            string dest=Path.GetFullPath(Path.Combine(payload,relative));if(!dest.StartsWith(payload+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Paket yolu geçersiz.");
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);entry.ExtractToFile(dest);hashes[relative]=Reliability.FileHash(dest);
        }
        string appName=File.Exists(Path.Combine(payload,"TubeFlow.exe"))?"TubeFlow":"AnkaStudio";
        if(!File.Exists(Path.Combine(payload,appName+".exe"))||!File.Exists(Path.Combine(payload,appName+".dll"))||!File.Exists(Path.Combine(payload,"coreclr.dll")))throw new InvalidOperationException("Güncelleme paketi çalışma ortamını içermiyor.");
        if(!System.Version.TryParse(FileVersionInfo.GetVersionInfo(Path.Combine(payload,appName+".dll")).FileVersion,out var actual)||Normal(actual)!=Normal(next))throw new InvalidOperationException("Paket sürümü güncelleme listesiyle eşleşmiyor.");
        await File.WriteAllTextAsync(Path.Combine(stage,"files.json"),JsonSerializer.Serialize(hashes),ct);
        return new Staged(stage,next.ToString(),manifest.Sha256);
    }
    public static bool Run(string[] args)
    {
        // Test, worker and command-line tools must never open an updater or use the network.
        if(args.Any(x=>x=="--skip-update-once"||x=="--worker"||x.StartsWith("--")&&(x.Contains("test")||x.Contains("preview"))||x=="--screenshot"))return false;
        string baseDir=AppContext.BaseDirectory,cache=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AnkaStudio","updates");
        using var mutex=new Mutex(false,"Local\\AnkaUpdate-"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(baseDir.ToUpperInvariant())))[..16]);
        if(!mutex.WaitOne(0))return false;
        try{
            using var dialog=new Form{Text="TubeFlow • Açılış kontrolü",Width=570,Height=220,StartPosition=FormStartPosition.CenterScreen,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false,BackColor=Theme.Bg};
            var label=new Label{Text="Güncellemeler kontrol ediliyor…",ForeColor=Theme.Text,Font=new Font("Segoe UI",12),Location=new Point(24,24),Size=new Size(510,56)};
            var bar=new ProgressBar{Location=new Point(24,90),Size=new Size(510,22),Style=ProgressBarStyle.Marquee};
            dialog.HandleCreated+=(_,_)=>Shapes.DarkTitle(dialog.Handle);var skip=new StudioButton{Text="Mevcut sürümü aç",Location=new Point(330,125),Size=new Size(204,34)};dialog.Controls.AddRange([label,bar,skip]);
            using var stop=new CancellationTokenSource();bool installing=false,workDone=false;
            skip.Click+=(_,_)=>{skip.Enabled=false;stop.Cancel();};dialog.FormClosing+=(_,e)=>{if(!workDone){stop.Cancel();e.Cancel=true;}};
            dialog.Shown+=async(_,_)=>{
                try{
                    using var http=new HttpClient();
                    Uri feed=new(DefaultFeed);string config=Path.Combine(baseDir,"anka-update-source.json");
                    if(File.Exists(config)){using var doc=JsonDocument.Parse(File.ReadAllText(config));feed=new Uri(doc.RootElement.GetProperty("FeedUrl").GetString()!);}
                    void Report(string text,int? percent){if(dialog.IsDisposed)return;label.Text=text;bar.Style=percent.HasValue?ProgressBarStyle.Continuous:ProgressBarStyle.Marquee;if(percent.HasValue)bar.Value=Math.Clamp(percent.Value,0,100);}
                    var stage=await Stage(http,feed,typeof(StartupUpdates).Assembly.GetName().Version!,cache,Report,stop.Token);
                    if(stage!=null){
                        string helper=Path.Combine(baseDir,"Update-Install.ps1");
                        string encoded=Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(args.Where(x=>x!="--skip-update-once").ToArray())));
                        string Q(string s)=>"\""+s.Replace("\"","\\\"")+"\"";
                        var info=new ProcessStartInfo("powershell.exe"){UseShellExecute=true,Verb="runas",WindowStyle=ProcessWindowStyle.Hidden,Arguments="-NoProfile -ExecutionPolicy Bypass -File "+Q(helper)+" -ParentId "+Environment.ProcessId+" -Source "+Q(stage.Folder)+" -Target "+Q(baseDir.TrimEnd(Path.DirectorySeparatorChar))+" -PackageSha256 "+stage.Sha256+" -RestartArgsBase64 "+encoded};
                        label.Text="Güncelleme kuruluyor; ardından TubeFlow açılacak…";
                        using var process=Process.Start(info);installing=process!=null;
                    }
                }catch(Exception ex){
                    try{Directory.CreateDirectory(cache);File.AppendAllText(Path.Combine(cache,"startup-update.log"),DateTimeOffset.Now.ToString("O")+" • "+ex.GetType().Name+": "+ex.Message+Environment.NewLine);}catch{}
                    label.Text="Kontrol tamamlanamadı; mevcut sürüm açılıyor.";
                }finally{workDone=true;dialog.Close();}
            };
            dialog.ShowDialog();return installing;
        }finally{mutex.ReleaseMutex();}
    }
}
