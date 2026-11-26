"""Face-aware mesh work for the HD passengers, shared by hdbake.py (the premium Hunyuan shapes) and passenger.py
(the older TRELLIS scans).

An image-to-3D shape sculpts what the pictures only paint: an open mouth as a cavity, a ball of a nose. Reduced to
the game's triangle budget as a whole, the smooth face keeps comparatively few triangles (the reduction spends
them on hair strands and cloth folds), and the face painted from the picture lands on folded or faceted geometry.
Here:
- fill_mouth fills the mouth cavity with a fold-free patch lying on a smooth surface fitted to the face;
- soften_nose lowers the nose to a fraction of its height (the picture paints the nose's light and shade, and the
  Toy shader's rim light paled the steep underside of a full ball);
- reduce keeps the face's own density through the reduction, up to a share of the budget;
- unwrap gives the face's UV islands a multiple of the texel density of the rest;
- transfer_texture carries a texture painted for an older UV map onto the new one.

face: {"eyes": ((xl, zl), (xr, zr)), "eye_half": (hx, hz), "mouth": (x, z), "mouth_half": (hx, hz)}, in the
mesh's own units; Z up, the character faces -Y.
"""
import math

import bpy
import numpy as np


def mesh_arrays(me):
    """Vertex positions, vertex normals and triangles (vertex indices) of the mesh as it is now."""
    me.update()
    nv = len(me.vertices)
    co = np.empty(nv * 3, dtype=np.float64)
    me.vertices.foreach_get("co", co)
    nr = np.empty(nv * 3, dtype=np.float64)
    me.vertices.foreach_get("normal", nr)
    me.calc_loop_triangles()
    tri = np.empty(len(me.loop_triangles) * 3, dtype=np.int64)
    me.loop_triangles.foreach_get("vertices", tri)
    return co.reshape(-1, 3), nr.reshape(-1, 3), tri.reshape(-1, 3)


def tri_edges(tri):
    """Unique edges (sorted vertex pairs) of a triangle list."""
    e = np.concatenate([tri[:, [0, 1]], tri[:, [1, 2]], tri[:, [2, 0]]])
    return np.unique(np.sort(e, axis=1), axis=0)


def tri_count(me):
    return sum(len(p.vertices) - 2 for p in me.polygons)


def face_region(co, face):
    """Vertices of the face, from brow to chin and cheek to cheek as seen from the front, on the front half of
    the head. Also returns the depth that splits the head into front and back."""
    (xl, ze), (xr, _) = face["eyes"]
    xm, zm = face["mouth"]
    ehx, ehz = face["eye_half"]
    g = xr - xl
    em = ze - zm
    z_top, z_bot = ze + em + ehz, zm - 0.9 * em
    rx, rz = 0.75 * g + 2.2 * ehx, (z_top - z_bot) / 2
    d = ((co[:, 0] - (xl + xr) / 2) / rx) ** 2 + ((co[:, 2] - (z_top + z_bot) / 2) / rz) ** 2
    band = d < 1.0
    y_mid = float(co[band, 1].min() + co[band, 1].max()) / 2
    return band & (co[:, 1] < y_mid), y_mid


def robust_quadratic(x, z, y, sel, rounds=5):
    """Coefficients of y = c0 + c1 x + c2 z + c3 x^2 + c4 z^2 + c5 x z fitted to the selected points, refitted
    without the points far off the previous fit (a nose, a cavity, glasses), and the design matrix."""
    A = np.stack([np.ones_like(x), x, z, x * x, z * z, x * z], 1)
    use = sel
    for _ in range(rounds):
        coef = np.linalg.lstsq(A[use], y[use], rcond=None)[0]
        res = y - A @ coef
        mad = 1.4826 * float(np.median(np.abs(res[use]))) + 1e-9
        use = sel & (np.abs(res) < 2.5 * mad)
    return coef, A


