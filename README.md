# TubeFlow 0.4.23 — Kapak ve üretim akışı

Kapak değişiklikleri adımlar arası geçişte kaydedilir; eski küçük resim yeni seçimin yerine kullanılmaz. Kontrol ekranından kapak editörüne doğrudan erişilir. AI kapak tasarımında başlık ve yatay/dikey oran otomatik uygulanır; oluştur/kaydet düğmeleri görünür kalır. Maksimum kalite iki motorda da tam 16:9 üretir. Seçili yerel kapak motoru bulut kotasından önce denenir. Hazır sahne sayısı canlı güncellenir; tek sahne yenilemede düzeltme talimatı yazılabilir.

# TubeFlow 0.4.22 — Hesap menüsü ve tuş hizası

YouTube / Google OAuth alanları varsayılan kapalı tek açılır başlık altında toplandı. Hesaplar ve AI motorları ekranındaki işlem tuşları ortak genişlik, sol hizası ve aralıklarla düzenlendi; pencere boyutu değişince aynı hiza korunur.

# TubeFlow 0.4.21 — Koyu giriş logosu

0.4.20 ortak koyu görünümüne ek olarak giriş ekranı beyaz yazılı TubeFlow logosunu kullanır.

# TubeFlow 0.4.20 — Ortak koyu görünüm

Masaüstü referansına göre dar simge şeridi, açılıp kapanan koyu sol menü, son proje bağlantıları, yuvarlatılmış çalışma alanı ve mavi seçim vurgusu uygulandı. Tüm ortak tuşlar, metin/süre alanları, kutucuklar, tablolar, kaydırma çubukları, giriş ve bilgi pencereleri aynı koyu yüzeyleri kullanır. Başlıklarda normal kalın yazı kullanılır; zorunlu italik/büyük harf dönüşümü kaldırıldı. Sohbette seçilebilir mesajlar ve mavi kullanıcı balonları vardır. Menü kapalıyken sohbet simge şeridinden açılabilir. Projeler, üretim tercihleri ve yayın izinleri korunur.

# TubeFlow 0.4.19 — Derin tarama düzeltmeleri

İptalde önceki sahne planı korunur. Asistan geri alma işlemi yüklenmiş/sonradan değişmiş çıktılara uygulanmaz; paylaşım kapalıyken araç hatası metni modele gönderilmez. Ses/müzik ayar grubu açılışı düzeltildi. Genel görsel kalite ve AI önbellek bütünlüğü denetlenir; kullanıcı görseli eski AI işaretinden ayrılır. Yedek anlatıcı sesi seçili sağlayıcının önbelleği gibi kullanılmaz; kurulu olmayan seçili Windows sesi sessizce değiştirilmez. Kaynak aktarımı ve güncelleme paketi öncesinde gizli bilgi içeriği denetlenir; kritik hata tekrar kontrolleri GitHub Windows iş akışına eklendi.

# TubeFlow 0.4.18 — İşlem yapabilen uygulama asistanı

Sohbet ses, sahne, altyazı ve müzik ayarlarını doğrudan uygulayabilir; yaptığı değişiklikleri bildirir ve son ayarı geri alabilir. Hata durumunda bilinen ayar sorunları otomatik denetlenir. İstek üzerine yerel motor kontrolü ve tek üretim yeniden denemesi yapılabilir. Kod, shell/komut, API anahtarı düzenleme, silme ve video yayınlama araçları yoktur. Ücret izinleri, kotalar, metin ve kullanım hakları onayları korunur. Proje metni/hatası paylaşımı ayrı kutuya bağlıdır.

# TubeFlow 0.4.17 — Bilgisayara göre otomatik motor

Her kurulu motorun PyTorch ortamında gerçek GPU işlemi denenir. Kullanılabilir NVIDIA CUDA, AMD ROCm, Intel XPU veya DirectML donanımı algılanır; klasör adı işlemci/GPU yeteneği kabul edilmez. Uyumlu GPU seçenekleri CPU'dan önce denenir; GPU çalışmazsa CPU modu kullanılır. CPU modu daha yavaştır. Bu özellik kurulu, sürücüsü ve modelleri hazır motorları seçer; ayrı motor paketlerini kendiliğinden indirmez. 0.4.16 çok sahneli anlatım ve ses seçimi düzeltmeleri dahildir.

# TubeFlow 0.4.16 — Sahne sahne anlatım ve seçili ses

Otomatik AI görselleri onaylı metni 5–10 görsel sahneye ayırır; eski tek bölüm tercihi bu modda tek görsele zorlamaz. Bölümleme servisi hata verirse metin korunarak cümle geçişlerinden yerel sahne planı çıkarılır. Her sahne için kendi anlatımına uygun ayrı görsel oluşturulur. Genel ayarlardaki son ses seçimi kullanılır; projeye özel açık ses seçimi korunur. Eski tek sahneli çıktılar için İzle ve düzelt adımında yeniden hazırlama düğmesi vardır. 0.4.15 sohbet ve neon dolgu düzeltmeleri dahildir.

# TubeFlow 0.4.15 — Sohbet görünürlüğü

Sol menünün altındaki sohbet düğmesini panel dışına taşıyan eski konumlandırma kaldırıldı. Düğme pencere boyutu değiştiğinde ve menü daraltılıp açıldığında görünür kalır.

# TubeFlow 0.4.14 — Otomatik görsel motoru kurtarma

Uyumlu NVIDIA motoruna öncelik verilir, açılmayan motor yerine diğer kurulumlar denenir ve motor açılış kayıtları tutulur. Görsel denemesinden sonra AI karakter/sahne planı korunur. Yerel seçenekler başarısızsa yalnız mevcut ücret izni, aylık sınır ve anahtarlar çerçevesinde Gemini/OpenAI yedeği kullanılabilir. Ayrı GitHub derleme kontrolünde FFmpeg testlerden önce hazırlanır.

# TubeFlow 0.4.13 — Sol menüde sohbet

Sohbet düğmesi sol menünün altına taşındı. Menü daraltılınca simge olarak görünür. Arşiv dışındaki gereksiz ikinci müzik önizleme düğmesi kaldırılmıştır.

# TubeFlow 0.4.12 — Müzik bölümünü sadeleştirme

Seçili müziği ön dinle düğmesi ve ikinci oynatıcı kaldırıldı. Arşiv listesindeki önizleme, seçili parçanın adı ve konuşma sesi önizlemesi korunur.

# TubeFlow 0.4.11 — Adlandırma ve seçim son kontrolleri

