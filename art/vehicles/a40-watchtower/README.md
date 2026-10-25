# A40 Watchtower：IRONFRONT 载具美术资产

此资产由用户提供的混元 3D A40 GLB 制作，保留原模型的八轮车体、四联炮、雷达、装甲面板和细节，同时将原本单一高模拆成可独立转动的刚体部件。蓝队和红队使用同一套几何与 UV，按参考图分别提供蓝灰、灰棕涂装。

## 交付文件

| 文件 | 用途 |
| --- | --- |
| `A40_WATCHTOWER.blend` | 可编辑的三档 LOD Blender 工程 |
| `exports/A40_WATCHTOWER_LOD0/1/2.fbx` | Unity 主导入格式；各档相同的可动层级 |
| `exports/A40_WATCHTOWER_LOD0/1/2.glb` | 自带贴图的通用预览与交换格式 |
| `exports/A40_WATCHTOWER_RED_LOD0.fbx/.glb` | 红队近景涂装；几何与蓝队 LOD0 完全共用 |
| `textures/A40_Blue_basecolor_2K.png` | 蓝队 2048² 基础色 |
| `textures/A40_Red_basecolor_2K.png` | 红队 2048² 基础色 |
| `textures/A40_normal_2K.png` | 两队共用 2048² 法线 |
| `textures/A40_metalrough_2K.png` | 两队共用 2048² 金属度与粗糙度 |
| `build_a40.py` | 从原始 GLB 重新制作全部文件的 Blender Python 脚本 |
| `build_metrics.json` | 面数、场景边界、层级和各可动轴心的实测坐标 |
| `renders/` | 蓝红五视图、八轮转动和炮塔姿态的渲染证据 |

蓝队 LOD1/2 也可供红队使用：复用其 FBX 网格，将材质基础色替换为红队贴图即可，无需复制两套中远景网格。

## LOD 与尺寸

| 档位 | 三角面 | Mesh 数 | 场景尺寸约值 |
| --- | ---: | ---: | --- |
| LOD0 | 87,937 | 15 | 4.700 × 7.100 × 3.876 m |
| LOD1 | 52,751 | 15 | 4.698 × 7.099 × 3.876 m |
| LOD2 | 26,372 | 15 | 4.698 × 7.098 × 3.874 m |

原始文件约 149.8 万三角面。最终流程在减面前焊接 GLB 的重复接缝顶点，并保留逐角 UV，消除了早期直接减面造成的大面积针孔。LOD0 保留约 8.8 万面维护近景细节；三档共用 2K 贴图，Unity 按视距切换。LOD0 全车最低点为 0 m，LOD2 最低点约 2 mm。

## Unity 导入和可动结构

