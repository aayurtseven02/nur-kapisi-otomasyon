using System.Text.Json;

namespace AnkaStudio;



public static class VideoQueue

{

    public static bool DeferExpired(Episode episode,DateTimeOffset now)
    {
        if(!episode.DeferMissedPublication || episode.PublishAt==null || episode.PublishAt>now.AddMinutes(10) || episode.Videos.Any(v=>v.Scheduled))return false;
        episode.PublishAt=now.AddMinutes(30);int shortIndex=0;
        foreach(var video in episode.Videos.OrderBy(v=>v.Short).ThenBy(v=>v.Key)){video.PublishAt=Rules.VideoDate(episode,video.Short,shortIndex);if(video.Short)shortIndex++;}
        episode.Log("Önceden seçilmiş erteleme kuralıyla yayın 30 dakika ileri alındı; Shorts aralıkları korundu.");return true;
    }
    public static void RequireYouTube(Store store)

    {

        if (store.Settings.YouTubeChannelId == "" || store.Key("YOUTUBE_REFRESH_TOKEN") == "")

            throw new InvalidOperationException("Bağlantı Merkezi’nde YouTube hesabını bağla ve kanalını doğrula.");

    }



    static readonly string[] Fields = "ChannelName,VoiceProvider,GoogleTtsVoice,GoogleTtsFallback,WindowsVoice,PiperPath,PiperModel,XttsUrl,XttsSpeaker,VoiceId,VoiceModel,VoiceSpeed,ShortVoiceOverride,ShortVoiceProvider,ShortVoiceId,ShortVoiceSpeed,MusicPath,LibraryMusic,MusicVolume,ShortMusicVolume,MusicStyle,CoverReferenceImage,CoverAiEnabled,CoverPaidAllowed,CoverProviderOrder,CoverNvidiaModel,CoverGeminiModel,CoverOpenAiModel,CoverMonthlyRequests,CoverStyle,CoverAutoStyle,CoverMode,CoverPng,CoverBackground,CoverLogo,CoverFont,CoverLogoPosition,CoverAccent,OpeningCardEnabled,OpeningCardFile,OpeningCardSeconds,ClosingCardEnabled,ClosingCardFile,ClosingCardSeconds,VideoLogoEnabled,VideoLogo,VideoLogoPosition,VideoLogoWidthPercent,SubtitleColor,LongFontSize,ShortFontSize,EncodingPreset,Crf,FramesPerSecond,PronunciationDictionary".Split(',');

    public static Settings Effective(Settings current, Settings? saved)

    {

        var copy = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(current, Json.Options), Json.Options)!;

        if (saved != null) foreach (var name in Fields) { var property = typeof(Settings).GetProperty(name)!; property.SetValue(copy, property.GetValue(saved)); }

        // Deep copy also isolates music metadata from the editor for the next video.

