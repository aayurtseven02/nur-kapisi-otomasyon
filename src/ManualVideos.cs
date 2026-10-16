namespace AnkaStudio;
public static class ManualVideos
{
    public static bool Active(Episode e) => e.UserNarration?.ManualVisuals == true;
    public static List<string> Files(Episode e, int shortIndex = -1)
    {
        var own = e.UserNarration!; var selection = shortIndex < 0 ? null : own.ShortVideos.ElementAtOrDefault(shortIndex);
        return selection?.UseSeparate == true ? selection.Files : own.LocalVideos;
    }
    public static List<string> Files(Episode e, VideoScript script) => Files(e, e.Plan?.Shorts.IndexOf(script) ?? -1);
    public static IEnumerable<string> AllFiles(Episode e) => (e.IncludeLong ? Files(e) : Enumerable.Empty<string>()).Concat(Enumerable.Range(0, e.ShortCount).SelectMany(i => Files(e, i))).Distinct(StringComparer.OrdinalIgnoreCase);
    public static void Validate(Episode e)
    {
        if (!Active(e)) return;
        var lists = (e.IncludeLong ? new[] { (Name: "Normal video", Videos: Files(e)) } : Array.Empty<(string Name, List<string> Videos)>()).Concat(Enumerable.Range(0, e.ShortCount).Select(i => (Name: "Shorts " + (i + 1), Videos: Files(e, i))));
        foreach (var (name, files) in lists) {
        if (files.Count == 0) throw new InvalidOperationException(name + ": manuel görüntü listesi boş. Stok görüntü teması bölümünden videolarını ekle; otomatik arama yapılmayacak.");
        if (files.Count > 120) throw new InvalidOperationException(name + ": en fazla 120 video ekleyebilirsin.");
        foreach (var file in files) {
            if (!File.Exists(file)) throw new InvalidOperationException("Seçtiğin video bulunamadı: " + file + ". Dosyayı yeniden seç; otomatik arama yapılmayacak.");
            if (!new[] { ".mp4", ".mov", ".mkv", ".webm" }.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase)) throw new InvalidOperationException("Desteklenmeyen video dosyası: " + file);
        }
        }
    }
    public static void Apply(Episode e, ContentPlan plan)
    {
        if (!Active(e)) return;
        Validate(e); var own = e.UserNarration!;
        var selection = System.Text.Json.JsonSerializer.Serialize(new { own.LocalVideos, own.ShortVideos, e.IncludeLong, e.ShortCount });
        bool sameSelection = own.AppliedVideoSelection == selection;
        var scripts = (e.IncludeLong && plan.Long != null ? new[] { (Script: plan.Long, Index: -1) } : Array.Empty<(VideoScript Script, int Index)>()).Concat(plan.Shorts.Take(e.ShortCount).Select((s, i) => (Script: s, Index: i)));
        foreach (var (script, index) in scripts) { var files = Files(e, index); for (int i = 0; i < script.Scenes.Count; i++) {
            var scene = script.Scenes[i];
            var file = sameSelection && scene.UserLocal && files.Contains(scene.StockFile, StringComparer.OrdinalIgnoreCase) ? scene.StockFile : files[i % files.Count];
            bool confirmed = scene.UserLocal && scene.StockFile == file && scene.RightsConfirmed;
            SceneVideoChoice.Apply(scene, file, null); scene.RightsConfirmed = own.LocalVideoRightsConfirmed || confirmed;
        }
        }
        own.AppliedVideoSelection = selection;
    }
}
