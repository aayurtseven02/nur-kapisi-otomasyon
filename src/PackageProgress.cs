using System.Drawing.Drawing2D;

namespace AnkaStudio;



public sealed class PackageTimeEstimate

{

    string phase = ""; int baseline, last; double started;

    public TimeSpan? Observe(string currentPhase, int progress, bool running, double seconds)

    {

        progress = Math.Clamp(progress, 0, 100);

        if (!running) { phase = ""; return null; }

        if (phase != currentPhase || progress < last) { phase = currentPhase; baseline = progress; started = seconds; }

        last = progress;

        if (progress >= 100) return TimeSpan.Zero;

        double elapsed = seconds - started; int advanced = progress - baseline;

        if (advanced < 2 || elapsed < 3) return null;

        double estimate = elapsed * (100 - progress) / advanced;

        return double.IsFinite(estimate) && estimate < 86400 ? TimeSpan.FromSeconds(Math.Max(1, estimate)) : null;

    }

    public static string Display(TimeSpan? remaining) => remaining == null ? "Hesaplanıyor…" : remaining.Value.TotalSeconds < 1 ? "Son kontroller…" : remaining.Value.TotalMinutes >= 1 ? $"Yaklaşık {Math.Max(1,(int)(remaining.Value.TotalMinutes*.7))}–{Math.Max(2,(int)Math.Ceiling(remaining.Value.TotalMinutes*1.3))} dk" : $"Yaklaşık {Math.Max(1, remaining.Value.Seconds)} sn";

}



public sealed class PackageStageBar : Control

{

    static readonly string[] Names = ["1. İÇERİK", "2. SAHNELER", "3. SES VE TASARIM", "4. ÜRET VE İNCELE", "5. YAYIN"];

    static bool[] MainStages(bool[] values)=>[values[0],values[1],values[2]&&values[3],values[4]&&values[5],values[6]];

    public bool[] Completed { get; private set; } = new bool[8];

    public int Active { get; private set; } = -1;

    public PackageStageBar() { SetStyle(ControlStyles.SupportsTransparentBackColor, true); DoubleBuffered = true; Height = 34; BackColor = Color.Transparent; AccessibleName = "Paket aşamalarının ilerlemesi"; }

    public void UpdateStages(bool[] complete, int active)

    {

        if (Completed.SequenceEqual(complete) && Active == active) return;

        Completed = complete.ToArray(); Active = active;

        AccessibleDescription = string.Join("; ", Names.Select((name, i) => name + ": " + (MainStages(Completed)[i] ? "tamamlandı" : i == active ? "sürüyor" : "bekliyor"))); Invalidate();

    }

    protected override void OnPaint(PaintEventArgs e)

    {

        base.OnPaint(e);

        var done=MainStages(Completed); int active=Active==0?0:Active==1?1:Active is 2 or 3?2:Active is 4 or 5?3:Active==6?4:-1;

        for (int i = 0; i < Names.Length; i++) {

            int left = i * Width / 5, right = (i + 1) * Width / 5 - 5;

            var area = new Rectangle(left, 0, Math.Max(1, right - left), Height);

            using var background = new SolidBrush(Theme.Elevated); e.Graphics.FillRectangle(background, area);

            using var fill = new SolidBrush(done[i] ? Theme.Success : i == active ? Theme.Teal : Theme.Border);

            e.Graphics.FillRectangle(fill, left, Height - 5, area.Width, 5);

            TextRenderer.DrawText(e.Graphics, Names[i], Font, new Rectangle(left + 2, 0, area.Width - 4, Height - 5), done[i] ? Theme.Success : i == active ? Theme.Text : Theme.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        }

    }

}



public sealed class AnimatedPackageStatus : Label

{

    readonly System.Windows.Forms.Timer animation = new() { Interval = 70 };

    string status = ""; int frame;
    bool contentChecklist, scenarioDone, proofreadingDone, proofreadingActive;

    public bool Animating { get; private set; }

