using System.Security.Cryptography;

using System.Text;

using System.Text.Json;



namespace AnkaStudio;



public static class Reliability

{

    public const string PipelineVersion = "reliable-render-v5-snapshots";

    public static string MediaPlanHash(Episode episode, string? key = null)

    {

        var scripts = Rules.Scripts(episode).Where(item => key == null || item.Key == key).Select(item => new { item.Key, item.Short, item.Script.Title, item.Script.Description,

            item.Script.ThumbnailText, item.Script.Tags, item.Script.TextLayout, item.Script.MusicTrack, item.Script.CoverSettings,

            scenes = item.Script.Scenes.Select(s => new { s.Narration, s.SubtitleText, s.VisualQuery, s.OnScreen, s.TitlePlacement, s.TitleStart, s.TitleDuration, s.StockFile, s.Duration, s.AiImageHash, s.AiImagePrompt, s.AiCharacterIds }) });

        var raw = JsonSerializer.Serialize(scripts, Json.Options);
        if(episode.PhotosOnly)raw += "|stock-photos-v1";
        if(episode.AiStoryImages)raw += JsonSerializer.Serialize(new { episode.AiStoryImages, episode.AiStoryStyle, episode.AiStoryCharacters, episode.AiStoryMotion, episode.AiStoryCinematic, CinemaVersion=StoryCinema.Version, episode.AiStoryLocal });

        if (episode.ProductionSettings != null) raw += JsonSerializer.Serialize(episode.ProductionSettings, Json.Options);
        if (episode.ArtworkSettings != null) raw += JsonSerializer.Serialize(episode.ArtworkSettings, Json.Options);

        if (episode.QueueSettings != null) raw += JsonSerializer.Serialize(new { episode.QueueSettings, episode.QueueLayout, episode.QueueMusic }, Json.Options);

        if (episode.MusicClip != null) raw += JsonSerializer.Serialize(new { episode.MusicClip, MainAudioHash = File.Exists(episode.QueueMusic?.File) ? FileHash(episode.QueueMusic!.File) : episode.MusicClip.AudioHash }, Json.Options);

        if (UserNarration.Active(episode) || ManualVideos.Active(episode)) raw += JsonSerializer.Serialize(episode.UserNarration, Json.Options);

        if(episode.Creation is {CoverChoice:not "legacy"} workflow)raw+=JsonSerializer.Serialize(new{workflow.Voice,workflow.AudioHash,SourceAudioHash=FileHash(workflow.AudioFile),workflow.Fit,workflow.PhotoMotion},Json.Options);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));

    }

    public static string FileHash(string path)

    {

        if (!File.Exists(path)) return "";

        using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream));

    }

    public static string RenderHash(VideoScript script, Episode episode, Settings settings, string pipelineVersion = PipelineVersion)

    {

        settings = VideoSettings.For(settings,episode,script);

        var design = new { script, episode.Demo, episode.Music, episode.AiStoryImages, episode.AiStoryStyle, episode.AiStoryCharacters, episode.AiStoryMotion, episode.AiStoryCinematic, CinemaVersion=StoryCinema.Version, episode.AiStoryLocal, localImageWorkflow=(episode.AiStoryImages && episode.AiStoryLocal || settings.CoverLocalEnabled)?FileHash(File.Exists(settings.CoverLocalWorkflow)?settings.CoverLocalWorkflow:LocalCoverEngine.FindInstalledWorkflow()):"", settings.FramesPerSecond, settings.EncodingPreset, settings.Crf,

            settings.LongFontSize, settings.ShortFontSize, settings.SubtitleColor, settings.ChannelName,

            settings.ShortVoiceOverride, settings.ShortVoiceProvider, settings.ShortVoiceId, settings.ShortVoiceSpeed, settings.ShortMusicVolume,

            voice = NarrationFlow.Fingerprint(script, settings), settings.MusicVolume, musicFileHash = FileHash(script.MusicTrack?.File ?? settings.MusicPath),

            settings.OpeningCardEnabled, settings.OpeningCardSeconds, openingCard = FileHash(settings.OpeningCardFile),

            settings.ClosingCardEnabled, settings.ClosingCardSeconds, closingCard = FileHash(settings.ClosingCardFile),

            settings.VideoLogoEnabled, settings.VideoLogoPosition, settings.VideoLogoWidthPercent, videoLogo = FileHash(settings.VideoLogo),

            artwork = WorkflowArtwork.Signature(settings), settings.CoverAutoStyle, settings.CoverStyle, settings.CoverMode, settings.CoverFont, settings.CoverLogoPosition, settings.CoverAccent,

            background = FileHash(settings.CoverBackground), referenceCover=FileHash(settings.CoverReferenceImage), logo = FileHash(settings.CoverLogo), overlay = FileHash(settings.CoverPng),

            media = script.Scenes.Select(s => FileHash(s.StockFile)).ToArray() };

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(pipelineVersion + JsonSerializer.Serialize(design, Json.Options)+(episode.Creation is {CoverChoice:not "legacy"} flow?JsonSerializer.Serialize(new{flow.CoverChoice,flow.Voice,AudioHash=FileHash(flow.AudioFile),flow.Fit}):""))));

    }

    public static bool AssetCurrent(Episode episode, AssetVideo asset, bool checkFile = true)

        => asset.QualityPassed && episode.Plan != null && asset.PlanHash == MediaPlanHash(episode, asset.Key)

            && File.Exists(asset.File) && new FileInfo(asset.File).Length > 0 && asset.FileHash != ""

            && (asset.PipelineVersion == "" || asset.PipelineVersion == PipelineVersion)

            && (!checkFile || FileHash(asset.File) == asset.FileHash && RenderCurrent(episode,asset));

    static bool RenderCurrent(Episode episode, AssetVideo asset) {

        if(episode.ProductionSettings==null || asset.RenderFingerprint=="") return true;

        var script=Rules.Scripts(episode).FirstOrDefault(x=>x.Key==asset.Key).Script;

        return script!=null && asset.RenderFingerprint==RenderHash(script,episode,episode.ProductionSettings);

    }

    public static bool OutputsProduced(Episode episode)

    {

        var expected = Rules.Scripts(episode).Select(s => s.Key).ToArray();

        return !episode.TextDesignPending && !episode.ArtworkPending && expected.Length > 0 && episode.Videos.Count == expected.Length

            && expected.All(key => episode.Videos.Count(v => v.Key == key && v.PlanHash == MediaPlanHash(episode, key)

                && File.Exists(v.File) && new FileInfo(v.File).Length > 0 && v.FileHash != "") == 1);

    }

    public static bool OutputsCurrent(Episode episode, bool checkFile = true)

    {

        var expected = Rules.Scripts(episode).Select(s => s.Key).ToArray();

        return !episode.TextDesignPending && !episode.ArtworkPending && expected.Length > 0 && episode.Videos.Count == expected.Length

            && expected.All(key => episode.Videos.Count(v => v.Key == key && AssetCurrent(episode, v, checkFile)) == 1);

    }

    public static async Task ValidateVideo(Store store, string path, CancellationToken ct, bool complete = false)

    {

        if (!File.Exists(path) || new FileInfo(path).Length == 0) throw new InvalidOperationException("Video dosyası boş veya eksik.");

        var raw = await Media.Exec(store.Probe, ["-v", "error", "-show_streams", "-show_format", "-of", "json", path], null, ct);

        using var doc = JsonDocument.Parse(raw);

        if (!doc.RootElement.GetProperty("streams").EnumerateArray().Any(s => s.TryGetProperty("codec_type", out var type) && type.GetString() == "video" && s.GetProperty("width").GetInt32() > 0 && s.GetProperty("height").GetInt32() > 0))

            throw new InvalidOperationException("Dosyada geçerli video akışı yok.");

        _ = await Media.Duration(store, path, ct);

        if (complete && !doc.RootElement.GetProperty("streams").EnumerateArray().Any(s => s.TryGetProperty("codec_type", out var type) && type.GetString() == "audio"))

            throw new InvalidOperationException("Tamamlanmış video çıktısında ses akışı yok.");

        var decode = new List<string> { "-v", "error", "-xerror", "-i", path };

        if (!complete) decode.AddRange(["-t", "1"]);

        decode.AddRange(["-map", "0:v:0", "-map", "0:a?", "-f", "null", "-"]);

        await Media.Exec(store.Ffmpeg, decode, null, ct);

    }

    public static async Task CopyBounded(Stream input, Stream output, long limit, CancellationToken ct)

    {

        var buffer = new byte[81920]; long total = 0; int read;

        while ((read = await input.ReadAsync(buffer, ct)) > 0) {

            total += read; if (total > limit) throw new InvalidOperationException("İndirme güvenli boyut sınırını aştı.");

            await output.WriteAsync(buffer.AsMemory(0, read), ct);

        }

    }

    public static async Task DownloadResponse(HttpResponseMessage response, string path, long limit, CancellationToken ct)

    {

        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength > limit) throw new InvalidOperationException("İndirme boyut sınırını aşıyor.");

        var temp = path + "." + Guid.NewGuid().ToString("N") + ".part";

        try {

            var volume = Path.GetPathRoot(Path.GetFullPath(path));

            if (volume != null && new DriveInfo(volume).AvailableFreeSpace < (response.Content.Headers.ContentLength ?? limit) + 100L * 1024 * 1024) throw new InvalidOperationException("İndirme için yeterli boş disk alanı yok.");

            await using (var input = await response.Content.ReadAsStreamAsync(ct)) await using (var output = File.Create(temp)) { await CopyBounded(input, output, limit, ct); await output.FlushAsync(ct); }

            ct.ThrowIfCancellationRequested(); File.Move(temp, path, true);

        } finally { if (File.Exists(temp)) File.Delete(temp); }

    }

    public static async Task<string> ReadText(HttpResponseMessage response, int limit, CancellationToken ct)

    {

        if (response.Content.Headers.ContentLength > limit) throw new InvalidOperationException("Hizmet yanıtı boyut sınırını aşıyor.");

        await using var stream = await response.Content.ReadAsStreamAsync(ct); using var buffer = new MemoryStream(); await CopyBounded(stream, buffer, limit, ct);

        return Encoding.UTF8.GetString(buffer.ToArray());

    }

    public static void Failed(Episode episode, Exception error, string stage)

    {

        episode.ResumeStage = stage; episode.RetryAttempts++;

        var permanent = error is AiUnavailableException ai ? !ai.Retryable : error is TimedNarration.TimingUnavailableException || error is CloudUsage.LimitException || error.Message.Contains("HTTP 402") || error.Message.Contains("HTTP 401") || error.Message.Contains("HTTP 403") || error.Message.Contains("anahtarı") || error.Message.Contains("lisans") || error.Message.Contains("Günlük üretim sınırı");

        episode.RetryAt = permanent || episode.RetryAttempts > 5 ? null : DateTimeOffset.UtcNow.AddSeconds(Math.Min(900, 15 * Math.Pow(2, episode.RetryAttempts - 1)));

        episode.Status = "Bekliyor"; episode.Error = error.Message;

        episode.Log(episode.RetryAt == null ? "Kullanıcı kontrolü gerekli; otomatik tekrar durduruldu." : $"Geçici hata; yeniden deneme: {episode.RetryAt.Value.ToOffset(TimeSpan.FromHours(3)):HH:mm:ss} ({episode.RetryAttempts}/5).");

    }

}



public sealed class OperationLease : IDisposable

{

    readonly Store store; bool disposed;

    public OperationLease(Store store) { this.store = store; lock (store.OperationGate) { if (store.OperationBusy) throw new InvalidOperationException("Başka bir işlem çalışıyor. Tamamlanmasını bekle."); store.OperationBusy = true; } }

    public void Dispose() { if (!disposed) { lock (store.OperationGate) store.OperationBusy = false; disposed = true; } }

}

