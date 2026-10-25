# Unity 0.59.1 — 恢复 IF-27 第一人称枪托

2026-10-24。用户指出枪托缺失。0.59.0 的导入脚本对第一人称预制体执行 `Stock.SetActive(false)`，因此整个枪托从第一人称画面消失。原始 GLB、Blender 源文件和第三人称模型仍有枪托。

## 修复

删除 `WeaponArtAssetSetup.BuildPrefab` 中按第一人称隐藏 Stock 的语句，重建 IF27View/World 预制体。两个视角都保留完整枪托，不改几何、握持点、枪械参数、镜头、换弹轨迹或角色动作。第一人称完整枪械为 62,498 三角面，另有瞄准红点。

## 验证

- 编辑器和最终 macOS 独立程序的现有枪械检查均 PASS，版本 0.59.1，运行错误 0。
- 瞄具与屏幕中心对齐；从相机射出的中心视线没有与 Stock 网格相交。
- 检查完整的第一/第三人称换弹、站立/跑步/蹲伏/趴伏握持、实际开火和弹量。已打开最终腰射、开镜及换弹截图确认枪托可见。
- 构建成功，应用文件共 542,684,060 字节。0.59.0 应用的 276 个文件与上一版交付哈希完全一致，未替换正在运行的旧程序。
- 修改只涉及分件可见性，未重跑与本改动无关的五组全量回归；该回归的历史结果在 0.59.0 交接中。

## 交付

- 新程序：`UnityProject/Builds/Weapons-0.59.1/IRONFRONT.app`。需启动此程序才能看到修正。
- 证据：`art/weapons/if27/stock-fix-0.59.1/player/results.txt`、`player/fp-hip.png`、`player/fp-ads.png`、`player/reload-fp/0028.png`。
- 对照图：同目录 `stock-before-after.png`；保留检查 `preservation.json`；版本清单 `delivery-manifest.json`。
- 源模型仍在 `art/weapons/if27/if27-articulated.blend`；原枪模、0.59.0 程序及其预览均保留。

已有分支和未提交工作保持原状，未自动合并或推送历史变更。
