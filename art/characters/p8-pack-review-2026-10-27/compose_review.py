from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import json
R=Path(__file__).resolve().parent
font=ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial.ttf',20)
small=ImageFont.truetype('/System/Library/Fonts/Supplemental/Arial.ttf',16)
keys=[1,6,11,16,21,26,31,36,41,46,49,52]
for label in ['Gun_Reload01','Gun_Reload02']:
    for view in ['front','reverse']:
        sheet=Image.new('RGB',(1440,1080),(20,25,32));d=ImageDraw.Draw(sheet)
        for i,f in enumerate(keys):
            im=Image.open(R/'frames'/label/view/f'{f:04}.png').convert('RGB').resize((360,240))
            x=(i%4)*360;y=(i//4)*360
            sheet.paste(im,(x,y+34));d.text((x+10,y+7),f'{label} / {view} / f{f:02}',font=small,fill='white')
            d.text((x+10,y+280),f'{(f-1)/30:.2f} s',font=font,fill=(175,190,210))
        sheet.save(R/f'{label}-{view}-sheet.jpg',quality=94)
out=R/'video-frames'/'comparison';out.mkdir(parents=True,exist_ok=True)
for f in range(1,53):
    im=Image.new('RGB',(960,640),(20,25,32));d=ImageDraw.Draw(im)
    for row,view in enumerate(['front','reverse']):
        for col,label in enumerate(['Gun_Reload01','Gun_Reload02']):
            frame=Image.open(R/'frames'/label/view/f'{f:04}.png').convert('RGB').resize((480,320))
            im.paste(frame,(col*480,row*320))
            d.rectangle((col*480,row*320,col*480+480,row*320+28),fill=(20,25,32))
            d.text((col*480+12,row*320+5),f'{label} | {view} | {(f-1)/30:.2f}s',font=small,fill='white')
    im.save(out/f'{f:04}.png')
frames=[Image.open(out/f'{f:04}.png') for f in range(1,53)]
frames[0].save(R/'pistol-reload-comparison.gif',save_all=True,append_images=frames[1:],duration=[33,33,34]*17+[33],loop=0,optimize=False)
print('CONTACT_SHEETS_AND_COMPARISON_READY')
