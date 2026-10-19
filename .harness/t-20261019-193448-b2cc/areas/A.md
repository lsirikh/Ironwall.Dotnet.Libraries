# A 3D 진입/이탈 시나리오 · 시험 결과

기준: COORD 커밋 0d732769(작업자 worktree 를 같은 내용으로 맞춘 뒤 작업 — 아래 "작업 환경" 참고).
새 시험 파일: `tests/GMaps.MapEdit.Tests/Map3DEntryExitMoreScenarioTests.cs`(47 건, 아래 표에서 `More::`).
기존 파일 약칭: `Entry::` = Map3DEntryScenarioTests · `Exit::` = Map3DExitInputScenarioTests · `Stale::` = Map3DStaleJobScenarioTests ·
`Cancel::` = Map3DCancelBakeScenarioTests · `BH::` = BuildingHeightScenarioTests · `Read::` = Map3DSettingsReadTests(이상 MapEdit) ·
`Ui.<클래스>::` = tests/GMaps.Ui.Tests 순수 함수 · `Nav::` = tests/GMaps.MapNav.Tests/Map3DRotatedAreaScenarioTests.

## 시나리오 표

| ID | 기능 | 시나리오 | 기대 | 시험 위치(file::method) | 상태 |
|---|---|---|---|---|---|
| A-01 | 자동(줌) | 영역 안 · 진입 줌 이상(같음 · +0.01 · +1) | 게이트 진입 → 펼침, 「나가기」 보임, 「3D 로 보기」 숨김 | Entry::should_enter_only_by_auto_zoom_and_show_button_only_inside_ready_area_when_gate_evaluated(256행) | 통과(기존) |
| A-02 | 자동(줌) | 영역 안 · 진입 줌 아래(−0.01 · −0.5) | 2D, 「3D 로 보기」 · "줌 N 이상" 칩 보임 | Entry::…gate_evaluated | 통과(기존) |
| A-03 | 수동(단추) | 어느 줌이든 줌으로 안 들어감 · 단추로만 · 나간 뒤 17.5~20 올려도 안 들어감 | 2D 유지, 단추 보임, 칩 안 보임 | Entry::…gate_evaluated · Entry::should_enter_by_button_exit_and_never_reenter_by_zoom_when_manual | 통과(기존) |
| A-04 | 공통 | 화면 가운데가 영역 밖 | 진입 없음 · 단추 없음 | Entry::…gate_evaluated(Outside) | 통과(기존) |
| A-05 | 공통 | 겹친 영역 | 목록 앞 영역으로 | Entry::…gate_evaluated(Overlap) · Stale::should_pick_newest_scene_first_when_bake_finishes_over_old_overlapping_area | 통과(기존) |
| A-06 | 공통 | 가운데가 영역 경계선 위 | 영역 안으로 봄 | Entry::…gate_evaluated(OnEdge) | 통과(기존) |
| A-07 | 공통 | 지도 줌 = 미리 준비 줌 ±0.01 | 줌으로 진입 안 함 | Entry::…gate_evaluated(delta 1.99 · 2 · 2.01) | 통과(기존) |
| A-08 | 설정 | 설정 「3D 지도」에서 끄고 저장(실제 SaveMap3DAsync → Map3DSaved → OnMap3DSettingsSaved → 파일 다시 읽기) — 3D 중 / 준비 중 | 3D 중 → 2D · 입력 살아 있음 · 단추 없음 / 준비 중 → 취소 · 안내 닫힘 · 돌아갈 자리 없음 | More::should_return_to_2d_with_live_input_and_cancel_pending_when_settings_saved_with_3d_off | 통과(새) |
| A-09 | 굽기 | 다 만든 뒤 안내 — 자동 / 수동 | "3D 준비됨 — 줌 17 이상에서 3D" / "3D 준비됨 — 지도 왼쪽 위 「3D 로 보기」로 들어갑니다" | More::should_tell_how_to_enter_by_entry_mode_when_bake_done(×2) | 통과(새) |
| A-10 | 설정 | EntryMode 읽기 — 옛 파일 · 빈 값 · 대소문자 · 이상값 · 왕복 | 수동 아니면 자동 | Read::* (4종) · Ui.Map3DEntryModeTests::should_read_auto_when_old_settings_lack_entry_mode 외 | 통과(기존) |
| A-11 | 설정 | 3D 중 방식 바꿈(자동↔수동) | 보던 3D 유지, 나간 뒤 새 규칙 | Entry::should_keep_3d_and_apply_new_rule_after_exit_when_mode_switched_while_shown | 통과(기존) |
| A-12 | 「3D 로 보기」 | 10번 연타 · 3D 중 연타 · 2D 에서 「나가기」 연타 | 진입 1회, 돌아갈 자리 불변, 2D 나가기 무시 | Entry::should_start_one_entry_when_button_pressed_many_times | 통과(기존) |
| A-13 | 「3D 로 보기」 | 준비 중 줌 12 · 20 으로 바꿈 | 직접 진입은 줌으로 취소 안 함 | Entry::should_keep_direct_entry_when_zoom_changed_while_pending(×4) | 통과(기존) |
| A-14 | 「3D 로 보기」 | 준비 중 가운데가 영역 밖으로 | 취소 · 안내 닫힘 · 단추 없음 | Entry::should_cancel_direct_entry_when_center_moved_out_of_area_while_pending | 통과(기존) |
| A-15 | 「3D 로 보기」 | 이벤트 지도 이동(flyTo) 중 누름 | 안 들어감 · 안내 없음, 끝나면 단추 | Entry::should_hold_entry_until_flight_ends_when_button_pressed_during_fly_to(×2) | 통과(기존) |
| A-16 | 「3D 로 보기」 | 3D 끔 · 영역 지움 | 단추 숨김 · 눌러도 무시 | Entry::should_hide_enter_button_when_area_deleted_or_3d_disabled | 통과(기존) |
| A-17 | 「3D 로 보기」 | 다시 만드는 중 | 줌 · 단추 진입 막힘("만드는 중"), 단추는 보임, 끝나면 자동 진입 | Entry::should_block_entry_but_keep_button_while_area_is_being_rebaked | 통과(기존) |
| A-18 | 입력 모드 | 편집 · 영역 그리기 · 심볼 배치 중 단추 · 영역 보기 | 거절 + "들어가지 않습니다", 모드 끝나면 단추로 진입 | Entry::should_refuse_button_and_area_view_with_reason_when_input_mode_active(×6) | 통과(기존) |
| A-19 | 단추 보임 | 2D↔3D 3회 순환 | 2D 에 「나가기」 없음 · 3D 에 「3D 로 보기」 없음 · 3D 에 줌 컨트롤 없음 | Entry::should_never_show_exit_in_2d_nor_enter_in_3d_when_states_cycle(×2) | 통과(기존) |
| A-20 | 전환 안내 | 만들어 둔(ready · rendered) 장면 재사용 | 안내 최소 0.4 s(설정이 짧으면 그 값) 뒤 펼침 | Entry 전 시나리오의 SettleEntry · Ui.Map3DTransitionTests::should_use_point4_seconds_reuse_guide_and_1point5_seconds_rendered_timeout_when_spec · Ui.Map3DGuideTests::should_keep_enter_guide_at_least_point6_seconds_when_scene_reused | 통과(기존) |
| A-21 | 전환 안내 | 불러오는 중 장면으로 진입 → 옛 페이지 늦은 ready(무시) → ready → rendered | ready 뒤 "그리는 중" 대기, rendered 받으면 펼침 · 로그 "3D 표시" · "전환 시간 … preloaded=true" | More::should_reveal_after_ready_then_rendered_when_entered_while_scene_loading | 통과(새) |
| A-22 | 전환 안내 | ready 뒤 rendered 1.5 s 안 옴 | 경고 "[Map3D] rendered 를 1.5 s 안에 받지 못함 → 그냥 펼침 job=…" + 펼침 | More::should_warn_and_reveal_when_rendered_not_received_within_timeout | 통과(새) |
| A-23 | 전환 안내 | 안내 표시 중 「나가기」 · ESC | 무시(준비 중) → 펼침 | Entry::should_ignore_exit_and_esc_while_entry_guide_is_showing(×2) | 통과(기존) |
| A-24 | 바인딩 | Off→Pending→Shown→Off 마다 PropertyChanged | Map3DStatusText · IsMap3DShown · IsMap3DZoomControlVisible · IsMap3DEnterButtonVisible 모두 통지 | More::should_notify_status_exit_zoom_and_enter_bindings_when_state_changes | 통과(새) |
| A-25 | 영역 보기 | 배지 · 목록 「이 영역 3D 로 보기」(먼 영역, 줌 12) | 영역 가운데 · 바로 진입 · 게이트가 취소 안 함, 나가면 누르기 전 자리(줌 12) | Entry::should_enter_directly_when_area_view_chosen_from_badge_or_list(×2) | 통과(기존) |
| A-26 | 영역 보기 | 회전 영역 / 옛(북쪽 정렬) 영역 | 회전 영역은 지도를 그 방위로 · 가운데 | Nav::should_center_and_rotate_map_when_area_view_opens_rotated_area · Nav::should_center_without_rotating_when_area_view_opens_old_area | 통과(기존) |
| A-27 | 영역 보기 | 영역 진입 줌을 17.5 보다 높인 영역 | 영역 보기 줌 = 그 진입 줌 | Ui.Map3DSettingsTests::should_view_area_at_enter_zoom_or_higher_when_enter_zoom_raised | 통과(기존) |
| A-28 | 영역 보기 | 편집 중 「이 영역 3D 로 보기」(거절) → 편집 끔 → 다른 자리 · 줌 18 에서 「3D 로 보기」 → 나가기 (+ 자동: 편집 끄면 게이트 진입 → 나가기) | 단추 누른 자리(줌 18)로 / 게이트가 들어온 자리(영역 가운데 · 17.5)로 | More::should_return_to_button_press_spot_when_area_view_was_refused_before_in_edit_mode · More::should_return_to_gate_entry_spot_when_area_view_was_refused_then_edit_mode_ended_in_auto | 수정 후 통과(D-1) |
| A-29 | 영역 보기 | 3D 중 다른 영역 「이 영역 3D 로 보기」 | 새 영역 3D, 나가면 처음 진입 전 자리 | More::should_open_other_area_and_return_to_spot_before_first_entry_when_area_view_chosen_in_3d | 통과(새) |
| A-30 | 영역 보기 | 준비 중(앞 「영역 보기」) 다른 영역 「이 영역 3D 로 보기」 | 새 영역 3D, 나가면 처음 「영역 보기」 누르기 전 자리(줌 12) | More::should_open_other_area_and_return_to_spot_before_first_area_view_when_chosen_while_pending | 수정 후 통과(D-6) |
| A-31 | 영역 보기 | 만드는 중 「이 영역 3D 로 보기」 | 거절 + "만드는 중", 안내 없음, 돌아갈 자리 · 영역 보기 표지 남기지 않음 | More::should_refuse_area_view_with_reason_and_leave_no_return_spot_when_baking | 수정 후 통과(D-1) |
| A-32 | 영역 보기 | 목록 항목은 있는데 장면 캐시에 없음(자동이면 게이트가 찾아 진입) | — | — | 미시험(항목은 ApplyMap3DAreas 가 장면 캐시와 함께 만들어 실제로는 도달하기 어려움) |
| A-33 | 「나가기」 | 나가기 → 들어가기 직전 2D 자리(위치 · 줌 · 방위), 없으면 3D 마지막 카메라 | 그 자리 | Entry::should_enter_by_button_exit_and_never_reenter_by_zoom_when_manual · Ui.Map3DEnterViewTests::should_return_to_2d_place_just_before_entering_when_exiting 외 2 | 통과(기존) |
| A-34 | 「나가기」 | 2D 에서 「나가기」 반복 | 아무 일 없음 | Entry::should_start_one_entry_when_button_pressed_many_times | 통과(기존) |
| A-35 | 재진입 보류 | 나간 뒤 같은 줌(자동) | 곧바로 다시 안 들어감, 진입 줌 밑으로 한 번 내리면 풀림, 단추로는 바로 | Entry::should_leave_by_esc_and_not_reenter_at_same_zoom_when_3d_shown(×2) · Entry::should_keep_3d_and_apply_new_rule… | 통과(기존) |
| A-36 | Esc | WPF 안(composition) 3D 에서 Esc | 안 나감(「나가기」로만) · 2D 입력 계속 막힘 | Entry::should_leave_by_esc_and_not_reenter… · Exit::should_keep_2d_blocked_by_esc_and_restore_only_by_exit_button_when_3d_shown_in_wpf | 통과(기존) |
| A-37 | Esc | 2D 에서 Esc | 3D 경로가 받지 않음 | 같은 두 시험 | 통과(기존) |
| A-38 | Esc | 「건물 높이」 판 열림 + Esc | 판만 취소, 3D 유지 / 3D 를 나가면 판 닫고 미리보기 원래대로 | BH::should_follow_handle_drag_and_go_back_when_drag_is_cancelled_or_escape_is_pressed · BH::should_put_original_back_when_cancel_close_escape_or_leaving_3d | 통과(기존) |
| A-39 | 처음 시점 | 모든 진입에 home(영역 전체 · 2D 방위 · 기울기 · 애니메이션), 회전 영역 「영역 보기」는 그 회전각 | 메시지 내용 | Ui.Map3DEnterViewTests::should_open_whole_area_home_view_tilted_with_2d_bearing_when_entering · ::should_send_same_enter_view_as_area_view_home · Ui.Map3DExitTests::should_send_home_type_with_bearing_pitch_animate_when_asking_whole_area_view · Ui.Map3DRotatedAreaTests::should_enter_area_view_with_rect_rotation | 통과(기존, 순수) · 실제 전송은 헤디드 |
| A-40 | Esc | EscExitsMap3D 판정 | hwnd 만 true | Ui.Map3DScreenTests::should_exit_3d_by_esc_only_when_native_window_hides_exit_button | 통과(기존) |
| A-41 | Esc | 네이티브 창(hwnd) 3D 에서 Esc(자동) | 나감 · 로그 "2D 복귀(esc)" · 같은 줌 재진입 보류 · 2D 입력 살아 있음 · 2D 에서 Esc 는 안 받음 | More::should_exit_hold_reentry_and_restore_input_when_esc_pressed_in_native_window_3d | 통과(새) · 실기 헤디드 |
| A-42 | 축소 한계 | 3D 에서 너무 멀리 축소 | 뷰어가 막음 | (영역 F — map-3d JS) | 헤디드(체감) |
| A-43 | 2D 입력 | 3D 가 보이는 동안 | 2D 지도 IsHitTestVisible=false | Exit::Enter(모든 Exit 시나리오) | 통과(기존) |
| A-44 | 2D 입력 | 3D 중 다른 코드가 누름 판정을 바꿈 · 두 번 펼침 | 나가면 막기 전 값 | Exit::should_restore_2d_input_when_other_code_changed_it_or_3d_revealed_twice_during_3d | 통과(기존) |
| A-45 | 2D 입력 | 5회 들어갔다 나오기 | 매번 휠 · 끌기 · 클릭 | Exit::should_restore_2d_input_every_time_when_entered_and_exited_many_times | 통과(기존) |
| A-46 | 2D 입력 | 3D 에서 돌리고 줌 바꾼 뒤 나가기(사용자 보고) | 휠 · 끌기 · 클릭 · 줌 17 | Exit::should_accept_wheel_drag_click_when_exited_after_rotating_and_zooming_in_3d(×2) | 통과(기존) |
| A-47 | 2D 입력 | 레이어 판 · 카메라 상자 · 빈 곳 클릭 · 건물 높이 판(+취소) 연 채 나가기 | 입력 살아 있음 · 판 닫힘 | Exit::should_restore_2d_input_when_exited_with_panel_or_window_opened_in_3d(×6) | 통과(기존) |
| A-48 | 2D 입력 | 나가는 중 3D 로 보내기 예외 | 입력 살아 있음 · 격리 로그 | Exit::should_restore_2d_input_and_close_panel_when_3d_send_throws_while_exiting | 통과(기존) |
| A-49 | 2D 입력 | 나가는 길 6종(단추 · 편집 모드 · 로딩 실패 · 설정 끔 · 네이티브 전환 · 이벤트 이동) | 입력 살아 있음 | Exit::should_restore_2d_input_whichever_way_3d_is_left(×6) | 통과(기존) |
| A-50 | 2D 입력 | 남은 제스처(이미지 끌기 · 팬 억제 · 마퀴 · 선 눌림) | 정리 + 경고 "나가며 남은 2D 입력 상태 정리" | Exit::should_clear_stuck_gesture_and_accept_input_when_exited_after_release_was_missed_in_3d(×4) | 통과(기존) |
| A-51 | 진단 줄 | 나간 직후 · 한가할 때 / 조상이 막을 때 | "[Map3D] 2D 입력 상태(<원인> · now|idle) hit=…" · 막는조상 이름 | Exit::should_log_2d_input_state_now_and_when_idle_after_exit · Exit::should_name_blocking_state_in_log_when_2d_input_is_still_blocked_after_exit | 통과(기존) |
| A-52 | 3D 카메라 | 3D camera 줌 10 · 3 · 16.4(이탈 줌 밑) | 3D 유지(줌으로 안 나감) · 게이트도 안 내림 | More::should_stay_in_3d_when_3d_camera_zoom_drops_below_exit_zoom(×3) · Ui.Map3DEnterViewTests::should_keep_following_3d_camera_in_2d_and_not_leave_at_any_zoom | 통과(새) |
| A-53 | 3D 카메라 | composition 은 2D 가 3D 카메라를 따라감 · hwnd 는 안 따라감 · 줌 NaN 무시 | — | Ui.Map3DEnterViewTests::should_keep_following… · ::should_not_move_2d_under_native_window_3d · ::should_not_move_2d_when_camera_zoom_is_not_finite | 통과(기존, 순수) · composition 실동작은 헤디드 |
| A-54 | 줌 컨트롤 | MapView.xaml 줌 컨트롤 Visibility ↔ IsMap3DZoomControlVisible, 줌 컨트롤 하나뿐, 지도 루트 ItemStatus ↔ Map3DStatusText | 바인딩 문구 | More::should_bind_zoom_control_visibility_to_3d_state_when_map_view_read | 통과(새) |
| A-55 | 줌 컨트롤 | 판정 함수 | 3D 에서 숨김 · 「나가기」와 정반대 | Ui.Map3DHudTests::should_hide_zoom_control_when_3d_is_shown · ::should_show_zoom_control_exactly_when_exit_button_is_hidden · Entry::should_never_show_exit… | 통과(기존) |
| A-56 | 좌표 칩 | cursor 메시지 — 2D 에서(무시) · 3D 에서(고도 "58.3 m", 음수 "-3.0 m", 고도 없음 → 숨김) · 나간 뒤 | 3D 중에만 좌표 · 고도, 나가면 null | More::should_show_cursor_coordinate_and_altitude_only_in_3d_when_cursor_messages_arrive | 통과(새) |
| A-57 | 좌표 칩 | cursor 해석 · 고도 글 | — | Ui.Map3DHudTests::should_parse_cursor_message_* (3) · ::should_format_altitude_with_one_decimal_and_hide_when_unknown | 통과(기존) |
| A-58 | 축척 | 3D camera(줌 18.3 · 15.2) → 축척 막대, 나가면 2D 값 | Map3DMath.ScaleBar(MetersPerPixel) 글 / 들어가기 전 2D 글 | More::should_draw_scale_from_3d_camera_and_restore_2d_scale_when_exited | 통과(새) |
| A-59 | 축척 | m/px · 막대 길이 | — | Ui.Map3DHudTests::should_compute_meters_per_pixel_like_web_mercator 외 4 | 통과(기존) |
| A-60 | UIA | 지도 루트 ItemStatus "3D"/"2D" | 3D 표시 중만 "3D" | Ui.Map3DEnterViewTests::should_show_3d_status_only_while_3d_is_shown · More::should_bind_zoom_control…(ItemStatus 바인딩) | 통과(기존·새) |
| A-61 | 화면 | 「3D 로 보기」 단추 위치 · 스타일 · 바인딩 · 도움말 · x:Name 없음 · 토큰 색 | — | Entry::should_place_enter_button_next_to_exit_with_same_styles_when_map_view_read | 통과(기존) |
| A-62 | 전환 안내 | 나갈 때 "2D 지도로 돌아갑니다" 0.5 s | 제목 · 시간 | Ui.Map3DGuideTests::should_use_spec_titles_when_entering_or_leaving_3d | 통과(기존, 순수) |
| A-63 | 전환 안내 | 로그인 화면 뒤에서 펼침 → 로그인 직후 1회 안내 | — | Ui.Map3DGuideTests::should_detect_login_screen_when_token_state_known · ::should_guide_once_after_login_when_3d_revealed_behind_login | 통과(기존, 순수) · VM 경로 미시험(IoC 세션 · 토큰 서비스 필요) |
| A-64 | 2D 입력 | 펼칠 때 2D 를 끌던 중(벤더 코어 _core.IsDragging, 지도 IsDragging 함께/코어만) 남은 채 나가기 | 코어 · 지도 끌기 정리, 경고에 "coreDrag", 휠 · 끌기 · 클릭, 정수 줌(18)을 코어가 받음 | More::should_clear_vendor_core_drag_when_exited_after_drag_left_over(×2) | 수정 후 통과(D-3) |
| A-65 | 2D 입력 | 들어가기 전 지도 로컬 IsHitTestVisible=false | 나간 뒤 false 그대로 | More::should_restore_local_hit_test_false_when_it_was_false_before_entering | 통과(새) |
| A-66 | 영역 사라짐 | 보는 중 영역 지움(확인 → DELETE) · 서버 목록에서 빠짐 · 3D 화면 scene-missing | 2D · 입력 살아 있음 · 진단 줄 · 목록 · 배지 · 호스트 장면 비움 · 재진입 없음 | More::should_restore_2d_input_and_hold_reentry_when_shown_area_disappears(×3) · Stale::should_unload_host_and_never_revive_area_when_delete_confirmed · ::should_leave_to_2d_with_notice_when_shown_job_disappears_from_server_list · ::should_retire_job_and_refresh_list_when_viewer_reports_scene_missing | 통과(새·기존) |
| A-67 | 로딩 실패 | 3D 화면 scene-broken(지금 장면) / 다른 장면 error | 지금 장면이면 취소 · 사유 안내 · 재진입 보류 · 영역 유지 / 다른 장면은 무시 | More::should_cancel_pending_hold_reentry_and_tell_reason_when_page_reports_load_error · Stale::should_cancel_entry_without_retiring_area_when_viewer_reports_broken_scene · Stale::should_parse_error_message_and_mark_host_failed_but_ignore_stale_ready | 통과(새·기존) |
| A-68 | 로딩 실패 | WebView2 준비 실패 · 렌더 프로세스 죽음(3D 중) | 2D · 보류 · 입력 살아 있음 | Exit::should_restore_2d_input_whichever_way_3d_is_left(LoadFailed) | 통과(기존) |
| A-69 | 서버 꺼짐 | 장면 조회(/api/scenes/at, 400 ms 디바운스)가 서버에 못 닿음 | 배너 "3D 서버(map-3d)가 꺼져 있습니다", 다음 같은 자리 재조회 | Ui.Map3DClientTests::should_throw_unavailable_when_server_refuses_connection(클라이언트만) | 미시험(VM 경로 — DispatcherTimer 400 ms + 실제 조회 흐름, 이번 범위에서 덜 중요) |
| A-70 | 미리 불러오기 | 자동 · 수동 — 줌 17(진입 19 · 미리 준비 15) 영역 안 | LoadMap3DScene 1회 · 로그 "[Map3D] 미리 불러오기 job=area-a zoom=17.0" · 2D 유지 · 줌 바꿔도 다시 안 엶 | More::should_preload_once_and_enter_without_new_navigation_when_zoom_reaches_preload_zoom_inside_area(×2) | 통과(새) · 수동 미리 불러오기는 결정 필요 |
| A-71 | 미리 불러오기 | 만드는 중 · 편집 모드 · 3D 끔 · 다른 장면 로딩 중 · 미리 준비 줌 밑 · 영역 밖 · 독점 입력 모드 | 안 함 | More::should_not_preload_when_blocked(×7) · Ui.Map3DTransitionTests::should_preload_only_when_zoom15_inside_area_and_host_free | 통과(새·기존) |
| A-72 | 미리 불러오기 | 미리 연 장면으로 이어 진입 | 다시 탐색 안 함(_url 그대로) · "진입 시작 … preloaded=True" · "전환 시간 … preloaded=true" | More::should_preload_once_and_enter_without_new_navigation…(후반) | 통과(새) |
| A-73 | 미리 데우기 | 지도가 뜬 뒤 WebView2 기동만 미리(PrewarmMap3DAsync) | 로그 "WebView2 미리 데우기 시작/끝 n.nns" | — | 헤디드(WebView2 필요) |
| A-74 | 묶음 회전 | 3D 중 「회전」 / 편집 모드 켬 | CanExecute false / 편집 켜면 3D 접음("2D 복귀(input-mode)") · 편집 중 단추 거절 · 끄면 수동은 재진입 없음 | More::should_refuse_group_rotation_in_3d_and_leave_3d_when_edit_mode_turned_on | 통과(새) |
| A-75 | 다중 선택 | 묶음 선택 상태에서 3D 진입 시도 | 묶음 선택은 편집 모드 안에서만 → 편집 모드 거절과 같음 | Entry::should_refuse_button_and_area_view_with_reason_when_input_mode_active(EditMode) | 통과(기존) |
| A-76 | 입력 모드 | 3D 중 측정 · 조준 · 심볼 배치 · 앵커 그리기 시작 | 2D 복귀 · 로그 "[Map3D] 2D 복귀(input-mode) zoom=" · 2D 가 입력 받음 · 모드 중 단추 거절 | More::should_return_to_2d_and_not_reenter_in_manual_when_input_mode_started_in_3d(×4) | 통과(새) |
| A-77 | 입력 모드 | 모드 끝(수동) / 편집 껐다 켬(자동) | 수동: 재진입 없음 · 단추 보임 / 자동: 곧바로 재진입(지금 동작) | More::…started_in_3d(후반) · More::should_refuse_group_rotation…(후반) | 통과(새) · 자동 재진입은 결정 필요 |
| A-78 | 만들기 취소 | 「다시 만들기」 취소 | 옛 장면 그대로 · 그 장면으로 「3D 로 보기」 진입 | More::should_enter_previous_scene_when_rebake_cancelled · Cancel::should_keep_previous_scene_when_rebake_cancelled | 통과(새·기존) |
| A-79 | 만들기 끝 | 자동 · 영역 안 · 진입 줌 이상에서 새 영역 만들기 취소 / 실패 / 다른 곳에서 취소 | 지도를 안 움직여도 게이트 재판정 → 3D | More::should_reenter_3d_without_moving_map_when_bake_ends_without_new_scene_inside_area_in_auto(×3) | 수정 후 통과(D-4) |
| A-80 | 입력 모드 | 준비 중(안내 표시) 영역 그리기 시작 | 취소 · 안내 닫힘 · 로그 "로딩 중 입력 모드 시작 → 취소" · 모드 끝나도 수동은 재진입 없음 | More::should_cancel_pending_entry_and_close_guide_when_input_mode_starts_while_pending | 통과(새) |
| A-81 | 네이티브 창 전환 | HostFallback 이벤트 실제 발생 — 3D 중 / 준비 중 | 3D 중 → 2D · 입력 살아 있음 · 경고 로그 / 준비 중 → 상태 그대로(같은 주소 다시 열기) | More::should_return_to_2d_with_live_input_when_host_falls_back_to_native_window_while_shown | 통과(새) |
| A-82 | 원격 세션 | RDP 면 네이티브 창 · 저사양(low=1) | — | Ui.Map3DPerfTests::should_pick_native_window_when_remote_session · Ui.Map3DMathTests::should_ask_low_quality_3d_when_remote_session | 통과(기존, 순수) · 헤디드 |
| A-83 | 미리 불러오기 | 2D 에서 미리 연 장면이 ready · rendered | 저절로 안 펼침 · 안내 없음, 단추로 바로 재사용 진입 | More::should_stay_2d_and_keep_page_parked_when_preloaded_scene_becomes_ready_in_2d | 통과(새) |
| A-84 | 전환 안내 | 불러오는 중 progress(stage · pct) | 안내 둘째 줄 "3D 장면을 받는 중… 42 %" · 막대 42 | More::should_show_page_progress_while_waiting_for_ready_when_entered_while_scene_loading | 통과(새) |
| A-85 | 이벤트 이동 | 3D 중 이벤트 지도 이동이 장면 밖/이탈 줌 밑에서 끝남(Sync3DAfter2DMove) | 2D, 아니면 3D 카메라 이동 | Ui.Map3DOverlayTests::should_stay_3d_after_move_when_inside_scene_and_zoom_enough(순수) · Exit::…whichever_way(EventFlyTo — Leave3D 직접) | 통과(기존, 순수) · VM 경로(EventFlyTo 연결)는 영역 C 쪽 |
| A-86 | 안내 문구 | 로그인 직후 3D 위 안내 "지금 3D 지도를 보고 있습니다 (나가기 · ESC = 2D)" — composition 에서는 ESC 로 안 나감 | 문구가 실제와 다름(D-2) | Ui.Map3DHudTests::should_guide_shown_3d_without_zoom_wording 이 Contains("ESC") 로 고정 | 결정 필요 |
| A-87 | 순수 함수 | 제품이 안 쓰는 Map3DMath.ShouldLeave3D(3종) · ExitCamera(bool,…) 를 시험(D-5) | — | Ui.Map3DExitTests::should_not_leave_by_zoom_when_entered_by_area_view 외 · Ui.Map3DMathTests::should_leave_3d_when_reported_zoom_below_16_5 · Ui.Map3DAreaZoomTests::should_leave_3d_below_area_exit_when_camera_reports_zoom | 결정 필요(보고만) |

