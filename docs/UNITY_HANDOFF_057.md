# Unity 0.57.0 — 红蓝士兵蒙皮与运行时替换

2026-10-24。延续 0.56.0 的九款载具接入，在现有 `codex/unity-port` 工作区增量制作；保留已有模型、静态交付、游戏逻辑和未提交工作。

## 本次结果

红蓝士兵从静态预览推进为实际角色：本地玩家、AI、联网角色生成路径、大厅配装预览、普通第一人称持枪与 M2 驾驶/乘客视角使用新美术。继续使用已有步行、跑步、侧移、蹲伏、趴伏、倒地和装备 IK 的程序化驱动。

每方模型身高 1.8 米，17 根变形骨：骨盆、躯干、头、左右上臂/前臂/手掌/合并手指、大腿/小腿/脚。使用 Blender 骨骼热扩散权重，最多四个影响、归一化；头盔与鞋底保留刚性区域。肩腋处经过抬臂与骑乘复查，替换了早期按坐标划区产生的拉伸权重。

| 队伍 | LOD0 | LOD1 | LOD2 |
| --- | ---: | ---: | ---: |
| Blue | 20,000 | 6,000 | 2,000 |
| Red | 19,984 | 6,000 | 2,000 |

每档分为身体、左臂、右臂三份蒙皮网格，继续使用原有 2K PBR 和 UV。运行时三档共用同一骨架。普通持枪第一人称另有从现有手臂派生的网格，去除隐藏躯干的权重和肩根过渡面，避免普通持枪画面出现长尖片；第三人称和摩托骑乘使用完整手臂网格。LOD 转换、躯干/手臂显示及切换恢复均由预制体管理。

## 文件与复现入口

- `art/characters/build_rigged_soldiers.py`：在原有 technical blend 上生成新蒙皮源文件；不改写原文件。
- `art/characters/blue-soldier/rigged/blue-soldier-rigged-v01.blend`、对应 red 路径：可编辑源工程。
- 每个 `rigged/` 内有三档 `*-skinned-lod*.fbx` 和 `rig-report.json`。
- `art/characters/validate_rigged_soldiers.py`：六份 FBX 在全新 Blender 场景回读，检查骨骼、权重、尺度及弯曲。
- `UnityProject/Assets/Editor/SoldierArtAssetSetup.cs`：`IRONFRONT/Art/Build Runtime Soldiers`；配置米制 Generic 蒙皮、原 PBR、共享骨架、第一人称网格和 LOD 预制体。
- `UnityProject/Assets/Resources/Soldiers/BlueSoldier.prefab`、`RedSoldier.prefab`：实际运行时资源。
- `PrototypeSoldierArtRig.cs`：将既有逻辑关节同步到美术骨骼，保存 rest rotation 修正及手指握持。
- `PrototypeSoldierVisual.cs`：加载美术，按实际臂长/腿长与脚底高度调整 IK，保留原型回退；替换普通第一人称手臂。
- `PrototypeMenuPreview.cs`：大厅使用真实臂长而非旧写死长度，保留士兵的 PBR 材质。
- `PrototypeCombinedVehicleVisual.cs`：M2 导入锚点的左右标签会因 FBX 坐标转换反转，按车辆局部 X 修正；不再让手脚交叉寻找另一侧握把/脚踏。
- `SoldierArtPlaySmoke.Run`：实际 Play 验证和近景；`PrototypeSoldierArtProbe` 只有显式开发版参数才会运行。

Blender 使用 `--background --python-exit-code 1 --python art/characters/build_rigged_soldiers.py`。Unity 无已打开编辑器时，固定 `-buildTarget StandaloneOSX`，依次执行 `SoldierArtAssetSetup.Build` 和 `SoldierArtPlaySmoke.Run`；后者进入 Play 后自行退出，不加 `-quit`。批量回归清单位于证据目录 `regression-checks.txt`。

## 校准与边界

- 美术骨盆高 .91 m，上臂约 .251 m、前臂到握点约 .317 m。步兵持枪挂点相应提高并后移，避免把新模型拉长到旧原型的 .69 m 臂长。
- 原 FBX 转换反射 X，导入时按 Unity 实际左右交换骨骼名；保留正前方向，避免人物背对枪口。
- M2 手掌跟随真实转向握把，靴底而不是脚踝中心落在脚踏上；第一人称显示同一骑乘手臂。
- 已有模型自带的胸前和腿侧袋保留，隐藏重复原型弹药袋；医疗、工程与侦察识别件贴合新轮廓。
- 此次没有重做枪械模型、面部表情、单独五指动作或完整 Humanoid Animator 动画库。FBX 是可蒙皮的 Generic 骨架，游戏动作由已有程序化 IK 驱动；合并手指骨提供握持弯曲。
- 联网路径检查覆盖实际 `PrototypeRemotePlayer.Initialize` 的美术生成，没有新开两台机器的联网对局。本次没有做全地图帧率性能承诺。

## 验证与交付

最终验证记录、截图、日志、原始文件校验、交付清单位于：
`art/characters/soldier-integration-2026-10-24/`。

- `play-results.txt`：160 项检查通过，双方全部三档蒙皮、站立/蹲伏/趴伏/跑步、脚底接地、握点、M2 两席与转向极值、视角恢复、普通第一人称无躯干权重、联网生成路径。
- `fresh-fbx-audit.json`：六份最终 FBX 回读通过，17 骨、三份网格、最多四个归一化影响，0 项错误。
- `batch-results.txt`：既有玩家/载具摄像机、手持装备、九类载具 28 次座位切换、大厅配装回归。
- `development/development-player.txt` 和 PNG：Apple M5 上实际 0.57.0 开发程序启动、红蓝姿势、步枪/手枪第一人称、M2 两席两视角。
- `preservation-check.json`：16 份原始高模/技术源工程/静态导出哈希未变；原贴图继续复用。

独立测试程序：`UnityProject/Builds/Soldiers-0.57.0/IRONFRONT.app`。既有 `Builds/Vehicles-0.56.0` 和更早交付保留。

最终开发程序运行退出码 0，运行时错误 0，十张实际帧通过空帧/缺材质检查；包内文件合计 520759534 字节。机器可读汇总见 `final-validation.json`。
