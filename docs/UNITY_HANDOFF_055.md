# IRONFRONT T90 / V12 可动载具与 Unity 接入（0.55.0）

2026-10-23。基于已有 0.54.2 工作树与载具交接，复用用户已交付的模型。实际使用 Blender 5.2.2 LTS、Unity 6000.5.10f1 / URP，继续在 `codex/unity-port` 本地工作，没有提交、清理或合并其他已有改动。

## 完成范围

| 车型 | 活动部件 | Unity LOD0 / 1 / 2 三角面 | 碰撞盒宽 × 高 × 长（米） |
| --- | --- | --- | --- |
| T90 BASTION | 炮塔偏航、炮管俯仰、炮管后坐 | 83,938 / 42,983 / 22,351 | 5.500 × 2.935 × 8.036 |
| V12 LANCER | 炮塔偏航、炮管俯仰、八轮滚动、前两轴转向 | 88,749 / 48,995 / 27,929 | 4.700 × 2.897 × 5.468 |

原始融合网格先沿关节切开，再以连通部件区分炮塔与车身附件，保留逐角 UV；活动关节补齐截面，并补充炮塔座圈和炮管支承。新增关节、切边与封口使面数高于旧静态版本。每档保留相同语义的活动层级，红蓝队共用几何，分别使用已有 Base Color。T90 履带和负重轮沿用原玩法的固定外观，本轮没有增加履带物理、纹理滚动或逐节履带动画。

米制尺寸按原型游戏的车宽校准：T90 宽 5.5 米，V12 宽 4.7 米，均匀缩放以保留已接受的造型比例。不是以现实同名军车尺寸重建模型。T90 包含炮管的总长约 10.35 米，V12 约 5.78 米；含天线高度约 4.51 / 3.77 米。碰撞只覆盖车体与主要上装，不覆盖长炮管和天线。

## 文件入口

- [T90 可编辑工程](../art/vehicles/t90-bastion/articulated/t90-articulated.blend)、[三档 FBX](../art/vehicles/t90-bastion/articulated/exports/)、[构建测量](../art/vehicles/t90-bastion/articulated/build.json)。
- [V12 可编辑工程](../art/vehicles/v12-lancer/articulated/v12-articulated.blend)、[三档 FBX](../art/vehicles/v12-lancer/articulated/exports/)、[构建测量](../art/vehicles/v12-lancer/articulated/build.json)。
- [统一制作脚本](../art/vehicles/build_articulated_armor.py)、[关节关键姿态预览脚本](../art/vehicles/render_articulated_armor.py)。
- `UnityProject/Assets/Art/Vehicles/T90Bastion`、`V12Lancer`：各三份 FBX、蓝红 Base Color、法线、Metal/Rough、转换后的 Unity 金属度/光滑度图和 URP 材质。
- `UnityProject/Assets/Resources/Vehicles/T90_Blue.prefab`、`T90_Red.prefab`、`V12_Blue.prefab`、`V12_Red.prefab`。
- [本次代码差异](../art/vehicles/armor-integration-2026-10-23/task-only-code.patch)：相对于任务开始时的工作区文件，避免混入原来尚未提交的其他代码变动。

新的 Blender 工程默认显示蓝队 LOD0，隐藏 LOD1/2，红队材质也已保存；原 `*-optimized.blend`、`*-red.blend`、静态 GLB/FBX 与纹理没有覆盖。四份旧 Blender 工程的 SHA-256 均与任务开始时一致。

## Unity 接入

### 预制体和材质

`VehicleArtAssetSetup.BuildArmor()` 构建这两款；`BuildAll()` 现在包含 H8/A40/T90/V12。每款导入目录仅放三档蓝队几何，红队通过材质切换生成。LOD 阈值沿用 `.52 / .18 / .025`。

新 FBX 使用米制单位，`globalScale=1`、`useFileUnits=true`。沿用现有 FBX 源轴转换，`SourceFBX` 根 X=-90°，实际活动部件包在 Unity 轴向的 `Driven_` 空节点下。不能把 H8 的 `globalScale=100` 复制到这两款。

glTF Metal/Rough 的 G 粗糙度、B 金属度转换为 Unity R 金属度、A 光滑度。新资产法线强度设为 0.65。截面/支承使用独立 JointSteel 材质，避免误用整车贴图。

### T90

实际路径为 `PrototypeTankVehicle → PrototypeTankVisual`，没有强行改成 CombinedVehicle。初始化时加载按队伍区分的 T90 美术，保留旧逻辑炮塔、炮管、炮口和碰撞组件；`PrototypeVehicleArtRig.Bind(PrototypeTankVisual)` 同步全部 LOD 的转动和后坐位移。

枪口从 FBX 测得的真实炮口挂点校准；后坐围绕新的休止位置计算，不再在每帧恢复旧原型的固定位置。观察挂点放在炮塔前方偏左，保持实际炮管可见。车长席跟随炮塔，驾驶与后排位置也按新车体校准。显隐时关闭/开启整个美术实例，保留独立乘员机枪，并保证已替换的原型网格不会在恢复显示时重新出现。

