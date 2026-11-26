"""Builds the Metro Sort passenger: a rigged toy-style character with animation clips, exported as FBX.

Run:  Blender --background --python Tools/art/passenger.py -- [--preview DIR]
Output: Assets/MetroSort/Resources/Characters/Passenger.fbx

HD variants (sculpted bodies, face painted back from the concept picture by Tools/art/hdface.py):
    Blender --background --python Tools/art/passenger.py -- --hd MetroSort-Media/yolcu-trellis.glb --variant 0 \
        --art MetroSort-Media/Gorseller/yolcu-a-poz.png
    Blender --background --python Tools/art/passenger.py -- --hd MetroSort-Media/yolcu-varyant-N.glb --variant N \
        --art MetroSort-Media/Gorseller/yolcu-varyant-N.png          (N = 1 .. 5)
Premium passengers (Hunyuan3D-2 multi-view shape, painted by Tools/art/hdbake.py, face marks in the JSON next to
the GLB, heads already drawn big so no chibi growth):
    Blender --background --python Tools/art/passenger.py -- --hd MetroSort-Media/3D/yolcu-hd-1-painted.glb \
        --variant 6 --art MetroSort-Media/Gorseller/yolcu-hd-1.png --chibi 1.0      (add --bun for a hair bun)
Output: Assets/MetroSort/Resources/Characters/PassengerHD_N.fbx and PassengerHD_N_albedo.png

Blender space: Z up, the character faces -Y, 1 unit = 1 Unity unit. Parts are skinned rigidly (one bone
each). Optional parts (hair styles, hats, skirt ...) are separate objects parented to bones; the game shows
and hides them by name and recolours material slots by name.
"""
import bpy, bmesh, json, math, os, sys
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
# --art PICTURE: the concept picture the HD model was made from; its crisp face is painted back onto the head
# (Tools/art/hdface.py), since image-to-3D smears it.
HD_ART = argv[argv.index("--art") + 1] if "--art" in argv else None
if HD:
    OUT = os.path.join(ROOT, "Assets", "MetroSort", "Resources", "Characters", "PassengerHD_%d.fbx" % HD_VARIANT)
# Shapes painted by Tools/art/hdbake.py bring what was read off their pictures (face marks, neck height) in a
# JSON file next to the GLB; such "premium" models also get a filled mouth and a brighter jacket reference.
HD_JSON = {}
if HD and os.path.exists(os.path.splitext(HD_SRC)[0] + ".json"):
    HD_JSON = json.load(open(os.path.splitext(HD_SRC)[0] + ".json"))
PREMIUM = bool(HD_JSON)
# phones draw dozens of passengers at once: the scanned mesh is reduced to about this (--tris to change)
HD_TRIS = int(argv[argv.index("--tris") + 1]) if "--tris" in argv else 6000
# hdbake.py has already reduced its shapes to the budget, keeping the face dense; reducing again would not
HD_TRIS = max(HD_TRIS, HD_JSON.get("tris") or 0)
PROJ = None           # HD: how the concept art was laid on the face (hd_body), for the overlays (hd_face)
SKIN_UV = None        # HD: the plain skin patch in the body texture

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import kit
import hdface
from kit import Part, MATS

