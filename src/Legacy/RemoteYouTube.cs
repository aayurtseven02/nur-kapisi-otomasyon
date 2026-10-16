using System.IO.Compression;

using System.Net.Http.Headers;

using System.Security.Cryptography;

using System.Text;

using System.Text.Json;

using System.Text.RegularExpressions;



namespace AnkaStudio;



public sealed class UploadBundle

{

    public string JobId { get; set; } = "";

    public string ChannelId { get; set; } = "";

    public string CategoryId { get; set; } = "27";

    public bool Schedule { get; set; }

    public Episode Episode { get; set; } = new();

}

public sealed class UploadCheckpoint

{

    public string JobId { get; set; } = "";

    public string EpisodeId { get; set; } = "";

    public string ChannelId { get; set; } = "";

    public string State { get; set; } = "uploading";

    public string Note { get; set; } = "";

    public DateTimeOffset CheckedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<AssetVideo> Videos { get; set; } = [];

}



public sealed partial class GitHub

{

    static void JobId(string id) { if (!Regex.IsMatch(id, "^[a-f0-9]{32}$")) throw new InvalidOperationException("Yükleme iş kimliği geçersiz."); }

    string StatePath(string job) { JobId(job); return "repos/" + Repo + "/contents/.anka/uploads/" + job + ".json"; }



    public async Task<UploadCheckpoint?> ReadUploadCheckpoint(string job, CancellationToken ct)

    {

        try {

            using var d = JsonDocument.Parse(await Call(HttpMethod.Get, StatePath(job) + "?ref=" + Uri.EscapeDataString(store.Settings.GitHubRef), null, ct));

            return JsonSerializer.Deserialize<UploadCheckpoint>(Convert.FromBase64String(d.RootElement.GetProperty("content").GetString()!.Replace("\n", "")), Json.Options);

        } catch (ProviderException ex) when (ex.Status == 404) { return null; }

    }

    public async Task WriteUploadCheckpoint(UploadCheckpoint state, CancellationToken ct)

