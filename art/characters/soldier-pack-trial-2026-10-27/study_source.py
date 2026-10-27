import bpy,json,math
from pathlib import Path
from mathutils import Vector
R=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(R/'source/Blender/HumanM_SoldierAnimationsFREE_2.0.blend'),load_ui=False,use_scripts=False)
rig=bpy.data.objects['Rig'];sc=bpy.context.scene
print('OBJECTS',[(o.name,o.type,tuple(o.dimensions)) for o in bpy.data.objects if o.type in ['MESH','CAMERA']])
for tr in list(rig.animation_data.nla_tracks):rig.animation_data.nla_tracks.remove(tr)
sc.render.engine='BLENDER_WORKBENCH';sc.render.resolution_x=640;sc.render.resolution_y=640;sc.render.resolution_percentage=100
sc.display.shading.light='STUDIO';sc.display.shading.color_type='MATERIAL';sc.display.shading.show_shadows=True;sc.display.shading.show_cavity=True
sc.display.shading.background_type='WORLD';sc.world.color=(.12,.12,.12)
sc.render.image_settings.file_format='PNG';sc.render.film_transparent=False
camdata=bpy.data.cameras.new('Study camera');cam=bpy.data.objects.new('Study camera',camdata);sc.collection.objects.link(cam);sc.camera=cam
cam.location=(2,-3,2.15);cam.rotation_euler=(Vector((0,0,1.25))-cam.location).to_track_quat('-Z','Y').to_euler();camdata.type='ORTHO';camdata.ortho_scale=1.8
out=R/'source-review';out.mkdir(exist_ok=True)
report=[]
for label in ['AssaultRifle_Reload01','AssaultRifle_Aim01','Gun_Aim01','Gun_Aim02']:

 for o in bpy.data.objects:
  if o.type=='MESH' and o.name.startswith('Human_') and o.name!='Human_SoldierHelmet':
   o.hide_render=o.name!=('Human_AssaultRifle' if 'Assault' in label else 'Human_GunR')
 a=bpy.data.actions['HumanM@'+label];rig.animation_data.action=a;rig.animation_data.action_slot=a.slots[0]
 print('ACTION',label,list(a.frame_range),a.use_frame_range,a.frame_start,a.frame_end,[dict(name=m.name,frame=m.frame) for m in a.pose_markers])
 data=[]
 for f in ([1,8,14,20,26,32,38,44,52] if 'Reload' in label else [1]):
  sc.frame_set(f);bpy.context.view_layer.update()
  pts={n:list(rig.pose.bones[n].head) for n in ['B-upperArm.L','B-forearm.L','B-hand.L','B-upperArm.R','B-forearm.R','B-hand.R','B-handProp.R','B-handProp.L']}
  data.append({'frame':f,'p':pts,'gun':{'matrix':[list(row) for row in bpy.data.objects['Human_AssaultRifle' if 'Assault' in label else 'Human_GunR'].matrix_world], 'prop':[list(row) for row in rig.pose.bones['B-handProp.R'].matrix]}})
  sc.render.filepath=str(out/(label+'-'+str(f)+'.png'));bpy.ops.render.render(write_still=True)
 report.append({'action':label,'points':data})
(R/'source-study.json').write_text(json.dumps(report,indent=2)+'\n')
