"""Authored close-view pose, equipment and foot-contact pass. Reads preserved production sources."""
import bpy, math, json, pathlib, sys, numpy as np
from mathutils import Vector, Matrix, Quaternion
R=pathlib.Path(__file__).parent;SOURCE=pathlib.Path('E:/ClaudeCode/RR3/ArtSource/Units');OUT=R/'Generated';OUT.mkdir(exist_ok=True)
wanted=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
# Per-silhouette pose choices. Amplitudes are intentionally not multiplied by faction at runtime.
# role, stride, shoulder height, pelvis height, wrist height, garment, equipment side.
profiles={
'craigon_assimilator':('reach',.22,.73,.44,.43,False,None),
'craigon_carapace_custodian':('guard',.17,.72,.43,.42,False,None),
'craigon_carapace_drone':('slash',.22,.74,.43,.44,False,None),
'craigon_evolved_praetor':('command',.15,.69,.41,.43,True,None),
'craigon_fleshweaver':('channel',.12,.67,.40,.43,True,'L'),
'craigon_giftbearer':('channel',.12,.69,.42,.53,True,'L'),
'craigon_harmonic_disruptor':('pulse',.19,.70,.43,.40,False,None),
'craigon_neural_spiker':('lunge',.26,.69,.44,.35,False,None),
'craigon_pincer_guard':('guard',.16,.72,.43,.41,False,None),
'craigon_shellbound':('heavy',.13,.70,.43,.40,False,None),
'craigon_synapse_stalker':('lunge',.27,.72,.46,.35,False,None),
'craigon_tide_skimmer':('hover',.17,.70,.46,.43,False,None),
'silex_bastion_keeper':('brace',.11,.71,.43,.58,True,'C'),
'silex_crown_warden':('hammer',.13,.71,.44,.57,True,'L'),
'silex_geode_colossus':('heavy',.13,.72,.43,.42,False,None),
'silex_lithic_strider':('thrust',.24,.70,.44,.53,True,'L'),
'silex_obelisk_guard':('thrust',.14,.69,.42,.54,True,'L'),
'silex_royal_castellan':('command',.12,.69,.42,.54,True,'L'),
'silex_shard_retainer':('thrust',.16,.72,.43,.55,True,'L'),
'silex_shard_sentinel':('guard',.13,.71,.43,.53,True,'L'),
}
def smooth(t):t=max(0,min(1,t));return t*t*(3-2*t)
def curve(t,keys):
    for (a,x),(b,y) in zip(keys,keys[1:]):
        if t<=b:return x+(y-x)*smooth((t-a)/(b-a))
    return keys[-1][1]
def coords(o):
    a=np.empty(len(o.data.vertices)*3);o.data.vertices.foreach_get('co',a);return a.reshape(-1,3)
def action_set(rig,a):
    rig.animation_data.action=a
    if len(a.slots):rig.animation_data.action_slot=a.slots[0]
def rot(rig,name,xyz):
    b=rig.pose.bones.get(name)
    if b:
        basis=b.bone.matrix_local.to_quaternion()
        q=Quaternion((1,0,0),xyz[0])@Quaternion((0,1,0),xyz[1])@Quaternion((0,0,1),xyz[2])
        b.rotation_quaternion=basis.inverted()@q@basis
def loc(rig,name,xyz):
    b=rig.pose.bones.get(name)
    if b:b.location=b.bone.matrix_local.to_3x3().inverted()@Vector(xyz)
def bone_matrix(b,head,tail):
    rest=b.bone.matrix_local
    direction=rest.to_3x3()@Vector((0,1,0))
    q=direction.rotation_difference(Vector(tail)-Vector(head))
    m=q.to_matrix().to_4x4()@rest;m.translation=Vector(head);b.matrix=m
