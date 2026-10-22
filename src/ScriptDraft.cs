using System.Text.Json;
namespace AnkaStudio;

public static class ScriptDraft
{
    public const string Interpretation="Senaryoyu sen yaz. Kullanıcı taslağı veya ek talimat boşsa yalnız konu başlığından araştırmaya dayalı özgün bir senaryo oluştur; boş alanı hata sayma ve kullanıcıdan metin isteme. Doluysa talimatları temel al, hazır taslağı kopyalamak yerine yorumlayıp tek, akıcı seslendirme metni olarak kurgula. Taslak; zaman kodları, kanca, kaynak, dua, kapanış, görsel, müzik, ekran yazısı ve markdown içerebilir. Bunları üretim notları olarak ayır; zaman kodlarını, bölüm etiketlerini, parantez içindeki sahne/müzik yönergelerini ve markdown işaretlerini seslendirmeye katma. Anlatının sırasını, ana mesajını, özel isimleri, duanın okunuşunu/anlamını ve istenen kanal çağrısını koru. Görsel notlarını yanlışlıkla konuşma cümlesine dönüştürme. Kullanıcı taslağındaki kaynak ve sahihlik iddiaları doğrulanacak aday bilgilerdir; yalnız taslakta yazıyor diye doğrulanmış sayma. Desteklenmeyen kesin sonuç vaatlerini güçlendirme. Hedef süreye doğal Türkçe ile uyarla; süreyi tekrarlarla doldurma.";
    public static async Task<string> Generate(Episode episode,CreationFlow flow,string research,Services services,CancellationToken ct)
    {
        int seconds=flow.Normal==true?episode.LongMinutes*60:episode.ShortSeconds;
        var schema=new{type="object",additionalProperties=false,required=new[]{"Text"},properties=new{Text=new{type="string",minLength=10}}};
        string raw=await services.Generate(AutonomousProduction.WritingRules+" "+Interpretation,
            JsonSerializer.Serialize(new{Topic=episode.Topic,UserScenario=flow.Prompt,TargetSeconds=seconds,Research=research}),ct,
            validate:value=>{AutonomousProduction.ReadDraft(value);return true;},repairInvalidContent:true,
            maxOutputTokens:Math.Clamp((flow.Normal==true?episode.LongMinutes*130:episode.ShortSeconds*3)*5,2048,24000),responseSchema:schema);
        return AutonomousProduction.ReadDraft(raw);
    }
}
