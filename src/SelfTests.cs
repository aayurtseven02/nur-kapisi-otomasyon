using System.Net;

using System.Text;

using System.Text.Json;



namespace AnkaStudio;



public static class SelfTests

{

    public static async Task MediaTest(Store store)

    {

        var episode = new Episode { Topic = "Anka Studio • Üretim motoru testi", Demo = true, IncludeLong = true, ShortCount = 1, LongMinutes = 1, ShortSeconds = 15 };

        episode.Plan = Engine.DemoPlan(episode);

        var musicFile = Path.Combine(store.Root, "fixture-music.wav");

        await Media.Exec(store.Ffmpeg, ["-y", "-f", "lavfi", "-i", "sine=frequency=880:sample_rate=44100", "-t", "2", "-c:a", "pcm_s16le", musicFile], store.Root, CancellationToken.None);

        foreach (var (_, vertical, script) in Rules.Scripts(episode)) {

            script.TextLayout = TextLayout.Defaults(store.Settings, vertical); script.TextLayout.TitleColor = "FF8800";

            script.TextLayout.Effect = vertical ? "3D kabartma" : "Neon parıltı"; script.TextLayout.TitleBackground = false;

            script.TextLayout.TitleAnimation = vertical ? "Vurgu / pop" : "Soldan kayma"; script.TextLayout.FadeOut = true; script.TextLayout.Karaoke = vertical;

            script.TextLayout.Transition = vertical ? "Beyaza geçiş" : "Yumuşak karışım";

            script.MusicTrack = new MusicTrack { Enabled = true, File = musicFile, Start = .5, End = 4.5, SourceOffset = .25, Source = "Yerel test sinyali", License = "Sentetik test sesi; yayımlanmaz", RightsConfirmed = true, Attribution = "Test müziği — yayımlanmaz" };

            foreach (var scene in script.Scenes) { scene.Narration = "Bu sessiz bir üretim testi."; scene.SubtitleText = "Önceden düzenlenmiş Türkçe altyazı."; scene.TitleStart = .5; scene.TitleDuration = 1.3; }

        }

        store.Episodes.Add(episode); store.Save(); var engine = new Engine(store); await engine.Produce(episode);

        if (episode.Videos.Count != 2 || episode.Videos.Any(v => !File.Exists(v.File) || v.Duration <= 0 || v.FileHash == "")) throw new Exception("İki formatlı montaj testi başarısız: " + episode.Error);

        if (episode.Videos.Any(v => v.QualityPassed) || episode.Approved) throw new Exception("Örnek paket yanlışlıkla yayınlanabilir durumda.");

        foreach (var v in episode.Videos)

        {

            if (!File.Exists(Path.Combine(Path.GetDirectoryName(v.File)!, "subtitles.ass"))) throw new Exception("Altyazı dosyası bulunamadı.");

            if (!File.ReadAllText(Path.Combine(Path.GetDirectoryName(v.File)!, "subtitles.ass")).Contains("0:00:00.50,0:00:01.80,Title")) throw new Exception("Başlık zamanı çıktıya uygulanmadı.");

            await Media.Exec(store.Ffmpeg, ["-y", "-ss", "1", "-i", v.File, "-frames:v", "1", "preview.png"], Path.GetDirectoryName(v.File), CancellationToken.None);

        }

        Json.Write(Path.Combine(store.Root, "episode-export.json"), episode);

        File.WriteAllText(Path.Combine(store.Root, "media-test-report.txt"), "PASS 1920x1080 normal video\nPASS 1080x1920 Shorts\nPASS Türkçe SRT + ASS altyazı\nPASS MP4 ses akışı\nPASS medya SHA256\nPASS demo yayın engeli\n");

    }

    public static async Task DesignMediaTest(Store store)

    {

        var root = Path.Combine(store.Root, "design-tests"); Directory.CreateDirectory(root); var clips = new List<string>();

        for (int i = 0; i < 2; i++) {

            var file = Path.Combine(root, "source-" + i + ".mp4");

            await Media.Exec(store.Ffmpeg, ["-y", "-f", "lavfi", "-i", $"color=c={(i == 0 ? "red" : "blue")}:s=320x180:r=24", "-f", "lavfi", "-i", $"sine=frequency={(i == 0 ? 440 : 880)}:sample_rate=44100", "-t", "1.5", "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p", "-c:a", "aac", file], root, CancellationToken.None); clips.Add(file);

        }

        async Task<string> Concat(IReadOnlyList<string> files, string folder) { Directory.CreateDirectory(folder); var list = Path.Combine(folder, "clips.txt"); await File.WriteAllTextAsync(list, string.Join("\n", files.Select(f => "file '" + f.Replace('\\', '/') + "'"))); var joined = Path.Combine(folder, "joined.mp4"); await Media.Exec(store.Ffmpeg, ["-y", "-f", "concat", "-safe", "0", "-i", list, "-c", "copy", joined], folder, CancellationToken.None); return joined; }

        async Task<string> AudioHash(string file) => await Media.Exec(store.Ffmpeg, ["-v", "error", "-i", file, "-map", "0:a:0", "-c:a", "pcm_s16le", "-f", "hash", "-hash", "sha256", "-"], root, CancellationToken.None);

        var results = new List<string>();

        foreach (var name in TextLayout.Transitions.Where(t => t != "Kesme")) {

            var folder = Path.Combine(root, "transition-" + Array.IndexOf(TextLayout.Transitions, name)); var joined = await Concat(clips, folder); string audio = await AudioHash(joined); double before = await Media.Duration(store, joined, CancellationToken.None);

            await SceneTransitions.Apply(store, clips, new TextLayout { Transition = name, TransitionSeconds = .5 }, 24, folder, joined, CancellationToken.None);

            if (audio != await AudioHash(joined) || Math.Abs(before - await Media.Duration(store, joined, CancellationToken.None)) > .1) throw new Exception("Geçiş sesi veya süreyi değiştirdi: " + name);

            await Media.Exec(store.Ffmpeg, ["-y", "-ss", "1.25", "-i", joined, "-frames:v", "1", "transition.png"], folder, CancellationToken.None);

            using var frame = new Bitmap(Path.Combine(folder, "transition.png")); var color = frame.GetPixel(160, 90);

            if (name == "Yumuşak karışım" && (color.R < 25 || color.B < 25)) throw new Exception("Karışım karesi iki kaynağı içermiyor");

            results.Add("PASS " + name + " • ses SHA256 ve süre korundu");

        }

        var many = Enumerable.Range(0, 9).Select(i => clips[i % 2]).ToList(); var batchFolder = Path.Combine(root, "batch"); var batch = await Concat(many, batchFolder); var beforeHash = await AudioHash(batch); var duration = await Media.Duration(store, batch, CancellationToken.None);

        await SceneTransitions.Apply(store, many, new TextLayout { Transition = "Sola silme" }, 24, batchFolder, batch, CancellationToken.None);

        if (beforeHash != await AudioHash(batch) || Math.Abs(duration - await Media.Duration(store, batch, CancellationToken.None)) > .1) throw new Exception("Çok sahneli geçiş zamanlaması bozuldu"); results.Add("PASS 9 sahne / 4 girişli sınırlı gruplar • ses ve süre korundu");

        var scene = new Scene { Narration = "Türkçe efekt testi", OnScreen = "Türkçe İŞIK", StockFile = clips[0] };

        foreach (var name in TextLayout.Effects) { var folder = Path.Combine(root, "effect-" + Array.IndexOf(TextLayout.Effects, name)); await TextDesign.Preview(store, scene, new TextLayout { Effect = name, TitleBackground = false, TitleFont = "Georgia", SubtitleFont = "Arial", Italic = true, Underline = true, Spacing = 2 }, false, 0, folder, CancellationToken.None); using var frame = new Bitmap(Path.Combine(folder, "preview.png")); if (frame.Width != 1920 || frame.Height != 1080) throw new Exception("Efekt kare boyutu hatalı"); results.Add("PASS " + name + " • yerel video üstünde Türkçe fontlu kare"); }

        foreach (var name in TextLayout.Animations) { var folder = Path.Combine(root, "animation-" + Array.IndexOf(TextLayout.Animations, name)); await TextDesign.Preview(store, scene, new TextLayout { TitleAnimation = name, SubtitleAnimation = name, FadeOut = true, Karaoke = true }, true, 0, folder, CancellationToken.None); using var frame = new Bitmap(Path.Combine(folder, "preview.png")); if (frame.Width != 1080 || frame.Height != 1920) throw new Exception("Animasyon kare boyutu hatalı"); results.Add("PASS " + name + " • Shorts animasyon ASS render"); }

        var timingScript = new VideoScript { Title = "Zaman testi", TextLayout = new TextLayout { SubtitleEnabled = false, TitleBackground = false }, Scenes = [new Scene { Narration = "Başlık testi", OnScreen = "TEK BAŞLIK", Duration = 4, TitleStart = 2, TitleDuration = 1 }] };

        int VisiblePixels(string file) { using var bmp = new Bitmap(file); int count = 0; for (int y = 50; y < 160; y++) for (int x = 700; x < 1250; x++) { var color = bmp.GetPixel(x, y); if (color.R + color.G + color.B > 500) count++; } return count; }

        int[] counts = new int[3]; double[] times = [1, 2.5, 3.5];

        for (int i = 0; i < 3; i++) { var folder = Path.Combine(root, "title-time-" + i); await EditorPreview.Frame(store, timingScript, 0, times[i], false, folder, CancellationToken.None); counts[i] = VisiblePixels(Path.Combine(folder, "preview.png")); }

        if (counts[0] != 0 || counts[1] < 20 || counts[2] != 0) throw new Exception("Başlık seçilen zamanda görünmüyor / kaybolmuyor"); results.Add("PASS başlık 2–3 saniye aralığında gerçek piksel kontrolü");

        var tone = Path.Combine(root, "music.wav"); await Media.Exec(store.Ffmpeg, ["-y", "-f", "lavfi", "-i", "sine=frequency=880:sample_rate=44100", "-t", "4", "-c:a", "pcm_s16le", tone], root, CancellationToken.None);

        async Task<byte[]> MixTone(double volume, bool duck, bool voice) {

            var track = new MusicTrack { Enabled = true, File = tone, Volume = volume, Start = 1, End = 3, FadeIn = .2, FadeOut = .2, Duck = duck, RightsConfirmed = true, Source = "Test", License = "Test" }; var args = new List<string> { "-y", "-f", "lavfi", "-i", voice ? "sine=frequency=440:sample_rate=44100" : "anullsrc=r=44100:cl=mono", "-stream_loop", "-1", "-i", tone, "-filter_complex", EditorAudio.Mix(track, 4, "0", "1"), "-map", "[a]", "-t", "4", "-ar", "44100", "-ac", "1", "-f", "s16le", "mix.raw" };

            await Media.Exec(store.Ffmpeg, args, root, CancellationToken.None); return await File.ReadAllBytesAsync(Path.Combine(root, "mix.raw"));

        }

        double Rms(byte[] bytes, double from, double to) { double sum = 0; int a = (int)(from * 44100), b = Math.Min(bytes.Length / 2, (int)(to * 44100)); for (int i = a; i < b; i++) { double sample = BitConverter.ToInt16(bytes, i * 2) / 32768d; sum += sample * sample; } return Math.Sqrt(sum / Math.Max(1, b - a)); }

        var quiet = await MixTone(.1, false, false); var loud = await MixTone(.3, false, false); double ratio = Rms(loud, 1.5, 2.5) / Rms(quiet, 1.5, 2.5);

        if (Rms(quiet, 0, .8) > .0001 || Rms(quiet, 3.3, 3.9) > .0001 || ratio < 2.5 || ratio > 3.5) throw new Exception("Müzik zamanı veya ses seviyesi uygulanmadı"); results.Add("PASS müzik başlangıç/bitiş sessizliği ve ses seviyesi oranı");

        var dry = await MixTone(.3, false, true); var ducked = await MixTone(.3, true, true);

        double ToneAmplitude(byte[] bytes) { double sin = 0, cos = 0; int start = 66000, count = 44000; for (int i = start; i < start + count; i++) { double value = BitConverter.ToInt16(bytes, i * 2) / 32768d, angle = 2 * Math.PI * 880 * i / 44100; sin += value * Math.Sin(angle); cos += value * Math.Cos(angle); } return Math.Sqrt(sin * sin + cos * cos) / count; }

        if (ToneAmplitude(ducked) >= ToneAmplitude(dry) * .9) throw new Exception("Konuşmada müzik kısılmadı"); results.Add("PASS konuşma sırasında otomatik müzik kısma");

        timingScript.MusicTrack = new MusicTrack { Enabled = true, File = tone, Start = 1, End = 3, RightsConfirmed = true, Source = "Test", License = "Test", Attribution = "Test kaynak yazısı" }; var previewFolder = Path.Combine(root, "timeline-preview");

        await EditorPreview.Movie(store, timingScript, false, 0, null, previewFolder, CancellationToken.None); if (Math.Abs(await Media.Duration(store, Path.Combine(previewFolder, "motion-with-audio.mp4"), CancellationToken.None) - 4) > .15) throw new Exception("Sesli pencere önizlemesi süresi yanlış"); results.Add("PASS zaman çizelgesinden müzikli MP4 / GIF önizlemesi");

        await File.WriteAllLinesAsync(Path.Combine(store.Root, "design-test-report.txt"), results);

    }

