using System.Diagnostics;

using System.Drawing.Drawing2D;

using System.Globalization;

using System.Text.Json;



namespace AnkaStudio;



public sealed class PackageDialog : Form

{

    internal const string ShortsModeHelp="Bağlantılı Shorts: Normal videoyla aynı konu içinde, onu tamamlayan kısa içerikler hazırlanır.\n\nBağımsız Shorts: Aynı konu paketinde, normal videoyu izlemeden kendi başına anlaşılabilen kısa içerikler hazırlanır.\n\nBu seçim içerik yaklaşımını belirler; tek başına YouTube yükleme veya yayın başlatmaz. Shorts sayısını 0 seçersen kısa video üretilmez.";

    public Episode Episode { get; private set; }

    public PackageDialog(Episode? original = null, Store? store = null)

    {

        Episode = original == null ? new() : JsonSerializer.Deserialize<Episode>(JsonSerializer.Serialize(original, Json.Options), Json.Options)!;

        Text = "İçerik paketi • normal video + Shorts"; Width = 880; Height = 850; StartPosition = FormStartPosition.CenterParent; BackColor = Theme.Bg; ForeColor = Theme.Text; Padding = new Padding(24); Font = Theme.Font();

        var outer = new Panel { Dock = DockStyle.Fill, AutoScroll = true }; var f = Theme.Flow(790); outer.Controls.Add(f); Controls.Add(outer);

        f.Controls.Add(Theme.Label("Video projesi", 20, true, Theme.Teal, 770, 45));

        var topic = Theme.Input(Episode.Topic, width: 740); Field("Konu", topic); var brief = Theme.Input(Episode.Brief, true, 740); Field("İçerik talimatı / kanalın yaklaşımı", brief);

        var style = Theme.Combo(["Belgesel", "Bilgilendirici", "Hikâye anlatımı", "Haber / analiz", "Sinematik", "Hızlı ve dinamik"], Episode.Style, 740); Field("Anlatım tarzı", style);

        var normal = Theme.Check("Normal video üret • 16:9", Episode.IncludeLong); f.Controls.Add(normal); var minutes = Theme.Number(Episode.LongMinutes, 1, 30, 740); Field("Normal video hedef süresi (dakika)", minutes);

        var shorts = Theme.Number(Episode.ShortCount, 0, 6, 740); Field("Shorts sayısı • 0 seçilirse yalnızca normal video", shorts); var seconds = Theme.Number(Episode.ShortSeconds, 15, 180, 740); Field("Her Shorts hedef süresi (saniye)", seconds);

        var mode = Theme.Combo(["Bağlantılı Shorts", "Bağımsız Shorts"], Episode.ShortMode, 740); Field("Shorts üretim şekli", mode); var music = Theme.Check("Lisanslı müzik kullan • Yönetim ayarlarındaki dosya", Episode.Music); f.Controls.Add(music);

        var own = Episode.UserNarration ?? new UserNarrationOptions();
        var story = Theme.Check("Hikâyeyi AI görselleriyle canlandır • aynı karakter referansı", Episode.AiStoryImages); f.Controls.Add(story);
        var storyLocal = Theme.Check("Yerel motoru kullan • bulut görsel API çağrısı yapılmaz",Episode.AiStoryLocal); f.Controls.Add(storyLocal);
        var storyStyle = Theme.Input(Episode.AiStoryStyle, width:740); Field("Hikâye görsel tarzı",storyStyle);
        var characters = Theme.Input(Episode.AiStoryCharacters,true,740); Field("Sabit karakterler • ad, yüz, yaş, saç/sakal, kıyafet ve renkler",characters);
        var motion = Theme.Check("Görselleri hareketlendir",Episode.AiStoryMotion); f.Controls.Add(motion);
        var cinema=Theme.Check("Sinematik animasyon • sahneye göre kamera ve atmosfer",Episode.AiStoryCinematic);f.Controls.Add(cinema);
        var autonomous=Theme.Check("Durmadan üret • görselleri bitince videoda incele",Episode.AiStoryAutonomous);f.Controls.Add(autonomous);
        var storyHelp=Theme.Label("Sabit karakter referansı korunur. Sinematik hareket fotoğrafı canlandırır; yürüyüş veya dudak animasyonu üretmez. Durmadan üret seçilirse son videoyu önizleyip onaylarsın.",9,width:740,height:70);f.Controls.Add(storyHelp);
        var storyGroup=CompactUi.Group(f,"Hikâye görselleri ve animasyon",storyLocal,storyHelp,Episode.AiStoryImages);
        void StoryOptions(){storyGroup.Enabled=story.Checked;if(story.Checked)storyGroup.SetExpanded(true);cinema.Enabled=motion.Checked;}
        story.CheckedChanged+=(_,_)=>StoryOptions();motion.CheckedChanged+=(_,_)=>StoryOptions();StoryOptions();

        var useOwn = Theme.Check("Hazır metnimi kullan • yapay zekâ içerik yazmasın", own.Enabled); f.Controls.Add(useOwn);

        f.Controls.Add(Theme.Button("Hazır metin editörünü aç", () => { using var editor = new UserNarrationEditor(own, normal.Checked, (int)shorts.Value,store); if (editor.ShowDialog(this) == DialogResult.OK) { own = editor.Value; useOwn.Checked = true; } }, false, 280));

        void OwnDuration() { minutes.Enabled = seconds.Enabled = !useOwn.Checked; }

        useOwn.CheckedChanged += (_, _) => OwnDuration(); OwnDuration();

        f.Controls.Add(Theme.Label("Hazır metinde süre metin uzunluğuna ve seçilen sesin hızına göre oluşur; metin otomatik uzatılmaz / kısaltılmaz.", 10, width: 740, height: 55));

        var save = Theme.Button("Paketi kaydet", () => { Episode.AiStoryImages=story.Checked; Episode.AiStoryLocal=storyLocal.Checked; Episode.PhotosOnly=story.Checked ? false : Episode.PhotosOnly; Episode.AiStoryStyle=storyStyle.Text.Trim(); Episode.AiStoryCharacters=characters.Text.Trim(); Episode.AiStoryMotion=motion.Checked; Episode.AiStoryCinematic=cinema.Checked; Episode.AiStoryAutonomous=autonomous.Checked; own.Enabled = useOwn.Checked; Episode.UserNarration = own; Episode.Topic = topic.Text.Trim(); Episode.Brief = brief.Text.Trim(); Episode.Style = style.Text; Episode.IncludeLong = normal.Checked; Episode.LongMinutes = (int)minutes.Value; Episode.ShortCount = (int)shorts.Value; Episode.ShortSeconds = (int)seconds.Value; Episode.ShortMode = mode.Text; Episode.Music = music.Checked; try { Rules.Validate(Episode); DialogResult = DialogResult.OK; Close(); } catch (Exception e) { Theme.Alert(e.Message); } }, true, 250); f.Controls.Add(save);

        var shortsGroup=CompactUi.Group(f,"Shorts seçenekleri",CompactUi.LabelFor(f,shorts),mode,Episode.ShortCount>0);
        var minutesLabel=CompactUi.LabelFor(f,minutes);
        void FitVideoOptions() { minutes.Visible=minutesLabel.Visible=normal.Checked; if(!normal.Checked) shortsGroup.SetExpanded(true); }
        normal.CheckedChanged+=(_,_)=>FitVideoOptions(); FitVideoOptions();
        var ownedHelp=f.Controls.OfType<Label>().FirstOrDefault(l=>l.Text.StartsWith("Hazır metinde süre",StringComparison.Ordinal));
        if(ownedHelp!=null) { ownedHelp.Visible=useOwn.Checked; useOwn.CheckedChanged+=(_,_)=>ownedHelp.Visible=useOwn.Checked; }
        ModernScrollbars.Apply(this);

        void Field(string name, Control c) {

            if(name=="Shorts üretim şekli") {

                var row=new Panel{Width=740,Height=29,Margin=Padding.Empty};row.Controls.Add(Theme.Label(name,9,true,Theme.Muted,700,29));

                var info=Theme.Button("i",()=>Theme.Alert(this,ShortsModeHelp,"Shorts üretim şekli"),false,30);info.Size=new(30,27);info.Location=new(705,0);info.AccessibleName="Shorts üretim şekli hakkında bilgi";info.AccessibleDescription=ShortsModeHelp;row.Controls.Add(info);f.Controls.Add(row);

            }else f.Controls.Add(Theme.Label(name, 9, true, Theme.Muted, 740, 29));

            c.Margin = new Padding(0, 0, 0, 13); f.Controls.Add(c);

        }

    }

}



