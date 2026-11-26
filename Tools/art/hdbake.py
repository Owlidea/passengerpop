"""Paints an untextured image-to-3D shape from the orthographic views it was made from, for passenger.py --hd.

Hunyuan3D-2 runs on this Mac for shapes only (its texture model needs an NVIDIA GPU), so the multi-view shape from
Tools/hunyuan/gen_mv.py comes without colour. This script reduces it to the game's triangle budget, gives it a UV
map and a texture painted from the same four views (front, left, back, right; cut out by
Tools/hunyuan/turnaround.py), and writes a textured GLB that passenger.py --hd uses exactly like the TRELLIS models.
It also reads the face marks (eye and mouth centres and half-sizes) and the collar height off the front view and
writes them next to the GLB as JSON, with the final triangle count; passenger.py picks them up.

The face gets its own care, since the painted face is what makes the character:
- Hunyuan carves an open mouth as a cavity, while the pictures paint the mouth on; the cavity is filled with a
  patch laid out as a smooth height field, so no triangle folds over (see fill_mouth);
- its nose, a ball standing well out of the face, is lowered to --nose of its height: the picture paints the
  nose's light and shade, and the ball's steep underside caught the Toy shader's pale rim light;
- the reduction would leave a smooth face only a few dozen large facets (it spends its triangles on hair strands
  and cloth folds), so the face keeps the shape's own density, up to --face-tris: the nose, the glasses and the
  cheeks stay round;
- the face's UV islands get --face-boost times the texel density of the rest, so passenger.py can lay the
  picture's face on at close to the picture's own resolution.

Run:
    Blender -b --factory-startup --python Tools/art/hdbake.py -- --shape MetroSort-Media/3D/yolcu-hd-1.glb \
        --views MetroSort-Media/3D/yolcu-hd-1 --out MetroSort-Media/3D/yolcu-hd-1-painted.glb \
        [--size 2048] [--tris 8000] [--face-tris 1500] [--face-boost 2.0] [--nose 0.3] [--preview PNG]

How each texel is coloured: its surface point is projected into every view (orthographic; each view is lined up
with the shape by height and centred on the shape's extent in that direction), a depth buffer of the shape rendered
from that view says whether the point is in sight, and the views that see it are blended by how squarely they face
it. Points no view sees (the top of the head, under the arms) take the colours of the views that face them most.

Blender space: Z up, the character faces -Y (as the shapes come out of Hunyuan and as passenger.py expects).
"""
import bpy, json, math, os, sys
import numpy as np
from mathutils import Matrix

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import hdface

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


def arg(name, default=None):
    return argv[argv.index(name) + 1] if name in argv else default


SHAPE = os.path.abspath(arg("--shape"))
VIEWS = os.path.abspath(arg("--views"))
OUT = os.path.abspath(arg("--out"))
SIZE = int(arg("--size", 2048))
TRIS = int(arg("--tris", 8000))              # the game's budget per passenger, face included
FACE_TRIS = int(arg("--face-tris", 1500))    # at most this many of them on the face
FACE_BOOST = float(arg("--face-boost", 2.0))  # texel density of the face's UV islands against the rest
NOSE = float(arg("--nose", 0.3))             # the nose's height over the face against the shape's own (1 keeps it)
PREVIEW = arg("--preview")
HD_H = 1.52             # passenger.py's HD height: the face marks are given in that scaled space
SHARP = 3.0             # how strongly the most squarely facing view wins the blend

# view: (screen-right axis, camera forward axis); the camera looks along "forward" at the character
AXES = {
    "front": ((1.0, 0.0, 0.0), (0.0, 1.0, 0.0)),
    "left": ((0.0, 1.0, 0.0), (-1.0, 0.0, 0.0)),     # shows the character's left side; it faces the picture's left
    "back": ((-1.0, 0.0, 0.0), (0.0, -1.0, 0.0)),
    "right": ((0.0, -1.0, 0.0), (1.0, 0.0, 0.0)),
}


# ---------------------------------------------------------------- shape
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
bpy.ops.import_scene.gltf(filepath=SHAPE)
ob = next(o for o in scene.objects if o.type == 'MESH')
ob.data.transform(ob.matrix_world)
ob.parent = None
ob.matrix_world = Matrix.Identity(4)
for o in [o for o in scene.objects if o is not ob]:
    bpy.data.objects.remove(o)
ob.name = ob.data.name = "Body"
me = ob.data
for poly in me.polygons:
    poly.use_smooth = True


