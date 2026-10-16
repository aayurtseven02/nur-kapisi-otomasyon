using System.Runtime.CompilerServices;
namespace AnkaStudio;

internal static class SoftSurfaces
{
    static readonly ConditionalWeakTable<Control,object> applied=new();
    public static void Apply(Control control)
    {
        if(control is StudioTextBox or StudioNumber||control is not (ComboBox or NumericUpDown or ListBox or DataGridView or PictureBox)&&!(control is Panel panel&&panel.BorderStyle!=BorderStyle.None))return;
        if(applied.TryGetValue(control,out _))return;applied.Add(control,new object());
        void Round(){if(control.IsDisposed||control.Width<8||control.Height<8)return;using var shape=Shapes.Round(new Rectangle(0,0,control.Width,control.Height),Math.Min(10,Math.Min(control.Width,control.Height)/2));var old=control.Region;control.Region=new Region(shape);old?.Dispose();}
        if(control is DataGridView grid){void Headings(){grid.EnableHeadersVisualStyles=false;grid.ColumnHeadersDefaultCellStyle.Alignment=DataGridViewContentAlignment.MiddleLeft;grid.ColumnHeadersDefaultCellStyle.Font=Theme.Font(10,true);grid.ColumnHeadersDefaultCellStyle.BackColor=Theme.Elevated;grid.ColumnHeadersDefaultCellStyle.SelectionBackColor=Theme.Elevated;grid.ColumnHeadersDefaultCellStyle.ForeColor=grid.ColumnHeadersDefaultCellStyle.SelectionForeColor=Theme.Text;foreach(DataGridViewColumn column in grid.Columns)column.HeaderText=column.HeaderText.ToUpper(System.Globalization.CultureInfo.GetCultureInfo("tr-TR"));}grid.ColumnAdded+=(_,_)=>Headings();grid.DataBindingComplete+=(_,_)=>Headings();Headings();}
        control.SizeChanged+=(_,_)=>Round();control.HandleCreated+=(_,_)=>Round();Round();
    }
}
