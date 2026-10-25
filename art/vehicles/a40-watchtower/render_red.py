"""Render the red material variant over the shared A40 LOD0 geometry."""
import bpy
import sys
from pathlib import Path
HERE=Path(__file__).resolve().parent
sys.path.insert(0,str(HERE))
import build_a40
bpy.ops.wm.open_mainfile(filepath=str(HERE/"A40_WATCHTOWER.blend"))
red=bpy.data.materials["A40_Red_PBR"]
for o in bpy.context.scene.objects:
    if o.type=="MESH" and o.get("lod_level")==0:
        o.data.materials.clear()
        o.data.materials.append(red)
build_a40.RENDERS=HERE/"renders"/"red"
build_a40.RENDERS.mkdir(parents=True,exist_ok=True)
build_a40.render_views(bpy.data.objects["A40_LOD0"])
