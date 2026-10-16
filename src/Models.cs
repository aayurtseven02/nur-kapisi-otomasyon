using System.Globalization;

using System.Runtime.InteropServices;

using System.Security.Cryptography;

using System.Text;

using System.Text.Json;



namespace AnkaStudio;



public static class Json

{

    public static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true, RespectNullableAnnotations = true };

    static readonly System.Collections.Concurrent.ConcurrentDictionary<string, object> Gates = new(StringComparer.OrdinalIgnoreCase);

    public static readonly System.Collections.Concurrent.ConcurrentQueue<string> RecoveryMessages = new();

    public static object Gate(string path) => Gates.GetOrAdd(Path.GetFullPath(path), _ => new());

    public static T Read<T>(string path, T fallback)

    {

        lock (Gate(path)) {

            if (!File.Exists(path)) return fallback;

            try { return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options) ?? throw new JsonException("Boş kayıt"); }

            catch (Exception ex) when (ex is JsonException or NotSupportedException) {

                var quarantine = path + ".damaged-" + DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff") + "-" + Guid.NewGuid().ToString("N");

                File.Copy(path, quarantine);

                if (File.Exists(path + ".bak")) {

                    var recovered = JsonSerializer.Deserialize<T>(File.ReadAllText(path + ".bak"), Options) ?? throw new InvalidOperationException("Kayıt ve yedek hasarlı. Dosyalar korundu: " + path);

                    File.Copy(path + ".bak", path, true); RecoveryMessages.Enqueue("Son sağlam yedekten kurtarıldı: " + Path.GetFileName(path)); return recovered;

                }

                throw new InvalidOperationException("Kayıt hasarlı; sağlam yedek yok. Dosyalar korundu: " + path, ex);

            }

        }

    }

    public static void Write<T>(string path, T value)

    {

        lock (Gate(path)) {

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";

            try {

                var bytes = JsonSerializer.SerializeToUtf8Bytes(value, Options);

                if(File.Exists(path) && new FileInfo(path).Length==bytes.Length && File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes)) {

                    if(!File.Exists(path+".bak") || !File.ReadAllBytes(path+".bak").AsSpan().SequenceEqual(bytes))File.Copy(path,path+".bak",true);

                    return;

                }

                using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { stream.Write(bytes); stream.Flush(true); }

                if (File.Exists(path)) {

                    bool valid = true; try { using var old = JsonDocument.Parse(File.ReadAllText(path)); } catch (JsonException) { valid = false; }

                    if (valid) {
                        for(int attempt=0;;attempt++) {
                            try { File.Replace(temp,path,path+".bak"); break; }
                            catch(IOException ex) when(attempt<5 && (ex.HResult & 0xFFFF) is 32 or 33) { Thread.Sleep(20 << attempt); }
                        }
                    }

                    else { File.Copy(path, path + ".damaged-" + Guid.NewGuid().ToString("N")); File.Move(temp, path, true); }

                } else { File.Move(temp, path); File.Copy(path, path + ".bak"); }

            } finally { if (File.Exists(temp)) File.Delete(temp); }

        }

    }

}



public class Settings

{

    public bool IncludeResearchSources { get; set; } = true;

    public string ExcludedStockUrls {get;set;}="";

    public string PronunciationDictionary { get; set; } = "";

    public string CoverStyle { get; set; } = "Minimal";

    public bool CoverAiEnabled { get; set; }
    public string CoverDesignInstructions { get; set; } = "";
    public string CoverReferenceImage {get;set;} = "";

    public bool CoverPaidAllowed { get; set; }

