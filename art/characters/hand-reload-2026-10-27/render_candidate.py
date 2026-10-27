import bpy,math
from pathlib import Path
from mathutils import Vector
R=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(R/'candidate/blue/blue-soldier-hand-candidate.blend'))
rig=next(o for o in bpy.data.objects if o.type=='ARMATURE');rig.data.pose_position='POSE'
for o in list(bpy.data.objects):
 if o.type in {'LIGHT','CAMERA'}:bpy.data.objects.remove(o,do_unlink=True)
 elif o.type=='MESH':o.hide_render=o.name!='BlueSoldier_LOD0_ArmL'
s=bpy.context.scene;s.render.engine='CYCLES';s.cycles.samples=24;s.render.resolution_x=800;s.render.resolution_y=800;s.render.resolution_percentage=100
s.world=bpy.data.worlds.new('Review');s.world.color=(.18,.18,.18)
center=Vector((-.44,-.015,.9))
for name,pos,power in [('Key',(-.8,-.6,1.4),70),('Fill',(.1,.3,1.2),45)]:
 d=bpy.data.lights.new(name,'AREA');d.energy=power;d.size=.5;o=bpy.data.objects.new(name,d);s.collection.objects.link(o);o.location=pos;o.rotation_euler=(center-o.location).to_track_quat('-Z','Y').to_euler()
c=bpy.data.objects.new('Review',bpy.data.cameras.new('Review'));s.collection.objects.link(c);s.camera=c;c.data.type='ORTHO';c.data.ortho_scale=.33
for pose in ['open','grip']:
 for f in ['Index','Middle','Ring','Pinky','Thumb']:
  for j in range(1,4):
   b=rig.pose.bones[f+str(j)+'_L'];b.rotation_mode='XYZ';b.rotation_euler.x=math.radians(-([58,78,46][j-1] if f!='Thumb' else [30,42,30][j-1])) if pose=='grip' else 0
 for view,delta in [('palm',(.12,-.7,.08)),('back',(-.12,.7,.08))]:
  c.location=center+Vector(delta);c.rotation_euler=(center-c.location).to_track_quat('-Z','Y').to_euler();s.render.filepath=str(R/('hand-'+pose+'-'+view+'.png'));bpy.ops.render.render(write_still=True)
