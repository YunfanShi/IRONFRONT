# 0.66.9 — P8 使用 Gun_Reload02

## 请求与结果

用户在原包两个换弹动作的对比检查后选择「那个 2」。本版以 `cf69ff5` / 0.66.8 为基线，把 P8 第一人称换弹全段接到 `HumanM@Gun_Reload02`。双队均生效，持枪继续使用 `Gun_Aim02`。

左手按源动作离开握把、移到腰侧取匣、抬到握把底部托送弹匣、恢复双手持枪。旧程序化的枪顶拉套筒动作已停用，避免再次出现此前截图中的跨枪顶垂指姿势。

## 源动作与适配

来源是用户提供的 Kevin Iglesias 免费士兵动作包，原件与许可记录见 [本轮来源](../art/characters/p8-reload02-2026-10-27/SOURCES.md)。选型的两个原动作检查见 [原包审查](P8_SOURCE_RELOAD_REVIEW_2026-10-27.md)。

- 随包 FBX 导入区间 0–51 对应 Blender 1–52，源动作 1.7 秒；60 Hz 采样 103 个完整姿势，包含 30 个独立指节。
- 原有三条动作数据逐项保持一致。P8 新增 `pistolReload`，运行时实际来源由探针验证。
- 现有玩法换弹时长仍为 1.3 秒，因此整段源动作播放速度约为 1.31 倍；没有更改弹药、输入或兵种数据。
- 枪身旋转使用源动作，位移按第一人称镜头空间缩放和偏移；这并非直接复制全身相机位置。
- 原检查模型没有独立弹匣。本项目为 P8 添加旧弹匣落下、新弹匣跟手、插匣与显隐时序。最后一小段同时修正手腕和新弹匣的位置、方向，使其沿当前 P8 的弹匣槽进入。
- 完成插入的相位为 `38/51`。交接前新弹匣的世界位置、旋转与已就位弹匣一致，交接后只显示一个就位弹匣。
- 源片段没有拉套筒／空仓上膛段，本版没有增加该动作。

## 修改文件

- `PrototypeSoldierPackMotion.cs`：按手枪／步枪选择各自换弹片段。
- `PrototypeWeaponArtRig.cs`：接入 P8 源动作、镜头空间取景、手掌接触、新旧弹匣和插入路径；禁止旧枪顶拉套筒段叠加。
- `SoldierPackTrial.json`：新增 `pistolReload` 的源采样。
- `PrototypeRigAuditProbe.cs`：增加来源、弹匣交接、旧动作停用检查和插入附近的观察帧，新增 `-pistolOnly`。
- `ProjectSettings.asset`：版本改为 0.66.9。

既有 v08 手指厚度、袖管、骨架、材质、武器比例和第三人称没有改动。IF-27 保持 `AssaultRifle_Reload01`。此次全枪械回归覆盖共用驱动，其他枪没有增加新片段。

## 校准与验证

最终构建为第三轮 `build03.log`，Unity 6000.5.10f1 macOS Player 构建 PASS。

1. 第一轮实机图暴露源全身位移令枪偏出画面、旧腕点不适合托底插入的问题；调整取景与掌心接触。
2. 第二轮反向接触图暴露插入前的侧向偏移；将最后接近段校准到实际弹匣槽方向。
3. 最终构建重新运行 P8 全段、红蓝两队七枪 rig 检查：均 PASS，`Version=0.66.9`、`RuntimeErrors=0`。

具体检查包含：P8 运行时确实使用 Reload02、末段没有旧拉套筒位移或肩部混合、插匣交接前误差小于 1 mm／0.1°、交接后只显示一个弹匣、五个换弹中断时刻能复位。原有臂长与瞄准视线检查也通过。原始结果保存在本轮 `checks/`，文本副本见 `review/pistol-results.txt` 和 `review/rig-results.txt`。

实际打开检查了最终 14 个姿势、每 0.1 秒的连续动作图、66%／70%／74%／75%／79%／87% 的反向网格接触图。可见新弹匣从下方接近、托底插入和手掌离开；末段没有再跨到枪顶。数值探针不代表逐三角形无穿插证明，也不替代用户对观感的最终判断。

30 fps 视频来自最终 Player 捕获帧；含持枪、瞄准、完整 1.3 秒换弹和恢复，整段重复三次便于观察。编码和 AVFoundation 解码均通过：432 帧、14.4 秒、960×640。QuickTime 已打开并从 0 播放到 14.4 秒；工具的界面观察为离散帧，不据此声称已完成人类连续视觉验收。

## 交付与复现

- 应用：`UnityProject/Builds/PistolReload02-0.66.9/IRONFRONT.app`。
- 视频：`art/characters/p8-reload02-2026-10-27/p8-reload02-30fps.mp4`。
- 最终实机图：同目录 `review/P8-final-poses.jpg`、`P8-final-sequence.jpg`、`P8-final-contact.jpg`。
- 原有问题对照：`review/P8-reload87-before-after.jpg`，相同换弹相位／反向观察比较 0.66.8 与 0.66.9；两版相位对应不同动作内容。
- 采样：`sample_pistol_reload.py`，依赖本地保留的原包源与 `baseline/SoldierPackTrial.json`。
- 运行检查：`python3 art/characters/p8-reload02-2026-10-27/run_checks.py pistol rig`。
- 拼图与视频帧：`review_frames.py`；编码器源沿用 `art/characters/hand-reload-2026-10-27/encode_review.swift`。
- `checks01/02` 与 `review` 中 iteration01/02 是返工过程，不能替代最终证据。

应用、原件、逐帧图片与视频按仓库现有规则留在本地；代码、采样、文档和文本验证纳入本地提交。本轮不推送、不合并主分支；保留开始前已有的五项用户删除。
