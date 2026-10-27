from pathlib import Path
from PIL import Image,ImageDraw
import sys,math
p=Path(__file__).resolve().parent;n=int(sys.argv[1]);fs=sorted((p/f'checks-sweep{n:02}').glob('*-close.png'));cols=3;w,h=480,350
out=Image.new('RGB',(cols*w,math.ceil(len(fs)/cols)*h),'#111822');d=ImageDraw.Draw(out)
for i,f in enumerate(fs):
 im=Image.open(f).convert('RGB');im.thumbnail((w,h-30));x=(i%cols)*w;y=(i//cols)*h;out.paste(im,(x,y+30));d.text((x+12,y+8),f.stem,fill='white')
out.save(p/f'review/sweep{n:02}.jpg');print(len(fs),'poses')
