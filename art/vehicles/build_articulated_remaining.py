"""Preserve accepted atlases/LOD meshes; split real mechanical joints in new files.
Blender --background --factory-startup --python this_file.py -- r4 u8 m2 r3 j20
"""
import bpy,bmesh,math,json,sys,hashlib
from pathlib import Path
from mathutils import Vector,Matrix
ROOT=Path(__file__).resolve().parent
# Reuse material and scene helpers without running the armor build entry point.
exec((ROOT/'build_articulated_armor.py').read_text().split('args=sys.argv')[0].split('def build(key):')[0])
CONFIG={
 'r4':dict(folder='r4-scout',scale=3.6/.6606673896,wheels=[-.327,.344],wz=.126,wr=.125,wx=.20,tz=.501,ty=.005,bz=.608,bcut=.551,yaw=math.pi),
 'u8':dict(folder='u8-rover',scale=3.4/.560847342,wheels=[-.390,.310],wz=.115,wr=.116,wx=.17,tz=.480,ty=-.085,bz=.54,bcut=.505,yaw=math.radians(29)),
 'm2':dict(folder='m2-courier',scale=2.55/1.1331070065),
 'r3':dict(folder='r3-firefly',scale=8/.8004325628),
 'j20':dict(folder='j20-stratus',scale=12.5/1.0238708258)}

def cut(bm,p,n):
 bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=1e-7,plane_co=p,plane_no=n,clear_inner=False,clear_outer=False)

def split(obj,key,c,paint,joint,lod):
 obj.data.transform(obj.matrix_world);obj.matrix_world=Matrix.Identity(4)
 bm=bmesh.new();bm.from_mesh(obj.data)
 if key in ('r4','u8'):
  cut(bm,(0,0,c['tz']),(0,0,1))
  cut(bm,(0,0,c['bcut']),(0,0,1))
  assembly=bm.faces.layers.int.new('assembly')
  candidates={f for f in bm.faces if f.calc_center_median().z>c['tz']+1e-6}
  islands=[]
  while candidates:
   seed=candidates.pop();island={seed};todo=[seed]
   while todo:
    f=todo.pop()
    for e in f.edges:
     for near in e.link_faces:
      if near in candidates:candidates.remove(near);island.add(near);todo.append(near)
   islands.append(island)
  upper=max(islands,key=len)
  for f in bm.faces:f[assembly]=int(f in upper)
  for x in (-c['wx'],c['wx']):cut(bm,(x,0,0),(1,0,0))
  cut(bm,(0,0,c['wz']+c['wr']),(0,0,1));cut(bm,(0,0,0),(0,1,0))
  labels=['Hull','TurretArmor','Cannon']+[f'Wheel_{s}_{i}' for s in ('L','R') for i in range(2)]
  def label(p,f):
   x,y,z=p
   if f[assembly]:return 'Cannon' if z>c['bcut']+1e-6 else 'TurretArmor'
   i=min(range(2),key=lambda i:abs(y-c['wheels'][i]))
   if abs(x)>c['wx']+1e-6 and ((y-c['wheels'][i])/c['wr'])**2+((z-c['wz'])/c['wr'])**2<1.08:return f'Wheel_{"L" if x<0 else "R"}_{i}'
   return 'Hull'
 elif key=='m2':
  # Stem plane follows the raked steering tube; fork and handlebars stay together.
  cut(bm,(0,-.213,.50),(0,1,-.37));cut(bm,(0,0,.50),(0,0,1));cut(bm,(0,-.12,0),(0,1,0))
  for x in (-.044,.044):cut(bm,(x,0,0),(1,0,0))
  cut(bm,(0,0,.346),(0,0,1))
  labels=['Hull','Fork','Wheel_Front','Wheel_Rear']
  def label(p,f):
   x,y,z=p
   front=((y+.213)-.37*(z-.50)<0) if z<.5 else y<-.12
   for name,cy in [('Front',-.388),('Rear',.344)]:
    if abs(x)<.044-1e-6 and z<.346-1e-6 and ((y-cy)/.178)**2+((z-.176)/.178)**2<1.02:return 'Wheel_'+name
   return 'Fork' if front else 'Hull'
 elif key=='r3':
  cut(bm,(0,0,.377),(0,0,1));cut(bm,(-.011,0,0),(1,0,0));cut(bm,(0,.605,0),(0,1,0))
  cut(bm,(0,0,.337),(0,0,1));cut(bm,(0,-.004,0),(0,1,0));cut(bm,(0,.108,0),(0,1,0))
  cut(bm,(0,0,.075),(0,0,1));cut(bm,(0,0,.14),(0,0,1));cut(bm,(-.10,0,0),(1,0,0));cut(bm,(0,-.22,0),(0,1,0))
  labels=['Hull','MainRotorMesh','TailRotorMesh','RadarMesh','Cannon']
  def label(p,f):
   x,y,z=p
   if x<-.10-1e-6 and y<-.22-1e-6 and .075+1e-6<z<.14-1e-6:return 'Cannon'
   if z>.377+1e-6 and y<.4:return 'MainRotorMesh'
   if x<-.011-1e-6 and y>.605+1e-6 and not (y<.635 and z>.305):return 'TailRotorMesh'
   if -.004+1e-6<y<.108-1e-6 and z>.337+1e-6 and x*x+(y-.041)**2<.055**2:return 'RadarMesh'
   return 'Hull'
 else:
  cut(bm,(0,0,.066),(0,0,1));cut(bm,(0,-.17,0),(0,1,0));cut(bm,(0,0,0),(1,0,0))
  cut(bm,(0,0,.135),(0,0,1));cut(bm,(0,-.53,0),(0,1,0))
  labels=['Hull','Wheel_Nose','Wheel_L_Main','Wheel_R_Main','Cannon']
  def label(p,f):
   x,y,z=p
   if y<-.53-1e-6 and z<.135-1e-6:return 'Cannon'
   if z<.066-1e-6:return 'Wheel_Nose' if y<-.17 else 'Wheel_L_Main' if x<0 else 'Wheel_R_Main'
   return 'Hull'
 obj.data.materials.clear()
 for l in labels:obj.data.materials.append(bpy.data.materials.new('SEG_'+l))
 for f in bm.faces:f.material_index=labels.index(label(f.calc_center_median(),f))
 bm.to_mesh(obj.data);bm.free();obj.data.update()
 active(obj);bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.separate(type='MATERIAL');bpy.ops.object.mode_set(mode='OBJECT')
 parts={}
 for part in list(bpy.context.selected_objects):
  l=part.data.materials[part.data.polygons[0].material_index].name.removeprefix('SEG_').split('.')[0]
  part.name=f'{l}_LOD{lod}';parts[l]=part
  part.data.materials.clear();part.data.materials.append(paint);part.data.materials.append(joint)
  for f in part.data.polygons:f.material_index=0
  bm=bmesh.new();bm.from_mesh(part.data)
  # Cap the split seam, leaving the accepted surface/UV untouched.
  caps=bmesh.ops.holes_fill(bm,edges=[e for e in bm.edges if e.is_boundary],sides=0).get('faces',[])
  for f in caps:f.material_index=1;f.smooth=False
  bmesh.ops.triangulate(bm,faces=list(bm.faces));bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(part.data);bm.free()
 if key in ('r4','u8'):
  center=Vector((0,c['ty'],c['tz']));rotation=Matrix.Translation(center)@Matrix.Rotation(c['yaw'],4,'Z')@Matrix.Translation(-center)
  for l in ('TurretArmor','Cannon'):parts[l].data.transform(rotation)
 assert set(parts)==set(labels),(key,lod,list(parts))
 return parts

