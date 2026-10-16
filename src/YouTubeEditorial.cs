using System.Text.RegularExpressions;

namespace AnkaStudio;

// Independent Turkish implementation inspired by youtube-agent-skill's packaging approach.
public static class YouTubeEditorial
{
    public const string Guidance = " İlk cümlede başlığın vaat ettiği konuyu somutlaştır; konuya uygun bir soru, sorun veya örnekle merak oluştur. Gereksiz kanal girişi ve başta abonelik çağrısı kullanma. Sahne geçişlerini birbirine bağlayan doğal cümleler yaz; her sahne tek fikri ilerletsin. Son bölüm açılışta verilen vaadi karşılasın ve en fazla bir çağrı içersin. Başlık ile kapak metni birbirini tamamlasın; aynı cümleyi tekrarlamasın. Kapak metni tercihen 2–4 sözcük olsun. Merak uğruna kaynak dışı sayı, kesin sonuç veya başarı garantisi uydurma. Kanalın hedef kitlesi ve istenen anlatım tarzını koru.";
    static HashSet<string> Words(string text) => Regex.Matches(text.ToLowerInvariant(), @"[\p{L}\p{N}]+").Select(m => m.Value).Where(w => !new[] { "ve", "bir", "bu", "ile", "için", "the", "a", "and", "of", "to" }.Contains(w)).ToHashSet();
    public static List<string> Check(VideoScript script)
    {
        var notes = new List<string>();
        if (script.Title.Length > 60) notes.Add("Başlık uzun; küçük ekranlarda kesilebilir. Ana konuyu başa taşı.");
        var cover = Regex.Matches(script.ThumbnailText, @"[\p{L}\p{N}]+");
        if (cover.Count > 4) notes.Add("Kapak metni dört sözcükten uzun; mobil görünümde okunabilirliğini kontrol et.");
        var titleWords = Words(script.Title); var coverWords = Words(script.ThumbnailText);
        if (coverWords.Count > 0 && coverWords.Intersect(titleWords).Count() >= Math.Max(1, (int)Math.Ceiling(coverWords.Count * .7))) notes.Add("Kapak metni başlığı büyük ölçüde tekrarlıyor; farklı bir vurgu kullanabilirsin.");
        if (script.Scenes.Count > 0 && script.Scenes[0].Narration.TrimStart().StartsWith("Kanalımıza", StringComparison.OrdinalIgnoreCase)) notes.Add("Açılış kanal tanıtımıyla başlıyor; önce izleyicinin merak ettiği konuya gir.");
        return notes;
    }
}
