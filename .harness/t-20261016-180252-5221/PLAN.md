# PLAN — 지도 3D 굽기 · 줌 게이트 · 3D 화면 바꿔 끼우기 (POC)

## 목표
GIS 지도(GMaps.Ui)에서 영역을 그려 map-3d 서버로 3D 를 굽고, 구운 영역 안에서 실효줌 ≥ 17 이면 지도 자리에
WebView2(map-3d `/view?embed=1`)를 같은 위치 · 축척 · 방향으로 올린다. 3D 에서 줌 < 16.5 또는 ESC 면 2D 로 돌아오며
위치 · 줌 · 방향을 2D 에 되돌린다.

## 파일
- `Ironwall.Dotnet.Libraries.GMaps/Models/Map3DModel.cs` — 설정(IsEnabled 기본 true, ServerUrl 기본 http://127.0.0.1:8765, Normalize)
- `GMaps.Ui/Helpers/MapSettingsHelper.cs` — `LoadMap3D(log)` 동기 로드(AppSettings.Map3D, LoadMapEventFlyTo 동형)
- `GMaps.Ui/Utils/Map3DMath.cs` — 순수: 게이트 Decide(17.0/16.5), 방위 변환, 줌 변환(실효줌↔메르카토르), URL, 메시지 JSON 생성/해석
- `GMaps.Ui/Services/Map3D/Map3DClient.cs` — HTTP(굽기 POST · 진행 GET · scenes/at), 가짜 HttpMessageHandler 로 시험
- `GMaps.Ui/GMapControls/Map3DViewHost.cs` — WebView2 감싼 호스트(처음 1회 생성 · 재사용, ready 전 1×1 대기)
- `GMaps.Ui/ViewModels/Maps/MapViewModel.Map3D.cs` — 그리기 모드(앵커 러버밴드 재사용) · 굽기 폴링 · 게이트 코얼레싱 · 바꿔 끼우기
- `MapView.xaml` — 툴바 "기준" 묶음에 3D 영역 단추(AutomationId GMaps.Map3D.DrawButton) + 지도 위 호스트 ContentControl
- 시험: `tests/GMaps.Ui.Tests/Map3DMathTests.cs`, `Map3DClientTests.cs`, `Map3DModelTests.cs` (소스 링크)

## 진행
- [x] 코드 조사(앵커 그리기 · 틸트 코얼레싱 · 방위 부호 · 설정 저장 방식 · WebView2 관례)
- [x] Map3DModel + LoadMap3D
- [x] Map3DMath + 시험
- [x] Map3DClient + 시험
- [x] Map3DViewHost + csproj WebView2 패키지(1.0.2903.40)
- [x] MapViewModel.Map3D + 앵커 그리기 분기 · ESC/Enter
- [x] XAML 단추(CubeOutline — dll 실존 확인) · 호스트 ContentControl
- [x] GMaps.Ui.Tests Map3D 69건 통과, 솔루션 빌드 오류 0
- [ ] dotnet test Ironwall.Dotnet.Libraries.sln — 하네스가 돌림(창 띄우는 시험 회피 위해 직접 전수 실행 안 함)

## 발견
- GMap Bearing: `_rotationMatrix.Angle = -Bearing`(WPF 시계 +) → 지도가 반시계로 Bearing 만큼 돈다 = 화면 위쪽 방위가 Bearing.
  `RotationMath.DisplayAngle = markerBearing − mapBearing` 도 같은 뜻. 즉 map-3d bearing 과 같은 규약 — 변환은 정규화만.
- 디지털 줌 배율표 {1, 1.25, 1.5, 2.0}, 실효줌 = 타일줌 + 0.5·dzl → 실제 메르카토르 줌 = 타일줌 + log2(배율). 3D 에는 메르카토르 줌을 보낸다.
- 뷰포트 스냅샷은 팬 · 정수 줌에 발행되지 않음 → OnPositionChanged/OnMapZoomChanged/DigitalZoomLevelChanged 에 Render 우선순위 코얼레싱을 직접 둔다.
- 2D 회전은 회전 kill-switch(기본 OFF) · 앵커 게이트를 거친다 → 3D 에서 돌린 방위가 2D 에 안 들어갈 수 있음(게이트가 막음, 로그만).

## 남은 일 / 앱 쪽 필요한 것
- 실기(WebView2 · map-3d) 전환 미검증 — 그리기 · 굽기 · 진입 · ready · 복귀 · ESC 모두 실제 화면으로 확인 못 함.
- 앱 쪽: 추가 연결 불필요(설정은 appsettings `AppSettings.Map3D` 를 직접 읽음). 배포본에 WebView2 런타임 필요(Reports.Ui 와 같음).
- 알려진 한계: 3D 가 펼쳐진 동안 WPF 상태 배너는 WebView2 아래에 가려진다(공역). 3D 화면 ESC 는 WebView2 가 Esc 를 WPF 로 넘겨 주는 것(AcceleratorKeyPressed)에 기댄다 — 미검증.
