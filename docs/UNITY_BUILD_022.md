# Unity macOS 桌面构建与验收

这份文档记录 `codex/unity-port` 的构建入口及其验收边界。构建脚本位于 `UnityProject/Assets/Editor/PrototypeBuild.cs`，仅在 Unity Editor 中编译。它检查原型场景已导入且在 Scene List 中启用、所有启用场景及当前十一个原型脚本已导入，并确保构建所需的 URP 材质资源存在，然后构建 macOS Standalone。构建产物固定为 `UnityProject/Builds/macOS/IRONFRONT.app`；`Builds/` 已被 Git 忽略。

## 在 Unity 编辑器中构建

1. 用 Unity **6000.5.10f1** 打开仓库中的 `UnityProject/`，等资源导入和脚本编译结束。先处理 Console 中的编译错误。
2. 在菜单栏选择 **IRONFRONT > Build macOS Standalone**。
3. Console 出现 `IRONFRONT macOS build succeeded` 后，在 Finder 打开 `UnityProject/Builds/macOS/IRONFRONT.app`。若失败，Console 会写出具体缺少的场景或脚本，或 Unity Build Report 的结果与错误数。

当前工具始终输出 macOS `.app`，不会修改网页游戏构建，也不会发布到 GitHub。已有同名 `.app` 可能在构建失败后仍留在目录中；只以本次 Console 的成功记录作为本次构建通过的依据。

## 从命令行构建

先关闭同一工程的 Unity 图形编辑器，避免项目锁冲突。在仓库根目录执行：

```sh
cd "/path/to/IRONFRONT"
"/Applications/Unity/Hub/Editor/6000.5.10f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -quit \
  -projectPath "$(pwd)/UnityProject" \
  -executeMethod Ironfront.UnityPrototype.Editor.PrototypeBuild.BuildMacOS \
  -logFile "/tmp/ironfront-unity-build.log"
```

命令正常完成时退出码为 `0`，日志中应出现 `IRONFRONT macOS build succeeded` 和完整输出路径；验证失败或构建失败时以非零退出并记录异常。可用 `tail -n 80 /tmp/ironfront-unity-build.log` 查看末尾结果。运行前需要本机 Unity Editor 已激活并具有 macOS 构建能力。

## 独立应用冒烟验收

每次首次构建以及改动场景、输入或运行时后，直接启动 `.app` 并检查：

1. 应用打开后显示战场或开始界面，没有黑屏或崩溃；若出现开始界面，选择兵种并进入战局。
2. 战场能看到地形、建筑、五个据点、玩家视角及 HUD；双方票数和据点进度开始变化。
3. 点击窗口捕获鼠标，使用 `WASD` 移动、鼠标瞄准和左键射击；`R` 换弹、`Shift` 冲刺、空格跳跃、`Esc` 释放鼠标。
4. 观察双方 AI 移动、交火、小队命令及掩体/搜索状态，确认票数持续变化，直到一方出现胜利提示。
5. 检查运行日志是否有异常。macOS 的 Unity Player 日志通常位于 `~/Library/Logs/IRONFRONT/IRONFRONT Unity Prototype/Player.log`；若产品名设置改变，以 Unity 当前 Player Settings 为准。

这份清单是实际玩家窗口验收，不能由脚本静态检查替代。双实例联机、跨机连接、长期平衡及其他平台构建需要单独测试。

## 本轮验证记录

- 已静态核对 `ProjectVersion.txt` 为 Unity 6000.5.10f1；`EditorBuildSettings.asset` 启用了 `Assets/Scenes/SampleScene.unity`，十个原型脚本及对应 `.meta` 文件存在，输出目录已有 Git 忽略规则。
- 使用本机 Unity 6000.5.10f1 随附 Roslyn、.NET Standard 2.1、UnityEngine 与 UnityEditor 程序集静态编译构建脚本，通过。
- 在 Unity 编辑器执行菜单构建，首次构建成功但独立程序黑屏。Player.log 指向运行时 `Shader.Find` 返回空；改为构建前生成并保存 `Resources/Materials/PrototypeLit` 与 `PrototypeUnlit`，运行时加载这些材质后再次构建成功。
- 第二次构建产出约 116.6 MB 的 `IRONFRONT.app`。实际启动独立程序后看到出击菜单及战场；按 `4`、Enter 以工程兵出击，按 `X` 后 HUD 护甲为 35、支援次数为 1；票数和据点继续变化。此次运行的 Player.log 未出现异常。尚未在独立程序中完整测试移动、射击与一局结束，也未进行双实例联机或跨机测试。
