using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AnkaStudio;

public sealed class NvidiaModelCheck
{
    public string Model { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTimeOffset CheckedAt { get; set; }
    public DateTimeOffset RetryAt { get; set; }
    public long Milliseconds { get; set; }
}
public sealed class NvidiaScanReport
{
    public string KeyId { get; set; } = "";
    public DateTimeOffset FinishedAt { get; set; }
    public DateTimeOffset RetryAt { get; set; }
    public bool Complete { get; set; }
    public int Total { get; set; }
    public List<NvidiaModelCheck> Models { get; set; } = [];
}

// Cache is account-specific. A successful small probe does not promise future quota or long-script quality.
public static class NvidiaModels
{
    sealed class State
    {
        public readonly object Gate = new();
        public CancellationTokenSource? Cancellation;
        public Task? Task;
        public string KeyId = "";
        public string Progress = "Tarama henüz başlamadı.";
    }
    static readonly ConditionalWeakTable<Store, State> States = new();
    static readonly HttpClient Http = new() { Timeout = Timeout.InfiniteTimeSpan };
    static string PathFor(Store store) => Path.Combine(store.Root, "nvidia-model-health.json");
    static string Identity(Store store) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(store.Key("NVIDIA_API_KEY"))));
    public static bool Enabled(Store store) => store.Settings.NvidiaAutoSelect && store.Settings.CloudAiEnabled &&
        store.Settings.EnabledCloudProviders.Split(',', StringSplitOptions.TrimEntries).Contains("NVIDIA") && store.Key("NVIDIA_API_KEY") != "";
    public static string Progress(Store store) { var state = States.GetOrCreateValue(store); lock (state.Gate) return state.Progress; }
    public static NvidiaScanReport Read(Store store)
    {
        var report = Json.Read(PathFor(store), new NvidiaScanReport());
        return report.KeyId == Identity(store) ? report : new NvidiaScanReport();
    }
    public static string[] Working(Store store)
    {
        var state = States.GetOrCreateValue(store);
        lock (state.Gate) return Read(store).Models.Where(m => m.Status == "Hazır" && m.RetryAt <= DateTimeOffset.UtcNow)
            .OrderBy(m => m.Model == store.Settings.NvidiaModel ? 0 : 1).ThenBy(m => m.Milliseconds).Select(m => m.Model).Distinct().ToArray();
    }
    public static string[] Candidates(Store store)
    {
        var working = Working(store);
        if (working.Length > 0) return working;
        // A finished scan with zero successes must not label a failed model as working.
        return Read(store).Models.Count > 0 || Read(store).Complete ? [] : [store.Settings.NvidiaModel];
    }
    public static void Cancel(Store store) { var state = States.GetOrCreateValue(store); lock (state.Gate) state.Cancellation?.Cancel(); }
    public static Task Start(Store store, bool force = false, HttpClient? client = null)
    {
        var state = States.GetOrCreateValue(store);
        lock (state.Gate)
        {
            if (!Enabled(store)) { state.Cancellation?.Cancel(); return Task.CompletedTask; }
            var keyId = Identity(store);
            if (state.Task is { IsCompleted: false } && state.KeyId == keyId) return state.Task;
            state.Cancellation?.Cancel();
            var report = Read(store);
            if (!force && (report.Complete || report.RetryAt > DateTimeOffset.UtcNow))
            { state.Progress = $"Kayıtlı kontrol: {Working(store).Length} çalışan model / {report.Total}."; return Task.CompletedTask; }
            state.KeyId = keyId; var cancellation = new CancellationTokenSource(); state.Cancellation = cancellation;
            state.Task = Task.Run(async () => { try { await Scan(store, state, keyId, client ?? Http, client != null, cancellation.Token); }
                catch (OperationCanceledException) { lock (state.Gate) if (state.KeyId == keyId) state.Progress = "Model taraması durduruldu; sonuçlar korundu."; }
                catch (Exception) { lock (state.Gate) if (state.KeyId == keyId) state.Progress = "Tarama tamamlanamadı; anahtar, internet ve NVIDIA erişimini kontrol et."; }
                finally { lock (state.Gate) { if (ReferenceEquals(state.Cancellation, cancellation)) state.Cancellation = null; cancellation.Dispose(); } }
            });
            return state.Task;
        }
    }
    public static async Task WaitForReady(Store store, CancellationToken ct)
    {
        var task = Start(store);
        await task.WaitAsync(ct);
        ct.ThrowIfCancellationRequested();
    }
    static void Save(Store store, State state, NvidiaScanReport report)
    {
        lock (state.Gate) { if (report.KeyId == Identity(store)) {
            var previous = Read(store);
            foreach (var item in report.Models) { var old = previous.Models.FirstOrDefault(m => m.Model == item.Model); if (old != null && old.RetryAt > item.RetryAt) item.RetryAt = old.RetryAt; }
            Json.Write(PathFor(store), report);
        } }
    }
    static async Task Scan(Store store, State state, string keyId, HttpClient client, bool testing, CancellationToken ct)
    {
        lock (state.Gate) state.Progress = "NVIDIA metin modelleri alınıyor…";
        var key = store.Key("NVIDIA_API_KEY");
        using var listTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct); listTimeout.CancelAfter(TimeSpan.FromSeconds(30));
        var models = await ModelDiscovery.Nvidia(store, listTimeout.Token, client);
        var report = new NvidiaScanReport { KeyId = keyId, Total = models.Length };
        int consecutiveLimits = 0;
        foreach (var model in models.OrderBy(m => m == store.Settings.NvidiaModel ? 0 : 1))
        {
            ct.ThrowIfCancellationRequested(); if (!Enabled(store) || Identity(store) != keyId) throw new OperationCanceledException(ct);
            lock (state.Gate) state.Progress = $"Kontrol {report.Models.Count + 1}/{models.Length} • {model} • hazır: {report.Models.Count(m => m.Status == "Hazır")}";
            var watch = Stopwatch.StartNew(); var check = new NvidiaModelCheck { Model = model, CheckedAt = DateTimeOffset.UtcNow };
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(model == "z-ai/glm-5.3-flash" ? 45 : 25));
            try { await Probe(client, key, model, timeout.Token); check.Status = "Hazır"; consecutiveLimits = 0; }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { check.Status = "Süre aşımı"; check.RetryAt = DateTimeOffset.UtcNow.AddMinutes(15); }
            catch (ProviderException ex) { check.Status = ex.Status == 429 ? "Kota / hız sınırı" : ex.Status == 401 ? "Anahtar reddedildi" : ex.Status == 403 ? "Model erişimi kapalı" : ex.Status == 404 ? "Model bulunamadı" : "Hizmet hatası"; check.RetryAt = DateTimeOffset.UtcNow.AddMinutes(ex.Status == 429 ? 15 : 60); if (ex.Status == 429) consecutiveLimits++; if (ex.Status == 401) consecutiveLimits = 3; }
            catch (Exception ex) when (ex is not OperationCanceledException) { check.Status = ex is AiContentException or JsonException ? "JSON denetimi başarısız" : "Bağlantı hatası"; check.RetryAt = DateTimeOffset.UtcNow.AddMinutes(15); }
            ct.ThrowIfCancellationRequested(); if (Identity(store) != keyId) throw new OperationCanceledException(ct);
            check.Milliseconds = watch.ElapsedMilliseconds; report.Models.Add(check); Save(store, state, report);
            if (consecutiveLimits >= 3) { report.RetryAt = DateTimeOffset.UtcNow.AddMinutes(15); break; }
            if (!testing) await Task.Delay(TimeSpan.FromSeconds(1), ct);
        }
        report.Complete = report.Models.Count == report.Total; report.FinishedAt = DateTimeOffset.UtcNow; Save(store, state, report);
        lock (state.Gate) state.Progress = $"{(report.Complete ? "Tarama tamamlandı" : "Kota/erişim nedeniyle tarama bekliyor")} • {report.Models.Count(m => m.Status == "Hazır")}/{report.Total} çalışan model.";
    }
    public static void Configure(Dictionary<string, object> payload, string model, bool probe = false)
    {
        if (model.StartsWith("qwen/qwen3", StringComparison.OrdinalIgnoreCase) || model.Contains("nemotron-3", StringComparison.OrdinalIgnoreCase)) payload["chat_template_kwargs"] = new { enable_thinking = false };
        if (model.Equals("z-ai/glm-5.3-flash", StringComparison.OrdinalIgnoreCase)) {
            payload["reasoning_effort"] = "low";
            payload["chat_template_kwargs"] = new { clear_thinking = true };
            if (probe) payload["max_tokens"] = 2048;
        }
    }
    static async Task Probe(HttpClient client, string key, string model, CancellationToken ct)
    {
        for (int mode = 0; mode < 2; mode++)
        {
            var payload = new Dictionary<string, object> { ["model"] = model, ["max_tokens"] = 512, ["temperature"] = 0,
                ["messages"] = new[] { new { role = "user", content = "Return exactly this JSON object and nothing else: {\"Ready\":true,\"Text\":\"anka\",\"Count\":2}. No explanation or reasoning." } } };
            if (mode == 0) payload["response_format"] = new { type = "json_object" };
            Configure(payload, model, true);
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://integrate.api.nvidia.com/v1/chat/completions");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            var body = await Reliability.ReadText(response, 1_000_000, ct);
            if (!response.IsSuccessStatusCode)
            {
                if (mode == 0 && (int)response.StatusCode is 400 or 422 && new[] { "response_format", "json_object", "structured output" }.Any(t => body.Contains(t, StringComparison.OrdinalIgnoreCase))) continue;
                throw new ProviderException("NVIDIA", (int)response.StatusCode);
            }
            using var doc = JsonDocument.Parse(body); var choice = doc.RootElement.GetProperty("choices")[0];
            if (choice.TryGetProperty("finish_reason", out var reason) && reason.GetString() == "length") throw new AiContentException("Kesilmiş yanıt");
            var text = choice.GetProperty("message").GetProperty("content").GetString() ?? "";
            text = Regex.Replace(text, @"<think>.*?</think>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            using var answer = JsonDocument.Parse(Services.ExtractJson(text)); var a = answer.RootElement;
            if (!a.TryGetProperty("Ready", out var ready) || ready.ValueKind != JsonValueKind.True || !a.TryGetProperty("Text", out var value) || value.GetString() != "anka" || !a.TryGetProperty("Count", out var count) || count.ValueKind != JsonValueKind.Number || !count.TryGetInt32(out var n) || n != 2) throw new AiContentException("JSON alanları hatalı");
            return;
        }
        throw new AiContentException("JSON denetimi başarısız");
    }
    public static void Failed(Store store, string model, bool permanent)
    {
        var state = States.GetOrCreateValue(store); lock (state.Gate)
        {
            var report = Read(store); var check = report.Models.FirstOrDefault(m => m.Model == model);
            if (check == null) return; check.RetryAt = DateTimeOffset.UtcNow.AddMinutes(permanent ? 60 : 5); Save(store, state, report);
        }
    }
}
