# 0.19.0 Unity 可运行切片：首轮移植

## 目标与基线

本轮是 Unity 移植的第一阶段。开始前本地 `main` 与 GitHub `origin/main` 均为 `55eecbb`、版本 `0.18.0`，工作区干净。现有 Three.js 游戏是玩法和数值的参考实现；本轮增加独立的 `UnityProject/`，先建立可以导入的工程与单人步兵战斗切片。`FurtherPlan.md` 所列多地图、新载具、新模式仍属于原网页游戏后续计划，本轮没有把它们标为已完成。

按 `ForAgent.md` 的新模块规则，将仓库版本升为 `0.19.0`。网页房间接口、客户端检查和菜单版本一同更新；这只是发布版本同步，网页与 Unity 之间尚无跨端联机协议。

## 工程与启动

- 工程采用本机 Unity 6000.5.10f1 安装包自带的 3D URP 模板文件，保留 `Packages/`、`ProjectSettings/`、URP 设置及 `Assets/Scenes/SampleScene.unity`。`Assets/Scripts/` 与音频资源为本项目新增内容，并带有版本控制用 `.meta` 文件。
- 在 Unity Hub 中通过 **Add project from disk** 选择仓库内的 `UnityProject` 目录，使用 Unity 6000.5.10f1 打开。打开 `Assets/Scenes/SampleScene.unity` 并点击 Play。`PrototypeRuntime` 在场景载入后自动创建战场、玩家和 AI，无需手动把脚本挂到场景对象。
- WASD 移动，鼠标看向，左键射击，R 换弹，Shift 冲刺，Space 跳跃，Esc 释放鼠标；释放后点击游戏画面重新锁定。左上角显示血量、子弹、双方票数和五个据点。这个工程目前只针对桌面键盘鼠标。
- Unity 的 `Library/`、`Temp/` 等导入产物已在 `.gitignore` 中排除；提交的是可再生成的工程源码和资源，不提交本机缓存。

## 本轮实现

| 模块 | 代码 | 移植内容 |
|---|---|---|
| 地图 | `PrototypeLayout.cs`、`PrototypeRuntime.cs` | 720 米地图、原五个据点坐标、基地坐标、三座丘陵高度函数、建筑/掩体布局；用 Unity 网格和基础几何生成可碰撞场景。 |
| 导航 | `PrototypeNavigation.cs` | 原 16 米、45×45 步兵导航网格，1.12 米间隙、可见节点、A* 和路径简化。 |
| 征服 | `PrototypeMatch.cs` | 25 米占点半径、多人加速占点、争夺停止、每多一处据点每秒 0.85 票消耗、阵亡扣票。为快速验收使用 100 票短局。 |
| 玩家 | `PrototypePlayer.cs` | 第一人称移动、跳跃、冲刺、鼠标视角、射线命中、死亡和重生。IF-27 的伤害 31、射击间隔 0.105 秒、30/150 弹药、1.9 秒换弹和 165 米最大射程沿用网页数值；射击音效复用 `public/audio/carbine.wav`。 |
| AI | `PrototypeBot.cs` | 蓝红各四名简化步兵，分别推进 B/C 据点，沿导航路径移动、发现无遮挡敌人后射击、阵亡后重生。 |
| 表现 | `PrototypeRuntime.cs` | 生成地形、方块建筑、据点环和旗杆、队伍颜色、简易枪模、弹道线及 IMGUI 战场 HUD。 |

## 与完整游戏的差距

Unity AI 目前只有固定的 B/C 目标、局部寻路和基础交火，没有移植网页版本的情报受限指挥官、四人兵种小队、掩体分配、治疗/补给、载具协作与难度配置。场景使用占位几何和简易 HUD，尚无网页的完整美术、主菜单、配装、征服/前线模式选择、载具、空战或局域网房间。现在的 Unity 与网页客户端不能互联。这里的“可运行切片”表示源代码与场景的设计目标，实际 Unity 编辑器 Play 尚待验证，不能视为整个游戏已移植完成。

## 验证和当前阻碍

- 使用 Unity 6000.5.10f1 随附 .NET SDK 与 Roslyn，对六个 C# 脚本引用本机 UnityEngine 和 .NET Standard 程序集进行静态编译，结果通过。此检查证明语法和引用解析，不证明 Unity 导入、场景渲染、运行时逻辑或打包成功。
- 两次使用本机 Unity Editor 命令行创建/导入项目。第一次受沙盒限制，Package Manager 的本地 socket 返回 `EPERM`；放宽该限制后 Package Manager 可连接，但 `Unity.Licensing.Client` 在初始化时反复抛出 `System.ObjectDisposedException`（`IServiceProvider`），编辑器无法进入场景。Unity Hub 界面显示已有 Personal 许可证，但编辑器批处理授权仍失败。为继续推进，工程文件从同一安装的官方模板解包构建；没有把这一步误记为 Unity Play 验证。
- 实际图形、输入、AI 移动、命中、占点和最终构建需在可正常启动 Unity Editor 的机器上按上面的启动步骤验收。若编辑器控制台报错，优先记录首条编译/导入错误与 Unity Editor 日志，再修正。

## 后续阶段

1. 在 Unity 编辑器中完成导入与 Play 验收，修复任何运行时错误，录制短局结果并进行构建测试。
2. 把网页 AI 的情报、指挥、小队状态、掩体和兵种行为逐模块移植，做确定性战局对比，而非直接复制目前的简化 AI。
3. 移植载具、装备、完整 UI 和视听资产；核对地图导航与性能。
4. 设计独立的 Unity 网络权威状态与房间协议，完成双实例和真实局域网验收。网页现有 Node/WebSocket 协议尚未直接供 Unity 使用。

每一步在对应 Unity 运行环境验证后再更新完成状态。
