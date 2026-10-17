# PLAN — 지도 3D 전환 시나리오 R4 · R5 · R6 · R7 (GIS GMaps.Ui)

명세: `C:\workspace_app\Ironwall.Dotnet.Libraries\docs\prds\2026-10-17-지도-3d-전환-시나리오와-요구사항.md`
손대지 않을 것(작업 B): `Utils/Map3DOverlay.cs` · `Services/Map3D/Map3DOverlayBridge.cs` · `MapViewModel.Map3D.cs` 의 `OnMap3DPick`.

## 설계
- 순수 함수: `Utils/Map3DGuide.cs`(새 파일, WPF 무의존) — 안내 문구 · 최소 표시 시간 · 로그인 뒤 안내 판정 · 영역 크기(m) · 2 km 판정 · 크기 글 · 영역 표시 줌(13) · "3D 가능" 칩 판정 · 영역 가운데 · 툴팁 글 · 패널 열 때 2D 복귀 판정.
- HTTP: `Map3DClient.ListScenesAsync`(GET /api/jobs 의 scenes) · `DeleteSceneAsync`(DELETE /api/jobs/{id}) · `ParseScenes`. 레코드 `Map3DSceneInfo`.
- R5 안내 패널: `SetMap3DBusy` 확장(제목 + 상세 + 막대), `_map3DBusyShownAt` + 50 ms 타이머(RevealWhenDue / HideWhenDue).
  - 진입: Begin3D 즉시 "3D 지도로 전환합니다" — 재사용이면 0.6 s 동안 막대 0→100 뒤 Reveal, 처음이면 progress % 뒤 ready → 최소 0.6 s 채운 뒤 Reveal.
  - 복귀: Leave3D 에서 "2D 지도로 돌아갑니다" 0.5 s.
  - 로그인: Reveal 때 ITokenStorageService.IsAuthenticated == false 면 표시 → ISessionLifecycle.LoginSucceeded 에서 3D 중이면 한 번 안내(1.5 s).
- R4 영역 표시: `Views/Maps/Map3DAreaMarker.cs`(GMapMarker, IEditableMarker 아님 = DB 저장 안 됨) — 테두리 PrimaryBrush 1.5 px + 모서리 "3D" 배지(툴팁 · 우클릭 메뉴). `Services/Map3D/Map3DAreaItem.cs`(목록 항목 + 동작 3개).
  - VM 새 파셜 `MapViewModel.Map3DAreas.cs`: 목록 갱신 · 마커 동기화 · 줌 13 표시 · 칩 · 보기/다시 만들기/지우기(확인 팝업 EventAggregator).
  - XAML: 지도(M) 메뉴 「3D 영역 목록」, 줌 컨트롤 옆 칩 `GMaps.Map3D.Chip`, 진행 패널 상세 줄.
- R6: `GMapCustomControl.AnchorDrawLabel`(공개 속성 추가) → `RubberBandAdorner.Update(rect, label, warn)` 가 크기 글을 그린다(2 km 초과 = StatusWarningBrush). 놓았을 때 넘으면 이유 배너 + 그리기 모드 유지, Enter 도 같은 판정.
- R7: 경로 확인(OnMap3DPick dblclick → TriggerMarkerDoubleClicked → OnMapMarkerDoubleClicked → CameraPopupDoubleClickDispatcher → OpenSelfCameraPopup → LeaveMap3DForPanel("camera-popup") → Leave3D). LeaveMap3DForPanel 판정을 순수 함수로 떼어 시험.

## 진행
- [x] 코드 조사 · 설계
- [x] Map3DGuide.cs + 시험(tests/GMaps.Ui.Tests/Map3DGuideTests.cs)
- [x] Map3DClient 목록 · 지우기 + 시험(Map3DClientTests 추가분)
- [x] R5 안내 패널(VM + XAML 둘째 줄 GMaps.Map3D.Progress.Detail)
- [x] R6 크기 표시 · 경고 · 이유(RubberBandAdorner · GMapCustomControl.AnchorDrawLabel · TakeMap3DArea)
- [x] R4 영역 마커 · 메뉴 · 칩(Map3DAreaMarker · Map3DAreaItem · MapViewModel.Map3DAreas.cs · MapView.xaml)
- [x] R7 판정 함수(Map3DGuide.ShouldLeave3DForPanel) · 시험
- [x] dotnet test tests/GMaps.Ui.Tests(975 통과) · dotnet test 솔루션(결과는 아래 기록)

## 발견
- 지도 마커 Shape 의 IsHitTestVisible 은 SetEditMode 가 모든 마커에 덮어쓴다 → 영역 마커는 강제 true 캔버스.
- map-3d 서버 소스(C:\workspace_python\map-3d)는 이 세션 권한 밖 — scenes 형식은 명세대로 관대하게 해석.

## 검증 기록(2026-10-17)
- `dotnet test tests/GMaps.Ui.Tests/GMaps.Ui.Tests.csproj` → 975 통과 · 실패 0
- `dotnet test Ironwall.Dotnet.Libraries.sln` → exit=0(전 프로젝트 통과)
- `dotnet build Ironwall.Dotnet.Libraries.GMaps.Ui` → 오류 0, 바꾼 파일 경고 0

## 남은 일
- 실기(화면) 확인은 미검증(창 띄우는 시험 금지).
