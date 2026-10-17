using System.IO.Compression;

using System.Net.Http.Headers;

using System.Text;

using System.Text.Json;

using System.Text.RegularExpressions;



namespace AnkaStudio;



public sealed partial class GitHub(Store store, HttpClient? client = null)

{

    static readonly HttpClient sharedHttp = new() { Timeout = TimeSpan.FromMinutes(8) };

    readonly HttpClient http = client ?? sharedHttp;

    public string Repo

    {

        get { if (!Regex.IsMatch(store.Settings.Repo, @"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$")) throw new InvalidOperationException("GitHub deposunu kullanıcı/depo biçiminde seç."); return store.Settings.Repo; }

    }

    async Task<string> Call(HttpMethod method, string path, object? body, CancellationToken ct)

    {

        var key = store.Key("GITHUB_TOKEN"); if (key == "") throw new InvalidOperationException("GitHub erişim anahtarını Bağlantı Merkezi'ne ekle.");

        using var req = new HttpRequestMessage(method, "https://api.github.com/" + path); req.Headers.Authorization = new("Bearer", key); req.Headers.UserAgent.ParseAdd("AnkaStudio/0.1"); req.Headers.Accept.ParseAdd("application/vnd.github+json"); req.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

        if (body != null) req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using var res = await http.SendAsync(req, ct); if (!res.IsSuccessStatusCode) throw new ProviderException("GitHub", (int)res.StatusCode); return await res.Content.ReadAsStringAsync(ct);

    }

    public async Task<string> User(CancellationToken ct) { using var d = JsonDocument.Parse(await Call(HttpMethod.Get, "user", null, ct)); return d.RootElement.GetProperty("login").GetString()!; }

    public async Task<List<string>> Repositories(CancellationToken ct) { using var d = JsonDocument.Parse(await Call(HttpMethod.Get, "user/repos?per_page=100&sort=updated", null, ct)); return d.RootElement.EnumerateArray().Select(r => r.GetProperty("full_name").GetString()!).ToList(); }

    public async Task<string> CreateRepository(string name, CancellationToken ct)

    {

        if (!Regex.IsMatch(name, @"^[A-Za-z0-9_.-]{1,70}$")) throw new InvalidOperationException("Depo adında harf, rakam, nokta veya tire kullan.");

        using var d = JsonDocument.Parse(await Call(HttpMethod.Post, "user/repos", new { name, description = "TubeFlow • Türkçe normal video ve Shorts üretimi", @private = true, auto_init = true }, ct));

        store.Settings.Repo = d.RootElement.GetProperty("full_name").GetString()!; store.Settings.GitHubRef = d.RootElement.GetProperty("default_branch").GetString()!; store.Save(); return store.Settings.Repo;

    }

    public async Task<List<(string Id, string Name, string State, string Url)>> Runs(CancellationToken ct)

    {

        using var d = JsonDocument.Parse(await Call(HttpMethod.Get, "repos/" + Repo + "/actions/runs?per_page=100", null, ct));

        var runs = d.RootElement.GetProperty("workflow_runs").EnumerateArray().Select(r => (Id: r.GetProperty("id").ToString(), Name: r.GetProperty("display_title").GetString()!, State: r.GetProperty("status").GetString()! + " / " + (r.GetProperty("conclusion").GetString() ?? "devam ediyor"), Url: r.GetProperty("html_url").GetString()!)).ToList();

        foreach (var episode in store.Episodes.Where(e => e.RemoteRunId != "" && e.RemoteRepo == Repo))

        {

            var run = runs.FirstOrDefault(r => r.Id == episode.RemoteRunId || (episode.RemoteRunId == "gönderildi" && r.Name == "Anka paket " + episode.Id));

            if (run.Id == null) continue;

            episode.RemoteRunId = run.Id; episode.RemoteState = run.State;

        }

        store.Save(); return runs;

    }

    public Task CancelRun(string id, CancellationToken ct) { if (!long.TryParse(id, out _)) throw new InvalidOperationException("Çalışma seç."); return Call(HttpMethod.Post, "repos/" + Repo + "/actions/runs/" + id + "/cancel", null, ct); }

    public async Task PushSource(CancellationToken ct)

