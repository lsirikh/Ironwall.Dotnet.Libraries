"""안양 45° 회전 영역 굽기 수치 — 건물 수 · 범위(사각형 밖 없음) · GLB 정점 범위 · 감싸는 bbox 굽기와 비교."""
import json
import sys
from pathlib import Path

import numpy as np
import trimesh
from shapely.geometry import Point, Polygon

from map3d.geo import LocalFrame, parse_rect

base = Path(sys.argv[1])
rot = json.loads((base / "anyang45" / "scene.json").read_text(encoding="utf-8"))
box = json.loads((base / "anyang_bbox" / "scene.json").read_text(encoding="utf-8"))
rr = parse_rect(rot["rect"])
poly = Polygon(rot["rect"]["corners"])
frame = LocalFrame(rot["origin"]["lon"], rot["origin"]["lat"], rot["origin"]["height"], rot["rotation_deg"])


def local(lon, lat):
    x, y, _ = frame.lonlat_to_enu(np.asarray(lon), np.asarray(lat), frame.h0)
    return x, y


worst = 0.0
for b in rot["buildings"]:
    c = np.asarray(b["footprint"])
    x, y = local(c[:, 0], c[:, 1])
    worst = max(worst, float(np.max(np.abs(x)) - rr.width_m / 2), float(np.max(np.abs(y)) - rr.height_m / 2))
centers_in = sum(1 for b in box["buildings"] if rr.contains(*Polygon(b["footprint"]).centroid.coords[0]))
crossing = sum(1 for b in box["buildings"] if poly.intersects(Polygon(b["footprint"])) and not poly.contains(Polygon(b["footprint"])))
g = trimesh.load(base / "anyang45" / "scene.glb", force="scene")
v = np.vstack([m.vertices for m in g.dump()])
terrain = [m for n, m in g.geometry.items() if "terrain" in n.lower()]
print(json.dumps({
    "rect": rot["rect"] | {"corners": None}, "bbox": rot["bbox"], "size_m": rot["size_m"], "enu_rect": rot["enu_rect"],
    "rotated": {"buildings": rot["counts"]["buildings"], "by_origin": rot["counts"]["buildings_by_origin"],
                "terrain_grid": rot["terrain"]["grid"], "vertices": rot["counts"]["vertices"], "triangles": rot["counts"]["triangles"],
                "glb_mb": round(rot["files"]["glb_bytes"] / 1e6, 2),
                "footprint_max_outside_m": round(worst, 3),
                "glb_x_range": [round(float(v[:, 0].min()), 2), round(float(v[:, 0].max()), 2)],
                "glb_z_range": [round(float(v[:, 2].min()), 2), round(float(v[:, 2].max()), 2)],
                "terrain_meshes": len(terrain)},
    "enclosing_bbox": {"buildings": box["counts"]["buildings"], "size_m": box["size_m"],
                       "buildings_center_in_rect": centers_in, "buildings_crossing_rect_edge": crossing,
                       "buildings_center_outside_rect": box["counts"]["buildings"] - centers_in},
}, ensure_ascii=False, indent=2))
