# PLAN — t-20261011-073820-4f76 헤드리스 시나리오 확인(EVM · ACC · RPT)

## 목표
카탈로그 `gis-map-event-scenario-catalog.md`(r27) 중
- §2 이벤트 → 지도 · 팝업 · 심볼 반영(EVM-001~140) 의 라이브러리 쪽 남은 항목
- §3.3 계정 · 계정 대화상자 · 내 정보(ACC-001~044)
- §3.4 보고서 · 조치보고 문구(RPT-001~028)
를 창 없이(Window.Show 금지) 시험으로 확인하고, 시험마다 `[Trait("Scenario", "…")]` 표지를 단다. 끝에 항목 id 별 결과표.

## 진행 상황
- [x] 이전 진행분 확인 — 이 브랜치(harness/t-20261011-073820-4f76)에 이 작업의 커밋 · PLAN.md 가 **없었다**(HEAD = d8a4d5e7, v2.6 accept 커밋). 처음부터 시작.
- [x] 카탈로그 §2 · §3.3 · §3.4 읽기, 증거 도구 `tools/scenario-evidence/collect.py`(v2.6 606e26bc — 이 브랜치엔 없음) 표지 규칙 확인
- [x] EVM: 새 시험 `Events.Ui/Tests/EventPopupScenarioTests.cs`(26 → 33 사례) — 이벤트 창 자동 열림 요청 · 카드 · SYNC_DETECTION · 억제 · 썸네일 주소 · 장애 카드/한국어 사유
- [x] EVM: 기존 시험에 표지만 추가(본문 무변경 — `git diff --numstat` 삭제 0줄 확인): EventMapScenarioTests · EventMarkerPaintScenarioTests(Housing) · EventWindowTriggerTests · EventCardLifecycleDefectTests · EventQueueDefectTests · MappingRuntimePresetDefectTests · DetectionSyncNatsServiceTests · DetectionServerDeletionTests · EventSuppressionSyncNatsServiceTests · LiveContractFixTests · MalfunctionCardDisplayTests · DoorStateWiringTests · CameraPopup.Tests EventWindowManagerTests · CameraPopup.Host.Tests EventWindowViewModelTests
- [x] EVM 실행 증거(TRX): `docs/tests/evidence/20261011-evm-headless/`(docs/ 는 git 밖) — `--filter "Scenario~EVM"`: Events.Ui 108/108 · CameraPopup.Tests 9/9 · CameraPopup.Host.Tests 6/6 · GMaps.Housing.Tests 16/16 통과
- [x] Events.Ui 회귀(창 띄우는 6클래스 제외): 1952/1952 통과 — `docs/tests/evidence/20261011-evm-regression/`
- [x] ACC · RPT(병렬 에이전트): 새 시험 67개(계정 56 — `Accounts.Ui.Tests/Scenarios/` 4파일, 보고서 11 — `Reports.Ui/Tests/ReportScenarioTests.cs`) + 기존 시험 표지(삭제 0줄 확인). 결함 3건 빨강→초록 수정(ACC-041 · ACC-033 · ACC-038)
- [x] ACC · RPT 실행 증거(TRX): `docs/tests/evidence/t-20261011-073820-4f76-acc-rpt/` — 계정 새 시험 56/56 · 보고서 새 시험 11/11 · 계정 전체(창 띄우는 AccountConsoleCloseButtonTests 제외) 351/351 · 보고서 전체 341/341
- [x] 결과표 완성(아래). sln 검사(`dotnet test Ironwall.Dotnet.Libraries.sln`)는 CameraPopup.Tests Survival(호스트 프로세스 기동) 등을 포함해 창이 뜰 수 있어 이 작업에서 직접 돌리지 않았다 — 하네스가 턴 끝에 돌린다.

## 결정
- 시험 위치: 이벤트 = `Ironwall.Dotnet.Libraries.Events.Ui/Tests`(이 프로젝트가 시험 프로젝트를 겸함), 계정 = `Ironwall.Dotnet.Libraries.Accounts.Ui.Tests`, 보고서 = `Ironwall.Dotnet.Libraries.Reports.Ui/Tests`.
- **주의**: 하네스 검사 `dotnet test Ironwall.Dotnet.Libraries.sln` 에는 Events.Ui · Accounts.Ui.Tests · GMaps.Housing.Tests 가 **들어 있지 않다**(sln 멤버 아님) — 그래서 이 프로젝트들은 직접 `dotnet test <csproj> --filter "Scenario~EVM" --logger trx` 로 돌려 증거를 남겼다. sln 에 들어 있는 것은 Reports.Ui · CameraPopup.Tests · CameraPopup.Host.Tests.
- 창 띄우는 기존 시험은 회귀 실행에서 뺐다: Events.Ui 의 SuppressionRowSelectBindingTests · SuppressionRecurrenceRadioTests · SuppressionDayChipAutomationTests · MappingDragInteractionTests · EventToolbarChipAutomationTests · EventConsoleSingleSelectionDetailTests(`.Show()` 사용), tests/Accounts.Ui.ViewTests 전체(AppHost.Show).
- 표지 형식: xUnit `[Trait("Scenario", "EVM-013")]`(여러 개는 쉼표). collect.py 정규식이 메서드 선언 줄에서 `public … 이름(` 를 찾으므로 표지는 메서드 바로 위 속성 줄에 둔다. 표지는 "기대" 칸을 실제로 단언하는 시험에만 — 부분만 보는 것은 결과표에 그 범위를 적었다.
- 이벤트 창 전 구간 시험은 실제 `EventWindowTrigger` 를 호스트 배선(EventUiModule.WireEventWindows :417-418 — 큐 OnEntryEnqueued · 카드 패널 ActionReported)과 같게 잇고, 창 관리자만 기록용 가짜로 둔다(창 없음).

