using System.Drawing.Drawing2D;

using System.Runtime.InteropServices;



namespace AnkaStudio;

internal sealed class StudioRedrawScope : IDisposable
{
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr handle, int message, IntPtr wParam, IntPtr lParam);
    readonly Control target;
    readonly bool paused;
    public StudioRedrawScope(Control target) {
        this.target = target;
        paused = target.IsHandleCreated && target.Visible;
        if (paused) SendMessage(target.Handle, 0x000B, IntPtr.Zero, IntPtr.Zero);
    }
    public void Dispose() {
        if (!paused || target.IsDisposed) return;
        SendMessage(target.Handle, 0x000B, new IntPtr(1), IntPtr.Zero);
        target.Invalidate(true);
    }
}

public sealed class StudioFlowPanel : FlowLayoutPanel
{
    [System.ComponentModel.DefaultValue(false)]
    public bool KeepScrollOnFocus { get; set; }
    public StudioFlowPanel() { DoubleBuffered = true; SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); }
    protected override Point ScrollToControl(Control activeControl) => KeepScrollOnFocus ? DisplayRectangle.Location : base.ScrollToControl(activeControl);
}

public sealed class StudioScrollPanel : Panel
{
    public StudioScrollPanel() { DoubleBuffered = true; SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); }
    protected override CreateParams CreateParams {
        get { var parameters = base.CreateParams; parameters.ExStyle |= 0x02000000; return parameters; }
    }
    protected override void OnScroll(ScrollEventArgs e) {
        base.OnScroll(e);
        Invalidate(true);
    }
}



public static class Shapes

{

    public static GraphicsPath Round(Rectangle r, int radius)

    {

        var p = new GraphicsPath(); var d = radius * 2;

        p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90); p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p;

    }

    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr window,int attribute,out int value,int size);

    internal static int? CaptionColor(IntPtr handle)=>DwmGetWindowAttribute(handle,35,out var value,4)==0?value:null;

    public static void DarkTitle(IntPtr handle) {

        try {

            int dark=0;DwmSetWindowAttribute(handle,20,ref dark,4);
            int rounded=2;DwmSetWindowAttribute(handle,33,ref rounded,4);

            int caption=ColorTranslator.ToWin32(Theme.Bg),text=ColorTranslator.ToWin32(Theme.Teal),border=ColorTranslator.ToWin32(Theme.Border);

            DwmSetWindowAttribute(handle,35,ref caption,4);DwmSetWindowAttribute(handle,36,ref text,4);DwmSetWindowAttribute(handle,34,ref border,4);

        }catch { }

    }

}

public class StudioButton : Button

{

    public bool Completed;
    public bool CurrentStep;

    public string? LeadingChevron;
    public bool WrapCaption;
    public string? LeadingIcon;
    public bool UppercaseCaption;

    readonly ToolTip helpTip=new(){AutoPopDelay=20000,InitialDelay=350,ReshowDelay=100,ShowAlways=true};

    bool helpPressed;
    bool pressSoundPlayed;

    public string HelpText => ButtonHelp.Explain(Text);

    public bool HasHelp => Width>=64 && ButtonHelp.NeedsInfo(Text) && !(Parent?.Controls.OfType<Button>().Any(b=>Equals(b.Tag,"publication-info"))??false);

    public Rectangle HelpBounds => new(Math.Max(0,Width-24),3,18,18);

    bool pressed; bool hovered;

