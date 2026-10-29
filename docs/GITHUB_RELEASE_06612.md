# GitHub 工程与游戏包同步：0.66.12

日期：2026-10-29。用户指定将工程更新与可直接玩的游戏包同步到 `YunfanShi/IRONFRONT`，对应分支为 `codex/unity-port`。

## 工程同步

- 将该分支从 `413cf0ec1b58662be77746734d47946f58918c52` 更新至 `293e3ce507d35597565369af4c3794c86abb2b2d`，共 14 个已有提交，包含装备、武器、滑铲、手臂与动作更新。
- 注解标签 `unity-v0.66.12` 固定指向 `293e3ce507d35597565369af4c3794c86abb2b2d`。后续 README 和本记录的提交只补充下载入口，不改变此游戏包对应的源码。
- 按用户指定使用 `codex/unity-port`，未合并或更新 `main`。保留工作区原有五项装备参考文件删除，未纳入本次提交。
- 本次为已有版本的同步和分发，不修改游戏逻辑或递增游戏版本号。

## 下载与运行

- [GitHub Release：unity-v0.66.12](https://github.com/YunfanShi/IRONFRONT/releases/tag/unity-v0.66.12)，标记为开发预发布版。
- [IRONFRONT-0.66.12-macOS-universal.zip](https://github.com/YunfanShi/IRONFRONT/releases/download/unity-v0.66.12/IRONFRONT-0.66.12-macOS-universal.zip)，大小 `413905371` 字节，约 414 MB。
- 同时附带 `README-macOS.txt` 与 `SHA256SUMS.txt`。
- 支持 macOS 12 及以上，包含 `arm64` 与 `x86_64`，适用于 Apple 芯片及 Intel Mac。解压后打开 `IRONFRONT.app`，无需安装 Unity 或 Node.js。此包不能在 Windows 运行。
- 应用只有临时签名，未使用 Apple Developer ID 签名或进行公证。首次打开可能触发 macOS 的安全拦截；确认来源可信后，按系统提示允许打开该应用，不关闭系统整体安全保护。
- GitHub 自动生成的 Source code 归档与分支 Download ZIP 均为工程源码，不是可运行游戏包。

ZIP 的 SHA-256：

```text
18d0825f3655191e11bb3cb26287d5b6e6203f5c6f868e0ff08328ca07101ce3
```

## 构建来源与打包

- 使用现有 `UnityProject/Builds/MotionUpdate-2026-10-28/IRONFRONT.app`，没有重新构建或替换原应用。动作整合范围见 [2026-10-28 接入记录](UNITY_HANDOFF_MOTION_UPDATE_2026-10-28.md)。
- 原构建 provenance 记录基于 `bf3a8d1` 且含未提交改动；这些动作改动随后提交为 `293e3ce`。应用仍显示 0.66.12，应结合构建日期与路径区分此前同版本应用。
- 发布暂存目录为 `UnityProject/Builds/GitHubRelease-0.66.12/`，在 Git 忽略范围内；游戏包通过 Release 分发。
- 原应用包根目录的 `unity-build.provenance.json` 导致签名检查报 `unsealed contents present in the bundle root`。仅在发布副本中将该记录移至应用外，并重做 ad-hoc 签名；原应用不变。
- 使用 `ditto -c -k --sequesterRsrc --keepParent` 创建 ZIP，保留应用目录和执行权限。

## 验证与边界

- 发布副本通过 `codesign --verify --deep --strict`；此结果仅验证临时签名结构，不代表 Apple 公证通过。
- 252 个游戏资源文件与原构建一致；ZIP 内 278 个文件与发布副本逐文件匹配，CRC 检查通过，可执行文件权限为 `0755`。
- GitHub Release 已公开发布（`draft=false`、`prerelease=true`），三个附件均为 `uploaded`；远端 ZIP 大小与 SHA-256 均与本地校验结果一致。远端标签解析到 `293e3ce`，与游戏源码提交一致。
- 现有构建日志记录 `IRONFRONT optimization build PASS`；此前实际开发播放器的 P8 与 IF-27 动作检查为 `PASS`、`RuntimeErrors=0`，详见动作接入记录。
- 本次没有重新进行完整玩法、全部视觉角度、联网或其他电脑的下载安装实测。此版本仍是开发试玩版。
