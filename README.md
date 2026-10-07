# İslami İçerik Kanalı — Tam Otonom Video Üretim & Yayınlama Sistemi

Bu proje; Kur'an sureleri, hadisler, dualar ve dini kıssalar için **otomatik
video üretimi, altyazı, başlık/açıklama/video kartı (thumbnail) tasarımı ve
YouTube'a zamanlanmış yayınlama** işini uçtan uca yapan, **%100 ücretsiz ve
açık kaynak** bileşenlerden oluşan bir sistemdir.

> ⚠️ **Dini içerik notu:** Kur'an metni, meal ve hadisler **asla yapay zeka
> ile üretilmez**. Yalnızca aşağıda listelenen, doğrulanmış/kaynak gösterilen
> veri tabanlarından çekilir. Dini kıssalar (hikayeler) ise bilinçli olarak
> otomatik üretilmez — bu metinleri siz, güvenilir bir kaynaktan derleyip
> sisteme eklersiniz (bkz. "İçerik Türleri" bölümü).

---

## 1) Mimariye Genel Bakış

```
config/schedule.yaml   -> "yayın takviminiz" (hangi video, ne zaman, hangi format)
config/settings.yaml   -> genel ayarlar (ses, görsel, YouTube ayarları)

src/content/           -> gerçek veri kaynakları (Kur'an, hadis, dua)
src/tts.py             -> Türkçe seslendirme (edge-tts) + altyazı zaman damgası
src/subtitles.py       -> .srt / .ass altyazı üretimi
src/visuals.py         -> ücretsiz stok video indirme (Pexels/Pixabay)
src/audio_tools.py     -> ses birleştirme + doğa ambiyansı
src/video_builder.py   -> ffmpeg ile video montajı, altyazı yakma
src/thumbnail.py       -> video kartı (thumbnail) tasarımı
src/metadata.py        -> başlık/açıklama/etiket şablonları
src/youtube_upload.py  -> YouTube Data API v3 ile zamanlanmış yükleme
src/pipeline.py         -> tek bir videoyu uçtan uca üreten orkestrasyon
src/scheduler.py        -> takvimi tarar, zamanı gelenleri işler

.github/workflows/publish.yml -> GitHub Actions ile 7/24 otomatik çalıştırma (ÜCRETSİZ)
```

### Akış
1. **GitHub Actions**, her 30 dakikada bir otomatik uyanır (ücretsiz, sunucu/bilgisayar açık tutmanıza gerek yok).
2. `scheduler.py`, `config/schedule.yaml` dosyanızı tarar; yayın saati yaklaşmış (varsayılan: 6 saat içinde) bir video bulur.
3. İçerik türüne göre (`sure` / `hadis` / `dua` / `hikaye`) gerçek kaynaktan metin + (varsa) gerçek hafız ses kaydı çekilir.
4. Gerekliyse Türkçe seslendirme (edge-tts) yapılır; cümle bazlı zaman damgalarıyla **birebir senkron** altyazı üretilir.
5. Pexels/Pixabay'dan insan figürü içermeyen (doğa, cami, kaligrafi vb.) stok görüntüler indirilir ve videoya işlenir.
6. ffmpeg ile klipler birleştirilir, ses (+ opsiyonel kısık doğa ambiyansı) mixlenir, altyazı yakılır.
7. Video karesinden otomatik bir **thumbnail** tasarlanır.
8. Başlık/açıklama/etiketler şablonlardan üretilir.
9. Video YouTube'a **"private" + zamanlanmış (`publishAt`)** olarak yüklenir. Yayın anını YouTube'un kendi
   sunucuları yönetir — tam o saatte otomatik olarak herkese açık olur. Uzun/Short formatı, dikey/yatay
   çözünürlük ve `#shorts` etiketi otomatik ayarlanır.
10. `schedule.yaml` içindeki durum `done`/`failed` olarak güncellenir ve GitHub'a geri commit'lenir.

---

