using System.Text.RegularExpressions;



namespace AnkaStudio;



public static class StockSearch

{

    public static bool Blocked(Settings settings,string url)=>url!="" && settings.ExcludedStockUrls.Split('\n',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Any(x=>url.Equals(x,StringComparison.OrdinalIgnoreCase));

    public static readonly (string Turkish, string English)[] Backgrounds = [

        ("doğa", "nature"), ("deniz", "sea"), ("okyanus", "ocean"), ("dalga", "waves"),

        ("sahil", "beach"), ("kıyı", "coast"), ("gün batımı", "sunset"), ("günbatımı", "sunset"), ("kumsal", "sandy beach"), ("su altı", "underwater"), ("mercan", "coral reef"),

        ("uzay", "space"), ("yıldız", "stars"), ("galaksi", "galaxy"), ("gezegen", "planet"),

        ("ay", "moon"), ("dünya", "earth"), ("bulutsu", "nebula"), ("kuzey ışıkları", "aurora"),

        ("gökyüzü", "sky"), ("bulut", "clouds"), ("gün doğumu", "sunrise"), ("gün batımı", "sunset"),

        ("orman", "forest"), ("ağaç", "trees"), ("yaprak", "leaves"), ("çiçek", "flowers"),

        ("dağ", "mountain"), ("vadi", "valley"), ("tepe", "hills"), ("çayır", "meadow"),

        ("göl", "lake"), ("nehir", "river"), ("dere", "stream"), ("şelale", "waterfall"),

        ("yağmur", "rain"), ("kar", "snow"), ("sis", "fog"), ("buzul", "glacier"),

        ("çöl", "desert"), ("kum tepeleri", "sand dunes"), ("ada", "island"), ("kayalık", "rocks"),

        ("bahar", "spring nature"), ("yaz", "summer landscape"), ("sonbahar", "autumn leaves"), ("kış", "winter landscape"),

        ("su", "water"), ("su damlası", "water drops"), ("yansıma", "reflection water"), ("güneş ışığı", "sunlight"),

        ("kuş", "birds"), ("kelebek", "butterfly"), ("çimen", "grass"), ("bahçe", "garden"),

        ("doğa manzarası", "landscape"), ("havadan doğa", "aerial nature"), ("orman yolu", "forest path"), ("sakin deniz", "calm sea"),

        ("çiçek yakın çekim", "flowers close up"), ("dağ zirvesi", "mountain peak"), ("yıldızlı gece", "night sky"), ("akan su", "flowing water"),

        ("soyut fon", "abstract background"), ("ışık parçacıkları", "light particles"), ("yumuşak ışık", "soft light"), ("renk geçişi", "gradient background")

    ];

    public static string Catalog => string.Join("\r\n", Backgrounds.Select(x => x.Turkish + " — " + x.English));

    public static string Normalize(string text)

    {

        var terms = Regex.Split(text ?? "", @"[;,\r\n]+")

            .Select(x => Regex.Replace(Regex.Replace(Translate(x.Trim()), @"^montage[^:]*:\s*", "", RegexOptions.IgnoreCase), @"\s+", " ")).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        if (terms.Count == 1 && !terms[0].Contains(';'))

        {

            var words = MediaPool.Words(terms[0]);

            foreach (var pair in Backgrounds.Where(x => MediaPool.Words(x.English).Count == 1 && words.Contains(x.English)).Take(2)) terms.Add(pair.English);

            foreach (var pair in new[] { ("reading", "reading book"), ("walking", "walking"), ("organizing", "desk"), ("family", "family") }) if (words.Contains(pair.Item1)) terms.Add(pair.Item2);

        }

        return string.Join("; ", terms.Distinct(StringComparer.OrdinalIgnoreCase).Take(12));

    }

    public static string Translate(string query)

    {

        foreach (var pair in Backgrounds.OrderByDescending(x => x.Turkish.Length)) query = Regex.Replace(query, @"(?<![\p{L}])" + Regex.Escape(pair.Turkish) + @"(?![\p{L}])", pair.English, RegexOptions.IgnoreCase);

        return query;

    }

    public static string[] GeneralQueries(string topic)

    {

        // Keep the catalog broad but the request budget small. Choose related backgrounds first.

        var words = MediaPool.Words(topic);
        if (words.Overlaps(MediaPool.Words("document documents manuscript manuscripts calligraphy"))) return ["old book", "manuscript", "library"];
        if (words.Overlaps(MediaPool.Words("sultan advisor adviser palace"))) return ["historic architecture", "palace", "mosque"];
        if (words.Overlaps(MediaPool.Words("painting paintings"))) return ["painting", "museum", "art gallery"];
        if (words.Overlaps(MediaPool.Words("ottoman medieval scholar teaching scholar's akşemseddin aksemseddin alim âlim medrese"))) {
            if (words.Overlaps(MediaPool.Words("scholar teaching alim âlim medrese"))) return ["reading book", "library", "mosque courtyard"];
            if (words.Overlaps(MediaPool.Words("medicine medical herbs"))) return ["herbs", "old book", "library"];
            return ["historic architecture", "mosque", "old book"];
        }


        var related = Backgrounds.Where(x => MediaPool.Words(x.Turkish + " " + x.English).Overlaps(words)).Select(x => x.English);

        var families = new[] { "sea ocean beach waves water", "forest trees woodland nature", "desert sand dunes", "sky clouds sunrise sunset sunlight", "space stars galaxy planet nebula", "mountain valley hills landscape", "book reading library pages", "mosque prayer islam", "city street urban town road avenue traffic skyline" };

        var alternatives = families.Where(f => MediaPool.Words(f).Overlaps(words)).SelectMany(f => f.Split(' '));

        return related.Concat(alternatives).Where(q => !q.Contains(' ') || MediaPool.Words(q).Overlaps(words)).Distinct().Take(6).ToArray();

    }

    public const int SelectionVersion = 3;

    static readonly HashSet<string> Weapons = MediaPool.Words("gun guns firearm firearms rifle rifles pistol pistols weapon weapons shooting ammunition military tank tanks silah tüfek tabanca mühimmat");

    public static string Metadata(System.Text.Json.JsonElement item)

    {

        var fields = new[] { "title", "tags", "description", "url", "pageURL" };

        return string.Join(" ", fields.Where(f => item.TryGetProperty(f, out _)).Select(f => item.GetProperty(f).ToString()));

    }

    public const string NeutralReviewQuery = "Neutral background: nature, forest, sea, sky, landscape or abstract light. No people, combat or unrelated city footage. This is an explicitly permitted representative background, not a depiction of the historical person or event.";
    public static string ReviewQuery(Scene scene) => scene.StockReviewTheme == Normalize(scene.VisualQuery) && scene.StockReviewQuery != "" ? scene.StockReviewQuery : scene.VisualQuery;
    public static int RelevanceScore(string query, string metadata) => MediaPool.ExpandedWords(query).Intersect(MediaPool.ExpandedWords(metadata)).Count();

    public static bool WeaponQuery(string query) => MediaPool.Words(query).Overlaps(Weapons);

    public static bool PossibleCandidate(string query, string metadata, bool canReviewFrames)
    {
        if (MediaPool.Words(metadata).Overlaps(Weapons) && !WeaponQuery(query)) return false;
        return canReviewFrames || Appropriate(query, metadata);
    }

    public static bool Appropriate(string query, string metadata)

    {

        var candidate = MediaPool.Words(metadata); var requested = MediaPool.Words(query);

        if (candidate.Overlaps(Weapons) && !requested.Overlaps(Weapons)) return false;

        var noise = MediaPool.Words("https http www com net org video videos stock free footage pexels pixabay vecteezy mp4 mov mkv webm hd 4k the a an and of on in by with at to is");

        var meaningful = candidate.Where(w => !noise.Contains(w) && !w.All(char.IsDigit)).ToHashSet();

        var wanted = MediaPool.ExpandedWords(query); wanted.ExceptWith(noise);

        return meaningful.Count == 0 || wanted.Count == 0 || MediaPool.ExpandedWords(string.Join(" ", meaningful)).Overlaps(wanted);

    }

    public static bool Automatic(Scene scene) => !scene.UserLocal && (scene.StockSelectionVersion > 0 || scene.StockUrl.Contains("pexels.com/", StringComparison.OrdinalIgnoreCase) || scene.StockUrl.Contains("pixabay.com/", StringComparison.OrdinalIgnoreCase) || scene.StockUrl.Contains("vecteezy.com/", StringComparison.OrdinalIgnoreCase) || Path.GetFileName(scene.StockFile).StartsWith("pool-", StringComparison.OrdinalIgnoreCase));



}

