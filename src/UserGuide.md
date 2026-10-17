# TubeFlow — Yeni üretim akışı

Ana menü: Stüdyo • Üret, Projelerim ve Merkez. Sol üst düğmeyle yan menüyü açıp kapatabilirsin.

**1. Kaynak ve Metin:** Konuyu yaz, yatay veya dikey videoyu seç. Kendi içeriğini ekle veya AI talimatını yazıp metni hazırlat. Metni okuyup onayla.

**2. Tasarım:** Görsel kaynaklarını birlikte seçebilirsin. AI sesi veya konuşma dosyanı belirle. Altyazı açık/kapalı kararını mutlaka seç; açıksa hazır kalıbı belirle. Kapak, açılış ve kapanış isteğe göre arşivden alınabilir. Normal videoya ek Shorts istersen metinlerini de inceleyip onayla.

**3. Üretim:** Otomatik veya manuel çalışmayı seç. AI bölümleme isteyip istemediğini belirt. Bölümler arasında süre sınırlarını sürükleyebilir; istemediğin bölümü kaldırabilir ve geri alabilirsin.

**4. İzle ve düzelt:** Videoyu izle. Konuşma ile ekrandaki altyazıyı ayrı düzenle. Gereken sahneyi değiştir, videoyu güncelle ve sonucu onayla.

**5. Yayın:** Bilgisayara kaydet, özel yükle veya tarih seç. Şimdi yayınlama işlemi ayrıca onay ister.

Bir videoda önceden gömülü altyazı varsa ikinci katman istemediğinde Altyazı kapalı seç. Hazır MP3/WAV yeniden seslendirilmez. Eski yayınlanmış projede değişiklik için kopya oluştur.

---

## Ayrıntılı araç başvurusu

# TubeFlow 0.3.21 — Kısa Kullanım

- **Stüdyo • Üret:** Konu/metin → tasarım → üretim → izle ve kontrol et → yayınla. Yeşil adımlar tamamlanmıştır. Ses ve kapak tercihlerini tasarım adımında, düzeltmeleri kontrol adımında aç.
- **Videolarım ve Takvim:** YouTube ve projeleri güncelle düğmesi bağlı kanalın tüm erişilebilir videolarını sayfa sayfa getirir. YouTube’daki videolar ile yerel hazırlanan/bekleyen projeler ayrı başlıklarda görünür. İnternet sorunu son başarılı listeyi silmez.
- **Merkez:** Hesaplar ve AI, Medya Havuzu, Kanal ve Program. Medya türlerini filtrele; dosyalar ayrı arşiv klasörlerinde kalır.
- **Yerel görseller:** Merkez → Hesaplar ve AI → Yerel motorlar → Görsel motoru. Mevcut ComfyUI portable klasörünü ve kurulu modellere uygun API iş akışını bağla. Başlat ve dene bağlantıyı ve model adlarını kontrol eder; görsel üretmez. Hazır iş akışlarının model dosyaları ayrıca kurulu olmalıdır.

## Önceki ayrıntılı rehber

# TubeFlow kullanım rehberi

## 1. Başlangıç ve bilgi simgeleri

TubeFlow, normal video ve Shorts içeriklerini hazırlamak, bilgisayarında üretmek, incelemek ve bağlı YouTube kanalına yüklemek için kullanılır. Bir içerik paketi aynı konuya ait normal video ile bir veya daha fazla Shorts içerebilir. Yayın sırasına eklenen her satır ise ayarlarını ayrı belirlediğin bir videodur.

Üretim, yayın, model ve kullanım hakkı gibi açıklama gerektiren tuşlardaki (i) işareti açıklama açar; asıl işlemi başlatmaz. Tuşun yazı kısmına basmak işlemi çalıştırır. Bilgi simgesi bulunan tuşun üzerinde bekleyerek kısa açıklamayı görebilirsin; tuşa odaklanıp F1'e basmak da açıklama açar. Tamam, Kapat, Vazgeç, Evet/Hayır, Kopyala/Yapıştır ve basit gezinme tuşlarında ek bilgi simgesi yoktur. Önceden ayrı (i) tuşuyla açıklanan yayın işlemlerinde ikinci bir simge eklenmez.

Rehber, sol menüde Çalışma klasörü'nün altında bulunur. Soldan konu seç; üstteki arama kutusuna bir kelime yazarsan konu başlığı veya açıklaması eşleşen bölümler listelenir. Rehber internetsiz okunabilir. Kopyalama ve metin dosyasına kaydetme seçenekleri bulunur.

## 2. Ana menü ve ekran düzeni

Ana ekran: kanal bağlantısı, çalışma özeti ve projelere hızlı geçiş.
Video hazırla: içerikten yayına kadar projenin beş ana adımı.
Yayın sırası: tek tek video ekleme, üretim sırasını ve yayın tarihlerini yönetme.
Videolarım: tamamlanmış, planlanmış, yayında veya çalışılacak projeleri bulma.
Ayarlar: varsayılanlar, hesaplar, medya arşivleri ve gelişmiş araçlar.
Çalışma klasörü: programın veri klasörünü Windows'ta açma.
Rehber: bu açıklamalar.

Üstteki kanal adı bağlı YouTube hesabından alınır. Kanal adı görünmüyor veya yanlışsa Hesaplar bölümünde kanalı doğrula. Proje konusu ile kanal adı farklı bilgilerdir.

Yeşil adımlar o aşamanın tamamlandığını gösterir. Turkuaz çoğunlukla seçili alanı veya ana işlemi belirtir; yeşil tamamlanma işaretiyle aynı anlamda değildir. Üretim sırasında durum yazısı ve ilerleme çubukları işlemin aşamasını gösterir.

## 3. İlk kurulum ve gerekli araçlar

Önce Ayarlar > Hesaplar bölümünde video motoru ve kullanacağın hizmetleri kontrol et. Video üretimi için FFmpeg gerekir; Video motoru seç ile mevcut dosyanın yolunu belirleyebilirsin. Tam paketten güncellerken tools klasörünü koru. Yalnız yeni EXE'yi kopyalamak yerine güncelleme paketindeki ilgili dosyaları birlikte değiştir.

