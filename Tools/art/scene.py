"""Builds the larger Metro Sort station-scene pieces (tunnel portals, the waiting bench), exported as FBX.

Run:  Blender --background --python Tools/art/scene.py -- [--preview DIR]
Output: Assets/MetroSort/Resources/Models/Scene.fbx

Blender space: Z up, -Y faces the game camera, X runs along the track. The game spawns pieces through
ModelKit, which turns them half a turn about the vertical, so Blender +X ends up as Unity -X.
"""
import bpy, os, sys, math, random

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import kit
from kit import Part

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Assets", "MetroSort", "Resources", "Models", "Scene.fbx")
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
PREVIEW = argv[argv.index("--preview") + 1] if "--preview" in argv else None

kit.reset({
    "Stone": (0.80, 0.74, 0.66), "StoneLight": (0.90, 0.85, 0.77), "StoneDark": (0.66, 0.60, 0.53),
    "Coping": (0.95, 0.92, 0.86), "Mouth": (0.06, 0.06, 0.10), "MouthEdge": (0.20, 0.20, 0.27),
    "Trim": (0.98, 0.76, 0.25), "Accent": (0.28, 0.55, 0.62), "White": (1, 1, 1),
    "Grass": (0.47, 0.76, 0.38), "Leaf": (0.36, 0.68, 0.40), "LeafLight": (0.58, 0.84, 0.48),
    "FlowerA": (0.98, 0.52, 0.66), "FlowerB": (1.0, 0.84, 0.30), "FlowerC": (0.98, 0.98, 1.0),
    "Light": (0.35, 0.92, 0.50), "Dark": (0.20, 0.22, 0.27),
    "Wood": (0.80, 0.56, 0.34), "WoodDark": (0.66, 0.44, 0.27), "Iron": (0.18, 0.30, 0.36),
    "Cushion": (0.86, 0.26, 0.30), "CushionLight": (0.95, 0.42, 0.44),
})

# ---------------------------------------------------------------- tunnel portal
# A stone block at the end of a track with the tunnel mouth on its +X face (toward the station once spawned on
# the right-hand side; the left-hand one is mirrored). Seen mostly from above, so the top is a small garden.
L, W, Z0, Z1 = 1.6, 2.5, -0.40, 1.50      # along track, across track, trench floor, top
H = Z1 - Z0
rnd = random.Random(7)

p = Part()
p.box((0, 0, (Z0 + Z1) / 2), (L, W, H), "Stone", bevel=0.10)
# plinth and a gold band under the coping
p.box((0, 0, Z0 + 0.22), (L + 0.06, W + 0.06, 0.18), "StoneDark", bevel=0.04)
p.box((0, 0, Z1 - 0.22), (L + 0.04, W + 0.04, 0.07), "Trim", bevel=0.02)
# ashlar courses on the camera face: slightly proud blocks in two tones, staggered every row
row_h, rows = 0.27, 5
for r in range(rows):
    z = Z0 + 0.42 + r * row_h
    if z + row_h * 0.5 > Z1 - 0.28:
        break
    widths = [0.52, 0.46, 0.54] if r % 2 == 0 else [0.30, 0.50, 0.48, 0.24]
    x = -L / 2 + 0.06
    for i, w in enumerate(widths):
        w = min(w, L / 2 - 0.06 - x)
        if w < 0.08:
            break
        tone = "StoneLight" if (r + i) % 3 == 0 else ("Stone" if (r + i) % 3 == 1 else "StoneDark")
        p.box((x + w / 2, -W / 2 - 0.012, z), (w - 0.035, 0.03, row_h - 0.035), tone, bevel=0.012)
        x += w
# roundel sign on the camera face
p.cone((0, -W / 2 - 0.03, Z1 - 0.62), 0.27, 0.27, 0.035, "Accent", seg=32, bevel=0.008, rot=(90, 0, 0))
p.cone((0, -W / 2 - 0.05, Z1 - 0.62), 0.16, 0.16, 0.035, "White", seg=32, bevel=0.006, rot=(90, 0, 0))
p.box((0, -W / 2 - 0.07, Z1 - 0.62), (0.66, 0.03, 0.12), "Dark", bevel=0.02)

# tunnel mouth on the +X face: a dark arched opening with a stone voussoir ring and a gold keystone
MX = L / 2
mr, mc = 0.86, 0.38                         # arch radius and springing height
p.box((MX - 0.02, 0, (Z0 + mc) / 2), (0.08, 2 * mr, mc - Z0), "Mouth", bevel=0.0)
p.cone((MX - 0.02, 0, mc), mr, mr, 0.08, "Mouth", seg=40, bevel=0.0, rot=(0, 90, 0))
p.torus((MX + 0.02, 0, mc), mr + 0.07, 0.085, "StoneLight", seg=28, ring=8, arc=(0, 180), rot=(90, 0, 90))
for side in (-1, 1):   # jambs
    p.box((MX + 0.02, side * (mr + 0.07), (Z0 + mc) / 2), (0.12, 0.17, mc - Z0), "StoneLight", bevel=0.03)
p.box((MX + 0.04, 0, mc + mr + 0.07), (0.14, 0.20, 0.24), "Trim", bevel=0.03)
# signal lamp beside the mouth, on the platform side
p.box((MX + 0.05, -mr - 0.32, 0.95), (0.10, 0.18, 0.40), "Dark", bevel=0.04)
p.ellipsoid((MX + 0.11, -mr - 0.32, 1.05), (0.04, 0.065, 0.065), "Light", seg=12, rings=8)
p.ellipsoid((MX + 0.11, -mr - 0.32, 0.86), (0.04, 0.06, 0.06), "MouthEdge", seg=12, rings=8)

