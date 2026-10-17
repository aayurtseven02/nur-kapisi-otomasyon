using System.Diagnostics;

using System.Drawing.Drawing2D;

using System.Globalization;

using System.Runtime.InteropServices;

using System.Security.Cryptography;

using System.Text;

using System.Text.Json;

using System.Text.RegularExpressions;



namespace AnkaStudio;



public static class Media

{

    public static async Task<string> Exec(string file, IEnumerable<string> args, string? cwd, CancellationToken ct)

    {

        var info = new ProcessStartInfo(file) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };

        if (cwd != null) info.WorkingDirectory = cwd;

        foreach (var arg in args) info.ArgumentList.Add(arg);

        using var p = Process.Start(info) ?? throw new InvalidOperationException("Video motoru başlatılamadı.");

        var stdout = p.StandardOutput.ReadToEndAsync(ct); var stderr = p.StandardError.ReadToEndAsync(ct);

        try { await p.WaitForExitAsync(ct); } catch { try { p.Kill(true); } catch { } throw; }

        var output = await stdout; var err = await stderr;

        if (p.ExitCode != 0) throw new InvalidOperationException("Video işlemi başarısız: " + err[..Math.Min(err.Length, 1800)]);

        return output;

    }

    public static async Task<double> Duration(Store store, string file, CancellationToken ct)

    {

        var raw = await Exec(store.Probe, ["-v", "error", "-show_entries", "format=duration", "-of", "default=noprint_wrappers=1:nokey=1", file], null, ct);

        if (!double.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var duration) || duration <= 0) throw new InvalidOperationException("Medya süresi okunamadı.");

        return duration;

    }

    public static string Num(double d) => d.ToString("0.###", CultureInfo.InvariantCulture);

    public static void Poster(string path, string title, bool vertical, string channel, bool demo)

    {

        var w = vertical ? 1080 : 1920; var h = vertical ? 1920 : 1080;

        using var bitmap = new Bitmap(w, h); using var g = Graphics.FromImage(bitmap);

        g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

        using var gradient = new LinearGradientBrush(new Rectangle(0, 0, w, h), Color.FromArgb(11, 20, 40), Color.FromArgb(39, 24, 66), 35f);

        g.FillRectangle(gradient, 0, 0, w, h);

        using var accent = new SolidBrush(Color.FromArgb(35, 94, 228, 210));

        g.FillEllipse(accent, w * 0.48f, h * 0.1f, w * 0.8f, w * 0.8f);

        using var line = new Pen(Color.FromArgb(50, 125, 151, 180), 2);

        for (int x = 0; x < w; x += 100) g.DrawLine(line, x, 0, x, h);

        using var white = new SolidBrush(Color.White); using var teal = new SolidBrush(Color.FromArgb(94, 228, 210));

        using var heading = new Font("Segoe UI", vertical ? 82 : 94, FontStyle.Bold, GraphicsUnit.Pixel);

        using var small = new Font("Segoe UI", 30, FontStyle.Bold, GraphicsUnit.Pixel);

        var top = vertical ? 500 : 290;

        g.DrawString(demo ? "TUBEFLOW / ÖRNEK" : channel.ToUpper(new CultureInfo("tr-TR")), small, teal, new RectangleF(90, top - 100, w - 180, 80));

        g.DrawString(title, heading, white, new RectangleF(85, top, w - 170, vertical ? 760 : 460));

        g.FillRectangle(teal, 90, h - 140, 160, 8);

        g.DrawString(vertical ? "SHORTS • TÜRKÇE" : "ÖZGÜN ANLATIM • TÜRKÇE", small, white, new PointF(90, h - 100));

        bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);

    }

}



public static class LocalVoice

{

    public static async Task SpeakWithFallback(string text, string path, Settings settings, CancellationToken ct)

    {

        if (settings.VoiceProvider.StartsWith("XTTS")) {
            var raw = path + ".xtts.wav";
            try {
                await new XttsTts().Speak(text, raw, settings, ct);
                var ffmpeg = string.IsNullOrWhiteSpace(settings.FfmpegPath) ? Path.Combine(AppContext.BaseDirectory, "tools", "ffmpeg.exe") : settings.FfmpegPath;
                await Media.Exec(ffmpeg, ["-y", "-i", raw, "-af", "atempo=" + Media.Num(settings.VoiceSpeed), "-ar", "44100", "-ac", "1", path], Path.GetDirectoryName(Path.GetFullPath(path))!, ct);
            } finally { if (File.Exists(raw)) File.Delete(raw); }
            return;
        }
        if (!settings.VoiceProvider.StartsWith("Piper") && List().Count > 0)

        {

            try { await Task.Run(() => Speak(text, path, settings, ct), ct); return; }

            catch (Exception ex) when (ex is not OperationCanceledException) { if (!File.Exists(settings.PiperPath) || !File.Exists(settings.PiperModel)) throw; }

        }

        if (!File.Exists(settings.PiperPath) || !File.Exists(settings.PiperModel)) throw new InvalidOperationException("Ücretsiz Türkçe ses için Windows konuşma sesini kur veya Yönetim ayarlarından Piper programını ve lisansını doğruladığın Türkçe ONNX modelini seç.");

        var info = new ProcessStartInfo(settings.PiperPath) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardInputEncoding = new UTF8Encoding(false) };

        foreach (var arg in new[] { "--model", settings.PiperModel, "--output_file", path, "--length_scale", Media.Num(1 / settings.VoiceSpeed) }) info.ArgumentList.Add(arg);

        using var p = Process.Start(info) ?? throw new InvalidOperationException("Piper başlatılamadı.");

        var output = p.StandardOutput.ReadToEndAsync(ct); var error = p.StandardError.ReadToEndAsync(ct); await p.StandardInput.WriteLineAsync(text.AsMemory(), ct); p.StandardInput.Close();

        try { await p.WaitForExitAsync(ct); } catch { try { p.Kill(true); } catch { } throw; }

        await output; var err = await error; if (p.ExitCode != 0 || !File.Exists(path)) throw new InvalidOperationException("Piper ses üretimi tamamlanamadı. Program / model / model.json eşleşmesini kontrol et. " + err[..Math.Min(300, err.Length)]);

    }

    public static List<(string Id, string Name)> List()

    {

        object? obj = null;

        try

        {

            dynamic speaker = obj = Activator.CreateInstance(Type.GetTypeFromProgID("SAPI.SpVoice")!)!;

            dynamic voices = speaker.GetVoices();

            var list = new List<(string, string)>();

            for (int i = 0; i < voices.Count; i++) { dynamic voice = voices.Item(i); var languages = ((string)voice.GetAttribute("Language")).Split(';'); if (languages.Any(l => l.Equals("41f", StringComparison.OrdinalIgnoreCase))) list.Add(((string)voice.Id, (string)voice.GetDescription())); Marshal.FinalReleaseComObject(voice); }

            Marshal.FinalReleaseComObject(voices); return list;

        }

        catch { return []; }

        finally { if (obj != null) Marshal.FinalReleaseComObject(obj); }

    }

    public static void Speak(string text, string path, Settings settings, CancellationToken ct = default)

    {

        object? speakerObj = null, streamObj = null;

        try

        {

            var voices = List(); if (voices.Count == 0) throw new InvalidOperationException("Windows'ta Türkçe ses bulunamadı. Windows Ayarları > Saat ve dil > Dil seçenekleri üzerinden Türkçe konuşma bileşenini yükle veya ses sağlayıcısını değiştir.");

            var voiceId = voices.Any(v => v.Id == settings.WindowsVoice) ? settings.WindowsVoice : voices[0].Id;

            dynamic speaker = speakerObj = Activator.CreateInstance(Type.GetTypeFromProgID("SAPI.SpVoice")!)!;

            dynamic token = Activator.CreateInstance(Type.GetTypeFromProgID("SAPI.SpObjectToken")!)!; token.SetId(voiceId); speaker.Voice = token;

            speaker.Rate = Math.Clamp((int)Math.Round((settings.VoiceSpeed - 1) * 10), -4, 4);

            dynamic stream = streamObj = Activator.CreateInstance(Type.GetTypeFromProgID("SAPI.SpFileStream")!)!;

            stream.Open(path, 3, false); speaker.AudioOutputStream = stream; speaker.Speak(text, 1);

            try { while (!speaker.WaitUntilDone(100)) { ct.ThrowIfCancellationRequested(); } } catch { speaker.Speak("", 3); throw; } finally { stream.Close(); }

            Marshal.FinalReleaseComObject(token);

        }

        finally { if (streamObj != null) Marshal.FinalReleaseComObject(streamObj); if (speakerObj != null) Marshal.FinalReleaseComObject(speakerObj); }

    }

}