Metin üretimi için yerel Ollama veya etkin bir bulut sağlayıcı gerekir. Kendi hazır metnini kullanmak yapay zekânın metni yeniden yazmasını engeller; ancak diğer işlemler için seçilen hizmetler yine gerekebilir. Seslendirme sağlayıcını ve varsa hesap/model bilgilerini seç. Bulut hesaplarında API erişimi uygulama aboneliğinden ayrı olabilir.

Stok arşivini, ses/müzik dosyalarını ve kapak görsellerini ekleyebilirsin. YouTube bağlantısını yayınlamadan önce tamamla. Programın çalışması için bütün bulut hizmetlerini etkinleştirmen gerekmez; kullanacağın hizmetleri ayarla.

## 4. Giriş şifresi ve güvenlik

İlk girişte uygulama şifresi oluşturulur. Şifremi hatırla seçeneği isteğe bağlıdır ve yalnız ilgili Windows hesabında çalışır. Bu, YouTube veya Google şifresi değildir.

Şifremi unuttum, kayıtlı kurtarma düzeni ve Windows hesabıyla şifre sıfırlama akışını açar. Açılış şifresini değiştir seçeneği güvenlik ayarlarında bulunur. Kurtarma imkânının bulunması hesabın ve kayıtlı kurtarma bilgilerinin durumuna bağlıdır.

API anahtarlarını, OAuth bilgilerini ve hizmet hesabı JSON dosyalarını başkalarıyla paylaşma. Hata ekranını paylaşmadan önce özel anahtar veya hesap bilgisi içerip içermediğini kontrol et. Çalışma klasöründeki dosyaları elle silmek projeleri etkileyebilir; proje silme işlemini programdan yap.

## 5. Yeni proje ve içerik talimatı

Video hazırla > Yeni video ile konu ve proje ayrıntılarını gir. Normal video, Shorts sayısı ve hedef süreleri belirle. İçerik talimatına anlatım üslubunu, hedef izleyiciyi, değinilecek noktaları, kaynak beklentisini ve kaçınılacak öğeleri yaz.

Örnek: “Türkçe, sakin bir anlatıcı üslubu kullan. Girişte konuyu açıklayıp sonunda kısa bir kapanış ekle. İddiaları kaynaklarla destekle; kaynakta olmayan söz üretme. Stok görüntüler konuya uygun olsun.” Hedef süreyi ilgili süre alanından seç; talimat ile süreyi çeliştirme.

Proje ayarları bu pakete ait ayrıntıları değiştirir. Ayarlar bölümündeki varsayılanlar ise yeni üretimlere başlangıç değerleri sağlar. Bir ayarı değiştirmek, daha önce üretilmiş MP4 dosyasını kendiliğinden değiştirmez.

## 6. Kendi hazır metnini kullanma

Hazır metni kullan seçeneğini etkinleştirip hazır metin editörünü aç. Elle yazabilir, panodan yapıştırabilir veya UTF-8 TXT dosyası seçebilirsin. Normal video ve her Shorts metnini kendi sekmesine ayrı gir. Kaydet ile projeye uygula.

Hazır metin seçildiğinde yapay zekâ bu anlatımı yeniden yazmaz. Metindeki okunmasını istemediğin notları, sahne yönergelerini veya gereksiz simgeleri anlatımın dışında tut. Gerçek ses süresi seçilen sesin okuma hızına bağlıdır; hedef süre tam bir ses süresi garantisi değildir.

Stok görüntü temasına arka plan görüntülerinin konusunu yaz: örneğin “çöl, tarihi taş yapılar, deniz dalgaları, gün doğumu; modern silah ve şehir görüntüsü kullanma”. Başlık, kapak ve sahne seçimlerini üretimden önce kontrol et.

Kendi videolarını kullanmak için Hazır Metin Editörü → Stok Görüntü Teması → “+ Videolarımı ekle” düğmesinden MP4, MOV, MKV veya WEBM dosyalarını seç. “Yalnızca eklediğim videoları kullan” açıkken otomatik stok araması ve havuzdan alternatif seçimi kapanır. Videolar liste sırasıyla normal video ve Shorts sahnelerine dağıtılır. Liste bitince baştan döner; kısa görüntü sahne boyunca tekrarlanır. Yukarı/Aşağı Taşı ile sırayı değiştir; Sahne Editörü ile seçili listedeki videoları sahnelere farklı eşleştir. Manuel modda liste dışındaki görüntüler kullanılmaz. Eksik veya bozuk dosyada üretim durur ve dosyayı yeniden seçmen istenir. Kullanım haklarını doğrulama seçeneği bu paket için geçerlidir. Dosyaları üretim bitene kadar taşıma veya silme.

Normal Video • 16:9 sekmesine yatay videolarını ekle. Shorts 1 • 9:16 (ve diğer Shorts) sekmesinde “Bu Shorts için ayrı videolar seç” kutusunu işaretleyip dikey görüntülerini ekle. Her Shorts kendi listesini kullanır. Ayrı seçim kapalıysa ortak/normal video listesini kullanır. Shorts için 9:16, normal video için 16:9 kaynak zorunludur. Yanlış format seçimde, kayıtta ve üretim öncesinde reddedilir; yatay videoyu Shorts listesine ekleme. Yalnızca Shorts üretiyorsan ve her Shorts için ayrı liste seçtiysen ortak listeyi doldurman gerekmez.

## 7. İçerik üretimi ve kaynak denetimi

İçerik adımındaki Araştır + yaz veya Hazır metni kullan işlemi senaryo ve sahne planını hazırlar. Yerel öncelik açıksa Ollama bulut alternatiflerinden önce çalışabilir. Kayıtlarda sağlayıcı, model, deneme ve varsa düzeltme bilgileri görünür.

İşlem bittikten sonra sahne editörünü aç. Araştırma ve kaynakları göster ile kaynakları kontrol et. Kaynak bulunmuş olması bütün cümlelerin doğru olduğunu garanti etmez. Özellikle alıntı, ayet, hadis, kişi adı ve tarih gibi bilgileri yayın öncesi doğrula.

