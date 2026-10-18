namespace AnkaStudio;

// Each message retains a native selectable text control, wrapped by a rounded
// surface. Resizing reflows existing messages without changing saved history.
internal sealed class ChatTranscript : Panel
{
    readonly List<(StudioCard Card, RichTextBox Text, bool User)> messages = [];
    (StudioCard Card, RichTextBox Text)? partial;
    bool arranging;

    public ChatTranscript()
    {
        DoubleBuffered=true; AutoScroll=true; BackColor=Theme.Bg; Padding=new Padding(0,12,8,12);
        SizeChanged+=(_,_)=>Arrange();
    }

    public void Append(string who,string text)
    {
        bool user=who=="Sen";
        var surface=user?Theme.AccentSurface:Theme.Bg;
        var card=new StudioCard { BackColor=surface, Borderless=true, Padding=new Padding(16), AccessibleName=who+" mesajı" };
        var content=new RichTextBox { Text=text, ReadOnly=true, BorderStyle=BorderStyle.None,
            BackColor=surface, ForeColor=who=="Uyarı"?Theme.Orange:Theme.Text,
            Font=Theme.Font(11), DetectUrls=false, ScrollBars=RichTextBoxScrollBars.None, TabStop=true };
        card.Controls.Add(content); Controls.Add(card); messages.Add((card,content,user));
        Arrange(); ScrollControlIntoView(card);
    }

    public void Partial(string text)
    {
        if(partial==null) { Append("TubeFlow",text); var last=messages[^1];partial=(last.Card,last.Text); }
        else partial.Value.Text.Text=text;
        Arrange(); ScrollControlIntoView(partial.Value.Card);
    }

    public void EndPartial()
    {
        if(partial is not {} pending)return;
        messages.RemoveAll(x=>x.Card==pending.Card);pending.Card.Dispose();partial=null;Arrange();
    }

    public void Clear()
    {
        partial=null; foreach(var item in messages)item.Card.Dispose(); messages.Clear();AutoScrollMinSize=Size.Empty;
    }

    void Arrange()
    {
        if(arranging||ClientSize.Width<40)return;arranging=true;
        try {
            int width=Math.Max(100,ClientSize.Width-SystemInformation.VerticalScrollBarWidth-12), y=12;
            foreach(var item in messages) {
                int w=item.User?Math.Max(100,(int)(width*.88)):width;
                var size=TextRenderer.MeasureText(item.Text.Text,item.Text.Font,new Size(w-32,int.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.TextBoxControl);
                int h=Math.Max(28,size.Height+10);
                item.Card.Bounds=new Rectangle((item.User?width-w:0)+AutoScrollPosition.X,y+AutoScrollPosition.Y,w,h+32);
                item.Text.Bounds=new Rectangle(16,16,w-32,h);y+=h+48;
            }
            AutoScrollMinSize=new Size(0,y);
        } finally {arranging=false;}
    }
}
