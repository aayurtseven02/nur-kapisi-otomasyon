namespace AnkaStudio;



public static class ClosingCards

{

    public static void Validate(Settings settings, bool? vertical = null)

    {

        foreach (var card in new[] { (settings.OpeningCardEnabled, settings.OpeningCardFile, settings.OpeningCardSeconds), (settings.ClosingCardEnabled, settings.ClosingCardFile, settings.ClosingCardSeconds) }) {

            if (!card.Item1) continue;

            if (!File.Exists(card.Item2)) throw new InvalidOperationException("Seçilen açılış/kapanış kartı bulunamadı. Kapak tasarımları bölümünden yeniden seç.");

            if (!double.IsFinite(card.Item3) || card.Item3 < 1 || card.Item3 > 15) throw new InvalidOperationException("Kart süresi 1–15 saniye olmalı.");

            if (vertical.HasValue) MediaFormat.Image(card.Item2, vertical.Value);

            using var image = System.Drawing.Image.FromFile(card.Item2); if ((long)image.Width * image.Height > 40_000_000) throw new InvalidOperationException("Kart görseli çok büyük.");

        }

    }

    public static async Task Append(Store store, Settings settings, string output, string folder, int width, int height, CancellationToken ct, bool prepend = false)

    {

        if (!File.Exists(settings.ClosingCardFile)) throw new InvalidOperationException("Seçilen kapanış kartı bulunamadı; Kapak tasarımları bölümünden yeniden seç.");

        if (!double.IsFinite(settings.ClosingCardSeconds) || settings.ClosingCardSeconds < 1 || settings.ClosingCardSeconds > 15) throw new InvalidOperationException("Kapanış kartı süresi 1–15 saniye olmalı.");

        using (var image = System.Drawing.Image.FromFile(settings.ClosingCardFile)) if ((long)image.Width * image.Height > 40_000_000) throw new InvalidOperationException("Kapanış kartı en fazla 40 milyon piksel olabilir.");

        MediaFormat.Image(settings.ClosingCardFile, height > width);

        var ending = Path.Combine(folder, "closing-card.mp4"); var finished = Path.Combine(folder, "with-closing.mp4");

        await Media.Exec(store.Ffmpeg, ["-y", "-loop", "1", "-i", settings.ClosingCardFile, "-f", "lavfi", "-i", "anullsrc=r=44100:cl=stereo", "-vf", $"scale={width}:{height}:force_original_aspect_ratio=decrease,pad={width}:{height}:(ow-iw)/2:(oh-ih)/2:color=0x081420,setsar=1,fps={settings.FramesPerSecond}", "-t", Media.Num(settings.ClosingCardSeconds), "-c:v", "libx264", "-preset", settings.EncodingPreset, "-crf", settings.Crf.ToString(), "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "192k", ending], folder, ct);

        await Media.Exec(store.Ffmpeg, ["-y", "-i", prepend ? ending : output, "-i", prepend ? output : ending, "-filter_complex", "[0:v]setpts=PTS-STARTPTS[v0];[1:v]setpts=PTS-STARTPTS[v1];[0:a]aresample=44100,aformat=channel_layouts=stereo,asetpts=PTS-STARTPTS[a0];[1:a]asetpts=PTS-STARTPTS[a1];[v0][a0][v1][a1]concat=n=2:v=1:a=1[v][a]", "-map", "[v]", "-map", "[a]", "-c:v", "libx264", "-preset", settings.EncodingPreset, "-crf", settings.Crf.ToString(), "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "192k", "-movflags", "+faststart", finished], folder, ct);

        ct.ThrowIfCancellationRequested(); File.Move(finished, output, true);

    }

}

