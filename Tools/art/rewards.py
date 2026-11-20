"""Renders the spinning 3D reward objects (star and coin) as flip-book sprite sheets.

Run:  Blender --background --python Tools/art/rewards.py [-- --preview DIR]
Output: Assets/MetroSort/Resources/Art/star_spin.png and coin_spin.png

Each sheet is COLS x ROWS frames of one full turn about the vertical axis, first frame top-left and facing
the camera. The game plays them with the FlipBook component (UiKit.cs), which must use the same grid.
Unlike icons.py this uses the lit renderer (EEVEE) with glossy materials, to sit beside the painted artwork
in Resources/Art rather than the flat-shaded icons in Resources/UI.
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


def material(name, color, roughness=0.22, coat=0.6, emission=0.0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*color, 1)
    bsdf.inputs["Roughness"].default_value = roughness
    for key, value in (("Coat Weight", coat), ("Coat Roughness", 0.08), ("Emission Strength", emission)):
        if key in bsdf.inputs:
            bsdf.inputs[key].default_value = value
    if emission > 0 and "Emission Color" in bsdf.inputs:
        bsdf.inputs["Emission Color"].default_value = (*color, 1)
    return mat


GOLD = material("Gold", (1.0, 0.56, 0.01))
GOLD_LIGHT = material("GoldLight", (1.0, 0.66, 0.08), roughness=0.18)
GOLD_DARK = material("GoldDark", (0.78, 0.20, 0.0), roughness=0.35, coat=0.3)


def chaikin(points, rounds):
    """Corner cutting: turns a sharp polygon into a soft, rounded one."""
    for _ in range(rounds):
        out = []
        for i, a in enumerate(points):
            b = points[(i + 1) % len(points)]
            out.append((a[0] * 0.75 + b[0] * 0.25, a[1] * 0.75 + b[1] * 0.25))
            out.append((a[0] * 0.25 + b[0] * 0.75, a[1] * 0.25 + b[1] * 0.75))
        points = out
    return points


def star_outline(outer, inner, rounds=3):
    pts = []
    for i in range(10):
        a = math.pi / 2 + i * math.pi / 5
        r = outer if i % 2 == 0 else inner
        pts.append((math.cos(a) * r, math.sin(a) * r))
    return chaikin(pts, rounds)


def circle_outline(radius, n=64):
    return [(math.cos(i * 2 * math.pi / n) * radius, math.sin(i * 2 * math.pi / n) * radius) for i in range(n)]


def slab(name, outline, depth, mat, bevel, y=0.0, parent=None):
    """A flat shape in the XZ plane (facing -Y), extruded to the given depth and given a pillowy edge."""
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
    mod.segments = 8
    mod.limit_method = 'ANGLE'
    mod.angle_limit = math.radians(50)
    mod.harden_normals = False
    ob.parent = parent
    return ob


def build_star():
    root = bpy.data.objects.new("Star", None)
    scene.collection.objects.link(root)
    # A darker rim behind a fat glossy body, like the painted star in Resources/Art/star.png.
    slab("StarRim", star_outline(0.50, 0.305), 0.20, GOLD_DARK, 0.07, parent=root)
    slab("StarBody", star_outline(0.445, 0.262), 0.30, GOLD, 0.12, parent=root)
    return root


def build_coin():
    root = bpy.data.objects.new("Coin", None)
    scene.collection.objects.link(root)
    slab("CoinBody", circle_outline(0.47), 0.14, GOLD, 0.04, parent=root)
    for side in (-1, 1):
        # Raised ring and a star on both faces, so the coin reads from either side as it turns.
        ring = slab("CoinRing", circle_outline(0.37), 0.03, GOLD_DARK, 0.012, y=side * 0.062, parent=root)
        inner = slab("CoinFace", circle_outline(0.315), 0.034, GOLD_LIGHT, 0.012, y=side * 0.064, parent=root)
        slab("CoinStar", star_outline(0.215, 0.115, rounds=2), 0.05, GOLD, 0.02, y=side * 0.072, parent=root)
    return root


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
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (1.0, 0.93, 0.82, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.50
    scene.world = world

    cam_data = bpy.data.cameras.new("Cam")
    cam_data.type = 'ORTHO'
    cam_data.ortho_scale = 1.12
    cam = bpy.data.objects.new("Cam", cam_data)
    cam.location = (0, -5, 0)
    cam.rotation_euler = (math.radians(90), 0, 0)
    scene.collection.objects.link(cam)
    scene.camera = cam

    def light(name, kind, location, energy, size=None, color=(1, 1, 1)):
        data = bpy.data.lights.new(name, kind)
        data.energy = energy
        data.color = color
        if size:
            data.size = size
        ob = bpy.data.objects.new(name, data)
        ob.location = location
        scene.collection.objects.link(ob)
        # Aim at the origin.
        direction = -ob.location.normalized()
        ob.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()
        return ob

    light("Key", 'AREA', (-2.2, -3.2, 2.8), 150, size=2.4, color=(1.0, 0.97, 0.90))
    light("Fill", 'AREA', (2.6, -2.6, -0.6), 40, size=3.0, color=(1.0, 0.86, 0.70))
    light("Top", 'AREA', (0.4, -1.0, 3.2), 60, size=1.6)


def atlas(name, root):
    """Renders one full turn of root and packs the frames into a sheet, first frame top-left."""
    folder = PREVIEW or bpy.app.tempdir
    sheet = np.zeros((ROWS * CELL, COLS * CELL, 4), dtype=np.float32)
    for frame in range(FRAMES):
        root.rotation_euler = (0, 0, frame * 2 * math.pi / FRAMES)
        path = os.path.join(folder, "%s_%02d.png" % (name, frame))
        scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        im = bpy.data.images.load(path)
        im.colorspace_settings.name = 'Non-Color'
        px = np.array(im.pixels[:], dtype=np.float32).reshape(CELL, CELL, 4)  # rows run bottom to top
        bpy.data.images.remove(im)
        if not PREVIEW:
            os.remove(path)
        col, row = frame % COLS, frame // COLS
        y0 = (ROWS - 1 - row) * CELL
        sheet[y0:y0 + CELL, col * CELL:(col + 1) * CELL] = px
    out = bpy.data.images.new(name, COLS * CELL, ROWS * CELL, alpha=True)
    out.colorspace_settings.name = 'Non-Color'
    out.pixels = sheet.ravel().tolist()
    out.filepath_raw = os.path.join(OUT, name + ".png")
    out.file_format = 'PNG'
    out.save()
    print("SHEET", out.filepath_raw)


setup_render()
os.makedirs(OUT, exist_ok=True)
if PREVIEW:
    os.makedirs(PREVIEW, exist_ok=True)
star, coin = build_star(), build_coin()
for name, root, other in (("star_spin", star, coin), ("coin_spin", coin, star)):
    for ob in [other] + list(other.children):
        ob.hide_render = True
    for ob in [root] + list(root.children):
        ob.hide_render = False
    atlas(name, root)
