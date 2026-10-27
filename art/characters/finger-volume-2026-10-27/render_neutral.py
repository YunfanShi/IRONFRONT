"""Fixed neutral hand closeups; diagnostic clay copies, never saved to source."""
import bpy, json
from pathlib import Path
from mathutils import Vector, Matrix

ROOT=Path(__file__).resolve().parent
BASE=ROOT.parent/'sleeve-width-2026-10-27'
OUT=ROOT/'checks/neutral'; OUT.mkdir(parents=True,exist_ok=True)
fixed=None
for label,folder in [('before',BASE),('after',ROOT)]:
    bpy.ops.wm.open_mainfile(filepath=str(folder/'candidate/blue/blue-soldier-hand-candidate.blend'))
    obj=bpy.data.objects['BlueSoldier_LOD0_ArmR']
    rig=next(o for o in bpy.data.objects if o.type=='ARMATURE')
    rig.data.pose_position='REST';bpy.context.view_layer.update()
    adjacency=[[] for _ in obj.data.vertices]
    for edge in obj.data.edges:
        a,b=edge.vertices;adjacency[a].append(b);adjacency[b].append(a)
    ids={v.index for v in obj.data.vertices if any(g.weight>0 and obj.vertex_groups[g.group].name.startswith(('Index','Middle','Ring','Pinky','Thumb')) for g in v.groups)}
    todo=list(ids)
    while todo:
        for j in adjacency[todo.pop()]:
            if j not in ids:ids.add(j);todo.append(j)
    ids=sorted(ids);remap={v:i for i,v in enumerate(ids)}
    verts=[obj.matrix_world @ obj.data.vertices[i].co for i in ids]
    faces=[[remap[i] for i in p.vertices] for p in obj.data.polygons if all(i in remap for i in p.vertices)]
    wrist=rig.matrix_world @ rig.data.bones['Hand_R'].head_local
    tip=rig.matrix_world @ rig.data.bones['Middle3_R'].tail_local
    up=(tip-wrist).normalized()
    lateral=(rig.matrix_world @ rig.data.bones['Index1_R'].head_local)-(rig.matrix_world @ rig.data.bones['Pinky1_R'].head_local)
    lateral=(lateral-up*lateral.dot(up)).normalized();normal=lateral.cross(up).normalized()
    if fixed is None:
        lo=Vector(tuple(min(v[i] for v in verts) for i in range(3)))
        hi=Vector(tuple(max(v[i] for v in verts) for i in range(3)))
        center=(lo+hi)*.5
        extent=max(max(v.dot(axis) for v in verts)-min(v.dot(axis) for v in verts) for axis in [up,lateral,normal])
        fixed=(center,up,lateral,normal,extent*1.24)
    center,up,lateral,normal,ortho=fixed
    for other in list(bpy.data.objects):bpy.data.objects.remove(other,do_unlink=True)
    mesh=bpy.data.meshes.new('Neutral glove');mesh.from_pydata(verts,[],faces);mesh.update()
    hand=bpy.data.objects.new('Neutral glove',mesh);bpy.context.collection.objects.link(hand)
    mat=bpy.data.materials.new('Diagnostic clay');mat.diffuse_color=(.38,.44,.52,1);mat.use_nodes=True
    bs=mat.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(.38,.44,.52,1);bs.inputs['Roughness'].default_value=.78
    mesh.materials.append(mat)
    for p in mesh.polygons:p.use_smooth=True
    scene=bpy.context.scene;scene.render.engine='BLENDER_EEVEE'
    scene.render.resolution_x=600;scene.render.resolution_y=600;scene.render.resolution_percentage=100
    scene.render.film_transparent=False;scene.view_settings.view_transform='AgX'
    world=bpy.data.worlds.new('Neutral studio');world.use_nodes=True
    world.node_tree.nodes['Background'].inputs['Color'].default_value=(.07,.085,.11,1)
    world.node_tree.nodes['Background'].inputs['Strength'].default_value=.45;scene.world=world
    for i,offset in enumerate([normal*.30+lateral*.20+up*.23,-normal*.28-lateral*.15+up*.10,normal*.05-lateral*.3]):
        data=bpy.data.lights.new('Area'+str(i),'AREA');data.energy=[5,4,2.3][i];data.shape='DISK';data.size=.35
        light=bpy.data.objects.new(data.name,data);bpy.context.collection.objects.link(light);light.location=center+offset
        light.rotation_euler=(center-light.location).to_track_quat('-Z','Y').to_euler()
    data=bpy.data.cameras.new('Fixed hand camera');data.type='ORTHO';data.ortho_scale=ortho;data.clip_start=.001
    camera=bpy.data.objects.new('Fixed hand camera',data);bpy.context.collection.objects.link(camera);scene.camera=camera
    for view,z in [('front',normal),('back',-normal),('side',lateral),('three-quarter',(normal+lateral*.65).normalized())]:
        right=up.cross(z).normalized();vertical=z.cross(right).normalized()
        camera.location=center+z*.7;camera.rotation_euler=Matrix((right,vertical,z)).transposed().to_euler()
        scene.render.filepath=str(OUT/(label+'-'+view+'.png'));bpy.ops.render.render(write_still=True)
(OUT/'settings.json').write_text(json.dumps({'scope':'isolated actual glove topology in rest pose, diagnostic clay','before':'0.66.7','after':'0.66.8','fixedCameraCenter':list(fixed[0]),'orthoScale':fixed[4],'resolution':600},indent=2))
print('NEUTRAL_HAND_RENDER_PASS')
