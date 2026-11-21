"""Renders the lives artwork: a spinning glossy heart and a heart that cracks and breaks apart, as flip-books.

Run:  Blender --background --python Tools/art/hearts.py [-- --preview DIR]
Output: Assets/MetroSort/Resources/Art/heart_spin.png, heart_break.png, heart.png

Both sheets use the FlipBook grid (6 x 4, first frame top-left), like Tools/art/rewards.py:
- heart_spin: one full turn about the vertical axis, for the lives counter and the refill popup.
- heart_break: played once when a life is lost: the heart swells, a jagged crack runs down it, and the two
  halves tip apart and drop away, fading out. The last frame is empty.
heart.png is the still front view, for places that only need an icon.

The heart is "inflated": every point of the classic heart outline's inside is raised by a rounded profile of
its distance to the edge, so it reads as a plump, glossy candy heart (gradient from cherry at the tip to bright
red on top, clear coat) rather than a flat cut-out. A dark outline is added in 2D, like the other UI art.
"""
import bpy, bmesh, math, os, sys
import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Assets", "MetroSort", "Resources", "Art")
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
PREVIEW = argv[argv.index("--preview") + 1] if "--preview" in argv else None

COLS, ROWS, CELL = 6, 4, 256
STILL = 512  # heart.png, the still icon
INK = (0.33, 0.02, 0.07)  # outline colour
FRAMES = COLS * ROWS

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene


def heart_material():
    mat = bpy.data.materials.new("HeartCandy")
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = 0.22
    for key, value in (("Coat Weight", 1.0), ("Coat Roughness", 0.04), ("Subsurface Weight", 0.04),
                       ("Specular IOR Level", 0.6)):
        if key in bsdf.inputs:
            bsdf.inputs[key].default_value = value
    if "Subsurface Radius" in bsdf.inputs:
        bsdf.inputs["Subsurface Radius"].default_value = (0.4, 0.05, 0.05)
    # Vertical gradient: warm pink-red on top, deep cherry at the tip.
    coord = nt.nodes.new("ShaderNodeTexCoord")
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    nt.links.new(coord.outputs["Object"], sep.inputs[0])
    maprange = nt.nodes.new("ShaderNodeMapRange")
    maprange.inputs["From Min"].default_value = -0.45
    maprange.inputs["From Max"].default_value = 0.40
    nt.links.new(sep.outputs["Z"], maprange.inputs["Value"])
    nt.links.new(maprange.outputs["Result"], ramp.inputs["Fac"])
    ramp.color_ramp.elements[0].color = (0.20, 0.0, 0.008, 1)
    ramp.color_ramp.elements[1].color = (0.92, 0.035, 0.07, 1)
    mid = ramp.color_ramp.elements.new(0.55)
    mid.color = (0.60, 0.0, 0.02, 1)
    nt.links.new(ramp.outputs["Color"], bsdf.inputs["Base Color"])
    if "Subsurface Color" in bsdf.inputs:
        nt.links.new(ramp.outputs["Color"], bsdf.inputs["Subsurface Color"])
    return mat



HEART = heart_material()


def heart_outline(n=360):
    """The classic heart curve (notch at the top, tip at the bottom), about 1.0 wide, as (x, z)."""
    t = np.linspace(0, 2 * np.pi, n, endpoint=False)
    x = 16 * np.sin(t) ** 3
    z = 13 * np.cos(t) - 5 * np.cos(2 * t) - 2 * np.cos(3 * t) - np.cos(4 * t)
    return np.stack([x, z + 2.5], 1) * 0.031


def crack_x(z):
    """A jagged crack from the notch to the tip, used to split the heart in two."""
    return 0.035 * np.sign(np.sin(z * 26.0)) * (np.abs(np.sin(z * 26.0)) ** 0.3)


