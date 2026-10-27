"""Compose actual player captures. No pose or pixel synthesis."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
R=Path(__file__).resolve().parent
P=R/'checks/pistol-pistol'
B=R.parent/'p8-reload02-2026-10-27/checks/pistol-pistol'
O=R/'review';O.mkdir(exist_ok=True)
font=ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial.ttf',18)
keys=['hip','ads','reload10','reload24','reload34','reload40','reload55','reload66','reload70','reload74','reload75','reload79','reload87','reload96']
s=Image.new('RGB',(1440,1720),(25,28,32));d=ImageDraw.Draw(s)
for i,k in enumerate(keys):
    x=i%3*480;y=i//3*344
    s.paste(Image.open(P/f'Blue-Pistol-{k}.png').resize((480,320)),(x,y))
    d.text((x+8,y+324),k,font=font,fill='white')
s.save(O/'P8-final-poses.jpg',quality=94)
s=Image.new('RGB',(1440,1040),(25,28,32));d=ImageDraw.Draw(s)
for i,f in enumerate(range(75,121,3)):
    x=i%4*360;y=i//4*260
    s.paste(Image.open(P/'motion-Pistol'/f'{f:04}.png').resize((360,240)),(x,y))
    d.text((x+8,y+242),f'{(f-75)/30:.2f}s',font=font,fill='white')
s.save(O/'P8-final-sequence.jpg',quality=94)
poses=['reload66','reload70','reload74','reload75','reload79','reload87']
s=Image.new('RGB',(1440,1512),(25,28,32));d=ImageDraw.Draw(s)
for i,k in enumerate(poses):
    x=i%2*720;y=i//2*504
    s.paste(Image.open(P/f'Blue-Pistol-{k}-contact.png').resize((720,480)),(x,y))
    d.text((x+8,y+482),k+' contact',font=font,fill='white')
s.save(O/'P8-final-contact.jpg',quality=95)
s=Image.new('RGB',(1440,534),(25,28,32));d=ImageDraw.Draw(s)
for col,(folder,label) in enumerate([(B,'0.66.9 / independent fingers'),(P,'0.66.10 / original arms + Reload02')]):
    s.paste(Image.open(folder/'Blue-Pistol-reload87-contact.png').resize((720,480)),(col*720,30))
    d.text((col*720+8,6),label,font=font,fill='white')
s.save(O/'P8-reload87-before-after.jpg',quality=95)
# Same full sequence repeated for convenient normal-speed playback.
V=R/'checks/video-frames'/'P8-Reload02';V.mkdir(parents=True,exist_ok=True)
for i in range(3*144):
    f=i%144
    im=Image.open(P/'motion-Pistol'/f'{f:04}.png').convert('RGB').resize((960,640))
    d=ImageDraw.Draw(im)
    d.text((16,580),'P8 | Gun_Reload02 | game speed: 1.3 s | original arms | 0.66.10',font=font,fill='white')
    im.save(V/f'{i:04}.png')
print('FINAL_REVIEW_READY: real player frames, three playback repetitions')
