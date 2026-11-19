"""Builds the Metro Sort passenger: a rigged toy-style character with animation clips, exported as FBX.

Run:  Blender --background --python Tools/art/passenger.py -- [--preview DIR]
Output: Assets/MetroSort/Resources/Characters/Passenger.fbx

Blender space: Z up, the character faces -Y, 1 unit = 1 Unity unit. Parts are skinned rigidly (one bone
each). Optional parts (hair styles, hats, skirt ...) are separate objects parented to bones; the game shows
and hides them by name and recolours material slots by name.
"""
import bpy, bmesh, math, os, sys
from mathutils import Vector, Matrix, Euler

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Assets", "MetroSort", "Resources", "Characters", "Passenger.fbx")
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
PREVIEW = argv[argv.index("--preview") + 1] if "--preview" in argv else None

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import kit
from kit import Part, MATS

COLORS = {
    "Jacket": (0.93, 0.33, 0.31), "Skin": (1.0, 0.82, 0.68), "Hair": (0.26, 0.17, 0.11),
    "Lower": (0.20, 0.24, 0.40), "Legs": (0.20, 0.24, 0.40), "Shoes": (0.16, 0.16, 0.20),
    "White": (1, 1, 1), "EyeDark": (0.13, 0.10, 0.12), "Blush": (1.0, 0.60, 0.60),
    "Mouth": (0.72, 0.28, 0.30), "Hat": (0.93, 0.33, 0.31), "Grey": (0.52, 0.55, 0.62),
    "Gold": (1.0, 0.82, 0.22), "Outline": (0.14, 0.15, 0.24),
}
scene = kit.reset(COLORS)


# ---------------------------------------------------------------- proportions
HEAD_C = Vector((0, 0, 1.10))
HEAD_S = Vector((0.34, 0.32, 0.31))
INK = 0.016


def face_y(x, z, lift=0.0):
    """Y of the face surface at (x, z); lift pushes the point out along -Y."""
    k = 1 - (x / HEAD_S.x) ** 2 - ((z - HEAD_C.z) / HEAD_S.z) ** 2
    return -HEAD_S.y * math.sqrt(max(k, 0.0)) - lift


# ---------------------------------------------------------------- armature
arm_data = bpy.data.armatures.new("Rig")
rig = bpy.data.objects.new("Rig", arm_data)
scene.collection.objects.link(rig)
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode='EDIT')
BONES = {
    "Hips": ((0, 0, 0.42), (0, 0, 0.55), None),
    "Spine": ((0, 0, 0.55), (0, 0, 0.84), "Hips"),
    "Head": ((0, 0, 0.86), (0, 0, 1.20), "Spine"),
    "Arm.L": ((0.235, 0, 0.775), (0.315, 0, 0.47), "Spine"),
    "Arm.R": ((-0.235, 0, 0.775), (-0.315, 0, 0.47), "Spine"),
    "Leg.L": ((0.10, 0, 0.42), (0.10, 0, 0.04), "Hips"),
    "Leg.R": ((-0.10, 0, 0.42), (-0.10, 0, 0.04), "Hips"),
}
for name, (head, tail, parent) in BONES.items():
    b = arm_data.edit_bones.new(name)
    b.head, b.tail = head, tail
    if parent:
        b.parent = arm_data.edit_bones[parent]
bpy.ops.object.mode_set(mode='OBJECT')


def skin(ob, bone):
    g = ob.vertex_groups.new(name=bone)
    g.add(range(len(ob.data.vertices)), 1.0, 'REPLACE')
    return ob


def attach(ob, bone):
    """Rigid attachment: parent the object to a bone, keeping its world placement."""
    world = ob.matrix_world.copy()
    ob.parent = rig
    ob.parent_type = 'BONE'
    ob.parent_bone = bone
    bpy.context.view_layer.update()
    ob.matrix_world = world
    return ob


# ---------------------------------------------------------------- body (one skinned mesh)
pieces = []

p = Part()  # pelvis
p.ellipsoid((0, 0, 0.45), (0.205, 0.165, 0.13), "Lower")
pieces.append(skin(p.build("pelvis"), "Hips"))

p = Part()  # torso: a soft bean, wider at the chest
p.ellipsoid((0, 0, 0.655), (0.225, 0.17, 0.215), "Jacket", seg=24, rings=12)
p.ellipsoid((0, 0.005, 0.80), (0.235, 0.15, 0.085), "Jacket")           # shoulders
p.ellipsoid((0, -0.118, 0.795), (0.085, 0.05, 0.07), "White", seg=12, rings=8)  # shirt collar
for i, z in enumerate((0.70, 0.62)):                                   # two buttons
    p.ellipsoid((0, -0.172 + i * 0.004, z), (0.02, 0.012, 0.02), "White", seg=8, rings=6)