    internal int AnimationFrame => frame;

    public AnimatedPackageStatus() { DoubleBuffered = true; animation.Tick += (_, _) => AdvanceFrame(); VisibleChanged += (_, _) => animation.Enabled = Animating && Visible; }

    public void UpdateStatus(string value, bool failed)

    {

        if (status != value) frame = 0;

        status = value; Animating = !failed && value is "Araştırılıyor" or "Üretiliyor" or "Yükleniyor";

        animation.Enabled = Animating && Visible; ForeColor = Theme.Warning;

        AccessibleName = "Paket işlem durumu"; AccessibleDescription = status; RefreshText();

    }

    public void UpdateActivity(string value, bool busy) {
        contentChecklist = false;
        status = value; Text = value; Animating = busy;
        animation.Enabled = busy && Visible; ForeColor = Theme.Warning; Invalidate();
    }

    public void UpdateContentSteps(Episode episode) {
        contentChecklist = true;
        scenarioDone = episode.Plan != null && (episode.Status == "Senaryo hazır" || episode.Progress >= 18);
        proofreadingDone = scenarioDone && (episode.Status == "Senaryo hazır" || episode.Progress >= 23);
        proofreadingActive = scenarioDone && !proofreadingDone;
        Animating = episode.Status == "Araştırılıyor" && episode.Error == "";
        animation.Enabled = Animating && Visible;
        status = episode.CurrentStage;
        Text = "Senaryo: " + (scenarioDone ? "tamamlandı" : "hazırlanıyor") + " • Otomatik Türkçe yazım denetimi: " + (proofreadingDone ? "tamamlandı" : proofreadingActive ? "devam ediyor" : "bekliyor");
        AccessibleDescription = Text; Invalidate();
    }

    internal void AdvanceFrame() { if (!Animating) return; frame = (frame + 1) % 32; Invalidate(); }

    void RefreshText() { Text = status; Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)

