"""Bake source shading onto the reduced articulated mesh without changing UVs."""
import bpy,json,time
from pathlib import Path
from mathutils import Vector
R=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(R/'if27-articulated.blend'))
low=[o for o in bpy.context.scene.objects if o.type=='MESH' and o.name!='Bolt']
with bpy.data.libraries.load(str(R/'work/source-welded.blend'),link=False) as (a,b):b.objects=['IF27_High']
high=b.objects[0];bpy.context.collection.objects.link(high)
high.data=high.data.copy()
for v in high.data.vertices:v.co=Vector((v.co.x*1.2,(v.co.y-.02)*1.2,(v.co.z-.30)*1.2))
for i,m in enumerate(high.data.materials):high.data.materials[i]=m.copy()
normal=bpy.data.images.new('IF27 reduced tangent normal',width=2048,height=2048,alpha=False)
normal.generated_color=(.5,.5,1,1);normal.colorspace_settings.name='Non-Color'
dummy=bpy.data.images.new('Cap bake scratch',width=32,height=32)
seen=set()
for o in low:
    for m in o.data.materials:
        if m.name in seen:continue
        seen.add(m.name);n=m.node_tree.nodes.new('ShaderNodeTexImage');n.image=dummy if 'Interior' in m.name else normal;m.node_tree.nodes.active=n
s=bpy.context.scene;s.render.engine='CYCLES';s.cycles.device='CPU';s.cycles.samples=4
s.render.bake.use_selected_to_active=True;s.render.bake.cage_extrusion=.008;s.render.bake.max_ray_distance=.018;s.render.bake.margin=8;s.render.bake.use_clear=False
start=time.time()
for o in low:
    bpy.ops.object.select_all(action='DESELECT');high.select_set(True);o.select_set(True);bpy.context.view_layer.objects.active=o
    bpy.ops.object.bake(type='NORMAL');print('BAKED',o.name,time.time()-start,flush=True)
normal.filepath_raw=str(R/'textures/if27_normal.png');normal.file_format='PNG';normal.save()
(R/'work/bake.json').write_text(json.dumps({'source':'work/source-welded.blend','target':'if27-articulated.blend','size':2048,'parts':[o.name for o in low],'seconds':time.time()-start},indent=2))
print('IF27_BAKE_PASS',flush=True)
