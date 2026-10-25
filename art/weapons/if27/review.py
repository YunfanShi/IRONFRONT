from pathlib import Path
import json,hashlib,shutil
import numpy as np
from PIL import Image,ImageDraw
R=Path(__file__).resolve().parent;repo=R.parents[2];player=R/'evidence/player'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
assert (player/'results.txt').read_text().startswith('PASS')
assert 'FINAL PASS:' in (R/'evidence/batch-results.txt').read_text()
checks=[]
for p in sorted(player.rglob('*.png')):
    a=np.array(Image.open(p).convert('RGB'));dark=float((a.max(2)<8).mean());magenta=float(((a[:,:,0]>225)&(a[:,:,1]<35)&(a[:,:,2]>225)).mean())
    if p.name!='menu-if27.png':assert dark<.95 and magenta<.001,(p,dark,magenta)
    checks.append({'file':str(p.relative_to(R)),'size':list(Image.open(p).size),'dark_fraction':dark,'magenta_fraction':magenta})
(R/'evidence/image-checks.json').write_text(json.dumps(checks,indent=2))
paths=['fp-hip.png','fp-ads.png','tp-standing.png','tp-run.png','tp-crouch.png','tp-prone.png']
board=Image.new('RGB',(1440,680),(25,29,34));draw=ImageDraw.Draw(board)
for i,name in enumerate(paths):
    tile=Image.open(player/name).convert('RGB').resize((480,320));x=(i%3)*480;y=(i//3)*340
    board.paste(tile,(x,y+20));draw.text((x+10,y+4),name[:-4],fill='white')
board.save(R/'evidence/game-review.png')
frames=sorted((player/'reload-fp').glob('*.png'))
board=Image.new('RGB',(1440,680),(25,29,34));draw=ImageDraw.Draw(board)
for i,t in enumerate([0,.18,.4,.65,.87,1]):
    p=frames[round(t*(len(frames)-1))];tile=Image.open(p).convert('RGB').resize((480,320));x=i%3*480;y=i//3*340
    board.paste(tile,(x,y+20));draw.text((x+10,y+4),f'Reload {t:.0%}',fill='white')
board.save(R/'evidence/reload-review.png')
sequence=R/'evidence/preview-sequence';sequence.mkdir(exist_ok=True)
for stage,name in [('hip-fire','fp-hip.png'),('aim-down-sight','fp-ads.png'),('third-person','tp-standing.png')]:
    (sequence/stage).mkdir(exist_ok=True);shutil.copy2(player/name,sequence/stage/'0000.png')
for stage in ['reload-fp','reload-tp']:
    (sequence/stage).mkdir(exist_ok=True)
    for p in (player/stage).glob('*.png'):shutil.copy2(p,sequence/stage/p.name)
preserved={}
for manifest in ['art/vehicles/remaining-integration-2026-10-23/baseline/source-sha256.json','art/vehicles/armor-integration-2026-10-23/baseline/preserved_source_hashes.json','art/characters/soldier-integration-2026-10-24/source-hashes.json']:
    hashes=json.loads((repo/manifest).read_text())
    for name,digest in hashes.items():
        p=repo/name
        if p.suffix.lower() not in ['.blend','.fbx','.glb','.png']:continue
        if p.is_file() and isinstance(digest,str):preserved[name]=sha(p)==digest
old=json.loads((repo/'art/characters/strafe-fix-2026-10-24/delivery-manifest.json').read_text())
for name,digest in old.items():
    if 'Builds/' in name or name.endswith('.mp4'):preserved[name]=sha(repo/name)==digest
preserved['supplied_original_glb']=sha(R/'source/original.glb')==sha(Path('/Users/liuwenjun/Downloads/20c68fb74bdce1d09948dd1b9bd0dd54.glb'))
assert all(preserved.values()),preserved
(R/'evidence/preservation.json').write_text(json.dumps(preserved,indent=2))
print(json.dumps({'images':len(checks),'reload_frames_each':len(frames),'preserved_files':len(preserved)},indent=2))
