using System.Diagnostics;

using System.Drawing.Drawing2D;

using System.Globalization;

using System.Text.Json;



namespace AnkaStudio;



public static class Theme

{

    public static readonly Color Warning = Color.FromArgb(255, 48, 78);

    public static DialogResult Alert(string message, string title = "Anka Studio", MessageBoxButtons buttons = MessageBoxButtons.OK) => Alert(null, message, title, buttons);

    public static DialogResult Alert(IWin32Window? owner, string message, string title = "Anka Studio", MessageBoxButtons buttons = MessageBoxButtons.OK, MessageBoxIcon icon = MessageBoxIcon.Warning)

    {

        using var dialog = new Form { Text = title, Width = 680, Height = Math.Clamp(210 + message.Length / 80 * 22, 310, 580), Padding = new Padding(18), BackColor = Bg, ForeColor = Warning, StartPosition = owner == null ? FormStartPosition.CenterScreen : FormStartPosition.CenterParent, MinimizeBox = false, MaximizeBox = false };

        var text = Label(message, 11, color: Warning, width: 580, height: 100); text.AutoSize = true; text.MaximumSize = new Size(580, 0); text.Location = new Point(18, 62);

        var content = new StudioCard { Dock = DockStyle.Fill, Padding = new Padding(18), AutoScroll = true, BackColor = Panel }; var heading = Label("Bilgilendirme / kontrol", 18, true, Warning, 580, 44); heading.Location = new Point(18, 12); content.Controls.AddRange([heading, text]);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 70, Padding = new Padding(24, 8, 0, 0) };

        foreach (var result in buttons == MessageBoxButtons.YesNo ? new[] { DialogResult.Yes, DialogResult.No } : new[] { DialogResult.OK })

        {

            var button = Button(result == DialogResult.Yes ? "Evet" : result == DialogResult.No ? "Hayır" : "Tamam", () => { dialog.DialogResult = result; dialog.Close(); });

            actions.Controls.Add(button);

            if (result is DialogResult.OK or DialogResult.No) { dialog.AcceptButton = button; dialog.CancelButton = button; }

        }

        dialog.Controls.Add(content); dialog.Controls.Add(actions);

        ModernScrollbars.Apply(dialog);

        return owner == null ? dialog.ShowDialog() : dialog.ShowDialog(owner);

    }

    public static Color Bg = Color.FromArgb(16, 17, 19), Panel = Color.FromArgb(23, 24, 27), Elevated = Color.FromArgb(32, 33, 37), Text = Color.FromArgb(255, 255, 255), Muted = Color.FromArgb(158, 157, 153), Teal = Color.FromArgb(255, 255, 255), Orange = Color.FromArgb(204, 179, 125), Border = Color.FromArgb(49, 49, 53);
    public static readonly Color Success = Color.FromArgb(57, 255, 20), SuccessSurface = Success;

    static readonly HashSet<string> InstalledFonts = FontFamily.Families.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
    static string PickFont(params string[] names) => names.FirstOrDefault(InstalledFonts.Contains) ?? "Segoe UI";
    public static readonly string BodyFont = PickFont("Consolas", "Cascadia Mono", "Courier New");
    public static readonly string HeadingFont = BodyFont;
    public static Font Font(float size = 10, bool bold = false) => new(BodyFont, size, bold ? FontStyle.Bold : FontStyle.Regular);

    public static Label Label(string text, float size = 10, bool bold = false, Color? color = null, int width = 900, int height = 32) => new StudioLabel() { Text = size >= 14 || bold && size >= 12 ? text.ToUpper(CultureInfo.GetCultureInfo("tr-TR")) : text, Font = Font(size, bold), ForeColor = color ?? Text, AutoSize = false, Width = width, Height = height, TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.Transparent, Margin = Padding.Empty };

    public static Button Button(string text, Action click, bool primary = false, int width = 150)

    {

        var b = new StudioButton { Text = text, AutoSize=false, Width = width, Height = 46, FlatStyle = FlatStyle.Flat, Font = Font(10, false), BackColor = primary ? Teal : Elevated, ForeColor = primary ? Bg : Text, Cursor = Cursors.Hand, Margin = new Padding(0, 0, 10, 0) };
        string name = text.ToLower(CultureInfo.GetCultureInfo("tr-TR"));
        if (name == "işlemi iptal et") { b.BackColor = Warning; b.ForeColor = Text; }
        if (name is "stok video arşivi" or "stok videolar") b.LeadingIcon = "film";
        else if (name is "ses ve müzik arşivi" or "ses ve müzik") b.LeadingIcon = "music";
        else if (name == "kapak şablonları") b.LeadingIcon = "image";
        if (b.LeadingIcon != null) b.UppercaseCaption = true;

        b.FlatAppearance.BorderSize = 0; b.Click += (_, _) => click(); return b;

    }

    public static TextBox Input(string value = "", bool multi = false, int width = 450) => new() { Text = value, Width = width, Height = multi ? 90 : 36, Multiline = multi, BackColor = Elevated, ForeColor = Text, BorderStyle = BorderStyle.FixedSingle, Font = Font(11), ScrollBars = multi ? ScrollBars.Vertical : ScrollBars.None };

    public static ComboBox Combo(string[] items, string current, int width = 450)

    {

        var c = new DarkComboBox { Width = width, Height = 36, ItemHeight = 28, DrawMode = DrawMode.OwnerDrawFixed, BackColor = Elevated, ForeColor = Text, FlatStyle = FlatStyle.Flat, DropDownStyle = ComboBoxStyle.DropDownList, Font = Font(11) };

        c.DrawItem += (_, e) => { using var brush = new SolidBrush(Elevated); e.Graphics.FillRectangle(brush, e.Bounds); if (e.Index >= 0) TextRenderer.DrawText(e.Graphics, c.Items[e.Index]?.ToString(), c.Font, new Rectangle(e.Bounds.X + 9, e.Bounds.Y, e.Bounds.Width - 9, e.Bounds.Height), Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis); };

        c.Items.AddRange(items); c.SelectedItem = current; if (c.SelectedIndex < 0 && c.Items.Count > 0) c.SelectedIndex = 0; return c;

    }

    public static NumericUpDown Number(decimal value, decimal min, decimal max, int width = 450, int decimals = 0) => new StudioNumber() { Minimum = min, Maximum = max, Value = Math.Clamp(value, min, max), DecimalPlaces = decimals, Increment = decimals > 0 ? .05m : 1, Width = width, BackColor = Elevated, ForeColor = Text, Font = Font(11) };

    public static CheckBox Check(string text, bool value, int width = 700) => new StudioCheck() { Text = text, Checked = value, ForeColor = Text, Font = Font(), Width = width, Height = 34 };

    public static FlowLayoutPanel Flow(int width = 1000) => new StudioFlowPanel() { Width = width, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, 20) };

    public static Panel Card(int width, int height) => new StudioCard() { Width = width, Height = height, BackColor = Panel, Padding = new Padding(20), Margin = new Padding(0, 0, 16, 16) };

    public static void Open(string path) { if (!string.IsNullOrWhiteSpace(path)) Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }

}