    {

        var source = Path.Combine(AppContext.BaseDirectory, "repository");

        if (!Directory.Exists(source)) throw new InvalidOperationException("Kaynak paketi bulunamadı. TubeFlow tam dağıtım paketini kullan.");

        ValidateSourceBundle(source,SupportPrivacy.SecretNames.Select(store.Key));

        using var reference = JsonDocument.Parse(await Call(HttpMethod.Get, "repos/" + Repo + "/git/ref/heads/" + Uri.EscapeDataString(store.Settings.GitHubRef), null, ct));

        var parent = reference.RootElement.GetProperty("object").GetProperty("sha").GetString()!;

        using var commit = JsonDocument.Parse(await Call(HttpMethod.Get, "repos/" + Repo + "/git/commits/" + parent, null, ct)); var baseTree = commit.RootElement.GetProperty("tree").GetProperty("sha").GetString()!;

        var tree = new List<object>();

        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))

        {

            var relative = Path.GetRelativePath(source, file).Replace('\\', '/');

            if (!AllowedSource(relative)) continue;

            using var blob = JsonDocument.Parse(await Call(HttpMethod.Post, "repos/" + Repo + "/git/blobs", new { content = Convert.ToBase64String(await File.ReadAllBytesAsync(file, ct)), encoding = "base64" }, ct));

            tree.Add(new { path = relative, mode = "100644", type = "blob", sha = blob.RootElement.GetProperty("sha").GetString()! });

        }

        using var newTree = JsonDocument.Parse(await Call(HttpMethod.Post, "repos/" + Repo + "/git/trees", new { base_tree = baseTree, tree }, ct));

        using var newCommit = JsonDocument.Parse(await Call(HttpMethod.Post, "repos/" + Repo + "/git/commits", new { message = "TubeFlow uygulama ve üretim iş akışları", tree = newTree.RootElement.GetProperty("sha").GetString(), parents = new[] { parent } }, ct));

        await Call(HttpMethod.Patch, "repos/" + Repo + "/git/refs/heads/" + Uri.EscapeDataString(store.Settings.GitHubRef), new { sha = newCommit.RootElement.GetProperty("sha").GetString(), force = false }, ct);

    }

    public static void ValidateSourceBundle(string source,IEnumerable<string>? knownSecrets=null)

    {

        var project = Path.Combine(source, "src", "AnkaStudio.csproj");

        if (!File.Exists(project)) throw new InvalidOperationException("Kaynak projesi eksik.");

        SourceSafety.ValidateFiles(source,knownSecrets);
        var document = System.Xml.Linq.XDocument.Load(project);

        var includes = document.Descendants().Where(x => x.Name.LocalName is "Content" or "EmbeddedResource").Select(x => (string?)x.Attribute("Include")).Where(x => x != null).Cast<string>().ToList();

        includes.AddRange(document.Descendants("ApplicationIcon").Select(x => x.Value));

        foreach (var include in includes) {

            var normalized = include.Replace('\\', '/');

            var directory = Path.Combine(Path.GetDirectoryName(project)!, Path.GetDirectoryName(normalized)!);

            var files = Directory.Exists(directory) ? Directory.GetFiles(directory, Path.GetFileName(normalized)) : [];

            if (files.Length == 0 || files.Any(f => !AllowedSource(Path.GetRelativePath(source, f).Replace('\\','/')))) throw new InvalidOperationException("Kaynak paketinde gerekli varlık eksik veya aktarım dışı: " + include);

        }

    }

    public static bool AllowedSource(string relative) => !relative.Split('/').Any(p => p is "bin" or "obj" or "data" or "projects" or ".git") && (relative.EndsWith(".cs") || relative.EndsWith(".csproj") || relative.EndsWith(".yml") || relative.EndsWith(".md") || relative.EndsWith(".Config") || relative=="tools/audio-alignment/align.py" || relative=="tools/Publish-Update.ps1" || relative=="tools/Update-Install.ps1" || relative=="tools/workflows/anka-flux2.json" || relative=="tools/workflows/anka-sdxl.json" || relative=="src/Assets/pairing-firebase.json" || relative == ".gitignore" || relative == "LICENSE" || relative == "src/Assets/anka-studio-3d.png" || relative == "src/Assets/tubeflow-horizontal.png" || relative == "src/Assets/tubeflow-dark.png" || relative == "src/Assets/anka-studio.ico" || relative == "src/Assets/nur-kapisi-background.png" || relative == "src/Assets/nur-kapisi-kanal-logo.png" || (relative.StartsWith("src/Assets/nur-kapisi-destek-", StringComparison.Ordinal) && relative.EndsWith(".png", StringComparison.Ordinal) && relative.Split('/').Length == 3)) && !relative.Contains("secret", StringComparison.OrdinalIgnoreCase);

    public static void CheckRemoteVoice(Settings settings, Episode episode)

    {

        settings = PackageArtwork.Effective(VideoQueue.Effective(settings,episode.QueueSettings),episode);

        if(episode.Plan!=null && Rules.Scripts(episode).Any(x=>x.Script.CoverSettings is Settings c && (c.CoverMode=="Kendi PNG kapak" || c.CoverBackground!="" || c.CoverLogo!=""))) throw new InvalidOperationException("Paket kapak editöründeki yerel görseller buluta aktarılmaz. Bu paketi Windows'ta üret.");

        if (!settings.CloudAiEnabled || string.IsNullOrWhiteSpace(settings.EnabledCloudProviders)) throw new InvalidOperationException("GitHub'da yerel Ollama kullanılamaz. Bulut AI sekmesinden hesabın ve kullanım hakkın olan sağlayıcıyı etkinleştir veya Windows'ta üret.");

        var enabled = settings.EnabledCloudProviders.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);

        var usable = settings.ProviderOrder.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Any(p => p != "Ollama" && enabled.Contains(p) && (!settings.FreeOnly || p is not ("OpenAI" or "Kimi")) && (p != "Kimi" || settings.UseKimi) && (p != "OpenRouter" || !settings.FreeOnly || settings.OpenRouterModel == "openrouter/free" || settings.OpenRouterModel.EndsWith(":free", StringComparison.Ordinal)));

        if (!usable) throw new InvalidOperationException("GitHub üretimi için alternatif sırasında etkin ve kullanımına izin verilen bir bulut AI bulunmalı. Bulut AI ayarlarında sağlayıcıyı etkinleştirip alternatif sırasına ekle.");

        if (settings.OpeningCardEnabled || settings.ClosingCardEnabled || settings.VideoLogoEnabled) throw new InvalidOperationException("Yerel açılış/kapanış kartları ve video logosu buluta aktarılmaz. Bu paketi Windows'ta üret.");

        if (settings.CoverMode == "Kendi PNG kapak" || settings.CoverBackground != "" || settings.CoverLogo != "") throw new InvalidOperationException("Seçilen yerel kapak görselleri buluta aktarılmaz. Bu paketi Windows'ta üret veya yerel görsel gerektirmeyen şablon seç.");

        if (episode.Plan != null && Rules.Scripts(episode).Any(x => x.Script.Scenes.Any(s => s.UserLocal))) throw new InvalidOperationException("Sahnelerde seçilen yerel videolar buluta aktarılmaz. Bu paketi Windows'ta üret.");

        if (settings.VoiceProvider.StartsWith("XTTS") || episode.ShortCount > 0 && settings.ShortVoiceOverride && settings.ShortVoiceProvider.StartsWith("XTTS")) throw new InvalidOperationException("Harici XTTS yerel üretimde kullanılabilir. GitHub üretimi için desteklenen bulut sesini seç.");
        if (settings.VoiceProvider.StartsWith("Piper", StringComparison.Ordinal) || episode.ShortCount > 0 && settings.ShortVoiceOverride && settings.ShortVoiceProvider.StartsWith("Piper", StringComparison.Ordinal)) throw new InvalidOperationException("Yerel ses motoru GitHub'da kullanılamaz. Desteklenen bulut sesini seç veya Windows'ta üret.");

        if (episode.Plan != null && Rules.Scripts(episode).Any(x => x.Script.MusicTrack?.Enabled == true)) throw new InvalidOperationException("Editörde seçilen yerel müzik dosyası bu sürümde buluta aktarılmaz. Bu paketi Windows üzerinde üret.");

        bool google = settings.VoiceProvider.StartsWith("Google") && episode.IncludeLong || episode.ShortCount > 0 && (settings.ShortVoiceOverride && settings.ShortVoiceProvider != "Normal videoyla aynı" ? settings.ShortVoiceProvider : settings.VoiceProvider).StartsWith("Google");

        if (google) throw new InvalidOperationException("GitHub Google TTS üretimi, hesap geneli ortak kota rezervasyonu hazır olana kadar kapalı. Bu paketi Windows üzerinde üret; yerel kota koruman devam eder.");

    }

    public async Task Dispatch(Episode episode, CancellationToken ct)

    {

        CheckRemoteVoice(store.Settings, episode);

        if (episode.RemoteRunId != "") throw new InvalidOperationException("Bu paket zaten GitHub'a gönderilmiş. Çalışmaları yenileyip sonucu al.");

        // Payload contains settings and an unapproved episode, never credentials.

        var payloadEpisode = JsonSerializer.Deserialize<Episode>(JsonSerializer.Serialize(episode, Json.Options), Json.Options)!;

        payloadEpisode.Approved = false; payloadEpisode.ApprovalHash = ""; payloadEpisode.Videos = []; payloadEpisode.Events = []; payloadEpisode.Status = "Taslak";

        var payloadSettings = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(store.Settings, Json.Options), Json.Options)!;

        payloadSettings.UseOllama = false; payloadSettings.LocalAiFirst = false; payloadSettings.LocalAiPolicyVersion = 1; payloadSettings.ProviderOrder = string.Join(',', payloadSettings.ProviderOrder.Split(',').Where(x => x.Trim() != "Ollama"));

        payloadSettings.VideoBackupFolder = ""; payloadSettings.AutoPublish = false; payloadSettings.AutoProduce = false; payloadSettings.AutoResearch = false; payloadSettings.FfmpegPath = ""; payloadSettings.MusicPath = "";

        SettingsValidation.Check(payloadSettings);

        if (episode.Music) throw new InvalidOperationException("Yerel müzik dosyası GitHub'a gönderilmez. GitHub üretimi için paket müziğini kapat.");

        var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new WorkerPayload { Episode = payloadEpisode, Settings = payloadSettings }, Json.Options)));

        if (payload.Length > 55000) throw new InvalidOperationException("Paket GitHub tetikleme boyut sınırını aşıyor. Araştırması yapılmamış yeni paket gönder.");

        await Call(HttpMethod.Post, "repos/" + Repo + "/actions/workflows/produce.yml/dispatches", new { @ref = store.Settings.GitHubRef, inputs = new { payload, job_id = episode.Id } }, ct);

        episode.RemoteRunId = "gönderildi"; episode.RemoteRepo = Repo; episode.RemoteState = "Tetikleme kabul edildi; çalışma bekleniyor"; episode.Status = "GitHub'da"; episode.Log("GitHub üretimi gönderildi. Çalışma tamamlanınca çıktıyı bu panelden al."); store.Save();

    }

    public async Task<List<string>> SecretNames(CancellationToken ct) { using var d = JsonDocument.Parse(await Call(HttpMethod.Get, "repos/" + Repo + "/actions/secrets?per_page=100", null, ct)); return d.RootElement.GetProperty("secrets").EnumerateArray().Select(s => s.GetProperty("name").GetString()!).ToList(); }

    public static readonly IReadOnlyList<string> ServiceSecretNames = Array.AsReadOnly(new[] { "NVIDIA_API_KEY", "KIMI_API_KEY", "GEMINI_API_KEY", "GROQ_API_KEY", "OPENROUTER_API_KEY", "TAVILY_API_KEY", "PEXELS_API_KEY", "PIXABAY_API_KEY", "VECTEEZY_ACCOUNT_ID", "VECTEEZY_SECRET_KEY", "ELEVENLABS_API_KEY", "OPENAI_API_KEY", "GOOGLE_TTS_API_KEY" });

    public sealed record SecretTransferResult(List<string> Sent, List<string> Missing, Dictionary<string,string> Failed);

    public async Task<SecretTransferResult> SendRegisteredSecrets(CancellationToken ct, Action<int,int,string>? progress = null)

    {

        _ = Repo;

        var registered = ServiceSecretNames.Select(name => (Name:name,Value:store.Key(name))).ToArray();

        var pending = registered.Where(x=>!string.IsNullOrWhiteSpace(x.Value)).ToArray();

        if (pending.Length==0) throw new InvalidOperationException("Bağlantı Merkezi'nde gönderilecek hizmet anahtarı kayıtlı değil.");

        var result = new SecretTransferResult([], registered.Where(x=>string.IsNullOrWhiteSpace(x.Value)).Select(x=>x.Name).ToList(), []);

        int done=0;

        foreach(var item in pending) {

            ct.ThrowIfCancellationRequested(); progress?.Invoke(done,pending.Length,item.Name);

            try { await SetSecret(item.Name,item.Value,ct); result.Sent.Add(item.Name); }

            catch(OperationCanceledException) { throw; }

            catch(ProviderException ex) { result.Failed[item.Name]="GitHub HTTP "+ex.Status; }

            catch(Exception) { result.Failed[item.Name]="Gönderim tamamlanamadı; bağlantı ve depo izinlerini kontrol et."; }

            done++; progress?.Invoke(done,pending.Length,item.Name);

        }

        return result;

    }

    public async Task SetSecret(string name, string value, CancellationToken ct)

    {

        if (!Regex.IsMatch(name, "^[A-Z][A-Z0-9_]+$")) throw new InvalidOperationException("Geçersiz secret adı.");

        using var d = JsonDocument.Parse(await Call(HttpMethod.Get, "repos/" + Repo + "/actions/secrets/public-key", null, ct));

        var encrypted = Sodium.SealedPublicKeyBox.Create(Encoding.UTF8.GetBytes(value), Convert.FromBase64String(d.RootElement.GetProperty("key").GetString()!));

        await Call(HttpMethod.Put, "repos/" + Repo + "/actions/secrets/" + name, new { encrypted_value = Convert.ToBase64String(encrypted), key_id = d.RootElement.GetProperty("key_id").GetString() }, ct);

    }

    public async Task Import(string runId, CancellationToken ct)

    {

        if (!long.TryParse(runId, out _)) throw new InvalidOperationException("Tamamlanmış bir GitHub çalışması seç.");

        using var d = JsonDocument.Parse(await Call(HttpMethod.Get, "repos/" + Repo + "/actions/runs/" + runId + "/artifacts", null, ct));

        var item = d.RootElement.GetProperty("artifacts").EnumerateArray().FirstOrDefault(x => x.GetProperty("name").GetString()!.StartsWith("anka-package-") && !x.GetProperty("expired").GetBoolean());

        if (item.ValueKind != JsonValueKind.Object) throw new InvalidOperationException("Bu çalışmada indirilebilir TubeFlow paketi henüz yok.");

        var folder = Path.Combine(store.Root, "imports", runId, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder); var zipPath = Path.Combine(folder, "package.zip");

        using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/repos/" + Repo + "/actions/artifacts/" + item.GetProperty("id") + "/zip"); req.Headers.Authorization = new("Bearer", store.Key("GITHUB_TOKEN")); req.Headers.UserAgent.ParseAdd("AnkaStudio/0.1");

        using var res = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct); if (!res.IsSuccessStatusCode) throw new ProviderException("GitHub çıktı indirme", (int)res.StatusCode);

        await Reliability.DownloadResponse(res, zipPath, 2L * 1024 * 1024 * 1024, ct);

        var extracted = Path.Combine(folder, "files"); Directory.CreateDirectory(extracted);

        using (var zip = ZipFile.OpenRead(zipPath))

        {

            long total = 0;

            foreach (var entry in zip.Entries)

            {

                total += entry.Length; if (total > 8L * 1024 * 1024 * 1024) throw new InvalidOperationException("GitHub paketi boyut sınırını aşıyor.");

                var path = Path.GetFullPath(Path.Combine(extracted, entry.FullName)); if (!path.StartsWith(extracted + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Güvenli olmayan paket yolu.");

                if (entry.Name == "") continue; var ext = Path.GetExtension(path).ToLowerInvariant(); if (ext is not (".json" or ".mp4" or ".png" or ".srt" or ".ass" or ".txt" or ".hash" or ".wav" or ".mp3")) continue;

                Directory.CreateDirectory(Path.GetDirectoryName(path)!); entry.ExtractToFile(path, true);

            }

        }

        var exports = Directory.GetFiles(extracted, "episode-export.json", SearchOption.AllDirectories);

        if (exports.Length != 1) throw new InvalidOperationException("GitHub çıktısında tam olarak bir paket kayıt dosyası bulunmalı. Önceki indirmeler bu pakete eklenmez.");

        var export = exports[0];

        var episode = Json.Read(export, new Episode()); if (!Regex.IsMatch(episode.Id, "^[a-f0-9]{32}$")) throw new InvalidOperationException("Paket kimliği geçersiz.");

        Rules.Validate(episode); Rules.ValidatePlan(episode);

        var original = store.Episodes.FirstOrDefault(e => e.Id == episode.Id); if (original?.Videos.Any(v => v.YouTubeId != "") == true) throw new InvalidOperationException("Yüklenmiş paket uzak çıktıyla değiştirilemez.");

        var bindings = episode.Videos.ToDictionary(v => v.Key, v => v.PlanHash != "" && v.PlanHash == Reliability.MediaPlanHash(episode, v.Key));

        foreach (var (_, _, script) in Rules.Scripts(episode)) foreach (var scene in script.Scenes) {

            var mediaName = Path.GetFileName(scene.StockFile);

            scene.StockFile = mediaName == "" ? "" : Directory.GetFiles(extracted, mediaName, SearchOption.AllDirectories).FirstOrDefault() ?? "";

            if (scene.UserLocal) scene.RightsConfirmed = false;

        }

        foreach (var asset in episode.Videos)

        {

            var match = Directory.GetFiles(extracted, "video.mp4", SearchOption.AllDirectories).FirstOrDefault(f => f.Split(Path.DirectorySeparatorChar).Contains($"r{episode.Revision}-{asset.Key}") && Reliability.FileHash(f) == asset.FileHash);

            asset.File = match ?? ""; asset.Thumbnail = match == null ? "" : Path.Combine(Path.GetDirectoryName(match)!, "thumbnail.png"); asset.YouTubeId = ""; asset.YouTubeState = ""; asset.YouTubeCheckedAt = null; asset.UploadSession = ""; asset.Scheduled = false;

            if (match == null || !bindings.GetValueOrDefault(asset.Key)) asset.QualityPassed = false;

            else { await using var stream = File.OpenRead(match); var hash = Convert.ToHexString(await System.Security.Cryptography.SHA256.HashDataAsync(stream, ct)); if (asset.FileHash != hash) { asset.QualityPassed = false; asset.Checks.Add("GitHub medya imzası uyuşmuyor. Yeniden üretim gerekiyor."); } }

        }

        foreach (var asset in episode.Videos) { if (asset.QualityPassed) { await Reliability.ValidateVideo(store, asset.File, ct, complete: true); asset.PlanHash = Reliability.MediaPlanHash(episode, asset.Key); } }

        // Imported content always returns to human review; remote approval is never trusted.

        episode.Approved = false; episode.ApprovalHash = ""; episode.RemoteRepo = Repo; episode.RemoteState = original?.RemoteState ?? "Çıktı içeri alındı"; episode.RemoteRunId = runId; episode.Status = episode.Error != "" ? "Bekliyor" : episode.Videos.Count == 0 ? (episode.Plan == null ? "Taslak" : "Senaryo hazır") : "Denetim bekliyor";

        if (original != null) store.Episodes.Remove(original); store.Episodes.Add(episode); store.Save();

    }

}



