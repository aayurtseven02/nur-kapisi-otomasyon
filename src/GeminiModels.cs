using System.Text.Json;

namespace AnkaStudio;

public sealed partial class Services

{

    public async Task<List<string>> GeminiTextModels(string key, CancellationToken ct, bool allTextModels = false)

    {

        var names = new HashSet<string>(StringComparer.Ordinal); string? token = null;

        for (int page = 0; page < 5; page++) {

            using var req = new HttpRequestMessage(HttpMethod.Get, "https://generativelanguage.googleapis.com/v1beta/models?pageSize=1000" + (token == null ? "" : "&pageToken=" + Uri.EscapeDataString(token)));

            req.Headers.Add("x-goog-api-key", key);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(30));

            using var response = await http.SendAsync(req, timeout.Token);

            if (!response.IsSuccessStatusCode) throw new ProviderException("Gemini model listesi", (int)response.StatusCode);

            using var doc = JsonDocument.Parse(await Reliability.ReadText(response, 2000000, timeout.Token));

            if (!doc.RootElement.TryGetProperty("models", out var models) || models.ValueKind != JsonValueKind.Array) throw new InvalidOperationException("Model listesi eksik.");

            foreach (var item in models.EnumerateArray()) {

                if (!item.TryGetProperty("name", out var nameValue) || nameValue.ValueKind != JsonValueKind.String) continue;

                var name = nameValue.GetString()!.Replace("models/", "", StringComparison.Ordinal);

                if (!name.StartsWith("gemini-", StringComparison.Ordinal) || (!allTextModels && !name.Contains("flash", StringComparison.Ordinal)) || new[] { "image", "tts", "audio", "live", "computer", "robot", "embedding", "agent" }.Any(name.Contains)) continue;

                if (item.TryGetProperty("supportedGenerationMethods", out var methods) && methods.ValueKind == JsonValueKind.Array && methods.EnumerateArray().Any(m => m.ValueKind == JsonValueKind.String && m.GetString() == "generateContent")) names.Add(name);

            }

            token = doc.RootElement.TryGetProperty("nextPageToken", out var next) ? next.GetString() : null;

            if (string.IsNullOrWhiteSpace(token)) break;

        }

        return names.OrderBy(n => n.Contains("preview") || n.Contains("exp") ? 1 : 0).ThenBy(n => n.Contains("lite") ? 1 : 0).ThenByDescending(n => n, StringComparer.Ordinal).ToList();

    }

}

