# PLAN — t-20261011-081052-b135 이벤트 · 계정 · 보고서 헤드리스 검증 남은 일 마무리

## 목표
앞 작업 t-20261011-073820-4f76(첨부 evt-plan-4f76.md)이 남긴 일을 끝낸다.
1. 보고만 한 결함 3건 빨강→초록: ACC-026(세션 자동 새로 고침 뒤 선택 풀림) · 가입 창 아이디(5~20자)/성명(2~10자) 길이 판정 · ACC-031(AccountSetupPanelView 입력 칸 AutomationId).
2. EVM "범위 밖" 28 · "헤디드 남김"(EVM 8 · ACC 5 · RPT 7)을 하나씩 다시 보고 라이브러리에서 창 없이 확인할 수 있는 부분은 시험을 쓴다. 남는 것은 이유 한 줄.
3. 카탈로그 §2 EVM · §3.3 ACC · §3.4 RPT 의 시험 없는 줄을 전부 덮는다.

규칙: 창 금지(Window.Show · 화면 밖 창) · 지도 DB · 운영 서버 · 실제 NATS 금지 · 새 시험마다 `[Trait("Scenario", "<id>")]` · 기존 시험 본문 무변경(표지 한 줄만) · 새 .cs UTF-8 BOM · 공개 형식 모양은 더하기만.

## 진행 상황
- [x] 빌드 준비(Events.Ui · Accounts.Ui.Tests · tests/Accounts.Ui.ViewTests · Reports.Ui) — 4개 모두 빌드 성공(오류 0)
- [x] 결함 1: ACC-026 (빨강→초록, 아래 「결함 — 고친 것」)
- [x] 결함 2: 가입 아이디 · 성명 길이 (빨강→초록)
- [x] 결함 3: ACC-031 AutomationId (빨강→초록)
- [x] EVM · RPT 재검토는 병렬 에이전트 2개에 맡김(결과 문서 `evm-results.md` · `rpt-results.md`). ACC 는 직접.
- [x] ACC 헤디드 5 재검토 — ACC-009 · 016 · 032 새 시험(부분 포함), ACC-034 · 044 는 남김(이유 아래)
- [x] RPT 헤디드 7 · RPT-025 재검토(에이전트) — `rpt-results.md`, Reports.Ui 359/359
- [x] EVM 범위 밖 28 · 헤디드 8 재검토(에이전트) → `evm-results.md` 합침(아래 EVM 표)
- [x] Events.Ui 의 RPT-025 시험(ReportPhraseTrayScenarioTests) 실행 — 3/3 통과
- [x] 최종 회귀 실행 · 결과표(아래 「최종 실행」)

## 결정
- tests/Accounts.Ui.ViewTests 는 **모든 시험 클래스가 `AppHost.Show`(화면 밖 창 -20000,-20000)를 쓴다** — 목표의 "창 금지(화면 밖 창 금지)" 규칙이 우선이라 이 작업에서는 빌드(컴파일 확인)만 하고 실행하지 않는다. 사용자가 PC 를 비울 때 `dotnet test tests/Accounts.Ui.ViewTests/Accounts.Ui.ViewTests.csproj` 로 돌린다. (이 프로젝트는 sln 멤버가 아니라 하네스 검사에도 들어가지 않는다.)
- 가입 창 「서버가 더 엄격하면 그 메시지는 그대로 보인다」는 **서버 거부 경로를 바꾸지 않는다**로 읽었다 — 클라이언트 판정이 통과시킨 값을 서버가 거부하면 기존 실패 안내(「계정 등록이 실패하였습니다.」)가 그대로 나오고 창이 닫히지 않음을 시험으로 고정했다. 게이트웨이 계약(IUserDirectoryGateway, 실패 = null)은 서버 원문을 넘기지 않으므로 그 모양은 바꾸지 않았다(공개 형식 무변경 규칙).
- 가입 창 길이 판정은 `CanClickOk`(= [확인] 단추 활성)에만 넣었다. `ClickOk` 자체에는 가드를 넣지 않았다 — 기존 시험(AccountLeftoverWriteTests 등)이 ClickOk 를 직접 불러 4자 아이디로 저장 경로를 시험하고, 목표가 "[확인] 판정" 을 지목했기 때문.
- ACC-026 시험에서 실제 DataGrid 의 `SelectedItem` TwoWay 동작(목록이 비면 null 을 밀어 넣음)은 시험 안 `GridLikeSelection` 으로 흉내 냈다(창 금지). 흉내 없이도 수정 전에는 옛 인스턴스를 붙든 채 상세가 낡은 값을 보이는 결함이 드러난다(두 번째 시험).
- Accounts.Ui.Tests 프로세스는 `Application.Current == null` 을 전제로 한다(ConsoleHarness — DispatcherService.Invoke 가 제자리에서 돈다). 그래서 Application 이 필요한 툴바 템플릿 시험(ACC-009)은 이미 WpfApplicationCollection 이 있는 **Utils.Tests**(sln 멤버)에 두었다.
- ACC-032 · RPT-019/028 의 Esc · F1 은 HWND 없는 가짜 `PresentationSource`(HeadlessSource)로 창 없이 확인했다(RPT 에이전트가 찾은 방법을 ACC 에도 적용).
- 공용 의존 프로젝트 동시 빌드 충돌을 피하려고 처음에 4개 시험 프로젝트를 한 번 빌드한 뒤로는 `--no-dependencies` 로 각자 빌드했다.
- 새 .cs 앞의 UTF-8 BOM 은 임시 파일 `bom.bin`(3바이트)을 `cat bom.bin 파일 > 새파일` 로 붙였다(PowerShell 인코딩 개체 생성이 거부됨). 다 쓴 뒤 지웠다. 새 .cs 9개 모두 `ef bb bf` 로 시작.

