using System.Diagnostics;

using System.Text;



namespace AnkaStudio;



internal static class Program

{

    [STAThread]

    static void Main(string[] args)

    {

        ApplicationConfiguration.Initialize();

        if(args.Contains("--cleanup-program-files")){
            try{var backup=Path.Combine(AppLock.DefaultDataRoot(),"cleanup-backups",DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N"));int count=InstallCleanup.Clean(AppContext.BaseDirectory,backup);Theme.Alert(count+" eski program dosyası yedeklendi.\n"+backup);}
            catch(Exception ex){Theme.Alert("Temizlik tamamlanamadı: "+ex.Message);}return;
        }

        if(args.Contains("--startup-health-check")){
            try{using var window=new Form();_=window.Handle;var report=Arg(args,"--health-report");if(report!=null){Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(report))!);File.WriteAllText(report,System.Text.Json.JsonSerializer.Serialize(new{Success=true,Version=typeof(Program).Assembly.GetName().Version!.ToString(3)}));}}
            catch{Environment.ExitCode=1;}return;
        }

        if(args.Contains("--local-engine-check")){
            WindowsFormsSynchronizationContext.AutoInstall=false;
            try {var workflow=Arg(args,"--workflow")??throw new InvalidOperationException("İş akışı gerekli.");using var stop=new CancellationTokenSource(TimeSpan.FromMinutes(3));Task.Run(async()=>{await LocalCoverEngine.Start(workflow,Console.WriteLine,stop.Token);Console.WriteLine(await LocalCoverEngine.Check(workflow,stop.Token));}).GetAwaiter().GetResult();}
            catch(Exception ex){Console.Error.WriteLine(ex.Message);Environment.ExitCode=1;}return;
        }
        if(StartupUpdates.Run(args))return;

        ModernScrollbars.Install();

        if (args.Contains("--local-ai-test")) { WindowsFormsSynchronizationContext.AutoInstall = false; try { LocalAiSmokeTests.Run(args).GetAwaiter().GetResult(); } catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; } return; }

        if (args.Contains("--reliability-test")) { Environment.Exit(ReliabilityTests.Run(args)); return; }

        if (Arg(args, "--login-preview") is string loginPreview) {

            var testRoot = Path.GetFullPath(Arg(args, "--data") ?? throw new InvalidOperationException("Ayrı test klasörü gerekli."));

            if (Directory.Exists(testRoot) && Directory.EnumerateFileSystemEntries(testRoot).Any()) throw new InvalidOperationException("Boş test klasörü gerekli.");

            Directory.CreateDirectory(testRoot); Directory.CreateDirectory(loginPreview);

            if (Arg(args, "--preview-channel-data") is string channelRoot) {

                var channelTitle = AppLock.SavedChannelTitle(channelRoot);

                Json.Write(Path.Combine(testRoot, "settings.json"), new Settings { YouTubeChannelId = channelTitle == "" ? "" : "preview", YouTubeChannelTitle = channelTitle });

            }

            AppLock.Login(testRoot, Path.Combine(loginPreview, "password-setup.png")); AppLock.Set(testRoot, "fixture-password-123"); AppLock.EnrollRecovery(testRoot); AppLock.Remember(testRoot, "fixture-password-123", true);

            AppLock.Login(testRoot, Path.Combine(loginPreview, "password-login.png")); return;

        }

        if (Arg(args, "--sound-preview") is string soundFile) { File.WriteAllBytes(soundFile, UiSound.Create(.25)); return; }

        if (args.Contains("--self-test"))

        {

            WindowsFormsSynchronizationContext.AutoInstall = false;

            Environment.Exit(SelfTests.Run());

            return;

        }

#if CLEAN_DISTRIBUTION
        var root = Arg(args, "--data") ?? Path.Combine(AppContext.BaseDirectory, "Data");
#else
        var root = Arg(args, "--data") ?? AppLock.DefaultDataRoot();
#endif

        Directory.CreateDirectory(root);

