using System.IO.Compression;

using System.Net.Http.Headers;

using System.Text.Json;

using System.Text.RegularExpressions;



namespace AnkaStudio;



public sealed class Vecteezy(Store store, HttpClient? client = null)

{

    readonly HttpClient http = client ?? new() { Timeout = TimeSpan.FromMinutes(4) };

    static readonly SemaphoreSlim Gate = new(1, 1);

    public bool Ready => store.Settings.VecteezyEnabled && store.Key("VECTEEZY_ACCOUNT_ID") != "" && store.Key("VECTEEZY_SECRET_KEY") != "";

    string Account => Regex.IsMatch(store.Key("VECTEEZY_ACCOUNT_ID").Trim(), @"^[0-9]{1,20}$") ? store.Key("VECTEEZY_ACCOUNT_ID").Trim() : throw new InvalidOperationException("Vecteezy Account Id yalnızca rakamlardan oluşmalı.");

    string Key => store.Key("VECTEEZY_SECRET_KEY").Trim() is { Length: > 0 } key ? key : throw new InvalidOperationException("Vecteezy Secret key eksik.");

    async Task<JsonDocument> Get(string path, CancellationToken ct)

    {

        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.vecteezy.com/v2/" + Account + "/" + path);

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Key);

        using var response = await http.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode) throw new ProviderException("Vecteezy", (int)response.StatusCode, (int)response.StatusCode == 402 ? "Hesap kotası doldu; ücretli aşım denenmedi." : "Hesap ve API izinlerini kontrol et.");

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));

    }

    public record Quota(long GeneralUsed, long GeneralLimit, long DownloadUsed, long DownloadLimit);

    public async Task<Quota> AccountInfo(CancellationToken ct)

    {

        using var doc = await Get("account/info", ct); var current = doc.RootElement.GetProperty("current");

        var general = current.GetProperty("general"); var download = current.GetProperty("download");

        return new(general.GetProperty("call_count").GetInt64(), general.GetProperty("call_limit").GetInt64(), download.GetProperty("call_count").GetInt64(), download.GetProperty("call_limit").GetInt64());

    }

    void Check(Quota quota, bool download)

    {

        var used = download ? quota.DownloadUsed : quota.GeneralUsed; var limit = download ? quota.DownloadLimit : quota.GeneralLimit;

        if (limit <= 0 || used >= limit) throw new ProviderException("Vecteezy", 402, (download ? "İndirme" : "Arama") + " kotası yok veya dolu; aşım isteği gönderilmedi.");

        if (download && quota.DownloadUsed >= store.Settings.VecteezyMonthlyDownloads) throw new ProviderException("Vecteezy", 402, "Programdaki aylık indirme sınırına ulaşıldı.");

    }

    public async Task Stock(Scene scene, bool vertical, string folder, HashSet<long> used, CancellationToken ct)

    {

        await Gate.WaitAsync(ct);

        try {

            Check(await AccountInfo(ct), false);

            using var search = await Get("resources?content_type=video&license_type=commercial&family_friendly=true&ai_generated=false&per_page=12&sort_by=relevance&term=" + Uri.EscapeDataString(scene.VisualQuery), ct);

            var candidates = search.RootElement.GetProperty("resources").EnumerateArray().Where(x => x.GetProperty("content_type").GetString() == "video").ToArray();

            candidates = candidates.OrderByDescending(x => x.TryGetProperty("dimensions", out var dimensions) && (dimensions.TryGetProperty("w", out var w) || dimensions.TryGetProperty("width", out w)) && (dimensions.TryGetProperty("h", out var h) || dimensions.TryGetProperty("height", out h)) && (vertical ? h.GetInt32() > w.GetInt32() : w.GetInt32() >= h.GetInt32())).ToArray();

            foreach (var resource in candidates)

            {

                ct.ThrowIfCancellationRequested(); if (!StockSearch.PossibleCandidate(scene.VisualQuery, StockSearch.Metadata(resource), StockVisualReview.CanReviewFrames(store))) continue; scene.StockMetadata = StockSearch.Metadata(resource); var id = resource.GetProperty("id").GetInt64(); var token = ResourceToken(id); if (used.Contains(token) || StockSearch.Blocked(store.Settings,"https://www.vecteezy.com/video/"+id)) continue;

                string extension = "";

                if (resource.TryGetProperty("file_metadata", out var metadata) && metadata.TryGetProperty("available_file_types", out var types))

                    extension = types.EnumerateArray().Select(x => x.GetProperty("extension").GetString()?.TrimStart('.').ToLowerInvariant() ?? "").Where(x => x is "mp4" or "mov" or "zip").OrderBy(x => x == "mp4" ? 0 : x == "mov" ? 1 : 2).FirstOrDefault() ?? "";

                if (extension == "") continue;

                var path = Path.Combine(folder, "vecteezy-" + id + "." + (extension == "zip" ? "mp4" : extension));

                var creditFile = path + ".credit.json";

                if (File.Exists(path) && File.Exists(creditFile)) {

                    if (!await StockVisualReview.Accept(store, scene.VisualQuery, path, http, _ => {}, ct)) continue; var cached = Json.Read(creditFile, new Credit()); if(StockSearch.Blocked(store.Settings,cached.Source))continue; Assign(scene, path, cached, id); used.Add(token); return;

                }

                Check(await AccountInfo(ct), true);

                using var download = await Get("resources/" + id + "/download?file_type=" + extension, ct);

                if (!download.RootElement.TryGetProperty("url", out var urlElement) || urlElement.GetString() is not { Length: > 0 } url) throw new InvalidOperationException("Vecteezy indirme adresi dönmedi; ücretsiz hesapta yeniden boyutlandırma kullanılmaz.");

                var uri = Https(url);

                var attributionUrl = download.RootElement.TryGetProperty("required_attribution_url", out var attribution) ? attribution.GetString() ?? "" : "";

                var source = "https://www.vecteezy.com/video/" + id;

                if (Uri.TryCreate(attributionUrl, UriKind.Absolute, out var attributionUri) && attributionUri.Scheme == "https" && (attributionUri.Host == "vecteezy.com" || attributionUri.Host.EndsWith(".vecteezy.com")) && attributionUri.Query == "") source = attributionUri.AbsoluteUri;

                if(StockSearch.Blocked(store.Settings,source))continue;

                var credit = new Credit { Source = source, Text = "Free Stock Footage by Vecteezy.com — " + source };

                if (download.RootElement.TryGetProperty("requires_attribution", out var required) && required.ValueKind == JsonValueKind.False) credit.Text = "Stock Footage by Vecteezy.com — " + source;

                await DownloadFile(uri, path, extension == "zip", ct); Json.Write(creditFile, credit);

                if (!await StockVisualReview.Accept(store, scene.VisualQuery, path, http, _ => {}, ct)) continue;

                Assign(scene, path, credit, id); used.Add(token); return;

            }

            throw new InvalidOperationException("Vecteezy hesabında kullanılabilir, tekrarsız video bulunamadı.");

        } finally { Gate.Release(); }

    }

    public static long ResourceToken(long id) => -3000000000000000000L - Math.Abs(id % 1000000000000000000L);

    static Uri Https(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "https" && uri.UserInfo == "" && !uri.IsLoopback ? uri : throw new InvalidOperationException("Vecteezy güvenli indirme adresi vermedi.");

    public sealed class Credit { public string Source { get; set; } = ""; public string Text { get; set; } = ""; }

    static void Assign(Scene scene, string path, Credit credit, long id) { scene.StockFile = path; scene.StockUrl = credit.Source; scene.Creator = "Vecteezy"; scene.LicenseUrl = "https://www.vecteezy.com/licensing-agreement"; scene.Attribution = credit.Text; }

    static async Task CopyLimited(Stream input, Stream output, CancellationToken ct)

    {

        var buffer = new byte[1048576]; long total = 0; int count;

        while ((count = await input.ReadAsync(buffer, ct)) > 0) { total += count; if (total > 512L * 1024 * 1024) throw new InvalidOperationException("Vecteezy dosyası 512 MB sınırını aşıyor."); await output.WriteAsync(buffer.AsMemory(0, count), ct); }

    }

    async Task DownloadFile(Uri uri, string path, bool zip, CancellationToken ct)

    {

        const long maxSize = 512L * 1024 * 1024; var temp = path + ".part";

        try {

            using var request = new HttpRequestMessage(HttpMethod.Get, uri); // Secret key is sent only to api.vecteezy.com, never to a CDN.

            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct); response.EnsureSuccessStatusCode();

            if (response.Content.Headers.ContentLength > maxSize) throw new InvalidOperationException("Vecteezy dosyası 512 MB sınırını aşıyor.");

            await using (var input = await response.Content.ReadAsStreamAsync(ct)) await using (var output = File.Create(temp)) {

                await CopyLimited(input, output, ct);

            }

            if (zip) {

                using (var archive = ZipFile.OpenRead(temp)) {

                    var video = archive.Entries.Where(x => Path.GetExtension(x.Name).ToLowerInvariant() is ".mp4" or ".mov").OrderBy(x => Path.GetExtension(x.Name).Equals(".mp4", StringComparison.OrdinalIgnoreCase) ? 0 : 1).FirstOrDefault() ?? throw new InvalidOperationException("Vecteezy ZIP içinde video yok.");

                    if (video.Length > maxSize) throw new InvalidOperationException("Vecteezy ZIP videosu 512 MB sınırını aşıyor.");

                    var extracted = path + ".extract";

                    try { await using var input = video.Open(); await using (var output = File.Create(extracted)) await CopyLimited(input, output, ct); File.Move(extracted, path, true); }

                    finally { if (File.Exists(extracted)) File.Delete(extracted); }

                }

                File.Delete(temp);

            } else File.Move(temp, path, true);

        } finally { if (File.Exists(temp)) File.Delete(temp); }

    }

}