## 결함 — 고친 것(빨강→초록)
수정 전 실행: `.harness/t-20261011-081052-b135/trx/acc-red.trx` — 24 중 16 실패. 수정 뒤: `trx/acc-green.trx` — 24/24 통과.

1. **ACC-026 세션 자동 새로 고침 뒤 선택 풀림**
   - 수정 전 실패 한 줄: `should_keep_the_same_session_selected_after_the_auto_refresh_replaces_the_rows` — `Assert.NotNull() Failure: Value is null`; `should_show_the_refreshed_session_values_in_the_detail_after_the_auto_refresh` — `Expected: "2026-10-11T10:00:00+09:00" Actual: "2026-10-11T09:00:00+09:00"`(옛 인스턴스를 붙들어 상세가 낡은 값).
   - 원인: `UserSessionPanelViewModel.ReloadAsync` 가 `Items.Clear()` 후 새 인스턴스로 채움 · `UserSessionDto` 같음 비교 없음 · 콘솔은 세션 Id 를 기억하지 않음(사용자 목록만 `_selectedIds` 로 되찾음).
   - 수정(`AccountConsolePanelViewModel.cs`): `_selectedSessionId` 를 두고 `SelectedSession` 세터가 Id 를 기억(목록이 빈 동안 들어오는 null 은 해제로 읽지 않음) · `Items.CollectionChanged` → `OnSessionRowsChanged` → `ReconcileSessions`(UI 스레드, 같은 Id 의 새 행으로 선택을 맞춤, 없으면 선택만 비우고 Id 는 유지 — 사용자 목록 규칙과 같음) · 레일 전환 때 Id 를 지움.
   - 수정 뒤: 4/4 통과(새로 고침 뒤 같은 세션 · 새 값 표시 · 사라진 세션은 선택 해제 · 레일을 떠났다 오면 되살리지 않음).
2. **ACC-033 가입 창 아이디 · 성명 길이**
   - 수정 전 실패 한 줄: `아이디 4자 · 성명 2자에 가입 [확인] 이 켜졌다 — 안내는 아이디 5~20자 · 성명 2~10자`(21자 아이디 · 1자 성명 · 11자 성명도 같은 실패, 8사례 중 4 실패).
   - 수정(`RegisterDialogViewModel.CanClickOk`): `Username.Length` 5~20 · `Name.Length` 2~10 (뷰 `StringLengthValidationRule` 과 같은 계산 — 단순 Length).
   - 수정 뒤: 8/8 + 서버 거부 시험 1 통과.
