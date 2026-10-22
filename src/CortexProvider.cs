namespace AnkaStudio;

// The assistant may prefer local chat without changing the production provider order.
public static class CortexProvider
{
    public static string? Preferred(Settings settings)=>settings.UseOllama&&settings.LocalAiFirst?"Ollama":null;
    public static async Task<string> Generate(Settings settings,CancellationToken ct,Func<CancellationToken,string?,Task<string>> generate)
    {
        string? provider=Preferred(settings);
        using var budget=CancellationTokenSource.CreateLinkedTokenSource(ct);
        if(provider!=null)budget.CancelAfter(TimeSpan.FromSeconds(90));
        try{return await generate(budget.Token,provider);}
        catch(OperationCanceledException ex)when(!ct.IsCancellationRequested){throw new InvalidOperationException("Cortex yerel modeli 90 saniyede yanıt vermedi. Ollama bağlantısını kontrol et veya Yerel motorlar bölümünde önce yerel model tercihini kapatıp seçili bulut sağlayıcısını dene.",ex);}
    }
}
