using System.Text;

using System.Text.Json;

using System.Net.Http.Json;



namespace AnkaStudio;



public sealed class GoogleTts(Store store, HttpClient? client = null)

{

    static readonly HttpClient Shared = new() { Timeout = TimeSpan.FromSeconds(90) };

    readonly HttpClient http = client ?? Shared;

    public sealed class Usage { public string Month { get; set; } = ""; public long Characters { get; set; } }

    static readonly SemaphoreSlim Gate = new(1, 1);

    string UsagePath => Path.Combine(store.Root, "google-tts-usage.json");

    public Usage ReadUsage()

    {

        var u = Json.Read(UsagePath, new Usage());

        var month = DateTimeOffset.UtcNow.ToString("yyyy-MM");

        return u.Month == month ? u : new Usage { Month = month };

    }

    HttpRequestMessage Request(HttpMethod method, string endpoint, bool timed = false)

    {

        var key = store.Key("GOOGLE_TTS_API_KEY");

        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("Bağlantılar → Google Cloud TTS anahtarını kaydet.");

        var req = new HttpRequestMessage(method, "https://texttospeech.googleapis.com/" + (timed ? "v1beta1/" : "v1/") + endpoint);

        req.Headers.Add("x-goog-api-key", key); return req;

    }

    public async Task<string[]> Voices(CancellationToken ct)

    {

        using var req = Request(HttpMethod.Get, "voices?languageCode=tr-TR");

        using var res = await http.SendAsync(req, ct);

        if (!res.IsSuccessStatusCode) throw new ProviderException("Google TTS • API / faturalandırma / anahtar izinlerini kontrol et", (int)res.StatusCode);

        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));

        return doc.RootElement.GetProperty("voices").EnumerateArray().Select(v => v.GetProperty("name").GetString()!).Where(v => v.StartsWith("tr-TR-")).Order().ToArray();

    }

    public static int Count(string text) => text.EnumerateRunes().Count();

    public static void CheckBudget(long used, int count, int limit)

    {

        if (limit <= 0 || used + count > limit) throw new InvalidOperationException("Google TTS aylık yerel karakter sınırına ulaşıldı. Limit veya yerel ses alternatifini kontrol et.");

    }

    public async Task Speak(string text, string path, Settings settings, CancellationToken ct) => await SpeakCore(text, path, settings, ct);

    public async Task<Dictionary<string, double>> SpeakMarked(IReadOnlyList<string> captions, string path, Settings settings, CancellationToken ct)

    {

        var spoken=captions.Select(x=>NarrationFlow.SpokenText(x,settings)).ToArray();

        var ssml = MarkedSsml(spoken);

        return await SpeakCore(string.Join(" ", spoken), path, settings, ct, ssml);

    }

    public static string MarkedSsml(IReadOnlyList<string> captions) => "<speak>" + string.Join(" ", captions.Select((text, i) => "<mark name=\"c" + i + "\"/>" + System.Security.SecurityElement.Escape(NarrationFlow.SpokenText(text)))) + "<mark name=\"end\"/></speak>";

    async Task<Dictionary<string, double>> SpeakCore(string text, string path, Settings settings, CancellationToken ct, string? ssml = null)

    {

        if (!store.Settings.GoogleTtsEnabled) throw new InvalidOperationException("Google TTS bu programda kapatıldı.");

        if (store.Settings.CloudUsageRequired) {

            var cloud = new CloudUsage(store); var report = cloud.Read();

            if (report.Fetched < DateTimeOffset.UtcNow.AddHours(-1) || report.Month != DateTimeOffset.UtcNow.ToString("yyyy-MM")) report = await cloud.Refresh(ct);

            try { CloudUsage.Check(report, ReadUsage().Characters, Count(text), settings.GoogleTtsMonthlyLimit); }

            catch (CloudUsage.LimitException ex) when (store.Settings.CloudAutoDisable && store.Settings.CloudApiControl) {

                store.Settings.GoogleTtsEnabled = false; store.Save();

                try { await cloud.Control(false, ct); } catch (Exception error) when (error is not OperationCanceledException) { throw new InvalidOperationException(ex.Message + " Programda TTS kapalı; Google Cloud API kapatma doğrulanamadı: " + error.Message); }

                throw;

            }

        }

        if (Encoding.UTF8.GetByteCount(ssml ?? text) > 4500) throw new InvalidOperationException("Google TTS metni çok uzun; daha kısa sahneler kullan.");

        if (!settings.GoogleTtsVoice.StartsWith("tr-TR-")) throw new InvalidOperationException("Türkçe Google TTS sesi seç.");

        await Gate.WaitAsync(ct);

        try

        {

            var usage = ReadUsage(); var count = Count(text); CheckBudget(usage.Characters, count, settings.GoogleTtsMonthlyLimit);

            using var req = Request(HttpMethod.Post, "text:synthesize", ssml != null);

            req.Content = ssml == null ? JsonContent.Create(new { input = new { text }, voice = new { languageCode = "tr-TR", name = settings.GoogleTtsVoice }, audioConfig = new { audioEncoding = "LINEAR16", sampleRateHertz = 24000 } }) : JsonContent.Create(new { input = new { ssml }, voice = new { languageCode = "tr-TR", name = settings.GoogleTtsVoice }, audioConfig = new { audioEncoding = "LINEAR16", sampleRateHertz = 24000 }, enableTimePointing = new[] { "SSML_MARK" } });

            // Reserve before sending: interrupted requests may still be billed. Never retry blindly.

            usage.Characters += count; Json.Write(UsagePath, usage);

            using var res = await http.SendAsync(req, ct);

            if (!res.IsSuccessStatusCode) throw new ProviderException("Google TTS • API / faturalandırma / anahtar izinlerini kontrol et", (int)res.StatusCode);

            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));

            var bytes = Convert.FromBase64String(doc.RootElement.GetProperty("audioContent").GetString()!);

            if (bytes.Length < 44 || Encoding.ASCII.GetString(bytes, 0, 4) != "RIFF") throw new InvalidOperationException("Google TTS geçerli WAV sesi döndürmedi.");

            await File.WriteAllBytesAsync(path, bytes, ct);

            var times = new Dictionary<string, double>();

            if (doc.RootElement.TryGetProperty("timepoints", out var points)) foreach (var point in points.EnumerateArray()) {

                var name = point.GetProperty("markName").GetString()!; double time = point.GetProperty("timeSeconds").GetDouble();

                if (!double.IsFinite(time) || time < 0 || !times.TryAdd(name, time)) throw new InvalidOperationException("Google TTS zaman işaretleri geçersiz.");

            }

            return times;

        }

        finally { Gate.Release(); }

    }

}

