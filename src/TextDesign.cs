using System.Globalization;

using System.Text;

using System.Text.RegularExpressions;



namespace AnkaStudio;



public sealed class TextLayout

{

    public string Font { get; set; } = "Segoe UI";

    public string TitleFont { get; set; } = "";

    public string SubtitleFont { get; set; } = "";

    public bool Italic { get; set; }

    public bool Underline { get; set; }

    public int Spacing { get; set; }

    public int ScaleX { get; set; } = 100;

    public int Rotation { get; set; }

    public int Opacity { get; set; } = 100;

    public string OutlineColor { get; set; } = "000000";

    public string ShadowColor { get; set; } = "000000";

    public string GlowColor { get; set; } = "FF8800";

    public string BackgroundColor { get; set; } = "000000";

    public int BackgroundOpacity { get; set; } = 65;

    public int Shadow { get; set; } = 1;

    public string Effect { get; set; } = "Düz";

    public int TitleAlignment { get; set; } = 2;

    public int SubtitleAlignment { get; set; } = 2;

    public string TitleAnimation { get; set; } = "Yok";

    public string SubtitleAnimation { get; set; } = "Yok";

    public bool FadeOut { get; set; }

    public bool Karaoke { get; set; }

    public string KaraokeColor { get; set; } = "808080";

    public double AnimationSeconds { get; set; } = .35;

    public string Transition { get; set; } = "Kesme";

    public double TransitionSeconds { get; set; } = .5;

    public static readonly string[] Effects = ["Düz", "Gölge", "Neon parıltı", "3D kabartma"];

    public static readonly string[] Animations = ["Yok", "Soluk giriş", "Alttan kayma", "Soldan kayma", "Sağdan kayma", "Yakınlaşma", "Vurgu / pop"];

    public static readonly string[] Transitions = ["Kesme", "Yumuşak karışım", "Siyaha geçiş", "Beyaza geçiş", "Sola silme", "Sağa silme", "Yukarı kayma", "Dairesel açılma"];

    public static bool SafeFont(string value) => Regex.IsMatch(value, @"^[\p{L}\p{N} ._\-]{1,80}$");

    public bool AutoSceneTitles { get; set; }

    public bool TitleEnabled { get; set; } = true;

    public bool SubtitleEnabled { get; set; } = true;

    public int TitleSize { get; set; } = 40;

    public int SubtitleSize { get; set; } = 46;

    public string TitleColor { get; set; } = "FFFFFF";

    public string SubtitleColor { get; set; } = "FFFFFF";

    public double TitleX { get; set; } = 50;

    public double TitleY { get; set; } = 7;

    public double SubtitleX { get; set; } = 50;

    public double SubtitleY { get; set; } = 92;

    public double TitleSeconds { get; set; } = 4;

    public bool Bold { get; set; } = true;

    public bool TitleBackground { get; set; } = true;

    public bool SubtitleBackground { get; set; }

    public int Outline { get; set; } = 3;

    public static TextLayout Defaults(Settings settings, bool vertical) => new() {

        TitleSize = vertical ? 48 : 40, SubtitleSize = vertical ? settings.ShortFontSize : settings.LongFontSize,

        SubtitleColor = settings.SubtitleColor, TitleY = vertical ? 10 : 7, SubtitleY = vertical ? 85 : 92

    };

    public void Validate()

