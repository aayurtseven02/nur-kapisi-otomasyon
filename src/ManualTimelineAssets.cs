namespace AnkaStudio;

public sealed partial class StudioForm
{
    Control ManualTimelineAssets(Episode episode,CreationFlow flow,WorkflowTimeline timeline,int width,Action<int,string> assign,Action<string> selectMusic,Func<string,Task> selectEffect)
    {
        bool compact=width<650;
        var library=new StudioDisclosure("Manuel kurgu • görüntüler ve müzik arşivi",width,true);
        var tabs=new Panel{Width=width,Height=width<650?360:310,BackColor=Theme.Panel,AccessibleName="Manuel kurgu dosya kütüphanesi"};var tabBar=new FlowLayoutPanel{Dock=DockStyle.Top,Height=50,WrapContents=false};
        var visualPage=new Panel{Dock=DockStyle.Fill,BackColor=Theme.Panel,ForeColor=Theme.Text};
        var musicPage=new Panel{Dock=DockStyle.Fill,BackColor=Theme.Panel,ForeColor=Theme.Text,Visible=false};tabs.Controls.Add(visualPage);tabs.Controls.Add(musicPage);tabs.Controls.Add(tabBar);tabBar.Controls.Add(Theme.Button("Resim ve videolar",()=>{musicPage.Visible=false;visualPage.Visible=true;},false,Math.Max(110,(width-30)/2)));tabBar.Controls.Add(Theme.Button("Müzik",()=>{visualPage.Visible=false;musicPage.Visible=true;},false,Math.Max(110,(width-30)/2)));library.Content.Controls.Add(tabs);
        var images=new ImageList{ImageSize=new Size(104,64),ColorDepth=ColorDepth.Depth32Bit};
        var ownedThumbnails=new List<Bitmap>();
        var visuals=new ListView{Dock=DockStyle.Fill,View=View.LargeIcon,LargeImageList=images,MultiSelect=false,BackColor=Theme.Shell,ForeColor=Theme.Text,BorderStyle=BorderStyle.None,ShowItemToolTips=true,AccessibleName="Yüklenen resim ve videolar"};
        var controls=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=52,WrapContents=true};visualPage.Controls.Add(visuals);visualPage.Controls.Add(controls);
        void RefreshFiles(){visuals.Items.Clear();images.Images.Clear();foreach(var old in ownedThumbnails)old.Dispose();ownedThumbnails.Clear();foreach(string file in flow.Files.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase)){
            using var thumbFont=Theme.Font(9);var thumbnail=new Bitmap(104,64);using(var g=Graphics.FromImage(thumbnail)){g.Clear(Theme.Elevated);try{if(WorkflowMedia.ImageFile(file)){using var image=Image.FromFile(file);float scale=Math.Min(104f/image.Width,64f/image.Height);g.DrawImage(image,(104-image.Width*scale)/2,(64-image.Height*scale)/2,image.Width*scale,image.Height*scale);}else TextRenderer.DrawText(g,"▶ VIDEO",thumbFont,new Rectangle(0,0,104,64),Theme.Teal,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);}catch{TextRenderer.DrawText(g,"Okunamadı",Font,new Rectangle(0,0,104,64),Theme.Warning);}}
            // ImageList materializes native images lazily, when the list handle is
            // painted. Keep the source bitmap alive until the list is disposed.
            ownedThumbnails.Add(thumbnail);images.Images.Add(thumbnail);var item=new ListViewItem(Path.GetFileName(file),images.Images.Count-1){Tag=file,ToolTipText=file};visuals.Items.Add(item);
        }}
        void AssignSelected(){if(visuals.SelectedItems.Count>0)assign(timeline.Selected,(string)visuals.SelectedItems[0].Tag!);}
        controls.Controls.Add(Theme.Button(compact?"Yerleştir":"Seçili sahneye yerleştir",AssignSelected,true,compact?Math.Max(110,width-200):220));
        controls.Controls.Add(Theme.Button(compact?"Dosya +":"Dosya ekle",()=>{using var pick=new OpenFileDialog{Filter="Resim / video|*.png;*.jpg;*.jpeg;*.webp;*.bmp;*.mp4;*.mov;*.mkv;*.webm",Multiselect=true};if(pick.ShowDialog(this)!=DialogResult.OK)return;var folder=Path.Combine(store.Folder(episode),"input-visuals");Directory.CreateDirectory(folder);foreach(string file in pick.FileNames){string target=Path.Combine(folder,Reliability.FileHash(file)+Path.GetExtension(file));if(!File.Exists(target))File.Copy(file,target);if(!flow.Files.Contains(target,StringComparer.OrdinalIgnoreCase))flow.Files.Add(target);}flow.OwnVisuals=true;store.Save();RefreshFiles();},false,compact?70:140));
        controls.Controls.Add(Theme.Button(compact?"Ara":"Ücretsiz kaynaklarda ara",()=>FreeMediaDialog(async(file,kind)=>{if(kind=="Ses efekti")await selectEffect(file);else if(kind=="Müzik")selectMusic(file);else{if(!flow.Files.Contains(file,StringComparer.OrdinalIgnoreCase))flow.Files.Add(file);flow.OwnVisuals=true;assign(timeline.Selected,file);RefreshFiles();}}),false,compact?70:230));
        visuals.DoubleClick+=(_,_)=>AssignSelected();visuals.ItemDrag+=(_,e)=>{if(e.Item is ListViewItem item&&item.Tag is string file)visuals.DoDragDrop(new DataObject(DataFormats.FileDrop,new[]{file}),DragDropEffects.Copy);};
        var music=new ListBox{Dock=DockStyle.Fill,BackColor=Theme.Shell,ForeColor=Theme.Text,BorderStyle=BorderStyle.None,DisplayMember="Name",AccessibleName="Manuel kurgu müzik arşivi"};
        var player=new ArchiveMusicPlayer(compact:true){Dock=DockStyle.Bottom,Height=compact?96:130,Visible=!compact};
        var musicActions=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=52,WrapContents=true};musicPage.Controls.Add(music);musicPage.Controls.Add(player);musicPage.Controls.Add(musicActions);
        foreach(var track in new AudioLibrary(store).List().Where(x=>x.Kind is "Müzik" or "Ses efekti"&&File.Exists(x.File)))music.Items.Add(track);
        musicActions.Controls.Add(Theme.Button("Ön dinle",()=>{if(music.SelectedItem is AudioItem track){player.Visible=true;player.LoadAndPlay(track.File);}},false,compact?75:120));
        var placeAudio=Theme.Button("Müziği yerleştir",()=>_=Safe(async()=>{if(music.SelectedItem is AudioItem track){if(track.Kind=="Ses efekti")await selectEffect(track.File);else selectMusic(track.File);}}),true,compact?110:180);musicActions.Controls.Add(placeAudio);music.SelectedIndexChanged+=(_,_)=>placeAudio.Text=music.SelectedItem is AudioItem{Kind:"Ses efekti"}?"Efekti yerleştir":"Müziği yerleştir";
        musicActions.Controls.Add(Theme.Button(compact?"Dosya +":"Dosyadan seç",()=>{using var pick=new OpenFileDialog{Filter="Müzik|*.mp3;*.wav;*.ogg;*.m4a;*.flac"};if(pick.ShowDialog(this)==DialogResult.OK)selectMusic(pick.FileName);},false,compact?75:150));
        if(compact){foreach(var seek in player.Controls.OfType<TrackBar>())seek.Visible=false;var buttons=player.Controls.OfType<Button>().ToArray();buttons[0].Width=80;buttons[0].Text="Oynat / Ⅱ";buttons[1].Width=65;foreach(var clock in player.Controls.OfType<Label>().Where(x=>x.Width==110))clock.Width=90;}
        music.MouseDown+=(_,e)=>{int index=music.IndexFromPoint(e.Location);if(index>=0&&e.Clicks==2&&music.Items[index] is AudioItem track){if(track.Kind=="Ses efekti")_=Safe(()=>selectEffect(track.File));else selectMusic(track.File);}};
        library.Disposed+=(_,_)=>{images.Dispose();foreach(var image in ownedThumbnails)image.Dispose();ownedThumbnails.Clear();};RefreshFiles();return library;
    }
}
