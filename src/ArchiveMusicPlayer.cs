using System.Runtime.InteropServices;
namespace AnkaStudio;
internal sealed class ArchiveMusicPlayer : FlowLayoutPanel
{
    readonly Label title=new(){Width=640,Height=24,Text="Listeden müzik seç ve Oynat düğmesine bas."};
    readonly Label clock=new(){Width=110,Height=25,Text="00:00 / 00:00"};
    readonly TrackBar seek=new(){Width=240,Height=35,Minimum=0,Maximum=1,TickStyle=TickStyle.None};
    readonly System.Windows.Forms.Timer timer=new(){Interval=300};
    dynamic? player;
    bool loaded;
    public ArchiveMusicPlayer()
    {
        Dock=DockStyle.Bottom;Height=94;Padding=new Padding(8);
        Controls.Add(title);SetFlowBreak(title,true);
        Controls.Add(Theme.Button("Oynat / Duraklat",()=>Safe(()=>{if(!loaded)return;if((int)player!.playState==3)player.controls.pause();else player.controls.play();}),false,180));
        Controls.Add(Theme.Button("Durdur",()=>Safe(()=>{if(!loaded)return;player!.controls.stop();UpdatePosition();}),false,110));
        Controls.Add(seek);Controls.Add(clock);
        seek.Scroll+=(_,_)=>Safe(()=>{if(loaded)player!.controls.currentPosition=seek.Value/1000d;});
        timer.Tick+=(_,_)=>Safe(UpdatePosition);
    }
    void Safe(Action action){try{action();}catch(Exception ex){timer.Stop();title.Text="Ses oynatılamadı: "+ex.Message;}}
    static string Time(int ms)=>TimeSpan.FromMilliseconds(ms).ToString(@"mm\:ss");
    void UpdatePosition()
    {
        if(!loaded)return;
        double duration=(double)player!.currentMedia.duration;
        seek.Maximum=Math.Max(1,(int)(duration*1000));
        seek.Value=Math.Clamp((int)((double)player.controls.currentPosition*1000),0,seek.Maximum);
        clock.Text=Time(seek.Value)+" / "+Time(seek.Maximum);
        if((int)player.error.errorCount>0){title.Text="Ses oynatılamadı: "+(string)player.error.Item(0).errorDescription;timer.Stop();}
    }
    public void Load(AudioItem item)
    {
        Safe(()=>{
            if(player==null){var type=Type.GetTypeFromProgID("WMPlayer.OCX")??throw new InvalidOperationException("Windows medya bileşeni bulunamadı.");player=Activator.CreateInstance(type)!;player.settings.autoStart=false;}
            player.controls.stop();player.error.clearErrorQueue();loaded=false;
            player.URL=item.File;title.Text=item.Name;loaded=true;seek.Value=0;seek.Maximum=1;clock.Text="00:00 / 00:00";timer.Start();
        });
    }
    protected override void Dispose(bool disposing)
    {
        if(disposing){timer.Stop();timer.Dispose();if(player!=null){try{player.controls.stop();player.close();}finally{Marshal.FinalReleaseComObject((object)player);player=null;}}}
        base.Dispose(disposing);
    }
}
