namespace AnkaStudio;
public static class ChannelPack
{
    public static bool Matches(Settings settings)=>new[]{settings.ChannelName,settings.YouTubeChannelTitle}.Any(name=>(name??"").Replace(" ","").Equals("NURKAPISI",StringComparison.OrdinalIgnoreCase));
    public static string Asset(string name){var packaged=Path.Combine(AppContext.BaseDirectory,"ChannelPackages","NurKapisi",name);return File.Exists(packaged)?packaged:Path.Combine(AppContext.BaseDirectory,"Assets",name);}
}
