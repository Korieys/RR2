"""Rule and Ruin: detailed Pygmaei warships (Blender 5.x, bmesh only), after the creator's concept sheets
(Documents/3DT/input/Pyg_Space_Units_Transparent): dark riveted iron, crimson armour plates with black diagonal stripes,
ribbed engine cylinders glowing red, long-barrelled turrets, gothic spires, spikes along every edge and hanging below.

Run:  blender -b --factory-startup --python Tools/Blender/rr_pygmaei.py -- [--only id,id] [--no-bake] [--render DIR]
Writes Assets/RR/Art/Models/<id>.fbx (or $RR_FBX_OUT) and, with --render, preview PNGs (four views) into DIR.
Faces carry the ship shader's classes in the "Col" corner colour: R = class (0 hull iron, 0.25 painted armour,
0.5 trim metal, 1 glass/glow), G = emission, B = baked ambient occlusion. Seeded per id, so rebuilds are identical.
"""
import bpy, bmesh, math, os, random, sys
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import rr_geo as G
from rr_geo import Builder, HULL, TRIM, GLASS

PAINT = 0.25
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT_FBX = os.environ.get("RR_FBX_OUT") or os.path.join(ROOT, "Assets", "RR", "Art", "Models")
V = Vector
X, Y, Z = V((1, 0, 0)), V((0, 1, 0)), V((0, 0, 1))


def rot_to(direction, up=Z):
    """Rotation whose local +Y points along direction and local +Z leans to up."""
    d = direction.normalized()
    side = d.cross(up)
    if side.length < 1e-5:
        side = d.cross(X)
    side.normalize()
    u = side.cross(d).normalized()
    return Matrix((side, d, u)).transposed()


class PB(Builder):
    """Builder with the Pygmaei kit."""

    def __init__(self, name):
        super().__init__(name)
        self.rnd = random.Random(name)

    # -------------------------------------------------------------- primitives
    def cbox(self, center, half, bev=None, rot=None, cls=HULL, emis=0.0, taper=1.0, taper_y=None):
        """Chamfered box (bevelled edges and corners). half = (x, y, z) half-extents; the top can taper."""
        rot = rot or Matrix.Identity(3)
        hx, hy, hz = half
        bev = min(bev if bev is not None else min(half) * 0.22, hx * 0.45, hy * 0.45, hz * 0.45)
        ty = taper if taper_y is None else taper_y

        def rect(z, sx, sy, c):
            pts = [(-sx + c, -sy), (sx - c, -sy), (sx, -sy + c), (sx, sy - c), (sx - c, sy), (-sx + c, sy), (-sx, sy - c), (-sx, -sy + c)]
            return [center + rot @ V((px, py, z)) for px, py in pts]
        rings = [rect(-hz, hx - bev, hy - bev, bev * 0.5), rect(-hz + bev, hx, hy, bev),
                 rect(hz - bev, hx * taper, hy * ty, bev), rect(hz, hx * taper - bev, hy * ty - bev, bev * 0.5)]
        return self.loft(rings, cls, emis)

    def cone(self, base, direction, length, radius, sides=5, cls=TRIM, emis=0.0):
        d = direction.normalized()
        return self.tube([base, base + d * (length * 0.7)], [radius, radius * 0.45], sides=sides, cls=cls, emis=emis, point_end=True,
                         up=Z if abs(d.z) < 0.95 else X)

    def cyl(self, a, b, r, sides=12, cls=HULL, emis=0.0, r2=None):
        return self.tube([a, b], [r, r if r2 is None else r2], sides=sides, cls=cls, emis=emis, point_end=False,
                         up=Z if abs((b - a).normalized().z) < 0.95 else X)

    def plate(self, faces, inset, depth, paint_fn=None, seam_cls=HULL):
        """Turns faces into raised armour plates with seams between them; paint_fn(center) -> class of each plate."""
        faces = [f for f in faces if f.is_valid and f.calc_area() > inset * inset * 4]
        if not faces:
            return
        info = {f: self.face_info.get(f, (HULL, 0.0)) for f in faces}
        centers = {f: f.calc_center_median() for f in faces}
        res = bmesh.ops.inset_individual(self.bm, faces=faces, thickness=inset, depth=depth, use_even_offset=True)
        for f in res["faces"]:
            self.face_info[f] = (seam_cls, 0.0)
        for f in faces:
            cls, emis = info[f]
            if paint_fn is not None:
                cls = paint_fn(centers[f], cls)
            self.face_info[f] = (cls, emis)

    def windows(self, a, b, normal, n, w, h, emis=1.1, cls=GLASS):
        """A row of n lit windows from a to b on a wall facing normal."""
        nrm = normal.normalized()
        for i in range(n):
            t = (i + 0.5) / n
            c = a.lerp(b, t) + nrm * (w * 0.15)
            rot = rot_to(nrm, Z if abs(nrm.z) < 0.9 else Y)
            # local y = outward, x = along wall, z = up
            self.box(c, (w * 0.32, w * 0.1, h * 0.34), rot=rot, cls=cls, emis=emis)   # plain box: windows are too small for bevels

    # -------------------------------------------------------------- hull
    @staticmethod
    def section(w, h, keel=0.35):
        """Armoured warship section in XZ: narrow deck, sloped upper armour, slab flank, sloped belly, keel ridge."""
        return [V((0, 0, h)), V((w * 0.42, 0, h)), V((w * 0.78, 0, h * 0.72)), V((w, 0, h * 0.28)), V((w, 0, -h * 0.25)),
                V((w * 0.82, 0, -h * 0.62)), V((w * 0.38, 0, -h * 0.9)), V((0, 0, -h * (0.9 + keel))),
                V((-w * 0.38, 0, -h * 0.9)), V((-w * 0.82, 0, -h * 0.62)), V((-w, 0, -h * 0.25)), V((-w, 0, h * 0.28)),
                V((-w * 0.78, 0, h * 0.72)), V((-w * 0.42, 0, h))]

    def hull(self, L, W, H, prof, prow_tip, z0=0.0, paint_fn=None, inset=None, depth=None, keel=0.35):
        """Lofted, plated hull from stern (y=-L/2) to prow; prof = [(t, width scale, height scale, z shift)]."""
        n = max(len(prof), int(L * prof[-1][0] / (W * 0.16)))
        rings = []
        for i in range(n + 1):
            t = prof[-1][0] * i / n
            k = max(j for j in range(len(prof)) if prof[j][0] <= t + 1e-9)
            a, bb = prof[k], prof[min(k + 1, len(prof) - 1)]
            f = 0.0 if bb[0] == a[0] else (t - a[0]) / (bb[0] - a[0])
            sw, sh, dz = [a[m] + (bb[m] - a[m]) * f for m in (1, 2, 3)]
            sec = self.section(W * sw, H * sh, keel)
            dense = []
            for m in range(len(sec)):
                p0, p1 = sec[m], sec[(m + 1) % len(sec)]
                dense += [p0, p0.lerp(p1, 0.5)]
            y = -L / 2 + L * t
            rings.append([p + V((0, y, z0 + dz * H)) for p in dense])
        faces = self.loft(rings, HULL, 0.0, cap_start=True, point_end=prow_tip)
        body = [f for f in faces if len(f.verts) == 4]
        self.plate(body, inset or W * 0.022, depth or W * 0.018, paint_fn)
        return faces

    # -------------------------------------------------------------- parts
    def spikes(self, pts, direction, length, radius, cls=TRIM, jitter=0.25):
        for p in pts:
            k = 1 + self.rnd.uniform(-jitter, jitter)
            d = (direction + V((self.rnd.uniform(-.08, .08), self.rnd.uniform(-.08, .08), self.rnd.uniform(-.05, .05)))).normalized()
            self.cone(p, d, length * k, radius * (0.8 + 0.4 * k), sides=5, cls=cls)

    def hanging_plates(self, y0, y1, x, z, n, depth, width, side):
        """Ragged armour skirts hanging off the flank: pointed blades, crimson, angled outward."""
        for i in range(n):
            t = (i + 0.5) / n
            y = y0 + (y1 - y0) * t
            d = depth * (0.7 + 0.6 * self.rnd.random())
            base = V((x * side, y, z))
            tip = base + V((side * d * 0.18, -width * 0.25, -d))
            w = width * (0.42 + 0.16 * self.rnd.random())
            thick = width * 0.06
            out = V((side, 0, 0))
            ring0 = [base + V((0, -w, 0)), base + V((0, w, 0)), base + V((0, w, 0)) + out * thick, base + V((0, -w, 0)) + out * thick]
            mid = base.lerp(tip, 0.55)
            ring1 = [mid + V((0, -w * 0.7, 0)), mid + V((0, w * 0.55, 0)), mid + V((0, w * 0.55, 0)) + out * thick, mid + V((0, -w * 0.7, 0)) + out * thick]
            self.loft([ring0, ring1], PAINT if i % 3 != 1 else HULL, 0.0, cap_start=True, point_end=tip)

    def turret(self, pos, s, barrels=2, yaw=0.0, length=2.2):
        """Gun turret facing +Y (rotated by yaw degrees): ring, armoured housing, mantlet, barrels with muzzle brakes."""
        R = Matrix.Rotation(math.radians(yaw), 3, 'Z')
        self.cyl(pos + V((0, 0, -s * 0.1)), pos + V((0, 0, s * 0.18)), s * 0.78, sides=14, cls=TRIM)
        self.cbox(pos + V((0, 0, s * 0.42)), (s * 0.62, s * 0.72, s * 0.3), bev=s * 0.12, rot=R, cls=HULL, taper=0.78, taper_y=0.86)
        self.cbox(pos + R @ V((0, s * 0.62, s * 0.4)), (s * 0.5, s * 0.16, s * 0.24), bev=s * 0.06, rot=R, cls=TRIM)
        self.cbox(pos + R @ V((0, -s * 0.2, s * 0.78)), (s * 0.18, s * 0.22, s * 0.08), bev=s * 0.03, rot=R, cls=PAINT)
        for i in range(barrels):
            x = (i - (barrels - 1) / 2) * s * (0.34 if barrels > 2 else 0.4)
            a = pos + R @ V((x, s * 0.72, s * 0.42))
            b = pos + R @ V((x, s * (0.72 + length), s * 0.46))
            self.cyl(a, b, s * 0.085, sides=8, cls=TRIM)
            self.cyl(a + (b - a) * 0.08, a + (b - a) * 0.22, s * 0.12, sides=8, cls=TRIM)
            self.cyl(b - (b - a) * 0.08, b + (b - a).normalized() * s * 0.08, s * 0.13, sides=8, cls=TRIM)
            self.cyl(b + (b - a).normalized() * s * 0.06, b + (b - a).normalized() * s * 0.09, s * 0.07, sides=8, cls=GLASS, emis=0.8)
        self.cone(pos + R @ V((s * 0.4, -s * 0.4, s * 0.7)), V((0.1, -0.2, 1)), s * 0.7, s * 0.05, cls=TRIM)

    def engine(self, front_y, back_y, x, z, r, rings=4, collar_spikes=6):
        """Exposed engine cylinder: an iron front cap, glowing coil bands between ribbed collars, straps, flared nozzle."""
        L = front_y - back_y
        a = V((x, front_y, z))
        self.tube([a + V((0, L * 0.12, 0)), a], [r * 0.55, r * 0.95], sides=16, cls=HULL, point_end=False)
        seg = L * 0.78 / rings
        y = front_y
        for i in range(rings):
            self.cyl(V((x, y, z)), V((x, y - seg * 0.32, z)), r * 1.06, sides=16, cls=TRIM)              # rib collar
            self.cyl(V((x, y - seg * 0.32, z)), V((x, y - seg, z)), r * 0.9, sides=16, cls=GLASS, emis=1.5)  # glowing coil
            y -= seg
        for k in range(6):                                                                                    # straps over the coils
            ang = 2 * math.pi * k / 6 + 0.3
            off = V((math.cos(ang) * r * 0.97, 0, math.sin(ang) * r * 0.97))
            self.cbox(V((x, (front_y + y) / 2, z)) + off, (r * 0.09, (front_y - y) / 2, r * 0.05),
                      rot=rot_to(V((0, 1, 0)), off.normalized()), bev=r * 0.03, cls=TRIM)
        self.cyl(V((x, y, z)), V((x, y - L * 0.05, z)), r * 1.12, sides=16, cls=TRIM)
        self.tube([V((x, y - L * 0.05, z)), V((x, back_y + L * 0.02, z)), V((x, back_y, z))], [r * 0.95, r * 1.18, r * 1.24],
                  sides=16, cls=TRIM, point_end=False)
        self.cyl(V((x, back_y + L * 0.004, z)), V((x, back_y - L * 0.006, z)), r * 1.06, sides=16, cls=GLASS, emis=3.0)
        self.cyl(V((x, back_y - L * 0.006, z)), V((x, back_y - L * 0.012, z)), r * 0.45, sides=12, cls=GLASS, emis=4.0)
        for k in range(collar_spikes):
            ang = 2 * math.pi * k / collar_spikes
            d = V((math.cos(ang), 0.15, math.sin(ang)))
            self.cone(V((x, y - L * 0.02, z)) + V((d.x, 0, d.z)) * r * 1.05, d, r * 0.9, r * 0.12, cls=TRIM)

    def spire(self, base, w, h, tiers=3, crown=4, windows=True):
        """Gothic tower: stacked chamfered tiers with lit window slits and corner pinnacles, a tall spike roof."""
        z = base.z
        tw = w
        th = h / (tiers + 1.2)
        for t in range(tiers):
            c = V((base.x, base.y, z + th * 0.5))
            self.cbox(c, (tw * 0.5, tw * 0.5, th * 0.5), bev=tw * 0.08, cls=HULL if t % 2 == 0 else TRIM, taper=0.92)
            if windows:
                for nrm in (X, -X, Y, -Y):
                    p = c + nrm * (tw * 0.47)
                    along = V((nrm.y, -nrm.x, 0))
                    k = 2 if tw > 0.12 else 1
                    self.windows(p - along * tw * 0.2, p + along * tw * 0.2, nrm, k, tw * 0.1, th * 0.36, emis=1.0)
            for sx in (-1, 1):
                for sy in (-1, 1):
                    self.cone(V((base.x + sx * tw * 0.46, base.y + sy * tw * 0.46, z + th * 0.9)), V((sx * 0.08, sy * 0.08, 1)),
                              th * 0.75, tw * 0.07, sides=4, cls=TRIM)
            z += th
            tw *= 0.72
        self.tube([V((base.x, base.y, z)), V((base.x, base.y, z + h * 0.42))], [tw * 0.55, tw * 0.08], sides=4, cls=HULL, point_end=True)
        for k in range(crown):
            ang = 2 * math.pi * k / crown + 0.4
            self.cone(V((base.x + math.cos(ang) * tw * 0.4, base.y + math.sin(ang) * tw * 0.4, z)), V((math.cos(ang) * 0.35, math.sin(ang) * 0.35, 1)),
                      h * 0.18, tw * 0.07, sides=4, cls=TRIM)
        self.cyl(V((base.x, base.y, z + h * 0.43)), V((base.x, base.y, z + h * 0.45)), tw * 0.06, sides=6, cls=GLASS, emis=2.0)

    def stack(self, x, y, z, r, h, glow=2.0):
        """Furnace smokestack: riveted collar, iron flue with straps, glowing throat."""
        self.cyl(V((x, y, z)), V((x, y, z + h * 0.16)), r * 1.32, sides=12, cls=TRIM)
        self.cyl(V((x, y, z)), V((x, y, z + h)), r, sides=12, cls=HULL, r2=r * 0.9)
        for t in (0.35, 0.62, 0.86):
            self.cyl(V((x, y, z + h * t)), V((x, y, z + h * t + h * 0.04)), r * 1.06, sides=12, cls=TRIM)
        self.cyl(V((x, y, z + h)), V((x, y, z + h * 1.08)), r * 1.12, sides=12, cls=TRIM, r2=r * 1.2)
        self.tube([V((x, y, z + h * 1.08)), V((x, y, z + h * 0.94))], [r * 0.95, r * 0.3], sides=12, cls=GLASS, emis=glow, point_end=False)
        for k in range(4):
            ang = math.pi / 4 + k * math.pi / 2
            self.cone(V((x + math.cos(ang) * r * 1.1, y + math.sin(ang) * r * 1.1, z + h * 1.06)), V((math.cos(ang) * 0.4, math.sin(ang) * 0.4, 1)),
                      h * 0.2, r * 0.14, sides=4, cls=TRIM)

    def pipe(self, pts, r, rings_every=3):
        self.tube(pts, [r] * len(pts), sides=7, cls=TRIM, point_end=False)
        for i, p in enumerate(pts[1:-1]):
            if i % rings_every == 0:
                d = (pts[i + 2] - pts[i]).normalized()
                self.cyl(p - d * r * 0.8, p + d * r * 0.8, r * 1.5, sides=7, cls=TRIM)

    def blade(self, root, tip, width, thick, up=Z, curve=0.0, cls=PAINT, stripes=True):
        """Long flat prow blade from root to tip (diamond section), painted with black diagonal stripes."""
        d = tip - root
        side = d.normalized().cross(up).normalized()
        nup = side.cross(d.normalized()).normalized()
        rings = []
        steps = 7
        for i in range(steps):
            t = i / (steps - 1) * 0.86
            c = root + d * t + nup * (math.sin(t * math.pi) * curve)
            s = 1 - t * 0.55
            rings.append([c + side * width * s, c + nup * thick * s, c - side * width * s * 0.35, c - nup * thick * s])
        faces = self.loft(rings, cls, 0.0, cap_start=True, point_end=tip)
        if stripes:
            for f in faces:
                cc = f.calc_center_median()
                t = (cc - root).dot(d.normalized()) / d.length
                if int(t * 7) % 2 == 1:
                    self.face_info[f] = (HULL, 0.0)
        return faces


