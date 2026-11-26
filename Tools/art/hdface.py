"""Premium faces for the HD passengers (used by passenger.py --hd).

The sculpted passengers come from image-to-3D (TRELLIS), which turns the crisp painted face of the concept
picture into a soft, smeared texture. This module paints the face back from the picture itself: every texel on
the front of the head takes its colour from the concept art, projected straight on from the front and lined up
on the eyes. Sides and back keep the scanned texture, blended in as the surface turns away.

It also builds the texture the game tints: original colours everywhere, except the jacket, which keeps only its
shading (around 0.5) and is marked in the alpha channel, so the line colour is applied per texel (no triangle
patches where a whole face of the mesh got the wrong region).
"""
import bpy, colorsys, math
import numpy as np
from mathutils import Vector


def load_rgba(path):
    """RGBA float array of a picture, top row first."""
    img = bpy.data.images.load(path)
    w, h = img.size
    a = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)[::-1].copy()
    bpy.data.images.remove(img)
    return a


def save_rgba(a, path):
    """Writes a top-row-first RGBA array as PNG."""
    h, w = a.shape[:2]
    img = bpy.data.images.new("out", w, h, alpha=True)
    img.alpha_mode = 'STRAIGHT'
    img.pixels = np.clip(a[::-1], 0, 1).astype(np.float32).ravel()
    img.filepath_raw = path
    img.file_format = 'PNG'
    img.save()
    bpy.data.images.remove(img)


def foreground(img):
    """Mask of the character in a concept picture (plain light background)."""
    h, w = img.shape[:2]
    rgb = img[..., :3]
    corners = np.concatenate([rgb[:8, :8].reshape(-1, 3), rgb[:8, -8:].reshape(-1, 3), rgb[-8:, :8].reshape(-1, 3), rgb[-8:, -8:].reshape(-1, 3)])
    bg = np.median(corners, axis=0)
    fg = np.abs(rgb - bg).sum(-1) > 0.10
    if img.shape[2] > 3:
        fg &= img[..., 3] > 0.5
    return fg


def components(mask):
    """4-connected components of a boolean mask: list of (ys, xs) index arrays."""
    h, w = mask.shape
    seen = np.zeros_like(mask, dtype=bool)
    out = []
    ys0, xs0 = np.nonzero(mask)
    for y0, x0 in zip(ys0, xs0):
        if seen[y0, x0]:
            continue
        stack = [(y0, x0)]
        seen[y0, x0] = True
        cy, cx = [], []
        while stack:
            y, x = stack.pop()
            cy.append(y)
            cx.append(x)
            for ny, nx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
                if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and not seen[ny, nx]:
                    seen[ny, nx] = True
                    stack.append((ny, nx))
        out.append((np.array(cy), np.array(cx)))
    return out


def find_eyes(img):
    """Centres (x, y, top-down pixels) and half-sizes of the two eyes in a front concept picture, plus the
    character's height in pixels. Eyes are the two largest dark, roundish blobs side by side in the head."""
    fg = foreground(img)
    ys, xs = np.nonzero(fg)
    top, bottom = ys.min(), ys.max()
    height = bottom - top
    # the head: upper 42% of the figure, the columns around the figure's middle at eye level
    band = slice(top, int(top + 0.42 * height))
    rows = fg[band]
    mid_row = rows[int(rows.shape[0] * 0.55)]
    cols = np.nonzero(mid_row)[0]
    cx = (cols.min() + cols.max()) / 2 if len(cols) else img.shape[1] / 2
    lum = img[..., :3] @ np.array([0.299, 0.587, 0.114], dtype=np.float32)
    dark = (lum < 0.20) & fg
    dark[:top + int(0.10 * height)] = False          # hair at the very top
    dark[int(top + 0.42 * height):] = False
    found = []
    for cy_, cx_ in components(dark):
        area = len(cy_)
        if area < (0.012 * height) ** 2 or area > (0.09 * height) ** 2:
            continue
        y0, y1, x0, x1 = cy_.min(), cy_.max(), cx_.min(), cx_.max()
        bw, bh = x1 - x0 + 1, y1 - y0 + 1
        fill = area / float(bw * bh)
        if fill < 0.45 or not (0.45 < bw / float(bh) < 2.2):
            continue
        found.append((area, (x0 + x1) / 2.0, (y0 + y1) / 2.0, bw / 2.0, bh / 2.0))
    best = None
    for i in range(len(found)):
        for j in range(i + 1, len(found)):
            a, b = found[i], found[j]
            if abs(a[2] - b[2]) > 0.035 * height:
                continue
            if (a[1] - cx) * (b[1] - cx) >= 0:               # one each side of the middle
                continue
            score = min(a[0], b[0])
            if best is None or score > best[0]:
                best = (score, a, b)
    if best is None:
        return find_closed_eyes(img)
    a, b = sorted(best[1:], key=lambda e: e[1])
    return {"left": (a[1], a[2]), "right": (b[1], b[2]), "half": ((a[3] + b[3]) / 2, (a[4] + b[4]) / 2), "height": height, "top": top}


