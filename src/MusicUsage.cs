namespace AnkaStudio;
public sealed class MusicUsage : FlowLayoutPanel
{
    public const string Notice = "Müzik kullanım haklarını kontrol et. İzinsiz kullanım YouTube üzerinde telif talebine veya videonun engellenmesine yol açabilir. Kullanım sorumluluğu kullanıcıya aittir.";
    public readonly TextBox Source = Theme.Input(), License = Theme.Input(), Attribution = Theme.Input();
    public readonly CheckBox Rights;
    public MusicUsage(string source,string license,string attribution,bool rights,int width)
    {
        Width=width;AutoSize=true;FlowDirection=FlowDirection.TopDown;WrapContents=false;BackColor=Color.Transparent;Margin=Padding.Empty;
        Source.Text=source;License.Text=license;Attribution.Text=attribution;Rights=Theme.Check("",rights,width);
        var note=Theme.Label(Notice,10,color:Theme.Warning,width:width,height:120);note.TextAlign=ContentAlignment.TopLeft;note.Margin=new Padding(0,8,0,12);Controls.Add(note);
        note.SizeChanged+=(_,_)=>{int height=TextRenderer.MeasureText(note.Text,note.Font,new Size(Math.Max(40,note.Width-8),int.MaxValue),TextFormatFlags.WordBreak).Height+12;if(note.Height!=height)note.Height=height;};
    }
    public void RefreshFromSaved(){}
    public void Commit(){}
    public void ApplySaved(string source,string license,string attribution,bool rights){Source.Text=source;License.Text=license;Attribution.Text=attribution;Rights.Checked=rights;}
    protected override void Dispose(bool disposing){if(disposing){Source.Dispose();License.Dispose();Attribution.Dispose();Rights.Dispose();}base.Dispose(disposing);}
}
