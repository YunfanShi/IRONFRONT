"""Review actual 0.66.12 Player frames and compare identical 0.66.11 phases."""
from pathlib import Path
import hashlib, json, sys
from PIL import Image, ImageDraw, ImageFont
sys.path.insert(0, '/Users/liuwenjun/.codex/skills/scenario-unity-expert/scripts')
import ut_review
R=Path(__file__).resolve().parent
repo=R.parents[2]
P=R/'checks/pistol-pistol'
B=R.parent/'p8-reference-grip-2026-10-27/checks/pistol-pistol'
O=R/'review';O.mkdir(exist_ok=True)
font_path=repo/'UnityProject/Assets/Fonts/Lobby/SourceHanSansCN-Medium.otf'
title=ImageFont.truetype(str(font_path),30)
body=ImageFont.truetype(str(font_path),23)
small=ImageFont.truetype(str(font_path),18)
checks=[]
for p in sorted(p for folder in (P, R/'checks/pistol-rig') for p in folder.rglob('*.png')):
    c=ut_review.image_checks(str(p))
    v=ut_review.image_verdict(c)
    checks.append({'path':str(p.relative_to(R)),'flags':c.get('flags'),'verdict':v})
errors=[x for x in checks if any(v.startswith('error') for v in x['verdict'])]
(R/'image-checks.json').write_text(json.dumps({'count':len(checks),'errors':errors,'frames':checks},ensure_ascii=False,indent=2)+'\n')
assert not errors,errors
keys=['hip','ads','reload10','reload24','reload34','reload40','reload55','reload66','reload70','reload74','reload75','reload79','reload87','reload96']
s=Image.new('RGB',(1440,1720),'#191c20');d=ImageDraw.Draw(s)
for i,k in enumerate(keys):
    x=i%3*480;y=i//3*344
    s.paste(Image.open(P/f'Blue-Pistol-{k}.png').resize((480,320)),(x,y))
    d.text((x+8,y+323),k,font=small,fill='white')
s.save(O/'P8-final-poses.jpg',quality=94)
s=Image.new('RGB',(1440,1040),'#191c20');d=ImageDraw.Draw(s)
for i,f in enumerate(range(75,121,3)):
    x=i%4*360;y=i//4*260
    s.paste(Image.open(P/'motion-Pistol'/f'{f:04}.png').resize((360,240)),(x,y))
    d.text((x+8,y+241),f'{(f-75)/30:.2f}s',font=small,fill='white')
s.save(O/'P8-final-sequence.jpg',quality=94)
# Same source camera and identical crop for a legible still. No changed poses.
s=Image.new('RGB',(1920,800),'#141a23');d=ImageDraw.Draw(s)
colors=['#e7bd7a','#8ddbcf']
for side,(folder,label) in enumerate([(B,'之前 · 0.66.11'),(P,'修正后 · 0.66.12')]):
    x=side*960
    d.text((x+26,17),label,font=title,fill=colors[side])
    d.text((x+26,59),'旧手臂 + 动作包 Gun_Aim02',font=body,fill='white')
    im=Image.open(folder/'Blue-Pistol-hip-close.png').convert('RGB').resize((960,640),Image.Resampling.LANCZOS)
    s.paste(im,(x,105))
d.text((26,757),'实际运行画面 · 同一近照机位、同一比例',font=body,fill='white')
s.save(O/'P8-wrist-before-after.jpg',quality=95)
s=Image.new('RGB',(1440,1570),'#141a23');d=ImageDraw.Draw(s)
for row,pose in enumerate(['reload66','reload74','reload87']):
    for side,(folder,label) in enumerate([(B,'之前'),(P,'修正后')]):
        x=side*720;y=row*520
        d.text((x+18,y+12),label+' · '+pose,font=body,fill=colors[side])
        s.paste(Image.open(folder/f'Blue-Pistol-{pose}-contact.png').convert('RGB').resize((720,480)),(x,y+40))
s.save(O/'P8-thumb-before-after.jpg',quality=95)
V=R/'checks/comparison-frames';V.mkdir(exist_ok=True)
frames={};provenance=[]
for side,(folder,version) in enumerate([(B,'0.66.11'),(P,'0.66.12')]):
    assert (folder/'results.txt').read_text().startswith(f'PASS\nVersion={version}\nRuntimeErrors=0')
    manifest=[]
    for n in range(144):
        path=folder/'motion-Pistol'/f'{n:04}.png'
        im=Image.open(path).convert('RGB');assert im.size==(1440,960)
        frames[side,n]=im.resize((960,640),Image.Resampling.LANCZOS)
        manifest.append({'frame':n,'sha256':hashlib.sha256(path.read_bytes()).hexdigest()})
    provenance.append({'version':version,'directory':str(folder.relative_to(repo)),'frames':manifest})
for i in range(288):
    n=i%144;s=Image.new('RGB',(1920,800),'#141a23');d=ImageDraw.Draw(s)
    for side in range(2):
        x=side*960
        d.text((x+26,12),['之前 · 0.66.11','修正后 · 0.66.12'][side],font=title,fill=colors[side])
        d.text((x+26,54),'旧手臂 · Gun_Aim02 / Gun_Reload02',font=body,fill='white')
        s.paste(frames[side,n],(x,100))
    phase='持枪' if n<30 else ('瞄准' if n<60 else ('持枪' if n<75 else ('换弹' if n<114 else '恢复持枪')))
    d.text((26,752),f'正常速度 1× · 第 {i//144+1} 遍 · {phase}',font=body,fill='white')
    d.text((1110,752),'同步起点 · 完整视野 · 换弹 1.30 秒',font=body,fill='white')
    s.save(V/f'{i:04}.png',compress_level=2)
(R/'comparison-manifest.json').write_text(json.dumps({'output':'p8-wrist-thumb-before-after.mp4','fps':30,'dimensions':[1920,800],'frames':288,'durationSeconds':9.6,'gameReloadSeconds':1.3,'composition':'Two normal-speed repetitions. Full source views scaled equally. No generated poses, retiming, interpolation or audio.','sources':provenance},ensure_ascii=False,indent=2)+'\n')
print(f'REVIEW_READY: {len(checks)} Player frames checked, no image errors; comparison 288 frames / 9.6 s',flush=True)
