# 0.37.0 Unity 载具炮塔跟随与双准星

## 本次完成

- T90 主炮、R4 侦察车机枪、U8 运输车炮手位的机枪会持续追随玩家相机准星。转塔水平旋转和炮管俯仰分别限速；T90 为每秒 42° / 24°，R4 为 95° / 70°，U8 为 110° / 80°。炮管仰角也按载具分别限制。
- R4、U8 的机枪模型新增独立俯仰枢轴和炮口锚点。坦克沿用已有的独立炮塔、炮管与炮口结构。载具开火统一从当前炮口位置、沿炮口前方发射，不会因为玩家突然移动准星而让子弹或炮弹瞬间转向。上一版的弹速、重力下坠、射程和伤害规则继续生效。
- 驾驶坦克、侦察车或坐在运输车炮手位时，画面中心的青色 `AIM` 为玩家相机瞄准方向，琥珀色 `BARREL` 为当前炮口轴线投影到画面的位置。炮口射线会检测首个非本载具障碍物；两者重合表示炮塔已基本跟上。炮口方向超出屏幕时，指示被限制在画面边缘。U8 驾驶员没有武器控制权，因此只显示普通中心准星。
- AI 驾驶的 R4 和 U8 仍会在开火前对目标计算重力补偿与随机偏差，并将炮管与实际发射方向对齐。联机规则及房主权限没有改动。
- 坦克炮弹爆炸的范围伤害遇到地形或非凸网格碰撞体时，使用碰撞包围盒估计距离，避免 Unity 对不支持的 `ClosestPoint` 调用发出警告。
- 版本号从 `0.36.0` 升到 `0.37.0`。开发成果保留在 `codex/unity-port` 分支。

## 主要实现位置

| 文件 | 内容 |
| --- | --- |
| `UnityProject/Assets/Scripts/IPrototypeVehicle.cs` | 载具武器瞄准接口、相机瞄准点、炮口投影点和限速转炮计算 |
| `UnityProject/Assets/Scripts/PrototypeTankVehicle.cs`、`PrototypeTankVisual.cs` | T90 持续转炮及按实际炮管方向开火 |
| `UnityProject/Assets/Scripts/PrototypeScoutVehicle.cs` | R4 独立机枪俯仰、炮口锚点及实际炮口射击 |
| `UnityProject/Assets/Scripts/PrototypeTransportVehicle.cs` | U8 炮手位独立机枪俯仰、炮口锚点及实际炮口射击 |
| `UnityProject/Assets/Scripts/PrototypeRuntime.cs` | HUD 双准星绘制和炮口落点投影 |
| `UnityProject/Assets/Scripts/PrototypeBallisticProjectile.cs` | 非凸地形碰撞体的炮弹爆炸距离计算 |
| `UnityProject/Assets/Editor/PrototypeGameplaySmoke.cs` | T90、R4、U8 转炮限速与炮管俯仰，以及 T90 炮弹发射方向检查 |

## 操作与验证方法

1. 用 Unity 6000.5.10f1 打开 `UnityProject/Assets/Scenes/SampleScene.unity`，按 Play，从大厅进入单人战斗。
2. 部署至友方 T90，点击 Game 画面锁定鼠标。快速向左、右或高处移动视线：`AIM` 应立即保持画面中心，`BARREL` 应逐渐向其靠拢；第三人称检查炮塔水平转动、炮管俯仰。转炮过程中按左键，炮弹应沿 `BARREL` 所示炮口轴线飞行，随后继续受弹道下坠影响。
3. 分别进入 R4 驾驶位、U8 炮手位，重复水平和上下转动及开火检查。R4、U8 炮管应可见俯仰，机枪弹沿真实枪口方向发射。U8 驾驶位只负责驾驶，可用 `F2` 切炮手位。
4. 在 Play 模式执行菜单 `IRONFRONT → Verify Tank and Respawn`；Console 中应出现 `IRONFRONT smoke PASS`，其中包含每帧转动角度及炮弹发射方向断言。

## 本轮验证与边界

- 使用 Unity 6000.5.10f1 随附的 C# 编译器按项目程序集引用编译运行时代码和编辑器烟测，两次均退出码 0；TypeScript 主配置和 `tsconfig.logic.json` 类型检查也通过。
- 在 Unity Play 模式运行 `IRONFRONT → Verify Tank and Respawn`，Console 输出 `IRONFRONT smoke PASS`，0 条警告、0 条错误。这项烟测实测了 T90、R4、U8 的炮塔每帧转速及炮管俯仰、T90 炮弹沿真实炮口方向发射，以及原有复活流程。编辑器内的菜单鼠标控制未能自动完成进入战斗与双准星的目视检查，仍需按上方步骤手动观察。
- HUD 的 `BARREL` 表示炮管直线指向，不是考虑重力下坠后的实际弹着点；远距离射击仍需手动抬高炮口。镜头与炮口有位置差，近距离障碍物也会造成两准星分离。
- 联机快照仍未同步炮塔/炮管姿态，远端载具的炮塔视觉可能与本地射击方向不完全一致。单机与房主本地控制使用真实炮口方向。
