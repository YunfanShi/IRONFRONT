# Unity 0.66.0 — 现成滑铲动作接入

2026-10-26。用户确认使用已找到的现成滑铲动画，沿用左 Alt 触发方案；Mac 对应左 Option。以已有 0.65.0 工作目录为基础，保留此前六款装备、第二批枪械、人物、载具和菜单成果。

## 交付与操作

- 新应用：`UnityProject/Builds/Slide-0.66.0/IRONFRONT.app`，独立目录，旧版保留。
- 实机视频：`art/characters/slide-2026-10-26/slide-0.66.0.mp4`，960×640、30 fps、5.7 秒，依次展示侧面收回蹲姿、正面恢复跑步和第一人称；按正常采样时间播放。
- 前进冲刺达到实际速度至少 5.2 m/s、接地时，按左 Alt 进入滑铲。原地、空中、蹲趴、换弹时不触发。
- C 始终为蹲下/起身；Ctrl 保留趴下。删除旧版 Shift+C 隐式触发滑铲。
- 自然动作长 1.08 秒，初速取实际冲刺速度（最多 6.6 m/s），衰减至 2 m/s；平地默认约 4.6 米，恢复后冷却 0.55 秒。长按 Alt 不连续触发。
- 滑行方向与鼠标视角分离，A/D 仅允许小幅修正方向（25°/秒）。第一人称平滑压低至约 0.88 米，轻微倾斜，滑行期间关闭跑步起伏。
- 开镜、按 C/Ctrl 或离地触发约 0.3 秒收腿恢复；开镜恢复后留在蹲姿。腰射沿用现有开火逻辑。
- 默认结束后蹲下；继续按住前进和 Shift、没有开火/开镜且头顶空间足够时接回站立跑步。撞墙立即停止前移并收回；低顶下保留蹲姿。

## 来源与骨骼适配

使用 Quaternius Universal Animation Library 2 免费 Standard 包中的 `Slide_Start`、`Slide_Loop`、`Slide_Exit`，CC0 1.0。来源、原始许可、压缩包校验和与复现说明见 [SOURCES](../art/characters/slide-2026-10-26/SOURCES.md)。

采用现有 Generic 士兵骨骼的语义重定向，不重建 Humanoid Avatar，不替换模型或蒙皮。Blender 从官方非 Root Motion GLB 以 60 Hz 采样，生成资源 `Assets/Resources/Animation/InfantrySlide.json`。原骨段长度保留，腰胸和下肢取源动作，双手继续约束到实际武器握点。最终动作混合后解算脚踝接触，修复第一轮检查发现的靴子穿地。

## 实现范围

- `PrototypeSlideState`：本地/远端共用状态、计时、速度、冷却、提前恢复与快照字段。
- `PrototypePlayerSlide`：Alt 按下沿锁存、接地与实际速度检查、姿态退出、控制器碰撞。
- `PrototypeSoldierSlidePose`：三段动作采样、原骨架重定向、靴底接触修正与武器姿势。
- `PrototypePlayer`、`PrototypeSoldierVisual`：接入运动、胶囊高度、第一/第三人称镜头及实际人物 LateUpdate。
- `PrototypeRemotePlayer`、`PrototypeLanClient`、`PrototypeRuntime`、`server/unity-room.cjs`：传递滑铲/开镜/蹲趴意图，主机校验入铲，快照同步动作阶段和方向。远端正常步速/冲刺速度对齐本地 4.2/6.6 m/s。
- 20 Hz 网络发送前锁存 Alt 短按，避免短按落在两包之间丢失。房间服务器仅接受严格布尔输入，动作和位置由主机决定。
- 旧检查中对 `slideEndsAt` 的反射改为新 Slide 状态。新增开发版专项探针不在正常启动时运行。

## 实际验证

证据根目录：`art/characters/slide-2026-10-26/`。

1. Unity 6000.5.10f1 StandaloneOSX 开发版构建 PASS，`build-03.log`：808,051,246 字节。保留原有武器检查脚本废弃 API 警告，无新增编译错误。
2. `probe-03/results.txt` PASS，RuntimeErrors=0。实际 CharacterController 验证原地/空中拒绝、C 单独蹲姿、长按不连发、开镜打断、墙碰撞、头顶障碍（同时保持 Shift）、所有现有枪械握点与远端权威状态。
3. 30/60/120 Hz 默认滑行距离分别为 4.607361/4.611664/4.631104 米。最大手部握点偏差 0.00004333 米；最大腿骨段长度误差 0.00002763 米；最小脚踝高度 0.1399976 米。脚踝高度不等于鞋底高度，探针结合脚部包络做接地检查。
4. `sprint/results.txt` PASS，RuntimeErrors=0：原跑步、开火/开镜中断、Shift 松开重按、侧移/后退、蹲趴、撞墙镜头、轻微上身运动及枪托保留。
5. `ui/results.txt` PASS，RuntimeErrors=0：五种窗口、四兵种、配装选择/保存、设置、联机入口和真实单人开局。
6. `scripts/test-unity-room.cjs` PASS：原协议回归及新增滑铲/姿态严格布尔输入、主机滑铲快照往返。Unity 探针另验证主机拒绝静止入铲、接受冲刺入铲、开镜恢复及断线清理。
7. 检查实机侧面、正面和第一人称帧，并在 QuickTime 正常速度播放 MP4。正常启动的新应用已成功进入对局，C 实际按键仍进入 CROUCHED。自动按键工具无法跨两次调用持续保持 Shift+W，故不将该尝试记为人工 Alt 入铲通过；Alt 的入铲/锁存由上述实际运行探针验证。
8. 独立 Blender 5.2.2 LTS 再次从原 GLB 采样，生成 JSON 与构建前逐字节一致（SHA256 `f6ed40a922619e13f6bfa55a4e798ff4931368720073f21c240333019ad76b71`），见 `reproduce.log`、`delivery-manifest.json`。
9. `preservation-result.json`：653 个既有 Art、菜单、装备和枪械资源逐文件 SHA256 未变。任务前修改文件备份在 `baseline/`。没有覆盖原工作原件或旧应用。

## 复测入口与边界

新应用可加 `-ironfrontSlideProbe <输出目录>` 启动专项探针。原跑步、菜单探针入口保留。正常游戏启动不执行探针。

联机已验证协议和单机主机模拟/快照阶段，尚未进行两台电脑实际联网和高延迟视觉验收。接地专项基于平地、墙和低顶夹具，尚未完成复杂斜坡/台阶全图验收。此版未增加独立滑铲音效或尘土粒子；既有模型、菜单风格和其他移动循环保留。

## 版本控制边界

任务开始已 fetch：HEAD `9826942` 与 `origin/codex/unity-port` 一致（0/0）；与 `origin/main` 相差 37/5。Unity 与网页分支分工沿用 [同步记录](GITHUB_SYNC_0631.md)。本地任务前已有 0.64/0.65 的装备/枪械修改、未跟踪资源和参考文档删除，均保留。本次交付为本机应用与源代码，未将这些历史混合修改一起提交或推送，也没有合并到网页 main。
