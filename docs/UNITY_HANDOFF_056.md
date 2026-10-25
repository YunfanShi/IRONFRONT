# IRONFRONT 剩余五款载具替换（0.56.0）

2026-10-23。在原 `codex/unity-port` 工作区继续，保留现有模型与未提交工作。R4、U8、M2、R3、J20 已接入实际生成路径；加上先前 H8、A40、T90、V12，九款均有红蓝美术、三档 LOD 与运行时绑定。使用 Blender 5.2.2 LTS、Unity 6000.5.10f1 / URP。

## 本次交付

| 车型 | 可动部分 | Unity LOD0 / 1 / 2 三角面 |
| --- | --- | --- |
| R4 SCOUT | 机枪偏航/俯仰、四轮滚动、前轮转向 | 72,579 / 38,876 / 21,050 |
| U8 ROVER | 机枪偏航/俯仰、四轮滚动、前轮转向 | 72,360 / 38,838 / 20,940 |
| M2 COURIER | 前叉/握把转向、前后轮滚动、车身侧倾 | 68,273 / 37,525 / 21,355 |
| R3 FIREFLY | 主旋翼、尾旋翼、雷达、机枪、机身俯仰/侧倾 | 59,868 / 32,118 / 17,635 |
| J20 STRATUS | 机身俯仰/侧倾、机炮、三只起落架轮滚动 | 45,270 / 23,696 / 12,662 |

已有融合网格沿机械关节切开，保留逐角 UV，并补齐分离截面；不是仅重命名整车网格。R4 机枪原本朝车后、U8 机枪原本斜向车头，现已校正初始朝向，炮口与发射方向对应。车顶附件以连通岛区分，避免炮塔转动时牵动车顶。M2 前叉与轮轴分为两级，身体、座位和手脚挂点随原有驾驶逻辑运动。

米制比例按原型游戏的占地校准：R4 宽 3.6 米、U8 宽 3.4 米、M2 长 2.55 米、R3 主旋翼投影宽 8 米、J20 翼展 12.5 米。保留原造型比例，长度因此与旧方块原型有差异。碰撞盒、炮口、驾驶观察和座位挂点均已匹配新模型；不以现实同名车辆尺寸重建。

## 文件入口

- 制作脚本：[build_articulated_remaining.py](../art/vehicles/build_articulated_remaining.py)；正交源预览和关节预览脚本为同目录 `render_remaining_sources.py`、`render_articulated_remaining.py`。
- 新 Blender 工程：`art/vehicles/{r4-scout,u8-rover,m2-courier,r3-firefly,j20-stratus}/articulated/<车型>-articulated.blend`。每个目录包含 `build.json`、三档 `exports/*.fbx`、`rest-front.png` 和 `posed-front.png`。
- 新 Unity 导入目录：`Assets/Art/Vehicles/{R4Scout,U8Rover,M2Courier,R3Firefly,J20Stratus}`。
- 运行预制体：`Assets/Resources/Vehicles/{R4,U8,M2,R3,J20}_{Blue,Red}.prefab`，共十个。
- [五款游戏预览](../art/vehicles/remaining-integration-2026-10-23/delivery-preview.png)、[红蓝 LOD0 联系表](../art/vehicles/remaining-integration-2026-10-23/unity-contact-sheet.png)、[驾驶/武器观察视图](../art/vehicles/remaining-integration-2026-10-23/optic-contact-sheet.png)。
- 新 macOS Development 程序：`UnityProject/Builds/Vehicles-0.56.0/IRONFRONT.app`；旧 `Armor-0.55.0` 构建保留。

## Unity 接入说明

`VehicleArtAssetSetup.BuildRemaining()` 构建五款；`BuildAll()` 包含九款。新增 `RemainingVehicleArtSetup.cs` 复用已有导入器、纹理转换和材质构建，独立处理各车型关节。FBX 为米制，`globalScale=1`，源节点沿用 X=-90° 转轴；活动件包在中性 Unity 轴向节点下。保留 `.52/.18/.025` LOD 阈值与既有红蓝 2K 贴图，金属/粗糙度按 glTF 通道转换，法线强度 0.65。