## 고친 결함

### D-1 거절된 「이 영역 3D 로 보기」가 돌아갈 자리 · 영역 보기 표지를 남김
- 원인: `ViewMap3DArea` 가 `_map3DReturn` · `_map3DAreaViewJob` 을 먼저 잡고 `EnterMap3DDirect` 를 부르는데, 편집 · 그리기 · 만드는 중 · 비행 중으로 거절돼도 지우지 않는다. 다음 진입은 `_map3DReturn ??=`(EnterMap3DDirect) · `Reveal3D` 의 `??=` 로 그 옛 자리를 그대로 쓰고, 자동이면 같은 영역의 게이트 진입도 `_map3DAreaViewJob == scene.Id` 라 옛 자리를 유지한다 (Ironwall.Dotnet.Libraries.GMaps.Ui/ViewModels/Maps/MapViewModel.Map3DAreas.cs:204-215, MapViewModel.Map3D.cs:448 · 764 · 799).
- 수정: `ViewMap3DArea` — 장면이 있어 `EnterMap3DDirect` 를 불렀는데 상태가 그대로 Off(거절)면 `_map3DReturn = null; _map3DAreaViewJob = null;` (MapViewModel.Map3DAreas.cs).
- 증거: 수정 전(수정 줄만 주석 처리해 다시 돌림)
  - `More::should_return_to_button_press_spot_when_area_view_was_refused_before_in_edit_mode` 실패 — "「3D 로 보기」를 누른 자리로 돌아와야 한다: 줌 18 이어야 하는데 17"
  - `More::should_return_to_gate_entry_spot_when_area_view_was_refused_then_edit_mode_ended_in_auto` 실패 — "게이트가 들어온 직전 자리로 돌아와야 한다: 줌 17.5 이어야 하는데 17"
  - `More::should_refuse_area_view_with_reason_and_leave_no_return_spot_when_baking` 실패 — "Assert.Null() Failure: Value of type 'Nullable<Map3DCamera>' has a value … Zoom = 17"
  - 수정 후 세 시험 통과.

