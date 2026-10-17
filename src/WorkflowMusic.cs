namespace AnkaStudio;

public static class WorkflowMusic
{
    public static void Validate(Episode episode,Settings settings){if(episode.Music&&!File.Exists(settings.MusicPath))throw new InvalidOperationException("Fon müziği açık. Tasarım bölümünden bir müzik dosyası seç veya fon müziğini kapat.");}
    public static void Apply(Store store,Episode episode,Settings settings){
        if(episode.Music&&!File.Exists(settings.MusicPath))return;
        foreach(var (_,vertical,script) in Rules.Scripts(episode)){
            if(!episode.Music){if(script.MusicTrack!=null)script.MusicTrack.Enabled=false;continue;}
            var track=script.MusicTrack;
            if(track==null||track.File!=settings.MusicPath)track=GlobalMusic.Resolve(store,vertical,settings);
            track.Enabled=true;track.Volume=vertical&&settings.ShortVoiceOverride?settings.ShortMusicVolume:settings.MusicVolume;script.MusicTrack=track;
        }
    }
    public static string Caption(Store store,Settings settings){var item=new AudioLibrary(store).List().FirstOrDefault(x=>x.File==settings.MusicPath);return settings.MusicPath==""?"Seçili müzik: henüz seçilmedi":"Seçili müzik: "+(string.IsNullOrWhiteSpace(item?.Name)?Path.GetFileName(settings.MusicPath):item.Name)+(File.Exists(settings.MusicPath)?"":" • dosya bulunamadı");}
}
