"""Preserve accepted geometry/UVs; correct glove skin and add thumb controls.
Creates independent rigged-v03 sources and FBX, never overwrites rigged-v02.
"""
import bpy,json,hashlib
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parent.parent

def smooth(a,b,v):
 t=max(0,min(1,(v-a)/(b-a)));return t*t*(3-2*t)
def digest(o):
 return hashlib.sha256(b''.join(__import__('struct').pack('3f',*v.co) for v in o.data.vertices)).hexdigest()
reports=[]
for team in ['blue','red']:
 folder=ROOT/(team+'-soldier');out=folder/'rigged-v03';out.mkdir(exist_ok=True)
 bpy.ops.wm.open_mainfile(filepath=str(folder/'rigged-v02'/(team+'-soldier-rigged-v02.blend')))
 rig=next(o for o in bpy.data.objects if o.type=='ARMATURE');meshes=[o for o in bpy.data.objects if o.type=='MESH' and 'LOD' in o.name]
 before={o.name:digest(o) for o in meshes};stats=[]
 bpy.ops.object.select_all(action='DESELECT');rig.hide_set(False);rig.select_set(True);bpy.context.view_layer.objects.active=rig;bpy.ops.object.mode_set(mode='EDIT')
 for side,sign in [('L',-1),('R',1)]:
  # The previous wrist was at z=.90, inside the knuckles. Place it
  # at the visible cuff seam; do not alter any surface vertices.
  wrist=Vector((sign*.418,-.015,.98))
  rig.data.edit_bones['Forearm_'+side].tail=wrist
  rig.data.edit_bones['Hand_'+side].head=wrist
  rig.data.edit_bones['Hand_'+side].tail=(sign*.45,-.015,.885)
  rig.data.edit_bones['Fingers_'+side].head=(sign*.45,-.015,.885)
  b=rig.data.edit_bones.new('Thumb_'+side);b.head=(sign*.406,-.015,.951);b.tail=(sign*.369,-.016,.86);b.parent=rig.data.edit_bones['Hand_'+side];b.align_roll(Vector((0,1,0)))
 bpy.ops.object.mode_set(mode='OBJECT')
 for o in meshes:
  if o.name.endswith('_Body'):continue
  side='L' if o.name.endswith('_ArmL') else 'R'
  for n in ['Thumb_'+side]:
   if not o.vertex_groups.get(n):o.vertex_groups.new(name=n)
  changed=0
  for v in o.data.vertices:
   x,y,z=v.co
   if abs(x)<.32 or z>1.005:continue
   blend=smooth(1.005,.955,z);thumb=smooth(.432,.398,abs(x))*smooth(.960,.915,z)
   finger=smooth(.905,.855,z)*(1-thumb)
   skin={'Forearm_'+side:1-blend,'Hand_'+side:blend*(1-thumb-finger),'Fingers_'+side:blend*finger,'Thumb_'+side:blend*thumb}
   for group_index in [g.group for g in v.groups]:o.vertex_groups[group_index].remove([v.index])
   for n,w in skin.items():
    if w>1e-6:o.vertex_groups[n].add([v.index],w,'REPLACE')
   changed+=1
  assert digest(o)==before[o.name]
  for v in o.data.vertices:
   assert len(v.groups)<=4 and abs(sum(g.weight for g in v.groups)-1)<1e-5, (o.name,v.index,[(g.group,g.weight) for g in v.groups])
  stats.append({'mesh':o.name,'reweightedVertices':changed,'geometrySHA256':before[o.name]})
 for lod in range(3):
  parts=[o for o in meshes if 'LOD'+str(lod) in o.name]
  bpy.ops.object.select_all(action='DESELECT');rig.select_set(True)
  for o in parts:o.hide_set(False);o.select_set(True)
  bpy.context.view_layer.objects.active=rig
  bpy.ops.export_scene.fbx(filepath=str(out/(team+'-soldier-skinned-lod'+str(lod)+'.fbx')),use_selection=True,object_types={'MESH','ARMATURE'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',add_leaf_bones=False,use_armature_deform_only=True,bake_anim=False,path_mode='RELATIVE')
 for o in meshes:o.hide_render='LOD0' not in o.name;o.hide_set('LOD0' not in o.name)
 bpy.ops.wm.save_as_mainfile(filepath=str(out/(team+'-soldier-rigged-v03.blend')))
 report={'team':team,'preservedGeometry':all(digest(o)==before[o.name] for o in meshes),'bones':len(rig.data.bones),'changes':stats,'source':'rigged-v02','bodyAndLocomotionWeights':'unchanged','maxHandInfluences':4}
 (out/'rig-report.json').write_text(json.dumps(report,indent=2));reports.append(report)
(ROOT/'model-rig-audit-2026-10-26/hand-repair-report.json').write_text(json.dumps(reports,indent=2))
print('HAND_SKIN_REPAIR_PASS')
