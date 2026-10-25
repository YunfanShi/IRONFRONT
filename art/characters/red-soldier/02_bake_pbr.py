import bpy
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parent / 'work'
TEXTURES = ROOT.parent / 'textures'
TEXTURES.mkdir(parents=True, exist_ok=True)
SIZE = 2048

def log(*values):
    print('BAKE_TRIAL:', *values, flush=True)

t0 = time.monotonic()
bpy.ops.wm.open_mainfile(filepath=str(ROOT / 'voxel_trial.blend'))
hi = bpy.data.objects['Highpoly bake source']
lo = bpy.data.objects['Soldier voxel trial LOD0']
hi.hide_set(False)
hi.hide_render = False
lo.hide_set(False)

bpy.ops.object.select_all(action='DESELECT')
lo.select_set(True)
bpy.context.view_layer.objects.active = lo
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(island_margin=0.008)
bpy.ops.object.mode_set(mode='OBJECT')
log('UV smart project', len(lo.data.uv_layers), 'seconds', round(time.monotonic()-t0, 1))

mat = lo.data.materials[0]
tree = mat.node_tree
nodes = tree.nodes
bsdf = nodes.get('Principled BSDF')
base = bpy.data.images.new('Soldier baked basecolor 2K', width=SIZE, height=SIZE, alpha=False)
base.filepath_raw = str(TEXTURES / 'red-soldier-basecolor-2k.png')
base.file_format = 'PNG'
tex = nodes.new('ShaderNodeTexImage')
tex.name = 'Baked basecolor'
tex.image = base
tex.interpolation = 'Linear'
tree.links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])
nodes.active = tex

scene = bpy.context.scene
scene.render.engine = 'CYCLES'
scene.cycles.samples = 1
scene.render.bake.use_selected_to_active = True
scene.render.bake.cage_extrusion = 0.01
scene.render.bake.max_ray_distance = 0.02
scene.render.bake.margin = 8
scene.render.bake.use_clear = True
scene.render.bake.use_pass_color = True
scene.render.bake.use_pass_direct = False
scene.render.bake.use_pass_indirect = False
scene.view_settings.view_transform = 'Standard'

bpy.ops.object.select_all(action='DESELECT')
hi.select_set(True)
lo.select_set(True)
bpy.context.view_layer.objects.active = lo

t = time.monotonic()
bpy.ops.object.bake(type='DIFFUSE')
base.save()
log('basecolor bake', round(time.monotonic()-t,1), 'seconds')

normal = bpy.data.images.new('Soldier baked normal 2K', width=SIZE, height=SIZE, alpha=False)
normal.filepath_raw = str(TEXTURES / 'red-soldier-normal-2k.png')
normal.file_format = 'PNG'
normal.colorspace_settings.name = 'Non-Color'
normal_tex = nodes.new('ShaderNodeTexImage')
normal_tex.name = 'Baked tangent normal'
normal_tex.image = normal
normal_tex.interpolation = 'Linear'
nodes.active = normal_tex

t = time.monotonic()
bpy.ops.object.bake(type='NORMAL')
normal.save()
log('normal bake', round(time.monotonic()-t,1), 'seconds')

nm = nodes.new('ShaderNodeNormalMap')
tree.links.new(normal_tex.outputs['Color'], nm.inputs['Color'])
tree.links.new(nm.outputs['Normal'], bsdf.inputs['Normal'])

rough = bpy.data.images.new('Soldier baked roughness 2K', width=SIZE, height=SIZE, alpha=False)
rough.filepath_raw = str(TEXTURES / 'red-soldier-roughness-2k.png')
rough.file_format = 'PNG'
rough.colorspace_settings.name = 'Non-Color'
rough_tex = nodes.new('ShaderNodeTexImage')
rough_tex.name = 'Baked roughness'
rough_tex.image = rough
nodes.active = rough_tex
t = time.monotonic()
bpy.ops.object.bake(type='ROUGHNESS')
rough.save()
tree.links.new(rough_tex.outputs['Color'], bsdf.inputs['Roughness'])
log('roughness bake', round(time.monotonic()-t, 1), 'seconds')

source_tree = hi.data.materials[0].node_tree
source_bsdf = source_tree.nodes.get('Principled BSDF')
source_output = source_tree.nodes.get('Material Output')
source_metallic = source_bsdf.inputs['Metallic'].links[0].from_socket
source_emission = source_tree.nodes.new('ShaderNodeEmission')
source_tree.links.new(source_metallic, source_emission.inputs['Color'])
source_tree.links.new(source_emission.outputs['Emission'], source_output.inputs['Surface'])

metal = bpy.data.images.new('Soldier baked metallic 2K', width=SIZE, height=SIZE, alpha=False)
metal.filepath_raw = str(TEXTURES / 'red-soldier-metallic-2k.png')
metal.file_format = 'PNG'
metal.colorspace_settings.name = 'Non-Color'
metal_tex = nodes.new('ShaderNodeTexImage')
metal_tex.name = 'Baked metallic'
metal_tex.image = metal
nodes.active = metal_tex
t = time.monotonic()
bpy.ops.object.bake(type='EMIT')
metal.save()
source_tree.links.new(source_bsdf.outputs['BSDF'], source_output.inputs['Surface'])
source_tree.nodes.remove(source_emission)
tree.links.new(metal_tex.outputs['Color'], bsdf.inputs['Metallic'])
log('metallic bake', round(time.monotonic()-t, 1), 'seconds')

hi.hide_render = True
hi.hide_set(True)
bpy.ops.object.select_all(action='DESELECT')
lo.select_set(True)
bpy.context.view_layer.objects.active = lo
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT / 'voxel_baked_2k_pbr.blend'))
bpy.ops.export_scene.gltf(filepath=str(ROOT / 'voxel_baked_2k_pbr.glb'), export_format='GLB', use_selection=True)
log('done', round(time.monotonic()-t0, 1), 'seconds')
