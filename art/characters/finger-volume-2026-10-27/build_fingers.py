"""Thicken the existing glove digits without scaling the skeleton or palm.

Uses 0.66.7 as the immutable source. The displacement is transverse to each
finger's overall axis and fades into the palm through existing skin weights.
"""
import bpy, json, shutil
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parent
BASE = ROOT.parent / 'sleeve-width-2026-10-27'
FINGERS = ['Index', 'Middle', 'Ring', 'Pinky', 'Thumb']
GROWTH = .10
reports = []

for team in ['blue', 'red']:
    source = BASE / 'candidate' / team
    out = ROOT / 'candidate' / team
    out.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=str(source / (team + '-soldier-hand-candidate.blend')))
    rig = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
    rig.data.pose_position = 'REST'
    parts = [o for o in bpy.data.objects if o.type == 'MESH' and 'LOD' in o.name]
    for img in bpy.data.images:
        path = Path(bpy.path.abspath(img.filepath))
        if path.is_file():
            target = out / 'textures' / path.name
            target.parent.mkdir(exist_ok=True)
            shutil.copyfile(path, target)
            img.filepath = str(target)
    mesh_reports = []
    for obj in parts:
        if not obj.name.endswith(('_ArmL', '_ArmR')):
            continue
        side = 'L' if obj.name.endswith('_ArmL') else 'R'
        to_mesh = obj.matrix_world.inverted() @ rig.matrix_world
        segments = {}
        finger_axes = {}
        for finger in FINGERS:
            first = rig.data.bones[finger + '1_' + side]
            last = rig.data.bones[finger + '3_' + side]
            finger_axes[finger] = ((to_mesh @ last.tail_local) - (to_mesh @ first.head_local)).normalized()
            for j in range(1, 4):
                name = finger + str(j) + '_' + side
                bone = rig.data.bones[name]
                head, tail = to_mesh @ bone.head_local, to_mesh @ bone.tail_local
                segments[name] = (finger, head, (tail-head).normalized())
        changed = 0
        max_offset = 0.0
        counts = {f: 0 for f in FINGERS}
        for v in obj.data.vertices:
            per_finger = {f: Vector((0,0,0)) for f in FINGERS}
            influenced = set()
            for weight in v.groups:
                name = obj.vertex_groups[weight.group].name
                if name not in segments or weight.weight <= 0:
                    continue
                finger, head, axis = segments[name]
                delta = v.co - head
                radial = delta - axis * delta.dot(axis)
                per_finger[finger] += radial * weight.weight
                influenced.add(finger)
            if not influenced:
                continue
            displacement = Vector((0,0,0))
            for finger in influenced:
                radial = per_finger[finger]
                axis = finger_axes[finger]
                # Preserve the longitudinal coordinate: do not lengthen fingers.
                displacement += GROWTH * (radial-axis*radial.dot(axis))
                counts[finger] += 1
            max_offset = max(max_offset, displacement.length)
            v.co += displacement
            changed += 1
        obj.data.update()
        mesh_reports.append({'mesh': obj.name, 'changedDigitVertices': changed,
                             'maxOffsetMeters': max_offset, 'verticesByFinger': counts})
    for lod in range(3):
        bpy.ops.object.select_all(action='DESELECT')
        rig.hide_set(False); rig.select_set(True)
        for obj in parts:
            if 'LOD'+str(lod) in obj.name:
                obj.hide_set(False); obj.select_set(True)
        bpy.context.view_layer.objects.active = rig
        bpy.ops.export_scene.fbx(filepath=str(out / (team + '-soldier-skinned-lod' + str(lod) + '.fbx')),
            use_selection=True, object_types={'MESH','ARMATURE'}, axis_forward='-Z', axis_up='Y',
            apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', add_leaf_bones=False,
            use_armature_deform_only=True, bake_anim=False, path_mode='RELATIVE')
    for obj in parts:
        obj.hide_render = 'LOD0' not in obj.name
        obj.hide_set('LOD0' not in obj.name)
    rig.data.pose_position = 'POSE'
    bpy.ops.wm.save_as_mainfile(filepath=str(out / (team + '-soldier-hand-candidate.blend')))
    reports.append({'team': team, 'targetTransverseGrowth': GROWTH, 'meshes': mesh_reports})
(ROOT / 'candidate-report.json').write_text(json.dumps(reports, indent=2)+'\n')
print('FINGER_VOLUME_CREATED')