# garden on top: coping kerb round a bed of grass, two clipped hedges, a round shrub and flowers
p.box((0, 0, Z1 + 0.03), (L + 0.02, W + 0.02, 0.10), "Coping", bevel=0.04)
p.box((0, 0, Z1 + 0.075), (L - 0.22, W - 0.22, 0.04), "Grass", bevel=0.015)
for y in (-0.66, 0.66):
    for k in range(3):
        cx = -0.36 + k * 0.36
        p.ellipsoid((cx, y, Z1 + 0.24), (0.24, 0.30, 0.21), "Leaf", seg=16, rings=10)
        p.ellipsoid((cx - 0.04, y - 0.10, Z1 + 0.34), (0.12, 0.13, 0.08), "LeafLight", seg=12, rings=8)
p.ellipsoid((0.0, 0.0, Z1 + 0.20), (0.26, 0.26, 0.20), "Leaf", seg=16, rings=10)
p.ellipsoid((-0.04, -0.07, Z1 + 0.32), (0.13, 0.13, 0.08), "LeafLight", seg=12, rings=8)
for i in range(26):
    y = rnd.uniform(-1.0, 1.0)
    x = rnd.uniform(-0.62, 0.62)
    on_hedge = abs(abs(y) - 0.66) < 0.24
    z = Z1 + (0.40 if on_hedge else 0.13) + rnd.uniform(-0.03, 0.03)
    if not on_hedge and abs(y) < 0.30 and abs(x) < 0.30:
        z = Z1 + 0.38
    p.ellipsoid((x, y, z), (0.05, 0.05, 0.04), ("FlowerA", "FlowerB", "FlowerC")[i % 3], seg=8, rings=5)
p.build("TunnelPortal")

# ---------------------------------------------------------------- waiting bench
# One seat (1 unit along X) and the cast-iron arm that stands between and at the ends of seats. The seat top
# stays low (0.16) because seated passengers are placed on the bench by the game at that height.
p = Part()
p.box((0, 0.02, 0.035), (1.0, 0.92, 0.07), "Iron", bevel=0.02)               # base rail
for i, y in enumerate((-0.30, -0.12, 0.06, 0.24)):
    p.box((0, y, 0.11), (1.0, 0.165, 0.06), "Wood" if i % 2 == 0 else "WoodDark", bevel=0.02)
p.box((0, -0.04, 0.165), (0.80, 0.60, 0.07), "Cushion", bevel=0.035)          # seat cushion marks the place
p.box((0, -0.10, 0.205), (0.56, 0.32, 0.012), "CushionLight", bevel=0.005)
for z in (0.34, 0.50, 0.66):                                                  # backrest slats, leaning back
    p.box((0, 0.42 + (z - 0.34) * 0.16, z), (1.0, 0.06, 0.12), "Wood", bevel=0.025, rot=(-10, 0, 0))
p.build("BenchSeat")

p = Part()
p.box((0, -0.05, 0.06), (0.10, 0.92, 0.12), "Iron", bevel=0.03)               # foot
p.capsule((0, -0.36, 0.08), (0, -0.33, 0.38), 0.035, "Iron", seg=10, rings=5)  # front post
p.capsule((0, 0.40, 0.06), (0, 0.50, 0.80), 0.04, "Iron", seg=10, rings=5)     # back post
p.box((0, -0.06, 0.40), (0.11, 0.66, 0.05), "Iron", bevel=0.02)               # armrest
p.ellipsoid((0, -0.40, 0.41), (0.065, 0.065, 0.065), "Trim", seg=14, rings=8)
p.ellipsoid((0, 0.51, 0.83), (0.06, 0.06, 0.06), "Trim", seg=14, rings=8)
p.build("BenchArm")

# ---------------------------------------------------------------- queue stanchion and rope
p = Part()
p.cone((0, 0, 0.02), 0.13, 0.13, 0.04, "Trim", seg=24, bevel=0.012)
p.capsule((0, 0, 0.03), (0, 0, 0.50), 0.03, "Trim", seg=12, rings=5)
p.ellipsoid((0, 0, 0.54), (0.06, 0.06, 0.06), "Trim", seg=14, rings=8)
p.build("Stanchion")

if PREVIEW:
    cam = kit.render_setup(6.0, (1100, 760))
    portal = bpy.data.objects["TunnelPortal"]
    portal.location = (-2.0, 0, 0.4)
    seat = bpy.data.objects["BenchSeat"]
    arm = bpy.data.objects["BenchArm"]
    for i, x in enumerate((0.6, 1.6, 2.6)):
        s = seat.copy() if i else seat
        s.location.x = x
        if i:
            kit.scene.collection.objects.link(s)
    for i, x in enumerate((0.1, 1.1, 2.1, 3.1)):
        a = arm.copy() if i else arm
        a.location.x = x
        if i:
            kit.scene.collection.objects.link(a)
    bpy.data.objects["Stanchion"].location = (1.6, -1.2, 0)
    kit.render(cam, os.path.join(PREVIEW, "scene_game.png"), (0.3, -7.0, 9.0), (0.3, 0, 0.4))
    kit.render(cam, os.path.join(PREVIEW, "scene_side.png"), (5.0, -6.0, 4.0), (0.3, 0, 0.5))
else:
    kit.export(OUT)
