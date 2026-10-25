from pathlib import Path
import json,sys,csv
sys.path.insert(0,'/Users/liuwenjun/.codex/skills/scenario-unity-expert/scripts')
import ut_review
r=Path(__file__).resolve().parent
results=[]
for p in sorted(list((r/'motion-player').rglob('*.png'))+list((r/'development').glob('*.png'))+list(r.glob('*-LOD*.png'))+list(r.glob('FP-*.png'))):
 c=ut_review.image_checks(str(p));results.append({'file':str(p.relative_to(r)),'checks':c,'verdict':ut_review.image_verdict(c)})
(r/'final-image-checks.json').write_text(json.dumps(results,indent=2))
errors=[x['file'] for x in results if any(v.startswith('error:') for v in x['verdict'])]
print('FRAME_CHECKS',len(results),'ERRORS',errors,flush=True)
files=[r/'motion-player'/'crawl'/f'{n:04d}.png' for n in [12,16,20,24,28,32,36,40,44]]
ut_review.contact_sheet([str(p) for p in files],str(r/'crawl-cycle.png'),cols=3,tile=420,labels=[f'Crawl {n/30:.2f}s' for n in [12,16,20,24,28,32,36,40,44]])
files=[r/'motion-player'/'idle'/'0030.png',r/'motion-player'/'walk'/'0030.png',r/'motion-player'/'run'/'0030.png',r/'motion-player'/'crouch'/'0030.png',r/'motion-player'/'prone-aim'/'0030.png',r/'motion-player'/'crawl-top-30.png']
ut_review.contact_sheet([str(p) for p in files],str(r/'delivery-preview.png'),cols=3,tile=440,labels=['Ready','Walk','Run','Crouch','Prone aim','Alternating crawl'])
assert not errors