Bir uyarıyı yalnız gerçekten inceledikten sonra çözüm notuyla kaydet. Senaryoda düzeltme gerekiyorsa sahne editöründe yap ve video üretimini yenile. Uyarıyı çözülmüş olarak işaretlemek video içeriğini değiştirmez.

## 8. Sahne editörü ve stok görüntü

Her satır bir sahnedir. Türkçe seslendirme metni anlatıcı tarafından okunacak bölümdür. Stok araması görüntü bulmak için kullanılır; alternatifleri belirtilen ayırıcıyla yaz. Sahne/kapak vurgusu görüntü üzerinde kullanılabilecek kısa metindir. Yerel görüntü dosyası sahneye bağlanan dosyanın yoludur.

Seçili sahneye havuzdan video seç programın video havuzunu açar. Farklı kaynaktan video seç kendi klasörlerinden dosya seçmeni sağlar. Önce doğru satırı seç. Dosyanın uygunluğunu ve kullanım hakkını kontrol et; sadece bulunması izinli olduğu anlamına gelmez.

Başlık ve açıklamayı düzenleyebilir, başlık/kapak kontrolünden öneri alabilirsin. Değişiklikleri kaydet ile sahne planını uygula. Anlatım veya görüntü değişirse son MP4 için yeniden üretim gerekir.

## 9. Seslendirme ve doğal ses

Varsayılanlarda ses sağlayıcısı ve sesi seç; projeye özel ses seçimi varsa onu da kontrol et. Yerel veya bulut ses seçeneklerinin kurulum ve hesap ihtiyaçları farklıdır. Ses listelerini yenileme ve kısa dinleme seçenekleri kullanılabilir.

Metindeki kısaltmaların nasıl okunduğunu deneme sesinde dinle. Örneğin “Hz.” için Türkçe normalizasyon kullanılabilir; adların ve yabancı kelimelerin doğru telaffuzunu yine kontrol et. Hazır metinde yalnız seslendirilmesini istediğin cümleleri tut.

Altyazı sesin zamanlarına uydurulur; özgün anlatım sesini hızlandırıp yavaşlatarak altyazıya yetiştirme yaklaşımı kullanılmaz. Ses ve metin değişikliği zamanlamayı yeniden üretmeyi gerektirebilir. Üretilen anlatım sesini proje dosyalarından açıp doğrudan dinleyebilirsin.

## 10. Başlık, altyazı ve zaman çizelgesi

Ses ve tasarım adımındaki başlık/altyazı editöründe ilgili video ve sahneyi seç. Başlık, altyazı, görünüm, animasyon, geçişler ve müzik bölümleri ayrı seçeneklerdir. Tüm sahnelere uygula seçeneğinin hangi video üzerinde çalıştığını kontrol et.

Altyazı gösterimini açabilir veya kapatabilirsin. Metni değil sesi temel alan zamanlama üretim sırasında uygulanır. Kaydedilen tasarımda altyazının açık olması kadar son videodaki zamanlamanın doğru olması da önemlidir.

Zaman çizelgesine tıklamak o anı gösterir. Başlık ve müzik şeritlerinin konum/sürelerini desteklenen sürükleme işlemleriyle düzenleyebilirsin. Yakınlaş ve uzaklaş yalnız çizelge ölçeğini değiştirir. Hareketli önizleme ve sesli önizleme taslağı kontrol etmek içindir; son çıktıyı da izle.

## 11. Yapay zekâ ile sahne başlıkları

Sahne başlıklarını yapay zekâya ayarlatma seçeneğini etkinleştir veya Tüm sahne başlıklarını şimdi ayarla işlemini kullan. Amaç başlık metni ve desteklenen konum düzenlemelerini hazırlamaktır. Yazı tipi ve tasarım tercihlerini ilgili alanlardan kendin belirle.

Başlık Gösterim Süresi • Saniye alanından 0,5–60 saniye arasında süre seç; sonra Tüm Sahne Başlıklarını Şimdi Ayarla'ya bas. Yeni başlıklar seçtiğin ortak süreyi kullanır. Yapay zekâ seçeneği açıkken bu süreyi sonradan değiştirirsen seçili videodaki bütün başlıklar yeni süreyi kullanır. Normal video ve her Shorts kendi süre ayarını saklar. Başlık kendi sahnesinin sonunda biter; sonraki sahneye taşmaz. Düzeni Kaydet ile ayarı projeye uygula. Hazırlanmış videoya yansıtmak için videoyu yeniden üret.

Modelin yanıtını ve başlıkların sağda, solda veya ortada sığmasını önizlemede kontrol et. Yapay zekâ seçilmiş olması taşma kontrolünü gereksiz kılmaz. Uzun başlığı kısaltabilir veya yazı boyutunu düzenleyebilirsin. Başlık üretimini durdur devam eden isteği iptal eder.

## 12. Müzik dosyası ve ses dengesi

Zaman çizelgesindeki Müzik ekle ile arşivi aç. Yeni dosyalar penceredeki Müzik ekle düğmesiyle otomatik arşive kaydedilir. Parçayı seçip gömülü oynatıcıda dinleyebilir, Videoya ekle ile kullanabilirsin.

Müzik ayarları düğmesinden başlangıç, bitiş, ses seviyesi, solma ve konuşma sırasında kısma ayarlarını düzenle. Müzik notu bölümünde kısa bilgilendirme bulunur; lisans formu veya zorunlu onay yoktur.

Müzik kullanım haklarını kontrol et. İzinsiz kullanım YouTube üzerinde telif talebine veya videonun engellenmesine yol açabilir. Kullanım sorumluluğu kullanıcıya aittir.

## 13. Müzik klibi hazırlama

Yayın sırasına video eklerken Müzik klibi bölümünü kullan. Parça adı, söz/müzik veya eser sahibi bilgileri ile müzik dosyasını gir. Konuşma senaryosu yerine müzik esas alınır; stok veya kendi görüntülerini klip havasında kullanabilirsin.

