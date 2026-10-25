"""Confirm export payloads, publish local indexes and package the red variants."""
from pathlib import Path
import hashlib,json,struct,zipfile

ROOT=Path(__file__).resolve().parent.parent
OUT=Path(__file__).resolve().parent
ASSETS=[('t90','t90-bastion','T90 主战坦克'),('v12','v12-lancer','V12 八轮步战车'),
        ('r4','r4-scout','R4 装甲侦察车'),('u8','u8-rover','U8 武装运输车'),
        ('m2','m2-courier','M2 双座摩托车'),('r3','r3-firefly','R3 侦察直升机'),
        ('j20','j20-stratus','J20 喷气机')]

def sha(data):return hashlib.sha256(data).hexdigest()
def glb(path):
    raw=path.read_bytes();assert raw[:4]==b'glTF'
    pos=12;doc=None;binary=None
    while pos<len(raw):
        length,kind=struct.unpack_from('<II',raw,pos);pos+=8
        if kind==0x4e4f534a:doc=json.loads(raw[pos:pos+length])
        if kind==0x004e4942:binary=raw[pos:pos+length]
        pos+=length
    return doc,binary
def accessor_hash(doc,binary,index):
    a=doc['accessors'][index];view=doc['bufferViews'][a['bufferView']]
    size={5120:1,5121:1,5122:2,5123:2,5125:4,5126:4}[a['componentType']]*{'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4,'MAT4':16}[a['type']]
    start=view.get('byteOffset',0)+a.get('byteOffset',0);stride=view.get('byteStride',size)
    data=b''.join(binary[start+i*stride:start+i*stride+size] for i in range(a['count']))
    return (a['count'],a['componentType'],a['type'],sha(data))
def image_bytes(doc,binary,index):
    view=doc['bufferViews'][doc['images'][index]['bufferView']]
    start=view.get('byteOffset',0);return binary[start:start+view['byteLength']]

records=[]
for key,folder,title in ASSETS:
    root=ROOT/folder
    build=json.loads((root/'red_build.json').read_text())
    assert sha((root/f'{key}-optimized.blend').read_bytes())==build['blue_blend_sha256']
    lods=[]
    for lod in range(3):
        blue=root/'exports'/f'{key}-lod{lod}.glb';red=root/'exports'/f'{key}-red-lod{lod}.glb'
        a,ab=glb(blue);b,bb=glb(red)
        pa=a['meshes'][0]['primitives'][0];pb=b['meshes'][0]['primitives'][0]
        assert pa['attributes'].keys()==pb['attributes'].keys()
        for name in pa['attributes']:
            assert accessor_hash(a,ab,pa['attributes'][name])==accessor_hash(b,bb,pb['attributes'][name]),(key,lod,name)
        assert accessor_hash(a,ab,pa['indices'])==accessor_hash(b,bb,pb['indices']),(key,lod,'indices')
        ma=a['materials'][pa['material']];mb=b['materials'][pb['material']]
        ta=a['textures'][ma['pbrMetallicRoughness']['baseColorTexture']['index']]['source']
        tb=b['textures'][mb['pbrMetallicRoughness']['baseColorTexture']['index']]['source']
        color=image_bytes(b,bb,tb)
        assert color[:8]==b'\x89PNG\r\n\x1a\n' and struct.unpack('>II',color[16:24])==(2048,2048)
        assert sha(image_bytes(a,ab,ta))!=sha(color),(key,lod,'unchanged paint')
        fbx=root/'exports'/f'{key}-red-lod{lod}.fbx';assert fbx.stat().st_size>0
        lods.append({'lod':lod,'triangles':b['accessors'][pb['indices']]['count']//3,
            'glb':str(red.relative_to(ROOT)),'fbx':str(fbx.relative_to(ROOT)),
            'glb_sha256':sha(red.read_bytes()),'glb_bytes':red.stat().st_size,
            'geometry_normals_uv_indices_match_blue':True,'red_basecolor_2k':True})
    record={'key':key,'title':title,'blend':f'{folder}/{key}-red.blend','exports':lods,
        'preview':f'{folder}/red-preview/perspective.png','blue_source_unchanged':True}
    records.append(record)
    p=root/'README.md';old=p.read_text();marker='## 红队涂装（2026-10-23）'
    if marker not in old:
        lines=[marker,'','已完成红队静态材质，使用原模型三档网格、法线和 UV。',
            '','| 版本 | 三角面 | GLB | FBX |','| --- | ---: | --- | --- |']
        for item in lods:
            l=item['lod'];lines.append(f'| LOD{l} | {item["triangles"]:,} | [GLB](exports/{key}-red-lod{l}.glb) | [FBX](exports/{key}-red-lod{l}.fbx) |')
        lines += ['',f'[红队 Blender 工程]({key}-red.blend) · [涂装预览](red-preview/perspective.png) · [生成脚本](../create_red_variants.py)',
            '',f'红队 Base Color：`textures/{key}_red_basecolor_2k.png`；法线与金属/粗糙度贴图共用现有资源。FBX 使用时保留同级 textures 目录。',
            '','七款均为静态模型；活动部件、真实尺寸校准和 Unity 接入尚未制作。','']
        first,rest=old.split('\n',1);p.write_text(first+'\n\n'+'\n'.join(lines)+'\n'+rest)

(OUT/'delivery_manifest.json').write_text(json.dumps({'assets':records,
    'export_confirmation':'21 GLBs retain exactly the blue mesh attributes and indices; each embeds a changed 2K base color. FBX files exported; no renewed full model quality audit.',
    'scope':'Static red material variants; no Unity integration'},indent=2,ensure_ascii=False))
section='''## 2026-10-23：七款红队涂装已完成

T90、V12、R4、U8、M2、R3、J20 已有红队三档 GLB/FBX、可编辑 Blender 工程和 2K Base Color。加上先前的 H8/A40，九款载具现在都具备蓝、红两队外观。

本次按现有红队参考图制作灰棕装甲、红色标识、砖红航空涂装。复用原网格与 UV；保留 PBR 贴图，并调整不正常的源材质高光。已确认 21 个 GLB 的几何、法线、UV 与对应蓝队完全一致，且写入红队 2K 贴图。此为定向导出确认，没有重做完整模型质量检查。

[全部七款红队 GLB 下载](red-variants-2026-10-23/seven-red-vehicles-glb.zip) · [涂装总览](red-variants-2026-10-23/preview.png) · [交付清单](red-variants-2026-10-23/delivery_manifest.json) · [生成脚本](create_red_variants.py)

| 车型 | 文件与说明 |
| --- | --- |
'''
for key,folder,title in ASSETS:section+=f'| {title} | [{folder}]({folder}/README.md) |\n'
section+='\n这七款仍是静态优化版，活动部件、真实尺寸校准和 Unity 接入尚未制作。\n\n'
p=ROOT/'README.md';text=p.read_text()
if '## 2026-10-23：七款红队涂装已完成' not in text:
    text=text.replace('# IRONFRONT 载具资产\n\n','# IRONFRONT 载具资产\n\n'+section,1)
p.write_text(text)
with zipfile.ZipFile(OUT/'seven-red-vehicles-glb.zip','w',compression=zipfile.ZIP_STORED) as archive:
    for item in records:
        for lod in item['exports']:
            p=ROOT/lod['glb'];archive.write(p,f'{item["key"].upper()}/{p.name}')
    archive.write(OUT/'delivery_manifest.json','delivery_manifest.json')
    if (OUT/'preview.png').exists():archive.write(OUT/'preview.png','preview.png')
(OUT/'work_state.md').write_text('''# 红队涂装完成

七款已完成：T90、V12、R4、U8、M2、R3、J20。
21 个 GLB、21 个 FBX、7 个 Blender 工程、7 张 2K Base Color 与材质预览。
文件入口见上级 README.md，交付清单见 delivery_manifest.json。
几何、法线、UV 及索引与对应蓝队导出完全一致；原蓝队工程未覆盖。
没有待执行的构建作业。七款当前为静态版本，尚未制作活动部件与 Unity 接入。
''')
print('COMPLETE',len(records),'vehicles',sum(len(r['exports']) for r in records),'red GLBs')
print('ZIP_MIB',round((OUT/'seven-red-vehicles-glb.zip').stat().st_size/1024/1024,2))
