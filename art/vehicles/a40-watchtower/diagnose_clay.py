import bpy
import sys
from pathlib import Path

HERE=Path(__file__).resolve().parent
sys.path.insert(0,str(HERE))
import build_a40
bpy.ops.wm.open_mainfile(filepath=str(HERE/"A40_WATCHTOWER.blend"))
mat=bpy.data.materials.new("Diagnostic_Clay")
mat.use_nodes=True
bsdf=mat.node_tree.nodes.get("Principled BSDF")
bsdf.inputs["Base Color"].default_value=(.55,.64,.72,1)
bsdf.inputs["Metallic"].default_value=0
bsdf.inputs["Roughness"].default_value=.8
for o in bpy.context.scene.objects:
    if o.type=="MESH" and o.get("lod_level")==0:
        o.data.materials.clear()
        o.data.materials.append(mat)
build_a40.RENDERS=Path("/private/tmp/a40-clay")
build_a40.RENDERS.mkdir(parents=True,exist_ok=True)
build_a40.render_views(bpy.data.objects["A40_LOD0"])
