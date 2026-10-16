using System.Security.Cryptography;



namespace AnkaStudio;



public sealed class AudioItem

{

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = "";

    public string File { get; set; } = "";

    public string Kind { get; set; } = "Müzik";

    public string Tags { get; set; } = "";

    public string Source { get; set; } = "";

    public string License { get; set; } = "";

    public string Attribution { get; set; } = "";

    public bool RightsConfirmed { get; set; }

    public string Hash { get; set; } = "";

}

public sealed class AudioLibrary(Store store)

{

    public string Folder => Path.Combine(store.Root, "audio-library");

    string Index => Path.Combine(Folder, "index.json");

    public List<AudioItem> List(bool refresh = false)
    {
        var items = Json.Read(Index, new List<AudioItem>());
        if (!refresh || !Directory.Exists(Folder)) return items;
        var known = items.Select(x => Path.GetFullPath(x.File)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        bool changed = false;
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint };
        foreach (var file in Directory.EnumerateFiles(Folder, "*", options))
        {
            if (!new[] { ".mp3", ".wav", ".m4a", ".flac", ".ogg", ".aac" }.Contains(Path.GetExtension(file).ToLowerInvariant())) continue;
            var full = Path.GetFullPath(file);
            if (!known.Add(full)) continue;
            items.Add(new AudioItem { Name = Path.GetFileNameWithoutExtension(file), File = full, Kind = "Müzik" });
            changed = true;
        }
        if (changed) Save(items);
        return items;
    }

    public void Save(List<AudioItem> items) => Json.Write(Index, items);

    public async Task<(int Added,int Failed)> Scan(string kind,IProgress<(int Done,int Total,string Name)> progress,CancellationToken ct)
    {
        Directory.CreateDirectory(Folder); var items=List();
        var known=items.Select(x=>Path.GetFullPath(x.File)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var files=Directory.EnumerateFiles(Folder).Where(f=>new[]{".mp3",".wav",".m4a",".flac",".ogg",".aac"}.Contains(Path.GetExtension(f).ToLowerInvariant()) && !known.Contains(Path.GetFullPath(f))).Take(500).ToArray();
        int added=0,failed=0,done=0;
        progress.Report((0,files.Length,"Hazırlanıyor"));
        foreach(var file in files){
            ct.ThrowIfCancellationRequested(); progress.Report((done,files.Length,Path.GetFileName(file)));
            try {
                if(new FileInfo(file).Length>100*1024*1024) throw new InvalidOperationException("Ses dosyası çok büyük.");
                if(await Media.Duration(store,file,ct)<=0) throw new InvalidOperationException("Ses okunamadı.");
                await using var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read,81920,true);
                var hash=Convert.ToHexString(await SHA256.HashDataAsync(stream,ct));
                if(!items.Any(x=>x.Hash==hash && File.Exists(x.File))) {
                    ct.ThrowIfCancellationRequested();
                    items.Add(new AudioItem {Name=Path.GetFileNameWithoutExtension(file),File=Path.GetFullPath(file),Kind=kind,Hash=hash,RightsConfirmed=false}); Save(items); added++;
                }
            }catch(Exception ex) when(ex is not OperationCanceledException){failed++;}
            progress.Report((++done,files.Length,Path.GetFileName(file)));
        }
        return(added,failed);
    }

    public async Task Add(string file, AudioItem metadata, CancellationToken ct)

    {

        var extension = Path.GetExtension(file).ToLowerInvariant();

        if (!new[] { ".mp3", ".wav", ".m4a", ".flac", ".ogg", ".aac" }.Contains(extension)) throw new InvalidOperationException("Desteklenmeyen ses biçimi.");

        if (new FileInfo(file).Length > 100 * 1024 * 1024) throw new InvalidOperationException("Bir ses dosyası en fazla 100 MB olabilir.");

        Directory.CreateDirectory(Folder);

        await using var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);

        var hash = Convert.ToHexString(await SHA256.HashDataAsync(input, ct));

        var items = List(); if (items.Any(x => x.Hash == hash && File.Exists(x.File))) return;

        var id = Guid.NewGuid().ToString("N"); var target = Path.Combine(Folder, id + extension); var temporary = target + ".part";

        try

