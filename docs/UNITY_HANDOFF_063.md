# Unity 0.63.0 — 大厅持枪待机与现成动作资源调查

2026-10-25。用户要求优化大厅人物姿势，并上网寻找可以现用的射击游戏骨骼。保留现有士兵、枪械、战斗动画和此前 UI；新增独立菜单动作模块。

## 实际效果

- 大厅／配装人物从静态摆姿改为循环待机：身体站直，前后错脚，胸部、骨盆、头部轻微变化。两脚在循环中保持固定接触。
- 步枪斜向下持握，双肘靠近身体；手枪独立校准。按实际枪械握点解算双臂，超过手臂自然长度时微调整支枪的位置，不拉伸骨骼。
- 原有模型比例、材质、枪托和握点继续使用。菜单专属 LateUpdate 驱动，原战斗角色的 PrototypeSoldierVisual、PrototypeSoldierArtRig、移动／跑步／蹲姿／趴伏代码未修改。
- 配装预览相机抬高 0.04 米，给新站姿在超宽屏中留出头顶与导航栏的间距。大厅居中、背景、名片、导航、装备卡和字体布局保持。

## 下载资源与适配范围

从作者官方网站获取 **Quaternius Universal Animation Library Standard**，附带 CC0 1.0 许可。实际免费包包含 43 个动作，含普通待机和手枪待机，**没有步枪待机**。官网 120+ 为完整库数量，不能将其当作免费版内容。本次没有购买付费资源。

使用 `Idle_Loop` 的骨盆／脊柱／胸腔／头部动态增量，保留原动作时间，再以 0.75 速度播放为约 3.333 秒循环。根据当前角色降低幅度，并修正导出的小幅首尾残差。腿部保持接地 IK，双手绑定现有武器握点。它是现有人物上的菜单适配，并非已完成全角色 Unity Humanoid 重绑定。

原下载包、README、License、提取脚本、数据审计均在 `art/characters/lobby-idle-2026-10-25/`。官方来源、价格／地区限制和后续候选记录见 `SOURCES.md`：

- Quaternius：https://quaternius.itch.io/universal-animation-library 。本次实际使用其免费 CC0 普通待机。
- Mixamo：https://helpx.adobe.com/creative-cloud/faq/mixamo-faq.html 。Adobe ID 免费且允许商业游戏，官方列出 country code China 不可用；未上传本项目模型或下载动作。
- MoCap Online Rifle：https://mocaponline.com/products/rifle 。枪械动作更成套，支持 Unity／FBX／Blender 等格式；未购买。后续完整射击动作升级优先比较其动作清单。
- Kubold Rifle Animset Pro：https://assetstore.unity.com/packages/3d/animations/rifle-animset-pro-15098 。可作为另一个步枪动作候选，演示控制器较旧，不能视为当前 Unity／URP 直接即插即用。

当前士兵为定制驱动骨骼，手指合并，并未配置直接通用重定向的 Humanoid Avatar。后续大规模动作替换应单独建立并验证人形映射，针对不同身材保留手部和落脚约束。

## 修改文件

- `Assets/Scripts/PrototypeMenuOperatorPose.cs`：独立待机采样、身体映射、固定脚踝、自然弯肘、双手握持与武器可达性修正。
- `Assets/Scripts/PrototypeMenuPreview.cs`：接入新模块，替代一次性摆姿；配装预览竖直取景调整。
- `Assets/Resources/Menu/Animation/LobbyIdleBody.json`：仅保留需要的身体循环采样，原模型与完整下载包不打入游戏。
- `Assets/Resources/Menu/Animation/Quaternius-LICENSE.txt`：作者附带的许可说明。
- `Assets/Scripts/PrototypeMenuPoseProbe.cs`：开发版实际窗口检查入口。覆盖七把枪、循环接触、实际 LateUpdate 播放、正侧面图及正常速度视频帧。
- `art/characters/lobby-idle-2026-10-25/sample_idle.py`：Blender 5.2 原始资源采样复现脚本，输出 JSON，不改动现有士兵。

## 验证

- Unity 6000.5.10f1 / StandaloneOSX 最终构建 `build-04.log` PASS。独立应用 278 个文件，共 578,418,123 字节；交付哈希记录在 `delivery-manifest.json`。
- `pose-03/results.txt` PASS，RuntimeErrors=0。七把枪各采样 201 个循环时刻；双手目标最大误差 0.000290 米，脚踝最大漂移 0.000270 米，首尾相邻采样胸部位置差 0.000226 米。此数据证明约束与循环，不能单独代表艺术自然度或每个手指的贴合质量。
- 查看了实际正面、两侧面与不同枪械截图。`lobby-idle-0.63.0.mp4` 为真实程序在 30 fps 时间采样的两个完整循环，共 6.667 秒，未加速；已在 QuickTime 正常播放并检查关键帧。配装相机最终调整不影响该大厅视频的姿势、取景或动画。
- `ui-04/results.txt` PASS，RuntimeErrors=0。实际独立应用覆盖 960×600、1280×800、1600×900、1920×1080、2560×1080，四兵种、主副武器、选择面板、保存返回、字体、真实像素轮廓、点选、设置、LAN 入口和单人开局。未开展双客户端联机对局。
- `batch-results.txt` FINAL PASS：原「Verify Lobby Loadout」「Verify Class Loadout Policy」均通过，测试恢复原存档与设置。
- 427 个基准文件哈希未变，包含原士兵、菜单素材、既有玩法脚本与完整 0.62.0 应用。`preservation-result.json` 的 changed 为空。原模型和所有旧版本保留。
- 早期 `pose-01` 暴露脚踝节点命名差异，`pose-02` 暴露左手超出自然臂长，`ui-03` 暴露超宽屏头顶间距；均已修正。保留这些记录，最终证据采用 pose-03、ui-04 与 build-04。
- 原 PrototypeWeaponArtProbe 两项废弃 API 警告仍在，新代码无编译错误，最终应用无运行异常。

## 交付与复现

- 应用：`UnityProject/Builds/LobbyPose-0.63.0/IRONFRONT.app`，直接打开查看大厅。
- 视频：`art/characters/lobby-idle-2026-10-25/lobby-idle-0.63.0.mp4`。
- 大厅实机图：`art/characters/lobby-idle-2026-10-25/pose-03/lobby-pose.png`。
- 最终配装／其他界面实机图：同根目录 `ui-04/`。
- 姿势测试：应用参数 `-ironfrontMenuPoseProbe <输出目录>`。
- 菜单回归：应用参数 `-ironfrontLobbyProbe <输出目录>`。
- 编辑器回归：`PrototypeBatchVerification.Run -ironfrontChecks <本次 checks.txt 绝对路径>`，不加 `-quit`，由协调器退出。测试与其他写 PlayerPrefs 的游戏进程顺序运行。
- 原源码快照：本次目录 `baseline/`。版本号统一为 0.63.0。

仓库继续保留既有 codex/unity-port。任务开始时 HEAD 与本地 origin/main 引用相差 36/5，并有大量此前未提交成果；本次没有合并、提交或推送这些历史修改。
