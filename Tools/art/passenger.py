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
# --hd MODEL.glb: use a sculpted, textured model (e.g. made with TRELLIS from a concept picture) as the body
# instead of the built-from-shapes one. The rig, clips and hats stay the same; output is PassengerHD.fbx.
HD = "--hd" in argv
HD_SRC = argv[argv.index("--hd") + 1] if HD else None
HD_VARIANT = int(argv[argv.index("--variant") + 1]) if "--variant" in argv else 0
if HD:
    OUT = os.path.join(ROOT, "Assets", "MetroSort", "Resources", "Characters", "PassengerHD_%d.fbx" % HD_VARIANT)
HD_TRIS = 6000        # phones draw dozens of passengers at once: the scanned mesh is reduced to about this

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import kit
from kit import Part, MATS

COLORS = {
    "Jacket": (0.93, 0.33, 0.31), "Skin": (1.0, 0.82, 0.68), "Hair": (0.26, 0.17, 0.11),
    "Lower": (0.20, 0.24, 0.40), "Legs": (0.20, 0.24, 0.40), "Shoes": (0.16, 0.16, 0.20),
    "White": (1, 1, 1), "EyeDark": (0.13, 0.10, 0.12), "Blush": (1.0, 0.60, 0.60),
    "Mouth": (0.72, 0.28, 0.30), "MouthIn": (0.42, 0.12, 0.16), "SkinFlat": (1.0, 0.82, 0.68), "Hat": (0.93, 0.33, 0.31), "Grey": (0.52, 0.55, 0.62),
    "Gold": (1.0, 0.82, 0.22), "Outline": (0.14, 0.15, 0.24),
}
scene = kit.reset(COLORS)


# ---------------------------------------------------------------- proportions
HEAD_C = Vector((0, 0, 1.10))
HEAD_S = Vector((0.34, 0.32, 0.31))
INK = 0.011
HEAD_K = 1.10            # the whole head (face, hair, hats) is built at 1:1 and then grown about the neck
NECK = Vector((0, 0, 0.86))


def face_y(x, z, lift=0.0):
    """Y of the face surface at (x, z); lift pushes the point out along -Y."""
    k = 1 - (x / HEAD_S.x) ** 2 - ((z - HEAD_C.z) / HEAD_S.z) ** 2
    return -HEAD_S.y * math.sqrt(max(k, 0.0)) - lift


# ---------------------------------------------------------------- HD body: import and measure
HD_H = 1.52           # overall height of the HD character (top of the hair), about that of the built one


def smooth01(a, b, x):
    t = min(1.0, max(0.0, (x - a) / (b - a)))
    return t * t * (3 - 2 * t)


if HD:
    bpy.ops.import_scene.gltf(filepath=HD_SRC)
    hd = next(o for o in scene.objects if o.type == 'MESH')
    hd.data.transform(hd.matrix_world)
    hd.parent = None
    hd.matrix_world = Matrix.Identity(4)
    for o in [o for o in scene.objects if o.type != 'MESH']:
        bpy.data.objects.remove(o)

    vz = [v.co.z for v in hd.data.vertices]
    vx = [v.co.x for v in hd.data.vertices]
    vy = [v.co.y for v in hd.data.vertices]
    k = HD_H / (max(vz) - min(vz))
    off = Vector(((max(vx) + min(vx)) / 2, (max(vy) + min(vy)) / 2, min(vz)))
    for v in hd.data.vertices:
        v.co = (v.co - off) * k
    V = [v.co.copy() for v in hd.data.vertices]
    H = HD_H
    # torso half-width at the jacket hem, where the arms are well clear of the body
    def torso_edge(z0, z1):
        """Half-width of the body in a horizontal band: walking outwards from 8% of the height (the scan is
        a hollow shell, so the middle is sparse), the body ends at the first two empty 1%-wide strips, which
        leaves out arms and hands hanging beside it."""
        bins = [0] * 60
        for c in V:
            if z0 < c.z < z1:
                i = int(abs(c.x) / (0.01 * H))
                if i < 60:
                    bins[i] += 1
        for i in range(8, 58):
            if bins[i] == 0 and bins[i + 1] == 0:
                return i * 0.01 * H
        return None
    bands = sorted(b for b in (torso_edge(0.27 * H, 0.30 * H), torso_edge(0.31 * H, 0.34 * H)) if b)
    TX = 1.04 * (bands[0] if bands else 0.17 * H)
    # neck: the narrowest slice between the shoulders and the head (arms left out)
    best = None
    for i in range(40):
        z = (0.48 + 0.11 * i / 39) * H
        sl = [abs(c.x) for c in V if abs(c.z - z) < 0.006 * H and abs(c.x) < TX * 1.2]
        if len(sl) > 8 and (best is None or max(sl) < best[0]):
            best = (max(sl), z)
    NECK_Z = best[1]
    arm = [c for c in V if abs(c.x) > TX and 0.30 * H < c.z < NECK_Z - 0.02 * H]
    tip_x = max(abs(c.x) for c in arm)
    # the arm's centre line, from two slices well clear of the body, carried back to the shoulder
    def slice_mid(x):
        sl = [c for c in arm if abs(abs(c.x) - x) < 0.02 * H]
        return Vector((x, sum(c.y for c in sl) / len(sl), sum(c.z for c in sl) / len(sl)))
    SH = Vector((TX * 0.80, 0.0, NECK_Z - 0.065 * H))   # shoulder joint, just under the neck
    far = [c for c in arm if abs(c.x) > tip_x - 0.05 * H]
    TIP = Vector((tip_x, sum(c.y for c in far) / len(far), sum(c.z for c in far) / len(far)))
    ELBOW = SH.lerp(TIP, 0.42)
    WRIST = SH.lerp(TIP, 0.74)
    HIP_Z = 0.29 * H
    ANKLE_Z = 0.075 * H
    KNEE_Z = (HIP_Z + ANKLE_Z) / 2
    legs = [c for c in V if 0.10 * H < c.z < 0.24 * H]
    LEG_X = sum(abs(c.x) for c in legs) / len(legs)
    toe_y = min(c.y for c in V if c.z < 0.06 * H)
    print("HD measures", "TX %.3f neck %.3f shoulder %s tip %s legx %.3f" % (TX, NECK_Z, tuple(round(a, 3) for a in SH), tuple(round(a, 3) for a in TIP), LEG_X))
    HEAD_TOP = max(c.z for c in V)
    crown = [c for c in V if c.z > NECK_Z + 0.62 * (HEAD_TOP - NECK_Z)]
    CROWN_W = max(abs(c.x) for c in crown)
    CROWN_Y = (max(c.y for c in crown) + min(c.y for c in crown)) / 2
    HAT_K = CROWN_W / 0.394          # built hats were made for a hair cap 0.394 wide (half), its top at z 1.553


    def to_hd(p):
        """Moves a point from the built character's (grown) head space onto the HD head."""
        return Vector((p[0] * HAT_K, (p[1] - 0.0385) * HAT_K + CROWN_Y, (p[2] - 1.553) * HAT_K + HEAD_TOP - 0.01))

    # measured on the full scan; only now reduced for phones
    tris = sum(len(p.vertices) - 2 for p in hd.data.polygons)
    if tris > HD_TRIS:
        dec = hd.modifiers.new("Decimate", 'DECIMATE')
        dec.ratio = HD_TRIS / tris
        with bpy.context.temp_override(object=hd, active_object=hd, selected_objects=[hd]):
            bpy.ops.object.modifier_apply(modifier="Decimate")
        print("HD decimated", tris, "->", sum(len(p.vertices) - 2 for p in hd.data.polygons))


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
    "Hat": ((0, 0.033, 1.41), (0, 0.033, 1.67), "Head"),       # hats ride their own bone so they can lag and bounce
    "Arm.L": ((0.235, 0, 0.775), (0.275, 0, 0.66), "Spine"),
    "Arm.R": ((-0.235, 0, 0.775), (-0.275, 0, 0.66), "Spine"),
    "ForeArm.L": ((0.275, 0, 0.66), (0.315, 0, 0.47), "Arm.L"),
    "ForeArm.R": ((-0.275, 0, 0.66), (-0.315, 0, 0.47), "Arm.R"),
    "Leg.L": ((0.10, 0, 0.42), (0.10, 0, 0.26), "Hips"),
    "Leg.R": ((-0.10, 0, 0.42), (-0.10, 0, 0.26), "Hips"),
    "Shin.L": ((0.10, 0, 0.26), (0.10, 0, 0.10), "Leg.L"),
    "Shin.R": ((-0.10, 0, 0.26), (-0.10, 0, 0.10), "Leg.R"),
    "Foot.L": ((0.10, 0, 0.10), (0.10, -0.14, 0.03), "Shin.L"),
    "Foot.R": ((-0.10, 0, 0.10), (-0.10, -0.14, 0.03), "Shin.R"),
}
THIGH = 0.16  # thigh and shin are the same length, which keeps crouches simple (see crouch())
if HD:
    def mirror(p):
        return (-p[0], p[1], p[2])
    sh, el, wr = tuple(SH), tuple(ELBOW), tuple(WRIST)
    spine_top = NECK_Z - 0.02 * H
    BONES = {
        "Hips": ((0, 0, HIP_Z), (0, 0, HIP_Z + 0.08 * H), None),
        "Spine": ((0, 0, HIP_Z + 0.08 * H), (0, 0, spine_top), "Hips"),
        "Head": ((0, 0, NECK_Z), (0, 0, NECK_Z + 0.25 * H), "Spine"),
        "Hat": (tuple(to_hd((0, 0.033, 1.41))), tuple(to_hd((0, 0.033, 1.67))), "Head"),
        "Arm.L": (sh, el, "Spine"), "Arm.R": (mirror(sh), mirror(el), "Spine"),
        "ForeArm.L": (el, wr, "Arm.L"), "ForeArm.R": (mirror(el), mirror(wr), "Arm.R"),
        "Leg.L": ((LEG_X, 0, HIP_Z), (LEG_X, 0, KNEE_Z), "Hips"), "Leg.R": ((-LEG_X, 0, HIP_Z), (-LEG_X, 0, KNEE_Z), "Hips"),
        "Shin.L": ((LEG_X, 0, KNEE_Z), (LEG_X, 0, ANKLE_Z), "Leg.L"), "Shin.R": ((-LEG_X, 0, KNEE_Z), (-LEG_X, 0, ANKLE_Z), "Leg.R"),
        "Foot.L": ((LEG_X, 0, ANKLE_Z), (LEG_X, toe_y, 0.02 * H), "Shin.L"), "Foot.R": ((-LEG_X, 0, ANKLE_Z), (-LEG_X, toe_y, 0.02 * H), "Shin.R"),
    }
    THIGH = HIP_Z - KNEE_Z
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


