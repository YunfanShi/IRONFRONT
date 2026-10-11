# IRONFRONT Unity 姿态、八类载具与座位交接（0.46.0）

2026-10-11，基于 0.45.0 的 `codex/unity-port` 开发，继续保留该分支，不合并 `main`。本轮分三个子智能体分别负责人物关节姿态、五类载具、统一座位；主智能体负责 main 参照读取、场景/HUD/部署接入、整合审查与 Unity 实测。

## 人物姿态

`C` 蹲下（冲刺时滑铲），`Ctrl` 趴下，`V` 在第一/第三人称之间切换。新模型通过髋、膝、踝、肩、肘、躯干、头部关节调整姿态，不再靠 Y 缩放压扁身体。

- 站立保持完整身体高度。
- 蹲下实际屈膝，躯干略前倾，靴底贴地。
- 趴下躯干放平、腿向后伸、头抬起，枪保持朝前。
- 行走、蹲行、爬行有各自步态；背包和职业装备随躯干。倒地与救援继续使用完整比例的地面/恢复姿态。
- M2 第三人称显示骑手，驾驶员握把，乘客手放腿上；第一人称隐藏自身身体，车上不显示步兵枪。

模型实现：`UnityProject/Assets/Scripts/PrototypeSoldierVisual.cs`。验证和 PNG 导出：`Assets/Editor/PrototypeInfantryStanceSmoke.cs`；导出在 `UnityProject/Builds/Verification046/`。

## 八类载具及席位

以 `main` 的 `Vehicle.ts`、`Flight.ts`、`Battle.ts` 和载具渲染为参照；读取 main 时补取了部分克隆缺少的文件，没有合并分支。

| 载具 | 席位 | 本地操作/武器 |
| --- | --- | --- |
| T90 BASTION | 4 | F1 驾驶兼主炮；F2 独立 MG；F3/F4 乘客 |
| R4 SCOUT | 2 | F1 驾驶及 MG；F2 炮手 MG |
| U8 ROVER | 4 | F1 无武器驾驶；F2 MG；F3/F4 乘客，可被现有 AI 后排占用 |
| V12 LANCER | 4 | F1 驾驶、30mm HE/同轴 MG；F2 独立 MG；F3/F4 乘客 |
| A40 WATCHTOWER | 4 | F1 驾驶、35mm 防空炮/制导导弹；F2 MG；F3/F4 乘客 |
| H8 KESTREL | 4 | F1 飞行、70mm HE/制导导弹；F2 MG；F3/F4 乘客 |
| J20 STRATUS | 1 | F1 飞行、25mm AP/制导导弹 |
| M2 COURIER | 2 | F1 驾驶；F2 乘客；两席无武器 |

每方八辆、25 席，总计 16 辆、50 席。新增五辆停在双方基地独立车位，保留旧六辆的列表 ID。单人 8v8 默认新增车辆空置供玩家使用，避免同时增加五套额外 AI 火力；`SetAiControlled` 提供基本目标移动、导航/飞行和射击路径，完整 AI 组员编制与协同仍需后续长局验收。

靠近友方载具 `E` 上/下车；离地超过 3 米的飞机不能从地面按 E 登入。`F1–F4` 选择驾驶/飞行、炮手或乘客席，超出席位或目标席已被 AI 占用会拒绝，原席位保留。HUD 左下显示席位、YOU/FREE/AI 状态及反馈。只有驾驶/飞行席能控制运动，武器按各席角色授权；换座保留玩家的人称选择，每席有独立眼点。战术部署列表可直接选择空闲具体席位，使用 `VEHICLE-{id}-{seat}`；旧 `VEHICLE-{id}` 继续代表驾驶席。

新增车辆 `1/2` 切主武器/同轴或导弹，左键开火。A40/H8/J20 可右键快捷发射导弹；须持续瞄准约 1.5 秒锁定，导弹有 7 秒冷却，A40 只锁离地至少 5 米的航空载具。V12/A40 `X` 烟幕，H8/J20 `X` 热诱弹：作用 5 秒、冷却 24 秒，解除锁定和导弹制导；HUD 显示 READY/ACTIVE/剩余冷却及锁定、导弹来袭警告，只有 F1 可释放。弹体按各自速度、重力、爆炸范围和装甲系数计算命中与伤害。A40 F1 视角/炮管可抬头 77°，F2 机枪视角按 32°限位。

