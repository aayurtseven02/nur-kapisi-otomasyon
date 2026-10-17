using System.Security.Cryptography;



namespace AnkaStudio;



public static class AppLock

{

    public static string DefaultDataRoot()
    {
        var current = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AnkaStudio");
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var previous = Path.Combine(profile, "AppData", "Local", "AnkaStudio");
        // Keep the established account when a launcher changes the AppData environment.
        if (!Enabled(current) && profile.Length > 0 && Enabled(previous)) return previous;
        return current;
    }

    public static void AddEye(Form form, params TextBox[] fields)

    {

        foreach (var field in fields)

        {

            field.Width -= 52;

            var eye = new PasswordEye(field) { Location = new(field.Right + 8, field.Top), Size = new(44, field.Height) };
            field.SizeChanged += (_, _) => eye.SetBounds(field.Right + 8, field.Top, 44, field.Height);
            field.LocationChanged += (_, _) => eye.Location = new(field.Right + 8, field.Top);

            form.Controls.Add(eye);

        }

    }

    public sealed class Record { public string Salt { get; set; } = ""; public string Hash { get; set; } = ""; }

    static string PathFor(string root) => Path.Combine(root, "app-lock.json");

    public static bool Enabled(string root) => File.Exists(PathFor(root));

    public static bool Verify(string root, string password)

    {

        var record = Json.Read(PathFor(root), new Record());

        if (record.Salt == "" || record.Hash == "") return false;

        var hash = Rfc2898DeriveBytes.Pbkdf2(password, Convert.FromBase64String(record.Salt), 310000, HashAlgorithmName.SHA256, 32);

        return CryptographicOperations.FixedTimeEquals(hash, Convert.FromBase64String(record.Hash));

    }

    public static void Set(string root, string password)

    {

        if (password.Length < 10) throw new InvalidOperationException("Açılış şifresi en az 10 karakter olmalı.");

        var salt = RandomNumberGenerator.GetBytes(16);

        Json.Write(PathFor(root), new Record { Salt = Convert.ToBase64String(salt), Hash = Convert.ToBase64String(Rfc2898DeriveBytes.Pbkdf2(password, salt, 310000, HashAlgorithmName.SHA256, 32)) });

        foreach (var file in new[] { RememberPath(root), RecoveryPath(root) }) if (File.Exists(file)) File.Delete(file);

    }

    static string RememberPath(string root) => Path.Combine(root, "login-remember.dat");

    static string RecoveryPath(string root) => Path.Combine(root, "login-recovery.dat");

    static string LockIdentity(string root) => Json.Read(PathFor(root), new Record()).Hash;

    public static void EnrollRecovery(string root) => new Vault(RecoveryPath(root)).Set("Lock", LockIdentity(root));

    public static bool CanRecover(string root)

    {

        try { return Enabled(root) && File.Exists(RecoveryPath(root)) && new Vault(RecoveryPath(root)).Get("Lock") == LockIdentity(root); } catch { return false; }

    }

    public static void Remember(string root, string password, bool enabled)

