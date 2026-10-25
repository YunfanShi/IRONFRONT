"""Local critical-pose review; no animation keys baked into runtime FBX."""
import bpy, math, sys
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parent
for key,folder in [('r4','r4-scout'),('u8','u8-rover'),('m2','m2-courier'),('r3','r3-firefly'),('j20','j20-stratus')]:
    out=ROOT/folder/'articulated'
    bpy.ops.wm.open_mainfile(filepath=str(out/f'{key}-articulated.blend'))
    scene=bpy.context.scene; scene.render.engine='CYCLES'; scene.cycles.device='CPU'; scene.cycles.samples=16
    scene.render.resolution_x=1000; scene.render.resolution_y=800; scene.render.resolution_percentage=100
    scene.world=bpy.data.worlds.new('review'); scene.world.color=(.45,.45,.45)
    points=[o.matrix_world@Vector(c) for o in scene.objects if o.type=='MESH' and not o.hide_render for c in o.bound_box]
    low=Vector([min(p[i] for p in points) for i in range(3)]);high=Vector([max(p[i] for p in points) for i in range(3)])
    target=(low+high)/2;size=max(high-low);scale=size/7
    for pos,power,area in [((6,-10,14),2100,8),((-7,-2,8),1400,7),((1,9,11),1700,7)]:
        d=bpy.data.lights.new('light','AREA'); d.energy=power*scale*scale; d.size=area*scale
        o=bpy.data.objects.new('light',d);scene.collection.objects.link(o);o.location=Vector(pos)*scale
        o.rotation_euler=(target-o.location).to_track_quat('-Z','Y').to_euler()
    d=bpy.data.cameras.new('review');o=bpy.data.objects.new('review',d);scene.collection.objects.link(o);scene.camera=o;d.type='ORTHO';d.ortho_scale=size*1.4
    for pose in ['rest','posed']:
        if pose=='posed':
            if key in ('r4','u8'):
                bpy.data.objects['Turret'].rotation_euler.z=math.radians(40);bpy.data.objects['Barrels'].rotation_euler.x=math.radians(-25)
            if key=='m2':bpy.data.objects['Steering'].rotation_euler.z=math.radians(-23)
            if key=='r3':
                bpy.data.objects['MainRotor'].rotation_euler.z=math.radians(60);bpy.data.objects['TailRotor'].rotation_euler.x=math.radians(60);bpy.data.objects['Radar'].rotation_euler.z=math.radians(45)
            for wheel in bpy.data.objects:
                if wheel.name.startswith('Wheel_') and 'LOD0' in wheel.name:wheel.rotation_euler.x=math.radians(80)
        o.location=target+Vector((1.2,-1.6,1.0))*size;o.rotation_euler=(target-o.location).to_track_quat('-Z','Y').to_euler()
        scene.render.filepath=str(out/f'{pose}-front.png');bpy.ops.render.render(write_still=True)
