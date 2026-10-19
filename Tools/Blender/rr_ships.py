"""Rule and Ruin vertical slice: procedural ships, station and debris (Blender 5.x).

Run:  blender -b --factory-startup --python Tools/Blender/rr_ships.py -- [--only id,id] [--no-bake] [--render]
Writes Assets/RR/Art/Models/<id>.fbx, Tools/Blender/out/rr_ships.blend (editable source) and, with --render,
Tools/Blender/out/renders/<id>.png. Idempotent: the same inputs give the same meshes (seeded per id).

Every ship shape here is a NEW ART INTERPRETATION of the faction references (Docs/References), pending approval.
"""
import bpy, math, os, random, sys
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import rr_geo as G
from rr_geo import Builder, HULL, TRIM, GLASS

ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT_FBX = os.environ.get("RR_FBX_OUT") or os.path.join(ROOT, "Assets", "RR", "Art", "Models")
OUT_SRC = os.path.join(HERE, "out")
V = Vector


def rot_z(deg):
    return Matrix.Rotation(math.radians(deg), 3, 'Z')


# =====================================================================================================
# Silex: granite fortress hulls, stacked shard plates, rising crystal spires, crystal inlays.
# =====================================================================================================

def silex_section(w, h):
    return [V((0, 0, h)), V((w * 0.56, 0, h * 0.58)), V((w, 0, h * 0.06)), V((w * 0.82, 0, -h * 0.42)),
            V((0, 0, -h * 0.66)), V((-w * 0.82, 0, -h * 0.42)), V((-w, 0, h * 0.06)), V((-w * 0.56, 0, h * 0.58))]


def silex_loft(b, L, W, H, y0, y1, z0=0.0, prof=None, cls=HULL, emis=0.0, point=True, n=9):
    prof = prof or [(0.0, 0.62, 0.72), (0.08, 0.86, 0.94), (0.26, 1.0, 1.0), (0.55, 0.96, 0.9), (0.76, 0.66, 0.62), (0.9, 0.32, 0.3)]
    rings = []
    for (t, sw, sh) in prof:
        y = y0 + (y1 - y0) * t
        rings.append([p + V((0, y, z0)) for p in silex_section(W * sw, H * sh)])
    tip = V((0, y1, z0 - H * 0.05)) if point else None
    return b.loft(rings, cls, emis, cap_start=True, point_end=tip)


def silex_ship(sid, p):
    rnd = random.Random(sid)
    b = Builder(sid)
    L, W, H = p["L"], p["W"], p["H"]
    y0, y1 = -L * 0.5, L * 0.5
    # Main keel hull and a raised dorsal tier: the stepped fortress silhouette.
    silex_loft(b, L, W, H, y0, y1)
    if p.get("tier", True):
        silex_loft(b, L, W * 0.62, H * 0.55, y0 + L * 0.06, y0 + L * 0.74, z0=H * 0.62,
                   prof=[(0.0, 0.7, 0.6), (0.15, 1.0, 1.0), (0.6, 0.9, 0.95), (0.85, 0.55, 0.6)], cls=HULL)
    # Ventral keel blade.
    b.shard(V((0, y0 + L * 0.15, -H * 0.55)), V((0, 1, -0.06)), L * 0.72, W * 0.12, H * 0.32, up=V((1, 0, 0)), cls=TRIM)
    # Flank armour: layered shards sweeping toward the prow, like stacked stone plates.
    n = p.get("plates", 4)
    for side in (-1, 1):
        for i in range(n):
            t = 0.12 + 0.62 * i / max(1, n - 1)
            y = y0 + L * t
            wl = W * (0.95 - 0.25 * abs(t - 0.4))
            base = V((side * wl * 0.78, y, H * (0.05 + 0.07 * (i % 2))))
            d = V((side * (0.28 + 0.06 * rnd.random()), 1.0, 0.04 - 0.05 * (i % 2)))
            b.shard(base, d, L * p.get("plate_len", 0.3) * (1.0 - 0.15 * i / n), W * 0.2, H * 0.14, cls=HULL)
        # Crystal inlays set into the flank, the faction's glowing seams.
        for i in range(p.get("inlays", 3)):
            t = 0.22 + 0.45 * i / max(1, p.get("inlays", 3) - 1)
            y = y0 + L * t
            base = V((side * W * 0.9, y, H * 0.12))
            b.shard(base, V((side * 0.12, 1.0, 0.02)), L * 0.16, W * 0.1, H * 0.08, cls=GLASS, emis=1.0)
        # Rear fins.
        b.shard(V((side * W * 0.5, y0 + L * 0.12, H * 0.2)), V((side * 0.9, -0.8, 0.15)), W * 0.9, W * 0.12, H * 0.08, cls=HULL)
    # Dorsal spires.
    for (t, hgt, rad, xoff, tilt) in p.get("spires", []):
        y = y0 + L * t
        for sx in ((-1, 1) if xoff > 0 else (0,)):
            base = V((sx * xoff * W, y, H * 0.95 if p.get("tier", True) else H * 0.8))
            axis = V((sx * tilt, -0.08, 1.0))
            b.prism_crystal(base, axis, hgt * H, rad * W, cls=GLASS, emis=0.75, twist=rnd.random())
            for k in range(p.get("satellites", 2)):
                a = rnd.uniform(0, 2 * math.pi)
                off = V((math.cos(a) * rad * W * 1.4, math.sin(a) * rad * W * 1.4, 0))
                b.prism_crystal(base + off, axis + V((off.x * 2.5, off.y * 2.5, 0)), hgt * H * rnd.uniform(0.35, 0.55),
                                rad * W * 0.55, cls=GLASS, emis=0.65, twist=rnd.random())
    # Crown: ring of spires around a centre (bastion, flagship).
    if p.get("crown"):
        cy, cr, cnt, ch = p["crown"]
        for i in range(cnt):
            a = 2 * math.pi * i / cnt
            base = V((math.cos(a) * cr * W, y0 + L * cy + math.sin(a) * cr * W * 0.8, H * 0.9))
            b.prism_crystal(base, V((math.cos(a) * 0.25, math.sin(a) * 0.2, 1)), ch * H * (1.0 if i % 2 == 0 else 0.7),
                            W * 0.07, cls=GLASS, emis=0.8, twist=a)
        b.torus(V((0, y0 + L * cy, H * 0.9)), V((0, 0, 1)), cr * W, W * 0.05, seg=cnt * 4, sides=5, cls=TRIM)
    # Towers: granite blocks with crystal caps (dreadnought, throne).
    for (t, xo, th, tw) in p.get("towers", []):
        for sx in ((-1, 1) if xo > 0 else (0,)):
            c = V((sx * xo * W, y0 + L * t, H * 0.9 + th * H * 0.5))
            b.box(c, (tw * W, tw * W * 1.3, th * H * 0.5), cls=HULL, taper=0.78)
            b.prism_crystal(c + V((0, 0, th * H * 0.5)), V((0, 0, 1)), th * H * 0.7, tw * W * 0.7, cls=GLASS, emis=0.8)
    # Prow lance (battleship), sensor prongs (scout).
    if p.get("lance"):
        b.prism_crystal(V((0, y1 - L * 0.12, 0)), V((0, 1, 0)), L * p["lance"], W * 0.16, sides=6, tip=0.4, cls=GLASS, emis=0.9)
    if p.get("prongs"):
        for side in (-1, 1):
            b.shard(V((side * W * 0.45, y1 - L * 0.3, 0)), V((side * 0.15, 1, 0)), L * p["prongs"], W * 0.08, H * 0.08, cls=TRIM)
            b.prism_crystal(V((side * W * 0.62, y1 - L * 0.3 + L * p["prongs"] * 0.75, 0)), V((side * 0.15, 1, 0)),
                            L * 0.08, W * 0.06, cls=GLASS, emis=1.0)
    # Lattice ribs (corvette's Linked Grid).
    for i in range(p.get("ribs", 0)):
        y = y0 + L * (0.25 + 0.5 * i / max(1, p["ribs"] - 1))
        b.torus(V((0, y, H * 0.1)), V((0, 1, 0)), W * 1.08, W * 0.035, seg=24, sides=4, cls=GLASS, emis=0.7)
    # Engines: crystal cores in granite collars.
    ne = p.get("engines", 2)
    for i in range(ne):
        x = 0 if ne == 1 else (i / (ne - 1) - 0.5) * W * 1.1
        c = V((x, y0 + L * 0.02, H * 0.05))
        b.torus(c, V((0, 1, 0)), W * 0.16, W * 0.05, seg=12, sides=5, cls=TRIM)
        b.prism_crystal(c + V((0, L * 0.03, 0)), V((0, -1, 0)), L * 0.07, W * 0.13, cls=GLASS, emis=1.0)
    ob = b.to_object(sharp_angle=28)
    bev = ob.modifiers.new("bevel", 'BEVEL')
    bev.width = L * 0.004
    bev.segments = 1
    bev.limit_method = 'ANGLE'
    bev.angle_limit = math.radians(30)
    G.apply_modifiers(ob)
    return ob


