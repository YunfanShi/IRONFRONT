"""Contact sheets from captured player frames. No reconstruction of the animation."""
from pathlib import Path
from PIL import Image,ImageDraw
root=Path(__file__).resolve().parent;source=root/'checks/pack-motion';out=root/'review';out.mkdir(exist_ok=True)
for page,start in enumerate([75,95,115]):
 sheet=Image.new('RGB',(1440,1300),(25,28,32));draw=ImageDraw.Draw(sheet)
 for i,frame in enumerate(range(start,min(start+20,135))):
  im=Image.open(source/'motion-Carbine'/f'{frame:04}.png').resize((360,240));x=i%4*360;y=i//4*260
  sheet.paste(im,(x,y));draw.text((x+8,y+242),f'IF-27 {(frame-75)/30:.3f} s',fill='white')
 sheet.save(out/f'if27-sequence-{page+1}.jpg')
for weapon in ['Carbine','Pistol']:
 names=['hip','ads','reload10','reload24','reload34','reload40','reload55','reload66','reload74','reload87','reload96']
 sheet=Image.new('RGB',(1440,1376),(25,28,32));draw=ImageDraw.Draw(sheet)
 for i,name in enumerate(names):
  im=Image.open(source/f'Blue-{weapon}-{name}.png').resize((480,320));x=i%3*480;y=i//3*344
  sheet.paste(im,(x,y));draw.text((x+8,y+324),weapon+' '+name,fill='white')
 sheet.save(out/(weapon+'-final.jpg'))
