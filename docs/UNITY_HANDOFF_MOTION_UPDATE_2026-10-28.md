# 动作接入记录：P8 与 IF-27

日期：2026-10-28。基于游戏提交 `bf3a8d1` / 0.66.12，用户要求把当前动作改动加入游戏。

- P8：接入独立动作实验室候选 `5ac4667` 的换弹左手腕基准、平滑切换及对应新弹匣偏移，沿用旧手臂与 `Gun_Aim02` / `Gun_Reload02`。持枪原有姿态本次未改。该候选在先前分相位图中减轻前臂翻转；实际掌面、拇指及弹匣接触尚未逐角度视觉验收。
- IF-27：按用户选择恢复 0.66.2 的原士兵手臂（四指共用弯曲骨）、程序持枪与换弹；不使用独立五指候选或步枪动作包。用户选择记录在 `IRONFRONT P8动作实验室/LabTools/IF-27动作决定.md`。
- 改动位置：`UnityProject/Assets/Scripts/PrototypeWeaponArtRig.cs`。其余武器的选择路径未改。

## 应用与检查

- 独立开发构建：`UnityProject/Builds/MotionUpdate-2026-10-28/IRONFRONT.app`。保留旧应用；本构建仍显示工程原有版本号 0.66.12，应通过此路径辨认，不能根据版本号区分旧应用。
- Unity CLI 独立构建日志：`UnityProject/Builds/MotionUpdate-2026-10-28-build.log`，`IRONFRONT optimization build PASS`。
- 实际开发播放器使用既有 `-ironfrontRigAudit -rigMotion` 采集蓝队 P8 与 IF-27；结果 `PASS`、`RuntimeErrors=0`。证据：`../IRONFRONT P8动作实验室/Evidence/game-motion-update/`。IF-27 持枪画面与 0.66.2 参考画面相符，P8 插匣阶段呈事实验室候选。此检查不能证明所有视觉角度无穿插，也不覆盖所有玩法或队伍。
- 本次仅本地保存，不推送远端、不合并主分支。保留用户已有五项装备参考文件删除。
