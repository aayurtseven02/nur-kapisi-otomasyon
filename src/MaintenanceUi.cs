namespace AnkaStudio;



public sealed partial class StudioForm

{

    Panel? assistantHost;

    int assistantWidth=460;

    void ToggleAssistant()

    {

        if(assistantHost!=null){assistantHost.Visible=!assistantHost.Visible;PositionAssistant();return;}

        assistantHost=new Panel{Width=460,BackColor=Theme.Panel,Padding=new Padding(1),Visible=false};

        assistant=new ChatAssistant(store,()=>store.Episodes.FirstOrDefault(e=>e.Id==selectedWorkspaceId),()=>PageCaption(current)){Dock=DockStyle.Fill};

        assistantHost.Controls.Add(assistant);var grip=new Panel{Dock=DockStyle.Left,Width=7,BackColor=Theme.Teal,Cursor=Cursors.SizeWE};assistantHost.Controls.Add(grip);grip.BringToFront();int origin=0,initial=0;grip.MouseDown+=(_,e)=>{if(e.Button!=MouseButtons.Left)return;origin=Cursor.Position.X;initial=assistantWidth;grip.Capture=true;};grip.MouseMove+=(_,e)=>{if(!grip.Capture)return;assistantWidth=Math.Clamp(initial+origin-Cursor.Position.X,440,Math.Min(680,Math.Max(440,ClientSize.Width-800)));PositionAssistant();};grip.MouseUp+=(_,_)=>grip.Capture=false;Controls.Add(assistantHost);assistant.Show();assistantHost.Visible=true;

        Resize+=(_,_)=>PositionAssistant();PositionAssistant();

    }

    void PositionAssistant()

    {

        if(assistantHost==null)return;

        if(!assistantHost.Visible){assistantHost.Dock=DockStyle.None;Reflow();return;}

        if(ClientSize.Width>=1720){assistantHost.Dock=DockStyle.None;assistantHost.Bounds=new Rectangle(ClientSize.Width-assistantWidth-10,appHeader.Bottom+4,assistantWidth,Math.Max(500,ClientSize.Height-appHeader.Bottom-70));}

        else {assistantHost.Dock=DockStyle.None;assistantHost.Bounds=new Rectangle(Math.Max(228,ClientSize.Width-assistantWidth-10),appHeader.Bottom+4,Math.Min(assistantWidth,ClientSize.Width-238),Math.Max(500,ClientSize.Height-appHeader.Bottom-70));}

        assistantHost.BringToFront();Reflow();

    }

    void Maintenance()

    {

        using var dialog=Dialog("Kurulum, yedek ve bakım",850,760);

        var flow=Theme.Flow(760);flow.Dock=DockStyle.Fill;flow.AutoScroll=true;flow.AutoSize=false;dialog.Controls.Add(flow);

        flow.Controls.Add(Theme.Label(ToolHealth.Check(store),11,color:Theme.Muted,width:730,height:160));

        flow.Controls.Add(Theme.Button("Araçları çalıştırarak kontrol et",()=>_=Safe(async()=>{

            await Media.Exec(store.Ffmpeg,["-version"],null,CancellationToken.None);

            await Media.Exec(store.Probe,["-version"],null,CancellationToken.None);

            Message("FFmpeg ve FFprobe çalıştırıldı.\n"+ToolHealth.Check(store));

        }),false,380));

        flow.Controls.Add(Theme.Button("Proje yedeğini geri yükle",()=>_=Safe(async()=>{

            if(engine.Busy || queueRunning)throw new InvalidOperationException("Önce çalışan üretimi durdur.");

            using var picker=new OpenFileDialog{Filter="TubeFlow proje yedeği|*.ankaproject",RestoreDirectory=true};if(picker.ShowDialog(dialog)!=DialogResult.OK)return;

            var restored=await PortableProject.Restore(store,picker.FileName,CancellationToken.None);dialog.Close();OpenWorkspace(restored);

        }),false,380));

        flow.Controls.Add(Theme.Button("Eksik kaynakları yeniden bağla",RelinkSources,false,380));

        flow.Controls.Add(Theme.Button("Önbellek boyutunu göster",()=>_=Safe(async()=>{var bytes=await Task.Run(()=>StorageMaintenance.Bytes(store));Message("Yeniden üretilebilir ses / sahne önbelleği: "+StorageMaintenance.Size(bytes)+". Çıktılar, kaynak videolar ve kapaklar bu hesaba dahil değildir.");}),false,380));

        flow.Controls.Add(Theme.Button("Üretim önbelleğini temizle",()=>_=Safe(async()=>{

            if(engine.Busy || queueRunning)throw new InvalidOperationException("Önce çalışan üretimi durdur.");

            var bytes=await Task.Run(()=>StorageMaintenance.Bytes(store));

            if(Theme.Alert(dialog,StorageMaintenance.Size(bytes)+" yeniden üretilebilir önbellek silinsin mi? Sonraki üretim daha uzun sürebilir. Kaynaklar ve bitmiş videolar korunur.","Önbellek",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;

            var removed=await Task.Run(()=>StorageMaintenance.Clean(store));Message(StorageMaintenance.Size(removed)+" önbellek temizlendi.");

        }),false,380));

        flow.Controls.Add(Theme.Button("Temizlenmiş destek kaydı oluştur",()=>{

            using var picker=new SaveFileDialog{Filter="Metin|*.txt",FileName="TubeFlow-destek.txt"};if(picker.ShowDialog(dialog)!=DialogResult.OK)return;

            var text=ToolHealth.Check(store)+"\n\n"+string.Join("\n\n",store.Episodes.Select(e=>e.Topic+"\n"+string.Join("\n",e.Events.TakeLast(30))));

            File.WriteAllText(picker.FileName,SupportPrivacy.Clean(store,text));Theme.Open(picker.FileName);

        },false,380));

        flow.Controls.Add(Theme.Label("Uygulama şifresi giriş korumasıdır; proje video ve metinlerini şifrelemez. Destek kaydını paylaşmadan önce gözden geçir. Tam proje yedeği API anahtarlarını, parolayı ve hesap oturumlarını taşımaz.",10,color:Theme.Muted,width:730,height:100));

        ModernScrollbars.Apply(dialog);dialog.ShowDialog(this);

    }

    async Task ExportProject(Episode episode)

    {

        if(engine.Busy || queueRunning)throw new InvalidOperationException("Yedeklemeden önce çalışan üretimi durdur.");

        using var picker=new SaveFileDialog{Filter="TubeFlow proje yedeği|*.ankaproject",FileName="TubeFlow-proje.ankaproject",RestoreDirectory=true};if(picker.ShowDialog(this)!=DialogResult.OK)return;

        await PortableProject.Export(store,episode,picker.FileName,CancellationToken.None);Message("Kaynaklar ve çıktılarla proje yedeği oluşturuldu ve doğrulandı.");

    }

}

