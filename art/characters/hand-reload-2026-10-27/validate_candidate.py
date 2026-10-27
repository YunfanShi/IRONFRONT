import bpy,json,hashlib,struct,math
from pathlib import Path
R=Path(__file__).resolve().parent
report=[]
def mesh_signature(o):
 return hashlib.sha256(b''.join(struct.pack('3f',*v.co) for v in o.data.vertices)+b''.join(struct.pack('If',g.group,g.weight) for v in o.data.vertices for g in v.groups)).hexdigest()
for team in ['blue','red']:
 bpy.ops.wm.open_mainfile(filepath=str(R.parent/(team+'-soldier')/'rigged-v03'/(team+'-soldier-rigged-v03.blend')))
 before={o.name:mesh_signature(o) for o in bpy.data.objects if o.type=='MESH' and o.name.endswith('_Body')}
 bpy.ops.wm.open_mainfile(filepath=str(R/'candidate'/team/(team+'-soldier-hand-candidate.blend')))
 for o in bpy.data.objects:
  if o.name in before:assert mesh_signature(o)==before[o.name]
 report.append({'team':team,'bodyGeometryAndWeightsPreserved':True})
 for lod in range(3):
  bpy.ops.wm.read_factory_settings(use_empty=True)
  bpy.ops.import_scene.fbx(filepath=str(R/'candidate'/team/(team+'-soldier-skinned-lod'+str(lod)+'.fbx')))
  arm=next(o for o in bpy.data.objects if o.type=='ARMATURE');assert len(arm.data.bones)==46
  assert not any(n.startswith('Fingers_') for n in arm.data.bones.keys())
  expected=[f+str(j)+'_'+s for s in ['L','R'] for f in ['Index','Middle','Ring','Pinky','Thumb'] for j in range(1,4)]
  assert all(n in arm.data.bones for n in expected)
  verts=0;max_inf=0;max_err=0
  for o in bpy.data.objects:
   if o.type!='MESH':continue
   for v in o.data.vertices:
    w=[g.weight for g in v.groups if g.weight>1e-6];max_inf=max(max_inf,len(w));max_err=max(max_err,abs(sum(w)-1))
    assert w and len(w)<=4 and abs(sum(w)-1)<2e-5, (o.name,v.index,w)
    assert all(math.isfinite(x) for x in v.co)
   verts+=len(o.data.vertices)
  report.append({'team':team,'lod':lod,'freshImport':True,'bones':46,'digitJoints':30,'vertices':verts,'maxInfluencesIncludingPreservedBody':max_inf,'maxWeightError':max_err})
(R/'validation.json').write_text(json.dumps({'pass':True,'checks':report},indent=2));print('CANDIDATE_ASSET_VALIDATION_PASS')
