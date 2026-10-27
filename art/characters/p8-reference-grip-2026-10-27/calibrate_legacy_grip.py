"""Measure the old finger row, derive its bend axis and source-hand alignment.
Uses the unchanged mesh exported with Fingers_R/L at rest in sweep07.
"""
from pathlib import Path
import numpy as np,json,math
P=Path(__file__).resolve().parent
src=P/'checks-sweep07/right-curl0'
joints={b['name']:b for b in json.loads(src.with_name(src.name+'-study.json').read_text())['joints']}
verts={};name=None
for line in src.with_suffix('.obj').read_text().splitlines():
 if line.startswith('o '):name=line[2:];verts[name]=[]
 if line.startswith('v '):verts[name].append([float(x) for x in line.split()[1:4]])
def arr(d):return np.array([d[k] for k in ['x','y','z']])
def v(a):return dict(zip(['x','y','z'],map(float,a)))
def basis(d,t):
 d=d/np.linalg.norm(d);t=t-d*np.dot(t,d);t=t/np.linalg.norm(t);return np.column_stack([d,t,np.cross(d,t)])
def matq(q):
 x,y,z,w=[q[k] for k in ['x','y','z','w']]
 return np.array([[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],[2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],[2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]])
def mate(x,y,z):
 x,y,z=map(math.radians,[x,y,z]);cx,sx=math.cos(x),math.sin(x);cy,sy=math.cos(y),math.sin(y);cz,sz=math.cos(z),math.sin(z)
 return np.array([[cy*cz+sy*sx*sz,-cy*sz+sy*sx*cz,sy*cx],[cx*sz,cx*cz,-sx],[-sy*cz+cy*sx*sz,sy*sz+cy*sx*cz,cy*cx]])
def euler(a):return v([math.degrees(math.asin(np.clip(-a[1,2],-1,1))),math.degrees(math.atan2(a[0,2],a[2,2])),math.degrees(math.atan2(a[1,0],a[1,1]))])
def bend(axis,degrees):
 axis=axis/np.linalg.norm(axis);x,y,z=axis;k=np.array([[0,-z,y],[z,0,-x],[-y,x,0]]);angle=math.radians(degrees)
 return euler(np.eye(3)+math.sin(angle)*k+(1-math.cos(angle))*(k@k))
bases={};axes={};metrics={}
for side,obj in [('R','BlueSoldier_LOD0_ArmL'),('L','BlueSoldier_LOD0_ArmR')]:
 w=arr(joints['Hand_'+side]['p']);d=arr(joints['Fingers_'+side]['p'])-w;d=d/np.linalg.norm(d);pts=np.array(verts[obj])-w;t=pts@d;pts=pts[(t>.20)&(t<.30)];flat=pts-np.outer(pts@d,d);val,vec=np.linalg.eigh(np.cov(flat.T));width=vec[:,-1];bases[side]=basis(d,width)
 axes[side]=matq(joints['Fingers_'+side]['q']).T@width;axes[side]/=np.linalg.norm(axes[side]);metrics[side]={'vertices':len(pts),'widthWorld':width.tolist(),'bendAxisRestLocal':axes[side].tolist(),'oldAxisAlignment':abs(float(axes[side][0])),'variance':val.tolist()}
pack=json.loads((P.parents[2]/'UnityProject/Assets/Resources/Animation/SoldierPackTrial.json').read_text());f=next(c for c in pack['clips'] if c['key']=='pistolHold')['frames'][0]
def target(q,sign):
 m=matq(q);return basis(m[:,2],m[:,1]*sign)
fits=[]
for r in [-1,1]:
 for l in [-1,1]:
  for curl in [-70,70]:
   ra=euler(target(f['rightRotation'],r)@bases['R'].T@mate(90,140,0));la=euler(target(f['leftRotation'],l)@bases['L'].T@mate(90,220,0))
   fits.append(dict(name=f'correct-axis-R{r}-L{l}-curl{curl}',rightOffset=v([.055,-.01,-.14]),rightAngles=ra,leftOffset=v([-.085,-.035,-.16]),leftAngles=la,legacyFingers=[bend(axes['L'],curl),bend(axes['R'],curl)]))
(P/'sweep14.json').write_text(json.dumps({'fits':fits},indent=2))
(P/'finger-axis-analysis.json').write_text(json.dumps(metrics,indent=2)+'\n')
print(json.dumps(metrics,indent=2))
