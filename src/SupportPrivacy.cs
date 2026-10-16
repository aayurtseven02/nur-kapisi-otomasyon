using System.Text.RegularExpressions;

namespace AnkaStudio;



public static class SupportPrivacy

{

    static readonly string[] Keys=["NVIDIA_API_KEY","GEMINI_API_KEY","GROQ_API_KEY","OPENROUTER_API_KEY","OPENAI_API_KEY","KIMI_API_KEY","ELEVENLABS_API_KEY","PEXELS_API_KEY","PIXABAY_API_KEY","YOUTUBE_REFRESH_TOKEN","GOOGLE_CLIENT_SECRET","GOOGLE_TTS_API_KEY","GOOGLE_SERVICE_ACCOUNT_JSON","GITHUB_TOKEN","VECTEEZY_SECRET_KEY"];

    public static string Clean(Store store,string text)

    {

        foreach(var name in Keys){var value=store.Key(name);if(value.Length>=6)text=text.Replace(value,"[gizli bilgi kaldırıldı]",StringComparison.Ordinal);}

        text=Regex.Replace(text,@"(?i)(bearer\s+|(?:api[_-]?key|refresh_token|access_token|password)\s*[:=]\s*)[^\s,;\""}]+","$1[gizli bilgi kaldırıldı]");

        text=Regex.Replace(text,@"(?i)([?&](?:key|token|access_token)=)[^&\s]+","$1[gizli bilgi kaldırıldı]");

        return text;

    }

}