    {

        if (!SafeFont(Font) || (TitleFont != "" && !SafeFont(TitleFont)) || (SubtitleFont != "" && !SafeFont(SubtitleFont)) || TitleSize < 20 || TitleSize > 180 || SubtitleSize < 20 || SubtitleSize > 180 || Outline < 0 || Outline > 12 ||

            new[] { OutlineColor, ShadowColor, GlowColor, BackgroundColor, KaraokeColor }.Any(c => !Regex.IsMatch(c, "^[a-fA-F0-9]{6}$")) ||

            !Effects.Contains(Effect) || !Animations.Contains(TitleAnimation) || !Animations.Contains(SubtitleAnimation) || !Transitions.Contains(Transition) ||

            TitleAlignment is < 1 or > 3 || SubtitleAlignment is < 1 or > 3 || Shadow is < 0 or > 12 || Spacing is < 0 or > 15 || ScaleX is < 50 or > 150 || Rotation is < -30 or > 30 || Opacity is < 10 or > 100 || BackgroundOpacity is < 0 or > 100 ||

            !double.IsFinite(AnimationSeconds) || AnimationSeconds is < .1 or > 2 || !double.IsFinite(TransitionSeconds) || TransitionSeconds is < .1 or > 2 ||

            !Regex.IsMatch(TitleColor, "^[a-fA-F0-9]{6}$") || !Regex.IsMatch(SubtitleColor, "^[a-fA-F0-9]{6}$") ||

            new[] { TitleX, TitleY, SubtitleX, SubtitleY }.Any(v => !double.IsFinite(v) || v < 5 || v > 95) || !double.IsFinite(TitleSeconds) || TitleSeconds < .5 || TitleSeconds > 60)

            throw new InvalidOperationException("Yazı ayarlarında boyut, renk veya konum geçersiz.");

    }

}



public static class TextDesign

{

