"""Summarize final Player and independent CPU skin checks; no source mutation."""
from pathlib import Path
import json, hashlib, csv, math, re
import numpy as np
R=Path(__file__).resolve().parent;repo=R.parents[2]
def v(p):return np.array([p[k] for k in 'xyz'])
records=[]
for suffix in ['-build01','-build02','-build03','-build04','']:
    poses=json.loads((R/f'thumb-skin-audit{suffix}.json').read_text())['poses'];rows=[]
    for d in poses:
        pts=np.array([v(j['p']) for j in d['points'] if j['thumb']>.999]);fs=np.array([v(j['p']) for j in d['points'] if j['fingers']>.8])
        root=v(d['thumbRoot']);center=pts.mean(0);actual=(center-root)/np.linalg.norm(center-root)
        rows.append({'phase':d['phase'],'pureThumbVertices':len(pts),'surfaceVsDriverDegrees':float(np.degrees(np.arccos(np.clip(actual@v(d['thumbAxis']),-1,1)))),'thumbCenterRadialFromFingers':float((center-fs.mean(0))@v(d['radial']))})
    records.append({'build':suffix[-2:] if suffix else '05','poses':rows})
assert all(x['surfaceVsDriverDegrees']<.1 and x['thumbCenterRadialFromFingers']>0 for x in records[-1]['poses'])
(R/'thumb-surface-summary.json').write_text(json.dumps(records,indent=2)+'\n')
base=json.loads((R/'baseline.json').read_text())
prior=json.loads((R.parent/'p8-legacy-pack-2026-10-27/baseline.json').read_text())
preservation=[]
for path,sha in {**prior['files'],**base['files']}.items():
    now=hashlib.sha256((repo/path).read_bytes()).hexdigest();preservation.append({'path':path,'baselineSha256':sha,'currentSha256':now,'unchanged':sha==now})
assert sum(not x['unchanged'] for x in preservation)==4
probes=json.loads((R/'pistol-summary-pistol.json').read_text())
assert len(probes)==2 and all(x['exitCode']==0 and x['report'].startswith('PASS\nVersion=0.66.12\nRuntimeErrors=0') for x in probes)
metrics=list(csv.DictReader((R/'checks/pistol-pistol/support-anatomy.csv').open()))
angles=[float(x['left_wrist_degrees']) for x in metrics]
thumbs=[float(x['thumb_radial_dot']) for x in metrics]
assert max(angles)<38.1 and min(thumbs)>0
images=json.loads((R/'image-checks.json').read_text());assert not images['errors']
log=(R/'build05.log').read_text();assert 'IRONFRONT optimization build PASS:' in log and 'error CS' not in log
validation={'version':'0.66.12','baselineCommit':base['commit'],'build':'build05','developmentPlayer':True,'unity':'6000.5.10f1','buildBytes':int(re.search(r'build PASS: (\d+) bytes',log).group(1)),'probes':[{'probe':x['probe'],'checks':sum(z.startswith('OK ') for z in x['report'].splitlines()),'runtimeErrors':0,'exitCode':0} for x in probes],'imageCount':images['count'],'imageErrors':len(images['errors']),'anatomy':{'sampleCount':len(metrics),'maxLeftWristDegrees':max(angles),'minThumbSurfaceRadialDot':min(thumbs),'keyPoses':[x for x in metrics if not x['pose'].startswith('frame')],'baseline':'baseline-anatomy.json','surfaceGeometry':'thumb-surface-summary.json'},'preservation':preservation,'visualReview':['Blue-Pistol-hip-close.png','Blue-Pistol-reload66-contact.png','Blue-Pistol-reload74.png','Blue-Pistol-reload87-contact.png','review/P8-wrist-before-after.jpg','review/P8-thumb-before-after.jpg','review/P8-final-sequence.jpg'],'limitations':['Original low-poly shared-four-finger mesh and single thumb are retained.','No exhaustive triangle intersection check.','Reverse diagnostic viewpoints can reveal the existing shoulder cut surface.'],'superseded':'build01-04 and checks-build01-04 are rejected calibration iterations, not final evidence.'}
(R/'validation.json').write_text(json.dumps(validation,ensure_ascii=False,indent=2)+'\n')
print(json.dumps({'probes':validation['probes'],'images':images['count'],'samples':len(metrics),'wristMax':max(angles),'thumbSideMin':min(thumbs),'preserved':sum(x['unchanged'] for x in preservation)},ensure_ascii=False))