    public StudioButton() { DoubleBuffered = true; SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); TextAlign = ContentAlignment.MiddleCenter; }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        if (Parent == null) { base.OnPaintBackground(e); return; }
        var state = e.Graphics.Save();
        e.Graphics.TranslateTransform(-Left, -Top);
        InvokePaintBackground(Parent, new PaintEventArgs(e.Graphics, Parent.ClientRectangle));
        e.Graphics.Restore(state);
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        // Native window regions clip antialiased corners to whole pixels.
        // Paint the rounded face over the parent background instead.
        var previous = Region; Region = null; previous?.Dispose();
    }

    protected override void OnTextChanged(EventArgs e) {base.OnTextChanged(e);helpTip.SetToolTip(this,ButtonHelp.NeedsInfo(Text)?HelpText:null);}

    protected override void Dispose(bool disposing) {if(disposing)helpTip.Dispose();base.Dispose(disposing);}

    protected override void OnClick(EventArgs e) { if(helpPressed){ShowHelp();return;} if (Enabled && !pressSoundPlayed) UiSound.Click(); base.OnClick(e); }

    public void ShowHelp() { if (!pressSoundPlayed) UiSound.Click(); Theme.Alert(FindForm(),HelpText,Text+" • bilgi"); }

    protected override void OnMouseEnter(EventArgs e) { hovered=true;Invalidate();base.OnMouseEnter(e); }

    protected override void OnMouseLeave(EventArgs e) { hovered=false;Invalidate();base.OnMouseLeave(e); }

    protected override void OnMouseDown(MouseEventArgs e) { pressSoundPlayed = Enabled && e.Button == MouseButtons.Left; if (pressSoundPlayed) UiSound.Click(); helpPressed=HasHelp && e.Button==MouseButtons.Left && HelpBounds.Contains(e.Location); pressed = !helpPressed; Invalidate(); base.OnMouseDown(e); }

    protected override void OnMouseUp(MouseEventArgs e) {

        pressed=false;Invalidate();

        if(helpPressed){helpPressed=false;Capture=false;try { if(HelpBounds.Contains(e.Location))ShowHelp(); } finally { pressSoundPlayed=false; } return;}

        try { base.OnMouseUp(e); } finally { pressSoundPlayed=false; }

    }

    protected override void OnKeyDown(KeyEventArgs e) {if(e.KeyCode==Keys.F1 && HasHelp){ShowHelp();e.Handled=true;e.SuppressKeyPress=true;return;} if (e.KeyCode == Keys.Space) { pressed = true; Invalidate(); } base.OnKeyDown(e); }

    protected override void OnKeyUp(KeyEventArgs e) { pressed = false; Invalidate(); base.OnKeyUp(e); }

    protected override void OnPaint(PaintEventArgs e)

    {

        if(Width<8||Height<8)return;

        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        int offset = 0;
        var face = new Rectangle(1, 1, Width - 3, Height - 3);
        using var shape=Shapes.Round(face,Math.Min(11,Math.Min(face.Width,face.Height)/2));
        Color top=Completed?Color.FromArgb(240,252,242):Color.FromArgb(253,254,255);
        Color bottom=Completed?Theme.SuccessSurface:pressed?Color.FromArgb(218,224,230):hovered?Color.FromArgb(230,235,240):Color.FromArgb(238,241,244);
        if(!Enabled){top=Color.FromArgb(249,250,251);bottom=Color.FromArgb(242,243,245);}
        using(var fill=new LinearGradientBrush(face,top,bottom,90f))g.FillPath(fill,shape);
        using(var border=new Pen(Completed?Color.FromArgb(137,185,150):CurrentStep||Focused?Color.FromArgb(146,158,172):Color.FromArgb(201,208,216),CurrentStep||Focused?1.5f:1f))g.DrawPath(border,shape);
        var shine=g.Save();g.SetClip(shape);using(var sheen=new SolidBrush(Color.FromArgb(80,Color.White)))g.FillRectangle(sheen,face.X+1,face.Y+1,face.Width-2,Math.Max(1,face.Height/2));g.Restore(shine);
        Color ink=Enabled?Theme.Text:Theme.Muted;

        if (Completed && !Equals(Tag,"wizard-step")) {

            using var tick = new Pen(Theme.Success, 2.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };

            int center = offset + (Height - 7) / 2;

            g.DrawLines(tick, [new Point(16, center), new Point(22, center + 6), new Point(34, center - 7)]);

        }

        // Reserve equal space on both sides so icons never shift the text off center.
        int inset = Equals(Tag,"wizard-step") ? 8 : Math.Max(Completed ? 44 : LeadingChevron != null ? 25 : LeadingIcon != null ? 32 : 10, HasHelp ? 27 : 10);
        var rect = new Rectangle(inset, offset, Math.Max(1, Width - inset * 2), Height - offset);

        if(Text is "i" or "ⓘ")rect=new Rectangle(0,offset,Width,Height-offset);

        var alignment=TextAlign is ContentAlignment.TopLeft or ContentAlignment.MiddleLeft or ContentAlignment.BottomLeft ? TextFormatFlags.Left : TextAlign is ContentAlignment.TopRight or ContentAlignment.MiddleRight or ContentAlignment.BottomRight ? TextFormatFlags.Right : TextFormatFlags.HorizontalCenter;
        var flags = alignment | TextFormatFlags.VerticalCenter | (WrapCaption ? TextFormatFlags.WordBreak : TextFormatFlags.SingleLine) | TextFormatFlags.EndEllipsis;

        // Fit text inside the existing button; painting never changes its bounds.
        var displayText=Text.Replace('\r',' ').Replace('\n',' ');
        if (UppercaseCaption) displayText = displayText.ToUpper(System.Globalization.CultureInfo.GetCultureInfo("tr-TR"));
        float fittedSize = Font.Size;
        Font? fitted = null;
        try {
            while (!WrapCaption && fittedSize > 8f && TextRenderer.MeasureText(displayText, fitted ?? Font, Size.Empty, flags & ~TextFormatFlags.EndEllipsis).Width > rect.Width) {
                fittedSize -= .25f; fitted?.Dispose(); fitted = new Font(Font.FontFamily, fittedSize, Font.Style);
            }
            var paintFont = fitted ?? Font;
            if(!ButtonHelp.NeedsInfo(Text) && TextRenderer.MeasureText(displayText,paintFont,Size.Empty,flags & ~TextFormatFlags.EndEllipsis).Width>rect.Width)helpTip.SetToolTip(this,Text);
            TextRenderer.DrawText(g, displayText, paintFont, rect, Text is "i" or "ⓘ" ? Theme.Warning : ink, flags);
        } finally { fitted?.Dispose(); }

        if(HasHelp){var infoColor=Theme.Warning;using var infoPen=new Pen(infoColor,1.2f);g.DrawEllipse(infoPen,HelpBounds);using var infoFont=Theme.Font(8,true);TextRenderer.DrawText(g,"i",infoFont,HelpBounds,infoColor,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);}

        if (LeadingChevron != null) TextRenderer.DrawText(g, LeadingChevron, Font, new Rectangle(7, offset, 15, Height-offset), ink, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        if (LeadingIcon != null && LeadingChevron == null && !Completed) {
            float x = 11, y = (Height - 16) / 2f + offset;
            using var iconPen = new Pen(ink, 1.5f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
            if (LeadingIcon == "music") {
                g.DrawLine(iconPen, x + 6, y + 12, x + 6, y + 2); g.DrawLine(iconPen, x + 6, y + 2, x + 15, y);
                g.DrawLine(iconPen, x + 15, y, x + 15, y + 10);
                g.DrawEllipse(iconPen, x, y + 11, 6, 4); g.DrawEllipse(iconPen, x + 9, y + 9, 6, 4);
            } else {
                g.DrawRectangle(iconPen, x, y, 16, 16);
                if (LeadingIcon == "film") {
                    g.DrawLine(iconPen, x + 4, y, x + 4, y + 16); g.DrawLine(iconPen, x + 12, y, x + 12, y + 16);
                    foreach (var row in new[] { 4, 8, 12 }) { g.DrawLine(iconPen, x, y + row, x + 4, y + row); g.DrawLine(iconPen, x + 12, y + row, x + 16, y + row); }
                } else {
                    g.DrawEllipse(iconPen, x + 10, y + 3, 3, 3);
                    g.DrawLines(iconPen, [new PointF(x + 2, y + 13), new PointF(x + 6, y + 8), new PointF(x + 10, y + 12), new PointF(x + 13, y + 9), new PointF(x + 15, y + 13)]);
                }
            }
        }

    }

}

public sealed class StudioLabel : Label

{

    public StudioLabel() { DoubleBuffered = true; SetStyle(ControlStyles.ResizeRedraw, true); }

    protected override void OnForeColorChanged(EventArgs e) { if (ForeColor == Theme.Teal) ForeColor = Theme.Text; base.OnForeColorChanged(e); }

    protected override void OnPaint(PaintEventArgs e) { base.OnPaint(e); }

}

public sealed class StudioGradientPanel : Panel
{
    public StudioGradientPanel() { DoubleBuffered = true; SetStyle(ControlStyles.ResizeRedraw, true); }
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        if (ClientSize.Width < 1 || ClientSize.Height < 1) return;
        using var gradient = new LinearGradientBrush(ClientRectangle, Theme.Bg, Theme.Bg, 35f);
        e.Graphics.FillRectangle(gradient, ClientRectangle);
    }
}

