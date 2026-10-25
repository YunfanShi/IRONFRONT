"""Preserve v01 heat weights and mesh topology; add a deforming lumbar segment.
Run in Blender background. Writes rigged-v02, never changes the accepted sources.
"""
import bpy, json
from pathlib import Path
ROOT = Path(__file__).resolve().parent
for team in ('blue', 'red'):
    folder = ROOT / (team + '-soldier')
    out = folder / 'rigged-v02'; out.mkdir(exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=str(folder/'rigged'/(team+'-soldier-rigged-v01.blend')))
    rig = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
    bpy.ops.object.select_all(action='DESELECT'); rig.hide_set(False); rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode='EDIT')
    bones = rig.data.edit_bones
    spine = bones.new('Spine'); spine.head = (0,0,.91); spine.tail = (0,0,1.18); spine.parent = bones['Pelvis']
    bones['Torso'].head = (0,0,1.18); bones['Torso'].parent = spine
    bpy.ops.object.mode_set(mode='OBJECT')
    meshes = [o for o in bpy.data.objects if o.type == 'MESH']
    for obj in meshes:
        spine_group = obj.vertex_groups.new(name='Spine')
        torso = obj.vertex_groups['Torso']
        # Redistribute only the original torso influence. Arms, helmet, boots
        # and the arm/body seam retain the accepted heat-diffusion solution.
        baked = []
        for v in obj.data.vertices:
            weights = {g.group:g.weight for g in v.groups}
            amount = weights.get(torso.index,0)
            t = max(0,min(1,(v.co.z-1.03)/.29)); t = t*t*(3-2*t)
            if amount:
                weights[torso.index] = amount*t; weights[spine_group.index] = amount*(1-t)
            top = sorted([(i,w) for i,w in weights.items() if w>1e-5], key=lambda p:-p[1])[:4]
            total = sum(w for i,w in top); baked.append([(i,w/total) for i,w in top])
        for g in obj.vertex_groups: g.remove(list(range(len(obj.data.vertices))))
        for v, weights in zip(obj.data.vertices,baked):
            for i,w in weights: obj.vertex_groups[i].add([v.index],w,'REPLACE')
    report = {'team':team,'bones':18,'source':'rigged-v01','change':'lumbar/chest weight transfer; unchanged mesh, UV and limb weights','lods':[]}
    for lod in range(3):
        bpy.ops.object.select_all(action='DESELECT'); rig.select_set(True)
        parts = [o for o in meshes if 'LOD'+str(lod) in o.name]
        for o in parts: o.hide_set(False); o.select_set(True)
        bpy.ops.export_scene.fbx(filepath=str(out/(team+'-soldier-skinned-lod'+str(lod)+'.fbx')),
            use_selection=True,object_types={'MESH','ARMATURE'},axis_forward='-Z',axis_up='Y',
            apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',add_leaf_bones=False,
            use_armature_deform_only=True,bake_anim=False,path_mode='RELATIVE')
        report['lods'].append({'lod':lod,'parts':len(parts),'triangles':sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in parts)})
    for o in meshes: o.hide_set('LOD0' not in o.name); o.hide_render='LOD0' not in o.name
    bpy.ops.wm.save_as_mainfile(filepath=str(out/(team+'-soldier-rigged-v02.blend')))
    (out/'rig-report.json').write_text(json.dumps(report,indent=2))
    print('SOLDIER_SPINE_PASS',team,flush=True)
