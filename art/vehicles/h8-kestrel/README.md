# H8 Kestrel：双阵营 Unity 载具视觉资产

由用户提供的蓝队 H8 高模 GLB 制成。保留装甲机首、驾驶舱、双涡轮、短翼武器、滑橇与长尾梁；原六叶主旋翼改为四叶，并拆出可驱动的主旋翼、尾旋翼、机首炮和翼下挂载。蓝红两队共用几何，分别使用钢蓝灰和灰陶红贴图。

## 交付文件

| 文件 | 用途 |
| --- | --- |
| `h8-kestrel.blend` | 可编辑 Blender 工程，内含三个 LOD 与两队材质 |
| `build_h8.py` | 从原始 GLB 重建上述工程与导出的确定性脚本 |
| `blue-h8-lod0/1/2.fbx`、`red-h8-lod0/1/2.fbx` | Unity 直接导入，几何与贴图分离 |
| `blue-h8-lod0/1/2.glb`、`red-h8-lod0/1/2.glb` | 自包含预览与其他 3D 管线交换 |
| `textures/h8_blue_albedo_2k.png`、`textures/h8_red_albedo_2k.png` | 两队 2048² base color |
| `textures/h8_normal_2k.png`、`textures/h8_orm_2k.png` | 共用 2048² 法线和 metallic-roughness；ORM 的 G=roughness，B=metallic |
| `build_metrics.json`、`evidence/` | 面数、枢轴、独立导入与多视图验收数据 |

Blender FBX 导出为 `-Z forward / Y up`、单位米、global scale 1。Unity Prefab 导入已经核对约 **12.76 m 宽 × 8.18 m 高 × 19.20 m 长**，机首指向 Unity **+Z**。LOD0/1/2 中语义节点保持同名：`H8_Root`、`Body`、`MainRotor`、`TailRotor`、`ChinGun`、`PortWeaponPod`、`StarboardWeaponPod`。`MainRotor` 与 `TailRotor` 是实际轴心的空对象，旋翼网格为它们的子对象。

材质基调（以中性光下的贴图色为近似，不含强高光）：蓝队深钢蓝灰约 `#35495E`–`#77899B`，红队低饱和灰陶红约 `#5B4745`–`#916E69`；玻璃和枪械保留更深的冷灰。原贴图局部白色机翼与灯光高光会超出这些区间。

## Unity 坐标与驱动

| 部位 | Blender 世界坐标，米（Z up、-Y 前方） | Unity 坐标，米（Y up、+Z 前方） | 驱动 |
| --- | --- | --- | --- |
| `MainRotor` | `(0, -0.253, 6.976)` | `(0, 6.976, 0.253)` | Blender 本地 Z / Unity 本地 Y；四叶直径 13.4 m |
| `TailRotor` | `(0.472, 9.571, 6.892)` | `(0.472, 6.892, -9.571)` | 两端本地 X 轴 |
| `ChinGun` | `(0, -6.454, 1.432)` | `(0, 1.432, 6.454)` | 两端本地 X 轴俯仰；枪口前端 Unity 约 `(0, 1.43, 8.385)` |
| `PortWeaponPod` | `(-5.055, -0.421, 2.106)` | `(-5.055, 2.106, 0.421)` | 独立武器挂载对象 |
| `StarboardWeaponPod` | `(5.055, -0.421, 2.106)` | `(5.055, 2.106, 0.421)` | 独立武器挂载对象 |

整个资产范围（Unity XYZ）：X `[-6.386, 6.377]`、Y `[0.001, 8.178]`、Z `[-10.826, 8.379]` m。已调整游戏中的机体碰撞、座位镜头和枪口。可见机首炮负责原有主武器；游戏中独立的乘员腹部机枪保留原型外观，安置在机腹。

FBX 材质名称为 `H8_Blue_PBR` 或 `H8_Red_PBR`，新旋翼为 `H8_RotorGraphite`。FBX 自动导入能找到 albedo 和 normal；其导出格式不会自动连接 glTF 式 G/B ORM 图，因此 Unity 需单独把 `h8_orm_2k.png` 的 B 装入金属度、把 `1-G` 装入光滑度。原高模的玻璃绘在不透明 PBR 图集中，没有独立透明材质。

## 质量和限制

| LOD | 三角面 | 用途 |
| --- | ---: | --- |
| LOD0 | 51,009 | 近景游戏视觉 |
| LOD1 | 25,656 | 中距离 |
| LOD2 | 20,587 | 远距离；从 LOD0 直接简化以保护薄壳 |

原模型约 1,499,888 三角面、4096² 贴图。源 GLB 因贴图/法线接缝拆开了顶点；原始直升机在合并重合顶点后开放边为 0。最终流程先按部位分件，焊接重复位置并保留逐角 UV，再减面，修复了早期版本在 Unity 中的大面积针孔。未进行逐面手工重新拓扑，近看仍有生成模型的软化和不规则细节。

最终 LOD0 在几何焊接后仍有 6,289 条开放边，主要涉及分件和移除旧旋翼后的边界；不能据此宣称完全封闭。使用简化碰撞体，不用可见网格承担精确碰撞。GLB/FBX 的三角面、尺寸配对一致（误差小于 1 mm），没有无效坐标或退化面。最终报告以 `../final-validation/summary.json` 为准；早期 `*inspect.json`、`*validation.json` 是修复前的留档。

Unity 6.5 实际 SampleScene Play Mode 已验证蓝红预制体、3 档 LOD 配置、URP 材质、旋翼转动、碰撞体和枪口/镜头位置。已检查两队实拍和洋红背景透孔检查图，位于 `../unity-preview/`。本次没有做独立发行包或完整多人战斗性能测试。

Unity 导入由 `VehicleArtAssetSetup.BuildH8()` 管理：H8 的 FBX Scale Factor 为 **100**（启用 Use File Scale），关闭 Bake Axis Conversion；预制体源根节点旋转 X **-90°**，活动部件重新挂在轴向中性的驱动节点下。请优先复用生成的 `Assets/Resources/Vehicles/H8_Blue.prefab`、`H8_Red.prefab`。

蓝队 [LOD0 透视](evidence/blue-lod0/perspective.png) · [六视图](evidence/blue-lod0/contact_sheet.png) · 红队 [LOD0 透视](evidence/red-lod0/perspective.png) · 蓝队 [LOD2 透视](evidence/blue-lod2/perspective.png)。

复现依赖原始高模仍位于 `/Users/liuwenjun/Downloads/33717aab9aa8b9b3fb2bb108d94498cb.glb`；原文件未修改。可直接在 Blender 5.2.2 运行 `build_h8.py`；macOS 受限沙箱内 Blender 的 Metal 初始化会崩溃，本次在获授权的沙箱外执行。