# =================================================================================================== paint patterns

def stripe_fn(L, start=0.15, period=0.16, slant=1.4, red_chance=0.28, seed="s", zone=0.11, H=None):
    """Crimson armour with broad black diagonal stripes on the prow wedge; aft of it, whole armour zones (a run of
    plates between two frames, on one side and one deck band) are painted crimson, the rest left dark iron."""
    zones = {}
    rnd = random.Random(seed)

    def fn(c, cls):
        t = (c.y + L / 2) / L
        if t > start:
            v = (c.y / L * 1.0 + abs(c.x) / L * slant - c.z / L * 0.5) / period
            return PAINT if (v % 1.0) < 0.58 else HULL
        band = 0 if H is None else (2 if c.z > H * 0.35 else 1 if c.z > -H * 0.3 else 0)
        key = (int(t / zone), c.x > 0, band)
        if key not in zones:
            zones[key] = rnd.random() < red_chance
        return PAINT if zones[key] else cls
    return fn


def random_red(chance, seed):
    rnd = random.Random(seed)
    return lambda c, cls: PAINT if rnd.random() < chance else cls


# =================================================================================================== ships

def superstructure(b, y0, y1, w, z, h, tiers=2, windows=True, red=0.3):
    """Stacked deck blocks along the spine with plated sides and window rows."""
    y_mid = (y0 + y1) / 2
    for k in range(tiers):
        hy = (y1 - y0) / 2 * (1 - 0.22 * k)
        hw = w * (1 - 0.25 * k)
        c = V((0, y_mid - k * (y1 - y0) * 0.06, z + h * (k + 0.5)))
        faces = b.cbox(c, (hw, hy, h * 0.5), bev=min(hw, h) * 0.18, cls=HULL, taper=0.9)
        b.bm.normal_update()
        sides = [f for f in faces if abs(f.normal.z) < 0.4 and f.calc_area() > (h * 0.4) ** 2]
        b.plate(sides, min(hw, h) * 0.06, min(hw, h) * 0.04, random_red(red, b.name + str(k)))
        if windows:
            for s in (-1, 1):
                b.windows(V((s * hw * 0.93, c.y - hy * 0.8, c.z + h * 0.12)), V((s * hw * 0.93, c.y + hy * 0.8, c.z + h * 0.12)),
                          V((s, 0, 0)), max(3, int(hy / h * 3)), h * 0.16, h * 0.22)
    return z + h * tiers


def broadside(b, L, W, H, y0, y1, side, n, z=0.02, barrel=0.6):
    """Broadside casemates: armoured gun ports along the flank slab, each with a short barrel and a lit sight slit."""
    for i in range(n):
        y = y0 + (y1 - y0) * (i + 0.5) / n
        c = V((side * W * 1.02, y, z * H))
        b.cbox(c, (W * 0.07, L * 0.022, H * 0.13), bev=W * 0.02, cls=TRIM if i % 2 else HULL, taper=0.85, taper_y=0.85)
        b.cyl(c + V((side * W * 0.06, 0, 0)), c + V((side * W * (0.06 + barrel * 0.25), L * 0.004, 0)), H * 0.035, sides=6, cls=TRIM)
        b.cbox(c + V((side * W * 0.065, 0, H * 0.09)), (W * 0.006, L * 0.012, H * 0.012), bev=W * 0.002, cls=GLASS, emis=1.0)


def strake(b, L, W, H, y0, y1, side, z=0.55, spikes=8):
    """Raised armour strake along the hull shoulder, with its own spikes."""
    c = V((side * W * 0.86, (y0 + y1) / 2, z * H))
    b.cbox(c, (W * 0.09, (y1 - y0) / 2, H * 0.09), bev=W * 0.03, rot=Matrix.Rotation(math.radians(side * -28), 3, 'Y'), cls=TRIM)
    pts = [V((side * W * 0.92, y0 + (y1 - y0) * (i + 0.5) / spikes, (z + 0.08) * H)) for i in range(spikes)]
    b.spikes(pts, V((side * 0.7, 0.5, 0.5)), H * 0.35, H * 0.04)


def flank_details(b, L, W, H, y0, y1, side, ribs=8, pipes=True):
    """Conduit pipe and a spiked gunwale along one flank."""
    if pipes:
        for k, zz in enumerate((H * 0.12,)):
            pts = []
            for i in range(9):
                y = y0 + (y1 - y0) * i / 8
                pts.append(V((side * W * 1.03, y, zz)))
            b.pipe(pts, H * 0.035)
    if not pipes:
        pts = [V((side * W * 0.96, y0 + (y1 - y0) * (i + 0.5) / 7, H * 0.3)) for i in range(7)]
        b.spikes(pts, V((side * 0.8, 0.45, 0.35)), H * 0.42, H * 0.045)


def greebles(b, y0, y1, xw, z, n, size, red=0.25):
    """Deck clutter on the strip y0..y1, |x| < xw at height z: vents, hatches and armoured boxes."""
    for i in range(n):
        x = b.rnd.uniform(-xw, xw)
        y = b.rnd.uniform(y0, y1)
        s = size * (0.5 + b.rnd.random())
        kind = b.rnd.random()
        if kind < 0.45:
            b.cbox(V((x, y, z + s * 0.25)), (s * 0.6, s * (0.6 + b.rnd.random()), s * 0.25), bev=s * 0.08,
                   cls=PAINT if b.rnd.random() < red else (TRIM if b.rnd.random() < 0.4 else HULL), taper=0.85)
        elif kind < 0.75:
            b.cyl(V((x, y, z)), V((x, y, z + s * 0.35)), s * 0.32, sides=8, cls=TRIM)
            b.cyl(V((x, y, z + s * 0.35)), V((x, y, z + s * 0.4)), s * 0.22, sides=8, cls=GLASS, emis=0.8)
        else:
            for k in range(3):
                b.cbox(V((x + (k - 1) * s * 0.3, y, z + s * 0.1)), (s * 0.1, s * 0.5, s * 0.1), bev=s * 0.03, cls=TRIM)


def keel_spikes(b, y0, y1, z, n, length, radius, spread=0.0):
    pts = []
    for i in range(n):
        t = (i + 0.5) / n
        x = (b.rnd.uniform(-1, 1) * spread) if spread else 0.0
        pts.append(V((x, y0 + (y1 - y0) * t, z)))
    for p in pts:
        k = 0.55 + 0.9 * b.rnd.random()
        if b.rnd.random() < 0.3:
            continue
        b.cone(p, V((b.rnd.uniform(-.15, .15), -0.45 + b.rnd.uniform(-.15, .1), -1)), length * k, radius * (0.7 + 0.5 * k),
               sides=5, cls=PAINT if b.rnd.random() < 0.6 else TRIM)


def revenant(sid):
    """Revenant-class command dreadnought: a cathedral flagship. Long plated hull, three deck tiers, a central cathedral of
    spires, four triple turrets, two stacked engine pairs, crimson striped ram prow, keel spikes and hanging skirts."""
    b = PB(sid)
    L, W, H = 2.2, 0.36, 0.26
    y0, y1 = -L / 2, L / 2
    prof = [(0.0, 0.86, 0.9, 0.0), (0.06, 1.0, 1.0, 0.0), (0.3, 1.04, 1.02, 0.0), (0.55, 1.0, 1.0, 0.0), (0.72, 0.88, 0.92, -0.02),
            (0.84, 0.66, 0.8, -0.05), (0.93, 0.4, 0.62, -0.1)]
    b.hull(L, W, H, prof, V((0, y1 + L * 0.1, -H * 0.38)), paint_fn=stripe_fn(L, 0.66, 0.2, 1.6, 0.35, sid, H=H))
    # Ram blades layered over the prow.
    for s in (-1, 1):
        b.blade(V((s * W * 0.55, y1 - L * 0.22, -H * 0.05)), V((s * W * 0.08, y1 + L * 0.12, -H * 0.45)), W * 0.22, W * 0.05)
    b.blade(V((0, y1 - L * 0.18, H * 0.75)), V((0, y1 + L * 0.06, -H * 0.1)), W * 0.12, W * 0.04, up=X)
    # Deck tiers and the cathedral.
    top = superstructure(b, y0 + L * 0.12, y0 + L * 0.68, W * 0.62, H * 0.95, H * 0.42, tiers=2)
    b.spire(V((0, y0 + L * 0.36, top)), W * 0.62, H * 1.9, tiers=4, crown=6)
    for s in (-1, 1):
        b.spire(V((s * W * 0.38, y0 + L * 0.27, top - H * 0.42)), W * 0.26, H * 1.3, tiers=3)
        b.spire(V((s * W * 0.34, y0 + L * 0.5, top - H * 0.42)), W * 0.22, H * 1.3, tiers=3)
        b.spire(V((s * W * 0.2, y0 + L * 0.2, top)), W * 0.16, H * 1.1, tiers=2)
    # Bridge windows on the cathedral front.
    b.windows(V((-W * 0.2, y0 + L * 0.36 + W * 0.33, top + H * 0.2)), V((W * 0.2, y0 + L * 0.36 + W * 0.33, top + H * 0.2)), Y, 5, W * 0.05, H * 0.18)
    # Four triple turrets: two superfiring forward, one each side aft of the cathedral.
    b.turret(V((0, y0 + L * 0.74, H * 1.0)), W * 0.3, barrels=3, length=2.6)
    b.turret(V((0, y0 + L * 0.63, H * 1.32)), W * 0.3, barrels=3, length=2.4)
    for s in (-1, 1):
        b.turret(V((s * W * 0.62, y0 + L * 0.58, H * 0.86)), W * 0.22, barrels=3, yaw=-s * 18, length=2.2)
        b.turret(V((s * W * 0.66, y0 + L * 0.22, H * 0.8)), W * 0.18, barrels=2, yaw=-s * 8, length=2.0)
    # Engines: two stacked pairs at the stern (the concept's cylinder banks), plus a central block.
    for s in (-1, 1):
        for zz in (H * 0.32, -H * 0.42):
            b.engine(y0 + L * 0.2, y0 - L * 0.1, s * W * 0.62, zz, H * 0.36, rings=4)
    b.cbox(V((0, y0 + L * 0.06, 0)), (W * 0.42, L * 0.08, H * 0.7), cls=HULL, taper=0.9)
    # Flanks, keel spikes, skirts.
    for s in (-1, 1):
        flank_details(b, L, W, H, y0 + L * 0.22, y1 - L * 0.2, s, ribs=10)
        b.hanging_plates(y0 + L * 0.28, y1 - L * 0.26, W * 0.98, -H * 0.55, 5, H * 0.95, L * 0.06, s)
        broadside(b, L, W, H, y0 + L * 0.26, y1 - L * 0.32, s, 9)
        strake(b, L, W, H, y0 + L * 0.2, y1 - L * 0.3, s, spikes=9)
    keel_spikes(b, y0 + L * 0.22, y1 - L * 0.14, -H * 1.12, 9, H * 1.5, H * 0.1, spread=W * 0.25)
    greebles(b, y0 + L * 0.06, y0 + L * 0.2, W * 0.5, H * 0.95, 14, W * 0.12)
    greebles(b, y0 + L * 0.68, y1 - L * 0.22, W * 0.32, H * 0.9, 10, W * 0.1)
    # Dorsal spine spikes ahead of the turrets.
    b.spikes([V((0, y0 + L * (0.8 + 0.035 * i), H * 0.92 - i * H * 0.04)) for i in range(4)], V((0, 0.5, 1)), H * 0.5, H * 0.05)
    return b.to_object(sharp_angle=30)


def furnace_cruiser(sid):
    """Furnace-class plasma cruiser: broad hull carrying four exposed furnace stacks, three engine cylinders, three triple
    turrets, a drill-and-blade prow and a striped crimson bow."""
    b = PB(sid)
    L, W, H = 1.7, 0.33, 0.24
    y0, y1 = -L / 2, L / 2
    prof = [(0.0, 0.9, 0.9, 0.0), (0.07, 1.0, 1.0, 0.0), (0.45, 1.05, 1.0, 0.0), (0.7, 0.95, 0.94, -0.02), (0.85, 0.7, 0.8, -0.06),
            (0.94, 0.42, 0.62, -0.1)]
    b.hull(L, W, H, prof, V((0, y1 + L * 0.08, -H * 0.35)), paint_fn=stripe_fn(L, 0.64, 0.2, 1.4, 0.35, sid, H=H))
    top = superstructure(b, y0 + L * 0.14, y0 + L * 0.6, W * 0.6, H * 0.95, H * 0.4, tiers=1)
    b.spire(V((0, y0 + L * 0.2, top)), W * 0.4, H * 1.7, tiers=3, crown=4)
    # Exposed furnace cores: two long glowing coil cylinders cradled along the upper deck, with short exhaust stacks.
    for s in (-1, 1):
        x = s * W * 0.72
        b.engine(y0 + L * 0.62, y0 + L * 0.18, x, H * 1.05, H * 0.34, rings=6, collar_spikes=0)
        for t in (0.25, 0.42, 0.58):
            b.cbox(V((x, y0 + L * t, H * 0.82)), (H * 0.3, L * 0.018, H * 0.14), bev=H * 0.04, cls=TRIM)
    for i, (x, y) in enumerate([(-0.18, 0.3), (0.18, 0.3), (0.0, 0.46)]):
        b.stack(x * W * 1.4, y0 + L * y, top - H * 0.1, W * 0.1, H * (0.75 + 0.15 * (i % 2)))
    b.turret(V((0, y0 + L * 0.7, H * 1.0)), W * 0.3, barrels=3, length=2.4)
    for s in (-1, 1):
        b.turret(V((s * W * 0.66, y0 + L * 0.62, H * 0.82)), W * 0.22, barrels=3, yaw=-s * 15, length=2.0)
    # Drill prow: three converging bits with spiral collars, between two blades.
    for k in range(3):
        a = 2 * math.pi * k / 3 + math.pi / 2
        root = V((math.cos(a) * W * 0.16, y1 - L * 0.04, math.sin(a) * H * 0.18 - H * 0.15))
        tip = V((0, y1 + L * 0.18, -H * 0.15))
        b.tube([root, root.lerp(tip, 0.5), tip], [W * 0.075, W * 0.05, W * 0.01], sides=7, cls=TRIM, point_end=False)
        for t in (0.2, 0.4, 0.6):
            p = root.lerp(tip, t)
            d = (tip - root).normalized()
            b.cyl(p, p + d * W * 0.03, W * 0.075 * (1.15 - t * 0.7), sides=7, cls=TRIM)
    for s in (-1, 1):
        b.blade(V((s * W * 0.55, y1 - L * 0.24, -H * 0.1)), V((s * W * 0.18, y1 + L * 0.1, -H * 0.4)), W * 0.2, W * 0.05)
    for k, (x, z) in enumerate([(-0.62, 0.0), (0.62, 0.0), (0.0, -0.45)]):
        b.engine(y0 + L * 0.2, y0 - L * 0.09, x * W, z * H * 1.2, H * 0.38, rings=4)
    for s in (-1, 1):
        flank_details(b, L, W, H, y0 + L * 0.22, y1 - L * 0.2, s, ribs=8)
        b.hanging_plates(y0 + L * 0.28, y1 - L * 0.26, W * 0.98, -H * 0.55, 4, H * 0.85, L * 0.06, s)
        broadside(b, L, W, H, y0 + L * 0.27, y1 - L * 0.34, s, 6)
        strake(b, L, W, H, y0 + L * 0.2, y1 - L * 0.32, s, spikes=7)
    keel_spikes(b, y0 + L * 0.22, y1 - L * 0.12, -H * 1.12, 8, H * 1.4, H * 0.1, spread=W * 0.25)
    return b.to_object(sharp_angle=30)


