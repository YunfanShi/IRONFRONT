"""Temporary experiment: voxelize a backing shell for the H8 Body."""
import bpy
from pathlib import Path

here=Path(__file__).resolve().parent
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=str(here/'blue-h8-lod0.glb'))
body=bpy.data.objects['Body']
print('BODY_LOCAL_BOUNDS',[(min(v.co[i] for v in body.data.vertices),max(v.co[i] for v in body.data.vertices)) for i in range(3)])
print('BODY_MATRIX',body.matrix_world[:])
proxy=body.copy(); proxy.data=body.data.copy(); bpy.context.collection.objects.link(proxy)
proxy.name='BodyOcclusionLiner'
proxy.data.remesh_voxel_size=.08
bpy.ops.object.select_all(action='DESELECT'); proxy.select_set(True); bpy.context.view_layer.objects.active=proxy
bpy.ops.object.voxel_remesh()
print('REMESH',len(proxy.data.vertices),len(proxy.data.polygons))
mod=proxy.modifiers.new('CloseOpenVoxels','SMOOTH'); mod.factor=1.4; mod.iterations=2
bpy.ops.object.modifier_apply(modifier=mod.name)
mod=proxy.modifiers.new('InteriorOffset','DISPLACE'); mod.strength=-.09
bpy.ops.object.modifier_apply(modifier=mod.name)
mod=proxy.modifiers.new('Reduce','DECIMATE'); mod.ratio=min(1,15000/len(proxy.data.polygons))
bpy.ops.object.modifier_apply(modifier=mod.name)
print('FINAL',len(proxy.data.vertices),len(proxy.data.polygons))
mat=bpy.data.materials.new('H8_OcclusionLiner'); mat.diffuse_color=(.045,.055,.065,1); mat.use_nodes=True
p=mat.node_tree.nodes.get('Principled BSDF'); p.inputs['Base Color'].default_value=(.045,.055,.065,1); p.inputs['Metallic'].default_value=0; p.inputs['Roughness'].default_value=.92
proxy.data.materials.clear(); proxy.data.materials.append(mat)
for p in proxy.data.polygons:p.material_index=0
bpy.ops.wm.save_as_mainfile(filepath=str(here/'evidence/test-occlusion.blend'))
