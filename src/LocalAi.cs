using System.Diagnostics;

using System.IO.Compression;

using System.Net.Http.Json;

using System.Security.Cryptography;

using System.Text.Json;



namespace AnkaStudio;



public sealed record LocalModel(string Id, string Label, long DownloadBytes);

public sealed record LocalAiState(bool Running, string[] Models, string Note);

public sealed class LocalAiSetupException(string message) : InvalidOperationException(message);



public sealed class LocalAi(Store store, HttpClient? client = null, Func<string?>? executableLocator = null)

{

    public static readonly LocalModel[] Catalog = [

        new("qwen3:1.7b", "Qwen3 1.7B • 1,4 GB • en hafif; doğruluk daha sınırlı", 1_400_000_000),

        new("qwen3:4b", "Qwen3 4B • 2,5 GB • hafif başlangıç önerisi", 2_500_000_000),

        new("qwen3:8b", "Qwen3 8B • 5,2 GB • daha fazla bellek; daha güçlü seçenek", 5_200_000_000),
        new("qwen3-vl:4b-instruct", "Qwen3-VL 4B • 3,3 GB • fotoğraf/video karelerini okur • yerel/ücretsiz",3_300_000_000)

    ];

    internal static readonly HttpClient Shared = new() { Timeout = Timeout.InfiniteTimeSpan };

    readonly HttpClient http = client ?? Shared;

    string Root => Path.Combine(store.Root, "local-ai");

    string Runtime => Path.Combine(Root, "runtime");

    public string? Executable()

    {

        if (executableLocator != null) return executableLocator();

        foreach (var path in new[] { Path.Combine(Runtime, "ollama.exe"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Ollama", "ollama.exe") })

            if (File.Exists(path)) return path;

        foreach (var folder in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator)) {

            try { var path = Path.Combine(folder.Trim('"'), "ollama.exe"); if (File.Exists(path)) return Path.GetFullPath(path); } catch (ArgumentException) { }

        }

        return null;

    }

    public async Task<LocalAiState> Status(CancellationToken ct)

    {

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(5));

        try {

            using var res = await http.GetAsync("http://127.0.0.1:11434/api/tags", HttpCompletionOption.ResponseHeadersRead, timeout.Token);

            res.EnsureSuccessStatusCode(); using var doc = JsonDocument.Parse(await Reliability.ReadText(res, 2_000_000, timeout.Token));

            var models = doc.RootElement.GetProperty("models").EnumerateArray().Select(m => m.GetProperty("name").GetString() ?? "").Where(IsLocalName).Distinct().ToArray();

            return new(true, models, "Yerel servis açık • " + models.Length + " yerel model kurulu.");

        } catch (Exception ex) when (ex is HttpRequestException or JsonException or KeyNotFoundException or InvalidOperationException or OperationCanceledException && !ct.IsCancellationRequested) {

            return new(false, [], Executable() == null ? "Ollama kurulu değil. Yerel motoru yükle düğmesini kullan." : "Ollama kurulu; yerel servis kapalı. Başlat düğmesini kullan.");

        }

    }

    public async Task Start(CancellationToken ct)

    {

        if ((await Status(ct)).Running) return;

        var exe = Executable() ?? throw new LocalAiSetupException("Önce Yerel motoru yükle düğmesiyle Ollama'yı yükle.");

        var info = new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(exe)! };

        info.ArgumentList.Add("serve"); info.Environment["OLLAMA_HOST"] = "127.0.0.1:11434";

        info.Environment["OLLAMA_NO_CLOUD"] = "1";

        if (Path.GetDirectoryName(exe) == Runtime) { var models = Path.Combine(Root, "models"); Directory.CreateDirectory(models); info.Environment["OLLAMA_MODELS"] = models; }

        using var process = Process.Start(info) ?? throw new InvalidOperationException("Yerel motor başlatılamadı.");

        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);

        while (DateTimeOffset.UtcNow < deadline) { ct.ThrowIfCancellationRequested(); await Task.Delay(500, ct); if ((await Status(ct)).Running) return; if (process.HasExited) throw new InvalidOperationException("Ollama servisi kapandı; port veya ekran kartı sürücüsünü kontrol et."); }

