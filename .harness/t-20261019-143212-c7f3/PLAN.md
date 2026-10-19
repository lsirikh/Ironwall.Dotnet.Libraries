# PLAN — 지도 기울이기(map-tilt-25d) 기능 제거

## 목표
사용자 요청(2026-10-19): 툴바 지도 기울이기 단추(PerspectiveMore) "이 기능은 제거해도될 것 같다. 현재 동작도 안되고".
기울이기를 켜는 사용자 길을 전부 없애고, 저장된 옛 설정이 있어도 항상 0° · 꺼짐. 전용 코드는 안전한 만큼 지우고, 시험은 "제거됨" 단언으로.

## 진행
- [x] MapView.xaml: Ctrl+Shift+T KeyBinding · 보기 메뉴 「지도 기울이기」(+앞 구분선) · 툴바 ④ 기울이기 토글 + 각도 Popup + 각도 단추 · 축척 옆 배지 · MainMap Height/Margin 오버스캔 MultiBinding · TiltOverscanConverter 자원 · 쓰지 않게 된 xmlns:helpers 제거. ④ 묶음 = 회전 토글 + "?" 만.
- [x] GMapCustomControl.cs: Ctrl(+Shift)+↑/↓ 기울기 키 처리 제거(꺼져 있을 때와 같은 동작 — 키는 소비하지 않고 통과).
- [x] GMapCustomControl.Tilt.cs: ToggleTiltFeature · StepTiltAngle · TILT_STEP_COARSE/FINE 제거, 주석 갱신(엔진은 남기되 항상 OFF).
- [x] MapViewModel.Tilt.cs 삭제(명령 · 팝업 상태 · 배지 · 복원/저장), 호출부(MapViewModel.cs 2곳 · Anchor.cs 3곳) 제거.
- [x] Utils/TiltUiText.cs · Helpers/TiltOverscanConverter.cs 삭제, MapSettingsHelper.SaveMapTiltAsync 삭제, GMaps.Ui.Tests.csproj 링크 제거.
- [x] MapsHelp: Map.Toolbar.RotateTilt 제목 「지도 회전」 · 기울이기 절 삭제(키 이름은 유지).
- [x] 시험(라이브러리): RotationTiltScenarioTests MAP-024~028 · MapNavRemainingScenarioTests MAP-021/022/023/029 · MapToolbarScenarioTests(④ 토글 4개 · 저장값 true 여도 평면 + 옛 키 미기록) · MapTiltWiringTests 2건 · MapViewTiltOverscanBindingTests · TiltOverscanConverterTests · TiltUiTextTests · MapsHelpTests → "제거됨" 단언.
- [x] 기존 결함(기울이기와 무관, 완료 판정 대상 프로젝트라 같이 고침): MapAux OverlayHelpPlaybackScenarioTests 의 끌기 도우미(OverlayHeaderDrag 공용화 반영) · MapNav 지도 전환 시험의 미초기화 VM 필드(_map3DScenes · _map3DAreaMarkers).
- [x] 메인 앱(복사본에서 수정 · 시험 → host.patch, `git apply --check` 통과): Dotnet.Monitoring.Solution.Tests/Maps/MapToolbarScenarioTests(24건 통과) · UiTests TiltRuntimeDiagTests(제거 확인 실기 시험으로 축소) · MapSurfaceR27 · LineDrawingHudScaleDiag · MapComponentR24 · UiToast 문구. UiTests 컴파일 + MapTiltGuardTests 34건 통과(실기 UI 시험은 미실행).
- [x] 6개 시험 프로젝트 직접 통과(GMaps.Ui 1472 · MapNav 48 · Housing 353 · MapAux 76 · PropertyPanel 317 · MapEdit 389).
- [x] `dotnet test Ironwall.Dotnet.Libraries.sln` 3회: ① MSBuild OutOfMemory(환경) ② CameraPopup SupervisorSurvivalTests 1건(native-access-violation 재시작 3초) ③ 같은 묶음의 다른 1건(host hangs) — 나머지 전부 통과. 실패한 두 건은 단독 재실행에서 통과(12/12 ×2, 1/1). 카메라 팝업 코드는 이번에 건드리지 않았다(부하 시 타이밍 민감 시험).

## 발견
- tests/GMaps.* 는 .sln 에 없어 이전 작업들의 검사에서 빠졌고, 그 사이 기울이기와 무관한 실패 2종이 쌓여 있었다(위 "기존 결함").
- `git archive` 사본은 서브모듈 · 무시 파일(서명 키 등)이 빠져 빌드되지 않는다 — 기준선 비교에 쓸 수 없음.
- Git Bash `sed -i` 는 CRLF 를 LF 로 바꾼다 → 줄끝을 지키는 파이썬 편집 도구(tmp/ed.py)로만 고쳤다.
- MapNav 의 설정 파일 영속 시험(ZoomView · HomeAnchor)은 빌드 직후 부하에서 한 번 시간 초과로 실패, 이어 2회 연속 통과(타이밍 민감).

## 남은 일
- 메인 앱에 `git apply .harness/t-20261019-143212-c7f3/host.patch`(코디네이터). 실기 UI 시험(TiltRuntimeDiagTests 등)은 미실행.
