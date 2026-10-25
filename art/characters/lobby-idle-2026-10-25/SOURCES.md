# 大厅姿势资源调查 — 2026-10-25

## 实际使用

Quaternius Universal Animation Library Standard，作者官方免费版本：
https://quaternius.itch.io/universal-animation-library
https://quaternius.com/packs/universalanimationlibrary.html

从作者 itch 页面中的「No thanks, just take me to the downloads」取得公开 Standard 下载。ZIP 15,904,933 字节，SHA-256 `cc73fc4e495b82958207316596317a3f40b9fa38065bde1027937452da537724`。完整包、原始 README 和 License 均保存于 source。CC0 1.0，允许商业使用。

本次下载的免费 Standard 包为 43 个动作，含 Idle_Loop、Pistol_Idle_Loop；没有步枪待机。官网 120+ 是完整库数量，不能当成免费版数量。没有购买 Pro/Source。

实际使用 Idle_Loop 的骨盆、脊柱、胸腔、头部小幅动态增量。GLB 经 Blender 5.2 导入后原时间长度 2.5 秒，以 30 Hz 采样；清除 NLA、仅选择指定 action，以骨骼朝向差做坐标系转换。平滑去除微小循环端点残差。Unity 播放速度 0.75（3.333 秒/循环），身体幅度 35–65%。脚、手由现有身材与枪械接触目标求解。不是直接照搬手枪动作，也没有更换现有士兵或战斗动画。

复现：Blender --background --python sample_idle.py。输出 UnityProject/Assets/Resources/Menu/Animation/LobbyIdleBody.json。

## 后续候选（只查询，未下载/购买）

- Mixamo：https://www.mixamo.com/ 。Adobe 官方 FAQ：https://helpx.adobe.com/creative-cloud/faq/mixamo-faq.html 。Adobe ID 免费，可用于商业游戏。官方列出账号 country code China 不可用；所以不能保证当前用户可直接获取。适合搜索 Rifle Idle、Rifle Walk 等，再适配当前骨骼。
- MoCap Online Rifle：https://mocaponline.com/products/rifle 。官方当前 Starter $5.99 / Basic $44.99 / Pro $149.99；有 Unity、FBX、Blender 等格式。持枪、瞄准、走跑、蹲姿更成套。不同档位内容不同，购买前按动作清单确认。
- Kubold Rifle Animset Pro：https://assetstore.unity.com/packages/3d/animations/rifle-animset-pro-15098 。官方当前 $50；作者 FAQ https://www.kubold.com/faq-unity 。动画可复用，但演示控制器/PlayMaker 资料较旧，不能等同于本项目 Unity/URP 即插即用。

目前士兵为定制 16 驱动点加合并手指骨，未配置可直接通用重定向的 Unity Humanoid Avatar。完整升级应保留模型，建立并验证标准人形映射，导入步枪动作集，并保留双手握持/落脚 IK。已有模型和运行时玩法不应为了测试一个动作库而整体覆盖。