Rehberdeki son eski ad güncellendi. Hazır motor seçiminin yanında gelişmiş özel motor dosyası korunur. Arşivde eksik müzik dosyası seçimi anlaşılır uyarıyla ele alınır. 0.4.10 müzik, kapak motoru/kalite, EXE adı ve simge değişiklikleri dahildir.

# TubeFlow 0.4.10 — Proje müziği, kapak motoru ve adlandırma

Proje müziği üretim kontrolünde korunur ve seçilen parça adı görünür. Kapak tasarımında FLUX.2 / Stable Diffusion XL ve Minimum / Orta / Maksimum kalite seçilir. Ana program TubeFlow.exe, masaüstü simgesi büyük TubeFlow amblemidir. Görünen eski adlar güncellenmiştir. Eski kurulumların ilk güncellemeyi alabilmesi için uyumluluk dosyaları korunur.

# TubeFlow 0.4.9 — Ses ve müzik önizlemesi

Seçili müzik uygulama içinde dinlenebilir. Müzik arşivi seçiminde her parça için ayrı Ön dinle ve Seç düğmeleri bulunur. Ses örneği de dış oynatıcı açmadan uygulama içinde çalınır.

# TubeFlow 0.4.8 — Yerleşik ses önizlemesi

Seçili sesi kısa dinle düğmesi dış program yerine ses bölümündeki küçük oynatıcıyı kullanır. Oynat/duraklat, durdur, zaman ve kaydırma kontrolleri bulunur.

# TubeFlow 0.4.7 — Sabit karakterler yardım düğmesi

Sabit karakterler başlığının yanındaki i düğmesi alanın amacını, kullanımını ve örnek karakter tarifini açıklar.

# TubeFlow 0.4.6 — Belirgin onay rengi

Tamamlanmış üretim adımları canlı, neona yakın yeşille gösterilir. Siyah yazı ve onay işareti korunur. Kaynak adımındaki büyük durum kutusu kaldırılmıştır.

# TubeFlow 0.4.5 — Kaynak ve Metin sadeleştirmesi

Kaynak ve Metin adımındaki gereksiz büyük durum kutusu kaldırıldı. AI yükleme göstergesi ve metin onay düğmesi korunur.

# TubeFlow 0.4.4 — AI metin yükleme göstergesi

AI metni hazırlanırken dönen simge ve görünür durum satırı gösterilir. İşlem düğmesi Metin hazırlanıyor olarak değişir ve tekrar tıklanamaz. Başarı veya hata sonrasında yükleme durumu temizlenir.

# TubeFlow 0.4.3 — Süre kutuları

Süre girişlerinin kesilmiş çerçeveleri düzeltildi. Yerel sayı düzenleme ve artırma/azaltma davranışı korunarak tek yuvarlak çerçeve çizilir; ok alanı dış çizgiyi örtmez.

# TubeFlow 0.4.2 — Hizalama ve çizim düzeltmeleri

İşlem düğmeleri sola hizalandı. Yuvarlak köşelerdeki siyah artıklar giderildi. Merkez bölüm başlıkları aynı genişlikte kısaltıldı; iç başlıklar aynı çizgiye alındı. Servis kartlarının altındaki kesilme düzeltildi. Gerçek pencere görüntüsüyle görsel kontroller yapıldı.

# TubeFlow 0.4.1 — Yumuşak, camsı görünüm

Düğmeler beyaz–açık gri geçişli ve yuvarlak köşelidir. Ana işlem düğmelerinin siyah dolgusu kaldırıldı. Metin/şifre kutuları, göz düğmesi, listeler ve kartlarda yuvarlak kenarlar; desteklenen Windows pencerelerinde yuvarlak dış köşeler kullanılır. Sol menü daha koyu açık fümedir; seçili satır açık gri/beyaz dolgu ve çerçeveyle belirtilir. Kaydırma kanalı ve gri tutamaç ince siyah konturludur; yatay/dikey birleşimdeki siyah blok giderildi. Seçim kutuları füme dolgulu, siyah konturlu ve belirgin çentiklidir. Bölüm ve tablo başlıkları büyük harf, kalın ve italiktir; açılır bölüm başlıkları sola hizalıdır.

---

# TubeFlow 0.4.0 — Beş adımda içerik üretimi

TubeFlow, önceki Anka Studio kurulumlarının güncelleme ve veri uyumluluğunu koruyarak yeni beyaz arayüze geçer. Uygulama dosyasının adı bu geçişte AnkaStudio.exe olarak korunur.

1. Kaynak ve Metin: yatay/dikey tür ve süre; kendi metnin veya talimatla AI metni; incele ve onayla.
2. Tasarım: birlikte seçilebilen görsel kaynakları, AI veya MP3/WAV ses, isteğe bağlı müzik, zorunlu altyazı kararı ve beş kalıp, kapak/kart/arşiv.
3. Üretim: otomatik veya manuel yöntem; isteğe bağlı AI bölümleme; süre çubuğu, bölüm kaldırma ve geri alma.
4. İzle ve düzelt: oynatıcı, sahne metni/görseli, ayrı altyazı, müzik ve yayın metni; güncel video onayı.
5. Yayın: MP4 kaydet, özel yükle, tarih seç veya açık onayla yayınla.

Yan menü daraltılıp genişletilebilir. Ayarlar açılıp kapanan gruplardadır. Normal videoya ek Shorts hazırlanabilir; her kısa metin ayrıca onaylanır. Yüklenmiş tek konuşma dosyasından bağımsız Shorts sesleri otomatik çıkarılmaz.

Güncelleme GitHub Releases üzerinden normal açılışta kontrol edilir. Korumalı kurulum klasörü için Windows yönetici onayı isteyebilir. Kişisel proje ve anahtarlar kaynak paketine dahil edilmez.

---

## Önceki değişiklik notları

# TubeFlow 0.2.12 — Groq JSON hata düzeltmesi

Groq HTTP 400 json_validate_failed hatasında doğrudan alternatif sağlayıcıya geçmek yerine aynı sağlayıcıdan bir kez standart sohbet yanıtı istenir. İlk istek JSON modu kullanır; yalnızca bu özel hata sonrası ikinci istekte response_format gönderilmez. JSON ayrıştırma, şema ve içerik kontrolü uygulamada devam eder; geçersiz yanıt başarılı sayılmaz. Senaryo hazırlama ve otonom yazım denetimi bu düzeltmeyi kullanır. Diğer HTTP 400 hataları bu değişiklikten etkilenmez. Gemini HTTP 402 API bakiyesi ve OpenAI ücretsiz mod kısıtı bu düzeltmeyle değişmez. Canlı Groq başarısı model/hesap koşullarına bağlıdır.

---
# TubeFlow 0.2.11 — Otonom yazım denetimi