### D-3 3D 를 나갈 때 남은 벤더 지도 끌기(_core.IsDragging)를 정리하지 않음
- 원인: `GMapCustomControl.ResetTransientInput` 은 이미지 끌기 · 마퀴 · 선 눌림 · 팬 억제 · 캡처만 비우고 벤더 코어 끌기(`GMapControl._core.IsDragging`, 지도 `IsDragging`)는 비우지 않았다(GMapCustomControl.cs:1025-1034). 3D 가 펼쳐질 때 끌던 중이면 뗌을 3D 화면이 받아 남는다 — 벤더 코어는 끌기 중 정수 줌(Core.Zoom setter `!IsDragging`, Core.cs:107) · 위치 이동(GoToCurrentPosition, Core.cs:168)을 받지 않고, 벤더 휠 줌도 `!_core.IsDragging` 일 때만 한다(GMapControl.cs:1884). `_core` 는 벤더 private 이라 파생 컨트롤에서 볼 수 없었다.
- 수정: 벤더 `GMapControl` 에 seam 두 개 추가(공개 형식 더하기만, 기존 `IsCoreStarted` seam 과 같은 방식) — `public bool IsCoreDragging => _core.IsDragging;` · `public void EndCoreDrag()`(OnMouseUp 의 끌기 끝과 같은 정리: 지도 IsDragging · 커서 · `_core.EndDrag()`). `ResetTransientInput` 에서 `if (IsDragging || IsCoreDragging) { EndCoreDrag(); cleared.Add("coreDrag"); }`, 진단 `InputGateText` 의 "coreDrag" 도 코어 끌기를 함께 본다 (GMap.NET/GMap.NET/GMap.NET.WindowsPresentation/GMapControl.cs, Ironwall.Dotnet.Libraries.GMaps.Ui/GMapCustoms/GMapCustomControl.cs).
- 증거: 수정 전 `More::should_clear_vendor_core_drag_when_exited_after_drag_left_over(controlDraggingToo: False|True)` 둘 다 실패 — "나간 뒤에도 벤더 코어 끌기가 남았다" / 수정 후 통과(정리 경고에 coreDrag · 휠 · 끌기 · 클릭 · 정수 줌 18 을 코어가 받음).

