# Soldier rig v03 — hand correction

Derived from the preserved rigged-v02 source. Geometry, triangles, UVs, materials and all body weights remain unchanged. The wrist joint was inside the palm (z=.90); this revision places it at the cuff (z=.98), adjusts the forearm endpoint and finger pivot, adds a thumb control per hand, and reweights the hand/wrist region. This is the existing character, not a replacement mesh.

Reproduction: run `../../model-rig-audit-2026-10-26/repair_hand_skin.py` from Blender (script resolves paths itself), then `validate_hand_exports.py` for an independent old/new FBX import comparison. Unity `SoldierArtAssetSetup.Build` imports these three FBX LODs, derives arm lengths from the new rest skeleton, remaps camera-only torso weights, and preserves the full arm surface.

The original v02 .blend and FBXs remain available. Four fingers still share one combined flex bone; this rig does not provide independent index-finger trigger animation.
