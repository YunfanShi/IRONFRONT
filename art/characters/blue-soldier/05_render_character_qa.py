"""Render reproducible character-qa-v1 images from the final Blender asset.

Run in Blender 5.2+ with:
  blender --background --factory-startup --disable-autoexec \
    --python-exit-code 1 --python 05_render_character_qa.py

The script reads the saved final .blend and does not save scene changes.
"""

import hashlib
import json
import time
from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parent
SOURCE = ROOT / "blue-soldier-technical-v01.blend"
OUTPUT = ROOT / "qa" / "character-qa-v1"
OUTPUT.mkdir(parents=True, exist_ok=True)
VERSION = "v01"
STAGE = "technical"
RESOLUTION = 1600
TARGET = Vector((0.0, 0.0, 0.94))
ORTHO_SCALE = 2.38
PERSPECTIVE_LENS_MM = 65.0
BACKGROUND_RGBA = (0.18, 0.18, 0.18, 1.0)

# Camera positions are in the final Blender asset's +Z-up/-Y-front metres.
CAMERAS = {
    "front": {"location": (0.0, -4.8, 0.94), "projection": "ORTHO"},
    "side": {"location": (-4.8, 0.0, 0.94), "projection": "ORTHO"},
    "back": {"location": (0.0, 4.8, 0.94), "projection": "ORTHO"},
    "three_quarter": {"location": (3.0, -3.0, 1.8), "projection": "PERSP"},
}

# Powers, color and size are constant for all views within each style.
LIGHTS = {
    "neutral": [
        ("QA_LIGHT_NEUTRAL_Key", (-3.4, -4.0, 4.0), 450.0, (1.0, 1.0, 1.0), 4.0),
        ("QA_LIGHT_NEUTRAL_Fill", (3.0, -2.2, 2.7), 270.0, (1.0, 1.0, 1.0), 4.0),
        ("QA_LIGHT_NEUTRAL_Rear", (0.6, 3.2, 3.2), 320.0, (1.0, 1.0, 1.0), 3.5),
    ],
    "target": [
        ("QA_LIGHT_STYLE_Key", (-3.0, -4.0, 4.0), 850.0, (0.87, 0.94, 1.0), 3.5),
        ("QA_LIGHT_STYLE_Fill", (3.2, -2.2, 2.6), 360.0, (1.0, 0.95, 0.88), 4.2),
        ("QA_LIGHT_STYLE_Rim", (0.8, 3.1, 3.5), 650.0, (0.9, 0.96, 1.0), 3.0),
    ],
}


def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def make_world(scene):
    world = bpy.data.worlds.new("QA_Background_NeutralGray")
    world.use_nodes = True
    background = world.node_tree.nodes.get("Background")
    background.inputs["Color"].default_value = BACKGROUND_RGBA
    background.inputs["Strength"].default_value = 0.8
    scene.world = world


def aim(obj, target):
    direction = target - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def create_camera(name, settings):
    data = bpy.data.cameras.new("QA_CAMERA_" + name)
    data.type = settings["projection"]
    if data.type == "ORTHO":
        data.ortho_scale = ORTHO_SCALE
    else:
        data.lens = PERSPECTIVE_LENS_MM
        data.sensor_fit = "AUTO"
    data.clip_start = 0.01
    data.clip_end = 100.0
    obj = bpy.data.objects.new(data.name, data)
    bpy.context.scene.collection.objects.link(obj)
    obj.location = settings["location"]
    aim(obj, TARGET)
    return obj


def create_lights(style):
    objects = []
    for name, location, power, color, size in LIGHTS[style]:
        data = bpy.data.lights.new(name, "AREA")
        data.energy = power
        data.color = color
        data.shape = "DISK"
        data.size = size
        obj = bpy.data.objects.new(name, data)
        bpy.context.scene.collection.objects.link(obj)
        obj.location = location
        aim(obj, TARGET)
        objects.append(obj)
    return objects


