"""Bake red faction materials and export the seven existing optimized vehicles.

Blender -b --factory-startup --python create_red_variants.py -- t90 v12 r4 u8 m2 r3 j20
Only new red files are written. Existing mesh geometry, UVs and blue assets stay intact.
"""
import bpy
import hashlib
import json
import sys
from pathlib import Path
from mathutils import Vector

HERE=Path(__file__).resolve().parent
sys.path.insert(0,str(HERE))
from optimize_new_vehicles import CONFIG, activate, export

PALETTES={
    't90':(1.34,1.08,.85), 'v12':(1.34,1.08,.85),
    'r4':(1.52,1.22,.92), 'u8':(1.43,1.15,.88),
    'm2':(1.40,1.16,.94), 'r3':(1.65,.65,.48),
    'j20':(1.34,1.09,.87),
}

def digest(path):return hashlib.sha256(Path(path).read_bytes()).hexdigest()

def material_graph(source,orm_image,key,position_mask=False):
    mat=bpy.data.materials.new(key.upper()+'_Red_Bake_Material');mat.use_nodes=True
    nodes=mat.node_tree.nodes;nodes.clear();links=mat.node_tree.links
    def node(kind):return nodes.new(kind)
    def bind(value,socket):
        if hasattr(value,'is_output'):links.new(value,socket)
        else:socket.default_value=value
    def math(op,a,b=0):
        n=node('ShaderNodeMath');n.operation=op;bind(a,n.inputs[0]);bind(b,n.inputs[1]);return n.outputs[0]
    def clamp_ramp(x,lo,hi):
        return math('MINIMUM',math('MAXIMUM',math('DIVIDE',math('SUBTRACT',x,lo),hi-lo),0),1)
    def mix(f,a,b):
        n=node('ShaderNodeMixRGB');n.blend_type='MIX';bind(f,n.inputs[0]);bind(a,n.inputs[1]);bind(b,n.inputs[2]);return n.outputs[0]
    def tint(lum,color):
        n=node('ShaderNodeMixRGB');n.blend_type='MULTIPLY';n.inputs[0].default_value=1;bind(lum,n.inputs[1]);n.inputs[2].default_value=(*color,1);return n.outputs[0]
    tex=node('ShaderNodeTexImage');tex.image=source;tex.interpolation='Linear'
    rgb=tex.outputs['Color'];hsv=node('ShaderNodeSeparateColor');hsv.mode='HSV';links.new(rgb,hsv.inputs[0])
    h,s,v=hsv.outputs[0],hsv.outputs[1],hsv.outputs[2]
    bw=node('ShaderNodeRGBToBW');links.new(rgb,bw.inputs[0]);lum=bw.outputs[0]
    hue_mask=math('MULTIPLY',math('GREATER_THAN',h,.48),math('LESS_THAN',h,.70))
    paint=math('MULTIPLY',hue_mask,clamp_ramp(s,.09,.23))
    paint=math('MULTIPLY',paint,clamp_ramp(lum,.035,.070))
    orm=node('ShaderNodeTexImage');orm.image=orm_image
    orm_sep=node('ShaderNodeSeparateColor');orm_sep.mode='RGB';links.new(orm.outputs['Color'],orm_sep.inputs[0])
    if key=='r3':
        paint=math('MULTIPLY',paint,clamp_ramp(orm_sep.outputs[1],.22,.48))
    colored=mix(paint,rgb,tint(lum,PALETTES[key]))
    bright=math('MULTIPLY',clamp_ramp(v,.24,.55),clamp_ramp(s,.25,.48))
    pale=math('MULTIPLY',clamp_ramp(v,.50,.75),clamp_ramp(s,.035,.12))
    accent=math('MULTIPLY',hue_mask,math('MAXIMUM',bright,pale))
    colored=mix(accent,colored,tint(lum,(2.20,.22,.13)))
    if position_mask:
        # Bake broad swept wing panels and two fuselage bands into the jet's UVs.
        # Original UVs and geometry are preserved; the color lives in the texture.
        coord=node('ShaderNodeTexCoord');sep=node('ShaderNodeSeparateXYZ');links.new(coord.outputs['Generated'],sep.inputs[0])
        x,y,z=sep.outputs['X'],sep.outputs['Y'],sep.outputs['Z']
        lateral=math('ABSOLUTE',math('SUBTRACT',x,.5))
        wings=math('MULTIPLY',math('GREATER_THAN',lateral,.19),math('LESS_THAN',y,.82))
        wings=math('MULTIPLY',wings,math('GREATER_THAN',y,.25))
        stripes=math('MULTIPLY',math('GREATER_THAN',lateral,.055),math('LESS_THAN',lateral,.105))
        stripes=math('MULTIPLY',stripes,math('GREATER_THAN',y,.12))
        stripes=math('MULTIPLY',stripes,math('LESS_THAN',y,.83))
        panel=math('MULTIPLY',math('MAXIMUM',wings,stripes),math('GREATER_THAN',z,.32))
        panel=math('MULTIPLY',panel,paint)
        colored=mix(panel,colored,tint(lum,(1.64,.57,.40)))
        cx=math('DIVIDE',lateral,.038)
        cy=math('DIVIDE',math('SUBTRACT',y,.305),.115)
        ellipse=math('ADD',math('MULTIPLY',cx,cx),math('MULTIPLY',cy,cy))
        canopy=math('MULTIPLY',math('LESS_THAN',ellipse,1),math('GREATER_THAN',z,.48))
        canopy=math('MULTIPLY',canopy,math('LESS_THAN',orm_sep.outputs[1],.62))
        colored=mix(canopy,colored,tint(lum,(.32,.52,.66)))
    emission=node('ShaderNodeEmission');links.new(colored,emission.inputs['Color']);emission.inputs['Strength'].default_value=1
    out=node('ShaderNodeOutputMaterial');links.new(emission.outputs[0],out.inputs['Surface'])
    return mat

