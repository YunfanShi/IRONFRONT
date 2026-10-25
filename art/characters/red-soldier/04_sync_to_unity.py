"""Copy the optimized static meshes and textures into the Unity project."""

from pathlib import Path
from shutil import copy2


ROOT = Path(__file__).resolve().parent
UNITY = ROOT.parents[2] / "UnityProject" / "Assets" / "Art" / "Characters" / "RedSoldier"


def main():
    for subdir, names in {
        "exports": [f"red-soldier-lod{i}.fbx" for i in range(3)],
        "textures": [
            "red-soldier-basecolor-2k.png",
            "red-soldier-normal-2k.png",
            "red-soldier-roughness-2k.png",
            "red-soldier-metallic-2k.png",
        ],
    }.items():
        destination = UNITY / subdir
        destination.mkdir(parents=True, exist_ok=True)
        for name in names:
            copy2(ROOT / subdir / name, destination / name)
            print(destination / name)


if __name__ == "__main__":
    main()
