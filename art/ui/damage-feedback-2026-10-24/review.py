from pathlib import Path
import csv,json,math
import numpy as np
from PIL import Image,ImageDraw,ImageFont
r=Path(__file__).resolve().parent; player=r/'player'
assert (player/'results.txt').read_text().startswith('PASS\nVersion=0.60.0\nRuntimeErrors=0')
assert 'FINAL PASS:' in (r/'batch-results.txt').read_text()
rows=list(csv.DictReader((player/'samples.csv').open())); assert len(rows)==360
font=ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial.ttf',18)
small=ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial.ttf',15)
labels={'front':'HIT FROM FRONT','right':'HIT FROM RIGHT','rear':'HIT FROM BEHIND','left':'HIT FROM LEFT','diagonal':'HIT FROM FRONT-RIGHT','burst':'MULTIPLE INCOMING DIRECTIONS','turn':'TURN TOWARD THE ATTACKER','unknown':'UNKNOWN SOURCE: EDGE FLASH ONLY'}
checks=[]
for row in rows:
 stage,frame=row['stage'],int(row['frame']);p=player/stage/f'{frame:04d}.png'; im=Image.open(p).convert('RGB');a=np.asarray(im)
 dark=float((a.max(2)<8).mean());pink=float(((a[:,:,0]>225)&(a[:,:,1]<35)&(a[:,:,2]>225)).mean());assert dark<.95 and pink<.001,(p,dark,pink)
 checks.append({'frame':str(p.relative_to(r)),'dark_fraction':dark,'magenta_fraction':pink})
 d=ImageDraw.Draw(im);d.rectangle((10,190,420,215),fill=(17,24,29));d.text((18,193),labels[stage],font=small,fill=(244,227,221))
 d.rectangle((10,218,370,241),fill=(17,24,29));d.text((18,221),f"{frame/30:.2f}s   HP {math.ceil(float(row['health']))}   Directions {row['directions']}",font=small,fill='white')
 t=r/'preview-sequence'/stage;t.mkdir(parents=True,exist_ok=True);im.save(t/p.name)
board=Image.new('RGB',(1440,1000),(21,27,33));d=ImageDraw.Draw(board)
for i,(stage,frame,label) in enumerate([('front',2,'FRONT'),('right',2,'RIGHT'),('rear',2,'BEHIND'),('left',2,'LEFT'),('burst',10,'MULTIPLE HITS'),('front',44,'FADED / CLEAR VIEW')]):
 x,y=i%3*480,i//3*500
 im=Image.open(player/stage/f'{frame:04d}.png').resize((480,320));board.paste(im,(x,y+25));d.text((x+10,y+4),label,font=font,fill='white')
 # Close-up keeps the four direction positions readable in the contact sheet.
 im=Image.open(player/stage/f'{frame:04d}.png').crop((350,190,610,450)).resize((150,150));board.paste(im,(x+165,y+347))
board.save(r/'damage-feedback-review.png')
ranges={}
for stage,expected in [('front',0),('right',90),('rear',180),('left',-90),('diagonal',45)]:
 a=np.asarray(Image.open(player/stage/'0002.png').convert('RGB')).astype(float);h,w=a.shape[:2];yy,xx=np.indices((h,w));dx=xx-w/2;dy=yy-h/2;rad=np.hypot(dx,dy)
 mask=(rad>80)&(rad<110)&(a[:,:,0]>160)&(a[:,:,0]>a[:,:,1]+50)&(a[:,:,0]>a[:,:,2]+30)
 assert mask.sum()>80,(stage,int(mask.sum()))
 angle=math.degrees(math.atan2(float(dx[mask].mean()),float(-dy[mask].mean())))
 error=(angle-expected+180)%360-180;assert abs(error)<2,(stage,angle,error)
 ranges[stage]={'red_marker_pixels':int(mask.sum()),'visual_angle':angle,'expected_angle':expected}
a=np.asarray(Image.open(player/'front/0002.png').convert('RGB')).astype(float);b=np.asarray(Image.open(player/'front/0044.png').convert('RGB')).astype(float)
h,w=a.shape[:2];center=np.abs(a[h//2-25:h//2+25,w//2-25:w//2+25]-b[h//2-25:h//2+25,w//2-25:w//2+25]);center_mean=float(center.mean());assert center_mean<5,center_mean
redness=(a[:,:,0]-(a[:,:,1]+a[:,:,2])/2)-(b[:,:,0]-(b[:,:,1]+b[:,:,2])/2)
edge=float(redness[200:450,:8].mean());assert edge>15,edge
report={'frames':360,'baseline_frames':1,'resolution':[w,h],'fps':30,'seconds':12,'marker_geometry':ranges,'center_mean_rgb_difference_hit_vs_faded':center_mean,'left_edge_redness_increase':edge,'frame_quality_checks':checks}
(r/'visual-validation.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps({k:v for k,v in report.items() if k!='frame_quality_checks'}))
