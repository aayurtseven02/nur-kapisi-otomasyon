using System.Diagnostics;

using System.Net;

using System.Net.Http.Headers;

using System.Net.Sockets;

using System.Security.Cryptography;

using System.Text;

using System.Text.Json;



namespace AnkaStudio;



public sealed class YouTube(Store store, HttpClient? client = null, Func<CancellationToken, Task>? checkpoint = null)

{

    static readonly HttpClient SharedHttp = new() { Timeout = TimeSpan.FromMinutes(10) };

    readonly HttpClient http = client ?? SharedHttp;

    async Task Save(CancellationToken ct) { store.Save(); if (checkpoint != null) await checkpoint(ct); }

    string ClientId => store.Settings.GoogleClientId;

    string ClientSecret => store.Key("GOOGLE_CLIENT_SECRET");

    static string B64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public async Task Connect(CancellationToken ct, bool analytics=false, Action<string>? report=null)

    {

        if (ClientId == "" || ClientSecret == "") throw new InvalidOperationException("Google Cloud Desktop OAuth istemci kimliği ve gizli anahtarı gerekiyor. Bağlantılar ekranına ekle.");

        var verifier = B64(RandomNumberGenerator.GetBytes(48)); var state = B64(RandomNumberGenerator.GetBytes(32)); var challenge = B64(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();

        try

        {

            var port = ((IPEndPoint)listener.LocalEndpoint).Port; var redirect = $"http://127.0.0.1:{port}/";

            var values = new Dictionary<string, string> { ["client_id"] = ClientId, ["redirect_uri"] = redirect, ["response_type"] = "code", ["scope"] = "https://www.googleapis.com/auth/youtube.upload https://www.googleapis.com/auth/youtube.force-ssl https://www.googleapis.com/auth/youtube.readonly", ["access_type"] = "offline", ["prompt"] = "consent", ["state"] = state, ["code_challenge"] = challenge, ["code_challenge_method"] = "S256" };
            if(analytics) values["scope"]+=" https://www.googleapis.com/auth/yt-analytics.readonly";

            Process.Start(new ProcessStartInfo("https://accounts.google.com/o/oauth2/v2/auth?" + string.Join('&', values.Select(v => Uri.EscapeDataString(v.Key) + "=" + Uri.EscapeDataString(v.Value)))) { UseShellExecute = true });

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromMinutes(4));

            report?.Invoke("Tarayıcıdaki Google onayı bekleniyor…");
            var query = await OAuthLoopback.Receive(listener,redirect,state,timeout.Token);
            report?.Invoke("Google onayı alındı • izin kaydediliyor…");

            if (query.ContainsKey("error")) throw new InvalidOperationException("Google bağlantı izni verilmedi.");

            using var response = await http.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(new Dictionary<string, string> { ["client_id"] = ClientId, ["client_secret"] = ClientSecret, ["code"] = query.GetValueOrDefault("code", ""), ["redirect_uri"] = redirect, ["grant_type"] = "authorization_code", ["code_verifier"] = verifier }), ct);

            if (!response.IsSuccessStatusCode) throw new ProviderException("Google OAuth", (int)response.StatusCode);

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));

            if(analytics && (!doc.RootElement.TryGetProperty("scope",out var scope) || !scope.GetString()!.Split(' ').Contains("https://www.googleapis.com/auth/yt-analytics.readonly")))
                throw new InvalidOperationException("Google istatistik okuma iznini vermedi. İzin ekranında YouTube Analytics erişimini seçip yeniden onayla. Önceki kanal bağlantısı korundu.");
            if (!doc.RootElement.TryGetProperty("refresh_token", out var refresh)) throw new InvalidOperationException("Google yenileme anahtarı dönmedi. Hesap izinlerini sıfırlayıp yeniden bağlan.");

            store.Vault.Set("YOUTUBE_REFRESH_TOKEN", refresh.GetString()!);
            report?.Invoke(analytics?"İstatistik izni doğrulandı ve kaydedildi. Raporlar alınıyor…":"Google bağlantısı kaydedildi.");

        }

        finally { listener.Stop(); }

    }

    public async Task<string> Token(CancellationToken ct)

    {

        var refresh = store.Key("YOUTUBE_REFRESH_TOKEN"); if (refresh == "") throw new InvalidOperationException("YouTube kanalı bağlı değil. Bağlantılar ekranından Google hesabını bağla.");

        using var response = await http.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(new Dictionary<string, string> { ["client_id"] = ClientId, ["client_secret"] = ClientSecret, ["refresh_token"] = refresh, ["grant_type"] = "refresh_token" }), ct);

        if (!response.IsSuccessStatusCode) throw new ProviderException("Google yenileme", (int)response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)); return doc.RootElement.GetProperty("access_token").GetString()!;

    }

    public async Task<string> Channel(CancellationToken ct)

    {

        var channel = await ChannelIdentity(ct); store.Settings.YouTubeChannelId = channel.Id; store.Settings.YouTubeChannelTitle = channel.Title; await Save(ct); return channel.Title;

    }

    public async Task<(string Id, string Title)> ChannelIdentity(CancellationToken ct)

    {

        using var req = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/youtube/v3/channels?part=snippet&mine=true"); req.Headers.Authorization = new("Bearer", await Token(ct));

        using var res = await http.SendAsync(req, ct); if (!res.IsSuccessStatusCode) throw new ProviderException("YouTube kanal", (int)res.StatusCode);

        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct)); var items = doc.RootElement.GetProperty("items"); if (items.GetArrayLength() == 0) throw new InvalidOperationException("Bu Google hesabında YouTube kanalı bulunamadı."); if (items.GetArrayLength() != 1) throw new InvalidOperationException("Tek bir hedef kanal doğrulanamadı."); return (items[0].GetProperty("id").GetString()!, items[0].GetProperty("snippet").GetProperty("title").GetString()!);

    }

    public static string BuildDescription(VideoScript script, IEnumerable<SourceRef> sources, bool includeSources = true)

    {

        // Visual matching explanations are project diagnostics, not licensing credits.
        // Normalize older projects too; retain all real provider/license attributions.
        var credits = string.Join("\n", script.Scenes.Select(s => s.UserLocal&&string.IsNullOrWhiteSpace(s.LicenseUrl)&&s.Attribution.StartsWith("Yüklenen dosya • AI metin eşleşmesi:",StringComparison.Ordinal)?"Kullanıcı tarafından eklenen görsel":s.Attribution).Concat(script.MusicTrack?.Enabled == true ? new[] { script.MusicTrack.Attribution } : Array.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct());

        var effectCredits=(script.SoundEffects??[]).Where(x=>x.Enabled&&!string.IsNullOrWhiteSpace(x.Attribution)).Select(x=>x.Attribution);
        credits=string.Join("\n",new[]{credits}.Concat(effectCredits).Where(x=>x!="").Distinct());
        if (credits.Length > 4400) throw new InvalidOperationException("Zorunlu kaynak yazıları açıklama sınırına sığmıyor. Kaynakları düzenlemeden yükleme yapılamaz.");

        var optional = script.Description.Trim();

        var research = includeSources ? string.Join("\n", sources.Concat(script.Scenes.SelectMany(s=>s.SourceLinks.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries).Select(url=>new SourceRef{Url=url}))).Where(s=>Uri.TryCreate(s.Url,UriKind.Absolute,out var uri) && uri.Scheme=="https").Select(s=>s.Url).Distinct().Where(url=>!optional.Contains(url)).Take(30)) : "";

        var suffix = "\n\nBu tür içeriklerin devamı için kanala abone olmayı, videoyu beğenmeyi ve düşüncelerini yorumlarda paylaşmayı unutma." + (credits == "" ? "" : "\n\n" + credits);

        if (suffix.Length > 4400) throw new InvalidOperationException("Zorunlu krediler ve kanal çağrısı açıklamaya sığmıyor.");

        var budget=4500-suffix.Length;

        if(research!="") { var section="\n\nAraştırma kaynakları:\n"; foreach(var url in research.Split('\n')) { if(section.Length+url.Length+1>Math.Min(1600,budget)) break; section+=url+"\n"; } if(section.EndsWith(":\n"))section=""; suffix=section+suffix; budget=4500-suffix.Length; }

        return optional[..Math.Min(optional.Length, budget)] + suffix;

    }

    public async Task Upload(Episode e, AssetVideo asset, VideoScript script, CancellationToken ct, Action<long,long>? progress = null)

    {

        if (asset.YouTubeId != "") return;

        var token = await Token(ct); var total = new FileInfo(asset.File).Length; long offset = 0; progress?.Invoke(0, total);

        if (asset.UploadSession != "")

        {

            if (!Uri.TryCreate(asset.UploadSession, UriKind.Absolute, out var u) || u.Scheme != "https" || u.Host != "www.googleapis.com") throw new InvalidOperationException("Yükleme oturumu güvenilir değil.");

            using var status = new HttpRequestMessage(HttpMethod.Put, asset.UploadSession); status.Headers.Authorization = new("Bearer", token); status.Content = new ByteArrayContent([]); status.Content.Headers.TryAddWithoutValidation("Content-Range", "bytes */" + total);

            using var res = await http.SendAsync(status, ct);

            if ((int)res.StatusCode == 308) { offset = UploadedOffset(res); progress?.Invoke(offset, total); }

            else if (res.IsSuccessStatusCode) { asset.YouTubeId = await ReadId(res, ct); progress?.Invoke(total, total); asset.UploadSession = ""; asset.YouTubeState = "Gizli"; asset.YouTubeCheckedAt = DateTimeOffset.UtcNow; await Save(ct); return; }

            else if (res.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone) { asset.UploadSession = ""; await Save(ct); }

            else throw new ProviderException("YouTube oturum sorgusu", (int)res.StatusCode);

        }

        if (asset.UploadSession == "")

        {

            var description = BuildDescription(script, e.Plan!.Sources,store.Settings.IncludeResearchSources);

            using var init = new HttpRequestMessage(HttpMethod.Post, "https://www.googleapis.com/upload/youtube/v3/videos?uploadType=resumable&part=snippet,status"); init.Headers.Authorization = new("Bearer", token); init.Headers.Add("X-Upload-Content-Type", "video/mp4"); init.Headers.Add("X-Upload-Content-Length", total.ToString());

            init.Content = new StringContent(JsonSerializer.Serialize(new { snippet = new { title = script.Title, description, tags = script.Tags.Take(12), categoryId = store.Settings.CategoryId, defaultLanguage = "tr", defaultAudioLanguage = "tr" }, status = new { privacyStatus = "private", selfDeclaredMadeForKids = e.MadeForKids, containsSyntheticMedia = e.DiscloseSynthetic } }), Encoding.UTF8, "application/json");

            using var res = await http.SendAsync(init, ct); if (!res.IsSuccessStatusCode) throw new ProviderException("YouTube yükleme başlatma", (int)res.StatusCode); asset.UploadSession = res.Headers.Location?.ToString() ?? throw new InvalidOperationException("YouTube yükleme adresi dönmedi."); await Save(ct);

        }

        await using var file = File.OpenRead(asset.File); const int chunkSize = 8 * 1024 * 1024; var buffer = new byte[chunkSize]; int recoveries=0;

        while (offset < total)

        {

            ct.ThrowIfCancellationRequested(); file.Position = offset; var count = await file.ReadAsync(buffer.AsMemory(0, (int)Math.Min(chunkSize, total - offset)), ct); if (count == 0) throw new IOException("Video dosyası yükleme sırasında değişti.");

            using var chunk = new HttpRequestMessage(HttpMethod.Put, asset.UploadSession); chunk.Headers.Authorization = new("Bearer", token); chunk.Content = new ByteArrayContent(buffer, 0, count); chunk.Content.Headers.ContentType = new("video/mp4"); chunk.Content.Headers.ContentRange = new ContentRangeHeaderValue(offset, offset + count - 1, total);

            HttpResponseMessage? transfer=null;

            try{transfer=await http.SendAsync(chunk,ct);}catch(Exception ex) when((ex is HttpRequestException or TaskCanceledException) && !ct.IsCancellationRequested){}

            if(transfer==null || (int)transfer.StatusCode is 429 or >=500) {

                transfer?.Dispose();if(++recoveries>3)throw new InvalidOperationException("YouTube bağlantısı / geçici sınır nedeniyle aktarım durdu. Oturum saklandı; yeniden yükle ile kaldığı yerden devam edebilirsin.");

                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2,recoveries-1)),ct);

                using var query=new HttpRequestMessage(HttpMethod.Put,asset.UploadSession);query.Headers.Authorization=new("Bearer",token);query.Content=new ByteArrayContent([]);query.Content.Headers.TryAddWithoutValidation("Content-Range","bytes */"+total);

                using var recovered=await http.SendAsync(query,ct);

                if((int)recovered.StatusCode==308){long next=UploadedOffset(recovered);if(next<offset || next>total)throw new InvalidOperationException("YouTube devam konumu geçersiz.");offset=next;progress?.Invoke(offset,total);continue;}

                if(recovered.IsSuccessStatusCode){asset.YouTubeId=await ReadId(recovered,ct);asset.UploadSession="";asset.YouTubeState="Gizli";asset.YouTubeCheckedAt=DateTimeOffset.UtcNow;progress?.Invoke(total,total);await Save(ct);return;}

                throw new ProviderException("YouTube aktarım kurtarma",(int)recovered.StatusCode);

            }

            using var res=transfer;

            recoveries=0;

            if ((int)res.StatusCode == 308) { var next = UploadedOffset(res); if (next <= offset || next > total) throw new InvalidOperationException("YouTube yükleme ilerlemesi doğrulanamadı."); offset = next; progress?.Invoke(offset, total); }

            else if (res.IsSuccessStatusCode) { asset.YouTubeId = await ReadId(res, ct); progress?.Invoke(total, total); asset.UploadSession = ""; asset.YouTubeState = "Gizli"; asset.YouTubeCheckedAt = DateTimeOffset.UtcNow; await Save(ct); return; }

            else throw new ProviderException("YouTube video aktarımı", (int)res.StatusCode);

        }

        throw new InvalidOperationException("YouTube yüklemeyi henüz doğrulamadı. Tekrar denediğinde oturum sorgulanacak.");

    }

    static long UploadedOffset(HttpResponseMessage res) => res.Headers.TryGetValues("Range", out var values) && long.TryParse(values.First().Split('-').Last(), out var last) ? last + 1 : 0;

    static async Task<string> ReadId(HttpResponseMessage res, CancellationToken ct) { using var d = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct)); return d.RootElement.GetProperty("id").GetString()!; }

    public async Task Thumbnail(AssetVideo asset, CancellationToken ct)

    {

        using var req = new HttpRequestMessage(HttpMethod.Post, "https://www.googleapis.com/upload/youtube/v3/thumbnails/set?videoId=" + Uri.EscapeDataString(asset.YouTubeId)); req.Headers.Authorization = new("Bearer", await Token(ct)); req.Content = new ByteArrayContent(await File.ReadAllBytesAsync(asset.Thumbnail, ct)); req.Content.Headers.ContentType = new("image/png");

        using var res = await http.SendAsync(req, ct); if (!res.IsSuccessStatusCode) throw new ProviderException("YouTube küçük resim", (int)res.StatusCode); asset.ThumbnailUploaded = true; await Save(ct);

    }

    public async Task UpdateDescription(AssetVideo asset, VideoScript script, CancellationToken ct)

    {

        if (asset.YouTubeId == "") throw new InvalidOperationException("Video henüz YouTube'a yüklenmedi.");

        var token = await Token(ct);

        using var get = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/youtube/v3/videos?part=snippet&id=" + Uri.EscapeDataString(asset.YouTubeId)); get.Headers.Authorization = new("Bearer", token);

        using var current = await http.SendAsync(get, ct); if (!current.IsSuccessStatusCode) throw new ProviderException("YouTube video bilgisi", (int)current.StatusCode);

        using var doc = JsonDocument.Parse(await current.Content.ReadAsStringAsync(ct)); var items = doc.RootElement.GetProperty("items"); if (items.GetArrayLength() != 1) throw new InvalidOperationException("Yüklenen video bulunamadı.");

        var original = items[0].GetProperty("snippet"); var snippet = new Dictionary<string, object?> { ["title"] = original.GetProperty("title").GetString(), ["categoryId"] = original.GetProperty("categoryId").GetString(), ["description"] = BuildDescription(script, []) };

        foreach (var name in new[] { "tags", "defaultLanguage", "defaultAudioLanguage" }) if (original.TryGetProperty(name, out var value)) snippet[name] = value.Clone();

        using var put = new HttpRequestMessage(HttpMethod.Put, "https://www.googleapis.com/youtube/v3/videos?part=snippet"); put.Headers.Authorization = new("Bearer", token); put.Content = new StringContent(JsonSerializer.Serialize(new { id = asset.YouTubeId, snippet }), Encoding.UTF8, "application/json");

        using var response = await http.SendAsync(put, ct); if (!response.IsSuccessStatusCode) throw new ProviderException("YouTube açıklama güncelleme", (int)response.StatusCode);

    }

    async Task UpdateStatus(AssetVideo asset, bool schedule, CancellationToken ct, bool publishNow = false)

    {

        if (asset.YouTubeId == "") throw new InvalidOperationException("Video henüz YouTube’a yüklenmedi.");

        var token = await Token(ct);

        using var get = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/youtube/v3/videos?part=status&id=" + Uri.EscapeDataString(asset.YouTubeId)); get.Headers.Authorization = new("Bearer", token);

        using var current = await http.SendAsync(get, ct); if (!current.IsSuccessStatusCode) throw new ProviderException("YouTube mevcut yayın ayarları", (int)current.StatusCode);

        using var doc = JsonDocument.Parse(await current.Content.ReadAsStringAsync(ct)); var items = doc.RootElement.GetProperty("items"); if (items.GetArrayLength() != 1) throw new InvalidOperationException("Video bulunamadı; yayın ayarları değiştirilmedi.");

        var original = items[0].GetProperty("status"); var status = new Dictionary<string, object?>();

        foreach (var name in new[] { "license", "embeddable", "publicStatsViewable", "selfDeclaredMadeForKids", "containsSyntheticMedia" }) if (original.TryGetProperty(name, out var value)) status[name] = value.Clone();

        status["privacyStatus"] = publishNow ? "public" : "private";

        if (schedule) status["publishAt"] = asset.PublishAt!.Value.ToUniversalTime().ToString("o");

        using var put = new HttpRequestMessage(HttpMethod.Put, "https://www.googleapis.com/youtube/v3/videos?part=status"); put.Headers.Authorization = new("Bearer", token); put.Content = new StringContent(JsonSerializer.Serialize(new { id = asset.YouTubeId, status }), Encoding.UTF8, "application/json");

        using var response = await http.SendAsync(put, ct); if (!response.IsSuccessStatusCode) throw new ProviderException("YouTube yayın ayarı", (int)response.StatusCode);

        if (publishNow) {

            using var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));

            if (!result.RootElement.TryGetProperty("status", out var confirmed) || !confirmed.TryGetProperty("privacyStatus", out var privacy) || privacy.GetString() != "public") throw new InvalidOperationException("YouTube videonun herkese açık olduğunu doğrulamadı. Durumu yenile; API proje kısıtlamasını kontrol et.");

        }

        asset.Scheduled = schedule; asset.RemotePublishAt = schedule ? asset.PublishAt : null; asset.YouTubeState = publishNow ? "Yayında" : schedule ? "Planlı bekliyor" : "Gizli"; asset.YouTubeCheckedAt = DateTimeOffset.UtcNow; await Save(ct);

    }

    public async Task RefreshVideoStates(CancellationToken ct)

    {

        var videos = store.Episodes.SelectMany(e => e.Videos).Where(v => v.YouTubeId != "").ToList();

        if (videos.Count == 0) return;

        var token = await Token(ct);

        var updates = new List<(AssetVideo Video, string State, bool Scheduled, DateTimeOffset? Date)>();

        foreach (var batch in videos.Chunk(50))

        {

            using var req = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/youtube/v3/videos?part=status,processingDetails&id=" + Uri.EscapeDataString(string.Join(',', batch.Select(v => v.YouTubeId).Distinct())));

            req.Headers.Authorization = new("Bearer", token);

            using var res = await http.SendAsync(req, ct); if (!res.IsSuccessStatusCode) throw new ProviderException("YouTube durum senkronizasyonu", (int)res.StatusCode);

            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));

            var items = doc.RootElement.GetProperty("items").EnumerateArray().ToDictionary(x => x.GetProperty("id").GetString()!);

            foreach (var video in batch)

            {

                if (!items.TryGetValue(video.YouTubeId, out var item)) { updates.Add((video, "Bulunamadı / erişilemiyor", false, null)); continue; }

                var status = item.GetProperty("status"); var privacy = status.GetProperty("privacyStatus").GetString();

                DateTimeOffset? date = status.TryGetProperty("publishAt", out var publish) && DateTimeOffset.TryParse(publish.GetString(), out var parsed) ? parsed : null;

                var state = privacy == "public" ? "Yayında" : date != null ? "Planlı bekliyor" : privacy == "unlisted" ? "Liste dışı" : "Gizli";

                if (status.TryGetProperty("uploadStatus", out var upload) && upload.GetString() is "failed" or "rejected" or "deleted") state = "Sorun: " + upload.GetString();

                else if (item.TryGetProperty("processingDetails", out var processing) && processing.TryGetProperty("processingStatus", out var value) && value.GetString() is "processing" or "failed") state = value.GetString() == "failed" ? "Sorun: işlenemedi" : "İşleniyor";

                updates.Add((video, state, date != null && privacy == "private", date));

            }

        }

        foreach (var update in updates) { update.Video.YouTubeState = update.State; update.Video.YouTubeCheckedAt = DateTimeOffset.UtcNow; update.Video.Scheduled = update.Scheduled; update.Video.RemotePublishAt = update.Date; }

        await Save(ct);

    }

    public async Task Schedule(AssetVideo asset, CancellationToken ct)

    {

        if (asset.PublishAt == null || asset.PublishAt < DateTimeOffset.UtcNow.AddMinutes(10)) throw new InvalidOperationException("Yayın tarihi en az 10 dakika ileride olmalı.");

        await UpdateStatus(asset, true, ct);

    }

    public Task PublishNow(AssetVideo asset, CancellationToken ct) => UpdateStatus(asset, false, ct, publishNow: true);

    public async Task Unschedule(AssetVideo asset, CancellationToken ct)

    {

        await UpdateStatus(asset, false, ct);

    }

}

