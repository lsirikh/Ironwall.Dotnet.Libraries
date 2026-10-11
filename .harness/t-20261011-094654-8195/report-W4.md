# W4(AUX) 보고 — 본 세션이 작업자 보고를 옮겨 적음

## 파일(tests/GMaps.MapAux.Tests 새 파일 · BOM 확인 · 기존 시험 · 제품 코드 · Scenario.Shared 무변경)
- AuxHarness.cs — AuxHeadlessSource(창 없는 입력 원천) · NoPopupHelp("?" 말풍선 몸을 제자리에) · MenuTrap(우클릭 메뉴를 열지 않고 통째로 잡음 — 메뉴 첫 항목 때 기록 · IsOpen → popup.Child 순간 스타일 재조회에서 표지 예외로 창 생성 전 차단, IL 순서 확인 · 자가 확인 · 끝나고 HwndSource 0 단언) · MapVmRig
- MarkerMenuOpsScenarioTests.cs — MAP-153 · 154 · 157 · 168
- SymbolDetailScenarioTests.cs — MAP-160 · 161 · 162 · 163 · 165 · 166
- DrawingHudScenarioTests.cs — MAP-177 · 178 · 179
- OverlayHelpPlaybackScenarioTests.cs — MAP-085 · 173 · 191 · AUX-012 · 017 · 018 · 021

