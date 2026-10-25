"""Build a game-oriented H8 from the supplied Hunyuan high-poly GLB.

The script deliberately keeps the imported body rather than replacing the
recognizable aircraft with a primitive blockout. It separates selected loose
surface components by their measured source-space bounds, discards the six
generated blade meshes, and installs lightweight four-blade rotors.

Run from a clean Blender process:
  Blender --background --factory-startup --python build_h8.py
"""
from array import array
from collections import Counter
from math import cos, pi, sin
from pathlib import Path
import json

import bpy
import bmesh
import numpy as np
from mathutils import Vector

HERE = Path(__file__).resolve().parent
SOURCE = Path('/Users/liuwenjun/Downloads/33717aab9aa8b9b3fb2bb108d94498cb.glb')
TEXTURES = HERE / 'textures'
TEXTURES.mkdir(exist_ok=True)
REPORT = HERE / 'build_metrics.json'

# Source-space coordinates use X=right, Y=up, Z=forward. The source import
# rotates them to Blender X=right, Z=up, -Y=forward. Exporters then map to
# Unity X=right, Y=up, Z=forward.
ROTOR_DIAMETER_M = 13.4
SOURCE_ROTOR_DIAMETER = 0.795222
S = ROTOR_DIAMETER_M / SOURCE_ROTOR_DIAMETER
MAIN_HUB_SRC = (0.0, 0.414, 0.015)
TAIL_HUB_SRC = (0.028, 0.409, -0.568)
CHIN_PIVOT_SRC = (0.0, 0.085, 0.383)
POD_PORT_SRC = (-0.30, 0.125, 0.025)
POD_STARBOARD_SRC = (0.30, 0.125, 0.025)

# LOD budgets are per semantic source section. Added new rotor blades are
# intentionally cheap and stay the same across LODs.
LOD0_BUDGET = {'Body': 43000, 'RotorHub': 3200, 'ChinGun': 1300,
               'PortWeaponPod': 1600, 'StarboardWeaponPod': 1600}
LOD_REDUCE = [1.0, 0.50, 0.40]

def src_to_world(co):
    x, up, forward = co
    return Vector((x * S, -forward * S, up * S))

def active(o):
    bpy.ops.object.select_all(action='DESELECT')
    o.select_set(True)
    bpy.context.view_layer.objects.active = o

def count_triangles(o):
    if o.type != 'MESH':
        return 0
    return sum(max(0, len(p.vertices) - 2) for p in o.data.polygons)

def decimate(o, target):
    before = count_triangles(o)
    if before <= target or before < 8:
        return before
    mod = o.modifiers.new('GamePolyReduction', 'DECIMATE')
    mod.decimate_type = 'COLLAPSE'
    mod.ratio = max(0.001, min(1.0, target / before))
    mod.use_collapse_triangulate = True
    active(o)
    bpy.ops.object.modifier_apply(modifier=mod.name)
    return count_triangles(o)

def weld_atlas_seams(o):
    # glTF splits shared vertices at UV/normal seams. COLLAPSE must see the
    # continuous surface; BMesh retains the per-corner UV atlas when welding.
    bm = bmesh.new()
    bm.from_mesh(o.data)
    before = sum(e.is_boundary for e in bm.edges)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=0.00001)
    after = sum(e.is_boundary for e in bm.edges)
    bm.to_mesh(o.data); bm.free(); o.data.update()
    print('WELDED_SEAMS', o.name, before, after, flush=True)

def partition_components(mesh):
    n = len(mesh.vertices)
    parent = array('I', range(n))
    rank = bytearray(n)
    def find(x):
        while parent[x] != x:
            parent[x] = parent[parent[x]]
            x = parent[x]
        return x
    def union(a, b):
        a, b = find(a), find(b)
        if a == b:
            return
        if rank[a] < rank[b]:
            a, b = b, a
        parent[b] = a
        if rank[a] == rank[b]:
            rank[a] += 1
    for poly in mesh.polygons:
        v = poly.vertices
        union(v[0], v[1]); union(v[0], v[2])
    stats = {}
    for v in mesh.vertices:
        root = find(v.index)
        co = v.co
        if root not in stats:
            stats[root] = [0, 0, [co.x, co.y, co.z], [co.x, co.y, co.z], [0., 0., 0.]]
        s = stats[root]
        s[0] += 1
        for i in range(3):
            s[2][i] = min(s[2][i], co[i]); s[3][i] = max(s[3][i], co[i]); s[4][i] += co[i]
    for poly in mesh.polygons:
        stats[find(poly.vertices[0])][1] += 1
    return find, stats