def bake_texture(key,folder,source):
    # Use a UV plane for a lossless-layout material bake. J20 uses its original
    # dense mesh so the object-space markings cover the complete source UV map.
    bpy.ops.wm.read_factory_settings(use_empty=True)
    source=bpy.data.images.load(str(source),check_existing=False);source.colorspace_settings.name='sRGB'
    orm=bpy.data.images.load(str(folder/'textures'/f'{key}_metalrough_2k.png'),check_existing=False)
    orm.colorspace_settings.name='Non-Color'
    if key=='j20':
        bpy.ops.import_scene.gltf(filepath=CONFIG[key]['source'])
        obj=next(o for o in bpy.context.scene.objects if o.type=='MESH')
        activate(obj);bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    else:
        bpy.ops.mesh.primitive_plane_add(size=2)
        obj=bpy.context.object
    mat=material_graph(source,orm,key,position_mask=key=='j20')
    obj.data.materials.clear();obj.data.materials.append(mat)
    output=bpy.data.images.new(f'{key}_red_basecolor_2k',width=2048,height=2048,alpha=True)
    output.colorspace_settings.name='sRGB'
    target=mat.node_tree.nodes.new('ShaderNodeTexImage');target.image=output
    mat.node_tree.nodes.active=target;target.select=True
    scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=1
    scene.render.bake.margin=0 if key!='j20' else 8
    scene.render.bake.use_clear=True
    activate(obj);bpy.ops.object.bake(type='EMIT')
    path=folder/'textures'/f'{key}_red_basecolor_2k.png'
    output.filepath_raw=str(path);output.file_format='PNG';output.save()
    print('RED_TEXTURE_BAKED',key,flush=True)
    return path

def render_preview(obj,path):
    for o in bpy.context.scene.objects:
        if o.type=='MESH':o.hide_render=o!=obj
    obj.hide_set(False);obj.hide_render=False
    corners=[obj.matrix_world@Vector(v) for v in obj.bound_box]
    mn=Vector([min(v[i] for v in corners) for i in range(3)])
    mx=Vector([max(v[i] for v in corners) for i in range(3)])
    center=(mn+mx)*.5;extent=max(mx-mn)
    bpy.ops.object.camera_add(location=center+Vector((1.45,-1.9,1.15))*extent)
    camera=bpy.context.object;camera.rotation_euler=(center-camera.location).to_track_quat('-Z','Y').to_euler()
    camera.data.type='ORTHO';camera.data.ortho_scale=extent*1.3
    scene=bpy.context.scene;scene.camera=camera
    for loc,power,size in [((3,-4,5),650,4),((-3,-1,2),400,3),((0,4,4),800,3)]:
        bpy.ops.object.light_add(type='AREA',location=center+Vector(loc)*extent)
        light=bpy.context.object;light.data.energy=power*extent*extent;light.data.shape='DISK';light.data.size=size*extent
        light.rotation_euler=(center-light.location).to_track_quat('-Z','Y').to_euler()
    world=bpy.data.worlds.new('Red_Vehicle_Studio');world.use_nodes=True
    world.node_tree.nodes['Background'].inputs[0].default_value=(.23,.25,.28,1)
    world.node_tree.nodes['Background'].inputs[1].default_value=.5;scene.world=world
    scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=12;scene.cycles.use_denoising=True
    scene.render.resolution_x=640;scene.render.resolution_y=520;scene.render.resolution_percentage=100
    scene.view_settings.view_transform='AgX';scene.view_settings.look='AgX - Medium High Contrast'
    scene.view_settings.exposure=-.6
    scene.render.image_settings.file_format='PNG';scene.render.filepath=str(path)
    bpy.ops.render.render(write_still=True)

