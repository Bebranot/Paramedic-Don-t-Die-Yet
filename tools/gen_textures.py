"""Генерация всех текстур мода «Paramedic: Don't Die Yet!».

Основа — ванильные текстуры RimWorld (силуэты рубашки, лётной куртки, каски, тел и голов), их достаёт
tools/extract_vanilla.py в tools/_vanilla/. Отсюда берутся только форма и светотень; цвет, полосы,
кресты, берет, очки и разгрузка рисуются здесь. Рисуем в 4× (512) и уменьшаем до 256 со сглаживанием.

    python tools/extract_vanilla.py   # один раз (нужен UnityPy)
    python tools/gen_textures.py
"""
import os
import sys

from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
VAN = os.path.join(os.path.dirname(os.path.abspath(__file__)), "_vanilla")
OUT = os.path.join(ROOT, "Textures", "Things", "Paramedic")
UI = os.path.join(ROOT, "Textures", "UI", "Abilities")

BODIES = ["Male", "Female", "Thin", "Hulk", "Fat"]
DIRS = ["south", "east", "north"]
K = 4                 # внутренний масштаб: 128 → 512
FINAL = 256           # размер итоговых текстур одежды

# Палитра (приглушённая, под ванильный стиль)
RED = (180, 42, 40)
RED_DARK = (128, 28, 28)
WHITE = (234, 231, 222)
REFLECT = (214, 216, 196)
REFLECT_EDGE = (150, 152, 140)
GREEN = (40, 152, 74)
STRAP = (64, 64, 68)
OUTLINE = (20, 18, 18)
LENS = (70, 205, 120)
STEEL = (165, 170, 176)


# ------------------------------------------------------------------ утилиты
def van(name):
    """Ванильная текстура, увеличенная до 512."""
    im = Image.open(os.path.join(VAN, name + ".png")).convert("RGBA")
    return im.resize((im.width * K, im.height * K), Image.LANCZOS)


def vanilla_name(base, body, d):
    # у лётной куртки тела в нижнем регистре
    if base == "FlakJacket":
        return f"FlakJacket_{body.lower()}_{d}"
    return f"{base}_{body}_{d}"


def lum(r, g, b):
    return 0.299 * r + 0.587 * g + 0.114 * b


def alpha_mask(im, thr=128):
    return im.split()[3].point(lambda v: 255 if v > thr else 0)


def bbox(mask):
    return mask.getbbox()


def erode(mask, px):
    return mask.filter(ImageFilter.MinFilter(px * 2 + 1)) if px > 0 else mask


def dilate(mask, px):
    return mask.filter(ImageFilter.MaxFilter(px * 2 + 1)) if px > 0 else mask