def fill_mouth(me, face):
    """Image-to-3D carves an open mouth as a cavity (with a lip ledge and teeth in it), while the pictures paint
    the mouth on. Pushing the cavity's walls straight forward (what passenger.py did) folds the ledge over the
    floor: red shards in the painted mouth. Here the cavity, with a margin of face around it, is taken out as one
    disc of the mesh; the disc is laid out flat seen from the front (each inner vertex at the mean of its
    neighbours, the rim where it was: a fold-free layout) and lifted onto a smooth surface fitted to the face
    around the mouth, plus the rim's own offset from that surface spread smoothly inwards, so it joins the face
    without a step. Returns whether it was done."""
    co, nr, tri = mesh_arrays(me)
    _, y_mid = face_region(co, face)
    (xl, _), (xr, _) = face["eyes"]
    g = xr - xl
    xm, zm = face["mouth"]
    hx, hz = face["mouth_half"]
    x, z = co[:, 0] - xm, co[:, 2] - zm
    front = co[:, 1] < y_mid
    outer = front & ((x / (hx + 0.35 * g)) ** 2 + (z / (hz + 0.3 * g)) ** 2 < 1.0)
    core = front & ((x / hx) ** 2 + (z / hz) ** 2 < 1.0)
    # the face around the mouth as a quadratic y(x, z); the nose and the cavity are outliers
    ring = outer & ~core & (nr[:, 1] < -0.3)
    if ring.sum() < 12 or not core.any():
        print("mouth not filled: too little face around it")
        return False
    coef, A = robust_quadratic(x, z, co[:, 1], ring)
    behind = co[:, 1] - A @ coef                   # > 0: behind the face surface
    edges = tri_edges(tri)
    ea, eb = edges[:, 0], edges[:, 1]

    def neighbours(mask):
        n = np.zeros_like(mask)
        n[eb[mask[ea]]] = True
        n[ea[mask[eb]]] = True
        return n & ~mask

    # the mouth area, grown over the cavity (whatever lies clearly behind the face surface near the mouth)
    region = core.copy()
    deep = outer & (behind > 0.03 * g)
    while True:
        new = neighbours(region) & deep
        if not new.any():
            break
        region |= new
    cavity = int(region.sum())
    for _ in range(2):
        region |= neighbours(region) & front
    for attempt in range(4):
        ft = tri[region[tri].all(1)]
        if not len(ft):
            print("mouth not filled: no triangles around it")
            return False
        # the connected piece of it that holds the mouth
        emap = {}
        for i, t in enumerate(ft):
            for a_, b_ in ((t[0], t[1]), (t[1], t[2]), (t[2], t[0])):
                emap.setdefault((min(a_, b_), max(a_, b_)), []).append(i)
        cen = co[ft].mean(1)
        start = int(np.argmin((cen[:, 0] - xm) ** 2 + (cen[:, 2] - zm) ** 2))
        seen, stack = {start}, [start]
        while stack:
            t = ft[stack.pop()]
            for a_, b_ in ((t[0], t[1]), (t[1], t[2]), (t[2], t[0])):
                for j in emap[(min(a_, b_), max(a_, b_))]:
                    if j not in seen:
                        seen.add(j)
                        stack.append(j)
        disc = ft[sorted(seen)]
        e_all = np.sort(np.concatenate([disc[:, [0, 1]], disc[:, [1, 2]], disc[:, [2, 0]]]), axis=1)
        e_uni, e_count = np.unique(e_all, axis=0, return_counts=True)
        verts = np.unique(disc)
        if len(verts) - len(e_uni) + len(disc) == 1:          # Euler characteristic of a disc
            break
        region |= neighbours(region) & front                 # close small holes and try again
    else:
        print("mouth not filled: the area around it is not a disc")
        return False
    n = len(verts)
    la, lb = np.searchsorted(verts, e_uni[:, 0]), np.searchsorted(verts, e_uni[:, 1])
    deg = np.bincount(la, minlength=n) + np.bincount(lb, minlength=n)
    rim = np.zeros(n, dtype=bool)
    rim[np.searchsorted(verts, e_uni[e_count == 1].ravel())] = True
    inner = ~rim
    P = co[verts][:, [0, 2]].copy()
    R = np.where(rim, behind[verts], 0.0)          # the rim's offset from the fitted surface, spread inwards
    for _ in range(6000):
        for c in (0, 1):
            s = np.bincount(la, P[lb, c], n) + np.bincount(lb, P[la, c], n)
            P[inner, c] = s[inner] / deg[inner]
        s = np.bincount(la, R[lb], n) + np.bincount(lb, R[la], n)
        R[inner] = s[inner] / deg[inner]
    X, Z = P[:, 0] - xm, P[:, 1] - zm
    new = co.copy()
    new[verts, 0] = P[:, 0]
    new[verts, 2] = P[:, 1]
    new[verts, 1] = np.stack([np.ones_like(X), X, Z, X * X, Z * Z, X * Z], 1) @ coef + R
    me.vertices.foreach_set("co", new.ravel())
    me.update()
    tn = np.cross(new[disc[:, 1]] - new[disc[:, 0]], new[disc[:, 2]] - new[disc[:, 0]])
    print("mouth filled: %d vertices (%d in the cavity, %d on the rim), cavity up to %.0f%% of the eye spacing "
          "deep, %d of %d triangles facing back" % (n, cavity, int(rim.sum()), 100 * float(behind[verts].max()) / g,
                                                    int((tn[:, 1] > 0).sum()), len(disc)))
    return True