Yeni senaryo hazırlandığında, ses/video üretiminden önce ayrı bir Türkçe yazım denetimi otomatik çalışır. Başlık, açıklama, kapak metni, anlatım, ekran yazısı ve özel altyazı metni kontrol edilir. Aynı AI alternatif sırası, ücretsiz kullanım izinleri ve hız sınırı beklemesi kullanılır. Tespit edilen yazım/dil bilgisi düzeltmeleri otomatik uygulanır; işlem kayıtlarında alan, önceki/yeni metin ve gerekçe raporlanır.

Paket klasörünün yazim-denetimi alt klasöründe önceki senaryo ve ayrıntılı JSON raporu korunur. Editörde metin değişirse üretim öncesi tekrar denetlenir; değişmeyen ve kontrol edilmiş metin için gereksiz yeni API isteği yapılmaz. Stok dosyaları, kaynaklar, zamanlama, müzik ve tasarım alanları denetim tarafından değiştirilmez. Denetim tamamlanamazsa üretim durur ve yeniden denendiğinde denetime devam edilir; mevcut senaryo korunur. Denetim AI tarafından yapılır; bütün yazım hatalarının yakalanması garanti edilmez.

Kurulum: ZIP'i yeni klasöre çıkar ve AnkaStudio.exe'yi aç. Ek düğmeye basmak gerekmez. Eski senaryolar da bir sonraki video üretiminde otomatik kontrol edilir. Mevcut videolar denetim tek başına çalıştığında değişmez; düzeltilen anlatım/altyazı video üretiminde uygulanır.

---
# TubeFlow 0.2.10 — Kapak, ses ve YouTube açıklamaları

Kapak tasarımlarında Nur Kapısı • Altın sinema stilini seçip tasarımı kaydet. Lacivert-altın sinematik arka plan uygulamayla birlikte gelir; API olmadan da kullanılabilir. Kendi görselin veya isteğe bağlı AI arka planıyla çeşitlendirebilirsin. Normal video ve Shorts için ayrı oranlarda kapak hazırlanır.

Türkçe özel ad eklerindeki kesme işareti yalnızca seslendirmeye gönderilen metinde birleştirilir: Nur Kapısı'nda → Nur Kapısında. Ekrandaki yazı korunur. Eski sesin değişmesi için videoları yeniden üret. Yeri belirtilmeyen yazım hatası özel olarak düzeltilemedi; yeni senaryo talimatına yazım kontrolü eklendi, bütün hataların bulunması garanti edilmez.

YouTube açıklaması video özeti ve doğal abonelik/beğeni çağrısı kullanır. Araştırma ve stok bağlantılarının tamamı eklenmez; zorunlu lisans atıfları korunur. Yüklenen açıklamaları güncelle düğmesi mevcut yüklemelerde yalnızca açıklamayı değiştirir.

Shorts kapağı da API üzerinden gönderilir. Hesap/API yüklemeyi reddederse video korunur ve tasarım PNG'sini YouTube Studio'daki Dosya yükle alanından seçme bilgisi gösterilir. Her hesapta API kabulü garanti edilmez.

Videoları YouTube'dan kendin sildiysen Yayın ayarları → Silinen YouTube bağlantılarını sıfırla. Bu işlem yerel video ve senaryoyu korur; eski yükleme bağlantılarını temizleyerek yeniden üretim/yükleme için hazırlar. YouTube'daki videoları silmez.

---
# TubeFlow 0.2.9 — Stok arama döngüsü

Görüntü bulunamadığında en fazla 3 tur arama yapılır. Turlar arasında 15 saniye beklenir; her turda siteler aynı alternatif sırasıyla ve yerel havuz tekrar kontrol edilerek denenir. Pixabay kendi hız sınırı beklemesini de uygular. Üretimi durdur beklemeyi iptal eder. API bakiye/erişim/model hataları (401/402/403/404) tekrar edilmez. Üç tur sonunda uygun görüntü yoksa mevcut aşamalar korunur ve anlaşılır hata gösterilir; lisanssız veya boş video otomatik üretilmez.

---
# TubeFlow 0.2.8 — AI bekleme ve Kimi alternatifi

Geçici AI HTTP 429 hatasında Retry-After veya hizmetin sayısal bekleme mesajı okunur. Bekleme en fazla 60 saniye; aynı istek sağlayıcı başına bir kez yeniden denenir. Üretimi durdur düğmesi beklemeyi iptal eder. Belirsiz/günlük/uzun kota veya devam eden hata diğer alternatife geçer. Eksik Shorts düzeltme isteği de aynı bekleme kuralına uyar. Shorts sayısı talimatı güçlendirildi; gerçek yanıt yine doğrulanır, başarı garanti edilmez.

OpenAI ücretsiz öncelikli modda kapalıdır; bu neden artık işlem kayıtlarında görünür. Groq'taki openai/gpt-oss-120b modeli Groq hizmetidir; OpenAI API değildir. HTTP 402 artık API bakiyesi/ön ödeme mesajıyla gösterilir; uygulama hesaba para yüklemez veya abonelik değiştirmez.

Kimi: Bağlantı Merkezi → Kimi / Moonshot → kendi API anahtarını kaydet → Dene. Yönetim ayarları → Yapay zekâ / alternatifler → alt kısımdaki Kimi modellerini getir düğmesiyle hesaba açık modeli seç. Kimi alternatifini etkinleştir; kaydedince mevcut sıraya yoksa Kimi eklenir. Sırasını elle değiştirebilirsin. Varsayılan model kimi-k3; varsayılan Kimi etkinliği kapalıdır. Ücretsiz öncelikli açıkken kullanılmaz. Ücretli API kullanmak istiyorsan sağlayıcı bütçeni kontrol edip ücretsiz öncelikli seçeneğini kapat. Bu genel seçenek OpenAI / ElevenLabs kullanımını da açabilir; seçili sağlayıcı ve ses ayarlarını kontrol et.

Dene ve model listesi yalnızca erişimi kontrol eder; üretim kotasını ve bakiyeyi garanti etmez. Anahtar şifreli kasada tutulur; mesajlara yazma. Kimi web sohbeti ile API erişimini ayrı doğrula. Model listesi ve senaryo üretimi için resmî api.moonshot.ai kullanılır. Yeni veri klasörlerinin varsayılanları güncel Gemini/Groq modelleriyle oluşturulur; mevcut model ayarları korunur.

Kaynaklar: https://console.groq.com/docs/rate-limits • https://platform.kimi.ai/docs/overview • https://platform.kimi.ai/docs/models • https://platform.kimi.ai/docs/guide/use-json-mode-feature-of-kimi-api • https://ai.google.dev/gemini-api/docs/models/gemini-3.8-flash

