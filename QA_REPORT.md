# 0.20.0 QA：Unity 小队指挥

本轮在 `codex/unity-port` 的 `c1ac5b9` 基线上开发；`main` 文件内容保持移植前状态。Unity 七个 C# 文件使用 Unity 6000.5.10f1 随附 Roslyn、.NET Standard 2.1 和 UnityEngine 引用静态编译通过。独立 .NET 场景桩运行实际指挥官代码，初始分兵、夺点后推进、路线失败改派、争夺中增援、目击影响防守五个场景通过。

本机 Unity 6000.5.10f1 实际刷新脚本并 Play `SampleScene`：初始双方小队分配到 B/C，随后红队命令改变至 A；据点进度、票数、玩家 HP 和小队存活人数实际变化。首次 Play 发现 2x Game 窗口下 HUD 与底部提示重叠；加入按逻辑分辨率缩放后再次 Play，面板分开、提示完整可读。Unity Console 显示 0 错误、0 警告。仍未验收独立桌面构建和长时间平衡。

网页回归：`tsc --noEmit` 通过，Vitest 13 个测试文件、117/117 测试通过，Vite 生产构建通过；构建仍提示已有 Three.js 分块超过 500 kB。LAN 双客户端自动回归通过，覆盖房主权威、重连、房主移交、倒计时取消、AI 接管、对立阵营和八人容量。本机沙盒首次禁止监听临时测试端口，允许回环端口后运行通过。

# 0.19.1 QA：Unity Safe Mode 修复

本轮开始前本地与 `origin/main` 均为 `e1040bc`、版本 `0.19.0`。Unity 6000.5.10f1 首次打开时出现 337 条编译错误，来自未使用的旧模板包。移除不兼容包后 Console 缩减为 5 条原型源代码错误，全部是未显式启用内置 Physics/Audio 模块；补齐后 `Assembly-CSharp.dll` 编译成功，编辑器退出 Safe Mode。

在 Unity Editor 打开 `Assets/Scenes/SampleScene.unity` 并 Play，观察到地形、建筑、枪模与 HUD；双方票数从 100 变化，B/C 据点进度变化、玩家 HP 下降。运行中 Console 为 0 错误、0 警告。网页 TypeScript 检查、Vitest 13 文件 117/117、Vite 生产构建及真实双客户端 LAN 集成测试均通过；现有约 502 kB 的 Three.js 分块提示仍在。独立构建、长期性能、玩家输入与跨机联机未验证。工程修复过程见 [docs/UNITY_SAFE_MODE_0191.md](docs/UNITY_SAFE_MODE_0191.md)。

---

# 0.19.0 QA：Unity 首轮移植原型

## 基线与范围

开始前本地 `main` 与 `origin/main` 均为 `55eecbb`，网页版本 `0.18.0`。新增 Unity 模块按 `ForAgent.md` 升至 `0.19.0`。详细设计和运行方法见 [docs/UNITY_PROTOTYPE_019.md](docs/UNITY_PROTOTYPE_019.md)。本轮的 Unity 代码是实验性首轮原型，网页游戏继续运行。

## 实际验证与限制

- Unity 六个 C# 源文件使用 Unity 6000.5.10f1 安装目录自带的 .NET SDK、Roslyn、UnityEngine 与 .NET Standard 引用进行静态编译，通过。
- 网页 `tsc --noEmit` 通过；Vitest 13 个文件、117/117 项通过；Vite 生产构建通过。约 502 kB 的 Three.js 分块仍有既有的体积提示。
- 网页逻辑脚本通过：8、16、32、64 人战局均结束，导航失败为 0；默认 32v32、500 票战局模拟 780.5 秒结束，导航失败为 0。
- LAN 集成脚本在允许本地监听端口后通过：两个真实 WebSocket 客户端、权威事件、同席位重连、房主移交、倒计时取消、AI 接管、对立阵营、权限和八人上限。初次在受限沙盒中运行时因端口 `EPERM` 停止；放宽本地端口限制后重跑通过。
- 本机 Unity Editor 命令行导入未完成：第一次 Package Manager 本地 socket 被沙盒拒绝；第二次可连接 Package Manager，但 `Unity.Licensing.Client` 初始化反复报 `System.ObjectDisposedException: IServiceProvider`。因此 Unity 编辑器导入、Play、图形和打包 **未验证**。
- Unity 原型不宣称通过网页游戏的玩法回归测试；网页浏览器可视化套件本轮未重跑，因为网页界面只改了展示版本号。

---

# 0.18.0 QA：房间界面与局域网连接恢复

## 基线