public sealed class Engine(Store store, Action<string>? testFault = null)

{

    public event Action? Changed;
    public Func<string,string,CancellationToken,Task<bool>>? ReviewStoryImage { get; set; }

    CancellationTokenSource? cancel;

    public bool Busy => cancel != null || store.OperationBusy;

    public void Stop() { foreach (var episode in store.Episodes.Where(e => e.Status is "Araştırılıyor" or "Üretiliyor" or "Yükleniyor")) episode.RetryAt = null; cancel?.Cancel(); }

    public async Task External(Func<CancellationToken, Task> action) {

        using var operation = new OperationLease(store); cancel = new();

        try { await action(cancel.Token); } finally { cancel.Dispose(); cancel = null; operation.Dispose(); Notify(); }

    }

    public async Task StopAndWait() { Stop(); while (Busy) await Task.Delay(100); }

    public void Notify() { store.Save(); Changed?.Invoke(); }

    public static void ResetDeletedUploads(Episode episode)

    {

        if (episode.RemoteUploadJob != "" && episode.RemoteUploadState is not ("completed" or "failed")) throw new InvalidOperationException("Önce GitHub yüklemesini tamamla veya durdurup sonucunu kontrol et.");

        episode.RemoteUploadJob = ""; episode.RemoteUploadRepo = ""; episode.RemoteUploadState = ""; episode.RemoteUploadNote = ""; episode.RemoteUploadRelease = 0; episode.RemoteUploadAsset = 0;

        foreach (var asset in episode.Videos) { asset.YouTubeId = ""; asset.YouTubeState = ""; asset.YouTubeCheckedAt = null; asset.UploadSession = ""; asset.Scheduled = false; asset.ThumbnailUploaded = false; asset.ThumbnailAttempted = false; asset.ThumbnailNote = ""; }

        episode.Status = episode.Approved ? "Onaylandı" : "Kontrol gerekli"; episode.Error = "";

        episode.Log("YouTube'dan silinen videoların yerel bağlantıları sıfırlandı. Video, senaryo ve kapak dosyaları korundu.");

    }

    void Log(Episode e, string text) { ProductionStages.Observe(e,text); e.Log(SupportPrivacy.Clean(store,text)); Notify(); }

    public async Task Plan(Episode e, bool resume = false)

    {

        if (Busy) throw new InvalidOperationException("Önce çalışan işi tamamla veya durdur.");

        using var operation = new OperationLease(store);

        if (e.Videos.Any(v => v.YouTubeId != "")) throw new InvalidOperationException("Yüklenmiş paketin araştırması değiştirilemez. Yeni paket oluştur.");

        Rules.Validate(e); cancel = new(); var phase = "Plan";

        try

        {

            e.ScenesReviewed = false; e.Log("Sahne incelemesi sıfırlandı."); e.Status = "Araştırılıyor"; e.CurrentStage = ""; e.Error = ""; e.Progress = 8; e.Approved = false; Notify();

            var services = new Services(store, s => Log(e, s));

            if (!(resume && e.ResumeStage == "Proofread" && e.Plan != null)) {

                e.ResumeStage = "Plan";

                var nextPlan = UserNarration.Active(e) ? UserNarration.Plan(e) : e.MusicClip != null ? await MusicClips.Plan(store, e, cancel.Token) : e.Demo ? DemoPlan(e) : await services.CreatePlan(e, cancel.Token);

                ManualVideos.Apply(e, nextPlan);

                e.Plan = nextPlan; e.Videos.Clear(); e.ApprovalHash = ""; e.Revision++;

            }

            if (UserNarration.Active(e)) Log(e, "Hazır kullanıcı anlatımı korunuyor; kelime tamamlama ve otomatik yazım düzeltmesi kapalı.");

            phase = "Proofread"; e.ResumeStage = phase; e.CurrentStage = "İçerik oluşturuldu. Süre ve kelime hedefi kontrol ediliyor"; e.Progress = 14; Notify();

            await services.EnsureScriptLength(e, cancel.Token);
            e.Progress = 18; Notify();
            e.CurrentStage = "İçerik oluşturuldu. Türkçe yazım denetimi devam ediyor"; Notify();

            await services.Proofread(e, cancel.Token);
            e.Progress = 23; Notify();
            e.CurrentStage = "İçerik oluşturuldu. Sahne başlıkları ve son kontroller hazırlanıyor"; Notify();

            SeriesTitles.Apply(store, e);

            await services.PrepareSceneHeadings(e, cancel.Token);

            Rules.ValidatePlan(e);

            if (ScriptLength.Problem(e, e.Plan!) is string durationIssue) throw new InvalidOperationException("Senaryo süre kontrolü tamamlanmadı: " + durationIssue);

            Json.Write(Path.Combine(store.Folder(e), "plan.json"), e.Plan);

            e.Status = "Senaryo hazır"; e.Progress = 25; e.RetryAt = null; e.RetryAttempts = 0; e.ResumeStage = ""; Log(e, "Senaryo ve sahne planı hazır. Düzenleyebilir veya üretime geçebilirsin.");

        }

        catch (OperationCanceledException) { e.Status = "Durduruldu"; Log(e, "Araştırma durduruldu."); }

        catch (Exception ex) { Reliability.Failed(e, ex, phase); Log(e, ex.Message); }

        finally { cancel.Dispose(); cancel = null; operation.Dispose(); Notify(); }

    }

    public async Task Produce(Episode e)

