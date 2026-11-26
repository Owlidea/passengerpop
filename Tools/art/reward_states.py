"""Cuts the sprite sheets the game shows on its dark panels, with media_extra.py's own helpers but without rerunning
the rest of that script (it also rewrites station textures and chapter pictures):

    Blender --background --python Tools/art/reward_states.py

- chest-states.png, gift-states.png -> Art/chest_0..2, gift_0..2 (the reward box opening)
- boosters.png                      -> Art/booster_undo, booster_hint, booster_platform, booster_shuffle
- train-icons.png                   -> Art/train_icon_0..5 (the next-trains strip, in Palette.Lines order)
- shop-packs.png                    -> Art/pack_starter, pack_traveller, pack_conductor, pack_station (the shop's
                                       starter pack and bundles; MetroSort-Media/Magaza-Paket-Promptu.md)

Sheets that are not in MetroSort-Media/Gorseller yet are skipped. Prompts: MetroSort-Media/HUD-Gorsel-Promptlari.md.
"""
import ast, os

SCRIPT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "media_extra.py")
tree = ast.parse(open(SCRIPT).read())
# Keep imports, path constants and functions; skip the module-level import steps.
keep = [n for n in tree.body if isinstance(n, (ast.Import, ast.ImportFrom, ast.FunctionDef, ast.Assign))]
helpers = {"__file__": SCRIPT}
exec(compile(ast.Module(body=keep, type_ignores=[]), SCRIPT, "exec"), helpers)

# (sheet, sprites, size, bright, clean). The boxes stand in glowing light on a lit floor, hence the high `bright`
# and the halo cleanup. The icons have large white and cream areas (the platform's tiles, the shuffle arrows, the
# headlights) that a high `bright` or the cleanup would eat: they keep the default matte, which only takes the
# backdrop grey and the shadow on it.
SHEETS = [
    ("chest-states", ("chest_0", "chest_1", "chest_2"), 512, 90, True),
    ("gift-states", ("gift_0", "gift_1", "gift_2"), 512, 90, True),
    ("boosters", ("booster_undo", "booster_hint", "booster_platform", "booster_shuffle"), 256, 9, False),
    ("train-icons", tuple("train_icon_%d" % i for i in range(6)), 256, 9, False),
]
for name, names, size, bright, clean in SHEETS:
    if not os.path.exists(os.path.join(helpers["SRC"], name + ".png")):
        print("skipped", name, "(not in Gorseller yet)")
        continue
    helpers["cells"](name, names, size, bright=bright, clean=clean)


def grow(m, n=1):
    """Widens a mask by n pixels."""
    for _ in range(n):
        g = m.copy()
        g[1:] |= m[:-1]; g[:-1] |= m[1:]; g[:, 1:] |= m[:, :-1]; g[:, :-1] |= m[:, 1:]
        m = g
    return m


def flood(seed, through):
    """The parts of `through` that `seed` reaches without leaving it."""
    reach = seed & through
    while True:
        g = grow(reach) & through
        if (g == reach).all():
            return reach
        reach = g


def pockets(raw, sheet, bg):
    """Backdrop that shows through a closed gap (inside the satchel's strap, between the wagon's rails) is out of
    reach of the matte's fill from the border: flat patches of the exact backdrop grey go as well, with their rim."""
    np = helpers["np"]
    dev = np.abs(raw[..., :3] - bg).max(axis=2) * 255
    hole = grow(~grow(~(dev < 7)), 2) & (dev < 20)     # patches at least three pixels across
    h, w = hole.shape
    pad = np.pad(hole.astype(np.float32), 1, mode='edge')
    soft = sum(pad[dy:dy + h, dx:dx + w] for dy in range(3) for dx in range(3)) / 9.0
    out = sheet.copy()
    out[..., 3] = np.minimum(sheet[..., 3], 1 - soft)
    return out