Şarkı sözlerinin altyazı olmasını istemiyorsan altyazı gösterimini kapalı tut. Parça ve eser sahibi bilgisini başlık/açıklama veya desteklenen ekran metni alanlarında belirt. Müzik klibinde lisans formu doldurmak gerekmez.

## 14. Kapak editörü: normal video ve Shorts

Kapak editöründe Normal video ve Shorts sekmeleri ayrı ayarları içerir. İlgili sekmeyi seç; şablon veya kendi PNG kapak seçeneğini kullan. Tasarım tarzı, arka plan, kapak logosu, yazı tipi ve vurgu rengini düzenle. Kapağı önizle ile sonucu gör.

Hazır kapak seçildiğinde görselin üzerine tekrar metin ve logo eklenmez. Görselin üzerinde yazı varsa zaten tasarımın parçasıdır. Yatay normal video ve dikey Shorts için uygun boyut/oran kullan; hazır kapak yanlış orandaysa kabul edilmez. Her videonun Kapak Editörü sekmesinde açılış/kapanışı ayrı seçebilirsin. YouTube küçük resmi açılış görselinden ayrıdır. “Seçtiğim hazır kapağı açılışta da kullan” düğmesi doğru kapağı başlangıca ekler.

YouTube küçük resmi, videonun başına gömülen açılış kartından farklıdır. Shorts için hazır PNG'nin bulunması, YouTube'un o küçük resmi kabul ettiğini garanti etmez. İlgili hesap ve YouTube'un desteklediği kapak seçimi imkânlarını kontrol et.

## 15. Açılış, kapanış kartı ve video logosu

Kapak editöründeki Açılış/kapanış/video logosu sekmesinde kartları etkinleştir, görsel seç ve saniye cinsinden süreyi belirle. Açılış kartı anlatım başlamadan önce, kapanış kartı konuşma bittikten sonra video içine eklenir. Kanal logosunun dosyasını, konumunu ve genişliğini ayrıca seç.

Kart tasarım editörü metin, yazı tipi, yerleşim, efekt, çerçeve, arka plan ve logo alanlarını içerir. PNG dışa aktar veya galeriye kaydet; sonra ilgili paketin açılış/kapanış ayarına bağla. Sadece kart tasarlamak onu her videoya otomatik bağlamaz.

Paketin kapak ayarlarını kaydet ardından yeniden video üret. Eski MP4 yeni kartları kendiliğinden içermez. Son videonun ilk ve son saniyelerini izleyerek kartların ve logo konumunun uygulandığını doğrula. Shorts'ta kapanış istemiyorsan ilgili ayarı kapalı tut.

## 16. Üretim, ilerleme ve durdurma

Üret ve incele > Videoları üret ile çıktı oluştur. Senaryo, görüntü, ses, altyazı, müzik ve kart ayarları üretimde birleştirilir. Program gerekli kontrol tamamlanmamışsa açıklama gösterir; uyarının ilgili videosunu ve editörünü kontrol et.

Bölümlere ayrılmış üst gösterge hazırlık aşamalarını belirtir. Alt turkuaz çubuk üretim ilerlemesini ve tahmini kalan süreyi gösterir. Tahmin, ölçülen ilerlemeye göre değişir; ilk yükleme, ses üretimi ve görüntü işleme sırasında kesin süre verilemeyebilir.

Üretim sürerken programı açık tut. Tüm üretimi durdur veya Üretimi hemen durdur iptal ister; tamamlanan aşamalar saklanır. İşlem kayıtlarında son durum görünür. İptal edilmiş veya yarım dosyayı onaylayıp yayınlamadan önce kontrol et.

## 17. Yayın öncesi inceleme ve onay

İncele/onayla bölümünde normal video ve tüm Shorts çıktısını izle. Ses anlaşılır mı, altyazı sesle uyumlu mu, stok görüntü konuya uygun mu, başlıklar sığıyor mu, kapaklar ve müzik yerinde mi kontrol et.

Kaynaklar, editoryal uyarılar ve görüntü hakları için gereken incelemeyi tamamla. Uyarıyı çözüm notuyla kaydetmek içeriği değiştirmez; gerçek metin/görüntü değişikliği gerekiyorsa editöre dön ve yeniden üret.

İçeriği ve medyayı onayla yayın onayını kaydeder. Bu düğme tek başına YouTube'a yüklemez. Sonradan metin, medya veya tasarım değişirse eski onay geçersiz olabilir; güncel çıktıyı tekrar incele.

## 18. YouTube'a yükleme ve hemen yayın

Hesaplar bölümünde doğru YouTube kanalını bağla ve doğrula. Video dosyaları hazır ve güncel onaylı olmalıdır. Yayın bölümünde Şimdi yayınla, doğrudan yükleyip herkese açık yapma akışını açar; yayın öncesi onay sorulur.

YouTube'a özel yükle videoyu özel görünürlükte gönderir. Bu işlem izleyicilere açmaz. Yüklenen videoları şimdi yayına al, paketin yüklenmiş videolarını herkese açık yapar; eksik yükleme veya onay varsa önce tamamla.

Başlık, açıklama, çocuklara özel içerik ve gerçekçi değiştirilmiş/sentetik içerik alanlarını doğru doldur. YouTube/API projesinin erişim, denetim veya görünürlük kısıtları program dışındaki yayın sonucunu etkileyebilir. İşlem başarı mesajı ile bağlantıdaki gerçek durumu kontrol et.

## 19. Tarih ve saatle yayın planlama

Saatler Türkiye saatidir (UTC+3). Ana yayın tarihini ve saatini gir. Paket içinde normal videodan sonraki ilk Shorts ve sonraki Shorts videoları için saat aralığını belirle. Takvimi kaydet yalnız programdaki ayarları kaydeder; YouTube planlamasını ayrıca başlat.

YouTube'da yayını planla eksik videoları yükler veya mevcut yüklemelere yayın zamanı uygular. En az 10 dakika ileride bir zaman seç. Yükleme tamamlanana kadar program ve internet açık kalmalıdır. YouTube planı başarıyla kaydedildikten sonra yayın saatinde bilgisayarın açık olması gerekmez.

