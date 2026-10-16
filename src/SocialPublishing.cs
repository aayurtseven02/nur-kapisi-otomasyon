using System.Net.Http.Headers;

using System.Net.Http.Json;

using System.Security.Cryptography;

using System.Text.Json;

using System.Text.RegularExpressions;



namespace AnkaStudio;



public sealed class SocialAccount

{

    public string Platform { get; set; } = "";

    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public string CredentialId { get; set; } = Guid.NewGuid().ToString("N");

    public DateTimeOffset? ExpiresAt { get; set; }

    public override string ToString() => Platform + " â€¢ " + Name;

}



public sealed class SocialPost

{

    public string Platform { get; set; } = "";

    public string AccountId { get; set; } = "";

    public string AccountName { get; set; } = "";

    public string FileHash { get; set; } = "";

    public string RemoteId { get; set; } = "";

    public string State { get; set; } = "";

    public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;

    public string Error { get; set; } = "";

}



public sealed class TikTokCreator

{

    public string Name { get; set; } = "";

    public string[] Privacy { get; set; } = [];

    public double MaxSeconds { get; set; }

    public bool CommentDisabled { get; set; }

    public bool DuetDisabled { get; set; }

    public bool StitchDisabled { get; set; }

}



public sealed class SocialPublisher(Store store, HttpClient? client = null)

{