---
# TubeFlow 0.2.7 — Tasarım ve zaman çizelgesi

İçerik paketinde **Başlık / altyazı editörü** düğmesini aç. Senaryo hazır olmalıdır. Normal ve Shorts ayrı düzenlenir.

- Alt panelde sahne, başlık, altyazı ve müzik şeritleri bulunur. Cetvele tıkla: seçilen anın karesi gösterilir. Yakınlaş / Uzaklaş ile cetveli büyüt.
- Turuncu başlık bloğunu taşı; kenarlarını sürükleyerek başlangıcını ve süresini değiştir. Başlık sekmesinde saniyeleri doğrudan da yazabilirsin. Süre 0, genel başlık süresini kullanır; sahne bitişi aşılmaz.
- Müzik sekmesinden bilgisayardaki dosyayı veya kullanım izni doğrulanmış ses arşivini seç. Başlangıç, bitiş, dosyadan kesme noktası, ses seviyesi, giriş/çıkış solması ve konuşmada otomatik kısma ayarlanır. Müzik şeridi taşınır, sağ kenarıyla bitişi değişir. Ses / görüntü dosyasını editöre sürükleyip bırakabilirsin.
- Başlık ve altyazı için ayrı yazı tipi; boyut, renk, konum, hizalama, dış çizgi, kutu, gölge, neon parıltı, 3D kabartma, italik, alt çizgi, harf aralığı, açı ve opaklık.
- 7 yazı animasyonu, kesme dahil 8 sahne geçişi ve 5 hazır tasarım. Kelime vurgusu yaklaşık zamanlıdır.
- Hareketli önizleme seçilen andan en fazla 6 saniye üretir. Paneldeki GIF sessizdir; **Sesli önizlemeyi aç** aynı pencerenin müzikli MP4 dosyasını oynatır. Kayıtlı anlatım uygunsa kullanılır; henüz ses yoksa yeni TTS çağrısı yapılmaz. Ses üretimi öncesi süreler tahminidir, son üretimde ölçülen süreler kullanılır.

**Düzeni kaydet**, sonra **Videoları üret**. Vazgeç asıl planı değiştirmez. Kaydetme eski plan/çıktıyı yedekler; önceki videolar silinmez. Anlatım değişmemişse ses önbelleği ve stok görüntüleri yeniden kullanılır. Müzik kaynak/lisans ve izin bilgileri gereklidir; kaynak yazısı YouTube açıklamasında korunur. Yerel müzik dosyası kullanan tasarım Windows'ta üretilir; GitHub bulutuna dosya kendiliğinden gönderilmez.

---
# TubeFlow 0.2.6 — Windows ön sürümü

## Başlık / altyazı editörü

İçerik paketindeki Başlık / altyazı editörü düğmesini, senaryo hazır olduktan sonra ve Videoları üret adımından önce aç. Normal video ve Shorts için ayrı düzen; sahneye özel üst başlık ve altyazı metni, yazı tipi, renk, boyut, konum, dış çizgi, arka kutu ve başlık görünme süresi ayarlanır. Düzeni kaydet ile tasarımı kaydet; ardından Videoları üret.

Önizleme gerçek video motorunda bir kare üretir. Kayıtlı sahne görüntüsü varsa onun ilk karesi, yoksa örnek fon gösterilir. Bu işlem TTS veya dış servis kullanmaz. Son altyazı zamanları anlatım sesinden sonra yaklaşık hizalanır; bu ekran ses senkronu veya tüm video oynatma önizlemesi değildir.

Kaydetme önceki plan ve çıktı bilgilerini design-history klasörüne yedekler; mevcut videoları silmez. Yeni düzen üretilene kadar önceki medya onaylanamaz. Anlatım ve ses ayarları değişmemişse mevcut bütün anlatım sesi tekrar kullanılır; mevcut stok görüntüleri ve kapak korunur. Altyazı metnini değiştirmek seslendirmeyi değiştirmez. Vazgeç asıl planı değiştirmez.

## YouTube içerik desteği

Sahne editöründe Başlık / kapak kontrolü düğmesi var. Türkçe tekrar ve uzun metin önerileri ücretsiz yerel kontrolle gösterilir. Yayın öncesi denetimde de görünür; onayı engellemez. Yeni senaryolarda güçlü açılış, doğal sahne bağlantıları, kendi başına anlaşılır Shorts ve ilgili açıklama/etiket yönergeleri kullanılır. Ayrı AI isteği eklenmez. Kaynak incelemesi ve sonraki işler YOUTUBE-AGENT-UYARLAMA.md dosyasında.

## Üretim durumu ve bulut

Dosyalar hazırsa Videoları üret düğmesi neon yeşil ve turuncu tikli görünür. Teknik/editoryal onay ayrı değerlendirilir. GitHub’da üret • Bulut düğmesinin açıklaması üretimin GitHub sunucularında çalıştığını belirtir.

---
# TubeFlow 0.2.5 — Windows ön sürümü

## Canlı işlem kutusu — 0.2.5

İçerik paketlerinde panelin altındaki İşlem kayıtlarını göster düğmesi, o paketin son 250 kaydını açar. Üretim sırasında yeni kayıtlar otomatik görünür; aç / kapat tercihi aynı uygulama oturumunda korunur. İşlem kayıtlarını kapat düğmesi yalnızca kutuyu gizler, üretimi durdurmaz. Kutu salt okunur, kaydırılabilir ve kopyalanabilir.

## Vecteezy bağlantısı — 0.2.3

Bağlantı Merkezi'nde **Vecteezy / stok video** tek karttır: hesap kimliği ve gizli anahtar alt alta girilir; ortak Kaydet / Dene / Paneli aç / Sil düğmeleri kullanılır. Mevcut kayıtlı bilgiler korunur; boş bırakılan satır kayıtlı bilgiyi değiştirmez. İki bilgiyi kendi panelinden kaydet; gizli anahtar şifreli kasada saklanır. Dene düğmesi hesabın arama / indirme kullanımını gösterir, video indirmez. Paylaşılmış anahtarları sağlayıcı panelinden yenile.

Yönetim ayarları → Görüntü / altyazı bölümünde etkinleştirme ve aylık indirme sınırı bulunur. Varsayılan program sınırı 450; hesabın daha düşük sınırı önceliklidir. Her aramadan / indirmeden önce hesap kotası okunur. Kota dolunca veya erişim başarısızsa diğer stok sağlayıcısı / yerel havuz denenir. Vecteezy eski stok sırasına da son alternatif olarak eklenir; kapatma tercihi korunur. Ücretli boyutlandırma istenmez. API aboneliği web Pro aboneliğinden ayrıdır; panelinden Free API planını doğrula. Bu sınır, başka uygulamaların aynı anda tükettiği kota veya sağlayıcının ücret tarifesi üzerinde garanti oluşturmaz.

