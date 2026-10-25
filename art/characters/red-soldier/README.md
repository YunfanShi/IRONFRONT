# Red Soldier static LOD asset

The supplied Hunyuan red-team soldier was rebuilt as a lighter static character asset. The original has 1,499,976 triangles and approximately 8,001 disconnected surface pieces. Voxel rebuilding removed the fragment noise, eight tiny isolated duplicate-face pairs were removed, and 2K PBR maps were baked from the original surface.

| Level | Triangles | Intended use |
| --- | ---: | --- |
| LOD0 | 19,984 | close view |
| LOD1 | 6,000 | middle distance |
| LOD2 | 2,000 | long distance |

The character is 1.8 m high and has one material and one UV set. The editable source is `red-soldier-technical-v01.blend`, with all three LODs and packed images. `exports/` contains both GLB and FBX versions. GLBs contain their textures; FBXs refer to the four PNGs in `textures/`, so keep the directory structure when moving FBXs outside Unity. The supplied original is retained unchanged in `source/`. The baked colors follow the supplied 3D original, including the salmon-red armor accents, gray vest and tan uniform.

## Unity preview

The imported FBXs and textures are under `UnityProject/Assets/Art/Characters/RedSoldier/`. Drag `RedSoldier_StaticLODPreview.prefab` into a scene to inspect the URP material and distance switching. The editor command **IRONFRONT → Art → Build Red Soldier Static Preview** recreates the material, packed metallic/smoothness map and prefab. The preview uses LOD transition heights 0.55, 0.18 and 0.02.

Unity reads these FBXs at 0.01× and leaves the mesh Z-up. `RedSoldierAssetSetup.cs` sets model import scale to 100 and rotates each preview LOD −90° about X. The generated prefab was checked in Unity 6000.5.10f1: all three LODs import at their stated triangle counts and approximately 1.8 m height, standing on the ground and facing +Z.

This is a **static art preview**. There are no bones, skin weights or animation clips. The current runtime soldier uses procedural body parts and animations; the preview is not wired into gameplay. Before replacing a moving soldier, this mesh needs deformation-friendly retopology around joints, skinning, animation testing and weapon-grip alignment.

## Rebuilding and checking

Run the numbered scripts with Blender 5.2 or later, in order:

```sh
blender --background --factory-startup --python 01_voxel_rebuild.py
blender --background --factory-startup --python 02_bake_pbr.py
blender --background --factory-startup --python 03_finalize_lods.py
python3 04_sync_to_unity.py
blender --background --factory-startup --python 05_render_character_qa.py
```

Stages 1 and 2 create temporary files in `work/`; this folder can be removed after stage 3. `build-report.json` records counts and bounds. `qa/` contains clean-import metrics, the final visual QA views and `review-v01.json` with the verified review state.
