# IRONFRONT Unity 背景音乐交接（0.47.0）

基于 `codex/unity-port` 的 0.46.0 姿态、载具与座位扩展，新增三首原创无歌词循环音乐，以及随战况切换的本地音乐控制。本轮分别由作曲、Unity 集成、只读审查子智能体协作，主智能体负责菜单设置、整合、实际 Unity 验证与构建。

## 音乐与资源

三个音轨存放于 `UnityProject/Assets/Resources/Audio/Music/`：

| 场景 | 曲名 / 音轨 | 时长 / 速度 | 编曲方向 |
| --- | --- | --- | --- |
| 背景大厅 | Before the Front / `lobby.wav` | 53.33 秒 / 72 BPM | 平静、宽阔的电钢琴与弦乐 |
| 快要胜利 | The Final Advance / `victory-near.wav` | 60 秒 / 128 BPM | 明亮铜管、弦乐、推进鼓点与上扬主题 |
| 快要失败 | Hold the Line / `defeat-near.wav` | 68.57 秒 / 112 BPM | 低音脉冲、紧张弦乐、较暗的主题变化 |

乐曲以共同的 D4–F4–G4–A4 短主题进行原创编排，通过 macOS AVAudioUnitSampler 渲染本机 GM 乐器采样，并以 NumPy 制作原创鼓点、低音与环形混响，未使用现成歌曲或商业循环。系统音色库仅用于本机渲染，不随项目分发。乐谱在 `docs/music-scores/`，制作脚本在 `scripts/generate-unity-music.py` 与 `scripts/render-native-instrument.swift`。

交付 WAV 为 44.1 kHz、16 bit、立体声。三首 RMS 均为 -19.5 dBFS，峰值分别为 -5.958 / -5.091 / -5.479 dBFS，削波样本为 0；首尾采样跳变为 0，无整秒静音。循环保留连续演奏的中间一轮，并将混响尾音环绕衔接。实际测量及 SHA-256 见 [资源清单](UNITY_MUSIC_MANIFEST_047.json)。Unity 以 Streaming / Vorbis 质量 0.8 导入，保留立体声，不做额外归一化。

## 切换规则

`PrototypeMusicDirector` 使用两个 2D 循环声源，状态切换以 2.8 秒渐变衔接。低票判定持续 3 秒才接受；攻防最终据点夺取持续 0.5 秒即接受，以便快速夺点时仍能提前播放。使用不受暂停影响的时间；倒地、救援、部署和暂停菜单仍属于原战局。正常战斗没有新增第四首音乐，环境与战斗音效继续照常播放。

- 大厅和联机房间播放大厅曲。进入正常战斗时淡出；返回大厅重新播放大厅曲。
- 征服：一方票数降至初始上限 25% 时，按玩家所属阵营选择胜利/失败临近；退出阈值为 32%。双方同时低票时先比较 5% 初始票数的明确差额，再依据双方占点多数判断掉票方向。优势不明确时维持已有临界曲或安静，避免一票差引起反复切换。联机房间按实际初始票数计算比例。
- 攻防：蓝方进攻票数低代表蓝方快输、红方快胜；最终据点的控制达到 +50 且蓝方正在无争议夺取，代表蓝方快胜、红方快输。+50 对应从 -100 到 +100 全程的 75%。两种预警同时满足时，进攻方低票优先。防守方无限票数不用于判定。
- 结算的实际 Winner 立即决定对应乐曲，不等待临界确认。近胜音乐代表当前战况优势，并不保证最终获胜。

## 音量与生命周期

大厅设置和暂停菜单新增独立“背景音乐”滑杆，默认 55%，可单独静音音乐。主音量继续通过 `AudioListener.volume` 同时控制音乐与音效；音乐偏好保存于 `ironfront.unity.musicVolume`。音量改变即时作用于正在播放和渐变的曲目，不重启乐曲。

组件禁用/销毁停止声音，场景重新加载不残留声源。第三首曲目若在两个声源渐变中被请求，会等待释放声源后衔接，避免直接切断仍可听见的音轨。编辑器预览和烟测入口仅用于开发，独立程序根据真实战况运行。

## 代码入口

- `Assets/Scripts/PrototypeMusicDirector.cs`：状态、阈值、确认、渐变、独立音量与声源生命周期。
- `PrototypeRuntime.cs`：创建音乐控制器、每帧传入实际战况与玩家阵营、销毁清理。
- `PrototypeStartMenuView.cs`、`PrototypeFrontend.cs`、`Resources/Menu/StartMenu.uxml` / `StartMenuTheme.uss`：大厅和暂停设置。
- `Assets/Editor/PrototypeMusicSmoke.cs`：状态及真实音源检查、开发试听。
- `Assets/Editor/PrototypeBuild.cs`：标准构建和版本目录构建。

## 验证与构建

2026-10-11 实际执行：

- 独立审查子智能体复测最终三个 WAV，采样格式、时长、RMS、峰值、削波、循环边界及文件哈希均符合上述记录。
- 全部 Runtime 和 Editor C# 通过 Unity 自带 Roslyn 编译；实际 Unity 导入与编译完成，控制台无编译错误。
- Unity Play 模式执行 `IRONFRONT/Verify Adaptive Music` 通过：实际加载三首音轨并验证声源播放，覆盖双方阵营、征服/攻防、800 初始票数、确认时间与退出阈值、结算/大厅、暂停渐变、静音后播放位置连续、快速三曲切换队列及两个声源生命周期。
- 实际目视确认大厅设置和暂停菜单的音乐滑杆及按钮完整显示，无重叠。独立程序 0.47.0 启动到大厅，并再次确认设置页显示背景音乐 55%。
- `IRONFRONT/Build macOS Versioned` 成功，产物为 `UnityProject/Builds/macOS-0.47.0/IRONFRONT.app`，构建报告大小 122,165,315 bytes，Info.plist 版本为 0.47.0。独立程序启动日志来自该路径，没有报错或缺失音乐警告。
- 功能文件 `git diff --check` 通过。验证摘要保存在 `UnityProject/Builds/Verification047/validation.txt`。

音频技术检查与实际播放状态已验证；完整曲目的审美验听留给玩家试听反馈。可直接打开以上 WAV，或在 Unity Play 模式使用 `IRONFRONT/Music Preview` 试听三个状态，`Resume Adaptive Music` 恢复真实战况切换。

## 范围

本轮实现 Unity 本地音乐播放；既有网页音乐系统未替换，LAN 服务器不新增音乐同步协议，各客户端根据已收到的战况判断。本轮未合并 main、未推送。已有 Unity 自动改写设置保持原状，本轮额外有意设置改动为版本号 0.47.0。
