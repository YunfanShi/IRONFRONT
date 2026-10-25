"""Non-destructive T90/V12 articulation from the accepted optimized meshes.

Blender --background --factory-startup --python build_articulated_armor.py -- t90 v12
All measurements below are in source Blender coordinates: X right, -Y nose, Z up.
Original files and textures are read-only inputs; UV corners survive the cuts.
"""
import bpy, bmesh, json, math, sys, hashlib
from pathlib import Path
from mathutils import Vector, Matrix

ROOT=Path(__file__).resolve().parent
CONFIG={
 't90':dict(folder='t90-bastion',width=5.5,source_width=.609775126,
            turret_z=.216, turret_y=.0, turret_back=.27, barrel_y=-.275,
            barrel_z=.270, barrel_x=0., wheel_centers=[]),
 'v12':dict(folder='v12-lancer',width=4.7,source_width=.790197968,
            turret_z=.351, turret_y=.045, turret_back=.27, barrel_y=-.150,
            barrel_z=.403, barrel_x=0., wheel_centers=[-.226,-.074,.112,.263]),
}

def active(o):
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active=o

def parent_keep(o,p):
    bpy.context.view_layer.update(); m=o.matrix_world.copy(); o.parent=p; o.matrix_world=m
    bpy.context.view_layer.update()

def empty(name,point,parent=None):
    o=bpy.data.objects.new(name,None); bpy.context.collection.objects.link(o)
    o.location=point; o.empty_display_size=.18; bpy.context.view_layer.update()
    if parent: parent_keep(o,parent)
    return o

def material(key,folder,red=False):
    m=bpy.data.materials.new(key+('_Red' if red else '_Blue')+'_PBR'); m.use_nodes=True
    n=m.node_tree.nodes; n.clear(); l=m.node_tree.links
    out=n.new('ShaderNodeOutputMaterial'); p=n.new('ShaderNodeBsdfPrincipled'); l.new(p.outputs['BSDF'],out.inputs['Surface'])
    for role in ('basecolor','normal','metalrough'):
        path=ROOT/folder/'textures'/f'{key}_{"red_" if red and role=="basecolor" else ""}{role}_2k.png'
        im=bpy.data.images.load(str(path),check_existing=True)
        im.colorspace_settings.name='sRGB' if role=='basecolor' else 'Non-Color'; im.pack()
        t=n.new('ShaderNodeTexImage'); t.image=im
        if role=='basecolor': l.new(t.outputs['Color'],p.inputs['Base Color'])
        elif role=='normal':
            nm=n.new('ShaderNodeNormalMap'); nm.inputs['Strength'].default_value=.65
            l.new(t.outputs['Color'],nm.inputs['Color']); l.new(nm.outputs['Normal'],p.inputs['Normal'])
        else:
            sep=n.new('ShaderNodeSeparateColor'); l.new(t.outputs['Color'],sep.inputs['Color'])
            l.new(sep.outputs['Green'],p.inputs['Roughness']); l.new(sep.outputs['Blue'],p.inputs['Metallic'])
    return m