SILEX = {
    "silex_facet_surveyor":      dict(L=0.85, W=0.17, H=0.12, plates=3, inlays=2, plate_len=0.28, tier=False,
                                      spires=[(0.45, 1.3, 0.22, 0, 0)], satellites=1, prongs=0.35, engines=1),
    "silex_prism_escort":        dict(L=0.9, W=0.22, H=0.12, plates=3, inlays=2, plate_len=0.34, tier=False,
                                      spires=[(0.4, 1.1, 0.2, 0.45, 0.25)], satellites=1, engines=2),
    "silex_lattice_corvette":    dict(L=1.05, W=0.25, H=0.15, plates=3, inlays=2, ribs=3,
                                      spires=[(0.42, 1.4, 0.18, 0, 0)], satellites=2, engines=2),
    "silex_tessera_frigate":     dict(L=1.2, W=0.27, H=0.17, plates=6, inlays=4, plate_len=0.22,
                                      spires=[(0.3, 1.2, 0.16, 0.4, 0.15), (0.55, 1.6, 0.18, 0, 0)], satellites=1, engines=2),
    "silex_pillar_destroyer":    dict(L=1.45, W=0.27, H=0.18, plates=4, inlays=3,
                                      spires=[(0.22, 1.2, 0.14, 0, 0), (0.4, 1.8, 0.16, 0, 0), (0.58, 1.4, 0.14, 0, 0)],
                                      satellites=1, engines=3),
    "silex_obelisk_cruiser":     dict(L=1.6, W=0.33, H=0.22, plates=5, inlays=3,
                                      spires=[(0.42, 3.0, 0.2, 0, 0), (0.25, 1.2, 0.12, 0.45, 0.2)], satellites=3, engines=3),
    "silex_crown_bastion":       dict(L=1.55, W=0.52, H=0.26, plates=4, inlays=4, plate_len=0.25,
                                      spires=[(0.45, 2.2, 0.16, 0, 0)], satellites=2, crown=(0.45, 0.42, 8, 1.4), engines=4),
    "silex_monolith_battleship": dict(L=1.95, W=0.42, H=0.3, plates=6, inlays=4, lance=0.42,
                                      spires=[(0.3, 1.8, 0.13, 0.35, 0.12), (0.5, 2.4, 0.16, 0, 0)], satellites=2, engines=4),
    "silex_citadel_dreadnought": dict(L=2.25, W=0.5, H=0.32, plates=7, inlays=5,
                                      towers=[(0.25, 0.55, 1.2, 0.12), (0.45, 0.45, 1.6, 0.13), (0.62, 0.3, 1.0, 0.1)],
                                      spires=[(0.45, 2.8, 0.17, 0, 0)], satellites=3, engines=5),
    "silex_throne_ark":          dict(L=2.1, W=0.48, H=0.3, plates=6, inlays=5,
                                      towers=[(0.3, 0.5, 1.4, 0.11), (0.5, 0.5, 1.8, 0.1)],
                                      spires=[(0.42, 3.6, 0.2, 0, 0), (0.25, 2.0, 0.13, 0.25, 0.1)], satellites=4,
                                      crown=(0.42, 0.3, 6, 1.8), engines=4),
}


# =====================================================================================================
# Craigon: segmented crustacean carapace, pincers, curved claw-legs, bioluminescent seams.
# =====================================================================================================

def c_section(w, h, ridge=0.0, sides=14):
    """Flattened elliptical section with an optional dorsal keel ridge (x across, z up)."""
    pts = []
    for i in range(sides):
        a = 2 * math.pi * i / sides
        s = math.sin(a)
        pts.append(V((math.cos(a) * w, 0, s * h + ridge * h * max(0.0, s) ** 10)))
    return pts


def c_ring(center, tangent, w, h, ridge=0.0, sides=14, up=V((0, 0, 1))):
    t = tangent.normalized()
    side = t.cross(up)
    if side.length < 1e-4:
        side = V((1, 0, 0))
    side.normalize()
    nup = side.cross(t).normalized()
    return [center + side * p.x + nup * p.z for p in c_section(w, h, ridge, sides)]


def c_band(b, r0, r1, cls, emis):
    """Open tube band between two rings with the same vertex count."""
    bm = b.bm
    v0 = [bm.verts.new(p) for p in r0]
    v1 = [bm.verts.new(p) for p in r1]
    n = len(r0)
    faces = [bm.faces.new((v0[i], v0[(i + 1) % n], v1[(i + 1) % n], v1[i])) for i in range(n)]
    b._tag(faces, cls, emis)


