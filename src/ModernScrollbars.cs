using System.Drawing.Drawing2D;

using System.Runtime.CompilerServices;

using System.Runtime.InteropServices;



namespace AnkaStudio;



// Paint the native scrollbars without replacing their input, range or accessibility.

internal static class ModernScrollbars

{

    static readonly ConditionalWeakTable<Control, Skin> skins = new();

    static bool installed;

    public static void Install()

    {

        if(installed)return; installed=true;

        // ControlAdded already skins new children; do not traverse every control
        // after each scroll message.
        Application.Idle += (_,_) => { foreach(Form form in Application.OpenForms) if (!skins.TryGetValue(form, out _)) Apply(form); };

    }

    internal static void Apply(Control control)

    {

        if(control.IsDisposed)return;
        SoftSurfaces.Apply(control);

        if(control is ScrollableControl or TextBoxBase or ScrollBar or ListBox or ComboBox)

            skins.GetValue(control,c=>new Skin(c));

        foreach(Control child in control.Controls)Apply(child);

    }

    static void PaintTree(Control control) {

        if(control.IsDisposed)return;

        if(skins.TryGetValue(control,out var skin))skin.RefreshTheme();

        foreach(Control child in control.Controls)PaintTree(child);

    }

    [StructLayout(LayoutKind.Sequential)] struct Rect { public int Left,Top,Right,Bottom; }

    [StructLayout(LayoutKind.Sequential)] struct BarInfo

    {

        public int Size; public Rect Bounds; public int Arrow,ThumbTop,ThumbBottom,Reserved;

        [MarshalAs(UnmanagedType.ByValArray,SizeConst=6)] public int[] State;

    }

