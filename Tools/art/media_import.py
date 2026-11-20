"""Cuts the hand-supplied media pack (MetroSort-Media/) into game-ready files.

    python3 Tools/art/media_import.py            (needs: pip install pillow numpy scipy)

Writes sprites to Assets/MetroSort/Resources/Art, audio to Assets/MetroSort/Resources/Audio and the store
icon to Assets/MetroSort/Art/AppIcon.png. Unlike the other scripts here the sources are finished pictures,
so this only trims, splits, mattes and resizes them. UiKit.Icon prefers Resources/Art over Resources/UI,
which is how these replace the Blender-rendered icons of the same name.
"""
import os
import shutil
import sys

import numpy as np
from PIL import Image, ImageOps
from scipy import ndimage

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC = os.path.join(ROOT, "MetroSort-Media")
ART = os.path.join(ROOT, "Assets/MetroSort/Resources/Art")
AUDIO = os.path.join(ROOT, "Assets/MetroSort/Resources/Audio")
ICON = os.path.join(ROOT, "Assets/MetroSort/Art/AppIcon.png")


def load(name):
    return Image.open(os.path.join(SRC, "Gorseller", name + ".png")).convert("RGBA")


def trim(im, pad=4, threshold=6):
    """Crops to the visible pixels, leaving a small transparent margin so bilinear edges stay clean."""
    alpha = np.array(im)[..., 3]
    ys, xs = np.where(alpha > threshold)
    box = (max(xs.min() - pad, 0), max(ys.min() - pad, 0), min(xs.max() + pad + 1, im.width), min(ys.max() + pad + 1, im.height))
    return im.crop(box)


def fit(im, size):
    """Scales down so the longer side is at most size."""
    scale = size / float(max(im.size))
    if scale >= 1.0:
        return im
    return im.resize((max(1, round(im.width * scale)), max(1, round(im.height * scale))), Image.LANCZOS)


def bleed(im):
    """Copies edge colours into fully transparent pixels so filtering never pulls in black fringes."""
    if im.mode != "RGBA":
        return im
    a = np.array(im)
    solid = a[..., 3] > 0
    if solid.all() or not solid.any():
        return im
    idx = ndimage.distance_transform_edt(~solid, return_distances=False, return_indices=True)
    rgb = a[..., :3][idx[0], idx[1]]
    a[..., :3] = np.where(solid[..., None], a[..., :3], rgb)
    return Image.fromarray(a)


def save(im, name):
    bleed(im).save(os.path.join(ART, name + ".png"), optimize=True)
    print("art  ", name, im.size)


def matte(im):
    """Removes a flat studio backdrop (and the soft floor shadow on it) from an opaque picture.

    Backdrop pixels are low in saturation, close to the corner colour or darker (shadow), and connected to
    the picture border. The mask is eroded by a pixel and feathered so no grey rim is left on the figure.
    """
    rgb = np.array(im.convert("RGB")).astype(np.float32)
    bg = np.median(np.concatenate([rgb[:8].reshape(-1, 3), rgb[-8:].reshape(-1, 3)]), axis=0)
    diff = rgb - bg
    chroma = rgb.max(axis=2) - rgb.min(axis=2)
    lum = diff.mean(axis=2)
    spread = np.abs(diff - lum[..., None]).max(axis=2)
    # Same hue as the backdrop, from a little brighter down to shadow-dark, and not a coloured surface.
    candidate = (spread < 7) & (lum < 9) & (lum > -75) & (chroma < 26)
    labels, _ = ndimage.label(candidate)
    border = np.unique(np.concatenate([labels[0], labels[-1], labels[:, 0], labels[:, -1]]))
    backdrop = np.isin(labels, border[border > 0])
    backdrop = ndimage.binary_dilation(backdrop, iterations=1)
    alpha = ndimage.gaussian_filter((~backdrop).astype(np.float32), 0.8)
    alpha = np.clip((alpha - 0.5) * 2.0, 0.0, 1.0)
    out = np.dstack([rgb, alpha * 255.0]).astype(np.uint8)
    return Image.fromarray(out)


def import_logo():
    # Keep the main menu's existing sprite bounds as the new mark is taller.
    logo = ImageOps.pad(trim(load("logo-v2")), (1207, 997), method=Image.Resampling.LANCZOS)
    save(logo, "logo")


def import_menu_icons():
    for name in ("levels", "shop", "daily"):
        source = "menu-" + name
        if os.path.isfile(os.path.join(SRC, "Gorseller", source + ".png")):
            save(fit(trim(load(source)), 384), "menu_" + name)


def main():
    if not os.path.isdir(SRC):
        sys.exit("MetroSort-Media/ not found next to the project")
    for folder in (ART, AUDIO, os.path.dirname(ICON)):
        os.makedirs(folder, exist_ok=True)

    import_logo()
    import_menu_icons()
    save(load("key-art").convert("RGB"), "keyart")

    # Reward sheet: star, empty slot, ribbon and rays sit in fixed quarters of the sheet.
    sheet = load("rewards")
    parts = {"star": (120, 0, 740, 530), "star_off": (800, 0, 1400, 530), "ribbon": (30, 580, 905, 960), "rays": (915, 505, 1440, 1010)}
    for name, box in parts.items():
        save(fit(trim(sheet.crop(box)), 640 if name in ("ribbon", "rays") else 384), name)

    # Shop and booster icons, named after the Resources/UI icons they replace.
    icons = {"hint": "hint", "undo": "undo", "shuffle": "shuffle", "extra-platform": "platform", "daily-gift": "gift",
             "coins": "coins", "coin-chest": "chest"}
    for source, name in icons.items():
        save(fit(trim(load(source)), 384), name)

    # Conductor sheet: four poses side by side on a grey studio backdrop.
    sheet = matte(load("conductor"))
    quarter = sheet.width // 4
    for i, pose in enumerate(("wave", "cheer", "point", "sad")):
        save(fit(trim(sheet.crop((i * quarter, 0, (i + 1) * quarter, sheet.height)), threshold=40), 640), "conductor_" + pose)

    # Station paintings become wide chapter banners: the wall and track strip across the top.
    for i, name in enumerate(("central-line", "old-town", "garden", "palace", "harbour")):
        im = load(name).convert("RGB")
        save(im.crop((0, 0, im.width, im.width // 3)).resize((1200, 400), Image.LANCZOS), "chapter_%d" % i)

    load("app-icon").convert("RGB").resize((1024, 1024), Image.LANCZOS).save(ICON, optimize=True)
    print("icon ", ICON)

    for name in sorted(os.listdir(os.path.join(SRC, "Ses"))):
        if name.endswith(".wav"):
            shutil.copyfile(os.path.join(SRC, "Ses", name), os.path.join(AUDIO, name.replace("-", "_")))
            print("audio", name)


if __name__ == "__main__":
    main()
