namespace AnkaStudio;

public sealed partial class StudioForm
{
    void ConfigureExtraTracks(WorkflowTimeline timeline, WorkflowTimelineViewport viewport, VideoScript script, Action remember, Action changed)
    {
        void Refresh(){timeline.Height=timeline.RequiredHeight;viewport.Height=timeline.RequiredHeight+20;timeline.Invalidate();changed();}
        timeline.AddTrackRequested+=()=>{
            if(engine.Busy||!timeline.AllowEditing)return;
            var menu=new ContextMenuStrip();
            foreach(string kind in new[]{"Görüntü","Ses","Altyazı"}){string value=kind;menu.Items.Add("+ "+kind+" satırı",null,(_,_)=>{remember();(script.ExtraTracks??=[]).Add(new TimelineLayer{Kind=value,Name=value+" "+((script.ExtraTracks?.Count??0)+1)});Refresh();});}
            menu.Closed+=(_,_)=>menu.Dispose();menu.Show(timeline,new Point(12,28));
        };
        timeline.ExtraClipRequested+=(row,index)=>{
            if(engine.Busy||!timeline.AllowEditing||script.ExtraTracks==null)return;
            var track=script.ExtraTracks[row];
            if(index>=0){EditClip(track,index);return;}
            var menu=new ContextMenuStrip();
            menu.Items.Add("+ Klip ekle",null,(_,_)=>EditClip(track,-1));
            menu.Items.Add(track.Enabled?"Satırı sessize al / gizle":"Satırı etkinleştir",null,(_,_)=>{remember();track.Enabled=!track.Enabled;Refresh();});
            menu.Items.Add("Satır adını değiştir",null,(_,_)=>{
                using var dialog=Dialog("Satır adı",460,220);var name=Theme.Input(track.Name,width:370);name.Dock=DockStyle.Top;dialog.Controls.Add(name);var save=Theme.Button("Kaydet",()=>{if(name.Text.Trim()=="")return;remember();track.Name=name.Text.Trim();Refresh();dialog.Close();});save.Dock=DockStyle.Bottom;dialog.Controls.Add(save);dialog.ShowDialog(this);
            });
            menu.Items.Add("Satırı kaldır",null,(_,_)=>{remember();script.ExtraTracks.Remove(track);Refresh();});
            menu.Closed+=(_,_)=>menu.Dispose();menu.Show(timeline,new Point(12,323+row*72+28));
        };
        timeline.ExtraFileDropped+=(row,file)=>{if(!engine.Busy&&timeline.AllowEditing&&script.ExtraTracks!=null&&row<script.ExtraTracks.Count)EditClip(script.ExtraTracks[row],-1,file);};
        void EditClip(TimelineLayer track,int index,string? droppedFile=null)
        {
            var clip=index>=0?CreationFlow.Copy(track.Clips[index]):new TimelineClip{Start=Math.Clamp(timeline.PlayheadSeconds,0,Math.Max(0,timeline.Total-.1)),Duration=Math.Min(5,timeline.Total)};
            if(droppedFile!=null)clip.File=droppedFile;
            using var dialog=Dialog(track.Name+" • klip",740,650);
            var content=Theme.Flow(660);content.Dock=DockStyle.Fill;content.AutoSize=false;dialog.Controls.Add(content);
            var file=Theme.Label(clip.File==""?"Dosya seçilmedi":Path.GetFileName(clip.File),10,width:650,height:34);content.Controls.Add(file);
            if(track.Kind!="Altyazı")content.Controls.Add(Theme.Button("Dosya seç",()=>{using var pick=new OpenFileDialog{Filter=track.Kind=="Ses"?"Ses|*.mp3;*.wav;*.ogg;*.m4a;*.flac":"Görüntü|*.png;*.jpg;*.jpeg;*.webp;*.bmp;*.mp4;*.mov;*.mkv;*.webm"};if(pick.ShowDialog(dialog)==DialogResult.OK){clip.File=pick.FileName;file.Text=Path.GetFileName(clip.File);}},false,180));
            var text=Theme.Input(clip.Text,true,650);text.Height=75;if(track.Kind=="Altyazı")content.Controls.Add(text);
            var fields=new TableLayoutPanel{Width=650,Height=track.Kind=="Görüntü"?260:180,ColumnCount=2,RowCount=4};fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));content.Controls.Add(fields);int cell=0;
            NumericUpDown Number(string label,double value,decimal min,decimal max){var panel=Theme.Flow(310);panel.Margin=Padding.Empty;panel.Controls.Add(Theme.Label(label,9,width:300,height:26));var number=Theme.Number((decimal)Math.Clamp(value,(double)min,(double)max),min,max,290,2);panel.Controls.Add(number);fields.Controls.Add(panel,cell%2,cell/2);cell++;return number;}
            var start=Number("Başlangıç • saniye",clip.Start,0,(decimal)Math.Max(.1,timeline.Total-.1));var duration=Number("Süre • saniye",clip.Duration,.1m,(decimal)timeline.Total);
            NumericUpDown? source=null,volume=null,size=null,x=null,y=null;
            if(track.Kind!="Altyazı")source=Number("Dosyadan başlangıç • saniye",clip.SourceOffset,0,86400);
            if(track.Kind=="Ses")volume=Number("Ses seviyesi • yüzde",clip.Volume*100,0,100);
            if(track.Kind=="Görüntü"){size=Number("Görüntü genişliği • yüzde",clip.WidthPercent,5,100);x=Number("Yatay konum • yüzde",clip.XPercent,0,100);y=Number("Dikey konum • yüzde",clip.YPercent,0,100);}
            var rights=Theme.Check("Bu dosyayı kullanma iznim var",clip.RightsConfirmed,650);if(track.Kind!="Altyazı")content.Controls.Add(rights);
            var actions=CompactActions.Row(650);content.Controls.Add(actions);
            actions.Controls.Add(Theme.Button("Kaydet",()=>_=Safe(()=>{clip.Start=(double)start.Value;clip.Duration=Math.Min((double)duration.Value,timeline.Total-clip.Start);clip.Text=text.Text;clip.SourceOffset=(double)(source?.Value??0);clip.Volume=(double)(volume?.Value??50)/100;clip.WidthPercent=(int)(size?.Value??100);clip.XPercent=(int)(x?.Value??50);clip.YPercent=(int)(y?.Value??50);clip.RightsConfirmed=rights.Checked;clip.Validate(track.Kind,timeline.Total);remember();if(index>=0)track.Clips[index]=clip;else track.Clips.Add(clip);Refresh();dialog.Close();return Task.CompletedTask;}),true,180));
            if(index>=0)actions.Controls.Add(Theme.Button("Klibi kaldır",()=>{remember();track.Clips.RemoveAt(index);Refresh();dialog.Close();},false,180));
            foreach(Button button in actions.Controls)CompactActions.Apply(button);
            dialog.ShowDialog(this);
        }
    }
}