    public string CoverProviderOrder { get; set; } = "NVIDIA,Gemini,OpenAI";
    public string CoverNvidiaModel { get; set; } = "black-forest-labs/flux.1-schnell";
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public bool? SetupCompleted { get; set; }
    public bool CoverLocalEnabled { get; set; }
    public string CoverLocalWorkflow { get; set; } = "";
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? CoverTitleStyle { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? CoverSubtitleText { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool CoverTitleRendered { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? CoverLocalModel { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? CoverLocalQuality { get; set; }

    public string CoverGeminiModel { get; set; } = "gemini-2.5-flash-image";

    public string CoverOpenAiModel { get; set; } = "gpt-image-1";

    public int CoverMonthlyRequests { get; set; } = 20;

    public bool CoverAutoStyle { get; set; } = true;



    public string CoverMode { get; set; } = "Şablon";

    public string CoverBackground { get; set; } = "";

    public string CoverLogo { get; set; } = "";

    public bool ScriptCardsOverride { get; set; }

    public bool OpeningCardEnabled { get; set; }

    public string OpeningCardFile { get; set; } = "";

    public double OpeningCardSeconds { get; set; } = 3;

    public bool ClosingCardEnabled { get; set; }

    public string ClosingCardFile { get; set; } = "";

    public double ClosingCardSeconds { get; set; } = 4;

    public string VideoLogo { get; set; } = "";

    public bool VideoLogoEnabled { get; set; }

    public string VideoLogoPosition { get; set; } = "Sağ üst";

    public int VideoLogoWidthPercent { get; set; } = 10;

    public string CoverPng { get; set; } = "";

    public string? CoverChannelName { get; set; }
    public bool CoverShowChannelName { get; set; } = true;
    public string CoverFont { get; set; } = "Segoe UI";

    public string CoverLogoPosition { get; set; } = "Sağ alt";

    public string CoverAccent { get; set; } = "";

    public bool UiSounds { get; set; } = true;
    public int UiClickSoundRevision { get; set; }

    public double UiSoundVolume { get; set; } = .25;

    public bool FreeOnly { get; set; } = true;

    public int LocalAiPolicyVersion { get; set; }

    public bool LocalAiFirst { get; set; } = true;

    public bool CloudAiEnabled { get; set; }

    public string EnabledCloudProviders { get; set; } = "";

    public int OllamaTimeoutMinutes { get; set; } = 15;

    public bool UseOllama { get; set; } = true;

    public string GeminiModel { get; set; } = "gemini-3.8-flash";

    public bool NvidiaAutoSelect { get; set; } = true;
    public string NvidiaModel { get; set; } = "meta/llama-3.1-70b-instruct";
    public string GroqModel { get; set; } = "openai/gpt-oss-120b";

    public string OpenRouterModel { get; set; } = "openrouter/free";

    public string KimiModel { get; set; } = "kimi-k3";

    public bool UseKimi { get; set; }

    public string OllamaModel { get; set; } = "qwen3:4b";

    public string VoiceProvider { get; set; } = "Windows / ücretsiz";

    public bool GoogleTtsEnabled { get; set; } = true;

    public bool CloudApiControl { get; set; }

    public bool CloudAutoDisable { get; set; }

    public string CloudControlProject { get; set; } = "";

    public bool CloudUsageRequired { get; set; } = true;

    public string CloudBillingTable { get; set; } = "";

    public string CloudBillingLocation { get; set; } = "EU";

    public string GoogleTtsVoice { get; set; } = "tr-TR-Standard-A";

    public int GoogleTtsMonthlyLimit { get; set; } = 900000;

    public bool GoogleTtsFallback { get; set; } = true;

    public string WindowsVoice { get; set; } = "";

    public string PiperPath { get; set; } = "";

    public string PiperModel { get; set; } = "";
    public string XttsUrl { get; set; } = "http://127.0.0.1:8020";
    public string XttsSpeaker { get; set; } = "";

    public string SourceUrls { get; set; } = "";

    public string ProviderOrder { get; set; } = "Ollama,OpenRouter,Groq,NVIDIA,Gemini,OpenAI,Kimi";

    public bool PoolEnabled { get; set; } = true;

    public bool PoolAutoSave { get; set; } = true;

    public bool PoolFallback { get; set; } = true;

    public bool StockGeneralFallback { get; set; } = true;

    public int PoolRepeatHours { get; set; } = 24;

    public string StockOrder { get; set; } = "Pexels,Pixabay,Vecteezy";

    public bool VecteezyEnabled { get; set; } = true;

    public int VecteezyMonthlyDownloads { get; set; } = 450;

    public string LocalMediaFolder { get; set; } = "";

    public string CategoryId { get; set; } = "27";

    public string EncodingPreset { get; set; } = "veryfast";

    public int Crf { get; set; } = 20;

    public int FramesPerSecond { get; set; } = 30;

    public Dictionary<string, int> SeriesCounters { get; set; } = [];

    public Dictionary<string, int> SeriesAssignments { get; set; } = [];

    public string SocialBrokerUrl { get; set; } = "";
    public string SocialGraphVersion { get; set; } = "v25.0";
    public List<SocialAccount> SocialAccounts { get; set; } = [];
    public string ChannelName { get; set; } = "Benim kanalım";

    public string Audience { get; set; } = "Meraklı Türkçe izleyiciler";

    public string Model { get; set; } = "gpt-4.1";

    public string VoiceId { get; set; } = "";

    public string VoiceModel { get; set; } = "eleven_multilingual_v2";

    public double VoiceSpeed { get; set; } = 1;

    public bool ShortVoiceOverride { get; set; }

    public string ShortVoiceProvider { get; set; } = "Normal videoyla aynı";

    public string ShortVoiceId { get; set; } = "";

    public double ShortVoiceSpeed { get; set; } = 1.05;

    public double ShortMusicVolume { get; set; } = .1;

    public string FfmpegPath { get; set; } = "";

    public string VideoBackupFolder { get; set; } = "";

    public string MusicPath { get; set; } = "";

    public MusicTrack? LibraryMusic { get; set; }

    public double MusicVolume { get; set; } = 0.12;

    public string MusicStyle { get; set; } = "Sakin / ambient";

    public string GoogleClientId { get; set; } = "";

    public string YouTubeChannelId { get; set; } = "";

    public string YouTubeChannelTitle { get; set; } = "";

    public string Repo { get; set; } = "";

    public string GitHubRef { get; set; } = "main";

    public bool AutoPublish { get; set; }

    public bool AutoResearch { get; set; }

    public bool AutoProduce { get; set; }

    public int DailyProductionLimit { get; set; } = 4;

    public string SubtitleColor { get; set; } = "FFFFFF";

    public int LongFontSize { get; set; } = 46;

    public int ShortFontSize { get; set; } = 68;

}



public class Episode

{

    public Settings? ProductionSettings { get; set; }

    public string CurrentStage { get; set; } = "";

    public bool DeferMissedPublication {get;set;}

    public Dictionary<string,double> StageSeconds {get;set;}=[];

    public MusicClipOptions? MusicClip { get; set; }
    public bool PhotosOnly {get;set;}
    public bool AiStoryImages {get;set;}
    public string AiStoryStyle {get;set;} = "Sinematik tarihî illüstrasyon, doğal ışık, tutarlı karakterler ve kıyafetler";
    public string AiStoryCharacters {get;set;} = "";
    public bool AiStoryMotion {get;set;} = true;
    public bool AiStoryCinematic {get;set;} = true;
    public bool AiStoryAutonomous {get;set;}
    public int WizardStep {get;set;}
    public string WizardPreset {get;set;}="";
    public bool AiStoryLocal {get;set;} = true;
    public string AiStoryCastPrompt {get;set;} = "";
    public string AiStoryPromptHash {get;set;} = "";
    public string AiStoryIdentityHash {get;set;} = "";
    public int AiStoryCastCount {get;set;} = 1;
    public string AiStoryReferenceFile {get;set;} = "";
    public string AiStoryReferenceHash {get;set;} = "";
    public string AiStoryReferenceIdentityHash {get;set;} = "";
    public bool WordRepairResumeProduction {get;set;}

    public UserNarrationOptions? UserNarration { get; set; }



    public int QueueOrder { get; set; }

    public string QueueState { get; set; } = "";

    public bool QueueAuthorized { get; set; }

    public Settings? QueueSettings { get; set; }

    public MusicTrack? QueueMusic { get; set; }

    public TextLayout? QueueLayout { get; set; }

    public string ResumeStage { get; set; } = "";

    public bool? ResumeSchedule { get; set; }

    public int RetryAttempts { get; set; }

    public DateTimeOffset? RetryAt { get; set; }

    public List<string> ProductionDays { get; set; } = [];

    public bool TextDesignPending { get; set; }

    public List<SocialPost> SocialPosts { get; set; } = [];
    public bool? ScenesReviewed { get; set; }
    public bool SkipArtworkEditing { get; set; }
    public bool SkipTextDesignEditing { get; set; }

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Topic { get; set; } = "";

    public string Brief { get; set; } = "";

    public string Style { get; set; } = "Belgesel";

    public int LongMinutes { get; set; } = 6;

    public int ShortSeconds { get; set; } = 45;

    public int ShortCount { get; set; } = 2;

    public bool IncludeLong { get; set; } = true;

    public string ShortMode { get; set; } = "Bağlantılı Shorts";

    public bool Music { get; set; }

    public bool Demo { get; set; }

    public bool MadeForKids { get; set; }

    public bool DiscloseSynthetic { get; set; }

    public string Status { get; set; } = "Taslak";

    public int Progress { get; set; }

    public string Error { get; set; } = "";

    public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? PublishAt { get; set; }

    public int ShortSpacingHours { get; set; } = 24;

    public bool Approved { get; set; }

    public string ApprovedChannelId { get; set; } = "";

    public string ApprovalHash { get; set; } = "";

    public string RemoteUploadJob { get; set; } = "";

    public string RemoteUploadNotifiedJob { get; set; } = "";

    public string RemoteUploadRepo { get; set; } = "";

    public string RemoteUploadNote { get; set; } = "";

    public string RemoteUploadState { get; set; } = "";

    public long RemoteUploadRelease { get; set; }

    public long RemoteUploadAsset { get; set; }

    public bool RemoteUploadSchedule { get; set; }

    public string RemoteState { get; set; } = "";

    public string RemoteRepo { get; set; } = "";

    public string RemoteRunId { get; set; } = "";

    public ContentPlan? Plan { get; set; }

    public Settings? ArtworkSettings { get; set; }

    public bool ArtworkPending { get; set; }

    public List<AssetVideo> Videos { get; set; } = [];

    public List<string> Events { get; set; } = [];

    public int Revision { get; set; } = 1;

    public void Log(string message) { Events.Add($"{DateTimeOffset.Now:HH:mm:ss}  {message}"); if (Events.Count > 250) Events.RemoveAt(0); }

}



public class SourceRef

{

    public string Title { get; set; } = "";

    public string Url { get; set; } = "";

}

public class ContentPlan

{

    public string SpellingHash { get; set; } = "";

    public string Research { get; set; } = "";

    public DateTimeOffset ResearchedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<SourceRef> Sources { get; set; } = [];

    public List<string> Warnings { get; set; } = [];

    public VideoScript? Long { get; set; }

    public List<VideoScript> Shorts { get; set; } = [];

}

public class VideoScript

{

    public Settings? CoverSettings { get; set; }

    public string SceneTitleHash { get; set; } = "";

    public MusicTrack? MusicTrack { get; set; }

    public TextLayout? TextLayout { get; set; }

    public string Title { get; set; } = "";

    public string Description { get; set; } = "";

    public string ThumbnailText { get; set; } = "";

    public List<string> Tags { get; set; } = [];

    public List<Scene> Scenes { get; set; } = [];

}

public class Scene

{

    public string SourceLinks {get;set;}="";
    public string AiImageHash {get;set;} = "";
    public string AiImagePrompt {get;set;} = "";
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? AiPromptSourceHash {get;set;}
    public List<int> AiCharacterIds {get;set;} = [];

    public bool RightsConfirmed { get; set; }

    public bool UserLocal { get; set; }

    public double TitleStart { get; set; }

    public double TitleDuration { get; set; }

    public string SubtitleText { get; set; } = "";

    public string Narration { get; set; } = "";

    public List<Caption> SpeechCaptions { get; set; } = [];

    public string SpeechTextHash { get; set; } = "";

    public string TitlePlacement { get; set; } = "";

    public string StockMetadata { get; set; } = "";

    public int StockSelectionVersion { get; set; }
    public string StockReviewQuery { get; set; } = "";
    public string StockReviewTheme { get; set; } = "";

    public string VisualQuery { get; set; } = "";

    public string OnScreen { get; set; } = "";

    public string StockFile { get; set; } = "";

    public string StockUrl { get; set; } = "";

    public string Creator { get; set; } = "";

    public string LicenseUrl { get; set; } = "";

    public string Attribution { get; set; } = "";

    public double Duration { get; set; }

}

public class AssetVideo

{

    public string RenderFingerprint { get; set; } = "";

    public string NarrationFile {get;set;}="";

    public string NarrationTimingFile {get;set;}="";

    public string PipelineVersion { get; set; } = "";

    public string PlanHash { get; set; } = "";

    public DateTimeOffset? RemotePublishAt { get; set; }

    public string Key { get; set; } = "";

    public bool Short { get; set; }

    public string Title { get; set; } = "";

    public string File { get; set; } = "";

    public string Thumbnail { get; set; } = "";

    public double Duration { get; set; }

    public DateTimeOffset? PublishAt { get; set; }

    public string YouTubeId { get; set; } = "";

    public string UploadSession { get; set; } = "";

    public string YouTubeState { get; set; } = "";

    public DateTimeOffset? YouTubeCheckedAt { get; set; }

    public bool Scheduled { get; set; }

    public bool ThumbnailUploaded { get; set; }

    public bool ThumbnailAttempted { get; set; }

    public string ThumbnailNote { get; set; } = "";

    public bool QualityPassed { get; set; }

    public List<string> Checks { get; set; } = [];

    public string FileHash { get; set; } = "";

}



public sealed class StoreSnapshot

{

    [System.Text.Json.Serialization.JsonRequired]

    public int SchemaVersion { get; set; } = 2;

    [System.Text.Json.Serialization.JsonRequired]

    public Settings Settings { get; set; } = new();

    [System.Text.Json.Serialization.JsonRequired]

    public List<Episode> Episodes { get; set; } = [];

    public List<string> ProductionLedger { get; set; } = [];

}

public sealed class Store

{

    public readonly object OperationGate = new();

    public bool OperationBusy;

    public readonly Dictionary<string, DateTimeOffset> ProviderBlocked = new(StringComparer.OrdinalIgnoreCase);

    public string PreferredProvider = "";

    public string Root { get; }

    public Settings Settings { get; private set; }

    public List<Episode> Episodes { get; private set; }

    public List<string> ProductionLedger { get; private set; } = [];

    public Vault Vault { get; }

    public Store(string root)

    {

        Root = Path.GetFullPath(root); Directory.CreateDirectory(Root);

        Settings = new(); Episodes = [];

        var stateFile = Path.Combine(Root, "state.json");

        if (File.Exists(stateFile)) { var snapshot = ReadState(stateFile); if (snapshot.SchemaVersion > 2) throw new InvalidOperationException("Veriler daha yeni bir sürüme ait; eski programla açılmadı."); Settings = snapshot.Settings; Episodes = snapshot.Episodes; ProductionLedger = snapshot.ProductionLedger; }

        else {

            Settings = Json.Read(Path.Combine(root, "settings.json"), new Settings()); Episodes = Json.Read(Path.Combine(root, "episodes.json"), new List<Episode>());

            foreach (var episode in Episodes) {

                foreach (var day in episode.ProductionDays) ProductionLedger.Add(day + ":" + episode.Id);

                if (episode.Status == "Kesildi" && episode.ResumeStage == "") { episode.ResumeStage = episode.Plan == null ? "Plan" : "Produce"; episode.RetryAt = DateTimeOffset.UtcNow.AddSeconds(15); }

            }

        }

        if (Settings.LocalAiPolicyVersion < 1) {

            Settings.LocalAiPolicyVersion = 1; Settings.LocalAiFirst = true; Settings.UseOllama = true;

            Settings.CloudAiEnabled = false; Settings.EnabledCloudProviders = "";

            Settings.ProviderOrder = "Ollama," + string.Join(',', Settings.ProviderOrder.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Where(p => p != "Ollama"));

        }

        Vault = new Vault(Path.Combine(root, "secrets.dat"));

        foreach (var ep in Episodes.Where(x => x.Status is "Araştırılıyor" or "Üretiliyor" or "Yükleniyor")) { if (ep.ResumeStage == "") ep.ResumeStage = ep.Status == "Araştırılıyor" ? "Plan" : ep.Status == "Yükleniyor" ? "Upload" : "Produce"; ep.Status = "Kesildi"; ep.RetryAt = DateTimeOffset.UtcNow.AddSeconds(15); ep.Log("Uygulama yeniden açıldı. Tamamlanan aşamalar saklandı; otomatik ayar açıksa iş devam edecek."); }

        Save();

    }

    StoreSnapshot ReadState(string path)

    {

        foreach (var candidate in new[] { path, path + ".bak" }) {

            if (!File.Exists(candidate)) continue;

            try { var state = System.Text.Json.JsonSerializer.Deserialize<StoreSnapshot>(File.ReadAllText(candidate), Json.Options) ?? throw new JsonException();

                if (candidate != path) { File.Copy(path, path + ".damaged-" + Guid.NewGuid().ToString("N")); File.Copy(candidate, path, true); Json.RecoveryMessages.Enqueue("Ana kayıt sağlam yedekten kurtarıldı."); }

                return state;

            } catch (Exception ex) when (ex is JsonException or NotSupportedException) { }

        }

        // Legacy mirrors are a recovery source only when both are independently readable.

        var settingsPath = Path.Combine(Root, "settings.json"); var episodesPath = Path.Combine(Root, "episodes.json");

        try {

            var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(settingsPath), Json.Options) ?? throw new JsonException();

            var episodes = JsonSerializer.Deserialize<List<Episode>>(File.ReadAllText(episodesPath), Json.Options) ?? throw new JsonException();

            File.Copy(path, path + ".damaged-" + Guid.NewGuid().ToString("N"));

            Json.RecoveryMessages.Enqueue("Ana kayıt hasarlı; sağlam ayar ve paket kayıtları kurtarıldı.");

            return new StoreSnapshot { Settings = settings, Episodes = episodes, ProductionLedger = episodes.SelectMany(e => e.ProductionDays.Select(day => day + ":" + e.Id)).Distinct().ToList() };

        } catch (Exception ex) when (ex is JsonException or NotSupportedException or IOException) {

            throw new InvalidOperationException("Ana kayıt ve kurtarma kaynakları okunamadı. Dosyalar korundu; boş kayıtla üzerine yazılmadı.", ex);

        }

    }

    public void ApplySettings(Settings settings) { SettingsValidation.Check(settings); lock (gate) { Settings = settings; Save(); } }

    readonly object gate = new();

    public void Save() { lock (gate) {

        Json.Write(Path.Combine(Root, "settings.json"), Settings); Json.Write(Path.Combine(Root, "episodes.json"), Episodes);

        Json.Write(Path.Combine(Root, "state.json"), new StoreSnapshot { Settings = Settings, Episodes = Episodes, ProductionLedger = ProductionLedger });

    } }

    public string Folder(Episode episode) { if (!System.Text.RegularExpressions.Regex.IsMatch(episode.Id, "^[a-f0-9]{32}$")) throw new InvalidOperationException("Geçersiz paket kimliği."); var p = Path.Combine(Root, "projects", episode.Id); Directory.CreateDirectory(p); return p; }

    public string Key(string name) => Environment.GetEnvironmentVariable(name) ?? Vault.Get(name);

    public int ProducedToday => ProductionLedger.Count(x => x.StartsWith(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(3)).ToString("yyyy-MM-dd") + ":"));

    public void RecordProduction(Episode e) { var day = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(3)).ToString("yyyy-MM-dd"); if (!e.ProductionDays.Contains(day)) e.ProductionDays.Add(day); var id = day + ":" + e.Id; if (!ProductionLedger.Contains(id)) ProductionLedger.Add(id); }

    public string Ffmpeg

    {

        get

        {

            if (File.Exists(Settings.FfmpegPath)) return Settings.FfmpegPath;

            var local = Path.Combine(AppContext.BaseDirectory, "tools", "ffmpeg.exe");

            if (File.Exists(local)) return local;

            foreach (var path in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator)) { var f = Path.Combine(path, "ffmpeg.exe"); if (File.Exists(f)) return f; }

            throw new InvalidOperationException("Video motoru bulunamadı. Bağlantılar ekranından FFmpeg dosyasını seç.");

        }

    }

