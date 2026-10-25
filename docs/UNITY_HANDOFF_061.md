# Unity 0.61.0 — 按确认稿升级大厅

2026-10-24。按用户确认的 A3「大厅／配装／设置」参考图接入真实大厅。此次只完成大厅，配装和设置页面留待后续设计；不实施先前讨论的 B 版部署页。

## 已实现

- 顶部保留大厅、配装、设置三项，删除顶部作战入口。大厅右侧「选择作战」打开原有地图与征服／攻防选择页，新增返回大厅按钮，当前模式会更新到大厅摘要。
- 右侧按顺序排列战场插画、地图名和模式信息、开始游戏、联机房间。主按钮使用琥珀色哑光表面、细微颗粒及明暗边缘；次按钮为深色面板，支持悬停、按下和键盘聚焦状态。
- 现有士兵与实际所选枪械独立实时渲染，人物居中。大厅单独使用更近的取景；其他页面恢复原相机取景。新增顶部安全间距以支持超宽屏。
- 左下角显示由当前真实士兵模型渲染的头像、可编辑玩家名字与程序绘制的等高线名片。大厅不显示兵种类别按钮或兵种摘要；兵种选择继续在原配装页进行。
- 背景、人物与交互控件分层。新机库背景只在大厅使用，原夕阳背景保留给配装和设置页。替换大厅背景不需要重做按钮或人物渲染。
- 修复打包后旧预览脚下阴影变成硬黑边的问题：使用显式透明混合的独立 URP 阴影着色器与单张渐隐平面，避免依赖被打包裁剪的运行时材质透明变体。

## 文件与素材

- `Assets/Resources/Menu/StartMenu.uxml`：保留原配装、设置子树，重新排列大厅与通用导航；作战页通过右侧入口访问。
- `Assets/Resources/Menu/StartMenuTheme.uss`：共用导航与旧页面样式。
- `Assets/Resources/Menu/LobbyTheme.uss`：独立、以 lobby-view 限定的大厅样式。
- `Assets/Scripts/PrototypeStartMenuView.cs`：现有事件接入、页面切换时的背景与按钮容器切换。
- `Assets/Scripts/PrototypeMenuPreview.cs`：大厅取景、模型头像 RenderTexture、可释放的脚底阴影。
- `Assets/Scripts/PrototypeLobbyDecoration.cs`：名片线条、地图文字渐隐底与按钮微颗粒；不拦截鼠标。
- `Assets/Resources/Menu/MenuContactShadow.shader`：明确的透明混合与径向衰减。
- `Assets/Resources/Menu/HangarBackdrop061.png`、`IndustrialFrontline061.png`：内置 imagegen 从用户确认的 A3 参考图提取并重新生成的独立背景与战场氛围插画。插画不是当前关卡实机截图；角色、枪械及控件没有烘焙进背景。
- 原素材 `HangarBackdrop053.png`、士兵、枪械、载具及旧应用均保留。生成提示词在 `art/ui/lobby-upgrade-2026-10-24/imagegen-prompts.json`。

## 验证与边界

- Unity 6000.5.10f1 / StandaloneOSX 构建成功，最终应用包含 276 个文件，合计 552,203,618 字节。
- 独立开发版专项通过，RuntimeErrors=0。实际运行 960×600、1280×800、1600×900、1920×1080、2560×1080 五种窗口尺寸，检查右侧操作对齐、名片位置、三个导航入口、真实 UI 拾取，以及渲染人物与控件不重叠。
- 实际生产回调验证：地图模式选择与返回、原配装兵种／枪械选择、原设置页、名字绑定、联机房间入口，以及单人 8v8 开局。独立程序通过原 OnGUI/Frontend 生命周期进入房间与战斗；未修改房间或战斗逻辑。
- 测试通过生产 Clickable/ClickEvent 回调及实际 panel.Pick 检查，不是操作系统键鼠录像；没有运行完整双客户端联机对局。
- 两组既有回归通过：Verify Lobby Loadout、Verify Class Loadout Policy。配装与设置 UXML 子树逐字一致，原配装和设置主体 USS 段保留，通用导航更新为三项。
- 旧配装规则测试仍按早期占位武器对象名判断，因此与既有 IF-27 美术替换不兼容。已将美术枪分支改为验证对应 View/World 预制体的实际共享网格和握持／枪口挂点，其他枪仍检查原名称；未改变武器规则。原测试与失败日志保留。
- 已查看最终 16:9、超宽屏、配装与设置实机截图；全部 10 张完整 UI 截图通过非空白／非异常洋红检查。人物透明层四角 alpha=0，保留 4,284 个部分透明像素，脚底硬黑边已消除。
- 642 个基准文件哈希保持一致：原美术资源、0.60.0 应用，以及玩家、士兵动作、受击反馈脚本。版本更新至 0.61.0。
- 原有 PrototypeWeaponArtProbe 中两项 FindObjectsSortMode 废弃 API 警告保留；本次脚本与 UI 样式未出现新增编译、解析或着色器错误。

## 交付与复现

- 应用：`UnityProject/Builds/Lobby-0.61.0/IRONFRONT.app`。
- 推荐查看实机图：`art/ui/lobby-upgrade-2026-10-24/player-02/lobby-1600x900.png`。
- 全部证据：`art/ui/lobby-upgrade-2026-10-24/`；最终独立程序记录 `player-02/results.txt`、`layout.csv`，回归 `batch-results.txt`、`regression-final.log`，构建 `build-02.log`，保留与图像检查 `preservation.json`、`visual-validation.json`。
- 开发验证入口：应用增加参数 `-ironfrontLobbyProbe <输出目录>`，会执行控件与分辨率检查、保存截图并自动退出。正常打开应用不执行验证。
- 验证源码：`Assets/Scripts/PrototypeLobbyProbe.cs`（仅编辑器与开发构建编译）。

工程继续保留在 codex/unity-port；开始时 HEAD 与本地 origin/main 引用相差 36/5，包含大量此前未提交成果。此次未自动合并、提交或推送这些历史工作。后续可在本大厅基础上设计配装和设置页，B 版部署界面仍待实施。
