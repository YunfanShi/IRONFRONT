"""Local critical-pose review; no animation keys baked into runtime FBX."""
import bpy, math, sys
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parent
for key,folder in [('t90','t90-bastion'),('v12','v12-lancer')]:
    out=ROOT/folder/'articulated'
    bpy.ops.wm.open_mainfile(filepath=str(out/f'{key}-articulated.blend'))
    scene=bpy.context.scene; scene.render.engine='CYCLES'; scene.cycles.device='CPU'; scene.cycles.samples=20
    scene.render.resolution_x=1100; scene.render.resolution_y=850; scene.render.resolution_percentage=100
    scene.world=bpy.data.worlds.new('review'); scene.world.color=(.36,.36,.36)
    for pos,power,size in [((6,-10,14),1800,8),((-7,-2,8),1100,7),((1,9,11),1700,7)]:
        d=bpy.data.lights.new('light','AREA'); d.energy=power; d.shape='DISK'; d.size=size
        o=bpy.data.objects.new('light',d); scene.collection.objects.link(o); o.location=pos
        o.rotation_euler=(Vector((0,0,1.5))-o.location).to_track_quat('-Z','Y').to_euler()
    d=bpy.data.cameras.new('review'); o=bpy.data.objects.new('review',d); scene.collection.objects.link(o); scene.camera=o
    d.type='ORTHO'; d.ortho_scale=13 if key=='t90' else 9
    turret=bpy.data.objects['Turret']; barrel=bpy.data.objects['Barrels']
    for pose,yaw,pitch,roll in [('rest',0,0,0),('aim',45,25,70),('depress',-60,-10,140)]:
        turret.rotation_euler.z=math.radians(yaw); barrel.rotation_euler.x=math.radians(-pitch)
        for w in bpy.data.objects:
            if w.name.startswith('Wheel_') and 'LOD0' in w.name:
                w.rotation_euler.x=math.radians(roll)
        for view,loc in [('front',(9,-13,8)),('rear',(-9,12,7))]:
            o.location=loc; o.rotation_euler=(Vector((0,-.3,1.6))-o.location).to_track_quat('-Z','Y').to_euler()
            scene.render.filepath=str(out/f'{pose}-{view}.png'); bpy.ops.render.render(write_still=True)