def grow(ob):
    for v in ob.data.vertices:
        v.co = NECK + (v.co - NECK) * HEAD_K
    return ob


def attach(ob, bone):
    """Rigid attachment: parent the object to a bone, keeping its world placement."""
    if bone in ("Head", "Hat"):
        grow(ob)
        if HD:
            for v in ob.data.vertices:
                v.co = to_hd(v.co)
    world = ob.matrix_world.copy()
    ob.parent = rig
    ob.parent_type = 'BONE'
    ob.parent_bone = bone
    bpy.context.view_layer.update()
    ob.matrix_world = world
    return ob


if not HD:
    # ---------------------------------------------------------------- body (one skinned mesh)
    pieces = []

    p = Part()  # pelvis
    p.ellipsoid((0, 0, 0.45), (0.205, 0.165, 0.13), "Lower")
    pieces.append(skin(p.build("pelvis"), "Hips"))

    p = Part()  # torso: a soft bean, wider at the chest
    p.ellipsoid((0, 0, 0.655), (0.225, 0.17, 0.215), "Jacket", seg=24, rings=12)
    p.ellipsoid((0, 0.005, 0.80), (0.235, 0.15, 0.085), "Jacket")           # shoulders
    p.cone((0, 0, 0.515), 0.245, 0.205, 0.15, "Jacket", seg=24, bevel=0.04)          # flared coat hem
    for side in (1, -1):                                                              # two collar wings
        p.ellipsoid((side * 0.062, -0.128, 0.80), (0.075, 0.045, 0.05), "White", seg=12, rings=8, rot=(0, side * 24, 0))
    for i, z in enumerate((0.71, 0.625, 0.54)):                                       # brass buttons
        p.ellipsoid((0, -0.176 + abs(i - 1) * 0.006, z), (0.024, 0.014, 0.024), "Gold", seg=10, rings=6)
    pieces.append(skin(p.build("torso"), "Spine"))

    for side, suffix in ((1, "L"), (-1, "R")):
        p = Part()  # upper arm
        p.capsule((side * 0.245, 0, 0.775), (side * 0.275, 0, 0.66), 0.080, "Jacket", radius1=0.078)
        pieces.append(skin(p.build("arm" + suffix), "Arm." + suffix))

        p = Part()  # forearm and hand
        p.capsule((side * 0.275, 0, 0.66), (side * 0.305, 0, 0.555), 0.078, "Jacket", radius1=0.080)
        p.torus((side * 0.307, 0, 0.535), 0.066, 0.026, "White", seg=14, ring=6, rot=(0, side * 14, 0))   # cuff
        p.ellipsoid((side * 0.318, -0.005, 0.470), (0.074, 0.074, 0.078), "Skin", seg=12, rings=8)
        pieces.append(skin(p.build("forearm" + suffix), "ForeArm." + suffix))

        p = Part()  # thigh
        p.capsule((side * 0.10, 0, 0.40), (side * 0.10, 0, 0.26), 0.088, "Legs", radius1=0.084)
        pieces.append(skin(p.build("leg" + suffix), "Leg." + suffix))

        p = Part()  # shin
        p.capsule((side * 0.10, 0, 0.26), (side * 0.10, 0, 0.13), 0.084, "Legs", radius1=0.078)
        pieces.append(skin(p.build("shin" + suffix), "Shin." + suffix))

        p = Part()  # shoe
        p.ellipsoid((side * 0.10, -0.05, 0.068), (0.104, 0.165, 0.068), "Shoes", seg=14, rings=8)
        p.ellipsoid((side * 0.10, -0.05, 0.024), (0.110, 0.172, 0.024), "White", seg=14, rings=6)  # sole
        pieces.append(skin(p.build("foot" + suffix), "Foot." + suffix))

    p = Part()  # head
    p.ellipsoid(HEAD_C, HEAD_S, "Skin", seg=28, rings=14)
    p.ellipsoid((0, 0, 0.86), (0.075, 0.07, 0.06), "Skin", seg=12, rings=6)               # neck
    for side in (1, -1):
        p.ellipsoid((side * 0.335, 0.01, 1.07), (0.045, 0.05, 0.07), "Skin", seg=10, rings=8)  # ears
    p.ellipsoid((0, face_y(0, 1.02, 0.005), 1.02), (0.032, 0.03, 0.028), "Skin", seg=10, rings=8)  # nose
    pieces.append(skin(grow(p.build("head")), "Head"))

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


if not HD:
    # ---------------------------------------------------------------- face (rigid on the head)
    EYE_X, EYE_Z = 0.132, 1.05

    p = Part()
    for side in (1, -1):
        y = face_y(side * EYE_X, EYE_Z)
        p.ellipsoid((side * EYE_X, y + 0.012, EYE_Z), (0.080, 0.03, 0.100), "EyeDark", seg=18, rings=10, rot=(0, 0, side * 14))
        p.ellipsoid((side * EYE_X - 0.024, y - 0.013, EYE_Z + 0.038), (0.030, 0.012, 0.035), "White", seg=10, rings=6)
        p.ellipsoid((side * EYE_X + 0.026, y - 0.014, EYE_Z - 0.036), (0.015, 0.008, 0.016), "White", seg=8, rings=6)
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
        p.ellipsoid((side * 0.222, face_y(side * 0.222, 0.955) + 0.012, 0.955), (0.068, 0.02, 0.05), "Blush", seg=12, rings=6, rot=(0, 0, side * 38))
    attach(p.build("Cheeks"), "Head")

    p = Part()  # smile
    p.torus((0, face_y(0, 0.955, 0.002), 1.01), 0.075, 0.013, "Mouth", seg=12, ring=6, arc=(-128, -52), rot=(98, 0, 0))
    attach(p.build("Mouth"), "Head")

    p = Part()  # open, laughing mouth (cheering, being carried, yawning)
    p.ellipsoid((0, face_y(0, 0.955) + 0.014, 0.955), (0.072, 0.022, 0.052), "MouthIn", seg=16, rings=8)
    p.ellipsoid((0, face_y(0, 0.93) + 0.006, 0.928), (0.042, 0.016, 0.022), "Mouth", seg=12, rings=6)   # tongue
    attach(p.build("MouthOpen"), "Head")

    p = Part()  # frown (refusing a move, level lost)
    p.torus((0, face_y(0, 0.95, 0.002), 0.885), 0.07, 0.013, "Mouth", seg=12, ring=6, arc=(56, 124), rot=(82, 0, 0))
    attach(p.build("MouthSad"), "Head")

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


