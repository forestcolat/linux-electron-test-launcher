"""Removes a "fake transparency" checkerboard that was saved into an image's pixels.

The background is flood-filled from the image border through light, neutral (grey/white) pixels;
it stops at the subject's dark outline. Edge pixels that mix the outline with the checkerboard get
partial alpha, so the cut-out stays smooth.

    python Tools/ReStreetAssetScan/DualSense/cutout.py <input.png> <output.png>
"""

import sys
from collections import deque

from PIL import Image, ImageFilter

source, target = sys.argv[1], sys.argv[2]
image = Image.open(source).convert("RGB")
w, h = image.size
px = image.load()


def checker(p, light=150):
    return min(p) >= light and max(p) - min(p) <= 12


outside = bytearray(w * h)
queue = deque()
for x in range(w):
    for y in (0, h - 1):
        if checker(px[x, y]): outside[y * w + x] = 1; queue.append((x, y))
for y in range(h):
    for x in (0, w - 1):
        if checker(px[x, y]): outside[y * w + x] = 1; queue.append((x, y))
while queue:
    x, y = queue.popleft()
    for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
        if 0 <= nx < w and 0 <= ny < h and not outside[ny * w + nx] and checker(px[nx, ny]):
            outside[ny * w + nx] = 1; queue.append((nx, ny))

alpha = Image.frombytes("L", (w, h), bytes(0 if o else 255 for o in outside))
# Soften the cut by one pixel so the outline's anti-aliasing is kept rather than a hard step.
alpha = alpha.filter(ImageFilter.MinFilter(3)).filter(ImageFilter.GaussianBlur(0.8))
result = image.convert("RGBA")
result.putalpha(alpha)
box = alpha.point(lambda v: 255 if v > 8 else 0).getbbox()
result.crop(box).save(target)
print(target, "subject", box)
