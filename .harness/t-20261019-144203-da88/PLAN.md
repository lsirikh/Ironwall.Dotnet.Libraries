# 레이어 패널 다중 선택 + 맵 편집 중 지도 선택 양방향 연동

## 목표
레이어 패널에서 클릭 · Ctrl · Shift · Ctrl+Shift · 키보드로 여러 항목을 고르고, 맵 편집 중이면 지도 오버레이 마커 선택(단일 · 묶음 ·
어도너 · 묶음 회전 · 속성 판 다중 편집)과 양방향으로 맞춘다. 편집 모드가 아니면 패널 안에서만 선택된다.

## 진행
- [x] 노드 선택 상태 `LayerTreeNode.IsSelected` · 초점 `IsCurrent` (Models/LayerTreeNode.cs)
- [x] 선택 판정 `Models/LayerSelection.cs` — 클릭 조합 · 묶음 머리 · 키보드 · 보이는 순서 · 재조회 열쇠 · 펼치기
- [x] `LayerPanelControl` — 행 누름(체크박스 · 자물쇠 · 셰브런 · 슬라이더 · 손잡이 · 글 칸 제외) · 우클릭 · 키 · 손잡이 클릭 목록 선택 맞춤 ·
      `LayerSelectionChanged` 이벤트(사용자 조작만) · `IsSelectionKey` · `BringNodeIntoView`
- [x] `LayerPanelStyle.xaml` — 오버레이 목록 Extended + ListBoxItem.IsSelected ↔ 노드, 심볼 잎 좌측 세로 바 · 초점 고리 · UIA id/ItemStatus
- [x] `GroupSelectionService.SelectionChanged` 이벤트(공개 형식 더하기)
- [x] `MapViewModel.LayerSelection.cs` — 패널 → 지도(PushLayerSelectionToMap), 지도 → 패널(SyncLayerSelectionFromMap), 재조회 복원
- [x] MapViewModel 배선 — SelectedMarker 세터 · 묶음 선택 이벤트 · 패널 이벤트 구독/해제 · 편집 모드 켜짐 · LoadLayersFromDbAsync · 창 키 후킹 양보
- [x] 레이어 "?" 설명(MapsHelp) 선택 조작 · 키보드 폴백 줄
- [x] 시험: tests/GMaps.MapLayer.Tests/LayerPanelSelectionScenarioTests.cs(34) · tests/GMaps.MapEdit.Tests/LayerPanelMapSelectionSyncTests.cs(8)
- [x] 수정 전 실패 증명: 배선 3줄(SelectedMarker 세터 동기 · 묶음 SelectionChanged 구독 · 패널 LayerSelectionChanged 구독)을 빼면 연동 시험 8건 중 6건 실패 → 되돌리면 8건 통과
- [ ] 완료 판정 실행: sln · GMaps.Ui.Tests · MapEdit · MapNav (+ MapLayer · Housing · MapAux · PropertyPanel)

## 결정
DECISIONS.md 1~10.

## 발견
- `PreviewMouseLeftButtonDown` 은 Direct 이벤트 — 시험에서 누름은 터널 `Mouse.PreviewMouseDownEvent` 로 올려야 패널까지 간다(실입력과 같은 길).
- `LoadLayersFromDbAsync` 의 펼침 복원(expandedNames)은 `LayerTreeBuilder.Flatten`(잎만)을 써서 카테고리 펼침이 재조회마다 접힌다(기존 결함, 범위 밖).
  선택 행의 묶음만은 `RestoreLayerSelection` 이 펼친다.
- 동시 작업(지도 기울이기 제거 — MapView.xaml 툴바 · 보기 메뉴 · MapViewModel.Tilt)은 건드리지 않았다.

## 남은 일
- 실창(헤디드) 확인은 하지 않았다(창 시험 금지 규칙) — 실제 마우스 Ctrl/Shift 상태 · 스크롤 위치는 실기 몫.