def split_mesh(obj,cfg,paint,joint,lod):
    obj.data.transform(obj.matrix_world); obj.matrix_world=Matrix.Identity(4)
    for mod in list(obj.modifiers):
        active(obj); bpy.ops.object.modifier_apply(modifier=mod.name)
    mesh=obj.data; bm=bmesh.new(); bm.from_mesh(mesh)
    planes=[((0,0,cfg['turret_z']),(0,0,1)),((0,cfg['barrel_y'],0),(0,1,0))]
    if cfg['wheel_centers']:
        planes += [((x,0,0),(1,0,0)) for x in (-.235,.235)]
        planes += [((0,0,.218),(0,0,1))]
        planes += [((0,(a+b)/2,0),(0,1,0)) for a,b in zip(cfg['wheel_centers'],cfg['wheel_centers'][1:])]
    for p,n in planes:
        bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=1e-7,
                              plane_co=p,plane_no=n,clear_inner=False,clear_outer=False)
    # A height cut also intersects deck bolts and the rear fuel drums. Only
    # the largest connected upper assembly is the turret; those small islands
    # stay on the hull instead of becoming floating shards during traversal.
    bm.faces.ensure_lookup_table()
    candidates={f for f in bm.faces if f.calc_center_median().z>cfg['turret_z']+1e-6 and
                f.calc_center_median().y<cfg['turret_back'] and abs(f.calc_center_median().x)<.29}
    islands=[]
    while candidates:
        seed=candidates.pop(); island={seed}; todo=[seed]
        while todo:
            face=todo.pop()
            for e in face.edges:
                for near in e.link_faces:
                    if near in candidates: candidates.remove(near); island.add(near); todo.append(near)
        islands.append(island)
    turret_faces=max(islands,key=len)
    for f in bm.faces: f.material_index=1 if f in turret_faces else 0
    bm.to_mesh(mesh); bm.free(); mesh.update()
    turret_indices={f.index for f in mesh.polygons if f.material_index==1}
    labels=['Hull','TurretArmor','Cannon']
    if cfg['wheel_centers']: labels += [f'Wheel_{side}_{i}' for side in ('L','R') for i in range(4)]
    mesh.materials.clear()
    for label in labels: mesh.materials.append(bpy.data.materials.new('SEG_'+label))
    counts={k:0 for k in labels}
    for f in mesh.polygons:
        x,y,z=f.center; label='Hull'
        if f.index in turret_indices:
            label='Cannon' if y<cfg['barrel_y']-1e-6 else 'TurretArmor'
        elif cfg['wheel_centers'] and abs(x)>.235+1e-6 and z<.218-1e-6:
            idx=min(range(4),key=lambda i:abs(y-cfg['wheel_centers'][i]))
            # The skirt tips between tires stay with the chassis.
            dy=(y-cfg['wheel_centers'][idx])/(.096 if idx in (0,1) else .101)
            dz=(z-.106)/.111
            if dy*dy+dz*dz<1.12:
                label=f'Wheel_{"L" if x<0 else "R"}_{idx}'
        f.material_index=labels.index(label); counts[label]+=len(f.vertices)-2
    active(obj); bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.separate(type='MATERIAL'); bpy.ops.object.mode_set(mode='OBJECT')
    parts={}
    for part in list(bpy.context.selected_objects):
        label=part.data.materials[part.data.polygons[0].material_index].name.removeprefix('SEG_').split('.')[0]
        part.name=f'{label}_LOD{lod}'; parts[label]=part
        part.data.materials.clear(); part.data.materials.append(paint); part.data.materials.append(joint)
        for f in part.data.polygons: f.material_index=0
        # Fill only newly exposed articulation boundaries. This is physical
        # joint geometry, not an interior liner concealing decimation defects.
        bm=bmesh.new(); bm.from_mesh(part.data)
        boundary=[e for e in bm.edges if e.is_boundary]
        caps=bmesh.ops.holes_fill(bm,edges=boundary,sides=0).get('faces',[])
        for f in caps: f.material_index=1; f.smooth=False
        bmesh.ops.triangulate(bm,faces=list(bm.faces))
        bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
        bm.to_mesh(part.data); bm.free(); part.data.update()
    assert set(parts)==set(labels),(parts.keys(),counts)
    return parts,counts

def cylinder(name,radius,depth,point,mat,parent,axis='Z'):
    bpy.ops.mesh.primitive_cylinder_add(vertices=48,radius=radius,depth=depth,location=point)
    o=bpy.context.object; o.name=name
    if axis=='X': o.rotation_euler.y=math.pi/2
    o.data.materials.append(mat); parent_keep(o,parent)
    for p in o.data.polygons: p.use_smooth=len(p.vertices)==4
    return o

def descendants(root): return [root]+list(root.children_recursive)

