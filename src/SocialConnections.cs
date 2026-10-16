using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;

namespace AnkaStudio;

public sealed class SocialConnections(Store store, HttpClient? client = null)
{
    static readonly HttpClient Shared = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(30) };
    readonly HttpClient http = client ?? Shared;
    public async Task Connect(string platform, CancellationToken ct, Action<string>? open = null)
    {
        var address = store.Settings.SocialBrokerUrl.TrimEnd('/');
        if (address == "") throw new InvalidOperationException("Sosyal hesap bağlantı hizmeti henüz kurulmadı. (i) içindeki kurulum adımlarını tamamla.");
        SocialPublisher.Https(address);
        using var start = await http.PostAsJsonAsync(address + "/start", new { platform }, ct);
        if (!start.IsSuccessStatusCode) throw new InvalidOperationException(platform + " geliştirici uygulaması henüz yapılandırılmamış veya bağlantı hizmeti kullanılamıyor.");
        using var json = JsonDocument.Parse(await start.Content.ReadAsStringAsync(ct));
        var root = json.RootElement; var ticket = root.GetProperty("ticket").GetString()!;
        var auth = SocialPublisher.Https(root.GetProperty("authorization_url").GetString()!);
        if (auth.Host is not ("www.facebook.com" or "www.instagram.com" or "www.tiktok.com")) throw new InvalidOperationException("Geçersiz hesap izin adresi.");
        if (open != null) open(auth.AbsoluteUri); else Process.Start(new ProcessStartInfo(auth.AbsoluteUri) { UseShellExecute = true });
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromMinutes(5));
        while (true) {
            timeout.Token.ThrowIfCancellationRequested();
            using var req = new HttpRequestMessage(HttpMethod.Post, address + "/poll") { Content = JsonContent.Create(new { ticket }) };
            using var response = await http.SendAsync(req, timeout.Token);
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Bağlantı oturumu sona erdi veya hesap izni reddedildi. Yeniden bağla.");
            using var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
            var state = result.RootElement.GetProperty("state").GetString();
            if (state == "error") throw new InvalidOperationException(platform + " hesabı bağlanamadı. Geliştirici izinlerini kontrol et.");
            if (state == "ready") { SaveAccounts(platform, result.RootElement.GetProperty("accounts")); return; }
            await Task.Delay(TimeSpan.FromSeconds(2), timeout.Token);
        }
    }
    public void SaveAccounts(string platform, JsonElement data)
    {
        var accounts = data.EnumerateArray().ToArray();
        if (accounts.Length == 0) throw new InvalidOperationException(platform == "Facebook" ? "Yönetilen Facebook Sayfası bulunamadı." : "Paylaşım izni olan hesap bulunamadı.");
        foreach (var entry in accounts) {
            string id = entry.GetProperty("id").GetString()!, token = entry.GetProperty("access_token").GetString()!;
            if (id == "" || token == "") throw new InvalidOperationException("Eksik hesap bağlantı sonucu.");
            var account = store.Settings.SocialAccounts.FirstOrDefault(a => a.Platform == platform && a.Id == id);
            if (account == null) { account = new() { Platform = platform, Id = id }; store.Settings.SocialAccounts.Add(account); }
            account.Name = entry.GetProperty("name").GetString() ?? id;
            account.ExpiresAt = entry.TryGetProperty("expires_in", out var expiry) && expiry.GetInt32() > 0 ? DateTimeOffset.UtcNow.AddSeconds(expiry.GetInt32()) : null;
            store.Vault.Set(SocialPublisher.TokenKey(account), token);
            store.Vault.Set(SocialPublisher.RefreshKey(account), entry.TryGetProperty("refresh_token", out var refresh) ? refresh.GetString() ?? "" : "");
        }
        store.Save();
    }
    public void Remove(SocialAccount account)
    {
        store.Vault.Set(SocialPublisher.TokenKey(account), ""); store.Vault.Set(SocialPublisher.RefreshKey(account), ""); store.Settings.SocialAccounts.Remove(account); store.Save();
    }
}
