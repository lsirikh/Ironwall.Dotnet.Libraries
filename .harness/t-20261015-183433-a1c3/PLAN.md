# PLAN — 이슈 #7 GIS 몫 나머지(G7-3 · G7-4 · #20 · Q-4a)

## 목표
근거 `docs/coordinations/GOP_Server_API_issue7_GIS_REQUEST_20261014.md`(합의 표) · `2026-10-15-이슈-7-gis-회신-…md`(GIS 확정 회신).
1. G7-3 억제 해제 타이머 + 캐시 TTL  2. G7-4 제어 명령 `requested_by` · `requested_client_id`
3. #20 `ENCLOSURE_METRICS` 표시  4. Q-4a 함체 환경 · 부품 판정 심볼 경고  (SYNC 5종 G7-1 은 범위 밖)

## 진행
- [x] 조사: G7-3 · #20 은 앞선 하네스 작업(t-20261015-111727-d0be · t-20261015-120551-ccaf, 둘 다 accept 됨)이 이미 넣었다
      — `SuppressionActiveMonitor` 해제 타이머 · TTL(폴링×3), `EnclosureMetricsNatsSyncService` → 툴팁 · 상세 창.
- [x] G7-4: `Services/Brokers/CommandRequester.cs`(새 · `ICommandRequester` + `ResolveRequestedBy` + `Stamp`) → `BrokerRequestClient`(경광등 · 방송 · 조준 · 팝업) ·
      `DoorControlService`(DTO `DoorSetBodyDto.RequestedClientId` 추가) · TTS. 생성자는 기존 것을 두고 오버로드만 더했다. DI: `GMapUiModule`.
      `MapViewModel.CurrentRequestedBy` 를 같은 순서(login_id 먼저)로 바꿈.
- [x] Q-4a: `Services/Enclosure/EnclosureAlertPayload.cs`(새 · 해석 · 요약) · `EnclosureMetricsNatsSyncService`(`AlertsChanged` · `GetAlert` 추가)
      → `GMapPidsMarker.EnclosureAlert`(런타임) → `GMapMarkerPidsControl.EnclosureAlert` DP → `ComponentStatusOverlay.Alert`(위 가운데 삼각형) + 툴팁 줄.
      템플릿 3곳(`PidsMarkerStyle.xaml` · `Housing3DMarkerStyle.xaml` ×2)에 `Alert="{TemplateBinding EnclosureAlert}"` 만 더함(x:Name 불변).
- [x] 시험 작성: `GMaps.Ui/Tests/CommandRequesterTests.cs` · `GMaps.Ui/Tests/EnclosureAlertPayloadTests.cs` · `tests/GMaps.Housing.Tests/EnclosureAlertScenarioTests.cs`
- [x] 시험 실행(모두 헤드리스 · 창 없음)
  - `dotnet test Ironwall.Dotnet.Libraries.GMaps.Ui/…csproj` → 706/706 통과(새 시험 포함). 처음엔 팝업 폴백 시험 1건 실패 → 폴백 복원 후 통과.
  - `tests/GMaps.Housing.Tests` (EnclosureAlert · EnclosureMetrics · ComponentMapWiring) → 17/17
  - `tests/GMaps.MapEdit.Tests` (DoorAndMultiSelectScenarioTests — `requested_by` "시험관리자" 기존 단언) → 7/7
  - `dotnet test Ironwall.Dotnet.Libraries.sln` → 10개 시험 어셈블리 전부 통과(실패 0)
  - Events.Ui 는 이번에 코드 변경 없음(공개 도우미 `OperationEventText` 를 읽기만) → 따로 돌리지 않았다.
- 빨강 → 초록 근거: 새 시험이 부르는 형식 · 멤버(`CommandRequester` · `ICommandRequester` · `DoorSetBodyDto.RequestedClientId` · 실행기 · 문 · 방송의 새 생성자,
  `EnclosureAlertPayload` · `EnclosureAlertSummary` · `GetAlert` · `GMapPidsMarker.EnclosureAlert` · `ComponentStatusOverlay.IsAlertDrawn`)는 변경 전 코드에 없어
  변경 전에는 컴파일부터 실패한다. 변경 후 통과.