    public string Probe => Path.Combine(Path.GetDirectoryName(Ffmpeg)!, "ffprobe.exe");

}



// DPAPI protects desktop credentials with the current Windows user. Never committed or exported.

public sealed class Vault

{

    public static Func<bool, string?>? PasswordProvider;

    [StructLayout(LayoutKind.Sequential)] struct Blob { public int Length; public IntPtr Data; }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)] static extern bool CryptProtectData(ref Blob input, string description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out Blob output);

    [DllImport("crypt32.dll", SetLastError = true)] static extern bool CryptUnprotectData(ref Blob input, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out Blob output);

    [DllImport("kernel32.dll")] static extern IntPtr LocalFree(IntPtr memory);

    readonly string path;

    readonly Dictionary<string, string> values;

    byte[]? passwordKey;

    byte[]? salt;

    byte[]? pending;

    public Vault(string file, string? password = null)

    {

        path = file;

        values = [];

        if (!File.Exists(file)) return;

        var bytes = File.ReadAllBytes(file);

        if (bytes.AsSpan().StartsWith("ANKA1"u8))

        {

            pending = bytes;

            var pass = password ?? PasswordProvider?.Invoke(false);

            if (pass == null) throw new InvalidOperationException("Şifreli kasayı açmak için parolan gerekiyor.");

            Unlock(pass);

        }

        else { foreach (var pair in JsonSerializer.Deserialize<Dictionary<string, string>>(Transform(bytes, false)) ?? []) values[pair.Key] = pair.Value; }

    }

    public string Get(string key) => values.GetValueOrDefault(key, "");

    public void Set(string key, string value)

    {

        var previous = values.GetValueOrDefault(key); values[key] = value.Trim();

        try

        {

            var raw = JsonSerializer.SerializeToUtf8Bytes(values); byte[] encrypted;

            if (passwordKey != null) encrypted = ProtectPassword(raw);

            else

            {

                try { encrypted = Transform(raw, true); }

                catch (System.ComponentModel.Win32Exception)

                {

                    var pass = PasswordProvider?.Invoke(true); if (pass == null) throw;

                    EnablePassword(pass); encrypted = ProtectPassword(raw);

                }

            }

            File.WriteAllBytes(path + ".tmp", encrypted); File.Move(path + ".tmp", path, true);

        }

        catch { if (previous == null) values.Remove(key); else values[key] = previous; throw; }

    }

    public void EnablePassword(string password)

    {

        if (password.Length < 10) throw new InvalidOperationException("Kasa parolası en az 10 karakter olmalı.");

        salt = RandomNumberGenerator.GetBytes(16); passwordKey = Rfc2898DeriveBytes.Pbkdf2(password, salt, 310_000, HashAlgorithmName.SHA256, 32);

    }

    void Unlock(string password)

    {

        var bytes = pending!; if (bytes.Length < 49) throw new InvalidOperationException("Şifreli kasa hasarlı.");

        salt = bytes[5..21]; passwordKey = Rfc2898DeriveBytes.Pbkdf2(password, salt, 310_000, HashAlgorithmName.SHA256, 32);

        var raw = new byte[bytes.Length - 49];

        try { using var aes = new AesGcm(passwordKey, 16); aes.Decrypt(bytes.AsSpan(21, 12), bytes.AsSpan(49), bytes.AsSpan(33, 16), raw, "ANKA1"u8); }

        catch (CryptographicException) { CryptographicOperations.ZeroMemory(passwordKey); passwordKey = null; throw new InvalidOperationException("Kasa parolası yanlış veya dosya hasarlı."); }

        foreach (var pair in JsonSerializer.Deserialize<Dictionary<string, string>>(raw) ?? []) values[pair.Key] = pair.Value;

        CryptographicOperations.ZeroMemory(raw); pending = null;

    }

    byte[] ProtectPassword(byte[] raw)

    {

        var nonce = RandomNumberGenerator.GetBytes(12); var cipher = new byte[raw.Length]; var tag = new byte[16];

        using var aes = new AesGcm(passwordKey!, 16); aes.Encrypt(nonce, raw, cipher, tag, "ANKA1"u8);

        return [.. "ANKA1"u8.ToArray(), .. salt!, .. nonce, .. tag, .. cipher];

    }

    static byte[] Transform(byte[] bytes, bool encrypt)

    {

        var input = new Blob { Length = bytes.Length, Data = Marshal.AllocHGlobal(bytes.Length) };

        try

        {

            Marshal.Copy(bytes, 0, input.Data, bytes.Length);

            Blob output;

            var ok = encrypt ? CryptProtectData(ref input, "Anka Studio", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output) : CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output);

            if (!ok) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Windows anahtar kasası açılamadı. Windows kullanıcı kimliği erişilemiyor veya dosya farklı bir hesaba ait.");

            try { var data = new byte[output.Length]; Marshal.Copy(output.Data, data, 0, output.Length); return data; } finally { LocalFree(output.Data); }

        }

        finally { Marshal.FreeHGlobal(input.Data); }

    }

}