## 결론(20개 모두 가)
| id | 결론 | 시험 | 결과 |
|---|---|---|---|
| MAP-085 | 가 | OverlayHelpPlaybackScenarioTests::should_open_tracking_settings_from_playback_gear_and_close_it_with_close_button_when_playback_panel_used(Trait "MAP-085,AUX-012") · ::should_close_tracking_settings_overlay_when_escape_pressed_inside_it [AUX-D6] | 통과(D6 본 세션 수정 뒤) |
| MAP-153 | 가 | MarkerMenuOpsScenarioTests::should_list_detail_components_unit_device_pages_and_history_in_order_when_sensor_symbol_right_clicked_in_operation_mode · ::should_add_home_page_aim_and_sound_items_by_type_and_open_their_panels_when_controller_camera_speaker_right_clicked · ::should_keep_device_page_items_visible_but_disabled_and_hide_menu_for_locked_symbol_when_web_server_off | 통과 |
| MAP-154 | 가 | ::should_omit_detail_and_unit_line_and_append_order_items_when_symbol_right_clicked_in_edit_mode | 통과 |
| MAP-157 | 가 | ::should_hand_device_page_and_home_page_urls_to_browser_opener_when_external_menu_items_clicked — URL 은 실제 BuildUrl, 여는 자리(OpenInChrome)만 가짜 | 통과 |
| MAP-160 | 가 | SymbolDetailScenarioTests::should_open_one_detail_overlay_and_close_it_with_close_button_then_reuse_it_when_detail_shown_again · ::should_close_detail_overlay_when_escape_pressed_inside_it [AUX-D5] | 통과(D5 본 세션 수정 뒤) |
| MAP-161 | 가 | ::should_show_basic_components_symbol_tabs_with_ids_and_switch_fields_when_tab_buttons_pressed · ::should_disable_device_tabs_with_reason_and_start_on_symbol_tab_when_symbol_has_no_linked_device(기록: 2026-09-09 결정으로 모든 종류가 기본 · [부품] · 심볼 탭 — SymbolDetailRules.cs:124-135, 결함 아님) | 통과 |
| MAP-162 | 가 | ::should_auto_rotate_in_45_degree_steps_pause_with_toggle_reset_to_front_and_resume_after_drag_when_3d_stage_used(끌기는 눌림 상태만 주입, 픽셀은 헤디드) | 통과 |
| MAP-163 | 가 | ::should_show_type_specific_actions_with_enable_rules_and_reason_help_text_when_detail_opened_for_sensor_speaker_gate · ::should_disable_every_device_action_except_page_and_show_on_map_when_symbol_unlinked | 통과 |
| MAP-165 | 가 | ::should_request_unit_map_and_show_block_reason_until_launcher_reports_shown_when_open_unit_map_pressed · ::should_disable_open_unit_map_for_unknown_unit_and_hide_line_when_unit_directory_unavailable | 통과 |
| MAP-166 | 가 | ::should_close_only_the_detail_callout_and_keep_detail_overlay_when_escape_pressed_with_question_mark_open | 통과 |
| MAP-168 | 가 | MarkerMenuOpsScenarioTests::should_open_sensor_info_overlay_and_request_group_history_when_pidsgroup_menu_items_clicked · ::should_disable_group_items_with_reason_tooltip_when_group_unlinked_or_has_no_sensors(×2) | 통과 |
| MAP-173 | 가 | OverlayHelpPlaybackScenarioTests::should_close_measure_callout_first_without_cancelling_measure_then_cancel_measure_with_second_escape(첫 Esc 말풍선만 · 측정 유지, 둘째 Esc 측정 취소) | 통과 |
| MAP-177 | 가 | DrawingHudScenarioTests::should_insert_pids_group_row_and_add_zone_line_marker_when_palette_pids_group_button_points_then_hud_complete | 통과 |
| MAP-178 | 가 | ::should_remove_last_point_with_undo_button_and_cancel_without_db_with_close_button_on_drawing_hud · ::should_undo_with_backspace_and_ctrl_z_complete_with_enter_and_cancel_with_escape_when_drawing_keys_pressed | 통과 |
| MAP-179 | 가 | ::should_keep_hud_screen_size_and_let_clicks_right_of_hud_reach_map_when_digital_zoom_and_tilt_change | 통과 |
| MAP-191 | 가 | OverlayHelpPlaybackScenarioTests::should_keep_layer_panel_and_palette_header_inside_map_when_dragged_past_window_edges · ::should_keep_every_overlay_header_inside_map_when_dragged_past_top_left_edge [AUX-D7] | 통과(D7 본 세션 수정 뒤) |
| AUX-012 | 가 | MAP-085 첫 시험과 공유 | 통과 |
| AUX-017 | 가 | ::should_move_playback_marker_along_recorded_points_with_speed_and_stop_on_pause_when_playback_runs(가짜 추적 기록 · 가짜 시계로 실제 재생 엔진) | 통과 |
| AUX-018 | 가 | ::should_open_one_tracking_or_playback_callout_at_a_time_and_close_it_with_escape_keeping_panels_open | 통과 |
| AUX-021 | 가 | ::should_send_play_request_with_typed_group_and_repeat_and_close_panel_without_notice_when_speaker_accepts · ::should_keep_panel_and_show_speaker_reason_then_allow_resend_when_play_request_rejected_or_unanswered | 통과 |

## 결함(작업자 보고)
- AUX-D5(MAP-160) 상세 겹창이 Esc 로 안 닫힘 — SymbolDetailControl 키 처리 없음, 지도 Esc 경로(OnMapPreviewKeyDownForAim)는 모드만 끔.
- AUX-D6(MAP-085) 추적 설정 겹창이 Esc 로 안 닫힘 — TrackingSettingsControl 키 처리 없음.
- AUX-D7(MAP-191) 겹창 머리 끌기 클램프 없음 — 상세 · 센서 정보 · 추적 설정 · 재생 · 사이트 고정이 Left/Top=-2900 으로 나감(레이어 패널 LAY-D3 · 팔레트만 자름). 같은 패턴: BroadcastPlayControl · TtsBroadcastControl · MapRegistrationControl · GMapPropertyBaseControl.

## 실행(작업자 시점)
- `실패! - 실패: 3, 통과: 73, 전체: 76 - GMaps.MapAux.Tests.dll` (실패 3 = D5 · D6 · D7), 새 시험 31건 3회 동일
