"""
Kur'an-ı Kerim içerik modülü.

Kaynak: alquran.cloud / islamic.network REST API (ücretsiz, API anahtarı gerekmez)
- Metin + Türkçe meal + gerçek hafız ses kaydı (Alafasy, Hüseyin, Sudais, vb.) ayet-ayet mp3

Hiçbir metin yapay zeka ile üretilmez; tamamen doğrulanmış kaynaktan çekilir.

=============================================================================
 ÖNEMLİ — MEAL VERİSİ BÜTÜNLÜK KONTROLÜ (2026-10 itibarıyla keşfedilen hata)
=============================================================================
alquran.cloud üzerindeki ``tr.diyanet`` ("Diyanet İşleri") meal edisyonunda
CİDDİ bir veri bozulması tespit edilmiştir: 114 surenin ~97'sinde ARDIŞIK
ayetlerin Türkçe meal metinleri BİREBİR AYNI dönmektedir. Örnekler:

  * Yasin (36): 2, 3 ve 4. ayetlerin üçü de aynı uzun metni döner
    (oysa Arapça metinleri birbirinden tamamen farklıdır).
  * Nas (114): 6 ayetin ALTISI da birebir aynı metni döner.
  * Bakara (2): 45=46, 183=184, 204=205 ... gibi onlarca eşleşme.

Bu bozulma yalnızca alquran.cloud'a özgü değildir: quran.com API'sindeki
"Turkish Translation (Diyanet)" (id 77) kaynağı da BİREBİR AYNI bozukluğu
göstermektedir; yani sorun iki sağlayıcının ortak kullandığı kaynak veridedir.

Aşağıda 114 surenin TAMAMI tek tek taranarak doğrulanmış TEMİZ edisyonlar
``TRANSLATION_EDITIONS`` sözlüğünde işaretlenmiştir. ``tr.golpinarli``
(Abdulbaki Gölpınarlı mealı) 114/114 surede hatasız olduğu için varsayılandır.

Ayrıca ``get_surah_ayahs`` her çekimde ``_validate_translations`` ile veriyi
doğrular; bu sınıf bozulma bir daha görülürse sessizce YANLIŞ dini metin
yayınlamak yerine net bir hata verir.
"""
from __future__ import annotations
import os
import requests
from dataclasses import dataclass
from typing import List

API_BASE = "https://api.alquran.cloud/v1"
CACHE_DIR = os.path.join(os.path.dirname(__file__), "..", "..", "assets", "cache", "quran")


class QuranDataError(RuntimeError):
    """Kaynaktan gelen Kur'an verisinin bütünlüğü bozuk olduğunda fırlatılır.

    Dini içerikte HATALI metin yayınlamak, hiç yayınlamamaktan çok daha kötü
    olduğu için bu hata bilinçli olarak pipeline'ı durdurur (görev 'failed'
    işaretlenir), sessizce geçilmez.
    """


# ---------------------------------------------------------------------------
# Meal (translation) edisyonları — 114 surenin tamamı tek tek doğrulanmıştır.
#   verified_clean=True  -> hiçbir surede ardışık ayetlerin meal metni tekrar etmiyor
#   verified_clean=False -> veri bozulması tespit edilmiş, KULLANILMAMALI
# ---------------------------------------------------------------------------
TRANSLATION_EDITIONS = {
    # --- doğrulanmış temiz ---
    "tr.golpinarli": {"name": "Abdulbaki Gölpınarlı", "verified_clean": True},
    "tr.ates": {"name": "Süleyman Ateş", "verified_clean": True},
    "tr.ozturk": {"name": "Yaşar Nuri Öztürk", "verified_clean": True},
    "tr.bulac": {"name": "Ali Bulaç", "verified_clean": True},
    "tr.yuksel": {"name": "Edip Yüksel", "verified_clean": True},
    # --- bilinen bozuk / doğrulanmamış ---
    "tr.diyanet": {"name": "Diyanet İşleri (KAYNAK VERİSİ BOZUK)", "verified_clean": False},
    "tr.vakfi": {"name": "Diyanet Vakfı (kısmi bozuk)", "verified_clean": False},
    "tr.yildirim": {"name": "Suat Yıldırım (kısmi bozuk)", "verified_clean": False},
    "tr.yazir": {"name": "Elmalılı Hamdi Yazır (kısmi bozuk)", "verified_clean": False},
}

DEFAULT_TRANSLATION_EDITION = "tr.golpinarli"

