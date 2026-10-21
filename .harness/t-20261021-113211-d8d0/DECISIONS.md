# DECISIONS — L3 이벤트 · NATS 수신 · 시스템 이벤트

## 1. 재연결 사건을 무엇으로 알릴까
- options: INatsService 에 공개 사건 · EventAggregator 메시지 NatsReconnectedMessage
- chosen: `INatsService.Reconnected`(EventHandler) — 인터페이스 기본 구현(빈 add/remove)을 둠
- why: NatsService 는 EventAggregator 를 받지 않는다. 기본 구현이라 호스트 시험의 MockNatsService · 도구의 GuardedNatsService 가 고칠 것 없이 그대로 빌드된다.
- effect: 호스트 H1 은 `nats.Reconnected += …` 로 받는다. 메시지로 바꾸려면 받는 쪽 하나에서 사건을 메시지로 옮기면 된다.

## 2. 억제 202 결과형
- options: 전용 결과형(반환형 변경) · 전용 예외형
- chosen: `EventSuppressedException(ScheduleId)` 를 던진다
- why: Insert*EventAsync 반환형 · 호출부를 그대로 두고 패널이 catch 한 줄로 실패와 가를 수 있다.
- effect: 패널은 억제를 실패로 세지 않고 Draft 행을 치운 뒤 "정비 창(schedule_id N)에 걸려 저장되지 않았습니다" 를 보인다.

## 3. 삭제 409 사유를 베이스 패널에 어떻게 넘길까
- options: deleteApi 반환형 확장 · 전용 예외
- chosen: `DeleteBlockedException`(BaseDataGridMultiPanelViewModel.cs 안) — 베이스가 일반 실패와 따로 모은다
- why: 장비 · 서버 패널 등 다른 소비자 위임(Func<int,CT,Task<bool>>)을 바꾸지 않는다. 8.0.15 의 동시성 409(details.retryable=true)는 다시 보내면 되는 실패라 일반 실패로 둔다.
- effect: 통지 문구 "조치보고가 있는 이벤트는 조치보고를 먼저 지워야 삭제됩니다(Id …)".

## 4. 억제 범위 '운영' 의 계약 판정 위치
- options: 서랍 · 패널 VM 에 프로브 주입 · IoC 로 늦게 찾기
- chosen: `SuppressionRequestBuilder.ContractFromIoC()`(없으면 V6_3) + 서랍 생성자에 선택 인자 `contract`
- why: EventApiService 와 같은 보수적 기본값. 시험은 인자로 V6_3 · V8_0 을 넣는다.
- effect: 콤보 '운영' 은 7.0 이상에서만 보이고, 6.3 에서 고르면 저장 전에 막고 안내한다.

## 5. SYNC_MALFUNCTION · SYNC_ACTION_REPORT · SYNC_OPERATION_EVENT 를 서비스 몇 개로
- options: cmd 마다 서비스 · 한 서비스
- chosen: `EventSyncNatsService` 하나(봉투 id 중복 거르기 공유)
- why: 셋 다 "알림형 → 다시 읽기 → 이미 가진 카드 · 행 고치기" 로 모양이 같다.
- effect: 나눠야 하면 cmd 분기 셋을 각 클래스로 옮기면 된다.

## 6. 장애가 장애 아닌 종류로 재분류되면
- options: 카드 유지 · 카드 · 큐 닫기
- chosen: 장애 카드 · 큐 엔트리를 닫는다(서버 삭제와 같은 길, 토스트 없음)
- why: 이 목록 · 큐의 장애가 더는 아니다. 조치됨(action_reported=True)은 원격 조치보고와 같은 길로 닫는다.
- effect: EventCardListPanelViewModel.HandleAsync(MalfunctionSyncedMessage).

## 7. 운영 카드 '있을 때만' 판정
- options: 카드 목록이 직접 다시 읽기 · 서비스가 판정 함수를 받기
- chosen: 서비스가 `Func<int,bool> hasOperationCard` 를 받는다(DI 에서 카드 목록의 HasOperationCard 로 잇는다)
- why: 다시 읽기 · 404=삭제 · 메시지 발행을 서비스 한 곳에 둔다. 판정이 안 되면 다시 읽는다(안전).

## 8. 시스템 이벤트 칸을 늘 붙일까
- options: ServerTypeCatalog.RailOrder 에 넣기 · 공급자가 있을 때만 붙이기
- chosen: 공급자(`ISystemEventSource`)가 있을 때만 서버 모니터가 SystemEventsSpec 를 끝에 붙인다
- why: 기존 시험 3곳이 "공급자 없는 콘솔엔 칸이 없다" 를 단언한다 — 그 규칙은 그대로 두고, GIS(EventApiModule 이 공급자를 등록)에서는 칸이 선다.
- effect: 공급 계약 `ISystemEventSource` 는 Devices.Ui · Events.Api 가 서로 참조하지 않아 Messages/Dto/Systems 에 둔다.

## 9. MIC_PAGING 을 기존 표시 경로에 어떻게 잇나
- options: MapViewModel 이 새 서비스를 구독 · BROADCAST_STATUS 서비스를 감싸 같은 인터페이스로 등록
- chosen: `MicPagingNatsSyncService` 가 BroadcastStatusNatsSyncService 를 감싸 IBroadcastStatusNatsSyncService 로 등록(GMapUiModule)
- why: 지도(MapViewModel) · BroadcastStatusNatsSyncService 를 고치지 않고(이 작업의 파일 범위 밖) 같은 표시 경로를 쓴다.
- effect: 시험 파일도 범위(Services/MicPaging*.cs) 안에 두려고 Services/MicPagingNatsSyncServiceTests.cs 로 뒀다.

## 10. G43 created_at
- options: 늘 비우기 · 판본 분기
- chosen: 서버 계약 V7_0 미만에서만 싣고 그 밖은 null
- why: CHANGELOG [6.3.16] F16 — 6.3.16 부터 조치 created_at 은 무시(서버 접수 시각). 운영 6.3.2 는 그 이전이라 받은 값을 저장한다. EventApiService(다른 작업 소유) 대신 생성 지점 7곳에서 `ActionReportRules.ApplyCreatedAtPolicy` 를 부른다.

## 11. G49 로 바뀐 기존 시험
- options: 기존 단언 유지(구현 포기) · 단언을 새 계약으로
- chosen: Messages/Tests/UnitTest.cs 의 "모르는 원본 → null" 단언 4곳을 "UnknownEventDto(공통 필드)" 로 바꿨다(약화 아님 — 같은 입력에 더 강한 단언)
- why: 지시문 G49 가 그 계약을 바꾼다. '원본 없음'(from_event: null) 시험은 그대로 null 이다.