started = time.monotonic()
bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
scene = bpy.context.scene
lod0 = bpy.data.objects["BlueSoldier_LOD0"]
for obj in bpy.data.objects:
    if obj.type == "MESH":
        obj.hide_set(obj != lod0)
        obj.hide_render = obj != lod0
lod0.hide_set(False)
lod0.hide_render = False

scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = RESOLUTION
scene.render.resolution_y = RESOLUTION
scene.render.resolution_percentage = 100
scene.render.film_transparent = False
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.render.image_settings.color_depth = "8"
scene.render.threads_mode = "FIXED"
scene.render.threads = 2
scene.view_settings.view_transform = "AgX"
scene.view_settings.look = "AgX - Medium High Contrast"
scene.view_settings.exposure = 0.0
scene.view_settings.gamma = 1.0
scene.camera = None
make_world(scene)

saved_materials = list(lod0.data.materials)
neutral_material = bpy.data.materials.new("QA_Neutral_Gray_Clay")
neutral_material.use_nodes = True
principled = neutral_material.node_tree.nodes.get("Principled BSDF")
principled.inputs["Base Color"].default_value = (0.16, 0.16, 0.16, 1.0)
principled.inputs["Metallic"].default_value = 0.0
principled.inputs["Roughness"].default_value = 0.72

cameras = {name: create_camera(name, settings)
           for name, settings in CAMERAS.items()}
results = []
for style in ("neutral", "target"):
    if style == "neutral":
        lod0.data.materials.clear()
        lod0.data.materials.append(neutral_material)
    else:
        lod0.data.materials.clear()
        for material in saved_materials:
            lod0.data.materials.append(material)
    lights = create_lights(style)
    for view in ("front", "side", "back", "three_quarter"):
        scene.camera = cameras[view]
        filename = f"{VERSION}__{STAGE}__{style}__{view}.png"
        output_path = OUTPUT / filename
        scene.render.filepath = str(output_path)
        print("CHARACTER_QA render", style, view, flush=True)
        bpy.ops.render.render(write_still=True)
        results.append({
            "style": style,
            "view": view,
            "path": filename,
            "sha256": sha256(output_path),
            "bytes": output_path.stat().st_size,
        })
    for obj in lights:
        bpy.data.objects.remove(obj, do_unlink=True)

manifest = {
    "qa_preset": "character-qa-v1",
    "asset": str(SOURCE),
    "asset_sha256": sha256(SOURCE),
    "lod": "BlueSoldier_LOD0",
    "version": VERSION,
    "stage": STAGE,
    "naming": "{version}__{stage}__{style}__{view}.png",
    "resolution": [RESOLUTION, RESOLUTION],
    "alpha": "opaque",
    "background_rgba_linear": BACKGROUND_RGBA,
    "world_strength": 0.8,
    "engine": scene.render.engine,
    "blender_version": bpy.app.version_string,
    "color_management": {
        "view_transform": scene.view_settings.view_transform,
        "look": scene.view_settings.look,
        "exposure": scene.view_settings.exposure,
        "gamma": scene.view_settings.gamma,
    },
    "camera_target_m": tuple(TARGET),
    "camera_positions_m": CAMERAS,
    "orthographic_scale_m": ORTHO_SCALE,
    "perspective_lens_mm": PERSPECTIVE_LENS_MM,
    "lights": LIGHTS,
    "neutral_material": {"base_color": [0.16, 0.16, 0.16, 1.0], "metallic": 0.0, "roughness": 0.72},
    "target_material": [material.name for material in saved_materials],
    "renders": results,
    "elapsed_seconds": round(time.monotonic() - started, 2),
    "scope": "Static LOD0 external visual QA; no rig, animation or runtime check.",
}
(OUTPUT / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2))
print("CHARACTER_QA complete", manifest["elapsed_seconds"], "seconds", flush=True)
