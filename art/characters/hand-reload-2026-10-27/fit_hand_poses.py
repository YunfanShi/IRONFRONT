"""Fit editable finger pose candidates to measured prop contacts from the Unity study.
Produces diagnostic input only; the rendered poses still require visual inspection.
"""
import json,math,random
from pathlib import Path
from mathutils import Vector,Quaternion,Euler,Matrix
ROOT=Path(__file__).resolve().parent
A=json.loads((ROOT/'pose-study-neutral.json').read_text());B={b['name']:b for b in A['joints']}
def vec(v):return Vector(tuple(v[k] for k in 'xyz'))
def quat(v):return Quaternion(tuple(v[k] for k in ['w','x','y','z']))
def angle(v):return Quaternion((0,1,0),math.radians(v[1]))@Quaternion((1,0,0),math.radians(v[0]))@Quaternion((0,0,1),math.radians(v[2]))
lengths=[.2741,.2926,.2903,.2169,.2758]
def chain(side,digit,angles,delta):
 names=['Index','Middle','Ring','Pinky','Thumb'];ns=[names[digit]+str(j+1)+'_'+side for j in range(3)]
 points=[];qold=None;qnew=None;pold=None;pnew=None
 for j,n in enumerate(ns):
  b=B[n];p=vec(b['p']);q=quat(b['q'])
  if j==0:pn=p+delta;qn=q@angle(angles[j*3:j*3+3])
  else:
   dq=qnew@qold.inverted();pn=pnew+dq@(p-pold);qn=dq@q@angle(angles[j*3:j*3+3])
  points.append(pn);pold=p;pnew=pn;qold=q;qnew=qn
 # FBX bone direction is local +Y.
 points.append(pnew+qnew@Vector((0,lengths[digit]*.086/.65,0)))
 return points
# Check FK against captured actual bent finger (to detect handedness/order errors).
H={b['name']:b for b in json.loads((ROOT/'pose-study-hip.json').read_text())['joints']}
p=chain('R',1,[-58,0,0,-78,0,0,-46,0,0],Vector())
print('FK_ERROR',[(p[j]-vec(H['Middle'+str(j+1)+'_R']['p'])).length for j in range(3)])

def fit(side,d,delta,targets,initial,thumb=False):
 # Only the root of a digit spreads/opposes. Interphalangeal joints hinge.
 active=[0,3,6]+([1,2] if thumb else [2]);bounds={0:(-85,45),3:((-45,0) if thumb else (-85,0)),6:((-40,0) if thumb else (-75,0)),1:(-75,75),2:(-75,75)}
 def score(a):
  ps=chain(side,d,a,delta);err=sum((p-t).length_squared for p,t in zip(ps[1:],targets))
  return err
 best=initial[:]
 for _ in range(4):
  a=best[:] if _==0 else [0.]*9
  if _:a[0]=random.uniform(-50,-10);a[3]=random.uniform(-50,-10);a[6]=-20
  for step in [15,8,4,2,1,.5]:
   for iteration in range(20):
    change=False
    for i in active:
     cur=score(a);choice=a[i]
     for off in [-step,step]:
      old=a[i];a[i]=min(bounds[i][1],max(bounds[i][0],old+off));s=score(a)
      if s<cur:cur=s;choice=a[i]
      a[i]=old
     if choice!=a[i]:a[i]=choice;change=True
    if not change:break
  if score(a)<score(best):best=a[:]
 return [round(v,1) for v in best],round(math.sqrt(score(best)),4),chain(side,d,best,delta)
