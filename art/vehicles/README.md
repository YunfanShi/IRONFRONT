> 2026-10-23 / 0.56.0：R4、U8、M2、R3、J20 已完成 articulated 分件及红蓝 Unity 接入。九款载具全部替换，原静态文件保留。最新入口：[交接](../../docs/UNITY_HANDOFF_056.md)。下文早期“待接入”状态以此更新为准。

# IRONFRONT 载具资产

## 2026-10-23：T90 / V12 可动接入完成

T90/V12 已新增可动工程、三档 FBX、红蓝 Unity 预制体并接入现有驾驶与武器逻辑。现在 H8/A40/T90/V12 已接入；R4/U8/M2/R3/J20 仍为已完成的红蓝静态资产。以下早期七款静态交付记录保留为历史状态。

- [T90 可动版本](t90-bastion/README.md)：炮塔、炮管、后坐，保留原履带表现。
- [V12 可动版本](v12-lancer/README.md)：炮塔、炮管、八轮、前两轴转向。
- [Unity 0.55.0 交接](../../docs/UNITY_HANDOFF_055.md) · [实际 Play 结果](armor-integration-2026-10-23/play-results.txt) · [源文件保留与 FBX 回读](armor-integration-2026-10-23/asset-validation.json)


## 2026-10-23：七款红队涂装已完成

T90、V12、R4、U8、M2、R3、J20 已有红队三档 GLB/FBX、可编辑 Blender 工程和 2K Base Color。加上先前的 H8/A40，九款载具现在都具备蓝、红两队外观。

本次按现有红队参考图制作灰棕装甲、红色标识、砖红航空涂装。复用原网格与 UV；保留 PBR 贴图，并调整不正常的源材质高光。已确认 21 个 GLB 的几何、法线、UV 与对应蓝队完全一致，且写入红队 2K 贴图。此为定向导出确认，没有重做完整模型质量检查。

[全部七款红队 GLB 下载](red-variants-2026-10-23/seven-red-vehicles-glb.zip) · [涂装总览](red-variants-2026-10-23/preview.png) · [交付清单](red-variants-2026-10-23/delivery_manifest.json) · [生成脚本](create_red_variants.py)

| 车型 | 文件与说明 |
| --- | --- |
| T90 主战坦克 | [t90-bastion](t90-bastion/README.md) |
| V12 八轮步战车 | [v12-lancer](v12-lancer/README.md) |
| R4 装甲侦察车 | [r4-scout](r4-scout/README.md) |
| U8 武装运输车 | [u8-rover](u8-rover/README.md) |
| M2 双座摩托车 | [m2-courier](m2-courier/README.md) |
| R3 侦察直升机 | [r3-firefly](r3-firefly/README.md) |
| J20 喷气机 | [j20-stratus](j20-stratus/README.md) |

这七款仍是静态优化版，活动部件、真实尺寸校准和 Unity 接入尚未制作。

## 2026-10-23：剩余五款载具静态优化

T90、V12、U8、M2、R3 已导出三档 LOD、2K PBR、GLB/FBX 和 Blender 工程。

| 载具及文件入口 | 原始三角面 | LOD0 | LOD1 | LOD2 | LOD0 GLB |
| --- | ---: | ---: | ---: | ---: | ---: |
| [T90 BASTION 主战坦克](t90-bastion/README.md) | 1,498,808 | 80,000 | 40,000 | 20,000 | 14.74 MiB |
| [V12 LANCER 八轮步战车](v12-lancer/README.md) | 1,491,418 | 70,000 | 35,000 | 17,500 | 13.09 MiB |
| [U8 ROVER 武装运输车](u8-rover/README.md) | 1,492,006 | 60,000 | 30,000 | 15,000 | 14.38 MiB |
| [M2 COURIER 双座摩托车](m2-courier/README.md) | 1,489,086 | 50,000 | 25,000 | 12,500 | 12.18 MiB |
| [R3 FIREFLY 侦察直升机](r3-firefly/README.md) | 1,499,780 | 50,000 | 25,000 | 12,500 | 12.34 MiB |

本批按用户要求直接处理，未进行单独质量评审或完整回读验收。保留源模型外观和归一化尺寸，目前为静态优化版；可动分件、真实尺寸校准与 Unity 接入尚未制作。红队涂装已由本页最新交付补齐。

[全部 15 个 GLB 打包下载](batch-2026-10-23/remaining-five-vehicles-glb.zip) · [交付清单](batch-2026-10-23/delivery_manifest.json)

