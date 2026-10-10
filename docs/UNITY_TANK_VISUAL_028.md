# 0.28.0 Unity T90 坦克外观预览

## 目标与现状

本轮从 `codex/unity-port` 的 0.27.0 继续。开始时本地 `HEAD` 与远端该分支均为 `0098b73`，`main` 保持独立。网页游戏的 `Vehicle.ts` 定义了 `kind: 'tank'` 的 T90 BASTION，可进入、驾驶与战斗；`VehicleVisuals.ts` 绘制其外观。Unity 原型当时只有 R4 侦察车和 U8 运输车，没有坦克实体。用户希望先提升坦克的建模，所以本轮在 Unity 补充 T90 的可见外形，不把未实现的坦克驾驶或武器功能称为已完成。

## 实现

- 新增 `PrototypeTankVisual.cs`，用可复用的方块、圆柱与自建硬边斜面网格组成双阵营坦克。车体包括下部装甲槽、倾斜首上装甲、后甲、引擎舱通风栅、前灯和拖钩；底盘两侧分别有七个负重轮、轮毂、上/下履带、履带片与五块分段侧裙。炮塔有独立根节点、座圈、斜面装甲、正面附加甲、炮盾、主炮、热护套、炮口制退器、同轴机枪、车长舱盖、观瞄、烟幕弹筒、天线与阵营标识。采用现有 URP 军事配色材质，不引入外部贴图或第三方模型。
- 蓝红两队在基地附近各放置一辆 T90 预览。外观分件移除逐件碰撞，只在模型根部设置一个车体 `BoxCollider`；`PrototypeLayout` 为两处位置配置占地障碍，供 AI 路径规划绕行。障碍在场景中由坦克模型替代显示，不再生成占位立方体。
- 预览与 `IPrototypeVehicle`、车辆座位、伤害/武器和局域网快照分离。靠近己方坦克按 E 时，HUD 会明确显示模型预览提示。`PrototypeBuild.cs` 将新脚本列为独立构建所需文件。
- 按 `ForAgent.md` 的新模块规则升级至 0.28.0，同步 `package.json`、锁文件、网页菜单、LAN 服务端和客户端的版本门槛，以及 Unity 应用版本。Unity 功能继续留在 `codex/unity-port`，遵从用户此前不推到 `main` 的要求。

## 验证

- Unity 6000.5.10f1 中运行 `SampleScene`，蓝方基地附近可见 T90 模型；控制台 0 错误、0 警告。画面检查仅覆盖初始视角和模型可见性，暂不代表所有距离与角度的美术验收。
- Unity 菜单 `IRONFRONT/Build macOS Standalone` 构建成功，输出 `UnityProject/Builds/macOS/IRONFRONT.app`，Unity 报告大小 116,650,661 字节；`Info.plist` 的应用版本为 0.28.0。已制作 `IRONFRONT-Unity-T90-0.28.0-macOS.zip`，`unzip -tq` 无错误。构建时 Unity 自动重写的 URP/项目设置已清理，只保留本轮所需的应用版本变更。
- TypeScript `tsc --noEmit` 通过；`scripts/test-unity-room.cjs` 的房主权限回归通过；`scripts/test-lan.cjs` 的网页双客户端 LAN 回归通过。这两项 LAN 测试验证版本与既有权限协议没有被破坏，不代表坦克已支持联机。

## 当前限制与后续

这是一辆静态外观预览，不会移动、转动炮塔、开炮、受损或供玩家入座，也不进入联机载具同步。下一步若继续坦克移植，应接入 `IPrototypeVehicle`、驾驶/炮手座位、履带运动和炮塔俯仰、主炮弹道与伤害、AI 使用，再做双客户端权限与同步验证。当前模型是 Unity 基础几何组成的低多边形样件，材质、贴花、履带轮廓和近距离细节可在 gameplay 接通后再细化。
