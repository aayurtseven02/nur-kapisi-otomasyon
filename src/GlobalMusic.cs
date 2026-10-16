using System.Text.Json;

namespace AnkaStudio;

public static class GlobalMusic

{

    public static MusicTrack Resolve(Store store, bool vertical)

    {

        var settings = store.Settings;

        var saved = settings.LibraryMusic;

        if (saved?.File != settings.MusicPath) saved = null;

        var item = new AudioLibrary(store).List().FirstOrDefault(a => a.File == settings.MusicPath);

        var track = saved == null ? new MusicTrack { File = settings.MusicPath, Source = item?.Source ?? "", License = item?.License ?? "", Attribution = item?.Attribution ?? "", RightsConfirmed = item?.RightsConfirmed ?? false } : JsonSerializer.Deserialize<MusicTrack>(JsonSerializer.Serialize(saved))!;

        track.Enabled = true; track.Volume = vertical && settings.ShortVoiceOverride ? settings.ShortMusicVolume : settings.MusicVolume;

        track.Validate(); return track;

    }

}