# Hafız (reciter) edisyon kodu -> insan tarafından okunabilir görünen ad
RECITER_DISPLAY_NAMES = {
    "ar.alafasy": "Mishary Rashid Alafasy",
    "ar.husary": "Mahmoud Khalil Al-Husary",
    "ar.husarymujawwad": "Mahmoud Khalil Al-Husary (Mücevved)",
    "ar.abdurrahmaansudais": "Abdurrahman As-Sudais",
    "ar.hudhaify": "Ali Al-Hudhaify",
    "ar.shaatree": "Abu Bakr Ash-Shaatree",
    "ar.minshawi": "Mohamed Siddiq Al-Minshawi",
    "ar.abdulbasitmurattal": "Abdul Basit Abdus-Samad",
    "ar.mahermuaiqly": "Maher Al-Muaiqly",
    "ar.ahmedajamy": "Ahmed ibn Ali Al-Ajamy",
    "ar.saoodshuraym": "Saood bin Ibraaheem Ash-Shuraym",
    "ar.yasser": "Yasser Al-Dosari",
    "ar.abdullahbasfar": "Abdullah Basfar",
    "ar.parhizgar": "Shahriar Parhizgar",
    "ar.aymanswoaid": "Ayman Sowaid",
    "ar.hanirifai": "Hani Ar-Rifai",
    "ar.ibrahimakhbar": "Ibrahim Al-Akhdar",
}


@dataclass
class Ayah:
    surah_number: int
    surah_name: str
    number_in_surah: int
    arabic_text: str
    translation_text: str
    audio_url: str
    local_audio_path: str | None = None


def _ensure_cache_dir():
    os.makedirs(CACHE_DIR, exist_ok=True)


def reciter_display_name(reciter_code: str) -> str:
    """Hafız edisyon kodunu okunabilir ada çevirir (örn. 'ar.alafasy' ->
    'Mishary Rashid Alafasy'). Bilinmeyen kod olduğu gibi döner."""
    return RECITER_DISPLAY_NAMES.get(reciter_code, reciter_code)


def translation_display_name(edition_code: str) -> str:
    """Meal edisyon kodunu okunabilir ada çevirir."""
    info = TRANSLATION_EDITIONS.get(edition_code)
    return info["name"] if info else edition_code


def _atomic_write_bytes(path: str, data: bytes):
    """Dosyayı ATOMİK yazar: önce geçici dosyaya, sonra ``os.replace`` ile taşır.

    İndirme yarıda kalırsa (ağ kesilmesi, runner zaman aşımı) bozuk/yarım dosya
    cache'te kalıcı hale gelir ve bir sonraki çalıştırmada JSON çözümlemesi
    sürekli başarısız olur. Bu yazma biçimi bunu engeller.
    """
    os.makedirs(os.path.dirname(path) or ".", exist_ok=True)
    tmp_path = f"{path}.part"
    with open(tmp_path, "wb") as f:
        f.write(data)
        f.flush()
        os.fsync(f.fileno())
    os.replace(tmp_path, path)


def _validate_translations(ayahs: List[Ayah], surah_number: int, edition: str):
    """Çekilen meal verisinin bütünlüğünü doğrular.

    Tespit edilen bozulma modu: Arapça metinleri birbirinden FARKLI olan ardışık
    ayetlerin Türkçe meal metinlerinin birebir aynı olması. Bu, meşru bir
    tekrardan ayırt edilebilir çünkü Kur'an'da iki farklı ayetin aynı Arapça
    metniyle başlaması neredeyse imkânsızdır; bozuk veride ise meal tekrar
    ederken Arapça metin farklıdır.

    Bozulma görülürse ``QuranDataError`` fırlatır — sessizce yanlış dini metin
    yayınlanmasına İZİN VERİLMEZ.
    """
    if len(ayahs) < 2:
        return
    for prev, cur in zip(ayahs, ayahs[1:]):
        same_tr = prev.translation_text.strip() == cur.translation_text.strip()
        diff_ar = prev.arabic_text.strip() != cur.arabic_text.strip()
        if same_tr and diff_ar:
            raise QuranDataError(
                "KUR'AN MEAL VERİSİ BOZUK — yayın DURDURULDU.\n"
                f"  Sure {surah_number}, ayet {prev.number_in_surah}-{cur.number_in_surah}: "
                "Arapça metinleri FARKLI olmalarına rağmen Türkçe meal metinleri "
                "BİREBİR AYNI geldi.\n"
                f"  Kullanılan edisyon: '{edition}'\n"
                f"  Tekrarlanan meal (ilk 120 karakter): {cur.translation_text[:120]!r}\n"
                "  Bu, kaynak sağlayıcıdaki (alquran.cloud) bilinen bir veri "
                "bozulmasıdır. Lütfen config/settings.yaml içinde "
                "quran.translation_edition değerini doğrulanmış TEMİZ bir "
                "edisyona çevirin (ör. 'tr.golpinarli')."
            )


