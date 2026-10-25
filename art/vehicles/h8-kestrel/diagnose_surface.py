"""Inspect the exported H8 UV range and wing/body surface openness."""
import bpy
import numpy as np
from pathlib import Path

here = Path(__file__).resolve().parent
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=str(here / 'blue-h8-lod0.glb'))
body = bpy.data.objects['Body']
me = body.data
world = np.array([(body.matrix_world @ v.co)[:] for v in me.vertices])
# Blender importer creates a Z-up scene.
print('WORLD_BOUNDS', world.min(axis=0), world.max(axis=0))
for uv in me.uv_layers:
    coords = np.array([l.uv[:] for l in uv.data])
    outside = np.any((coords < 0) | (coords > 1), axis=1)
    print('UV', uv.name, 'corners', len(coords), 'min', coords.min(axis=0), 'max', coords.max(axis=0), 'outside', outside.sum())
for side in (-1, 1):
    for inner, outer in ((1.4, 2.2), (2.2, 3.5), (3.5, 4.5), (4.5, 5.6)):
        section = world[(world[:,0]*side >= inner)&(world[:,0]*side < outer)]
        if len(section):
            print('WING_BIN',side,inner,outer,len(section),'Z q',np.quantile(section[:,2],[0,.1,.5,.9,1]),'Y q',np.quantile(section[:,1],[0,.1,.5,.9,1]))
for forward_min, forward_max in ((-2,0),(0,2),(2,4),(4,6)):
    section=world[(abs(world[:,0])<1.3)&(world[:,2]>=forward_min)&(world[:,2]<forward_max)]
    print('BODY_BIN',forward_min,forward_max,len(section),'Y q',np.quantile(section[:,1],[0,.1,.5,.9,1]) if len(section) else [])
