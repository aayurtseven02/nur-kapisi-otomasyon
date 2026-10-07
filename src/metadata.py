"""
Başlık / açıklama / etiket üretim modülü.

Bilinçli olarak TAMAMEN ŞABLON TABANLI tasarlandı (serbest LLM üretimi değil):
dini içerikte başlık/açıklamanın kaynağı (hangi sure/hadis/dua olduğu) net ve
hatasız olmalı. Şablonlar content modüllerinden gelen gerçek veriyi doldurur.
"""
from __future__ import annotations
from dataclasses import dataclass
from typing import List, Optional

_TR_UPPER_TO_LOWER = str.maketrans({"I": "ı", "İ": "i"})
_TR_LOWER_TO_UPPER_FIRST = {"ı": "I", "i": "İ"}


def turkish_lower(text: str) -> str:
    """Türkçe'ye uygun küçük harf dönüşümü.

    Python'un yerleşik ``.lower()`` fonksiyonu "İ" harfini "i" + BİRLEŞEN
    NOKTA (U+0307) olarak üretir (yani "İhlas" -> "i̇hlas", 5 karakter değil
    6 karakter). YouTube etiketleri/açıklamalarında bu birleşen karakter
    kirik/garip görünür ve aramada eşleşmez. Bu fonksiyon "İ" -> "i",
    "I" -> "ı" dönüşümünü doğru yapar.
    """
    result = []
    for ch in text:
        if ch == "İ":
            result.append("i")
        elif ch == "I":
            result.append("ı")
        else:
            result.append(ch.lower())
    return "".join(result)


def turkish_upper(text: str) -> str:
    """Python'un yerleşik .upper() fonksiyonu Türkçe 'i' harfini 'I' yapar (yanlış,
    doğrusu 'İ'). Bu fonksiyon video üstü başlık gibi TAMAMEN BÜYÜK HARF gereken
    yerlerde Türkçe harfleri doğru çevirir (i->İ, ı->I, diğerleri normal upper)."""
    result = []
    for ch in text:
        if ch == "i":
            result.append("İ")
        elif ch == "ı":
            result.append("I")
        else:
            result.append(ch.upper())
    return "".join(result)


def turkish_title_case(text: str) -> str:
    """Python'un yerleşik .title() fonksiyonu Türkçe İ/I/ı/i harflerini yanlış
    çevirir (ör. 'UYANINCA'.title() -> 'Uyaninca', doğrusu 'Uyanınca' olmalı).
    Bu fonksiyon Türkçe harf eşlemesini doğru yaparak başlık harfi büyütür."""
    words = text.translate(_TR_UPPER_TO_LOWER).lower().split(" ")
    result_words = []
    for w in words:
        if not w:
            result_words.append(w)
            continue
        first = _TR_LOWER_TO_UPPER_FIRST.get(w[0], w[0].upper())
        result_words.append(first + w[1:])
    return " ".join(result_words)


@dataclass
class VideoMetadata:
    title: str
    description: str
    tags: List[str]


def build_sure_metadata(surah_name_tr: str, ayah_start: int, ayah_end: int,
                          reciter_display: str, is_short: bool,
                          channel_name: str, extra_tags: Optional[List[str]] = None,
                          meal_display: Optional[str] = None) -> VideoMetadata:
    ayet_araligi = f"{ayah_start}-{ayah_end}" if ayah_start != ayah_end else str(ayah_start)
    title = f"{surah_name_tr} Suresi ({ayet_araligi}. Ayetler) | Türkçe Meali ile Dinle"
    if is_short:
        title = f"{surah_name_tr} Suresi {ayet_araligi}. Ayet | Kur'an-ı Kerim #shorts"
    meal_line = (
        f"📖 Meal Kaynağı: {meal_display}\n\n" if meal_display
        else "📖 Meal Kaynağı: Diyanet İşleri Başkanlığı\n\n"
    )
    description = (
        f"{surah_name_tr} Suresi, {ayet_araligi}. ayetlerinin kıraati ve Türkçe "
        f"meali ile birlikte sunulmuştur.\n\n"
        f"🎙️ Kıraat: {reciter_display}\n"
        f"{meal_line}"
        f"Allah kabul etsin. Beğenmeyi ve abone olmayı unutmayın.\n\n"
        f"#Kuran #{surah_name_tr.replace(' ', '')}Suresi #İslam #Dua #Huzur\n\n"
        f"{channel_name}"
    )
    tags = ["kuran", "kuran-ı kerim", turkish_lower(surah_name_tr), "meal", "kıraat",
            "islam", "dini video"]
    if extra_tags:
        tags += extra_tags
    return VideoMetadata(title=title, description=description, tags=tags[:30])