def classify(s):
    verts, tris, vmin, vmax, sm = s
    x, y, z = (sm[i] / verts for i in range(3))
    dx, dy, dz = (vmax[i]-vmin[i] for i in range(3))
    # Small UV islands can be real surface patches; retain them by location.
    # Six high-poly rotor blades are replaced with four game blades. Their
    # source islands are thin, high and radially extended.
    if y > .38 and z > -.43 and (x*x + z*z) ** .5 > .125:
        return 'Discard'
    if y > .38 and z > -.43 and max(dx, dz) > .10:
        return 'Discard'
    # Tiny existing tail rotor surface pieces would overlap the new four blades.
    if z < -.48 and y > .38 and abs(x) < .05:
        return 'Discard'
    if z > .40 and y < .095 and abs(x) < .045:
        return 'ChinGun'
    if -.2 < z < .22 and y < .19 and x < -.25:
        return 'PortWeaponPod'
    if -.2 < z < .22 and y < .19 and x > .25:
        return 'StarboardWeaponPod'
    if y > .385 and z > -.35 and z < .35 and abs(x) < .125 and vmin[1] > .35:
        return 'RotorHub'
    return 'Body'

def save_image_2k(im, name, *, variant=None):
    color_space = im.colorspace_settings.name
    im.scale(2048, 2048)
    if variant:
        px = np.empty(2048*2048*4, dtype=np.float32)
        im.pixels.foreach_get(px)
        rgba = px.reshape((-1,4))
        rgb = rgba[:,:3].copy()
        if variant == 'blue':
            lum = rgb @ np.array([.2126,.7152,.0722], dtype=np.float32)
            # The source's sunlit armor is almost white. Darken broad armor
            # plates while leaving the dark cockpit, gunmetal and recesses
            # separated, close to the project reference's steel-blue value.
            factor = np.array([.38,.43,.50], dtype=np.float32)
            darker = rgb * factor
            keep_dark = rgb * np.array([.69,.74,.79], dtype=np.float32)
            mix = np.clip((lum-.15)/.18,0,1)[:,None]
            rgba[:,:3] = np.clip(keep_dark*(1-mix)+darker*mix, 0, 1)
        elif variant == 'red':
            # Remap cool armor to muted brick red, keep dark glass/steel dark.
            lum = rgb @ np.array([.2126,.7152,.0722], dtype=np.float32)
            cool = (rgb[:,2] > rgb[:,0]*1.025) & (lum > .16)
            warm = rgb * np.array([.57,.55,.52], dtype=np.float32)
            warm[cool,0] = np.clip(lum[cool]*.54, 0, 1)
            warm[cool,1] = np.clip(lum[cool]*.32, 0, 1)
            warm[cool,2] = np.clip(lum[cool]*.29, 0, 1)
            rgba[:,:3] = np.clip(warm, 0, 1)
        im.pixels.foreach_set(px)
        im.update()
    dest = TEXTURES/name
    im.filepath_raw = str(dest)
    im.file_format = 'PNG'
    im.save()
    # Image.copy() inherits the high-poly GLB's 4K packed file. Blender's
    # exporter may prefer that stale packed payload over the new 2K pixels.
    # Reload from disk, then repack the actual 2K file for the .blend/GLB.
    bpy.data.images.remove(im)
    fresh = bpy.data.images.load(str(dest), check_existing=False)
    fresh.name = name.rsplit('.', 1)[0]
    fresh.colorspace_settings.name = color_space
    fresh.pack()
    return fresh