    static string N(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    public static string Color(string rgb) => "&H00" + rgb[4..6] + rgb[2..4] + rgb[..2];

    static string Time(double value) { var t = TimeSpan.FromSeconds(Math.Max(0, value)); return $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}.{t.Milliseconds / 10:00}"; }

    static string Escape(string value) => value.Replace("\\", "\\\\").Replace("{", "(").Replace("}", ")").Replace("\r", "").Replace("\n", "\\N");

    public static string Ass(IEnumerable<Caption> captions, IEnumerable<(double Start, double End, string Text)> titles, TextLayout layout, int width, int height, bool limitTitles = true, IReadOnlyList<string>? titlePlacements = null)

    {

        layout.Validate();

        var text = new StringBuilder($"[Script Info]\nScriptType: v4.00+\nPlayResX: {width}\nPlayResY: {height}\nWrapStyle: 0\nScaledBorderAndShadow: yes\n\n[V4+ Styles]\nFormat: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding\n");

        string AssColor(string rgb, int opacity) => "&H" + ((int)Math.Round(255 * (1 - opacity / 100d))).ToString("X2") + rgb[4..6] + rgb[2..4] + rgb[..2];

        void Style(string name, string font, int size, string color, bool background, int align) => text.AppendLine($"Style: {name},{font},{size},{AssColor(color, layout.Opacity)},{AssColor(layout.KaraokeColor, layout.Opacity)},{AssColor(layout.OutlineColor, layout.Opacity)},{AssColor(background ? layout.BackgroundColor : layout.ShadowColor, background ? layout.BackgroundOpacity : layout.Opacity)},{(layout.Bold ? -1 : 0)},{(layout.Italic ? -1 : 0)},{(layout.Underline ? -1 : 0)},0,{layout.ScaleX},100,{layout.Spacing},{layout.Rotation},{(background ? 3 : 1)},{layout.Outline},{(layout.Effect == "Gölge" ? Math.Max(4, layout.Shadow) : layout.Shadow)},{align},{width / 12},{width / 12},0,1");

        Style("Title", layout.TitleFont == "" ? layout.Font : layout.TitleFont, layout.TitleSize, layout.TitleColor, layout.TitleBackground, 6 + layout.TitleAlignment);

        Style("Subtitle", layout.SubtitleFont == "" ? layout.Font : layout.SubtitleFont, layout.SubtitleSize, layout.SubtitleColor, layout.SubtitleBackground, layout.SubtitleAlignment);

        text.AppendLine("\n[Events]\nFormat: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text");

        void Event(string style, double start, double end, string value, double x, double y, string placement = "") {

            if (string.IsNullOrWhiteSpace(value) || end <= start) return;

            double px = width * x / 100, py = height * y / 100;

            TitlePlacement.Fitted? fitted = null;

            if (style == "Title") { fitted = TitlePlacement.Fit(value, layout, width, height, placement); value = fitted.Text; px = fitted.X; py = fitted.Y; }

            int ms = (int)Math.Min(layout.AnimationSeconds * 1000, (end - start) * 450);

            string animation = style == "Title" ? layout.TitleAnimation : layout.SubtitleAnimation;

            string position = $"\\pos({N(px)},{N(py)})";

            if (animation.Contains("kayma")) {

                double sx = animation == "Soldan kayma" ? px - width * .15 : animation == "Sağdan kayma" ? px + width * .15 : px;

                double sy = animation == "Alttan kayma" ? py + height * .08 : py;

                if (fitted != null) { double margin = Math.Max(24, width * .055); sx = Math.Clamp(sx, margin - fitted.MinX, width - margin - fitted.MaxX); sy = Math.Clamp(sy, margin - fitted.MinY, height - margin - fitted.MaxY); }

                position = $"\\move({N(sx)},{N(sy)},{N(px)},{N(py)},0,{ms})";

            }

            if (fitted != null) position += "\\an" + fitted.Alignment + "\\q2";

            if (animation == "Yakınlaşma") position += $"\\fscx{layout.ScaleX * .6:0}\\fscy60\\t(0,{ms},\\fscx{layout.ScaleX}\\fscy100)";

            if (animation == "Vurgu / pop") position += $"\\fscx{layout.ScaleX * 1.25:0}\\fscy125\\t(0,{ms},\\fscx{layout.ScaleX}\\fscy100)";

            if (animation != "Yok" || layout.FadeOut) position += $"\\fad({(animation == "Yok" ? 0 : ms)},{(layout.FadeOut ? ms : 0)})";

            string content = Escape(value);

            if (style == "Subtitle" && layout.Karaoke) {

                var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries); int total = Math.Max(1, words.Sum(w => w.Length));

                content = string.Join(" ", words.Select(w => $"{{\\kf{Math.Max(1, (int)Math.Round((end - start) * 100 * w.Length / total))}}}" + Escape(w)));

            }

            void Line(int layer, string extra, string body) => text.AppendLine($"Dialogue: {layer},{Time(start)},{Time(end)},{style},,0,0,0,,{{{position}{extra}}}{body}");

            if (layout.Effect == "Neon parıltı") Line(style == "Title" ? 2 : 0, $"\\bord8\\blur6\\3c{Color(layout.GlowColor)}\\shad0", Escape(value));

            if (layout.Effect == "3D kabartma") Line(style == "Title" ? 2 : 0, $"\\xshad5\\yshad5\\4c{Color(layout.ShadowColor)}\\bord2", Escape(value));

            Line(style == "Title" ? 3 : 1, layout.Effect == "3D kabartma" ? "\\shad1" : "", content);

        }

        if (layout.SubtitleEnabled) foreach (var c in captions) Event("Subtitle", c.Start, c.End, c.Text, layout.SubtitleX, layout.SubtitleY);

        int titleIndex = 0; if (layout.TitleEnabled) foreach (var t in titles) { Event("Title", t.Start, limitTitles ? Math.Min(t.End, t.Start + layout.TitleSeconds) : t.End, t.Text, layout.TitleX, layout.TitleY, titlePlacements?.ElementAtOrDefault(titleIndex) ?? ""); titleIndex++; }

        return text.ToString();

    }

    public static string ForVideo(IEnumerable<Caption> captions, VideoScript script, int width, int height)

    {

        double offset = 0; var titles = new List<(double, double, string)>();

        foreach (var scene in script.Scenes) {

            double start = Math.Min(scene.TitleStart, scene.Duration); double length = scene.TitleDuration > 0 ? scene.TitleDuration : script.TextLayout!.TitleSeconds;

            // Per-scene duration overrides the global default; clip to the scene's measured end.

            titles.Add((offset + start, Math.Min(offset + scene.Duration, offset + start + length), scene.OnScreen)); offset += scene.Duration;

        }

        return Ass(captions, titles, script.TextLayout!, width, height, limitTitles: false, titlePlacements: script.Scenes.Select(s => script.TextLayout!.AutoSceneTitles ? s.TitlePlacement : "").ToArray());

    }

    public static async Task Preview(Store store, Scene scene, TextLayout layout, bool vertical, int sample, string folder, CancellationToken ct)

