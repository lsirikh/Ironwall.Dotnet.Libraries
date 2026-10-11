# PLAN — 직전 작업(t-20261011-091723-6450) 남은 것 마치기: MAP-056 결함 + rem-lib-a2 71개 (t-20261011-094654-8195)

## 목표
1. `LayerLeafActionScenarioTests.should_hide_that_kind_with_a_single_click_on_the_kind_row_checkbox`(MAP-056 · LAY-D4) 빨강→초록 — 제품 수정, 시험 단언 불변.
2. 첨부 rem-lib-a2.txt 71개 id 각각 결론: (가) 헤드리스 시험 '기대' 단언 + `[Trait("Scenario", id)]` / (나) 헤디드 필요 — 이유 / (다) 관찰 항목 — 이유. 최대한 (가).
   `CameraPopup.Tests/Unit/MapCameraPopupRig.cs` 로 MAP-183~187 시험.
3. 작업자 보고를 전부 받아 반영한 뒤에만 끝낸다(아래 진행 체크박스 전부 [x]).
4. 손댄 시험 프로젝트 전부 실행(창 시험 --filter 제외) → 실패 0 기록.

## 규칙(요약)
창 금지(Window.Show · 화면 밖 창 금지) · 지도 DB/운영 서버/실제 NATS 금지 · 이벤트는 가짜 API 먼저 → 같은 id NATS ·
탐지/장애는 조치보고 전 같은 대상 중복 없음 · 기존 시험 본문 불변(표지 한 줄만) · 공개 형식은 더하기만 · 새 .cs UTF-8 BOM · x:Name 불변.

## 진행
- [x] 첨부 2개 · 직전 PLAN(bb161f0d) 읽기 — 71개 id 모두 아직 Scenario 표지 없음(Grep 확인)
- [x] MAP-056 원인 규명 · 수정(LayerPanelStyle.xaml 두 체크박스 IsThreeState True→False)
- [x] MAP-056 빨강→초록 실행 확인 — 수정 전 GMaps.Ui 로 `실패 1, 통과 6`(메시지 「체크 상태=null(중간), 노드=null」) → 수정 후 MapLayer.Tests `통과 57/57`
- [x] MAP-056 세 상태 순환 · 부모/자식 동기 시험 추가 `tests/GMaps.MapLayer.Tests/LayerKindCheckboxCycleScenarioTests.cs` 3건 통과(3회 반복)
- [x] 기반 빌드(GMaps.Ui · 시험 프로젝트 11개 · Utils.Tests) 오류 0 — 재개 때 bin/obj 가 지워져 다시 빌드
- [x] 작업자 보고 수령 · 반영(첫 실행은 7명 동시 → 사용 한도 429 로 전원 끊김, 산출물 0 → 재개, 결정 D3). 작업자는 보고 파일 쓰기가 막혀 있어 본 세션이 report-W{n}.md 로 옮겨 적음.
  - [x] W1 NAV — 13개 중 (가) 12 · (나) 1(MAP-011), 새 시험 14건, 결함 NAV-D1 → 본 세션 수정(report-W1.md)
  - [x] W2 EDIT — 6/6 (가), 새 시험 17건, 결함 EDT-D1 → 본 세션 수정(report-W2.md)
  - [x] W3 PROP3D — 6/6 (가), 새 시험 30건, 결함 P3D-D1 · P3D-D2 → 본 세션 수정(report-W3.md)
  - [x] W4 AUX — 20/20 (가), 새 시험 31건, 결함 AUX-D5 · D6 · D7 → 본 세션 수정(report-W4.md)
  - [x] W6 CAMERA — 6/6 (가), 새 시험 13건, 결함 없음(report-W6.md)
  - [x] W7 SHELL — 18개 중 (가) 10 · (나) 8, 새 시험 30건, 결함 없음 · 관찰 3건(report-W7.md)
- [x] W5 몫(본 세션): MAP-142 → `tests/GMaps.MapLayer.Tests/SymbolSyncDeviceRenameScenarioTests.cs` 3건 통과 · PERF-016 → (나)(발견 F2)
- [x] 작업자가 보고한 결함 7건 수정(본 세션) · 빨강→초록(아래 결함 표)
- [x] 전체 회귀(손댄 시험 프로젝트 13개) 실패 0 · TRX 증거(아래 회귀 결과)
- [x] 새 .cs 25개 UTF-8 BOM 확인(ef bb bf) · sln 빌드 오류 0 · Utils XAML 계약 시험(ConsoleStyleContractTests · HelpContractTests) 64/64

## 결정
- D1(MAP-056): 원인 = `LayerPanelStyle.xaml` NodeRowTemplate(그룹/종류 행)과 섹션 머리 전체 체크의 `IsThreeState="True"`.
  WPF ToggleButton.OnToggle 은 IsThreeState=True 일 때 켜짐 → null(중간)로 보내고, `LayerTreeNode.IsChecked` 세터는 null 을 자식에 내리지 않는다
  (`value.HasValue` 조건, 중간은 집계 표시 전용) → 한 번 눌러도 아무것도 안 숨음. 잎 체크박스는 두 상태라 한 번에 꺼져 어긋났다.
  수정 = `IsThreeState="False"`(두 곳). 중간 표시는 바인딩으로 그대로 보인다(IsThreeState 는 누름 순환만 정함).
  누름 순환: 켜짐 → 꺼짐 · 꺼짐 → 켜짐 · 중간 → 꺼짐(WPF 기본). 모델 세터 · 집계 로직은 그대로.