def mesh_arrays():
    """Vertex positions, vertex normals and triangles (vertex indices) of the mesh as it is now."""
    me.update()
    nv = len(me.vertices)
    co_ = np.empty(nv * 3, dtype=np.float64)
    me.vertices.foreach_get("co", co_)
    nr_ = np.empty(nv * 3, dtype=np.float64)
    me.vertices.foreach_get("normal", nr_)
    me.calc_loop_triangles()
    tri_ = np.empty(len(me.loop_triangles) * 3, dtype=np.int64)
    me.loop_triangles.foreach_get("vertices", tri_)
    return co_.reshape(-1, 3), nr_.reshape(-1, 3), tri_.reshape(-1, 3)


def tri_edges(tri_):
    """Unique edges (sorted vertex pairs) of a triangle list."""
    e = np.concatenate([tri_[:, [0, 1]], tri_[:, [1, 2]], tri_[:, [2, 0]]])
    return np.unique(np.sort(e, axis=1), axis=0)


co, nr, tris = mesh_arrays()
zmin, zmax = float(co[:, 2].min()), float(co[:, 2].max())
height = zmax - zmin
print("shape: %d vertices, %d triangles, height %.3f" % (len(co), len(tris), height))


# ---------------------------------------------------------------- views
class View:
    def __init__(self, name):
        right, fwd = AXES[name]
        self.name = name
        self.right = np.array(right, dtype=np.float64)
        self.fwd = np.array(fwd, dtype=np.float64)
        cut = hdface.load_rgba(os.path.join(VIEWS, name + ".png"))              # top row first
        self.cut = cut
        self.img = hdface.load_rgba(os.path.join(VIEWS, name + "_filled.png"))[..., :3]
        ys, xs = np.nonzero(cut[..., 3] > 0.5)
        self.box = (int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max()))
        # Lined up with the shape as it comes from Hunyuan; the reduction below hardly moves its extent.
        s = co @ self.right
        self.s_mid = float(s.min() + s.max()) / 2
        self.k = (self.box[3] - self.box[1] + 1) / height                     # picture pixels per model unit
        # The shape is not exactly as deep or wide as the pictures draw it (side views come out up to ~7% wider),
        # so each view is fitted across on its own: the shape's extent spans the picture's.
        self.kx = (self.box[2] - self.box[0] + 1) / float(s.max() - s.min())
        self.u_mid = (self.box[0] + self.box[2] + 1) / 2.0
        self.v_feet = self.box[3] + 1.0
        print("%-5s view %dx%d px, shape %.0f%% as wide as the picture at equal height" % (
            name, cut.shape[1], cut.shape[0], 100.0 * self.k / self.kx))
        # The picture's silhouette, shrunk by a few pixels: colours right at its edge are unreliable.
        a = (cut[..., 3] > 0.5).astype(np.float32)
        for _ in range(3):
            a = np.minimum.reduce([a, np.roll(a, 1, 0), np.roll(a, -1, 0), np.roll(a, 1, 1), np.roll(a, -1, 1)])
        self.inner = a
        self.trust = 1.0 if name in ("front", "back") else 0.6      # side views are narrower and more often off
        self.zbuf = None

    def project(self, p):
        """Picture coordinates (u right, v down, pixels) and depth along the view of model points."""
        u = self.u_mid + (p @ self.right - self.s_mid) * self.kx
        v = self.v_feet - (p[..., 2] - zmin) * self.k
        return u, v, p @ self.fwd

    def depth_buffer(self, co_, tri_):
        """Nearest depth of the shape at every picture pixel (inf where the shape is not)."""
        h, w = self.cut.shape[:2]
        zb = np.full((h, w), np.inf, dtype=np.float32)
        u, v, d = self.project(co_)
        U, V, D = u[tri_], v[tri_], d[tri_]
        for (u0, u1, u2), (v0, v1, v2), (d0, d1, d2) in zip(U, V, D):
            x0 = max(int(math.floor(min(u0, u1, u2))), 0)
            x1 = min(int(math.ceil(max(u0, u1, u2))), w - 1)
            y0 = max(int(math.floor(min(v0, v1, v2))), 0)
            y1 = min(int(math.ceil(max(v0, v1, v2))), h - 1)
            if x1 < x0 or y1 < y0:
                continue
            den = (v1 - v2) * (u0 - u2) + (u2 - u1) * (v0 - v2)
            if abs(den) < 1e-9:
                continue
            gx, gy = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
            l0 = ((v1 - v2) * (gx - u2) + (u2 - u1) * (gy - v2)) / den
            l1 = ((v2 - v0) * (gx - u2) + (u0 - u2) * (gy - v2)) / den
            l2 = 1 - l0 - l1
            inside = (l0 >= 0) & (l1 >= 0) & (l2 >= 0)
            if not inside.any():
                continue
            dep = l0 * d0 + l1 * d1 + l2 * d2
            sub = zb[y0:y1 + 1, x0:x1 + 1]
            np.minimum(sub, np.where(inside, dep, np.inf), out=sub)
        return zb


