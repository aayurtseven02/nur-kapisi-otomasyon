using System.Security.Cryptography;

using System.Text.RegularExpressions;



namespace AnkaStudio;



public sealed class PoolItem

{

    public bool RightsConfirmed { get; set; }

    public bool Validated { get; set; }

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string File { get; set; } = "";

    public string Tags { get; set; } = "";

    public string Category { get; set; } = "Genel";

    public string Source { get; set; } = "";

    public string Creator { get; set; } = "";

    public string License { get; set; } = "";

    public string Attribution { get; set; } = "";

    public DateTimeOffset Added { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastUsed { get; set; }

    public int Uses { get; set; }

    public string Hash { get; set; } = "";

    public bool GeneralBackground { get; set; }

    public long Token => -Math.Abs(BitConverter.ToInt64(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Id)), 0) >> 1) - 10000000000;

}



public sealed class MediaPool(Store store)

{

    readonly System.Runtime.CompilerServices.ConditionalWeakTable<List<PoolItem>, Dictionary<string, string>> snapshots = new();

    public string Folder => Path.Combine(store.Root, "media-pool");

    string Index => Path.Combine(Folder, "index.json");

    public List<PoolItem> List() { var items = Json.Read(Index, new List<PoolItem>()); snapshots.Add(items, items.ToDictionary(x => x.Id, x => System.Text.Json.JsonSerializer.Serialize(x, Json.Options))); return items; }

    public void Save(List<PoolItem> items) {

        lock (Json.Gate(Index)) {

            var current = Json.Read(Index, new List<PoolItem>()); snapshots.TryGetValue(items, out var before);

            foreach (var item in items) {

                var serialized = System.Text.Json.JsonSerializer.Serialize(item, Json.Options);

                if (before != null && before.TryGetValue(item.Id, out var previous)) {

                    if (serialized == previous) continue;

                    var latest = current.FirstOrDefault(x => x.Id == item.Id);

                    if (latest == null || System.Text.Json.JsonSerializer.Serialize(latest, Json.Options) != previous) throw new InvalidOperationException("Havuz kaydı başka bir işlemde değişti. Listeyi yenileyip tekrar kaydet.");

                }

                current.RemoveAll(x => x.Id == item.Id); current.Add(item);

            }

            Json.Write(Index, current);

        }

    }

    public void ConfirmRights() { lock (Json.Gate(Index)) { var items = List(); foreach (var item in items) item.RightsConfirmed = true; Json.Write(Index, items); } }

    bool Healthy(PoolItem item) {

        try { if (!File.Exists(item.File)) return false; var hash = Reliability.FileHash(item.File); if (item.Validated && hash == item.Hash) return true;

            if (hash != item.Hash) { item.RightsConfirmed = false; item.Source = "Kullanıcının havuz klasörü"; item.Creator = "Kullanıcı arşivi"; item.License = "Kullanım hakkı kullanıcı tarafından doğrulanmalı"; item.Attribution = ""; }

            Task.Run(() => Reliability.ValidateVideo(store, item.File, CancellationToken.None)).GetAwaiter().GetResult(); item.Hash = hash; item.Validated = true; return true;

        } catch { item.Validated = false; return false; }

    }

    public static HashSet<string> Words(string text) => Regex.Matches(text.ToLowerInvariant(), @"[\p{L}\p{N}]+").Select(m => m.Value).ToHashSet();

    public static HashSet<string> ExpandedWords(string text)

    {

        var words = Words(text);

        foreach (var group in new[] { "nature doğa doga", "forest orman trees ağaç agac", "sea deniz ocean okyanus beach sahil waves dalga", "space uzay stars yıldız yildiz galaxy galaksi", "sky gökyüzü gokyuzu clouds bulut", "sunrise sunset güneş gunes", "mountain dağ dag", "lake göl gol river nehir water su", "walking walk yürüyüş yuruyus", "reading read book books kitap", "document documents manuscript manuscripts belge belgeler elyazması", "family aile", "city şehir sehir" })

        { var synonyms = Words(group); if (words.Overlaps(synonyms)) words.UnionWith(synonyms); }

        return words;

    }

    public static int Score(PoolItem item, string query) => ExpandedWords(query).Intersect(ExpandedWords(item.Tags + " " + item.Category)).Count();

    static bool Neutral(PoolItem item) => item.GeneralBackground || ExpandedWords(item.Tags + " " + item.Category).Overlaps(Words("nature forest sea ocean space sky mountain lake river sunrise sunset"));

    public static string CategoryFor(string tags)

    {

        var words = Words(tags);

        foreach (var (category, terms) in new[] { ("Doğa", "nature forest sunrise mountain river lake trees"), ("Aile", "family parents children home"), ("Sofra", "bread food dinner meal kitchen wheat"), ("Yardımlaşma", "helping charity kindness volunteer community"), ("İbadet", "mosque prayer islam"), ("Şehir", "city street traffic building") })

            if (words.Overlaps(Words(terms))) return category;

        return "Genel";

    }

