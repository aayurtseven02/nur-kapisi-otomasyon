"""
Thumbnail (video kartı) üretim modülü. Pillow (açık kaynak) ile, arka planda
videodan alınmış GERÇEK bir kare + koyu/vinyet gradyan overlay + altın gradyanlı
büyük başlık + süsleme çizgisi + italik alt başlık kullanır (kullanıcının
verdiği referans görsele yakın bir tipografi tarzı; arka plan AI-üretim değil,
videonun kendi gerçek sahnesidir). İnsan yüzü/figürü gerektirmez.
"""
from __future__ import annotations
import os
import math
import subprocess
import textwrap
from PIL import Image, ImageDraw, ImageFont, ImageFilter


def extract_frame(video_path: str, out_path: str, timestamp: float = 1.5):
    # "-vf format=yuvj420p": logo bindirme adımından sonra videonun renk aralığı
    # etiketi (tv/limited range) mjpeg kodlayıcıyla çakışabiliyor ("Non
    # full-range YUV is non-standard" hatası); bu filtre kareyi mjpeg'in
    # beklediği tam aralığa (full-range) dönüştürüp hatayı önler.
    subprocess.run(
        ["ffmpeg", "-y", "-ss", str(timestamp), "-i", video_path,
         "-frames:v", "1", "-vf", "format=yuvj420p", out_path],
        check=True, capture_output=True,
    )
    return out_path


def _fit_cover(img: Image.Image, width: int, height: int) -> Image.Image:
    src_ratio = img.width / img.height
    dst_ratio = width / height
    if src_ratio > dst_ratio:
        new_h = height
        new_w = int(height * src_ratio)
    else:
        new_w = width
        new_h = int(width / src_ratio)
    img = img.resize((new_w, new_h))
    left = (new_w - width) // 2
    top = (new_h - height) // 2
    return img.crop((left, top, left + width, top + height))


def _load_font(font_path: str, size: int, weight: str | None = None) -> ImageFont.FreeTypeFont:
    font = ImageFont.truetype(font_path, size)
    if weight:
        try:
            names = {n.decode() if isinstance(n, bytes) else n for n in font.get_variation_names()}
            if weight in names:
                font.set_variation_by_name(weight)
        except Exception:
            pass
    return font


def _draw_outlined_text(img: Image.Image, xy, text, font, fill, outline_color=(0, 0, 0, 255),
                         outline_width: int = 5, steps: int = 16):
    """Yuvarlak/pürüzsüz kontur için metni daire üzerinde çok sayıda açıda kaydırarak çizer."""
    draw = ImageDraw.Draw(img)
    x, y = xy
    for i in range(steps):
        angle = 2 * math.pi * i / steps
        dx = round(outline_width * math.cos(angle))
        dy = round(outline_width * math.sin(angle))
        draw.text((x + dx, y + dy), text, font=font, fill=outline_color)
    draw.text((x, y), text, font=font, fill=fill)


def _draw_gradient_text(img: Image.Image, xy, text, font, top_rgb, bottom_rgb,
                         outline_color=(10, 5, 0, 255), outline_width: int = 5):
    """Metni dikey altın gradyanla doldurup etrafına koyu kontur ekler.
    Her şey ayrı, tamamen şeffaf bir katmanda hazırlanıp TEK SEFERDE
    img.alpha_composite ile birleştirilir (yarı saydam renklerin doğrudan
    ImageDraw ile 'overwrite' edilmesi -- ghost/hayalet görüntü hatasına
    yol açtığı için bu yöntem terk edildi)."""
    probe = ImageDraw.Draw(Image.new("RGBA", (1, 1)))
    bbox = probe.textbbox((0, 0), text, font=font)
    pad = outline_width + 6
    w = bbox[2] - bbox[0] + pad * 2
    h = bbox[3] - bbox[1] + pad * 2
    if w <= 0 or h <= 0:
        return
    x, y = xy
    origin = (pad - bbox[0], pad - bbox[1])

    layer = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    ldraw = ImageDraw.Draw(layer)
    for i in range(16):
        angle = 2 * math.pi * i / 16
        dx = round(outline_width * math.cos(angle))
        dy = round(outline_width * math.sin(angle))
        ldraw.text((origin[0] + dx, origin[1] + dy), text, font=font, fill=outline_color)

    mask = Image.new("L", (w, h), 0)
    mdraw = ImageDraw.Draw(mask)
    mdraw.text(origin, text, font=font, fill=255)

    grad = Image.new("RGB", (w, h), top_rgb)
    gdraw = ImageDraw.Draw(grad)
    for row in range(h):
        t = row / max(h - 1, 1)
        r = int(top_rgb[0] + (bottom_rgb[0] - top_rgb[0]) * t)
        g = int(top_rgb[1] + (bottom_rgb[1] - top_rgb[1]) * t)
        b = int(top_rgb[2] + (bottom_rgb[2] - top_rgb[2]) * t)
        gdraw.line([(0, row), (w, row)], fill=(r, g, b))
    grad_rgba = grad.convert("RGBA")
    grad_rgba.putalpha(mask)

    layer = Image.alpha_composite(layer, grad_rgba)
    img.alpha_composite(layer, (int(x - pad), int(y - pad)))


