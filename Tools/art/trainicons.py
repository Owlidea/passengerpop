"""Renders the small train icons for the "next trains" bar: the toy metro train of the game, nose first, one
picture per line colour.

Run:  Blender --background --python Tools/art/trainicons.py [-- --preview DIR]
Output: Assets/MetroSort/Resources/Art/train_icon_0..5.png (256 x 256, transparent)

The shapes follow Tools/art/station.py (car body, cab), seen from the front left and a little above, lit with
EEVEE so they sit beside the painted artwork. Colours follow Palette.Lines in Visuals.cs.
"""
import bpy, os, sys, math
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import kit
from kit import Part

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Assets", "MetroSort", "Resources", "Art")
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
PREVIEW = argv[argv.index("--preview") + 1] if "--preview" in argv else None

LINES = [(0.93, 0.33, 0.31), (0.25, 0.56, 0.93), (0.30, 0.76, 0.45), (0.98, 0.80, 0.25), (0.62, 0.42, 0.86), (0.97, 0.58, 0.24)]
SIZE = 256
CAR_L, CAR_W = 1.42, 1.06


def srgb_to_linear(c):
    return tuple(((x + 0.055) / 1.055) ** 2.4 if x > 0.04045 else x / 12.92 for x in c)


def build():
    kit.reset({"Body": (1, 1, 1), "Stripe": (1, 1, 1), "Glass": (0.70, 0.90, 1.0), "DarkGlass": (0.13, 0.16, 0.23),
               "Silver": (0.90, 0.92, 0.96), "Under": (0.15, 0.16, 0.20), "Wheel": (0.32, 0.34, 0.40),
               "White": (1, 1, 1), "Lamp": (1.0, 0.97, 0.75), "Sign": (1.0, 0.72, 0.25)})
    # one car behind the cab, running along +X
    p = Part()
    p.box((CAR_L / 2, 0, 0.78), (CAR_L, CAR_W, 0.92), "Body", bevel=0.17)
    p.box((CAR_L / 2, 0, 0.455), (CAR_L - 0.10, CAR_W + 0.012, 0.13), "Stripe", bevel=0.03)
    p.box((CAR_L / 2, 0, 0.955), (CAR_L - 0.16, CAR_W + 0.012, 0.31), "DarkGlass", bevel=0.05)
    for side in (-1, 1):
        for x in (-0.47, 0.47):
            p.box((CAR_L / 2 + x, side * (CAR_W / 2 + 0.004), 0.955), (0.30, 0.02, 0.22), "Glass", bevel=0.008)
        p.box((CAR_L / 2, side * (CAR_W / 2 + 0.002), 0.735), (CAR_L - 0.12, 0.02, 0.04), "White", bevel=0.006)
    p.box((CAR_L / 2, 0, 1.25), (CAR_L - 0.20, 0.60, 0.08), "Silver", bevel=0.035)
    p.box((CAR_L / 2, 0, 0.30), (CAR_L - 0.08, 0.98, 0.14), "Under", bevel=0.03)
    for end in (-1, 1):
        for side in (-1, 1):
            for dx in (-0.115, 0.115):
                p.cone((CAR_L / 2 + end * 0.40 + dx, side * 0.45, 0.15), 0.105, 0.105, 0.05, "Wheel", seg=16, bevel=0.012, rot=(90, 0, 0))
    p.build("Car")
    # the cab, nosing towards -X
    p = Part()
    p.ellipsoid((-0.10, 0, 0.76), (0.50, 0.525, 0.45), "Body", seg=32, rings=16)
    p.ellipsoid((-0.275, 0, 0.935), (0.33, 0.43, 0.25), "DarkGlass", seg=28, rings=14)
    p.ellipsoid((-0.50, 0.17, 1.02), (0.035, 0.07, 0.05), "White", seg=10, rings=6, rot=(0, -35, 0))
    p.box((-0.505, 0, 1.115), (0.05, 0.40, 0.065), "Sign", bevel=0.015, rot=(0, -28, 0))
    p.ellipsoid((-0.30, 0, 0.40), (0.31, 0.45, 0.15), "Under", seg=24, rings=10)
    p.ellipsoid((-0.30, 0, 0.50), (0.315, 0.455, 0.045), "Stripe", seg=24, rings=6)
    for side in (-1, 1):
        p.ellipsoid((-0.535, side * 0.27, 0.62), (0.05, 0.075, 0.075), "White", seg=12, rings=8)
        p.ellipsoid((-0.552, side * 0.27, 0.62), (0.04, 0.055, 0.055), "Lamp", seg=12, rings=8)
    p.build("Cab")


