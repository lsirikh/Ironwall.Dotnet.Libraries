# 운영 이벤트 카드 PRD (operation-event-card) v1.1

- 작성: 2026-10-13 · 하네스 t-20261013-163518-dbb4 (v1.0)
- 개정: 2026-10-13 · 하네스 t-20261013-175001-acde (v1.1 — 운영 이벤트 조치보고)
- 배경(v1.0): 사용자 보고 — "운영이벤트가 이벤트 패널 쪽에 이벤트 카드로 나오지 않는다". 이벤트 패널(`EventCardListPanelViewModel`)엔 탐지 · 장애 카드만 있고,
  `OperationEventNatsSyncService` 는 개폐만 지도에 반영하고 카드는 "별도 차수" 로 미뤄 두었다.
- 배경(v1.1): 사용자 질문 — "운영이벤트 조치보고 없는건가?". 서버는 운영 이벤트 조치보고를 받는다(api-test-server operation-event PRD **FR-07**
  "운영 이벤트도 조치보고 대상이므로 Union 에 포함" — `ActionEventResponse.from_event: Union[Detection, Malfunction, Connection, OperationEventResponse]`,
  "POST /api/events/actions (from_event_id = 이 운영 이벤트 id) 로 남기십시오", `routers/actions.py` 운영 이벤트 eager-load · 조치 자동 연결).
  v1.0 §3 "조치 대상 아님" 과 이벤트 콘솔 '운영' 레일(t-20261003-231054-7247)의 "조회 전용 · 탐지 · 장애만 조치" 는 서버 규격과 어긋났다 → **뒤집는다**.

## v1.1 바뀐 점 요약
| 곳 | v1.0 | v1.1 |
|---|---|---|
| 운영 카드 | [닫기] 만 | **[조치보고]**(탐지 · 장애와 같은 창 · 문구 · 사진 없음) + [닫기](조치 없이 치우기) |
| [전체 조치보고] | 운영 카드는 치우기만 | 운영 카드도 **서버 조치 생성** + ACTION_REPORT(서버가 운영 조치를 받지 못하는 6.3 이면 종전대로 치우기만) |
| 원격 ACTION_REPORT | 운영 카드 안 건드림 | from_event `category_event=operation`(또는 `type_event=Operation`)이면 **종류 + 번호**가 맞는 운영 카드만 닫음 |
| 콘솔 '운영' 레일 | 조회 전용 | 행 우클릭 · 상세 [조치보고] · 조치 트레이 담기(끌기 · 툴바) · 일괄 [조치 적용] · '조치' 열(조치 있음/미조치) |
| 자동 조치보고 | 없음 | 없음(유지 — §3.4) |

## 1. 무엇을 카드로 띄우나 (v1.0 그대로)
| 항목 | 결정 |
|---|---|
| 대상 | `cmd=OPERATION_EVENT` 알림(m_type≠RSP) **전부** — 문 열림/닫힘(함체 · 통문) · 환경 경보(고온 · 저온 · 고습 · 저전압 · 과전류 · 진동 · UPS) · 모르는 사유 |
| 글 | 사유는 `OperationEventText.ReasonText`(이벤트 콘솔 '운영' 레일과 같은 공용 글), 모르는 사유는 원문 그대로. 심각도는 `SeverityText`, 상세는 `DetailText` |
| 번호 없는 본문(`body.id` ≤ 0) | 카드 없음(중복을 가를 열쇠가 없다) — 로그만. 개폐 반영은 종전대로 |
| 장비가 지워진 본문(`device: null`) | 카드는 띄운다(1행 "삭제된 장비" 또는 서버 스냅샷 이름). 개폐 반영은 종전대로 건너뜀 |

## 2. 카드 모양
- 탐지 · 장애 카드와 같은 틀(높이 200, 폭 150~224, 머리 띠 + 본문 + 바닥).
- **운영 표지**: 머리 띠에 아이콘 + 글 "운영 이벤트"(색만으로 가르지 않는다). 아이콘은 문 사유=`DoorOpen`, 그 밖=`Information`.
  경고 이상(WARNING · CRITICAL)은 본문에 심각도 글("경고" · "심각")을 같이 보인다.
- 본문: ① 구역 · 장비(`ZoneDeviceText`) ② 사유 글(굵게) ③ 심각도 글. 바닥: **[조치보고](v1.1)** + [닫기] 단추 + 시각.
- 뒤집기(상세) 없음 — 상세 한 줄은 본문 아래(말줄임)로.

