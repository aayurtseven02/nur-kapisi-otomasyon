using System.Drawing.Drawing2D;



namespace AnkaStudio;



public sealed class EditorTimeline : Control

{

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)] public VideoScript? Script { get; set; }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)] public double VoiceSpeed { get; set; } = 1;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)] public int SelectedScene { get; set; }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)] public double Position { get; set; } = 1;

    public event Action<int>? SceneSelected;

    public event Action<double>? Seeked;

    public event Action? Edited;

    readonly List<(RectangleF Rect, int Scene, double Start, double End)> titles = [];

    RectangleF musicRect;

    int dragScene = -1, dragMode;

    double originX, originStart, originEnd;

    bool changed;

    public EditorTimeline() { DoubleBuffered = true; Height = 210; BackColor = Theme.Bg; Cursor = Cursors.Hand; SetStyle(ControlStyles.ResizeRedraw, true); }

    public static double Duration(Scene scene, double speed = 1) => scene.Duration > 0 ? scene.Duration : Math.Max(1, scene.Narration.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length / (2.2 * Math.Max(.5, speed)));

    public double Total => Script == null ? 1 : Math.Max(1, Script.Scenes.Sum(s => Duration(s, VoiceSpeed)));

    double X(double seconds) => 105 + seconds / Total * Math.Max(1, Width - 120);

    double Seconds(double x) => Math.Clamp((x - 105) / Math.Max(1, Width - 120) * Total, 0, Total);

    public double SceneStart(int index) => Script == null ? 0 : Script.Scenes.Take(index).Sum(s => Duration(s, VoiceSpeed));

    protected override void OnPaint(PaintEventArgs e)

    {

        base.OnPaint(e); var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; titles.Clear(); musicRect = RectangleF.Empty;

        using var font = Theme.Font(9); using var pen = new Pen(Color.FromArgb(60, 94, 228, 210)); using var text = new SolidBrush(Theme.Text); using var teal = new SolidBrush(Theme.Teal);

        if (Script == null) return;

        double step = Total <= 30 ? 2 : Total <= 120 ? 10 : Total <= 600 ? 30 : 120;

        for (double t = 0; t <= Total; t += step) { float x = (float)X(t); g.DrawLine(pen, x, 20, x, Height - 8); g.DrawString(t.ToString("0") + " sn", font, text, x + 2, 2); }

        string[] names = ["Sahneler", "Başlık", "Altyazı", "Müzik"];

        for (int i = 0; i < 4; i++) { int y = 40 + i * 40; using var lane = new SolidBrush(Theme.Elevated); g.FillRectangle(lane, 105, y, Width - 120, 34); g.DrawString(names[i].ToUpper(System.Globalization.CultureInfo.GetCultureInfo("tr-TR")), font, text, 5, y + 9); }

        double offset = 0;

        for (int i = 0; i < Script.Scenes.Count; i++) {

            var scene = Script.Scenes[i]; double duration = Duration(scene, VoiceSpeed); float left = (float)X(offset), size = (float)(X(offset + duration) - left);

            using var sceneBrush = new SolidBrush(i == SelectedScene ? Color.FromArgb(60, 139, 170) : Color.FromArgb(52, 63, 87)); g.FillRectangle(sceneBrush, left + 1, 41, Math.Max(1, size - 2), 32);

            TextRenderer.DrawText(g, "S" + (i + 1), font, Rectangle.Round(new RectangleF(left + 3, 41, Math.Max(1, size - 5), 32)), Color.White, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            if (Script.TextLayout?.TitleEnabled != false && scene.OnScreen != "") {

                double start = offset + Math.Min(scene.TitleStart, duration), end = Math.Min(offset + duration, start + (scene.TitleDuration > 0 ? scene.TitleDuration : Script.TextLayout?.TitleSeconds ?? 4));

                if (end > start) {

                var rect = new RectangleF((float)X(start), 81, Math.Max(2, (float)(X(end) - X(start))), 32); titles.Add((rect, i, start, end));

                using var titleBrush = new SolidBrush(Color.FromArgb(225, 134, 30)); g.FillRectangle(titleBrush, rect); TextRenderer.DrawText(g, scene.OnScreen.Replace('\n', ' '), font, Rectangle.Round(rect), Theme.Bg, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                using var edge = new Pen(Theme.Teal, 3); g.DrawLine(edge, rect.Left + 1, rect.Top + 5, rect.Left + 1, rect.Bottom - 5); g.DrawLine(edge, rect.Right - 1, rect.Top + 5, rect.Right - 1, rect.Bottom - 5);

                }

            }

            if (Script.TextLayout?.SubtitleEnabled != false) { using var sub = new SolidBrush(Color.FromArgb(65, 71, 92)); g.FillRectangle(sub, left + 1, 121, Math.Max(1, size - 2), 32); }

            offset += duration;

        }

        var music = Script.MusicTrack;

        if (music?.Enabled == true) { double end = music.End > 0 ? Math.Min(music.End, Total) : Total; musicRect = new RectangleF((float)X(Math.Min(music.Start, Total)), 161, Math.Max(2, (float)(X(end) - X(Math.Min(music.Start, Total)))), 32); g.FillRectangle(teal, musicRect); TextRenderer.DrawText(g, Path.GetFileName(music.File), font, Rectangle.Round(musicRect), Theme.Bg, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis); }

        else g.DrawString("+ Müzik sekmesinden dosya / arşiv seç", font, text, 113, 170);

        using var cursor = new Pen(Color.FromArgb(255, 65, 80), 2); float marker = (float)X(Position); g.DrawLine(cursor, marker, 20, marker, Height - 8);

    }

    (RectangleF Rect, int Scene, double Start, double End)? HitTitle(Point point)

    {

        var candidates = titles.Where(t => point.Y >= t.Rect.Top && point.Y <= t.Rect.Bottom && point.X >= t.Rect.Left - 10 && point.X <= t.Rect.Right + 10).ToArray();

        if (candidates.Length == 0) return null;

        return candidates.OrderBy(t => Math.Min(Math.Abs(point.X - t.Rect.Left), Math.Abs(point.X - t.Rect.Right))).First();

    }

    protected override void OnMouseDown(MouseEventArgs e)

    {

        base.OnMouseDown(e); if (e.Button != MouseButtons.Left || Script == null) return;

        changed = false; dragScene = -1; dragMode = 0;

        var hit = HitTitle(e.Location);

        if (hit is { } t) {

            double leftDistance = Math.Abs(e.X - t.Rect.Left), rightDistance = Math.Abs(e.X - t.Rect.Right);

            dragScene = t.Scene; dragMode = Math.Min(leftDistance, rightDistance) <= 10 ? (rightDistance <= leftDistance ? 2 : 1) : 3;

            originStart = t.Start; originEnd = t.End; originX = Seconds(e.X);

            SceneSelected?.Invoke(t.Scene); Capture = true; return;

        }

        if (musicRect.Contains(e.Location) && Script.MusicTrack != null) { dragMode = Math.Abs(e.X - musicRect.Right) < 8 ? 5 : 4; originX = Seconds(e.X); originStart = Script.MusicTrack.Start; originEnd = Script.MusicTrack.End > 0 ? Math.Min(Script.MusicTrack.End, Total) : Total; Capture = true; return; }

        double time = Seconds(e.X); int index = 0; while (index < Script.Scenes.Count - 1 && SceneStart(index + 1) <= time) index++;

        SceneSelected?.Invoke(index); Position = time; Seeked?.Invoke(time); Invalidate();

    }

    protected override void OnMouseMove(MouseEventArgs e)

    {

        base.OnMouseMove(e);

        if (!Capture || Script == null || dragMode == 0) {

            var hit = HitTitle(e.Location);

            Cursor = hit is { } t ? (Math.Min(Math.Abs(e.X - t.Rect.Left), Math.Abs(e.X - t.Rect.Right)) <= 10 ? Cursors.SizeWE : Cursors.SizeAll) : Cursors.Hand;

            return;

        }

        double delta = Seconds(e.X) - originX;

        if (dragScene >= 0) {

            var scene = Script.Scenes[dragScene]; double baseTime = SceneStart(dragScene), limit = baseTime + Duration(scene, VoiceSpeed), start = originStart, end = originEnd;

            if (dragMode == 1) start = Math.Clamp(originStart + delta, baseTime, Math.Max(baseTime, end - .1));

            if (dragMode == 2) end = Math.Clamp(originEnd + delta, Math.Min(start + .1, limit), limit);

            if (dragMode == 3) { start = Math.Clamp(originStart + delta, baseTime, Math.Max(baseTime, limit - (end - originStart))); end = start + originEnd - originStart; }

            scene.TitleStart = Math.Round(start - baseTime, 2); scene.TitleDuration = Math.Round(Math.Max(.1, end - start), 2);

        } else if (Script.MusicTrack != null) {

            var music = Script.MusicTrack;

            if (dragMode == 5) music.End = Math.Round(Math.Clamp(originEnd + delta, Math.Min(music.Start + .1, Total), Total), 2);

            else { music.Start = Math.Round(Math.Clamp(originStart + delta, 0, Math.Max(0, Total - .1)), 2); music.End = Math.Round(Math.Min(Total, music.Start + originEnd - originStart), 2); }

        }

        changed = true; Invalidate();

    }

    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); Capture = false; dragMode = 0; if (changed) Edited?.Invoke(); }

    internal void TestDrag(double from, double to, int y) { using var surface = new Bitmap(Width, Height); using var graphics = Graphics.FromImage(surface); OnPaint(new PaintEventArgs(graphics, ClientRectangle)); OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, (int)X(from), y, 0)); OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, (int)X(to), y, 0)); OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, (int)X(to), y, 0)); }

}

