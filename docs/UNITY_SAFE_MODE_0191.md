# 0.19.1 Unity Safe Mode 导入修复与首次 Play 验收

## 基线与现象

本轮开始前本地与 GitHub `main` 均为 `e1040bc`、版本 `0.19.0`。用户通过 Unity Hub 把 `UnityProject/` 加入并用 Unity 6000.5.10f1 打开后，Unity 弹出 **Enter Safe Mode?**，提示项目包含脚本编译错误。进入 Safe Mode 后，Console 共显示 337 条编译错误。多数错误出自随旧模板附带的 `com.unity.ai.navigation@2.0.0` 与 `com.unity.collab-proxy@2.2.0`；移除后，又出现旧 `com.unity.inputsystem@1.12.0` 的编辑器代码与 Unity 6.5 废弃 API 不兼容。原型没有使用这些包：导航使用 `PrototypeNavigation.cs`，输入使用 Unity 内置的旧 `Input` API。

## 修复

- `Packages/manifest.json` 删除首轮原型未使用的 AI Navigation、Version Control、Input System、Visual Scripting、Timeline、IDE、测试框架等模板依赖，只保留与实际场景对应的 URP 17.5.0，并显式启用 `com.unity.modules.physics` 与 `com.unity.modules.audio`。
- 删除不再使用的 `Assets/InputSystem_Actions.inputactions` 及 `.meta`，移除 `EditorBuildSettings.asset` 中对应的输入动作引用。
- 首次精简后，Unity 编译器准确指出 `Collider`、`CharacterController`、`AudioSource`、`AudioClip` 所在的内置 Physics/Audio 模块未启用。补齐两个模块后，Unity 生成 `Assembly-CSharp.dll`，Console 编译错误清零。
- Unity 导入期间自动将 URP 模板资产及 ProjectSettings 升级至本机编辑器格式，更新 `packages-lock.json`。接受 Unity 的 URP Material upgrade 后，编辑器退出 Safe Mode。只保留这些由 Unity 实际迁移的工程设置，未提交 `Library/` 缓存。

## 实际验证

在同一台 Mac、Unity 6000.5.10f1 中打开 `Assets/Scenes/SampleScene.unity` 并点击 Play：

- Game 窗口显示地形、建筑、旗圈、枪模、第一人称准星和 HUD；Hierarchy 中出现 `IRONFRONT Unity Prototype`。
- 蓝红双方票数从 100 发生变化，B/C 据点进度和归属发生变化，玩家 HP 从 100 降到 91，表明 AI、占点、票数和受击循环实际运行。
- Play 期间 Console 显示 0 条错误、0 条警告。停止 Play 后场景保留在编辑器中，用户可再次点击 ▶ 测试。
- Unity 曾短暂出现“使用旧 Input Manager”的弃用提示；这是 Unity 6.5 对旧输入接口的迁移提示，本轮没有替换输入实现。后续可在完整 UI/控制移植时升级到新 Input System。

首次 Play 能运行不等于完整移植完成。本轮尚未验证独立桌面构建、长时间稳定性、玩家手动输入全流程或多机联机。现有 Unity AI 仍是首轮简化版本。

## 用户打开方式

Unity Hub 中打开 `UnityProject/`；在 Project 面板双击 `Assets/Scenes/SampleScene.unity`，再点击顶部 ▶ Play。若底部 Game 窗口显示为 `2x`，可以把 Scale 滑块调到 `1x`，HUD 会更适合当前编辑器窗口。按 Esc 释放鼠标，停止 Play 使用顶部同一个按钮。
