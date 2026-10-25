# Unity 0.61.1 — 大厅字体向确认稿靠拢

2026-10-25。用户指出 0.61.0 实机字体与 A3 参考图不同。本次替换实际运行时字体，并调整标题字重、大小与字距；不重做配装、设置或部署界面。

## 修改

- 大厅「准备行动」「工业前线」「开始游戏」使用内置 Source Han Sans CN Heavy。消除默认中文回退字体笔画过细的问题，不再依赖合成粗体。主标题从原来的 40 / 紧凑布局 35 改成 52 / 50，重新设置高度和字距以接近参考稿。
- 顶部导航、次按钮和选择作战使用 Medium，辅助信息和可编辑名字使用 Regular。左上 IRONFRONT 改为 Black Ops One 军事模板字。共用标志和顶部导航在切换页面后保持一致，配装和设置的主体样式保留。
- 使用 UI Toolkit / TextCore FontAsset，字体源文件随构建内置。中文玩家名继续支持运行时生成字形，不依赖目标电脑是否安装相同字体。
- 字体均来自官方开源仓库，源文件未修改。完整 OFL 许可同时位于源码字体目录与应用 StreamingAssets/ThirdPartyFonts。
- 参考图为生成的栅格概念图，没有可提取的原始字体元数据。本次依据轮廓、粗细与排版选择接近字体，不声称找回完全相同的原始字库；英文标志不包含概念图的做旧纹理。

## 文件

- `Assets/Resources/Menu/LobbyTheme.uss`：字库引用、真实字重和标题尺寸；其他布局只随标题高度自然调整。
- `Assets/Fonts/Lobby/`：Regular、Medium、Heavy、Black Ops One 原字体及许可、README。
- `Assets/Resources/Menu/Fonts/`：四个 TextCore FontAsset。
- `Assets/Editor/PrototypeLobbyFontBuilder.cs`：可重复执行的字体生成和缺字校验入口「IRONFRONT / Generate Lobby Fonts」。保留动态字体源文件；Unity 可在构建时清理动态缓存。
- `Assets/Scripts/PrototypeLobbyProbe.cs`：现有大厅验证增加真实解析字体、源字库字形覆盖、文本宽度和中文名字检查。测试通过实际生产回调及 panel.Pick，不是系统键鼠录像。
- `Assets/StreamingAssets/ThirdPartyFonts/`：随应用分发的许可。

## 验证

- Unity 6000.5.10f1 / StandaloneOSX 开发构建成功。
- 独立程序验证 PASS，RuntimeErrors=0。实际检查 960×600、1280×800、1600×900、1920×1080、2560×1080 五种尺寸；主标题、地图名、主次按钮、导航和辅助信息均解析到指定内置字库，文本宽度未超出控件。
- 已查看 16:9、小窗口、超宽屏和中文玩家名实机截图；标题没有裁字或缺字方框。中文可编辑名字还校验了「龘」在 Regular 源字库中的覆盖。
- 原地图/模式选择、返回大厅、配装切换、设置参数、LAN 房间入口和单人 8v8 开局通过现有程序验证。没有进行完整双客户端联机测试；本次没有改联机或战斗代码。
- 初次测试把 Unity 新文字引擎未填充旧 characterLookupTable 误判为缺字，结果和日志保留。已改为直接验证指定内置字体能否生成所需字形，最终独立程序通过。旧查询 API 的新增警告已移除。
- 原 0.61.0 应用、已有菜单资源及指定玩法脚本共 341 个基准文件哈希保持一致；StartMenu.uxml、StartMenuTheme.uss、实际菜单控制器和人物预览代码均未修改。ProjectSettings 相对本次基准只改 bundleVersion。版本统一为 0.61.1。
- 原 PrototypeWeaponArtProbe 的两项废弃 API 警告保留，与本次字体无关。

## 交付与复现

- 新应用：`UnityProject/Builds/LobbyTypography-0.61.1/IRONFRONT.app`。已打开的旧 0.61.0 窗口不会自动更新，需要打开这个新版本。
- 实机图：`art/ui/lobby-typography-2026-10-25/player-02/lobby-1600x900.png`。
- 最终验证：`player-02/results.txt`、`layout.csv`、`player-02.log`；构建 `build-02.log`；字体生成 `fonts-01.log`；保留/图像检查 `validation.json`。
- 字体来源与下载 SHA-256：`art/ui/lobby-typography-2026-10-25/font-sources.json`。上版代码备份位于同目录 `baseline/`。
- 调用应用参数 `-ironfrontLobbyProbe <输出目录>` 可复现字体、分辨率和原功能入口验证，完成后自动退出。不要与其他修改 PlayerPrefs 的验证并行运行。

字体来源：[Adobe Source Han Sans](https://github.com/adobe-fonts/source-han-sans/tree/release/SubsetOTF/CN)、[Google Fonts Black Ops One](https://github.com/google/fonts/tree/main/ofl/blackopsone)。

工程继续保留在 codex/unity-port；开始时 HEAD 与本地 origin/main 引用相差 36/5，已有大量历史未提交成果。本次保留这些工作，未自动合并、提交或推送历史修改。
