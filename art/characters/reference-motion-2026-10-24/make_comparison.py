from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import csv,json,sys,hashlib
root=Path(__file__).resolve().parent
old=root.parent/'motion-refinement-2026-10-24'/'motion-player'
font=ImageFont.truetype('/System/Library/Fonts/Menlo.ttc',20)
for action in ['run','crouch']:
 out=root/'comparison'/action;out.mkdir(parents=True,exist_ok=True)
 for n in range(90):
  canvas=Image.new('RGB',(960,640),'#16212b');d=ImageDraw.Draw(canvas)
  for side,folder,label in [(0,old,'BEFORE 0.58.0'),(1,root/'player','AFTER 0.58.1')]:
   im=Image.open(folder/action/f'{n:04d}.png').crop((180,0,780,640)).resize((480,512))
   canvas.paste(im,(side*480,80));d.text((side*480+25,50),label,font=font,fill='white')
  d.text((25,608),f'Same speed | {action} | 30 fps | {n/30:.2f} s',font=font,fill='white')
  canvas.save(out/f'{n:04d}.png')
# Compare the complete preserved prone curves, not just one picture.
def rows(folder):
 return {(r['action'],r['frame']):r for r in csv.DictReader((folder/'samples.csv').open())}
a=rows(old);b=rows(root/'player');diff={}
for action in ['crawl','crawl-strafe','crawl-reverse','prone-aim']:
 errors=[]
 for key,v in b.items():
  if key[0]!=action:continue
  for name in v:
   if name not in ('action','frame'):errors.append(abs(float(v[name])-float(a[key][name])))
 diff[action]=max(errors)
(root/'prone-preservation.json').write_text(json.dumps(diff,indent=2)+'\n')
print('Prone numeric delta:',diff)
assert max(diff.values())<0.0001
# Inspect actual player frames, plus separate game-context frames when present.
sys.path.insert(0,'/Users/liuwenjun/.codex/skills/scenario-unity-expert/scripts')
import ut_review
checks=[]
for p in list((root/'player').rglob('*.png'))+list((root/'development').glob('*.png')):
 result=ut_review.image_checks(str(p));verdict=ut_review.image_verdict(result)
 checks.append({'file':str(p.relative_to(root)),'verdict':verdict})
errors=[x for x in checks if any(v.startswith('error:') for v in x['verdict'])]
(root/'image-checks.json').write_text(json.dumps(checks,indent=2)+'\n')
print('Frame checks:',len(checks),'Errors:',errors)
assert not errors
files=[root/'player'/a/'0030.png' for a in ['run','crouch','run-front','crouch-front','run-rear','crouch-rear']]
ut_review.contact_sheet([str(p) for p in files],str(root/'preview.png'),cols=2,tile=480,labels=['Run side','Crouch side','Run front','Crouch front','Run rear','Crouch rear'])
