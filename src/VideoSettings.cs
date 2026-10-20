using System.Text.Json;
namespace AnkaStudio;

/// <summary>One resolver shared by editors, validation and rendering.</summary>
public static class VideoSettings
{
    public static Settings For(Settings defaults, Episode episode, VideoScript? script = null)
    {
        var resolved=PackageArtwork.Effective(VideoQueue.Effective(defaults, episode.QueueSettings ?? episode.ProductionSettings), episode, script);
        if(episode.Creation is {UseProjectVoice:false})CopyVoice(defaults,resolved);
        return resolved;
    }
    public static void CopyVoice(Settings source,Settings target)
    {
        foreach(var name in "VoiceProvider,EdgeTtsVoice,EdgeTtsPython,GoogleTtsVoice,GoogleTtsFallback,WindowsVoice,PiperPath,PiperModel,XttsUrl,XttsSpeaker,VoiceId,VoiceModel,VoiceSpeed,ElevenStability,ElevenSimilarity,ElevenStyle,ElevenSpeakerBoost,PronunciationDictionary,ShortVoiceOverride,ShortVoiceProvider,ShortVoiceId,ShortVoiceSpeed".Split(',')){
            var property=typeof(Settings).GetProperty(name)!;property.SetValue(target,property.GetValue(source));
        }
    }
    public static void Freeze(Settings defaults, Episode episode)
    {
        episode.ProductionSettings ??= JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(For(defaults,episode),Json.Options),Json.Options)!;
        if(episode.Creation is {UseProjectVoice:false}){
            CopyVoice(defaults,episode.ProductionSettings);
            if(episode.QueueSettings!=null)CopyVoice(defaults,episode.QueueSettings);
        }
    }
}

public static class ProjectState
{
    public static int Progress(Episode e) => e.Status is "Araştırılıyor" or "Üretiliyor" or "Yükleniyor" ? Math.Clamp(e.Progress,0,100) : PackageArchive.PublicationGroup(e) != "" || Reliability.OutputsProduced(e) ? 100 : e.Plan!=null ? 25 : 0;
    public static bool[] CompletedStages(Episode e, bool scenesReviewed) {
        if (e.Status == "Araştırılıyor" || e.ResumeStage is "Plan" or "Proofread") return new bool[8];
        if (e.Error == "" && PackageArchive.PublicationGroup(e) != "") return Enumerable.Repeat(true, 8).ToArray();
        var expected=Rules.Scripts(e).Select(x=>x.Key).ToList();
        bool produced=Reliability.OutputsProduced(e), filesReady=Reliability.OutputsCurrent(e,false);
        bool approved=filesReady && expected.Count>0 && expected.All(key=>e.Videos.Any(v=>v.Key==key && v.QualityPassed)) && e.Approved && e.ApprovalHash==Rules.HashPlan(e);
        bool uploaded=approved && expected.All(key=>e.Videos.Any(v=>v.Key==key && v.YouTubeId!="" && (v.Short || v.ThumbnailUploaded)));
        return [e.Plan!=null,e.Plan!=null && scenesReviewed,(e.SkipArtworkEditing || e.ArtworkSettings!=null || e.ProductionSettings!=null),e.Plan!=null && (e.SkipTextDesignEditing || Rules.Scripts(e).Any() && Rules.Scripts(e).All(x=>x.Script.TextLayout!=null)),produced,approved,uploaded,produced];
    }
    public static string Message(Episode e)
    {
        if(e.Error!="")return e.Error;
        if(e.Status == "Araştırılıyor" && e.CurrentStage.StartsWith("İçerik oluşturuldu."))return e.CurrentStage;
        if(e.Status is "Araştırılıyor" or "Üretiliyor" or "Yükleniyor")return e.Events.LastOrDefault() ?? e.Status;
        var publication=PackageArchive.PublicationGroup(e);
        if(publication!="")return publication=="Yayına alınmış projeler" ? "YouTube’da yayında." : "YouTube’da yayın tarihi planlandı.";
        if(e.Videos.Any(v=>!File.Exists(v.File)))return "Çıktı dosyası eksik. Dosyayı geri getir veya yeniden üret; proje tamamlanmış sayılmadı.";
        if(e.TextDesignPending || e.ArtworkPending)return "Ayarlar değişti. Yeni ayarları uygulamak için videoyu yeniden üret.";
        if(Reliability.OutputsCurrent(e,false))return e.Approved ? "Videolar onaylandı. Yayın adımına geçebilirsin." : "Videolar hazır. İncele ve onayla.";
        if(e.Plan!=null)return "Senaryo hazır. Sahne, kapak ve ses ayarlarını kontrol edip üretime geçebilirsin.";
        return "İçerik talimatını veya hazır metnini gir; ardından içeriği hazırla.";
    }
}

public static class ToolHealth
{
    public static string Summary(Store store)
    {
        try { if(!File.Exists(store.Probe))return "● Video motoru eksik"; _=store.Ffmpeg; }
        catch {return "● Video motoru eksik";}
        return "● Video motoru bulundu";
    }
    public static string Check(Store store)
    {
        var lines=new List<string>{Summary(store)};
        var align=LocalAudioAlignment.Folder;
        lines.Add(new[]{"runtime/python.exe","align.py","model/model.bin"}.All(x=>File.Exists(Path.Combine(align,x))) ? "Altyazı eşleme dosyaları bulundu" : "Altyazı eşleme eksik: tam kurulum paketindeki tools klasörü gerekli");
        lines.Add(store.Settings.VoiceProvider.StartsWith("Piper") && (!File.Exists(store.Settings.PiperPath)||!File.Exists(store.Settings.PiperModel)) ? "Seçili Piper motoru / modeli eksik" : "Ses motoru: "+store.Settings.VoiceProvider);
        lines.Add(store.Settings.YouTubeChannelTitle=="" ? "YouTube kanalı doğrulanmamış" : "Kayıtlı kanal: "+store.Settings.YouTubeChannelTitle);
        lines.Add("Yerel AI modeli ve ağ bağlantıları: Bağlantılar → Durumu yenile ile canlı kontrol et.");
        return string.Join("\n",lines);
    }
}
