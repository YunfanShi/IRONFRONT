# P8 第三人称冲刺动作接入

日期：2026-10-29。用户查看实验室候选后要求将 P8 第三人称冲刺改动加入游戏。

- 实验室候选：`6f3e2b0`，记录位于工作区 `IRONFRONT P8动作实验室/LabTools/P8第三人称冲刺候选记录.md`。
- 游戏改动：`UnityProject/Assets/Scripts/PrototypeSoldierVisual.cs`。P8 从单手低位携枪切换为双手低位携枪，VX-9 及其他枪保留此前的双手分支；该姿态也可随普通移动的动作权重混入。第一人称 P8 持枪与换弹代码未改。
- 独立开发构建：`UnityProject/Builds/P8SprintUpdate-2026-10-29/IRONFRONT.app`，日志 `UnityProject/Builds/P8SprintUpdate-2026-10-29-build.log` 包含 `IRONFRONT optimization build PASS`。内部版本号仍为 0.66.12，以构建路径区别旧版。
- 实验室中的蓝队背面、侧面及近景采样已生成对比视频，属于按时间步重建的画面。本次游戏构建后未执行额外运行或玩法测试。
- 仅本地保存，不推送或合并；保留游戏仓库中用户既有五项装备参考文件删除。
