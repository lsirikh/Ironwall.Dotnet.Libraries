## 1. 묶음 머리(PIDS 장비 · 카메라 · PIDS 그룹 등) 클릭을 어떻게 다루나
- options: (a) 머리는 선택 대상 아님 · (b) 머리 클릭 = 그 안 전부 선택 · (c) 그냥 클릭 = 예전 그대로, Ctrl+머리 클릭 = 그 묶음 전부 더하기
- chosen: (c) — 그냥 클릭은 예전처럼 아무 일 없음(펼치기/접기는 셰브런), Ctrl+클릭은 그 묶음 잎 전부(접혀 있어도)를 더한다. 섹션 머리(오버레이 지도 · 이미지 · 심볼)는 대상 아님.
- why: 목표의 추천안 그대로. 실수로 수십 개를 고르는 일이 없고, 묶음 전체가 필요하면 Ctrl 한 번.
- effect: `LayerSelection.AddGroup` · `LayerPanelControl.OnRowPreviewMouseLeftButtonDown`. 그냥 클릭도 묶음 선택으로 바꾸려면 처리기의 Ctrl 조건만 빼면 된다.

## 2. Shift 범위와 접힌 묶음
- options: (a) 보이는 트리 순서만(접힌 묶음 안은 건너뜀) · (b) 데이터 전체 순서(접힌 묶음 안도 포함)
- chosen: (a). 기준 행이 나중에 접힌 묶음 안으로 숨으면 Shift+클릭은 그 행 하나에서 다시 시작한다.
- why: 보이지 않는 행이 몰래 골라지면 지도에서 예상 밖 마커가 같이 움직인다(편집 모드 연동). 탐색기 관례.
- effect: `LayerSelection.VisibleLeaves` · `SelectRange`. (b)로 바꾸려면 VisibleLeaves 대신 `LayerTreeBuilder.Flatten` 을 쓰면 된다.

## 3. 지도에서 고르면 접힌 묶음을 펼치나
- options: (a) 새로 골라진 행의 접힌 조상을 펼치고 첫 행이 보이게 굴림 · (b) 펼치지 않고 데이터만 표시
- chosen: (a) — 지도 클릭 · Ctrl+클릭 · 러버밴드 · Ctrl+A 로 새로 골라진 행만(이미 골라져 있던 행은 다시 펼치지 않음).
- why: 목표 "그 항목이 보이게 스크롤(접힌 묶음이면 펼침)". 지도 Ctrl+A 는 여러 카테고리를 한꺼번에 펼친다(대가).
- effect: `MapViewModel.SyncLayerSelectionFromMap(reveal: true)`. 펼치지 않으려면 `LayerSelection.ExpandToShow` 호출만 빼면 된다.

## 4. 잠긴 · 숨긴 항목을 패널에서 고르면(편집 모드)
- options: (a) 패널에는 남기고 지도로만 안 보냄 · (b) 지도 규칙과 같게 — 지도로 안 보내고 패널도 지도 선택을 비춰 풀림
- chosen: (b). 대상 규칙은 지도 Ctrl+A 와 같다: 잠김(`IsLocked`) · 숨김(`IsLayerEnabled=false` — 레이어 체크 끔) 제외. 줌 때문에 안 보이는 것은 패널에서 고를 수 있다(지도 Ctrl+A 와 다른 점 — 패널에서는 그 행을 직접 가리켰으므로).
- why: 편집 모드에서 "패널 선택 = 지도 선택" 이 유지돼야 묶음 회전 · 이동 · 삭제 · 속성 판 대상이 헷갈리지 않는다.
- effect: `MapViewModel.PushLayerSelectionToMap` 의 조건식. (a)로 바꾸려면 끝의 `SyncLayerSelectionFromMap` 호출을 빼면 된다.

## 5. 오버레이 이미지 행과 지도 Ctrl+A
- options: (a) 패널 Ctrl+A · 클릭은 오버레이 이미지 행도 지도 선택으로 보냄 · (b) 지도 Ctrl+A 처럼 이미지는 빼기
- chosen: (a) — 패널에서 이미지 행을 고르면 그 이미지 마커가 지도에서 골라진다(지도 Ctrl+클릭으로 이미지를 더하는 것과 같다). 지도 Ctrl+A 자체 규칙(이미지 제외)은 그대로.
- why: 목표 "오버레이 이미지 · 오버레이 지도 · 심볼 모두 같은 규칙". 패널 Ctrl+A 는 "보이는 행 전부"로 정의됐다.
- effect: `PushLayerSelectionToMap` 에 `m is GMapImageMarker` 제외 조건을 더하면 (b).