    {
        e.Graphics.SetClip(ClientRectangle);
        if (contentChecklist) {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            void Step(string title, int top, bool done, bool active) {
                float cy = top + 10;
                if (done) {
                    using var tick = new Pen(Theme.Success, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
                    e.Graphics.DrawLines(tick, [new PointF(2, cy), new PointF(6, cy + 4), new PointF(13, cy - 5)]);
                } else if (active) {
                    using var pen = new Pen(Theme.Text, 1.2f);
                    e.Graphics.DrawLine(pen, 2, cy - 7, 12, cy - 7); e.Graphics.DrawLine(pen, 2, cy + 7, 12, cy + 7);
                    e.Graphics.DrawLines(pen, [new PointF(3, cy - 6), new PointF(11, cy + 6), new PointF(3, cy + 6), new PointF(11, cy - 6), new PointF(3, cy - 6)]);
                    using var sand = new SolidBrush(Theme.Text);
                    e.Graphics.FillEllipse(sand, 6, cy - 3 + frame % 7, 2, 2);
                } else { using var pen = new Pen(Theme.Muted); e.Graphics.DrawEllipse(pen, 3, cy - 4, 8, 8); }
                TextRenderer.DrawText(e.Graphics, title, Font, new Rectangle(22, top, Math.Max(1, Width - 22), 21), done ? Theme.Success : active ? Theme.Text : Theme.Muted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
            }
            Step("Senaryo" + (scenarioDone ? " • tamamlandı" : " • hazırlanıyor"), 0, scenarioDone, Animating && !scenarioDone);
            string detail = proofreadingDone ? " • tamamlandı" : proofreadingActive ? " • devam ediyor" : " • bekliyor";
            int group = status.IndexOf("grup ", StringComparison.Ordinal);
            if (proofreadingActive && group >= 0) detail += " • " + status[group..];
            Step("Otomatik Türkçe yazım denetimi" + detail, 23, proofreadingDone, Animating && proofreadingActive);
            return;
        }

        if (Width < 1 || Height < 1 || status.Length == 0) return;

        e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

        using var format = new StringFormat(StringFormat.GenericTypographic) { Alignment = TextAlign is ContentAlignment.TopRight or ContentAlignment.MiddleRight or ContentAlignment.BottomRight ? StringAlignment.Far : TextAlign is ContentAlignment.TopCenter or ContentAlignment.MiddleCenter or ContentAlignment.BottomCenter ? StringAlignment.Center : StringAlignment.Near, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };

        var area = new RectangleF(Padding.Left, Padding.Top, Math.Max(1, Width - Padding.Horizontal), Math.Max(1, Height - Padding.Vertical));

        if (status.Contains("yayında", StringComparison.OrdinalIgnoreCase)) {
            area.X += 20; area.Width = Math.Max(1, area.Width - 20);
            float measured = Math.Min(area.Width, e.Graphics.MeasureString(status, Font, PointF.Empty, StringFormat.GenericTypographic).Width);
            float textLeft = format.Alignment == StringAlignment.Far ? area.Right - measured : format.Alignment == StringAlignment.Center ? area.Left + (area.Width - measured) / 2 : area.Left;
            float arrowX = Math.Max(2, textLeft - 18), arrowY = area.Top + area.Height / 2;
            using var arrowPen = new Pen(Theme.Success, 2.5f) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.DrawLines(arrowPen, [new PointF(arrowX, arrowY), new PointF(arrowX + 4, arrowY + 4), new PointF(arrowX + 11, arrowY - 5)]);
        }

        if (!Animating) { using var solid = new SolidBrush(ForeColor); e.Graphics.DrawString(status, Font, solid, area, format); return; }

        float textWidth = Math.Min(area.Width, Math.Max(1, e.Graphics.MeasureString(status, Font, PointF.Empty, StringFormat.GenericTypographic).Width));

        float left = format.Alignment == StringAlignment.Far ? area.Right - textWidth : format.Alignment == StringAlignment.Center ? area.Left + (area.Width - textWidth) / 2 : area.Left;

        using var glow = new LinearGradientBrush(new RectangleF(left, area.Top, textWidth, area.Height), ForeColor, ForeColor, 0f);

        var positions = Enumerable.Range(0, 17).Select(i => i / 16f).ToArray(); double focus = frame / 31.0 * 1.4 - .2;

        var colors = positions.Select(p => { double amount = Math.Exp(-Math.Pow((p - focus) / .12, 2)); return Color.FromArgb((int)(ForeColor.R + (255 - ForeColor.R) * amount), (int)(ForeColor.G + (255 - ForeColor.G) * amount), (int)(ForeColor.B + (255 - ForeColor.B) * amount)); }).ToArray();

        glow.InterpolationColors = new ColorBlend { Positions = positions, Colors = colors };

        e.Graphics.DrawString(status, Font, glow, area, format);

    }

    protected override void Dispose(bool disposing) { if (disposing) animation.Dispose(); base.Dispose(disposing); }

}

public sealed class AnimatedTimeLabel : Label
{
    readonly System.Windows.Forms.Timer timer = new() { Interval = 120 };
    int frame;
    bool running;
    public AnimatedTimeLabel() {
        DoubleBuffered = true; Padding = new Padding(24, 0, 0, 0);
        timer.Tick += (_, _) => { frame = (frame + 1) % 16; Invalidate(); };
        VisibleChanged += (_, _) => timer.Enabled = running && Visible;
    }
    public void SetRunning(bool value) { running = value; timer.Enabled = value && Visible; Invalidate(); }
    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e);
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        float x = 7, y = (Height - 16) / 2f;
        using var pen = new Pen(ForeColor, 1.3f);
        g.DrawLine(pen, x, y, x + 10, y); g.DrawLine(pen, x, y + 16, x + 10, y + 16);
        g.DrawLines(pen, [new PointF(x + 1, y + 1), new PointF(x + 1, y + 4), new PointF(x + 9, y + 12), new PointF(x + 9, y + 15)]);
        g.DrawLines(pen, [new PointF(x + 9, y + 1), new PointF(x + 9, y + 4), new PointF(x + 1, y + 12), new PointF(x + 1, y + 15)]);
        using var sand = new SolidBrush(ForeColor);
        float amount = running ? frame / 15f : .5f;
        g.FillRectangle(sand, x + 2, y + 2, 6, 3 * (1 - amount));
        g.FillRectangle(sand, x + 2, y + 14 - 3 * amount, 6, 3 * amount);
        if (running) g.FillEllipse(sand, x + 4, y + 6 + frame % 4, 2, 2);
    }
    protected override void Dispose(bool disposing) { if (disposing) timer.Dispose(); base.Dispose(disposing); }
}



public sealed class TurquoisePackageBar : Control

{

