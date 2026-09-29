# 이벤트 라이브 파이프라인 프로브 (WP-5) — 결과 요약

> 2026-09-30 · 도구 `tools/live-nats-pipeline` (net8 콘솔, .sln 비포함) · **헤드리스** — 실제 라이브러리 파이프라인을 프로세스 안에서 돌리고 루프백 NATS(`127.0.0.1:4222`, nats-server 2.10.29, max_payload 1 MiB)에 붙였다.
> **격리**: 발행·구독 전부 `sensorway.unit999.*`(헤디드 GIS 의 unit001 과 분리, `global` 발행 0) · REST 는 기본 모드에서 **네트워크 0**(프로세스 안 가짜 서버 — 서버 자신의 v7 계약 픽스처로 응답, 쓰기 전면 차단) · `--real` 모드만 전용 시험 계정 1회 로그인 + GET 전용(쓰기 차단 게이트) · 행 생성·수정 0건 · GIS 앱 미기동.

## 무엇이 진짜로 돌았나
- 라이브러리(실코드): `NatsService`(NatsModule) · `DetectionNatsSyncService` · `MalfunctionNatsSyncService` · `OperationEventNatsSyncService` · `DetectionSyncNatsService` · `EventQueueManager` · `SymbolEventManager`(심볼 = 실제 `PidsSymbolModel`/`PidsGroupSymbolModel`) · `EventCardListPanelViewModel`(활성화) · `DeviceProviderService`/`DeviceApiService`/`EventApiService`.
- 호스트(실코드, **링크 컴파일**): `NatsBrokerService.cs` · `NatsDomainService.cs` · `RecentEnvelopeIds.cs` · `EventCallInfo.cs`. 설정 가방 `SetupModel` 만 심(shim). Redis·사운드는 기록형 가짜.
- EQM↔SEM↔카드 배선은 `EventUiModule` 빌드 콜백과 같은 순서(동기화 서비스 먼저, 호스트 브로커 나중).
- 판정 근거 = 관측 가능한 상태만: EQM 엔트리(키·EntryId·그룹), 심볼 CompositeStatus/DoorState, 카드(종류·EntryId), 캡처 로그(file:line), 가짜 서버가 받은 REST, EA 메시지.

## 실행 4회
| 실행 | 라이브러리 | 호스트 | PASS | FAIL | INFO | 상세 |
|---|---|---|---|---|---|---|
| baseline | `61b99bdf` (스냅숏) | `c403d81` | 57 | 10 | 4 | `event-live-pipeline-probe-log.head.md` |
| latest(수정 전) | `6721afdb` (스냅숏) | `ebfdcc8` | 60 | 7 | 4 | (아래 판정 비교의 'latest(수정 전)' 열) |
| **latest** (D1~D8 수정 후, 2026-09-30 08:45) | `57ada316` (스냅숏) | `0df7e07` (스냅숏) | **67** | **0** | 4 | `event-live-pipeline-probe-log.latest.md` |
| real 장비(latest) | `6721afdb` | `ebfdcc8` | 17 | 0 | 0 | `event-live-pipeline-probe-log.real.md` (실서버 61대·13종, 전 종류 DETECT→조치보고 · 실제어기 346 블랙아웃 그룹 1·2 · 실 GET 재조회) |

(baseline 중 S28~S30 은 같은 baseline 빌드로 따로 돌린 값 — `out/head-s28/`.) 스냅숏은 `git archive HEAD`(+ OnvifSolution 서브모듈)로 떠서 작업 중인 다른 세션의 반쯤 고친 파일이 결과를 물들이지 않게 했다.

## 결함 (latest(수정 전) 기준) — 전부 해결
> 수정: 라이브러리 `7dc1375b`(D1 · D2 · D3 · D5 · D6 · D7 · D8 + D4 카드 조치 회귀 시험) · 도구 `57ada316`(캐시 미스 조회기 배선 · S18.b 기대 교정) · 호스트 `0df7e07`(D4 RemoveByDevice · D6 cmd 대문자). 재실행 latest = PASS 67 / FAIL 0.

