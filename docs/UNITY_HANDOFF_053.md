# IRONFRONT Unity 大厅与兵种配装交接（0.53.0）

2026-10-13，基于本地 0.52.1。继续在 `codex/unity-port` 保留此前未提交工作，不重置 0.47–0.52.1 的功能或资产。本轮开始只读核验分支 HEAD 与远端仍为 `c166d59`。上一轮 AI 反载具逻辑见 [0.52.1 交接](UNITY_HANDOFF_0521.md)。

## 兵种武器规则

配装使用统一的兵种许可策略，而非仅在菜单中隐藏按钮：

| 兵种 | 可选主武器 | 默认主武器 | 副武器 |
| --- | --- | --- | --- |
| 突击兵 | IF-27 卡宾枪、BR-44 战斗步枪 | IF-27 | P8 手枪 |
| 医疗兵 | VX-9 冲锋枪、IF-27 卡宾枪 | VX-9 | P8 手枪 |
| 侦察兵 | M89 精确射手步枪、S12 狙击步枪 | M89 | P8 手枪 |
| 工程兵 | BR-44 战斗步枪、H60 轻机枪 | BR-44 | P8 手枪 |

更换兵种时保留仍合法的主武器，否则换回该兵种默认枪；副武器统一手枪。旧存档中跨兵种的主武器或精确射手副武器会在读取时修正，损坏 JSON 和非法枚举回到有效默认值。Load、Save、玩家 ApplyLoadout 及实际部署共享同一 Validate 规则。枪械伤害、弹道、装甲、弹匣和兵种装备性能不因菜单改版改变。

## 参考图与素材

用户两张图作为大厅构图、黑金视觉和配装层级的参考：顶部大厅/作战/配装导航与右上设置；大厅保留角色展示和开始游戏操作；配装展示竖排兵种和主副武器、兵种工具、投掷物。

使用内置 imagegen 生成一张无人物、无文字、无 UI 的夕阳机库背景。素材已复制进工程：`Assets/Resources/Menu/HangarBackdrop053.png`。完整提示词、生成方式及用途见 [美术素材清单](UNITY_MENU_ART_MANIFEST_053.json)。背景不包含假按钮、货币或配装结果，角色与枪械展示由实际 Unity 模型和选择数据驱动。

## 大厅与配装操作

顶部提供大厅、作战、配装三个页面，设置在右上角。大厅展示当前兵种、实际持枪模型、玩家代号和已选作战模式，右下角开始游戏；代号可在左下方编辑并保存。作战页保留工业前线地图、征服/攻防和实际 8v8 人数组合。

配装页左侧是四兵种竖排列表，下面是主武器、副武器、兵种工具和投掷物卡片。点击主武器显示当前兵种的两把许可枪械，点击副武器显示 P8 手枪，点击投掷物可选择破片弹或烟雾弹。选枪同步更新卡片名称、真实枪械缩略图、伤害/射速/弹匣/射程和角色持枪。查看副武器时右侧明确显示“展示副武器”；返回大厅恢复展示所选主武器。兵种工具显示现有能力和操作键，不新增未实现的装备或附件。

画面分为环境背景与透明的真实 3D 角色预览两层。角色使用现有 `PrototypeSoldierVisual` 模型和双手持枪姿势，配装展示位置偏右；角色层等比例适配，背景铺满并裁切。七种枪械缩略图由现有枪械几何体单独渲染和缓存。预览独立使用灯光、材质副本和渲染层，关闭菜单时释放自身资源。本轮未把参考图中写实人物作为游戏模型，也未引入新的人物网格或骨骼资产。

设置继续提供主音量、背景音乐、鼠标灵敏度和准星大小。音量与准星显示百分比，灵敏度维持原来的 0.5–5.0 数值范围。

## 联机范围

新配装规则应用于单人玩家和联机房主本机玩家。现有访客部署协议仅传部署类型、地点和编号，远端玩家继续使用既有突击兵/卡宾枪配置；本轮没有增加访客配装同步或修改 LAN 协议。访客部署请求不能通过额外的主副武器字段绕过房主验证。这一边界已在策略检查中核验，不能将菜单本机选择等同于远端已同步配装。

