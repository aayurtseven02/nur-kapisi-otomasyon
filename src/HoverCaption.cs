namespace AnkaStudio;

// Only an overflowing caption animates. Icons and the button itself stay fixed.
internal sealed class HoverCaption
{
    readonly Control owner;
    readonly System.Windows.Forms.Timer timer = new() { Interval = 25 };
    bool hovered;
    int overflow;
    long started;
    string key = "";
    internal int Offset { get; private set; }

    public HoverCaption(Control owner)
    {
        this.owner = owner;
        owner.MouseEnter += (_, _) => { hovered = true; Reset(); owner.Invalidate(); };
        owner.MouseLeave += (_, _) => { hovered = false; Reset(); owner.Invalidate(); };
        owner.EnabledChanged += (_, _) => { Reset(); owner.Invalidate(); };
        owner.Disposed += (_, _) => timer.Dispose();
        timer.Tick += (_, _) => {
            if (!hovered || !owner.Enabled || overflow <= 0) { Reset(); return; }
            long elapsed = Environment.TickCount64 - started;
            double travel = overflow / 36d * 1000;
            double cycle = 650 + travel + 1200;
            double phase = elapsed % cycle;
            Offset = phase < 650 ? 0 : Math.Min(overflow, (int)((phase - 650) * .036));
            owner.Invalidate();
        };
    }

    void Reset() { timer.Stop(); Offset = 0; started = Environment.TickCount64; }

    public bool Draw(Graphics graphics, string text, Font font, Rectangle bounds, Color color, bool wrap)
    {
        const TextFormatFlags measure = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;
        int textWidth = TextRenderer.MeasureText(text, font, Size.Empty, measure | TextFormatFlags.SingleLine).Width;
        bool clipped = wrap
            ? TextRenderer.MeasureText(text, font, new Size(bounds.Width, int.MaxValue), measure | TextFormatFlags.WordBreak).Height > bounds.Height
            : textWidth > bounds.Width;
        string nextKey = text + "|" + font.Name + "|" + font.Size + "|" + font.Style + "|" + bounds.Size;
        if (nextKey != key) { key = nextKey; Reset(); }
        overflow = clipped ? Math.Max(0, textWidth - bounds.Width + 4) : 0;
        if (!hovered || !owner.Enabled || overflow == 0) { Reset(); return false; }
        if (!timer.Enabled) { started = Environment.TickCount64; timer.Start(); }
        var state = graphics.Save();
        try {
            graphics.SetClip(bounds, System.Drawing.Drawing2D.CombineMode.Intersect);
            TextRenderer.DrawText(graphics, text, font,
                new Rectangle(bounds.X - Offset, bounds.Y, textWidth + 8, bounds.Height), color,
                measure | TextFormatFlags.SingleLine | TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.PreserveGraphicsClipping);
        } finally { graphics.Restore(state); }
        return true;
    }
}
