# IRONFRONT 0.21.0 Windows 服务器与 Mac 联机部署

## 目标与版本基线

将 GitHub YunfanShi/IRONFRONT 拉取到本机 IRONFRONT 目录，保留 main 的完整历史，恢复用户提供的完整 ZIP 中的音乐，并提供 Windows 专用服务器与 Mac 房主边玩边共享两种启动方式。两种方式共用既有权威战局，不复制或重写游戏规则。

开始时本机目标目录为空。远端 main 为 05cea567f0ac909d6f67171a6d1f0d0cb5d06789，版本 0.20.0；clone 后本机 HEAD 与 origin/main 一致。ZIP 中 166 个文本文件在统一 CRLF 与 LF 后与仓库一致，120 个文件原始 SHA256 一致；新增资源为 11 首音乐与 ZIP 发布清单。只恢复 public/audio/music 中的 11 个 MP3，生产资源通过本机构建生成，避免覆盖远端源码和提交旧 dist。

新增跨平台部署入口按新功能规则升至 0.21.0。package.json 是当前版本来源，锁文件、网页版本显示和服务状态同步更新；网页接受同一主次版本的服务，拒绝旧的 0.20.x 服务。历史版本报告与历史 QA 不改写为本轮结果。

## 两种运行方式

| 项目 | Windows 专用服务器 | Mac 房主边玩边共享 |
| --- | --- | --- |
| 启动命令 | npm run server | npm run host |
| 战局计算位置 | Windows Node 进程 | Mac Node 进程 |
| 本机浏览器 | 不自动打开 | 成功监听后自动打开 localhost |
| 玩家入口 | Mac 浏览器访问 Windows 的局域网地址 | 房主访问 localhost，朋友访问 Mac 的局域网地址 |
| 是否需要服务器玩家占位 | 不需要，首位 Mac 玩家可以创建房间并成为房主 | 房主浏览器创建房间 |
| 默认端口与真人人数 | TCP 7878，最多 8 人 | TCP 7878，最多 8 人 |

专用服务器的“服务器所有者”与房间里的“房主玩家”是不同角色。Windows 可以仅运行服务；Mac 玩家打开页面，选择联机创建房间后获得该房间的开始、阵营分配和分享权限。服务器允许多个房间，现有上限为 12；各房间分别保存 Battle 状态。

## 计算与渲染职责

Node 服务每 50 毫秒推进权威战局。AI、载具和弹体状态、伤害、复活、占点、票数、支援与胜负由服务决定；HTTP 提供游戏网页和音频资源，WebSocket 接收操作并下发玩家私有状态及战场快照。

Mac 浏览器仍需要本机 CPU 和 GPU 来运行 Three.js 渲染、界面、音频、输入及步兵移动预测与纠正。联机时不在浏览器调用完整 battle.tick。这里的服务器计算指战局规则计算，不是远程视频串流，也不会把 Mac 的图形渲染转移到 Windows。Mac 房主模式仍通过浏览器连接本机服务，使用同一套权威协议。

## 安装与首次启动

运行服务的机器安装 Node.js 24，在项目根目录执行 npm ci。不要把 Windows 的 node_modules 复制给 Mac，反之亦然。仅作为玩家的 Mac 不需要 Node.js 或下载源码，直接用浏览器访问服务器即可。

Windows：双击根目录 Start-IRONFRONT.cmd，按 1 选择专用服务器，按 2 选择本机也游玩。终端也可运行 npm run server 或 npm run host。首次启动会编译权威逻辑、检查 TypeScript 并构建网页；保留该终端运行。

Mac：在项目根目录执行 npm ci，再运行 npm run host。也可执行 bash Start-IRONFRONT.command 选择模式；如希望在 Finder 双击，先执行 chmod +x Start-IRONFRONT.command。脚本使用 LF 换行，Windows 菜单使用 CRLF，仓库通过 .gitattributes 保持一致。

自定义端口：npm run server -- --port 8787，或 npm run host -- --port 8787。禁止使用空值、小数或超出 1 至 65535 的端口。已构建且未修改源代码时可以追加 --skip-build 快速启动；代码或版本更新后必须重新执行默认构建。host 模式追加 --no-open 可关闭自动打开浏览器。

## Mac 玩家连接步骤

