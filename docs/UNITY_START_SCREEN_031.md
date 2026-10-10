# 0.31.0 Unity 全屏启动页与玩家代号

## 目标与范围

用户希望 Unity 游戏进入时有独立的全屏初始界面，包含开始游戏、玩家名字等基本入口。本轮设计不照搬网页 `main` 菜单，而是采用深色战术终端视觉：左侧显示标题、战区说明与五据点线路图；右侧集中放玩家代号、单人兵种和两个主操作。界面背景与分栏依据当前游戏画布宽高绘制，覆盖整个 Game View 或独立程序窗口。

## 操作流程

1. 打开 `UnityProject/Assets/Scenes/SampleScene.unity` 并按 Play，先看到启动页。首次代号默认为 `Player`；可以点击输入框修改，最长 20 字符。空白输入会恢复为 `Player`。
2. 单人模式可选择突击兵、医疗兵、侦察兵或工程兵，点击“开始游戏 / SOLO”进入战场。数字键 `1` 至 `4` 仍可选职业，输入代号时不会触发这些快捷键；在代号框中按回车仅结束输入。
3. 点击“联机房间 / CREATE OR JOIN”进入现有 Unity 房间流程。联机仍暂时统一使用突击兵。创建房间的房主运行 `npm run lan`，其他玩家填写房主地址和六位房间码。玩家代号出现在房间名单中，供队友辨认。
4. 玩家代号通过 Unity `PlayerPrefs` 留在本机，下次启动直接带入；进入战斗后显示在左上角 HUD。该设置只是显示信息，不是登录身份，也不影响战斗规则。

## 联机数据与权限

- Unity 客户端在首次 WebSocket 连接时发送 URL 编码的代号。Node 房间服务去掉控制字符、限制为 20 个 Unicode 字符，空值回退为 `Player`，然后从服务端房间成员对象产生名单。重连沿用预留会话中的原代号，不接受新查询参数改变该成员。
- 客户端不提交 `host` 或玩家 ID 作为名字的一部分；服务器仍独立核验房主令牌并分配 ID、阵营与 `host` 标记。普通玩家即使把自己命名为 `HOST`，也无法更改设置、移动他人阵营、踢人、开始战局或发布战场快照。
- 房间消息增加 `name` 字段，Unity 房间协议从 `unity-2` 升至 `unity-3`。Unity 客户端在创建或加入时拒绝旧协议的服务。升级后必须停止旧 `npm run lan` 进程并重新运行，房主和访客也应同步更新工程或独立程序。
- 网页包、房主状态接口、网页客户端版本门槛与 Unity 应用版本一同升级为 `0.31.0`。网页房间玩法没有添加代号流程，也没有接入 Unity 房间协议。

## 验证

- `scripts/test-unity-room.cjs`：房间协议版本、代号清理与名单展示、房主重连后保留名字、普通玩家权限隔离和原有快照/部署回归通过。
- `node_modules/typescript/bin/tsc --noEmit`：网页 TypeScript 静态检查通过。
- Unity 6000.5.10f1 编辑器打开 `SampleScene` 后 Play：启动页覆盖整个 Game View，输入 `Vanguard`、按两次回车进入单人战斗，HUD 显示 `IRONFRONT | Vanguard`；退出并再次 Play，代号仍被本机记住。最终界面的 Console 为 0 错误、0 警告。
- 在启动页按 `L` 能进入现有 Unity LAN 房间界面；创建和加入由房间服务处理，未在本机代替其他玩家完成跨设备人工验收。
- 使用编辑器菜单 `IRONFRONT/Build macOS Standalone` 构建成功，应用 `Info.plist` 版本为 0.31.0；构建日志报告 `Build Finished, Result: Success`。
- 本地归档 `UnityProject/Builds/macOS/IRONFRONT-Unity-Start-Screen-0.31.0-macOS.zip` 经 `unzip -tq` 完整性验证，约 44 MB。构建产物保留在本机，不纳入 Git。

## 当前限制

启动页使用 Unity IMGUI 与工程现有 HUD 一致；字号和布局针对桌面窗口优化，极窄纵向画面不在本轮目标内。代号只用于显示，不提供账户、跨设备同步或唯一性保证。局域网模式仍需房主运行 Node 服务并提供可达地址；现有访客坦克驾驶、多人座位与职业技能限制不因本次 UI 改动而消失。

开发与提交保留在 `codex/unity-port`，不合并 `main`。
