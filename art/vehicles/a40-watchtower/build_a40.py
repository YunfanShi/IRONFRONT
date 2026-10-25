"""Rebuild the supplied A40 Hunyuan mesh as a segmented, optimized Unity asset.

Run with Blender 5.2:
  Blender --background --factory-startup --python build_a40.py

Source axis: mesh-local X right, Y up, +Z nose. Its imported object rotates this
to Blender world X right, Z up, -Y nose.  FBX export converts Blender -Y nose
back to Unity +Z nose / Y up.
"""
import bpy
import json
import math
import os
import sys
from collections import Counter
from pathlib import Path
from mathutils import Matrix, Vector
import numpy as np
import bmesh

HERE = Path(__file__).resolve().parent
SOURCE = Path("/Users/liuwenjun/Downloads/d722c1edebb754a5735858477acc4459.glb")
TEX_DIR = HERE / "textures"
TEX_DIR.mkdir(parents=True, exist_ok=True)
OUTPUT = HERE / "exports"
OUTPUT.mkdir(parents=True, exist_ok=True)
RENDERS = HERE / "renders"
RENDERS.mkdir(parents=True, exist_ok=True)

WIDTH_METERS = 4.7
LENGTH_METERS = 7.1
SOURCE_WIDTH = .670359
SOURCE_LENGTH = 1.108163
SCALE_X = WIDTH_METERS / SOURCE_WIDTH
SCALE_LENGTH = LENGTH_METERS / SOURCE_LENGTH
# Preserve the source vertical aspect after the full-length adjustment.
SCALE_HEIGHT = SCALE_LENGTH
TEX_SIZE = 2048

LABELS = ["Hull", "TurretArmor", "Barrel_Lower_L", "Barrel_Upper_L",
          "Barrel_Lower_R", "Barrel_Upper_R", "RadarArray"] + [
          f"Wheel_{s}_{p}" for s in "LR" for p in ("Front", "MidFront", "MidRear", "Rear")]
WHEEL_POS = (("Front", .34), ("MidFront", .11), ("MidRear", -.11), ("Rear", -.34))
LOD0_RATIOS = {
    "Hull": .08, "TurretArmor": .075, "RadarArray": .10,
    "barrel": .10, "wheel": .035,
}
LOD1_RATIO = .60
LOD2_RATIO = .30

def phase(msg):
    print("A40_STAGE", msg, flush=True)

def deselect():
    bpy.ops.object.select_all(action="DESELECT")

def set_active(obj):
    deselect()
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj

def find(i, parent):
    while parent[i] != i:
        parent[i] = parent[parent[i]]
        i = parent[i]
    return i

def components(mesh):
    n = len(mesh.vertices)
    parent = list(range(n))
    rank = bytearray(n)
    for p in mesh.polygons:
        a, b, c = p.vertices
        a = find(a, parent); b = find(b, parent); c = find(c, parent)
        if a != b:
            if rank[a] < rank[b]: a,b=b,a
            parent[b]=a
            if rank[a] == rank[b]: rank[a]+=1
        a=find(a,parent)
        if a != c:
            if rank[a] < rank[c]: a,c=c,a
            parent[c]=a
            if rank[a] == rank[c]: rank[a]+=1
    stats = {}
    for i, vert in enumerate(mesh.vertices):
        r = find(i, parent)
        s = stats.get(r)
        if s is None:
            s = dict(min=[1e9]*3,max=[-1e9]*3,vertices=0,triangles=0)
            stats[r]=s
        s["vertices"]+=1
        co=vert.co
        for k in range(3):
            v=co[k]
            if v<s["min"][k]: s["min"][k]=v
            if v>s["max"][k]: s["max"][k]=v
    for p in mesh.polygons:
        stats[find(p.vertices[0],parent)]["triangles"]+=1
    return parent, stats

def classify(s):
    mn, mx = s["min"], s["max"]
    cx,cy,cz=[(a+b)*.5 for a,b in zip(mn,mx)]
    if cy < .205 and mx[1] < .215 and abs(cx)>.16:
        side = "L" if cx < 0 else "R"
        for pos,center_z in WHEEL_POS:
            if mn[2]>center_z-.13 and mx[2]<center_z+.13 and all(
                    math.hypot(h-.095,z-center_z)<.145
                    for h in (mn[1],mx[1]) for z in (mn[2],mx[2])):
                return f"Wheel_{side}_{pos}"
    if mn[1]>.475 and -.32<cz<-.07 and abs(cx)<.17:
        return "RadarArray"
    if mn[2]>.16 and mx[2]>.27 and mn[1]>.31 and mx[1]<.445 and abs(cx)<.14:
        level="Upper" if cy>.373 else "Lower"
        side="L" if cx<0 else "R"
        return f"Barrel_{level}_{side}"
    if cy>.315 and abs(cx)<.275 and -.29<cz<.205 and mx[1]>.33:
        return "TurretArmor"
    return "Hull"

