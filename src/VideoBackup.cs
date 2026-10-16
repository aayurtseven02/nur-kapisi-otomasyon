using System.Security.Cryptography;

using System.Text.Json;

using System.Text.RegularExpressions;

namespace AnkaStudio;

public static class VideoBackup

{

    public static async Task Save(Store store, Episode episode, CancellationToken ct)

    {

        if (string.IsNullOrWhiteSpace(store.Settings.VideoBackupFolder)) return;

        if (!Regex.IsMatch(episode.Id, "^[A-Za-z0-9_-]+$")) throw new InvalidOperationException("Yedek paket kimliği geçersiz.");

        var root = Path.GetFullPath(store.Settings.VideoBackupFolder);

        var folder = Path.Combine(root, episode.Id, "r" + episode.Revision); Directory.CreateDirectory(folder);

        foreach (var video in episode.Videos)

        {

            var name = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(video.Key)))[..16];

            await Copy(video.File, Path.Combine(folder, name + ".mp4"), video.FileHash, ct);

            if (File.Exists(video.Thumbnail)) await Copy(video.Thumbnail, Path.Combine(folder, name + Path.GetExtension(video.Thumbnail)), "", ct);

        }

        var manifest = JsonSerializer.Serialize(new { episode.Id, episode.Topic, episode.Revision, episode.Plan, SavedAt = DateTimeOffset.UtcNow, Videos = episode.Videos.Select(v => new { v.Key, v.Title, v.FileHash, v.PublishAt, v.YouTubeId }) }, Json.Options);

        var temp = Path.Combine(folder, "manifest-" + Guid.NewGuid().ToString("N") + ".tmp");

        await File.WriteAllTextAsync(temp, manifest, ct); File.Move(temp, Path.Combine(folder, "manifest.json"), true);

    }

    static async Task Copy(string source, string target, string expected, CancellationToken ct)

    {

        await using var input = File.OpenRead(source);

        var hash = Convert.ToHexString(await SHA256.HashDataAsync(input, ct));

        if (expected != "" && hash != expected) throw new InvalidOperationException("Yedeklenecek video değişmiş; yeniden kontrol et.");

        if (File.Exists(target)) { await using var existing = File.OpenRead(target); if (Convert.ToHexString(await SHA256.HashDataAsync(existing, ct)) == hash) return; }

        if (Path.GetFullPath(source).Equals(Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Yedek klasörü kaynak dosyayla aynı olamaz.");

        var temp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";

        try {

            input.Position = 0;

            await using (var output = File.Create(temp)) await input.CopyToAsync(output, ct);

            await using (var verify = File.OpenRead(temp)) if (Convert.ToHexString(await SHA256.HashDataAsync(verify, ct)) != hash) throw new IOException("Yedek doğrulanamadı.");

            File.Move(temp, target, true);

        } finally { if (File.Exists(temp)) File.Delete(temp); }

    }

}

