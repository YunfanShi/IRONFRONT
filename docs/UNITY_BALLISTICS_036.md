# 0.36.0 Unity 坦克炮击与枪械弹道

## 本次完成

- T90 驾驶员在鼠标锁定的战斗状态下按住鼠标左键开炮，间隔 2.4 秒。炮塔追随准星水平旋转，炮管按目标方向俯仰；开火时炮口出现短暂闪光和后坐，能看到炮弹及其飞行尾迹。炮弹从真实炮口出发，不再在按下鼠标时立即命中。
- 炮弹初速为 170 世界单位/秒，最大飞行距离 230，受到 Unity `Physics.gravity` 影响。落地、撞墙或命中单位时产生半径 4.5 的爆炸，中心伤害 145，随距离线性衰减；不会伤害友方。瞄准点没有自动补偿弹坠，远距离开炮需要抬高炮口。
- 步兵玩家、AI 士兵、由房主控制的远端玩家，以及 R4 侦察车和 U8 运输车的机枪，统一发射有飞行时间的子弹。每个子弹按 `p + v·dt + ½g·dt²` 前进，并对每段轨迹做碰撞射线检测，避免高弹速穿过薄墙。步枪按飞行距离计算原有射程衰减；车辆仍承受枪械伤害的 55%。
- AI 对可见目标预估飞行时间与重力下坠，同时保留原有的随机失准和 29% 伤害系数。玩家需要自己补偿远距离弹坠。枪口散布、开镜缩圈、弹药消耗、换弹计时及配装数据继续使用已有规则。
- 仓库、网页客户端、房主状态接口和 Unity 包版本升至 `0.36.0`；Unity 房间协议未改动。开发成果继续保存在 `codex/unity-port`。

## 弹速配置

单位为 Unity 世界单位/秒。各枪共用 `Physics.gravity`，因此弹速越慢，在同一水平距离的下坠越大。

| 武器 | 初速 | 最大射程 |
| --- | ---: | ---: |
| IF-27 卡宾枪 | 360 | 165 |
| M89 精确射手步枪 | 500 | 220 |
| VX-9 黄蜂冲锋枪 | 190 | 105 |
| H60 哨兵轻机枪 | 390 | 190 |
| BR-44 阿特拉斯战斗步枪 | 440 | 185 |
| S12 子午线狙击步枪 | 680 | 310 |
| P8 接力手枪 | 160 | 75 |
| R4 侦察车机枪 | 430 | 110 |
| U8 运输车机枪 | 370 | 100 |
| T90 主炮 | 170 | 230 |

忽略空气阻力、且炮口保持水平时，距离 `d` 的近似下坠为 `|g|·(d/v)²/2`。例如 IF-27 在 100 单位约下坠 0.38，S12 约 0.11，T90 炮弹约 1.70。真实命中还受枪口高度、炮管俯仰、地形和移动目标影响。

## 主要实现位置

| 文件 | 内容 |
| --- | --- |
| `UnityProject/Assets/Scripts/PrototypeBallisticProjectile.cs` | 子弹与炮弹飞行、逐段碰撞、弹道尾迹、命中和爆炸 |
| `UnityProject/Assets/Scripts/PrototypeInfantryRoles.cs` | 每种步兵枪械的初速 |
| `UnityProject/Assets/Scripts/PrototypeTankVehicle.cs` | 驾驶员输入、炮塔瞄准、炮弹发射和射速限制 |
| `UnityProject/Assets/Scripts/PrototypeTankVisual.cs` | 可俯仰炮管、炮口锚点和后坐动作 |
| `UnityProject/Assets/Scripts/PrototypePlayer.cs`、`PrototypeBot.cs`、`PrototypeRemotePlayer.cs` | 步兵射击接入同一弹道系统 |
| `UnityProject/Assets/Scripts/PrototypeScoutVehicle.cs`、`PrototypeTransportVehicle.cs` | 玩家与 AI 的载具机枪接入弹道系统 |
| `UnityProject/Assets/Editor/PrototypeBallisticsSmoke.cs` | 运行态验证弹体移动、重力、不同弹速及薄障碍碰撞 |

## 操作与验证方法

1. 在 Unity 6000.5.10f1 打开 `UnityProject/Assets/Scenes/SampleScene.unity`，按 Play，在大厅按 Enter 进入单人战斗。用 `1/2` 切换枪，向中远距离的同一目标射击；观察子弹尾迹和弹着点，下坠应随距离增加，P8/VX-9 比 IF-27/S12 更明显。开镜不会取消弹坠。
2. 在 Play 模式执行 `IRONFRONT → Verify Ballistics`。该烟测从高空同时发射手枪弹、炮弹和一枚射向薄方块的手枪弹；Console 应显示 `IRONFRONT ballistics smoke PASS`。
3. 在 Play 模式执行 `IRONFRONT → Verify Tank and Respawn`。该烟测复活到友方 T90、测试驾驶并检查按开火流程生成了炮弹；Console 应显示 `IRONFRONT smoke PASS`。
4. 手动部署到 T90 后点击游戏画面锁定鼠标，鼠标移动会转动炮塔并俯仰炮管，按住左键可连续按冷却发射；近处墙体会截住炮弹，远处地面落点低于炮管的直线瞄准点。按 Esc 返回鼠标控制。

## 本轮验证

- 用 Unity 6000.5.10f1 随附的 C# 编译器，按工程程序集引用编译 `Assembly-CSharp` 与 `Assembly-CSharp-Editor`，包含新增弹道脚本和烟测脚本；两次编译均以退出码 0 结束，没有 C# 错误或警告。
- TypeScript 主配置 `tsc --noEmit` 与逻辑配置 `tsc -p tsconfig.logic.json --noEmit` 均通过。编辑器自动改写的 URP 与 ProjectSettings 其余本机差异没有纳入提交。
- Unity 编辑器窗口控制在本轮中途失去可用窗口，因此尚未实际运行两项 Play 模式烟测。临时工程的无界面完整构建因 Unity Licensing 服务初始化异常中止；该环境问题没有产生新的构建包。以上两项仍需在可用的 Unity 编辑器中按前述步骤复测。

## 当前边界

弹道只模拟统一重力，不计空气阻力、风和穿透。炮弹在最大射程后消失，不在射程末端爆炸；炮击没有单独的装填弹药库存。工程兵火箭仍沿用旧射线实现。炮塔姿态与炮弹轨迹首先按单人及房主权威模拟，现有联机快照尚未传输它们的完整视觉状态。
