"""Shared mesh-building helpers for the Metro Sort art scripts (run inside Blender).

Geometry is authored in world space: Z up, -Y is the side that faces the game camera, 1 unit = 1 Unity unit.
Materials are only named colour slots; the game recolours each slot by name.
"""
import bpy, bmesh, math
from mathutils import Vector, Matrix, Euler

MATS = {}
scene = None


def reset(colors):
    """Empties the scene and registers one material per named colour slot."""
    global scene
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.fps = 30
    MATS.clear()
    for name, c in colors.items():
        m = bpy.data.materials.new(name)
        m.diffuse_color = (c[0], c[1], c[2], 1)
        MATS[name] = m
    return scene


class Part:
    """Accumulates geometry (in world space) with a material per face."""

    def __init__(self):
        self.bm = bmesh.new()
        self.mats = []

    def _slot(self, mat):
        if mat not in self.mats:
            self.mats.append(mat)
        return self.mats.index(mat)

    def _finish(self, geom_verts, matrix, mat, smooth=True):
        bmesh.ops.transform(self.bm, matrix=matrix, verts=geom_verts)
        idx = self._slot(mat)
        faces = set()
        for v in geom_verts:
            faces.update(v.link_faces)
        for f in faces:
            f.material_index = idx
            f.smooth = smooth
        return faces

    def ellipsoid(self, loc, scale, mat, rot=(0, 0, 0), seg=20, rings=10):
        r = bmesh.ops.create_uvsphere(self.bm, u_segments=seg, v_segments=rings, radius=1.0)
        m = Matrix.Translation(loc) @ Euler([math.radians(a) for a in rot]).to_matrix().to_4x4() @ Matrix.Diagonal((*scale, 1))
        self._finish(r["verts"], m, mat)

    def capsule(self, p0, p1, radius, mat, seg=16, rings=9, radius1=None):
        """Rounded limb from p0 to p1. radius1 tapers the far end."""
        p0, p1 = Vector(p0), Vector(p1)
        r = bmesh.ops.create_uvsphere(self.bm, u_segments=seg, v_segments=rings, radius=1.0)
        half = (p1 - p0).length / 2
        r1 = radius if radius1 is None else radius1
        for v in r["verts"]:
            top = v.co.z > 0
            rr = radius if top else r1
            v.co = Vector((v.co.x * rr, v.co.y * rr, v.co.z * rr + (half if top else -half)))
        quat = Vector((0, 0, 1)).rotation_difference((p0 - p1).normalized())
        m = Matrix.Translation((p0 + p1) / 2) @ quat.to_matrix().to_4x4()
        self._finish(r["verts"], m, mat)

    def cone(self, loc, r_bottom, r_top, depth, mat, seg=24, bevel=0.012, rot=(0, 0, 0)):
        r = bmesh.ops.create_cone(self.bm, cap_ends=True, segments=seg, radius1=r_bottom, radius2=max(r_top, 0.001), depth=depth)
        verts = r["verts"]
        if bevel > 0:
            edges = [e for e in {e for v in verts for e in v.link_edges}
                     if abs(e.verts[0].co.z - e.verts[1].co.z) < 1e-5 and len(e.link_faces) == 2]
            res = bmesh.ops.bevel(self.bm, geom=edges, offset=bevel, segments=3, profile=0.5, affect='EDGES')
            verts = list(set(verts) | set(res["verts"]))
            verts = [v for v in verts if v.is_valid]
        # the flat end caps stay flat-shaded so they do not pick up a gradient from the rounded edge
        flat = []
        for f in {f for v in verts for f in v.link_faces}:
            f.normal_update()
            if abs(f.normal.z) > 0.999:
                flat.append(f)
        m = Matrix.Translation(loc) @ Euler([math.radians(a) for a in rot]).to_matrix().to_4x4()
        self._finish(verts, m, mat)
        for f in flat:
            f.smooth = False

    def box(self, loc, size, mat, bevel=0.04, rot=(0, 0, 0)):
        r = bmesh.ops.create_cube(self.bm, size=1.0)
        verts = r["verts"]
        for v in verts:
            v.co = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2]))
        if bevel > 0:
            edges = list({e for v in verts for e in v.link_edges})
            res = bmesh.ops.bevel(self.bm, geom=edges, offset=bevel, segments=3, profile=0.5, affect='EDGES')
            verts = [v for v in set(verts) | set(res["verts"]) if v.is_valid]
        m = Matrix.Translation(loc) @ Euler([math.radians(a) for a in rot]).to_matrix().to_4x4()
        self._finish(verts, m, mat)

    def torus(self, loc, major, minor, mat, seg=24, ring=8, arc=(0, 360), rot=(0, 0, 0)):
        """Tube bent around Z (full ring or an arc, capped by collapsing the ends)."""
        a0, a1 = math.radians(arc[0]), math.radians(arc[1])
        full = abs(arc[1] - arc[0]) >= 360
        n = seg if full else seg + 1
        rows = []
        for i in range(n):
            t = a0 + (a1 - a0) * i / (seg if not full else seg)
            # ends of an arc shrink to a point so it reads as a rounded stroke
            k = 1.0
            if not full:
                edge = min(i, n - 1 - i)
                k = [0.05, 0.7, 0.93][edge] if edge < 3 else 1.0
            centre = Vector((math.cos(t) * major, math.sin(t) * major, 0))
            out = Vector((math.cos(t), math.sin(t), 0))
            row = []
            for j in range(ring):
                p = 2 * math.pi * j / ring
                row.append(self.bm.verts.new(centre + out * (math.cos(p) * minor * k) + Vector((0, 0, math.sin(p) * minor * k))))
            rows.append(row)
        count = n if full else n - 1
        for i in range(count):
            a, b = rows[i], rows[(i + 1) % n]
            for j in range(ring):
                self.bm.faces.new((a[j], b[j], b[(j + 1) % ring], a[(j + 1) % ring]))
        verts = [v for row in rows for v in row]
        if not full:
            self.bm.faces.new(rows[0])
            self.bm.faces.new(list(reversed(rows[-1])))
        m = Matrix.Translation(loc) @ Euler([math.radians(a) for a in rot]).to_matrix().to_4x4()
        self._finish(verts, m, mat)

    def text(self, body, loc, size, depth, mat):
        cu = bpy.data.curves.new("txt", 'FONT')
        cu.body = body
        cu.size = size
        cu.extrude = depth
        cu.bevel_depth = 0.008
        cu.align_x = 'CENTER'
        cu.align_y = 'CENTER'
        ob = bpy.data.objects.new("txt", cu)
        scene.collection.objects.link(ob)
        bpy.context.view_layer.update()
        me = bpy.data.meshes.new_from_object(ob.evaluated_get(bpy.context.evaluated_depsgraph_get()))
        before = set(self.bm.verts)
        self.bm.from_mesh(me)
        verts = [v for v in self.bm.verts if v not in before]
        bpy.data.objects.remove(ob)
        # stand the glyph up facing -Y
        m = Matrix.Translation(loc) @ Euler((math.radians(90), 0, 0)).to_matrix().to_4x4()
        self._finish(verts, m, mat, smooth=False)

    def profile(self, points, x0, x1, mat, smooth=True):
        """Extrudes a closed (y, z) outline along X, without end caps."""
        rows = []
        for x in (x0, x1):
            rows.append([self.bm.verts.new((x, y, z)) for y, z in points])
        n = len(points)
        for j in range(n):
            self.bm.faces.new((rows[0][j], rows[1][j], rows[1][(j + 1) % n], rows[0][(j + 1) % n]))
        self._finish(rows[0] + rows[1], Matrix.Identity(4), mat, smooth)

    def prism(self, points, depth, mat, bevel=0.03, y=0.0):
        """A flat (x, z) outline given thickness along Y, with rounded rims. Faces the camera (-Y)."""
        front = [self.bm.verts.new((x, y - depth / 2, z)) for x, z in points]
        back = [self.bm.verts.new((x, y + depth / 2, z)) for x, z in points]
        n = len(points)
        self.bm.faces.new(front)
        self.bm.faces.new(list(reversed(back)))
        for j in range(n):
            self.bm.faces.new((front[j], back[j], back[(j + 1) % n], front[(j + 1) % n]))
        verts = front + back
        if bevel > 0:
            edges = [e for e in {e for v in verts for e in v.link_edges} if abs(e.verts[0].co.y - e.verts[1].co.y) < 1e-6]
            res = bmesh.ops.bevel(self.bm, geom=edges, offset=bevel, segments=3, profile=0.5, affect='EDGES')
            verts = [v for v in set(verts) | set(res["verts"]) if v.is_valid]
        bmesh.ops.recalc_face_normals(self.bm, faces=list({f for v in verts for f in v.link_faces}))
        self._finish(verts, Matrix.Identity(4), mat, smooth=False)

    def outline(self, thickness, mat="Outline"):
        """Inverted-hull ink outline baked in as extra geometry (drawn with a front-culling material)."""
        self.bm.normal_update()
        geom = list(self.bm.verts) + list(self.bm.edges) + list(self.bm.faces)
        dup = bmesh.ops.duplicate(self.bm, geom=geom)
        idx = self._slot(mat)
        for el in dup["geom"]:
            if isinstance(el, bmesh.types.BMVert):
                el.co += el.normal * thickness
            elif isinstance(el, bmesh.types.BMFace):
                el.material_index = idx
                el.smooth = True

    def build(self, name, origin=(0, 0, 0)):
        bmesh.ops.recalc_face_normals(self.bm, faces=[f for f in self.bm.faces if not self.mats[f.material_index].startswith("Outline")])
        if any(abs(c) > 0 for c in origin):
            bmesh.ops.translate(self.bm, verts=self.bm.verts, vec=-Vector(origin))
        me = bpy.data.meshes.new(name)
        self.bm.to_mesh(me)
        self.bm.free()
        for mname in self.mats:
            me.materials.append(MATS[mname])
        ob = bpy.data.objects.new(name, me)
        ob.location = origin
        scene.collection.objects.link(ob)
        return ob