3. **ACC-031 세션 정책 칸 자동화 id**
   - 수정 전 실패 한 줄: `TimeoutHours 칸의 자동화 id 가 '(없음)' — Console.Accounts.SessionSetup.TimeoutHours 이어야 한다`(8칸 + 새로고침 단추 + 중복 검사 = 10 실패).
   - 수정(`AccountSetupPanelView.xaml`, x:Name 무변경): `Console.Accounts.SessionSetup.` + `TimeoutHours` · `RefreshDays` · `LockoutDurationMinutes` · `SessionPolicyEnabled` · `ConcurrencyPolicy` · `MaxConcurrentSessions` · `SessionSelfReplaceEnabled` · `SessionHistoryRetentionDays` · `Reload`(ClickReload 단추).
   - 수정 뒤: 11/11 통과(자동화 id 13개 · 중복 없음 · ClickReload/ClickSave x:Name 유지).

## ACC 헤디드 5 — 다시 본 결과
| id | 새 상태 | 시험 / 남긴 이유 |
|---|---|---|
| ACC-009 | 통과(새, 부분) | Utils.Tests/AccountToolbarOverflowScenarioTests 3개 — 넓으면 [⋯] 없음 · 좁히면 [열 n/m] 이 [⋯](`Console.Accounts.Toolbar.More`) 뒤로 접히고 접힌 단추가 제자리와 같은 `ColumnsClick` 을 냄 · 계정 뷰는 `ConsoleKey="Accounts"` · `ColumnsClick="OnColumns"` 처리기 하나. 실제 폭 · 팝업 위치 · 메뉴 그림은 헤디드 |
| ACC-016 | 통과(새, 부분) | Accounts.Ui.Tests/Scenarios/AccountHeadedLeftoverScenarioTests — 칩에 끌어 놓기(CanDrop → Drop) = 칩 클릭(Draft 목록 · 상태 글 같음) · 칩 아닌 곳에 놓으면 아무것도 안 담음. 실마우스 캡처 끌기 · Esc 취소(CaptureDragBehavior — 마우스 캡처 필요)는 헤디드 |
| ACC-032 | 통과(새, 부분) | 같은 파일 4개 — 계정 뷰의 "?" 키 5개(콘솔 · 권한 · 부여 · 정책 ×2) 모두 설명 등록 · 차례로 열면 하나만 열림 · Esc 는 열린 것만 닫고 먹음(닫혀 있으면 통과) · F1 = 가장 가까운 범위의 "?"(정책 칸 → 정책, 절 밖 → 콘솔). 말풍선 그림 · 실제 뷰 트리의 범위 배선 · 실제 키 입력은 헤디드 |
| ACC-034 | 헤디드 남김 | 기대가 「Win32 파일창」 자체 — `ClickAddPicture` 가 `OpenFileDialog.ShowDialog()` 를 직접 연다(GAP-N). 고른 뒤 경로(`SetPictureAsync`)는 기존 AccountWriteRoundTripTests 가 이미 본다 |
| ACC-044 | 헤디드 남김 | 「두 테마 레이아웃 위반 없음」(LayoutAudit)은 실앱 렌더의 UIA 사각형 판정 — 창 없이 같은 것을 단언할 수 없다(RPT-020 과 같은 판단) |

## RPT — 다시 본 결과(에이전트, 상세는 rpt-results.md)
| id | 새 상태 | 시험 / 남긴 이유 |
|---|---|---|
| RPT-009 | 통과(새, 부분) | RLS.should_ask_for_the_large_window_with_the_html_and_say_the_preview_is_shown_there_until_it_closes. 별도 최상위 창 자체는 뷰 코드비하인드가 만든다 [WIN] |
| RPT-010 | 헤디드 남김 | 큰 창 줌은 `ReportLargePreviewWindow.xaml.cs` 코드비하인드가 WebView2.ZoomFactor 를 직접 바꿈 — 뷰모델 · 순수 함수 없음 |
| RPT-017 | 통과(새, 부분) | RLS 2개(마우스 놓기 = Alt+↑/↓ · 바깥/제자리 놓기는 그대로). 실마우스 · Esc 는 헤디드 |
| RPT-019 | 통과(새, 부분) | RLS 3개(키 등록 · 한 번에 하나 + Esc · F1 가장 가까운 "?"). 말풍선 그림 · 실제 키 · 실제 뷰 트리 배선은 헤디드 |
| RPT-020 | 헤디드 남김 | 정적 토큰 계약 시험(표지 없음)만 — 레이아웃 위반 없음은 실렌더 |
| RPT-024 | 통과(새, 부분) | RLS 2개(마우스 놓기 = Alt+화살표 = ▲▼ · 바깥/제자리는 그대로) |
| RPT-025 | 통과(새) | Events.Ui/Tests/ReportPhraseTrayScenarioTests 3개(등록 → 템플릿 변경 알림 → 트레이 끝에 새 문구 · '기타' 마지막 / 콘솔이 닫혀 있다 다시 열 때 / 트레이 문구 칸 바인딩) — 3/3. SYNC_ACTION_REPORT_TEMPLATE → 메시지 변환 라우터는 호스트 |
| RPT-028 | 통과(새, 부분) | RLS 2개(문구 창 머리 "?" 등록 · 한 번에 하나 + Esc) |
RLS = `Reports.Ui/Tests/ReportLeftoverScenarioTests`. Reports.Ui 전체 359/359(앞 341 + 새 18). 결함 없음.

