namespace AnkaStudio;

// Keep native tab navigation, but paint the entire chrome in the studio theme.
public sealed class StudioTabs : TabControl
{
    public StudioTabs()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        SizeMode=TabSizeMode.Fixed; Multiline=true; ItemSize=new Size(270,48); Font=Theme.Font(10,true);
        BackColor=Theme.Bg; ForeColor=Theme.Text; Padding=new Point(12,8);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Theme.Bg);
        for(int i=0;i<TabCount;i++) {
            var rect=GetTabRect(i); rect.Inflate(-3,-3);
            using var fill=new SolidBrush(i==SelectedIndex?Theme.Teal:Theme.Elevated);
            e.Graphics.FillRectangle(fill,rect);
            TextRenderer.DrawText(e.Graphics,TabPages[i].Text,Font,rect,i==SelectedIndex?Theme.Bg:Theme.Text,
                TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.WordBreak);
        }
    }
    protected override void OnSelectedIndexChanged(EventArgs e) {base.OnSelectedIndexChanged(e);Invalidate();}
    protected override void OnLayout(LayoutEventArgs e)
    {
        var size=new Size(Math.Clamp((ClientSize.Width-16)/Math.Max(1,TabCount),130,270),48);
        if(ItemSize!=size)ItemSize=size;
        base.OnLayout(e);
    }
}