def soften_nose(me, face, nose):
    """The pictures paint the nose with its own light and shade, while the shapes model it as a ball standing well
    out of the face, often a little lower than the painted one: the Toy shader's rim light (strong where a surface
    turns away from the camera) made its steep underside a pale patch under the painted nose. Its height over the
    face is scaled by nose, along Y only (each point keeps its order along the view: nothing folds). The nose is
    the point standing out most between the eyes and the mouth; glasses (their bridge sits just above it) and a
    moustache below are outside its radius."""
    co, nr, _ = mesh_arrays(me)
    _, y_mid = face_region(co, face)
    (xl, ze), (xr, _) = face["eyes"]
    g = xr - xl
    xm, zm = face["mouth"]
    em = ze - zm
    x, z = co[:, 0] - (xl + xr) / 2, co[:, 2] - (ze + zm) / 2
    front = co[:, 1] < y_mid
    # the face between eyes and mouth as a quadratic (nose, glasses and eyes are outliers)
    area = front & (np.abs(x) < 0.45 * g) & (np.abs(z) < 0.75 * em) & (nr[:, 1] < -0.3)
    if area.sum() < 12:
        return
    coef, A = robust_quadratic(x, z, co[:, 1], area)
    out = -(co[:, 1] - A @ coef)                      # > 0: in front of the face surface
    cand = front & (np.abs(x) < 0.2 * g) & (co[:, 2] > zm + 0.15 * em) & (co[:, 2] < ze - 0.15 * em)
    if not cand.any():
        return
    tip = np.nonzero(cand)[0][int(np.argmax(out[cand]))]
    r = 0.2 * g
    d = np.hypot(co[:, 0] - co[tip, 0], co[:, 2] - co[tip, 2])
    t = np.clip((r - d) / (0.4 * r), 0.0, 1.0)         # 1 within 0.6 r of the tip, 0 from r on
    w = t * t * (3 - 2 * t) * front * (out > 0)
    new = co.copy()
    new[:, 1] += w * (1.0 - nose) * out
    me.vertices.foreach_set("co", new.ravel())
    me.update()
    print("nose: stood %.0f%% of the eye spacing out of the face, now %.0f%% (%d vertices)"
          % (100 * out[tip] / g, 100 * nose * out[tip] / g, int((w > 0).sum())))


