namespace AnkaStudio;

internal static class CompactUi
{
    public static StudioDisclosure Group(FlowLayoutPanel parent, string title, Control first, Control last, bool expanded=false)
    {
        var controls=parent.Controls.Cast<Control>().ToArray();
        int start=Array.IndexOf(controls,first), end=Array.IndexOf(controls,last);
        if(start<0 || end<start) throw new InvalidOperationException("Menü bölümü bulunamadı: "+title);
        var section=new StudioDisclosure(title,Math.Max(180,parent.ClientSize.Width-parent.Padding.Horizontal-24),expanded);
        parent.SuspendLayout();
        foreach(var control in controls.Skip(start).Take(end-start+1)) section.Content.Controls.Add(control);
        parent.Controls.Add(section); parent.Controls.SetChildIndex(section,start);
        section.FitWidth(section.Width);
        parent.SizeChanged+=(_,_)=>{ if(!section.IsDisposed) section.FitWidth(Math.Max(180,parent.ClientSize.Width-parent.Padding.Horizontal-24)); };
        parent.ResumeLayout(true); return section;
    }
    public static Control LabelFor(FlowLayoutPanel parent, Control field) => parent.Controls[Math.Max(0,parent.Controls.IndexOf(field)-1)];
}
