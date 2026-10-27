"""Reproducible FP candidate: retain soldier body/UVs, replace gloves from CC0 hand topology.
Only the P8/IF-27 first-person resource opts in. Original v03 files are untouched.
"""
import bpy,bmesh,json,hashlib,math
from pathlib import Path
from mathutils import Vector,Matrix
ROOT=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'source/FPS ARMS RIG 1.blend'))
ref=next(o for o in bpy.data.objects if o.type=='ARMATURE')
mesh=next(o for o in bpy.data.objects if o.type=='MESH')
wrist=ref.data.bones['hand.L'].head_local.copy()
Y=(ref.data.bones['f_middle.01.L'].head_local-wrist).normalized()
X=ref.data.bones['f_index.01.L'].head_local-ref.data.bones['f_pinky.01.L'].head_local
X=(X-Y*X.dot(Y)).normalized();Z=X.cross(Y).normalized()
frame=Matrix((X,Y,Z)).transposed();inv=frame.transposed()
# Anatomical neutral surface and original authored skin weights, cropped at the cuff.
keep={v.index for v in mesh.data.vertices if v.co.x>0 and (v.co-wrist).dot(Y)>-.20}
verts={v.index:{'p':list(inv@(v.co-wrist)),'w':{mesh.vertex_groups[g.group].name:g.weight for g in v.groups}} for v in mesh.data.vertices if v.index in keep}
faces=[list(p.vertices) for p in mesh.data.polygons if all(i in keep for i in p.vertices)]
finger_names=['Index','Middle','Ring','Pinky','Thumb']
bones={}
for f in finger_names:
 for j in range(1,4):
  n=('thumb' if f=='Thumb' else 'f_'+f.lower())+'.0'+str(j)+'.L';b=ref.data.bones[n]
  bones[(f,j)]=(inv@(b.head_local-wrist),inv@(b.tail_local-wrist),n)