def decimate(ob, target, group=None, protect=True):
    """Collapse-decimates the mesh to about target triangles. With a vertex group, protect=True keeps the group
    (its edges get a cost far above any other) and protect=False keeps everything else."""
    now = tri_count(ob.data)
    if now <= target:
        return
    dec = ob.modifiers.new("Decimate", 'DECIMATE')
    dec.ratio = target / now
    dec.use_collapse_triangulate = True
    if group:
        dec.vertex_group = group
        # the modifier adds cost to edges whose vertices weigh less than 1, so the kept part must weigh 0
        dec.invert_vertex_group = protect
        dec.vertex_group_factor = 1000.0
    with bpy.context.temp_override(object=ob, active_object=ob, selected_objects=[ob]):
        bpy.ops.object.modifier_apply(modifier="Decimate")


def reduce(ob, face, total, face_tris):
    """Reduces the mesh to about total triangles, of which the face keeps its own count, up to face_tris: first
    everything else comes down to its share with the face untouched, then the face to face_tris."""
    me = ob.data
    co, _, tri = mesh_arrays(me)
    on_face, _ = face_region(co, face)
    f_raw = int(on_face[tri].all(1).sum())
    ob.vertex_groups.new(name="Face").add(np.nonzero(on_face)[0].tolist(), 1.0, 'REPLACE')
    keep = min(f_raw, face_tris)
    decimate(ob, total - keep + f_raw, "Face", protect=True)
    if f_raw > face_tris:
        decimate(ob, total, "Face", protect=False)
    ob.vertex_groups.remove(ob.vertex_groups["Face"])     # (applying a modifier leaves old references stale)
    co, _, tri = mesh_arrays(me)
    on_face, _ = face_region(co, face)
    print("reduced to %d triangles, %d of them on the face (it had %d)"
          % (len(tri), int(on_face[tri].all(1).sum()), f_raw))


def uv_arrays(me, face):
    """Per polygon (triangles): loop corners, area, UV area and whether it lies on the face; per loop: UV."""
    npoly = len(me.polygons)
    ls = np.empty(npoly, dtype=np.int64)
    me.polygons.foreach_get("loop_start", ls)
    area = np.empty(npoly)
    me.polygons.foreach_get("area", area)
    lv = np.empty(len(me.loops), dtype=np.int64)
    me.loops.foreach_get("vertex_index", lv)
    luv = np.empty(len(me.loops) * 2)
    me.uv_layers.active.data.foreach_get("uv", luv)
    luv = luv.reshape(-1, 2)
    corners = ls[:, None] + np.arange(3)
    co, _, _ = mesh_arrays(me)
    on_face, _ = face_region(co, face)
    pn = np.empty(npoly * 3)
    me.polygons.foreach_get("normal", pn)
    face_poly = on_face[lv[corners]].all(1) & (pn.reshape(-1, 3)[:, 1] < -0.2)
    t = luv[corners]
    uv_area = 0.5 * np.abs((t[:, 1, 0] - t[:, 0, 0]) * (t[:, 2, 1] - t[:, 0, 1])
                           - (t[:, 2, 0] - t[:, 0, 0]) * (t[:, 1, 1] - t[:, 0, 1]))
    return corners, area, uv_area, face_poly, luv


def triangulate(ob):
    """Splits any polygon with more than three corners (the UV work below expects triangles)."""
    if all(len(p.vertices) == 3 for p in ob.data.polygons):
        return
    import bmesh
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    bm.to_mesh(ob.data)
    bm.free()
    ob.data.update()


