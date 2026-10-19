using System.Text.Json;

namespace AnkaStudio;



public static class PackageArtwork

{

    public static readonly string[] CoverFields = "CoverLocalEnabled,CoverLocalWorkflow,CoverLocalModel,CoverLocalQuality,CoverLocalResolution,CoverTitleStyle,CoverSubtitleText,CoverDesignInstructions,CoverReferenceImage,CoverAiEnabled,CoverPaidAllowed,CoverProviderOrder,CoverNvidiaModel,CoverGeminiModel,CoverOpenAiModel,CoverMonthlyRequests,CoverStyle,CoverAutoStyle,CoverMode,CoverPng,CoverBackground,CoverLogo,CoverChannelName,CoverShowChannelName,CoverFont,CoverLogoPosition,CoverAccent".Split(',');

    public static readonly string[] CardFields = "OpeningCardEnabled,OpeningCardFile,OpeningCardSeconds,ClosingCardEnabled,ClosingCardFile,ClosingCardSeconds".Split(',');

    public static readonly string[] Fields = CoverFields.Concat("ScriptCardsOverride,OpeningCardEnabled,OpeningCardFile,OpeningCardSeconds,ClosingCardEnabled,ClosingCardFile,ClosingCardSeconds,VideoLogoEnabled,VideoLogo,VideoLogoPosition,VideoLogoWidthPercent".Split(',')).ToArray();

    static Settings Copy(Settings settings) => JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(settings, Json.Options), Json.Options)!;

    public static void Apply(Settings target, Settings source, IEnumerable<string> fields) { foreach (var name in fields) { var property = typeof(Settings).GetProperty(name)!; property.SetValue(target, property.GetValue(source)); } }

    public static Settings Snapshot(Settings settings) { var result = new Settings(); Apply(result, settings, Fields); return result; }

    public static Settings Effective(Settings settings, Episode episode, VideoScript? script = null) {

        var result = Copy(settings);
        if(episode.AiStoryImages && episode.AiStoryLocal && episode.ArtworkSettings==null && script?.CoverSettings==null){result.CoverAiEnabled=true;result.CoverLocalEnabled=true;if(!File.Exists(result.CoverLocalWorkflow))result.CoverLocalWorkflow=LocalCoverEngine.FindInstalledWorkflow();}
        if (episode.ArtworkSettings != null) Apply(result, episode.ArtworkSettings, Fields);

        if (script?.CoverSettings != null) { Apply(result, script.CoverSettings, CoverFields); if (script.CoverSettings.ScriptCardsOverride) Apply(result, script.CoverSettings, CardFields); } if (result.CoverChannelName != null) result.ChannelName = result.CoverChannelName; return result;

    }

}

