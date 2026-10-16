using System.Security.Cryptography;

using System.Text;

using System.Text.Json;

namespace AnkaStudio;



public static class TimedNarration

{

    public sealed record Cue(int Scene, double Start, double End, string Text, double AudioEnd);

    public sealed class TimingUnavailableException(string message) : InvalidOperationException(message);

    public static string TextHash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    public static List<Caption> MarkedCaptions(IReadOnlyList<string> text, IReadOnlyDictionary<string, double> marks, double duration, double speed)

    {

        var result = new List<Caption>();

        for (int i = 0; i < text.Count; i++) {

            if (!marks.TryGetValue("c" + i, out double start)) throw new TimingUnavailableException("Ses sağlayıcısı bütün altyazı zamanlarını döndürmedi.");

            double end = i + 1 < text.Count ? marks.GetValueOrDefault("c" + (i + 1), -1) : marks.GetValueOrDefault("end", duration * speed);

            start /= speed; end /= speed;

            if (!double.IsFinite(start) || !double.IsFinite(end) || start < 0 || start >= duration || end <= start || end > duration + .15) throw new TimingUnavailableException("Ses zaman işaretleri sıralı veya ses süresiyle uyumlu değil.");

            result.Add(new(start, Math.Min(end, duration), text[i]));

        }

        return result;

    }

    public static List<Caption> Redisplay(IReadOnlyList<Caption> measured, string text)

    {

        if (measured.Count == 0) return [];

        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries); var weights = measured.Select(c => Math.Max(1, ScriptLength.Count(c.Text))).ToArray(); int used = 0, total = weights.Sum(); var result = new List<Caption>();

        for (int i = 0; i < measured.Count && used < words.Length; i++) {

            int end = i == measured.Count - 1 ? words.Length : Math.Min(words.Length, Math.Max(used + 1, (int)Math.Round(words.Length * weights.Take(i + 1).Sum() / (double)total)));

            result.Add(new(measured[i].Start, measured[i].End, string.Join(" ", words[used..end]))); used = end;

        }