Yayın planını kaldır, yüklenmiş videoyu özel görünürlüğe alır ve tarihini kaldırır; silmez. Yayında olan bir videoyu da gizleyebilir. Bu işlemi yalnız istediğin video/paket üzerinde uygula.

## 20. On gün için ayrı ayrı video sırası

Yayın sırası > Bir video ayarla ve ekle ile ilk videoyu oluştur. Normal/Shorts türü, konu veya hazır metin, ses, müzik, kapak/kart/logo, altyazı, tarih ve saati ayarla. Ekle sonraki sıraya al ile kaydet. Sonra ikinci videoyu kendi ayrıntılarıyla ekle; toplu tek ayar yerine her satır ayrı düzenlenir.

Örneğin on gün içine beş normal ve beş Shorts eklemek için on satır oluştur. Tarih/saat ve türleri kontrol et, günlük üretim sınırını belirle. Hepsini yayına yolla işlemi sırayla üretir, kontrol eder ve YouTube'a planlı yükler.

Yayın tarihini çok yakın seçme; üretim ve yükleme için zaman bırak. Günlük sınır veya hata sıranın ilerlemesini durdurabilir. Sırayı duraklat otomatik ilerlemeyi keser. Ayrıntıları düzenle satır ayarlarını değiştirir; Sıradan çıkar yerel projeyi tamamen silmekle aynı işlem değildir.

## 21. Videolarım, arşiv ve yeniden düzenleme

Videolarım'da proje durumuna uygun grubu aç. Tamamlanmış projede çıktı hazırlanmış olabilir; bu tek başına YouTube'da yayında olduğunu göstermez. Planlanmış ve yayına alınmış projeler ayrı durumları ifade eder.

Düzenle ile kapalı projenin hazırlık alanını aç; video, kapak veya altyazıyı değiştirebilirsin. Yeni sürüm/kopya alternatif bir çalışma oluşturur. İzle simgesi hazır videoyu açar. Sil, onaydan sonra yerel projeyi ve dosyalarını siler; YouTube'daki videoyu silmez.

Program açıldığında durum kontrolü yapılır. Proje durumlarını kontrol et/güncelle ile elle de kontrol başlatabilirsin. Bağlantı sorunu varsa son bilinen durum güncellenemeyebilir. Yayınlanmış görünen yüklemeyi YouTube bağlantısıyla doğrula.

## 22. Varsayılan ayarlar ve sağlayıcı sırası

Yerel AI: Ollama kurulum, model seçimi, yerel öncelik ve bekleme süresi.
Bulut AI: etkin sağlayıcılar, alternatif sırası, modeller ve ücretli API izinleri.
Kanal/araştırma: kanal ve araştırma tercihleri.
Stok havuzu: görüntü kaynakları ve havuz davranışı.
Ses/müzik: seslendirme ve müzik varsayılanları.
Google TTS kota: kullanım takibi ve hizmet yönetimi.
Görüntü/altyazı: temel görsel ve altyazı ayarları.
Otonomi/denetim: otomatik çalışma ve kontrol tercihleri.
Arayüz/ses: arayüz ve tıklama sesleri.
Güvenlik: giriş, kurtarma ve ilgili güvenlik ayarları.

Tüm ayarları kaydet ile değişiklikleri uygula. Yerel öncelik etkinse önce Ollama denenebilir; bulut sağlayıcısı seçmek bu önceliği otomatik kaldırmaz. Alternatif sağlayıcıya geçişi işlem kayıtlarından izle. Ücretli API izni sağlayıcı kotası veya bakiye sağlamaz.

## 23. Yerel Ollama ve bilgisayar kapasitesi

Yerel motoru yükle Ollama kurulumunu, Seçili modeli yükle model indirmesini başlatır. Servisi başlat ve Durumu yenile ile hizmeti kontrol et. Seçili modeli dene yanıt testi yapar; Ana model yap seçimi içerik üretimine bağlar.

Büyük model daha fazla RAM/disk ve işlem gücü ister. İlk yükleme uzun sürebilir. Yerel üretim API bakiyesi kullanmaz, ancak bilgisayar kaynaklarını tüketir ve model indirmesi internet gerektirir. Yerel motor metin üretir; video motoru ve seslendirme sağlayıcısının yerine geçmez.

Yükleme/yerel AI işlemini iptal et devam eden işlemi durdurur. Model testinde başarı olması bütün içeriklerin aynı kalitede üretileceği garantisi değildir; kaynak ve anlatım kontrolünü sürdür.

## 24. Medya arşivleri ve dosyalar

Stok video arşivi ortak görüntü dosyalarını tutar. Havuz klasörünü aç, dosya ekle ve Klasörü tara/listeyi güncelle ile tarat. Etiket/lisans bölümünde konu ve hak bilgilerini düzenle. Paketlerden havuza al uygun dosyaları ortak kullanıma taşır.

Ses ve müzik arşivi tekrar kullanacağın sesleri bulmana yardımcı olur. Kapak şablonları ve kart galerisi görsel tasarımları saklar. Arşivde bulunması dosyanın her projeye otomatik uygulanması anlamına gelmez; ilgili editörde seç.

Dosyalar adımından MP4, kapak, anlatım ve altyazı çıktısına ulaş. Çalışma klasörünü taşımadan veya dosyaları silmeden önce yedek al. Projelerde kullanılan dosyaların yolunu bozmak yeniden üretimde hataya neden olabilir.

## 25. Hatalar, yeniden deneme ve kayıtlar

Önce ekrandaki uyarıyı oku; hangi video ve hangi kayıt eksik belirtiliyorsa o videoyu seçip ilgili editöre dön. Müzik hatasında gerçek dosya, eser sahibi/kaynak, kullanım türü ve hak onayını birlikte kontrol et. Sonra kaydet.

Model yanıtı, bağlantı, kota veya geçici hizmet hatalarında işlem kayıtları sağlayıcıyı ve denemeyi gösterir. Hatanın tüm ayrıntıları ile daha fazla bilgi açılır. Sürekli yeniden çalıştırmak yerine eksik dosya/ayar veya hizmet sorununu gider.