def c_spine(p, t):
    """Arched spine: tail curls up, body humps, head dips slightly."""
    L, H = p["L"], p["H"]
    y = -L * 0.5 + L * t
    z = p.get("arch", 0.6) * H * math.sin(math.pi * t) + p.get("curl", 0.5) * H * (1 - t) ** 3 - 0.15 * H * t ** 4
    return V((0, y, z))


def c_profile(t):
    k = [(0.0, 0.28), (0.15, 0.55), (0.38, 1.0), (0.62, 0.95), (0.8, 0.78), (0.9, 0.62)]
    for (a, ra), (c, rc) in zip(k[:-1], k[1:]):
        if a <= t <= c:
            u = (t - a) / (c - a)
            u = u * u * (3 - 2 * u)
            return ra + (rc - ra) * u
    return k[-1][1]


def claw_jaw(b, root, forward, down, length, w0, h0, curl, cls=HULL):
    """One jaw of the claw: a tapered, curving blade with a serrated inner edge."""
    pts, rad = [], []
    n = 9
    for i in range(n + 1):
        u = i / n
        p = root + forward * (length * u) + down * (curl * length * (u ** 1.6))
        pts.append(p)
        rad.append(max(0.004, 1.0 - u ** 1.25))
    rings = []
    for i, c in enumerate(pts):
        t = (pts[min(i + 1, n)] - pts[max(i - 1, 0)]).normalized()
        rings.append(c_ring(c, t, w0 * rad[i], h0 * rad[i], ridge=0.6, sides=12))
    b.loft(rings, cls, 0.0, cap_start=True, point_end=pts[-1] + (pts[-1] - pts[-2]).normalized() * length * 0.06)
    # Serrations on the inner edge.
    for i in range(2, n - 1, 2):
        u = i / n
        c = pts[i]
        b.tube([c, c - down * (h0 * rad[i] * 1.9) + forward * length * 0.02], [w0 * rad[i] * 0.35, 0.003], sides=5, cls=cls)
    return pts


def craigon_ship(sid, p):
    rnd = random.Random(sid)
    b = Builder(sid)
    L, W, H = p["L"], p["W"], p["H"]
    segs = p.get("segs", 7)
    head_t = 0.9
    # Segmented carapace along the arched spine: each segment flares toward its front edge, so the silhouette is
    # a sawtooth of overlapping plates; the step faces between segments are bioluminescent seams.
    prev_front = None
    for k in range(segs):
        ta, tb = head_t * k / segs, head_t * (k + 1) / segs
        rear, front = c_spine(p, ta), c_spine(p, tb)
        tan = (front - rear).normalized()
        ra, rb = c_profile(ta), c_profile(tb)
        ring_rear = c_ring(rear, tan, W * ra * 0.9, H * ra * 0.86, ridge=0.35)
        ring_front = c_ring(front, tan, W * rb * 1.1, H * rb * 1.06, ridge=0.45)
        if prev_front is not None:
            c_band(b, prev_front, ring_rear, GLASS, 0.85)
        else:
            b.loft([ring_rear], HULL, 0.0, cap_start=True, cap_end=False, point_start=rear - tan * L * 0.02)
            for kk in (-1, 0, 1):
                b.shard(rear + V((kk * W * 0.15, 0, H * 0.05)), V((kk * 0.7, -1.0, 0.15)), L * 0.2, W * 0.35, H * 0.06, cls=HULL)
        c_band(b, ring_rear, ring_front, HULL if k % 3 != 2 else HULL, 0.0)
        prev_front = ring_front
    # Head cowl closing the carapace.
    head = c_spine(p, head_t)
    tan = (head - c_spine(p, head_t - 0.02)).normalized()
    cowl = c_ring(head + tan * L * 0.03, tan, W * 0.55, H * 0.5, ridge=0.5)
    c_band(b, prev_front, cowl, TRIM, 0.0)
    b.loft([cowl], TRIM, 0.0, cap_start=False, cap_end=True)
    # The claw maw: upper and lower jaws curving toward each other, an energy orb held in the gap.
    jaw = p.get("jaw", 0.32)
    if jaw > 0:
        fwd = V((0, 1, 0))
        up_root = head + V((0, -L * 0.02, H * 0.28))
        lo_root = head + V((0, -L * 0.02, -H * 0.3))
        up = claw_jaw(b, up_root, fwd, V((0, 0, -1)), L * jaw * 1.25, W * 0.95, H * 0.62, p.get("jaw_curl", 0.42))
        lo = claw_jaw(b, lo_root, fwd, V((0, 0, 1)), L * jaw * 1.05, W * 0.8, H * 0.5, p.get("jaw_curl", 0.42) * 0.8)
        orb = (up[4] + lo[4]) * 0.5
        b.ellipsoid(orb, (W * 0.26, W * 0.26, W * 0.26), cls=GLASS, emis=1.0, u=12, v=8)
    # Side pincers (crab claws) for the line-breakers.
    pc = p.get("pincers", 0)
    if pc:
        for side in (-1, 1):
            root = c_spine(p, 0.72) + V((side * W * 0.85, 0, -H * 0.15))
            fwd = V((side * 0.35, 1, -0.05)).normalized()
            arm = [root, root + fwd * L * pc * 0.3, root + fwd * L * pc * 0.55 + V((side * W * 0.15, 0, H * 0.1))]
            b.tube(arm, [W * 0.4, W * 0.36, W * 0.34], sides=10, cls=HULL, point_end=False)
            hand = arm[-1]
            claw_jaw(b, hand + V((0, 0, H * 0.08)), V((-side * 0.25, 1, 0)).normalized(), V((0, 0, -1)), L * pc * 0.55, W * 0.62, H * 0.5, 0.35)
            claw_jaw(b, hand + V((0, 0, -H * 0.08)), V((-side * 0.25, 1, 0)).normalized(), V((0, 0, 1)), L * pc * 0.46, W * 0.5, H * 0.4, 0.3)
            b.ellipsoid(hand, (W * 0.42, W * 0.5, W * 0.4), cls=TRIM, u=12, v=8)
    # Dorsal spikes swept back along the keel.
    for i in range(p.get("spikes", 5)):
        t = 0.18 + 0.6 * i / max(1, p.get("spikes", 5) - 1)
        c = c_spine(p, t)
        r = c_profile(t)
        base = c + V((0, 0, H * r * 1.1))
        h = H * p.get("spike_h", 0.9) * (0.6 + 0.5 * math.sin(math.pi * t))
        b.tube([base, base + V((0, -L * 0.04, h * 0.6)), base + V((0, -L * 0.12, h))], [W * 0.09, W * 0.05, W * 0.01], sides=6, cls=HULL)
    # Flank spines and jointed legs, swept back.
    for i in range(p.get("legs", 3)):
        t = 0.3 + 0.32 * i / max(1, p.get("legs", 3) - 1)
        c = c_spine(p, t)
        r = c_profile(t)
        for side in (-1, 1):
            a = c + V((side * W * r * 0.9, 0, -H * r * 0.35))
            pts = [a, a + V((side * W * 0.55, -L * 0.02, -H * 0.25)), a + V((side * W * 1.0, -L * 0.09, -H * 0.55)), a + V((side * W * 1.1, -L * 0.17, -H * 0.95))]
            b.tube(pts, [W * 0.08, W * 0.07, W * 0.05, W * 0.015], sides=6, cls=TRIM)
    # Neural crest: tall swept horns with synapse lights.
    for i in range(p.get("crest", 0)):
        t = 0.55 + 0.25 * i / max(1, p["crest"] - 1) if p["crest"] > 1 else 0.62
        c = c_spine(p, t)
        for side in ((-1, 1) if p.get("crest_pairs") else (0,)):
            base = c + V((side * W * 0.3, 0, H * c_profile(t) * 1.0))
            h = H * p.get("crest_h", 1.6) * (1.0 - 0.2 * i / max(1, p["crest"]))
            tip = base + V((side * W * 0.35, -L * 0.2, h))
            b.tube([base, base + V((side * W * 0.12, -L * 0.07, h * 0.55)), tip], [W * 0.1, W * 0.065, W * 0.02], sides=7, cls=HULL)
            b.ellipsoid(tip, (W * 0.07, W * 0.07, W * 0.07), cls=GLASS, emis=1.0, u=8, v=6)
    # Wings for the sporewing.
    if p.get("wings"):
        for side in (-1, 1):
            base = c_spine(p, 0.55) + V((side * W * 0.7, 0, H * 0.2))
            b.shard(base, V((side * 1.0, -0.7, 0.15)), L * p["wings"], L * 0.14, H * 0.08, cls=HULL)
            b.shard(base + V((0, -L * 0.05, H * 0.05)), V((side * 1.0, -0.95, 0.1)), L * p["wings"] * 0.8, L * 0.035, H * 0.05, cls=GLASS, emis=0.9)
    # Chrysalis pods for the carrier.
    for i in range(p.get("pods", 0)):
        t = 0.32 + 0.36 * i / max(1, p["pods"] - 1)
        c = c_spine(p, t)
        r = c_profile(t)
        for side in (-1, 1):
            pc_ = c + V((side * W * r * 1.2, 0, -H * r * 0.15))
            b.ellipsoid(pc_, (W * 0.3, L * 0.085, H * 0.32), cls=GLASS, emis=0.75, u=12, v=8)
            b.torus(pc_, V((0, 1, 0)), W * 0.31, W * 0.05, seg=14, sides=5, cls=TRIM)
    # Bastion shell: a wide domed carapace with a ring of spikes.
    if p.get("dome"):
        dw = p["dome"]
        mid = c_spine(p, 0.48)
        for k in range(5):
            y0 = mid.y - L * 0.32 + k * L * 0.13
            b.shell_plate(y0, y0 + L * 0.17, W * dw * (1.05 - 0.06 * abs(k - 2)), W * dw * (0.95 - 0.06 * abs(k - 2)),
                          math.radians(-12), math.radians(192), W * 0.1, zoff=mid.z + H * 0.1, steps=18, cls=HULL, sz=0.62)
        for i in range(12):
            a = math.radians(-6 + 192 * i / 11)
            base = V((math.cos(a) * W * dw, mid.y + L * 0.05 * math.sin(i), mid.z + math.sin(a) * W * dw * 0.6 + H * 0.1))
            b.tube([base, base + V((math.cos(a) * W * 0.45, -L * 0.05, math.sin(a) * W * 0.35))], [W * 0.1, W * 0.015], sides=6, cls=HULL)
    # Bio-thrusters at the tail.
    tail = c_spine(p, 0.02)
    for i in range(p.get("thrusters", 2)):
        x = (i / max(1, p.get("thrusters", 2) - 1) - 0.5) * W * 0.8 if p.get("thrusters", 2) > 1 else 0
        b.ellipsoid(tail + V((x, L * 0.03, -H * 0.1)), (W * 0.13, L * 0.05, H * 0.12), cls=GLASS, emis=1.0, u=10, v=6)
    ob = b.to_object(sharp_angle=50)
    sub = ob.modifiers.new("sub", 'SUBSURF')
    sub.levels = 1
    sub.render_levels = 1
    G.apply_modifiers(ob)
    for poly in ob.data.polygons:
        poly.use_smooth = True
    ob.data.set_sharp_from_angle(angle=math.radians(62))
    return ob