        return result;

    }

    public static async Task Prepare(Store store, Episode episode, VideoScript script, Settings settings, string folder, Services services, CancellationToken ct, Func<string, string, Settings, CancellationToken, Task>? testSpeech = null)

    {

        Directory.CreateDirectory(folder); var hash = NarrationFlow.Fingerprint(script, settings);

        string cache = Path.Combine(Path.GetDirectoryName(folder)!, "audio-cache", hash); Directory.CreateDirectory(cache);

        string audio = Path.Combine(folder, "narration-full.wav"), cached = Path.Combine(cache, "narration-full.wav"), timing = Path.Combine(cache, "timing.json");

        List<Cue>? cues = null;

        if (!File.Exists(cached) && File.Exists(audio) && File.Exists(audio + ".hash") && File.ReadAllText(audio + ".hash") == hash && File.Exists(Path.Combine(folder, "narration-timing.json"))) { cached = audio; timing = Path.Combine(folder, "narration-timing.json"); }

        if (File.Exists(cached) && File.Exists(cached + ".sha") && File.Exists(timing) && Reliability.FileHash(cached) == File.ReadAllText(cached + ".sha")) {

            try {

                if (!File.Exists(timing + ".sha") || Reliability.FileHash(timing) != File.ReadAllText(timing + ".sha")) throw new InvalidOperationException("Ses zaman kaydı değişmiş.");

                var candidate = Json.Read(timing, new List<Cue>()); double duration = await Media.Duration(store, cached, ct);

                if (candidate.Count == 0 || candidate.Any(c => c.Scene < 0 || c.Scene >= script.Scenes.Count || !double.IsFinite(c.Start) || !double.IsFinite(c.End) || c.Start < 0 || c.End <= c.Start || c.End > duration + .15 || !double.IsFinite(c.AudioEnd) || c.AudioEnd < c.End || c.AudioEnd > duration + .15) || Enumerable.Range(0, script.Scenes.Count).Any(i => !candidate.Any(c => c.Scene == i))) throw new InvalidOperationException("Ses zaman önbelleği bozuk.");

                if (!services.InjectedClient && !File.Exists(timing + ".local-word-v1")) throw new InvalidOperationException("Eski tahmini altyazı zamanları kelime zamanlarıyla yenilenecek; ses korunuyor.");

                cues = candidate; if (cached != audio) File.Copy(cached, audio, true);

                if (script.TextLayout?.SubtitleEnabled != false && candidate.Any(c => Engine.SplitText(c.Text,48).Count>1)) { cues=null; episode.Log("Altyazı açıldı; mevcut doğal ses yeniden üretilmeden zamanları eşlenecek."); }

            } catch (Exception ex) when (ex is not OperationCanceledException) { episode.Log("Ses / altyazı önbelleği geçersiz; ölçülen zamanlar yeniden hazırlanıyor."); }

        }

        if (cues == null) {

            async Task<List<Cue>> Create(Settings voice)

            {

                var result = new List<Cue>(); var parts = new List<string>(); double offset = 0;

                async Task Append(string raw, List<Caption> captions, double duration, int scene) {

                    parts.Add(raw); result.AddRange(captions.Select(c => new Cue(scene, offset + c.Start, offset + c.End, c.Text, offset + duration))); offset += duration; await Task.CompletedTask;

                }

                async Task Segment(string text, int scene, int index) {

                    string key = TextHash("natural-block-v2" + text + NarrationFlow.Fingerprint(new VideoScript{Scenes=[new Scene{Narration=text}]}, voice));var blockCache=Path.Combine(Path.GetDirectoryName(cache)!,"blocks");Directory.CreateDirectory(blockCache);string part = Path.Combine(blockCache, key + ".wav");

                    if (!File.Exists(part) || !File.Exists(part + ".sha") || Reliability.FileHash(part) != File.ReadAllText(part + ".sha")) {

                        string raw = part + ".raw.wav"; var spoken = NarrationFlow.SpokenText(text,voice);

                        if (testSpeech != null) await testSpeech(spoken, raw, voice, ct);

                        else if (voice.VoiceProvider.StartsWith("Google")) await new GoogleTts(store, services.Http).Speak(spoken, raw, voice, ct);

                        else if(voice.VoiceProvider.StartsWith("ElevenLabs")){var speech=await services.Speech(spoken,ct,voice);await File.WriteAllBytesAsync(raw,speech.Audio,ct);}
                        else await LocalVoice.SpeakWithFallback(spoken, raw, voice, ct);

                        await Media.Exec(store.Ffmpeg, ["-y", "-i", raw, "-ar", "44100", "-ac", "1", "-c:a", "pcm_s16le", part], folder, ct);

                        File.WriteAllText(part + ".sha", Reliability.FileHash(part));

                    }

                    double duration = await Media.Duration(store, part, ct);

                    var pieces = Engine.SplitText(text, 48);

                    var captions = script.TextLayout?.SubtitleEnabled == false || pieces.Count == 1 ? new List<Caption> { new(0, duration, text) } : services.InjectedClient ? await AudioSubtitleAlignment.Align(store, part, pieces, duration, services.Http, episode.Log, ct) : await LocalAudioAlignment.Align(part, pieces, duration, episode.Log, ct);

                    await Append(part, captions, duration, scene);

                }

                for (int scene = 0; scene < script.Scenes.Count; scene++) {

                    ct.ThrowIfCancellationRequested(); var texts = Engine.SplitText(script.Scenes[scene].Narration, 48);

                    if(episode.Creation!=null){int block=0;foreach(var text in NaturalBlocks(script.Scenes[scene].Narration))await Segment(text,scene,block++);continue;}
                    if (voice.VoiceProvider.StartsWith("ElevenLabs") && testSpeech == null) {

                        var speech = await services.Speech(NarrationFlow.SpokenText(script.Scenes[scene].Narration,voice), ct, voice);

                        string raw = Path.Combine(cache, "eleven-" + scene + ".mp3"), part = raw + ".wav"; await File.WriteAllBytesAsync(raw, speech.Audio, ct);

                        await Media.Exec(store.Ffmpeg, ["-y", "-i", raw, "-ar", "44100", "-ac", "1", part], folder, ct);

                        double duration = await Media.Duration(store, part, ct);

                        if (speech.Captions.Count == 0 || speech.Captions.Any(c => c.Start < 0 || c.End <= c.Start || c.End > duration + .15)) throw new InvalidOperationException("ElevenLabs konuşma zamanları geçersiz; yaklaşık altyazı kullanılmadı.");

                        await Append(part, Redisplay(speech.Captions, script.Scenes[scene].Narration), duration, scene); continue;

                    }

                    bool marked = testSpeech == null && voice.VoiceProvider.StartsWith("Google") && !voice.GoogleTtsVoice.Contains("Chirp", StringComparison.OrdinalIgnoreCase);

                    if (!marked) {

                        int block = 0;

                        foreach (var text in NaturalBlocks(script.Scenes[scene].Narration)) await Segment(text, scene, block++);

                        continue;

                    }

                    for (int start = 0; start < texts.Count;) {

                        int count = 1;

                        if (marked) while (start + count < texts.Count && Encoding.UTF8.GetByteCount(GoogleTts.MarkedSsml(texts.Skip(start).Take(count + 1).ToArray())) <= 4300) count++;

                        var batch = texts.Skip(start).Take(count).ToArray();

                        if (marked) {

                            string key = TextHash("marked-v1" + string.Join(" ", batch) + NarrationFlow.Fingerprint(script, voice)), part = Path.Combine(cache, key + ".wav"), marksFile = part + ".json";

                            try {

                                List<Caption> captions;

                                if (File.Exists(part) && File.Exists(part + ".sha") && File.Exists(marksFile) && Reliability.FileHash(part) == File.ReadAllText(part + ".sha")) captions = Json.Read(marksFile, new List<Caption>());

                                else {

                                    var marks = await new GoogleTts(store, services.Http).SpeakMarked(batch, part + ".raw.wav", voice, ct);

                                    await Media.Exec(store.Ffmpeg, ["-y", "-i", part + ".raw.wav", "-ar", "44100", "-ac", "1", "-c:a", "pcm_s16le", part], folder, ct);

                                    double dur = await Media.Duration(store, part, ct); captions = MarkedCaptions(batch, marks, dur, 1); Json.Write(marksFile, captions); File.WriteAllText(part + ".sha", Reliability.FileHash(part));

                                }

                                double duration = await Media.Duration(store, part, ct);

                                if (captions.Count != batch.Length || captions.Where((c, i) => c.Text != batch[i] || c.Start < 0 || c.End <= c.Start || c.End > duration + .15).Any()) throw new TimingUnavailableException("Altyazı zaman önbelleği geçersiz.");

                                await Append(part, captions, duration, scene); start += count; continue;

                            } catch (Exception ex) when (ex is TimingUnavailableException || ex is ProviderException { Status: 400 }) {

                                episode.Log("Seçili ses zaman işareti döndürmedi; doğal anlatım korunarak altyazı sese eşlenecek.");

                                var remaining = string.Join(" ", texts.Skip(start)); int block = start;

                                foreach (var text in NaturalBlocks(remaining)) await Segment(text, scene, block++);

                                break;

                            }

                        }

                    }

                }

                string list = Path.Combine(folder, "timed-narration-list.txt");

                await File.WriteAllTextAsync(list, string.Join("\n", parts.Select(p => "file '" + p.Replace('\\', '/') + "'")), ct);

                await Media.Exec(store.Ffmpeg, ["-y", "-f", "concat", "-safe", "0", "-i", list, "-c:a", "pcm_s16le", audio], folder, ct); return result;

            }

            bool localFallback = false;

            try { cues = await Create(settings); }

            catch (Exception ex) when (ex is not OperationCanceledException && ex is not TimingUnavailableException && settings.VoiceProvider.StartsWith("Google") && settings.GoogleTtsFallback) {

                episode.Log("Google TTS hatası; sesin tamamı aynı yerel sağlayıcıyla ve ölçülen altyazı zamanlarıyla yeniden hazırlanıyor: " + ex.Message);

                var local = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(settings))!; local.VoiceProvider = LocalVoice.List().Count > 0 ? "Windows / ücretsiz" : "Piper / ücretsiz"; cues = await Create(local); localFallback = true;

                File.WriteAllText(audio + ".actual-provider", local.VoiceProvider);

            }

            if (!localFallback) { File.Copy(audio, cached, true); File.WriteAllText(cached + ".sha", Reliability.FileHash(cached)); Json.Write(timing, cues); File.WriteAllText(timing + ".sha", Reliability.FileHash(timing)); if (!services.InjectedClient) File.WriteAllText(timing + ".local-word-v1","1"); }

        }

        File.WriteAllText(audio + ".hash", hash); File.WriteAllText(audio + ".sha", Reliability.FileHash(audio)); Json.Write(Path.Combine(folder, "narration-timing.json"), cues); File.WriteAllText(Path.Combine(folder, "narration-timing.json.sha"), Reliability.FileHash(Path.Combine(folder, "narration-timing.json")));

        double fullDuration = await Media.Duration(store, audio, ct), sceneStart = 0;

        for (int i = 0; i < script.Scenes.Count; i++) {

            var scene = script.Scenes[i]; double end = i == script.Scenes.Count - 1 ? fullDuration : cues.Where(c => c.Scene == i).Max(c => c.AudioEnd);

            if (end <= sceneStart) throw new InvalidOperationException("Ölçülen sahne ses süresi geçersiz.");

            string path = Path.Combine(folder, $"speech-{i + 1}.wav"); await Media.Exec(store.Ffmpeg, ["-y", "-ss", Media.Num(sceneStart), "-i", audio, "-t", Media.Num(end - sceneStart), "-ar", "44100", "-ac", "1", path], folder, ct);

            scene.Duration = await Media.Duration(store, path, ct);

            var captions = cues.Where(c => c.Scene == i).Select(c => new Caption(Math.Max(0, c.Start - sceneStart), Math.Min(scene.Duration, c.End - sceneStart), c.Text)).ToList();

            scene.SpeechCaptions = captions; scene.SpeechTextHash = TextHash(scene.Narration); Json.Write(Path.Combine(folder, $"speech-{i + 1}.json"), captions); sceneStart = end;

        }

    }

    public static List<string> NaturalBlocks(string text, int maxBytes = 4300)

    {

        var blocks = new List<string>(); var current = new StringBuilder();

        // Provider limits can require blocks, but never split at subtitle-line boundaries.

        foreach (var sentence in System.Text.RegularExpressions.Regex.Split(text.Trim(), @"(?<=[.!?])\s+")) {

            if (Encoding.UTF8.GetByteCount(sentence) > maxBytes) {

                if (current.Length > 0) { blocks.Add(current.ToString()); current.Clear(); }

                blocks.AddRange(NarrationFlow.Blocks(sentence, maxBytes)); continue;

            }

            if (current.Length > 0 && Encoding.UTF8.GetByteCount(current + " " + sentence) > maxBytes) { blocks.Add(current.ToString()); current.Clear(); }

            if (current.Length > 0) current.Append(' '); current.Append(sentence);

        }

        if (current.Length > 0) blocks.Add(current.ToString()); return blocks;

    }

}

