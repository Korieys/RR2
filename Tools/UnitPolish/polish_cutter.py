import bpy,pathlib,json,math,numpy as np
from mathutils import Quaternion
R=pathlib.Path(__file__).parent;uid='silex_facet_ram';folder=R/'Generated'/uid
bpy.ops.wm.open_mainfile(filepath=str(folder/(uid+'.blend')));rig=bpy.data.objects['Rig']
m=bpy.data.objects['Body_LOD0'];points=np.array([v.co[:] for v in m.data.vertices])
mask=(points[:,2]<.6)&(points[:,1]<-.2);center=points[mask].mean(0);center[2]=.4
bpy.context.view_layer.objects.active=rig;bpy.ops.object.mode_set(mode='EDIT')
bone=rig.data.edit_bones.new('Cutter');bone.head=center;bone.tail=center+np.array([0,0,.22]);bone.parent=rig.data.edit_bones['Core'];bpy.ops.object.mode_set(mode='OBJECT')
for name in ['Body_LOD0','Body_LOD1','Body_LOD2']:
 mesh=bpy.data.objects[name];n=len(mesh.data.vertices);p=list(range(n))
 def find(i):
  while p[i]!=i:p[i]=p[p[i]];i=p[i]
  return i
 for edge in mesh.data.edges:
  a,b=edge.vertices;p[find(a)]=find(b)
 groups={}
 for i in range(n):groups.setdefault(find(i),[]).append(i)
 cutter=mesh.vertex_groups.new(name='Cutter')
 for ids in groups.values():
  a=np.array([mesh.data.vertices[i].co[:] for i in ids]);c=a.mean(0)
  if c[2]<.56 and c[1]<-.2:
   for g in mesh.vertex_groups:
    if g!=cutter:g.remove(ids)
   cutter.add(ids,1,'REPLACE')
for a in bpy.data.actions:
 rig.animation_data.action=a;rig.animation_data.action_slot=a.slots[0];bone=rig.pose.bones['Cutter'];bone.rotation_mode='QUATERNION'
 start,end=map(int,a.frame_range)
 for frame in range(start,end+1):
  t=(frame-start)/max(1,end-start)
  if a.name=='Move':angle=2*math.pi*t
  elif a.name=='Attack':angle=6*math.pi*(t*t*(3-2*t))
  elif a.name=='Ability':angle=4*math.pi*(t*t*(3-2*t))
  elif a.name=='Idle':angle=.025*math.sin(t*2*math.pi)
  else:angle=.08*math.sin(t*math.pi)
  bone.rotation_quaternion=Quaternion((0,1,0),angle);bone.keyframe_insert('rotation_quaternion',frame=frame)
a=bpy.data.actions['Idle'];rig.animation_data.action=a;rig.animation_data.action_slot=a.slots[0];bpy.context.scene.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=str(folder/(uid+'.blend')),compress=True);bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=str(folder/(uid+'.fbx')),use_selection=True,object_types={'MESH','ARMATURE','EMPTY'},add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=.15,axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,apply_scale_options='FBX_SCALE_ALL',apply_unit_scale=True)
stats=json.loads((folder/'stats.json').read_text());stats['bones']=len(rig.data.bones);stats['animation_role']='tracked cutter';(folder/'stats.json').write_text(json.dumps(stats,indent=2))