# ---------------------------------------------------------------- HD body: weights, colour regions, texture
def hd_weights(c):
    """Bone weights for one vertex of the HD body, from its position (the model has no rig of its own)."""
    w = {}
    side = "L" if c.x > 0 else "R"
    ax = abs(c.x)
    if ax > TX * 0.98 and 0.20 * H < c.z < NECK_Z - 0.01 * H and ax > LEG_X + 0.09 * H:
        d = TIP - SH
        t = ((Vector((ax, c.y, c.z)) - SH).dot(d)) / d.length_squared
        e = smooth01(0.36, 0.48, t)
        s_ = smooth01(-0.02, 0.10, t)
        w["Arm." + side] = (1 - e) * s_
        w["ForeArm." + side] = e
        w["Spine"] = 1 - s_
    elif c.z > NECK_Z - 0.03 * H:
        h = smooth01(NECK_Z - 0.03 * H, NECK_Z + 0.02 * H, c.z)
        w["Head"] = h
        w["Spine"] = 1 - h
    elif c.z > HIP_Z - 0.01 * H:
        h = smooth01(HIP_Z - 0.01 * H, HIP_Z + 0.06 * H, c.z)
        w["Spine"] = h
        w["Hips"] = 1 - h
    else:
        # far from the leg's own axis (a skirt, a coat hem) or between the legs: rides on the hips
        off = abs(ax - LEG_X)
        loose = smooth01(0.06 * H, 0.09 * H, off) if c.z > ANKLE_Z + 0.03 * H else 0.0
        top = max(smooth01(HIP_Z - 0.05 * H, HIP_Z, c.z), loose)
        knee = smooth01(KNEE_Z - 0.02 * H, KNEE_Z + 0.02 * H, c.z)
        ankle = smooth01(ANKLE_Z - 0.015 * H, ANKLE_Z + 0.015 * H, c.z)
        w["Hips"] = top
        rest = 1 - top
        w["Leg." + side] = rest * knee
        w["Shin." + side] = rest * (1 - knee) * ankle
        w["Foot." + side] = rest * (1 - knee) * (1 - ankle)
    return {b: x for b, x in w.items() if x > 0.001}


def hd_body():
    """Skins the HD mesh, sorts its faces into recolourable regions and writes the region-normalised texture."""
    import numpy as np, colorsys
    ob = hd
    ob.name = ob.data.name = "Body"
    me = ob.data
    groups = {b: ob.vertex_groups.new(name=b) for b in BONES}
    for v in me.vertices:
        for b, x in hd_weights(v.co).items():
            groups[b].add([v.index], x, 'REPLACE')
    img = next(i for i in bpy.data.images if i.size[0] > 0)
    W, Hh = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape(Hh, W, 4)
    uv = me.uv_layers.active.data
    regions = ("Jacket", "Hair", "Skin", "Lower", "Detail")
    region_of = []
    tris = []
    for poly in me.polygons:
        loops = list(poly.loop_indices)
        uvs = [uv[l].uv.copy() for l in loops]
        cu = sum((u for u in uvs), Vector((0, 0))) / len(uvs)
        r, g, b, _ = px[min(Hh - 1, int(cu.y * Hh)), min(W - 1, int(cu.x * W))]
        hh, ss, vv = colorsys.rgb_to_hsv(r, g, b)
        z = poly.center.z
        upper_head = z > NECK_Z + 0.5 * (HEAD_TOP - NECK_Z) or (z > NECK_Z and poly.center.y > CROWN_Y + 0.04 * H)
        if (hh < 0.05 or hh > 0.92) and ss > 0.35 and vv > 0.15 and z < NECK_Z and z > HIP_Z - 0.06 * H:
            reg = "Jacket"
        elif 0.5 < hh < 0.75 and z < HIP_Z + 0.02 * H and z > ANKLE_Z:
            reg = "Lower"
        elif 0.02 < hh < 0.13 and upper_head and vv < 0.62 and ss > 0.35:
            reg = "Hair"
        elif 0.0 <= hh < 0.12 and ss < 0.55 and vv > 0.55 and z > HIP_Z:
            reg = "Skin"
        else:
            reg = "Detail"
        region_of.append(regions.index(reg))
        for i in range(1, len(uvs) - 1):
            tris.append((regions.index(reg), uvs[0], uvs[i], uvs[i + 1]))
    # region mask over the texture (rasterised UV triangles), then normalise each tinted region
    mask = np.full((Hh, W), 5, dtype=np.int8)      # 5 = no face here (padding between UV islands)
    for reg, a, b, c in tris:
        P = np.array([[a.x * W, a.y * Hh], [b.x * W, b.y * Hh], [c.x * W, c.y * Hh]])
        x0, y0 = np.floor(P.min(0)).astype(int) - 1
        x1, y1 = np.ceil(P.max(0)).astype(int) + 1
        x0, y0 = max(x0, 0), max(y0, 0)
        x1, y1 = min(x1, W - 1), min(y1, Hh - 1)
        if x1 < x0 or y1 < y0:
            continue
        gx, gy = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
        d = (P[1, 1] - P[2, 1]) * (P[0, 0] - P[2, 0]) + (P[2, 0] - P[1, 0]) * (P[0, 1] - P[2, 1])
        if abs(d) < 1e-9:
            continue
        l1 = ((P[1, 1] - P[2, 1]) * (gx - P[2, 0]) + (P[2, 0] - P[1, 0]) * (gy - P[2, 1])) / d
        l2 = ((P[2, 1] - P[0, 1]) * (gx - P[2, 0]) + (P[0, 0] - P[2, 0]) * (gy - P[2, 1])) / d
        inside = (l1 >= -0.01) & (l2 >= -0.01) & (1 - l1 - l2 >= -0.01)
        sub = mask[y0:y1 + 1, x0:x1 + 1]
        sub[inside] = reg
    # grow the tinted regions a few texels into the empty padding, so filtering at island edges
    # never pulls in unnormalised colour (which shows as bright slivers once tinted)
    for _ in range(4):
        grown = mask.copy()
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            src = np.roll(mask, (dy, dx), axis=(0, 1))
            fill = (grown == 5) & (src < 4)
            grown[fill] = src[fill]
        mask = grown
    rgb = px[..., :3]
    lum = rgb @ np.array([0.299, 0.587, 0.114], dtype=np.float32)
    out = px.copy()
    for i, name in enumerate(regions[:4]):
        m = mask == i
        if not m.any():
            continue
        core = m  # statistics over every texel of the region
        if name == "Skin":
            mean = rgb[core].mean(0)
            out[..., :3][m] = np.clip(rgb[m] / mean * 0.5, 0, 1)            # keeps blush and shading hues
        else:
            mean = lum[m].mean()
            out[..., :3][m] = np.clip((lum[m] / mean * 0.5)[:, None], 0, 1)  # shading only, the game tints it
        print("region", name, int(m.sum()), "texels")
    tex = bpy.data.images.new("PassengerHD_albedo", W, Hh, alpha=True)
    tex.pixels = out.ravel()
    tex.filepath_raw = OUT[:-4] + "_albedo.png"
    tex.file_format = 'PNG'
    tex.save()
    for poly in me.polygons:
        poly.use_smooth = True
    me.materials.clear()
    for name in regions:
        me.materials.append(MATS[name] if name in MATS else bpy.data.materials.new(name))
    for poly, r in zip(me.polygons, region_of):
        poly.material_index = r
    ob.parent = rig
    mod = ob.modifiers.new("Armature", 'ARMATURE')
    mod.object = rig
    return ob


# Where each HD variant's painted eyes and mouth are, read off a front render (x and z in model units, after
# scaling, rest pose with no head tilt): ((left eye x, z), (right eye x, z)), eye half-size (x, z), mouth centre
# (x, z), mouth half-size.
# Picked by hand: detecting them from the texture mixed them up with eyebrows, dark hair and red noses.
FACE_MARKS = {
    0: (((-0.163, 1.049), (0.150, 1.055)), (0.062, 0.062), (0.000, 0.911), (0.056, 0.019)),
    1: (((-0.106, 0.994), (0.119, 1.000)), (0.056, 0.056), (0.006, 0.919), (0.031, 0.013)),
    2: (((-0.113, 0.999), (0.119, 0.999)), (0.060, 0.060), (0.000, 0.867), (0.062, 0.031)),
    3: (((-0.131, 1.137), (0.125, 1.137)), (0.052, 0.060), (0.000, 0.949), (0.050, 0.019)),
    4: (((-0.113, 1.093), (0.119, 1.093)), (0.060, 0.060), (0.006, 0.943), (0.081, 0.050)),
    5: (((-0.094, 1.120), (0.106, 1.120)), (0.044, 0.044), (0.000, 1.027), (0.069, 0.015)),
}


