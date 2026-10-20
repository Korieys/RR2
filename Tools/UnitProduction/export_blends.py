"""Blender batch export of the editable unit sources into the Unity asset folder."""
import bpy,pathlib,sys,argparse,shutil
args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project',type=pathlib.Path,required=True);p.add_argument('ids',nargs='*');opt=p.parse_args(args)
for folder in sorted((opt.project/'ArtSource/Units').iterdir()):
    if not folder.is_dir() or (opt.ids and folder.name not in opt.ids):continue
    src=folder/(folder.name+'.blend')
    if not src.exists():continue
    bpy.ops.wm.open_mainfile(filepath=str(src));bpy.ops.object.select_all(action='SELECT')
    out=opt.project/'Assets/RR/Art/Units'/folder.name;out.mkdir(parents=True,exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=str(out/(folder.name+'.fbx')),use_selection=True,object_types={'MESH','ARMATURE','EMPTY'},add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=.5,axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,apply_scale_options='FBX_SCALE_ALL',apply_unit_scale=True)
    for name in ['albedo.png','normal.png','orm.png','stats.json']:shutil.copyfile(folder/name,out/name)
    print('EXPORTED',folder.name,flush=True)
