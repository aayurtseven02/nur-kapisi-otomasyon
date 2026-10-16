namespace AnkaStudio;



public static class PackageArchive

{

    public static string PublicationGroup(Episode episode)

    {

        if (episode.Demo || episode.Plan == null || episode.TextDesignPending || episode.ArtworkPending) return "";

        var expected = Rules.Scripts(episode).Select(x => x.Key).ToList();

        if (expected.Count == 0) return "";

        var videos = expected.Select(key => episode.Videos.FirstOrDefault(v => v.Key == key)).ToList();

        if (videos.Any(v => v == null || v.YouTubeId == "")) return "";

        // Publication is remote history. A local render fingerprint can change
        // after a software upgrade; pending edits are tracked separately above.

        if (videos.All(v => v!.YouTubeState == "Yayında")) return "Yayına alınmış projeler";

        if (videos.All(v => v!.YouTubeState is "Yayında" or "Planlı bekliyor")) return "Planlanmış projeler";

        return "";

    }

    public static string Group(Episode episode)

    {

        var publication = PublicationGroup(episode); if (publication != "") return publication;

        if (episode.Demo || episode.TextDesignPending || episode.ArtworkPending || episode.Error != "" || episode.Plan == null || episode.RemoteUploadState == "failed" || episode.Status is "Araştırılıyor" or "Üretiliyor" or "Yükleniyor" or "Kesildi") return "";

        var expected = Rules.Scripts(episode).Select(x => x.Key).ToList();

        if (expected.Count == 0) return "";

        var videos = expected.Select(key => episode.Videos.FirstOrDefault(v => v.Key == key)).ToList();

        if (!Reliability.OutputsCurrent(episode,false)) return "";

        if (videos.Any(v => v == null || !v.QualityPassed || v.YouTubeState.StartsWith("Sorun") || v.YouTubeState.StartsWith("Bulunamadı"))) return "";

        if (videos.All(v => v!.YouTubeId != "" && v.YouTubeState == "Yayında")) return "Yayına alınmış projeler";

        if (videos.All(v => v!.YouTubeId != "" && (v.YouTubeState is "Yayında" or "Planlı bekliyor" || v.Scheduled))) return "Planlanmış projeler";

        return "Tamamlanmış projeler";

    }

}

