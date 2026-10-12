"""Генерация текстур формы парамедика.

Силуэт, обводка и объём (светотень) берутся из готовой одежды мода [RH2] Rimmu-Nation² - Clothing
(мастерская 2563506048) — так форма гарантированно садится на все типы тел. Цвет, полосы, кресты,
нашивки рисуются здесь. Иконки способностей, шприц, штаны — нарисованы с нуля.

ВНИМАНИЕ: перед публикацией в Steam силуэты RH2 нужно заменить своими (см. docs/QUESTIONS.md).

    python tools/gen_textures.py
"""
import math
import os
import sys

from PIL import Image, ImageChops, ImageDraw, ImageFilter

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
RH2 = r"C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\2563506048\Textures\Things\Apparel"
OUT = os.path.join(ROOT, "Textures", "Things", "Paramedic")
UI = os.path.join(ROOT, "Textures", "UI", "Abilities")

BODIES = ["Male", "Female", "Thin", "Hulk", "Fat"]
DIRS = ["south", "east", "north"]

# Палитра
RED = (196, 36, 40)
RED_DARK = (140, 22, 28)
WHITE = (236, 236, 230)
REFLECT = (214, 222, 160)      # светоотражающая полоса (чуть желтоватая)
GREEN = (30, 150, 70)
OUTLINE = (24, 20, 20)
LEATHER = (92, 60, 40)
STEEL = (150, 156, 162)


def lum(c):
    return 0.299 * c[0] + 0.587 * c[1] + 0.114 * c[2]


def mul(c, k):
    return tuple(max(0, min(255, int(v * k))) for v in c)


def load(path):
    return Image.open(path).convert("RGBA")


def bbox_alpha(im, thr=8):
    a = im.split()[3].point(lambda v: 255 if v > thr else 0)
    return a.getbbox()


