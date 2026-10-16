using System.Text.Json;

namespace AnkaStudio;



public sealed class MusicClipOptions

{

    public string TrackTitle { get; set; } = "";

    public string Artist { get; set; } = "";

    public string LyricsBy { get; set; } = "";

    public string MusicBy { get; set; } = "";

    public string VisualTheme { get; set; } = "doğa manzarası; gün batımı; orman";

    public bool ShowCredits { get; set; } = true;
    public bool NarrationEnabled {get;set;}
    public bool SubtitlesEnabled {get;set;}
    public string Text {get;set;} = "";

    public string AudioHash { get; set; } = "";

    public double Duration { get; set; }

}

public static class MusicClips

{

    public static void Validate(Episode e)

    {

        var clip = e.MusicClip ?? throw new InvalidOperationException("Müzik klibi ayarları gerekli.");

        if (!e.IncludeLong || e.ShortCount != 0) throw new InvalidOperationException("Müzik klibi tek yatay video olarak hazırlanır.");

        if (clip.TrackTitle.Trim().Length is < 1 or > 100 || new[] { clip.Artist, clip.LyricsBy, clip.MusicBy }.Any(x => x.Length > 150 || x.Contains('\n') || x.Contains('\r'))) throw new InvalidOperationException("Parça adı ve sanatçı / söz / müzik bilgilerini kontrol et.");

        if (clip.VisualTheme.Trim().Length is < 1 or > 1000) throw new InvalidOperationException("Stok görüntü temasını seç / yaz.");

        if ((clip.NarrationEnabled || clip.SubtitlesEnabled) && (clip.Text.Trim().Length is < 1 or > 30000)) throw new InvalidOperationException("Seçtiğin altyazı / seslendirme için metin gir (en fazla 30000 karakter).");

        if (!double.IsFinite(clip.Duration) || clip.Duration is < 5 or > 1800) throw new InvalidOperationException("Müzik klibi parçası 5 saniye–30 dakika arasında olmalı.");

        if (e.QueueMusic?.Enabled != true) throw new InvalidOperationException("Klip için ana müzik dosyasını seç.");

        e.QueueMusic.Validate();

        if (!File.Exists(e.QueueMusic.File) || e.QueueMusic.Volume <= 0) throw new InvalidOperationException("Ana müzik dosyası veya ses seviyesi geçersiz.");

        if (e.QueueMusic.Start != 0 || e.QueueMusic.End != 0 || e.QueueMusic.SourceOffset != 0 || e.QueueMusic.Duck) throw new InvalidOperationException("Müzik klibinde parça baştan sona kullanılır; konuşmada kısma uygulanmaz.");

    }

    public static async Task<double> ReadDuration(Store store, string file, CancellationToken ct)

    {

        if (!File.Exists(file)) throw new InvalidOperationException("Ana müzik dosyasını seç.");

        var duration = await Media.Duration(store, file, ct);

        if (!double.IsFinite(duration) || duration is < 5 or > 1800) throw new InvalidOperationException("Müzik parçası 5 saniye–30 dakika arasında olmalı.");

        return duration;

    }

    public static string Credits(MusicClipOptions clip) => string.Join("\n", new[] {

        clip.TrackTitle.Trim(), clip.Artist.Trim() == "" ? "" : "Sanatçı: " + clip.Artist.Trim(),

        clip.LyricsBy.Trim() == "" ? "" : "Söz: " + clip.LyricsBy.Trim(), clip.MusicBy.Trim() == "" ? "" : "Müzik: " + clip.MusicBy.Trim()

    }.Where(x => x != ""));

    public static async Task<ContentPlan> Plan(Store store, Episode e, CancellationToken ct)

