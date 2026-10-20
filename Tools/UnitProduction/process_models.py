"""Reproducible GLB -> normalized skinned FBX/LODs/clips. Source GLBs remain immutable."""
import bpy, math, json, pathlib, sys, time, argparse
import numpy as np
from mathutils import Vector, Matrix
ROOT=pathlib.Path(__file__).parent
args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('indices',type=int,nargs='*');parser.add_argument('--source',type=pathlib.Path,default=ROOT.parent/'unit-assets-review'/'Silex_and_Craigon_Renamed')
parser.add_argument('--output',type=pathlib.Path,default=ROOT/'Generated');parser.add_argument('--force',action='store_true')
options=parser.parse_args(args);SOURCE=options.source;OUT=options.output;OUT.mkdir(parents=True,exist_ok=True)
rows=json.loads((SOURCE/'asset_manifest.json').read_text())['assets']
wanted=set(options.indices) if options.indices else set(range(1,50))

def apply(obj):
    bpy.context.view_layer.objects.active=obj;obj.select_set(True)
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
def trim(obj,target):
    bpy.context.view_layer.objects.active=obj
    mod=obj.modifiers.new('Silhouette reduction','DECIMATE');mod.ratio=min(1,target/max(1,len(obj.data.polygons)));mod.use_collapse_triangulate=True
    bpy.ops.object.modifier_apply(modifier=mod.name)
def mesh_positions(obj):
    a=np.empty(len(obj.data.vertices)*3,dtype=np.float32);obj.data.vertices.foreach_get('co',a);return a.reshape(-1,3)
def set_positions(obj,a):obj.data.vertices.foreach_set('co',a.astype(np.float32).ravel());obj.data.update()
def normalize(obj,row):
    a=mesh_positions(obj);a-=np.mean(a,axis=0)
    if row['category']=='Infantry':
        if row['source_index'] not in (26,48): a=a@np.array([[0,1,0],[-1,0,0],[0,0,1]]).T
        span=np.ptp(a,axis=0);a*=2.0/span[2];a[:,2]-=a[:,2].min();a[:,:2]-=(a[:,:2].max(0)+a[:,:2].min(0))/2
    else:
        # PCA levels image-generated hulls that arrived pitched/rolled; choose dorsal axis nearest source up.
        vals,vec=np.linalg.eigh(np.cov(a.T));fwd=vec[:,-1]
        up=vec[:,0] if row['category']=='Ships' else vec[:,0] if abs(vec[2,0])>abs(vec[2,1]) else vec[:,1]
        if up[2]<0:up=-up
        # Nose tapers: compare radial spread near the two ends, pointing the narrow end forward.
        proj=a@fwd; cross=a-proj[:,None]*fwd
        minus=np.percentile(np.linalg.norm(cross[proj<np.quantile(proj,.12)],axis=1),80)
        plus=np.percentile(np.linalg.norm(cross[proj>np.quantile(proj,.88)],axis=1),80)
        if minus<plus:fwd=-fwd
        right=np.cross(fwd,up);right/=np.linalg.norm(right);up=np.cross(right,fwd)
        if row['category']=='GroundVehicles':
            # Keep walkers vertical; only horizontal alignment for grounded creatures.
            up=np.array([0.,0.,1.]);fwd[2]=0;fwd/=np.linalg.norm(fwd);right=np.cross(fwd,up)
        a=np.column_stack((a@right,-a@fwd,a@up));a-= (a.max(0)+a.min(0))/2
        a*=2.4/max(np.ptp(a,axis=0))
        if row['category']=='GroundVehicles':a[:,2]-=a[:,2].min()
    set_positions(obj,a)
def skeleton(mesh,row):
    a=mesh_positions(mesh);lo=a.min(0);hi=a.max(0);h=hi[2]-lo[2];w=hi[0]-lo[0]
    arm=bpy.data.armatures.new('UnitRig');rig=bpy.data.objects.new('Rig',arm);bpy.context.collection.objects.link(rig)
    bpy.context.view_layer.objects.active=rig;mesh.select_set(False);rig.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
    desc={}
    def bone(name,head,tail,parent=None):
        b=arm.edit_bones.new(name);b.head=head;b.tail=tail
        if parent:b.parent=arm.edit_bones[parent]
        desc[name]=(np.array(head),np.array(tail));return b
    bone('Root',(0,0,0),(0,0,.15))
    bone('Core',(0,0,h*.43),(0,0,h*.73),'Root')
    if row['category']=='Infantry':
        bone('Head',(0,0,h*.80),(0,0,h*.97),'Core')
        for side,s in [('L',-1),('R',1)]:
            bone('Arm'+side,(s*w*.24,0,h*.72),(s*w*.40,0,h*.49),'Core')
            bone('Hand'+side,(s*w*.40,0,h*.49),(s*w*.43,-.04,h*.34),'Arm'+side)
            bone('Leg'+side,(s*w*.13,0,h*.43),(s*w*.17,0,h*.22),'Root')
            bone('Foot'+side,(s*w*.17,0,h*.22),(s*w*.19,-.10,h*.025),'Leg'+side)
    elif row['category']=='GroundVehicles' and ('walker' in row['suggested_unit_id'] or 'crawler' in row['suggested_unit_id'] or 'hauler' in row['suggested_unit_id']):
        for s in (-1,1):
            for f in (-1,1):
                bone(f'Strut{s}_{f}',(s*w*.23,f*.3,h*.55),(s*w*.40,f*.42,h*.08),'Root')
    bpy.ops.object.mode_set(mode='OBJECT')
    return rig,desc
