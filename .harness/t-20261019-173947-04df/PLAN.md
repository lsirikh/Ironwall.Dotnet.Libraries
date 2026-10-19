# 회전된 3D 영역 — PLAN

## 목표
지도가 돌아간 상태에서 그린 3D 영역을 화면 사각형 그대로(중심 · 가로 · 세로 · 회전각) 저장 · 굽기 · 표시 · 판정.
회전 0 이면 지금과 똑같이(북쪽 정렬 bbox, 옛 영역 · 옛 장면 호환).

## 정한 것(자세히는 DECISIONS.md)
- 영역 표현 = `rect: {lon, lat, width_m, height_m, rotation_deg}` + 감싸는 `bbox`(옛 서버 호환). 회전 0 이면 rect 없음.
- map-3d 좌표계 = 회전 ENU(LocalFrame.rotation_deg, 원점 = 사각형 중심) — 장면 직사각형이 곧 회전 사각형. 뷰어는 모델 rotation.y = −회전각(camsync.sceneYaw).
- 위경도 ↔ 미터 = WGS84 곡률 반지름 등장방형(GIS Map3DRotRect = map-3d geo.RotRect, 같은 식).
- 바로 들어가기(「이 영역 3D 로 보기」 · 「3D 로 보기」 단추)는 영역 회전각, 줌 게이트는 지금 방위.
- 2D 테두리는 모든 영역을 네 모서리 다각형으로.

## 진행
- [x] map-3d(작업 복사본 `artifacts/map3d-work` — git 무시 폴더, 원본 무변경): geo(RotRect · parse_rect · scene_contains · LocalFrame 회전) · terrain(build_grid rect) · pipeline(rect 경로: 원점 · 축 · 직사각형 · 건물/빈 곳 채우기/오버레이/높이 덮어쓰기 범위 · scene.json rect/rotation_deg/frame) · server(JobIn.rect · scenes/at 회전 사각형 판정 · rect 반환) · jobs(목록 rect) · heights(reset_all 범위) · CLI `--rect` · 뷰어(view.js 모델 회전 · 처음 시점/축소 한계 회전 사각형 · overlay-geom 높이표 yaw)
- [x] map-3d 시험: tests/test_rotated_area.py(16) + rotated_check.mjs → 전체 pytest 210 통과(기존 194 + 16)
- [x] `.harness/t-20261019-173947-04df/map3d.patch`(원본에 `git apply --check` 통과)
- [x] 안양 45° 회전 영역 CLI 굽기(오프라인 캐시 복사본) — 건물 73(지도 69 · 사진 4), 범위 밖 0(최대 0.009 m), GLB 로컬 ±240 m 정확. 감싸는 bbox 굽기 186동(중심이 사각형 안 70 · 밖 116 · 테두리 걸침 23)
- [x] 수정 전/후 증명: `artifacts/bake/proof_scene_at.py` — 원본은 회전 사각형 밖 점에 200(틀림), 수정본 404
- [x] GIS: Utils/Map3DRotRect.cs(Map3DRotRect · Map3DArea) · Map3DMath(AreaFromScreenCorners · SceneContains · EnterBearing) · Map3DGuide.AreaDrawLabel(Map3DArea) · Map3DBake.BuildBody(rect) · Map3DClient(rect 보내기 · 읽기, Map3DScene/SceneInfo.Rect) · GMapCustomControl(LastAnchorCorners · AnchorDrawCornersLabel) · VM(그리기 라벨 · 그린 영역 · Enter · 굽기 · 장면 찾기 · 영역 보기 · 다시 만들기) · Map3DAreaMarker(다각형 테두리)
- [x] GIS 시험: GMaps.Ui.Tests/Map3DRotatedAreaTests(순수 함수) · GMaps.MapNav.Tests/Map3DRotatedAreaScenarioTests(창 없는 실제 지도 VM 시나리오)
- [ ] 완료 판정: sln + GMaps.Ui · MapEdit · MapNav 통과 확인, 찌꺼기 정리

## 기준선(작업 전)
GMaps.Ui.Tests 1497 · GMaps.MapEdit.Tests 451 · GMaps.MapNav.Tests 48 모두 통과. map-3d pytest 194 통과.

## 발견
- 구(球) 등장방형과 LocalFrame(타원체 ENU)은 0.1~0.3 % 어긋난다 → RotRect 를 곡률 반지름으로(시험이 잡음).
- GMaps.MapEdit GroupRotationScenarioTests 의 되돌리기 시험 1개가 CPU 부하(굽기 동시 실행) 중 한 번 실패 — 단독 3회 통과(이 작업과 무관한 경로).
- Edit 도구는 작업트리 밖(%TEMP%)에 못 쓴다 → map-3d 작업 복사본을 작업트리의 git 무시 폴더 `artifacts/` 에 둠. `%TEMP%\map3d-t-20261019-173947-04df` 는 처음 만든 빈 복사본(바깥이라 지우지 않음).

## 남은 일
- 실기 미확인: map3d.patch 를 map-3d 에 적용하고 GIS 에서 회전 지도로 영역 그리기 → 굽기 → 3D 진입 · 2D 테두리 확인.