本轮开始前本地 `main` 为 0.17.0 的 `8a76609508956eaae158f7d5c232e96fe2df2649`，工作区干净；远端 `origin/main` 仍为 `329a0d2289d7023ff78a262e1c34aad9a6d1c048`，原因是前一轮 GitHub HTTPS 推送没有可用凭据。本轮按新增房间功能将版本升至 0.18.0。设计与操作说明见 [docs/LAN_ROOM_018.md](docs/LAN_ROOM_018.md)。

## 实际验证

- `tsc --noEmit` 通过；`vitest run` 13 个文件、117/117 通过；Vite 生产构建通过。Three.js 分块略超 500 kB 的既有提示仍在。
- `tsc -p tsconfig.logic.json` 和 `scripts/verify-core.cjs` 通过。8、16、32、64 人自动战局全部结束且导航失败为 0；默认 32v32、500 票战局模拟 780.5 秒结束，29 次据点变更、63 次阵亡、三种大事件均出现，导航失败 0。
- `scripts/test-lan.cjs` 使用真实服务和 WebSocket 验证双客户端权威状态、同 ID 恢复、掉线保留席位、房主移交、房主令牌一次性使用、邀请码不在公开状态接口泄漏、倒计时回退、载具席位失效后状态清理、阵营分配和 8 人上限。
- 固定源码后的完整 Chrome 浏览器套件 41 项通过，1 项因本机缺少 MP3 文件按测试条件跳过，耗时约 3.7 分钟。其后对一次性房主令牌、连接表单状态和防空 HUD 文案做了局部调整；相关的创建/加入、阵营、窄屏、自动重连、房主移交、连接诊断和工程兵装备浏览器用例均定向重跑通过。
- 房间卡片、准备室和 390 像素宽屏幕已截图目视检查。新的创建/加入面板、逐人状态和连接信息可读；窄屏为单列，无横向溢出。

## 测试过程与边界

第一次完整浏览器运行期间修改了客户端源码，导致旧步兵镜头测试所用页面热重载，该次结果为 40 项通过、1 项因缺 MP3 跳过、1 项失败；停止修改源码后，该失败项单独重跑通过，固定源码的第二次完整套件 41 项全部通过。后续局部修改均已用相应 LAN 集成和 Chrome 用例验证。

断线恢复只覆盖同一房主进程仍存活且 30 秒内重新连接的情况。两浏览器和本机局域网 IP 验证不能替代朋友在另一台电脑上的防火墙、Wi-Fi 隔离和系统权限验收。

---

# 0.17.0 QA：小队 AI、导航与合成作战

## 基线与版本

开始前 GitHub `main` 与本地都为 `329a0d2289d7023ff78a262e1c34aad9a6d1c048`、`0.16.0`。本轮新增功能按 ForAgent.md 升至 `0.17.0`。实现说明见 [docs/AI_SQUAD_017.md](docs/AI_SQUAD_017.md)。

## 实际验证

- `tsc --noEmit` 通过；`vitest run` 13 个文件、117/117 测试通过。
- `tsc -p tsconfig.logic.json` 和 `scripts/verify-core.cjs` 通过。8、16、32、64 人自动战局均结束，导航失败均为 0。默认 32v32、500 票战局在模拟 780.5 秒结束：29 次据点归属变更、63 次阵亡、6231 次步兵射击、三种大事件均实际触发，导航失败 0。数值是确定性脚本场景的结果，不是玩家设备实时性能承诺。
- Vite 生产构建通过；Three.js 分块略超 500 kB 的既有提示仍在。
- `scripts/test-lan.cjs` 通过：两个真实客户端、权威位置和事件、独立弹药、掉线接管、房主权限、阵营和 8 人容量。
- 正常 Chrome 渲染的全套浏览器测试 38/38 通过，另 1 项本地 MP3 测试因所需素材不存在按测试条件跳过。最终局部调整后，精锐配置/F3、人物画面与座位、LAN 房间准备及指挥建议相关测试再次通过。强制软件 WebGL 的早期测试出现超时，改用正常 Chrome 后重跑通过。
- 新增测试实际覆盖路径段验证、不可达目的地、导航恢复报告、听觉误差落点、小队集结超时、意图优先级与持续时间、载具掩护、防空双发命中，以及常规/精锐自动战局。

## 行为诊断样本

固定 seed 505、普通难度、32v32、500 票，分别推进到模拟 200 秒：常规配置小队凝聚度 64.1%、占点任务完成率 5.9%、导航失败 0；精锐配置凝聚度 65.6%、占点任务完成率 6.5%、导航失败 0。精锐配置追踪移动编队槽位，因此原始目的地坐标变化更频繁（33.7 对 28.5 次/机器人/分钟）；这项计数本身不能证明移动方向更稳定。两种配置都完成了新增的无玩家战局测试，精锐未改伤害、视野或免伤参数。该样本是单一地图和种子的短时对照，不能概括所有战局。