CRAIGON = {
    "craigon_sporewing_fighter":      dict(L=0.8, W=0.15, H=0.11, segs=5, spikes=3, wings=0.45, legs=0, jaw=0.22, thrusters=1, arch=0.4),
    "craigon_scuttler_interceptor":   dict(L=0.9, W=0.16, H=0.13, segs=6, spikes=4, legs=2, jaw=0.36, jaw_curl=0.5, thrusters=2),
    "craigon_scuttler_corvette":      dict(L=1.05, W=0.2, H=0.16, segs=6, spikes=4, legs=3, jaw=0.3, thrusters=2),
    "craigon_pincer_frigate":         dict(L=1.2, W=0.22, H=0.17, segs=7, spikes=4, legs=2, jaw=0.18, pincers=0.62, thrusters=2),
    "craigon_communion_destroyer":    dict(L=1.5, W=0.24, H=0.18, segs=8, spikes=6, legs=3, jaw=0.34, crest=2, crest_h=1.5, thrusters=3),
    "craigon_synaptic_cruiser":       dict(L=1.7, W=0.3, H=0.22, segs=8, spikes=5, legs=3, jaw=0.26, pincers=0.42, crest=3, crest_h=1.9, crest_pairs=True, thrusters=3),
    "craigon_chrysalis_carrier":      dict(L=1.75, W=0.36, H=0.26, segs=8, spikes=3, legs=2, pods=3, jaw=0.2, thrusters=3, arch=0.9),
    "craigon_evolutionary_bastion":   dict(L=1.6, W=0.36, H=0.26, segs=6, spikes=0, legs=4, dome=1.45, jaw=0.22, pincers=0.32, thrusters=4),
    "craigon_ascendant_ark":          dict(L=2.1, W=0.34, H=0.25, segs=10, spikes=7, legs=3, pods=2, jaw=0.3, crest=3, crest_h=1.8, crest_pairs=True, thrusters=4, arch=0.8),
    "craigon_prime_synapse_flagship": dict(L=2.3, W=0.4, H=0.28, segs=10, spikes=7, legs=3, jaw=0.36, pincers=0.48, crest=4, crest_h=2.4, crest_pairs=True, thrusters=4, spike_h=1.2),
    "token_craigon_sporewing":        dict(L=0.55, W=0.11, H=0.08, segs=4, spikes=2, wings=0.35, legs=0, jaw=0.2, thrusters=1, arch=0.4),
}