### D-4 만들기 취소 · 실패로 끝나면 게이트를 다시 판정하지 않음
- 원인: 굽는 동안 게이트는 진입을 막는다. 다 만든 경우만 `QueueMap3DGate()` 를 부르고(MapViewModel.Map3D.cs:593), 취소(CancelMap3DBakeAsync → EndMap3DBake) · 실패(job.IsError) · 다른 곳에서 취소(job.IsCancelled) 길은 부르지 않아(MapViewModel.Map3D.cs:506-508 · 575-584 · EndMap3DBake 529-540) 자동 · 영역 안 · 진입 줌 이상이어도 지도를 움직이기 전까지 3D 로 안 들어갔다(「다시 만들기」는 ReapplyMap3DAreas 가 게이트를 불러 우연히 피함).
- 수정: `EndMap3DBake` 끝에 `QueueMap3DGate()`(굽기가 끝나는 모든 길 — 프레임당 1회로 모여 중복 무해) (MapViewModel.Map3D.cs).
- 증거: 수정 전 `More::should_reenter_3d_without_moving_map_when_bake_ends_without_new_scene_inside_area_in_auto(cancel|error|cancelled-elsewhere)` 셋 다 실패 — "만들기가 끝났는데(cancel) 지도를 움직이기 전까지 3D 로 들어가지 않는다"(error · cancelled-elsewhere 같은 문구) / 수정 후 통과.
  - 참고: 처음엔 cancel 변형이 우연히 통과했다 — 영역 표시의 첫 방위 통지(OnMap3DAreaBearingChanged → 게이트 큐)가 취소 직후에 들어왔기 때문. 시험에서 그 통지가 만드는 동안 지나가게 1.2 s 기다린 뒤 다시 돌려 실패를 확인했다.

