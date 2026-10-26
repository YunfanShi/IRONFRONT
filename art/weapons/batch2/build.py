"""Articulate the six optimized GLB derivatives for the existing Unity weapon rig.
Run with Blender 5.2 background. Coordinates below are fractions of gun length:
u travels from muzzle to stock, v from the lowest surface upwards.
"""
import bpy,bmesh,json,math,shutil
from pathlib import Path
from mathutils import Vector
R=Path(__file__).resolve().parent
SOURCE=R.parents[3]/'模型游戏优化_第二批_2026-10-26'
DEST=R.parents[2]/'UnityProject/Assets/Art/Weapons/Batch2'
BOUNDS=json.loads((R/'bounds.json').read_text())
SPECS={
 '01':dict(id='BattleRifle',barrel=.262,right=(.70,.105),left=(.31,.237),sight=(.595,.326),mag=(.43,.59,.172),charge=(.70,.278),aperture=.020),
 '02':dict(id='Lmg',barrel=.237,right=(.72,.085),left=(.29,.216),sight=(.625,.333),mag=(.43,.60,.177),charge=(.67,.242),aperture=.019),
 '03':dict(id='Marksman',barrel=.136,right=(.72,.054),left=(.34,.119),sight=(.59,.194),mag=(.51,.61,.104),charge=(.69,.148),aperture=.012),
 '04':dict(id='Pistol',barrel=.570,right=(.79,.30),left=(.65,.29),sight=(.87,.675),mag=(.61,1.01,.04),charge=(.86,.570),aperture=.018),
 '05':dict(id='Sniper',barrel=.180,right=(.71,.052),left=(.35,.153),sight=(.59,.241),mag=(.53,.61,.128),charge=(.67,.164),aperture=.013),
 '06':dict(id='Smg',barrel=.337,right=(.57,.140),left=(.19,.310),sight=(.44,.428),mag=(.32,.45,.233),charge=(.43,.317),aperture=.023),
}
def active(o):
 bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
def bisect(o,point,normal):
 bm=bmesh.new();bm.from_mesh(o.data)
 bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=1e-7,plane_co=point,plane_no=normal)
 bm.to_mesh(o.data);bm.free()
def split(source,name,predicate):
 o=source.copy();o.data=source.data.copy();bpy.context.collection.objects.link(o);o.name=name
 for obj,keep in [(o,True),(source,False)]:
  bm=bmesh.new();bm.from_mesh(obj.data)
  bmesh.ops.delete(bm,geom=[f for f in bm.faces if predicate(f.calc_center_median())!=keep],context='FACES')
  bmesh.ops.delete(bm,geom=[v for v in bm.verts if not v.link_faces],context='VERTS')
  bm.to_mesh(obj.data);bm.free();obj.data.update()
 return o
