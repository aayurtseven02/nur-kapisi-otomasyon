using System.Runtime.InteropServices;
namespace AnkaStudio;
internal sealed class ArchiveMusicPlayer : FlowLayoutPanel
{
    readonly Label title=new(){Width=640,Height=24,Text="Listeden müzik seç ve Oynat düğmesine bas."};
    readonly Label clock=new(){Width=110,Height=25,Text="00:00 / 00:00"};
    readonly TrackBar seek=new StudioSlider(){Width=240,Height=35,Minimum=0,Maximum=1,TickStyle=TickStyle.None};
    readonly System.Windows.Forms.Timer timer=new(){Interval=300};
    dynamic? player;
    static WeakReference<ArchiveMusicPlayer>? active;
    void StartPlayback(){if(active!=null&&active.TryGetTarget(out var previous)&&previous!=this&&!previous.IsDisposed)previous.Safe(()=>{if(previous.loaded)previous.player!.controls.stop();});active=new(this);player!.controls.play();}
    bool loaded;
    internal bool IsLoaded=>loaded;
    internal void StopPlayback()=>Safe(()=>{if(loaded)player!.controls.stop();});
    internal int PlaybackState{get{try{return player==null?0:(int)player.playState;}catch{return 0;}}}
    public ArchiveMusicPlayer(bool compact=false)
    {
        Dock=compact?DockStyle.None:DockStyle.Bottom;Height=compact?130:94;Padding=new Padding(8);
        if(compact){Width=680;title.Text="Ses örneği burada oynatılacak.";title.AccessibleName="Ses önizleme durumu";seek.Width=160;clock.Font=Theme.Font(9);title.Font=Theme.Font(10);clock.ForeColor=title.ForeColor=Theme.Text;BackColor=Theme.Panel;}
        Controls.Add(title);SetFlowBreak(title,true);
        Controls.Add(Theme.Button("Oynat / Duraklat",()=>Safe(()=>{if(!loaded)return;if((int)player!.playState==3)player.controls.pause();else StartPlayback();}),false,compact?145:180));
        Controls.Add(Theme.Button("Durdur",()=>Safe(()=>{if(!loaded)return;player!.controls.stop();UpdatePosition();}),false,compact?90:110));
        Controls.Add(seek);Controls.Add(clock);
        seek.Scroll+=(_,_)=>Safe(()=>{if(loaded)player!.controls.currentPosition=seek.Value/1000d;});
        timer.Tick+=(_,_)=>Safe(UpdatePosition);
    }
    protected override void OnSizeChanged(EventArgs e){base.OnSizeChanged(e);if(title!=null)title.Width=Math.Max(100,Width-Padding.Horizontal-12);}
    public void Preparing(){Safe(()=>{if(loaded)player!.controls.stop();title.Text="Ses örneği hazırlanıyor…";});}
    public void LoadAndPlay(string file){if(!File.Exists(file))throw new InvalidOperationException("Önce bir ses dosyası seç.");Load(new AudioItem{File=file,Name="Ses önizlemesi • "+Path.GetFileName(file)});Safe(()=>{if(loaded)StartPlayback();});}
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

