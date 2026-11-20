"""Renders the reward-opening animations: a gift box and a treasure chest bursting open, as flip-books.

Run:  Blender --background --python Tools/art/rewardbox.py [-- --preview DIR]
Output: Assets/MetroSort/Resources/Art/gift_open.png, chest_open.png

Each sheet is the FlipBook grid (6 x 4 = 24 frames, first frame top-left), played once by the reveal
sequence in UIManager:
  0-9   anticipation: the box squashes, stretches and wobbles harder and harder
  10-12 burst: the lid (with its bow) pops off / swings open, light floods out of the box
  13-23 settle: the lid flies away (gift) or rests open (chest), coins leap out and the glow pulses
The game adds rays, flash, confetti and the coins flying to the counter on top.
"""
import bpy, bmesh, math, os, sys
import numpy as np
from mathutils import Vector

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Assets", "MetroSort", "Resources", "Art")
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
PREVIEW = argv[argv.index("--preview") + 1] if "--preview" in argv else None

COLS, ROWS, CELL = 6, 4, 256
FRAMES = COLS * ROWS


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def material(name, color, roughness=0.22, coat=0.6, emission=0.0, metallic=0.0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*color, 1)
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    for key, value in (("Coat Weight", coat), ("Coat Roughness", 0.08), ("Emission Strength", emission)):
        if key in bsdf.inputs:
            bsdf.inputs[key].default_value = value
    if emission > 0 and "Emission Color" in bsdf.inputs:
        bsdf.inputs["Emission Color"].default_value = (*color, 1)
    return mat


def rounded_box(name, size, mat, bevel, location=(0, 0, 0), parent=None):
    bpy.ops.mesh.primitive_cube_add(size=1.0)
    ob = bpy.context.active_object
    ob.name = name
    ob.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    ob.location = location
    mod = ob.modifiers.new("Bevel", 'BEVEL')
    mod.width = bevel
    mod.segments = 6
    for p in ob.data.polygons:
        p.use_smooth = True
    ob.data.materials.append(mat)
    ob.parent = parent
    return ob


def cylinder(name, radius, depth, mat, location=(0, 0, 0), rotation=(0, 0, 0), parent=None, verts=32, bevel=0.01):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=radius, depth=depth, rotation=rotation)
    ob = bpy.context.active_object
    ob.name = name
    ob.location = location
    if bevel > 0:
        mod = ob.modifiers.new("Bevel", 'BEVEL')
        mod.width = bevel
        mod.segments = 3
    for p in ob.data.polygons:
        p.use_smooth = True
    ob.data.materials.append(mat)
    ob.parent = parent
    return ob


def torus(name, major, minor, mat, location, rotation, parent=None):
    bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor, major_segments=32, minor_segments=12,
                                     rotation=rotation)
    ob = bpy.context.active_object
    ob.name = name
    ob.location = location
    for p in ob.data.polygons:
        p.use_smooth = True
    ob.data.materials.append(mat)
    ob.parent = parent
    return ob


def empty(name, location=(0, 0, 0), parent=None):
    ob = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(ob)
    ob.location = location
    ob.parent = parent
    return ob


