import bpy, json
from pathlib import Path
from mathutils import Vector

ROOT=Path(__file__).resolve().parent
def inspect(path,name):
    bpy.ops.wm.open_mainfile(filepath=str(path))
    rig=next(o for o in bpy.data.objects if o.type=='ARMATURE')
    rig.animation_data_clear()
    rig.data.pose_position='REST'
    info={'armature':rig.name,'matrix':[list(x) for x in rig.matrix_world], 'bones':{b.name:{'head':list(b.head_local),'tail':list(b.tail_local),'parent':b.parent.name if b.parent else None,'deform':b.use_deform,'matrix':[list(x) for x in b.matrix_local]} for b in rig.data.bones},'meshes':[]}
    for o in bpy.data.objects:
        if o.type=='MESH':
            info['meshes'].append({'name':o.name,'matrix':[list(x) for x in o.matrix_world],'verts':len(o.data.vertices),'materials':[m.name if m else None for m in o.data.materials],'groups':[g.name for g in o.vertex_groups]})
    (ROOT/(name+'-structure.json')).write_text(json.dumps(info,indent=2))
    return rig

rig=inspect(ROOT/'source/FPS ARMS RIG 1.blend','reference')
print('REFERENCE_BONES',json.dumps({b.name:[list(b.head_local),list(b.tail_local)] for b in rig.data.bones}))
for ob in list(bpy.data.objects):
    if ob.type in {'CAMERA','LIGHT'}:bpy.data.objects.remove(ob,do_unlink=True)
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=16
scene.render.resolution_x=900;scene.render.resolution_y=700;scene.render.resolution_percentage=100
scene.world=bpy.data.worlds.new('Inspect');scene.world.color=(.25,.25,.25)
mesh=next(o for o in bpy.data.objects if o.type=='MESH')
mat=bpy.data.materials.new('Inspection neutral');mat.diffuse_color=(.42,.46,.5,1);mat.use_nodes=True
mat.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=(.42,.46,.5,1)
mesh.data.materials.clear();mesh.data.materials.append(mat)
corners=[mesh.matrix_world@Vector(v) for v in mesh.bound_box];center=sum(corners,Vector())/8
for name,pos in [('Key',(0,-12,12)),('Fill',(10,0,8))]:
    d=bpy.data.lights.new(name,'AREA');d.energy=2200;d.size=8;o=bpy.data.objects.new(name,d);scene.collection.objects.link(o);o.location=pos;o.rotation_euler=(center-o.location).to_track_quat('-Z','Y').to_euler()
c=bpy.data.objects.new('InspectionCamera',bpy.data.cameras.new('InspectionCamera'));scene.collection.objects.link(c);scene.camera=c;c.data.type='ORTHO';c.data.ortho_scale=14
for name,delta in [('front',(0,-18,1)),('top',(0,0,18))]:
    c.location=center+Vector(delta);c.rotation_euler=(center-c.location).to_track_quat('-Z','Y').to_euler();scene.render.filepath=str(ROOT/('reference-'+name+'.png'));bpy.ops.render.render(write_still=True)
rig=inspect(ROOT.parent/'blue-soldier/rigged-v03/blue-soldier-rigged-v03.blend','original')
print('ORIGINAL_BONES',json.dumps({b.name:[list(b.head_local),list(b.tail_local)] for b in rig.data.bones}))
