namespace AnkaStudio;





public static class PlannedStock


{


    public static List<Episode> Pending(IEnumerable<Episode> episodes) => episodes


        .Where(e => !e.Demo && e.Videos.Count > 0 && e.Videos.Any(v => v.YouTubeState != "Yayında" && Engine.NeedsSchedule(v)))


        .OrderBy(e => e.Videos.Where(Engine.NeedsSchedule).Min(v => v.PublishAt ?? DateTimeOffset.MaxValue)).ToList();





    public static void Validate(IEnumerable<Episode> episodes, DateTimeOffset now)


    {


        foreach (var episode in episodes)


        {


            if (!episode.Approved || episode.ApprovalHash != Rules.HashPlan(episode) || !Reliability.OutputsCurrent(episode))


                throw new InvalidOperationException(episode.Topic + ": gÃ¼ncel videolarÄ± Ã¶nce incele ve onayla.");


            foreach (var video in episode.Videos.Where(Engine.NeedsSchedule))


                if (video.PublishAt == null || video.PublishAt <= now.AddMinutes(10))


                    throw new InvalidOperationException(video.Title + ": yayÄ±n tarihini en az 10 dakika ileride belirle.");


        }


    }


}