- 새 .cs 5개 맨 앞에 Edit 로 U+FEFF(BOM)를 넣었다 — 바이트 확인 명령(head/od)은 셸 권한 거부로 직접 확인하지 못했다.

## 결정
- G7-3: 다시 만들지 않는다. SYNC 수신 → `/active` 즉시 재조회 → 응답의 `occurrence_end`(단발 `window_end`)로 해제 타이머(앞선 작업의 결정 유지 —
  `GET /{id}` 를 따로 부르지 않아도 같은 값이 `/active` 에 실린다). 칸이 없는 옛 서버는 타이머 없음 = 종전 폴링만.
- #20 호스트 배선: **필요 없다.** 호스트 `Dotnet.Monitoring.Solution/Services/NatsBrokerService.cs:343` 이 `ENCLOSURE_METRICS` 를
  "처음 한 번 정보 기록 후 넘김" 목록에 두고 있고, 라이브러리 `EnclosureMetricsNatsSyncService`(GMapUiModule 등록 · MapViewModel 활성 때 시작)가
  `gis.>` 구독 이벤트에서 직접 받는다. `OPERATION_EVENT`(339행) · `ACTION_REPORT` 도 같은 이벤트를 라이브러리가 함께 받으므로 Q-4a 도 호스트 변경 없음.
- G7-4 `requested_by` = `IPermissionService.LoginId` → `ITokenStorageService.UserId`(JWT sub = login_id) → 표시이름(옛 시험 · 옛 경로 호환) → 없음(칸 생략).
  문 개폐 · 경광등 · 방송 · TTS 는 값이 없으면 칸 생략(`MapViewModel.CurrentOperatorAccount`). 회전요청 · 카메라 팝업은 종전 `Environment.UserName`
  폴백을 유지했다 — 기존 시험 `CameraPopupDoubleClickDispatcherTests.should_route_map_double_click_…`(305행)이 그 폴백을 단언하고, 단언 약화 금지.
- G7-4 `requested_client_id` = `ClientIdResolver.Resolve(IApiSetupModel.ClientId)` — `ApiService` 가 `X-Client-Id` 헤더에 싣는 값과 같은 함수 · 같은 캐시.
- G7-4 싣는 곳: REQ 공통 실행기 `BrokerRequestClient` 한 곳(경광등 · 방송 PLAY/STOP/MIC · PTZ_AIM_LOCATION · 카메라 팝업 4종 — 브로커 §11.2 표 전부 "제어 명령"),
  문 개폐(형식 DTO 에 칸 추가), TTS(PUB). 이미 값이 있는 칸(팝업 · 조준의 `requested_by`)은 덮지 않는다. 칸은 선택이라 서버 판본 분기 없음.
- Q-4a 경고 대상: 함체 장비의 운영 이벤트 중 **개폐 사유가 아닌 것**(환경 9종 + 앞으로 올 조합 판정 사유) · severity WARNING/CRITICAL. INFO 는 띄우지 않는다.
  해제: 그 운영 이벤트의 조치보고(`ACTION_REPORT` · `from_event.category_event = "operation"`) — 카드 [X] 의 '운영상태 확인' 자동 조치도 같은 길로 돌아온다.
- Q-4a 그림: 부품 층(ComponentStatusOverlay) **위 가운데** 12px 삼각형 + "!" — WARNING = 바탕 채움 + `StatusWarningBrush` 테두리 · "!",
  CRITICAL = `StatusCriticalBrush` 채움. 좌상단은 3D 템플릿의 문 열림 표지(`PART_DoorOpenMark`)가 쓰므로 피했다.
  우상단 이벤트 점 · 좌하단 문 · 우하단 공구와 겹치지 않는다. 깜빡임 없음. 크기 LOD 없이 늘 그린다(경고가 작은 아이콘에서 사라지면 안 된다).
- Q-4a 데이터: 운영 카드는 이벤트 큐(EQM)에 들어가지 않는다(operation-event-card PRD §4) — 심볼 색 축을 쓰지 않고 함체 계측과 같은
  라이브러리 수신 서비스가 따로 든다(인터페이스 `ISymbolEventManager` 를 늘리면 목 · 페이크가 깨진다).

## 남은 일
- 없음(범위 안). 실서버 · 실기 확인은 하지 않았다. 범위 밖: SYNC 5종(G7-1, 서버 발행 전).
