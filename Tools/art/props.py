"""Builds the Metro Sort station props, exported as FBX.

Run:  Blender --background --python Tools/art/props.py -- [--preview DIR]
Output: Assets/MetroSort/Resources/Models/Props.fbx

Every object is one prop standing on the floor at its own origin (the clock is centred on its face).
Blender space: Z up, -Y faces the game camera.
"""
import bpy, os, sys, math

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import kit
from kit import Part

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Assets", "MetroSort", "Resources", "Models", "Props.fbx")
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
PREVIEW = argv[argv.index("--preview") + 1] if "--preview" in argv else None

kit.reset({
    "Steel": (0.74, 0.77, 0.84), "Dark": (0.20, 0.22, 0.27), "Light": (0.35, 0.92, 0.50), "Accent": (0.28, 0.55, 0.62),
    "Pot": (0.82, 0.48, 0.34), "Soil": (0.36, 0.25, 0.18), "Leaf": (0.38, 0.72, 0.45), "LeafLight": (0.56, 0.84, 0.52),
    "Wood": (0.78, 0.60, 0.42), "WoodDark": (0.62, 0.45, 0.30), "Bulb": (1.0, 0.97, 0.82),
    "Pillar": (0.72, 0.75, 0.83), "PillarDark": (0.60, 0.63, 0.72), "White": (1, 1, 1), "Glass": (0.70, 0.90, 1.0),
    "Red": (0.93, 0.33, 0.31), "Outline": (0.14, 0.15, 0.24),
})

# ---- ticket gate post
p = Part()
p.box((0, 0, 0.33), (0.24, 0.86, 0.66), "Steel", bevel=0.08)
p.outline(0.02)
p.box((0, 0, 0.20), (0.252, 0.74, 0.10), "Accent", bevel=0.02)                 # line-coloured band
p.box((0, 0.12, 0.662), (0.17, 0.34, 0.02), "Dark", bevel=0.008)               # card reader
p.box((0, 0.12, 0.676), (0.11, 0.16, 0.006), "Glass", bevel=0.0)
p.ellipsoid((0, -0.28, 0.69), (0.055, 0.055, 0.04), "Light", seg=12, rings=8)
p.box((0, -0.432, 0.40), (0.14, 0.012, 0.22), "Dark", bevel=0.004)             # front panel with an arrow
p.box((0, -0.44, 0.40), (0.03, 0.008, 0.13), "Light", bevel=0.0)
for side in (-1, 1):   # arrow head
    p.box((side * 0.024, -0.44, 0.445), (0.026, 0.008, 0.075), "Light", bevel=0.0, rot=(0, side * 42, 0))
p.build("Turnstile")

# ---- potted plant: the pot, and the foliage as its own piece (origin at the soil) so it can sway
p = Part()
p.cone((0, 0, 0.21), 0.22, 0.30, 0.42, "Pot", seg=24, bevel=0.03)
p.torus((0, 0, 0.42), 0.295, 0.04, "Pot", seg=24, ring=8)
p.outline(0.022)
p.cone((0, 0, 0.425), 0.26, 0.26, 0.02, "Soil", seg=20, bevel=0.0)
p.build("Plant")

p = Part()
blobs = [((0, 0, 0.86), 0.40), ((0.24, -0.05, 0.74), 0.27), ((-0.25, 0.04, 0.78), 0.28), ((0.10, 0.18, 1.12), 0.30),
         ((-0.10, -0.16, 1.14), 0.27), ((0.02, 0.0, 1.40), 0.22), ((0.26, 0.10, 1.00), 0.20), ((-0.24, -0.10, 1.02), 0.20)]
for (c, r) in blobs:
    p.ellipsoid(c, (r, r, r * 0.92), "Leaf", seg=16, rings=10)
p.outline(0.022)
for (c, r) in blobs[::2]:   # sunlit tufts on the camera side
    p.ellipsoid((c[0] - 0.05, c[1] - r * 0.55, c[2] + r * 0.45), (r * 0.42, r * 0.36, r * 0.34), "LeafLight", seg=12, rings=8)
p.build("PlantLeaves", origin=(0, 0, 0.42))