    {

        Directory.CreateDirectory(folder); var width = vertical ? 1080 : 1920; var height = vertical ? 1920 : 1080;

        var captions = Engine.SplitText(scene.SubtitleText == "" ? scene.Narration : scene.SubtitleText, 48);

        var value = captions.ElementAtOrDefault(Math.Clamp(sample, 0, Math.Max(0, captions.Count - 1))) ?? "Altyazı önizlemesi";

        await File.WriteAllTextAsync(Path.Combine(folder, "preview.ass"), Ass([new Caption(0, 10, value)], [(0d, 10d, scene.OnScreen)], layout, width, height), new UTF8Encoding(false), ct);

        var args = new List<string> { "-y" };

        if (File.Exists(scene.StockFile)) args.AddRange(["-stream_loop", "-1", "-ss", "0", "-i", scene.StockFile]);

        else args.AddRange(["-f", "lavfi", "-i", $"color=c=0x18283c:s={width}x{height}:r=1"]);

        args.AddRange(["-vf", $"scale={width}:{height}:force_original_aspect_ratio=increase,crop={width}:{height},setsar=1,subtitles=preview.ass", "-ss", "1", "-frames:v", "1", "preview.png"]);

        await Media.Exec(store.Ffmpeg, args, folder, ct);

    }

    public static async Task MotionPreview(Store store, Scene scene, Scene next, TextLayout layout, bool vertical, int sample, string folder, CancellationToken ct)

    {

        Directory.CreateDirectory(folder); int width = vertical ? 540 : 960, height = vertical ? 960 : 540; const int fps = 24;

        var inputs = new[] { scene, next }; var clips = new List<string>();

        for (int i = 0; i < inputs.Length; i++) {

            var args = new List<string> { "-y" }; var source = inputs[i];

            if (File.Exists(source.StockFile)) args.AddRange(["-stream_loop", "-1", "-i", source.StockFile]);

            else args.AddRange(["-f", "lavfi", "-i", $"color=c={(i == 0 ? "0x18283c" : "0x343054")}:s={width}x{height}:r={fps}"]);

            var file = Path.Combine(folder, "motion-scene-" + i + ".mp4");

            args.AddRange(["-vf", $"scale={width}:{height}:force_original_aspect_ratio=increase,crop={width}:{height},setsar=1,fps={fps}", "-an", "-t", "3", "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p", "-threads", "2", file]);

            await Media.Exec(store.Ffmpeg, args, folder, ct); clips.Add(file);

        }

        await File.WriteAllTextAsync(Path.Combine(folder, "motion-list.txt"), string.Join("\n", clips.Select(f => "file '" + Path.GetFileName(f) + "'")), ct);

        var joined = Path.Combine(folder, "motion-joined.mp4");

        await Media.Exec(store.Ffmpeg, ["-y", "-f", "concat", "-safe", "1", "-i", "motion-list.txt", "-c", "copy", joined], folder, ct);

        await SceneTransitions.Apply(store, clips, layout, fps, folder, joined, ct);

        var captions = new List<Caption>();

        for (int i = 0; i < inputs.Length; i++) {

            var pieces = Engine.SplitText(inputs[i].SubtitleText == "" ? inputs[i].Narration : inputs[i].SubtitleText, 48);

            string value = pieces.ElementAtOrDefault(Math.Clamp(sample, 0, Math.Max(0, pieces.Count - 1))) ?? "Altyazı önizlemesi";

            captions.Add(new Caption(i * 3, i * 3 + 3, value));

        }

        await File.WriteAllTextAsync(Path.Combine(folder, "motion.ass"), Ass(captions, [(0d, 3d, scene.OnScreen), (3d, 6d, next.OnScreen)], layout, vertical ? 1080 : 1920, vertical ? 1920 : 1080), new UTF8Encoding(false), ct);

        await Media.Exec(store.Ffmpeg, ["-y", "-filter_complex_threads", "1", "-i", joined, "-filter_complex", "subtitles=motion.ass,fps=12,split[a][b];[a]palettegen=stats_mode=diff[p];[b][p]paletteuse=dither=bayer", "-t", "6", "-loop", "0", "motion.gif"], folder, ct);

    }

}

