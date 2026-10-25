# Unity 0.62.0 — B 版兵种总览配装

2026-10-25。用户确认「B-战地式兵种总览.png」作为配装界面的样式。本次将现有 UI Toolkit 配装页落地为横向兵种总览，并接通装备选择、保存和返回大厅；现有人物、枪械、载具及其他工作保留。

## 界面与操作

- 顶部继续为大厅／配装／设置；配装页上方横排突击兵、医疗兵、侦察兵、工程兵。使用可缩放矢量兵种图标，避免系统 Emoji 或缺字符号。
- 左侧使用内置 Heavy 中文标题、兵种定位、专属装备及适用武器说明。正文 Regular、卡片与兵种名 Medium，继承 0.61.1 的军事英文标志。
- 真实士兵放大至中右区域，人物预览相机单独调整配装构图。背景仍为可独立更换的夕阳机库资源；大厅原居中全身构图、背景与名片保持。
- 底部四张半透明深色卡片：主武器、副武器、兵种装备、投掷物。主武器卡显示真实定义中的伤害、射速、弹匣及有效射程。查看副武器不会把主武器卡的属性改成手枪属性。
- 点击卡片打开同风格选择面板。主武器只列出所属兵种允许的两种武器；副武器为 P8；投掷物可选破片手雷、烟雾弹；专属装备显示现有实际操作说明。
- 「完成选择」回到总览，「完成配装」保存并返回大厅。Esc 先关闭选择面板，再返回大厅；配装内的键盘事件与全局开局快捷键隔离，避免回车误开战局。弹窗期间底部卡片、兵种栏和完成按钮禁用，顶栏仍允许离开当前页面。
- 选择继续即时保存到原存档，不新增独立配装存储或改装配件系统。进入战局沿用原兵种许可、库存和部署校验。
- 五种窗口尺寸适配；超宽屏的逻辑高度更小，单独压缩左侧标题和说明，避免被装备卡遮挡。

## 现有模型的边界

本次实现参考图的排版、字重、配色与交互结构。人物和武器均为工程里的真实模型，不将概念图人物或武器截图当作运行模型。枪械缩略图提高到 640×304，并新增实际投掷物与兵种装备的透明渲染缩略图。P8、其他尚未替换的枪械和部分装备仍是既有简模，细节自然与概念图有差距；这次未重建它们。第一人称枪托、动画、移动、受击、载具和设置主体均未修改。

## 文件

- `Assets/Resources/Menu/StartMenu.uxml`：替换配装页子树，保留大厅、地图模式、设置、名片和原部署按钮子树。
- `Assets/Resources/Menu/LoadoutTheme.uss`：配装页独立样式，包括普通、紧凑和超宽屏布局。
- `Assets/Scripts/PrototypeStartMenuView.cs`：新总览数据绑定、选择面板、完成返回及键盘事件隔离，复用原存档和兵种规则。
- `Assets/Scripts/PrototypeLoadoutSymbol.cs`：Painter2D 兵种标志及左侧装备符号。
- `Assets/Scripts/PrototypeMenuPreview.cs`：仅配装相机的近景构图，新增装备缩略图入口。
- `Assets/Scripts/PrototypeWeaponMenuPreview.cs`：高清真实枪械缩略图及兵种装备、投掷物缩略图缓存；退出菜单释放资源。
- `Assets/Scripts/PrototypeLobbyProbe.cs`：扩展真实独立程序验证，覆盖五种尺寸、字体、点选区域、说明和卡片不遮挡、四兵种及装备选择、保存返回、大厅、设置、LAN 入口和单人开局。
- `Assets/Editor/PrototypeLobbyLoadoutSmoke.cs`：更新旧菜单回归的构图和选择面板布局要求。

## 验证与保留

- Unity 6000.5.10f1 / StandaloneOSX 开发构建成功：`build-03.log`。
- 独立应用验证 `player-03/results.txt` 为 PASS，Version=0.62.0，RuntimeErrors=0。真实窗口尺寸为 960×600、1280×800、1600×900、1920×1080、2560×1080。
- 检查实际解析字库、源字形、文字宽度、控件边界、`panel.Pick` 点选及真实角色像素轮廓；查看了普通窗口、超宽屏、选择面板和不同兵种的实机截图。测试使用实际生产回调，非系统键鼠录像；Esc/Enter 的返回逻辑通过生产处理器验证。
- 原菜单回归与兵种许可回归均 PASS：`batch-results.txt`、`editor-checks-01.log`。包含非法／旧存档修正、主副武器库存、切换兵种、部署与现有 LAN 请求边界。本次未做双客户端联机对局。
- 小窗口说明间距和超宽屏遮挡在前两次实机检查中发现并修正，原始证据保留在 `player-01` / `player-02`；最终证据使用 `player-03`。
- 406 个基准文件（含原 0.61.1 应用、字体、原菜单样式与指定玩法脚本）哈希未变。大厅／设置／地图模式／名片／部署按钮 UXML 子树未变；ProjectSettings 相对本次基准仅变更 bundleVersion。记录见 `validation.json`、`preserved-sha256.json`。
- 原 `PrototypeWeaponArtProbe` 两项废弃 API 编译警告保留；新样式的兼容警告已修正，最终独立程序日志无新增异常。

## 交付与复现

- 新应用：`UnityProject/Builds/Loadout-0.62.0/IRONFRONT.app`。打开后点击顶部「配装」。
- 总览实机图：`art/ui/loadout-upgrade-2026-10-25/player-03/loadout-1600x900.png`。
- 其他兵种：同目录 `loadout-medic.png`、`loadout-recon.png`、`loadout-engineer.png`。
- 选择面板：同目录 `picker-assault.png`、`picker-throwable.png`；超宽图 `loadout-2560x1080.png`。
- 用户确认稿副本：`art/ui/loadout-upgrade-2026-10-25/approved-reference.png`。
- 本次源码修改前备份：`art/ui/loadout-upgrade-2026-10-25/baseline/`。0.61.1 应用及更早版本仍在原 Build 目录。
- 复现独立程序检查：给新应用添加参数 `-ironfrontLobbyProbe <输出目录>`；完成后恢复测试前存档、玩家名及模式并退出。不可与其他写 PlayerPrefs 的检查并行。
- 编辑器回归：`PrototypeBatchVerification.Run -ironfrontChecks <art/ui/loadout-upgrade-2026-10-25/checks.txt 的绝对路径>`，不加 `-quit`，由协调器结束进程。

版本号已在 package.json、package-lock.json 与 Unity bundleVersion 中统一为 0.62.0。工程保留在既有 codex/unity-port；开始时 HEAD 与本地 origin/main 引用相差 36/5，存在大量此前未提交成果。本次没有自动合并、提交或推送这些历史修改。
