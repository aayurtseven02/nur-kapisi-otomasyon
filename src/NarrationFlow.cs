using System.Text;

using System.Text.Json;

using System.Security.Cryptography;



namespace AnkaStudio;



public static class NarrationFlow

{
    public static bool UsesRequestedVoice(string audio,Settings settings)
    {
        string marker=audio+".actual-provider";if(!File.Exists(marker))return true;
        try{return new FileInfo(marker).Length<=512&&File.ReadAllText(marker).Trim()==settings.VoiceProvider;}catch(IOException){return false;}
    }

    public static string SpokenText(string text)=>SpokenText(text,null);

    public static string SpokenText(string text, Settings? settings)

    {

        if(settings!=null)foreach(var line in settings.PronunciationDictionary.Split('\n',StringSplitOptions.RemoveEmptyEntries)) {

            var pair=line.Split('=',2,StringSplitOptions.TrimEntries);if(pair.Length!=2 || pair[0]=="" || pair[1]=="")continue;

            text=System.Text.RegularExpressions.Regex.Replace(text,@"(?<![\p{L}\p{N}])"+System.Text.RegularExpressions.Regex.Escape(pair[0])+@"(?![\p{L}\p{N}])",_=>pair[1],System.Text.RegularExpressions.RegexOptions.IgnoreCase|System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        }

        text = System.Text.RegularExpressions.Regex.Replace(text, @"(?<![\p{L}\p{N}])(?:\(\s*)?s\s*\.\s*a\s*\.\s*v\.?(?:\s*\))?(?![\p{L}\p{N}])", "sallallahu aleyhi vessellem", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        text = System.Text.RegularExpressions.Regex.Replace(text, @"(?<![\p{L}\p{N}])(?:\(\s*)?r\s*\.\s*a\.?(?:\s*\))?(?![\p{L}\p{N}])", "radıyallahu anh", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        text = System.Text.RegularExpressions.Regex.Replace(text, @"(\p{Lu}[\p{L}’'-]*)[ \t]+(?:\(\s*)?(?i:ra)(?:\s*\))?(?![\p{L}\p{N}])", "$1 radıyallahu anh");

        text = System.Text.RegularExpressions.Regex.Replace(text, @"\b(Allah)(?:[ \t]+|(?=\())(?:\(\s*)?c\s*\.?\s*c\.?(?:\s*\))?(?![\p{L}\p{N}])", "$1 celle celaluhu", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        text = System.Text.RegularExpressions.Regex.Replace(text, @"(?<![\p{L}\p{N}])hz(?:\.\s*|\s+)(?=\p{L})", "Hazreti ", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        text=System.Text.RegularExpressions.Regex.Replace(text,@"(?<![\p{L}\p{N}])[Ââ]min(?![\p{L}\p{N}])","Aamin",System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        return System.Text.RegularExpressions.Regex.Replace(text, @"(?<=[\p{L}\p{N}])['’‘ʼ´`＇](?=[\p{L}\p{N}])", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    }

    public static string Fingerprint(VideoScript script, Settings settings) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("natural-narration-v8-pronunciation" + string.Join("\n\n", script.Scenes.Select(s => s.Narration)) + JsonSerializer.Serialize(new { settings.VoiceProvider, settings.GoogleTtsVoice, settings.VoiceId, settings.VoiceModel, VoiceSpeed = settings.VoiceProvider.StartsWith("Google") ? 1 : settings.VoiceSpeed, settings.WindowsVoice, settings.PiperPath, settings.PiperModel, settings.XttsUrl, settings.XttsSpeaker, piperExeHash = Reliability.FileHash(settings.PiperPath), piperModelHash = Reliability.FileHash(settings.PiperModel) }) + settings.PronunciationDictionary)));

    public static async Task Reuse(Store store, Episode episode, VideoScript script, Settings settings, string folder, string key, CancellationToken ct)

    {

        if (!episode.TextDesignPending || File.Exists(Path.Combine(folder, "narration-full.wav"))) return;

        var previousAsset=episode.Videos.FirstOrDefault(v=>v.Key==key);

        var previous = previousAsset?.File;

        if (previous == null) return;

        var oldFolder = Path.GetDirectoryName(previous)!; var audio = previousAsset?.NarrationFile is string saved && saved!="" ? saved : Path.Combine(oldFolder, "narration-full.wav"); var marker = audio + ".hash";

        if (!File.Exists(audio) || !UsesRequestedVoice(audio,settings) || !File.Exists(marker) || await File.ReadAllTextAsync(marker, ct) != Fingerprint(script, settings)) return;

        try { _ = await Media.Duration(store, audio, ct); if (File.Exists(audio + ".sha") && Reliability.FileHash(audio) != File.ReadAllText(audio + ".sha")) return; }

        catch (Exception ex) when (ex is not OperationCanceledException) { episode.Log("Önceki ses önbelleği geçersiz; yeniden hazırlanacak."); return; }

        await using (var input = File.OpenRead(audio)) await using (var output = File.Create(Path.Combine(folder, "narration-full.wav"))) await input.CopyToAsync(output, ct);

        await File.WriteAllTextAsync(Path.Combine(folder, "narration-full.wav.hash"), Fingerprint(script, settings), ct);

        var oldTiming = previousAsset?.NarrationTimingFile is string timingFile && timingFile!="" ? timingFile : Path.Combine(oldFolder, "narration-timing.json"); if (File.Exists(oldTiming) && File.Exists(oldTiming + ".sha")) { File.Copy(oldTiming, Path.Combine(folder, "narration-timing.json"), true); File.Copy(oldTiming + ".sha", Path.Combine(folder, "narration-timing.json.sha"), true); }

        File.WriteAllText(Path.Combine(folder, "narration-full.wav.sha"), Reliability.FileHash(Path.Combine(folder, "narration-full.wav")));

        episode.Log("Yazı düzeni için mevcut anlatım sesi yeniden kullanıldı; yeni TTS isteği gönderilmedi.");

    }

    public static List<string> Blocks(string text, int maxBytes = 4400)

    {

        var blocks = new List<string>(); var current = new StringBuilder();

        foreach (var sentence in System.Text.RegularExpressions.Regex.Split(text.Trim(), @"(?<=[.!?])\s+")) {

            foreach (var word in sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries)) {

                if (Encoding.UTF8.GetByteCount(word) > maxBytes) throw new InvalidOperationException("Ses metninde aşırı uzun bir sözcük var.");

                if (Encoding.UTF8.GetByteCount(current + " " + word) > maxBytes && current.Length > 0) { blocks.Add(current.ToString()); current.Clear(); }

                if (current.Length > 0) current.Append(' '); current.Append(word);

            }

        }

        if (current.Length > 0) blocks.Add(current.ToString()); return blocks;

    }

    public static List<Caption> Align(string text, double duration)

    {

        var pieces = Engine.SplitText(text, 48); var weights = pieces.Select(x => Math.Max(1, x.Length)).ToList(); double total = weights.Sum(), time = 0; var result = new List<Caption>();

        for (int i = 0; i < pieces.Count; i++) { var end = i == pieces.Count - 1 ? duration : time + duration * weights[i] / total; result.Add(new(time, end, pieces[i])); time = end; } return result;

    }

    public static Task Prepare(Store store, Episode episode, VideoScript script, Settings settings, string folder, Services services, CancellationToken ct, Func<string, string, Settings, CancellationToken, Task>? testSpeech = null) => TimedNarration.Prepare(store, episode, script, settings, folder, services, ct, testSpeech);

}