pieces.append(skin(p.build("torso"), "Spine"))

for side, suffix in ((1, "L"), (-1, "R")):
    p = Part()
    p.capsule((side * 0.245, 0, 0.775), (side * 0.305, 0, 0.545), 0.072, "Jacket", radius1=0.066)
    p.ellipsoid((side * 0.318, -0.005, 0.475), (0.066, 0.066, 0.07), "Skin", seg=12, rings=8)
    pieces.append(skin(p.build("arm" + suffix), "Arm." + suffix))

    p = Part()
    p.capsule((side * 0.10, 0, 0.40), (side * 0.10, 0, 0.13), 0.088, "Legs", radius1=0.078)
    p.ellipsoid((side * 0.10, -0.045, 0.062), (0.092, 0.145, 0.062), "Shoes", seg=14, rings=8)
    p.ellipsoid((side * 0.10, -0.045, 0.022), (0.097, 0.15, 0.022), "White", seg=14, rings=6)  # sole
    pieces.append(skin(p.build("leg" + suffix), "Leg." + suffix))

p = Part()  # head
p.ellipsoid(HEAD_C, HEAD_S, "Skin", seg=28, rings=14)
p.ellipsoid((0, 0, 0.86), (0.075, 0.07, 0.06), "Skin", seg=12, rings=6)               # neck
for side in (1, -1):
    p.ellipsoid((side * 0.335, 0.01, 1.07), (0.045, 0.05, 0.07), "Skin", seg=10, rings=8)  # ears
p.ellipsoid((0, face_y(0, 1.02, 0.005), 1.02), (0.032, 0.03, 0.028), "Skin", seg=10, rings=8)  # nose
pieces.append(skin(p.build("head"), "Head"))

bpy.ops.object.select_all(action='DESELECT')
for ob in pieces:
    ob.select_set(True)
bpy.context.view_layer.objects.active = pieces[0]
bpy.ops.object.join()
body = bpy.context.view_layer.objects.active
body.name = body.data.name = "Body"

# bake the ink outline into the body, keeping the skin weights of the source vertices
bm = bmesh.new()
bm.from_mesh(body.data)
names = [m.name for m in body.data.materials]
body.data.materials.append(MATS["Outline"])
bm.normal_update()
dup = bmesh.ops.duplicate(bm, geom=list(bm.verts) + list(bm.edges) + list(bm.faces))
for el in dup["geom"]:
    if isinstance(el, bmesh.types.BMVert):
        el.co += el.normal * INK
    elif isinstance(el, bmesh.types.BMFace):
        el.material_index = len(names)
bm.to_mesh(body.data)
bm.free()

body.parent = rig
mod = body.modifiers.new("Armature", 'ARMATURE')
mod.object = rig

# ---------------------------------------------------------------- face (rigid on the head)
EYE_X, EYE_Z = 0.128, 1.065

p = Part()
for side in (1, -1):
    y = face_y(side * EYE_X, EYE_Z)
    p.ellipsoid((side * EYE_X, y + 0.012, EYE_Z), (0.070, 0.03, 0.088), "EyeDark", seg=16, rings=10, rot=(0, 0, side * 14))
    p.ellipsoid((side * EYE_X - 0.02, y - 0.012, EYE_Z + 0.034), (0.024, 0.012, 0.028), "White", seg=10, rings=6)
    p.ellipsoid((side * EYE_X + 0.022, y - 0.013, EYE_Z - 0.03), (0.011, 0.008, 0.012), "White", seg=8, rings=6)
attach(p.build("EyesOpen"), "Head")

p = Part()  # happy closed eyes: two upward arcs
for side in (1, -1):
    y = face_y(side * EYE_X, EYE_Z, 0.004)
    p.torus((side * EYE_X, y, EYE_Z - 0.035), 0.062, 0.014, "EyeDark", seg=12, ring=6, arc=(25, 155), rot=(90, 0, side * 14))
attach(p.build("EyesClosed"), "Head")

p = Part()
for side in (1, -1):
    y = face_y(side * EYE_X, EYE_Z + 0.135, 0.004)
    p.torus((side * EYE_X, y, EYE_Z + 0.02), 0.115, 0.012, "Hair", seg=10, ring=6, arc=(68, 112), rot=(84, 0, side * 16))
