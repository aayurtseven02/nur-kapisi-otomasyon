using LibVLCSharp.Shared;
using VlcMedia=LibVLCSharp.Shared.Media;
using LibVLCSharp.WinForms;
namespace AnkaStudio;

public sealed class WorkflowPlayer : UserControl
{
    readonly VideoView view=new(){Dock=DockStyle.Fill,BackColor=Color.Black};
    LibVLC? library;
    MediaPlayer? player;
    VlcMedia? media;
    Form? fullscreen;
    int requestedVolume=100;double? pendingPosition;
    public WorkflowPlayer(){Height=260;BackColor=Color.Black;AccessibleName="TubeFlow VLC video önizlemesi";Controls.Add(view);}
    void Ensure(){if(player!=null)return;Core.Initialize(Path.Combine(AppContext.BaseDirectory,"libvlc","win-x64"));library=new LibVLC("--no-video-title-show","--no-osd","--no-metadata-network-access");player=new MediaPlayer(library);view.MediaPlayer=player;player.Playing+=(_,_)=>{if(IsHandleCreated&&!IsDisposed)BeginInvoke((Action)(()=>{if(player==null)return;player.Volume=requestedVolume;if(pendingPosition is double position){player.Time=(long)(position*1000);pendingPosition=null;}}));};}
    public void Open(string path){if(!File.Exists(path))throw new FileNotFoundException("Önizleme videosu bulunamadı.",path);Ensure();player!.Stop();media?.Dispose();media=new VlcMedia(library!,Path.GetFullPath(path),FromType.FromPath);player.Media=media;}
    public void Play(){Ensure();if(!player!.Play())throw new InvalidOperationException("VLC video oynatmayı başlatamadı.");}
    public void Pause()=>player?.SetPause(true);
    public void Stop()=>player?.Stop();
    public bool IsPlaying=>player?.IsPlaying==true;
    public double Duration=>Math.Max(0,(player?.Length??0)/1000d);
    public double Position=>Math.Max(0,(player?.Time??0)/1000d);
    public void Seek(double seconds){if(!double.IsFinite(seconds)||seconds<0)return;if(player!=null&&player.Length>0)player.Time=(long)(seconds*1000);else pendingPosition=seconds;}
    [System.ComponentModel.DefaultValue(100)]
    public int Volume{get=>requestedVolume;set{requestedVolume=Math.Clamp(value,0,100);if(player!=null)player.Volume=requestedVolume;}}
    public void ToggleFullScreen(){
        if(fullscreen!=null){fullscreen.Close();return;}if(Parent==null)return;
        var prior=Parent;var bounds=Bounds;var dock=Dock;var owner=FindForm();
        var window=new Form{FormBorderStyle=FormBorderStyle.None,WindowState=FormWindowState.Maximized,BackColor=Color.Black,KeyPreview=true,Text="TubeFlow • tam ekran"};fullscreen=window;
        var controls=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=58,BackColor=Theme.Panel};
        controls.Controls.Add(Theme.Button("▶ Oynat / duraklat",()=>{if(IsPlaying)Pause();else Play();},false,220));controls.Controls.Add(Theme.Button("Tam ekrandan çık • Esc",()=>window.Close(),false,250));
        prior.Controls.Remove(this);Dock=DockStyle.Fill;window.Controls.Add(this);window.Controls.Add(controls);controls.BringToFront();
        window.KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Escape)window.Close();else if(e.KeyCode==Keys.Space){if(IsPlaying)Pause();else Play();e.Handled=true;}};
        window.FormClosing+=(_,_)=>{window.Controls.Remove(this);fullscreen=null;if(!prior.IsDisposed){prior.Controls.Add(this);Dock=dock;Bounds=bounds;}else Dispose();};
        window.Show(owner);
    }
    protected override void Dispose(bool disposing){if(disposing){if(fullscreen!=null){fullscreen.Close();fullscreen=null;}view.MediaPlayer=null;player?.Stop();player?.Dispose();media?.Dispose();library?.Dispose();player=null;media=null;library=null;}base.Dispose(disposing);}
}



