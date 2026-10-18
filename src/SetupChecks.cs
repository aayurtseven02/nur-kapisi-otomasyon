using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
namespace AnkaStudio;

public sealed record SetupCheck(string Id,string Name,string Logo,string Url,string Purpose,string Action="connections");
public sealed record SetupResult(bool? Good,string Status,string Detail);

public static class SetupChecks
{
    public static readonly SetupCheck[] Programs=[
        new("ffmpeg","FFmpeg","ffmpeg","https://ffmpeg.org/download.html","Video birleştirme, ses işleme ve görsel süreleri için gerekli. Güncel TubeFlow paketinin tools klasöründe gelir."),
        new("ollama","Ollama","ollama","https://ollama.com/download/windows","Ücretsiz yerel metin ve görsel modellerini çalıştırır. Kurulu olması ve servisinin açık olması ayrı ayrı kontrol edilir.","local"),
        new("text","Seçili yerel metin modeli","qwen","https://ollama.com/library/qwen3","Metni bölümleme, sohbet ve analiz edilmiş görüntüleri anlatımla eşleştirme için kullanılır. Bulut alternatifi varsa isteğe bağlıdır.","local"),
        new("vision","Yerel görsel okuma modeli","qwen","https://ollama.com/library/qwen3-vl:4b-instruct","Fotoğraf ve video örnek karelerini okuyarak konuya göre eşleştirme sağlar. qwen3:8b tek başına görsel okuyamaz.","local"),
        new("comfy","ComfyUI / yerel görsel motoru","comfy","https://github.com/Comfy-Org/ComfyUI","Yerelde yeni kapak ve sahne resmi üretmek için gerekir. Hazır dosyaları okumak için zorunlu değildir. Motor, iş akışı ve model dosyaları ayrı kontrol edilir.","visual"),
        new("workflow","Görsel iş akışı ve model dosyaları","comfy","https://docs.comfy.org/installation/system_requirements","ComfyUI tek başına yeterli değildir; iş akışındaki modeller de kurulmalı. Motor açıksa düğüm ve model seçenekleri ayrıca denetlenir.","visual"),
        new("alignment","Python / Faster Whisper ses eşleme paketi","python","https://github.com/SYSTRAN/faster-whisper","Hazır ses dosyalarını kelimelerle ve altyazıyla eşler. TubeFlow paketindeki Python, kütüphane ve model gerekir; yalnız genel Python kurulması yeterli değildir."),
        new("voices","Windows Türkçe sesleri","windows","https://support.microsoft.com/windows","Windows ile ücretsiz Türkçe seslendirme için gerekir. Google TTS veya başka ses sağlayıcısı kullanıyorsan isteğe bağlıdır."),
        new("runtime","TubeFlow çalışma ortamı (.NET)","tubeflow","https://dotnet.microsoft.com/en-us/download/dotnet/10.0","Resmî Windows paketi çalışma ortamını içerir. Kaynaktan çalıştırırken Windows Desktop Runtime gerekir; tam paket kullanıyorsan ayrıca SDK kurmana gerek yok.")
    ];
    public static readonly SetupCheck[] Accounts=[
        new("GEMINI_API_KEY","Gemini","google","https://aistudio.google.com/apikey","Bulut metin ve desteklenen görsel işlemleri. Abonelik, API kotası ve ücret izni ayrı koşullardır."),
        new("NVIDIA_API_KEY","NVIDIA","nvidia","https://build.nvidia.com/","Alternatif bulut metin/görsel modelleri. Katalog erişimi, seçili modelin çalıştığını tek başına kanıtlamaz."),
        new("GROQ_API_KEY","Groq","groq","https://console.groq.com/keys","Alternatif metin üretimi."),
        new("OPENROUTER_API_KEY","OpenRouter","openrouter","https://openrouter.ai/settings/keys","Ücretsiz veya izinli ücretli model alternatifleri."),
        new("OPENAI_API_KEY","OpenAI","openai","https://platform.openai.com/api-keys","İsteğe bağlı metin/görsel sağlayıcısı; ücretli kullanım ayrı izin gerektirir."),
        new("KIMI_API_KEY","Kimi / Moonshot","kimi","https://platform.kimi.ai","Alternatif metin üretimi."),
        new("TAVILY_API_KEY","Tavily","tavily","https://app.tavily.com","Web araştırması. Kontrolde arama yerine hesap kullanım bilgisi okunur."),
        new("PEXELS_API_KEY","Pexels","pexels","https://www.pexels.com/api/","Stok video arama. Kontrol küçük bir arama kotası kullanabilir; dosya indirmez."),
        new("PIXABAY_API_KEY","Pixabay","pixabay","https://pixabay.com/api/docs/","Alternatif stok video arama. Kontrol küçük bir arama kotası kullanabilir; dosya indirmez."),
        new("VECTEEZY_SECRET_KEY","Vecteezy • hesap kimliği + gizli anahtar","vecteezy","https://www.vecteezy.com/account/developer-tools","Stok video kaynağı. İki bilgi birlikte kontrol edilir; hesap kotası okunur, video indirilmez."),
        new("GOOGLE_TTS_API_KEY","Google Cloud TTS","google","https://console.cloud.google.com/apis/credentials","Seçtiğin Google sesleriyle anlatım. Ses listesi okunur, ses üretilmez."),
        new("ELEVENLABS_API_KEY","ElevenLabs","elevenlabs","https://elevenlabs.io/app/settings/api-keys","İsteğe bağlı seslendirme. Kontrol yalnız ses listesini okur."),
        new("GITHUB_TOKEN","GitHub","github","https://github.com/settings/tokens","Kaynak deposu işlemleri. Herkese açık TubeFlow güncellemelerini indirmek için kişisel token zorunlu değildir."),
        new("youtube","Google OAuth / YouTube","google","https://console.cloud.google.com/apis/credentials","Google istemci kimliği, gizli anahtar ve YouTube oturum izni birlikte gerekir. Kontrol kanal kimliğini okur; video yüklemez."),
        new("GOOGLE_BILLING_JSON","Google kullanım raporu kimliği","google","https://console.cloud.google.com/iam-admin/serviceaccounts","İsteğe bağlı Cloud kullanım raporu hizmet hesabı. Dosya yapısı ile gerçek rapor yetkisi farklı kontrollerdir."),
        new("GOOGLE_CONTROL_JSON","Google API yönetim kimliği","google","https://console.cloud.google.com/iam-admin/serviceaccounts","İsteğe bağlı API yönetimi hizmet hesabı. Bu tarama API açıp kapatmaz veya bulut ayarlarını değiştirmez.")
    ];
    public static string? Resolve(string configured,string name)
    {
        if(File.Exists(configured))return Path.GetFullPath(configured);
        foreach(var root in new[]{Path.Combine(AppContext.BaseDirectory,"tools")}.Concat((Environment.GetEnvironmentVariable("PATH")??"").Split(Path.PathSeparator)))try{string candidate=Path.Combine(root.Trim('"'),name);if(File.Exists(candidate))return Path.GetFullPath(candidate);}catch(ArgumentException){}
        return null;
    }
    static async Task<bool> Run(string path,string[] args,CancellationToken ct)
    {
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(12));
        var info=new ProcessStartInfo(path){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};foreach(var arg in args)info.ArgumentList.Add(arg);
        using var process=Process.Start(info)??throw new InvalidOperationException("Başlatılamadı.");
        try{var output=process.StandardOutput.ReadToEndAsync(timeout.Token);var error=process.StandardError.ReadToEndAsync(timeout.Token);await process.WaitForExitAsync(timeout.Token);await Task.WhenAll(output,error);return process.ExitCode==0;}catch{if(!process.HasExited)try{process.Kill(true);}catch{}throw;}
    }
    public static async Task<SetupResult> Program(Store store,string id,CancellationToken ct,HttpClient? client=null)
    {
        var local=new LocalAi(store,client);
        if(id=="runtime")return new(true,"Yüklü",System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription+" • çalışan uygulama doğrulandı");
        if(id=="ffmpeg"){var exe=Resolve(store.Ffmpeg,"ffmpeg.exe");return exe==null?new(false,"Yüklü değil","FFmpeg bulunamadı; tam paketi çıkar veya ayarlardan dosyasını seç."):await Run(exe,["-version"],ct)?new(true,"Yüklü",exe):new(false,"Kurulu, çalışmıyor","FFmpeg sürüm kontrolü başarısız.");}
        if(id=="voices"){var voices=LocalVoice.List();return new(voices.Count>0,voices.Count>0?"Yüklü":"Yüklü değil",voices.Count+" Türkçe Windows sesi bulundu.");}
        if(id is "ollama" or "text" or "vision"){
            var state=await local.Status(ct);
            if(id=="ollama")return state.Running?new(true,"Yüklü • servis açık",state.Note):local.Executable()!=null?new(null,"Yüklü • servis kapalı",state.Note):new(false,"Yüklü değil",state.Note);
            string model=id=="vision"?store.Settings.OllamaVisionModel:store.Settings.OllamaModel;
            if(!state.Running)return new(null,"Kontrol edilemedi","Ollama servisini başlat, sonra Güncelle’ye bas. Seçili: "+model);
            if(!state.Models.Contains(model,StringComparer.OrdinalIgnoreCase))return new(false,"Yüklü değil","Seçili model bulunamadı: "+model+". Yerel motor ayarlarından indir veya kurulu bir model seç.");
            using var http=client==null?new HttpClient{Timeout=TimeSpan.FromSeconds(8)}:null;using var response=await (client??http!).PostAsJsonAsync("http://127.0.0.1:11434/api/show",new{model},ct);response.EnsureSuccessStatusCode();using var doc=JsonDocument.Parse(await Reliability.ReadText(response,1_000_000,ct));
            bool remote=new[]{"remote_model","remote_host"}.Any(k=>doc.RootElement.TryGetProperty(k,out var value)&&value.ValueKind==JsonValueKind.String&&!string.IsNullOrWhiteSpace(value.GetString()));bool suitable=doc.RootElement.TryGetProperty("capabilities",out var caps)&&caps.EnumerateArray().Any(c=>c.GetString()==(id=="vision"?"vision":"completion"));
            return remote?new(false,"Yerel model değil","Seçili model uzaktan çalışıyor; ücretsiz yerel model seç."):suitable?new(true,"Yüklü",model+" • gereken yetenek doğrulandı; tam üretim testi değildir."):new(false,"Model uygun değil",model+" • gereken "+(id=="vision"?"görsel okuma":"metin üretimi")+" yeteneği yok.");
        }
        string workflow=File.Exists(store.Settings.CoverLocalWorkflow)?store.Settings.CoverLocalWorkflow:LocalCoverEngine.FindInstalledWorkflow();var roots=LocalCoverEngine.StandardRoots().Concat(workflow!=""?[Path.GetDirectoryName(workflow)!]:Array.Empty<string>());string? engine=roots.SelectMany(LocalCoverEngine.FindEngines).FirstOrDefault();
        if(id=="comfy")return new(engine!=null,engine!=null?"Yüklü":"Yüklü değil",engine??"TubeFlow standart motor klasörlerinde bulunamadı. Farklı kurulumun varsa yerel motor ayarlarından bağla.");
        if(id=="workflow"){
            if(workflow=="")return new(false,"Yüklü değil","Yerel görsel iş akışı seçilmedi veya bulunamadı.");
            try{string detail=await LocalCoverEngine.Check(workflow,ct);return new(true,"Yüklü • modeller doğrulandı",detail);}catch(HttpRequestException){return new(null,"İş akışı kayıtlı • motor kapalı","Motoru başlatıp Güncelle’ye bas; model dosyaları henüz doğrulanamadı.");}
        }
        if(id=="alignment"){
            string folder=LocalAudioAlignment.RuntimeFolder,python=Path.Combine(folder,"runtime","python.exe");bool files=File.Exists(python)&&File.Exists(Path.Combine(LocalAudioAlignment.Folder,"align.py"))&&File.Exists(Path.Combine(folder,"model","model.bin"));
            return !files?new(false,"Yüklü değil","Python + Faster Whisper + model içeren tam TubeFlow paketini kullan."):await Run(python,["-c","import faster_whisper,ctranslate2"],ct)?new(true,"Yüklü","Python, ses eşleme kütüphaneleri ve model dosyası bulundu."):new(false,"Eksik / bozuk kurulum","Python açıldı ancak gereken ses eşleme kütüphaneleri yüklenemedi.");
        }
        return new(null,"Kontrol edilemedi","Bileşen tanınmadı.");
    }
    public static bool Present(Store store,string id)=>id=="youtube"?store.Settings.GoogleClientId!=""&&store.Key("GOOGLE_CLIENT_SECRET")!=""&&store.Key("YOUTUBE_REFRESH_TOKEN")!="":store.Key(id)!=""&&(id!="VECTEEZY_SECRET_KEY"||store.Key("VECTEEZY_ACCOUNT_ID")!="");
    public static async Task<SetupResult> Account(Store store,string id,CancellationToken ct,HttpClient? client=null)
    {
        if(!Present(store,id))return new(false,"Anahtar / bilgi eksik",id=="youtube"?"İstemci kimliği: "+(store.Settings.GoogleClientId!=""?"kayıtlı":"eksik")+" • Gizli anahtar: "+(store.Key("GOOGLE_CLIENT_SECRET")!=""?"kayıtlı":"eksik")+" • YouTube izni: "+(store.Key("YOUTUBE_REFRESH_TOKEN")!=""?"kayıtlı":"eksik"):id=="VECTEEZY_SECRET_KEY"?"Hesap kimliği: "+(store.Key("VECTEEZY_ACCOUNT_ID")!=""?"kayıtlı":"eksik")+" • Gizli anahtar: "+(store.Key(id)!=""?"kayıtlı":"eksik"):"Hesaplar ve AI bölümünde bu hizmetin bilgisini kaydet.");
        if(id is "GOOGLE_BILLING_JSON" or "GOOGLE_CONTROL_JSON"){
            using var json=JsonDocument.Parse(store.Key(id));bool shape=new[]{"private_key","client_email","project_id"}.All(k=>json.RootElement.TryGetProperty(k,out var v)&&v.ValueKind==JsonValueKind.String&&!string.IsNullOrWhiteSpace(v.GetString()));return shape?new(null,"Kimlik kayıtlı • yetki doğrulanmadı","Dosya yapısı uygun. Gerçek rapor/API yönetim yetkisini ilgili ayarın Dene işlemiyle kontrol et."):new(false,"Kimlik dosyası geçersiz","Gerekli hizmet hesabı alanları eksik.");
        }
        using var owned=client==null?new HttpClient{Timeout=TimeSpan.FromSeconds(18)}:null;var http=client??owned!;string value=store.Key(id);
        if(id=="youtube"){await new YouTube(store,http).ChannelIdentity(ct);return new(true,"Bağlantı doğrulandı","OAuth oturumu ve tek hedef YouTube kanalı doğrulandı; video yüklenmedi.");}
        if(id=="VECTEEZY_SECRET_KEY"){var quota=await new Vecteezy(store,http).AccountInfo(ct);return new(true,"Bağlantı doğrulandı",$"Arama {quota.GeneralUsed}/{quota.GeneralLimit} • indirme {quota.DownloadUsed}/{quota.DownloadLimit}");}
        string url=id switch{
            "GEMINI_API_KEY"=>"https://generativelanguage.googleapis.com/v1beta/models",
            "NVIDIA_API_KEY"=>"https://integrate.api.nvidia.com/v1/models",
            "GROQ_API_KEY"=>"https://api.groq.com/openai/v1/models",
            "OPENROUTER_API_KEY"=>"https://openrouter.ai/api/v1/auth/key",
            "OPENAI_API_KEY"=>"https://api.openai.com/v1/models",
            "KIMI_API_KEY"=>"https://api.moonshot.ai/v1/models",
            "TAVILY_API_KEY"=>"https://api.tavily.com/usage",
            "GOOGLE_TTS_API_KEY"=>"https://texttospeech.googleapis.com/v1/voices?languageCode=tr-TR",
            "ELEVENLABS_API_KEY"=>"https://api.elevenlabs.io/v1/voices",
            "GITHUB_TOKEN"=>"https://api.github.com/user",
            "PEXELS_API_KEY"=>"https://api.pexels.com/v1/videos/search?query=nature&per_page=1",
            "PIXABAY_API_KEY"=>"https://pixabay.com/api/videos/?per_page=3&q=nature&key="+Uri.EscapeDataString(value),_=>""};
        if(url=="")return new(null,"Anahtar kayıtlı • doğrulanmadı","Bu bağlantıyı ilgili ayarlar bölümünden dene.");
        using var request=new HttpRequestMessage(HttpMethod.Get,url);request.Headers.UserAgent.ParseAdd("TubeFlow-Setup/0.4.25");
        if(id is "GEMINI_API_KEY" or "GOOGLE_TTS_API_KEY")request.Headers.Add("x-goog-api-key",value);else if(id=="ELEVENLABS_API_KEY")request.Headers.Add("xi-api-key",value);else if(id=="PEXELS_API_KEY")request.Headers.Add("Authorization",value);else if(id!="PIXABAY_API_KEY")request.Headers.Authorization=new("Bearer",value);
        using var response=await http.SendAsync(request,ct);if(!response.IsSuccessStatusCode)return HttpFailure((int)response.StatusCode);
        using var doc=JsonDocument.Parse(await Reliability.ReadText(response,4_000_000,ct));if(doc.RootElement.ValueKind!=JsonValueKind.Object||doc.RootElement.TryGetProperty("error",out _))return new(false,"Yanıt doğrulanamadı","Hizmet beklenen hesap/katalog yanıtını vermedi.");
        if(id is "NVIDIA_API_KEY" or "KIMI_API_KEY")return new(null,"Katalog erişiliyor • model denenmedi","Anahtar kayıtlı. Katalog yanıtı anahtar/model üretim iznini kanıtlamaz; Hesaplar ve AI bölümünde Dene’yi kullan.");
        string field=id switch{"GITHUB_TOKEN"=>"login","GEMINI_API_KEY"=>"models","GOOGLE_TTS_API_KEY" or "ELEVENLABS_API_KEY"=>"voices","PEXELS_API_KEY"=>"videos","PIXABAY_API_KEY"=>"hits","TAVILY_API_KEY"=>"key",_=>"data"};
        return doc.RootElement.TryGetProperty(field,out _)?new(true,"Bağlantı doğrulandı","Anahtar yetkili okuma isteğinde kabul edildi. Seçili model/üretim kotası ve ücret izni ayrıca geçerlidir."):new(null,"Anahtar kayıtlı • yanıt belirsiz","Beklenen hesap alanı bulunamadı; ilgili bağlantıyı ayrıca dene.");
    }
    public static SetupResult HttpFailure(int status)=>new(false,status is 401 or 403?"Anahtar / izin hatası":status==429?"Kota / hız sınırı":status==402?"Bakiye gerekli":"Bağlantı hatası","HTTP "+status+" • "+(status is 401 or 403?"Anahtarı ve hizmet erişim iznini kontrol et.":status==429?"Kota veya hız sınırı; anahtarın silindiği anlamına gelmez.":"Hizmet panelini kontrol et; anahtar değeri bu listede gösterilmez."));
}