def build(key):
    c=CONFIG[key]; folder=c['folder']; out=ROOT/folder/'articulated'; out.mkdir(exist_ok=True)
    (out/'exports').mkdir(exist_ok=True)
    source=ROOT/folder/f'{key}-optimized.blend'
    bpy.ops.wm.open_mainfile(filepath=str(source))
    bpy.context.scene.unit_settings.system='METRIC'; bpy.context.scene.unit_settings.scale_length=1
    sources=sorted([o for o in bpy.data.objects if o.type=='MESH' and 'LOD' in o.name],key=lambda o:o.name)
    assert len(sources)==3
    for o in list(bpy.data.objects):
        if o not in sources: bpy.data.objects.remove(o,do_unlink=True)
    paint=material(key,folder); red=material(key,folder,True); red.use_fake_user=True
    joint=bpy.data.materials.new('JointSteel'); joint.diffuse_color=(.055,.067,.075,1); joint.use_nodes=True
    p=joint.node_tree.nodes.get('Principled BSDF'); p.inputs['Base Color'].default_value=joint.diffuse_color
    p.inputs['Metallic'].default_value=.45; p.inputs['Roughness'].default_value=.72
    scale=c['width']/c['source_width']; roots=[]; reports=[]
    def point(x,y,z): return Vector((x*scale,y*scale,z*scale))
    for lod,source_obj in enumerate(sources):
        source_obj.hide_set(False); source_obj.hide_render=False; source_obj.hide_viewport=False
        parts,counts=split_mesh(source_obj,c,paint,joint,lod)
        root=empty(f'{key.upper()}_LOD{lod}',(0,0,0)); roots.append(root)
        turret=empty('Turret'+('.'+str(lod).zfill(3) if lod else ''),point(0,c['turret_y'],c['turret_z']),root)
        barrel=empty('Barrels'+('.'+str(lod).zfill(3) if lod else ''),point(c['barrel_x'],c['barrel_y']+.005,c['barrel_z']),turret)
        for label,part in parts.items():
            part.data.transform(Matrix.Scale(scale,4))
            pivot=Vector((0,0,0)); parent=root
            if label=='TurretArmor': pivot=turret.location.copy(); parent=turret
            if label=='Cannon': pivot=barrel.matrix_world.translation.copy(); parent=barrel
            if label.startswith('Wheel_'):
                idx=int(label[-1]); co=[v.co for v in part.data.vertices]
                pivot=point((min(v.x for v in co)+max(v.x for v in co))/(2*scale),c['wheel_centers'][idx],.106)
            part.data.transform(Matrix.Translation(-pivot)); part.location=pivot
            bpy.context.view_layer.update(); parent_keep(part,parent)
            part['bas_role']=label; part['lod_level']=lod
        # A visible, continuous bearing supports the turret when traversed.
        cylinder(f'TurretBearing_LOD{lod}',(.16 if key=='v12' else .17)*scale,.018*scale,
                 point(0,c['turret_y'],c['turret_z']-.003),joint,root)
        # Trunnion ball fills the moving elevation seam in the original mantlet.
        bpy.ops.mesh.primitive_uv_sphere_add(segments=24,ring_count=12,radius=(.031 if key=='v12' else .036)*scale,
                                           location=barrel.matrix_world.translation)
        tr=bpy.context.object; tr.name=f'GunTrunnion_LOD{lod}'; tr.data.materials.append(joint)
        for f in tr.data.polygons: f.use_smooth=True
        parent_keep(tr,barrel)
        cannon=parts['Cannon']; pts=[cannon.matrix_world@v.co for v in cannon.data.vertices]
        nose=min(v.y for v in pts); rim=[v for v in pts if v.y<nose+.012*scale]
        muzzle=empty('Muzzle'+('.'+str(lod).zfill(3) if lod else ''),
                     (sum(v.x for v in rim)/len(rim),nose-.04,sum(v.z for v in rim)/len(rim)),barrel)
        meshes=[o for o in descendants(root) if o.type=='MESH']
        pts=[o.matrix_world@v.co for o in meshes for v in o.data.vertices]
        lo=[min(p[i] for p in pts) for i in range(3)]; hi=[max(p[i] for p in pts) for i in range(3)]
        anchors={o.name:[o.matrix_world.translation.x,o.matrix_world.translation.z,-o.matrix_world.translation.y]
                 for o in descendants(root) if o.type=='EMPTY' or o.name.startswith('Wheel_')}
        report=dict(lod=lod,triangles=sum(sum(len(f.vertices)-2 for f in o.data.polygons) for o in meshes),
                    parts=counts,bounds_blender=[lo,hi],anchors_unity=anchors)
        reports.append(report)
        bpy.ops.object.select_all(action='DESELECT')
        for o in descendants(root): o.select_set(True)
        bpy.context.view_layer.objects.active=root
        bpy.ops.export_scene.fbx(filepath=str(out/'exports'/f'{key}-articulated-lod{lod}.fbx'),use_selection=True,
            apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',
            path_mode='RELATIVE',embed_textures=False,use_mesh_modifiers=True,use_custom_props=True,
            add_leaf_bones=False,bake_anim=False)
        print('ARMOR_LOD',key,json.dumps(report),flush=True)
    for lod,root in enumerate(roots):
        for o in descendants(root): o.hide_render=lod>0; o.hide_set(lod>0)
    bpy.ops.wm.save_as_mainfile(filepath=str(out/f'{key}-articulated.blend'))
    (out/'build.json').write_text(json.dumps(dict(key=key,scale=scale,blender=bpy.app.version_string,
        source=str(source),source_sha256=hashlib.sha256(source.read_bytes()).hexdigest(),lods=reports),indent=2))

args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else ['t90','v12']
for key in args: build(key)