    [DllImport("user32.dll")] static extern bool GetScrollBarInfo(IntPtr hwnd,int id,ref BarInfo info);

    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd,out Rect rect);

    [DllImport("user32.dll")] static extern IntPtr GetWindowDC(IntPtr hwnd);

    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hwnd,IntPtr dc);

    [DllImport("user32.dll")] static extern bool ValidateRect(IntPtr hwnd,IntPtr rect);

    [DllImport("uxtheme.dll",CharSet=CharSet.Unicode)] static extern int SetWindowTheme(IntPtr hwnd,string subApp,string? subId);

    [StructLayout(LayoutKind.Sequential)] struct ComboInfo {public int Size;public Rect Item,Button;public int State;public IntPtr Combo,Edit,List;}

    [DllImport("user32.dll")] static extern bool GetComboBoxInfo(IntPtr hwnd,ref ComboInfo info);



    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd,int message,IntPtr wParam,IntPtr lParam);

    internal static bool HoverLeaveRetainsTheme(Control control)

    {

        var info=new BarInfo {Size=Marshal.SizeOf<BarInfo>(),State=new int[6]};

        if(!GetScrollBarInfo(control.Handle,-5,ref info)||!GetWindowRect(control.Handle,out var window))return false;

        var skin=skins.GetValue(control,c=>new Skin(c));

        SendMessage(control.Handle,0x00A0,new IntPtr(7),IntPtr.Zero);

        int previous=skin.PaintCalls;

        SendMessage(control.Handle,0x02A2,IntPtr.Zero,IntPtr.Zero);

        if(skin.PaintCalls<=previous)return false;

        previous=skin.PaintCalls;

        SendMessage(control.Handle,0x02A3,IntPtr.Zero,IntPtr.Zero);

        if(skin.PaintCalls<=previous)return false;

        Application.DoEvents();previous=skin.PaintCalls;

        var watch=System.Diagnostics.Stopwatch.StartNew();

        while(watch.ElapsedMilliseconds<350) {Application.DoEvents();Thread.Sleep(5);}

        if(skin.PaintCalls>previous+1)return false;

        using var image=new Bitmap(control.Width,control.Height);control.DrawToBitmap(image,new Rectangle(Point.Empty,control.Size));

        int x=(info.Bounds.Left+info.Bounds.Right)/2-window.Left;

        int y=info.Bounds.Top-window.Top+(info.ThumbTop+info.ThumbBottom)/2;

        return x>=0 && y>=0 && x<image.Width && y<image.Height && image.GetPixel(x,y).ToArgb()==Color.FromArgb(208,215,224).ToArgb();

    }



    internal static bool CornerUsesLightTheme(Control control)
    {
        var v=new BarInfo{Size=Marshal.SizeOf<BarInfo>(),State=new int[6]};var h=new BarInfo{Size=Marshal.SizeOf<BarInfo>(),State=new int[6]};
        if(!GetScrollBarInfo(control.Handle,-5,ref v)||!GetScrollBarInfo(control.Handle,-6,ref h)||(v.State[0]&0x18000)!=0||(h.State[0]&0x18000)!=0||!GetWindowRect(control.Handle,out var window))return false;
        using var image=new Bitmap(control.Width,control.Height);control.DrawToBitmap(image,new Rectangle(Point.Empty,control.Size));int x=(v.Bounds.Left+v.Bounds.Right)/2-window.Left,y=(h.Bounds.Top+h.Bounds.Bottom)/2-window.Top;
        return x>=0&&y>=0&&x<image.Width&&y<image.Height&&image.GetPixel(x,y).GetBrightness()>.8f;
    }

    sealed class Skin : NativeWindow

    {

        readonly Control control;

        bool painting;

        public int PaintCalls {get;private set;}

        Skin? popup;

        public Skin(Control control)

        {

            this.control=control;

            if(control is Form form)form.Shown+=(_,_)=>{Apply(form);PaintTree(form);};

            control.HandleCreated+=(_,_)=>Bind(control.Handle);

            control.HandleDestroyed+=(_,_)=>ReleaseHandle();

            control.ControlAdded+=(_,e)=> {if(e.Control!=null)Apply(e.Control);};

            if(control is ComboBox combo)combo.DropDown+=(_,_)=>{

                var info=new ComboInfo {Size=Marshal.SizeOf<ComboInfo>()};

                if(GetComboBoxInfo(combo.Handle,ref info) && info.List!=IntPtr.Zero && popup==null)popup=new Skin(combo,info.List);

            };

            if(control.IsHandleCreated)Bind(control.Handle);

        }

        Skin(Control owner,IntPtr handle) {control=owner;Bind(handle);owner.HandleDestroyed+=(_,_)=>ReleaseHandle();}

        void Bind(IntPtr handle) {

            AssignHandle(handle);

            if(control is Form)Shapes.DarkTitle(handle);

            // Keep the native fallback light too. Scroll tracking and

            // range changes can draw inside Windows before another paint message.

            // Do not change combo/form themes: they manage their own handles.

            if(control is Panel or TextBoxBase or ScrollBar or ListBox)SetWindowTheme(handle,"Explorer",null);

            Paint();

        }

        public void RefreshTheme()=>Paint();

        protected override void WndProc(ref Message message)

        {

            int kind=message.Msg;

            // Own the non-client scrollbar paint. Windows must not draw its white

            // hover frame underneath; repainting over it on Idle causes flicker.

            bool candidate=kind is 0x0085 or 0x00A0 or 0x02A2 || (control is ScrollBar && kind is 0x000F or 0x0200);

            bool bars=candidate && Handle!=IntPtr.Zero && control is not Form && HasBars();

            if(bars && (kind==0x0085 || kind==0x00A0 && message.WParam.ToInt64() is 6 or 7 || kind==0x02A2)) {

                Paint();message.Result=IntPtr.Zero;return;

            }

            if(control is ScrollBar && bars && (kind==0x000F || kind==0x0200 && (message.WParam.ToInt64()&1)==0)) {

                Paint();if(kind==0x000F)ValidateRect(Handle,IntPtr.Zero);message.Result=IntPtr.Zero;return;

            }

            base.WndProc(ref message);

            if(kind is 0x000F or 0x0114 or 0x0115 or 0x020A or 0x020E or 0x0005 or 0x02A3 or 0x00A1 or 0x00A2 or 0x0215 or 0x001F or 0x031A

                or 0x0047 or 0x007D or 0x0007 or 0x0008 or 0x000A or 0x000C or 0x0100 or 0x00B6 or 0x04DE or 0x00E0 or 0x00E2 or 0x00E6 or 0x00E9)

                Paint();

            else if(kind is 0x0317 or 0x0318)Paint(message.WParam);

        }

        bool HasBars()

        {

            foreach(int id in control is ScrollBar ? new[]{-4} : new[]{-5,-6}) {

                var info=new BarInfo {Size=Marshal.SizeOf<BarInfo>(),State=new int[6]};

                if(GetScrollBarInfo(Handle,id,ref info) && (info.State[0]&0x18000)==0 && info.Bounds.Right>info.Bounds.Left && info.Bounds.Bottom>info.Bounds.Top)return true;

            }

            return false;

        }

        void Paint(IntPtr printDc=default)

        {

            if(painting || Handle==IntPtr.Zero || control.IsDisposed)return;

            painting=true; IntPtr dc=IntPtr.Zero;

            try {

                dc=printDc!=IntPtr.Zero?printDc:GetWindowDC(Handle);

                if(dc==IntPtr.Zero)return;

                using var graphics=Graphics.FromHdc(dc);graphics.SmoothingMode=SmoothingMode.AntiAlias;

                if(control is ScrollBar bar)Draw(graphics,-4,bar is VScrollBar);

                else {Draw(graphics,-5,true);Draw(graphics,-6,false);DrawCorner(graphics);}

                PaintCalls++;

            } finally { if(dc!=IntPtr.Zero && printDc==IntPtr.Zero)ReleaseDC(Handle,dc);painting=false; }

        }

        void DrawCorner(Graphics graphics)
        {
            var v=new BarInfo{Size=Marshal.SizeOf<BarInfo>(),State=new int[6]};var h=new BarInfo{Size=Marshal.SizeOf<BarInfo>(),State=new int[6]};
            if(!GetScrollBarInfo(Handle,-5,ref v)||!GetScrollBarInfo(Handle,-6,ref h)||(v.State[0]&0x18000)!=0||(h.State[0]&0x18000)!=0||!GetWindowRect(Handle,out var window))return;
            var corner=new Rectangle(v.Bounds.Left-window.Left,h.Bounds.Top-window.Top,v.Bounds.Right-v.Bounds.Left,h.Bounds.Bottom-h.Bounds.Top);
            if(corner.Width<1||corner.Height<1)return;using var fill=new SolidBrush(Theme.Panel);graphics.FillRectangle(fill,corner);
        }

        void Draw(Graphics target,int id,bool vertical)

        {

            var info=new BarInfo { Size=Marshal.SizeOf<BarInfo>(),State=new int[6] };

            if(!GetScrollBarInfo(Handle,id,ref info) || (info.State[0]&0x18000)!=0 || !GetWindowRect(Handle,out var window))return;

            var bounds=new Rectangle(info.Bounds.Left-window.Left,info.Bounds.Top-window.Top,info.Bounds.Right-info.Bounds.Left,info.Bounds.Bottom-info.Bounds.Top);

            if(bounds.Width<3 || bounds.Height<3)return;

            using var buffer=new Bitmap(bounds.Width,bounds.Height);

            using var graphics=Graphics.FromImage(buffer);graphics.SmoothingMode=SmoothingMode.AntiAlias;

            graphics.TranslateTransform(-bounds.X,-bounds.Y);

            using var background=new SolidBrush(Theme.Panel);graphics.FillRectangle(background,bounds);
            using(var track=Shapes.Round(new Rectangle(bounds.X+1,bounds.Y+1,bounds.Width-3,bounds.Height-3),Math.Min(7,Math.Min(bounds.Width-3,bounds.Height-3)/2))){using var fill=new LinearGradientBrush(bounds,Color.White,Color.FromArgb(237,240,243),vertical?0f:90f);using var outline=new Pen(Color.Black,1);graphics.FillPath(fill,track);graphics.DrawPath(outline,track);}

            int inset=Math.Max(3,(vertical?bounds.Width:bounds.Height)/4);

            var thumb=vertical?new Rectangle(bounds.X+inset,bounds.Y+info.ThumbTop,bounds.Width-inset*2,info.ThumbBottom-info.ThumbTop):new Rectangle(bounds.X+info.ThumbTop,bounds.Y+inset,info.ThumbBottom-info.ThumbTop,bounds.Height-inset*2);

            if(thumb.Width>2 && thumb.Height>2 && (info.State[3]&1)==0) {

                int radius=Math.Min(4,Math.Min(thumb.Width,thumb.Height)/2);

                using var shape=Shapes.Round(thumb,radius);using var brush=new SolidBrush(Color.FromArgb(208,215,224));using var edge=new Pen(Color.Black,1);graphics.FillPath(brush,shape);graphics.DrawPath(edge,shape);

            }

            // Retain visible arrow targets at both ends, using understated chevrons.

            using var pen=new Pen(Color.Black,1.2f);

            int line=Math.Max(4,info.Arrow);

            foreach(bool end in new[]{false,true}) {

                int x=vertical?bounds.X+bounds.Width/2:end?bounds.Right-line/2:bounds.X+line/2;

                int y=vertical?(end?bounds.Bottom-line/2:bounds.Y+line/2):bounds.Y+bounds.Height/2;

                int sign=end?1:-1;

                Point[] points=vertical?[new(x-3,y-sign*2),new(x,y+sign),new(x+3,y-sign*2)]:[new(x-sign*2,y-3),new(x+sign,y),new(x-sign*2,y+3)];

                graphics.DrawLines(pen,points);

            }

            target.DrawImageUnscaled(buffer,bounds.Location);

        }

    }

}

