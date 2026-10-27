"""Present actual Unity captures at matching poses for sleeve-volume review."""
from pathlib import Path
from PIL import Image, ImageDraw

root = Path(__file__).resolve().parent
source = root / 'checks/finger-motion'
before = root.parent / 'sleeve-width-2026-10-27/checks/sleeve-motion'
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
        draw.text((12,10), '0.66.7 BEFORE', fill='white')
        draw.text((732,10), '0.66.8 THICKER FINGERS', fill='white')
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
draw.text((12,10),'0.66.7 BEFORE',fill='white');draw.text((732,10),'0.66.8 THICKER FINGERS',fill='white')
for row,weapon in enumerate(['Carbine','Pistol']):
    for col,folder in enumerate([before,source]):
        sheet.paste(Image.open(folder/f'Blue-{weapon}-hip.png').resize((720,480)),(col*720,30+row*504))
    draw.text((12,514+row*504),weapon,fill='white')
sheet.save(out/'finger-before-after.jpg',quality=94)

# Actual reverse contact captures reveal the digits hidden from the FP camera.
for weapon in ['Carbine','Pistol']:
    poses=['hip','reload24','reload66','reload87']
    sheet=Image.new('RGB',(1440,504*len(poses)+30),(25,28,32));draw=ImageDraw.Draw(sheet)
    draw.text((12,10),'0.66.7 BEFORE',fill='white');draw.text((732,10),'0.66.8 THICKER FINGERS',fill='white')
    for row,pose in enumerate(poses):
        for col,folder in enumerate([before,source]):
            im=Image.open(folder/f'Blue-{weapon}-{pose}-contact.png').resize((720,480))
            sheet.paste(im,(col*720,30+row*504));draw.text((col*720+8,30+row*504+482),weapon+' '+pose+' contact',fill='white')
    sheet.save(out/(weapon+'-contact-comparison.jpg'),quality=95)
neutral=root/'checks/neutral'
if (neutral/'after-three-quarter.png').exists():
    sheet=Image.new('RGB',(1200,4*624+30),(25,28,32));draw=ImageDraw.Draw(sheet)
    draw.text((12,10),'0.66.7 BEFORE',fill='white');draw.text((612,10),'0.66.8 THICKER FINGERS',fill='white')
    for row,view in enumerate(['front','back','side','three-quarter']):
        for col,label in enumerate(['before','after']):
            sheet.paste(Image.open(neutral/(label+'-'+view+'.png')),(col*600,30+row*624))
            draw.text((col*600+8,30+row*624+602),view+' - diagnostic clay',fill='white')
    sheet.save(out/'neutral-hand-comparison.jpg',quality=95)

# Matched pixel crops make finger volume readable without changing exposure.
examples=[('Carbine','reload66',(450,280,880,710)),('Pistol','reload87',(490,410,930,850))]
sheet=Image.new('RGB',(1200,1278),(25,28,32));draw=ImageDraw.Draw(sheet)
draw.text((12,10),'0.66.7 BEFORE',fill='white');draw.text((612,10),'0.66.8 THICKER FINGERS',fill='white')
for row,(weapon,pose,crop) in enumerate(examples):
    for col,folder in enumerate([before,source]):
        im=Image.open(folder/f'Blue-{weapon}-{pose}-contact.png').crop(crop).resize((600,600))
        sheet.paste(im,(col*600,30+row*624));draw.text((col*600+8,30+row*624+602),weapon+' '+pose+' - matched crop',fill='white')
sheet.save(out/'finger-detail-before-after.jpg',quality=95)