def build_hadis_metadata(collection_display: str, hadith_number: int, hint: str, is_short: bool,
                           channel_name: str, extra_tags: Optional[List[str]] = None) -> VideoMetadata:
    base = hint or "Hadis-i Şerif"
    title = f"{base} | {collection_display}"
    if is_short:
        title += " #shorts"
    description = (
        f"\"{base}\"\n\n"
        f"📚 Kaynak: {collection_display}, Hadis No: {hadith_number}\n\n"
        f"Peygamber Efendimiz'in (Sallallahu Aleyhi ve Sellem) mübarek sözlerinden biri.\n\n"
        f"#Hadis #SünnetiSeniyye #İslam #{collection_display.replace(' ', '').replace(chr(39), '')}\n\n"
        f"{channel_name}"
    )
    tags = ["hadis", "hadis-i şerif", turkish_lower(collection_display), "sünnet",
            "islam", "peygamber efendimiz"]
    if extra_tags:
        tags += extra_tags
    return VideoMetadata(title=title, description=description, tags=tags[:30])


def build_dua_metadata(dua_title: str, source: str, is_short: bool,
                         channel_name: str, extra_tags: Optional[List[str]] = None) -> VideoMetadata:
    clean_title = turkish_title_case(dua_title.strip().rstrip(":"))
    title = f"{clean_title} | Dua"
    if is_short:
        title += " #shorts"
    description = (
        f"{clean_title}\n\n"
        f"📚 Kaynak: {source}\n\n"
        f"Hısnu'l Müslim (Müslümanın Kalesi) derlemesinden.\n\n"
        f"#Dua #Zikir #İslam #HısnülMüslim\n\n"
        f"{channel_name}"
    )
    tags = ["dua", "zikir", "hısnul müslim", "islam", "dini video"]
    if extra_tags:
        tags += extra_tags
    return VideoMetadata(title=title, description=description, tags=tags[:30])


def build_hikaye_metadata(hint: str, is_short: bool, channel_name: str,
                            extra_tags: Optional[List[str]] = None) -> VideoMetadata:
    title = hint or "Dini Kıssa"
    if is_short:
        title += " #shorts"
    description = (
        f"{hint}\n\nKıssadan hisse alalım.\n\n#DiniHikaye #Kıssa #İslam\n\n{channel_name}"
    )
    tags = ["dini hikaye", "kıssa", "islam", "ibret"]
    if extra_tags:
        tags += extra_tags
    return VideoMetadata(title=title, description=description, tags=tags[:30])


TEMA_CATEGORY_LABELS = {
    "hadis_sohbet": "Hadis Sohbetleri",
    "kuran_tefsir": "Kur'an'ın Işığında",
    "dua_fazilet": "Dualarla Huzur",
    # "dua_zikir" (dua + zikir fazileti) için "dua_fazilet" ile ÇAKIŞMAYAN,
    # ayrı bir seri adı verilir; aksi halde iki farklı seri aynı ekranda
    # "DUALARLA HUZUR #1" olarak görünür ve karışıklık yaratırdı.
    "dua_zikir": "Zikrin Bereketi",
    "ilmihal": "Günlük Hayatta İslam",
    "peygamberler_tarihi": "Peygamberler Tarihi",
    "sahabe": "Sahabe Hayatları",
    "tefekkur": "Tefekkür Vakti",
}

TEMA_CATEGORY_HASHTAGS = {
    "hadis_sohbet": "#Hadis #SünnetiSeniyye",
    "kuran_tefsir": "#Kuran #Tefsir",
    "dua_fazilet": "#Dua #Zikir",
    "dua_zikir": "#Zikir #Dua #Tesbih",
    "ilmihal": "#İlmihal #İslam",
    "peygamberler_tarihi": "#PeygamberlerTarihi #KurandanKıssalar",
    "sahabe": "#Sahabe #İslamTarihi",
    "tefekkur": "#Tefekkür #DiniBilgi",
}


def build_tema_metadata(hint: str, category: str, is_short: bool, channel_name: str,
                          sources: str = "", extra_tags: Optional[List[str]] = None) -> VideoMetadata:
    """Yorumlu/sohbet tarzı (tefsir, ilmihal, sahabe, peygamberler tarihi vb.)
    videolar için başlık/açıklama üretir. İçindeki metnin KENDİSİ (ayet/hadis/dua
    alıntıları) her zaman doğrulanmış kaynaktan alınır; sadece bağlayıcı anlatım
    yapay zeka ile yazılır (bkz. content_library/tema/ klasöründeki metin dosyaları)."""
    title = hint or "Dini Sohbet"
    if is_short:
        title += " #shorts"
    hashtags = TEMA_CATEGORY_HASHTAGS.get(category, "#İslam #DiniSohbet")
    kaynak_line = f"\n\n📚 Kaynaklar: {sources}" if sources else ""
    description = (
        f"{hint}\n\n"
        f"Bu video; ayet ve hadislerin güvenilir, doğrulanmış kaynaklardan alınan "
        f"meal/metinleri eşliğinde hazırlanmış bir sohbet/tefekkür içeriğidir."
        f"{kaynak_line}\n\n"
        f"Allah kabul etsin. Beğenmeyi ve abone olmayı unutmayın.\n\n"
        f"{hashtags} #İslam #Huzur\n\n"
        f"{channel_name}"
    )
    tags = ["islam", "dini sohbet", "tefekkür", (category or "islam").replace("_", " ")]
    if extra_tags:
        tags += extra_tags
    return VideoMetadata(title=title, description=description, tags=tags[:30])