## 발견
- 이벤트(EVM) 쪽 라이브러리 경로에서 새 결함은 나오지 않았다 — 새 시험 33사례 모두 첫 실행 통과.
- EVM-010(셸 배지 TotalEvents) · EVM-080(CONNECTION device.status) · EVM-097(다른 부대) · EVM-118 · EVM-122(SYNC_DEVICE CREATED · 깨진 body) · EVM-044(옛 AI_DETECTION 정규화)는 **호스트 코드**(Dotnet.Monitoring.Solution NatsBrokerService) 몫이라 라이브러리엔 해당 코드가 없다 → 범위 밖.

### 결함 — 고친 것(빨강→초록)
- **ACC-041** 비밀번호 바꾸기: 수정 전 `ResetPasswordLengthScenarioTests.should_disable_ok_when_new_password_is_21_characters` 실패 — "21자 새 비밀번호에 [확인] 이 켜졌다 — 안내는 8~20자"(4건 중 1 실패). 원인: `ResetPassDialogViewModel.CanClickOk` 가 하한(>=8)만 봄, 뷰는 「8자 이상 20자 이하」 · StringLengthValidationRule Max=20. 수정: `&& NewPassword!.Length <= 20` → 4/4 통과.
- **ACC-033** 가입 대화상자(같은 종류 새로 찾음): 수정 전 `AccountDialogScenarioTests.should_disable_register_ok_when_the_password_is_21_characters` 실패 — "21자 비밀번호에 가입 [확인] 이 켜졌다 — 안내는 8~20자". 수정: `RegisterDialogViewModel.CanClickOk` 에 `RegisterPass.Length <= 20` → 통과.
- **ACC-038** 내 정보 이름 없는 칸: 수정 전 `AccountDialogScenarioTests.should_give_each_editable_my_page_field_an_automation_id` 4건 실패 — "ViewModel.Phone 칸에 자동화 id 가 없다". 수정: `MyPagePanelView.xaml` 전화 · 이메일 · 직급 · 부서 TextBox 에 AutomationId(`Accounts.MyPage.PhoneInput` · `EmailInput` · `PositionInput` · `DepartmentInput`) 추가(x:Name 무변경) → 통과.

### 결함 — 보고만
- **ACC-026(의심 — 헤디드 확인 필요)**: 세션 자동 새로 고침 뒤 선택이 풀릴 것으로 보임 — `UserSessionPanelViewModel.cs:145-146` 이 목록을 비우고 새 객체로 다시 채움, `UserSessionDto` 에 같음 비교가 없음, `AccountConsolePanelView.xaml:929` 선택 양방향 바인딩, 사용자 목록처럼 Id 로 선택을 되찾는 처리가 세션 쪽엔 없음. 선택 복원 순서를 다뤄야 해 작지 않아 손대지 않음.
- 가입 창 아이디(안내 5~20자) · 성명(검증 2~10자) 길이를 [확인] 판정이 보지 않음 — 서버 규칙이 불확실해 고치지 않음.
- ACC-031: `AccountSetupPanelView.xaml` 에서 LockoutThreshold 외 입력 칸에 자동화 id 없음(시험 가능성 결함).

## 남은 일
- 헤디드 남김 항목(아래 표)은 사용자가 PC 를 비워 줄 때 호스트 UiTests 회차로.
- ACC-026 은 헤디드로 실제 확인 뒤 수정 여부 결정.

## 결과표 — §3.3 ACC · §3.4 RPT
약호: ACS=Accounts.Ui.Tests/Scenarios/AccountConsoleScenarioTests · ADS=AccountDialogScenarioTests · APS=AccountPanelScenarioTests · RPS=ResetPasswordLengthScenarioTests · RSC=Reports.Ui/Tests/ReportScenarioTests · RCT=Reports.Ui/Tests/ReportConsoleTests · CFP=CompletenessFixPassTests.

