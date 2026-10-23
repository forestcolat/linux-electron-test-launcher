"""Builds the controls-screen pictures (the ones with call-out lines) from your own controller picture.

On the PlayStation layout of those screens the lines end on the buttons of the game's DualShock 3
photo. The picture is scaled (uniformly, or very nearly so) and moved so that its buttons land on
those ends as closely as possible, while it stays inside the part of the texture the game draws.

    python Tools/ReStreetAssetScan/DualSense/controls_from_image.py <cutout.png> <points.json> <output folder>

points.json gives the picture's button centres in pixels, for example
    {"L2": [210, 40], "L1": [200, 130], "R2": [1010, 40], "R1": [1020, 130], "D-pad": [208, 355],
     "Triangle": [1012, 285], "Square": [918, 362], "Circle": [1100, 357], "Cross": [1010, 435],
     "L3": [410, 510], "R3": [820, 510]}
The picture should be shot from above, like the DualShock 3 photo (triggers visible behind the
bumpers), or the shoulder and stick lines cannot meet their buttons.
"""

import json
import os
import sys

import numpy as np
from PIL import Image

# Where each line ends, on the DualShock 3 photo of the 512x256 texture (loading screens, Game
# Manual), measured in texels. The 1024x512 copy (controller settings) is the same photo x1.45.
ENDS = {
    "L2": (78, 37), "L1": (73, 61), "R2": (322, 37), "R1": (323, 60), "D-pad": (80, 152),
    "Triangle": (328, 128), "Square": (288, 153), "Circle": (357, 155), "Cross": (320, 175),
    "L3": (140, 205), "R3": (260, 205),
}
TARGETS = {
    # name: (texture size, visible area drawn by the game, small-photo texel -> this texture)
    "controls": ((512, 256), (400, 250), lambda x, y: (x, y)),
}
# The large copy draws only to y = 297, right under the photo, so fitting the whole pad there would
# shrink it off the line ends. It takes the small fit instead; the game clips the grip tips.
LARGE = ((1024, 512), 1.45, (14, 25), (5, 3))
SHOULDER_WEIGHT = 0.5   # the photo's angle differs most at the shoulders; let the main buttons decide
SS = 4

source, points_file, out = sys.argv[1:4]
os.makedirs(out, exist_ok=True)
pad = Image.open(source).convert("RGBA")
W, H = pad.size
points = json.load(open(points_file, encoding="utf-8"))
names = [n for n in ENDS if n in points]
Q = np.array([points[n] for n in names], float)
weights = np.array([SHOULDER_WEIGHT if n[0] in "LR" and n[1] in "12" else 1.0 for n in names])

def place(file, size, sx, sy, tx, ty):
    """Premultiplied, supersampled resize so the transparent edge gets no halo."""
    big = pad.convert("RGBa").resize((round(W * sx * SS), round(H * sy * SS)), Image.LANCZOS)
    canvas = Image.new("RGBa", (size[0] * SS, size[1] * SS), (0, 0, 0, 0))
    canvas.paste(big, (round(tx * SS), round(ty * SS)))
    canvas.resize(size, Image.LANCZOS).convert("RGBA").save(os.path.join(out, file))


for name, (size, visible, to_texture) in TARGETS.items():
    P = np.array([to_texture(*ENDS[n]) for n in names], float)
    best = None
    for sx in np.linspace(0.05, visible[0] / W, 400):
        for aspect in np.linspace(0.92, 1.08, 17):      # at most 8% squash or stretch
            sy = sx * aspect
            if H * sy > visible[1]:
                continue
            tx = np.clip(np.sum(weights * (P[:, 0] - sx * Q[:, 0])) / weights.sum(), 0, visible[0] - W * sx)
            ty = np.clip(np.sum(weights * (P[:, 1] - sy * Q[:, 1])) / weights.sum(), 0, visible[1] - H * sy)
            err = np.linalg.norm(np.stack([sx * Q[:, 0] + tx, sy * Q[:, 1] + ty], 1) - P, axis=1)
            score = np.sum(weights * err) / weights.sum()
            if best is None or score < best[0]:
                best = (score, sx, sy, tx, ty, err)
    score, sx, sy, tx, ty, err = best
    place(f"{name}.png", size, sx, sy, tx, ty)
    print(f"{name}: picture {W * sx:.0f}x{H * sy:.0f} at ({tx:.0f},{ty:.0f}) of {visible[0]}x{visible[1]} visible, "
          f"mean error {score:.1f} texels ({', '.join(f'{n} {e:.0f}' for n, e in zip(names, err))})")

    size, k, (fx, fy), (gx, gy) = LARGE
    place("controls_large.png", size, sx * k, sy * k, (tx - fx) * k + gx, (ty - fy) * k + gy)
    print(f"controls_large: same placement x{k} ({W * sx * k:.0f}x{H * sy * k:.0f} at ({(tx - fx) * k + gx:.0f},{(ty - fy) * k + gy:.0f}))")
