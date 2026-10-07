"""
Stok görüntü modülü.

Sağlayıcılar (ikisi de ÜCRETSİZ, anlık ve bedava API anahtarı ile):
  - Pexels   (https://www.pexels.com/api/)   -> env: PEXELS_API_KEY
  - Pixabay  (https://pixabay.com/api/docs/) -> env: PIXABAY_API_KEY

En az biri tanımlıysa çalışır. Hiçbiri yoksa (ör. yerel test) placeholder
ffmpeg gradyan klipleri üretir, böylece pipeline anahtarsız da uçtan uca
test edilebilir.

Dini hassasiyet gereği arama kelimeleri settings.yaml > stock_video.keyword_pool
içinden seçilir (insan yüzü/figürü olmayan doğa, cami, kaligrafi vb. temalar).
"""
from __future__ import annotations
import os
import random
import subprocess
import zlib

import requests
from dataclasses import dataclass
from typing import List, Optional

CACHE_DIR = os.path.join(os.path.dirname(__file__), "..", "assets", "cache", "stock_video")


@dataclass
class StockClip:
    local_path: str
    source: str
    keyword: str


def _ensure_cache_dir():
    os.makedirs(CACHE_DIR, exist_ok=True)


def _pexels_search(keyword: str, orientation: str, api_key: str, per_page: int = 5) -> List[dict]:
    url = "https://api.pexels.com/videos/search"
    headers = {"Authorization": api_key}
    params = {"query": keyword, "orientation": orientation, "per_page": per_page, "size": "medium"}
    r = requests.get(url, headers=headers, params=params, timeout=20)
    r.raise_for_status()
    return r.json().get("videos", [])


def _pexels_pick_file_url(video: dict, target_w: int) -> Optional[str]:
    files = sorted(video.get("video_files", []), key=lambda f: abs((f.get("width") or 0) - target_w))
    return files[0]["link"] if files else None


def _pixabay_search(keyword: str, api_key: str, per_page: int = 5) -> List[dict]:
    url = "https://pixabay.com/api/videos/"
    params = {"key": api_key, "q": keyword, "per_page": per_page, "safesearch": "true"}
    r = requests.get(url, params=params, timeout=20)
    r.raise_for_status()
    hits = r.json().get("hits", [])
    return [h for h in hits if not _has_irrelevant_tags(h.get("tags", ""))]


# Dini içerikle alakasız/uygunsuz temaları (yılbaşı, yiyecek, hayvan vb. yanlış eşleşmeler)
# ayıklamak için basit bir kara liste. Arama sonucu alakasız geldiğinde bir sonraki
# anahtar kelimeye/klibe geçilir; video asla rastgele/konu dışı kalmaz.
_IRRELEVANT_TAG_BLOCKLIST = {
    "christmas", "santa", "xmas", "halloween", "party", "alcohol", "beer", "wine",
    "dumpling", "food", "cuisine", "zongzi", "dragon", "cartoon", "character",
    "dating", "couple kissing", "swimsuit", "bikini",
}


def _has_irrelevant_tags(tags_str: str) -> bool:
    tags = {t.strip().lower() for t in tags_str.split(",")}
    return bool(tags & _IRRELEVANT_TAG_BLOCKLIST)



def _download(url: str, out_path: str, timeout: int = 60):
    r = requests.get(url, stream=True, timeout=timeout)
    r.raise_for_status()
    with open(out_path, "wb") as f:
        for chunk in r.iter_content(chunk_size=1 << 16):
            f.write(chunk)


def _placeholder_clip(out_path: str, width: int, height: int, duration: float, seed: int):
    """API anahtarı yokken yerel test için basit hareketli gradyan klip üretir.

    ``seed`` için anahtar kelimenin ``hash()`` değeri DEĞİL, kararlı bir
    özet (zlib.crc32) kullanılır: Python'un ``hash()`` fonksiyonu süreç
    başına rastgele tuzlanır (PYTHONHASHSEED), bu yüzden aynı anahtar kelime
    her çalıştırmada FARKLI renkler üretip tekrarlanabilirliği bozuyordu.
    """
    random.seed(seed)
    c1 = f"{random.randint(0,80):02x}{random.randint(40,90):02x}{random.randint(20,60):02x}"
    c2 = f"{random.randint(0,40):02x}{random.randint(20,60):02x}{random.randint(40,90):02x}"
    filter_complex = (
        f"gradients=s={width}x{height}:d={duration}:c0=0x{c1}:c1=0x{c2}:x0=0:y0=0:"
        f"x1={width}:y1={height}:speed=0.02"
    )
    cmd = [
        "ffmpeg", "-y", "-f", "lavfi", "-i", filter_complex,
        "-t", str(duration), "-pix_fmt", "yuv420p", out_path,
    ]
    subprocess.run(cmd, check=True, capture_output=True)


