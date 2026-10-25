# Soldier runtime replacement — complete

Version 0.57.0, 2026-10-24. Red/blue soldiers now use authored 17-bone skins and 3 LODs in local/AI/remote/menu paths. First-person firearm arm meshes omit hidden torso weights; M2 views retain full posed arms. Existing procedural gameplay actions remain the driver.

Final verification: 160 Play assertions; 6 fresh FBX round trips; 4 existing gameplay regression groups; macOS development-player exit 0, runtime errors 0 and 10 captured frames. Original 16 model/source/export hashes unchanged. Source, export, runtime mesh and code manifest contains 39 files.

Visual repairs complete: coordinate facing/left-right remap; bone-heat shoulder weights; hand reach/hand curl; M2 physical left/right contacts and boot-sole offset; isolated first-person weights and framing; lobby actual arm lengths and retained PBR.

Sources: ../blue-soldier/rigged and ../red-soldier/rigged. Integration: Assets/Resources/Soldiers, PrototypeSoldierArtRig, PrototypeSoldierVisual and SoldierArtAssetSetup. Reproduction and scope: docs/UNITY_HANDOFF_057.md. Test app: UnityProject/Builds/Soldiers-0.57.0/IRONFRONT.app. No user work reset, no source model overwritten, no commit/merge/push.

Boundary: combined gloved-finger bones, no facial rig or separate five-finger animation; existing procedural weapons retained. Network object spawn verified locally, not a fresh multi-device network session.