    {

        if (!enabled) { if (File.Exists(RememberPath(root))) File.Delete(RememberPath(root)); return; }

        if (!Verify(root, password)) throw new InvalidOperationException("Şifre doğrulanamadı; kaydedilmedi.");

        new Vault(RememberPath(root)).Set("Password", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(password)));

    }

    public static string Remembered(string root)

    {

        try { if (!File.Exists(RememberPath(root))) return ""; var password = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(new Vault(RememberPath(root)).Get("Password"))); return Verify(root, password) ? password : ""; } catch { return ""; }

    }

    public static void ResetWithWindows(string root, string password)

    {

        if (!CanRecover(root)) throw new InvalidOperationException("Bu Windows hesabında şifre kurtarma etkin değil. Yeni sürüme bir kez mevcut şifrenle giriş yapılması gerekiyor.");

        Set(root, password); EnrollRecovery(root);

    }

    static bool RecoveryDialog(string root, IWin32Window owner)

    {

        if (!CanRecover(root)) { Theme.Alert(owner, "Bu Windows hesabında kurtarma henüz etkin değil. Yeni sürüme bir kez mevcut şifrenle giriş yapılması gerekiyor."); return false; }

        using var form = new Form { Text = "Şifremi unuttum • yeni şifre", Width = 580, Height = 340, StartPosition = FormStartPosition.CenterParent, BackColor = Theme.Bg, MaximizeBox = false };

        var info = Theme.Label("Kayıtlı Windows hesabı doğrulandı. Yeni şifreni iki kez yaz. İçeriklerin ve ayarların korunur.", 10, width: 500, height: 64); info.Location = new(20, 15);

        var first = Theme.Input(width: 500); first.UseSystemPasswordChar = true; first.PlaceholderText = "Yeni şifre • en az 10 karakter"; first.Location = new(20, 90);

        var second = Theme.Input(width: 500); second.UseSystemPasswordChar = true; second.PlaceholderText = "Yeni şifreyi tekrar yaz"; second.Location = new(20, 140);

        var status = Theme.Label("", 9, color: Theme.Warning, width: 500, height: 40); status.Location = new(20, 185);

        var save = Theme.Button("Yeni şifreyi kaydet", () => { try { if (first.Text != second.Text) throw new InvalidOperationException("Şifreler eşleşmiyor."); ResetWithWindows(root, first.Text); form.DialogResult = DialogResult.OK; } catch (Exception ex) { status.Text = ex.Message; } }, true, 240); save.Location = new(20, 235);

        form.Controls.AddRange([info, first, second, status, save]); AddEye(form, first, second); form.AcceptButton = save; return form.ShowDialog(owner) == DialogResult.OK;

    }

    public static bool Login(string root, string? preview = null)

    {

        var channel = SavedChannelTitle(root);

        using var form = new Form { Text = Enabled(root) ? "TubeFlow • giriş" : "TubeFlow • açılış şifresi oluştur", Width = 580, Height = 590, StartPosition = FormStartPosition.CenterScreen, BackColor = Theme.Bg, ForeColor = Theme.Text, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false };

        using var logoStream=typeof(AppLock).Assembly.GetManifestResourceStream("AnkaStudio.Assets.anka-studio-3d.png");using var logoSource=Image.FromStream(logoStream!);var logo=new PictureBox{Image=new Bitmap(logoSource),SizeMode=PictureBoxSizeMode.Zoom,BackColor=Theme.Bg,Size=new Size(190,125),Location=new Point((form.ClientSize.Width-190)/2,8),AccessibleName="TubeFlow logosu"};form.FormClosed+=(_,_)=>logo.Image?.Dispose();

        if (channel != "") form.Text = channel + " • " + form.Text;

        string ChannelHeading(string title) => "BAĞLI YOUTUBE KANALI:" + title.ToUpper(System.Globalization.CultureInfo.GetCultureInfo("tr-TR"));
        var heading = Theme.Label(channel == "" ? "YOUTUBE KANALI HENÜZ DOĞRULANMADI" : ChannelHeading(channel), 14, true, Theme.Text, width: 500, height: 42); heading.Location = new(25, 15); heading.AutoEllipsis = true;

        using var channelCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        form.FormClosed += (_, _) => channelCancellation.Cancel();

        if (preview == null) form.Shown += async (_, _) => {

            try {

                var liveTitle = await ConnectedChannelTitle(root, channelCancellation.Token);

                if (!form.IsDisposed && liveTitle != "") { heading.Text = ChannelHeading(liveTitle); form.Text = liveTitle + " • TubeFlow • giriş"; }

            } catch (Exception) { /* Offline login remains available with the last verified identity. */ }

        };

        var setup = !Enabled(root);

        var info = Theme.Label(setup ? "Uygulamayı korumak için en az 10 karakterlik şifre belirle. Bu yerel giriş şifresidir; e-posta doğrulaması değildir. API anahtarların korunur." : "Yönetim panelini açmak için uygulama şifreni gir.", 11, width: 500, height: 55); info.Location = new(25, 60);

        var password = Theme.Input("", width: 500); password.UseSystemPasswordChar = true; password.Location = new(25, 115);

        var repeat = Theme.Input("", width: 500); repeat.UseSystemPasswordChar = true; repeat.PlaceholderText = "Şifreyi tekrar yaz"; repeat.Location = new(25, 165); repeat.Visible = setup;

        var status = Theme.Label("", 9, color: Theme.Warning, width: 500, height: 42); status.Location = new(25, 210);

        var savedPassword = setup ? "" : Remembered(root); password.Text = savedPassword;

        var remember = Theme.Check("Şifremi hatırla • yalnızca bu Windows hesabında", savedPassword != "", 500); remember.Location = new(25, 255);

        remember.CheckedChanged += (_, _) => { if (!remember.Checked) Remember(root, "", false); };

        var recovery = Theme.Button("Şifremi unuttum", () => { if (setup) { Theme.Alert(form, "Bu veri klasöründe kayıtlı bir hesap bulunamadı. Eski şifrenin üzerine yazılmadı. Önceden kullandığın TubeFlow kurulumunu ve veri klasörünü kontrol et. Kayıtlı hesap bulunduğunda bu düğmeyle, aynı Windows hesabından yeni şifre belirleyebilirsin."); return; } if (RecoveryDialog(root, form)) { password.Clear(); remember.Checked = false; status.Text = "Şifre sıfırlandı. Yeni şifrenle giriş yap."; } }, false, 230); recovery.Location = new(270, 320); recovery.Visible = true;

        var attempts = 0; var button = Theme.Button(setup ? "Şifre oluştur ve aç" : "Giriş yap", () => {

            try { if (setup) { if (password.Text != repeat.Text) throw new InvalidOperationException("Şifreler eşleşmiyor."); Set(root, password.Text); } else if (!Verify(root, password.Text)) { attempts++; if (attempts >= 5) { form.Close(); return; } throw new InvalidOperationException("Şifre yanlış. Kalan deneme: " + (5 - attempts)); } EnrollRecovery(root); Remember(root, password.Text, remember.Checked); form.DialogResult = DialogResult.OK; }

            catch (Exception ex) { status.Text = ex.Message; password.Clear(); }

        }, true, 230); button.Location = new(25, 320); form.AcceptButton = button;

        if (preview != null) form.Shown += (_, _) => { form.Refresh(); using var image = new Bitmap(form.Width, form.Height); form.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size)); image.Save(preview); form.DialogResult = DialogResult.Cancel; form.Close(); };

        foreach (var control in new Control[] { heading, info, password, repeat, status, remember, recovery, button }) control.Top += 130;
        form.Controls.AddRange([logo, heading, info, password, repeat, status, remember, recovery, button]); AddEye(form, setup ? [password, repeat] : [password]); return form.ShowDialog() == DialogResult.OK;

    }

    public static string SavedChannelTitle(string root)

    {

        // Read only the verified YouTube identity; do not open or mutate Store before login.

        foreach (var name in new[] { "state.json", "state.json.bak", "settings.json", "settings.json.bak" }) {

            try {

                using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(root, name)));

                var settings = document.RootElement;

                if (name.StartsWith("state.")) settings = settings.GetProperty("Settings");

                var id = settings.GetProperty("YouTubeChannelId").GetString();

                var title = settings.GetProperty("YouTubeChannelTitle").GetString();

                return !string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(title) ? title.Trim() : "";

            } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or KeyNotFoundException or InvalidOperationException) { }

        }

        return "";

    }

    static async Task<string> ConnectedChannelTitle(string root, CancellationToken ct)

    {

        var path = Path.Combine(root, "settings.json");

        using var settings = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));

        var clientId = settings.RootElement.GetProperty("GoogleClientId").GetString() ?? "";

        var vault = new Vault(Path.Combine(root, "secrets.dat"));

        var refresh = Environment.GetEnvironmentVariable("YOUTUBE_REFRESH_TOKEN") ?? vault.Get("YOUTUBE_REFRESH_TOKEN");

        var secret = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET") ?? vault.Get("GOOGLE_CLIENT_SECRET");

        if (clientId == "" || refresh == "" || secret == "") return "";

        using var http = new HttpClient();

        using var tokenResponse = await http.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(new Dictionary<string, string> { ["client_id"] = clientId, ["client_secret"] = secret, ["refresh_token"] = refresh, ["grant_type"] = "refresh_token" }), ct);

        tokenResponse.EnsureSuccessStatusCode();

        using var token = System.Text.Json.JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync(ct));

        using var request = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/youtube/v3/channels?part=snippet&mine=true");

        request.Headers.Authorization = new("Bearer", token.RootElement.GetProperty("access_token").GetString());

        using var response = await http.SendAsync(request, ct); response.EnsureSuccessStatusCode();

        using var identity = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));

        var items = identity.RootElement.GetProperty("items");

        return items.GetArrayLength() == 1 ? items[0].GetProperty("snippet").GetProperty("title").GetString()?.Trim() ?? "" : "";

    }

    public static void Change(string root)

    {

        if (!Login(root)) return;

        using var form = new Form { Text = "Açılış şifresini değiştir", Width = 570, Height = 280, StartPosition = FormStartPosition.CenterParent, BackColor = Theme.Bg };

        var first = Theme.Input("", width: 500); first.UseSystemPasswordChar = true; first.PlaceholderText = "Yeni şifre • en az 10 karakter"; first.Location = new(20, 25);

        var second = Theme.Input("", width: 500); second.UseSystemPasswordChar = true; second.PlaceholderText = "Yeni şifreyi tekrar yaz"; second.Location = new(20, 80);

        var button = Theme.Button("Yeni şifreyi kaydet", () => { try { if (first.Text != second.Text) throw new InvalidOperationException("Şifreler eşleşmiyor."); Set(root, first.Text); EnrollRecovery(root); form.Close(); } catch (Exception ex) { Theme.Alert(ex.Message); } }, true, 230); button.Location = new(20, 145); form.Controls.AddRange([first, second, button]); AddEye(form, first, second); form.ShowDialog();

    }

}