random.seed(4)
rightDelta=Vector((.020,-.005,.022));leftDelta=Vector((-.025,-.035,-.018))
pose=[];reports=[]
for side,delta in [('L',leftDelta),('R',rightDelta)]:
 for d in range(5):
  initial=[-45,0,0,-40,0,0,-25,0,0]
  p=chain(side,d,[0]*9,delta);sgn=1 if side=='R' else -1
  if d==4:
   ts=[Vector((sgn*.075,-.055 if side=='R' else -.095,-.060 if side=='R' else -.050)),Vector((sgn*.066,-.050 if side=='R' else -.080,-.005 if side=='R' else 0)),Vector((sgn*.062,-.050 if side=='R' else -.075,.030))]
  elif d==0 and side=='R':
   ts=[Vector((.055,-.10,.02)),Vector((.027,-.11,.045)),Vector((.005,-.115,.035))]
  else:
   y=p[0].y;front=.006+(y+.14)*.5+(0.025 if side=='L' else 0)
   ts=[Vector((sgn*.043,y-.007,front+.025)),Vector((sgn*.008,y-.006,front+.030)),Vector((-sgn*.026,y,front+.016))]
  a,e,points=fit(side,d,delta,ts,initial,d==4);pose.extend([dict(zip('xyz',a[j*3:j*3+3])) for j in range(3)])
  reports.append(dict(side=side,digit=d,angles=a,error=e,points=[list(v) for v in points]))
# Same support palm on a detached magazine. Unlike the old full fist the fingers
# lie over the front face, with the thumb opposing on the upper side.
magpose=pose[:];delta=Vector((-.005,0,-.055))
# Fit in the neutral hand frame. Magazine world translation is applied by the runtime.
for d in range(5):
 p=chain('L',d,[0]*9,delta)
 if d==4:ts=[Vector((-.071,-.067,-.11)),Vector((-.055,-.05,-.08)),Vector((-.035,-.048,-.06))]
 else:
  y=p[0].y;ts=[Vector((-.035,y,-.023)),Vector((.005,y,-.02)),Vector((.035,y,-.035))]
 a,e,points=fit('L',d,delta,ts,[-50,0,0,-40,0,0,-25,0,0],d==4)
 magpose[d*3:d*3+3]=[dict(zip('xyz',a[j*3:j*3+3])) for j in range(3)];reports.append(dict(mode='mag',digit=d,angles=a,error=e))
slidepose=pose[:]
for d,flex in enumerate([65,75,65,45]):
 a=[-flex,0,0,-10,0,0,-18,0,0]
 slidepose[d*3:d*3+3]=[dict(zip('xyz',a[j*3:j*3+3])) for j in range(3)]
rot=Matrix(((0,0,1),(-1,0,0),(0,-1,0))).to_quaternion()@angle([10,0,0]).inverted()
w=Vector((-.11485,.080,-.058956));hand=vec(B['Hand_L']['p'])
targets=[Vector((-.050,.050,-.130)),Vector((-.052,0,-.115)),Vector((-.045,-.030,-.10))]
neutralTargets=[hand+rot.inverted()@(t-w) for t in targets]
a,e,_=fit('L',4,Vector(),neutralTargets,[-20,0,0,-20,0,0,-15,0,0],True)
slidepose[12:15]=[dict(zip('xyz',a[j*3:j*3+3])) for j in range(3)]
reports.append(dict(mode='slide',digit=4,angles=a,error=e))
fits=[dict(name='fitted-hip',rightOffset=dict(x=.075,y=.020,z=-.088),rightAngles=dict(x=10,y=0,z=0),leftOffset=dict(x=-.085,y=-.015,z=-.083),leftAngles=dict(x=10,y=0,z=0),joints=pose),dict(name='fitted-mag',currentLeft=True,currentRight=True,reloading=True,phase=.34,supportDelta=dict(x=-.005,y=0,z=-.055),joints=magpose),dict(name='fitted-slide',currentLeft=True,currentRight=True,reloading=True,phase=.87,supportDelta=dict(x=.06,y=.055,z=.08),joints=slidepose)]
(ROOT/'refine-fits.json').write_text(json.dumps(dict(fits=fits),indent=2));(ROOT/'pose-fit-report.json').write_text(json.dumps(reports,indent=2));print(json.dumps(reports))
