"""Sleeve volume revision of the 0.66.4/0.66.5 FP candidate.
Reuses its hand construction, bone positions and weights. Only sleeve section
radii and the fitted forearm pad are changed. Older sources remain available.
"""
import bpy,json,math
from pathlib import Path
from mathutils import Vector,Matrix
ROOT=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(ROOT.parent/'hand-reload-2026-10-27/source/FPS ARMS RIG 1.blend'))
ref=next(o for o in bpy.data.objects if o.type=='ARMATURE')
mesh=next(o for o in bpy.data.objects if o.type=='MESH')
wrist=ref.data.bones['hand.L'].head_local.copy()
Y=(ref.data.bones['f_middle.01.L'].head_local-wrist).normalized()
X=ref.data.bones['f_index.01.L'].head_local-ref.data.bones['f_pinky.01.L'].head_local
X=(X-Y*X.dot(Y)).normalized();Z=X.cross(Y).normalized()
frame=Matrix((X,Y,Z)).transposed();inv=frame.transposed()
# Anatomical neutral surface and original authored skin weights, cropped at the cuff.
keep={v.index for v in mesh.data.vertices if v.co.x>0 and (v.co-wrist).dot(Y)>-.60}
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
  # A complete candidate sleeve avoids joining a narrow anatomical forearm
  # to the original armor's oversized, displaced open cross-section.
  # The full-body meshes and the original v03 assets stay unchanged.
  o.data.clear_geometry()
  ids=list(verts);remap={n:i for i,n in enumerate(ids)}
  # Since reference-frame projection and target basis can reflect, use actual determinant.
  flip=(basis@inv).determinant()<0
  fs=[[remap[i] for i in (list(reversed(p)) if flip else p)] for p in faces]
  def glove_point(n):
   return origin+basis@Vector(verts[n]['p'])*scale
  data=bpy.data.meshes.new(o.name+'_Glove');data.from_pydata([glove_point(i) for i in ids],[],fs);data.update()
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
  # All sleeve rings share the forearm transform; only the original wrist
  # topology blends into the hand. Small folds keep a rounded sleeve silhouette.
  elbow=Vector((sign*.335,0,1.2));axis=(elbow-origin).normalized()
  dorsal=Vector((0,-1,0));dorsal=(dorsal-axis*dorsal.dot(axis)).normalized();across=axis.cross(dorsal).normalized()
  sv=[];sf=[];sm=[]
  # Keep the inner wrist opening; increase cloth allowance progressively.
  rings=[(.022,.033,.028),(.030,.034,.029),(.043,.037,.031),(.058,.040,.034),(.074,.043,.0365),(.09,.046,.039),(.115,.048,.041),(.14,.050,.043),(.17,.052,.045),(.20,.055,.047),(.23,.058,.049)]
  shoulder=rig.data.bones['UpperArm_'+side].head_local.copy()
  lower_length=(elbow-origin).length;upper_axis=(shoulder-elbow).normalized();upper_length=(shoulder-elbow).length
  rings=rings+[(lower_length+.025,.060,.050),(lower_length+.055,.062,.051),(lower_length+.09,.064,.053),(lower_length+.14,.066,.055),(lower_length+.19,.067,.056),(lower_length+upper_length,.069,.058),(lower_length+upper_length+.12,.066,.056),(lower_length+upper_length+.35,.063,.054)]
  centers=[];frames=[]
  for j,(h,rx,ry) in enumerate(rings):
   c=origin+axis*min(h,lower_length)+upper_axis*max(0,h-lower_length)
   blend=max(0,min(1,(h-lower_length+.04)/.08));ring_axis=axis.lerp(upper_axis,blend).normalized()
   ring_dorsal=(dorsal-ring_axis*dorsal.dot(ring_axis)).normalized();ring_across=ring_axis.cross(ring_dorsal).normalized()
   centers.append(c);frames.append((ring_across,ring_dorsal))
   for n in range(32):
    a=2*math.pi*n/32;fold=1+((.05 if abs(h-lower_length)<.07 else .025)*math.cos(a*3+j*1.7) if j>2 else 0)
    sv.append(c+ring_across*(rx*math.cos(a)*fold)+ring_dorsal*(ry*math.sin(a)*fold))
  for j in range(len(rings)-1):
   for n in range(32):sf.append((j*32+n,j*32+(n+1)%32,(j+1)*32+(n+1)%32,(j+1)*32+n));sm.append(0 if j<2 else 1)
  # Inward cuff return closes the lip around the underlying anatomical wrist.
  for n in range(32):
   a=2*math.pi*n/32;sv.append(origin+axis*.028+across*(.025*math.cos(a))+dorsal*(.021*math.sin(a)))
  for n in range(32):sf.append((n,(n+1)%32,len(rings)*32+(n+1)%32,len(rings)*32+n));sm.append(0)
  sleeve_mesh=bpy.data.meshes.new('Continuous FP sleeve');sleeve_mesh.from_pydata(sv,[],[list(reversed(f)) for f in sf]);sleeve_mesh.materials.append(glove)
  cloth=bpy.data.materials.get('Candidate sleeve') or bpy.data.materials.new('Candidate sleeve');cloth.diffuse_color=(.065,.095,.145,1) if team=='blue' else (.14,.055,.06,1)
  sleeve_mesh.materials.append(cloth)
  uv=sleeve_mesh.uv_layers.new(name='Sleeve weave')
  for poly in sleeve_mesh.polygons:
   raw=[sleeve_mesh.loops[k].vertex_index for k in poly.loop_indices]
   wrap=any(i%32==0 for i in raw) and any(i%32==31 for i in raw)
   for k in poly.loop_indices:
    index=sleeve_mesh.loops[k].vertex_index;n=index%32;j=min(index//32,len(rings)-1)
    uv.data[k].uv=(1.0 if wrap and n==0 else n/32,rings[j][0]*4)
  for i,p in enumerate(sleeve_mesh.polygons):p.material_index=sm[i];p.use_smooth=True
  sleeve=bpy.data.objects.new('Continuous FP sleeve',sleeve_mesh);bpy.context.collection.objects.link(sleeve)
  fg=sleeve.vertex_groups.new(name='Forearm_'+side);ug=sleeve.vertex_groups.new(name='UpperArm_'+side)
  for j,(h,_,_) in enumerate(rings):
   w=max(0,min(1,(h-lower_length+.035)/.07));indices=list(range(j*32,(j+1)*32))
   if w<1:fg.add(indices,1-w,'REPLACE')
   if w>0:ug.add(indices,w,'REPLACE')
  fg.add(list(range(len(rings)*32,len(sv))),1,'REPLACE')
  # Bevelled dorsal pad follows the forearm, never the hand or fingers.
  outline=[(-.022,.065),(.022,.065),(.032,.082),(.034,.157),(.023,.181),(-.023,.181),(-.034,.157),(-.032,.082)]
  def sleeve_depth(h):
   for k in range(len(rings)-1):
    a,b=rings[k],rings[k+1]
    if a[0]<=h<=b[0]:
     t=(h-a[0])/(b[0]-a[0]);return a[2]*(1-t)+b[2]*t
   return rings[-1][2]
  pv=[];pf=[]
  for inset,rise in [(1,0),(.85,.007)]:
   for x,h in outline:
    centerh=.123;hh=centerh+(h-centerh)*inset
    pv.append(origin+axis*hh+across*(x*inset*1.18)+dorsal*(sleeve_depth(hh)*1.025+.0015+rise))
  pf.append(tuple(range(8,16)))
  for n in range(8):pf.append((n,(n+1)%8,(n+1)%8+8,n+8))
  pd=bpy.data.meshes.new('Dorsal forearm pad');pd.from_pydata(pv,[],pf)
  armor=bpy.data.materials.get('Candidate armor') or bpy.data.materials.new('Candidate armor');armor.diffuse_color=(.22,.30,.43,1) if team=='blue' else (.42,.18,.19,1);pd.materials.append(armor)
  pad=bpy.data.objects.new('Dorsal forearm pad',pd);bpy.context.collection.objects.link(pad);pad.vertex_groups.new(name='Forearm_'+side).add(list(range(16)),1,'REPLACE')
  # Establish deterministic material order: original, glove, sleeve, pad.
  o.data.materials.append(glove);o.data.materials.append(cloth);o.data.materials.append(armor)
  bpy.ops.object.select_all(action='DESELECT');o.hide_set(False);o.select_set(True);hand.select_set(True);sleeve.select_set(True);pad.select_set(True);bpy.context.view_layer.objects.active=o;bpy.ops.object.join()

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
 reports.append({'team':team,'bones':len(rig.data.bones),'meshes':stats,'source':'v03 + para FPS arms CC0 hand topology','body':'unchanged','scope':'first-person P8/IF27 sleeve volume only'})
(ROOT/'candidate-report.json').write_text(json.dumps(reports,indent=2));print('SLEEVE_VOLUME_CREATED')