def find_closed_eyes(img):
    """Fallback for closed or narrow eyes (smiling arcs, lines) that find_eyes rejects as not roundish: the two
    largest dark blobs in the eye band of the head, one each side of the middle, level and about as far out."""
    fg = foreground(img)
    ys, xs = np.nonzero(fg)
    top, bottom = ys.min(), ys.max()
    hgt = bottom - top
    lum = img[..., :3] @ np.array([0.299, 0.587, 0.114], dtype=np.float32)
    dark = (lum < 0.32) & fg
    dark[:top + int(0.12 * hgt)] = False
    dark[int(top + 0.40 * hgt):] = False
    rows = fg[top:int(top + 0.40 * hgt)]
    cols = np.nonzero(rows[int(rows.shape[0] * 0.6)])[0]
    cx = (cols.min() + cols.max()) / 2 if len(cols) else img.shape[1] / 2
    found = []
    for cy_, cx_ in components(dark):
        area = len(cy_)
        if area < (0.006 * hgt) ** 2 or area > (0.08 * hgt) ** 2:
            continue
        found.append((area, (cx_.min() + cx_.max()) / 2.0, (cy_.min() + cy_.max()) / 2.0,
                      (cx_.max() - cx_.min() + 1) / 2.0, (cy_.max() - cy_.min() + 1) / 2.0))
    best = None
    for i in range(len(found)):
        for j in range(i + 1, len(found)):
            a, b = found[i], found[j]
            if abs(a[2] - b[2]) > 0.03 * hgt or (a[1] - cx) * (b[1] - cx) >= 0:
                continue
            sym = abs(abs(a[1] - cx) - abs(b[1] - cx)) / max(abs(a[1] - cx), 1.0)
            if sym > 0.35:
                continue
            score = min(a[0], b[0]) * (1 - sym)
            if best is None or score > best[0]:
                best = (score, a, b)
    if best is None:
        return None
    a, b = sorted(best[1:], key=lambda e: e[1])
    return {"left": (a[1], a[2]), "right": (b[1], b[2]),
            "half": ((a[3] + b[3]) / 2, max(a[4], b[4], (a[3] + b[3]) / 4)), "height": hgt, "top": top}


def texel_geometry(me, W, H):
    """Model-space position and normal of the surface under every texel of the active UV map, and which
    texels lie on the mesh at all. Arrays are bottom row first, like Blender's image pixels."""
    pos = np.zeros((H, W, 3), dtype=np.float32)
    nrm = np.zeros((H, W, 3), dtype=np.float32)
    valid = np.zeros((H, W), dtype=bool)
    uv = me.uv_layers.active.data
    me.calc_loop_triangles()
    co = np.array([v.co for v in me.vertices], dtype=np.float32)
    vn = np.array([v.normal for v in me.vertices], dtype=np.float32)
    for tri in me.loop_triangles:
        P = np.array([[uv[l].uv.x * W, uv[l].uv.y * H] for l in tri.loops])
        x0, y0 = np.floor(P.min(0)).astype(int)
        x1, y1 = np.ceil(P.max(0)).astype(int)
        x0, y0 = max(x0, 0), max(y0, 0)
        x1, y1 = min(x1, W - 1), min(y1, H - 1)
        if x1 < x0 or y1 < y0:
            continue
        d = (P[1, 1] - P[2, 1]) * (P[0, 0] - P[2, 0]) + (P[2, 0] - P[1, 0]) * (P[0, 1] - P[2, 1])
        if abs(d) < 1e-12:
            continue
        gx, gy = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
        l1 = ((P[1, 1] - P[2, 1]) * (gx - P[2, 0]) + (P[2, 0] - P[1, 0]) * (gy - P[2, 1])) / d
        l2 = ((P[2, 1] - P[0, 1]) * (gx - P[2, 0]) + (P[0, 0] - P[2, 0]) * (gy - P[2, 1])) / d
        l3 = 1 - l1 - l2
        inside = (l1 >= -0.02) & (l2 >= -0.02) & (l3 >= -0.02)
        if not inside.any():
            continue
        v = tri.vertices
        p = l1[..., None] * co[v[0]] + l2[..., None] * co[v[1]] + l3[..., None] * co[v[2]]
        n = l1[..., None] * vn[v[0]] + l2[..., None] * vn[v[1]] + l3[..., None] * vn[v[2]]
        sub_p = pos[y0:y1 + 1, x0:x1 + 1]
        sub_n = nrm[y0:y1 + 1, x0:x1 + 1]
        sub_v = valid[y0:y1 + 1, x0:x1 + 1]
        sub_p[inside] = p[inside]
        sub_n[inside] = n[inside]
        sub_v[inside] = True
    length = np.linalg.norm(nrm, axis=-1, keepdims=True)
    nrm /= np.maximum(length, 1e-6)
    return pos, nrm, valid


