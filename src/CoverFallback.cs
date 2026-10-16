using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AnkaStudio;

public sealed class CoverFallback(Store store, Action<string> log, HttpClient? client = null)
{
    public sealed class Usage { public string Month { get; set; } = ""; public int Requests { get; set; } }
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(2) };
    static readonly object Gate = new();
    public static string[] ModelsFor(string provider, Settings settings) => (provider switch {
        "NVIDIA" => new[] { settings.CoverNvidiaModel, "black-forest-labs/flux.1-schnell", "stabilityai/stable-diffusion-xl" },
        "Gemini" => new[] { settings.CoverGeminiModel, "gemini-3.1-flash-image", "gemini-3-pro-image", "gemini-2.5-flash-image" },
        "OpenAI" => new[] { settings.CoverOpenAiModel, "gpt-image-1.5", "gpt-image-1", "gpt-image-1-mini" },
        _ => Array.Empty<string>()
    }).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray();
    bool Reserve(Settings settings)
    {
        lock (Gate) {
            var file = Path.Combine(store.Root, "cover-usage.json"); var month = DateTimeOffset.UtcNow.ToString("yyyy-MM");
            var usage = Json.Read(file, new Usage()); if (usage.Month != month) usage = new Usage { Month = month };
            if (usage.Requests >= Math.Clamp(settings.CoverMonthlyRequests, 0, 1000)) return false;
            usage.Requests++; Json.Write(file, usage); return true;
        }
    }
    void ReleaseRejectedRequest()
    {
        lock(Gate){var file=Path.Combine(store.Root,"cover-usage.json");var usage=Json.Read(file,new Usage());if(usage.Month==DateTimeOffset.UtcNow.ToString("yyyy-MM")&&usage.Requests>0){usage.Requests--;Json.Write(file,usage);}}
    }
    public async Task Create(string path, Settings settings, string topic, string title, bool vertical, CancellationToken ct, string? scenePrompt = null, string? referenceImage = null,int castCount=1,IReadOnlyList<int>? characterIds=null)
    {
        var copy = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(settings))!;
        bool styleReference=scenePrompt==null && settings.CoverReferenceImage!="";
        if(styleReference){referenceImage=settings.CoverReferenceImage;if(!File.Exists(referenceImage))throw new InvalidOperationException("Kapak referansı bulunamadı. Referans kapağı yeniden seç veya referansı kaldır.");}
        if (copy.CoverAutoStyle) copy.CoverStyle = CoverDesigner.Suggest(topic);
        if (scenePrompt != null) { copy.CoverAiEnabled = true; copy.CoverMode = "Şablon"; }
        if (copy.CoverMode == "Kendi PNG kapak") { CoverDesigner.Save(path, copy, title, vertical); log("Kullanıcının hazır kapağı kullanıldı."); return; }
        if (copy.CoverAiEnabled)
        {
            copy.CoverBackground = "";
            async Task GenerateLocal()
            {
                try {
                    if(!File.Exists(settings.CoverLocalWorkflow))settings.CoverLocalWorkflow=LocalCoverEngine.FindInstalledWorkflow();
                    if(client==null)await LocalCoverEngine.Start(settings.CoverLocalWorkflow,log,ct);
                    if(client==null && styleReference)await LocalCoverEngine.CheckStorySupport(ct,settings.CoverLocalWorkflow);
                    var prompt = "Subject: " + topic[..Math.Min(topic.Length, 1800)] + ". User visual direction: " + copy.CoverDesignInstructions[..Math.Min(copy.CoverDesignInstructions.Length, 2500)] + ". Cinematic illustration, rich colors, warm light. Subject on right, title space on left. No text or logos, no fabricated scripture.";
                    if (scenePrompt != null) prompt = scenePrompt;
                    else if(copy.CoverTitleStyle=="3D Telkari Altın"&&LocalCoverEngine.ModelOf(settings.CoverLocalWorkflow)=="FLUX.2")prompt="Subject: "+topic+". Direction: "+copy.CoverDesignInstructions+". Cinematic rich-color illustration on right. On left, massive embossed metallic gold serif title reading exactly: '"+title+"'. Ottoman floral filigree engraved in the gold letters, bevelled edges, delicate gold corner ornaments, warm light, empty emerald subtitle banner. Do not add any other words or logos.";
                    else if(styleReference)prompt+=" Use the supplied image only as a style reference for palette, lighting, mood and visual hierarchy. Depict the NEW subject described above; do not copy its people, old title, letters, logo or unrelated objects. No text; the current approved title is added separately.";
                    var bytes = await LocalCoverEngine.Generate(settings.CoverLocalWorkflow, prompt, vertical, log, ct, client:client, square: scenePrompt!=null && referenceImage==null, referenceImage: referenceImage,castCount:castCount,characterIds:characterIds,styleReference:styleReference,quality:settings.CoverLocalQuality??"Dengeli");
                    using var stream = new MemoryStream(bytes); using var image = Image.FromStream(stream);
                    if (image.Width < 256 || image.Height < 256 || (long)image.Width * image.Height > 40_000_000) throw new InvalidOperationException("Yerel görsel ölçüsü uygun değil.");
                    var localBackground = Path.Combine(Path.GetDirectoryName(path)!, "ai-cover-background.png"); image.Save(localBackground, System.Drawing.Imaging.ImageFormat.Png); copy.CoverBackground = localBackground;copy.CoverTitleRendered=scenePrompt==null&&copy.CoverTitleStyle=="3D Telkari Altın"&&LocalCoverEngine.ModelOf(settings.CoverLocalWorkflow)=="FLUX.2";
                    log("YEREL MOTOR • Görsel hazır; yalnızca video başlığı ekleniyor.");
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested) {
                    if(scenePrompt != null)throw new InvalidOperationException("Yerel hikâye görseli üretilemedi. Hazır referans ve sahneler saklandı; bulut API kullanılmadı. "+ex.Message,ex);
                    log("YEREL MOTOR • " + (ex is InvalidOperationException ? ex.Message : "Bağlantı veya üretim tamamlanamadı (" + ex.GetType().Name + ").") + " Yerel üretim tamamlanamadı; neden işlem kaydında gösterildi.");
                }
            }
            if(settings.CoverLocalEnabled && scenePrompt!=null)await GenerateLocal();
            if(scenePrompt != null && settings.CoverLocalEnabled && copy.CoverBackground != "") {
                File.Copy(copy.CoverBackground,path,true);log("Yerel hikâye sahnesi hazır.");return;
            }
            foreach (var attempt in settings.CoverProviderOrder.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Concat(new[] { "NVIDIA", "Gemini", "OpenAI" }).Distinct().SelectMany(p => ModelsFor(p, settings).Select(m => (Provider: p, Model: m))).Where(_ => copy.CoverBackground == ""))
            {
                var provider = attempt.Provider; var model = attempt.Model;
                if (referenceImage!=null && provider == "NVIDIA") { log("NVIDIA atlandı: mevcut bağlantı referans görsel kabul etmiyor. Referans destekleyen bulut modeli veya yerel motor kullanılacak."); continue; }
                if (provider != "NVIDIA" && (settings.FreeOnly || !settings.CoverPaidAllowed)) { log(provider + " • " + model + " atlandı: ücretli görsel API izni kapalı veya ücretsiz öncelik açık."); continue; }
                var key = store.Key(provider == "NVIDIA" ? "NVIDIA_API_KEY" : provider == "Gemini" ? "GEMINI_API_KEY" : "OPENAI_API_KEY"); if (key == "") { log(provider + " görsel anahtarı yok; alternatif deneniyor."); continue; }
                ct.ThrowIfCancellationRequested();
                if (!Reserve(settings)) { log("Bulut yapay zekâ denemesi başlamadı: aylık " + settings.CoverMonthlyRequests + " istek sınırı doldu. Yerel motor bu kotayı kullanmaz; yerel motoru etkinleştir veya aylık API istek sınırını ayarla."); break; }
                try
                {
                    log(provider + " • " + model + " kapak tasarlıyor…");
                    var prompt = $"Create a professional YouTube cover BACKGROUND, no text, no lettering, no logos. Style: {copy.CoverStyle}. Subject: {topic[..Math.Min(topic.Length, 1000)]}. Clear main subject on right, dark negative space on left for readable title. {(vertical ? "Portrait" : "Landscape")} composition. Do not depict fabricated scripture text or misleading factual evidence.";
                    if (copy.CoverStyle.StartsWith("Nur Kapısı")) prompt += " Premium cinematic Islamic channel visual identity: deep midnight navy, warm amber lantern light, rich metallic gold accents, realistic dramatic sky and volumetric light. Use a mosque skyline or a respectfully placed closed unlettered book only when relevant to the topic; for habits or motivation use a serene human silhouette or path. Leave uncluttered dark space for a large gold serif headline. No Arabic writing, no invented Quran pages, no busy collage.";
                    if (!string.IsNullOrWhiteSpace(copy.CoverDesignInstructions)) prompt += " Optional visual direction: " + copy.CoverDesignInstructions[..Math.Min(2500,copy.CoverDesignInstructions.Length)] + ". Keep the image free of all text and logos; the approved title is added separately.";
                    if (scenePrompt != null) prompt = scenePrompt;
                    else if(styleReference)prompt+=" Use the attached previous cover only as a style reference: match palette, lighting, cinematic mood and hierarchy. Create the NEW topic, not its old people or objects. Never reproduce old text or logo; output no letters or text.";
                    byte[] bytes;
                    if (!Regex.IsMatch(model, "^[a-zA-Z0-9._/-]+$")) throw new InvalidOperationException("Geçersiz model");
                    using var req = new HttpRequestMessage(HttpMethod.Post, provider switch {
                        "NVIDIA" => "https://ai.api.nvidia.com/v1/genai/" + model,
                        "OpenAI" => referenceImage == null ? "https://api.openai.com/v1/images/generations" : "https://api.openai.com/v1/images/edits",
                        _ => "https://generativelanguage.googleapis.com/v1beta/models/" + Uri.EscapeDataString(model) + ":generateContent"
                    });
                    req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    if (provider == "NVIDIA") {
                        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
                        req.Content = model == "stabilityai/stable-diffusion-xl"
                            ? Body(new { text_prompts = new[] { new { text = prompt, weight = 1 } }, width = 1024, height = 1024, cfg_scale = 5, samples = 1, seed = 0, steps = 25 })
                            : Body(new { prompt, width = 1024, height = 1024, cfg_scale = 0, mode = "base", samples = 1, seed = 0, steps = 4 });
                    }
                    else if (provider == "OpenAI") {
                        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
                        if (referenceImage == null) req.Content = Body(new { model, prompt, n = 1, size = vertical ? "1024x1536" : "1536x1024", quality = "low" });
                        else {
                            var form = new MultipartFormDataContent(); form.Add(new StringContent(model),"model"); form.Add(new StringContent(prompt),"prompt");
                            form.Add(new StringContent(vertical ? "1024x1536" : "1536x1024"),"size"); form.Add(new StringContent("low"),"quality");
                            var reference = new ByteArrayContent(await File.ReadAllBytesAsync(referenceImage,ct)); reference.Headers.ContentType = new("image/png"); form.Add(reference,"image","characters.png"); req.Content=form;
                        }
                    }
                    else {
                        req.Headers.Add("x-goog-api-key", key);
                        var parts = new List<object> { new { text = prompt } };
                        if(referenceImage != null) parts.Add(new { inlineData = new { mimeType="image/png", data=Convert.ToBase64String(await File.ReadAllBytesAsync(referenceImage,ct)) } });
                        req.Content = Body(new { contents = new[] { new { parts } }, generationConfig = new { responseModalities = new[] { "TEXT", "IMAGE" } } });
                    }
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(90));
                    using var response = await (client ?? Http).SendAsync(req, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                    if (!response.IsSuccessStatusCode) throw new ProviderException(provider, (int)response.StatusCode);
                    if ((response.Content.Headers.ContentLength ?? 0) > 32_000_000) throw new InvalidOperationException("Görsel yanıtı çok büyük");
                    await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token); using var data = new MemoryStream(); var buffer = new byte[81920]; int read;
                    while ((read = await stream.ReadAsync(buffer, timeout.Token)) > 0) { if (data.Length + read > 32_000_000) throw new InvalidOperationException("Görsel yanıtı çok büyük"); data.Write(buffer, 0, read); }
                    using var doc = JsonDocument.Parse(data.ToArray());
                    if (provider == "NVIDIA") bytes = Convert.FromBase64String(doc.RootElement.GetProperty("artifacts")[0].GetProperty("base64").GetString()!);
                    else if (provider == "OpenAI") bytes = Convert.FromBase64String(doc.RootElement.GetProperty("data")[0].GetProperty("b64_json").GetString()!);
                    else { var image = doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts").EnumerateArray().First(x => x.TryGetProperty("inlineData", out _)); bytes = Convert.FromBase64String(image.GetProperty("inlineData").GetProperty("data").GetString()!); }
                    using var imageStream = new MemoryStream(bytes); using var generated = Image.FromStream(imageStream);
                    if ((long)generated.Width * generated.Height > 40_000_000 || generated.Width < 256 || generated.Height < 256) throw new InvalidOperationException("Görsel ölçüsü uygun değil");
                    var background = Path.Combine(Path.GetDirectoryName(path)!, "ai-cover-background.png"); generated.Save(background, System.Drawing.Imaging.ImageFormat.Png); copy.CoverBackground = background;
                    log(provider + " • " + model + " görseli hazır; başlık ekleniyor."); break;
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested) { log(provider + " • " + model + " süre sınırı; sıradaki model deneniyor."); }
                catch (Exception ex) when (ex is not OperationCanceledException) { if(ex is ProviderException {Status:404 or 429}){ReleaseRejectedRequest();log("Sağlayıcının reddettiği istek yerel üretim sayacından çıkarıldı; sağlayıcı kotası ayrıca geçerlidir.");} log(provider + " • " + model + ": " + (ex is ProviderException ? ex.Message : "Görsel yanıtı kullanılamadı (" + ex.GetType().Name + ").") + " Sıradaki model deneniyor."); }
            }
            if(scenePrompt==null && settings.CoverLocalEnabled && copy.CoverBackground==""){log("Bulut modelleri kapak üretemedi; yerel motora geçiliyor.");await GenerateLocal();}
        }
        ct.ThrowIfCancellationRequested();
        if (scenePrompt != null) {
            if (copy.CoverBackground == "") throw new InvalidOperationException("Hikâye sahnesi için AI görseli üretilemedi. Yerel görsel motorunu veya görsel API anahtarlarını / kullanım izinlerini kontrol et. Hazır sahneler korunuyor; stok fotoğraf ya da kapak şablonuyla değiştirilmedi.");
            File.Copy(copy.CoverBackground, path, true);
            log("Hikâye sahnesinin özgün AI görseli hazır.");
            return;
        }
        if (copy.CoverAiEnabled && copy.CoverBackground == "") { copy.CoverStyle = CoverDesigner.Suggest(topic); log("Kullanılabilir yapay zekâ modelleri görsel üretemedi veya API sınırı nedeniyle denemeler durdu. Konuya göre otomatik kapak seçiliyor."); }
        try { CoverDesigner.Save(path, copy, title, vertical); log(copy.CoverBackground == "" ? "Şablon kapak hazır • AI görsel üretilmedi." : "Görselli kapak tasarımı hazır."); }
        catch (Exception) { log("Kapak bileşeni okunamadı; sade yerel güvenli tasarım deneniyor."); CoverDesigner.Save(path, new Settings { ChannelName = settings.ChannelName, CoverStyle = "Minimal" }, title, vertical); }
    }
    static StringContent Body(object body) => new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
}
