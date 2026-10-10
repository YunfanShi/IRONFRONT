# 0.33.0 Unity 正式启动菜单

## 本次完成

- 首次进入游戏时使用 UI Toolkit 显示全屏大厅、作战、配装、设置四页。顶栏、内容区和底部操作栏采用固定版式，配色、标题层级及间距按用户提供的网页截图调整。战内 HUD、房间和复活页面继续使用原有流程。
- 机库与士兵改为可在 Unity Inspector 中编辑的 `MenuHangar.prefab`、`MenuOperator.prefab` 和独立材质。机库预制体包含墙板、灯光和专用摄像机；士兵预制体包含盔甲、臂章和武器挂点。选兵种后，臂章、武器预览及配装数据同步更新。
- 作战页保留工业前线的地图缩略图；赤砂走廊和攻防模式明确显示为开发中。底部玩家代号、单人游戏和联机房间入口继续接入现有逻辑。
- 用 `StartMenu.uxml` 和 `StartMenuTheme.uss` 管理菜单结构和样式。`StartMenuPanel.asset` 以 960 × 600 为参考分辨率，按屏幕宽高缩放。游戏显示版本与网页包版本同步为 `0.33.0`；房间协议仍为 `unity-3`。

## 如何修改画面

1. 打开 `UnityProject/Assets/Resources/Menu/StartMenu.uxml` 修改菜单元素，在 `StartMenuTheme.uss` 调整颜色、字体大小和位置。
2. 打开同目录的 `MenuHangar.prefab`，可直接移动墙板、物资箱、摄像机和灯光，修改 `Materials` 中的材质。`MenuOperator.prefab` 可编辑角色形体和武器挂点。
3. 编辑器菜单 `IRONFRONT/Generate Editable Menu Assets` 用于重新生成这些资产；它会覆盖相应预制体和材质，因此手工微调后不要再次执行。菜单模型只用于预览，不会改变战场角色。
4. 在 `Assets/Scenes/SampleScene.unity` 按 Play，使用顶部页签或 F1–F4 检查四页；数字键 1–4 选兵种，Enter 出击，L 打开联机房间。

## 验证

- Unity 6000.5.10f1 编辑器中逐页查看大厅、作战、配装、设置；底部栏未遮挡卡片内容，Console 为 0 错误、0 警告。
- 从配装页选医疗兵，角色说明、默认武器与数值同步变化；按 Enter 进入单人战局，战内武器为 VX-9。
- 编辑器菜单 `IRONFRONT/Build macOS Standalone` 构建成功；本地 `UnityProject/Builds/macOS/IRONFRONT.app` 的版本为 `0.33.0`。TypeScript 的 `tsc --noEmit` 和 `tsc -p tsconfig.logic.json --noEmit` 均通过。

## 当前边界

此版以桌面横屏为目标；极窄窗口和竖屏尚未单独设计。字体使用 Unity 当前中文回退字体，尚未打包独立授权字体。赤砂走廊与攻防模式仍是概念展示。

开发提交保留在 `codex/unity-port`，不合并 `main`。