def create_texture_copies(source_material):
    images={}
    for node in source_material.node_tree.nodes:
        if node.type!="TEX_IMAGE" or node.image is None: continue
        src=node.image
        name=src.name.lower()
        if "normal" in name: role="normal"
        elif "metallic" in name or "roughness" in name: role="metalrough"
        else: role="basecolor"
        images[role]=src
    assert set(images)=={"normal","metalrough","basecolor"}, list(images)
    result={}
    for role in ("normal","metalrough"):
        img=images[role].copy()
        img.name=f"A40_{role}_2K"
        img.scale(TEX_SIZE,TEX_SIZE)
        img.filepath_raw=str(TEX_DIR/f"A40_{role}_2K.png")
        img.file_format="PNG"
        img.save()
        path=img.filepath_raw
        color_space=img.colorspace_settings.name
        # Image.copy() retains the original GLB's packed 4K payload even
        # after resizing. Reload the written 2K pixels before GLB export.
        bpy.data.images.remove(img)
        img=bpy.data.images.load(path,check_existing=False)
        img.name=f"A40_{role}_2K"
        img.colorspace_settings.name=color_space
        img.pack()
        result[role]=img
        phase(f"saved {path}")
    src=images["basecolor"].copy()
    src.name="A40_source_base_2K"
    src.scale(TEX_SIZE,TEX_SIZE)
    rgba=np.empty(TEX_SIZE*TEX_SIZE*4,dtype=np.float32)
    src.pixels.foreach_get(rgba)
    rgba=rgba.reshape((-1,4))
    original=rgba[:,:3].copy()
    luminance=np.max(original,axis=1)
    # Keep rubber, recesses and dark hardware dark. Recolor the painted panels
    # without flattening the photographic baked micro-detail.
    paint=np.clip((luminance-.17)/.26,0,1)[:,None]
    for team,tint in (("Blue",(0.46,0.60,0.75)),("Red",(0.84,0.62,0.42))):
        img=bpy.data.images.new(f"A40_{team}_basecolor_2K",
            width=TEX_SIZE,height=TEX_SIZE,alpha=True)
        img.colorspace_settings.name=src.colorspace_settings.name
        rgb=original*(1-paint)+original*np.array(tint,dtype=np.float32)[None,:]*paint
        if team=="Red":
            # The reference uses warm signal lights. Convert only the source
            # cyan accents, leaving the neutral armor and dark rubber intact.
            cyan=(original[:,2]>.30)&(original[:,2]>original[:,1]*1.12)&(original[:,1]>original[:,0]*1.35)
            rgb[cyan]=luminance[cyan,None]*np.array((.95,.20,.13),dtype=np.float32)
        rgba[:,:3]=rgb
        img.pixels.foreach_set(rgba.reshape(-1))
        img.filepath_raw=str(TEX_DIR/f"A40_{team}_basecolor_2K.png")
        img.file_format="PNG"
        img.save()
        img.pack()
        result[team.lower()]=img
        phase(f"saved {img.filepath_raw}")
    bpy.data.images.remove(src)
    return result

