# 0.66.6 — 袖管体积修正

## 请求与范围

用户在 0.66.5 免费士兵动作试版后指出手臂偏细，随后明确要求“修正袖管体积”。本版修正红蓝两队 P8、IF-27 第一人称候选袖管；基线为 0.66.5 / c1050d2。

旧应用、v04 候选资源和源模型保留。继续在 `codex/unity-port` 本地保存，不推送、不合并 main；用户既有五项装备参考文件删除未纳入本轮提交。

## 实际修改

`art/characters/sleeve-volume-2026-10-27/build_sleeves.py` 从上一版生成流程派生，仅调整袖管环形截面和护臂片。手套仍使用既有 CC0 手部拓扑；不引入新来源资产。

| 部位 | 0.66.5 半径，宽向 / 厚向 | 0.66.6 半径，宽向 / 厚向 |
| --- | --- | --- |
| 袖口外缘 | 0.029 / 0.025 m | 0.033 / 0.028 m |
| 前臂中段，距腕 0.115 m | 0.036 / 0.031 m | 0.048 / 0.041 m |
| 近肩截面 | 0.054 / 0.045 m | 0.069 / 0.058 m |
| 袖口内收边 | 0.025 / 0.021 m | 保持一致 |

前臂中段截面宽增加 33.3%、厚增加 32.3%；这是截面尺寸变化，不是体积百分比。按照既有第一人称 0.55/0.65 缩放，忽略细微褶皱时，该截面由约 6.09 × 5.25 cm 增至 8.12 × 6.94 cm。上臂适度加粗，向腕部逐渐收紧。护臂片宽度增加 18%，厚度不变，底面随新袖管表面外移。

候选仍是第一人称专用袖管：源文件上臂后段包含避免近景露出断面的延长部分，不作为第三人称身体外观。第三人称使用原角色资源。

Unity 编辑器导入指向独立 `hand-candidate-v06` 文件夹，并更新现有 HandCandidate 预制体引用。动作驱动、枪械尺寸、握点和玩法参数不变。

## 保持一致的内容与检查

`validate_sleeves.py` 对旧、新 Blender 文件直接比较：

- 双队身体全部 LOD 的几何、拓扑和权重一致。
- 46 根骨骼的名称、层级、静止头尾和矩阵一致，含 30 根独立指节。
- 按与指节加权顶点连通的组件识别完整手套；每手 1689 个顶点，坐标、拓扑和权重哈希一致。
- 所有手臂网格拓扑、权重一致；每条手臂仅 624 个袖管／护臂顶点的位置变化。
- 六份 FBX 在清空场景后逐一重新导入通过；无旧合并 Fingers 骨，最大 4 个权重，权重归一化误差小于 2e-5。

`baseline.json` 和 `delivery.json` 记录五项运行文件 SHA256 比对：PrototypePlayer、PrototypeWeaponArtRig、PrototypeSoldierArtRig、PrototypeSoldierPackMotion 和 SoldierPackTrial.json 均不变。

## 实机与视觉验证

Unity 6000.5.10f1 的 `build01.log` 包含 `HAND_CANDIDATE_IMPORT_PASS` 和 `IRONFRONT optimization build PASS`。原生应用执行以下检查，均 PASS、RuntimeErrors=0：

| 检查 | 范围 |
| --- | --- |
| motion | 蓝队 P8、IF-27 持枪、瞄准、换弹关键帧与 306 帧连续采集 |
| rig | 双队七枪的骨长、实际蒙皮、瞄准中心射线、原有手指／动作来源及换弹复位检查 |
| sprint | 冲刺收枪、开镜／开火中断及既有移动规则 |

已查看新旧同机位对照、两枪持枪／瞄准／换弹关键帧和间隔 0.1 秒逐帧表、腕部反向接触视角、红队关键帧、冲刺收枪画面。袖管中段比基线更饱满，腕部仍收紧；所检查的瞄准中心没有袖管遮挡。正常速度录像为 306 帧、30 fps、10.2 秒；AVFoundation 独立解码 8 个时间点通过，并查看了解码样图。

这些结果只支持本轮袖管体积修正，不代表用户已经验收完整动作。未在 UI 中观看视频连续播放。既有简化褶皱、材质和换弹逻辑保留；没有据此声称所有帧、所有接触角度零穿插。

## 本地交付

- 应用：`UnityProject/Builds/SleeveVolume-0.66.6/IRONFRONT.app`
- 录像：`art/characters/sleeve-volume-2026-10-27/sleeve-volume-30fps.mp4`
- 新旧对比：该目录 `review/sleeve-before-after.jpg`，左为 0.66.5、右为 0.66.6。
- 分枪对比：`review/Carbine-hold-comparison.jpg`、`Carbine-reload-comparison.jpg`、对应 Pistol 两张。
- 关键帧与动作表：`review/{Carbine,Pistol}-final.jpg`、`{Carbine,Pistol}-sequence.jpg`。
- 原始实机证据：`checks/sleeve-motion`、`checks/sleeve-rig`、`checks/sleeve-sprint`；最终文本结果在 `review/*-results.txt`。
- 源模型：`candidate/{blue,red}/*.blend` 与三档 FBX；源码和校验脚本在该任务目录。

运行资源、脚本、版本文档和文本验证纳入本地 Git；大型源文件、重复导出、逐帧图、应用和录像按仓库现有规则保留本地。

## 复现

1. Blender 执行 `build_sleeves.py`，随后执行 `validate_sleeves.py`；依赖上一轮已保留的手部源文件及 v03 身体源文件。
2. Unity 调用 `Ironfront.UnityPrototype.Editor.SoldierArtAssetSetup.BuildHandCandidatePlayer`，构建到上述独立应用路径。
3. 运行 `run_checks.py motion rig` 和 `run_checks.py sprint`。
4. 使用带 Pillow 的 Python 运行 `review_frames.py`。录像编码与解码沿用 `hand-reload-2026-10-27/encode_review.swift`、`inspect_video.swift`。
