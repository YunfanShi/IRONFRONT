# 0.42.0 Unity 倒地视角与医疗兵救援

## 目标

玩家受致命伤进入 30 秒濒死窗口时，应倒在地上仍能观察周围。鼠标可改变视角，但不能像正常站立时无限旋转；医疗兵在玩家身边完成持续约 4 秒的扶起动作后才恢复生命。此前 Unity 保留了 30 秒濒死和呼救/弃救流程，但镜头仍在站立高度，整屏界面遮住战场，医疗兵接近后瞬间救起。

## 实现

- 倒地时玩家的第一人称镜头降到离角色脚底约 0.42 米，轻微倾斜，并收起武器；角色碰撞体也降低。倒地期间不能移动或射击。按住鼠标右键观察，水平角相对倒地瞬间的身体朝向限制在左右各 85°，即总计约 170°；俯仰限制为向下 35°、向上 45°。松开右键仍可用鼠标点击呼救或放弃按钮。
- 濒死界面缩成底部状态卡，战场保持可见；显示剩余救援时间和正在进行的扶起进度。呼救 `H`、按住空格加快放弃及点击按钮的现有操作保留。被救起或重新部署后恢复站立镜头、碰撞体与武器。
- 己方 AI 医疗兵靠近到 3.5 米内后持续施救 4 秒；医疗兵离开、死亡或施救中断超过 0.25 秒时，进度重新开始。多个医疗兵不会互相重置同一玩家的进度。施救期间医疗兵转向患者并保持救援姿态。单机玩家和房主模拟的远端玩家遵循相同持续时间。
- 联机远端玩家倒地后保留可见的趴卧模型，但关闭命中碰撞体；正式阵亡后才隐藏模型。访客通过现有战局快照中的濒死剩余时间显示队友的倒地姿势。访客自身仍由房主决定受伤、施救完成和生命值。

## 验证

Unity 6.5.10f1 的 `SampleScene` Play 菜单增加 `IRONFRONT → Verify Downed View and Revive`，检查倒地高度、左右 85° 限制、无法瞬间救起、连续约 4 秒后恢复站立镜头和关闭濒死界面。该测试在真实 Play 时间中等待医疗兵完成救援，而非直接写入复活状态。

在 Unity 6.5.10f1 的 `SampleScene` 中运行该菜单验证，Console 报 `IRONFRONT downed view and revive PASS: ground camera, 170-degree look arc, continuous four-second medic rescue`；实机画面显示倒地视角可看到战场、医疗兵救援进度 `0.4 / 4.0 s`，救援后恢复站立视角。Console 没有错误。

旧版 `IRONFRONT → Verify Tank and Respawn` 回归测试也通过，覆盖倒地、呼救、放弃救援、复活等待、载具部署和坦克驾驶。网页逻辑的 TypeScript 类型检查与 `scripts/verify-core.cjs` 通过。

通过 `IRONFRONT → Build macOS Standalone` 构建到 `UnityProject/Builds/macOS/IRONFRONT.app`，Unity Console 报构建成功，`Info.plist` 版本为 `0.42.0`。重新签署构建产物后，`codesign --verify --deep --strict` 通过；直接运行应用包内的可执行文件，大厅画面显示 `0.42.0`。当前执行环境的 Launch Services 对本游戏及系统自带的 `Calculator.app` 都返回 `kLSNoExecutableErr`，因此本次没有把双击打开 `.app` 计为已验证。