def build(key):
 c=CONFIG[key];folder=c['folder'];s=c['scale'];out=ROOT/folder/'articulated';(out/'exports').mkdir(parents=True,exist_ok=True)
 source=ROOT/folder/f'{key}-optimized.blend';bpy.ops.wm.open_mainfile(filepath=str(source))
 sources=sorted([o for o in bpy.data.objects if o.type=='MESH' and 'LOD' in o.name],key=lambda o:o.name)
 for o in list(bpy.data.objects):
  if o not in sources:bpy.data.objects.remove(o,do_unlink=True)
 bpy.context.scene.unit_settings.system='METRIC';bpy.context.scene.unit_settings.scale_length=1
 paint=material(key,folder);red=material(key,folder,True);red.use_fake_user=True
 joint=bpy.data.materials.new('JointSteel');joint.diffuse_color=(.055,.067,.075,1);joint.use_nodes=True
 p=joint.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=joint.diffuse_color;p.inputs['Metallic'].default_value=.45;p.inputs['Roughness'].default_value=.72
 roots=[];reports=[]
 for lod,src in enumerate(sources):
  src.hide_set(False);src.hide_render=False;src.hide_viewport=False
  parts=split(src,key,c,paint,joint,lod);root=empty(f'{key.upper()}_LOD{lod}',(0,0,0));roots.append(root)
  suf='.'+str(lod).zfill(3) if lod else ''
  def pivot(name,co,parent=root):return empty(name+suf,Vector(co)*s,parent)
  parents={};origins={}
  if key in ('r4','u8'):
   t=pivot('Turret',(0,c['ty'],c['tz']));b=pivot('Barrels',(-.037 if key=='r4' else 0,c['ty']-.025,c['bz']),t)
   parents.update(TurretArmor=t,Cannon=b)
   for side in ('L','R'):
    for i,cy in enumerate(c['wheels']):
     l=f'Wheel_{side}_{i}';pts=[v.co for v in parts[l].data.vertices];origins[l]=Vector(((min(v.x for v in pts)+max(v.x for v in pts))/2,cy,c['wz']))*s
   cylinder(f'TurretBearing_LOD{lod}',.075*s,.012*s,Vector((0,c['ty'],c['tz']))*s,joint,root)
  elif key=='m2':
   st=pivot('Steering',(0,-.213,.50));parents['Fork']=st;parents['Wheel_Front']=st
   origins['Wheel_Front']=Vector((0,-.388,.176))*s;origins['Wheel_Rear']=Vector((0,.344,.176))*s
   for n,co in {'DriverSeat':(0,.10,.405),'PassengerSeat':(0,.32,.445),'DriverEye':(0,.13,.735),'PassengerEye':(0,.36,.82),'LeftGrip':(-.235,-.195,.583),'RightGrip':(.235,-.195,.583),'LeftPeg':(-.177,.03,.174),'RightPeg':(.177,.03,.174),'PassengerLeftGrip':(-.12,.34,.445),'PassengerRightGrip':(.12,.34,.445),'PassengerLeftPeg':(-.15,.27,.215),'PassengerRightPeg':(.15,.27,.215)}.items():pivot(n,co,st if n in ('LeftGrip','RightGrip') else root)
  elif key=='r3':
   parents['MainRotorMesh']=pivot('MainRotor',(0,-.057,.401));parents['TailRotorMesh']=pivot('TailRotor',(-.019,.641,.269));parents['RadarMesh']=pivot('Radar',(0,.041,.326))
   t=pivot('Turret',(-.13,-.245,.102));parents['Cannon']=pivot('Barrels',(-.13,-.245,.102),t)
   pivot('PilotEye',(-.045,-.30,.318));pivot('PilotSeat',(-.045,-.20,.17));pivot('Muzzle',(-.13,-.39,.102),parents['Cannon'])
  else:
   for l in [n for n in parts if n.startswith('Wheel_')]:
    pts=[v.co for v in parts[l].data.vertices];origins[l]=Vector([(min(v[i] for v in pts)+max(v[i] for v in pts))/2 for i in range(3)])*s
   t=pivot('Turret',(0,-.54,.126));parents['Cannon']=pivot('Barrels',(0,-.54,.126),t)
   pivot('PilotEye',(0,-.43,.325));pivot('PilotSeat',(0,-.39,.23));pivot('Muzzle',(0,-.57,.126),parents['Cannon'])
  for l,part in parts.items():
   part.data.transform(Matrix.Scale(s,4));par=parents.get(l,root)
   origin=origins.get(l,par.matrix_world.translation.copy())
   part.data.transform(Matrix.Translation(-origin));part.location=origin;bpy.context.view_layer.update();parent_keep(part,par)
   part['bas_role']=l;part['lod_level']=lod
  if key in ('r4','u8'):
   pts=[parts['Cannon'].matrix_world@v.co for v in parts['Cannon'].data.vertices];nose=min(p.y for p in pts);rim=[p for p in pts if p.y<nose+.006*s]
   empty('Muzzle'+suf,(sum(p.x for p in rim)/len(rim),nose-.025,sum(p.z for p in rim)/len(rim)),b)
  meshes=[o for o in descendants(root) if o.type=='MESH'];pts=[o.matrix_world@v.co for o in meshes for v in o.data.vertices]
  anchors={o.name:[o.matrix_world.translation.x,o.matrix_world.translation.z,-o.matrix_world.translation.y] for o in descendants(root) if o.type=='EMPTY' or o.name.startswith('Wheel_')}
  reports.append(dict(lod=lod,triangles=sum(sum(len(f.vertices)-2 for f in o.data.polygons) for o in meshes),parts={l:len(p.data.polygons) for l,p in parts.items()},bounds_blender=[[min(p[i] for p in pts) for i in range(3)],[max(p[i] for p in pts) for i in range(3)]],anchors_unity=anchors))
  bpy.ops.object.select_all(action='DESELECT')
  for o in descendants(root):o.select_set(True)
  bpy.context.view_layer.objects.active=root
  bpy.ops.export_scene.fbx(filepath=str(out/'exports'/f'{key}-articulated-lod{lod}.fbx'),use_selection=True,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',path_mode='RELATIVE',embed_textures=False,use_mesh_modifiers=True,use_custom_props=True,add_leaf_bones=False,bake_anim=False)
 for lod,root in enumerate(roots):
  for o in descendants(root):o.hide_render=lod>0;o.hide_set(lod>0)
 bpy.ops.wm.save_as_mainfile(filepath=str(out/f'{key}-articulated.blend'))
 (out/'build.json').write_text(json.dumps(dict(key=key,scale=s,source_sha256=hashlib.sha256(source.read_bytes()).hexdigest(),lods=reports),indent=2));print('REMAINING_BUILT',key,flush=True)
for key in sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else CONFIG:build(key)