## 2026-10-16：J20 / R4 模型优化

项目中未发现本次两份上传文件的先前优化版，现已制作完成。保留原外形、涂装和比例，提供 2K 贴图、三档 LOD、GLB、FBX 与可编辑 Blender 工程。

| 载具及文件入口 | 原始三角面 | LOD0 | LOD1 | LOD2 | LOD0 GLB |
| --- | ---: | ---: | ---: | ---: | ---: |
| [J20 STRATUS 喷气机](j20-stratus/README.md) | 1,499,582 | 40,000 | 20,000 | 10,000 | 12.17 MiB |
| [R4 SCOUT 装甲侦察车](r4-scout/README.md) | 1,498,158 | 60,000 | 30,000 | 15,000 | 14.03 MiB |

全部 12 个导出回读通过；已检查近景六视图和远景预览。原始文件未修改。这两款目前是静态网格优化版，保留源模型单 Mesh，尚未做可动分件、真实尺寸校准和 Unity 游戏替换。其交付范围与下方已经接入 Unity 的 H8/A40 不同。

## 2026-10-15：H8 / A40 Unity 载具交付

2026-10-15。由用户上传的两份混元 GLB 制作；原始文件未修改。

| 载具 | 原始三角面 | LOD0 | LOD1 | LOD2 | 贴图 |
| --- | ---: | ---: | ---: | ---: | --- |
| H8 Kestrel | 1,499,888 | 51,009 | 25,656 | 20,587 | 2K PBR |
| A40 Watchtower | 1,498,458 | 87,937 | 52,751 | 26,372 | 2K PBR |

## 使用

- [H8 文件与说明](h8-kestrel/README.md)：蓝红两队 GLB / FBX、可编辑 `.blend`、生成脚本。
- [A40 文件与说明](a40-watchtower/README.md)：蓝红涂装与三档 LOD，炮塔、炮管、雷达和八轮可独立驱动。
- Unity 已接入 `Assets/Resources/Vehicles/` 下的 `H8_Blue`、`H8_Red`、`A40_Blue`、`A40_Red` 预制体；双方共用几何，按队伍换贴图。
- 修改导出后，将三档蓝队 FBX 和两队贴图复制到 `Assets/Art/Vehicles/H8Kestrel` / `A40Watchtower`，运行编辑器菜单 **IRONFRONT → Art → Build All Vehicle Prefabs**。不要把额外红队 FBX 放入同一导入目录，避免重复 LOD 匹配。

## 本次处理

空间分件、真实轴心、先焊接重复的 UV 接缝位置再减面、三档 LOD、2K 贴图、蓝红涂装、Unity URP 材质转换与运行时部件同步。修复了直接减面产生的大面积针孔。机体碰撞、枪口和 H8 镜头位置已校准。

## 验证证据

- [最终导出检查](final-validation/summary.json)：20 个 GLB/FBX 新鲜导入，配对面数一致、边界尺寸差小于 1 mm、无无效坐标或退化面，并记录文件 SHA-256。
- [可复现检查脚本](validate_exports.py)：在 Blender 后台执行。
- [Unity Play Mode 日志](final-validation/unity-play.log)：实际 SampleScene 中的双方载具生成、LOD/材质配置和活动部件同步检查通过。
- [H8 六视图](h8-kestrel/evidence/blue-lod0/contact_sheet.png) · [A40 六视图](a40-watchtower/renders/final-six-views/contact_sheet.png)。
- [H8 蓝队场景截图](unity-preview/H8_Blue.png) · [H8 红队](unity-preview/H8_Red.png) · [A40 蓝队](unity-preview/A40_Blue.png) · [A40 红队](unity-preview/A40_Red.png)。截图由 Play Mode 临时检视相机拍摄，使用场景实际光照；H8 的 `_Pilot` 图使用实际座位镜头位置。
- `_Isolated.png` 为洋红背景透孔检查，不是游戏配色。

## 当前边界

这是保留生成模型外观的游戏适配版，未做手工重新拓扑。分件边界仍有开放边，局部细节偏软；碰撞继续使用简化体。乘员独立机枪保留原型外观。已完成编辑器内视觉与活动部件检查，未进行独立发行包、完整对局或多人性能测试。

最终文件以本目录的统一报告为准；单资产目录内较早的 `qa_*` / `*inspect.json` 等报告保留作迭代记录。