def make_textures_and_material(source_mat):
    tex_nodes = [n for n in source_mat.node_tree.nodes if n.type == 'TEX_IMAGE' and n.image]
    base_node = next(n for n in tex_nodes if 'normal' not in n.image.name.lower() and 'metallic' not in n.image.name.lower())
    orm_node = next(n for n in tex_nodes if 'metallic' in n.image.name.lower())
    normal_node = next(n for n in tex_nodes if 'normal' in n.image.name.lower())
    # Copy the original unmodified base image before editing either palette.
    original = base_node.image
    red_im = original.copy(); red_im.name = 'h8_red_albedo_2k'
    blue_im = original.copy(); blue_im.name = 'h8_blue_albedo_2k'
    blue_im = save_image_2k(blue_im, 'h8_blue_albedo_2k.png', variant='blue')
    red_im = save_image_2k(red_im, 'h8_red_albedo_2k.png', variant='red')
    orm_im = orm_node.image.copy(); orm_im.name = 'h8_orm_2k'
    normal_im = normal_node.image.copy(); normal_im.name = 'h8_normal_2k'
    orm_im = save_image_2k(orm_im, 'h8_orm_2k.png')
    normal_im = save_image_2k(normal_im, 'h8_normal_2k.png')
    blue = source_mat.copy(); blue.name = 'H8_Blue_PBR'
    red = source_mat.copy(); red.name = 'H8_Red_PBR'
    for mat, image in ((blue, blue_im),(red, red_im)):
        for n in mat.node_tree.nodes:
            if n.type == 'TEX_IMAGE' and n.image:
                nm = n.image.name.lower()
                if 'normal' in nm: n.image = normal_im
                elif 'metallic' in nm: n.image = orm_im
                else: n.image = image
    graphite = bpy.data.materials.new('H8_RotorGraphite')
    graphite.diffuse_color = (.075,.095,.12,1)
    graphite.use_nodes = True
    p = graphite.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value = (.075,.095,.12,1)
    p.inputs['Metallic'].default_value = .52
    p.inputs['Roughness'].default_value = .43
    return blue, red, graphite

def create_prism_blade(name, parent, axis, radius, material, blade_count=4):
    # Axis Z is the horizontal main rotor, axis X is the vertical tail rotor.
    verts, faces = [], []
    for blade in range(blade_count):
        ang = 2*pi*blade/blade_count + (pi/4 if axis == 'Z' else 0)
        dr = Vector((cos(ang),sin(ang),0)) if axis == 'Z' else Vector((0,cos(ang),sin(ang)))
        tangent = Vector((-dr.y,dr.x,0)) if axis == 'Z' else Vector((0,-dr.z,dr.y))
        thick_dir = Vector((0,0,1)) if axis == 'Z' else Vector((1,0,0))
        base = len(verts)
        for radius_fraction, width, thickness in ((.13, .48, .055),(.60,.39,.04),(1.0,.27,.025)):
            center = dr * (radius * radius_fraction)
            for side, depth in ((-1,-1),(1,-1),(1,1),(-1,1)):
                p = center + tangent * (side*width/2) + thick_dir*(depth*thickness/2)
                verts.append(tuple(p))
        for j in range(2):
            a,b = base+j*4, base+(j+1)*4
            for k in range(4):
                faces.append((a+k,a+(k+1)%4,b+(k+1)%4,b+k))
        faces.append((base+3,base+2,base+1,base))
        faces.append((base+8,base+9,base+10,base+11))
    me = bpy.data.meshes.new(name+'Mesh')
    me.from_pydata(verts, [], faces); me.update()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    ob.parent = parent
    ob.data.materials.append(material)
    for p in ob.data.polygons: p.use_smooth = True
    return ob

def add_cylinder(name, parent, radius, depth, material, rotation=None):
    bpy.ops.mesh.primitive_cylinder_add(vertices=20, radius=radius, depth=depth)
    ob = bpy.context.object; ob.name = name
    ob.parent = parent
    if rotation: ob.rotation_euler = rotation
    ob.data.materials.append(material)
    for p in ob.data.polygons: p.use_smooth = True
    return ob

def make_empty(name, parent=None, location=None):
    ob = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(ob)
    ob.empty_display_type = 'ARROWS'; ob.empty_display_size = .35
    if parent: ob.parent = parent
    if location: ob.location = location
    return ob

def set_mesh_pivot(o, pivot, parent):
    # After applying source import rotation/scale, mesh vertices are world-aligned.
    for v in o.data.vertices:
        v.co -= pivot
    o.parent = parent
    o.location = pivot

