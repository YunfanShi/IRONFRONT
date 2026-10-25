# Unity 0.59.2 — 视频参考冲刺持枪与战斗中断

2026-10-24。根据用户提供的 `normal_video.mp4` 修改冲刺持枪和输入优先级。原有模型、枪托、换弹、角色腿部动作与旧程序保留。

## 规则

- 向前或斜前移动并按住 Shift：按原有 6.6m/s 冲刺。
- 左键或右键按下的同一帧，在计算移动速度之前中断冲刺。左键继续执行原有开火逻辑，腰射移动为 4.2m/s；右键进入瞄准，沿用 2.73m/s 瞄准步速。
- 松开鼠标但继续按 Shift 时保持普通移动。松开并重新按 Shift 才能再次冲刺，避免持续射击或快速点射时反复切换状态。
- 即使枪为空、射速冷却未结束、使用半自动手枪，中断规则仍生效；不依赖这一帧是否实际发射子弹。
- 蹲伏、趴伏、侧移、后退、滑铲及装填限制保留。没有新增开火等待时间。

## 视频与姿态

参考视频长 10.242 秒，544×960、约29fps，拍摄方向横置；分析时将提取帧逆时针转90度。3.0秒为通常持枪，3.25至4.5秒枪身斜收到左下侧，约5.0秒回到通常持枪。视频被真实手指遮挡的区域不作为骨骼细节依据。

第一人称冲刺枪械向左收并侧转，随现有步频小幅摆动，让出画面中央；退出冲刺后通过原有平滑插值返回腰射或瞄准位置。完整枪托仍显示。本次没有重做下半身跑步循环，没有改士兵骨架和模型。

## 实现

`PrototypePlayer.ResolveSprintInput` 在移动之前统一解析冲刺和战斗意图；`sprintInterrupted` 锁存此次 Shift 持续按压期间的中断，释放 Shift 后解除。`ResolveMovementSpeed` 统一设置瞄准、冲刺状态及对应速度。`Fire` 也清除冲刺意图，保护其他调用入口。复活清空中断状态。

`SprintRequested` 表示通过姿势/战斗限制后的输入意图，与本地碰撞器的接地结果分离，避免联网快照暂时重置碰撞器时丢失持续冲刺请求。联网发送仍检查实际 Shift 和鼠标输入；主机的远端移动入口也禁止同时 fire/sprint 使用冲刺速度。没有新增协议字段，也没有修改既有远端速度常量。

## 验证和范围

- 最终独立程序：`art/characters/sprint-interruption-2026-10-24/player/results.txt`。通过实际生产代码的输入解析方法、CharacterController 位移及真实开火方法验证，非物理键盘注入。
- 第一/第三人称各采集“冲刺→开火→继续走路→重按Shift”和“冲刺→开镜→继续走路→重按Shift”，共504帧、30fps。每一帧记录输入、冲刺状态、期望和实际速度、弹量；证据为 `samples.csv` 和 `movement-summary.json`。
- 验证空枪自动装填、冷却期点击、手枪、同时开镜开火、前斜向/侧移/后退、蹲趴滑铲，以及主机远端输入同时开火和冲刺的速度保护。
- `batch-results.txt`：原五组回归通过，含视角、持有装备、9类载具28次座位切换、大厅、864个蒙皮姿态和2304个方向采样。最后分离联网冲刺意图的改动由最终独立程序复验。
- 联网覆盖输入意图和主机入口检查；本次没有实际启动两台客户端进行整局联机验收。
- `preservation.json`：确认0.59.1应用的全部276个文件、枪械源模型与贴图、整个 `PrototypeSoldierVisual.cs` 未改动。

## 交付和复现

- 新程序：`UnityProject/Builds/Sprint-0.59.2/IRONFRONT.app`，需要打开此版本才能体验新规则。
- 视频：`art/characters/sprint-interruption-2026-10-24/sprint-interruption-0.59.2.mp4`，标记输入阶段、实测速度和弹量。
- 关键帧：同目录 `sprint-review.png`；原视频提取帧在 `reference-initial`、`reference-detail`，分析图在 `reference-transition.png`。
- 开发版验证参数：`-ironfrontSprintProbe <输出目录>`。正常启动不会创建验证场地或自动退出。
- 构建入口：`Ironfront.UnityPrototype.Editor.PrototypeOptimizationBuild.Build`，传入 `-buildOutput`；序列整理脚本为本目录 `review.py`。

现有未提交工作和 `codex/unity-port` 分支保留。当前 HEAD 与本地 origin/main 引用相差36/5；没有自动合并、提交或推送历史工作。