views = [View(n) for n in AXES]
front = views[0]


# ---------------------------------------------------------------- face marks
def find_mouth(img, eyes):
    """Centre and half-size (pixels) of the mouth: the largest dark or deep-red blob under the eyes, near the
    middle of the face and no wider than the eyes are apart (so the red jacket under the chin never counts)."""
    (lx, ly), (rx, ry) = eyes["left"], eyes["right"]
    gap = rx - lx
    cy = (ly + ry) / 2
    mid = (lx + rx) / 2
    x0, x1 = int(lx - 0.1 * gap), int(rx + 0.1 * gap)
    y0, y1 = int(cy + 0.25 * gap), int(cy + 0.95 * gap)
    rgb = img[..., :3]
    lum = rgb @ np.array([0.299, 0.587, 0.114], dtype=np.float32)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    mx, mn = rgb.max(-1), rgb.min(-1)
    sat = (mx - mn) / np.maximum(mx, 1e-6)
    # very dark, or a deep saturated red; shaded skin under the chin is red-leaning too, but paler
    mouthy = ((lum < 0.30) | ((r > g * 1.35) & (r > b * 1.2) & (sat > 0.45) & (lum < 0.55))) & hdface.foreground(img)
    box = np.zeros_like(mouthy)
    box[max(y0, 0):y1, max(x0, 0):x1] = True
    mouthy &= box
    comps = []
    for cy_, cx_ in hdface.components(mouthy):
        w_, h_ = cx_.max() - cx_.min() + 1, cy_.max() - cy_.min() + 1
        centre = (cx_.min() + cx_.max()) / 2.0
        if len(cy_) >= 12 and w_ < 0.9 * gap and h_ < 0.5 * gap and abs(centre - mid) < 0.3 * gap:
            comps.append((len(cy_), cy_, cx_))
    if not comps:
        return mid, cy + 0.55 * gap, 0.28 * gap, 0.08 * gap
    # of the sizeable blobs, the one nearest where a mouth sits (about half the eye spacing under the eyes)
    big = max(c[0] for c in comps)
    _, cy_, cx_ = min((c for c in comps if c[0] >= 0.2 * big),
                      key=lambda c: abs((c[1].min() + c[1].max()) / 2.0 - (cy + 0.5 * gap)))
    return ((cx_.min() + cx_.max()) / 2.0, (cy_.min() + cy_.max()) / 2.0,
            (cx_.max() - cx_.min() + 1) / 2.0, (cy_.max() - cy_.min() + 1) / 2.0)


def find_neck(img, eyes, mouth_v):
    """Picture row of the neck: the top of the white collar under the face (the passengers all wear one), or of
    the red jacket where no collar shows. Big hair hanging beside the neck hides it from passenger.py's own
    measurement."""
    (lx, ly), (rx, ry) = eyes["left"], eyes["right"]
    gap = rx - lx
    mid = (lx + rx) / 2
    rgb = img[..., :3]
    fg = hdface.foreground(img)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    x0, x1 = int(mid - 0.35 * gap), int(mid + 0.35 * gap)
    red = (r > 0.5) & (g < 0.35) & (b < 0.35) & fg
    rows = np.nonzero(red[:, x0:x1].mean(1) > 0.5)[0]
    rows = rows[rows > mouth_v + 0.25 * gap]
    if not len(rows):
        return None
    jacket_top = int(rows.min())
    white = (rgb.min(-1) > 0.80) & fg
    collar = [y for y in range(int(mouth_v + 0.2 * gap), jacket_top) if white[y, x0:x1].mean() > 0.25]
    return min(collar) if collar else jacket_top


