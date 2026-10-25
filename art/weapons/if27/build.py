"""Build articulated IF-27, preserving the supplied surface and UVs.
Run prepare.py first. Source coordinates: -Y muzzle, Z up.
"""
import bpy,bmesh,json,math
from pathlib import Path
from mathutils import Vector
R=Path(__file__).resolve().parent
S=1.2
def active(o):
    bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
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
def box(name,loc,size,mat,bevel=.002):
    bpy.ops.mesh.primitive_cube_add(size=1,location=loc);o=bpy.context.object;o.name=name;o.scale=size
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);o.data.materials.append(mat)
    m=o.modifiers.new('Manufactured edge','BEVEL');m.width=bevel;m.segments=3;bpy.ops.object.modifier_apply(modifier=m.name)
    return o
def triangles(o):o.data.calc_loop_triangles();return len(o.data.loop_triangles)
bpy.ops.wm.open_mainfile(filepath=str(R/'work/working.blend'))
body=bpy.data.objects['Body'];active(body)
# Reload saved runtime maps: the imported image datablocks still carry their
# original packed 4K bytes even after scale/save. GLB must embed the actual 2K maps.
for m in body.data.materials:
    for n in m.node_tree.nodes:
        if n.type!='TEX_IMAGE' or not n.image:continue
        name=n.image.name.lower()
        role='normal' if 'normal' in name else 'metalrough' if 'metallic' in name or 'metalrough' in name else 'basecolor'
        n.image=bpy.data.images.load(str(R/'textures'/f'if27_{role}.png'),check_existing=False)
        n.image.colorspace_settings.name='sRGB' if role=='basecolor' else 'Non-Color';n.image.pack()
dark=bpy.data.materials.new('Interior graphite');dark.use_nodes=True
bs=dark.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(.025,.03,.035,1);bs.inputs['Metallic'].default_value=.55;bs.inputs['Roughness'].default_value=.42
body.data.materials.append(dark)
# The supplied optic has a solid reconstructed lens. Cut a real sight line;
# the visible casing and its top silhouette remain from the source mesh.
cutter=box('Optic clear aperture',(0,.03,.405),(.038,.23,.034),dark,.004)
active(body);mod=body.modifiers.new('Optic through opening','BOOLEAN');mod.operation='DIFFERENCE';mod.solver='EXACT';mod.object=cutter
bpy.ops.object.modifier_apply(modifier=mod.name);bpy.data.objects.remove(cutter,do_unlink=True)
# Keep the rear stock available in world views but independently hideable
# for a close first-person camera. The split lies on the buffer tube collar.
bm=bmesh.new();bm.from_mesh(body.data)
bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=1e-6,plane_co=(0,.248,0),plane_no=(0,1,0))
bm.to_mesh(body.data);bm.free()
stock=split(body,'Stock',lambda p:p.y>.248001);cap(stock);cap(body)
# A flat hidden cut at the actual magazine-well entrance. Both exposed ends
# are closed; the added neck slides into the receiver during reload.
bm=bmesh.new();bm.from_mesh(body.data)
bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=1e-6,plane_co=(0,0,.184),plane_no=(0,0,1))
bm.to_mesh(body.data);bm.free()
mag=split(body,'Magazine',lambda p:p.z<.184001 and p.y<.052 and p.y>-.17)
cap(mag);cap(body)
neck=box('Magazine neck',(0,-.028,.190),(.046,.092,.036),dark)
active(mag);neck.select_set(True);bpy.ops.object.join()
handle=split(body,'ChargingHandle',lambda p:p.z>.348 and p.y>.158 and p.y<.214 and abs(p.x)>.023)
cap(handle);cap(body)
stem=box('Charging stem',(0,.181,.349),(.063,.025,.013),dark,.001)
active(handle);stem.select_set(True);bpy.ops.object.join()
# Internal slide is cosmetic; it has no gameplay or ballistics authority.
bolt=box('Bolt',(-.026,.043,.309),(.014,.093,.028),dark,.003)
parts=[body,mag,handle,bolt,stock]
for o in parts:
    active(o);bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    for v in o.data.vertices:v.co=Vector((v.co.x,(v.co.y-.02)*S,(v.co.z-.30)*S));v.co.x*=S
    o.data.update()
    # Retain detailed source maps; sharpen only genuine manufactured corners.
    o.data.set_sharp_from_angle(angle=math.radians(55))
    for p in o.data.polygons:p.use_smooth=p.material_index==0
    o.data.calc_loop_triangles()
anchors={
 'RightGrip':(0,(.17-.02)*S,(.15-.30)*S),
 'LeftGrip':(.065,(-.30-.02)*S,(.255-.30)*S),
 'Muzzle':(0,(-.654-.02)*S,(.306-.30)*S),
 'Sight':(0,(.03-.02)*S,(.405-.30)*S),
 'MagazineGrip':(0,(-.035-.02)*S,(.10-.30)*S),
 'ChargingGrip':(.060,(.184-.02)*S,(.357-.30)*S)}
root=bpy.data.objects.new('IF27',None);bpy.context.collection.objects.link(root)
for o in parts:o.parent=root
for name,pos in anchors.items():
    e=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(e);e.parent=root;e.location=pos;e.empty_display_size=.025
bpy.ops.wm.save_as_mainfile(filepath=str(R/'if27-articulated.blend'))
report={'scale':S,'canonical_blender_anchors':anchors,'lods':[]}
source=[o.data.copy() for o in parts]
for lod,budget in enumerate([61000,18000,6000,2000]):
    for o,data in zip(parts,source):
        o.data=data.copy();active(o)
        if lod:
            mod=o.modifiers.new('Distance LOD','DECIMATE');mod.ratio=max(.035,budget/61000);mod.use_collapse_triangulate=True
            bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.export_scene.fbx(filepath=str(R/'exports'/f'if27_lod{lod}.fbx'),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_space_transform=False,add_leaf_bones=False,bake_anim=False,path_mode='RELATIVE')
    if lod==0:
        bpy.ops.export_scene.gltf(filepath=str(R/'exports/if27.glb'),export_format='GLB',use_selection=True,export_yup=True)
    report['lods'].append({'level':lod,'parts':{o.name:triangles(o) for o in parts},'triangles':sum(triangles(o) for o in parts)})
(R/'work/build.json').write_text(json.dumps(report,indent=2))
print('IF27_EXPORT_PASS',json.dumps(report),flush=True)