public static class Rules

{

    public static void Validate(Episode e)

    {
        StoryImages.Validate(e);

        ManualVideos.Validate(e);

        UserNarration.Validate(e);

        if (string.IsNullOrWhiteSpace(e.Topic) || e.Topic.Length > 300) throw new InvalidOperationException("Konu 1–300 karakter olmalı.");

        if (!e.IncludeLong && e.ShortCount == 0) throw new InvalidOperationException("Normal video veya en az bir Shorts seç.");

        if (e.LongMinutes is < 1 or > 30 || e.ShortSeconds is < 15 or > 180 || e.ShortCount is < 0 or > 6) throw new InvalidOperationException("Süre veya Shorts sayısı izin verilen aralıkta değil.");

    }

    public static IEnumerable<(string Key, bool Short, VideoScript Script)> Scripts(Episode e)

    {

        if (e.Plan == null) yield break;

        if (e.IncludeLong && e.Plan.Long != null) yield return ("long", false, e.Plan.Long);

        for (int i = 0; i < e.Plan.Shorts.Count; i++) yield return ($"short-{i + 1}", true, e.Plan.Shorts[i]);

    }

    public static void ValidatePlan(Episode e)

    {

        if (e.Plan == null) throw new InvalidOperationException("İçerik planı bulunamadı.");

        if (e.IncludeLong && e.Plan.Long == null || e.Plan.Shorts.Count != e.ShortCount) throw new InvalidOperationException("Üretilen normal video / Shorts sayısı paketle uyuşmuyor.");

        foreach (var (_, _, s) in Scripts(e))

        {

            s.TextLayout?.Validate();

            s.MusicTrack?.Validate();

            if (s.Scenes.Any(x => !double.IsFinite(x.TitleStart) || x.TitleStart is < 0 or > 1800 || !double.IsFinite(x.TitleDuration) || x.TitleDuration is < 0 or > 1800)) throw new InvalidOperationException("Başlık zamanları geçersiz.");

            if (string.IsNullOrWhiteSpace(s.Title) || s.Title.Length > 100 || s.Scenes.Count == 0 || s.Scenes.Count > 120 || s.Scenes.Any(x => string.IsNullOrWhiteSpace(x.Narration) || x.Narration.Length > 3500 || string.IsNullOrWhiteSpace(x.VisualQuery)))

                throw new InvalidOperationException("Senaryo yapısı geçersiz: başlık, anlatım ve sahne sorgularını kontrol et.");

        }

        if (e.MusicClip != null) MusicClips.Validate(e);

        if (!e.Demo && e.MusicClip == null && !UserNarration.Active(e) && (e.Plan.Sources.Count < 2 || e.Plan.Sources.Any(s => string.IsNullOrWhiteSpace(s.Title) || !Uri.TryCreate(s.Url, UriKind.Absolute, out var u) || u.Scheme != "https" || u.Host == "localhost") || e.Plan.Sources.Select(s => s.Url.TrimEnd('/')).Distinct(StringComparer.OrdinalIgnoreCase).Count() < 2)) throw new InvalidOperationException("Araştırma en az iki gerçek kaynak içermeli. Araştırmayı yeniden çalıştır.");

    }

