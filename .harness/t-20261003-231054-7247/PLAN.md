# PLAN — 탐지 · 장애 · 연결 · 운영 이벤트 확인 + 운영 이벤트 창

## 목표(원문)
"탐지, 장애, 연결, 운영이벤트는 우리 빠져있잖아 그것도 확인해야된다. 창도 있어야될거 같아."

## 해석(결정)
- 이벤트 콘솔(EventDashboardViewModel) 레일: 개요 · 탐지 · 장애 · 연결 · 조치 · 억제. **운영 이벤트 레일이 없었다**
  (API `GET /events/operations` 는 EventApiService 에 있으나 어느 창도 쓰지 않음, NATS 는 문 상태만 반영).
  → "창도 있어야" = 이벤트 콘솔에 **'운영' 레일(목록 + 상세)** 추가. 조회 전용(서버가 스스로 적재, POST/DELETE 경로 없음).
- "확인해야" = 네 종류(탐지 · 장애 · 연결 · 운영)가 콘솔 레일에서 서버 응답 그대로 보이는지 헤드리스 시험 + 실서버 하네스 점검.
- 운영 조치보고(트레이 끌기)는 범위 밖 — 목록 · 상세 · 기간 조회 · 칩 · 검색만. ('탐지·장애만 조치' 규칙 유지)
- 서버 7.0 미만(운영 6.3.2)은 경로가 없다 → API 서비스의 NOT_SUPPORTED 를 PagedResult.NotSupported 로 받아 빈 목록 + 안내(오류 팝업 없음).
- 대시보드 생성자에는 **선택 인자**로 추가(`operationEventPanelViewModel = null`) — 기존 호출부 · 가짜 · 미리보기 도구를 깨지 않는다.
  DI(EventUiModule)에 패널을 등록해 실앱에서는 레일이 선다.
- EventDetailKind.Operation 은 enum **맨 끝**에 추가(기존 값 번호 유지).
- 운영 칩: 전체 · 개폐(사유 접미사 규약 _OPEN/_CLOSED) · 환경 경보 · 경고 이상(WARNING/CRITICAL). 레일 ▲ = 경고 이상 건수,
  목록을 열기 전 배지 = 개요 summary.operation.

## 진행
- [x] OperationEventModel(Events.Ui/Models) · OperationEventText(사유 11종 한글, 모르는 값 원문 보존, detail 한 줄)
- [x] DtoToModelHelper.ToOperationEventModel · EventProviderService.FetchOperationEventsPageAsync · PagedResult.NotSupported
- [x] OperationEventViewModel · OperationEventPanelViewModel(조회 전용, swap-on-success, 쪽 겹침 중복 제거, 기간 캐시)
- [x] 대시보드 레일 '운영'(연결과 조치 사이, 아이콘 DoorOpen) · CurrentKind · CanEdit/CanDelete/CanReport false · 빈 칸 안내
- [x] 상세(BuildOperation, 전부 잠금) · 행 사실 · 칩 · XAML 그리드(Console.Events.Grid.Operation, 별 열 1개)
- [x] DI 등록(EventUiModule)
- [x] 헤드리스 시험 Events.Ui/Tests/EventConsoleFourKindsTests.cs — 36건 통과(4종 레일 → 목록 → 상세, 운영 값 · 칩 · 검색 · 판본 미지원 · 실패 보존 · 기간 와이어)
- [x] EventDataGridColumnFloorTests 열 개수 기대값 26→32 · 별 열 4→5(운영 그리드 6열 추가 반영, 규칙 검사는 그대로)
- [x] 실서버 하네스 점검 추가: tools/live-api-roundtrip/src/Steps.EventsKinds.cs (`[ExtraStep("events-kinds", 95)]`) — 빌드 성공
- [x] Events.Ui 전체 1712/1712 통과 · `dotnet test Ironwall.Dotnet.Libraries.sln` 통과(아래 참고)

## 발견
- `dotnet test Ironwall.Dotnet.Libraries.sln` 은 Events.Ui(WPF) 시험을 돌리지 않는다 — 직접 `dotnet test Ironwall.Dotnet.Libraries.Events.Ui/...csproj` 로 확인함.
- CameraPopup.Host.Tests.DispatcherStarvationTests(20 초 타이밍 시험)가 두 번째 실행에서 1회 실패("flood lasted 20.0 s"), 첫 실행은 통과 — 이번 변경과 무관한 타이밍 흔들림.
- 실서버 하네스 실행(`LRT_ONLY=events-kinds dotnet run --project tools/live-api-roundtrip`)은 이 무인 세션에서 승인 거부로 돌리지 못함.
  돌리면 tools/live-api-roundtrip/results.json 을 덮어쓰므로 먼저 복사해 둘 것.

## 남은 일(선택)
- 실서버(루프백 8.0.x)로 `LRT_ONLY=events-kinds` 실행해 K1a~K1e 확인.
- 실창 육안 확인(운영 레일 · 그리드 폭) — 사용자 데스크톱 독점 필요.
- tools/events-console-preview 에 운영 패널 주입(미리보기에서도 레일 보이게) — 미착수.