        {

            input.Position = 0;

            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true)) await input.CopyToAsync(output, ct);

            ct.ThrowIfCancellationRequested(); File.Move(temporary, target);

            items.Add(new AudioItem { Id = id, Name = Path.GetFileNameWithoutExtension(file), File = target, Hash = hash, Kind = metadata.Kind, Tags = metadata.Tags, Source = metadata.Source, License = metadata.License, Attribution = metadata.Attribution, RightsConfirmed = metadata.RightsConfirmed });

            Save(items);

        }

        catch { if (File.Exists(temporary)) File.Delete(temporary); if (File.Exists(target)) File.Delete(target); throw; }

    }

}



public sealed partial class StudioForm

{

    string audioKind = "Müzik";

    int audioOffset;

    void AudioPage()

    {

        Title("Ses ve müzik arşivi", "Müzik, ses efekti ve dışarıdan seslendirmelerini tek yerde sakla.");

        var library = new AudioLibrary(store);

        var choices = new[] { "Müzik", "Ses efekti", "Seslendirme" }.Select(kind => Theme.Button(kind, () => { audioKind = kind; audioOffset = 0; Navigate("Ses arşivi"); }, audioKind == kind, 220)).ToArray();

        if(current!="Medya arşivleri")page.Controls.Add(Actions(choices));

        page.Controls.Add(Actions(Theme.Button("+ Ses dosyaları ekle", ImportAudio, true, 230), Theme.Button("Klasörü tara / listeyi güncelle", ScanAudioFolder, false, 320), Theme.Button("Arşiv klasörünü aç", () => { Directory.CreateDirectory(library.Folder); Theme.Open(library.Folder); }, false, 230)));

        page.Controls.Add(Theme.Label("Müzik kullanım haklarını kontrol et. İzinsiz kullanım YouTube üzerinde telif talebine veya videonun engellenmesine yol açabilir. Kullanım sorumluluğu kullanıcıya aittir.", 10, color: Theme.Warning, width: page.Width, height: 80));

        var items = library.List(refresh: true).Where(x => x.Kind == audioKind).ToList();

        page.Controls.Add(Theme.Label($"{audioKind} • {items.Count} dosya", 14, true, width: page.Width, height: 44));

        page.Controls.Add(Actions(Theme.Button("Önceki 20", () => { audioOffset = Math.Max(0, audioOffset - 20); Navigate("Ses arşivi"); }), Theme.Button("Sonraki 20", () => { if (audioOffset + 20 < items.Count) audioOffset += 20; Navigate("Ses arşivi"); })));

        foreach (var item in items.Skip(audioOffset).Take(20))

        {

            var card = Theme.Card(page.Width, 230); card.Tag = "wide";

            var title = Theme.Label(item.Name, 15, true, width: page.Width - 40, height: 38); title.Location = new(20, 12);

            var info = Theme.Label(item.Kind == "Müzik" ? $"Etiket: {item.Tags}" : $"Etiket: {item.Tags}\nKaynak: {item.Source}\nLisans: {item.License}\nAtıf: {item.Attribution}", 9, width: page.Width - 40, height: 94); info.Location = new(20, 52);

            var rights = Theme.Label(item.RightsConfirmed ? "Kullanım izni kullanıcı tarafından doğrulandı" : "Kullanım izni doğrulanmadı", 9, color: item.RightsConfirmed ? Theme.Teal : Theme.Warning, width: page.Width - 40); rights.Location = new(20, 146);

            var actions = new FlowLayoutPanel { Location = new(20, 182), Width = page.Width - 40, Height = 48, BackColor = Color.Transparent };

            actions.Controls.Add(Theme.Button("Dinle", () => { if (File.Exists(item.File)) Theme.Open(item.File); else Message("Dosya bulunamadı."); }, false, 130));

            actions.Controls.Add(Theme.Button(item.Kind == "Müzik" ? "Bilgi" : "Bilgi / lisans", () => { EditAudio(item, library); Navigate("Ses arşivi"); }, false, 170));

            if (item.Kind == "Müzik") actions.Controls.Add(Theme.Button("Üretimde kullan", () => {

                if (engine.Busy) { Message("Üretim bitince müziği değiştirebilirsin."); return; }


                if (!File.Exists(item.File)) { Message("Dosya bulunamadı."); return; }

                store.Settings.MusicPath = item.File; store.Settings.LibraryMusic = new MusicTrack { Enabled = true, File = item.File, Source = item.Source, License = item.License, Attribution = item.Attribution, RightsConfirmed = item.RightsConfirmed }; store.Save(); Message("Müzik seçildi. Paket ayarlarında müziği aç.");

            }, true, 190));

            card.Controls.AddRange([title, info, actions]); if (item.Kind != "Müzik") card.Controls.Add(rights); page.Controls.Add(card);

        }

    }