## 修复过程与限制

测试曾发现战斗载具在步兵仍在基地时选择后方掩护位，导致无玩家短局缺少交战；已增加步兵实际推进与车辆相对前后位置判断。听觉误差坐标可能落在建筑或地图外，触发虚假的导航失败；已在搜索前校正到可通行位置。失去路线后的等待与真实不可达分别记录，避免把正常到点误报为卡住。

外观已加入较合理的头身比例、头盔、护目镜、背心、护膝和手套，但仍是低面数原创模型，未达到照片级写实。多地图、新模式、新运输载具和完整道路驾驶不属于本轮成果。

---

# 0.16.0 QA

Strict TypeScript、106项单元测试、38项Chrome浏览器测试、8/16/32/64自动战局、默认32v32/500票、LAN与生产构建全部通过。新增驾驶舱测试逐一登上八种车辆、核对实际可见模型、检查摩托车加速与把手转向、切换V和F1/F2/F3座位、下车清除；警报测试验证字号至少32px、真正的60点装甲损伤、衰减及干扰清除。

本轮是原创低多边形座舱与骑乘表现，不等同于完整真实车辆内饰或商业游戏材质质量。镜头旁的座舱模型不是碰撞物理结构；右键继续使用已有缩放瞄具。未加入镜面实时反射、全身驾驶IK、可点击舱内按钮或完整自由头部观察。两浏览器LAN验证不等于两台用户电脑已实测；主观音量与驾驶手感仍需要实际试玩。未重跑0.15.0的20分钟AI长测，历史结果保留。Three.js大于500kB的分块警告保留。

第一轮新增驾驶舱检查的换座断言读取早于渲染帧，改为等待可见模型更新。第一次38项完整检查有37通过1失败，警报每帧重建strong节点导致读取已脱离DOM的字号为NaN；改为仅状态变化时更新DOM，随后旧机枪位测试也暴露读取渲染帧过早的断言，改用等待可见状态后再次完整重跑38项通过。原始日志与最终日志均保留。

## 历史0.15.1与0.15.0验证

# 0.15.1 QA

开始前fetch后，本机main与origin/main均为11f20f1766441e8a7adfbd040fcaffe39bad27ec，工作区干净。六个本机历史目录的源码、server、scripts、tests、docs和dist与仓库0.15.0逐字节一致。结束将直接提交main并同步本机目录。

Strict TypeScript、106项单元测试、37项Chrome浏览器测试、8/16/32/64自动战局、默认32v32/500票、LAN和生产构建全部通过。新增浏览器用实际damageVehicle事件验证：大字警报至少32px；其他车辆受击不闪屏；自己装甲受击显示−60 ARMOR、方向、闪光及震动；1.4秒后反馈消退；X干扰隐藏威胁警报。

音效路由和解码由自动检查覆盖，主观响度与手感仍需要用户实际试听。短时浏览器性能采样不是长期硬件基准。本轮没有重跑上一轮20分钟AI长测，其结果保留为0.15.0历史证据。大于500kB的Three.js分块警告保留。

## 0.15.0 历史验证

# 0.15.0 QA report

## Baseline

Main/origin/main 6f2c23d41f07ae4c07c4340e758c22cd12c3f61a; five local directories byte-identical to 0.14.0 src/server/scripts/tests/docs/dist before work. Direct main upgrade to 0.15.0 under the requested feature version rule.

## Actual checks

- Strict TypeScript, 106/106 unit tests, 36/36 native Chrome browser tests, Vite production build PASS.
- 8/16/32/64 autonomous matches PASS; small complete-match scenario uses60 tickets instead of36 to observe combat and final deaths under new scoring. No automatic-win or injected kill workaround.
- Default32v32/500: 305 simulated seconds, red winner, 14 captures, 1444 infantry shots, 26 deaths, 1068 vehicle shots; navigation failures 1; all three major events, counterattack distribution and live occupied armor/infantry cooperation PASS. Armor regression now requires actual alive driven tank/IFV, rather than checking the first dead IFV's stale goal.
- 1200-second soak: 23 captures, 10960 shots, 46 deaths, 0 navigation failures, report peak 63, projectile peak 8, duplicate crew0. Runtime 9696ms; heap delta before GC is observational, not a memory leak proof. Retained-Battle forced-GC sample: 4.53MiB at start; 6.96/6.96/6.86/6.91MiB at300/600/900/1200seconds, stable after warm-up.
- LAN protocol at 10.100.113.195, max8 humans, host team permissions, ready gate, shared authority and browser multiplayer PASS. New flight inputs and recommendations carried by authority server.
- Headless32v32 short sample 59.98–60.01FPS, 782draw calls. Not a sustained hardware benchmark.

