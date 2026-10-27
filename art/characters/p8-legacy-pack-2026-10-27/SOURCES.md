# 动作与模型来源

- 本轮基线：`ed6143f` / 0.66.9；旧手臂对应独立五指之前的 0.66.2（`c21faf2`）。
- 用户提供的 `Human Soldier Animations FREE.unitypackage`，SHA-256：`5a9f20fb075363c8b28a97c3360c3c93d9a6004af6e5caf7bcb1e852f6783b40`。
- 随包 Blender 原件 SHA-256：`b1d45ea5f9281708d5fcac1294bc7c1989e7a3bfbc82fb97c11a7fb7dc01e64a`。
- P8 源动作：`HumanM@Gun_Aim02`、`HumanM@Gun_Reload02`。本轮沿用 0.66.9 的 103 帧 / 60 Hz 采样数据，`SoldierPackTrial.json` 哈希不变。
- 模型直接使用本工程 `Resources/Soldiers/{Blue,Red}Soldier` 原资源。既有网格、骨架、绑定权重和材质未修改；P8 的运行时实例选择由候选五指切回原手臂。
- [动作包来源与许可记录](../soldier-pack-trial-2026-10-27/SOURCES.md)；[Reload02 采样记录](../p8-reload02-2026-10-27/SOURCES.md)。原件按现有规则仅留本地。
- 比较片左侧使用 0.66.2 最终 build-06 的 Player 帧，按历史探针相位重定时；右侧为本轮最终 Player 帧。详细映射、帧哈希与视频哈希见 `legacy-comparison-manifest.json`。
