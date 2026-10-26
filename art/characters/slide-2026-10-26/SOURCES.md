# 滑铲动作来源与适配

2026-10-26，IRONFRONT 0.66.0。

- 作者：Quaternius。
- 官方下载页：https://quaternius.itch.io/universal-animation-library-2
- 官方介绍：https://quaternius.com/packs/universalanimationlibrary2.html
- 使用免费 Standard 包，许可为 CC0 1.0；原文保存在 `source/Universal Animation Library 2[Standard]/License.txt`。没有购买或使用 Pro/Source 包。
- 原压缩包：`source/Universal-Animation-Library-2-Standard.zip`，18,735,003 字节。
- SHA256：`4008ea208a604773a2b2177d965f0f5d3195498b5bf838c3f5785d68e95f2a68`。

采样原件：`source/Universal Animation Library 2[Standard]/Unreal-Godot/UAL2_Standard.glb`。官方 Unity FBX 与 Root Motion 版本也保留在 source 中，但运行时没有直接替换现有士兵骨架或模型。

| 原动作 | 原时长 | 游戏播放时长 |
| --- | ---: | ---: |
| Slide_Start | 0.833333 秒 | 0.26 秒 |
| Slide_Loop | 2 秒 | 0.52 秒 |
| Slide_Exit | 0.5 秒 | 0.30 秒 |

`sample_slide.py` 使用独立 Blender 场景读取源 GLB，以 60 Hz 采样骨盆、腰胸、头、腿方向和脚部旋转，生成 `UnityProject/Assets/Resources/Animation/InfantrySlide.json`。运行时按语义重定向到原士兵骨骼，保留骨段长度和蒙皮；上肢使用现有枪械握点 IK，最终混合后补偿靴底接地。位移由角色碰撞控制器处理，不应用源动画的根位移。

复现：在仓库根目录运行 Blender `--background --python art/characters/slide-2026-10-26/sample_slide.py`。脚本会更新上述 JSON 和 `source-motion-audit.json`。原件已保留在本机；仓库 art 忽略规则不上传重复的大体积压缩包/GLB/FBX，其他机器重新制作时需从官方获取同一包。运行游戏只需已生成的 JSON。

完整实现和验证见 `docs/UNITY_HANDOFF_066.md`。