## 2) Kullanılan Ücretsiz/Açık Kaynaklar (maliyet: 0 TL)

| İhtiyaç | Kaynak | Not |
|---|---|---|
| Kur'an metni (Arapça) | `api.alquran.cloud` | Ücretsiz, anahtar gerekmez |
| Kur'an Türkçe meali | `api.alquran.cloud` (Süleyman Ateş) | Ücretsiz, anahtar gerekmez — **bkz. uyarı** |
| Hafız ses kaydı (Alafasy, Hüseyin, Sudais...) | `islamic.network` CDN | Ücretsiz, anahtar gerekmez |
| Hadis metinleri (Buhari, Müslim, Ebu Davud, İbni Mace, Muvatta, Nevevi) | `fawazahmed0/hadith-api` (GitHub, CC0) | Ücretsiz, açık kaynak |
| Dua metinleri (Hısnu'l Müslim, 273 dua) | GitHub açık veri seti | Ücretsiz, açık kaynak |
| Türkçe seslendirme | **Google Cloud TTS (Chirp3-HD)**, yedek: `edge-tts` | Chirp3-HD: 1M karakter/ay ücretsiz (kart gerekir); edge-tts: tamamen ücretsiz/hesapsız yedek seçenek |
| Stok video | Pexels API + Pixabay API | Ücretsiz, anlık/bedava API anahtarı gerekir |
| Video/ses işleme | `ffmpeg` | Açık kaynak |
| Thumbnail tasarımı | `Pillow` | Açık kaynak |
| Fontlar | Noto Sans, Amiri (Google Fonts, SIL Open Font License) | Ücretsiz, repoya dahil |
| YouTube'a yükleme | YouTube Data API v3 | Google'ın resmi ücretsiz API'si |
| Otomatik çalıştırma/zamanlama | GitHub Actions | Public repo: sınırsız, Private repo: 2000 dk/ay ücretsiz |

**Hiçbir adımda kredi kartı veya ücretli abonelik gerekmez.**

> ### ⚠️ Kur'an meali kaynağı hakkında ÖNEMLİ uyarı
>
> **2026-10'da keşfedilen bir veri bozulması nedeniyle sistem artık varsayılan
> olarak `tr.diyanet` (Diyanet İşleri) mealini KULLANMIYOR.**
>
> `api.alquran.cloud` üzerindeki `tr.diyanet` edisyonunda ciddi bir hata
> tespit edilmiştir: **114 surenin ~97'sinde ardışık ayetlerin Türkçe meal
> metinleri birebir aynı dönmektedir.** Örnekler:
>
> | Sure | Hata |
> |---|---|
> | Yasin (36) | 2, 3 ve 4. ayetlerin üçü de aynı uzun metni döner (Arapça metinler farklı) |
> | Nas (114) | 6 ayetin **altısı** da birebir aynı metni döner |
> | Bakara (2) | 45=46, 183=184, 204=205 ... gibi onlarca eşleşme |
>
> Aynı bozulma `quran.com` API'sindeki "Turkish Translation (Diyanet)"
> kaynağında da görülmektedir; yani sorun iki sağlayıcının ortak kullandığı
> kaynak veridedir.
>
> **Çözüm:** 114 surenin tamamı tek tek doğrulanmış TEMİZ edisyonlar
> kullanılmaktadır:
>
> | Edisyon | Meal | Durum |
> |---|---|---|
> | `tr.ates` | **Süleyman Ateş** (varsayılan) | ✅ 114/114 sure hatasız (daha akıcı, sade Türkçe) |
> | `tr.golpinarli` | Abdulbaki Gölpınarlı | ✅ 114/114 sure hatasız (klasik) |
> | `tr.ozturk` | Yaşar Nuri Öztürk | ✅ 114/114 sure hatasız |
> | `tr.bulac` | Ali Bulaç | ✅ 114/114 sure hatasız |
> | `tr.yuksel` | Edip Yüksel | ✅ 114/114 sure hatasız |
> | `tr.diyanet` | Diyanet İşleri | ❌ **BOZUK — kullanmayın** |
>
> Ayrıca `src/content/quran.py` her çekimde verinin bütünlüğünü doğrular:
> Arapça metinleri farklı olan ardışık ayetlerin meal metinleri aynı gelirse
> `QuranDataError` fırlatır ve video **yayınlanmaz**. Dini içerikte hatalı metin
> yayınlamak, hiç yayınlamamaktan çok daha kötü olduğu için bu hata bilinçli
> olarak sessizce geçilmez.
>
> **Mealı değiştirmek isterseniz:** `config/settings.yaml` →
> `quran.translation_edition` değerini yukarıdaki temiz edisyonlardan biriyle
> değiştirmek yeterli (tek satır).
>
> **Varsayılan neden Süleyman Ateş?** Gölpınarlı mealı klasik ve güvenilir olsa
> da Türkçe'si yerinden ağırdır; TTS ile seslendirildiğinde örneğin Nas 4. ayet
> *"Gizlice, sinsisinsi vesveseler verenin şerrinden"* gibi cümleler kaba
> duyulabilir. Ateş meali daha akıcı/sade Türkçe ile yazıldığı için
> seslendirmede daha doğal okunur.

---

## 3) İçerik Türleri ve Doğruluk Politikası

| `type` | Kaynak | Yapay zeka üretir mi? |
|---|---|---|
| `sure` | alquran.cloud (Arapça metin + Süleyman Ateş meali + gerçek hafız sesi) | ❌ Hayır |
| `hadis` | fawazahmed0/hadith-api (Kütüb-i Sitte) | ❌ Hayır — sadece TTS ile seslendirilir |
| `dua` | Hısnu'l Müslim veri seti | ❌ Hayır — sadece TTS ile seslendirilir |
| `hikaye` | **Sizin sağladığınız metin dosyası** (`content_library/hikayeler/*.txt`) | ❌ Hayır — sistem sadece seslendirir/altyazılar, metni üretmez |
| `tema` | **Sizin sağladığınız metin dosyası** (`content_library/tema/*.txt`) | ❌ Hayır — sistem sadece seslendirir/altyazılar, metni üretmez |

Başlık ve açıklamalar da serbest LLM üretimi değil, **şablon tabanlıdır**
(`src/metadata.py`) — kaynak/hadis numarası/sure adı gibi bilgiler şablona
otomatik yerleştirilir, hatalı/uydurma bilgi riski taşımaz.

### `tema` kategorileri

`tema` tipindeki videolar `ref.category` alanıyla gruplanır; her kategori kendi
"seri adını", kapak alt yazısını ve etiketlerini alır:

| `category` | Seri adı | Kapak alt yazısı |
|---|---|---|
| `kuran_tefsir` | Kur'an'ın Işığında | Kur'an Tefsiri |
| `hadis_sohbet` | Hadis Sohbetleri | Hadis Sohbeti |
| `dua_fazilet` | Dualarla Huzur | Dua ve Fazilet |
| `dua_zikir` | Zikrin Bereketi | Dua ve Zikir |
| `ilmihal` | Günlük Hayatta İslam | İlmihal |
| `peygamberler_tarihi` | Peygamberler Tarihi | Peygamberler Tarihi |
| `sahabe` | Sahabe Hayatları | Sahabe Hayatı |
| `tefekkur` | Tefekkür Vakti | Tefekkür |

---

## 4) Kurulum Adımları

### A) Yerel test (opsiyonel ama tavsiye edilir)

```bash
cd islami-otomasyon
pip install -r requirements.txt
sudo apt-get install ffmpeg   # Mac: brew install ffmpeg | Windows: choco install ffmpeg

# Tek bir videoyu YouTube'a yüklemeden, sadece yerelde üretip test edin:
python3 -m src.pipeline 2026-10-07-short-niyet-hadisi
# output/ klasöründe mp4 ve thumbnail dosyasını bulacaksınız.
```

Stok video için (opsiyonel, yoksa otomatik placeholder gradyan klip kullanılır):
```bash
export PEXELS_API_KEY="..."
export PIXABAY_API_KEY="..."
```

### B) Ücretsiz Pexels / Pixabay API anahtarı alma (2 dakika)

> **Not (güncel durum):** Pexels şu anda yeni API anahtarı vermeyi geçici
> olarak durdurmuş durumda ("New API key issuance is currently paused").
> Bu geçici bir Pexels kısıtlaması, sizinle ilgili bir sorun değil. **Pixabay
> tek başına da gayet yeterli** — sistem zaten Pexels anahtarı yoksa otomatik
> olarak Pixabay'ı kullanıyor. İleride Pexels tekrar anahtar vermeye
> başlarsa, aynı şekilde `PEXELS_API_KEY` olarak ekleyebilirsiniz.

1. ~~https://www.pexels.com/api/~~ (şu an yeni kayıt almıyor, ileride tekrar deneyebilirsiniz)
2. **Pixabay (şu an kullandığımız):** https://pixabay.com/api/docs/ adresine ücretsiz hesapla giriş yapın;
   anahtarınız aynı sayfada, örnek isteklerin içinde (`key=...`) otomatik olarak görünür —
   ayrı bir başvuru/dashboard yoktur. Sayfada `Ctrl+F` ile "key=" arayarak da bulabilirsiniz.

### C) Google Cloud TTS (Chirp3-HD) Kurulumu — DOĞAL SESLENDİRME İÇİN

Test dinlemelerinde edge-tts'in sesini yeterince doğal bulmadığımız için sistem
artık varsayılan olarak **Google Cloud'un en yeni nesil "Chirp3-HD" seslerini**
kullanacak şekilde ayarlandı (`config/settings.yaml` → `tts.engine: google_cloud`).
Bu, Kur'an ayetlerini etkilemez (onlarda zaten gerçek hafız sesi kullanılıyor);
sadece hadis/dua/hikaye anlatımını seslendirir.

**Bilmeniz gereken:** Bu adım, Google Cloud hesabınıza bir kredi kartı eklemenizi
gerektirir (ücretsiz kotada kaldığınız sürece ücret yansımaz — Chirp3-HD için
ayda 1.000.000 karakter ücretsizdir, bu kanalınız için muhtemelen aylarca yeter).
Riski sıfıra yakın tutmak için aşağıda bütçe uyarısı kurma adımını da ekledim.

1. https://console.cloud.google.com → (YouTube için kullandığınız projeyi kullanabilir
   ya da yeni bir proje açabilirsiniz).
