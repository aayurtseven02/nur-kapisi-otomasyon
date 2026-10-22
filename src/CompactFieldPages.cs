using System.Runtime.CompilerServices;
namespace AnkaStudio;

// Fixed viewport for settings. Label/input pairs stay together on each page.
internal sealed class CompactFieldPages
{
    static readonly ConditionalWeakTable<FlowLayoutPanel,CompactFieldPages> attached=new();
    readonly FlowLayoutPanel host;
    readonly Control[] fields;
    readonly FlowLayoutPanel navigation=new(){Height=32,WrapContents=false,Margin=Padding.Empty};
    readonly Label page=Theme.Label("",9,width:66,height:30);
    readonly Button previous,next;
    bool fitting;
    int selected;
    CompactFieldPages(FlowLayoutPanel host){
        this.host=host;fields=host.Controls.Cast<Control>().Where(c=>c.Visible).ToArray();
        previous=Theme.Button("‹",()=>{selected=Math.Max(0,selected-1);Render();},false,36);next=Theme.Button("›",()=>{selected++;Render();},false,36);previous.AccessibleName="Önceki ayar sayfası";next.AccessibleName="Sonraki ayar sayfası";previous.Height=next.Height=28;
        navigation.Controls.AddRange([previous,page,next]);host.Controls.Add(navigation);
        host.SizeChanged+=(_,_)=>Render();host.Layout+=(_,_)=>Render();
    }
    public static void Fit(FlowLayoutPanel host,int width,int height){
        host.SuspendLayout();host.AutoSize=false;host.AutoScroll=false;host.MaximumSize=new Size(width,0);host.Size=new Size(width,Math.Max(100,height));var pager=attached.GetValue(host,h=>new(h));host.ResumeLayout(false);pager.Render();
    }
    void Render(){
        if(fitting||host.IsDisposed)return;fitting=true;host.SuspendLayout();
        try{
            int width=Math.Max(100,host.ClientSize.Width-12),budget=Math.Max(52,host.Height-42);
            var units=new List<List<Control>>();
            for(int i=0;i<fields.Length;i++){
                var field=fields[i];if(field.IsDisposed)continue;
                field.Margin=new Padding(0,0,0,4);
                if(field is Button) {field.Width=Math.Min(280,width);field.Height=34;}
                else if(field is Label label){field.Width=width;field.Height=Math.Max(20,TextRenderer.MeasureText(label.Text,label.Font,new Size(Math.Max(80,width-12),int.MaxValue),TextFormatFlags.WordBreak).Height+4);}
                else if(field is StudioDisclosure disclosure){disclosure.FitWidth(width);if(disclosure.Expanded){Fit(disclosure.Content,width,Math.Max(90,budget-48));disclosure.AutoSize=true;}}
                else field.Width=Math.Min(field is NumericUpDown?240:650,width);
                var unit=new List<Control>{field};if(field is Label&&i+1<fields.Length&&fields[i+1] is TextBox or ComboBox or NumericUpDown){var input=fields[++i];input.Width=Math.Min(input is NumericUpDown?240:650,width);input.Margin=new Padding(0,0,0,5);unit.Add(input);}units.Add(unit);
            }
            var pages=new List<List<Control>>{new()};int used=0;
            foreach(var unit in units){int height=unit.Sum(x=>x.Height+x.Margin.Vertical);if(used>0&&used+height>budget){pages.Add(new());used=0;}pages[^1].AddRange(unit);used+=height;}
            selected=Math.Clamp(selected,0,pages.Count-1);foreach(var field in fields)field.Visible=pages[selected].Contains(field);
            navigation.Width=Math.Min(160,width);navigation.Visible=pages.Count>1;page.Text=$"{selected+1} / {pages.Count}";previous.Enabled=selected>0;next.Enabled=selected<pages.Count-1;host.Controls.SetChildIndex(navigation,host.Controls.Count-1);
        }finally{host.ResumeLayout(true);fitting=false;}
    }
}

