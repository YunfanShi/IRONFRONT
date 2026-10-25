# T90 BASTION 主战坦克

## 2026-10-23：可动版本已接入 Unity（0.55.0）

[可动 Blender 工程](articulated/t90-articulated.blend) · [三档 FBX](articulated/exports/) · [可复现脚本](../build_articulated_armor.py) · [静止预览](articulated/rest-front.png) · [转动预览](articulated/aim-rear.png)

炮塔偏航、炮管俯仰和后坐；履带保持已有固定外观。 红蓝 Unity 预制体位于 `Assets/Resources/Vehicles/T90_Blue.prefab`、`T90_Red.prefab`。原静态 `.blend`、GLB/FBX 与贴图保留；下方记录为本次可动适配前的历史交付状态。

[完整接入说明与验证](../../../docs/UNITY_HANDOFF_055.md) · [分件构建数据](articulated/build.json)

## 红队涂装（2026-10-23）

已完成红队静态材质，使用原模型三档网格、法线和 UV。

| 版本 | 三角面 | GLB | FBX |
| --- | ---: | --- | --- |
| LOD0 | 80,000 | [GLB](exports/t90-red-lod0.glb) | [FBX](exports/t90-red-lod0.fbx) |
| LOD1 | 40,000 | [GLB](exports/t90-red-lod1.glb) | [FBX](exports/t90-red-lod1.fbx) |
| LOD2 | 20,000 | [GLB](exports/t90-red-lod2.glb) | [FBX](exports/t90-red-lod2.fbx) |

[红队 Blender 工程](t90-red.blend) · [涂装预览](red-preview/perspective.png) · [生成脚本](../create_red_variants.py)

红队 Base Color：`textures/t90_red_basecolor_2k.png`；法线与金属/粗糙度贴图共用现有资源。FBX 使用时保留同级 textures 目录。

七款均为静态模型；活动部件、真实尺寸校准和 Unity 接入尚未制作。


2026-10-23：由用户提供的混元 GLB 直接制作静态优化版，保留原比例、涂装、UV、方向和归一化尺寸。

| 版本 | 三角面 | GLB | FBX |
| --- | ---: | --- | --- |
| LOD0 | 80,000 | [t90-lod0.glb](exports/t90-lod0.glb) | [t90-lod0.fbx](exports/t90-lod0.fbx) |
| LOD1 | 40,000 | [t90-lod1.glb](exports/t90-lod1.glb) | [t90-lod1.fbx](exports/t90-lod1.fbx) |
| LOD2 | 20,000 | [t90-lod2.glb](exports/t90-lod2.glb) | [t90-lod2.fbx](exports/t90-lod2.fbx) |

- [可编辑 Blender 工程](t90-optimized.blend)：包含三档 LOD，默认仅显示 LOD0，贴图已打包。
- `textures/`：2K Base Color、Normal、Metallic/Roughness PNG。使用 FBX 时请保留同级目录结构。
- [构建数据](build_metrics.json)；[可复现脚本](../optimize_new_vehicles.py)。
- 源文件：`/Users/liuwenjun/Downloads/c9fd2d8fd8c3ec5cfbcfff869985a9b8.glb`，原文件未覆盖。

处理：先焊接 glTF 重复接缝顶点，再从原网格分别生成三档 LOD，重新计算减面后法线，并保存真正的 2K 贴图数据。

范围：静态单 Mesh，尚未拆分活动部件、标定真实尺寸或接入 Unity。红队涂装已补齐，见上方交付。依照本次用户要求，未进行单独质量评审、完整回读检查或多视图验收；构建时保留基本导出断言。
