# PLAN — 오버레이 이미지 다각형 자르기

## 목표
오버레이 이미지를 처음 불러올 때 · 이미 놓인 뒤에 다각형으로 잘라 투명 PNG 로 쓰기(3D 는 투명 부분 모양을 범위로 씀).

## 찾은 경로
- 추가 흐름: `MapViewModel.ExecuteLoadImageOverlay`(MapViewModel.cs ~3777) — OpenFileDialog → **(새) `CopyOverlayImageWithCropAsync`**(판 → 복사 → 자른 PNG) → 픽셀 크기로 경계 계산 → `_markerFactory.CreateImageMarker` → `DbSaveProcess` → MapLayers INSERT.
- 저장 위치 규칙: `ImageFileService`(실행 폴더/Images/Overlays, 상대 경로 저장). 자른 그림 = `Images/Overlays/{이름}_crop_{yyyyMMdd_HHmmss_fff}.png` + 옆 기록 `.crop.json`.
- 회전: `GMapMarkerImageControl` 은 경계(회전 전 상자)를 화면(메르카토르)에 그린 뒤 RenderTransform(중심 0.5/0.5)로 시계 방향 회전.
- 3D 인코딩: `Map3DImageCache.Encode` — 투명 픽셀 있으면 PNG(바꾸지 않음, 시험으로 확인).
- 우클릭 메뉴: `ShowMarkerContextMenu`(MapViewModel.cs, 편집 모드 레이어 순서 항목 아래).

## 판 디자인(참조 스타일 키)
BuildingHeightPanelStyle 답습 — `fb:FrameBorder`(SurfaceBrush · BorderBrush · 반지름 10 · 그림자), 머리 띠 `PrimaryBrush` 38(끌기, OverlayHeaderDrag) · `OnPrimaryFixedBrush` 제목/아이콘, "?" = `PanelHeaderHelpDisc` + `fb:HelpTip HelpKey="Map.ImageCrop.Panel"`, 닫기 = `PanelCloseButtonStyle`, 단추 = `PanelPrimaryButtonStyle` / `PanelSecondaryButtonStyle`, 상태 = `TextSecondaryBrush`, 이유 = `StatusCriticalBrush`, 닫기 확인 띠 = `TintWarningBrush`, 캔버스 바탕 = `SurfaceSunkenBrush`, 바깥 어둡게 = `ScrimModalBrush`. 라이트/다크 시험으로 확인.

## 새 파일
- `GMaps.Ui/Utils/ImageCropMath.cs` — 검사 · 마스크 · 상자 · PNG · 경계 조정/되돌림(메르카토르)
- `GMaps.Ui/ViewModels/Maps/ImageCropViewModel.cs` — 판 VM(점 · 끌기 데드존 DragMath.IsDrag · Undo/Redo · Esc 2단계 · 확대/이동 · 결과 Task)
- `GMaps.Ui/GMapControls/ImageCropPanelControl.cs` — 판 Control + `ImageCropCanvas`(그리기 · 마우스)
- `GMaps.Ui/Themes/ImageCropPanelStyle.xaml`(Generic.xaml 병합)
- `GMaps.Ui/Services/ImageCropStore.cs` — 파일 저장 · 기록
- `GMaps.Ui/Services/Undo/Commands/ImageCropCommand.cs` — Undo 1단위(파일 · 경계 · 크기)
- `GMaps.Ui/ViewModels/Maps/MapViewModel.ImageCrop.cs` — 판 열기 · 처음 불러오기 · 다시 자르기 · 되돌리기 · 적용(DB · MapLayers 경로)
- 수정: MapViewModel.cs(추가 흐름 3줄 · 우클릭 메뉴), MapView.xaml(ContentPresenter 1개), MapsHelp.cs(도움말), Generic.xaml
- 시험: `tests/GMaps.MapEdit.Tests/ImageCropMathTests.cs`(순수 · VM), `ImageCropScenarioTests.cs`(MapViewModel · 판 템플릿)

## 진행
- [x] 순수 수학 · 판 VM · 판 뷰 · MapView 호스팅
- [x] 추가 흐름에 끼우기 · 자른 파일 저장(원본 복사본 보관 · 옆 기록)
- [x] 다시 자르기 · 자르기 되돌리기(우클릭) · Undo 1단위 · DB · MapLayers 경로
- [x] 도움말(MapsHelp "Map.ImageCrop.Panel")
- [x] 시험 38개(MapEdit.Tests) 통과
- [x] 수정 전 실패 → 수정 후 통과 증명: 그리는 중 기존 꼭짓점 클릭이 점을 하나 더 찍던 결함 — 고친 줄을 빼면 2건 실패(Expected 3/Actual 4, Expected 2/Actual 3), 넣으면 통과
- [x] GMaps.Ui.Tests 1501 · GMaps.MapEdit.Tests 427 · GMaps.PropertyPanel.Tests 314 통과
- [x] dotnet test Ironwall.Dotnet.Libraries.sln — 시험 어셈블리 10개 모두 통과(실패 0)

## 발견
- 나비 모양 다각형은 넓이가 0 이라 넓이 검사가 먼저면 "넓이 없음"으로 나왔다 → 교차 검사를 먼저.
- 빌드가 MSBuild OutOfMemory 로 한 번 실패(동시 작업 부하) → `-m:1 -nodeReuse:false` 로 통과.

## 남은 일
- 헤디드(실제 마우스) 확인은 하지 않음(창 시험 금지) — 마우스 → VM 은 ImageCropCanvas 의 얇은 전달만.
