namespace AnkaStudio;

public static class VoiceProviders
{
    public static readonly string[] Names=["Windows / ücretsiz","Piper / ücretsiz","XTTS v2 / harici",EdgeTts.Provider,"Google Cloud TTS","ElevenLabs"];
    public static string[] Choices(string current)=>Names.Concat(Names.Contains(current)||current==""?[]:new[]{current}).ToArray();
    public static string Id(Settings s)=>s.VoiceProvider==EdgeTts.Provider?s.EdgeTtsVoice:s.VoiceProvider.StartsWith("Google")?s.GoogleTtsVoice:s.VoiceProvider.StartsWith("ElevenLabs")?s.VoiceId:s.WindowsVoice;
    public static void Select(Settings s,string id){if(s.VoiceProvider==EdgeTts.Provider)s.EdgeTtsVoice=id;else if(s.VoiceProvider.StartsWith("Google"))s.GoogleTtsVoice=id;else if(s.VoiceProvider.StartsWith("ElevenLabs"))s.VoiceId=id;else s.WindowsVoice=id;}
}
