namespace AnkaStudio;
public static class SpeechSample
{
    public static async Task<string> Create(Store store,string text,Settings settings,CancellationToken ct)
    {
        using var lease=new OperationLease(store);text=text.Trim();if(text=="")throw new InvalidOperationException("Önce bir metin yaz.");
        text=NarrationFlow.SpokenText(text[..Math.Min(300,text.Length)],settings);
        var folder=Path.Combine(store.Root,"speech-samples");Directory.CreateDirectory(folder);var path=Path.Combine(folder,Guid.NewGuid().ToString("N")+".wav");
        if(settings.VoiceProvider.StartsWith("Google"))await new GoogleTts(store).Speak(text,path,settings,ct);
        else if(settings.VoiceProvider.StartsWith("ElevenLabs")){var sample=await new Services(store,_=>{}).Speech(text,ct,settings);await File.WriteAllBytesAsync(path,sample.Audio,ct);}
        else await LocalVoice.SpeakWithFallback(text,path,settings,ct);
        return path;
    }
}
