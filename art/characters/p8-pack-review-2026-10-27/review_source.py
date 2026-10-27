"""Read the author's two pistol reloads; render evidence without saving source.
Unity clip metadata: frames 0..51; Blender source frames 1..52 at 30 fps.
No retargeting and no changes to the game project.
"""
import bpy, json, math, hashlib
from pathlib import Path
from mathutils import Vector

R = Path(__file__).resolve().parent
source = R.parent / 'soldier-pack-trial-2026-10-27/source/Blender/HumanM_SoldierAnimationsFREE_2.0.blend'
source_hash = hashlib.sha256(source.read_bytes()).hexdigest()
bpy.ops.wm.open_mainfile(filepath=str(source), load_ui=False, use_scripts=False)
sc = bpy.context.scene
rig = bpy.data.objects['Rig']
gun = bpy.data.objects['Human_GunR']
for track in list(rig.animation_data.nla_tracks):
    rig.animation_data.nla_tracks.remove(track)
for o in bpy.data.objects:
    if o.type == 'MESH':
        o.hide_render = o.name not in ['HumanM_BodyMesh', 'Human_GunR', 'Human_SoldierHelmet']
        o.color = (.53, .59, .65, 1) if o != gun else (.43, .22, .07, 1)
sc.render.engine = 'BLENDER_WORKBENCH'
sc.render.resolution_x = 960
sc.render.resolution_y = 640
sc.render.resolution_percentage = 100
sc.display.shading.light = 'STUDIO'
sc.display.shading.color_type = 'OBJECT'
sc.display.shading.show_shadows = True
sc.display.shading.show_cavity = True
sc.display.shading.cavity_type = 'BOTH'
sc.display.shading.background_type = 'WORLD'
sc.world.color = (.075, .085, .10)
sc.render.image_settings.file_format = 'PNG'
sc.render.film_transparent = False
camera_data = bpy.data.cameras.new('Pistol source review')
camera = bpy.data.objects.new('Pistol source review', camera_data)
sc.collection.objects.link(camera)
sc.camera = camera
camera_data.type = 'ORTHO'
camera_data.clip_start = .01
camera_data.clip_end = 100

def activate(name):
    a = bpy.data.actions['HumanM@' + name]
    rig.animation_data.action = a
    rig.animation_data.action_slot = a.slots[0]
    return a

def aim(location, target, scale):
    camera.location = location
    camera.rotation_euler = (Vector(target) - camera.location).to_track_quat('-Z','Y').to_euler()
    camera_data.ortho_scale = scale

report = {'source': str(source), 'sourceSha256': source_hash, 'fps': 30,
          'firstFrame': 1, 'lastFrame': 52, 'durationSeconds': 1.7,
          'renderedObjects': [o.name for o in bpy.data.objects if o.type=='MESH' and not o.hide_render], 'clips': []}
hand_names = [f'B-{digit}{j:02}.{side}' for side in ['L','R']
              for digit in ['indexFinger','middleFinger','ringFinger','pinky','thumb'] for j in range(1,4)]
names = ['B-hand.L', 'B-hand.R', 'B-forearm.L', 'B-forearm.R', 'B-handProp.L', 'B-handProp.R'] + hand_names
for label in ['Gun_Reload01', 'Gun_Reload02']:
    action = activate(label)
    clip = {'action': action.name, 'rawActionRange': list(action.frame_range), 'frames': []}
    points = []
    for f in range(1,53):
        sc.frame_set(f)
        bpy.context.view_layer.update()
        data = {'frame': f, 'seconds': (f-1)/30,
                'joints': {n: {'head':list(rig.matrix_world @ rig.pose.bones[n].head),
                               'tail':list(rig.matrix_world @ rig.pose.bones[n].tail),
                               'matrix':[list(row) for row in rig.pose.bones[n].matrix]} for n in names},
                'gunMatrix':[list(row) for row in gun.matrix_world]}
        clip['frames'].append(data)
        points += [Vector(data['joints'][n]['head']) for n in ['B-hand.L','B-hand.R'] + hand_names]
        points += [gun.matrix_world @ Vector(c) for c in gun.bound_box]
    low = Vector(tuple(min(p[i] for p in points) for i in range(3)))
    high = Vector(tuple(max(p[i] for p in points) for i in range(3)))
    center = (low+high)*.5
    clip['handMotionBounds'] = {'min':list(low),'max':list(high)}
    # Two fixed review cameras spanning the entire action; no per-frame reframing.
    for view, offset, scale in [('front',Vector((2,-3,1.1)),1.25),
                               ('reverse',Vector((-2,-3,1.1)),1.25)]:
        target = center
        aim(target+offset, target, scale)
        out = R/'frames'/label/view
        out.mkdir(parents=True,exist_ok=True)
        for f in range(1,53):
            sc.frame_set(f)
            bpy.context.view_layer.update()
            sc.render.filepath = str(out/f'{f:04}.png')
            bpy.ops.render.render(write_still=True)
    report['clips'].append(clip)
    print('REVIEW_CLIP_RENDERED',label,flush=True)
# Source holding poses provide the actual start/end comparison.
for label in ['Gun_Aim01', 'Gun_Aim02']:
    activate(label)
    sc.frame_set(1)
    bpy.context.view_layer.update()
    p = (rig.pose.bones['B-hand.L'].head + rig.pose.bones['B-hand.R'].head)*.5
    aim(p+Vector((2,-3,1.1)), p, 1.25)
    out=R/'holds';out.mkdir(exist_ok=True)
    sc.render.filepath=str(out/(label+'.png'))
    bpy.ops.render.render(write_still=True)
assert hashlib.sha256(source.read_bytes()).hexdigest() == source_hash
(R/'source-review.json').write_text(json.dumps(report,indent=2)+'\n')
print('PISTOL_SOURCE_REVIEW_PASS: 208 sequence frames, 2 hold frames, source unchanged',flush=True)