## 3. 조치 대상인가 — **그렇다**(v1.1, 서버 FR-07)
### 3.1 카드 [조치보고]
- 탐지 · 장애 카드와 같은 길: 단추 → 조치보고 창(`OperationReportDialogViewModel`, 문구 템플릿 = 서버 조치보고 문구 관리 목록 · '기타'+내용, 사진 없음)
  → `OperationEventCardViewModel.SendActionDetailed` → `POST /api/events/actions {user, content, from_event_id = 운영 id}`.
- **API 저장 성공 뒤에만** ① `OperationReportedMessageModel` → 카드 목록이 그 카드를 닫고 닫힘을 기억(재배달에 다시 뜨지 않음) ② NATS ACTION_REPORT 발행 요청
  (`SendActionRequestMessage{EventType=Operation, ServerActionJson=201 data 원문}`) ③ 모델 `action_reported`=참. 실패하면 카드 · 창 유지, 발행 없음.
- 멱등 가드 열쇠 = ("operation", 번호) — 같은 번호의 탐지 · 장애 조치를 막지 않는다(`IKeyedActionReportGuard`).
- 운영 카드 조치는 큐(EventQueueManager) · 심볼을 **건드리지 않는다** — 운영은 큐에 없고, 번호로 엔트리를 찾으면 같은 번호의 탐지 엔트리를 뺄 위험이 있다.
- 서버가 운영 조치를 받지 못하는 판본(계약 7.0 미만 = 운영 6.3.2 — `/api/events/operations` 경로 없음)이면 단추를 **숨긴다**
  (`IsReportSupported` = `IServerContractProbe.Contract >= V7_0`, 프로브 없으면 숨김 — EventApiService 기본값 6.3 과 같다).
- 창 메시지: `OpenEventReportDialogMessageModel{EventType="OPERATION"}`. ⚠ **호스트 작업 필요**(§10).

### 3.2 [닫기] — 남긴다
- 조치 없이 치우기(서버 호출 · ACTION_REPORT 없음, 닫힘만 기억). 문 닫힘 알림처럼 조치할 일이 없는 운영 알림이 많아 조치를 강제하지 않는다.
- 시간이 지나도 저절로 사라지지 않는다(문 열림을 놓치지 않게) — 표시 상한(500)에서 가장 오래된 것부터 밀려난다(v1.0 그대로).

### 3.3 [전체 조치보고]
- 운영 카드도 탐지 · 장애와 **같게** 서버 조치(내용 "일괄처리")를 만들고 ACTION_REPORT 를 낸다(서버 규격상 조치 대상 — 패널이 비워지는 기대와도 맞다).
- 예외: `IsReportSupported=false`(6.3) 면 종전대로 치우기만(경로 없는 서버에 보내 일괄 처리 전체가 실패로 멈추지 않게).

### 3.4 자동 조치보고 — 넣지 않는다
- 자동 조치보고는 EventQueueManager 의 엔트리 만료(탐지 `TimeDiscardSec` · 장애 `MalfunctionTimeDiscardSec`)로 돈다. 운영은 큐에 들어가지 않고(§4)
  설정(`IEventSetupModel`)에도 운영 항목이 없다. 운영 카드는 "저절로 사라지지 않는다"(§3.2)가 v1.0 결정이라 시간 만료 조치는 그 결정과도 어긋난다.

### 3.5 원격 ACTION_REPORT
- 호스트 라우터(`NatsDomainService`)가 쓰는 `EventCardKind.FromActionReport(category_event, type_event)` 가 `"operation"` · `type_event "Operation"` 을
  운영(`EventCardKind.Operation`)으로 읽는다 → `CloseByRemoteActionReport("operation", id)` 가 **종류 + 번호**가 맞는 운영 카드만 닫는다(호스트 수정 불필요).
  - 종전 결함: 판별자 없이 `type_event "Operation"` 만 오면 탐지로 읽혀 **같은 번호의 탐지 카드를 닫을 수 있었다** — 함께 고쳤다.
- 종류를 모르는 원격 조치(null)는 운영 카드를 닫지 않는다(`EventCardKind.Of` 에 운영을 넣지 않음 — v1.0 규칙 유지).
- 열린 운영 조치보고 창은 같은 종류 + 번호의 원격 조치를 받으면 [확인]을 끈다(탐지 · 장애 창과 같은 규칙, `ListKeyOf` 로 비교).

## 4. 신호등 · 배지 · 소리 (v1.0 그대로)
- 신호등(탐지 · 장애 활성 건수) · 셸 배지: **넣지 않는다** — 운영은 큐(EventQueueManager)에 들어가지 않는다.
- 패널 머리의 "이벤트 수"(목록 카드 수)에는 들어간다(보이는 카드 수).
- 소리: 운영 소리 설정이 없다(`Sounds` 에 Operation 항목 없음) → **소리 없음**.