    public static string HashPlan(Episode e) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(e.Plan, Json.Options))));

    public static DateTimeOffset? VideoDate(Episode e, bool shortVideo, int shortIndex) => e.PublishAt?.AddHours(shortVideo ? (shortIndex + (e.IncludeLong ? 1 : 0)) * e.ShortSpacingHours : 0);

    public static string Stamp(double t) => TimeSpan.FromSeconds(Math.Max(0, t)).ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture).Replace('.', ',');

}



public static class TopicProgram

{

    public static List<Episode> Parse(string text, bool includeLong, int minutes, int shorts, int seconds)

    {

        var result = new List<Episode>(); int lineNo = 0;

        foreach (var line in text.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))

        {

            lineNo++; if (lineNo > 100) throw new InvalidOperationException("Tek seferde en fazla 100 konu eklenebilir.");

            var parts = line.Split('|', 3, StringSplitOptions.TrimEntries); var e = new Episode { IncludeLong = includeLong, LongMinutes = minutes, ShortCount = shorts, ShortSeconds = seconds };

            if (parts.Length == 1) e.Topic = parts[0];

            else

            {

                if (!DateTime.TryParseExact(parts[0], "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) throw new InvalidOperationException($"Satır {lineNo}: tarih yyyy-MM-dd HH:mm biçiminde olmalı.");

                e.PublishAt = new DateTimeOffset(DateTime.SpecifyKind(date, DateTimeKind.Unspecified), TimeSpan.FromHours(3)); e.Topic = parts[1]; e.Brief = parts.Length > 2 ? parts[2] : "";

            }

            Rules.Validate(e); result.Add(e);

        }

        return result;

    }

}







