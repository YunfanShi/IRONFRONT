"""Build delivery indexes and package after all five Blender jobs complete."""
from pathlib import Path
import json
import zipfile

HERE=Path(__file__).resolve().parent
ROOT=HERE.parent
ASSETS=[('t90','t90-bastion','T90 BASTION 主战坦克'),
        ('v12','v12-lancer','V12 LANCER 八轮步战车'),
        ('u8','u8-rover','U8 ROVER 武装运输车'),
        ('m2','m2-courier','M2 COURIER 双座摩托车'),
        ('r3','r3-firefly','R3 FIREFLY 侦察直升机')]
rows=[]
manifest=[]
for key,folder,title in ASSETS:
    root=ROOT/folder
    build=json.loads((root/'build_metrics.json').read_text())
    tris=[m['triangles'] for m in build['authored_lods']]
    files=[]
    for lod in range(3):
        for ext in ['glb','fbx']:
            p=root/'exports'/f'{key}-lod{lod}.{ext}'
            files.append({'file':str(p.relative_to(ROOT)),'bytes':p.stat().st_size})
    blend=root/f'{key}-optimized.blend'
    files.append({'file':str(blend.relative_to(ROOT)),'bytes':blend.stat().st_size})
    size=(root/'exports'/f'{key}-lod0.glb').stat().st_size/1024/1024
    manifest.append({'key':key,'title':title,'source':build['source'],
        'source_sha256':build['source_sha256'],'original_triangles':build['original']['triangles'],
        'lod_triangles':tris,'lod0_mib':round(size,2),'files':files,
        'status':'exported','independent_quality_review':'omitted at user request'})
    rows.append(f'| [{title}]({folder}/README.md) | {build["original"]["triangles"]:,} | {tris[0]:,} | {tris[1]:,} | {tris[2]:,} | {size:.2f} MiB |')
    text=f'''# {title}

2026-10-23：由用户提供的混元 GLB 直接制作静态优化版，保留原比例、涂装、UV、方向和归一化尺寸。

| 版本 | 三角面 | GLB | FBX |
| --- | ---: | --- | --- |
'''
    for lod,tri in enumerate(tris):
        text+=f'| LOD{lod} | {tri:,} | [{key}-lod{lod}.glb](exports/{key}-lod{lod}.glb) | [{key}-lod{lod}.fbx](exports/{key}-lod{lod}.fbx) |\n'
    text+=f'''
- [可编辑 Blender 工程]({key}-optimized.blend)：包含三档 LOD，默认仅显示 LOD0，贴图已打包。
- `textures/`：2K Base Color、Normal、Metallic/Roughness PNG。使用 FBX 时请保留同级目录结构。
- [构建数据](build_metrics.json)；[可复现脚本](../optimize_new_vehicles.py)。
- 源文件：`{build['source']}`，原文件未覆盖。

处理：先焊接 glTF 重复接缝顶点，再从原网格分别生成三档 LOD，重新计算减面后法线，并保存真正的 2K 贴图数据。

范围：静态单 Mesh，尚未拆分活动部件、制作红队涂装、标定真实尺寸或接入 Unity。依照本次用户要求，未进行单独质量评审、完整回读检查或多视图验收；构建时保留基本导出断言。
'''
    (root/'README.md').write_text(text)

section='''## 2026-10-23：剩余五款载具静态优化

T90、V12、U8、M2、R3 已导出三档 LOD、2K PBR、GLB/FBX 和 Blender 工程。

| 载具及文件入口 | 原始三角面 | LOD0 | LOD1 | LOD2 | LOD0 GLB |
| --- | ---: | ---: | ---: | ---: | ---: |
'''+ '\n'.join(rows)+'''

本批按用户要求直接处理，未进行单独质量评审或完整回读验收。保留源模型外观和归一化尺寸，目前为静态优化版；可动分件、红队涂装、真实尺寸校准与 Unity 接入尚未制作。

[全部 15 个 GLB 打包下载](batch-2026-10-23/remaining-five-vehicles-glb.zip) · [交付清单](batch-2026-10-23/delivery_manifest.json)

'''
index=ROOT/'README.md'
old=index.read_text()
header='# IRONFRONT 载具资产\n\n'
if '## 2026-10-23：剩余五款载具静态优化' not in old:
    index.write_text(old.replace(header,header+section,1))
(HERE/'delivery_manifest.json').write_text(json.dumps(manifest,indent=2,ensure_ascii=False))
archive=HERE/'remaining-five-vehicles-glb.zip'
with zipfile.ZipFile(archive,'w',compression=zipfile.ZIP_STORED) as z:
    for key,folder,title in ASSETS:
        for lod in range(3):
            p=ROOT/folder/'exports'/f'{key}-lod{lod}.glb'
            z.write(p,f'{key.upper()}/{p.name}')
    z.write(HERE/'delivery_manifest.json','delivery_manifest.json')
(HERE/'work_state.md').write_text('''# 2026-10-23 载具批处理

五款已全部完成静态优化和导出，交付文件见 delivery_manifest.json 与上级 README.md。
三档 LOD、2K PBR、15 个 GLB、15 个 FBX、5 个 Blender 工程已生成，GLB 包已保存。
原下载文件未覆盖。按用户要求未执行独立质量检查、多视图验收或 Unity 测试。
活动部件、红队涂装、真实尺寸校准与 Unity 接入不在此次静态优化范围内。
没有待处理的构建作业。
''')
print(json.dumps(manifest,indent=2,ensure_ascii=False))
print('DELIVERY_ARCHIVE',archive,'MiB',round(archive.stat().st_size/1024/1024,2))
