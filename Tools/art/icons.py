"""Renders the Metro Sort UI icons as transparent PNG sprites.

Run:  Blender --background --python Tools/art/icons.py
Output: Assets/MetroSort/Resources/UI/<name>.png (256 x 256)

Icons are small toy-like 3D shapes in the XZ plane, facing -Y, about one unit across, each with a thick
outline in a darker shade of its own colour.
"""
import bpy, os, sys, math

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import kit
from kit import Part

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Assets", "MetroSort", "Resources", "UI")

kit.reset({
    "Gold": (1.0, 0.70, 0.08), "GoldLight": (1.0, 0.92, 0.45), "GoldDark": (0.93, 0.58, 0.05), "OutlineGold": (0.52, 0.27, 0.02),
    "White": (1, 1, 1), "OutlineInk": (0.13, 0.16, 0.30),
    "Grey": (0.80, 0.83, 0.89), "GreyDark": (0.62, 0.66, 0.75), "OutlineGrey": (0.38, 0.42, 0.52),
    "Steel": (0.86, 0.89, 0.95), "Dark": (0.22, 0.25, 0.36),
    "Red": (0.96, 0.33, 0.33), "RedDark": (0.80, 0.20, 0.24), "OutlineRed": (0.45, 0.08, 0.14),
    "Blue": (0.25, 0.60, 0.98), "Green": (0.30, 0.80, 0.47), "Yellow": (1.0, 0.82, 0.25), "Purple": (0.64, 0.45, 0.90),
    "Orange": (0.99, 0.60, 0.22), "OrangeDark": (0.90, 0.45, 0.12), "OutlineOrange": (0.50, 0.22, 0.03),
    "Sky": (0.72, 0.86, 1.0),
})
INK = 0.045


def star_points(outer, inner, n=5):
    pts = []
    for i in range(n * 2):
        a = math.pi / 2 + i * math.pi / n
        r = outer if i % 2 == 0 else inner
        pts.append((math.cos(a) * r, math.sin(a) * r))
    return pts


def arrow_arc(p, a0, a1, mat, major=0.30, minor=0.075):
    """A curved arrow in the XZ plane from angle a0 to a1 (degrees), head at a1."""
    lo, hi = min(a0, a1), max(a0, a1)
    p.torus((0, 0, 0), major, minor, mat, seg=20, ring=8, arc=(lo, hi), rot=(90, 0, 0))
    t = math.radians(a1)
    turn = -a1 if a1 > a0 else 180 - a1
    p.cone((math.cos(t) * major, 0, math.sin(t) * major), 0.17, 0.0, 0.22, mat, seg=16, bevel=0.0, rot=(0, turn, 0))


ICONS = {}


def icon(name):
    def wrap(fn):
        ICONS[name] = fn
        return fn
    return wrap


@icon("coin")
def _(p):
    p.cone((0, 0, 0), 0.46, 0.46, 0.13, "Gold", seg=40, bevel=0.035, rot=(90, 0, 0))
    p.outline(INK, "OutlineGold")
    p.torus((0, -0.065, 0), 0.335, 0.03, "GoldDark", seg=36, ring=6, rot=(90, 0, 0))
    p.prism(star_points(0.21, 0.10), 0.03, "GoldLight", bevel=0.008, y=-0.075)


@icon("star")
def _(p):
    p.prism(star_points(0.50, 0.24), 0.18, "Gold", bevel=0.06)
    p.outline(INK, "OutlineGold")
    p.ellipsoid((-0.10, -0.10, 0.12), (0.07, 0.02, 0.05), "GoldLight", seg=12, rings=6, rot=(0, 30, 0))


@icon("star_off")
def _(p):
    p.prism(star_points(0.50, 0.24), 0.18, "Grey", bevel=0.06)
    p.outline(INK, "OutlineGrey")


@icon("gear")
def _(p):
    pts = []
    teeth = 8
    for i in range(teeth * 4):
        a = 2 * math.pi * (i + 0.5) / (teeth * 4)
        r = 0.47 if (i % 4) in (0, 1) else 0.35
        pts.append((math.cos(a) * r, math.sin(a) * r))
    p.prism(pts, 0.14, "White", bevel=0.03)
    p.outline(INK, "OutlineInk")
    p.cone((0, -0.06, 0), 0.15, 0.15, 0.06, "Dark", seg=24, bevel=0.0, rot=(90, 0, 0))


@icon("pause")
def _(p):
    for x in (-0.17, 0.17):
        p.box((x, 0, 0), (0.22, 0.14, 0.72), "White", bevel=0.06)
    p.outline(INK, "OutlineInk")


@icon("play")
def _(p):
    p.prism([(-0.30, 0.40), (-0.30, -0.40), (0.42, 0.0)], 0.16, "White", bevel=0.06)
    p.outline(INK, "OutlineInk")


@icon("undo")
def _(p):
    arrow_arc(p, -60, 215, "White", major=0.30)
    p.outline(INK, "OutlineInk")


@icon("shuffle")
def _(p):
    arrow_arc(p, 20, 150, "White", major=0.33, minor=0.07)
    arrow_arc(p, 200, 330, "White", major=0.33, minor=0.07)
    p.outline(INK, "OutlineInk")


@icon("hint")
def _(p):
    p.ellipsoid((0, 0, 0.10), (0.31, 0.26, 0.33), "Yellow", seg=24, rings=14)
    p.cone((0, 0, -0.24), 0.15, 0.19, 0.14, "Yellow", seg=20, bevel=0.02)
    p.cone((0, 0, -0.35), 0.14, 0.15, 0.10, "Steel", seg=20, bevel=0.02)
    p.cone((0, 0, -0.43), 0.07, 0.11, 0.07, "Dark", seg=16, bevel=0.015)
    p.outline(INK, "OutlineGold")
    p.ellipsoid((-0.12, -0.22, 0.22), (0.07, 0.03, 0.10), "White", seg=12, rings=6, rot=(0, 30, 0))


