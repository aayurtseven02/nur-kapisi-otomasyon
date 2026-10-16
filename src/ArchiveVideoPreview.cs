using System.Runtime.InteropServices;
using System.Text;
namespace AnkaStudio;
internal sealed class ArchiveVideoPreview : Panel
{
    [DllImport("winmm.dll", CharSet=CharSet.Unicode)] static extern int mciSendString(string command,StringBuilder? output,int size,IntPtr callback);
    readonly string alias="ankav"+Guid.NewGuid().ToString("N");
    bool opened;
    string status="Önizleme için soldan bir görüntü seç.";
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);if(!opened)TextRenderer.DrawText(e.Graphics,status,Font,ClientRectangle,Color.White,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.WordBreak);}
    public ArchiveVideoPreview() { BackColor=Color.Black; Resize+=(_,_)=>Fit(); }
    void Fit() { if(opened) mciSendString($"put {alias} destination at 0 0 {ClientSize.Width} {ClientSize.Height}",null,0,IntPtr.Zero); }
    public void Play(string file)
    {
        Stop();
        if(mciSendString($"open \"{file}\" type mpegvideo alias {alias} parent {Handle.ToInt64()} style 1073741824",null,0,IntPtr.Zero)!=0) { status="Bu videonun biçimi yerleşik oynatıcıda açılamadı."; Invalidate(); return; }
        opened=true; Fit();
        mciSendString($"setaudio {alias} off",null,0,IntPtr.Zero); mciSendString($"play {alias} repeat",null,0,IntPtr.Zero);
    }
    void Stop(){if(opened)mciSendString("close "+alias,null,0,IntPtr.Zero);opened=false;}
    protected override void Dispose(bool disposing){if(disposing)Stop();base.Dispose(disposing);}
}
