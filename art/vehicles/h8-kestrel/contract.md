# H8 Kestrel game asset contract

- Source: user-supplied `33717aab9aa8b9b3fb2bb108d94498cb.glb` (blue H8, 1,499,888 triangles), kept unmodified in Downloads.
- Reference: `../../hunyuan-vehicle-refs/blue-h8.png` and four split views. Preserve the angular armored nose, faceted canopy, twin turbine housings, short weapon wings, skid gear, long tail boom, and rotor silhouette. Source has six main blades; reference has four. Use four blades for the deliverable if rebuild quality allows.
- Unity contract: scene root at origin, aircraft nose points Blender -Y so converted model points Unity +Z; ground at y=0 Unity; rotor diameter 13.4 m. Semantically separate main rotor, tail rotor, chin weapon, port/starboard wing stores, and static airframe. Moving assemblies need pivots at their actual axes. Use names that can be found in Unity.
- Finish: smooth enough for game camera while retaining recognizable source detail and blue steel armor. LOD0 target roughly 30–60k triangles if achievable without losing character. LOD1 and LOD2 visibly lower. Portable 2K texture set or equivalent. Export GLB and Unity-native FBX, plus a self-contained editable .blend and deterministic source script.
- Validation: new Blender process import for GLB and FBX, geometry/material/pivot/dimension metrics, six-view renders, inspect actual images, document defects honestly.

Review questions: Does LOD0 still look like the supplied H8 from front, side, top and three-quarter views? Do separated rotor, tail rotor and weapon assemblies rotate around usable axes? Is the model acceptably sized and oriented for Unity's +Z-forward convention?
