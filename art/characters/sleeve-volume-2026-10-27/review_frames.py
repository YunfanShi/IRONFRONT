"""Present actual Unity captures at matching poses for sleeve-volume review."""
from pathlib import Path
from PIL import Image, ImageDraw

root = Path(__file__).resolve().parent
source = root / 'checks/sleeve-motion'
before = root.parent / 'soldier-pack-trial-2026-10-27/checks/pack-motion'
out = root / 'review'
out.mkdir(exist_ok=True)
for weapon in ['Carbine', 'Pistol']:
    names = ['hip','ads','reload10','reload24','reload34','reload40','reload55','reload66','reload74','reload87','reload96']
    sheet = Image.new('RGB', (1440,1376), (25,28,32))
    draw = ImageDraw.Draw(sheet)
    for i, name in enumerate(names):
        im = Image.open(source / f'Blue-{weapon}-{name}.png').resize((480,320))
        x, y = i % 3 * 480, i // 3 * 344
        sheet.paste(im, (x,y)); draw.text((x+8,y+324),weapon+' '+name,fill='white')
    sheet.save(out / (weapon+'-final.jpg'), quality=92)
    for group, names in [('hold',['hip','ads']),('reload',['reload24','reload66','reload87'])]:
        sheet = Image.new('RGB', (1440,504*len(names)+30), (25,28,32))
        draw = ImageDraw.Draw(sheet)
        draw.text((12,10), '0.66.5 BEFORE', fill='white')
        draw.text((732,10), '0.66.6 SLEEVE VOLUME', fill='white')
        for row, name in enumerate(names):
            for col, folder in enumerate([before,source]):
                im = Image.open(folder / f'Blue-{weapon}-{name}.png').resize((720,480))
                sheet.paste(im, (720*col,30+row*504))
                draw.text((720*col+8,30+row*504+482), weapon+' '+name,fill='white')
        sheet.save(out / (weapon+'-'+group+'-comparison.jpg'),quality=94)
for weapon, end in [('Carbine',132),('Pistol',114)]:
    frames = list(range(75,end+7,3))
    sheet = Image.new('RGB', (1440,260*((len(frames)+3)//4)),(25,28,32))
    draw=ImageDraw.Draw(sheet)
    for i,frame in enumerate(frames):
        im=Image.open(source / ('motion-'+weapon) / f'{frame:04}.png').resize((360,240))
        x,y=i%4*360,i//4*260;sheet.paste(im,(x,y))
        draw.text((x+8,y+242),f'{weapon} {(frame-75)/30:.2f} s',fill='white')
    sheet.save(out / (weapon+'-sequence.jpg'),quality=93)
sheet=Image.new('RGB',(1440,1040),(25,28,32));draw=ImageDraw.Draw(sheet)
draw.text((12,10),'0.66.5 BEFORE',fill='white');draw.text((732,10),'0.66.6 SLEEVE VOLUME',fill='white')
for row,weapon in enumerate(['Carbine','Pistol']):
    for col,folder in enumerate([before,source]):
        sheet.paste(Image.open(folder/f'Blue-{weapon}-hip.png').resize((720,480)),(col*720,30+row*504))
    draw.text((12,514+row*504),weapon,fill='white')
sheet.save(out/'sleeve-before-after.jpg',quality=94)
