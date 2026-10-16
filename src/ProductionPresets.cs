namespace AnkaStudio;

public static class ProductionPresets
{
    public sealed record Preset(string Id,string Name,string Description);
    public static readonly Preset[] All=[
        new("story","Kıssa ve Hikâye • 9:16","Karakter referanslı AI görselleri, sinematik hareket, altın altyazı."),
        new("documentary","Belgesel • 16:9","Yatay video, konuya göre stok görüntü, sade ve okunaklı altyazı."),
        new("package","Normal video + 2 Shorts","Bir yatay ana video ve iki bağımsız dikey kısa video."),
        new("music","Müzik / Fotoğraf Klibi","Seçtiğin müzikle fotoğraf akışı; parça ve hak bilgileri ayrıca seçilir.")];
    public static void Apply(Episode episode,Settings defaults,string id)
    {
        if(!All.Any(p=>p.Id==id))throw new InvalidOperationException("Tasarım kalıbı bulunamadı.");
        if(episode.Plan!=null||episode.Videos.Count>0)throw new InvalidOperationException("Hazırlanmış metni korumak için başka üretim kalıbını yeni bir projede seç.");
        if(episode.UserNarration?.Enabled==true && id=="music")throw new InvalidOperationException("Hazır anlatım metni için hikâye veya belgesel kalıbını seç.");
        if(episode.UserNarration?.Enabled==true && id=="package")throw new InvalidOperationException("Hazır metinli paket için İnce ayar bölümünde ana video ve iki Shorts metnini ayrı gir.");
        if(episode.UserNarration?.Enabled==true && id=="story" && ScriptLength.Count(episode.UserNarration.LongText!=""?episode.UserNarration.LongText:episode.UserNarration.Shorts.FirstOrDefault()??"")>360)throw new InvalidOperationException("Shorts için metin çok uzun; belgesel kalıbını seç veya metni kısalt.");
        var settings=VideoQueue.Snapshot(defaults);
        settings.CoverStyle=ChannelPack.Matches(defaults)?"Nur Kapısı • Altın sinema":defaults.CoverStyle;settings.CoverAutoStyle=false;settings.CoverFont="Segoe UI";
        settings.SubtitleColor=id=="story"?"#FFD166":"#FFFFFF";settings.MusicVolume=.08;settings.ShortMusicVolume=.06;
        episode.IncludeLong=id is "documentary" or "package" or "music";episode.ShortCount=id=="package"?2:id=="story"?1:0;
        episode.ShortSeconds=90;episode.LongMinutes=6;episode.Style=id=="story"?"Hikâye anlatımı":"Belgesel";
        episode.ShortMode="Bağımsız Shorts";episode.AiStoryImages=id=="story";episode.PhotosOnly=id=="music";
        episode.AiStoryLocal=true;episode.AiStoryMotion=true;episode.AiStoryCinematic=true;episode.AiStoryAutonomous=id=="story";
        episode.AiStoryStyle="Sinematik tarihî illüstrasyon, tutarlı yüzler ve kıyafetler, koyu turkuaz gölgeler ve sıcak altın ışık";
        episode.Music=false;episode.MusicClip=id=="music"?new MusicClipOptions():null;
        if(episode.UserNarration?.Enabled==true){var own=episode.UserNarration;string text=own.LongText!=""?own.LongText:own.Shorts.FirstOrDefault()??"";if(id=="story"&&ScriptLength.Count(text)>360)throw new InvalidOperationException("Shorts için metin çok uzun; belgesel kalıbını seç veya metni kısalt.");own.LongText=text;own.Shorts=[text];}
        episode.ProductionSettings=settings;episode.WizardPreset=id;episode.SkipArtworkEditing=true;episode.SkipTextDesignEditing=true;
    }
    public static bool[] Complete(Episode e)=>[
        e.Topic.Trim()!="", e.Plan!=null,
        Reliability.OutputsCurrent(e,false),
        e.Approved&&e.ApprovalHash==Rules.HashPlan(e)&&Reliability.OutputsCurrent(e,false),
        PackageArchive.PublicationGroup(e)!=""];
}
