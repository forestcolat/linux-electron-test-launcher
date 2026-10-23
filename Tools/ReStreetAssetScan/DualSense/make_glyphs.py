"""Draws the DualSense symbols the patcher paints into the game's button prompts.

Each glyph is a white shape on transparency, cropped to its ink. The patcher scales it into the
black area of the original prompt, so only the shape matters here, not the final size.

    python Tools/ReStreetAssetScan/DualSense/make_glyphs.py
"""

import os

from PIL import Image, ImageDraw, ImageFilter, ImageFont

SIZE = 256
STROKE = 34
HERE = os.path.dirname(os.path.abspath(__file__))
FONT = os.path.join(os.environ.get("WINDIR", r"C:\Windows"), "Fonts", "arialbd.ttf")


def canvas():
    image = Image.new("L", (SIZE, SIZE), 0)
    return image, ImageDraw.Draw(image)


def cross():
    image, draw = canvas()
    m = STROKE // 2 + 8
    draw.line([(m, m), (SIZE - m, SIZE - m)], fill=255, width=STROKE)
    draw.line([(m, SIZE - m), (SIZE - m, m)], fill=255, width=STROKE)
    return image


def circle():
    image, draw = canvas()
    m = 8
    draw.ellipse([m, m, SIZE - m, SIZE - m], outline=255, width=STROKE)
    return image


def square():
    image, draw = canvas()
    m = 24
    draw.rectangle([m, m, SIZE - m, SIZE - m], outline=255, width=STROKE)
    return image


def triangle():
    image, draw = canvas()
    m = 8
    top, left, right = (SIZE / 2, m + 10), (m, SIZE - m - 30), (SIZE - m, SIZE - m - 30)
    draw.line([top, left, right, top, left], fill=255, width=STROKE, joint="curve")
    return image


def options():
    # Three horizontal bars, the mark printed next to the OPTIONS button.
    image, draw = canvas()
    for i in range(3):
        y = 40 + i * 80
        draw.rounded_rectangle([20, y, SIZE - 20, y + STROKE + 4], radius=12, fill=255)
    return image


def create():
    # Three short slanted strokes, the mark printed next to the CREATE button.
    image, draw = canvas()
    for i in range(3):
        x = 40 + i * 80
        draw.line([(x + 30, 30), (x - 10, SIZE - 30)], fill=255, width=STROKE + 4)
    return image


def text(label):
    font = ImageFont.truetype(FONT, 220)
    image = Image.new("L", (SIZE * 2, SIZE), 0)
    ImageDraw.Draw(image).text((10, 0), label, font=font, fill=255)
    return image


GLYPHS = {
    "cross": cross, "circle": circle, "square": square, "triangle": triangle,
    "options": options, "create": create,
    **{name.lower(): (lambda name=name: text(name)) for name in ["L1", "R1", "L2", "R2", "L3", "R3", "L", "R", "PS"]},
}

# ---- Select Sides controller ---------------------------------------------------------------------
#
# The pad is drawn once in its own design space (it uses 0..96 x 0..68) and then mapped into each
# texture the game uses for it: texel = offset + scale * design.
#
# Each controller texture comes with a white strip the game tints with the player's colour and lays
# over the bottom of the pad: strip texel (u, v) lands on controller texel (x + sx*u, y + sy*v).
# Those mappings and the old outlines were measured in game against the Xbox 360 art.
SS = 8  # supersampling
TARGETS = {
    # data1.big proxy/art/controllers.big: texture 5 (pad, 128x128) and 2 (strip, 128x32).
    "controller": dict(size=(128, 128), box=(4, 4, 92, 64), bar=(19.0, 0.641, 49.2, 0.73), bar_size=(128, 32)),
    # data1.big data/ui/game/components/fifasideselector.big: texture 33 (pad), 30 (strip), 8 (empty slot).
    "sideselector": dict(size=(256, 128), box=(4, 5, 126, 88), bar=(23.95, 0.95, 67.6, 0.916), bar_size=(128, 32),
                         silhouette=dict(size=(128, 64), box=(1, 1, 90, 62), colour=(176, 180, 176, 130))),
}


