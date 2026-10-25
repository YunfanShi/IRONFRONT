import bpy
import json
from collections import defaultdict
from pathlib import Path

SOURCE = Path('/Users/liuwenjun/Downloads/d722c1edebb754a5735858477acc4459.glb')
OUT = Path(__file__).with_name('source_components.json')
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(SOURCE))
obj = next(o for o in bpy.context.scene.objects if o.type == 'MESH')
mesh = obj.data
n = len(mesh.vertices)
parent = list(range(n))
rank = bytearray(n)

def find(i):
    while parent[i] != i:
        parent[i] = parent[parent[i]]
        i = parent[i]
    return i

def union(a, b):
    a, b = find(a), find(b)
    if a == b: return
    if rank[a] < rank[b]: a, b = b, a
    parent[b] = a
    if rank[a] == rank[b]: rank[a] += 1

for poly in mesh.polygons:
    v = poly.vertices
    union(v[0], v[1])
    union(v[0], v[2])

stats = {}
for poly in mesh.polygons:
    r = find(poly.vertices[0])
    if r not in stats:
        stats[r] = dict(triangles=0, min=[float('inf')]*3, max=[-float('inf')]*3)
    s = stats[r]
    s['triangles'] += 1
    for vi in poly.vertices:
        co = mesh.vertices[vi].co
        for k in range(3):
            x = co[k]
            if x < s['min'][k]: s['min'][k] = x
            if x > s['max'][k]: s['max'][k] = x

for s in stats.values():
    s['center'] = [(a+b)/2 for a,b in zip(s['min'],s['max'])]
    s['size'] = [b-a for a,b in zip(s['min'],s['max'])]

top = sorted(stats.values(), key=lambda s: s['triangles'], reverse=True)[:300]
OUT.write_text(json.dumps(dict(object=obj.name,
    object_matrix_world=[list(row) for row in obj.matrix_world],
    parent=obj.parent.name if obj.parent else None,
    vertices=n, polygons=len(mesh.polygons),
    components=len(stats), top=top), indent=2))
print('ANALYZE', OUT, 'COMPONENTS', len(stats))
