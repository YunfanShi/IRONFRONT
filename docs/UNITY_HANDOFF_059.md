> 0.59.1 更新：已恢复第一人称枪托，下文“第一人称关闭枪托”是 0.59.0 的历史行为，现已修正。见 [UNITY_HANDOFF_0591.md](UNITY_HANDOFF_0591.md)。

# Unity 0.59.0 — IF-27 枪械资产处理与接入

2026-10-24。将用户提供的 `20c68fb74bdce1d09948dd1b9bd0dd54.glb` 加工为 IF-27 卡宾枪。只替换 Carbine；原始高模、其他枪械、红蓝士兵、全部载具、旧程序和既有未提交工作保留。

## 交付位置

- 试玩程序：`UnityProject/Builds/Weapons-0.59.0/IRONFRONT.app`。
- 编辑源文件：`art/weapons/if27/if27-articulated.blend`，贴图已打包。
- 通用模型：同目录 `exports/if27.glb`，四档 `exports/if27_lod0.fbx` 至 `if27_lod3.fbx`。
- 视频：`art/weapons/if27/IF27-preview-0.59.0.mp4`，30fps，展示第一人称、开镜、两种视角的换弹和第三人称握持。
- 实机对照图：`art/weapons/if27/evidence/game-review.png`、`reload-review.png`。
- 原文件副本：`art/weapons/if27/source/original.glb`。SHA-256：`f84a9cf3632819f26c054cf42ee77839de1c9467b992f9b70ce1291b612667b4`，与 Downloads 中原文件一致。

## 资产处理

源文件为一个网格、1,499,986 个三角面、一个 PBR 材质、三张 4K 贴图，无骨架和动作。焊接重合 UV 接缝后，原几何没有开边；保留 UV 和原始外形，进行减面，随后将高模表面烘焙为低模切线法线。运行贴图为 2K Base Color、Normal、Metallic/Roughness；Unity 另生成 Metallic/Smoothness 通道图。

| 级别 | Blender 导出前统计 | 独立 FBX 回读统计 | 用途 |
| --- | ---: | ---: | --- |
| LOD0 | 62,498 | 62,498 | 精细源模型、第一人称 |
| LOD1 | 18,442 | 18,426 | 第三人称近景 |
| LOD2 | 6,145 | 6,128 | 第三人称中景 |
| LOD3 | 2,224 | 2,207 | 远景 |

第一人称关闭独立枪托网格，实际可见枪械约 48,074 面，另有小型红点。第三人称保留完整枪托。世界中全枪长约 0.919m，这是游戏比例选择，不是对真实枪型的规格认定。远距离 FBX 的导入三角化统计与导出前略有差别，分别记录，不声称逐面无损。

分件为 Body、Magazine、ChargingHandle、Bolt、Stock。弹匣入口和暴露端面补面，拉机柄添加连接杆，枪机为内部装饰滑动件。源模型瞄具窗口原为实心，在外壳内做贯通孔，使开镜视线穿过真实几何。没有更换整把枪的设计或拼接其他枪型。

该资产用于实时渲染。局部重建、切分和低级别减面仍存在少量非流形边/孤立顶点，详情在 `evidence/final-lod*.json`；不是封闭打印模型。回读无无效坐标、零长度边或退化面，图像检查确认可见面、主要轮廓和瞄具开口。几何指标不代替视觉验收。

## Unity 接入

`WeaponArtAssetSetup` 导入 FBX、设置贴图、创建 URP 材质，并生成 `Resources/Weapons/IF27View`、`IF27World`。资源通过 `PrototypeWeaponVisual.Build` 接入玩家、AI、远端角色、配装缩略图和大厅；缺少资源时保留原程序化模型回退。

`PrototypeWeaponArtRig` 管理左右手、枪口、瞄准、弹匣和拉机柄接触点。FBX 空物体的缩放被归一化，避免小型红点被放大。左右手目标随枪械移动；左手改为护木握持，换弹时依次接触弹匣、拉机柄，再回到护木。世界和第一人称共用动作相位，保留原弹量和装填时间。第一人称换弹抬枪并侧倾，避免旧动作把弹匣压出画面。第三人称增加握把与胸前护甲的距离并向内轻转枪身，大厅同步修正，避免直接套用旧模型原点导致枪身埋入胸口。

本地玩家实弹和枪口特效使用新 Muzzle 点。新增枪机短行程和第一人称射击回弹。第三人称仍受实际臂展约束。大厅另有独立姿势代码，已同步新握持点，保留枪械贴图材质。未增加网络装填相位协议；远端角色使用新外形，网络动作同步维持原能力。

现有士兵使用合并的手指骨骼，保留该骨架；没有逐指扳机动作，也没有新增动作捕捉素材。换弹是程序化分件及手臂联动，尚不能等同于战地级精细手部表演。

## 验证与保留

- `evidence/player/results.txt`：最终独立开发版运行，0.59.0，运行错误为零；检查实际库存中的新模型、世界 LOD、红点尺寸、瞄具中心对齐、真实网格的贯通视线、左右手握持、弹匣/拉机柄往复及复位。
- 连续采集第一/第三人称换弹，站立、跑步、蹲伏、趴伏，以及真实开火。开火验证弹量减少一发，弹丸初始点与新枪口一致。探针使用真实玩家对象和方法驱动等价状态，不声称通过物理键盘操作。
- `evidence/batch-results.txt`：原五组回归通过，包含视角、持有装备、9 类载具的 28 次座位切换、大厅 UI、864 个蒙皮姿态以及 2304 个方向步态采样。完成第一人称换弹取景和第三人称持枪位置修正后，已重新运行五组回归和独立程序。
- `evidence/leg-code-preservation.json`：与任务开始时保留的 0.58.2 源码比较，距离驱动步态、下半身姿势、脚步曲线及腿部求解代码不变。
- `evidence/preservation.json`：核对既有车辆、士兵源模型清单、0.58.2 程序/视频和本次原始 GLB。
- `evidence/image-checks.json`：最终实机图像空白/黑帧/洋红检查；已打开实机画面、换弹关键帧和最终模型六视图。视频进行了编码和解码帧回读，不声称真人实时观看。
- `work/build-player-final.log`：Unity 6000.5.10f1，macOS 开发版构建。最终体积约 543MB。
- `evidence/packed-textures.json`：实际 GLB 内嵌的三张贴图均为 2048×2048，避免 Blender 沿用旧 4K 打包字节。

## 复现

1. `python3 art/weapons/if27/rebuild.py`，可用 `BLENDER_EXECUTABLE` 指定 Blender。依次从保留原文件制作工作网格、分件、烘焙法线并最终导出。
2. 将 `exports/*.fbx`、`textures/*.png` 复制到 `UnityProject/Assets/Art/Weapons/IF27/`，运行菜单 `IRONFRONT/Art/Build IF27 Weapon` 或 `WeaponArtAssetSetup.Build`。
3. 使用 `WeaponArtAssetSetup.BuildPlayer` 和 `-buildOutput` 构建；普通构建入口仍保留。
4. 独立程序参数：`-ironfrontWeaponProbe <输出目录>`。仅编辑器或开发版启用，正常启动不会生成测试场景或自动退出。
5. `review.py` 检查最终图像、保留文件并生成预览序列。

当前分支仍为 `codex/unity-port`。任务开始时 HEAD 与已有 origin/main 引用相差 36/5，且包含大量此前未提交工作；本次没有自动合并、提交或推送这些历史工作。