Altyazı zamanlama hatasında özgün ses dosyasını dinle ve metnin gerçekten sesle aynı olup olmadığını kontrol et. Tasarım değişikliğinin eski MP4'e yansımaması durumunda yeniden üret; yalnız editörü kaydetmek yeterli değildir.

## 26. GitHub, yedekleme ve program güncelleme

Video üretimi ve YouTube yüklemesi bilgisayarda yapılır; GitHub bu video akışının zorunlu parçası değildir. Program kaynakları ekranındaki GitHub araçları isteğe bağlı kaynak kod yönetimi içindir.

Güncellemeden önce programı kapat. Güncelleme paketini mevcut program klasörüne doğru şekilde çıkar; tools klasörünü koru. Kullanıcı verileri çalışma klasöründe saklanır. Güncelleme arşivi kişisel projelerinin yedeği değildir.

Yedekleme ayarlarını ve hedef klasörü kontrol et. Yedekleri başka bir konumda tut. Yeni sürümde önce mevcut bir projeyi açıp ayarlarını ve örnek önizlemeyi kontrol etmek faydalıdır.

## 27. Yayın öncesi kısa kontrol listesi

1. Doğru kanal, video türü ve konu seçildi mi?
2. Normal video ve her Shorts metni doğru mu; kaynaklar incelendi mi?
3. Ses, müzik dengesi ve altyazı senkronu son MP4'te dinlendi mi?
4. Stok görüntüler uygun mu; dosya ve müzik hakları doğrulandı mı?
5. Başlıklar sığıyor mu; doğru kapak, kartlar ve logo uygulanmış mı?
6. Güncel çıktılar incelenip onaylandı mı?
7. Yayın tarihi/saatinin Türkiye saati olduğu kontrol edildi mi?
8. Özel yükleme, hemen yayın veya planlı yayın doğru seçildi mi?
9. Yükleme tamamlandı ve YouTube durumu doğrulandı mı?

Rehber mevcut program özelliklerini anlatır. Sağlayıcıların erişim ve kota koşulları değişebilir; programdaki güncel mesajı ve hizmet hesabının durumunu kontrol et.


### Bakım, yedek ve yeni durum kuralları

Sol menüde Bakım ve Yedek: FFmpeg/FFprobe çalıştırma kontrolü, yerel ses eşleme dosyaları, proje yedeğini geri yükleme, önbellek boyutu, güvenli temizlik ve temizlenmiş destek kaydı. Dosyaların bulunması bir AI modelinin çalıştığı anlamına gelmez; yerel model için Durumu yenile / Seçili modeli dene kullanılır.

Projede Tam proje yedeği: kaynak görüntüler, müzik, kapak, plan ve çıktılar .ankaproject dosyasına konur ve özetleri doğrulanır. Geri yükleme yeni proje oluşturur; YouTube oturumu ve onayı taşınmaz. Kaynak eksikse önce yeniden seç. Eksik çıktı tamamlanmış listesine taşınmaz; YouTube’da yayında olduğu doğrulanmış kayıt ayrı değerlendirilir.

Genel ayarlar yeni projeler içindir. İlk üretimde ses ve görüntü varsayılanları projede sabitlenir. Mevcut projeye yeni varsayılanları uygulamak için Proje ayarları veya Yeni sürüm kullan. Kapak editöründe video sekmesindeki kart kutusu / süre değişikliği özel video ayarını açar. Hazır kapak seçmek Kendi PNG kapak türünü etkinleştirir.

Sahne editöründe geri al/yinele, kaynak URL’leri ve görüntü önizlemesi vardır. Stok seçimini engelle aynı kaynağın sonraki otomatik seçimlerini önler; mevcut sahneyi ayrıca değiştir. Araştırma kaynakları açıklamaya varsayılan olarak eklenir; Arayüz / ses bölümünden kapatılabilir. Kaynak listesi doğruluk garantisi değildir.

Hazır metin editöründe önceki metin ve ilk 300 karakter için kısa ses denemesi bulunur. Seçili ücretli ses sağlayıcısı kullanılıyorsa bu deneme de aynı kullanım koşullarına tabidir. Telaffuz sözlüğü her satırda yazılış=okunuş biçimindedir; altyazıdaki özgün yazımı değiştirmez.

Yayın sırası üretim uzadığında kendiliğinden tarihi değiştirmez. Tarih / saat ekranında erteleme kutusunu önceden seçersen süresi geçen yayın 30 dakika ileri alınabilir. Aksi halde hazır çıktılar korunarak yeni tarih istenir.

Sohbet sağda açılır. Küçük pencerede çalışma alanının üzerinde yan panel olur; Sohbet tuşuyla kapat. Seçili Anka AI sağlayıcısı kullanılır. Proje paylaşımı kutusu seçilmeden proje metni gönderilmez. Sohbet API anahtarıyla veya yerel yapay zekâyla çalışır; ChatGPT hesabı bağlama özelliği yoktur. Yanıtları doğrudan uygulamaz veya yayın yapmaz. Sohbet geçmişi bu Windows hesabındaki çalışma klasöründe tutulur; Temizle geçmişi temizler.

Uygulama parolası video ve metin dosyalarını şifrelemez. API anahtarları ve hesap oturumları yedeğe taşınmaz. Destek kaydı bilinen gizli bilgileri temizler; yine de paylaşmadan önce gözden geçir.

## Yeni oluşturma seçenekleri

Yeni Video tuşu içerik videosu, müzik klibi veya fotoğraflardan video seçeneklerini açar.

Müzik klibinde ana müziği Ses / müzik bölümünden seç ve kullanım bilgilerini tamamla. Süre parçadan okunur. Müzik klibi sekmesinde Seslendirme ekle ve Altyazı ekle ayrı seçeneklerdir; başlangıçta kapalıdır. Açarsan metin alanını doldur. Her satır bir sahne olur. Seslendirme varsa altyazı doğal konuşma zamanlarına göre hazırlanır. Yalnız altyazı varsa metin satırları müzik süresine eşit bölünür; şarkı sözleri otomatik tanınmaz. Seslendirme müzikten uzunsa metni kısalt veya daha uzun parça seç; program sesi zorla hızlandırmaz.

