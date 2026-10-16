using System.Text.Json;
namespace AnkaStudio;

public sealed class CreationFlow
{
    public int Reached {get;set;}
    public string PlannedTextHash {get;set;}="";
    public string Signature=>TimedNarration.TextHash(Text+Normal+Split+ExtraShortCount+string.Join("|",ExtraShortTexts));
    public int ExtraShortCount {get;set;}
    public List<string> ExtraShortTexts {get;set;}=[];
    public string ExtraApprovalHash {get;set;}="";
    public string ExtrasHash=>TimedNarration.TextHash(Text+"|"+string.Join("|",ExtraShortTexts));
    public Dictionary<string,string> OriginalScripts {get;set;}=[];
    public bool? Normal {get;set;}
    public string TextMode {get;set;}="";
    public string OwnText {get;set;}="";
    public string Prompt {get;set;}="";
    public string AiText {get;set;}="";
    public string AiRequestHash {get;set;}="";
    public string RequestHash(Episode e)=>TimedNarration.TextHash(e.Topic+"|"+Prompt+"|"+Normal+"|"+(Normal==true?e.LongMinutes*60:e.ShortSeconds));
    public string OriginalAudioSnapshot {get;set;}="";
    public string ApprovedHash {get;set;}="";
    public List<SourceRef> Sources {get;set;}=[];
    public bool? Subtitles {get;set;}
    public string SubtitleStyle {get;set;}="";
    public bool AiVisuals {get;set;}
    public bool StockVisuals {get;set;}
    public bool OwnVisuals {get;set;}
    public List<string> Files {get;set;}=[];
    public string Voice {get;set;}="ai";
    public string AudioFile {get;set;}="";
    public string AudioHash {get;set;}="";
    public string FrozenAudio {get;set;}="";
    public string FrozenTiming {get;set;}="";
    public string FrozenAudioHash {get;set;}="";
    public bool FrozenCaptioned {get;set;}
    public string FrozenVoiceHash {get;set;}="";
    public List<string> EditHistory {get;set;}=[];
    public string FrozenTextHash {get;set;}="";
    public double AudioDuration {get;set;}
    public List<Caption> AudioCaptions {get;set;}=[];
    public string Mode {get;set;}="";
    public bool? Split {get;set;}
    public string OriginalSections {get;set;}="";
    public List<Scene> BeforeRender {get;set;}=[];
    public string CoverChoice {get;set;}="";
    public string Fit {get;set;}="crop";
    public bool PhotoMotion {get;set;}=true;
    public string Text=>TextMode=="ai"?AiText:OwnText;
    public bool TextApproved=>Text.Trim()!=""&&ApprovedHash==TimedNarration.TextHash(Text);
    public static readonly string[] SubtitleStyles=["Sade beyaz","Sarı vurgulu","Kelime kelime renklenen","Koyu kutulu","Sinematik altın"];
    public void ValidateSource(Episode e)
    {
        if(e.Topic.Trim()=="")throw new InvalidOperationException("Proje adı / konu başlığını yaz.");
        if(Normal==null)throw new InvalidOperationException("Normal veya kısa video seç.");
        if(TextMode is not ("own" or "ai"))throw new InvalidOperationException("Metni kimin hazırlayacağını seç.");
        if(TextMode=="ai"&&AiRequestHash!=RequestHash(e))throw new InvalidOperationException("Konu, talimat veya hedef süre değişti; AI metnini yeniden hazırla ve onayla.");
        if(!TextApproved)throw new InvalidOperationException("Metni okuyup onayla.");
    }
    public void ValidateDesign()
    {
        if(!AiVisuals&&!StockVisuals&&!OwnVisuals)throw new InvalidOperationException("En az bir görsel kaynağı seç.");
        if(OwnVisuals&&Files.Count==0&&!AiVisuals&&!StockVisuals)throw new InvalidOperationException("Kendi görsellerini ekle veya bir otomatik kaynak seç.");
        if(Subtitles==null)throw new InvalidOperationException("Altyazı açık veya kapalı seçimini yap.");
        if(Subtitles==true&&!SubtitleStyles.Contains(SubtitleStyle))throw new InvalidOperationException("Hazır altyazı kalıbını seç.");
        if(Voice=="file"&&(!File.Exists(AudioFile)||Reliability.FileHash(AudioFile)!=AudioHash))throw new InvalidOperationException("Konuşma dosyası eksik veya değişmiş; yeniden seç.");
        if(Normal==true&&ExtraShortCount>0){if(Voice=="file")throw new InvalidOperationException("Ek Shorts için AI seslendirmesini seç veya ses dosyalı videonu ayrı proje olarak hazırla.");if(ExtraShortTexts.Count!=ExtraShortCount||ExtraApprovalHash!=ExtrasHash||ExtraShortTexts.Any(x=>string.IsNullOrWhiteSpace(x)||ScriptLength.Count(x)>360))throw new InvalidOperationException("Ek Shorts metinlerini hazırlayıp onayla.");}
        if(CoverChoice=="")throw new InvalidOperationException("Kapak seçimini yap; kapaksız da devam edebilirsin.");
    }
    public TextLayout Layout(Settings settings)
    {
        var layout=TextLayout.Defaults(settings,Normal==false);layout.TitleEnabled=false;layout.SubtitleEnabled=Subtitles==true;layout.SubtitleY=Normal==false?80:88;
        layout.SubtitleColor=SubtitleStyle is "Sarı vurgulu" or "Sinematik altın"?"FFD166":"FFFFFF";
        layout.SubtitleBackground=SubtitleStyle=="Koyu kutulu";layout.Karaoke=SubtitleStyle=="Kelime kelime renklenen";layout.Effect=SubtitleStyle=="Sinematik altın"?"3D kabartma":"Gölge";return layout;
    }
    public void CommitText(Episode e)
    {
        ValidateSource(e);e.IncludeLong=Normal==true;e.ShortCount=e.IncludeLong&&ExtraShortTexts.Count==ExtraShortCount?ExtraShortCount:e.IncludeLong?0:1;
        e.UserNarration??=new();e.UserNarration.Enabled=true;e.UserNarration.VisualTheme=e.Topic;
        if(e.IncludeLong){e.UserNarration.LongText=Text;e.UserNarration.Shorts=ExtraShortTexts.Take(e.ShortCount).ToList();}else{if(e.UserNarration.Shorts.Count==0)e.UserNarration.Shorts.Add(Text);else e.UserNarration.Shorts[0]=Text;}
        if(CoverChoice=="legacy"&&!e.IncludeLong&&e.Plan?.Shorts.Count>1){e.ShortCount=e.Plan.Shorts.Count;e.UserNarration.Shorts=e.Plan.Shorts.Select(s=>string.Join(" ",s.Scenes.Select(x=>x.Narration))).ToList();e.UserNarration.Shorts[0]=Text;}
        UserNarration.Validate(e);
    }
    public static T Copy<T>(T value)=>JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value,Json.Options),Json.Options)!;
    public static CreationFlow Migrate(Episode e)
    {
        if(e.Plan==null)return new();var script=Rules.Scripts(e).FirstOrDefault().Script;var text=script==null?"":string.Join(" ",script.Scenes.Select(s=>s.Narration));
        var f=new CreationFlow{Sources=Copy(e.Plan.Sources),Normal=e.IncludeLong,TextMode="own",OwnText=text,ExtraShortCount=e.IncludeLong?e.Plan.Shorts.Count:0,ExtraShortTexts=e.IncludeLong?e.Plan.Shorts.Select(s=>string.Join(" ",s.Scenes.Select(x=>x.Narration))).ToList():[],ApprovedHash=TimedNarration.TextHash(text),AiVisuals=e.AiStoryImages,StockVisuals=!e.AiStoryImages,OwnVisuals=script?.Scenes.Any(s=>s.UserLocal)==true,Subtitles=script?.TextLayout?.SubtitleEnabled??true,SubtitleStyle="Sade beyaz",Mode="auto",Split=script?.Scenes.Count>1,CoverChoice="legacy",Reached=e.Approved?4:e.Videos.Count>0?3:2};f.ExtraApprovalHash=f.ExtrasHash;f.PlannedTextHash=f.Signature;return f;
    }
}