def craigon_auxiliary_hulk(sid):
    """A converted hulk: a Silex-style hull shell overgrown by Craigon carapace (token_craigon_auxiliary_orbital)."""
    b = Builder(sid)
    L, W, H = 0.9, 0.24, 0.14
    silex_loft(b, L, W, H, -L / 2, L / 2)
    for i in range(4):
        t = i / 4
        y = -L / 2 + L * (0.1 + t * 0.8)
        b.shell_plate(y, y + L * 0.22, W * 1.25, W * 1.1, math.radians(-20), math.radians(200), W * 0.06, steps=10, cls=TRIM, sz=0.8)
    for side in (-1, 1):
        b.tube([V((side * W * 0.8, 0.1, -H * 0.2)), V((side * W * 1.4, 0.0, -H * 0.6)), V((side * W * 1.5, -0.12, -H * 1.0))],
               [W * 0.1, W * 0.07, W * 0.02], cls=TRIM)
    b.ellipsoid(V((0, 0, H * 0.8)), (W * 0.35, L * 0.2, W * 0.25), cls=GLASS, emis=0.8, u=12, v=8)
    return b.to_object(sharp_angle=40)


# =====================================================================================================
# Station and debris
# =====================================================================================================

def silex_citadel_station(sid):
    """Silex orbital citadel: stacked granite rings around a great crystal spire, docking arms, glowing nodes."""
    rnd = random.Random(sid)
    b = Builder(sid)
    # Core column (hexagonal, tapered both ways).
    rings = []
    for z, r in [(-2.2, 0.0), (-1.6, 0.35), (-0.6, 0.62), (0.0, 0.72), (0.6, 0.62), (1.3, 0.42)]:
        if r == 0.0:
            continue
        rings.append([V((math.cos(2 * math.pi * i / 6) * r, math.sin(2 * math.pi * i / 6) * r, z)) for i in range(6)])
    b.loft(rings, HULL, 0.0, point_start=V((0, 0, -2.6)), cap_end=True)
    # Great spire and satellite spires.
    b.prism_crystal(V((0, 0, 1.2)), V((0, 0, 1)), 3.2, 0.34, cls=GLASS, emis=0.85)
    for i in range(6):
        a = 2 * math.pi * i / 6 + 0.5
        b.prism_crystal(V((math.cos(a) * 0.45, math.sin(a) * 0.45, 1.1)), V((math.cos(a) * 0.3, math.sin(a) * 0.3, 1)),
                        1.4 + 0.3 * (i % 2), 0.13, cls=GLASS, emis=0.7, twist=a)
    b.prism_crystal(V((0, 0, -2.2)), V((0, 0, -1)), 1.1, 0.2, cls=GLASS, emis=0.8)
    # Habitation rings: hexagonal-section granite rings at two heights.
    for z, R, r in [(0.0, 2.1, 0.16), (-0.75, 1.55, 0.12), (0.7, 1.35, 0.1)]:
        b.torus(V((0, 0, z)), V((0, 0, 1)), R, r, seg=48, sides=6, cls=HULL)
        # Window band.
        b.torus(V((0, 0, z)), V((0, 0, 1)), R + r * 0.92, r * 0.25, seg=48, sides=4, cls=GLASS, emis=0.6)
    # Spokes and docking arms.
    for i in range(6):
        a = 2 * math.pi * i / 6
        d = V((math.cos(a), math.sin(a), 0))
        b.shard(d * 0.55, d, 1.65, 0.12, 0.09, cls=TRIM)
        if i % 2 == 0:
            arm0 = d * 2.2
            b.shard(arm0, d + V((0, 0, -0.05)), 1.4, 0.16, 0.1, cls=HULL)
            for k in range(3):
                p = arm0 + d * (0.35 + 0.35 * k)
                b.box(p + V((0, 0, -0.14)), (0.08, 0.08, 0.05), cls=GLASS, emis=1.0)
    # Buttress shards hanging below the main ring.
    for i in range(12):
        a = 2 * math.pi * (i + 0.5) / 12
        d = V((math.cos(a), math.sin(a), 0))
        b.shard(d * 1.9 + V((0, 0, -0.1)), d * 0.35 + V((0, 0, -1)), 0.9 + 0.3 * rnd.random(), 0.12, 0.07, cls=HULL)
    ob = b.to_object(sharp_angle=28)
    bev = ob.modifiers.new("bevel", 'BEVEL')
    bev.width = 0.012
    bev.segments = 1
    bev.limit_method = 'ANGLE'
    bev.angle_limit = math.radians(30)
    G.apply_modifiers(ob)
    return ob


def wreck_hulk(sid):
    """Large broken hull (orbital battlefield obstacle): a torn keel section with exposed ribs."""
    rnd = random.Random(sid)
    b = Builder(sid)
    L, W, H = 2.4, 0.55, 0.35
    # Two hull halves with a jagged gap.
    silex_loft(b, L, W, H, -L / 2, -L * 0.08, prof=[(0.0, 0.6, 0.7), (0.3, 1.0, 1.0), (0.85, 0.95, 0.85), (1.0, 0.7, 0.55)],
               cls=TRIM, point=False)
    silex_loft(b, L, W * 0.9, H * 0.9, L * 0.05, L / 2, prof=[(0.0, 0.8, 0.7), (0.2, 1.0, 1.0), (0.7, 0.6, 0.6), (0.9, 0.3, 0.3)],
               cls=TRIM)
    for i in range(7):
        y = -L * 0.12 + i * 0.05
        b.torus(V((0, y, 0)), V((0, 1, 0)), W * (0.75 + 0.1 * rnd.random()), 0.025, seg=10, sides=4, cls=HULL)
    for i in range(10):
        a = rnd.uniform(0, 2 * math.pi)
        b.shard(V((math.cos(a) * W * 0.6, rnd.uniform(-L * 0.4, L * 0.4), math.sin(a) * H * 0.7)),
                V((math.cos(a), rnd.uniform(-0.4, 0.4), math.sin(a))), rnd.uniform(0.15, 0.4), 0.05, 0.03, cls=HULL)
    b.ellipsoid(V((0, -L * 0.05, 0)), (W * 0.3, 0.1, H * 0.3), cls=GLASS, emis=0.35, u=10, v=6)
    return b.to_object(sharp_angle=30)


