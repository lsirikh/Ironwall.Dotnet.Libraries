# PLAN — 이벤트 경로 시간 계측(PerfTrace) + 헤드리스 처리량 시험 (t-20261011-074941-f094)

## 목표
카탈로그 §7 PERF-* 를 위한 라이브러리 쪽 기반:
1. 계측(PerfTrace) — `IRONWALL_PERF_TRACE=1` 일 때만, 이벤트(종류+id) 단계 시각을 jsonl 로.
2. 분석기(순수 코드) — GIS jsonl(+ 주입기 발행 jsonl) → 단계 간 지연 분포 · 누락 · 중복 · UI 지연 · 메모리 증가 → md/json 표.
3. 헤드리스 시험 — 실제 EQM · SEM · 카드 패널(EventMapRig)에 초당 1 · 20 · 200건 · 순간 100건 → PERF-014 · PERF-015, PerfTrace 단계 순서, 분석기 백분위.
4. 시험마다 `[Trait("Scenario", "PERF-0xx")]`.

## 설계 결정
- **위치**: `Ironwall.Dotnet.Libraries.Utils/Perf/`(네임스페이스 `Ironwall.Dotnet.Libraries.Utils.Perf`). 기본 경로에 `LocalAppDataFolder`(Utils) 를 써야 하고,
  UI 지연 프로브에 Dispatcher(WPF) 가 필요해 WPF 인 Utils 에 둔다. GIS 쪽 카메라 팝업(`CameraPopup`)이 popup_open_sent · popup_opened · first_frame 을
  찍어야 해서 `CameraPopup.csproj` 에 Utils 프로젝트 참조를 **더했다**(순환 없음: Utils → Enums 뿐). 카메라 호스트(`CameraPopup.Host`)는 Utils 를 참조하지 않는다.
- **꺼져 있을 때 비용**: `PerfTrace.IsEnabled` = volatile bool 한 번 읽기. 호출부는 extra 사전을 만들기 전에 `if (PerfTrace.IsEnabled)` 로 거른다.
- **한 줄 형식**: `{"t":<UTC, Stopwatch 기준 고해상도>,"wall":<DateTime.UtcNow>,"mono":<ms>,"pid":..,"tid":..,"stage":..,"kind":..,"id":..,"extra":{..}}`.
  분석기는 `t` 로 잰다(같은 프로세스 안에서 단조 · 고해상도, 프로세스 사이엔 벽시계와 같은 기준).
- **kind**: detection · malfunction · operation · connection · gis(프로세스/UI 표본) · camera_host(호스트 하트비트). NATS 명령은 `extra.cmd`
  (DETECT · MALFUNCTION · OPERATION_EVENT · SYNC_DETECTION · ACTION_REPORT). 조치보고는 원본 이벤트의 kind/id 로 `nats_received` + `cmd=ACTION_REPORT`.
- **symbol_state 의 이벤트 id**: SEM 전이 콜백은 장비 · 그룹만 안다 → EQM 이 Enqueue/Dequeue 의 전이 발화 동안 스레드 정적 문맥
  (`PerfTrace.BeginContext(kind,id)`)을 세우고 SEM 이 그 문맥으로 찍는다(콜백은 같은 스레드에서 동기 발화). 문맥 없으면 kind=device.
- **조치보고 수신 시점**: ACTION_REPORT NATS 는 호스트 라우터(Dotnet.Monitoring.Solution)가 받는다 — 라이브러리의 가장 이른 지점
  `EventCardListPanelViewModel.CloseByRemoteActionReport` 에서 찍는다. CONNECTION · SYNC_DEVICE 도 호스트 라우터 소관 → 호스트가
  공개 API `PerfTrace.Mark(...)` 로 찍으면 된다(아래 남은 일).