attach(p.build("Brows"), "Head")

p = Part()
for side in (1, -1):
    p.ellipsoid((side * 0.215, face_y(side * 0.215, 0.985) + 0.012, 0.985), (0.06, 0.02, 0.04), "Blush", seg=12, rings=6, rot=(0, 0, side * 38))
attach(p.build("Cheeks"), "Head")

p = Part()  # smile
p.torus((0, face_y(0, 0.955, 0.002), 1.01), 0.075, 0.013, "Mouth", seg=12, ring=6, arc=(-128, -52), rot=(98, 0, 0))
attach(p.build("Mouth"), "Head")

p = Part()
for side in (1, -1):
    x = side * (EYE_X + 0.07)
    y = face_y(x, EYE_Z + 0.05, 0.004)
    p.capsule((x - side * 0.012, y, EYE_Z + 0.045), (x + side * 0.03, y + 0.012, EYE_Z + 0.085), 0.009, "EyeDark", seg=6, rings=5)
    p.capsule((x - side * 0.005, y, EYE_Z + 0.02), (x + side * 0.04, y + 0.016, EYE_Z + 0.04), 0.009, "EyeDark", seg=6, rings=5)
attach(p.build("Lashes"), "Head")

p = Part()
for side in (1, -1):
    p.ellipsoid((side * 0.045, face_y(side * 0.045, 0.985, 0.012), 0.985), (0.055, 0.02, 0.022), "Hair", seg=12, rings=6, rot=(0, side * 14, 0))
attach(p.build("Moustache"), "Head")

# ---------------------------------------------------------------- hair styles
HAIR_C = (0, 0.035, 1.19)
HAIR_S = (0.358, 0.345, 0.30)


def hair_cap(p):
    p.ellipsoid(HAIR_C, HAIR_S, "Hair", seg=24, rings=12)


def fringe(p, x=0.05):
    p.ellipsoid((x, -0.245, 1.315), (0.25, 0.10, 0.085), "Hair", seg=16, rings=8, rot=(18, 0, -8))


for style in range(5):
    p = Part()
    hair_cap(p)
    if style == 0:      # short, with a little tuft
        p.ellipsoid((0.02, -0.20, 1.40), (0.16, 0.12, 0.07), "Hair", seg=14, rings=8, rot=(20, 0, 0))
    if style == 1:      # side fringe
        fringe(p)
    if style == 2:      # long hair down to the shoulders
        fringe(p, -0.04)
        p.ellipsoid((0, 0.14, 0.99), (0.33, 0.20, 0.27), "Hair", seg=20, rings=10)
        for side in (1, -1):
            p.capsule((side * 0.30, -0.03, 1.15), (side * 0.29, 0.0, 0.88), 0.075, "Hair", radius1=0.05)
    if style == 3:      # bun
        p.ellipsoid((0, 0.16, 1.50), (0.15, 0.15, 0.14), "Hair", seg=16, rings=8)
        p.ellipsoid((0.03, -0.235, 1.33), (0.22, 0.09, 0.07), "Hair", seg=14, rings=8, rot=(18, 0, 0))
    if style == 4:      # ponytail
        fringe(p, 0.0)
        p.ellipsoid((0, 0.36, 1.28), (0.10, 0.10, 0.10), "Hair", seg=12, rings=8)
        p.capsule((0, 0.42, 1.24), (0, 0.50, 0.95), 0.085, "Hair", radius1=0.04)
    p.outline(INK * 0.8)
    attach(p.build("Hair%d" % style), "Head")

# ---------------------------------------------------------------- line hats (one silhouette per line)
p = Part()  # 0 red: baseball cap
p.ellipsoid((0, 0.03, 1.37), (0.375, 0.365, 0.215), "Hat", seg=24, rings=12)
p.ellipsoid((0, -0.33, 1.375), (0.22, 0.14, 0.028), "Hat", seg=18, rings=6, rot=(14, 0, 0))
p.ellipsoid((0, 0.03, 1.585), (0.045, 0.045, 0.025), "White", seg=10, rings=6)
p.outline(INK * 0.8)
attach(p.build("Hat0"), "Head")

p = Part()  # 1 blue: square conductor hat
p.box((0, 0.03, 1.56), (0.50, 0.50, 0.24), "Hat", bevel=0.05)
p.box((0, 0.03, 1.455), (0.53, 0.53, 0.06), "White", bevel=0.025)
p.outline(INK * 0.8)
attach(p.build("Hat1"), "Head")