Fotoğraflardan video seçildiğinde Pexels ve Pixabay fotoğraf aramaları kullanılır. En az bir stok anahtarı gereklidir. Kaynak ve fotoğrafçı bilgileri korunur; fotoğraflar sahne süresince gösterilir. Fotoğrafları bir müzik klibinde de kullanabilirsin: Konu / yayın sekmesinde Fotoğraflardan video oluştur kutusunu seç. Sağlayıcı belgeleri: https://www.pexels.com/api/documentation/ ve https://pixabay.com/api/docs/

Kelime sayısı yüzünden duran projede Video Hazırla ekranında Eksik Kelimeleri Tamamla ve Devam Et tuşu görünür. Son kaydedilmiş taslak ve sahne seçimleri kullanılır. Üretim aşamasında durmuşsa metin düzeltildikten sonra üretime döner. Hazır kullanıcı metni bu özellik tarafından değiştirilmez. Değişen anlatımın sesi yeniden hazırlanabilir.

Tuşlar metin uzunluğuna göre boyut değiştirmez. Yazı tek satırda mevcut alana sığdırılır; çok dar alanlarda kısaltılmış yazının tamamı fareyle üstüne gelince gösterilir.


## NVIDIA
Bağlantı Merkezi → NVIDIA kartında anahtarını kaydet. Yönetim ayarlarında Bulut AI ve NVIDIA iznini aç; model listesini yenile, metin modelini seç ve tüm ayarları kaydet. Dene seçili modelle kısa bir istek yapar ve kota tüketebilir. Anahtar şifreli kasada saklanır; kota ve ücretler NVIDIA hesabının koşullarına bağlıdır.


## NVIDIA otomatik model kontrolü
NVIDIA anahtarı ve bulut kullanım izni kayıtlıysa otomatik seçim ilk açılışta arka plan taraması yapar. Her metin modeli kısa bir JSON isteğiyle sınanır; alanlar ve veri türleri kontrol edilir. İlk tarama tamamlanana kadar NVIDIA üretimi bekler; arayüz kullanılabilir. Tamamlanmış sonuçlar anahtara özel saklanır ve sonraki açılışlarda otomatik yeniden taranmaz. Yeni anahtarda veya elle yeniden tarama istendiğinde kontrol yapılır. Üretimde hata alan model beklemeye alınır ve kontrolü geçmiş diğer model denenir. Ayarlardaki NVIDIA Modellerini Tara, Taramayı Durdur ve Model Kontrol Sonuçları düğmeleriyle kontrol edebilirsin. Otomatik seçim kapatılınca yalnız seçtiğin model kullanılır. Tarama az miktarda API kotası tüketir; küçük isteğin başarısı uzun senaryo kalitesini veya kalan token miktarını garanti etmez. Üç kota reddinde tarama durur ve bekleme kaydedilir.

## Hikâyeyi yerel AI görselleriyle canlandırma

Yeni video / Proje ayarları veya Yayın sırası ekranında Hikâyeyi AI görselleriyle canlandır seçeneğini aç. Yerel motoru kullan başlangıçta seçilidir. Mevcut SDXL / ComfyUI motoru, IPAdapter Plus ve CLIP Vision ile ortak karakter referansını kullanır. Bu bilgisayarda karakter bileşenleri kurulmuştur. Başka bilgisayarda modeli ve karakter desteğini ayrıca kurmak gerekir.

Hikâye tarzını ve karakterlerin ad, yaş, yüz, saç/sakal, kıyafet ve renklerini yaz. Anlatım mevcut metin sağlayıcısında sahne görsel talimatlarına dönüştürülür; hazır anlatım değiştirilmez. Yerel görsel seçimi metin yapay zekâsı tercihini değiştirmez. Ortak karakter görseli bir kez hazırlanır, tüm sahnelere kimlik referansı olarak verilir. Sahne anlatımı olay, poz ve ortamı belirler. Tarz/karakter tanımı aynıysa hikâye metni değişse de referans korunur.

Her sahnenin görseli, üretim talimatı ve dosya özeti saklanır. Durdurma ve yeniden denemede sağlam çıktılar kullanılır; bozuk veya değişmiş görsel yeniden üretilir. Eksik model/bağlantı veya üretim hatası başarılı sayılmaz. Yerel modda hata olursa bulut görsel API'sine kendiliğinden geçilmez. Stok fotoğraf ve yalnız kendi videolarımı kullan seçenekleri aynı anda açık olamaz.

Görseller anlatımın ölçülen süresince gösterilir; yavaş kamera yakınlaşması isteğe bağlıdır. Bu hareket bir kamera efektidir; karakterleri yürütmek veya dudaklarını konuşturmak için ayrı video üretim modeli kullanılmaz. Ses, konuşmayla zamanlanan altyazı, müzik, geçiş ve yayın onayı mevcut akışta korunur.

Yerel seçimi kapatırsan referanslı Gemini / OpenAI görsel API'si kullanılabilir; ilgili anahtar ve mevcut ücretli görsel API izni gerekir, ücretsiz öncelik kapalı olmalıdır. Ortak referans ve sahne/alternatif denemeleri mevcut aylık görsel istek sayacına dahildir. NVIDIA kapak bağlantısı bu modda karakter referansı desteklemediği için kullanılmaz.

Referansla üretim karakter benzerliğini güçlendirir; yüzlerin, kıyafetlerin ve olayın her sahnede tam doğru olması garanti değildir. Önizlemede kontrol et; gerektiğinde karakter tanımını veya sahne görsel talimatını düzelt. AI görselleri de model/sağlayıcı kullanım koşullarına ve kullanıcı hak teyidine tabidir. Sistem kendiliğinden yayın onayı vermez.


## Sinematik hikâye üretimi
Hikâyeyi AI görselleriyle canlandır seçeneğini aç; altındaki Hikâye görselleri ve animasyon bölümünden Görselleri hareketlendir ve Sinematik animasyon seçeneklerini seç. Aynı bölüm Yeni video / Proje ayarları ve Yayın sırası düzenleyicisinde bulunur.