| # | 시나리오 | 기대 | 실제(수정 전) | 근거 | 담당 | 상태 |
|---|---|---|---|---|---|---|
| D1 | S28 장애 → 30 ms 뒤 같은 장비 탐지 | 자동복구 조치보고(API) 시도, 간격 무관 | `카드 없음 — API 스킵` · 조치 POST 0회 · 장애 카드가 이미 사라진 EQM 엔트리를 가리킨 채 남음(1500 ms 간격은 정상). **baseline 은 30 ms 도 정상 → 회귀** | `EventCardListPanelViewModel.cs:715` `_cardByEntryId` 조회 — 카드가 150 ms 묶음 버퍼(`EnqueueCard`, `NatsDomainService.cs:267-271`)에 있는 동안엔 없음 | WP-1 | **해결** — 자동복구가 보류 표(종류, 번호)로 묶음 버퍼 속 카드도 찾고 성공 시 RememberClosed 로 올리지 않음 · 재실행 S28.30ms PASS(POST /api/events/actions 시도) |
| D2 | S11.a 캐시에 없는 센서의 DETECT(SYNC_DEVICE CREATED 보다 먼저 도착) | 명세 §6.1: `GET /devices/sensors/{id}` 1회 → 캐시 채우고 처리 | GET 0회 · 종류 `NONE`·그룹 없음으로 큐 적재(그룹 표시 누락). baseline 은 아예 버림 | `DetectionNatsSyncService.cs:119-127`, `NatsEventDeviceResolver.TryResolve`(조회 경로 없음) | WP-1 | **해결** — 캐시 미스면 `FetchDeviceByIdAsync` 단건 GET 1회(5초 상한) → 캐시 채우고 Fence·그룹13 으로 적재 · S11.a PASS |
| D3 | S11.b 어디에도 없는 제어기 2399 DETECT | S11.a 와 같은 규칙(조회→404→스냅숏 카드만) | 조회 없이 `Controller` 로 큐 적재 — 카테고리가 1:1 인 종류만 통과, sensor 와 규칙이 다름 | `NatsEventDeviceResolver`(`DeviceTypeResolver.FromCategory`) | WP-1 | **해결** — 같은 규칙(조회 → 404 → 큐 없음 · 스냅숏 카드) · S11.b PASS(GET /api/devices/controllers/2399) |
| D4 | S18.b 활성 탐지가 있는 센서 SYNC_DEVICE DELETED | 그 장비 EQM 엔트리·카드 정리, 그룹 복원 | 캐시에선 지워졌지만 EQM 엔트리·카드 잔류, 그룹13 계속 Detecting(고아) | 호스트 `NatsDomainService.cs:1095-1102` 가 `EventQueueManager.RemoveByDevice`(EB3, 휴면)를 안 부름 | WP-1 | **해결(기대 교정)** — DELETED 시 `RemoveByDevice`(+같은 id 의 NONE) · 그룹13 Normal · **카드는 유지**(EVT-E2E-022 · 브로커 N-5, 결정 2026-09-30 — 원래 기대 '카드 정리'는 기각) · S18.b PASS |
| D5 | S27 `m_type=RSP` 인 DETECT | 응답 메시지는 탐지 아님 | 호스트는 무시(카드 0), 라이브러리는 큐 적재 → 심볼 Detecting·계기 +1 인데 카드 없음(반쪽 상태) | `DetectionNatsSyncService.cs:94` 부근 — `m_type` 미확인 | WP-1 | **해결** — `NatsEnvelopeItems.IsNotice` 가 `m_type=RSP` 제외(DETECT·MALFUNCTION·OPERATION_EVENT·SYNC_DETECTION 공통) · S27 PASS |
| D6 | S21 cmd 소문자 `detect` | 호스트·라이브러리 같은 판정 | 호스트는 받아 카드 1장(`NatsBrokerService.cs:509` ignoreCase), 라이브러리는 버림(`DetectionNatsSyncService.cs:94` `cmd != "DETECT"`) → 카드만 있고 심볼·EQM 없음 | 두 곳 대소문자 규칙 불일치 | WP-1 | **해결** — 명세 대문자 토큰만(서수 비교): 호스트 `ResolveCommand(ignoreCase:false)` · 라이브러리 `IsNotice` 같은 규칙(결정 2026-09-30) · S21 PASS(둘 다 버림) |
| D7 | S17.g `device: null` OPERATION_EVENT | 조용히 무시 | `ERROR OnNatsOperationAsync 오류: Cannot access child value on JValue` | `OperationEventNatsSyncService.cs:81-82` — `body["device"]` 가 JSON null(JValue)인데 `?.Value<int?>("id")` 로 파고듦 | WP-1 | **해결** — `device`·`detail` 은 JObject 일 때만 읽고 장비 없음은 INFO 로 넘김 · S17.g PASS(ERROR 0) |
| D8(경) | S13 배열 봉투 | 전 구독자가 배열 처리 | DETECT/MALFUNCTION 은 해결됐으나 `DetectionSyncNatsService` 는 여전히 `JObject.Parse` → 배열마다 ERROR 1줄, 배열 속 SYNC_DETECTION 유실 | `DetectionSyncNatsService.cs:82` | WP-1 | **해결** — `DetectionSyncNatsService` 도 `NatsEnvelopeItems.Parse` 항목마다 · S13 PASS(ERROR 0) |