def export(path, log=True):
    """Static (unrigged) models: transforms are baked so every object arrives in Unity unrotated."""
    import os
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH'}, mesh_smooth_type='OFF',
                             apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True, bake_anim=False)
    if log:
        print("EXPORTED", path, os.path.getsize(path))


def render_setup(ortho_scale, size=(640, 640), background=(0.82, 0.80, 0.76)):
    """Quick viewport-style render for checking shapes; the hull faces are flipped so outlines show."""
    cam_data = bpy.data.cameras.new("Cam")
    cam_data.type = 'ORTHO'
    cam_data.ortho_scale = ortho_scale
    cam = bpy.data.objects.new("Cam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.display.shading.light = 'STUDIO'
    scene.display.shading.color_type = 'MATERIAL'
    scene.display.shading.show_shadows = True
    scene.display.shading.show_backface_culling = True
    scene.render.resolution_x, scene.render.resolution_y = size
    scene.world = bpy.data.worlds.new("W")
    scene.world.color = background
    for ob in scene.objects:
        if ob.type != 'MESH':
            continue
        names = [m.name for m in ob.data.materials]
        hull = [i for i, n in enumerate(names) if n.startswith("Outline")]
        if hull:
            bm = bmesh.new()
            bm.from_mesh(ob.data)
            bmesh.ops.reverse_faces(bm, faces=[f for f in bm.faces if f.material_index in hull])
            bm.to_mesh(ob.data)
            bm.free()
    return cam


def render(cam, path, cam_from, look_at):
    cam.location = cam_from
    cam.rotation_euler = (Vector(look_at) - Vector(cam_from)).to_track_quat('-Z', 'Y').to_euler()
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
