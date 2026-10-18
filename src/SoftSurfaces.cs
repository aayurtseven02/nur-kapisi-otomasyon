using System.Runtime.CompilerServices;
namespace AnkaStudio;

internal static class SoftSurfaces
{
    static readonly ConditionalWeakTable<Control,object> applied=new();
    public static void Apply(Control control)
    {
        if(control is ToolStrip strip) { strip.Renderer=new ToolStripProfessionalRenderer(new StudioMenuColors());strip.BackColor=Theme.Panel;strip.ForeColor=Theme.Text;foreach(ToolStripItem item in strip.Items){item.BackColor=Theme.Panel;item.ForeColor=Theme.Text;} }
        if(control is StudioTextBox or StudioNumber||control is not (ComboBox or NumericUpDown or ListBox or DataGridView or PictureBox)&&!(control is Panel panel&&panel.BorderStyle!=BorderStyle.None))return;
        if(applied.TryGetValue(control,out _))return;applied.Add(control,new object());
        void Round(){if(control.IsDisposed||control.Width<8||control.Height<8)return;using var shape=Shapes.Round(new Rectangle(0,0,control.Width,control.Height),Math.Min(10,Math.Min(control.Width,control.Height)/2));var old=control.Region;control.Region=new Region(shape);old?.Dispose();}
        if(control is DataGridView grid){void Headings(){grid.EnableHeadersVisualStyles=false;grid.ColumnHeadersDefaultCellStyle.Alignment=DataGridViewContentAlignment.MiddleLeft;grid.ColumnHeadersDefaultCellStyle.Font=Theme.Font(10,true);grid.ColumnHeadersDefaultCellStyle.BackColor=Theme.Elevated;grid.ColumnHeadersDefaultCellStyle.SelectionBackColor=Theme.Elevated;grid.ColumnHeadersDefaultCellStyle.ForeColor=grid.ColumnHeadersDefaultCellStyle.SelectionForeColor=Theme.Text;grid.BackgroundColor=Theme.Bg;grid.GridColor=Theme.Border;grid.DefaultCellStyle.BackColor=Theme.Panel;grid.DefaultCellStyle.ForeColor=Theme.Text;grid.DefaultCellStyle.SelectionBackColor=Theme.AccentSurface;grid.DefaultCellStyle.SelectionForeColor=Theme.Text;grid.AlternatingRowsDefaultCellStyle.BackColor=Theme.Bg;}grid.ColumnAdded+=(_,_)=>Headings();grid.DataBindingComplete+=(_,_)=>Headings();Headings();}
        control.SizeChanged+=(_,_)=>Round();control.HandleCreated+=(_,_)=>Round();Round();
    }
}

internal sealed class StudioMenuColors : ProfessionalColorTable
{
    public override Color MenuItemSelected => Theme.Selection;
    public override Color MenuItemBorder => Theme.Border;
    public override Color MenuBorder => Theme.Border;
    public override Color ToolStripDropDownBackground => Theme.Panel;
    public override Color ImageMarginGradientBegin => Theme.Panel;
    public override Color ImageMarginGradientMiddle => Theme.Panel;
    public override Color ImageMarginGradientEnd => Theme.Panel;
    public override Color MenuItemSelectedGradientBegin => Theme.Selection;
    public override Color MenuItemSelectedGradientEnd => Theme.Selection;
    public override Color MenuItemPressedGradientBegin => Theme.Hover;
    public override Color MenuItemPressedGradientEnd => Theme.Hover;
}
