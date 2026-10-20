namespace AnkaStudio;
public static class ProductionStages
{
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Episode,Clock> Clocks=new();
    sealed class Clock {public string Stage="";public DateTimeOffset Start=DateTimeOffset.UtcNow;}
    public static void Observe(Episode episode,string text)
    {
        var clock=Clocks.GetOrCreateValue(episode);string stage=text.StartsWith("LTX",StringComparison.Ordinal)?"LTX kısa video":text.Contains("altyaz",StringComparison.OrdinalIgnoreCase)?"Altyazı":text.Contains("ses",StringComparison.OrdinalIgnoreCase)?"Ses":text.Contains("stok",StringComparison.OrdinalIgnoreCase)?"Görüntü":text.Contains("geçiş",StringComparison.OrdinalIgnoreCase)?"Geçiş":text.Contains("kapak",StringComparison.OrdinalIgnoreCase)?"Kapak":episode.Status;
        if(stage!=clock.Stage){if(clock.Stage!=""){double seconds=(DateTimeOffset.UtcNow-clock.Start).TotalSeconds;if(seconds>1)episode.StageSeconds[clock.Stage]=episode.StageSeconds.TryGetValue(clock.Stage,out var old)?old*.6+seconds*.4:seconds;}clock.Start=DateTimeOffset.UtcNow;clock.Stage=stage;episode.CurrentStage=stage;}
        if(text.StartsWith("LTX",StringComparison.Ordinal))episode.CurrentStage=text;
    }
    public static TimeSpan? Estimate(Episode episode,TimeSpan? measured)
    {
        if(measured!=null)return measured;
        if(!episode.StageSeconds.TryGetValue(episode.CurrentStage,out var seconds))return null;
        var clock=Clocks.GetOrCreateValue(episode);double left=seconds-(DateTimeOffset.UtcNow-clock.Start).TotalSeconds;
        return left>0?TimeSpan.FromSeconds(left):null;
    }
}
