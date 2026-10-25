# Unity 0.58.2 — 左右移动与斜向冲刺穿腿修复

2026-10-24。针对用户截图中A/D横移、W+A/W+D冲刺时双腿交叉、膝盖扭转的问题。原模型、骨架、权重、0.58.1构建、现有速度与趴伏保留。

## 原因与修正

旧代码只把足部位移乘上移动方向，髋部站位、膝盖弯曲方向和脚尖仍按正前方求解。右移时左右脚可以交换位置，膝盖向同一平面挤压；固定骨长检查无法发现两条腿彼此穿插。按实际玩家速度复现后，斜向冲刺腿部骨段最近距离仅0.00176m。

`PrototypeSoldierVisual`现在让下半身步态平面随移动方向旋转：

- 髋部、膝盖弯曲方向、双脚落点间距和脚尖使用一致的方向。
- 转向分摊到骨盆、腰椎和胸廓，上身继续朝向瞄准方向，原握持点与开火逻辑保留。
- 下半身朝向平滑变化，快速左右换向时限制脚踝留在各自腿侧，重新落脚时不会从另一条腿中间穿过。
- 斜后退增加少量屈膝，使支撑脚保持在真实腿长可达范围内，避免伸展上限引起滑脚。

未修改玩家输入、移动速度、碰撞体、载具或角色源模型。完整正向循环和匍匐循环保留；后退接触姿势因修复可达性而略有降低。

## 交付

- 新程序：`UnityProject/Builds/Motion-0.58.2/IRONFRONT.app`。旧程序仍可运行，必须打开新版才能看到修复。
- `art/characters/strafe-fix-2026-10-24/strafe-before-after.mp4`：18秒，左0.58.1、右0.58.2；左右横移、左右斜向冲刺、左右蹲行，同镜头、同速度、30fps。
- 同目录 `direction-changes.mp4`：9秒，快速左右换向及左右斜后退。
- `comparison-preview.png`：背面视角静态对照；`user-reference.png`保留用户截图。

## 验证

证据根目录：`art/characters/strafe-fix-2026-10-24/`。

- `before/`：旧动作实际玩家复现，明确使用`-ironfrontAllowCrossedLegs`收集坏样本；其PASS只代表探针采集完成，不能表示动作正确。
- `player/motion-results.txt`：最终0.58.2开发版，实际玩家对象，16组、1395连续帧，运行错误0。等价输入速度覆盖A/D 4.2m/s、W+A/W+D冲刺6.6m/s、蹲行2m/s、斜后退与快速换向。没有声称通过物理键盘注入测试。
- `leg-review.json`：四组大腿/小腿线段之间的独立最近距离；包含启动与换向所有帧，修复后最低0.23704m。稳定阶段脚、膝分别处于相互分开的移动轨道；瞬时反向时不对尚未转完的身体强制套用新方向左右符号，而持续检查骨段不相交。
- `batch-results.txt`：原五组回归通过，含视角、持有装备、9类载具28次座位切换、大厅、864步兵姿态；新增红蓝双方2304个方向动作采样，稳定后检查双脚和膝盖分离。
- `motion-preservation.json`：0.58.1与0.58.2正向跑/走/蹲行及四种趴伏模式，稳定阶段CSV差值均小于0.0001。排除前8帧是因为启动呼吸取实际墙钟时间，不是相同时间输入；两次采集的呼吸相位不同。运行检查仍覆盖启动全过程。
- `image-checks.json`：1442张实际程序截图无空白、黑帧或洋红错误。已查看左右横移、斜向、蹲行和换向连续帧。
- `preservation.json`：既有清单中的24个源模型/导出/骨架接入/玩家玩法文件哈希未变。
- `build.log`：Unity 6000.5.10f1，macOS开发版构建成功，在Apple M5上复验。

数值线段距离用于发现穿腿，不等同于逐三角形服装碰撞检测。视觉检查包括蒙皮序列和视频回读帧，未声称真人正常速度观看验收。沿用现有地面接触范围，没有新增任意台阶/障碍物逐脚IK。

## 复现

独立程序参数：`-ironfrontMotionProbe <输出目录> -ironfrontMotionPlayer -ironfrontMotionActions strafe-left,strafe-right,diagonal-left,diagonal-right,crouch-left,crouch-right,reverse-diagonal-left,reverse-diagonal-right,turn-left-right`。编辑器入口仍是`SoldierMotionPlaySmoke.Run`。

`PrototypeInfantryLocomotionSmoke`菜单回归现在包含方向动作测试。`review.py`生成同速对照，并独立检查启动/换向骨段距离及原动作连续数据。原分支与未提交工作保留，未自动合并或推送其他工作。
