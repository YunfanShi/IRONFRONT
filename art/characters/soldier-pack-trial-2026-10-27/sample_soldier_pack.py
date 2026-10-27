"""Sample evaluated author rig; no procedural replacement for source motion.
Semantic hands: +Z wrist-to-knuckles, +Y pinky-to-index. Source .L is left.
Only selected upper-body tracks are embedded in the game, under the source license.
"""
import bpy,json,math
from pathlib import Path
from mathutils import Matrix,Vector
R=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(R/'source/Blender/HumanM_SoldierAnimationsFREE_2.0.blend'),load_ui=False,use_scripts=False)
rig=bpy.data.objects['Rig'];sc=bpy.context.scene
for tr in list(rig.animation_data.nla_tracks):rig.animation_data.nla_tracks.remove(tr)
C=Matrix(((-1,0,0),(0,0,1),(0,-1,0)));D=Matrix(((-1,0,0),(0,1,0),(0,0,1)))
def vec(v):return dict(zip(('x','y','z'),v))
def quat(m):
 q=m.to_quaternion().normalized()
 if q.w<0:q.negate()
 return dict(x=q.x,y=q.y,z=q.z,w=q.w)
def semantic(points,side):
 z=(points['B-middleFinger01.'+side]-points['B-hand.'+side]).normalized()
 y=points['B-indexFinger01.'+side]-points['B-pinky01.'+side];y=(y-z*y.dot(z)).normalized();x=y.cross(z).normalized()
 return Matrix((x,y,z)).transposed()
restpts={b.name:C@b.head_local for b in rig.data.bones};resthands={s:semantic(restpts,s) for s in ['L','R']}
restlocals={}
fnames=['indexFinger','middleFinger','ringFinger','pinky','thumb']
for side in ['L','R']:
 for f in fnames:
  for j in range(1,4):
   n=f'B-{f}{j:02}.{side}';restlocals[n]=resthands[side].transposed()@C@rig.data.bones[n].matrix_local.to_3x3()@C.transposed()
clips=[]
for key,action,end,gun in [('rifleReload','AssaultRifle_Reload01',52,'Human_AssaultRifle'),('rifleHold','AssaultRifle_Aim01',71,'Human_AssaultRifle'),('pistolHold','Gun_Aim02',71,'Human_GunR')]:
 a=bpy.data.actions['HumanM@'+action];rig.animation_data.action=a;rig.animation_data.action_slot=a.slots[0]
 frames=[];basepos=None;baserot=None
 # 60 Hz sampling preserves the author's constraints/curves between 30 fps keys.
 for i in range((end-1)*2+1):
  f=1+i*.5;sc.frame_set(int(f),subframe=f%1);bpy.context.view_layer.update()
  pts={b.name:C@b.head for b in rig.pose.bones};hands={s:semantic(pts,s) for s in ['L','R']}
  W=bpy.data.objects[gun].matrix_world.to_3x3().normalized();W=C@W@D;inv=W.transposed()
  pos=C@bpy.data.objects[gun].matrix_world.translation
  if basepos is None:basepos=pos.copy();baserot=W.copy()
  origin=pts['B-hand.R'];item={}
  for label,n in [('leftHand','B-hand.L'),('leftElbow','B-forearm.L'),('rightElbow','B-forearm.R'),('leftShoulder','B-upperArm.L'),('rightShoulder','B-upperArm.R')]:item[label]=vec(inv@(pts[n]-origin))
  item['leftRotation']=quat(inv@hands['L']);item['rightRotation']=quat(inv@hands['R'])
  item['gunPosition']=vec(baserot.transposed()@(pos-basepos));item['gunRotation']=quat(baserot.transposed()@W)
  item['fingers']=[]
  for side in ['L','R']:
   for digit in fnames:
    for j in range(1,4):
     n=f'B-{digit}{j:02}.{side}'
     local=hands[side].transposed()@C@rig.pose.bones[n].matrix.to_3x3().normalized()@C.transposed()
     item['fingers'].append(quat(local@restlocals[n].transposed()))
  frames.append(item)
 # Remove exporter loop endpoint noise only on holding loops.
 if key!='rifleReload':frames[-1]=frames[0]
 clips.append({'key':key,'sourceAction':a.name,'duration':(end-1)/30,'sampleRate':60,'firstFrame':1,'lastFrame':end,'frames':frames})
 print('SAMPLED',key,len(frames),'samples',frames[0]['leftHand'],'rot',frames[0]['leftRotation'],frames[0]['rightRotation'])
out={'source':'Kevin Iglesias / Human Soldier Animations FREE 2.0 / user supplied unitypackage','packageSha256':'5a9f20fb075363c8b28a97c3360c3c93d9a6004af6e5caf7bcb1e852f6783b40','clips':clips,'fingerRestDirections':[vec(resthands[side].transposed()@C@(rig.data.bones[f'B-{digit}{j:02}.{side}'].tail_local-rig.data.bones[f'B-{digit}{j:02}.{side}'].head_local).normalized()) for side in ['L','R'] for digit in fnames for j in range(1,4)]}
dest=R.parents[2]/'UnityProject/Assets/Resources/Animation/SoldierPackTrial.json';dest.parent.mkdir(parents=True,exist_ok=True);dest.write_text(json.dumps(out,separators=(',',':'))+'\n')
(R/'sampling-report.json').write_text(json.dumps({k:v for k,v in out.items() if k!='clips'}|{'clips':[{k:v for k,v in c.items() if k!='frames'}|{'samples':len(c['frames'])} for c in clips]},indent=2)+'\n')
print('SOLDIER_PACK_SAMPLING_PASS',dest)