def weight(obj,rig,desc,category):
    groups={n:obj.vertex_groups.new(name=n) for n in desc}
    a=mesh_positions(obj);h=a[:,2].max()-min(0,a[:,2].min());w=np.ptp(a[:,0])
    if category=='Ships':groups['Core'].add(list(range(len(a))),1,'REPLACE')
    else:
        # Capsule distance weighting, gated by anatomy to avoid pulling the abdomen into a hand.
        for i,p in enumerate(a):
            choices=['Core']
            if category=='Infantry':
                side='L' if p[0]<0 else 'R'
                if p[2]>h*.82:choices=['Head','Core']
                elif p[2]<h*.43:choices=['Leg'+side,'Foot'+side,'Core']
                elif abs(p[0])>w*.20 and p[2]<h*.81:choices=['Arm'+side,'Hand'+side,'Core']
            elif p[2]<h*.55:choices=[n for n in desc if n.startswith('Strut')]+['Core']
            distances=[]
            for n in choices:
                head,tail=desc[n];v=tail-head;t=np.clip(np.dot(p-head,v)/max(1e-8,np.dot(v,v)),0,1)
                distances.append((np.linalg.norm(p-head-v*t),n))
            distances.sort();nearest=distances[:2];weights=np.array([1/(d+.035)**4 for d,n in nearest]);weights/=weights.sum()
            for (_,n),v in zip(nearest,weights):groups[n].add([i],float(v),'REPLACE')
    mod=obj.modifiers.new('Articulated motion','ARMATURE');mod.object=rig;obj.parent=rig
def animate(rig,row):
    names=['Idle','Move','Attack','Ability','Hit','Death'];fps=30
    for name in names:
        frames={'Idle':90,'Move':36,'Attack':24,'Ability':42,'Hit':12,'Death':36}[name]
        action=bpy.data.actions.new(name);action.use_fake_user=True;rig.animation_data_create();rig.animation_data.action=action
        for frame in range(1,frames+1,3):
            t=(frame-1)/(frames-1);phase=t*math.tau;beat=math.sin(math.pi*t);impact=math.exp(-((t-.4)/.16)**2)
            for b in rig.pose.bones:b.rotation_mode='XYZ';b.rotation_euler=(0,0,0);b.location=(0,0,0);b.scale=(1,1,1)
            core=rig.pose.bones['Core'];root=rig.pose.bones['Root'];ship=row['category']=='Ships'
            if name=='Idle':core.rotation_euler.y=math.sin(phase)*.008;core.location.y=math.sin(phase)*.009
            elif name=='Move':
                core.rotation_euler.x=(-.025 if ship else .035)+math.sin(phase*2)*.009
                core.location.y=math.sin(phase*2)*(.008 if ship else .018)
                for s,sign in [('L',1),('R',-1)]:
                    for part,amp in [('Leg',.15),('Foot',.12),('Arm',-.08),('Hand',.035)]:
                        b=rig.pose.bones.get(part+s)
                        if b:b.rotation_euler.x=math.sin(phase)*sign*amp
                for b in rig.pose.bones:
                    if b.name.startswith('Strut'):b.rotation_euler.x=math.sin(phase+(0 if b.name in ('Strut-1_-1','Strut1_1') else math.pi))*.10
            elif name in ('Attack','Ability'):
                core.rotation_euler.x=(-.05*impact+.025*beat) if ship else -.06*impact+.025*beat
                core.rotation_euler.y=0 if ship else .07*beat
                for s,sign in [('L',1),('R',-1)]:
                    b=rig.pose.bones.get('Arm'+s)
                    if b:b.rotation_euler.x=(-.24*impact if name=='Attack' else -.14*beat);b.rotation_euler.y=sign*.035*beat
                    b=rig.pose.bones.get('Hand'+s)
                    if b:b.rotation_euler.x=-.08*impact
                core.location.z=.025*impact
            elif name=='Hit':core.rotation_euler.x=.07*beat;core.rotation_euler.y=.04*beat
            elif name=='Death':root.rotation_euler.x=t*(.18 if ship else .5);root.location.y=-t*.18;root.rotation_euler.z=t*.12
            for b in rig.pose.bones:
                b.keyframe_insert('rotation_euler',frame=frame);b.keyframe_insert('location',frame=frame)
        # Ensure exact rest pose at loop and action boundaries, death retains final posture.
        if name!='Death':
            for b in rig.pose.bones:
                b.rotation_euler=(0,0,0);b.location=(0,0,0)
                if name=='Move' and b.name=='Core':b.rotation_euler.x=-.025 if row['category']=='Ships' else .035
                b.keyframe_insert('rotation_euler',frame=frames);b.keyframe_insert('location',frame=frames)
    rig.animation_data.action=bpy.data.actions['Idle']
    bpy.context.scene.frame_set(1)

