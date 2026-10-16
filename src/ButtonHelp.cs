namespace AnkaStudio;

public static class ButtonHelp
{
    static readonly HashSet<string> explainedActions=new(StringComparer.OrdinalIgnoreCase) {
        "Hatayı Düzelt ve Devam Et","Araştır + yaz","Hazır metni kullan","1. Hazır metni kullan","5. Videoları üret","6. İncele / onayla","İçeriği ve medyayı onayla",
        "Hepsini yayına yolla","Şimdi yayınla","YouTube'a özel yükle","Yüklenen videoları şimdi yayına al","YouTube'da yayını planla",
        "YouTube yayın planını kaldır","Silinen YouTube bağlantılarını sıfırla","Yüklenen açıklamaları güncelle","Takvimi kaydet",
        "Tarih seçerek yayınla","Sırayı duraklat","Sıradan çıkar","Günlük sınırı kaydet","Yeni sürüm / kopya",
        "Proje durumlarını kontrol et / güncelle","Video yükleme durum kontrol","Seçili sahneye havuzdan video seç","Farklı kaynaktan video seç",
        "Tüm sahne başlıklarını şimdi ayarla","Düzeni diğer videolara uygula","Varsayılan düzen","Shorts için normal video sesini kullan",
        "Görüntü haklarını kontrol ettim / onayla","Havuz videolarının kullanım haklarını onayla","Etiket / lisans","Bilgi / lisans",
        "İncele / çözüm kaydet","Çözümü kaydet • videoları koru","Üretimde kullan","Paketlerden havuza al",
        "Açılış kartı olarak seç","Kapanış kartı olarak seç","Bu logoyu videolarda kullan","Shorts tasarım kapağını aç",
        "Yerel motoru yükle","Seçili modeli yükle","Seçili modeli dene","Ana model yap","Servisi başlat","Ollama bağlantısını dene",
        "YouTube kanalını bağla","YouTube kanalını doğrula","OAuth bilgilerini kaydet","Google hesap kullanımını güncelle",
        "Cloud projesinde TTS API AÇ","Cloud projesinde TTS API KAPAT","Google TTS'yi durdur",
        "Salt okunur hizmet hesabı JSON ekle","İsteğe bağlı API yönetim JSON'u ekle","Piper programını seç","Piper ses modelini seç","Piper programı seç","Piper modeli seç",
        "Video motoru seç","Yedek klasörü seç","Program kaynaklarını GitHub’a gönder","Kaynak depo ayarlarını kaydet",
        "Stok videoları YouTube’a planlı yükle","Programı paketlere dönüştür","Açılış şifresini değiştir"
    };
    public static bool NeedsInfo(string text) {
        var name=text.Trim();
        if(name.Contains("güncelle",StringComparison.CurrentCultureIgnoreCase)||name.Contains("yenile",StringComparison.CurrentCultureIgnoreCase)||name=="Shorts tasarım kapağını aç")return false;
        return explainedActions.Contains(name);
    }
    static readonly Dictionary<string,string> descriptions=Build();
    static Dictionary<string,string> Build()
    {
        var map=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        void Add(string names,string explanation){foreach(var name in names.Split('|'))map[name]=explanation;}
        Add("Hatayı Düzelt ve Devam Et","Geçici hata sonrası kayıtlı aşamadan yeniden dener. NVIDIA ve Gemini alternatiflerini kullanır; tamamlanmış önbellekleri korur. Anahtar, kota, eksik dosya veya lisans sorununu kendiliğinden gideremez; kalan sorunu gösterir. Yayın işlemini başlatmaz.");
        Add("Ana ekran|Stüdyo","Kanal bağlantısını, çalışma durumunu ve son projeleri gösteren ana ekranı açar.");
        Add("Video hazırla","Bir projeyi seçerek içerik, sahneler, ses ve tasarım, üretim ve yayın adımlarını düzenlemeni sağlar.");
        Add("Yayın sırası|Üretim ve yayın sırası|Üretim ve yayın planı","Video ekleme, tarih/saat belirleme ve sırayla üretip YouTube'a gönderme ekranını açar.");
        Add("Videolarım|Videolarımı aç","Projeleri durumlarına göre listeler. Projeyi yeniden düzenleyebilir veya hazır videoları izleyebilirsin.");
        Add("Ayarlar|Varsayılanlar","Yeni üretimlerin varsayılan sağlayıcı, ses, medya, altyazı, güvenlik ve otomatik çalışma ayarlarını açar.");
        Add("Hesaplar|Bağlantı Merkezi","YouTube, yapay zekâ ve ses sağlayıcılarının bağlantı ve kimlik bilgilerini yönetir.");
        Add("Medya arşivleri","Ortak stok video, ses/müzik ve kapak arşivlerine geçiş sağlar.");
        Add("Gelişmiş|Araçlar","İşlem kayıtları, çalışma klasörü ve program kaynakları gibi yönetim araçlarını açar.");
        Add("Rehber","Programın kapsamlı kullanım rehberini açar. Konu listesinden bölüm seçebilir, arama kutusuyla rehberde arayabilirsin.");
        Add("Çalışma klasörü|Çalışma klasörünü aç","Programın proje ve ayar dosyalarını sakladığı klasörü Windows'ta açar. Bu klasördeki dosyaların silinmesi verilerini etkileyebilir.");
        Add("İçerik|1. İçerik","Konu, içerik talimatı veya hazır metinle senaryoyu hazırlama adımını gösterir.");
        Add("Sahneler|2. Sahneler|2. Sahne editörü","Senaryo sahnelerini, anlatımı, stok aramalarını ve sahneye bağlı görüntü dosyalarını düzenler.");
        Add("Ses ve tasarım|3. Ses ve tasarım","Kapak ve başlık/altyazı editörlerine geçiş sağlar. Müzik, görünüm ve video kartlarını ilgili editörden düzenle.");
        Add("Üret ve incele|4. Üret ve incele","Videoyu üretme, izleme ve yayın öncesi onaylama işlemlerini gösterir.");
        Add("Yayın|5. Yayın|7. Yayın ayarları","Hazır videoları YouTube'a yükleme, hemen yayınlama veya ileri tarih için planlama seçeneklerini açar.");
        Add("Araştır + yaz|Araştır + yaz|Hazır metni kullan|1. Hazır metni kullan","İçeriği ve sahne planını hazırlar. Hazır metin seçilmişse o metin kullanılır; aksi durumda seçilen yapay zekâ sağlayıcısından üretim istenir.");
        Add("3. Kapak editörü","Normal video ve Shorts küçük resmini, videoya gömülen açılış/kapanış kartlarını ve kanal logosunu düzenler.");
        Add("4. Başlık / altyazı editörü","Sahne başlıkları, altyazı, yazı görünümü, animasyon, geçişler ve müziği düzenleyen zaman çizelgesini açar.");
        Add("5. Videoları üret","Kaydedilmiş senaryo, ses, görüntü ve tasarım ayarlarıyla yerel video çıktıları üretir. Değişen düzenin MP4'e yansıması için yeniden üretim gerekir.");
        Add("6. İncele / onayla|İçeriği ve medyayı onayla","Videoların teknik, kaynak ve kullanım hakkı kontrollerini inceleyip yayın onayı vermeni sağlar. Onaylamak tek başına yükleme başlatmaz.");
        Add("8. Dosyalar","Projenin üretilen video, kapak, ses ve altyazı dosyalarına ulaşmanı sağlar.");
        Add("+ Yeni video|+ Yeni paket|Paket ayarları|Proje ayarları • konu / metin / süre","Konu, içerik talimatı, hazır metin, video türü ve süre gibi proje bilgilerini girmeni veya düzenlemeni sağlar.");
        Add("Yeni sürüm / kopya","Mevcut projeden yeni bir sürüm veya kopya hazırlar; farklı düzenlemeleri ayrı çıktı olarak çalışabilirsin.");
        Add("Örnek paket|Örnek paketi oluştur","Arayüzü ve üretim akışını denemek için örnek proje oluşturur. Örnek içerik gerçek yayın içeriği değildir.");
        Add("Sil|⚠ Projeyi tamamen sil","Seçilen yerel projeyi ve dosyalarını onaydan sonra siler. YouTube'da yüklenmiş videoyu silmez.");
        Add("Düzenle|Ayrıntıları düzenle|Paketi incele","Seçilen projenin ayrıntılarını düzenlemek veya hazırlık adımlarını incelemek için açar.");
        Add("Proje durumlarını kontrol et / güncelle|Video yükleme durum kontrol","Yerel projelerin ve bağlı YouTube yüklemelerinin durumunu kontrol eder; sonucu uygun proje grubuna taşır. İnternet bağlantısı gerekebilir.");
        Add("+ Bir video ayarla ve ekle|+ Video ekle","Tek bir videonun konusu, yayın zamanı, sesi, müziği, kapağı ve diğer ayrıntılarını seçip sıraya ekleme formunu açar.");
        Add("Hepsini yayına yolla","Sıradaki yetkilendirilmiş videoları ayarlarına göre sırayla üretir, kontrol eder ve doğrudan YouTube'a planlı yükler. Yüklemeler tamamlanana kadar program açık kalmalıdır.");
        Add("Sırayı duraklat","Sıranın ilerlemesini durdurur ve sıradaki işler için otomatik çalışma yetkisini kaldırır. Tamamlanan dosyaları korur.");
        Add("Sıradan çıkar","Videoyu üretim/yayın sırasından çıkarır. Yerel projeyi tamamen silme işlemi değildir.");
        Add("Günlük sınırı kaydet","Günde üretilecek proje sınırını kaydeder. Otomatik sıra bu sınıra uyar.");
        Add("Tarih / saat|Tarih / saat ayarla|Tarih seçerek yayınla","Yayın tarihi ve saatini seçme ekranını açar. Saatler Türkiye saatidir; planlama için gelecekte bir zaman seç.");
        Add("Takvimi kaydet|Tarihi kaydet|Yayın tarihini ve saatini kaydet","Seçilen yayın zamanını programda kaydeder. Tek başına YouTube'a yükleme veya herkese açık yayın başlatmaz.");
        Add("Şimdi yayınla","Onaylı çıktıları doğrudan YouTube'a yükleyip herkese açık yayınlama işlemini başlatır. Yayından önce onay istenir.");
        Add("YouTube'a özel yükle","Onaylanan videoları YouTube'a özel görünürlükte yükler. Daha sonra hemen yayınlayabilir veya tarih belirleyebilirsin.");
        Add("Yüklenen videoları şimdi yayına al","Bu paketin yüklenmiş videolarını onaydan sonra hemen herkese açık yapar. Güncel çıktılar onaylı ve tüm videolar yüklenmiş olmalıdır.");
        Add("YouTube'da yayını planla","Eksik videoları yükler veya mevcut yüklemelerin yayın tarihini ayarlar. En az 10 dakika ileride bir tarih gerekir. Shorts seçilen saat aralığıyla planlanır.");
        Add("YouTube yayın planını kaldır","Yayın tarihini kaldırır ve ilgili YouTube videolarını özel görünürlüğe alır. Videoyu silmez; yayındaki videoları da gizleyebilir.");
        Add("Silinen YouTube bağlantılarını sıfırla","YouTube'dan zaten sildiğin videoların programdaki eski yükleme bağlantılarını temizler. Yeniden yüklemeye izin verir; YouTube videosunu silmez.");
        Add("Yüklenen açıklamaları güncelle","YouTube'a yüklenen videoların açıklamalarını günceller. Video dosyasını yeniden üretmez.");
        Add("Hazır metin editörünü aç","Kendi metnini yazma, yapıştırma veya TXT dosyasından yükleme ekranını açar. Normal ve Shorts metinlerini ayrı gir.");
        Add("TXT dosyasından yükle","Seçilen TXT dosyasını metin alanına alır. Kaydetmeden önce içeriği kontrol et; mevcut taslak metnin yerini alır.");
        Add("Kopyala","Metin alanının içeriğini panoya kopyalar.");Add("Yapıştır","Panodaki metni ilgili metin alanına yerleştirir.");
        Add("Seçili sahneye havuzdan video seç","Seçili sahne için programın stok video havuzundan dosya seçme penceresini açar.");
        Add("Farklı kaynaktan video seç|+ Görüntü ekle","Bilgisayarındaki başka bir klasörden görüntü/video seçmeni sağlar. Seçilen dosyanın kullanım hakkını ayrıca doğrula.");
        Add("Araştırma ve kaynakları göster","Senaryonun araştırma notlarını ve kaynak bağlantılarını açar; doğruluk denetimi için kullan.");
        Add("Başlık / kapak kontrolü","Başlık ve kapak metnindeki uzunluk veya tekrar sorunları için öneriler gösterir. İzlenme garantisi değildir.");
        Add("Tüm sahne başlıklarını şimdi ayarla","Yapay zekâdan sahne başlıklarını düzenlemesini ister. Kullandığın modelin bağlantısı ve izinleri gerekir; taslağı üretimden önce kontrol et.");
        Add("Başlık üretimini durdur","Devam eden yapay zekâ başlık düzenlemesini iptal eder.");
        Add("Düzeni diğer videolara uygula","Seçilen videonun düzen ayarlarını paketteki diğer videolara uygular. Hedef videoları da kontrol et.");
        Add("Varsayılan düzen|Hazır tasarımı uygula","Seçilen hazır veya varsayılan görünüm ayarlarını düzenleme taslağına uygular. Kaydetmeden önce önizlemeyi kontrol et.");
        Add("+ Müzik ekle","Müzik arşivini açar. Arşivdeki parçayı dinleyip videoya ekleyebilirsin.");
        Add("+ Bilgisayardan müzik seç|Müzik dosyası seç","Bir müzik dosyası seçerek videoya ekler. Ses seviyesi ve başlangıç/bitiş ayarlarını zaman çizelgesinden düzenle.");
        Add("Bu videodan müziği kaldır","Seçili videonun müzik kaydını düzenleme taslağından kaldırır. Kaydettikten sonra yeni çıktıya yansıması için yeniden üret.");
        Add("▶ Hareketli önizleme","Zaman çizelgesini hareketli önizlemede oynatır. Son MP4'ün teknik kontrolünün yerini almaz.");
        Add("♫ Sesli önizlemeyi aç","Anlatım ve müzik dengesini dinlemek için sesli önizleme açar.");
        Add("■ Durdur","Editördeki önizleme oynatımını durdurur.");
        Add("Yakınlaş +|Uzaklaş −","Zaman çizelgesinin gösterim ölçeğini değiştirir; videonun süresini veya ses hızını değiştirmez.");
        Add("Shorts için normal video sesini kullan","Shorts ses seçimini normal videodaki ses ayarıyla eşler.");
        Add("Kapağı önizle|Kartı önizle|Önizle","Kaydedilmeden veya üretilmeden önce mevcut görsel/tasarım taslağını gösterir.");
        Add("Shorts tasarım kapağını aç|Kapağı aç|Kapağı incele","Hazırlanan kapak dosyasını açar. Bu işlem YouTube'a kapak yüklemez.");
        Add("Açılış kapağını tasarla / düzenle|Kapanış kapağını tasarla / düzenle|Yeni açılış / kapanış kartı oluştur|Yeni kart oluştur","Videonun başına veya sonuna gömülebilen kartın görsel tasarım editörünü açar. Kartı kaydet, pakette etkinleştir ve videoyu yeniden üret.");
        Add("Açılış kartı olarak seç|Kapanış kartı olarak seç","Seçilen kartı ilgili açılış veya kapanış ayarına bağlar. Yeni video üretiminde MP4 içine eklenir.");
        Add("Açılış kartını kapat|Kapanış kartını kapat|Video logosunu kapat","İlgili kart veya logoyu yeni üretimler için kapatır; önceden üretilmiş MP4 kendiliğinden değişmez.");
        Add("Hazır kapağı içe aktar|Kendi kapağını seç","Bilgisayarından hazır bir görseli tasarıma alır. Dosyayı seçtikten sonra önizleme ve boyutunu kontrol et.");
        Add("PNG dışa aktar|Kartı dosyaya kaydet","Kart tasarımını PNG görsel dosyası olarak seçtiğin yere kaydeder.");
        Add("Kartı galeriye kaydet","Tasarımı programın kart galerisine kaydeder. Sonra açılış veya kapanış kartı olarak seçebilirsin.");
        Add("Bu kartı düzenle / yeni kopya","Seçilen kartı düzenleme taslağına alır; farklı bir tasarım veya kopya oluşturabilirsin.");
        Add("Yazıyı sığdır","Kart metnini görsel sınırlarına sığacak şekilde ayarlar. Sonucu önizlemede kontrol et.");
        Add("Geri al|Yinele","Kart tasarımında önceki değişikliği geri alır veya geri alınan değişikliği yeniden uygular.");
        Add("Bu logoyu videolarda kullan","Seçilen logoyu video logosu ayarına bağlar; yeni çıktılarda seçilen konuma eklenir.");
        Add("Altyazıyı aç|Altyazıyı incele","Üretilmiş altyazı dosyasını açar. Son videoda zamanlama ve görünümü ayrıca izle.");
        Add("Videoyu izle|Videoyu oynat|▶ İzle","Hazır video dosyasını oynatıcıda açar. İçeriği, sesi ve altyazı uyumunu izleyerek kontrol et.");
        Add("İncele / çözüm kaydet|Çözümü kaydet • videoları koru","Editoryal uyarıyı inceleme notuyla çözülmüş olarak kaydeder. Metni veya video dosyasını değiştirmez.");
        Add("Görüntü haklarını kontrol ettim / onayla|Havuz videolarının kullanım haklarını onayla","İlgili görüntülerin kullanım haklarını kontrol ettiğini kaydeder. Lisans veya kullanım iznini kendisi sağlamaz.");
        Add("Havuz klasörünü aç","Programın stok video klasörünü açar; sahne seçimi için kullanılacak dosyaları burada yönetebilirsin.");
        Add("Klasörü tara / listeyi güncelle","Havuz klasöründeki dosyaları tarayıp arşiv listesini günceller. Tarama tamamlanmasını bekle.");
        Add("Paketlerden havuza al","Projelerdeki uygun görüntü dosyalarını ortak stok havuzuna alır.");
        Add("Havuzdan sil","Seçilen dosyayı stok havuzundan siler; dosyayı kullanan projeleri kontrol et.");
        Add("Etiket / lisans","Stok görüntünün konu etiketlerini ve kaynak/lisans bilgilerini düzenler.");
        Add("Türkçe / İngilizce fon kelimelerini göster","Stok görüntü aramasında kullanılabilecek konu/arka plan kelimelerini gösterir.");
        Add("Yerel motoru yükle","Yerel Ollama motorunu indirip kurma işlemini başlatır. İnternet, disk alanı ve uygun bilgisayar gerekir.");
        Add("Seçili modeli yükle","Seçilen Ollama modelini indirir; listedeki tüm modelleri indirmez. Model boyutuna göre disk ve bellek gerekir.");
        Add("Servisi başlat","Yerel Ollama hizmetini başlatmayı dener; ardından durumu yenileyerek bağlantıyı kontrol et.");
        Add("Seçili modeli dene|Ollama bağlantısını dene","Yerel modelin yanıt verebildiğini kontrol eden deneme yapar. İlk yükleme daha uzun sürebilir.");
        Add("Ana model yap","Seçilen kurulu yerel modeli içerik üretiminde kullanılacak ana model olarak ayarlar.");
        Add("Yükleme / yerel AI işlemini iptal et","Devam eden yerel motor/model yükleme veya deneme işlemini iptal eder.");
        Add("YouTube kanalını bağla","Tarayıcıdaki Google yetkilendirmesiyle YouTube kanalına bağlanır. Doğru Google hesabı ve kanalı seç.");
        Add("YouTube kanalını doğrula","Mevcut YouTube bağlantısından kanal kimliği ve adını alıp bağlantıyı doğrular.");
        Add("OAuth bilgilerini kaydet","YouTube bağlantısı için girilen OAuth istemci bilgilerini kaydeder; kanal yetkilendirmesini ayrıca tamamla.");
        Add("Google Türkçe seslerini getir|Google TTS ses listesini yenile|ElevenLabs seslerini listele","Sağlayıcıdaki kullanılabilir ses listesini günceller. Bağlantı ve ilgili hesap bilgileri gerekir.");
        Add("Seçili Google sesini dinle","Seçilen Google sesiyle kısa bir deneme seslendirmesi yapar. Sağlayıcının kullanım kotası geçerli olabilir.");
        Add("Seçili sesi kullan","Arşivde seçilen sesi seslendirme ayarına aktarır; doğru video/sağlayıcı ayarını kontrol et.");
        Add("Google TTS'yi durdur","Programdaki Google seslendirme kullanımını durdurur. Cloud hizmetinin ayrıca kapatılması farklı bir işlemdir.");
        Add("Cloud projesinde TTS API AÇ|Cloud projesinde TTS API KAPAT","Yetkili yönetim hesabıyla Google Cloud projesindeki TTS API hizmetini açar veya kapatır. Diğer uygulamalar da etkilenebilir.");
        Add("Google hesap kullanımını güncelle","Google hesap/proje kullanım bilgilerini yeniler; erişilebilir veri ve yetkilere bağlıdır.");
        Add("Salt okunur hizmet hesabı JSON ekle","Google kullanım takibi için salt okunur hizmet hesabı dosyası seçer.");
        Add("İsteğe bağlı API yönetim JSON'u ekle","Google Cloud API yönetim işlemleri için yetkili hizmet hesabı dosyası seçer. Dosyanın erişimini güvenli tut.");
        Add("Açılış şifresini değiştir|Yeni şifreyi kaydet","Uygulamanın yerel giriş şifresini değiştirir veya yeni şifreyi kaydeder. YouTube/Google şifresini değiştirmez.");
        Add("Şifremi unuttum","Kayıtlı kurtarma düzeni ve Windows hesabı üzerinden uygulama şifresini sıfırlama akışını açar.");
        Add("Giriş yap|Şifre oluştur ve aç","Girilen uygulama şifresiyle yerel çalışma alanını açar; ilk kurulumda şifre oluşturur.");
        Add("Sohbet","Programın sohbet yardım panelini açar. Model bağlantı ayarları yanıt üretimini etkileyebilir.");
        Add("Program kaynakları","Programın kaynak kod deposu ve kaynak yönetimi araçlarını açar. Video üretmek ve YouTube'a yüklemek için GitHub gerekmez.");
        Add("Program kaynaklarını GitHub’a gönder","Program kaynak kodlarını yapılandırılan GitHub deposuna gönderir. Video yükleme işlemi değildir; depo erişimi gerekir.");
        Add("Kaynak depo ayarlarını kaydet","İsteğe bağlı kaynak kod deposu ayarlarını kaydeder. YouTube bağlantısını ayarlamaz.");
        Add("Tüm üretimi durdur|Üretimi hemen durdur","Devam eden üretim işlemini iptal eder. Tamamlanan aşamaları korur; yarım çıktıyı yayınlamadan önce kontrol et.");
        Add("Hatanın tüm ayrıntıları","Son işlem hatasının ayrıntısını açar. Sorun giderirken bu bilgi kullanılabilir.");
        Add("Önceki 40|Sonraki 40","Listedeki önceki veya sonraki 40 kaydı gösterir.");
        Add("Ara","Girilen metne göre listede arama yapar.");
        Add("Vazgeç|İptal","Açık taslağı veya seçme işlemini iptal eder. Devam eden bir işlem penceresindeyse o işlemin iptal edilmesini ister.");
        Add("Kapat|Tamam","Açık bilgi veya yardımcı pencereyi kapatır.");
        Add("Evet|Hayır","Açık onay penceresindeki işlemi kabul eder veya reddeder. Karar vermeden önce mesajı oku.");
        Add("Rehberi kopyala","Rehberin tüm bölümlerini metin olarak panoya kopyalar; yalnız açık bölümü değil tüm rehberi alır.");
        Add("Rehberi dosyaya kaydet","Rehberin tamamını seçtiğin konuma TXT metin dosyası olarak kaydeder.");
        Add("+ Konu planla","Yeni konu/proje ekleme ekranını açar; konu ve yayın ayrıntılarını girerek planlayabilirsin.");
        Add("+ Ses dosyaları ekle","Bilgisayardan ses dosyalarını seçip ses/müzik arşivine ekler. Dosyalar arşive kopyalanır.");
        Add("Aktarımı başlat","Bu aktarım penceresinde seçtiğin ses dosyalarını arşive aktarmayı başlatır. İlerlemeyi tamamlanana kadar izle.");
        Add("Dinle","Seçilen ses dosyasını oynatıcıda açar. Arşiv kaydını veya üretim ayarını değiştirmez.");
        Add("Durdur","Bu penceredeki devam eden aktarım veya sohbet isteğini iptal eder. Tamamlanan kayıtlar ve sonuçlar korunur.");
        Add("Gönder","Yazdığın mesajı sohbet yardımcısına gönderir. Seçilen yapay zekâ sağlayıcısının bağlantısı ve kota koşulları geçerlidir.");
        Add("Temizle","Sohbet geçmişini ve ekrandaki konuşmayı temizler. Projeleri veya üretilmiş videoları silmez.");
        Add("Görselleri temizle","Kapak tasarımındaki görsel seçimlerini temizler. Kaydetmeden önce yeni tasarımı kontrol et.");
        Add("Üretimde kullan","Arşivdeki seçilen müziği üretim varsayılanına bağlar. Projeye özel müzik kaydı varsa ilgili projeyi ayrıca kontrol et.");
        Add("Programı paketlere dönüştür","Girilen konu programından içerik paketleri oluşturur. Oluşan konuları, süreleri ve yayın tarihlerini listede kontrol et.");
        Add("Stok videoları YouTube’a planlı yükle","Takvimdeki stok üretimlerini hazırlayıp onay/kontrol koşullarıyla YouTube'a planlı yükleme akışını başlatır. Doğru kanal ve gelecekte yayın zamanı gerekir.");
        Add("Stok video arşivi","Ortak stok görüntü havuzunun tarama, etiket, lisans ve dosya yönetimi ekranını açar.");
        Add("Ses ve müzik arşivi","Ortak ses/müzik dosyalarını listeleme, dinleme ve üretimde seçme ekranını açar.");
        Add("Kapak şablonları","Ortak kapak tasarımlarını, kartları ve logoları yönetme ekranını açar; projeye uygulamak için ilgili editörde seç.");
        Add("Arşiv klasörünü aç","Ses/müzik arşivinin dosya klasörünü Windows'ta açar.");
        Add("Bilgi / lisans","Seçilen ses dosyasının kaynak, eser sahibi ve kullanım hakkı/lisans bilgilerini düzenler.");
        Add("Videoları ekle","Bilgisayardan video dosyaları seçerek görüntü havuzuna ekler. Kullanım haklarını ve konu etiketlerini kontrol et.");
        Add("Arka plan görselini kaldır|Logoyu kaldır","İlgili görseli kart tasarımından kaldırır. Orijinal dosyayı silmez; son tasarımı kaydederek uygula.");
        Add("Arka plan görseli seç|Arka plan seç","Tasarımda kullanılacak arka plan görselini bilgisayarından seçer. Sonucu önizlemede kontrol et.");
        Add("Logo görseli seç|Kanal logosu seç","Kullanacağın logo dosyasını bilgisayardan seçer. Konumu ve boyutunu ilgili tasarım alanlarında ayarla.");
        Add("Nur Kapısı logosunu ekle","Programla gelen Nur Kapısı logo görselini açık kart tasarımına ekler. Kendi kanalın için kendi logo dosyanı seçebilirsin.");
        Add("Piper programını seç|Piper programı seç","Piper, metni bilgisayarında sese çeviren yerel seslendirme motorudur. Burada piper.exe dosyasını seç. API anahtarı gerekmez. Konuşmacının sesi ve dili ayrı seçilen ONNX modelinden gelir; yalnız program dosyasını seçmek yeterli değildir.");
        Add("Piper ses modelini seç|Piper modeli seç","Piper modeli, konuşmacının sesini ve dilini belirleyen ses paketidir. Türkçe konuşma için Türkçe .onnx dosyasını seç; aynı adlı .onnx.json yapılandırması yanında bulunmalıdır. Model indirildikten sonra yerel çalışır, API anahtarı veya API kotası gerekmez. Telaffuz ve doğallık modele bağlıdır. Ticari kullanım için seçtiğin ses modelinin lisansını kontrol et.");
        Add("Video motoru seç","Yerel video üretiminde kullanılacak FFmpeg çalıştırılabilir dosyasını seçer; araç klasöründeki diğer gerekli dosyaları koru.");
        Add("Yedek klasörü seç","Yüklemeden önce çıktı yedeği alınacak klasörü seçer. Boş bırakılırsa ikinci yedek oluşturulmaz; bulut senkronizasyonunu kendi uygulaması yapar.");
        Add("Google Cloud fatura raporu|Google Cloud kullanım paneli","Google Cloud'un fatura/kullanım sayfasını tarayıcıda açar. Doğru Google hesabı ve proje ile giriş yap.");
        Add("Hizmet sayfasını aç|Paneli aç|Anahtar al","İlgili sağlayıcının hesap veya API yönetim sayfasını tarayıcıda açar. Hizmetin giriş ve API erişim şartları geçerlidir.");
        Add("Klik sesini dene","Arayüz tıklama sesini bilgisayarda çalar. Bu ses video dosyasına veya anlatıma eklenmez.");
        Add("NVIDIA model listesini yenile|NVIDIA modellerini getir|Groq modellerini getir|Kimi modellerini getir","İlgili sağlayıcıdan erişilebilir model listesini getirir. İnternet ve geçerli API bilgisi gerekir.");
        Add("Kaydet|Değişiklikleri kaydet|Düzeni kaydet|Metinleri kaydet|Paketi kaydet|Paketin kapak ayarlarını kaydet|Tasarımı kaydet|Tüm ayarları kaydet","Bu ekrandaki değişiklikleri kaydeder. Mevcut MP4'ün değişmesi için gerekli durumda yeniden üret; kaydetmek tek başına YouTube'a yüklemez.");
        Add("Başlık rengi|Altyazı rengi|Vurgu rengi|Vurgu rengini seç","İlgili metin veya tasarım vurgusu için renk seçme penceresini açar; taslağı kaydet ve önizlemede kontrol et.");
        Add("Yenile|Durumu yenile","Bu ekranın listesini veya bağlı yerel hizmet/sağlayıcı durumunu yeniden kontrol edip günceller.");
        Add("Dene","Bu ekranda ayarlanan bağlantıyı veya özelliği deneme isteğiyle kontrol eder. Sağlayıcının kota veya ücret şartları geçerli olabilir.");
        Add("Önceki 20|Sonraki 20","Listedeki önceki veya sonraki 20 kaydı gösterir.");
        return map;
    }
    public static string Explain(string text)
    {
        var name=text.Trim();
        if(descriptions.TryGetValue(name,out var result))return result;
        if(name.Contains("İşlem kayıt",StringComparison.OrdinalIgnoreCase))return "Projenin işlem geçmişini gösterir veya gizler. Hata, deneme ve tamamlanan aşamaların kayıtlarını buradan incele.";
        if(name.StartsWith("Ekle •"))return "Bu formda ayarlanan tek videoyu bir sonraki sıra numarasıyla kaydeder. Üretim için Hepsini yayına yolla düğmesini ayrıca kullan.";
        if(name.StartsWith("Düzenlemeyi"))return "Açılmış projenin düzenleme alanını kapatır; proje listesindeki özet görünümüne döner.";
        if(name.StartsWith("▶"))return "Seçilen hazır videoyu oynatıcıda açar; yükleme veya yayın başlatmaz.";
        if(name.Contains("kaydet",StringComparison.OrdinalIgnoreCase))return "Bu ekranda yaptığın değişiklikleri kaydeder. Mevcut video dosyasının değişmesi gerekiyorsa yeniden üret; kaydetmek tek başına YouTube'a yüklemez.";
        if(name.Contains("model",StringComparison.OrdinalIgnoreCase) && (name.Contains("yenile",StringComparison.OrdinalIgnoreCase)||name.Contains("getir",StringComparison.OrdinalIgnoreCase)))return "İlgili sağlayıcıdan kullanılabilir model listesini getirir. İnternet ve geçerli hesap/API bilgileri gerekebilir.";
        if(name.Contains("yenile",StringComparison.OrdinalIgnoreCase)||name.Contains("Durumu",StringComparison.OrdinalIgnoreCase))return "Bu ekrandaki güncel liste veya bağlantı/işlem durumunu yeniden kontrol eder. Sonuç kullanılan yerel hizmete veya internet bağlantısına bağlıdır.";
        if(name.Contains("rengi",StringComparison.OrdinalIgnoreCase)||name.Contains("renk",StringComparison.OrdinalIgnoreCase))return "İlgili yazı veya tasarım öğesi için renk seçme penceresini açar. Taslağı kaydederek uygula.";
        if(name.Contains("seç",StringComparison.OrdinalIgnoreCase))return "Bu alana bağlanacak dosya veya seçeneği seçmeni sağlar. Seçimden sonra ilgili ayarları kaydet ve önizlemeyi kontrol et.";
        if(name.Contains("rehber",StringComparison.OrdinalIgnoreCase)||name.StartsWith("ⓘ"))return "Bu bağlantı veya kurulum konusunun açıklamasını ve uygulama adımlarını açar.";
        if(name.Contains("aç",StringComparison.OrdinalIgnoreCase)||name.Contains("Panel",StringComparison.OrdinalIgnoreCase)||name.Contains("raporu",StringComparison.OrdinalIgnoreCase)||name=="Anahtar al")return "İlgili dosya, hizmet sayfası veya yönetim panelini açar. Dış hizmet sayfaları internet ve hesap girişi isteyebilir.";
        if(name.Contains("dene",StringComparison.OrdinalIgnoreCase)||name=="Dene")return "Seçilen bağlantı, ses veya özelliği kısa bir denemeyle kontrol eder. Sağlayıcı denemeleri kullanım kotasına dahil olabilir.";
        if(name.Contains("Başlık"))return "Sahne başlıklarının metin, konum ve görünüm ayarlarını gösterir; kaydettikten sonra önizlemede kontrol et.";
        if(name.Contains("Altyazı"))return "Altyazının gösterimi ve görünüm ayarlarını açar. Altyazı sese göre zamanlanır; özgün anlatım sesini değiştirmez.";
        if(name.Contains("Müzik")||name=="Ses / müzik")return "Müzik dosyası, ses seviyesi ve solma ayarlarını açar.";
        return "“"+name+"” bölümünü veya bu adla belirtilen düzenleme seçeneğini açar. Ayrıntılı kullanım için sol menüdeki Rehber'i kullan; bilgi simgesine basmak işlemi başlatmaz.";
    }
}
