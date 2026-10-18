namespace AnkaStudio;

public sealed partial class StudioForm
{
    void SetupChecklist()
    {
        Title("Yüklenmesi gerekenler","Kurulum ve API bağlantıları • eksiklerini gör, indir veya ayarlarını tamamla.");
        page.Controls.Add(Theme.Label("Tüm satırların yeşil olması şart değil. Yerel ve bulut servisleri alternatiflerdir; seçtiğin iş akışı için gerekenleri tamamla. Kayıtlı anahtar, çalışan model veya yeterli kota anlamına gelmez.",10,width:page.Width,height:66));
        var summary=Theme.Label("Sistem taraması hazırlanıyor…",11,true,width:page.Width,height:54);page.Controls.Add(summary);
        var refresh=Theme.Button("Güncelle • yeniden tara",()=>{},true,290);refresh.AccessibleName="Kurulum ve API listesini güncelle";page.Controls.Add(refresh);
        var stop=Theme.Button("Taramayı durdur",()=>{},false,220);page.Controls.Add(stop);
        var checkboxes=new Dictionary<string,(Label Status,Label Detail)>();var results=new Dictionary<string,SetupResult>();CancellationTokenSource? scan=null;bool running=false;
        var anchor=new Panel{Width=1,Height=1};page.Controls.Add(anchor);anchor.Disposed+=(_,_)=>scan?.Cancel();
        void AddRows(string title,IEnumerable<SetupCheck> definitions,bool account){
            page.Controls.Add(Theme.Label(title,16,true,width:page.Width,height:44));
            foreach(var definition in definitions){
                var card=new StudioCard{Width=page.Width-10,Height=172,Margin=new Padding(0,0,0,10)};int w=card.Width;
                var picture=new PictureBox{Location=new(14,14),Size=new(48,48),SizeMode=PictureBoxSizeMode.Zoom,AccessibleName=definition.Name+" amblemi"};
                string resource="AnkaStudio.Assets.requirements-"+definition.Logo+".png";using(var stream=typeof(StudioForm).Assembly.GetManifestResourceStream(resource)){if(stream!=null){using var image=Image.FromStream(stream);picture.Image=new Bitmap(image);}else if(definition.Logo=="tubeflow")picture.Image=Icon?.ToBitmap();else{var badge=new Bitmap(48,48);using var g=Graphics.FromImage(badge);g.Clear(Theme.Elevated);using var font=Theme.Font(12,true);TextRenderer.DrawText(g,definition.Name[..Math.Min(2,definition.Name.Length)].ToUpperInvariant(),font,new Rectangle(0,0,48,48),Theme.Text,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);picture.Image=badge;}}picture.Disposed+=(_,_)=>picture.Image?.Dispose();
                var name=Theme.Label(definition.Name,12,true,width:w-100,height:28);name.Location=new(78,12);
                var state=Theme.Label(account?(SetupChecks.Present(store,definition.Id)?"◷ Anahtar kayıtlı • doğrulama bekleniyor":"✕ Anahtar / bilgi eksik"):"◷ Kontrol ediliyor",10,true,Theme.Warning,w-100,28);state.Location=new(78,43);state.AccessibleName=definition.Name+" durumu";
                var note=Theme.Label("",9,color:Theme.Muted,width:w-100,height:40);note.Location=new(78,74);note.AutoEllipsis=true;
                var link=Theme.Button(account?"Hizmet / anahtar sayfası":"Resmî indirme / rehber",()=>Theme.Open(definition.Url),false,240);link.Location=new(78,120);
                var settings=Theme.Button("Kurulum / ayarlar",()=>{
                    if(definition.Action is "local" or "visual"){
                        using var dialog=Dialog(definition.Action=="local"?"Yerel AI kurulumu":"Yerel görsel motoru",960,800);var list=Theme.Flow(860);list.Dock=DockStyle.Fill;list.AutoScroll=true;dialog.Controls.Add(list);if(definition.Action=="local")LocalAiPanel(list);else LocalVisualPanel(list);dialog.ShowDialog(this);
                    }else Navigate(account&&definition.Id is "GOOGLE_BILLING_JSON" or "GOOGLE_CONTROL_JSON"?"Yönetim ayarları":"Bağlantı Merkezi");
                },false,220);settings.Location=new(330,120);
                card.Controls.AddRange([picture,name,state,note,link,settings]);page.Controls.Add(card);checkboxes[definition.Id]=(state,note);
            }
        }
        AddRows("Programlar ve yerel modeller",SetupChecks.Programs,false);AddRows("API anahtarları ve hesap bağlantıları",SetupChecks.Accounts,true);
        page.Controls.Add(Theme.Label("Neden yüklenmeli / hangi bağlantı gerekli?",16,true,width:page.Width,height:48));
        foreach(var row in SetupChecks.Programs.Concat(SetupChecks.Accounts))page.Controls.Add(Theme.Label(row.Name+" — "+row.Purpose,10,width:page.Width,height:Math.Max(56,32+(row.Purpose.Length/(Math.Max(40,page.Width/8)))*21)));
        page.Controls.Add(Theme.Label("Tarama sürücü kurmaz, üretim başlatmaz, API ayarlarını değiştirmez. API kontrolleri ilgili hizmete yetkili okuma isteği gönderir; stok araması ve YouTube okuması küçük API kotası kullanabilir. Anahtarlar ve yanıt içerikleri listeye veya tarama kaydına yazılmaz. İnternet/servis kapalıysa sonuç ‘kontrol edilemedi’ olur; bu, ‘kurulu değil’ demek değildir. Doğrulama yalnız belirtilen kontrol anı içindir.",10,color:Theme.Muted,width:page.Width,height:120));
        void Paint(string id,SetupResult result){if(anchor.IsDisposed)return;results[id]=result;var row=checkboxes[id];row.Status.Text=(result.Good==true?"✓ ":result.Good==false?"✕ ":"◷ ")+result.Status;row.Status.ForeColor=result.Good==true?Color.FromArgb(91,238,145):result.Good==false?Color.FromArgb(255,111,111):Theme.Warning;row.Detail.Text=result.Detail;hints.SetToolTip(row.Detail,result.Detail);}
        async Task Scan(){
            if(running||anchor.IsDisposed)return;running=true;refresh.Enabled=false;stop.Enabled=true;scan?.Dispose();scan=new();var token=scan.Token;results.Clear();foreach(var row in checkboxes.Values){row.Status.Text="◷ Tarama bekleniyor";row.Status.ForeColor=Theme.Warning;row.Detail.Text="";}
            try{
                foreach(var definition in SetupChecks.Programs.Concat(SetupChecks.Accounts)){
                    token.ThrowIfCancellationRequested();if(anchor.IsDisposed)return;summary.Text="Taranıyor • "+definition.Name+" • "+results.Count+"/"+checkboxes.Count;
                    var row=checkboxes[definition.Id];row.Status.Text="◷ Kontrol ediliyor…";row.Status.ForeColor=Theme.Warning;row.Detail.Text="";
                    try{using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromSeconds(25));var result=SetupChecks.Programs.Contains(definition)?await SetupChecks.Program(store,definition.Id,timeout.Token):await SetupChecks.Account(store,definition.Id,timeout.Token);Paint(definition.Id,result);}
                    catch(OperationCanceledException)when(!token.IsCancellationRequested){Paint(definition.Id,new(null,"Kontrol edilemedi","Bağlantı zaman aşımına uğradı. Servis / internet durumunu kontrol edip Güncelle’ye bas."));}
                    catch(Exception ex)when(ex is not OperationCanceledException){Paint(definition.Id,ex is ProviderException provider?SetupChecks.HttpFailure(provider.Status):new(null,"Kontrol edilemedi",ex is HttpRequestException?"Servise ulaşılamadı. İnternet veya yerel servisi kontrol et.":"Kurulum / bilgi yapısı doğrulanamadı. İlgili ayarlar bölümünü kontrol et."));}
                }
                if(!anchor.IsDisposed)summary.Text=$"Son tarama: {DateTime.Now:dd.MM.yyyy HH:mm:ss} • ✓ {results.Count(x=>x.Value.Good==true)} doğrulandı • ✕ {results.Count(x=>x.Value.Good==false)} eksik / hatalı • ◷ {results.Count(x=>x.Value.Good==null)} kontrol gerekli";
            }catch(OperationCanceledException){if(!anchor.IsDisposed){summary.Text="Tarama durduruldu • tamamlanan kontroller korundu.";foreach(var row in checkboxes.Where(x=>!results.ContainsKey(x.Key)))Paint(row.Key,new(null,"Kontrol edilmedi","Güncelle’ye basarak yeniden tara."));}}
            finally{running=false;if(!anchor.IsDisposed){refresh.Enabled=true;stop.Enabled=false;}}
        }
        refresh.Click+=async(_,_)=>await Scan();stop.Click+=(_,_)=>scan?.Cancel();BeginInvoke(async()=>{if(!anchor.IsDisposed)await Scan();});
    }
}
