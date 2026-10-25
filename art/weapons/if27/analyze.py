import bpy,json
from pathlib import Path
R=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(R/'work/working.blend'))
o=bpy.data.objects['Body'];report={}
for z in [.03,.08,.13,.18,.21,.24,.27,.30,.33,.36,.39,.42]:
    vs=[v.co for v in o.data.vertices if abs(v.co.z-z)<.006]
    bins={}
    for v in vs:
        y=round(v.y/.025)*.025;k=f'{y:.3f}'
        bins.setdefault(k,[]).append(v.x)
    report[str(z)]={k:[round(min(v),4),round(max(v),4)] for k,v in sorted(bins.items(),key=lambda kv:float(kv[0]))}
(R/'work/slices.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report))