## EVM — 다시 본 36개(에이전트, 상세는 evm-results.md)
약호: ELS = `Events.Ui/Tests/EventLeftoverScenarioTests` · ELH = `tests/GMaps.Housing.Tests/EventLeftoverHousingScenarioTests` · EPC = `CameraPopup.Tests/Unit/EventWindowPopupCoexistenceTests` · NUS = `Nats/Tests/NatsUnitSubjectScenarioTests` · DCS = `Devices.Ui/Tests/DeviceConsoleSyncSelectionTests`.

| id | 새 상태 | 시험 메서드 / 남긴 이유 |
|---|---|---|
| EVM-003 | 통과(새) | ELS.should_write_group_dash_type_and_number_on_the_card_and_drop_the_dash_without_group_evm003_018 |
| EVM-010 | 통과(새, 부분) | ELS.should_raise_update_action_with_two_cards_listed_when_two_detections_arrive_evm010 — 배지의 원천(UpdateAction 순간 카드 2장)까지. 배지 글자(ShellViewModel.TotalEvents)는 호스트 |
| EVM-018 | 통과(새, 부분) | ELS.…_evm003_018(그룹 없는 센서 → 구분자 없음). 대상 구역선이 없어 「구역선 변화 없음」은 따로 단언 안 함 |
| EVM-022 | 범위 밖 | 같은 센서 ×20 — 「탐지/장애는 조치보고 전 같은 대상 중복 송신 없음」 규칙 위반 입력 |
| EVM-023 | 헤디드 남김 | 편집 모드 토글 → GMap 지도 컨트롤 SetEditMode 마커 재구성 — 지도 컨트롤 · 실창 필요 |
| EVM-026 | 통과(새) | ELS.should_not_ask_the_map_to_move_when_events_arrive_and_only_when_the_card_is_double_clicked_evm026 |
| EVM-027 | 통과(새, 부분) | ELS.should_keep_listing_cards_by_the_panel_timer_alone_while_the_view_is_not_looked_at_evm027_028 — 뷰모델 수준. 실제 셸 최소화는 헤디드 |
| EVM-028 | 통과(새, 부분) | 같은 시험. 실제 모달 열림은 헤디드 |
| EVM-029 | 통과(새, 부분) | ELS.should_show_every_card_that_arrived_before_the_panel_was_opened_once_it_is_opened_evm029. 실제 패널 접기/펴기는 헤디드 |
| EVM-030 | 통과(새, 부분) | EPC.should_never_close_the_double_click_camera_popup_while_event_windows_open_evict_and_close_evm030 — 관리자 수준. 실창 팝업은 헤디드 |
| EVM-031 | 범위 밖 | Redis EVENT_CALL 송신 코드가 라이브러리 탐지 경로에 없음(grep) — 호스트 |
| EVM-035 | 통과(새, 부분) | ELS.should_split_array_envelope_take_only_detect_item_and_leave_no_card_once_its_report_is_routed_evm035. AR 항목을 패널로 옮기는 라우터는 호스트 |
| EVM-037 | 범위 밖 | 같은 장비 반복 버스트 — 규칙 위반 입력 |
| EVM-040 | 통과(새, 부분) | ELS.should_keep_the_typed_memo_and_choice_of_an_open_report_dialog_when_a_burst_arrives_and_send_that_memo_evm040. 실창 키 입력 지연은 헤디드 |
| EVM-044 | 범위 밖 | 옛 cmd AI_DETECTION 정규화는 호스트 NatsBrokerService 뿐(grep) |
| EVM-048 | 통과(새, 부분) | ELH.should_swing_3d_gate_leaves_on_contact_without_touching_the_status_color_evm048 — 최종 자세 · 색 축 불변. 중간 프레임 픽셀은 헤디드 |
| EVM-063 | 통과(새) | ELS.should_list_controller_fault_card_with_korean_reason_controller_number_and_section_one_to_three_evm063 |
| EVM-068 | 범위 밖 | 구현 없음(GAP-N) — 1차 구간을 지도에 그리는 코드가 없어 단언할 대상 없음 |
| EVM-069 | 범위 밖 | 구현 없음(GAP-N) — 2차 구간 |
| EVM-080 | 범위 밖 | 라이브러리에 CONNECTION 수신 서비스 없음 — 호스트 ProcessConnection |
| EVM-081 | 통과(새) | ELS.should_never_produce_the_cyan_connection_state_on_symbol_or_zone_evm081 |
| EVM-092 | 통과(새) | ELS.should_keep_card_symbol_and_alarm_and_write_nothing_when_report_dialog_is_cancelled_evm092 |
| EVM-097 | 통과(새, 부분) | NUS.should_subscribe_only_own_unit_and_global_so_other_unit_action_report_never_arrives_evm097(6) — 구독 subject 계산. 실제 배달 거름은 NATS 서버 |
| EVM-098 | 통과(새) | ELS.should_close_the_map_card_and_restore_the_symbol_when_the_console_tray_is_applied_evm098 |
| EVM-099 | 통과(새) | ELS.should_close_the_map_card_when_a_console_row_is_reported_through_its_report_dialog_evm099 |
| EVM-102 | 통과(새) | ELS.should_close_every_card_restore_every_symbol_and_write_one_action_each_when_report_all_is_confirmed_evm102 |
| EVM-108 | 통과(새, 부분) | ELH.should_swing_3d_housing_door_open_in_04s_and_back_when_operation_event_opens_and_closes_evm108 — 최종 자세. 프레임 픽셀은 헤디드 |
| EVM-112 | 통과(새, 부분) | ELH.should_show_fault_then_degraded_badge_and_plate_and_clear_them_when_ptz_component_status_changes_evm112_113 — 지도 배지 · 이름표. 띠 카드 · 상세 부품 탭은 범위 밖(장비 콘솔) |
| EVM-113 | 통과(새) | 같은 시험(저하 → 정상에서 사라짐) |
| EVM-118 | 범위 밖 | SYNC_DEVICE CREATED 캐시 갱신은 호스트(라이브러리 코드 없음) |
| EVM-121 | 통과(기존+표지) | DCS.should_keep_the_unapplied_edit_and_say_so_when_the_selected_controller_is_updated_by_sync(표지 한 줄, 삭제 0줄) |
| EVM-122 | 범위 밖 | 깨진 SYNC_DEVICE body 처리는 호스트 라우터 |
| EVM-127 | 범위 밖 | 조치 뒤 SYNC_DETECTION 발행 = 서버 몫 |
| EVM-128 | 범위 밖 | 조치 삭제 · action_reported 재계산 = 서버 몫 |
| EVM-129 | 범위 밖 | 억제 = 서버 REST 게이트(GIS 코드 없음 — 클라이언트 게이트 없음은 EVM-130 이 확인) |
| EVM-137 | 헤디드 남김 | 카메라 팝업 호스트 프로세스 죽임 · 멈춤 = 외부 프로세스(Survival 시험은 프로세스 · 창을 띄움) |

