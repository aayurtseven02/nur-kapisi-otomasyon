# Üçüncü taraf bileşenler

- **.NET 10 / Windows Desktop runtime** — Microsoft; MIT ve bileşenlere özgü üçüncü taraf lisansları. Çalışma ortamı bildirimleri Windows dağıtım klasöründedir. https://github.com/dotnet/runtime ve https://github.com/dotnet/winforms
- **Sodium.Core 1.4.1** — MIT; https://github.com/ektrah/libsodium-core . GitHub secret şifrelemesinde kullanılır.
- **libsodium 1.0.22** — ISC; https://github.com/jedisct1/libsodium . Sodium.Core bağımlılığıdır.
- **FFmpeg 9.0.2 essentials, Gyan build** — dağıtımın LICENSE dosyası `Windows/tools/FFmpeg-LICENSE.txt` altında korunmuştur. Derleme yapılandırması GPL etkin bileşenler içerir. Dağıtım / yapılandırma / kaynak bağlantıları: https://www.gyan.dev/ffmpeg/builds/ ; FFmpeg kaynak kodu: https://ffmpeg.org/download.html ; kaynak deposu: https://git.ffmpeg.org/ffmpeg.git . FFmpeg ayrı bir işlem olarak çalıştırılır; uygulamaya statik bağlanmaz.

Stok videolar bu pakete dahil değildir. Pexels/Pixabay görüntüleri ancak kullanıcı kendi API anahtarını eklediğinde indirilir; kaynak ve lisans adresleri proje başına kaydedilir. Müzik kullanıcı tarafından sağlanır.

## 0.2.87 yerel ses eşleme paketi

Python çalışma ortamı ve kendi LICENSE.txt dosyası tools/audio-alignment/runtime altındadır. Dağıtılan Python paketlerinin sürüm ve lisans dosyası envanteri notices/alignment-components.json içindedir; paketlere ait lisanslar özgün dist-info dizinlerinde korunmuştur. PyAV farklı sürüm kalıntıları da envanterde ayrı gösterilir; yalnızca yüklenen sürüm varsayılmamıştır.

Model: Systran/faster-whisper-small; OpenAI Whisper small modelinin CTranslate2 dönüşümü. [Model kaynağı](https://huggingface.co/Systran/faster-whisper-small) MIT lisansı belirtir. [Whisper lisansı](https://github.com/openai/whisper/blob/main/LICENSE) notices/Whisper-LICENSE.txt içinde korunmuştur. Model çalışırken ses bilgisayarda işlenir. Envanter, ticari dağıtımın hukuki uygunluk onayı değildir; kaynak sunma ve diğer yükümlülükler satış öncesinde yayıncı tarafından denetlenmelidir.


## Kurulum listesi marka simgeleri (0.4.25)

Marka simgeleri yalnız ilgili ürünü tanıtmak ve resmî kurulum sayfasını göstermek için kullanılır. Markalar sahiplerine aittir; TubeFlow ile ortaklık/onay anlamına gelmez. Resmî kaynak adresleri:

- ollama: https://ollama.com/public/icon-16x16.png
- ffmpeg: https://ffmpeg.org/favicon.ico
- python: https://www.python.org/static/favicon.ico
- comfy: https://www.comfy.org/favicon-96x96.png
- qwen: https://img.alicdn.com/imgextra/i4/O1CN01OXv3EM1FN8t9W4P79_!!6000000000474-2-tps-80-80.png
- google: https://www.gstatic.com/images/branding/productlogos/ai_studio/v1/web-32dp/logo_ai_studio_color_1x_web_32dp.png
- nvidia: https://build.nvidia.com/favicon.ico?favicon.40e0a499ra0f3.ico
- groq: https://console.groq.com/favicon.ico?cache=rounded-bolt
- openrouter: https://openrouter.ai/favicon/glyph.png
- kimi: https://platform.kimi.ai/favicon.ico?v=3
- tavily: https://app.tavily.com/favicon.ico
- vecteezy: https://www.vecteezy.com/favicon.png
- elevenlabs: https://elevenlabs.io/favicon.ico?9c55860cee08706b
- github: https://github.com/fluidicon.png

## Microsoft Edge speech adapter
TubeFlow includes edge-tts 7.3.1 (LGPL-3.0); its srt_composer component is MIT licensed. The packaged Python dependency directories retain their license metadata and source files. Python is distributed under the Python Software Foundation License. edge-tts is a third-party client, not an official Microsoft SDK; online service availability can change.
Project: https://github.com/rany2/edge-tts


## Local LTX inference
TubeFlow's adapter builds its own ComfyUI graph and calls the local server. ComfyUI, VideoHelperSuite and LTX model weights are installed separately and retain their upstream licenses. No model weights are bundled in the TubeFlow update ZIP. LTX project: https://github.com/Lightricks/LTX-Video ; ComfyUI: https://github.com/Comfy-Org/ComfyUI ; VideoHelperSuite: https://github.com/Kosinkadink/ComfyUI-VideoHelperSuite .