        throw new InvalidOperationException("Yerel motor zamanında açılamadı. Durumu yenile ve yeniden dene.");

    }

    public static void ValidateModel(string model)

    {

        if (!Catalog.Any(m => m.Id == model)) throw new InvalidOperationException("Model indirmek için listeden önerilen bir yerel model seç.");

    }

    public static bool IsLocalName(string model) => !string.IsNullOrWhiteSpace(model) && model.Length <= 150 && !model.Any(char.IsWhiteSpace) && !model.EndsWith(":cloud", StringComparison.OrdinalIgnoreCase) && !model.EndsWith("-cloud", StringComparison.OrdinalIgnoreCase);

    public async Task EnsureLocal(string model, CancellationToken ct)

    {

        if (!IsLocalName(model)) throw new LocalAiSetupException("Bulut Ollama modeli kapalı; bir yerel model seç.");

        using var req = new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1:11434/api/show") { Content = JsonContent.Create(new { model }) };

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(15));

        using var res = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, timeout.Token);

        if (!res.IsSuccessStatusCode) throw new LocalAiSetupException("Ollama modeli kurulu değil veya okunamadı: " + model + ". Yerel AI panelinden modeli yükle.");

        using var doc = JsonDocument.Parse(await Reliability.ReadText(res, 2_000_000, timeout.Token));

        foreach (var key in new[] { "remote_model", "remote_host" }) if (doc.RootElement.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())) throw new LocalAiSetupException("Bu Ollama modeli uzaktaki bulut hizmetini kullanıyor; yerel AI için kabul edilmedi.");

    }

    public async Task Pull(string model, IProgress<string> progress, CancellationToken ct)

    {

        ValidateModel(model); await Start(ct);

        if ((await Status(ct)).Models.Contains(model)) { store.ProviderBlocked.Remove("Ollama"); progress.Report("Model zaten kurulu; yeniden indirilmedi."); return; }

        var expected = Catalog.Single(m => m.Id == model).DownloadBytes;

        var modelRoot = Path.GetDirectoryName(Executable()) == Runtime ? Path.Combine(Root, "models") : Environment.GetEnvironmentVariable("OLLAMA_MODELS") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ollama");

        var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(modelRoot))!);

        if (drive.AvailableFreeSpace < expected * 2 + 500_000_000) throw new InvalidOperationException("Model indirmesi için yeterli boş disk alanı yok.");

        using var req = new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1:11434/api/pull") { Content = JsonContent.Create(new { model, stream = true }) };

        using var res = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct); res.EnsureSuccessStatusCode();

        await using var stream = await res.Content.ReadAsStreamAsync(ct); using var reader = new StreamReader(stream);

        bool success = false;

        await foreach (var line in Lines(reader, ct)) {

            using var doc = JsonDocument.Parse(line); var root = doc.RootElement;

            if (root.TryGetProperty("error", out var error)) throw new InvalidOperationException("Ollama indirmesi: " + error.GetString());

            var status = root.TryGetProperty("status", out var value) ? value.GetString() ?? "İndiriliyor" : "İndiriliyor";

            if (root.TryGetProperty("total", out var total) && total.TryGetInt64(out var bytes) && bytes > 0 && root.TryGetProperty("completed", out var completed) && completed.TryGetInt64(out var done)) status = $"Model indiriliyor • %{Math.Clamp(done * 100d / bytes, 0, 100):0} • {done / 1_000_000} / {bytes / 1_000_000} MB";

            progress.Report(status); success |= status == "success";

        }

        if (!success || !(await Status(ct)).Models.Contains(model)) throw new InvalidOperationException("Model indirmesi tamamlanmadı. Model seçimi değiştirilmedi; yeniden deneyebilirsin.");

        store.ProviderBlocked.Remove("Ollama"); progress.Report("Model hazır: " + model);

    }

    static async IAsyncEnumerable<string> Lines(StreamReader reader, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)

    {

        var buffer = new char[4096]; var line = new System.Text.StringBuilder(); int count;

        while ((count = await reader.ReadAsync(buffer.AsMemory(), ct)) > 0) {

            for (int i = 0; i < count; i++) {

                if (buffer[i] == '\n') { if (line.Length > 0) yield return line.ToString().TrimEnd('\r'); line.Clear(); }

                else { if (line.Length >= 65536) throw new InvalidOperationException("Model indirme yanıtı güvenli sınırı aştı."); line.Append(buffer[i]); }

            }

        }

        if (line.Length > 0) yield return line.ToString().TrimEnd('\r');

    }

    public static void ValidateDigest(string path, string digest)

    {

        if (!digest.StartsWith("sha256:", StringComparison.Ordinal) || digest.Length != 71 || !Reliability.FileHash(path).Equals(digest[7..], StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Ollama indirmesinin resmi SHA-256 doğrulaması başarısız; çalıştırılmadı.");

    }

    public static void ExtractRuntime(string zip, string folder, CancellationToken ct)

    {

        Directory.CreateDirectory(folder); var prefix = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        using var archive = ZipFile.OpenRead(zip); long total = 0;

        foreach (var entry in archive.Entries) {

            ct.ThrowIfCancellationRequested(); total += entry.Length; if (total > 16L * 1024 * 1024 * 1024) throw new InvalidOperationException("Yerel motor arşivi boyut sınırını aştı.");

            if (entry.FullName.Contains(':') || Path.IsPathRooted(entry.FullName)) throw new InvalidOperationException("Arşiv dosya yolu geçersiz.");

            var path = Path.GetFullPath(Path.Combine(folder, entry.FullName)); if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Arşiv klasör dışına dosya yazamaz.");

            if (entry.FullName.EndsWith('/')) { Directory.CreateDirectory(path); continue; }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!); using var input = entry.Open(); using var output = File.Create(path); Reliability.CopyBounded(input, output, entry.Length, ct).GetAwaiter().GetResult();

        }

    }

    public async Task Install(IProgress<string> progress, CancellationToken ct)

    {

        if ((await Status(ct)).Running || Executable() != null) { progress.Report("Kurulu Ollama bulundu; tekrar indirilmedi."); await Start(ct); return; }

        Directory.CreateDirectory(Root); var stage = Path.Combine(Root, "install-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(stage);

        try {

            if (new DriveInfo(Path.GetPathRoot(Root)!).AvailableFreeSpace < 20L * 1024 * 1024 * 1024) throw new InvalidOperationException("Yerel motor kurulumu için en az 20 GB geçici boş alan gerekli.");

            progress.Report("Resmi Ollama sürümü kontrol ediliyor...");

            using var metadata = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/repos/ollama/ollama/releases/latest"); metadata.Headers.UserAgent.ParseAdd("AnkaStudio/0.2.16");

            using var info = await http.SendAsync(metadata, HttpCompletionOption.ResponseHeadersRead, ct); info.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await Reliability.ReadText(info, 2_000_000, ct));

            var assets = doc.RootElement.GetProperty("assets").EnumerateArray().Where(a => a.GetProperty("name").GetString() is "ollama-windows-amd64.zip" or "ollama-windows-amd64-vulkan.zip").Select(a => a.Clone()).ToArray();

            if (!assets.Any(a => a.GetProperty("name").GetString() == "ollama-windows-amd64.zip")) throw new InvalidOperationException("Resmi Windows motor arşivi bulunamadı.");

            var unpack = Path.Combine(stage, "runtime"); Directory.CreateDirectory(unpack);

            foreach (var asset in assets) {

                var name = asset.GetProperty("name").GetString()!; var url = asset.GetProperty("browser_download_url").GetString()!;

                if (!url.StartsWith("https://github.com/ollama/ollama/releases/download/", StringComparison.Ordinal) || !asset.TryGetProperty("digest", out var digest) || digest.ValueKind != JsonValueKind.String) throw new InvalidOperationException("Resmi indirme veya SHA-256 bilgisi doğrulanamadı.");

                progress.Report("Yerel motor indiriliyor: " + name + " • model ayrı indirilir.");

                var path = Path.Combine(stage, name); using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);

                await Reliability.DownloadResponse(response, path, 4L * 1024 * 1024 * 1024, ct); await Task.Run(() => ValidateDigest(path, digest.GetString()!), ct);

                await Task.Run(() => ExtractRuntime(path, unpack, ct), ct);

            }

            if (!File.Exists(Path.Combine(unpack, "ollama.exe"))) throw new InvalidOperationException("Motor arşivinde ollama.exe yok.");

            ct.ThrowIfCancellationRequested(); Directory.Move(unpack, Runtime);

            Json.Write(Path.Combine(Root, "runtime-source.json"), new { Version = doc.RootElement.GetProperty("tag_name").GetString(), Source = "https://github.com/ollama/ollama", Installed = DateTimeOffset.UtcNow });

            progress.Report("Yerel motor kuruldu; başlatılıyor..."); await Start(ct);

        } finally { if (Directory.Exists(stage)) Directory.Delete(stage, true); }

    }

}

