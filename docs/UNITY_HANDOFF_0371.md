# IRONFRONT Unity 移植交接（0.37.1）

## 接手时先看这里

这份文件记录 2026-10-11 的开发状态，供下一位开发者或智能体接手。仓库同时包含原网页游戏、Node 局域网服务和正在移植的 Unity 游戏；不要把网页已经实现的功能直接视为 Unity 已实现。

| 项目 | 当前状态 |
| --- | --- |
| 开发分支 | `codex/unity-port`，远端 `git@github.com:YunfanShi/IRONFRONT.git`；用户明确要求 Unity 二次开发留在独立分支，不推到 `main` |
| 交接前最后一个玩法提交 | `b113093`：载具炮塔追随与双准星；编写本文件前已确认远端分支也指向该提交 |
| 当前版本 | `0.37.1`：本轮只增加交接文档、README 入口，并按 `ForAgent.md` 将补丁版本加一；上一版玩法为 `0.37.0` |
| Unity 编辑器 | Unity `6000.5.10f1`；工程为 `UnityProject`，入口场景为 `Assets/Scenes/SampleScene.unity` |
| Unity 房间协议 | `unity-3`；网页房间与 Unity 房间不能混用 |

`ForAgent.md` 要求每次修改更新版本、写文档并上传仓库。它还提到合并 `main`，但用户在此前会话中明确要求二次开发留在其他分支，因此以用户要求为准。后续继续提交到 `codex/unity-port`，不要擅自合并 `main`。

## 目前能运行的内容

- **网页版本**：原游戏及其 Node LAN 服务仍在仓库中，供规则、武器和外观对照；它与 Unity 原型是不同的运行实现。网页启动和测试脚本见 `package.json`。
- **Unity 单人战斗**：全屏 UI Toolkit 启动菜单、玩家名字、作战与配装页，四兵种、五据点工业地图、AI 小队、部署与复活、战斗 HUD、暂停设置、步兵射击/开镜/换弹/冲刺/滑铲/蹲下/趴下。T90 坦克、R4 侦察车和 U8 运输车可在已有权限范围内驾驶或操作武器。
- **最近两轮玩法**：`0.36.0` 为坦克炮弹和枪弹加入飞行时间与重力下坠；`0.37.0` 让 T90 主炮、R4 机枪、U8 炮手位机枪按各自转速追随玩家视线，并从真实炮口方向发射。载具 HUD 的青色 `AIM` 是玩家相机准星，琥珀色 `BARREL` 是当前炮口轴线投影。
- **Unity LAN**：房主创建房间、阵营、准备、票数、踢人和开局由服务端核验；普通玩家不能自称房主或发布战场快照。此处已有实现和局部自动验证，不能等同于完整多人载具玩法或两台电脑的长期实战验收。
- **暂未实现的菜单选项**：`赤砂走廊` 地图和 `攻防 / BREAKTHROUGH` 模式在 Unity 作战页明确标为“开发中，暂不可选择”。此前生成的概念图不代表玩法已落地。

入口与近期详细记录：

| 需要了解 | 先读 |
| --- | --- |
| Unity 项目与编辑器 | [首轮移植](UNITY_PROTOTYPE_019.md)、[安全模式修复](UNITY_SAFE_MODE_0191.md) |
| 菜单与配装 | [可编辑 UI Toolkit 菜单](UNITY_MENU_UI_TOOLKIT_033.md)、[战斗 HUD 与配装](UNITY_COMBAT_UI_LOADOUT_034.md) |
| 步兵操作 | [步兵操作和战斗 HUD](UNITY_INFANTRY_HANDLING_035.md) |
| 坦克、弹道与准星 | [驾驶与复活](UNITY_TANK_RESPAWN_030.md)、[弹道](UNITY_BALLISTICS_036.md)、[载具瞄准](UNITY_VEHICLE_AIM_037.md) |
| 局域网边界 | [房间权限](UNITY_LAN_026.md)、[重连与载具接管](UNITY_STABILITY_0311.md) |

## 代码从哪里改