p = Part()  # 2 green: top hat
p.cone((0, 0.05, 1.455), 0.38, 0.38, 0.04, "Hat", bevel=0.015)
p.cone((0, 0.03, 1.63), 0.26, 0.29, 0.32, "Hat", bevel=0.03)
p.cone((0, 0.03, 1.51), 0.272, 0.272, 0.055, "White", bevel=0.01)
p.outline(INK * 0.8)
attach(p.build("Hat2"), "Head")

p = Part()  # 3 yellow: party cone with a pompom
p.cone((0, 0.03, 1.67), 0.24, 0.03, 0.46, "Hat", bevel=0.0)
p.torus((0, 0.03, 1.455), 0.235, 0.035, "White", seg=20, ring=8)
p.ellipsoid((0, 0.03, 1.92), (0.075, 0.075, 0.075), "White", seg=12, rings=8)
p.outline(INK * 0.8)
attach(p.build("Hat3"), "Head")

p = Part()  # 4 purple: round ears on a band
p.torus((0, 0.03, 1.19), 0.37, 0.035, "Hat", seg=24, ring=8, arc=(-5, 185), rot=(90, 0, 0))
for side in (1, -1):
    p.ellipsoid((side * 0.25, 0.03, 1.56), (0.16, 0.075, 0.16), "Hat", seg=18, rings=10)
    p.ellipsoid((side * 0.25, -0.03, 1.56), (0.09, 0.03, 0.09), "White", seg=12, rings=6)
p.outline(INK * 0.8)
attach(p.build("Hat4"), "Head")

p = Part()  # 5 orange: wide sun hat
p.cone((0, 0.07, 1.44), 0.45, 0.44, 0.035, "Hat", seg=28, bevel=0.014, rot=(-8, 0, 0))
p.ellipsoid((0, 0.03, 1.44), (0.33, 0.33, 0.20), "Hat", seg=24, rings=12)
p.cone((0, 0.03, 1.475), 0.325, 0.31, 0.05, "White", bevel=0.01)
p.outline(INK * 0.8)
attach(p.build("Hat5"), "Head")

p = Part()  # mystery passenger: grey cap with a floating question mark
p.ellipsoid((0, 0.03, 1.37), (0.38, 0.37, 0.22), "Grey", seg=24, rings=12)
p.outline(INK * 0.8)
p.text("?", (0, -0.02, 1.80), 0.46, 0.035, "White")
attach(p.build("Mystery"), "Head")

p = Part()  # VIP halo
p.torus((0, 0.03, 2.02), 0.24, 0.035, "Gold", seg=28, ring=8)
attach(p.build("Halo"), "Head")

p = Part()  # skirt
p.cone((0, 0, 0.385), 0.31, 0.19, 0.27, "Lower", seg=24, bevel=0.03)
p.outline(INK * 0.8)
attach(p.build("Skirt"), "Hips")


# ---------------------------------------------------------------- animation
rig.animation_data_create()
REST = {b.name: b.matrix_local.to_3x3() for b in arm_data.bones}
for pb in rig.pose.bones:
    pb.rotation_mode = 'QUATERNION'


def reset_pose():
    for pb in rig.pose.bones:
        pb.rotation_quaternion = (1, 0, 0, 0)
        pb.location = (0, 0, 0)
        pb.scale = (1, 1, 1)


HEAD_TILT = -20.0  # the game camera looks steeply down: heads tip back so faces show under the hats


def pose_quat(bone, rot):
    m = REST[bone]
    r = Euler([math.radians(a) for a in rot], 'XYZ').to_matrix()
    if bone == "Head":
        r = r @ Euler((math.radians(HEAD_TILT), 0, 0), 'XYZ').to_matrix()
    return (m.inverted() @ r @ m).to_quaternion()


def key(bone, frame, rot=(0, 0, 0), loc=None, scale=None):
    """rot: world-axis Euler in degrees relative to the rest pose (X pitch, Y roll, Z turn). loc: world offset."""
    pb = rig.pose.bones[bone]
    m = REST[bone]
    pb.rotation_quaternion = pose_quat(bone, rot)
    pb.keyframe_insert("rotation_quaternion", frame=frame)
    if loc is not None:
        pb.location = m.inverted() @ Vector(loc)
        pb.keyframe_insert("location", frame=frame)
    if scale is not None:
        pb.scale = scale
        pb.keyframe_insert("scale", frame=frame)


