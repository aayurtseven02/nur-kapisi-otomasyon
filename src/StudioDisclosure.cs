namespace AnkaStudio;

public sealed class StudioDisclosure : FlowLayoutPanel
{
    readonly Button heading;
    readonly string title;
    public FlowLayoutPanel Content { get; }
    public bool Expanded { get; private set; }
    public StudioDisclosure(string name, int width, bool expanded = false)
    {
        title = name; Width = width; FlowDirection = FlowDirection.TopDown; WrapContents = false;
        AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink; Margin = new Padding(0, 0, 0, 8); BackColor = Color.Transparent;
        heading = Theme.Button(name, () => SetExpanded(!Expanded), false, width); heading.Font=Theme.Font(10,true);heading.TextAlign = ContentAlignment.MiddleLeft; heading.BackColor = Theme.Bg; heading.Height = 42; heading.Margin = Padding.Empty;
        heading.AccessibleName = name + " bölümünü aç veya kapat";
        Content = new FlowLayoutPanel { Width = width, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(8, 0, 0, 0), Margin = Padding.Empty, BackColor = Color.Transparent };
        Controls.Add(heading); Controls.Add(Content); SetExpanded(expanded);
    }
    public void SetExpanded(bool value)
    {
        Expanded = value; Content.Visible = value; heading.Text = title.ToUpper(System.Globalization.CultureInfo.GetCultureInfo("tr-TR"));
        ((StudioButton)heading).LeadingChevron = value ? "⌄" : "›"; heading.Invalidate();
        heading.AccessibleDescription = value ? "Açık bölüm" : "Kapalı bölüm"; PerformLayout();
    }

    public void FitWidth(int width)
    {
        width = Math.Max(100, width);
        MaximumSize = new Size(width, 0); Width = width;
        heading.Width = width;
        Content.MaximumSize = new Size(width, 0); Content.Width = width;
        foreach (Control child in Content.Controls)
            child.Width = Math.Max(40, width - Content.Padding.Horizontal - child.Margin.Horizontal);
    }
}