- D2: 작업자를 시험 프로젝트별로 나눈다(같은 프로젝트 동시 빌드 = obj 충돌). 제품 코드 수정은 본 세션만(작업자는 결함을 빨강 증거로 보고).
  - W1 NAV → tests/GMaps.MapNav.Tests: MAP-001 · 011 · 013 · 014 · 021 · 022 · 023 · 029 · 039 · 040 · 194 · 195 · 196
  - W2 EDIT → tests/GMaps.MapEdit.Tests: MAP-093 · 094 · 109 · 110 · 132 · 134
  - W3 PROP3D → tests/GMaps.PropertyPanel.Tests(· Housing 실행): MAP-122 · 123 · 126 · 127 · 129 · 149
  - W4 AUX → tests/GMaps.MapAux.Tests: MAP-085 · 153 · 154 · 157 · 160 · 161 · 162 · 163 · 165 · 166 · 168 · 173 · 177 · 178 · 179 · 191 · AUX-012 · 017 · 018 · 021
  - W5 → 본 세션(D4): MAP-142 · PERF-016
  - W6 CAMERA → CameraPopup.Tests(MapCameraPopupRig): MAP-183 ~ 187 · EVM-137
  - W7 SHELL → Accounts.Ui.Tests · Reports.Ui · Utils.Tests: ACC-034 · 044 · RPT-010 · 020 · AUX-002 · 003 · 006 · 023 ~ 026 · 028 ~ 034
- D3: 7명 동시 실행이 사용 한도(429)에 걸려 전원 산출물 없이 끊김 → 같은 작업자를 SendMessage 로 재개. 재개 때 하네스가 bin/obj 를 지워
  본 세션이 기반을 다시 빌드하고 `base-build-done.txt` 로 알림(작업자는 --no-dependencies 만).
- D4: W5(MAP-142 · PERF-016)는 범위가 작아 본 세션이 직접. MAP-142 는 GMaps.Ui 경로(상세 VM · 심볼 컨트롤)라 GMaps 시험이어야 하고,
  tests/GMaps.Ui.Tests 는 GMaps.Ui 를 참조하지 않는 순수 로직 프로젝트(net8.0)라 본 세션 전용 tests/GMaps.MapLayer.Tests 에 둔다.
- D5(AUX-D7): 클램프 규칙을 레이어 패널(LAY-D3) 것과 같게 공용 `Helpers/OverlayHeaderClamp.cs`(internal)로 빼고, 시험된 5개 겹창(상세 · 센서 정보 · 추적 설정 · 재생 · 사이트 고정)
  뿐 아니라 같은 끌기 패턴인 방송 · TTS · 지도 등록 · 속성창 겹창에도 적용(머리 높이는 각 OnDragDown 기준 38/36/42/45). 한쪽만 고치면 같은 결함이 남기 때문.
- D6(AUX-D5/D6): Esc 는 겹창 컨트롤의 **버블 KeyDown**(아직 처리 안 된 Esc만) → 닫기 단추와 같은 CloseCommand. PreviewKeyDown 으로 하면 MAP-166
  (열린 "?" 는 첫 Esc 에 말풍선만 닫힘 · 닫힌 뒤엔 Esc 를 가로채지 않음) 단언과 실제 HelpTip 창 단 처리 순서를 깬다.
- D7(P3D-D1): 처음엔 W3 제안대로 xmlns:house 에 assembly 를 붙였으나 여전히 PathError(시험으로 확인) → 실제 원인은 **위치 인자** `{Binding 경로}` 가
  Binding(string) 생성자로 접두사 해석기 없이 PropertyPath 를 만드는 것. `Path=` 명명 인자로 바꾸고 xmlns 는 원래대로 되돌림.
- D8(P3D-D2): 탐지 범위 · 각도 · 방향은 기준방향(BaseBearing)과 같은 길 — 단일=마커 쓰기 + OnMarkerPropertyChanged(저장 · undo 병합), 그룹=VM 일괄(ApplyProperty).
  UndoableCommandBase 의 ApplyProperty · ReadProperty · IsReplayableProperty 에 세 이름 등재. 슬라이더 연속 이동은 VM 의 EditRecorder 병합(coalescing)에 맡김(지연 커밋 신설 안 함).
- D9(증거): 작업 트리 밖(메인 docs/tests/evidence) 쓰기 · 읽기가 막혀 TRX + meta.json 을 `.harness/t-20261011-094654-8195/evidence/20261011-105822-rem-lib-a2/` 에 둔다(하네스가 커밋).
  상태표 생성기(python collect.py)는 승인 없이 실행이 막혀 돌리지 못함 — 사람이 다음 명령으로 만들 수 있다:
  `python tools/scenario-evidence/collect.py --catalog docs/tests/gis-map-event-scenario-catalog.md --repo . --evidence .harness/t-20261011-094654-8195/evidence --out <출력 폴더>`
  (메인 docs/tests/evidence/20261011-105424-rem-lib-a2/ 에 같은 회귀 첫 실행의 TRX 13개가 meta.json 없이 남아 있다 — collect.py 는 meta.json 없는 폴더를 무시하므로 해 없음.)