def make_lod0(mesh_parts, blue, graphite):
    root = make_empty('H8_Root')
    parts = {}
    pivots = {
        'Body': Vector((0,0,0)),
        'ChinGun': src_to_world(CHIN_PIVOT_SRC),
        'PortWeaponPod': src_to_world(POD_PORT_SRC),
        'StarboardWeaponPod': src_to_world(POD_STARBOARD_SRC),
    }
    for name, o in mesh_parts.items():
        if name == 'RotorHub': continue
        o.name = name
        o.data.materials.clear(); o.data.materials.append(blue)
        for p in o.data.polygons: p.material_index = 0
        o.scale = (S,S,S)
        active(o); bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
        weld_atlas_seams(o)
        after = decimate(o, LOD0_BUDGET[name])
        print('LOD0', name, 'tris', after)
        set_mesh_pivot(o, pivots[name], root)
        parts[name] = o

    main = make_empty('MainRotor', root, src_to_world(MAIN_HUB_SRC))
    hub = mesh_parts.get('RotorHub')
    if hub:
        hub.name = 'RotorHub'
        hub.data.materials.clear(); hub.data.materials.append(blue)
        for p in hub.data.polygons: p.material_index = 0
        hub.scale = (S,S,S)
        active(hub); bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
        weld_atlas_seams(hub)
        decimate(hub, LOD0_BUDGET['RotorHub'])
        # Hub's object origin inherits MainRotor's true axis.
        for v in hub.data.vertices: v.co -= main.location
        hub.parent = main
        parts['RotorHub'] = hub
    parts['RotorBlades'] = create_prism_blade('RotorBlades', main, 'Z', ROTOR_DIAMETER_M/2, graphite)
    parts['RotorCap'] = add_cylinder('RotorCap', main, .37, .20, graphite)

    tail = make_empty('TailRotor', root, src_to_world(TAIL_HUB_SRC))
    parts['TailRotorBlades'] = create_prism_blade('TailRotorBlades', tail, 'X', 1.03, graphite)
    parts['TailRotorCap'] = add_cylinder('TailRotorCap', tail, .17, .19, graphite, rotation=(0,pi/2,0))
    return root

def hierarchy(root):
    out=[]; stack=[root]
    while stack:
        ob=stack.pop(); out.append(ob)
        stack.extend(ob.children)
    return out

def rename_for_storage(root, suffix):
    for ob in hierarchy(root):
        ob.name = ob.name + suffix

def clone_lod(old_root, ratio):
    original=hierarchy(old_root)
    new={}
    for o in original:
        c=o.copy()
        if o.data: c.data=o.data.copy()
        bpy.context.scene.collection.objects.link(c)
        c.name=o.name.rsplit('_LOD',1)[0]
        new[o]=c
    for o,c in new.items():
        c.parent=new.get(o.parent)
        c.matrix_parent_inverse=o.matrix_parent_inverse.copy()
        if c.type=='MESH' and o.name.split('_LOD')[0] not in {'RotorBlades','TailRotorBlades','RotorCap','TailRotorCap'}:
            decimate(c, int(count_triangles(c)*ratio))
    return new[old_root]

def export_lod(root, lod_idx, team, material):
    objects=hierarchy(root)
    for ob in objects:
        if ob.type=='MESH' and ob.data.materials and ob.data.materials[0].name.startswith('H8_') and 'RotorGraphite' not in ob.data.materials[0].name:
            ob.data.materials[0]=material
    bpy.ops.object.select_all(action='DESELECT')
    for ob in objects: ob.select_set(True)
    bpy.context.view_layer.objects.active=root
    stem=f'{team}-h8-lod{lod_idx}'
    glb=HERE/(stem+'.glb'); fbx=HERE/(stem+'.fbx')
    bpy.ops.export_scene.gltf(filepath=str(glb), export_format='GLB', use_selection=True,
                              export_yup=True, export_materials='EXPORT', export_apply=True)
    bpy.ops.export_scene.fbx(filepath=str(fbx), use_selection=True, object_types={'MESH','EMPTY'},
                             axis_forward='-Z', axis_up='Y', global_scale=1.0,
                             apply_unit_scale=True, bake_space_transform=False,
                             path_mode='RELATIVE', embed_textures=False, add_leaf_bones=False)
    return str(glb),str(fbx)