def sample(img, u, v):
    """Bilinear sample of a top-row-first image at pixel coordinates (arrays)."""
    h, w = img.shape[:2]
    u = np.clip(u - 0.5, 0, w - 1.001)
    v = np.clip(v - 0.5, 0, h - 1.001)
    x0 = np.floor(u).astype(int)
    y0 = np.floor(v).astype(int)
    fx = (u - x0)[..., None]
    fy = (v - y0)[..., None]
    a = img[y0, x0] * (1 - fx) + img[y0, x0 + 1] * fx
    b = img[y0 + 1, x0] * (1 - fx) + img[y0 + 1, x0 + 1] * fx
    return a * (1 - fy) + b * fy


def smoothstep(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


def project_face(px, pos, nrm, valid, art, eyes_art, eyes_model, neck_z):
    """Paints the concept picture onto the front of the head, in place. eyes_model are the two eye centres
    (x, z) on the finished model, left to right as seen from the front; eyes_art the same in the picture."""
    (ux1, vy1), (ux2, vy2) = eyes_art["left"], eyes_art["right"]
    (mx1, mz1), (mx2, mz2) = eyes_model
    scale = (ux2 - ux1) / (mx2 - mx1)                  # picture pixels per model unit
    um, vm = (ux1 + ux2) / 2, (vy1 + vy2) / 2
    xm, zm = (mx1 + mx2) / 2, (mz1 + mz2) / 2
    u = um + (pos[..., 0] - xm) * scale
    v = vm - (pos[..., 2] - zm) * scale
    facing = -nrm[..., 1]
    weight = smoothstep(0.12, 0.42, facing) * smoothstep(neck_z + 0.005, neck_z + 0.045, pos[..., 2]) * valid
    fg = foreground(art).astype(np.float32)
    # shrink the picture's silhouette a little so its outline (and the white around it) never lands on the head
    for _ in range(3):
        fg = np.minimum.reduce([fg, np.roll(fg, 1, 0), np.roll(fg, -1, 0), np.roll(fg, 1, 1), np.roll(fg, -1, 1)])
    h, w = art.shape[:2]
    inside = (u > 1) & (u < w - 2) & (v > 1) & (v < h - 2)
    weight = weight * inside
    pick = weight > 0.001
    colour = sample(art[..., :3], u[pick], v[pick])
    keep = sample(fg[..., None], u[pick], v[pick])[..., 0]
    wsel = weight[pick] * keep
    px[..., :3][pick] = px[..., :3][pick] * (1 - wsel[:, None]) + colour * wsel[:, None]
    return scale, int(pick.sum())


def jacket_mask(px, pos, valid, z_low, z_high, hands=(), hand_r=0.0):
    """Texels of the red jacket (the concept art always paints it red): a pure red hue, well saturated, between
    hips and neck, away from the hands. Shaded skin and auburn hair lean orange, so a narrow hue window keeps
    them out (they were tinted with the line colour before)."""
    rgb = px[..., :3]
    mx = rgb.max(-1)
    mn = rgb.min(-1)
    s = (mx - mn) / np.maximum(mx, 1e-6)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    # hue in turns (red = 0): only meaningful where red is the largest channel
    hue = np.where(g >= b, (g - b), (g - b)) / np.maximum(mx - mn, 1e-6) / 6.0
    hue = np.where(hue < 0, hue + 1.0, hue)
    red_hue = (hue > 0.955) | (hue < 0.022)
    reddish = (r >= mx - 1e-6) & red_hue & (g < r * 0.5) & (b < r * 0.6)
    z = pos[..., 2]
    m = valid & reddish & (s > 0.42) & (mx > 0.16) & (z > z_low) & (z < z_high)
    for tip in hands:
        d = np.linalg.norm(pos - np.array(tip, dtype=np.float32), axis=-1)
        m &= d > hand_r
    return m


def finish_texture(px, valid, jacket, ref_percentile=None):
    """Jacket texels keep only their shading (around 0.5, the game multiplies by twice the line colour) and
    are flagged in alpha; everything else keeps its colour with alpha 0. Empty texels between UV islands are
    filled from their neighbours, so filtering never pulls in stray colour. ref_percentile: the jacket
    brightness that maps to 0.5 (default the mean); painted pictures have deep folds, so a higher percentile
    keeps the lit cloth at the line colour instead of darker."""
    out = px.copy()
    rgb = out[..., :3]
    lum = rgb @ np.array([0.299, 0.587, 0.114], dtype=np.float32)
    if jacket.any():
        mean = lum[jacket].mean() if ref_percentile is None else float(np.percentile(lum[jacket], ref_percentile))
        rgb[jacket] = np.clip((lum[jacket] / mean * 0.5)[:, None], 0, 1)
    out[..., 3] = jacket.astype(np.float32)
    filled = valid.copy()
    for _ in range(6):
        grow = np.zeros_like(out)
        count = np.zeros(filled.shape, dtype=np.float32)
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            src = np.roll(filled, (dy, dx), axis=(0, 1))
            val = np.roll(out, (dy, dx), axis=(0, 1))
            add = src & ~filled
            grow[add] += val[add]
            count[add] += 1
        new = (count > 0) & ~filled
        out[new] = grow[new] / count[new][:, None]
        filled |= new
    return out


def free_block(valid, size=8):
    """A size x size block of texels no UV island uses (bottom-left corner), for flat colours."""
    H, W = valid.shape
    for y in range(0, H - size, size):
        for x in range(0, W - size, size):
            if not valid[max(0, y - 4):y + size + 4, max(0, x - 4):x + size + 4].any():
                return x, y
    return None


def open_smile(name, w, h, mats):
    """A laughing mouth as a flat cut-out facing -Y: a wide D (the top edge smiles a little, the bottom is
    round) in dark red, a row of upper teeth and a pink tongue. w is the half-width, h the depth; the shape is
    bent back like the face it sits on."""
    import bmesh
    me = bpy.data.meshes.new(name)
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    for slot in ("MouthIn", "White", "Pink"):
        me.materials.append(mats[slot])
    bm = bmesh.new()

    def bend(x, z):
        return 0.0          # flat: passenger.py lays every overlay onto the face surface

    def poly(points, y, slot):
        f = bm.faces.new([bm.verts.new((x, y + bend(x, z), z)) for x, z in points])
        f.material_index = slot
        f.normal_update()
        if f.normal.y > 0:
            f.normal_flip()

    n = 20
    lift = 0.18 * h
    top = [(-w + 2 * w * i / n, lift * ((-w + 2 * w * i / n) / w) ** 2) for i in range(n + 1)]
    bottom = [(x, lift * (x / w) ** 2 - h * math.sqrt(max(0.0, 1 - (x / w) ** 2))) for x, _ in reversed(top[1:-1])]
    poly(top + bottom, 0.0, 0)
    tw = 0.78 * w
    upper = [(-tw + 2 * tw * i / 12, lift * ((-tw + 2 * tw * i / 12) / w) ** 2 - 0.1 * lift) for i in range(13)]
    lower = [(x, z - 0.24 * h) for x, z in reversed(upper)]
    poly(upper + lower, -0.0015, 1)
    rx, rz = 0.52 * w, 0.27 * h
    poly([(rx * math.cos(2 * math.pi * i / 24), -0.66 * h + rz * math.sin(2 * math.pi * i / 24)) for i in range(24)], -0.001, 2)
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    # small triangles, so the shape can follow the face when it is laid on (see passenger.py place())
    bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=3, use_grid_fill=True)
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    bm.normal_update()
    bm.to_mesh(me)
    bm.free()
    for poly_ in me.polygons:
        poly_.use_smooth = False
    return ob
