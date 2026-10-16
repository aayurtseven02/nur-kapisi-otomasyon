using System.Text.Json;
namespace AnkaStudio;
public sealed partial class StudioForm
{
    void RelinkSources()
    {
        using var dialog=Dialog("Eksik proje dosyalarını yeniden bağla",950,700);
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4};
        foreach(var height in new[]{45,0,50,60})layout.RowStyles.Add(height==0?new RowStyle(SizeType.Percent,100):new RowStyle(SizeType.Absolute,height));
        var projects=Theme.Combo(store.Episodes.Select(e=>e.Topic+" • "+e.Id[..8]).ToArray(),store.Episodes.FirstOrDefault()?.Topic??"");projects.Dock=DockStyle.Fill;
        var list=new ListBox{Dock=DockStyle.Fill,BackColor=Theme.Panel,ForeColor=Theme.Text,Font=Theme.Font(),HorizontalScrollbar=true};
        var copy=Theme.Check("Seçtiğim dosyayı proje klasörüne kopyala",true,700);var actions=new FlowLayoutPanel{Dock=DockStyle.Fill};
        Episode? selected=null;
        void Load(){selected=projects.SelectedIndex>=0?store.Episodes[projects.SelectedIndex]:null;list.Items.Clear();if(selected==null)return;foreach(var path in PortableProject.Paths(JsonSerializer.SerializeToNode(selected,Json.Options)).Distinct(StringComparer.OrdinalIgnoreCase).Where(p=>!File.Exists(p)))list.Items.Add(path);}
        actions.Controls.Add(Theme.Button("Seçili dosyayı yeniden bağla",()=>_=Safe(async()=>{
            if(engine.Busy || queueRunning)throw new InvalidOperationException("Önce çalışan üretimi durdur.");
            if(selected==null || list.SelectedItem is not string missing)return;
            var episode=selected;using var picker=new OpenFileDialog{Title="Dosyayı bul: "+Path.GetFileName(missing),RestoreDirectory=true};if(picker.ShowDialog(dialog)!=DialogResult.OK)return;
            if(!Path.GetExtension(picker.FileName).Equals(Path.GetExtension(missing),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Aynı dosya türünü seç.");
            using var lease=new OperationLease(store);
            if(episode.Plan!=null)foreach(var (key,vertical,script) in Rules.Scripts(episode)){
                if(script.Scenes.Any(s=>s.StockFile.Equals(missing,StringComparison.OrdinalIgnoreCase)))await MediaFormat.Video(store,picker.FileName,key!="long",CancellationToken.None);
                var settings=VideoSettings.For(store.Settings,episode,script);
                if(new[]{settings.CoverPng,settings.OpeningCardFile,settings.ClosingCardFile}.Any(p=>p.Equals(missing,StringComparison.OrdinalIgnoreCase)))MediaFormat.Image(picker.FileName,key!="long");
            }
            string replacement=picker.FileName;if(copy.Checked){var folder=Path.Combine(store.Folder(episode),"source-assets");Directory.CreateDirectory(folder);replacement=Path.Combine(folder,Guid.NewGuid().ToString("N")+Path.GetExtension(picker.FileName));File.Copy(picker.FileName,replacement);}
            var node=JsonSerializer.SerializeToNode(episode,Json.Options)!;PortableProject.Remap(node,new(StringComparer.OrdinalIgnoreCase){[missing]=replacement});var restored=node.Deserialize<Episode>(Json.Options)!;restored.Approved=false;
            store.Episodes[store.Episodes.IndexOf(episode)]=restored;store.Save();Load();Message("Dosya yeniden bağlandı. Üretim ve yayın öncesinde projenin güncelliğini tekrar denetle.");
        }),true,340));
        projects.SelectedIndexChanged+=(_,_)=>Load();if(projects.Items.Count>0)projects.SelectedIndex=0;Load();
        layout.Controls.Add(projects,0,0);layout.Controls.Add(list,0,1);layout.Controls.Add(copy,0,2);layout.Controls.Add(actions,0,3);dialog.Controls.Add(layout);ModernScrollbars.Apply(dialog);dialog.ShowDialog(this);
    }
}