### D-6 준비 중 다른 영역 「이 영역 3D 로 보기」가 처음 자리를 잃음(새로 찾음)
- 원인: `ViewMap3DArea` 는 3D 표시 중(Shown)일 때만 처음 돌아갈 자리를 이어 받고(`_map3DState == Shown ? _map3DReturn : null`), 준비 중(Pending)이면 `CancelMap3DPending` 이 `_map3DReturn` 을 지운 뒤 지금 2D(앞 「영역 보기」가 옮긴 자리)를 새로 잡았다 (MapViewModel.Map3DAreas.cs:200).
- 수정: `var before = _map3DState != Map3DState.Off ? _map3DReturn : null;` — 준비 중에도 처음 자리를 이어 받는다(게이트로 준비 중이면 `_map3DReturn` 이 null 이라 지금 2D — 그대로) (MapViewModel.Map3DAreas.cs).
- 증거: 수정 전 `More::should_open_other_area_and_return_to_spot_before_first_area_view_when_chosen_while_pending` 실패 — "처음 「영역 보기」를 누르기 전 자리로: 줌 12 이어야 하는데 17.5" / 수정 후 통과.

## 결정
- D-1 수정 방식: 지시대로 "거절되면 그 호출이 잡은 돌아갈 자리 · 영역 보기 표지를 지운다"(ViewMap3DArea 안, 장면이 있어 바로 들어가기를 부른 경우만 — 장면 캐시에 없는 자동 게이트 경로는 그대로).
- D-3 수정 위치: 벤더 `_core` 가 private 이라 (a) 제품 코드 리플렉션 (b) 벤더 OnMouseUp 을 가짜 이벤트로 부르기 (c) 벤더에 seam 추가 중 (c) — 벤더에 이미 같은 목적의 seam(`IsCoreStarted`)이 있고, (b)는 끌기가 아닐 때 선택 영역 통지 등 부수 동작이 있다. 공개 형식은 더하기만.
- D-6(새로 찾음): A-30 기대("돌아갈 자리는 처음 진입 전")에 맞춰 고침. 준비 중 상태의 `_map3DReturn` 은 바로 들어가기가 잡은 누르기 전 자리이거나 null 이라 부작용 없음.
- 결정 필요(지시대로 고치지 않음):
  - D-2: 로그인 직후 3D 위 안내 `Map3DGuide.StageText("shown")` = "지금 3D 지도를 보고 있습니다 (나가기 · ESC = 2D)" — composition 3D 는 ESC 로 나가지 않는다(10-19). 기존 시험 `Ui.Map3DHudTests::should_guide_shown_3d_without_zoom_wording` 이 Contains("ESC") 로 고정 — 결정 필요(예: hwnd 일 때만 "· ESC" 를 붙이기).
  - D-5: 제품이 안 쓰는 `Map3DMath.ShouldLeave3D`(3 오버로드) · `ExitCamera(bool, …)` 를 기존 시험 여럿이 시험한다(Map3DExitTests 4건 · Map3DMathTests 1건 · Map3DAreaZoomTests 1건) — 7차 "줌으로 나가지 않음" 이후 죽은 코드. 지울지 결정 필요(보고만).
  - 수동(단추) 모드에서도 미리 불러오기를 한다(`ShouldPreload` 가 방식을 안 봄) — 수동은 진입이 단추라 미리 불러온 페이지가 안 쓰일 수 있다. 지금 동작을 시험으로 기록(More::should_preload_once…(manual)) — 유지할지 결정 필요.
  - 자동 모드에서 3D 중 편집 · 측정 등 입력 모드를 켰다 끄면 `SyncMap3DWithInputModes` → 게이트가 곧바로 다시 들어간다(Leave3D(input-mode) 는 재진입 보류를 걸지 않음). 「나가기」와 달리 사용자가 "나가겠다"고 한 것이 아니라 의도된 동작으로 보이나, 측정이 끝나자마자 3D 로 다시 바뀌는 것이 맞는지 결정 필요. 시험으로 고정하지 않았다(보고만).
