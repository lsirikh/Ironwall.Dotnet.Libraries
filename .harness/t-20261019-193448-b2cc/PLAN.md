# PLAN — 최근 기능 전수 시나리오 시험 (t-20261019-193448-b2cc)

## 목표
10-18~10-19 에 만든 · 고친 기능(A 3D 진입/이탈 · B 3D 영역 · C 3D 상호작용 · D 2D 편집 · E 오버레이 이미지 · F map-3d 품질)을
시나리오 표로 빠짐없이 정리 → 헤드리스로 전부 시험(없으면 새 시험, 실패면 결함 수정) → 헤디드 2단계 실기 목록(HEADED.md) → 전 프로젝트 결과 보고(REPORT.md).

## 작업 위치
- 라이브러리: 이 worktree
- map-3d: `artifacts/map3d`(원본 C:\workspace_python\map-3d 의 git clone, vendor 는 원본에서 복사, git 무시) → 바꾸면 `git -C artifacts/map3d diff > .harness/t-20261019-193448-b2cc/map3d.patch`
- 메인 앱: `artifacts/host/Dotnet.Monitoring.Solution`(clone) + `artifacts/host/Ironwall.Dotnet.Libraries` = 이 worktree 로의 junction(메인 앱의 `..\..\Ironwall.Dotnet.Libraries\` 참조가 이 worktree 를 보게). 바꾸면 host.patch
- 시험 로그: `.harness/t-20261019-193448-b2cc/logs/`

## 진행
- [x] 기준선: sln 10 dll 2888 통과(EXIT 0) · map-3d pytest 234 passed
- [x] 기준선: GMaps.Ui 1581 · MapEdit 503 · MapNav 60 · Housing 353 · MapAux 76 · PropertyPanel 436 — 모두 실패 0
- [x] 기준선: 메인 앱 Tests 887 통과 · 8 건너뜀 · 실패 0(`run-host-tests.ps1`)
- [x] 영역별 시나리오 지도(A~F, 읽기 전용 분석 5건) — 결함 의심 약 30건
- [x] 작업자 6명: A~E 는 각자 git worktree → `patches/<영역>.patch` + `patches/<영역>-new/`, F 는 artifacts/map3d → `map3d.patch`. 각자 `areas/<영역>.md`(시나리오 표 · 고친 결함 · 결정 · 헤디드 · 결과)
- [x] 패치 합치기(충돌 없음) → 전 프로젝트 시험 실패 0(logs/merge3-*). 메인 앱 복제본은 `.harness-worktrees/host` 로 옮김(artifacts 에 두면 Utils.Tests 소스 스캔에 잡힘)
- [x] SCENARIOS.md · DEFECTS.md · HEADED.md(`assemble.ps1` 로 areas/*.md 에서 생성) · REPORT.md
- [x] 최종: sln 2888 · GMaps 7개 실패 0 · map-3d 286 passed · 메인 앱 887 — REPORT.md

## 이어서 할 사람에게
- 작업자 규칙: `worker-rules.md`. 작업자가 끊겼으면 `areas/<영역>.md` · `patches/` 가 있는지 보고, 없으면 그 영역을 다시 맡긴다.
- 시험 명령: `dotnet test Ironwall.Dotnet.Libraries.sln` · `dotnet test tests/<GMaps.*>` · `powershell -File .harness/t-20261019-193448-b2cc/run-host-tests.ps1 -Log …` · map-3d: `cd artifacts/map3d; & C:\workspace_python\map-3d\.venv\Scripts\python.exe -m pytest -q -p no:cacheprovider`

## 결정 · 발견
- DECISIONS.md 참고. 발견: junction 을 worktree 안에 상주시키면 MapsHelpTests 가 무한 경로로 실패(우리 환경 문제, 결함 아님).

## 남은 일
- 헤디드 2단계(HEADED.md) · REPORT.md 「결정 필요」 12건은 사용자 결정.

## 부록 — 시나리오 표 전체(영역 보고서 areas/*.md 에서 모음)

상태: 통과(기존) = 이미 있던 시험 · 통과(새) = 이번에 쓴 시험 · 수정 후 통과 = 시험이 드러낸 결함을 고친 뒤 통과 · 헤디드 = HEADED.md · 결정 필요 = REPORT.md

### A 3D 진입/이탈

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


### B 3D 영역

| ID | 기능 | 시나리오 | 기대 | 시험 위치(file::method) | 상태 |
|---|---|---|---|---|---|
| B-01 | 그리기 | 「3D 영역 그리기」 누름 | 러버밴드 모드 · 3D 그리기 표지 · 크기 라벨 켜짐 | MapNav/Map3DAreaDrawScenarioTests::should_close_bake_panel_when_draw_area_pressed_again(모드 진입 단언) · MapNav/Map3DRotatedAreaScenarioTests::should_label_and_limit_by_rotated_sides_when_drawing | 통과(새) · 통과(기존) |
| B-02 | 그리기 | 회전 지도(30 · 45 · 90 · −60°)에서 러버밴드 | 회전 사각형 그대로(회전각 = 지도 방위, 가로 · 세로 = 화면 크기) · 본문 rect | MapNav/Map3DRotatedAreaScenarioTests::should_hand_rotated_rect_to_bake_panel_when_drawn_on_rotated_map · Ui/Map3DRotatedAreaTests::should_keep_drawn_rect_when_map_rotated | 통과(기존) |
| B-03 | 그리기 | 회전 없는 지도에서 러버밴드 | 북쪽 정렬 bbox, rect 없음(예전 본문) | MapNav/Map3DRotatedAreaScenarioTests::should_keep_north_aligned_bbox_when_drawn_on_unrotated_map · Ui/Map3DRotatedAreaTests::should_stay_north_aligned_bbox_when_map_not_rotated | 통과(기존) |
| B-04 | 그리기 | Enter = 지금 화면(0° · 45°) | 0° bbox / 45° 화면 1200×800 그대로 회전 영역 | MapNav/Map3DRotatedAreaScenarioTests::should_take_visible_screen_when_enter_pressed | 통과(기존) |
| B-05 | 그리기 | 회전 없음 + 디지털 줌(배율≠1) + Enter | ViewArea 를 배율만큼 줄인 bbox(가운데 같음) | MapNav/Map3DAreaDrawScenarioTests::should_take_bbox_shrunk_by_digital_zoom_when_enter_pressed_on_unrotated_map | 통과(새) |
| B-06 | 그리기 | 회전 지도 + 디지털 줌(17.5 · 16.5) + Enter | 화면 안쪽 네 모서리(가로 1200/s · 세로 800/s px 의 m), 가운데 2 px 안 | MapNav/Map3DAreaDrawScenarioTests::should_take_inner_visible_screen_scaled_by_digital_zoom_when_enter_pressed_on_rotated_map(×2) | 통과(새) |
| B-07 | 그리기 | 그리는 중 크기 라벨 | 회전 영역은 실제 변 길이, 2 km 넘으면 경고색 | MapNav/Map3DRotatedAreaScenarioTests::should_label_and_limit_by_rotated_sides_when_drawing · Ui/Map3DRotatedAreaTests::should_label_real_side_lengths_when_rotated_rect_drawn · Ui/Map3DGuideTests::should_warn_amber_when_drawn_area_exceeds_2km · should_show_plain_size_when_drawn_area_within_limit | 통과(기존) |
| B-08 | 그리기 | 놓을 때 LastAnchorCorners 가 null(회전 지도) | nw/se bbox 로 폴백, rect 없음, 판 열림 | MapNav/Map3DAreaDrawScenarioTests::should_fall_back_to_bbox_without_rect_when_last_anchor_corners_missing | 통과(새) |
| B-09 | 그리기 | 회전 변이 2 km 초과 | 판 안 열고 다시 그리기 모드 + "영역이 너무 큽니다" | MapNav/Map3DRotatedAreaScenarioTests::should_ask_to_redraw_when_rotated_side_too_large · Ui/Map3DRotatedAreaTests::should_check_max_size_on_rotated_sides · Ui/Map3DGuideTests::should_explain_reason_when_released_area_too_large | 통과(기존) |
| B-10 | 그리기 | 회전 변 정확히 2000 m / 2000.01 · 2000.5 m | 2000 = 허용(경고 없음 · 판 열림), 넘으면 다시 그리기 | Ui/Map3DAreaEdgeTests::should_allow_rotated_area_when_side_is_exactly_2000m · MapNav/Map3DAreaDrawScenarioTests::should_open_bake_panel_when_rotated_side_is_exactly_2000m | 통과(새) |
| B-11 | 그리기 | 4 px 미만 드래그(오클릭) | 무시, 그리기 모드 유지(GMapCustomControl.cs:1344 `ar.Width >= 4 && ar.Height >= 4`) | — | 헤디드(H-2) |
| B-12 | 그리기 | 그리기 중 ESC(창 PreviewKeyDown 처리기 그대로) → 늦게 온 놓기 | 모드 끝 · 크기 라벨 내림 · 판 없음, 늦은 OnAnchorAreaDrawn 도 판을 안 연다 | MapNav/Map3DAreaDrawScenarioTests::should_end_draw_mode_and_ignore_late_release_when_esc_pressed_while_drawing | 통과(새) |
| B-13 | 그리기 | 만들기 판 열린 채 다시 「영역 그리기」 | 판 닫힘 · 영역 버림 · 그리기 모드 | MapNav/Map3DAreaDrawScenarioTests::should_close_bake_panel_when_draw_area_pressed_again | 통과(새) |
| B-14 | 그리기 · 3D | 3D 보는 중 「영역 그리기」 | 먼저 2D, 그리는 동안 게이트 · 「3D 로 보기」로 안 들어감(이유 안내) | MapEdit/Map3DAreaScenarioTests::should_leave_3d_first_when_draw_area_pressed_while_3d_shown · MapEdit/Map3DEntryScenarioTests::should_refuse_button_and_area_view_with_reason_when_input_mode_active | 통과(새) · 통과(기존) |
| B-15 | 기하 | GIS RotRect ↔ map-3d geo.RotRect | 모서리 · bbox 같은 값 | Ui/Map3DRotRectParityTests::should_give_same_corners_and_bbox_as_map3d_rotrect · Ui/Map3DRotatedAreaTests::should_match_map3d_rotrect_numbers | 통과(기존) |
| B-16 | 기하 | 측지 진실과 비교 | 10 cm 안 | Ui/Map3DRotRectParityTests::should_match_geodesic_truth_within_decimeter | 통과(기존) |
| B-17 | 기하 | 그린 화면 사각형 재현 | 모서리 왕복 | Ui/Map3DRotRectParityTests::should_reproduce_drawn_screen_rect_when_map_rotated | 통과(기존) |
| B-18 | 기하 | 모서리 4개 아님 | 빈 영역 | Ui/Map3DRotatedAreaTests::should_return_empty_area_when_corners_missing | 통과(기존) |
| B-19 | 표시 | 줌 12 · 12.5 / 13 · 16 / 다시 12 | 13 미만 테두리 · 배지 Collapsed(Refresh(false)), 13 이상 Visible | MapEdit/Map3DAreaScenarioTests::should_collapse_area_outline_below_zoom_13_and_show_again_at_13 · Ui/Map3DGuideTests::should_show_area_outlines_when_zoom_13_or_more | 통과(새) · 통과(기존) |
| B-20 | 표시 | 회전 영역 테두리 | 지도 0° 에서 기울어진 네모, 영역 방위로 돌리면 반듯, 옛 영역도 4꼭짓점 | MapNav/Map3DRotatedAreaScenarioTests::should_draw_tilted_outline_and_find_scene_inside_rotated_rect · Ui/Map3DRotatedAreaTests::should_draw_area_outline_from_four_corners | 통과(기존) |
| B-21 | 표시 | 지도 회전(키 · 나침반 · 3D 동기) | 팬 없이 다음 프레임에 테두리 갱신 | MapEdit/Map3DStaleJobScenarioTests::should_redraw_outline_with_new_rotation_without_pan_when_bearing_changes(×3) | 통과(기존) |
| B-22 | 표시 | 연속 회전 45 스텝 | 프레임당 1회만 다시 그림 | MapEdit/Map3DStaleJobScenarioTests::should_redraw_outline_at_most_once_per_frame_when_rotating_continuously | 통과(기존) |
| B-23 | 게이트 | 화면 가운데가 감싸는 bbox 모서리(회전 사각형 밖) / 사각형 안(수동 · 자동) | 밖: 「3D 로 보기」 · 칩 · 줌 진입 없음 / 안: 보임, 자동은 줌 17 에서 진입 | MapEdit/Map3DAreaScenarioTests::should_use_rotated_rect_not_bbox_when_center_in_bbox_corner(×2) | 통과(새) |
| B-24 | 게이트 | 회전 영역 모서리 바로 안/밖 | 회전 사각형으로 판정 | Ui/Map3DRotatedAreaTests::should_judge_inside_by_rotated_rect_near_corners · MapNav/Map3DRotatedAreaScenarioTests::should_draw_tilted_outline_and_find_scene_inside_rotated_rect | 통과(기존) |
| B-25 | 게이트 | 옛 장면(rect 없음) | bbox 로 판정(경계 포함) | Ui/Map3DRotatedAreaTests::should_judge_old_scene_by_bbox_when_no_rect · MapEdit/Map3DEntryScenarioTests::should_enter_only_by_auto_zoom_and_show_button_only_inside_ready_area_when_gate_evaluated(OnEdge 행) | 통과(기존) |
| B-26 | 게이트 | 새 영역이 옛 영역과 겹침 | 새 장면이 먼저 잡힘 | MapEdit/Map3DStaleJobScenarioTests::should_pick_newest_scene_first_when_bake_finishes_over_old_overlapping_area | 통과(기존) |
| B-27 | 해석 | rect 해석 경계(width 0 · 음수, rotation 0 · ±360 · 720, −60, −360.5, 370, 정수 값, 문자열 · 불리언 · 빠짐 · NaN · 배열 · null) | 크기 ≤0 · 회전 360 배수 · 수 아님 = null, −60→300, 정수 해석 | Ui/Map3DAreaEdgeTests::should_return_null_when_rect_size_is_zero_or_negative · should_return_null_when_rotation_is_multiple_of_360 · should_wrap_rotation_into_0_360_when_rect_parsed · should_read_integer_values_when_rect_numbers_are_integers · should_return_null_when_rect_value_is_not_a_number_or_missing | 통과(새) |
| B-28 | 영역 보기 | 회전 영역 「이 영역 3D 로 보기」 → 3D 펼침 | enter-view 방위 = rect 회전각(2D 회전이 막혀 있어도) | MapEdit/Map3DAreaScenarioTests::should_open_area_view_with_rect_rotation_when_rotated_area_viewed(Reveal3D 입력 + 같은 식) · Ui/Map3DAreaEdgeTests::should_open_3d_at_rect_rotation_regardless_of_2d_bearing_when_area_view · should_open_3d_at_2d_bearing_when_area_not_rotated | 통과(새) — PostJson 대체(결정 4) |
| B-29 | 영역 보기 | 회전 영역 보기 2D | 사각형 가운데 · 2D 도 회전각, 나가면 들어오기 전 2D | MapNav/Map3DRotatedAreaScenarioTests::should_center_and_rotate_map_when_area_view_opens_rotated_area · Ui/Map3DRotatedAreaTests::should_enter_area_view_with_rect_rotation · should_center_area_view_on_rect_center | 통과(기존) |
| B-30 | 영역 보기 | 옛 영역 보기 | 가운데, 지도 방위 그대로 | MapNav/Map3DRotatedAreaScenarioTests::should_center_without_rotating_when_area_view_opens_old_area | 통과(기존) |
| B-31 | 서버 | rect 보내기 · 읽기 왕복 | 회전이면 rect + bbox, 아니면 rect 없음 | Ui/Map3DRotatedAreaTests::should_send_rect_with_bbox_when_rotated_and_omit_when_not · should_post_rect_and_read_rect_back_from_server | 통과(기존) |
| B-32 | 만들기 판 | 영역 안 이미지 있음 · 방식 4종(tiles/photo/mask/extract) [만들기] | 라디오 보임 · 마지막 값 미리 골라 둠 · 본문 overlay_mode(없음/composite/composite/extract) · overlays[].mask(-/false/true/true) · id 77 · image_path · rotation 330 · opacity 0.8 · enter_zoom 18 | MapEdit/Map3DBakeOptionsScenarioTests::should_post_overlay_mode_and_mask_per_chosen_mode_when_images_in_area(×4) | 통과(새) |
| B-33 | 만들기 판 | 영역 안 이미지 없음(밖 · 숨김만) | 라디오 숨김 · tiles · overlay_mode/overlays 없음 · 기본 줌 17 | MapEdit/Map3DBakeOptionsScenarioTests::should_hide_mode_radios_and_post_tiles_body_when_no_image_in_area | 통과(새) |
| B-34 | 만들기 판 | 「다시 만들기」 판 [취소] → 새 영역 만들기 | POST 0 · 다시 만들기 표지(_map3DBakeReplaces · _map3DBakeOptionsSourceId) 비움 · 뒤 굽기가 옛 영역을 안 지움 | MapEdit/Map3DBakeOptionsScenarioTests::should_post_nothing_and_clear_rebake_marks_when_bake_panel_cancelled | 수정 후 통과(잠재 누수) |
| B-35 | 만들기 판 | 회전 영역 — bbox 모서리(사각형 밖)에만 이미지 | 지금 동작: 판이 1개로 셈 · 라디오 보임 | MapEdit/Map3DBakeOptionsScenarioTests::should_count_image_in_bbox_corner_outside_rotated_rect_when_bake_panel_opens | 통과(새) — 결정(의도된 근사, 결정 2) |
| B-36 | 만들기 판 | 굽는 중 [만들기] | "이미 3D 를 만드는 중입니다" · POST 1회뿐 · 판 닫힘 · 「영역 그리기」 CanExecute false | MapEdit/Map3DAreaScenarioTests::should_refuse_second_bake_and_disable_draw_when_already_baking | 통과(새) |
| B-37 | 만들기 판 | 판 기본 방식 | 이미지 없음 tiles / 있음 마지막 값(없으면 extract, 옛 composite → extract) | Ui/Map3DBakeTests::should_bake_tiles_without_asking_when_no_overlay_image_in_area · should_preselect_last_mode_when_overlay_images_are_in_area · should_preselect_extract_…(2) · should_normalize_saved_overlay_mode_when_settings_load · B-32 | 통과(기존) · 통과(새) |
| B-38 | 만들기 판 | 3D 줌 고르기 | 15~19 0.5 단위 · 가까운 값 · enter_zoom | Ui/Map3DAreaZoomTests::should_offer_half_steps_from_15_to_19_when_choices_listed · should_pick_nearest_choice_when_default_zoom_given · should_add_enter_zoom_to_body_when_zoom_chosen · B-32 | 통과(기존) · 통과(새) |
| B-39 | 만들기 판 | 이미지 겹침 판정(회전 · 가장자리 · 바깥 · 무한) | 회전 감싸는 상자로 겹침 | Ui/Map3DBakeTests::should_intersect_*(5) · should_widen_extent_when_image_is_rotated · should_not_intersect_when_bounds_are_not_finite | 통과(기존) |
| B-40 | 만들기 판 | TIF · 50 MB 초과 이미지 | PNG/JPG 캐시로 바꿔 보냄 | Ui/Map3DBakeTests::should_convert_image_when_server_cannot_take_original · Housing/Map3DImageCacheTests | 통과(기존) |
| B-41 | 만들기 판 | 영역 밖 · 숨긴 이미지 | 넣지 않음 | MapEdit/Map3DBakeOptionsScenarioTests::should_hide_mode_radios_and_post_tiles_body_when_no_image_in_area | 통과(새) |
| B-42 | 만들기 판 | 놓은 뒤 원본 파일이 지워진 이미지 | 뺌(0개 · 라디오 숨김) | MapEdit/Map3DBakeOptionsScenarioTests::should_leave_out_image_whose_file_is_gone_when_bake_panel_opens | 통과(새) |
| B-43 | 만들기 판 | 「3D 줌 바꾸기…」 | 같은 판 · 제목 "3D 줌 바꾸기" · [바꾸기] · 방식 숨김 · 지금 줌, [취소] 요청 0, [바꾸기] PATCH enter_zoom · POST 없음 · 장면 캐시 줌 | MapEdit/Map3DBakeOptionsScenarioTests::should_open_zoom_only_panel_and_patch_zoom_when_change_zoom_chosen · Ui/Map3DAreaZoomTests::should_patch_enter_zoom_and_return_true_when_server_stores_it 외 2 | 통과(새) · 통과(기존) |
| B-44 | 진행 판 | 「만들기 취소」 보임 조건 | 굽기 진행 표시 중 · 작업 id 있음 · 보내는 중 아님 · 옛 서버 아님 | MapEdit/Map3DCancelBakeScenarioTests::should_show_cancel_button_only_while_bake_progress_shown_when_baking · Ui/Map3DCancelBakeTests::should_show_cancel_button_only_when_baking_with_job_and_not_cancelling | 통과(기존) |
| B-45 | 취소 | 취소 결과 "already" | 진행 조회 계속 · 취소 안내 없음 · done → "3D 준비됨" | MapEdit/Map3DCancelBakeScenarioTests::should_keep_polling_without_cancel_notice_when_cancel_result_already · m3d/test_cancel.py::should_answer_harmlessly_when_job_already_done | 통과(새) |
| B-46 | 취소 | 취소 결과 "missing" | 취소로 처리(조회 멈춤 · 만들기 전 상태 · 장면 없음) | MapEdit/Map3DCancelBakeScenarioTests::should_treat_as_cancelled_when_cancel_result_missing · m3d/test_cancel.py::should_answer_harmlessly_when_job_missing_or_id_invalid | 통과(새) |
| B-47 | 취소 | 다른 곳에서 취소(status cancelled) | 진행 조회 끝 · 취소 안내 | MapEdit/Map3DCancelBakeScenarioTests::should_end_bake_with_cancelled_notice_when_server_reports_cancelled · Ui/Map3DCancelBakeTests::should_mark_job_cancelled_when_status_cancelled | 통과(기존) |
| B-48 | 취소 | 「만들기 취소」 | POST cancel 1회 · 진행 판 닫힘 · 조회 멈춤 · 다시 그리기 가능 | MapEdit/Map3DCancelBakeScenarioTests::should_send_cancel_and_return_to_before_bake_state_when_cancel_pressed · Ui/Map3DCancelBakeTests::should_post_cancel_and_read_result_when_server_supports_cancel | 통과(기존) |
| B-49 | 취소 | 다시 만들기 취소 → 그 영역 3D | 옛 장면 그대로 · DELETE 없음 · 「3D 로 보기」 → 옛 장면 | MapEdit/Map3DCancelBakeScenarioTests::should_enter_old_scene_after_rebake_cancelled · should_keep_previous_scene_when_rebake_cancelled · m3d/test_cancel.py::should_keep_previous_scene_when_rebake_cancelled | 통과(새) · 통과(기존) |
| B-50 | 지우기 | 다시 만드는 중인 그 영역 「지우기…」 | 메뉴에 지우기 없음(「만들기 취소」 맨 위) · 굽기 전에 연 메뉴 · 확인 창으로 와도 안 지움 + "만드는 중에는 지울 수 없습니다 — 「만들기 취소」 뒤 지우세요" · 끝난 뒤 정상 지우기 | MapEdit/Map3DAreaScenarioTests::should_refuse_delete_of_area_being_rebaked_and_allow_after_bake · should_not_open_delete_confirm_when_area_being_rebaked | 수정 후 통과(D1) |
| B-51 | 지우기 | 굽는 중 다른 영역 지우기 | 그 영역만 빠짐 · 굽기 계속 · 늦은 목록도 안 되살림 | MapEdit/Map3DAreaScenarioTests::should_delete_other_area_and_keep_baking_when_area_deleted_during_bake | 통과(새) |
| B-52 | 취소 | 「만들기 취소」 연타 · 끝난 뒤 누름 | 요청 1회 · 보내는 중 단추 숨김 | MapEdit/Map3DCancelBakeScenarioTests::should_send_one_cancel_when_pressed_repeatedly · m3d/test_cancel.py::should_be_idempotent_when_cancelled_twice | 통과(기존) |
| B-53 | 다시 만들기 | 굽는 중 「다시 만들기」 | "이미 3D 를 만드는 중입니다" · 판 안 열림 · POST 없음 | MapEdit/Map3DAreaScenarioTests::should_refuse_second_bake_and_disable_draw_when_already_baking | 통과(새) |
| B-54 | 취소 · 경쟁 | 취소 응답 대기 중 진행 조회가 done | 새 장면 등록 · "3D 준비됨" · 늦은 취소 응답이 덮지 않음 | MapEdit/Map3DCancelBakeScenarioTests::should_register_new_scene_and_ignore_late_cancel_answer_when_done_arrives_first · m3d/test_cancel.py::should_cancel_at_last_check_when_request_races_glb_step | 통과(새) |
| B-55 | 취소 | too_late | 계속 만듦 · 안내 · 끝나면 준비됨 | MapEdit/Map3DCancelBakeScenarioTests::should_keep_baking_when_cancel_too_late · m3d/test_cancel.py::should_not_cancel_when_writing_glb | 통과(기존) |
| B-56 | 취소 | 옛 서버(404) | "지원하지 않습니다" · 이번 굽기 단추 숨김 | MapEdit/Map3DCancelBakeScenarioTests::should_tell_unsupported_and_hide_button_when_old_server · Ui/Map3DCancelBakeTests::should_report_unsupported_when_old_server_has_no_cancel | 통과(기존) |
| B-57 | 취소 | 취소가 서버에 닿지 않음 | "꺼져" · 다시 누를 수 있음 · 계속 만듦 | MapEdit/Map3DCancelBakeScenarioTests::should_keep_baking_and_tell_when_cancel_cannot_reach_server | 통과(기존) |
| B-58 | 취소 | 취소 직후(서버 아직 멈추는 중) 새 만들기 | 새 굽기 정상 · 앞 조회가 안 건드림 | MapEdit/Map3DCancelBakeScenarioTests::should_allow_new_bake_right_after_cancel_when_server_still_stopping · m3d/test_cancel.py::should_bake_again_when_same_area_requested_after_cancel | 통과(기존) |
| B-59 | 다시 만들기 | 다 만든 뒤 옛 장면 DELETE 405 · 닿지 않음 / 404 | 옛 영역 유지 · 경고 로그 · 새 장면이 앞 / 404 는 정보 로그 · 목록이 판단 | MapEdit/Map3DAreaScenarioTests::should_keep_old_area_and_warn_when_old_scene_delete_fails_after_rebake(×2) · should_leave_old_area_to_server_list_when_old_scene_already_gone_after_rebake | 통과(새) |
| B-60 | 지우기 | DELETE 가 서버에 닿지 않음(2D · 보던 3D) | "꺼져" 안내 · 영역 · 장면 유지 · 되살리기 막기 목록에 안 넣음 · 「3D 로 보기」 다시 가능 / 보던 3D 는 이미 2D(D6) | MapEdit/Map3DAreaScenarioTests::should_keep_area_and_tell_server_off_when_delete_cannot_reach_server(×2) | 통과(새) — D6 결정 필요(결정 3) |
| B-61 | 다시 만들기 | 회전 영역 다시 만들기 | POST rect 같은 회전각 · 크기 · 가운데, bbox == rect.Bbox(), enter_zoom = 그 영역 줌, 새 장면도 같은 rect | MapEdit/Map3DAreaScenarioTests::should_post_same_rect_when_rotated_area_rebaked | 통과(새) |
| B-62 | 다시 만들기 | 다 만들기(2D · 옛 장면 보던 3D) | 옛 job 참조 0 · 옛 장면 DELETE · 같은 자리 = 새 장면 · 늦은 목록이 안 되살림 | MapEdit/Map3DStaleJobScenarioTests::should_drop_every_reference_to_old_job_and_delete_it_when_rebake_completes(×2) | 통과(기존) |
| B-63 | 지우기 | 지우기 확인(2D · 3D) | 호스트 내려놓음 · 참조 0 · 늦은 조회 · 목록이 안 되살림 · 게이트 진입 없음 | MapEdit/Map3DStaleJobScenarioTests::should_unload_host_and_never_revive_area_when_delete_confirmed(×2) · m3d/test_server.py::should_delete_only_finished_job_scenes_when_gis_asks | 통과(기존) |
| B-64 | 목록 | 보던 장면이 서버 목록에서 빠짐 | 2D + 안내 · 같은 줌에서 곧바로 재진입 없음 | MapEdit/Map3DStaleJobScenarioTests::should_leave_to_2d_with_notice_when_shown_job_disappears_from_server_list(×2) | 통과(기존) |
| B-65 | 조합 | 회전 영역 + 건물 높이 덮어쓰기 + 다시 만들기 | GIS 는 같은 rect 로 다시 굽기 요청(B-61), 서버가 사각형 안 덮어쓰기만 다시 붙임 | MapEdit/Map3DAreaScenarioTests::should_post_same_rect_when_rotated_area_rebaked · m3d/test_rotated_area.py::should_ignore_height_overrides_outside_rect_and_reset_only_inside · m3d/test_heights.py::should_reattach_override_after_rebake_even_if_outline_moves | 통과(새) · map-3d 기존 |
| B-66 | 지우기 | 이미 서버에서 지워진 영역(DELETE 404) | "이미 지워진 3D 영역입니다" · 표시에서 뺌 | MapEdit/Map3DAreaScenarioTests::should_remove_area_and_say_already_deleted_when_delete_returns_404 · Ui/Map3DClientTests::should_return_false_when_deleted_scene_already_gone | 통과(새) · 통과(기존) |
| B-67 | 3D error | reason=server-unreachable(준비 중) / job 없는 scene-missing | 2D · "3D 서버(map-3d)가 꺼져 있습니다" · 영역 유지 / 무시(상태 그대로 · 목록 요청 없음) | MapEdit/Map3DAreaScenarioTests::should_tell_server_off_and_keep_area_when_viewer_reports_server_unreachable | 통과(새) |
| B-68 | 3D error | 다시 만드는 원본 job 이 scene-missing | 원본만 내려놓음 · 굽기 계속 · 끝나면 새 장면 · 늦은 목록이 원본 안 되살림 | MapEdit/Map3DAreaScenarioTests::should_keep_baking_and_register_new_scene_when_rebake_source_reported_missing | 통과(새) |
| B-69 | 3D error | scene-missing(Shown · Pending · 1×1) | 그 job 모든 상태에서 뺌 · 안내 · 목록 새로 고침 | MapEdit/Map3DStaleJobScenarioTests::should_retire_job_and_refresh_list_when_viewer_reports_scene_missing(×3) · m3d/test_viewer_load.py::should_report_missing_or_broken_scene_in_plain_words_when_checked_with_node | 통과(기존) |
| B-70 | 3D error | scene-broken | 진입 취소 · 영역 유지 | MapEdit/Map3DStaleJobScenarioTests::should_cancel_entry_without_retiring_area_when_viewer_reports_broken_scene | 통과(기존) |
| B-71 | 3D error | error 메시지 해석 · 내려놓은 페이지의 늦은 ready | 해석 · 늦은 ready 무시 · 실패 표지 | MapEdit/Map3DStaleJobScenarioTests::should_parse_error_message_and_mark_host_failed_but_ignore_stale_ready | 통과(기존) |
| B-72 | 영역 메뉴 | 다시 만드는 영역 항목 · 새 영역 굽기 중 다른 영역 | 그 영역만 「만들기 취소」 맨 위 · "다시 만드는 중", 끝나면 원래대로 | MapEdit/Map3DCancelBakeScenarioTests::should_put_cancel_on_rebaking_area_only_when_rebake_runs · should_not_put_cancel_on_other_areas_when_new_area_baking · should_put_cancel_first_instead_of_rebake_when_area_item_baking | 통과(기존) |
| B-73 | 만들기 실패 | POST 가 서버에 닿지 않음 | "3D 서버(map-3d)가 꺼져 있습니다" · 만들기 전 상태 · 장면 없음 | MapEdit/Map3DAreaScenarioTests::should_tell_reason_and_return_to_before_bake_when_bake_fails(Unreachable) · Ui/Map3DClientTests::should_throw_unavailable_when_server_refuses_connection | 통과(새) · 통과(기존) |
| B-74 | 만들기 실패 | 진행 중 status error | "3D 만들기 실패: {message}" · 만들기 전 상태 | MapEdit/Map3DAreaScenarioTests::should_tell_reason_and_return_to_before_bake_when_bake_fails(JobError) | 통과(새) |
| B-75 | 만들기 실패 | POST 400 거절(2 km 초과 등) | "3D 를 만들 수 없습니다: {detail}" | MapEdit/Map3DAreaScenarioTests::should_tell_reason_and_return_to_before_bake_when_bake_fails(Rejected) · Ui/Map3DClientTests::should_throw_request_exception_with_detail_when_bbox_too_large | 통과(새) · 통과(기존) |
| B-76 | map-3d | 장면 밖 오버레이 숨김 · 타원체 좌표 | — | (F 작업자 — map-3d 쪽) | 미시험(F 담당) |


### C 3D 상호작용
상태: 통과(기존) · 통과(새) · 수정 후 통과 · 헤디드 · 결정 필요 · 미시험(이유). 시험 위치 약칭: `UiT`=tests/GMaps.Ui.Tests, `ME`=tests/GMaps.MapEdit.Tests, `AUX`=tests/GMaps.MapAux.Tests, `HOU`=tests/GMaps.Housing.Tests, `F`=COORD\artifacts\map3d\tests(F 작업자).

### A. 3D pick 해석 · 분배
| ID | 기능 | 시나리오 | 기대 | 시험 위치(file::method) | 상태 |
|---|---|---|---|---|---|
| C-01 | pick 해석 | 마커 click 메시지 | action · kind · id · screen 해석 | UiT/Map3DOverlayTests::should_parse_pick_when_marker_clicked | 통과(기존) |
| C-02 | pick 해석 | kind 또는 id 없음 | 빈 곳 pick(kind=id=null) | UiT/Map3DOverlayTests::should_parse_empty_pick_when_kind_or_id_missing | 통과(기존) |
| C-03 | pick 해석 | 모르는 action | 버림(null) | UiT/Map3DOverlayTests::should_return_null_when_pick_action_unknown | 통과(기존) |
| C-04 | pick 해석 | line · polygon · image 등 새 kind | 해석 | UiT/Map3DOverlayV2Tests::should_parse_pick_when_new_kind_clicked | 통과(기존) |
| C-05 | pick 해석 | track(문자열 id) | TrackId 에 담고 Id=null | UiT/Map3DOverlayV2Tests::should_parse_track_id_as_string_when_track_picked | 통과(기존) |
| C-06 | pick 해석 | track id 없음 | 빈 곳 | UiT/Map3DOverlayV2Tests::should_parse_empty_pick_when_track_id_missing | 통과(기존) |
| C-07 | 키 분리 | 종류가 다른 같은 id | 다른 키 | UiT/Map3DOverlayTests::should_separate_keys_by_kind_when_ids_collide | 통과(기존) |
| C-08 | 마커 우선 고르기(3D 쪽) | 마커와 건물이 겹친 자리 클릭 | 마커가 먼저(건물 메시지 안 감) · 가리지 않게 그림 | F/overlay_pick_check.mjs ← test_server.py::should_pick_overlays_before_buildings_and_draw_them_unoccluded_when_clicked | F 담당(연결) |
| C-09 | 건물 고르기(3D 쪽) | 건물 클릭 · 미리보기 · 윤곽 | building 메시지 · preview · select 처리 | F/building_check.mjs ← test_heights.py::should_pick_preview_and_highlight_buildings_when_checked_with_node | F 담당(연결) |

### B. 클릭
| ID | 기능 | 시나리오 | 기대 | 시험 위치 | 상태 |
|---|---|---|---|---|---|
| C-10 | 빈 곳 click | "지도에서 보기" 강조 중 3D 빈 곳 click | 강조 해제(ActiveRequestId=null) · 3D 유지 · 로그 `pick click kind=- id=- → 빈 곳` | ME/Map3DPickScenarioTests::should_clear_locate_highlight_and_close_card_when_empty_place_clicked_in_3d | 통과(새) |
| C-11 | 빈 곳 dblclick · context | 빈 곳 더블클릭 · 우클릭 | 아무것도 안 함(카드 그대로) | 같은 시험 | 통과(새) |
| C-12 | 판 열린 채 빈 땅 클릭 | 건물 높이 판 열림 → 3D 빈 땅 클릭 | 3D 는 윤곽만 지우고(view.js selectBuilding(null)) GIS 판은 그대로 — 판과 윤곽이 어긋남(체감 확인) | — | 헤디드 |
| C-13 | 3D 아닐 때 | 2D 에서 pick 메시지 | 무시(통지 0) | ME/Map3DPickScenarioTests::should_raise_marker_double_click_once_for_camera_and_group_report_for_fence_and_ignore_when_not_3d | 통과(새) |
| C-14 | pick 예외 격리 | pick 처리 중 예외 | Error 로그 `[Map3D] pick 처리 실패(격리)` · 3D 유지 | — | 미시험(try/catch 코드 확인만, 시나리오 시험 장치는 Error 0 을 단언) |
| C-15 | 이미지 pick | 지도 CustomImages(음수 id) click | OnMapImageClicked(선택) | — | 미시험(오버레이 이미지 다리 = 영역 E/B 범위) |
| C-16 | 마커 click | 3D 마커 click | 지도 OnMarkerClicked 통지 → 조립 카드 경로 | ME/Map3DPickScenarioTests::should_open_card_in_2d_for_camera_click_and_replace_it_when_another_marker_clicked_in_3d | 통과(새) |
| C-17 | 편집 모드 | 편집 · 독점 입력 모드 | 3D 자체가 안 열림(pick 없음) | UiT/Map3DOverlayTests::should_block_3d_when_edit_or_exclusive_mode | 통과(기존) |

### C. 더블클릭 · 우클릭
| ID | 기능 | 시나리오 | 기대 | 시험 위치 | 상태 |
|---|---|---|---|---|---|
| C-18 | 카메라 dblclick | 3D 카메라 마커 dblclick | TriggerMarkerDoubleClicked → OnMarkerDoubleClicked 정확히 1회 | ME/Map3DPickScenarioTests::should_raise_marker_double_click_once_for_camera_and_group_report_for_fence_and_ignore_when_not_3d | 통과(새) |
| C-19 | 3D 우클릭 메뉴 자리 | pick context + screen(320,210) | 메뉴 Placement=Relative · PlacementTarget=지도 · Offset=(320,210) · 로그 `3D 위 우클릭 메뉴 열기 — 항목 n개 @(320,210)` · `_map3DMenuAt` 원복 | AUX/Map3DContextMenuScenarioTests::should_place_menu_at_pick_point_relative_to_map_when_marker_right_clicked_in_3d (MenuTrap — 창 없음) | 통과(새) |
| C-20 | 추적 마커 | track click · dblclick · context | 동작 없음 · 오류 없음 · 로그 `kind=track id=… → 동작 없음(2D 와 같음)` | ME/Map3DPickScenarioTests::should_do_nothing_for_track_pick_and_not_open_card_for_locked_marker | 통과(새) |
| C-21 | 잠긴 마커 | 잠긴 카메라 click | 카드 안 열림 · 3D 유지 | 같은 시험 | 통과(새) |
| C-22 | 구역 dblclick | pidsGroup dblclick | 조치보고(TryOpenForGroup 1회) · 마커 더블클릭 통지 0 | ME/Map3DPickScenarioTests::should_raise_marker_double_click_once_… | 통과(새) |
| C-23 | 없는 id | 지도에 없는 id dblclick | 빈 곳 처리(통지 없음) | 같은 시험 | 통과(새) |
| C-24 | 카메라 dblclick 2D 복귀 | 호스트 none · hwnd | 2D 복귀 · 로그 `[Map3D] 2D 복귀(camera-popup)` · 진입 보류 | ME/Map3DPickScenarioTests::should_return_to_2d_with_camera_popup_cause_when_camera_double_clicked_in_window_covering_3d(none/hwnd) | 통과(새) |
| C-24b | 카메라 dblclick 3D 유지 | 호스트 composition | 3D 유지(상자가 3D 위에) | UiT/Map3DScreenTests::should_stay_in_3d_when_window_opens_over_composition_host(순수 규칙) — VM 경로는 WebView2CompositionControl 필요 | 통과(기존, 규칙만) · 헤디드 |
| C-25 | 실제 더블클릭 | 3D 화면에서 400 ms 안 두 번 | dblclick 1회(click 두 번 아님) | — | 헤디드 |
| C-26 | 옛 3D 페이지 우클릭 | screen 없는 context | MousePoint 로 떨어짐(오류 없음) | AUX/Map3DContextMenuScenarioTests::should_place_menu_at_pick_point_… | 통과(새) |
| C-27 | 메뉴 실제 자리 | 3D 우클릭(DPI · 창 위치) | 누른 자리에 뜸 | — | 헤디드 |

### D. 카메라 상자(3D 따라가기)
| ID | 기능 | 시나리오 | 기대 | 시험 위치 | 상태 |
|---|---|---|---|---|---|
| C-28 | 상자 연 채 나가기 | 3D 에서 카메라 상자 열고 「나가기」 | 상자 유지 · 좌표 유한 | ME/Map3DPickScenarioTests::should_keep_camera_box_with_finite_position_when_3d_exited_with_box_open | 통과(새) |
| C-29 | 상자 자리 | 마커 3D 자리 + 2D 오프셋 | 같은 자리 관계 | UiT/Map3DScreenTests::should_put_popup_at_3d_marker_plus_2d_offset_when_view_matches · should_keep_offset_when_2d_and_3d_view_match | 통과(기존) |
| C-30 | 따라가기 | 3D 카메라 이동 | 상자가 마커 따라감 | UiT/Map3DScreenTests::should_follow_marker_when_3d_camera_moves_marker_on_screen | 통과(기존) |
| C-31 | 줌 | 3D 한 단계 확대 | 오프셋 2배 | UiT/Map3DScreenTests::should_double_offset_when_3d_zoomed_in_one_level | 통과(기존) |
| C-32 | 회전 | 3D 방위 오른쪽 | 오프셋 반시계 회전 | UiT/Map3DScreenTests::should_rotate_offset_counterclockwise_when_3d_bearing_turned_right | 통과(기존) |
| C-33 | 북쪽 넘김 | 359°↔1° | 같은 처리 | UiT/Map3DScreenTests::should_treat_wrapped_bearings_alike_when_crossing_north | 통과(기존) |
| C-34 | 3D 에서 끌어 놓기 | 상자 끌기 → 2D 앵커 | 2D 로 되돌린 앵커 저장 | UiT/Map3DScreenTests::should_restore_2d_anchor_when_popup_dragged_in_3d · should_restore_2d_offset_when_3d_drag_converted_back | 통과(기존) |
| C-35 | 비유한 값 | NaN · ∞ view | 무시 | UiT/Map3DScreenTests::should_ignore_non_finite_view_values_when_computing_offset | 통과(기존) |
| C-36 | 지켜볼 목록 | 상자 · 카드 열림/없음 | overlay.watch(종류 · id · 위경도) / 빈 목록 | UiT/Map3DScreenTests::should_send_marker_kind_id_and_geo_when_watch_built · should_send_empty_list_when_no_window_open | 통과(기존) |
| C-37 | overlay.screen | 좌표 · 빈 항목 | 해석 / 빈 목록 | UiT/Map3DScreenTests::should_parse_screen_points_when_overlay_screen_arrives · should_return_empty_screens_when_items_missing | 통과(기존) |
| C-38 | 관성 회전 떨림 | 관성 회전 · 줌 · 메시지 순서 바뀜 · 일부 빠짐 | 앞뒤 떨림 없음 | UiT/Map3DPopupJitterTests::should_have_no_back_and_forth_when_inertia_rotation_after_fix · should_not_jitter_when_message_order_varies · should_never_move_backwards_when_some_screen_messages_missing · should_stay_smooth_when_rotating_around_marker_itself · should_stay_smooth_when_rotating_and_zooming_with_inertia (모형) | 통과(기존) · 헤디드(체감) |
| C-39 | 마커가 카메라 뒤 | 좌표 없음 → 다시 보임 | 마지막 자리 → 튐 없이 재개 | UiT/Map3DPopupJitterTests::should_keep_last_spot_then_resume_without_jump_when_marker_goes_behind_camera | 통과(기존) |
| C-40 | 끄는 중 상자 | 관성 중 상자 끌기(IsWindowDragging) · 오프셋 캐시 | 끌기만 따름 | UiT/Map3DPopupJitterTests::should_follow_drag_only_when_dragging_box_during_3d_inertia(모형) — 실제 TryPlaceCameraPopupsIn3D 는 composition 호스트 필요 | 통과(기존, 모형) · 헤디드 |
| C-41 | 좌표 변환 | 3D CSS px → 지도 칸 | 호스트 원점 + ZoomFactor | UiT/Map3DScreenTests::should_add_host_origin_and_zoom_factor_when_converting_to_outer | 통과(기존) |
| C-42 | 3D 쪽 화면점 보고 | watch → screen | 바뀐 것만 보고 | F/overlay_screen_check.mjs ← test_server.py::should_report_gis_window_screen_points_when_watched | F 담당(연결) |
| C-43 | 프레임 view | view 있음 · 없음/잘못됨 | 해석 / null | UiT/Map3DPopupJitterTests::should_parse_frame_view_when_overlay_screen_carries_view · should_have_no_frame_view_when_view_missing_or_bad | 통과(기존) |
| C-44 | view 우선순위 | 프레임 view > 마지막 카메라 > 2D | 그 순서 | UiT/Map3DPopupJitterTests::should_prefer_frame_view_then_last_camera_then_2d_when_offset_view_chosen · should_place_from_2d_change_only_when_no_frame_view | 통과(기존) |
| C-45 | 관성 멈춤 | 멈춘 뒤 | 기존 규칙 자리에 착지 | UiT/Map3DPopupJitterTests::should_land_on_existing_rule_spot_when_inertia_stops · should_equal_existing_rule_when_offset_measured_once_as_base | 통과(기존) |

### E. 조립 카드
| ID | 기능 | 시나리오 | 기대 | 시험 위치 | 상태 |
|---|---|---|---|---|---|
| C-46 | 카드 열기 | 3D 카메라(부품 축 있음) click, 호스트 none | 카드 열림 · 2D 복귀(`2D 복귀(component-card)`) | ME/Map3DPickScenarioTests::should_open_card_in_2d_for_camera_click_and_replace_it_when_another_marker_clicked_in_3d | 통과(새) |
| C-47 | 카드 닫기 | 카드 열린 채 3D 빈 곳 click | 카드 닫힘 | ME/Map3DPickScenarioTests::should_clear_locate_highlight_and_close_card_when_empty_place_clicked_in_3d | 통과(새) |
| C-48 | 카드 교체 | 카드 연 채 다시 3D → 다른 마커 click | 새 마커로 교체 · 옛 마커 IsComponentCardOpen=false | ME/Map3DPickScenarioTests::should_open_card_in_2d_… | 통과(새) |
| C-49 | 못 여는 심볼 | 부품 축 없는 마커 click | 옛 카드 걷음 · 3D 유지 | 같은 시험 | 통과(새) |
| C-50 | 3D 위 층 카드 | composition 위 층 배치 | 2D 와 같은 배치 규칙 | HOU/Map3DWindowCardTests::should_place_card_beside_3d_marker_with_2d_rule_when_overlay_layer_active | 통과(기존) |
| C-51 | 따라가기 · 숨김 | 화면점 이동 / 좌표 모름 | 따라감 / Hidden | HOU/Map3DWindowCardTests::should_follow_3d_marker_when_screen_point_moves · should_hide_card_until_3d_screen_point_known_when_anchor_unknown | 통과(기존) |
| C-52 | 3D↔2D 전환 | 전환 · 닫기 | 같은 VM 유지 · 층에서 제거 | HOU/Map3DWindowCardTests::should_keep_same_card_view_model_when_switching_between_3d_and_2d · should_remove_card_from_3d_layer_when_closed | 통과(기존) |

### F. 건물 높이 — 순수 규칙 · 창구
| ID | 기능 | 시나리오 | 기대 | 시험 위치 | 상태 |
|---|---|---|---|---|---|
| C-53 | building 해석 | 정상 · 나쁜 id/높이 · 이상한 선택 값 | 해석 / 버림 / null | UiT/Map3DBuildingTests::should_read_every_field_when_3d_sends_building_message · should_drop_building_message_when_id_or_height_is_bad · should_keep_optional_fields_empty_when_they_are_odd | 통과(기존) |
| C-54 | GIS→3D 메시지 | preview · select · commit | JSON 모양 | UiT/Map3DBuildingTests::should_build_preview_select_and_commit_messages_when_gis_talks_to_3d | 통과(기존) |
| C-55 | 층수↔높이 | 반올림 · 왕복 | 1~200 · 소수 1자리 | UiT/Map3DBuildingTests::should_count_floors_from_height_and_floor_height · should_turn_floors_into_height_with_one_decimal_inside_limits | 통과(기존) |
| C-56 | 600 m 자르기 | 층고 10 × 200층 = 2000 m | 지금: 안내 없이 600 m(판에는 60층으로 보임) | UiT/Map3DBuildingTests::should_turn_floors_into_height_with_one_decimal_inside_limits(기존이 고정) · UiT/Map3DBuildingStepEdgeTests::should_clamp_to_600_meters_without_error_when_floors_times_floor_height_exceeds_max(기록) | 결정 필요 |
| C-57 | ±1층 경계 | 1층 −1 · 200층 +1 | 제자리 | UiT/Map3DBuildingTests::should_step_one_floor_and_stop_at_limits | 통과(기존) |
| C-58 | 키 | ↑/↓ · Shift ±0.1 m | 규칙대로 | UiT/Map3DBuildingTests::should_step_floor_with_arrows_and_tenth_meter_with_shift | 통과(기존) |
| C-59 | 낮은 건물 −1층(D1) | 2.0 · 1.2 · 1.0 · 2.9 m(층고 3) · 3.9(층고 4) · 1.5(층고 2) 에서 −1 / ↓ | 높이를 올리지 않음(제자리) | UiT/Map3DBuildingStepEdgeTests::should_not_raise_height_when_minus_one_floor_on_building_lower_than_one_floor · should_keep_lowering_by_floor_when_result_is_not_higher · ME/BuildingHeightEdgeScenarioTests::should_keep_low_building_height_when_minus_floor_button_or_down_key_pressed(2.0/1.2, StepBuildingFloorCommand "-1" · ↓) | 수정 후 통과 |
| C-60 | 높은 건물 +1층(D1 짝) | 450 · 500 · 600 m(층고 2 = 200층 상한 넘음) 에서 +1 / ↑ | 높이를 내리지 않음(지금은 400 m 로 떨어짐) | UiT/Map3DBuildingStepEdgeTests::should_not_lower_height_when_plus_one_floor_on_building_above_floor_limit | 수정 후 통과 |
| C-61 | 손잡이 | 데드존 · 끌기 | 4 px 넘어야 끌기 · 10 px = 1 m | UiT/Map3DBuildingTests::should_start_drag_only_past_dead_zone · should_follow_handle_drag_from_start_height | 통과(기존) |
| C-62 | 입력 검증 | 층수 · 높이 · 층고 정상/거절 | 이유 글 | UiT/Map3DBuildingTests::should_accept_whole_floors_in_range · should_reject_floors_with_reason_when_out_of_range_or_not_whole · should_accept_height_rounded_to_one_decimal · should_reject_height_with_reason_when_bad · should_check_floor_height_between_two_and_ten_meters | 통과(기존) |
| C-63 | 쉬운 말 | 출처 · 판 글 | 직접 입력 · 지도 자료 · 국가 자료 · … | UiT/Map3DBuildingTests::should_say_height_source_in_plain_words · should_write_panel_lines_when_showing_building | 통과(기존) |
| C-64 | map-3d 창구 | PUT · DELETE · height-overrides · 거절/이상 응답 · 목록 개수 | 요청 모양 · 예외 | UiT/Map3DBuildingTests::should_put_height_and_floors_and_read_saved_row_when_applying · should_delete_for_auto_and_reset_all_when_asked · should_raise_request_error_with_detail_when_server_rejects_or_answers_oddly · should_read_manual_counts_from_scene_list_and_default_to_zero | 통과(기존) |

### G. 건물 높이 판 — VM 흐름
| ID | 기능 | 시나리오 | 기대 | 시험 위치 | 상태 |
|---|---|---|---|---|---|
| C-65 | 판 열기 | 2D 에서 building / 3D 에서 building | 안 열림 / 높이 · 층수 · 출처 | ME/BuildingHeightScenarioTests::should_open_panel_with_height_floors_and_plain_source_when_building_is_clicked_in_3d | 통과(기존) |
| C-66 | 미리보기 | 층수 · 높이 · 층고 · ± · 키 · 잘못된 입력 | 그 건물만 미리보기 · 이유 줄 | ME/BuildingHeightScenarioTests::should_preview_only_that_building_when_floors_height_floor_height_steps_and_keys_change | 통과(기존) |
| C-67 | 손잡이 · Esc | 끌기 · 놓기 · 캡처 잃음 · 끄는 중 Esc | 따라감 · 되돌림 · 판/3D 유지 | ME/BuildingHeightScenarioTests::should_follow_handle_drag_and_go_back_when_drag_is_cancelled_or_escape_is_pressed | 통과(기존) |
| C-68 | 적용 | [적용] | PUT · commit · 칸 1 · 판 유지 | ME/BuildingHeightScenarioTests::should_save_with_put_and_commit_to_3d_when_apply_is_pressed | 통과(기존) |
| C-69 | 취소 계열 | 취소 · ✕ · Esc · 나가기 | 미리보기 null · 강조 지움 · 저장 없음 | ME/BuildingHeightScenarioTests::should_put_original_back_when_cancel_close_escape_or_leaving_3d | 통과(기존) |
| C-70 | 권한 없음 | 보기만 · 강제 Apply / **강제 Reset · Undo** | 요청 0 · 「권한 없음」 안내 | ME/BuildingHeightScenarioTests::should_only_show_with_reason_when_map_edit_permission_is_missing · ME/BuildingHeightEdgeScenarioTests::should_send_nothing_and_show_permission_info_when_reset_undo_or_reset_all_forced_without_permission | 통과(기존) · 통과(새) |
| C-71 | 권한 없음 「모두 자동으로…」 | 메뉴 실행 · 확인 없이 온 콜백 | 확인 팝업 없음 · 요청 0 · 안내 | ME/BuildingHeightEdgeScenarioTests::should_send_nothing_and_show_permission_info_… | 통과(새) |
| C-72 | 권한 없음 판 | 판 템플릿 | 입력 · 손잡이 비활성 · 층고만 켜짐 · 이유 줄 | ME/BuildingHeightScenarioTests::should_disable_inputs_and_show_reason_in_panel_when_permission_is_missing | 통과(기존) |
| C-73 | 연타 | [적용] 여러 번 | 요청 1 · commit 1 | ME/BuildingHeightScenarioTests::should_send_one_request_when_apply_is_pressed_repeatedly | 통과(기존) |
| C-74 | 저장 실패 | 409 · 서버 꺼짐 | 미리보기 유지 · 이유 줄 | ME/BuildingHeightScenarioTests::should_keep_preview_and_show_reason_when_server_refuses_or_is_off | 통과(기존) |
| C-75 | 실패 뒤 재시도 | 서버 실패 이유 줄이 있는 채 | [적용] 켜짐(입력 오류와 구분) | ME/BuildingHeightEdgeScenarioTests::should_still_allow_retry_when_error_line_is_from_server_failure_not_input | 통과(새) |
| C-76 | 되돌리기 순서 | 적용 ×2 · 자동으로 · 되돌리기 ×3 · Ctrl+Z(VM) | 거꾸로 PUT/DELETE | ME/BuildingHeightScenarioTests::should_reset_to_auto_and_step_back_through_saves_with_panel_undo | 통과(기존) |
| C-77 | 나가면 비움 | 저장 뒤 3D 나가기 | 칸 0 | 같은 시험(끝 부분) | 통과(기존) |
| C-78 | 되돌리기 실패 | 409 · 서버 꺼짐 | 칸 다시 남음 · 이유 줄 · Manual 유지 · 서버 복구 뒤 되돌리기 성공 | ME/BuildingHeightEdgeScenarioTests::should_keep_undo_step_reason_and_manual_when_undo_or_reset_fails(false/true) | 통과(새) |
| C-79 | 자동으로 되돌리기 실패 | 409 · 서버 꺼짐 | 이유 줄 · Manual 유지 · 칸 안 쌓음 · commit 없음 | 같은 시험 | 통과(새) |
| C-80 | 다른 건물 고르기 | 바꾼 뒤 다른 건물 / 바꾸지 않은 건물에서 옮김 | 앞 미리보기 null / 보낼 것 없음 | ME/BuildingHeightScenarioTests::should_cancel_previous_preview_when_another_building_is_picked | 통과(기존) |
| C-81 | 저장 중 3D 나가기(D2) | 응답 전 「나가기」 → 늦은 응답 → 다시 3D | 칸 0 · 다시 3D 에서 앞 세션 되돌리기 불가 · 서버 저장 · commit 은 그대로 / 되돌리기 실패 응답도 칸 안 되살림 | ME/BuildingHeightEdgeScenarioTests::should_not_refill_undo_nor_offer_previous_session_undo_when_save_response_arrives_after_leaving_3d · should_not_put_back_undo_step_when_undo_fails_after_leaving_3d | 수정 후 통과 |
| C-82 | 저장 중 다른 건물 | 저장 응답 전에 건물 8 고름 | 새 판(8) 안 덮음 · 7 commit · 칸 1 | ME/BuildingHeightEdgeScenarioTests::should_keep_new_panel_commit_previous_and_undo_only_previous_when_other_building_picked_during_save | 통과(새) |
| C-83 | 다른 건물 판에서 되돌리기 | 판=8, 칸=7 의 저장 | DELETE …/buildings/7 · 판 8 그대로 | 같은 시험 | 통과(새) |
| C-84 | 같은 건물 다시 | 바꾸던 건물을 다시 클릭 | 넣던 값 유지 | ME/BuildingHeightScenarioTests::should_cancel_previous_preview_when_another_building_is_picked | 통과(기존) |
| C-85 | 닫기 중 보내기 실패 | 판 열린 채 나가기 · PostWebMessage 예외 | 판 닫힘 · 2D 입력 살아남 · Error `판 닫기 — 3D 로 보내기 실패(격리)` | ME/Map3DExitInputScenarioTests::should_restore_2d_input_and_close_panel_when_3d_send_throws_while_exiting | 통과(기존) |
| C-86 | 판 연 채 나가기 | 판 · 판 취소 후 | 2D 입력 · 판 닫힘 | ME/Map3DExitInputScenarioTests::should_restore_2d_input_when_exited_with_panel_or_window_opened_in_3d(BuildingPanel · BuildingPanelThenCancel) | 통과(기존) |
| C-87 | 「모두 자동으로…」 | 메뉴 · 확인 · 실행 | DELETE height-overrides · commit 7/8 · 판 닫음 | ME/BuildingHeightScenarioTests::should_show_manual_count_warn_unmatched_and_reset_all_after_confirm_in_area_menu | 통과(기존) |
| C-88 | 못 붙인 직접 입력 | 같은 수 두 번 | 한 번만 알림 | 같은 시험 | 통과(기존) |
| C-89 | 직접 입력 없음 | 개수 0 | 메뉴 없음 | 같은 시험 | 통과(기존) |
| C-90 | scene-missing | 저장 뒤 3D 가 scene-missing | 2D · 판 닫힘 · 칸 0 · `_bhJob` null · 안내 | ME/BuildingHeightEdgeScenarioTests::should_close_panel_clear_undo_and_job_when_scene_goes_missing · ME/Map3DStaleJobScenarioTests::should_retire_job_and_refresh_list_when_viewer_reports_scene_missing | 통과(새) · 통과(기존) |
| C-91 | 다시 만들기 완료 | 옛 job 교체 | 판 · `_bhJob` · 칸 등 옛 job 참조 0 | ME/Map3DStaleJobScenarioTests::should_drop_every_reference_to_old_job_and_delete_it_when_rebake_completes | 통과(기존) |
| C-92 | 서버 목록에서 사라짐 | 보던 job 없음 | 판 닫고 2D · 안내 | ME/Map3DStaleJobScenarioTests::should_leave_to_2d_with_notice_when_shown_job_disappears_from_server_list | 통과(기존) |
| C-93 | 「모두 자동으로」 실패 | 서버 꺼짐 · 409 | 안내(AimStatus) · 확인 팝업 닫힘 · commit 없음 · (판은 요청 전에 닫힌 채) | ME/BuildingHeightEdgeScenarioTests::should_tell_reason_close_popup_and_commit_nothing_when_reset_all_fails(false/true) | 통과(새) |
| C-94 | 「모두 자동으로」 성공 → 칸 | 저장 뒤 모두 자동으로 | 그 장면 되돌리기 칸 0 | ME/BuildingHeightEdgeScenarioTests::should_drop_that_area_undo_steps_when_reset_all_succeeds | 통과(새) |
| C-95 | 다시 만들기 뒤 직접 입력 유지 | 다시 굽기 | 영역 단위 · 건물 자리로 다시 붙음 | F/test_heights.py(map-3d heights.py) | F 담당(연결) |

### H. 판 컨트롤 · 키
| ID | 기능 | 시나리오 | 기대 | 시험 위치 | 상태 |
|---|---|---|---|---|---|
| C-96 | 판 템플릿 | AutomationId 전부 · 바인딩 | Map.BuildingHeight.* 전부 · VM 값 | ME/BuildingHeightScenarioTests::should_build_panel_with_every_automation_id_drag_handle_keys_and_light_dark_tokens | 통과(기존) |
| C-97 | 손잡이 Thumb | DragStarted/Delta/Completed(취소) | VM 미리보기 따름 · 되돌림 | 같은 시험 | 통과(기존) |
| C-98 | 판 안 ↑ | 층수 칸에서 ↑ | +1층 | 같은 시험 | 통과(기존) |
| C-99 | 토큰 | 라이트 → 다크 | 머리 띠 색 따라 바뀜 | 같은 시험 | 통과(기존) |
| C-100 | 지도 Esc | 판 열림 → 판만 / 끄는 중 → 끌기만 / 판 닫힘(composition) → 3D 유지 | 그대로 | ME/BuildingHeightScenarioTests::should_put_original_back_… · should_follow_handle_drag_… · ME/Map3DExitInputScenarioTests::should_keep_2d_blocked_by_esc_and_restore_only_by_exit_button_when_3d_shown_in_wpf | 통과(기존) |
| C-101 | 네이티브 창 3D 건물 클릭(D3) | HostMode=hwnd 에서 building | 판 안 열림 · 안내 `네이티브 창 3D 에서는 건물 높이 판을 쓸 수 없습니다` · select null · 로그 · 첫 Esc = 3D 나가기 | ME/BuildingHeightEdgeScenarioTests::should_not_open_panel_and_tell_reason_and_let_esc_exit_when_building_clicked_in_native_window_3d | 수정 후 통과 |
| C-102 | 판 Ctrl+Z · Shift+↑ | 실제 키 조합(Keyboard.Modifiers 는 실제 키보드 상태라 주입 불가) | 되돌리기 · +0.1 m | VM 경로는 C-76 · C-58 | 헤디드 |
| C-103 | 입력칸 Enter | 층수 칸 글 바꾸고 Enter / 높이 "abc" Enter / "21.5" Enter | 바로 반영(30 m) / 오류 줄 보임 / 21.5 m · 7층 | ME/BuildingHeightEdgeScenarioTests::should_apply_typed_value_on_enter_and_cancel_panel_on_escape_inside_panel | 통과(새) |
| C-104 | 판 Esc | 판 안 Esc | 판 취소(미리보기 null · select null) · 3D 유지 | 같은 시험 | 통과(새) |
| C-105 | 잘못된 입력 남은 채 [적용](D7) | "abc" · "600.1" · "0" · "3.5" · 층고 "11" | [적용] 꺼짐 · 강제 Apply 도 요청 0 · 오류 줄 유지 · 바른 값이면 다시 켜짐 | ME/BuildingHeightEdgeScenarioTests::should_refuse_apply_and_keep_error_when_invalid_input_remains(5 경우) | 수정 후 통과 |
| C-106 | 층수 입력 직후 [적용](D4) | 층수 칸에 쓰고(포커스 그대로) 비활성 [적용] 클릭 | LostFocus 가 안 일어나 반영 안 됨 — 고치지 않음(지시) | — | 헤디드 |


### D 2D 편집

상태: 통과(기존) · 통과(새) · 수정 후 통과 · 헤디드 · 결정 필요 · 미시험(이유)
약칭: GRS = tests/GMaps.MapEdit.Tests/GroupRotationScenarioTests.cs · GRP = …/GroupRotationPanelScenarioTests.cs · **GRE = …/GroupRotationEdgeScenarioTests.cs(새)** ·
LPS = …/LayerPanelMapSelectionSyncTests.cs · **LPE = …/LayerPanelSelectionEdgeTests.cs(새)** · **GPE = …/GroupPropertyEditEdgeTests.cs(새)** ·
DRS = …/DeviceReassignScenarioTests.cs · **DRE = …/DeviceReassignEdgeTests.cs(새)** · GRM = tests/GMaps.Ui.Tests/GroupRotationMathTests.cs · GRR = tests/GMaps.Ui.Tests/GroupRotationPanelRulesTests.cs ·
MLS = tests/GMaps.MapLayer.Tests/LayerPanelSelectionScenarioTests.cs · LWS = tests/GMaps.MapLayer.Tests/LockWhileSelectedTests.cs ·
SKP = tests/GMaps.PropertyPanel.Tests/SameKindGroupPanelParityTests.cs · PGF = tests/GMaps.PropertyPanel.Tests/PidsGroupFenceGroupEditScenarioTests.cs ·
RTS = tests/GMaps.MapNav.Tests/RotationTiltScenarioTests.cs · MTW = tests/GMaps.Housing.Tests/MapTiltWiringTests.cs

### 묶음 회전 (D-01 ~ D-45)
| ID | 기능 | 시나리오 | 기대 | 시험 위치(file::method) | 상태 |
|---|---|---|---|---|---|
| D-01 | 키 회전 | ] ] ] [ → Enter | 미리보기 중 저장 0 · Enter 에 멤버마다 1회 저장 · Undo 1단위 전원 원위치 · "3개 회전 +2.0° 저장" | GRS::should_preview_with_keys_then_save_once_and_undo_as_one_unit_when_enter_confirms | 통과(기존) |
| D-02 | 키 회전 | 미리보기 뒤 Esc | 원위치 · 저장 0 · Undo 0 · "회전 취소" · 선택 유지 | GRS::should_restore_everything_without_saving_when_escape_cancels_rotation | 통과(기존) |
| D-03 | 키 회전 | Shift+] = 0.1° | 0.1° 한 칸 | GRM::should_step_one_degree_and_fine_tenth_when_keys_are_used(상수) + 실제 Shift(Keyboard.Modifiers 위조 불가) | 헤디드 |
| D-04 | 경계값 | 359°+2° · 180° 넘음 | 1° 로 감김 · 각 (-180,180] | GRS::should_preview_with_keys…(b 359→1) · GRM::should_wrap_bearing_into_zero_to_360_when_rotation_is_added · GRM::should_wrap_rotation_into_signed_half_turn_when_beyond_180 | 통과(기존) |
| D-05 | 대상 | 선(꼭짓점 3) + 심볼 + 잠긴 심볼 90° | 꼭짓점 전부 · 잠금 그대로 · 잠금 저장 없음 · Undo | GRS::should_rotate_all_line_vertices_but_skip_locked_members_when_group_is_rotated | 통과(기존) |
| D-06 | 시작 조건 | 단일 선택 + ] | 회전 안 함 · 키 미소비(Handled=false) · 「회전」 단추 꺼짐 | GRE::should_not_rotate_or_consume_bracket_key_when_only_one_marker_is_selected | 통과(새) |
| D-07 | 시작 조건 | 전원 잠김 + 「회전」 · ] | 세션 없음 · "돌릴 수 있는 항목이 없습니다(잠기지 않은 것이 있어야 합니다)." | GRE::should_refuse_with_notice_when_rotate_is_pressed_and_every_member_is_locked | 통과(새) |
| D-08 | 손잡이 | 2개 이상 선택 | Thumb(peer) Map.GroupSelection.RotateHandle · 묶음 위쪽 가운데 | GRS::should_show_rotate_handle_thumb_with_automation_id_when_two_or_more_are_selected | 통과(기존) |
| D-09 | 손잡이 | 7px → 100px 끌기 → 놓음 → 확정 | 데드존 8 DIU · 90° · 놓아도 세션 유지 · 확정 때만 저장 | GRS::should_start_only_past_dead_zone_and_save_only_after_confirm_when_handle_is_dragged | 통과(기존) |
| D-10 | 손잡이 취소 | 클릭만 / 끌다 캡처 잃음 | 무반응 / 원위치 · 저장 0 | GRS::should_put_members_back_when_drag_loses_capture_or_is_only_a_click | 통과(기존) |
| D-11 | 대상 | PIDS 구역 + 카메라 60° | 구역 = 꼭짓점만(방향 각 그대로 · 감시 방향 대상 아님) · 카메라 감시 방향 +60 · 저장 · Undo | GRE::should_rotate_only_zone_vertices_and_leave_zone_bearing_when_pids_zone_is_in_group | 통과(새) |
| D-12 | 대상 | 오버레이 이미지 + 심볼 +10° | 이미지 중심 · 회전각 함께 · UpdateImageAsync 저장 · Undo 원위치 | GRE::should_rotate_overlay_image_with_symbols_and_undo_both_when_image_is_in_group | 통과(새) |
| D-13 | 감시 방향 | 카메라 · 제어기 +20 → Esc → +20 Enter → Undo | DetectionBearing 함께(350+20→10) · BaseBearing 그대로 · Undo 1번 | GRS::should_turn_device_view_direction_with_group_and_undo_it_when_rotation_confirmed | 통과(기존) |
| D-14 | 판 | 「회전」 단추 | 판 · 요약 "2개 · 도형 2 (잠금 1개 제외)" · 중심 · 0.0 | GRP::should_open_panel_with_kind_summary_center_and_zero_angle_when_rotate_button_is_pressed | 통과(기존) |
| D-15 | 판 | 선택 1개 이하 | 단추 꺼짐 | GRP::should_not_offer_rotate_button_when_fewer_than_two_are_selected | 통과(기존) |
| D-16 | 판 양방향 | 입력 · ] · 막대 · ±단추 · 손잡이 이어 돌리기 · 캡처 잃음 | 판 ↔ 세션 ↔ 지도 · 저장 0 | GRP::should_keep_panel_session_and_map_in_sync_both_ways_when_angle_changes_from_panel_keys_or_slider | 통과(기존) |
| D-17 | 판 입력 오류 | "abc" · "200" · 위도 95 · 경도 "동경" | 오류 글 · 지도 그대로 · 바른 값이면 지움 | GRP::should_show_error_and_leave_members_when_angle_or_center_text_is_invalid · GRR::should_reject_* | 통과(기존) |
| D-18 | 중심 | 입력 → 가운데로 → 입력 → 확정 | 중심 기준 · 각 유지 · 저장 1회 · Undo | GRP::should_rotate_about_typed_center_then_back_about_bounds_center_when_reset_and_save_once_on_confirm | 통과(기존) |
| D-19 | 판 취소 | ✕ · 취소 / 0° 확정 | 원위치 · 저장 0 / 저장 없이 닫힘 | GRP::should_put_everything_back_without_saving_when_panel_cancel_or_close_is_pressed | 통과(기존) |
| D-20 | 중심 고르기 | 고르기 → 지도 클릭 / Esc | 클릭 자리 중심 / 고르기만 그만 | GRP::should_take_next_map_click_as_center_when_pick_center_is_on_and_stop_on_escape | 통과(기존) + 헤디드 |
| D-21 | 중심 십자 | 끌기 · 캡처 잃음 | 이동 · 잃으면 원래 중심 · 세션 끝 = 십자 빠짐 | GRP::should_move_center_when_center_cross_is_dragged_and_restore_it_when_capture_is_lost | 통과(기존) + 헤디드 |
| D-22 | 판 템플릿 | AutomationId 15 · 라이트/다크 | 전부 · 토큰 색 | GRP::should_build_panel_with_every_automation_id_and_follow_light_dark_tokens_when_template_is_applied | 통과(기존) |
| D-23 | 수학 | 메르카토르 · 경계 상자 중심 · 거리 보존 · 드래그 각 · 15° 스냅 | 순수 함수 | GRM 22건 | 통과(기존) |
| D-24 | 판 규칙 | 파싱 · 형식 · 요약 · 확인 글 · 단계 | 순수 함수 | GRR 12건 | 통과(기존) |
| D-25 | 권한 | 권한 없음 + ] | 회전 안 함 | GRS::should_not_rotate_when_account_cannot_edit_map | 통과(기존) |
| D-26 | 권한 | 권한 없음 + 「회전」 · ] | 세션 없음 · "권한 없음" 팝업 · 키 소비 | GRE::should_block_rotate_button_and_keys_with_permission_notice_when_account_cannot_edit_map | 통과(새)(손잡이 = 결정 1) |
| D-27 | 다른 조작 = 확정 | 키 미리보기 중 선택 해제 | 확정 · 저장 · Undo | GRS::should_confirm_pending_keyboard_rotation_when_selection_changes | 통과(기존) |
| D-28 | 다른 조작 = 확정 | 미리보기 중 Undo | 확정 → 그것만 되돌림 | GRS::should_confirm_then_revert_pending_rotation_when_undo_is_pressed | 통과(기존) |
| D-29 | 다른 조작 = 확정 | 미리보기 중 Redo | 먼저 확정(새 편집이 옛 Redo 무효화) · 저장 · Redo 불가 | GRE::should_confirm_preview_first_when_redo_is_pressed_during_rotation_preview | 통과(새) |
| D-30 | 다른 조작 = 확정 | 미리보기 중 → | 확정 후 1px 이동 · Undo 두 단위 | GRE::should_confirm_then_move_with_two_undo_units_when_arrow_key_is_pressed_during_preview | 통과(새) |
| D-31 | 다른 조작 = 확정 | 미리보기 중 묶음 끌기 시작(MoveDragStarting) | 확정 · 저장 | GRE::should_confirm_rotation_when_group_move_drag_starts_during_preview | 통과(새) + 헤디드 |
| D-32 | 다른 조작 = 확정 | 판 열린 채 편집 모드 끄기 | 확정 · 저장 · 판 닫힘 · 선택 해제 | GRE::should_confirm_save_and_close_panel_when_edit_mode_is_turned_off_during_preview | 통과(새) |
| D-33 | 권한 회수 | 미리보기 중 역할 강등(OnPtzPermissionsChanged) | 편집 모드 꺼짐 → 권한 없음 = 원위치 · 저장 0 · Undo 0 | GRE::should_put_members_back_without_saving_when_permission_is_revoked_during_preview | 통과(새) |
| D-34 | 다른 조작 = 확정 | 판 열린 채 선택 해제 | 확정 · 판 닫힘 | GRP::should_close_panel_by_confirming_when_selection_changes_like_keyboard_rotation | 통과(기존) |
| D-35 | 잠금(①) | 미리보기 중 그룹 잠금 → Esc | 먼저 확정 · Esc 무효 · 화면 = DB · Undo "그룹 잠금" → "그룹 회전 2개" | GRE::should_confirm_open_rotation_before_group_lock_so_escape_cannot_split_screen_and_db | 수정 후 통과 |
| D-36 | 잠금(①) | 미리보기 중 레이어 패널 자물쇠 → ] | 먼저 확정 · 잠긴 멤버는 다음 회전에서 빠짐 | GRE::should_confirm_open_rotation_and_stop_turning_member_when_it_is_locked_from_layer_panel | 수정 후 통과 |
| D-37 | 속성 판(②) | 미리보기 중 그룹 판 너비 64 → Esc | 저장 전 확정 · Esc 무효 · 판 64 · 회전 칸 6 · Undo 두 단위 | GRE::should_confirm_open_rotation_before_group_property_change_is_saved | 수정 후 통과 |
| D-38 | 연타 | 확정 · 확정 · Enter | 저장 멤버당 1 · Undo 1 | GRE::should_save_once_and_record_one_undo_when_confirm_is_pressed_repeatedly | 통과(새) |
| D-39 | 판 포커스 | 판 칸 · 막대 포커스 중 ←/→ · Esc | 키는 판 몫 · Esc 만 회전 취소 | — (Keyboard.FocusedElement 창 없이 불가) | 헤디드 |
| D-40 | Ctrl+A | 잠금 · 숨김 · 이미지 · 줌 제외 | 보이는 편집 대상만 · "3개 선택" | GRS::should_group_select_visible_unlocked_symbols_when_select_all_is_used | 통과(기존) + 헤디드(Ctrl) |
| D-41 | 판 값(③) | PIDS 2개 그룹 판 + 20° 확정 → Undo | 판 회전 0→20 · 감시 방향 30→50 · Undo 뒤 0/30 | GRE::should_refill_group_panel_bearing_and_view_direction_after_rotation_confirm_and_undo | 수정 후 통과 |
| D-42 | 저장 실패 | 확정 저장 일부 실패 | 지금: Undo 먼저(동기) 기록 · Error 로그 "그룹 회전 영속 실패: …" · 상태 글 "{저장 수}개 회전 … 저장" | — | 결정 필요(결정 2) |
| D-43 | 손잡이 스냅 | Shift 끌기 | 15° 단위 | GRM::should_snap_to_fifteen_degree_steps_when_shift_is_held · should_snap_drag_angle_when_snap_is_on + 실제 Shift | 헤디드 |
| D-44 | 지도 회전 | 지도 Bearing 30° 에서 손잡이 위→오른쪽 | 지리 회전량 = 화면 끌기 각(≈90°) | GRE::should_turn_by_same_geographic_angle_as_drag_when_map_itself_is_rotated_30_degrees | 통과(새) |
| D-45 | 손잡이 · 잠금 | 전원 잠김 묶음 손잡이 끌기 | 데드존 통과 즉시 끝 · 세션 · 판 없음 | GRE::should_end_handle_drag_at_once_without_moving_when_every_member_is_locked | 통과(새) |

### 레이어 패널 다중 선택 (D-46 ~ D-70)
| ID | 기능 | 시나리오 | 기대 | 시험 위치(file::method) | 상태 |
|---|---|---|---|---|---|
| D-46 | 패널 → 지도 | 행 1개 클릭 | 단일 편집 | LPS::should_select_same_markers_on_map_as_single_then_group_when_layer_rows_are_picked_in_edit_mode | 통과(기존) |
| D-47 | 패널 → 지도 | Ctrl+클릭 | 묶음 + 공통 속성창 | 같은 시험 | 통과(기존) |
| D-48 | 패널 → 지도 | Shift+클릭 | 기준~행 | 같은 시험 | 통과(기존) |
| D-49 | 패널 → 지도 | Ctrl+Shift+클릭 | 범위 더하기 | 같은 시험 | 통과(기존) |
| D-50 | 패널 키 | Esc | 지도 선택 해제 | 같은 시험 | 통과(기존) |
| D-51 | 패널 → 지도 | Ctrl+묶음 머리 → 하나 빼기 → ] Enter | 회전 · 속성 대상 = 패널 선택 · 선택 유지 | LPS::should_rotate_and_edit_exactly_the_layer_selected_set_when_rows_are_picked_then_rotated | 통과(기존) |
| D-52 | 제외 | 잠김 · 숨김 | 지도로 안 감 · 패널 풀림 | LPS::should_skip_locked_and_hidden_rows_and_unmark_them_when_layer_selection_goes_to_map | 통과(기존) |
| D-53 | 지도 → 패널 | 클릭 · Ctrl+클릭 · 러버밴드 · Ctrl+A · 해제 | 잎 표시 · 접힌 묶음 펼침 | LPS::should_mark_rows_and_expand_folded_group_when_map_selection_changes_by_click_ctrl_click_rubber_band_and_select_all | 통과(기존) |
| D-54 | 되먹임 | 양방향 | 알림 1번 · 적용 1번 | LPS::should_not_feed_back_when_selection_flows_between_layer_panel_and_map | 통과(기존) |
| D-55 | 편집 모드 연동 | 운영 → 켬 → 끔 | 패널만 → 지도로 → 지도만 풀림 | LPS::should_keep_selection_inside_panel_while_not_editing_and_carry_it_to_map_when_edit_mode_turns_on | 통과(기존) |
| D-56 | 키 양보 | 패널 초점 ↑↓ Space Ctrl+A Esc | 패널 몫 · Del 은 지도 | LPS::should_not_nudge_or_select_all_on_map_when_layer_panel_owns_the_key · MLS::should_claim_only_selection_keys_when_map_hook_asks_layer_panel | 통과(기존) + 헤디드 |
| D-57 | 재조회 · 삭제 | 트리 재구성 · 묶음 삭제 | 같은 항목 재선택 · 삭제 빠짐 | LPS::should_keep_selection_on_same_items_and_drop_deleted_ones_when_layer_tree_is_reloaded | 통과(기존) |
| D-58 | 패널 규칙 | 탐색기 규칙 · 접힌 묶음 Shift · 접혀 사라진 기준 | 기대대로 | MLS::should_select_by_explorer_rules_when_rows_are_clicked_with_modifiers · should_skip_rows_inside_collapsed_group_when_shift_range_crosses_it · should_restart_range_from_clicked_row_when_anchor_was_folded_away | 통과(기존) |
| D-59 | 패널 규칙 | 접힌 묶음 머리 Ctrl | 잎 전부 | MLS::should_add_every_leaf_of_group_even_collapsed_when_header_is_ctrl_clicked | 통과(기존) |
| D-60 | 패널 알림 | 조작 1번 = 알림 1번 | 기대대로 | MLS::should_raise_one_change_per_user_action_and_none_when_nothing_changed | 통과(기존) |
| D-61 | 패널 키보드 | ↑↓ · Shift+↑↓ · Space · Ctrl+A · Esc | 이동 · 확장 · 토글 · 전체 · 해제 | MLS::should_move_extend_toggle_select_all_and_clear_when_selection_keys_are_pressed | 통과(기존) |
| D-62 | 클릭 판정 · UIA | 이름 · 체크 · 자물쇠 · 펼침 · 슬라이더 · 손잡이 · 우클릭 · 재구성 기준 | 이름만 선택 · 나머지 유지 · UIA SelectionItem | MLS::should_select_row_when_its_name_is_pressed_… · should_select_unselected_row_and_keep_multi_selection_when_row_is_right_clicked · should_expose_selection_to_uia_… · should_follow_handle_click_… · should_keep_shift_anchor_on_same_item_when_tree_is_rebuilt | 통과(기존) |
| D-63 | 줌(⑤) | 줌 21 심볼 포함 Shift 범위 | 지도 묶음 · 패널 선택에서 빠짐 · Ctrl+A 와 같음 | LPE::should_leave_out_symbols_hidden_by_zoom_and_unmark_their_rows_when_layer_rows_go_to_map | 수정 후 통과 |
| D-64 | 이미지 행 | 심볼 행 + 이미지 행 Ctrl | 묶음 = 심볼 + 이미지 | LPE::should_include_overlay_image_in_map_group_when_image_row_and_symbol_row_are_picked | 통과(새) |
| D-65 | 회전 판 + 패널 | 판 20° 열린 채 다른 행 | 먼저 확정 → 새 단일 선택 | LPE::should_confirm_open_rotation_then_select_new_row_when_other_row_is_clicked_with_panel_open | 통과(새) |
| D-66 | 패널 잠금 | 묶음 멤버 자물쇠(잎 IsLocked → SymbolLockChanged) → 회전 | "(잠금 1개 제외)" · 안 돎 | LPE::should_leave_member_out_of_rotation_when_it_is_locked_from_layer_panel_while_grouped | 통과(새) |
| D-67 | 잠근 채 선택 | 선택된 이미지 · 심볼 패널 잠금/풀기 | 선택 해제 · 삭제 거절 이유 | LWS 3건 | 통과(기존) |
| D-68 | 실제 마우스 | 행 클릭 + 수식키 | 위와 같음 | — | 헤디드 |
| D-69 | 실제 키보드 | 패널 초점 키가 지도로 안 샘 | 묶음 안 움직임 | — | 헤디드 |
| D-70 | 도구줄 | GMaps.Toolbar.LayerPanelButton | 패널 열림 · 지도 선택 비춤 | (VM.ShowLayerPanel 은 LPS/LPE 사용) | 헤디드 |

### 다중 선택 속성 판(그룹 편집) (D-71 ~ D-80)
| ID | 기능 | 시나리오 | 기대 | 시험 위치(file::method) | 상태 |
|---|---|---|---|---|---|
| D-71 | 같은 종류 | 같은 종류 여러 개 | 단일 칸 전부 | SKP::should_show_every_single_selection_field_when_same_kind_is_multi_selected | 통과(기존) |
| D-72 | 공통 값 | 적용 | 잠기지 않은 멤버 · Undo 1 | SKP::should_apply_common_value_to_unlocked_members_and_undo_once_when_same_kind_group_edits | 통과(기존) |
| D-73 | 고유 값 잠금 | 장비 연결 · 현재위치 적용 | 비활성 + 대표값 | SKP::should_disable_unique_field_with_representative_value_when_group_editing · PaletteAndPropertyTests::should_disable_device_link_combo_and_apply_button_when_two_symbols_are_selected · DRS::should_block_device_link_change_in_group_property_panel | 통과(기존) |
| D-74 | 다른 종류 | 도형 + 카메라 최소 줌 15 → Undo | 둘 다 15 · 종류별 저장 · Undo 각자 값 · 판 다시 빈칸 | GPE::should_apply_common_field_to_every_kind_and_undo_as_one_unit_when_group_mixes_kinds | 통과(새) |
| D-75 | Redo | 너비 72 → Undo → Redo | 전원 72 · 다시 저장 · 판 72 | GPE::should_reapply_group_change_to_all_and_refill_panel_when_redo_follows_undo | 통과(새) |
| D-76 | 권한 상실 | 판 열린 채 회수 후 너비 | 변화 0 · 저장 0 · "권한 없음" · "[RBAC] 맵 편집 권한 없음 — 속성 저장 차단: Width" | GPE::should_block_group_property_save_with_notice_when_permission_is_lost_while_panel_is_open | 통과(새) |
| D-77 | 저장 실패(④) | 한 멤버 실패 / 전원 실패 | 실패 멤버 Undo 제외 · 전원 실패 = 기록 0 | GPE::should_not_record_undo_for_member_whose_group_property_save_failed | 수정 후 통과 |
| D-78 | 구역 · 철망 | 높이 · 간격 · 3D · 모드 · 잠금 · 단일 복귀 | PGF 11건 | PGF 전체 | 통과(기존) |
| D-79 | 장비 종류 | 통문 폭 · 모델 / 종류 섞임 / 선 정보 | 전원 적용 / 종류 줄 숨김 / 합계 | SKP::should_apply_gate_width_and_model_variant_to_all_when_same_device_type_group_edits · should_hide_device_type_rows_when_pids_group_mixes_device_types · should_sum_read_only_line_info_when_zones_are_multi_selected | 통과(기존) |
| D-80 | 실제 조작 | 그룹 판 칸 실제 입력 · 빈칸 표시 | 위와 같음 | — | 헤디드 |

### 장비 재할당(1장비 = 1마커) (D-81 ~ D-98)
| ID | 기능 | 시나리오 | 기대 | 시험 위치(file::method) | 상태 |
|---|---|---|---|---|---|
| D-81 | 안내 | 다른 마커가 가진 장비 | 팝업 문구 · B 원래 값 · 저장 0 | DRS::should_ask_with_holder_title_and_not_save_when_device_is_already_linked_to_other_marker | 통과(기존) |
| D-82 | 아니오 | 실제 콤보 | 콤보 · 판 · 마커 원래 | DRS::should_revert_combo_selection_and_keep_everything_when_user_answers_no | 통과(기존) |
| D-83 | 상태 바뀜 | 대기 없음 · A 풀림 · **대기 중 대상(B)/소유(A) 삭제** | 아무 일 없음 · 경고 로그 "[장비 재할당] 확인 사이에 상태가 바뀌어 취소: '…'" · 창 닫힘 | DRS::should_do_nothing_when_confirm_arrives_without_pending_or_after_state_changed · DRE::should_cancel_without_saving_and_close_popup_when_target_or_holder_is_deleted_before_confirm | 통과(기존) · 통과(새) |
| D-84 | 예 | 확인 | A 비움 → B 연결 · 조회표 B · Undo 1 · Redo | DRS::should_clear_old_marker_and_link_new_marker_with_two_saves_and_one_undo_when_user_answers_yes | 통과(기존) |
| D-85 | 실패 | B 저장 실패 | 둘 다 원복 | DRS::should_restore_both_markers_and_saves_when_new_marker_save_fails | 통과(기존) |
| D-86 | 실패 | A 비움 실패 | B 시도 안 함 | DRS::should_change_nothing_when_old_marker_clear_save_fails | 통과(기존) |
| D-87 | 안내 없음 | 같은 장비 · 해제 · 빈 장비 | 팝업 없음 · Undo | DRS::should_not_ask_when_same_marker_repicks_its_device_or_unlinks_or_picks_free_device | 통과(기존) |
| D-88 | 같은 Id | 제어기 7 vs 카메라 7 | 충돌 아님 | DRS::should_not_treat_same_id_of_other_device_kind_as_conflict | 통과(기존) |
| D-89 | 그룹 판 | 연결 바꾸기 | 막힘 | DRS::should_block_device_link_change_in_group_property_panel | 통과(기존) |
| D-90 | 종류 문구 | 카메라 · 센서 · 제어기 · 함체 / **통문 · 스피커 · 경광등**(GateDeviceModel · SpeakerDeviceModel · LampDeviceModel, DeviceType 일치) | "'가' {종류} 심볼에 할당되어 있습니다" · 예 처리 | DRS::should_reassign_per_device_kind_with_kind_name_in_question · DRE::should_name_gate_speaker_and_lamp_kinds_in_question_and_reassign | 통과(기존) · 통과(새) |
| D-91 | 다시 고름 | cam1 대기 중 cam2 → 예 → 늦은 예 | 마지막만 적용 · 늦은 예 무시 | DRE::should_apply_only_last_choice_when_another_conflicting_device_is_picked_before_confirm | 통과(새) |
| D-92 | 세 마커(⑥) | 중복 자료(정문1 · 정문2)에서 후문이 고름 → 예 → Undo | 다른 소유 마커 모두 비움 · Undo 1단위 · 경고 글 사라짐 | DRE::should_clear_every_other_holder_and_undo_all_together_when_three_markers_share_device | 수정 후 통과 |
| D-93 | 권한 없음 | 권한 없는 계정이 연결 고름 | 재할당 팝업 없이 "권한 없음" · 저장 0 · A 그대로 · "[RBAC] … 속성 저장 차단: LinkedDevice" | DRE::should_block_without_reassign_popup_when_account_cannot_edit_map | 통과(새)(판 선택값 = 결정 3) |
| D-94 | 대기 중 Undo | 평범한 변경 → 충돌(대기) → Ctrl+Z → 예 | 상태 바뀜으로 취소 · A 안 비움 | DRE::should_cancel_pending_reassign_when_undo_changes_target_before_confirm | 통과(새) |
| D-95 | 복사 | 붙여넣기 복제본 | 연결 비움 | DRS::should_clear_device_link_on_copy_paste_clone | 통과(기존) |
| D-96 | 부팅 중복 | 같은 장비 두 마커 | 첫 마커만 · "[장비 중복 연결]" 경고 · 판 경고 글 | DRS::should_keep_first_marker_warn_in_log_and_show_note_without_saving_when_loading_duplicate_links | 통과(기존) |
| D-97 | 콤보 글 | 다른 마커가 가진 장비 | "· 정문1 에 할당됨" | DRS::should_mark_devices_linked_to_other_markers_in_combo_items | 통과(기존) |
| D-98 | 실제 팝업 | 예 / 아니오 · 닫기 | 위와 같음 | — | 헤디드 |

### 지도 기울이기 제거 (D-99 ~ D-103)
| ID | 기능 | 시나리오 | 기대 | 시험 위치(file::method) | 상태 |
|---|---|---|---|---|---|
| D-99 | 진입점 없음 | 뷰 · VM · 단축키 · 저장된 켜짐 설정 | 토글 · 각도 · 메뉴 · 배지 · Ctrl+Shift+T · 기울이기 화살표 없음 · 평평 | RTS::should_have_no_tilt_toggle_angle_popup_menu_item_or_badge_when_map_view_loaded · should_expose_no_tilt_members_on_map_view_model_when_tilt_removed · should_not_bind_ctrl_shift_t_or_tilt_arrow_keys_when_tilt_removed · should_stay_flat_when_saved_tilt_setting_was_on_and_vm_restored | 통과(기존) |
| D-100 | 회전은 그대로 | 회전 토글 · Ctrl+←/→ | 동작 | RTS::should_keep_rotation_toggle_working_when_tilt_removed 외 회전 5건 | 통과(기존) |
| D-101 | 지도 컨트롤 | 진입점 · 시작 상태 | 없음 · 꺼짐 | MTW::should_have_no_toggle_or_step_entry_points_and_start_off_when_tilt_removed(+MTW 16건) | 통과(기존) |
| D-102 | 뷰 바인딩 · 전환 | 오버스캔 바인딩 · 변환기 · 지도 전환 · 옛 게이트 줌 | 없음 · 평평 | MapViewTiltOverscanBindingTests 3 · TiltOverscanConverterTests 1 · MapToolbarScenarioTests::should_stay_flat_and_leave_old_tilt_key_untouched_when_saved_tilt_was_on_and_map_switched · MapNavRemainingScenarioTests 3 | 통과(기존) |
| D-103 | ↑ = 묶음 이동 | 묶음 + ↑(Ctrl 없음) · 지도 OnKeyDown ↑↓ PgUp PgDn | 1px 위(소비) · "격자 이동 2개" · RequestedTiltDeg/TiltDeg 그대로 | GRE::should_move_group_with_plain_up_key_and_never_tilt_map_when_arrow_keys_are_pressed | 통과(새) · Ctrl+↑↓ 무반응은 헤디드 |


### E 오버레이 이미지(GIS)

| ID | 기능 | 시나리오 | 기대 | 시험 위치(file::method) | 상태 |
|---|---|---|---|---|---|
| E-01 | 처음 불러오기 | 다각형 그리고 「자르기 적용」 | 원본 복사본 옆에 `_crop_` PNG(상자 크기 · 바깥 투명) + `.crop.json`(원본 · 상자) · 사용자 원본 불변 | ImageCropScenarioTests::should_save_cropped_png_next_to_untouched_copy_when_crop_is_applied_on_first_load | 통과(기존) |
| E-02 | 처음 불러오기 | 「자르지 않고 그대로」 | 원본 그대로 복사 · 기록 없음 | ImageCropScenarioTests::should_copy_unchanged_when_skip_is_chosen_and_add_nothing_when_cancelled | 통과(기존) |
| E-03 | 처음 불러오기 | 「취소」 | null(추가 안 함) · 복사도 안 함 | 같은 시험 | 통과(기존) |
| E-04 | 처음 불러오기 | 머리 띠 ✕ | 취소와 같음 | 같은 시험 | 통과(기존) |
| E-05 | 처음 불러오기 | 원본 PNG 에 이미 투명 | 원본 알파 ∩ 다각형 | ImageCropScenarioTests::should_keep_original_transparency_and_polygon_together_when_source_png_has_alpha · ImageCropMathTests::should_intersect_with_source_alpha_when_original_png_is_already_transparent | 통과(기존) |
| E-06 | 처음 불러오기 | 판이 떠 있는 동안 | 아직 복사하지 않음 | ImageCropScenarioTests::should_save_cropped_png_next_to_untouched_copy_when_crop_is_applied_on_first_load(Copies==0) | 통과(기존) |
| E-07 | 다각형 검사 | 넓이 0.5 지만 남는 픽셀 0(1×1 그림 삼각형) | 적용 막고 "남는 픽셀이 없습니다" | ImageCropEdgeScenarioTests::should_block_apply_with_reason_when_polygon_keeps_no_pixel · ::should_keep_panel_open_with_reason_when_first_load_polygon_keeps_no_pixel | 수정 후 통과 |
| E-08 | 다각형 검사 | 높이 0.8 px 띠(넓이 80) · 무작위 다각형 600개 | 남는 픽셀 판정 = 실제 자르기(MaskAndCrop) 결과 | ImageCropEdgeScenarioTests::should_block_apply_with_reason_when_polygon_keeps_no_pixel · ::should_agree_with_mask_when_pixel_check_runs_on_random_polygons | 수정 후 통과 |
| E-09 | 다각형 검사 | 3점 미만 · 나비 · 꼭짓점이 변에 닿음 · 한 줄 · 오목 | 이유 줄 / 오목은 됨 | ImageCropMathTests::should_block_apply_with_reason_when_polygon_has_fewer_than_three_points_or_crosses_itself · ::should_block_apply_with_reason_and_complete_only_for_a_valid_closed_polygon | 통과(기존) |
| E-10 | 처음 불러오기 | 깨진 PNG 를 고름 | 판 없이 그대로 복사 + Warning "[ImageCrop] 그림을 읽지 못해 자르기 없이 추가" | ImageCropEdgeScenarioTests::should_copy_unchanged_without_panel_and_warn_when_picked_file_is_not_a_readable_image | 통과(새) |
| E-11 | 저장 실패 | 저장 폴더에 못 씀(복사 실패 · 자른 PNG 쓰기 실패 · 다시 자르기 쓰기 실패) | 안내 · 추가/변경 없음 · 예외가 위로 새지 않음 | ImageCropEdgeScenarioTests::should_tell_and_add_nothing_without_throwing_when_overlay_folder_cannot_be_written_on_first_load · ::should_tell_and_leave_placed_image_when_cropped_png_cannot_be_written_on_recrop | 수정 후 통과 |
| E-12 | 판 편집 | 클릭 추가 · 첫 점 = 닫기 · 더블클릭/Enter 닫기(3점 이상) | 다각형 상태 | ImageCropMathTests::should_add_points_and_close_when_first_point_is_clicked · ::should_close_by_double_click_or_enter_only_when_three_points_exist | 통과(기존) |
| E-13 | 판 편집 | 꼭짓점 클릭 vs 끌기(8 DIU 데드존) · 가운데 손잡이 끌기 = 점 추가 · 끌기 중 캡처 잃음 | 끌기 전 모양으로 | ImageCropMathTests::should_treat_small_move_as_click_and_larger_move_as_vertex_drag_when_pressing_a_vertex · ::should_insert_vertex_when_edge_midpoint_handle_is_dragged · ::should_cancel_vertex_drag_when_pointer_is_cancelled · ::should_select_without_adding_point_when_existing_vertex_is_clicked_while_drawing | 통과(기존) |
| E-14 | 판 편집 | 우클릭/Delete/Backspace 지우기 · 3점 미만이면 다시 열림 | 점 지움 · 열림 | ImageCropMathTests::should_remove_vertex_by_right_click_delete_and_backspace · ::should_reopen_polygon_when_vertex_removal_leaves_fewer_than_three_points | 통과(기존) |
| E-15 | 결과 | 「적용」 두 번 · 적용 뒤 취소 · 그대로 | 결과 하나(첫 적용) · 자른 파일 1개 · 복사 1번 | ImageCropEdgeScenarioTests::should_produce_one_result_when_apply_is_pressed_twice_or_cancel_follows_apply | 통과(새) |
| E-16 | 조합 | 판이 떠 있는데 Ctrl+I 로 또 불러오기 | "자르기 판을 먼저 닫으세요" · 파일 고르기 창 안 엶 · 첫 판 유지 · CanExecute 그대로 | ImageCropEdgeScenarioTests::should_tell_to_close_panel_first_and_keep_first_panel_when_image_load_is_requested_again · ImageCropMenuScenarioTests::should_check_open_crop_panel_before_opening_file_picker_when_image_overlay_is_loaded(소스 순서) | 수정 후 통과 |
| E-17 | 조합 | 판이 떠 있는데 편집 모드 끄기 | 판 취소(처음 불러오기 = 추가 안 함, 다시 자르기 = 그대로 · DB/Undo 없음) | ImageCropEdgeScenarioTests::should_cancel_open_crop_panel_and_add_nothing_when_edit_mode_is_turned_off | 수정 후 통과 |
| E-18 | 조합 | 판 안에서 Delete · 방향키(Ctrl+Z/Y 같은 경로) | 지도 처리기가 손대지 않음(삭제 확인창 없음 · 그림 안 움직임 · Handled=false), 지도에서 온 Delete 는 그대로 | ImageCropEdgeScenarioTests::should_leave_delete_and_arrow_keys_to_panel_when_key_comes_from_image_crop_panel | 수정 후 통과 |
| E-19 | 판 템플릿 | AutomationId · 토큰(라이트/다크) · 판 안 Enter/Esc 두 번 · 이유 줄 · 그대로 단추 숨김 | 템플릿 그대로 | ImageCropScenarioTests::should_expose_automation_ids_tokens_and_keys_when_panel_template_is_applied | 통과(기존) |
| E-20 | 판 보기 | 확대 · 이동 뒤 클릭 → 그림 픽셀 · Esc 단계 · 닫힌 다각형 Esc = 확인 · Undo/Redo · 그대로는 처음만 | VM 상태 | ImageCropMathTests::should_map_click_to_image_pixel_when_view_is_zoomed_and_panned · ::should_cancel_drawing_then_ask_then_close_when_escape_is_pressed_repeatedly · ::should_ask_before_closing_when_escape_is_pressed_on_a_closed_polygon · ::should_undo_and_redo_with_ctrl_keys_inside_panel · ::should_offer_skip_only_on_first_load | 통과(기존) |
| E-21 | 다시 자르기 | 0° · 30° · 90° 그림 자르기 | 남은 부분 제자리 · 크기 비율 · DB · MapLayers 경로 · Undo/Redo 1단위 | ImageCropScenarioTests::should_crop_placed_image_in_place_save_and_undo_as_one_step | 통과(기존) |
| E-22 | 다시 자르기 | Esc 두 번 취소 | 그림 · 경계 · Undo 그대로 | ImageCropScenarioTests::should_leave_image_unchanged_when_recrop_is_cancelled_or_permission_is_missing | 통과(기존) |
| E-23 | 다시 자르기 | 편집 권한 없음 | 판 안 열림 | 같은 시험 | 통과(기존) |
| E-24 | 되돌리기 | 자른 뒤 옮기고 「자르기 되돌리기」 · Undo | 지금 자리 기준 원본 · Undo 1단위 | ImageCropScenarioTests::should_restore_pre_crop_image_in_place_when_crop_is_reverted | 통과(기존) |
| E-25 | 되돌리기 | 자르기 전 원본 파일이 지워짐 | "자르기 전 그림 파일이 없어 되돌릴 수 없습니다" · 그대로 | ImageCropEdgeScenarioTests::should_tell_and_keep_cropped_image_when_pre_crop_file_is_missing_on_revert | 통과(새) |
| E-26 | 되돌리기 | `.crop.json` 깨짐 | 자른 그림 아님(메뉴 안 보임 · "자른 그림이 아니라…") | ImageCropEdgeScenarioTests::should_treat_image_as_not_cropped_when_crop_record_is_broken | 통과(새) |
| E-27 | 되돌리기 | 편집 권한 없음 | 「권한 없음」 안내 · 그대로 · Undo 없음 | ImageCropEdgeScenarioTests::should_tell_no_permission_and_keep_cropped_image_when_revert_is_requested_without_edit_permission | 통과(새) |
| E-28 | 다시 자르기 | 놓인 그림 파일이 없음 | 판 안 열림 · "그림 파일을 읽을 수 없어 자를 수 없습니다" · Warning | ImageCropEdgeScenarioTests::should_tell_and_not_open_panel_when_placed_image_file_is_missing_on_recrop | 통과(새) |
| E-29 | 순서 | 두 번 자르고 두 번 되돌리기 | 한 단계씩(1차 결과 → 원본, 경계 · 크기 왕복) | ImageCropEdgeScenarioTests::should_revert_one_step_at_a_time_when_image_was_cropped_twice | 통과(새) |
| E-30 | 조합 | 자른 뒤 회전 45° 로 바꾸고 되돌리기 | 회전 유지 · 남은 부분 제자리(현재 회전 기준) | ImageCropEdgeScenarioTests::should_restore_around_cropped_part_with_current_rotation_when_rotation_changed_after_crop | 통과(새) |
| E-31 | 연타 | 「다각형으로 자르기」 세 번 | 판 하나 | ImageCropEdgeScenarioTests::should_keep_single_panel_when_recrop_menu_is_clicked_repeatedly | 통과(새) |
| E-32 | 조합 | 판 열린 채 그 그림 삭제 후 적용 | 무시(파일 · DB · Undo 없음) | ImageCropEdgeScenarioTests::should_ignore_apply_when_image_was_deleted_while_recrop_panel_is_open | 통과(새) |
| E-33 | 조합 | 다시 자르기 판이 떠 있는 동안 그림이 바뀜(이전 자르기 Undo) | 적용 안 함 · "그림이 바뀌어 자르기를 취소했습니다" | ImageCropEdgeScenarioTests::should_not_apply_and_tell_when_image_changes_while_recrop_panel_is_open | 수정 후 통과 |
| E-34 | 저장 실패 | 자르기 · 되돌리기 DB 저장(UpdateImageAsync) 예외 | Undo 안 쌓음 · 마커 이전 상태 · MapLayers 그대로 · 실패 안내 | ImageCropEdgeScenarioTests::should_keep_image_and_add_no_undo_when_db_save_fails_on_crop_or_revert | 수정 후 통과 |
| E-35 | 조합 | 자른 그림을 지운 뒤 자르기 Undo | 아무 일 없음(DB · 레이어 · 되살아남 없음) | ImageCropEdgeScenarioTests::should_do_nothing_when_crop_is_undone_after_cropped_image_was_deleted | 통과(새) |
| E-36 | 메뉴 | 우클릭 메뉴 — 편집 모드에서만 · 자른 그림에만 「자르기 되돌리기」(바로 다음 줄) · 누르면 그 그림 판 | 메뉴 항목 · 판 | ImageCropMenuScenarioTests::should_show_crop_items_with_automation_ids_only_in_edit_mode_and_revert_only_for_cropped_image · ::should_open_crop_panel_for_that_image_when_crop_menu_item_is_pressed | 수정 후 통과(AutomationId 부분 D-7) |
| E-37 | 예전 데이터 | MapLayers 에 그 경로 행 없음 | 조용히 자름(레이어 갱신 없음 · 오류 없음) | ImageCropEdgeScenarioTests::should_crop_quietly_without_layer_update_when_map_layers_has_no_row_for_image | 통과(새) |
| E-38 | 수학 | 경계 조정 회전 0/90 · 왕복 · 경계 상자 · 대각선 픽셀 · 원본 해상도 | 정확 | ImageCropMathTests::should_keep_remaining_part_in_place_when_placed_image_is_cropped · ::should_move_center_south_when_right_half_is_kept_at_90_degrees · ::should_restore_original_bounds_when_crop_is_reverted · ::should_make_notch_transparent_when_polygon_is_concave · ::should_decide_each_boundary_pixel_by_its_center_when_edge_is_diagonal · ::should_crop_to_bounding_box_with_one_pixel_accuracy_when_polygon_has_fractional_edges · ::should_clamp_box_to_image_when_points_lie_on_the_image_border · ::should_keep_full_resolution_and_bgra_png_when_cropping_a_bitmap | 통과(기존) |
| E-39 | 3D 보냄 | 투명 있음 = PNG · 불투명 = JPEG(2048 맞춤) · 캐시 · 파일 바뀌면 다시 | 형식 · 캐시 | Map3DImageCacheTests::should_encode_png_when_image_has_transparency · ::should_encode_jpeg_fitted_to_2048_when_image_opaque · ::should_read_disk_cache_instead_of_encoding_when_same_file_again · ::should_reencode_when_source_file_changes · ::should_not_write_cache_when_image_has_no_source_file · Map3DTransitionTests::should_detect_transparency_when_any_alpha_below_255 · ::should_choose_png_when_transparent_and_jpeg_when_opaque | 통과(기존) |
| E-40 | 3D 보냄 | 자른 뒤 3D 굽기 | 자르기 전 JPEG → 자른 뒤 PNG · 투명 유지 · 캐시 파일 .png | ImageCropScenarioTests::should_send_cropped_image_to_3d_as_png_when_baking | 통과(기존) |
| E-41~E-89 | map-3d | region_ll · 성격 판정 · 사진 텍스처 · 흐린 자리 바닥 | (F 작업자 표) | artifacts/map3d/tests/test_alpha_region.py · test_style_alpha.py · test_photo_texture.py · test_ground.py | F |


### F map-3d(E · F 의 map-3d 쪽 · B/C 뷰어)

### B · C (map-3d 뷰어 쪽)
| ID | 기능 | 시나리오 | 기대 | 시험 위치(file::method) | 상태 |
|---|---|---|---|---|---|
| B-70 | 영역 밖 오버레이 | 옛(bbox) · 회전 장면에서 1 km 떨어진 발전소 사진 · 표식 | 영역 밖 숨김 · 회전 장면 안 보임 | overlay_area_check.mjs(test_rotated_truth.py::should_put_terrain_buildings_and_overlays_on_same_lonlat_when_rotated) | 통과(기존) |
| B-71 | 영역 밖 오버레이 | 항목이 안 → overlay.patch 로 밖 → 다시 안 · 선 밖으로 · add 밖 · visible 토글 · 줌 갱신 · remove | 밖=숨김, 다시 안=보임, 줌 갱신해도 영역 판정 유지 | bc(coord::should_hide_out_of_area_markers_tracks_and_not_double_click_across_building_when_checked_with_node) | 통과(새) |
| B-72 | 영역 밖 추적 | track.upsert 영역 밖 → 안(꼬리 포함) → 다시 밖 · 숫자 id · 스냅샷 뒤 유지 · 영역 없는 옛 장면 | 밖=숨김, 안=보임, 옛 장면=보임 | bc(coord::…node) | 수정 후 통과(B-D4) |
| B-D5 | GIS 창 자리 | 영역 밖이라 숨긴 마커 · 추적의 anchorOf · watchPoint | null(위경도로 되살리지 않음 → overlay.screen visible:false) | bc(coord::…node) | 수정 후 통과 |
| B-73 | 모양별 겹침 | 영역을 꼭짓점 없이 가로지르는 선 · 둘러싼 다각형 · 열린 선 · 가장자리 걸친 이미지 · 위치 없는 항목 · 영역 없음 | itemInArea 규칙대로 | overlay_area_check.mjs | 통과(기존) |
| B-74 | 회전 영역 처음 시점 · 축소 한계 · sceneYaw · 높이표 | 회전 사각형 | 꽉 차게 · 어느 방위든 영역 전체 | rotated_check.mjs(test_rotated_area.py::should_fit_rotated_area_and_rotate_model_when_viewer_checks) | 통과(기존) |
| B-75 | 지형 ↔ 오버레이 같은 자리 | 회전 장면 GLB 정점 vs enuOf | ±0.5 m · 높이 ±0.1 m | test_rotated_truth.py::should_put_terrain_buildings_and_overlays_on_same_lonlat_when_rotated | 통과(기존) |
| B-76 | 좌표 왕복 | lonLatOf(enuOf(p))==p, 위도 0 · 37.5 · 60 · 80 · 동서/남북 거리 = WGS84 묘유선 · 자오선 식 | 1e-12° · 상대오차 1e-9 | bc | 통과(새) |
| B-77 | 축소 한계 · 휠 칸 자르기 | fitAreaView · maxOrbitDistance · clampZoomSteps | 한계에 딱 멈춤 | camsync_check.mjs(test_server.py::should_round_trip_map_view_through_camera_when_synced) | 통과(기존) |
| B-78 | 굽기: 회전 영역 자르기 · 영역 밖 오버레이 건너뜀 · 영역 밖 높이 입력 무시 | | | test_rotated_area.py(should_cut_scene_to_rotated_rect_when_baking 외 7) | 통과(기존) |
| B-79 | GIS 창 자리 보고 | 지켜볼 항목 해석 · 투영 · 바뀐 것만 · 같은 프레임 view | | overlay_screen_check.mjs(test_server.py::should_report_gis_window_screen_points_when_watched) | 통과(기존) |
| C-10 | 화면 거리 고르기 | 마커 > 선 > 면 > 건물, 빗나간 클릭 8 px, 숨김 · 화면 밖 · 카메라 뒤 | | overlay_pick_check.mjs(test_server.py::should_pick_overlays_before_buildings_and_draw_them_unoccluded_when_clicked) | 통과(기존) |
| C-14 | 더블클릭 | 같은 마커 A 두 번(400 ms 안) | dblclick | bc(clickStep) | 통과(새) |
| C-15 | 더블클릭 경계 | 정확히 400 ms · 세 번 연타 · 같은 id 다른 kind · 선 | 두 클릭 · dblclick 뒤 새 클릭 · 아님 · 선 dblclick | bc | 통과(새) |
| C-16 | 다른 마커 사이 | A → B → A(400 ms 안) | 클릭 셋 | bc | 통과(새) |
| C-99 | 사이에 건물/빈 땅 | 마커 A → 건물 → 마커 A(400 ms 안) | 더블클릭 아님(C-D6) | bc(clickStep + view.js 처리기 소스 단언) | 수정 후 통과 |
| C-17 | 건물 commit 뒤 다음 미리보기 | 10→20 저장 뒤 20→15 미리보기 | 처음 모양에서 바로 15 로 늘인 것과 같다(옛 줄로 셈하면 다르다 — 대조) | bc(stretchPositions 합성) | 통과(새) |
| C-18 | 건물 클릭 데드존 · 메시지 · 미리보기 · GIS 메시지 검사 | | | building_check.mjs(test_heights.py::should_pick_preview_and_highlight_buildings_when_checked_with_node) | 통과(기존) |
| C-19 | 높이 직접 입력 저장 · 다시 굽기 뒤 다시 붙임 · 경쟁 | | | test_heights.py(7건) | 통과(기존) |
| C-D6b | 빈 땅 클릭 | 건물 선택만 지우고 GIS 에 안 알림 → 건물 높이 판이 열린 채 | — | — | 결정 필요(보고만, 아래 「결정」) |

### E (map-3d 쪽)
| ID | 기능 | 시나리오 | 기대 | 시험 위치(file::method) | 상태 |
|---|---|---|---|---|---|
| E-41 | 투명 범위 | 알파로 자른 모양 = 굽는 범위(region_ll) · region_info | 불투명 모양만 | test_alpha_region.py | 통과(기존) |
| E-42 | 투명 범위 합성 | composite 가 투명 화소는 위성 그대로 | | test_alpha_region.py | 통과(기존) |
| E-43 | 투명 범위 마스크 | 모양 안 OSM 만 뺌 | | test_alpha_region.py | 통과(기존) |
| E-44 | 성격 판정 알파 | 투명 배경은 성격 판정에서 뺌 | | test_style_alpha.py | 통과(기존) |
| E-45 | 도식 오버레이 | 도식 → 건물 윤곽 | | test_overlay.py | 통과(기존) |
| E-46 | 영역 여러 개 · 겹침 | | | test_regions.py | 통과(기존) |
| E-47 | 사진 텍스처 | 두 건물 사진 면 · 카메라 | | test_photo_texture.py | 통과(기존) |
| E-48 | 흐린 자리 바닥 | 사진 땅(건물 지움) | | test_ground.py | 통과(기존) |
| E-49 | 완전 투명 PNG | region 빈 결과 · coverage 0 · composite 0 | 순수 함수 | E::should_give_empty_region_zero_cover_and_untouched_texture_when_png_fully_transparent | 통과(새) |
| E-49b | 완전 투명 PNG 굽기 | composite · extract 모드 | 경고 2개(「불투명 부분이 너무 작음」 · 「가 영역 밖이라 굽지 않음」) · overlays_baked==[] · scene.glb | E::should_warn_tiny_and_outside_and_bake_nothing_when_png_fully_transparent[composite/extract] | 통과(새) |
| E-50 | 1×1 PNG | 불투명 → 전체 사각형으로 늘임 · 투명 → 완전 투명과 같음 · 굽기 안 터짐 | | E::should_treat_one_pixel_opaque_png_as_full_rectangle_and_stretch_it · should_treat_one_pixel_transparent_png_like_fully_transparent · should_bake_one_pixel_png_without_crash_when_generating | 통과(새) · 결정 필요(아래) |
| E-51 | 투명 PNG + 회전 영역 | 불투명 부분이 회전 사각형 밖 → 건너뜀 / 안 → 굽기 | | E::should_skip_transparent_png_whose_opaque_part_is_outside_rotated_rect · should_bake_transparent_png_whose_opaque_part_is_inside_rotated_rect | 통과(새) |
| E-52 | 투명 PNG + mask=false | 불투명 모양에만 위성 위 합성 · OSM 안 뺌(removed_osm 비어 있음) | | E::should_blend_over_satellite_only_on_opaque_shape_when_mask_off · should_keep_osm_features_and_report_empty_removed_osm_when_mask_off | 통과(새) |
| E-53~E-62 | 오버레이 이미지 검증 · 경계 · 회전 · 파이프라인 기본(composite/extract) | | | test_overlay.py · test_alpha_region.py · test_extract_photo.py · test_oblique.py(should_not_paint_overlay_on_terrain_and_hide_it_in_viewer_when_extract) | 통과(기존) |
| E-63 | 자른 도식 | 투명으로 자른 도식 | illustration 유지 | E::should_keep_illustration_style_when_diagram_is_cropped_with_transparency | 통과(새) |
| E-64 | 마스크 64 px | 깎은 마스크 63/64/65 경계 | 64 미만만 전체로 폴백 | E::should_fall_back_to_whole_image_only_when_eroded_mask_is_below_64 | 통과(새) |
| E-65 | 알파 전달 | extract_buildings → detect_style 에 알파 마스크 | | E::should_pass_alpha_mask_of_probe_to_detect_style_when_extracting | 통과(새) |
| E-66~E-72 | 사진 텍스처 기본 · 비스듬한 실사 추출 | | | test_photo_texture.py · test_extract_photo.py · test_oblique.py | 통과(기존) |
| E-73 | repeat=False | 가린 벽은 절차 벽 · wall_rgb 줌 | | E::should_leave_hidden_walls_procedural_and_give_wall_rgb_when_repeat_off | 통과(새) |
| E-74 | 투명 사진 자리 | 투명 화소 위 면은 사진 면 아님 | | E::should_not_use_photo_for_face_lying_on_transparent_photo_pixels | 통과(새) |
| E-75 | 사진 가장자리 | 벽 · 지붕이 사진 밖으로 → 절차 폴백 | | E::should_fall_back_to_procedural_when_wall_and_roof_run_off_photo_edge | 통과(새) |
| E-76 | 가림 건물 | 텍스처 없는 건물도 가림에는 씀 | | E::should_occlude_with_untextured_building_but_not_texture_it | 통과(새) |
| E-77 | 1 텍셀 미만 건물 | 안 터짐 | | E::should_handle_building_smaller_than_one_texel_without_crash | 통과(새) |
| E-78~E-85 | 흐린 자리 판정 · 바닥 · 취소(SAM 타일 · 점 격자 · 정사영상 줄) | | | test_ground.py · test_oblique.py::should_mark_blurred_tile_area_but_not_small_smooth_patch · test_cancel.py | 통과(기존) |
| E-86 | 흐린 곳 없음 | photo_ground pixels 0, 채우지 않음 | | E::should_return_zero_pixels_and_not_fill_when_nothing_is_blurred | 통과(새) |
| E-87 | OpenCV 없음 | 경고 「OpenCV(opencv-python-headless)가 없어 흐린 자리 바닥은 위성 그대로」 | | — | 미시험(문구 확인만, 환경 의존) |
| E-88 | LaMa 창 사이 취소 | 취소 → 멈춤 · Telea 로 폴백하지 않음 | | E::should_stop_between_lama_windows_and_not_fall_back_to_telea_when_cancelled | 통과(새) |
| E-89 | 다시 굽기 취소 · 이전 장면 유지 | | | test_cancel.py::should_keep_previous_scene_when_rebake_cancelled | 통과(기존) |

### F (map-3d 굽기 품질)
| ID | 기능 | 시나리오 | 기대 | 시험 위치(file::method) | 상태 |
|---|---|---|---|---|---|
| F-01 | 출처 결정 | 사진과 맞으면 지도 윤곽, 지도에 없으면 사진, 흐린 곳 지도 | | test_quality.py::should_pick_map_outline_when_photo_matches_and_photo_where_map_missing | 통과(기존) |
| F-02 | 출처 결정 | 사진 덩어리가 지도 건물 둘을 삼킴 · 구조물은 사진 | | test_quality.py::should_keep_map_buildings_when_photo_blob_swallows_them_and_photo_for_structures | 통과(기존) |
| F-03 | 출처 기록 | origin=map · height_from=levels · photo_match | | test_quality.py::should_record_origin_and_height_source_per_building_when_extract_with_map_data | 통과(기존) |
| F-04 | 국가 자료 | 로컬 색인 건물 → origin=national | | F::should_mark_origin_national_when_buildings_come_from_local_index | 통과(새) |
| F-05 | 사진 건물 | origin=photo | | F::should_build_photo_only_scene_with_overlay_when_no_external_data | 통과(새) |
| F-06~F-07 | 로컬 건물 자료 · OSM 파이프라인 | | | test_buildings_local.py · test_osm_pipeline.py | 통과(기존) |
| F-08 | 높이 출처 | 지도 높이가 기본값 + 사진 짝 → 사진 높이(overlay) | | F::should_take_photo_height_when_map_building_has_default_height_and_matches_photo | 통과(새) |
| F-09~F-10 | 가정 해 · 맞춘 해 | 위도별 가정 · 시차 ↔ 그림자 맞춤 | | test_quality.py::should_assume_leaf_off_late_morning_sun_from_latitude · should_fit_sun_elevation_from_parallax_heights_and_tile_shadows | 통과(기존) |
| F-11 | 기울기 맞춤 순서 | 지도 → 그림자 → 기본 | | test_oblique.py::should_pick_tilt_from_known_buildings_then_shadows_then_default | 통과(기존) |
| F-12 | refs 0 | sun.source default | | F::should_keep_assumed_sun_when_oblique_photo_has_no_known_heights | 통과(새) |
| F-13 | 맞춘 고도 극단 | SUN_EL_RANGE(15~75°)로 자름 | | F::should_clip_fitted_sun_elevation_to_range_when_fit_is_extreme[…] | 통과(새) |
| F-14 | 해 고도 0 | 가정 고도 0° | 3° 로 자름(shadow_k>0) · 뷰어 sunOf 0 → 3 | coord::should_clip_default_sun_to_3_degrees_for_shadow_heights_when_assumed_elevation_is_zero_or_negative[0.0] · bc | 수정 후 통과(F-D8) |
| F-15 | 높이 이상치 | 가짜 고층 → 그림자 · 기본, 고층 단지는 그대로 | | test_quality.py::should_replace_false_tall_match_with_shadow_or_default_height | 통과(기존) |
| F-16 | 이상치 경계 | 3동 미만 → 안 바꿈 · 이웃 149/151 m | 150 m 안만 이웃 | F::should_not_change_heights_when_fewer_than_three_buildings · should_judge_outlier_only_against_neighbors_within_150m | 통과(새) |
| F-17 | shadow_dir 없음 | azimuth_source default · 가정 방위 | | F::should_use_assumed_azimuth_when_tiles_give_no_shadow_direction | 통과(새) |
| F-18~F-23 | 그림자 다각형 · 구운 음영 · 사진 그림자 이중 방지 · 외부 0건 장면 · 뷰어 해 | | | test_quality.py(그림자 · 음영 · 0건) · light_check.mjs | 통과(기존) |
| F-24 | 해 고도 음수(고위도) | 위도 80° 가정 −7.1° | default_sun 은 천문 값(음수) · 굽기 shadow_k · elevation_deg = 3° · 사진 추출(stats.sun)도 3° · 뷰어 sunOf −7 → 3 | coord::should_assume_negative_elevation_when_latitude_is_far_north · …[-7.1] · should_clip_assumed_sun_in_photo_extract_when_no_map_refs_and_far_north · should_keep_default_sun_when_assumed_elevation_is_above_3_degrees · bc | 수정 후 통과(F-D8) |
| F-25 | 음영 | 건물 0동 → 전부 255 | | F::should_leave_shade_map_blank_when_no_buildings | 통과(새) |
| F-26 | 음영 | 높이 0 → 그림자 없음 · 접지 음영만 | | F::should_darken_only_wall_base_when_building_height_is_zero | 통과(새) |
| F-27 | 음영 축소 | 정사영상 긴 변 > 2048 → MAX_PX | | F::should_cap_shade_map_long_side_at_max_px_when_ortho_is_large[…] | 통과(새) |
| F-28~F-30 | 난간 · 옥탑 기본 · 경사 · 탱크 없음 · 직접 입력 때 난간 올림 | | | test_quality.py::should_add_parapet_and_penthouse_on_flat_roof_but_not_on_pitched_or_tank · test_heights.py::should_lift_roof_parapet_and_stretch_walls_when_height_changes | 통과(기존) |
| F-31 | 난간 | 4 m 미만 → 없음 | | F::should_skip_parapet_when_building_lower_than_4m | 통과(새) |
| F-32 | 난간 | 40 m² 미만 → 없음 | | F::should_skip_parapet_when_roof_smaller_than_40m2 | 통과(새) |
| F-33 | 난간 | 좁은 목(< 2×PARAPET_T) → 난간 전체 없음 | | F::should_skip_whole_parapet_when_neck_narrower_than_twice_parapet_thickness | 지금 동작 확인 · 결정 필요 |
| F-34 | 난간 | 중정 → 안팎 난간 | | F::should_put_parapet_on_both_outer_and_courtyard_edges_when_footprint_has_hole | 통과(새) |
| F-35~F-40 | 외벽 스타일 · 구조물(탱크 · 굴뚝 · 침전지) · Blender 상세 | | | test_facade.py · test_structures.py · test_blender_detail.py | 통과(기존) |
| F-41 | 공장색 | 탱크 · 굴뚝 근처 낮은 미상 건물 → industrial 색 | | F::should_paint_factory_tint_when_low_unknown_building_sits_near_tanks | 통과(새) |
| F-42 | 사진 벽색 | 사진 벽 없으면 절차 벽을 사진색 반 · 흰색 반으로 | | F::should_tint_procedural_walls_with_half_white_photo_color_when_photo_walls_missing | 통과(새) |
| F-43~F-52 | 비스듬한 실사 등록 · 기울기 · 윤곽 정리 · 가중치 없음 폴백 · 정사영상/지형 · OSM · 지오 · 서버 | | | test_oblique.py · test_ortho_terrain.py · test_osm_pipeline.py · test_geo.py · test_server.py | 통과(기존) |
| F-53 | 외부 0건 + extract 오버레이 | 사진 건물만 · sun default · GLB 재질 | | F::should_build_photo_only_scene_with_overlay_when_no_external_data | 통과(새) |
| F-54 | MBTiles 없음 | 회색 정사영상 + 경고 | | F::should_use_gray_ortho_and_warn_when_mbtiles_file_missing | 통과(새) · 결정 필요(줌이 경고 없이 16) |
| F-55~F-57 | 취소(OSM 대기 · 메시 · GLB 쓰기 경쟁) | | | test_cancel.py | 통과(기존) |

(기존 시험 행은 이번 전체 수트 실행에서 함께 통과.)