İndirilen video ve gerekli kaynak yazısı birlikte havuza kaydedilir. Kaynak yazısı video açıklamasında kesilmeden korunur. Dışarıdan video eklerken ve Etiket / lisans ekranında kaynak yazısı girilebilir; kendi indirdiğin Videezy / Dareful gibi kaynakların lisansına göre doldur. Editoryal kullanım videoları aranmaz; ticari izin ve varsa ek lisans sınırlarını yayın öncesi doğrula.

Yeni bağlantı sahte servis yanıtlarıyla sınandı; gerçek hesabında canlı bağlantı henüz doğrulanmadı. Güncel stok kaynak incelemesi için STOK-KAYNAKLARI.md dosyasına bak.

## Stok güncellemesi — 0.2.2

**Stok havuzu → Klasörü tara / listeyi güncelle**, klasöre doğrudan eklenen MP4/MOV/MKV/WEBM dosyalarını arka planda kaydeder. Tek taramada 500 yeni dosya işlenir; aynı içerik tekrar eklenmez, dosyalar yeniden kopyalanmaz. Liste 40'ar video gösterir; toplam sayı ayrıca görünür. İptalde tamamlanan kayıtlar korunur. Dosya adı etiket olur; taranan görüntüler genel fon olarak kullanılabilir. Etiket/kategori ekranından bu tercih değiştirilebilir. Kullanım hakkını yayın öncesinde kontrol et.

Pixabay'ın hesap başlıklarındaki hız sınırı ve sıfırlanma süresi dikkate alınır. 429 beklemesi yeniden açılışta korunur. En fazla bir tekrar yapılır; 90 saniyeden uzun beklemelerde alternatiflere geçilir. Yerel alternatif mevcutsa beklemek yerine havuza dönülür. Arama sonuçları 24 saat önbellekte tutulur.

Yönetim ayarları → Stok havuzu bölümünde servis hatası sonrası havuza dönüş ve sade genel fon seçenekleri bulunur. 64 Türkçe–İngilizce fon başlığı görüntülenebilir. Katalog her seferinde topluca sorgulanmaz; sahne başına en fazla 12 farklı kısa arama kullanılır. Eski virgüllü stok ifadeleri noktalı virgülle düzeltilir; Türkçe fon adları API aramasında İngilizceye çevrilir. Pixabay sorgusu 100 karakteri aşmaz.

Türkçe YouTube kanalları için normal video ve Shorts'u tek konu paketinden yöneten Windows masaüstü uygulaması. Bu paket bir ilk çalışan sürümdür; bütün hizmetlerin canlı doğrulandığı tamamlanmış ticari ürün değildir.

## Başlatma
Windows klasöründeki **AnkaStudio.exe** dosyasına çift tıkla. .NET çalışma ortamı pakete dahildir; ayrıca kurulum gerekmez. Windows x64 içindir. Dosyaları birlikte tut; yalnızca exe'yi ayırma. İmzasız bir ön sürüm olduğu için Windows dosyanın yayımlayıcısını doğrulamayabilir.

## İlk kullanım
1. **Stüdyo → Örnek paketi oluştur** ile anahtarsız deneme yap. **İçerik paketleri → Videoları üret** gerçek MP4, Türkçe altyazı ve kapak üretir. Bu örnek sessiz grafik testidir; gerçek araştırma, seslendirme veya stok görüntü içermez ve YouTube'a yüklenemez.
2. **Bağlantı Merkezi** ekranından bir metin sağlayıcısının anahtarını ekle: Gemini, Groq veya OpenRouter. Yerel Ollama varsa anahtar gerekmez; model önceden indirilmiş olmalıdır.
3. Tavily araştırma anahtarını ve Pexels / Pixabay stok anahtarlarından en az birini kaydet. Tavily olmadan Yönetim ayarlarındaki en az iki konuya uygun HTTPS kaynak adresi okunabilir. Genel arama hizmeti kesilirse kayıtlı kaynak adresleri denenir.
4. **Yönetim ayarları → Ses / müzik** içinde Türkçe sesi seç. Ücretsiz yerel ses için Windows'ta Türkçe SAPI konuşma sesi bulunmalı. Bulunmuyorsa Windows dil/konuşma bileşeni kurulumunu kontrol et; her OneCore sesi SAPI üzerinden görünmeyebilir. Alternatif olarak Piper programını ve Türkçe ONNX modelini seçebilirsin; modelin .onnx.json dosyası da bulunmalı ve kullanım lisansı doğrulanmalı. Piper programı/modeli bu pakete dahil değildir ve canlı ses üretimi test edilmemiştir. ElevenLabs isteğe bağlıdır ve ücretsiz öncelik modunda kapalıdır. Shorts için ayrı sağlayıcı, ses kimliği, hız ve müzik seviyesi açılabilir.
5. Yeni paket oluştur: konu, talimat, normal video süresi, Shorts sayısı/süresi, bağlantılı veya bağımsız kısa içerik ve müzik.
   Toplu program için **İçerik paketleri → Konu programı ekle**: her satıra bir konu veya `2030-01-01 20:00 | Konu | Talimat` biçiminde tarihli bir satır yapıştır. En fazla 100 konu tek seferde eklenir; program satırların tamamını doğrulamadan hiçbirini kaydetmez.
6. **Araştır + yaz → Sahne editörü → Videoları üret → İncele / onayla** sırasıyla ilerle.
7. Google Cloud'da YouTube Data API ve Desktop OAuth istemcisi oluştur. İstemci kimliği/gizli anahtarını ekle, **YouTube kanalını bağla** ile resmî Google izin ekranını tamamla. API proje denetimi tamamlanmadıysa yüklenen videolar özel görünürlükle sınırlanabilir.
8. **Yayın ayarları** üzerinden özel yükle veya ileri tarihe planla. Saatler Türkiye saati (UTC+3). Shorts'ların ana videodan sonra hangi aralıkla çıkacağını ayarla.

## Puzzle bağlantıları ve güvenlik
Anahtarları koda yazman gerekmez. Uygulama `PIXABAY_API_KEY`, `GEMINI_API_KEY` gibi doğru hizmet isimleriyle anahtarları otomatik eşleştirir. Desktop anahtarları Windows DPAPI ile şifrelenir. Windows kasası kullanılamazsa en az 10 karakterli bir parola ile AES-256-GCM kasa oluşturulabilir; parola saklanmaz. Parola kaybolursa anahtarları yeniden girmek gerekir. GitHub işçisinde anahtarlar ortam değişkenlerinden alınır.

