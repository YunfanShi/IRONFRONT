# Unity 0.63.1 GitHub 同步记录

2026-10-26。将本机已完成的 0.47.0–0.63.1 累积成果整理为可检出的 Unity 工程，同步到现有 `codex/unity-port` 分支。此次只整理版本控制范围和文档，不修改游戏逻辑，游戏版本保持 0.63.1。

## 基线与分支

同步前，本机 HEAD 与 GitHub 的 `codex/unity-port` 均为 `c166d59b37338e1d341b98d0b7153aa52a2af54d`（0.46.0）。远端 `main` 为 `1afb5fae0be9dc5770778d952b65d43875064ab9`，继续维护网页 0.21.0。

历史提交 `34637fd` 已专门将 Unity 工程从 main 移到 `codex/unity-port`，后续 Unity 交接也持续使用此分支。本次沿用该分工，不将两个产品版本相互覆盖，不进行强制推送。根目录 ForAgent.md 的通用 main 规则早于这次产品分支拆分。

## 同步内容

- 当前 `UnityProject/Assets`、Packages、ProjectSettings，包括 C# 源码、Editor 工具与既有烟测、真实角色及九款载具、IF-27、材质/贴图、预制体、动作采样、字体及许可、原创音乐和音效、菜单样式与背景。
- 0.47–0.63.1 的 README、CHANGELOG、PROGRESS、交接文档、音频清单与乐谱，以及素材生成脚本。
- Unity 房间服务器及对应回归脚本的现有更改。
- art 下的制作脚本、来源、许可、参数和文本验证记录。既有 `Build Runtime Soldiers` 工具依赖的两套 rigged-v02 FBX 一并保留。

当前运行工程的模型和贴图在 UnityProject/Assets 中完整提交，普通 Git 即可下载，无需 Git LFS。此次新增文件没有超过 25 MiB 的单个文件。

## 本机保留内容

`UnityProject/Builds` 中的 .app、Unity Library/Temp/Logs 等缓存继续遵循现有忽略规则。art 新增独立忽略规则，大体积 Blender/GLB 工作原件、重复压缩包与导出、参考图、录屏、逐帧图片和历史备份留在本机。没有删除这些文件；本次同步不是完整美术工作档或应用发布包备份。详见 `art/README.md`。

网页使用的用户 MP3 仍遵循原有忽略规则。Unity 三首音乐是现有制作脚本生成的原创 WAV，与网页用户 MP3 分开管理。

## 本次重新验证

- TypeScript 类型检查通过。
- Vitest：13 个测试文件、117 项测试全部通过。
- 核心逻辑回归通过，包括 8/16/32/64 人规模与默认完整战局。
- LAN 回归通过：双客户端、权限、事件、重连、房主交接、AI 接管、载具与八人容量。
- Unity 房间协议回归通过：房主权限、快照/积分榜边界、移动/部署/濒死动作、速率限制、重连及退出。
- Vite 生产构建通过；仍有 Three.js chunk 超过 500 kB 的体积提示。
- 检查新增文本中的常见凭据模式和带签名访问 URL，未发现匹配；检查新增文件尺寸与提交范围。
- Unity YAML 保留编辑器原始序列化格式，差异检查忽略行尾空格与既有许可证/验证文本的文末空行。

本次没有重跑 Unity 构建或完整实机验收。沿用 2026-10-25 的 0.63.1 已有记录：构建、pose 与 actualUI 均 PASS，RuntimeErrors=0，710 个受保护文件无变更。文本证据在 `art/characters/lobby-pose-tune-2026-10-25/`，完整范围见 `docs/UNITY_HANDOFF_0631.md`。

## 检出与构建

检出 `codex/unity-port` 后，用 Unity 6000.5.10f1 打开 `UnityProject`，等待资源导入和编译。使用现有 `IRONFRONT/Build macOS Standalone` 菜单构建；运行工程无需重新执行 art 中的制作流程。制作脚本可能引用本机保留的源素材，重新制作美术工作原件需要单独取得对应素材。