def _draw_italic_text(img: Image.Image, xy, text, font, fill, shear: float = 0.22):
    """Fontta italik varyant olmadığı için metni hafifçe eğerek (shear) italik görünüm verir."""
    draw = ImageDraw.Draw(Image.new("RGBA", (1, 1)))
    bbox = draw.textbbox((0, 0), text, font=font)
    w = bbox[2] - bbox[0] + int((bbox[3] - bbox[1]) * shear) + 20
    h = bbox[3] - bbox[1] + 20
    if w <= 0 or h <= 0:
        return
    layer = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    ldraw = ImageDraw.Draw(layer)
    ldraw.text((10 - bbox[0], 10 - bbox[1]), text, font=font, fill=fill)
    sheared = layer.transform(
        (w, h), Image.AFFINE, (1, -shear, shear * h, 0, 1, 0), resample=Image.BICUBIC
    )
    img.alpha_composite(sheared, (int(xy[0] - 10), int(xy[1] - 10)))


def _draw_divider(img: Image.Image, center_x: int, y: int, half_width: int,
                   color=(214, 170, 90, 255)):
    draw = ImageDraw.Draw(img)
    draw.line([(center_x - half_width, y), (center_x - 18, y)], fill=color, width=3)
    draw.line([(center_x + 18, y), (center_x + half_width, y)], fill=color, width=3)
    r = 7
    diamond = [(center_x, y - r), (center_x + r, y), (center_x, y + r), (center_x - r, y)]
    draw.polygon(diamond, fill=color)


def _paste_logo_badge(img: Image.Image, logo_path: str, width: int, height: int,
                       position: str = "bottom_right", size_ratio: float = 0.17,
                       margin_ratio: float = 0.03):
    """Kanal logosunu thumbnail'in SABİT bir köşesine, net (bulanıklaştırılmamış)
    şekilde yapıştırır -- her videoda aynı konum/boyut (marka tutarlılığı)."""
    if not logo_path or not os.path.exists(logo_path):
        return
    logo_size = int(height * size_ratio)
    margin = int(height * margin_ratio)
    logo = Image.open(logo_path).convert("RGBA").resize((logo_size, logo_size), Image.LANCZOS)

    positions = {
        "bottom_right": (width - logo_size - margin, height - logo_size - margin),
        "bottom_left": (margin, height - logo_size - margin),
        "top_right": (width - logo_size - margin, margin),
        "top_left": (margin, margin),
    }
    x, y = positions.get(position, positions["bottom_right"])

    # Hafif zemin/gölge halkası, logo açık renkli bölgelerde kaybolmasın diye
    ring = Image.new("RGBA", (logo_size + 14, logo_size + 14), (0, 0, 0, 0))
    ImageDraw.Draw(ring).ellipse([0, 0, logo_size + 14, logo_size + 14], fill=(0, 0, 0, 130))
    img.alpha_composite(ring, (x - 7, y - 7))
    img.alpha_composite(logo, (x, y))