def asteroid(sid, radius=1.0, rough=0.28):
    rnd = random.Random(sid)
    import bmesh
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=4, radius=radius)
    from mathutils import noise
    off = V((rnd.uniform(-50, 50), rnd.uniform(-50, 50), rnd.uniform(-50, 50)))
    stretch = V((rnd.uniform(0.75, 1.25), rnd.uniform(0.75, 1.3), rnd.uniform(0.6, 1.0)))
    for v in bm.verts:
        n = v.co.normalized()
        d = noise.fractal(n * 1.6 + off, 0.6, 2.1, 5) * rough
        crater = 0.0
        v.co = V((n.x * stretch.x, n.y * stretch.y, n.z * stretch.z)) * radius * (1.0 + d + crater)
    me = bpy.data.meshes.new(sid)
    col = bm.loops.layers.float_color.new("Col")
    for f in bm.faces:
        for loop in f.loops:
            loop[col] = (TRIM, 0.0, 1.0, 1.0)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(sid, me)
    bpy.context.scene.collection.objects.link(ob)
    me.color_attributes.active_color = me.color_attributes["Col"]
    for p in me.polygons:
        p.use_smooth = True
    me.set_sharp_from_angle(angle=math.radians(50))
    return ob


# =====================================================================================================
# Pygmaei: war-foundry hulls. Riveted, stepped iron blocks, a ram or drill prow, exposed furnace stacks whose glowing
# throats read as heat (the slice's overclock doctrine), side sponsons and stubby gun barrels. Gritty and asymmetric.
# NEW ART INTERPRETATION for the vertical slice, pending the creator's approval (Docs/vertical-slice/DECISIONS.md).
# =====================================================================================================

def pyg_section(w, h):
    # Chamfered iron box: flat belly, sloped shoulders, a narrow deck.
    return [V((-w, 0, -h * 0.55)), V((-w * 0.62, 0, -h * 0.8)), V((w * 0.62, 0, -h * 0.8)), V((w, 0, -h * 0.55)),
            V((w * 0.92, 0, h * 0.3)), V((w * 0.48, 0, h * 0.62)), V((-w * 0.48, 0, h * 0.62)), V((-w * 0.92, 0, h * 0.3))]


def pyg_hull(b, L, W, H, y0, y1, z0=0.0, prof=None, cls=HULL, prow=0.18):
    prof = prof or [(0.0, 0.78, 0.78), (0.06, 1.0, 1.0), (0.5, 1.0, 0.96), (0.78, 0.8, 0.78), (0.94, 0.42, 0.5)]
    rings = []
    for (t, sw, sh) in prof:
        y = y0 + (y1 - y0) * t
        rings.append([p + V((0, y, z0)) for p in pyg_section(W * sw, H * sh)])
    return b.loft(rings, cls, 0.0, cap_start=True, point_end=V((0, y1 + L * prow * 0.2, z0 - H * 0.12)))


def pyg_stack(b, x, y, z, r, h, glow=1.4):
    """Furnace stack: a riveted trim collar, the iron flue, and a glowing throat at the top."""
    b.tube([V((x, y, z)), V((x, y, z + h * 0.15))], [r * 1.35, r * 1.35], sides=8, cls=TRIM, point_end=False)
    b.tube([V((x, y, z)), V((x, y, z + h))], [r, r * 0.86], sides=8, cls=HULL, point_end=False)
    b.tube([V((x, y, z + h)), V((x, y, z + h * 1.06))], [r * 0.8, r * 0.7], sides=8, cls=GLASS, emis=glow, point_end=False)


def pyg_gun(b, x, y, z, size, length, side=0):
    b.box(V((x, y, z)), (size, size * 1.1, size * 0.55), cls=TRIM, taper=0.8)
    for k in ((-0.4, 0.4) if size > 0.04 else (0.0,)):
        b.tube([V((x + k * size, y + size * 0.6, z + size * 0.15)), V((x + k * size, y + size * 0.6 + length, z + size * 0.18))],
               [size * 0.22, size * 0.18], sides=6, cls=TRIM, point_end=False)


def pyg_engines(b, y0, W, H, n, r, z=0.0):
    for i in range(n):
        x = (i - (n - 1) / 2) * W * 0.62
        b.tube([V((x, y0 + 0.02, z)), V((x, y0 - r * 1.6, z))], [r, r * 1.15], sides=10, cls=TRIM, point_end=False)
        b.tube([V((x, y0 - r * 1.6, z)), V((x, y0 - r * 1.75, z))], [r * 0.9, r * 0.8], sides=10, cls=GLASS, emis=1.6, point_end=False)


def pygmaei_ship(sid, p):
    rnd = random.Random(sid)
    b = Builder(sid)
    L, W, H = p["L"], p["W"], p["H"]
    y0, y1 = -L * 0.5, L * 0.5
    pyg_hull(b, L, W, H, y0, y1)
    # Raised rear citadel: a stepped block (taller, crowned with spikes on the command ship).
    tower = p.get("tower", 0.0)
    if tower > 0:
        b.box(V((0, y0 + L * 0.3, H * 0.62 + H * tower * 0.5)), (W * 0.42, L * 0.14, H * tower * 0.5), cls=HULL, taper=0.82)
        b.box(V((0, y0 + L * 0.33, H * 0.62 + H * tower * 1.05)), (W * 0.3, L * 0.07, H * tower * 0.12), cls=GLASS, emis=0.9, taper=0.9)
        if p.get("crown"):
            for k in range(5):
                a = (k - 2) * 0.32
                base = V((math.sin(a) * W * 0.28, y0 + L * 0.3, H * 0.62 + H * tower * 1.0))
                b.tube([base, base + V((math.sin(a) * 0.04, 0.0, H * 0.55))], [W * 0.05, W * 0.02], sides=5, cls=TRIM)
    # Ram or drill prow.
    if p.get("ram"):
        b.tube([V((0, y1 - L * 0.08, -H * 0.25)), V((0, y1 + L * p["ram"], -H * 0.3))], [W * 0.34, W * 0.08], sides=6, cls=TRIM)
    if p.get("drill"):
        for k in range(3):
            a = 2 * math.pi * k / 3
            b.tube([V((math.cos(a) * W * 0.18, y1 - L * 0.04, math.sin(a) * H * 0.18 - H * 0.1)), V((0, y1 + L * p["drill"], -H * 0.1))],
                   [W * 0.09, W * 0.02], sides=5, cls=TRIM)
    # Furnace stacks along the rear deck: the visible heat of the overclock doctrine.
    n = p.get("stacks", 2)
    for i in range(n):
        x = (i - (n - 1) / 2) * W * 0.5 + rnd.uniform(-0.01, 0.01)
        pyg_stack(b, x, y0 + L * (0.12 + 0.05 * (i % 2)), H * 0.45, W * 0.1, H * p.get("stack_h", 1.0))
    # Side sponsons with guns.
    for side in (-1, 1):
        if p.get("sponsons"):
            b.box(V((side * W * 1.05, y0 + L * 0.52, -H * 0.05)), (W * 0.28, L * 0.18, H * 0.3), cls=HULL, taper=0.85)
            pyg_gun(b, side * W * 1.1, y0 + L * 0.6, H * 0.28, W * 0.14, L * 0.16)
    # Deck guns and spinal ordnance.
    for k in range(p.get("guns", 1)):
        pyg_gun(b, 0, y0 + L * (0.5 + 0.12 * k), H * 0.6, W * (0.2 if p.get("heavy") else 0.15), L * (0.22 if p.get("heavy") else 0.14))
    if p.get("spinal"):
        b.tube([V((0, y0 + L * 0.2, -H * 0.82)), V((0, y1 + L * p["spinal"], -H * 0.82))], [W * 0.16, W * 0.12], sides=8, cls=TRIM, point_end=False)
        b.tube([V((0, y1 + L * p["spinal"], -H * 0.82)), V((0, y1 + L * p["spinal"] + 0.02, -H * 0.82))], [W * 0.1, W * 0.08], sides=8, cls=GLASS, emis=1.0, point_end=False)
    if p.get("barge"):
        # Artillery barge mortar wells.
        for k in range(p["barge"]):
            pyg_stack(b, (k - (p["barge"] - 1) / 2) * W * 0.6, y0 + L * 0.62, H * 0.5, W * 0.16, H * 0.5, glow=0.6)
    # Rivet bands: thin trim belts around the hull.
    for t in p.get("belts", (0.35, 0.65)):
        y = y0 + L * t
        b.loft([[q + V((0, y - 0.01, 0)) for q in pyg_section(W * 1.04, H * 1.02)], [q + V((0, y + 0.01, 0)) for q in pyg_section(W * 1.04, H * 1.02)]], TRIM, 0.0)
    # Prongs for the interceptor.
    if p.get("prongs"):
        for side in (-1, 1):
            b.shard(V((side * W * 0.7, y0 + L * 0.35, 0)), V((side * 0.08, 1, 0)), L * p["prongs"], W * 0.18, H * 0.25, cls=TRIM)
    pyg_engines(b, y0, W, H, p.get("engines", 2), W * 0.18, z=-H * 0.1)
    return b.to_object()


