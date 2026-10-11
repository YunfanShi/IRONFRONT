# 0.39.0 Unity 单人战局闭环首轮

## 目标与结论

本轮按 [网页与 Unity 逐项对照及验收清单](UNITY_PARITY_038.md) 的 S1、S2、S8 推进第一步：征服和攻防须能从菜单开局，经据点争夺、阵亡部署走到明确胜负，再回大厅。版本号按新增结算功能升级到 0.39.0；版本号不代表还原比例。当前是**规则闭环与失败结算路径通过，完整人工长局待验**，因此 S1/S2/S8 仍不得标为“已还原”。

## 网页对照与实现

| 场景 | 网页参照 | 本轮 Unity 行为 | 已验证范围 |
| --- | --- | --- | --- |
| 征服票数 | `src/core/Battle.ts`：占点优势耗对手票，900 秒后双方额外耗票，票尽时按剩票、占点、击杀依次判胜 | `PrototypeMatch.Tick` 补 900 秒后每队每秒 0.28 票耗损；双方同为 0 票时按占点数判胜 | 隔离规则烟测；击杀数平票裁决尚未移植 |
| 攻防推进 | `Battle.ts`：红方守 A–E，蓝方依次夺点，夺点补 40 票，五点全部夺取或蓝方票尽结束 | 用 `PrototypeBot` 置于据点并连续运行真实 `Tick`，验证非活动 B 不能跳过 A、A–E 顺序推进、补票和蓝方胜利；票尽判红方胜利 | 规则层实际 Tick；人工带队完整战局尚未验收 |
| 部署地点 | 网页基地、据点、队友、载具可选 | `PrototypeFrontend` 的候选地点全部纳入可滚动列表，侦察信标加入后不再被前 11 项截断 | 代码与编辑器编译；逐地点人工部署待验 |
| 结算返回 | 网页有胜负和结束后返回菜单的路径 | `PrototypeRuntime` 单人结算页显示胜负、模式、票数、A–E 归属和返回大厅按钮；结束后先释放鼠标锁定，避免点击又锁鼠标 | 编辑器 Play 预览攻防失败分支，实际点击按钮回到大厅 |

## 修改文件

- `UnityProject/Assets/Scripts/PrototypeMatch.cs`：征服加时耗票和双方 0 票的据点裁决。
- `UnityProject/Assets/Scripts/PrototypeRuntime.cs`：单人结算页与返回大厅入口。
- `UnityProject/Assets/Scripts/PrototypePlayer.cs`：胜负判定先于鼠标重新锁定。
- `UnityProject/Assets/Scripts/PrototypeFrontend.cs`：部署地点滚动显示。
- `UnityProject/Assets/Editor/PrototypeMatchRulesSmoke.cs`：扩展两模式规则检查；增加编辑器 Play 中预览攻防失败结算的入口。
- 菜单、网页、LAN 服务、包清单及 Unity Bundle 版本同步为 0.39.0。网页和 LAN 本轮仅同步版本显示，没有新增玩法或协议变更。

## 验证记录（2026-10-11）

1. Unity 6000.5.10f1 打开 `SampleScene`，`Assets → Refresh` 后 C# 编译无错误、无警告。
2. 执行 `IRONFRONT → Verify Solo Match Rules`，Console 输出 `IRONFRONT solo match rules PASS: Conquest and Breakthrough`。检查了两模式初态、红方无限票、非活动据点、真实 Tick 占 A–E、补票、双方胜负、征服占点耗票与加时耗票。
3. 编辑器 Play 从启动页进入攻防，确认 A 为当前目标、A–E 初始归红、蓝方 100 票、红方无限。执行 `IRONFRONT → Preview Solo Breakthrough Defeat` 将蓝方票设置为 0，目视出现战败结算页；实际点击“返回大厅”后回到启动页。Console 0 错误、0 警告。该预览是测试入口，不代表真实连续战斗已赢/输一局。
4. 执行 `IRONFRONT → Build macOS Standalone`，Unity Console 输出 `IRONFRONT macOS build succeeded`，0 错误、0 警告。`IRONFRONT.app/Contents/Info.plist` 的 `CFBundleShortVersionString` 为 `0.39.0`。关闭旧实例后重新启动新包，目视菜单显示 0.39.0；按 Enter 进入攻防战局，确认 A 高亮、五点归红和票数显示；按 Esc 暂停、L 返回大厅。独立程序尚未运行到自然胜负。
5. 网页 TypeScript 主配置 `tsc --noEmit` 和逻辑配置 `tsc -p tsconfig.logic.json` 均通过。这只验证版本同步没有破坏网页类型检查。

## 仍需完成的第一步验收

| 验收项 | 当前状态 | 通过条件 |
| --- | --- | --- |
| S1 征服人工整局 | 待验 | 独立程序从菜单开始，人工与 AI 争夺据点、阵亡部署、票尽结算、返回大厅再开局；记录票数与 Console |
| S2 攻防人工整局 | 待验 | 独立程序实际带队按 A→E 推进并赢一局，另以真实战斗耗尽蓝方票输一局；记录重生点、AI 命令与卡路情况 |
| S8 多次复活与全部部署点 | 待验 | 基地、己方点、队友、载具、侦察信标逐项试选，至少两次阵亡后复活无阻断 |
| 原版规则细节 | 局部 | 征服同票同占点时根据真实击杀数决胜；确认各模式可选票数与网页一致，目前 Unity 单人默认 100 票短局 |

只有对应场景实际通过，才在对照表中把该项改为“通过”。隔离规则烟测、结算预览和独立构建不能替代人工长局。
