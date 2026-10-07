"""
Video montaj modülü (ffmpeg tabanlı).

Adımlar:
  1) Stok klipleri hedef çözünürlüğe kırp/ölçekle (crop+scale, "cover" mantığı)
  2) Klipleri arka arkaya ekleyip anlatım/kıraat ses süresine denk uzunlukta tek bir
     görsel parça (video-only) oluştur
  3) Ses katmanlarını mixle: ana ses (kıraat/anlatım) + opsiyonel kısık doğa ambiyansı
  4) Altyazıyı (.ass) yak
  5) Short/Long formatına göre çözünürlük ve süre sınırlarını uygula
"""
from __future__ import annotations
import os
import subprocess
import json
from typing import List, Optional

from .visuals import StockClip


def _ffprobe_duration(path: str) -> float:
    cmd = [
        "ffprobe", "-v", "error", "-show_entries", "format=duration",
        "-of", "json", path,
    ]
    out = subprocess.run(cmd, check=True, capture_output=True, text=True).stdout
    return float(json.loads(out)["format"]["duration"])


def _run(cmd: List[str]):
    result = subprocess.run(cmd, capture_output=True, text=True)
    if result.returncode != 0:
        raise RuntimeError(f"ffmpeg hatası:\ncmd={' '.join(cmd)}\nstderr={result.stderr[-4000:]}")
    return result


def build_background_video(
    clips: List[StockClip],
    target_duration: float,
    width: int,
    height: int,
    fps: int,
    out_path: str,
):
    """Stok klipleri hedef süreye ulaşana kadar arka arkaya ekler (cover-crop + loop gerekirse)."""
    tmp_dir = os.path.dirname(out_path)
    os.makedirs(tmp_dir, exist_ok=True)

    # Her klibi normalize et (çözünürlük/fps/pix_fmt eşitle) ki concat sorunsuz olsun
    normalized = []
    acc_duration = 0.0
    i = 0
    while acc_duration < target_duration:
        clip = clips[i % len(clips)]
        norm_path = os.path.join(tmp_dir, f"norm_{i}.mp4")
        vf = (
            f"scale={width}:{height}:force_original_aspect_ratio=increase,"
            f"crop={width}:{height},fps={fps},setsar=1"
        )
        _run([
            "ffmpeg", "-y", "-i", clip.local_path,
            "-vf", vf, "-an", "-c:v", "libx264", "-preset", "veryfast",
            "-crf", "20", norm_path,
        ])
        normalized.append(norm_path)
        acc_duration += _ffprobe_duration(norm_path)
        i += 1

    concat_list_path = os.path.join(tmp_dir, "concat_list.txt")
    with open(concat_list_path, "w") as f:
        for p in normalized:
            f.write(f"file '{os.path.abspath(p)}'\n")

    concat_out = os.path.join(tmp_dir, "concat_raw.mp4")
    _run([
        "ffmpeg", "-y", "-f", "concat", "-safe", "0", "-i", concat_list_path,
        "-c", "copy", concat_out,
    ])

    # Tam olarak hedef süreye kırp
    _run([
        "ffmpeg", "-y", "-i", concat_out, "-t", str(target_duration),
        "-c:v", "libx264", "-preset", "veryfast", "-crf", "20", out_path,
    ])
    return out_path


def mux_audio_video(
    video_path: str,
    narration_audio_path: str,
    out_path: str,
    background_audio_path: Optional[str] = None,
    background_volume_db: float = -22.0,
):
    """Video ile anlatım sesini (ve varsa kısık arka plan ambiyansını) birleştirir."""
    if background_audio_path:
        filter_complex = (
            f"[2:a]volume={background_volume_db}dB,aloop=loop=-1:size=2e9[bg];"
            f"[1:a][bg]amix=inputs=2:duration=first:dropout_transition=2[aout]"
        )
        cmd = [
            "ffmpeg", "-y",
            "-i", video_path,
            "-i", narration_audio_path,
            "-i", background_audio_path,
            "-filter_complex", filter_complex,
            "-map", "0:v", "-map", "[aout]",
            "-c:v", "copy", "-c:a", "aac", "-shortest", out_path,
        ]
    else:
        cmd = [
            "ffmpeg", "-y",
            "-i", video_path,
            "-i", narration_audio_path,
            "-map", "0:v", "-map", "1:a",
            "-c:v", "copy", "-c:a", "aac", "-shortest", out_path,
        ]
    _run(cmd)
    return out_path


def burn_subtitles(
    video_path: str,
    ass_path: str,
    out_path: str,
    fonts_dir: str,
):
    ass_escaped = ass_path.replace(":", "\\:")
    fonts_dir_escaped = fonts_dir.replace(":", "\\:")
    vf = f"subtitles='{ass_escaped}':fontsdir='{fonts_dir_escaped}'"
    _run([
        "ffmpeg", "-y", "-i", video_path,
        "-vf", vf,
        "-c:v", "libx264", "-preset", "medium", "-crf", "19",
        "-c:a", "copy",
        out_path,
    ])
    return out_path


def overlay_logo(
    video_path: str,
    logo_path: str,
    out_path: str,
    width: int,
    height: int,
    position: str = "bottom_right",
    size_ratio: float = 0.16,
    opacity: float = 0.92,
    margin_ratio: float = 0.035,
):
    """Kanal logosunu (saydam arka planlı PNG) videonun SABİT bir köşesine,
    baştan sona görünür şekilde (filigran) bindirir. Her video için aynı
    konum/boyut kullanılır (kullanıcı talebiyle: marka tutarlılığı)."""
    logo_size = int(min(width, height) * size_ratio)
    margin = int(min(width, height) * margin_ratio)

    anchors = {
        "bottom_right": (f"W-w-{margin}", f"H-h-{margin}"),
        "bottom_left": (f"{margin}", f"H-h-{margin}"),
        "top_right": (f"W-w-{margin}", f"{margin}"),
        "top_left": (f"{margin}", f"{margin}"),
    }
    x_expr, y_expr = anchors.get(position, anchors["bottom_right"])

    filter_complex = (
        f"[1:v]scale={logo_size}:{logo_size},format=rgba,"
        f"colorchannelmixer=aa={opacity}[logo];"
        f"[0:v][logo]overlay=x={x_expr}:y={y_expr}[vout]"
    )
    _run([
        "ffmpeg", "-y",
        "-i", video_path,
        "-loop", "1", "-i", logo_path,
        "-filter_complex", filter_complex,
        "-map", "[vout]", "-map", "0:a",
        "-c:v", "libx264", "-preset", "medium", "-crf", "19",
        "-c:a", "copy",
        "-shortest",
        out_path,
    ])
    return out_path


def finalize_video(
    clips: List[StockClip],
    narration_audio_path: str,
    narration_duration: float,
    ass_subtitle_path: str,
    width: int,
    height: int,
    fps: int,
    fonts_dir: str,
    work_dir: str,
    final_out_path: str,
    background_audio_path: Optional[str] = None,
    background_volume_db: float = -22.0,
):
    """Tüm adımları sırayla çalıştırıp nihai mp4'ü üretir."""
    os.makedirs(work_dir, exist_ok=True)
    bg_video = os.path.join(work_dir, "bg_video.mp4")
    build_background_video(clips, narration_duration, width, height, fps, bg_video)

    muxed = os.path.join(work_dir, "muxed.mp4")
    mux_audio_video(bg_video, narration_audio_path, muxed, background_audio_path, background_volume_db)

    burn_subtitles(muxed, ass_subtitle_path, final_out_path, fonts_dir)
    return final_out_path
