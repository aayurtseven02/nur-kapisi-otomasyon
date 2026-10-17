using System.Text.Json;
using System.Text.RegularExpressions;
namespace AnkaStudio;

public static class SourceSafety
{
    static readonly Regex Tokens=new(@"(?:gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{30,}|sk-(?:proj-)?[A-Za-z0-9_-]{32,}|AIza[0-9A-Za-z_-]{35})",RegexOptions.CultureInvariant);
    static readonly Regex PrivatePem=new(@"-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----[\sA-Za-z0-9+/=]{64,}-----END (?:RSA |EC |OPENSSH )?PRIVATE KEY-----",RegexOptions.CultureInvariant);
    public static void ValidateText(string relative,string text,IEnumerable<string>? knownSecrets=null)
    {
        void Reject()=>throw new InvalidOperationException("Kaynak aktarımı durduruldu: "+relative+" • gizli anahtar veya özel kimlik bilgisi bulundu; değer gösterilmedi.");
        if(knownSecrets?.Any(value=>value.Length>=12&&text.Contains(value,StringComparison.Ordinal))==true||PrivatePem.IsMatch(text.Replace("\\n","\n").Replace("\\r","\r")))Reject();
        if(relative.Replace('\\','/')=="src/Assets/pairing-firebase.json"){
            using var doc=JsonDocument.Parse(text);var root=doc.RootElement;
            if(root.ValueKind!=JsonValueKind.Object||root.EnumerateObject().Any(x=>x.Name is not ("apiKey" or "project"))||!root.TryGetProperty("apiKey",out var key)||key.ValueKind!=JsonValueKind.String||!Regex.IsMatch(key.GetString()??"",@"^AIza[0-9A-Za-z_-]{35}$")||!root.TryGetProperty("project",out var project)||project.ValueKind!=JsonValueKind.String||string.IsNullOrWhiteSpace(project.GetString()))Reject();
            return;
        }
        if(Tokens.IsMatch(text))Reject();
    }
    public static void ValidateFiles(string source,IEnumerable<string>? knownSecrets=null)
    {
        var secrets=knownSecrets?.Where(x=>x.Length>=12).Distinct().ToArray();
        foreach(var file in Directory.EnumerateFiles(source,"*",SearchOption.AllDirectories)){
            var relative=Path.GetRelativePath(source,file).Replace('\\','/');if(!GitHub.AllowedSource(relative)||Path.GetExtension(file).ToLowerInvariant() is ".png" or ".ico")continue;
            if(new FileInfo(file).Length>20_000_000)throw new InvalidOperationException("Kaynak dosyası fazla büyük: "+relative);
            ValidateText(relative,File.ReadAllText(file),secrets);
        }
    }
}
