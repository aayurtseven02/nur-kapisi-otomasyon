using System.Diagnostics;

using System.Text.Json;

namespace AnkaStudio;



public static class LocalAudioAlignment

{

    public static string Folder => Path.Combine(AppContext.BaseDirectory,"tools","audio-alignment");

    public static async Task<List<Caption>> Align(string audio, IReadOnlyList<string> texts, double duration, Action<string> log, CancellationToken ct)

    {

        string python=Path.Combine(Folder,"runtime","python.exe"), script=Path.Combine(Folder,"align.py");

        if (!File.Exists(python) || !File.Exists(script) || !File.Exists(Path.Combine(Folder,"model","model.bin"))) throw new TimedNarration.TimingUnavailableException("Yerel ses eşleme motoru eksik. Güncel ZIP'in tools klasörüyle birlikte çıkarılması gerekiyor.");

        string cache=Path.Combine(Path.GetDirectoryName(audio)!,"align-"+TimedNarration.TextHash(Reliability.FileHash(audio)+string.Join('\n',texts))+".json");

        if(File.Exists(cache)) { try {return AudioSubtitleAlignment.Parse(File.ReadAllText(cache),texts,duration);} catch(Exception ex) when(ex is not OperationCanceledException) {} }

        string request=cache+".request.json";

        await File.WriteAllTextAsync(request,JsonSerializer.Serialize(new {Audio=audio,Texts=texts.Select(NarrationFlow.SpokenText).ToArray(),Duration=duration}),ct);

        log("Altyazı özgün sesin kelimelerinden yerelde eşleniyor; ses değiştirilmiyor, dış hizmete gönderilmiyor.");

        var start=new ProcessStartInfo(python){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=System.Text.Encoding.UTF8,StandardErrorEncoding=System.Text.Encoding.UTF8};

        start.ArgumentList.Add(script);start.ArgumentList.Add(request);start.ArgumentList.Add(cache);

        start.Environment["PYTHONIOENCODING"]="utf-8";start.Environment["PYTHONUTF8"]="1";

        start.Environment["HF_HUB_OFFLINE"]="1";start.Environment["HF_HUB_DISABLE_TELEMETRY"]="1";

        using var process=Process.Start(start) ?? throw new InvalidOperationException("Ses eşleme başlatılamadı.");

        var output=process.StandardOutput.ReadToEndAsync(ct);var error=process.StandardError.ReadToEndAsync(ct);

        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromMinutes(15));

        try {await process.WaitForExitAsync(timeout.Token);} catch(OperationCanceledException) {if(!process.HasExited)process.Kill(true); if(ct.IsCancellationRequested)throw; throw new TimedNarration.TimingUnavailableException("Yerel ses eşleme 15 dakika içinde tamamlanamadı. Özgün ses korundu. Motor kontrolünden sonra Videoları üret ile tekrar dene.");}

        string note=await output, failure=await error;

        if(process.ExitCode!=0) throw new TimedNarration.TimingUnavailableException("Yerel ses eşleme tamamlanamadı; özgün ses korundu. " + failure.Split('\n').LastOrDefault(x=>x.Trim()!=""));

        log(note.Trim());return AudioSubtitleAlignment.Parse(await File.ReadAllTextAsync(cache,ct),texts,duration);

    }

}

