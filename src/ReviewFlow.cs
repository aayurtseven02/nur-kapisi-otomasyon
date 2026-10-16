namespace AnkaStudio;



public sealed partial class StudioForm

{

    void WatchEpisode(Episode episode)
    {
        var videos = episode.Videos.Where(v => File.Exists(v.File)).OrderBy(v => v.Short).ThenBy(v => v.Key).ToArray();
        if (videos.Length == 0) { Message("İzlemek için hazır video bulunamadı. Önce video üretimini tamamla."); return; }
        if (videos.Length == 1) { Theme.Open(videos[0].File); return; }
        using var preview = Dialog("Videoyu izle • yayın öncesi kontrol", 840, 540);
        var list = Theme.Flow(740); list.Dock = DockStyle.Fill; list.AutoScroll = true; preview.Controls.Add(list);
        list.Controls.Add(Theme.Label("İzlemek istediğin videoyu seç.", 11, width: 700, height: 50));
        foreach (var video in videos) {
            var selected = video;
            list.Controls.Add(Theme.Button((selected.Short ? "SHORTS" : "NORMAL VİDEO") + " • " + selected.Title, () => Theme.Open(selected.File), false, 700));
        }
        preview.ShowDialog(this);
    }

    void Review(Episode episode, string? capture = null)

    {

        using var dialog = Dialog("Yayın öncesi denetim", 1040, 850);

        dialog.MinimumSize = new(920, 700);

        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };

        var flow = Theme.Flow(950); scroll.Controls.Add(flow); dialog.Controls.Add(scroll);

        void Card(string heading, string body, bool warning, params Button[] actions)

        {

            var height = Math.Max(70, (body.Length / 95 + body.Count(x => x == '\n') + 2) * 24);

            var card = Theme.Card(920, height + 112);

            if (card is StudioCard stateCard) stateCard.Completed = !warning;

            var title = Theme.Label((warning ? "" : "✓ ") + heading, 14, true, warning ? Theme.Warning : Color.Black, 870, 40); title.Location = new(20, 12);

            var text = Theme.Label(body, 10, color: warning ? Theme.Warning : Color.Black, width: 870, height: height); text.Location = new(20, 55);

            var row = new FlowLayoutPanel { Location = new(20, height + 60), Width = 880, Height = 50, BackColor = Color.Transparent }; row.Controls.AddRange(actions);

            card.Controls.AddRange([title, text, row]); flow.Controls.Add(card);

        }

        void Build()

