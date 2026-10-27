# 0.66.4 — 第一人称握姿与换弹返工

## 状态

用户明确否决了 0.66.3 的视觉效果。此前将功能检查通过表述成视觉修复完成不成立。0.66.4 是进一步返工、可直接对照的候选，尚无用户视觉验收结论。

## 实际修改

- P8、IF-27 保留每手 15 个指节；把单一弯曲量改成独立关节姿态表，包括握枪、托枪、抓弹匣、P8 套筒和 IF-27 拉柄。拇指有独立对掌角度。
- 用实际 Unity 指节坐标和侧面／背面画面重新校准掌部及物体接触；P8 和 IF-27 的弹匣前后偏移不同。
- 调整第一人称肩肘位置，减少前臂斜顶手掌造成的腕部折角。P8 操作套筒时左肘向侧面绕行，避免袖管正对镜头遮住手掌。
- 原护甲被裁切后与新手腕截面不匹配，先后出现腕部与肘部开口。候选版改为从腕部到上臂的连续布料袖管，增加收口、布料 UV、简单护甲片；近镜头的肩部袖管延伸到镜头后方。最终收窄肘部与上臂截面，保持腕口尺寸，同步降低护臂片贴合高度。
- 换弹时抬高并侧转枪身，保持操作姿势；P8 缩短多余的拔匣行程。自由手到套筒、返回握把采用绕行弧线，插匣保留轴向运动。
- 新旧弹匣仍是不同物件，保持原有 1.9 秒／1.3 秒玩法时长、弹药结算和中断复位。
- 按 FBX 材质名称分配材质，每个 arm renderer 使用自身的材质列表。

## 本地交付

- 应用：`UnityProject/Builds/HandReload-0.66.4/IRONFRONT.app`
- 正常速度录像：`art/characters/hand-reload-2026-10-27/reload-refined-30fps.mp4`
- 实机原始帧及既有检查结果：`art/characters/hand-reload-2026-10-27/checks/refined-*`
- 关键画面：`art/characters/hand-reload-2026-10-27/refined-review/`
- 候选源文件：`art/characters/hand-reload-2026-10-27/candidate/{blue,red}/*-hand-candidate.blend`
- 可重建脚本：同目录 `build_candidate.py`；姿态拟合诊断：`fit_hand_poses.py`。
- 运行时姿态：`Assets/Scripts/PrototypeFingerPoses.cs`；手臂驱动：`PrototypeSoldierArtRig.cs`；换弹：`PrototypeWeaponArtRig.cs`。

原版士兵 v03、原身体网格及权重、其他枪械的手部资源、第三人称和旧应用保留。只在 P8 / IF-27 第一人称启用候选。CC0 手部来源与授权见 `reference_notes.md`。大型源文件和视频按既有规则保留在本地；Unity 运行资源、代码和重建脚本纳入本地版本。

## 验证与限制

最终构建日志为 `refine-build-slim.log`（含最后一轮左肘和袖管修订）。motion / rig / batch2 / if27 / sprint 均为 PASS、RuntimeErrors=0；视频为 306 帧、30 fps、10.2 秒，独立解码通过。

构建与实机检查结果以 `refined-review/*-results.txt` 为准。原有 motion / rig / batch2 / if27 / sprint 检查验证运行错误、双队七枪、瞄准遮挡、弹匣复位及相关玩法；这些检查不评判美术质量。

`validation.json` 验证双队原身体几何和权重不变、六份 FBX 独立重导入、46 根骨、30 个独立指节、每顶点最多 4 个权重且归一化。

本轮逐帧和多角度检查了新袖管、握把、弹匣与套筒／拉柄接触，并导出 30 fps 实际时长录像。新袖管的造型、护甲和材质细节比原角色简化；它是明确的候选美术取舍。手部仍为程序驱动的关键姿态插值，尚不是经过完整手工动画打磨的最终动作。不能把代码 PASS 或骨骼数量作为“看起来自然”的证明。

仅本地保存，不推送 GitHub。用户原有的五项装备参考文件删除不处理。
