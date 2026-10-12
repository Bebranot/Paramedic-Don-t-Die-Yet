"""Обложка мода для Steam: медик в форме прикрывает раненого под огнём.

    python tools/gen_preview.py          # About/Preview.png (с криком) и docs/Preview_noshout.png

Нужны текстуры мода (gen_textures.py) и ванильные основы (tools/_vanilla, extract_vanilla.py).
"""
import math
import os
import random
import sys

from PIL import Image, ImageDraw, ImageFilter, ImageFont

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from gen_textures import (OUTLINE, RED, RED_DARK, REFLECT, ROOT, VAN, WHITE, cross_patch)  # noqa: E402

APP = os.path.join(ROOT, "Textures", "Things", "Paramedic", "Apparel")
W, H = 1280, 720
SKIN = (224, 178, 140)
FONT_DIR = r"C:\Windows\Fonts"


def font(name, size):
    for n in (name, "arialbd.ttf"):
        try:
            return ImageFont.truetype(os.path.join(FONT_DIR, n), size)
        except OSError:
            pass
    return ImageFont.load_default()


def tinted(path, color, size):
    im = Image.open(path).convert("RGBA").resize((size, size), Image.LANCZOS)
    r, g, b, a = im.split()
    gray = Image.merge("RGB", (r, g, b)).convert("L")
    col = Image.merge("RGB", [gray.point(lambda v, c=c: v * c // 255) for c in color])
    col.putalpha(a)
    return col


def tex(path, size):
    return Image.open(path).convert("RGBA").resize((size, size), Image.LANCZOS)


def pawn(d, size, outfit, hat=None, goggles=False, body="Male", shirt_color=None):
    """Пешка из слоёв: тело, одежда, голова, шапка. Голова выше тела на ~0.34 размера тела."""
    canvas = Image.new("RGBA", (size, int(size * 1.45)), (0, 0, 0, 0))
    by = int(size * 0.45)
    canvas.alpha_composite(tinted(os.path.join(VAN, f"Naked_{body}_{d}.png"), SKIN, size), (0, by))
    if shirt_color:
        canvas.alpha_composite(tinted(os.path.join(VAN, f"ShirtBasic_{body}_{d}.png"), shirt_color, size), (0, by))
    for item in outfit:
        canvas.alpha_composite(tex(os.path.join(APP, item, f"PM_{item}_{body}_{d}.png"), size), (0, by))
    hx = int(size * 0.05) if d == "east" else 0
    hy = by - int(size * 0.27)
    canvas.alpha_composite(tinted(os.path.join(VAN, f"Male_Average_Normal_{d}.png"), SKIN, size), (hx, hy))
    if goggles:
        canvas.alpha_composite(tex(os.path.join(APP, "Goggles", f"PM_Goggles_{d}.png"), size), (hx, hy))
    if hat:
        canvas.alpha_composite(tex(os.path.join(APP, hat, f"PM_{hat}_{d}.png"), size), (hx, hy))
    return canvas


def background():
    random.seed(7)
    bg = Image.new("RGB", (W, H))
    px = bg.load()
    for y in range(H):
        for x in range(W):
            t = y / H
            n = random.randint(-6, 6)
            px[x, y] = (int(78 + 30 * t) + n, int(66 + 22 * t) + n, int(48 + 12 * t) + n)   # земля
    bg = bg.filter(ImageFilter.GaussianBlur(1.2))
    d = ImageDraw.Draw(bg, "RGBA")
    for _ in range(140):   # камешки, трава
        x, y = random.randint(0, W), random.randint(0, H)
        r = random.randint(2, 7)
        d.ellipse([x - r, y - r // 2, x + r, y + r // 2], fill=(52, 44, 32, 120))
    for _ in range(60):
        x, y = random.randint(0, W), random.randint(0, H)
        d.line([(x, y), (x + random.randint(-4, 4), y - random.randint(6, 14))], fill=(90, 110, 50, 160), width=2)
    # мешки с песком справа
    for i in range(5):
        cx, cy = 1180 + (i % 2) * 34, 300 + i * 52
        d.rounded_rectangle([cx - 40, cy - 22, cx + 40, cy + 22], radius=16, fill=(146, 128, 92, 255), outline=(*OUTLINE, 255), width=4)
    return bg.convert("RGBA")


def tracers(im, seed=3):
    random.seed(seed)
    layer = Image.new("RGBA", im.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    for _ in range(7):
        y0 = random.randint(120, 640)
        x0 = W + 20
        x1 = random.randint(520, 980)
        y1 = y0 + random.randint(-40, 40)
        d.line([(x0, y0), (x1, y1)], fill=(255, 210, 90, 70), width=9)
        d.line([(x0, y0), (x1, y1)], fill=(255, 236, 150, 230), width=3)
    layer = layer.filter(ImageFilter.GaussianBlur(0.8))
    im.alpha_composite(layer)
    # искры попаданий в землю
    d = ImageDraw.Draw(im)
    for _ in range(5):
        x, y = random.randint(560, 1000), random.randint(560, 690)
        for a in range(6):
            ang = random.uniform(-math.pi, 0)
            d.line([(x, y), (x + 18 * math.cos(ang), y + 18 * math.sin(ang))], fill=(255, 220, 120, 255), width=3)


def bubble(im, text_lines, tip, box):
    """Комикс-пузырь с хвостиком к голове медика."""
    d = ImageDraw.Draw(im)
    x0, y0, x1, y1 = box
    d.polygon([((x0 + x1) // 2 - 40, y1 - 6), ((x0 + x1) // 2 + 30, y1 - 6), tip], fill=(*OUTLINE, 255))
    d.rounded_rectangle([x0 - 6, y0 - 6, x1 + 6, y1 + 6], radius=46, fill=(*OUTLINE, 255))
    d.polygon([((x0 + x1) // 2 - 30, y1 - 10), ((x0 + x1) // 2 + 20, y1 - 10), (tip[0] + 6, tip[1] - 14)], fill=(255, 255, 255, 255))
    d.rounded_rectangle([x0, y0, x1, y1], radius=40, fill=(255, 255, 255, 255))
    f1 = font("impact.ttf", 70)
    f2 = font("impact.ttf", 44)
    cy = y0 + 22
    for i, line in enumerate(text_lines):
        f = f1 if i == 0 else f2
        tw = d.textlength(line, font=f)
        d.text(((x0 + x1 - tw) / 2, cy), line, font=f, fill=(*RED_DARK, 255) if i == 0 else (*OUTLINE, 255))
        cy += 78 if i == 0 else 50


def title(im):
    d = ImageDraw.Draw(im)
    # плашка под названием
    shape = [(0, 0), (640, 0), (560, H), (0, H)]
    mask = Image.new("L", im.size, 0)
    ImageDraw.Draw(mask).polygon(shape, fill=235)
    plate = Image.new("RGBA", im.size, (*RED_DARK, 0))
    dp = ImageDraw.Draw(plate)
    dp.rectangle([0, 0, W, H], fill=(*RED_DARK, 255))
    for i in range(-H, 640 + H, 70):
        dp.line([(i, 0), (i - H, H)], fill=(*RED, 255), width=26)
    dp.rectangle([0, H - 96, W, H - 70], fill=(*REFLECT, 255))
    plate.putalpha(mask)
    im.alpha_composite(plate)
    ImageDraw.Draw(im).line([(640, 0), (560, H)], fill=(*OUTLINE, 255), width=6)
    cross_patch(d, 110, 120, 64)
    f = font("impact.ttf", 104)
    d.text((40, 210), "PARAMEDIC", font=f, fill=(*WHITE, 255), stroke_width=6, stroke_fill=(*OUTLINE, 255))
    f = font("impact.ttf", 60)
    d.text((44, 330), "DON'T DIE YET!", font=f, fill=(*REFLECT, 255), stroke_width=5, stroke_fill=(*OUTLINE, 255))
    f = font("arialbd.ttf", 26)
    for i, line in enumerate(("Field medic role you wear", "8 uniform pieces · 3 abilities", "An oath not to kill")):
        d.text((46, 430 + i * 38), line, font=f, fill=(*WHITE, 255), stroke_width=3, stroke_fill=(*OUTLINE, 255))


def scene(shout):
    im = background()
    tracers(im)
    # раненый: лежит на земле, под ним кровь, на нём давящая повязка
    d = ImageDraw.Draw(im, "RGBA")
    d.ellipse([600, 560, 840, 640], fill=(120, 14, 14, 200))
    wounded = pawn("south", 230, [], body="Male", shirt_color=(70, 110, 170)).rotate(90, expand=True, resample=Image.BICUBIC)
    im.alpha_composite(wounded, (575, 470))
    cross_patch(ImageDraw.Draw(im), 812, 590, 15)
    # медик: полная форма, стоит над раненым
    medic = pawn("south", 380, ["Shirt", "Vest", "Belt"], hat="Beret", goggles=True, body="Male")
    mx, my = 740, 120
    shadow = Image.new("RGBA", im.size, (0, 0, 0, 0))
    ImageDraw.Draw(shadow).ellipse([mx + 70, my + 500, mx + 310, my + 550], fill=(0, 0, 0, 90))
    im.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(6)))
    im.alpha_composite(medic, (mx, my))
    if shout:
        bubble(im, ["MEDIC!", "HOLD YOUR FIRE!"], (mx + 150, my + 190), (650, 18, 1110, 190))
    title(im)
    # виньетка
    vig = Image.new("L", im.size, 0)
    ImageDraw.Draw(vig).rectangle([0, 0, W, H], fill=0)
    vg = Image.new("RGBA", im.size, (0, 0, 0, 0))
    dv = ImageDraw.Draw(vg)
    for i in range(30):
        dv.rectangle([i * 3, i * 3, W - i * 3, H - i * 3], outline=(0, 0, 0, 6))
    im.alpha_composite(vg)
    return im.convert("RGB")


def main():
    with_shout = scene(True)
    with_shout.save(os.path.join(ROOT, "About", "Preview.png"), optimize=True)
    scene(False).save(os.path.join(ROOT, "docs", "Preview_noshout.png"), optimize=True)
    print("ok", os.path.getsize(os.path.join(ROOT, "About", "Preview.png")) // 1024, "KB")


if __name__ == "__main__":
    sys.exit(main())