def recolor(src, painter, flat_shade=False):
    """Перекраска: обводка остаётся тёмной, внутренность красится painter(xr, yr) с объёмом из исходника."""
    w, h = src.size
    px = src.load()
    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    po = out.load()
    bb = bbox_alpha(src)
    if not bb:
        return out
    x0, y0, x1, y1 = bb
    bw, bh = max(1, x1 - x0), max(1, y1 - y0)
    # средняя яркость внутренности — для нормировки светотени
    vals = [lum(px[x, y]) for y in range(h) for x in range(w) if px[x, y][3] > 200 and lum(px[x, y]) > 70]
    mean = sum(vals) / len(vals) if vals else 200
    # обводка — тёмные пиксели у края силуэта (внутренние тёмные пятна камуфляжа не считаются)
    alpha = src.split()[3].point(lambda v: 255 if v > 8 else 0)
    near_edge = alpha.filter(ImageFilter.MinFilter(max(3, w // 40 * 2 + 1))).load()
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a < 8:
                continue
            L = lum((r, g, b))
            if flat_shade and near_edge[x, y] == 0 or not flat_shade and L < 55:
                po[x, y] = (*OUTLINE, a)
                continue
            shade = 1.0 if flat_shade else max(0.55, min(1.2, L / mean))
            c = painter((x - x0) / bw, (y - y0) / bh)
            po[x, y] = (*mul(c, shade), a)
    return out


def draw_cross(im, cx, cy, size, patch=True, outline=True):
    """Зелёный крест на белой плашке (знак первой помощи)."""
    d = ImageDraw.Draw(im)
    s = size
    if patch:
        d.rectangle([cx - s, cy - s, cx + s, cy + s], fill=(*WHITE, 255), outline=(*OUTLINE, 255) if outline else None)
    t = max(1, int(s * 0.36))
    a = int(s * 0.8)
    d.rectangle([cx - t, cy - a, cx + t, cy + a], fill=(*GREEN, 255))
    d.rectangle([cx - a, cy - t, cx + a, cy + t], fill=(*GREEN, 255))


def save(im, *parts):
    path = os.path.join(*parts)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    im.save(path)
    return path


def apply_decor(im, decorate, d):
    """Нашивки рисуются на отдельном слое и обрезаются по силуэту вещи (не вылезают за край)."""
    bb = bbox_alpha(im, thr=128)
    if not bb or not decorate:
        return
    if getattr(decorate, "in_place", False):
        decorate(im, d, bb)
        return
    layer = Image.new("RGBA", im.size, (0, 0, 0, 0))
    decorate(layer, d, bb)
    layer.putalpha(ImageChops.darker(layer.split()[3], im.split()[3]))
    im.alpha_composite(layer)


def body_set(name, src_dir, src_base, painter, decorate, flat=False):
    for body in BODIES:
        for d in DIRS:
            src = load(os.path.join(RH2, src_dir, f"{src_base}_{body}_{d}.png"))
            im = recolor(src, painter(d), flat_shade=flat)
            apply_decor(im, decorate, d)
            save(im, OUT, "Apparel", name, f"PM_{name}_{body}_{d}.png")


def head_set(name, src_dir, src_base, painter, decorate, flat=False):
    for d in DIRS:
        src = load(os.path.join(RH2, src_dir, f"{src_base}_{d}.png"))
        im = recolor(src, painter(d), flat_shade=flat)
        apply_decor(im, decorate, d)
        save(im, OUT, "Apparel", name, f"PM_{name}_{d}.png")


def icon_from(name, body="Male", d="south", size=128):
    """Иконка на земле: вид спереди, обрезан и вписан в квадрат."""
    path = os.path.join(OUT, "Apparel", name, f"PM_{name}_{body}_{d}.png")
    if not os.path.exists(path):
        path = os.path.join(OUT, "Apparel", name, f"PM_{name}_{d}.png")
    im = load(path)
    bb = bbox_alpha(im)
    crop = im.crop(bb)
    k = (size * 0.86) / max(crop.size)
    crop = crop.resize((max(1, int(crop.width * k)), max(1, int(crop.height * k))), Image.LANCZOS)
    canvas = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    canvas.alpha_composite(crop, ((size - crop.width) // 2, (size - crop.height) // 2))
    save(canvas, OUT, "Apparel", name, f"PM_{name}.png")


# ------------------------------------------------------------------ рубашка (поло)
def shirt_painter(d):
    def p(xr, yr):
        if yr < 0.16:
            return WHITE                               # белый воротник/плечи
        if 0.50 < yr < 0.58:
            return REFLECT                             # светоотражающая вставка
        return RED
    return p


def shirt_decor(im, d, bb):
    x0, y0, x1, y1 = bb
    if d == "south":
        draw_cross(im, int(x0 + (x1 - x0) * 0.68), int(y0 + (y1 - y0) * 0.30), max(3, (x1 - x0) // 14))


# ------------------------------------------------------------------ жилет
def vest_painter(d):
    def p(xr, yr):
        if 0.56 < yr < 0.66:
            return REFLECT
        if d != "east" and (xr < 0.16 or xr > 0.84):
            return WHITE                               # белые боковины
        return RED
    return p


def vest_decor(im, d, bb):
    x0, y0, x1, y1 = bb
    w, h = x1 - x0, y1 - y0
    if d == "south":
        draw_cross(im, int(x0 + w * 0.68), int(y0 + h * 0.44), max(4, w // 11))
    elif d == "north":
        draw_cross(im, x0 + w // 2, int(y0 + h * 0.38), max(6, w // 5))
    else:
        draw_cross(im, int(x0 + w * 0.5), int(y0 + h * 0.34), max(3, w // 8))


# ------------------------------------------------------------------ куртка спасателя
def jacket_painter(d):
    def p(xr, yr):
        if 0.40 < yr < 0.47 or 0.80 < yr < 0.87:
            return REFLECT                             # две широкие полосы
        if yr < 0.14:
            return RED_DARK                            # тёмный воротник
        if d == "east":
            return WHITE if yr < 0.40 else RED
        return WHITE if (xr < 0.28 or xr > 0.72) and yr < 0.40 else RED
    return p


def jacket_decor(im, d, bb):
    x0, y0, x1, y1 = bb
    w, h = x1 - x0, y1 - y0
    if d == "north":
        draw_cross(im, x0 + w // 2, int(y0 + h * 0.28), max(6, w // 5))
    elif d == "south":
        draw_cross(im, int(x0 + w * 0.30), int(y0 + h * 0.27), max(3, w // 14))


# ------------------------------------------------------------------ разгрузка
def belt_painter(d):
    def p(xr, yr):
        # тёмная стропа, по краям — красные подсумки
        return RED if xr < 0.22 or xr > 0.78 else (58, 58, 62)
    return p


def belt_decor(im, d, bb):
    x0, y0, x1, y1 = bb
    w, h = x1 - x0, y1 - y0
    if d in ("south", "north"):
        draw_cross(im, x0 + int(w * 0.75), y0 + h // 2, max(2, min(w, h) // 5))


# ------------------------------------------------------------------ берет
def beret_painter(d):
    def p(xr, yr):
        return RED if yr < 0.82 else RED_DARK          # кант снизу
    return p


def beret_decor(im, d, bb):
    x0, y0, x1, y1 = bb
    w, h = x1 - x0, y1 - y0
    if d == "south":
        draw_cross(im, x0 + int(w * 0.30), y0 + int(h * 0.55), max(3, w // 12))
    elif d == "east":
        draw_cross(im, x0 + int(w * 0.62), y0 + int(h * 0.55), max(3, w // 10))


# ------------------------------------------------------------------ каска
def helmet_painter(d):
    def p(xr, yr):
        return RED if 0.62 < yr < 0.74 else WHITE      # красная лента
    return p


def helmet_decor(im, d, bb):
    x0, y0, x1, y1 = bb
    w, h = x1 - x0, y1 - y0
    if d == "south":
        draw_cross(im, x0 + w // 2, y0 + int(h * 0.36), max(3, w // 9))
    elif d == "north":
        draw_cross(im, x0 + w // 2, y0 + int(h * 0.40), max(3, w // 8))
    else:
        draw_cross(im, x0 + int(w * 0.45), y0 + int(h * 0.38), max(3, w // 9))


# ------------------------------------------------------------------ очки
def goggles_painter(d):
    def p(xr, yr):
        return (60, 64, 70)
    return p


def goggles_decor(im, d, bb):
    # линзы — зелёный отблеск тепловизора
    px = im.load()
    x0, y0, x1, y1 = bb
    for y in range(y0, y1):
        for x in range(x0, x1):
            r, g, b, a = px[x, y]
            if a > 200 and (r, g, b) != OUTLINE and (y - y0) / max(1, y1 - y0) > 0.25:
                px[x, y] = (70, 200, 110, a)


goggles_decor.in_place = True   # перекрашивает пиксели самой вещи, а не рисует поверх


# ------------------------------------------------------------------ штаны (иконка, на теле не рисуются)
def pants_icon(size=128):
    im = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    s = size / 128
    poly = [(34, 18), (94, 18), (100, 112), (72, 112), (64, 52), (56, 112), (28, 112)]
    poly = [(x * s, y * s) for x, y in poly]
    d.polygon(poly, fill=(*RED, 255), outline=(*OUTLINE, 255))
    d.line(poly + [poly[0]], fill=(*OUTLINE, 255), width=int(4 * s))
    d.rectangle([34 * s, 18 * s, 94 * s, 28 * s], fill=(*RED_DARK, 255), outline=(*OUTLINE, 255), width=int(3 * s))
    for y in (78, 84):
        d.line([(31 * s, y * s), (59 * s, y * s)], fill=(*REFLECT, 255), width=int(4 * s))
        d.line([(69 * s, y * s), (97 * s, y * s)], fill=(*REFLECT, 255), width=int(4 * s))
    d.rectangle([34 * s, 46 * s, 50 * s, 62 * s], fill=(*RED_DARK, 255), outline=(*OUTLINE, 255), width=int(2 * s))
    d.rectangle([78 * s, 46 * s, 94 * s, 62 * s], fill=(*RED_DARK, 255), outline=(*OUTLINE, 255), width=int(2 * s))
    save(im, OUT, "Apparel", "Pants", "PM_Pants.png")


# ------------------------------------------------------------------ шприц
def syringe_icon(size=64):
    big = Image.new("RGBA", (size * 4, size * 4), (0, 0, 0, 0))
    d = ImageDraw.Draw(big)
    S = size * 4
    # корпус по диагонали
    body = [(0.22, 0.70), (0.62, 0.30), (0.74, 0.42), (0.34, 0.82)]
    d.polygon([(x * S, y * S) for x, y in body], fill=(230, 236, 240, 255), outline=(*OUTLINE, 255), width=10)
    liquid = [(0.30, 0.66), (0.56, 0.40), (0.64, 0.48), (0.38, 0.74)]
    d.polygon([(x * S, y * S) for x, y in liquid], fill=(240, 190, 40, 255))
    d.line([(0.68 * S, 0.36 * S), (0.86 * S, 0.18 * S)], fill=(*OUTLINE, 255), width=22)
    d.line([(0.68 * S, 0.36 * S), (0.86 * S, 0.18 * S)], fill=(*RED, 255), width=12)
    d.line([(0.28 * S, 0.76 * S), (0.12 * S, 0.92 * S)], fill=(*OUTLINE, 255), width=10)
    d.line([(0.28 * S, 0.76 * S), (0.12 * S, 0.92 * S)], fill=(*STEEL, 255), width=5)
    im = big.resize((size, size), Image.LANCZOS)
    draw_cross(im, int(size * 0.52), int(size * 0.48), max(3, size // 12), patch=False)
    save(im, OUT, "Item", "PM_AdrenalineSyringe.png")


# ------------------------------------------------------------------ иконки способностей
def ability_base(size=128):
    im = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rounded_rectangle([4, 4, size - 5, size - 5], radius=18, fill=(*RED_DARK, 255), outline=(*OUTLINE, 255), width=4)
    return im, d


def ability_icons(size=128):
    # стабилизация: белый бинт-крест + капля
    im, d = ability_base(size)
    draw_cross(im, size // 2, size // 2, size // 3)
    d.ellipse([size * 0.62, size * 0.62, size * 0.86, size * 0.86], fill=(*RED, 255), outline=(*OUTLINE, 255), width=3)
    d.line([(size * 0.60, size * 0.74), (size * 0.88, size * 0.74)], fill=(*WHITE, 255), width=4)
    save(im, UI, "PM_Stabilize.png")

    # не стрелять: поднятая ладонь + крест
    im, d = ability_base(size)
    s = size / 128
    palm = [(44, 108), (40, 62), (46, 58), (52, 74), (52, 30), (60, 28), (62, 70), (64, 24), (72, 24), (72, 70),
            (76, 30), (84, 32), (82, 76), (90, 52), (98, 56), (86, 96), (78, 108)]
    d.polygon([(x * s, y * s) for x, y in palm], fill=(*WHITE, 255), outline=(*OUTLINE, 255))
    draw_cross(im, int(66 * s), int(86 * s), int(9 * s))
    save(im, UI, "PM_DontShoot.png")

    # адреналин: шприц на фоне
    im, d = ability_base(size)
    syr = load(os.path.join(OUT, "Item", "PM_AdrenalineSyringe.png")).resize((int(size * 0.8), int(size * 0.8)), Image.LANCZOS)
    im.alpha_composite(syr, (int(size * 0.1), int(size * 0.1)))
    save(im, UI, "PM_Adrenaline.png")


def preview():
    """About/Preview.png: название, форма и знак."""
    from PIL import ImageFont
    W, H = 900, 506
    im = Image.new("RGBA", (W, H), (*RED_DARK, 255))
    d = ImageDraw.Draw(im)
    for i in range(0, W + H, 60):   # диагональные светоотражающие полосы
        d.line([(i, 0), (i - H, H)], fill=(*RED, 255), width=26)
    d.rectangle([0, H - 70, W, H - 52], fill=(*REFLECT, 255))
    draw_cross(im, 150, 170, 90)
    try:
        big = ImageFont.truetype(r"C:\Windows\Fonts\arialbd.ttf", 64)
        small = ImageFont.truetype(r"C:\Windows\Fonts\arialbd.ttf", 34)
    except OSError:
        big = small = ImageFont.load_default()
    d.text((290, 90), "PARAMEDIC", font=big, fill=(*WHITE, 255), stroke_width=4, stroke_fill=(*OUTLINE, 255))
    d.text((292, 170), "Don't Die Yet!", font=small, fill=(*REFLECT, 255), stroke_width=3, stroke_fill=(*OUTLINE, 255))
    x = 40
    for n in ("Beret", "Vest", "Jacket", "Helmet", "Belt", "Goggles"):
        icon = load(os.path.join(OUT, "Apparel", n, f"PM_{n}.png")).resize((130, 130), Image.LANCZOS)
        im.alpha_composite(icon, (x, 290))
        x += 140
    save(im.convert("RGB"), ROOT, "About", "Preview.png")


def main():
    body_set("Shirt", r"Tops\SuitShirt", "SuitShirt", shirt_painter, shirt_decor)
    body_set("Vest", r"Armour\PASGT", "PASGT_CC", vest_painter, vest_decor, flat=True)
    body_set("Jacket", r"Shell\Field_Parka", "ReconJacket", jacket_painter, jacket_decor)
    body_set("Belt", r"Webbing\Condor_BattleBelt", "Condor_BattleBelt", belt_painter, belt_decor, flat=True)
    head_set("Beret", r"Headgear\Hats\VDVBeret", "Beret", beret_painter, beret_decor, flat=True)
    head_set("Helmet", r"Helmet\ACH", "ACH_UCP", helmet_painter, helmet_decor, flat=True)
    head_set("Goggles", r"Headgear\EyePros", "BallisticGoggles_8X10", goggles_painter, goggles_decor, flat=True)
    for n in ("Shirt", "Vest", "Jacket", "Belt", "Beret", "Helmet", "Goggles"):
        icon_from(n)
    pants_icon()
    syringe_icon()
    ability_icons()
    preview()
    print("ok")


if __name__ == "__main__":
    sys.exit(main())