def annihilator(sid):
    """Annihilator-class destroyer: a long narrow ramming hull, a spinal cannon down the keel, four twin turrets (two
    superfiring forward), three engines, a crimson ram prow and a spiked gunwale."""
    b = PB(sid)
    L, W, H = 1.55, 0.24, 0.19
    y0, y1 = -L / 2, L / 2
    prof = [(0.0, 0.9, 0.92, 0.0), (0.07, 1.0, 1.0, 0.0), (0.5, 1.02, 1.0, 0.0), (0.74, 0.9, 0.92, -0.03), (0.87, 0.62, 0.76, -0.07),
            (0.95, 0.36, 0.58, -0.12)]
    b.hull(L, W, H, prof, V((0, y1 + L * 0.13, -H * 0.45)), paint_fn=stripe_fn(L, 0.6, 0.18, 1.8, 0.3, sid, H=H))
    top = superstructure(b, y0 + L * 0.12, y0 + L * 0.48, W * 0.6, H * 0.95, H * 0.42, tiers=1)
    b.spire(V((0, y0 + L * 0.24, top)), W * 0.5, H * 2.0, tiers=3, crown=4)
    for s in (-1, 1):
        b.spire(V((s * W * 0.3, y0 + L * 0.36, top)), W * 0.2, H * 1.0, tiers=2)
    b.turret(V((0, y0 + L * 0.66, H * 1.0)), W * 0.3, barrels=2, length=2.6)
    b.turret(V((0, y0 + L * 0.55, H * 1.36)), W * 0.3, barrels=2, length=2.4)
    for s in (-1, 1):
        b.turret(V((s * W * 0.7, y0 + L * 0.42, H * 0.8)), W * 0.22, barrels=2, yaw=-s * 20, length=2.0)
    # Spinal cannon along the keel, out through the ram.
    b.cyl(V((0, y0 + L * 0.15, -H * 0.88)), V((0, y1 + L * 0.2, -H * 0.88)), W * 0.13, sides=10, cls=TRIM, r2=W * 0.1)
    for t in (0.3, 0.45, 0.6, 0.75, 0.9):
        y = y0 + L * t
        b.cyl(V((0, y, -H * 0.88)), V((0, y + L * 0.02, -H * 0.88)), W * 0.17, sides=10, cls=TRIM)
    b.cyl(V((0, y1 + L * 0.2, -H * 0.88)), V((0, y1 + L * 0.215, -H * 0.88)), W * 0.08, sides=10, cls=GLASS, emis=2.0)
    for s in (-1, 1):
        b.blade(V((s * W * 0.6, y1 - L * 0.26, -H * 0.0)), V((s * W * 0.1, y1 + L * 0.16, -H * 0.5)), W * 0.24, W * 0.05)
    for k, (x, z) in enumerate([(-0.58, 0.15), (0.58, 0.15), (0.0, -0.5)]):
        b.engine(y0 + L * 0.18, y0 - L * 0.1, x * W, z * H * 1.3, H * 0.4, rings=4)
    for s in (-1, 1):
        flank_details(b, L, W, H, y0 + L * 0.2, y1 - L * 0.18, s, ribs=9)
        b.hanging_plates(y0 + L * 0.3, y1 - L * 0.24, W * 0.98, -H * 0.55, 4, H * 0.85, L * 0.055, s)
        broadside(b, L, W, H, y0 + L * 0.24, y1 - L * 0.34, s, 6)
        strake(b, L, W, H, y0 + L * 0.2, y1 - L * 0.3, s, spikes=7)
    keel_spikes(b, y0 + L * 0.22, y1 - L * 0.16, -H * 1.1, 7, H * 1.3, H * 0.1, spread=W * 0.25)
    return b.to_object(sharp_angle=30)


def skirmish_frigate(sid):
    """Skirmish-class missile frigate: a compact wedge hull with twin striped prow blades, four twin turrets, missile
    racks along the flanks, a short spire and two engine cylinders."""
    b = PB(sid)
    L, W, H = 1.2, 0.26, 0.17
    y0, y1 = -L / 2, L / 2
    prof = [(0.0, 0.9, 0.95, 0.0), (0.08, 1.0, 1.0, 0.0), (0.45, 1.0, 1.0, 0.0), (0.7, 0.8, 0.9, -0.03), (0.86, 0.5, 0.72, -0.08)]
    b.hull(L, W, H, prof, V((0, y1 + L * 0.02, -H * 0.3)), paint_fn=stripe_fn(L, 0.5, 0.22, 1.2, 0.4, sid, H=H))
    top = superstructure(b, y0 + L * 0.12, y0 + L * 0.5, W * 0.55, H * 0.95, H * 0.42, tiers=1)
    b.spire(V((0, y0 + L * 0.22, top)), W * 0.36, H * 1.6, tiers=2, crown=4)
    for s in (-1, 1):
        b.blade(V((s * W * 0.7, y1 - L * 0.36, -H * 0.1)), V((s * W * 0.32, y1 + L * 0.26, -H * 0.25)), W * 0.3, W * 0.06)
        b.turret(V((s * W * 0.55, y0 + L * 0.62, H * 0.88)), W * 0.22, barrels=2, yaw=-s * 10, length=2.0)
        b.turret(V((s * W * 0.62, y0 + L * 0.36, H * 0.85)), W * 0.2, barrels=2, yaw=-s * 25, length=1.8)
        # Missile racks: boxes of glowing tubes on the flanks.
        for k in range(2):
            c = V((s * W * 1.12, y0 + L * (0.4 + 0.16 * k), H * 0.05))
            b.cbox(c, (W * 0.12, L * 0.06, H * 0.32), cls=HULL)
            for i in range(3):
                for j in range(2):
                    p = c + V((s * W * 0.12, (i - 1) * L * 0.035, (j - 0.5) * H * 0.3))
                    b.cyl(p, p + V((s * W * 0.03, 0, 0)), H * 0.06, sides=6, cls=GLASS, emis=1.2)
        b.engine(y0 + L * 0.18, y0 - L * 0.12, s * W * 0.5, -H * 0.05, H * 0.45, rings=3)
        flank_details(b, L, W, H, y0 + L * 0.2, y1 - L * 0.3, s, ribs=6, pipes=False)
        b.hanging_plates(y0 + L * 0.3, y1 - L * 0.3, W * 0.98, -H * 0.55, 5, H * 0.8, L * 0.05, s)
    keel_spikes(b, y0 + L * 0.2, y1 - L * 0.2, -H * 1.1, 8, H * 1.1, H * 0.08, spread=W * 0.3)
    b.spikes([V((0, y0 + L * (0.55 + 0.05 * i), H * 1.0)) for i in range(3)], V((0, 0.4, 1)), H * 0.55, H * 0.05)
    return b.to_object(sharp_angle=30)


def rift_interceptor(sid):
    """Rift-class void interceptor: a narrow fuselage with a red-glass canopy, twin forked scythe prongs, swept wing plates
    with spikes, two engine cylinders and twin underslung guns."""
    b = PB(sid)
    L, W, H = 0.95, 0.12, 0.09
    y0, y1 = -L / 2, L / 2
    prof = [(0.0, 0.9, 0.9, 0.0), (0.1, 1.0, 1.0, 0.0), (0.5, 1.0, 1.0, 0.0), (0.75, 0.7, 0.8, -0.05), (0.9, 0.38, 0.55, -0.1)]
    b.hull(L, W, H, prof, V((0, y1 + L * 0.06, -H * 0.2)), paint_fn=stripe_fn(L, 0.5, 0.25, 1.0, 0.4, sid, H=H), keel=0.2)
    # Canopy.
    b.ellipsoid(V((0, y0 + L * 0.66, H * 0.92)), (W * 0.36, L * 0.1, H * 0.34), cls=PAINT, emis=0.0, u=8, v=6)   # crimson canopy, unlit
    for k in range(3):
        b.cbox(V((0, y0 + L * (0.6 + 0.05 * k), H * 1.2)), (W * 0.02, L * 0.004, H * 0.06), bev=W * 0.005, cls=TRIM)
    b.cbox(V((0, y0 + L * 0.5, H * 1.0)), (W * 0.5, L * 0.08, H * 0.32), cls=HULL, taper=0.8)
    # Forked prongs, the concept's twin scythes.
    for s in (-1, 1):
        b.blade(V((s * W * 0.9, y0 + L * 0.45, -H * 0.1)), V((s * W * 0.7, y1 + L * 0.38, -H * 0.2)), W * 0.55, W * 0.1, curve=-H * 0.15)
        # Swept wing plate with an emblem panel and trailing spikes.
        wing = [V((s * W * 0.9, y0 + L * 0.2, 0)), V((s * W * 3.0, y0 + L * 0.05, -H * 0.1)), V((s * W * 1.6, y0 + L * 0.42, 0))]
        thick = V((0, 0, H * 0.08))
        b.loft([[wing[0], wing[1], wing[2]], [wing[0] + thick, wing[1] + thick * 0.4, wing[2] + thick]], PAINT, 0.0)
        b.spikes([wing[0].lerp(wing[1], t) for t in (0.3, 0.55, 0.8)], V((s * 0.6, -0.8, 0)), L * 0.12, H * 0.06)
        b.cone(wing[1], V((s * 0.5, -1, 0)), L * 0.2, H * 0.08, cls=PAINT)
        b.spikes([V((s * W * 0.7, y0 + L * (0.55 + 0.06 * i), H * 0.6)) for i in range(3)], V((s * 0.4, 0.3, 1)), L * 0.09, H * 0.05)
        b.engine(y0 + L * 0.2, y0 - L * 0.1, s * W * 0.95, H * 0.15, H * 0.5, rings=3, collar_spikes=4)
        g = V((s * W * 0.55, y0 + L * 0.55, -H * 0.85))
        b.cyl(g, g + V((0, L * 0.38, 0)), H * 0.12, sides=8, cls=TRIM)
        b.cyl(g + V((0, L * 0.38, 0)), g + V((0, L * 0.4, 0)), H * 0.08, sides=8, cls=GLASS, emis=1.5)
    keel_spikes(b, y0 + L * 0.25, y1 - L * 0.2, -H * 1.05, 5, H * 1.4, H * 0.1)
    return b.to_object(sharp_angle=30)



# =================================================================================================== concept-fidelity kit
# Forms read off the concept sheets: deep slab hulls with a vertical knife stem, stepped armoured "carriage" blocks with
# window grids, drum engines with spiked rims, long side guns, and a beard of long crimson spikes under the keel.

def slab_section(w, top, keel):
    """Deep warship section: a narrow deck, a near-vertical wall, a rounded bilge and a keel."""
    return [V((0, 0, top)), V((w * 0.72, 0, top)), V((w, 0, top * 0.72)), V((w * 1.02, 0, top * 0.1)), V((w, 0, -keel * 0.5)),
            V((w * 0.86, 0, -keel * 0.82)), V((w * 0.42, 0, -keel * 0.97)), V((0, 0, -keel)),
            V((-w * 0.42, 0, -keel * 0.97)), V((-w * 0.86, 0, -keel * 0.82)), V((-w, 0, -keel * 0.5)), V((-w * 1.02, 0, top * 0.1)),
            V((-w, 0, top * 0.72)), V((-w * 0.72, 0, top))]


def slab_hull(b, L, W, top, keel, prof, paint_fn, lean=0.0, step=0.14):
    """prof: [(t, width, top, keel scales)] stern (t=0) to stem (t=1). The last ring is the knife stem (closed, not
    pointed); lean moves the stem's top forward. Dense rings and split edges so the plating reads as panels."""
    n = max(len(prof), int(L / (W * step)))
    rings = []
    for i in range(n + 1):
        t = i / n
        k = max(j for j in range(len(prof)) if prof[j][0] <= t + 1e-9)
        a, c = prof[k], prof[min(k + 1, len(prof) - 1)]
        f = 0.0 if c[0] == a[0] else (t - a[0]) / (c[0] - a[0])
        sw, st, sk = [a[m] + (c[m] - a[m]) * f for m in (1, 2, 3)]
        sec = slab_section(W * sw, top * st, keel * sk)
        dense = []
        for m in range(len(sec)):
            p0, p1 = sec[m], sec[(m + 1) % len(sec)]
            dense += [p0, p0.lerp(p1, 0.5)]
        y = -L / 2 + L * t
        rings.append([p + V((0, y + lean * max(0.0, p.z) * t ** 4, 0)) for p in dense])
    faces = b.loft(rings, HULL, 0.0, cap_start=True, cap_end=True)
    b.plate([f for f in faces if len(f.verts) == 4], W * 0.035, W * 0.026, paint_fn)
    return faces


def carriage_row(b, y0, y1, n, x, z, w, h, side=0, red=0.3, windows=True, emblem=True):
    """A row of stepped armoured blocks with gaps, lit window grids and (some) crimson emblem panels."""
    span = (y1 - y0) / n
    for i in range(n):
        hy = span * (0.38 + 0.08 * b.rnd.random())
        hh = h * (0.75 + 0.5 * b.rnd.random())
        c = V((x, y0 + span * (i + 0.5), z + hh))
        painted = b.rnd.random() < red
        b.cbox(c, (w, hy, hh), bev=min(w, hy, hh) * 0.2, cls=PAINT if painted else HULL, taper=0.92)
        b.cbox(c + V((0, 0, hh * 1.02)), (w * 0.8, hy * 0.9, hh * 0.08), bev=hh * 0.03, cls=TRIM)
        for sgn in ((side,) if side else (-1, 1)):
            face_x = x + sgn * w * 0.98
            if windows:
                rows = 2 if hh > h * 0.9 else 1
                for r in range(rows):
                    zz = c.z + hh * (0.35 - 0.5 * r)
                    b.windows(V((face_x, c.y - hy * 0.75, zz)), V((face_x, c.y + hy * 0.75, zz)), V((sgn, 0, 0)),
                              max(2, int(hy / (w * 0.22))), w * 0.22, hh * 0.3, emis=1.0)
            if emblem and painted and b.rnd.random() < 0.6:
                b.cbox(V((face_x + sgn * w * 0.04, c.y, c.z)), (w * 0.03, hy * 0.35, hh * 0.45), bev=w * 0.01, cls=HULL)
                b.cbox(V((face_x + sgn * w * 0.07, c.y, c.z)), (w * 0.02, hy * 0.18, hh * 0.25), bev=w * 0.006, cls=TRIM)
        b.cbox(V((x, c.y + hy + span * 0.06, z + h * 0.5)), (w * 1.04, span * 0.03, h * 0.5), bev=w * 0.05, cls=TRIM)


