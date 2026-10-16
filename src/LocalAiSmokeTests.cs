using System.Text.Json;



namespace AnkaStudio;



public static class LocalAiSmokeTests

{

    public static async Task Run(string[] args)

    {

        var root = Path.GetFullPath(Program.Arg(args, "--data") ?? throw new InvalidOperationException("Ayrı test klasörü gerekli."));

        if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any()) throw new InvalidOperationException("Yerel AI testi boş, ayrı klasörde çalıştırılmalı.");

        var store = new Store(root); store.Settings.OllamaModel = Program.Arg(args, "--local-model") ?? "qwen3:8b";

        store.Settings.CloudAiEnabled = false; store.Settings.LocalAiFirst = true;

        using var cancel = new CancellationTokenSource(TimeSpan.FromMinutes(4));

        var logs = new List<string>(); var services = new Services(store, text => { logs.Add(text); File.AppendAllText(Path.Combine(root, "live-log.txt"), text + Environment.NewLine); });

        var episode = new Episode { Topic = "Doğada kısa bir mola • yerel test", IncludeLong = false, ShortCount = 1 };

        var sources = new List<SourceRef> { new() { Title = "Yerel test kaynağı A", Url = "https://example.com/a" }, new() { Title = "Yerel test kaynağı B", Url = "https://example.com/b" } };

        var watch = System.Diagnostics.Stopwatch.StartNew();

        var raw = await services.Generate("Türkçe bir Shorts senaryosu yaz. Yalnız JSON döndür. Long null, Shorts tam bir öğe olsun. Her sahnede Narration, VisualQuery, OnScreen olsun. Dini veya bilimsel iddia ekleme.", "Konu: doğada kısa mola. İki sahne, her sahnede 10–20 Türkçe kelime. Alanlar Research, Warnings, Long, Shorts. Shorts alanları Title, Description, ThumbnailText, Tags, Scenes. VisualQuery İngilizce, noktalı virgülle ayrılmış forest; sea; sky gibi kısa aramalar olsun.", cancel.Token, text => { Services.ValidateGeneratedPlan(text, episode, sources); return true; }, true, maxOutputTokens: 2048, expectedShorts: 1, expectedLong: false);

        episode.Plan = JsonSerializer.Deserialize<ContentPlan>(Services.ExtractJson(raw), Json.Options)!; episode.Plan.Sources = sources;

        var planSeconds = watch.Elapsed.TotalSeconds; Json.Write(Path.Combine(root, "generated-plan.json"), episode.Plan);

        await services.Proofread(episode, cancel.Token); watch.Stop();

        Json.Write(Path.Combine(root, "local-ai-live-report.json"), new { Model = store.Settings.OllamaModel, PlanPassed = true, ProofreadingPassed = true, CloudEnabled = store.Settings.CloudAiEnabled, PlanSeconds = planSeconds, TotalSeconds = watch.Elapsed.TotalSeconds, Logs = logs });

    }

}

