"""Validate exported enclosure STL scale, connectivity and closed surfaces."""
from __future__ import annotations

import argparse
import json
from pathlib import Path

import numpy as np
import trimesh


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("model", type=Path)
    args = parser.parse_args()
    output = args.model / "printable_enclosure"
    files = sorted((output / "STL_mm").glob("E-*.stl"))
    if len(files) != 6:
        raise ValueError(f"Expected 6 STL files, found {len(files)}")
    expected = [(72, 69, 22), (72, 69, 4.4)] * 2 + [(32, 48, 50), (32, 48, 4.4)]
    results = []
    for path, size in zip(files, expected, strict=True):
        mesh = trimesh.load_mesh(path, process=True)
        # A tiny union-find avoids optional SciPy/networkx graph dependencies.
        parent = list(range(len(mesh.faces)))
        def find(i: int) -> int:
            while parent[i] != i:
                parent[i] = parent[parent[i]]
                i = parent[i]
            return i
        for a, b in mesh.face_adjacency:
            ra, rb = find(int(a)), find(int(b))
            if ra != rb:
                parent[rb] = ra
        entry = {
            "file": path.name,
            "units": "mm",
            "size_mm": mesh.extents.tolist(),
            "z_min_mm": float(mesh.bounds[0, 2]),
            "closed": bool(mesh.is_watertight),
            "winding_consistent": bool(mesh.is_winding_consistent),
            "connected_solids": len({find(i) for i in range(len(parent))}),
            "volume_mm3": float(mesh.volume),
            "triangles": len(mesh.faces),
        }
        if not entry["closed"] or not entry["winding_consistent"] or entry["connected_solids"] != 1:
            raise ValueError(f"STL surface/connectivity failure: {entry}")
        if not np.allclose(mesh.extents, size, atol=0.02) or abs(entry["z_min_mm"]) > 0.02:
            raise ValueError(f"STL scale/print-orientation failure: {entry}")
        results.append(entry)
    report = {
        "passed": True,
        "count": len(results),
        "description": "Millimetre STL meshes; single closed solids; suggested bed orientation; Z minimum 0.",
        "parts": results,
    }
    (output / "STL_VALIDATION.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
