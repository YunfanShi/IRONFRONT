"""Inspect the Hunyuan GLB before creating game LODs."""

import bpy
import json
import math
import sys
from pathlib import Path
from mathutils import Vector


def main():
    source = Path(sys.argv[sys.argv.index("--") + 1])
    output = Path(sys.argv[sys.argv.index("--") + 2])
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(source))
    records = []
    for obj in bpy.data.objects:
        if obj.type != "MESH":
            continue
        mesh = obj.data
        bb = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
        lo = [min(v[i] for v in bb) for i in range(3)]
        hi = [max(v[i] for v in bb) for i in range(3)]
        z_bands = [0] * 12
        x_bands = [0] * 12
        y_bands = [0] * 12
        for vert in mesh.vertices:
            v = obj.matrix_world @ vert.co
            for value, limits, target in ((v.z, (lo[2], hi[2]), z_bands),
                                          (v.x, (lo[0], hi[0]), x_bands),
                                          (v.y, (lo[1], hi[1]), y_bands)):
                span = max(limits[1] - limits[0], 1e-8)
                index = min(11, max(0, int((value - limits[0]) / span * 12)))
                target[index] += 1
        records.append({
            "name": obj.name,
            "vertices": len(mesh.vertices),
            "polygons": len(mesh.polygons),
            "triangles": sum(len(p.vertices) - 2 for p in mesh.polygons),
            "bounds_min": lo,
            "bounds_max": hi,
            "matrix_world": [list(row) for row in obj.matrix_world],
            "uv_layers": [uv.name for uv in mesh.uv_layers],
            "material_slots": [slot.material.name if slot.material else None for slot in obj.material_slots],
            "z_vertex_bands": z_bands,
            "x_vertex_bands": x_bands,
            "y_vertex_bands": y_bands,
        })
    images = []
    for img in bpy.data.images:
        images.append({"name": img.name, "size": list(img.size), "packed": bool(img.packed_file), "source": img.source})
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps({"source": str(source), "objects": records, "images": images}, indent=2))


if __name__ == "__main__":
    main()