        return JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(copy, Json.Options), Json.Options)!;

    }

    public static Settings Snapshot(Settings settings) => Effective(new Settings(), settings);

    public static void Validate(Episode episode, Settings current)

    {

        Rules.Validate(episode);

        if (episode.IncludeLong ? episode.ShortCount != 0 : episode.ShortCount != 1) throw new InvalidOperationException("Her sıraya yalnızca bir normal video veya bir Shorts ekle.");

        if (episode.RemoteUploadJob == "" && (episode.PublishAt == null || episode.PublishAt <= DateTimeOffset.UtcNow.AddMinutes(10))) throw new InvalidOperationException("Yayın tarihi en az 10 dakika ileride olmalı; üretim için yeterli zaman bırak.");

        var settings = VideoSettings.For(current,episode);

        var targets = Rules.Scripts(episode).Select(x => (x.Short, Settings: VideoSettings.For(current,episode,x.Script))).ToList();

        if(targets.Count==0) targets.Add((!episode.IncludeLong,settings));

        foreach(var target in targets) { settings=target.Settings;

        SettingsValidation.Check(settings); ClosingCards.Validate(settings,target.Short);

        if (settings.VideoLogoEnabled && !File.Exists(settings.VideoLogo)) throw new InvalidOperationException("Video logosunu seç.");

        if (settings.CoverMode == "Kendi PNG kapak" && !File.Exists(settings.CoverPng)) throw new InvalidOperationException("Küçük resim dosyasını seç.");

        foreach (var image in new[] { settings.VideoLogoEnabled ? settings.VideoLogo : "", settings.CoverMode == "Kendi PNG kapak" ? settings.CoverPng : "", settings.CoverBackground, settings.CoverLogo }.Where(x => x != "")) {

            if (!File.Exists(image)) throw new InvalidOperationException("Seçilen görsel bulunamadı: " + Path.GetFileName(image));

            using var bitmap = System.Drawing.Image.FromFile(image); if ((long)bitmap.Width * bitmap.Height > 40_000_000) throw new InvalidOperationException("Plan görseli çok büyük.");

        }

        if ((episode.MusicClip == null || episode.MusicClip.NarrationEnabled) && settings.VoiceProvider.StartsWith("ElevenLabs") && settings.VoiceId.Trim() == "") throw new InvalidOperationException("ElevenLabs ses kimliğini seç / gir.");

        if ((episode.MusicClip == null || episode.MusicClip.NarrationEnabled) && settings.VoiceProvider.StartsWith("Piper") && (!File.Exists(settings.PiperPath) || !File.Exists(settings.PiperModel))) throw new InvalidOperationException("Piper programını ve Türkçe modelini seç.");

        if(settings.CoverMode=="Kendi PNG kapak") MediaFormat.Image(settings.CoverPng,target.Short);

        }

        episode.QueueLayout?.Validate();

        if (episode.MusicClip != null) MusicClips.Validate(episode);

        if (episode.QueueMusic?.Enabled == true) { episode.QueueMusic.Validate(); if (!File.Exists(episode.QueueMusic.File) || episode.QueueMusic.Volume <= 0) throw new InvalidOperationException("Müzik dosyası veya ses seviyesi geçersiz."); }

        else if (episode.Music) throw new InvalidOperationException("Müzik açık; dosya seçip kullanım bilgilerini tamamla.");

    }

    public static void PrepareDesign(Episode episode, Settings current)

    {

        var settings = Effective(current, episode.QueueSettings);

        foreach (var (_, vertical, script) in Rules.Scripts(episode)) {

            script.MusicTrack ??= episode.QueueMusic == null ? null : JsonSerializer.Deserialize<MusicTrack>(JsonSerializer.Serialize(episode.QueueMusic))!;

            script.TextLayout ??= episode.QueueLayout == null ? TextLayout.Defaults(settings, vertical) : JsonSerializer.Deserialize<TextLayout>(JsonSerializer.Serialize(episode.QueueLayout))!;

            if (episode.MusicClip != null) { script.TextLayout.SubtitleEnabled = episode.MusicClip.SubtitlesEnabled; script.TextLayout.TitleEnabled = false; }

        }

    }

    public static void CopyAssets(Store store, Episode episode)

    {

        if (episode.QueueSettings == null) return;

        var folder = Path.Combine(store.Root, "queue-assets", episode.Id); Directory.CreateDirectory(folder);

        string Copy(string file) {

            if (file == "") return "";

            if (!File.Exists(file)) throw new InvalidOperationException("Seçilen plan dosyası bulunamadı: " + Path.GetFileName(file));

            var target = Path.Combine(folder, Reliability.FileHash(file)[..12] + "-" + Path.GetFileName(file));

            if (!File.Exists(target)) File.Copy(file, target);

            return target;

        }

        var settings = episode.QueueSettings;

        if (settings.OpeningCardEnabled) settings.OpeningCardFile = Copy(settings.OpeningCardFile);

        if (settings.ClosingCardEnabled) settings.ClosingCardFile = Copy(settings.ClosingCardFile);

        if (settings.VideoLogoEnabled) settings.VideoLogo = Copy(settings.VideoLogo);

        if (settings.CoverMode == "Kendi PNG kapak") settings.CoverPng = Copy(settings.CoverPng);

        if (settings.CoverBackground != "") settings.CoverBackground = Copy(settings.CoverBackground);

        if (settings.CoverLogo != "") settings.CoverLogo = Copy(settings.CoverLogo);

        if (episode.QueueMusic?.Enabled == true) episode.QueueMusic.File = Copy(episode.QueueMusic.File);

    }

    public static void Reconcile(Store store)

    {

        bool changed = false;

        foreach (var episode in store.Episodes.Where(e => e.QueueOrder > 0)) {

            string group = PackageArchive.PublicationGroup(episode); if (group == "") continue;

            string state = group == "Yayına alınmış projeler" ? "Yayına alındı" : "Planlandı";

            string status = state == "Yayına alındı" ? "Yayında" : "Yayın planlandı";

            if (episode.QueueState == state && !episode.QueueAuthorized && episode.Status == status) continue;

            episode.QueueState = state; episode.QueueAuthorized = false; episode.Status = status; changed = true;

        }

        if (changed) store.Save();

    }

    public static IEnumerable<Episode> Items(Store store) => store.Episodes.Where(e => e.QueueOrder > 0 && PackageArchive.PublicationGroup(e) == "").OrderBy(e => e.QueueOrder);

}

