# H8 Kestrel 验收记录

- 已按 `contract.md` 和 `reference_notes.md` 核对原 GLB、项目 H8 四视图与 Unity 原型。原始高模不修改。
- 已完成比例、主要形体、语义分件、材质/贴图、旋翼及枪口轴心、LOD 导出与新鲜导入检查。蓝队 LOD0 已人工检查透视、前、后、左、右、顶六视图；红队检查透视、前、左、顶视图。LOD2 首版 12k 三角面出现破面，废弃并由 LOD0 直接重做为 20.6k。
- GLB 与 FBX 的 LOD0 新鲜导入均为 51,011 三角面，整体包围盒相同。LOD1 为 25,661，LOD2 为 20,591。GLB 三张嵌入图均为 2048²；蓝红 base color 文件不同。LOD0 有 9 个网格、2 个材质、0 个退化面、0 个无效坐标；FBX 自动导入只重建了 albedo 和 normal 连接，ORM 图需 Unity 手动装配。
- 核验文件：`evidence/blue-h8-lod0-glb-validation.json`、`evidence/blue-h8-lod0-fbx-validation.json`、`evidence/blue-lod0-inspect.json`、`evidence/blue-lod0-fbx-inspect.json`、`evidence/blue-lod2-inspect.json`。
- 限制：保留原高模大量独立壳片和开放边；该资产是可用的静态/刚性可动游戏视觉模型，不是完全重新拓扑的封闭网格。玻璃为原图集里的不透明着色。没有导出旋翼动画，Unity 按 `MainRotor`/`TailRotor` 空对象绕轴实时驱动。
