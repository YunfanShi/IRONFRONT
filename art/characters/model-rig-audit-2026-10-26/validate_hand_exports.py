import bpy,json,math
from pathlib import Path
R=Path(__file__).resolve().parent
reports=[]
def read(p):
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(p),use_anim=False)
 data={}
 for o in bpy.data.objects:
  if o.type!='MESH':continue
  data[o.name]={'vertices':[list(o.matrix_world@v.co) for v in o.data.vertices],'triangles':len(o.data.polygons),'uv':[[list(v.uv) for v in layer.data] for layer in o.data.uv_layers],'weights':[{o.vertex_groups[g.group].name:g.weight for g in v.groups} for v in o.data.vertices]}
 rig=next(o for o in bpy.data.objects if o.type=='ARMATURE')
 return data,set(b.name for b in rig.data.bones)
for team in ['blue','red']:
 for lod in range(3):
  folder=R.parent/(team+'-soldier');file=team+'-soldier-skinned-lod'+str(lod)+'.fbx'
  a,ab=read(folder/'rigged-v02'/file);b,bb=read(folder/'rigged-v03'/file)
  assert set(a)==set(b) and bb-ab=={'Thumb_L','Thumb_R'}
  diff=0.;badweights=0;bodyequal=True
  for n,old in a.items():
   new=b[n];assert len(old['vertices'])==len(new['vertices']) and old['triangles']==new['triangles'] and old['uv']==new['uv']
   diff=max(diff,max(math.dist(x,y) for x,y in zip(old['vertices'],new['vertices'])))
   for w in new['weights']:
    if len(w)>4 or abs(sum(w.values())-1)>.0001:badweights+=1
   if n.endswith('_Body'):bodyequal &= all(max([abs(x.get(k,0)-y.get(k,0)) for k in set(x)|set(y)]+[0])<1e-5 for x,y in zip(old['weights'],new['weights']))
  print(team,lod,diff,badweights,bodyequal,flush=True)
  assert diff<1e-5 and badweights==0 and bodyequal
  reports.append({'team':team,'lod':lod,'verticesTrianglesUVPreserved':True,'maxVertexDisplacementM':diff,'bodyWeightsUnchanged':bodyequal,'invalidSkinVertices':badweights,'newBones':sorted(bb-ab)})
R.joinpath('fresh-import-validation.json').write_text(json.dumps({'passed':True,'exports':reports},indent=2));print('FRESH_HAND_EXPORT_PASS')