GMaps.Ui(WP-2) 소관 결함은 이번 프로브 범위(심볼 상태 = `SymbolEventManager`, Events.Ui)에서 나오지 않았다 — 지도 렌더링 자체는 헤드리스라 판정 대상이 아니다.

## baseline → latest 사이에 라이브로 확인된 해결
- S06.b 같은 번호 탐지·장애 공존 시 장애 ACTION_REPORT 가 **탐지 카드를 닫고 탐지 엔트리를 지우던 것**(baseline: 2131 Normal·2114 Faulted 로 뒤집힘) → 해결.
- S09.a 같은 봉투 재수신 → 카드 2장·탐지음 2회 → 해결(1장). S09.b 같은 봉투 id 의 다른 장비 재사용 → EQM 인덱스 오염으로 2131 **영구 Detecting** → 해결(두 번째 거부).
- S13 배열 봉투 → 카드만 뜨고 EQM·심볼 0 → 해결(D8 잔여).
- S30 표시 카드 상한 → baseline 600장 전부 표시 → latest 500 상한 적용(EQM 600 보존).

## 참고 관측(INFO)
- S05.c 장애 → 탐지 순서면 설계상 자동복구로 장애가 지워져 FaultedDetecting 이 아니라 Detecting(탐지 → 장애 순서는 FaultedDetecting 정상, S05.a/b PASS).
- S15.a 폭주 중 클라이언트 재연결: 121~136 ms, 200 건 중 1 건 유실, 재연결 뒤 재동기화 동작 없음(명세 RF-8 '재연결 뒤 전량 재적재' 미구현). 재연결 후 중복 구독 없음(S15.b PASS).
- S24 `type_event=Alert`: latest 는 탐지 계기에 1 로 셈·심볼은 Normal·카드 표시·조치보고로 정리.
- 성능: 200 건/5 s 지연 p50 1 ms·p95 1 ms·max 2~9 ms, 600 건/5 s 도 p95 1 ms(유실 0).
- 호스트 `NatsBrokerService` 는 baseline 에서 모든 OPERATION_EVENT 마다 `Unknown type_command` 경고 2줄 → latest 해결.