PYGMAEI = {
    "pygmaei_rift_interceptor":      dict(L=0.85, W=0.17, H=0.12, stacks=1, stack_h=0.7, guns=0, prongs=0.62, engines=1, belts=(0.5,)),
    "pygmaei_skirmish_frigate":      dict(L=1.15, W=0.24, H=0.17, stacks=2, guns=1, sponsons=True, ram=0.12, engines=2),
    "pygmaei_annihilator_destroyer": dict(L=1.45, W=0.25, H=0.18, stacks=2, guns=1, heavy=True, spinal=0.22, engines=2, belts=(0.3, 0.55, 0.75)),
    "pygmaei_furnace_cruiser":       dict(L=1.6, W=0.33, H=0.24, stacks=4, stack_h=1.25, guns=2, sponsons=True, drill=0.16, engines=3),
    "pygmaei_revenant_command_ship": dict(L=2.15, W=0.42, H=0.3, stacks=4, stack_h=1.4, guns=2, heavy=True, sponsons=True, ram=0.2,
                                          tower=1.6, crown=True, engines=4, belts=(0.25, 0.45, 0.65, 0.82)),
}


# =====================================================================================================
# Inhumanus: low-gravity corporate hulls. Smooth pale lenticular bodies with gold trim, gravity rings standing clear of
# the hull, detached floating segments held in place by fields, and prism emitters at the prow. Clean and symmetric.
# NEW ART INTERPRETATION for the vertical slice, pending the creator's approval (Docs/vertical-slice/DECISIONS.md).
# =====================================================================================================