def spline(points, steps=12):
    """Closed Catmull-Rom curve through points."""
    out = []
    n = len(points)
    for i in range(n):
        p0, p1, p2, p3 = points[i - 1], points[i], points[(i + 1) % n], points[(i + 2) % n]
        for s in range(steps):
            t = s / steps
            out.append(tuple(0.5 * ((2 * p1[k]) + (-p0[k] + p2[k]) * t + (2 * p0[k] - 5 * p1[k] + 4 * p2[k] - p3[k]) * t * t
                                    + (-p0[k] + 3 * p1[k] - 3 * p2[k] + p3[k]) * t * t * t) for k in range(2)))
    return out


def mirrored(left):
    """Left half (top centre to bottom centre) to a full symmetric outline around x = 48."""
    return left + [(96 - x, y) for x, y in reversed(left[1:-1])]


BODY = spline(mirrored([(48, 9), (34, 8.5), (20, 9.5), (12, 13), (8, 20), (6, 30), (6.5, 42), (9, 54), (13, 63), (19, 66.5),
                        (25, 64.5), (29, 59.5), (33, 57.5), (40, 56.7), (48, 56.5)]))
CENTRE = spline(mirrored([(48, 28), (36, 28), (28, 31), (25.5, 37), (27, 46), (31, 54), (40, 56.6), (48, 56.4)]))
DESIGN = (128, 128)


def scaled(points):
    return [(x * SS, y * SS) for x, y in points]


