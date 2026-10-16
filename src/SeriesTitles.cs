using System.Globalization;

using System.Text.RegularExpressions;

namespace AnkaStudio;

public static class SeriesTitles

{

    static readonly Regex Pattern = new(@"^hadis\s+sohbetleri(?:\s*(?:[-–—:#.]|bölüm)?\s*(?<number>\d+))?(?<suffix>\s*[|•:–—-].*)?$", RegexOptions.CultureInvariant);

    static Match Parse(string value) => Pattern.Match(value.Trim().ToLower(CultureInfo.GetCultureInfo("tr-TR")));

    static int Number(string value) => int.TryParse(Parse(value).Groups["number"].Value, out var number) && number is > 0 and < 1000000 ? number : 0;

    public static void Apply(Store store, Episode episode)

    {

        if (episode.Plan == null || episode.MusicClip != null || episode.Demo || episode.Videos.Any(v => v.YouTubeId != "")) return;

        const string series = "Hadis Sohbetleri";

        bool seriesTopic = Parse(episode.Topic).Success;

        var existing = new Dictionary<string, string>();

        foreach (var other in store.Episodes.Where(e => e.Id != episode.Id)) {

            foreach (var item in Rules.Scripts(other)) if (Parse(item.Script.Title).Success || Parse(other.Topic).Success) existing[other.Id + ":" + item.Key] = item.Script.Title;

            foreach (var video in other.Videos) if (Parse(video.Title).Success || Parse(other.Topic).Success) existing[other.Id + ":" + video.Key] = video.Title;

        }

        int last = Math.Max(store.Settings.SeriesCounters.GetValueOrDefault(series), Math.Max(existing.Count, existing.Values.Select(Number).DefaultIfEmpty(0).Max()));

        foreach (var (key, _, script) in Rules.Scripts(episode)) {

            if (!seriesTopic && !Parse(script.Title).Success) continue;

            string binding = episode.Id + ":" + key + ":" + series;

            if (!store.Settings.SeriesAssignments.TryGetValue(binding, out int number)) { number = checked(++last); store.Settings.SeriesAssignments[binding] = number; }

            last = Math.Max(last, number); string old = script.Title;

            var match = Parse(old); string suffix = match.Success ? (match.Groups["suffix"].Success ? old.Trim()[match.Groups["suffix"].Index..] : "") : " • " + old;

            script.Title = series + " " + number + suffix; if (script.Title.Length > 100) script.Title = script.Title[..100];

            if (script.ThumbnailText == old || Parse(script.ThumbnailText).Success) script.ThumbnailText = series + " " + number;

            foreach (var scene in script.Scenes) if (Parse(scene.OnScreen).Success) scene.OnScreen = series + " " + number;

            if (script.Title != old) episode.Log("Seri başlığı: " + script.Title);

        }

        if (last > 0) store.Settings.SeriesCounters[series] = last;

        if (episode.Plan.SpellingHash != "") episode.Plan.SpellingHash = Proofreading.Fingerprint(episode.Plan);

    }

}

