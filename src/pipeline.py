"""
Ana orkestrasyon modülü: schedule.yaml'daki TEK bir video görevini alır,
içeriği getirir, sesi/altyazıyı/görselleri hazırlar, videoyu üretir,
başlık/açıklama/thumbnail'i oluşturur ve (varsa YouTube kimlik bilgileri ile)
YouTube'a yükler.
"""
from __future__ import annotations
import os
import datetime
import yaml

from .content import quran, hadith, dua
from . import tts, subtitles, visuals, video_builder, thumbnail, metadata, audio_tools

ROOT = os.path.join(os.path.dirname(__file__), "..")
SETTINGS_PATH = os.path.join(ROOT, "config", "settings.yaml")


def load_settings():
    with open(SETTINGS_PATH, "r", encoding="utf-8") as f:
        return yaml.safe_load(f)


def _dims(settings, is_short: bool):
    key = "short_form" if is_short else "long_form"
    v = settings["video"][key]
    return v["width"], v["height"], v["fps"]


def _narrate_text(text: str, narration_path: str, settings: dict):
    """Ayarlarda seçilen motora göre (edge-tts veya Google Cloud Chirp3-HD)
    metni seslendirir ve altyazı zaman damgalarını (SubtitleCue listesi) döndürür."""
    engine = settings["tts"].get("engine", "edge")
    if engine == "google_cloud":
        from . import tts_google_cloud
        api_key = os.environ.get("GOOGLE_TTS_API_KEY")
        if not api_key:
            raise RuntimeError(
                "GOOGLE_TTS_API_KEY ortam değişkeni bulunamadı. "
                "config/settings.yaml içinde tts.engine: 'google_cloud' seçili ama "
                "API anahtarı tanımlı değil. README > 'Google Cloud TTS Kurulumu' "
                "bölümüne bakın ya da tts.engine değerini 'edge' yapın."
            )
        gc = settings["tts"]["google_cloud"]
        return tts_google_cloud.narrate(
            text, narration_path, api_key=api_key,
            voice_name=gc["voice_name"], language_code=gc["language_code"],
            speaking_rate=gc.get("speaking_rate", 1.0),
        )
    else:
        voice = settings["tts"][settings["tts"]["default_voice"]]
        return tts.split_long_cues(tts.narrate(
            text, narration_path, voice=voice,
            rate=settings["tts"]["rate"], pitch=settings["tts"]["pitch"],
        ))


def _build_sure_assets(entry, settings, work_dir, is_short):
    ref = entry["ref"]
    reciter = ref.get("reciter", settings["quran"]["default_reciter"])
    ayahs = quran.get_surah_ayahs(
        ref["surah_number"], ref["ayah_start"], ref["ayah_end"],
        reciter=reciter, translation_edition=settings["quran"]["translation_edition"],
    )
    audio_paths = [quran.download_ayah_audio(a) for a in ayahs]
    durations = audio_tools.get_durations(audio_paths)

    narration_path = os.path.join(work_dir, "narration.mp3")
    audio_tools.concat_audio_files(audio_paths, narration_path)

    cues = []
    t = 0.0
    width, height, _ = _dims(settings, is_short)
    ar_size = int(height * 0.06)
    tr_size = int(height * 0.05)
    for ayah, dur in zip(ayahs, durations):
        text = (
            f"{{\\fnAmiri\\fs{ar_size}}}{ayah.arabic_text}"
            f"\\N{{\\fnNoto Sans\\fs{tr_size}}}{ayah.translation_text}"
        )
        cues.append(subtitles.SubtitleCue(start=t, end=t + dur, text=text))
        t += dur

    total_duration = t
    surah_name = quran.surah_display_name_tr(ref["surah_number"])
    meta = metadata.build_sure_metadata(
        surah_name_tr=surah_name,
        ayah_start=ref["ayah_start"], ayah_end=ref["ayah_end"],
        reciter_display=reciter, is_short=is_short,
        channel_name=settings["channel"]["name"],
    )
    return {
        "narration_path": narration_path,
        "cues": cues,
        "total_duration": total_duration,
        "metadata": meta,
        "keyword_pool": settings["stock_video"]["keyword_pool"],
    }