public sealed class StudioCard : Panel

{

    public bool Completed;
    public bool CurrentStep;

    public StudioCard() { DoubleBuffered = true; SetStyle(ControlStyles.ResizeRedraw, true); }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        if (Parent == null) { base.OnPaintBackground(e); return; }
        var state = e.Graphics.Save();
        e.Graphics.TranslateTransform(-Left, -Top);
        InvokePaintBackground(Parent, new PaintEventArgs(e.Graphics, Parent.ClientRectangle));
        e.Graphics.Restore(state);
    }

    protected override void OnPaint(PaintEventArgs e)

    {

        if(Width<8||Height<8)return;

        base.OnPaint(e); var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;

        using var path = Shapes.Round(new(0, 0, Width - 1, Height - 1), 10);

        using var fill = new SolidBrush(Completed ? Theme.SuccessSurface : BackColor); g.FillPath(fill, path);
        using var border = new Pen(CurrentStep ? Theme.Teal : Completed ? Theme.Success : Theme.Border, CurrentStep ? 3 : 1); g.DrawPath(border, path);

    }

}

public sealed class StudioHeader : Control

{

    public string PageName = "Stüdyo";

    public string Channel = "";
    public string PublicationSummary = "";
    public string PublicationSummaryShort = "";

    public bool Busy;

