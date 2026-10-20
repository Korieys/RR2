import bpy,pathlib,sys,json,math
from mathutils import Vector
R=pathlib.Path(__file__).parent;out=R/'poses';out.mkdir(exist_ok=True)
ids=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
for f in sorted((R/'Generated').iterdir()):
 if ids and f.name not in ids:continue
 bpy.ops.wm.open_mainfile(filepath=str(f/(f.name+'.blend')))
 s=bpy.context.scene;rig=bpy.data.objects['Rig'];mesh=bpy.data.objects['Body_LOD0']
 for o in s.objects:
  if o.type=='MESH':o.hide_render=not o.name.endswith('LOD0')
 s.render.engine='BLENDER_WORKBENCH';s.display.shading.light='STUDIO';s.display.shading.color_type='TEXTURE';s.display.shading.show_cavity=True
 if not s.world:s.world=bpy.data.worlds.new('Review')
 s.world.color=(.045,.055,.075);s.render.resolution_x=600;s.render.resolution_y=600;s.render.resolution_percentage=100
 bounds=[mesh.matrix_world@Vector(v) for v in mesh.bound_box];center=sum(bounds,Vector())/8
 bpy.ops.object.camera_add(location=center+Vector((1,-5,1)));cam=bpy.context.object;cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=max(mesh.dimensions)*1.28;s.camera=cam
 for clip,t in [('Idle',0),('Move',.23),('Attack',.38),('Ability',.5),('Hit',.18),('Death',.72)]:
  a=bpy.data.actions[clip];rig.animation_data.action=a;rig.animation_data.action_slot=a.slots[0];s.frame_set(round(a.frame_range.x+(a.frame_range.y-a.frame_range.x)*t))
  s.render.filepath=str(out/(f.name+'_'+clip+'.png'));bpy.ops.render.render(write_still=True)
 print('REVIEW',f.name,flush=True)

