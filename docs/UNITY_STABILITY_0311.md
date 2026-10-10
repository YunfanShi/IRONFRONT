# 0.31.1 Unity 重连状态与载具接管修复

## 目标与基线

本轮从 `codex/unity-port` 的 `a370017`（0.31.0）继续；开始时本地与 GitHub 开发分支一致。工作区原有 `ProjectAuditorSettings.asset` 和 `ShaderGraphSettings.asset` 的本机改动不属于本轮提交。`main` 继续单独维护网页版，Unity 开发不推入 `main`。

Unity 房间服务已经会在访客异常断线后预留同一 ID、名字和阵营 30 秒，但房主端原先在名单中看到 `connected=false` 就删除其战场实体；重连后重新生成，位置、生命和弹药被重置。另一处不对称是红方 R4 与 U8 被固定标为 AI 车辆，红队房主无法接管己方车辆。本轮修复这两个可玩性与公平性问题，不新增车型或多人载具座位。

## 实现

- 房主对仍在房间名单中的暂离访客保留同一 `PrototypeRemotePlayer`。断开期间禁用其输入、部署、救援请求和持续开火；房间预留消失、玩家被踢或阵营不匹配时才移除实体。恢复同一 ID 后继续使用原实例及其战场状态，而不是重新出生。
- Unity 客户端在服务端 30 秒预留期内继续尝试恢复，连接失败后把 HUD 从 `RECONNECTING` 更新为 `SESSION ENDED`，并显示明确提示。断线期间房主仍保有战斗权威；普通玩家无法发送房主快照或房间管理操作。
- R4/U8 的本机玩家占座优先于原 AI 控制。下车时清除过时路线和卡路计时，从当前车位重新规划。阵营、存活、席位与网络副本检查仍生效；联机访客仍不能驾驶车辆。
- 按仓库修复版本规则升为 `0.31.1`。Unity 房间消息格式没有变化，协议保持 `unity-3`；网页客户端继续接受同一 `0.31.x` 的房主服务。

## 验证记录

- Unity 6000.5.10f1 编辑器脚本编译通过；Play 模式执行 `IRONFRONT/Verify Reconnect and Red Vehicles`，日志为 `IRONFRONT stability smoke PASS`。验证同一远端角色在断线、恢复期间保留引用、生命值与位置，断线输入被拒绝；红方可接管 R4 驾驶位及 U8 驾驶/炮手位。
- `scripts/test-unity-room.cjs` 通过：覆盖访客断线名单保留、同一身份恢复及恢复后仍无房主权限，并复测快照、移动、部署、濒死动作和踢人等限制。
- `scripts/test-lan.cjs` 通过：两名真实 WebSocket 客户端的席位恢复、房主权限、阵营和容量回归。
- TypeScript `tsc --noEmit` 通过。
- macOS 独立构建成功，`IRONFRONT.app` 的 `CFBundleShortVersionString` 为 `0.31.1`；`IRONFRONT-Unity-Stability-0.31.1-macOS.zip` 经 `unzip -tq` 完整性检查通过。构建物位于本机 `UnityProject/Builds/macOS/`，由仓库忽略规则排除，不随代码提交。

## 当前限制

本轮只保证同一服务进程、预留期内的同一席位恢复；服务重启后房间与战场状态仍会丢失。暂离角色仍留在房主模拟的战场里，可能受伤或进入濒死，恢复时应看到房主裁定的最新状态。U8 后排仍只支持 AI 乘员，访客不能占用任何载具座位。两台物理计算机的长期实战和弱网延迟评估留待后续；本轮完成了协议级双客户端回归和 Unity Play 模式验证。