def design_pad():
    size = (DESIGN[0] * SS, DESIGN[1] * SS)
    image = Image.new("RGBA", size, (0, 0, 0, 0))
    d = ImageDraw.Draw(image)
    body = scaled(BODY)
    d.polygon(body, fill=(58, 60, 66, 255))                         # dark rim, keeps it readable on light walls
    inner = Image.new("L", size, 0)
    ImageDraw.Draw(inner).polygon(body, fill=255)
    inner = inner.filter(ImageFilter.MinFilter(2 * SS - 1))         # body shrunk by ~1 unit
    white = Image.new("RGBA", size, (238, 239, 242, 255))
    shade = Image.linear_gradient("L").resize(size).point(lambda v: int(v * 0.16))
    white = Image.composite(Image.new("RGBA", size, (196, 199, 206, 255)), white, shade)
    image.paste(white, (0, 0), inner)

    # Black centre section with the sticks, as on the real pad.
    d.polygon(scaled(CENTRE), fill=(24, 25, 29, 255))
    # Touchpad.
    d.rounded_rectangle([33 * SS, 9.5 * SS, 63 * SS, 27 * SS], radius=4 * SS, fill=(228, 230, 234, 255), outline=(150, 153, 160, 255), width=SS)
    # Light bar edges either side of the touchpad.
    for x in (31.2, 64.8):
        d.line([(x * SS, 11 * SS), (x * SS, 25 * SS)], fill=(120, 170, 255, 255), width=int(1.2 * SS))
    # Create and Options.
    for x in (26.5, 69.5):
        d.rounded_rectangle([(x - 1.2) * SS, 10.5 * SS, (x + 1.2) * SS, 15 * SS], radius=SS, fill=(70, 72, 78, 255))
    # Sticks.
    for x in (37.5, 58.5):
        d.ellipse([(x - 6.2) * SS, (39.5 - 6.2) * SS, (x + 6.2) * SS, (39.5 + 6.2) * SS], fill=(8, 8, 10, 255))
        d.ellipse([(x - 4.6) * SS, (39.5 - 4.6) * SS, (x + 4.6) * SS, (39.5 + 4.6) * SS], fill=(52, 54, 60, 255))
        d.ellipse([(x - 3.2) * SS, (39.5 - 3.2) * SS, (x + 3.2) * SS, (39.5 + 3.2) * SS], fill=(36, 38, 42, 255))
    # PS button and mute button.
    d.ellipse([46 * SS, 32 * SS, 50 * SS, 36 * SS], fill=(90, 92, 98, 255))
    d.rounded_rectangle([46.3 * SS, 46 * SS, 49.7 * SS, 47.5 * SS], radius=SS // 2, fill=(255, 140, 120, 255))
    # D-pad.
    cx, cy, a = 17, 23, 3.4
    for dx, dy in ((0, -1), (0, 1), (-1, 0), (1, 0)):
        x, y = cx + dx * a, cy + dy * a
        d.rounded_rectangle([(x - 1.7) * SS, (y - 1.7) * SS, (x + 1.7) * SS, (y + 1.7) * SS], radius=SS // 2, fill=(70, 72, 78, 255))
    # Face buttons, tinted like their symbols.
    cx, cy, a = 79, 23, 4.2
    for (dx, dy), colour in zip(((0, -1), (1, 0), (0, 1), (-1, 0)), ((70, 190, 150), (235, 90, 100), (110, 150, 235), (220, 130, 200))):
        x, y = cx + dx * a, cy + dy * a
        d.ellipse([(x - 2.2) * SS, (y - 2.2) * SS, (x + 2.2) * SS, (y + 2.2) * SS], fill=(62, 64, 70, 255))
        d.ellipse([(x - 1.0) * SS, (y - 1.0) * SS, (x + 1.0) * SS, (y + 1.0) * SS], fill=colour + (255,))
    return image


def design_strip():
    """A colour strip along the inside of the bottom edge of the black centre section."""
    layer = Image.new("L", (DESIGN[0] * SS, DESIGN[1] * SS), 0)
    edge = sorted((x, y) for x, y in CENTRE if 30 <= x <= 66 and y > 51)
    top = [(x * SS, (y - 3.6) * SS) for x, y in edge]
    bottom = [(x * SS, (y - 0.4) * SS) for x, y in reversed(edge)]
    ImageDraw.Draw(layer).polygon(top + bottom, fill=255)
    return layer


def design_mask():
    layer = Image.new("L", (DESIGN[0] * SS, DESIGN[1] * SS), 0)
    ImageDraw.Draw(layer).polygon(scaled(BODY), fill=255)
    return layer


def fit(box):
    """Design-to-texel mapping that lays the pad outline over box (where the old pad was)."""
    xs, ys = [x for x, _ in BODY], [y for _, y in BODY]
    sx, sy = (box[2] - box[0]) / (max(xs) - min(xs)), (box[3] - box[1]) / (max(ys) - min(ys))
    return box[0] - sx * min(xs), sx, box[1] - sy * min(ys), sy


def render(image, size, mapping):
    """Resamples a design-space image into a texture; mapping is (ox, sx, oy, sy), texel = o + s * design."""
    ox, sx, oy, sy = mapping
    big = image.transform((size[0] * SS, size[1] * SS), Image.AFFINE,
                          (1 / sx, 0, -ox * SS / sx, 0, 1 / sy, -oy * SS / sy), resample=Image.BICUBIC)
    return big.resize(size, Image.LANCZOS)


def alpha_image(mask, colour=(255, 255, 255, 255)):
    image = Image.new("RGBA", mask.size, colour[:3] + (0,))
    image.putalpha(mask.point(lambda v: v * colour[3] // 255))
    return image


def controller_art():
    pad, strip, mask = design_pad(), design_strip(), design_mask()
    for name, target in TARGETS.items():
        mapping = fit(target["box"])
        render(pad, target["size"], mapping).save(os.path.join(HERE, f"{name}.png"), optimize=True)
        # Strip texel (u, v) -> pad texel (bx + bsx*u, by + bsy*v) -> design (texel - o) / s.
        bx, bsx, by, bsy = target["bar"]
        ox, sx, oy, sy = mapping
        bar = (ox - bx) / bsx, sx / bsx, (oy - by) / bsy, sy / bsy
        alpha_image(render(strip, target["bar_size"], bar)).save(os.path.join(HERE, f"{name}_bar.png"), optimize=True)
        if "silhouette" in target:
            s = target["silhouette"]
            alpha_image(render(mask, s["size"], fit(s["box"])), s["colour"]).save(os.path.join(HERE, f"{name}_silhouette.png"), optimize=True)
        print(name, target["size"])


if __name__ == "__main__":
    for name, draw in GLYPHS.items():
        mask = draw()
        mask = mask.crop(mask.getbbox())
        glyph = Image.new("RGBA", mask.size, (255, 255, 255, 0))
        glyph.putalpha(mask)
        glyph.save(os.path.join(HERE, f"{name}.png"), optimize=True)
        print(name, mask.size)

    controller_art()
