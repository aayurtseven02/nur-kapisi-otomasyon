using System.Text;

using System.Text.Json;



namespace AnkaStudio;



public static partial class EditorPreview

{

    public static VideoScript Timed(VideoScript script, double speed)

    {

        var copy = JsonSerializer.Deserialize<VideoScript>(JsonSerializer.Serialize(script))!;

        foreach (var scene in copy.Scenes) scene.Duration = EditorTimeline.Duration(scene, speed);

        return copy;

    }

    static List<Caption> Captions(VideoScript script)

    {

        double offset = 0; var result = new List<Caption>();

        foreach (var scene in script.Scenes) { result.AddRange((scene.SpeechTextHash == TimedNarration.TextHash(scene.Narration) && scene.SpeechCaptions.Count > 0 ? (scene.SubtitleText == "" ? scene.SpeechCaptions : TimedNarration.Redisplay(scene.SpeechCaptions, scene.SubtitleText)) : NarrationFlow.Align(scene.SubtitleText == "" ? scene.Narration : scene.SubtitleText, scene.Duration)).Select(c => new Caption(offset + c.Start, offset + c.End, c.Text))); offset += scene.Duration; }

        return result;

    }

    public static async Task Frame(Store store, VideoScript script, int index, double local, bool vertical, string folder, CancellationToken ct)

    {

        Directory.CreateDirectory(folder); var scene = script.Scenes[index]; int width = vertical ? 1080 : 1920, height = vertical ? 1920 : 1080;

        double offset = script.Scenes.Take(index).Sum(s => s.Duration); local = Math.Clamp(local, 0, Math.Max(0, scene.Duration - .05));

        await File.WriteAllTextAsync(Path.Combine(folder, "preview.ass"), TextDesign.ForVideo(Captions(script), script, width, height), new UTF8Encoding(false), ct);

        var args = new List<string> { "-y" };

        if (File.Exists(scene.StockFile)) { double duration = await Media.Duration(store, scene.StockFile, ct); args.AddRange(["-ss", Media.Num(local % duration), "-i", scene.StockFile]); }

        else args.AddRange(["-f", "lavfi", "-i", $"color=c=0x18283c:s={width}x{height}:r=1"]);

        args.AddRange(["-vf", $"scale={width}:{height}:force_original_aspect_ratio=increase,crop={width}:{height},setsar=1,setpts=PTS+{Media.Num(offset + local)}/TB,subtitles=preview.ass", "-frames:v", "1", "preview.png"]);

        await Media.Exec(store.Ffmpeg, args, folder, ct);

    }

    public static async Task Movie(Store store, VideoScript script, bool vertical, double start, string? narration, string folder, CancellationToken ct)

    {

        Directory.CreateDirectory(folder); double total = script.Scenes.Sum(s => s.Duration); start = Math.Clamp(start, 0, Math.Max(0, total - .2)); double length = Math.Min(6, total - start);

        int width = vertical ? 540 : 960, height = vertical ? 960 : 540; var clips = new List<string>(); double offset = 0;

        for (int i = 0; i < script.Scenes.Count; i++) {

            var scene = script.Scenes[i]; double from = Math.Max(start, offset), to = Math.Min(start + length, offset + scene.Duration); offset += scene.Duration;

            if (to <= from) continue; var args = new List<string> { "-y" };

            if (File.Exists(scene.StockFile)) { double sourceDuration = await Media.Duration(store, scene.StockFile, ct); args.AddRange(["-stream_loop", "-1", "-ss", Media.Num((from - (offset - scene.Duration)) % sourceDuration), "-i", scene.StockFile]); }

            else args.AddRange(["-f", "lavfi", "-i", $"color=c={(i % 2 == 0 ? "0x18283c" : "0x343054")}:s={width}x{height}:r=24"]);

            var file = Path.Combine(folder, "window-scene-" + i + ".mp4");

            args.AddRange(["-vf", $"scale={width}:{height}:force_original_aspect_ratio=increase,crop={width}:{height},setsar=1,fps=24", "-an", "-t", Media.Num(to - from), "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p", "-threads", "2", file]); await Media.Exec(store.Ffmpeg, args, folder, ct); clips.Add(file);

        }

        await File.WriteAllTextAsync(Path.Combine(folder, "window-clips.txt"), string.Join("\n", clips.Select(f => "file '" + Path.GetFileName(f) + "'")), ct);

        var joined = Path.Combine(folder, "window-joined.mp4"); await Media.Exec(store.Ffmpeg, ["-y", "-f", "concat", "-safe", "1", "-i", "window-clips.txt", "-c", "copy", joined], folder, ct);

        await SceneTransitions.Apply(store, clips, script.TextLayout!, 24, folder, joined, ct);

        await File.WriteAllTextAsync(Path.Combine(folder, "window.ass"), TextDesign.ForVideo(Captions(script), script, vertical ? 1080 : 1920, vertical ? 1920 : 1080), new UTF8Encoding(false), ct);

        var final = new List<string> { "-y", "-i", joined };

        if (narration != null && File.Exists(narration)) final.AddRange(["-ss", Media.Num(start), "-i", narration]); else final.AddRange(["-f", "lavfi", "-i", "anullsrc=r=44100:cl=mono"]);

        var music = script.MusicTrack;

        if (music?.Enabled == true) {

            music.Validate(); if (!File.Exists(music.File)) throw new InvalidOperationException("Önizleme müzik dosyası bulunamadı.");

            double source = (music.SourceOffset + Math.Max(0, start - music.Start)) % await Media.Duration(store, music.File, ct);

            if (music.End == 0) music.End = total;

            final.AddRange(["-stream_loop", "-1", "-ss", Media.Num(source), "-i", music.File, "-filter_complex", EditorAudio.Mix(music, length, "1", "2", start, true), "-map", "0:v:0", "-map", "[a]"]);

        } else final.AddRange(["-map", "0:v:0", "-map", "1:a:0"]);

        final.AddRange(["-vf", $"setpts=PTS+{Media.Num(start)}/TB,subtitles=window.ass,setpts=PTS-STARTPTS", "-t", Media.Num(length), "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p", "-c:a", "aac", "-threads", "2", "-movflags", "+faststart", "motion-with-audio.mp4"]);

        await Media.Exec(store.Ffmpeg, final, folder, ct);

        await Media.Exec(store.Ffmpeg, ["-y", "-filter_complex_threads", "1", "-i", "motion-with-audio.mp4", "-filter_complex", "fps=12,split[a][b];[a]palettegen=stats_mode=diff[p];[b][p]paletteuse=dither=bayer", "-an", "-loop", "0", "motion.gif"], folder, ct);

    }

}