def hd_face():
    """The HD faces are painted, so expressions are added on top: find the painted eyes and mouth, then lay
    skin-coloured lids with closed-eye arcs (blinks, happy squint), an open mouth and a frown over them.
    The game shows these by name exactly as it does on the built character; the painted face is the default."""
    import numpy as np, colorsys
    from mathutils.bvhtree import BVHTree
    me = hd.data
    src = next(i for i in bpy.data.images if i.size[0] > 0 and not i.name.startswith("PassengerHD"))
    W, Hh = src.size
    px = np.array(src.pixels[:], dtype=np.float32).reshape(Hh, W, 4)
    uv = me.uv_layers.active.data
    head = [v.co for v in me.vertices if v.co.z > NECK_Z]
    front_y = min(c.y for c in head)
    depth = max(c.y for c in head) - front_y
    marks = FACE_MARKS.get(HD_VARIANT)
    if marks is None:
        print("HD face: no face marks for variant %d, no expressions" % HD_VARIANT)
        return
    (e1, e2), (erx, erz), (mx, mz), (mrx, mrz) = marks
    E = {-1: (Vector((e1[0], front_y, e1[1])), erx * 1.15, erz * 1.15),
         1: (Vector((e2[0], front_y, e2[1])), erx * 1.15, erz * 1.15)}
    M = (Vector((mx, front_y, mz)), mrx, mrz)
    bvh = BVHTree.FromObject(hd, bpy.context.evaluated_depsgraph_get())
    print("HD face eyes %s %s mouth %s" % (tuple(round(a, 3) for a in E[1][0]), tuple(round(a, 3) for a in E[-1][0]), tuple(round(a, 3) for a in M[0])))

    def on_face(x, z):
        """Point and normal of the face surface straight in front of (x, z)."""
        hit, n, _, _ = bvh.ray_cast(Vector((x, front_y - 0.5, z)), Vector((0, 1, 0)), 2.0)
        if hit is None:
            return Vector((x, front_y, z)), Vector((0, -1, 0))
        return hit, n.normalized()

    def place(part, name, x, z, lift):
        ob = part.build(name)
        at, n = on_face(x, z)
        turn = Vector((0, -1, 0)).rotation_difference(n).to_matrix().to_4x4()
        ob.matrix_world = Matrix.Translation(at + n * lift) @ turn
        world = ob.matrix_world.copy()
        ob.parent = rig
        ob.parent_type = 'BONE'
        ob.parent_bone = "Head"
        bpy.context.view_layer.update()
        ob.matrix_world = world

    # closed eyes: a skin lid over each painted eye with a happy upward arc drawn on it
    for side in (1, -1):
        c, rx, rz = E[side]
        p = Part()
        p.ellipsoid((0, 0, 0), (rx, 0.012, rz), "SkinFlat", seg=18, rings=10)
        p.torus((0, -0.011, -rz * 0.25), rx * 0.75, max(0.008, rz * 0.13), "EyeDark", seg=12, ring=6, arc=(25, 155), rot=(90, 0, 0))
        place(p, "EyesClosed_%s" % ("L" if side > 0 else "R"), c.x, c.z, 0.004)
    mc, mrx, mrz = M
    p = Part()   # open, laughing mouth
    p.ellipsoid((0, 0, 0), (max(mrx, 0.045), 0.012, max(mrz, 0.035) * 1.1), "MouthIn", seg=16, rings=8)
    p.ellipsoid((0, -0.006, -max(mrz, 0.035) * 0.45), (max(mrx, 0.045) * 0.55, 0.008, max(mrz, 0.035) * 0.4), "Mouth", seg=12, rings=6)
    place(p, "MouthOpen", mc.x, mc.z, 0.004)
    p = Part()   # frown over a skin patch that hides the painted smile
    p.ellipsoid((0, 0, 0), (max(mrx, 0.035) * 1.2 + 0.01, 0.012, max(mrz, 0.02) * 1.4 + 0.014), "SkinFlat", seg=16, rings=8)
    fr = max(mrx, 0.035)
    p.torus((0, -0.015, -fr * 0.55), fr * 0.8, 0.012, "MouthIn", seg=12, ring=6, arc=(50, 130), rot=(90, 0, 0))
    place(p, "MouthSad", mc.x, mc.z, 0.004)
    # the game looks for single objects: join the two lids into EyesClosed
    lids = [bpy.data.objects["EyesClosed_L"], bpy.data.objects["EyesClosed_R"]]
    bpy.ops.object.select_all(action='DESELECT')
    for o in lids:
        o.select_set(True)
    bpy.context.view_layer.objects.active = lids[0]
    bpy.ops.object.join()
    lids[0].name = lids[0].data.name = "EyesClosed"


if HD:
    body = hd_body()
    hd_face()


# ---------------------------------------------------------------- line hats (one silhouette per line)
p = Part()  # 0 red: baseball cap
p.ellipsoid((0, 0.03, 1.37), (0.375, 0.365, 0.215), "Hat", seg=24, rings=12)
p.ellipsoid((0, -0.33, 1.375), (0.22, 0.14, 0.028), "Hat", seg=18, rings=6, rot=(14, 0, 0))
p.ellipsoid((0, 0.03, 1.585), (0.045, 0.045, 0.025), "White", seg=10, rings=6)
p.outline(INK * 0.8)
p.ellipsoid((0, -0.318, 1.455), (0.075, 0.03, 0.06), "White", seg=14, rings=8, rot=(-18, 0, 0))   # front badge
p.ellipsoid((0, -0.340, 1.455), (0.040, 0.02, 0.032), "Gold", seg=10, rings=6, rot=(-18, 0, 0))
attach(p.build("Hat0"), "Hat")

p = Part()  # 1 blue: square conductor hat
p.box((0, 0.03, 1.56), (0.50, 0.50, 0.24), "Hat", bevel=0.05)
p.box((0, 0.03, 1.455), (0.53, 0.53, 0.06), "White", bevel=0.025)
p.outline(INK * 0.8)
p.ellipsoid((0, -0.228, 1.57), (0.075, 0.02, 0.075), "Gold", seg=14, rings=8)                     # cap badge
p.box((0, -0.262, 1.428), (0.34, 0.11, 0.022), "EyeDark", bevel=0.01, rot=(12, 0, 0))             # shiny peak
attach(p.build("Hat1"), "Hat")

p = Part()  # 2 green: top hat
p.cone((0, 0.05, 1.455), 0.38, 0.38, 0.04, "Hat", bevel=0.015)
p.cone((0, 0.03, 1.63), 0.26, 0.29, 0.32, "Hat", bevel=0.03)
p.cone((0, 0.03, 1.51), 0.272, 0.272, 0.055, "White", bevel=0.01)
p.outline(INK * 0.8)
p.box((0, -0.245, 1.51), (0.085, 0.03, 0.07), "Gold", bevel=0.012)                                # buckle
attach(p.build("Hat2"), "Hat")

p = Part()  # 3 yellow: party cone with a pompom
p.cone((0, 0.03, 1.67), 0.24, 0.03, 0.46, "Hat", bevel=0.0)
p.torus((0, 0.03, 1.455), 0.235, 0.035, "White", seg=20, ring=8)
p.ellipsoid((0, 0.03, 1.92), (0.075, 0.075, 0.075), "White", seg=12, rings=8)
p.outline(INK * 0.8)
for row, (z, n) in enumerate(((1.54, 6), (1.66, 5), (1.77, 4))):                                  # polka dots
    r = 0.24 - 0.21 * (z - 1.44) / 0.46
    for i in range(n):
        a = 2 * math.pi * (i + row * 0.5) / n
        p.ellipsoid((math.sin(a) * r, 0.03 - math.cos(a) * r, z), (0.034, 0.034, 0.034), "White", seg=8, rings=6)
attach(p.build("Hat3"), "Hat")

