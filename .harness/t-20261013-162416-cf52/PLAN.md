# PLAN — 이벤트 발생 시 지도가 그 위치로 부드럽게 날아간다(줌 + 팬)

## 목표
탐지 · 장애 이벤트가 큐에 들어오면 지도가 그 장비 심볼 위치로 줌을 당기며 시간 기반으로 부드럽게 이동.
설정: 이벤트 시 최대 줌(정수) · 이동 시간(0~5000 ms, 정수, 슬라이더 100 ms 스냅) · 탐지/장애 각각 켜기.
PRD: `docs/prds/event-map-flyto-prd.md`

## 조사 결과(발견)
- 지금 이벤트가 와도 지도는 안 움직인다. `NavigateToMarker` · `OnSymbolNavigateRequested` = `MainMap.Position =` 순간 이동,
  `MapLocateRequest`(MapViewModel.Locate.cs) = `SetZoomToFitRect` 순간 맞춤.
- 줌은 벤더 Zoom 정수 + 디지털 0.5 단(`SetEffectiveZoom`) — 연속 소수 줌 불가 → 줌은 0.5 단계로 시간 곡선 따라 올린다.
- 큐 진입 훅 = `EventQueueManager.OnEntryEnqueued`(구체 형식에만 있음, NATS 스레드 발화).
- 호스트 `SetupModel` 이 `IGMapSetupModel` 을 구현 → 인터페이스에 속성 추가 금지(호스트 CS0535). 설정은 별도 모델 + appsettings 키로.
- 호스트 설정 창 '지도' 레일(`MapSetupViewModel`)은 호스트(Dotnet.Monitoring.Solution) 소유 → 라이브러리는 모델 · 블록 · 키 · DI 등록까지, 호스트 배선은 보고.
- GMaps.* 는 sln 밖 → `dotnet test tests/GMaps.Ui.Tests/GMaps.Ui.Tests.csproj --filter` 로 직접.

## 결정
- 몰림: 마지막 이벤트가 이긴다(우선순위 없음). 진행 중이면 현재 위치 · 줌에서 새 목표로 이어서 출발.
- 줌: 목표 = clamp(설정 최대 줌, 지도 Min, 지도 Max). 이미 그보다 크게 확대돼 있으면 줄이지 않는다.
- 이미 화면 안 + 줌 변화 없음 → 이동 시간 절반. 이미 중심 + 줌 같음 → 이동 없음.
- 양보: 끌기 중 · 마우스 캡처 · 편집 모드 · 독점 입력 모드(그리기 · 측정 · 배치 등) · 최근 2초 내 지도 조작이면 날지 않는다. 비행 중 사용자가 누르거나 휠 → 즉시 멈춤.
- 기본값: 탐지 켬 · 장애 켬 · 최대 줌 19 · 1000 ms.
- 기존 순간 이동 2곳(NavigateToMarker · OnSymbolNavigateRequested)은 공용 `FlyTo(…, 0 ms)` 로 — 동작 동일(순간) + 진행 중 비행 취소.

## 진행
- [x] 조사
- [x] PLAN · PRD
- [x] 모델 `GMaps/Models/MapEventFlyToModel.cs`(+ 정규화)
- [x] 순수 수식 `GMaps.Ui/Utils/MapFlyToMath.cs`
- [x] 저장 · 로드 `MapSettingsHelper`(AppSettings.MapEventFlyTo)
- [x] 설정 블록 VM + XAML + "?" 도움말 키
- [x] MapViewModel.EventFlyTo.cs 배선 + DI 등록
- [x] 헤드리스 시험(FLY-0nn)
- [x] 빌드 · 시험 통과 확인
  - `dotnet test tests/GMaps.Ui.Tests/GMaps.Ui.Tests.csproj --filter "FullyQualifiedName~MapEventFlyToTests"` → 44/44 통과
  - `dotnet build Ironwall.Dotnet.Libraries.GMaps.Ui/…csproj` → 오류 0
  - `dotnet test Ironwall.Dotnet.Libraries.sln` → 전 프로젝트 통과(Utils.Tests 882 — XAML · "?" 계약 훑기 포함)
  - 창을 띄우는 WPF 시험(tests/GMaps.MapLayer · PropertyPanel)은 데스크톱 규칙상 돌리지 않음

## 남은 일 / 호스트 보고
- 호스트 설정 창 '지도' 레일에 `MapEventFlyToSettingsBlock`(DataContext = `MapEventFlyToSetupViewModel` 싱글톤) 끼우기.
- 헤디드 확인 목록은 PRD §8.
