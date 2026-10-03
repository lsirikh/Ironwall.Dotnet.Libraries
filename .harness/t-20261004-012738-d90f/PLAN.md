# PLAN — 이벤트 콘솔 툴바 [트레이에 담기] 가 상세 칸 열리면 [⋯] 로 숨는 문제

## 목표
행을 골라 상세 칸이 열려도 [트레이에 담기]가 툴바에 늘 보이게. 사용자 지시: 한글 글자 단추 대신 아이콘 + 툴팁(이름), 설명은 "?" 도움말에.

## 결정
- 커널(ConsoleToolbar)의 넘침 로직은 건드리지 않는다 — 이벤트 콘솔 Extra 단추를 아이콘으로 바꿔 폭(≈330 → ≈140)을 줄이면 표준 폭(서랍 열림 484 포함)에서 접히지 않는다.
- 범위: Extra 의 [중단](Stop) · [이벤트 맵핑](LinkVariant) · [트레이에 담기](TrayArrowDown, Primary 유지). 억제 레일 [종료·취소 정리]는 다른 레일(트레이 단추와 동시 노출 안 됨) · 파괴적 동작이라 글자 유지.
- 여러 건 선택 시 아이콘 옆에 건수(QueueCountText/HasQueueCount), 전체 문구(QueueButtonText)는 툴팁 · AutomationProperties.Name.
- 공용 [삭제][갱신]은 커널 공용이라 다른 콘솔에 영향 → 범위 밖.
- "?" 도움말(EventsHelp: Events.Dashboard, Events.Tray)에 아이콘 설명 추가.

## 진행
- [x] EventDashboardView.xaml Extra 아이콘화
- [x] VM HasQueueCount/QueueCountText + 알림
- [x] EventsHelp 문구
- [x] ConsoleLayoutFitTests 회귀 시험 추가(아이콘 Extra 140 은 표준 폭에서 Overflow None)
- [x] dotnet test 솔루션 전부 통과 + Events.Ui(솔루션 밖) 콘솔 · 도움말 시험 79건 통과

## 남은 일
- 실창 확인(아이콘 Kind 표시) — 헤디드 시험은 이 실행에서 불가.