    public PoolItem Add(string file, string tags, string category, string source, string creator, string license, string attribution = "")

    {

        if (!File.Exists(file) || !new[] { ".mp4", ".mov", ".mkv", ".webm" }.Contains(Path.GetExtension(file).ToLowerInvariant())) throw new InvalidOperationException("Desteklenen video dosyası seç.");

        if (string.IsNullOrWhiteSpace(tags) || string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(license)) throw new InvalidOperationException("Etiket, kaynak ve kullanım izni bilgisi gerekli.");

        Task.Run(() => Reliability.ValidateVideo(store, file, CancellationToken.None)).GetAwaiter().GetResult();

        Directory.CreateDirectory(Folder);

        using var input = File.OpenRead(file); var hash = Convert.ToHexString(SHA256.HashData(input)); input.Close();

        var items = List(); var existing = items.FirstOrDefault(x => x.Hash == hash);

        if (existing != null) { existing.Tags = string.Join("; ", new[] { existing.Tags, tags }.Distinct()); if (attribution != "" && !existing.Attribution.Contains(attribution)) existing.Attribution = string.Join("\n", new[] { existing.Attribution, attribution }.Where(x => x != "")); Save(items); return existing; }

        var item = new PoolItem { Tags = tags, Category = category, Source = source, Creator = creator, License = license, Attribution = attribution, Hash = hash, Validated = true, RightsConfirmed = !license.Contains("doğrulan") };

        item.File = Path.Combine(Folder, item.Id + Path.GetExtension(file).ToLowerInvariant()); File.Copy(file, item.File); items.Add(item); Save(items); return item;

    }

    public async Task AddAsync(string file, string tags, string category, string source, string creator, string license, CancellationToken ct, string attribution = "")

    {

        if (string.IsNullOrWhiteSpace(tags) || string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(license)) throw new InvalidOperationException("Etiket, kaynak ve kullanım izni bilgisi gerekli.");

        await Reliability.ValidateVideo(store, file, ct);

        Directory.CreateDirectory(Folder);

        await using var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 1048576, true);

        var hash = Convert.ToHexString(await SHA256.HashDataAsync(input, ct)); ct.ThrowIfCancellationRequested();

        var items = List(); var existing = items.FirstOrDefault(x => x.Hash == hash);

        if (existing != null) { existing.Tags = string.Join("; ", new[] { existing.Tags, tags }.Distinct()); if (attribution != "" && !existing.Attribution.Contains(attribution)) existing.Attribution = string.Join("\n", new[] { existing.Attribution, attribution }.Where(x => x != "")); Save(items); return; }

        var item = new PoolItem { Tags = tags, Category = category, Source = source, Creator = creator, License = license, Attribution = attribution, Hash = hash, Validated = true, RightsConfirmed = !license.Contains("doğrulan") };

        item.File = Path.Combine(Folder, item.Id + Path.GetExtension(file).ToLowerInvariant()); var temp = item.File + ".part";

