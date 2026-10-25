# IRONFRONT Unity 载具护甲状态条（0.54.1）

2026-10-15，基于本地 0.54.0；保留原工作分支及此前未提交的功能。

## 显示规则

玩家上车后，在屏幕底部中央的生命条上方显示蓝色载具护甲条，宽度同为屏幕的 25%，两块面板间隔 8 个逻辑像素。标题显示现有护甲等级，右侧显示当前载具剩余耐久百分比；不高于 45% 变黄、不高于 25% 变红。

耐久比例来自当前载具的 `Health / IPrototypeArmoredVehicle.MaximumHealth`，不同车型使用各自真实上限；受损减少、工程维修恢复。驾驶、炮手和乘客席均读取同一辆车的状态，切车即时更新，下车、角色倒地、座位失效、载具销毁或停用时隐藏。

本轮没有新增独立护甲伤害池，也没有改动护甲穿透、普通枪零伤害、载具生命或维修规则。护甲等级仍是既有防护属性，条形表示载具剩余耐久；玩家生命条继续读取玩家自己的生命值。

## 布局与实现

`PrototypePlayerHealthHud.cs` 增加只读载具数据读取、百分比归一化及上方状态条。读取时验证存活、有效对象、启用状态、真实本机座位归属和数值有效性，不创建、缓存或修改载具战斗数据。车体生命沿用既有快照读取路径，无新增 LAN 字段。

`PrototypeTankSightHud.cs` 将方位/装填信息保持在护甲条上方；`PrototypeScorePopup.cs` 在乘车时把完整的积分数字、图标和明细组移到右侧，避开中央双血条。步兵积分提示保留原来的中央位置，积分结算不变。

分工：主智能体完成 HUD、布局、版本、文档与构建；载具子智能体独立检查九种载具的真实生命与座位生命周期，并制作真实 GameView 验证截图。

## 验收

完整 Runtime/Editor C# 编译通过。本轮实际执行 Unity 6000.5.10f1 原生 GUI Editor，以下三项运行检查与一项真实截图全部 PASS，协调进程退出 0：

- `Verify Vehicle Armor HUD`：九类真实车体、不同生命上限、实际登车/换席、受损/修理、失效座位/车体/数值及退车隐藏，五种屏幕尺寸下的布局。
- `Verify Center Health and Scoreboard Controls`：原生命条与积分榜输入、光标和世界时钟回归。
- `Verify Score Popup`：真实伤害/击倒补足积分、归属、合并、支援消费排除及过期回归。
- `Capture Vehicle Armor HUD`：真实受损坦克耐久 55%，独立玩家生命 74，原生 GameView/Runtime.OnGUI 截图为 1024×667。已复看两条比例、数字及坦克方位/装填信息，互不遮挡；未通过图像合成或缩放制作。

输出目录为 `UnityProject/Builds/Verification0541`，运行日志 `checks.log`、结果 `batch-results.txt`、界面图 `vehicle-armor-hud.png`。

macOS 0.54.1 实际构建成功、退出 0，BuildReport 大小为 127,726,035 bytes。可运行程序为 `UnityProject/Builds/macOS-0.54.1/IRONFRONT.app`；Info.plist 版本已核对为 0.54.1。独立可执行文件启动 15 秒后仍正常运行，Unity 引擎与 Metal/GfxDevice 初始化成功，未发现所检查的启动异常；结束时只关闭该次检查启动的进程。日志为 `build.log`、`player-startup.log`，结果为 `startup-validation.json`。该启动检查不等同于人工完整对局。

最终资源审计通过：203 个唯一 GUID，无遗漏 .meta。汇总在 `validation.txt`。本轮没有新增 Git 提交、推送或合并 main。
