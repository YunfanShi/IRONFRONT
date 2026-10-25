# Unity 0.63.1 — 大厅待机小幅调整并应用

2026-10-25。用户在 0.63.0 待机预览基础上要求「稍微调整一点，并应用」。本次保留已有姿势方向，作轻微收敛。

- 身体位移、腰胸转动幅度降低约 18–20%，头部转动幅度降低约 29%。
- 循环播放系数由 0.75 调为 0.68，完整循环由约 3.33 秒变为 3.68 秒。呼吸更缓，减少重复点头和晃枪。
- 长枪下倾角从 30° 调为 28°，双肘弯曲方向略向身体收拢；手枪基础角度保留。
- 继续保持双手握点、固定双脚和原骨骼长度，应用于大厅与配装中的真实角色。

仅调整 `Assets/Scripts/PrototypeMenuOperatorPose.cs` 的参数，原模型、动画采样文件、战斗动作、界面布局与存档逻辑保留。版本号统一更新为 0.63.1。旧 0.63.0 与更早应用保留。

新程序：`UnityProject/Builds/LobbyPose-0.63.1/IRONFRONT.app`。
证据、修改前备份、保留哈希：`art/characters/lobby-pose-tune-2026-10-25/`。

继续沿用 0.63.0 的官方 CC0 Quaternius 站立身体循环；未重新下载或购买动画。资源和适配说明见 `UNITY_HANDOFF_063.md`。

仓库仍保留现有 codex/unity-port，任务开始时 HEAD 与本地 origin/main 相差 36/5，并有大量历史未提交成果。本次未自动合并或推送这些历史修改。

## 实际验证

- Unity 6000.5.10f1 StandaloneOSX 构建 PASS，见 build.log。新应用 578,418,131 字节。
- 开发版 pose/results.txt 为 PASS，RuntimeErrors=0；七把枪各 201 个采样点，实际 LateUpdate 和两个完整循环。最大握点偏差 0.000291 米，脚踝漂移 0.000249 米，循环接缝相邻采样差 0.000168 米。
- 开发版 ui/results.txt 为 PASS，RuntimeErrors=0；五种窗口尺寸、四兵种、装备选择、保存返回、原菜单与真实开局流程。复用现有回归，不新增测试逻辑。
- 检查了实机正侧面及循环采样，视频 lobby-idle-0.63.1.mp4 按正常时间以 30 fps 编码。新应用已在本机打开。
- 710 个基准文件哈希未变，包括两套旧应用、现有模型、原 UI 和战斗脚本；ProjectSettings 本次只变更 bundleVersion。
- 原 PrototypeWeaponArtProbe 两项废弃 API 编译警告仍在；新代码无编译错误。