def build_heart(name, side=0, step=0.003, thick=0.15):
    """An inflated heart: every point is raised by a rounded profile of its distance to the outline.
    side 0: whole heart; -1 / +1: the left / right half, split along a jagged crack."""
    poly = heart_outline()
    xs = np.arange(-0.56, 0.56, step)
    zs = np.arange(-0.52, 0.48, step)
    gx, gz = np.meshgrid(xs, zs)
    pts = np.stack([gx.ravel(), gz.ravel()], 1)
    # inside test (even-odd) and distance to the outline
    a = poly
    b = np.roll(poly, -1, 0)
    inside = np.zeros(len(pts), bool)
    dist = np.full(len(pts), 9.0)
    for i in range(len(a)):
        (x1, z1), (x2, z2) = a[i], b[i]
        cond = (z1 > pts[:, 1]) != (z2 > pts[:, 1])
        xc = x1 + (pts[:, 1] - z1) * (x2 - x1) / (z2 - z1 + 1e-12)
        inside ^= cond & (pts[:, 0] < xc)
        dx, dz = x2 - x1, z2 - z1
        u = np.clip(((pts[:, 0] - x1) * dx + (pts[:, 1] - z1) * dz) / (dx * dx + dz * dz), 0, 1)
        dist = np.minimum(dist, np.hypot(pts[:, 0] - x1 - u * dx, pts[:, 1] - z1 - u * dz))
    if side:
        cx = crack_x(pts[:, 1])
        dist = np.minimum(dist, np.abs(pts[:, 0] - cx) * 1.0 + 0.0)
        inside &= (pts[:, 0] - cx) * side > 0.004
    R = 0.20
    d = np.clip(dist / R, 0, 1)
    h = thick * (np.sqrt(1 - (1 - d) ** 2) * 0.78 + 0.22 * d ** 0.8)
    ny, nx = gx.shape
    inside = inside.reshape(ny, nx)
    h = h.reshape(ny, nx)
    # quads whose four corners are inside
    q = inside[:-1, :-1] & inside[1:, :-1] & inside[:-1, 1:] & inside[1:, 1:]
    used = np.zeros_like(inside)
    used[:-1, :-1] |= q; used[1:, :-1] |= q; used[:-1, 1:] |= q; used[1:, 1:] |= q
    idx = -np.ones((ny, nx), int)
    idx[used] = np.arange(used.sum())
    nv = used.sum()
    vx, vz, vh = gx[used], gz[used], h[used]
    verts = [(x, -hh, z) for x, z, hh in zip(vx, vz, vh)] + [(x, hh * 0.85, z) for x, z, hh in zip(vx, vz, vh)]
    faces = []
    edges = {}
    jj, ii = np.nonzero(q)
    for j, i in zip(jj, ii):
        f = (idx[j, i], idx[j, i + 1], idx[j + 1, i + 1], idx[j + 1, i])
        faces.append(f)
        faces.append(tuple(v + nv for v in reversed(f)))
        for k in range(4):
            e = (f[k], f[(k + 1) % 4])
            key = (min(e), max(e))
            edges[key] = None if key in edges else e
    for e in edges.values():
        if e is None:
            continue
        a0, b0 = e
        faces.append((b0, a0, a0 + nv, b0 + nv))
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.validate()
    for p in mesh.polygons:
        p.use_smooth = True
    ob = bpy.data.objects.new(name, mesh)
    scene.collection.objects.link(ob)
    mesh.materials.append(HEART)
    sm = ob.modifiers.new("Smooth", 'CORRECTIVE_SMOOTH')
    sm.iterations = 6
    sm.smooth_type = 'LENGTH_WEIGHTED'
    return ob



def outline(px, width, color):
    """Adds a dark outline around the alpha shape (px is H x W x 4 floats, bottom-up)."""
    # Padded, so shapes near the frame edge do not wrap round to the opposite side.
    a = np.pad(px[..., 3], width)
    grown = a.copy()
    inner = a.copy()
    for dy in range(-width, width + 1):
        for dx in range(-width, width + 1):
            if dx * dx + dy * dy > width * width:
                continue
            shifted = np.roll(np.roll(a, dy, 0), dx, 1)
            grown = np.maximum(grown, shifted)
            if dx * dx + dy * dy <= 4:
                inner = np.minimum(inner, shifted)
    grown = grown[width:-width, width:-width]
    inner = inner[width:-width, width:-width]
    out = np.zeros_like(px)
    k = inner[..., None]
    out[..., :3] = px[..., :3] * k + np.array(color) * (1 - k)
    out[..., 3] = grown
    return out


