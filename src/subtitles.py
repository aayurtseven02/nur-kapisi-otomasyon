"""
Altyazı üretim modülü. TTS'ten gelen zaman damgalı cümlelerden (SubtitleCue)
hem .srt hem de ffmpeg ile yakılacak stil bilgisi içeren .ass dosyası üretir.
"""
from __future__ import annotations
from typing import List
from .tts import SubtitleCue


def _fmt_srt_time(t: float) -> str:
    """Saniyeyi SRT zaman biçimine çevirir.

    Not: ``round`` sonucu 1000 milisaniyeye çıkabilir (örn. 1.9996 sn).
    Eski kod bunu "00:00:01,1000" olarak yazıp GEÇERSİZ bir SRT üretiyordu;
    bu yüzden taşma bir üst birime devredilir.
    """
    total_ms = int(round(t * 1000))
    ms = total_ms % 1000
    total_s = total_ms // 1000
    h = total_s // 3600
    m = (total_s % 3600) // 60
    s = total_s % 60
    return f"{h:02d}:{m:02d}:{s:02d},{ms:03d}"


def write_srt(cues: List[SubtitleCue], out_path: str):
    lines = []
    for i, c in enumerate(cues, start=1):
        lines.append(str(i))
        lines.append(f"{_fmt_srt_time(c.start)} --> {_fmt_srt_time(c.end)}")
        lines.append(c.text)
        lines.append("")
    with open(out_path, "w", encoding="utf-8") as f:
        f.write("\n".join(lines))
    return out_path


def _fmt_ass_time(t: float) -> str:
    """Saniyeyi ASS zaman biçimine (h:mm:ss.cc) çevirir.

    Santisaniye yuvarlaması 100'e çıkabilir (örn. 1.999 sn); bu durumda
    "0:00:01.100" gibi geçersiz bir değer üretilmesin diye taşma bir üst
    birime devredilir.
    """
    total_cs = int(round(t * 100))
    cs = total_cs % 100
    total_s = total_cs // 100
    h = total_s // 3600
    m = (total_s % 3600) // 60
    s = total_s % 60
    return f"{h:01d}:{m:02d}:{s:02d}.{cs:02d}"


ASS_HEADER_TEMPLATE = """[Script Info]
ScriptType: v4.00+
PlayResX: {res_x}
PlayResY: {res_y}
WrapStyle: 0
ScaledBorderAndShadow: yes

[V4+ Styles]
Format: Name, Fontname, Fontsize, PrimaryColour, OutlineColour, BackColour, Bold, Italic, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding
Style: Default,{font_name},{font_size},{primary_color},{outline_color},&H00000000,1,0,1,3,0,{alignment},40,40,{margin_v},1
Style: Title,{title_font_name},{title_font_size},{title_color},&H00000000,&H90000000,1,0,3,4,0,8,{title_margin_h},{title_margin_h},{title_margin_v},1

[Events]
Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
"""


def _wrap_text_by_chars(text: str, max_chars: int) -> str:
    """Uzun başlıkları ekrana sığacak şekilde kelime sınırından böler (\\N ile)."""
    words = text.split(" ")
    lines: List[str] = []
    current = ""
    for w in words:
        candidate = (current + " " + w).strip()
        if len(candidate) > max_chars and current:
            lines.append(current)
            current = w
        else:
            current = candidate
    if current:
        lines.append(current)
    return "\\N".join(lines)


def _rgb_to_ass(rgb) -> str:
    """RGB (r,g,b) -> ASS satır-içi renk kodu (\\c etiketi için &HBBGGRR& formatı)."""
    r, g, b = rgb
    return f"&H{b:02X}{g:02X}{r:02X}&"


def write_ass(
    cues: List[SubtitleCue],
    out_path: str,
    res_x: int = 1920,
    res_y: int = 1080,
    font_name: str = "Noto Sans",
    font_size: int = 54,
    primary_color: str = "&H00FFFFFF",
    outline_color: str = "&H00000000",
    alignment: int = 2,   # 2 = alt orta
    margin_v: int = 80,
    title_text: str = "",
    title_duration: float = 0.0,
    title_font_name: str = "Noto Sans",
    title_font_size: int = 0,
    title_color: str = "&H00FFFFFF",
    title_margin_v: int = 50,
    title_max_chars: int = 20,
    series_text: str = "",
    series_color_rgb=(255, 214, 120),
    ornament_color_rgb=(214, 170, 90),
):
    """Altyazı (.ass) dosyasını üretir. title_text verilirse, videonun en üstünde
    (alignment=8, top-center) başından sonuna kadar görünen BÜYÜK HARF bir başlık
    bindirmesi (opak kutu arkaplanlı) de ekler. series_text verilirse (ör.
    "PEYGAMBERİMİZDEN HADİSLER #1"), ana başlığın ÜSTÜNDE daha küçük, altın
    renkli bir "seri etiketi" satırı + aralarına süsleme (✦) eklenir."""
    if not title_font_size:
        title_font_size = int(font_size * 1.15)
    header = ASS_HEADER_TEMPLATE.format(
        res_x=res_x, res_y=res_y, font_name=font_name, font_size=font_size,
        primary_color=primary_color, outline_color=outline_color,
        alignment=alignment, margin_v=margin_v,
        title_font_name=title_font_name, title_font_size=title_font_size,
        title_color=title_color, title_margin_h=50, title_margin_v=title_margin_v,
    )
    events = []
    if title_text and title_duration > 0:
        wrapped = _wrap_text_by_chars(title_text, title_max_chars)
        if series_text:
            kicker_fs = int(title_font_size * 1.08)
            orn_fs = int(title_font_size * 0.46)
            series_color = _rgb_to_ass(series_color_rgb)
            orn_color = _rgb_to_ass(ornament_color_rgb)
            title_block = (
                f"{{\\fs{kicker_fs}\\c{series_color}\\b1}}{series_text}{{\\r}}"
                f"\\N{{\\fs{orn_fs}\\c{orn_color}}}\u2726 \u2726 \u2726{{\\r}}"
                f"\\N{{\\fs{title_font_size}}}{wrapped}"
            )
        else:
            title_block = wrapped
        events.append(
            f"Dialogue: 0,{_fmt_ass_time(0.0)},{_fmt_ass_time(title_duration)},Title,,0,0,0,,{title_block}"
        )
    for c in cues:
        text = c.text.replace("\n", "\\N")
        events.append(
            f"Dialogue: 0,{_fmt_ass_time(c.start)},{_fmt_ass_time(c.end)},Default,,0,0,0,,{text}"
        )
    with open(out_path, "w", encoding="utf-8") as f:
        f.write(header + "\n".join(events) + "\n")
    return out_path



if __name__ == "__main__":
    test_cues = [
        SubtitleCue(0.1, 3.6, "Peygamber Efendimiz şöyle buyurmuştur."),
        SubtitleCue(3.6, 6.5, "Ameller niyetlere göredir."),
    ]
    write_srt(test_cues, "/tmp/test.srt")
    write_ass(test_cues, "/tmp/test.ass")
    print(open("/tmp/test.srt").read())
    print(open("/tmp/test.ass").read())
