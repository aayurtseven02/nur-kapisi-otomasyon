"""
Kur'an-ı Kerim içerik modülü.

Kaynak: alquran.cloud / islamic.network REST API (ücretsiz, API anahtarı gerekmez)
- Metin + Diyanet Türkçe meali
- Gerçek hafız ses kaydı (Alafasy, Hüseyin, Sudais, vb.) ayet-ayet mp3

Hiçbir metin yapay zeka ile üretilmez; tamamen doğrulanmış kaynaktan çekilir.
"""
from __future__ import annotations
import os
import requests
from dataclasses import dataclass
from typing import List

API_BASE = "https://api.alquran.cloud/v1"
CACHE_DIR = os.path.join(os.path.dirname(__file__), "..", "..", "assets", "cache", "quran")


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


def get_surah_ayahs(
    surah_number: int,
    ayah_start: int,
    ayah_end: int,
    reciter: str = "ar.alafasy",
    translation_edition: str = "tr.diyanet",
    timeout: int = 20,
) -> List[Ayah]:
    """Belirtilen sure/ayet aralığı için Arapça metin + Türkçe meal + hafız ses linklerini getirir."""
    ar_resp = requests.get(f"{API_BASE}/surah/{surah_number}/quran-uthmani", timeout=timeout)
    ar_resp.raise_for_status()
    ar_data = ar_resp.json()["data"]

    tr_resp = requests.get(f"{API_BASE}/surah/{surah_number}/{translation_edition}", timeout=timeout)
    tr_resp.raise_for_status()
    tr_data = tr_resp.json()["data"]

    audio_resp = requests.get(f"{API_BASE}/surah/{surah_number}/{reciter}", timeout=timeout)
    audio_resp.raise_for_status()
    audio_data = audio_resp.json()["data"]

    surah_name = ar_data["englishName"]
    ayahs: List[Ayah] = []
    for ar_ayah, tr_ayah, au_ayah in zip(ar_data["ayahs"], tr_data["ayahs"], audio_data["ayahs"]):
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
    return ayahs


def download_ayah_audio(ayah: Ayah, timeout: int = 30) -> str:
    """Hafız ses kaydını yerel diske indirir ve yolunu döndürür (cache'li)."""
    _ensure_cache_dir()
    fname = f"{ayah.surah_number:03d}_{ayah.number_in_surah:03d}_{os.path.basename(ayah.audio_url)}"
    local_path = os.path.join(CACHE_DIR, fname)
    if not os.path.exists(local_path):
        r = requests.get(ayah.audio_url, timeout=timeout)
        r.raise_for_status()
        with open(local_path, "wb") as f:
            f.write(r.content)
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