public class WorkerPayload { public Episode Episode { get; set; } = new(); public Settings Settings { get; set; } = new(); }

public static class Worker

{

    public static async Task Run(Store store, string[] args)

    {

        if (Program.Arg(args, "--payload") is string payload)

        {

            var p = JsonSerializer.Deserialize<WorkerPayload>(Encoding.UTF8.GetString(Convert.FromBase64String(await File.ReadAllTextAsync(payload))), Json.Options) ?? throw new InvalidOperationException("İş paketi geçersiz.");

            // Copy the non-secret configuration only; cloud secrets come from environment variables.

            p.Settings.UseOllama = false; p.Settings.LocalAiFirst = false; p.Settings.LocalAiPolicyVersion = 1; p.Settings.AutoPublish = false; p.Settings.AutoProduce = false; p.Settings.AutoResearch = false;

            store.ApplySettings(p.Settings);

            store.Settings.FfmpegPath = Environment.GetEnvironmentVariable("FFMPEG_PATH") ?? "";

            p.Episode.Approved = false; p.Episode.ApprovalHash = ""; store.Episodes.Add(p.Episode); store.Save();

        }

        var e = store.Episodes.FirstOrDefault(x => x.Id == Program.Arg(args, "--episode")) ?? store.Episodes.FirstOrDefault() ?? throw new InvalidOperationException("İş paketi yok.");

        GitHub.CheckRemoteVoice(store.Settings, e);

        var engine = new Engine(store); if (e.Plan == null) await engine.Plan(e); if (e.Plan != null) await engine.Produce(e);

        Json.Write(Path.Combine(store.Root, "episode-export.json"), e);

        if (e.Status is "Bekliyor" or "Kesildi" or "Durduruldu") throw new InvalidOperationException(e.Error == "" ? e.Status : e.Error);

    }

}

