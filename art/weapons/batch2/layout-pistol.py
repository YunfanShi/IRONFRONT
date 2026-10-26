import bpy, json
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parent
SOURCE=ROOT.parents[3]/'模型游戏优化_第二批_2026-10-26'
records={}
for key in ['04']:
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE/'Blender'/f'Asset_{key}_Optimized.blend'))
    obj=bpy.data.objects[f'Asset_{key}_LOD0']
    for o in list(bpy.data.objects):
        if o!=obj:bpy.data.objects.remove(o,do_unlink=True)
    obj.hide_render=False;obj.hide_set(False)
    lo=Vector([min(v.co[i] for v in obj.data.vertices) for i in range(3)])
    hi=Vector([max(v.co[i] for v in obj.data.vertices) for i in range(3)])
    length=hi.y-lo.y
    records[key]={'min':list(lo),'max':list(hi),'length':length}
    for v in obj.data.vertices:v.co=(v.co-Vector((0,lo.y,lo.z)))/length
    camera_data=bpy.data.cameras.new('layout');cam=bpy.data.objects.new('layout',camera_data);bpy.context.collection.objects.link(cam)
    target=Vector((0,.5,.35));cam.location=target+Vector((3,0,0));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
    camera_data.type='ORTHO';camera_data.ortho_scale=1.2
    s=bpy.context.scene;s.camera=cam;s.render.engine='BLENDER_WORKBENCH';s.render.resolution_x=1200;s.render.resolution_y=600;s.render.resolution_percentage=100
    s.display.shading.light='STUDIO';s.display.shading.color_type='MATERIAL';s.display.shading.show_shadows=True;s.display.shading.show_cavity=True
    s.world=bpy.data.worlds.new('LayoutWorld');s.display.shading.background_type='WORLD';s.world.color=(.32,.32,.32)
    s.render.image_settings.file_format='PNG';s.render.filepath=str(ROOT/f'layout-{key}.png');bpy.ops.render.render(write_still=True)
(ROOT/'pistol-bounds.json').write_text(json.dumps(records,indent=2))