## 5. 묶음 · 상한 · 중복 · 순서 (v1.0 그대로)
- 입구는 탐지 · 장애와 같은 `EnqueueCard` → 150 ms 묶음 버퍼 → 맨 위에 삽입 → 상한 500.
- 열쇠 = ("operation", 서버 번호). 같은 봉투 재수신 · 같은 이벤트 새 봉투 → 한 장.
- 닫은(조치 · [닫기] · 원격) 운영 카드는 기억(256개) — 브로커가 같은 봉투를 다시 배달해도 다시 뜨지 않는다.
- 순서: 받은 순서대로 맨 위(최신 위).

## 6. 지도 · 개폐 (v1.0 그대로)
- 카드 더블클릭 → `LocateCardAsync` → `MapLocateRequest`(장비가 있으면).
- 개폐 사유면 지도 문 상태(`SetDoorState`)는 종전대로 같이 바뀐다. 조치보고는 문 상태를 바꾸지 않는다.

## 7. 배선
- (v1.0) `OperationEventNatsSyncService` 새 생성자 · `OperationEventCardMessage` · `EventCardListPanelViewModel : IHandle<OperationEventCardMessage>`.
- (v1.1 — 모두 더하기만)
  - `OperationEventCardViewModel`: 생성자(ea, log, model) · `SendAction`/`SendActionDetailed` 재정의 · `IsReportSupported`.
  - `OperationReportedMessageModel`(Models/Messages.cs) — 카드 목록 · 이벤트 콘솔이 듣는다.
  - `OperationReportDialogViewModel`(+ `Views/Dialogs/OperationReportDialogView.xaml`), `EventUiModule` 에 `AsSelf().SingleInstance()` 등록
    (베이스 `EventReportDialogViewModel` 로는 등록하지 않음 — 종전 해석 결과 보존).
  - `EventCardListPanelViewModel : IHandle<OperationReportedMessageModel>`, `OnButtonAction` 운영 분기.
  - `EventCardKind.FromActionReport` 운영 판독. `ActionTrayDrop.KindOperation` · `IsReportableKind` 에 운영.
  - `OperationEventPanelViewModel.ReportCommand` · `CanReportRow`.

## 8. 이벤트 콘솔 '운영' 레일 (v1.1)
- 이벤트 자체는 여전히 고치지 · 지우지 않는다(서버가 만든 기록). 조치만 한다 — 탐지 · 장애 레일과 같은 계약:
  - 행 우클릭 '조치보고'(`Console.Events.Row.Report`) · 상세 동작 줄 [조치보고] → 같은 운영 조치보고 창.
  - 손잡이 열(끌어서 조치 트레이) · 툴바 [트레이에 담기] · 트레이 [조치 적용](일괄) — 줄마다 운영 카드 경로(`SendActionDetailed`)로 보낸다.
  - '조치' 열(조치 있음 / 미조치 — 서버 `action_reported`). 상세 '조치' 칸은 보고 뒤 다시 그린다.
  - 원격 조치(RemoteActionReportedMessage kind=operation) → 그 행을 '조치 있음' 으로.
- 범위 밖: 상세의 '조치 내역' 절 — 운영 원본별 조치 조회 API(`GET .../operations/{id}/actions` 류)가 클라 API 서비스에 없다. '조치 내역 보기' 메뉴도 두지 않는다.
- 6.3: 운영 목록이 비고(NOT_SUPPORTED 안내) 행이 없으니 조치 경로도 열리지 않는다.

## 9. 자동화 id (호스트 UiTests 실창 시험용 — 호스트는 이 저장소 밖)
- 카드 뿌리: `Events.Card.Operation.{eventId}` · [닫기] `Events.Card.Operation.{eventId}.Close` · **[조치보고] `Events.Card.Operation.{eventId}.Report`(v1.1)**
- 운영 조치보고 창(v1.1): 뿌리 `Events.OperationActionReport.Root` · [확인] `Events.OperationActionReport.Ok` · [취소] `Events.OperationActionReport.Cancel` ·
  문구 `Events.OperationActionReport.Phrase.{n}` · 기타 `Events.OperationActionReport.Etc` · 내용 `Events.OperationActionReport.Memo` ·
  값 `Events.OperationActionReport.Field.{Device|Time|Reason|Severity|Detail}`