art = front.cut.copy()
art[..., :3] = np.where(art[..., 3:4] > 0.5, art[..., :3], 1.0)     # plain white around the figure
eyes = hdface.find_eyes(art)       # falls back to closed, arc-shaped eyes
marks = None
neck_z = None
face = None                        # the same marks in the shape's own units, for the face work below
if eyes:
    k2 = HD_H / height
    to_x = lambda u: (u - front.u_mid) / front.kx * k2                  # passenger.py centres x on the extent
    to_z = lambda v: (front.v_feet - v) / front.k * k2
    raw_x = lambda u: (u - front.u_mid) / front.kx + front.s_mid
    raw_z = lambda v: zmin + (front.v_feet - v) / front.k
    (lu, lv), (ru, rv) = eyes["left"], eyes["right"]
    mu, mv, mhu, mhv = find_mouth(art, eyes)
    marks = [[[round(to_x(lu), 3), round(to_z(lv), 3)], [round(to_x(ru), 3), round(to_z(rv), 3)]],
             [round(eyes["half"][0] / front.kx * k2, 3), round(eyes["half"][1] / front.k * k2, 3)],
             [round(to_x(mu), 3), round(to_z(mv), 3)],
             [round(mhu / front.kx * k2, 3), round(mhv / front.k * k2, 3)]]
    print("face marks", marks)
    face = {"eyes": ((raw_x(lu), raw_z(lv)), (raw_x(ru), raw_z(rv))),
            "eye_half": (eyes["half"][0] / front.kx, eyes["half"][1] / front.k),
            "mouth": (raw_x(mu), raw_z(mv)), "mouth_half": (mhu / front.kx, mhv / front.k)}
    neck_v = find_neck(art, eyes, mv)
    if neck_v is not None:
        neck_z = round(to_z(neck_v), 3)
        print("neck (collar top) at z", neck_z)
else:
    print("face marks: no eyes found in the front view")


# ---------------------------------------------------------------- face geometry
def face_region(co_):
    """Vertices of the face, from brow to chin and cheek to cheek as seen from the front, on the front half of
    the head. Also returns the depth that splits the head into front and back."""
    (xl, ze), (xr, _) = face["eyes"]
    xm, zm = face["mouth"]
    ehx, ehz = face["eye_half"]
    g = xr - xl
    em = ze - zm
    z_top, z_bot = ze + em + ehz, zm - 0.9 * em
    rx, rz = 0.75 * g + 2.2 * ehx, (z_top - z_bot) / 2
    d = ((co_[:, 0] - (xl + xr) / 2) / rx) ** 2 + ((co_[:, 2] - (z_top + z_bot) / 2) / rz) ** 2
    band = d < 1.0
    y_mid = float(co_[band, 1].min() + co_[band, 1].max()) / 2
    return band & (co_[:, 1] < y_mid), y_mid


