# IRONFRONT Unity 视角切换交接（0.45.0）

## 接手时先看这里

本文件记录 2026-10-11 的 Unity 移植状态和本轮第一、第三人称双向切换，供后续开发者接手。仓库同时包含网页原版、Node LAN 服务和 Unity 工程；版本号是开发编号，不表示网页功能的还原比例。当前分支 `codex/unity-port`，此前用户明确要求 Unity 二次开发留在此分支，不合并 `main`。Unity 编辑器为 `6000.5.10f1`，工程入口 `UnityProject/Assets/Scenes/SampleScene.unity`。

本轮起点为 `def4252`（0.44.0：步兵固定第三人称、载具固定第一人称）。用户明确修正需求：**人物和载具都要能在第一、第三人称之间来回切换**。0.45.0 已实现，用户反馈在独立程序中验证功能正确。详细技术记录见 [0.45.0 视角切换](UNITY_PERSPECTIVE_TOGGLE_045.md)，较早的固定视角实现见 [0.44.0 视角记录](UNITY_PERSPECTIVE_044.md)。

## 玩家操作与当前行为

| 场景 | 操作和表现 |
| --- | --- |
| 步兵 | 开局默认肩后第三人称；每按一次 `V`，在肩后全身视角与眼前持枪第一人称之间切换。第一人称隐藏自身身体、显示镜头下枪模；第三人称显示人物和手中枪。准星仍以镜头射线选目标，子弹、手雷、火箭从当前可见武器附近发出。 |
| T90、R4、U8 | 首次上车默认座位第一人称；`V` 往返切换座位视野和车后上方第三人称。车后相机受外部障碍阻挡时前移，忽略本车碰撞。T90 第一人称保持较窄的瞄具视角。 |
| 上下车、换座、复活 | 步兵与载具各自记住上次选择；上车使用载具所选模式，下车恢复步兵所选模式，U8 的 `F1/F2` 换座不重置载具视角。重新部署后保留步兵选择。 |
| 倒地救援 | 倒地时使用原有地面视角与约 170° 水平观察范围；救援后回到倒地前的步兵模式。倒地时 `V` 不切换。 |
| 卧倒键 | Unity 原先的 `V` 卧倒移至左或右 `Ctrl`；`C` 仍是蹲下或滑铲。HUD 底部显示 `V view 1P/3P` 和 `Ctrl prone`。网页原版的 `V` 视角键保持原样。 |

## 从哪里修改

| 目标 | 文件 |
| --- | --- |
| 视角状态、输入、步兵枪模显隐、相机、载具镜头、发射位置 | `UnityProject/Assets/Scripts/PrototypePlayer.cs` |
| 战斗 HUD 的当前人称与操作提示 | `UnityProject/Assets/Scripts/PrototypeRuntime.cs` |
| 编辑器视角与上车、U8 换座烟测 | `UnityProject/Assets/Editor/PrototypePerspectiveSmoke.cs` |
| 第一人称倒地救援后视角回归 | `UnityProject/Assets/Editor/PrototypeDownedReviveSmoke.cs` |
| 启动菜单与网页界面的版本显示 | `UnityProject/Assets/Resources/Menu/StartMenu.uxml`、`src/ui/UI.ts` |
| 长期逐项验收与本轮说明 | [对照及验收清单](UNITY_PARITY_038.md)、[0.45.0 技术记录](UNITY_PERSPECTIVE_TOGGLE_045.md) |

两种步兵视角的枪模是独立对象：第三人称武器随人物骨架，第一人称武器随相机；`ApplyPerspectiveVisibility()` 决定人物和两套枪模的显隐。载具的人称状态与步兵状态分开保存。继续改枪械或载具时应同时检查两种画面、镜头与枪口的位置关系，不要只验证默认视角。

## 本轮验证及其边界

1. Unity 编辑器 `Assets → Refresh` 后 C# 编译无错误或警告。Play 中运行 `IRONFRONT → Verify Player and Vehicle Cameras`，输出 `IRONFRONT perspective PASS`，覆盖步兵双向切换、T90/R4/U8 双向切换、U8 换座、上下车恢复和枪模显隐。编辑器中目视看到步兵眼前枪模和 T90 车后镜头，HUD 显示当前模式。
2. 在第一人称步兵状态运行 `IRONFRONT → Verify Downed View and Revive`，输出 `IRONFRONT downed view and revive PASS`；医疗兵救起后回到第一人称。`Verify Tank and Respawn` 输出 `IRONFRONT smoke PASS`，覆盖坦克起步、转角驾驶、主炮冷却、离车和复活。测试后 Console 为 0 警告、0 错误。
3. Unity 成功生成 macOS 独立程序 `UnityProject/Builds/macOS/IRONFRONT.app`，`Info.plist` 中版本为 `0.45.0`。用户随后反馈已在独立程序里提前验证本轮双向切换，功能正确；这是用户的实机验收结论，本轮代理未另外采集独立程序的逐次按键录像。
4. TypeScript 主配置、逻辑配置编译与 `scripts/verify-core.cjs` 通过，覆盖网页和共享逻辑的回归；不将这些检查算作 Unity 玩法验证。

