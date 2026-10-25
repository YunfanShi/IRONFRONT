import bpy
import bmesh
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parent
SRC = ROOT / 'source' / 'hunyuan-red-original.glb'
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

# This source makes a few tiny, closed, two-sided triangle islands during
# collapse. The exporters discard one face from each pair, leaving open edges.
# Remove only duplicate pairs whose three vertices have no other face users.
bm = bmesh.new()
bm.from_mesh(lo.data)
bm.verts.index_update()
face_groups = {}
for face in bm.faces:
    key = tuple(sorted(vertex.index for vertex in face.verts))
    face_groups.setdefault(key, []).append(face)
isolated_pairs = []
for group in face_groups.values():
    if len(group) < 2:
        continue
    if len(group) != 2 or any(set(vertex.link_faces) != set(group)
                              for vertex in group[0].verts):
        raise RuntimeError('Unexpected non-isolated duplicate face group')
    isolated_pairs.append(group)
delete_vertices = list({vertex for group in isolated_pairs
                        for vertex in group[0].verts})
if delete_vertices:
    bmesh.ops.delete(bm, geom=delete_vertices, context='VERTS')
bm.to_mesh(lo.data)
bm.free()
lo.data.update()
log('removed isolated duplicate face pairs', len(isolated_pairs),
    'remaining faces', len(lo.data.polygons))

for p in lo.data.polygons:
    p.use_smooth = True

hi.hide_render = True
hi.hide_set(True)
lo.data.materials.clear()
mat = bpy.data.materials.new('Trial neutral red gray')
mat.diffuse_color = (0.29, 0.12, 0.12, 1)
mat.use_nodes = True
bsdf = mat.node_tree.nodes.get('Principled BSDF')
bsdf.inputs['Base Color'].default_value = (0.29, 0.12, 0.12, 1)
bsdf.inputs['Roughness'].default_value = 0.65
lo.data.materials.append(mat)

bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'voxel_trial.blend'))
bpy.ops.object.select_all(action='DESELECT')
lo.select_set(True)
bpy.context.view_layer.objects.active = lo
bpy.ops.export_scene.gltf(filepath=str(OUT/'voxel_trial.glb'), export_format='GLB', use_selection=True)
log('export complete', round(time.monotonic()-t0, 1))
