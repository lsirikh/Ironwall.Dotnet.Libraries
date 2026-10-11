# PLAN — 직전 작업(t-20261011-091723-6450) 남은 것 마치기: MAP-056 결함 + rem-lib-a2 71개 (t-20261011-094654-8195)

## 목표
1. `LayerLeafActionScenarioTests.should_hide_that_kind_with_a_single_click_on_the_kind_row_checkbox`(MAP-056 · LAY-D4) 빨강→초록 — 제품 수정, 시험 단언 불변.
2. 첨부 rem-lib-a2.txt 71개 id 각각 결론: (가) 헤드리스 시험 '기대' 단언 + `[Trait("Scenario", id)]` / (나) 헤디드 필요 — 이유 / (다) 관찰 항목 — 이유. 최대한 (가).
   `CameraPopup.Tests/Unit/MapCameraPopupRig.cs` 로 MAP-183~187 시험.
3. 작업자 보고를 전부 받아 반영한 뒤에만 끝낸다(아래 진행 체크박스 전부 [x]).
4. 손댄 시험 프로젝트 전부 실행(창 시험 --filter 제외) → 실패 0 기록.

## 규칙(요약)
창 금지(Window.Show · 화면 밖 창 금지) · 지도 DB/운영 서버/실제 NATS 금지 · 이벤트는 가짜 API 먼저 → 같은 id NATS ·
탐지/장애는 조치보고 전 같은 대상 중복 없음 · 기존 시험 본문 불변(표지 한 줄만) · 공개 형식은 더하기만 · 새 .cs UTF-8 BOM · x:Name 불변.

## 진행
- [x] 첨부 2개 · 직전 PLAN(bb161f0d) 읽기 — 71개 id 모두 아직 Scenario 표지 없음(Grep 확인)
- [x] MAP-056 원인 규명 · 수정(LayerPanelStyle.xaml 두 체크박스 IsThreeState True→False)
- [ ] MAP-056 빨강→초록 실행 확인
- [ ] 기반 빌드(GMaps.Ui · 시험 프로젝트) 오류 0
- [ ] 작업자 W1~W7 보고 수령 · 반영
- [ ] 작업자가 보고한 결함 수정(본 세션) · 빨강→초록
- [ ] 전체 회귀(손댄 시험 프로젝트 전부) 실패 0 · 결과표

## 결정
- D1(MAP-056): 원인 = `LayerPanelStyle.xaml` NodeRowTemplate(그룹/종류 행)과 섹션 머리 전체 체크의 `IsThreeState="True"`.
  WPF ToggleButton.OnToggle 은 IsThreeState=True 일 때 켜짐 → null(중간)로 보내고, `LayerTreeNode.IsChecked` 세터는 null 을 자식에 내리지 않는다
  (`value.HasValue` 조건, 중간은 집계 표시 전용) → 한 번 눌러도 아무것도 안 숨음. 잎 체크박스는 두 상태라 한 번에 꺼져 어긋났다.
  수정 = `IsThreeState="False"`(두 곳). 중간 표시는 바인딩으로 그대로 보인다(IsThreeState 는 누름 순환만 정함).
  누름 순환: 켜짐 → 꺼짐 · 꺼짐 → 켜짐 · 중간 → 꺼짐(WPF 기본 — IsThreeState 와 무관하게 중간 누름은 꺼짐). 모델 세터 · 집계 로직은 그대로.
- D2: 작업자를 시험 프로젝트별로 나눈다(같은 프로젝트 동시 빌드 = obj 충돌). 제품 코드 수정은 본 세션만(작업자는 결함을 빨강 증거로 보고).
  - W1 NAV → tests/GMaps.MapNav.Tests: MAP-001 · 011 · 013 · 014 · 021 · 022 · 023 · 029 · 039 · 040 · 194 · 195 · 196
  - W2 EDIT → tests/GMaps.MapEdit.Tests: MAP-093 · 094 · 109 · 110 · 132 · 134
  - W3 PROP3D → tests/GMaps.PropertyPanel.Tests · tests/GMaps.Housing.Tests: MAP-122 · 123 · 126 · 127 · 129 · 149
  - W4 AUX → tests/GMaps.MapAux.Tests: MAP-085 · 153 · 154 · 157 · 160 · 161 · 162 · 163 · 165 · 166 · 168 · 173 · 177 · 178 · 179 · 191 · AUX-012 · 017 · 018 · 021
  - W5 EVENTS → Events.Ui(Tests 폴더): MAP-142 · PERF-016
  - W6 CAMERA → CameraPopup.Tests(MapCameraPopupRig): MAP-183 ~ 187 · EVM-137
  - W7 SHELL → Accounts.Ui.Tests · Reports.Ui · (WINDY/말풍선은 있는 곳): ACC-034 · 044 · RPT-010 · 020 · AUX-002 · 003 · 006 · 023 ~ 026 · 028 ~ 034
  (MAP-056 은 본 세션이 tests/GMaps.MapLayer.Tests 에서 직접.)

## 발견

## 남은 일
- 위 진행 체크박스

## id 별 결과표
(작업자 보고 수령 후 채움)
