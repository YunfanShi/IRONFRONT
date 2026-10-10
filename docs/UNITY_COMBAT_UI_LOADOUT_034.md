# 0.34.0 Unity 战斗界面与配装

## 本次完成

- Esc 打开战斗暂停界面。单人模式冻结战局，显示连接信息、鼠标灵敏度、主音量、继续战斗及返回大厅；联机房间打开菜单时仅暂停本地输入，房间战局继续。可点击返回大厅，或在暂停界面按 `L`；返回大厅会关闭当前房间连接并重新加载起始场景。
- 战斗画面右下角增加生命值条、当前武器、弹匣剩余弹药、备用弹药及 AUTO/SEMI 模式。下方快捷栏用图标与键位显示主武器 `1`、副武器 `2`、兵种装备 `X`、投掷物 `G`；工程兵额外显示反装甲火箭 `Z`。显示值来自角色和武器实时状态。
- 大厅配装页加入四个装备位、七把枪的图标与选项、破片手雷及烟雾弹选项、武器属性。主武器可选六种长枪，副武器可选 M89 或 P8；主武器为 M89 时副武器限定为 P8，规则对齐 `main` 的 `src/combat/Weapons.ts`。更换兵种、枪械、投掷物后立即写入本机设置，出击与复活沿用当前配装。玩家代号保留原有保存逻辑。
- 主副武器有独立弹匣、备弹及装填状态。`1/2` 切换时第一人称枪械模型同步变化；`R` 装填当前武器。`G` 投掷所选类型，破片手雷造成范围伤害，烟雾弹产生临时烟幕。兵种装备按 `main` 对齐：突击兵补充当前武器备弹，医疗兵治疗，侦察兵放置一次性复活信标，工程兵修理己方载具并有两发反装甲火箭。
- 起始菜单设置页也加入鼠标灵敏度滑块，与暂停菜单使用同一个本机设置。版本更新为 `0.34.0`。

## 主要实现位置

| 文件 | 内容 |
| --- | --- |
| `UnityProject/Assets/Scripts/PrototypeLoadout.cs` | 配装数据、约束与本机保存 |
| `UnityProject/Assets/Scripts/PrototypeStartMenuView.cs` | 配装及设置交互、预览与武器数值 |
| `UnityProject/Assets/Resources/Menu/StartMenu.uxml`、`StartMenuTheme.uss` | 可编辑的配装布局和样式 |
| `UnityProject/Assets/Scripts/PrototypeEquipmentIcons.cs` | 装备图标纹理 |
| `UnityProject/Assets/Scripts/PrototypePlayer.cs`、`PrototypeInfantryRoles.cs` | 切枪、弹药、投掷物、兵种能力 |
| `UnityProject/Assets/Scripts/PrototypeRuntime.cs`、`PrototypeFrontend.cs` | 战斗 HUD、Esc 菜单、返回大厅 |
| `UnityProject/Assets/Scripts/PrototypeDeployment.cs` | 侦察兵信标部署点 |

## 操作与验证方法

1. 用 Unity 6000.5.10f1 打开 `UnityProject`，运行 `Assets/Scenes/SampleScene.unity`。从菜单进入「配装」，选工程兵、BR-44、P8 和烟雾弹；确认选择边框、装备名称、角色预览及数值变化。
2. 点击「单人游戏」。检查右下 HUD 的 HP、弹匣、备弹和 AUTO/SEMI；按 `2` 应切换到 P8，开火后弹匣减少，按 `R` 装填，按 `1` 返回 BR-44。按 `G` 投掷烟雾弹，数量减少；工程兵按 `Z` 使用反装甲火箭，按 `X` 在受损己方载具附近修理。
3. 按 Esc，检查鼠标释放、灵敏度与音量滑块；单人战局应停止，继续后重新锁定鼠标。点击返回大厅应显示初始界面，重新打开配装可看到保存的选择。
4. 将兵种改为侦察兵，出击后按 `X` 放置信标。阵亡并结束濒死状态后，部署列表应出现「侦察兵复活信标」；从信标部署后该点消失。

## 本轮实测

- Unity 6000.5.10f1 编辑器中启动场景，确认菜单与配装页可显示；调整配装页布局后重新导入，武器选项与属性区不再相互遮盖。Console 为 0 错误、0 警告。
- 单人战局中按 `2`，武器、独立弹药与 `SEMI` 输出模式同步切换；右下角生命条、弹匣/备弹和装备快捷栏可见。
- 按 Esc 显示暂停菜单；首次实测返回大厅发现场景重新载入后游戏入口没有重建，已改为监听场景加载。修复后从战斗按 Esc、`L` 返回大厅，确认初始界面重新出现。
- 编辑器菜单 `IRONFRONT → Build macOS Standalone` 构建成功，输出 `UnityProject/Builds/macOS/IRONFRONT.app`，包版本 `0.34.0`。仓库 TypeScript 主配置与逻辑配置 `tsc --noEmit` 均通过。

## 当前边界

本轮以单人战局为主要验证目标。联机房间仍由既有房主权威逻辑控制；本机新增的配装、投掷物、信标及火箭尚未作为完整装备状态通过房间协议同步，因此联机实测仍需单独迭代。烟幕目前提供视觉遮挡，尚未加入 AI 视线阻挡；医疗兵当前可治疗自己或附近活着的友军，完整的玩家间濒死救援尚未移植。

开发提交保留在 `codex/unity-port`，不合并 `main`。