def inhumanus_ship(sid, p):
    b = Builder(sid)
    L, W, H = p["L"], p["W"], p["H"]
    y0, y1 = -L * 0.5, L * 0.5
    # Lenticular body: an elongated ellipsoid, flattened, with a slimmer upper spine.
    b.ellipsoid(V((0, 0, 0)), (W, L * 0.5, H), u=20, v=14, cls=HULL)
    b.ellipsoid(V((0, -L * 0.05, H * 0.55)), (W * 0.32, L * 0.36, H * 0.42), u=14, v=10, cls=HULL)
    # Gold trim keel line.
    b.shard(V((0, y0 + L * 0.08, -H * 0.72)), V((0, 1, 0)), L * 0.84, W * 0.08, H * 0.16, up=V((1, 0, 0)), cls=TRIM)
    # Gravity rings, standing clear of the hull around the long axis.
    for (t, rs) in p.get("rings", ((0.42, 1.55),)):
        b.torus(V((0, y0 + L * t, 0)), V((0, 1, 0)), W * rs, W * 0.06, seg=40, sides=6, cls=TRIM)
        b.torus(V((0, y0 + L * t, 0)), V((0, 1, 0)), W * rs * 0.94, W * 0.025, seg=40, sides=5, cls=GLASS, emis=0.9)
    # Detached floating segments (low-gravity architecture): pods held beside the hull, not touching it.
    for k in range(p.get("pods", 0)):
        side = -1 if k % 2 == 0 else 1
        y = y0 + L * (0.3 + 0.18 * (k // 2))
        b.ellipsoid(V((side * W * 1.75, y, 0)), (W * 0.26, L * 0.13, H * 0.3), u=12, v=8, cls=HULL)
        b.ellipsoid(V((side * W * 1.75, y + L * 0.1, 0)), (W * 0.08, L * 0.03, H * 0.08), u=8, v=6, cls=GLASS, emis=1.1)
    # Prism emitter at the prow (longer on the arbitration lance and the flagship).
    b.prism_crystal(V((0, y1 - L * 0.02, 0)), V((0, 1, 0)), L * p.get("prism", 0.12), W * 0.16, sides=6, tip=0.45, cls=GLASS, emis=1.4)
    # Swept fins.
    for side in (-1, 1):
        fin = p.get("fins", 0.0)
        if fin > 0:
            b.shard(V((side * W * 0.6, y0 + L * 0.22, 0)), V((side * 0.9, -0.5, 0)), L * fin, W * 0.25, H * 0.06, cls=TRIM)
    # Executive tower: a tall blade fin above the spine.
    if p.get("tower"):
        b.shard(V((0, y0 + L * 0.3, H * 0.75)), V((0, 0.35, 1)), H * p["tower"], W * 0.32, W * 0.06, up=V((1, 0, 0)), cls=TRIM)
        b.ellipsoid(V((0, y0 + L * 0.38, H * (0.8 + p["tower"] * 0.45))), (W * 0.12, W * 0.12, W * 0.12), u=10, v=8, cls=GLASS, emis=1.2)
    # Ventral hangar for escort tokens.
    if p.get("hangar"):
        b.box(V((0, y0 + L * 0.5, -H * 0.95)), (W * 0.55, L * 0.22, H * 0.22), cls=TRIM, taper=0.8)
        b.box(V((0, y0 + L * 0.62, -H * 1.08)), (W * 0.4, L * 0.02, H * 0.08), cls=GLASS, emis=1.0)
    # Drive: a glowing ring at the stern.
    b.torus(V((0, y0 + L * 0.01, 0)), V((0, 1, 0)), W * 0.42, W * 0.08, seg=28, sides=6, cls=GLASS, emis=1.5)
    return b.to_object()


INHUMANUS = {
    "token_inhumanus_escort":          dict(L=0.55, W=0.13, H=0.07, rings=(), fins=0.2, prism=0.1),
    "inhumanus_mercator_corvette":     dict(L=0.95, W=0.2, H=0.09, rings=(), fins=0.28, prism=0.12),
    "inhumanus_proxy_frigate":         dict(L=1.15, W=0.23, H=0.11, rings=((0.5, 1.5),), hangar=True, prism=0.1),
    "inhumanus_arbitration_destroyer": dict(L=1.4, W=0.21, H=0.1, rings=((0.35, 1.6),), pods=2, prism=0.32),
    "inhumanus_parallax_cruiser":      dict(L=1.55, W=0.3, H=0.15, rings=((0.3, 1.55), (0.62, 1.4)), pods=2, prism=0.14),
    "inhumanus_executive_flagship":    dict(L=2.2, W=0.36, H=0.18, rings=((0.28, 1.7), (0.58, 1.55)), pods=4, prism=0.28, tower=1.8, fins=0.3),
}


# =====================================================================================================

def build_all(only=None):
    jobs = []
    for sid, p in SILEX.items():
        jobs.append((sid, lambda sid=sid, p=p: silex_ship(sid, p)))
    for sid, p in CRAIGON.items():
        jobs.append((sid, lambda sid=sid, p=p: craigon_ship(sid, p)))
    for sid, p in PYGMAEI.items():
        jobs.append((sid, lambda sid=sid, p=p: pygmaei_ship(sid, p)))
    for sid, p in INHUMANUS.items():
        jobs.append((sid, lambda sid=sid, p=p: inhumanus_ship(sid, p)))
    jobs.append(("token_craigon_auxiliary_orbital", lambda: craigon_auxiliary_hulk("token_craigon_auxiliary_orbital")))
    jobs.append(("station_silex_citadel", lambda: silex_citadel_station("station_silex_citadel")))
    jobs.append(("wreck_hulk_a", lambda: wreck_hulk("wreck_hulk_a")))
    for i in range(4):
        jobs.append((f"asteroid_{i}", lambda i=i: asteroid(f"asteroid_{i}", 1.0, 0.22 + 0.06 * i)))
    if only:
        jobs = [j for j in jobs if j[0] in only]
    return jobs


def render_preview(ob, path):
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_EEVEE' if 'BLENDER_EEVEE' in [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items] else 'BLENDER_WORKBENCH'
    scene.render.resolution_x, scene.render.resolution_y = 900, 600
    scene.render.film_transparent = False
    mat = bpy.data.materials.get("RR_Preview")
    if mat is None:
        mat = bpy.data.materials.new("RR_Preview")
        mat.use_nodes = True
        nt = mat.node_tree
        bsdf = nt.nodes.get("Principled BSDF")
        attr = nt.nodes.new("ShaderNodeVertexColor")
        attr.layer_name = "Col"
        sep = nt.nodes.new("ShaderNodeSeparateColor")
        nt.links.new(attr.outputs["Color"], sep.inputs["Color"])
        ramp = nt.nodes.new("ShaderNodeValToRGB")
        ramp.color_ramp.elements[0].color = (0.6, 0.6, 0.62, 1)
        ramp.color_ramp.elements[1].color = (0.25, 0.55, 1.0, 1)
        nt.links.new(sep.outputs["Red"], ramp.inputs["Fac"])
        mul = nt.nodes.new("ShaderNodeMixRGB")
        mul.blend_type = 'MULTIPLY'
        mul.inputs["Fac"].default_value = 1.0
        nt.links.new(ramp.outputs["Color"], mul.inputs["Color1"])
        nt.links.new(sep.outputs["Blue"], mul.inputs["Color2"])
        nt.links.new(mul.outputs["Color"], bsdf.inputs["Base Color"])
        bsdf.inputs["Roughness"].default_value = 0.5
    ob.data.materials.clear()
    ob.data.materials.append(mat)
    bb = [ob.matrix_world @ V(c) for c in ob.bound_box]
    size = max((max(v[i] for v in bb) - min(v[i] for v in bb)) for i in range(3))
    center = sum(bb, V()) / 8
    cam_data = bpy.data.cameras.new("cam")
    cam = bpy.data.objects.new("cam", cam_data)
    scene.collection.objects.link(cam)
    cam.location = center + V((size * 1.35, -size * 1.25, size * 0.7))
    cam.rotation_euler = (center - cam.location).to_track_quat('-Z', 'Y').to_euler()
    scene.camera = cam
    sun_d = bpy.data.lights.new("sun", 'SUN')
    sun_d.energy = 4.0
    sun = bpy.data.objects.new("sun", sun_d)
    sun.rotation_euler = (math.radians(50), math.radians(10), math.radians(-40))
    scene.collection.objects.link(sun)
    scene.world.use_nodes = True
    bg = scene.world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.02, 0.022, 0.03, 1)
        bg.inputs[1].default_value = 1.0
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    for o in (cam, sun):
        bpy.data.objects.remove(o)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    only = None
    if "--only" in argv:
        only = set(argv[argv.index("--only") + 1].split(","))
    bake = "--no-bake" not in argv
    render = "--render" in argv
    os.makedirs(OUT_FBX, exist_ok=True)
    os.makedirs(os.path.join(OUT_SRC, "renders"), exist_ok=True)
    G.reset_scene()
    built = []
    for sid, fn in build_all(only):
        ob = fn()
        tris = sum(len(p.vertices) - 2 for p in ob.data.polygons)
        if bake:
            G.bake_ao(ob, samples=32)
        G.export_fbx(ob, os.path.join(OUT_FBX, sid + ".fbx"))
        dims = ob.dimensions
        print(f"[rr_ships] {sid}: {tris} tris, dims {dims.x:.2f} x {dims.y:.2f} x {dims.z:.2f}")
        built.append(ob)
    if render:
        for ob in built:
            for o in built:
                o.hide_render = o is not ob
            render_preview(ob, os.path.join(OUT_SRC, "renders", ob.name + ".png"))
    for i, ob in enumerate(built):
        ob.location.x = (i % 6) * 3.0
        ob.location.y = (i // 6) * 3.5
        ob.hide_render = False
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT_SRC, "rr_ships.blend" if not only else "rr_ships_partial.blend"))
    print("[rr_ships] done", len(built))


if __name__ == "__main__":
    main()