        using var mutex = new Mutex(true, "Local\\AnkaStudio-" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar).ToUpperInvariant())))[..16], out var first);

        if (!first) { Theme.Alert("Anka Studio zaten açık. Görev çubuğundan uygulamaya geçebilirsin."); return; }

        try

        {

            if (AppLock.Enabled(root) && args.Any(x => x is "--worker" or "--screenshot" or "--ui-test" or "--media-test" or "--design-test")) throw new InvalidOperationException("Şifreli yerel veri klasörü etkileşimsiz komutlarla açılamaz. Uygulamada giriş yap.");

            if (!args.Contains("--worker") && !args.Contains("--screenshot") && !args.Contains("--ui-test") && !args.Contains("--media-test") && !args.Contains("--design-test") && !AppLock.Login(root)) return;

            if (!args.Contains("--worker") && !args.Contains("--screenshot")) Vault.PasswordProvider = firstUse => PasswordPrompt(firstUse);

            var store = new Store(root);

            if(args.Contains("--package-dialog-preview") && Arg(args,"--ui-test") is string packageShots){

                Directory.CreateDirectory(packageShots);using var editor=new PackageDialog();editor.Shown+=(_,_)=>{try{

                    void Shot(string name){editor.PerformLayout();Application.DoEvents();using var image=new Bitmap(editor.Width,editor.Height);editor.DrawToBitmap(image,new Rectangle(Point.Empty,editor.Size));image.Save(Path.Combine(packageShots,name+".png"));}

                    Shot("package-top");var outer=editor.Controls.OfType<Panel>().Single();if(!ModernScrollbars.HoverLeaveRetainsTheme(outer))throw new InvalidOperationException("Paket kaydırma teması korunmadı.");var caption=Shapes.CaptionColor(editor.Handle);if(caption!=null && caption!=ColorTranslator.ToWin32(Theme.Bg))throw new InvalidOperationException("Koyu başlık rengi uygulanmadı.");outer.AutoScrollPosition=new Point(0,1000);Shot("package-bottom");File.WriteAllText(Path.Combine(packageShots,"verified.txt"),"Paket ilk açılış, kaydırma ve fare ayrılma görünümü kontrol edildi. Başlık rengi: "+(caption?.ToString()??"Bu Windows sürümünde renk sorgusu desteklenmiyor."));

                }catch(Exception ex){File.WriteAllText(Path.Combine(packageShots,"error.txt"),ex.ToString());Environment.ExitCode=1;}finally{editor.Close();}};Application.Run(editor);return;

            }

            if(args.Contains("--guide-preview") && Arg(args,"--ui-test") is string guideShots){UserGuide.Capture(guideShots);return;}

            if (args.Contains("--cover-style-preview") && Arg(args, "--ui-test") is string covers) { Directory.CreateDirectory(covers); var settings = new Settings { CoverStyle = "Nur Kapısı • Altın sinema", ChannelName = "NUR KAPISI" }; CoverDesigner.Save(Path.Combine(covers, "nur-kapisi-normal.png"), settings, "FETİH SURESİ", false); CoverDesigner.Save(Path.Combine(covers, "nur-kapisi-shorts.png"), settings, "KÜÇÜK ADIMLAR", true); return; }

            if (args.Contains("--design-test")) { if (AppLock.Enabled(root)) throw new InvalidOperationException("Şifreli veri klasörü test amacıyla kullanılamaz."); SelfTests.DesignMediaTest(store).GetAwaiter().GetResult(); return; }

            if (args.Contains("--media-test")) { SelfTests.MediaTest(store).GetAwaiter().GetResult(); return; }

            if (args.Contains("--worker"))

            {

                if (args.Contains("--youtube-upload")) RemoteYouTubeWorker.Run(store, args).GetAwaiter().GetResult(); else Worker.Run(store, args).GetAwaiter().GetResult();

                return;

            }

            if (args.Contains("--card-editor-preview") && Arg(args, "--ui-test") is string cardShots) {

                Directory.CreateDirectory(cardShots); using var editor = new CardEditor(store); editor.Shown += (_, _) => { try { editor.CaptureTest(cardShots); } catch(Exception ex) { File.WriteAllText(Path.Combine(cardShots,"error.txt"),ex.ToString()); Environment.ExitCode=1; } finally { editor.Close(); } }; Application.Run(editor); return;

            }

            if (args.Contains("--package-cover-preview") && Arg(args,"--ui-test") is string coverShots) {

                var fixture=new Episode{Topic="Kapak düzeni",Demo=true,ShortCount=1};fixture.Plan=Engine.DemoPlan(fixture);

                using var editor=new PackageCoverEditor(store,fixture);editor.Shown+=(_,_)=>{try{editor.CaptureTest(coverShots);}catch(Exception ex){Directory.CreateDirectory(coverShots);File.WriteAllText(Path.Combine(coverShots,"error.txt"),ex.ToString());Environment.ExitCode=1;}finally{editor.Close();}};Application.Run(editor);return;

            }

            if ((args.Contains("--text-editor-preview") || args.Contains("--ai-titles-preview") || args.Contains("--music-save-preview")) && Arg(args, "--ui-test") is string textShots) {

                var fixture = new Episode { Topic = "Yazı tasarımı testi", Demo = true, Music = args.Contains("--music-save-preview"), ShortCount = 1 }; fixture.Plan = Engine.DemoPlan(fixture);

                using var editor = new TextEditor(store, fixture);

                editor.Shown += async (_, _) => { try { if (args.Contains("--music-save-preview")) editor.CaptureMusicSaveTest(textShots); else if (args.Contains("--ai-titles-preview")) await editor.CaptureAiTitlesTest(textShots); else await editor.CaptureTest(textShots); if (fixture.Plan.Long!.TextLayout != null) throw new Exception("Taslak asıl planı değiştirdi"); } catch (Exception ex) { File.WriteAllText(Path.Combine(root, "ui-error.txt"), ex.ToString()); Environment.ExitCode = 1; } finally { editor.Close(); } };

                Application.Run(editor); return;

            }

            using var form = new StudioForm(store);

            if (Arg(args, "--ui-test") is string screenFolder) form.Shown += (_, _) => { if (args.Contains("--compact-center-preview")) form.CaptureCompactCenter(screenFolder); else if (args.Contains("--wizard-preview")) form.CaptureProductionWizard(screenFolder); else if (args.Contains("--laptop-layout-preview")) form.CaptureLaptopLayout(screenFolder); else if (args.Contains("--organized-ui-preview")) form.CaptureOrganizedUi(screenFolder); else if (args.Contains("--package-progress-preview")) form.CapturePackageProgress(screenFolder); else if (args.Contains("--archive-actions-preview")) form.CaptureArchiveActions(screenFolder); else if (args.Contains("--scene-editor-preview")) form.CaptureSceneEditor(screenFolder); else if (args.Contains("--new-features-preview")) form.CaptureNewFeatures(screenFolder); else if (args.Contains("--publication-help-preview")) form.CapturePublicationHelp(screenFolder); else if (args.Contains("--queue-preview")) form.CaptureQueue(screenFolder); else if (args.Contains("--upload-status-preview")) form.CaptureUploadStatus(screenFolder); else if (args.Contains("--connections-preview")) form.CaptureConnections(screenFolder); else if (args.Contains("--package-logs-preview")) form.CapturePackageLogs(screenFolder); else if (args.Contains("--stock-settings-preview")) form.CaptureStockSettings(screenFolder); else if (args.Contains("--ai-settings-preview")) form.CaptureAiSettings(screenFolder); else form.CapturePages(screenFolder); form.Close(); };

            if (Arg(args, "--screenshot") is string shot)

            {

                form.Shown += (_, _) =>

                {

                    var timer = new System.Windows.Forms.Timer { Interval = 1000 };

                    timer.Tick += (_, _) => { timer.Stop(); using var bitmap = new Bitmap(form.Width, form.Height); form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size)); bitmap.Save(shot); form.Close(); };

                    timer.Start();

                    // Keep the timer alive until the capture finishes.

                    form.Tag = timer;

                };

            }

            Application.ThreadException += (_, e) =>

            {

                File.WriteAllText(Path.Combine(root, "ui-error.txt"), e.Exception.ToString());

                if (args.Contains("--screenshot") || args.Contains("--ui-test")) { form.Close(); Environment.ExitCode = 1; }

                else Theme.Alert(e.Exception.Message, "Anka Studio");

            };

            Application.Run(form);

        }

        catch (Exception e) { File.WriteAllText(Path.Combine(root, "startup-error.txt"), e.ToString()); if (!args.Any(x => x is "--worker" or "--ui-test" or "--media-test" or "--design-test")) Theme.Alert(e.Message, "Anka Studio başlatılamadı"); Environment.ExitCode = 1; }

    }

    public static string? Arg(string[] args, string name) { var i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }

    static string? PasswordPrompt(bool firstUse)

    {

        using var form = StudioForm.Dialog("Anka Studio • Güvenli anahtar kasası", 670, 360);

        var heading = Theme.Label(firstUse ? "Windows kasası yerine parola kasası" : "Anahtar kasasının kilidini aç", 17, true, Theme.Teal, 600, 48); heading.Location = new(24, 24);

        var note = Theme.Label(firstUse ? "Windows kimlik kasası kullanılamıyor. En az 10 karakterlik bir parola belirle. Anahtarların AES-GCM ile şifrelenir; parola kaydedilmez. Parolanı unutursan kayıtlı anahtarları yeniden girmen gerekir." : "Anahtarlarını açmak için kasa parolanı gir. Parola yalnızca bu oturumda kullanılır.", 10, color: Theme.Muted, width: 590, height: 85); note.Location = new(24, 82);

        var input = Theme.Input(width: 590); input.UseSystemPasswordChar = true; input.Location = new(24, 180);

        var button = Theme.Button(firstUse ? "Şifreli kasayı oluştur" : "Kasayı aç", () => { if (firstUse && input.Text.Length < 10) { Theme.Alert("En az 10 karakter kullan."); return; } form.DialogResult = DialogResult.OK; form.Close(); }, true, 250); button.Location = new(24, 237); form.Controls.AddRange([heading, note, input, button]); AppLock.AddEye(form, input); form.AcceptButton = button;

        return form.ShowDialog() == DialogResult.OK ? input.Text : null;

    }

}





