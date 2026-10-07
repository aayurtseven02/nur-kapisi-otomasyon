"""
Google Cloud Text-to-Speech (Chirp3-HD / WaveNet / Neural2 / Standard) entegrasyonu.

Kimlik doğrulama: Basit bir API anahtarı ile (OAuth/servis hesabı dosyası
GEREKMEZ) -> env: GOOGLE_TTS_API_KEY

Ücretsiz kota (aylık, otomatik yenilenir):
  - Standard sesler: 4.000.000 karakter
  - WaveNet / Neural2 / Chirp3-HD sesler: 1.000.000 karakter

ÖNEMLİ: Google Cloud projesinde "Billing" (faturalandırma) etkinleştirilmiş
olmalı -- ücretsiz kotada kalsanız bile Google bunu zorunlu kılıyor. Kurulum
adımları için README.md > "Google Cloud TTS Kurulumu" bölümüne bakın.

Not: Chirp3-HD sesleri SSML/<mark> zaman damgalama (timepointing) özelliğini
desteklemez. Bu yüzden altyazı senkronizasyonu, metni cümlelere bölüp her
parçayı AYRI AYRI seslendirerek (ve gerçek süresini ölçerek) yapılır -- bu
sayede her cümlenin başlangıç/bitiş zamanı gerçek ses süresinden elde edilir,
tahmini/yaklaşık değildir.
"""
from __future__ import annotations
import base64
import os

import requests
from typing import List

from .tts import SubtitleCue, split_into_sentences, strip_suffix_apostrophes
from . import audio_tools

API_URL = "https://texttospeech.googleapis.com/v1/text:synthesize"


def _synthesize_chunk(text: str, api_key: str, voice_name: str, language_code: str = "tr-TR",
                       speaking_rate: float = 1.0, timeout: int = 30) -> bytes:
    body = {
        "input": {"text": text},
        "voice": {"languageCode": language_code, "name": voice_name},
        "audioConfig": {"audioEncoding": "MP3", "speakingRate": speaking_rate},
    }
    r = requests.post(f"{API_URL}?key={api_key}", json=body, timeout=timeout)
    if r.status_code != 200:
        raise RuntimeError(f"Google Cloud TTS hatası ({r.status_code}): {r.text[:500]}")
    audio_b64 = r.json()["audioContent"]
    return base64.b64decode(audio_b64)


def narrate(
    text: str,
    out_path: str,
    api_key: str,
    voice_name: str = "tr-TR-Chirp3-HD-Charon",
    language_code: str = "tr-TR",
    speaking_rate: float = 1.0,
    work_dir: str = "/tmp/google_tts_chunks",
) -> List[SubtitleCue]:
    """Metni cümle cümle Google Cloud TTS ile seslendirir, her cümlenin gerçek
    süresini ölçerek birebir senkron altyazı zaman damgaları üretir, sonra
    tüm parçaları tek bir ses dosyasında birleştirir."""
    os.makedirs(work_dir, exist_ok=True)
    sentences = split_into_sentences(text)
    if not sentences:
        sentences = [text]

    chunk_paths = []
    cues: List[SubtitleCue] = []
    t = 0.0
    for i, sentence in enumerate(sentences):
        # Seslendirmeye giden metinden SADECE özel isim+ek kesme işaretleri
        # kaldırılır (ör. "Hattâb'ın" -> "Hattâbın"); altyazıda gösterilecek
        # `sentence` (orijinal, doğru imlalı) değişmeden kalır.
        audio_bytes = _synthesize_chunk(
            strip_suffix_apostrophes(sentence), api_key, voice_name, language_code,
            speaking_rate=speaking_rate,
        )
        chunk_path = os.path.join(work_dir, f"chunk_{i}.mp3")
        with open(chunk_path, "wb") as f:
            f.write(audio_bytes)
        duration = audio_tools._ffprobe_duration(chunk_path)
        cues.append(SubtitleCue(start=t, end=t + duration, text=sentence))
        chunk_paths.append(chunk_path)
        t += duration

    audio_tools.concat_audio_files(chunk_paths, out_path)
    return cues


# Türkçe (tr-TR) Chirp3-HD erkek sesler (en doğal nesil, 1M karakter/ay ücretsiz)
MALE_VOICES = [
    "tr-TR-Chirp3-HD-Achird", "tr-TR-Chirp3-HD-Algenib", "tr-TR-Chirp3-HD-Algieba",
    "tr-TR-Chirp3-HD-Alnilam", "tr-TR-Chirp3-HD-Charon", "tr-TR-Chirp3-HD-Enceladus",
    "tr-TR-Chirp3-HD-Fenrir", "tr-TR-Chirp3-HD-Iapetus", "tr-TR-Chirp3-HD-Orus",
    "tr-TR-Chirp3-HD-Puck", "tr-TR-Chirp3-HD-Rasalgethi", "tr-TR-Chirp3-HD-Sadachbia",
    "tr-TR-Chirp3-HD-Sadaltager", "tr-TR-Chirp3-HD-Schedar", "tr-TR-Chirp3-HD-Umbriel",
    "tr-TR-Chirp3-HD-Zubenelgenubi",
]

FEMALE_VOICES = [
    "tr-TR-Chirp3-HD-Achernar", "tr-TR-Chirp3-HD-Aoede", "tr-TR-Chirp3-HD-Autonoe",
    "tr-TR-Chirp3-HD-Callirrhoe", "tr-TR-Chirp3-HD-Despina", "tr-TR-Chirp3-HD-Erinome",
    "tr-TR-Chirp3-HD-Gacrux", "tr-TR-Chirp3-HD-Kore", "tr-TR-Chirp3-HD-Laomedeia",
    "tr-TR-Chirp3-HD-Leda", "tr-TR-Chirp3-HD-Pulcherrima", "tr-TR-Chirp3-HD-Sulafat",
    "tr-TR-Chirp3-HD-Vindemiatrix", "tr-TR-Chirp3-HD-Zephyr",
]


if __name__ == "__main__":
    key = os.environ["GOOGLE_TTS_API_KEY"]
    cues = narrate(
        "Peygamber Efendimiz şöyle buyurmuştur. Ameller niyetlere göredir ve "
        "herkes için niyet ettiğinin karşılığı vardır.",
        "/tmp/google_tts_test.mp3", key,
    )
    for c in cues:
        print(f"{c.start:.2f}-{c.end:.2f}: {c.text}")
