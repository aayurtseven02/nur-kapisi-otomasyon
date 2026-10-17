using System.Net.Http.Json;
using System.Text;

namespace AnkaStudio;

public sealed class XttsTts(HttpClient? client = null)
{
    static readonly HttpClient Shared = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromMinutes(3) };
    public const string Help = "Harici XTTS v2: Model TubeFlow ile dağıtılmaz ve otomatik indirilmez.\n1. Kendi bilgisayarında daswer123/xtts-api-server uyumlu hizmeti kur ve çalıştır.\n2. Sunucu adresini gir; yerel varsayılan http://127.0.0.1:8020. Uzak bağlantıda HTTPS kullan.\n3. Konuşmacı adını veya SUNUCUDA bulunan referans WAV dosyasının yolunu gir. Buradaki alan ses dosyasını sunucuya yüklemez.\n4. Örnek sesi dinle; ardından normal video veya Shorts sağlayıcısını XTTS seç.\nSunucu /tts_to_audio/ adresine text, speaker_wav, language alanlarını kabul edip WAV dönmelidir. Farklı XTTS sunucuları doğrudan uyumlu olmayabilir.\nXTTS çalışmazsa başka sağlayıcıyı seçip üretime devam edebilirsin; sesin habersiz değiştirilmez.\nModelin CPML lisansı model ve çıktıları için ticari olmayan kullanımı kapsar. Gelir amaçlı kullanım için uygun ayrı izin gerekir. Kendi sesini veya sahibinin izin verdiği sesi kullan.\nKurulum: https://github.com/daswer123/xtts-api-server\nLisans: https://huggingface.co/coqui/XTTS-v2/blob/main/LICENSE.txt";
    public static Uri Endpoint(Settings settings)
    {
        if (!Uri.TryCreate(settings.XttsUrl.Trim().TrimEnd('/') + "/tts_to_audio/", UriKind.Absolute, out var uri) ||
            !(uri.Scheme == "https" || uri.Scheme == "http" && uri.IsLoopback) || uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0)
            throw new InvalidOperationException("XTTS adresi yerel HTTP veya uzak HTTPS adresi olmalıdır.");
        if (string.IsNullOrWhiteSpace(settings.XttsSpeaker)) throw new InvalidOperationException("XTTS konuşmacı adını veya sunucudaki referans WAV yolunu gir.");
        return uri;
    }
    public async Task Speak(string text, string path, Settings settings, CancellationToken ct)
    {
        var uri = Endpoint(settings);
        using var response = await (client ?? Shared).PostAsJsonAsync(uri, new { text, speaker_wav = settings.XttsSpeaker.Trim(), language = "tr" }, ct);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"XTTS seslendirme başarısız (HTTP {(int)response.StatusCode}). Sunucuyu ve konuşmacıyı kontrol et.");
        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        if (bytes.Length < 44 || Encoding.ASCII.GetString(bytes, 0, 4) != "RIFF" || Encoding.ASCII.GetString(bytes, 8, 4) != "WAVE")
            throw new InvalidOperationException("XTTS sunucusu geçerli WAV sesi döndürmedi.");
        await File.WriteAllBytesAsync(path, bytes, ct);
    }
}
