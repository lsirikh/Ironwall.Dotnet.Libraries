# PLAN — 시나리오 상태표 남은 라이브러리 항목(rem-lib-a, 112개) 덮기 (t-20261011-091723-6450)

## 목표
첨부 rem-lib-a.txt 의 112개 id(지도 · 기타 · 이벤트 · 계정 · 보고서 · 성능) 각각에 결론:
(가) 헤드리스 시험으로 "기대" 단언 + `[Trait("Scenario", id)]` / (나) 헤디드 필요 — 이유 / (다) 관찰 항목 — 이유.
대상: GMaps.Ui · tests/GMaps.* · Events.Ui · Accounts.Ui · Reports.Ui · CameraPopup(창 없는 부분) · Utils.

## 규칙(요약)
창 금지(Window.Show · 화면 밖 창 금지) · 지도 DB/운영 서버/실제 NATS 금지(가짜만) · 이벤트는 가짜 API 기록 먼저 → 같은 id NATS ·
결함은 수정 + 빨강→초록 기록 · 기존 시험 본문 불변(표지 한 줄만) · 공개 형식은 더하기만 · 새 .cs UTF-8 BOM · x:Name 불변 · 대외비 명칭 금지.

## 진행
- [x] 첨부 2개 읽기, 직전 작업(t-20261011-081026-0754) PLAN 읽기
- [x] 기반 빌드(GMaps.Ui · 시험 프로젝트 전부 · sln) 오류 0
- [ ] 작업자 W1~W8 병렬 진행 중(결정 D1) — 보고를 받으면 아래 결과표 · 결함 표에 옮긴다
- [ ] 결함 수정(본 세션) · 빨강→초록
- [ ] 전체 회귀 · 결과표

## 결정
- D1: 작업자 8명에게 시험 프로젝트별로 나눈다(같은 프로젝트 동시 빌드 = obj 충돌). 기반은 본 세션이 먼저 빌드(오류 0), 작업자는 자기 시험 프로젝트만 `--no-dependencies` 빌드 · `--no-build` 실행. 제품 코드 수정은 본 세션 한 곳에서만(결함은 빨강 증거로 보고받음).
  - W1 NAV → tests/GMaps.MapNav.Tests: MAP-001 · 006 · 007 · 010 · 011 · 012 · 013 · 014 · 021 · 022 · 023 · 029 · 039 · 040 · 194 · 195 · 196
  - W2 LAYER → tests/GMaps.MapLayer.Tests: MAP-041 · 042 · 044 · 049 · 051 · 053 · 054 · 056 · 061 · 062 · 074 · 075 · 076 · 079
  - W3 EDIT → tests/GMaps.MapEdit.Tests: MAP-086 · 088 · 091 · 093 · 094 · 096 · 109 · 110 · 132 · 134
  - W4 PROP3D → tests/GMaps.PropertyPanel.Tests · tests/GMaps.Housing.Tests: MAP-122 · 123 · 126 · 127 · 129 · 146 · 147 · 148 · 149 · 150
  - W5 AUX → tests/GMaps.MapAux.Tests: MAP-085 · 153 · 154 · 157 · 160 · 161 · 162 · 163 · 165 · 166 · 168 · 173 · 177 · 178 · 179 · 191 · AUX-012 · 017 · 018 · 021
  - W6 EVENTS → Events.Ui/Tests (+ Nats/Tests): MAP-140 ~ 145 · EVM-022 · 023 · 031 · 037 · 068 · 069 · 097 · 127 · 128 · 129 · PERF-016
  - W7 CAMERA → CameraPopup.Tests · CameraPopup.Host.Tests · tests/GMaps.Ui.Tests: MAP-183 ~ 187 · EVM-137
  - W8 SHELL → Utils.Tests · Accounts.Ui.Tests · Reports.Ui/Tests: ACC-034 · 044 · RPT-010 · 020 · AUX-002 · 003 · 006 · 023 ~ 026 · 028 ~ 034

## 발견

## 남은 일