- **first_frame**: `OpenEventWindow` 는 이미 Kind + EventId(=EventKey) 를 싣고 있고, 호스트의 `StreamStateChanged` 도 EventKey 를 싣는다.
  여기에 선택 필드 `StreamStateChanged.FirstFrameMs`(호스트가 창 열기 요청 받은 뒤 첫 프레임까지 ms)를 **더했다**. 판(ProtocolVersion) 은 그대로 —
  옛 GIS 는 모르는 필드를 무시, 옛 호스트는 안 보냄 → GIS 는 첫 Playing 받은 시각만 찍는다(호환).
- **호스트 하트비트**: 감시자(`CameraPopupHostSupervisor`)가 HeartbeatAck 을 받을 때 1초에 한 번 `host_heartbeat`(ui_stall_ms · private_bytes …).
- **표본기 시작**: `PerfTraceRuntime.StartGisSamplers(dispatcher)` — EventUiModule 빌드 콜백에서 환경변수가 켜졌을 때만. UI 지연 프로브는
  UiPumpProbe 와 같은 방식(배경 타이머가 Send 우선순위 표식을 넣고, 넣은 시각 대비 실행 지연을 잰다, 100 ms 마다), 1초마다 최대 · p95.
- **기록**: 별도 배경 스레드 + 제한 큐(가득 차면 버리고 `trace_dropped` 로 개수 기록). 파일 크기 상한(기본 64 MB) 넘으면 `.1` `.2` … 로 회전(기본 5개).
- **백분위**: nearest-rank(정렬 후 ceil(p/100·n) 번째). 1..100 → p50=50 · p95=95 · p99=99.
- **헤드리스 시험 위치**: 장치(EventMapRig)가 `Events.Ui/Tests` 에 있어 PERF-014/015 · 단계 순서 시험은 그곳(Events.Ui 는 sln 밖 → 검사 명령이
  돌리지 않음, 직접 `dotnet test <Events.Ui csproj> --filter` 로 돌린다). PerfTrace · 분석기 단위 시험은 `Utils.Tests`(sln 안), 팝업 단계는 `CameraPopup.Tests`(sln 안).

- **헤드리스 처리량 시험의 스레드 모델**: 장치(EventMapRig)는 카드 패널의 UI 넘김을 즉시 실행으로 바꿔 둔다. 실제 앱에서 카드 패널 일은 전부
  UI 스레드 한 줄이라, 시험도 보내기(NATS → 큐 → 심볼 → EnqueueCard) · 묶음 비우기를 **한 흐름에서 차례로** 한다(동시 접근으로 생기는 가짜 실패 방지).
  묶음 비우기 간격은 패널 타이머와 같은 적응형(`EventCardBatchBuffer.CalculateInterval`: 150/300/500 ms). 실제 스레드 넘김 지연은 호스트 실측 jsonl 이 잰다.
- **처리 능력 판정**: "일한 시간(busy)" 기준 건/초 ≥ 200 (Task.Delay 대기는 빼고). JIT 첫 호출 비용은 시험마다 예열 1건으로 뺐다.
- **분석기 구간에 `보냄→첫영상` 추가**: 호스트는 창을 띄우며 스트림을 시작하므로 첫 영상이 WindowOpened 보다 먼저 올 수 있다(실제 호스트 왕복에서 확인 가능성).
  호스트 쪽 정확한 값은 `first_frame.extra.host_ms`.
- **카드 누락 판정**: 카드 쪽이 일부러 거른 것(`dedup_skipped` where=`card_*` — 이미 조치됨 · 같은 카드)은 누락이 아니다. 묶음 버퍼 포화로 버린 카드는
  `card_dropped` 단계로 따로 찍어 누락으로 센다.
- **시험 창 금지 준수**: 사용자 PC 사용 중 — Events.Ui 전체 시험(창을 띄우는 시험 포함 가능)은 한 번 시작했다가 즉시 멈췄고, 계측 코드에 닿는 헤드리스
  클래스만 `--filter` 로 돌렸다. 카메라 호스트 왕복 시험은 헤드리스 호스트(창 없음 · 시험 무늬)다.

