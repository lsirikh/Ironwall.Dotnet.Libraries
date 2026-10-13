# PLAN — 운영 이벤트도 조치보고한다(서버 규격 FR-07)

## 목표
운영 카드 · 이벤트 콘솔 '운영' 레일에서 운영 이벤트를 조치보고(POST /api/events/actions, from_event_id=운영 id)한다.
원격 ACTION_REPORT(from_event.category_event=operation)는 종류 + 번호가 맞는 운영 카드만 닫는다. PRD = operation-event-card-prd v1.1.

## 결정
- 조치보고 창: 새 `OperationReportDialogViewModel`/`OperationReportDialogView`(탐지 · 장애 창과 같은 틀 · 문구 템플릿 · 사진 없음),
  `OpenEventReportDialogMessageModel{EventType="OPERATION"}`. ⚠ 호스트 `ConductorControlViewModel.HandleAsync(OpenEventReportDialogMessageModel)` 에
  "OPERATION" 분기 + 래퍼(OperationEventDialogViewModel) 가 필요하다(호스트는 이 저장소 밖 — PRD §10 · 최종 보고에 적음).
- 운영 카드 [닫기](서버 호출 없는 치우기)는 **남긴다** — 문 닫힘 알림처럼 조치할 일이 없는 운영 알림이 많다.
- [전체 조치보고]: 운영 카드도 서버 조치를 만든다(서버 규격상 조치 대상). 단 서버가 운영 조치를 못 받는 판본(6.3)이면 종전대로 치우기만.
- 6.3 판정: 카드 `IsReportSupported` = `IServerContractProbe.Contract >= V7_0`(IoC, 없으면 false — EventApiService 와 같은 보수 기본값).
  콘솔은 6.3 에서 운영 행이 0건이라(NOT_SUPPORTED) 별도 판정 불필요.
- 자동 조치보고는 운영에 넣지 않는다 — 운영은 큐(EventQueueManager)에 들어가지 않고(시간 만료 · 신호등 없음) 설정(Setup)에도 운영 항목이 없다.
- 원격: `EventCardKind.FromActionReport` 가 "operation"(category) · "Operation"(type) 을 운영으로 읽는다(종전엔 type "Operation" 이 탐지로 읽혀
  같은 번호 탐지 카드를 닫을 수 있었다). 종류 모름(null)은 운영 카드를 닫지 않는다(종전 규칙 유지).
- 운영 카드 조치는 큐 · 심볼을 건드리지 않는다(ReleaseQueueAndSymbol 건너뜀 — 운영은 큐 엔트리가 없고, 번호로 찾으면 같은 번호 탐지 엔트리를 뺄 위험).
- 콘솔 조치 내역(상세 조치 내역 절)은 운영 전용 조회 API 가 없어 범위 밖 — '조치' 칸(조치 있음/미조치)만 갱신.

## 진행
- [x] EventCardKind / 카드 VM / 패널 / 보고 메시지 / 창(OperationReportDialogViewModel · View, EventUiModule 등록)
- [x] 콘솔(상세 [조치보고] · 행 우클릭 · 트레이 KindOperation · 조치 열 · 원격 반영)
- [x] 시험 OPA-001~013(Tests/OperationEventActionReportScenarioTests.cs) + OPC-011/014 · FourKinds 읽기 전용 시험 · 열 수 시험(32→34) 갱신
- [x] PRD v1.1 — worktree docs/prds/operation-event-card-prd.md(gitignore) + 사본 .harness/t-20261013-175001-acde/operation-event-card-prd.md
- [x] Events.Ui 전체 2094 통과
- [x] sln 시험 — CameraPopup.Tests 시간 예산 시험 1건(388 ms > 50 ms) 실패, 단독 재실행 23/23 통과(부하성 타이밍 · 이번 변경과 무관), 나머지 전부 통과

## 남은 일(이 저장소 밖)
- 호스트 ConductorControlViewModel 에 "OPERATION" 분기 + OperationEventDialogViewModel 래퍼(PRD §10). 그 전엔 카드 · 콘솔 [조치보고] 창이 안 뜬다.
- 호스트 UiTests 실창 시험 OPA-H01~05 · DestructiveGuard 등록(PRD §9).

## 발견
- docs/ 는 .gitignore — PRD 원본(v1.0)은 메인 체크아웃 docs/prds 에 있다(이 작업은 worktree 쪽을 v1.1 로 썼다).
- 종전 결함: FromActionReport(null, "Operation") 이 탐지로 읽혀 같은 번호 탐지 카드를 닫을 수 있었다 — 운영으로 고침.
- EventMapRig IoC 에 IEventApiService · IActionReportGuard(패널과 공유) · IServerContractProbe(ServerContract 속성, 기본 V8_0) 추가.
