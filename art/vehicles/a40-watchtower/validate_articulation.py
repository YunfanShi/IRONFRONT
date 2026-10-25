"""Render and validate a nonzero turret, barrel, and radar pose from the saved .blend."""
import bpy
import json
import math
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import build_a40

bpy.ops.wm.open_mainfile(filepath=str(HERE / "A40_WATCHTOWER.blend"))
root = bpy.data.objects["A40_LOD0"]
turret = bpy.data.objects["Turret"]
barrels = bpy.data.objects["Barrels"]
radar = bpy.data.objects["Radar"]
assert turret.parent == root
assert barrels.parent == turret
assert radar.parent == turret
assert bpy.data.objects["Muzzle_LeftLower"].parent == barrels
assert len([o for o in barrels.children if o.type == "MESH"]) == 4
assert len([o for o in radar.children if o.type == "MESH"]) == 1

baseline = {
    o.name: list(o.matrix_world.translation)
    for o in (turret, barrels, radar, bpy.data.objects["Muzzle_LeftLower"])
}
turret.rotation_mode = "XYZ"
barrels.rotation_mode = "XYZ"
radar.rotation_mode = "XYZ"
turret.rotation_euler.z = math.radians(42)
barrels.rotation_euler.x = math.radians(12)
radar.rotation_euler.z = math.radians(70)
bpy.context.view_layer.update()
posed = {
    o.name: list(o.matrix_world.translation)
    for o in (turret, barrels, radar, bpy.data.objects["Muzzle_LeftLower"])
}
assert math.dist(baseline["Muzzle_LeftLower"], posed["Muzzle_LeftLower"]) > 1.0
assert math.dist(baseline["Radar"], posed["Radar"]) > .5
assert math.dist(baseline["Turret"], posed["Turret"]) < 1e-5
assert math.dist(baseline["Barrels"], posed["Barrels"]) < 1e-5

build_a40.RENDERS = HERE / "renders" / "articulation_pose"
build_a40.RENDERS.mkdir(parents=True, exist_ok=True)
build_a40.render_views(root)
(HERE / "articulation_validation.json").write_text(json.dumps({
    "angles_deg": {"turret_yaw": 42, "barrel_elevation": 12, "radar_yaw": 70},
    "baseline_blender_xyz": baseline,
    "posed_blender_xyz": posed,
    "hierarchy_pass": True,
}, indent=2))
print("A40_ARTICULATION_VALID")