def fill_mouth():
    """Hunyuan carves an open mouth as a cavity (with a lip ledge and teeth in it), while the pictures paint the
    mouth on. Pushing the cavity's walls straight forward (what passenger.py did) folds the ledge over the floor:
    the red shards in the first premium faces. Here the cavity, with a margin of face around it, is taken out as
    one disc of the mesh; the disc is laid out flat seen from the front (each inner vertex at the mean of its
    neighbours, the rim where it was: a fold-free layout) and lifted onto a smooth surface fitted to the face
    around the mouth, plus the rim's own offset from that surface spread smoothly inwards, so it joins the face
    without a step. Returns whether it was done."""
    co_, nr_, tri_ = mesh_arrays()
    _, y_mid = face_region(co_)
    (xl, _), (xr, _) = face["eyes"]
    g = xr - xl
    xm, zm = face["mouth"]
    hx, hz = face["mouth_half"]
    x, z = co_[:, 0] - xm, co_[:, 2] - zm
    front_ = co_[:, 1] < y_mid
    outer = front_ & ((x / (hx + 0.35 * g)) ** 2 + (z / (hz + 0.3 * g)) ** 2 < 1.0)
    core = front_ & ((x / hx) ** 2 + (z / hz) ** 2 < 1.0)
    # the face around the mouth as a quadratic y(x, z), fitted robustly: the nose and the cavity are outliers
    A = np.stack([np.ones_like(x), x, z, x * x, z * z, x * z], 1)
    ring = outer & ~core & (nr_[:, 1] < -0.3)
    sel = ring
    for _ in range(5):
        coef = np.linalg.lstsq(A[sel], co_[sel, 1], rcond=None)[0]
        res = co_[:, 1] - A @ coef
        mad = 1.4826 * float(np.median(np.abs(res[sel]))) + 1e-9
        sel = ring & (np.abs(res) < 2.5 * mad)
    behind = co_[:, 1] - A @ coef                   # > 0: behind the face surface
    edges = tri_edges(tri_)
    ea, eb = edges[:, 0], edges[:, 1]

    def neighbours(mask):
        n = np.zeros_like(mask)
        n[eb[mask[ea]]] = True
        n[ea[mask[eb]]] = True
        return n & ~mask

    # the mouth area, grown over the cavity (whatever lies clearly behind the face surface near the mouth)
    region = core.copy()
    deep = outer & (behind > 0.03 * g)
    while True:
        new = neighbours(region) & deep
        if not new.any():
            break
        region |= new
    cavity = int(region.sum())
    for _ in range(2):
        region |= neighbours(region) & front_
    for attempt in range(4):
        ft = tri_[region[tri_].all(1)]
        # the connected piece of it that holds the mouth
        emap = {}
        for i, t in enumerate(ft):
            for a_, b_ in ((t[0], t[1]), (t[1], t[2]), (t[2], t[0])):
                emap.setdefault((min(a_, b_), max(a_, b_)), []).append(i)
        cen = co_[ft].mean(1)
        start = int(np.argmin((cen[:, 0] - xm) ** 2 + (cen[:, 2] - zm) ** 2))
        seen, stack = {start}, [start]
        while stack:
            t = ft[stack.pop()]
            for a_, b_ in ((t[0], t[1]), (t[1], t[2]), (t[2], t[0])):
                for j in emap[(min(a_, b_), max(a_, b_))]:
                    if j not in seen:
                        seen.add(j)
                        stack.append(j)
        disc = ft[sorted(seen)]
        e_all = np.sort(np.concatenate([disc[:, [0, 1]], disc[:, [1, 2]], disc[:, [2, 0]]]), axis=1)
        e_uni, e_count = np.unique(e_all, axis=0, return_counts=True)
        verts = np.unique(disc)
        if len(verts) - len(e_uni) + len(disc) == 1:          # Euler characteristic of a disc
            break
        region |= neighbours(region) & front_               # close small holes and try again
    else:
        print("mouth not filled: the area around it is not a disc")
        return False
    n = len(verts)
    la, lb = np.searchsorted(verts, e_uni[:, 0]), np.searchsorted(verts, e_uni[:, 1])
    deg = np.bincount(la, minlength=n) + np.bincount(lb, minlength=n)
    rim = np.zeros(n, dtype=bool)
    rim[np.searchsorted(verts, e_uni[e_count == 1].ravel())] = True
    inner = ~rim
    P = co_[verts][:, [0, 2]].copy()
    R = np.where(rim, behind[verts], 0.0)          # the rim's offset from the fitted surface, spread inwards
    for _ in range(6000):
        for c in (0, 1):
            s = np.bincount(la, P[lb, c], n) + np.bincount(lb, P[la, c], n)
            P[inner, c] = s[inner] / deg[inner]
        s = np.bincount(la, R[lb], n) + np.bincount(lb, R[la], n)
        R[inner] = s[inner] / deg[inner]
    X, Z = P[:, 0] - xm, P[:, 1] - zm
    new = co_.copy()
    new[verts, 0] = P[:, 0]
    new[verts, 2] = P[:, 1]
    new[verts, 1] = np.stack([np.ones_like(X), X, Z, X * X, Z * Z, X * Z], 1) @ coef + R
    me.vertices.foreach_set("co", new.ravel())
    me.update()
    tn = np.cross(new[disc[:, 1]] - new[disc[:, 0]], new[disc[:, 2]] - new[disc[:, 0]])
    print("mouth filled: %d vertices (%d in the cavity, %d on the rim), cavity up to %.0f%% of the eye spacing "
          "deep, %d of %d triangles facing back" % (n, cavity, int(rim.sum()), 100 * float(behind[verts].max()) / g,
                                                    int((tn[:, 1] > 0).sum()), len(disc)))
    return True