public sealed class SceneEditor : Form

{

    protected override void OnShown(EventArgs e) {ModernScrollbars.Apply(this);base.OnShown(e);}

    public ContentPlan Plan { get; private set; }

    readonly Episode episode;

    readonly ComboBox select;

    readonly TextBox title, thumbnail, description;

    readonly DataGridView grid;

    VideoScript? active;

    readonly Stack<string> undo=[]; readonly Stack<string> redo=[]; bool restoringHistory;

    public SceneEditor(Episode e, Store store)

    {

        episode = e; Plan = JsonSerializer.Deserialize<ContentPlan>(JsonSerializer.Serialize(e.Plan, Json.Options), Json.Options)!;

        var warnings = Theme.Input(string.Join("\r\n", Plan.Warnings), true, 1100);

        Text = "Sahne editörü • " + e.Topic; Width = 1250; Height = 920; MinimumSize=new(1060,820); StartPosition = FormStartPosition.CenterParent; BackColor = Theme.Bg; ForeColor = Theme.Text; Font = Theme.Font(); Padding = new Padding(24);

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 100, WrapContents = true };

        var scripts = new List<VideoScript>(); if (Plan.Long != null) scripts.Add(Plan.Long); scripts.AddRange(Plan.Shorts);

        select = Theme.Combo(scripts.Select((s, i) => (Plan.Long != null && i == 0 ? "NORMAL • " : "SHORTS • ") + s.Title).ToArray(), "", 600);