def drum_engine(b, y_front, y_back, x, z, r, spikes=14):
    """Drum engine (concept sterns): a riveted drum with rib collars and glowing bands, a spiked rim crown and a glowing
    ringed exhaust face."""
    L = y_front - y_back
    b.tube([V((x, y_front + L * 0.12, z)), V((x, y_front, z))], [r * 0.5, r * 0.9], sides=20, cls=HULL, point_end=False)
    y = y_front
    for k in range(3):
        b.cyl(V((x, y, z)), V((x, y - L * 0.1, z)), r * 1.04, sides=20, cls=TRIM)
        b.cyl(V((x, y - L * 0.1, z)), V((x, y - L * 0.27, z)), r * 0.92, sides=20, cls=GLASS, emis=1.6)
        y -= L * 0.27
    b.cyl(V((x, y, z)), V((x, y_back, z)), r * 1.12, sides=20, cls=HULL)
    b.cyl(V((x, y_back + L * 0.02, z)), V((x, y_back - L * 0.04, z)), r * 1.2, sides=20, cls=TRIM)
    for k, rr in enumerate((1.0, 0.72, 0.44)):
        b.cyl(V((x, y_back - L * 0.04, z)), V((x, y_back - L * (0.05 + 0.01 * k), z)), r * rr, sides=20,
              cls=GLASS if k != 1 else TRIM, emis=2.6 if k != 1 else 0.0)
    for k in range(spikes):
        a = 2 * math.pi * k / spikes
        d = V((math.cos(a), -0.25, math.sin(a)))
        b.cone(V((x, y_back - L * 0.01, z)) + V((math.cos(a), 0, math.sin(a))) * r * 1.15, d, r * 0.55, r * 0.07, sides=4, cls=TRIM)
    for k in range(8):
        a = 2 * math.pi * k / 8 + 0.2
        off = V((math.cos(a), 0, math.sin(a))) * r * 1.0
        b.cbox(V((x, y_front - L * 0.45, z)) + off, (r * 0.1, L * 0.45, r * 0.06), rot=rot_to(Y, off.normalized()), bev=r * 0.03, cls=TRIM)


def side_gun(b, y0, y1, x, z, r):
    """Long gun cylinder along the lower flank (concept bows), banded, with a glowing muzzle."""
    b.cyl(V((x, y0, z)), V((x, y1, z)), r, sides=12, cls=HULL)
    for t in (0.1, 0.3, 0.5, 0.7, 0.88):
        y = y0 + (y1 - y0) * t
        b.cyl(V((x, y, z)), V((x, y + (y1 - y0) * 0.04, z)), r * 1.18, sides=12, cls=TRIM)
    b.cyl(V((x, y1, z)), V((x, y1 + (y1 - y0) * 0.08, z)), r * 0.7, sides=12, cls=TRIM)
    b.cyl(V((x, y1 + (y1 - y0) * 0.08, z)), V((x, y1 + (y1 - y0) * 0.09, z)), r * 0.5, sides=12, cls=GLASS, emis=2.4)
    for k in range(4):
        b.cbox(V((x, y0 + (y1 - y0) * (0.2 + 0.2 * k), z + r * 1.1)), (r * 0.4, (y1 - y0) * 0.03, r * 0.25), bev=r * 0.08, cls=TRIM)


def stalactites(b, y0, y1, zfun, n, short, long, bow_bias=1.5, width=0.5):
    """Beard of long crimson spikes hanging under the keel, longest toward the bow."""
    for i in range(n):
        t = (i + b.rnd.random() * 0.8) / n
        y = y0 + (y1 - y0) * t
        x = b.rnd.uniform(-1, 1) * width
        ln = short + (long - short) * (t ** bow_bias) * (0.55 + 0.45 * b.rnd.random())
        base = V((x * 0.06, y, zfun(y) + 0.01))
        d = V((x * 0.12, -0.18 - 0.1 * b.rnd.random(), -1))
        b.tube([base, base + d.normalized() * ln * 0.72], [ln * 0.07, ln * 0.03], sides=4,
               cls=PAINT if b.rnd.random() < 0.75 else TRIM, point_end=True, up=X)


def edge_spikes(b, pts, d, ln, r):
    for p in pts:
        b.cone(p, (d + V((b.rnd.uniform(-.1, .1), b.rnd.uniform(-.1, .1), b.rnd.uniform(-.05, .1)))).normalized(),
               ln * (0.7 + 0.6 * b.rnd.random()), r, sides=4, cls=TRIM)


def bow_stripes(L, bow_t, period, seed, red_chance=0.35, zone=0.1, top=0.25):
    """Crimson bow armour with broad black diagonal bands; aft, whole zones painted."""
    zones = {}
    rnd = random.Random(seed)

    def fn(c, cls):
        t = (c.y + L / 2) / L
        if t > bow_t:
            v = (c.y - c.z * 0.9) / (L * period)
            return PAINT if (v % 1.0) < 0.6 else HULL
        band = 2 if c.z > top * 0.4 else 1 if c.z > -top * 0.6 else 0
        key = (int(t / zone), c.x > 0, band)
        if key not in zones:
            zones[key] = rnd.random() < red_chance
        return PAINT if zones[key] else cls
    return fn


def revenant2(sid):
    """Revenant-class command dreadnought after the concept: a deep slab hull ending in a tall knife stem with crimson
    and black striped bow armour; stepped carriage blocks along deck and flanks; a cathedral of spires a third of the way
    from the stern; three forward turrets; two huge stacked drum engines with spiked rims; long side guns; a beard of
    crimson spikes under the keel."""
    b = PB(sid)
    L, W, TOP, KEEL = 2.2, 0.2, 0.24, 0.3
    y0, y1 = -L / 2, L / 2
    prof = [(0.0, 0.92, 0.9, 0.85), (0.05, 1.0, 1.0, 1.0), (0.62, 1.0, 1.0, 1.0), (0.8, 0.78, 1.05, 1.0), (0.93, 0.38, 1.1, 0.98),
            (1.0, 0.07, 1.14, 0.95)]
    slab_hull(b, L, W, TOP, KEEL, prof, bow_stripes(L, 0.7, 0.1, sid, top=TOP), lean=0.12)
    # Deck: carriages fore and aft of the cathedral, turrets forward.
    carriage_row(b, y0 + L * 0.04, y0 + L * 0.24, 3, 0, TOP, W * 0.62, 0.05, red=0.3)
    carriage_row(b, y0 + L * 0.46, y0 + L * 0.56, 1, 0, TOP, W * 0.6, 0.05, red=0.5)
    for k, (t, zz) in enumerate(((0.6, 0.11), (0.7, 0.06), (0.79, 0.02))):
        b.cbox(V((0, y0 + L * t, TOP + zz * 0.5)), (W * 0.55, L * 0.035, zz * 0.5 + 0.005), cls=HULL, taper=0.9)
        b.turret(V((0, y0 + L * t, TOP + zz)), W * 0.42, barrels=3 if k < 2 else 2, length=2.3)
    # Cathedral.
    cz = TOP
    b.cbox(V((0, y0 + L * 0.35, cz + 0.07)), (W * 0.85, L * 0.1, 0.07), cls=HULL, taper=0.88)
    b.windows(V((W * 0.86, y0 + L * 0.27, cz + 0.08)), V((W * 0.86, y0 + L * 0.43, cz + 0.08)), X, 8, 0.025, 0.04)
    b.windows(V((-W * 0.86, y0 + L * 0.27, cz + 0.08)), V((-W * 0.86, y0 + L * 0.43, cz + 0.08)), -X, 8, 0.025, 0.04)
    b.spire(V((0, y0 + L * 0.35, cz + 0.14)), W * 0.75, 0.62, tiers=5, crown=6)
    for sx, ty, h, w in ((-1, 0.26, 0.38, 0.32), (1, 0.27, 0.42, 0.3), (-1, 0.44, 0.34, 0.28), (1, 0.43, 0.3, 0.28),
                         (0, 0.22, 0.3, 0.3), (0, 0.48, 0.26, 0.26)):
        b.spire(V((sx * W * 0.55, y0 + L * ty, cz + 0.1)), W * w, h, tiers=3, crown=4)
    edge_spikes(b, [V((s * W * 0.7, y0 + L * (0.08 + 0.06 * i), TOP)) for s in (-1, 1) for i in range(14)], V((0, 0.2, 1)), 0.09, 0.008)
    # Flank galleries: lower carriage rows and casemates, side guns near the bow.
    for s in (-1, 1):
        carriage_row(b, y0 + L * 0.1, y0 + L * 0.62, 7, s * W * 1.08, -0.05, W * 0.12, 0.05, side=s, red=0.35)
        broadside(b, L, W, 0.24, y0 + L * 0.12, y0 + L * 0.6, s, 9, z=-0.6)
        side_gun(b, y0 + L * 0.5, y0 + L * 0.86, s * W * 0.98, -0.17, 0.045)
        b.pipe([V((s * W * 1.05, y0 + L * (0.08 + 0.08 * i), 0.02)) for i in range(9)], 0.008)
        b.hanging_plates(y0 + L * 0.2, y0 + L * 0.7, W * 0.9, -KEEL * 0.6, 5, 0.14, L * 0.04, s)
        edge_spikes(b, [V((s * W * 0.8, y0 + L * (0.72 + 0.04 * i), TOP * 1.05 + 0.02 * i)) for i in range(6)], V((s * 0.5, 0.6, 0.6)), 0.08, 0.009)
    # Engines: two huge drums stacked at the stern.
    drum_engine(b, y0 + L * 0.06, y0 - L * 0.13, 0, 0.16, 0.19)
    drum_engine(b, y0 + L * 0.06, y0 - L * 0.13, 0, -0.24, 0.17)
    b.cbox(V((0, y0 + L * 0.02, -0.04)), (W * 0.8, L * 0.05, 0.3), cls=TRIM, taper=0.95)
    # Beard of spikes, longest under the bow.
    stalactites(b, y0 + L * 0.15, y1 - L * 0.04, lambda y: -KEEL * 0.95, 30, 0.08, 0.5, bow_bias=1.6)
    greebles(b, y0 + L * 0.5, y0 + L * 0.58, W * 0.5, TOP, 6, 0.03)
    return b.to_object(sharp_angle=30)


def prof_at(prof, t):
    k = max(j for j in range(len(prof)) if prof[j][0] <= t + 1e-9)
    a, c = prof[k], prof[min(k + 1, len(prof) - 1)]
    f = 0.0 if c[0] == a[0] else (t - a[0]) / (c[0] - a[0])
    return [a[m] + (c[m] - a[m]) * f for m in (1, 2, 3)]


def bow_armor(b, L, W, top, keel, prof, lean, t0, slope, bands, side, out=0.012, thick=0.01, first_red=True, z_lo=-0.5, z_hi=0.72):
    """Armour plates over one side of the bow wall, in diagonal bands whose edges are straight lines in the side view
    (y - slope*z = const), alternating crimson and black, following the bow's taper exactly. Each plate is a raised grid
    with a skirt down to the hull, so the bands read as separate plates with real edges."""
    bm = b.bm
    y_start = -L / 2 + L * t0
    y_end = L / 2

    def wall(y, z):
        t = min(1.0, max(0.0, (y + L / 2) / L))
        sw, st, sk = prof_at(prof, t)
        zc = max(-keel * sk * 0.5, min(top * st * 0.72, z))
        yy = y + lean * max(0.0, zc) * t ** 4
        return V((side * (W * sw * 1.0 + out), yy, zc))

    z0, z1 = keel * z_lo, top * z_hi
    c0 = y_start - slope * z1
    c1 = y_end - slope * z0
    width = (c1 - c0) / bands
    for k in range(bands):
        cls = PAINT if (k % 2 == 0) == first_red else HULL
        ca, cb = c0 + width * k, c0 + width * (k + 1)
        nu, nz = 4, 7
        grid = []
        for i in range(nu + 1):
            row = []
            cu = ca + (cb - ca) * i / nu
            for j in range(nz + 1):
                z = z0 + (z1 - z0) * j / nz
                y = cu + slope * z
                y = min(y_end - 0.004, max(y_start, y))
                row.append(wall(y, z))
            grid.append(row)
        verts = [[bm.verts.new(p) for p in row] for row in grid]
        back = [[bm.verts.new(p - V((side * thick, 0, 0))) for p in row] for row in grid]
        faces = []
        for i in range(nu):
            for j in range(nz):
                q = (verts[i][j], verts[i + 1][j], verts[i + 1][j + 1], verts[i][j + 1])
                if len({v.co.to_tuple(5) for v in q}) == 4:
                    faces.append(bm.faces.new(q if side > 0 else q[::-1]))
        rim = [(verts[i][0], verts[i + 1][0], back[i + 1][0], back[i][0]) for i in range(nu)]
        rim += [(verts[i + 1][nz], verts[i][nz], back[i][nz], back[i + 1][nz]) for i in range(nu)]
        rim += [(verts[0][j + 1], verts[0][j], back[0][j], back[0][j + 1]) for j in range(nz)]
        rim += [(verts[nu][j], verts[nu][j + 1], back[nu][j + 1], back[nu][j]) for j in range(nz)]
        for q in rim:
            if len({v.co.to_tuple(5) for v in q}) == 4:
                try:
                    faces.append(bm.faces.new(q if side > 0 else q[::-1]))
                except ValueError:
                    pass
        b._tag(faces, cls, 0.0)
        # Rivet row along the band's leading edge.
        for j in range(1, nz, 2):
            p = grid[0][j] + V((side * 0.004, 0, 0))
            b.box(p, (0.004, 0.006, 0.006), cls=TRIM)


def girders(b, y0, y1, n, x, z0, z1, side, w=0.012):
    """Vertical girders up a flank wall, each with a cross brace and bolts."""
    for i in range(n):
        y = y0 + (y1 - y0) * (i + 0.5) / n
        c = V((x, y, (z0 + z1) / 2))
        b.cbox(c, (w, w * 0.8, (z1 - z0) / 2), bev=w * 0.3, cls=TRIM)
        b.cbox(c + V((side * w * 0.6, 0, (z1 - z0) * 0.3)), (w * 0.4, w * 1.6, w * 0.6), bev=w * 0.15, cls=TRIM)


def pipe_bundle(b, y0, y1, x, z, side, n=3, r=0.007):
    for k in range(n):
        pts = [V((x + side * r * 2.2 * k, y0 + (y1 - y0) * i / 10, z - r * 2.4 * k)) for i in range(11)]
        b.pipe(pts, r, rings_every=2)


def drum_engine2(b, y_front, y_back, x, z, r, spikes=16):
    """Drum engine after the concept: dark ribbed drum with narrow glowing slits between many ribs, banded straps, a
    spiked crown on the rear rim and a glowing ringed exhaust face."""
    L = y_front - y_back
    b.tube([V((x, y_front + L * 0.14, z)), V((x, y_front, z))], [r * 0.45, r * 0.92], sides=24, cls=HULL, point_end=False)
    ribs = 7
    y = y_front
    for k in range(ribs):
        seg = L * 0.8 / ribs
        b.cyl(V((x, y, z)), V((x, y - seg * 0.62, z)), r * 1.04, sides=24, cls=TRIM if k % 2 else HULL)
        b.cyl(V((x, y - seg * 0.62, z)), V((x, y - seg, z)), r * 0.93, sides=24, cls=GLASS, emis=1.7)
        y -= seg
    b.cyl(V((x, y, z)), V((x, y_back, z)), r * 1.12, sides=24, cls=HULL)
    b.cyl(V((x, y_back + L * 0.02, z)), V((x, y_back - L * 0.035, z)), r * 1.2, sides=24, cls=TRIM)
    for k, rr in enumerate((1.02, 0.78, 0.55, 0.3)):
        b.cyl(V((x, y_back - L * 0.035, z)), V((x, y_back - L * (0.04 + 0.008 * k), z)), r * rr, sides=24,
              cls=GLASS if k % 2 == 0 else TRIM, emis=2.6 if k % 2 == 0 else 0.0)
    for k in range(spikes):
        a = 2 * math.pi * k / spikes
        d = V((math.cos(a), -0.35, math.sin(a)))
        b.cone(V((x, y_back - L * 0.015, z)) + V((math.cos(a), 0, math.sin(a))) * r * 1.16, d, r * (0.5 + 0.15 * (k % 2)), r * 0.07,
               sides=4, cls=TRIM)
    for k in range(10):
        a = 2 * math.pi * k / 10 + 0.15
        off = V((math.cos(a), 0, math.sin(a))) * r * 1.02
        b.cbox(V((x, y_front - L * 0.42, z)) + off, (r * 0.07, L * 0.42, r * 0.04), rot=rot_to(Y, off.normalized()), bev=r * 0.02, cls=TRIM)


