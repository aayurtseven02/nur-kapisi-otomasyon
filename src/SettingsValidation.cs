namespace AnkaStudio;



public static class SettingsValidation

{

    public static void Check(Settings settings)

    {

        if(settings.PronunciationDictionary.Length>10000 || settings.PronunciationDictionary.Split('\n').Any(line=>line.Trim()!="" && (line.Split('=',2).Length!=2 || line.Split('=',2).Any(string.IsNullOrWhiteSpace))))throw new InvalidOperationException("Telaffuz sözlüğünde her satır yazılış=okunuş biçiminde olmalı (en fazla 10.000 karakter).");

        if (settings.OllamaTimeoutMinutes is < 1 or > 30) throw new InvalidOperationException("Yerel AI bekleme süresi 1–30 dakika arasında olmalı.");
        if(settings.VoiceProvider==EdgeTts.Provider&&!EdgeTts.TurkishVoices.Any(x=>x.Id==settings.EdgeTtsVoice))throw new InvalidOperationException("Geçerli Türkçe Edge sesi seç.");

        if (!LocalAi.IsLocalName(settings.OllamaModel)) throw new InvalidOperationException("Ollama için bir yerel model seç; bulut modeli bu alanda kullanılamaz.");

        if (settings.EnabledCloudProviders.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Any(p => !new[] { "Gemini", "NVIDIA", "Groq", "OpenRouter", "Kimi", "OpenAI", "Pollinations" }.Contains(p))) throw new InvalidOperationException("Bulut AI izin listesinde geçersiz sağlayıcı var.");

        var providers = settings.ProviderOrder.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (providers.Length == 0 || providers.Any(p => !new[] { "Gemini", "NVIDIA", "Groq", "OpenRouter", "Kimi", "Ollama", "OpenAI", "Pollinations" }.Contains(p))) throw new InvalidOperationException("Yapay zekâ alternatif sırasında geçersiz sağlayıcı var.");

        var stock = settings.StockOrder.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (stock.Any(p => !new[] { "Pexels", "Pixabay", "Vecteezy" }.Contains(p))) throw new InvalidOperationException("Stok sırasında desteklenmeyen sağlayıcı var.");

        foreach (var model in new[] { settings.GeminiModel, settings.NvidiaModel, settings.GroqModel, settings.OpenRouterModel, settings.KimiModel, settings.OllamaModel, settings.Model })

            if (string.IsNullOrWhiteSpace(model) || model.Length > 150 || model.Any(char.IsWhiteSpace)) throw new InvalidOperationException("Model kimliği boş veya geçersiz.");

        if (settings.GroqModel.Contains("guard", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Groq güvenlik modeli senaryo yazmak için kullanılamaz; metin modeli seç.");

        if (!double.IsFinite(settings.VoiceSpeed) || !double.IsFinite(settings.ShortVoiceSpeed) || settings.VoiceSpeed <= 0 || settings.ShortVoiceSpeed <= 0 || settings.DailyProductionLimit <= 0 || settings.PoolRepeatHours < 0) throw new InvalidOperationException("Üretim hız/kota ayarları geçersiz.");

    }

}

