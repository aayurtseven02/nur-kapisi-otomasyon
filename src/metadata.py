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
                          channel_name: str, extra_tags: Optional[List[str]] = None) -> VideoMetadata:
    ayet_araligi = f"{ayah_start}-{ayah_end}" if ayah_start != ayah_end else str(ayah_start)
    title = f"{surah_name_tr} Suresi ({ayet_araligi}. Ayetler) | Türkçe Meali ile Dinle"
    if is_short:
        title = f"{surah_name_tr} Suresi {ayet_araligi}. Ayet | Kur'an-ı Kerim #shorts"
    description = (
        f"{surah_name_tr} Suresi, {ayet_araligi}. ayetlerinin kıraati ve Diyanet İşleri Başkanlığı "
        f"Türkçe meali ile birlikte sunulmuştur.\n\n"
        f"🎙️ Kıraat: {reciter_display}\n"
        f"📖 Meal Kaynağı: Diyanet İşleri Başkanlığı\n\n"
        f"Allah kabul etsin. Beğenmeyi ve abone olmayı unutmayın.\n\n"
        f"#Kuran #{surah_name_tr.replace(' ', '')}Suresi #İslam #Dua #Huzur\n\n"
        f"{channel_name}"
    )
    tags = ["kuran", "kuran-ı kerim", surah_name_tr.lower(), "meal", "kıraat", "islam", "dini video"]
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
    tags = ["hadis", "hadis-i şerif", collection_display.lower(), "sünnet", "islam", "peygamber efendimiz"]
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