        {

            foreach (var control in flow.Controls.Cast<Control>().ToArray()) control.Dispose(); flow.Controls.Clear();

            flow.Controls.Add(Theme.Label("Yayın öncesi denetim", 23, true, width: 920, height: 58));

            flow.Controls.Add(Theme.Label("Videoları izle, kaynakları kontrol et ve uyarıları çöz. Onay tek başına YouTube'a yüklemez.", 10, width: 920, height: 50));

            foreach (var asset in episode.Videos)

                Card((asset.Short ? "SHORTS" : "NORMAL VİDEO") + " • " + asset.Title, $"{asset.Duration:0} saniye • " + (asset.QualityPassed ? "Teknik kontrol geçti" : "Teknik kontrol tamamlanmalı") + "\n" + string.Join("\n", asset.Checks), !asset.QualityPassed,

                    Theme.Button("Videoyu izle", () => Theme.Open(asset.File), false, 180), Theme.Button("Altyazıyı incele", () => Theme.Open(Path.Combine(Path.GetDirectoryName(asset.File)!, "subtitles.srt")), false, 190), Theme.Button("Kapağı incele", () => Theme.Open(asset.Thumbnail), false, 180));

            if (episode.Plan != null) foreach (var (_, _, script) in Rules.Scripts(episode)) {

                var suggestions = YouTubeEditorial.Check(script);

                Card("Başlık / kapak uyumu • " + script.Title, suggestions.Count == 0 ? "Başlık ve kısa kapak metninde belirgin bir tekrar veya uzunluk sorunu bulunmadı. Bu kontrol izlenme garantisi vermez." : string.Join("\n", suggestions) + "\nBunlar öneridir; yayın onayını engellemez.", suggestions.Count > 0);

            }

            if (episode.Plan != null) foreach (var warning in episode.Plan.Warnings.ToArray())

                Card("Editoryal uyarı • inceleme gerekli", warning, true, Theme.Button("İncele / çözüm kaydet", () => {

                    using var resolve = Dialog("Uyarı çözümü", 860, 630); var content = Theme.Flow(770); content.Dock = DockStyle.Fill; content.AutoScroll = true; resolve.Controls.Add(content);

                    content.Controls.Add(Theme.Label(warning, 11, color: Theme.Warning, width: 760, height: 160));

                    var note = Theme.Input("", true, 740); Field(content, "Kontrol ettiğin kaynak / yaptığın inceleme ve çözüm notu", note);

                    var confirmed = Theme.Check("Uyarıyı inceledim; mevcut içerikte gerekli kontrolü yaptım", false, 750); content.Controls.Add(confirmed);

                    content.Controls.Add(Theme.Label("Bu işlem video veya senaryoyu değiştirmez. İçerik değişmesi gerekiyorsa önce sahne editöründen düzelt; medya yeniden üretilmelidir.", 10, color: Theme.Warning, width: 750, height: 75));

                    content.Controls.Add(Theme.Button("Çözümü kaydet • videoları koru", () => {

                        if (!confirmed.Checked || note.Text.Trim().Length < 10) { Message("İnceleme onayı ve en az 10 karakterlik çözüm notu gerekli."); return; }

                        WarningResolution.Resolve(episode, warning, note.Text); store.Save(); resolve.DialogResult = DialogResult.OK;

                    }, true, 380)); if (resolve.ShowDialog(dialog) == DialogResult.OK) Build();

                }, true, 300));

            var lastReset = episode.Events.FindLastIndex(x => x.Contains("Sahne incelemesi sıfırlandı."));

            foreach (var resolved in episode.Events.Skip(lastReset + 1).Where(x => x.Contains("Uyarı incelendi: ")).Distinct()) {

                var detail = resolved[(resolved.IndexOf("Uyarı incelendi: ", StringComparison.Ordinal) + "Uyarı incelendi: ".Length)..];

                var split = detail.IndexOf(" | Çözüm notu: ", StringComparison.Ordinal);

                if (split >= 0 && episode.Plan != null && !episode.Plan.Warnings.Contains(detail[..split]))

                    Card("Editoryal uyarı • inceleme tamamlandı", detail.Replace(" | Çözüm notu: ", "\nÇözüm notu: "), false);

            }

            if (episode.Plan?.Warnings.Count == 0) Card("Editoryal uyarılar çözüldü", "Medya dosyaları korunur. Son onay için videoların teknik kontrolleri de geçmiş olmalı.", false);

            if (episode.Plan != null && Rules.Scripts(episode).SelectMany(x => x.Script.Scenes).Any(s => !s.RightsConfirmed))

                Card("Yerel görüntü kullanım hakları", "Kaynak ve lisansları kontrol edip bu videoları kanalda kullanma hakkın olduğunu doğrula. Bu teyit teknik kalite kontrolünden ayrıdır.", true,

                    Theme.Button("Görüntü haklarını kontrol ettim / onayla", () => { if (engine.Busy) { Message("Çalışan işlem tamamlanmalı."); return; } if (Theme.Alert(dialog, "Bu paketteki yerel görüntüler için kullanım hakkını doğruluyor musun?", "Hak teyidi", MessageBoxButtons.YesNo) != DialogResult.Yes) return; foreach (var scene in Rules.Scripts(episode).SelectMany(x => x.Script.Scenes)) scene.RightsConfirmed = true; episode.Log("Görüntü kullanım hakları kullanıcı tarafından teyit edildi."); store.Save(); Build(); }, false, 420));

            if (episode.Plan != null && Rules.Scripts(episode).Any() && Rules.Scripts(episode).SelectMany(x => x.Script.Scenes).All(s => s.RightsConfirmed))

                Card("Görüntü kullanım hakları teyit edildi", "Bu paketin görüntüleri için kullanım hakkı teyidi kaydedildi.", false);

            var blockers = new List<string>();

            if (episode.Plan != null && Rules.Scripts(episode).SelectMany(x => x.Script.Scenes).Any(s => !s.RightsConfirmed)) blockers.Add("Yerel görüntü kullanım hakları teyit edilmeli.");

            if (episode.Demo) blockers.Add("Örnek paketler yayınlanamaz.");

            if (!Reliability.OutputsProduced(episode)) blockers.Add("Video çıktısı eksik veya kaydedilen düzen henüz uygulanmadı; Videoları üret adımını tamamla.");

            else if (episode.Videos.Any(v => Reliability.FileHash(v.File) != v.FileHash)) blockers.Add("Video dosyası üretimden sonra değişmiş; yeniden üretim gerekli.");

            foreach (var asset in episode.Videos.Where(x => !x.QualityPassed)) {

                var details = asset.Checks.Where(c => c.Contains("Süre kontrolü") || c.Contains("aşıyor") || c.Contains("ÖRNEK")).ToArray();

                blockers.Add(asset.Title + ": " + (details.Length > 0 ? string.Join("; ", details) : "Kalite kontrolünden geçmedi; video kartındaki kontrolleri incele."));

            }

            if (episode.Plan?.Warnings.Count > 0) blockers.Add("Yukarıdaki editoryal uyarıları kaynakları inceleyerek çöz.");

            Card(blockers.Count == 0 ? "Onay için hazır" : "Onaydan önce tamamlanacaklar", blockers.Count == 0 ? "İçeriği, ses akışını, altyazıları ve kullanım izinlerini kontrol ettiysen onaylayabilirsin." : string.Join("\n", blockers), blockers.Count > 0,

                Theme.Button("İçeriği ve medyayı onayla", () => { try { engine.Approve(episode); dialog.Close(); Navigate(current); } catch (Exception ex) { Message(ex.Message); } }, true, 340));

        }

        Build();

        if (capture != null) dialog.Shown += (_, _) => { Application.DoEvents(); using var image = new Bitmap(dialog.Width, dialog.Height); dialog.DrawToBitmap(image, new Rectangle(Point.Empty, dialog.Size)); image.Save(capture); scroll.AutoScrollPosition = new Point(0, flow.Height); Application.DoEvents(); using var bottom = new Bitmap(dialog.Width, dialog.Height); dialog.DrawToBitmap(bottom, new Rectangle(Point.Empty, dialog.Size)); bottom.Save(capture + "-bottom.png"); dialog.Close(); };

        dialog.ShowDialog(this);

    }

}

