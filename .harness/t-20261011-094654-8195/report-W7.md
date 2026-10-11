# W7(SHELL · 계정 · 보고서 · 기타) 보고 — 본 세션이 작업자 보고를 옮겨 적음

## 파일(모두 새 파일 · BOM 확인 · 제품 코드/기존 시험 무변경)
- Ironwall.Dotnet.Libraries.Accounts.Ui.Tests/Scenarios/ShellAccountsLeftoverScenarioTests.cs (10)
- Ironwall.Dotnet.Libraries.Reports.Ui/Tests/ShellReportsLeftoverScenarioTests.cs (6)
- Ironwall.Dotnet.Libraries.Utils.Tests/ShellKernelScenarioTests.cs (8) — 실제 커널 템플릿(Generic.xaml) · 부모 없는 Measure/Arrange · HWND 없는 가짜 PresentationSource · "?" 는 Popup 없는 몸 템플릿
- Ironwall.Dotnet.Libraries.Utils.Tests/ShellHostEntryPointContractTests.cs (6) — 라이브러리 + 형제 호스트 저장소 제품 소스 정적 판독(주석 제외), 호스트 저장소를 못 찾으면 실패

## 결론
| id | 결론 | 시험 또는 이유 | 결과 |
|---|---|---|---|
| ACC-034 | 가 | ShellAccountsLeftoverScenarioTests::should_hand_the_file_chosen_in_the_add_picture_window_straight_to_the_same_picture_path_and_do_nothing_when_cancelled · ::should_show_the_chosen_picture_as_a_copy_in_the_profile_folder_and_upload_that_copy_once_the_account_is_created · ::should_keep_the_previous_picture_and_say_why_when_the_chosen_file_cannot_be_a_photo(×2) — 파일창 seam 이 없어 창이 돌려준 이름이 가는 SetPictureAsync 를 직접 탐 | 통과 |
| ACC-044 | 나 | 판정기(LayoutAudit · LayoutSweep · CheckLayout)가 호스트 UiTests FlaUI 실앱(UIA rect + 캡처 밝기 테마 판별). 실제 계정 뷰를 창 없이 세우려면 Application + App.xaml 병합 필요, 오프스크린 창 금지 | — |
| RPT-010 | 나 | 줌 = ReportLargePreviewWindow(자체 HWND Window) 코드숨김 private Zoom → WebView2.ZoomFactor. Window 생성 금지 · WebView2 는 HWND/런타임 필요 | — |
| RPT-020 | 나 | ACC-044 와 같음. 토큰 계약은 기존 ReportLeftoverScenarioTests::should_draw_reports_views_with_theme_brushes_that_exist_in_both_themes_and_follow_a_theme_switch 가 보나 "레이아웃 위반 없음" 단언이 아니라 표지 안 담 | — |
| AUX-002 | 가 | ShellKernelScenarioTests::should_close_the_windy_card_through_the_same_cancel_path_from_the_close_button_the_header_x_and_escape · ShellHostEntryPointContractTests::should_wire_the_windy_header_x_and_escape_to_the_close_button_that_closes_the_panel | 통과 |
| AUX-003 | 가 | ShellKernelScenarioTests::should_close_only_the_windy_callout_on_the_first_escape_and_close_the_card_on_the_second | 통과 |
| AUX-006 | 나 | "먼저 묻기" 가드(호스트 PanelShellViewModel.ActivateItemAsync → PanelLeaveRule) · 행 숨김(호스트 LeftMenuSectionViewModel.cs:154 `CanSeeWindy => CanControl("broadcast")`) 모두 호스트에만 — 라이브러리 시험은 호스트 참조 불가 | — |
| AUX-023 | 가 | ShellAccountsLeftoverScenarioTests::should_follow_the_callout_rules_escape_outside_click_one_at_a_time_and_f1_on_the_account_question_marks · ShellReportsLeftoverScenarioTests::should_follow_the_callout_rules_escape_outside_click_one_at_a_time_and_f1_on_the_report_and_phrase_question_marks | 통과 |
| AUX-024 | 가 | ShellKernelScenarioTests::should_place_the_callout_body_inside_the_shell_wherever_the_question_mark_sits(×4 셸 크기 · 12px 격자 × 몸 높이 5) | 통과 |
| AUX-025 | 가 | ShellAccountsLeftoverScenarioTests::should_draw_the_whole_callout_text_with_its_body_id_and_close_it_with_escape_for_every_account_key(×5) · ShellReportsLeftoverScenarioTests::…_for_every_report_key(×5) | 통과 |
| AUX-026 | 가 | ShellKernelScenarioTests::should_open_the_console_header_question_mark_when_f1_is_pressed_where_no_section_owns_the_focus | 통과 |
| AUX-028 | 가 | ShellKernelScenarioTests::should_move_the_card_one_step_per_arrow_from_the_move_handle_and_clamp_it_inside_the_host — 한계: 가짜 PresentationSource 는 키보드 초점을 못 받아 "초점 받은 손잡이에 실제 화살표" 마지막 단계는 헤디드 몫(KeyboardMove 를 실제 카드/틀 크기로 · 배선은 소스 계약) | 통과 |
| AUX-029 | 나 | 창 갤러리 = 실창 사진 + 판정표가 본질 — 창 금지 | — |
| AUX-030 | 가 | ShellHostEntryPointContractTests::should_find_no_entry_point_to_the_old_about_setup_view · …_account_manager_panel_view · …_sound_setting_view · …_open_vca_panel_handler | 통과 |
| AUX-031 | 가 | ShellHostEntryPointContractTests::should_open_the_events_console_on_the_suppression_rail_from_the_old_suppression_row_and_keep_no_old_window | 통과 |
| AUX-032 | 나 | LayoutSweep = 호스트 UiTests LayoutSweepTests(FlaUI 실앱). 창 없이 도는 판정기가 저장소에 없음 | — |
| AUX-033 | 나 | 참조 L::should_have_no_layout_violations_in_any_console_when_all_consoles_were_checked 는 호스트 ConsoleLiveApiTests(CheckLayout = ConsoleLiveApiTests.Layout.cs:42) — 실앱 UIA rect · 캡처 밝기 · ✕ 실제 닫힘. 창 없는 시험이 아니라 표지만도 불가 | — |
| AUX-034 | 나 | LayoutSweep 이 빠뜨린 표면 두 테마 = 실앱 LayoutAudit(r27) 몫 — 실제 뷰 렌더에 Application + 앱 사전 필요, 팝업 · 오프스크린 창 금지 | — |