p = Part()  # 4 purple: round ears on a band
p.torus((0, 0.03, 1.19), 0.37, 0.035, "Hat", seg=24, ring=8, arc=(-5, 185), rot=(90, 0, 0))
for side in (1, -1):
    p.ellipsoid((side * 0.25, 0.03, 1.56), (0.16, 0.075, 0.16), "Hat", seg=18, rings=10)
    p.ellipsoid((side * 0.25, -0.03, 1.56), (0.09, 0.03, 0.09), "White", seg=12, rings=6)
p.outline(INK * 0.8)
attach(p.build("Hat4"), "Hat")

p = Part()  # 5 orange: wide sun hat
p.cone((0, 0.07, 1.44), 0.45, 0.44, 0.035, "Hat", seg=28, bevel=0.014, rot=(-8, 0, 0))
p.ellipsoid((0, 0.03, 1.44), (0.33, 0.33, 0.20), "Hat", seg=24, rings=12)
p.cone((0, 0.03, 1.475), 0.325, 0.31, 0.05, "White", bevel=0.01)
p.outline(INK * 0.8)
for side in (1, -1):                                                                              # ribbon bow
    p.ellipsoid((side * 0.07, -0.325, 1.48), (0.07, 0.03, 0.045), "White", seg=12, rings=6, rot=(0, side * 20, 0))
p.ellipsoid((0, -0.335, 1.48), (0.03, 0.03, 0.03), "Gold", seg=8, rings=6)
attach(p.build("Hat5"), "Hat")

p = Part()  # mystery passenger: grey cap with a floating question mark
p.ellipsoid((0, 0.03, 1.37), (0.38, 0.37, 0.22), "Grey", seg=24, rings=12)
p.outline(INK * 0.8)
p.text("?", (0, -0.02, 1.80), 0.46, 0.035, "White")
attach(p.build("Mystery"), "Hat")

p = Part()  # VIP halo, studded so that its spin shows
p.torus((0, 0.03, 2.02), 0.24, 0.035, "Gold", seg=28, ring=8)
for i in range(5):
    a = 2 * math.pi * i / 5
    p.ellipsoid((math.sin(a) * 0.24, 0.03 - math.cos(a) * 0.24, 2.02), (0.05, 0.05, 0.05), "White", seg=10, rings=6)
attach(p.build("Halo"), "Hat")

p = Part()  # skirt
p.cone((0, 0, 0.385), 0.31, 0.19, 0.27, "Lower", seg=24, bevel=0.03)
p.outline(INK * 0.8)
if not HD:
    attach(p.build("Skirt"), "Hips")


# ---------------------------------------------------------------- HD: lower the arms into the rest pose
if HD:
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode='POSE')
    for side, sx in (("L", 1), ("R", -1)):
        pb = rig.pose.bones["Arm." + side]
        b = arm_data.bones["Arm." + side]
        cur = (b.tail_local - b.head_local).normalized()
        want = Vector((sx * 0.33, 0.0, -0.94)).normalized()       # how the built character's arms hang
        r = cur.rotation_difference(want).to_matrix()
        m = b.matrix_local.to_3x3()
        pb.rotation_mode = 'QUATERNION'
        pb.rotation_quaternion = (m.inverted() @ r @ m).to_quaternion()
    bpy.ops.object.mode_set(mode='OBJECT')
    bpy.context.view_layer.update()
    with bpy.context.temp_override(object=body, active_object=body, selected_objects=[body]):
        bpy.ops.object.modifier_apply(modifier="Armature")
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode='POSE')
    bpy.ops.pose.armature_apply(selected=False)
    bpy.ops.object.mode_set(mode='OBJECT')
    mod = body.modifiers.new("Armature", 'ARMATURE')
    mod.object = rig


# ---------------------------------------------------------------- animation
rig.animation_data_create()
REST = {b.name: b.matrix_local.to_3x3() for b in arm_data.bones}
for pb in rig.pose.bones:
    pb.rotation_mode = 'QUATERNION'

HEAD_TILT = -20.0  # the game camera looks steeply down: heads tip back so faces show under the hats


def pose_quat(bone, rot):
    m = REST[bone]
    r = Euler([math.radians(a) for a in rot], 'XYZ').to_matrix()
    if bone == "Head":
        r = r @ Euler((math.radians(HEAD_TILT), 0, 0), 'XYZ').to_matrix()
    return (m.inverted() @ r @ m).to_quaternion()


def full(pose):
    """Fills in every bone: {bone: (rot, loc, scale)}. rot is a world-axis Euler in degrees relative to the
    parent (X pitch: negative swings a hanging limb forward; Y roll; Z turn). loc is a world-axis offset."""
    out = {}
    for bone in BONES:
        vals = tuple(pose.get(bone, ())) + (None,) * 3
        out[bone] = (tuple(vals[0] or (0, 0, 0)), tuple(vals[1] or (0, 0, 0)), tuple(vals[2] or (1, 1, 1)))
    return out


def apply(pose, frame=None):
    for bone, (rot, loc, scale) in full(pose).items():
        pb = rig.pose.bones[bone]
        pb.rotation_quaternion = pose_quat(bone, rot)
        pb.location = REST[bone].inverted() @ Vector(loc)
        pb.scale = scale
        if frame is not None:
            pb.keyframe_insert("rotation_quaternion", frame=frame)
            pb.keyframe_insert("location", frame=frame)
            pb.keyframe_insert("scale", frame=frame)


CLIPS = []
POSES = {}


def clip(name, length, pose):
    """pose(t) -> {bone: (rot, loc, scale)} with t in 0..1; sampled every frame so loops are exact."""
    action = bpy.data.actions.new(name)
    rig.animation_data.action = action
    for f in range(length + 1):
        apply(pose(f / length), f + 1)
    CLIPS.append((name, action, length))
    POSES[name] = pose
    rig.animation_data.action = None


EASE = {
    "smooth": lambda k: k * k * (3 - 2 * k),
    "out": lambda k: 1 - (1 - k) * (1 - k),
    "in": lambda k: k * k,
    "lin": lambda k: k,
}


def keyed(name, length, keys):
    """Pose-to-pose clip. keys: [(frame, pose, ease)], ease being how the pose is arrived at."""
    keys = [(k[0], full(k[1]), k[2] if len(k) > 2 else "smooth") for k in keys]

    def pose(t):
        f = t * length
        for (f0, a, _), (f1, b, ease) in zip(keys, keys[1:]):
            if f <= f1:
                k = EASE[ease](min(1.0, max(0.0, (f - f0) / (f1 - f0))))
                return {bone: tuple(tuple(x + (y - x) * k for x, y in zip(a[bone][i], b[bone][i])) for i in range(3))
                        for bone in BONES}
        return keys[-1][1]

    clip(name, length, pose)


def s(t, n=1.0, phase=0.0):
    return math.sin((t * n + phase) * 2 * math.pi)


def sym(bone, rot, loc=None, scale=None):
    """The same pose on both sides (mirrored)."""
    return {bone + ".L": (rot, loc, scale), bone + ".R": ((rot[0], -rot[1], -rot[2]), loc, scale)}


def mix(*parts):
    out = {}
    for part in parts:
        out.update(part)
    return out


def drop(a):
    """How far the hips sink in crouch(a)."""
    return -2 * THIGH * (1 - math.cos(math.radians(a)))


def crouch(a, rot=(0, 0, 0), z=0.0):
    """Knees bent by a degrees with the feet flat and planted under the hips."""
    return mix(sym("Leg", (-a, 0, 0)), sym("Shin", (2 * a, 0, 0)), sym("Foot", (-a, 0, 0)),
               {"Hips": (rot, (0, 0, drop(a) + z))})


def hat(z=0.0, squash=0.0, rot=(0, 0, 0)):
    """Hat follow-through: z lifts it off the head, squash flattens (+) or stretches (-) it."""
    return {"Hat": (rot, (0, 0, z), (1 + squash * 0.5, 1 - squash, 1 + squash * 0.5))}  # the bone's Y runs up the hat


NEUTRAL = mix(sym("Arm", (0, -3, 0)), sym("ForeArm", (-8, 0, 0)))


# ---- loops
def idle(t):
    breathe = s(t)
    return mix(
        crouch(2 + 1.5 * breathe),
        {"Spine": ((1.5 * breathe, 0, 0), None, (1 + 0.012 * breathe, 1, 1 + 0.012 * breathe)),
         "Head": ((-1.5 * breathe, 2.5 * s(t, 1, 0.2), 0),)},
        sym("Arm", (0, -3 - 2 * breathe, 0)), sym("ForeArm", (-8 - 3 * breathe, 0, 0)),
        hat(0.004 * s(t, 1, -0.15)))


