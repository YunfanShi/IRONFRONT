# References

- 原角色：../blue-soldier/rigged-v03/blue-soldier-rigged-v03.blend 与 red 对应源；保留盔甲/布料风格、蓝红队身份。
- 原手部近景：../model-rig-audit-2026-10-26/hand-rest-anatomy.png；手指形体粗，近景独立弯曲能力待拓扑检查。
- 0.66.2 实机：../weapon-fit-2026-10-26/checks/motion/Blue-Carbine-reload40.png、reload87.png 及 Pistol 对应帧；手指固定握拳，拉柄阶段大面积护臂遮挡。
- para / MakeHuman FPS arms (rigged only)：https://opengameart.org/content/fps-arms-rigged-only ，下载 https://opengameart.org/sites/default/files/fps%20arms.7z 。官方页标 CC0，2026-10-27 核验。用于解剖、五指关节和蒙皮参考；如果采用部分几何，须在交付来源中注明。

实际采用：CC0 参考的手部拓扑和原始指节权重，局部裁切后重新缩放/定向，连接原士兵腕部并添加柔性腕套。新换弹由项目运行时代码编排。来源已在 docs/UNITY_HANDOFF_0663.md 中注明。
