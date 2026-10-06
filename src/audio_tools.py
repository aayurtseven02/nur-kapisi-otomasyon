"""
Ses yardımcı fonksiyonları: ayet seslerini birleştirme, süre ölçme,
arka plan doğa ambiyansı seçme/üretme.

Arka plan sesi politikası (kullanıcı talebiyle): enstrümantal müzik YOK.
Önce assets/library/nature/ klasöründeki (varsa, kullanıcının kendi royalty-free
doğa sesi dosyaları) rastgele bir dosya kullanılır. Hiç dosya yoksa, tamamen
programatik/sentetik, lisans sorunu taşımayan hafif bir "rüzgar/ambiyans"
sesi üretilir (ffmpeg anoisesrc), bu sayede sistem ek bir API anahtarı veya
indirme olmadan da çalışabilir.
"""
from __future__ import annotations
import os
import glob
import random
import subprocess
import json

LIBRARY_DIR = os.path.join(os.path.dirname(__file__), "..", "assets", "library", "nature")
GENERATED_DIR = os.path.join(os.path.dirname(__file__), "..", "assets", "cache", "audio")


def _ffprobe_duration(path: str) -> float:
    cmd = ["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "json", path]
    out = subprocess.run(cmd, check=True, capture_output=True, text=True).stdout
    return float(json.loads(out)["format"]["duration"])


def concat_audio_files(file_paths, out_path: str):
    """Birden fazla ses dosyasını (örn. ardışık ayet kıraatleri) tek dosyada birleştirir."""
    os.makedirs(os.path.dirname(out_path) or ".", exist_ok=True)
    inputs = []
    filter_parts = []
    for i, p in enumerate(file_paths):
        inputs += ["-i", p]
        filter_parts.append(f"[{i}:a]")
    filter_complex = "".join(filter_parts) + f"concat=n={len(file_paths)}:v=0:a=1[aout]"
    cmd = ["ffmpeg", "-y", *inputs, "-filter_complex", filter_complex, "-map", "[aout]", out_path]
    subprocess.run(cmd, check=True, capture_output=True)
    return out_path


def get_durations(file_paths):
    return [_ffprobe_duration(p) for p in file_paths]


def _synth_wind(duration: float, out_path: str):
    """Hafif rüzgar/ambiyans benzeri sentetik ses: pembe gürültü + alçak geçiren filtre + hafif tremolo."""
    filt = (
        "anoisesrc=color=pink:amplitude=0.4,"
        "lowpass=f=700,"
        "tremolo=f=0.2:d=0.3"
    )
    cmd = ["ffmpeg", "-y", "-f", "lavfi", "-i", filt, "-t", str(max(duration, 3)), out_path]
    subprocess.run(cmd, check=True, capture_output=True)


def _synth_water(duration: float, out_path: str):
    """Su/dalga sesine benzeyen sentetik ambiyans: iki katman gürültüden oluşur --
    (1) orta-yüksek frekans 'köpük/şırıltı' katmanı ve (2) alçak frekans 'dalga
    vuruşu' katmanı; ikisi de yavaş genlik modülasyonuyla (tremolo, ~12 sn
    periyotlu) dalga gelip-gitme hissi verir. Lisans sorunu taşımaz (tamamen
    programatik üretim), internet/API anahtarı gerekmez."""
    dur = max(duration, 3)
    cmd = [
        "ffmpeg", "-y",
        "-f", "lavfi", "-i", "anoisesrc=color=white:amplitude=0.6:sample_rate=44100",
        "-f", "lavfi", "-i", "anoisesrc=color=brown:amplitude=0.5:sample_rate=44100",
        "-filter_complex",
        "[0:a]highpass=f=500,lowpass=f=4000,tremolo=f=0.1:d=0.6,volume=0.6[foam];"
        "[1:a]lowpass=f=250,tremolo=f=0.13:d=0.5,volume=0.8[swell];"
        "[foam][swell]amix=inputs=2:duration=first:normalize=0[aout]",
        "-map", "[aout]", "-t", str(dur), out_path,
    ]
    subprocess.run(cmd, check=True, capture_output=True)


def get_background_ambience(duration: float, ambience_type: str = "water") -> str:
    """Doğa ambiyansı dosyası döndürür: önce kullanıcı kütüphanesinden (varsa),
    yoksa sentetik üretilmiş ('water' = su/dalga sesi, 'wind' = rüzgar sesi)."""
    os.makedirs(GENERATED_DIR, exist_ok=True)
    candidates = []
    if os.path.isdir(LIBRARY_DIR):
        for ext in ("mp3", "wav", "ogg", "m4a"):
            candidates += glob.glob(os.path.join(LIBRARY_DIR, f"*.{ext}"))
    if candidates:
        return random.choice(candidates)

    out_path = os.path.join(GENERATED_DIR, f"synthetic_{ambience_type}_{int(duration)}.wav")
    if not os.path.exists(out_path):
        if ambience_type == "water":
            _synth_water(duration, out_path)
        else:
            _synth_wind(duration, out_path)
    return out_path



if __name__ == "__main__":
    p = get_background_ambience(10)
    print("ambiyans:", p, _ffprobe_duration(p))
