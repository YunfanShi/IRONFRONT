"""Read-only connected-component measurements of the existing optimized armor."""
import bpy, json
from pathlib import Path

ROOT = Path(__file__).resolve().parent
for key, folder in [('r4', 'r4-scout'), ('u8', 'u8-rover'), ('m2', 'm2-courier'), ('r3', 'r3-firefly'), ('j20', 'j20-stratus')]:
    bpy.ops.wm.open_mainfile(filepath=str(ROOT/folder/f'{key}-optimized.blend'))
    obj = next(o for o in bpy.data.objects if o.type == 'MESH' and 'LOD0' in o.name)
    mesh = obj.data
    parents = list(range(len(mesh.vertices)))
    def find(i):
        while parents[i] != i:
            parents[i] = parents[parents[i]]
            i = parents[i]
        return i
    for e in mesh.edges:
        a,b = map(find, e.vertices)
        parents[b] = a
    stats = {}
    for v in mesh.vertices:
        r = find(v.index)
        s = stats.setdefault(r, dict(vertices=0, triangles=0, min=[1e9]*3, max=[-1e9]*3))
        co = obj.matrix_world @ v.co
        s['vertices'] += 1
        for k in range(3):
            s['min'][k] = min(s['min'][k], co[k]); s['max'][k] = max(s['max'][k], co[k])
    for f in mesh.polygons: stats[find(f.vertices[0])]['triangles'] += len(f.vertices)-2
    out = ROOT/folder/'articulated'
    out.mkdir(exist_ok=True)
    for s in stats.values():
        s['center'] = [(a+b)/2 for a,b in zip(s['min'],s['max'])]
        s['size'] = [b-a for a,b in zip(s['min'],s['max'])]
    report = dict(key=key, object=obj.name, matrix=[list(r) for r in obj.matrix_world],
                  components=sorted(stats.values(), key=lambda s:s['triangles'],reverse=True))
    (out/'source_components.json').write_text(json.dumps(report,indent=2))
    print(key, 'components',len(stats), 'top',json.dumps(report['components'][:22]),flush=True)
