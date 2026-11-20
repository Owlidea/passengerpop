"""Builds the Metro Sort train and platform pieces, exported as FBX.

Run:  Blender --background --python Tools/art/station.py -- [--preview DIR]
Output: Assets/MetroSort/Resources/Models/Train.fbx, Platform.fbx

Each object is one reusable piece that the game instantiates by name and places itself. Blender space:
X runs along the track, Z is up, -Y faces the game camera (so the platform-side doors are on -Y for the
train, and the track edge of a platform is +Y).
"""
import bpy, os, sys, math

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import kit
from kit import Part

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Assets", "MetroSort", "Resources", "Models")
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
PREVIEW = argv[argv.index("--preview") + 1] if "--preview" in argv else None

LINE = (0.25, 0.56, 0.93)
TRAIN_COLORS = {
    "Body": LINE, "Stripe": (0.66, 0.80, 0.97), "Glass": (0.70, 0.90, 1.0), "DarkGlass": (0.13, 0.16, 0.23),
    "Silver": (0.90, 0.92, 0.96), "Under": (0.15, 0.16, 0.20), "Wheel": (0.32, 0.34, 0.40),
    "White": (1, 1, 1), "Lamp": (1.0, 0.97, 0.75), "Sign": (1.0, 0.72, 0.25), "Outline": (0.14, 0.15, 0.24),
}
PLATFORM_COLORS = {
    "Slab": (0.90, 0.91, 0.93), "Top": (0.96, 0.97, 0.98), "Skirt": (0.62, 0.66, 0.75),
    "Slot": (0.74, 0.78, 0.85), "SlotRing": (1, 1, 1), "Outline": (0.14, 0.15, 0.24), "Rim": (0.55, 0.62, 0.78),
    "Coping": (1, 1, 1),
}

CAR_L, CAR_W, SIDE = 1.42, 1.06, -0.53