def setup_render(size):
    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.resolution_x = scene.render.resolution_y = size
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = 'PNG'
    scene.render.image_settings.color_mode = 'RGBA'
    scene.view_settings.view_transform = 'Standard'
    scene.view_settings.look = 'None'
    if hasattr(scene, "eevee") and hasattr(scene.eevee, "taa_render_samples"):
        scene.eevee.taa_render_samples = 64


def setup_scene():
    world = bpy.data.worlds.new("World")
    world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    bg.inputs["Color"].default_value = (1.0, 0.95, 0.95, 1)
    bg.inputs["Strength"].default_value = 0.22
    scene.world = world
    cam_data = bpy.data.cameras.new("Cam")
    cam_data.type = 'ORTHO'
    cam_data.ortho_scale = 1.22
    cam = bpy.data.objects.new("Cam", cam_data)
    cam.location = (0, -5, -0.02)
    cam.rotation_euler = (math.radians(90), 0, 0)
    scene.collection.objects.link(cam)
    scene.camera = cam

    def light(name, loc, energy, size, color=(1, 1, 1), shape='DISK'):
        data = bpy.data.lights.new(name, 'AREA')
        data.energy, data.size, data.color = energy, size, color
        data.shape = shape
        ob = bpy.data.objects.new(name, data)
        ob.location = loc
        scene.collection.objects.link(ob)
        ob.rotation_euler = (-ob.location.normalized()).to_track_quat('-Z', 'Y').to_euler()

    light("Key", (-1.4, -3.0, 2.0), 170, 0.9, (1.0, 0.97, 0.95), 'RECTANGLE')   # soft highlight, upper left
    light("Fill", (2.4, -2.8, -0.4), 35, 3.0, (1.0, 0.85, 0.85))
    light("Rim", (1.8, 2.5, 1.6), 220, 2.0, (1.0, 0.75, 0.70))                   # warm rim along the edge
    light("Bounce", (0.0, -2.0, -3.0), 18, 3.0, (1.0, 0.55, 0.55))