        toolbar.Controls.Add(select); toolbar.Controls.Add(Theme.Button("Değişiklikleri kaydet", Save, true, 250));

        toolbar.Controls.Add(Theme.Button("Başlık / kapak kontrolü", () => { Commit(); if (active == null) return; var notes = YouTubeEditorial.Check(active); Theme.Alert(this, notes.Count == 0 ? "Belirgin uzunluk veya tekrar sorunu bulunmadı. Bu kontrol izlenme garantisi vermez." : string.Join("\n", notes) + "\nBunlar öneridir; üretimi engellemez.", "Başlık / kapak kontrolü"); }, false, 260)); Controls.Add(toolbar);

        var fields = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 210, FlowDirection = FlowDirection.TopDown, WrapContents = false };

        title = Theme.Input(width: 1100); thumbnail = Theme.Input("", true, 1100); thumbnail.Height = 55; description = Theme.Input("", true, 1100); description.Height = 55;

        fields.Controls.AddRange([Theme.Label("Başlık", 9, true, Theme.Muted, 1100, 22), title, Theme.Label("Kapak metni", 9, true, Theme.Muted, 1100, 22), thumbnail, Theme.Label("Açıklama", 9, true, Theme.Muted, 1100, 22), description]); Controls.Add(fields);

        grid = StudioForm.Grid(); grid.Dock = DockStyle.Fill; grid.AllowUserToAddRows = true; grid.AllowUserToDeleteRows = true; grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;

        grid.Columns.Add("Narration", "Türkçe seslendirme metni"); grid.Columns.Add("Visual", "Stok araması • alternatifler ; ile"); grid.Columns.Add("Screen", "Sahne / kapak vurgusu"); grid.Columns.Add("Local", "Yerel görüntü dosyası"); grid.Columns.Add("Sources","Bu sahnenin kaynak URL’leri"); grid.Columns[0].FillWeight = 250;

        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;

        grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;

        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;

        grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;

        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 300, FlowDirection = FlowDirection.TopDown, WrapContents = false };

        async void ChooseVideo(bool fromPool) {

            if (grid.CurrentRow == null || grid.CurrentRow.IsNewRow) return; Remember();

            using var dialog = SceneVideoChoice.Dialog(store, fromPool);

            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            var row=grid.CurrentRow;var script=active;

            try { await MediaFormat.Video(store,dialog.FileName,script != Plan.Long,CancellationToken.None); } catch(Exception ex){if(!IsDisposed)Theme.Alert(this,ex.Message);return;}

            if(IsDisposed || active!=script || row.DataGridView!=grid)return;

            var selected = row.Tag as Scene ?? new Scene();

            var item = fromPool ? new MediaPool(store).List().FirstOrDefault(p => string.Equals(Path.GetFullPath(p.File), Path.GetFullPath(dialog.FileName), StringComparison.OrdinalIgnoreCase)) : null;

            SceneVideoChoice.Apply(selected, dialog.FileName, item); row.Cells[3].Value = dialog.FileName; row.Tag = selected;

        }

        void Remember(){if(restoringHistory || active==null)return;CommitCore(false);string json=JsonSerializer.Serialize(Plan,Json.Options);if(undo.Count==0 || undo.Peek()!=json){undo.Push(json);if(undo.Count>30){var recent=undo.Take(30).Reverse().ToArray();undo.Clear();foreach(var entry in recent)undo.Push(entry);}redo.Clear();}}

        void Restore(bool backwards){var source=backwards?undo:redo;var destination=backwards?redo:undo;if(source.Count==0)return;Commit();destination.Push(JsonSerializer.Serialize(Plan,Json.Options));int index=select.SelectedIndex;restoringHistory=true;try{Plan=JsonSerializer.Deserialize<ContentPlan>(source.Pop(),Json.Options)!;scripts.Clear();if(Plan.Long!=null)scripts.Add(Plan.Long);scripts.AddRange(Plan.Shorts);active=scripts[index];LoadScript();}finally{restoringHistory=false;}}

        grid.CellBeginEdit+=(_,_)=>Remember();grid.UserDeletingRow+=(_,_)=>Remember();foreach(var field in new[]{title,thumbnail,description})field.Enter+=(_,_)=>Remember();

        var historyActions=new FlowLayoutPanel{Width=1100,Height=50,WrapContents=false};historyActions.Controls.Add(Theme.Button("Geri al",()=>Restore(true),false,140));historyActions.Controls.Add(Theme.Button("Yinele",()=>Restore(false),false,140));

        historyActions.Controls.Add(Theme.Button("Değişiklik özeti",()=>{Commit();int oldCount=Rules.Scripts(e).Sum(x=>x.Script.Scenes.Count);int newCount=scripts.Sum(x=>x.Scenes.Count);Theme.Alert(this,$"Sahne sayısı: {oldCount} → {newCount}.\nMetin, başlık, görüntü veya kaynak değişiklikleri kaydettiğinde uygulanır.");},false,230));historyActions.WrapContents=true;historyActions.Height=100;

        historyActions.Controls.Add(Theme.Button("Görüntüyü izle",()=>{if(grid.CurrentRow!=null)Theme.Open(grid.CurrentRow.Cells[3].Value?.ToString()??"");},false,170));

        historyActions.Controls.Add(Theme.Button("Stok seçimini engelle",()=>{Commit();var selected=grid.CurrentRow?.Tag as Scene;if(selected==null || selected.StockUrl=="" || selected.UserLocal){Theme.Alert(this,"Otomatik stok kaynağı olan bir sahne seç.");return;}store.Settings.ExcludedStockUrls=string.Join("\n",store.Settings.ExcludedStockUrls.Split('\n').Append(selected.StockUrl).Where(x=>x!="").Distinct());store.Save();Theme.Alert(this,"Bu kaynak sonraki otomatik seçimlerde kullanılmayacak. Mevcut sahne için başka görüntü seç.");},false,220));bottom.Controls.Add(historyActions);

        var videoActions = new FlowLayoutPanel { Width = 1100, Height = 50, WrapContents = false };

        videoActions.Controls.Add(Theme.Button("Seçili sahneye havuzdan video seç", () => ChooseVideo(true), false, 350));

        videoActions.Controls.Add(Theme.Button("Farklı kaynaktan video seç", () => ChooseVideo(false), false, 300)); bottom.Controls.Add(videoActions);

        warnings.Height = 56; warnings.ForeColor = Theme.Warning; bottom.Controls.Add(Theme.Label("Editoryal uyarılar • sorunu çözdükten sonra ilgili uyarıyı kaldır", 9, true, Theme.Warning, 1100, 24)); bottom.Controls.Add(warnings);

        var sources = Theme.Button("Araştırma ve kaynakları göster", () => { using var d = StudioForm.Dialog("Kaynak denetimi", 950, 720); var text = Theme.Input("Araştırma tarihi: " + Plan.ResearchedAt.ToOffset(TimeSpan.FromHours(3)) + "\r\n\r\n" + string.Join("\r\n", Plan.Sources.Select(s => s.Title + " — " + s.Url)) + "\r\n\r\n" + Plan.Research, true, 860); text.Dock = DockStyle.Fill; text.ReadOnly = true; d.Controls.Add(text); d.ShowDialog(this); }, false, 300); bottom.Controls.Add(sources);

        Controls.Clear(); toolbar.Dock = DockStyle.Fill; fields.Dock = DockStyle.Fill; bottom.Dock = DockStyle.Fill;

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = Theme.Bg };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 260)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 300));

        layout.Controls.Add(toolbar, 0, 0); layout.Controls.Add(fields, 0, 1); layout.Controls.Add(grid, 0, 2); layout.Controls.Add(bottom, 0, 3); Controls.Add(layout);

        void FitEditor(){int width=Math.Max(400,ClientSize.Width-Padding.Horizontal-24);foreach(var container in new[]{fields,bottom})foreach(Control child in container.Controls){child.Width=Math.Min(1100,width);if(child is FlowLayoutPanel row)foreach(Control button in row.Controls)if(button is Button)button.Margin=new Padding(0,0,10,6);}select.Width=Math.Min(600,width);}

        Resize+=(_,_)=>FitEditor();Shown+=(_,_)=>FitEditor();

        select.SelectedIndexChanged += (_, _) => { Commit(); active = scripts[select.SelectedIndex]; LoadScript(); }; active = scripts.First(); LoadScript();

        void Save()

        {

            try { Commit(); Plan.Warnings = warnings.Text.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList(); var check = new Episode { IncludeLong = e.IncludeLong, ShortCount = e.ShortCount, Demo = e.Demo, MusicClip = e.MusicClip, UserNarration = e.UserNarration, QueueMusic = e.QueueMusic, Plan = Plan }; Rules.ValidatePlan(check); DialogResult = DialogResult.OK; Close(); } catch (Exception ex) { Theme.Alert(ex.Message); }

        }

    }

    void Commit()=>CommitCore(true);

    void CommitCore(bool endEdit)

    {

        if (active == null) return; if(endEdit)grid.EndEdit(); active.Title = title.Text.Trim(); active.ThumbnailText = thumbnail.Text.Trim(); active.Description = description.Text.Trim();

        var scenes = new List<Scene>();

        foreach (DataGridViewRow row in grid.Rows)

        {

            if (row.IsNewRow) continue; var scene = row.Tag as Scene ?? new Scene(); var query = StockSearch.Normalize(row.Cells[1].Value?.ToString()?.Trim() ?? "");

            if (scene.VisualQuery != query && !scene.UserLocal) { row.Cells[3].Value = ""; scene.RightsConfirmed = false; scene.StockFile = ""; scene.StockUrl = ""; scene.Creator = ""; scene.LicenseUrl = ""; scene.Attribution = ""; }

            scene.SourceLinks=row.Cells[4].Value?.ToString()?.Trim()??"";

            scene.Narration = row.Cells[0].Value?.ToString()?.Trim() ?? ""; scene.VisualQuery = query; scene.OnScreen = row.Cells[2].Value?.ToString()?.Trim() ?? "";

            var local = row.Cells[3].Value?.ToString()?.Trim() ?? ""; if (local != "" && local != scene.StockFile) { SceneVideoChoice.Apply(scene, local, null); scene.StockUrl = "Kullanıcının yerel arşivi"; scene.Creator = "Kullanıcı arşivi"; scene.LicenseUrl = "Kullanıcı tarafından doğrulanacak"; scene.Attribution = ""; }

            scenes.Add(scene);

        }

        active.Scenes = scenes;

    }

    void LoadScript()

    {

        if (active == null) return; title.Text = active.Title; thumbnail.Text = active.ThumbnailText; description.Text = active.Description; grid.Rows.Clear();

        foreach (var s in active.Scenes) { s.VisualQuery = StockSearch.Normalize(s.VisualQuery); var i = grid.Rows.Add(s.Narration, s.VisualQuery, s.OnScreen, s.StockFile, s.SourceLinks); grid.Rows[i].Tag = s; }

    }

}













































