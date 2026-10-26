from pathlib import Path
import sys,json
from PIL import Image,ImageDraw
sys.path.insert(0,'/Users/liuwenjun/.codex/skills/scenario-unity-expert/scripts')
import ut_review
root=Path(__file__).resolve().parent
r=root/'checks/rig'
weapons=['Carbine','BattleRifle','Smg','Lmg','Marksman','Sniper','Pistol']
for team in ['Blue','Red']:
 sheet=Image.new('RGB',(1600,7*190),(24,24,24));draw=ImageDraw.Draw(sheet)
 for row,weapon in enumerate(weapons):
  for col,pose in enumerate(['hip','ads','reload40','reload87']):
   p=r/f'{team}-{weapon}-{pose}.png';x,y=col*400,row*190
   draw.text((x+5,y+3),p.stem,fill='white')
   if p.exists():
    im=Image.open(p).crop((0,390,1440,960));im.thumbnail((400,165));sheet.paste(im,(x,y+22))
   else:draw.text((x+100,y+90),'Scope overlay',fill='white')
 sheet.save(root/f'{team}-review.jpg')
# Lock the camera and crop identically for the user screenshot comparison.
baseline=root.parent/'model-rig-audit-2026-10-26/accepted-rig'
sheet=Image.new('RGB',(1440,4*340),(24,24,24));d=ImageDraw.Draw(sheet)
for row,name in enumerate(['Blue-Pistol-hip','Blue-Carbine-hip','Blue-Carbine-reload40','Blue-Pistol-ads']):
 for col,folder in enumerate([baseline,r]):
  im=Image.open(folder/(name+'.png')).crop((0,360,1440,960));im.thumbnail((720,300));sheet.paste(im,(col*720,row*340+30));d.text((col*720+8,row*340+8),('0.66.1  ' if col==0 else '0.66.2  ')+name,fill='white')
sheet.save(root/'before-after.jpg')
checks={p.name:ut_review.image_checks(str(p)) for p in r.glob('*.png')}
(root/'frame-checks.json').write_text(json.dumps(checks,indent=2))
print('Reviewed frame metrics:',len(checks))
