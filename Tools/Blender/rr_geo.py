"""Shared geometry helpers for Rule and Ruin procedural assets (Blender 5.x, bmesh only).

Every part is built into one bmesh with a per-face material class and emission, written to the
corner colour attribute "Col":  R = material class (0 hull, 0.5 trim, 1 glass/crystal/bio),
G = emission strength, B = baked ambient occlusion (filled by bake_ao), A = 1.
"""
import bpy, bmesh, math
from mathutils import Vector, Matrix

HULL, TRIM, GLASS = 0.0, 0.5, 1.0


class Builder:
    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.face_info = {}   # BMFace -> (class, emission)

    def _tag(self, faces, cls, emis):
        for f in faces:
            self.face_info[f] = (cls, emis)

    def loft(self, rings, cls=HULL, emis=0.0, cap_start=True, cap_end=True, point_start=None, point_end=None):
        """rings: list of closed loops (lists of Vector, equal length)."""
        bm = self.bm
        vr = [[bm.verts.new(p) for p in r] for r in rings]
        faces = []
        n = len(rings[0])
        for a, b in zip(vr[:-1], vr[1:]):
            for i in range(n):
                j = (i + 1) % n
                faces.append(bm.faces.new((a[i], a[j], b[j], b[i])))
        if point_start is not None:
            pv = bm.verts.new(point_start)
            for i in range(n):
                faces.append(bm.faces.new((vr[0][(i + 1) % n], vr[0][i], pv)))
        elif cap_start:
            faces.append(bm.faces.new(list(reversed(vr[0]))))
        if point_end is not None:
            pv = bm.verts.new(point_end)
            for i in range(n):
                faces.append(bm.faces.new((vr[-1][i], vr[-1][(i + 1) % n], pv)))
        elif cap_end:
            faces.append(bm.faces.new(vr[-1]))
        self._tag(faces, cls, emis)
        return faces

    def prism_crystal(self, base, axis, height, radius, sides=6, tip=0.35, cls=GLASS, emis=0.8, twist=0.0):
        """Hexagonal crystal: narrower foot, prism body and a pointed pyramidal tip."""
        axis = axis.normalized()
        q = axis.to_track_quat('Z', 'Y')

        def ring(z, r):
            return [base + q @ Vector((math.cos(2 * math.pi * i / sides + twist) * r,
                                       math.sin(2 * math.pi * i / sides + twist) * r, z)) for i in range(sides)]
        body = height * (1 - tip)
        rings = [ring(-radius * 0.6, radius * 0.7), ring(0, radius), ring(body, radius * 0.96)]
        return self.loft(rings, cls, emis, cap_start=True, point_end=base + axis * height)

    def shard(self, base, direction, length, width, thick, up=Vector((0, 0, 1)), cls=HULL, emis=0.0):
        """Long faceted blade with a diamond cross-section, pointed at both ends (front at base + dir*length)."""
        d = direction.normalized()
        side = d.cross(up)
        if side.length < 1e-4:
            side = Vector((1, 0, 0))
        side.normalize()
        nup = side.cross(d).normalized()

        def sec(t, s):
            c = base + d * (t * length)
            return [c + side * (width * s), c + nup * (thick * s), c - side * (width * s), c - nup * (thick * s * 0.6)]
        rings = [sec(0.16, 0.7), sec(0.42, 1.0), sec(0.76, 0.62)]
        return self.loft(rings, cls, emis, point_start=base, point_end=base + d * length)

    def tube(self, pts, radii, sides=8, cls=HULL, emis=0.0, point_end=True, up=Vector((0, 0, 1)), squash=1.0):
        """Tapered tube along a polyline (horns, claws, spines)."""
        rings = []
        for i, p in enumerate(pts):
            t = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
            s = t.cross(up)
            if s.length < 1e-4:
                s = t.cross(Vector((1, 0, 0)))
            s.normalize()
            u = s.cross(t).normalized()
            r = radii[i]
            rings.append([p + s * (math.cos(2 * math.pi * k / sides) * r) + u * (math.sin(2 * math.pi * k / sides) * r * squash)
                          for k in range(sides)])
        if point_end:
            tip = pts[-1] + (pts[-1] - pts[-2]).normalized() * max(radii[-1] * 3, 1e-3)
            return self.loft(rings, cls, emis, cap_start=True, point_end=tip)
        return self.loft(rings, cls, emis)

    def box(self, center, size, rot=None, cls=TRIM, emis=0.0, taper=1.0):
        rot = rot or Matrix.Identity(3)
        sx, sy, sz = size
        rings = []
        for z in (-1, 1):
            k = taper if z > 0 else 1.0
            rings.append([center + rot @ Vector((x * sx * k, y * sy * k, z * sz)) for x, y in ((-1, -1), (1, -1), (1, 1), (-1, 1))])
        return self.loft(rings, cls, emis)

    def torus(self, center, axis, R, r, seg=32, sides=6, cls=TRIM, emis=0.0):
        axis = axis.normalized()
        q = axis.to_track_quat('Z', 'Y')
        bm = self.bm
        verts = []
        for i in range(seg):
            a = 2 * math.pi * i / seg
            c = Vector((math.cos(a) * R, math.sin(a) * R, 0))
            radial = c.normalized()
            verts.append([bm.verts.new(center + q @ (c + radial * (math.cos(2 * math.pi * k / sides) * r)
                                                    + Vector((0, 0, math.sin(2 * math.pi * k / sides) * r))))
                          for k in range(sides)])
        faces = []
        for i in range(seg):
            a, b = verts[i], verts[(i + 1) % seg]
            for k in range(sides):
                j = (k + 1) % sides
                faces.append(bm.faces.new((a[k], a[j], b[j], b[k])))
        self._tag(faces, cls, emis)
        return faces

    def ellipsoid(self, center, radii, rot=None, u=16, v=10, cls=HULL, emis=0.0):
        """Ellipsoid whose long axis is local Y."""
        rot = rot or Matrix.Identity(3)
        rings = []
        for j in range(1, v):
            phi = math.pi * j / v
            rings.append([center + rot @ Vector((math.sin(phi) * math.cos(2 * math.pi * i / u) * radii[0],
                                                 -math.cos(phi) * radii[1],
                                                 math.sin(phi) * math.sin(2 * math.pi * i / u) * radii[2])) for i in range(u)])
        return self.loft(rings, cls, emis, point_start=center + rot @ Vector((0, -radii[1], 0)),
                         point_end=center + rot @ Vector((0, radii[1], 0)))

    def shell_plate(self, y0, y1, r0, r1, a0, a1, thick, zoff=0.0, steps=10, cls=HULL, emis=0.0, sx=1.0, sz=1.0):
        """Carapace plate: a partial elliptic shell from y0 (rear, radius r0) to y1 (front, radius r1),
        spanning the arc a0..a1 around the Y axis (0 = +X, pi/2 = +Z), with thickness."""
        def arc(y, r):
            return [Vector((math.cos(a) * r * sx, y, zoff + math.sin(a) * r * sz))
                    for a in [a0 + (a1 - a0) * i / steps for i in range(steps + 1)]]
        bm = self.bm

        def V(points):
            return [bm.verts.new(p) for p in points]
        o0, o1 = V(arc(y0, r0)), V(arc(y1, r1))
        i0, i1 = V(arc(y0, r0 - thick)), V(arc(y1, r1 - thick))
        faces = []
        for k in range(steps):
            faces.append(bm.faces.new((o0[k], o0[k + 1], o1[k + 1], o1[k])))
            faces.append(bm.faces.new((i0[k + 1], i0[k], i1[k], i1[k + 1])))
            faces.append(bm.faces.new((o0[k + 1], o0[k], i0[k], i0[k + 1])))
            faces.append(bm.faces.new((o1[k], o1[k + 1], i1[k + 1], i1[k])))
        faces.append(bm.faces.new((o0[0], i0[0], i1[0], o1[0])))
        faces.append(bm.faces.new((o1[-1], i1[-1], i0[-1], o0[-1])))
        self._tag(faces, cls, emis)
        return faces

    def to_object(self, collection=None, sharp_angle=35.0):
        bm = self.bm
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        col = bm.loops.layers.float_color.new("Col")
        for f in bm.faces:
            cls, emis = self.face_info.get(f, (HULL, 0.0))
            for loop in f.loops:
                loop[col] = (cls, emis, 1.0, 1.0)
        me = bpy.data.meshes.new(self.name)
        bm.to_mesh(me)
        bm.free()
        ob = bpy.data.objects.new(self.name, me)
        (collection or bpy.context.scene.collection).objects.link(ob)
        me.color_attributes.active_color = me.color_attributes["Col"]
        for p in me.polygons:
            p.use_smooth = True
        me.set_sharp_from_angle(angle=math.radians(sharp_angle))
        return ob