for row in rows:
    i=row['source_index']
    if i not in wanted:continue
    uid=row['suggested_unit_id']+('__variant_02' if i==9 else '')
    folder=OUT/uid;folder.mkdir(exist_ok=True)
    if (folder/'stats.json').exists():
        if not options.force:continue
        (folder/'stats.json').unlink()
    start=time.time();print('BEGIN',i,uid,flush=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(SOURCE/row['renamed_path']))
    objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:o.select_set(True)
    bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.join();obj=bpy.context.object;apply(obj)
    normalize(obj,row)
    textures=[]
    for mat in obj.data.materials:
        if mat and mat.use_nodes:
            for n in mat.node_tree.nodes:
                if n.type=='TEX_IMAGE' and n.image:
                    role='albedo' if any(l.to_socket.name=='Base Color' for l in n.outputs['Color'].links) else 'normal' if any(l.to_node.type=='NORMAL_MAP' for l in n.outputs['Color'].links) else 'orm'
                    img=n.image;img.scale(2048,2048);img.filepath_raw=str(folder/(role+'.png'));img.file_format='PNG';img.save();textures.append(img)
    for role,color in [('normal',(.5,.5,1,1)),('orm',(1,.65,.1,1))]:
        if not (folder/(role+'.png')).exists():
            neutral=bpy.data.images.new('Neutral '+role,width=4,height=4,alpha=False);neutral.colorspace_settings.name='Non-Color';neutral.generated_color=color;neutral.filepath_raw=str(folder/(role+'.png'));neutral.file_format='PNG';neutral.save()
    trim(obj,32000 if row['category']=='Infantry' else 26000)
    # Remove only tiny disconnected islands after simplification; retain detached equipment.
    import bmesh
    bm=bmesh.new();bm.from_mesh(obj.data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00002);bmesh.ops.delete_loose(bm,use_verts=True,use_edges=True,use_faces=False) if hasattr(bmesh.ops,'delete_loose') else None
    bm.to_mesh(obj.data);bm.free()
    rig,desc=skeleton(obj,row)
    lods=[]
    for level,target in enumerate([0,8000,2000]):
        lod=obj if level==0 else bpy.data.objects.new(uid+f'_LOD{level}',obj.data.copy())
        if level:bpy.context.collection.objects.link(lod);trim(lod,target)
        lod.name=f'Body_LOD{level}';weight(lod,rig,desc,row['category']);lods.append(lod)
    animate(rig,row)
    for name,loc in [('Muzzle',(0,-1.0,.05 if row['category']=='Ships' else 1.15)),('DriveL',(-.18,.95,0)),('DriveR',(.18,.95,0)),('CoreFX',(0,0,.1 if row['category']=='Ships' else 1.0))]:
        e=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(e);e.parent=rig;e.location=loc
    scene=bpy.context.scene;scene.render.fps=30;scene.frame_start=1;scene.frame_end=90
    bpy.ops.wm.save_as_mainfile(filepath=str(folder/(uid+'.blend')),compress=True)
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.export_scene.fbx(filepath=str(folder/(uid+'.fbx')),use_selection=True,object_types={'MESH','ARMATURE','EMPTY'},add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0.5,axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,apply_scale_options='FBX_SCALE_ALL',apply_unit_scale=True)
    stats={'source_index':i,'id':uid,'category':row['category'],'source_triangles':row['triangles'],'lod_triangles':[sum(len(p.vertices)-2 for p in o.data.polygons) for o in lods],'bones':len(desc),'clips':['Idle','Move','Attack','Ability','Hit','Death'],'elapsed_seconds':round(time.time()-start,1),'provisional':i in (31,33)}
    (folder/'stats.json').write_text(json.dumps(stats,indent=2));print('FINISH',json.dumps(stats),flush=True)