    int value, tick; bool busy;

    readonly System.Windows.Forms.Timer animation = new() { Interval = 80 };

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]

    public int Value { get => value; set { this.value = Math.Clamp(value, 0, 100); Invalidate(); } }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]

    public bool Busy { get => busy; set { busy = value; animation.Enabled = value && Visible; Invalidate(); } }

    public TurquoisePackageBar()

    {

        SetStyle(ControlStyles.SupportsTransparentBackColor, true); DoubleBuffered = true; Height = 32; BackColor = Color.Transparent; AccessibleName = "Canlı işlem ilerlemesi";

        animation.Tick += (_, _) => { tick = (tick + 2) % 44; Invalidate(); };

        VisibleChanged += (_, _) => animation.Enabled = busy && Visible;

    }

    protected override void Dispose(bool disposing) { if (disposing) animation.Dispose(); base.Dispose(disposing); }

    protected override void OnPaint(PaintEventArgs e)

    {

        base.OnPaint(e); if (Width < 10 || Height < 10) return;

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        using var shape = new GraphicsPath(); int radius = Height - 2;

        shape.AddArc(1, 1, radius, radius, 90, 180); shape.AddArc(Width - radius - 1, 1, radius, radius, 270, 180); shape.CloseFigure();

        using var bg = new SolidBrush(Theme.Panel); e.Graphics.FillPath(bg, shape);

        var state = e.Graphics.Save(); e.Graphics.SetClip(shape);

        int fillWidth = busy && value == 0 ? 0 : Width * value / 100;

        if (fillWidth > 0) {

            using var fill = new SolidBrush(Color.FromArgb(0, 255, 220)); e.Graphics.FillRectangle(fill, 0, 0, fillWidth, Height);

        }

        if (busy) {

            using var glow = new Pen(Color.FromArgb(75, 255, 245, 0), 12) { LineJoin = LineJoin.Round };
            using var arrow = new Pen(Color.FromArgb(255, 245, 0), 5) { LineJoin = LineJoin.Miter };

            int end = value == 0 ? Width : fillWidth;

            e.Graphics.IntersectClip(new Rectangle(0, 0, end, Height));
            for (int x = -44 + tick; x < end; x += 44) {
                PointF[] chevron = [new(x + 4, 5), new(x + 18, Height / 2f), new(x + 4, Height - 5)];
                e.Graphics.DrawLines(glow, chevron);
                e.Graphics.DrawLines(arrow, chevron);
            }

        }

        e.Graphics.Restore(state);

        using var border = new Pen(Theme.Border); e.Graphics.DrawPath(border, shape);

        var labelRect = new Rectangle(12, 1, Width - 24, Height - 2);
        var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
        TextRenderer.DrawText(e.Graphics, Text, Font, labelRect, Theme.Text, flags);
        if (fillWidth > 0) { var saved = e.Graphics.Save(); e.Graphics.SetClip(shape); e.Graphics.IntersectClip(new Rectangle(0, 0, fillWidth, Height)); TextRenderer.DrawText(e.Graphics, Text, Font, labelRect, Theme.Bg, flags | TextFormatFlags.PreserveGraphicsClipping); e.Graphics.Restore(saved); }

    }

}