def walk(t):
    sw = s(t)
    co = math.cos(t * 2 * math.pi)
    bend_l, bend_r = 12 + 44 * max(0.0, co), 12 + 44 * max(0.0, -co)   # the knee folds as the leg swings through
    return {
        "Hips": ((4, 0, 6 * sw), (0, 0, 0.026 * abs(s(t, 1, 0.25)) - 0.012)),
        "Spine": ((4, 0, -11 * sw),),
        "Head": ((-4, 3 * sw, 5 * sw),),
        "Hat": ((0, 0, 0), (0, 0, 0.012 * abs(s(t, 1, 0.12)))),
        "Arm.L": ((34 * sw, -6, 0),), "Arm.R": ((-34 * sw, 6, 0),),
        "ForeArm.L": ((-32 + 18 * sw, 0, 0),), "ForeArm.R": ((-32 - 18 * sw, 0, 0),),
        "Leg.L": ((-32 * sw, 0, 0),), "Leg.R": ((32 * sw, 0, 0),),
        "Shin.L": ((bend_l, 0, 0),), "Shin.R": ((bend_r, 0, 0),),
        "Foot.L": ((-bend_l * 0.5 + 14 * sw, 0, 0),), "Foot.R": ((-bend_r * 0.5 - 14 * sw, 0, 0),),
    }


def wave(t):
    w = s(t, 2)
    return mix(
        crouch(3 + 2 * abs(s(t))),
        {"Spine": ((-2, -4, 0),), "Head": ((-6, -7 + 2 * w, 0),),
         "Arm.L": ((-10, -112, 0),), "ForeArm.L": ((0, -42 + 30 * w, 0),),
         "Arm.R": ((0, 8, 0),), "ForeArm.R": ((-14, 0, 0),)},
        hat(0.0, 0.0, (0, -3 * w, 0)))


def picked(t):
    """Carried through the air: legs dangle and kick, arms flail, face turned up to the player."""
    k = s(t)
    return {
        "Hips": ((-4, 0, 7 * k), (0, 0, 0)),
        "Spine": ((-5, 0, -7 * k),),
        "Head": ((-9, 4 * k, 0),),
        "Hat": ((0, 0, 0), (0, 0, 0.012 + 0.008 * s(t, 2))),
        "Arm.L": ((-8, -72 + 14 * s(t, 2), 0),), "ForeArm.L": ((0, -38 + 24 * s(t, 2, 0.25), 0),),
        "Arm.R": ((-8, 72 + 14 * s(t, 2, 0.5), 0),), "ForeArm.R": ((0, 38 + 24 * s(t, 2, 0.75), 0),),
        "Leg.L": ((-14 + 24 * k, -5, 0),), "Leg.R": ((-14 - 24 * k, 5, 0),),
        "Shin.L": ((34 + 22 * s(t, 1, 0.25), 0, 0),), "Shin.R": ((34 - 22 * s(t, 1, 0.25), 0, 0),),
        "Foot.L": ((24, 0, 0),), "Foot.R": ((24, 0, 0),),
    }


def sad(t):
    """Slumped and sighing: the level is lost."""
    b = s(t)
    return mix(
        crouch(7 + b),
        {"Spine": ((15 + 2.5 * b, 0, 0),), "Head": ((13 + 2 * b, 0, 5 * s(t, 1, 0.3)),)},
        sym("Arm", (-9 - 2 * b, 2, 0)), sym("ForeArm", (-5, 0, 0)),
        hat(-0.004))


def vip(t):
    """A VIP waits with the chin up, a hand on the hip and an impatient foot."""
    b = s(t)
    return mix(
        crouch(1.5, rot=(0, 3, 0)),
        {"Spine": ((-6 + b, -2, 0), None, (1 + 0.012 * b, 1, 1 + 0.012 * b)), "Head": ((-9 - b, 0, 7),),
         "Arm.L": ((8, -44, 0),), "ForeArm.L": ((-20, 86, 0),),
         "Arm.R": ((0, 6 + b, 0),), "ForeArm.R": ((-14, 0, 0),),
         "Foot.R": ((-1.5 - 24 * max(0.0, s(t, 3)), 0, 0),)},
        hat(0.004 * b))


def sit(t):
    """On the bench: weight back on the hands, legs out in front and swinging in turn."""
    k = s(t)
    return {
        "Hips": ((-8, 0, 0), (0, 0, -2 * THIGH + 0.075)),
        "Spine": ((-5 + 1.5 * s(t, 2), 0, 0),),
        "Head": ((-4, 5 * s(t, 1, 0.2), 4 * k),),
        "Hat": ((0, 0, 0), (0, 0, 0.004 * s(t, 2))),
        "Arm.L": ((20, -16, 0),), "Arm.R": ((20, 16, 0),),
        "ForeArm.L": ((-6, 0, 0),), "ForeArm.R": ((-6, 0, 0),),
        "Leg.L": ((-76 + 5 * k, -6, 0),), "Leg.R": ((-76 - 5 * k, 6, 0),),
        "Shin.L": ((24 + 22 * k, 0, 0),), "Shin.R": ((24 - 22 * k, 0, 0),),
        "Foot.L": ((-12 + 10 * k, 0, 0),), "Foot.R": ((-12 - 10 * k, 0, 0),),
    }


def eager(t):
    """One more passenger and the train comes: bouncing on the toes with clenched fists."""
    up = abs(s(t))
    return mix(
        {"Hips": ((0, 0, 0), (0, 0, 0.035 * up)), "Spine": ((-3, 0, 3 * s(t)),), "Head": ((-8, 5 * s(t), 0),)},
        sym("Foot", (16 * up, 0, 0)), sym("Leg", (-3 * (1 - up), 0, 0)),
        sym("Arm", (10, -12, 0)), sym("ForeArm", (-84 + 12 * s(t, 2), 0, 0)),
        hat(0.012 * abs(s(t, 1, -0.08)), -0.04 * up))


def worry(t):
    """Running out of room or moves: knees knocking, hands up, glancing from side to side."""
    look = s(t)
    return mix(
        crouch(7 + 1.5 * s(t, 6)),
        {"Spine": ((8, 0, 4 * look),), "Head": ((5, 0, 26 * look),)},
        sym("Arm", (-34, -16, 0)), sym("ForeArm", (-100 + 6 * s(t, 6), 0, 0)),
        hat(0, 0, (0, 0, -7 * s(t, 1, -0.08))))


def lean(t):
    """Leaning out of a train window: folded forward at the hips, face turned up to the camera, one arm waving."""
    w = s(t, 2)
    return mix(
        {"Hips": ((36, 0, 0), (0, 0, 0)),
         "Spine": ((8, -4, 3 * s(t)),),
         "Head": ((-54, -6 + 3 * w, 4 * s(t)),),
         "Arm.L": ((-24, -120, 0),), "ForeArm.L": ((0, -40 + 32 * w, 0),),
         "Arm.R": ((-34, 24, 0),), "ForeArm.R": ((-34, 0, 0),)},
        hat(0.0, 0.0, (0, -3 * w, 0)))


clip("Idle", 60, idle)
clip("Vip", 60, vip)
clip("Eager", 24, eager)
clip("Worry", 44, worry)
clip("Walk", 20, walk)
clip("Wave", 24, wave)
clip("Picked", 20, picked)
clip("Sad", 60, sad)
clip("Sit", 40, sit)
clip("Lean", 24, lean)

# ---- jump: sampled by the game along the hop from one spot to the next (0 = take-off, 1 = touch-down)
TOUCHDOWN = mix(
    sym("Leg", (-16, 0, 0)), sym("Shin", (22, 0, 0)), sym("Foot", (-12, 0, 0)),
    {"Spine": ((2, 0, 0),), "Head": ((-4, 0, 0),)},
    sym("Arm", (0, -78, 0)), sym("ForeArm", (0, -18, 0)), hat(0.07, -0.08))
keyed("Jump", 10, [
    (0, mix(sym("Leg", (12, 0, 0)), sym("Shin", (6, 0, 0)), sym("Foot", (30, 0, 0)),
            {"Hips": ((-6, 0, 0),), "Spine": ((-8, 0, 0),), "Head": ((-8, 0, 0),)},
            sym("Arm", (38, -14, 0)), sym("ForeArm", (-12, 0, 0)), hat(-0.01, 0.10))),
    (5, mix(sym("Leg", (-62, -8, 0)), sym("Shin", (96, 0, 0)), sym("Foot", (-8, 0, 0)),
            {"Hips": ((6, 0, 0),), "Spine": ((8, 0, 0),), "Head": ((-10, 0, 0),)},
            sym("Arm", (-30, -122, 0)), sym("ForeArm", (0, -30, 0)), hat(0.045, -0.06)), "out"),
    (10, TOUCHDOWN, "in"),
])