Sahne anlatımı ve görsel talimatına göre yavaş yaklaşma, yatay kamera geçişi, yükseliş, deniz salınımı veya kısa kontrollü sarsıntı seçilir. Hafif atmosfer parçacıkları ayrı katmanda hareket eder. Bu, fotoğraf animasyonudur; karakterlerin yürüyüşünü veya dudak hareketini üretmez. Süre, hazır sesin gerçek süresinden alınır; altyazı mevcut konuşma eşleme akışında kalır. Geçiş tipi Ses ve tasarım bölümündeki geçiş ayarından seçilir.

Durmadan üret seçeneği açıkken karakter ve sahne görsellerinde tek tek onay beklenmez. Oluşturulan görseller kayıtlı kalır; son videoyu önizleyip incelemen ve yayın için ayrıca onaylaman gerekir. Seçenek kapalıysa her görsel için mevcut onay ekranı korunur. Eksik motor veya görsel hatası stok görüntüyle gizlenmez.

Sinematik sahne hazırlanırken yüzde ilerlemesi gösterilir. Sağlam animasyon önbelleği durdurma ve yeniden denemede tekrar kullanılır; görsel, anlatım, hareket motoru veya çıktı ayarı değişirse yeniden hazırlanır. Ek Python paketi gerekmez; uygulamanın mevcut FFmpeg motoru kullanılır.

Ses motoruna giden metinde kesme işaretleri kaldırılır: Allah’ım → Allahım. Âmin → Aamin telaffuz karşılığı kullanılır; görüntüdeki özgün metin korunur. Telaffuz sözlüğüyle özel karşılık eklenebilir.


## Beş adımlı sade üretim ekranı (0.3.19)
Program Yeni İçerik Üret ekranında açılır. Sol menüde Yeni İçerik Üret, Videolarım ve Takvim, Ayarlar bulunur. Videolarım bölümünün üst sekmelerinden üretim/yayın sırası ve takvime geçilir. Arşivler ve bağlantılar Ayarlar altında, yardımcı araçlar Araçlar ve Yardım altında korunur.

1. Kaynak ve Metin: Konuyu yaz; hazır anlatımın varsa işaretleyip yapıştır. Devam Et metni kaydeder.
2. Tasarım: Kıssa/Hikâye, Belgesel, Normal + 2 Shorts veya Müzik/Fotoğraf Klibi seç. Metin ve kaynak hazırlığı bu seçimin ardından başlar; böylece doğru video türü için hazırlanır. Hazır planı olan projede üretim türü korunur; farklı kalıp için yeni proje aç.
3. Üretim: Videoyu Üret düğmesi mevcut üretim hattını çalıştırır. Gerçek ilerleme ve son işlem yazısı görünür. Müzik klibinde parça ve kullanım bilgilerini Müzik ve klip ayarlarından seç.
4. Düzenle ve Kontrol: Videoyu izle; sahne düğmesine basarak cümleyi düzelt veya görsel yenileme iste. Değişiklik öncesi proje yedeği saklanır. Teknik sonuçlar, kaynak sayısı, uyarılar ve kullanıcı hak teyidi gerçek kayıtlardan gösterilir. Yayın öncesi ayrıntılı inceleme/onay kapısı korunur.
5. Yayına Al: Güncel ve onaylı çıktıyı şimdi yayınla veya tarih seç. Bu ekranı görüntülemek yayınlama değildir; ayrıca yayın onayı gerekir.

Alt bölümdeki İnce ayar ve araçlar kapak, ses/altyazı/geçiş ve klasik çalışma alanına erişim sağlar. Kanalın ses ve hesap tercihleri kalıpla değiştirilmez. Kaynak sayısının görünmesi telif izni veya dini/tarihî doğruluk garantisi değildir.

Proje kayıtları ve şifreli hesap kasası mevcut biçimde korunmuştur; bu sürüm SQLite veya .ankaproject veri göçü yapmaz. Mevcut sağlam sahne önbellekleri yeniden kullanılır; yeniden birleştirme süresi video uzunluğuna ve bilgisayara bağlıdır, sabit saniye garantisi verilmez.


## Açılışta GitHub güncellemesi (0.3.20)
Normal TubeFlow.exe açılışında önce güncelleme penceresi görünür. GitHub Releases içindeki anka-update.json listesi okunur; kurulu sürümden sonraki ilk sürüm indirilir. Dosya özeti, paket içindeki uygulama sürümü ve ZIP yolları doğrulanır. Hesap/proje dosyaları paketle değiştirilemez.

Kurulum öncesi eski program dosyaları yedeklenir. Yeni uygulamanın bağımsız açılış kontrolü başarılıysa uygulama yeniden başlar ve GitHub listesini tekrar kontrol eder. Böylece sıradaki yamalar da uygulanır; liste bitince normal giriş ekranı açılır. Ağ hatası, kullanıcı iptali veya bozuk pakette mevcut sürüm açılır. Kurulum kontrolü başarısızsa yedeklenen program dosyaları geri alınır.

Program Files altındaki kurulumlarda Windows yönetici onayı güncelleme sırasında gerekebilir. Güncelleme yokken yönetici izni istenmez. Kontrol penceresinde Mevcut sürümü aç seçeneği vardır. Test ve arka plan işçisi komutları açılış güncellemesine girmez.

Yayın hazırlığı: tools/Publish-Update.ps1, .NET dahil Windows ZIP'ini ve önceki sürümler korunmuş anka-update.json listesini üretir. GitHub'a yeni vX.Y.Z etiketi gönderilince release-update.yml bu iki dosyayı Release'e ekler. İlk kuruluma bu açılış güncelleyicili sürüm bir kez yüklenmelidir. Dağıtım deposu ve Release dosyaları kullanıcılar tarafından erişilebilir olmalıdır; özel depoya ait erişim anahtarı uygulamaya gömülmez. Henüz GitHub Release yayınlanmadıysa mevcut sürüm açılır.
