# Blue Soldier static LOD asset

The supplied Hunyuan blue-team soldier was rebuilt as a lighter static character asset. The original has 1,500,000 triangles. Voxel rebuilding removed thousands of tiny disconnected pieces; the 2K PBR maps were baked from the original surface onto the new mesh.

| Level | Triangles | Intended use |
| --- | ---: | --- |
| LOD0 | 20,000 | close view |
| LOD1 | 6,000 | middle distance |
| LOD2 | 2,000 | long distance |

The character is 1.8 m high and has one material and one UV set. The editable source is `blue-soldier-technical-v01.blend`, with all three LODs and packed images. `exports/` contains both GLB and FBX versions. GLBs contain their textures; FBXs refer to the four PNGs in `textures/`, so keep the directory structure when moving FBXs outside Unity. The original file is retained in `source/`. The baked colors follow that supplied 3D original, which is lighter than the earlier 2D concept images.

## Unity preview

The imported FBXs and textures are under `UnityProject/Assets/Art/Characters/BlueSoldier/`. Drag `BlueSoldier_StaticLODPreview.prefab` into a scene to inspect the URP material and distance switching. The editor command **IRONFRONT → Art → Build Blue Soldier Static Preview** recreates the material, packed metallic/smoothness map, and prefab. The preview uses LOD transition heights 0.55, 0.18, and 0.02.

Unity reads these FBXs at 0.01× and leaves the mesh Z-up. `BlueSoldierAssetSetup.cs` sets model import scale to 100 and rotates each preview LOD −90° about X. The generated prefab was checked in Unity 6000.5.10f1: all three LODs import at their stated triangle counts and approximately 1.8 m height, standing on the ground and facing +Z.

This is a **static art preview**. There are no bones, skin weights, or animation clips. The current runtime soldier uses procedural body parts and animations; the preview is not wired into gameplay. Before replacing a moving soldier, this mesh needs deformation-friendly retopology around joints, skinning, animation testing, and weapon-grip alignment. Only the blue-team model was supplied and processed.

## Rebuilding and checking

Run the numbered scripts with Blender 5.2 or later, in order:

```sh
blender --background --factory-startup --python 01_voxel_rebuild.py
blender --background --factory-startup --python 02_bake_pbr.py
blender --background --factory-startup --python 03_finalize_lods.py
python3 04_sync_to_unity.py
```

Stages 1 and 2 create temporary files in `work/`; this folder can be removed after stage 3. `build-report.json` records counts and bounds. `qa/` contains clean-import metrics and rendered views of the finished exports. `qa/review-v01.json` records the technical review and any remaining formal review gaps.
