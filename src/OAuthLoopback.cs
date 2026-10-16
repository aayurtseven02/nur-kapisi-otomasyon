using System.Net;
using System.Net.Sockets;
using System.Text;

namespace AnkaStudio;

internal static class OAuthLoopback
{
    public static async Task<Dictionary<string,string>> Receive(TcpListener listener,string redirect,string state,CancellationToken ct)
    {
        while(true){
            using var client=await listener.AcceptTcpClientAsync(ct).ConfigureAwait(false);
            using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);deadline.CancelAfter(TimeSpan.FromSeconds(4));
            try{
                using var stream=client.GetStream();using var reader=new StreamReader(stream,Encoding.ASCII,false,1024,true);
                var first=await reader.ReadLineAsync(deadline.Token).ConfigureAwait(false)??"";
                var request=first.Split(' ');if(request.Length<3||request[0]!="GET"||request[1].Length>16000||!request[1].StartsWith('/'))continue;
                int bytes=first.Length;string? line;
                do{line=await reader.ReadLineAsync(deadline.Token).ConfigureAwait(false);bytes+=line?.Length??0;if(bytes>32000)throw new InvalidOperationException("Dönüş isteği çok büyük.");}while(!string.IsNullOrEmpty(line));
                var uri=new Uri(new Uri(redirect),request[1]);
                var query=new Dictionary<string,string>();bool duplicate=false;
                foreach(var part in uri.Query.TrimStart('?').Split('&',StringSplitOptions.RemoveEmptyEntries)){
                    var pair=part.Split('=',2);if(pair.Length!=2)continue;
                    if(!query.TryAdd(WebUtility.UrlDecode(pair[0]),WebUtility.UrlDecode(pair[1])))duplicate=true;
                }
                bool valid=!duplicate&&uri.Host=="127.0.0.1"&&uri.AbsolutePath=="/"&&query.GetValueOrDefault("state")==state&&(query.ContainsKey("code")||query.ContainsKey("error"));
                string message=valid?query.ContainsKey("error")?"Google izni verilmedi. Programa dönebilirsin.":"Google onayı TubeFlow’ya ulaştı. Bu sekmeyi kapatıp programa dön. İznin kaydedilme sonucu programda gösterilecek.":"Bu bağlantı geçerli bir onay dönüşü değil. TubeFlow'dan izin işlemini yeniden başlat.";
                var body=Encoding.UTF8.GetBytes("<!doctype html><html lang='tr'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>TubeFlow • Google bağlantısı</title></head><body style='margin:0;background:#101827;color:#ffffff;font:24px Segoe UI,Arial;padding:60px'><h1 style='color:#00ffdc'>TubeFlow</h1><p>"+message+"</p></body></html>");
                var header=Encoding.ASCII.GetBytes($"HTTP/1.1 {(valid?"200 OK":"400 Bad Request")}\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {body.Length}\r\nCache-Control: no-store\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(header,deadline.Token).ConfigureAwait(false);await stream.WriteAsync(body,deadline.Token).ConfigureAwait(false);await stream.FlushAsync(deadline.Token).ConfigureAwait(false);
                client.Client.Shutdown(SocketShutdown.Send);
                if(valid)return query;
            }catch(OperationCanceledException)when(!ct.IsCancellationRequested){}
            catch(IOException){}catch(SocketException){}
        }
    }
}
