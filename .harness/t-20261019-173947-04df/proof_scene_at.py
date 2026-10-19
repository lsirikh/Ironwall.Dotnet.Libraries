"""수정 전/후 증명 — 회전 장면(scene.json rect 45°)의 감싸는 bbox 모서리(회전 사각형 밖)를 /api/scenes/at 으로 묻는다.
기대: 404(그 지점을 덮는 장면 없음). 수정 전 map-3d 는 bbox 로만 판정해 200(장면 있음 — 틀림)."""
import json
import sys
import tempfile
from pathlib import Path

from fastapi.testclient import TestClient

import map3d
from map3d.config import Settings
from map3d.server import create_app

rect = {"lon": 126.96, "lat": 37.38655, "width_m": 480.0, "height_m": 480.0, "rotation_deg": 45.0}
bbox = [126.9561574, 37.3834818, 126.9638426, 37.3896182]
with tempfile.TemporaryDirectory() as d:
    (Path(d) / "jobs" / "rot").mkdir(parents=True)
    (Path(d) / "jobs" / "rot" / "scene.json").write_text(json.dumps({"created": "2026-10-19", "bbox": bbox, "rect": rect}), encoding="utf-8")
    c = TestClient(create_app(Settings(mbtiles_path="none", data_dir=d)))
    corner = c.get("/api/scenes/at", params={"lon": bbox[2] - 2e-5, "lat": bbox[3] - 2e-5}).status_code
    center = c.get("/api/scenes/at", params={"lon": rect["lon"], "lat": rect["lat"]}).status_code
print(f"map3d={Path(map3d.__file__).parent} corner(outside rect)={corner} center={center}")
sys.exit(0 if (corner, center) == (404, 200) else 1)