def soften_nose():
    """The pictures paint the nose with its own light and shade, while Hunyuan's nose is a ball standing well
    out of the face and a little lower than the painted one: the Toy shader's rim light (strong where a surface
    turns away from the camera) made its steep underside a pale patch under the painted nose. Its
    height over the face is scaled by NOSE, along Y only (each point keeps its order along the view: nothing
    folds). The nose is the point standing out most between the eyes and the mouth; glasses (their bridge sits
    just above it) and a moustache below are outside its radius."""
    co_, nr_, _ = mesh_arrays()
    _, y_mid = face_region(co_)
    (xl, ze), (xr, _) = face["eyes"]
    g = xr - xl
    xm, zm = face["mouth"]
    em = ze - zm
    xc, zc = (xl + xr) / 2, (ze + zm) / 2
    x, z = co_[:, 0] - xc, co_[:, 2] - zc
    front_ = co_[:, 1] < y_mid
    # the face between eyes and mouth as a quadratic, fitted robustly (nose, glasses and eyes are outliers)
    A = np.stack([np.ones_like(x), x, z, x * x, z * z, x * z], 1)
    area = front_ & (np.abs(x) < 0.45 * g) & (np.abs(z) < 0.75 * em) & (nr_[:, 1] < -0.3)
    sel = area
    for _ in range(5):
        coef = np.linalg.lstsq(A[sel], co_[sel, 1], rcond=None)[0]
        res = co_[:, 1] - A @ coef
        mad = 1.4826 * float(np.median(np.abs(res[sel]))) + 1e-9
        sel = area & (np.abs(res) < 2.5 * mad)
    out = -(co_[:, 1] - A @ coef)                      # > 0: in front of the face surface
    cand = front_ & (np.abs(x) < 0.2 * g) & (co_[:, 2] > zm + 0.15 * em) & (co_[:, 2] < ze - 0.15 * em)
    if not cand.any():
        return
    tip = np.nonzero(cand)[0][int(np.argmax(out[cand]))]
    r = 0.2 * g
    d = np.hypot(co_[:, 0] - co_[tip, 0], co_[:, 2] - co_[tip, 2])
    t = np.clip((r - d) / (0.4 * r), 0.0, 1.0)         # 1 within 0.6 r of the tip, 0 from r on
    w = t * t * (3 - 2 * t) * front_ * (out > 0)
    new = co_.copy()
    new[:, 1] += w * (1.0 - NOSE) * out
    me.vertices.foreach_set("co", new.ravel())
    me.update()
    print("nose: stood %.0f%% of the eye spacing out of the face, now %.0f%% (%d vertices)"
          % (100 * out[tip] / g, 100 * NOSE * out[tip] / g, int((w > 0).sum())))


def tri_count():
    return sum(len(p.vertices) - 2 for p in me.polygons)


def decimate(target, group=None, protect=True):
    """Collapse-decimates the mesh to about target triangles. With a vertex group, protect=True keeps the group
    (its edges get a cost far above any other) and protect=False keeps everything else."""
    now = tri_count()
    if now <= target:
        return
    dec = ob.modifiers.new("Decimate", 'DECIMATE')
    dec.ratio = target / now
    dec.use_collapse_triangulate = True
    if group:
        dec.vertex_group = group
        # the modifier adds cost to edges whose vertices weigh less than 1, so the kept part must weigh 0
        dec.invert_vertex_group = protect
        dec.vertex_group_factor = 1000.0
    with bpy.context.temp_override(object=ob, active_object=ob, selected_objects=[ob]):
        bpy.ops.object.modifier_apply(modifier="Decimate")


mouth_filled = False
if face:
    mouth_filled = fill_mouth()
    if NOSE != 1.0:
        soften_nose()
    co, nr, tris = mesh_arrays()
    on_face, _ = face_region(co)
    f_raw = int(on_face[tris].all(1).sum())
    ob.vertex_groups.new(name="Face").add(np.nonzero(on_face)[0].tolist(), 1.0, 'REPLACE')
    keep = min(f_raw, FACE_TRIS)
    decimate(TRIS - keep + f_raw, "Face", protect=True)      # everything but the face down to its share
    if f_raw > FACE_TRIS:
        decimate(TRIS, "Face", protect=False)                 # then the face down to --face-tris
    ob.vertex_groups.remove(ob.vertex_groups["Face"])         # (applying a modifier leaves old references stale)
    co, nr, tris = mesh_arrays()
    on_face, _ = face_region(co)
    print("reduced to %d triangles, %d of them on the face (it had %d)" % (len(tris), int(on_face[tris].all(1).sum()), f_raw))
else:
    decimate(TRIS)
    print("reduced to %d triangles" % tri_count())


# ---------------------------------------------------------------- UV map
bpy.context.view_layer.objects.active = ob
ob.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=math.radians(66.0), island_margin=0.003)
bpy.ops.object.mode_set(mode='OBJECT')


