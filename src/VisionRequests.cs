using System.Net;
namespace AnkaStudio;

public static class VisionRequests
{
    public static async Task<HttpResponseMessage> Send(HttpClient http,Func<HttpRequestMessage> create,string provider,Action<string> log,CancellationToken ct,Func<TimeSpan,CancellationToken,Task>? delay=null)
    {
        delay??=Task.Delay;
        for(int attempt=0;;attempt++)
        {
            ct.ThrowIfCancellationRequested();using var req=create();using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(provider=="Ollama"?180:60));
            HttpResponseMessage response;
            try{response=await http.SendAsync(req,timeout.Token);}
            catch(HttpRequestException ex)when(ex.StatusCode==null&&attempt<2){log(provider+" bağlantısı kesildi • aynı dosya yeniden deneniyor ("+(attempt+1)+"/2)");await delay(TimeSpan.FromSeconds(3),ct);continue;}
            catch(OperationCanceledException)when(!ct.IsCancellationRequested&&attempt<2){log(provider+" bağlantısı gecikti • aynı dosya yeniden deneniyor ("+(attempt+1)+"/2)");await delay(TimeSpan.FromSeconds(3),ct);continue;}
            int status=(int)response.StatusCode;
            if(response.IsSuccessStatusCode)return response;
            string body=await Reliability.ReadText(response,100_000,ct);log(provider+" görsel analizi • HTTP "+status);
            bool transient=status is 408 or 429 or 500 or 502 or 503 or 504;
            if(transient&&attempt<2){
                var wait=Services.AiRateWait(response,body);
                if(wait==null){var match=System.Text.RegularExpressions.Regex.Match(body,"retryDelay\\\"\\s*:\\s*\\\"(\\d+(?:\\.\\d+)?)s");if(match.Success&&double.TryParse(match.Groups[1].Value,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var seconds)&&seconds<=59)wait=TimeSpan.FromSeconds(seconds+1);}
                bool daily=System.Text.RegularExpressions.Regex.IsMatch(body,"per.?day|daily|quota[^\\r\\n]{0,80}limit[^\\r\\n]{0,20}0",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if(!daily){wait??=TimeSpan.FromSeconds(status==429?30:5*(attempt+1));response.Dispose();log(provider+" geçici sınır/yoğunluk • "+Math.Ceiling(wait.Value.TotalSeconds)+" sn bekleniyor; hazır analizler korunuyor.");await delay(wait.Value,ct);continue;}
            }
            response.Dispose();throw new HttpRequestException(provider+" görsel analizi HTTP "+status,null,(HttpStatusCode)status);
        }
    }
}
