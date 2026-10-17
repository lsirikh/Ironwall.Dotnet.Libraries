# PLAN — 지도 3D R1(오버레이 이미지) · R2(오버레이 아이템 전부) GIS 쪽

명세: `C:\workspace_app\Ironwall.Dotnet.Libraries\docs\prds\2026-10-17-지도-3d-전환-시나리오와-요구사항.md` + 하네스 목표의 메시지 규약 v2.
범위 파일: `GMaps.Ui/Utils/Map3DOverlay.cs` · `Services/Map3D/Map3DOverlayBridge.cs` · `Utils/Map3DMath.cs`(pick 해석) · `ViewModels/Maps/MapViewModel.Map3D.cs`(OnMap3DPick 만) + 시험.

## 진행
- [x] Map3DOverlay(순수): 새 kind · 아이콘/라벨/FOV/선 스타일/이미지 DTO · overlay.icon/imageData · track.upsert/remove · sha1 키 · 긴 변 맞추기 · 라벨 배경
- [x] Map3DMath.ParsePick: track 문자열 id(Map3DPick.TrackId)
- [x] Bridge: KindOf 확장, 아이콘 PNG(VisualBrush→RenderTargetBitmap, 루트 변환 역적용, 시각 서명 캐시), 이미지(GMapImageMarker · CustomImages) PNG, 추적, snapshot images[]
- [x] OnMap3DPick: image(음수 id = TIF 오버레이 → OnMapImageClicked) · track(2D 동작 없음 → 기록만) · 나머지는 KindOf 로 찾음
- [x] 시험: GMaps.Ui.Tests `Map3DOverlayV2Tests`(순수, 945 통과) · GMaps.Housing.Tests `Map3DOverlayPixelTests` · `Map3DOverlayBridgeFlowTests`(11 통과)
- [x] dotnet test Ironwall.Dotnet.Libraries.sln — 2회차 exit 0(1회차는 무관한 Utils.Tests `PerfSamplerTests` 가 부하 중 호스트를 죽임 — 단독 3회 · 2회차 통과, 기존 불안정 시험)

## 발견
- GMaps.Ui.Tests 는 net8.0(비 WPF) + 소스 링크 → Map3DOverlay.cs 는 WPF · ColorHelper 무의존 유지. 새 enum(EnumLinePattern)은 csproj 링크 추가.
- GMapCustomImage(TIF) 는 마커가 아니고 Id 가 DB 키가 아님(대개 0) → 다리가 음수 id 를 붙인다.
- 2D 추적 마커 클릭 동작 없음(IEditableMarker 아님) → 3D track pick 은 기록만.
- 추적 꼬리 점 · 진행 방위는 private(GMapTrailMarker._points · GMapTrackingMarker._worldBearing) — 범위 밖 파일이라 리플렉션으로 읽고 실패하면 빈 꼬리/0.

- VisualBrush 는 부모 오프셋은 빼지만 루트 RenderTransform 은 그린다(시험으로 확인) → 역변환을 DrawingContext 에 걸어 회전 없는 아이콘.
- 추적 최소 줌 게이트(Shape.Visibility)는 PropertyChanged 가 없어 다음 위치 갱신 때 반영된다.

## 남은 일
- 실기(3D 화면에서 가림 · 아이콘 · 클릭) 미검증 — map-3d 쪽(M) 구현 뒤.
