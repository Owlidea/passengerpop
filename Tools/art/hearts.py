"""Renders the lives artwork: a spinning glossy heart and a heart that cracks and breaks apart, as flip-books.

Run:  Blender --background --python Tools/art/hearts.py [-- --preview DIR]
Output: Assets/MetroSort/Resources/Art/heart_spin.png, heart_break.png, heart.png

Both sheets use the FlipBook grid (6 x 4, first frame top-left), like Tools/art/rewards.py:
- heart_spin: one full turn about the vertical axis, for the lives counter and the refill popup.
- heart_break: played once when a life is lost: the heart swells, a jagged crack runs down it, and the two
  halves tip apart and drop away, fading out. The last frame is empty.
heart.png is the still front view, for places that only need an icon.
"""
import bpy, bmesh, math, os, sys
import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Assets", "MetroSort", "Resources", "Art")
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
PREVIEW = argv[argv.index("--preview") + 1] if "--preview" in argv else None

COLS, ROWS, CELL = 6, 4, 192
FRAMES = COLS * ROWS

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene


def material(name, color, roughness=0.18, coat=0.8, emission=0.0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*color, 1)
    bsdf.inputs["Roughness"].default_value = roughness
    for key, value in (("Coat Weight", coat), ("Coat Roughness", 0.06), ("Emission Strength", emission)):
        if key in bsdf.inputs:
            bsdf.inputs[key].default_value = value
    if emission > 0 and "Emission Color" in bsdf.inputs:
        bsdf.inputs["Emission Color"].default_value = (*color, 1)
    # Alpha drives the fade-out of the broken halves.
    mat.blend_method = 'BLEND' if hasattr(mat, "blend_method") else None
    return mat


RED = material("HeartRed", (0.78, 0.0, 0.05))
RED_DARK = material("HeartRim", (0.30, 0.0, 0.02), roughness=0.3, coat=0.4)
SHINE = material("HeartShine", (1.0, 0.85, 0.88), roughness=0.1, emission=0.6)
CRACK = material("Crack", (0.12, 0.0, 0.02), roughness=0.6, coat=0.0)


def heart_outline(scale, n=96):
    """The classic heart curve, notch at the top (x = 0), tip at the bottom; returned as (x, z) points."""
    pts = []
    for i in range(n):
        t = 2 * math.pi * i / n
        x = 16 * math.sin(t) ** 3
        z = 13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t)
        pts.append((x * scale, (z + 2.5) * scale))
    return pts