## 발견
- F1(MAP-142): 우클릭 「{장치명}페이지」의 {장치명}은 장비 이름이 아니라 **종류 이름**(ShowMarkerContextMenu 의 `pidsMarker.DeviceType switch`, 카메라=「감시카메라」) — 호스트 r27 실측과 같다.
  상세 액션 바도 `UiKoreanMap.To(DeviceType)`. 이름 변경이 바꾸는 것은 상세 기본 탭 「장비명」뿐이고 심볼 제목(지도 DB Title) · 툴팁은 그대로(설계). 모두 시험으로 단언.
  참고: GMaps 는 DeviceProvider.DeviceUpdated 를 구독하지 않아 **이미 열린** 상세 창은 이름 변경 뒤 자동 갱신되지 않는다(다시 열면 새 이름) — 카탈로그 기대와 어긋나지 않아 결함 아님.
- F2(PERF-016): 라이브러리 NATS 표면(INatsService/IMessageService)에 끊김 · 재연결 이벤트가 없고, 재연결 뒤 재조회(보충) 경로가 제품 어디에도 없다
  (Nats · Events.Ui · 호스트 NatsDomainService Grep — Reconnect/Disconnect 0건). 호스트 E2E 도 EVT-E2E-090/091 을 "공유 NATS 를 멈출 수 없다 · 끊김 표시 UI 없음(GAP-C11)" 으로 건너뛴다.
  → 증명할 제품 기능 자체가 없음. 실 브로커 재시작(전용 스택) + 보충 기능 신설(별도 PRD) 필요 — 이 작업 범위 밖.
- F3(W7 관찰): ① 진입점 0 인 옛 화면(Events.Ui EventSuppressionSchedulePanelView/VM · 호스트 AboutSetupView · AccountManagerPanelView · Sounds.Ui SoundSettingView · 호스트 OpenVcaPanelViewModel) — 정리 후보
  ② 호스트의 낡은 시험 4개(SuppressionSweepTests · PanelNavigationMatrixTests:43 · AppSweepTests:361 · SurfaceCatalogTests)가 옛 억제창 기준 — 호스트 저장소라 범위 밖
  ③ AUX-006: 카탈로그 "비관리자 → WINDY 행 숨김" vs 실제 broadcast:control 권한 조건(호스트 LeftMenuSectionViewModel.cs:154) — 의도 확인 필요.
- F4(W3 관찰): GeometricPropertyStyle.xaml:21-35 도형 종류 콤보가 주석 처리(속성창에서 도형 모양 변경 불가) · ComponentCardPlacement.cs:30-31 아이콘이 지도 오른쪽 밖이면 카드를 끌어오지 않음 — 의도 확인 권장.
- F5(시험 의존): W7 의 `Utils.Tests/ShellHostEntryPointContractTests`(sln 안)는 형제 호스트 저장소(C:\workspace_app\Dotnet.Monitoring.Solution) 소스를 읽고, 없으면 실패로 알린다 — 하네스 sln 검사가 그 저장소 상태에 묶인다.
- F6(시험 장치): W6 이 앞 작업의 시험 전용 internal 장치 `MapCameraPopupRig.cs` 두 곳을 보완(STA IoC 에 IEventAggregator 등록 · NewVm 의 IsNotifying=true) — 그대로는 장치를 쓰는 시험이 전부 깨졌다.

## 결함 표(빨강 → 초록)
| 결함 | id | 증상 | 원인 · 수정 | 빨강 증거 | 초록 증거 |
|---|---|---|---|---|---|
| LAY-D4 | MAP-056 | 켜진 종류 행 체크박스를 한 번 눌러도 안 숨음(체크=null) | LayerPanelStyle.xaml 묶음 행 체크박스 2곳 IsThreeState True→False | 수정 전 GMaps.Ui 로 LayerLeafActionScenarioTests 실패 1/통과 6 | MapLayer.Tests 63/63(순환 시험 3건 포함) |
| EDT-D1 | MAP-132 | 통문/함체 속성창을 막 열면 권한이 있어도 「권한 없음」 · 단추 꺼짐(다음 보고까지) | GMapPropertyPidsControl.CanControlDevice · DoorCommandSender 를 넣는 즉시 RefreshDoorUi(배선 순서 무관, 같은 시그니처 속성) | W2: MapEdit 실패 1/통과 54(3회 동일) | MapEdit.Tests 55/55 |
| NAV-D1 | MAP-040 | 지도 전환이 appsettings.json 에 저장 안 됨(재시작하면 이전 지도) | MapSettingsHelper.SaveMultipleSettingsAsync — 이미 있는 AppSettings 절 재대입(Json.NET 복제)을 없을 때만 생성으로 | W1: MapNav 실패 2/통과 45(3회 동일) | MapNav.Tests 47/47 |
| AUX-D5 | MAP-160 | 상세 겹창이 Esc 로 안 닫힘 | SymbolDetailControl 버블 KeyDown(미처리 Esc) → CloseCommand | W4: MapAux 실패 3/통과 73(3회 동일) | MapAux.Tests 76/76(MAP-166 말풍선 우선 유지) |
| AUX-D6 | MAP-085 | 추적 설정 겹창이 Esc 로 안 닫힘 | TrackingSettingsControl 버블 KeyDown(미처리 Esc) → CloseCommand | (위와 같은 실행) | MapAux.Tests 76/76 |
| AUX-D7 | MAP-191 | 겹창 머리를 창 끝 너머로 끌면 지도 밖(Left/Top=-2900)으로 나가 다시 못 잡음 | 공용 OverlayHeaderClamp(LAY-D3 규칙) → 겹창 9종 OnDragMove(결정 D5) | (위와 같은 실행) | MapAux.Tests 76/76 |
| P3D-D1 | MAP-122 · 126 | 속성창 '색상 강도' 슬라이더가 3D 하우징 · 건물에 안 먹음(PathError) | Pids/InfraPropertyStyle.xaml 슬라이더 바인딩을 `{Binding Path=…(house:HousingAppearance.TintStrength)}` 로(결정 D7) | W3: PropertyPanel 실패 5/통과 274 · 본 세션 xmlns 시도 후에도 실패 2 | PropertyPanel.Tests 279/279 |
| P3D-D2 | MAP-123 | 탐지범위 · 각도 · 방향 변경이 DB 저장 요청을 안 냄(재시작하면 원복 · undo 없음) | GMapPropertyPidsControl 세 콜백 → OnMarkerPropertyChanged + UndoableCommandBase 등재(결정 D8) | (W3 같은 실행 · 3건) | PropertyPanel.Tests 279/279 |

