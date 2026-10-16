namespace AnkaStudio;

internal static class CoverArchive
{
    public static string Folder(Store store)=>Path.Combine(store.Root,"cover-library");
    public static string StoreCover(Store store,string file){Directory.CreateDirectory(Folder(store));var target=Path.Combine(Folder(store),Reliability.FileHash(file)+Path.GetExtension(file));if(!File.Exists(target))File.Copy(file,target);return target;}
    public static string[] Files(Store store,bool cards=false)
    {
        var folders=cards?new[]{Folder(store),Path.Combine(store.Root,"card-assets"),Path.Combine(store.Root,"cover-assets")}:new[]{Folder(store)};
        var bundled=cards&&ChannelPack.Matches(store.Settings)?new[]{"nur-kapisi-destek-karti.png","nur-kapisi-destek-gece.png","nur-kapisi-destek-sade.png"}.Select(ChannelPack.Asset).Where(File.Exists):Enumerable.Empty<string>();
        return folders.Where(Directory.Exists).SelectMany(f=>Directory.EnumerateFiles(f)).Concat(bundled).Where(f=>new[]{".png",".jpg",".jpeg",".bmp"}.Contains(Path.GetExtension(f).ToLowerInvariant())).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(Path.GetFileName).ToArray();
    }
    public static string? Pick(Store store,IWin32Window owner,string[]? archiveFiles=null,string title="Arşivden kapak seç")
    {
        var files=archiveFiles??Files(store,true);if(files.Length==0){Theme.Alert(owner,"Arşivde görsel yok. Önce görsel ekle veya yapay zekâ ile tasarla.");return null;}
        using var dialog=StudioForm.Dialog(title,900,670);
        var list=new ComboBox {Dock=DockStyle.Top,DropDownStyle=ComboBoxStyle.DropDownList,Font=Theme.Font(11)};
        list.Items.AddRange(files.Select(f=>Path.GetFileName(f)+" • "+Path.GetFileName(Path.GetDirectoryName(f))).ToArray());
        var picture=new PictureBox {Dock=DockStyle.Fill,SizeMode=PictureBoxSizeMode.Zoom,BackColor=Theme.Bg};
        var select=Theme.Button("Bu görseli seç",()=>{dialog.DialogResult=DialogResult.OK;dialog.Close();},true,240);select.Dock=DockStyle.Bottom;
        select.Enabled=false;
        list.SelectedIndexChanged+=(_,_)=>{select.Enabled=false;var old=picture.Image;picture.Image=null;old?.Dispose();try{using var image=Image.FromFile(files[list.SelectedIndex]);if((long)image.Width*image.Height>40_000_000)throw new InvalidOperationException();picture.Image=new Bitmap(image);select.Enabled=true;}catch{Theme.Alert(dialog,"Bu görsel okunamadı veya çok büyük.");}};
        dialog.Controls.Add(picture);dialog.Controls.Add(list);dialog.Controls.Add(select);list.SelectedIndex=0;
        try{return dialog.ShowDialog(owner)==DialogResult.OK?files[list.SelectedIndex]:null;}finally{picture.Image?.Dispose();}
    }
}

public sealed partial class StudioForm
{
    void AddArchiveCovers()
    {
        if(engine.Busy || poolImportBusy){Message("Çalışan işlem bitince kapak ekleyebilirsin.");return;}
        using var picker=new OpenFileDialog {Title="Arşive kapak ekle",Filter="Görseller|*.png;*.jpg;*.jpeg;*.bmp",Multiselect=true};
        if(picker.ShowDialog(this)!=DialogResult.OK)return;
        var folder=CoverArchive.Folder(store);Directory.CreateDirectory(folder);int added=0;
        foreach(var file in picker.FileNames){
            try{using var image=Image.FromFile(file);if((long)image.Width*image.Height>40_000_000)throw new InvalidOperationException("Görsel çok büyük.");
                var target=Path.Combine(folder,Path.GetFileNameWithoutExtension(file)+"-"+Reliability.FileHash(file)[..12]+Path.GetExtension(file).ToLowerInvariant());
                if(!File.Exists(target)){File.Copy(file,target);added++;}}
            catch(Exception ex){Message(Path.GetFileName(file)+": "+ex.Message);}
        }
        Message(added+" kapak arşive eklendi.");Navigate("Kapak tasarımları");
    }
}
