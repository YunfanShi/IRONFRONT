"""Read-only connected-component inventory of the supplied H8 mesh.

Run with Blender: blender -b --factory-startup --python analyze_source.py
"""
import bpy
import json
from array import array
from collections import defaultdict
from pathlib import Path

HERE = Path(__file__).resolve().parent
SOURCE = Path('/Users/liuwenjun/Downloads/33717aab9aa8b9b3fb2bb108d94498cb.glb')
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=str(SOURCE))
ob = next(o for o in bpy.data.objects if o.type == 'MESH')
me = ob.data
n = len(me.vertices)
parent = array('I', range(n))
rank = bytearray(n)

def find(x):
    while parent[x] != x:
        parent[x] = parent[parent[x]]
        x = parent[x]
    return x

def union(a, b):
    a, b = find(a), find(b)
    if a == b:
        return
    if rank[a] < rank[b]:
        a, b = b, a
    parent[b] = a
    if rank[a] == rank[b]:
        rank[a] += 1

for poly in me.polygons:
    vs = poly.vertices
    union(vs[0], vs[1])
    union(vs[0], vs[2])

stats = {}
for v in me.vertices:
    root = find(v.index)
    co = v.co
    if root not in stats:
        stats[root] = [0, 0, [co.x, co.y, co.z], [co.x, co.y, co.z], [0., 0., 0.]]
    s = stats[root]
    s[0] += 1
    for i in range(3):
        s[2][i] = min(s[2][i], co[i])
        s[3][i] = max(s[3][i], co[i])
        s[4][i] += co[i]
for poly in me.polygons:
    stats[find(poly.vertices[0])][1] += 1

components = []
for root, s in stats.items():
    count, faces, vmin, vmax, vsum = s
    components.append({
        'root': root, 'vertices': count, 'triangles': faces,
        'min': [round(x, 6) for x in vmin],
        'max': [round(x, 6) for x in vmax],
        'center': [round(x/count, 6) for x in vsum],
        'size': [round(vmax[i]-vmin[i], 6) for i in range(3)],
    })
components.sort(key=lambda c: c['triangles'], reverse=True)
summary = {
    'source': str(SOURCE), 'vertices': n, 'triangles': len(me.polygons),
    'components_count': len(components),
    'components': components,
}
dest = HERE/'source_components.json'
dest.write_text(json.dumps(summary, indent=2))
print('COMPONENTS', len(components), 'TOP100', [(c['root'], c['triangles'], c['center'], c['size']) for c in components[:100]])
print('WROTE', dest)