## 결함: 없음. 관찰(확인 필요)
1. 죽은 코드(진입점 0): 라이브러리 Events.Ui EventSuppressionSchedulePanelView/ViewModel(EventUiModule.cs:85 DI 등록만) · 호스트 AboutSetupView · AccountManagerPanelView · Sounds.Ui SoundSettingView(SoundUiModule.cs:35 등록만) · 호스트 LeftMenuSectionViewModel.cs:463 OpenVcaPanelViewModel — 정리 후보.
2. 호스트 낡은 시험 4개(범위 밖): SuppressionSweepTests.cs · PanelNavigationMatrixTests.cs:43 · AppSweepTests.cs:361(T-PANEL041) · Tests/Surfaces/SurfaceCatalogTests.cs — 옛 억제창 기준.
3. AUX-006 조건 불일치: 카탈로그 "비관리자 → WINDY 행 숨김" vs 실제 broadcast:control 권한 조건(LeftMenuSectionViewModel.cs:154).

## 실행
- Accounts.Ui.Tests(`FullyQualifiedName!~AccountConsoleCloseButtonTests` — 실창) 391/391
- Reports.Ui(필터 없음 — 창 시험 없음) 365/365
- Utils.Tests(창 · 화면 밖 창 · 팝업 17클래스 제외) 687/687
- 새 시험 3회: Accounts 10/10 · Reports 6/6 · Utils 14/14 ×3