- 콘솔: 운영 그리드 `Console.Events.Grid.Operation` · 행 `Console.Events.Row.{id}` · 우클릭 `Console.Events.Row.Report` · 손잡이 `Console.Events.Row.Handle`
- 실창 시험 제안:
  - OPC-H01(v1.0, 수정): OPERATION_EVENT(GATE_OPEN) 송신 → 카드 맨 위 · **`.Report` 단추 있음(8.0 서버)** · [닫기] 누르면 사라짐 · 재송신에 안 뜸.
  - OPA-H01: 카드 `.Report` → 창 `Events.OperationActionReport.Root` → 문구 고르고 `.Ok` → 카드 사라짐 · 서버 조치 1건(from_event_id) · ACTION_REPORT 1건.
  - OPA-H02: 다른 GIS(주입기)가 같은 운영 id 로 조치 → ACTION_REPORT(category operation) → 이 GIS 의 운영 카드 닫힘 · 같은 번호 탐지 카드 유지.
  - OPA-H03: 콘솔 '운영' 레일 행 우클릭 `Console.Events.Row.Report` → 같은 창 → [확인] → 행 '조치 있음'.
  - OPA-H04: 콘솔 '운영' 행 손잡이를 트레이로 끌기 → [조치 적용] → 행 '조치 있음' · 지도 옆 운영 카드 닫힘.
  - OPA-H05: 6.3 서버에 붙은 bin 에서 운영 카드에 `.Report` 가 없다.
- **DestructiveGuard 등록 필요**(서버에 조치 기록을 만드는 단추): `Events.Card.Operation.{id}.Report`(창을 연다) · `Events.OperationActionReport.Ok`(실제 POST).
  탐지 · 장애 `.Report` · `Events.MalfunctionActionReport.Ok` 와 같은 등급으로 등록한다.

## 10. 호스트(Dotnet.Monitoring.Solution) 작업 — 이 저장소 밖, 요청
- `ConductorControlViewModel.HandleAsync(OpenEventReportDialogMessageModel)` 에 분기 추가:
  `else if (message.EventType == OperationReportDialogViewModel.EventType /* "OPERATION" */) await conductorShell.ActivateItemAsync(IoC.Get<OperationEventDialogViewModel>(), ct);`
- 래퍼 `OperationEventDialogViewModel`(+ 뷰) — `MalfunctionEventDialogViewModel` 과 같은 모양(생성자에 `OperationReportDialogViewModel`, 활성/비활성 전달).
- 이 작업 전에는 카드 · 콘솔 [조치보고] 를 눌러도 창이 뜨지 않는다(호스트가 모르는 EventType 은 무시) — 트레이 [조치 적용] · [전체 조치보고] · 원격 종결은 호스트 수정 없이 동작.

## 11. 시험(헤드리스 · 창 없음)
- v1.0 `Tests/OperationEventCardScenarioTests.cs`(OPC-001~015) — OPC-011 은 "운영도 서버 조치" 로, OPC-014 는 "[조치보고] 단추 있음" 으로 v1.1 에 맞춰 갱신.
- v1.1 `Tests/OperationEventActionReportScenarioTests.cs`, `[Trait("Scenario","OPA-0nn")]`:
  OPA-001 카드 [조치보고] → API 본문(from_event_id · content · user) · 성공 뒤 카드 종결 · NATS(EventType Operation · ActionId) · 재배달에 안 뜸 ·
  OPA-002 서버 거절 → 카드 유지 · NATS 없음 · 다시 보고 가능 · OPA-003 같은 번호 탐지 카드 · 큐 · 심볼 불변 ·
  OPA-004 원격 종류 판독(category/type) · OPA-005 원격 operation → 운영 카드만 종결(같은 번호 탐지 유지) · OPA-006 원격 detection → 운영 카드 유지 ·
  OPA-007 열린 운영 창 [확인] 끄기(종류 + 번호) · OPA-008 [전체 조치보고] 운영 카드마다 서버 조치 + NATS · OPA-009 6.3 / 프로브 없음 → 단추 숨김 · 일괄은 치우기만 ·
  OPA-010 콘솔 행 우클릭 → 창 → [확인] → 지도 카드 종결 · 행 '조치 있음' · OPA-011 콘솔 트레이 담기 · [조치 적용] · OPA-012 콘솔 원격 조치 반영 · OPA-013 XAML 배선.
- `EventConsoleFourKindsTests` 의 운영 읽기 전용 시험: 고치기 · 지우기는 꺼짐 유지, 조치보고 · 트레이 끌기 · 상세 [조치보고] 는 켜짐으로 갱신.
- `EventDataGridColumnFloorTests`: 운영 그리드 2열(손잡이 · 조치) 추가로 열 수 32 → 34.