    {

        Validate(e); var clip = e.MusicClip!;

        clip.Duration = await ReadDuration(store, e.QueueMusic!.File, ct); clip.AudioHash = Reliability.FileHash(e.QueueMusic.File);

        var queries = clip.VisualTheme.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(q => StockSearch.Normalize(StockSearch.Translate(q))).Where(x => x != "").ToArray();

        if (queries.Length == 0) throw new InvalidOperationException("Stok görüntü teması gerekli.");

        var text = clip.Text.Split(['\r','\n'],StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
        if(text.Length>120 || text.Any(x=>x.Length>3500))throw new InvalidOperationException("Klip metnini en fazla 120 satır, satır başına en fazla 3500 karakter olacak şekilde düzenle.");
        int count = clip.NarrationEnabled || clip.SubtitlesEnabled ? text.Length : Math.Clamp((int)Math.Ceiling(clip.Duration / 8), 1, 120);

        var design = e.QueueLayout == null ? TextLayout.Defaults(store.Settings, false) : JsonSerializer.Deserialize<TextLayout>(JsonSerializer.Serialize(e.QueueLayout))!;

        design.SubtitleEnabled = clip.SubtitlesEnabled; design.TitleEnabled = false;

        var script = new VideoScript { Title = clip.TrackTitle, ThumbnailText = clip.TrackTitle, Description = Credits(clip), Tags = ["müzik", "klip"], MusicTrack = JsonSerializer.Deserialize<MusicTrack>(JsonSerializer.Serialize(e.QueueMusic)), TextLayout = design,

            Scenes = Enumerable.Range(0, count).Select(i => new Scene { Narration = clip.NarrationEnabled || clip.SubtitlesEnabled ? text[i] : "Müzik klibi görüntü planı " + (i + 1), VisualQuery = queries[i % queries.Length], Duration = clip.Duration / count }).ToList() };

        return new ContentPlan { Long = script, Research = "Müzik klibi: kullanıcı tarafından seçilen parça ve görüntü teması. Seslendirme: "+(clip.NarrationEnabled?"açık":"kapalı")+". Altyazı: "+(clip.SubtitlesEnabled?"açık":"kapalı")+"." };

    }

    public static async Task PrepareAudio(Store store, Episode e, VideoScript script, string folder, CancellationToken ct, Settings? settings=null, Services? services=null, Func<string,string,Settings,CancellationToken,Task>? testSpeech=null)

    {

        Validate(e); var clip = e.MusicClip!; var track = e.QueueMusic!;

        if (clip.AudioHash != "" && clip.AudioHash != Reliability.FileHash(track.File)) throw new InvalidOperationException("Ana müzik dosyası değişmiş; klip planını yeniden oluştur.");

        double actual = await ReadDuration(store, track.File, ct);

        if (Math.Abs(actual - clip.Duration) > .5) throw new InvalidOperationException("Müzik dosyasının süresi değişmiş; klip planını yeniden oluştur.");

        string filter = "volume=" + Media.Num(track.Volume);

        if (track.FadeIn > 0) filter += $",afade=t=in:st=0:d={Media.Num(Math.Min(track.FadeIn, actual / 2))}";

        if (track.FadeOut > 0) filter += $",afade=t=out:st={Media.Num(actual - Math.Min(track.FadeOut, actual / 2))}:d={Media.Num(Math.Min(track.FadeOut, actual / 2))}";

        var full = Path.Combine(folder, "narration-full.wav");

        await Media.Exec(store.Ffmpeg, ["-y", "-i", track.File, "-vn", "-af", filter, "-ar", "44100", "-ac", "2", "-c:a", "pcm_s16le", full], folder, ct);

        double[]? speechLengths=null; List<Caption>[]? speechCaptions=null;
        if(clip.NarrationEnabled) {
            if(settings==null || services==null)throw new InvalidOperationException("Klip ses sağlayıcısı eksik.");
            var voiceFolder=Path.Combine(folder,"clip-voice");Directory.CreateDirectory(voiceFolder);
            await NarrationFlow.Prepare(store,e,script,settings,voiceFolder,services,ct,testSpeech);
            double voiceDuration=await Media.Duration(store,Path.Combine(voiceFolder,"narration-full.wav"),ct);
            if(voiceDuration>actual+.05)throw new InvalidOperationException("Seslendirme müzikten uzun. Metni kısalt veya daha uzun müzik seç; ses hızlandırılmadı.");
            speechLengths=script.Scenes.Select(x=>x.Duration).ToArray();
            speechCaptions=Enumerable.Range(1,script.Scenes.Count).Select(i=>Json.Read(Path.Combine(voiceFolder,$"speech-{i}.json"),new List<Caption>())).ToArray();
            var mixed=Path.Combine(folder,"clip-mixed.wav");
            await Media.Exec(store.Ffmpeg,["-y","-i",full,"-i",Path.Combine(voiceFolder,"narration-full.wav"),"-filter_complex","[0:a][1:a]amix=inputs=2:duration=first:normalize=0,alimiter=limit=0.95:level=false[a]","-map","[a]","-t",Media.Num(actual),"-ar","44100","-ac","2","-c:a","pcm_s16le",mixed],folder,ct);
            File.Copy(mixed,full,true);
        }
        double offset = 0;

        for (int i = 0; i < script.Scenes.Count; i++) {

            double length = i == script.Scenes.Count - 1 ? actual - offset : speechLengths?[i] ?? actual / script.Scenes.Count;

            await Media.Exec(store.Ffmpeg, ["-y", "-ss", Media.Num(offset), "-i", full, "-t", Media.Num(length), "-c:a", "pcm_s16le", Path.Combine(folder, $"speech-{i + 1}.wav")], folder, ct);

            var cues=clip.SubtitlesEnabled ? speechCaptions?[i] ?? Engine.SplitText(script.Scenes[i].Narration,48).Select((text,j)=>new Caption(length*j/Engine.SplitText(script.Scenes[i].Narration,48).Count,length*(j+1)/Engine.SplitText(script.Scenes[i].Narration,48).Count,text)).ToList() : new List<Caption>();
            script.Scenes[i].Duration=length;
            Json.Write(Path.Combine(folder, $"speech-{i + 1}.json"),cues); offset += length;

        }

    }

    public static string Ass(Episode e, VideoScript script, int width, int height, double duration, IEnumerable<Caption>? captions=null)

    {

        var design = script.TextLayout == null ? new TextLayout() : JsonSerializer.Deserialize<TextLayout>(JsonSerializer.Serialize(script.TextLayout))!;

        design.SubtitleEnabled = e.MusicClip!.SubtitlesEnabled; design.TitleEnabled = e.MusicClip.ShowCredits; design.TitleX = 50; design.TitleY = 78;

        design.TitleSize = Math.Min(design.TitleSize, 40);

        var lines = Credits(e.MusicClip).Split('\n');

        while (design.TitleSize > 20 && lines.Sum(x => Math.Max(1, (int)Math.Ceiling(x.Length * design.TitleSize * .6 / (width * .8)))) * design.TitleSize * 1.25 > height * .2) design.TitleSize -= 2;

        design.TitleAnimation = "Yok"; design.FadeOut = false;

        return TextDesign.Ass(captions ?? [], [(0d, duration, Credits(e.MusicClip))], design, width, height, limitTitles: false);

    }

}

