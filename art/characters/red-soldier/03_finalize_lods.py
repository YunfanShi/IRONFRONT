"""Scale the baked soldier to game units and export static LOD meshes.

Run after 01_voxel_rebuild.py and 02_bake_pbr.py in Blender 5.2+.
"""

import bpy
import json
import time
from pathlib import Path


ROOT = Path(__file__).resolve().parent
WORK = ROOT / "work"
EXPORTS = ROOT / "exports"
EXPORTS.mkdir(parents=True, exist_ok=True)


def log(*parts):
    print("FINALIZE:", *parts, flush=True)


def select_only(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.hide_set(False)
    obj.hide_render = False
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def triangle_count(obj):
    return sum(len(poly.vertices) - 2 for poly in obj.data.polygons)


def bounds(obj):
    points = [obj.matrix_world @ v.co for v in obj.data.vertices]
    return ([min(p[i] for p in points) for i in range(3)],
            [max(p[i] for p in points) for i in range(3)])


def move_to_collection(obj, collection):
    for former in list(obj.users_collection):
        former.objects.unlink(obj)
    collection.objects.link(obj)


def make_lod(source, name, target, collection):
    obj = source.copy()
    obj.data = source.data.copy()
    collection.objects.link(obj)
    obj.name = name
    obj.data.name = name + "_Mesh"
    select_only(obj)
    decimate = obj.modifiers.new("Reduce to %d triangles" % target, "DECIMATE")
    decimate.decimate_type = "COLLAPSE"
    decimate.ratio = target / triangle_count(obj)
    decimate.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier=decimate.name)
    log(name, len(obj.data.vertices), "edit vertices", triangle_count(obj), "triangles")
    return obj


def export(obj, path, fmt):
    select_only(obj)
    if fmt == "GLB":
        bpy.ops.export_scene.gltf(filepath=str(path), export_format="GLB",
                                  use_selection=True, export_normals=True,
                                  export_tangents=True)
    else:
        bpy.ops.export_scene.fbx(filepath=str(path), use_selection=True,
                                 object_types={"MESH"}, axis_forward="-Z",
                                 axis_up="Y", path_mode="RELATIVE",
                                 embed_textures=False, bake_anim=False)
    log("exported", path.name, path.stat().st_size, "bytes")


started = time.monotonic()
bpy.ops.wm.open_mainfile(filepath=str(WORK / "voxel_baked_2k_pbr.blend"))
high = bpy.data.objects["Highpoly bake source"]
low = bpy.data.objects["Soldier voxel trial LOD0"]
bpy.data.objects.remove(high, do_unlink=True)
low.name = "RedSoldier_LOD0"
low.data.name = "RedSoldier_LOD0_Mesh"
select_only(low)

# Apply the glTF Y-up to Blender Z-up transform, then use real Unity metres.
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
before_min, before_max = bounds(low)
source_height = before_max[2] - before_min[2]
scale_factor = 1.8 / source_height
low.scale = (scale_factor,) * 3
bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
after_min, _ = bounds(low)
low.location.z = -after_min[2]
bpy.ops.object.transform_apply(location=True, rotation=False, scale=False)
final_min, final_max = bounds(low)
log("scaled", scale_factor, "height", final_max[2] - final_min[2])

lod0_collection = bpy.data.collections.new("LOD0")
lod1_collection = bpy.data.collections.new("LOD1")
lod2_collection = bpy.data.collections.new("LOD2")
for col in (lod0_collection, lod1_collection, lod2_collection):
    bpy.context.scene.collection.children.link(col)
move_to_collection(low, lod0_collection)
lod1 = make_lod(low, "RedSoldier_LOD1", 6000, lod1_collection)
lod2 = make_lod(low, "RedSoldier_LOD2", 2000, lod2_collection)

# The four baked images are external for Unity and packed in the Blender source.
texture_names = {
    "Soldier baked basecolor 2K": "red-soldier-basecolor-2k.png",
    "Soldier baked normal 2K": "red-soldier-normal-2k.png",
    "Soldier baked roughness 2K": "red-soldier-roughness-2k.png",
    "Soldier baked metallic 2K": "red-soldier-metallic-2k.png",
}
for image_name, filename in texture_names.items():
    image = bpy.data.images[image_name]
    image.filepath = str(ROOT / "textures" / filename)
    image.pack()
    image.filepath = "//textures/" + filename

for mesh in list(bpy.data.meshes):
    if mesh.users == 0:
        bpy.data.meshes.remove(mesh)
for material in list(bpy.data.materials):
    if material.users == 0:
        bpy.data.materials.remove(material)
for image in list(bpy.data.images):
    if image.users == 0:
        bpy.data.images.remove(image)

for obj in (lod1, lod2):
    obj.hide_set(True)
    obj.hide_render = True
select_only(low)
scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT / "red-soldier-technical-v01.blend"))

# Blender's FBX exporter resolves packed-image relative paths against the
# previously opened baking .blend. Give it the real external texture paths.
for image_name, filename in texture_names.items():
    bpy.data.images[image_name].filepath = str(ROOT / "textures" / filename)

lods = (low, lod1, lod2)
for obj in lods:
    suffix = obj.name.rsplit("_", 1)[-1].lower()
    export(obj, EXPORTS / ("red-soldier-%s.glb" % suffix), "GLB")
    export(obj, EXPORTS / ("red-soldier-%s.fbx" % suffix), "FBX")

report = {
    "source_file": str(ROOT / "source" / "hunyuan-red-original.glb"),
    "source_triangles": 1499976,
    "source_height_m": source_height,
    "uniform_scale": scale_factor,
    "height_m": final_max[2] - final_min[2],
    "bounds_m": {"min": final_min, "max": final_max},
    "blender_up": "+Z",
    "blender_front": "-Y",
    "fbx_up": "+Y",
    "fbx_front_target": "+Z",
    "lods": {obj.name: {
        "edit_vertices": len(obj.data.vertices),
        "triangles": triangle_count(obj),
        "uv_layers": len(obj.data.uv_layers),
        "material_slots": len(obj.data.materials),
    } for obj in lods},
    "texture_files": list(texture_names.values()),
    "bones": 0,
    "animations": 0,
    "elapsed_seconds": time.monotonic() - started,
}
(ROOT / "build-report.json").write_text(json.dumps(report, indent=2))
log("done", round(report["elapsed_seconds"], 1), "seconds")