EVM 쪽 시험 수정(제품 무변경): ELS 1차 12/15(시험 IoC 에 DeviceProvider 없음 — 덧댐) → 15/15, ELH 1차 1/4(창 없는 시계에서 스토리보드 판정 → SeekAlignedToLastTick) · 2차 3/4(함체 기대값 오기) → 4/4. 제품 결함 없음.

## 발견
- RPT 쪽 관찰(결함 단정 아님): 큰 창 줌 하한 0.5 · 상세 칸 미리보기 줌 하한 0.30 — 사양 확인은 헤디드 RPT-010 회차에서.
- EVM 쪽 관찰(결함 단정 아님 · 보고만): `EventCardListPanelViewModel.OnDeactivateAsync` 는 카드 · 버퍼를 모두 지우지만 큐 엔트리는 남긴다. 호스트가 이 패널을 끄지 않아 지금 경로로는 닿지 않는다 — 끄는 경로가 생기면 카드 없이 경보 · 심볼만 남을 위험.
- EVM 에이전트가 Nats 새 시험 첫 실행 때 기존 `NatsServiceGlobalSubjectTests` 를 함께 돌렸는데, 그중 1개가 127.0.0.1:4222(로컬) 연결을 시도하는 기존 시험이었다 — 운영 서버 · 실제 NATS 접속은 아니며 이후 실행에서는 뺐다.