Kaynak gönderimi, videoları veya yerel anahtar dosyalarını kapsamaz. GitHub Secret aktarımı ayrı bir kullanıcı işlemi olarak yapılır; değer GitHub açık anahtarıyla sealed-box yöntemiyle şifrelenir. API anahtarlarını sohbete, kaynak dosyalarına veya ekran görüntülerine yapıştırma.

## Ücretsiz öncelik ve alternatifler
Varsayılan sıra: **Gemini → Groq → OpenRouter → Ollama**. Eksik anahtarlar atlanır; kota, erişim, bağlantı, zaman aşımı ve geçersiz JSON / senaryo yapısında alternatif denenir. OpenRouter ücretsiz modda yalnızca `openrouter/free` veya `:free` modelleri kabul eder. OpenAI ve ElevenLabs bu modda çalışmaz.

Gemini, Groq, Tavily gibi hizmetlerin ücretsiz kullanım sınırlarını ve hesabın faturalama durumunu uygulama garanti edemez. Sağlayıcı panelinde ücretsiz hesap / bütçe sınırı kullan. Ücretsiz kotalar sınırlı ve değişkendir. Tüm alternatifler durursa sahte içerik üretilmez; iş bekler. Model kimlikleri yönetim ekranında değiştirilebilir; hesapta erişilebilir modeller kullanılmalıdır. Varsayılanlar her hesapta aynı erişimi garanti etmez.

## GitHub'ı uygulamanın içinden yönetme
1. Bağlantı Merkezi'ne erişim token'ını kaydet. Seçilen depo için Contents, Actions, Secrets ve workflow yazma izinleri gerekir; token türüne göre izin adları değişebilir.
2. **GitHub kontrolü → Hesabı bağla / depolar** üzerinden depo seç veya yeni özel depo oluştur.
3. **Kaynak + iş akışlarını gönder** uygulama kaynaklarını ve iki iş akışını bir commit olarak gönderir. Aynı adlı kaynak dosyalarını günceller; dalı zorla değiştirmez.
4. Secret bölümünde kullanılacak anahtarları tek tek, şifreli olarak GitHub'a aktar.
5. İçerik paketini seçip GitHub'da üret. Çalışmaları yenile; tamamlanmış çıktıyı uygulamaya al. İndirilen paket tekrar kullanıcı denetimine döner.

GitHub üretimi ayrı bir uzak iş olarak çalışır. Canlı iki yönlü bulut senkronizasyonu / sunucu servisi bu sürümde yoktur. GitHub-hosted Windows ortamında Türkçe yerel ses bulunmayabilir; yerel Windows'ta ücretsiz üretim bu yüzden daha uygun başlangıçtır. Uzak makinede ses bulunmazsa işlem açık hata verir, ücretli hizmete otomatik geçmez. GitHub Actions dakika, depolama ve süre sınırları vardır. Uzak iş bu sürümde YouTube'a otomatik yayın yapmaz; çıktı Windows'a alınır, kontrol edilir ve onaylanır.

## Denetim ve otonomi
- Normal video 1920×1080; Shorts 1080×1920. Süre gerçek sesten ölçülür.
- SRT ve gerçek görüntü boyutuna göre ASS altyazı; altyazı video piksellerine işlenir.
- Kaynaklar, görüntü sahibi, lisans adresi, senaryo ve yayın metaverisi kaydedilir.
- Müzik kendi lisanslı dosyan üzerinden kullanılır; konuşmada otomatik kısılır. Müzik türü mevcut dosyanın sınıflandırmasıdır; otomatik müzik arama/üretimi değildir.
- Kalite kapısı: video/ses akışları, çözünürlük, süre toleransı, altyazı, medya kayıtları. Görsel uygunluk, bilgi doğruluğu, telif ve okunabilirlik insan tarafından ayrıca kontrol edilmelidir.
- Demo, uyarılı veya kalite kapısından geçmemiş paket onaylanamaz. İçerik değişince onay kalkar. Yükleme öncesi video SHA256 imzası tekrar doğrulanır.
- Otonomi sekmesinden taslak araştırma, senaryo üretimi ve kullanıcının onayladığı paketlerin yüklenmesi ayrı ayrı açılır. Yerel çalışma için uygulama açık olmalıdır.
- Üretim tek kuyrukta yürür. Durdurma, kesinti sonrası devam ve ses/video önbelleği vardır. Başarısız iş sonsuz tekrar yapmaz.
- YouTube yüklemesi yeniden başlatılabilir oturum ve parça aktarımı kullanır; tamamlanmış video tekrar yüklenmez.

## Bu sürümdeki sınırlar
Normal video ve Shorts tam kurgu editörü seviyesinde değildir: sahne metni, stok sorgusu, yerel medya, kapak metni ve açıklama düzenlenebilir; gelişmiş geçişler, çok kanallı zaman çizelgesi ve özel grafik animasyonları sonraki sürümlerdir. Kapaklar şablonla oluşturulur. Shorts özel küçük resim seçimi bu uygulamadan yapılmaz. Analytics, otomatik müzik keşfi, kurulum imzası, güncelleme servisi, çoklu kanal ve sunucu senkronizasyonu henüz geliştirilmemiştir.

Canlı API anahtarları ve kanal sağlanmadığı için gerçek araştırma/ses/YouTube/GitHub hesap işlemleri canlı uçtan uca doğrulanmadı. Test raporunda başarılı, başarısız ve test ortamında engellenmiş kontroller ayrı yazılır. Windows DPAPI bu geliştirme ortamında doğrulanamadı; parola kasası şifreleme/çözme testi geçti.

## Veriler ve geliştirme
Normal kullanım verileri `%LOCALAPPDATA%\AnkaStudio` içinde tutulur; **Çalışma klasörü** düğmesinden açılır. Anahtar kasası başka hesaba taşınacaksa anahtarları yeniden gir. Kaynak kod `src/` altında. Geliştirme için .NET 10 SDK gerekir:

```powershell
dotnet publish src/AnkaStudio.csproj -c Release -r win-x64 --self-contained true -o Windows
```

Video motoru `Windows/tools/ffmpeg.exe` ve `ffprobe.exe` içindedir. FFmpeg dağıtımı ve lisansı `tools/` altında; dağıtım kaynağı https://www.gyan.dev/ffmpeg/builds/ . Sodium.Core MIT, libsodium ISC lisanslarıyla kullanılır. Ayrıntılar THIRD-PARTY-NOTICES.md dosyasında.

**Android kararı:** Önce Windows, GitHub ve normal video–Shorts akışının tüm işleri tamamlanacak. Android'e daha sonra geçilecek; ROADMAP.md'de kayıtlıdır.