- FBX 导出参数：Forward **-Z**、Up **Y**、Scale **1**。Unity 中 Scale Factor **1**、启用 Use File Scale、关闭 Bake Axis Conversion；生成预制体的源根节点额外旋转 X **-90°**，活动部件挂在轴向中性的驱动节点下。车头朝 Unity **+Z**，车顶朝 **+Y**，尺寸单位为米。请运行 `VehicleArtAssetSetup.BuildA40()` 或直接使用 `Assets/Resources/Vehicles/A40_Blue.prefab` 与 `A40_Red.prefab`。
- 每档单一材质槽。蓝队槽名 `A40_Blue_PBR`；红队 LOD0 为 `A40_Red_PBR`。Unity URP Lit 应分别创建蓝、红材质并绑定对应 BaseColor。法线贴图按 Normal Map 导入。`metalrough` 遵循 glTF 约定：**G = roughness、B = metallic**；URP Lit 使用时须转换为 R = metallic、A = smoothness (= 1 − roughness) 的 MetallicGlossMap，或在自定义 Shader 中直接取 G/B。
- 根节点为 `A40_LOD0/1/2`。其下 `Hull`、`Turret` 和八个 `Wheel_*`；`Turret` 下有 `TurretArmor`、`Barrels`、`Radar`；四根 `Barrel_*` 及 `Muzzle_LeftLower` 在 `Barrels` 下，`RadarArray` 在 `Radar` 下。LOD1/2 对 mesh 加 `_LOD1/2` 后缀，对空节点加 Blender `.001/.002` 后缀；可按 `Turret`、`Barrels`、`Radar`、`Wheel_` 前缀查找。
- 轴心以 Unity (X,Y,Z) 米计：炮塔 **(0, 2.00, -0.10)**；四炮共同俯仰 **(0, 2.48, -0.10)**；雷达 **(0.25, 3.12, -1.17)**；左下炮口 **(-0.575, 2.235, 2.593)**。炮塔绕 Y 偏航，炮管绕本地 X 俯仰，雷达绕本地 Y 转向。
- 八个轮子均有独立轮心、本地 X 转轴和 `Wheel_` 前缀。轴心 Y 均为 **0.65 m**；前后四排 Z = **+2.178、+0.705、-0.705、-2.178 m**，左右轮心 X 约 **±1.56 至 ±1.63 m**。轮胎测得半径约 **0.67–0.71 m**；运行时转动可用 **0.684 m** 的速度换算半径。每个精确坐标见 `build_metrics.json`。
- 文件不含预制动画 clip，以上部件供游戏运行时控制。乘员座位沿用游戏逻辑中的车辆偏移：驾驶员 (-0.65,0.6,0.85)，炮手 (0.77,1.6,-0.6)，后排两个 (-0.75,0.95,-1.55)、(0.75,0.95,-1.55)；它们并非模型中额外导出的节点。

## 验证与限制

Blender 5.2.2 对最终全部 **8 个导出文件**进行了新鲜导入核验；与 H8 的 12 个导出共同记录在 `../final-validation/summary.json`。GLB/FBX 的三角面、尺寸与层级匹配；没有无效坐标或退化面。贴图在外置 PNG 和内嵌 GLB 中均为 2048²。根目录旧 `qa_*.json` 为接缝修复前留档，应以新的统一报告为准。

模型经过语义分件后仍有开放边，LOD0 在合并重合点后的边界计数为 13,155。未做手工重新拓扑；近看部分装甲细节仍偏软，适合作为游戏刚体可见模型，碰撞使用项目简化碰撞体。

Unity 6.5 的实际 SampleScene Play Mode 已验证双方预制体、LOD 配置、URP 材质、八轮转动、炮塔偏航、炮管俯仰、雷达和枪口位置。最终实拍与洋红背景检查图见 `../unity-preview/`。乘员独立机枪仍沿用原型外观。本次未验证独立发行包、完整对局或多人性能。

`validate_motion.py` 将八轮各旋转 90°，检查轴心和轮胎边界并输出 `wheel_motion_validation.json` 与 `renders/wheel_rotation_90/`。照片中未见轮拱随轮扫动。`validate_articulation.py` 对炮塔偏航 42°、炮管俯仰 12°、雷达转向 70° 检查层级并输出 `articulation_validation.json` 与 `renders/articulation_pose/`。蓝队与红队静态五视图分别在 `renders/` 与 `renders/red/`。

## 复现

原始输入：`/Users/liuwenjun/Downloads/d722c1edebb754a5735858477acc4459.glb`。在 Blender 5.2.2 中运行：

```sh
/Applications/Blender.app/Contents/MacOS/Blender -b -t 4 --python build_a40.py
/Applications/Blender.app/Contents/MacOS/Blender -b -t 4 --python render_red.py
/Applications/Blender.app/Contents/MacOS/Blender -b -t 4 --python validate_motion.py
/Applications/Blender.app/Contents/MacOS/Blender -b -t 4 --python validate_articulation.py
```

脚本依赖原始 GLB 的绝对路径，且会覆盖同目录的导出、贴图、指标和渲染文件。
