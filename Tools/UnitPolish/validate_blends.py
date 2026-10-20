import bpy,pathlib,json,numpy as np,sys
R=pathlib.Path(__file__).parent;rows=[]
wanted=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
for folder in sorted((R/'Generated').iterdir()):
 if wanted and folder.name not in wanted:continue
 bpy.ops.wm.open_mainfile(filepath=str(folder/(folder.name+'.blend')))
 rig=bpy.data.objects['Rig'];mesh=bpy.data.objects['Body_LOD0'];s=bpy.context.scene
 raw=np.empty(len(mesh.data.vertices)*3);mesh.data.vertices.foreach_get('co',raw);raw=raw.reshape(-1,3)
 edges=np.array([list(e.vertices) for e in mesh.data.edges]);base=np.linalg.norm(raw[edges[:,0]]-raw[edges[:,1]],axis=1)
 mask=base>.003;edges=edges[mask];base=base[mask]
 info={'id':folder.name,'clips':{}}
 for name in ['Idle','Move','Attack','Ability','Hit','Death']:
  a=bpy.data.actions[name];rig.animation_data.action=a;rig.animation_data.action_slot=a.slots[0];metrics=[]
  for t in [0,.18,.27,.38,.5,.65,.8,1]:
   s.frame_set(round(a.frame_range.x+(a.frame_range.y-a.frame_range.x)*t));o=mesh.evaluated_get(bpy.context.evaluated_depsgraph_get());m=o.to_mesh()
   p=np.empty(len(m.vertices)*3);m.vertices.foreach_get('co',p);p=p.reshape(-1,3)
   ratios=np.linalg.norm(p[edges[:,0]]-p[edges[:,1]],axis=1)/base
   metrics.append({'t':t,'p99_stretch':round(float(np.quantile(ratios,.99)),3),'max_stretch':round(float(ratios.max()),2),'edges_over_2x':int(np.sum(ratios>2)),'min_z':round(float(p[:,2].min()),3)})
   o.to_mesh_clear()
  info['clips'][name]=metrics
 rows.append(info);print('AUDIT',folder.name,max(x['p99_stretch'] for c in info['clips'].values() for x in c),flush=True)
(R/'deformation-audit.json').write_text(json.dumps(rows,indent=2))

