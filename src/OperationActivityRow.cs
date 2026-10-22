namespace AnkaStudio;

public sealed class OperationActivityRow : TableLayoutPanel
{
    public AiTextActivity Activity{get;}=new(){Dock=DockStyle.Fill,Margin=Padding.Empty,BackColor=Color.Transparent,Font=Theme.Font(11,true),AccessibleName="Çalışan işlem"};
    public Button Stop{get;}
    public OperationActivityRow(Action stop)
    {
        BackColor=Theme.Bg;ColumnCount=2;RowCount=1;Padding=Padding.Empty;Margin=Padding.Empty;
        ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,CompactActions.Width+CompactActions.Gap));
        Stop=Theme.Button("Durdur",stop,false,CompactActions.Width);CompactActions.Apply(Stop);Stop.AccessibleName="Çalışan işlemi durdur";Stop.Anchor=AnchorStyles.Right;Stop.Margin=new Padding(CompactActions.Gap,0,0,0);
        Controls.Add(Activity,0,0);Controls.Add(Stop,1,0);
    }
}
