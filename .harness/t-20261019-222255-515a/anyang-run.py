"""실제 안양 영역(data/jobs/20261019-183014-f366a1 의 요청 그대로)에 가짜 직접 그린 건물 2채를 넣어 CLI(generate)로 다시 굽고 수치를 남긴다.
서버 · 브라우저 · 렌더 없음. 원본 data/ 는 읽기만 — 캐시는 이 폴더(data/cache)에 복사본, 결과는 out/."""
import json
import sys
import time
from pathlib import Path

from shapely.geometry import Polygon

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent / "map3d"))

from map3d import userbuildings as UB  # noqa: E402
from map3d.config import load_settings  # noqa: E402
from map3d.pipeline import GenerateRequest, generate  # noqa: E402

req_json = json.loads((HERE / "request.json").read_text(encoding="utf-8"))
before = json.loads((HERE / "before-scene.json").read_text(encoding="utf-8"))
settings = load_settings(data_dir=str(HERE / "data"))
scene = {"bbox": before["bbox"], "rect": before.get("rect")}
ov = req_json["overlays"][0]["bounds"]


def in_overlay(c):
    return ov["left"] <= c[0] <= ov["right"] and ov["bottom"] <= c[1] <= ov["top"]


# ① 사진(오버레이) 안 가장 큰 자동 건물 자리를 조금 넓게 다시 그린 건물(갈아 끼우기) — 옥상 · 외벽이 사진으로 꾸며지는지
#    (원래 장면에서 사진을 입은 건물 = 사진의 불투명 범위 안 — 그중 가장 큰 것)
target = max((b for b in before["buildings"] if b.get("photo") and b.get("footprint")), key=lambda b: b["area_m2"])
p = Polygon(target["footprint"]).buffer(0.00002, join_style=2).simplify(0.000005)
outline1 = [[round(x, 7), round(y, 7)] for x, y in list(p.exterior.coords)[:-1]]
# ② 오버레이 밖 빈 자리(자동 건물과 안 겹침) 30 × 20 m L 자 — 절차 외벽 · 기본 난간 · 옥탑 규칙
cx, cy = before["rect"]["lon"], before["rect"]["lat"]
kx, ky = 111320.0 * 0.7939, 110540.0
best = None
for i in range(-12, 13):
    for j in range(-12, 13):
        x0, y0 = cx + i * 20 / kx, cy + j * 20 / ky
        cand = Polygon([(x0, y0), (x0 + 30 / kx, y0), (x0 + 30 / kx, y0 + 10 / ky), (x0 + 12 / kx, y0 + 10 / ky),
                        (x0 + 12 / kx, y0 + 20 / ky), (x0, y0 + 20 / ky)])
        c = cand.centroid
        if in_overlay((c.x, c.y)) or not UB.scene_contains(scene, c.x, c.y):
            continue
        if any(cand.buffer(5 / kx).intersects(Polygon(b["footprint"])) for b in before["buildings"] if b.get("footprint")):
            continue
        best = cand
        break
    if best is not None:
        break
outline2 = [[round(x, 7), round(y, 7)] for x, y in list(best.exterior.coords)[:-1]]

for f in (HERE / "data" / "user-buildings").glob("*.json") if (HERE / "data" / "user-buildings").exists() else []:
    f.unlink()
UB.put(settings.data_dir, scene, "a1a1a1a1a1a1", outline1, 24.0, 8, "가짜 본관(사진 안)")
UB.put(settings.data_dir, scene, "b2b2b2b2b2b2", outline2, 15.0, 5, "가짜 경비동(사진 밖)")

req = GenerateRequest.from_json(req_json)
t = time.time()
out = HERE / "out"
sc = generate(req, settings, out)
dt = time.time() - t

users = [b for b in sc["buildings"] if b.get("user_uid")]
report = {
    "seconds": round(dt, 1),
    "buildings_before": len(before["buildings"]),
    "buildings_after": len(sc["buildings"]),
    "origin_before": before["counts"].get("buildings_by_origin"),
    "origin_after": sc["counts"].get("buildings_by_origin"),
    "user_buildings": sc["user_buildings"],
    "replaced_target": {"id": target["id"], "origin": target["origin"], "area_m2": target["area_m2"], "height": target["height"]},
    "users": [{k: b.get(k) for k in ("id", "user_uid", "origin", "height", "levels", "area_m2", "facade", "parapet", "roof_shape_built",
                                       "photo", "top_z", "ground_min")} | {"name": b.get("tags", {}).get("name")} for b in users],
    "sun": sc.get("sun"),
    "flatten": (sc.get("terrain") or {}).get("flatten"),
    "warnings": sc.get("warnings"),
    "glb_bytes": (out / "scene.glb").stat().st_size,
}
(HERE / "report.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps(report, ensure_ascii=False, indent=2))