def unwrap(ob, face, boost, size):
    """A fresh UV map on the active UV layer (smart projection), with the face's islands given boost times the
    texel density of the rest before packing. size is only for the report (texels per model unit)."""
    me = ob.data
    triangulate(ob)
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    bpy.context.view_layer.objects.active = ob
    ob.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66.0), island_margin=0.003)
    bpy.ops.object.mode_set(mode='OBJECT')
    if face and boost != 1.0:
        from bpy_extras import mesh_utils
        corners, area, uv_area, face_poly, luv = uv_arrays(me, face)
        boosted = 0
        for island in mesh_utils.mesh_linked_uv_islands(me):
            island = np.array(island, dtype=np.int64)
            if area[island][face_poly[island]].sum() > 0.25 * area[island].sum():
                lp = corners[island].ravel()
                c = luv[lp].mean(0)
                luv[lp] = c + (luv[lp] - c) * boost
                boosted += len(island)
        me.uv_layers.active.data.foreach_set("uv", luv.ravel())
        bpy.ops.object.mode_set(mode='EDIT')
        bpy.context.scene.tool_settings.use_uv_select_sync = True
        bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.uv.pack_islands(rotate=True, margin=0.003)
        bpy.ops.object.mode_set(mode='OBJECT')
        me.update()
        print("face UV islands (%d triangles) scaled up %.1fx before packing" % (boosted, boost))
    if face:
        corners, area, uv_area, face_poly, luv = uv_arrays(me, face)
        (xl, _), (xr, _) = face["eyes"]

        def density(sel):
            return size * math.sqrt(float(uv_area[sel].sum()) / max(float(area[sel].sum()), 1e-12))

        print("texels per unit: face %.0f, rest %.0f; eye spacing %.0f texels at %d"
              % (density(face_poly), density(~face_poly), density(face_poly) * (xr - xl), size))


def transfer_texture(me, old_layer, old_px, size, sample):
    """A size x size texture for the active UV map, each texel taking the colour that old_px (RGBA, bottom row
    first, as Blender keeps pixels) has at the same surface point through the UV map old_layer. Empty texels
    between islands take their neighbours' colours, so filtering never pulls in black. sample is hdface.sample."""
    h0, w0 = old_px.shape[:2]
    src = old_px[::-1]                                   # sample() reads top row first
    out = np.zeros((size, size, 4), dtype=np.float32)
    got = np.zeros((size, size), dtype=bool)
    new_uv = me.uv_layers.active.data
    old_uv = me.uv_layers[old_layer].data
    me.calc_loop_triangles()
    for tri in me.loop_triangles:
        P = np.array([[new_uv[l].uv.x * size, new_uv[l].uv.y * size] for l in tri.loops])
        Q = np.array([[old_uv[l].uv.x, old_uv[l].uv.y] for l in tri.loops])
        x0, y0 = np.floor(P.min(0)).astype(int)
        x1, y1 = np.ceil(P.max(0)).astype(int)
        x0, y0 = max(x0, 0), max(y0, 0)
        x1, y1 = min(x1, size - 1), min(y1, size - 1)
        if x1 < x0 or y1 < y0:
            continue
        d = (P[1, 1] - P[2, 1]) * (P[0, 0] - P[2, 0]) + (P[2, 0] - P[1, 0]) * (P[0, 1] - P[2, 1])
        if abs(d) < 1e-12:
            continue
        gx, gy = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
        l1 = ((P[1, 1] - P[2, 1]) * (gx - P[2, 0]) + (P[2, 0] - P[1, 0]) * (gy - P[2, 1])) / d
        l2 = ((P[2, 1] - P[0, 1]) * (gx - P[2, 0]) + (P[0, 0] - P[2, 0]) * (gy - P[2, 1])) / d
        l3 = 1 - l1 - l2
        inside = (l1 >= -0.02) & (l2 >= -0.02) & (l3 >= -0.02)
        if not inside.any():
            continue
        q = l1[inside][:, None] * Q[0] + l2[inside][:, None] * Q[1] + l3[inside][:, None] * Q[2]
        colour = sample(src, q[:, 0] * w0, (1.0 - q[:, 1]) * h0)
        sub = out[y0:y1 + 1, x0:x1 + 1]
        sub[inside] = colour
        got[y0:y1 + 1, x0:x1 + 1] |= inside
    for _ in range(8):
        grow = np.zeros_like(out)
        count = np.zeros(got.shape, dtype=np.float32)
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            src_ok = np.roll(got, (dy, dx), axis=(0, 1))
            val = np.roll(out, (dy, dx), axis=(0, 1))
            add = src_ok & ~got
            grow[add] += val[add]
            count[add] += 1
        new = (count > 0) & ~got
        out[new] = grow[new] / count[new][:, None]
        got |= new
    out[..., 3] = 1.0
    return out