## Failures caught and corrected

Initial commander defense preference suppressed small-match contacts; balanced attack/uncertainty utility then tested actual outcomes. Hull navigation initially joined nodes across walls, causing repeated failures; visible clearance-valid endpoints removed that error. First browser pass31/36: two obsolete count assertions, one invalid high-altitude boarding setup, and two closed browser contexts; corrected setups and a frozen-source full rerun passed36/36. Legacy vehicle/support-count expectations updated for two real new motorcycles and one support choice. Tests explicitly prove hidden obstacles create no visual report, lost contacts freeze, recommendations do not move humans, missile turn bounds, flare stages, physical jet evasion without flares, transport disembark and blocked navigation report without teleporting.

- 情报限制：Intelligence 仅接收复制后的目击/听觉/据点/失败报告，视觉24秒、声音8秒、据点60秒过期，置信度随时间下降。Commander 不接收 Battle 或敌方实体，也不能查询其坐标。
- 双方独立指挥官：按未知区域、已报告据点、敌方活动估计、小队健康、路线失败、路程、票数和任务拥挤评分。任务至少保留12秒，紧急受压或路线失败允许提前调整；Easy/Normal/Hard战略间隔6/4/3秒。
- 小队领队：每秒检查集结、移动、交战、驻守、占领和退却状态；等待掉队队员最多10秒，避免永久等候。掩体位置保留5秒，过期或阵亡释放；分批推进与停止掩护使用小队周期，狙击、医疗救援、维修和补给延续既有真实行为。
- 玩家建议：HUD给出目标与理由，J接受、K忽略；接受由队友执行，玩家移动和武器不会被接管。建议45秒内不会重复弹出。LAN由房主确认操作，只发送本玩家阵营/小队的建议。
- 车辆导航：按车型实际半径创建独立通行图，检查车体扫掠范围和节点连线，避免隔墙连接或无法到达却直接冲目标。路径失败报告进入小队/指挥反馈，重寻路有冷却；加减速、转弯减速及动态车辆避让仍连续逐帧执行。
- 运输协作：运输车优先让驾驶员同小队成员使用空座，抵达目标附近后AI乘员实际下车继续任务。保留真人接管、专属空投和死亡规则，不能靠凭空设置driver让无人车开动。
- 飞行控制拆分：喷气机持续前进，W/S推力、A/D倾斜转弯、鼠标俯仰/航向、Shift加力、Ctrl空气刹车；直升机W/S前后、A/D侧移、鼠标航向、空格上升/Ctrl下降、Shift加速，能悬停和倒飞。机体姿态、HUD数据和操作提示随车型变化。
- 导弹两阶段提示：正在被锁定为慢警报，导弹来袭为快警报及闪烁；载具导弹和步兵防空都生效。X热诱弹在锁定时清除捕获，在来袭时移除弹体引导，5秒保护、24秒冷却；弹体继续实际飞行。
- 机动规避：导弹寻的锥约77度、最大转向1.25rad/s；高速横向机动可能让寻的器丢失目标，不采用随机免伤或传送。AI飞机受威胁也会使用干扰和规避输入。
- 新增M2 COURIER双座无武装摩托车：85HP、最高33m/s、小半径与较快加速，有独立双轮模型和图标。双方基地各一辆，Q菜单70RP、25秒冷却可召唤专属8秒空投。双方现各8种车型。


## Limits

仍使用原创规则驱动AI和轻量运动模型，未达到商业游戏NPC/刚体飞行模拟的复杂度。完整RESUPPLY、ESCORT、RESERVE等战略任务、自动召集远处运输乘员、全道路车道和转弯半径规划、可点击个人任务覆盖与“换一个建议”按钮尚未完成；医疗/维修/补给是实际个体行为，不能视作这些战略模块已经实现。指挥官未知信息会导致不理想分配，属于可见的限制。浏览器FPS仅短时headless采样，不代表用户硬件长期性能；本环境两个浏览器/LAN IP验证不等于用户两台电脑已实测。大于500kB的Three.js分块警告保留。

## Deliverables

Detailed DOCX, source/private fullZIP, CRC/SHA256, updated main and local copies. User MP3s remain local/private fullZIP only. Friend needs host HTTP link/IP:7878 and room code; restart old server before npm run lan. Public code/version rejects outdated servers.
