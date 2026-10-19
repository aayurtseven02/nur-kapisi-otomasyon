using System.Text.Json;
namespace AnkaStudio;

public sealed partial class Services
{
    void RequireVoiceAccount(){if(store.Settings.FreeOnly)throw new InvalidOperationException("Bu işlem ElevenLabs hesabını kullanır. Önce ücretli bağlantı kullanımını ayarlardan etkinleştir.");if(store.Key("ELEVENLABS_API_KEY")=="")throw new InvalidOperationException("ElevenLabs API anahtarı eksik.");}
    public async Task<(string Id,bool Verify)> CloneVoice(string name,IReadOnlyList<string> files,bool consent,CancellationToken ct)
    {
        if(!consent)throw new InvalidOperationException("Sesin sana ait olduğunu veya kullanma iznin olduğunu doğrula.");
        RequireVoiceAccount();if(string.IsNullOrWhiteSpace(name)||name.Length>100||files.Count is <1 or >10)throw new InvalidOperationException("Ses adı ve 1–10 örnek dosya gerekli.");
        using var form=new MultipartFormDataContent();form.Add(new StringContent(name),"name");
        foreach(string file in files){if(!File.Exists(file)||new FileInfo(file).Length>100_000_000)throw new InvalidOperationException("Ses örneği bulunamadı veya 100 MB sınırını aşıyor.");form.Add(new StreamContent(File.OpenRead(file)),"files",Path.GetFileName(file));}
        using var request=new HttpRequestMessage(HttpMethod.Post,"https://api.elevenlabs.io/v1/voices/add"){Content=form};request.Headers.Add("xi-api-key",store.Key("ELEVENLABS_API_KEY"));
        using var response=await http.SendAsync(request,ct);if(!response.IsSuccessStatusCode)throw new ProviderException("ElevenLabs ses klonlama",(int)response.StatusCode);
        using var result=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));string id=result.RootElement.GetProperty("voice_id").GetString()??throw new InvalidOperationException("Ses kimliği alınamadı.");return(id,result.RootElement.TryGetProperty("requires_verification",out var verify)&&verify.GetBoolean());
    }
    public async Task<List<Caption>> TranscribeEleven(string file,CancellationToken ct)
    {
        RequireVoiceAccount();if(!File.Exists(file)||new FileInfo(file).Length>200_000_000)throw new InvalidOperationException("Ses dosyası bulunamadı veya 200 MB sınırını aşıyor.");
        using var form=new MultipartFormDataContent();form.Add(new StringContent("scribe_v2"),"model_id");form.Add(new StringContent("tr"),"language_code");form.Add(new StringContent("word"),"timestamps_granularity");form.Add(new StringContent("false"),"tag_audio_events");form.Add(new StreamContent(File.OpenRead(file)),"file",Path.GetFileName(file));
        using var request=new HttpRequestMessage(HttpMethod.Post,"https://api.elevenlabs.io/v1/speech-to-text"){Content=form};request.Headers.Add("xi-api-key",store.Key("ELEVENLABS_API_KEY"));
        using var response=await http.SendAsync(request,ct);if(!response.IsSuccessStatusCode)throw new ProviderException("ElevenLabs konuşmadan metin",(int)response.StatusCode);
        return ParseTranscript(await response.Content.ReadAsStringAsync(ct));
    }
    public static List<Caption> ParseTranscript(string json)
    {
        using var doc=JsonDocument.Parse(json);var result=new List<Caption>();var words=new List<string>();double start=0,end=0;
        foreach(var word in doc.RootElement.GetProperty("words").EnumerateArray()){
            if(word.TryGetProperty("type",out var type)&&type.GetString()!="word")continue;
            string text=word.GetProperty("text").GetString()??"";double from=word.GetProperty("start").GetDouble(),to=word.GetProperty("end").GetDouble();
            if(!double.IsFinite(from)||!double.IsFinite(to)||from<0||to<=from||from<end-.05)throw new InvalidOperationException("Servis geçersiz kelime zamanı döndürdü.");
            if(words.Count==0)start=from;words.Add(text);end=to;
            if(words.Count>=10||end-start>=4||text.EndsWith('.')||text.EndsWith('?')||text.EndsWith('!')){result.Add(new Caption(start,end,string.Join(" ",words)));words.Clear();}
        }
        if(words.Count>0)result.Add(new Caption(start,end,string.Join(" ",words)));
        if(result.Count==0)throw new InvalidOperationException("Konuşma çözümlenemedi; boş altyazı uygulanmadı.");return result;
    }
}