public sealed class PasswordEye : StudioButton

{

    readonly TextBox field;

    public PasswordEye(TextBox field)

    {

        this.field = field; AccessibleName = "Şifreyi göster"; BackColor = Theme.Elevated;

        FlatStyle = FlatStyle.Flat; FlatAppearance.BorderColor = Theme.Border; Cursor = Cursors.Hand;

        Click += (_, _) => { field.UseSystemPasswordChar = !field.UseSystemPasswordChar; AccessibleName = field.UseSystemPasswordChar ? "Şifreyi göster" : "Şifreyi gizle"; Invalidate(); };

    }

    protected override void OnPaint(PaintEventArgs e)

    {

        base.OnPaint(e); e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        using var pen = new Pen(Theme.Text, 1.6f) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round, LineJoin = System.Drawing.Drawing2D.LineJoin.Round };
        float cx = ClientSize.Width / 2f, cy = ClientSize.Height / 2f;
        float halfWidth = Math.Min(12f, ClientSize.Width * .3f), halfHeight = Math.Min(6f, ClientSize.Height * .24f);
        using var eyeShape = new System.Drawing.Drawing2D.GraphicsPath();
        eyeShape.AddBezier(cx - halfWidth, cy, cx - halfWidth * .45f, cy - halfHeight * 1.4f, cx + halfWidth * .45f, cy - halfHeight * 1.4f, cx + halfWidth, cy);
        eyeShape.AddBezier(cx + halfWidth, cy, cx + halfWidth * .45f, cy + halfHeight * 1.4f, cx - halfWidth * .45f, cy + halfHeight * 1.4f, cx - halfWidth, cy);
        e.Graphics.DrawPath(pen, eyeShape);
        e.Graphics.DrawEllipse(pen, cx - 3, cy - 3, 6, 6);
        if (field.UseSystemPasswordChar) e.Graphics.DrawLine(pen, cx - 11, cy + 7, cx + 11, cy - 7);

    }

}