def ik_leg(rig,side,offset,body=(0,0,0),toe_pitch=0):
    upper=rig.pose.bones['Leg'+side];lower=rig.pose.bones['Foot'+side];toe=rig.pose.bones['Toe'+side]
    hip=upper.bone.head_local+Vector(body);target=toe.bone.head_local+Vector(offset)
    a=upper.bone.length;b=lower.bone.length;delta=target-hip;d=min(delta.length,a+b-.0001);direction=delta.normalized()
    along=(a*a-b*b+d*d)/(2*d)
    rest=lower.bone.head_local-upper.bone.head_local
    axis=(toe.bone.head_local-upper.bone.head_local).normalized()
    pole=rest-axis*rest.dot(axis)
    if pole.length<.001:pole=Vector((0,-1,0))
    bend=(pole-direction*direction.dot(pole)).normalized()
    knee=hip+direction*along+bend*math.sqrt(max(0,a*a-along*along))
    bone_matrix(upper,hip,knee);bpy.context.view_layer.update()
    bone_matrix(lower,knee,target);bpy.context.view_layer.update()
    toe_dir=Quaternion((1,0,0),toe_pitch)@(toe.bone.tail_local-toe.bone.head_local)
    bone_matrix(toe,target,target+toe_dir)
def bind_socket(rig,name,position,parent='Core'):
    e=bpy.data.objects.get(name)
    if not e:e=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(e)
    e.parent=rig;e.parent_type='BONE';e.parent_bone=parent;e.matrix_parent_inverse=Matrix.Identity(4)
    bone=rig.data.bones[parent];e.location=bone.matrix_local.inverted()@Vector(position)-Vector((0,bone.length,0));e.rotation_euler=(0,0,0)
