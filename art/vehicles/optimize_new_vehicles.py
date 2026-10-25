"""Optimize supplied vehicle GLBs while preserving their source appearance.

Blender -b --factory-startup --python optimize_new_vehicles.py -- j20
Blender -b --factory-startup --python optimize_new_vehicles.py -- r4
Blender -b --factory-startup --python optimize_new_vehicles.py -- t90 v12 u8 m2 r3
"""
import bpy
import bmesh
import hashlib
import json
import math
import sys
from pathlib import Path
from mathutils import Vector

HERE = Path(__file__).resolve().parent
CONFIG = {
    'j20': dict(folder='j20-stratus', title='J20 STRATUS',
                source='/Users/liuwenjun/Downloads/e500d16c7909d01d470ae8ad618285a5.glb',
                budgets=[40000, 20000, 10000]),
    'r4': dict(folder='r4-scout', title='R4 SCOUT',
               source='/Users/liuwenjun/Downloads/aeb2ce5110245d186594fb95589644f4.glb',
               budgets=[60000, 30000, 15000]),
    't90': dict(folder='t90-bastion', title='T90 BASTION',
                source='/Users/liuwenjun/Downloads/c9fd2d8fd8c3ec5cfbcfff869985a9b8.glb',
                budgets=[80000, 40000, 20000]),
    'v12': dict(folder='v12-lancer', title='V12 LANCER',
                source='/Users/liuwenjun/Downloads/fae886068598e465b15c7b0082fb3061.glb',
                budgets=[70000, 35000, 17500]),
    'u8': dict(folder='u8-rover', title='U8 ROVER',
               source='/Users/liuwenjun/Downloads/4e6d759fd311d5370f59552486ec1ade.glb',
               budgets=[60000, 30000, 15000]),
    'm2': dict(folder='m2-courier', title='M2 COURIER',
               source='/Users/liuwenjun/Downloads/17f7e3624bd9b87fcc6e3efe8f6bc47c.glb',
               budgets=[50000, 25000, 12500]),
    'r3': dict(folder='r3-firefly', title='R3 FIREFLY',
               source='/Users/liuwenjun/Downloads/616637844d883cd74eac2eb255af4b8e.glb',
               budgets=[50000, 25000, 12500]),
}

def activate(obj):
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj

def measure(obj):
    obj.data.calc_loop_triangles()
    points = [obj.matrix_world @ v.co for v in obj.data.vertices]
    mn = [min(p[i] for p in points) for i in range(3)]
    mx = [max(p[i] for p in points) for i in range(3)]
    bm = bmesh.new(); bm.from_mesh(obj.data)
    metrics = dict(triangles=len(obj.data.loop_triangles), vertices=len(obj.data.vertices),
        min=mn, max=mx, dimensions=[b-a for a,b in zip(mn,mx)],
        boundary_edges=sum(e.is_boundary for e in bm.edges),
        nonmanifold_edges=sum(not e.is_manifold for e in bm.edges),
        degenerate_faces=sum(f.calc_area() < 1e-14 for f in bm.faces),
        isolated_vertices=sum(not v.link_edges for v in bm.verts),
        uv_layers=len(obj.data.uv_layers))
    bm.free()
    return metrics

def resize_textures(obj, folder, key):
    out = folder/'textures'; out.mkdir(exist_ok=True)
    replacements = {}
    for mat in obj.data.materials:
        mat.name = key.upper()+'_Source_PBR'
        for node in mat.node_tree.nodes:
            if node.type != 'TEX_IMAGE' or not node.image: continue
            source = node.image
            if source.name not in replacements:
                role = ('normal' if 'normal' in source.name.lower() else
                        'metalrough' if 'metallic' in source.name.lower() else 'basecolor')
                path = out/f'{key}_{role}_2k.png'
                color_space = source.colorspace_settings.name
                temp = source.copy()
                temp.scale(2048, 2048)
                temp.filepath_raw = str(path)
                temp.file_format = 'PNG'
                temp.save()
                # Discard the inherited original 4K packed payload.
                bpy.data.images.remove(temp)
                image = bpy.data.images.load(str(path), check_existing=False)
                image.name = path.stem
                image.colorspace_settings.name = color_space
                image.pack()
                replacements[source.name] = image
            node.image = replacements[source.name]
    return [dict(name=im.name, size=list(im.size), colorspace=im.colorspace_settings.name)
            for im in replacements.values()]