def _build_hadis_assets(entry, settings, work_dir, is_short):
    ref = entry["ref"]
    h = hadith.get_hadith(ref["collection"], ref["hadithnumber"])
    narration_path = os.path.join(work_dir, "narration.mp3")
    narr_cues = _narrate_text(h.text, narration_path, settings)
    total_duration = narr_cues[-1].end if narr_cues else audio_tools._ffprobe_duration(narration_path)

    meta = metadata.build_hadis_metadata(
        collection_display=h.collection_display, hadith_number=h.hadith_number,
        hint=entry.get("title_hint", ""), is_short=is_short,
        channel_name=settings["channel"]["name"],
    )
    return {
        "narration_path": narration_path,
        "cues": narr_cues,
        "total_duration": total_duration,
        "metadata": meta,
        "keyword_pool": settings["stock_video"]["keyword_pool"],
    }


def _build_dua_assets(entry, settings, work_dir, is_short):
    ref = entry["ref"]
    d = dua.get_dua(ref["dua_id"])
    narration_path = os.path.join(work_dir, "narration.mp3")
    narr_cues = _narrate_text(d.turkish, narration_path, settings)
    total_duration = narr_cues[-1].end if narr_cues else audio_tools._ffprobe_duration(narration_path)

    meta = metadata.build_dua_metadata(
        dua_title=d.title, source=d.source, is_short=is_short,
        channel_name=settings["channel"]["name"],
    )
    return {
        "narration_path": narration_path,
        "cues": narr_cues,
        "total_duration": total_duration,
        "metadata": meta,
        "keyword_pool": settings["stock_video"]["keyword_pool"],
    }


def _build_hikaye_assets(entry, settings, work_dir, is_short):
    ref = entry["ref"]
    text_file = os.path.join(ROOT, ref["text_file"])
    if not os.path.exists(text_file):
        raise FileNotFoundError(
            f"Hikaye metni bulunamadı: {text_file}\n"
            f"Dini hikayelerin DOĞRULUĞU önemli olduğundan bu metinler yapay zeka ile "
            f"üretilmez; lütfen content_library/hikayeler/ altına güvenilir bir "
            f"kaynaktan derlediğiniz metni koyun."
        )
    with open(text_file, "r", encoding="utf-8") as f:
        text = f.read()

    narration_path = os.path.join(work_dir, "narration.mp3")
    narr_cues = _narrate_text(text, narration_path, settings)
    total_duration = narr_cues[-1].end if narr_cues else audio_tools._ffprobe_duration(narration_path)

    meta = metadata.build_hikaye_metadata(
        hint=entry.get("title_hint", ""), is_short=is_short,
        channel_name=settings["channel"]["name"],
    )
    return {
        "narration_path": narration_path,
        "cues": narr_cues,
        "total_duration": total_duration,
        "metadata": meta,
        "keyword_pool": settings["stock_video"]["keyword_pool"],
    }


BUILDERS = {
    "sure": _build_sure_assets,
    "hadis": _build_hadis_assets,
    "dua": _build_dua_assets,
    "hikaye": _build_hikaye_assets,
}

# Video üstü başlıkta ana başlığın ÜSTÜNDE gösterilen "seri etiketi" (kullanıcı
# talebiyle: ör. "PEYGAMBERİMİZDEN HADİSLER #1" + altında hadisin adı).
SERIES_LABELS = {
    "hadis": "Peygamberimizden Hadisler",
    "dua": "Dualarla Huzur",
    "sure": "Kur'an-ı Kerim'den",
    "hikaye": "Dini Kıssalar",
}


def _series_label(entry: dict, schedule_entries: list | None) -> str:
    """entry'nin türüne göre seri adını ve sırasını hesaplar (ör. 'Peygamberimizden
    Hadisler #3'). Sıra numarası, schedule.yaml'daki AYNI TÜRDEKİ girdiler arasında
    publish_at'e göre kaçıncı olduğuna bakılarak belirlenir (ayrı bir sayaç
    dosyasına gerek kalmadan, schedule.yaml tek doğruluk kaynağı olarak kalır)."""
    label = SERIES_LABELS.get(entry.get("type"))
    if not label or not schedule_entries:
        return ""
    same_type = [e for e in schedule_entries if e.get("type") == entry.get("type")]
    same_type.sort(key=lambda e: (e.get("publish_at", ""), e.get("id", "")))
    try:
        rank = next(i for i, e in enumerate(same_type, start=1) if e.get("id") == entry.get("id"))
    except StopIteration:
        rank = len(same_type)
    return f"{label} #{rank}"


