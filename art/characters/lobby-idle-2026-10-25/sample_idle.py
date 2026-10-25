"""Extract only additive body motion from CC0 Quaternius Idle_Loop.
Existing soldier limb proportions, grips and planted feet remain authoritative.
Run with Blender 5.2 --background --python sample_idle.py.
"""
import bpy,json,math
from pathlib import Path
from mathutils import Matrix,Quaternion,Vector
here=Path(__file__).resolve().parent
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(here/'source/Universal Animation Library[Standard]/Unreal-Godot/UAL1_Standard.glb'))
rig=next(o for o in bpy.data.objects if o.type=='ARMATURE')
# Remove imported NLA playback: sample one named action, at its original seconds.
for t in list(rig.animation_data.nla_tracks):rig.animation_data.nla_tracks.remove(t)
a=bpy.data.actions['Idle_Loop'];rig.animation_data.action=a
rig.animation_data.action_slot=a.slots[0]
scene=bpy.context.scene;fps=scene.render.fps/scene.render.fps_base
start,end=a.frame_range;duration=(end-start)/fps
names=['pelvis','spine_02','spine_03','Head']
C=Matrix(((-1,0,0),(0,0,1),(0,-1,0)))
def snap(sec):
 f=start+sec*fps;scene.frame_set(int(f),subframe=f-int(f))
 return [(C@rig.pose.bones[n].head,C@rig.pose.bones[n].matrix.to_3x3()@C.inverted()) for n in names]
ref=snap(0);frames=[];maxpos=0;maxangle=[0]*4
for i in range(round(duration*30)+1):
 state=snap(min(duration,i/30));p=state[0][0]-ref[0][0];maxpos=max(maxpos,p.length)
 item={'position':dict(zip(('x','y','z'),p))}
 for j,k in enumerate(['pelvis','spine','chest','head']):
  q=(state[j][1]@ref[j][1].inverted()).to_quaternion().normalized()
  if q.w<0:q.negate()
  maxangle[j]=max(maxangle[j],math.degrees(q.angle))
  item[k]={'x':q.x,'y':q.y,'z':q.z,'w':q.w}
 frames.append(item)
# Remove the tiny exporter endpoint residual with a smooth correction.
last=frames[-1]
for i,item in enumerate(frames):
 t=i/(len(frames)-1);t=t*t*(3-2*t)
 for k in ['position']:
  for c in ['x','y','z']:item[k][c]-=last[k][c]*t
 for k in ['pelvis','spine','chest','head']:
  q=Quaternion(tuple(item[k][c] for c in ['w','x','y','z']))
  endq=Quaternion(tuple(last[k][c] for c in ['w','x','y','z']))
  q=Quaternion().slerp(endq,t).inverted()@q
  item[k]=dict(zip(['w','x','y','z'],q))
frames[-1]=frames[0].copy()
out={'source' :'Quaternius Universal Animation Library Standard v3 / Idle_Loop / CC0 1.0','duration':duration,'frames':frames}
p=here.parents[2]/'UnityProject/Assets/Resources/Menu/Animation/LobbyIdleBody.json';p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(out,separators=(',',':'))+'\n')
manifest={'sourceAction':a.name,'sourceFpsOnImport':fps,'durationSeconds':duration,'sampleRate':30,'samples':len(frames),'maximumTranslationMeters':maxpos,'maxRotationDegrees':dict(zip(names,maxangle)),'first':frames[0],'last':frames[-1]}
(here/'source-motion-audit.json').write_text(json.dumps(manifest,indent=2))
print(json.dumps(manifest,indent=2))
