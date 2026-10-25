import bpy, math
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parent
for key,folder in [('t90','t90-bastion'),('v12','v12-lancer')]:
    bpy.ops.wm.open_mainfile(filepath=str(ROOT/folder/f'{key}-optimized.blend'))
    for o in list(bpy.data.objects):
        if o.type!='MESH': bpy.data.objects.remove(o,do_unlink=True)
        else: o.hide_render='LOD0' not in o.name
    scene=bpy.context.scene
    scene.render.engine='CYCLES'; scene.cycles.device='CPU'; scene.cycles.samples=12
    scene.render.resolution_x=1000; scene.render.resolution_y=800; scene.render.resolution_percentage=100
    scene.world=bpy.data.worlds.new('MeasureWorld'); scene.world.color=(.5,.5,.5)
    for pos,power,size in [((1,-2,3),180,3),((-2,1,2),120,3)]:
        d=bpy.data.lights.new('light','AREA'); d.energy=power; d.shape='DISK'; d.size=size
        o=bpy.data.objects.new('light',d); scene.collection.objects.link(o); o.location=pos
        o.rotation_euler=(Vector((0,0,.2))-o.location).to_track_quat('-Z','Y').to_euler()
    d=bpy.data.cameras.new('measure'); o=bpy.data.objects.new('measure',d); scene.collection.objects.link(o)
    scene.camera=o; d.type='ORTHO'; d.ortho_scale=1.45
    for name,loc,target in [('side',(3,0,.3),(0,0,.3)),('top',(0,0,3),(0,0,0)),('front',(0,-3,.3),(0,0,.3))]:
        o.location=loc; o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler()
        scene.render.filepath=str(ROOT/folder/'articulated'/f'source-{name}.png')
        bpy.ops.render.render(write_still=True)
