namespace AnkaStudio;



public sealed partial class StudioForm

{

    const string CloudAiNotice = "Bu bulut modellerini yalnızca API anahtarınız (token) ve yeterli kullanım kotanız veya bakiyeniz varsa etkinleştirin. Program bu hizmetlere ücretsiz erişim sağlamaz; aboneliğiniz API kullanımını kapsamayabilir. Ücretli kullanım ayrıca izin gerektirir.";

    void LocalAiPanel(FlowLayoutPanel target)

    {

        var panel = Theme.Flow(Math.Min(820, page.Width)); panel.Tag = "wide";

        panel.Controls.Add(Theme.Label("YEREL YAPAY ZEKÂ • OLLAMA", 16, true, Theme.Teal, panel.Width, 42));

        panel.Controls.Add(Theme.Label("Ollama bilgisayarında çalışır; API anahtarı gerekmez. Qwen3 metin üretir; Qwen3-VL fotoğraf ve video karelerini okur. İlk kurulumda model indirilir; RAM, disk ve elektrik kullanır. Seslendirme veya görsel üretim motorunun yerine geçmez.", 10, width: panel.Width, height: 90));

        panel.Controls.Add(Theme.Label(DeviceMemory.Note(),10,color:Theme.Muted,width:panel.Width,height:92));
        var labels = LocalAi.Catalog.Select(m => m.Label).ToList();

        var chosen = LocalAi.Catalog.FirstOrDefault(m => m.Id == store.Settings.OllamaModel)?.Label ?? store.Settings.OllamaModel;

        if (!labels.Contains(chosen)) labels.Add(chosen);

        var select = Theme.Combo(labels.ToArray(), chosen, Math.Min(760, panel.Width));

        select.AccessibleName = "Ollama yerel model seçimi"; select.Margin = new Padding(0, 0, 0, 18); panel.Controls.Add(select);

        string Selected() => LocalAi.Catalog.FirstOrDefault(m => m.Label == select.Text)?.Id ?? select.Text;

        var status = Theme.Label("Durumu yenile ile kurulu modelleri ve servisi kontrol et.", 10, true, Theme.Teal, panel.Width, 76);

        status.AccessibleName = "Ollama servis ve model durumu";

        var buttons = new FlowLayoutPanel { Width = panel.Width, Height = 110, WrapContents = true, BackColor = Color.Transparent };

        var local = new LocalAi(store);

        async Task Refresh(CancellationToken ct) {

            var state = await local.Status(ct); if (panel.IsDisposed) return;

            status.Text = state.Note + "\nEtkin model: " + store.Settings.OllamaModel + (state.Running && !state.Models.Contains(store.Settings.OllamaModel) ? " • henüz kurulu değil" : "");

            foreach (var model in state.Models) if (!LocalAi.Catalog.Any(m => m.Id == model) && !select.Items.Contains(model)) select.Items.Add(model);

        }

        IProgress<string> progress = new Progress<string>(text => { if (!status.IsDisposed) status.Text = text; });

        bool working = false;

        async Task Run(Func<CancellationToken, Task> action) {

            if (working) return; working = true;

            foreach (Control button in buttons.Controls) button.Enabled = false;

            select.Enabled = false;

            try { await engine.External(async ct => { try { await action(ct); } catch (OperationCanceledException) { if (!status.IsDisposed) status.Text = "Yerel AI işlemi iptal edildi. Yeniden deneyebilirsin."; } }); }

            catch (Exception ex) { if (!status.IsDisposed) status.Text = "İşlem tamamlanmadı: " + ex.Message; throw; }

            finally { working = false; if (!panel.IsDisposed) { foreach (Control button in buttons.Controls) button.Enabled = true; select.Enabled = true; } }

        }

        buttons.Controls.Add(Theme.Button("Yerel motoru yükle", () => _ = Safe(() => Run(ct => local.Install(progress, ct))), true, 235));

        buttons.Controls.Add(Theme.Button("Seçili modeli yükle", () => _ = Safe(() => Run(async ct => { var model = Selected(); await local.Pull(model, progress, ct); if (!panel.IsDisposed) status.Text = "Model kuruldu: " + model + ". Ana model yap düğmesiyle kullanıma al."; })), false, 235));

        buttons.Controls.Add(Theme.Button("Servisi başlat", () => _ = Safe(() => Run(async ct => { await local.Start(ct); await Refresh(ct); })), false, 220));

        buttons.Controls.Add(Theme.Button("Durumu yenile", () => _ = Safe(async () => { await Refresh(CancellationToken.None); }), false, 235));

        buttons.Controls.Add(Theme.Button("Seçili modeli dene", () => _ = Safe(() => Run(async ct => {

            var model = Selected(); await local.Start(ct); await local.EnsureLocal(model, ct);

            var previous = store.Settings.OllamaModel; store.Settings.OllamaModel = model;

            try { status.Text = "Yerel model deneniyor; ilk yükleme daha uzun sürebilir..."; await new Services(store, text => progress.Report(text)).VerifyModel("Ollama", ct); if (!status.IsDisposed) status.Text = "Seçili yerel model geçerli JSON üretti: " + model + ". Tam senaryo ve telaffuz ayrıca kontrol edilir."; }

            finally { store.Settings.OllamaModel = previous; }

        })), false, 235));

        buttons.Controls.Add(Theme.Button("Ana model yap", () => _ = Safe(() => Run(async ct => {

            var model = Selected(); await local.Start(ct); await local.EnsureLocal(model, ct);

            var previous = store.Settings.OllamaModel; store.Settings.OllamaModel = model;

            try { status.Text = "Ana model doğrulanıyor..."; await new Services(store, text => progress.Report(text)).VerifyModel("Ollama", ct); }

            catch { store.Settings.OllamaModel = previous; throw; }

            store.Settings.UseOllama = true; store.Settings.LocalAiFirst = true;

            store.ProviderBlocked.Remove("Ollama"); store.PreferredProvider = "Ollama"; store.Save();

            if (!panel.IsDisposed) status.Text = "Ana yerel model kaydedildi: " + model + ". Bulut izinlerin ayrıca yönetilir.";

        })), true, 220));

        panel.Controls.Add(buttons); panel.Controls.Add(status);
        var visionStatus=Theme.Label("Görsel okuma modeli: "+store.Settings.OllamaVisionModel+" • yerel/ücretsiz",10,true,width:panel.Width,height:55);panel.Controls.Add(visionStatus);
        panel.Controls.Add(Theme.Button("Ücretsiz görsel okuma modelini yükle / kontrol et",()=>_=Safe(()=>Run(async ct=>{string model="qwen3-vl:4b-instruct";await local.Pull(model,progress,ct);store.Settings.OllamaVisionModel=model;store.Settings.UseOllama=true;store.Settings.LocalAiFirst=true;store.Save();if(!visionStatus.IsDisposed)visionStatus.Text="Görsel okuma hazır: "+model+" • fotoğraf/video analizi yerelde öncelikli.";})),true,Math.Min(520,panel.Width)));

        panel.Controls.Add(Theme.Button("Yükleme / yerel AI işlemini iptal et", () => { if (working) engine.Stop(); }, false, 430));

        panel.Controls.Add(Theme.Label("Önerilen modeller: Apache 2.0 lisanslı Qwen3. Hafif modelde hata ihtimali daha yüksektir; dua ve alıntılar kaynakla kontrol edilir. Yükleme düğmesi yalnız seçtiğin modeli indirir; bütün modeller indirilmez. Yerel motor yüklemesi resmi, SHA-256 doğrulanan Ollama arşivini ayrı klasöre kurar; sisteminde Ollama varsa onu kullanır.", 9, width: panel.Width, height: 100));

        var preference=new StudioDisclosure("Ollama kullanım tercihleri",panel.Width);
        var use=Theme.Check("Yerel metin motoru etkin",store.Settings.UseOllama,650);var first=Theme.Check("Önce yerel metin motorunu dene",store.Settings.LocalAiFirst,650);var wait=Theme.Number(store.Settings.OllamaTimeoutMinutes,1,30,350);
        preference.Content.Controls.Add(use);preference.Content.Controls.Add(first);Field(preference.Content,"Bekleme süresi • dakika",wait);
        preference.Content.Controls.Add(Theme.Button("Tercihleri kaydet",()=>{if(engine.Busy){Message("Üretimin bitmesini bekle.");return;}store.Settings.UseOllama=use.Checked;store.Settings.LocalAiFirst=first.Checked;store.Settings.OllamaTimeoutMinutes=(int)wait.Value;store.Save();Message("Ollama tercihleri kaydedildi.");},false,260));panel.Controls.Add(preference);
        var group=new StudioDisclosure("Metin motoru • Ollama",target.Width);group.Content.Controls.Add(panel);target.Controls.Add(group);

    }