| id | 상태 | 시험 메서드 / 사유 |
|---|---|---|
| ACC-001 | 통과(새) | ACS.should_open_on_the_users_rail_under_the_accounts_title_when_the_console_is_opened |
| ACC-002 | 통과(기존+표지) | AccountConsoleCloseGuardTests.should_close_the_account_console_without_asking_when_nothing_is_pending; …should_ask_instead_of_closing_when_the_user_form_has_unapplied_changes |
| ACC-003 | 통과(새 · 기존+표지) | ACS.should_switch_the_content_to_each_of_the_six_rails_when_each_rail_is_picked; AccountConsoleRailTests.should_list_six_rails_in_the_design_order_when_an_admin_opens_the_console |
| ACC-004 | 통과(기존+표지) | AccountConsoleRailTests.should_block_the_rail_switch_when_there_are_unapplied_changes; PermissionMatrixReviewTests.should_block_the_rail_switch_when_the_matrix_has_unapplied_changes |
| ACC-005 | 통과(새 · 기존+표지) | ACS.should_count_users_and_locked_users_on_the_rail_badge_and_footer; AccountConsoleRailTests.should_hide_count_badges_on_audit_and_session_setup |
| ACC-006 | 통과(새) | ACS.should_show_only_the_toolbar_buttons_that_mean_something_on_each_rail |
| ACC-007 | 통과(새) | ACS.should_say_no_user_matches_the_search_when_the_search_text_matches_nobody |
| ACC-008 | 통과(새 · 기존+표지, 열 상태) | APS.should_return_to_the_default_six_columns_when_an_added_column_is_turned_off_again; AccountColumnsTests 2개. 실제 열 메뉴 · prefs 파일은 헤디드 |
| ACC-009 | 헤디드 남김 | 툴바 넘침(⋯) — 실제 폭 레이아웃 |
| ACC-010 | 통과(새) | ACS.should_fill_the_detail_with_fields_sessions_and_lock_state_when_a_row_is_picked |
| ACC-011 | 통과(기존+표지) | AccountDetailFormTests 4개(여러 값 표시 · 한 칸 일괄 적용 · 필수 거부 · 되돌리기) |
| ACC-012 | 통과(기존+표지) | AccountConsoleCompletionTests.should_count_the_visible_rows_when_a_search_filter_is_on |
| ACC-013 | 통과(새) | ACS.should_unlock_through_the_confirmation_and_ask_before_ending_sessions_when_detail_actions_are_used |
| ACC-014 | 통과(새) | ACS.should_keep_the_row_when_delete_is_cancelled_and_remove_it_when_delete_is_confirmed |
| ACC-015 | 통과(기존+표지) | UserGroupDropTests 3개(적용 · 버림 · 되돌리기) |
| ACC-016 | 헤디드 남김 | 실제 마우스 끌기 [COORD] |
| ACC-017 | 통과(새) | ACS.should_say_nothing_is_selected_in_the_detail_when_a_rail_has_no_selection; ACS.should_ask_to_pick_a_group_in_the_detail_when_the_permission_rail_has_no_selection |
| ACC-018 | 통과(새) | ACS.should_point_at_the_new_group_button_when_there_is_no_permission_group |
| ACC-019 | 통과(새) | APS.should_create_the_group_and_show_it_as_a_row_when_a_new_group_is_saved; APS.should_refuse_a_duplicate_group_name_in_plain_korean_and_keep_the_form_open |
| ACC-020 | 통과(기존+표지) | PermissionMatrixConsoleTests 3개; AccountCompletenessFixTests.should_turn_on_every_applicable_cell_of_the_row_when_the_row_button_is_pressed; PermissionMatrixReviewTests.should_refuse_every_cell_when_the_account_may_not_edit |
| ACC-021 | 통과(새 · 기존+표지) | ACS.should_report_draft_count_enabled_modules_and_member_count_when_a_cell_is_edited |
| ACC-022 | 통과(새 · 기존+표지) | ACS.should_show_the_empty_members_state_when_the_group_has_no_member |
| ACC-023 | 통과(기존+표지) | UserSessionPanelCleanupTests.should_default_to_active_only_when_activated |
| ACC-024 | 통과(새) | ACS.should_open_the_session_detail_with_its_fields_when_a_session_is_picked; ACS.should_show_the_logout_reason_in_korean_when_the_session_detail_is_drawn |
| ACC-025 | 통과(새 · 기존+표지) | ACS.should_warn_that_my_own_session_is_included_when_force_logout_all_targets_my_account; UserSessionPanelCleanupTests.should_call_delete_user_sessions_when_force_logout_all |
| ACC-026 | 결함 보고만 | 위 「결함 — 보고만」 |
| ACC-027 | 통과(새) | APS.should_list_the_new_grant_with_names_and_revoke_it_after_confirmation |
| ACC-028 | 통과(새, 끝<시작) | APS.should_refuse_a_grant_that_ends_before_it_starts. 날짜 칸 실제 조작은 헤디드 |
| ACC-029 | 통과(기존+표지) | AuditLogPanelTests 2개; AccountCompletenessFixTests.should_describe_the_selected_audit_record_in_korean_when_the_detail_is_shown |
| ACC-030 | 통과(기존+표지) | AccountLeftoverWriteTests 범위 검사 2개; AccountVisualReviewFixTests.should_restore_the_loaded_values_when_revert_is_pressed_on_the_session_policy |
| ACC-031 | 통과(기존+표지, 변경 수) · id 없음은 결함 보고만 | AccountCompletenessFixTests.should_enable_save_only_when_a_policy_value_differs_from_the_loaded_one |
| ACC-032 | 헤디드 남김 | "?" 말풍선 · F1 · Esc |
| ACC-033 | 결함→수정 | ADS.should_disable_register_ok_when_the_password_is_21_characters 외 가입 시험 5개; AccountLeftoverWriteTests.should_register_as_user_when_the_server_mode_local_list_is_empty |
| ACC-034 | 헤디드 남김 | Win32 파일창(GAP-N) |
| ACC-035 | 통과(기존+표지) | AccountLeftoverWriteTests 2개; AccountDialogOutcomeTests.should_show_success_and_close_when_the_server_resets_the_password |
| ACC-036 | 통과(기존+표지, 가짜 서버 거부) | AccountDialogOutcomeTests.should_not_claim_success_when_the_server_fails_to_reset_the_password |
| ACC-037 | 통과(새) | ADS.should_fill_my_fields_and_keep_the_password_box_disabled_when_my_page_opens |
| ACC-038 | 결함→수정 | ADS.should_give_each_editable_my_page_field_an_automation_id(4); ADS.should_send_the_edited_contact_fields_when_my_page_is_applied |
| ACC-039 | 통과(새 · 기존+표지) | ADS.should_confirm_then_report_success_when_my_page_apply_is_pressed; ADS.should_bring_back_the_saved_values_when_my_page_reset_is_confirmed |
| ACC-040 | 통과(새) | ADS 의 틀린 현재 비밀번호 · 성공 · 취소 3개 |
| ACC-041 | 결함→수정 | RPS 4개(21 · 20 · 8 · 7자) |
| ACC-042 | 통과(새 · 기존+표지) | ADS.should_confirm_then_clear_the_photo_when_my_photo_is_deleted; 서버 모드 삭제 꺼짐 기존 시험. 사진 추가(Win32)는 헤디드 |
| ACC-043 | 통과(새, 닫기 명령) | ADS.should_ask_the_host_to_close_the_panel_when_my_page_close_is_pressed. 실제 Esc · ✕ 키는 헤디드 |
| ACC-044 | 헤디드 남김 | 두 테마 레이아웃 감사(실렌더) |
| RPT-001 | 통과(기존+표지) | RCT.should_show_three_rails_in_the_mockup_order_when_opened; CFP.should_refuse_to_close_while_a_template_has_unapplied_changes |
| RPT-002 | 통과(기존+표지) | RCT.should_swap_the_columns_when_the_rail_changes |
| RPT-003 | 통과(새 · 기존+표지) | RSC.should_name_the_pressed_status_chip_in_the_filter_summary; CFP 3개 |
| RPT-004 | 통과(기존+표지) | RCT.should_send_no_status_parameter_when_the_pressed_chip_is_pressed_again |
| RPT-005 | 통과(기존+표지) | RCT.should_filter_rows_without_calling_the_server_when_searching; RCT.should_say_the_search_only_covers_the_newest_page_when_nothing_matches |
| RPT-006 | 통과(기존+표지) | RCT.should_load_the_preview_into_the_detail_pane_when_a_report_is_selected; ReportPreviewSurfaceTests.should_name_the_reason_when_the_report_cannot_be_previewed_yet. WebView2 실제 그림은 헤디드 |
| RPT-007 | 통과(새) | RSC.should_show_meta_and_progress_for_a_running_report_and_the_reason_for_a_failed_one |
| RPT-008 | 통과(기존+표지) | RCT.should_follow_the_pane_width_until_the_user_zooms_and_return_to_fit_when_asked |
| RPT-009 | 헤디드 남김 | 별도 최상위 창 [WIN] |
| RPT-010 | 헤디드 남김 | 큰 창 줌 — 코드비하인드가 WebView2 직접 조작 |
| RPT-011 | 통과(새, 게이트) | RSC.should_enable_downloads_only_for_a_completed_report(3). 저장창은 GAP-N |
| RPT-012 | 통과(기존+표지) | RCT.should_open_the_new_report_with_its_preview_in_the_history_rail_when_generation_completes_on_the_create_rail |
| RPT-013 | 통과(기존+표지) | RCT 4개(빈 제목 · 템플릿 미선택 · 끝<시작 · 비우기) |
| RPT-014 | 통과(새 · 기존+표지) | RSC.should_say_the_generation_was_cancelled_after_the_cancel_is_confirmed; 기존 삭제 시험 2개 |
| RPT-015 | 통과(기존+표지) | RCT.should_swap_the_columns_when_the_rail_changes; ReportListsTests.should_show_an_unset_default_period_as_not_specified |
| RPT-016 | 통과(기존+표지) | RCT 4개(바뀐 칸만 · 순서 한 번에 · 되돌리기 · 새로 만들기) |
| RPT-017 | 헤디드 남김 | 실제 마우스 끌기 [COORD] |
| RPT-018 | 통과(기존+표지) | RCT.should_show_an_empty_state_instead_of_a_blank_form_when_no_template_is_chosen; CFP.should_say_why_the_component_list_is_empty_when_the_catalog_fails |
| RPT-019 | 헤디드 남김 | "?" 말풍선 · F1 |
| RPT-020 | 헤디드 남김 | 두 테마 레이아웃 감사 |
| RPT-021 | 통과(새 · 기존+표지) | RSC.should_not_offer_a_search_box_on_the_phrase_console; 기존 순서 · 닫기 거부 시험 |
| RPT-022 | 통과(새 · 기존+표지) | RSC 3개(501자 · 고치기 · 삭제); 기존 빈 글 · 중복 · 등록 3개 |
| RPT-023 | 통과(기존+표지) | ActionReportTemplateConsoleViewModelTests 3개(순서 · 되돌리기 · 위/아래) |
| RPT-024 | 헤디드 남김 | 실제 마우스 끌기 [COORD] |
| RPT-025 | 범위 밖 | 이벤트 트레이 문구 반영 = 이벤트 콘솔 내부(사용자 범위 제외 「이벤트 · 장비 쪽 빼고」) |
| RPT-026 | 통과(새 · 기존+표지) | RSC.should_update_the_phrase_count_when_a_phrase_is_registered; 기존 빈 상태 · 수 시험 |
| RPT-027 | 통과(기존+표지) | ActionReportTemplateExternalChangeTests.should_not_read_again_and_show_the_notice_when_the_content_has_unapplied_edits |
| RPT-028 | 헤디드 남김 | "?" 말풍선 · Esc |