public sealed class Hero : Control
{
    public Hero() { Height = 210; DoubleBuffered = true; SetStyle(ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true); BackColor = Color.Transparent; }
    protected override void OnPaint(PaintEventArgs e)
    {
        if (Width < 10 || Height < 10) return;
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        using var small = Theme.Font(9); using var copy = Theme.Font(11);
        using var title = Theme.Font(Width < 900 ? 25 : 31, true);
        using var accent = new SolidBrush(Theme.Teal); using var text = new SolidBrush(Theme.Text); using var muted = new SolidBrush(Theme.Muted);
        g.DrawString("ANKA STUDIO  /  İÇERİK ÜRETİMİ", small, text, 0, 18);
        g.DrawString("FİKRİNİ BİR VİDEOYA\nDÖNÜŞTÜR.", title, text, new RectangleF(0, 50, Math.Max(320, Width - 310), 105));
        g.DrawString("Konunu ekle. İçeriğini hazırla. İncele ve paylaş.", copy, muted, new RectangleF(0, 165, Width - 10, 36));
        if (Width > 740) {
            int left = Width - 290;
            using var stroke = new Pen(Theme.Teal, 2);
            using var screen = new SolidBrush(Theme.Panel);
            var landscape = new Rectangle(left, 52, 180, 102);
            var portrait = new Rectangle(left + 196, 31, 70, 142);
            g.FillRectangle(screen, landscape); g.DrawRectangle(stroke, landscape);
            g.FillRectangle(screen, portrait); g.DrawRectangle(stroke, portrait);
            TextRenderer.DrawText(g, "16:9", title, landscape, Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            TextRenderer.DrawText(g, "9:16", small, portrait, Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            g.FillEllipse(accent, left + 176, 143, 18, 18);
        }
        using var line = new Pen(Theme.Border); g.DrawLine(line, 0, Height - 1, Width, Height - 1);
    }
}

public sealed class DarkComboBox : ComboBox

{
    protected override void OnDropDown(EventArgs e)
    {
        // The native popup keeps its earlier width after layout/DPI changes.
        // Refresh it when opening instead of keeping the construction-time width.
        DropDownWidth = Math.Max(1, Width);
        base.OnDropDown(e);
    }

    void PaintFace(Graphics g)

    {
        Rectangle visible = RectangleToScreen(ClientRectangle);
        for (Control? ancestor = Parent; ancestor != null; ancestor = ancestor.Parent)
            visible = Rectangle.Intersect(visible, ancestor.RectangleToScreen(ancestor.ClientRectangle));
        if (visible.Width <= 0 || visible.Height <= 0) return;
        g.SetClip(new Rectangle(PointToClient(visible.Location), visible.Size), CombineMode.Intersect);

        using var fill = new SolidBrush(Theme.Elevated); using var pen = new Pen(Theme.Border); g.FillRectangle(fill, ClientRectangle); g.DrawRectangle(pen, 0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));

        var display = SelectedItem?.ToString() ?? "Seç..."; using var text = new SolidBrush(Theme.Text); using var format = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };

        g.DrawString(display, Font, text, new RectangleF(10, 0, Math.Max(1, Width - 40), Height), format); using var arrow = new SolidBrush(Theme.Teal); g.FillPolygon(arrow, [new Point(Width - 23, Height / 2 - 2), new Point(Width - 13, Height / 2 - 2), new Point(Width - 18, Height / 2 + 4)]);

    }

    protected override void WndProc(ref System.Windows.Forms.Message m)

    {

        base.WndProc(ref m);

        if (m.Msg == 0x000F && IsHandleCreated) { using var g = Graphics.FromHwnd(Handle); PaintFace(g); }

        if ((m.Msg == 0x0318 || m.Msg == 0x0317) && m.WParam != IntPtr.Zero) { using var g = Graphics.FromHdc(m.WParam); PaintFace(g); }

    }

}