    {

        if (Busy) throw new InvalidOperationException("Başka bir iş çalışıyor.");

        using var operation = new OperationLease(store);

        VideoSettings.Freeze(store.Settings,e);

        StorageMaintenance.RequireSpace(store,e);
        if(e.AiStoryImages)StoryImages.RequireReady(store,StoryImages.LocalSettings(e,VideoSettings.For(store.Settings,e)));

        Rules.Validate(e); if (e.Plan != null) ManualVideos.Apply(e, e.Plan); Rules.ValidatePlan(e); foreach(var (_,vertical,script) in Rules.Scripts(e)) { var artwork=VideoSettings.For(store.Settings,e,script); ClosingCards.Validate(artwork,vertical); if(artwork.CoverMode=="Kendi PNG kapak")MediaFormat.Image(artwork.CoverPng,vertical); }

        if (e.Videos.Any(v => v.YouTubeId != "")) throw new InvalidOperationException("Yüklenmiş paketi değiştirmek için yeni paket oluştur.");

        if (store.ProducedToday >= store.Settings.DailyProductionLimit) throw new InvalidOperationException("Günlük üretim sınırına ulaşıldı. Yönetim ayarlarından değiştirebilirsin.");

        _ = store.Ffmpeg;

        if (!File.Exists(store.Probe)) throw new InvalidOperationException("FFprobe dosyası FFmpeg ile aynı klasörde bulunmalı.");

        foreach (var (_, vertical, script) in Rules.Scripts(e)) {

            var musicSettings=VideoSettings.For(store.Settings,e,script);
            if (script.MusicTrack == null && e.Music) script.MusicTrack = GlobalMusic.Resolve(store, vertical,musicSettings);

            script.MusicTrack?.Validate();

            if (e.Music && script.MusicTrack?.Enabled == false) throw new InvalidOperationException("Paket müziği açık, fakat bu videonun müziği kapalı. Sahne editöründen müziği etkinleştir veya paket müziğini kapat.");

            if (script.MusicTrack?.Enabled == true && script.MusicTrack.Volume == 0) throw new InvalidOperationException("Müzik açık fakat ses seviyesi sıfır. Sahne editöründen ses seviyesini yükselt.");

            var path = script.MusicTrack == null ? (e.Music ? musicSettings.MusicPath : "") : (script.MusicTrack.Enabled ? script.MusicTrack.File : "");

            if (path != "" && !File.Exists(path) || script.MusicTrack == null && e.Music && path == "") throw new InvalidOperationException("Müzik seçili ama dosya yok. Editörden müzik seç veya müziği kapat.");

        }

        cancel = new(); var ct = cancel.Token;

        try

        {

            e.Status = "Üretiliyor"; e.ResumeStage = "Produce"; e.Error = ""; e.Approved = false; e.Progress = 28; Notify();

            var services = new Services(store, s => Log(e, s)); var folder = store.Folder(e);

            if (ManualVideos.Active(e)) {

                Log(e, "Manuel görüntü modu: yalnızca eklediğin videolar kullanılacak; otomatik stok araması kapalı.");

                foreach (var file in ManualVideos.AllFiles(e)) {

                    try { await Reliability.ValidateVideo(store, file, ct); }

                    catch (Exception ex) when (ex is not OperationCanceledException) { throw new InvalidOperationException("Seçtiğin video okunamadı: " + file + ". Otomatik arama yapılmadı. " + ex.Message, ex); }

                }

            }

            await MediaFormat.Manual(store,e,ct);

            foreach(var (_,vertical,script) in Rules.Scripts(e)) foreach(var scene in script.Scenes.Where(s=>s.UserLocal && !WorkflowMedia.ImageFile(s.StockFile))) await MediaFormat.Video(store,scene.StockFile,vertical,ct);

            await services.EnsureScriptLength(e, ct);

            await services.Proofread(e, ct);

            SeriesTitles.Apply(store, e);

            await services.PrepareSceneHeadings(e, ct);

            if (ScriptLength.Problem(e, e.Plan!) is string durationIssue) throw new InvalidOperationException("Senaryo süre kontrolü tamamlanmadı: " + durationIssue);

            var scripts = Rules.Scripts(e).ToList(); var used = new HashSet<long>();
            if(e.Creation!=null)await WorkflowMedia.PrepareVisuals(store,e,services,ct,Notify);
            if(e.AiStoryImages) {
                if(!e.AiStoryAutonomous && ReviewStoryImage==null)throw new InvalidOperationException("Hikâye görsellerinin onayı için uygulama penceresini aç veya proje ayarlarından durmadan üret seçeneğini aç.");
                await StoryImages.Plan(store,e,services,ct,s=>Log(e,s));
                foreach(var (_,vertical,script) in scripts) foreach(var scene in script.Scenes) {
                    ct.ThrowIfCancellationRequested();
                    await StoryImages.Prepare(store,e,script,scene,vertical,VideoSettings.For(store.Settings,e,script),ct,s=>Log(e,s),review:e.AiStoryAutonomous?null:ReviewStoryImage);
                    Notify();
                }
                Log(e,e.AiStoryAutonomous?"Hikâye görselleri hazır • video oluşturuluyor; bitince önizle ve onayla.":"Tüm hikâye görselleri onaylandı • video oluşturuluyor.");
            }

            foreach (var item in new MediaPool(store).List()) if (scripts.SelectMany(x => x.Script.Scenes).Any(scene => Path.GetFileName(scene.StockFile).StartsWith("pool-" + item.Id))) used.Add(item.Token);

            foreach (var scene in scripts.SelectMany(x => x.Script.Scenes)) if (long.TryParse(Regex.Match(Path.GetFileNameWithoutExtension(scene.StockFile), @"\d+").Value, out var id)) used.Add(Path.GetFileName(scene.StockFile).StartsWith("vecteezy-") ? Vecteezy.ResourceToken(id) : Path.GetFileName(scene.StockFile).StartsWith("pixabay-") ? -id : id);

            int complete = 0, shortIndex = 0;

            foreach (var (key, vertical, script) in scripts)

            {

                ct.ThrowIfCancellationRequested();

                var sub = Path.Combine(folder, $"r{e.Revision}-{key}"); Directory.CreateDirectory(sub);

                var hash = Reliability.RenderHash(script, e, store.Settings);

                var existing = e.Videos.FirstOrDefault(x => x.Key == key);

                if (File.Exists(Path.Combine(sub, "complete.hash")) && File.ReadAllText(Path.Combine(sub, "complete.hash")) == hash && existing != null && Reliability.AssetCurrent(e, existing))

                {

                Log(e, key + " tamamlanmış; yeniden üretim yapılmadan devam ediliyor.");

                    await Reliability.ValidateVideo(store, existing.File, ct, complete: true);

                    existing.PublishAt = Rules.VideoDate(e, vertical, shortIndex); if (vertical) shortIndex++; complete++; continue;

                }

                if (existing != null && !e.Demo) Log(e, key + " • eski video güncel ses eşleme sürümüne uymuyor; mevcut ses korunarak yeniden hazırlanıyor.");

                var staging = Path.Combine(sub, "attempt-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(staging);

                var result = await Render(e, script, key, vertical, staging, used, services, ct, fraction => { e.Progress = 28 + (int)(65 * (complete + fraction) / scripts.Count); Notify(); });

                WorkflowMedia.FreezeAudio(e,script,result,VideoSettings.For(store.Settings,e,script));
                result.PlanHash = Reliability.MediaPlanHash(e, key);

                result.PublishAt = Rules.VideoDate(e, vertical, shortIndex); if (vertical) shortIndex++;

                e.Videos.RemoveAll(x => x.Key == key); e.Videos.Add(result);

                // Rendering adds source metadata and measured durations; hash the final version.

                hash = Reliability.RenderHash(script, e, store.Settings);

                result.RenderFingerprint=hash; result.PipelineVersion=Reliability.PipelineVersion;

                File.WriteAllText(Path.Combine(sub, "complete.hash"), hash);

                complete++; e.Progress = 28 + 65 * complete / scripts.Count; Notify();

            }

            Json.Write(Path.Combine(folder, "plan.json"), e.Plan);

            foreach (var asset in e.Videos) { asset.PlanHash = Reliability.MediaPlanHash(e, asset.Key); var script=Rules.Scripts(e).Single(x=>x.Key==asset.Key).Script; asset.RenderFingerprint=Reliability.RenderHash(script,e,store.Settings); asset.PipelineVersion=Reliability.PipelineVersion; }

            e.TextDesignPending = false; e.ArtworkPending = false; e.RetryAt = null; e.RetryAttempts = 0; e.ResumeStage = ""; store.RecordProduction(e);

            Json.Write(Path.Combine(folder, "media-rights.json"), scripts.SelectMany(x => x.Script.Scenes).Where(x => x.StockUrl != "").Select(x => new { x.StockUrl, x.Creator, x.LicenseUrl, x.StockFile, x.Attribution, retrievedAt = DateTimeOffset.UtcNow }));

            e.Status = e.Videos.All(v => v.QualityPassed) ? "Denetim bekliyor" : "Kontrol gerekli"; e.Progress = 100;

            Log(e, "PAKET TAMAMLANDI " + DateTimeOffset.UtcNow.ToString("yyyy-MM-dd") + ": normal video ve Shorts çıktıları hazır.");

        }

        catch (OperationCanceledException) { e.Status = "Durduruldu"; Log(e, "Üretim durduruldu. Tamamlanmış video ve sesler saklandı."); }

        catch (Exception ex) { Reliability.Failed(e, ex, "Produce"); Log(e, ex.Message); }

        finally { cancel.Dispose(); cancel = null; operation.Dispose(); Notify(); }

    }



    async Task<AssetVideo> Render(Episode e, VideoScript script, string key, bool vertical, string folder, HashSet<long> used, Services services, CancellationToken ct, Action<double>? report = null)

    {

        var renderSettings = VideoSettings.For(store.Settings,e,script);

        if (vertical && renderSettings.ShortVoiceOverride && renderSettings.ShortVoiceProvider != "Normal videoyla aynı") { renderSettings.VoiceProvider = renderSettings.ShortVoiceProvider; renderSettings.VoiceId = renderSettings.ShortVoiceId; renderSettings.VoiceSpeed = renderSettings.ShortVoiceSpeed; renderSettings.MusicVolume = renderSettings.ShortMusicVolume; }

        bool musicClip = e.MusicClip != null;

        Log(e, musicClip ? "Müzik klibi: seçilen parça ana ses; seslendirme "+(e.MusicClip!.NarrationEnabled?"açık":"kapalı")+", altyazı "+(e.MusicClip.SubtitlesEnabled?"açık":"kapalı")+"." : $"{(vertical ? "Shorts" : "Normal video")} ses sağlayıcısı: {renderSettings.VoiceProvider}.");

        if(e.Creation is {Voice:"ai"})e.Log("Seçili anlatıcı: "+(renderSettings.VoiceProvider.StartsWith("Google")?renderSettings.GoogleTtsVoice:renderSettings.VoiceProvider.StartsWith("ElevenLabs")?renderSettings.VoiceId:renderSettings.VoiceProvider.StartsWith("Piper")?Path.GetFileName(renderSettings.PiperModel):renderSettings.WindowsVoice));
        var w = vertical ? 1080 : 1920; var h = vertical ? 1920 : 1080;

        var clips = new List<string>(); var captions = new List<Caption>(); double offset = 0;

        var fontSize = vertical ? renderSettings.ShortFontSize : renderSettings.LongFontSize;

        var requestedDurations=e.Creation?.BeforeRender.Count>0?script.Scenes.Select(s=>s.Duration).ToArray():null;
        if (musicClip) await MusicClips.PrepareAudio(store, e, script, folder, ct,renderSettings,services);

        else if(!e.Demo && WorkflowMedia.CanReuseAudio(e,script,VideoSettings.For(store.Settings,e,script)))await WorkflowMedia.PrepareFrozen(store,e,script,folder,ct);
        else if(!e.Demo && e.Creation?.Voice=="file")await WorkflowMedia.PrepareImportedAudio(store,e,script,folder,ct);
        else if (!e.Demo) { await NarrationFlow.Reuse(store, e, script, renderSettings, folder, key, ct); Log(e, "Ses ve altyazı gerçek konuşma zamanlarıyla hazırlanıyor; metin uzunluğuna göre tahmini bölme kullanılmıyor."); await NarrationFlow.Prepare(store, e, script, renderSettings, folder, services, ct); }

        if(!e.Demo&&requestedDurations!=null)await WorkflowMedia.Recut(store,script,folder,requestedDurations,ct);
        report?.Invoke(.25);

        int n = 0;

        foreach (var scene in script.Scenes)

        {

            ct.ThrowIfCancellationRequested(); n++;

            Log(e, $"{(vertical ? "Shorts" : "Normal video")} • sahne {n}/{script.Scenes.Count}: ses ve görüntü hazırlanıyor.");

            var sceneHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(scene.Narration + renderSettings.VoiceProvider + renderSettings.VoiceId + renderSettings.VoiceModel + renderSettings.WindowsVoice + renderSettings.VoiceSpeed + renderSettings.PiperPath + renderSettings.PiperModel + renderSettings.XttsUrl + renderSettings.XttsSpeaker + renderSettings.GoogleTtsVoice + e.Demo)));

            var audioFile = Path.Combine(folder, $"speech-{n}.wav"); var dataFile = Path.Combine(folder, $"speech-{n}.json");

            List<Caption> segmentCaptions;

            if (!e.Demo) segmentCaptions = Json.Read(dataFile, new List<Caption>());

            else if (File.Exists(audioFile) && File.Exists(dataFile) && File.Exists(dataFile + ".hash") && File.ReadAllText(dataFile + ".hash") == sceneHash)

                segmentCaptions = Json.Read(dataFile, new List<Caption>());

            else

            {

                if (e.Demo)

                {

                    // A clearly labelled silent sample tests render geometry without API keys.

                    var seconds = Math.Clamp(scene.Narration.Split(' ').Length / 2.2, 3, 8);

                    await Media.Exec(store.Ffmpeg, ["-y", "-f", "lavfi", "-i", "anullsrc=r=44100:cl=mono", "-t", Media.Num(seconds), audioFile], folder, ct);

                    segmentCaptions = TimedText(scene.Narration, seconds);

                }

                else if (!renderSettings.VoiceProvider.StartsWith("ElevenLabs"))

                {

                    // Speak small chunks separately; subtitle boundaries use measured audio lengths.

                    var chunks = SplitText(scene.Narration, vertical ? 42 : 70); var speechParts = new List<string>(); segmentCaptions = []; double start = 0;

                    int j = 0;

                    foreach (var chunk in chunks)

                    {

                        ct.ThrowIfCancellationRequested(); var part = Path.Combine(folder, $"voice-{n}-{++j}.wav");

                        if (renderSettings.VoiceProvider.StartsWith("Google"))

                        {

                            try

                            {

                                var raw = part + ".google.wav";

                                await new GoogleTts(store).Speak(chunk, raw, renderSettings, ct);

                                await Media.Exec(store.Ffmpeg, ["-y", "-i", raw, "-af", "atempo=" + Media.Num(renderSettings.VoiceSpeed), "-ar", "44100", "-ac", "1", part], folder, ct);

                            }

                            catch (Exception ex) when (ex is not OperationCanceledException && renderSettings.GoogleTtsFallback)

                            {

                                e.Log("Google TTS kullanılamadı; yerel Türkçe ses deneniyor: " + ex.Message);

                                var local = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(renderSettings, Json.Options), Json.Options)!;

                                local.VoiceProvider = "Windows / ücretsiz";

                                try { await LocalVoice.SpeakWithFallback(chunk, part, local, ct); }

                                catch (Exception localError) when (localError is not OperationCanceledException) { throw new InvalidOperationException("Google TTS hatası: " + ex.Message + "\nYedek ses de kullanılamadı: " + localError.Message, localError); }

                            }

                        }

                        else {

                            try { await LocalVoice.SpeakWithFallback(chunk, part, renderSettings, ct); }

                            catch (Exception localError) when (localError is not OperationCanceledException) { throw new InvalidOperationException($"{(vertical ? "Shorts" : "Normal video")} için seçili sağlayıcı: {renderSettings.VoiceProvider}. " + (vertical ? "Yönetim ayarları → Ses / müzik → Shorts için normal video sesini kullan seçeneğini kullanabilirsin. " : "") + localError.Message, localError); }

                        }

                        var dur = await Media.Duration(store, part, ct); segmentCaptions.Add(new(start, start + dur, chunk)); start += dur; speechParts.Add(part);

                    }

                    File.WriteAllText(Path.Combine(folder, "voice-list.txt"), string.Join("\n", speechParts.Select(f => "file '" + Path.GetFileName(f) + "'")), new UTF8Encoding(false));

                    await Media.Exec(store.Ffmpeg, ["-y", "-f", "concat", "-safe", "1", "-i", "voice-list.txt", "-c:a", "pcm_s16le", audioFile], folder, ct);

                }

                else

                {

                    var speech = await services.Speech(scene.Narration, ct, renderSettings);

                    var mp3 = Path.Combine(folder, $"speech-{n}.mp3"); await File.WriteAllBytesAsync(mp3, speech.Audio, ct);

                    await Media.Exec(store.Ffmpeg, ["-y", "-i", mp3, "-ar", "44100", "-ac", "1", audioFile], folder, ct);

                    segmentCaptions = speech.Captions;

                }

                Json.Write(dataFile, segmentCaptions); File.WriteAllText(dataFile + ".hash", sceneHash);

            }