## 진행
- [x] Utils/Perf: `PerfTrace`(+`PerfStages` · `PerfKinds` · 문맥 · 받는 곳/붙잡기) · `PerfTraceRecord`(jsonl 왕복) · `PerfTraceFileWriter`(배경 스레드 · 제한 큐 · 회전)
      · `PerfUiLagProbe` · `PerfProcessSampler` · `PerfTraceRuntime` · `PerfTraceAnalyzer`(+`PerfPublishRecord` · `PerfAnalysisReport` md/json)
- [x] 계측 지점: DetectionNatsSyncService · MalfunctionNatsSyncService · OperationEventNatsSyncService · DetectionSyncNatsService(nats_received · dedup_skipped)
      · EventQueueManager(enqueued + Enqueue/Dequeue 전이 동안 이벤트 문맥) · SymbolEventManager(symbol_state — 전이 · 재칠)
      · EventCardListPanelViewModel(card_added · card_dropped · dedup_skipped card_* · CloseByRemoteActionReport = nats_received cmd=ACTION_REPORT)
      · EventWindowTrigger(popup_requested) · CameraPopup EventWindowManager(popup_open_sent) · HostPerfRecorder ← 감시자(popup_opened · first_frame · host_heartbeat)
- [x] 계약: `StreamStateChanged.FirstFrameMs`(선택) + 호스트 EventWindowManager 가 타일의 첫 Playing 에 채움(열기 요청 받은 뒤 ms, 호스트 로그에도 한 줄)
- [x] EventUiModule 빌드 콜백에서 환경변수가 켜졌을 때만 표본기 시작
- [x] 시험: Utils.Tests 44건 · CameraPopup.Tests 신규 6건(실제 호스트 왕복 1건 포함) · Events.Ui 신규 11건 — 전부 통과
- [x] 기존 시험 회귀: Events.Ui 의 계측 코드에 닿는 헤드리스 15개 클래스 286건 통과, CameraPopup EventWindowManagerTests · EventWindowHostTests 통과
- [x] 솔루션 검사(`dotnet test Ironwall.Dotnet.Libraries.sln`) — 종료 코드 0, 실패 0 (Utils.Tests 862 · CameraPopup.Tests 272 · CameraPopup.Host.Tests 161 · 그 밖 전부 통과)

## 발견
- **결함 없음(빨강→초록 기록 없음)**: PERF-014 · 015 시나리오는 첫 실행부터 초록이었다 — 중복 봉투 · 같은 이벤트 새 봉투 · 카드 재전송 · 조치보고가
  DETECT 를 앞지름 · 늦은 SYNC_DEVICE ACTIVATED · 늦은/이른 SYNC_DETECTION · 구역 안 조치/탐지 교차 모두 큐 · 카드 · 심볼 최종 상태가 맞았다.
- 실측(이 PC, 헤드리스, 참고값): 200건/초 × 400건 — 일한 시간 기준 약 470건/초, 수신→큐 p95 0.2 ms, 수신→심볼 p95 0.2 ms,
  수신→카드 p95 약 190 ms(묶음 간격이 지배). 순간 100건 — 27 ms 에 전부 처리(약 3,700건/초), 수신→카드 p95 약 25 ms.
  카드 수가 많을수록(같은 카드 찾기 · 목록 삽입이 O(n)) 건당 비용이 늘어나는 경향 — 표시 상한 500 안에서는 기준(200건/초)을 넉넉히 넘는다.
- **기준 밖 주의(PERF-010 변형용)**: 카드 묶음 버퍼 상한이 200(`EventCardBatchBuffer.DEFAULT_MAX_PENDING`)이고 간격이 최대 500 ms 로 늘어나므로,
  지속 약 400건/초 이상이거나 한 간격 안 200건 넘는 순간 폭주는 카드가 버려진다(이제 `card_dropped` 로 계측에 드러남). 카탈로그 기준(200건/초 · 순간 100건)은 안전.
- `OpenEventWindow` 는 원래 Kind + EventId(=EventKey)를 싣고 있어 "열기 요청에 이벤트 id" 는 기존 필드로 충족 — 계약에는 첫 영상 시간만 더했다.

