# PLAN — 3D 화면에서도 2D 와 같은 오버레이 창(카메라 상자 · 조립 카드 등) + 정확한 자리 + 3D ↔ 2D 전환 유지

## 목표
사용자 요청(2026-10-18): 3D 렌더링 상태에서 오버레이 마커를 누르면 2D 와 같은 창이 같은 기능으로 뜨고, 카메라 상자는 마커가 3D 에 보이는 자리에 정확히 붙으며,
3D ↔ 2D 로 넘어가도 창이 닫히지 않고 같은 마커의 새 자리로 옮겨 보인다.

## 1. 전수 목록 — 2D 지도에서 마커 · 심볼 · 이벤트로 뜨는 창

| # | 창 · 팝업 | 여는 경로 | 2D 위치 계산 | 작업 전 3D | 작업 뒤 3D (WPF 안 3D = composition, 기본) |
|---|---|---|---|---|---|
| 1 | 카메라 영상 상자 `CameraStreamPopupViewModel` / `CameraStreamPopupControl` (PropertyPanelCanvas, ZIndex 200) | 카메라 심볼 더블클릭 → `OnMapMarkerDoubleClicked` → `CameraPopupDoubleClick.Handle` → `OpenSelfCameraPopup` → `OpenCameraStreamPopupAsync` | 좌상단 = 위경도 앵커 `AnchorGeo`(저장 위치 또는 심볼 + 오른쪽 100 · 위 100 + 높이). `RefreshCameraPopupPositions` 가 팬 · 줌 · 디지털 줌 · 회전 · 크기 바뀜마다 `FromLatLngToLocal` → `InnerToOuter`. 화면 안 고정 없음(FR-A1), 캔버스 클립만 | 2D 로 돌아온 뒤 열림 | **3D 유지**. 마커가 3D 에 보이는 자리(viewer `overlay.screen`) + 2D 오프셋(앵커 − 마커)을 3D 줌 · 방위로 키우고 돌린 자리. 3D 카메라를 움직이면 따라감. 3D 에서 끌면 2D 앵커로 되돌려 저장 |
| 2 | 조립 카드 `ComponentCardViewModel` (`ComponentCardAdorner`, 지도 어도너 층) | 운영 모드 심볼 클릭(누름 → 뗌 데드존) → `ComponentCardService.Show` | 아이콘 화면 중심 오른쪽(자리 없으면 왼쪽) · 화면 안 가둠(`ComponentCardPlacement`) · 지도 변경마다 다시 배치 | 2D 로 돌아온 뒤 열림(어도너 층이 3D 밑) | **3D 유지**. 같은 VM 을 3D 위 층(`Map3DViewHost.OverlayLayer`)으로 옮겨 같은 배치 규칙 — 자리는 viewer `overlay.screen` |
| 3 | 우클릭 메뉴 `ShowMarkerContextMenu` (WPF ContextMenu) | 심볼 우클릭 | 마우스 자리(`MousePoint`) | 3D pick 자리(`_map3DMenuAt`)에 3D 유지로 열림 | 그대로(이미 됨) |
| 4 | 심볼 상세 보기 `SymbolDetailViewModel` (PropertyPanelCanvas 고정 150,70) | 메뉴 "상세 보기" | 고정 자리 · 드래그 | composition 이면 3D 유지 | 그대로(규칙 함수만 새 것으로) |
| 5 | 등록 센서 정보 `SensorInfoPanelViewModel` (고정 210,110) | 구역 우클릭 메뉴 | 고정 자리 · 드래그 | 3D 유지 | 그대로 |
| 6 | 카메라 상자 허브 `CameraPopupControlHub` | 상자 1개 이상이면 자동 | 저장된 화면 자리 | 3D 유지(고정) | 그대로 |
| 7 | 구역 조치보고(Events.Ui 모달) `GroupActionReportLauncher` | 구역 라인 더블클릭 | 독립 창 | 3D pick dblclick → `TriggerGroupActionReport` 로 열림 | 그대로 |
| 8 | 이벤트 창(탐지 · 장애 자동 카메라 창) `EventWindowManager` | 이벤트 큐 | 모니터 작업 영역 계단 배치(OS 창, 지도 무관) | 지도와 무관 | 그대로(지도 창 아님) |
| 9 | 이벤트 지도 이동(FlyTo) | 이벤트 큐 | 팝업 아님 | 3D 유지 · 3D 카메라 반영 | 그대로 |
| 10 | 라벨 `LabelAdorner` / 호버 툴팁 `SymbolToolTip` | 항시 / 호버 | 마커 아래 · 마우스 근처 | 라벨은 viewer 가 3D 에 그림, 호버 툴팁은 3D 에 없음 | 그대로(호버는 이번 범위 — 클릭 · 더블클릭 · 우클릭 · 이벤트 — 밖) |
| 11 | 오버레이 이미지 클릭 | 클릭 | 선택만 | pick → `OnMapImageClicked` | 그대로 |

네이티브 창 3D(hwnd, Windows SDK 투영이 없을 때의 폴백)는 WPF 창을 덮으므로(공역) 1 · 2 · 4 를 열 때 지금처럼 2D 로 돌아온다(DECISIONS 1).

## 진행
- [x] 전수 조사(위 표)
- [x] viewer(map-3d 복제본): `static/overlay-screen.js`(parseWatch · screenOf · ScreenReporter, three 무의존) · `overlay3d.js anchorOf` · `view.js`
      `overlay.watch` 받기 → 그린 프레임 뒤에만 바뀐 항목(0.5 px 넘게)을 `overlay.screen` 으로. `tests/overlay_screen_check.mjs` + pytest 1개
      → `.harness/t-20261018-013253-800e/map3d.patch` (원본 새 복제본에 `git apply --check` 통과)
