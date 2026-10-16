namespace AnkaStudio;

public sealed partial class StudioForm
{
    void PageSections(params (string Name, Control Content)[] sections)
    {
        var holder=new Panel {Width=page.Width,Height=400,Tag="wide"};
        var menu=new TableLayoutPanel {Dock=DockStyle.Left,Width=190,ColumnCount=1,RowCount=sections.Length+1,Padding=new Padding(0,0,10,0)};
        menu.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        for(int i=0;i<sections.Length;i++)menu.RowStyles.Add(new RowStyle(SizeType.Absolute,54));
        menu.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        var content=new Panel {Dock=DockStyle.Fill,BackColor=Theme.Panel,Padding=new Padding(12)};
        holder.Controls.Add(content);holder.Controls.Add(menu);
        var tabs=new List<(Button Button,Control Section)>();
        Control? activeSection=null;bool sizing=false;
        void FitActive() {
            if(sizing || holder.IsDisposed || activeSection==null)return;
            sizing=true;
            try {
                FitSection(activeSection,Math.Max(180,holder.Width-menu.Width-content.Padding.Horizontal-20));
                activeSection.PerformLayout();
                holder.Height=Math.Max(sections.Length*54,activeSection.Height+content.Padding.Vertical+24);
                page.PerformLayout();
                page.Height=page.Controls.Cast<Control>().Sum(c=>c.Height+c.Margin.Vertical)+24;
            } finally {sizing=false;}
        }
        foreach(var section in sections) {
            section.Content.Location=new Point(12,12);section.Content.Visible=tabs.Count==0;
            content.Controls.Add(section.Content);
            var button=Theme.Button(section.Name,()=>{
                activeSection=section.Content;
                foreach(var tab in tabs){bool active=tab.Section==activeSection;tab.Section.Visible=active;tab.Button.BackColor=active?Theme.Teal:Theme.Elevated;tab.Button.ForeColor=active?Theme.Bg:Theme.Text;}
                activeSection.BringToFront();FitActive();
            },tabs.Count==0);
            button.Dock=DockStyle.Fill;button.Margin=new Padding(0,0,10,8);
            ((StudioButton)button).WrapCaption = true;
            ((StudioButton)button).UppercaseCaption = true;
            menu.Controls.Add(button,0,tabs.Count);tabs.Add((button,section.Content));
            if(activeSection==null)activeSection=section.Content;
            section.Content.SizeChanged+=(_,_)=>{if(section.Content==activeSection)FitActive();};
        }
        page.Controls.Add(holder);holder.SizeChanged+=(_,_)=>FitActive();FitActive();
    }
}
