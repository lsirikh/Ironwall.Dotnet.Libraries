# PLAN — 3D 진입 시점 · 줌으로 안 나감 · 3D 마커 보임 · 「나가기」 버그/자리/디자인 (t-20261018-180539-3f13)

## 목표
사용자 요청(2026-10-18): 3D 로 바뀔 때 너무 확대돼 들어온다 → 처음엔 전체가 보이게. 나가기 버튼 왼쪽으로, 3D 가 아닌데 떠 있는 버그 수정, 제대로 디자인.
+ 3D 에서 오버레이 마커가 2D 와 같게(줌 때문에 숨지 않게).

## 진행
- [x] 원인 증거(앱 로그 `Dotnet.Monitoring.Solution\bin\Debug\net8.0-windows10.0.17763.0\Logs\log-2026-10-18.txt`, 읽기만)
  - "3D 표시" ↔ "2D 복귀" 는 늘 짝이 맞는다(VM 상태가 Shown 으로 남은 기록은 없음).
  - 18:02:05 나가기 → 18:02:11 같은 영역 재진입(zoom 18) 등 — 나간 뒤 줌을 내렸다 올려 줌 게이트로 다시 들어오면 2D 자리 그대로(확대된 채) 3D 가 열렸다 = 요구 1 의 "너무 확대돼서 전환".
- [x] 뷰어(map-3d 사본 `claude-skill-harness-map3d/`, 기준 사본 `claude-skill-harness-map3d-base/` — 둘 다 .gitignore `claude-skill-harness*/`)
  - overlay-store.js `shown3D(item, zoom)` → `{body, label}`: 몸체는 visible=false 만, 이름표만 minZoom 규칙(isShown)
  - overlay3d.js `refreshVisibility` 가 group.visible = body, label.visible = label
  - view.js `refreshOverlayZoom` 은 접힌 화면(높이 < 100 px)에서 줌을 셈하지 않음 + resize 때 다시 셈
  - view.js 휠: 최대 거리에서 멈춤(끼워 넣기도) — GIS 가 줌으로 나가지 않으므로
  - 주석: home 은 모든 진입에서 부름
  - tests/overlay_check.mjs 에 shown3D 6건
  - `.harness/t-20261018-180539-3f13/map3d.patch` (기준 사본에 `git apply --check` 통과 · 원본 map-3d 에 직접 적용은 하네스가 막아 못 봄 — 기준 사본은 원본 HEAD f83f6de 복사)
- [x] GIS 순수 함수(Map3DMath, 더하기만): `ExitCamera(before3D, last3D)` · `EnterViewMessageJson(bearing)` · `ShouldSync2DToCamera(hostMode, cam)` · `StatusText(shown)` · `IsExitButtonVisible(status)`
- [x] VM(MapViewModel.Map3D.cs · Map3DAreas.cs)
  - `SetMap3DState` 하나로 상태 변경 + Map3DStatusText · IsMap3DShown 함께 알림, IsMap3DShown 은 Map3DStatusText 에서만 파생
  - Reveal3D: 모든 진입에 home(EnterViewMessageJson), `_map3DReturn ??= 지금 2D`(영역 보기는 누르기 전 자리)
  - 카메라 메시지: 줌 이탈 판정 제거(ShouldSync2DToCamera 로 2D 따라가기만)
  - ExitMap3D · 입력 모드 · 설정 끔 · 로딩 실패 · 영역 지움 · 호스트 대체 → `Map3DExitCamera`(들어오기 직전 자리 ?? 3D 마지막)
  - Leave3D: MainMap 이 없어도 상태는 먼저 내림
  - 호스트 대체(CompositionControl → 네이티브 창, 호스트가 접힘)인데 Shown 이면 2D 로
  - `IsMap3DAreaView` · `_map3DAreaViewReturn` → 쓰지 않게 되어 제거/`_map3DReturn` 으로 이름 바꿈
- [x] Map3DViewHost: 렌더 프로세스 종료(ProcessFailed RenderProcessExited) → LoadFailed(멈춘 3D 에 "3D"로 남지 않게, 다음 진입은 다시 연다)
- [x] MapView.xaml: 「나가기」를 왼쪽 위 좌표 칩 왼쪽(같은 가로 줄)으로, PanelPrimaryButtonStyle + ArrowLeft + "나가기" + Esc 키 칩 + "?" — AutomationId 그대로
- [x] MapsHelp.cs `Map.Map3D.Exit` 문구를 새 규칙으로
- [x] tests/GMaps.Ui.Tests/Map3DEnterViewTests.cs (UTF-8 BOM, should_X_when_Y)
- [x] 검증: dotnet build GMaps.Ui · dotnet test tests/GMaps.Ui.Tests(1254 통과) · node tests/*.mjs · pytest 110 통과
- [x] dotnet test Ironwall.Dotnet.Libraries.sln — 결과는 아래 「검증」

## 발견
- 하네스 가드: map-3d 원본에 git(archive · apply)은 막힘 → 추적 폴더를 cp 로 사본 둘(작업 · 기준)에 복사, 패치는 diff -u(a/ b/ 접두)로.
- pytest 는 원본 .venv 파이썬으로 사본 폴더에서 돌리면 사본 코드를 쓴다(pyproject pythonpath ".").
- 버튼 버그: 정적 경로로 찾은 "상태는 3D 인데 3D 가 안 보이는" 길 = ① 호스트 대체(SwitchToHwnd 가 호스트를 접고 다시 엶) ② Leave3D 의 MainMap null 조기 반환 ③ 렌더 프로세스 종료. 로그엔 셋 다 안 보여, 남은 가능성은 원격(RDP) 저사양에서 펼친 직후 3D 첫 그림이 늦는 동안(2D 가 비침) — 실기 확인 필요(코디네이터).

## 남은 일
- 실기 확인(창 · WebView2 시험 금지): 줌 게이트 진입이 영역 전체 시점으로 열리는지 · 나가기 왼쪽 위 · 라이트/다크 · 3D 마커 보임.
- map-3d 에 map3d.patch 적용(코디네이터/사용자 몫).
- 찌꺼기: worktree 안 `claude-skill-harness-map3d/` · `claude-skill-harness-map3d-base/`(.gitignore 대상, 커밋 안 됨).

## 검증
- `dotnet build Ironwall.Dotnet.Libraries.GMaps.Ui` — 오류 0
- `dotnet test tests/GMaps.Ui.Tests` — 1254 통과(새 Map3DEnterViewTests 포함)
- map-3d 사본: `node tests/*.mjs` 6종 ok(extract_view_check 는 pytest 가 인자 주고 부름) · `.venv\Scripts\python.exe -m pytest -q` 110 통과
- `dotnet test Ironwall.Dotnet.Libraries.sln` — 1회차 CameraPopup.Tests 1 실패(시간 예산 시험, 단독 재실행 334 통과 — 이전 작업 기록과 같은 흔들림), 2회차 전부 통과