- 작업 환경: 받은 worktree 가 옛 main(f7643f9e, 3D 코드 없음)이었다. git checkout 은 쓰지 않고 임시 index 파일(`GIT_INDEX_FILE`)로 0d732769 트리를 작업 폴더에 풀어(checkout-index) COORD 와 같게 맞추고, 그 커밋에서 지워진 파일 3개를 지웠다. 서브모듈 `Ironwall.Dotnet.Libraries.OnvifSolution` 은 비어 있어 COORD 의 소스를 복사했다(빌드용, 패치 제외). 패치는 같은 임시 index 기준 `git diff` 라 COORD(0d732769)에 바로 붙는다(`git apply --check` 통과).
- 하네스 가드가 worktree 밖 쓰기를 막아 패치 · 새 파일 · 이 보고서를 COORD 가 아니라 이 worktree 의 같은 상대 경로(`.harness/t-20261019-193448-b2cc/patches/A.patch` · `patches/A-new/…` · `areas/A.md`)에 두었다.

## 3. 헤디드 전용(HEADED 용) — 단계 · AutomationId · 기대 · 판정

공통 판정 로그 줄(코드의 정확한 문자열):
- 진입: `[Map3D] 바로 들어가기(<enter-button|area-view>) job=<id> mode=<auto|manual>` → `[Map3D] 진입 시작 job=<id> zoom=<z> reuse=<True|False> preloaded=<True|False> prewarmed=<True|False>` → (`[Map3D] ready job=<id>` · `[Map3D] rendered job=<id> items=<n>`) → `[Map3D] 3D 표시 job=<id> host=<composition|hwnd>` → `[Map3D] 전환 시간 total=<s> webview=<s|-> page=<s|-> snapshot=<s|-> rendered=<s|-> reveal=<s> (prewarmed=<true|false>, preloaded=<true|false>)`
- rendered 시간 초과: `[Map3D] rendered 를 1.5 s 안에 받지 못함 → 그냥 펼침 job=<id>`(경고)
- 나감: `[Map3D] 2D 복귀(<exit-button|esc|input-mode|settings-off|host-fallback|load-failed|area-view|area-delete|list-missing|scene-missing|…>) zoom=<z> bearing=<b>` → `[Map3D] 2D 입력 상태(<원인> · now) hit=True enabled=True visible=True 막는조상=없음 가운데=지도 캡처=없음 … 3D펼침=False … 지도상태=없음` → 같은 줄 `· idle)`
- 남은 제스처: `[Map3D] 나가며 남은 2D 입력 상태 정리: <imageDrag,rubberBand,linePress,panSuppress,coreDrag,capture 중>`(경고)
- 미리: `[Map3D] WebView2 미리 데우기 시작` · `[Map3D] WebView2 미리 데우기 끝 <s>s` · `[Map3D] 미리 불러오기 job=<id> zoom=<z>`
- 준비 중 취소: `[Map3D] 로딩 중 게이트 이탈(zoom=<z>) → 취소` · `[Map3D] 로딩 중 입력 모드 시작 → 취소`
- 영역 보기: `[Map3D] 영역 보기 id=<id> → (<lat>,<lon>) zoom=<z>`