def summarize_lod(root):
    obs=hierarchy(root)
    return {'triangles':sum(count_triangles(o) for o in obs),
            'parts':{o.name:count_triangles(o) for o in obs if o.type=='MESH'},
            'pivots':{o.name:[round(v,4) for v in o.matrix_world.translation] for o in obs if o.type=='EMPTY'},
            'objects':len(obs)}

def main():
    assert SOURCE.exists(), f'Missing source: {SOURCE}'
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.gltf(filepath=str(SOURCE))
    imported = next(o for o in bpy.data.objects if o.type=='MESH')
    source_mat = imported.data.materials[0]
    print('SOURCE_WORLD_MATRIX',tuple(tuple(round(float(v),4) for v in row) for row in imported.matrix_world))
    find,stats=partition_components(imported.data)
    groups={r:classify(s) for r,s in stats.items()}
    group_triangles=Counter()
    for poly in imported.data.polygons:
        group=groups[find(poly.vertices[0])]
        group_triangles[group]+=1
    print('SOURCE_GROUP_TRIANGLES',dict(group_triangles))

    names=['Body','RotorHub','ChinGun','PortWeaponPod','StarboardWeaponPod','Discard']
    for name in names:
        imported.data.materials.append(bpy.data.materials.new('H8Group_'+name))
    slot={name:1+i for i,name in enumerate(names)}
    for poly in imported.data.polygons:
        poly.material_index=slot[groups[find(poly.vertices[0])]]
    active(imported)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.separate(type='MATERIAL')
    bpy.ops.object.mode_set(mode='OBJECT')
    parts={}
    for o in [o for o in bpy.data.objects if o.type=='MESH']:
        used={o.data.materials[p.material_index].name for p in o.data.polygons}
        assert len(used)==1, (o.name,used)
        group=used.pop().replace('H8Group_','')
        if group=='Discard':
            bpy.data.objects.remove(o,do_unlink=True)
        elif group in names:
            parts[group]=o
    assert {'Body','RotorHub','ChinGun','PortWeaponPod','StarboardWeaponPod'} <= parts.keys(),parts.keys()

    blue,red,graphite=make_textures_and_material(source_mat)
    lod_roots=[]; outputs=[]; summaries={}
    root=make_lod0(parts,blue,graphite)
    for lod_idx in range(3):
        if lod_idx:
            previous=lod_roots[-1]
            rename_for_storage(previous,f'_LOD{lod_idx-1}')
            # Derive each LOD from the accepted LOD0 mesh. Applying two
            # consecutive collapses damaged the thin cockpit and wing shell.
            root=clone_lod(lod_roots[0], LOD_REDUCE[lod_idx])
        lod_roots.append(root)
        summary=summarize_lod(root)
        summaries[f'lod{lod_idx}']=summary
        print('LOD_SUMMARY',lod_idx,json.dumps(summary))
        outputs.append({'lod':lod_idx,'team':'blue','files':export_lod(root,lod_idx,'blue',blue)})
        outputs.append({'lod':lod_idx,'team':'red','files':export_lod(root,lod_idx,'red',red)})
        # Reset source material so the editable project defaults to the blue team.
        for o in hierarchy(root):
            if o.type=='MESH' and o.data.materials and o.data.materials[0].name=='H8_Red_PBR':
                o.data.materials[0]=blue
    # Keep only the blue LOD0 visible in the source file.
    for idx,root in enumerate(lod_roots):
        if idx:
            for o in hierarchy(root): o.hide_render=True; o.hide_set(True)
    bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'h8-kestrel.blend'))
    report={'source':str(SOURCE),'source_triangle_groups':dict(group_triangles),
            'scale':S,'rotor_diameter_m':ROTOR_DIAMETER_M,'lods':summaries,
            'exports':outputs,'texture_files':[str(p) for p in sorted(TEXTURES.glob('*.png'))]}
    REPORT.write_text(json.dumps(report,indent=2))
    print('BUILD_COMPLETE',REPORT)

if __name__=='__main__':
    main()
