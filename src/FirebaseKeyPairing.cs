using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AnkaStudio;

public sealed class FirebaseKeyPairing : IDisposable
{
    readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(25) };
    readonly string apiKey, root;
    string token = "", uid = "";
    public static readonly string[] Allowed = ["GEMINI_API_KEY", "OPENAI_API_KEY", "OPENROUTER_API_KEY", "GROQ_API_KEY", "KIMI_API_KEY", "GOOGLE_TTS_API_KEY", "ELEVENLABS_API_KEY"];
    public FirebaseKeyPairing()
    {
        var assembly = typeof(FirebaseKeyPairing).Assembly;
        using var stream = assembly.GetManifestResourceStream(assembly.GetManifestResourceNames().Single(n => n.EndsWith("pairing-firebase.json")))!;
        using var config = JsonDocument.Parse(stream);
        apiKey = config.RootElement.GetProperty("apiKey").GetString()!;
        root = $"https://firestore.googleapis.com/v1/projects/{config.RootElement.GetProperty("project").GetString()}/databases/(default)/documents/ankaKeyPairs/";
    }
    async Task<JsonObject> Request(string url, HttpMethod method, JsonObject? body, bool auth, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, url);
        if (auth) request.Headers.Authorization = new("Bearer", token);
        if (body != null) request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Firebase eşleme isteği başarısız (HTTP {(int)response.StatusCode}). Anonim giriş, eşleme kodu ve Firestore kurallarını kontrol et.");
        await using var input = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream(); var bytes = new byte[8192]; int n;
        while ((n = await input.ReadAsync(bytes, ct)) > 0) { buffer.Write(bytes, 0, n); if (buffer.Length > 100000) throw new InvalidOperationException("Eşleme cevabı fazla büyük."); }
        return buffer.Length == 0 ? new() : JsonNode.Parse(buffer.ToArray())!.AsObject();
    }
    async Task Login(CancellationToken ct)
    {
        var d = await Request($"https://identitytoolkit.googleapis.com/v1/accounts:signUp?key={apiKey}", HttpMethod.Post, new() { ["returnSecureToken"] = true }, false, ct);
        token = d["idToken"]!.GetValue<string>(); uid = d["localId"]!.GetValue<string>();
    }
    static string Value(JsonObject doc, string name) => doc["fields"]![name]!["stringValue"]!.GetValue<string>();
    static JsonObject Field(string value) => new() { ["stringValue"] = value };
    async Task Update(string code, JsonObject doc, Dictionary<string, string> fields, CancellationToken ct)
    {
        var f = new JsonObject(); foreach (var (k,v) in fields) f[k] = Field(v);
        var mask = string.Join("&", fields.Keys.Select(k => "updateMask.fieldPaths=" + k));
        await Request(root + code + "?" + mask + "&currentDocument.updateTime=" + Uri.EscapeDataString(doc["updateTime"]!.GetValue<string>()), HttpMethod.Patch, new() { ["fields"] = f }, true, ct);
    }
    public async Task Send(string code, IReadOnlyDictionary<string,string> keys, Action<string> status, CancellationToken ct)
    {
        if (code.Length != 12 || !code.All(char.IsAsciiDigit)) throw new InvalidOperationException("Telefondaki 12 haneli eşleme kodunu gir.");
        if (keys.Count is < 1 or > 7 || keys.Any(k => !Allowed.Contains(k.Key) || k.Value.Length is < 1 or > 4096)) throw new InvalidOperationException("Aktarılacak anahtar listesi geçersiz.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromMinutes(5)); ct = timeout.Token;
        await Login(ct); var doc = await Request(root + code, HttpMethod.Get, null, true, ct);
        if (Value(doc,"state") != "waiting") throw new InvalidOperationException("Kod kullanımda. Telefonda yeni eşleme başlat.");
        using var pair = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var own = Convert.ToBase64String(pair.ExportSubjectPublicKeyInfo()); var peer = Value(doc,"receiverKey");
        if (peer.Length is < 100 or > 200) throw new InvalidOperationException("Telefon eşleme anahtarı geçersiz.");
        using var receiver = ECDiffieHellman.Create(); receiver.ImportSubjectPublicKeyInfo(Convert.FromBase64String(peer), out var consumed);
        if (receiver.KeySize != 256 || consumed != Convert.FromBase64String(peer).Length) throw new InvalidOperationException("Telefon eşleme anahtarı geçersiz.");
        var raw = pair.DeriveRawSecretAgreement(receiver.PublicKey);
        var shared = SHA256.HashData(Encoding.UTF8.GetBytes($"anka-pair-v1|{code}|{peer}|{own}|").Concat(raw).ToArray()); CryptographicOperations.ZeroMemory(raw);
        try
        {
            await Update(code, doc, new() { ["sender"] = uid, ["senderKey"] = own, ["state"] = "requested" }, ct);
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes("anka-sas-v1").Concat(shared).ToArray());
            var fingerprint = (System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(hash) % 1000000).ToString("D6");
            status($"Kontrol Numarası: {fingerprint}\nTelefonda aynı numara görünüyorsa onayla. Anahtarlar henüz gönderilmedi.");
            while (true)
            {
                await Task.Delay(4000,ct); doc = await Request(root + code,HttpMethod.Get,null,true,ct);
                if (Value(doc,"state") == "approved") break;
                if (Value(doc,"state") != "requested") throw new InvalidOperationException("Eşleme onaylanmadı.");
            }
            var clear = JsonSerializer.SerializeToUtf8Bytes(new { version = 1, code, keys });
            var nonce = RandomNumberGenerator.GetBytes(12); var cipher = new byte[clear.Length]; var tag = new byte[16];
            try { using var aes = new AesGcm(shared,16); aes.Encrypt(nonce,clear,cipher,tag,Encoding.UTF8.GetBytes(code)); } finally { CryptographicOperations.ZeroMemory(clear); }
            await Update(code, doc, new() { ["state"] = "delivered", ["nonce"] = Convert.ToBase64String(nonce), ["payload"] = Convert.ToBase64String(cipher.Concat(tag).ToArray()) }, ct);
            status("Şifreli anahtarlar gönderildi. Telefonda Anahtarları Kaydet düğmesine bas.");
            while (true) { await Task.Delay(3000,ct); doc = await Request(root+code,HttpMethod.Get,null,true,ct); if (Value(doc,"state") == "received") { await Update(code,doc,new() { ["state"] = "finished" },ct); break; } }
            status($"{keys.Count} anahtar telefona kaydedildi. Eşleme tamamlandı.");
        }
        finally { CryptographicOperations.ZeroMemory(shared); try { await Request(root+code,HttpMethod.Delete,null,true,CancellationToken.None); } catch { /* Expired records are inaccessible; no secret is logged. */ } }
    }
    public void Dispose() => http.Dispose();
}

