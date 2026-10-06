"""
Hadis içerik modülü.

Kaynak: fawazahmed0/hadith-api (GitHub, CC0 - açık kaynak/ücretsiz)
Koleksiyonlar: tur-bukhari, tur-muslim, tur-abudawud, tur-ibnmajah, tur-malik, tur-nawawi

Metinler doğrudan klasik hadis kitaplarından (Kütüb-i Sitte / Nevevi 40 Hadis) alınır;
yapay zeka ile hadis metni ASLA üretilmez. Bu dosya sadece var olan, kaynak gösterilen
metinleri getirir.
"""
from __future__ import annotations
import os
import re
import json
import requests
from dataclasses import dataclass

CDN_BASE = "https://cdn.jsdelivr.net/gh/fawazahmed0/hadith-api@1/editions"
CACHE_DIR = os.path.join(os.path.dirname(__file__), "..", "..", "assets", "cache", "hadith")

COLLECTION_DISPLAY_NAMES = {
    "tur-bukhari": "Sahih-i Buhari",
    "tur-muslim": "Sahih-i Müslim",
    "tur-abudawud": "Sünen-i Ebu Davud",
    "tur-ibnmajah": "Sünen-i İbni Mace",
    "tur-malik": "Muvatta",
    "tur-nawawi": "Nevevi'nin 40 Hadisi",
}

# fawazahmed0/hadith-api kaynağındaki Türkçe metinlerin SONUNA, orijinal web
# sitesinden miras kalan bibliyografik/editöryel ekler iliştirilmiş oluyor
# (ör. "Tekrar: 54, 2529, 3898...", "Diğer Tahric:: Müslim, imare;...",
# "İZAHI İÇİN BURAYA TIKLA"). Bunlar hadisin GERÇEK METNİNİN BİR PARÇASI
# DEĞİL; sadece site arayüzünden kalan referans/çapraz-kaynak künyeleridir.
# Seslendirme/altyazıda bunlar okununca anlamsız/karışık ("saçma") bir ses
# çıkıyor. Bu yüzden hadisin asıl sözünü ASLA değiştirmeden, sadece bu footer
# künyesini kırpıyoruz (ilk eşleşen işaretten itibaren).
_CITATION_FOOTER_PATTERNS = [
    re.compile(r"Tekrarı?\s*:"),
    re.compile(r"Diğer\s+Tahric"),
    re.compile(r"\bTahric\s*:"),
    re.compile(r"İZAHI"),
]


def _strip_citation_footer(text: str) -> str:
    earliest = None
    for pat in _CITATION_FOOTER_PATTERNS:
        m = pat.search(text)
        if m and (earliest is None or m.start() < earliest):
            earliest = m.start()
    if earliest is not None:
        text = text[:earliest]
    return text.strip()



@dataclass
class Hadith:
    collection: str
    collection_display: str
    hadith_number: int
    text: str


def _ensure_cache_dir():
    os.makedirs(CACHE_DIR, exist_ok=True)


def _load_collection(collection: str, timeout: int = 60) -> dict:
    """Koleksiyonun tam JSON'unu indirir ve yerelde cache'ler (büyük dosya, bir kez indirilir)."""
    _ensure_cache_dir()
    local_path = os.path.join(CACHE_DIR, f"{collection}.min.json")
    if not os.path.exists(local_path):
        url = f"{CDN_BASE}/{collection}.min.json"
        r = requests.get(url, timeout=timeout)
        r.raise_for_status()
        with open(local_path, "wb") as f:
            f.write(r.content)
    with open(local_path, "r", encoding="utf-8") as f:
        return json.load(f)


def get_hadith(collection: str, hadith_number: int) -> Hadith:
    """Belirli bir hadis numarasını koleksiyondan getirir."""
    data = _load_collection(collection)
    for h in data["hadiths"]:
        if h["hadithnumber"] == hadith_number:
            return Hadith(
                collection=collection,
                collection_display=COLLECTION_DISPLAY_NAMES.get(collection, collection),
                hadith_number=hadith_number,
                text=_strip_citation_footer(h["text"]),
            )
    raise ValueError(f"Hadis bulunamadı: {collection} #{hadith_number}")


def search_short_hadiths(collection: str, max_chars: int = 280, limit: int = 20):
    """Shorts formatı için kısa hadisleri bulur (yardımcı/opsiyonel fonksiyon)."""
    data = _load_collection(collection)
    results = []
    for h in data["hadiths"]:
        text = _strip_citation_footer(h["text"])
        if len(text) <= max_chars:
            results.append(
                Hadith(
                    collection=collection,
                    collection_display=COLLECTION_DISPLAY_NAMES.get(collection, collection),
                    hadith_number=h["hadithnumber"],
                    text=text,
                )
            )
        if len(results) >= limit:
            break
    return results


if __name__ == "__main__":
    h = get_hadith("tur-bukhari", 1)
    print(h.collection_display, "-", h.hadith_number)
    print(h.text[:300])