def setup(scene):
    engines = [item.identifier for item in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items]
    scene.render.engine = 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in engines else 'BLENDER_EEVEE'
    scene.render.resolution_x = scene.render.resolution_y = CELL
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = 'PNG'
    scene.render.image_settings.color_mode = 'RGBA'
    scene.view_settings.view_transform = 'Standard'
    if hasattr(scene.eevee, "taa_render_samples"):
        scene.eevee.taa_render_samples = 32
    if hasattr(scene.eevee, "use_bloom"):
        scene.eevee.use_bloom = True
    world = bpy.data.worlds.new("World")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (1.0, 0.93, 0.85, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.35
    scene.world = world
    cam_data = bpy.data.cameras.new("Cam")
    cam_data.type = 'ORTHO'
    cam_data.ortho_scale = 3.9
    cam = bpy.data.objects.new("Cam", cam_data)
    # A little from above, so the inside of the open box shows.
    cam.location = (0, -6.0, 3.75)
    cam.rotation_euler = (math.radians(62), 0, 0)
    scene.collection.objects.link(cam)
    scene.camera = cam

    def area(name, location, energy, size, color=(1, 1, 1)):
        data = bpy.data.lights.new(name, 'AREA')
        data.energy = energy
        data.size = size
        data.color = color
        ob = bpy.data.objects.new(name, data)
        ob.location = location
        scene.collection.objects.link(ob)
        ob.rotation_euler = (Vector((0, 0, 0.6)) - ob.location).to_track_quat('-Z', 'Y').to_euler()

    area("Key", (-3.0, -4.0, 4.0), 230, 3.0, (1.0, 0.96, 0.90))
    area("Fill", (3.5, -3.0, 1.0), 70, 4.0, (1.0, 0.85, 0.75))
    area("Rim", (0.0, 3.0, 3.5), 140, 3.0)


def render_frame(scene, path):
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


def ease_out_back(t, s=1.7):
    t -= 1
    return t * t * ((s + 1) * t + s) + 1


def build_coins(parent, gold, count=7):
    coins = []
    for i in range(count):
        c = cylinder("Coin%d" % i, 0.17, 0.05, gold, rotation=(math.radians(90), 0, 0), parent=parent, verts=24, bevel=0.015)
        c.hide_render = True
        coins.append(c)
    return coins


def animate_coins(coins, t, top_z):
    """Coins leap out of the box in a fan after the burst (t = 0..1 over the settle phase)."""
    for i, c in enumerate(coins):
        start = i * 0.08
        k = (t - start) / 0.75
        if k <= 0:
            c.hide_render = True
            continue
        c.hide_render = k > 1.0
        k = min(1.0, k)
        spread = (i - (len(coins) - 1) / 2) * 0.28
        c.location = (spread * k * 1.6, -0.1, top_z + 1.9 * k - 1.6 * k * k + 0.2)
        c.rotation_euler = (math.radians(90 + 360 * k * (1 + i % 2)), math.radians(30 * spread), 0)
        s = 1.0 - 0.3 * max(0.0, k - 0.7) / 0.3
        c.scale = (s, s, s)


def glow_parts(parent, top_z, width):
    """Light pouring out of the open box: a soft emissive column and a point light inside."""
    glow_mat = material("Glow", (1.0, 0.80, 0.25), emission=8.0, coat=0.0)
    column = rounded_box("Glow", (width * 0.86, width * 0.86, 0.02), glow_mat, 0.005, (0, 0, top_z - 0.01), parent)
    column.hide_render = True
    data = bpy.data.lights.new("Inside", 'POINT')
    data.energy = 0
    data.color = (1.0, 0.85, 0.45)
    lamp = bpy.data.objects.new("Inside", data)
    bpy.context.scene.collection.objects.link(lamp)
    lamp.location = (0, 0, top_z + 0.45)
    lamp.parent = parent
    return column, lamp


def anticipation(root, f):
    """Frames 0-9: squash and stretch with a growing wobble, as if something inside wants out."""
    k = f / 9.0
    wob = math.sin(f * 2.4) * (0.04 + 0.12 * k)
    sq = 1.0 + math.sin(f * 1.9) * (0.03 + 0.07 * k)
    root.scale = (1.0 / math.sqrt(sq), 1.0 / math.sqrt(sq), sq)
    root.rotation_euler = (0, wob, 0)


# ================================================================ gift box
def gift():
    reset()
    scene = bpy.context.scene
    setup(scene)
    purple = material("Box", (0.24, 0.03, 0.55))
    purple_lid = material("Lid", (0.30, 0.05, 0.64))
    gold = material("Gold", (1.0, 0.50, 0.02), roughness=0.18, metallic=0.6)
    inside = material("Inside", (0.20, 0.04, 0.36), roughness=0.5, coat=0.0)

    root = empty("Gift", (0, 0, -0.9))
    W, H = 1.5, 1.15
    rounded_box("Body", (W, W, H), purple, 0.08, (0, 0, H / 2), root)
    rounded_box("Inner", (W - 0.18, W - 0.18, 0.04), inside, 0.01, (0, 0, H - 0.02), root)
    for axis in (0, 1):
        size = (0.26, W + 0.02, H + 0.01) if axis == 0 else (W + 0.02, 0.26, H + 0.01)
        rounded_box("Ribbon%d" % axis, size, gold, 0.03, (0, 0, H / 2), root)
    # lid with ribbon and bow, hinged nowhere: it pops straight up
    lid = empty("LidRoot", (0, 0, H), root)
    rounded_box("Lid", (W + 0.14, W + 0.14, 0.32), purple_lid, 0.08, (0, 0, 0.12), lid)
    for axis in (0, 1):
        size = (0.27, W + 0.16, 0.33) if axis == 0 else (W + 0.16, 0.27, 0.33)
        rounded_box("LidRibbon%d" % axis, size, gold, 0.03, (0, 0, 0.12), lid)
    for side in (-1, 1):
        torus("Bow%d" % side, 0.26, 0.09, gold, (side * 0.27, 0, 0.47), (math.radians(90), math.radians(side * 25), 0), lid)
    rounded_box("Knot", (0.22, 0.22, 0.2), gold, 0.08, (0, 0, 0.36), lid)

    column, lamp = glow_parts(root, H, W)
    coins = build_coins(root, gold)

    folder = PREVIEW or bpy.app.tempdir
    frames = []
    for f in range(FRAMES):
        if f < 10:
            anticipation(root, f)
            lid.location = (0, 0, H)
            lid.rotation_euler = (0, 0, 0)
        else:
            root.rotation_euler = (0, 0, 0)
            t = (f - 10) / 13.0
            # the box squashes as the lid pops, then springs back
            squash = 1.0 - 0.16 * math.exp(-6 * t) * math.cos(10 * t)
            root.scale = (1.0 / math.sqrt(squash), 1.0 / math.sqrt(squash), squash)
            # the lid shoots up and away to the top right, spinning, and leaves the frame
            up = 5.5 * t
            lid.location = (1.8 * t * t, 0, H + 0.2 + up)
            lid.rotation_euler = (math.radians(-60 * t), math.radians(300 * t), 0)
            lid.hide_render = False
            column.hide_render = False
            lamp.data.energy = 900 * min(1.0, t * 4) * (1.0 + 0.15 * math.sin(f * 1.3))
            animate_coins(coins, t, H)
        frames.append(render_frame(scene, os.path.join(folder, "gift_%02d.png" % f)))
    save_sheet("gift_open", frames)


# ================================================================ treasure chest
def chest():
    reset()
    scene = bpy.context.scene
    setup(scene)
    wood = material("Wood", (0.34, 0.12, 0.03), roughness=0.45, coat=0.3)
    wood_dark = material("WoodDark", (0.18, 0.06, 0.02), roughness=0.5, coat=0.2)
    gold = material("Gold", (1.0, 0.50, 0.02), roughness=0.18, metallic=0.6)
    inside = material("Inside", (0.22, 0.08, 0.03), roughness=0.6, coat=0.0)

    root = empty("Chest", (0, 0, -0.9))
    W, D, H = 1.8, 1.25, 0.95
    rounded_box("Body", (W, D, H), wood, 0.07, (0, 0, H / 2), root)
    rounded_box("Inner", (W - 0.2, D - 0.2, 0.04), inside, 0.01, (0, 0, H - 0.02), root)
    for x in (-0.62, 0.62):
        rounded_box("Band%.1f" % x, (0.16, D + 0.04, H + 0.02), gold, 0.03, (x, 0, H / 2), root)
    rounded_box("Trim", (W + 0.04, D + 0.04, 0.12), wood_dark, 0.03, (0, 0, 0.06), root)
    rounded_box("Lock", (0.3, 0.12, 0.34), gold, 0.05, (0, -D / 2 - 0.04, H - 0.12), root)
    # lid: a half cylinder hinged along the back top edge
    hinge = empty("Hinge", (0, D / 2, H), root)
    lid = empty("Lid", (0, -D / 2, 0), hinge)
    cylinder("LidBody", D / 2, W, wood, location=(0, 0, 0), rotation=(0, math.radians(90), 0), parent=lid, verts=32, bevel=0.04)
    for x in (-0.62, 0.62):
        cylinder("LidBand%.1f" % x, D / 2 + 0.02, 0.16, gold, location=(x, 0, 0), rotation=(0, math.radians(90), 0), parent=lid)
    # hide the lower half of the lid cylinder inside the body by sitting it on the rim
    column, lamp = glow_parts(root, H, W * 0.8)
    coins = build_coins(root, gold, 9)

    folder = PREVIEW or bpy.app.tempdir
    frames = []
    for f in range(FRAMES):
        if f < 10:
            anticipation(root, f)
            hinge.rotation_euler = (math.radians(-abs(6 * math.sin(f * 2.1)) * f / 9.0), 0, 0)
        else:
            root.rotation_euler = (0, 0, 0)
            t = (f - 10) / 13.0
            squash = 1.0 - 0.12 * math.exp(-6 * t) * math.cos(10 * t)
            root.scale = (1.0 / math.sqrt(squash), 1.0 / math.sqrt(squash), squash)
            # the lid swings back past open and settles, like a spring
            angle = 110 * min(1.0, ease_out_back(min(1.0, t * 2.2)))
            hinge.rotation_euler = (math.radians(-angle), 0, 0)
            column.hide_render = False
            lamp.data.energy = 900 * min(1.0, t * 4) * (1.0 + 0.12 * math.sin(f * 1.3))
            animate_coins(coins, t, H)
        frames.append(render_frame(scene, os.path.join(folder, "chest_%02d.png" % f)))
    save_sheet("chest_open", frames)


os.makedirs(OUT, exist_ok=True)
if PREVIEW:
    os.makedirs(PREVIEW, exist_ok=True)
gift()
chest()
