using System.Text.Json;

namespace AnkaStudio;

public static class WorkflowArtwork
{
    public static void SelectCover(Episode episode,Settings settings,CreationFlow flow,string path)
    {
        if(!File.Exists(path))throw new InvalidOperationException("Seçilen kapak dosyası bulunamadı; arşivden yeniden seç.");
        using(var image=Image.FromFile(path)){if((long)image.Width*image.Height>40_000_000)throw new InvalidOperationException("Kapak görseli çok büyük.");}
        settings.CoverMode="Kendi PNG kapak";settings.CoverPng=Path.GetFullPath(path);settings.CoverAiEnabled=false;flow.CoverChoice="file";
        episode.Creation=flow;episode.ProductionSettings??=new Settings();PackageArtwork.Apply(episode.ProductionSettings,settings,PackageArtwork.CoverFields);
        Apply(episode,settings,"file",true);
    }
    public static string Signature(Settings settings) => JsonSerializer.Serialize(
        PackageArtwork.Fields.ToDictionary(name => name, name => typeof(Settings).GetProperty(name)!.GetValue(settings)), Json.Options);

    // A wizard selection applies immediately, including when another step is clicked.
    static string CoverSignature(Settings settings) => JsonSerializer.Serialize(
        PackageArtwork.CoverFields.ToDictionary(name => name, name => typeof(Settings).GetProperty(name)!.GetValue(settings)), Json.Options);

    public static bool Apply(Episode episode, Settings settings, string choice, bool replaceScriptCovers = false)
    {
        if (choice == "legacy") return false;
        var previous = episode.ArtworkSettings ?? episode.ProductionSettings;
        bool changed = replaceScriptCovers || previous != null && Signature(previous) != Signature(settings);
        bool coverChanged = replaceScriptCovers || previous != null && CoverSignature(previous) != CoverSignature(settings);
        if (coverChanged && episode.Plan != null)
            foreach (var row in Rules.Scripts(episode))
                if (row.Script.CoverSettings != null)
                    // Preserve explicitly configured per-video opening/closing cards.
                    PackageArtwork.Apply(row.Script.CoverSettings, settings, PackageArtwork.CoverFields);
        episode.ArtworkSettings = PackageArtwork.Snapshot(settings);
        if (changed && episode.Videos.Count > 0)
        {
            episode.ArtworkPending = true;
            episode.TextDesignPending = true;
            episode.Approved = false;
            episode.ApprovalHash = "";
        }
        return changed;
    }

    public static string ReadyText(Episode episode, Settings settings)
    {
        var scenes = Rules.Scripts(episode).SelectMany(row => row.Script.Scenes).ToArray();
        return scenes.Count(scene => File.Exists(scene.StockFile)) + "/" + scenes.Length + " bölüm görseli hazır";
    }
}