            var duration = await Media.Duration(store, audioFile, ct); scene.Duration = duration;

            var source = scene.StockFile;

            if (e.Demo) { source = Path.Combine(folder, $"scene-{n}.png"); Media.Poster(source, scene.OnScreen == "" ? script.ThumbnailText : scene.OnScreen, vertical, renderSettings.ChannelName, true); }

            else if(e.PhotosOnly || e.AiStoryImages || WorkflowMedia.ImageFile(source)) {
                if(e.AiStoryImages) {
                    await StoryImages.Prepare(store,e,script,scene,vertical,renderSettings,ct,s=>Log(e,s),review:e.AiStoryAutonomous?null:ReviewStoryImage);source=scene.StockFile;Notify();
                }
                else if(!File.Exists(source) || !new[]{".jpg",".jpeg",".png",".webp",".bmp"}.Contains(Path.GetExtension(source).ToLowerInvariant())) {
                    await services.StockPhoto(scene,vertical,folder,used,ct);source=scene.StockFile;Notify();
                }
                using(var input=File.OpenRead(source))using(var image=System.Drawing.Image.FromStream(input))if((long)image.Width*image.Height>40_000_000)throw new InvalidOperationException("Sahne fotoğrafı çok büyük.");
                var photoClip=Path.Combine(folder,$"photo-scene-{n}.mp4");
                if((e.AiStoryImages || e.Creation is {CoverChoice:not "legacy",PhotoMotion:true}&&scene.AiImageHash!="") && e.AiStoryMotion && e.AiStoryCinematic)
                    photoClip=await StoryCinema.Render(store,source,scene,w,h,duration,renderSettings,folder,ct,s=>Log(e,s),segmentCaptions);
                else await Media.Exec(store.Ffmpeg,["-y","-loop","1","-i",source,"-t",Media.Num(duration),"-vf",e.Creation is {CoverChoice:not "legacy"} layout ? WorkflowMedia.PhotoFilter(w,h,renderSettings.FramesPerSecond,layout) : StoryImages.PhotoFilter(w,h,renderSettings.FramesPerSecond,e.AiStoryImages && e.AiStoryMotion),"-an","-c:v","libx264","-preset",renderSettings.EncodingPreset,"-crf",renderSettings.Crf.ToString(),"-pix_fmt","yuv420p",photoClip],folder,ct);
                source=photoClip;
            }
            else if (ManualVideos.Active(e)) {

                if (!scene.UserLocal || !ManualVideos.Files(e, script).Contains(source, StringComparer.OrdinalIgnoreCase) || !File.Exists(source)) throw new InvalidOperationException("Manuel sahne videosu bulunamadı veya seçili listede değil: " + source + ". Otomatik arama yapılmadı.");

                await Reliability.ValidateVideo(store, source, ct);

            }