R4 和 U8 仍由 `PrototypeScoutVehicle`、`PrototypeTransportVehicle` 驱动；新增 `BindLight` 同步三档机枪轴心，轮子按每轮世界位移与实测半径滚动，前轴跟随转向。驾驶席/炮手席的既有权限保持。旧原型 Renderer 被关闭，死亡/网络显隐控制整个美术实例，避免 LODGroup 重新显示旧车体。

M2、R3、J20 接入 `PrototypeCombinedVehicleVisual`。M2 使用原有转向、侧倾与驾驶姿态逻辑，新挂点校准驾驶/乘客的座位、眼睛、握把和脚踏。R3/J20 武器逻辑节点置于机身下，与可见机身姿态一致；仍使用既有瞄准角限制、射击和飞行规则。R3 保留独立乘员机枪并调整位置/比例。`UpdateVisualMotion` 从原 Update 提取，游戏与验证共同调用同一运动路径。

`PrototypeRemainingArtProbe` 仅在 Editor 或 Development Build 编译，显式传入 `-ironfrontRemainingProbe <输出目录>` 才创建；普通启动不改变游戏状态。

## 验证结果

- [实际场景 Play](../art/vehicles/remaining-integration-2026-10-23/play-results.txt)：106 项断言通过。覆盖十个红蓝实际生成实例、三档 LOD、材质、五款玩家驾驶/飞行、轮轴/转向、旋翼、摩托侧倾及乘客权限、炮塔/俯仰/炮口对齐、真实发射、显隐和旧网格关闭；还确认先前四款仍正常绑定。
- [已有转向与观瞄回归](../art/vehicles/remaining-integration-2026-10-23/batch-results.txt)：摩托车平滑转向/回正/反向、乘客权限、坦克真实炮管观察、稳定视线、测距、俯仰、装填、切换视角及座位生命周期通过。
- [资产验证汇总](../art/vehicles/remaining-integration-2026-10-23/asset-validation.json)：十五份 FBX 均在独立 Blender 导入中检查，UV 与材质存在，无无效顶点、退化面或缺失材质。少量切口面在 Blender/Unity 统计上有差异；边界数量不代表此类游戏网格必须可打印密闭。
- [独立程序启动](../art/vehicles/remaining-integration-2026-10-23/development-player.txt)：0.56.0，Apple M5 / Mac17,4，九款双方资源与三档 LOD 均加载，运行时错误 0。五张 `development-<车型>.png` 为真实程序包含 HUD 的画面；Editor 相机截图不包含 OnGUI。
- macOS 开发构建成功，BuildReport 大小 486,807,654 字节。十份原静态 optimized/red Blender 的 SHA-256 与任务开始时一致。
- [代码局部差异](../art/vehicles/remaining-integration-2026-10-23/task-only-code.patch) 相对本次开始时工作区基线；新增代码文件另行保存在工程内。没有提交、合并、清理其他已有改动。

## 复现和保留边界

```sh
'/Applications/Blender.app/Contents/MacOS/Blender' --background --factory-startup \
  --python-exit-code 1 --python art/vehicles/build_articulated_remaining.py -- r4 u8 m2 r3 j20
```

复制各 `articulated/exports` 到对应 Unity Art 目录后，在 Unity 执行 **IRONFRONT → Art → Build Remaining Vehicle Prefabs**。关闭其他占用同一工程的 Editor 后，专用 batch 可执行 `Ironfront.UnityPrototype.Editor.RemainingArtPlaySmoke.Run`；此 Play 作业自行退出，不加 `-quit` 或 `-nographics`。

沿用现有飞行/碰撞和武器玩法，没有新增 WheelCollider、悬挂、起落架收放、飞行舵面控制或乘员动画。J20 起落架保持原模型固定伸出；R3 尾旋翼为源融合网格的局部分件。没有双机 LAN、长期完整对局或最低配置性能评测。九款载具的替换工作已完成，原先“剩余五款待接入”的交接条目由本文件更新。
