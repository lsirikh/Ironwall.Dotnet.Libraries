# PLAN — 3D 회전 영역에서 지형과 오버레이가 어긋나 보임(t-20261019-184431-1007)

## 목표
사용자 캡처(18:4x): 3D 에서 지형 · 건물(왼쪽)과 오버레이(마커 · 발전소 사진 · 기울어진 선 — 오른쪽, 검은 허공)가 서로 다른 자리.
원인을 수치로 규명 → 한 정의로 통일 → 독립 기준 시험(수정 전 실패 → 수정 후 통과) → 실제 안양 회전 영역 다시 굽기로 확인.

## 진행
- [x] 캡처 · 장면(f366a1) · GIS 로그 · 썸네일로 원인 규명 → REPORT.md §1
  - 캡처는 회전 장면이 아니라 **옛 장면 20261017-205808-5e0a01**(18:42:18 「장면 찾음」 → 18:42:20 진입, 방위 −30). 썸네일의 원형 공연장 = 캡처 왼쪽 지형.
  - 발전소 오버레이는 그 장면 원점에서 1009 m(방위 37.8°), 장면 모서리에서 233 m 밖 → 지형 없는 허공에 그려짐(뷰어가 장면 밖 오버레이를 걸러내지 않음).
  - 회전 수식(GIS Map3DRotRect · map-3d RotRect/LocalFrame · sceneYaw · makeHeightField)은 서로 맞음: GLB 지형 모서리 vs 측지선 진실값 0.003~0.004 m.
  - 대신 뷰어 enuOf 가 구(球) 반지름이라 오버레이 · 카메라가 지형과 0.3 % 어긋남 — 안양 모서리 1.37 m, 높이표 0.49 m.
- [x] map-3d 수정(복사본 artifacts/map3d → .harness/…/map3d.patch)
  - camsync.enuOf/lonLatOf: WGS84 묘유선 · 자오선 곡률 반지름(RotRect._k 와 같은 길이). view.js lonlatOf 는 camsync.lonLatOf 로.
  - overlay-geom.itemInArea + Overlay3D.refreshVisibility: 장면 영역(rect 또는 bbox) 밖 항목은 그리지 않음. view.js 가 ctx.area 제공.
  - tests/rotated_check.mjs 의 rect() 도우미를 RotRect 와 같은 타원체 식으로(주석은 같은 식이라 했는데 구였다).
- [x] 시험: map-3d tests/test_rotated_truth.py(0/30/65/90/−45°, 720×590 m, pyproj 측지선 · 자체 ENU · GLB 정점) + tests/overlay_area_check.mjs(실제 안양 자리 · Overlay3D 배선 · 진실값)
- [x] GIS 시험: tests/GMaps.Ui.Tests/Map3DRotRectParityTests.cs(map-3d RotRect 동치 · 측지선 · GMap 화면 시늉) — GIS 코드 변경 없음
- [x] 실제 안양 회전 영역 CLI 다시 굽기 → artifacts/rebake-anyang(무시 폴더), 수치 REPORT.md §3
- [x] map-3d pytest 215 통과(수정 전 210) · node 전부 통과 · GMaps.Ui.Tests 1538 · GMaps.MapEdit.Tests 475 통과
- [x] dotnet test Ironwall.Dotnet.Libraries.sln — exit 0, 10개 시험 dll 모두 통과(첫 하네스 검사는 이전 세션의 내 시험 실행이 남긴 testhost · CameraPopupHost 가 exe 를 잡고 있어 MSB3021 복사 실패 → 그 프로세스를 멈추고 다시 돌려 통과)

## 남은 일
- map3d.patch 를 map-3d 저장소에 적용(사람이 · 하네스 수락 때). 실기(GIS 안 3D)는 미확인.
- 옛 장면 찾기가 회전 영역 대신 옛 장면을 고른 것(18:42:18)은 동시 작업 t-20261019-183759-f6fc(옛 장면 참조) 범위라 손대지 않음.

## 파일
- .harness/t-20261019-184431-1007/map3d.patch — map-3d 변경(7 파일)
- .harness/t-20261019-184431-1007/measure_real.py · truth_eval.mjs — 실제 장면 재기 · 수정 전/후 증명 스크립트
- artifacts/(git 무시) — map3d(수정 복사본, vendor 는 원본에서 복사) · rebake-anyang(다시 구운 장면) · truth-*.json. 수정 전 비교용 클론 · 패치 확인 클론 · 캐시 복사본은 지움