CLIPS = []


def clip(name, length, pose):
    """pose(t) -> {bone: (rot, loc, scale)} with t in 0..1; sampled every frame so loops are exact."""
    reset_pose()
    action = bpy.data.actions.new(name)
    rig.animation_data.action = action
    for f in range(length + 1):
        t = f / length
        for bone, vals in pose(t).items():
            vals = tuple(vals) + (None,) * (3 - len(vals))
            key(bone, f + 1, vals[0], vals[1], vals[2])
    CLIPS.append((name, action, length))
    rig.animation_data.action = None


def s(t, n=1.0, phase=0.0):
    return math.sin((t * n + phase) * 2 * math.pi)


def idle(t):
    breathe = s(t)
    return {
        "Hips": ((0, 0, 0), (0, 0, 0.006 * breathe)),
        "Spine": ((1.5 * breathe, 0, 0), None, (1 + 0.012 * breathe, 1, 1 + 0.012 * breathe)),
        "Head": ((-1.5 * breathe, 2.5 * s(t, 1, 0.2), 0),),
        "Arm.L": ((0, -3 - 2 * breathe, 0),),
        "Arm.R": ((0, 3 + 2 * breathe, 0),),
        "Leg.L": ((0, 0, 0),),
        "Leg.R": ((0, 0, 0),),
    }


def walk(t):
    sw = s(t)
    return {
        "Hips": ((4, 0, 5 * sw), (0, 0, 0.022 * abs(s(t, 1, 0.25)) - 0.006)),
        "Spine": ((3, 0, -9 * sw),),
        "Head": ((-4, 3 * sw, 4 * sw),),
        "Arm.L": ((34 * sw, -6, 0),),
        "Arm.R": ((-34 * sw, 6, 0),),
        "Leg.L": ((-36 * sw, 0, 0),),
        "Leg.R": ((36 * sw, 0, 0),),
    }


def wave(t):
    w = s(t, 2)
    return {
        "Hips": ((0, 0, 0), (0, 0, 0.012 * abs(s(t)))),
        "Spine": ((-2, -3, 0),),
        "Head": ((-6, -6 + 2 * w, 0),),
        "Arm.L": ((-18, -132 + 18 * w, 0),),
        "Arm.R": ((0, 8, 0),),
        "Leg.L": ((0, 0, 0),),
        "Leg.R": ((0, 0, 0),),
    }


def cheer(t):
    up = abs(s(t, 1))
    w = s(t, 2)
    return {
        "Hips": ((0, 0, 0), (0, 0, 0.07 * up)),
        "Spine": ((-5, 3 * w, 0),),
        "Head": ((-12, 4 * w, 0),),
        "Arm.L": ((-20, -128 - 12 * w, 0),),
        "Arm.R": ((-20, 128 - 12 * w, 0),),
        "Leg.L": ((-10 * up, -6 * up, 0),),
        "Leg.R": ((-10 * up, 6 * up, 0),),
    }


def refuse(t):
    """Shakes the head and droops: an illegal move."""
    sh = s(t, 2)
    return {
        "Hips": ((0, 0, 0), (0, 0, -0.01)),
        "Spine": ((6, 0, 0),),
        "Head": ((8, 0, 22 * sh),),
        "Arm.L": ((0, -2, 0),),
        "Arm.R": ((0, 2, 0),),
        "Leg.L": ((0, 0, 0),),
        "Leg.R": ((0, 0, 0),),
    }


clip("Idle", 60, idle)
clip("Walk", 20, walk)
clip("Wave", 24, wave)
clip("Cheer", 16, cheer)
clip("Refuse", 20, refuse)
reset_pose()

for name, action, length in CLIPS:
    track = rig.animation_data.nla_tracks.new()
    track.name = name
    strip = track.strips.new(name, 1, action)
    strip.name = name
    if hasattr(strip, "action_slot") and strip.action_slot is None and len(action.slots) > 0:
        strip.action_slot = action.slots[0]
    print("STRIP", name, strip.frame_start, strip.frame_end, getattr(strip, "action_slot", None))


# ---------------------------------------------------------------- preview renders
def set_variant(hat, hair, female, closed=False, mystery=False, vip=False):
    show = {"Hat%d" % hat: not mystery, "Hair%d" % hair: True, "Skirt": female, "Lashes": female,
            "Moustache": False, "EyesOpen": not closed, "EyesClosed": closed, "Mystery": mystery, "Halo": vip}
    for ob in scene.objects:
        if ob.type != 'MESH' or ob == body:
            continue
        hidden = ob.name[:3] in ("Hat", "Hai") or ob.name in show
        ob.hide_render = hidden and not show.get(ob.name, False)


