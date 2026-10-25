# IRONFRONT Unity 寻路与战斗分配优化（0.54.2）

2026-10-23，基于工作区已有 0.54.1。用户要求使用新获得的 Unity 技能优化现有游戏，并暂时不更换建模。

## 范围与环境

使用 `scenario-unity-expert` 的环境检查与测量闭环、`scenario-unity-performance` 的开发版对照、`scenario-unity-gameplay` 的对象复用原则、`scenario-unity-architecture` 的状态所有权与会话清理，以及官方 `unity-cli` 的版本检查与构建流程。

实际项目为 Unity **6000.5.10f1 / URP 17.5.0**，不是技能示例的 6000.3；保持引擎、渲染管线、PC 质量档、Linear 色彩空间及原有输入配置。Enter Play Mode 使用完整域与场景重载。默认目标为桌面 60 fps（16.67 ms），测试设备为 Apple M5 / Mac17,4，原生 macOS 开发版，1280×720。尚未在最低配置设备、Windows 或长期满规模对局上测量。

官方 CLI 从 beta.5 更新至检测到的 beta.14；其首次构建调用停留在启动 Editor 之前，停止该自有进程后，使用已安装 Editor 的明确路径完成构建。开始前确认没有编辑器占用工程，授权有效。没有为此安装 Pipeline、NavMesh 或更换现有导航体系。

## 修改内容

### 1. 步兵和车辆寻路

`PrototypePathWorkspace` 为每个导航网格持有 cost/prior/closed 数组、原始路线列表和稳定最小堆。重复查询清空并复用容量；开放节点由线性扫描改为堆，分数相等时仍选最早插入的节点。原有 16 米网格、45×45 单元、八方向邻接、不可穿角规则、2200 次扫描上限和路线平滑规则保持一致。

寻找可见网格节点时，只遍历原 80 米半径的外接格子范围；保留原有逐行顺序、精确距离与遮挡检查。并没有扩大搜索距离或放宽通路。

`PrototypeNavigation.Find` 和 `PrototypeVehicleRoute.Find` 保留原有返回独立列表的 API，新增填写调用方列表的重载。步兵与组合载具使用各自持有的路线缓冲，避免反复创建结果列表。临时工作区不暴露给调用方，不会覆盖其他士兵正在使用的路线。这些对象用于当前同步主线程查询，不是线程安全共享查询器。

车辆网格缓存增加 `SubsystemRegistration` 清理，防止关闭域重载后跨 Play 会话残留。地图仍为既有静态布局，未增加动态障碍更新机制。

### 2. 高频地图查询

`PrototypeLayout.Collides` 与 `LineBlocked` 从对 `IReadOnlyList<Block>` 的 foreach 改为索引遍历，消除这些热点函数反复构造枚举器的分配。原有矩形、圆形采样、地形和视线几何数学保持一致。

### 3. 曳光弹复用

`PrototypeTracerPool` 属于当前 `PrototypeRuntime`，启动预热 64 个 LineRenderer，场景关闭时随所属对象一起销毁。每次射击重新填写原有双方材质和世界坐标端点，保持 0.035/0.012 宽度、2 个帽顶点及 **0.09 秒游戏时间**寿命；暂停时与原延时销毁一样冻结。

到期返回空闲池，最多保留 256 个空闲对象。突发射击可以扩展活动对象，不会因缓存上限丢掉视觉射击；回收超出的对象，避免峰值容量长期保留。加入 `IRONFRONT.Tracer.Spawn`、`IRONFRONT.Navigation.Infantry`、`IRONFRONT.Navigation.Vehicle` ProfilerMarker，便于继续定位。

没有改造真实子弹、火箭、烟雾、爆炸伤害、AI 决策频率、武器数值、角色动画或操控参数。

## 开发版测量

完整证据目录：`UnityProject/Builds/Verification0542`。优化前开发版保留于 `baseline/IRONFRONT.app`，优化后对照版为 `optimized/IRONFRONT.app`。

