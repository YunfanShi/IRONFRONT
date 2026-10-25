import bpy
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parent
SRC = ROOT / 'source' / 'hunyuan-blue-original.glb'
OUT = ROOT / 'work'
OUT.mkdir(parents=True, exist_ok=True)

def log(*values):
    print('TRIAL:', *values, flush=True)

t0 = time.monotonic()
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(SRC))
obj = next(o for o in bpy.data.objects if o.type == 'MESH')
log('import', obj.name, len(obj.data.vertices), len(obj.data.polygons), 'seconds', round(time.monotonic()-t0, 1))

# Retain the original source for texture baking. The other object is built only
# from volume and thus has no surviving source UVs.
hi = obj
hi.name = 'Highpoly bake source'
lo = hi.copy()
lo.data = hi.data.copy()
bpy.context.collection.objects.link(lo)
lo.name = 'Soldier voxel trial LOD0'
bpy.ops.object.select_all(action='DESELECT')
lo.select_set(True)
bpy.context.view_layer.objects.active = lo

t = time.monotonic()
m = lo.modifiers.new('Voxel remesh 3mm', 'REMESH')
m.mode = 'VOXEL'
m.voxel_size = 0.003
m.use_smooth_shade = True
bpy.ops.object.modifier_apply(modifier=m.name)
log('voxel', len(lo.data.vertices), len(lo.data.polygons), sum(len(p.vertices)-2 for p in lo.data.polygons), 'seconds', round(time.monotonic()-t, 1))

t = time.monotonic()
dec = lo.modifiers.new('Target 20000 triangles', 'DECIMATE')
dec.ratio = min(1.0, 20000 / max(1, sum(len(p.vertices)-2 for p in lo.data.polygons)))
dec.use_collapse_triangulate = True
bpy.ops.object.modifier_apply(modifier=dec.name)
log('decimated', len(lo.data.vertices), len(lo.data.polygons), sum(len(p.vertices)-2 for p in lo.data.polygons), 'seconds', round(time.monotonic()-t, 1))

for p in lo.data.polygons:
    p.use_smooth = True

hi.hide_render = True
hi.hide_set(True)
lo.data.materials.clear()
mat = bpy.data.materials.new('Trial neutral blue gray')
mat.diffuse_color = (0.13, 0.22, 0.29, 1)
mat.use_nodes = True
bsdf = mat.node_tree.nodes.get('Principled BSDF')
bsdf.inputs['Base Color'].default_value = (0.13, 0.22, 0.29, 1)
bsdf.inputs['Roughness'].default_value = 0.65
lo.data.materials.append(mat)

bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'voxel_trial.blend'))
bpy.ops.object.select_all(action='DESELECT')
lo.select_set(True)
bpy.context.view_layer.objects.active = lo
bpy.ops.export_scene.gltf(filepath=str(OUT/'voxel_trial.glb'), export_format='GLB', use_selection=True)
log('export complete', round(time.monotonic()-t0, 1))