def revenant3(sid):
    """Revenant-class command dreadnought after the concept (third pass): deep slab hull with a leaning knife stem and
    diagonal crimson/black bow armour plates; an upper deck tier; stepped carriage blocks on deck and in two galleries
    down each flank with girders and pipe bundles; the cathedral of spires; three forward turrets; two drum engines with
    spiked crowns; long side guns; a beard of crimson spikes under the keel."""
    b = PB(sid)
    L, W, TOP, KEEL = 2.2, 0.2, 0.28, 0.36
    y0, y1 = -L / 2, L / 2
    prof = [(0.0, 0.92, 0.9, 0.85), (0.05, 1.0, 1.0, 1.0), (0.62, 1.0, 1.0, 1.0), (0.8, 0.78, 1.05, 1.0), (0.93, 0.38, 1.1, 0.98),
            (1.0, 0.07, 1.14, 0.95)]
    lean = 0.12
    hull_paint = bow_stripes(L, 2.0, 0.1, sid, red_chance=0.3, top=TOP)   # bow plates carry the stripes; hull zones only
    slab_hull(b, L, W, TOP, KEEL, prof, hull_paint, lean=lean)
    for s in (-1, 1):
        bow_armor(b, L, W, TOP, KEEL, prof, lean, 0.64, 0.55, 5, s)
    # Upper deck tier along the middle of the ship.
    tier = b.cbox(V((0, y0 + L * 0.38, TOP + 0.035)), (W * 0.78, L * 0.3, 0.035), bev=0.012, cls=HULL, taper=0.95)
    b.bm.normal_update()
    b.plate([f for f in tier if abs(f.normal.z) < 0.5 and f.calc_area() > 0.002], 0.006, 0.004, random_red(0.35, sid + "tier"))
    T2 = TOP + 0.07
    carriage_row(b, y0 + L * 0.04, y0 + L * 0.22, 3, 0, TOP, W * 0.62, 0.05, red=0.35)
    carriage_row(b, y0 + L * 0.47, y0 + L * 0.56, 1, 0, T2, W * 0.6, 0.04, red=0.6)
    for k, (t, zz) in enumerate(((0.6, 0.1), (0.7, 0.055), (0.79, 0.02))):
        base = T2 if t < 0.66 else TOP
        b.cbox(V((0, y0 + L * t, base + zz * 0.5)), (W * 0.55, L * 0.035, zz * 0.5 + 0.005), cls=HULL, taper=0.9)
        b.turret(V((0, y0 + L * t, base + zz)), W * 0.42, barrels=3 if k < 2 else 2, length=2.3)
    # Cathedral.
    b.cbox(V((0, y0 + L * 0.35, T2 + 0.06)), (W * 0.85, L * 0.1, 0.06), cls=HULL, taper=0.88)
    for s in (-1, 1):
        b.windows(V((s * W * 0.86, y0 + L * 0.27, T2 + 0.07)), V((s * W * 0.86, y0 + L * 0.43, T2 + 0.07)), V((s, 0, 0)), 10, 0.018, 0.03)
    b.spire(V((0, y0 + L * 0.35, T2 + 0.12)), W * 0.75, 0.62, tiers=5, crown=6)
    for sx, ty, h, w in ((-1, 0.26, 0.38, 0.32), (1, 0.27, 0.42, 0.3), (-1, 0.44, 0.34, 0.28), (1, 0.43, 0.3, 0.28),
                         (0, 0.22, 0.3, 0.3), (0, 0.48, 0.26, 0.26)):
        b.spire(V((sx * W * 0.55, y0 + L * ty, T2 + 0.08)), W * w, h, tiers=3, crown=4)
    edge_spikes(b, [V((s * W * 0.72, y0 + L * (0.06 + 0.05 * i), TOP)) for s in (-1, 1) for i in range(17)], V((0, 0.2, 1)), 0.08, 0.007)
    # Flanks: two galleries of carriages, girders and pipe bundles, casemates, a side gun near the bow.
    for s in (-1, 1):
        carriage_row(b, y0 + L * 0.08, y0 + L * 0.64, 8, s * W * 1.07, 0.02, W * 0.1, 0.045, side=s, red=0.35)
        carriage_row(b, y0 + L * 0.12, y0 + L * 0.6, 6, s * W * 1.0, -0.2, W * 0.1, 0.05, side=s, red=0.25)
        girders(b, y0 + L * 0.08, y0 + L * 0.66, 12, s * W * 1.03, -0.12, 0.0, s)
        pipe_bundle(b, y0 + L * 0.06, y0 + L * 0.66, s * W * 1.04, -0.02, s)
        broadside(b, L, W, 0.24, y0 + L * 0.14, y0 + L * 0.6, s, 8, z=-0.05)
        side_gun(b, y0 + L * 0.48, y0 + L * 0.84, s * W * 0.97, -0.19, 0.045)
        b.hanging_plates(y0 + L * 0.2, y0 + L * 0.7, W * 0.9, -KEEL * 0.6, 5, 0.14, L * 0.04, s)
        edge_spikes(b, [V((s * W * 0.8, y0 + L * (0.72 + 0.04 * i), TOP * 1.05 + 0.02 * i)) for i in range(6)], V((s * 0.5, 0.6, 0.6)), 0.08, 0.009)
    # Engines.
    drum_engine2(b, y0 + L * 0.06, y0 - L * 0.13, 0, 0.16, 0.19)
    drum_engine2(b, y0 + L * 0.06, y0 - L * 0.13, 0, -0.24, 0.17)
    b.cbox(V((0, y0 + L * 0.02, -0.04)), (W * 0.8, L * 0.05, 0.3), cls=TRIM, taper=0.95)
    stalactites(b, y0 + L * 0.15, y1 - L * 0.04, lambda y: -KEEL * 0.95, 34, 0.08, 0.5, bow_bias=1.6)
    greebles(b, y0 + L * 0.5, y0 + L * 0.58, W * 0.5, T2, 6, 0.03)
    return b.to_object(sharp_angle=30)


def preview_material2():
    """Preview that follows the game's RR/Ship shader for the Pygmaei: classes from Col.R (iron, crimson paint, trim,
    glow), the weathering pass (blotchy grime, paint chipped to iron, rivet heads), AO from Col.B, emission from Col.G."""
    mat = bpy.data.materials.get("RR_PygPreview2")
    if mat:
        return mat
    mat = bpy.data.materials.new("RR_PygPreview2")
    mat.use_nodes = True
    nt = mat.node_tree
    N, Lk = nt.nodes, nt.links
    bsdf = N.get("Principled BSDF")
    attr = N.new("ShaderNodeVertexColor"); attr.layer_name = "Col"
    sep = N.new("ShaderNodeSeparateColor"); Lk.new(attr.outputs["Color"], sep.inputs["Color"])
    tex = N.new("ShaderNodeTexCoord")
    mp = N.new("ShaderNodeMapping"); mp.inputs["Scale"].default_value = (7, 7, 7); Lk.new(tex.outputs["Object"], mp.inputs["Vector"])

    def math(op, a=None, b=None, va=None, vb=None):
        n = N.new("ShaderNodeMath"); n.operation = op
        if a is not None: Lk.new(a, n.inputs[0])
        if va is not None: n.inputs[0].default_value = va
        if b is not None: Lk.new(b, n.inputs[1])
        if vb is not None: n.inputs[1].default_value = vb
        return n.outputs[0]

    def mix(fac, a, b):
        m = N.new("ShaderNodeMix"); m.data_type = 'RGBA'
        if isinstance(fac, float): m.inputs["Factor"].default_value = fac
        else: Lk.new(fac, m.inputs["Factor"])
        for sock, v in (("A", a), ("B", b)):
            if isinstance(v, tuple): m.inputs[sock].default_value = v
            else: Lk.new(v, m.inputs[sock])
        return m.outputs["Result"]

    cls = sep.outputs["Red"]
    glass = math('GREATER_THAN', cls, vb=0.75)
    trim = math('MULTIPLY', math('GREATER_THAN', cls, vb=0.4), math('SUBTRACT', va=1.0, b=glass))
    paint = math('MULTIPLY', math('GREATER_THAN', cls, vb=0.15), math('LESS_THAN', cls, vb=0.35))
    grime_n = N.new("ShaderNodeTexNoise"); grime_n.inputs["Scale"].default_value = 0.6; grime_n.inputs["Detail"].default_value = 4
    Lk.new(mp.outputs["Vector"], grime_n.inputs["Vector"])
    chip_n = N.new("ShaderNodeTexNoise"); chip_n.inputs["Scale"].default_value = 1.7; chip_n.inputs["Detail"].default_value = 5
    Lk.new(mp.outputs["Vector"], chip_n.inputs["Vector"])
    chip = math('GREATER_THAN', chip_n.outputs["Fac"], vb=0.64)
    vor = N.new("ShaderNodeTexVoronoi"); vor.inputs["Scale"].default_value = 3.2; Lk.new(mp.outputs["Vector"], vor.inputs["Vector"])
    rivet = math('LESS_THAN', vor.outputs["Distance"], vb=0.12)
    iron = (0.12, 0.11, 0.105, 1); red = (0.6, 0.05, 0.035, 1); trim_c = (0.3, 0.27, 0.24, 1); glow = (0.25, 0.055, 0.02, 1)
    paint_c = mix(chip, red, (0.42, 0.39, 0.38, 1))
    c = mix(paint, iron, paint_c)
    c = mix(trim, c, trim_c)
    c = mix(glass, c, glow)
    grime = math('SUBTRACT', va=1.0, b=math('MULTIPLY', math('MAXIMUM', math('SUBTRACT', va=1.2, b=math('MULTIPLY', grime_n.outputs["Fac"], vb=1.6)), vb=0.0), vb=0.3))
    mul = N.new("ShaderNodeMix"); mul.data_type = 'RGBA'; mul.blend_type = 'MULTIPLY'; mul.inputs["Factor"].default_value = 1.0
    Lk.new(c, mul.inputs["A"])
    gcol = N.new("ShaderNodeCombineColor"); Lk.new(grime, gcol.inputs[0]); Lk.new(grime, gcol.inputs[1]); Lk.new(grime, gcol.inputs[2])
    Lk.new(gcol.outputs[0], mul.inputs["B"])
    ao = math('MULTIPLY_ADD', sep.outputs["Blue"], vb=0.65); ao.node.inputs[2].default_value = 0.35
    mul2 = N.new("ShaderNodeMix"); mul2.data_type = 'RGBA'; mul2.blend_type = 'MULTIPLY'; mul2.inputs["Factor"].default_value = 1.0
    Lk.new(mul.outputs["Result"], mul2.inputs["A"])
    acol = N.new("ShaderNodeCombineColor"); Lk.new(ao, acol.inputs[0]); Lk.new(ao, acol.inputs[1]); Lk.new(ao, acol.inputs[2])
    Lk.new(acol.outputs[0], mul2.inputs["B"])
    add = N.new("ShaderNodeMix"); add.data_type = 'RGBA'; add.blend_type = 'ADD'
    Lk.new(math('MULTIPLY', rivet, vb=0.05), add.inputs["Factor"]); Lk.new(mul2.outputs["Result"], add.inputs["A"]); add.inputs["B"].default_value = (1, 1, 1, 1)
    Lk.new(add.outputs["Result"], bsdf.inputs["Base Color"])
    Lk.new(math('MULTIPLY', trim, vb=0.75), bsdf.inputs["Metallic"])
    rough = math('ADD', math('MULTIPLY', trim, vb=-0.2), vb=0.66)
    Lk.new(rough, bsdf.inputs["Roughness"])
    bsdf.inputs["Emission Color"].default_value = (1.0, 0.1, 0.03, 1)
    Lk.new(math('MULTIPLY', sep.outputs["Green"], vb=1.4), bsdf.inputs["Emission Strength"])
    return mat


def drum_engine3(b, y_front, x, z, r, spikes=18):
    """Short fat drum engine (concept sterns): diameter about equal to length, a dark riveted barrel with a few rib
    collars, a glowing band around the rear rim, a spiked crown and a dark exhaust face with glowing rings."""
    L = r * 1.9
    y_back = y_front - L
    b.tube([V((x, y_front + L * 0.12, z)), V((x, y_front, z))], [r * 0.5, r * 0.95], sides=28, cls=HULL, point_end=False)
    b.cyl(V((x, y_front, z)), V((x, y_back + L * 0.22, z)), r, sides=28, cls=HULL)
    for t in (0.08, 0.3, 0.52):
        y = y_front - L * t
        b.cyl(V((x, y, z)), V((x, y - L * 0.07, z)), r * 1.05, sides=28, cls=TRIM)
        b.cyl(V((x, y - L * 0.07, z)), V((x, y - L * 0.1, z)), r * 1.01, sides=28, cls=GLASS, emis=1.4)
    b.cyl(V((x, y_back + L * 0.22, z)), V((x, y_back + L * 0.08, z)), r * 1.1, sides=28, cls=GLASS, emis=2.2)
    b.cyl(V((x, y_back + L * 0.08, z)), V((x, y_back, z)), r * 1.16, sides=28, cls=TRIM)
    for k, rr in enumerate((1.0, 0.82, 0.62, 0.42, 0.22)):
        b.cyl(V((x, y_back, z)), V((x, y_back - L * (0.01 + 0.012 * k), z)), r * rr, sides=28,
              cls=GLASS if k % 2 == 1 else HULL, emis=2.4 if k % 2 == 1 else 0.0)
    for k in range(spikes):
        a = 2 * math.pi * k / spikes
        d = V((math.cos(a), -0.2, math.sin(a)))
        b.cone(V((x, y_back + L * 0.04, z)) + V((math.cos(a), 0, math.sin(a))) * r * 1.14, d, r * (0.42 + 0.18 * (k % 2)), r * 0.06,
               sides=4, cls=TRIM)
    for k in range(12):
        a = 2 * math.pi * k / 12 + 0.13
        off = V((math.cos(a), 0, math.sin(a))) * r * 1.02
        b.cbox(V((x, y_front - L * 0.4, z)) + off, (r * 0.05, L * 0.36, r * 0.035), rot=rot_to(Y, off.normalized()), bev=r * 0.015, cls=TRIM)
    return y_back


def needle_spire(b, base, w, h, tiers=4, needles=8, cupola=True):
    """Gothic tower after the concept: stacked chamfered tiers with lit window bands, a lit cupola gallery, a tall thin
    needle roof and a ring of thin needles around each tier."""
    z = base.z
    tw = w
    th = h * 0.45 / tiers
    for t in range(tiers):
        c = V((base.x, base.y, z + th * 0.5))
        b.cbox(c, (tw * 0.5, tw * 0.5, th * 0.5), bev=tw * 0.06, cls=HULL if t % 2 == 0 else TRIM, taper=0.9)
        for nrm in (X, -X, Y, -Y):
            p = c + nrm * (tw * 0.47)
            along = V((nrm.y, -nrm.x, 0))
            b.windows(p - along * tw * 0.3, p + along * tw * 0.3, nrm, 3 if tw > 0.08 else 2, tw * 0.07, th * 0.4, emis=1.2)
        for k in range(needles):
            a = 2 * math.pi * k / needles + t * 0.3
            p = V((base.x + math.cos(a) * tw * 0.5, base.y + math.sin(a) * tw * 0.5, z + th * 0.9))
            b.tube([p, p + V((math.cos(a) * 0.01, math.sin(a) * 0.01, th * (1.2 + 0.8 * (k % 3 == 0))))], [tw * 0.035, tw * 0.004],
                   sides=4, cls=TRIM, point_end=False)
        z += th
        tw *= 0.78
    if cupola:
        c = V((base.x, base.y, z + th * 0.4))
        b.cyl(c - V((0, 0, th * 0.4)), c + V((0, 0, th * 0.4)), tw * 0.55, sides=12, cls=GLASS, emis=1.6)
        b.cyl(c + V((0, 0, th * 0.4)), c + V((0, 0, th * 0.55)), tw * 0.65, sides=12, cls=TRIM)
        z += th * 0.55
    b.tube([V((base.x, base.y, z)), V((base.x, base.y, z + h * 0.5))], [tw * 0.42, tw * 0.03], sides=6, cls=HULL, point_end=False)
    for k in range(4):
        a = math.pi / 4 + k * math.pi / 2
        p = V((base.x + math.cos(a) * tw * 0.45, base.y + math.sin(a) * tw * 0.45, z))
        b.tube([p, p + V((0, 0, h * 0.22))], [tw * 0.08, tw * 0.006], sides=4, cls=TRIM, point_end=False)


