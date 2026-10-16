namespace AnkaStudio;



public sealed class MusicTrack

{

    public bool Enabled { get; set; }

    public string File { get; set; } = "";

    public string RightsDocument {get;set;}="";

    public double Start { get; set; }

    public double End { get; set; }

    public double SourceOffset { get; set; }

    public double Volume { get; set; } = .12;

    public double FadeIn { get; set; } = .5;

    public double FadeOut { get; set; } = 1;

    public bool Duck { get; set; } = true;

    public string Source { get; set; } = "";

    public string License { get; set; } = "";

    public string Attribution { get; set; } = "";

    public bool RightsConfirmed { get; set; }

    public void Validate()

    {

        if (new[] { Start, End, SourceOffset, Volume, FadeIn, FadeOut }.Any(v => !double.IsFinite(v) || v < 0) || Start > 1800 || End > 1800 || SourceOffset > 86400 || Volume > 1 || FadeIn > 30 || FadeOut > 30 || End > 0 && End <= Start) throw new InvalidOperationException("Müzik başlangıcı, bitişi veya ses ayarları geçersiz.");


        if (Enabled && string.IsNullOrWhiteSpace(File)) throw new InvalidOperationException("Müzik dosyası seçilmemiş.");

    }

}



public static class EditorAudio

{

    public static string Mix(MusicTrack music, double duration, string voice, string input, double windowStart = 0, bool sourceSeeked = false)

    {

        double from = Math.Max(music.Start, windowStart), to = Math.Min(music.End > 0 ? music.End : windowStart + duration, windowStart + duration);

        if (to <= from) return $"[{voice}:a]loudnorm=I=-16:TP=-1.5:LRA=11[a]";

        double length = to - from, lead = from - windowStart, skip = sourceSeeked ? 0 : music.SourceOffset + Math.Max(0, windowStart - music.Start);

        double fadeIn = Math.Min(music.FadeIn, length / 2), fadeOut = Math.Min(music.FadeOut, length / 2);

        var filter = $"[{voice}:a]loudnorm=I=-16:TP=-1.5:LRA=11,aresample=44100[normalvoice];[{input}:a]atrim=start={Media.Num(skip)}:duration={Media.Num(length)},asetpts=PTS-STARTPTS,loudnorm=I=-16:TP=-1.5:LRA=11,volume={Media.Num(music.Volume)}";

        if (fadeIn > 0 && windowStart <= music.Start) filter += $",afade=t=in:st=0:d={Media.Num(fadeIn)}";

        if (fadeOut > 0 && (music.End == 0 || music.End <= windowStart + duration)) filter += $",afade=t=out:st={Media.Num(length - fadeOut)}:d={Media.Num(fadeOut)}";

        filter += $",adelay={Math.Round(lead * 1000)}:all=1,apad,atrim=duration={Media.Num(duration)}[music];";

        if (music.Duck) filter += "[normalvoice]asplit=2[voice][side];[music]asplit=2[dry][bed];[bed][side]sidechaincompress=threshold=0.06:ratio=3:attack=20:release=400[duck];[dry][duck]amix=inputs=2:weights=0.5 0.5:normalize=0[musicbed];[voice][musicbed]amix=inputs=2:duration=first:normalize=0";

        else filter += "[normalvoice][music]amix=inputs=2:duration=first:normalize=0";

        return filter + ",alimiter=limit=0.95:level=0[a]";

    }

}

