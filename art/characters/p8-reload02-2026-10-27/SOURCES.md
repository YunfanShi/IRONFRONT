# P8 Gun_Reload02 来源

用户提供的 Kevin Iglesias《Human Soldier Animations FREE 2.0》，沿用 [0.66.5 原包与许可记录](../soldier-pack-trial-2026-10-27/SOURCES.md)。本次不重新下载或发布原始素材包。

- 原包 SHA256：`5a9f20fb075363c8b28a97c3360c3c93d9a6004af6e5caf7bcb1e852f6783b40`。
- 作者 Blender 源 SHA256：`b1d45ea5f9281708d5fcac1294bc7c1989e7a3bfbc82fb97c11a7fb7dc01e64a`。
- 源路径：`../soldier-pack-trial-2026-10-27/source/Blender/HumanM_SoldierAnimationsFREE_2.0.blend`。
- 实际片段：`HumanM@Gun_Reload02`；随包 Unity 导入帧 0–51，对应 Blender 1–52，30 fps，1.7 秒。
- 采样脚本：`sample_pistol_reload.py`，读取源骨架并计算约束后以 60 Hz 采样 103 帧；包括 30 指节、手腕、肘部参考和枪身运动。没有修改作者源文件。
- 游戏映射：完整片段映射到现有 1.3 秒，速度约为源动作的 1.31 倍；运行时添加当前 P8 的掌心接触、插入路径及镜头空间校正。

原动作没有独立可见弹匣，也没有枪顶拉套筒段。新旧弹匣跟手、显隐与插入对齐由本项目实现。手套、袖管和武器沿用既有 0.66.8 资源。