def revenant4(sid):
    """Revenant-class command dreadnought after the concept (fourth pass): deeper slab hull, diagonal bow armour, an
    upper deck tier, three galleries of carriages down each flank reaching the bow, girders, pipe bundles, a forest of
    needle spires around a broad lit cathedral, three forward turrets, two short fat drum engines with glowing rims and
    spiked crowns, long side guns and a dense beard of crimson spikes, longest under the bow."""
    b = PB(sid)
    L, W, TOP, KEEL = 2.2, 0.2, 0.28, 0.36
    y0, y1 = -L / 2, L / 2
    prof = [(0.0, 0.92, 0.9, 0.85), (0.05, 1.0, 1.0, 1.0), (0.62, 1.0, 1.0, 1.0), (0.8, 0.78, 1.05, 1.0), (0.93, 0.38, 1.1, 0.98),
            (1.0, 0.07, 1.14, 0.95)]
    lean = 0.12
    slab_hull(b, L, W, TOP, KEEL, prof, bow_stripes(L, 2.0, 0.1, sid, red_chance=0.3, top=TOP), lean=lean)
    for s in (-1, 1):
        bow_armor(b, L, W, TOP, KEEL, prof, lean, 0.66, 0.55, 5, s)
    tier = b.cbox(V((0, y0 + L * 0.38, TOP + 0.035)), (W * 0.8, L * 0.31, 0.035), bev=0.012, cls=HULL, taper=0.95)
    b.bm.normal_update()
    b.plate([f for f in tier if abs(f.normal.z) < 0.5 and f.calc_area() > 0.002], 0.006, 0.004, random_red(0.35, sid + "tier"))
    T2 = TOP + 0.07
    carriage_row(b, y0 + L * 0.06, y0 + L * 0.2, 3, 0, T2, W * 0.66, 0.055, red=0.35)
    carriage_row(b, y0 + L * 0.47, y0 + L * 0.56, 1, 0, T2, W * 0.6, 0.045, red=0.6)
    for k, (t, zz) in enumerate(((0.6, 0.1), (0.7, 0.055), (0.79, 0.02))):
        base = T2 if t < 0.66 else TOP
        b.cbox(V((0, y0 + L * t, base + zz * 0.5)), (W * 0.55, L * 0.035, zz * 0.5 + 0.005), cls=HULL, taper=0.9)
        b.turret(V((0, y0 + L * t, base + zz)), W * 0.42, barrels=3 if k < 2 else 2, length=2.3)
    # Cathedral: broad lit main tower, a forest of needle spires around it.
    b.cbox(V((0, y0 + L * 0.34, T2 + 0.06)), (W * 0.88, L * 0.11, 0.06), cls=HULL, taper=0.88)
    for s in (-1, 1):
        b.windows(V((s * W * 0.88, y0 + L * 0.25, T2 + 0.07)), V((s * W * 0.88, y0 + L * 0.43, T2 + 0.07)), V((s, 0, 0)), 12, 0.016, 0.03)
    needle_spire(b, V((0, y0 + L * 0.34, T2 + 0.12)), W * 0.85, 0.7, tiers=4, needles=10)
    for sx, ty, h, w in ((-1, 0.25, 0.46, 0.34), (1, 0.27, 0.5, 0.32), (-1, 0.43, 0.4, 0.3), (1, 0.42, 0.36, 0.3),
                         (0, 0.2, 0.34, 0.3), (0, 0.48, 0.3, 0.26), (-1, 0.15, 0.26, 0.22), (1, 0.13, 0.24, 0.22)):
        needle_spire(b, V((sx * W * 0.58, y0 + L * ty, T2 + 0.08)), W * w, h, tiers=3, needles=6, cupola=w > 0.29)
    edge_spikes(b, [V((s * W * 0.74, y0 + L * (0.06 + 0.04 * i), TOP)) for s in (-1, 1) for i in range(22)], V((0, 0.2, 1)), 0.08, 0.006)
    for s in (-1, 1):
        carriage_row(b, y0 + L * 0.06, y0 + L * 0.66, 10, s * W * 1.07, 0.06, W * 0.1, 0.05, side=s, red=0.4)
        carriage_row(b, y0 + L * 0.08, y0 + L * 0.7, 9, s * W * 1.05, -0.12, W * 0.1, 0.05, side=s, red=0.3)
        carriage_row(b, y0 + L * 0.12, y0 + L * 0.62, 7, s * W * 1.0, -0.28, W * 0.1, 0.05, side=s, red=0.25)
        girders(b, y0 + L * 0.06, y0 + L * 0.7, 16, s * W * 1.03, -0.3, 0.1, s)
        pipe_bundle(b, y0 + L * 0.04, y0 + L * 0.7, s * W * 1.04, 0.03, s)
        pipe_bundle(b, y0 + L * 0.1, y0 + L * 0.66, s * W * 1.02, -0.16, s, n=2)
        side_gun(b, y0 + L * 0.46, y0 + L * 0.84, s * W * 0.97, -0.24, 0.05)
        b.hanging_plates(y0 + L * 0.2, y0 + L * 0.72, W * 0.9, -KEEL * 0.6, 6, 0.16, L * 0.04, s)
        edge_spikes(b, [V((s * W * 0.8, y0 + L * (0.72 + 0.035 * i), TOP * 1.05 + 0.02 * i)) for i in range(7)], V((s * 0.5, 0.6, 0.6)), 0.09, 0.009)
        girders(b, y0 + L * 0.9, y1 - L * 0.01, 3, s * W * 0.3, -KEEL * 0.4, TOP * 0.8, s, w=0.008)
    # Engines: two short fat drums stacked at the stern, a block between them.
    drum_engine3(b, y0 + L * 0.04, 0, 0.17, 0.2)
    drum_engine3(b, y0 + L * 0.04, 0, -0.27, 0.19)
    b.cbox(V((0, y0 + L * 0.02, -0.05)), (W * 0.85, L * 0.05, 0.34), cls=TRIM, taper=0.95)
    stalactites(b, y0 + L * 0.12, y1 - L * 0.02, lambda y: -KEEL * 0.95, 52, 0.08, 0.7, bow_bias=1.4)
    greebles(b, y0 + L * 0.5, y0 + L * 0.58, W * 0.5, T2, 6, 0.03)
    return b.to_object(sharp_angle=30)


def carriage_row2(b, y0, y1, n, x, z, w, h, side=0, red=0.3):
    """Irregular carriage blocks: varied depth, height and spacing, roof clutter (vents, small spikes, conduits), lit
    window grids, emblem panels on some crimson blocks, and gaps filled with dark machinery."""
    span = (y1 - y0) / n
    for i in range(n):
        hy = span * (0.3 + 0.18 * b.rnd.random())
        hh = h * (0.6 + 0.8 * b.rnd.random())
        ww = w * (0.7 + 0.6 * b.rnd.random())
        cy = y0 + span * (i + 0.5) + span * b.rnd.uniform(-0.08, 0.08)
        cz = z + hh + h * b.rnd.uniform(-0.15, 0.15)
        cx = x + side * (ww - w) * 0.5
        c = V((cx, cy, cz))
        painted = b.rnd.random() < red
        b.cbox(c, (ww, hy, hh), bev=min(ww, hy, hh) * 0.18, cls=PAINT if painted else HULL, taper=0.9 + 0.08 * b.rnd.random())
        top = cz + hh
        b.cbox(V((cx, cy, top)), (ww * 0.85, hy * 0.92, hh * 0.07), bev=hh * 0.02, cls=TRIM)
        for k in range(b.rnd.randint(1, 3)):
            p = V((cx + b.rnd.uniform(-ww, ww) * 0.6, cy + b.rnd.uniform(-hy, hy) * 0.7, top + hh * 0.05))
            kind = b.rnd.random()
            if kind < 0.4:
                b.cone(p, V((b.rnd.uniform(-.2, .2), b.rnd.uniform(-.2, .2), 1)), hh * (0.8 + b.rnd.random()), hh * 0.08, sides=4, cls=TRIM)
            elif kind < 0.7:
                b.cyl(p, p + V((0, 0, hh * 0.35)), hh * 0.18, sides=8, cls=TRIM)
            else:
                b.cbox(p + V((0, 0, hh * 0.12)), (ww * 0.25, hy * 0.3, hh * 0.12), bev=hh * 0.03, cls=HULL)
        for sgn in ((side,) if side else (-1, 1)):
            fx = cx + sgn * ww * 0.98
            rows = 1 + int(hh > h * 0.95) + int(hh > h * 1.25)
            for r in range(rows):
                zz = cz + hh * (0.5 - 0.95 * (r + 0.5) / rows)
                b.windows(V((fx, cy - hy * 0.72, zz)), V((fx, cy + hy * 0.72, zz)), V((sgn, 0, 0)), max(2, int(hy / (ww * 0.18))), ww * 0.18, hh * 0.22, emis=0.9)
            if painted and b.rnd.random() < 0.5:
                b.cbox(V((fx + sgn * ww * 0.04, cy, cz)), (ww * 0.03, hy * 0.32, hh * 0.42), bev=ww * 0.01, cls=HULL)
                b.cbox(V((fx + sgn * ww * 0.07, cy, cz)), (ww * 0.02, hy * 0.16, hh * 0.22), bev=ww * 0.006, cls=TRIM)
        if i < n - 1:
            gy = y0 + span * (i + 1)
            b.cbox(V((x, gy, z + h * 0.9)), (w * 0.8, span * 0.05, h * 0.9), bev=w * 0.05, cls=TRIM)
            b.cyl(V((x + side * w * 0.6, gy - span * 0.06, z + h * 0.4)), V((x + side * w * 0.6, gy + span * 0.06, z + h * 0.4)), h * 0.12, sides=8, cls=HULL)


def wall_clutter(b, y0, y1, x, z0, z1, side, n):
    """Small machinery scattered over a wall band: vents, junction boxes, short pipes, sensor stubs and spikes."""
    for i in range(n):
        y = b.rnd.uniform(y0, y1)
        z = b.rnd.uniform(z0, z1)
        s = 0.012 + 0.018 * b.rnd.random()
        p = V((x, y, z))
        k = b.rnd.random()
        if k < 0.35:
            b.cbox(p + V((side * s * 0.5, 0, 0)), (s * 0.5, s, s * 0.7), bev=s * 0.15, cls=TRIM if b.rnd.random() < 0.5 else HULL)
        elif k < 0.6:
            b.cyl(p, p + V((side * s * 1.2, 0, 0)), s * 0.35, sides=6, cls=TRIM)
        elif k < 0.8:
            b.cone(p, V((side, b.rnd.uniform(-.3, .5), b.rnd.uniform(-.2, .4))), s * 3.5, s * 0.22, sides=4, cls=TRIM)
        else:
            b.cbox(p + V((side * s * 0.3, 0, 0)), (s * 0.3, s * 1.4, s * 0.25), bev=s * 0.08, cls=GLASS, emis=0.9)


def revenant5(sid):
    """Fifth pass: the fourth pass with irregular carriage galleries, wall clutter between them, more deck-edge spikes."""
    b = PB(sid)
    L, W, TOP, KEEL = 2.2, 0.2, 0.36, 0.5
    y0, y1 = -L / 2, L / 2
    prof = [(0.0, 0.92, 0.9, 0.85), (0.05, 1.0, 1.0, 1.0), (0.62, 1.0, 1.0, 1.0), (0.8, 0.8, 1.12, 1.0), (0.93, 0.4, 1.3, 0.98),
            (1.0, 0.07, 1.38, 0.95)]
    lean = 0.12
    slab_hull(b, L, W, TOP, KEEL, prof, bow_stripes(L, 2.0, 0.1, sid, red_chance=0.3, top=TOP), lean=lean)
    for s in (-1, 1):
        bow_armor(b, L, W, TOP, KEEL, prof, lean, 0.66, 0.55, 5, s)
    tier = b.cbox(V((0, y0 + L * 0.38, TOP + 0.035)), (W * 0.8, L * 0.31, 0.035), bev=0.012, cls=HULL, taper=0.95)
    b.bm.normal_update()
    b.plate([f for f in tier if abs(f.normal.z) < 0.5 and f.calc_area() > 0.002], 0.006, 0.004, random_red(0.35, sid + "tier"))
    T2 = TOP + 0.07
    carriage_row2(b, y0 + L * 0.05, y0 + L * 0.2, 3, 0, T2, W * 0.66, 0.055, red=0.35)
    carriage_row2(b, y0 + L * 0.47, y0 + L * 0.56, 1, 0, T2, W * 0.6, 0.045, red=0.6)
    for k, (t, zz) in enumerate(((0.6, 0.1), (0.7, 0.055), (0.79, 0.02))):
        base = T2 if t < 0.66 else TOP
        b.cbox(V((0, y0 + L * t, base + zz * 0.5)), (W * 0.55, L * 0.035, zz * 0.5 + 0.005), cls=HULL, taper=0.9)
        b.turret(V((0, y0 + L * t, base + zz)), W * 0.42, barrels=3 if k < 2 else 2, length=2.3)
    b.cbox(V((0, y0 + L * 0.34, T2 + 0.06)), (W * 0.88, L * 0.11, 0.06), cls=HULL, taper=0.88)
    for s in (-1, 1):
        b.windows(V((s * W * 0.88, y0 + L * 0.25, T2 + 0.07)), V((s * W * 0.88, y0 + L * 0.43, T2 + 0.07)), V((s, 0, 0)), 12, 0.016, 0.03)
    needle_spire(b, V((0, y0 + L * 0.34, T2 + 0.12)), W * 0.85, 0.7, tiers=4, needles=10)
    for sx, ty, h, w in ((-1, 0.25, 0.46, 0.34), (1, 0.27, 0.5, 0.32), (-1, 0.43, 0.4, 0.3), (1, 0.42, 0.36, 0.3),
                         (0, 0.2, 0.34, 0.3), (0, 0.48, 0.3, 0.26), (-1, 0.15, 0.26, 0.22), (1, 0.13, 0.24, 0.22)):
        needle_spire(b, V((sx * W * 0.58, y0 + L * ty, T2 + 0.08)), W * w, h, tiers=3, needles=6, cupola=w > 0.29)
    edge_spikes(b, [V((s * W * 0.76, y0 + L * (0.05 + 0.03 * i), TOP)) for s in (-1, 1) for i in range(30)], V((0, 0.2, 1)), 0.09, 0.006)
    for s in (-1, 1):
        for g, (zz, a0, a1, n, red) in enumerate(((0.12, 0.06, 0.66, 10, 0.4), (-0.06, 0.08, 0.7, 9, 0.3), (-0.24, 0.1, 0.66, 8, 0.3),
                                                 (-0.42, 0.14, 0.6, 6, 0.25))):
            carriage_row2(b, y0 + L * a0, y0 + L * a1, n, s * W * (1.07 - 0.02 * g), zz, W * 0.1, 0.055, side=s, red=red)
        wall_clutter(b, y0 + L * 0.06, y0 + L * 0.7, s * W * 1.03, -0.45, 0.3, s, 90)
        girders(b, y0 + L * 0.06, y0 + L * 0.7, 16, s * W * 1.03, -0.42, 0.2, s)
        pipe_bundle(b, y0 + L * 0.04, y0 + L * 0.7, s * W * 1.04, 0.1, s)
        pipe_bundle(b, y0 + L * 0.1, y0 + L * 0.66, s * W * 1.02, -0.12, s, n=2)
        pipe_bundle(b, y0 + L * 0.12, y0 + L * 0.62, s * W * 1.0, -0.3, s, n=2)
        side_gun(b, y0 + L * 0.46, y0 + L * 0.84, s * W * 0.97, -0.33, 0.055)
        b.hanging_plates(y0 + L * 0.2, y0 + L * 0.72, W * 0.9, -KEEL * 0.6, 6, 0.16, L * 0.04, s)
        edge_spikes(b, [V((s * W * 0.8, y0 + L * (0.7 + 0.03 * i), TOP * 1.05 + 0.02 * i)) for i in range(9)], V((s * 0.5, 0.6, 0.6)), 0.09, 0.009)
        girders(b, y0 + L * 0.9, y1 - L * 0.01, 3, s * W * 0.3, -KEEL * 0.4, TOP * 0.8, s, w=0.008)
    drum_engine3(b, y0 + L * 0.04, 0, TOP * 0.35, 0.2)
    drum_engine3(b, y0 + L * 0.04, 0, -KEEL * 0.5, 0.19)
    b.cbox(V((0, y0 + L * 0.02, -0.05)), (W * 0.85, L * 0.05, 0.42), cls=TRIM, taper=0.95)
    stalactites(b, y0 + L * 0.12, y1 - L * 0.02, lambda y: -KEEL * 0.95, 56, 0.08, 0.72, bow_bias=1.4)
    greebles(b, y0 + L * 0.5, y0 + L * 0.58, W * 0.5, T2, 6, 0.03)
    return b.to_object(sharp_angle=30)