def main(key):
    cfg=CONFIG[key];folder=HERE/cfg['folder'];blue=folder/f'{key}-optimized.blend'
    blue_hash=digest(blue)
    texture=bake_texture(key,folder,folder/'textures'/f'{key}_basecolor_2k.png')
    bpy.ops.wm.open_mainfile(filepath=str(blue))
    meshes=sorted([o for o in bpy.context.scene.objects if o.type=='MESH'],key=lambda o:o.name)
    original_mat=meshes[0].data.materials[0]
    # The generator's specular extension is above the normal PBR range and
    # washes out paint. Use the same clean Principled setup as the A40 asset.
    red=bpy.data.materials.new(key.upper()+'_Red_PBR');red.use_nodes=True
    nodes=red.node_tree.nodes;nodes.clear();links=red.node_tree.links
    bsdf=nodes.new('ShaderNodeBsdfPrincipled');bsdf.inputs['Specular IOR Level'].default_value=.35
    output=nodes.new('ShaderNodeOutputMaterial');links.new(bsdf.outputs['BSDF'],output.inputs['Surface'])
    image=bpy.data.images.load(str(texture),check_existing=False);image.colorspace_settings.name='sRGB';image.pack()
    base=nodes.new('ShaderNodeTexImage');base.image=image;links.new(base.outputs['Color'],bsdf.inputs['Base Color'])
    orm=nodes.new('ShaderNodeTexImage');orm.image=next(n.image for n in original_mat.node_tree.nodes if n.type=='TEX_IMAGE' and n.image and 'metalrough' in n.image.name)
    orm.image.colorspace_settings.name='Non-Color';sep=nodes.new('ShaderNodeSeparateColor');sep.mode='RGB'
    links.new(orm.outputs['Color'],sep.inputs[0]);links.new(sep.outputs[1],bsdf.inputs['Roughness']);links.new(sep.outputs[2],bsdf.inputs['Metallic'])
    norm=nodes.new('ShaderNodeTexImage');norm.image=next(n.image for n in original_mat.node_tree.nodes if n.type=='TEX_IMAGE' and n.image and 'normal' in n.image.name)
    norm.image.colorspace_settings.name='Non-Color';nm=nodes.new('ShaderNodeNormalMap');nm.inputs['Strength'].default_value=.65
    links.new(norm.outputs['Color'],nm.inputs['Color']);links.new(nm.outputs['Normal'],bsdf.inputs['Normal'])
    exports=[];lod0=None
    for obj in meshes:
        lod=int(obj.name.rsplit('LOD',1)[1]);obj.hide_set(False);obj.hide_render=False
        obj.data.materials.clear();obj.data.materials.append(red)
        obj.name=cfg['title'].replace(' ','_')+f'_RED_LOD{lod}'
        export(obj,folder/'exports'/f'{key}-red-lod{lod}')
        obj.data.calc_loop_triangles()
        exports.append(dict(lod=lod,triangles=len(obj.data.loop_triangles),
            glb=f'exports/{key}-red-lod{lod}.glb',fbx=f'exports/{key}-red-lod{lod}.fbx'))
        if lod==0:lod0=obj
    for obj in meshes:obj.hide_render=obj!=lod0;obj.hide_set(obj!=lod0)
    activate(lod0);bpy.ops.outliner.orphans_purge(do_recursive=True)
    blend=folder/f'{key}-red.blend'
    bpy.ops.wm.save_as_mainfile(filepath=str(blend))
    report=dict(asset=key,team='Red',blender=bpy.app.version_string,blue_blend_sha256=blue_hash,
        texture=str(texture.relative_to(folder)),palette_linear=PALETTES[key],
        geometry='Existing optimized mesh, normals and UVs reused without modification',exports=exports,
        scope='Static paint variant; no new articulation or Unity integration')
    (folder/'red_build.json').write_text(json.dumps(report,indent=2))
    evidence=folder/'red-preview';evidence.mkdir(exist_ok=True)
    render_preview(lod0,evidence/'perspective.png')
    print('RED_VARIANT_COMPLETE',key,flush=True)

if __name__=='__main__':
    for key in sys.argv[sys.argv.index('--')+1:]:main(key)
