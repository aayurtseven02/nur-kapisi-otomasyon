using System.Text.Json;
namespace AnkaStudio;

public static class EdgeTts
{
    public const string Provider="Microsoft Edge / anahtarsız";
    public static string Folder=>Path.Combine(AppContext.BaseDirectory,"tools","edge-tts");
    public static readonly (string Id,string Name)[] TurkishVoices=[("tr-TR-AhmetNeural","Ahmet • Türkçe erkek"),("tr-TR-EmelNeural","Emel • Türkçe kadın")];
    public static async Task<string> Python(Settings settings,CancellationToken ct)
    {
        var candidates=new List<string>{settings.EdgeTtsPython,Path.Combine(Folder,"runtime","python.exe")};
        string normal=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","Python");
        if(Directory.Exists(normal))candidates.AddRange(Directory.EnumerateDirectories(normal).Select(x=>Path.Combine(x,"python.exe")));
        string script=Path.Combine(Folder,"bridge.py");if(!File.Exists(script))throw new InvalidOperationException("Edge ses adaptörü eksik; tam TubeFlow paketini kullan.");
        foreach(string python in candidates.Where(File.Exists).Distinct()){
            ct.ThrowIfCancellationRequested();using var budget=CancellationTokenSource.CreateLinkedTokenSource(ct);budget.CancelAfter(TimeSpan.FromSeconds(10));
            try{var result=await Media.Exec(python,[script,"--check"],Folder,budget.Token);if(result.Contains("\"Ready\": true"))return python;}catch(OperationCanceledException)when(!ct.IsCancellationRequested){}catch(InvalidOperationException){}
        }
        throw new InvalidOperationException("Edge ses motoru bulunamadı. Tam paketi güncelle veya Edge Python yolunda edge-tts 7.3.1 kurulu ortamı seç.");
    }
    public static async Task Speak(string text,string path,Settings settings,CancellationToken ct)
    {
        if(!TurkishVoices.Any(x=>x.Id==settings.EdgeTtsVoice))throw new InvalidOperationException("Geçerli Türkçe Edge sesi seç; başka anlatıcıya geçilmedi.");
        string python=await Python(settings,ct),folder=Path.GetDirectoryName(Path.GetFullPath(path))!;Directory.CreateDirectory(folder);
        string request=Path.Combine(folder,Guid.NewGuid().ToString("N")+".edge.json"),audio=request+".mp3";
        try{
            Json.Write(request,new{Text=text,Voice=settings.EdgeTtsVoice,Speed=settings.VoiceSpeed});
            using var budget=CancellationTokenSource.CreateLinkedTokenSource(ct);budget.CancelAfter(TimeSpan.FromMinutes(4));
            try{await Media.Exec(python,[Path.Combine(Folder,"bridge.py"),request,audio],folder,budget.Token);}
            catch(InvalidOperationException error) when(error.Message.Contains("NoAudioReceived")||error.Message.Contains("No speech audio received")){
                throw new InvalidOperationException($"Microsoft Edge seçilen {settings.EdgeTtsVoice} sesi için ses verisi döndürmedi. Ses önizlemesini yeniden dene veya ses ayarlarından başka bir sesi kendin seç. Anlatıcı değiştirilmedi.",error);
            }
            catch(OperationCanceledException) when(!ct.IsCancellationRequested){throw new InvalidOperationException("Microsoft Edge ses isteği zaman aşımına uğradı. İnternet bağlantısını kontrol edip yeniden dene; anlatıcı değiştirilmedi.");}
            var ffmpeg=string.IsNullOrWhiteSpace(settings.FfmpegPath)?Path.Combine(AppContext.BaseDirectory,"tools","ffmpeg.exe"):settings.FfmpegPath;
            await Media.Exec(ffmpeg,["-y","-i",audio,"-ar","44100","-ac","1","-c:a","pcm_s16le",path],folder,ct);
        }finally{foreach(string file in new[]{request,audio,audio+".words.json",audio+".partial"})if(File.Exists(file))File.Delete(file);}
    }
}
