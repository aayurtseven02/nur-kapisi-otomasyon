using System.Text;

using System.Text.Json;

using System.Net.Http.Json;

using System.Security.Cryptography;

using System.Text.RegularExpressions;

using System.Globalization;



namespace AnkaStudio;



public sealed class CloudUsage(Store store, HttpClient? client = null)

{

    static readonly HttpClient SharedHttp = new() { Timeout = TimeSpan.FromSeconds(60) };

    readonly HttpClient http = client ?? SharedHttp;

    public sealed class Row { public string Sku { get; set; } = ""; public string Unit { get; set; } = ""; public double Amount { get; set; } public double Cost { get; set; } public string Currency { get; set; } = ""; }

    public sealed class Report { public string Month { get; set; } = ""; public DateTimeOffset Fetched { get; set; } public List<Row> Rows { get; set; } = []; }

    string FilePath => Path.Combine(store.Root, "cloud-tts-report.json");

    public Report Read() => Json.Read(FilePath, new Report());

    static string B64(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static void ValidateTable(string value) { if (!Regex.IsMatch(value, @"^[a-z][a-z0-9-]{4,62}\.[A-Za-z_][A-Za-z0-9_]*\.[A-Za-z_][A-Za-z0-9_]*$")) throw new InvalidOperationException("BigQuery tablo adı project.dataset.table biçiminde olmalı."); }

    async Task<(string Project, string Access)> Token(string keyName, string scope, CancellationToken ct)

    {

        var credential = store.Key(keyName); if (credential == "") throw new InvalidOperationException("Salt okunur Google Cloud hizmet hesabı JSON dosyasını içe aktar.");

        using var doc = JsonDocument.Parse(credential); var root = doc.RootElement;

        var project = root.GetProperty("project_id").GetString()!;

        if (!Regex.IsMatch(project, @"^[a-z][a-z0-9-]{4,62}$")) throw new InvalidOperationException("Geçersiz proje kimliği.");

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var head = B64(Encoding.UTF8.GetBytes("{\"alg\":\"RS256\",\"typ\":\"JWT\"}"));

        var body = B64(JsonSerializer.SerializeToUtf8Bytes(new { iss = root.GetProperty("client_email").GetString(), scope, aud = "https://oauth2.googleapis.com/token", iat = now, exp = now + 3600 }));

        using var rsa = RSA.Create(); rsa.ImportFromPem(root.GetProperty("private_key").GetString()); var unsigned = head + "." + body; var jwt = unsigned + "." + B64(rsa.SignData(Encoding.UTF8.GetBytes(unsigned), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));

        using var tokenRes = await http.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer", ["assertion"] = jwt }), ct);

        if (!tokenRes.IsSuccessStatusCode) throw new ProviderException("Cloud kullanım yetkilendirmesi", (int)tokenRes.StatusCode);

        using var token = JsonDocument.Parse(await tokenRes.Content.ReadAsStringAsync(ct));

        return (project, token.RootElement.GetProperty("access_token").GetString()!);

    }

    public async Task<Report> Refresh(CancellationToken ct)

    {

        ValidateTable(store.Settings.CloudBillingTable);

        var (project, access) = await Token("GOOGLE_BILLING_JSON", "https://www.googleapis.com/auth/bigquery", ct);

        var query = $"SELECT sku.description, usage.unit, SUM(usage.amount), SUM(cost + IFNULL((SELECT SUM(c.amount) FROM UNNEST(credits) c), 0)), currency FROM `{store.Settings.CloudBillingTable}` WHERE usage_start_time >= TIMESTAMP_TRUNC(CURRENT_TIMESTAMP(), MONTH, 'America/Los_Angeles') AND REGEXP_CONTAINS(LOWER(service.description), r'text.?to.?speech') GROUP BY 1,2,5";

        using var req = new HttpRequestMessage(HttpMethod.Post, "https://bigquery.googleapis.com/bigquery/v2/projects/" + project + "/queries"); req.Headers.Authorization = new("Bearer", access);

        req.Content = JsonContent.Create(new { query, useLegacySql = false, timeoutMs = 20000, maximumBytesBilled = "100000000", location = store.Settings.CloudBillingLocation });

        using var res = await http.SendAsync(req, ct); if (!res.IsSuccessStatusCode) throw new ProviderException("Cloud kullanım sorgusu / tablo izinleri", (int)res.StatusCode);

        using var data = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));

        if (!data.RootElement.GetProperty("jobComplete").GetBoolean() || data.RootElement.TryGetProperty("pageToken", out _)) throw new InvalidOperationException("Kullanım sorgusu tamamlanmadı veya sonuç eksik. Tekrar güncelle.");