2. **"API'ler ve Hizmetler" → "Kitaplık"** → **"Cloud Text-to-Speech API"** arayıp **Etkinleştir**.
3. İlk kez bir ücretli API'yi etkinleştirdiğinizde Google sizden **faturalandırma hesabı**
   (kredi kartı) bağlamanızı isteyecek — "Faturalandırmayı Bağla" diyip kartınızı ekleyin.
4. **Güvenlik için bütçe uyarısı kurun:** Sol menüden **"Faturalandırma" → "Bütçeler ve uyarılar"
   → "Bütçe Oluştur"**. Örneğin 1 TL/1 USD gibi çok düşük bir eşik belirleyip %50/%90/%100'de
   e-posta uyarısı alacak şekilde ayarlayın. Böylece beklenmedik bir kullanım artışı olursa
   anında haberiniz olur.
5. **(Opsiyonel ama tavsiye edilir) Kota sınırı koyun:** "API'ler ve Hizmetler" →
   "Cloud Text-to-Speech API" → "Kotalar" sekmesinden günlük/aylık karakter sınırını
   makul bir değere (örn. kanalınızın ihtiyacının birkaç katı) manuel olarak düşürebilirsiniz.
6. **API anahtarı oluşturun:** "API'ler ve Hizmetler" → "Kimlik Bilgileri" → "Kimlik Bilgisi
   Oluştur" → "API anahtarı". Oluşan anahtarı kopyalayın, sonra **"Anahtarı Kısıtla"**
   diyip "API kısıtlamaları" altından sadece **"Cloud Text-to-Speech API"**'yi seçin
   (böylece anahtar çalınsa bile başka bir API için kullanılamaz).