def make_material(source, images, team):
    # Construct a clean Principled graph. The imported Hunyuan material has
    # KHR_materials_specular color factor 2, which washed out the dark blue
    # armor despite a correctly tinted base map.
    mat=bpy.data.materials.new(f"A40_{team}_PBR")
    mat.use_nodes=True
    nodes=mat.node_tree.nodes
    nodes.clear()
    links=mat.node_tree.links
    out=nodes.new("ShaderNodeOutputMaterial")
    bsdf=nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.inputs["Roughness"].default_value=.72
    bsdf.inputs["Metallic"].default_value=.14
    links.new(bsdf.outputs["BSDF"],out.inputs["Surface"])
    base=nodes.new("ShaderNodeTexImage")
    base.label="Base color"
    base.image=images[team.lower()]
    links.new(base.outputs["Color"],bsdf.inputs["Base Color"])
    orm=nodes.new("ShaderNodeTexImage")
    orm.label="glTF G roughness B metallic"
    orm.image=images["metalrough"]
    orm.image.colorspace_settings.name="Non-Color"
    sep=nodes.new("ShaderNodeSeparateColor")
    sep.mode="RGB"
    links.new(orm.outputs["Color"],sep.inputs["Color"])
    links.new(sep.outputs["Green"],bsdf.inputs["Roughness"])
    links.new(sep.outputs["Blue"],bsdf.inputs["Metallic"])
    normal=nodes.new("ShaderNodeTexImage")
    normal.label="Normal"
    normal.image=images["normal"]
    normal.image.colorspace_settings.name="Non-Color"
    normal_map=nodes.new("ShaderNodeNormalMap")
    normal_map.inputs["Strength"].default_value=.65
    links.new(normal.outputs["Color"],normal_map.inputs["Color"])
    links.new(normal_map.outputs["Normal"],bsdf.inputs["Normal"])
    return mat

def split_semantic(obj, parent, stats):
    mesh=obj.data
    source_mat=mesh.materials[0]
    temp_materials=[]
    for label in LABELS:
        m=source_mat.copy()
        m.name=f"SEGMENT_{label}"
        mesh.materials.append(m)
        temp_materials.append(m)
    labels_by_root={r:classify(s) for r,s in stats.items()}
    wheel_leaks=[]
    for r,s in stats.items():
        if labels_by_root[r]!="Hull": continue
        mn,mx=s["min"],s["max"]
        cx,cy,cz=[(a+b)*.5 for a,b in zip(mn,mx)]
        wheel_z=min(abs(cz-p[1]) for p in WHEEL_POS)
        if abs(cx)>.13 and mx[1]<.235 and wheel_z<.095:
            wheel_leaks.append(dict(triangles=s["triangles"],
                center=[cx,cy,cz],size=[mx[k]-mn[k] for k in range(3)]))
    wheel_leaks.sort(key=lambda x:x["triangles"],reverse=True)
    counts=Counter()
    for poly in mesh.polygons:
        r=find(poly.vertices[0],parent)
        label=labels_by_root[r]
        poly.material_index=LABELS.index(label)+1
        counts[label]+=1
    phase(f"classified {dict(counts)}")
    (HERE/"component_classification.json").write_text(json.dumps(dict(
        source=str(SOURCE), counts=counts, components=len(stats),
        source_vertices=len(mesh.vertices), source_triangles=len(mesh.polygons),
        wheel_region_hull_components=len(wheel_leaks),
        wheel_region_hull_triangles=sum(x["triangles"] for x in wheel_leaks),
        wheel_region_hull_top=wheel_leaks[:40]),indent=2))
    set_active(obj)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.separate(type="MATERIAL")
    bpy.ops.object.mode_set(mode="OBJECT")
    objs=[o for o in bpy.context.selected_objects if o.type=="MESH"]
    phase(f"separated {len(objs)} mesh objects")
    result={}
    for part in objs:
        c=Counter(part.data.materials[p.material_index].name for p in part.data.polygons)
        name=c.most_common(1)[0][0].replace("SEGMENT_","")
        assert name in LABELS, name
        assert name not in result, name
        part.name=name
        result[name]=part
    return result, source_mat

def apply_local_scale_and_rotation(parts):
    for obj in parts.values():
        # The imported GLB object contains the same +90 X conversion on every
        # separated mesh. Scale source-local length and height separately.
        obj.scale.x *= SCALE_X
        obj.scale.y *= SCALE_HEIGHT
        obj.scale.z *= SCALE_LENGTH
        set_active(obj)
        bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)

def purge_temp_materials(parts, blue_mat):
    for obj in parts.values():
        obj.data.materials.clear()
        obj.data.materials.append(blue_mat)

def weld_atlas_seams(obj):
    # Keep loop UVs while making glTF's duplicated seam vertices continuous
    # for the decimator. Otherwise UV islands collapse independently and tear.
    bm=bmesh.new()
    bm.from_mesh(obj.data)
    before=sum(e.is_boundary for e in bm.edges)
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00001)
    after=sum(e.is_boundary for e in bm.edges)
    bm.to_mesh(obj.data); bm.free(); obj.data.update()
    phase(f"weld {obj.name}: boundary {before} -> {after}")