def cap(o):
 bm=bmesh.new();bm.from_mesh(o.data)
 result=bmesh.ops.holes_fill(bm,edges=[e for e in bm.edges if e.is_boundary],sides=0)
 for f in result['faces']:f.material_index=1;f.smooth=False
 bmesh.ops.triangulate(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free()
def box(name,loc,size,mat):
 bpy.ops.mesh.primitive_cube_add(size=1,location=loc);o=bpy.context.object;o.name=name;o.scale=size
 bpy.ops.object.transform_apply(location=True,rotation=True,scale=True);o.data.materials.append(mat);return o
def boolean(body,cutter):
 active(body);m=body.modifiers.new('Open optical sight line','BOOLEAN');m.operation='DIFFERENCE';m.solver='EXACT';m.object=cutter
 bpy.ops.object.modifier_apply(modifier=m.name);bpy.data.objects.remove(cutter,do_unlink=True)
manifest=[]
for key,spec in SPECS.items():
 folder=DEST/spec['id'];folder.mkdir(parents=True,exist_ok=True)
 for role in ['BaseColor','Normal','MetallicSmoothness']:
  shutil.copy2(SOURCE/'UnityReady/OptimizedModelsBatch2'/f'Asset_{key}'/'Textures'/f'Asset_{key}_{role}.png',folder/f'{role}.png')
 b=BOUNDS[key];length=b['length'];lo=Vector(b['min']);record=dict(key=key,weapon=spec['id'],lods=[])
 # Every LOD is split from its independently optimized source, retaining UVs.
 for lod in range(3):
  bpy.ops.wm.open_mainfile(filepath=str(SOURCE/'Blender'/f'Asset_{key}_Optimized.blend'))
  body=bpy.data.objects[f'Asset_{key}_LOD{lod}']
  for o in list(bpy.data.objects):
   if o!=body:bpy.data.objects.remove(o,do_unlink=True)
  body.hide_render=False;body.hide_set(False);body.name='Body';active(body)
  dark=bpy.data.materials.new('Interior');dark.diffuse_color=(.035,.04,.045,1);body.data.materials.append(dark)
  # Normalize solely during articulation; final meshes return to game metres.
  for v in body.data.vertices:v.co=(v.co-Vector((0,lo.y,lo.z)))/length
  maglo,maghi,magtop=spec['mag']
  if key=='04':
   bisect(body,(0,.74,magtop),(0,-.33,1))
   mag=split(body,'Magazine',lambda p:p.y>maglo and p.z<magtop+.33*(p.y-.74)+1e-6)
  else:
   bisect(body,(0,0,magtop),(0,0,1))
   mag=split(body,'Magazine',lambda p:maglo<p.y<maghi and p.z<magtop+1e-6)
  cap(mag);cap(body)
  if not mag.data.polygons:raise RuntimeError('Empty magazine '+key)
  if key=='04':
   # Extend the authored floor plate into an internal, closed magazine body.
   neck=box('Internal magazine body',(0,.765,.19),(.106,.153,.285),dark)
   for v in neck.data.vertices:v.co.y-=.28*(v.co.z-.05)
   active(mag);neck.select_set(True);bpy.ops.object.join()
   bisect(body,(0,0,.523),(0,0,1));bolt=split(body,'Bolt',lambda p:p.z>.523001);cap(bolt);cap(body)
   parts=[body,mag,bolt]
  else:
   cu,cv=spec['charge'];side=.036 if key in ['02','05'] else .025
   handle=split(body,'ChargingHandle',lambda p:abs(p.y-cu)<.025 and abs(p.z-cv)<.027 and abs(p.x)>side)
   if len(handle.data.polygons):cap(handle);cap(body);parts=[body,mag,handle]
   else:bpy.data.objects.remove(handle,do_unlink=True);parts=[body,mag]
  # Generated optics arrived solid. A real aperture keeps the aiming ray open.
  su,sv=spec['sight'];rad=spec['aperture']
  optic=next((o for o in parts if o.name=='Bolt'),body) if key=='04' else body
  if key in ['03','05']:
   bpy.ops.mesh.primitive_cylinder_add(vertices=32,radius=rad,depth=.50,location=(0,su,sv),rotation=(math.pi/2,0,0))
   cutter=bpy.context.object;boolean(optic,cutter)
  else:
   cutter=box('Clear sight aperture',(0,su,sv),(rad*2,.24 if key!='04' else .34,.028 if key!='04' else .020),dark);boolean(optic,cutter)
  ru,rv=spec['right'];barrel=spec['barrel'];rear=.045 if key=='04' else .18
  def canonical(p):
   return Vector((p[0]*length/.65,(p[1]-ru)*length/.65+rear,(p[2]-barrel)*length/.65))
  for o in parts:
   active(o);bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
   for v in o.data.vertices:v.co=canonical(v.co)
   o.data.update();o.data.set_sharp_from_angle(angle=math.radians(60))
  lu,lv=spec['left'];cu,cv=spec['charge']
  # Left palm is just outside the handguard rather than inside its surface.
  leftx=(.027 if key=='04' else .065 if key=='02' else .041)/length
  anchors={'RightGrip':(0,ru,rv),'LeftGrip':(leftx,lu,lv),'Muzzle':(0,-.008,barrel),
    'Sight':(0,su,sv),'MagazineGrip':(0,(maglo+maghi)/2,magtop-.05 if key!='04' else .065),
    'ChargingGrip':((.04 if key!='04' else .023)/length,cu,cv)}
  for name,pos in anchors.items():
   e=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(e);e.location=canonical(pos);e.empty_display_size=.025
  bpy.ops.object.select_all(action='SELECT')
  bpy.ops.export_scene.fbx(filepath=str(folder/f'LOD{lod}.fbx'),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=False,add_leaf_bones=False,bake_anim=False,path_mode='RELATIVE')
  counts={o.name:sum(len(p.vertices)-2 for p in o.data.polygons) for o in parts}
  record['lods'].append(dict(level=lod,parts=counts,triangles=sum(counts.values())))
  if lod==0:record['unityAnchors']={n:[canonical(p).x,canonical(p).z,-canonical(p).y] for n,p in anchors.items()}
 manifest.append(record);print('WEAPON_EXPORTED',json.dumps(record),flush=True)
(R/'articulation.json').write_text(json.dumps(manifest,indent=2))
