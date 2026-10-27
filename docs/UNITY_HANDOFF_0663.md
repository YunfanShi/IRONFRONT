# 0.66.3 — P8 / IF-27 五指与换弹候选

2026-10-27。本轮按用户确认的方案实施；本地分支 codex/unity-port，基线 c21faf2 / 0.66.2，不推送远端。

## 使用范围

- 双队 P8、IF-27 的第一人称启用新手臂和换弹。
- 原 v02/v03、Resources/Soldiers 的既有两份士兵 prefab、其余五枪及第三人称仍保留原实现。候选资源在 Resources/Soldiers/HandCandidate。
- 枪械比例沿用 0.66.2；弹药、伤害、射速、1.9 秒 IF-27 / 1.3 秒 P8 换弹时间保持现有数值。

## 实施内容

1. 新网格替换原手套局部；五指各三段、双手 30 个指节，骨架从 20 骨改为候选 46 骨。保留原身体网格、身体权重与团队护甲材质。
2. 导入时以腕点、Middle1、Index1 和 Pinky1 建立手掌坐标。旧骨骼中用于粗略握姿的 90 度补偿不再用于候选手掌。
3. 候选独立控制左手抓握、抓拉柄时的捏合，以及右手食指。持枪、松手、取放弹匣、操作枪机和复位有不同指节姿态。
4. 旧弹匣移出后带到画面下方；独立的新弹匣从下方取出，对齐后插入。接触阶段手腕跟随当前弹匣的位姿。结束和中断均恢复原弹匣与手姿。
5. 第一人称前臂近腕区域收窄，护甲末端后移，增加柔性腕套并保留双面袖口渲染。

## 动作阶段

| 相位 | 表现 |
| --- | --- |
| 0–0.10 | 托枪手松开，食指离开扳机 |
| 0.08–0.26 | 左手伸向弹匣并闭合 |
| 0.26–0.47 | 沿插槽拔出，带到下方并松手 |
| 0.49–0.64 | 取独立新弹匣，抬起并对齐 |
| 0.64–0.72 | 沿插槽插入 |
| 0.72–0.83 | 松开弹匣，手移至套筒/拉柄 |
| 0.83–0.92 | 拉动、松开，机件回位 |
| 0.92–1.00 | 返回托枪姿势 |

## 文件

- 源与脚本：art/characters/hand-reload-2026-10-27/
- 可编辑文件：candidate/{blue,red}/*-soldier-hand-candidate.blend
- FBX：candidate/{blue,red}/*-soldier-skinned-lod{0,1,2}.fbx
- 生成：build_candidate.py；独立重导入检查：validate_candidate.py / validation.json
- Unity 导入：SoldierArtAssetSetup.BuildHandCandidates；导入并构建：BuildHandCandidatePlayer
- 独立应用：UnityProject/Builds/HandReload-0.66.3/IRONFRONT.app
- 最终实机证据：checks/final-*；正常速度视频：reload-review-30fps.mp4
- 原始关键文件备份及 SHA256：baseline/、baseline-manifest.json

## 来源与授权

手部拓扑和初始指节权重改编自 para 的 **FPS Arms (rigged only)**，OpenGameArt 页面明确标为 CC0：
https://opengameart.org/content/fps-arms-rigged-only

下载源文件存于 source/fps-arms-para-cc0.7z。仅复用其手部局部，重新裁切、缩放、定向、绑定到现有腕骨，加入腕套；换弹阶段为本项目重新实现。没有引入收费动作包或完整 Humanoid 动画系统。

## 验证与边界

源资产检查：双队六份 FBX 新会话重导入，46 骨、30 个指节；全部顶点有权重，最多四权重，归一化误差 < 4.5e-8。双队原身体三档 LOD 的几何与权重哈希不变。Blender 资产检查无无效顶点、孤立顶点或退化面。袖口、肩部裁口为有意保留的开放边界，不作为封闭实体交付。

最终构建 build06：Unity 6000.5.10f1，StandaloneOSX，0.66.3，构建成功。五组实机检查全部 PASS / RuntimeErrors=0：

- final-motion：P8、IF-27 实际时长连续帧及 11 个持枪/瞄准/换弹关键姿势，近景观察图。
- final-rig：双队七枪，含候选 30 个独立指节、臂长、瞄准中心无遮挡；两枪在 5 个换弹相位中断后，弹匣位姿及新弹匣可见性均恢复。
- final-batch2：六枪机件、弹药和显示回归。
- final-if27：IF-27 枪械与换弹回归。
- final-sprint：冲刺与战斗恢复回归。

原始资源保护：BlueSoldier.prefab、RedSoldier.prefab 和 PrototypeSoldierVisual.cs 与基线 SHA256 一致。既有五项道具参考删除未改动。

已人工查看最终握把、拔出/插入弹匣、拉套筒近景和关键帧；正常速度视频包含 306 帧，30 FPS，10.2 秒。独立 AVFoundation 解码核实 30 FPS / 960×640 / 10.2 秒，并抽查 8 个时刻。视频前半为 IF-27，后半为 P8，包含持枪、开镜、换弹和复位。保存结果摘要在 final-results.json，便携结果与精选截图在 final-review/。

这是两把枪的第一人称候选版本。仍使用现有低多边形护甲与程序化动作；近景材质及袖口过渡可以继续美术打磨。最终视觉判断需结合正常速度视频，数值接触点或骨长检查不代表所有视角零穿插。没有把候选自动扩展到其余枪械或第三人称，也没有进行本轮无关的联机压力测试。

源 .blend、源 FBX、贴图、原始截图和视频按现有 art/.gitignore 规则保存在本地；Unity 运行所需的新 FBX、材质、网格与 prefab 纳入本地代码提交。