def render_frame(path, size):
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    im = bpy.data.images.load(path)
    im.colorspace_settings.name = 'Non-Color'
    px = np.array(im.pixels[:], dtype=np.float32).reshape(size, size, 4)
    bpy.data.images.remove(im)
    if not PREVIEW:
        os.remove(path)
    return outline(px, max(3, size // 52), INK)


def save_image(name, px):
    h, w = px.shape[:2]
    out = bpy.data.images.new(name, w, h, alpha=True)
    out.colorspace_settings.name = 'Non-Color'
    out.pixels = px.ravel().tolist()
    out.filepath_raw = os.path.join(OUT, name + ".png")
    out.file_format = 'PNG'
    out.save()
    print("SAVED", out.filepath_raw)


def save_sheet(name, frames):
    sheet = np.zeros((ROWS * CELL, COLS * CELL, 4), dtype=np.float32)
    for f, px in enumerate(frames):
        col, row = f % COLS, f // COLS
        y0 = (ROWS - 1 - row) * CELL
        sheet[y0:y0 + CELL, col * CELL:(col + 1) * CELL] = px
    save_image(name, sheet)


def show(objs, visible):
    for ob in objs:
        ob.hide_render = not visible


setup_scene()
os.makedirs(OUT, exist_ok=True)
folder = PREVIEW or bpy.app.tempdir
if PREVIEW:
    os.makedirs(PREVIEW, exist_ok=True)

heart = build_heart("Heart")
halves = [build_heart("HalfL", side=-1), build_heart("HalfR", side=1)]

# ---- still icon (front view, high resolution)
show(halves, False)
setup_render(STILL)
save_image("heart", render_frame(os.path.join(folder, "heart_still.png"), STILL))

# ---- spin
setup_render(CELL)
frames = []
for f in range(FRAMES):
    heart.rotation_euler = (0, 0, f * 2 * math.pi / FRAMES)
    frames.append(render_frame(os.path.join(folder, "spin_%02d.png" % f), CELL))
save_sheet("heart_spin", frames)
heart.rotation_euler = (0, 0, 0)

# ---- break: 0-5 swell, 6-9 crack shows (halves pulled a hair apart), 10-23 halves tip apart, drop and fade
frames = []
for f in range(FRAMES):
    if f < 6:
        s = 1.0 + 0.10 * math.sin(f / 5 * math.pi)
        show([heart], True)
        show(halves, False)
        heart.scale = (s, s, s)
        px = render_frame(os.path.join(folder, "break_%02d.png" % f), CELL)
    else:
        show([heart], False)
        show(halves, True)
        t = 0.0 if f < 10 else (f - 10) / 13.0
        gap = 0.012 + (f - 6) * 0.004 if f < 10 else 0.03 + 0.09 * t
        for ob, side in ((halves[1], 1), (halves[0], -1)):
            ob.location = (side * gap, 0, -0.55 * t * t)
            ob.rotation_euler = (0, math.radians(side * 38 * t), math.radians(side * 12 * t))
        px = render_frame(os.path.join(folder, "break_%02d.png" % f), CELL)
        fade = 1.0 if f < 15 else max(0.0, 1.0 - (f - 15) / 8.0)
        px[..., 3] *= fade
    frames.append(px)
save_sheet("heart_break", frames)


# ---- empty heart: the still heart's silhouette as a soft navy hollow with a cream rim (spent lives)
def empty_heart(px, rim):
    a = px[..., 3]
    pad = np.pad(a, rim)
    inner = pad.copy()
    for dy in range(-rim, rim + 1):
        for dx in range(-rim, rim + 1):
            if dx * dx + dy * dy <= rim * rim:
                inner = np.minimum(inner, np.roll(np.roll(pad, dy, 0), dx, 1))
    inner = inner[rim:-rim, rim:-rim]
    out = np.zeros_like(px)
    edge = np.clip(a - inner, 0, 1)
    out[..., :3] = np.array((1.0, 0.95, 0.86)) * edge[..., None] + np.array((0.07, 0.12, 0.30)) * (1 - edge[..., None])
    out[..., 3] = edge + inner * 0.62
    return out



icon = bpy.data.images.load(os.path.join(OUT, "heart.png"))
icon.colorspace_settings.name = 'Non-Color'
hp = np.array(icon.pixels[:], dtype=np.float32).reshape(STILL, STILL, 4)
save_image("heart_empty", empty_heart(hp, 22))


# ---- infinity sign for unlimited lives: a thick white lemniscate with a dark rim, drawn in 2D
def infinity(w=512, h=256):
    yy, xx = np.mgrid[0:h, 0:w]
    t = np.linspace(0, 2 * np.pi, 1400)
    sx = np.cos(t) / (1 + np.sin(t) ** 2)
    sy = np.sin(t) * np.cos(t) / (1 + np.sin(t) ** 2)
    cx, cy, scale = w / 2, h / 2, w * 0.40
    px_ = cx + sx * scale
    py_ = cy + sy * scale * 1.15
    d = np.full((h, w), 1e9)
    for x0, y0 in zip(px_[::2], py_[::2]):
        d = np.minimum(d, (xx - x0) ** 2 + (yy - y0) ** 2)
    d = np.sqrt(d)
    core, rim = h * 0.085, h * 0.135
    out = np.zeros((h, w, 4), np.float32)
    a_rim = np.clip(rim - d + 0.5, 0, 1)
    a_core = np.clip(core - d + 0.5, 0, 1)
    shade = np.clip(1.0 - (yy - cy) / (h * 0.9), 0.75, 1.0)[..., None]
    col = np.array((0.33, 0.02, 0.07)) * (1 - a_core[..., None]) + np.array((1.0, 0.98, 0.93)) * shade * a_core[..., None]
    out[..., :3] = col
    out[..., 3] = a_rim
    return out[::-1]  # Blender images are bottom-up


save_image("infinity", infinity())