def _fetch_json(url: str, timeout: int) -> dict:
    r = requests.get(url, timeout=timeout)
    r.raise_for_status()
    return r.json()


def get_surah_ayahs(
    surah_number: int,
    ayah_start: int,
    ayah_end: int,
    reciter: str = "ar.alafasy",
    translation_edition: str = DEFAULT_TRANSLATION_EDITION,
    timeout: int = 20,
) -> List[Ayah]:
    """Belirtilen sure/ayet aralığı için Arapça metin + Türkçe meal + hafız ses
    linklerini getirir ve verinin bütünlüğünü doğrular."""
    # Bilinen bozuk edisyonu kullanmaya çalışılırsa erkenden uyar.
    info = TRANSLATION_EDITIONS.get(translation_edition)
    if info is not None and not info["verified_clean"]:
        print(
            f"[quran] UYARI: '{translation_edition}' ({info['name']}) edisyonunda "
            f"veri bozulması tespit edilmiş. Doğrulanmış temiz bir edisyon "
            f"kullanmanız şiddetle tavsiye edilir (ör. '{DEFAULT_TRANSLATION_EDITION}')."
        )

    ar_data = _fetch_json(f"{API_BASE}/surah/{surah_number}/quran-uthmani", timeout)["data"]
    tr_data = _fetch_json(f"{API_BASE}/surah/{surah_number}/{translation_edition}", timeout)["data"]
    audio_data = _fetch_json(f"{API_BASE}/surah/{surah_number}/{reciter}", timeout)["data"]

    ar_ayahs, tr_ayahs, au_ayahs = (
        ar_data["ayahs"], tr_data["ayahs"], audio_data["ayahs"],
    )
    # Üç edisyonun da aynı sayıda ayet içermesi gerekir; zip() farklı uzunluklarda
    # sessizce kısaltma yapıp meal/Arapça metni birbirine KAYDIRIRDI.
    if not (len(ar_ayahs) == len(tr_ayahs) == len(au_ayahs)):
        raise QuranDataError(
            f"Kur'an verisi hizasız: sure {surah_number} için "
            f"arapça={len(ar_ayahs)}, meal={len(tr_ayahs)}, ses={len(au_ayahs)} ayet "
            f"döndü. Edisyonlar: {translation_edition} / {reciter}. "
            "Metinlerin birbirine kayması DİNİ AÇIDAN KABUL EDİLEMEZ; işlem durduruldu."
        )

    surah_name = ar_data["englishName"]
    ayahs: List[Ayah] = []
    for ar_ayah, tr_ayah, au_ayah in zip(ar_ayahs, tr_ayahs, au_ayahs):
        n = ar_ayah["numberInSurah"]
        if n < ayah_start or n > ayah_end:
            continue
        ayahs.append(
            Ayah(
                surah_number=surah_number,
                surah_name=surah_name,
                number_in_surah=n,
                arabic_text=ar_ayah["text"],
                translation_text=tr_ayah["text"],
                audio_url=au_ayah["audio"],
                local_audio_path=None,
            )
        )

    if not ayahs:
        raise QuranDataError(
            f"Sure {surah_number} için {ayah_start}-{ayah_end} aralığında ayet bulunamadı."
        )

    _validate_translations(ayahs, surah_number, translation_edition)
    return ayahs


def download_ayah_audio(ayah: Ayah, timeout: int = 30) -> str:
    """Hafız ses kaydını yerel diske indirir ve yolunu döndürür (cache'li)."""
    _ensure_cache_dir()
    fname = f"{ayah.surah_number:03d}_{ayah.number_in_surah:03d}_{os.path.basename(ayah.audio_url)}"
    local_path = os.path.join(CACHE_DIR, fname)
    if not os.path.exists(local_path):
        r = requests.get(ayah.audio_url, timeout=timeout)
        r.raise_for_status()
        _atomic_write_bytes(local_path, r.content)
    ayah.local_audio_path = local_path
    return local_path


def surah_display_name_tr(surah_number: int) -> str:
    """Sure adının Türkçe okunuşunu getirir (ör. 'Yasin')."""
    from .surah_names_tr import get_surah_name_tr
    return get_surah_name_tr(surah_number)


if __name__ == "__main__":
    ayahs = get_surah_ayahs(36, 1, 5)
    for a in ayahs:
        print(a.number_in_surah, a.translation_text)
    p = download_ayah_audio(ayahs[0])
    print("indirildi:", p)
