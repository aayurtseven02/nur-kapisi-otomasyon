namespace AnkaStudio;



public static class StorageMaintenance

{

    static readonly HashSet<string> CacheNames=new(StringComparer.OrdinalIgnoreCase){"audio-cache","clip-cache"};

    public static long Estimate(Episode e) => Math.Max(512L*1024*1024,(long)((e.IncludeLong?e.LongMinutes*60:0)+e.ShortCount*e.ShortSeconds)*6*1024*1024);

    public static void RequireSpace(Store store,Episode e)

    {

        var drive=new DriveInfo(Path.GetPathRoot(Path.GetFullPath(store.Root))!);

        if(drive.AvailableFreeSpace<Estimate(e))throw new InvalidOperationException("Üretim için tahmini boş alan yetersiz. Önbelleği veya başka dosyaları temizle; gereken yaklaşık alan: "+Size(Estimate(e)));

    }

    public static string Size(long bytes)=>bytes>=1024L*1024*1024 ? $"{bytes/(1024d*1024*1024):0.0} GB" : $"{bytes/(1024d*1024):0} MB";

    static IEnumerable<string> Files(string root)

    {

        if(!Directory.Exists(root))yield break;

        foreach(var entry in Directory.EnumerateFileSystemEntries(root)) {

            if((File.GetAttributes(entry)&FileAttributes.ReparsePoint)!=0)continue;

            if(Directory.Exists(entry)) { foreach(var path in Files(entry))yield return path; }

            else yield return entry;

        }

    }

    public static List<string> Candidates(Store store)

    {

        var root=Path.GetFullPath(Path.Combine(store.Root,"projects"))+Path.DirectorySeparatorChar;

        var outputs=store.Episodes.SelectMany(e=>e.Videos).SelectMany(v=>new[]{v.File,v.Thumbnail,v.NarrationFile,v.NarrationTimingFile}).Where(File.Exists).Select(Path.GetFullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var sources=store.Episodes.Where(e=>e.Plan!=null).SelectMany(Rules.Scripts).SelectMany(x=>x.Script.Scenes).Select(x=>x.StockFile).Where(File.Exists).Select(Path.GetFullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return Files(root).Where(p=>Path.GetFullPath(p).StartsWith(root,StringComparison.OrdinalIgnoreCase) && !outputs.Contains(Path.GetFullPath(p)) && !sources.Contains(Path.GetFullPath(p)) && p[root.Length..].Split(Path.DirectorySeparatorChar).Any(CacheNames.Contains)).ToList();

    }

    public static long Bytes(Store store)=>Candidates(store).Sum(p=>new FileInfo(p).Length);

    public static long Clean(Store store)

    {

        using var lease=new OperationLease(store);long bytes=0;

        foreach(var file in Candidates(store)){bytes+=new FileInfo(file).Length;File.Delete(file);}

        return bytes;

    }

}