## Google Cloud TTS güncellemesi — 7 Ekim 2026
Bağlantılar → Google Cloud TTS: GOOGLE_TTS_API_KEY anahtarını kaydet ve Dene.
Ayarlar → Ses / müzik: sağlayıcı Google Cloud TTS; Türkçe seslerini getir, seçili sesi dinle ve ayarları kaydet. Aynı seçenek Shorts sağlayıcısında da bulunur; Google ses kimliği ortaktır, hız farklı olabilir.
Varsayılan aylık yerel sınır 900.000 karakterdir. Sayaç yalnızca bu veri klasöründeki istekleri kapsar; diğer uygulamalar, cihazlar ve GitHub çalışmaları dahil değildir. Sınır, ücretsiz kullanım garantisi değildir. Google Cloud kullanımını ayrıca izle. Kesilen/hatalı istekler ihtiyatlı olarak sayaçta kalır. API key ile canlı erişim bu sürümde kullanıcı hesabında ayrıca doğrulanmalıdır.
Google sesleri LINEAR16 WAV alınır, konuşma hızı yerelde uygulanır. Altyazılar ölçülen parça sürelerine dayanır. Hata veya sınıra ulaşmada, ayar açıksa kurulu Windows/Piper Türkçe sesi denenir; yerel ses yoksa üretim durur.
Güncelleme: programı kapat, yeni ZIP'i ayrı klasöre çıkar ve yeni AnkaStudio.exe'yi aynı Windows hesabında çalıştır. Varsayılan %LOCALAPPDATA%\AnkaStudio kayıtları aynen kullanılır; anahtarları yeniden girmek gerekmez. Veri klasörünü silme; özel --data konumu kullanıyorsan aynı konumu koru.
GitHub için Kaynak + iş akışlarını gönder ile yeni kaynakları gönder; GOOGLE_TTS_API_KEY'i GitHub Secrets'a ayrıca aktar.

Stok arama güncellemesi: sahne stok aramalarında alternatifleri noktalı virgülle ayır: sunrise; morning; nature. Her ifade için kayıtlı sağlayıcılar sırayla denenir. Boş ifadeler ve tekrarlar atlanır; en fazla 12 ifade. API erişim/kota hatası olan sağlayıcı aynı sahnede tekrar denenmez. Bulunan görüntünün kaynak kaydı korunur.

## Kalıcı stok havuzu
Stok havuzu menüsünden videoları önizle, video ekle, etiket/kategori düzenle, havuzdan sil. Kendi dosyalarında kaynak ve lisans açıklaması ile kullanım hakkı onayı gereklidir. İndirilen görüntüler otomatik etiketlenir ve basit anahtar kelimelerle kategorilenir. Üretim önce etiket eşleşmesi olan havuz videolarını kullanır; sonra internet sağlayıcılarını arar. Anlamsal yapay zekâ görüntü incelemesi yapılmaz.
Yönetim ayarları → Stok havuzu: öncelik, otomatik kayıt ve tekrar kullanım bekleme süresi. Varsayılan 24 saat; 0 seçilirse sonraki paketlerde yeniden kullanılabilir. Aynı iş içindeki tekrarlar engellenir. Havuzda metadata ve içerik hash'i korunur; proje dosyaları ayrı kopyadır. Eski paketlerde indirilmiş görüntüleri Paketlerden havuza al ile aktar. Havuzun GitHub makinelerine eşitlenmesi bu sürümde yoktur.
Pixabay aramaları en az 1,2 saniye aralıkla gönderilir; 429 yanıtında Retry-After / X-RateLimit-Reset süresi beklenir ve en fazla iki tekrar yapılır. Bekleme iptal edilebilir, günlükte görünür. 24 saat arama önbelleği korunur. Başka uygulamaların aynı anahtar kullanımını bu uygulama ölçemez.

Toplu video ekleme: en fazla 20 dosya/seçim. Kontrol, hash ve kopyalama arka planda yürür; ilerleme ve iptal vardır. Her dosya kontrolünde 45 saniyelik süre sınırı uygulanır; sorunlu dosya atlanır, rapor pool-import-errors.txt'ye yazılır. Tamamlanan kayıtlar korunur. Liste 40 öğelik sayfalarla gösterilir. Kopyalama disk hızına bağlıdır; kaynak dosyalar silinmez.

## Bağlantı yardımı ve açılış güvenliği
Bağlantı kartlarındaki ⓘ düğmesi her hizmet için ayrı kurulum, anahtar girişi, test, izin ve hata açıklaması açar. YouTube ve anahtarsız Ollama/Windows/Piper için ayrı rehberler vardır.
Normal ilk açılışta en az 10 karakterlik uygulama şifresi oluşturulur. Şifre PBKDF2-SHA256, 310.000 iterasyon, rastgele salt ile doğrulanır; şifrenin kendisi saklanmaz. Beş yanlış girişte pencere kapanır. Yönetim ayarları → Güvenlik şifre değişimi için mevcut şifreyi sorar. Bu açılış şifresi API kasası şifresinden ayrıdır. Güvenilir olmayan kişilerle aynı Windows hesabını paylaşma: yerel kilit, dosyalara erişebilen bir Windows kullanıcısına karşı güvenlik sınırı değildir. Şifre kurtarma/e-posta doğrulama hizmeti bu sürümde yoktur. Şifreyi güvenli sakla.
Kilitli yerel veri klasöründe worker/screenshot/ui-test/media-test komutları engellenir. GitHub işçisi ayrı geçici veri klasöründe çalışır; yerel kilit dosyası kaynak paketine girmez.
Google TTS: yerel sayaç hesap genelindeki kotayı birebir göstermez. Ses ayarlarında Cloud kullanım ve fatura paneline bağlantı eklenmiştir. Hesap rapor bağlantısı için aşağıdaki BigQuery kurulumu gereklidir. Google Cloud fatura verileri de gecikmeli gelebilir.
Uygulama henüz dijital imzalı değildir ve bağımsız güvenlik denetiminden geçmemiştir. Kaynak kod incelemeye açıktır. Anahtarlar kullanıcı veri klasöründeki şifreli kasada tutulur; program güncellemesi kasayı silmez.

