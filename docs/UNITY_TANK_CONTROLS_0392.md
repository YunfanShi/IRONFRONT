# 0.39.2 Unity 坦克驾驶与主炮修复

## 目标与基线

针对 T90 BASTION 在战斗中偶尔按 W 不前进、按左键没有炮弹的反馈，从 `codex/unity-port` 的 0.39.1 继续修复。开始时本地 `HEAD` 为 `a70d130`，六个 Unity 设置和渲染资产已有本机序列化改动；这些改动不属于本次修复。版本按仓库的小问题修复规则升至 0.39.2。此版本号只标识一次修复，不改变 [网页与 Unity 还原对照](UNITY_PARITY_038.md) 的验收状态。

## 复现与原因

1. `PrototypeTankVehicle.Update` 原来把 `Cursor.lockState == Locked` 当作驾驶和主炮输入的总开关。切换窗口或鼠标锁定丢失后，W 和左键同时被跳过；`PrototypePlayer.Update` 的第一下左键只重新锁定鼠标并返回。结果是车可坐、血量正常，却暂时不能走或开炮。
2. 坦克原来用 `PrototypeLayout.LineBlocked` 检查每帧移动。这个函数为视线和导航把建筑矩形向四周扩展车体半径；在建筑斜角外侧，真正的圆形车体没有撞到建筑，线段却仍落入扩展矩形。例如工厂 `(-269,-170)` 的东北角，车心在 `(-247.8,-152.3)`，半径 3.5 米时 `Collides` 判断可通行，但旧的 `LineBlocked` 判断受阻。碰撞分支每帧把速度清零，因此向外开也像 W 失效。
3. 主炮有 2.4 秒射击间隔。网页原版 `src/vehicles/Vehicle.ts` 的 T90 也配置 `weaponInterval: 2.4`；原 Unity HUD 没有装填提示，连续点击时玩家无法区分冷却与输入失灵。开火本身还要求驾驶员和炮口存在。

## 修改

- 战斗进行、驾驶员存活、未暂停且前端菜单未打开时，坦克持续读取油门和转向；鼠标解锁不再截断这两个输入，也不再吞掉用于回到游戏的首次左键。鼠标解锁期间不根据自由鼠标位置调整炮塔；锁定后恢复瞄准。
- 新增 `PrototypeLayout.CirclePathBlocked`，在短距离移动线上按 0.25 米采样真实圆形碰撞。坦克改用此检查代替扩展矩形线段检查；建筑实体、地图边界和其他载具碰撞仍有效。
- HUD 在准星下方显示 `CANNON READY` 或 `RELOAD x.x s`，沿用原 2.4 秒射击间隔。
- 编辑器 `Verify Tank and Respawn` 新增斜角误判的回归点，检查未锁定鼠标时仍能驶离；通过同一输入方法验证首次左键生成实体炮弹、第二次立即点击被装填时间限制。

## 验证记录（2026-10-11）

| 环境 | 实际结果 | 验证范围 |
| --- | --- | --- |
| Unity 6000.5.10f1，SampleScene Play，`IRONFRONT → Verify Tank and Respawn` | Console 输出 `IRONFRONT smoke PASS`，0 错误、0 警告 | 实际入座、移动、斜角驶离、鼠标未锁定时的输入方法、炮弹生成及冷却、毁车下车 |
| Unity 编辑器 `IRONFRONT → Build macOS Standalone` | Console 输出 `IRONFRONT macOS build succeeded`，0 错误、0 警告 | 独立程序构建成功 |
| 新构建的 `Builds/macOS/IRONFRONT.app` | `CFBundleShortVersionString` 为 `0.39.2`；关闭旧实例后重新启动，主菜单显示 `0.39.2` | 确认新独立程序能启动并显示新版本 |

编辑器检查通过反射调用输入方法，不等同于真人在独立程序中长时间驾驶。当前自动界面输入没有完成一整段按住 W 的实机操作；如果仍在某个具体地形位置发生卡住，需要记录坐标、朝向和附近载具，再针对该位置补复现。联机访客驾驶坦克本来未实现，本次修复仅作用于单机与房主驾驶。
