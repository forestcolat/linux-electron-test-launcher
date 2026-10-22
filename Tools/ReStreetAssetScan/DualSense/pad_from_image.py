"""Builds the Select Sides controller textures from your own controller picture.

The picture must already be a cut-out (real alpha; see cutout.py). It is fitted, without
distortion, where the Xbox 360 pad was. The player-colour strip is made from the picture's thick
dark parts (a DualSense's black inner grips) inside the area where the game draws the strip, and
the empty-slot silhouette from its alpha.

    python Tools/ReStreetAssetScan/DualSense/pad_from_image.py <cutout.png> <output folder> [accents|pill|none]

Strip styles: "accents" colours the dark inner grips, "pill" draws a player-colour bar under the
centre of the pad (default), "none" leaves the pad without player colour.

Then patch with:  ReStreetAssetScan dualsense apply <game folder> --art <output folder>
Keep pictures you did not make yourself (and what this builds from them) out of Git.
"""

import os
import sys

from PIL import Image, ImageFilter

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from make_glyphs import SS, TARGETS  # noqa: E402  (shared measured layout)

source, out = sys.argv[1], sys.argv[2]
style = sys.argv[3] if len(sys.argv) > 3 else "pill"
os.makedirs(out, exist_ok=True)
pad = Image.open(source).convert("RGBA")
W, H = pad.size

# Dark, thick parts of the picture: candidates for the colour strip (thin outlines are opened away).
grey = pad.convert("L")
alpha = pad.getchannel("A")
dark = Image.eval(Image.merge("LA", (grey, alpha)).convert("LA").getchannel("L"), lambda v: 255 if v < 100 else 0)
dark = Image.composite(dark, Image.new("L", pad.size, 0), alpha.point(lambda v: 255 if v > 200 else 0))
k = max(3, (W // 90) | 1)                                   # ~1 texel at the final size
dark = dark.filter(ImageFilter.MinFilter(k)).filter(ImageFilter.MaxFilter(k)).filter(ImageFilter.GaussianBlur(k / 4))


def fit(box):
    """Picture-to-texel mapping (texel = o + s * pixel) that fits the picture into box, centred."""
    bw, bh = box[2] - box[0], box[3] - box[1]
    s = min(bw / W, bh / H)
    return box[0] + (bw - s * W) / 2, box[1] + (bh - s * H) / 2, s


def render(image, size, ax, cx, ay, cy):
    """Resamples image into a texture of size; input pixel = a * texel + c (premultiplied, supersampled)."""
    mode = image.mode
    work = image.convert("RGBa") if mode == "RGBA" else image
    big = work.transform((size[0] * SS, size[1] * SS), Image.AFFINE, (ax / SS, 0, cx, 0, ay / SS, cy), resample=Image.BILINEAR)
    small = big.resize(size, Image.LANCZOS)
    return small.convert("RGBA") if mode == "RGBA" else small


for name, target in TARGETS.items():
    ox, oy, s = fit(target["box"])
    render(pad, target["size"], 1 / s, -ox / s, 1 / s, -oy / s).save(os.path.join(out, f"{name}.png"))

    # The game only draws the strip right of bx and below by; keep it symmetric about the pad's
    # centre and fade it in at those edges so the cut does not show.
    bx, bsx, by, bsy = target["bar"]
    centre = (target["box"][0] + target["box"][2]) / 2
    left, right, fade_x, fade_y = bx + 0.5, 2 * centre - (bx + 0.5), 3.0, 4.0
    weight = Image.new("L", pad.size, 0)
    wp = weight.load()
    columns = [max(0.0, min(1.0, (t - left) / fade_x, (right - t) / fade_x)) for t in (ox + s * (x + 0.5) for x in range(W))]
    rows = [max(0.0, min(1.0, (t - by) / fade_y)) for t in (oy + s * (y + 0.5) for y in range(H))]
    for y in range(H):
        if rows[y] == 0: continue
        for x in range(W):
            if columns[x]: wp[x, y] = int(255 * rows[y] * columns[x])
    if style == "accents":
        masked = Image.frombytes("L", pad.size, bytes(d * w // 255 for d, w in zip(dark.tobytes(), weight.tobytes())))
        # Strip texel (u, v) -> pad texel (bx + bsx*u, by + bsy*v) -> picture pixel (texel - o) / s.
        strip = render(masked, target["bar_size"], bsx / s, (bx - ox) / s, bsy / s, (by - oy) / s)
    else:
        # A rounded bar centred under the pad, in the gap between the grips (pad texels).
        bw = (target["box"][2] - target["box"][0]) * 0.3
        rect = (centre - bw / 2, by + 1.5, centre + bw / 2, by + 6.0)
        layer = Image.new("L", (target["bar_size"][0] * SS, target["bar_size"][1] * SS), 0)
        if style == "pill":
            from PIL import ImageDraw
            u0, v0, u1, v1 = ((rect[0] - bx) / bsx, (rect[1] - by) / bsy, (rect[2] - bx) / bsx, (rect[3] - by) / bsy)
            ImageDraw.Draw(layer).rounded_rectangle([u0 * SS, v0 * SS, u1 * SS, v1 * SS], radius=(v1 - v0) * SS / 2, fill=255)
        strip = layer.resize(target["bar_size"], Image.LANCZOS)
    bar = Image.new("RGBA", target["bar_size"], (255, 255, 255, 0))
    bar.putalpha(strip.point(lambda v: v * 235 // 255))
    bar.save(os.path.join(out, f"{name}_bar.png"))

    if "silhouette" in target:
        sil = target["silhouette"]
        sx, sy, ss = fit(sil["box"])
        mask = render(alpha, sil["size"], 1 / ss, -sx / ss, 1 / ss, -sy / ss)
        image = Image.new("RGBA", sil["size"], sil["colour"][:3] + (0,))
        image.putalpha(mask.point(lambda v: v * sil["colour"][3] // 255))
        image.save(os.path.join(out, f"{name}_silhouette.png"))
    print(name, target["size"], "scale", round(s, 4))