def decimate(obj, ratio):
    if len(obj.data.polygons)<100: return
    mod=obj.modifiers.new("ProductionDecimate","DECIMATE")
    mod.decimate_type="COLLAPSE"
    mod.ratio=ratio
    mod.use_collapse_triangulate=True
    set_active(obj)
    bpy.ops.object.modifier_apply(modifier=mod.name)

def clamp_wheel_ground(obj):
    # Source treads touch z=0. At low LODs edge collapses can place a few
    # vertices below the ground plane, causing visible clipping in Unity.
    if not obj.name.startswith("Wheel_"): return
    for v in obj.data.vertices:
        if v.co.z<0: v.co.z=0

def trim_wheel_radial_outliers(obj):
    if not obj.name.startswith("Wheel_"): return
    position=obj.name.split("_")[2]
    center_y=-dict(WHEEL_POS)[position]*SCALE_LENGTH
    center_z=.65
    bm=bmesh.new()
    bm.from_mesh(obj.data)
    limit2=.72**2
    bad=[f for f in bm.faces if any(
        (v.co.y-center_y)**2+(v.co.z-center_z)**2>limit2
        for v in f.verts)]
    removed=len(bad)
    if bad:
        bmesh.ops.delete(bm,geom=bad,context="FACES")
        bmesh.ops.delete(bm,geom=[v for v in bm.verts if not v.link_faces],
            context="VERTS")
        bm.to_mesh(obj.data)
        obj.data.update()
    bm.free()
    if removed: phase(f"trimmed {removed} radial outlier faces in {obj.name}")

def world_pivot(obj, p):
    # Mesh vertices are now in Blender world coordinates and object transform
    # is identity. Move the origin to an actual mechanical rotation center.
    obj.data.transform(Matrix.Translation(-Vector(p)))
    obj.matrix_world=Matrix.Translation(Vector(p))
    bpy.context.view_layer.update()

def empty(name, at, parent=None):
    o=bpy.data.objects.new(name,None)
    bpy.context.collection.objects.link(o)
    o.empty_display_type="PLAIN_AXES"
    o.empty_display_size=.15
    o.matrix_world=Matrix.Translation(Vector(at))
    bpy.context.view_layer.update()
    if parent:
        world=o.matrix_world.copy()
        o.parent=parent
        o.matrix_parent_inverse=parent.matrix_world.inverted()
        o.matrix_world=world
        bpy.context.view_layer.update()
    return o

def parent_keep(obj, parent):
    bpy.context.view_layer.update()
    world=obj.matrix_world.copy()
    obj.parent=parent
    obj.matrix_parent_inverse=parent.matrix_world.inverted()
    obj.matrix_world=world
    bpy.context.view_layer.update()

def bounds(obj):
    pts=[obj.matrix_world@Vector(c) for c in obj.bound_box]
    return [[min(p[i] for p in pts) for i in range(3)],
            [max(p[i] for p in pts) for i in range(3)]]

def make_hierarchy(parts, lod):
    root=empty(f"A40_LOD{lod}",(0,0,0))
    root["bas_role"]="vehicle_root"
    turret=empty("Turret",(0,.10,2.00),root)
    turret["bas_role"]="turret_yaw"
    barrels=empty("Barrels",(0,.10,2.48),turret)
    barrels["bas_role"]="barrels_elevation"
    radar=empty("Radar",(.25,1.17,3.12),turret)
    radar["bas_role"]="radar_azimuth"
    for label,obj in parts.items():
        if label=="Hull": pivot=(0,0,0); parent=root
        elif label=="TurretArmor": pivot=turret.matrix_world.translation; parent=turret
        elif label=="RadarArray": pivot=radar.matrix_world.translation; parent=radar
        elif label.startswith("Barrel_"): pivot=barrels.matrix_world.translation; parent=barrels
        else:
            side="L" if "_L_" in label else "R"
            position=label.split("_")[-1]
            longitudinal=dict(WHEEL_POS)[position]
            b=bounds(obj)
            center_x=(b[0][0]+b[1][0])*.5
            pivot=(center_x,-longitudinal*SCALE_LENGTH,.65)
            parent=root
        world_pivot(obj,pivot)
        parent_keep(obj,parent)
        obj["bas_role"]="wheel_spin" if label.startswith("Wheel_") else (
            "barrel" if label.startswith("Barrel_") else label.lower())
        obj["lod_level"]=lod
    lower_left=parts["Barrel_Lower_L"]
    muzzle_vertices=[lower_left.matrix_world@v.co for v in lower_left.data.vertices]
    most_forward=min(v.y for v in muzzle_vertices)
    rim=[v for v in muzzle_vertices if v.y<most_forward+.035]
    muzzle_point=(sum(v.x for v in rim)/len(rim),most_forward,
                  sum(v.z for v in rim)/len(rim))
    muzzle=empty("Muzzle_LeftLower",muzzle_point,barrels)
    muzzle["bas_role"]="muzzle"
    return root