    {

        var path = StatePath(state.JobId); state.CheckedAt = DateTimeOffset.UtcNow;

        for (int attempt = 0; attempt < 3; attempt++) {

            string? sha = null;

            try { using var existing = JsonDocument.Parse(await Call(HttpMethod.Get, path + "?ref=" + Uri.EscapeDataString(store.Settings.GitHubRef), null, ct)); sha = existing.RootElement.GetProperty("sha").GetString(); }

            catch (ProviderException ex) when (ex.Status == 404) { }

            var body = new Dictionary<string, object?> { ["message"] = "Anka yükleme durumu [skip ci]", ["content"] = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(state, Json.Options)), ["branch"] = store.Settings.GitHubRef };

            if (sha != null) body["sha"] = sha;

            try { await Call(HttpMethod.Put, path, body, ct); return; }

            catch (ProviderException ex) when (ex.Status == 409 && attempt < 2) { await Task.Delay(500, ct); }

        }

    }



    public Task RemoveUploadMedia(long assetId, CancellationToken ct) {

        if (assetId <= 0) throw new InvalidOperationException("Geçersiz aktarım varlığı.");

        return Call(HttpMethod.Delete, "repos/" + Repo + "/releases/assets/" + assetId, null, ct);

    }

    public static void ValidateUpload(Episode episode, bool schedule, DateTimeOffset now)

    {

        if (episode.Demo || !episode.Approved || episode.ApprovalHash != Rules.HashPlan(episode) || !Reliability.OutputsCurrent(episode)) throw new InvalidOperationException("Önce gerçek videoları üret, incele ve onayla.");

        if (episode.ApprovedChannelId == "") throw new InvalidOperationException("Onaylanan YouTube kanalı eksik.");

        if (schedule && episode.Videos.Any(v => Engine.NeedsSchedule(v) && (v.PublishAt == null || v.PublishAt <= now.AddMinutes(10)))) throw new InvalidOperationException("Her video için en az 10 dakika ileride yayın tarihi belirle.");

    }

    public async Task SendYouTubeUpload(Episode episode, bool schedule, CancellationToken ct, Action<int, string>? progress = null)

    {

        progress?.Invoke(0, "Depo, bağlantı ve onay kontrol ediliyor…");

        using (var repo = JsonDocument.Parse(await Call(HttpMethod.Get, "repos/" + Repo, null, ct)))

            if (!repo.RootElement.GetProperty("private").GetBoolean()) throw new InvalidOperationException("Video aktarımı için özel GitHub deposu gerekiyor.");

        if (episode.ApprovedChannelId != store.Settings.YouTubeChannelId) throw new InvalidOperationException("Onaylanan kanal ile bağlı kanal uyuşmuyor.");

        // Fail before moving files or credentials if the workflow has not been installed.

        try { await Call(HttpMethod.Get, "repos/" + Repo + "/actions/workflows/upload-youtube.yml", null, ct); }

        catch (ProviderException ex) when (ex.Status == 404) { throw new InvalidOperationException("YouTube yükleme iş akışı bulunamadı veya erişilemiyor. Önce Kaynak + iş akışlarını gönder düğmesiyle bu sürümü depoya gönder; depo izinlerini de kontrol et."); }

        if (episode.RemoteUploadJob != "") {

            if (episode.RemoteUploadRepo != Repo) throw new InvalidOperationException("Mevcut yükleme işi başka depoda. Önce o deponun durumunu kontrol et.");

            progress?.Invoke(10, "Önceki yükleme durumu kontrol ediliyor…");

            await SyncUpload(episode, ct);

            if (episode.RemoteUploadState == "completed") throw new InvalidOperationException("Bu yükleme tamamlanmış. Yeni tarih için programdan yayın ayarını değiştir.");

            if (episode.RemoteUploadState is not ("failed" or "hazır")) throw new InvalidOperationException("Yükleme zaten gönderilmiş. Durum kontrolünü kullan; ikinci iş gönderilmedi.");

            if (schedule != episode.RemoteUploadSchedule) throw new InvalidOperationException("Devam eden işin yayın seçimi değiştirilemez.");

            ValidateUpload(episode, schedule, DateTimeOffset.UtcNow);

            await WriteUploadCheckpoint(new UploadCheckpoint { JobId = episode.RemoteUploadJob, EpisodeId = episode.Id, ChannelId = episode.ApprovedChannelId, State = "hazır", Videos = episode.Videos }, ct);

        } else {

            ValidateUpload(episode, schedule, DateTimeOffset.UtcNow);

            var newJob = Guid.NewGuid().ToString("N");

            var folder = Path.Combine(store.Root, "cloud-uploads", newJob); Directory.CreateDirectory(folder);

            var zip = Path.Combine(folder, "upload.zip");

            progress?.Invoke(10, "Video yedeği ve aktarım paketi hazırlanıyor…");

            await VideoBackup.Save(store, episode, ct);

            var copy = JsonSerializer.Deserialize<Episode>(JsonSerializer.Serialize(episode, Json.Options), Json.Options)!;

            copy.Events = []; copy.RemoteUploadJob = ""; copy.RemoteRunId = "";

            using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create)) {

                for (int i = 0; i < copy.Videos.Count; i++) {

                    ct.ThrowIfCancellationRequested(); var source = episode.Videos[i]; var asset = copy.Videos[i];

                    if (new FileInfo(source.File).Length > 1900L * 1024 * 1024) throw new InvalidOperationException("Video geçici aktarım sınırını aşıyor (1,9 GB).");

                    asset.File = "videos/" + i + ".mp4"; archive.CreateEntryFromFile(source.File, asset.File, CompressionLevel.NoCompression);

                    if (File.Exists(source.Thumbnail)) { asset.Thumbnail = "covers/" + i + ".png"; archive.CreateEntryFromFile(source.Thumbnail, asset.Thumbnail); } else asset.Thumbnail = "";

                }

                using var output = archive.CreateEntry("upload.json").Open();

                JsonSerializer.Serialize(output, new UploadBundle { JobId = newJob, ChannelId = episode.ApprovedChannelId, CategoryId = store.Settings.CategoryId, Schedule = schedule, Episode = copy }, Json.Options);

            }

            if (new FileInfo(zip).Length > 1900L * 1024 * 1024) throw new InvalidOperationException("Paket geçici aktarım sınırını aşıyor (1,9 GB). Daha küçük paket kullan.");

            progress?.Invoke(20, "Video paketi GitHub’a aktarılıyor…");

            using var release = JsonDocument.Parse(await Call(HttpMethod.Post, "repos/" + Repo + "/releases", new { tag_name = "anka-upload-" + newJob, target_commitish = store.Settings.GitHubRef, name = "Anka geçici video aktarımı", draft = true }, ct));

            var releaseId = release.RootElement.GetProperty("id").GetInt64();

            await using var stream = File.OpenRead(zip);

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://uploads.github.com/repos/" + Repo + "/releases/" + releaseId + "/assets?name=upload.zip");

            request.Headers.Authorization = new("Bearer", store.Key("GITHUB_TOKEN")); request.Headers.UserAgent.ParseAdd("AnkaStudio"); request.Content = new TransferContent(stream, (sent, total) => progress?.Invoke(20 + (int)(65 * sent / Math.Max(1, total)), $"GitHub’a aktarılıyor: {sent / 1048576d:0.0} / {total / 1048576d:0.0} MB")); request.Content.Headers.ContentType = new("application/zip");

            using var response = await http.SendAsync(request, ct); if (!response.IsSuccessStatusCode) throw new ProviderException("GitHub video aktarımı", (int)response.StatusCode);

            using var assetDoc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));

            episode.RemoteUploadJob = newJob; episode.RemoteUploadRepo = Repo; episode.RemoteUploadRelease = releaseId; episode.RemoteUploadAsset = assetDoc.RootElement.GetProperty("id").GetInt64(); episode.RemoteUploadSchedule = schedule; episode.RemoteUploadState = "hazır"; store.Save();

        }

        var credentials = new Dictionary<string, string> { ["GOOGLE_CLIENT_ID"] = store.Settings.GoogleClientId, ["GOOGLE_CLIENT_SECRET"] = store.Key("GOOGLE_CLIENT_SECRET"), ["YOUTUBE_REFRESH_TOKEN"] = store.Key("YOUTUBE_REFRESH_TOKEN") };

        if (credentials.Values.Any(string.IsNullOrWhiteSpace)) throw new InvalidOperationException("YouTube OAuth bağlantısı eksik. Kanalı programdan yeniden bağla.");

        progress?.Invoke(88, "YouTube bağlantı bilgileri güvenli biçimde gönderiliyor…");

        foreach (var pair in credentials) await SetSecret(pair.Key, pair.Value, ct);

        progress?.Invoke(95, "GitHub YouTube yükleme işi başlatılıyor…");

        episode.RemoteUploadState = "gönderildi"; store.Save(); // Ambiguous dispatch failures remain blocked until checked.

        try { await Call(HttpMethod.Post, "repos/" + Repo + "/actions/workflows/upload-youtube.yml/dispatches", new { @ref = store.Settings.GitHubRef, inputs = new { job_id = episode.RemoteUploadJob, episode_id = episode.Id, asset_id = episode.RemoteUploadAsset.ToString() } }, ct); }

        catch (ProviderException ex) when (ex.Status is 400 or 401 or 403 or 404 or 409 or 422 or 429) { episode.RemoteUploadState = "hazır"; store.Save(); throw; }

        progress?.Invoke(100, "GitHub’a gönderim tamamlandı. YouTube yüklemesi bulutta devam ediyor; durum kontrolünden izleyebilirsin.");

        episode.Log("Hazır video GitHub üzerinden YouTube’a " + (schedule ? "planlı" : "gizli") + " yükleme için gönderildi."); store.Save();

    }



    public async Task SyncUpload(Episode episode, CancellationToken ct)

    {

        if (episode.RemoteUploadJob == "" || episode.RemoteUploadRepo != Repo) return;

        var state = await ReadUploadCheckpoint(episode.RemoteUploadJob, ct);

        if (state == null) {

            using var runs = JsonDocument.Parse(await Call(HttpMethod.Get, "repos/" + Repo + "/actions/workflows/upload-youtube.yml/runs?per_page=100", null, ct));

            var run = runs.RootElement.GetProperty("workflow_runs").EnumerateArray().FirstOrDefault(r => r.GetProperty("display_title").GetString() == "Anka YouTube " + episode.RemoteUploadJob);

            if (run.ValueKind == JsonValueKind.Object && run.GetProperty("status").GetString() == "completed") { episode.RemoteUploadState = "failed"; store.Save(); }

            return;

        }

        if (state.State is not ("completed" or "failed")) {

            using var runDoc = JsonDocument.Parse(await Call(HttpMethod.Get, "repos/" + Repo + "/actions/workflows/upload-youtube.yml/runs?per_page=100", null, ct));

            var ended = runDoc.RootElement.GetProperty("workflow_runs").EnumerateArray().FirstOrDefault(r => r.GetProperty("display_title").GetString() == "Anka YouTube " + episode.RemoteUploadJob);

            if (ended.ValueKind == JsonValueKind.Object && ended.GetProperty("status").GetString() == "completed") { state.State = "failed"; state.Note = "İş tamamlanmadan durdu; oturum korunur ve tekrar denenebilir."; }

        }

        if (state.JobId != episode.RemoteUploadJob || state.EpisodeId != episode.Id || state.ChannelId != episode.ApprovedChannelId) throw new InvalidOperationException("Uzak yükleme sonucu paket / kanal ile uyuşmuyor.");

        if (state.Videos.Count != episode.Videos.Count || state.Videos.Select(v => v.Key).Distinct().Count() != state.Videos.Count || state.State == "completed" && state.Videos.Any(v => v.YouTubeId == "")) throw new InvalidOperationException("Uzak yükleme sonuç sayısı eksik veya tutarsız.");

        foreach (var remote in state.Videos) {

            var local = episode.Videos.SingleOrDefault(v => v.Key == remote.Key);

            if (local == null || local.FileHash != remote.FileHash) throw new InvalidOperationException("Uzak yükleme farklı video sürümüne ait. Yerel bağlantılar değiştirilmedi.");

        }

        foreach (var remote in state.Videos) {

            var local = episode.Videos.Single(v => v.Key == remote.Key);

            local.YouTubeId = remote.YouTubeId; local.UploadSession = remote.UploadSession; local.YouTubeState = remote.YouTubeState; local.YouTubeCheckedAt = remote.YouTubeCheckedAt;

            local.Scheduled = remote.Scheduled; local.RemotePublishAt = remote.RemotePublishAt;

            local.ThumbnailUploaded = remote.ThumbnailUploaded; local.ThumbnailAttempted = remote.ThumbnailAttempted; local.ThumbnailNote = remote.ThumbnailNote;

        }

        episode.RemoteUploadState = state.State; episode.RemoteUploadNote = state.Note; store.Save();

    }

}



