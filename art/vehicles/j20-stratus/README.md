> 0.56.0 更新：已完成可动分件及红蓝 Unity 替换。新工程为 [articulated/j20-articulated.blend](articulated/j20-articulated.blend)，三档 FBX 在 articulated/exports，测量在 articulated/build.json；原静态文件保持不变。详见 [统一交接](../../../docs/UNITY_HANDOFF_056.md)。

# J20 STRATUS 喷气机：模型优化版

## 红队涂装（2026-10-23）

已完成红队静态材质，使用原模型三档网格、法线和 UV。

| 版本 | 三角面 | GLB | FBX |
| --- | ---: | --- | --- |
| LOD0 | 40,000 | [GLB](exports/j20-red-lod0.glb) | [FBX](exports/j20-red-lod0.fbx) |
| LOD1 | 20,000 | [GLB](exports/j20-red-lod1.glb) | [FBX](exports/j20-red-lod1.fbx) |
| LOD2 | 10,000 | [GLB](exports/j20-red-lod2.glb) | [FBX](exports/j20-red-lod2.fbx) |

[红队 Blender 工程](j20-red.blend) · [涂装预览](red-preview/perspective.png) · [生成脚本](../create_red_variants.py)

红队 Base Color：`textures/j20_red_basecolor_2k.png`；法线与金属/粗糙度贴图共用现有资源。FBX 使用时保留同级 textures 目录。

七款均为静态模型；活动部件、真实尺寸校准和 Unity 接入尚未制作。


日期：2026-10-16。已检查项目资产、已有优化目录和源文件记录，未发现此模型的先前优化版本，因此从本次上传的 `e500d16c7909d01d470ae8ad618285a5.glb` 制作。原始文件未修改，SHA-256 见 `build_metrics.json`。

## 结果

原始 **1,499,582 三角面**、3 张 4096² 贴图，约 **78.20 MiB**。保留原模型外形、比例、朝向、淡蓝灰涂装和 UV；输出三档 LOD 与 2048² PBR 贴图。

| 档位 | 三角面 | 自带贴图 GLB 大小 | 文件 |
| --- | ---: | ---: | --- |
| LOD0 | 40,000 | 12.17 MiB | [GLB](exports/j20-lod0.glb) · [FBX](exports/j20-lod0.fbx) |
| LOD1 | 20,000 | 11.60 MiB | [GLB](exports/j20-lod1.glb) · [FBX](exports/j20-lod1.fbx) |
| LOD2 | 10,000 | 11.26 MiB | [GLB](exports/j20-lod2.glb) · [FBX](exports/j20-lod2.fbx) |

- [可编辑 Blender 工程](j20-optimized.blend)
- [生成脚本](../optimize_new_vehicles.py)
- [制作指标](build_metrics.json) · [最终导出检查](validation.json) · [独立 LOD0 检查](inspection-lod0.json)
- [近景透视预览](evidence/lod0/perspective.png) · [近景六视图](evidence/lod0/contact_sheet.png) · [远景 LOD2 预览](evidence/lod2/contact_sheet.png)

## 处理与验证

减面前合并重合的贴图接缝顶点，保留逐角 UV，再让每个 LOD 从完整高模独立简化。R4 的门板和中远景喷气机使用重新计算的面积加权法线，修复继承高模自定义法线造成的三角阴影。贴图缩小后从 PNG 重新加载并打包，避免 GLB 携带旧 4K 数据。

三个 GLB 和三个 FBX 均在新 Blender 进程回读，三角面与制作指标一致，顶点和变换有效，无退化面；合并重合位置后的几何开放边为 0。贴图均为 2048²。普通索引网格检查仍会显示 UV/法线拆点形成的边界，应结合焊接后的几何指标理解。已经打开检查近景六视图和远景预览，LOD2 仅供远距离使用，小附件与轮毂细节更简化。

## 使用范围

这是**静态网格优化版**，保持源文件的单个 Mesh 和单个材质，没有独立的车轮、机枪、起落架或舵面绑定。保留源模型归一化尺寸，没有设定真实米制长度；本次未替换 Unity 游戏内载具。后续接入时需要配置实际尺寸、可动分件、碰撞体和 LODGroup。原模型的生成细节与局部不对称仍保留，未做手工重新拓扑。

GLB 自带材质和贴图，可直接用于预览；FBX 使用同目录上级 `textures/` 的 PNG。金属粗糙度贴图遵循 glTF 的 **G=roughness、B=metallic**，在 Unity URP 中需转换为 **R=metallic、A=1−roughness**。法线图应以 Normal Map 导入。

## 复现

在项目 `art/vehicles` 目录运行：

```sh
/Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --python optimize_new_vehicles.py -- j20
/Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --python validate_new_vehicles.py
```

检查脚本会同时验证 J20 与 R4 的全部 12 个导出。生成脚本依赖 Downloads 中的原始 GLB 路径。