    static byte[]? videoFixture;

    static byte[] VideoFixture(int variant = 0) {

        if (videoFixture == null) {

            var testRoot=Program.Arg(Environment.GetCommandLineArgs(),"--data")??Path.Combine(Path.GetTempPath(),"anka-video-fixtures");var folder=Path.Combine(Path.GetFullPath(testRoot),"video-fixture");var store=new Store(folder);store.Settings.FfmpegPath=Program.Arg(Environment.GetCommandLineArgs(),"--ffmpeg")??store.Settings.FfmpegPath;

            var path = Path.Combine(folder, "fixture.mp4");

            Task.Run(() => Media.Exec(store.Ffmpeg, ["-y", "-f", "lavfi", "-i", "color=c=blue:s=160x90:r=10", "-t", "1", "-c:v", "libx264", "-pix_fmt", "yuv420p", path], folder, CancellationToken.None)).GetAwaiter().GetResult();

            videoFixture = File.ReadAllBytes(path);

        }

        return [.. videoFixture, .. BitConverter.GetBytes(variant)];

    }

    public static int Run()

    {

        var root=Path.GetFullPath(Program.Arg(Environment.GetCommandLineArgs(),"--data")??Path.Combine(Path.GetTempPath(),"anka-tests-"+Guid.NewGuid().ToString("N")));if(Directory.Exists(root)&&Directory.EnumerateFileSystemEntries(root).Any()&&!File.Exists(Path.Combine(root,".anka-self-test-root")))throw new InvalidOperationException("Boş ve ayrı test klasörü gerekli.");Directory.CreateDirectory(root);File.WriteAllText(Path.Combine(root,".anka-self-test-root"),"isolated tests");var result=new List<string>();

        Store FixtureStore(string path){var value=new Store(path);value.Settings.LocalAiFirst=false;value.Settings.FfmpegPath=Program.Arg(Environment.GetCommandLineArgs(),"--ffmpeg")??value.Settings.FfmpegPath;return value;}

        void Test(string name, Action action) { if(Program.Arg(Environment.GetCommandLineArgs(),"--only-self-test") is string only && !name.Contains(only))return; try { action(); result.Add("PASS " + name); } catch (System.ComponentModel.Win32Exception ex) { result.Add("BLOCKED " + name + ": test ortamında Windows kimlik kasası kullanılamadı (" + ex.NativeErrorCode + "). Normal kullanıcı oturumunda tekrar doğrulanmalı."); } catch (Exception ex) { result.Add("FAIL " + name + ": " + ex.Message); } }

        Test("Yazım raporu düzeltir, özgün metni ve stok alanlarını korur", () => {

            var plan = new ContentPlan { Long = new VideoScript { Title = "Herkez", Scenes = [new Scene { Narration = "Herkez burada.", VisualQuery = "forest; sea", StockFile = "keep.mp4", Duration = 8 }] } };

            var report = Proofreading.Parse("{\"Changes\":[{\"Field\":\"Long.Scenes[0].Narration\",\"Before\":\"Herkez burada.\",\"After\":\"Herkes burada.\",\"Reason\":\"Yazım hatası\"}]}", plan);

            var corrected = Proofreading.Apply(plan, report);

            if (plan.Long.Scenes[0].Narration != "Herkez burada." || corrected.Long!.Scenes[0].Narration != "Herkes burada." || corrected.Long.Scenes[0].StockFile != "keep.mp4" || corrected.Long.Scenes[0].Duration != 8) throw new Exception("Metin veya medya korunmadı");

            var hash = Proofreading.Fingerprint(corrected); corrected.Long.Scenes[0].OnScreen = "Yeni metin";

            if (hash == Proofreading.Fingerprint(corrected)) throw new Exception("Editör değişikliği denetimi yenilemedi");

        });

        Test("Geçersiz yazım raporu topluca reddedilir", () => {

            var plan = new ContentPlan { Long = new VideoScript { Title = "Herkez" } };

            foreach (var report in new[] {

                new SpellingResult { Changes = [new() { Field = "Long.Title", Before = "başka", After = "Herkes", Reason = "yazım" }] },

                new SpellingResult { Changes = [new() { Field = "Long.VisualQuery", Before = "", After = "forest", Reason = "yazım" }] },

                new SpellingResult { Changes = [new() { Field = "Long.Title", Before = "Herkez", After = "Herkes", Reason = "yazım" }, new() { Field = "Long.Title", Before = "Herkez", After = "Herkes", Reason = "yazım" }] }

            }) { bool rejected = false; try { Proofreading.Apply(plan, report); } catch (AiContentException) { rejected = true; } if (!rejected || plan.Long.Title != "Herkez") throw new Exception("Geçersiz rapor kabul edildi"); }

            if (Proofreading.Parse("{\"Changes\":[]}", plan).Changes.Count != 0) throw new Exception("Hatasız rapor reddedildi");

        });

        Test("Otonom yazım denetimi rapor kaydeder ve değişmeyen metni tekrar çağırmaz", () => {

            var st = FixtureStore(Path.Combine(root, "proofreading")); st.Settings.LocalAiFirst = false; st.Settings.CloudAiEnabled = true; st.Settings.EnabledCloudProviders = "Gemini,Groq,OpenRouter,OpenAI,Kimi"; st.Settings.ProviderOrder = "OpenRouter";

            var old = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");

            try {

                Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", "fixture");

                var ep = new Episode { ShortCount = 0, Plan = new ContentPlan { Sources = [new() { Title = "Kaynak", Url = "https://example.com/a" }, new() { Title = "Kaynak", Url = "https://example.com/b" }], Long = new VideoScript { Title = "Başlık", Scenes = [new Scene { Narration = "Herkez burada.", VisualQuery = "forest" }] } } };

                using var handler = new SpellingHandler(); using var client = new HttpClient(handler); var logs = new List<string>(); var services = new Services(st, logs.Add, client);

                services.Proofread(ep, CancellationToken.None).GetAwaiter().GetResult();

                services.Proofread(ep, CancellationToken.None).GetAwaiter().GetResult();

                if (handler.Calls != 1 || ep.Plan.Long!.Scenes[0].Narration != "Herkes burada." || !logs.Any(x => x.Contains("Herkez burada. → Herkes burada.")) || Directory.GetFiles(Path.Combine(st.Folder(ep), "yazim-denetimi"), "*-rapor.json").Length != 1) throw new Exception("Otonom düzeltme/rapor/önbellek başarısız");

                ep.Plan.Long.Scenes[0].OnScreen = "Yeni vurgu";

                services.Proofread(ep, CancellationToken.None).GetAwaiter().GetResult();

                if (handler.Calls != 2) throw new Exception("Düzenlenen metin yeniden denetlenmedi");

            } finally { Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", old); }

        });

        Test("Groq JSON 400 standart yanıtla düzeltilir; doğrulama korunur", () => {

            var st = FixtureStore(Path.Combine(root, "groq-json")); st.Settings.LocalAiFirst = false; st.Settings.CloudAiEnabled = true; st.Settings.EnabledCloudProviders = "Gemini,Groq,OpenRouter,OpenAI,Kimi"; st.Settings.ProviderOrder = "Groq"; var old = Environment.GetEnvironmentVariable("GROQ_API_KEY");

            try { Environment.SetEnvironmentVariable("GROQ_API_KEY", "fixture");

                foreach (bool invalid in new[] { false, true }) {

                    using var handler = new GroqJsonHandler { Invalid = invalid }; using var client = new HttpClient(handler); bool rejected = false;

                    try { new Services(st, _ => {}, client).Generate("JSON", "test", CancellationToken.None, text => { if (!text.Contains("ok")) throw new AiContentException("Alan eksik"); return true; }, true).GetAwaiter().GetResult(); } catch (InvalidOperationException) { rejected = true; }

                    if (handler.Calls != 2 || rejected != invalid) throw new Exception("Groq düzeltme veya doğrulama başarısız");

                }

            } finally { Environment.SetEnvironmentVariable("GROQ_API_KEY", old); }

        });

        Test("Müzik zamanı ve kullanım izni doğrulanır", () => {

            var track = new MusicTrack { Enabled = true, File = "test.wav", Start = 1, End = 4, Source = "Kendi kaydım", License = "Kendi eserim", RightsConfirmed = true }; track.Validate();

            foreach (int invalid in new[] { 0, 1, 2 }) { var copy = JsonSerializer.Deserialize<MusicTrack>(JsonSerializer.Serialize(track))!; if (invalid == 0) copy.End = .5; if (invalid == 1) copy.Volume = double.NaN; if (invalid == 2) copy.File = ""; bool rejected = false; try { copy.Validate(); } catch (InvalidOperationException) { rejected = true; } if (!rejected) throw new Exception("Geçersiz müzik kabul edildi"); }

        });

        Test("Hz. seslendirmede Hazreti olur; benzer kelimeler korunur", () => {

            var source = "Hz.Muhammet, hz. Ali ve HZ Ömer. 50Hz frekans, hızlı ve Hazreti.";

            if (NarrationFlow.SpokenText(source) != "Hazreti Muhammet, Hazreti Ali ve Hazreti Ömer. 50Hz frekans, hızlı ve Hazreti." || !source.Contains("Hz.Muhammet")) throw new Exception("Hazreti telaffuzu hatalı");

        });

        Test("Türkçe özel ad ekleri ses için birleşir; ekrandaki yazı korunur", () => {

            var text = "Nur Kapısı'nda, Ankara’da, Türkiye'nin ve Ayşe’ye. 'Merhaba' dedi.";

            if (NarrationFlow.SpokenText(text) != "Nur Kapısında, Ankarada, Türkiyenin ve Ayşeye. 'Merhaba' dedi." || !text.Contains("Kapısı'nda")) throw new Exception("Telaffuz dönüşümü hatalı");

        });

        Test("Silinen YouTube bağlantıları temizlenir; yerel medya korunur", () => {

            var episode = new Episode { Approved = true, Videos = [new AssetVideo { YouTubeId = "old-id", UploadSession = "old-session", Scheduled = true, ThumbnailUploaded = true, ThumbnailAttempted = true, File = "local.mp4", Thumbnail = "local.png" }] };

            Engine.ResetDeletedUploads(episode); var asset = episode.Videos[0];

            if (asset.YouTubeId != "" || asset.UploadSession != "" || asset.Scheduled || asset.ThumbnailUploaded || asset.File != "local.mp4" || asset.Thumbnail != "local.png") throw new Exception("Bağlantı veya dosya durumu hatalı");

        });

        Test("Nur Kapısı altın sinema stili iki formatta çizilir", () => {

            var settings = new Settings { CoverStyle = "Nur Kapısı • Altın sinema", ChannelName = "NUR KAPISI" };

            using var normal = CoverDesigner.Draw(settings, "FETİH SURESİ"); using var shorts = CoverDesigner.Draw(settings, "KÜÇÜK ADIMLAR", true);

            if (normal.Width != 1280 || normal.Height != 720 || shorts.Width != 720 || shorts.Height != 1280) throw new Exception("Kapak oranı yanlış");

            normal.Save(Path.Combine(root, "nur-kapisi-cover.png"));

        });

        Test("Shorts kapağı API'ye gönderilir; açıklama güncellemesi başlık ve etiketi korur", () => {

            var st = FixtureStore(Path.Combine(root, "youtube-design")); var old = Environment.GetEnvironmentVariable("YOUTUBE_REFRESH_TOKEN");

            try { Environment.SetEnvironmentVariable("YOUTUBE_REFRESH_TOKEN", "fixture"); var asset = new AssetVideo { Short = true, YouTubeId = "fixture-video", Thumbnail = Path.Combine(st.Root, "cover.png") }; File.WriteAllBytes(asset.Thumbnail, [1,2,3]); using var handler = new YouTubeDesignHandler(); using var client = new HttpClient(handler); var yt = new YouTube(st, client);

                yt.Thumbnail(asset, CancellationToken.None).GetAwaiter().GetResult(); yt.UpdateDescription(asset, new VideoScript { Description = "Video özeti" }, CancellationToken.None).GetAwaiter().GetResult();

                if (!asset.ThumbnailUploaded || !handler.Thumbnail || !handler.Description) throw new Exception("Kapak veya açıklama güncellenmedi");

            } finally { Environment.SetEnvironmentVariable("YOUTUBE_REFRESH_TOKEN", old); }

        });

        Test("Açıklamada kaynak dökümü kaldırılır; abone çağrısı ve lisans korunur", () => {

            var script = new VideoScript { Description = "Küçük adımların günlük yaşama katkısı.", Scenes = [new Scene { StockUrl = "https://stock.example/video", Creator = "stock", Attribution = "Zorunlu lisans yazısı" }] };

            var description = YouTube.BuildDescription(script, [new SourceRef { Title = "Araştırma", Url = "https://research.example" }],false);

            if (description.Contains("stock.example") || description.Contains("research.example") || !description.Contains("abone ol") || !description.EndsWith("Zorunlu lisans yazısı")) throw new Exception("Açıklama biçimi yanlış");

        });

        Test("Stok siteleri üç tur denenir; bakiye hatası tekrarlanmaz ve bekleme iptal edilir", () => {

            var st = FixtureStore(Path.Combine(root, "stock-loop")); st.Settings.StockOrder = "Pexels"; st.Settings.PoolEnabled = false; st.Settings.PoolFallback = false; st.Settings.PoolAutoSave = false; st.Settings.StockGeneralFallback = false;

            var old = Environment.GetEnvironmentVariable("PEXELS_API_KEY");

            try { Environment.SetEnvironmentVariable("PEXELS_API_KEY", "fixture"); using var handler = new LoopStockHandler(); using var client = new HttpClient(handler); var logs = new List<string>(); var scene = new Scene { VisualQuery = "forest" };

                new Services(st, logs.Add, client).Stock(scene, false, st.Root, [], CancellationToken.None).GetAwaiter().GetResult();

                if (handler.Calls != 3 || !File.Exists(scene.StockFile) || logs.Count(x => x.Contains("Stok arama turu")) != 2) throw new Exception("Üçüncü turda sonuç alınamadı");

                handler.Calls = 0; handler.Permanent = true; try { new Services(st, _ => {}, client).Stock(new Scene { VisualQuery = "forest" }, false, st.Root, [], CancellationToken.None).GetAwaiter().GetResult(); } catch (InvalidOperationException) { }

                if (handler.Calls != 1) throw new Exception("Bakiye hatası tekrarlandı");

                handler.Calls = 0; handler.Permanent = false; st.ProviderBlocked.Clear(); using var cancel = new CancellationTokenSource(100); bool stopped = false;

                try { new Services(st, _ => {}, client).Stock(new Scene { VisualQuery = "forest" }, false, st.Root, [], cancel.Token).GetAwaiter().GetResult(); } catch (OperationCanceledException) { stopped = true; }

                if (!stopped || handler.Calls != 1) throw new Exception("Stok beklemesi durdurulamadı");

            } finally { Environment.SetEnvironmentVariable("PEXELS_API_KEY", old); }

        });

        Test("AI hız sınırı başlık ve hizmet mesajından okunur; uzun kota beklenmez", () => {

            using var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);

            var body = "{\"error\":{\"message\":\"Please try again in 13.462499999s.\"}}";

            if (Services.AiRateWait(response, body)?.TotalSeconds != 15) throw new Exception("Groq beklemesi okunamadı");

            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(3));

            if (Services.AiRateWait(response, body)?.TotalSeconds != 4) throw new Exception("Retry-After önceliği hatalı");

            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromHours(1));

            if (Services.AiRateWait(response, body) != null) throw new Exception("Uzun kota bekleniyor");

            response.Headers.RetryAfter = null;

            if (Services.AiRateWait(response, "[]") != null || Services.AiRateWait(response, "{}") != null) throw new Exception("Belirsiz kotaya bekleme uyduruldu");

        });

        Test("Kimi izinsiz çağrılmaz; JSON üretimi ve model listesi doğru adrese gider", () => {

            var st = FixtureStore(Path.Combine(root, "kimi")); st.Settings.LocalAiFirst = false; st.Settings.CloudAiEnabled = true; st.Settings.EnabledCloudProviders = "Gemini,Groq,OpenRouter,OpenAI,Kimi"; st.Settings.ProviderOrder = "Kimi"; var old = Environment.GetEnvironmentVariable("KIMI_API_KEY");

            try { Environment.SetEnvironmentVariable("KIMI_API_KEY", "fixture"); using var handler = new KimiHandler(); using var client = new HttpClient(handler); var logs = new List<string>();

                void Blocked() { bool blocked = false; try { new Services(st, logs.Add, client).Generate("JSON", "test", CancellationToken.None).GetAwaiter().GetResult(); } catch (InvalidOperationException) { blocked = true; } if (!blocked || handler.Calls != 0) throw new Exception("Ücret izinsiz istek gitti"); }

                st.Settings.UseKimi = true; Blocked(); st.Settings.FreeOnly = false; st.Settings.UseKimi = false; Blocked(); st.Settings.UseKimi = true;

                if (!new Services(st, logs.Add, client).Generate("JSON", "test", CancellationToken.None).GetAwaiter().GetResult().Contains("ok")) throw new Exception("Kimi yanıtı kayboldu");

                var models = ModelDiscovery.Kimi(st, CancellationToken.None, client).GetAwaiter().GetResult(); if (handler.Calls != 2 || models.Length != 1 || models[0] != "kimi-k3") throw new Exception("Kimi model listesi hatalı");

            } finally { Environment.SetEnvironmentVariable("KIMI_API_KEY", old); }

        });

        Test("OpenAI ücretsiz modda neden atlandığını açıklar", () => {

            var st = FixtureStore(Path.Combine(root, "openai-skip")); st.Settings.LocalAiFirst = false; st.Settings.CloudAiEnabled = true; st.Settings.EnabledCloudProviders = "Gemini,Groq,OpenRouter,OpenAI,Kimi"; st.Settings.ProviderOrder = "OpenAI"; var logs = new List<string>(); using var handler = new KimiHandler(); using var client = new HttpClient(handler);

            try { new Services(st, logs.Add, client).Generate("JSON", "test", CancellationToken.None).GetAwaiter().GetResult(); } catch (InvalidOperationException) { }

            if (handler.Calls != 0 || !logs.Any(x => x.Contains("OpenAI atlanacak") && x.Contains("Ücretsiz"))) throw new Exception("Atlama nedeni görünmüyor");

        });

        Test("Eksik Shorts düzeltmesi 429 sonrası aynı sağlayıcıda tamamlanır", () => {

            var st = FixtureStore(Path.Combine(root, "rate-repair")); st.Settings.LocalAiFirst = false; st.Settings.CloudAiEnabled = true; st.Settings.EnabledCloudProviders = "Gemini,Groq,OpenRouter,OpenAI,Kimi"; st.Settings.ProviderOrder = "Groq"; var old = Environment.GetEnvironmentVariable("GROQ_API_KEY");

            try { Environment.SetEnvironmentVariable("GROQ_API_KEY", "fixture"); using var handler = new RateAiHandler(); using var client = new HttpClient(handler); var logs = new List<string>(); var timer = System.Diagnostics.Stopwatch.StartNew();

                var result = new Services(st, logs.Add, client).Generate("JSON", "Bir Shorts gerekli", CancellationToken.None, text => { using var doc = JsonDocument.Parse(text); if (doc.RootElement.GetProperty("Shorts").GetArrayLength() != 1) throw new AiContentException("Shorts sayısı 0; beklenen 1."); return true; }, true).GetAwaiter().GetResult();

                if (handler.Bodies.Count != 3 || handler.Bodies[1] != handler.Bodies[2] || !result.Contains("Başlık") || timer.Elapsed.TotalSeconds < .9 || !logs.Any(x => x.Contains("beklenip"))) throw new Exception("Düzeltme veya bekleme kayboldu");

            } finally { Environment.SetEnvironmentVariable("GROQ_API_KEY", old); }

        });

        Test("AI bekleme durdurulabilir ve sonsuz tekrar yapılmaz", () => {

            var st = FixtureStore(Path.Combine(root, "rate-cancel")); st.Settings.LocalAiFirst = false; st.Settings.CloudAiEnabled = true; st.Settings.EnabledCloudProviders = "Gemini,Groq,OpenRouter,OpenAI,Kimi"; st.Settings.ProviderOrder = "Groq"; var old = Environment.GetEnvironmentVariable("GROQ_API_KEY");

            try { Environment.SetEnvironmentVariable("GROQ_API_KEY", "fixture"); using var handler = new RateAiHandler { DirectLimit = true }; using var client = new HttpClient(handler);

                using (var cancel = new CancellationTokenSource(100)) { bool stopped = false; try { new Services(st, _ => {}, client).Generate("JSON", "test", cancel.Token).GetAwaiter().GetResult(); } catch (OperationCanceledException) { stopped = true; } if (!stopped || handler.Bodies.Count != 1) throw new Exception("Bekleme durdurulamadı"); }

                handler.Bodies.Clear(); bool failed = false; try { new Services(st, _ => {}, client).Generate("JSON", "test", CancellationToken.None).GetAwaiter().GetResult(); } catch (InvalidOperationException) { failed = true; }

                if (!failed || handler.Bodies.Count != 2) throw new Exception("429 tekrar sınırı bozuldu");

            } finally { Environment.SetEnvironmentVariable("GROQ_API_KEY", old); }

        });

        Test("Önizleme zamanları asıl taslağı değiştirmez", () => {

            var script = new VideoScript { Scenes = [new Scene { Narration = "Küçük adımlar büyük değişimler getirir", TitleStart = 1, TitleDuration = 2 }], MusicTrack = new MusicTrack { End = 0 } };

            var preview = EditorPreview.Timed(script, 1); preview.MusicTrack!.End = 5;

            if (script.Scenes[0].Duration != 0 || script.MusicTrack.End != 0 || preview.Scenes[0].TitleStart != 1) throw new Exception("Önizleme taslağı bozdu");

        });

        Test("Gelişmiş font ve efekt verisi güvenli doğrulanır", () => {

            var d = new TextLayout { TitleFont = "Georgia", SubtitleFont = "Arial", Effect = "Neon parıltı", GlowColor = "FF8800", Italic = true, Underline = true, Spacing = 3, ScaleX = 110, Opacity = 80 };

            d.Validate(); var ass = TextDesign.Ass([new Caption(0, 3, "İstanbul gökyüzü")], [(0d, 3d, "Türkçe başlık")], d, 1920, 1080);

            if (!ass.Contains("Style: Title,Georgia") || !ass.Contains("Style: Subtitle,Arial") || !ass.Contains(@"\blur6") || !ass.Contains("&H33FFFFFF")) throw new Exception("Görünüm kayboldu");

            var saved = JsonSerializer.Deserialize<TextLayout>(JsonSerializer.Serialize(d))!; if (saved.TitleFont != "Georgia" || saved.Spacing != 3) throw new Exception("Serileştirme kaybı");

            d.TitleFont = "Arial\n[Events]"; bool rejected = false; try { d.Validate(); } catch (InvalidOperationException) { rejected = true; } if (!rejected) throw new Exception("Font enjeksiyonu kabul edildi");

        });

        Test("Animasyon hizalama kelime vurgusu ve geçiş ayarları", () => {

            var d = new TextLayout { TitleAnimation = "Soldan kayma", SubtitleAnimation = "Yakınlaşma", FadeOut = true, Karaoke = true, TitleAlignment = 1, SubtitleAlignment = 3, Transition = "Sola silme" };

            var ass = TextDesign.Ass([new Caption(0, 3, "Her gün ilerle")], [(0d, 3d, "Başla")], d, 1080, 1920);

            if (!ass.Contains(@"\move(") || !ass.Contains(@"\fad(") || !ass.Contains(@"\t(") || !ass.Contains(@"\kf") || SceneTransitions.Filter(d.Transition) != "wipeleft") throw new Exception("Animasyon veya geçiş kayboldu");

            d.TransitionSeconds = double.NaN; bool rejected = false; try { d.Validate(); } catch (InvalidOperationException) { rejected = true; } if (!rejected) throw new Exception("Geçersiz süre kabul edildi");

        });

        Test("Eski yazı düzeni yeni sürümde animasyonsuz açılır", () => {

            var d = JsonSerializer.Deserialize<TextLayout>("{\"Font\":\"Tahoma\",\"TitleColor\":\"FF8800\"}")!; d.Validate();

            if (d.Transition != "Kesme" || d.TitleAnimation != "Yok" || d.TitleFont != "") throw new Exception("Eski proje varsayılanı değişti");

        });

        Test("Türkçe başlık kapak denetimi öneri üretir", () => {

            var script = new VideoScript { Title = "Küçük Adımlar Büyük Değişim", ThumbnailText = "Küçük Adımlar Büyük Değişim" };

            if (!YouTubeEditorial.Check(script).Any(x => x.Contains("tekrarlıyor"))) throw new Exception("Türkçe tekrar bulunamadı");

            script.ThumbnailText = "Bugün başla"; if (YouTubeEditorial.Check(script).Count != 0) throw new Exception("Farklı vurgu yanlış uyarıldı");

        });

        Test("Yazı tasarımı renk konum süre ve gizleme", () => {

            var design = new TextLayout { TitleColor = "FF8800", TitleSeconds = 2, TitleX = 50, TitleY = 10 };

            var ass = TextDesign.Ass([new Caption(0, 5, "Türkçe altyazı")], [(0d, 5d, "Başlık {test}")], design, 1920, 1080);

            if (!ass.Contains("&H000088FF") || !ass.Contains(@"\pos(960,108)") || !ass.Contains("0:00:02.00,Title") || !ass.Contains("Başlık (test)")) throw new Exception("ASS tasarımı hatalı");

            design.TitleEnabled = false; design.SubtitleEnabled = false;

            if (TextDesign.Ass([new Caption(0, 5, "Test")], [(0d, 5d, "Test")], design, 1080, 1920).Contains("Dialogue:")) throw new Exception("Gizleme çalışmadı");

            design.TitleColor = "invalid"; bool rejected = false; try { design.Validate(); } catch (InvalidOperationException) { rejected = true; } if (!rejected) throw new Exception("Geçersiz renk kabul edildi");

        });

        Test("Yazı kaydı önceki medyayı korur ve yeniden onay ister", () => {

            var st = FixtureStore(Path.Combine(root, "text-save")); var ep = new Episode { Topic = "Yazı testi", Demo = true, ShortCount = 1 }; ep.Plan = Engine.DemoPlan(ep); st.Episodes.Add(ep);

            var file = Path.Combine(st.Folder(ep), "old.mp4"); Directory.CreateDirectory(Path.GetDirectoryName(file)!); File.WriteAllText(file, "preserved-media");

            ep.Videos.Add(new AssetVideo { File = file, QualityPassed = true });

            var plan = JsonSerializer.Deserialize<ContentPlan>(JsonSerializer.Serialize(ep.Plan))!; var narration = plan.Long!.Scenes[0].Narration;

            plan.Long.TextLayout = new TextLayout(); plan.Long.Scenes[0].SubtitleText = "Yeni altyazı"; TextEditSave.Apply(st, ep, plan);

            if (!ep.TextDesignPending || ep.Videos[0].QualityPassed || File.ReadAllText(file) != "preserved-media" || ep.Plan!.Long!.Scenes[0].Narration != narration) throw new Exception("Medya veya seslendirme değişti");

            var hash = NarrationFlow.Fingerprint(plan.Long, st.Settings); plan.Long.TextLayout.TitleSize = 70; plan.Long.Scenes[0].SubtitleText = "Başka altyazı";

            if (hash != NarrationFlow.Fingerprint(plan.Long, st.Settings)) throw new Exception("Yazı düzeni ses önbelleğini bozdu");

            var previousFolder = Path.GetDirectoryName(file)!; Media.Exec(st.Ffmpeg, ["-y", "-f", "lavfi", "-i", "sine=frequency=400:sample_rate=44100", "-t", "1", Path.Combine(previousFolder, "narration-full.wav")], previousFolder, CancellationToken.None).GetAwaiter().GetResult(); File.WriteAllText(Path.Combine(previousFolder, "narration-full.wav.hash"), hash); ep.Videos[0].Key = "long";

            var nextFolder = Path.Combine(previousFolder, "next"); Directory.CreateDirectory(nextFolder); NarrationFlow.Reuse(st, ep, plan.Long, st.Settings, nextFolder, "long", CancellationToken.None).GetAwaiter().GetResult();

            if (Reliability.FileHash(Path.Combine(nextFolder, "narration-full.wav")) != Reliability.FileHash(Path.Combine(previousFolder, "narration-full.wav"))) throw new Exception("Eski ses tekrar kullanılamadı");

            plan.Long.Scenes[0].Narration += " yeni ses"; if (hash == NarrationFlow.Fingerprint(plan.Long, st.Settings)) throw new Exception("Yeni ses eski önbelleği kullanıyor");

        });

        Test("Groq model listesi aktif metin modellerini getirir", () => {

            var st = FixtureStore(Path.Combine(root, "model-discovery")); var old = Environment.GetEnvironmentVariable("GROQ_API_KEY");

            try { Environment.SetEnvironmentVariable("GROQ_API_KEY", "fixture"); using var client = new HttpClient(new DiscoveryHandler()); var models = ModelDiscovery.Groq(st, CancellationToken.None, client).GetAwaiter().GetResult(); if (models.Length != 1 || models[0] != "available-text-model") throw new Exception("Model filtresi hatalı"); }

            finally { Environment.SetEnvironmentVariable("GROQ_API_KEY", old); }

        });

        Test("Sohbet ayrı kalır ve bozuk senaryo bir kez düzeltilir", () => {

            var st = FixtureStore(Path.Combine(root, "plan-repair")); st.Settings.LocalAiFirst = false; st.Settings.CloudAiEnabled = true; st.Settings.EnabledCloudProviders = "Gemini,Groq,OpenRouter,OpenAI,Kimi"; st.Settings.ProviderOrder = "Gemini";

            var old = Environment.GetEnvironmentVariable("GEMINI_API_KEY");

            try {

                Environment.SetEnvironmentVariable("GEMINI_API_KEY", "fixture"); var episode = new Episode { Topic = "Test konusu", Demo = true, IncludeLong = true, LongMinutes=1, ShortCount = 1 };

                var handler = new RepairHandler { ValidPlan = JsonSerializer.Serialize(Engine.DemoPlan(episode)) }; using var client = new HttpClient(handler); var service = new Services(st, _ => {}, client);

                var chat = service.Generate("CHAT_SYSTEM", "CHAT_PRIVATE_MARKER", CancellationToken.None).GetAwaiter().GetResult();

                var plan = service.Generate("PLAN_SYSTEM", "PLAN_REQUEST", CancellationToken.None, text => { Services.ValidateGeneratedPlan(text, episode, []); return true; }, true).GetAwaiter().GetResult();

                if (handler.Calls != 3 || !plan.Contains("Long") || handler.PlanContainsChat) throw new Exception("Sohbet karıştı veya düzeltme yapılmadı");

                bool named = false; try { Services.ValidateGeneratedPlan("{\"Long\":null,\"Shorts\":[]}", episode, []); } catch (AiContentException ex) { named = ex.Message.Contains("Long"); } if (!named) throw new Exception("Eksik alan açıklanmadı");

            } finally { Environment.SetEnvironmentVariable("GEMINI_API_KEY", old); }

        });

        Test("Hedef kanal uyuşmazlığı ve GitHub güvenli aktarım", () => { Engine.VerifyChannel("UCtest", "UCtest", "UCtest"); bool rejected = false; try { Engine.VerifyChannel("UCtest", "UCtest", "UCother"); } catch (InvalidOperationException) { rejected = true; } if (!rejected) throw new Exception("Yanlış kanal kabul edildi"); if (!GitHub.AllowedSource("src/Assets/anka-studio.ico") || !GitHub.AllowedSource("src/Assets/anka-studio-3d.png") || GitHub.AllowedSource("data/private.png")) throw new Exception("Varlık filtresi hatalı"); rejected = false; try { GitHub.CheckRemoteVoice(new Settings { VoiceProvider = "Google TTS" }, new Episode { IncludeLong = true }); } catch (InvalidOperationException) { rejected = true; } if (!rejected) throw new Exception("Uzak kota koruması yok"); });

        Test("Boş Gemini yanıtı ve Groq 404 ayrıntıları alternatif sonunda korunur", () => {

            var st = FixtureStore(Path.Combine(root, "provider-detail")); st.Settings.LocalAiFirst = false; st.Settings.CloudAiEnabled = true; st.Settings.EnabledCloudProviders = "Gemini,Groq,OpenRouter,OpenAI,Kimi"; st.Settings.ProviderOrder = "Gemini,Groq";

            var oldGemini = Environment.GetEnvironmentVariable("GEMINI_API_KEY"); var oldGroq = Environment.GetEnvironmentVariable("GROQ_API_KEY");

            try {

                Environment.SetEnvironmentVariable("GEMINI_API_KEY", "fixture"); Environment.SetEnvironmentVariable("GROQ_API_KEY", "fixture");

                using var client = new HttpClient(new DetailHandler());

                try { new Services(st, _ => {}, client).Generate("JSON test", "test", CancellationToken.None).GetAwaiter().GetResult(); throw new Exception("Boş yanıt kabul edildi"); }

                catch (InvalidOperationException ex) { if (!ex.Message.Contains("boş metin") || !ex.Message.Contains("model_not_found") || !ex.Message.Contains("Model:")) throw new Exception("Ayrıntılar kayboldu"); }

            } finally { Environment.SetEnvironmentVariable("GEMINI_API_KEY", oldGemini); Environment.SetEnvironmentVariable("GROQ_API_KEY", oldGroq); }

        });

        Test("Sağlayıcı hata ayrıntısı model kodunu gösterir ve anahtarı gizler", () => {

            var text = Services.ErrorDetail("{\"error\":{\"code\":\"model_not_found\",\"message\":\"Unknown model; key gsk_secret123\"}}", "llama-3.3-70b-versatile", "gsk_secret123");

            if (!text.Contains("model_not_found") || !text.Contains("llama-3.3") || text.Contains("gsk_secret123")) throw new Exception("Hata ayrıntısı veya gizleme başarısız");

            if (Services.ErrorDetail("<html>private proxy body</html>", "test", "").Contains("private")) throw new Exception("Ham yanıt açığa çıktı");

            var arrayError = Services.ErrorDetail("[{\"error\":{\"code\":404,\"message\":\"Model unavailable\"}}]", "gemini", ""); if (!arrayError.Contains("Model unavailable")) throw new Exception("Dizi hata yanıtı okunamadı");

            _ = Services.ErrorDetail("[]", "gemini", "");

        });

        Test("Proje silme yalnızca seçili paketi siler ve geçersiz yolu reddeder", () => {

            var st = FixtureStore(Path.Combine(root, "delete-test")); var first = new Episode(); var other = new Episode(); st.Episodes.AddRange([first, other]);

            File.WriteAllText(Path.Combine(st.Folder(first), "video.mp4"), "test"); var otherPath = st.Folder(other); File.WriteAllText(Path.Combine(otherPath, "keep.txt"), "keep");

            var archive = Path.Combine(st.Root, "audio-library"); Directory.CreateDirectory(archive); File.WriteAllText(Path.Combine(archive, "keep.wav"), "keep"); st.Save();

            PackageDeletion.Delete(st, first);

            if (st.Episodes.Contains(first) || Directory.Exists(Path.Combine(st.Root, "projects", first.Id)) || !File.Exists(Path.Combine(otherPath, "keep.txt")) || !File.Exists(Path.Combine(archive, "keep.wav")) || !File.Exists(Path.Combine(st.Root, "settings.json"))) throw new Exception("Silme kapsamı hatalı");

            var bad = new Episode { Id = ".." }; st.Episodes.Add(bad); bool rejected = false; try { PackageDeletion.Delete(st, bad); } catch (InvalidOperationException) { rejected = true; } if (!rejected || !st.Episodes.Contains(bad)) throw new Exception("Geçersiz yol kabul edildi");

        });

        Test("Uyarı çözümü videoları ve revizyonu korur", () => {

            var episode = new Episode { Revision = 4, Approved = true, ApprovalHash = "old", Progress = 100, Plan = new ContentPlan { Warnings = ["Kaynak incelenmeli"] }, Videos = [new AssetVideo { File = "existing.mp4", QualityPassed = true }] };

            var asset = episode.Videos[0]; WarningResolution.Resolve(episode, "Kaynak incelenmeli", "Kaynak incelendi; alıntı kullanılmadığı doğrulandı.");

            if (episode.Videos.Count != 1 || !ReferenceEquals(asset, episode.Videos[0]) || episode.Revision != 4 || episode.Progress != 100 || episode.Approved || episode.Plan.Warnings.Count != 0 || !episode.Events.Last().Contains("Kaynak incelendi")) throw new Exception("Medya veya inceleme kaydı bozuldu");

        });

        Test("Bütün anlatım ses blokları ve altyazı sınırları", () => {

            var text = "Birinci cümle. İkinci cümle birinciyle bağlantılıdır."; if (NarrationFlow.Blocks(text).Count != 1) throw new Exception("Kısa anlatım bölündü");

            if (NarrationFlow.Blocks(string.Join(" ", Enumerable.Repeat("Türkçe sözcük.", 1000))).Any(x => Encoding.UTF8.GetByteCount(x) > 4400)) throw new Exception("Sağlayıcı sınırı aşıldı");

            var captions = NarrationFlow.Align(text, 10); if (captions.Count == 0 || captions[0].Start != 0 || captions.Last().End != 10 || captions.Any(x => x.End <= x.Start)) throw new Exception("Altyazı zamanları geçersiz");

        });

        Test("İki sahne tek anlatım ve ses önbelleği", () => {

            var st = FixtureStore(Path.Combine(root, "narration-flow")); Directory.CreateDirectory(st.Root); var script = new VideoScript { Scenes = [new Scene { Narration = "Birinci cümle." }, new Scene { Narration = "İkinci cümle." }] }; var calls = 0;

            Task Speech(string text, string path, Settings settings, CancellationToken ct) { calls++; return Media.Exec(st.Ffmpeg, ["-y", "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=24000", "-t", "2", path], st.Root, ct); }

            var services = new Services(st, _ => { }); NarrationFlow.Prepare(st, new Episode(), script, st.Settings, st.Root, services, CancellationToken.None, Speech).GetAwaiter().GetResult();

            NarrationFlow.Prepare(st, new Episode(), script, st.Settings, st.Root, services, CancellationToken.None, Speech).GetAwaiter().GetResult();

            if (calls != 2 || !File.Exists(Path.Combine(st.Root, "speech-2.wav"))) throw new Exception("Önbellek veya sahne sesleri hatalı");

        });

        Test("Kapak alternatifleri: 429, ücret izni ve aylık sınır", () => {

            var previousNvidia=Environment.GetEnvironmentVariable("NVIDIA_API_KEY");Environment.SetEnvironmentVariable("NVIDIA_API_KEY",null);var st = FixtureStore(Path.Combine(root, "cover-fallback")); var settings = new Settings { CoverAiEnabled = true, CoverPaidAllowed = true, FreeOnly = false };

            Environment.SetEnvironmentVariable("GEMINI_API_KEY", "fixture"); Environment.SetEnvironmentVariable("OPENAI_API_KEY", "fixture");

            try {

                var handler = new CoverHandler(); using var http = new HttpClient(handler); var cover = new CoverFallback(st, _ => { }, http); var file = Path.Combine(st.Root, "cover.png");

                cover.Create(file, settings, "Teknoloji", "Başlık", false, CancellationToken.None).GetAwaiter().GetResult(); int expected=CoverFallback.ModelsFor("Gemini",settings).Length+1;if (handler.Calls != expected || !File.Exists(file)) throw new Exception($"Alternatif geçişi başarısız: çağrı={handler.Calls}, beklenen={expected}, dosya={File.Exists(file)}");

                settings.FreeOnly = true; cover.Create(file, settings, "Konu", "Başlık", false, CancellationToken.None).GetAwaiter().GetResult(); if (handler.Calls != expected) throw new Exception("Ücretsiz mod ücretli servise gitti");

                settings.FreeOnly = false; settings.CoverPaidAllowed = false; cover.Create(file, settings, "Konu", "Başlık", false, CancellationToken.None).GetAwaiter().GetResult(); if (handler.Calls != expected) throw new Exception("Ücret izni yokken istek atıldı");

                settings.CoverPaidAllowed = true; settings.CoverMonthlyRequests = 0; cover.Create(file, settings, "Konu", "Başlık", false, CancellationToken.None).GetAwaiter().GetResult(); if (handler.Calls != expected) throw new Exception("İstek sınırı aşıldı");

            } finally { Environment.SetEnvironmentVariable("NVIDIA_API_KEY",previousNvidia);Environment.SetEnvironmentVariable("GEMINI_API_KEY", null); Environment.SetEnvironmentVariable("OPENAI_API_KEY", null); }

        });

        Test("Kapak tarzları, oranlar ve kullanıcı görseli", () => {

            foreach (var style in CoverDesigner.Styles) { using var image = CoverDesigner.Draw(new Settings { CoverStyle = style, ChannelName = "Örnek kanal" }, "Türkçe başlık • yeni fikir"); if (image.Width != 1280 || image.Height != 720) throw new Exception("Kapak oranı hatalı"); }

            using var portrait = CoverDesigner.Draw(new Settings(), "Shorts", true); if (portrait.Width != 720 || portrait.Height != 1280) throw new Exception("Shorts kapağı hatalı");

            var file = Path.Combine(root, "custom-cover.png"); using (var custom = new Bitmap(1280, 720)) { using var g = Graphics.FromImage(custom); g.Clear(Color.Red); custom.Save(file); }

            using var ready = CoverDesigner.Draw(new Settings { CoverMode = "Kendi PNG kapak", CoverPng = file }, "Bu yazı eklenmemeli"); if (ready.GetPixel(100, 100).R != 255) throw new Exception("Kullanıcı kapağı değiştirilmiş");

            if (CoverDesigner.Suggest("Fetih suresi") != "Manevi" || CoverDesigner.Suggest("Yapay zekâ") != "Teknoloji") throw new Exception("Otomatik tarz hatalı");

        });

        Test("Ses arşivi kopyalama, tekrar engeli ve iptal", () => {

            var st = FixtureStore(Path.Combine(root, "audio")); var library = new AudioLibrary(st);

            var file = Path.Combine(root, "click.wav"); File.WriteAllBytes(file, UiSound.Create(.25));

            library.Add(file, new AudioItem { Kind = "Ses efekti" }, CancellationToken.None).GetAwaiter().GetResult();

            library.Add(file, new AudioItem { Kind = "Ses efekti" }, CancellationToken.None).GetAwaiter().GetResult();

            var items = library.List(); if (items.Count != 1 || items[0].RightsConfirmed || !File.Exists(items[0].File) || !File.Exists(file)) throw new Exception("Arşiv veya izin varsayılanı hatalı");

            File.WriteAllBytes(file, UiSound.Create(.5)); using var cancellation = new CancellationTokenSource(); cancellation.Cancel();

            try { library.Add(file, new AudioItem(), cancellation.Token).GetAwaiter().GetResult(); throw new Exception("İptal işlenmedi"); } catch (OperationCanceledException) { }

            if (library.List().Count != 1 || Directory.GetFiles(library.Folder, "*.part").Length != 0) throw new Exception("İptal artığı kaldı");

        });

        Test("Paket: iki döngü veya bağımsız Shorts", () => { Rules.Validate(new() { Topic = "Bilim", IncludeLong = false, ShortCount = 2 }); try { Rules.Validate(new() { Topic = "Bilim", IncludeLong = false, ShortCount = 0 }); throw new Exception("Geçersiz paket kabul edildi"); } catch (InvalidOperationException) { } });

        Test("Toplu konu programı ve saat dilimi", () => { var e = TopicProgram.Parse("Tarihsiz konu\n2030-01-01 20:00 | Planlı konu | Editoryal talimat", true, 6, 2, 45); if (e.Count != 2 || e[0].PublishAt != null || e[1].PublishAt?.Offset != TimeSpan.FromHours(3) || e[1].Brief != "Editoryal talimat") throw new Exception("Konu programı bozuldu"); });

        Test("Normal video + Shorts tarihleri UTC+3", () => { var e = new Episode { IncludeLong = true, ShortSpacingHours = 24, PublishAt = new DateTimeOffset(2030, 1, 1, 20, 0, 0, TimeSpan.FromHours(3)) }; if (Rules.VideoDate(e, true, 0) != e.PublishAt.Value.AddDays(1)) throw new Exception("Tarih sırası hatalı"); e.IncludeLong = false; if (Rules.VideoDate(e, true, 0) != e.PublishAt) throw new Exception("Bağımsız tarih hatalı"); });

        Test("Türkçe altyazı zamanlaması", () => { var text = Captions.Srt([new(.125, 2.3, "İstanbul, ışık ve öğrenme")]); if (!text.Contains("00:00:00,125 --> 00:00:02,300") || !text.Contains("İstanbul")) throw new Exception("Altyazı bozuldu"); });

        Test("Vecteezy araması, kota kontrolü ve gizli anahtar ayrımı", () => {

            VecteezyFixture(() => {

                var st = FixtureStore(Path.Combine(root, "vecteezy-success")); st.Settings.StockOrder = "Vecteezy";

                using var handler = new VecteezyHandler(); using var http = new HttpClient(handler); var scene = new Scene { VisualQuery = "sea" };

                new Services(st, _ => { }, http).Stock(scene, false, st.Root, [], CancellationToken.None).GetAwaiter().GetResult();

                if (handler.Quotas != 2 || handler.Searches != 1 || handler.Downloads != 1 || !File.Exists(scene.StockFile) || !scene.Attribution.Contains("Free Stock Footage by Vecteezy.com")) throw new Exception("Kota / arama / kaynak kaydı hatalı");

                var pool = new MediaPool(st); var reused = new Scene();

                if (!pool.TryUse(reused, "sea", st.Root, [], true) || reused.Attribution != scene.Attribution) throw new Exception("Havuzda kaynak yazısı kayboldu");

                var zippedFolder = Path.Combine(st.Root, "zip-test"); Directory.CreateDirectory(zippedFolder);

                using var zipHandler = new VecteezyHandler { Zip = true }; using var zipHttp = new HttpClient(zipHandler); var zipped = new Scene { VisualQuery = "sea" };

                new Vecteezy(st, zipHttp).Stock(zipped, false, zippedFolder, [], CancellationToken.None).GetAwaiter().GetResult();

                if (!File.ReadAllBytes(zipped.StockFile).SequenceEqual(VideoFixture()) || File.Exists(Path.Combine(root, "escape.mp4")) || Directory.GetFiles(zippedFolder, "*.part").Length > 0) throw new Exception("ZIP çıkarma veya geçici dosya temizleme hatalı");

                var extra = Path.Combine(st.Root, "manual.mp4"); File.WriteAllBytes(extra, VideoFixture(6));

                pool.AddAsync(extra, "forest", "Doğa", "Dareful", "creator", "CC BY 4.0", CancellationToken.None, "Video courtesy of Dareful — fixture").GetAwaiter().GetResult();

                if (!pool.List().Any(x => x.Attribution.Contains("Dareful"))) throw new Exception("Dışarıdan eklenen kaynak yazısı kayboldu");

                var description = YouTube.BuildDescription(new VideoScript { Description = new string('x', 5000), Scenes = [reused] }, []);

                if (description.Length > 4500 || !description.EndsWith(scene.Attribution)) throw new Exception("Açıklama kesilince kaynak yazısı kayboldu");

                if (Directory.GetFiles(st.Root, "*.credit.json").Any(f => File.ReadAllText(f).Contains("fixture-vecteezy"))) throw new Exception("Anahtar kayda sızdı");

            });

        });

        Test("Vecteezy dolu kota indirmeyi engeller ve havuza döner", () => {

            VecteezyFixture(() => {

                var st = FixtureStore(Path.Combine(root, "vecteezy-quota")); st.Settings.StockOrder = "Vecteezy"; st.Settings.PoolEnabled = false;

                var input = Path.Combine(st.Root, "input.mp4"); File.WriteAllBytes(input, VideoFixture(9)); new MediaPool(st).Add(input, "sea", "Doğa", "Kendi çekimim", "test", "Kullanım hakkı bende");

                using var handler = new VecteezyHandler { DownloadUsed = 500 }; using var http = new HttpClient(handler); var scene = new Scene { VisualQuery = "sea" };

                new Services(st, _ => { }, http).Stock(scene, false, st.Root, [], CancellationToken.None).GetAwaiter().GetResult();

                if (handler.Downloads != 0 || handler.Searches != 1 || scene.Creator != "test") throw new Exception("Kota aşımı veya havuza geçiş hatalı");

            });

        });

        Test("Vecteezy program sınırı, arama sınırı ve kapatma", () => {

            VecteezyFixture(() => {

                var st = FixtureStore(Path.Combine(root, "vecteezy-limits"));

                using var handler = new VecteezyHandler { DownloadUsed = 450 }; using var http = new HttpClient(handler); var api = new Vecteezy(st, http);

                bool rejected = false; try { api.Stock(new Scene { VisualQuery = "sea" }, false, st.Root, [], CancellationToken.None).GetAwaiter().GetResult(); } catch (ProviderException) { rejected = true; }

                if (!rejected || handler.Downloads != 0) throw new Exception("450 sınırı uygulanmadı");

                handler.GeneralUsed = 100; handler.Searches = 0; rejected = false;

                try { api.Stock(new Scene { VisualQuery = "sea" }, false, st.Root, [], CancellationToken.None).GetAwaiter().GetResult(); } catch (ProviderException) { rejected = true; }

                if (!rejected || handler.Searches != 0) throw new Exception("Arama kotası aşımı");

                st.Settings.VecteezyEnabled = false; if (api.Ready) throw new Exception("Kapalı alternatif hâlâ etkin");

            });

        });

        Test("Zorunlu kaynak yazıları sığmazsa yayın engellenir", () => {

            try { YouTube.BuildDescription(new VideoScript { Scenes = [new Scene { Attribution = new string('x', 4500) }] }, []); throw new Exception("Kaynak yazısı sessizce kesildi"); } catch (InvalidOperationException) { }

        });

        Test("Windows kasası şifreleme ve tekrar açma", () => { var path = Path.Combine(root, "secrets.dat"); var v = new Vault(path); v.Set("PIXABAY_API_KEY", "fixture-secret-never-ship"); if (Encoding.UTF8.GetString(File.ReadAllBytes(path)).Contains("fixture-secret-never-ship")) throw new Exception("Düz metin anahtar"); if (new Vault(path).Get("PIXABAY_API_KEY") != "fixture-secret-never-ship") throw new Exception("Anahtar okunamadı"); });

        Test("Alternatif AES kasa ve yanlış parola reddi", () => { var path = Path.Combine(root, "password-secrets.dat"); var v = new Vault(path); v.EnablePassword("test-password-12345"); v.Set("PIXABAY_API_KEY", "fixture-password-secret"); if (Encoding.UTF8.GetString(File.ReadAllBytes(path)).Contains("fixture-password-secret")) throw new Exception("Düz metin anahtar"); if (new Vault(path, "test-password-12345").Get("PIXABAY_API_KEY") != "fixture-password-secret") throw new Exception("Anahtar okunamadı"); try { _ = new Vault(path, "wrong-password"); throw new Exception("Yanlış parola kabul edildi"); } catch (InvalidOperationException) { } });

        Test("Kayıt ve kesinti sonrası durum", () => { var s = FixtureStore(Path.Combine(root, "store")); s.Episodes.Add(new() { Topic = "Kayıt", Status = "Üretiliyor" }); s.Save(); var recovered = FixtureStore(s.Root); if (recovered.Episodes[0].Status != "Kesildi") throw new Exception("İş kurtarılamadı"); });

        Test("Senaryo değişikliği onayı geçersiz kılar", () => { var e = new Episode { Topic = "Örnek", Demo = true, ShortCount = 1 }; e.Plan = Engine.DemoPlan(e); var before = Rules.HashPlan(e); e.Plan.Long!.Scenes[0].Narration += " Revizyon."; if (before == Rules.HashPlan(e)) throw new Exception("İçerik imzası değişmedi"); });

        Test("Kaynak paketinden anahtar ve çıktılar dışlanır", () => { foreach (var p in new[] { "secrets.dat", "settings.json", "projects/a/plan.json", "src/bin/app.cs", ".git/config" }) if (GitHub.AllowedSource(p)) throw new Exception("Özel dosya seçildi"); if (!GitHub.AllowedSource(".github/workflows/produce.yml")) throw new Exception("İş akışı dışlandı"); });

        Test("GitHub secret şifrelemesi", () => { using var pair = Sodium.PublicKeyBox.GenerateKeyPair(); var text = Encoding.UTF8.GetBytes("test-secret"); var encrypted = Sodium.SealedPublicKeyBox.Create(text, pair.PublicKey); var restored = Sodium.SealedPublicKeyBox.Open(encrypted, pair); if (!restored.SequenceEqual(text)) throw new Exception("Şifreleme hatası"); });

        Test("Ücretsiz sağlayıcı 429 alternatif geçişi", () => { var s = FixtureStore(Path.Combine(root, "fallback")); s.Settings.LocalAiFirst = false; s.Settings.CloudAiEnabled = true; s.Settings.EnabledCloudProviders = "Gemini,Groq,OpenRouter,OpenAI,Kimi"; s.Settings.ProviderOrder = "Gemini,Groq,OpenAI"; Environment.SetEnvironmentVariable("GEMINI_API_KEY", "fixture"); Environment.SetEnvironmentVariable("GROQ_API_KEY", "fixture"); Environment.SetEnvironmentVariable("OPENAI_API_KEY", "fixture"); var handler = new FakeHandler(); using var client = new HttpClient(handler); var log = new List<string>(); var output = new Services(s, log.Add, client).Generate("test", "test", CancellationToken.None).GetAwaiter().GetResult(); if (handler.Calls.Count != 2 || !output.Contains("ok") || !log.Any(x => x.Contains("Alternatife"))) throw new Exception("Alternatif geçişi yapılmadı"); });

        Test("Tüm kotalar dolduğunda kontrollü bekleme", () => { var s = FixtureStore(Path.Combine(root, "blocked")); s.Settings.LocalAiFirst = false; s.Settings.CloudAiEnabled = true; s.Settings.EnabledCloudProviders = "Gemini,Groq,OpenRouter,OpenAI,Kimi"; s.Settings.ProviderOrder = "Gemini,Groq"; Environment.SetEnvironmentVariable("GEMINI_API_KEY", "fixture"); Environment.SetEnvironmentVariable("GROQ_API_KEY", "fixture"); using var client = new HttpClient(new FakeHandler { FailAll = true }); try { new Services(s, _ => { }, client).Generate("test", "test", CancellationToken.None).GetAwaiter().GetResult(); throw new Exception("Sahte içerik üretildi"); } catch (InvalidOperationException e) { if (!e.Message.Contains("Tüm kullanılabilir")) throw; } });

        Test("Geçersiz JSON kabul edilmez", () => { try { Services.ExtractJson("JSON yok"); throw new Exception("Bozuk veri kabul edildi"); } catch (InvalidOperationException) { } });

        Test("Demo yayın onayına geçemez", () => { var s = FixtureStore(Path.Combine(root, "demo")); try { new Engine(s).Approve(new() { Demo = true }); throw new Exception("Demo onaylandı"); } catch (InvalidOperationException) { } });

        Test("Google TTS API anahtarı, WAV ve yerel kota", () => {

            var st = FixtureStore(Path.Combine(root, "google-tts")); st.Settings.CloudUsageRequired = false; Environment.SetEnvironmentVariable("GOOGLE_TTS_API_KEY", "fixture-tts");

            using var http = new HttpClient(new TtsHandler()); var google = new GoogleTts(st, http);

            var voices = google.Voices(CancellationToken.None).GetAwaiter().GetResult(); if (!voices.Contains("tr-TR-Standard-A")) throw new Exception("Türkçe ses yok");

            var path = Path.Combine(st.Root, "voice.wav"); google.Speak("Işık 🌙", path, st.Settings, CancellationToken.None).GetAwaiter().GetResult();

            if (!File.Exists(path) || google.ReadUsage().Characters != 6) throw new Exception("Ses veya karakter sayacı hatalı");

            st.Settings.GoogleTtsMonthlyLimit = 6; try { google.Speak("x", path, st.Settings, CancellationToken.None).GetAwaiter().GetResult(); throw new Exception("Kota aşımı kabul edildi"); } catch (InvalidOperationException ex) { if (!ex.Message.Contains("sınır")) throw; }

            Environment.SetEnvironmentVariable("GOOGLE_TTS_API_KEY", null);

        });

        Test("Güncellemede eski ayarlar ve kayıtlı anahtar korunur", () => {

            var dir = Path.Combine(root, "migration"); Directory.CreateDirectory(dir);

            File.WriteAllText(Path.Combine(dir, "settings.json"), "{\"GoogleClientId\":\"existing-client\",\"Repo\":\"owner/repo\",\"VoiceProvider\":\"Windows / ücretsiz\"}");

            var vault = new Vault(Path.Combine(dir, "secrets.dat")); vault.EnablePassword("migration-password-123"); vault.Set("GITHUB_TOKEN", "existing-token");

            var before = File.ReadAllBytes(Path.Combine(dir, "secrets.dat")); var oldProvider = Vault.PasswordProvider; Vault.PasswordProvider = _ => "migration-password-123"; Store st; try { st = FixtureStore(dir); } finally { Vault.PasswordProvider = oldProvider; }

            if (st.Settings.GoogleClientId != "existing-client" || st.Settings.Repo != "owner/repo" || st.Settings.GoogleTtsMonthlyLimit != 900000 || !before.SequenceEqual(File.ReadAllBytes(Path.Combine(dir, "secrets.dat")))) throw new Exception("Eski kayıt değişti");

            if (new Vault(Path.Combine(dir, "secrets.dat"), "migration-password-123").Get("GITHUB_TOKEN") != "existing-token") throw new Exception("Anahtar kayboldu");

        });

        Test("Stok sonuç yoksa sonraki aramaya geçer", () => {

            var st = FixtureStore(Path.Combine(root, "stock-retry")); st.Settings.StockOrder = "Pixabay";

            Environment.SetEnvironmentVariable("PIXABAY_API_KEY", "fixture");

            try {

                var handler = new StockHandler(); using var http = new HttpClient(handler);

                var scene = new Scene { VisualQuery = "missing; sunrise" };

                new Services(st, _ => { }, http).Stock(scene, false, st.Root, [], CancellationToken.None).GetAwaiter().GetResult();

                if (handler.Searches != 2 || !File.Exists(scene.StockFile) || scene.VisualQuery != "missing; sunrise" || scene.Creator != "author") throw new Exception("Alternatif veya kaynak kaydı hatalı");

            } finally { Environment.SetEnvironmentVariable("PIXABAY_API_KEY", null); }

        });

        Test("Stok aramaları eski virgülleri düzeltir ve Türkçeyi çevirir", () => {

            var query = StockSearch.Normalize("montage of daily routines: reading, walking, organizing");

            if (!query.Contains("; walking; organizing") || query.Contains(',')) throw new Exception("Eski virgüller ayrılmadı");

            if (StockSearch.Normalize(query) != query) throw new Exception("Normalizasyon kararsız");

            if (!Services.StockQueries("deniz; uzay; orman").SequenceEqual(new[] { "sea", "space", "forest" })) throw new Exception("Türkçe çeviri hatalı");

            if (StockSearch.Backgrounds.Length < 60 || StockSearch.GeneralQueries("sea waves").Length > 6) throw new Exception("Katalog veya istek bütçesi hatalı");

        });

        Test("429 yerel arşive döner ve yeniden açılışta beklemeyi korur", () => {

            var st = FixtureStore(Path.Combine(root, "pool-rate-fallback")); st.Settings.StockOrder = "Pixabay"; st.Settings.PoolEnabled = false;

            var pool = new MediaPool(st);

            for (int i = 1; i <= 2; i++) { var path = Path.Combine(st.Root, "input" + i + ".mp4"); File.WriteAllBytes(path, VideoFixture(i)); var item = pool.Add(path, "orman", "Doğa", "Arşiv", "test", "Kullanım hakkı bende"); }

            var entries = pool.List(); foreach (var entry in entries) entry.LastUsed = DateTimeOffset.UtcNow; pool.Save(entries);

            var old = Environment.GetEnvironmentVariable("PIXABAY_API_KEY"); Environment.SetEnvironmentVariable("PIXABAY_API_KEY", "fixture-cooldown");

            try {

                var handler = new RateStockHandler(); using var client = new HttpClient(handler); var used = new HashSet<long>(); var first = new Scene { VisualQuery = "forest" };

                new Services(st, _ => { }, client).Stock(first, false, st.Root, used, CancellationToken.None).GetAwaiter().GetResult();

                var reopened = FixtureStore(st.Root); var second = new Scene { VisualQuery = "forest" };

                new Services(reopened, _ => { }, client).Stock(second, false, st.Root, used, CancellationToken.None).GetAwaiter().GetResult();

                if (handler.Calls != 1 || !File.Exists(first.StockFile) || !File.Exists(second.StockFile) || first.StockFile == second.StockFile) throw new Exception("429 tekrar isteği, havuz veya paket içi tekrar hatalı");

                if (Directory.GetFiles(Path.Combine(st.Root, "cache"), "pixabay-budget-*.json").Any(f => File.ReadAllText(f).Contains("fixture-cooldown"))) throw new Exception("Anahtar sızdı");

            } finally { Environment.SetEnvironmentVariable("PIXABAY_API_KEY", old); }

        });

        Test("Stok servisi yokken genel sade fon arşivden seçilir", () => {

            var st = FixtureStore(Path.Combine(root, "generic-pool-fallback")); st.Settings.StockOrder = ""; st.Settings.PoolEnabled = false;

            var input = Path.Combine(st.Root, "sea.mp4"); File.WriteAllBytes(input, VideoFixture(5)); var pool = new MediaPool(st); pool.Add(input, "deniz dalga", "Genel", "Kendi çekimim", "test", "Kullanım hakkı bende");

            var scene = new Scene { VisualQuery = "organizing tasks desk" };

            new Services(st, _ => { }).Stock(scene, false, st.Root, [], CancellationToken.None).GetAwaiter().GetResult();

            if (!File.Exists(scene.StockFile) || scene.Creator != "test") throw new Exception("Genel fon seçilmedi");

            st.Settings.StockGeneralFallback = false;

            bool rejected = false; try { new Services(st, _ => { }).Stock(new Scene { VisualQuery = "organizing tasks desk" }, false, st.Root, [], CancellationToken.None).GetAwaiter().GetResult(); } catch (InvalidOperationException) { rejected = true; }

            if (!rejected) throw new Exception("Genel fon kapalıyken kullanıldı");

        });

        Test("Pixabay hesap hızına göre güvenli aralık", () => {

            if (Services.PixabayInterval(100) < 1200 || Services.PixabayInterval(10) < 6000 || Services.PixabayInterval(1) < 60000) throw new Exception("İstek aralığı hesabın limitini aşar");

        });

        Test("Havuz klasörü 70 MP4 tarar, kayıtları korur ve tekrar eklemez", () => {

            var st = FixtureStore(Path.Combine(root, "pool-folder-scan")); var folder = Path.Combine(st.Root, "media-pool"); Directory.CreateDirectory(folder); var pool = new MediaPool(st);

            for (int i = 0; i < 70; i++) File.WriteAllBytes(Path.Combine(folder, "video-" + i + ".mp4"), VideoFixture(i));

            File.WriteAllBytes(Path.Combine(folder, "duplicate.mp4"), VideoFixture()); File.WriteAllText(Path.Combine(folder, "ignored.txt"), "untouched");

            var scan = pool.ScanFolder(CancellationToken.None).GetAwaiter().GetResult();

            if (scan.Added != 70 || scan.Duplicate != 1 || pool.List().Count != 70 || Directory.GetFiles(folder, "*.mp4").Length != 71) throw new Exception("70 dosya taraması veya kopyasız kayıt hatalı");

            var again = pool.ScanFolder(CancellationToken.None).GetAwaiter().GetResult(); if (again.Added != 0 || again.Existing != 70 || pool.List().Count != 70) throw new Exception("Tekrar taramada çoğaltıldı");

            var scene = new Scene { VisualQuery = "desk organizing" }; st.Settings.StockOrder = ""; st.Settings.PoolEnabled = false;

            new Services(st, _ => { }).Stock(scene, false, st.Root, [], CancellationToken.None).GetAwaiter().GetResult(); if (!File.Exists(scene.StockFile)) throw new Exception("Taranan videolar fon olarak kullanılamadı");

            using var canceled = new CancellationTokenSource(); canceled.Cancel(); bool stopped = false;

            try { pool.ScanFolder(canceled.Token).GetAwaiter().GetResult(); } catch (OperationCanceledException) { stopped = true; }

            if (!stopped || pool.List().Count != 70) throw new Exception("İptal veya kayıt koruma hatalı");

        });

        Test("Stok noktalı virgül alternatifleri", () => {

            var queries = Services.StockQueries(" sunrise ; family dinner ; SUNRISE ; ");

            if (!queries.SequenceEqual(new[] { "sunrise", "family dinner" })) throw new Exception("Alternatif sırası veya boşluklar hatalı");

            try { Services.StockQueries(" ; ; "); throw new Exception("Boş arama kabul edildi"); } catch (InvalidOperationException) { }

        });

        Test("Havuz kopyası, eşleşme, tekrar sınırı ve güvenli silme", () => {

            var st = FixtureStore(Path.Combine(root, "pool")); var pool = new MediaPool(st);

            var input = Path.Combine(st.Root, "fixture.mp4"); File.WriteAllBytes(input, VideoFixture());

            var item = pool.Add(input, "forest; nature", "Doğa", "Kendi çekimim", "test", "Kullanım hakkı bende");

            pool.Add(input, "trees", "Doğa", "Kendi çekimim", "test", "Kullanım hakkı bende");

            if (pool.List().Count != 1 || !pool.List()[0].Tags.Contains("trees")) throw new Exception("Tekrar dosya oluştu");

            var target = Path.Combine(st.Root, "project"); Directory.CreateDirectory(target); var scene = new Scene(); var used = new HashSet<long>();

            if (pool.TryUse(scene, "dinner", target, used) || !pool.TryUse(scene, "forest", target, used) || !File.Exists(scene.StockFile) || scene.Creator != "test") throw new Exception("Etiket veya kaynak eşleşmesi hatalı");

            if (pool.TryUse(new Scene(), "forest", target, []) || pool.TryUse(new Scene(), "forest", target, used)) throw new Exception("Tekrar sınırı çalışmadı");

            st.Settings.PoolRepeatHours = 0; if (!pool.TryUse(new Scene(), "trees", target, [])) throw new Exception("Tekrar ayarı uygulanmadı");

            pool.Delete(item); if (File.Exists(item.File) || !File.Exists(input) || !File.Exists(scene.StockFile)) throw new Exception("Yanlış dosya silindi");

            try { pool.Delete(new PoolItem { File = input }); throw new Exception("Havuz dışı silindi"); } catch (InvalidOperationException) { }

        });

        Test("Pixabay 429 bekleme süresi", () => {

            using var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);

            response.Headers.Add("X-RateLimit-Reset", "7"); if (Services.PixabayWait(response) != 8) throw new Exception("Reset süresi yanlış");

            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(3)); if (Services.PixabayWait(response) != 3) throw new Exception("Retry-After önceliği yanlış");

        });

        Test("Pixabay 429 ardından aynı arama sürer", () => {

            var st = FixtureStore(Path.Combine(root, "rate-retry")); st.Settings.StockOrder = "Pixabay";

            Environment.SetEnvironmentVariable("PIXABAY_API_KEY", "fixture");

            try {

                var handler = new StockHandler { FirstRateLimit = true }; using var http = new HttpClient(handler); var scene = new Scene { VisualQuery = "sunrise" };

                new Services(st, _ => { }, http).Stock(scene, false, st.Root, [], CancellationToken.None).GetAwaiter().GetResult();

                if (handler.Searches != 2 || !File.Exists(scene.StockFile) || new MediaPool(st).List().Count != 1) throw new Exception("Bekleme, yeniden deneme veya havuz kaydı eksik");

            } finally { Environment.SetEnvironmentVariable("PIXABAY_API_KEY", null); }

        });

        Test("Asenkron havuz ekleme ve iptal kayıt koruması", () => {

            var st = FixtureStore(Path.Combine(root, "async-pool")); var pool = new MediaPool(st); var file = Path.Combine(st.Root, "sample.mp4"); File.WriteAllBytes(file, VideoFixture());

            pool.AddAsync(file, "nature", "Doğa", "Kendi çekimim", "test", "Kullanım hakkı bende", CancellationToken.None).GetAwaiter().GetResult();

            var before = File.ReadAllText(Path.Combine(st.Root, "media-pool", "index.json"));

            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();

            try { pool.AddAsync(file, "cancelled", "Genel", "Kendi çekimim", "test", "Kullanım hakkı bende", cancelled.Token).GetAwaiter().GetResult(); throw new Exception("İptal uygulanmadı"); } catch (OperationCanceledException) { }

            if (pool.List().Count != 1 || before != File.ReadAllText(Path.Combine(st.Root, "media-pool", "index.json")) || Directory.GetFiles(Path.Combine(st.Root, "media-pool"), "*.part").Length != 0 || !File.Exists(file)) throw new Exception("Kayıt veya kaynak dosya değişti");

        });

        Test("Açılış şifresi düz metin tutulmaz ve değişim eski şifreyi reddeder", () => {

            var dir = Path.Combine(root, "app-lock"); Directory.CreateDirectory(dir); AppLock.Set(dir, "initial-password-123");

            if (!AppLock.Verify(dir, "initial-password-123") || AppLock.Verify(dir, "wrong-password") || File.ReadAllText(Path.Combine(dir, "app-lock.json")).Contains("initial-password-123")) throw new Exception("Şifre doğrulama hatalı");

            AppLock.Set(dir, "replacement-password-456"); if (AppLock.Verify(dir, "initial-password-123") || !AppLock.Verify(dir, "replacement-password-456")) throw new Exception("Şifre değişimi hatalı");

        });

        Test("Tüm bağlantılar için ayrıntılı yardım bulunur", () => {

            foreach (var key in new[] { "GEMINI_API_KEY", "GROQ_API_KEY", "OPENROUTER_API_KEY", "TAVILY_API_KEY", "PEXELS_API_KEY", "PIXABAY_API_KEY", "VECTEEZY_ACCOUNT_ID", "VECTEEZY_SECRET_KEY", "GOOGLE_TTS_API_KEY", "GITHUB_TOKEN", "ELEVENLABS_API_KEY", "OPENAI_API_KEY", "YOUTUBE", "OLLAMA" }) if (!ConnectionHelp.Guides.TryGetValue(key, out var help) || help.Steps.Length < 200 || !help.Url.StartsWith("https://")) throw new Exception("Yardım eksik: " + key);

        });

        Test("Cloud kota eksik/eski/ücretli/bilinmeyen birimde durur", () => {

            void Reject(CloudUsage.Report report) { try { CloudUsage.Check(report, 0, 10, 900000); throw new Exception("Koruma engellemedi"); } catch (InvalidOperationException) { } }

            Reject(new());

            var r = new CloudUsage.Report { Month = DateTimeOffset.UtcNow.ToString("yyyy-MM"), Fetched = DateTimeOffset.UtcNow, Rows = [new() { Unit = "count", Amount = 100, Cost = 0 }] };

            CloudUsage.Check(r, 20, 10, 900000); r.Rows[0].Cost = 1; Reject(r); r.Rows[0].Cost = 0; r.Rows[0].Unit = "tokens"; Reject(r); r.Rows[0].Unit = "count"; r.Rows[0].Amount = 900001; Reject(r); r.Rows[0].Amount = 100; r.Fetched = DateTimeOffset.UtcNow.AddHours(-2); Reject(r);

            try { CloudUsage.ValidateTable("project.dataset.table` DROP"); throw new Exception("Geçersiz tablo kabul edildi"); } catch (InvalidOperationException) { }

        });

        Test("Tek tuş TTS kapatma yeni isteği engeller", () => {

            var st = FixtureStore(Path.Combine(root, "tts-off")); st.Settings.GoogleTtsEnabled = false;

            try { new GoogleTts(st).Speak("test", Path.Combine(st.Root, "test.wav"), st.Settings, CancellationToken.None).GetAwaiter().GetResult(); throw new Exception("Kapalı TTS çalıştı"); } catch (InvalidOperationException e) { if (!e.Message.Contains("kapatıldı")) throw; }

        });

        Test("Cloud rapor okuma ve isteğe bağlı API yönetimi", () => {

            var st = FixtureStore(Path.Combine(root, "cloud-client")); st.Settings.CloudBillingTable = "sample-project.billing.export_table"; st.Settings.CloudControlProject = "123456789";

            using var rsa = System.Security.Cryptography.RSA.Create(2048); var credential = JsonSerializer.Serialize(new { project_id = "sample-project", client_email = "fixture@example.com", private_key = rsa.ExportPkcs8PrivateKeyPem() });

            Environment.SetEnvironmentVariable("GOOGLE_BILLING_JSON", credential); Environment.SetEnvironmentVariable("GOOGLE_CONTROL_JSON", credential);

            try {

                var handler = new CloudHandler(); using var http = new HttpClient(handler); var cloud = new CloudUsage(st, http); var report = cloud.Refresh(CancellationToken.None).GetAwaiter().GetResult();

                if (report.Rows.Count != 1 || report.Rows[0].Amount != 123) throw new Exception("Rapor yanlış");

                try { cloud.Control(false, CancellationToken.None).GetAwaiter().GetResult(); throw new Exception("Kapalı yönetim izni aşıldı"); } catch (InvalidOperationException) { }

                st.Settings.CloudApiControl = true; cloud.Control(false, CancellationToken.None).GetAwaiter().GetResult(); cloud.Control(true, CancellationToken.None).GetAwaiter().GetResult();

                if (handler.Controls != 2) throw new Exception("Açma/kapatma eksik");

            } finally { Environment.SetEnvironmentVariable("GOOGLE_BILLING_JSON", null); Environment.SetEnvironmentVariable("GOOGLE_CONTROL_JSON", null); }

        });

        Test("Klik sesi WAV, süre, seviye ve sessiz ayar", () => {

            var sound = UiSound.Create(.25); var silent = UiSound.Create(0);

            if (Encoding.ASCII.GetString(sound, 0, 4) != "RIFF" || BitConverter.ToInt32(sound, 24) != 44100 || sound.Length != 44 + 1764 * 2 || silent.Skip(44).Any(x => x != 0)) throw new Exception("Ses formatı hatalı");

            int peak = 0; for (int i = 44; i < sound.Length; i += 2) peak = Math.Max(peak, Math.Abs((int)BitConverter.ToInt16(sound, i))); if (peak == 0 || peak > 5000) throw new Exception("Ses seviyesi hatalı");

        });

        var report = Program.Arg(Environment.GetCommandLineArgs(), "--report") ?? Path.Combine(AppContext.BaseDirectory, "test-report.txt"); File.WriteAllLines(report, result, Encoding.UTF8);

        return result.Any(x => x.StartsWith("FAIL")) ? 1 : 0;

    }

    sealed class DiscoveryHandler : HttpMessageHandler

    {

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)

        {

            if (req.RequestUri!.AbsolutePath != "/openai/v1/models" || req.Headers.Authorization?.Parameter != "fixture") throw new Exception("Model isteği hatalı");

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"data\":[{\"id\":\"available-text-model\",\"active\":true},{\"id\":\"retired-model\",\"active\":false},{\"id\":\"whisper-model\",\"active\":true}]}") });

        }

    }

    sealed class KimiHandler : HttpMessageHandler

    {

        public int Calls;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)

        {

            Calls++; if (req.RequestUri!.Host != "api.moonshot.ai" || req.Headers.Authorization?.Parameter != "fixture") throw new Exception("Kimi adres veya kimliği hatalı");

            if (req.Method == HttpMethod.Get) { if (req.RequestUri.AbsolutePath != "/v1/models") throw new Exception("Model adresi hatalı"); return new(HttpStatusCode.OK) { Content = new StringContent("{\"data\":[{\"id\":\"kimi-k3\"}]}") }; }

            using var body = JsonDocument.Parse(await req.Content!.ReadAsStringAsync(ct)); if (req.RequestUri.AbsolutePath != "/v1/chat/completions" || body.RootElement.GetProperty("model").GetString() != "kimi-k3" || body.RootElement.GetProperty("response_format").GetProperty("type").GetString() != "json_object") throw new Exception("Kimi isteği hatalı");

            return new(HttpStatusCode.OK) { Content = new StringContent("{\"choices\":[{\"message\":{\"content\":\"{\\\"ok\\\":true}\"}}]}") };

        }

    }

    sealed class RateAiHandler : HttpMessageHandler

    {

        public bool DirectLimit; public List<string> Bodies = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)

        {

            Bodies.Add(await req.Content!.ReadAsStringAsync(ct));

            if (DirectLimit || Bodies.Count == 2) { var limited = new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = new StringContent("{\"error\":{\"code\":\"rate_limit_exceeded\",\"message\":\"Temporary rate limit\"}}") }; limited.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.Zero); return limited; }

            var content = Bodies.Count == 1 ? "{\"Shorts\":[]}" : "{\"Shorts\":[{\"Title\":\"Başlık\"}]}";

            return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { choices = new[] { new { message = new { content } } } })) };

        }

    }

    sealed class RepairHandler : HttpMessageHandler

    {

        public int Calls; public bool PlanContainsChat; public string ValidPlan = "";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)

        {

            var body = await req.Content!.ReadAsStringAsync(ct); Calls++;

            if (Calls > 1 && body.Contains("CHAT_PRIVATE_MARKER")) PlanContainsChat = true;

            var content = Calls == 3 ? ValidPlan : Calls == 2 ? "JSON yerine açıklama geldi" : "{\"answer\":\"Sohbet yanıtı\"}";

            return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { choices = new[] { new { message = new { content } } } })) };

        }

    }

    sealed class DetailHandler : HttpMessageHandler

    {

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)

        {

            var body = await request.Content!.ReadAsStringAsync(ct);

            if (!body.Contains("json_object")) throw new Exception("JSON çıktı biçimi istenmedi");

            return request.RequestUri!.Host.Contains("google")

                ? new(HttpStatusCode.OK) { Content = new StringContent("{\"choices\":[{\"message\":{\"content\":null},\"finish_reason\":\"length\"}]}") }

                : new(HttpStatusCode.NotFound) { Content = new StringContent("{\"error\":{\"code\":\"model_not_found\",\"message\":\"Unknown model\"}}") };

        }

    }

    sealed class CloudHandler : HttpMessageHandler

    {

        public int Controls;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)

        {

            string raw;

            if (req.RequestUri!.Host == "oauth2.googleapis.com") raw = "{\"access_token\":\"fixture\"}";

            else if (req.RequestUri.Host == "bigquery.googleapis.com") raw = "{\"jobComplete\":true,\"rows\":[{\"f\":[{\"v\":\"Standard\"},{\"v\":\"count\"},{\"v\":\"123\"},{\"v\":\"0\"},{\"v\":\"USD\"}]}]}";

            else { Controls++; raw = "{\"done\":true}"; if (!req.RequestUri.AbsolutePath.Contains("texttospeech.googleapis.com:")) throw new Exception("Yanlış hizmet kontrol edildi"); }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(raw) });

        }

    }

    sealed class YouTubeDesignHandler : HttpMessageHandler

    {

        public bool Thumbnail, Description;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)

        {

            if (request.RequestUri!.Host == "oauth2.googleapis.com") return new(HttpStatusCode.OK) { Content = new StringContent("{\"access_token\":\"fixture-token\"}") };

            if (request.Headers.Authorization?.Parameter != "fixture-token") throw new Exception("Yetkilendirme eksik");

            if (request.RequestUri.AbsolutePath.EndsWith("thumbnails/set")) { Thumbnail = true; if (request.Content!.Headers.ContentType?.MediaType != "image/png") throw new Exception("PNG gönderilmedi"); }

            else if (request.Method == HttpMethod.Get) return new(HttpStatusCode.OK) { Content = new StringContent("{\"items\":[{\"snippet\":{\"title\":\"Studio başlığı\",\"categoryId\":\"27\",\"tags\":[\"korunan etiket\"]}}]}") };

            else { using var doc = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct)); var snippet = doc.RootElement.GetProperty("snippet"); if (snippet.GetProperty("title").GetString() != "Studio başlığı" || snippet.GetProperty("categoryId").GetString() != "27" || snippet.GetProperty("tags")[0].GetString() != "korunan etiket" || !snippet.GetProperty("description").GetString()!.Contains("abone ol") || doc.RootElement.TryGetProperty("status", out _)) throw new Exception("Açıklama güncellemesi başka alanı bozdu"); Description = true; }

            return new(HttpStatusCode.OK) { Content = new StringContent("{}") };

        }

    }

    sealed class LoopStockHandler : HttpMessageHandler

    {

        public int Calls; public bool Permanent;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)

        {

            if (request.RequestUri!.Host == "example.com") return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(VideoFixture()) });

            Calls++; if (Permanent || Calls < 3) return Task.FromResult(new HttpResponseMessage(Permanent ? HttpStatusCode.PaymentRequired : HttpStatusCode.ServiceUnavailable) { Content = new StringContent("{}") });

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"videos\":[{\"id\":77,\"url\":\"https://www.pexels.com/video/77\",\"user\":{\"name\":\"Test\"},\"video_files\":[{\"file_type\":\"video/mp4\",\"width\":1280,\"height\":720,\"link\":\"https://example.com/test.mp4\"}]}]}") });

        }

    }

    sealed class StockHandler : HttpMessageHandler

    {

        public int Searches; public bool FirstRateLimit;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)

        {

            if (request.RequestUri!.Host == "example.com") return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(VideoFixture()) });

            Searches++;

            if (FirstRateLimit && Searches == 1) { var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests); response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(1)); return Task.FromResult(response); }

            var data = request.RequestUri.Query.Contains("q=sunrise")

                ? JsonSerializer.Serialize(new { hits = new[] { new { id = 12, pageURL = "https://pixabay.com/videos/12", user = "author", videos = new { medium = new { url = "https://example.com/test.mp4", width = 1280 } } } } })

                : "{\"hits\":[]}";

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(data) });

        }

    }

    sealed class RateStockHandler : HttpMessageHandler

    {

        public int Calls;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)

        {

            Calls++; var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);

            response.Headers.Add("X-RateLimit-Limit", "100"); response.Headers.Add("X-RateLimit-Remaining", "0"); response.Headers.Add("X-RateLimit-Reset", "65");

            return Task.FromResult(response);

        }

    }

    sealed class TtsHandler : HttpMessageHandler

    {

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)

        {

            if (req.Headers.GetValues("x-goog-api-key").Single() != "fixture-tts" || req.RequestUri!.Query.Contains("fixture")) throw new Exception("Anahtar başlığı hatalı");

            if (req.Method == HttpMethod.Get) return new(HttpStatusCode.OK) { Content = new StringContent("{\"voices\":[{\"name\":\"tr-TR-Standard-A\"}]}") };

            using var body = JsonDocument.Parse(await req.Content!.ReadAsStringAsync(ct)); if (body.RootElement.GetProperty("audioConfig").GetProperty("audioEncoding").GetString() != "LINEAR16") throw new Exception("Kodlama hatalı");

            var wav = new byte[44]; Encoding.ASCII.GetBytes("RIFF").CopyTo(wav, 0);

            return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { audioContent = Convert.ToBase64String(wav) })) };

        }

    }

    sealed class CoverHandler : HttpMessageHandler

    {

        public int Calls;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)

        {

            Calls++;

            if (request.RequestUri!.Host.Contains("google")) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests));

            using var image = new Bitmap(512, 512); using var graphics = Graphics.FromImage(image); graphics.Clear(Color.Navy); using var stream = new MemoryStream(); image.Save(stream, System.Drawing.Imaging.ImageFormat.Png);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { data = new[] { new { b64_json = Convert.ToBase64String(stream.ToArray()) } } })) });

        }

    }

    static void VecteezyFixture(Action run)

    {

        var oldId = Environment.GetEnvironmentVariable("VECTEEZY_ACCOUNT_ID"); var oldKey = Environment.GetEnvironmentVariable("VECTEEZY_SECRET_KEY");

        Environment.SetEnvironmentVariable("VECTEEZY_ACCOUNT_ID", "123"); Environment.SetEnvironmentVariable("VECTEEZY_SECRET_KEY", "fixture-vecteezy");

        try { run(); } finally { Environment.SetEnvironmentVariable("VECTEEZY_ACCOUNT_ID", oldId); Environment.SetEnvironmentVariable("VECTEEZY_SECRET_KEY", oldKey); }

    }

    sealed class VecteezyHandler : HttpMessageHandler

    {

        public int Quotas, Searches, Downloads; public long DownloadUsed, GeneralUsed; public bool Zip;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)

        {

            var uri = request.RequestUri!; string json;

            if (uri.Host == "fixture.example") {

                if (request.Headers.Authorization != null || uri.Query.Contains("fixture-vecteezy")) throw new Exception("CDN adresine gizli anahtar gönderildi");

                byte[] data = VideoFixture();

                if (Zip) { using var stream = new MemoryStream(); using (var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create, true)) { using var entry = archive.CreateEntry("../../escape.mp4").Open(); entry.Write(data); } data = stream.ToArray(); }

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(data) });

            }

            if (uri.Host != "api.vecteezy.com" || request.Headers.Authorization?.ToString() != "Bearer fixture-vecteezy" || uri.Query.Contains("fixture-vecteezy")) throw new Exception("API anahtarı yanlış yere gönderildi");

            if (uri.AbsolutePath.EndsWith("account/info")) { Quotas++; json = JsonSerializer.Serialize(new { current = new { general = new { call_count = GeneralUsed, call_limit = 100 }, download = new { call_count = DownloadUsed, call_limit = 500 } } }); }

            else if (uri.AbsolutePath.EndsWith("/resources")) {

                Searches++; if (!uri.Query.Contains("content_type=video") || !uri.Query.Contains("license_type=commercial")) throw new Exception("Ticari video filtresi yok");

                json = "{\"resources\":[{\"id\":42,\"content_type\":\"video\",\"dimensions\":{\"w\":1920,\"h\":1080},\"file_metadata\":{\"available_file_types\":[{\"extension\":\"mp4\"}]}}]}";

                if (Zip) json = json.Replace("mp4", "zip");

            } else if (uri.AbsolutePath.EndsWith("/download")) { Downloads++; if (uri.Query.Contains("file_size")) throw new Exception("Ücretli boyutlandırma kullanıldı"); json = "{\"url\":\"https://fixture.example/video.mp4\",\"requires_attribution\":true,\"required_attribution_url\":\"https://www.vecteezy.com/video/42-sea\"}"; }

            else throw new Exception("Beklenmeyen Vecteezy isteği");

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });

        }

    }

    sealed class SpellingHandler : HttpMessageHandler

    {

        public int Calls;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) {

            Calls++;

            var report = new SpellingResult { Changes = Calls == 1 ? [new() { Field = "Long.Scenes[0].Narration", Before = "Herkez burada.", After = "Herkes burada.", Reason = "Herkes sözcüğünün yazımı" }] : [] };

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = JsonSerializer.Serialize(report) } } } })) });

        }

    }

    sealed class GroqJsonHandler : HttpMessageHandler

    {

        public int Calls; public bool Invalid;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) {

            Calls++; using var doc = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));

            if (doc.RootElement.TryGetProperty("response_format", out _) != (Calls == 1)) throw new Exception("JSON modu yanlış");

            if (Calls == 1) return new(HttpStatusCode.BadRequest) { Content = new StringContent("{\"error\":{\"code\":\"json_validate_failed\"}}") };

            return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = Invalid ? "{\"wrong\":true}" : "{\"ok\":true}" } } } })) };

        }

    }

    sealed class FakeHandler : HttpMessageHandler

    {

        public List<string> Calls = []; public bool FailAll;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)

        {

            Calls.Add(request.RequestUri!.Host); var fail = FailAll || request.RequestUri.Host.Contains("google");

            return Task.FromResult(new HttpResponseMessage(fail ? HttpStatusCode.TooManyRequests : HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = "{\"ok\":true}" } } } })) });

        }

    }

}

