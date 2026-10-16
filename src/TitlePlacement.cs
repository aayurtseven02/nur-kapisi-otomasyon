using System.Drawing;

using System.Text;

namespace AnkaStudio;



public static class TitlePlacement

{

    public static readonly string[] Choices = ["Sol", "Orta", "Sağ"];

    public sealed record Fitted(string Text, double X, double Y, int Alignment, double MinX, double MaxX, double MinY, double MaxY);

    public static Fitted Fit(string text, TextLayout layout, int width, int height, string placement = "")

    {

        int align = placement == "Sol" ? 7 : placement == "Sağ" ? 9 : placement == "Orta" ? 8 : 6 + layout.TitleAlignment;

        double x = placement == "Sol" ? width * .08 : placement == "Sağ" ? width * .92 : placement == "Orta" ? width * .5 : width * layout.TitleX / 100;

        double y = height * layout.TitleY / 100, margin = Math.Max(24, width * .055);

        using var image = new Bitmap(1, 1); using var g = Graphics.FromImage(image);

        using var font = new Font(layout.TitleFont == "" ? layout.Font : layout.TitleFont, layout.TitleSize, (layout.Bold ? FontStyle.Bold : FontStyle.Regular) | (layout.Italic ? FontStyle.Italic : FontStyle.Regular), GraphicsUnit.Pixel);

        using var format = (StringFormat)StringFormat.GenericTypographic.Clone(); format.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;

        double Measure(string line) => (g.MeasureString(line, font, int.MaxValue, format).Width + Math.Max(0, line.Length - 1) * layout.Spacing) * layout.ScaleX / 100d * 1.14;

        double pad = 14 + Math.Max(layout.Outline, layout.Effect == "Neon parıltı" ? 14 : 0) + layout.Shadow;

        double pop = layout.TitleAnimation == "Vurgu / pop" ? 1.25 : 1;

        string Wrap(double available) {

            var rows = new List<string>();

            foreach (var paragraph in text.Replace("\r", "").Split('\n')) {

                var line = new StringBuilder();

                foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries)) {

                    if (line.Length > 0 && Measure(line + " " + word) > available) { rows.Add(line.ToString()); line.Clear(); }

                    foreach (var ch in word) {

                        if (line.Length > 0 && Measure(line.ToString() + ch) > available) { rows.Add(line.ToString()); line.Clear(); }

                        line.Append(ch);

                    }

                    line.Append(' ');

                }

                if (line.Length > 0) rows.Add(line.ToString().TrimEnd());

            }

            return string.Join("\n", rows);

        }

        for (double available = (width - margin * 2 - pad * 2) / pop; available >= layout.TitleSize * 1.2; available *= .88) {

            string wrapped = Wrap(available); var lines = wrapped.Split('\n');

            double w = lines.Max(Measure) + pad * 2, h = font.GetHeight(g) * lines.Length * 1.18 + pad * 2;

            double anchor = align == 7 ? 0 : align == 9 ? w : w / 2;

            double radians = -layout.Rotation * Math.PI / 180;

            var corners = new[] { (X: -anchor, Y: 0d), (X: w - anchor, Y: 0d), (X: -anchor, Y: h), (X: w - anchor, Y: h) }.Select(p => (X: (p.X * Math.Cos(radians) - p.Y * Math.Sin(radians)) * pop, Y: (p.X * Math.Sin(radians) + p.Y * Math.Cos(radians)) * pop)).ToArray();

            double minX = corners.Min(p => p.X), maxX = corners.Max(p => p.X), minY = corners.Min(p => p.Y), maxY = corners.Max(p => p.Y);

            if (maxX - minX > width - margin * 2 || maxY - minY > height * .72) continue;

            x = Math.Clamp(x, margin - minX, width - margin - maxX); y = Math.Clamp(y, margin - minY, height - margin - maxY);

            return new(wrapped, x, y, align, minX, maxX, minY, maxY);

        }

        throw new InvalidOperationException("Üst başlık seçili yazı boyutuyla ekrana sığmıyor. Başlığı kısalt veya boyutunu düzenle; seçtiğin yazı tipi ve stil otomatik değiştirilmedi.");

    }

}