| 단계 | 조작 · AutomationId | 기대 | 판정 |
|---|---|---|---|
| H-A1 처음 시점(home) 실제 전송 | 줌 17 · 영역 안에서 `Map.Map3D.EnterButton` 누름(수동) / 자동은 줌 17 로 | 영역 전체가 화면에 들어오는 비스듬한 시점으로 0.6 s 눕기, 회전 영역 「이 영역 3D 로 보기」(`GMaps.Map3D.AreaList` 항목 · `GMaps.Map3D.AreaBadge.{id}` 우클릭)는 사각형이 반듯하게 | `GMaps.Map.Root` ItemStatus "3D" · `GMaps.Map3D.ExitButton` 보임 · 로그 "3D 표시 job=" · 캡처(영역 네 변이 화면 안) |
| H-A2 축소 한계 체감(영역 F) | 3D 에서 휠로 끝까지 축소 | 영역이 너무 작아지기 전에 멈춤 · 2D 로 안 나감 | ItemStatus "3D" 유지 · 캡처 |
| H-A3 hwnd Esc 실기 | 원격(RDP) 또는 `UseCompositionHost=false` → 3D 진입 → Esc | 나감 · 2D 입력 즉시 동작 | 로그 "3D 표시 job=… host=hwnd" → "2D 복귀(esc)" · ItemStatus "2D" · `GMaps.Map3D.ExitButton` 사라짐 |
| H-A4 composition 2D 따라가기 · 나간 뒤 방위 | 3D 에서 마우스로 돌리고(약 60°) 확대/축소 → `GMaps.Map3D.ExitButton` | 3D 동안 나침반 · 축척 · 좌표 칩(고도 줄 "58.3 m" 꼴)이 3D 를 따라감, "3D 가능" 칩 `GMaps.Map3D.Chip` 은 안 보임, 줌 컨트롤(`GMaps.MapZoom.Label` · `GMaps.MapZoom.ZoomInButton` · `GMaps.MapZoom.ZoomOutButton` · `GMaps.MapZoom.Slider`) 숨김 · 나가면 들어가기 직전 2D 자리 · 방위 | "2D 복귀(exit-button) zoom=<들어가기 전 줌> bearing=<들어가기 전 방위>" · 나간 뒤 `GMaps.MapZoom.*` 다시 보임 |
| H-A5 나간 뒤 실제 마우스 | 나간 직후 지도 클릭 · 우클릭 · 휠 위/아래 · 끌기, 2D 끌던 중 3D 펼침(자동 줌 진입 순간 끌기) 뒤 나가기 | 모두 즉시 동작(D-3: 끌던 중 펼친 경우도) | "2D 입력 상태(exit-button · now|idle) hit=True … 지도상태=없음" · 끌다 들어간 경우 "나가며 남은 2D 입력 상태 정리: …coreDrag" |
| H-A6 미리 데우기 · 미리 불러오기 | 앱 시작 → 지도 뜬 뒤 대기 → 줌 15 · 영역 안 → 줌 17 | 첫 진입이 짧다 | "WebView2 미리 데우기 시작/끝" · "미리 불러오기 job=" · "전환 시간 … (prewarmed=true, preloaded=true)" |
| H-A7 진행 패널 | 처음(미리 안 연) 진입 | `GMaps.Map3D.Progress` 에 "3D 지도로 전환합니다"(`GMaps.Map3D.Progress.Title`) · 단계 %(`GMaps.Map3D.Progress.Detail`) → 펼침, 나갈 때 "2D 지도로 돌아갑니다" 0.5 s | UIA 글 · 로그 순서 |
| H-A8 만들기 취소 뒤 자동 진입(D-4) | 자동 · 영역 안 줌 17 에서 `GMaps.Map3D.DrawButton` → 옆에 새 영역 → [만들기] → 진행 판 `GMaps.Map3D.CancelBake` | 취소 직후 지도를 안 움직여도 옛 영역 3D 로 | "만들기 취소 응답 … result=requested" 뒤 "진입 시작 job=<옛 id>" |
| H-A9 거절된 영역 보기 뒤(D-1) | 편집 모드에서 영역 목록 「이 영역 3D 로 보기」(거절 안내) → 편집 끔 → 다른 자리 · 줌 18 → `Map.Map3D.EnterButton` → `GMaps.Map3D.ExitButton` | 줌 18 · 누른 자리로 돌아옴 | "2D 복귀(exit-button) zoom=18.0" |

