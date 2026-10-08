# PLAN — t-20261008-150643-332f

## 목표
카탈로그 §2(EVM-001 ~ EVM-140) 중 기존 시험이 없거나(GAP) 헤디드로만 있던 항목을, 창을 띄우지 않는 헤드리스 시험(뷰모델 · 라우터 · 서비스 · 부모 없는 비주얼 렌더)으로 만든다. 결함은 증거로 원인 확인 → 작고 분명하면 수정 + 회귀 시험.

## 결정
- 시험 ① `Ironwall.Dotnet.Libraries.Events.Ui/Tests/EventMapScenarioTests.cs`(58건) — 실제 큐 + 심볼 관리자 + NATS 수신 서비스(탐지 · 장애 · 운영 · PTZ) + 카드 패널을 호스트 배선(EventUiModule)과 같게 잇는 장치(EventMapRig). 가짜 서버에 먼저 기록 → 같은 번호로 NATS.
- 시험 ② `tests/GMaps.Housing.Tests/EventMarkerPaintScenarioTests.cs`(16건) — 같은 흐름 끝에 실제 마커 + 실제 템플릿(PidsMarkerStyle · PidsGroupMarkerStyle)을 부모 없이 Measure/Arrange + RenderTargetBitmap. 구역선은 지도 컨트롤이 있어야 점을 계산하므로 템플릿 세 선에 같은 점을 넣고 색 · 보임 · 깜빡임 트리거를 본다.
- 깜빡임 · 펄스 판정은 템플릿 트리거의 이름 붙은 스토리보드 상태(GetCurrentState = Active/Stopped). HasAnimatedProperties 는 StopStoryboard 뒤에도 true 로 남아 쓸 수 없다(실측). 창이 없으면 렌더 시계가 돌지 않아 프레임 간 변화는 헤디드 몫.
- Events.Ui · GMaps.Ui · tests/GMaps.* 는 `Ironwall.Dotnet.Libraries.sln` 에 없다 → 하네스 검사(`dotnet test sln`)는 이 시험을 돌리지 않는다. sln 에 넣지 않았다(범위 밖 · 창 띄우는 기존 시험이 섞여 사용자 화면을 건드림). 직접 `dotnet test <csproj> --filter` 로 돌렸다.
- 회귀 실행 때 창을 띄우는 클래스 제외: Events.Ui(SuppressionRowSelectBindingTests · SuppressionRecurrenceRadioTests · SuppressionDayChipAutomationTests · MappingDragInteractionTests · EventToolbarChipAutomationTests · EventConsoleSingleSelectionDetailTests), Housing(MapLocateFitTests).
- 호스트(Dotnet.Monitoring.Solution) 코드에만 있는 처리(CONNECTION body.device.status · SYNC_DEVICE 라우팅 · AI_DETECTION 정규화 · 셸 배지 · 카드 생성)는 이 저장소 밖 → 라이브러리 쪽 절반만 시험.
- CHANGELOG 등 기존 문서는 손대지 않음(AGENTS.md: 문서는 요청할 때만).

## 진행
- [x] 카탈로그 §2 읽음, 기존 시험 · 가짜 기반 조사
- [x] 결함 1 수정 — EventQueueManager.ComputeDeviceState/ComputeGroupState 가 Intrusion 만 탐지로 침 → 사전경보(Alert) · 접점 센서 ContactOn/Off 가 카드 · 소리 · 신호등 수 · 이벤트 창엔 잡히는데 지도 심볼 · 구역선은 초록 그대로. "장애가 아니면 탐지"(GetActiveCounts · EventWindowPlanning 과 같은 규칙)로 바꿈. 수정 전 코드로 되돌려 새 시험 3건 실패 확인 → 복원.
- [x] ① 58건 통과. Events.Ui 회귀(창 시험 제외) 1918 중 1917 통과 — 실패 1건(EventConsoleNavigationTests.should_show_the_custom_range_fields_when_a_trend_band_is_committed)은 단독 10/10 통과(순서 의존 흔들림, 큐와 무관). ①에 EVM-067 2건 추가 뒤 58건.
- [x] ② 16건 통과. Housing 회귀(MapLocateFitTests 제외) 281/281 통과.
- [x] 최종 보고

## 발견
- 라이브러리 DetectionNatsSyncService 는 옛 cmd AI_DETECTION 을 받지 않는다 — 호스트 NatsBrokerService :309 가 DETECT 로 정규화해 직접 큐에 넣는다(이중 적재 방지 설계).
- 탐지 + 장애가 한 센서에 겹치면(FaultedDetecting) 마커 점은 주황 · 펄스 없음 — 마커만 보면 탐지가 가려진다(구역선은 빨강/주황 번갈아). 설계(PidsSymbolModel 매핑)로 보이나 제품 판단 필요 → 보고에만 적음.
- 구역선 정상 색 Lime 은 Material 라임 #CDDC39 로 그린다(ColorHelper).

## 남은 일
- 없음(헤디드 남김 목록은 최종 보고에).
