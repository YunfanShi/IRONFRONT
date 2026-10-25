"""Fresh-import every delivered GLB/FBX; compare paired geometry and bounds.

Run with Blender --background --factory-startup --python validate_exports.py.
Does not modify asset files. Reports are written to final-validation/.
"""
import bpy
import bmesh
import hashlib
import json
import math
from pathlib import Path
from mathutils import Vector

HERE = Path(__file__).resolve().parent
OUT = HERE / 'final-validation'
OUT.mkdir(exist_ok=True)

def inspect(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if path.suffix == '.glb':
        bpy.ops.import_scene.gltf(filepath=str(path))
    else:
        bpy.ops.wm.fbx_import(filepath=str(path))
    bpy.context.view_layer.update()
    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    positions = [o.matrix_world @ v.co for o in meshes for v in o.data.vertices]
    assert meshes and all(math.isfinite(c) for v in positions for c in v), path
    mn = [min(v[i] for v in positions) for i in range(3)]
    mx = [max(v[i] for v in positions) for i in range(3)]
    report = dict(file=str(path.relative_to(HERE)), sha256=hashlib.sha256(path.read_bytes()).hexdigest(),
                  bytes=path.stat().st_size, meshes=len(meshes), triangles=0,
                  min=mn, max=mx, dimensions=[b-a for a,b in zip(mn,mx)],
                  nodes=sorted(o.name for o in bpy.context.scene.objects), mesh_details=[])
    images={n.image.name: list(n.image.size) for mat in bpy.data.materials if mat.use_nodes
            for n in mat.node_tree.nodes if n.type=='TEX_IMAGE' and n.image}
    assert all(size==[2048,2048] for size in images.values()), (path, images)
    report['texture_dimensions']=images
    for o in meshes:
        me = o.data
        me.calc_loop_triangles()
        tris = len(me.loop_triangles)
        assert tris and me.materials, o.name
        assert all(math.isfinite(x) for uv in me.uv_layers for loop in uv.data for x in loop.uv)
        degenerate = sum(p.area < 1e-12 for p in me.polygons)
        assert degenerate == 0, (path, o.name, degenerate)
        bm = bmesh.new(); bm.from_mesh(me)
        # Indexed GLB/FBX necessarily split some vertices for UV/normal data;
        # report geometric boundaries after merging coincident positions.
        bm.transform(o.matrix_world)
        bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=.00001)
        boundary = sum(e.is_boundary for e in bm.edges)
        isolated = sum(not v.link_edges for v in bm.verts)
        bm.free()
        report['triangles'] += tris
        report['mesh_details'].append(dict(name=o.name, triangles=tris,
            geometric_boundary_edges=boundary, isolated_vertices=isolated,
            uv_layers=len(me.uv_layers), materials=[m.name for m in me.materials]))
    report['pass'] = True
    return report

paths = sorted((HERE/'h8-kestrel').glob('*.glb')) + sorted((HERE/'h8-kestrel').glob('*.fbx'))
paths += sorted((HERE/'a40-watchtower/exports').glob('*.glb')) + sorted((HERE/'a40-watchtower/exports').glob('*.fbx'))
reports = []
for path in paths:
    report = inspect(path)
    (OUT/(path.stem+'_'+path.suffix[1:]+'.json')).write_text(json.dumps(report, indent=2))
    reports.append(report)
    print('FRESH_IMPORT_PASS', report['file'], report['triangles'], flush=True)
by_path = {r['file']:r for r in reports}
for glb in [r for r in reports if r['file'].endswith('.glb')]:
    fbx = by_path[glb['file'][:-4]+'.fbx']
    assert glb['triangles'] == fbx['triangles'], glb['file']
    assert max(abs(a-b) for a,b in zip(glb['min']+glb['max'],fbx['min']+fbx['max'])) < .001, glb['file']
summary = dict(blender=bpy.app.version_string, exports=len(reports),
               paired_glb_fbx_match=True, passed=True, assets=reports)
(OUT/'summary.json').write_text(json.dumps(summary, indent=2))
print('ALL_EXPORTS_VALID', len(reports), flush=True)