def generate_thumbnail(
    background_image_path: str,
    title_text: str,
    out_path: str,
    subtitle_text: str = "",
    extra_text: str = "",
    width: int = 1280,
    height: int = 720,
    font_path: str = "assets/fonts/NotoSans-Variable.ttf",
    accent_text: str = "",
    arabic_header_text: str = "",
    arabic_font_path: str = "assets/fonts/Amiri-Regular.ttf",
    logo_path: str = "",
    logo_position: str = "bottom_right",
    logo_size_ratio: float = 0.17,
    logo_margin_ratio: float = 0.03,
):
    img = Image.open(background_image_path).convert("RGB")
    img = _fit_cover(img, width, height)
    img = img.filter(ImageFilter.GaussianBlur(1))

    overlay = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    draw = ImageDraw.Draw(overlay)
    # Genel vinyet: üst ve alt kenarlar biraz, ORTA BAND (başlığın oturacağı yer) belirgin koyu
    for y in range(height):
        band_center = height * 0.46
        band_half = height * 0.34
        dist = abs(y - band_center) / band_half
        band_alpha = int(175 * max(0.0, 1 - dist)) if dist < 1 else 0
        edge_alpha = int(120 * (y / height) ** 2) if y > height * 0.75 else int(90 * (1 - y / height) ** 3)
        alpha = min(215, band_alpha + edge_alpha)
        draw.line([(0, y), (width, y)], fill=(4, 10, 8, alpha))

    img = Image.alpha_composite(img.convert("RGBA"), overlay)

    cx = width // 2
    cursor_y = int(height * 0.16)

    if arabic_header_text:
        ar_font = _load_font(arabic_font_path, int(height * 0.08))
        bbox = ImageDraw.Draw(img).textbbox((0, 0), arabic_header_text, font=ar_font)
        w = bbox[2] - bbox[0]
        _draw_gradient_text(
            img, (cx - w / 2 - bbox[0], cursor_y), arabic_header_text, ar_font,
            top_rgb=(255, 236, 180), bottom_rgb=(205, 160, 70), outline_width=4,
        )
        cursor_y += int(height * 0.10)

    title_font_size = int(height * 0.145)
    title_font = _load_font(font_path, title_font_size, weight="Black")
    # break_long_words=False: uzun bir kelime (ör. "PEYGAMBERİMİZDEN") tek
    # satıra sığmasa bile ORTADAN BÖLÜNMEZ; o satır sadece hedef genişlikten
    # biraz taşar (sonraki `_fit_title_font` benzeri bir mekanizma yoksa bile
    # görsel olarak kelimenin yarısının kesilmesinden çok daha az rahatsız edicidir).
    wrapped = textwrap.wrap(title_text, width=14, break_long_words=False, break_on_hyphens=False)
    line_gap = int(title_font_size * 0.18)

    title_y = cursor_y
    for line in wrapped:
        bbox = ImageDraw.Draw(img).textbbox((0, 0), line, font=title_font)
        w = bbox[2] - bbox[0]
        x = cx - w / 2 - bbox[0]
        _draw_gradient_text(
            img, (x, title_y), line, title_font,
            top_rgb=(255, 240, 190), bottom_rgb=(197, 145, 46), outline_width=6,
        )
        title_y += (bbox[3] - bbox[1]) + line_gap

    title_y += int(height * 0.015)
    _draw_divider(img, cx, title_y, half_width=int(width * 0.16))
    title_y += int(height * 0.045)

    if subtitle_text:
        sub_font = _load_font(font_path, int(height * 0.052), weight="SemiBold")
        quoted = f"\u201c{subtitle_text}\u201d"
        bbox = ImageDraw.Draw(Image.new("RGBA", (1, 1))).textbbox((0, 0), quoted, font=sub_font)
        w = bbox[2] - bbox[0]
        _draw_italic_text(img, (cx - w / 2, title_y), quoted, sub_font, fill=(255, 255, 255, 255))
        title_y += (bbox[3] - bbox[1]) + int(height * 0.035)

    if extra_text:
        ex_font = _load_font(font_path, int(height * 0.038), weight="Medium")
        bbox = ImageDraw.Draw(img).textbbox((0, 0), extra_text, font=ex_font)
        w = bbox[2] - bbox[0]
        x = cx - w / 2 - bbox[0]
        _draw_outlined_text(img, (x, title_y), extra_text, ex_font,
                             fill=(225, 205, 160, 255), outline_color=(0, 0, 0, 200), outline_width=3)

    if accent_text:
        af = _load_font(font_path, int(height * 0.042), weight="Bold")
        _draw_outlined_text(img, (28, 22), accent_text, af, fill=(230, 200, 120, 255),
                             outline_color=(0, 0, 0, 200), outline_width=3)

    if logo_path:
        _paste_logo_badge(img, logo_path, width, height, position=logo_position,
                           size_ratio=logo_size_ratio, margin_ratio=logo_margin_ratio)

    img.convert("RGB").save(out_path, quality=94)
    return out_path


if __name__ == "__main__":
    sample_video = "assets/cache/stock_video/placeholder_nature_sunrise_1280x720.mp4"
    if os.path.exists(sample_video):
        extract_frame(sample_video, "/tmp/frame.jpg")
        generate_thumbnail(
            "/tmp/frame.jpg",
            "YASİN SURESİ",
            "/tmp/thumb.jpg",
            subtitle_text="Kalbin Şifası",
            extra_text="(1-20. Ayetler Türkçe Meali ile)",
            accent_text="#Kuran",
        )
        print("thumbnail üretildi: /tmp/thumb.jpg")