### V12

实际路径为 `PrototypeCombinedArmsVehicle → PrototypeCombinedVehicleVisual`。`Ifv` 已加入资源加载分支，炮塔、俯仰、枪口和碰撞体使用新预制体的测量值。保留既有四席、驾驶和武器规则；独立乘员机枪缩放并贴合新车顶，驾驶观察与乘员镜头做了校准。

八轮在三个 LOD 中分别具有真实轴心，轮子按世界位移投影和测量半径滚动，转弯时各轮使用自身行驶距离；超过 2 米的单帧位移作为瞬移跳过。前两轴四个轮子的转向父节点跟随平滑后的输入，最大视觉转角 25°。轮胎保留生成模型的既有轮廓，不引入 WheelCollider 或悬挂模拟。

## 验证与证据

- [实际 SampleScene Play 结果](../art/vehicles/armor-integration-2026-10-23/play-results.txt)：76 项断言通过，涉及红蓝实际生成、材质、三档 LOD、真实部件轴心、驾驶、从真实炮口发射、T90 后坐、V12 轮子和转向、乘客权限、显隐恢复。
- [已有观瞄回归](../art/vehicles/armor-integration-2026-10-23/batch-results.txt)：实际炮管位于第一人称右下、稳定观察方向、测距、炮口俯仰、装填、切换视角和座位等通过。该测试也包含原有摩托车转向断言，未另行扩展全项目回归。
- [新资产验证汇总](../art/vehicles/armor-integration-2026-10-23/asset-validation.json)：六份 FBX 在独立 Blender 导入进程中回读，所需层级和 UV 存在，无无效顶点、退化面或缺失材质。Blender 与 Unity 对少量切口面的导入计数有差别，两套数值分别记录；不是可打印密闭网格的验收。
- [Unity LOD 与观察视图联系表](../art/vehicles/armor-integration-2026-10-23/unity-contact-sheet.png)、[图像基础检查](../art/vehicles/armor-integration-2026-10-23/image-checks.json)。图片已打开检查，无全黑、全白或缺失材质粉色帧。观察视图由真实相机位置渲染，Editor Camera.Render 不包含 OnGUI HUD。
- 关键关节图：各车型 `articulated/rest-*`、`aim-*`、`depress-*`，检查静止、45° 偏航/25° 仰角、-60° 偏航/10° 俯角；V12 还检查不同轮子滚动角度。预制体不内置这些测试关键帧，游戏中由既有逻辑驱动。
- macOS Development 构建成功，BuildReport 为 387,477,064 字节。开发版在 `UnityProject/Builds/Armor-0.55.0/IRONFRONT.app`，没有覆盖此前构建；[独立启动结果](../art/vehicles/armor-integration-2026-10-23/development-player.txt)：Apple M5 / Mac17,4、版本 0.55.0，红蓝两款及三档活动 LOD 均加载，运行时错误为 0；[真实程序画面](../art/vehicles/armor-integration-2026-10-23/development-player.png) 已检查。

## 复现

从仓库目录运行 Blender：

```sh
'/Applications/Blender.app/Contents/MacOS/Blender' --background --factory-startup \
  --python art/vehicles/build_articulated_armor.py -- t90 v12
```

将各车型 `articulated/exports` 的三份 FBX 复制到对应 Unity Art 目录，保持已有四张原纹理；在 Unity 运行 **IRONFRONT → Art → Build T90 and V12 Prefabs**。T90/V12 出生时会自动加载对应资源，不需要修改 SampleScene。

在没有其他 Editor 占用工程时，使用当前项目版本的 Unity `-batchmode -projectPath … -buildTarget StandaloneOSX -executeMethod Ironfront.UnityPrototype.Editor.ArmorArtPlaySmoke.Run -logFile …`。这是会自行退出的 Play 作业，不要加 `-quit` 或 `-nographics`。观瞄回归通过现有 `PrototypeBatchVerification.Run` 和本交付的 `sight-checks.txt` 执行。

`PrototypeArmorArtProbe` 只在 Editor 或 Development Build 编译，只有显式提供 `-ironfrontArmorProbe <输出目录>` 才创建；用于确认独立程序中的红蓝载具资源、活动 LOD 和真实帧，不改变普通启动流程。

## 保留边界与下一步

没有重新生成、重新涂装或重做九款载具，也没有运行无关的全面质量评审。此轮未做双机 LAN 操作、长期完整对局或最低配置性能测试，网络同步字段保持原有范围。T90 履带固定，独立乘员机枪保留既有原型结构；没有悬挂、履带物理或新乘员动画。

当前四款 H8/A40/T90/V12 已接入。后续可按原交接推进 R4/U8/M2，再做 R3/J20；这五款已经有红蓝静态模型，不需要用户重新生成。