    void CloudAiOptions(FlowLayoutPanel target, Settings settings, List<Action> saves)

    {

        target.Controls.Add(Theme.Label("BULUT YAPAY ZEKÂ • İSTEĞE BAĞLI", 14, true, Theme.Teal, 760, 40));

        target.Controls.Add(Theme.Label(CloudAiNotice, 10, color: Theme.Warning, width: 760, height: 110));

        var selected = settings.EnabledCloudProviders.Split(',', StringSplitOptions.TrimEntries).ToHashSet();

        var checks = new Dictionary<string, CheckBox>();

        foreach (var (name, note) in new[] { ("OpenRouter", "Ücretsiz modellerde de API anahtarı ve kota gerekir"), ("NVIDIA", "API anahtarı gerekir; kota ve ücret koşulları NVIDIA hesabına bağlıdır"), ("Groq", "Ücretsiz kota hesabına bağlıdır; ücretli hesap ücret doğurabilir"), ("Gemini", "API bakiyesi/kotası ayrıdır; uygulama aboneliği yeterli olmayabilir"), ("OpenAI", "Ücretli API; ChatGPT aboneliği API bakiyesi değildir"), ("Kimi", "Moonshot API; bakiye ve ücretli kullanım izni gerekir"), ("Pollinations", "Anahtarsız son yedek • konu/metin bu servise gönderilir; ücretsiz erişim kesilebilir") }) {

            var check = Theme.Check(name + " • " + note, settings.CloudAiEnabled && selected.Contains(name), 760); checks.Add(name, check); target.Controls.Add(check);

        }

        var paid = Theme.Check("Ücretli API kullanımına izin ver • hesapta ücret oluşabilir", !settings.FreeOnly, 760); target.Controls.Add(paid);

        saves.Add(() => {

            settings.EnabledCloudProviders = string.Join(',', checks.Where(c => c.Value.Checked).Select(c => c.Key));

            settings.CloudAiEnabled = settings.EnabledCloudProviders != ""; settings.UseKimi = checks["Kimi"].Checked; settings.FreeOnly = !paid.Checked;

            foreach (var provider in checks.Where(c => c.Value.Checked).Select(c => c.Key)) if (!settings.ProviderOrder.Split(',', StringSplitOptions.TrimEntries).Contains(provider)) settings.ProviderOrder += "," + provider;

        });

    }

}