reports=[]
for team in ['blue','red']:
 src=ROOT.parent/(team+'-soldier')/'rigged-v03'/(team+'-soldier-rigged-v03.blend')
 out=ROOT/'candidate'/team;out.mkdir(parents=True,exist_ok=True)
 bpy.ops.wm.open_mainfile(filepath=str(src));rig=next(o for o in bpy.data.objects if o.type=='ARMATURE');rig.data.pose_position='REST'
 parts=[o for o in bpy.data.objects if o.type=='MESH' and 'LOD' in o.name]
 # Resolve legacy packed-image paths so exported FBXs also reopen with materials.
 for img in bpy.data.images:
  if img.packed_file:
   tex=out/'textures'/Path(img.name).name;tex.parent.mkdir(exist_ok=True)
   tex.write_bytes(img.packed_file.data);img.filepath=str(tex)

 glove=bpy.data.materials.new('HandGlove');glove.diffuse_color=(.028,.034,.041,1);glove.use_nodes=True
 bs=glove.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(.028,.034,.041,1);bs.inputs['Roughness'].default_value=.82
 bpy.ops.object.select_all(action='DESELECT');rig.hide_set(False);rig.select_set(True);bpy.context.view_layer.objects.active=rig;bpy.ops.object.mode_set(mode='EDIT')
 maps={}
 for side,sign in [('L',-1),('R',1)]:
  ey=Vector((sign*.032,0,-.095)).normalized();ex=Vector((-sign*.095,0,-.032)).normalized();ez=Vector((0,1,0))
  # Mirroring the same reference hand also mirrors winding below.
  basis=Matrix((-ex,ey,-ez)).transposed();origin=Vector((sign*.418,-.015,.98));scale=.086
  maps[side]=(basis,origin,scale)
  for old in ['Fingers_'+side,'Thumb_'+side]:
   if rig.data.edit_bones.get(old):rig.data.edit_bones.remove(rig.data.edit_bones[old])
  for f in finger_names:
   for j in range(1,4):
    h,t,_=bones[(f,j)];b=rig.data.edit_bones.new(f+str(j)+'_'+side);b.head=origin+basis@h*scale;b.tail=origin+basis@t*scale
    b.parent=rig.data.edit_bones['Hand_'+side if j==1 else f+str(j-1)+'_'+side];b.align_roll(ez);b.use_deform=True
 bpy.ops.object.mode_set(mode='OBJECT')
 stats=[]
 for o in parts:
  if o.name.endswith('_Body'):continue
  side='L' if o.name.endswith('_ArmL') else 'R';sign=-1 if side=='L' else 1;basis,origin,scale=maps[side]
  # Remove old mitten geometry at the wrist, preserve every upper-arm/body vertex.
  bm=bmesh.new();bm.from_mesh(o.data)
  bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),plane_co=(0,0,1.02),plane_no=(0,0,1),clear_inner=True,clear_outer=False)
  bm.to_mesh(o.data);bm.free()
  # Slim the cuff and distal armor toward the anatomical forearm, retaining UVs.
  for v in o.data.vertices:
   if v.co.z<1.17:
    t=max(0,min(1,(1.17-v.co.z)/.16));z=v.co.z
    a=Vector((sign*.335,0,1.2));b=origin
    center=a+(b-a)*max(0,min(1,(1.2-z)/.22))
    v.co.x=center.x+(v.co.x-center.x)*(1-.30*t)
    v.co.y=center.y+(v.co.y-center.y)*(1-.30*t)
  ids=list(verts);remap={n:i for i,n in enumerate(ids)}
  # Since reference-frame projection and target basis can reflect, use actual determinant.
  flip=(basis@inv).determinant()<0
  fs=[[remap[i] for i in (list(reversed(p)) if flip else p)] for p in faces]
  data=bpy.data.meshes.new(o.name+'_Glove');data.from_pydata([origin+basis@Vector(verts[i]['p'])*scale for i in ids],[],fs);data.update()
  hand=bpy.data.objects.new(o.name+'_Glove',data);bpy.context.collection.objects.link(hand);hand.data.materials.append(glove)
  for i,n in enumerate(ids):
   ww={}
   for rn,w in verts[n]['w'].items():
    target='Hand_'+side
    for f in finger_names:
     for j in range(1,4):
      if rn==bones[(f,j)][2]:target=f+str(j)+'_'+side
    # Proximal cut weights transition into the forearm under the retained cuff.
    if rn.startswith('forearm'):target='Forearm_'+side
    ww[target]=ww.get(target,0)+w
   ww=sorted(ww.items(),key=lambda x:-x[1])[:4];total=sum(w for _,w in ww)
   for n,w in ww:
    vg=hand.vertex_groups.get(n) or hand.vertex_groups.new(name=n);vg.add([i],w/total,'REPLACE')
  for p in hand.data.polygons:p.use_smooth=True
  # Short flexible wrist gaiter overlaps both the glove edge and trimmed sleeve.
  cv=[];cf=[];rings=[(-.015,.030,.023),(.005,.031,.025),(.035,.034,.027),(.070,.040,.030)]
  for h,rx,ry in rings:
   c=origin+Vector((-sign*.36*h,0,h))
   for n in range(20):
    theta=2*math.pi*n/20;cv.append(c+Vector((rx*math.cos(theta),ry*math.sin(theta),0)))
  for ring in range(3):
   for n in range(20):cf.append((ring*20+n,ring*20+(n+1)%20,(ring+1)*20+(n+1)%20,(ring+1)*20+n))
  cd=bpy.data.meshes.new('Flexible wrist cuff');cd.from_pydata(cv,[],cf);cd.materials.append(glove)
  cuff=bpy.data.objects.new('Flexible wrist cuff',cd);bpy.context.collection.objects.link(cuff)
  vg=cuff.vertex_groups.new(name='Hand_'+side);fg=cuff.vertex_groups.new(name='Forearm_'+side)
  for n in range(80):
   w=[1,.7,.2,0][n//20]
   if w>0:vg.add([n],w,'REPLACE')
   if w<1:fg.add([n],1-w,'REPLACE')
  for p in cd.polygons:p.use_smooth=True
  bpy.ops.object.select_all(action='DESELECT');hand.select_set(True);cuff.select_set(True);bpy.context.view_layer.objects.active=hand;bpy.ops.object.join()
  bpy.ops.object.select_all(action='DESELECT');o.hide_set(False);o.select_set(True);hand.select_set(True);bpy.context.view_layer.objects.active=o;bpy.ops.object.join()
  # LOD1/2 keep the same deformation topology in this close-up candidate.
  for v in o.data.vertices:
   merged={}
   for g in v.groups:
    idx=g.group
    if o.vertex_groups[idx].name not in rig.data.bones:
     idx=o.vertex_groups['Forearm_'+side if v.co.z>1.02 else 'Hand_'+side].index
    merged[idx]=merged.get(idx,0)+g.weight
   weights=sorted([(i,w) for i,w in merged.items() if w>=.001],key=lambda p:-p[1])[:4];total=sum(w for _,w in weights)
   for idx in [g.group for g in v.groups]:o.vertex_groups[idx].remove([v.index])
   for g,w in weights:o.vertex_groups[g].add([v.index],w/total,'REPLACE')
  o.data.update();stats.append({'mesh':o.name,'vertices':len(o.data.vertices),'triangles':sum(len(p.vertices)-2 for p in o.data.polygons)})
 for lod in range(3):
  bpy.ops.object.select_all(action='DESELECT');rig.select_set(True)
  for o in parts:
   if 'LOD'+str(lod) in o.name:o.hide_set(False);o.select_set(True)
  bpy.context.view_layer.objects.active=rig
  bpy.ops.export_scene.fbx(filepath=str(out/(team+'-soldier-skinned-lod'+str(lod)+'.fbx')),use_selection=True,object_types={'MESH','ARMATURE'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',add_leaf_bones=False,use_armature_deform_only=True,bake_anim=False,path_mode='RELATIVE')
 for o in parts:o.hide_render='LOD0' not in o.name;o.hide_set('LOD0' not in o.name)
 rig.data.pose_position='POSE'
 bpy.ops.wm.save_as_mainfile(filepath=str(out/(team+'-soldier-hand-candidate.blend')))
 reports.append({'team':team,'bones':len(rig.data.bones),'meshes':stats,'source':'v03 + para FPS arms CC0 hand topology','body':'unchanged','scope':'first-person P8/IF27 candidate'})
(ROOT/'candidate-report.json').write_text(json.dumps(reports,indent=2));print('HAND_CANDIDATE_CREATED')