基准使用固定随机种子 542、8v8（15 个 AI）、固定相机。寻路先预热缓存，再执行 120 对坐标 × 步兵/车辆 × 3 次，共 720 次查询；车辆半径 3.15 米，包含成功、失败和绕障路径。画面基准固定模拟步长 1/60，预热 300 帧，采集 600 帧，每帧附加 4 条短时曳光弹；关闭 VSync 和帧率上限只作用于显式启动的基准进程。正常游戏与发布构建的设置不改变。

| 数据 | 优化前 | 优化后 | 结论 |
| --- | --- | --- | --- |
| 720 次查询耗时，第一次有效运行 | 6045.93 ms | 4212.70 ms | 查询成本下降 |
| 720 次查询耗时，第二次有效运行 | 6149.35 ms | 4162.16 ms | 方向一致 |
| 两次查询平均耗时 | 6097.64 ms | 4187.43 ms | **下降 31.3%** |
| 可达结果数量 | 636 / 720 | 636 / 720 | 完全一致 |
| 全部有序路点校验值 | 6173766864656313763 | 6173766864656313763 | 完全一致 |
| 战斗采样平均 GC Allocated In Frame | 30,743 B/帧 | 27,430 B/帧 | **下降 10.8%** |
| 平均帧耗时，两次有效运行 | 1.511 / 1.516 ms | 1.626 / 1.475 ms | 未确认稳定提升 |
| p95 帧耗时，两次有效运行 | 2.192 / 2.100 ms | 2.300 / 2.006 ms | 均低于本次 16.67 ms 目标 |

`before-2` 与一次 Editor 编译重叠，时间结果作废；保留原始记录并在 `summary.json` 标注，使用 `before-1`、`before-3`、`after-1`、`after-2` 比较。帧统计排除首个记录器初始化帧（其设置本身会分配约 1 MB）。

该设备的 `GC.GetAllocatedBytesForCurrentThread()` 不提供有效计数，旧基准 JSON 的 `allocatedBytes: 0` **不代表零分配**；汇总将它改记为 null，最终基准代码加入正对照校准，不可用时输出 -1。逐帧分配来自 Unity `GC Allocated In Frame`，专项零分配由下面的 `GC.Alloc` 检查证明。

此环境未提供 `Draw Calls Count` / `Render Thread` 记录器；保留 -1 表示缺失。实际图形提交通过约 126 次 SetPass、约 43 万三角形以及真实屏幕截图核验，未做 GPU 耗时归因，不能据此宣称 GPU 或整体 FPS 提升。原有大部分模型和渲染开销仍然存在。

## 实际 Unity Play 验收

`PrototypeBatchVerification.Run` 为每项检查建立全新 Play 会话，完整 Runtime / Editor 编译通过，8 组检查全部 PASS，进程退出 0，结果位于 `batch-results.txt`，日志 `checks.log`。

1. `Verify Navigation Optimization`：720 条结果与旧版固定校验值一致；调用方路线所有权、后续查询不覆盖、失败清除；1.85/2.1/3.15/3.5 米四种车体半径的真实路径逐段通过圆形碰撞检查。预热后连续 24 次使用调用方缓冲的查询，`GC.Alloc` 记录为 0，且显式分配正对照能够被同一记录器检测。
2. `Verify Tracer Pool`：原材质、端点、宽度、世界空间；0.09 秒到期回收；复用时零托管分配且无新增线对象；暂停冻结、恢复到期；300 发突发全部保留；峰值结束后回到 256 个空闲对象；清理测试场景。
3. `Verify Infantry Vision`：正面、边缘视野、背后盲区及转身可见。
4. `Verify Bot Anti-Vehicle`：实际装备/发射/弹道与装甲伤害、弹药/冷却、步兵回退、掩体/烟雾/友军过滤、近车后撤、暂停与会话边界。
5. `Verify All Vehicle Seats`：9 类载具、28 次席位转换、占座互斥、控制和相机、退出与毁车释放。
6. `Verify Ballistics`：飞行时间、下坠、速度与扫掠命中。
7. `Verify Equipment and Effects`：7 种枪械、4 种兵种装备、2 种投掷物、火箭与爆炸边界、坦克声音和特效。
8. `Verify Tank and Respawn`：部署、救援/复活、驾驶、墙角通行、主炮冷却与弹出。