def uv_arrays():
    """Per polygon (all triangles here): loop start, area, normal, whether it lies on the face; per loop: UV."""
    npoly = len(me.polygons)
    ls = np.empty(npoly, dtype=np.int64)
    me.polygons.foreach_get("loop_start", ls)
    area = np.empty(npoly)
    me.polygons.foreach_get("area", area)
    lv = np.empty(len(me.loops), dtype=np.int64)
    me.loops.foreach_get("vertex_index", lv)
    luv = np.empty(len(me.loops) * 2)
    me.uv_layers.active.data.foreach_get("uv", luv)
    luv = luv.reshape(-1, 2)
    corners = ls[:, None] + np.arange(3)
    co_, _, _ = mesh_arrays()
    on_face_, _ = face_region(co_)
    pn = np.empty(npoly * 3)
    me.polygons.foreach_get("normal", pn)
    face_poly = on_face_[lv[corners]].all(1) & (pn.reshape(-1, 3)[:, 1] < -0.2)
    t = luv[corners]
    uv_area = 0.5 * np.abs((t[:, 1, 0] - t[:, 0, 0]) * (t[:, 2, 1] - t[:, 0, 1]) - (t[:, 2, 0] - t[:, 0, 0]) * (t[:, 1, 1] - t[:, 0, 1]))
    return corners, area, uv_area, face_poly, luv


def density(sel, area, uv_area):
    """Texels per model unit (at SIZE) over the selected polygons."""
    return SIZE * math.sqrt(float(uv_area[sel].sum()) / max(float(area[sel].sum()), 1e-12))


if face and FACE_BOOST != 1.0:
    from bpy_extras import mesh_utils
    corners, area, uv_area, face_poly, luv = uv_arrays()
    boosted = 0
    for island in mesh_utils.mesh_linked_uv_islands(me):
        island = np.array(island, dtype=np.int64)
        if area[island][face_poly[island]].sum() > 0.25 * area[island].sum():
            lp = corners[island].ravel()
            c = luv[lp].mean(0)
            luv[lp] = c + (luv[lp] - c) * FACE_BOOST
            boosted += len(island)
    me.uv_layers.active.data.foreach_set("uv", luv.ravel())
    bpy.ops.object.mode_set(mode='EDIT')
    scene.tool_settings.use_uv_select_sync = True
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.pack_islands(rotate=True, margin=0.003)
    bpy.ops.object.mode_set(mode='OBJECT')
    me.update()
    print("face UV islands (%d triangles) scaled up %.1fx before packing" % (boosted, FACE_BOOST))
if face:
    corners, area, uv_area, face_poly, luv = uv_arrays()
    (xl, _), (xr, _) = face["eyes"]
    d_face, d_rest = density(face_poly, area, uv_area), density(~face_poly, area, uv_area)
    print("texels per unit: face %.0f, rest %.0f; eye spacing %.0f texels at %d" % (d_face, d_rest, d_face * (xr - xl), SIZE))

co, nr, tris = mesh_arrays()
for view in views:
    view.zbuf = view.depth_buffer(co, tris)


# ---------------------------------------------------------------- texture
pos, nrm, valid = hdface.texel_geometry(me, SIZE, SIZE)                   # bottom row first
P = pos[valid]
N = nrm[valid]
eps = 0.012 * height
acc = np.zeros((len(P), 3), dtype=np.float32)
wsum = np.zeros(len(P), dtype=np.float32)
fallback = np.zeros((len(P), 3), dtype=np.float32)
fsum = np.zeros(len(P), dtype=np.float32)
clear = np.zeros(len(P), dtype=bool)        # seen squarely enough by at least one view to trust its colour
for view in views:
    u, v, d = view.project(P)
    h, w = view.zbuf.shape
    iu = np.clip(np.floor(u).astype(int), 0, w - 1)
    iv = np.clip(np.floor(v).astype(int), 0, h - 1)
    # nearest depth in the 3x3 pixels around the point, so silhouette texels are not lost to rounding
    near = view.zbuf[iv, iu]
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            near = np.minimum(near, view.zbuf[np.clip(iv + dy, 0, h - 1), np.clip(iu + dx, 0, w - 1)])
    seen = (d <= near + eps) | ~np.isfinite(near)
    seen &= hdface.sample(view.inner[..., None], u, v)[..., 0] > 0.5
    facing = np.clip(-(N @ view.fwd), 0.0, 1.0).astype(np.float32)
    colour = hdface.sample(view.img, u, v)
    weight = view.trust * facing ** SHARP * seen
    acc += colour * weight[:, None]
    wsum += weight
    fw = (facing + 0.02) ** SHARP
    fallback += colour * fw[:, None]
    fsum += fw
    clear |= seen & (facing > 0.25)
    print("%-5s view sees %.0f%% of the texels" % (view.name, 100.0 * float((seen & (facing > 0.05)).mean())))
