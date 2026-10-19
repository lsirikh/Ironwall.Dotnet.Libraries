"""실제 안양 장면 재기(읽기만) — python measure_real.py <장면 폴더> <이름> <truth.json 출력>
① 지형 네 모서리: GLB 정점을 뷰어처럼 sceneYaw 로 돌린 세계 좌표 vs pyproj 측지선으로 구한 회전 사각형 모서리의 진짜 ENU
② OSM 건물(바닥이 볼록한 것): GLB 건물 정점(세계)의 볼록 껍질 가운데 vs scene.json 건물 center 위경도의 진짜 ENU
③ 오버레이 자리 비교용 진실값 JSON(truth_eval.mjs 가 수정 전/후 enuOf 로 잰다) — 모서리 · 발전소 사진 모서리 · 건물 가운데 · 높이
"""
import json
import math
import sys
from pathlib import Path

import numpy as np
import trimesh
from pyproj import Geod, Transformer
from shapely.geometry import MultiPoint

G = Geod(ellps="WGS84")
TO_ECEF = Transformer.from_crs("EPSG:4979", "EPSG:4978", always_xy=True)


def enu(o, lon, lat, h=None):
    lo, la = math.radians(o["lon"]), math.radians(o["lat"])
    x0 = np.array(TO_ECEF.transform(o["lon"], o["lat"], o["height"]))
    p = np.array(TO_ECEF.transform(lon, lat, o["height"] if h is None else h)) - x0
    e = np.array([-math.sin(lo), math.cos(lo), 0.0])
    n = np.array([-math.sin(la) * math.cos(lo), -math.sin(la) * math.sin(lo), math.cos(la)])
    return float(p @ e), float(p @ n)


def main(folder, name, out):
    folder = Path(folder)
    s = json.loads((folder / "scene.json").read_text(encoding="utf-8"))
    o, r = s["origin"], s.get("rect")
    rot = r["rotation_deg"] if r else 0.0
    yaw = -math.radians(rot)
    glb = trimesh.load(folder / "scene.glb", force="scene")

    def world(v):
        return np.stack([v[:, 0] * math.cos(yaw) + v[:, 2] * math.sin(yaw), -v[:, 0] * math.sin(yaw) + v[:, 2] * math.cos(yaw)], -1)

    T = glb.graph.get(glb.graph.geometry_nodes["terrain"][0])[0]
    tv = trimesh.transform_points(glb.geometry["terrain"].vertices, T)
    tw = world(tv)
    samples, report = [], {"name": name, "rotation_deg": rot}
    # ① 모서리 — 측지선(방위 rot + atan2(x, y), 거리)
    w, h = (r["width_m"], r["height_m"]) if r else (None, None)
    corner_d = []
    if r:
        for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
            x, y = sx * w / 2, sy * h / 2
            lon, lat, _ = G.fwd(r["lon"], r["lat"], rot + math.degrees(math.atan2(x, y)), math.hypot(x, y))
            e, n = enu(o, lon, lat)
            d = np.hypot(tw[:, 0] - e, tw[:, 1] + n)
            i = int(d.argmin())
            corner_d.append(round(float(d[i]), 3))
            samples.append({"what": f"terrain corner {sx},{sy}", "lon": lon, "lat": lat, "local": tv[i].tolist()})
    report["terrain_corner_vs_geodesic_m"] = corner_d
    # ② 건물
    bd = []
    for b in s.get("buildings", []):
        verts = []
        for node in glb.graph.nodes_geometry:
            Tn, gname = glb.graph.get(node)
            if gname.startswith(f"building_{b['id']} ") or gname == f"building_{b['id']}":
                verts.append(trimesh.transform_points(glb.geometry[gname].vertices, Tn))
        if not verts or not b.get("center"):
            continue
        v = np.concatenate(verts)
        hull = MultiPoint([tuple(p) for p in world(v)]).convex_hull
        if not b.get("area_m2") or abs(hull.area / b["area_m2"] - 1) > 0.01:
            continue   # 오목한 바닥 — 껍질 가운데 ≠ 바닥 무게중심이라 재는 값이 아니다(볼록한 건물만)
        e, n = enu(o, *b["center"])
        c = hull.centroid
        bd.append(math.hypot(c.x - e, c.y + n))
        if len(bd) <= 40:
            # 건물 가운데 로컬(돌리기 전) — 껍질 가운데를 되돌린다
            cx, cz = c.x * math.cos(-yaw) + c.y * math.sin(-yaw), -c.x * math.sin(-yaw) + c.y * math.cos(-yaw)
            samples.append({"what": f"building {b['id']}", "lon": b["center"][0], "lat": b["center"][1], "local": [cx, 0.0, cz]})
    report["buildings"] = len(bd)
    report["building_center_vs_lonlat_m"] = {"median": round(float(np.median(bd)), 3), "p90": round(float(np.percentile(bd, 90)), 3), "max": round(float(max(bd)), 3)} if bd else None
    # ③ 높이 — 무작위 지형 정점의 (세계 → 측지 역산 위경도, y)
    heights = []
    rng = np.random.default_rng(7)
    for i in rng.choice(len(tv), 80, replace=False):
        e, n = tw[i, 0], -tw[i, 1]
        lon, lat, _ = G.fwd(o["lon"], o["lat"], math.degrees(math.atan2(e, n)), math.hypot(e, n))
        heights.append({"lon": lon, "lat": lat, "y": float(tv[i, 1])})
    nx, ny = s["terrain"]["grid"]
    truth = {"name": name, "scene": {"origin": o, **({"rect": r} if r else {}), "bbox": s["bbox"]}, "samples": samples,
             "grid": [nx, ny], "terrain": [round(float(x), 4) for x in tv.ravel()], "heights": heights}
    Path(out).write_text(json.dumps(truth), encoding="utf-8")
    print(json.dumps(report, ensure_ascii=False))


if __name__ == "__main__":
    main(*sys.argv[1:4])