## 회귀 결과(최종 — 모든 제품 수정 뒤, run `20261011-105822-rem-lib-a2`, TRX = `.harness/t-20261011-094654-8195/evidence/20261011-105822-rem-lib-a2/*.trx` · meta.json)
| 프로젝트 | 필터(창 · 실프로세스 제외) | 결과 |
|---|---|---|
| tests/GMaps.MapNav.Tests | 없음 | 통과 47/47 |
| tests/GMaps.MapLayer.Tests | 없음 | 통과 63/63 |
| tests/GMaps.MapEdit.Tests | 없음 | 통과 55/55 |
| tests/GMaps.MapAux.Tests | 없음 | 통과 76/76 |
| tests/GMaps.PropertyPanel.Tests | `FullyQualifiedName!~ComponentCardScreenSizeTests` | 통과 279/279 |
| tests/GMaps.Housing.Tests | `FullyQualifiedName!~MapLocateFitTests` | 통과 300/300 |
| tests/GMaps.Ui.Tests | 없음 | 통과 757/757 |
| Ironwall.Dotnet.Libraries.GMaps.Ui(자체 시험 — 제품 수정 대상이라 추가) | 없음 | 통과 680/680 |
| Ironwall.Dotnet.Libraries.Events.Ui | 창 6클래스 제외(EventConsoleSingleSelectionDetailTests · EventToolbarChipAutomationTests · MappingDragInteractionTests · SuppressionDayChipAutomationTests · SuppressionRecurrenceRadioTests · SuppressionRowSelectBindingTests) | 통과 2005/2005 |
| Ironwall.Dotnet.Libraries.Accounts.Ui.Tests | `FullyQualifiedName!~AccountConsoleCloseButtonTests`(실창) | 통과 391/391 |
| Ironwall.Dotnet.Libraries.Reports.Ui | 없음(창 시험 없음) | 통과 365/365 |
| Ironwall.Dotnet.Libraries.CameraPopup.Tests | `Category!=Survival&Category!=LibVlc&Category!=RealCamera`(실호스트 · LibVLC · 실카메라) | 통과 251/251 |
| Ironwall.Dotnet.Libraries.Utils.Tests | 새 시험 2클래스 14건 + XAML 계약 2클래스 64건(나머지는 sln 하네스 검사가 돌림, W7 창 제외 전체 687/687) | 통과 14/14 · 64/64 |
- TRX 13+1개 전부 `failed="0"`, `outcome="Failed|NotExecuted|Error|Timeout|Aborted"` 0건(Grep). 합계 5,283 + 64 통과.
- sln `dotnet build Ironwall.Dotnet.Libraries.sln` 오류 0. `dotnet test` sln(하네스 검사)은 창 시험이 섞여 있어 본 세션은 돌리지 않음(사용자 PC 사용 중 · 창 금지).

## 하네스 검사 1차(sln) — 2637 통과 / 1 실패(CameraPopup.Tests 286 중 1) 분석
- [x] 실패 시험 특정: 출력 끝의 `host error not-supported scope=cam19~cam49 ptz ContinuousMove` 경고는 `SupervisorSurvivalTests.should_kill_and_restart_when_host_hangs`(K2)만 보내는
  PTZ 명령(`"cam" + (i % 50)`, 2000건)이고, 그 로그는 K2 가 재시작 뒤 14초 안에 Running 이 안 될 때만 쏟는다(`if (running is null) DumpLog(log)`) → K2 의 재시작 시간 초과.
- [x] K2 의 시험 대상은 이번 작업과 무관: 감시자(Ironwall.Dotnet.Libraries.CameraPopup — 참조 Base · Contracts · Streaming.Base · Utils) · 호스트(CameraPopup.Host — 참조 Contracts · Theme · Providers)
  어느 것도 이번 작업에서 바뀌지 않았다(제품 변경은 GMaps.Ui 한 곳뿐, Utils 는 시험만 추가). 기준선 실행(같은 워크트리, 작업 전)은 2605 통과 · 실패 0.
- [x] 새 지도 팝업 시험(MapCameraPopupSta 모음)과의 간섭 점검: 그 모음과 Survival 모음은 둘 다 `DisableParallelization = true` 라 겹쳐 돌지 않는다(병렬 모음 뒤 하나씩).
  새 시험의 Caliburn IoC 교체는 STA 스레드 끝에서 되돌리고, 지도 컨트롤은 디스패처 종료 때 스스로 닫히며(Dispatcher_ShutdownStarted → Dispose → OnMapClose),
  실제 감시자를 쓰는 EVM-137 시험은 `using` 으로 해제하고 실행 파일이 없어 프로세스를 띄우지 않는다 · 파이프 이름은 토큰별(PipeNaming.Build).
