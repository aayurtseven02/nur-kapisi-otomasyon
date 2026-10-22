namespace AnkaStudio;

public sealed partial class StudioForm
{
    Control AutonomousSetup(Episode e,CreationFlow flow,Settings settings,int width,Action dirty,Action redraw)
    {
        var panel=Theme.Flow(width);panel.AutoSize=true;panel.MaximumSize=new Size(width,0);panel.Margin=new Padding(0,0,0,8);
        panel.Controls.Add(Theme.Label("Üretim yöntemi",10,true,width:width-20,height:28));
        var mode=Theme.Combo(["Manuel • adımlarla düzenle","Yarı otonom • senaryoyu onayla","Tam otonom • üret ve yayınla"],flow.Job.Mode=="full"?"Tam otonom • üret ve yayınla":flow.Job.Mode=="review"?"Yarı otonom • senaryoyu onayla":"Manuel • adımlarla düzenle",width-20);panel.Controls.Add(mode);
        mode.SelectedIndexChanged+=(_,_)=>{
            flow.Job.Mode=mode.SelectedIndex==2?"full":mode.SelectedIndex==1?"review":"guided";
            flow.Job.Pending=false;flow.Job.Authorization="";
            if(flow.Job.Mode!="guided"){
                flow.Mode="auto";flow.Split=true;
                if(!flow.AiVisuals&&!flow.StockVisuals&&!flow.OwnVisuals){flow.AiVisuals=true;flow.StockVisuals=false;}
                flow.Subtitles=true;if(flow.SubtitleStyle=="")flow.SubtitleStyle="Sade beyaz";
            }
            dirty();BeginInvoke(redraw);
        };
        if(flow.Job.Mode=="guided")return panel;
        var options=new StudioDisclosure("Başlangıç planı • yayın ve görsel bütünlüğü",width-20,true);panel.Controls.Add(options);int inner=width-46;
        options.Content.Controls.Add(Theme.Label("Başlangıçta yaptığın yayın seçimi bu proje için talimattır. Tam otonom iş sonunda yeniden onay istemez. Yarı otonom senaryoyu incelemen için bekler.",10,width:inner,height:66));
        var publish=Theme.Combo(["Yayın kararını seç…","Üretim bitince hemen yayınla","Belirlediğim tarih ve saatte yayınla","Yalnız üret • önizlemede bekle"],flow.Job.PublishMode=="immediate"?"Üretim bitince hemen yayınla":flow.Job.PublishMode=="scheduled"?"Belirlediğim tarih ve saatte yayınla":flow.Job.PublishMode=="review"?"Yalnız üret • önizlemede bekle":"Yayın kararını seç…",inner);options.Content.Controls.Add(publish);
        var date=new DateTimePicker{Width=Math.Min(350,inner),Format=DateTimePickerFormat.Custom,CustomFormat="dd.MM.yyyy HH:mm",Value=e.PublishAt?.ToOffset(TimeSpan.FromHours(3)).DateTime??DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(3)).AddHours(2).DateTime,Visible=flow.Job.PublishMode=="scheduled"};date.AccessibleName="Otonom yayın tarihi ve saati • Türkiye";options.Content.Controls.Add(date);
        var defer=Theme.Check("Üretim yayın saatini aşarsa hazır olunca 30 dakika sonrasına planla",e.DeferMissedPublication,inner);defer.Visible=date.Visible;options.Content.Controls.Add(defer);defer.CheckedChanged+=(_,_)=>{e.DeferMissedPublication=defer.Checked;dirty();};
        void ReadDate(){e.PublishAt=new DateTimeOffset(DateTime.SpecifyKind(date.Value,DateTimeKind.Unspecified),TimeSpan.FromHours(3));dirty();}
        date.ValueChanged+=(_,_)=>ReadDate();
        publish.SelectedIndexChanged+=(_,_)=>{flow.Job.PublishMode=publish.SelectedIndex==1?"immediate":publish.SelectedIndex==2?"scheduled":publish.SelectedIndex==3?"review":"";date.Visible=defer.Visible=publish.SelectedIndex==2;if(date.Visible)ReadDate();else e.PublishAt=null;dirty();options.FitWidth(width-20);};
        var local=Theme.Check("Yerel motorda referanslı sahne resimleri üret",flow.AiVisuals,inner);var stock=Theme.Check("Konuyla ilgili ücretsiz stok videolarla birleştir",flow.StockVisuals,inner);options.Content.Controls.Add(local);options.Content.Controls.Add(stock);
        local.CheckedChanged+=(_,_)=>{flow.AiVisuals=local.Checked;e.AiStoryLocal=true;dirty();};stock.CheckedChanged+=(_,_)=>{flow.StockVisuals=stock.Checked;dirty();};
        int referenceStart=options.Content.Controls.Count;
        options.Content.Controls.Add(Theme.Label("Aynı karakter referansı yüz, kıyafet ve görsel tarz için bütün sahnelerde kullanılır. Seçmezsen konuya göre ortak referans hazırlanır.",10,width:inner,height:58));
        var reference=Theme.Label(flow.SceneReferenceFile==""?"Ortak referans: AI hazırlayacak":"Ortak referans: "+Path.GetFileName(flow.SceneReferenceFile),10,width:inner,height:32);options.Content.Controls.Add(reference);
        options.Content.Controls.Add(Theme.Button("Ortak referans görseli seç",()=>{using var file=new OpenFileDialog{Filter="Referans|*.png;*.jpg;*.jpeg;*.webp"};if(file.ShowDialog(this)!=DialogResult.OK)return;try{StoryImages.SelectReference(store,e,flow,file.FileName);reference.Text="Ortak referans: "+Path.GetFileName(flow.SceneReferenceFile);dirty();}catch(Exception ex){Message(ex.Message);}},false,Math.Min(350,inner)));
        var referenceControls=options.Content.Controls.Cast<Control>().Skip(referenceStart).ToArray();void RefreshReference(){foreach(var item in referenceControls)item.Visible=flow.AiVisuals;options.PerformLayout();}local.CheckedChanged+=(_,_)=>RefreshReference();RefreshReference();
        if(flow.OwnVisuals&&flow.Files.Count>0){var rights=Theme.Check("Eklediğim dosyaların bu videoda kullanımına izin veriyorum",flow.Job.OwnMediaConsent,inner);rights.CheckedChanged+=(_,_)=>{flow.Job.OwnMediaConsent=rights.Checked;dirty();};options.Content.Controls.Add(rights);}
        options.Content.Controls.Add(Theme.Label("Kapak: seçilmiş dosya korunur; seçim yoksa konuya uygun yerel AI kapağı hazırlanır. Ses: mevcut seçimin korunur. Altyazı: konuşmanın gerçek zamanları kullanılır.",10,width:inner,height:66));
        return panel;
    }
}
