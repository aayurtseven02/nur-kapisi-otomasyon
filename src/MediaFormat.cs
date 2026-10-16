using System.Text.Json;
namespace AnkaStudio;

public static class MediaFormat
{
    public static void Dimensions(double width, double height, bool vertical, string name)
    {
        double expected = vertical ? 9.0 / 16 : 16.0 / 9;
        if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0 || Math.Abs(width / height / expected - 1) > .01)
            throw new InvalidOperationException($"{name}: {(vertical ? "Shorts için 9:16 dikey" : "Normal video için 16:9 yatay")} formatında olmalıdır. Seçilen boyut: {width:0} × {height:0}. Uygun dosyayı yeniden seç.");
    }
    public static void Image(string path, bool vertical)
    {
        using var image = System.Drawing.Image.FromFile(path);
        Dimensions(image.Width, image.Height, vertical, Path.GetFileName(path));
    }
    public static void Probe(JsonElement stream, bool vertical, string name)
    {
        double width = stream.GetProperty("width").GetDouble(), height = stream.GetProperty("height").GetDouble();
        if (stream.TryGetProperty("sample_aspect_ratio", out var sar)) {
            var parts = (sar.GetString() ?? "").Split(':');
            if (parts.Length == 2 && double.TryParse(parts[0], out var n) && double.TryParse(parts[1], out var d) && n > 0 && d > 0) width *= n / d;
        }
        double rotation = 0;
        if (stream.TryGetProperty("tags", out var tags) && tags.TryGetProperty("rotate", out var rotate)) double.TryParse(rotate.GetString(), out rotation);
        if (stream.TryGetProperty("side_data_list", out var side)) foreach (var entry in side.EnumerateArray()) if (entry.TryGetProperty("rotation", out var value)) rotation = value.GetDouble();
        if (Math.Abs(rotation % 180) > 45 && Math.Abs(rotation % 180) < 135) (width, height) = (height, width);
        Dimensions(width, height, vertical, name);
    }
    public static async Task Video(Store store, string path, bool vertical, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(20));
        var raw = await Media.Exec(store.Probe, ["-v", "error", "-select_streams", "v:0", "-show_streams", "-of", "json", path], null, timeout.Token);
        using var doc = JsonDocument.Parse(raw); var streams = doc.RootElement.GetProperty("streams");
        if (streams.GetArrayLength() == 0) throw new InvalidOperationException("Dosyada video görüntüsü bulunamadı: " + Path.GetFileName(path));
        Probe(streams[0], vertical, Path.GetFileName(path));
    }
    public static async Task Manual(Store store, Episode episode, CancellationToken ct)
    {
        if (!ManualVideos.Active(episode)) return;
        if (episode.IncludeLong) foreach (var file in ManualVideos.Files(episode)) await Video(store, file, false, ct);
        for (int i = 0; i < episode.ShortCount; i++) foreach (var file in ManualVideos.Files(episode, i)) await Video(store, file, true, ct);
    }
}