    public StudioHeader() { Height = 66; DoubleBuffered = true; SetStyle(ControlStyles.ResizeRedraw, true); BackColor = Theme.Bg; }

    protected override void OnPaint(PaintEventArgs e)

    {

        base.OnPaint(e); var g = e.Graphics; using var f = Theme.Font(10); using var b = Theme.Font(10, true);

        TextRenderer.DrawText(g, "ÇALIŞMA ALANI  /  " + PageName, b, new Rectangle(28, 32, Math.Max(100, Width - 210), 28), Theme.Muted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        int workspaceWidth=TextRenderer.MeasureText("ÇALIŞMA ALANI  /  "+PageName,b).Width;
        int statsLeft=28+workspaceWidth+26;
        int statsWidth=Width-230-statsLeft;
        if(statsWidth>150) TextRenderer.DrawText(g,statsWidth>730?PublicationSummary:PublicationSummaryShort,f,new Rectangle(statsLeft,32,statsWidth,28),Theme.Success,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis);

        using var channelFont = Theme.Font(14, true);

        TextRenderer.DrawText(g, string.IsNullOrWhiteSpace(Channel) || Channel == "YouTube kanalı bağlı değil" ? "YOUTUBE KANALI BAĞLI DEĞİL" : "BAĞLI OLAN YOUTUBE KANALI: " + Channel.ToUpper(System.Globalization.CultureInfo.GetCultureInfo("tr-TR")), channelFont, new Rectangle(28, 3, Math.Max(100, Width - 240), 30), Theme.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);

        using var stateFont = Theme.Font(Busy ? 10 : 14, true);
        TextRenderer.DrawText(g, Busy ? "ÜRETİM ÇALIŞIYOR" : "HAZIR", stateFont, new Rectangle(Width - 220, 0, 190, Height), Busy ? Theme.Text : Theme.Success, TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);

        using var pen = new Pen(Theme.Warning); g.DrawLine(pen, 28, Height - 1, Width - 30, Height - 1);

    }

}

public sealed class Pipeline : Control

{

    public Pipeline() { DoubleBuffered = true; SetStyle(ControlStyles.ResizeRedraw, true); Height = 66; }

    protected override void OnPaint(PaintEventArgs e)

    {

        base.OnPaint(e); var names = new[] { "İÇERİK", "SAHNELER", "SES / TASARIM", "ÜRET / İNCELE", "YAYIN" }; var step = Width / 5;

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; using var f = Theme.Font(9, true);

        for (int i = 0; i < 5; i++) { var x = i * step; using var fill = new SolidBrush(i == 0 ? Theme.Teal : Theme.Elevated); e.Graphics.FillEllipse(fill, x, 5, 28, 28); TextRenderer.DrawText(e.Graphics, (i + 1).ToString("00"), f, new Rectangle(x, 5, 28, 28), i == 0 ? Theme.Bg : Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter); TextRenderer.DrawText(e.Graphics, names[i], f, new Rectangle(x + 36, 4, step - 42, 30), Theme.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis); if (i < 4) { using var line = new Pen(Theme.Border); e.Graphics.DrawLine(line, x + 36, 46, x + step - 12, 46); } }

    }

}





