namespace AnkaStudio;

internal static class PublicationStatistics
{
    public static (int Projects,int Shorts,int Normal) Count(IEnumerable<Episode> episodes)
    {
        var projects=episodes.Where(e=>!e.Demo).ToArray();
        bool Published(AssetVideo video)=>video.YouTubeId!="" && video.YouTubeState=="Yayında";
        var videos=projects.SelectMany(e=>e.Videos).Where(Published).GroupBy(v=>v.YouTubeId).Select(g=>g.First()).ToArray();
        return(projects.Count(e=>e.Videos.Any(Published)),videos.Count(v=>v.Short),videos.Count(v=>!v.Short));
    }
}