7. Bu anahtarı `GOOGLE_TTS_API_KEY` olarak yerel ortam değişkeninize ve GitHub
   repo secrets'a ekleyin (adım D'de diğer secret'larla birlikte).

**edge-tts'e geri dönmek isterseniz:** `config/settings.yaml` içinde
`tts.engine: "google_cloud"` satırını `tts.engine: "edge"` olarak değiştirmeniz
yeterli — hiçbir kod değişikliği gerekmez, anında ücretsiz/hesapsız moda döner.

**Farklı bir Chirp3-HD sesi denemek isterseniz:** `config/settings.yaml` →
`tts.google_cloud.voice_name` değerini değiştirin. Türkçe erkek seçenekler:
`tr-TR-Chirp3-HD-Charon`, `Fenrir`, `Orus`, `Achird`, `Algenib`, `Puck`, `Enceladus`,
`Iapetus`, `Rasalgethi`, `Sadachbia`, `Sadaltager`, `Schedar`, `Umbriel`, `Zubenelgenubi`,
`Algieba`, `Alnilam` (hepsi `src/tts_google_cloud.py` içinde `MALE_VOICES` listesinde).

---

### D) YouTube Data API Kurulumu (adım adım)

Bu, otomatik yüklemenin çalışması için **tek seferlik** ve **ücretsiz** bir kurulumdur.