## 시험 결과 — 프로젝트별 통과/실패 수(명령 그대로)
- `dotnet test tests/GMaps.MapEdit.Tests --nologo --filter "FullyQualifiedName~Map3D|FullyQualifiedName~BuildingHeight"`(작업 전 기준선) — 통과 365 · 실패 0
- `dotnet test tests/GMaps.MapEdit.Tests --nologo --filter "FullyQualifiedName~Map3DEntryExitMore"`(수정 전) — 통과 35 · 실패 8(D-1 ×3 · D-3 ×2 · D-4 ×2(+cancel 은 대기 추가 뒤 실패 확인) · D-6 ×1)
- `dotnet test tests/GMaps.MapEdit.Tests --nologo --filter "FullyQualifiedName~Map3DEntryExitMore"`(수정 후) — 통과 47 · 실패 0
- `dotnet test tests/GMaps.MapEdit.Tests --nologo` — 통과 550 · 실패 0(기준선 503 + 새 47)
- `dotnet test tests/GMaps.Ui.Tests --nologo` — 통과 1581 · 실패 0
- `dotnet test tests/GMaps.MapNav.Tests --nologo` — 통과 60 · 실패 0(GMapCustomControl · 벤더 GMapControl 을 고쳐 함께 돌림)
- 정리: 시험이 쓰는 설정 파일(시험 bin Config\appsettings.json)은 시험 안에서 원본으로 되돌림 · Map3DSaved 정적 구독 해제 · Mouse.OverrideCursor 원복. WebView2 · 창 · 브라우저 · 서버 띄우지 않음(가짜 HttpMessageHandler · 가짜 IWebView2). 남은 msedgewebview2 프로세스 없음(작업 전부터 있던 것만).

## 바꾼 파일 목록(제품 · 시험 · 새 파일)
- 제품
  - `GMap.NET/GMap.NET/GMap.NET.WindowsPresentation/GMapControl.cs` — seam `IsCoreDragging` · `EndCoreDrag()` 추가(D-3)
  - `Ironwall.Dotnet.Libraries.GMaps.Ui/GMapCustoms/GMapCustomControl.cs` — ResetTransientInput 이 코어 끌기 정리 · InputGateText 가 코어 끌기 표시(D-3)
  - `Ironwall.Dotnet.Libraries.GMaps.Ui/ViewModels/Maps/MapViewModel.Map3D.cs` — EndMap3DBake 끝에 게이트 다시 큐(D-4)
  - `Ironwall.Dotnet.Libraries.GMaps.Ui/ViewModels/Maps/MapViewModel.Map3DAreas.cs` — 준비 중에도 처음 자리 이어 받기(D-6) · 거절되면 돌아갈 자리 · 표지 지움(D-1)
- 시험
  - `tests/GMaps.MapEdit.Tests/Map3DExitInputScenarioTests.cs` — 도우미 4개(Enter · Camera3D · From3D · AssertMap2DInputAlive) private → internal 만
- 새 파일
  - `tests/GMaps.MapEdit.Tests/Map3DEntryExitMoreScenarioTests.cs`(UTF-8 BOM · CRLF, 47 시험)
- 패치: `.harness/t-20261019-193448-b2cc/patches/A.patch`(제품 4 + 시험 1) · 새 파일 사본 `.harness/t-20261019-193448-b2cc/patches/A-new/tests/GMaps.MapEdit.Tests/Map3DEntryExitMoreScenarioTests.cs` — 둘 다 이 worktree 안(가드 때문에 COORD 에 못 씀)
