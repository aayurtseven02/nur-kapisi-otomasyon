"""
Seslendirme (TTS) modülü.

Motor: edge-tts (MIT lisanslı açık kaynak kütüphane; Microsoft Edge'in ücretsiz
bulut TTS servisini kullanır, API anahtarı gerekmez, pratikte sınırsız/ücretsizdir).

Kur'an ayetleri İÇİN bu modül KULLANILMAZ -> onlarda gerçek hafız ses kaydı kullanılır
(bkz content/quran.py). Bu modül hadis, dua anlamı ve hikaye anlatımı gibi metinleri
seslendirmek içindir.

Ayrıca edge-tts'in "SentenceBoundary" olaylarından faydalanarak, TTS'e verilen
metinle BİREBİR örtüşen, kelime kaydırma/transkripsiyon hatası riski taşımayan
altyazı zamanlama bilgisi üretir (Whisper gibi bir dış transkripsiyon modeline
gerek kalmaz).
"""
from __future__ import annotations
import asyncio
import re
from dataclasses import dataclass
from typing import List

import edge_tts

TICKS_PER_SECOND = 10_000_000  # edge-tts offset/duration birimi: 100ns


@dataclass
class SubtitleCue:
    start: float   # saniye
    end: float     # saniye
    text: str


def split_into_sentences(text: str) -> List[str]:
    """Basit, bağımlılıksız cümle bölücü (Türkçe noktalama için yeterli)."""
    text = re.sub(r"\s+", " ", text).strip()
    parts = re.split(r"(?<=[.!?…:])\s+", text)
    return [p.strip() for p in parts if p.strip()]


_APOSTROPHE_RE = re.compile(r"[’'‘´`]")


def strip_suffix_apostrophes(text: str) -> str:
    """TTS motorları Türkçe imla kuralı gereği özel isimlere eklenen kesme
    işaretini (ör. "Hattâb'ın", "İstanbul'da") görünce aradaki eki ayrı bir
    kelimeymiş gibi, arada anlamsız bir duraklamayla okuyor (ör. "Hattab...ın").
    Kesme işaretini kaldırıp eki kelimeye bitiştirmek ("Hattâbın") TTS'in
    doğru/akıcı okumasını sağlıyor. SADECE SESLENDİRME girdisine uygulanır;
    altyazıda gösterilen metin doğru Türkçe imla (kesme işaretli) ile kalır."""
    return _APOSTROPHE_RE.sub("", text)


async def _synthesize(text: str, voice: str, out_path: str, rate: str = "+0%", pitch: str = "+0Hz"):
    communicate = edge_tts.Communicate(text, voice, rate=rate, pitch=pitch)
    cues: List[SubtitleCue] = []
    with open(out_path, "wb") as f:
        async for chunk in communicate.stream():
            if chunk["type"] == "audio":
                f.write(chunk["data"])
            elif chunk["type"] == "SentenceBoundary":
                start = chunk["offset"] / TICKS_PER_SECOND
                end = (chunk["offset"] + chunk["duration"]) / TICKS_PER_SECOND
                cues.append(SubtitleCue(start=start, end=end, text=chunk["text"]))
    return cues


def narrate(text: str, out_path: str, voice: str = "tr-TR-AhmetNeural",
            rate: str = "+0%", pitch: str = "+0Hz") -> List[SubtitleCue]:
    """Metni seslendirir, mp3 olarak kaydeder ve altyazı zaman bilgisini döndürür."""
    return asyncio.run(_synthesize(strip_suffix_apostrophes(text), voice, out_path, rate, pitch))


_CLAUSE_SPLIT_RE = re.compile(r"(?<=[,;:])\s+")


def split_long_cues(cues: List[SubtitleCue], max_chars: int = 70) -> List[SubtitleCue]:
    """Uzun cümle sınırlı (SentenceBoundary) cue'ları, ekranda tek seferde daha az
    metin görünmesi için virgül/noktalı virgül/iki nokta gibi doğal duraklama
    noktalarından böler. Süre, alt parçalar arasında karakter sayısına göre
    orantılı paylaştırılır (sesteki gerçek duraklamalarla yaklaşık örtüşür)."""
    result: List[SubtitleCue] = []
    for cue in cues:
        if len(cue.text) <= max_chars:
            result.append(cue)
            continue
        parts = [p for p in _CLAUSE_SPLIT_RE.split(cue.text) if p.strip()]
        if len(parts) <= 1:
            # Virgül yoksa kelime bazlı eşit böl
            words = cue.text.split(" ")
            mid = len(words) // 2
            parts = [" ".join(words[:mid]), " ".join(words[mid:])]
        total_chars = sum(len(p) for p in parts) or 1
        duration = cue.end - cue.start
        t = cue.start
        for p in parts:
            share = len(p) / total_chars
            seg_dur = duration * share
            result.append(SubtitleCue(start=t, end=t + seg_dur, text=p.strip()))
            t += seg_dur
    return result


if __name__ == "__main__":
    cues = narrate(
        "Peygamber Efendimiz şöyle buyurmuştur. Ameller niyetlere göredir. "
        "Herkes için niyet ettiğinin karşılığı vardır.",
        "/tmp/narration_test.mp3",
    )
    for c in cues:
        print(f"{c.start:.2f}-{c.end:.2f}: {c.text}")
