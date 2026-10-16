using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
namespace AnkaStudio;
public sealed class EditorDraft<T>
{
    public string BaseHash { get; set; } = "";
    public T? Value { get; set; }
}
public static class EditorDrafts
{
    public static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, Json.Options))));
    public static T Recover<T>(string path, string baseline, T original)
    {
        try {
            var draft = Json.Read(path, new EditorDraft<T>());
            return draft.BaseHash == baseline && draft.Value != null ? draft.Value : original;
        } catch (Exception ex) when (ex is JsonException or InvalidOperationException or NotSupportedException) {
            Json.RecoveryMessages.Enqueue("Editör taslağı okunamadı; özgün içerik açıldı. Taslak dosyası korundu."); return original;
        }
    }
    public static void Save<T>(string path, string baseline, T value) => Json.Write(path, new EditorDraft<T> { BaseHash = baseline, Value = value });
    public static void Remove(string path) { if (File.Exists(path)) File.Delete(path); if (File.Exists(path + ".bak")) File.Delete(path + ".bak"); }
}