def slab(name, outline, depth, mat, bevel, y=0.0, parent=None):
    """A flat (x, z) outline facing -Y, extruded to depth and given a soft, pillowy bevel."""
    bm = bmesh.new()
    front = [bm.verts.new((x, y - depth / 2, z)) for x, z in outline]
    back = [bm.verts.new((x, y + depth / 2, z)) for x, z in outline]
    bm.faces.new(front)
    bm.faces.new(list(reversed(back)))
    n = len(outline)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((front[j], front[i], back[i], back[j]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    for poly in mesh.polygons:
        poly.use_smooth = True
    ob = bpy.data.objects.new(name, mesh)
    scene.collection.objects.link(ob)
    mesh.materials.append(mat)
    mod = ob.modifiers.new("Bevel", 'BEVEL')
    mod.width = bevel
    mod.segments = 10
    mod.limit_method = 'ANGLE'
    mod.angle_limit = math.radians(50)
    ob.parent = parent
    return ob


def shine(parent, scale):
    """A soft white highlight on the upper left lobe, slightly in front of the face."""
    bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=12, radius=1.0)
    ob = bpy.context.active_object
    ob.name = "Shine"
    ob.scale = (0.085 * scale / 0.026, 0.02, 0.055 * scale / 0.026)
    ob.location = (-0.19 * scale / 0.026, -0.165, 0.15 * scale / 0.026)
    ob.rotation_euler = (0, math.radians(-35), 0)
    ob.data.materials.append(SHINE)
    for p in ob.data.polygons:
        p.use_smooth = True
    ob.parent = parent
    return ob


S = 0.026  # outline scale: the heart is about 0.84 wide


def build_heart(name):
    root = bpy.data.objects.new(name, None)
    scene.collection.objects.link(root)
    slab(name + "Rim", heart_outline(S * 1.06), 0.20, RED_DARK, 0.06, parent=root)
    slab(name + "Body", heart_outline(S), 0.32, RED, 0.13, parent=root)
    shine(root, S)
    return root


# ---- the two halves of the broken heart, split along a jagged crack from the notch to the tip
def crack_line(top, bottom, teeth=4, amp=0.06):
    pts = []
    for i in range(teeth + 1):
        f = i / teeth
        z = top + (bottom - top) * f
        x = 0.0 if i in (0, teeth) else (amp if i % 2 else -amp)
        pts.append((x, z))
    return pts


def build_halves():
    full = heart_outline(S)
    n = len(full)
    notch = full[0]
    tip = full[n // 2]
    right = full[:n // 2 + 1]            # notch -> tip down the right side
    left = full[n // 2:] + [full[0]]     # tip -> notch up the left side
    crack = crack_line(notch[1], tip[1])
    halves = []
    for side, outline in (("R", right + list(reversed(crack))[1:-1]), ("L", left + crack[1:-1])):
        root = bpy.data.objects.new("Half" + side, None)
        scene.collection.objects.link(root)
        slab("Half%sBody" % side, outline, 0.32, RED, 0.035, parent=root)
        if side == "L":
            shine(root, S)
        halves.append(root)
    return halves


def setup_render():
    engines = [item.identifier for item in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items]
    scene.render.engine = 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in engines else 'BLENDER_EEVEE'
    scene.render.resolution_x = scene.render.resolution_y = CELL
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = 'PNG'
    scene.render.image_settings.color_mode = 'RGBA'
    scene.view_settings.view_transform = 'Standard'
    scene.view_settings.look = 'None'
    if hasattr(scene, "eevee") and hasattr(scene.eevee, "taa_render_samples"):
        scene.eevee.taa_render_samples = 48
    world = bpy.data.worlds.new("World")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (1.0, 0.92, 0.88, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.55
    scene.world = world
    cam_data = bpy.data.cameras.new("Cam")
    cam_data.type = 'ORTHO'
    cam_data.ortho_scale = 1.2
    cam = bpy.data.objects.new("Cam", cam_data)
    cam.location = (0, -5, 0.02)
    cam.rotation_euler = (math.radians(90), 0, 0)
    scene.collection.objects.link(cam)
    scene.camera = cam

    def light(name, location, energy, size, color=(1, 1, 1)):
        data = bpy.data.lights.new(name, 'AREA')
        data.energy = energy
        data.color = color
        data.size = size
        ob = bpy.data.objects.new(name, data)
        ob.location = location
        scene.collection.objects.link(ob)
        ob.rotation_euler = (-ob.location.normalized()).to_track_quat('-Z', 'Y').to_euler()

    light("Key", (-2.2, -3.2, 2.8), 160, 2.4, (1.0, 0.96, 0.92))
    light("Fill", (2.6, -2.6, -0.6), 45, 3.0, (1.0, 0.82, 0.80))
    light("Top", (0.4, -1.0, 3.2), 60, 1.6)


def render_frame(path):
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    im = bpy.data.images.load(path)
    im.colorspace_settings.name = 'Non-Color'
    px = np.array(im.pixels[:], dtype=np.float32).reshape(CELL, CELL, 4)
    bpy.data.images.remove(im)
    if not PREVIEW:
        os.remove(path)
    return px


def save_sheet(name, frames):
    sheet = np.zeros((ROWS * CELL, COLS * CELL, 4), dtype=np.float32)
    for f, px in enumerate(frames):
        col, row = f % COLS, f // COLS
        y0 = (ROWS - 1 - row) * CELL
        sheet[y0:y0 + CELL, col * CELL:(col + 1) * CELL] = px
    out = bpy.data.images.new(name, COLS * CELL, ROWS * CELL, alpha=True)
    out.colorspace_settings.name = 'Non-Color'
    out.pixels = sheet.ravel().tolist()
    out.filepath_raw = os.path.join(OUT, name + ".png")
    out.file_format = 'PNG'
    out.save()
    print("SHEET", out.filepath_raw)


def show(objs, visible):
    for root in objs:
        for ob in [root] + list(root.children_recursive):
            ob.hide_render = not visible


setup_render()
os.makedirs(OUT, exist_ok=True)
folder = PREVIEW or bpy.app.tempdir
if PREVIEW:
    os.makedirs(PREVIEW, exist_ok=True)

heart = build_heart("Heart")
halves = build_halves()

# ---- spin
show(halves, False)
frames = []
for f in range(FRAMES):
    heart.rotation_euler = (0, 0, f * 2 * math.pi / FRAMES)
    frames.append(render_frame(os.path.join(folder, "spin_%02d.png" % f)))
save_sheet("heart_spin", frames)
heart.rotation_euler = (0, 0, 0)

# ---- still icon (front view, a little larger)
still = render_frame(os.path.join(folder, "heart_still.png"))
icon = bpy.data.images.new("heart", CELL, CELL, alpha=True)
icon.colorspace_settings.name = 'Non-Color'
icon.pixels = still.ravel().tolist()
icon.filepath_raw = os.path.join(OUT, "heart.png")
icon.file_format = 'PNG'
icon.save()

# ---- break: 0-5 swell, 6-9 crack shows (halves pulled a hair apart), 10-23 halves tip apart, drop and fade
frames = []
for f in range(FRAMES):
    if f < 6:
        s = 1.0 + 0.10 * math.sin(f / 5 * math.pi)
        show([heart], True)
        show(halves, False)
        heart.scale = (s, s, s)
        px = render_frame(os.path.join(folder, "break_%02d.png" % f))
    else:
        show([heart], False)
        show(halves, True)
        t = 0.0 if f < 10 else (f - 10) / 13.0
        gap = 0.012 + (f - 6) * 0.004 if f < 10 else 0.03 + 0.25 * t
        for root, side in ((halves[0], 1), (halves[1], -1)):
            root.location = (side * gap, 0, -0.9 * t * t)
            root.rotation_euler = (0, math.radians(side * 38 * t), math.radians(side * 12 * t))
        px = render_frame(os.path.join(folder, "break_%02d.png" % f))
        # fade the halves out over the last frames
        fade = 1.0 if f < 15 else max(0.0, 1.0 - (f - 15) / 8.0)
        px[..., 3] *= fade
    frames.append(px)
save_sheet("heart_break", frames)