def apply_modifiers(ob):
    bpy.ops.object.select_all(action='DESELECT')
    bpy.context.view_layer.objects.active = ob
    ob.select_set(True)
    for m in list(ob.modifiers):
        bpy.ops.object.modifier_apply(modifier=m.name)
    ob.select_set(False)


def bake_ao(ob, samples=40):
    """Bake ambient occlusion into the B channel of the Col attribute (Cycles vertex-colour bake)."""
    me = ob.data
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.device = 'CPU'
    scene.cycles.samples = samples
    if not me.materials:
        mat = bpy.data.materials.get("RR_Bake") or bpy.data.materials.new("RR_Bake")
        me.materials.append(mat)
    ao = me.color_attributes.new("AO", 'FLOAT_COLOR', 'CORNER')
    me.color_attributes.active_color = ao
    bpy.ops.object.select_all(action='DESELECT')
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.bake(type='AO', target='VERTEX_COLORS')
    col = me.color_attributes["Col"]
    ao = me.color_attributes["AO"]
    for i in range(len(me.loops)):
        c = list(col.data[i].color)
        c[2] = max(0.0, min(1.0, ao.data[i].color[0]))
        col.data[i].color = c
    me.color_attributes.remove(ao)
    me.color_attributes.active_color = me.color_attributes["Col"]
    ob.select_set(False)


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    world = bpy.data.worlds.new("World")
    bpy.context.scene.world = world
    world.light_settings.distance = 0.6


def export_fbx(ob, path):
    bpy.ops.object.select_all(action='DESELECT')
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                             bake_space_transform=True, axis_forward='Z', axis_up='Y', object_types={'MESH'},
                             use_mesh_modifiers=True, mesh_smooth_type='OFF', colors_type='LINEAR', add_leaf_bones=False,
                             bake_anim=False)
    ob.select_set(False)