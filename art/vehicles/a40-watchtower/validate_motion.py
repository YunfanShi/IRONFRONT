"""Smoke test for wheel pivots and rigid moving assemblies in the authored .blend."""
import bpy
import json
import math
import sys
from pathlib import Path
from mathutils import Vector

HERE=Path(__file__).resolve().parent
sys.path.insert(0,str(HERE))
import build_a40
bpy.ops.wm.open_mainfile(filepath=str(HERE/"A40_WATCHTOWER.blend"))
bpy.context.view_layer.update()
names=[f"Wheel_{s}_{p}" for s in "LR"
       for p in ("Front","MidFront","MidRear","Rear")]
report={}
for name in names:
    o=bpy.data.objects[name]
    bb=build_a40.bounds(o)
    center=list(o.matrix_world.translation)
    report[name]=dict(pivot_blender_xyz=center,static_bounds=bb,
        radius_m=.5*max(bb[1][1]-bb[0][1],bb[1][2]-bb[0][2]))
    assert abs(center[0])>.9 and abs(center[1])>.2 and .45<center[2]<.82, (name,center)
    assert bb[1][1]-bb[0][1]<1.8, (name,bb)
    assert bb[1][2]-bb[0][2]<1.8, (name,bb)
for name in names:
    wheel=bpy.data.objects[name]
    wheel.rotation_mode="XYZ"
    wheel.rotation_euler.x=math.radians(90)
bpy.context.view_layer.update()
for name in names:
    bb=build_a40.bounds(bpy.data.objects[name])
    report[name]["rotated90_bounds"]=bb
    assert bb[1][1]-bb[0][1]<1.8, (name,bb)
    assert bb[1][2]-bb[0][2]<1.8, (name,bb)
build_a40.RENDERS=HERE/"renders"/"wheel_rotation_90"
build_a40.RENDERS.mkdir(parents=True,exist_ok=True)
build_a40.render_views(bpy.data.objects["A40_LOD0"])
(HERE/"wheel_motion_validation.json").write_text(json.dumps(report,indent=2))
print("A40_WHEEL_MOTION_VALID",len(names))
