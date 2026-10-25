"""Fresh-process import validation for one exported H8 GLB or FBX.

Usage: Blender -b --factory-startup --python validate_h8.py -- asset.glb report.json
"""
import bpy
import json
import sys
from pathlib import Path
from mathutils import Vector

args = sys.argv[sys.argv.index('--')+1:]
asset, report = map(Path, args[:2])
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
if asset.suffix.lower() == '.glb':
    bpy.ops.import_scene.gltf(filepath=str(asset))
elif asset.suffix.lower() == '.fbx':
    bpy.ops.import_scene.fbx(filepath=str(asset))
else:
    raise ValueError(asset)

def in_unity(v):
    return [round(float(v.x),4),round(float(v.z),4),round(float(-v.y),4)]

data = {'asset':str(asset),'format':asset.suffix.lower(),'objects':{},'materials':[], 'images':[], 'total_triangles':0}
mins=Vector((float('inf'),)*3); maxs=Vector((float('-inf'),)*3)
for o in bpy.data.objects:
    entry={'type':o.type,'parent':o.parent.name if o.parent else None,
           'pivot_blender':[round(float(x),4) for x in o.matrix_world.translation],
           'pivot_unity':in_unity(o.matrix_world.translation)}
    if o.type=='MESH':
        coords=[o.matrix_world @ Vector(corner) for corner in o.bound_box]
        lo=Vector(tuple(min(v[i] for v in coords) for i in range(3)))
        hi=Vector(tuple(max(v[i] for v in coords) for i in range(3)))
        entry['bounds_blender']=[[round(float(x),4) for x in lo],[round(float(x),4) for x in hi]]
        entry['bounds_unity']=[in_unity(Vector((lo.x,hi.y,lo.z))),in_unity(Vector((hi.x,lo.y,hi.z)))]
        entry['triangles']=sum(max(0,len(p.vertices)-2) for p in o.data.polygons)
        entry['uv_layers']=len(o.data.uv_layers)
        entry['materials']=[m.name if m else None for m in o.data.materials]
        data['total_triangles']+=entry['triangles']
        for i in range(3): mins[i]=min(mins[i],lo[i]); maxs[i]=max(maxs[i],hi[i])
    data['objects'][o.name]=entry
data['bounds_blender']=[[round(float(x),4) for x in mins],[round(float(x),4) for x in maxs]]
data['bounds_unity']=[in_unity(Vector((mins.x,maxs.y,mins.z))),in_unity(Vector((maxs.x,mins.y,maxs.z)))]
data['dimensions_m']=[round(float(maxs[i]-mins[i]),4) for i in range(3)]
data['materials']=sorted(m.name for m in bpy.data.materials if m.users)
data['images']=sorted([{'name':im.name,'size':list(im.size)} for im in bpy.data.images if im.users and im.size[0]],key=lambda x:x['name'])
assert 'Body' in data['objects'] and 'MainRotor' in data['objects'] and 'TailRotor' in data['objects']
assert data['total_triangles'] < 60000
report.write_text(json.dumps(data,indent=2))
print('VALIDATED',asset.name,'tris',data['total_triangles'],'unity_bounds',data['bounds_unity'])