@icon("platform")
def _(p):
    p.box((0, 0, 0.10), (0.20, 0.14, 0.66), "White", bevel=0.05)
    p.box((0, 0, 0.10), (0.66, 0.14, 0.20), "White", bevel=0.05)
    p.outline(INK, "OutlineInk")


@icon("lock")
def _(p):
    p.torus((0, 0, 0.08), 0.19, 0.06, "Steel", seg=20, ring=8, arc=(0, 180), rot=(90, 0, 0))
    for x in (-0.19, 0.19):
        p.capsule((x, 0, 0.08), (x, 0, -0.10), 0.06, "Steel", seg=8, rings=5)
    p.box((0, 0, -0.18), (0.62, 0.22, 0.46), "Grey", bevel=0.09)
    p.outline(INK, "OutlineGrey")
    p.ellipsoid((0, -0.11, -0.13), (0.065, 0.03, 0.065), "Dark", seg=12, rings=8)
    p.box((0, -0.11, -0.23), (0.05, 0.03, 0.14), "Dark", bevel=0.01)


@icon("gift")
def _(p):
    p.box((0, 0, -0.17), (0.66, 0.40, 0.48), "Red", bevel=0.05)
    p.box((0, 0, 0.12), (0.76, 0.46, 0.16), "RedDark", bevel=0.04)
    for side in (-1, 1):
        p.ellipsoid((side * 0.14, 0, 0.30), (0.15, 0.08, 0.10), "Yellow", seg=14, rings=8, rot=(0, side * -25, 0))
    p.outline(INK, "OutlineRed")
    p.box((0, 0, -0.06), (0.15, 0.48, 0.74), "Yellow", bevel=0.02)
    p.ellipsoid((0, -0.02, 0.27), (0.07, 0.07, 0.07), "GoldDark", seg=12, rings=8)


@icon("shop")
def _(p):
    p.torus((0, 0.02, 0.16), 0.17, 0.035, "Dark", seg=20, ring=8, arc=(0, 180), rot=(90, 0, 0))
    p.box((0, 0, -0.12), (0.66, 0.26, 0.60), "Orange", bevel=0.07)
    p.outline(INK, "OutlineOrange")
    p.box((0, -0.005, 0.13), (0.67, 0.27, 0.10), "OrangeDark", bevel=0.03)
    p.prism(star_points(0.15, 0.07), 0.03, "White", bevel=0.006, y=-0.14)


@icon("levels")
def _(p):
    for (x, z, mat) in ((-0.23, 0.23, "Red"), (0.23, 0.23, "Blue"), (-0.23, -0.23, "Green"), (0.23, -0.23, "Yellow")):
        p.box((x, 0, z), (0.38, 0.14, 0.38), mat, bevel=0.08)
    p.outline(INK, "OutlineInk")


@icon("home")
def _(p):
    p.prism([(-0.48, 0.02), (0.0, 0.46), (0.48, 0.02), (0.34, 0.02), (0.34, -0.42), (-0.34, -0.42), (-0.34, 0.02)], 0.16, "White", bevel=0.05)
    p.outline(INK, "OutlineInk")
    p.box((0, -0.08, -0.26), (0.20, 0.03, 0.30), "Dark", bevel=0.03)


@icon("restart")
def _(p):
    arrow_arc(p, 110, -200, "White", major=0.31)
    p.outline(INK, "OutlineInk")


@icon("check")
def _(p):
    p.capsule((-0.30, 0, -0.02), (-0.08, 0, -0.26), 0.10, "White", seg=10, rings=7)
    p.capsule((-0.08, 0, -0.26), (0.34, 0, 0.26), 0.10, "White", seg=10, rings=7)
    p.outline(INK, "OutlineInk")


def grade(path, gain=1.58, saturation=1.18):
    """The viewport renderer's studio light is dim and washes colours out: lift whites to white and restore
    saturation, working on the stored pixel values."""
    import numpy as np
    im = bpy.data.images.load(path)
    im.colorspace_settings.name = 'Non-Color'
    px = np.array(im.pixels[:], dtype=np.float32).reshape(-1, 4)
    rgb = px[:, :3] * gain
    mean = rgb.mean(axis=1, keepdims=True)
    px[:, :3] = np.clip(mean + (rgb - mean) * saturation, 0.0, 1.0)
    im.pixels = px.ravel().tolist()
    im.filepath_raw = path
    im.file_format = 'PNG'
    im.save()
    bpy.data.images.remove(im)


os.makedirs(OUT, exist_ok=True)
objs = {}
for name, fn in ICONS.items():
    part = Part()
    fn(part)
    objs[name] = part.build(name)

cam = kit.render_setup(1.22, (256, 256))
scene = kit.scene
scene.render.film_transparent = True
scene.render.image_settings.file_format = 'PNG'
scene.render.image_settings.color_mode = 'RGBA'
scene.view_settings.view_transform = 'Standard'
scene.display.shading.show_shadows = False
scene.display.shading.show_specular_highlight = True
scene.display.shading.show_cavity = False
scene.display.render_aa = '16'
for name, ob in objs.items():
    for other in objs.values():
        other.hide_render = other != ob
    path = os.path.join(OUT, name + ".png")
    kit.render(cam, path, (0.0, -5.0, 0.9), (0, 0, 0))
    grade(path)
print("ICONS", len(objs), OUT)