- [x] 부하 재현(창 없는 범위): Events.Ui · Housing · PropertyPanel · GMaps.Ui.Tests 를 뒤에서 동시에 돌리며 CameraPopup Unit 251건 → 통과 251/251.
- 판단: K2 는 실제 호스트 프로세스를 멈추게 한 뒤 죽이고 다시 띄우는 시간 민감 시험(탐지 2.5~6.5초 · 재시작 3초 · Running 14초)이고, sln 실행은 시험 프로젝트들을
  동시에 돌려 CPU 가 바쁘다 → 이번 실패는 이번 변경이 아니라 실행 환경의 시간 초과로 본다(증거: 시험 대상 코드 불변 · 간섭 경로 없음 · 기준선 통과).
  Survival 범주는 호스트 프로세스가 실제 이벤트 창 · 오버레이 창을 띄우므로 창 금지 규칙상 본 세션이 직접 다시 돌리지 않았다 — 하네스 재검사로 확인한다.
  시험(K2) · 검사 명령은 바꾸지 않는다.

## 남은 일
- 하네스 재검사 결과 확인(위 판단 검증). 후속 후보: F2 끊김 보충 기능(PRD) · F3/F4 의도 확인 · 상태표 재생성(D9 명령).

## id 별 결과표(71개 — (가) 61 · (나) 10 · (다) 0, 부분 한계는 비고)
| id | 결론 | 시험(가) 또는 이유(나) | 비고 |
|---|---|---|---|
| MAP-001 | 가 | MapNavRemainingScenarioTests::should_expose_map_root_and_only_known_marker_types_when_symbols_loaded | |
| MAP-011 | 나 | 메뉴는 대상 없는 OpenSetupPanelMessageModel 발행까지(라이브러리) — 설정 › 지도 카드는 호스트 앱(ConductorControlViewModel → SetupPanelViewModel)이라 창 없이 못 띄움. 발행은 기존 ZoomViewScenarioTests::should_publish_setup_panel_open_when_zoom_range_menu_chosen | |
| MAP-013 | 가 | MapNavRemainingScenarioTests::should_move_center_and_return_when_empty_spot_dragged_and_dragged_back_in_operation_mode | 포인터 이동만 벤더 코어 BeginDrag/Drag |
| MAP-014 | 가 | MapNavRemainingScenarioTests::should_pan_without_rubber_band_or_selection_when_empty_spot_dragged_in_edit_mode_without_shift | |
| MAP-021 | 가 | MapNavRemainingScenarioTests::should_show_tilt_badge_and_foreshorten_map_when_tilt_toggle_clicked_above_gate_zoom | |
| MAP-022 | 가 | MapNavRemainingScenarioTests::should_hold_then_flatten_then_reenter_tilt_when_zoom_goes_18_5_17_5_17_0_18_5 | |
| MAP-023 | 가 | MapNavRemainingScenarioTests::should_show_35_degrees_in_badge_and_angle_text_when_angle_slider_set_to_35 | |
| MAP-029 | 가 | MapNavRemainingScenarioTests::should_hide_badge_and_restore_identical_view_state_when_tilt_turned_off | 픽셀 대신 그림을 정하는 상태값 비교 |
| MAP-039 | 가 | MapNavRemainingScenarioTests::should_keep_compass_position_after_round_trip_and_clamp_display_in_small_window | 끌기 포인터 이동 자체는 헤디드 몫 |
| MAP-040 | 가 | MapToolbarScenarioTests::should_switch_base_tiles_and_keep_combo_and_menu_radio_in_sync_when_map_changed_and_restored · ::should_persist_selected_map_name_to_settings_file_when_map_switched · ::should_write_map_name_into_existing_app_settings_section_when_map_settings_saved | NAV-D1 수정 |
| MAP-085 | 가 | OverlayHelpPlaybackScenarioTests::should_open_tracking_settings_from_playback_gear_and_close_it_with_close_button_when_playback_panel_used · ::should_close_tracking_settings_overlay_when_escape_pressed_inside_it | AUX-D6 수정 |
| MAP-093 | 가 | SymbolCreateScenarioTests::should_create_symbol_at_drop_point_and_insert_db_row_when_palette_cell_is_dropped_on_empty_map · ::should_insert_pids_row_with_device_type_and_variant_at_drop_point_when_pids_cell_is_dropped · ::should_not_insert_row_when_drop_lands_outside_map_or_placement_was_not_started | |
| MAP-094 | 가 | SymbolCreateScenarioTests::should_create_symbol_at_clicked_point_and_insert_db_row_when_cell_is_selected_then_map_is_clicked · ::should_place_one_symbol_per_click_at_each_clicked_point_when_cell_is_reselected | |
| MAP-109 | 가 | PropertyPanelScenarioTests::should_open_property_panel_with_title_field_and_zorder_buttons_when_symbol_is_clicked_in_edit_mode · ::should_save_edited_title_from_title_field_to_marker_and_db_after_typing_pause | |
| MAP-110 | 가 | PropertyPanelScenarioTests::should_close_property_panel_and_release_selection_when_close_button_is_clicked · ::should_move_property_panel_by_header_drag_delta_inside_property_canvas · ::should_not_start_drag_when_panel_is_not_draggable | 헤더 45px 판정 · 캡처 · 실좌표는 헤디드 몫 |
| MAP-122 | 가 | PidsHousingPropertyScenarioTests::should_lift_3d_housing_and_save_altitude_to_db_when_install_height_slider_moves · ::should_swap_3d_model_and_save_variant_to_db_when_model_variant_combo_changes · ::should_retint_3d_housing_body_without_db_write_when_tint_strength_reaches_symbol_shape · ::should_retint_3d_housing_body_without_db_write_when_color_intensity_slider_moves | P3D-D1 수정 |
| MAP-123 | 가 | PidsHousingPropertyScenarioTests::should_show_recolor_and_fade_fov_wedge_and_save_each_to_db_when_fov_rows_change · ::should_redraw_wedge_geometry_when_detection_range_angle_and_bearing_sliders_move · ::should_save_detection_value_to_db_when_detection_slider_moves(×3) | P3D-D2 수정 |
| MAP-126 | 가 | InfraShapeFencePropertyScenarioTests::should_disable_floor_sliders_and_hide_floor_text_when_building_type_has_no_floors · ::should_gate_floor_rows_and_marker_floor_text_by_building_type_when_selected_in_panel(×12) | 건물 색상 강도 = P3D-D1 수정 |
| MAP-127 | 가 | InfraShapeFencePropertyScenarioTests::should_draw_distinct_silhouette_for_every_palette_shape_when_shape_symbols_are_created · ::should_fade_symbol_alpha_and_save_opacity_to_db_when_opacity_slider_moves · ::should_hide_and_restore_shape_and_save_show_shape_to_db_when_shape_checkbox_toggles | 도형 종류 콤보 주석(F4) |
| MAP-129 | 가 | InfraShapeFencePropertyScenarioTests::should_rebuild_3d_fence_posts_and_summary_and_save_to_db_when_fence_section_edited | Symbol3D 플래그 게이트 뒤 본문(ApplyFrame) 직접 구동 |
| MAP-132 | 가 | DoorAndMultiSelectScenarioTests::should_publish_gate_door_set_to_nats_and_follow_reported_state_when_door_buttons_are_pressed · ::should_release_pending_and_notify_when_nats_publish_fails · ::should_keep_door_buttons_off_and_publish_nothing_without_device_control_permission · ::should_show_reported_door_state_and_enable_buttons_as_soon_as_gate_property_panel_opens | EDT-D1 수정 · 가짜 NATS 발행 내용 단언 |
| MAP-134 | 가 | DoorAndMultiSelectScenarioTests::should_toggle_members_and_show_group_adorner_only_for_two_or_more_when_symbols_are_ctrl_clicked · ::should_group_unlocked_symbols_inside_rubber_band_and_attach_group_adorner_when_shift_dragged · ::should_ignore_ctrl_click_toggle_when_edit_mode_is_off | Ctrl/Shift 판정은 헤디드 몫 |
| MAP-142 | 가 | SymbolSyncDeviceRenameScenarioTests::should_show_new_device_name_in_detail_basic_tab_but_keep_symbol_title_when_sync_device_renames_linked_device · ::should_keep_device_page_action_labelled_by_kind_not_device_name_after_sync_device_rename · ::should_build_right_click_device_page_header_from_device_kind_so_rename_does_not_change_menu | F1 · 우클릭 메뉴는 팝업이라 소스 판독 |
| MAP-149 | 가 | ComponentCardZoomTiltScenarioTests::should_keep_card_screen_size_and_stay_inside_map_when_zoom_steps_half_and_whole · ::should_keep_card_upright_same_size_and_inside_map_when_map_tilt_toggles · ::should_show_full_intent_mismatch_state_text_in_card_when_heater_set_off_but_observed_on | |
| MAP-153 | 가 | MarkerMenuOpsScenarioTests::should_list_detail_components_unit_device_pages_and_history_in_order_when_sensor_symbol_right_clicked_in_operation_mode · ::should_add_home_page_aim_and_sound_items_by_type_and_open_their_panels_when_controller_camera_speaker_right_clicked · ::should_keep_device_page_items_visible_but_disabled_and_hide_menu_for_locked_symbol_when_web_server_off | MenuTrap(창 생성 전 차단) |
| MAP-154 | 가 | MarkerMenuOpsScenarioTests::should_omit_detail_and_unit_line_and_append_order_items_when_symbol_right_clicked_in_edit_mode | |
| MAP-157 | 가 | MarkerMenuOpsScenarioTests::should_hand_device_page_and_home_page_urls_to_browser_opener_when_external_menu_items_clicked | 실제 Chrome 실행만 가짜 |
| MAP-160 | 가 | SymbolDetailScenarioTests::should_open_one_detail_overlay_and_close_it_with_close_button_then_reuse_it_when_detail_shown_again · ::should_close_detail_overlay_when_escape_pressed_inside_it | AUX-D5 수정 |
| MAP-161 | 가 | SymbolDetailScenarioTests::should_show_basic_components_symbol_tabs_with_ids_and_switch_fields_when_tab_buttons_pressed · ::should_disable_device_tabs_with_reason_and_start_on_symbol_tab_when_symbol_has_no_linked_device | 2026-09-09 결정으로 공통 탭(결함 아님) |
| MAP-162 | 가 | SymbolDetailScenarioTests::should_auto_rotate_in_45_degree_steps_pause_with_toggle_reset_to_front_and_resume_after_drag_when_3d_stage_used | 픽셀 변화는 헤디드 몫 |
| MAP-163 | 가 | SymbolDetailScenarioTests::should_show_type_specific_actions_with_enable_rules_and_reason_help_text_when_detail_opened_for_sensor_speaker_gate · ::should_disable_every_device_action_except_page_and_show_on_map_when_symbol_unlinked | |
| MAP-165 | 가 | SymbolDetailScenarioTests::should_request_unit_map_and_show_block_reason_until_launcher_reports_shown_when_open_unit_map_pressed · ::should_disable_open_unit_map_for_unknown_unit_and_hide_line_when_unit_directory_unavailable | |
| MAP-166 | 가 | SymbolDetailScenarioTests::should_close_only_the_detail_callout_and_keep_detail_overlay_when_escape_pressed_with_question_mark_open | |
| MAP-168 | 가 | MarkerMenuOpsScenarioTests::should_open_sensor_info_overlay_and_request_group_history_when_pidsgroup_menu_items_clicked · ::should_disable_group_items_with_reason_tooltip_when_group_unlinked_or_has_no_sensors(×2) | |
| MAP-173 | 가 | OverlayHelpPlaybackScenarioTests::should_close_measure_callout_first_without_cancelling_measure_then_cancel_measure_with_second_escape | 기록: 첫 Esc 는 측정 취소 안 함 |
| MAP-177 | 가 | DrawingHudScenarioTests::should_insert_pids_group_row_and_add_zone_line_marker_when_palette_pids_group_button_points_then_hud_complete | |
| MAP-178 | 가 | DrawingHudScenarioTests::should_remove_last_point_with_undo_button_and_cancel_without_db_with_close_button_on_drawing_hud · ::should_undo_with_backspace_and_ctrl_z_complete_with_enter_and_cancel_with_escape_when_drawing_keys_pressed | |
| MAP-179 | 가 | DrawingHudScenarioTests::should_keep_hud_screen_size_and_let_clicks_right_of_hud_reach_map_when_digital_zoom_and_tilt_change | |
| MAP-183 | 가 | MapCameraPopupScenarioTests::should_play_overlay_and_reopen_at_moved_spot_when_camera_symbol_double_clicked · ::should_bring_open_popup_to_front_without_second_stream_when_same_camera_double_clicked_again | 머리 끌기 이동량만 직접 주입 |
| MAP-184 | 가 | MapCameraPopupScenarioTests::should_keep_leader_line_from_symbol_to_popup_edge_inside_map_when_panned_and_remove_it_when_closed | |
| MAP-185 | 가 | MapCameraPopupScenarioTests::should_send_one_relative_drag_move_when_ptz_overlay_video_dragged_and_none_when_clicked_or_cancelled · ::should_show_reason_and_send_no_ptz_when_fixed_or_unpermitted_camera_overlay_dragged | |
| MAP-186 | 가 | MapCameraPopupScenarioTests::should_show_host_notice_with_restart_on_map_when_host_suspended_and_restart_host_from_it | 실제 예산 소진은 가짜 호스트 상태로 |
| MAP-187 | 가 | MapCameraPopupScenarioTests::should_show_broker_toast_and_open_no_overlay_when_camera_double_clicked_in_broker_mode · ::should_show_failure_toast_and_open_no_overlay_when_nvr_rejects_or_never_answers(×2) · ::should_show_no_broker_toast_when_broker_mode_has_no_broker_service | |
| MAP-191 | 가 | OverlayHelpPlaybackScenarioTests::should_keep_layer_panel_and_palette_header_inside_map_when_dragged_past_window_edges · ::should_keep_every_overlay_header_inside_map_when_dragged_past_top_left_edge | AUX-D7 수정 |
| MAP-194 | 가 | MapToolbarScenarioTests::should_expose_map_combo_four_operational_buttons_and_mode_toggles_in_order_when_toolbar_rendered | 카탈로그 '토글 4' 는 기울이기 추가 전 표기 |
| MAP-195 | 가 | MapToolbarScenarioTests::should_move_hidden_items_into_overflow_popup_body_and_show_overflow_button_when_toolbar_narrowed | Esc 닫기(팝업 HWND)는 제외 |
| MAP-196 | 가 | MapToolbarScenarioTests::should_run_measure_and_edit_from_overflow_items_when_toolbar_narrowed | |
| EVM-137 | 가 | MapCameraPopupScenarioTests::should_fail_only_the_dead_stream_popup_and_retry_only_it_when_one_camera_stream_dies · ::should_keep_gis_alive_show_connecting_and_restore_every_popup_when_host_dies_and_comes_back · ::should_keep_restore_list_and_offer_restart_when_real_supervisor_has_no_host | 호스트 TileView 는 Host.Tests 몫 |
| ACC-034 | 가 | ShellAccountsLeftoverScenarioTests::should_hand_the_file_chosen_in_the_add_picture_window_straight_to_the_same_picture_path_and_do_nothing_when_cancelled · ::should_show_the_chosen_picture_as_a_copy_in_the_profile_folder_and_upload_that_copy_once_the_account_is_created · ::should_keep_the_previous_picture_and_say_why_when_the_chosen_file_cannot_be_a_photo(×2) | Win32 파일창 seam 없음 → 창이 돌려준 경로 이후를 탐 |
| ACC-044 | 나 | 판정기(LayoutAudit · LayoutSweep · CheckLayout)가 호스트 UiTests FlaUI 실앱(UIA rect + 캡처 밝기 테마 판별) — 실제 계정 뷰는 Application + App.xaml 병합이 필요하고 오프스크린 창 금지 | |
| RPT-010 | 나 | 줌 = ReportLargePreviewWindow(자체 HWND Window) 코드숨김 → WebView2.ZoomFactor — Window 생성 금지 · WebView2 는 HWND/런타임 필요 | |
| RPT-020 | 나 | ACC-044 와 같음(토큰 계약은 기존 ReportLeftoverScenarioTests 가 보나 "레이아웃 위반 없음" 단언 아님) | |
| AUX-002 | 가 | ShellKernelScenarioTests::should_close_the_windy_card_through_the_same_cancel_path_from_the_close_button_the_header_x_and_escape · ShellHostEntryPointContractTests::should_wire_the_windy_header_x_and_escape_to_the_close_button_that_closes_the_panel | |
| AUX-003 | 가 | ShellKernelScenarioTests::should_close_only_the_windy_callout_on_the_first_escape_and_close_the_card_on_the_second | |
| AUX-006 | 나 | "먼저 묻기" 가드(호스트 PanelShellViewModel · PanelLeaveRule) · 행 숨김(호스트 LeftMenuSectionViewModel) 모두 호스트에만 — 라이브러리 시험은 호스트 참조 불가 | 조건 불일치 관찰(F3 ③) |
| AUX-012 | 가 | OverlayHelpPlaybackScenarioTests::should_open_tracking_settings_from_playback_gear_and_close_it_with_close_button_when_playback_panel_used | MAP-085 와 공유 |
| AUX-017 | 가 | OverlayHelpPlaybackScenarioTests::should_move_playback_marker_along_recorded_points_with_speed_and_stop_on_pause_when_playback_runs | 가짜 추적 기록 · 가짜 시계 |
| AUX-018 | 가 | OverlayHelpPlaybackScenarioTests::should_open_one_tracking_or_playback_callout_at_a_time_and_close_it_with_escape_keeping_panels_open | |
| AUX-021 | 가 | OverlayHelpPlaybackScenarioTests::should_send_play_request_with_typed_group_and_repeat_and_close_panel_without_notice_when_speaker_accepts · ::should_keep_panel_and_show_speaker_reason_then_allow_resend_when_play_request_rejected_or_unanswered | 가짜 방송 서비스 |
| AUX-023 | 가 | ShellAccountsLeftoverScenarioTests::should_follow_the_callout_rules_escape_outside_click_one_at_a_time_and_f1_on_the_account_question_marks · ShellReportsLeftoverScenarioTests::should_follow_the_callout_rules_escape_outside_click_one_at_a_time_and_f1_on_the_report_and_phrase_question_marks | |
| AUX-024 | 가 | ShellKernelScenarioTests::should_place_the_callout_body_inside_the_shell_wherever_the_question_mark_sits(×4) | 설정 레일별 실제 rect 는 r27 몫 |
| AUX-025 | 가 | ShellAccountsLeftoverScenarioTests::should_draw_the_whole_callout_text_with_its_body_id_and_close_it_with_escape_for_every_account_key(×5) · ShellReportsLeftoverScenarioTests::should_draw_the_whole_callout_text_with_its_body_id_and_close_it_with_escape_for_every_report_key(×5) | 설정 키 6개는 호스트(r27) |
| AUX-026 | 가 | ShellKernelScenarioTests::should_open_the_console_header_question_mark_when_f1_is_pressed_where_no_section_owns_the_focus | |
| AUX-028 | 가 | ShellKernelScenarioTests::should_move_the_card_one_step_per_arrow_from_the_move_handle_and_clamp_it_inside_the_host | 초점 받은 손잡이에 실제 화살표는 헤디드 몫 |
| AUX-029 | 나 | 창 갤러리 = 실창 사진 + 판정표가 본질 — 창 금지 | |
| AUX-030 | 가 | ShellHostEntryPointContractTests::should_find_no_entry_point_to_the_old_about_setup_view · ::should_find_no_entry_point_to_the_old_account_manager_panel_view · ::should_find_no_entry_point_to_the_old_sound_setting_view · ::should_find_no_entry_point_to_the_old_open_vca_panel_handler | 호스트 저장소 판독(F5) |
| AUX-031 | 가 | ShellHostEntryPointContractTests::should_open_the_events_console_on_the_suppression_rail_from_the_old_suppression_row_and_keep_no_old_window | |
| AUX-032 | 나 | LayoutSweep = 호스트 UiTests LayoutSweepTests(FlaUI 실앱) — 창 없이 도는 판정기가 저장소에 없음 | |
| AUX-033 | 나 | 참조 시험은 호스트 ConsoleLiveApiTests(CheckLayout — 실앱 UIA rect · 캡처 밝기 · ✕ 실제 닫힘) — 창 없는 시험이 아니라 표지만도 불가 | |
| AUX-034 | 나 | LayoutSweep 이 빠뜨린 표면 두 테마 = 실앱 LayoutAudit(r27) 몫 — Application + 앱 사전 필요, 팝업 · 오프스크린 창 금지 | |
| PERF-016 | 나 | 실 NATS 브로커 재시작(전용 스택)이 필요하고, 제품에 끊김/재연결 신호 · 재조회 보충 경로가 없어 증명할 기능이 없음(F2) | 기능 신설은 범위 밖 |