| 目标 | 文件 |
| --- | --- |
| Unity 战局、HUD、双准星、部署入口 | `UnityProject/Assets/Scripts/PrototypeRuntime.cs` |
| 步兵输入、姿态、枪械与上车 | `UnityProject/Assets/Scripts/PrototypePlayer.cs` |
| 载具瞄准公共计算 | `UnityProject/Assets/Scripts/IPrototypeVehicle.cs` 中的 `IPrototypeWeaponAim`、`PrototypeVehicleAim` |
| T90 炮塔、R4 机枪、U8 机枪 | `PrototypeTankVehicle.cs`、`PrototypeTankVisual.cs`、`PrototypeScoutVehicle.cs`、`PrototypeTransportVehicle.cs`，均在 `UnityProject/Assets/Scripts/` |
| 枪弹与炮弹飞行 | `UnityProject/Assets/Scripts/PrototypeBallisticProjectile.cs` |
| 启动菜单结构与样式 | `UnityProject/Assets/Resources/Menu/StartMenu.uxml`、`StartMenuTheme.uss`；逻辑在 `PrototypeStartMenuView.cs` |
| Unity LAN 客户端与服务端权限 | `UnityProject/Assets/Scripts/PrototypeLanClient.cs`、`server/unity-room.cjs` |
| 编辑器内验证入口 | `UnityProject/Assets/Editor/PrototypeGameplaySmoke.cs`、`PrototypeBallisticsSmoke.cs`、`PrototypeStabilitySmoke.cs` |

## 启动和复测

1. 用 Unity Hub 打开仓库中的 `UnityProject`，确认编辑器版本为 `6000.5.10f1`。打开 `Assets/Scenes/SampleScene.unity`，等待资源导入和 C# 编译完成；如果 Unity 没有自动刷新，执行 `Assets → Refresh`。
2. 点击 Play，在启动菜单按 **Enter** 进入单人战斗。靠近己方载具按 `E` 上车，T90 与 R4 用鼠标转炮/机枪、左键开火；U8 按 `F2` 切到炮手位。`Esc` 暂停或释放鼠标。
3. 进入 Play 模式后运行 `IRONFRONT → Verify Tank and Respawn`。上一轮实测输出 `IRONFRONT smoke PASS`，Console 为 0 警告、0 错误；该烟测检查 T90/R4/U8 的水平与俯仰转速、T90 炮弹从当前炮管方向发射，以及复活流程。另可运行 `IRONFRONT → Verify Ballistics` 检查飞行与弹坠。
4. 手动进入 T90、R4 和 U8 炮手位，快速横移及抬高视线。中心 `AIM` 应立即随视角移动，`BARREL` 应随炮塔转动逐渐靠近；开火应沿当前炮管方向。最后核对不同窗口尺寸下两标记的可读性和屏幕边缘处理。
5. 若改动了网页或共享版本字段，在仓库根目录执行 `npm run typecheck` 和 `npx tsc -p tsconfig.logic.json --noEmit`。完整回归脚本见 `package.json`；不要把历史文档中的旧版构建结果当作当前版本的构建证明。

## 已验证与尚未验收

- `0.37.0` 的 Unity 运行时与编辑器程序集使用 Unity 随附 C# 编译器编译通过；TypeScript 主配置和逻辑配置检查通过。Unity 编辑器内的 `Verify Tank and Respawn` 在加入 R4、U8 断言后通过，Console 为 0 警告、0 错误。曾用启动菜单 **Enter** 进入单人战斗，看到战斗 HUD 正常出现。
- **尚未目视确认载具内的双准星**：自动控制没能稳定完成上车，因此仍需按上面的手动步骤检查准星位置、颜色、炮塔追随速度和开火手感。程序化测试覆盖了转向与炮口发射方向，不能替代画面验收。
- `BARREL` 是炮管的**直线轴线**，不是计算弹坠后的预计落点；远距离射击仍需自行抬高。镜头与炮口有位置差，近处掩体也会使两个准星分开。
- Unity LAN 快照尚未同步炮塔/炮管姿态，也未完成多人载具席位与姿态、开镜等完整同步；访客看到的炮塔方向可能与房主本地发射方向不一致。单人/房主本地的真实炮口逻辑已接通。
- 本轮交接仅改文档和版本字段，没有生成新的 `0.37.1` macOS 独立应用，也没有重新执行两机联机实测。Unity 编辑器如果继续显示旧 `Application.version`，重启编辑器并重新导入工程后检查；源文件中的版本字段应为 `0.37.1`。

## 工作树注意事项与建议顺序

编写交接文件前，工作树已有以下 **Unity 编辑器自动改写、未提交** 的本机差异：`UnityProject/Assets/Settings/DefaultVolumeProfile.asset`、`PC_RPAsset.asset`、`UniversalRenderPipelineGlobalSettings.asset`、`UnityProject/ProjectSettings/ProjectAuditorSettings.asset`、`ProjectSettings.asset`、`ShaderGraphSettings.asset`。不要把它们作为本轮功能提交，也不要未经检查就删除。`ProjectSettings.asset` 本轮只应提交 `bundleVersion` 的单行版本变更。

建议下一位开发者先完成三辆车的**双准星实机目视验收**并修正发现的显示或瞄准偏差，再决定是否加入弹道预测落点。联机炮塔姿态同步应在用户重新要求联机开发时处理；赤砂地图和攻防模式仍是另一项独立工作。
