"""Rule and Ruin vertical slice: ship-design reference renders from the actual game assets (Blender 5.x).

Run:  blender -b --factory-startup --python Tools/Blender/rr_design_sheet.py -- <out dir>
Imports the FBX files the game ships (Assets/RR/Art/Models) and renders, with no repainting or retouching:
  lineup_<faction>_<view>.png   the five slice classes side by side, flat black on white (top and side), to scale;
  lineup_<faction>_small.png    the same top view at 96 px high, the size a ship reads at on the battle map;
  config_<flagship>.png         the flagship hull alone and with each module, top and three-quarter views;
  shaded_<faction>.png          a plainly lit three-quarter view of the five classes, to scale.
A digital reference only: the meshes are game models, not print- or manufacturing-ready geometry.
"""
import bpy, math, os, sys
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rr_mounts

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
MODELS = os.path.join(ROOT, "Assets", "RR", "Art", "Models")
OUT = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else os.path.join(HERE, "out", "design_sheet")
os.makedirs(OUT, exist_ok=True)

FACTIONS = {
    "pygmaei": ["pygmaei_revenant_command_ship", "pygmaei_furnace_cruiser", "pygmaei_annihilator_destroyer", "pygmaei_skirmish_frigate", "pygmaei_rift_interceptor"],
    "inhumanus": ["inhumanus_executive_flagship", "inhumanus_parallax_cruiser", "inhumanus_arbitration_destroyer", "inhumanus_proxy_frigate", "inhumanus_mercator_corvette"],
}
MODULES = ["afterburners", "ablative_plating", "heavy_battery"]


def clear():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def load(name):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(MODELS, name + ".fbx"))
    objs = [o for o in bpy.data.objects if o not in before]
    meshes = [o for o in objs if o.type == 'MESH']
    root = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(root)
    for o in objs:
        if o.parent is None:
            o.parent = root
    return root, meshes


def bounds(meshes):
    pts = [m.matrix_world @ Vector(c) for m in meshes for c in m.bound_box]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return lo, hi


def attach_module(host_meshes, module, fit):
    """As the game does (ShipModules.Attach): the module at the hull's bounds centre, scaled by the hull's length, then
    fitted with the same ray-cast measurement that generates ShipMounts.g.cs (taken on the hull alone in its scene)."""
    lo, hi = bounds(host_meshes)
    size = hi - lo
    length = max(size.x, size.y)
    root, meshes = load("module_" + module)
    bpy.context.view_layer.update()
    sx = fit["afterburner_width"] if module == "afterburners" else fit["plating_width"] if module == "ablative_plating" else 1.0
    root.scale = (length * sx, length, length)
    root.location = (lo + hi) / 2
    if module == "heavy_battery":
        root.location.z += fit["battery_lift"] * length
    return root, meshes


def setup_render(w, h, silhouette):
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_WORKBENCH'
    sc.render.resolution_x, sc.render.resolution_y = w, h
    sc.render.film_transparent = False
    sh = sc.display.shading
    if silhouette:
        sh.light = 'FLAT'; sh.color_type = 'SINGLE'; sh.single_color = (0, 0, 0)
        sh.show_object_outline = False; sh.show_cavity = False
        sc.world = sc.world or bpy.data.worlds.new("w")
        sh.background_type = 'VIEWPORT'; sh.background_color = (1, 1, 1)
    else:
        sh.light = 'STUDIO'; sh.color_type = 'MATERIAL'; sh.show_cavity = True; sh.cavity_type = 'WORLD'
        sh.background_type = 'VIEWPORT'; sh.background_color = (0.82, 0.83, 0.85)
    sc.view_settings.view_transform = 'Standard'


def camera(direction, lo, hi, ortho=True, margin=1.15):
    sc = bpy.context.scene
    cam_data = bpy.data.cameras.new("cam")
    cam = bpy.data.objects.new("cam", cam_data)
    sc.collection.objects.link(cam)
    sc.camera = cam
    centre = (lo + hi) / 2
    size = hi - lo
    d = Vector(direction).normalized()
    cam.location = centre + d * (size.length * 3 + 10)
    cam.rotation_euler = (centre - cam.location).to_track_quat('-Z', 'Y').to_euler()
    if ortho:
        # Frame the bounding box as the camera sees it: project its corners on the camera's right and up axes.
        cam_data.type = 'ORTHO'
        bpy.context.view_layer.update()
        rot = cam.rotation_euler.to_matrix()
        right, up = rot @ Vector((1, 0, 0)), rot @ Vector((0, 1, 0))
        corners = [Vector((x, y, z)) for x in (lo.x, hi.x) for y in (lo.y, hi.y) for z in (lo.z, hi.z)]
        need_w = max(c.dot(right) for c in corners) - min(c.dot(right) for c in corners)
        need_h = max(c.dot(up) for c in corners) - min(c.dot(up) for c in corners)
        aspect = sc.render.resolution_x / sc.render.resolution_y
        cam_data.ortho_scale = max(need_w, need_h * aspect) * margin
    else:
        cam_data.lens = 50
        cam.location = centre + d * size.length * 1.6
        cam.rotation_euler = (centre - cam.location).to_track_quat('-Z', 'Y').to_euler()
    cam_data.clip_end = 10000
    return cam


def render(path):
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    print("wrote", path)


def lineup(faction, ids):
    clear()
    placed = []
    x = 0.0
    for i in ids:
        root, meshes = load(i)
        bpy.context.view_layer.update()
        lo, hi = bounds(meshes)
        width = hi.x - lo.x
        root.location.x += x - lo.x
        bpy.context.view_layer.update()
        x += width + 0.35 * max(1.0, width)
        placed += meshes
    lo, hi = bounds(placed)
    setup_render(2400, 700, True)
    camera((0, 0, 1), lo, hi); render(os.path.join(OUT, f"lineup_{faction}_top.png"))
    camera((0, -1, 0.0001), lo, hi); render(os.path.join(OUT, f"lineup_{faction}_side.png"))
    setup_render(330, 96, True)
    camera((0, 0, 1), lo, hi); render(os.path.join(OUT, f"lineup_{faction}_small.png"))
    setup_render(2400, 900, False)
    camera((0.55, -1, 0.75), lo, hi, ortho=True, margin=1.08); render(os.path.join(OUT, f"shaded_{faction}.png"))


def configurations(flagship):
    clear()
    fit = rr_mounts.measure(load(flagship)[1])   # measured alone, so no other ship intercepts the rays
    clear()
    placed = []
    x = 0.0
    variants = [None] + MODULES
    for v in variants:
        root, meshes = load(flagship)
        bpy.context.view_layer.update()
        group = list(meshes)
        if v:
            mroot, mmeshes = attach_module(meshes, v, fit)
            mroot.parent = root
            mroot.matrix_parent_inverse = root.matrix_world.inverted()
            group += mmeshes
        bpy.context.view_layer.update()
        lo, hi = bounds(group)
        width = hi.x - lo.x
        root.location.x += x - lo.x
        bpy.context.view_layer.update()
        x += width * 1.5
        placed += group
    lo, hi = bounds(placed)
    setup_render(2400, 760, False)
    camera((0, 0, 1), lo, hi); render(os.path.join(OUT, f"config_{flagship}_top.png"))
    camera((0.55, -1, 0.75), lo, hi, ortho=True, margin=1.08); render(os.path.join(OUT, f"config_{flagship}_3q.png"))


for f, ids in FACTIONS.items():
    lineup(f, ids)
    configurations(ids[0])
print("design sheet complete:", OUT)