## 최종 실행(2026-10-11, 모든 수정 뒤 — TRX: `.harness/t-20261011-081052-b135/trx/final-*.trx`)
- `dotnet build Ironwall.Dotnet.Libraries.sln` — 오류 0.
- **Events.Ui**(창 띄우는 6클래스 SuppressionRowSelectBindingTests · SuppressionRecurrenceRadioTests · SuppressionDayChipAutomationTests · MappingDragInteractionTests · EventToolbarChipAutomationTests · EventConsoleSingleSelectionDetailTests 제외): **1970/1970 통과, 실패 0**(앞 1952 + EVM 15 + RPT-025 3).
- **Accounts.Ui.Tests**(창 띄우는 AccountConsoleCloseButtonTests 제외): **381/381 통과, 실패 0**(앞 351 + 새 30).
- **Reports.Ui** 전체: **359/359 통과, 실패 0**(앞 341 + 새 18, 창 띄우는 클래스 없음).
- **tests/Accounts.Ui.ViewTests**: 빌드 오류 0, **실행 안 함** — 모든 클래스가 화면 밖 창(AppHost.Show)을 띄워 「창 금지」 규칙과 충돌(결정 참조). 사용자가 PC 를 비울 때 `dotnet test tests/Accounts.Ui.ViewTests/Accounts.Ui.ViewTests.csproj`.
- 다른 프로젝트에 더한 새 시험: Utils.Tests AccountToolbarOverflowScenarioTests 3/3 · Housing EventLeftoverHousingScenarioTests 4/4 · CameraPopup EventWindowPopupCoexistenceTests 1/1 · Nats NatsUnitSubjectScenarioTests 6/6 · Devices.Ui DeviceConsoleSyncSelectionTests 8/8.
- Utils.Tests 창 없는 클래스 617/617(XAML 계약 시험 영향 없음).
- 하네스 검사 `dotnet test Ironwall.Dotnet.Libraries.sln` 은 CameraPopup Survival · Utils.Tests 화면 밖 창 시험 등 창을 띄우는 시험을 포함해 이 작업에서 직접 돌리지 않았다 — 하네스가 턴 끝에 돌린다.

## 집계(이번 반영 후)
- **EVM 140**: 통과 126(이번 22 = 새 시험 21(그중 부분 12) + 기존+표지 1) · 범위 밖 12 · 헤디드 남김 2 · 결함 0. (앞: 통과 104 · 헤디드 8 · 범위 밖 28)
- **ACC 44**: 통과 37(이번 부분 3: 009 · 016 · 032) · 결함→수정 5(033 · 038 · 041 앞 작업 + 026 · 031 이번, 033 은 이번에 길이 판정 추가 수정) · 헤디드 남김 2(034 · 044) · 결함 보고만 0.
- **RPT 28**: 통과 26(이번 6: 009 · 017 · 019 · 024 · 028 부분 + 025) · 헤디드 남김 2(010 · 020).
- 끝까지 남는 6줄(이유는 위 표): EVM-023 · EVM-137 · ACC-034 · ACC-044 · RPT-010 · RPT-020. 범위 밖 EVM 12줄은 호스트 · 서버 몫이거나 구현 없음 · 규칙 위반 입력.

## 남은 일
- 헤디드(사용자가 PC 를 비울 때): 위 6줄 + 「부분」 항목의 실창 몫, tests/Accounts.Ui.ViewTests 전체.
- 이번 작업으로 남은 할 일은 없다.