    static readonly HttpClient Shared = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromMinutes(10) };

    readonly HttpClient http = client ?? Shared;

    string Graph => Regex.IsMatch(store.Settings.SocialGraphVersion, @"^v\d+\.0$") ? store.Settings.SocialGraphVersion : throw new InvalidOperationException("Geçersiz Meta API sürümü.");

    static string Id(string value) => Regex.IsMatch(value, "^[A-Za-z0-9_-]{1,200}$") ? value : throw new InvalidOperationException("Geçersiz hesap / paylaşım kimliği.");

    public static string TokenKey(SocialAccount a) => "SOCIAL_TOKEN_" + a.CredentialId;

    public static string RefreshKey(SocialAccount a) => "SOCIAL_REFRESH_" + a.CredentialId;

    public static Uri Https(string url)

    {

        if (!Uri.TryCreate(url, UriKind.Absolute, out var u) || u.Scheme != "https" || u.UserInfo != "" || u.IsLoopback || System.Net.IPAddress.TryParse(u.Host, out _)) throw new InvalidOperationException("Herkese erişilebilir HTTPS bağlantısı gerekli.");

        return u;

    }

    static Uri UploadUri(string url, string platform)

    {

        var u = Https(url);

        bool valid = platform == "TikTok" ? u.Host.EndsWith(".tiktokapis.com", StringComparison.OrdinalIgnoreCase) : u.Host == "rupload.facebook.com";

        if (!valid) throw new InvalidOperationException("Platform beklenmeyen bir yükleme adresi döndürdü.");

        return u;

    }

    public async Task<string> Token(SocialAccount a, CancellationToken ct)

    {

        var token = store.Vault.Get(TokenKey(a));

        if (token == "") throw new InvalidOperationException(a.Platform + " hesabını yeniden bağla.");

        if (a.ExpiresAt <= DateTimeOffset.UtcNow.AddMinutes(3)) {

            if (store.Vault.Get(RefreshKey(a)) == "") throw new InvalidOperationException(a.Platform + " bağlantı izni sona erdi; hesabını yeniden bağla.");

            var broker = Https(store.Settings.SocialBrokerUrl.TrimEnd('/') + "/refresh");

            using var req = new HttpRequestMessage(HttpMethod.Post, broker) { Content = JsonContent.Create(new { platform = a.Platform, refresh_token = store.Vault.Get(RefreshKey(a)) }) };

            var data = await Send(req, ct);

            token = data.GetProperty("access_token").GetString() ?? "";

            if (token == "") throw new InvalidOperationException("Bağlantı yenilenemedi.");

            store.Vault.Set(TokenKey(a), token);

            if (data.TryGetProperty("refresh_token", out var refresh) && !string.IsNullOrEmpty(refresh.GetString())) store.Vault.Set(RefreshKey(a), refresh.GetString()!);

            a.ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(data.GetProperty("expires_in").GetInt32()); store.Save();

        }

        return token;

    }

    async Task<JsonElement> Send(HttpRequestMessage req, CancellationToken ct)

    {

        using var response = await http.SendAsync(req, ct);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));

        var root = doc.RootElement;

        if (!response.IsSuccessStatusCode || root.TryGetProperty("error", out var e) && (e.ValueKind != JsonValueKind.Object || e.TryGetProperty("code", out var code) && code.ToString() != "ok")) {

            // Do not put upstream bodies or authorization URLs into logs: they can contain credentials.

            throw new InvalidOperationException("Sosyal paylaşım servisi isteği reddetti (HTTP " + (int)response.StatusCode + "). Hesap izinlerini ve uygulama incelemesini (i) rehberinden kontrol et.");

        }

        return root.Clone();

    }

    async Task<JsonElement> GraphCall(string path, string token, CancellationToken ct, Dictionary<string,string>? values = null, bool instagram = false)

    {

        using var req = new HttpRequestMessage(values == null ? HttpMethod.Get : HttpMethod.Post, (instagram ? "https://graph.instagram.com/" : "https://graph.facebook.com/") + Graph + "/" + path);

        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (values != null) req.Content = new FormUrlEncodedContent(values);

        return await Send(req, ct);

    }

    Task<JsonElement> IgCall(string path, string token, CancellationToken ct, Dictionary<string,string>? values = null) => GraphCall(path, token, ct, values, true);

    async Task<JsonElement> TikCall(string path, string token, object payload, CancellationToken ct)

    {

        using var req = new HttpRequestMessage(HttpMethod.Post, "https://open.tiktokapis.com/v2/" + path) { Content = JsonContent.Create(payload) };

        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var root = await Send(req, ct); return root.GetProperty("data").Clone();

    }

    public async Task<TikTokCreator> Creator(SocialAccount a, CancellationToken ct)

    {

        var data = await TikCall("post/publish/creator_info/query/", await Token(a, ct), new {}, ct);

        return new() { Name = data.GetProperty("creator_nickname").GetString() ?? a.Name, Privacy = data.GetProperty("privacy_level_options").EnumerateArray().Select(x => x.GetString()!).ToArray(), MaxSeconds = data.GetProperty("max_video_post_duration_sec").GetDouble(), CommentDisabled = data.GetProperty("comment_disabled").GetBoolean(), DuetDisabled = data.GetProperty("duet_disabled").GetBoolean(), StitchDisabled = data.GetProperty("stitch_disabled").GetBoolean() };

    }

    public static (long Size, int Count) Chunks(long length)

    {

        if (length <= 0 || length > 4L * 1024 * 1024 * 1024) throw new InvalidOperationException("Video boyutu geçersiz; TikTok en fazla 4 GB kabul eder.");

        long size = Math.Min(length, 10L * 1024 * 1024); return (size, Math.Max(1, (int)(length / size)));

    }

    public async Task<SocialPost> Publish(Episode episode, SocialAccount a, string file, double seconds, string caption, string privacy, bool comments, bool duet, bool stitch, bool ai, bool consent, string[] photoUrls, CancellationToken ct, bool ownBrand = false, bool paidBrand = false, bool reel = true)

    {

        if (!consent) throw new InvalidOperationException("Önizleme ve paylaşım onayı gerekli.");

        if (a.Platform is not ("Instagram" or "TikTok" or "Facebook")) throw new InvalidOperationException("Desteklenmeyen platform.");

        bool photo = photoUrls.Length > 0;

        if (a.Platform == "Facebook" && photoUrls.Length > 1) throw new InvalidOperationException("Facebook için tek fotoğraf seç.");

        if (photo) foreach (var url in photoUrls) Https(url);

        else if (!File.Exists(file)) throw new InvalidOperationException("Video dosyası bulunamadı.");

        if (caption.Length > (a.Platform == "Instagram" ? 2200 : 4000)) throw new InvalidOperationException("Açıklama çok uzun.");

        var hash = photo ? Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(string.Join("\n", photoUrls)))) : Reliability.FileHash(file);

        if (episode.SocialPosts.Any(x => x.Platform == a.Platform && x.AccountId == a.Id && x.FileHash == hash && x.State != "FAILED")) throw new InvalidOperationException("Bu içerik için zaten bir paylaşım kaydı var. Yeniden göndermek yerine durumunu kontrol et.");

        var token = await Token(a, ct);

        TikTokCreator? creator = null;

        if (a.Platform == "TikTok") {

            creator = await Creator(a, ct);

            if (paidBrand && privacy == "SELF_ONLY") throw new InvalidOperationException("Ücretli marka içeriği özel görünürlükte paylaşılamaz.");

            if (!creator.Privacy.Contains(privacy)) throw new InvalidOperationException("TikTok görünürlüğünü hesap için sunulan listeden seç.");

            if (!photo && seconds > creator.MaxSeconds) throw new InvalidOperationException("Video bu TikTok hesabının süre sınırını aşıyor.");

            if (photoUrls.Length > 35) throw new InvalidOperationException("TikTok en fazla 35 fotoğraf kabul eder.");

        }

        if (a.Platform == "Instagram" && photoUrls.Length > 10) throw new InvalidOperationException("Bu sürümde Instagram için en fazla 10 fotoğraf seç.");

        var record = new SocialPost { Platform = a.Platform, AccountId = a.Id, AccountName = a.Name, FileHash = hash, State = "STARTING" };

        episode.SocialPosts.Add(record); store.Save();

        try {

            if (a.Platform == "TikTok") {

                var info = new { title = caption, privacy_level = privacy, disable_comment = !comments || creator!.CommentDisabled, disable_duet = photo || !duet || creator!.DuetDisabled, disable_stitch = photo || !stitch || creator!.StitchDisabled, is_aigc = ai, brand_organic_toggle = ownBrand, brand_content_toggle = paidBrand };

                JsonElement start;

                if (photo) start = await TikCall("post/publish/content/init/", token, new { post_info = new { title = caption[..Math.Min(90, caption.Length)], description = caption, privacy_level = privacy, disable_comment = !comments || creator!.CommentDisabled, is_aigc = ai, brand_organic_toggle = ownBrand, brand_content_toggle = paidBrand }, source_info = new { source = "PULL_FROM_URL", photo_images = photoUrls, photo_cover_index = 0 }, post_mode = "DIRECT_POST", media_type = "PHOTO" }, ct);

                else {

                    var length = new FileInfo(file).Length; var chunk = Chunks(length);

                    start = await TikCall("post/publish/video/init/", token, new { post_info = info, source_info = new { source = "FILE_UPLOAD", video_size = length, chunk_size = chunk.Size, total_chunk_count = chunk.Count } }, ct);

                    record.RemoteId = start.GetProperty("publish_id").GetString()!; record.State = "UPLOADING"; store.Save();

                    var url = UploadUri(start.GetProperty("upload_url").GetString()!, a.Platform);

                    await using var stream = File.OpenRead(file);

                    for (int i = 0; i < chunk.Count; i++) {

                        long offset = i * chunk.Size; int count = checked((int)(i == chunk.Count - 1 ? length - offset : chunk.Size));

                        var bytes = new byte[count]; await stream.ReadExactlyAsync(bytes, ct);

                        using var req = new HttpRequestMessage(HttpMethod.Put, url) { Content = new ByteArrayContent(bytes) };

                        req.Content.Headers.ContentType = new MediaTypeHeaderValue("video/mp4"); req.Content.Headers.ContentRange = new ContentRangeHeaderValue(offset, offset + count - 1, length);

                        using var response = await http.SendAsync(req, ct); if (!response.IsSuccessStatusCode) throw new InvalidOperationException("TikTok video aktarımı kesildi. Durumu kontrol et; otomatik olarak ikinci paylaşım oluşturulmadı.");

                    }

                }

                record.RemoteId = start.GetProperty("publish_id").GetString()!; record.State = "PROCESSING";

            } else if (a.Platform == "Instagram") {

                var values = new Dictionary<string,string> { ["caption"] = caption };

                if (photo && photoUrls.Length == 1) values["image_url"] = photoUrls[0];

                else if (photo) {

                    var children = new List<string>();

                    foreach (var url in photoUrls) { var child = await IgCall(Id(a.Id) + "/media", token, ct, new() { ["image_url"] = url, ["is_carousel_item"] = "true" }); children.Add(child.GetProperty("id").GetString()!); }

                    values["media_type"] = "CAROUSEL"; values["children"] = string.Join(',', children);

                } else { values["media_type"] = "REELS"; values["upload_type"] = "resumable"; }

                var start = await IgCall(Id(a.Id) + "/media", token, ct, values);

                record.RemoteId = start.GetProperty("id").GetString()!; record.State = "PROCESSING"; store.Save();

                if (!photo) await UploadMeta(UploadUri(start.GetProperty("uri").GetString()!, a.Platform), token, file, ct);

                // Publish only after Meta confirms that its container is ready. Status checks can be resumed.

            } else {

                if (photo) {

                    if (photoUrls.Length != 1) throw new InvalidOperationException("Facebook fotoğraf paylaşımında bu sürümde tek fotoğraf seç.");

                    var posted = await GraphCall(Id(a.Id) + "/photos", token, ct, new() { ["url"] = photoUrls[0], ["caption"] = caption });

                    record.RemoteId = posted.TryGetProperty("post_id", out var postId) ? postId.GetString()! : posted.GetProperty("id").GetString()!; record.State = "PUBLISHED";

                } else {

                    if (!reel) {
                        using var content = new MultipartFormDataContent();
                        var source = new StreamContent(File.OpenRead(file)); source.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
                        content.Add(source, "source", Path.GetFileName(file)); content.Add(new StringContent(caption), "description"); content.Add(new StringContent("true"), "published");
                        using var req = new HttpRequestMessage(HttpMethod.Post, "https://graph-video.facebook.com/" + Graph + "/" + Id(a.Id) + "/videos") { Content = content };
                        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                        var posted = await Send(req, ct); record.RemoteId = posted.GetProperty("id").GetString()!; record.State = "PROCESSING";
                        store.Save(); return record;
                    }
                    var start = await GraphCall(Id(a.Id) + "/video_reels", token, ct, new() { ["upload_phase"] = "start" });

                    record.RemoteId = start.GetProperty("video_id").GetString()!; record.State = "UPLOADING"; store.Save();

                    await UploadMeta(UploadUri(start.GetProperty("upload_url").GetString()!, a.Platform), token, file, ct);

                    var finish = await GraphCall(Id(a.Id) + "/video_reels", token, ct, new() { ["upload_phase"] = "finish", ["video_id"] = record.RemoteId, ["video_state"] = "PUBLISHED", ["description"] = caption });

                    if (!finish.GetProperty("success").GetBoolean()) throw new InvalidOperationException("Facebook yayını kabul etmedi.");

                    record.State = "PROCESSING";

                }

            }

            store.Save(); return record;

        } catch { record.State = record.RemoteId == "" ? "UNCERTAIN" : "CHECK_REQUIRED"; record.Error = "İşlem kesildi. Yeniden gönderimden önce platformdaki durumu kontrol et."; store.Save(); throw; }

    }

    async Task UploadMeta(Uri url, string token, string file, CancellationToken ct)

    {

        using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = new StreamContent(File.OpenRead(file)) };

        req.Headers.Authorization = new AuthenticationHeaderValue("OAuth", token); req.Headers.Add("offset", "0"); req.Headers.Add("file_size", new FileInfo(file).Length.ToString()); req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        await Send(req, ct);

    }

    public async Task Check(SocialAccount a, SocialPost post, CancellationToken ct)

    {

        if (post.AccountId != a.Id || post.Platform != a.Platform || post.RemoteId == "") throw new InvalidOperationException("Bu paylaşım için bağlı hesap / işlem kimliği bulunamadı.");

        var token = await Token(a, ct);

        if (a.Platform == "TikTok") {

            var data = await TikCall("post/publish/status/fetch/", token, new { publish_id = post.RemoteId }, ct);

            post.State = data.GetProperty("status").GetString() switch { "PUBLISH_COMPLETE" => "PUBLISHED", "FAILED" => "FAILED", _ => "PROCESSING" };

        } else if (a.Platform == "Instagram") {

            if (post.State == "PUBLISHED") return;

            var data = await IgCall(Id(post.RemoteId) + "?fields=status_code", token, ct);

            var state = data.GetProperty("status_code").GetString();

            if (state == "PUBLISHED") post.State = "PUBLISHED";

            else if (state is "ERROR" or "EXPIRED") post.State = "FAILED";

            else if (state == "FINISHED") {

                post.State = "PUBLISHING"; store.Save();

                // A lost publish response must not cause a duplicate: the container's status is checked first next time.

                await IgCall(Id(a.Id) + "/media_publish", token, ct, new() { ["creation_id"] = post.RemoteId }); post.State = "PUBLISHED";

            } else post.State = "PROCESSING";

        } else if (post.State != "PUBLISHED") {

            var data = await GraphCall(Id(post.RemoteId) + "?fields=status", token, ct);

            if (data.TryGetProperty("status", out var status) && status.TryGetProperty("publishing_phase", out var phase) && phase.TryGetProperty("status", out var value) && value.GetString() == "complete") post.State = "PUBLISHED";

        }

        post.Error = ""; store.Save();

    }

}