def create_rig(rig,meshes,uid,stats):
    infantry=stats['category']=='Infantry';vehicle=stats['category']=='GroundVehicles'
    a=coords(meshes[0]);lo=a.min(0);hi=a.max(0);h=hi[2]-lo[2];w=hi[0]-lo[0]
    if not infantry:
        count={'craigon_ascension_engine':4,'craigon_chitin_walker':2,'craigon_convergence_hauler':4,'craigon_specimen_crawler':4,'silex_geode_walker':4}.get(uid,0)
        if count:
            # Fit articulated suspension to the actual contact islands of each creature/walker.
            samples=a[a[:,2]<h*.15,:2];centers=[samples[np.argmin(samples[:,0])]]
            for n in range(1,count):centers.append(samples[np.argmax(np.min(np.array([np.sum((samples-c)**2,axis=1) for c in centers]),axis=0))])
            centers=np.array(centers)
            for n in range(25):
                labels=np.argmin(np.sum((samples[:,None,:]-centers[None,:,:])**2,axis=2),axis=1)
                for k in range(count):
                    if np.any(labels==k):centers[k]=samples[labels==k].mean(0)
            rig.animation_data_clear();bpy.context.view_layer.objects.active=rig;bpy.ops.object.mode_set(mode='EDIT')
            for b in list(rig.data.edit_bones):rig.data.edit_bones.remove(b)
            def addv(name,head,tail,parent=None):
                b=rig.data.edit_bones.new(name);b.head=head;b.tail=tail
                if parent:b.parent=rig.data.edit_bones[parent]
            addv('Root',(0,0,0),(0,0,.12));addv('Core',(0,0,h*.48),(0,0,h*.76),'Root')
            for n,(x,y) in enumerate(centers):
                side=str(n);hip=(x*.5,y*.65,h*.50);knee=(x*.9,y*.95,h*.27);ankle=(x,y,h*.075)
                addv('Leg'+side,hip,knee,'Root');addv('Foot'+side,knee,ankle,'Leg'+side);addv('Toe'+side,ankle,(x,y-.10,h*.025),'Foot'+side)
            bpy.ops.object.mode_set(mode='OBJECT')
            desc={b.name:(np.array(b.head_local),np.array(b.tail_local)) for b in rig.data.bones}
            for mesh in meshes:
                mesh.vertex_groups.clear();groups={n:mesh.vertex_groups.new(name=n) for n in desc}
                for i,p in enumerate(coords(mesh)):
                    if p[2]>h*.57:groups['Core'].add([i],1,'REPLACE');continue
                    side=str(int(np.argmin(np.sum((centers-p[:2])**2,axis=1))));choices=['Leg'+side,'Foot'+side,'Toe'+side]
                    if p[2]>h*.42:choices+=['Core']
                    ds=[]
                    for n in choices:
                        head,tail=desc[n];v=tail-head;t=np.clip(np.dot(p-head,v)/np.dot(v,v),0,1);ds.append((np.linalg.norm(p-head-v*t),n))
                    ds.sort();ds=ds[:2];weights=np.array([1/(d+.025)**6 for d,n in ds]);weights/=weights.sum()
                    for (d,n),weight in zip(ds,weights):groups[n].add([i],float(weight),'REPLACE')
        return a,h,w,None
    role,stride,shoulder,pelvis,wrist,garment,equipment=profiles[uid]
    rig.animation_data_clear()
    bpy.context.view_layer.objects.active=rig;bpy.ops.object.mode_set(mode='EDIT')
    for b in list(rig.data.edit_bones):rig.data.edit_bones.remove(b)
    def add(name,head,tail,parent=None):
        b=rig.data.edit_bones.new(name);b.head=head;b.tail=tail
        if parent:b.parent=rig.data.edit_bones[parent]
    def y_at(x,z):
        near=a[(np.abs(a[:,0]-x)<w*.08)&(np.abs(a[:,2]-z)<h*.06)]
        return float(np.median(near[:,1])) if len(near) else 0
    add('Root',(0,0,0),(0,0,.15));add('Core',(0,0,h*pelvis),(0,0,h*.61),'Root')
    add('Chest',(0,0,h*.61),(0,0,h*.79),'Core');add('Head',(0,0,h*.80),(0,0,h*.94),'Chest')
    add('Mantle',(0,.04,h*pelvis),(0,.05,h*.16),'Core')
    for side,sign in [('L',-1),('R',1)]:
        # Equipment-bearing arms are bent at the elbow in the provided rest pose.
        armed=equipment==side or equipment=='C'
        sx=sign*w*.22;ex=sign*w*(.30 if armed else .32);wx=sign*w*(.31 if armed else .39)
        if equipment=='C':wx=sign*w*.07;ex=sign*w*.22
        sh=(sx,y_at(sx,h*shoulder),h*shoulder);el=(ex,y_at(ex,h*.59),h*(.60 if armed else .57))
        wr=(wx,y_at(wx,h*wrist),h*wrist)
        add('Arm'+side,sh,el,'Chest');add('Hand'+side,el,wr,'Arm'+side)
        hx=sign*w*.17;kx=sign*w*.23;ax=sign*w*.29
        hip=(hx,y_at(hx,h*pelvis),h*pelvis);knee=(kx,y_at(kx,h*.26)-.035,h*.26)
        ankle=(ax,y_at(ax,h*.07),h*.07);tip=(ax,ankle[1]-.17,h*.025)
        add('Leg'+side,hip,knee,'Root');add('Foot'+side,knee,ankle,'Leg'+side);add('Toe'+side,ankle,tip,'Foot'+side)
    bpy.ops.object.mode_set(mode='OBJECT')
    desc={b.name:(np.array(b.head_local),np.array(b.tail_local)) for b in rig.data.bones}
    for mesh in meshes:
        mesh.vertex_groups.clear();groups={name:mesh.vertex_groups.new(name=name) for name in desc}
        points=coords(mesh)
        for i,p in enumerate(points):
            x,y,z=p;side='L' if x<0 else 'R';ax=abs(x)/w;zz=z/h
            # Continuous shafts/hammer heads bind rigidly to the carrying hand, including below the waist.
            weapon=False
            if equipment=='L':
                weapon=x<-w*.29 and z>h*.12 and z<h*.79
                if uid in ('silex_obelisk_guard','silex_shard_retainer','silex_shard_sentinel','silex_royal_castellan','craigon_giftbearer','craigon_fleshweaver'):
                    weapon=x<-w*.32 and z>h*.13
            elif equipment=='C':weapon=abs(x)<w*.10 and z<h*.63 and z>h*.05 and y<-.025
            if uid=='silex_crown_warden':
                shaftA=np.array([-.35,1.28]);shaftB=np.array([-.225,.29]);point=np.array([x,z]);shaft=shaftB-shaftA
                u=np.clip(np.dot(point-shaftA,shaft)/np.dot(shaft,shaft),0,1)
                weapon=weapon or np.linalg.norm(point-shaftA-u*shaft)<.048 or (x<-.29 and 1.28<z<1.57)
            if weapon:groups['HandL' if equipment!='C' else 'HandR'].add([i],1,'REPLACE');continue
            if garment and .12<zz<pelvis and ax<.27:
                groups['Mantle'].add([i],1,'REPLACE');continue
            if zz>.84:choices=['Head']
            elif zz>.78:choices=['Head','Chest']
            elif zz<pelvis:
                choices=['Leg'+side,'Foot'+side,'Toe'+side]
                if zz>pelvis-.07:choices+=['Core']
            elif ax>.23 and zz<shoulder+.05:choices=['Arm'+side,'Hand'+side]
            elif ax>.18 and zz>shoulder-.12:choices=['Chest','Arm'+side]
            elif zz>.61:choices=['Chest']
            else:choices=['Core','Chest']
            distances=[]
            for n in choices:
                head,tail=desc[n];v=tail-head;t=np.clip(np.dot(p-head,v)/np.dot(v,v),0,1);distances.append((np.linalg.norm(p-head-v*t),n))
            distances.sort();nearest=distances[:2];weights=np.array([1/(d+.025)**6 for d,n in nearest]);weights/=weights.sum()
            # Solid plates and distal extremities remain rigid; narrow blends are reserved for joints.
            if weights[0]>.94:weights[0]=1;weights[1:]=0
            for (_,name),weight in zip(nearest,weights):
                if weight>0:groups[name].add([i],float(weight),'REPLACE')
    return a,h,w,profiles[uid]