def preview(path, pose_clip=None, t=0.25, hat=0, hair=0, female=False, jacket="Jacket", cam_from=(1.6, -4.2, 3.0), **kw):
    set_variant(hat, hair, female, **kw)
    line = [(0.93, 0.33, 0.31), (0.25, 0.56, 0.93), (0.30, 0.76, 0.45), (0.98, 0.80, 0.25), (0.62, 0.42, 0.86), (0.97, 0.58, 0.24)][hat]
    for n in ("Jacket", "Hat"):
        MATS[n].diffuse_color = (*line, 1)
    reset_pose()
    if pose_clip:
        fn = {"Idle": idle, "Walk": walk, "Wave": wave, "Cheer": cheer, "Refuse": refuse}[pose_clip]
        for bone, vals in fn(t).items():
            vals = tuple(vals) + (None,) * (3 - len(vals))
            pb = rig.pose.bones[bone]
            m = REST[bone]
            pb.rotation_quaternion = pose_quat(bone, vals[0])
            if vals[1] is not None:
                pb.location = m.inverted() @ Vector(vals[1])
    cam = scene.camera
    cam.location = cam_from
    direction = Vector((0, 0, 0.95)) - cam.location
    cam.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


if PREVIEW:
    os.makedirs(PREVIEW, exist_ok=True)
    cam_data = bpy.data.cameras.new("Cam")
    cam_data.type = 'ORTHO'
    cam_data.ortho_scale = 2.6
    cam_ob = bpy.data.objects.new("Cam", cam_data)
    scene.collection.objects.link(cam_ob)
    scene.camera = cam_ob
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.display.shading.light = 'STUDIO'
    scene.display.shading.color_type = 'MATERIAL'
    scene.display.shading.show_shadows = True
    scene.display.shading.show_cavity = False
    scene.render.resolution_x = 520
    scene.render.resolution_y = 640
    scene.render.film_transparent = False
    scene.world = bpy.data.worlds.new("W")
    scene.world.color = (0.82, 0.80, 0.76)
    # the hull needs back-face culling to read as an outline in the viewport renderer
    scene.display.shading.show_backface_culling = True
    hull = MATS["Outline"]
    hull.use_backface_culling = True
    for ob in scene.objects:
        if ob.type == 'MESH':
            # flip only the hull faces for the preview (Unity culls front faces on that slot instead)
            me = ob.data
            names = [m.name for m in me.materials]
            if "Outline" in names:
                idx = names.index("Outline")
                bmx = bmesh.new()
                bmx.from_mesh(me)
                bmesh.ops.reverse_faces(bmx, faces=[f for f in bmx.faces if f.material_index == idx])
                bmx.to_mesh(me)
                bmx.free()
    preview(os.path.join(PREVIEW, "front.png"), None, hat=0, hair=1, cam_from=(0.0, -4.5, 1.3))
    preview(os.path.join(PREVIEW, "game_idle.png"), "Idle", hat=1, hair=2, female=True)
    preview(os.path.join(PREVIEW, "walk.png"), "Walk", hat=2, hair=0, cam_from=(3.6, -2.6, 2.2))
    preview(os.path.join(PREVIEW, "wave.png"), "Wave", t=0.1, hat=3, hair=4, female=True)
    preview(os.path.join(PREVIEW, "cheer.png"), "Cheer", t=0.25, hat=4, hair=3, female=True, closed=True)
    preview(os.path.join(PREVIEW, "sunhat.png"), "Refuse", t=0.12, hat=5, hair=1)
    preview(os.path.join(PREVIEW, "mystery.png"), None, hat=0, hair=0, mystery=True, vip=True)
    print("PREVIEWS", PREVIEW)
else:
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.export_scene.fbx(
        filepath=OUT, use_selection=True, object_types={'ARMATURE', 'MESH'},
        add_leaf_bones=False, use_armature_deform_only=False, mesh_smooth_type='OFF',
        apply_scale_options='FBX_SCALE_ALL', use_mesh_modifiers=True,
        bake_anim=True, bake_anim_use_nla_strips=True, bake_anim_use_all_actions=False,
        bake_anim_use_all_bones=True, bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0)
    print("EXPORTED", OUT, os.path.getsize(OUT))