        var report = new Report { Month = DateTimeOffset.UtcNow.ToString("yyyy-MM"), Fetched = DateTimeOffset.UtcNow };

        if (data.RootElement.TryGetProperty("rows", out var rows)) foreach (var row in rows.EnumerateArray()) { var values = row.GetProperty("f").EnumerateArray().Select(f => f.GetProperty("v").ToString()).ToArray(); report.Rows.Add(new() { Sku = values[0], Unit = values[1], Amount = double.Parse(values[2], CultureInfo.InvariantCulture), Cost = double.Parse(values[3], CultureInfo.InvariantCulture), Currency = values[4] }); }

        if (report.Rows.Count == 0) throw new InvalidOperationException("Google raporunda TTS verisi yok. Bunu sıfır kullanım saymıyoruz; veri aktarımının tamamlanmasını bekle.");

        Json.Write(FilePath, report); return report;

    }

    public async Task Control(bool enable, CancellationToken ct)

    {

        if (!store.Settings.CloudApiControl) throw new InvalidOperationException("Cloud API yönetim izni program ayarlarında kapalı.");

        var project = store.Settings.CloudControlProject;

        if (!Regex.IsMatch(project, @"^\d{6,20}$")) throw new InvalidOperationException("Yönetilecek Cloud proje numarasını yaz ve ayarları kaydet.");

        var (_, access) = await Token("GOOGLE_CONTROL_JSON", "https://www.googleapis.com/auth/cloud-platform", ct);

        using var req = new HttpRequestMessage(HttpMethod.Post, "https://serviceusage.googleapis.com/v1/projects/" + project + "/services/texttospeech.googleapis.com:" + (enable ? "enable" : "disable"));

        req.Headers.Authorization = new("Bearer", access); req.Content = JsonContent.Create(new { });

        using var response = await http.SendAsync(req, ct); if (!response.IsSuccessStatusCode) throw new ProviderException("Cloud TTS API yönetimi", (int)response.StatusCode);

        using var initial = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));

        var op = initial.RootElement;

        for (int i = 0; i < 20; i++) {

            if (op.TryGetProperty("error", out _)) throw new InvalidOperationException("Google API yönetim işlemi başarısız oldu; Cloud konsolundan kontrol et.");

            if (op.TryGetProperty("done", out var done) && done.GetBoolean()) return;

            var name = op.GetProperty("name").GetString()!; if (!Regex.IsMatch(name, @"^operations/[A-Za-z0-9_-]+$")) throw new InvalidOperationException("Google işlem kimliği doğrulanamadı.");

            await Task.Delay(2000, ct); using var poll = new HttpRequestMessage(HttpMethod.Get, "https://serviceusage.googleapis.com/v1/" + name); poll.Headers.Authorization = new("Bearer", access);

            using var result = await http.SendAsync(poll, ct); if (!result.IsSuccessStatusCode) throw new ProviderException("Cloud API işlem durumu", (int)result.StatusCode);

            using var data = JsonDocument.Parse(await result.Content.ReadAsStringAsync(ct)); op = data.RootElement.Clone();

        }

        throw new InvalidOperationException("Google işlemi henüz tamamlanmadı; proje API durumunu Cloud konsolundan kontrol et.");

    }

    public sealed class LimitException(string message) : InvalidOperationException(message);

    public static void Check(Report report, long local, int upcoming, int limit)

    {

        if (local < 0 || upcoming < 0 || limit <= 0 || report.Rows.Any(x => !double.IsFinite(x.Amount) || !double.IsFinite(x.Cost) || x.Amount < 0)) throw new InvalidOperationException("Kota raporunda geçersiz sayı var; üretim güvenli biçimde durduruldu.");

        if (report.Month != DateTimeOffset.UtcNow.ToString("yyyy-MM") || report.Fetched < DateTimeOffset.UtcNow.AddHours(-1) || report.Rows.Count == 0) throw new InvalidOperationException("Google TTS hesap raporu eksik veya eski. Kota takibi ekranından güncelle; yerel ses alternatifi kullanılabilir.");

        if (report.Rows.Any(x => x.Cost > .001)) throw new LimitException("Cloud raporunda TTS ücreti görüldü. Ücretsiz koruma Google TTS üretimini durdurdu.");

        if (report.Rows.Any(x => x.Unit != "count" && x.Unit != "characters")) throw new InvalidOperationException("Google kullanım birimi karakter olarak doğrulanamadı. Üretim güvenli biçimde durduruldu.");

        var count = report.Rows.Sum(x => Math.Max(0, x.Amount));

        // Deliberately double-count local usage to preserve a margin for delayed billing rows.

        if (count + local + upcoming > limit) throw new LimitException("Cloud raporu + yerel kullanım güvenlik sınırına ulaştı. Google TTS durduruldu.");

    }

}

