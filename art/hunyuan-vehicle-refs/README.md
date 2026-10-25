# IRONFRONT 双方载具建模参考图

本目录为 Unity 原型中的双方载具提供图生 3D / 人工建模参考。每种车型有蓝、红两张四视图参考板；同车型应共用几何模型，仅替换阵营涂装。图片是概念参考，准确比例、碰撞体和可动件轴心仍以游戏工程为准。

四视图已另外裁成 [72 张独立视角 PNG](single-views/README.md)，可按车型、阵营和视角分别取用。

## 视图布局

参考板均为 PNG、2 × 2 布局；除 M2 摩托车蓝红版为 1426 × 1103 外，其余为 1536 × 1024：

| 左上 | 右上 |
| --- | --- |
| **正面**（FRONT） | **左侧**（LEFT SIDE） |
| **背面**（REAR） | **顶部**（TOP） |

各格采用接近正交的静态展示，背景保持简洁。载具在 Unity 中面向本地 **+Z**。如果建模服务要求每个视角作为独立输入，应按四格拆分；不要把蓝、红两队同时作为同一次图生 3D 的主体。

## 车型清单

| 车型 | 蓝队 | 红队 | 建模时需要分开的结构 |
| --- | --- | --- | --- |
| T90 BASTION 主战坦克 | [blue-t90.png](blue-t90.png) | [red-t90.png](red-t90.png) | 车体、履带、炮塔、主炮、观瞄 |
| V12 LANCER 八轮步战车 | [blue-v12.png](blue-v12.png) | [red-v12.png](red-v12.png) | 八个车轮、炮塔、30 mm 炮 |
| A40 WATCHTOWER 八轮防空车 | [blue-a40.png](blue-a40.png) | [red-a40.png](red-a40.png) | 八个车轮、炮塔、四管防空炮、雷达 |
| R4 SCOUT 装甲侦察车 | [blue-r4.png](blue-r4.png) | [red-r4.png](red-r4.png) | 四个车轮、顶部机枪 |
| U8 ROVER 武装运输车 | [blue-u8.png](blue-u8.png) | [red-u8.png](red-u8.png) | 四个车轮、开放后舱、机枪座 |
| M2 COURIER 双座摩托 | [blue-m2.png](blue-m2.png) | [red-m2.png](red-m2.png) | 前后车轮、转向前叉、双座、脚踏 |
| H8 KESTREL 武装直升机 | [blue-h8.png](blue-h8.png) | [red-h8.png](red-h8.png) | 机身、主旋翼、尾旋翼、短翼、机枪 |
| R3 FIREFLY 侦察直升机 | [blue-r3.png](blue-r3.png) | [red-r3.png](red-r3.png) | 机身、主旋翼、尾旋翼、雷达和传感器 |
| J20 STRATUS 喷气机 | [blue-j20.png](blue-j20.png) | [red-j20.png](red-j20.png) | 机身、机翼、尾翼、舵面、进气道 |

## 生成设定

- 工具：Codex 内置 ImageGen；用途为 `stylized-concept` 游戏载具建模参考。
- 蓝队：灰蓝车体与冷青识别色；红队：灰棕车体与暗陶红识别色。深灰装甲、黑色橡胶与蓝黑玻璃共用。
- 美术方向：原创近未来军事硬表面设计，偏写实，结构和大轮廓清楚，便于后续制作游戏低模。
- 画面限制：同一车型四视图共用同一机械设计；无场景、人物、文字、编号、徽标和水印。
- 阵营版制作：先生成蓝队四视图，再以其为编辑目标换成红队涂装，保持车体、轮组和武装形制一致。

## 提示词记录

蓝队各车型以如下模板分别生成，并填入下方对应的车型细节：

```text
Use case: stylized-concept. Asset type: IRONFRONT game vehicle 3D modeling turnaround reference, not poster art. Draw ONE original fictional near-future {VEHICLE}. Show the SAME mechanically coherent vehicle four times in a clean 2x2 board: upper-left exact FRONT, upper-right exact LEFT SIDE, lower-left exact REAR, lower-right exact TOP DOWN. Near-orthographic, centered, fully visible, consistent hull, wheel/track, weapon and cockpit placement across panels. {FEATURES} Crisp semi-realistic military hard-surface rendering, broad shapes that can be simplified to a low-poly game asset, and clear movable-part boundaries. Blue faction matte desaturated blue-gray armor with sparse cold-cyan identifiers; dark gunmetal mechanisms, black rubber and smoky blue-black glass. Neutral light-gray studio background, even soft light. No people, environment, text, numbers, logos, flags or watermark.
```

| `{VEHICLE}` | `{FEATURES}` |
| --- | --- |
| T90 BASTION | Broad sloped tracked main battle tank; one long central cannon on a separate turret, seven road wheels on each side, side skirts and rear fuel drums. |
| V12 LANCER | Eight-wheeled infantry fighting vehicle with four wheels per side, sloped nose, rear troop door, compact turret and exactly one 30 mm cannon; no radar or tracks. |
| A40 WATCHTOWER | Eight-wheeled anti-air vehicle with four wheels per side, separate turret, four 35 mm barrels in a 2 × 2 arrangement and an independent tilted rectangular radar array; no tracks. |
| R4 SCOUT | Compact enclosed four-wheel armored scout car, sloped hood, small windows, doors and a roof-mounted rotating machine gun with shield. |
| U8 ROVER | Four-wheel transport with enclosed two-seat front cab, open rear bed with two passenger seats and rails, and one roof machine gun. |
| M2 COURIER | Unarmed two-person off-road motorcycle with exactly two inline wheels, exposed frame, engine, fuel tank, front steering fork, two saddles, foot pegs and passenger grab rails. |
| H8 KESTREL | Heavy attack helicopter with broad armored cockpit, four-blade main rotor, tail rotor, twin turbines, short weapon wings, nose gun, belly machine gun and landing skids. |
| R3 FIREFLY | Smaller light reconnaissance helicopter with panoramic bubble cockpit, slim tail boom, main and tail rotors, radar, nose sensor ball and small gun; no weapon wings or rocket pods. |
| J20 STRATUS | Original single-seat jet with pointed nose, side intakes, swept wings, two canted vertical tails, one large exhaust, nose cannon, wing pylons and landing gear. |

红队以对应蓝图作为编辑目标，使用共同的定向改色提示词：

```text
Use case: precise-object-edit. Input image is the exact blue-team {VEHICLE} four-view sheet and EDIT TARGET. Recolor ONLY painted blue-gray or teal armor to matte desaturated gray-brown and muted dark-terracotta panels; recolor cyan faction marks to dark coral red. Preserve all four camera views, layout, silhouette, proportions, wheels, tracks, rotors, weapons, seats, panel seams, glass, gunmetal machinery, lighting and background. Material repaint only. No added or removed parts, no people, text, numbers, logos or watermark.
```

这些图尚不是可导入 Unity 的 3D 网格。图生 3D 后仍需核对四视图之间的细节一致性，整理拓扑和贴图，拆出旋转、转向、俯仰部件，并按游戏代码复核比例与轴心。

AI 多视图可能有局部角度偏差。R4 顶部机枪在各格的朝向略有差异；M2 顶视车把略偏转。建模时以正面和侧面的直行姿态确定轴心。A40 的四根炮管在严格侧视下互相遮挡，应结合正面和顶视确认其 2 × 2 布局。