## 实现与验证入口

- 许可策略：`PrototypeInfantryRoles.cs`、`PrototypeLoadout.cs`，以及玩家默认副武器与换兵种逻辑。
- 界面：`Assets/Resources/Menu/StartMenu.uxml`、`StartMenuTheme.uss`、`PrototypeStartMenuView.cs`；`PrototypeFrontend.cs` 保留回退界面的规则一致性。
- 模型预览：`PrototypeMenuPreview.cs`、新增 `PrototypeWeaponMenuPreview.cs`。
- 策略运行检查：`IRONFRONT/Verify Class Loadout Policy`。覆盖四兵种矩阵、损坏及旧存档、实际玩家库存/模型、换兵种、部署和现有联机权限边界。
- 实际界面检查：`IRONFRONT/Verify Lobby Loadout` 与 `IRONFRONT/Capture Lobby and Loadout`。通过真正 UI Toolkit 回调进行选兵种、选枪、选烟雾弹、调设置和请求开始；检查真实布局、点击拾取和模型画面，再输出实际渲染截图。

## 本轮验证记录

使用本机 Unity 6000.5.10f1 的真实 Play 模式批处理执行 9 个菜单检查，最终全部 PASS，进程退出码 0：兵种配装策略、实际大厅/配装回调及布局、实际界面截图、准星设置、AI 反载具、手持装备、弹药补给、玩家/载具视角和工程兵防空。

界面检查在 960×600、1280×720、1280×800 三种分辨率下实际渲染大厅、四兵种主武器、P8 副武器、设置和作战，共 24 张 PNG。主智能体检查主要页面，预览子智能体逐张复看最终截图。完整头脚、透明背景叠层、真实枪械缩略图、各卡片层级与开始按钮显示正常。首轮 960×600 的配装开始按钮曾与右脚少量重叠，实际截图定位后将该页按钮宽度调整为 124、右边距 12，最终三分辨率复测通过；旧失败日志与截图单独保留。

日志与截图保存于 `UnityProject/Builds/Verification053/`（构建输出目录不入 Git）：

- `checks.log`、`batch-results.txt`：最终 9 项实际 Play 检查结果。
- `lobby-1280x800.png`、`loadout-assault-1280x800.png` 等：最终实际 UI Toolkit 渲染结果。
- `checks-first-layout.log`、`batch-results-first-layout.txt`、`first-layout-failure.png`：修复前的布局失败证据。

以上为实际引擎和 UI 回调/渲染自动化检查，不等同于人工鼠标完整实战验收。检查使用独立临时界面、玩家和预览对象，并恢复配装、模式、音量、灵敏度、准星及光标偏好。

网页端 `tsc --noEmit` 与 Vite production build 均通过；Vite 保留原有 Three.js 分块大于 500 kB 的提示。源文件差异检查、UXML/JSON 解析及资源 `.meta` 完整性/GUID 唯一性检查通过。

## macOS 交付

通过 `PrototypeBuild.BuildMacOSVersioned` 完成实际构建，Unity BuildReport 成功、退出码 0，报告大小 127,593,571 字节。程序位于 `UnityProject/Builds/macOS-0.53.0/IRONFRONT.app`，已核验实际 `Info.plist` 的版本为 0.53.0。

独立程序自动启动运行 15 秒，确认进程持续存活、引擎与 Metal 图形初始化、启动日志无 NullReference/MissingReference/InvalidOperation 等异常。验证只终止本次创建的程序进程，不关闭用户既有应用。日志见 `Builds/Verification053/build.log`、`player-startup.log`、`startup-validation.json` 和 `validation.txt`。未将启动检查表述为人工完整战局测试。

本轮完成时分支仍为 `codex/unity-port`，HEAD 为 `c166d59`。本轮源码、素材及此前本地工作均保留，未创建提交、未推送 GitHub、未合并 main。