        try {

            input.Position = 0;

            await using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1048576, true)) await input.CopyToAsync(output, ct);

            ct.ThrowIfCancellationRequested(); File.Move(temp, item.File); items.Add(item); Save(items);

        } catch { if (File.Exists(temp)) File.Delete(temp); if (File.Exists(item.File)) File.Delete(item.File); throw; }

    }

    public bool CanUse(string query, HashSet<long> used, bool allowRecent = false, bool general = false) => Choose(List(), query, used, allowRecent, general) != null;

    PoolItem? Choose(List<PoolItem> items, string query, HashSet<long> used, bool allowRecent, bool general)

    {

        var cutoff = DateTimeOffset.UtcNow.AddHours(-store.Settings.PoolRepeatHours);

        return items.Where(x => !StockSearch.Blocked(store.Settings,x.Source) && File.Exists(x.File) && !used.Contains(x.Token) && (allowRecent || x.LastUsed == null || x.LastUsed <= cutoff) && (general ? Neutral(x) && !StockSearch.WeaponQuery(x.Tags + " " + x.Category) : Score(x, query) > 0 && StockSearch.Appropriate(query, x.Tags + " " + x.Category + " " + x.Source)))

            .OrderByDescending(x => Score(x, query)).ThenBy(x => x.Uses).ThenBy(x => x.LastUsed).FirstOrDefault(Healthy);

    }

    public bool TryUse(Scene scene, string query, string targetFolder, HashSet<long> used, bool allowRecent = false, bool general = false)

    {

        lock (Json.Gate(Index)) {

        var items = List(); var chosen = Choose(items, query, used, allowRecent, general);

        if (chosen == null) return false;

        var path = Path.Combine(targetFolder, "pool-" + chosen.Id + Path.GetExtension(chosen.File)); File.Copy(chosen.File, path, true);

        scene.StockFile = path; scene.StockMetadata = chosen.Tags + " " + chosen.Category + " " + chosen.Source; scene.StockSelectionVersion = StockSearch.SelectionVersion; scene.StockUrl = chosen.Source; scene.Creator = chosen.Creator; scene.LicenseUrl = chosen.License; scene.Attribution = chosen.Attribution; scene.RightsConfirmed = chosen.RightsConfirmed; scene.UserLocal = false;

        chosen.Uses++; chosen.LastUsed = DateTimeOffset.UtcNow; used.Add(chosen.Token); Save(items); return true;

        }

    }

    public void Delete(PoolItem item)

    {

        var folder = Path.GetFullPath(Folder) + Path.DirectorySeparatorChar;

        var path = Path.GetFullPath(item.File);

        if (!path.StartsWith(folder, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Havuz dışındaki dosya silinemez.");

        if (File.Exists(path)) File.Delete(path);

        lock (Json.Gate(Index)) { var items = List(); items.RemoveAll(x => x.Id == item.Id); Json.Write(Index, items); }

    }

    public record ScanResult(int Added, int Existing, int Duplicate, int Failed, int Remaining);

    public async Task<ScanResult> ScanFolder(CancellationToken ct, IProgress<(int Done, int Total, string Name)>? progress = null)

    {

        ct.ThrowIfCancellationRequested();

        Directory.CreateDirectory(Folder);

        if ((File.GetAttributes(Folder) & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("Havuz klasörü bir bağlantı olamaz.");

        var items = List();

        var paths = items.Select(x => Path.GetFullPath(x.File)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var hashes = items.Where(x => x.Hash != "").Select(x => x.Hash).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var supported = new[] { ".mp4", ".mov", ".mkv", ".webm" };

        var files = Directory.EnumerateFiles(Folder).Where(f => supported.Contains(Path.GetExtension(f).ToLowerInvariant()))

            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray();

        int added = 0, existing = paths.Count(p => File.Exists(p)), duplicate = 0, failed = 0;

        var scanIndex = Path.Combine(Folder, "scan-progress.json");

        var scanned = Json.Read(scanIndex, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        string Stamp(string file) { var info = new FileInfo(file); return info.Length + ":" + info.LastWriteTimeUtc.Ticks; }

        var pending = files.Where(f => !scanned.TryGetValue(f, out var stamp) || stamp.Replace("indexed:", "", StringComparison.Ordinal) != Stamp(f) || stamp.StartsWith("indexed:", StringComparison.Ordinal) && !paths.Contains(Path.GetFullPath(f))).ToArray();

        var selected = pending.Take(500).ToArray();

        for (int i = 0; i < selected.Length; i++)

        {

            ct.ThrowIfCancellationRequested(); var file = selected[i];

            try {

                if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) { failed++; continue; }

                await using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 1048576, true);

                var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false)); ct.ThrowIfCancellationRequested();

                await Reliability.ValidateVideo(store, file, ct).ConfigureAwait(false);

                var prior = items.FirstOrDefault(x => Path.GetFullPath(x.File).Equals(Path.GetFullPath(file), StringComparison.OrdinalIgnoreCase));

                if (prior != null) {

                    if (prior.Hash != hash) { prior.RightsConfirmed = false; prior.Source = "Kullanıcının havuz klasörü"; prior.Creator = "Kullanıcı arşivi"; prior.License = "Kullanım hakkı kullanıcı tarafından doğrulanmalı"; prior.Attribution = ""; }

                    prior.Hash = hash; prior.Validated = true; hashes.Add(hash); Save([prior]); continue;

                }

                if (!hashes.Add(hash)) { duplicate++; continue; }

                var tags = Path.GetFileNameWithoutExtension(file).Replace('_', ' ').Replace('-', ' ');

                var item = new PoolItem { File = Path.GetFullPath(file), Hash = hash, Tags = tags + "; yerel arşiv; local archive", Category = CategoryFor(tags), Source = "Kullanıcının havuz klasörü", Creator = "Kullanıcı arşivi", License = "Kullanım hakkı kullanıcı tarafından doğrulanmalı", GeneralBackground = true, Validated = true, RightsConfirmed = false };

                items.Add(item); Save(items); added++;

            } catch (Exception ex) when (ex is not OperationCanceledException) { failed++; var invalid = items.FirstOrDefault(x => x.File.Equals(file, StringComparison.OrdinalIgnoreCase)); if (invalid != null) { invalid.Validated = false; Save([invalid]); } }

            finally { if (!ct.IsCancellationRequested && File.Exists(file)) { scanned[file] = (items.Any(x => x.File.Equals(file, StringComparison.OrdinalIgnoreCase) && x.Validated) ? "indexed:" : "") + Stamp(file); Json.Write(scanIndex, scanned); } progress?.Report((i + 1, selected.Length, Path.GetFileName(file))); }

        }

        return new(added, existing, duplicate, failed, Math.Max(0, pending.Length - selected.Length));

    }

}

