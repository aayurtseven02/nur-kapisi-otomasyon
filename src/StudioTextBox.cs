using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
namespace AnkaStudio;

// Keep the native edit control: password masking, selection, clipboard and
// accessibility remain Windows behavior. Only the outer edge is softened.
public sealed class StudioTextBox : TextBox
{
    [DllImport("user32.dll")] static extern IntPtr GetWindowDC(IntPtr handle);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr handle,IntPtr dc);
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr handle,int message,IntPtr wParam,IntPtr lParam);
    public StudioTextBox(){AutoSize=false;}
    void RoundEdge(){if(Width<8||Height<8)return;using var path=Shapes.Round(new Rectangle(0,0,Width,Height),Math.Min(8,Height/2));var old=Region;Region=new Region(path);old?.Dispose();}
    protected override void OnSizeChanged(EventArgs e){base.OnSizeChanged(e);RoundEdge();}
    protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);SendMessage(Handle,0x00D3,new IntPtr(3),new IntPtr((6<<16)|6));RoundEdge();}
    protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}
    protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);Invalidate();}
    protected override void WndProc(ref Message message)
    {
        base.WndProc(ref message);bool printing=message.Msg is 0x0317 or 0x0318;
        if(!printing&&message.Msg is not (0x000F or 0x0085)||Width<8||Height<8)return;
        var dc=printing?message.WParam:GetWindowDC(Handle);if(dc==IntPtr.Zero)return;
        try{using var g=Graphics.FromHdc(dc);g.SmoothingMode=SmoothingMode.AntiAlias;using var path=Shapes.Round(new Rectangle(0,0,Width-1,Height-1),Math.Min(8,Height/2));var saved=g.Save();using var outside=new Region(new Rectangle(0,0,Width,Height));outside.Exclude(path);g.SetClip(outside,CombineMode.Replace);using(var back=new SolidBrush(Parent?.BackColor??Theme.Bg))g.FillRectangle(back,0,0,Width,Height);g.Restore(saved);using var pen=new Pen(Focused?Color.FromArgb(146,158,172):Theme.Border,1);g.DrawPath(pen,path);}finally{if(!printing)ReleaseDC(Handle,dc);}
    }
}