def build_train():
    kit.reset(TRAIN_COLORS)

    # ---- car body: shell, glazing on both sides, roof gear, running gear
    p = Part()
    p.box((0, 0, 0.78), (CAR_L, CAR_W, 0.92), "Body", bevel=0.17)
    p.outline(0.03)
    p.box((0, 0, 0.455), (CAR_L - 0.10, CAR_W + 0.012, 0.13), "Stripe", bevel=0.03)      # lower skirt band
    p.box((0, 0, 0.955), (CAR_L - 0.16, CAR_W + 0.012, 0.31), "DarkGlass", bevel=0.05)   # window band
    for side in (-1, 1):
        for x in (-0.47, 0.47):
            p.box((x, side * (CAR_W / 2 + 0.004), 0.955), (0.30, 0.02, 0.22), "Glass", bevel=0.008)
            p.box((x + 0.075, side * (CAR_W / 2 + 0.012), 1.005), (0.06, 0.012, 0.06), "White", bevel=0.004)
        p.box((0, side * (CAR_W / 2 + 0.002), 0.735), (CAR_L - 0.12, 0.02, 0.04), "White", bevel=0.006)  # waist line
    # doorway recess behind the sliding leaves (platform side) and a painted door on the far side
    p.box((0, SIDE - 0.004, 0.73), (0.47, 0.03, 0.68), "Under", bevel=0.01)
    p.box((0, -SIDE + 0.004, 0.73), (0.44, 0.03, 0.66), "Silver", bevel=0.01)
    # roof
    p.box((0, 0, 1.25), (CAR_L - 0.20, 0.60, 0.08), "Silver", bevel=0.035)
    p.box((0, 0, 1.293), (CAR_L - 0.18, 0.20, 0.014), "Stripe", bevel=0.005)
    p.box((0.32, 0, 1.335), (0.46, 0.40, 0.11), "Silver", bevel=0.04)
    p.cone((0.32, 0, 1.392), 0.125, 0.125, 0.012, "Under", seg=20, bevel=0.0)
    p.torus((0.32, 0, 1.394), 0.125, 0.014, "Silver", seg=20, ring=6)
    for x in (-0.60, -0.10):
        p.box((x, 0.18, 1.30), (0.16, 0.10, 0.035), "Silver", bevel=0.012)
    # running gear
    p.box((0, 0, 0.30), (CAR_L - 0.08, 0.98, 0.14), "Under", bevel=0.03)
    for end in (-1, 1):
        p.box((end * 0.40, 0, 0.17), (0.46, 0.86, 0.14), "Under", bevel=0.03)
        for side in (-1, 1):
            for dx in (-0.115, 0.115):
                p.cone((end * 0.40 + dx, side * 0.45, 0.15), 0.105, 0.105, 0.05, "Wheel", seg=16, bevel=0.012, rot=(90, 0, 0))
                p.cone((end * 0.40 + dx, side * 0.478, 0.15), 0.045, 0.04, 0.012, "Silver", seg=10, bevel=0.0, rot=(90, 0, 0))
    p.build("Car")

    # ---- one sliding door leaf, centred on its own origin (the game slides two of them apart)
    for name in ("DoorL", "DoorR"):
        sgn = -1 if name == "DoorL" else 1
        p = Part()
        p.box((0, 0, 0), (0.215, 0.028, 0.64), "Silver", bevel=0.008)
        p.box((0, -0.006, 0.10), (0.14, 0.028, 0.30), "Glass", bevel=0.02)
        p.box((0.04, -0.016, 0.16), (0.03, 0.01, 0.08), "White", bevel=0.003)
        p.box((-sgn * 0.10, -0.004, 0), (0.016, 0.03, 0.62), "Under", bevel=0.004)   # rubber edge where the leaves meet
        p.box((-sgn * 0.06, -0.016, -0.06), (0.018, 0.012, 0.11), "Under", bevel=0.004)  # handle
        p.build(name)

    # ---- gangway between two cars, centred on the joint
    p = Part()
    for i, dx in enumerate((-0.05, 0, 0.05)):
        p.box((dx, 0, 0.56), (0.06, 0.74 - (i % 2) * 0.05, 0.54 - (i % 2) * 0.05), "Under", bevel=0.02)
    p.box((0, 0, 0.26), (0.20, 0.10, 0.06), "Wheel", bevel=0.015)
    p.build("Coupler")

    # ---- pantograph: a base plate, and the sprung arm as its own piece (origin at its foot) so the game
    #      can make it bob against the wire
    p = Part()
    p.box((0, 0, 1.32), (0.34, 0.30, 0.04), "Under", bevel=0.012)
    p.build("Pantograph")
    p = Part()
    for side in (-1, 1):
        p.capsule((side * 0.13, 0, 1.33), (0, 0, 1.47), 0.016, "Under", seg=6, rings=5)
        p.capsule((0, 0, 1.47), (side * 0.10, 0, 1.61), 0.014, "Under", seg=6, rings=5)
    p.capsule((0.10, -0.30, 1.615), (0.10, 0.30, 1.615), 0.018, "Wheel", seg=6, rings=5)
    p.capsule((-0.10, -0.30, 1.615), (-0.10, 0.30, 1.615), 0.018, "Wheel", seg=6, rings=5)
    p.build("PantographArm", origin=(0, 0, 1.33))

    # ---- driver cab: joins a car end at x = 0 and noses towards -X
    p = Part()
    p.ellipsoid((-0.10, 0, 0.76), (0.50, 0.525, 0.45), "Body", seg=28, rings=14)
    p.outline(0.03)
    p.ellipsoid((-0.275, 0, 0.935), (0.33, 0.43, 0.25), "DarkGlass", seg=24, rings=12)
    p.ellipsoid((-0.50, 0.17, 1.02), (0.035, 0.07, 0.05), "White", seg=10, rings=6, rot=(0, -35, 0))  # windscreen glint
    p.box((-0.505, 0, 1.115), (0.05, 0.40, 0.065), "Sign", bevel=0.015, rot=(0, -28, 0))
    p.ellipsoid((-0.30, 0, 0.40), (0.31, 0.45, 0.15), "Under", seg=20, rings=10)
    p.ellipsoid((-0.30, 0, 0.50), (0.315, 0.455, 0.045), "Stripe", seg=20, rings=6)
    for side in (-1, 1):
        p.ellipsoid((-0.535, side * 0.27, 0.62), (0.05, 0.075, 0.075), "White", seg=12, rings=8)
        p.ellipsoid((-0.552, side * 0.27, 0.62), (0.04, 0.055, 0.055), "Lamp", seg=12, rings=8)
    p.box((-0.60, 0, 0.36), (0.10, 0.16, 0.08), "Wheel", bevel=0.02)   # coupler
    p.build("Cab")

    if PREVIEW:
        # lay a three-car train out the way the game assembles it
        import bmesh
        cam = kit.render_setup(6.4, (900, 520))
        car = bpy.data.objects["Car"]
        for i, x in enumerate((-1.5, 1.5)):
            c = car.copy()
            c.location.x = x
            kit.scene.collection.objects.link(c)
        for x in (-1.5, 0, 1.5):
            for name, dx in (("DoorL", -0.108), ("DoorR", 0.108)):
                d = bpy.data.objects[name].copy()
                d.location = (x + dx * (2.6 if x == 0 else 1), SIDE - 0.012, 0.72)
                kit.scene.collection.objects.link(d)
        for name in ("DoorL", "DoorR"):
            bpy.data.objects[name].hide_render = True
        for x in (-0.75, 0.75):
            g = bpy.data.objects["Coupler"].copy()
            g.location.x = x
            kit.scene.collection.objects.link(g)
        bpy.data.objects["Coupler"].hide_render = True
        bpy.data.objects["Pantograph"].location.x = -0.34
        bpy.data.objects["PantographArm"].location.x = -0.34
        cab = bpy.data.objects["Cab"]
        cab.location.x = -1.5 - CAR_L / 2
        rear = cab.copy()
        rear.location.x = 1.5 + CAR_L / 2
        rear.rotation_euler = (0, 0, math.pi)
        kit.scene.collection.objects.link(rear)
        kit.render(cam, os.path.join(PREVIEW, "train_game.png"), (0, -6, 9), (0, 0, 0.6))
        kit.render(cam, os.path.join(PREVIEW, "train_side.png"), (-5, -8, 3.5), (0, 0, 0.7))
    else:
        kit.export(os.path.join(OUT, "Train.fbx"))


