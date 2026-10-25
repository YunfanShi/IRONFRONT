from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import csv,json,sys
r=Path(__file__).resolve().parent
old=r/'before';new=r/'player'
actions=['strafe-left','strafe-right','diagonal-left','diagonal-right','crouch-left','crouch-right']
font=ImageFont.truetype('/System/Library/Fonts/Menlo.ttc',20)
metrics=[]
for a in actions:
 out=r/'comparison'/a;out.mkdir(parents=True,exist_ok=True)
 for n in range(90):
  canvas=Image.new('RGB',(960,640),'#16212b');d=ImageDraw.Draw(canvas)
  for side,root,label in [(0,old,'BEFORE 0.58.1'),(1,new,'AFTER 0.58.2')]:
   im=Image.open(root/a/f'{n:04d}.png').crop((180,0,780,640)).resize((480,512));canvas.paste(im,(side*480,80));d.text((side*480+25,50),label,font=font,fill='white')
  d.text((25,608),f'{a} | Same input speed | 30 fps',font=font,fill='white');canvas.save(out/f'{n:04d}.png')
def load(root):return list(csv.DictReader((root/'leg-samples.csv').open()))
pre,post=load(old),load(new)
for a in actions+['reverse-diagonal-left','reverse-diagonal-right','turn-left-right']:
 current=[v for v in post if v['action']==a]
 previous=[v for v in pre if v['action']==a]
 minimum=min(float(v['legClearance']) for v in current)
 assert minimum>.12,(a,minimum)
 metrics.append({'action':a,'old_min_segment_distance':min(float(v['legClearance']) for v in previous) if previous else None,'new_min_segment_distance':minimum,'new_min_foot_lane_after_warmup':min(float(v['footLaneGap']) for v in current if int(v['frame'])>15)})
(r/'leg-review.json').write_text(json.dumps(metrics,indent=2)+'\n');print('Leg clearance including start and turns:',metrics,flush=True)
# Forward motion and prone comparison at identical input speeds and phases.
prior=r.parent/'reference-motion-2026-10-24'/'player'
def motion(root):return {(v['action'],v['frame']):v for v in csv.DictReader((root/'samples.csv').open())}
a,b=motion(prior),motion(new);preserve={}
for action in ['run','walk','crouch','crawl','crawl-strafe','crawl-reverse','prone-aim']:
 diffs=[]
 for k,v in b.items():
  # Startup idle breathing uses real wall-clock time, so compare the
  # settled distance-driven cycle after motion has reached 1.
  if k[0]==action and int(k[1])>=8:
   diffs += [abs(float(v[n])-float(a[k][n])) for n in v if n not in ('action','frame')]
 preserve[action]=max(diffs)
(r/'motion-preservation.json').write_text(json.dumps(preserve,indent=2)+'\n');print('Existing motion deltas:',preserve,flush=True)
assert max(preserve.values())<.0001
sys.path.insert(0,'/Users/liuwenjun/.codex/skills/scenario-unity-expert/scripts')
import ut_review
checks=[]
for p in new.rglob('*.png'):
 result=ut_review.image_checks(str(p));checks.append({'file':str(p.relative_to(r)),'verdict':ut_review.image_verdict(result)})
errors=[v for v in checks if any(x.startswith('error:') for x in v['verdict'])]
(r/'image-checks.json').write_text(json.dumps(checks,indent=2)+'\n');print('Images:',len(checks),'errors:',errors,flush=True);assert not errors
files=[r/'comparison'/a/'0047.png' for a in ['strafe-left','strafe-right','diagonal-right','crouch-right']]
ut_review.contact_sheet([str(p) for p in files],str(r/'comparison-preview.png'),cols=2,tile=640,labels=['A / left','D / right','W+D sprint','Crouch + D'])
