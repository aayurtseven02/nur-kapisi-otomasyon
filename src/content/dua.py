"""
Dua içerik modülü.

Kaynak: Hısnu'l Müslim (Müslümanın Kalesi) - açık kaynak Türkçe JSON veri seti
(Muhammed-Turgut/tr_hisnul_muslim_json, GitHub). 273 dua; başlık, Arapça metin,
Türkçe anlam ve klasik kaynak (Buhari/Müslim vb.) bilgisiyle birlikte gelir.

Hiçbir dua metni yapay zeka ile üretilmez.
"""
from __future__ import annotations
import os
import json
import requests
from dataclasses import dataclass
from typing import List

RAW_URL = "https://raw.githubusercontent.com/Muhammed-Turgut/tr_hisnul_muslim_json/master/hisnul_muslim.json"
CACHE_DIR = os.path.join(os.path.dirname(__file__), "..", "..", "assets", "cache")
CACHE_FILE = os.path.join(CACHE_DIR, "hisnul_muslim.json")


@dataclass
class Dua:
    id: str
    title: str
    arabic: str
    turkish: str
    source: str


def _load_all(timeout: int = 30) -> List[dict]:
    os.makedirs(CACHE_DIR, exist_ok=True)
    if not os.path.exists(CACHE_FILE):
        r = requests.get(RAW_URL, timeout=timeout)
        r.raise_for_status()
        # ATOMIK yazma: indirme yarıda kalırsa bozuk JSON cache'te kalıcı
        # olur ve sonraki her çalıştırmada json.load hatası verir.
        tmp_path = f"{CACHE_FILE}.part"
        with open(tmp_path, "wb") as f:
            f.write(r.content)
            f.flush()
            os.fsync(f.fileno())
        os.replace(tmp_path, CACHE_FILE)
    with open(CACHE_FILE, "r", encoding="utf-8") as f:
        return json.load(f)


def get_dua(dua_id) -> Dua:
    data = _load_all()
    for d in data:
        if str(d["id"]) == str(dua_id):
            return Dua(id=d["id"], title=d["title"].strip(), arabic=d["arabic"],
                        turkish=d["turkish"], source=d.get("source", ""))
    raise ValueError(f"Dua bulunamadı: id={dua_id}")


def list_duas() -> List[Dua]:
    data = _load_all()
    return [Dua(id=d["id"], title=d["title"].strip(), arabic=d["arabic"],
                 turkish=d["turkish"], source=d.get("source", "")) for d in data]


if __name__ == "__main__":
    d = get_dua(1)
    print(d.title)
    print(d.turkish)
    print(d.source)
