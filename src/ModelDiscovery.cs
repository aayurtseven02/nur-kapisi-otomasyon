using System.Net.Http.Headers;

using System.Text.Json;



namespace AnkaStudio;



public static class ModelDiscovery

{

    public static async Task<string[]> Nvidia(Store store, CancellationToken ct, HttpClient? client = null)
        => await List(store, "NVIDIA", "NVIDIA_API_KEY", "https://integrate.api.nvidia.com/v1/models", ct, client);
    public static async Task<string[]> Groq(Store store, CancellationToken ct, HttpClient? client = null)

        => await List(store, "Groq", "GROQ_API_KEY", "https://api.groq.com/openai/v1/models", ct, client);

    public static async Task<string[]> Kimi(Store store, CancellationToken ct, HttpClient? client = null)

        => await List(store, "Kimi", "KIMI_API_KEY", "https://api.moonshot.ai/v1/models", ct, client);

    public static async Task<string[]> Cloud(Store store, string provider, CancellationToken ct) => provider switch {

        "Gemini" => (await new Services(store, _ => {}).GeminiTextModels(store.Key("GEMINI_API_KEY") is string key && key != "" ? key : throw new InvalidOperationException("Önce Gemini anahtarını kaydet."), ct, true)).ToArray(),

        "NVIDIA" => await Nvidia(store, ct), "Groq" => await Groq(store, ct), "Kimi" => await Kimi(store, ct),

        "OpenAI" => await List(store, provider, "OPENAI_API_KEY", "https://api.openai.com/v1/models", ct, null),

        "OpenRouter" => await List(store, provider, "OPENROUTER_API_KEY", "https://openrouter.ai/api/v1/models", ct, null),

        _ => throw new InvalidOperationException("Bilinmeyen sağlayıcı.")

    };

    static async Task<string[]> List(Store store, string provider, string keyName, string url, CancellationToken ct, HttpClient? client)

    {

        var key = store.Key(keyName);

        if (key == "") throw new InvalidOperationException("Önce " + provider + " anahtarını Bağlantı Merkezi'ne kaydet.");

        using var own = client == null ? new HttpClient { Timeout = TimeSpan.FromSeconds(30) } : null;

        using var req = new HttpRequestMessage(HttpMethod.Get, url); req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);

        using var res = await (client ?? own!).SendAsync(req, ct);

        var body = await res.Content.ReadAsStringAsync(ct);

        if (!res.IsSuccessStatusCode) throw new ProviderException(provider + " model listesi", (int)res.StatusCode, Services.ErrorDetail(body, "model listesi", key));

        using var doc = JsonDocument.Parse(body);

        var models = doc.RootElement.GetProperty("data").EnumerateArray()

            .Where(x => !x.TryGetProperty("active", out var active) || active.ValueKind != JsonValueKind.False)

            .Select(x => x.GetProperty("id").GetString() ?? "")

            .Where(x => x != "" && !x.Contains("guard", StringComparison.OrdinalIgnoreCase) && !x.Contains("safeguard", StringComparison.OrdinalIgnoreCase) && !x.Contains("whisper", StringComparison.OrdinalIgnoreCase) && !x.Contains("tts", StringComparison.OrdinalIgnoreCase) && !x.Contains("orpheus", StringComparison.OrdinalIgnoreCase) && !x.Contains("playai", StringComparison.OrdinalIgnoreCase))

            .Where(x => !new[] { "embedding", "embed", "rerank", "reward", "diffusion", "image", "audio", "transcribe", "dall-e", "sora", "realtime", "moderation" }.Any(term => x.Contains(term, StringComparison.OrdinalIgnoreCase)))

            .Distinct().Order().ToArray();

        if (models.Length == 0) throw new InvalidOperationException(provider + " hesabından seçilebilir metin modeli alınamadı."); return models;

    }

}



public sealed partial class StudioForm

{

    void CloudModelPicker(FlowLayoutPanel panel, string provider, string currentModel, Action<string> set, List<Action> saves)