def descendants(root):
    out=[root]
    todo=[root]
    while todo:
        node=todo.pop()
        out.extend(node.children)
        todo.extend(node.children)
    return list(dict.fromkeys(out))

def export_selected(root,lod,fmt,variant=""):
    deselect()
    for obj in descendants(root): obj.select_set(True)
    bpy.context.view_layer.objects.active=root
    infix=f"_{variant}" if variant else ""
    path=OUTPUT/f"A40_WATCHTOWER{infix}_LOD{lod}.{fmt}"
    if fmt=="glb":
        bpy.ops.export_scene.gltf(filepath=str(path),export_format="GLB",
            use_selection=True,export_apply=True,
            export_materials="EXPORT",export_texcoords=True,export_normals=True)
    else:
        bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,
            apply_unit_scale=True,apply_scale_options="FBX_SCALE_UNITS",
            axis_forward="-Z",axis_up="Y",path_mode="RELATIVE",
            embed_textures=False,use_mesh_modifiers=False,
            use_custom_props=True,add_leaf_bones=False)
    phase(f"exported {path}")
    return path

def clone_lod(parts,level,ratio):
    copies={}
    for name,orig in parts.items():
        obj=orig.copy()
        obj.data=orig.data.copy()
        bpy.context.collection.objects.link(obj)
        world=orig.matrix_world.copy()
        obj.parent=None
        obj.matrix_world=Matrix.Identity(4)
        obj.data.transform(world)
        obj.name=f"{name}_LOD{level}"
        decimate(obj,ratio)
        clamp_wheel_ground(obj)
        trim_wheel_radial_outliers(obj)
        copies[name]=obj
    return copies

def metrics(root, level):
    parts=[o for o in descendants(root) if o.type=="MESH"]
    triangles=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in parts)
    bb=[bounds(o) for o in parts]
    mini=[min(b[0][i] for b in bb) for i in range(3)]
    maxi=[max(b[1][i] for b in bb) for i in range(3)]
    bpy.context.view_layer.update()
    attachments={}
    for o in descendants(root):
        if o.name.startswith(("Turret","Barrels","Radar","Wheel_","Muzzle_LeftLower")):
            p=o.matrix_world.translation
            attachments[o.name]=[p.x,p.z,-p.y]
    return dict(lod=level,triangles=triangles,mesh_objects=len(parts),
        bounds_blender_xyz=[mini,maxi],dimensions_m=[maxi[i]-mini[i] for i in range(3)],
        names=sorted(o.name for o in parts),
        attachment_points_unity_xyz_m=attachments,
        hierarchy={o.name:(o.parent.name if o.parent else None) for o in descendants(root)})

def add_camera(name,loc,target,ortho):
    data=bpy.data.cameras.new(name)
    cam=bpy.data.objects.new(name,data); bpy.context.collection.objects.link(cam)
    cam.location=loc
    direction=Vector(target)-cam.location
    cam.rotation_euler=direction.to_track_quat("-Z","Y").to_euler()
    data.type="ORTHO"; data.ortho_scale=ortho
    return cam