col = np.where((wsum > 1e-4)[:, None], acc / np.maximum(wsum, 1e-6)[:, None], fallback / np.maximum(fsum, 1e-6)[:, None])
print("texels no view sees squarely: %.1f%%" % (100.0 * float((~clear).mean())))

# Texels no view sees squarely (under the arms, the top of the head, behind a hand) would take the colour of
# whatever hides them in the pictures, so they are filled from the clearly seen texels around them instead,
# growing over the surface in texture space. Whole islands no view sees keep the blended colour.
colmap = np.zeros((SIZE, SIZE, 3), dtype=np.float32)
colmap[valid] = col
known = np.zeros((SIZE, SIZE), dtype=bool)
known[valid] = clear
todo = valid & ~known
for _ in range(160):
    if not todo.any():
        break
    grow = np.zeros_like(colmap)
    count = np.zeros(todo.shape, dtype=np.float32)
    for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        src = np.roll(known, (dy, dx), axis=(0, 1))
        val = np.roll(colmap, (dy, dx), axis=(0, 1))
        add = src & todo
        grow[add] += val[add]
        count[add] += 1
    new = (count > 0) & todo
    colmap[new] = grow[new] / count[new][:, None]
    known |= new
    todo &= ~new
print("texels left with the blended colour: %.1f%%" % (100.0 * float(todo.sum()) / max(1, int(valid.sum()))))

px = np.zeros((SIZE, SIZE, 4), dtype=np.float32)
px[..., 3] = 1.0
px[..., :3][valid] = colmap[valid]
filled = valid.copy()
for _ in range(8):                      # spread colour into the gaps between UV islands, for filtering
    grow = np.zeros_like(px[..., :3])
    count = np.zeros(filled.shape, dtype=np.float32)
    for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        src = np.roll(filled, (dy, dx), axis=(0, 1))
        val = np.roll(px[..., :3], (dy, dx), axis=(0, 1))
        add = src & ~filled
        grow[add] += val[add]
        count[add] += 1
    new = (count > 0) & ~filled
    px[..., :3][new] = grow[new] / count[new][:, None]
    filled |= new

tex = bpy.data.images.new(os.path.splitext(os.path.basename(OUT))[0] + "_albedo", SIZE, SIZE, alpha=False)
tex.pixels = px.ravel()
tex.filepath_raw = os.path.splitext(OUT)[0] + "_albedo.png"
tex.file_format = 'PNG'
tex.save()
mat = bpy.data.materials.new("Body")
mat.use_nodes = True
nodes = mat.node_tree.nodes
bsdf = nodes.get("Principled BSDF")
node = nodes.new("ShaderNodeTexImage")
node.image = tex
mat.node_tree.links.new(node.outputs["Color"], bsdf.inputs["Base Color"])
bsdf.inputs["Roughness"].default_value = 0.6
me.materials.clear()
me.materials.append(mat)
bpy.ops.export_scene.gltf(filepath=OUT, export_format='GLB')
print("wrote", OUT)
json.dump({"face_marks": marks, "neck_z": neck_z, "tris": len(tris), "mouth_filled": mouth_filled,
           "source_views": os.path.relpath(VIEWS)}, open(os.path.splitext(OUT)[0] + ".json", "w"), indent=1)


# ---------------------------------------------------------------- preview
if PREVIEW:
    cam_data = bpy.data.cameras.new("Cam")
    cam_data.type = 'ORTHO'
    cam_data.ortho_scale = height * 1.12
    cam = bpy.data.objects.new("Cam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.display.shading.light = 'STUDIO'
    scene.display.shading.color_type = 'TEXTURE'
    scene.render.resolution_x = 420
    scene.render.resolution_y = 560
    scene.world = bpy.data.worlds.new("W")
    scene.world.color = (0.82, 0.80, 0.76)
    mid_z = (zmin + zmax) / 2
    cx = float(co[:, 0].min() + co[:, 0].max()) / 2
    cy = float(co[:, 1].min() + co[:, 1].max()) / 2
    shots = []
    for i, angle in enumerate((0, 35, 90, 180, 270)):
        a = math.radians(angle)
        dist = height * 3
        cam.location = (cx + math.sin(a) * dist, cy - math.cos(a) * dist, mid_z)
        cam.rotation_euler = (math.radians(90), 0, a)
        path = os.path.join(os.path.dirname(PREVIEW), "_shot%d.png" % i)
        scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        shots.append(hdface.load_rgba(path))
        os.remove(path)
    hdface.save_rgba(np.concatenate(shots, axis=1), PREVIEW)
    print("preview", PREVIEW)