## 집계
- EVM 140: 통과 104 · 헤디드 남김 8 · 범위 밖 28 · 결함 0
- ACC 44: 통과 35 · 결함→수정 3 · 결함 보고만 1 · 헤디드 남김 5
- RPT 28: 통과 20 · 헤디드 남김 7 · 범위 밖 1

## 결과표 — §2 EVM (항목 id → 상태 · 시험 메서드)
약호: EPS=Events.Ui/Tests/EventPopupScenarioTests · EMS=Events.Ui/Tests/EventMapScenarioTests · EMP=tests/GMaps.Housing.Tests/EventMarkerPaintScenarioTests · EWT=Events.Ui/Tests/EventWindowTriggerTests · ECL=EventCardLifecycleDefectTests · EWM=CameraPopup.Tests/Unit/EventWindowManagerTests · EWV=CameraPopup.Host.Tests/EventWindowViewModelTests. "통과(새)"=이번에 만든 시험, "통과(기존+표지)"=기존 시험에 표지만 단 것. 모두 2026-10-11 실행 통과(TRX 위).

| id | 상태 | 시험 메서드 / 사유 |
|---|---|---|
| EVM-001 | 통과(새) | EPS.should_list_detection_card_named_by_kind_and_id_when_detect_arrives_after_api_record_evm001 |
| EVM-002 | 통과(새 · 기존+표지) | EPS.should_put_newest_card_on_top_when_two_detections_arrive_on_distinct_sensors_evm002; ECL.should_hold_cards_in_the_batch_buffer_until_flushed_and_put_the_newest_on_top |
| EVM-003 | 범위 밖 | 카드 글 "그룹 - 장비타입 번호" 는 기존 헤디드 CardList 시험이 있음 — 이 작업은 건너뜀 |
| EVM-004 | 통과(새, 주소 결정) | EPS.should_resolve_card_photo_against_app_api_host_when_detection_carries_thumbnail_evm004_043 — 상대 썸네일 → 앱 API 호스트 주소. 실제 사진 픽셀(자홍/노랑)은 헤디드 |
| EVM-005 | 통과(기존+표지) | EMS.should_paint_symbol_and_zone_detecting_and_count_one_when_detect_arrives_after_api_record_evm005_009; EMS.should_fetch_device_once_and_paint_when_v7_reference_misses_cache_evm005_n5 |
| EVM-006 | 통과(기존+표지) | EMP.should_paint_red_dot_with_pulse_on_detect_and_return_green_without_pulse_on_report_evm006_084_085 |
| EVM-007 | 통과(기존+표지) | EMP.should_blink_red_line_on_detect_and_collapse_on_report_evm007_008_086 |
| EVM-008 | 통과(기존+표지) | EMP.should_blink_red_line_on_detect_and_collapse_on_report_evm007_008_086 |
| EVM-009 | 통과(기존+표지) | EMS.…_evm005_009; EMP.should_light_traffic_lamps_from_queue_counts_evm009_057_087 |
| EVM-010 | 범위 밖 | 셸 배지 TotalEvents 는 호스트 셸 코드 — 라이브러리에 없음 |
| EVM-011 | 통과(기존+표지, 소리 요청까지) | EMS.…_evm005_009(탐지 소리 재생 요청 1회). 실제 소리는 GAP-N |
| EVM-012 | 통과(새 · 기존+표지) | EPS.should_request_event_window_with_mapped_cameras_when_mapped_sensor_detects_evm012; EWT.should_open_window_with_mapped_cameras_when_detection_enqueued_in_self_mode |
| EVM-013 | 통과(새) | EPS.should_not_request_window_and_log_no_mapped_camera_when_group_has_no_mapping_evm013(창 없음 + 로그 「매핑 카메라 없음」) |
| EVM-014 | 통과(기존+표지) | EWV.should_goto_target_and_count_down_delay_when_window_starts |
| EVM-015 | 통과(기존+표지) | MappingRuntimePresetDefectTests.should_not_show_or_aim_a_camera_at_a_restricted_preset_when_a_detection_arrives |
| EVM-016 | 통과(기존+표지) | EMS.should_skip_queue_and_symbols_when_detected_device_was_deleted_on_server_evm016(지도 변화 없음); LiveContractFixTests.should_show_server_snapshot_when_device_was_deleted(「삭제된 장비」 글) |
| EVM-017 | 통과(기존+표지) | EMS.should_queue_as_none_and_paint_nothing_when_sensor_type_cannot_be_resolved_evm017 |
| EVM-018 | 범위 밖 | 기존 헤디드 CardList 시험 있음 — 건너뜀 |
| EVM-019 | 통과(기존+표지) | EMS.should_paint_both_zone_lines_when_sensor_in_two_groups_detects_evm019 |
| EVM-020 | 통과(새) | EPS.should_queue_and_paint_without_crash_when_detection_result_varies_evm020(6변형) |
| EVM-021 | 통과(새 · 기존+표지) | EMS.should_paint_symbol_and_zone_detecting_when_alert_or_contact_sensor_detects_evm021_051; EPS.…_evm012(Alert 창) |
| EVM-022 | 범위 밖 | 같은 센서 ×20 — 운영 전제(조치 전 같은 대상 중복 송신 없음) 밖 |
| EVM-023 | 범위 밖 | 편집 모드 재구성 — 기존 헤디드 MapSymbol 시험 있음 |
| EVM-024 | 통과(기존+표지) | EMP.…_evm006_084_085(다크 사례) |
| EVM-025 | 통과(기존+표지, 라이브러리 쪽) | ECL.should_select_the_card_and_ask_the_map_to_locate_its_device_when_double_clicked(카드 → MapLocateRequest). 지도 이동 · 고리는 기존 헤디드 |
| EVM-026 | 범위 밖 | GAP-N 구현 없음(제품 결정) |
| EVM-027 | 헤디드 남김 | 셸 최소화 = 실창 상태 |
| EVM-028 | 헤디드 남김 | 모달 열림 = 실창 |
| EVM-029 | 헤디드 남김 | 패널 닫힘/다시 열기 = 실창(기존 헤디드 있음) |
| EVM-030 | 헤디드 남김 | 카메라 팝업 실창 |
| EVM-031 | 범위 밖 | Redis EVENT_CALL — 호스트 · Redis |
| EVM-032 | 통과(기존+표지) | EMS.should_queue_once_when_same_envelope_is_published_three_times_evm032 |
| EVM-033 | 통과(기존+표지) | EMS.should_queue_once_when_same_event_is_resent_with_new_envelope_id_evm033 |
| EVM-034 | 통과(새 · 기존+표지) | EPS.should_not_open_window_or_fetch_mapping_when_action_report_arrived_before_detection_evm034; EMS.should_end_normal_without_alarm_when_action_report_arrives_before_detection_evm034; EWT.should_not_open_window_when_action_report_arrives_while_mapping_is_loading |
| EVM-035 | 범위 밖 | [DETECT, AR] 배열의 AR 은 호스트 라우터가 패널로 옮김 — 기존 헤디드 DeepR25 |
| EVM-036 | 통과(기존+표지) | EMS.should_not_repaint_when_same_detection_envelope_is_redelivered_after_report_evm036 |
| EVM-037 | 범위 밖 | 같은 장비 반복 버스트 — 운영 전제 밖 |
| EVM-038 | 통과(기존+표지) | ECL.should_drop_the_oldest_cards_when_the_display_cap_is_exceeded(500장 상한) |
| EVM-039 | 통과(기존+표지) | EWM.should_evict_oldest_unpinned_window_when_eleventh_event_opens_with_max_ten |
| EVM-040 | 헤디드 남김 | 조치 창 입력 지연 = 실창 키보드 |
| EVM-041 | 통과(기존+표지) | EMS.should_paint_camera_detecting_and_release_on_report_when_ai_detection_arrives_evm041_042 |
| EVM-042 | 통과(기존+표지) | 같은 시험 |
| EVM-043 | 통과(새) | EPS.…_evm004_043(다른 호스트 절대 URL → 앱 API 호스트로 다시 붙임) |
| EVM-044 | 범위 밖 | 옛 cmd AI_DETECTION 정규화는 호스트(NatsBrokerService :309). 라이브러리는 이중 적재를 막는 것만(EMS.should_ignore_legacy_ai_detection_cmd_in_library_because_host_normalizes_it_evm044, 표지 없음) |
| EVM-045 | 통과(기존+표지) | EMS.should_skip_queue_when_ai_camera_was_deleted_evm045 |
| EVM-046 | 통과(새) | EPS.should_request_event_window_when_ai_detection_arrives_for_camera_in_mapped_group_evm046 |
| EVM-047 | 통과(기존+표지) | EMS.should_move_door_without_card_queue_or_color_when_enclosure_contact_arrives_evm047 |
| EVM-048 | 헤디드 남김 | 3D 문짝 움직임 픽셀(기존 FenceGate 헤디드) |
| EVM-049 | 통과(기존+표지) | EMS.should_invert_door_when_open_on_contact_on_is_off_evm049 |
| EVM-050 | 통과(기존+표지) | EMS.should_leave_door_untouched_when_door_contact_fallback_is_off_evm050 |
| EVM-051 | 통과(기존+표지 · 새) | EMS.…_evm021_051; EPS.…_evm012(ContactOn 창) |
| EVM-052 | 통과(새) | EPS.should_list_malfunction_card_apart_from_detection_card_when_both_arrive_after_api_record_evm052_073 |
| EVM-053 | 통과(새) | EPS.should_show_korean_fault_type_for_each_of_five_malfunction_reasons_evm053(5사유) |
| EVM-054 | 통과(기존+표지) | EMS.should_paint_fault_and_count_fault_and_play_fault_sound_when_malfunction_arrives_evm054_055_057_058; EMP.should_paint_steady_orange_dot_without_blink_on_malfunction_evm054 |
| EVM-055 | 통과(기존+표지) | EMS.…_evm054_055_057_058; EMP.should_paint_steady_orange_line_on_malfunction_evm055 |
| EVM-056 | 통과(기존+표지) | EMS.should_show_fault_color_and_error_axis_together_when_server_marks_device_error_after_fault_evm056; EMP.should_blink_icon_orange_when_server_marks_device_error_and_stop_on_activated_evm056_114 |
| EVM-057 | 통과(기존+표지) | EMS.…_evm054_055_057_058; EMP.…_evm009_057_087 |
| EVM-058 | 통과(기존+표지, 소리 요청까지) | EMS.…_evm054_055_057_058 |
| EVM-059 | 통과(새 · 기존+표지) | EPS.should_request_malfunction_window_only_when_event_window_on_malfunction_is_on_evm059_060(true); EWT.should_open_malfunction_window_only_when_malfunction_option_is_on |
| EVM-060 | 통과(새 · 기존+표지) | 같은 두 시험(false 사례) |
| EVM-061 | 통과(기존+표지) | EMS.should_skip_queue_when_faulted_device_was_deleted_evm061; MalfunctionCardDisplayTests.should_show_deleted_device_label_when_card_device_is_null |
| EVM-062 | 통과(기존+표지) | EMS.should_paint_again_when_new_malfunction_arrives_after_report_evm062 |
| EVM-063 | 범위 밖 | 제어기 장애 카드 — 기존 헤디드 시험 있음 |
| EVM-064 | 통과(기존+표지) | EMS.should_black_out_controller_and_member_zones_but_keep_member_sensor_color_when_controller_faults_evm064_065_066 |
| EVM-065 | 통과(기존+표지) | 같은 시험; EMP.should_paint_black_line_for_member_zone_when_controller_blacks_out_evm065 |
| EVM-066 | 통과(기존+표지) | 같은 시험; EMP.should_paint_black_dot_on_controller_when_controller_faults_evm066 |
| EVM-067 | 통과(기존+표지) | EMS.should_show_first_and_second_section_as_start_tilde_end_in_malfunction_report_dialog_evm067 |
| EVM-068 | 범위 밖 | GAP-N 구현 없음(지도에 1차 구간 그리지 않음) |
| EVM-069 | 범위 밖 | GAP-N 구현 없음 |
| EVM-070 | 통과(기존+표지) | EMS.should_auto_recover_blackout_when_controller_becomes_activated_evm070 |
| EVM-071 | 통과(기존+표지) | EMS.should_turn_zone_from_black_to_red_when_member_sensor_detects_during_blackout_evm071 |
| EVM-072 | 통과(기존+표지) | EMS.should_not_expand_groups_when_lonely_controller_faults_evm072 |
| EVM-073 | 통과(새) | EPS.…_evm052_073 |
| EVM-074 | 통과(기존+표지) | EMS.should_step_down_to_fault_when_detection_of_faulted_detecting_sensor_is_reported_evm074_075; EMP.should_show_orange_dot_without_pulse_when_sensor_both_detects_and_faults_evm074_075 |
| EVM-075 | 통과(기존+표지) | 같은 두 시험 |
| EVM-076 | 통과(기존+표지) | EMS.should_keep_detecting_when_fault_is_reported_first_evm076 |
| EVM-077 | 통과(기존+표지) | EMS.should_auto_recover_fault_and_show_detecting_when_same_sensor_detects_after_fault_evm077 |
| EVM-078 | 통과(기존+표지) | EMP.should_alternate_red_and_orange_line_when_zone_has_detection_and_fault_evm078 |
| EVM-079 | 통과(기존+표지) | EMS.should_change_nothing_on_map_when_connection_envelope_arrives_evm079_081_082 |
| EVM-080 | 범위 밖 | CONNECTION + device.status 는 호스트 ProcessConnection |
| EVM-081 | 범위 밖 | GAP-N 코드가 청록 상태를 만들지 않음 |
| EVM-082 | 통과(기존+표지) | EMS.…_evm079_081_082(신호등 건수 그대로) |
| EVM-083 | 통과(기존+표지) | EMS.should_close_only_detection_when_detection_and_malfunction_share_number_evm083 |
| EVM-084 | 통과(기존+표지) | EMP.…_evm006_084_085 |
| EVM-085 | 통과(기존+표지) | EMP.…_evm006_084_085 |
| EVM-086 | 통과(기존+표지) | EMP.…_evm007_008_086 |
| EVM-087 | 통과(기존+표지) | EMP.…_evm009_057_087 |
| EVM-088 | 통과(새 · 기존+표지) | EPS.should_send_close_for_same_kind_window_when_remote_action_report_arrives_evm088; EWM.should_close_with_action_reported_and_return_home_when_report_arrives; EWV.should_return_home_only_auto_moved_tiles_when_shutdown_with_return_home |
| EVM-089 | 통과(기존+표지) | EWM.should_keep_window_when_report_is_for_other_kind_pinned_or_close_on_report_off |
| EVM-090 | 통과(기존+표지) | 같은 시험(CloseOnActionReport=false) |
| EVM-091 | 통과(새 · 기존+표지) | EPS.should_close_window_card_and_symbol_together_when_local_report_dialog_ok_evm091; EMS.should_restore_symbol_zone_counts_and_stop_sound_when_local_report_ok_evm091_095 |
| EVM-092 | 범위 밖 | 조치 창 [취소] — 기존 헤디드 ActionReport 시험 있음, 라이브러리 새 시험 미작성 |
| EVM-093 | 통과(기존+표지) | EMS.should_restore_symbol_when_local_malfunction_report_ok_evm093; EMP.…_evm056_114(ACTIVATED 뒤 깜빡임 멈춤) |
| EVM-094 | 통과(기존+표지) | EMS.should_restore_symbol_and_close_card_when_auto_report_timer_fires_evm094 |
| EVM-095 | 통과(새 · 기존+표지) | EPS.should_keep_alarm_until_last_of_two_detections_is_reported_evm095; EventQueueDefectTests.should_stop_the_alarm_when_the_last_active_event_is_reported; EMS.…_evm091_095 |
| EVM-096 | 통과(기존+표지) | EMS.should_leave_own_symbols_when_unknown_event_is_reported_elsewhere_evm096 |
| EVM-097 | 범위 밖 | 다른 부대 subject 거름은 호스트 라우터 |
| EVM-098 | 범위 밖 | 콘솔 트레이 → 지도 카드 — 기존 헤디드 시험 있음 |
| EVM-099 | 범위 밖 | 콘솔 행 → 지도 카드 — 기존 헤디드 시험 있음 |
| EVM-100 | 통과(기존+표지) | ECL.should_disable_ok_and_explain_when_another_operator_reported_the_open_dialogs_event |
| EVM-101 | 통과(기존+표지) | EMS.should_clear_every_symbol_when_burst_on_distinct_sensors_is_reported_one_by_one_evm101 |
| EVM-102 | 범위 밖 | 전체 조치(격리 회차) — 기존 헤디드 시험 있음 |
| EVM-103 | 통과(기존+표지) | EMS.should_do_nothing_when_action_report_has_no_from_event_evm103 |
| EVM-104 | 통과(기존+표지) | EMS.should_follow_sync_device_status_on_operation_axis_without_touching_event_axis_evm104_114_115; EMP.should_keep_red_pulse_over_error_and_deactivated_axes_when_detecting_evm104 |
| EVM-105 | 통과(기존+표지) | EWV.should_restart_countdown_when_user_interacts(타이머 닫힘); EWV.should_return_home_only_auto_moved_tiles_when_shutdown_with_return_home(홈) |
| EVM-106 | 통과(기존+표지) | EMS.should_set_door_state_without_color_or_count_when_operation_event_arrives_evm106_107_109 |
| EVM-107 | 통과(기존+표지) | 같은 시험 |
| EVM-108 | 헤디드 남김 | 3D 하우징 문짝 0.4초 애니메이션 픽셀 |
| EVM-109 | 통과(기존+표지) | 같은 시험(카드 · 신호등 영향 없음) |
| EVM-110 | 통과(기존+표지) | EMS.should_keep_door_when_operation_event_is_threshold_reason_evm110 |
| EVM-111 | 통과(기존+표지) | EMS.should_skip_operation_event_for_deleted_device_and_keep_processing_evm111 |
| EVM-112 | 범위 밖 | 부품 고장 이름표 — 기존 헤디드(MapComponentDisplayR22) |
| EVM-113 | 범위 밖 | 부품 저하 → 정상 — 기존 헤디드(MapComponentR24) |
| EVM-114 | 통과(기존+표지) | EMP.…_evm056_114 |
| EVM-115 | 통과(기존+표지) | EMP.should_gray_dot_and_dim_icon_when_sync_device_deactivated_evm115 |
| EVM-116 | 통과(기존+표지) | EMS.should_recompute_from_queue_when_activated_arrives_with_stale_blackout_evm116 |
| EVM-117 | 통과(기존+표지) | EMS.should_keep_card_reportable_and_lower_counts_and_normalize_symbol_when_device_deleted_evm117 |
| EVM-118 | 범위 밖 | SYNC_DEVICE CREATED 캐시 갱신은 호스트 |
| EVM-119 | 통과(기존+표지, 라이브러리 쪽) | DoorStateWiringTests.should_set_door_state_directly_and_notify_only_on_change(SetDoorState → 심볼 문 상태). 호스트 라우팅은 범위 밖 |
| EVM-120 | 통과(기존+표지) | EMS.should_paint_new_zone_when_cached_device_moves_to_another_group_evm120 |
| EVM-121 | 범위 밖 | 장비 상세 편집 유지 — 장비 콘솔(범위 밖 도메인), 기존 헤디드 |
| EVM-122 | 범위 밖 | 깨진 SYNC_DEVICE body — 호스트 라우터 |
| EVM-123 | 통과(새 · 기존+표지) | EPS.should_replace_card_photo_when_server_patches_thumbnail_and_sync_detection_updated_arrives_evm123; DetectionSyncNatsServiceTests.should_fetch_and_publish_when_updated_and_entry_active |
| EVM-124 | 통과(새 · 기존+표지) | EPS.should_not_revive_card_or_fetch_when_sync_detection_arrives_for_reported_event_evm124; DetectionSyncNatsServiceTests.should_skip_get_when_event_not_in_queue |
| EVM-125 | 통과(새 · 기존+표지) | EPS.should_close_card_lower_lamp_restore_symbol_and_close_window_when_sync_detection_deleted_arrives_evm125; EMS.should_close_card_restore_symbol_and_signal_window_when_detection_deleted_on_server_evm125; DetectionServerDeletionTests.should_close_card_release_queue_and_notify_when_detection_deleted_on_server |
| EVM-126 | 통과(기존+표지) | DetectionServerDeletionTests.should_disable_ok_with_deletion_notice_even_while_sending |
| EVM-127 | 범위 밖 | 서버의 SYNC 발행 포착 — 서버 몫(기존 헤디드) |
| EVM-128 | 범위 밖 | 서버 조치 삭제 · action_reported — 서버 몫(기존 헤디드) |
| EVM-129 | 범위 밖 | 억제는 서버 REST 게이트(202, 발행 없음) — GIS 쪽 코드 없음. 클라이언트 게이트가 없음은 EVM-130 이 확인 |
| EVM-130 | 통과(기존+표지) | EMS.should_still_paint_when_suppressed_device_event_bypasses_rest_gate_evm130 |
| EVM-131 | 통과(새 · 기존+표지) | EPS.should_poll_suppression_panel_three_times_and_leave_map_untouched_when_suppression_created_updated_deleted_evm131; EventSuppressionSyncNatsServiceTests.should_request_poll_when_own_command_arrives |
| EVM-132 | 통과(기존+표지) | EWT.should_order_by_priority_take_per_window_and_count_extra_when_more_cameras_are_mapped; EWM.should_fill_window_policy_from_settings_when_opening; EWV.should_fill_one_empty_cell_when_five_cameras_on_3x2; EWV.should_show_only_six_and_plus_n_when_more_cameras_mapped |
| EVM-133 | 통과(기존+표지) | EWM.should_evict_oldest_unpinned_window_when_eleventh_event_opens_with_max_ten; EWM.should_cascade_right_down_by_step_and_wrap_to_first_position_when_reaching_work_area_edge |
| EVM-134 | 통과(기존+표지, 배치 계산) | EWM.should_place_on_target_monitor_work_area_with_its_dpi_when_monitor_exists; EWM.should_clamp_to_primary_and_notify_once_when_target_monitor_is_missing. 실제 WS_EX_TOPMOST · 여러 모니터 실창은 헤디드 |
| EVM-135 | 통과(기존+표지) | EWT.should_not_open_or_fetch_when_mode_is_broker_or_none; EWM.should_not_open_window_when_mode_is_not_self |
| EVM-136 | 통과(기존+표지, 권한) | EWT.should_mark_ptz_not_allowed_when_user_lacks_camera_control; EWV.should_auto_move_even_without_user_permission_when_mapping_has_target. 더블클릭 재생 · 옮긴 곳 · 자동 닫힘 실창은 헤디드 |
| EVM-137 | 헤디드 남김 | 호스트 프로세스 죽임/멈춤 = 외부 프로세스 |
| EVM-138 | 통과(새 · 기존+표지) | EPS.should_use_new_camera_for_next_window_when_sync_event_mapping_changes_cameras_evm138; EWT.should_refetch_when_sync_event_mapping_invalidates_cache |
| EVM-139 | 통과(기존+표지, PTZ_STATUS) | EMS.should_rotate_fov_when_ptz_status_arrives_evm139. WINDY 강풍 표시는 호스트 ProcessWindyModeAsync — 범위 밖 |
| EVM-140 | 통과(기존+표지) | EMS.should_keep_map_unchanged_and_keep_processing_when_junk_messages_arrive_evm140 |
