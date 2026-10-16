using System.Text;

namespace AnkaStudio;

public static class SceneTransitions
{
    public static string Filter(string name) => name switch {
        "Yumuşak karışım" => "fade", "Siyaha geçiş" => "fadeblack", "Beyaza geçiş" => "fadewhite",
        "Sola silme" => "wipeleft", "Sağa silme" => "wiperight", "Yukarı kayma" => "slideup", "Dairesel açılma" => "circleopen",
        _ => throw new InvalidOperationException("Geçiş seçimi geçersiz.")
    };
    // Clone the incoming first frame during overlap: narration and scene boundaries keep their original times.
    // Four inputs per batch bounds decoder memory even for long multi-scene videos.
    public static async Task Apply(Store store, IReadOnlyList<string> clips, TextLayout design, int fps, string folder, string joined, CancellationToken ct)
    {
        design.Validate(); if (design.Transition == "Kesme" || clips.Count < 2) return;
        var nodes = new List<(string File, double Duration)>();
        foreach (var clip in clips) nodes.Add((clip, await Media.Duration(store, clip, ct)));
        double fade = Math.Floor(Math.Min(design.TransitionSeconds, nodes.Min(n => n.Duration) / 3) * fps) / fps;
        if (fade < 1d / fps) return;
        int stage = 0;
        while (nodes.Count > 1) {
            var next = new List<(string File, double Duration)>(); int group = 0;
            foreach (var batch in nodes.Chunk(4)) {
                ct.ThrowIfCancellationRequested(); group++;
                if (batch.Length == 1) { next.Add(batch[0]); continue; }
                var args = new List<string> { "-y", "-filter_complex_threads", "1" };
                foreach (var item in batch) args.AddRange(["-i", item.File]);
                var graph = new StringBuilder();
                for (int i = 0; i < batch.Length; i++) graph.Append($"[{i}:v]fps={fps},settb=AVTB,setpts=PTS-STARTPTS,format=yuv420p" + (i > 0 ? $",tpad=start_mode=clone:start_duration={Media.Num(fade)}" : "") + $"[v{i}];");
                double offset = batch[0].Duration; string previous = "v0";
                for (int i = 1; i < batch.Length; i++) { graph.Append($"[{previous}][v{i}]xfade=transition={Filter(design.Transition)}:duration={Media.Num(fade)}:offset={Media.Num(offset - fade)}[x{i}];"); previous = "x" + i; offset += batch[i].Duration; }
                var output = Path.Combine(folder, $"transition-{stage}-{group}.mp4");
                args.AddRange(["-filter_complex", graph.ToString().TrimEnd(';'), "-map", $"[{previous}]", "-an", "-t", Media.Num(offset), "-c:v", "libx264", "-preset", store.Settings.EncodingPreset, "-crf", store.Settings.Crf.ToString(), "-pix_fmt", "yuv420p", "-threads", "2", output]);
                await Media.Exec(store.Ffmpeg, args, folder, ct); next.Add((output, await Media.Duration(store, output, ct)));
            }
            nodes = next; stage++;
        }
        var muxed = Path.Combine(folder, "joined-transitions.mp4");
        await Media.Exec(store.Ffmpeg, ["-y", "-i", nodes[0].File, "-i", joined, "-map", "0:v:0", "-map", "1:a:0?", "-c", "copy", muxed], folder, ct);
        ct.ThrowIfCancellationRequested(); File.Move(muxed, joined, true);
    }
}
