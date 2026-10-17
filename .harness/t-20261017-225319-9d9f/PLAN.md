# PLAN — 오버레이 이미지 3D 굽기 옵션(GIS 쪽)

## 목표
3D 영역 굽기 직전에 "위성 타일만 / 오버레이 이미지 포함(그림 합성) / 오버레이 이미지 포함 + 건물 추출" 을 고르고,
POST /api/jobs 본문에 overlay_mode · overlays 를 실어 보낸다(GIS ↔ map-3d 규약 2026-10-17). map-3d(Python)는 범위 밖.

## 진행
- [x] 순수 함수 `Utils/Map3DBake.cs` — 모드 정규화 · 패널 띄울지 · 기본 모드 · 회전 포함 겹침 판정 · 변환 필요 판정(PNG/JPG · 50 MB) · 요청 본문
- [x] `Map3DClient.StartBakeAsync(bbox, overlayMode, overlays)` 오버로드(기존 bbox 만 받는 것은 tiles 로 위임 — 본문 그대로)
- [x] `Map3DModel.OverlayMode`(기본 tiles, Normalize) · `MapSettingsHelper.LoadMap3D` 읽기 · `SaveMap3DAsync` 저장
- [x] `Map3DImageCache.GetOrEncodeFile` — TIF · 50 MB 초과 이미지를 PNG/JPG 캐시 파일로(굽기 요청 image_path)
- [x] `Map3DOverlayBridge.IdOf(GMapCustomImage)` — CustomImages id 를 3D 스냅샷과 같게
- [x] `MapViewModel.Map3DBakeOptions.cs` — RequestMap3DBake(겹치는 이미지 없으면 바로 tiles) · 패널 상태 · [만들기]/[취소] · 이미지 수집
- [x] 영역 그리기(TakeMap3DArea) · 배지 「다시 만들기」(RebakeMap3DArea) 가 RequestMap3DBake 를 거친다. 새 영역 그리기 시작 시 패널 닫음
- [x] `MapView.xaml` 선택 패널(MapFloatingPanelStyle · 토큰, AutomationId GMaps.Map3D.BakeOptions.*, x:Name 없음)
- [x] "?" 말풍선 3개(MapsHelp: Map.Map3D.BakeTiles / BakeComposite / BakeExtract)
- [x] 단위 시험 `tests/GMaps.Ui.Tests/Map3DBakeTests.cs`(csproj 에 Map3DBake.cs 링크) — GMaps.Ui.Tests 1072 통과
- [x] `dotnet test Ironwall.Dotnet.Libraries.sln` 통과(10개 시험 어셈블리, 실패 0) · 도움말 키 계약 시험(MapsHelpTests, 솔루션 밖) 통과

## 발견
- TIF 오버레이(GMapCustomImage)의 FilePath 는 .tif 그대로 — 서버는 PNG/JPG 만 받으므로 변환 캐시가 필요했다.
- CustomImages 의 3D id 는 다리(Map3DOverlayBridge)가 세션마다 붙이는 음수 — 앱을 다시 켜면 바뀐다(아래 미검증 · 한계).

## 남은 일 · 미검증
- 실기 미검증: 실제 map-3d 서버(규약 구현본)와 왕복, 실제 GIS 이미지(GeoTIFF · 큰 PNG)로 composite/extract 결과.
- 한계: CustomImages(TIF) id 는 세션 단위라 앱 재시작 뒤엔 overlays_baked 와 안 맞을 수 있다(3D 화면이 구운 이미지를 다시 덮어 그림).
