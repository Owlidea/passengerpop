"""Imports the second media pack (textures, posters, extra chapter pictures, chest/gift states, extra conductor
poses) from MetroSort-Media/Gorseller. Runs inside Blender, which brings its own numpy, so no pip installs:

    Blender --background --python Tools/art/media_extra.py

Tiling textures and posters go to Assets/MetroSort/Resources/Textures (plain textures, sampled by the toy
shader); sprites go to Resources/Art next to the ones written by media_import.py.
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


def trim(a, pad=4, threshold=0.15):
    ys, xs = np.where(a[..., 3] > threshold)
    return a[max(ys.min() - pad, 0):ys.max() + pad + 1, max(xs.min() - pad, 0):xs.max() + pad + 1]


def matte(a, bright=9):
    """Removes the flat grey studio backdrop (and the soft shadow on it): low-saturation pixels close to the
    corner colour or darker that are connected to the picture border become transparent. bright is how much
    lighter than the backdrop still counts as backdrop (raise it for pictures with a glow on the floor)."""
    rgb = a[..., :3] * 255.0
    bg = np.median(np.concatenate([rgb[:8].reshape(-1, 3), rgb[-8:].reshape(-1, 3)]), axis=0)
    diff = rgb - bg
    chroma = rgb.max(axis=2) - rgb.min(axis=2)
    lum = diff.mean(axis=2)
    spread = np.abs(diff - lum[..., None]).max(axis=2)
    candidate = (spread < 7) & (lum < bright) & (lum > -75) & (chroma < 26)
    reach = np.zeros_like(candidate)
    reach[0], reach[-1], reach[:, 0], reach[:, -1] = candidate[0], candidate[-1], candidate[:, 0], candidate[:, -1]
    for _ in range(6000):   # flood fill from the border, one pixel ring per pass
        grow = reach.copy()
        grow[1:] |= reach[:-1]; grow[:-1] |= reach[1:]; grow[:, 1:] |= reach[:, :-1]; grow[:, :-1] |= reach[:, 1:]
        grow &= candidate
        if (grow == reach).all():
            break
        reach = grow
    back = reach.copy()     # widen by a pixel so no grey rim is left on the figure
    back[1:] |= reach[:-1]; back[:-1] |= reach[1:]; back[:, 1:] |= reach[:, :-1]; back[:, :-1] |= reach[:, 1:]
    alpha = (~back).astype(np.float32)
    pad = np.pad(alpha, 1, mode='edge')
    alpha = sum(pad[dy:dy + alpha.shape[0], dx:dx + alpha.shape[1]] for dy in range(3) for dx in range(3)) / 9.0
    out = a.copy()
    out[..., 3] = np.clip((alpha - 0.5) * 2.0, 0, 1)
    return out


def clean_halo(a, near=6, far=22, grey=40):
    """Clears what the matte leaves around a figure: the studio floor shadow and white glow puffs, which show as
    pale blotches on the game's dark reward backdrop. Grey pixels (chroma below `grey`) more than `near` pixels
    from the strongly coloured figure fade out, gone by `far`; coloured bits (coins, confetti) are kept."""
    rgb = a[..., :3] * 255.0
    chroma = rgb.max(axis=2) - rgb.min(axis=2)
    ring = (a[..., 3] > 0.5) & (chroma > 60)
    dist = np.where(ring, 0.0, far + 1.0).astype(np.float32)
    for d in range(1, far + 1):     # grow the figure one pixel ring at a time
        grow = ring.copy()
        grow[1:] |= ring[:-1]; grow[:-1] |= ring[1:]; grow[:, 1:] |= ring[:, :-1]; grow[:, :-1] |= ring[:, 1:]
        dist[grow & ~ring] = d
        ring = grow
    keep = np.clip((far - dist) / float(far - near), 0, 1)
    out = a.copy()
    out[..., 3] = np.where(chroma < grey, a[..., 3] * keep, a[..., 3])
    return out


def cells(name, names, size, bright=9, clean=False):
    """Splits a sheet of equally spaced figures on a grey backdrop into one trimmed sprite each."""
    sheet = load(name)
    sheet = matte(fit(sheet, 2048), bright)
    step = sheet.shape[1] // len(names)
    for i, out in enumerate(names):
        cell = sheet[:, i * step:(i + 1) * step]
        save(fit(trim(clean_halo(cell) if clean else cell), size), ART, out)


# Tiling surfaces for the 3D station and the wall posters.
for name in ("floor", "wall", "platform", "track"):
    im = resize(load("tex-" + name), 512, 512)
    if name == "track":
        # The painted sleepers are a loud orange: pull them towards warm grey-brown so the board stays calm.
        grey = im[..., :3].mean(axis=2, keepdims=True)
        im[..., :3] = (im[..., :3] * 0.45 + grey * 0.55) * np.array([0.92, 0.86, 0.80])
    save(im, TEX, "tex_" + name)
for i, name in enumerate(("balloon", "city", "seaside", "mountain", "garden", "train")):
    save(resize(load("poster-" + name), 384, 512), TEX, "poster_%d" % i)

# The painted station walls: the top of each chapter painting (arches, lamps, pictures) becomes the back wall
# of the 3D station. Order matches Chapters.PaintingFor.
PAINTINGS = ("central-line", "old-town", "garden", "palace", "harbour",
             "crimson", "emerald", "sapphire", "amber", "ivory", "northern", "southern", "eastern", "western", "uptown")
for i, name in enumerate(PAINTINGS):
    im = load(name)
    save(resize(im[:int(im.shape[0] * 0.375)], 2048, 512), TEX, "wall_%d" % i)   # 4:1, so not stretched

# Ten more chapter banners (5-14), cut like the first five: the wall and track strip across the top.
for i, name in enumerate(("crimson", "emerald", "sapphire", "amber", "ivory", "northern", "southern", "eastern", "western", "uptown")):
    im = load(name)
    w = im.shape[1]
    save(resize(im[:w // 3], 1200, 400), ART, "chapter_%d" % (i + 5))

# Reward boxes open on a dark backdrop (UIManager.RevealRoutine): clean_halo removes the pale floor shadow.
cells("chest-states", ("chest_0", "chest_1", "chest_2"), 512, bright=90, clean=True)
cells("gift-states", ("gift_0", "gift_1", "gift_2"), 512, bright=90, clean=True)
cells("conductor-extra", ("conductor_think", "conductor_surprised", "conductor_thumbs", "conductor_idea"), 640)