def render_views(root):
    # Hide other LODs during authored LOD0 evidence renders.
    for o in bpy.data.objects:
        if o.name.startswith("A40_LOD") and o!=root:
            o.hide_render=True
            for child in descendants(o): child.hide_render=True
    bpy.context.scene.render.engine="BLENDER_EEVEE"
    scene=bpy.context.scene
    scene.render.resolution_x=900; scene.render.resolution_y=700
    scene.render.resolution_percentage=100
    scene.render.image_settings.file_format="PNG"
    if scene.world is None:
        scene.world=bpy.data.worlds.new("A40_Studio_World")
    scene.world.color=(.12,.15,.19)
    scene.render.film_transparent=False
    def light(name,loc,power,size):
        d=bpy.data.lights.new(name,"AREA")
        d.energy=power; d.shape="DISK"; d.size=size
        o=bpy.data.objects.new(name,d); bpy.context.collection.objects.link(o)
        o.location=loc
        o.rotation_euler=(Vector((0,0,1.3))-o.location).to_track_quat("-Z","Y").to_euler()
    light("Key",(-5,-5,8),2400,7)
    light("Fill",(4,3,5),1450,6)
    target=(0,0,1.7)
    cams={
        "perspective":add_camera("Cam_Perspective",(9,-12,8),target,11),
        "front":add_camera("Cam_Front",(0,-12,2.1),target,6.6),
        "left":add_camera("Cam_Left",(-12,0,2.1),target,9.3),
        "back":add_camera("Cam_Back",(0,12,2.1),target,6.6),
        "top":add_camera("Cam_Top",(0,0,15),target,9.3),
    }
    for name,cam in cams.items():
        scene.camera=cam
        scene.render.filepath=str(RENDERS/f"lod0_{name}.png")
        bpy.ops.render.render(write_still=True)

def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    phase("import source")
    bpy.ops.import_scene.gltf(filepath=str(SOURCE))
    imported=next(o for o in bpy.context.scene.objects if o.type=="MESH")
    source_mat=imported.data.materials[0]
    phase("components")
    parent,stats=components(imported.data)
    phase("textures")
    images=create_texture_copies(source_mat)
    blue=make_material(source_mat,images,"Blue")
    red=make_material(source_mat,images,"Red")
    red.use_fake_user=True
    parts,source_mat=split_semantic(imported,parent,stats)
    assert set(parts)==set(LABELS), set(LABELS)-set(parts)
    del parent,stats
    apply_local_scale_and_rotation(parts)
    purge_temp_materials(parts,blue)
    phase("decimate LOD0")
    for name,obj in parts.items():
        weld_atlas_seams(obj)
        ratio=LOD0_RATIOS["wheel"] if name.startswith("Wheel_") else (
            LOD0_RATIOS["barrel"] if name.startswith("Barrel_") else LOD0_RATIOS[name])
        decimate(obj,ratio)
        clamp_wheel_ground(obj)
        trim_wheel_radial_outliers(obj)
    root0=make_hierarchy(parts,0)
    lod_metrics=[metrics(root0,0)]
    phase(f"LOD0 triangles {lod_metrics[-1]['triangles']}")
    roots=[root0]
    for level,ratio in ((1,LOD1_RATIO),(2,LOD2_RATIO)):
        phase(f"LOD{level}")
        copies=clone_lod(parts,level,ratio)
        root=make_hierarchy(copies,level)
        roots.append(root)
        lod_metrics.append(metrics(root,level))
        phase(f"LOD{level} triangles {lod_metrics[-1]['triangles']}")
    for level,root in enumerate(roots):
        export_selected(root,level,"glb")
        export_selected(root,level,"fbx")
    # Red is a material swap over the exact same LOD0 mesh and pivots.
    for obj in parts.values():
        obj.data.materials.clear()
        obj.data.materials.append(red)
    export_selected(root0,0,"glb","RED")
    export_selected(root0,0,"fbx","RED")
    for obj in parts.values():
        obj.data.materials.clear()
        obj.data.materials.append(blue)
    for i,r in enumerate(roots):
        r.hide_viewport=i>0
        r.hide_render=i>0
        for c in descendants(r):
            c.hide_render=i>0
    bpy.context.scene.unit_settings.system="METRIC"
    bpy.ops.wm.save_as_mainfile(filepath=str(HERE/"A40_WATCHTOWER.blend"))
    phase("render evidence")
    render_views(root0)
    output=dict(source=str(SOURCE),blender_version=bpy.app.version_string,
        dimensions_target_m=dict(width=WIDTH_METERS,length=LENGTH_METERS),
        local_source_to_blender_world="X width, local +Z front => Blender -Y, local +Y up => Blender +Z",
        unity_axes="Unity Y up, +Z forward",textures=[str(p) for p in TEX_DIR.glob("*.png")],
        materials=[blue.name,red.name],lods=lod_metrics,
        export_axis=dict(forward="-Z",up="Y",scale=1))
    (HERE/"build_metrics.json").write_text(json.dumps(output,indent=2))
    phase("complete")

if __name__=="__main__":
    main()
