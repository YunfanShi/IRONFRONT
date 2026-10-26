"""Sample CC0 UAL2 slides into the existing Unity soldier's semantic axes.
No mesh, skin weights or runtime bone lengths are replaced.
Blender --background --python sample_slide.py
"""
import bpy,json,math
from pathlib import Path
from mathutils import Matrix,Vector
here=Path(__file__).resolve().parent
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(here/'source/Universal Animation Library 2[Standard]/Unreal-Godot/UAL2_Standard.glb'))
rig=next(o for o in bpy.data.objects if o.type=='ARMATURE')
for t in list(rig.animation_data.nla_tracks):rig.animation_data.nla_tracks.remove(t)
scene=bpy.context.scene;fps=scene.render.fps/scene.render.fps_base
C=Matrix(((-1,0,0),(0,0,1),(0,-1,0)))
vec=lambda p:dict(zip(('x','y','z'),p))
quat=lambda q:dict(zip(('w','x','y','z'),q.normalized()))
print('BONES',[(b.name,list(C@b.head_local)) for b in rig.data.bones if any(s in b.name.lower() for s in ('pelvis','spine','head','thigh','calf','foot','ball'))])
names={'pelvis':'pelvis','spine':'spine_02','chest':'spine_03','head':'Head','leftFoot':'foot_l','rightFoot':'foot_r'}
rest={k:C@rig.data.bones[n].matrix_local.to_3x3()@C.inverted() for k,n in names.items()}
clips=[];audit=[]
for label in ['Slide_Start','Slide_Loop','Slide_Exit']:
 a=bpy.data.actions[label];rig.animation_data.action=a;rig.animation_data.action_slot=a.slots[0]
 begin,end=a.frame_range;duration=(end-begin)/fps;frames=[]
 for i in range(round(duration*60)+1):
  f=begin+min(duration,i/60)*fps;scene.frame_set(int(f),subframe=f-int(f))
  state={k:quat((C@rig.pose.bones[n].matrix.to_3x3()@C.inverted()@rest[k].inverted()).to_quaternion()) for k,n in names.items()}
  state['position']=vec(C@rig.pose.bones['pelvis'].head)
  for side,key in [('l','left'),('r','right')]:
   state[key+'Thigh']=vec((C@(rig.pose.bones['calf_'+side].head-rig.pose.bones['thigh_'+side].head)).normalized())
   state[key+'Shin']=vec((C@(rig.pose.bones['foot_'+side].head-rig.pose.bones['calf_'+side].head)).normalized())
  frames.append(state)
 clips.append({'name':label,'duration':duration,'frames':frames})
 audit.append({'name':label,'duration':duration,'frames':len(frames),'keyFrames':[frames[j] for j in [0,len(frames)//2,len(frames)-1]]})
out={'source':'Quaternius Universal Animation Library 2 Standard / CC0 1.0','hipHeight':(C@rig.data.bones['pelvis'].head_local).y,'clips':clips}
p=here.parents[2]/'UnityProject/Assets/Resources/Animation/InfantrySlide.json';p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(out,separators=(',',':'))+'\n')
(here/'source-motion-audit.json').write_text(json.dumps(audit,indent=2))
print('HIP',out['hipHeight'])
for a in audit:print(a['name'],a['duration'],[(x['position'],x['leftThigh'],x['rightThigh']) for x in a['keyFrames']])