public sealed partial class StudioForm
{
    void VoiceStudio()
    {
        if(engine.Busy){Message("Üretim tamamlandığında Ses Stüdyosunu açabilirsin.");return;}
        using var dialog=Dialog("Ses Stüdyosu • klonlama ve konuşmadan metin",950,790);var flow=Theme.Flow(850);flow.Dock=DockStyle.Fill;flow.AutoScroll=true;dialog.Controls.Add(flow);
        var status=Theme.Label("ElevenLabs hesabı ve kullanım hakkı gerekir. İşlemler seçtiğin dosyaları bu servise gönderir.",10,width:800,height:54);flow.Controls.Add(status);
        var name=Theme.Input("Benim sesim",width:800);flow.Controls.Add(Theme.Label("Klon ses adı",10,true,width:800,height:28));flow.Controls.Add(name);
        var sample=Theme.Label("Ses örneği seçilmedi",10,width:800,height:40);flow.Controls.Add(sample);string[] files=[];
        flow.Controls.Add(Theme.Button("Temiz ses örneklerini seç",()=>{using var pick=new OpenFileDialog{Filter="Ses|*.wav;*.mp3;*.m4a;*.flac",Multiselect=true};if(pick.ShowDialog(dialog)==DialogResult.OK){files=pick.FileNames;sample.Text=string.Join(", ",files.Select(Path.GetFileName));}},false,350));
        var consent=Theme.Check("Bu ses bana ait veya klonlamak için izinliyim",false,800);flow.Controls.Add(consent);
        using var lifetime=new CancellationTokenSource();dialog.FormClosed+=(_,_)=>lifetime.Cancel();CancellationTokenSource? operation=null;bool working=false;var activity=new TurquoisePackageBar{Width=800,Height=28,Visible=false};flow.Controls.Add(activity);
        async Task Run(Func<Task> action){if(working)return;using var current=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);operation=current;working=true;activity.Visible=activity.Busy=true;status.Text="İşlem sürüyor…";try{await action();}catch(OperationCanceledException){status.Text="İşlem durduruldu.";}catch(Exception ex){status.Text="Tamamlanamadı: "+ex.Message;}finally{working=false;operation=null;if(!dialog.IsDisposed)activity.Visible=activity.Busy=false;}}
        flow.Controls.Add(Theme.Button("Klon sesi oluştur • IVC",()=>_=Run(async()=>{var result=await new Services(store,_=>{}).CloneVoice(name.Text,files,consent.Checked,operation!.Token);if(result.Verify){status.Text="Ses oluşturuldu; ElevenLabs hesabında doğrulama gerekli. Doğrulamadan anlatıcı olarak seçilmedi.";return;}store.Settings.VoiceProvider="ElevenLabs";store.Settings.VoiceId=result.Id;store.Save();status.Text="Klon ses oluşturuldu ve genel anlatıcı olarak seçildi. Ön dinleme ile kalitesini kontrol et.";}),true,350));
        flow.Controls.Add(Theme.Button("Profesyonel klon • eğitim ve doğrulama",()=>Theme.Open("https://elevenlabs.io/app/voice-lab"),false,450));
        flow.Controls.Add(Theme.Label("Profesyonel klonun eğitim ve sahiplik doğrulaması ElevenLabs hesabında tamamlanır; ardından Sesleri getir ve seç ile TubeFlow'a alınır.",10,width:800,height:55));
        var preview=new ArchiveMusicPlayer(compact:true){Width=800};flow.Controls.Add(preview);flow.Controls.Add(Theme.Button("Seçili anlatıcıyı ön dinle",()=>_=Run(async()=>{string path=await SpeechSample.Create(store,"Merhaba. Bu, seçtiğim anlatıcının kalite ve telaffuz denemesidir.",store.Settings,operation!.Token);preview.LoadAndPlay(path);status.Text="Ses örneği hazır.";}),false,350));
        flow.Controls.Add(Theme.Label("Konuşmadan zamanlı metin ve altyazı",14,true,width:800,height:38));
        var transcript=Theme.Input("",true,800);transcript.Height=110;flow.Controls.Add(transcript);List<Caption> captions=[];
        flow.Controls.Add(Theme.Button("Ses dosyasını çözümle • ElevenLabs",()=>{using var pick=new OpenFileDialog{Filter="Ses / video|*.wav;*.mp3;*.m4a;*.flac;*.mp4;*.webm"};if(pick.ShowDialog(dialog)!=DialogResult.OK)return;string file=pick.FileName;_=Run(async()=>{captions=await new Services(store,_=>{}).TranscribeEleven(file,operation!.Token);transcript.Text=string.Join(" ",captions.Select(x=>x.Text));status.Text=captions.Count+" zamanlı altyazı satırı hazır.";});},true,450));
        flow.Controls.Add(Theme.Button("SRT altyazısını kaydet",()=>{if(captions.Count==0)return;using var save=new SaveFileDialog{Filter="SRT altyazı|*.srt",FileName="altyazi.srt"};if(save.ShowDialog(dialog)==DialogResult.OK){string Clock(double value)=>TimeSpan.FromSeconds(value).ToString(@"hh\:mm\:ss\,fff");File.WriteAllText(save.FileName,string.Join("\n\n",captions.Select((c,i)=>$"{i+1}\n{Clock(c.Start)} --> {Clock(c.End)}\n{c.Text}")));}},false,350));
        flow.Controls.Add(Theme.Button("İşlemi durdur",()=>operation?.Cancel(),false,180));dialog.ShowDialog(this);
    }
}

