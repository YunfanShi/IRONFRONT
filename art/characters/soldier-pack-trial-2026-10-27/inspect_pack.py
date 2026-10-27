"""Inspect downloaded animation data in an isolated Blender process.
Usage: blender -b --disable-autoexec --python inspect_pack.py -- input.blend output.json
No changes are saved to the source file or the Unity project.
"""
import bpy, json, sys
from pathlib import Path
args=sys.argv[sys.argv.index("--")+1:]
src=Path(args[0]).resolve();out=Path(args[1]).resolve()
if src.suffix.lower()==".blend":
    bpy.ops.wm.open_mainfile(filepath=str(src),load_ui=False,use_scripts=False)
elif src.suffix.lower()==".fbx":
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(src))
else:
    raise ValueError("Expected .blend or .fbx data")
report={"file":str(src),"fps":bpy.context.scene.render.fps/bpy.context.scene.render.fps_base,
        "rigs":[],"actions":[]}
for obj in bpy.data.objects:
    if obj.type!="ARMATURE":continue
    report["rigs"].append({"name":obj.name,"world": [list(row) for row in obj.matrix_world],
        "bones":[{"name":b.name,"parent":b.parent.name if b.parent else None,
        "head":list(b.head_local),"tail":list(b.tail_local),
        "restMatrix":[list(row) for row in b.matrix_local]} for b in obj.data.bones]})
for a in bpy.data.actions:
    report["actions"].append({"name":a.name,"range":list(a.frame_range),
        "slots":[{"identifier":s.identifier,"target":s.target_id_type} for s in a.slots]})
assert report["rigs"],"No skeleton found"
assert report["actions"],"No animation actions found"
out.parent.mkdir(parents=True,exist_ok=True);out.write_text(json.dumps(report,indent=2)+"\n")
print("PACK_INSPECTION_PASS",len(report["rigs"]),"rigs",len(report["actions"]),"actions")
