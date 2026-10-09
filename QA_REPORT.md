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
