# PLAN — 3D 방향 링(앵글) · 이동 컨트롤러

## 목표
3D 표시 중 지도 오른쪽 위에 방향 링(정북 N · 끌어 방위 회전 · 가운데 구 손잡이 위아래 끌기 = 기울기 · 위에서 보기 · 기본 비스듬히) +
이동 단추(전진 · 후진 · 좌 · 우 · 확대 · 축소, 누르고 있으면 계속). 2D 에서는 기존 나침반.

## 작업 환경
- map-3d: `artifacts/map3d`(원본 C:\workspace_python\map-3d 의 git clone, vendor 는 원본에서 복사) → `git -C artifacts/map3d diff > .harness/t-20261019-221631-c0a7/map3d.patch`
- 시험: `dotnet test Ironwall.Dotnet.Libraries.sln` · `dotnet test tests/GMaps.Ui.Tests` 등 4개 · map-3d: `cd artifacts/map3d; C:\workspace_python\map-3d\.venv\Scripts\python.exe -m pytest -q -p no:cacheprovider`

## 설계
- 메시지(GIS → 뷰어): `{type:"camera.nudge", bearingDelta, pitchDelta, forward, right, zoom}`(forward/right = 카메라 거리 비율, zoom = 줌 단계) ·
  `{type:"camera.preset", name:"north"|"top"|"default"}`. 뷰어는 짧은 보간 뒤 `camera`(reason user) 회신 → 링 방위 갱신.
- 뷰어 순수 함수(camsync.js): `nudgeView` · `presetView` · `clampToArea`. 한계: 기울기 0~85(cameraFromMap), 거리 = OrbitControls min/max, 바라볼 점 = 장면 영역 안.
- GIS 순수 함수: `Utils/Map3DNavMath.cs`(링 각도 · 감기 · 끌기 판정 · 기울기 끌기 · 계속 이동 · 메시지 JSON).
- GIS 컨트롤: `Views/Maps/Map3DNavControl.xaml(.cs)`, VM: `MapViewModel.Map3DNav.cs`.

## 진행
- [x] 조사(Map3DViewHost.PostJson · view.js onHostMessage · camsync.js · 나침반 자리)
- [x] map-3d: camsync `nudgeView` · `presetView` · `clampToArea` + view.js `onNavMessage` + tests/nav_check.mjs + test_server.py → pytest 287 passed
  (수정 전 실패 증명: 원본 camsync/view.js 로 nav_check.mjs → export 없음 SyntaxError, 수정 후 "nav ok")
- [x] GIS 순수 함수 `Utils/Map3DNavMath.cs`(Map3DNudge · Map3DNavMove · Map3DNavDrag · Map3DNavMover) + tests/GMaps.Ui.Tests/Map3DNavMathTests.cs
- [x] VM `MapViewModel.Map3DNav.cs` · 컨트롤 `Views/Maps/Map3DNavControl.xaml(.cs)` · MapView 배선(나침반 → IsCompassShown) · MapsHelp `Map.Map3D.Nav`
  + tests/GMaps.MapEdit.Tests/Map3DNavScenarioTests.cs (수정 전 실패 증명: OnMap3DMessage 의 SetMap3DNavCamera 를 빼면 링 따라가기 시험 실패)
- [x] map3d.patch(.harness/t-20261019-221631-c0a7/map3d.patch)
- [x] 전체 시험: sln exit 0 · GMaps.Ui 1660 · MapEdit 676 · MapNav 67 · PropertyPanel 436 모두 실패 0 · map-3d pytest 287 passed

## 남은 일
- 실기(헤디드) 미확인 — 실제 3D 화면에서 링 끌기 · 이동 단추 체감 · map3d.patch 를 원본 map-3d 에 적용하는 것은 사용자 몫

## 추가 지시(v2.6 합친 뒤 실패 1건) — 2026-10-19 23시
- 합치기(e15105e2, t-20261019-221052-8a7a)는 이 작업의 파일(Map3DNav*)을 건드리지 않았다. 겹친 파일 MapViewModel.Map3D.cs 는 자동 병합(빈 땅 클릭 · 진입 보류 줄과 서로 다른 자리).
- 1회차: GMaps.MapEdit 685 중 GroupRotationEdgeScenarioTests 6건 실패(키 `]` 4번 → 기대 4°, 실제 0.4° = 0.1° × 4 — `MapViewModel.cs` 묶음 회전 키가 실제 키보드의
  `Keyboard.Modifiers` 에서 Shift 를 읽어 세밀 단계 0.1° 로 셈). 그 6건만 다시 → 42/42 통과, MapEdit 전체 다시 → 685/685 통과.
- sln 1회차: CameraPopup.Tests `EventWindowManagerTests.should_return_within_budget_and_not_throw_when_supervisor_is_unavailable` 1건 실패(가장 느린 호출 790 ms > 50 ms 예산).
  단독 3회 통과 · sln 다시 exit 0 — 시험 동시 실행 부하로 시간이 튄 것.
- 결론: 두 변경이 함께 깨뜨리는 결정적 실패는 재현되지 않음 → 코드 변경 없음(DECISIONS 8).
- 최종 재확인: sln exit 0 · GMaps.Ui 1642 · MapEdit 685 · MapNav 67 · PropertyPanel 436 실패 0 (로그 artifacts/logs2/)