## 남은 일(호스트 쪽 — 이 저장소 밖, Dotnet.Monitoring.Solution)
- 호스트 라우터가 받는 CONNECTION · SYNC_DEVICE(그리고 ACTION_REPORT 를 라우터가 받은 순간)를 공개 API
  `PerfTrace.Mark(PerfStages.NatsReceived, PerfKinds.Connection|…, id, PerfTrace.Extra(("cmd","CONNECTION")))` 로 찍으면 라우터 안 지연까지 보인다.
- 호스트 실측 시험(PERF-001 ~ 013): `IRONWALL_PERF_TRACE=1` + `IRONWALL_PERF_TRACE_FILE=<경로>` 로 GIS 실행 → 주입기가 발행 jsonl
  `{kind,id,api_created_at,nats_published_at}` 기록 → `PerfTraceAnalyzer.Analyze(ReadRecords(WithRotations(path)), ReadPublished(...))` → `ToMarkdown()/ToJson()`.
- 카메라 호스트가 새 판으로 함께 배포돼야 `first_frame.extra.host_ms` 가 채워진다(옛 호스트면 GIS 수신 시각만).

## 결과표
| 항목 | 시험(표지) | 결과 |
|---|---|---|
| PERF-001 탐지 → 카드 | Events.Ui `EventPipelinePerfTests` 단계 순서 · 처리량 시험(수신→카드 p95 ≤ 1 s) · Utils.Tests 계측/분석기 | 계측 · 분석 수단 완료(헤드리스 초록) — 실측은 호스트 |
| PERF-002 탐지 → 지도 | 단계 순서(심볼 · 구역선 symbol_state 가 이벤트 문맥으로) · 처리량(수신→심볼 건수 = 보낸 수) | 수단 완료(초록) — 실측은 호스트 |
| PERF-003 팝업 열림 | `PopupPerfTraceTests` · `EventWindowPerfTraceHostTests`(실제 헤드리스 호스트) · 단계 순서(popup_requested → popup_open_sent) | 수단 완료(초록) |
| PERF-004 첫 영상 | `StreamStateChanged.FirstFrameMs` 왕복 · 옛 호스트 호환 · 실제 호스트에서 host_ms 수신 | 수단 완료(초록) |
| PERF-005 장애 | 장애 단계 순서(수신 → 큐 → Faulted → 카드 → 조치 → Normal) · 늦은 SYNC_DEVICE | 수단 완료(초록) |
| PERF-006 조치보고 되돌림 | 조치보고→심볼정상 구간(처리량 시험 끝에 이벤트마다) | 수단 완료(초록) |
| PERF-007 ~ 010 부하 | 분석기 발행 jsonl 대조 · 누락 · 순서 · 따라잡기 시험, 헤드리스 1/20/200건/초 · 순간 100건 | 분석 수단 완료 — 실측은 호스트 |
| PERF-011 화면 반응 | `PerfSamplerTests`(UI 지연 프로브, 창 없는 디스패처) · 분석기 UI 지연 요약 | 수단 완료(초록) — 실측은 호스트 |
| PERF-012 동시 팝업 | 호스트 하트비트 계측(1초) · 실제 호스트 왕복 | 수단 완료 — 4 · 8 동시 실측은 호스트 |
| PERF-013 장시간 | 프로세스 표본기 · 파일 회전 · 분석기 메모리 증가 | 수단 완료(초록) — 60분 실측은 호스트 |
| PERF-014 처리량(헤드리스) | `EventPipelinePerfTests` 1 · 20 · 200건/초 · 순간 100건 | **검증됨** — 누락 0 · 순서 보존 · ≥ 200건/초 |
| PERF-015 중복 · 순서 | 같은 봉투 · 같은 이벤트 새 봉투 · 카드 재전송 · 조치보고 선행 · 늦은 SYNC_DEVICE · SYNC_DETECTION 선행 · 구역 교차 | **검증됨** — 카드 · 큐 중복 없음 · 최종 상태 맞음 |
| PERF-016 끊김 중 부하 | — | 범위 밖(이번 목표에 없음) |
