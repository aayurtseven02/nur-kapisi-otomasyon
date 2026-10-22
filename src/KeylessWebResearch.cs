using System.Net;
using System.Xml.Linq;
using System.Text.RegularExpressions;
namespace AnkaStudio;

public static class KeylessWebResearch
{
    public static string Article(string html)
    {
        html=Regex.Replace(html,@"<(script|style|nav|footer|header)[^>]*>.*?</\1>"," ",RegexOptions.IgnoreCase|RegexOptions.Singleline);
        var opening=Regex.Match(html,"<(article|main)\\b[^>]*>|<(div)\\b[^>]*class=[\"'][^\"']*entry-content[^\"']*[\"'][^>]*>",RegexOptions.IgnoreCase);
        if(opening.Success){string tag=opening.Groups[1].Success?opening.Groups[1].Value:"div";int depth=1,start=opening.Index+opening.Length;foreach(Match m in Regex.Matches(html[start..],"</?"+tag+"\\b[^>]*>",RegexOptions.IgnoreCase)){depth+=m.Value.StartsWith("</")?-1:1;if(depth==0){html=html.Substring(start,m.Index);break;}}}
        else{var paragraphs=Regex.Matches(html,@"<p\b[^>]*>(.*?)</p>",RegexOptions.IgnoreCase|RegexOptions.Singleline).Select(m=>m.Groups[1].Value).Where(p=>Regex.Replace(p,"<[^>]+>","").Length>=80).ToArray();if(paragraphs.Length>0)html=string.Join(" ",paragraphs);}
        return Regex.Replace(WebUtility.HtmlDecode(Regex.Replace(html,"<[^>]+>"," ")),@"\s+"," ").Trim();
    }
    public static bool Relevant(string text,string query)
    {
        string Normalize(string value)=>new string(value.ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD).Where(c=>System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)!=System.Globalization.UnicodeCategory.NonSpacingMark).ToArray()).Replace('ı','i');
        var ignored=new HashSet<string>{"the","and","for","bir","ile","icin","metni","turkce","meali","hakkinda","nedir","anlat","video","hikayesi","hadis","duasi"};
        var terms=Regex.Matches(Normalize(query),@"\p{L}{3,}").Select(x=>x.Value).Where(x=>!ignored.Contains(x)).Distinct().ToArray();
        string normalized=Normalize(text);return terms.Length==0||terms.Count(t=>normalized.Contains(t,StringComparison.Ordinal))>=Math.Min(2,terms.Length);
    }
    static bool Private(IPAddress address){var a=address.IsIPv4MappedToIPv6?address.MapToIPv4():address;var b=a.GetAddressBytes();return IPAddress.IsLoopback(a)||(b.Length==16?(a.IsIPv6LinkLocal||a.IsIPv6SiteLocal||a.IsIPv6Multicast||(b[0]&254)==252):b[0] is 0 or 10 or 127 or >=224||b[0]==169&&b[1]==254||b[0]==192&&b[1]==168||b[0]==172&&b[1] is >=16 and <=31);}
    public static async Task<List<(SourceRef Source,string Text)>> Collect(HttpClient http,string topic,CancellationToken ct)
    {
        var rows=new List<(SourceRef Source,string Text)>();
        using var budget=CancellationTokenSource.CreateLinkedTokenSource(ct);budget.CancelAfter(TimeSpan.FromSeconds(60));var token=budget.Token;
        try{
            using var result=await http.GetAsync("https://html.duckduckgo.com/html/?q="+Uri.EscapeDataString(topic[..Math.Min(200,topic.Length)]),HttpCompletionOption.ResponseHeadersRead,token);result.EnsureSuccessStatusCode();
            string html=await Reliability.ReadText(result,2_000_000,token);
            var targets=Regex.Matches(html,"<a\\b[^>]*>",RegexOptions.IgnoreCase).Where(a=>Regex.IsMatch(a.Value,"class=[\"'][^\"']*result__a",RegexOptions.IgnoreCase)).Select(a=>WebUtility.HtmlDecode(Regex.Match(a.Value,"href=[\"']([^\"']+)[\"']",RegexOptions.IgnoreCase).Groups[1].Value)).ToList();
            if(targets.Count==0){
                using var rss=await http.GetAsync("https://www.bing.com/search?format=rss&q="+Uri.EscapeDataString(topic[..Math.Min(200,topic.Length)]),token);rss.EnsureSuccessStatusCode();
                string xml=await Reliability.ReadText(rss,2_000_000,token);targets.AddRange(XDocument.Parse(xml).Descendants("item").Select(x=>x.Element("link")?.Value??""));
            }
            foreach(string candidate in targets.Take(12)){
                string target=candidate;var encoded=Regex.Match(target,@"[?&]uddg=([^&]+)");if(encoded.Success)target=Uri.UnescapeDataString(encoded.Groups[1].Value);
                if(!Uri.TryCreate(target,UriKind.Absolute,out var uri)||uri.Scheme!="https"||uri.Port!=443||Uri.CheckHostName(uri.Host)!=UriHostNameType.Dns)continue;
                try{
                    var addresses=await Dns.GetHostAddressesAsync(uri.Host,token);
                    if(addresses.Length==0||addresses.Any(Private))continue;
                    using var client=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(12)};
                    using var response=await client.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,token);if(!response.IsSuccessStatusCode||response.Content.Headers.ContentType?.MediaType!="text/html")continue;
                    string raw=await Reliability.ReadText(response,2_000_000,token),title=WebUtility.HtmlDecode(Regex.Match(raw,@"<title[^>]*>(.*?)</title>",RegexOptions.IgnoreCase|RegexOptions.Singleline).Groups[1].Value);
                    raw=Article(raw);
                    if(raw.Length<300||!Relevant(title+" "+raw,topic))continue;
                    rows.Add((new SourceRef{Title=title==""?uri.Host:title,Url=uri.AbsoluteUri},raw[..Math.Min(9000,raw.Length)]));
                    if(rows.Select(x=>new Uri(x.Source.Url).Host).Distinct().Count()>=2)break;
                }catch(Exception error)when(error is HttpRequestException or System.Net.Sockets.SocketException or InvalidOperationException or OperationCanceledException){ct.ThrowIfCancellationRequested();}
            }
        }catch(Exception error)when(error is HttpRequestException or InvalidOperationException or System.Xml.XmlException or OperationCanceledException){ct.ThrowIfCancellationRequested();}
        return rows;
    }
}
