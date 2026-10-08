# PLAN — 지도 심볼 속성 반영 · 위치 · 레이어 · 겹창 헤드리스 시험(t-20261008-150645-cbad)

## 목표
첨부 카탈로그 §1(MAP-001 ~ MAP-196) 중 기존 시험이 없거나(GAP) 헤디드로만 있던 항목을, 창을 띄우지 않고(Window.Show 금지 · 화면 밖 창 금지) 뷰모델 · 서비스 · 부모 없는 Measure/Arrange · RenderTargetBitmap 픽셀로 검사한다. 결함은 증거로 원인 확인 → 작고 분명하면 고치고 회귀 시험.

## 결정
- 새 시험 위치: `tests/GMaps.Housing.Tests`(Application 없는 STA · 마커/서비스) · `tests/GMaps.PropertyPanel.Tests`(Application 상주 STA · 속성창/겹창 렌더).
  - 두 프로젝트는 `Ironwall.Dotnet.Libraries.sln` 밖이라 하네스 검사(`dotnet test sln`)에는 안 잡힌다. 솔루션에 넣으면 기존 시험(환경 의존 포함)까지 검사에 끌려 들어가므로 넣지 않는다 — 직접 `dotnet test <csproj>` 로 돌려 결과를 기록한다.
- GMap 컨트롤 시험이 필요하면 벤더 정적 템플릿 3종을 시험 스레드에서 다시 만든다. 단 MapLocateFitTests 처럼 window.Show 는 하지 않는다.
- 지도 DB 접속 금지 — 모델 · 가짜 저장소만.

## 진행
- [x] 카탈로그 §1 읽기, 기존 시험 목록 파악, 빌드 확인(PropertyPanel.Tests 빌드 0 오류)
- [x] 심볼 속성 반영 · 그룹 편집 시험 `tests/GMaps.PropertyPanel.Tests/MapSymbolReflectionTests.cs`(22건 통과)
- [x] 결함 D1 확인(수정 전 5건 실패) → 수정(`GMapMarkerBaseControl.UpdateMarkerAppearance`, `GMapMarkerInfraControl.UpdateInfraAppearance`) → 통과
- [x] 결함 D2 수정(`MapViewModel.CopyPaste.cs` PersistLabelStyleAsync, `MapViewModel.UndoContext.cs` RestoreOrInsertSymbolAsync) + 회귀 `tests/GMaps.Housing.Tests/SymbolCopyLabelStylePersistenceTests.cs`(4건)
- [x] 레이어 트리 · 오버레이 이미지 시험 + 결함 D3 수정(`MapViewModel.ApplyOverlayImageLayerState`, OnLayerOpacityChanged 이미지 행 저장) `tests/GMaps.Housing.Tests/LayerPanelMapStateTests.cs`(7건)
- [x] 겹창 렌더(라이트/다크) 시험 `tests/GMaps.PropertyPanel.Tests/MapOverlayPanelThemeLayoutTests.cs`(14종 × 2 = 28건)
- [~] 위치(홈 · 고정): MAP-003 은 벤더 GMapControl 지연 이벤트(_lazyEvents)가 창 Loaded 뒤에만 풀려 창 없이 재현 불가 → 헤디드 남김(순수 수학은 기존 AnchorViewportClampTests)
- [x] 결과표(항목 id 별) 정리 — 아래
- [ ] 두 시험 프로젝트 전체 회귀 실행 · `dotnet test Ironwall.Dotnet.Libraries.sln`

## 항목별 결과(이번 작업에서 새로 본 것)
- 통과: MAP-052 · 053 · 055 · 056 · 057 · 058 · 059(재적재) · 067 · 068(보임 복원) · 080 · 106(스냅샷 · 되돌리기 왕복) · 109 · 111 · 112 · 113 · 114 · 116 · 117 · 118(값 왕복) · 119 · 134(그룹 빈칸 표기) · 137 · 139(다시 열기 · 스냅샷) · 190(14종)
- 결함 → 수정 → 통과: MAP-115 · 141(D1) · MAP-104 · 105 · 106(D2) · MAP-067 영속(D3)
- 헤디드 남김: MAP-003 · 013 · 014 · 039 · 063 · 064 · 065 · 066 · 077 · 078 · 087 · 096 · 099 · 100 · 110(속성창 머리 끌기 — 코드에 지도 안 가두기 없음, 실끌기로 확인 필요) · 135 · 169 · 188(끌기) · 191 · 192
- 이번에 손대지 않음(기존 시험으로 덮였거나 범위 시간 밖): 나머지 MAP 항목 — 카탈로그의 기존 시험 칸 그대로

## 발견(결함)
- D1(확정 · 수정): `GMapMarkerBaseControl.UpdateMarkerAppearance` 가 MarkerState 바뀔 때 `MarkerFill` 에 로컬 값(초록/회색/빨강)을 넣어 `FillColor` OneWay 바인딩을 지움 → 기하 · 선 · 군대부호 · PIDS 그룹 · 인프라 2D 심볼에서 상태가 한 번 바뀐 뒤(PIDS 그룹 SYNC, 되돌리기 OperationState) 사용자 채우기 색이 사라지고 이후 속성창 색 변경이 지도에 반영되지 않음. 증거: 시험 5건 실패(DP · 픽셀) → 수정 뒤 통과.
- D2(확정 · 수정): 타입별 INSERT/복원 SQL 이 라벨 스타일 6종(TitleColor · TitleBackground · TitleFontFamily · TitleBold · TitleItalic · TitleMaxWidth)을 쓰지 않음(전용 부분 UPDATE 전략). 복제 · 붙여넣기(`CreateSymbolCopyAsync`)는 INSERT 직후 Fetch 한 모델로 마커를 만들어 복사본이 그 자리에서 기본 라벨로 보였고, 삭제 되돌리기도 다시 불러오면 기본 라벨. 증거: INSERT SQL 컬럼 목록(GMapDbSymbolService.cs:1184-1189 등) · Fetch SELECT 는 스타일 컬럼을 읽음 · 복사 경로가 Fetch 결과 반환.
- D3(확정 · 수정): 레이어 패널 오버레이 이미지 투명도 슬라이더가 MapLayers.Opacity 만 저장하고 Images.Opacity 는 안 씀 + 재시작 때 `RestoreLayerVisibility` 가 보임 · 순서만 이미지 마커에 되돌리고 투명도는 빠뜨림 → 다시 켜면 슬라이더 값과 그림 투명도가 어긋남.
- 참고(결함 아님): `LayerTreeBuilder.PopulateImageSection` 은 운영 코드에서 호출되지 않음(죽은 코드 — 이미지 행은 MapLayers 행으로 만든다).
- 남김(미수정): 이미지 복원(`RestoreImageAsync` · 복원 SQL)도 라벨 스타일 컬럼을 쓰지 않음 — 이미지는 이후 UpdateImageAsync 에서 스타일이 실리므로 영향 작음, 보고에만 적는다.

## 남은 일
(진행하며 갱신)