            else {

                if (StockSearch.Automatic(scene) && File.Exists(source) && (scene.StockSelectionVersion != StockSearch.SelectionVersion || !StockSearch.Appropriate(StockSearch.ReviewQuery(scene), scene.StockMetadata + " " + scene.StockUrl))) { Log(e, "Eski otomatik stok seçimi yeniden konuya göre denetleniyor."); scene.StockFile = ""; scene.RightsConfirmed = false; source = ""; }

                if (StockSearch.Automatic(scene) && File.Exists(source) && !await services.ReviewStock(scene, ct)) { scene.StockFile = ""; scene.RightsConfirmed = false; source = ""; }

                if (File.Exists(source)) { try { await Reliability.ValidateVideo(store, source, ct); } catch (Exception ex) when (ex is not OperationCanceledException) { Log(e, "Geçersiz sahne videosu atlandı; alternatif aranıyor."); scene.StockFile = ""; scene.RightsConfirmed = false; source = ""; } }

                if (!File.Exists(source)) { await services.Stock(scene, vertical, folder, used, ct); source = scene.StockFile; Notify(); }

            }

            var clip = Path.Combine(folder, $"clip-{n}.mp4");

            var filter = e.Creation is {CoverChoice:not "legacy"} workflow ? WorkflowMedia.FitFilter(w,h,renderSettings.FramesPerSecond,workflow.Fit) : $"scale={w}:{h}:force_original_aspect_ratio=increase,crop={w}:{h},setsar=1,fps={renderSettings.FramesPerSecond}";

            if (!musicClip && !e.Demo && script.TextLayout == null && scene.OnScreen != "")

            {

                await File.WriteAllTextAsync(Path.Combine(folder, $"overlay-{n}.txt"), scene.OnScreen, new UTF8Encoding(false), ct);

                filter += $",drawtext=font='Segoe UI':textfile=overlay-{n}.txt:expansion=none:fontsize={(vertical ? 48 : 40)}:fontcolor=white:box=1:boxcolor=black@0.45:boxborderw=18:x=70:y={(vertical ? 200 : 75)}:enable='lt(t,4)'";

            }

            var args = new List<string> { "-y" }; if (e.Demo) args.AddRange(["-loop", "1"]); else args.AddRange(["-stream_loop", "-1"]);

            args.AddRange(["-i", source, "-i", audioFile, "-t", Media.Num(duration), "-vf", filter, "-map", "0:v:0", "-map", "1:a:0", "-c:v", "libx264", "-preset", renderSettings.EncodingPreset, "-crf", renderSettings.Crf.ToString(), "-pix_fmt", "yuv420p", "-c:a", "aac", "-ar", "48000", "-ac", "2", "-movflags", "+faststart", clip]);

            var clipCache = Path.Combine(Path.GetDirectoryName(folder)!, "clip-cache"); Directory.CreateDirectory(clipCache);

