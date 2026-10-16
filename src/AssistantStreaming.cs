using System.Text;
using System.Text.Json;
namespace AnkaStudio;
public static class AssistantStreaming
{
    public static async Task<string> Read(HttpResponseMessage response,bool local,Action<string> partial,CancellationToken ct)
    {
        string? type=response.Content.Headers.ContentType?.MediaType;
        if(type=="application/json" && !local){var plain=await Reliability.ReadText(response,2_000_000,ct);return plain;}
        await using var stream=await response.Content.ReadAsStreamAsync(ct);using var reader=new StreamReader(stream);
        var content=new StringBuilder();string finish="";bool completed=false;int count=0;var clock=System.Diagnostics.Stopwatch.StartNew();
        partial("");
        while(await reader.ReadLineAsync(ct) is string line){
            ct.ThrowIfCancellationRequested();if(++count>50000 || line.Length>200000)throw new InvalidOperationException("Sohbet akışı sınırı aştı.");
            if(!local){if(!line.StartsWith("data:"))continue;line=line[5..].Trim();if(line=="[DONE]"){completed=true;break;}}
            if(string.IsNullOrWhiteSpace(line))continue;using var doc=JsonDocument.Parse(line);var root=doc.RootElement;
            if(root.TryGetProperty("error",out _))throw new InvalidOperationException("Sağlayıcı sohbet akışında hata bildirdi.");
            if(local){
                if(root.TryGetProperty("message",out var message) && message.TryGetProperty("content",out var text) && text.ValueKind==JsonValueKind.String)content.Append(text.GetString());
                if(root.TryGetProperty("done",out var done) && done.ValueKind==JsonValueKind.True){completed=true;finish=root.TryGetProperty("done_reason",out var reason)?reason.GetString()??"stop":"stop";}
            } else if(root.TryGetProperty("choices",out var choices) && choices.GetArrayLength()>0){
                var choice=choices[0];if(choice.TryGetProperty("delta",out var delta) && delta.TryGetProperty("content",out var text) && text.ValueKind==JsonValueKind.String)content.Append(text.GetString());
                if(choice.TryGetProperty("finish_reason",out var reason) && reason.ValueKind==JsonValueKind.String){finish=reason.GetString()??"";completed=true;}
            }
            if(content.Length>1_000_000)throw new InvalidOperationException("Sohbet yanıtı sınırı aştı.");
            if(clock.ElapsedMilliseconds>=100 || completed){partial(content.ToString());clock.Restart();}
            if(local && completed)break;
        }
        if(!completed)throw new AiContentException("Yanıt akışı kesildi; tamamlanmış cevap kaydedilmedi.");
        partial(content.ToString());
        return local?JsonSerializer.Serialize(new{message=new{content=content.ToString()},done=true,done_reason=finish}):JsonSerializer.Serialize(new{choices=new[]{new{finish_reason=finish,message=new{content=content.ToString()}}}});
    }
    public static string Answer(string json)
    {
        var match=System.Text.RegularExpressions.Regex.Match(json,"\\\"answer\\\"\\s*:\\s*\\\"");if(!match.Success)return "";
        int start=match.Index+match.Length;var result=new StringBuilder();
        for(int i=start;i<json.Length;i++){
            char value=json[i];if(value=='"')break;if(value!='\\'){result.Append(value);continue;}
            if(++i>=json.Length)break;char escape=json[i];
            if(escape=='u'){if(i+4>=json.Length)break;if(ushort.TryParse(json.AsSpan(i+1,4),System.Globalization.NumberStyles.HexNumber,null,out var unicode)){result.Append((char)unicode);i+=4;}else break;}
            else result.Append(escape switch{'n'=>'\n','r'=>'\r','t'=>'\t','b'=>'\b','f'=>'\f','"'=>'"','\\'=>'\\','/'=>'/',_=>escape});
        }
        return result.ToString();
    }
}