    void EditAudio(AudioItem item, AudioLibrary library)

    {

        using var dialog = Dialog("Ses bilgisi ve kullanım izni", 850, 700); var flow = Theme.Flow(760); flow.Dock = DockStyle.Fill; flow.AutoScroll = true; dialog.Controls.Add(flow);

        var tags = Theme.Input(item.Tags, width: 700); Field(flow, "Etiketler / tarz / ruh hâli", tags);

        MusicUsage? usage = null; TextBox source, license, attribution; CheckBox confirmed;

        if (item.Kind == "Müzik") { usage = new MusicUsage(item.Source, item.License, item.Attribution, item.RightsConfirmed, 700); flow.Controls.Add(usage); source = usage.Source; license = usage.License; attribution = usage.Attribution; confirmed = usage.Rights; }

        else { source = Theme.Input(item.Source, width: 700); Field(flow, "Ses kaynağı", source); license = Theme.Input(item.License, width: 700); Field(flow, "Ses kullanım izni", license); attribution = Theme.Input(item.Attribution, true, 700); Field(flow, "Gerekli atıf", attribution); confirmed = Theme.Check("Bu sesi kullanma hakkımı doğruladım", item.RightsConfirmed); flow.Controls.Add(confirmed); }

        flow.Controls.Add(Theme.Button("Kaydet", () => { usage?.Commit(); if (item.Kind != "Müzik" && confirmed.Checked && (source.Text.Trim() == "" || license.Text.Trim() == "")) { Message("Kaynak ve lisans bilgisi gerekli."); return; } item.Tags = tags.Text.Trim(); item.Source = source.Text.Trim(); item.License = license.Text.Trim(); item.Attribution = attribution.Text.Trim(); item.RightsConfirmed = confirmed.Checked; var all = library.List(); var index = all.FindIndex(x => x.Id == item.Id); if (index >= 0) { all[index] = item; library.Save(all); } dialog.DialogResult = DialogResult.OK; }, true, 230)); dialog.ShowDialog(this);

    }

    void ImportAudio()

    {

        using var picker = new OpenFileDialog { Multiselect = true, Filter = "Ses dosyaları|*.mp3;*.wav;*.m4a;*.flac;*.ogg;*.aac" };

        if (picker.ShowDialog(this) != DialogResult.OK) return;

        if (picker.FileNames.Length > 20) { Message("Tek seferde en fazla 20 dosya seç. Her dosya en fazla 100 MB olabilir."); return; }

        using var dialog = Dialog("Ses arşivine aktar", 770, 370); var flow = Theme.Flow(680); flow.Dock = DockStyle.Fill; dialog.Controls.Add(flow);

        flow.Controls.Add(Theme.Label($"{picker.FileNames.Length} dosya → {audioKind}. Kaynak dosyalar korunur. Eklenen dosyalar otomatik arşive kaydedilir.", 11, width: 680, height: 70));

        var status = Theme.Label("Hazır", 10, width: 680, height: 55); flow.Controls.Add(status);

        using var cancellation = new CancellationTokenSource(); bool busy = false;

        dialog.FormClosing += (_, e) => { if (busy) { cancellation.Cancel(); e.Cancel = true; status.Text = "Durduruluyor…"; } };

        Button? start = null;

        start = Theme.Button("Aktarımı başlat", () => _ = Run(), true, 230); flow.Controls.Add(start); flow.Controls.Add(Theme.Button("Durdur", () => cancellation.Cancel(), false, 180));

        async Task Run()

        {

            if (busy) return; busy = true; start!.Enabled = false; var errors = new List<string>(); int count = 0;

            try { foreach (var file in picker.FileNames) { cancellation.Token.ThrowIfCancellationRequested(); status.Text = $"{++count}/{picker.FileNames.Length} • {Path.GetFileName(file)}"; try { await new AudioLibrary(store).Add(file, new AudioItem { Kind = audioKind }, cancellation.Token); } catch (Exception ex) when (ex is not OperationCanceledException) { errors.Add(Path.GetFileName(file) + ": " + ex.Message); } } }

            catch (OperationCanceledException) { errors.Add("Aktarım durduruldu. Tamamlanan dosyalar saklandı."); }

            finally { busy = false; dialog.Close(); if (errors.Count > 0) Message(string.Join("\n", errors)); }

        }

        dialog.ShowDialog(this); Navigate("Ses arşivi");

    }

}