            var clipHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { filter, overlayText = scene.OnScreen, duration, w, h, renderSettings.Crf, renderSettings.EncodingPreset, video = Reliability.FileHash(source), audio = Reliability.FileHash(audioFile) }))));

            var cachedClip = Path.Combine(clipCache, clipHash + ".mp4");

            if (File.Exists(cachedClip) && File.Exists(cachedClip + ".sha") && File.ReadAllText(cachedClip + ".sha") == Reliability.FileHash(cachedClip)) { await Reliability.ValidateVideo(store, cachedClip, ct); File.Copy(cachedClip, clip, true); Log(e, "Sağlam sahne çıktısı yeniden kullanıldı: " + n); }

            else { await Media.Exec(store.Ffmpeg, args, folder, ct); await Reliability.ValidateVideo(store, clip, ct); File.Copy(clip, cachedClip, true); File.WriteAllText(cachedClip + ".sha", Reliability.FileHash(cachedClip)); }

            if (!musicClip && scene.SubtitleText != "") segmentCaptions = TimedNarration.Redisplay(segmentCaptions, scene.SubtitleText);

            captions.AddRange(segmentCaptions.Select(c => new Caption(offset + c.Start, offset + Math.Min(c.End, duration), c.Text))); offset += duration; clips.Add(clip); report?.Invoke(.25 + .45 * n / script.Scenes.Count);

        }

        File.WriteAllText(Path.Combine(folder, "clips.txt"), string.Join("\n", clips.Select(f => "file '" + Path.GetFileName(f) + "'")), new UTF8Encoding(false));

        await Media.Exec(store.Ffmpeg, ["-y", "-f", "concat", "-safe", "1", "-i", "clips.txt", "-c", "copy", "joined.mp4"], folder, ct);

        if (script.TextLayout != null && script.TextLayout.Transition != "Kesme") {

            Log(e, "Sahne geçişleri uygulanıyor: " + script.TextLayout.Transition + " • anlatım zamanları korunuyor.");

            await SceneTransitions.Apply(store, clips, script.TextLayout, renderSettings.FramesPerSecond, folder, Path.Combine(folder, "joined.mp4"), ct);

        }

        await File.WriteAllTextAsync(Path.Combine(folder, "subtitles.srt"), Captions.Srt(captions), new UTF8Encoding(false), ct);

        await File.WriteAllTextAsync(Path.Combine(folder, "subtitles.ass"), musicClip ? MusicClips.Ass(e, script, w, h, offset,captions) : script.TextLayout == null ? Captions.Ass(captions, w, h, fontSize, renderSettings.SubtitleColor, vertical) : TextDesign.ForVideo(captions, script, w, h), new UTF8Encoding(false), ct);

        var vf = "subtitles=subtitles.ass";

        var finalArgs = new List<string> { "-y", "-i", "joined.mp4" };

        if (!e.Demo) finalArgs.AddRange(["-i", "narration-full.wav"]);

        if (!musicClip && script.MusicTrack?.Enabled == true)

        {

            var voiceInput = e.Demo ? "0" : "1"; var musicInput = e.Demo ? "1" : "2";

            Log(e, "Fon müziği videoya karıştırılıyor: " + Path.GetFileName(script.MusicTrack.File) + " • seviye %" + Math.Round(script.MusicTrack.Volume * 100) + (script.MusicTrack.Duck ? " • konuşmada dengeli kısılır" : ""));

            double sourceOffset = script.MusicTrack.SourceOffset % await Media.Duration(store, script.MusicTrack.File, ct);

            finalArgs.AddRange(["-stream_loop", "-1", "-ss", Media.Num(sourceOffset), "-i", script.MusicTrack.File, "-filter_complex", EditorAudio.Mix(script.MusicTrack, offset, voiceInput, musicInput, sourceSeeked: true), "-map", "0:v", "-map", "[a]"]);

        }

        else if (!musicClip && script.MusicTrack == null && e.Music)

        {

            var voiceInput = e.Demo ? "0" : "1"; var musicInput = e.Demo ? "1" : "2";

            finalArgs.AddRange(["-stream_loop", "-1", "-i", renderSettings.MusicPath, "-filter_complex", $"[{voiceInput}:a]asplit=2[voice][side];[{musicInput}:a]volume={Media.Num(renderSettings.MusicVolume)}[music];[music][side]sidechaincompress=threshold=0.02:ratio=8:attack=20:release=400[duck];[voice][duck]amix=inputs=2:duration=first:normalize=0,loudnorm=I=-16:TP=-1.5:LRA=11[a]", "-map", "0:v", "-map", "[a]"]);

        }

        else { if (!e.Demo) finalArgs.AddRange(["-map", "0:v", "-map", "1:a"]); }

        var output = Path.Combine(folder, "video.mp4");

        finalArgs.AddRange(["-vf", vf, "-c:v", "libx264", "-preset", renderSettings.EncodingPreset, "-crf", renderSettings.Crf.ToString(), "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "192k", "-t", Media.Num(offset), "-movflags", "+faststart", output]);

        await Media.Exec(store.Ffmpeg, finalArgs, folder, ct); report?.Invoke(.82);

        if (renderSettings.VideoLogoEnabled) {

            if (!File.Exists(renderSettings.VideoLogo)) throw new InvalidOperationException("Video logosu bulunamadı. Kapak tasarımları → Kanal logoları bölümünden yeniden seç.");

            using (var logoCheck = System.Drawing.Image.FromFile(renderSettings.VideoLogo)) if ((long)logoCheck.Width * logoCheck.Height > 40_000_000) throw new InvalidOperationException("Logo boyutu çok büyük.");

            var logoWidth = w * Math.Clamp(renderSettings.VideoLogoWidthPercent, 5, 20) / 100;

            var logoX = renderSettings.VideoLogoPosition.StartsWith("Sol") ? "32" : "W-w-32";

            var logoY = renderSettings.VideoLogoPosition.EndsWith("alt") ? "H-h-48" : "48";

            var brandedOutput = Path.Combine(folder, "video-logo.mp4");

            await Media.Exec(store.Ffmpeg, ["-y", "-i", output, "-i", renderSettings.VideoLogo, "-filter_complex", $"[1:v]scale={logoWidth}:-1,format=rgba[logo];[0:v][logo]overlay=x={logoX}:y={logoY}:eof_action=repeat:format=auto[v]", "-map", "[v]", "-map", "0:a", "-c:v", "libx264", "-preset", renderSettings.EncodingPreset, "-crf", renderSettings.Crf.ToString(), "-pix_fmt", "yuv420p", "-c:a", "copy", "-movflags", "+faststart", brandedOutput], folder, ct);

            File.Move(brandedOutput, output, true);

        }

        if (renderSettings.OpeningCardEnabled) {

            var opening = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(renderSettings))!; opening.ClosingCardFile = renderSettings.OpeningCardFile; opening.ClosingCardSeconds = renderSettings.OpeningCardSeconds;

            Log(e, "Seçilen açılış kartı videonun başına ekleniyor.");

            await ClosingCards.Append(store, opening, output, folder, w, h, ct, prepend: true);

            await File.WriteAllTextAsync(Path.Combine(folder, "subtitles.srt"), Captions.Srt(captions.Select(c => new Caption(c.Start + opening.ClosingCardSeconds, c.End + opening.ClosingCardSeconds, c.Text)).ToList()), new UTF8Encoding(false), ct);

        }

        if (renderSettings.ClosingCardEnabled) {

            Log(e, "Seçilen kapanış kartı videonun sonuna ekleniyor.");

            await ClosingCards.Append(store, renderSettings, output, folder, w, h, ct);

        }

        report?.Invoke(.9); await Reliability.ValidateVideo(store, output, ct, complete: true);

        testFault?.Invoke("encoded");

        Log(e, "Video kartları: açılış " + (renderSettings.OpeningCardEnabled ? "eklendi" : "kapalı / eklenmedi") + ", kapanış " + (renderSettings.ClosingCardEnabled ? "eklendi" : "kapalı / eklenmedi") + ". YouTube küçük resmi ayrı dosyadır.");

        var thumb = Path.Combine(folder, "thumbnail.png");

        var coverSettings = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(renderSettings))!;

        if (coverSettings.CoverAutoStyle) coverSettings.CoverStyle = CoverDesigner.Suggest(e.Topic + " " + script.Title);

        var priorCover = e.TextDesignPending && !e.ArtworkPending ? e.Videos.FirstOrDefault(v => v.Key == key)?.Thumbnail : null;

        if(e.Creation?.CoverChoice=="none")await Media.Exec(store.Ffmpeg,["-y","-i",output,"-frames:v","1",thumb],folder,ct);
        else if (priorCover != null && File.Exists(priorCover) && Path.GetFullPath(priorCover) != Path.GetFullPath(thumb)) File.Copy(priorCover, thumb, true);

        else await new CoverFallback(store, message => e.Log(message)).Create(thumb, coverSettings, e.Topic, script.ThumbnailText == "" ? script.Title : script.ThumbnailText, vertical, ct);

        report?.Invoke(.95); var asset = new AssetVideo { NarrationFile=File.Exists(Path.Combine(folder,"narration-full.wav"))?Path.Combine(folder,"narration-full.wav"):"", NarrationTimingFile=File.Exists(Path.Combine(folder,"narration-timing.json"))?Path.Combine(folder,"narration-timing.json"):"", Key = key, Short = vertical, Title = script.Title, File = output, Thumbnail = thumb, Duration = await Media.Duration(store, output, ct) };

        var target = (musicClip ? e.MusicClip!.Duration : UserNarration.Active(e) ? await Media.Duration(store, Path.Combine(folder, "narration-full.wav"), ct) : vertical ? e.ShortSeconds : e.LongMinutes * 60) + (renderSettings.ClosingCardEnabled ? renderSettings.ClosingCardSeconds : 0) + (renderSettings.OpeningCardEnabled ? renderSettings.OpeningCardSeconds : 0); var tolerance = DurationStandard.Tolerance(target);

        asset.Checks.Add($"Görüntü: {w}×{h} / {renderSettings.FramesPerSecond} fps"); asset.Checks.Add("Ses akışı ve dosya süresi okunabilir"); asset.Checks.Add(musicClip ? "Şarkı sözü altyazısı ve yapay seslendirme kapalı; parça bilgileri ayrı gösterilir" : $"Altyazı: {captions.Count} zamanlanmış bölüm"); asset.Checks.Add("Kapak ve kaynak dosyaları hazır");

        var durationOk = Math.Abs(asset.Duration - target) <= tolerance;

        asset.Checks.Add(durationOk ? "Süre hedef aralığında" : $"Süre kontrolü: hedef {target} sn, çıktı {asset.Duration:0} sn. Senaryoyu düzenle.");

        if (UserNarration.Active(e) && !vertical && asset.Duration > 1800) { durationOk = false; asset.Checks.Add("Hazır metnin ses süresi 30 dakikayı aşıyor; metni ayrı videolara böl."); }

        if (vertical && asset.Duration > 180) { durationOk = false; asset.Checks.Add("Shorts 180 saniyeyi aşıyor; kısaltılmalı."); }

        if (e.Demo) asset.Checks.Add("ÖRNEK: sessiz grafik video, stok görüntü ve gerçek ses içermez; YouTube yükleme kapalı.");

        var probe = await Media.Exec(store.Probe, ["-v", "error", "-show_streams", "-of", "json", output], folder, ct);

        using var probeDoc = JsonDocument.Parse(probe);

        var streams = probeDoc.RootElement.GetProperty("streams").EnumerateArray().ToList();

        var validStreams = streams.Any(s => s.GetProperty("codec_type").GetString() == "audio") && streams.Any(s => s.GetProperty("codec_type").GetString() == "video" && s.GetProperty("width").GetInt32() == w && s.GetProperty("height").GetInt32() == h);

        asset.QualityPassed = validStreams && durationOk && !e.Demo && (musicClip || script.TextLayout?.SubtitleEnabled==false || captions.Count > 0) && script.Scenes.All(s => File.Exists(s.StockFile) && (s.LicenseUrl != "" || e.Creation!=null && s.UserLocal || e.AiStoryImages && s.AiImageHash!="" && File.Exists(s.StockFile+".json") && File.Exists(s.StockFile+".sha") && Reliability.FileHash(s.StockFile)==File.ReadAllText(s.StockFile+".sha")));

        await using (var hashStream = File.OpenRead(output)) asset.FileHash = Convert.ToHexString(await SHA256.HashDataAsync(hashStream, ct));

        var description = YouTube.BuildDescription(script, e.Plan!.Sources,store.Settings.IncludeResearchSources);

        Json.Write(Path.Combine(folder, "youtube-metadata.json"), new { title = script.Title, description, tags = script.Tags, defaultLanguage = "tr", defaultAudioLanguage = "tr", privacyStatus = "private", checks = asset.Checks });

        await File.WriteAllTextAsync(Path.Combine(folder, "script.txt"), string.Join("\n\n", script.Scenes.Select(s => s.Narration)), ct);

        return asset;

    }

    public static List<string> SplitText(string text, int length)

    {

        var result = new List<string>(); var current = new StringBuilder();

        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries)) { if (current.Length > 0 && current.Length + word.Length + 1 > length && !System.Text.RegularExpressions.Regex.IsMatch(current.ToString(), @"(?i)(?:^|\s)hz\.?$")) { result.Add(current.ToString()); current.Clear(); } if (current.Length > 0) current.Append(' '); current.Append(word); }

        if (current.Length > 0) result.Add(current.ToString()); return result;

    }

    static List<Caption> TimedText(string text, double duration)

    {

        var chunks = SplitText(text, 42); return chunks.Select((x, i) => new Caption(i * duration / chunks.Count, (i + 1) * duration / chunks.Count, x)).ToList();

    }

    public static bool NeedsSchedule(AssetVideo asset) => asset.YouTubeState != "Yayında" && (!asset.Scheduled || asset.RemotePublishAt != asset.PublishAt);

    public static void VerifyChannel(string approved, string configured, string actual) { if (approved == "" || actual == "" || actual != approved || actual != configured) throw new InvalidOperationException("Bağlı kanal onaylanan hedefle uyuşmuyor. Kanalı doğrula ve yeniden onayla; yükleme yapılmadı."); }

    public void Approve(Episode e, bool planned = false)

    {

        if (e.TextDesignPending) throw new InvalidOperationException("Kaydedilen yazı düzeni henüz videoya uygulanmadı. Videoları üret adımını tamamla.");

        if (Busy) throw new InvalidOperationException("Çalışan iş tamamlanmadan onay verilemez.");

        if (e.Videos.Any(v => !v.QualityPassed)) {

            var issues = e.Videos.Where(v => !v.QualityPassed).Select(v => v.Title + ": " + string.Join("; ", v.Checks.Where(c => c.Contains("Süre kontrolü") || c.Contains("aşıyor") || c.Contains("ÖRNEK"))));

            throw new InvalidOperationException("Video kalite kontrolünden geçmedi.\n" + string.Join("\n", issues) + "\nSahne editöründe anlatımı ve süreyi düzelt; sonra videoyu yeniden üret.");

        }

        if (!Reliability.OutputsCurrent(e)) throw new InvalidOperationException("Çıktılar güncel senaryoyla eşleşmiyor veya dosya değişmiş. Yeniden üretim gerekiyor.");

        if (e.Demo || e.Videos.Count != Rules.Scripts(e).Count() || e.Videos.Count == 0 || e.Videos.Any(v => !v.QualityPassed) || e.Plan?.Warnings.Count > 0) throw new InvalidOperationException("Onay için tüm gerçek videoların kalite kontrolünden geçmesi ve senaryo uyarılarının çözülmesi gerekiyor.");

        if (store.Settings.YouTubeChannelId == "") throw new InvalidOperationException("Bağlantı Merkezi → YouTube kanalını doğrula tuşuyla hedef kanalı kaydet; sonra yeniden onayla.");

        if (Rules.Scripts(e).SelectMany(x => x.Script.Scenes).Any(s => !s.RightsConfirmed)) throw new InvalidOperationException("Yerel görüntü kullanım hakları doğrulanmalı. İncele/onayla ekranında kaynakları kontrol edip hak teyidini kaydet.");

        e.ApprovedChannelId = store.Settings.YouTubeChannelId;

        e.Approved = true; e.ApprovalHash = Rules.HashPlan(e); e.Status = "Onaylandı"; Log(e, planned ? "Yetkilendirilmiş yayın sırası: içerik ve medya denetimleri geçti; plan kapsamında onaylandı." : "İçerik ve medya kullanıcı tarafından onaylandı.");

    }

    public async Task Upload(Episode e, bool schedule, YouTube? remoteClient = null)

    {

        if (Busy) throw new InvalidOperationException("Başka bir iş çalışıyor.");

        using var operation = new OperationLease(store);

        if (remoteClient == null && e.RemoteUploadJob != "" && e.RemoteUploadState != "completed") throw new InvalidOperationException("Bu paketin GitHub yüklemesi var. Önce durum kontrolünden sonucu al; çift yükleme yapılmadı.");

        if (e.Demo) throw new InvalidOperationException("Örnek paketler YouTube'a yüklenmez.");

        if (!e.Approved || e.ApprovalHash != Rules.HashPlan(e)) throw new InvalidOperationException("Güncel senaryo ve videoları önce onayla.");

        if (!Reliability.OutputsCurrent(e)) throw new InvalidOperationException("Onaylanan çıktı değişmiş veya senaryoyla eşleşmiyor; yeniden incele.");

        if (e.Videos.Any(v => !v.QualityPassed || !File.Exists(v.File))) throw new InvalidOperationException("Kalite kontrolünden geçmemiş veya eksik video var.");

        if (schedule && e.Videos.Any(v => NeedsSchedule(v) && (v.PublishAt == null || v.PublishAt <= DateTimeOffset.UtcNow.AddMinutes(10)))) throw new InvalidOperationException("Yayın tarihleri en az 10 dakika ileride olmalı.");

        cancel = new();

        try

        {

            e.Error = ""; e.Status = "Yükleniyor"; e.Progress = 0; e.ResumeStage = "Upload"; e.ResumeSchedule = schedule; Notify();

            foreach (var asset in e.Videos) { await using var stream = File.OpenRead(asset.File); if (asset.FileHash == "" || Convert.ToHexString(await SHA256.HashDataAsync(stream, cancel.Token)) != asset.FileHash) throw new InvalidOperationException("Video dosyası denetimden sonra değişmiş. Yeniden üretip onayla."); }

            await VideoBackup.Save(store, e, cancel.Token);

            var yt = remoteClient ?? new YouTube(store);

            var channel = await yt.ChannelIdentity(cancel.Token);

            VerifyChannel(e.ApprovedChannelId, store.Settings.YouTubeChannelId, channel.Id);

            foreach (var asset in e.Videos)

            {

                if (schedule && !NeedsSchedule(asset)) continue;

                var script = Rules.Scripts(e).First(x => x.Key == asset.Key).Script;

                Log(e, asset.Title + ": YouTube yüklemesi / yayın ayarı.");

                if (!schedule && asset.YouTubeId != "") { await yt.Unschedule(asset, cancel.Token); Notify(); }

                await Reliability.ValidateVideo(store, asset.File, cancel.Token, complete: true);

                await yt.Upload(e, asset, script, cancel.Token, (sent, total) => { double units = e.Videos.Count(v => v.YouTubeId != "" && v != asset) + sent / (double)Math.Max(1, total); e.Progress = Math.Min(99, (int)(100 * units / e.Videos.Count)); Notify(); }); Notify();

                if (!asset.ThumbnailUploaded && (!asset.Short || !asset.ThumbnailAttempted)) {

                    try { await yt.Thumbnail(asset, cancel.Token); asset.ThumbnailAttempted = true; asset.ThumbnailNote = "Kapak YouTube API tarafından kabul edildi."; }

                    catch (ProviderException ex) when (asset.Short && ex.Status is 400 or 403 or 415) { asset.ThumbnailAttempted = true; asset.ThumbnailNote = "Shorts kapağı API üzerinden kabul edilmedi. Hazırlanan PNG'yi YouTube Studio → Küçük resim → Dosya yükle bölümünden seç."; Log(e, asset.ThumbnailNote + " " + ex.Message); }

                    Notify();

                }

                if (schedule && NeedsSchedule(asset)) { await yt.Schedule(asset, cancel.Token); Notify(); }

            }

            e.RetryAt = null; e.RetryAttempts = 0; e.ResumeStage = ""; e.ResumeSchedule = null;

            e.Progress = 100; e.Status = schedule ? "Yayın planlandı" : "YouTube'da özel"; Log(e, schedule ? "Videolar YouTube üzerinde zamanlandı. Yayın saati için bilgisayar açık olmak zorunda değil." : "Videolar özel olarak yüklendi; herkese açık yayın henüz planlanmadı.");

        }

        catch (OperationCanceledException) { e.Status = "Durduruldu"; Log(e, "Yükleme durduruldu. Devam edebilirsin."); }

        catch (Exception ex) { Reliability.Failed(e, ex, "Upload"); Log(e, ex.Message); }

        finally { cancel.Dispose(); cancel = null; operation.Dispose(); Notify(); }

    }

    public static ContentPlan DemoPlan(Episode e)

    {

        VideoScript Make(string title, bool shortVideo) => new() { Title = title, ThumbnailText = shortVideo ? "BİR KONU.\nİKİ FORMAT." : "FİKRİNDEN\nYAYIN TAKVİMİNE", Description = "TubeFlow arayüz ve video motoru örneği. Gerçek içerik değildir.", Scenes = [new() { Narration = "Bu bir TubeFlow örnek projesidir. Normal video ve Shorts aynı konu paketinde yönetilir.", VisualQuery = "creative studio", OnScreen = "Bir fikir.\nBir içerik evreni." }, new() { Narration = "Senaryo, ses, görüntü ve altyazı ayrı aşamalardır. Her aşamayı kontrol panelinden düzenleyebilirsin.", VisualQuery = "video editing", OnScreen = "Otonom üretim.\nKontrol sende." }] };

        return new() { Research = "Yerel örnek. İnternetten araştırma yapılmadı. Grafikler uygulama tarafından oluşturuldu. Ses yerine sessiz akış kullanıldı.", Long = e.IncludeLong ? Make(e.Topic, false) : null, Shorts = Enumerable.Range(0, e.ShortCount).Select(i => Make("Shorts " + (i + 1) + " • " + e.Topic, true)).ToList() };

    }

}















