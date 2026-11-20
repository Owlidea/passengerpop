"""Imports the third media pack from MetroSort-Media/Gorseller: station paintings for the twenty lines that had
none, the settings icons and the level map background. Runs inside Blender (it brings numpy):

    Blender --background --python Tools/art/media_lines.py

Each painting is cut like the first fifteen (Tools/art/media_extra.py): its top becomes the 3D station's back
wall (Resources/Textures/wall_N, 4:1) and its top third the line's banner on the level map
(Resources/Art/chapter_N). Painting numbers 15-34 follow on from 0-14; Chapters.PaintingFor maps lines to them.
"""
import bpy, os
import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC = os.path.join(ROOT, "MetroSort-Media", "Gorseller")
ART = os.path.join(ROOT, "Assets", "MetroSort", "Resources", "Art")
TEX = os.path.join(ROOT, "Assets", "MetroSort", "Resources", "Textures")


def load(name):
    """RGBA float array, top row first."""
    img = bpy.data.images.load(os.path.join(SRC, name + ".png"))
    w, h = img.size
    a = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)[::-1].copy()
    bpy.data.images.remove(img)
    return a


def save(a, folder, name):
    os.makedirs(folder, exist_ok=True)
    h, w = a.shape[:2]
    img = bpy.data.images.new(name, w, h, alpha=True)
    img.alpha_mode = 'STRAIGHT'
    img.pixels = np.clip(a[::-1], 0, 1).astype(np.float32).ravel()
    img.filepath_raw = os.path.join(folder, name + ".png")
    img.file_format = 'PNG'
    img.save()
    bpy.data.images.remove(img)
    print("saved", name, (w, h))


def resize(a, w, h):
    """Box-filters down by a whole factor, then samples bilinearly to the exact size."""
    sh, sw = a.shape[:2]
    f = max(1, min(sw // w, sh // h))
    if f > 1:
        a = a[:sh // f * f, :sw // f * f].reshape(sh // f, f, sw // f, f, 4).mean(axis=(1, 3))
        sh, sw = a.shape[:2]
    ys = (np.arange(h) + 0.5) * sh / h - 0.5
    xs = (np.arange(w) + 0.5) * sw / w - 0.5
    y0 = np.clip(np.floor(ys).astype(int), 0, sh - 1); y1 = np.clip(y0 + 1, 0, sh - 1); fy = np.clip(ys - y0, 0, 1)[:, None, None]
    x0 = np.clip(np.floor(xs).astype(int), 0, sw - 1); x1 = np.clip(x0 + 1, 0, sw - 1); fx = np.clip(xs - x0, 0, 1)[None, :, None]
    top = a[y0][:, x0] * (1 - fx) + a[y0][:, x1] * fx
    bottom = a[y1][:, x0] * (1 - fx) + a[y1][:, x1] * fx
    return top * (1 - fy) + bottom * fy


def fit(a, size):
    h, w = a.shape[:2]
    k = size / float(max(w, h))
    return a if k >= 1 else resize(a, max(1, round(w * k)), max(1, round(h * k)))


# Painting numbers 15-34, in this order (lines 5-19, then 30-34).
LINES = ("misty", "night", "grand", "polar", "capital", "coastal", "desert", "alpine", "tropic", "royal",
         "lakeside", "canyon", "island", "aurora", "golden", "spring", "summer", "autumn", "winter", "festival")
for i, name in enumerate(LINES):
    n = 15 + i
    im = load(name)
    h, w = im.shape[:2]
    save(resize(im[:int(h * 0.375)], 2048, 512), TEX, "wall_%d" % n)   # 4:1, so not stretched
    save(resize(im[:w // 3], 1200, 400), ART, "chapter_%d" % n)

for name in ("music", "sound", "tap", "motion", "fast", "language"):
    save(fit(load("icon-" + name), 256), ART, "icon_" + name)

save(fit(load("map-bg"), 1024), ART, "map_bg")