- [x] GIS 순수 함수 `Utils/Map3DScreen.cs`: WatchJson · ParseScreens · ToOuter(CSS px × ZoomFactor + 호스트 원점) · Offset2DTo3D/Offset3DTo2D · PopupTopLeft3D · DraggedAnchor2D,
      `Map3DMath.ParseMessage` 에 overlay.screen(`Map3DMessage.Screens`), `Map3DGuide.ShouldLeave3DForWindow`
- [x] `Map3DViewHost.OverlayLayer`(3D 위 Canvas, WebView2 는 늘 맨 아래) · `ZoomFactor`
- [x] `ComponentCardService.UseOverlay` · `Relayout` — 같은 카드 VM 을 2D 어도너 ↔ 3D 위 층으로 옮김(카드 보기만 새로)
- [x] `MapViewModel.Map3DWindows.cs`: Begin(Reveal3D 끝)/End(Leave3D — 2D 카메라 맞춘 뒤) · 지켜볼 목록 · overlay.screen 처리 · 카메라 상자 3D 배치 · 3D 드래그 → 2D 앵커,
      `RefreshCameraPopupPositions` 서두 분기, 드래그 저장 분기, `LeaveMap3DForPanel` 새 규칙, 3D pick 클릭 뒤 목록 갱신. 진입점마다 예외 격리(로그 + 창만 실패)
- [x] 시험: `tests/GMaps.Ui.Tests/Map3DScreenTests.cs`(23) · `tests/GMaps.Housing.Tests/Map3DWindowCardTests.cs`(5, 창 없음)
- [x] 메인 앱 변경 불필요(라이브러리 공개 형식 더하기만, 메인 앱이 쓰는 형식 바뀜 없음) → host.patch 없음
- [x] 정리: map-3d 복제본(`_map3d`) · 기준선 사본(`_baseline`) 지움

## 검증 결과
- `dotnet test Ironwall.Dotnet.Libraries.sln`: 2887 통과 · 1 실패 — 실패는 기존 시간 예산 시험 `EventWindowManagerTests.should_return_within_budget_and_not_throw_when_supervisor_is_unavailable`(기준선과 같음, 이번 변경과 무관한 CameraPopup)
  · 마지막 재실행(원격 세션 Disc 뒤): Utils.Tests 8 실패(FrameBorder 렌더 · CaptureDragLifecycle 마우스 캡처) · CameraPopup.Host.Tests 1 실패(EventWindowRender 렌더),
    CameraPopup.Tests 는 이번엔 통과. 두 실패 프로젝트는 GMaps.Ui 를 참조하지 않는다(Utils.Tests → Utils(참조 없음), Host.Tests → CameraPopup.Host · Contracts · Enums)
    → 이번 변경과 무관한, 세션이 끊겨 WPF 렌더 · 캡처가 비는 환경 탓. 세션이 붙은 상태의 첫 실행은 기준선과 같았다.
- `tests/GMaps.Ui.Tests`: 1181 통과
- `tests/GMaps.Housing.Tests`: 359 통과(첫 전체 실행). 뒤의 재실행에서 렌더 픽셀 시험 19개가 "픽셀 0개"로 실패 — 그 사이 원격 세션이 끊김(quser STATE=Disc)
  · 같은 렌더 코드로 앞 실행은 전부 통과 · 이번 변경은 그 렌더 경로를 건드리지 않음. 새 카드 시험 5개는 두 번 모두 통과.
- `tests/GMaps.PropertyPanel.Tests` 308 · `tests/GMaps.MapAux.Tests` 76 통과(카드 호스트를 바꿔서 함께 확인)
- map-3d 복제본 pytest 72 통과(새 시험 포함)
- **실기 미확인** — 창 · WebView2 를 띄우는 확인은 하지 않았다(사용자 PC 사용 중 규칙).

## 발견
- 메인 앱 대상은 net8.0-windows10.0.17763.0 → 3D 기본은 WPF 안 3D(composition). Map3D.cs 의 "composition 되돌림" 주석은 옛 대상(windows7.0) 시절 이야기다.
- 조립 카드 어도너는 지도 AdornerDecorator 안이라 3D 호스트(ContentControl, 같은 ZIndex · 뒤 선언) 밑에 깔린다 → 3D 위 층이 필요했다.
- 지도 컨트롤을 부모 없이 Measure 하면 다른 시험 스레드가 봉인한 템플릿 때문에 교차 스레드 예외 — 카드 시험은 지도를 배치하지 않는다.
- `git archive` 사본은 서브모듈 · 서명 키가 없어 빌드되지 않는다(기준선 비교 불가).
- `%TEMP%\m3d-verify-800e`(map3d.patch 적용 확인용 새 복제본)는 worktree 밖이라 지우지 못했다 — 필요하면 지워도 된다.

## 남은 일(코디네이터)
- map-3d 저장소 루트에서 `git apply <이 worktree>/.harness/t-20261018-013253-800e/map3d.patch` → pytest.
- 실기: 3D 에서 카메라 더블클릭 → 상자가 마커 옆에 뜨고 궤도 조작 · 휠에 따라가는지, 심볼 클릭 → 조립 카드, ESC/줌 아웃으로 2D → 창 유지, 다시 3D → 창 유지.