def pennants(b, y0, y1, x, z, n, h, w, side, red=0.75):
    """Hanging pennant plates down a flank (concept destroyers): tall plates with pointed bottoms and a rivet stud."""
    for i in range(n):
        y = y0 + (y1 - y0) * (i + 0.5) / n
        hh = h * (0.8 + 0.4 * b.rnd.random())
        top = V((side * x, y, z))
        out = V((side * w * 0.12, 0, 0))
        ring0 = [top + V((0, -w, 0)), top + V((0, w, 0)), top + V((0, w, 0)) + out, top + V((0, -w, 0)) + out]
        mid = top + V((side * w * 0.05, 0, -hh * 0.72))
        ring1 = [mid + V((0, -w, 0)), mid + V((0, w, 0)), mid + V((0, w, 0)) + out, mid + V((0, -w, 0)) + out]
        b.loft([ring0, ring1], PAINT if b.rnd.random() < red else HULL, 0.0, cap_start=True, point_end=top + V((side * w * 0.1, 0, -hh)))
        b.cbox(top + V((side * w * 0.14, 0, -hh * 0.3)), (w * 0.05, w * 0.2, w * 0.2), bev=w * 0.04, cls=TRIM)


def emblem_crest(b, center, normal, size):
    """The Pygmaei crest in relief on a black plate: a central spike and two swept wings, crimson."""
    nrm = normal.normalized()
    R = rot_to(nrm, Z if abs(nrm.z) < 0.9 else Y)
    b.cbox(center, (size, size * 0.06, size * 1.1), rot=R, bev=size * 0.05, cls=HULL)
    def tri(a, bb, c):
        pts = [center + R @ V((p[0] * size, size * 0.08, p[1] * size)) for p in (a, bb, c)]
        thick = R @ V((0, size * 0.05, 0))
        b.loft([pts, [p + thick for p in pts]], PAINT, 0.0)
    tri((0, 0.9), (-0.12, -0.6), (0.12, -0.6))
    for s in (-1, 1):
        tri((s * 0.1, 0.3), (s * 0.85, 0.75), (s * 0.35, -0.2))
        tri((s * 0.2, -0.1), (s * 0.7, -0.15), (s * 0.15, -0.85))


def holes(b, a, c, n, r, normal):
    """Row of circular cut-outs, as dark recessed discs, from a to c on a plate facing normal."""
    nrm = normal.normalized()
    for i in range(n):
        p = a.lerp(c, (i + 0.5) / n)
        b.cyl(p + nrm * 0.002, p + nrm * 0.006, r, sides=10, cls=HULL)


def arrow_ram(b, root_y, tip_y, z, w, h, layers=3, spikes=10):
    """Layered arrowhead ram (concept destroyer bows): stacked crimson blades converging to a point, black-striped,
    ringed with forward spikes."""
    for k in range(layers):
        f = 1 - 0.22 * k
        for s in (-1, 1):
            root = V((s * w * f, root_y + (tip_y - root_y) * 0.08 * k, z + h * 0.25 * (1 - k)))
            tip = V((s * w * 0.04, tip_y - (tip_y - root_y) * 0.05 * k, z - h * 0.3))
            b.blade(root, tip, h * 0.55 * f, w * 0.12, up=V((s, 0, 0.3)).normalized(), cls=PAINT)
    for i in range(spikes):
        t = (i + 0.5) / spikes
        s = -1 if i % 2 else 1
        p = V((s * w * (1 - t) * 0.95, root_y + (tip_y - root_y) * t, z + h * (0.3 - 0.4 * t)))
        b.cone(p, V((s * 0.5, 1, 0.05)), h * 0.6, h * 0.05, sides=4, cls=TRIM)


def furnace2(sid):
    """Furnace-class plasma cruiser after the concept: a deep hull walled with shield plates and spikes, a long caged
    glowing furnace core along each upper flank, three stacked stern engines, a round central tower with a gallery and
    spires, two triple turrets forward and twin-gun casemates low on the flank, a blunt bow bristling with spikes, ragged
    skirts and a spike beard."""
    b = PB(sid)
    L, W, TOP, KEEL = 1.8, 0.18, 0.24, 0.34
    y0, y1 = -L / 2, L / 2
    prof = [(0.0, 0.92, 0.9, 0.85), (0.05, 1.0, 1.0, 1.0), (0.7, 1.0, 1.0, 1.0), (0.88, 0.85, 1.0, 0.95), (1.0, 0.5, 0.88, 0.75)]
    slab_hull(b, L, W, TOP, KEEL, prof, bow_stripes(L, 2.0, 0.1, sid, red_chance=0.45, top=TOP))
    T2 = TOP + 0.06
    tier = b.cbox(V((0, y0 + L * 0.4, TOP + 0.03)), (W * 0.8, L * 0.32, 0.03), bev=0.01, cls=HULL, taper=0.95)
    # Shield plates down each wall, crimson and grey, each with a spike.
    for s in (-1, 1):
        for row, (zc, hz, y_a, n) in enumerate(((0.12, 0.09, 0.56, 6), (-0.14, 0.13, 0.2, 11))):
            for i in range(n):
                y = y0 + L * (y_a + (0.98 - y_a) * (i + 0.5) / n)
                red = b.rnd.random() < 0.55
                c = V((s * W * (1.06 + 0.02 * row), y, zc))
                b.cbox(c, (W * 0.05, (0.98 - y_a) * L / n * 0.46, hz), bev=0.01, cls=PAINT if red else HULL, taper=0.92)
                b.cone(c + V((s * W * 0.05, 0, -hz * 0.5)), V((s * 0.6, 0.4, -0.3)), 0.07, 0.008, sides=4, cls=TRIM)
        b.hanging_plates(y0 + L * 0.22, y0 + L * 0.86, W * 0.95, -KEEL * 0.55, 8, 0.16, L * 0.035, s)
        carriage_row2(b, y0 + L * 0.06, y0 + L * 0.2, 2, s * W * 1.04, -0.15, W * 0.1, 0.05, side=s, red=0.3)
        # Twin-gun casemates low on the flank.
        for k in range(3):
            c = V((s * W * 1.12, y0 + L * (0.12 + 0.06 * k), -0.22))
            b.cbox(c, (W * 0.14, L * 0.025, 0.035), bev=0.008, cls=HULL)
            for j in (-1, 1):
                b.cyl(c + V((0, 0, j * 0.015)), c + V((s * 0.02, L * 0.08, j * 0.015)), 0.008, sides=6, cls=TRIM)
        # Caged furnace core along the upper flank.
        b.engine(y0 + L * 0.56, y0 + L * 0.04, s * W * 1.12, 0.03, 0.095, rings=8, collar_spikes=0)
        for i in range(11):
            y = y0 + L * (0.06 + 0.046 * i)
            b.cbox(V((s * W * 1.12, y, 0.03 + 0.1)), (0.1, 0.004, 0.006), bev=0.002, cls=TRIM)
            b.cbox(V((s * (W * 1.12 + 0.1), y, 0.03)), (0.006, 0.004, 0.1), bev=0.002, cls=TRIM)
        wall_clutter(b, y0 + L * 0.1, y0 + L * 0.85, s * W * 1.03, -0.25, 0.15, s, 50)
        girders(b, y0 + L * 0.2, y0 + L * 0.85, 9, s * W * 1.02, -0.24, 0.12, s)
    # Three stacked stern engines.
    for zz, r in ((TOP * 0.55, 0.12), (-0.04, 0.13), (-KEEL * 0.62, 0.11)):
        drum_engine3(b, y0 + L * 0.03, 0, zz, r, spikes=12)
    b.cbox(V((0, y0 + L * 0.02, -0.05)), (W * 0.8, L * 0.04, 0.3), cls=TRIM, taper=0.95)
    # Round tower with gallery and spires.
    needle_spire(b, V((0, y0 + L * 0.42, T2)), W * 1.0, 0.5, tiers=3, needles=10)
    for sx, ty, h, w in ((-1, 0.3, 0.28, 0.3), (1, 0.32, 0.3, 0.28), (0, 0.24, 0.24, 0.26), (-1, 0.2, 0.2, 0.2)):
        needle_spire(b, V((sx * W * 0.55, y0 + L * ty, T2)), W * w, h, tiers=3, needles=5, cupola=False)
    for k, (t, zz) in enumerate(((0.62, 0.06), (0.72, 0.02))):
        b.turret(V((0, y0 + L * t, T2 + zz)), W * 0.56, barrels=3, length=2.3)
    carriage_row2(b, y0 + L * 0.06, y0 + L * 0.3, 3, 0, T2, W * 0.6, 0.045, red=0.4)
    b.turret(V((W * 0.6, y0 + L * 0.8, TOP)), W * 0.36, barrels=2, yaw=-10, length=2.0)
    # Blunt spiked bow.
    for i in range(16):
        a = 2 * math.pi * i / 16
        p = V((math.cos(a) * W * 0.4, y1 + 0.005, math.sin(a) * 0.18 - 0.03))
        b.cone(p, V((math.cos(a) * 0.5, 1, math.sin(a) * 0.4)), 0.13 + 0.05 * (i % 3), 0.012, sides=4, cls=TRIM)
    for s in (-1, 1):
        b.blade(V((s * W * 0.9, y1 - L * 0.16, -0.02)), V((s * W * 0.35, y1 + L * 0.05, -0.12)), 0.08, 0.012, up=V((s, 0, 0.4)).normalized())
    stalactites(b, y0 + L * 0.12, y1 - L * 0.02, lambda y: -KEEL * 0.95, 34, 0.06, 0.32, bow_bias=1.2)
    edge_spikes(b, [V((s * W * 0.76, y0 + L * (0.06 + 0.04 * i), TOP)) for s in (-1, 1) for i in range(22)], V((0, 0.2, 1)), 0.06, 0.005)
    return b.to_object(sharp_angle=30)


def annihilator2(sid):
    """Annihilator-class destroyer after the concept: a long deep hull with a dorsal spine of pipes, four twin turrets
    stepping up toward the bow, a layered crimson arrowhead ram ringed with spikes, hanging pennant plates down the
    flanks, three stacked stern engines, a small lit tower aft and a spike beard."""
    b = PB(sid)
    L, W, TOP, KEEL = 1.7, 0.15, 0.17, 0.25
    y0, y1 = -L / 2, L / 2
    prof = [(0.0, 0.92, 0.9, 0.85), (0.05, 1.0, 1.0, 1.0), (0.72, 1.0, 1.0, 1.0), (0.88, 0.85, 1.05, 0.95), (1.0, 0.45, 1.05, 0.8)]
    slab_hull(b, L, W, TOP, KEEL, prof, bow_stripes(L, 2.0, 0.1, sid, red_chance=0.4, top=TOP))
    arrow_ram(b, y1 - L * 0.34, y1 + L * 0.24, -0.03, W * 1.45, 0.42, layers=4, spikes=16)
    # Dorsal spine: pipe bundle and a raised gun deck.
    b.cbox(V((0, y0 + L * 0.5, TOP + 0.025)), (W * 0.55, L * 0.38, 0.025), bev=0.008, cls=HULL, taper=0.92)
    for k in range(3):
        pts = [V(((k - 1) * 0.02, y0 + L * (0.12 + 0.08 * i), TOP + 0.06)) for i in range(10)]
        b.pipe(pts, 0.008)
    for k, (t, zz) in enumerate(((0.45, 0.07), (0.58, 0.07), (0.7, 0.05), (0.8, 0.03))):
        b.cbox(V((0, y0 + L * t, TOP + zz * 0.5 + 0.03)), (W * 0.45, L * 0.03, zz * 0.5), cls=HULL, taper=0.9)
        b.turret(V((0, y0 + L * t, TOP + zz + 0.03)), W * 0.62, barrels=2, length=2.6)
    needle_spire(b, V((0, y0 + L * 0.24, TOP + 0.05)), W * 0.8, 0.34, tiers=3, needles=8)
    for sx, ty in ((-1, 0.17), (1, 0.18), (0, 0.33)):
        needle_spire(b, V((sx * W * 0.5, y0 + L * ty, TOP + 0.04)), W * 0.3, 0.2, tiers=2, needles=4, cupola=False)
    for s in (-1, 1):
        pennants(b, y0 + L * 0.14, y0 + L * 0.52, W * 1.1, 0.12, 9, 0.24, 0.03, s)
        pennants(b, y0 + L * 0.5, y0 + L * 0.7, W * 1.08, -0.06, 6, 0.18, 0.026, s)
        carriage_row2(b, y0 + L * 0.5, y0 + L * 0.8, 4, s * W * 1.05, 0.04, W * 0.12, 0.04, side=s, red=0.3)
        wall_clutter(b, y0 + L * 0.06, y0 + L * 0.84, s * W * 1.03, -0.2, 0.12, s, 50)
        girders(b, y0 + L * 0.1, y0 + L * 0.84, 12, s * W * 1.02, -0.2, 0.1, s)
        pipe_bundle(b, y0 + L * 0.05, y0 + L * 0.85, s * W * 1.03, -0.12, s, n=3)
        side_gun(b, y0 + L * 0.3, y0 + L * 0.62, s * W * 0.98, -0.18, 0.03)
        edge_spikes(b, [V((s * W * 0.9, y0 + L * (0.1 + 0.04 * i), TOP)) for i in range(18)], V((s * 0.6, 0.4, 0.6)), 0.06, 0.005)
    for zz, r in ((TOP * 0.6, 0.1), (-0.03, 0.11), (-KEEL * 0.6, 0.095)):
        drum_engine3(b, y0 + L * 0.03, 0, zz, r, spikes=12)
    b.cbox(V((0, y0 + L * 0.02, -0.03)), (W * 0.8, L * 0.035, 0.22), cls=TRIM, taper=0.95)
    stalactites(b, y0 + L * 0.12, y1, lambda y: -KEEL * 0.95, 40, 0.05, 0.36, bow_bias=1.2)
    return b.to_object(sharp_angle=30)