飞行：`W/S` 控速，`A/D` 转向/侧移，鼠标朝向辅助操纵；`Space` 起飞/爬升，`Ctrl` 下降及喷气机刹车，`Shift` 加力。喷气机依赖前进速度产生升力、转弯倾斜；直升机可悬停、倒退、侧移。飞行席相机保存世界航向，机体朝鼠标目标收敛；松手后不会因相机跟随机体而继续盘旋，同席重选和 V 不重置航向。高空离机保留实际高度，自动以 5 m/s 缓降；落地或再上车解除。航空第三人称相机后退 14 米并抬高，避免落在尾翼/旋翼内部。

新增车辆按 main 配置修复、损毁和延时复活：M2 70 秒，其余新增四类 90 秒；毁车消灭乘员并释放席位，车辆损失/乘员阵亡依原有票数路径处理。复活等待出生车位清空；地面车辆碰撞忽略高空航空载具。

## 代码入口

- `PrototypeVehicleSeats.cs`：共用席位契约、角色、占用账本及车辆间距。
- `PrototypePlayer.cs`：E/F1–F4、人称/席位切换、具体席位部署、缓降及摩托显隐。
- `PrototypeCombinedArmsVehicle.cs`、`PrototypeVehicleDefinition.cs`：新增五类玩法与 main 参数。
- `PrototypeCombinedVehicleVisual.cs`：八轮步战车、四联炮雷达防空车、旋翼直升机、翼面喷气机、两轮摩托及仪表/席位模型。
- `PrototypeVehicleProjectile.cs`、`PrototypeVehicleCountermeasure.cs`、`PrototypeVehicleRoute.cs`：物理弹/制导、烟幕热诱弹、基本车辆导航。
- `PrototypeTankVehicle.cs`、`PrototypeScoutVehicle.cs`、`PrototypeTransportVehicle.cs`：旧三车接入共用席位。
- `PrototypeRuntime.cs`、`PrototypeDeployment.cs`：双方车辆生成、HUD、席位部署、现有快照的新增车位置/生命读取。

## 验证记录

Unity 6000.5.10f1，2026-10-11 最终 Play 验证：

| 菜单检查 | 最终结果与覆盖 |
| --- | --- |
| Verify Infantry Stances | PASS：完整站姿、实际屈膝、水平趴姿、地面接触、爬行、装备、救援与人称恢复 |
| Verify All Vehicle Seats | PASS：八类共 25 个席位切换、互斥占用、驾驶/开火权限、每席两种人称、AI 占座、释放；含 A40 真实鼠标高仰角和地面 E 拒绝高空飞机 |
| Verify Five Added Vehicles | PASS：驾驶、起飞/转向/下降/落地/跳伞、武器实际伤害、AA 锁定/导弹/热诱弹、修复、乘员损失、出生车位阻挡及复活；含 H8/J20 一次 30°鼠标输入后九秒相机/飞控联动收敛 |
| Verify 0.46 Scene and Seat Deployment | PASS：16 辆、50 席、出生车位无重叠、具体乘客席部署、权限、换席与下车释放 |
| 原有相机、倒地救援、坦克/复活检查 | PASS：T90/R4/U8 人称切换与退出恢复、四秒连续医疗兵救援、坦克转角驾驶/炮冷却/弹射和部署 |

姿态六视图及五张独立载具截图已目视检查，位于 `UnityProject/Builds/Verification046/`；同目录 `validation.txt` 保留最终 PASS 摘录。Roslyn 全部运行时/Editor 脚本编译、TypeScript `--noEmit`、逻辑编译和 `scripts/verify-core.cjs` 均通过；后者仅作为原网页逻辑回归，不代替 Unity 玩法验收。功能文件范围 `git diff --check` 通过。

通过 `IRONFRONT/Build macOS Standalone` 成功构建 `UnityProject/Builds/macOS/IRONFRONT.app`，产物约 118 MB，Info.plist 版本 `0.46.0`。独立程序已启动，目视确认 0.46.0 菜单、单人 8v8 开局及 V12 驾驶席/四席状态/烟幕 HUD。用户随后正在操作程序，保留其游戏状态；本记录没有将全部八车独立程序长局记为已验收。

## 范围与后续

本轮是本地单人姿态与载具席位扩展。Unity LAN 访客席位、完整乘员/炮塔/俯仰/滚转的网络同步、两名真人同车、AI 车组与战局平衡、完整独立程序八车长局及性能还需独立验证；对照表的 S5 保留“局部”，N3 仍未通过跨机验收。

已有六个 Unity 自动改写设置文件继续保留，不整文件混入功能提交；本轮 `ProjectSettings.asset` 的有意改动只有 `bundleVersion: 0.46.0`。构建和截图位于 Git 忽略的 `UnityProject/Builds/`。0.46.0 功能提交保留在 `codex/unity-port`，不合并 `main`；0.47.0 音乐不在这次提交范围内。