    {

        var cachePath = Path.Combine(store.Root, "model-catalog.json");

        var cache = Json.Read(cachePath, new Dictionary<string, string[]>());

        var items = (cache.GetValueOrDefault(provider) ?? []).Append(currentModel).Where(x => !string.IsNullOrWhiteSpace(x)).Where(x => !new[] { "embedding", "embed", "rerank", "reward", "diffusion", "image", "audio", "transcribe", "dall-e", "sora", "realtime", "moderation" }.Any(term => x.Contains(term, StringComparison.OrdinalIgnoreCase)))

            .Distinct().Order().ToArray();

        var select = Theme.Combo(items, currentModel, 650);

        Field(panel, provider + " modeli • listeden seç", select);

        saves.Add(() => { if (select.SelectedItem is string model) set(model); });

        var info = Theme.Label("Kayıtlı seçenekler gösteriliyor. Güncel listeyi sağlayıcıdan yenileyebilirsin; listede bulunmak bakiye ve üretim erişimini garanti etmez.", 9, color: Theme.Muted, width: 740, height: 65);

        Button? refresh = null;

        refresh = Theme.Button(provider + " model listesini yenile", () => _ = Safe(async () => {

            if (engine.Busy) throw new InvalidOperationException("Önce çalışan işlemi tamamla.");

            refresh!.Enabled = false; info.Text = "Model listesi alınıyor…";

            try {

                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));

                var models = await ModelDiscovery.Cloud(store, provider, timeout.Token);

                if (provider == "OpenRouter" && store.Settings.FreeOnly) models = models.Where(m => m == "openrouter/free" || m.EndsWith(":free")).Append("openrouter/free").Distinct().ToArray();

                if (models.Length == 0) throw new InvalidOperationException("Seçilebilir metin modeli bulunamadı.");

                cache = Json.Read(cachePath, new Dictionary<string, string[]>()); cache[provider] = models; Json.Write(cachePath, cache);

                if (select.IsDisposed) return; string previous = select.Text; select.Items.Clear(); select.Items.AddRange(models); select.SelectedItem = previous;

                if (select.SelectedIndex < 0) select.SelectedIndex = 0;

                info.Text = models.Length + " model geldi. Birini seçip Tüm ayarları kaydet düğmesine bas.";

            } catch { if (!info.IsDisposed) info.Text = "Liste yenilenemedi; önceki seçimler korundu."; throw; }

            finally { if (!refresh.IsDisposed) refresh.Enabled = true; }

        }), false, 330);

        panel.Controls.Add(refresh); panel.Controls.Add(info);
        if (provider == "NVIDIA") NvidiaScanPanel(panel, saves);

    }

    void KimiPicker(FlowLayoutPanel page, Settings settings, List<Action> saves)

    {

        var field = Theme.Input(settings.KimiModel, width: 700); saves.Add(() => settings.KimiModel = field.Text.Trim());

        var select = Theme.Combo([], "", 700); select.SelectedIndexChanged += (_, _) => { if (select.SelectedItem is string model) field.Text = model; };

        var status = Theme.Label("API hesabındaki modeller listelenir; kullanım ücretli olabilir. Model listesi üretim bakiyesini doğrulamaz.", 9, color: Theme.Warning, width: 740, height: 65);

        Button? fetch = null;

        fetch = Theme.Button("Kimi modellerini getir", () => _ = Safe(async () => {

            if (engine.Busy) throw new InvalidOperationException("Önce çalışan üretimi durdur."); fetch!.Enabled = false;

            try { using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30)); var models = await ModelDiscovery.Kimi(store, timeout.Token); if (select.IsDisposed) return; select.Items.Clear(); select.Items.AddRange(models); status.Text = models.Length + " model geldi. Birini seç ve Tüm ayarları kaydet tuşuna bas."; }

            finally { if (!fetch.IsDisposed) fetch.Enabled = true; }

        }), false, 290); page.Controls.Add(fetch); Field(page, "Kimi model kimliği", field); Field(page, "Kimi hesabından alınan modeller", select); page.Controls.Add(status);

    }

    void GroqPicker(FlowLayoutPanel page, Settings settings, List<Action> saves)

    {

        var field = Theme.Input(settings.GroqModel, width: 700); saves.Add(() => settings.GroqModel = field.Text.Trim());

        var select = Theme.Combo([], "", 700);

        select.SelectedIndexChanged += (_, _) => { if (select.SelectedItem is string model) field.Text = model; };

        var status = Theme.Label("Model listesi ücret ve kota bilgisini garanti etmez; hesap erişimini hizmet belirler.", 9, color: Theme.Warning, width: 740, height: 65);

        Button? fetch = null;

        fetch = Theme.Button("Groq modellerini getir", () => _ = Safe(async () => {

            if (engine.Busy) throw new InvalidOperationException("Önce çalışan üretimi durdur.");

            fetch!.Enabled = false; status.Text = "Groq hesabındaki modeller alınıyor...";

            try { using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30)); var models = await ModelDiscovery.Groq(store, timeout.Token); if (select.IsDisposed) return; select.Items.Clear(); select.Items.AddRange(models); status.Text = models.Length + " model geldi. Birini seç ve Tüm ayarları kaydet tuşuna bas."; }

            finally { if (!fetch.IsDisposed) fetch.Enabled = true; }

        }), false, 290); page.Controls.Add(fetch);

        Field(page, "Groq model kimliği", field);

        Field(page, "Groq hesabından alınan modeller • seçince model alanına aktarılır", select);

        page.Controls.Add(status);

    }

}

