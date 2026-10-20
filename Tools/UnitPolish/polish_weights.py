"""Keep independent armor and equipment pieces rigid; smooth continuous joint skins."""
import bpy,pathlib,json,numpy as np,sys
R=pathlib.Path(__file__).parent
wanted=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
for folder in sorted((R/'Generated').iterdir()):
 stats=json.loads((folder/'stats.json').read_text())
 if wanted==['infantry']:
  if stats['category']!='Infantry':continue
 elif wanted==['vehicles']:
  if stats['category']!='GroundVehicles':continue
 elif wanted and folder.name not in wanted:continue
 if stats['category']=='Ships':continue
 bpy.ops.wm.open_mainfile(filepath=str(folder/(folder.name+'.blend')));rig=bpy.data.objects['Rig']
 report=[]
 for name in ['Body_LOD0','Body_LOD1','Body_LOD2']:
  mesh=bpy.data.objects[name];n=len(mesh.data.vertices);group_names=[g.name for g in mesh.vertex_groups];weights=np.zeros((n,len(group_names)))
  points=np.array([v.co[:] for v in mesh.data.vertices]);h=points[:,2].max()
  for v in mesh.data.vertices:
   for g in v.groups:weights[v.index,g.group]=g.weight
  edges=np.array([list(e.vertices) for e in mesh.data.edges]);parent=np.arange(n)
  def find(i):
   while parent[i]!=i:parent[i]=parent[parent[i]];i=parent[i]
   return i
  for a,b in edges:parent[find(a)]=find(b)
  components={}
  for i in range(n):components.setdefault(find(i),[]).append(i)
  fixed=np.zeros(n,dtype=bool);rigid=0
  for indices in components.values():
   ids=np.array(indices);p=points[ids];lo=p.min(0);hi=p.max(0);center=p.mean(0)
   if len(ids)<max(80,n*.12):
    dominant=int(weights[ids].mean(0).argmax())
    if stats['category']=='Infantry' and hi[2]<h*.19 and lo[2]<h*.04:
     foot='Toe'+('L' if center[0]<0 else 'R')
     if foot in group_names:dominant=group_names.index(foot)
    weights[ids]=0;weights[ids,dominant]=1;fixed[ids]=True;rigid+=1
  # Continuous body pieces have gradual joint weights. Isolated plated components stay solid.
  counts=np.zeros(n);np.add.at(counts,edges[:,0],1);np.add.at(counts,edges[:,1],1);counts=np.maximum(1,counts)
  for iteration in range(144):
   accum=np.zeros_like(weights);np.add.at(accum,edges[:,0],weights[edges[:,1]]);np.add.at(accum,edges[:,1],weights[edges[:,0]])
   blend=weights*.35+.65*accum/counts[:,None];weights[~fixed]=blend[~fixed]
  top=np.argsort(weights,axis=1)[:,-4:];limited=np.zeros_like(weights);np.put_along_axis(limited,top,np.take_along_axis(weights,top,axis=1),axis=1)
  weights=limited/np.maximum(limited.sum(1)[:,None],1e-8)
  mesh.vertex_groups.clear();groups=[mesh.vertex_groups.new(name=n) for n in group_names]
  for i,row in enumerate(weights):
   for gi in np.flatnonzero(row>.00001):groups[gi].add([i],float(row[gi]),'REPLACE')
  report.append({'mesh':name,'rigid_components':rigid,'rigid_vertices':int(fixed.sum()),'joint_vertices':int((~fixed).sum()),'max_influences':4})
 a=bpy.data.actions['Idle'];rig.animation_data.action=a;rig.animation_data.action_slot=a.slots[0];bpy.context.scene.frame_set(1)
 bpy.ops.wm.save_as_mainfile(filepath=str(folder/(folder.name+'.blend')),compress=True)
 bpy.ops.object.select_all(action='SELECT')
 bpy.ops.export_scene.fbx(filepath=str(folder/(folder.name+'.fbx')),use_selection=True,object_types={'MESH','ARMATURE','EMPTY'},add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=.15,axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,apply_scale_options='FBX_SCALE_ALL',apply_unit_scale=True)
 (folder/'skin_review.json').write_text(json.dumps(report,indent=2));print('REFINED SKIN',folder.name,flush=True)