COLORS = {
    "Jacket": (0.93, 0.33, 0.31), "Skin": (1.0, 0.82, 0.68), "Hair": (0.26, 0.17, 0.11),
    "Lower": (0.20, 0.24, 0.40), "Legs": (0.20, 0.24, 0.40), "Shoes": (0.16, 0.16, 0.20),
    "White": (1, 1, 1), "EyeDark": (0.13, 0.10, 0.12), "Blush": (1.0, 0.60, 0.60),
    "Mouth": (0.72, 0.28, 0.30), "MouthIn": (0.42, 0.12, 0.16), "SkinFlat": (1.0, 0.82, 0.68), "Hat": (0.93, 0.33, 0.31), "Grey": (0.52, 0.55, 0.62),
    "Gold": (1.0, 0.82, 0.22), "Outline": (0.14, 0.15, 0.24),
    "HatShade": (0.70, 0.22, 0.20), "Pink": (1.0, 0.62, 0.72),
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
# head growth for chibi proportions (the figure keeps HD_H, so the body gets shorter); --chibi 1.0 for models
# whose pictures already have big heads
CHIBI = float(argv[argv.index("--chibi") + 1]) if "--chibi" in argv else 1.28


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
    if HD_JSON.get("neck_z"):
        # Big hair hanging beside the neck (pigtails, a bob) hides the narrowest slice; the picture's collar
        # says where the neck is.
        print("HD neck from the picture's collar %.3f (slice search said %.3f)" % (HD_JSON["neck_z"], NECK_Z))
        NECK_Z = HD_JSON["neck_z"]

    # Chibi proportions, to match the 2D art: the head grows about the neck, then the whole figure is scaled
    # back to HD_H, so the body comes out shorter. The blend under the neck keeps the shoulders as they were.
    neck_slice = [c for c in V if abs(c.z - NECK_Z) < 0.01 * H and abs(c.x) < TX]
    CHIBI_PIVOT = Vector((0.0, sum(c.y for c in neck_slice) / len(neck_slice), NECK_Z))
    def chibi(c):
        k = 1 + (CHIBI - 1) * smooth01(NECK_Z - 0.025 * H, NECK_Z + 0.03 * H, c.z)
        return CHIBI_PIVOT + (c - CHIBI_PIVOT) * k
    for v in hd.data.vertices:
        v.co = chibi(v.co)
    CHIBI_S = HD_H / max(v.co.z for v in hd.data.vertices)
    for v in hd.data.vertices:
        v.co = v.co * CHIBI_S
    V = [v.co.copy() for v in hd.data.vertices]
    TX *= CHIBI_S
    NECK_Z *= CHIBI_S
    CHIBI_PIVOT = CHIBI_PIVOT * CHIBI_S
    print("HD chibi: head x%.2f, figure x%.3f" % (CHIBI, CHIBI_S))

    def grown(x, z):
        """A point read off the model before the chibi head growth (FACE_MARKS), carried onto the grown head."""
        pz = CHIBI_PIVOT.z / CHIBI_S
        return x * CHIBI * CHIBI_S, (pz + (z - pz) * CHIBI) * CHIBI_S

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

    def squash_bun():
        """--bun: a hair bun on top of the head lifts every hat off the head. The line hats are always worn, so
        the bun is squashed into a low knot under the hat. It starts where the head, above its widest level,
        first narrows to under 62% of that width (a round head only gets that narrow right at its top)."""
        if "--bun" not in argv:
            return False
        top = max(c.z for c in V)
        z0 = NECK_Z + 0.05 * H
        nb = 40
        widths = []
        for i in range(nb):
            a_, b_ = z0 + (top - z0) * i / nb, z0 + (top - z0) * (i + 1) / nb
            sl = [abs(c.x) for c in V if a_ <= c.z < b_]
            widths.append(max(sl) if sl else 0.0)
        imax = max(range(nb), key=lambda i: widths[i])
        waist = next((j for j in range(imax + 1, nb) if 0 < widths[j] < 0.62 * widths[imax]), None)
        if waist is None:
            print("HD bun: the head never narrows enough, nothing squashed")
            return False
        z_cut = z0 + (top - z0) * (waist + 0.5) / nb
        ring = [c for c in V if abs(c.z - z_cut) < (top - z0) / nb]
        cy_ = sum(c.y for c in ring) / len(ring) if ring else 0.0
        for v in hd.data.vertices:
            if v.co.z > z_cut:
                v.co.z = z_cut + (v.co.z - z_cut) * 0.4
                v.co.x *= 0.85
                v.co.y = cy_ + (v.co.y - cy_) * 0.85
        print("HD bun squashed above z %.3f (head top was %.3f)" % (z_cut, top))
        return True

    if squash_bun():
        V = [v.co.copy() for v in hd.data.vertices]
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


def flatten_mouth():
    """Image-to-3D sculpts a pouting lip bump where the picture only paints a smile line, and the painted face
    reads cleanly only on a smooth surface. The mouth area is pushed back (along Y only, so no triangle can fold
    over as seen from the front) onto a smooth surface fitted to the ring of face around it."""
    import numpy as np
    marks = FACE_MARKS.get(HD_VARIANT)
    if not marks or HD_JSON.get("mouth_filled"):        # hdbake.py fills the mouth cavity without fold-overs
        return
    cx, cz = grown(*marks[2])
    rx = max(marks[3][0] * CHIBI * CHIBI_S * 2.0, 0.07)
    rz = max(marks[3][1] * CHIBI * CHIBI_S * 3.0, 0.05)
    me = hd.data
    me.update()
    ring, inner = [], []
    for v in me.vertices:
        if v.normal.y > -0.25 or v.co.y > 0:
            continue
        d = ((v.co.x - cx) / rx) ** 2 + ((v.co.z - cz) / rz) ** 2
        if d < 1.0:
            inner.append((v, d))
        elif d < 2.2:
            ring.append(v.co.copy())
    if len(ring) < 8 or not inner:
        return
    R = np.array([[1, c.x - cx, c.z - cz, (c.x - cx) ** 2, (c.z - cz) ** 2, (c.x - cx) * (c.z - cz)] for c in ring])
    coef, *_ = np.linalg.lstsq(R, np.array([c.y for c in ring]), rcond=None)
    moved = 0
    for v, d in inner:
        x, z = v.co.x - cx, v.co.z - cz
        fit = float(np.dot(coef, [1, x, z, x * x, z * z, x * z]))
        w = 1 - d
        w = w * w * (3 - 2 * w)
        # only ever push a bump back, never pull the face out; premium models also fill a sculpted open mouth,
        # since their pictures paint it on
        if fit > v.co.y or PREMIUM:
            v.co.y = v.co.y + (fit - v.co.y) * w
            moved += 1
    me.update()
    print("HD mouth flattened", moved, "of", len(inner), "vertices")


def hd_body():
    """Skins the HD mesh, sorts its faces into recolourable regions and writes the region-normalised texture."""
    flatten_mouth()
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
    px = np.array(img.pixels[:], dtype=np.float32).reshape(Hh, W, 4)   # bottom row first, like UV space
    pos, nrm, valid = hdface.texel_geometry(me, W, Hh)
    global PROJ, SKIN_UV
    PROJ = None
    marks = FACE_MARKS.get(HD_VARIANT)
    if HD_ART and marks:
        art = hdface.load_rgba(HD_ART)
        eyes_art = hdface.find_eyes(art)
        if eyes_art:
            eyes_model = (grown(*marks[0][0]), grown(*marks[0][1]))
            scale, painted = hdface.project_face(px, pos, nrm, valid, art, eyes_art, eyes_model, NECK_Z)
            PROJ = {"scale": scale, "eye_half": (eyes_art["half"][0] / scale, eyes_art["half"][1] / scale)}
            print("HD face painted from %s: %d texels, %.0f px per unit" % (os.path.basename(HD_ART), painted, scale))
        else:
            print("HD face: no eyes found in", HD_ART)
    # Everything keeps the colours of its design; only the jacket is tinted with the line colour, per texel.
    tips = [(TIP.x, TIP.y, TIP.z), (-TIP.x, TIP.y, TIP.z)]
    jacket = hdface.jacket_mask(px, pos, valid, HIP_Z - 0.06 * H, NECK_Z - 0.01 * H, tips, (TIP - WRIST).length * 1.25)
    out = hdface.finish_texture(px, valid, jacket, ref_percentile=65 if PREMIUM else None)
    print("HD jacket texels", int(jacket.sum()))
    # A patch of plain skin colour in a free corner of the texture, for eyelids and patches laid on the face.
    rgb = out[..., :3]
    mx, mn = rgb.max(-1), rgb.min(-1)
    sat = (mx - mn) / np.maximum(mx, 1e-6)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    face = valid & (-nrm[..., 1] > 0.6) & (pos[..., 2] > NECK_Z + 0.02) & ~jacket
    skinny = face & (r >= g) & (g >= b) & (sat > 0.12) & (sat < 0.55) & (mx > 0.55) & (r - g < 0.35)
    skin = np.median(rgb[skinny], axis=0) if skinny.sum() > 50 else np.array(COLORS["Skin"], dtype=np.float32)
    block = hdface.free_block(valid, 32)
    if block is None:
        block = (0, 0)
    bx, by = block
    out[by:by + 32, bx:bx + 32, :3] = skin
    out[by:by + 32, bx:bx + 32, 3] = 0.0
    SKIN_UV = ((bx + 16) / W, (by + 16) / Hh)
    print("HD skin", tuple(round(float(c), 3) for c in skin), "at", block)
    tex = bpy.data.images.new("PassengerHD_albedo", W, Hh, alpha=True)
    tex.alpha_mode = 'STRAIGHT'
    tex.pixels = out.ravel()
    tex.filepath_raw = OUT[:-4] + "_albedo.png"
    tex.file_format = 'PNG'
    tex.save()
    for poly in me.polygons:
        poly.use_smooth = True
    me.materials.clear()
    mat = bpy.data.materials.get("Body") or bpy.data.materials.new("Body")
    mat.name = "Body"
    # hdbake.py's GLB brings a "Body" material with its texture; the FBX would carry that texture's local path
    # (the game sets its own material and texture)
    if mat.node_tree:
        for node in [n for n in mat.node_tree.nodes if n.type == 'TEX_IMAGE']:
            mat.node_tree.nodes.remove(node)
    me.materials.append(mat)
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
# Shapes painted by Tools/art/hdbake.py bring their marks, read off the front view (see HD_JSON).
if HD_JSON.get("face_marks"):
    _m = HD_JSON["face_marks"]
    FACE_MARKS[HD_VARIANT] = ((tuple(_m[0][0]), tuple(_m[0][1])), tuple(_m[1]), tuple(_m[2]), tuple(_m[3]))
    print("HD face marks from the picture", FACE_MARKS[HD_VARIANT])


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
    # the marks were read before the chibi head growth: carry them onto the grown head
    kk = CHIBI * CHIBI_S
    pz = CHIBI_PIVOT.z / CHIBI_S
    def grown(x, z):
        return x * kk, (pz + (z - pz) * CHIBI) * CHIBI_S
    e1, e2, (mx, mz) = grown(*e1), grown(*e2), grown(mx, mz)
    erx, erz, mrx, mrz = erx * kk, erz * kk, mrx * kk, mrz * kk
    if PROJ:
        # the face is now the concept art's: lids cover its eyes (lashes may peek out, like closed eyes do)
        erx, erz = max(erx, PROJ["eye_half"][0]), max(erz, PROJ["eye_half"][1])
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

    def skin_normal(index, at_):
        """The smooth (vertex-interpolated) normal of the body surface at a ray hit."""
        poly = hd.data.polygons[index]
        vs = [hd.data.vertices[i] for i in poly.vertices[:3]]
        a, b, c = (v.co for v in vs)
        area = (b - a).cross(c - a).length or 1e-9
        wa = (b - at_).cross(c - at_).length / area
        wb = (c - at_).cross(a - at_).length / area
        wc = max(0.0, 1.0 - wa - wb)
        nn = vs[0].normal * wa + vs[1].normal * wb + vs[2].normal * wc
        return nn.normalized() if nn.length > 1e-6 else poly.normal.copy()

    def skin_uvs(ob):
        """Every corner of the overlay samples the plain skin patch of the body texture (see hd_body), so
        lids and patches are exactly the skin colour under the game's lighting."""
        me_ = ob.data
        layer = me_.uv_layers.active or me_.uv_layers.new(name="UVMap")
        for d in layer.data:
            d.uv = SKIN_UV

    def place(part, name, x, z, lift):
        ob = part.build(name) if isinstance(part, Part) else part
        if SKIN_UV is not None:
            skin_uvs(ob)
        at, n = on_face(x, z)
        turn = Vector((0, -1, 0)).rotation_difference(n).to_matrix().to_4x4()
        ob.matrix_world = Matrix.Translation(at + n * lift) @ turn
        # Lay it onto the face: each vertex moves along the normal by how far the surface under it stands out
        # from (or falls back behind) the surface at the centre, so nothing sinks into cheeks or lips.
        mw = ob.matrix_world.copy()
        inv = mw.inverted()
        under = {}
        for v in ob.data.vertices:
            p_ = mw @ v.co
            hit, _, index, _ = bvh.ray_cast(p_ + n * 0.06, -n, 0.3)
            if hit is not None:
                shift = max(-0.025, min(0.025, (hit - at).dot(n)))
                v.co = inv @ (p_ + n * shift)
                under[v.index] = skin_normal(index, hit)
        # Skin parts (lids, patches) take the normals of the face under them, so they light exactly like the
        # skin they cover instead of showing as a little dome.
        me_ = ob.data
        skin_slots = {i for i, m in enumerate(me_.materials) if m is not None and m.name.startswith("SkinFlat")}
        if skin_slots and under:
            turn_back = inv.to_3x3()
            normals = []
            for poly in me_.polygons:
                skin = poly.material_index in skin_slots
                if skin:
                    poly.use_smooth = True
                for li in poly.loop_indices:
                    vi = me_.loops[li].vertex_index
                    if skin and vi in under:
                        normals.append((turn_back @ under[vi]).normalized())
                    else:
                        normals.append(me_.corner_normals[li].vector.copy())
            me_.normals_split_custom_set(normals)
        world = ob.matrix_world.copy()
        ob.parent = rig
        ob.parent_type = 'BONE'
        ob.parent_bone = "Head"
        bpy.context.view_layer.update()
        ob.matrix_world = world

    # closed eyes: a skin lid over each eye with a happy upward arc drawn on it
    for side in (1, -1):
        c, rx, rz = E[side]
        p = Part()
        p.ellipsoid((0, 0, 0), (rx, 0.012, rz), "SkinFlat", seg=18, rings=10)
        p.torus((0, -0.011, -rz * 0.25), rx * 0.75, max(0.008, rz * 0.13), "EyeDark", seg=12, ring=6, arc=(25, 155), rot=(90, 0, 0))
        place(p, "EyesClosed_%s" % ("L" if side > 0 else "R"), c.x, c.z, 0.004)
    mc, mrx, mrz = M
    # open, laughing mouth: a wide D (flat, slightly smiling top, round bottom) with teeth and a tongue, as
    # the conductor laughs in the 2D art
    half_w = max(mrx * 1.25, 0.05)
    place(hdface.open_smile("MouthOpen", half_w, half_w * 1.05, MATS), "MouthOpen", mc.x, mc.z - half_w * 0.25, 0.004)
    p = Part()   # frown over a skin patch that hides the painted smile
    p.ellipsoid((0, 0, 0), (max(mrx, 0.035) * 1.2 + 0.01, 0.012, max(mrz, 0.02) * 1.4 + 0.014), "SkinFlat", seg=16, rings=8)
    fr = max(mrx, 0.035)
    p.torus((0, -0.015, -fr * 0.55), fr * 0.8, 0.012, "MouthIn", seg=12, ring=6, arc=(50, 130), rot=(90, 0, 0))
    place(p, "MouthSad", mc.x, mc.z, 0.004)
    # the game looks for single objects: join the two lids into EyesClosed
    for name in ("EyesClosed",):
        pair = [bpy.data.objects[name + "_L"], bpy.data.objects[name + "_R"]]
        bpy.ops.object.select_all(action='DESELECT')
        for o in pair:
            o.select_set(True)
        bpy.context.view_layer.objects.active = pair[0]
        bpy.ops.object.join()
        pair[0].name = pair[0].data.name = name


if HD:
    body = hd_body()
    hd_face()


# ---------------------------------------------------------------- line hats (one silhouette per line)
def seam(p, points, radius, mat):
    """A thin piped line through a list of points (stitching, braid, stripes)."""
    for p0, p1 in zip(points, points[1:]):
        p.capsule(p0, p1, radius, mat, seg=8, rings=3)


def dome_line(cx, cy, cz, rx, ry, rz, azimuth, steps=10):
    """Points over the top of an ellipsoidal dome, from the front rim to the back rim, turned by azimuth."""
    pts = []
    for i in range(steps + 1):
        th = math.pi * i / steps                       # 0 = front rim, pi = back rim
        u, w = -math.cos(th), math.sin(th)              # along the line, and height
        ca, sa = math.cos(math.radians(azimuth)), math.sin(math.radians(azimuth))
        pts.append((cx + u * rx * sa * 1.01, cy + u * ry * ca * 1.01, cz + w * rz * 1.01))
    return pts


# Hat slots: "Hat" wears the line colour, "HatShade" a deeper shade of it (bands, panels, undersides),
# "White", "Gold", "EyeDark" (glossy black) and "Pink" are fixed. No ink hull: the soft clay look has none.

p = Part()  # 0 red: baseball cap with six stitched panels, a shaded peak and a metro roundel
p.ellipsoid((0, 0.03, 1.37), (0.375, 0.365, 0.225), "Hat", seg=32, rings=16)
for az in (0, 60, 120):
    seam(p, dome_line(0, 0.03, 1.37, 0.375, 0.365, 0.225, az), 0.007, "HatShade")
p.ellipsoid((0, 0.03, 1.598), (0.042, 0.042, 0.03), "HatShade", seg=12, rings=8)          # top button
p.cone((0, 0.03, 1.165), 0.37, 0.37, 0.05, "HatShade", seg=32, bevel=0.012)            # sweatband rim
p.ellipsoid((0, -0.36, 1.385), (0.25, 0.18, 0.03), "Hat", seg=28, rings=8, rot=(13, 0, 0))   # peak
p.ellipsoid((0, -0.355, 1.372), (0.245, 0.175, 0.024), "HatShade", seg=28, rings=8, rot=(13, 0, 0))
seam(p, [(math.sin(math.radians(a)) * 0.215, -0.36 - math.cos(math.radians(a)) * 0.155, 1.405 - 0.035 * math.cos(math.radians(a)))
         for a in range(-70, 71, 14)], 0.006, "White")                                   # stitching on the peak
p.cone((0, -0.33, 1.47), 0.085, 0.085, 0.02, "White", seg=24, bevel=0.006, rot=(70, 0, 0))   # roundel
p.torus((0, -0.336, 1.47), 0.072, 0.014, "Gold", seg=20, ring=6, rot=(70, 0, 0))
p.box((0, -0.343, 1.468), (0.15, 0.02, 0.032), "HatShade", bevel=0.008, rot=(-20, 0, 0))
attach(p.build("Hat0"), "Hat")

p = Part()  # 1 blue: conductor's peaked cap with a flared crown, gold cord and a winged badge
p.cone((0, 0.03, 1.56), 0.275, 0.335, 0.22, "Hat", seg=32, bevel=0.03)
p.ellipsoid((0, 0.03, 1.668), (0.335, 0.335, 0.045), "Hat", seg=32, rings=8)              # soft top
p.cone((0, 0.03, 1.475), 0.282, 0.285, 0.085, "HatShade", seg=32, bevel=0.015)        # band
p.ellipsoid((0, -0.255, 1.44), (0.25, 0.17, 0.026), "EyeDark", seg=28, rings=8, rot=(16, 0, 0))   # glossy visor
seam(p, [(-0.24, -0.215, 1.485), (-0.12, -0.268, 1.49), (0.0, -0.283, 1.492), (0.12, -0.268, 1.49), (0.24, -0.215, 1.485)], 0.013, "Gold")
for side in (-1, 1):
    p.ellipsoid((side * 0.245, -0.205, 1.486), (0.024, 0.02, 0.024), "Gold", seg=10, rings=6)
p.ellipsoid((0, -0.286, 1.585), (0.055, 0.02, 0.06), "Gold", seg=14, rings=8)          # badge
for side in (-1, 1):
    p.ellipsoid((side * 0.075, -0.282, 1.59), (0.05, 0.014, 0.024), "Gold", seg=12, rings=6, rot=(0, side * -20, 0))   # wings
p.ellipsoid((0, -0.3, 1.585), (0.028, 0.012, 0.03), "Hat", seg=10, rings=6)
attach(p.build("Hat1"), "Hat")

p = Part()  # 2 green: top hat with a rolled brim, a gently waisted crown, a satin band and a gold buckle
p.cone((0, 0.04, 1.462), 0.36, 0.36, 0.035, "Hat", seg=36, bevel=0.012)
p.torus((0, 0.04, 1.47), 0.365, 0.03, "HatShade", seg=36, ring=8)                     # rolled edge
p.cone((0, 0.03, 1.56), 0.265, 0.25, 0.17, "Hat", seg=32, bevel=0.0)
p.cone((0, 0.03, 1.72), 0.25, 0.285, 0.16, "Hat", seg=32, bevel=0.0)
p.cone((0, 0.03, 1.80), 0.285, 0.28, 0.02, "Hat", seg=32, bevel=0.015)                # flat top
p.cone((0, 0.03, 1.52), 0.27, 0.268, 0.075, "HatShade", seg=32, bevel=0.008)          # band
p.box((0, -0.235, 1.52), (0.11, 0.03, 0.09), "Gold", bevel=0.012)                       # buckle frame
p.box((0, -0.245, 1.52), (0.055, 0.02, 0.04), "HatShade", bevel=0.006)
p.ellipsoid((0.17, -0.18, 1.56), (0.03, 0.02, 0.09), "White", seg=10, rings=8, rot=(0, -30, 0))   # little feather
attach(p.build("Hat2"), "Hat")

p = Part()  # 3 yellow: party cone with spiral stripes, a frilly collar and a fluffy pompom
p.cone((0, 0.03, 1.67), 0.25, 0.035, 0.46, "Hat", seg=32, bevel=0.0)
for k in range(3):                                                                     # spiral stripes
    pts = []
    for i in range(13):
        f = i / 12
        z = 1.46 + 0.42 * f
        r = 0.25 - 0.215 * (z - 1.44) / 0.46 + 0.006
        a = math.radians(k * 120 + f * 300)
        pts.append((math.sin(a) * r, 0.03 - math.cos(a) * r, z))
    seam(p, pts, 0.016, "White")
for i in range(16):                                                                    # frilly collar
    a = 2 * math.pi * i / 16
    p.ellipsoid((math.sin(a) * 0.255, 0.03 - math.cos(a) * 0.255, 1.455), (0.055, 0.055, 0.04), "White", seg=10, rings=6)
for (dx, dy, dz) in ((0, 0, 0), (0.045, 0, 0.02), (-0.045, 0, 0.02), (0, 0.045, 0.02), (0, -0.045, 0.02), (0, 0, 0.055)):
    p.ellipsoid((dx, 0.03 + dy, 1.915 + dz), (0.055, 0.055, 0.055), "White", seg=10, rings=6)
attach(p.build("Hat3"), "Hat")

p = Part()  # 4 purple: knitted bobble beanie with a turned-up ribbed cuff and a big pompom
p.ellipsoid((0, 0.03, 1.40), (0.36, 0.355, 0.27), "Hat", seg=32, rings=16)
p.cone((0, 0.03, 1.215), 0.372, 0.37, 0.13, "HatShade", seg=40, bevel=0.03)
for i in range(44):                                                                    # ribbing on the cuff
    a = 2 * math.pi * i / 44
    p.capsule((math.sin(a) * 0.371, 0.03 - math.cos(a) * 0.371, 1.172), (math.sin(a) * 0.371, 0.03 - math.cos(a) * 0.371, 1.258), 0.019, "HatShade", seg=6, rings=3)
for az in range(0, 180, 30):                                                           # knit ridges over the crown
    seam(p, dome_line(0, 0.03, 1.40, 0.36, 0.355, 0.27, az, steps=12)[2:-2], 0.006, "HatShade")
for (dx, dy, dz, r) in ((0, 0, 0, 0.11), (0.06, 0.02, 0.03, 0.08), (-0.06, -0.01, 0.03, 0.08), (0.0, -0.06, 0.04, 0.075), (0.0, 0.06, 0.05, 0.075), (0.02, 0.0, 0.09, 0.07)):
    p.ellipsoid((dx, 0.03 + dy, 1.70 + dz), (r, r, r * 0.92), "White", seg=12, rings=8)
attach(p.build("Hat4"), "Hat")

p = Part()  # 5 orange: straw sun hat with a wavy brim, woven ridges, a ribbon band and a flower
for i in range(18):                                                                    # wavy brim from overlapping petals
    a = 2 * math.pi * i / 18
    p.ellipsoid((math.sin(a) * 0.33, 0.06 - math.cos(a) * 0.33, 1.435 + 0.012 * math.sin(a * 3)), (0.15, 0.15, 0.025), "Hat", seg=12, rings=6,
                rot=(0, 0, -math.degrees(a)))
p.cone((0, 0.07, 1.43), 0.43, 0.42, 0.03, "Hat", seg=40, bevel=0.012, rot=(-6, 0, 0))
p.ellipsoid((0, 0.03, 1.45), (0.32, 0.32, 0.21), "Hat", seg=32, rings=14)
for z, r in ((1.53, 0.305), (1.58, 0.28), (1.62, 0.24)):                               # woven ridges
    p.torus((0, 0.03, z), r, 0.008, "HatShade", seg=32, ring=6)
p.cone((0, 0.03, 1.485), 0.322, 0.31, 0.055, "White", seg=32, bevel=0.01)              # ribbon band
for k in range(5):                                                                     # flower on the side
    a = 2 * math.pi * k / 5
    p.ellipsoid((0.20 + math.sin(a) * 0.045, -0.25, 1.495 + math.cos(a) * 0.045), (0.035, 0.018, 0.035), "Pink", seg=10, rings=6)
p.ellipsoid((0.20, -0.262, 1.495), (0.025, 0.015, 0.025), "Gold", seg=8, rings=6)
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
