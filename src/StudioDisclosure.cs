namespace AnkaStudio;

public sealed class StudioDisclosure : FlowLayoutPanel
{
    readonly Button heading;
    readonly string title;
    int headingLimit=int.MaxValue;
    int compactWidth, availableWidth;
    public void CompactHeading(int width){compactWidth=width;headingLimit=width;heading.MaximumSize=new Size(width,CompactActions.Height);heading.Height=CompactActions.Height;heading.Margin=Padding.Empty;((StudioButton)heading).WrapCaption=false;Margin=new Padding(0,0,CompactActions.Gap,CompactActions.Gap);FitWidth(availableWidth>0?availableWidth:Width);}
    public void LimitHeadingWidth(int width){headingLimit=Math.Max(100,width);heading.Width=Math.Min(Width,headingLimit);((StudioButton)heading).WrapCaption=true;heading.Height=48;}
    public FlowLayoutPanel Content { get; }
    public bool Expanded { get; private set; }
    public StudioDisclosure(string name, int width, bool expanded = false)
    {
        title = name; Width = width; FlowDirection = FlowDirection.TopDown; WrapContents = false;
        MaximumSize=new Size(width,0); AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink; Margin = new Padding(0, 0, 0, 8); BackColor = Color.Transparent;
        heading = Theme.Button(name, () => SetExpanded(!Expanded), false, width); heading.Font=Theme.Font(10,true);heading.TextAlign = ContentAlignment.MiddleLeft; heading.BackColor = Theme.Bg; heading.Height = 42; heading.Margin = Padding.Empty;
        heading.AccessibleName = name + " bölümünü aç veya kapat";
        Content = new FlowLayoutPanel { Width = width, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = Padding.Empty, Margin = Padding.Empty, MaximumSize=new Size(width,0), BackColor = Color.Transparent };
        Controls.Add(heading); Controls.Add(Content); SetExpanded(expanded);
    }
    public void SetExpanded(bool value)
    {
        Expanded = value; Content.Visible = value; heading.Text = title;
        ((StudioButton)heading).LeadingChevron = value ? "⌄" : "›"; heading.Invalidate();
        heading.AccessibleDescription = value ? "Açık bölüm" : "Kapalı bölüm"; PerformLayout();
        if(compactWidth>0)FitWidth(availableWidth);
    }

    public void FitWidth(int width)
    {
        width = Math.Max(100, width);
        availableWidth=width;
        MaximumSize = new Size(width, 0); Width = compactWidth>0&&!Expanded?Math.Min(compactWidth,width):width;
        heading.Width = Math.Min(width,headingLimit);
        Content.MaximumSize = new Size(width, 0); Content.Width = width;
        foreach (Control child in Content.Controls)
            child.Width = Math.Max(40, Math.Min(child is Button?400:child is TextBox or ComboBox?650:width, width - Content.Padding.Horizontal - child.Margin.Horizontal));
    }
}