def polish(uid):
    folder=SOURCE/uid;stats=json.loads((folder/'stats.json').read_text());inf=stats['category']=='Infantry';ship=stats['category']=='Ships';bio=uid.startswith('craigon')
    bpy.ops.wm.open_mainfile(filepath=str(folder/(uid+'.blend')));s=bpy.context.scene;rig=bpy.data.objects['Rig'];meshes=[bpy.data.objects['Body_LOD'+str(n)] for n in range(3)]
    rig.animation_data_clear()
    for action in list(bpy.data.actions):bpy.data.actions.remove(action)
    a,h,w,profile=create_rig(rig,meshes,uid,stats)
    role,stride,*_=profile if profile else ('hull',.14,0,0,0,False,None)
    heavy=any(v in uid for v in ('colossus','shellbound','bastion','dreadnought','monolith','ark','engine','hauler'))
    fast=any(v in uid for v in ('fighter','interceptor','scuttler','skimmer','stalker','strider'))
    mass=.65 if heavy else 1.2 if fast else 1
    equipment=profile[-1] if profile else None
    lengths={'Idle':150 if heavy else 120,'Move':48 if heavy else 30 if fast else 38,'Attack':40 if heavy else 28 if fast else 34,'Ability':72,'Hit':22,'Death':64}
    contacts=[];clips={}
    for name,end in lengths.items():
        act=bpy.data.actions.new(name);act.use_fake_user=True;rig.animation_data_create();rig.animation_data.action=act
        for frame in range(1,end+1):
            t=(frame-1)/(end-1);phase=t*math.tau
            for b in rig.pose.bones:b.rotation_mode='QUATERNION';b.rotation_quaternion=Quaternion();b.location=(0,0,0);b.scale=(1,1,1)
            inhale=math.sin(phase);body=[0,0,0];left=(0,0,0);right=(0,0,0);toeL=toeR=0
            if name=='Idle':
                rot(rig,'Core',(.006*inhale*mass,0,.006*math.sin(phase)*mass))
                rot(rig,'Chest',(-.008*inhale,0,.003*inhale))
                rot(rig,'Head',(.006*math.sin(phase),0,.018*math.sin(phase)*mass))
                if ship:loc(rig,'Core',(0,0,.009*inhale*mass));rot(rig,'Core',(.004*inhale,.009*inhale*mass,0))
                elif inf:
                    rot(rig,'ArmR',(.009*inhale,0,-.006*inhale));rot(rig,'ArmL',(.006*inhale,0,.005*inhale))
                    rot(rig,'Mantle',(.007*inhale,0,0))
            elif name=='Move':
                if inf and role!='hover':
                    body=[.018*math.sin(phase),0,-.012*(1-math.cos(phase*2))]
                    loc(rig,'Root',body);rot(rig,'Core',(.035,-.018*math.sin(phase),-.025*math.sin(phase)))
                    rot(rig,'Chest',(-.018,0,.035*math.sin(phase)));rot(rig,'Head',(-.014,0,-.014*math.sin(phase)))
                    offsets=[]
                    for side,shift in [('L',0),('R',.5)]:
                        p=(t+shift)%1
                        if p<.62:yy=-stride*.5+stride*p/.62;lift=0;pitch=0
                        else:
                            q=(p-.62)/.38;yy=stride*.5-stride*smooth(q);lift=.085*mass*math.sin(q*math.pi)**2;pitch=.16*math.sin(q*math.pi)
                        offsets.append((0,yy,lift))
                        if side=='L':toeL=pitch
                        else:toeR=pitch
                        arm=.065 if equipment else .15*mass;rot(rig,'Arm'+side,(arm*math.sin(phase+shift*math.tau),0,0));rot(rig,'Hand'+side,(-.04*abs(math.sin(phase)),0,0))
                        if p<.62:contacts.append((name,frame,side,lift))
                    left,right=offsets;rot(rig,'Mantle',(.018*math.sin(phase),.01*math.sin(phase),0))
                else:
                    # Steady forward thrust; banks and suspension settle without ornamental spinning.
                    rot(rig,'Core',(.012+.006*math.cos(phase),.014*math.sin(phase)*mass,0))
                    loc(rig,'Core',(0,0,.008*math.sin(phase*2)*mass))
                    for b in rig.pose.bones:
                        if b.name.startswith('Strut'):rot(rig,b.name,(.10*math.sin(phase+(0 if b.name in ('Strut-1_-1','Strut1_1') else math.pi)),0,0))
            elif name in ('Attack','Ability'):
                # Held anticipation -> quick contact -> short settle -> longer recovery.
                hit=curve(t,[(0,0),(.16,-.32),(.27,-.38),(.38,1),(.45,.88),(.58,.18),(.75,-.08),(1,0)])
                charge=curve(t,[(0,0),(.20,.28),(.42,1),(.56,1),(.75,.24),(1,0)])
                beat=hit if name=='Attack' else charge
                if inf:
                    body=[.012*beat,0,-.025*abs(beat)];loc(rig,'Root',body)
                    if role in ('slash','lunge','reach'):
                        rot(rig,'Core',(-.055*beat,0,-.14*beat));rot(rig,'Chest',(-.035*beat,0,-.08*beat))
                        rot(rig,'ArmR',(-.48*beat,0,.12*beat));rot(rig,'HandR',(-.12*beat,0,0))
                        rot(rig,'ArmL',(.12*beat,0,-.04*beat))
                    elif role in ('thrust','hammer'):
                        rot(rig,'Core',(-.035*beat,0,.065*beat));rot(rig,'Chest',(-.025*beat,0,.05*beat))
                        rot(rig,'ArmL',((-.30 if role=='hammer' else -.20)*beat,0,-.035*beat));rot(rig,'HandL',(-.10*beat,0,0))
                        rot(rig,'ArmR',(-.08*beat,0,.025*beat))
                    elif role in ('channel','command','pulse'):
                        rot(rig,'Core',(-.022*beat,0,.035*beat));rot(rig,'Chest',(-.025*beat,0,-.035*beat))
                        rot(rig,'ArmL',(-.10*beat,0,-.06*beat));rot(rig,'ArmR',(-.23*beat,0,.07*beat));rot(rig,'HandR',(-.1*beat,0,0))
                    else:
                        rot(rig,'Core',(-.055*beat,0,.045*beat));rot(rig,'Chest',(-.03*beat,0,0))
                        rot(rig,'ArmL',(-.16*beat,0,-.035*beat));rot(rig,'ArmR',(-.22*beat,0,.035*beat))
                    rot(rig,'Head',(.018*beat,0,-.025*beat));rot(rig,'Mantle',(-.012*beat,0,0))
                    if name=='Ability':
                        rot(rig,'ArmR',(-.24*charge,0,.08*charge));rot(rig,'HandR',(-.12*charge,0,0))
                else:
                    recoil=curve(t,[(0,0),(.22,-.1),(.38,1),(.48,.5),(.64,-.12),(1,0)]) if name=='Attack' else charge
                    loc(rig,'Core',(0,.035*mass*recoil,.008*recoil))
                    rot(rig,'Core',(-.026*mass*recoil,.008*recoil,0))
            elif name=='Hit':
                hit=curve(t,[(0,0),(.18,1),(.34,.72),(.58,-.22),(.78,.07),(1,0)])
                rot(rig,'Core',(-.055*hit*mass,.02*hit,0));rot(rig,'Head',(.026*hit,0,.018*hit))
                if inf:body=[0,0,-.015*max(0,hit)];loc(rig,'Root',body)
                else:loc(rig,'Core',(0,.025*hit*mass,0))
            elif name=='Death':
                fall=curve(t,[(0,0),(.14,.12),(.3,.08),(.62,.88),(.8,1),(1,1)])
                if inf:
                    body=[.025*fall,.025*fall,-.25*fall];loc(rig,'Root',body)
                    rot(rig,'Core',(.30*fall,0,.08*fall));rot(rig,'Chest',(.12*fall,0,-.045*fall));rot(rig,'Head',(.19*fall,0,0))
                    rot(rig,'ArmL',(.09*fall,0,.07*fall));rot(rig,'ArmR',(.16*fall,0,-.06*fall));rot(rig,'HandR',(.10*fall,0,0))
                else:
                    rot(rig,'Core',(.12*fall,.18*fall,0));loc(rig,'Core',(0,.08*fall,-.09*fall))
            bpy.context.view_layer.update()
            if inf and role!='hover':
                ik_leg(rig,'L',left,body,toeL);ik_leg(rig,'R',right,body,toeR)
            elif not ship:
                for bone in rig.data.bones:
                    if not bone.name.startswith('Toe'):continue
                    side=bone.name[3:];p=(t+(0 if bone.head_local.x*bone.head_local.y<0 else .5))%1
                    y=0;lift=0
                    if name=='Move':
                        if p<.65:y=-.08+.16*p/.65
                        else:q=(p-.65)/.35;y=.08-.16*smooth(q);lift=.07*math.sin(q*math.pi)**2
                    ik_leg(rig,side,(0,y,lift))
            # Hovering skimmer keeps its sculpted airborne silhouette; no fictitious foot plant.
            for b in rig.pose.bones:
                b.keyframe_insert('rotation_quaternion',frame=frame);b.keyframe_insert('location',frame=frame)
        # Linear, every-frame sampling preserves the authored timing; FBX reduces only redundant curves.
        for layer in act.layers:
            for strip in layer.strips:
                for bag in strip.channelbags:
                    for fc in bag.fcurves:
                        for key in fc.keyframe_points:key.interpolation='LINEAR'
        clips[name]={'frames':end,'seconds':(end-1)/30,'contact':.38 if name=='Attack' else .5 if name=='Ability' else None}
    action_set(rig,bpy.data.actions['Idle']);s.frame_set(1);bpy.context.view_layer.update()
    muzzle=(0,float(a[:,1].min())-.025,float(h*.55 if inf else h*.5 if not ship else 0))
    if inf and equipment:parent='HandL' if equipment!='C' else 'HandR'
    else:parent='HandR' if inf else 'Core'
    bind_socket(rig,'Muzzle',muzzle,parent);bind_socket(rig,'CoreFX',(0,0,h*.62 if not ship else 0),'Chest' if inf else 'Core')
    if ship:
        aft=float(np.quantile(a[:,1],.92));bind_socket(rig,'DriveL',(-w*.17,aft,0));bind_socket(rig,'DriveR',(w*.17,aft,0))
    dest=OUT/uid;dest.mkdir(exist_ok=True)
    stats.update({'bones':len(rig.data.bones),'animation_pass':'close-view-2','animation_role':role if inf else ('capital' if heavy else 'agile' if fast else 'cruising') if ship else 'suspension','clip_details':clips})
    (dest/'stats.json').write_text(json.dumps(stats,indent=2))
    (dest/'animation_notes.json').write_text(json.dumps({'unit':uid,'profile':profile,'contact_samples':len(contacts),'max_authored_stance_lift':max((c[3] for c in contacts),default=0),'equipment_rigid_binding':equipment,'clips':clips},indent=2))
    rig['animation_notes']='Close view pass 2: authored anticipation/contact/recovery; planted two-link legs; rigid carried equipment; per-silhouette pose profiles.'
    s.render.fps=30;s.frame_start=1;s.frame_end=lengths['Idle']
    bpy.ops.wm.save_as_mainfile(filepath=str(dest/(uid+'.blend')),compress=True)
    bpy.ops.object.select_all(action='DESELECT')
    for o in s.objects:
        if o.type in ('MESH','ARMATURE','EMPTY'):o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(dest/(uid+'.fbx')),use_selection=True,object_types={'MESH','ARMATURE','EMPTY'},add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=.15,axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,apply_scale_options='FBX_SCALE_ALL',apply_unit_scale=True)
    print('POLISHED',uid,flush=True)
for folder in sorted(SOURCE.iterdir()):
    if folder.is_dir() and (not wanted or folder.name in wanted):polish(folder.name)