# ---- land: squash into the floor, spring back up, settle
keyed("Land", 14, [
    (0, TOUCHDOWN),
    (2, mix(crouch(38), {"Spine": ((15, 0, 0), None, (1.06, 0.94, 1.06)), "Head": ((7, 0, 0),)},
            sym("Arm", (12, -36, 0)), sym("ForeArm", (-24, 0, 0)), hat(-0.035, 0.16)), "out"),
    (6, mix(crouch(0, z=0.016), sym("Foot", (9, 0, 0)), {"Spine": ((-5, 0, 0), None, (0.98, 1.03, 0.98)), "Head": ((-6, 0, 0),)},
            sym("Arm", (0, -22, 0)), sym("ForeArm", (-14, 0, 0)), hat(0.03, -0.10)), "smooth"),
    (10, mix(crouch(8), {"Spine": ((3, 0, 0),)}, NEUTRAL, hat(-0.008, 0.05))),
    (14, mix(crouch(2), NEUTRAL)),
])

# ---- cheer: two-footed jumps with the arms thrown up (loop)
CHEER0 = mix(crouch(28), {"Spine": ((9, 0, 0),), "Head": ((2, 0, 0),)},
             sym("Arm", (16, -10, 0)), sym("ForeArm", (-78, 0, 0)), hat(-0.012, 0.08))
keyed("Cheer", 24, [
    (0, CHEER0),
    (4, mix({"Hips": ((-4, 0, 0), (0, 0, 0.10)), "Spine": ((-8, 0, 0),), "Head": ((-13, 0, 0),)},
            sym("Foot", (32, 0, 0)), sym("Arm", (4, -128, 0)), sym("ForeArm", (0, -14, 0)), hat(0.0, -0.10)), "in"),
    (8, mix({"Hips": ((0, 0, 0), (0, 0, 0.27)), "Spine": ((-6, 0, 0),), "Head": ((-15, 0, 0),)},
            sym("Leg", (-28, -14, 0)), sym("Shin", (56, 0, 0)), sym("Foot", (10, 0, 0)),
            sym("Arm", (6, -136, 0)), sym("ForeArm", (0, -6, 0)), hat(0.05, -0.05)), "out"),
    (12, mix({"Hips": ((0, 0, 0), (0, 0, 0.09)), "Spine": ((-2, 0, 0),), "Head": ((-10, 0, 0),)},
             sym("Foot", (22, 0, 0)), sym("Arm", (4, -124, 0)), sym("ForeArm", (0, -18, 0)), hat(0.065, -0.08)), "in"),
    (15, mix(crouch(30), {"Spine": ((11, 0, 0), None, (1.05, 0.95, 1.05)), "Head": ((4, 0, 0),)},
             sym("Arm", (0, -74, 0)), sym("ForeArm", (-30, -30, 0)), hat(-0.03, 0.16)), "out"),
    (20, mix(crouch(4), {"Spine": ((-4, 0, 5),), "Head": ((-10, 6, 0),)},
             sym("Arm", (0, -112, 0)), sym("ForeArm", (0, -58, 0)), hat(0.02, -0.06))),
    (24, CHEER0),
])

# ---- refuse: "no, no" - leans back, hands up, shakes the head
HANDS_UP = mix(sym("Arm", (-42, -26, 0)), sym("ForeArm", (-84, 0, 0)))
keyed("Refuse", 18, [
    (0, mix(crouch(2), NEUTRAL)),
    (3, mix(crouch(6), {"Spine": ((-9, 0, 0),), "Head": ((5, 0, -30),)}, HANDS_UP, hat(0, 0, (0, 0, 10))), "out"),
    (7, mix(crouch(6), {"Spine": ((-9, 0, 0),), "Head": ((5, 0, 30),)}, HANDS_UP, hat(0, 0, (0, 0, -10)))),
    (11, mix(crouch(5), {"Spine": ((-7, 0, 0),), "Head": ((5, 0, -22),)}, HANDS_UP, hat(0, 0, (0, 0, 8)))),
    (14, mix(crouch(4), {"Spine": ((-3, 0, 0),), "Head": ((3, 0, 10),)},
             sym("Arm", (-20, -14, 0)), sym("ForeArm", (-40, 0, 0)), hat(0, 0, (0, 0, -4)))),
    (18, mix(crouch(2), NEUTRAL)),
])

# ---- reveal: a mystery passenger's hat flies off - a startled little jump
keyed("Reveal", 16, [
    (0, mix(crouch(2), NEUTRAL)),
    (3, mix(crouch(24), {"Spine": ((8, 0, 0),), "Head": ((4, 0, 0),)}, sym("Arm", (10, -12, 0)), sym("ForeArm", (-60, 0, 0)), hat(-0.01, 0.08)), "out"),
    (7, mix({"Hips": ((0, 0, 0), (0, 0, 0.15)), "Spine": ((-7, 0, 0),), "Head": ((-13, 0, 0),)}, sym("Foot", (26, 0, 0)),
            sym("Arm", (4, -128, 0)), sym("ForeArm", (0, -14, 0)), hat(0.05, -0.08)), "out"),
    (11, mix(crouch(22), {"Spine": ((9, 0, 0), None, (1.05, 0.95, 1.05)), "Head": ((3, 0, 0),)},
             sym("Arm", (0, -60, 0)), sym("ForeArm", (-20, -20, 0)), hat(-0.03, 0.14)), "in"),
    (16, mix(crouch(2), NEUTRAL)),
])

# ---- idle fidgets (one-shots the game plays now and then on waiting passengers)
keyed("Look", 48, [
    (0, mix(crouch(2), NEUTRAL)),
    (8, mix(crouch(3), {"Spine": ((0, 2, 11),), "Head": ((0, 5, 38),)}, NEUTRAL, hat(0, 0, (0, 0, -6)))),
    (19, mix(crouch(3), {"Spine": ((0, 2, 12),), "Head": ((-2, 5, 41),)}, NEUTRAL)),
    (28, mix(crouch(3), {"Spine": ((0, -2, -11),), "Head": ((0, -5, -38),)}, NEUTRAL, hat(0, 0, (0, 0, 6)))),
    (40, mix(crouch(3), {"Spine": ((0, -2, -12),), "Head": ((-2, -5, -41),)}, NEUTRAL)),
    (48, mix(crouch(2), NEUTRAL)),
])

AKIMBO = mix(sym("Arm", (8, -44, 0)), sym("ForeArm", (-20, 86, 0)))


def tap(frame, up):
    return (frame, mix(crouch(2, rot=(0, 3, 0)), {"Spine": ((2, -3, 0),), "Head": ((3, 7, 12),)}, AKIMBO,
                       {"Foot.R": ((-28 if up else -2, 0, 0),)}), "out" if up else "in")


keyed("Tap", 54, [
    (0, mix(crouch(2), NEUTRAL)),
    tap(9, False), tap(13, True), tap(16, False), tap(20, True), tap(23, False),
    tap(27, True), tap(30, False), tap(34, True), tap(37, False), tap(45, False),
    (54, mix(crouch(2), NEUTRAL)),
])

STRETCH = mix({"Hips": ((0, 0, 0), (0, 0, 0.018)), "Spine": ((-11, 0, 0), None, (0.97, 1.04, 0.97)), "Head": ((-13, 0, 0),)},
              sym("Foot", (16, 0, 0)), sym("Arm", (8, -130, 0)), sym("ForeArm", (0, -22, 0)), hat(0.01))
keyed("Yawn", 56, [
    (0, mix(crouch(2), NEUTRAL)),
    (5, mix(crouch(7), {"Spine": ((5, 0, 0),)}, sym("Arm", (6, -12, 0)), sym("ForeArm", (-30, 0, 0)))),
    (15, STRETCH),
    (30, mix(STRETCH, {"Spine": ((-12, 4, 0), None, (0.97, 1.04, 0.97)), "Head": ((-15, 4, 0),)})),
    (41, mix(crouch(5), {"Spine": ((4, 0, 0),), "Head": ((2, 0, 0),)}, sym("Arm", (0, -58, 0)), sym("ForeArm", (-6, -10, 0)))),
    (50, mix(crouch(4), NEUTRAL)),
    (56, mix(crouch(2), NEUTRAL)),
])
apply({})

