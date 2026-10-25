import bpy, json, hashlib, struct
from pathlib import Path
ROOT=Path(__file__).resolve().parent
report=[]
def snapshot(path):
 bpy.ops.wm.open_mainfile(filepath=str(path));result={}
 for o in bpy.data.objects:
  if o.type!='MESH':continue
  h=hashlib.sha256()
  for v in o.data.vertices:h.update(struct.pack('fff',*v.co))
  for f in o.data.polygons:h.update(struct.pack('i'*len(f.vertices),*f.vertices))
  for uv in o.data.uv_layers:
   for p in uv.data:h.update(struct.pack('ff',*p.uv))
  result[o.name]=dict(hash=h.hexdigest(),vertices=len(o.data.vertices),triangles=sum(len(p.vertices)-2 for p in o.data.polygons),materials=[m.name for m in o.data.materials])
 return result
for team in ('blue','red'):
 folder=ROOT/(team+'-soldier')
 old=snapshot(folder/'rigged'/(team+'-soldier-rigged-v01.blend'))
 new=snapshot(folder/'rigged-v02'/(team+'-soldier-rigged-v02.blend'))
 if old!=new:raise RuntimeError('Geometry or UV changed '+team)
 report.append(dict(team=team,pass_geometry_uv_materials=True,meshes=new))
(ROOT/'motion-refinement-2026-10-24'/'preserved-geometry.json').write_text(json.dumps(report,indent=2))
print('SPINE_GEOMETRY_PRESERVATION_PASS',flush=True)
