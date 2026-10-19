# PLAN — t-20261019-183759-f6fc

## 목표
사용자 보고(2026-10-19 18:3x) 두 건
1. 3D 가운데 "불러오지 못했습니다: TypeError: Cannot read properties of undefined (re…" + 옛 장면 「건물 높이」 판 — 지운 job 을 GIS 가 계속 들고 있었다.
2. 지도 회전 때 3D 영역 테두리 · 배지가 회전을 따르지 않고 팬해야 고쳐진다.

## 원인(근거)
- 뷰어(map-3d view.js loadScene): `await (await fetch(scene.json)).json()` — 404 본문 `{"detail":"Not Found"}` 을 장면으로 읽고 `fillInfo()` 의 `s.size_m[0]` 에서 TypeError(보고 글 "Cannot read properties of undefined (reading '0')" 와 같은 꼴).
- GIS 「지우기」(HandleAsync)는 `RemoveMap3DArea`(목록 · 배지 · 장면 캐시)만 뺐다. 3D 호스트(Map3DViewHost)는 지운 job 을 `_job` · `_ready` 로 그대로 들고 있었고(재사용 reuse=True · 네이티브 창 전환 `SwitchToHwnd` 가 `_job` 주소를 다시 연다), `_map3DJob` · `_bhJob`(건물 높이 판) · `_map3DCenterScene`(「3D 로 보기」 대상)도 남았다.
  지우기 전에 보낸 `/api/scenes/at` · 목록 요청의 늦은 응답이 지운 장면을 캐시에 되살릴 수도 있었다.
  → 수정 전 시험 실측: "옛 job 참조 남음: _bhJob, _map3DCenterScene, _map3DJob, Map3DHost".
- 「다시 만들기」는 새 job 을 만들 뿐 옛 job 이 남아(서버 목록 · 배지 두 개), 굽기 직후엔 새 장면을 캐시 **뒤**에 붙여 겹친 자리에서 옛 장면이 먼저 잡혔다.
- 회전: 테두리는 `QueueMap3DGate`(위치 · 줌 이벤트)에서만 다시 그렸다. 회전은 그 이벤트를 내지 않는다(벤더 Bearing setter 는 마커 자리만 옮김 → 왼쪽 위 모서리 기준 다각형은 옛 방위 모양).
  → 수정 전 시험 실측: "방위 30: 꼭짓점 1 373,0 ≠ 323,-187", 연속 회전 뒤 다시 그린 횟수 0.

## 진행
- [x] map-3d(작업 복사본 `artifacts/map3d-work` — git 무시 폴더, 원본 무변경): `static/sceneload.js`(fetchSceneJson · classifyLoadError · loadFailure, 쉬운 말 3종) · view.js 연결(실패 → 안내 글 + `post({type:"error", reason, job, detail})`)
- [x] map-3d 시험: `tests/sceneload_check.mjs` + `tests/test_viewer_load.py`. 원래 view.js 로는 연결 검사 5개 실패 → 수정 뒤 통과
- [x] `.harness/t-20261019-183759-f6fc/map3d.patch`(원본에 `git apply --check` 통과 — Bash 로 만들 것: PowerShell 파이프는 한글을 깨뜨린다)
- [x] GIS: `RetireMap3DJob`(MapViewModel.Map3DAreas) — 보던 3D/준비 중이면 2D 로 + 같은 줌 재진입 보류 + 안내, 호스트 `Unload()`(빈 화면 · job 비움), 오버레이 기록, 건물 높이 판 · 판 되돌리기, 「영역 보기」 진입, 화면 가운데 영역, 장면 캐시 · 목록 · 배지. 지운 job 기억(`Map3DGoneJobs`) → 늦은 목록 · 장면 조회가 되살리지 않음
- [x] 호출: 지우기 확인 · 서버 목록에서 빠짐 · 3D 화면 error(scene-missing) · 다시 만들기 완료(`ReplaceMap3DJobAsync` — 옛 job DELETE)
- [x] 굽기 완료 장면은 캐시 맨 앞(겹치면 새 장면 먼저)
- [x] `Map3DMath.ParseMessage` "error" · `Map3DViewHost` error → 실패 표시, 다른 job 의 늦은 ready/rendered 무시
- [x] 회전: `OnViewportSnapshotForVm`(회전 동기 계약 FR-05, 프레임당 1회) → `OnMap3DAreaBearingChanged` → 방위가 바뀌었을 때만 `QueueMap3DGate`(프레임당 1회)
- [x] GIS 시험 `tests/GMaps.MapEdit.Tests/Map3DStaleJobScenarioTests.cs` 16건 — 수정 전 13 실패 → 수정 후 16 통과
- [x] 전체: map-3d pytest 211 통과(기존 210 + 1) · dotnet test sln 통과 · GMaps.Ui.Tests 1524 · MapEdit.Tests 491 · MapNav.Tests 60 통과
  (MapNav 첫 실행은 "테스트 호스트 작동 중단" — --blame 재실행 · 다시 실행 두 번 모두 60/60, 재현 안 됨: 개발 PC 무작위 크래시로 봄)

## 남은 일
- 실기 미확인: map3d.patch 를 map-3d 에 적용하고(서버 재시작은 사용자) GIS 에서 지우기 · 다시 만들기 · 회전 확인.