## 6. 지도 마커가 없는 잎(오버레이 지도 행 등)
- options: (a) 지도 → 패널 동기가 그 행을 건드리지 않음(패널 안 선택 유지) · (b) 지도 선택이 바뀔 때마다 풀림
- chosen: (a).
- why: 오버레이 지도는 지도 위 편집 마커가 아니라서 지도에서 고를 길이 없다 — 지도 클릭마다 풀리면 패널에서 고를 수가 없다.
- effect: `SyncLayerSelectionFromMap` 의 `want = null` 분기.

## 7. 편집 모드를 켜고 끌 때
- options: 켜짐 — (a) 패널 선택을 지도 편집 선택으로 이어 감 · (b) 패널 선택을 지움 / 꺼짐 — (c) 지도 선택만 풀고 패널 선택은 남김 · (d) 둘 다 지움
- chosen: 켜짐 (a), 꺼짐 (c).
- why: 운영 중 패널에서 골라 두고 편집을 켜면 바로 그 집합을 편집할 수 있다. 끌 때 지도 선택 해제는 기존 규칙(ClearAllSelections)이고, 편집 모드가 아닐 때 패널 선택은 패널 안에서만 산다(예전 동작 — 지도와 무관).
- effect: `IsEditModeEnabled` 세터 끝의 `PushLayerSelectionToMap` 호출. (b)/(d)는 그 자리에서 `LayerSelection.Clear` 로 바꾸면 된다.

## 8. 심볼 잎 행의 UIA 선택 상태 표현
- options: (a) 심볼 잎 목록도 ListBox 로 바꿔 SelectionItemPattern · (b) 기존 ItemsControl 그대로 두고 항목 칸에 AutomationId + ItemStatus "선택됨"(FenceCanvas 관용구)
- chosen: (b). 오버레이 행(이미 ListBox)은 ListBoxItem.IsSelected 를 노드 선택에 묶어 SelectionItemPattern 이 그대로 따른다.
- why: (a)는 레이어 패널의 ListBox 목록 = 오버레이 순서 목록 둘뿐이라는 기존 계약(MAP-051 시험)을 깨고, 끌기 커널 · 휠 스크롤 동작에 영향이 크다.
- effect: `LayerPanelStyle.xaml` CategoryNodeTemplate 의 ItemContainerStyle. 심볼 칸 id = `GMaps.LayerPanel.Symbol.{심볼 Id}`.

## 9. 키보드 초점만 옮기기
- options: (a) Ctrl+↑/↓ = 선택은 그대로 두고 초점만 이동(Ctrl+Space 로 더하기 · 빼기) · (b) 초점 이동 없이 Ctrl+Space 는 마지막 행만 토글
- chosen: (a) — 탐색기 관례. 초점 행은 패널에 키보드 초점이 있을 때 가는 고리로 보인다(오버레이 행은 기존 ListBoxItem 초점 고리).
- why: 목표의 Ctrl+Space 토글이 쓸모 있으려면 선택과 다른 행으로 초점을 옮길 수 있어야 한다.
- effect: `LayerSelection.HandleKey`, 노드 `IsCurrent`.

## 10. 패널에 초점이 있을 때 지도 창 단축키
- options: (a) 패널 선택 키(↑/↓ · Space · Ctrl+A · Esc)만 지도 창 후킹이 양보 · (b) 모든 키 양보
- chosen: (a). Delete · Ctrl+Z · ←/→ 등은 예전처럼 지도가 받는다(편집 모드에서는 패널 선택 = 지도 선택이라 Delete 는 고른 것을 지운다).
- why: 창 전역 후킹이 ↑/↓ 를 마커 이동으로, Ctrl+A 를 지도 전체 선택으로 가져가 패널 키보드 폴백이 먹지 않았다. 최소 변경.
- effect: `OnMapPreviewKeyDownForGroup` 의 양보 한 줄 · `LayerPanelControl.IsSelectionKey`.