def export(obj, path):
    activate(obj)
    bpy.ops.export_scene.gltf(filepath=str(path.with_suffix('.glb')), export_format='GLB',
        use_selection=True, export_yup=True, export_materials='EXPORT', export_apply=True)
    bpy.ops.export_scene.fbx(filepath=str(path.with_suffix('.fbx')), use_selection=True,
        object_types={'MESH'}, axis_forward='-Z', axis_up='Y', global_scale=1,
        apply_unit_scale=True, bake_space_transform=False, path_mode='RELATIVE',
        embed_textures=False, add_leaf_bones=False)

def main(key):
    cfg = CONFIG[key]
    folder = HERE/cfg['folder']; folder.mkdir(exist_ok=True)
    (folder/'exports').mkdir(exist_ok=True)
    source_path = Path(cfg['source'])
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(source_path))
    source = next(o for o in bpy.context.scene.objects if o.type == 'MESH')
    source.name = cfg['title'].replace(' ', '_')+'_SOURCE'
    activate(source)
    # Apply the import transform without changing world-space geometry.
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    original = measure(source)
    bm = bmesh.new(); bm.from_mesh(source.data)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-7)
    bm.to_mesh(source.data); bm.free(); source.data.update()
    welded = measure(source)
    print('WELD',key,original['boundary_edges'],'->',welded['boundary_edges'],flush=True)
    textures = resize_textures(source, folder, key)
    report = dict(title=cfg['title'], source=str(source_path),
        source_sha256=hashlib.sha256(source_path.read_bytes()).hexdigest(),
        blender=bpy.app.version_string, original=original, welded=welded,
        textures=textures, authored_lods=[],
        contract='Static mesh optimization: original proportions, orientation, UVs, paint and normalized scale retained. No new animation rig or gameplay replacement.')
    roots = []
    for lod, target in enumerate(cfg['budgets']):
        obj = source.copy(); obj.data = source.data.copy()
        bpy.context.scene.collection.objects.link(obj)
        obj.name = cfg['title'].replace(' ', '_')+f'_LOD{lod}'
        activate(obj)
        modifier = obj.modifiers.new('Topology_preserving_reduction', 'DECIMATE')
        modifier.ratio = target / welded['triangles']
        modifier.use_collapse_triangulate = True
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        if key != 'j20' or lod > 0:
            # Imported custom split normals interpolate poorly after collapsing
            # dense panels (and the jet's distant LODs). Recompute normals on the actual
            # low mesh, retaining crisp manufactured edges and the normal map.
            bpy.ops.mesh.customdata_custom_splitnormals_clear()
            obj.data.set_sharp_from_angle(angle=math.radians(60))
            for poly in obj.data.polygons: poly.use_smooth=True
            normals=obj.modifiers.new('Panel_weighted_normals','WEIGHTED_NORMAL')
            normals.keep_sharp=True;normals.weight=50
            bpy.ops.object.modifier_apply(modifier=normals.name)
        bpy.context.view_layer.update()
        metrics = measure(obj)
        assert metrics['triangles'] <= target+8
        assert metrics['uv_layers'] > 0
        assert metrics['degenerate_faces'] == 0, (key,lod,metrics)
        # The welded input is closed; reduction should not create new holes.
        assert metrics['boundary_edges'] <= welded['boundary_edges'], (key,lod,metrics)
        export(obj,folder/'exports'/f'{key}-lod{lod}')
        metrics['lod'] = lod
        metrics['name'] = obj.name
        report['authored_lods'].append(metrics)
        roots.append(obj)
        print('EXPORTED',key,lod,metrics['triangles'],flush=True)
    bpy.data.objects.remove(source,do_unlink=True)
    for i,obj in enumerate(roots):
        obj.hide_render=i>0
        obj.hide_set(i>0)
    activate(roots[0])
    bpy.ops.outliner.orphans_purge(do_recursive=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(folder/f'{key}-optimized.blend'))
    (folder/'build_metrics.json').write_text(json.dumps(report,indent=2))
    print('OPTIMIZATION_COMPLETE',key,flush=True)

if __name__ == '__main__':
    for key in sys.argv[sys.argv.index('--')+1:]:
        main(key)