public sealed partial class StudioForm
{
    void AndroidKeyTransfer()
    {
        var available = FirebaseKeyPairing.Allowed.Select(name => (name,value:store.Vault.Get(name))).Where(x=>!string.IsNullOrWhiteSpace(x.value)).ToList();
        if (available.Count == 0) { Message("Aktarılacak kayıtlı API anahtarı yok."); return; }
        using var f = new Form { Text="Android'e Anahtar Aktar", Width=650, Height=650, StartPosition=FormStartPosition.CenterParent, BackColor=Theme.Bg };
        var panel=new FlowLayoutPanel { Dock=DockStyle.Fill, FlowDirection=FlowDirection.TopDown, WrapContents=false, AutoScroll=true, Padding=new Padding(20) }; f.Controls.Add(panel);
        panel.Controls.Add(Theme.Label("Telefonda Ayarlar → Yapay Zekâ → Windows’tan Anahtar Al seç. Aynı Wi-Fi gerekmez. Yalnız seçilen API anahtarları aktarılır.",11,width:560,height:65));
        panel.Controls.Add(Theme.Label("Eşleştirme Kodu • Telefonda Görünen 12 Haneli Kod",11,true,Theme.Teal,560,32));
        var input=Theme.Input("",width:560); input.MaxLength=14; input.PlaceholderText="12 haneli eşleştirme kodunu buraya gir"; panel.Controls.Add(input);
        var choices=new CheckedListBox { Width=560,Height=170,BackColor=Theme.Panel,ForeColor=Theme.Text,CheckOnClick=true };
        foreach(var x in available)choices.Items.Add(x.name,true);panel.Controls.Add(choices);
        var state=Theme.Label("Telefondaki eşleme kodunu gir.",12,true,Theme.Teal,560,125);panel.Controls.Add(state);
        using var cancel=new CancellationTokenSource();bool busy=false;
        var send=Theme.Button("Eşle ve Anahtarları Gönder",()=>{},true,560);
        send.Click+=async (_,_)=>{if(busy)return;var chosen=choices.CheckedIndices.Cast<int>().Select(i=>available[i]).ToDictionary(x=>x.name,x=>x.value);if(chosen.Count==0){state.Text="En az bir anahtar seç.";return;}busy=true;send.Enabled=false;choices.Enabled=false;input.Enabled=false;
            try {using var client=new FirebaseKeyPairing();await client.Send(input.Text.Replace(" ",""),chosen,message=>{if(!f.IsDisposed)state.Text=message;},cancel.Token);}
            catch(OperationCanceledException){if(!f.IsDisposed)state.Text="Aktarım iptal edildi veya eşleme süresi doldu.";}
            catch(Exception){if(!f.IsDisposed)state.Text="Aktarım tamamlanamadı. Firebase anonim girişini, kuralları ve kodu kontrol et. Mevcut Windows anahtarları korunuyor.";}
            finally{busy=false;if(!f.IsDisposed){send.Enabled=true;choices.Enabled=true;input.Enabled=true;}}};panel.Controls.Add(send);
        f.FormClosing+=(_,_)=>cancel.Cancel();f.ShowDialog(this);
    }
}