# ---- platform lamp
p = Part()
p.cone((0, 0, 0.05), 0.16, 0.10, 0.10, "Dark", seg=20, bevel=0.02)
p.capsule((0, 0, 0.08), (0, 0, 1.86), 0.04, "Dark", seg=10, rings=5)
p.cone((0, 0, 1.90), 0.07, 0.13, 0.08, "Dark", seg=20, bevel=0.015)
p.ellipsoid((0, 0, 2.12), (0.19, 0.19, 0.20), "Bulb", seg=20, rings=12)
p.cone((0, 0, 2.33), 0.13, 0.05, 0.07, "Dark", seg=20, bevel=0.015)
p.ellipsoid((0, 0, 2.39), (0.035, 0.035, 0.04), "Dark", seg=10, rings=6)
p.outline(0.018)
p.build("Lamp")

# ---- slatted bench (long axis along Y)
p = Part()
for i, x in enumerate((-0.19, 0, 0.19)):
    p.box((x, 0, 0.42), (0.16, 1.60, 0.055), "Wood" if i != 1 else "WoodDark", bevel=0.02)
p.outline(0.018)
for y in (-0.58, 0.58):
    p.box((0, y, 0.37), (0.56, 0.07, 0.05), "Dark", bevel=0.015)
    for x in (-0.22, 0.22):
        p.capsule((x, y, 0.36), (x * 1.12, y, 0.03), 0.032, "Dark", seg=8, rings=5)
p.build("Bench")

# ---- wall pillar
p = Part()
p.box((0, 0, 1.5), (0.46, 0.34, 3.0), "Pillar", bevel=0.10)
p.box((0, 0, 0.11), (0.58, 0.44, 0.22), "PillarDark", bevel=0.05)
p.box((0, 0, 2.86), (0.58, 0.44, 0.20), "PillarDark", bevel=0.05)
p.box((0, -0.172, 1.50), (0.20, 0.012, 2.20), "PillarDark", bevel=0.004)
p.build("Pillar")

# ---- wall clock (faces -Y, centred on the dial)
p = Part()
p.cone((0, 0.01, 0), 0.30, 0.30, 0.05, "Dark", seg=32, bevel=0.012, rot=(90, 0, 0))
p.cone((0, -0.012, 0), 0.26, 0.26, 0.02, "White", seg=32, bevel=0.0, rot=(90, 0, 0))
for i in range(12):
    a = math.radians(i * 30)
    big = i % 3 == 0
    p.box((math.sin(a) * 0.215, -0.026, math.cos(a) * 0.215), (0.022 if big else 0.012, 0.006, 0.05 if big else 0.03), "Dark", bevel=0.0, rot=(0, math.degrees(a), 0))
p.box((0.06, -0.034, 0.035), (0.03, 0.008, 0.13), "Dark", bevel=0.004, rot=(0, 60, 0))            # hour hand at 2
p.ellipsoid((0, -0.04, 0), (0.022, 0.012, 0.022), "Dark", seg=10, rings=6)
p.build("Clock")

# the minute and second hands are their own pieces, pointing at 12 and turning about the dial centre
p = Part()
p.box((0, -0.03, 0.075), (0.026, 0.008, 0.17), "Dark", bevel=0.004)
p.build("ClockMinute")
p = Part()
p.box((0, -0.038, 0.06), (0.01, 0.006, 0.20), "Red", bevel=0.0)
p.build("ClockSecond")

# ---- litter bin
p = Part()
p.cone((0, 0, 0.30), 0.19, 0.22, 0.60, "Accent", seg=24, bevel=0.03)
p.outline(0.018)
p.cone((0, 0, 0.63), 0.235, 0.235, 0.06, "Steel", seg=24, bevel=0.02)
p.cone((0, 0, 0.664), 0.13, 0.13, 0.01, "Dark", seg=20, bevel=0.0)
p.box((0, -0.212, 0.34), (0.16, 0.012, 0.16), "White", bevel=0.004)
p.build("Bin")

if PREVIEW:
    cam = kit.render_setup(4.2, (1100, 520))
    for i, n in enumerate(("Turnstile", "Plant", "Lamp", "Bench", "Pillar", "Clock", "Bin")):
        for ob in [o for o in bpy.data.objects if o.name.startswith(n)]:
            ob.location.x = (i - 3) * 1.25
            if n == "Clock":
                ob.location.z = 1.5
    kit.render(cam, os.path.join(PREVIEW, "props.png"), (0, -6, 6.5), (0, 0, 1.1))
else:
    kit.export(OUT)
