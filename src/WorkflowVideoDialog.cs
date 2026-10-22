namespace AnkaStudio;

internal static class WorkflowVideoDialog
{
    public static void Show(Form owner,string path)
    {
        using var dialog=StudioForm.Dialog("TubeFlow • video önizleme",1000,700);dialog.Padding=Padding.Empty;dialog.KeyPreview=true;
        using var player=new WorkflowPlayer{Dock=DockStyle.Fill};var transport=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=88,WrapContents=true,BackColor=Theme.Panel,Padding=new Padding(8)};
        var play=Theme.Button("▶ Oynat",()=>{if(player.IsPlaying)player.Pause();else player.Play();},true,110);transport.Controls.Add(play);
        transport.Controls.Add(Theme.Button("Başa dön",()=>{player.Pause();player.Seek(0);},false,100));transport.Controls.Add(Theme.Button("Tam ekran",player.ToggleFullScreen,false,110));
        var clock=Theme.Label("00:00 / 00:00",9,width:125,height:34);transport.Controls.Add(clock);
        var volume=new StudioSlider{Minimum=0,Maximum=100,Value=100,Width=100,Height=32,TickStyle=TickStyle.None,AccessibleName="Önizleme ses seviyesi",BackColor=Theme.Panel};volume.Scroll+=(_,_)=>player.Volume=volume.Value;transport.Controls.Add(volume);
        var seek=new StudioSlider{Minimum=0,Maximum=1000,Width=850,Height=30,TickStyle=TickStyle.None,AccessibleName="Önizleme konumu",BackColor=Theme.Panel};seek.Scroll+=(_,_)=>player.Seek(seek.Value/1000d*player.Duration);transport.Controls.Add(seek);
        dialog.Controls.Add(player);dialog.Controls.Add(transport);using var timer=new System.Windows.Forms.Timer{Interval=200};timer.Tick+=(_,_)=>{play.Text=player.IsPlaying?"Ⅱ Duraklat":"▶ Oynat";clock.Text=TimeSpan.FromSeconds(player.Position).ToString(@"mm\:ss")+" / "+TimeSpan.FromSeconds(player.Duration).ToString(@"mm\:ss");seek.Value=Math.Clamp((int)(player.Position/Math.Max(1,player.Duration)*1000),0,1000);};
        dialog.SizeChanged+=(_,_)=>seek.Width=Math.Max(160,dialog.ClientSize.Width-30);dialog.Shown+=(_,_)=>{try{player.Open(path);timer.Start();}catch(Exception ex){clock.Text="Video açılamadı";play.Enabled=false;Theme.Alert(dialog,ex.Message);}};
        dialog.KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Space){play.PerformClick();e.Handled=true;}else if(e.KeyCode==Keys.Escape)dialog.Close();};dialog.ShowDialog(owner);timer.Stop();player.Stop();
    }
}