def mix(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def shade(c, k):
    return tuple(max(0, min(255, int(v * k))) for v in c)


def finish(im, size=FINAL):
    return im.resize((size, size), Image.LANCZOS)


def save(im, *parts):
    path = os.path.join(*parts)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    im.save(path)
    return path


def recolor(src, painter):
    """Перекраска ванильной основы: тёмная обводка остаётся, внутренность — painter(xr, yr) × светотень."""
    w, h = src.size
    px = src.load()
    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    po = out.load()
    inner = alpha_mask(src)
    x0, y0, x1, y1 = bbox(inner)
    bw, bh = max(1, x1 - x0), max(1, y1 - y0)
    lums = sorted(lum(*px[x, y][:3]) for y in range(0, h, 2) for x in range(0, w, 2) if px[x, y][3] > 200 and lum(*px[x, y][:3]) > 120)
    ref = lums[int(len(lums) * 0.9)] if lums else 230
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a < 4:
                continue
            L = lum(r, g, b)
            t = max(0.0, min(1.0, (L - 45) / 60))          # 0 — обводка, 1 — внутренность
            c = painter((x - x0) / bw, (y - y0) / bh)
            k = max(0.72, min(1.06, L / ref))
            po[x, y] = (*mix(OUTLINE, shade(c, k), t), a)
    return out


def interior(im):
    """Маска внутренней части (без обводки) — для обрезки нашивок."""
    m = Image.new("L", im.size, 0)
    pi, pm = im.load(), m.load()
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = pi[x, y]
            if a > 200 and lum(r, g, b) > 70:
                pm[x, y] = 255
    return erode(m, 2)


def overlay_clipped(im, layer, mask):
    layer.putalpha(ImageChops.darker(layer.split()[3], mask))
    im.alpha_composite(layer)


def cross_patch(d, cx, cy, r, patch=True):
    """Знак первой помощи: зелёный крест на белой скруглённой плашке с тонкой обводкой."""
    if patch:
        d.rounded_rectangle([cx - r, cy - r, cx + r, cy + r], radius=max(2, r // 4),
                            fill=(*WHITE, 255), outline=(*OUTLINE, 255), width=max(2, r // 7))
    a = int(r * 0.72)
    t = max(2, int(r * 0.24))
    d.rectangle([cx - t, cy - a, cx + t, cy + a], fill=(*GREEN, 255))
    d.rectangle([cx - a, cy - t, cx + a, cy + t], fill=(*GREEN, 255))


def reflect_band(d, x0, x1, y0, y1):
    d.rectangle([x0, y0, x1, y1], fill=(*REFLECT, 255))
    e = max(2, (y1 - y0) // 6)
    d.rectangle([x0, y0, x1, y0 + e], fill=(*REFLECT_EDGE, 255))
    d.rectangle([x0, y1 - e, x1, y1], fill=(*REFLECT_EDGE, 255))


# ------------------------------------------------------------------ рубашка-поло
def make_shirt(body, d):
    src = van(vanilla_name("ShirtBasic", body, d))

    def p(xr, yr):
        return RED if yr < 0.17 else WHITE           # красная кокетка, белое поло
    im = recolor(src, p)
    m = erode(alpha_mask(im), 12)   # по силуэту: нашивка перекрывает швы
    x0, y0, x1, y1 = bbox(m)
    w, h = x1 - x0, y1 - y0
    layer = Image.new("RGBA", im.size, (0, 0, 0, 0))
    dr = ImageDraw.Draw(layer)
    dr.rectangle([x0, y0 + int(h * 0.80), x1, y0 + int(h * 0.84)], fill=(*RED, 255))   # красная полоса внизу
    if d == "south":
        cross_patch(dr, x0 + int(w * 0.70), y0 + int(h * 0.33), max(10, w // 11))
    overlay_clipped(im, layer, m)
    return im


# ------------------------------------------------------------------ жилет (своя форма по силуэту рубашки)
def make_vest(body, d):
    base = van(vanilla_name("ShirtBasic", body, d))
    shape = alpha_mask(base)
    shape = dilate(shape, 6)                         # чуть шире рубашки, чтобы её перекрывать
    x0, y0, x1, y1 = bbox(shape)
    w, h = x1 - x0, y1 - y0
    cut = Image.new("L", shape.size, 0)
    dc = ImageDraw.Draw(cut)
    # проймы/низ: жилет короче рубашки и открывает плечи
    dc.rectangle([x0, y1 - int(h * 0.08), x1, y1], fill=255)
    if d == "south":
        cx = x0 + w // 2
        dc.polygon([(cx - int(w * 0.17), y0), (cx + int(w * 0.17), y0), (cx, y0 + int(h * 0.42))], fill=255)
    elif d == "north":
        cx = x0 + w // 2
        dc.ellipse([cx - int(w * 0.16), y0 - int(h * 0.08), cx + int(w * 0.16), y0 + int(h * 0.10)], fill=255)
    else:
        dc.ellipse([x0 + int(w * 0.30), y0 - int(h * 0.08), x0 + int(w * 0.80), y0 + int(h * 0.09)], fill=255)
    shape = ImageChops.subtract(shape, cut)
    shape = shape.filter(ImageFilter.GaussianBlur(2)).point(lambda v: 255 if v > 128 else 0)

    # светотень берём у рубашки, обводку рисуем сами
    im = Image.new("RGBA", shape.size, (0, 0, 0, 0))
    ring = ImageChops.subtract(shape, erode(shape, 9))
    inner = erode(shape, 9)
    bx0, by0, bx1, by1 = bbox(inner)
    bw, bh = bx1 - bx0, by1 - by0
    pb, pi, pr, po = base.load(), inner.load(), ring.load(), im.load()

    def p(xr, yr):
        if d != "east" and (0.20 < xr < 0.30 or 0.70 < xr < 0.80) and yr < 0.55:
            return REFLECT                           # светоотражающие «подтяжки»
        if 0.58 < yr < 0.68:
            return REFLECT                           # поясная полоса
        return RED

    for y in range(im.height):
        for x in range(im.width):
            if pr[x, y]:
                po[x, y] = (*OUTLINE, 255)
            elif pi[x, y]:
                r, g, b, a = pb[x, y]
                k = 0.9 + 0.12 * (lum(r, g, b) / 255) if a > 0 else 0.95
                c = p((x - bx0) / bw, (y - by0) / bh)
                po[x, y] = (*shade(c, k), 255)
    layer = Image.new("RGBA", im.size, (0, 0, 0, 0))
    dr = ImageDraw.Draw(layer)
    # тонкие края светоотражающих полос
    for yy in (0.58, 0.68):
        y = by0 + int(bh * yy)
        dr.line([(bx0, y), (bx1, y)], fill=(*REFLECT_EDGE, 255), width=3)
    if d == "south":
        cross_patch(dr, bx0 + int(bw * 0.66), by0 + int(bh * 0.42), max(10, bw // 12))
    elif d == "north":
        cross_patch(dr, bx0 + bw // 2, by0 + int(bh * 0.40), max(16, bw // 7))
    else:
        cross_patch(dr, bx0 + int(bw * 0.45), by0 + int(bh * 0.32), max(9, bw // 9))
    overlay_clipped(im, layer, inner)
    return im


# ------------------------------------------------------------------ куртка спасателя (основа — лётная куртка)
def make_jacket(body, d):
    src = van(vanilla_name("FlakJacket", body, d))

    def p(xr, yr):
        if 0.56 < yr < 0.66:
            return REFLECT
        if yr < 0.24:
            return WHITE                             # белый верх/воротник
        return RED
    im = recolor(src, p)
    m = erode(alpha_mask(im), 12)   # по силуэту: нашивка перекрывает швы
    x0, y0, x1, y1 = bbox(m)
    w, h = x1 - x0, y1 - y0
    layer = Image.new("RGBA", im.size, (0, 0, 0, 0))
    dr = ImageDraw.Draw(layer)
    for yy in (0.56, 0.66):
        y = y0 + int(h * yy)
        dr.line([(x0, y), (x1, y)], fill=(*REFLECT_EDGE, 255), width=3)
    if d == "north":
        cross_patch(dr, x0 + w // 2, y0 + int(h * 0.46), max(16, w // 7))
    elif d == "south":
        cross_patch(dr, x0 + int(w * 0.27), y0 + int(h * 0.36), max(9, w // 13))
    else:
        cross_patch(dr, x0 + int(w * 0.40), y0 + int(h * 0.36), max(9, w // 10))
    overlay_clipped(im, layer, m)
    return im


# ------------------------------------------------------------------ разгрузка (по силуэту тела)
def make_belt(body, d):
    naked = van(f"Naked_{body}_{d}")
    bodym = alpha_mask(naked)
    x0, y0, x1, y1 = bbox(bodym)
    w, h = x1 - x0, y1 - y0
    shape = Image.new("L", bodym.size, 0)
    ds = ImageDraw.Draw(shape)
    by0, by1 = y0 + int(h * 0.66), y0 + int(h * 0.75)
    ds.rectangle([x0 - 8, by0, x1 + 8, by1], fill=255)         # ремень
    band = ImageChops.darker(shape, dilate(bodym, 8))
    pouches = Image.new("L", bodym.size, 0)
    dp = ImageDraw.Draw(pouches)
    pw, ph = int(w * 0.20), int(h * 0.17)
    if d == "east":
        spots = [x0 + int(w * 0.08)]                            # подсумок сзади
    else:
        spots = [x0 + int(w * 0.06), x1 - int(w * 0.06) - pw]
    for sx in spots:
        dp.rounded_rectangle([sx, by0 - int(ph * 0.25), sx + pw, by0 + ph], radius=10, fill=255)
    shape = ImageChops.lighter(band, pouches)
    im = Image.new("RGBA", shape.size, (0, 0, 0, 0))
    ring = ImageChops.subtract(shape, erode(shape, 8))
    inner = erode(shape, 8)
    po, pr, pi, pp = im.load(), ring.load(), inner.load(), erode(pouches, 8).load()
    for y in range(im.height):
        for x in range(im.width):
            if pr[x, y]:
                po[x, y] = (*OUTLINE, 255)
            elif pi[x, y]:
                po[x, y] = (*(RED if pp[x, y] else STRAP), 255)
    layer = Image.new("RGBA", im.size, (0, 0, 0, 0))
    dr = ImageDraw.Draw(layer)
    if d == "south":
        cross_patch(dr, spots[-1] + pw // 2, by0 + int(ph * 0.35), max(9, pw // 3))
    # пряжка
    cx = x0 + w // 2
    if d != "east":
        dr.rounded_rectangle([cx - 16, by0 + 4, cx + 16, by1 - 4], radius=4, fill=(*STEEL, 255), outline=(*OUTLINE, 255), width=3)
    overlay_clipped(im, layer, inner)
    return im


# ------------------------------------------------------------------ каска (основа — продвинутый шлем)
def make_helmet(d):
    src = van(f"AdvancedHelmet_{d}")

    def p(xr, yr):
        return RED if 0.60 < yr < 0.76 else WHITE
    im = recolor(src, p)
    m = erode(alpha_mask(im), 12)   # по силуэту: нашивка перекрывает швы
    x0, y0, x1, y1 = bbox(m)
    w, h = x1 - x0, y1 - y0
    layer = Image.new("RGBA", im.size, (0, 0, 0, 0))
    dr = ImageDraw.Draw(layer)
    if d == "south":
        cross_patch(dr, x0 + w // 2, y0 + int(h * 0.34), max(12, w // 9))
    elif d == "north":
        cross_patch(dr, x0 + w // 2, y0 + int(h * 0.38), max(12, w // 8))
    else:
        cross_patch(dr, x0 + int(w * 0.42), y0 + int(h * 0.36), max(12, w // 10))
    overlay_clipped(im, layer, m)
    return im


# ------------------------------------------------------------------ берет (рисуется с нуля)
def make_beret(d):
    S = 128 * K
    shape = Image.new("L", (S, S), 0)
    ds = ImageDraw.Draw(shape)
    s = K
    if d == "south":
        ds.ellipse([36 * s, 36 * s, 92 * s, 55 * s], fill=255)        # тулья
        ds.ellipse([56 * s, 33 * s, 98 * s, 50 * s], fill=255)        # сдвинутый набок верх
        band = [39 * s, 47 * s, 89 * s, 55 * s]
        badge = (50 * s, 46 * s)
    elif d == "east":
        ds.ellipse([38 * s, 36 * s, 90 * s, 56 * s], fill=255)
        ds.ellipse([30 * s, 34 * s, 66 * s, 51 * s], fill=255)        # верх свисает назад
        band = [42 * s, 48 * s, 90 * s, 56 * s]
        badge = (79 * s, 47 * s)
    else:
        ds.ellipse([37 * s, 36 * s, 91 * s, 56 * s], fill=255)
        ds.ellipse([31 * s, 33 * s, 70 * s, 50 * s], fill=255)
        band = [39 * s, 48 * s, 89 * s, 56 * s]
        badge = None
    db = ImageDraw.Draw(shape)
    db.rounded_rectangle(band, radius=4 * s, fill=255)
    im = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    ring = ImageChops.subtract(shape, erode(shape, 9))
    inner = erode(shape, 9)
    x0, y0, x1, y1 = bbox(inner)
    po, pr, pi = im.load(), ring.load(), inner.load()
    for y in range(S):
        for x in range(S):
            if pr[x, y]:
                po[x, y] = (*OUTLINE, 255)
            elif pi[x, y]:
                yr = (y - y0) / max(1, y1 - y0)
                xr = (x - x0) / max(1, x1 - x0)
                k = 1.04 - 0.22 * yr + (0.05 if xr > 0.55 and d == "south" else 0)
                po[x, y] = (*shade(RED_DARK if y >= band[1] + 6 else RED, k), 255)
    layer = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    dr = ImageDraw.Draw(layer)
    if badge:
        cross_patch(dr, badge[0], badge[1], 6 * s)
    overlay_clipped(im, layer, inner)
    return im


# ------------------------------------------------------------------ очки-тепловизор (рисуются с нуля)
def make_goggles(d):
    S = 128 * K
    im = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    dr = ImageDraw.Draw(im)
    s = K
    ey = 70 * s
    if d == "north":
        dr.rounded_rectangle([40 * s, ey - 4 * s, 88 * s, ey + 3 * s], radius=2 * s, fill=(*STRAP, 255), outline=(*OUTLINE, 255), width=2 * s)
        return im
    if d == "south":
        dr.rounded_rectangle([40 * s, ey - 3 * s, 88 * s, ey + 3 * s], radius=2 * s, fill=(*STRAP, 255), outline=(*OUTLINE, 255), width=2 * s)
        for cx in (55, 73):
            dr.rounded_rectangle([(cx - 9) * s, ey - 7 * s, (cx + 9) * s, ey + 6 * s], radius=4 * s, fill=(*STRAP, 255), outline=(*OUTLINE, 255), width=2 * s)
            dr.rounded_rectangle([(cx - 6) * s, ey - 4 * s, (cx + 6) * s, ey + 3 * s], radius=3 * s, fill=(*LENS, 255))
            dr.ellipse([(cx - 4) * s, ey - 3 * s, (cx - 1) * s, ey], fill=(220, 255, 230, 255))   # блик
    else:
        dr.rounded_rectangle([40 * s, ey - 3 * s, 84 * s, ey + 3 * s], radius=2 * s, fill=(*STRAP, 255), outline=(*OUTLINE, 255), width=2 * s)
        cx = 80
        dr.rounded_rectangle([(cx - 7) * s, ey - 7 * s, (cx + 8) * s, ey + 6 * s], radius=4 * s, fill=(*STRAP, 255), outline=(*OUTLINE, 255), width=2 * s)
        dr.rounded_rectangle([(cx - 2) * s, ey - 4 * s, (cx + 6) * s, ey + 3 * s], radius=3 * s, fill=(*LENS, 255))
    return im


# ------------------------------------------------------------------ иконки и прочее
def icon_from(im_512, size=128, pad=0.86):
    m = alpha_mask(im_512, 20)
    crop = im_512.crop(bbox(m))
    k = (size * K * pad) / max(crop.size)
    crop = crop.resize((max(1, int(crop.width * k)), max(1, int(crop.height * k))), Image.LANCZOS)
    canvas = Image.new("RGBA", (size * K, size * K), (0, 0, 0, 0))
    canvas.alpha_composite(crop, ((canvas.width - crop.width) // 2, (canvas.height - crop.height) // 2))
    return canvas.resize((size, size), Image.LANCZOS)


def make_pants_icon():
    src = van("FlakPants")

    def p(xr, yr):
        if 0.62 < yr < 0.68 or 0.74 < yr < 0.80:
            return REFLECT
        return RED if yr > 0.12 else RED_DARK
    return recolor(src, p)


def syringe_icon(size=64):
    S = size * K * 2
    im = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    w = S // 40

    def line(a, b, col, width):
        d.line([(a[0] * S, a[1] * S), (b[0] * S, b[1] * S)], fill=(*col, 255), width=width)

    line((0.70, 0.30), (0.88, 0.12), OUTLINE, w * 7)     # поршень
    line((0.70, 0.30), (0.88, 0.12), RED, w * 4)
    line((0.27, 0.73), (0.10, 0.90), OUTLINE, w * 4)     # игла
    line((0.27, 0.73), (0.10, 0.90), STEEL, w * 2)
    body = [(0.20, 0.68), (0.64, 0.24), (0.78, 0.38), (0.34, 0.82)]
    d.polygon([(x * S, y * S) for x, y in body], fill=(*WHITE, 255), outline=(*OUTLINE, 255), width=w * 2)
    liquid = [(0.29, 0.66), (0.56, 0.39), (0.66, 0.49), (0.39, 0.76)]
    d.polygon([(x * S, y * S) for x, y in liquid], fill=(236, 186, 48, 255))
    cross_patch(d, int(0.47 * S), int(0.57 * S), int(0.09 * S))
    return im.resize((size, size), Image.LANCZOS)


def ability_icon(kind, size=128):
    S = size * K
    im = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rounded_rectangle([8, 8, S - 9, S - 9], radius=S // 7, fill=(*RED_DARK, 255), outline=(*OUTLINE, 255), width=S // 40)
    d.rounded_rectangle([S // 14, S // 14, S - S // 14, S // 2], radius=S // 9, fill=(*RED, 255))   # блик сверху
    d.rounded_rectangle([S // 14, S // 3, S - S // 14, S - S // 14], radius=S // 9, fill=(*RED_DARK, 255))
    if kind == "stabilize":
        cross_patch(d, S // 2, int(S * 0.46), int(S * 0.27))
        # жгут
        d.rounded_rectangle([int(S * 0.18), int(S * 0.74), int(S * 0.82), int(S * 0.84)], radius=S // 30,
                            fill=(*WHITE, 255), outline=(*OUTLINE, 255), width=S // 60)
        d.rectangle([int(S * 0.46), int(S * 0.72), int(S * 0.54), int(S * 0.86)], fill=(*STEEL, 255), outline=(*OUTLINE, 255), width=S // 80)
    elif kind == "dontshoot":
        s = S / 128
        palm = [(44, 110), (38, 64), (45, 59), (52, 74), (51, 28), (60, 26), (62, 68), (64, 20), (73, 20), (73, 68),
                (77, 28), (86, 30), (83, 76), (92, 54), (100, 58), (88, 98), (80, 110)]
        d.polygon([(x * s, y * s) for x, y in palm], fill=(*WHITE, 255), outline=(*OUTLINE, 255), width=int(3 * s))
        cross_patch(d, int(66 * s), int(88 * s), int(11 * s), patch=False)
    else:
        syr = syringe_icon(size * K // 2).resize((int(S * 0.82), int(S * 0.82)), Image.LANCZOS)
        im.alpha_composite(syr, (int(S * 0.09), int(S * 0.09)))
    return im.resize((size, size), Image.LANCZOS)


def preview(icons):
    W, H = 900, 506
    im = Image.new("RGBA", (W, H), (*RED_DARK, 255))
    d = ImageDraw.Draw(im)
    for i in range(-H, W + H, 64):
        d.line([(i, 0), (i - H, H)], fill=(*RED, 255), width=24)
    d.rectangle([0, H - 86, W, H - 60], fill=(*REFLECT, 255))
    d.rectangle([0, H - 86, W, H - 82], fill=(*REFLECT_EDGE, 255))
    d.rectangle([0, H - 64, W, H - 60], fill=(*REFLECT_EDGE, 255))
    cross_patch(d, 150, 160, 92)
    try:
        big = ImageFont.truetype(r"C:\Windows\Fonts\arialbd.ttf", 70)
        small = ImageFont.truetype(r"C:\Windows\Fonts\arialbd.ttf", 38)
    except OSError:
        big = small = ImageFont.load_default()
    d.text((282, 82), "PARAMEDIC", font=big, fill=(*WHITE, 255), stroke_width=5, stroke_fill=(*OUTLINE, 255))
    d.text((286, 168), "Don't Die Yet!", font=small, fill=(*REFLECT, 255), stroke_width=4, stroke_fill=(*OUTLINE, 255))
    x = 36
    for ic in icons:
        im.alpha_composite(ic.resize((128, 128), Image.LANCZOS), (x, 292))
        x += 140
    save(im.convert("RGB"), ROOT, "About", "Preview.png")


def mod_icon(size=128):
    """About/ModIcon.png — значок в списке модов."""
    S = size * K
    im = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.ellipse([8, 8, S - 9, S - 9], fill=(*RED, 255), outline=(*OUTLINE, 255), width=S // 30)
    d.rectangle([0, int(S * 0.62), S, int(S * 0.70)], fill=(*REFLECT, 255))
    ring = Image.new("L", (S, S), 0)
    ImageDraw.Draw(ring).ellipse([8 + S // 30, 8 + S // 30, S - 9 - S // 30, S - 9 - S // 30], fill=255)
    layer = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    ImageDraw.Draw(layer).rectangle([0, int(S * 0.62), S, int(S * 0.70)], fill=(*REFLECT, 255))
    im2 = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    ImageDraw.Draw(im2).ellipse([8, 8, S - 9, S - 9], fill=(*RED, 255), outline=(*OUTLINE, 255), width=S // 30)
    overlay_clipped(im2, layer, ring)
    cross_patch(ImageDraw.Draw(im2), S // 2, int(S * 0.42), int(S * 0.24))
    save(im2.resize((size, size), Image.LANCZOS), ROOT, "About", "ModIcon.png")


def main():
    if not os.path.isdir(VAN):
        print("нет tools/_vanilla — сначала python tools/extract_vanilla.py")
        return 1
    icons = {}
    for name, maker in (("Shirt", make_shirt), ("Vest", make_vest), ("Jacket", make_jacket), ("Belt", make_belt)):
        for body in BODIES:
            for d in DIRS:
                im = maker(body, d)
                save(finish(im), OUT, "Apparel", name, f"PM_{name}_{body}_{d}.png")
                if body == "Male" and d == "south":
                    icons[name] = icon_from(im)
        print(name, "ok")
    for name, maker in (("Helmet", make_helmet), ("Beret", make_beret), ("Goggles", make_goggles)):
        for d in DIRS:
            im = maker(d)
            save(finish(im), OUT, "Apparel", name, f"PM_{name}_{d}.png")
            if d == "south":
                icons[name] = icon_from(im)
        print(name, "ok")
    icons["Pants"] = icon_from(make_pants_icon())
    for name, ic in icons.items():
        save(ic, OUT, "Apparel", name, f"PM_{name}.png")
    save(syringe_icon(64), OUT, "Item", "PM_AdrenalineSyringe.png")
    save(ability_icon("stabilize"), UI, "PM_Stabilize.png")
    save(ability_icon("dontshoot"), UI, "PM_DontShoot.png")
    save(ability_icon("adrenaline"), UI, "PM_Adrenaline.png")
    preview([icons[n] for n in ("Beret", "Vest", "Jacket", "Helmet", "Belt", "Goggles")])
    mod_icon()
    print("ok")
    return 0


if __name__ == "__main__":
    sys.exit(main())
