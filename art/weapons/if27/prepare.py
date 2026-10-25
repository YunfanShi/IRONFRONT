"""Preserve source, weld UV seams geometrically and produce a working mesh."""
import bpy, bmesh, json, hashlib, math
from pathlib import Path
from mathutils import Vector
R=Path(__file__).resolve().parent
for folder in ('work','textures','exports','evidence'):(R/folder).mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(R/'source/original.glb'))
o=next(o for o in bpy.context.scene.objects if o.type=='MESH')
bpy.context.view_layer.objects.active=o;o.select_set(True)
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
report={'sha256':hashlib.sha256((R/'source/original.glb').read_bytes()).hexdigest(),'source_triangles':len(o.data.polygons),'textures':[]}
for mat in o.data.materials:
    for n in mat.node_tree.nodes:
        if n.type!='TEX_IMAGE' or not n.image:continue
        im=n.image
        role='normal' if 'normal' in im.name.lower() else 'metalrough' if 'metallic' in im.name.lower() else 'basecolor'
        report['textures'].append({'role':role,'original_size':list(im.size)})
        im.scale(2048,2048);im.filepath_raw=str(R/'textures'/f'if27_{role}.png');im.file_format='PNG';im.save();im.pack()
bm=bmesh.new();bm.from_mesh(o.data)
bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-6)
report['welded_vertices']=len(bm.verts);report['welded_boundary_edges']=sum(e.is_boundary for e in bm.edges)
bm.to_mesh(o.data);bm.free()
o.name='IF27_High';bpy.ops.wm.save_as_mainfile(filepath=str(R/'work/source-welded.blend'))
mod=o.modifiers.new('Preserve silhouette reduction','DECIMATE');mod.ratio=60000/len(o.data.polygons);mod.use_collapse_triangulate=True
bpy.ops.object.modifier_apply(modifier=mod.name)
bpy.ops.mesh.customdata_custom_splitnormals_clear()
o.data.set_sharp_from_angle(angle=math.radians(52))
for p in o.data.polygons:p.use_smooth=True
norm=o.modifiers.new('Weighted manufactured panels','WEIGHTED_NORMAL');norm.keep_sharp=True
bpy.ops.object.modifier_apply(modifier=norm.name)
report['working_triangles']=len(o.data.polygons)
report['bounds']=[[min(v.co[i] for v in o.data.vertices),max(v.co[i] for v in o.data.vertices)] for i in range(3)]
o.name='Body';bpy.ops.wm.save_as_mainfile(filepath=str(R/'work/working.blend'))
(R/'work/preparation.json').write_text(json.dumps(report,indent=2))
print('IF27_PREPARED',json.dumps(report),flush=True)
