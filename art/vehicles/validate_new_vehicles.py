"""Fresh-import J20/R4 final exports, including texture sizes and geometric seams.

Blender -b --factory-startup --python validate_new_vehicles.py
"""
import bpy
import bmesh
import hashlib
import json
import math
from pathlib import Path

HERE=Path(__file__).resolve().parent
results=[]
for folder,key in [('j20-stratus','j20'),('r4-scout','r4')]:
    root=HERE/folder
    build=json.loads((root/'build_metrics.json').read_text())
    report=[]
    for lod in range(3):
        pair=[]
        for ext in ['glb','fbx']:
            p=root/'exports'/f'{key}-lod{lod}.{ext}'
            bpy.ops.wm.read_factory_settings(use_empty=True)
            if ext=='glb': bpy.ops.import_scene.gltf(filepath=str(p))
            else: bpy.ops.wm.fbx_import(filepath=str(p))
            bpy.context.view_layer.update()
            objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
            assert len(objects)==1
            o=objects[0];me=o.data;me.calc_loop_triangles()
            assert o.name==build['authored_lods'][lod]['name'],o.name
            points=[o.matrix_world @ v.co for v in me.vertices]
            assert all(math.isfinite(x) for v in points for x in v)
            assert len(me.uv_layers)>0 and len(me.materials)==1
            mn=[min(v[i] for v in points) for i in range(3)]
            mx=[max(v[i] for v in points) for i in range(3)]
            triangles=len(me.loop_triangles)
            assert triangles==build['authored_lods'][lod]['triangles']
            bm=bmesh.new();bm.from_mesh(me);bm.transform(o.matrix_world)
            bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-7)
            boundary=sum(e.is_boundary for e in bm.edges)
            degenerate=sum(f.calc_area()<1e-14 for f in bm.faces)
            isolated=sum(not v.link_edges for v in bm.verts)
            assert boundary==0 and degenerate==0,(key,lod,ext,boundary,degenerate)
            bm.free()
            textures={n.image.name:list(n.image.size) for m in me.materials
                for n in m.node_tree.nodes if n.type=='TEX_IMAGE' and n.image}
            assert len(textures)>=2 and all(s==[2048,2048] for s in textures.values()),textures
            authored=build['authored_lods'][lod]
            delta=max(abs(x-y) for x,y in zip(mn+mx,authored['min']+authored['max']))
            assert delta<1e-5,(key,lod,ext,delta)
            record=dict(file=p.name,sha256=hashlib.sha256(p.read_bytes()).hexdigest(),
                triangles=triangles,bytes=p.stat().st_size,bounds=[mn,mx],
                textures=textures,geometric_boundary_edges=boundary,
                degenerate_faces=degenerate,isolated_vertices=isolated,
                authored_bounds_max_error=delta,passed=True)
            report.append(record);pair.append(record)
        assert pair[0]['triangles']==pair[1]['triangles']
    output=dict(blender=bpy.app.version_string,passed=True,source=build['source'],
        source_sha256=build['source_sha256'],exports=report)
    (root/'validation.json').write_text(json.dumps(output,indent=2))
    print('VALIDATED',key,len(report),'exports',flush=True)
    results.append(output)
print('ALL_NEW_VEHICLE_EXPORTS_VALID',sum(len(r['exports']) for r in results),flush=True)
