# PLAN — L2: GOP API 8.0.15 대조 · 장비 · 함체 · 지도 표시

목표: 지시문 17항목(G21 G35 G14 G15 G16 G17 G18 G31 G36 G25 G22 G32 G33 G24 G37 G39 + P3 G20 G23 G26 G27 G29 G30 G34 G50)을 고치고, 항목마다 빨강→초록 헤드리스 시험을 붙인다.
판정: `dotnet test Ironwall.Dotnet.Libraries.sln` + tests/GMaps.*.Tests 7개 종료 코드(기준선 비교).
분석 원본: `%TEMP%/claude/.../scratchpad/gop815/plan.md` §1-2, §3 L2.

## 진행
- [x] 기준선: GMaps 7개 + sln (결과는 $TEMP/baseline/summary.txt → 아래 '발견' 에 옮김)
- [x] G21 프리셋 페이지
- [x] G35 계측 잰 시각 · 오래된 값
- [x] G14 SPEED_DOME
- [x] G15 HEAT_HAZE
- [x] G16 임계값 지우기
- [x] G17 warnings
- [x] G18 부품 상태 19종
- [x] G31 그룹 넣기 100 단위
- [x] G36 방송 volume
- [x] G25 음원 그룹 목록
- [x] G22 함체 안 장비
- [x] G32 함체째 부대 이동
- [x] G33 인접 부대 제대
- [x] G24 계측 일괄 초깃값
- [x] G37 LRF 표식
- [x] G39 단건 404 구분
- [x] P3: G20 G23 G26 G27 G29 G30 G34 G50
- [x] 라이브(로컬 8.0.11): 프리셋 11+ · file-groups · enclosure-metrics · 함체 contents → 정리
- [ ] 최종 sln + GMaps 7개

## 작업 나눔 (같은 worktree · 서로 다른 파일 · dotnet 은 $TEMP/locked.ps1 뮤텍스 래퍼로만)
- A API·DTO: G21(DeviceApiService) G22 G25(API·DTO) G17(헬퍼·config) G20 G23 G26 G27 G29 G30 G36(DTO) + 라이브 → 계약은 contracts-A.md
- B Devices.Ui·Models: G15 G16 G17(UI) G18 G31 G32 G33 G34 G39 G14 시험
- C GMaps.Ui: G35 G24 G37 G14(MapViewModel) G36(UI) G25(방송 콤보) + GMaps 7개 재실행
- D Events.Ui 매핑: G21(게이트웨이) G14(EventWindowPlanning·HostMapping*) G50 G25(스피커 행)
- 총괄: CameraDriveKind 작성(완료) · 기준선(깨끗한 복사본 $TEMP/wt-probe, 결과 $TEMP/baseline2) · 통합 · 최종 시험
- 공통 규칙: agent-rules.md

## 결정
DECISIONS.md 참고.

## 발견
- 기준선(깨끗한 복사본): GMaps 7개 전부 exit=0. sln exit=1 — Utils.Tests `ShellHostEntryPointContractTests` 6건 실패인데, 복사본 위치에서 형제 호스트 저장소를 못 찾아서다(실제 worktree 에선 찾음). 그 외 sln 전부 통과.
- ★ sln 에는 Devices.Api · Devices.Ui · Events.Ui · GMaps.Ui · Events.Api 가 없다 → 하네스 검사가 이 묶음 시험을 안 돌린다. 이 5개 csproj 시험을 따로 기준선 · 최종 실행한다($TEMP/baseline2/*.log).
- ★ Devices.Ui(22파일) · Events.Ui(13파일)에 실제 창을 띄우는 시험이 있다 → `$TEMP/nowin.sh <루트> <프로젝트> <로그>` 로 그 클래스들을 빼고 돌린다(실수로 전체 실행했다가 멈춤 — Devices.Ui 창 시험 일부가 돌았음).
- 창 없는 기준선: Devices.Api 244통과/104건너뜀 · Devices.Ui 4103 통과 · Events.Ui 1980 통과 · GMaps.Ui 713 통과 · Events.Api 16통과/44건너뜀 — 전부 exit 0.

- A 완료(13:40경): G21(API) G22 G25(API) G17 헬퍼 SaveWarnings G24 API(GetAllEnclosureLatestMetricsAsync) G23 G26 G27 G20 G29 G30(ServerReferences 덧붙임) G36 DTO. 단위 50건 · 라이브 4건 통과, 라이브 자료(카메라 6048·6051 · 함체 6049 · 제어기 6050) 삭제함.
- D 완료: G21(게이트웨이 공용 ReadAllPresetsAsync) G14(4곳) G50(Diagnostics 로그만) G25(스피커 칸 · IMappingFileGroupSource · 런처 선택 인자 IDeviceApiService). 시험 19건 + 관련 기존 13클래스 214건 통과.

- B 완료: G14 시험 · G15 · G16(EnclosureThresholdClears) · G17(SaveWarningNotice) · G18(19종 · IComponentStateLabels) · G31(ChunkAssigns 3경로) · G32(EnclosureUnitMover) · G33 · G34(DoorPosition) · G39(TryFetch*ByIdAsync → ProviderFetchResult). 관련 454건 + Models 250건 통과.
- C 완료: G35 · G24(RefreshFromServerAsync) · G37 · G14(MapViewModel 2곳) · G36(음량 막대) · G25(방송 창 콤보). 허락한 소유 밖 최소 변경: SymbolDetailViewModel.cs:586 한 줄 · IBroadcastControlService 겹 2개 · BroadcastEventArgs.Volume.
- 총괄: B 가 남긴 틈 — 지도 아이콘 문 상태(`ComponentHealthSummary.ResolveDoorMotion`)도 G34 순서로 고침 + `ComponentDoorOrderTests`(빨강: 함체 1건 FAIL → 초록 2/2).
- 기존 시험 기대값 변경(명세 근거): ComponentDisplayTests 상태 수 10→19 · AssignDeltaTests 호출 수 1→3, 4→6(§5.6 device_ids 1~100) · 계측 '받은 시각'→'잰 시각'(브로커 §6.6 observed_at) · MapMode/OverlayHelp 방송 기대를 음량 겹으로.

- 최종 1회차에서 잡힌 회귀(D 매핑 화면) 3건을 총괄이 고침: 번호 칸 힌트 글(설명형 문구 금지) 삭제 · "?" 키를 글자 그대로(HelpSourceScan 이 상수를 못 읽음) · 상태 TextBlock 의 AutomationId 삭제(peerless 시험).
- 최종 결과: sln exit=0(Utils 886 · CameraPopup 334 등 전부 통과) · GMaps 7개 전부 exit=0 · 창 없는 Devices.Api/Devices.Ui/Events.Ui(2000)/GMaps.Ui/Events.Api 전부 exit=0. 1회차의 CameraPopup 생존 12건 · Events.Ui 추이 시험 1건은 재실행에서 통과(부하 · 날짜 흔들림, 이 작업 파일과 무관).

## 남은 일
- 없음(이 작업 범위). 아래는 후속.
- 후속(이 작업 밖): G24 재연결 사건 연결(L3 accept 뒤 한 줄) · LRF 전용 아이콘(GMapTrackingMarker.cs) · G30 `Servers` 교체(L1 시험 고친 뒤) · 호스트 appsettings 에 `AppSettings.Broadcast.DefaultVolume`(없어도 100).
