using System.Drawing.Drawing2D;
namespace AnkaStudio;

public sealed class StudioNumber : NumericUpDown
{
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd,int msg,IntPtr wparam,IntPtr lparam);
    public StudioNumber() { BorderStyle=BorderStyle.None; }
    protected override void OnSizeChanged(EventArgs e){base.OnSizeChanged(e);if(Width<8||Height<8)return;using var path=Shapes.Round(new Rectangle(0,0,Width,Height),Math.Min(8,Height/2));var old=Region;Region=new Region(path);old?.Dispose();}
    void InsetChildren(){
        foreach(Control child in Controls){
            bool arrows=child.GetType().Name.Contains("UpDownButtons");
            var old=child.Region;
            child.Region=new Region(new Rectangle(arrows?0:1,2,Math.Max(1,child.Width-(arrows?3:1)),Math.Max(1,child.Height-4)));old?.Dispose();
        }
    }
    protected override void OnLayout(LayoutEventArgs e){base.OnLayout(e);InsetChildren();}
    protected override void OnPaint(PaintEventArgs e){
        base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        using var shape=Shapes.Round(new Rectangle(0,0,Width-1,Height-1),Math.Min(8,Height/2));
        using var pen=new Pen(Color.FromArgb(100,108,118),1);e.Graphics.DrawPath(pen,shape);
    }
    protected override void OnHandleCreated(EventArgs e){
        base.OnHandleCreated(e); foreach(var edit in Controls.OfType<TextBox>())SendMessage(edit.Handle,0x00D3,new IntPtr(3),new IntPtr((3<<16)|6));
        var arrows=Controls.Cast<Control>().FirstOrDefault(c=>c.GetType().Name.Contains("UpDownButtons"));
        if(arrows!=null){arrows.BackColor=Theme.Elevated;arrows.Paint-=PaintArrows;arrows.Paint+=PaintArrows;}
    }
    void PaintArrows(object? sender,PaintEventArgs e){
        if(sender is not Control c)return;e.Graphics.Clear(BackColor);
        using var pen=new Pen(Enabled?Theme.Text:SystemColors.GrayText,1.2f);
        int x=c.Width/2,upper=c.Height/4,lower=3*c.Height/4;
        e.Graphics.DrawLines(pen,[new Point(x-3,upper+2),new Point(x,upper-1),new Point(x+3,upper+2)]);
        e.Graphics.DrawLines(pen,[new Point(x-3,lower-2),new Point(x,lower+1),new Point(x+3,lower-2)]);
    }
}