## baseline ↔ latest(수정 전) ↔ latest 판정 비교
| # | 시나리오 | baseline | latest(수정 전) | 변화 | latest(D1~D8 수정 후) |
|---|---|---|---|---|---|
| S00 | 파이프라인 수신 준비 | PASS | PASS |  | PASS |
| S01.SmartSensor2/sensor | DETECT Intrusion — sensor:SmartSensor2(2131) | PASS | PASS |  | PASS |
| S01.Multi/sensor | DETECT Intrusion — sensor:Multi(2126) | PASS | PASS |  | PASS |
| S01.PIR/sensor | DETECT Intrusion — sensor:PIR(2114) | PASS | PASS |  | PASS |
| S01.Fence/sensor | DETECT Intrusion — sensor:Fence(2200) | PASS | PASS |  | PASS |
| S01.SmartCompound/sensor | DETECT Intrusion — sensor:SmartCompound(2201) | PASS | PASS |  | PASS |
| S01.PTZ/camera | DETECT Intrusion — camera:PTZ(379) | PASS | PASS |  | PASS |
| S01.Controller/controller | DETECT Intrusion — controller:Controller(2113) | PASS | PASS |  | PASS |
| S01.Unknown/enclosure | DETECT Intrusion — enclosure:Unknown(2159) | PASS | PASS |  | PASS |
| S01.Sliding/gate | DETECT Intrusion — gate:Sliding(2157) | PASS | PASS |  | PASS |
| S01.Unknown/lamp | DETECT Intrusion — lamp:Unknown(2156) | PASS | PASS |  | PASS |
| S01.Unknown/speaker | DETECT Intrusion — speaker:Unknown(2154) | PASS | PASS |  | PASS |
| S02.a | AI 탐지(all.event_ai.detect, 카메라 참조) | PASS | PASS |  | PASS |
| S02.b | AI 탐지 조치보고 메아리 → 복원 | PASS | PASS |  | PASS |
| S03.FAULT_FENCE/Fence | MALFUNCTION FAULT_FENCE — sensor:Fence(2200) | PASS | PASS |  | PASS |
| S03.FAULT_MULTI/Multi | MALFUNCTION FAULT_MULTI — sensor:Multi(2126) | PASS | PASS |  | PASS |
| S03.FAULT_CABLE_CUTTING/SmartSensor2 | MALFUNCTION FAULT_CABLE_CUTTING — sensor:SmartSensor2(2131) | PASS | PASS |  | PASS |
| S03.FAULT_ETC/PIR | MALFUNCTION FAULT_ETC — sensor:PIR(2114) | PASS | PASS |  | PASS |
| S03.FAULT_ETC/PTZ | MALFUNCTION FAULT_ETC — camera:PTZ(379) | PASS | PASS |  | PASS |
| S03.FAULT_SENSOR/SmartCompound | MALFUNCTION FAULT_SENSOR — sensor:SmartCompound(2201) | PASS | PASS |  | PASS |
| S04.a | 제어기 무통신(FAULT_CONTROLLER, 제어기 2113) → 그룹 검정 | PASS | PASS |  | PASS |
| S04.b | 제어기 ERROR 상태 SYNC_DEVICE(UPDATED) — 아직 고장 | PASS | PASS |  | PASS |
| S04.c | 제어기 통신 복구 SYNC_DEVICE(UPDATED, ACTIVATED) → 자동복구 | PASS | PASS |  | PASS |
| S04.d | 블랙아웃 중 그 제어기 소속 센서(2131) 탐지 → 제어기-소유 자동복구 | PASS | PASS |  | PASS |
| S05.a | 같은 장비: 탐지 → 장애 순서 | PASS | PASS |  | PASS |
| S05.b | FaultedDetecting 에서 장애만 조치보고 | PASS | PASS |  | PASS |
| S05.c | 같은 장비: 장애 → 탐지 순서(참고) | INFO | INFO |  | INFO |
| S06.a | 같은 이벤트 번호(7791628)를 탐지(2131)·장애(2114)가 공유 — 카드↔큐 엔트리 짝 | PASS | PASS |  | PASS |
| S06.b | ACTION_REPORT(from_event.category_event=malfunction, id 7791628) — 같은 번호의 탐지가 살아 있을 때 | FAIL | PASS | **해결** | PASS |
| S07.a | 탐지 조치보고 메아리(ACTION_REPORT, from_event=detection) | PASS | PASS |  | PASS |
| S07.b | 장애 조치보고 메아리(ACTION_REPORT, from_event=malfunction) | PASS | PASS |  | PASS |
| S08 | 모르는 이벤트/ id 0 의 ACTION_REPORT | PASS | PASS |  | PASS |
| S09.a | 같은 봉투(id 동일)를 두 번 수신(브로커 재전송·이중 구독 모사) | FAIL | PASS | **해결** | PASS |
| S09.b | 같은 봉투 id 가 다른 장비(2131→2114) 탐지에 재사용된 뒤 둘 다 조치보고 | FAIL | PASS | **해결** | PASS |
| S10 | device:null 탐지·장애(삭제된 장비, 브로커 §6.1/6.2) | PASS | PASS |  | PASS |
| S11.a | 캐시에 없는 센서 2301 의 DETECT(서버엔 있음 — SYNC_DEVICE CREATED 보다 먼저 도착) | FAIL | FAIL |  | PASS (**해결**) |
| S11.b | 어디에도 없는 제어기 2399 의 DETECT(서버 404 = 삭제된 장비) | FAIL | FAIL |  | PASS (**해결**) |
| S12 | v7 이전 전문(device 에 type_device + device_groups 중첩, category_device 없음) | PASS | PASS |  | PASS |
| S13 | 배열 봉투 [DETECT, DETECT] 한 메시지(호스트 ParseMessageItems 가 지원하는 형태) | FAIL | PASS | **해결** | PASS |
| S14 | 폭주 200건 / 5초(센서 6종 순환, 고유 이벤트 번호) | PASS | PASS |  | PASS |
| S15.a | 폭주 200건 중간(100건째)에 파이프라인 NATS 클라이언트를 버리고 새로 연결(브로커는 건드리지 않음) | INFO | INFO |  | INFO |
| S15.b | 재연결 뒤 단건 DETECT — 중복 구독/유실 없음 | PASS | PASS |  | PASS |
| S16 | CONNECTION 이벤트(v2.0 body, 소비자 없음 — 브로커 §6.3) | PASS | PASS |  | PASS |
| S17.a | OPERATION_EVENT 함체 문 열림(v2.0 detail.state=OPEN) | PASS | PASS |  | PASS |
| S17.b | OPERATION_EVENT 함체 문 닫힘 | PASS | PASS |  | PASS |
| S17.c | OPERATION_EVENT 통문 열림(구동부 actuator) | PASS | PASS |  | PASS |
| S17.d | 통문 detail.state=RUNNING, reason 없음(서버는 RUNNING 진입에 이벤트를 안 만들지만 들어오면) | PASS | PASS |  | PASS |
| S17.e | OPERATION_EVENT 통문 닫힘 | PASS | PASS |  | PASS |
| S17.f | 함체 환경 임계(온도) — 개폐 무관 → 형태 불변 | PASS | PASS |  | PASS |
| S17.g | device:null 운영 이벤트(지워진 함체) | FAIL | FAIL |  | PASS (**해결**) |
| S17.h | 함체 접점 DETECT ContactOn → ContactOff (FR-13③ 접점 폴백) | PASS | PASS |  | PASS |
| S18.a | SYNC_DEVICE CREATED {action, resource_id, category_device} → 새 센서 2300 → 그 장비 DETECT | PASS | PASS |  | PASS |
| S18.b | 활성 탐지가 있는 센서 2300 의 SYNC_DEVICE DELETED | FAIL | FAIL |  | PASS (**해결**) |
| S18.c | SYNC_DEVICE UPDATED — 2131 소속 11 → 11,13 | PASS | PASS |  | PASS |
| S18.d | SYNC_DEVICE UPDATED 상태 ERROR → ACTIVATED(센서 2114) | PASS | PASS |  | PASS |
| S18.e | SYNC_DEVICE 옛 body {action, resource_id, type_device:'Fence'} | PASS | PASS |  | PASS |
| S18.f | SYNC_DEVICE 모르는 category_device 'robot' | PASS | PASS |  | PASS |
| S19 | SYNC_EVENT_MAPPING (같은 봉투 2회 + 다른 봉투 1회) | PASS | PASS |  | PASS |
| S20 | 깨진 JSON 4종 + body 가 숫자인 봉투 뒤에 정상 DETECT | PASS | PASS |  | PASS |
| S21 | cmd 소문자 'detect' | FAIL | FAIL |  | PASS (**해결**) |
| S22.a | 대형 본문 DETECT(~878 KB, max_payload 1024 KB 이내) | PASS | PASS |  | PASS |
| S22.b | max_payload 초과 본문 뒤 정상 DETECT | PASS | PASS |  | PASS |
| S23 | 로그인 게이트: 미인증 상태 DETECT·MALFUNCTION → 인증 후 DETECT | PASS | PASS |  | PASS |
| S24 | type_event=Alert(사전 경보) 탐지 → 조치보고 | INFO | INFO |  | INFO |
| S25 | SYNC_DETECTION UPDATED(id 7792072) — 같은 번호의 장애 카드가 먼저 있음 | PASS | PASS |  | PASS |
| S26 | 같은 장비(2201) 탐지 2건 → 1건씩 조치보고 | PASS | PASS |  | PASS |
| S27 | m_type=RSP 인 DETECT 봉투(응답 메시지 — 새 탐지가 아님) | FAIL | FAIL |  | PASS (**해결**) |
| S28.30ms | 장애 → 30 ms 뒤 같은 장비(2114) 탐지 — 자동복구가 카드를 찾는가 | PASS | FAIL | **회귀** | PASS (**해결**) |
| S28.1500ms | 장애 → 1500 ms 뒤 같은 장비(2114) 탐지 — 자동복구가 카드를 찾는가 | PASS | PASS |  | PASS |
| S29 | DETECT 직후(대기 없이) 그 이벤트의 ACTION_REPORT — 카드가 아직 묶음 버퍼에 있을 수 있는 순간 | PASS | PASS |  | PASS |
| S30 | 폭주 600건 / 5초(120 msg/s) — 표시 카드 상한(500) 넘김 | INFO | INFO |  | INFO |

## 재현
```
# 스냅숏(권장) — 다른 세션의 편집 중 파일 영향 차단
dotnet build tools\live-nats-pipeline\LiveNatsPipeline.csproj -p:LibRoot=<libsnap>\ -p:HostRoot=<hostsnap>dotnet run --no-build --project tools\live-nats-pipeline\LiveNatsPipeline.csproj -- --label latest [--only S06,S28]
# 2026-09-30 재실행: 스냅숏 = git archive HEAD(라이브러리 57ada316 + OnvifSolution 서브모듈 35e0aeb 작업 폴더 복사 · 호스트 0df7e07) → PASS 67 / FAIL 0 / INFO 4
# 실장비 목록(전용 계정, GET 전용, 로그인 1회): 환경변수 LNP_CRED_FILE=<id=/pw= 파일>  → -- --label real --real
```