def frigate2(sid):
    """Skirmish-class missile frigate after the concept: a flat wedge deck crowded with boxy twin turrets and glowing
    missile grids, a hull that splits forward into twin striped blade prongs with circular cut-outs, an emblem plate on
    the port flank, two coil engines and finned spires aft, spikes along every edge."""
    b = PB(sid)
    L, W, TOP, KEEL = 1.3, 0.2, 0.09, 0.13
    y0, y1 = -L / 2, L / 2
    prof = [(0.0, 0.85, 0.9, 0.85), (0.08, 1.0, 1.0, 1.0), (0.55, 1.0, 1.0, 1.0), (0.75, 0.75, 0.9, 0.85)]
    hull_L = L * 0.75
    b2 = b
    slab_hull(b2, hull_L, W, TOP, KEEL, prof, bow_stripes(hull_L, 2.0, 0.1, sid, red_chance=0.5, top=TOP))
    # Shift is not supported by slab_hull: the hull spans -hull_L/2..hull_L/2; blades continue forward from its bow.
    yb = hull_L / 2
    for s in (-1, 1):
        root = V((s * W * 0.62, yb - hull_L * 0.35, 0.0))
        tip = V((s * W * 0.32, y1 + L * 0.15, -0.03))
        b.blade(root, tip, W * 0.42, 0.035, up=Z, cls=PAINT)
        b.blade(root + V((s * W * 0.25, -0.05, -0.02)), V((s * W * 0.7, y1 - L * 0.05, -0.04)), W * 0.25, 0.025, up=Z, cls=PAINT)
        holes(b, root + V((s * W * 0.1, 0.12, 0.034)), root + V((s * W * 0.02, 0.42, 0.03)), 5, 0.014, Z)
        edge_spikes(b, [root.lerp(tip, t) + V((s * W * 0.4 * (1 - t), 0, 0)) for t in (0.15, 0.3, 0.45, 0.6, 0.75)], V((s * 0.8, 0.5, 0)), 0.07, 0.007)
    # Deck: spine, boxy turrets, missile grids.
    deck = b.cbox(V((0, -hull_L * 0.02, TOP + 0.018)), (W * 0.92, hull_L * 0.44, 0.018), bev=0.006, cls=HULL, taper=0.97)
    b.bm.normal_update()
    b.plate([f for f in deck if f.normal.z > 0.5], 0.004, 0.003, None)
    for f in deck:
        pass
    for k in range(5):
        y = -hull_L * 0.4 + k * hull_L * 0.18
        for s2 in (-1, 1):
            b.blade(V((s2 * W * 0.95, y, TOP + 0.04)), V((s2 * W * 0.25, y + hull_L * 0.16, TOP + 0.04)), W * 0.14, 0.006, up=Z, cls=PAINT, stripes=False)
    b.cbox(V((0, -hull_L * 0.05, TOP + 0.05)), (W * 0.3, hull_L * 0.42, 0.02), bev=0.008, cls=HULL)
    for (x, y, yaw) in ((-0.55, 0.12, 5), (0.55, 0.12, -5), (-0.6, -0.1, 15), (0.6, -0.12, -15)):
        c = V((x * W, y * L, TOP + 0.02))
        b.cbox(c + V((0, 0, 0.02)), (W * 0.22, W * 0.25, 0.025), bev=0.008, cls=HULL)
        b.turret(c + V((0, 0, 0.04)), W * 0.34, barrels=2, yaw=yaw, length=2.0)
    for (x, y) in ((-0.15, -0.02), (0.15, 0.05), (0.0, -0.18)):
        c = V((x * W * 2, y * L, TOP + 0.05))
        b.cbox(c, (W * 0.14, W * 0.12, 0.04), bev=0.006, cls=HULL)
        for i in range(3):
            for j in range(3):
                b.cbox(c + V(((i - 1) * W * 0.08, W * 0.125, (j - 1) * 0.022)), (W * 0.03, 0.003, 0.008), bev=0.002, cls=GLASS, emis=1.6)
    # Emblem plate on the port flank, a wing plate aft.
    emblem_crest(b, V((W * 1.12, -hull_L * 0.22, 0.0)), X, 0.06)
    for s in (-1, 1):
        wing = [V((s * W * 0.9, -hull_L * 0.4, 0.0)), V((s * W * 1.45, -hull_L * 0.44, 0.01)), V((s * W * 1.05, -hull_L * 0.22, 0.0))]
        th = V((0, 0, 0.02))
        b.loft([wing, [p + th for p in wing]], PAINT, 0.0)
        edge_spikes(b, [wing[0].lerp(wing[1], t) for t in (0.3, 0.6, 0.9)], V((s * 0.6, -0.6, 0)), 0.06, 0.006)
        carriage_row2(b, -hull_L * 0.35, hull_L * 0.15, 4, s * W * 1.04, -0.06, W * 0.08, 0.035, side=s, red=0.3)
        pipe_bundle(b, -hull_L * 0.45, hull_L * 0.3, s * W * 1.02, -0.07, s, n=2)
        edge_spikes(b, [V((s * W * 1.0, -hull_L * 0.4 + 0.05 * i, -0.08)) for i in range(12)], V((s * 0.7, 0.2, -0.5)), 0.06, 0.005)
    # Engines and finned spires aft.
    for x in (-1, 1):
        b.engine(-hull_L * 0.38, -hull_L * 0.5 - L * 0.12, x * W * 0.55, 0.02, 0.07, rings=4, collar_spikes=8)
    for (x, y, h) in ((-0.3, -0.36, 0.2), (0.3, -0.34, 0.22), (0.0, -0.3, 0.26)):
        b.blade(V((x * W, y * hull_L, TOP + 0.03)), V((x * W, y * hull_L - 0.05, TOP + 0.03 + h)), 0.04, 0.008, up=Y, cls=PAINT)
    stalactites(b, -hull_L * 0.4, hull_L * 0.4, lambda y: -KEEL * 0.95, 18, 0.04, 0.12, bow_bias=1.0)
    return b.to_object(sharp_angle=30)


def interceptor2(sid):
    """Rift-class void interceptor after the concept: a slim armoured fuselage with a long multi-pane red canopy, two
    forked blade prongs (starboard longer), a big triangular emblem wing to port with spiked edges and cut-outs,
    scythe fins above, two coil engines and underslung guns."""
    b = PB(sid)
    L, W, TOP, KEEL = 1.0, 0.085, 0.07, 0.08
    prof = [(0.0, 0.85, 0.9, 0.85), (0.1, 1.0, 1.0, 1.0), (0.6, 1.0, 1.0, 1.0), (0.85, 0.6, 0.85, 0.7), (1.0, 0.1, 0.55, 0.4)]
    body_L = L * 0.82
    slab_hull(b, body_L, W, TOP, KEEL, prof, bow_stripes(body_L, 2.0, 0.1, sid, red_chance=0.55, top=TOP))
    yb = body_L / 2
    # Canopy: faceted panes in trim frames.
    for k in range(4):
        y = -body_L * 0.08 + k * body_L * 0.1
        b.cbox(V((0, y, TOP + 0.025)), (W * 0.62, body_L * 0.045, 0.028), bev=0.008, cls=PAINT, taper=0.7)
        b.cbox(V((0, y + body_L * 0.05, TOP + 0.025)), (W * 0.66, 0.004, 0.03), bev=0.002, cls=TRIM)
    for s in (-1, 1):
        b.cbox(V((s * W * 0.66, body_L * 0.05, TOP + 0.02)), (0.004, body_L * 0.17, 0.026), bev=0.002, cls=TRIM)
    # Forked prongs.
    for s, ln in ((1, 0.5), (-1, 0.36)):
        root = V((s * W * 1.0, yb - body_L * 0.5, -0.01))
        tip = V((s * W * 1.3, yb + L * ln, -0.03))
        b.blade(root, tip, W * 1.2, 0.026, up=Z, cls=PAINT)
        edge_spikes(b, [root.lerp(tip, t) + V((s * W * 0.7 * (1 - t), 0, 0)) for t in (0.2, 0.4, 0.6)], V((s * 0.8, 0.4, 0)), 0.05, 0.005)
    # Port wing with emblem and cut-outs.
    wing = [V((W * 0.9, -body_L * 0.42, 0.0)), V((W * 5.2, -body_L * 0.5, -0.02)), V((W * 0.95, body_L * 0.08, 0.0))]
    th = V((0, 0, 0.018))
    b.loft([wing, [p + th for p in wing]], PAINT, 0.0)
    emblem_crest(b, V((W * 2.2, -body_L * 0.3, 0.03)), Z, 0.05)
    holes(b, V((W * 1.6, -body_L * 0.02, 0.019)), V((W * 4.2, -body_L * 0.42, 0.019)), 5, 0.012, Z)
    edge_spikes(b, [wing[0].lerp(wing[1], t) for t in (0.2, 0.4, 0.6, 0.8)], V((0.5, -0.8, 0)), 0.06, 0.005)
    edge_spikes(b, [wing[1].lerp(wing[2], t) for t in (0.2, 0.45, 0.7)], V((0.4, 0.3, 0)), 0.05, 0.005)
    b.cone(wing[1], V((0.6, -0.8, 0)), 0.12, 0.012, cls=PAINT)
    # Starboard: a smaller wing and underslung guns.
    swing = [V((-W * 0.9, -body_L * 0.38, 0.0)), V((-W * 3.2, -body_L * 0.3, -0.01)), V((-W * 1.6, body_L * 0.05, 0.0))]
    b.loft([swing, [p + th for p in swing]], PAINT, 0.0)
    for k in range(2):
        g = V((W * (0.9 + 0.5 * k), -body_L * 0.05, -KEEL * 0.8))
        side_gun(b, g.y, g.y + body_L * 0.5, g.x, g.z, 0.012)
    # Scythe fins above.
    for (x, y, h, lean) in ((0.4, -0.3, 0.18, -0.06), (-0.1, -0.22, 0.14, -0.05)):
        b.blade(V((x * W, y * body_L, TOP + 0.02)), V((x * W, y * body_L + lean, TOP + 0.02 + h)), 0.03, 0.006, up=Y, cls=PAINT)
    # Engines.
    for x in (-1, 1):
        b.engine(-body_L * 0.36, -body_L * 0.5 - L * 0.12, x * W * 0.9, 0.02, 0.055, rings=4, collar_spikes=6)
    b.pipe([V((W * 0.95, -body_L * 0.4 + 0.05 * i, TOP * 0.6)) for i in range(9)], 0.006)
    b.pipe([V((-W * 0.95, -body_L * 0.4 + 0.05 * i, TOP * 0.6)) for i in range(9)], 0.006)
    stalactites(b, -body_L * 0.3, body_L * 0.35, lambda y: -KEEL * 0.95, 8, 0.03, 0.08, bow_bias=1.0)
    return b.to_object(sharp_angle=30)

SHIPS = {
    "pygmaei_revenant_command_ship": revenant5,
    "pygmaei_furnace_cruiser": furnace2,
    "pygmaei_annihilator_destroyer": annihilator2,
    "pygmaei_skirmish_frigate": frigate2,
    "pygmaei_rift_interceptor": interceptor2,
}


# =================================================================================================== preview

def preview_material():
    mat = bpy.data.materials.get("RR_PygPreview")
    if mat:
        return mat
    mat = bpy.data.materials.new("RR_PygPreview")
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes.get("Principled BSDF")
    attr = nt.nodes.new("ShaderNodeVertexColor"); attr.layer_name = "Col"
    sep = nt.nodes.new("ShaderNodeSeparateColor")
    nt.links.new(attr.outputs["Color"], sep.inputs["Color"])
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.interpolation = 'CONSTANT'
    els = ramp.color_ramp.elements
    els[0].position = 0.0; els[0].color = (0.055, 0.05, 0.05, 1)       # iron
    els[1].position = 0.18; els[1].color = (0.42, 0.025, 0.02, 1)     # crimson paint
    e = els.new(0.4); e.color = (0.30, 0.24, 0.2, 1)                  # trim metal
    e = els.new(0.8); e.color = (0.25, 0.04, 0.02, 1)                 # glass
    nt.links.new(sep.outputs["Red"], ramp.inputs["Fac"])
    ao = nt.nodes.new("ShaderNodeMath"); ao.operation = 'MULTIPLY_ADD'
    ao.inputs[1].default_value = 0.65; ao.inputs[2].default_value = 0.35
    nt.links.new(sep.outputs["Blue"], ao.inputs[0])
    mul = nt.nodes.new("ShaderNodeMix"); mul.data_type = 'RGBA'; mul.blend_type = 'MULTIPLY'
    mul.inputs["Factor"].default_value = 1.0
    nt.links.new(ramp.outputs["Color"], mul.inputs["A"])
    nt.links.new(ao.outputs[0], mul.inputs["B"])
    nt.links.new(mul.outputs["Result"], bsdf.inputs["Base Color"])
    metal = nt.nodes.new("ShaderNodeMath"); metal.operation = 'COMPARE'
    metal.inputs[1].default_value = 0.5; metal.inputs[2].default_value = 0.1
    nt.links.new(sep.outputs["Red"], metal.inputs[0])
    nt.links.new(metal.outputs[0], bsdf.inputs["Metallic"])
    bsdf.inputs["Roughness"].default_value = 0.55
    em = nt.nodes.new("ShaderNodeMath"); em.operation = 'MULTIPLY'; em.inputs[1].default_value = 2.0
    nt.links.new(sep.outputs["Green"], em.inputs[0])
    bsdf.inputs["Emission Color"].default_value = (1.0, 0.1, 0.03, 1)
    nt.links.new(em.outputs[0], bsdf.inputs["Emission Strength"])
    return mat


def render_views(ob, out_dir):
    scene = bpy.context.scene
    engines = [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items]
    scene.render.engine = 'BLENDER_EEVEE' if 'BLENDER_EEVEE' in engines else 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in engines else 'CYCLES'
    if scene.render.engine == 'CYCLES':
        scene.cycles.samples = 48
    scene.render.resolution_x, scene.render.resolution_y = 1280, 800
    me = ob.data
    me.materials.clear(); me.materials.append(preview_material())
    if not scene.world:
        scene.world = bpy.data.worlds.new("w")
    scene.world.use_nodes = True
    bg = scene.world.node_tree.nodes.get("Background")
    bg.inputs[0].default_value = (0.06, 0.065, 0.09, 1); bg.inputs[1].default_value = 1.0
    bb = [ob.matrix_world @ V(c) for c in ob.bound_box]
    lo = V((min(v.x for v in bb), min(v.y for v in bb), min(v.z for v in bb)))
    hi = V((max(v.x for v in bb), max(v.y for v in bb), max(v.z for v in bb)))
    center, size = (lo + hi) / 2, (hi - lo).length
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); scene.collection.objects.link(cam); scene.camera = cam
    cam.data.lens = 70
    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", 'SUN')); scene.collection.objects.link(sun)
    sun.data.energy = 6.0; sun.rotation_euler = (math.radians(55), math.radians(5), math.radians(-35))
    fill = bpy.data.objects.new("fill", bpy.data.lights.new("fill", 'SUN')); scene.collection.objects.link(fill)
    fill.data.energy = 0.7; fill.data.color = (0.5, 0.6, 1.0); fill.rotation_euler = (math.radians(-60), 0, math.radians(140))
    views = {"hero": V((1.0, 1.15, 0.55)), "side": V((1.0, 0.0, 0.12)), "battle": V((0.25, -0.35, 1.0)), "rear": V((-0.8, -1.1, 0.45))}
    for name, d in views.items():
        cam.location = center + d.normalized() * size * 1.6
        cam.rotation_euler = (center - cam.location).to_track_quat('-Z', 'Y').to_euler()
        scene.render.filepath = os.path.join(out_dir, f"{ob.name}__{name}.png")
        bpy.ops.render.render(write_still=True)
    for o in (cam, sun, fill):
        bpy.data.objects.remove(o)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    only = set(argv[argv.index("--only") + 1].split(",")) if "--only" in argv else None
    bake = "--no-bake" not in argv
    render_dir = argv[argv.index("--render") + 1] if "--render" in argv else None
    os.makedirs(OUT_FBX, exist_ok=True)
    G.reset_scene()
    for sid, fn in SHIPS.items():
        if only and sid not in only:
            continue
        ob = fn(sid)
        tris = sum(len(p.vertices) - 2 for p in ob.data.polygons)
        if bake:
            G.bake_ao(ob, samples=48)
        G.export_fbx(ob, os.path.join(OUT_FBX, sid + ".fbx"))
        d = ob.dimensions
        print(f"[rr_pygmaei] {sid}: {tris} tris, dims {d.x:.2f} x {d.y:.2f} x {d.z:.2f}")
        if render_dir:
            os.makedirs(render_dir, exist_ok=True)
            for o in bpy.context.scene.objects:
                o.hide_render = o is not ob
            render_views(ob, render_dir)
        ob.hide_render = True
    print("[rr_pygmaei] done")


if __name__ == "__main__":
    main()