性能记录器按 Unity 官方 [GC.Alloc 当前线程采样方法](https://docs.unity.com/en-us/engine/6000.5/script-reference/unity/profiling/profilerrecorderoptions/collectonlyoncurrentthread) 使用；验证的是预热后的相应热点，不是宣称整个游戏零分配。

## 画面与资产

实际开发版 `before-1/battlefield.png` 与 `after-1/battlefield.png` 已逐张复看。Scenario 技能 `ut_review.image_checks` 未发现空白、全黑或粉色材质；`ut_review.compare` 得到 **逐像素相同**，均为 1280×720。原图直接来自运行游戏的 ScreenCapture，包含真实 HUD；没有重绘或合成。

任务开始保存并最终核对 `Assets/Art` 下 96 个现有文件的 SHA-256，全部一致。源代码修改也未触及 SoldierVisual、VehicleVisual、WorldVisual、模型导入和材质定义。工程仅新增 Unity 内置 `com.unity.modules.screencapture` 模块，供显式基准采样保存游戏画面。

## 使用与复现

正常游戏无需启用测试功能。版本为 **0.54.2**，正式可执行包目标路径：`UnityProject/Builds/macOS-0.54.2/IRONFRONT.app`。

该正式 macOS 构建已实际成功、进程退出 0，BuildReport 为 170,266,994 bytes，日志 `release-build.log`。Info.plist 的 CFBundleShortVersionString 核对为 0.54.2；独立应用启动 15 秒保持运行，Metal 初始化成功，未发现所检查的启动异常，仅关闭本次测试进程。结果为 `startup-validation.json`，日志 `release-startup.log`。这是发布包启动检查，不等同于人工完整对局。

编辑器运行 SampleScene 后，可用菜单 `IRONFRONT/Verify Navigation Optimization`、`IRONFRONT/Verify Tracer Pool` 分别复核专项行为。完整列表位于 `Builds/Verification0542/checks.txt`，由现有 `PrototypeBatchVerification.Run` 配合 `-ironfrontChecks <绝对路径>` 执行并退出。

`PrototypeOptimizationBuild.Build` 创建独立 Development Build，接受 `-buildOutput <绝对 .app 路径>`；启动该应用的 `Contents/MacOS/IRONFRONT Unity Prototype`，传入 `-screen-fullscreen 0 -screen-width 1280 -screen-height 720 -ironfrontBenchmark <绝对输出目录>`。只有显式传入此参数的开发版才会建立基准对象、固定相机并自动退出。普通发布包编译时排除基准代码。

`summary.json`、各轮 `navigation.json` / `frames.csv` / `device.txt` / `battlefield.png`、`image-checks.json` 为本轮证据。新增脚本保留对应 `.meta`。

最终核验 119 个 C# 源文件均有 `.meta`，本次新增 5 个脚本包含对应元数据；本次修改的源代码与版本/说明文件通过定向 `git diff --check`。全仓库仍有任务开始前既存 Unity 序列化配置的行尾空格，不在本次性能修改中清理。

## 源码状态与边界

开始时工作区已包含 0.46.0 至 0.54.1 的大量未提交功能与资产。远端检查显示当前 HEAD 为 `c166d59`，与远端 `codex/unity-port` 的尖端一致；远端 main 为 `1afb5fa`，两者有分歧。更新了远端引用，没有重置、变基或覆盖原工作区。本轮变更保留在当前本地工作区，未把这些既有未提交内容一并发布或合并 main。

此次证明了固定场景的寻路成本和分配改善，没有证明完整长期对局、更多人数或最低配置机器的性能。现有视觉和战斗行为继续保留，后续可根据真实大规模对局的 Profiler 捕获选择下一热点。