## Google TTS hesap takibi ve isteğe bağlı Cloud API yönetimi
Yönetim ayarları → Google TTS kota. Hesap raporu zorunlu koruması varsayılan açıktır; bağlantı kurulmadan Google TTS üretimi durur veya yerel ses alternatifi denenir. Kullanıcı bu korumayı değiştirebilir; yerel sayacın hesap genelini kapsamadığı ekranda belirtilir.
Cloud Billing → Billing export → Standard usage cost BigQuery export etkinleştirilmelidir. Veriler için bir project.dataset.table ve veri konumu (EU/US/bölge) girilir. Ayrı hizmet hesabına sorgu projesinde BigQuery Job User, ilgili dataset üzerinde BigQuery Data Viewer verilir; JSON kimliği programdan şifreli kasaya aktarılır. Sorgu başına tarama üst sınırı 100 MB; BigQuery depolama/sorgu hizmeti ücret oluşturabilir.
Rapor seçilen faturalandırma tablosundaki bütün projeler için bu ayın TTS kullanımını SKU, birim, kredi sonrası maliyet ve para birimiyle gösterir. Google Cloud verisi gecikmelidir; e-posta/paneldeki anlık kalan ücretsiz kota garantisi değildir. İlk aktarım günler sürebilir. Boş/eski rapor, bilinmeyen birim veya ücretli kullanımda koruma engeller. Yerel kullanım ayrıca ihtiyatlı biçimde rapora eklenir; rapordaki aynı kullanım iki kez sayılabilir. Bu, erken durmaya yol açan korumadır. İlk gerçek hesap sorgusu ve birim eşleşmesi ayrıca doğrulanmalıdır.
Rapor 1 saatten eskiyse üretimde otomatik yenilenir. Üretim yapılmıyorken sürekli sorgulama yapılmaz. Yerel limit, başka programların rapora henüz ulaşmamış kullanımını bilemez. Sıfır ücret garantisi yoktur.
Google TTS'yi durdur düğmesi bu uygulamadaki yeni istekleri engeller ve çalışan işi iptal eder. Google'a gönderilmiş isteği veya başka uygulamaları geri alamaz.
Cloud proje API yönetimi isteğe bağlı ve varsayılan kapalıdır. Kontrol için ayrı GOOGLE_CONTROL_JSON hizmet hesabı kimliği kullanılır. Hedef projede Service Usage Admin veya yalnızca serviceusage.services.enable/disable izinli özel rol verilmelidir. Service Usage API etkin olmalı. Cloud proje NUMARASI açıkça girilir; kapatma o projenin bütün TTS kullanıcılarını etkiler. Hesap raporu kimliğine yönetim yetkisi vermek zorunlu değildir. Açma/kapatma düğmeleri Google işlemi tamamlanmadan başarı göstermez. Sınırda otomatik kapatma yalnızca hem yönetim hem otomatik kapatma tercihleri açıkken çalışır; kapatma doğrulanamazsa yerel engel korunur, hata gösterilir.
Google Cloud yapılandırması harici hesapta kullanıcı tarafından yapılır. Canlı Cloud Billing ve Service Usage işlemleri geliştirici testinde yapılmamıştır; sahte yanıtlar ile akış doğrulanmıştır.

Arayüz güncellemesi: Cam yüzeyli kabartmalı tuşlar ve yazılar eklendi. Yönetim ayarları > Arayüz / ses bölümünde klik sesi açılıp kapatılabilir ve seviyesi değiştirilebilir. Ses yalnızca uygulamada çalar; üretilen videoya eklenmez. Güncelleme kullanıcı verilerinin saklandığı LocalAppData/AnkaStudio klasörünü değiştirmez.

Sohbet yardımcısı: Sağ alt Sohbet tuşu paneli açar. Kayıtlı yapay zekâ sırası ve ücretsiz öncelik ayarı kullanılır. Ctrl+Enter gönderir; Durdur isteği iptal eder. Sohbet yalnızca oturumda tutulur, mesajlar seçili sağlayıcıya gönderilir. Anahtar veya parola yazmayın. Yardımcı dosyaları okuyamaz, ayar değiştiremez veya yayın yapamaz.

Kapak tasarımları: Sekiz örnek tarz, kanal adı/logo/yazı tipi/renk/konum, yerel PNG/JPEG arka plan veya hazır kapak. Varsayılan otomatik tarz konu anahtar kelimelerine göre belirlenir; arka plan yerel tasarım şablonuyla çizilir. Videodan kare alınmaz. Haricî yapay zekâ görsel üretimi bağlı değildir. Yeni üretimlerde uygulanır; eski çıktıyı değiştirmez. Normal video kapağı mevcut YouTube yükleme akışında gönderilir; Shorts PNG'si otomatik yüklenmez.


Kapak görsel alternatifleri: Gemini, OpenAI (sırası değiştirilebilir), yerel şablon, sorunlu bileşen durumunda sade yerel tasarım. Görsel API'si ve ücretli kullanım izni varsayılan kapalıdır; ücretsiz öncelik açıkken iki servis atlanır. Kayıtlı GEMINI_API_KEY / OPENAI_API_KEY kullanılır. Kota/erişim/yanıt/süre hatalarında sıradaki sağlayıcıya geçilir. Aylık UTC istek sınırı başarısız ve iptal istekleri de sayar; parasal bütçe değildir. Servis hesap erişimi ve model kullanılabilirliği ayrıca doğrulanmalıdır. Canlı görsel API testi kullanıcı hesabında henüz yapılmadı.

## Ses akışı ve denetim güncellemesi
Ses akışı iyileştirmesi için pakette Videoları üret adımını yeniden çalıştırın. Altyazı hizalaması yaklaşık olduğundan çıktıyı dinleyip kontrol edin. İncele / onayla ekranında her uyarı için İncele / çözüm kaydet tuşunu kullanın; inceleme notu mevcut videoları silmez. Senaryo değişikliği yeniden üretim gerektirir. Güncelleme paketi kullanıcı ayarlarını ve anahtar kasasını içermez; uygulamayı kapatarak program dosyalarını güncelleyin.

Projeyi tamamen sil: İçerik paketleri ekranındaki kırmızı tuş, Emin misiniz? onayından sonra seçili paketin yerel proje klasörünü ve paket kaydını kalıcı siler. Çalışan iş varken kullanılamaz. Ortak arşivler, hesaplar, YouTube ve GitHub içeriği korunur.

Kanal güvenliği: Güncellemeden sonra Bağlantı Merkezi → YouTube kanalını doğrula ile hedef kimliğini kaydedin. Eski paketleri yüklemeden önce yeniden onaylayın. API anahtarları korunur. GitHub Google TTS üretimi ortak hesap kota sistemi hazır olana kadar engellenir; Windows üretimi kullanılabilir.

0.4.18 sahne kalitesi: Tasarım → Görseller → AI görsel kalitesi • yerel motor. Genel ayar / Hızlı / Dengeli / Ultra sinematik seçilebilir. Asistan da kalite seçebilir. Değişen kalite yerel AI görsellerinin sonraki üretimde yenilenmesini sağlar; kullanıcı dosyaları korunur.
