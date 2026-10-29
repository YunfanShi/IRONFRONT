# VX-9 冲刺动作接入

日期：2026-10-29。用户观看实验室的前后对比后，要求将候选应用到游戏。

- 实验室候选提交：`56c64f6`，记录位于工作区 `IRONFRONT P8动作实验室/LabTools/VX9冲刺候选记录.md`。
- 游戏变更：`UnityProject/Assets/Scripts/PrototypeSoldierVisual.cs`；VX-9 使用既有双手携枪分支，P8 保持单手携枪分支。动作权重在部分普通移动时也可能混入双手携枪。
- 保留此前 P8 换弹候选及 IF-27 0.66.2 动作接入。旧应用不覆盖。
- 独立构建：`UnityProject/Builds/VX9SprintUpdate-2026-10-29/IRONFRONT.app`。构建日志 `UnityProject/Builds/VX9SprintUpdate-2026-10-29-build.log` 有 `IRONFRONT optimization build PASS`。内部版本号仍为 0.66.12，须通过上述路径区分。
- 实验室已生成蓝队第三人称冲刺和停步的多角度采样与对比视频；该视频为采样帧合成，不是实时游戏录屏。游戏新构建本次没有执行额外玩法或运行测试。
- 仅本地保存，不推送或合并。保留游戏仓库中用户既有五项装备参考文件删除。