def run_entry(entry: dict, settings: dict, output_dir: str, upload: bool = False,
              schedule_entries: list | None = None):
    """Tek bir schedule.yaml girdisini uçtan uca işler. upload=False ise sadece
    yerel mp4+thumbnail üretir (test/önizleme modu). schedule_entries verilirse
    (tüm programın listesi), video üstü başlıkta "seri #N" etiketi hesaplanır."""
    is_short = entry["format"] == "short"
    work_dir = os.path.join(output_dir, entry["id"])
    os.makedirs(work_dir, exist_ok=True)

    builder = BUILDERS[entry["type"]]
    assets = builder(entry, settings, work_dir, is_short)

    width, height, fps = _dims(settings, is_short)
    if is_short and assets["total_duration"] > settings["video"]["max_short_seconds"]:
        print(f"[pipeline] UYARI: {entry['id']} süresi short limitini aşıyor "
              f"({assets['total_duration']:.1f}s > {settings['video']['max_short_seconds']}s)")

    title_cfg = settings.get("title_overlay", {})
    title_text = ""
    title_max_chars = 20
    title_font_size = 0
    if title_cfg.get("enabled", False):
        raw_title = entry.get("title_hint") or assets["metadata"].title
        raw_title = raw_title.split(" | ")[0].replace("#shorts", "").strip()
        title_text = metadata.turkish_upper(raw_title)
        title_max_chars = title_cfg.get(
            "max_chars_per_line_short" if is_short else "max_chars_per_line_long", 20
        )
        title_font_size = title_cfg.get(
            "font_size_short" if is_short else "font_size_long", 0
        )

    series_text = metadata.turkish_upper(_series_label(entry, schedule_entries)) if title_cfg.get("enabled", False) else ""

    ass_path = os.path.join(work_dir, "subs.ass")
    subtitles.write_ass(
        assets["cues"], ass_path, res_x=width, res_y=height,
        font_name=settings["subtitles"]["font_family"],
        font_size=settings["subtitles"]["font_size_short" if is_short else "font_size_long"],
        margin_v=int(height * 0.10),
        title_text=title_text,
        title_duration=assets["total_duration"],
        title_font_name=settings["subtitles"]["font_family"],
        title_font_size=title_font_size,
        title_margin_v=int(height * 0.05),
        title_max_chars=title_max_chars,
        series_text=series_text,
    )

    clips = visuals.build_visual_playlist(
        total_duration=assets["total_duration"],
        keyword_pool=assets["keyword_pool"],
        orientation="portrait" if is_short else "landscape",
        width=width, height=height,
        pexels_api_key=os.environ.get("PEXELS_API_KEY"),
        pixabay_api_key=os.environ.get("PIXABAY_API_KEY"),
    )

    bg_mode = entry.get("background_mode", settings["audio"]["background_mode"])
    bg_audio_path = None
    if bg_mode == "nature":
        bg_audio_path = audio_tools.get_background_ambience(
            assets["total_duration"], ambience_type=settings["audio"].get("ambience_type", "water")
        )

    final_video_path = os.path.join(output_dir, f"{entry['id']}.mp4")
    video_builder.finalize_video(
        clips=clips,
        narration_audio_path=assets["narration_path"],
        narration_duration=assets["total_duration"],
        ass_subtitle_path=ass_path,
        width=width, height=height, fps=fps,
        fonts_dir=os.path.join(ROOT, settings["subtitles"]["fonts_dir"]),
        work_dir=work_dir,
        final_out_path=final_video_path,
        background_audio_path=bg_audio_path,
        background_volume_db=settings["audio"]["background_volume_db"],
    )

    branding = settings.get("branding", {})
    logo_path = os.path.join(ROOT, branding.get("logo_path", "")) if branding.get("logo_path") else ""
    if logo_path and os.path.exists(logo_path):
        logo_out = os.path.join(work_dir, "with_logo.mp4")
        video_builder.overlay_logo(
            final_video_path, logo_path, logo_out, width=width, height=height,
            position=branding.get("position", "bottom_right"),
            size_ratio=branding.get("video_size_ratio", 0.16),
            opacity=branding.get("video_opacity", 0.92),
            margin_ratio=branding.get("video_margin_ratio", 0.035),
        )
        os.replace(logo_out, final_video_path)

    frame_path = os.path.join(work_dir, "frame.jpg")
    thumbnail.extract_frame(final_video_path, frame_path, timestamp=min(1.5, assets["total_duration"] / 2))
    thumb_path = os.path.join(output_dir, f"{entry['id']}_thumb.jpg")

    TYPE_SUBTITLE = {
        "sure": "Kur'an-ı Kerim", "hadis": "Hadis-i Şerif",
        "dua": "Dua", "hikaye": "Dini Kıssa",
    }
    TYPE_ACCENT = {"sure": "#Kuran", "hadis": "#Hadis", "dua": "#Dua", "hikaye": "#Kıssa"}

    full_title = assets["metadata"].title
    main_part, _, rest_part = full_title.partition(" | ")
    rest_part = rest_part.replace("#shorts", "").strip()
    raw_title = entry.get("title_hint") or main_part
    raw_title = raw_title.split(" | ")[0].replace("#shorts", "").strip()

    thumbnail.generate_thumbnail(
        frame_path, metadata.turkish_upper(raw_title),
        thumb_path, width=1280, height=720,
        subtitle_text=TYPE_SUBTITLE.get(entry["type"], ""),
        extra_text=f"({rest_part})" if rest_part else "",
        accent_text=TYPE_ACCENT.get(entry["type"], ""),
        font_path=os.path.join(ROOT, "assets", "fonts", "NotoSans-Variable.ttf"),
        arabic_font_path=os.path.join(ROOT, "assets", "fonts", "Amiri-Regular.ttf"),
        logo_path=logo_path,
        logo_position=branding.get("position", "bottom_right"),
        logo_size_ratio=branding.get("thumb_size_ratio", 0.17),
        logo_margin_ratio=branding.get("thumb_margin_ratio", 0.03),
    )

    result = {"video_path": final_video_path, "thumbnail_path": thumb_path, "metadata": assets["metadata"]}

    if upload:
        from . import youtube_upload
        youtube = youtube_upload.get_authenticated_service(
            refresh_token=os.environ.get("YT_REFRESH_TOKEN"),
            client_id=os.environ.get("YT_CLIENT_ID"),
            client_secret=os.environ.get("YT_CLIENT_SECRET"),
        )
        publish_at = entry["publish_at"]
        dt = datetime.datetime.fromisoformat(publish_at)
        publish_at_utc = dt.astimezone(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")

        upload_result = youtube_upload.upload_video(
            youtube, final_video_path,
            title=assets["metadata"].title,
            description=assets["metadata"].description,
            tags=assets["metadata"].tags,
            category_id=settings["youtube"]["category_id"],
            privacy_status="private",
            publish_at_iso_utc=publish_at_utc,
            made_for_kids=settings["youtube"]["made_for_kids"],
            thumbnail_path=thumb_path,
        )
        result["youtube"] = upload_result

    return result


if __name__ == "__main__":
    import sys
    settings = load_settings()
    with open(os.path.join(ROOT, "config", "schedule.yaml"), "r", encoding="utf-8") as f:
        schedule = yaml.safe_load(f)
    entry_id = sys.argv[1] if len(sys.argv) > 1 else schedule["videos"][1]["id"]
    entry = next(v for v in schedule["videos"] if v["id"] == entry_id)
    res = run_entry(entry, settings, os.path.join(ROOT, "output"), upload=False,
                     schedule_entries=schedule["videos"])
    print(res["video_path"])
    print(res["thumbnail_path"])
    print(res["metadata"].title)