1. https://console.cloud.google.com adresine gidin, Google hesabınızla giriş yapın.
2. Üstten **"Yeni Proje"** oluşturun (örn. "islami-kanal-otomasyon").
3. Sol menüden **"API'ler ve Hizmetler" → "Kitaplık"** açın, **"YouTube Data API v3"** arayıp **Etkinleştir**'e tıklayın.
4. **"API'ler ve Hizmetler" → "OAuth onay ekranı"**:
   - Kullanıcı tipi: **Harici (External)**
   - Uygulama adı, destek e-postası gibi zorunlu alanları doldurun.
   - "Kapsamlar" adımını atlayabilirsiniz (varsayılan ile devam).
   - "Test kullanıcıları" adımında **kendi Gmail/YouTube hesabınızı** ekleyin.
5. **"API'ler ve Hizmetler" → "Kimlik Bilgileri" → "Kimlik Bilgisi Oluştur" → "OAuth istemci kimliği"**:
   - Uygulama türü: **Masaüstü uygulaması (Desktop app)**
   - Oluşturduktan sonra **"JSON indir"** deyip dosyayı `client_secret.json` olarak projenizin
     ana klasörüne kaydedin. *(Bu dosyayı asla paylaşmayın/commit etmeyin — `.gitignore` zaten engelliyor.)*
6. Kendi bilgisayarınızda (tarayıcınız açılacak, o yüzden sunucuda değil yerelde çalıştırın):
   ```bash
   python3 scripts/oauth_setup.py --client-secrets client_secret.json
   ```
   Tarayıcı açılacak, YouTube hesabınızla giriş yapıp izin vereceksiniz. İşlem bitince terminalde
   şöyle 3 değer göreceksiniz:
   ```
   YT_CLIENT_ID      = ...
   YT_CLIENT_SECRET  = ...
   YT_REFRESH_TOKEN  = ...
   ```

### E) GitHub'a yükleme ve otomatik çalıştırmayı etkinleştirme