def build_platform():
    kit.reset(PLATFORM_COLORS)
    D = 1.5  # platform depth; the game stretches Mid along X between two Caps

    p = Part()   # round end
    p.cone((0, 0, 0.14), D / 2, D / 2, 0.28, "Slab", seg=40, bevel=0.045)
    p.outline(0.03)
    p.cone((0, 0, 0.05), D / 2 + 0.08, D / 2 + 0.08, 0.10, "Skirt", seg=40, bevel=0.02)
    p.cone((0, 0, 0.195), D / 2 + 0.008, D / 2 + 0.008, 0.055, "Rim", seg=40, bevel=0.012)       # line-coloured band
    p.cone((0, 0, 0.283), D / 2 - 0.10, D / 2 - 0.10, 0.012, "Top", seg=40, bevel=0.004)         # framed walking surface (just under the middle's, so no seam shows)
    p.torus((0, 0, 0.291), D / 2 - 0.09, 0.04, "Coping", seg=40, ring=8, arc=(-90, 90))  # raised kerb round the outer half
    p.build("Cap")

    p = Part()   # straight middle, one unit long
    h, r = D / 2, 0.045
    slab = [(-h, 0), (h, 0), (h, 0.28 - r), (h - r * 0.3, 0.28 - r * 0.3), (h - r, 0.28),
            (-h + r, 0.28), (-h + r * 0.3, 0.28 - r * 0.3), (-h, 0.28 - r)]
    p.profile(slab, -0.5, 0.5, "Slab", smooth=False)
    p.outline(0.03)
    k = h + 0.08
    p.profile([(-k, 0), (k, 0), (k, 0.08), (k - 0.02, 0.10), (-k + 0.02, 0.10), (-k, 0.08)], -0.5, 0.5, "Skirt", smooth=False)
    b = h + 0.008
    p.profile([(-b, 0.168), (b, 0.168), (b, 0.222), (-b, 0.222)], -0.5, 0.5, "Rim", smooth=False)
    t = h - 0.10
    p.profile([(-t, 0.278), (t, 0.278), (t, 0.29), (-t, 0.29)], -0.5, 0.5, "Top", smooth=False)
    for sy in (-1, 1):   # raised kerb along both long edges, continuing the ring on the caps
        c = sy * (h - 0.09)
        ring = [(c + 0.04 * math.cos(a * math.pi / 4), 0.291 + 0.04 * math.sin(a * math.pi / 4)) for a in range(8)]
        p.profile(ring, -0.5, 0.5, "Coping")
    p.build("Mid")

    p = Part()   # standing spot: a soft ring around a centre dot
    p.cone((0, 0, 0.004), 0.31, 0.31, 0.008, "Slot", seg=32, bevel=0.0)
    p.torus((0, 0, 0.008), 0.30, 0.022, "SlotRing", seg=32, ring=6)
    p.cone((0, 0, 0.006), 0.10, 0.10, 0.008, "SlotRing", seg=20, bevel=0.0)
    p.build("Slot")

    if PREVIEW:
        cam = kit.render_setup(5.0, (900, 520))
        cap = bpy.data.objects["Cap"]
        mid = bpy.data.objects["Mid"]
        mid.scale.x = 2.7
        cap.location.x = -1.35
        c2 = cap.copy()
        c2.location.x = 1.35
        kit.scene.collection.objects.link(c2)
        slot = bpy.data.objects["Slot"]
        slot.location = (0, 0.10, 0.28)
        for x in (-0.95, 0.95):
            s2 = slot.copy()
            s2.location.x = x
            kit.scene.collection.objects.link(s2)
        kit.render(cam, os.path.join(PREVIEW, "platform.png"), (0, -5, 8), (0, 0, 0.1))
    else:
        kit.export(os.path.join(OUT, "Platform.fbx"))


build_train()
build_platform()