def _stable_seed(text: str) -> int:
    """Süreçler arası kararlı, pozitif bir tohum üretir (hash() tuzlanmasından etkilenmez)."""
    return zlib.crc32(text.encode("utf-8")) & 0x7FFFFFFF


def fetch_clip_for_keyword(
    keyword: str,
    orientation: str,
    min_duration: float,
    pexels_api_key: Optional[str] = None,
    pixabay_api_key: Optional[str] = None,
    width: int = 1920,
    height: int = 1080,
) -> StockClip:
    """Anahtar kelimeye uygun bir stok video indirir. Sağlayıcı sırası: Pexels -> Pixabay -> placeholder."""
    _ensure_cache_dir()
    safe_kw = keyword.replace(" ", "_")

    if pexels_api_key:
        try:
            videos = _pexels_search(keyword, orientation, pexels_api_key)
            videos = [v for v in videos if v.get("duration", 0) >= min_duration]
            if videos:
                video = random.choice(videos)
                file_url = _pexels_pick_file_url(video, width)
                if file_url:
                    out_path = os.path.join(CACHE_DIR, f"pexels_{safe_kw}_{video['id']}.mp4")
                    if not os.path.exists(out_path):
                        _download(file_url, out_path)
                    return StockClip(local_path=out_path, source="pexels", keyword=keyword)
        except Exception as e:
            print(f"[visuals] Pexels hata ({keyword}): {e}")

    if pixabay_api_key:
        try:
            hits = _pixabay_search(keyword, pixabay_api_key)
            if hits:
                hit = random.choice(hits)
                videos = hit.get("videos", {})
                file_url = (videos.get("large") or videos.get("medium") or videos.get("small") or {}).get("url")
                if file_url:
                    out_path = os.path.join(CACHE_DIR, f"pixabay_{safe_kw}_{hit['id']}.mp4")
                    if not os.path.exists(out_path):
                        _download(file_url, out_path)
                    return StockClip(local_path=out_path, source="pixabay", keyword=keyword)
        except Exception as e:
            print(f"[visuals] Pixabay hata ({keyword}): {e}")

    # Fallback: placeholder (anahtarsız yerel test için)
    out_path = os.path.join(CACHE_DIR, f"placeholder_{safe_kw}_{width}x{height}.mp4")
    if not os.path.exists(out_path):
        _placeholder_clip(out_path, width, height, max(min_duration, 6),
                           seed=_stable_seed(keyword) % 1000)
    return StockClip(local_path=out_path, source="placeholder", keyword=keyword)


def build_visual_playlist(
    total_duration: float,
    keyword_pool: List[str],
    orientation: str,
    width: int,
    height: int,
    pexels_api_key: Optional[str] = None,
    pixabay_api_key: Optional[str] = None,
    clip_len: float = 8.0,
) -> List[StockClip]:
    """Toplam süreyi kapsayacak kadar stok klip indirir (sırayla farklı anahtar kelimelerden)."""
    needed = max(1, int(total_duration // clip_len) + 1)
    pool = keyword_pool.copy()
    random.shuffle(pool)
    clips = []
    for i in range(needed):
        kw = pool[i % len(pool)]
        clip = fetch_clip_for_keyword(
            kw, orientation, clip_len,
            pexels_api_key=pexels_api_key, pixabay_api_key=pixabay_api_key,
            width=width, height=height,
        )
        clips.append(clip)
    return clips


if __name__ == "__main__":
    clips = build_visual_playlist(
        total_duration=20,
        keyword_pool=["nature sunrise", "mosque architecture", "starry night sky"],
        orientation="landscape",
        width=1280, height=720,
        pexels_api_key=os.environ.get("PEXELS_API_KEY"),
        pixabay_api_key=os.environ.get("PIXABAY_API_KEY"),
        clip_len=6,
    )
    for c in clips:
        print(c)