后续复测：用 Unity Hub 打开 `UnityProject`，进 `SampleScene.unity`，Play 后在菜单按 Enter 进入单人战局；用 `V` 在步兵两种人称间来回切换，靠近己方 T90/R4/U8 按 `E` 上车并再次按 `V`，U8 用 `F1/F2` 换座，离车后确认步兵模式恢复。要验证独立程序，先关闭旧进程，再运行当前构建，以标题页版本 `0.45.0` 为准。

## 仍未完成的还原工作

本轮只完成**当前可操控角色与三种载具**的人称切换。Unity 仍只实现 T90、R4、U8 的本地载具控制；其余五类原版载具、访客载具席位、完整跨机战斗、规模与性能等缺口继续按 [逐项对照及验收清单](UNITY_PARITY_038.md) 处理。不要把本轮视角验收写成整款游戏已还原。

## 工作树与分支注意事项

本轮开始前，工作树已有六个 Unity 编辑器自动改写、未提交的设置文件：`Assets/Settings/DefaultVolumeProfile.asset`、`PC_RPAsset.asset`、`UniversalRenderPipelineGlobalSettings.asset`，以及 `ProjectSettings/ProjectAuditorSettings.asset`、`ProjectSettings.asset`、`ShaderGraphSettings.asset`（均在 `UnityProject/` 下）。不要把这些整文件混入功能提交，也不要随意清除；`ProjectSettings.asset` 的本轮有效改动仅为 `bundleVersion: 0.45.0` 一行。构建产物在 `UnityProject/Builds/`，不纳入 Git。

## GitHub 递交地址与流程

| 用途 | 地址 |
| --- | --- |
| GitHub 仓库 | [YunfanShi/IRONFRONT](https://github.com/YunfanShi/IRONFRONT) |
| Unity 开发分支 | [codex/unity-port](https://github.com/YunfanShi/IRONFRONT/tree/codex/unity-port) |
| 本轮 0.45.0 提交 | [5ff0a6e：双向切换人称及交接](https://github.com/YunfanShi/IRONFRONT/commit/5ff0a6e45295a4efb44a2292d41c6c1e2f64b2cc) |
| 本交接文件的线上位置 | [docs/UNITY_HANDOFF_045.md](https://github.com/YunfanShi/IRONFRONT/blob/codex/unity-port/docs/UNITY_HANDOFF_045.md) |
| 需要发起审核时 | [比较 main 与 codex/unity-port](https://github.com/YunfanShi/IRONFRONT/compare/main...codex/unity-port?expand=1) |

本机 Git 远端名是 `origin`，推送地址为 `git@github.com:YunfanShi/IRONFRONT.git`。此前用户要求 Unity 二次开发保留在 `codex/unity-port`；提交和推送只进入该分支。比较链接用于查看差异或创建 Pull Request，合并 `main` 是另一步操作。

后续每次递交按以下顺序执行，特别要避免把 Unity 编辑器自动改写或其他正在开发的文件混入提交：

1. 进入仓库根目录，运行 `git branch --show-current`、`git remote -v` 和 `git status --short`，确认当前是 `codex/unity-port`，远端是上述仓库，并辨认本轮实际修改的文件。
2. 完成相应编译、编辑器或独立程序验证，再运行 `git diff --check -- <本轮文件>`。只使用 `git add --sparse <本轮文件>` 暂存明确属于该任务的文件，不使用 `git add .`；若 `ProjectSettings.asset` 同时含 Unity 自动改写，应只暂存经过检查的版本号片段。
3. 用 `git diff --cached --stat`、`git diff --cached --check` 和 `git diff --cached` 检查拟提交内容，然后执行 `git commit -m "<本轮改动说明>"`。
4. 运行 `git push origin codex/unity-port`。推送后执行 `git ls-remote origin refs/heads/codex/unity-port`，确认远端 SHA 与 `git rev-parse HEAD` 相同，再打开上方分支或提交链接查看文件。
5. 如需他人审核，从上方比较链接创建 Pull Request，目标选 `main`、来源选 `codex/unity-port`；在审查通过并得到合并指示前，保留开发分支上的提交。