1. 服务器启动完成后，从终端“朋友访问”行复制 http://局域网IP:7878。不要把 localhost 发给朋友，也不要使用 Vite 开发端口作为正式联机入口。
2. Mac 与服务器连接同一局域网，浏览器打开该地址。Windows 防火墙如阻止连接，应仅在可信专用网络允许该 Node 服务或 TCP 7878；本轮未自动修改防火墙规则。
3. 第一位玩家在联机页面创建房间，选择地图、模式、AI 与阵营设置，然后复制包含 room 参数的分享链接。
4. 朋友打开完整链接或输入同一服务器地址及六位房间码，完成配装并准备。全员准备后，房主开始倒计时与战斗。
5. 暂停或准备界面可查看连接信息。临时断线保留身份 30 秒并自动重试；主动离房转交房主权限。停止 Node 服务会结束所有房间，重启后需重新建房。

房间房主移交只移交玩家权限，不会把服务器计算自动迁移到另一台电脑。若从 Windows 专用服务改成 Mac 房主服务，需要在 Mac 安装、构建、启动并重新建房分享新的地址。

## 音乐恢复与协作

11 首 MP3 已从用户的 IRONFRONT_v0.20.0_FULL.zip 恢复到 public/audio/music。沿用 public/audio/music/*.mp3 的 Git 忽略规则，音乐和 dist 不提交到公开源码仓库。仓库同步代码与文档，本机完整包继续保存音乐。

Windows 部署完成后，Mac 玩家通过该 Windows 服务即可下载播放音乐，无需额外复制音频。若 Mac 自己担任服务器，需从完整包把 public/audio/music 复制到该 Mac 的同一路径，再构建；仅 clone 公开仓库不会获得这 11 首音乐。docs/MUSIC_MANIFEST.json 保留曲目映射，scripts/import-music.py 是既有本机导入工具。

## 本轮实现

增加 scripts/start-lan.cjs、Windows 与 Mac 菜单、npm server 和 host 命令。启动器从自己的位置定位根目录，因此可从别的工作目录运行；直接用当前 Node 执行 tsc 和 Vite，避免 Windows 与 Unix 的环境变量和 shell 语法差异。只有成功监听后 host 才打开浏览器，构建失败时不启动旧服务。

服务 /api/status 新增 hostMode 与 simulation 字段，版本直接读取 package.json。网页版本显示与兼容检查共享 src/version.ts。server/addresses.cjs 统一服务和测试的 IPv4 地址筛选，避免把本机 Tailscale 地址误当作服务分享地址。LAN 复活测试等待权威模拟时间达到复活截止值，不再假定固定墙钟等待足够。

Playwright 的 PORT 配置改为 env 字段，移除仅适用于 Unix 的 PORT=18788 命令写法。新增真实启动检查，在不同工作目录下分别启动两种模式，检查版本、权威标志、游戏页面、房间创建，以及本机音乐存在时的实际音频字节。

## 验证与实际验收

本轮结果记录在 docs/qa/deployment-021.json。覆盖 TypeScript、142 项单元测试、自动战局、真实 Wi-Fi IP 的 HTTP 与 WebSocket、联机权限和恢复、生产构建、两种启动入口与浏览器房间流程。自动战局默认 32v32 500 票正常结束，但报告中出现 1 次导航失败，不能沿用旧文档的导航零失败结论。

Windows 本机的双客户端验证不能等同于两台物理机器跨系统实测。部署后还需在实际 Mac 上验收：地址可达、建房与加入、音乐播放、双方移动和射击同步、临时断线恢复。Mac 房主入口的脚本和协议已准备，Mac 硬件与系统权限仍须在 Mac 上验收。

## 常见问题与维护

网页能打开但无法开始：确认进入联机房间而非单人模式，所有玩家均已准备，房主仍在线。端口占用：停止旧服务后重试，或指定另一端口并重新分享对应地址。页面显示版本不匹配：刷新浏览器，停止旧 Node 进程，从同一 main 源码重新构建并启动。音乐缺失：检查 MP3 是否位于 public/audio/music，恢复资源后重新构建。

维护前执行 git fetch origin，检查 git status 与 git rev-list --left-right --count main...origin/main，确认本地与远端一致。工作结束按修改类别更新版本，生成详细 DOCX、完成相关检查，提交到 main 后 git push origin main，再核对 HEAD 与远端 main。不得强制推送来覆盖协作者提交；远端有新提交时应先检查并合并。

当前为局域网部署，没有公网匹配、NAT 穿透、账号或持久战局。服务进程退出会丢失内存中的房间。优先使用可信局域网，不把服务直接作为公网托管平台。
