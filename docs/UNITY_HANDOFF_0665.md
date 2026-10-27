# 0.66.5 — 免费士兵动作包试版

## 请求与交付状态

用户选定“用免费士兵动作包试做 IF-27 换弹和 P8 持枪”，随后提供 `Human Soldier Animations FREE.unitypackage`。本版已实际使用包内动作，提供独立可运行候选；没有用户视觉验收结论。

基线为 0.66.4 / e4bb251；原应用和原角色模型保留。仅本地保存，不推送 GitHub，不改用户既有五项装备参考文件删除。

## 实际源动作

| 用途 | 作者动作 | 原区间与时长 |
| --- | --- | --- |
| IF-27 换弹 | HumanM@AssaultRifle_Reload01 | Blender 1–52 帧 / 1.7 秒，游戏播放 1.9 秒 |
| IF-27 持枪 | HumanM@AssaultRifle_Aim01 | 1–71 帧 / 2.333 秒 |
| P8 双手持枪 | HumanM@Gun_Aim02 | 1–71 帧 / 2.333 秒 |

实查 Gun_Aim01 是单手，故选 Aim02。源 action 含负帧和额外关键帧，实际区间与随包 FBX 导入元数据对应。原包 SHA256：`5a9f20fb075363c8b28a97c3360c3c93d9a6004af6e5caf7bcb1e852f6783b40`。作者、许可、原件保存路径及 171 个文件资产路径详见 `art/characters/soldier-pack-trial-2026-10-27/SOURCES.md` 和 `package-manifest.json`。原件不纳入 Git，也不单独发布资产包。

## 适配方法与修改

1. 在作者附带的男性 Blender 源骨架上计算约束，以 60 Hz 采样肩肘参考、手腕、30 根手指骨及枪身变化。输出 `Assets/Resources/Animation/SoldierPackTrial.json`。肩部数据保留用于来源检查；第一人称实际肩点使用相机胸部锚定。
2. `PrototypeSoldierPackMotion` 读取源采样并按游戏换弹进度取样。手套保留 0.66.4 候选网格和独立骨架，每个指节使用源动作；额外匹配源、目标指骨方向，避免两个模型的拇指静止姿态差异放大。
3. `PrototypeSoldierArtRig` 保持臂长，以源肘部参考求解现有手臂；肩部留在镜头胸部位置。首轮直接复用全身肩点导致袖管遮挡，此问题已在后续迭代修正。
4. `PrototypeWeaponArtRig` 针对现有握把倾角加固定腕部对齐，保留源指节形状。IF-27 在原曲线上校准托枪、拔匣与插匣接触；分开驱动旧弹匣和新弹匣，握住时随手掌运动。
5. `PrototypePlayer` 使用源枪身位移／旋转，并加第一人称取景偏移，使换匣过程位于可见区域。弹药结算与既有换弹时长不变。
6. 原 IF-27 片段插匣后张手回到护木，**不含拉机柄／空仓上膛**。已取消误加的枪机接触映射；早期迭代中手掌穿过机匣的截图不可当作最终证据。
7. P8 本次替换双手持枪；其换弹主体沿用 0.66.4，起止处混合新持枪姿态。第三人称和其他枪继续既有表现。

## 本地文件

- 应用：`UnityProject/Builds/SoldierPack-0.66.5/IRONFRONT.app`
- 正常速度录像：`art/characters/soldier-pack-trial-2026-10-27/soldier-pack-30fps.mp4`
- 最终实机帧：该目录 `checks/pack-motion/`，含两枪持枪、瞄准、换弹连续帧和接触观察视角。
- 逐帧表：`review/if27-sequence-1.jpg` 至 `-3.jpg`；汇总：`review/Carbine-final.jpg`、`Pistol-final.jpg`。
- 原件和 Blender 源：`source/`，本地忽略；原动作检查图：`source-review/`。
- 采样／复现：`sample_soldier_pack.py`、`study_source.py`、`inspect_pack.py`、`run_checks.py`、`review_frames.py`。
- 来源与交付记录：`source-rig.json`、`sampling-report.json`、`data-validation.json`、`delivery.json`。

大型原件、应用、视频和逐帧图按仓库现有规则本地保留；运行资源、驱动、脚本、来源清单及最终文本报告纳入本地版本。

## 验证及视觉边界

最终构建 `build05.log` 报 PASS。`motion / rig / batch2 / if27 / sprint` 均 PASS，RuntimeErrors=0；最终文本证据复制到 `review/*-results.txt`。

motion、rig、batch2、sprint 对应 build04；build05 只修改 IF-27 开发探针，随后单独复测 if27。修正了旧探针两处假设：第三人称检查应读第三人称枪体；原包片段不拉柄，应验证拉柄保持原位，同时仍验证弹匣移动和复位。实际游戏表现自 build04 未再更改。

数据检查覆盖 103 / 141 / 141 个采样、每帧 30 个指节、向量有限及旋转归一化；源换弹左指节相对起始姿态最大变化约 125.7°。这些只证明数据和执行，不能证明动作自然。

已查看源关键帧、实机两枪持枪／瞄准、IF-27 全段逐帧表，以及拔匣、插匣、回托的反向接触图。最终录像为 306 帧、30 fps、10.2 秒；AVFoundation 独立解码 8 个时间点通过。电脑控制服务超时，未能在 UI 中完成连续播放观看，不能声称已完成正常速度人工观感验收。

候选仍使用上一版较简化的袖管与手套，不是新的完整角色美术。源动作适配后仍需用户实际试用判断；未证明所有视角、所有帧都零穿插。完整空仓上膛、P8 原包换弹及其他枪动作不属于本版已完成内容。