for name, action, length in CLIPS:
    track = rig.animation_data.nla_tracks.new()
    track.name = name
    strip = track.strips.new(name, 1, action)
    strip.name = name
    if hasattr(strip, "action_slot") and strip.action_slot is None and len(action.slots) > 0:
        strip.action_slot = action.slots[0]
    print("STRIP", name, strip.frame_start, strip.frame_end, getattr(strip, "action_slot", None))


# ---------------------------------------------------------------- preview renders
LINES = [(0.93, 0.33, 0.31), (0.25, 0.56, 0.93), (0.30, 0.76, 0.45), (0.98, 0.80, 0.25), (0.62, 0.42, 0.86), (0.97, 0.58, 0.24)]


def set_variant(hat, hair, female, closed=False, mystery=False, vip=False, mouth="Mouth"):
    show = {"Hat%d" % hat: not mystery, "Hair%d" % hair: True, "Skirt": female, "Lashes": female,
            "Moustache": False, "EyesOpen": not closed, "EyesClosed": closed, "Mystery": mystery, "Halo": vip,
            "Mouth": False, "MouthOpen": False, "MouthSad": False}
    show[mouth] = True
    for ob in scene.objects:
        if ob.type != 'MESH' or ob == body:
            continue
        hidden = ob.name[:3] in ("Hat", "Hai") or ob.name in show
        ob.hide_render = hidden and not show.get(ob.name, False)


def preview(path, pose_clip=None, t=0.25, hat=0, hair=0, female=False, cam_from=(1.6, -4.2, 3.0), **kw):
    set_variant(hat, hair, female, **kw)
    for n in ("Jacket", "Hat"):
        MATS[n].diffuse_color = (*LINES[hat], 1)
    apply(POSES[pose_clip](t) if pose_clip else {})
    cam = scene.camera
    cam.location = cam_from
    direction = Vector((0, 0, 0.95)) - cam.location
    cam.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def sheet(folder, name, frames=6, **kw):
    """One strip of evenly spaced frames of a clip, for judging the motion at a glance."""
    import numpy as np
    w, h = scene.render.resolution_x, scene.render.resolution_y
    strip = []
    for i in range(frames):
        path = os.path.join(folder, "_tmp.png")
        preview(path, name, t=i / (frames - 1) if frames > 1 else 0, **kw)
        img = bpy.data.images.load(path)
        strip.append(np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4))
        bpy.data.images.remove(img)
    out = bpy.data.images.new(name, w * frames, h)
    out.pixels = np.concatenate(strip, axis=1).ravel()
    out.filepath_raw = os.path.join(folder, "sheet_%s.png" % name)
    out.file_format = 'PNG'
    out.save()
    bpy.data.images.remove(out)
    os.remove(os.path.join(folder, "_tmp.png"))


def bake_ao(rays=24, reach=0.32):
    """Bakes soft ambient occlusion into a vertex colour ("AO", grey in R) on every mesh: armpits, the neck
    under the chin, between the legs, under hat brims and hair. Each part is occluded by itself and by the
    body; computed in the bind pose. The toy shader darkens with it, which is what makes the clay look."""
    from mathutils.bvhtree import BVHTree
    arm_data.pose_position = 'REST'
    bpy.context.view_layer.update()

    def world(ob):
        me = ob.data
        names = [m.name for m in me.materials]
        verts = [ob.matrix_world @ v.co for v in me.vertices]
        polys = [list(p.vertices) for p in me.polygons if not names[p.material_index].startswith("Outline")]
        return verts, polys

    bv, bp = world(body)
    body_tree = BVHTree.FromPolygons(bv, bp)
    # The hair cap is shared by every hair style, so it may shade the face and ears of any variant.
    hv, hp = world(bpy.data.objects["Hair0"])
    cap_tree = BVHTree.FromPolygons(hv, hp)
    dirs = []   # cosine-weighted hemisphere about +Z (golden-angle spiral)
    for i in range(rays):
        u = (i + 0.5) / rays
        a = i * 2.399963
        r = math.sqrt(u)
        dirs.append(Vector((r * math.cos(a), r * math.sin(a), math.sqrt(1 - u))))
    for ob in [o for o in scene.objects if o.type == 'MESH']:
        me = ob.data
        verts, polys = world(ob)
        trees = [body_tree, cap_tree] if ob == body else [body_tree, BVHTree.FromPolygons(verts, polys)]
        names = [m.name for m in me.materials]
        hull = set()
        for p in me.polygons:
            if names[p.material_index].startswith("Outline"):
                hull.update(p.vertices)
        to_world = ob.matrix_world.to_3x3().inverted().transposed()
        ao = [1.0] * len(me.vertices)
        for v in me.vertices:
            if v.index in hull:
                continue
            n = (to_world @ v.normal).normalized()
            turn = Vector((0, 0, 1)).rotation_difference(n)
            origin = verts[v.index] + n * 0.004
            blocked = 0.0
            for d in dirs:
                ray = turn @ d
                for tree in trees:
                    hit, _, _, dist = tree.ray_cast(origin, ray, reach)
                    if hit is not None:
                        blocked += 1.0 - 0.5 * dist / reach   # near occluders darken more
                        break
            ao[v.index] = max(0.25, 1.0 - blocked / rays)
        # Blur along the mesh so coarse parts get soft gradients instead of blotches.
        nbrs = [[] for _ in me.vertices]
        for e in me.edges:
            a, b = e.vertices
            if a in hull or b in hull:
                continue
            nbrs[a].append(b)
            nbrs[b].append(a)
        for _ in range(3):
            ao = [(ao[i] + sum(ao[j] for j in nbrs[i])) / (1 + len(nbrs[i])) if nbrs[i] else ao[i] for i in range(len(ao))]
        live = [ao[i] for i in range(len(ao)) if i not in hull]
        if live:
            print("AO", ob.name, len(live), "min %.2f mean %.2f" % (min(live), sum(live) / len(live)))
        attr = me.color_attributes.new("AO", 'BYTE_COLOR', 'CORNER')
        for li, loop in enumerate(me.loops):
            a = ao[loop.vertex_index]
            attr.data[li].color = (a, a, a, 1.0)
    arm_data.pose_position = 'POSE'
    bpy.context.view_layer.update()
    print("AO baked")


if PREVIEW:
    os.makedirs(PREVIEW, exist_ok=True)
    rig.animation_data_clear()  # otherwise the NLA strips override the poses set by hand below
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
    scene.render.resolution_x = 300
    scene.render.resolution_y = 380
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
    side = (3.0, -3.4, 1.9)
    sheet(PREVIEW, "Idle", 4, hat=1, hair=2, female=True)
    sheet(PREVIEW, "Walk", 6, hat=2, hair=0, cam_from=side)
    sheet(PREVIEW, "Wave", 5, hat=3, hair=4, female=True)
    sheet(PREVIEW, "Picked", 6, hat=4, hair=3, female=True, mouth="MouthOpen")
    sheet(PREVIEW, "Sad", 3, hat=5, hair=1, mouth="MouthSad", cam_from=side)
    sheet(PREVIEW, "Jump", 6, hat=0, hair=1, mouth="MouthOpen", cam_from=side)
    sheet(PREVIEW, "Land", 8, hat=0, hair=1, cam_from=side)
    sheet(PREVIEW, "Cheer", 9, hat=2, hair=3, female=True, closed=True, mouth="MouthOpen")
    sheet(PREVIEW, "Refuse", 7, hat=5, hair=0, mouth="MouthSad")
    sheet(PREVIEW, "Look", 7, hat=1, hair=1)
    sheet(PREVIEW, "Tap", 8, hat=3, hair=0)
    sheet(PREVIEW, "Yawn", 8, hat=4, hair=2, female=True, closed=True, mouth="MouthOpen")
    print("PREVIEWS", PREVIEW)
else:
    if not HD:
        bake_ao()
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.export_scene.fbx(
        filepath=OUT, use_selection=True, object_types={'ARMATURE', 'MESH'},
        add_leaf_bones=False, use_armature_deform_only=False, mesh_smooth_type='OFF',
        apply_scale_options='FBX_SCALE_ALL', use_mesh_modifiers=True,
        bake_anim=True, bake_anim_use_nla_strips=True, bake_anim_use_all_actions=False,
        bake_anim_use_all_bones=True, bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0,
        colors_type='LINEAR')
    print("EXPORTED", OUT, os.path.getsize(OUT))