def materials(line):
    body = srgb_to_linear(line)
    stripe = srgb_to_linear(tuple(min(1.0, c * 0.55 + 0.45) for c in line))
    looks = {"Body": (body, 0.25, 0.7, 0.0), "Stripe": (stripe, 0.3, 0.5, 0.0),
             "Glass": (srgb_to_linear((0.70, 0.90, 1.0)), 0.05, 0.9, 0.0), "DarkGlass": (srgb_to_linear((0.10, 0.13, 0.20)), 0.08, 0.9, 0.0),
             "Silver": (srgb_to_linear((0.86, 0.88, 0.93)), 0.2, 0.5, 0.0), "Under": (srgb_to_linear((0.15, 0.16, 0.20)), 0.5, 0.0, 0.0),
             "Wheel": (srgb_to_linear((0.32, 0.34, 0.40)), 0.4, 0.0, 0.0), "White": ((1, 1, 1), 0.2, 0.5, 0.0),
             "Lamp": (srgb_to_linear((1.0, 0.95, 0.70)), 0.2, 0.0, 4.0), "Sign": (srgb_to_linear((1.0, 0.72, 0.25)), 0.25, 0.5, 1.0)}
    for name, (col, rough, coat, emit) in looks.items():
        m = kit.MATS[name]
        m.use_nodes = True
        bsdf = m.node_tree.nodes["Principled BSDF"]
        bsdf.inputs["Base Color"].default_value = (*col, 1)
        bsdf.inputs["Roughness"].default_value = rough
        if "Coat Weight" in bsdf.inputs:
            bsdf.inputs["Coat Weight"].default_value = coat
        if emit > 0 and "Emission Color" in bsdf.inputs:
            bsdf.inputs["Emission Color"].default_value = (*col, 1)
            bsdf.inputs["Emission Strength"].default_value = emit


def setup():
    scene = kit.scene
    engines = [item.identifier for item in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items]
    scene.render.engine = 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in engines else 'BLENDER_EEVEE'
    scene.render.resolution_x = scene.render.resolution_y = SIZE
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = 'PNG'
    scene.render.image_settings.color_mode = 'RGBA'
    scene.view_settings.view_transform = 'Standard'
    if hasattr(scene.eevee, "taa_render_samples"):
        scene.eevee.taa_render_samples = 48
    world = bpy.data.worlds.new("World")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (1.0, 0.95, 0.88, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.6
    scene.world = world
    cam_data = bpy.data.cameras.new("Cam")
    cam_data.type = 'ORTHO'
    cam_data.ortho_scale = 2.6
    cam = bpy.data.objects.new("Cam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    target = Vector((0.42, 0, 0.70))
    cam.location = target + Vector((-3.2, -2.6, 1.5))
    cam.rotation_euler = (target - cam.location).to_track_quat('-Z', 'Y').to_euler()

    def area(name, location, energy, size, color=(1, 1, 1)):
        data = bpy.data.lights.new(name, 'AREA')
        data.energy = energy
        data.size = size
        data.color = color
        ob = bpy.data.objects.new(name, data)
        ob.location = location
        scene.collection.objects.link(ob)
        ob.rotation_euler = (target - ob.location).to_track_quat('-Z', 'Y').to_euler()

    area("Key", (-3.0, -3.0, 3.5), 260, 3.0, (1.0, 0.97, 0.92))
    area("Fill", (2.0, -3.5, 1.0), 90, 4.0, (0.85, 0.90, 1.0))
    area("Rim", (2.5, 2.5, 3.0), 160, 3.0)


build()
setup()
os.makedirs(OUT, exist_ok=True)
folder = PREVIEW or OUT
os.makedirs(folder, exist_ok=True)
for i, line in enumerate(LINES):
    materials(line)
    kit.scene.render.filepath = os.path.join(folder, "train_icon_%d.png" % i)
    bpy.ops.render.render(write_still=True)
    print("ICON", kit.scene.render.filepath)
