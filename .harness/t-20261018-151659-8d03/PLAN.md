# PLAN — 3D 나가기 단추 · 영역 전체 처음 시점 (t-20261018-151659-8d03)

## 목표
사용자 요청(2026-10-18): 3D 영역은 줌 개념이 없으니 「나가기」 단추로 나가게, 처음 들어갔을 때 영역 전체가 다 들어오게.
1. 「이 영역 3D 로 보기」 진입 → 영역(scene bbox) 전체가 들어오는 비스듬한 시점(뷰어 순수 함수 + node 시험). 줌 게이트 진입은 지금처럼 2D 자리 이어받기.
2. 3D 가 보이는 동안 「나가기」 단추(GMaps.Map3D.ExitButton) = ESC 경로. 영역 보기 진입은 들어오기 전 2D 자리로, 줌 진입은 3D 카메라를 2D 로.
3. 영역 보기 진입 3D 는 줌으로 2D 로 튀지 않는다(순수 함수 + 시험).

## 진행
- [x] 뷰어(map-3d 사본 `claude-skill-harness-map3d/` — .gitignore 의 `claude-skill-harness*/` 로 커밋 안 됨)
  - camsync.js `fitAreaView(bbox, fov, w, h, {pitch, bearing, margin})` — 모서리마다 닫힌 꼴 거리, 최대값
  - view.js `homeView(opts)` = fitAreaView 로 처음 시점(처음 시점 단추 · 가운데 더블클릭 · GIS `{type:"home"}`), 접혀 있으면 펼친 뒤(resize), 끝나면 camera(reason "home") 한 번 보냄. 끼워 넣기 첫 로드는 GIS 카메라(주소)만.
  - tests/camsync_check.mjs 에 fit 시험(네 모서리 여백 안 · 한 모서리 여백 선에 닿음 · 화면/영역 크기 단조)
  - `.harness/t-20261018-151659-8d03/map3d.patch` (git apply -R --check 확인)
- [x] GIS 순수 함수 Map3DMath: `ShouldLeave3D(zoom, exit, areaView)` · `ExitCamera(areaView, before, last3D)` · `HomeMessageJson(bearing, pitch, animate)`
- [x] VM(MapViewModel.Map3D.cs): `_map3DAreaViewJob` · `_map3DAreaViewReturn` · `IsMap3DAreaView`, `IsMap3DShown` · `ExitMap3DCommand` · `ExitMap3D(cause)`(ESC 도 여기로), Reveal3D 에서 영역 보기면 home 메시지, 카메라 메시지 줌 이탈 판정에 areaView
- [x] MapViewModel.Map3DAreas.cs ViewMap3DArea: 들어오기 전 2D 자리 저장(이미 영역 보기 중이면 처음 자리 유지)
- [x] MapView.xaml 「나가기」 HUD(오른쪽 위, MapFloatingPanelStyle + MaterialDesignOutlinedButton + PackIcon ExitToApp + "?" Map.Map3D.Exit) — 자기 블록만 더함
- [x] MapsHelp.cs `Map.Map3D.Exit` 한 항목만 더함
- [x] tests/GMaps.Ui.Tests/Map3DExitTests.cs (should_X_when_Y 11건)
- [x] dotnet build GMaps.Ui · GMaps.Ui.Tests(Map3D 382 통과) · map-3d node 5종 · pytest 84 통과
- [x] dotnet test Ironwall.Dotnet.Libraries.sln — 통과 2887 · 실패 1(CameraPopup EventWindowManagerTests 시간 예산, 기준선과 같은 수)
- [x] tests/GMaps.PropertyPanel.Tests 도움말 키 계약 시험(should_resolve_every_map_screen_help_key…) 통과
- 남은 찌꺼기: worktree 안 `claude-skill-harness-map3d/` · `claude-skill-harness-logs/`(.gitignore 대상, 커밋 안 됨) — 하네스 가드가 지우기를 막아 남겨 둠

## 발견
- 하네스 가드: 작업 폴더가 사본으로 바뀐 뒤엔 셸에서 worktree 안 파일 쓰기가 막힘 → patch 는 Write 도구로 썼다.
- 기본 3D 호스트는 WPF 안(CompositionControl)이라 WPF HUD 가 3D 위에 보인다. 네이티브 창 대체 모드에선 가려진다(ESC 만).

## 남은 일
- 실기 미확인(창 · WebView2 시험 금지). map-3d 에 patch 적용은 사용자/서버 쪽 몫(옛 뷰어는 home 을 몰라 눕히기만 된다).
