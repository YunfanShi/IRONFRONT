"""Render H8 against a magenta ground to reveal small through-holes."""
import bpy
from mathutils import Vector
from pathlib import Path

here=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(here/'evidence/test-occlusion.blend'))
scene=bpy.context.scene
scene.render.engine='BLENDER_WORKBENCH'
scene.display.shading.light='STUDIO'
scene.display.shading.color_type='OBJECT'
scene.display.shading.show_shadows=False
scene.display.shading.show_cavity=False
scene.display.shading.background_type='WORLD'
scene.world.color=(.85,.05,.75)
scene.render.resolution_x=1280; scene.render.resolution_y=960; scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'
for o in scene.objects:
    if o.type=='MESH':
        o.color=(.27,.36,.47,1)
        if 'Rotor' in o.name:o.color=(.1,.12,.14,1)
liner=bpy.data.objects['BodyOcclusionLiner']; liner.color=(.055,.075,.09,1)
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.25))
plane=bpy.context.object; plane.name='MAGENTA_GROUND'; plane.color=(.9,.02,.6,1)
bpy.ops.object.camera_add(location=(11,-14,12))
camera=bpy.context.object
target=Vector((0,-.6,3.1));camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler()
camera.data.type='ORTHO'; camera.data.ortho_scale=22
scene.camera=camera
for name,hide in [('before',True),('after',False)]:
    liner.hide_render=hide
    scene.render.filepath=str(here/f'evidence/hole-test-{name}.png')
    bpy.ops.render.render(write_still=True)