1. Bu klasörü bir GitHub reposuna yükleyin (public veya private, ikisi de ücretsiz çalışır).
2. Repo → **Settings → Secrets and variables → Actions → New repository secret** ile şu 6 secret'ı ekleyin:
   - `YT_CLIENT_ID`, `YT_CLIENT_SECRET`, `YT_REFRESH_TOKEN` (adım D'den)
   - `PEXELS_API_KEY`, `PIXABAY_API_KEY` (adım B'den)
   - `GOOGLE_TTS_API_KEY` (adım C'den)
3. Repo → **Actions** sekmesine girip workflow'un göründüğünü doğrulayın. İsterseniz
   **"Run workflow"** ile elle bir kere `no_upload: true` seçerek test tetikleyin.
4. Artık `.github/workflows/publish.yml` her 30 dakikada bir otomatik çalışacak ve
   `config/schedule.yaml`'da zamanı gelen videoları otomatik üretip yayınlayacaktır.

---

## 5) Programınızı (Takvimi) Yönetme

`config/schedule.yaml` dosyasını düzenleyerek yeni video ekleyin:

```yaml
- id: "2026-10-10-mulk-suresi"
  status: "pending"
  publish_at: "2026-10-10T20:00:00+03:00"
  format: "long"          # long | short
  type: "sure"            # sure | hadis | dua | hikaye
  ref:
    surah_number: 67
    ayah_start: 1
    ayah_end: 10
    reciter: "ar.alafasy"
  title_hint: "Mülk Suresi (1-10. Ayetler)"
  background_mode: "nature"
```

Değişikliği GitHub'a `git push` ettiğinizde, bir sonraki Actions çalıştırmasında
sistem bu videoyu otomatik olarak işleyecektir. Yayın saatinden en az "lead
time" (varsayılan 6 saat) önce video hazırlanıp YouTube'a yüklenir; gerçek
yayın anı tam istediğiniz saatte YouTube tarafından gerçekleştirilir.

---

## 6) Önemli Sınırlamalar ve Notlar

- **Google Cloud TTS kart gereksinimi:** Chirp3-HD sesleri için Google Cloud
  hesabınıza kart eklemeniz gerekir (ücretsiz kotada kaldığınız sürece $0
  ücretlendirilirsiniz). Riski en aza indirmek için README'deki bütçe uyarısı
  ve kota sınırlama adımlarını mutlaka uygulayın. Kart eklemek istemezseniz
  `config/settings.yaml` → `tts.engine: "edge"` yaparak anında tamamen
  ücretsiz/hesapsız edge-tts moduna dönebilirsiniz.
- **YouTube API günlük kota:** Google varsayılan olarak günde 10.000 "birim" kota
  verir; bir video yüklemesi ~1600 birim tutar (thumbnail dahil ~1650). Yani
  varsayılan kotayla **günde ~6 video** yükleyebilirsiniz. Daha fazlası için
  Google Cloud Console'dan ücretsiz kota artışı talep edebilirsiniz (onay süresi birkaç gün).
- **Arka plan sesi:** Talebiniz doğrultusunda enstrümantal müzik kullanılmaz.
  Varsayılan olarak hafif, programatik üretilmiş "doğa ambiyansı" kullanılır.
  Kendi royalty-free doğa sesi kayıtlarınızı `assets/library/nature/` klasörüne
  koyarsanız sistem onları rastgele seçip kullanır.
- **Dini hikaye/kıssa içerikleri** bilinçli olarak otomatik üretilmez; bu metinleri
  siz sağlarsınız. Bu, dini doğruluğu garanti etmenin tek güvenli yoludur.
- **Video üretim süresi:** Uzun bir sure (ör. 20 ayet) videosu, GitHub Actions
  sunucusunda birkaç dakika sürebilir; bu süre free tier limitlerinin çok altındadır.
- **Short süre sınırı (`video.max_short_seconds`):** Eşik **180 saniye
  (3 dakika)** — YouTube'un gerçek Shorts limiti. YouTube, 15 Ekim 2024 ve
  sonrası yüklenen, **kare veya dikey (9:16)** formatdaki videoları 3 dakikaya
  kadar Short sayar; 180 sn üzeri bir dikey video Short sayılmaz, normal uzun
  video olur. (Kaynak: YouTube Help → *"Understand three-minute YouTube Shorts"*)

  > **Kullanıcı kararı (nihai):** Mevcut yapı bozulmayacak, hiçbir video
  > kırpılmayacak — **hepsi olduğu gibi yayınlanacak.** Yalnızca 3 dakikayı
  > **geçen** videolar iptal edilecek. Bu yüzden eşik 180 sn'e çekildi ve
  > politika `fail` yapıldı.

  **Gerçek ölçüm (2026-10-07):** 60 short videosunun tamamı edge-tts ile
  seslendirilip ölçüldü — **59'u 60 sn'i aşıyor** (ortalama **86.7 sn**, en
  uzun 113.5 sn; ortalama ~11.7 karakter/saniye). Ancak **hepsi 180 sn'in rahat
  altında** olduğu için hiçbiri iptal edilmez; tamamı eksiksiz yayınlanır.

  Sınırı aşan videolara ne olacağını belirleyen politika
  (`config/settings.yaml` → `video.short_over_limit_action`):

  | Politika | Davranış |
  |---|---|
  | `fail` **(aktif)** | 180 sn'i aşan video üretilmez/yüklenmez, görev `failed` işaretlenir. |
  | `warn` | Video olduğu gibi üretilir ve yüklenir, logda uyarı görürsünüz. |
  | `trim` | Video son **tam cümlenin** sonunda kırpılır (şu an kullanılmıyor). |

  **Kırpma güvenliği (`trim` seçilirse):** Kırpma noktası olarak yalnızca
  noktalama ile gerçekten biten cümle sonları seçilir. `split_long_cues` bir
  cümlenin virgülden bölünmüş parçasını ayrı bir altyazı cue'u yaptığı için,
  cümle ortasındaki bir cue'da asla kesilmez — dini alıntılar (ayet/hadis/dua)
  yarım kalmaz.

  > **Not — Content ID:** 1 dakikayı aşan bir Short, üzerinde aktif bir Content
  > ID şikâyeti varsa YouTube tarafından **küresel olarak engellenir** (oynatılamaz,
  > önerilmez, para kazandırmaz). Bu sistemde arka plan müziği bilinçli olarak
  > kapalıdır (`audio.background_mode: "none"`) ve short'lardaki ses yalnızca
  > kendi metninizin TTS seslendirmesidir; bu yüzden şikâyet riski yoktur.
- **Thumbnail yüklenemezse video yine de yüklenir:** YouTube, kanal telefonla
  doğrulanmamışsa `thumbnails().set()` çağrısını reddeder. Sistem artık bu
  durumda uyarı verip yüklemeyi başarılı sayar; aksi halde aynı video bir
  sonraki koşuda **tekrar** yüklenir (mükerrer video).
- Thumbnail ve video kalitesi, kullandığınız Pexels/Pixabay API anahtarlarıyla
  çekilen gerçek stok görüntülerle (placeholder değil) çok daha etkileyici olur;
  bu yüzden API anahtarlarını eklemeniz şiddetle tavsiye edilir.

---

## 7) Genişletme Fikirleri (opsiyonel, ileride eklenebilir)

- Farklı hafızlar arasında rastgele/sırayla geçiş
- Çoklu kanal desteği (ayrı schedule.yaml / settings.yaml profilleri)
- Otomatik "oynatma listesi" (playlist) ataması
- Yorumlara otomatik yanıt (YouTube API ile, dikkatli ve nezaketli şablonlarla)
- Analitik tabanlı en iyi yayın saati önerisi (YouTube Analytics API)

---

## 8) Hızlı Komut Özeti

```bash
# Tek video üret (yüklemeden, test):
python3 -m src.pipeline <schedule-id>

# Takvimi tara ve zamanı gelen 1 videoyu üret+yükle:
python3 -m src.scheduler

# Takvimi tara ama yüklemeden sadece üret (test):
python3 -m src.scheduler --no-upload --lead-time-hours 48
```
