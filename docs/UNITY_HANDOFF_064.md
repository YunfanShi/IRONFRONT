# Unity 0.64.0 — 六款装备模型替换

2026-10-26。将用户提供并已完成优化的六款装备接入现有 IRONFRONT。新应用：`UnityProject/Builds/Equipment-0.64.0/IRONFRONT.app`；旧 0.63.1 应用和 Downloads 中的原始 GLB 保留。

| 资源 | 游戏内位置 | LOD0 三角形 |
| --- | --- | ---: |
| SupplyBag / Asset_01 | 突击兵腰后携带、配装卡、按 6 部署的补给物 | 24,000 |
| Beacon / Asset_02 | 侦察兵携带、配装卡、X 部署的重生信标 | 15,998 |
| FragGrenade / Asset_03 | 配装卡、第一/第三人称手持、实际投掷物 | 16,000 |
| MedicalKit / Asset_04 | 医疗兵腰侧携带、配装卡 | 18,000 |
| RepairTool / Asset_05 | 工程兵腰侧携带、配装卡 | 20,000 |
| SmokeGrenade / Asset_06 | 配装卡、第一/第三人称手持、投掷物与落地烟罐 | 32,000 |

各款均使用三档 LOD、2K BaseColor/Normal/MetallicSmoothness 贴图与 URP Lit 材质。修正 FBX 的 X 轴旋转，按作者尺寸设置摆放基准；两种投掷物沿用现有手持比例。第一人称与卡片固定 LOD0，场景装备按距离切换。菜单保留新装备的真实贴图。

补给包碰撞箱根据新模型边界生成，信标与补给包的落点计入道路和基地地板高度。工程兵发射器、地图固定补给柜、治疗/维修即时逻辑、道具数量、冷却、爆炸和烟雾规则沿用原系统。医疗兵旧悬空方块标记仅在旧角色回退模型中保留，新医疗包自带十字标识。

这是静态装备替换：医疗/维修尚无专门的手持使用动画；手雷的拉环和保险杆仍包含在整体网格中，没有新增拔销或脱杆动画。

## 文件与复现

- 模型、贴图和材质：`UnityProject/Assets/Art/Equipment/`。
- 可加载预制件：`UnityProject/Assets/Resources/Equipment/`。
- 导入与预制件生成：`Assets/Editor/EquipmentArtAssetSetup.cs`，菜单 `IRONFRONT/Art/Build Six Equipment Prefabs`。
- 运行时工厂与挂载：`Assets/Scripts/PrototypeEquipmentArt.cs`；原构建器保留缺资源时的回退模型。
- 修改前文件：`art/equipment/integration-2026-10-26/baseline/`。
- 导入尺寸和面数：`art/equipment/integration-2026-10-26/import.txt`。
- 构建日志与实际运行画面：同目录 `build.log`、`runtime/`。
- 开发版带 `-ironfrontEquipmentArtProbe <输出目录>` 可重现配装、手持、投掷和部署的定向运行记录；普通启动不执行此入口。

未自动提交、合并或推送 Git 分支。

## 交付结果

Unity 6000.5.10f1 StandaloneOSX 构建成功，应用 686,601,913 字节。最终开发版定向运行记录为 PASS、RuntimeErrors=0：六款资源和 PBR 贴图正常加载、三档 LOD 存在，两种投掷物实际消耗库存并保留 Rigidbody/SphereCollider，信标和补给包落在可见地板上。已保存并查看配装卡、人物前后、第一人称和部署画面。只运行此次替换相关路径，未重跑整个项目测试。
