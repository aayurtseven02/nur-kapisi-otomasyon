namespace AnkaStudio;

internal static class CompactActions
{
    public const int Width = 180, Height = 34, Gap = 8;
    public static void Apply(Button button)
    {
        button.Tag = "compact-action";
        button.MaximumSize = new Size(Width, Height);
        button.Size = new Size(Width, Height);
        button.Margin = new Padding(0, 0, Gap, Gap);
        if (button is StudioButton studio) studio.WrapCaption = false;
    }
    public static FlowLayoutPanel Row(int width)
    {
        return new FlowLayoutPanel {
            Width = width, MaximumSize = new Size(width, 0), AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight, BackColor = Color.Transparent,
            Margin = Padding.Empty, Padding = Padding.Empty, Tag = "compact-action-row"
        };
    }
    public static void Group(FlowLayoutPanel parent)
    {
        FlowLayoutPanel? row = null;
        foreach (var child in parent.Controls.Cast<Control>().ToArray()) {
            if (child is Button button) {
                if (row == null) {
                    int index = parent.Controls.GetChildIndex(child);
                    row = Row(parent.ClientSize.Width - parent.Padding.Horizontal - 12);
                    parent.Controls.Add(row); parent.Controls.SetChildIndex(row, index);
                }
                parent.Controls.Remove(child); Apply(button); row.Controls.Add(button);
            } else row = null;
        }
    }
}
