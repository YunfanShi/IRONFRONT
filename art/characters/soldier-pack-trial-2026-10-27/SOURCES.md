# Human Soldier Animations FREE — 0.66.5 候选来源

用户选择使用免费士兵动作包试做 IF-27 换弹和 P8 持枪，并于 2026-10-27 提供官方下载的 `Human Soldier Animations FREE.unitypackage`。

## 原件与可追溯记录

- 本地原件：`/Users/liuwenjun/Downloads/Human Soldier Animations FREE.unitypackage`
- 体积：17,916,443 字节；SHA256：`5a9f20fb075363c8b28a97c3360c3c93d9a6004af6e5caf7bcb1e852f6783b40`
- 本任务原包备份、独立解包文件、作者 Blender 源：`source/`（Git 忽略）。171 个文件资产路径见 `package-manifest.json`。
- 随包说明：`Human Soldier Animations 2.0 FREE.pdf`；原包及随附说明原样保留。
- 读取文件中的骨架和动画数据，不执行随包脚本。

## 官方来源

- [作者 itch.io](https://kevdev.itch.io/human-soldier-animations-free)
- [Unity Asset Store](https://assetstore.unity.com/packages/3d/animations/human-soldier-animations-free-340690)
- [作者官网](https://keviniglesias.com/)
- [作者免费动作清单](https://www.keviniglesias.com/documentation/Human%20Soldier%20Animations%20FREE.pdf)

此前核对的作者许可允许用于商业游戏，不允许独立转售或再分发原始资产。作者直接渠道与 Unity 商店对应的许可渠道不同；本项目不重新发布素材包。原件留在本地；游戏只内嵌本次使用的上肢动作采样作为游戏表现资源。

## 实际使用

- `HumanM@AssaultRifle_Reload01`：IF-27 第一人称换弹；Blender 1–52 帧，原长 1.7 秒，映射至现有游戏 1.9 秒。
- `HumanM@AssaultRifle_Aim01`：IF-27 持枪；Blender 1–71 帧。
- `HumanM@Gun_Aim02`：P8 双手持枪；Blender 1–71 帧。实查 `Gun_Aim01` 为单手姿势，因此未选用。

动作区间由随包 FBX `.meta` 的导入区间核对；不是 action 中全部含预备负帧的 keyframe range。脚本 `sample_soldier_pack.py` 在作者 Blender 源骨架上计算约束后以 60 Hz 采样，保留独立手指、手腕、手肘与枪身的实际变化。采样清单见 `sampling-report.json`，骨架清单见 `source-rig.json`。

本版仍使用现有角色和候选手套网格，另做坐标系、指骨轴向和枪械接触修正。P8 换弹沿用 0.66.4，只在起止处混合新的持枪姿态；没有声称它也使用此包的换弹片段。

复查动作末段：`AssaultRifle_Reload01` 在插匣后张手回到前护木，不包含拉机柄。候选按这一实际内容映射，取消旧程序动作的拉柄段；完整空仓上膛不在本片段中。