def glow(a, bg, near=3, far=6):
    """Sparkles drawn right beside coins: their glow, warmed by the coins, is not grey enough for the matte and
    stays as a pale opaque haze, a dirty smudge on the shop's coloured cards. Each haze region that holds a
    sparkle and touches the cleared backdrop is unmixed from the grey: what is lighter becomes that light at the
    opacity it was painted with, the rest goes, and where the haze meets the figure the pixels are unmixed too so
    the edge stays soft. The figure (clearly darker than the backdrop or strongly coloured) is left alone, and so
    are pale regions without a sparkle (the rails) or enclosed by the figure (the gems' facets)."""
    np = helpers["np"]
    rgb, al = a[..., :3], a[..., 3]
    lum = (rgb.mean(axis=2) - bg.mean()) * 255
    chroma = (rgb.max(axis=2) - rgb.min(axis=2)) * 255
    body = (al > 0.5) & ((lum < -15) | (chroma > 120))
    ring = grow(body, near)
    pale = (al > 0) & ~ring
    # star cores: bright pale blobs clear of the figure (thin glints on the rails do not survive the shrink)
    stars = ~grow(~((al > 0.5) & (lum > 20) & (chroma < 76)), 2) & ~grow(body, far)
    zone = grow(flood(stars, pale) & flood(grow(al < 0.05) & pale, pale), near)
    d = rgb - bg
    # the least opaque colour that, laid over the backdrop, gives the pixel: light added or taken away
    alpha = np.clip(np.maximum(d / np.maximum(1 - bg, 1e-3), -d / np.maximum(bg, 1e-3)).max(axis=2), 0, 1)
    colour = np.clip(bg + d / np.maximum(alpha, 1e-3)[..., None], 0, 1)
    edge = zone & ~body & ring
    light = zone & ~ring & (lum > 1)
    gone = zone & ~ring & ~light
    alpha = np.where(light, alpha * np.clip((lum - 4) / 8.0, 0, 1), alpha)     # the faintest haze fades out
    out = a.copy()
    out[..., :3] = np.where((edge | light)[..., None], colour, rgb)
    out[..., 3] = np.where(edge | light, np.minimum(al, alpha), np.where(gone, 0, al))
    return out


def figures(name, names, size, bright=9, glowing=()):
    """Cuts a sheet whose figures are not evenly spaced (the shop packs: a wagon's rails and loose sparkles reach
    past the quarter lines): the sheet is split in the middle of its widest empty column gaps. The sprites named
    in `glowing` get their sparkle haze unmixed (glow)."""
    np = helpers["np"]
    raw = helpers["fit"](helpers["load"](name), 2048)
    bg = np.median(np.concatenate([raw[:8].reshape(-1, 4), raw[-8:].reshape(-1, 4)]), axis=0)[:3]
    sheet = pockets(raw, helpers["matte"](raw, bright), bg)
    solid = (sheet[..., 3] > 0.15).any(axis=0)
    xs = np.nonzero(solid)[0]
    gaps, start = [], None
    for x in range(xs.min(), xs.max() + 1):
        if not solid[x] and start is None:
            start = x
        elif solid[x] and start is not None:
            gaps.append((x - start, start, x))
            start = None
    cuts = sorted((a + b) // 2 for _, a, b in sorted(gaps, reverse=True)[:len(names) - 1])
    edges = [0] + cuts + [sheet.shape[1]]
    for i, out in enumerate(names):
        cell = sheet[:, edges[i]:edges[i + 1]]
        if out in glowing:
            cell = glow(cell, bg)
        helpers["save"](helpers["fit"](helpers["trim"](cell), size), helpers["ART"], out)
    print(name, "cut at", cuts)


# The satchel's and the wagon's sparkles sit against the coins and leave haze. The gift's and the suitcase's stand
# clear and need no help; their cream ticket and lining are as pale as haze and would be unmixed with it.
if os.path.exists(os.path.join(helpers["SRC"], "shop-packs.png")):
    figures("shop-packs", ("pack_starter", "pack_traveller", "pack_conductor", "pack_station"), 512,
            glowing=("pack_conductor", "pack_station"))
else:
    print("skipped shop-packs (not in Gorseller yet)")
