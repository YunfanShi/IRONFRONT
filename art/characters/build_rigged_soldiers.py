"""Non-destructive, reproducible skinning of the accepted soldier LOD meshes.
Blender --background --python-exit-code 1 --python this_file.py
The gameplay rig retargets these rest-space joints; FBX contains no baked gait.
"""
import bpy, bmesh, json, math
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parent
JOINTS = {
    'Pelvis': ((0,0,.91),(0,0,1.03),None),
    'Torso': ((0,0,.91),(0,0,1.51),'Pelvis'),
    'Head': ((0,0,1.51),(0,0,1.78),'Torso'),
}
for side,s in [('L',-1),('R',1)]:
    for n,a,b,p in [
        ('UpperArm',(.235*s,0,1.43),(.335*s,0,1.20),'Torso'),
        ('Forearm',(.335*s,0,1.20),(.435*s,-.015,.90),'UpperArm_'+side),
        ('Hand',(.435*s,-.015,.90),(.45*s,-.015,.81),'Forearm_'+side),
        ('Fingers',(.45*s,-.015,.855),(.448*s,-.015,.79),'Hand_'+side),
        ('Thigh',(.17*s,0,.91),(.17*s,-.005,.53),'Pelvis'),
        ('Shin',(.17*s,-.005,.53),(.21*s,0,.14),'Thigh_'+side),
        ('Foot',(.21*s,0,.14),(.21*s,-.16,.045),'Shin_'+side),
    ]: JOINTS[n+'_'+side]=(a,b,p)

def select(objs,active=None):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.hide_set(False);o.hide_render=False;o.select_set(True)
    bpy.context.view_layer.objects.active=active or objs[0]

def split_mesh(obj,part):
    dup=obj.copy();dup.data=obj.data.copy();bpy.context.collection.objects.link(dup)
    dup.name=obj.name+'_'+part
    bm=bmesh.new();bm.from_mesh(dup.data)
    layer=bm.verts.layers.deform.active
    arm_ids={g.index for g in obj.vertex_groups if g.name.startswith(('UpperArm','Forearm','Hand','Fingers'))}
    delete=[]
    for f in bm.faces:
        score=sum(sum(w for i,w in v[layer].items() if i in arm_ids) for v in f.verts)/len(f.verts)
        center=f.calc_center_median()
        category=('ArmL' if center.x<0 else 'ArmR') if score>.5 else 'Body'
        if category!=part: delete.append(f)
    bmesh.ops.delete(bm,geom=delete,context='FACES')
    # Remove isolated vertices that are irrelevant to render bounds and budgets.
    bmesh.ops.delete(bm,geom=[v for v in bm.verts if not v.link_faces],context='VERTS')
    bm.to_mesh(dup.data);bm.free();dup.data.update()
    return dup

for team in ('blue','red'):
    folder=ROOT/(team+'-soldier'); out=folder/'rigged'; out.mkdir(exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=str(folder/(team+'-soldier-technical-v01.blend')))
    meshes=sorted([o for o in bpy.data.objects if o.type=='MESH' and 'LOD' in o.name],key=lambda o:o.name)
    for o in list(bpy.data.objects):
        if o not in meshes: bpy.data.objects.remove(o,do_unlink=True)
    bpy.context.scene.unit_settings.system='METRIC';bpy.context.scene.unit_settings.scale_length=1
    rig=bpy.data.objects.new('SoldierRig',bpy.data.armatures.new('SoldierSkeleton'))
    bpy.context.collection.objects.link(rig); select([rig]);bpy.ops.object.mode_set(mode='EDIT')
    for name,(a,b,p) in JOINTS.items():
        bone=rig.data.edit_bones.new(name);bone.head=a;bone.tail=b
        if p: bone.parent=rig.data.edit_bones[p]
    bpy.ops.object.mode_set(mode='OBJECT');rig.show_in_front=True
    report={'team':team,'height_m':1.8,'max_influences':4,'skeleton':JOINTS,'lods':[],'weighting':'Blender bone heat diffusion, rigid helmet/sole, normalized strongest four influences','facial_animation':False,'finger_control':'combined gloved fingers per hand'}
    allparts=[]
    for lod,obj in enumerate(meshes):
        select([obj]);bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
        obj.vertex_groups.clear()
        # Heat diffusion follows actual garment volume and the arm/torso gap.
        # Coordinate-only regions pull vest edges into raised arms.
        select([obj,rig],rig)
        bpy.ops.object.parent_set(type='ARMATURE_AUTO')
        if not any(m.type=='ARMATURE' for m in obj.modifiers): raise RuntimeError('No skin modifier')
        baked=[]
        for v in obj.data.vertices:
            x,y,z=v.co
            values={obj.vertex_groups[g.group].name:g.weight for g in v.groups if g.weight>.0001}
            if z>1.55: values={'Head':1.0}
            if z<.16: values={'Foot_'+('R' if x>=0 else 'L'):1.0}
            if not values: raise RuntimeError('Heat solver left unweighted vertex '+str(v.index))
            top=sorted(values.items(),key=lambda p:-p[1])[:4];total=sum(w for n,w in top)
            baked.append({n:w/total for n,w in top})
        obj.vertex_groups.clear();groups={n:obj.vertex_groups.new(name=n) for n in JOINTS}
        for v,values in zip(obj.data.vertices,baked):
            for n,w in values.items():groups[n].add([v.index],w,'REPLACE')
        obj.parent=rig
        parts=[split_mesh(obj,p) for p in ('Body','ArmL','ArmR')]
        total=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in parts)
        bpy.data.objects.remove(obj,do_unlink=True)
        for p in parts:
            if not p.data.vertices:raise RuntimeError('Empty '+p.name)
            for v in p.data.vertices:
                if not 1<=len(v.groups)<=4 or abs(sum(g.weight for g in v.groups)-1)>1e-5:raise RuntimeError('Bad skin')
        select([rig]+parts,rig)
        fbx=out/(team+'-soldier-skinned-lod'+str(lod)+'.fbx')
        bpy.ops.export_scene.fbx(filepath=str(fbx),use_selection=True,object_types={'MESH','ARMATURE'},
            axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',
            add_leaf_bones=False,use_armature_deform_only=True,bake_anim=False,path_mode='RELATIVE')
        report['lods'].append({'lod':lod,'triangles':total,'parts':[p.name for p in parts],'vertices':sum(len(p.data.vertices) for p in parts)})
        allparts+=parts
    # Editable source keeps all three LODs; only LOD0 is visible by default.
    for o in allparts:o.hide_render='LOD0' not in o.name;o.hide_set('LOD0' not in o.name)
    select([rig]);bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=str(out/(team+'-soldier-rigged-v01.blend')))
    (out/'rig-report.json').write_text(json.dumps(report,indent=2))
    print('SOLDIER_RIG_RESULT',team,json.dumps(report['lods']),flush=True)