public static class RemoteYouTubeWorker

{

    public static string Inside(string root, string relative)

    {

        if (Path.IsPathRooted(relative) || relative.Contains(':')) throw new InvalidOperationException("Geçersiz aktarım dosya yolu.");

        var basePath = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        var path = Path.GetFullPath(Path.Combine(basePath, relative));

        if (!path.StartsWith(basePath, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Aktarım dosyası paket dışına çıkıyor.");

        return path;

    }

    public static async Task Run(Store store, string[] args, HttpClient? githubHttp = null, HttpClient? youtubeHttp = null)

    {

        var bundlePath = Program.Arg(args, "--upload-bundle") ?? throw new InvalidOperationException("Aktarım paketi yok.");

        var bundle = Json.Read(bundlePath, new UploadBundle());

        if (!Regex.IsMatch(bundle.JobId, "^[a-f0-9]{32}$") || bundle.JobId != Environment.GetEnvironmentVariable("ANKA_UPLOAD_JOB") || bundle.Episode.Id != Environment.GetEnvironmentVariable("ANKA_EPISODE_ID")) throw new InvalidOperationException("Aktarım iş kimliği uyuşmuyor.");

        var folder = Path.GetDirectoryName(Path.GetFullPath(bundlePath))!; var episode = bundle.Episode;

        foreach (var video in episode.Videos) { video.File = Inside(folder, video.File); if (video.Thumbnail != "") video.Thumbnail = Inside(folder, video.Thumbnail); }

        store.Settings.GoogleClientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID") ?? "";

        store.Settings.YouTubeChannelId = bundle.ChannelId; store.Settings.CategoryId = bundle.CategoryId;

        store.Settings.Repo = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY") ?? "";

        store.Settings.GitHubRef = Environment.GetEnvironmentVariable("ANKA_BRANCH") ?? "main";

        episode.RemoteUploadJob = ""; store.Episodes.Add(episode); store.Save();

        var github = new GitHub(store, githubHttp); var previous = await github.ReadUploadCheckpoint(bundle.JobId, CancellationToken.None);

        if (previous != null) {

            if (previous.JobId != bundle.JobId || previous.EpisodeId != episode.Id || previous.ChannelId != bundle.ChannelId) throw new InvalidOperationException("Devam kaydı uyuşmuyor.");

            foreach (var remote in previous.Videos) {

                var local = episode.Videos.Single(v => v.Key == remote.Key);

                if (local.FileHash != remote.FileHash) throw new InvalidOperationException("Devam kaydında video imzası uyuşmuyor.");

                local.PublishAt = remote.PublishAt; local.YouTubeState = remote.YouTubeState; local.YouTubeId = remote.YouTubeId; local.UploadSession = remote.UploadSession; local.Scheduled = remote.Scheduled; local.RemotePublishAt = remote.RemotePublishAt; local.ThumbnailUploaded = remote.ThumbnailUploaded;

            }

            if (previous.State == "completed") return;

        }

        var state = new UploadCheckpoint { JobId = bundle.JobId, EpisodeId = episode.Id, ChannelId = bundle.ChannelId, Videos = episode.Videos };

        async Task Save(CancellationToken ct) { state.Videos = episode.Videos; await github.WriteUploadCheckpoint(state, ct); }

        await Save(CancellationToken.None);

        try {

            GitHub.ValidateUpload(episode, bundle.Schedule, DateTimeOffset.UtcNow);

            var youtube = new YouTube(store, youtubeHttp, Save);

            await new Engine(store).Upload(episode, bundle.Schedule, youtube);

            state.State = episode.Error == "" && episode.Status != "Durduruldu" ? "completed" : "failed";

        } catch (Exception) { state.State = "failed"; }

        state.Note = state.State == "failed" ? "Yükleme durdu; bağlantı, kota veya izinleri kontrol et. Oturum korunur. " + Regex.Match(episode.Error, @"HTTP \d{3}").Value : "YouTube yüklemesi tamamlandı.";

        await Save(CancellationToken.None);

        if (state.State == "completed" && long.TryParse(Environment.GetEnvironmentVariable("ANKA_ASSET_ID"), out var assetId)) {

            try { await github.RemoveUploadMedia(assetId, CancellationToken.None); state.Note += " Geçici GitHub video dosyası temizlendi."; }

            catch (Exception) { state.Note += " Geçici dosya temizlenemedi; yükleme sonucu korunuyor."; }

            await Save(CancellationToken.None);

        }

        // Receipt never contains OAuth keys or full local settings.

        Json.Write(Path.Combine(store.Root, "upload-result.json"), state);

        if (state.State != "completed") throw new InvalidOperationException(state.Note);

    }

}

