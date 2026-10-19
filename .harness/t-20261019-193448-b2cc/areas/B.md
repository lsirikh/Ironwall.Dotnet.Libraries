# B 3D 영역 시나리오 · 시험 결과

작업 worktree: `C:\workspace_app\Ironwall.Dotnet.Libraries\.claude\worktrees\agent-a2e7b905795a6f222`(아래 결정 8 — 작업 환경).
줄임: MapNav = `tests/GMaps.MapNav.Tests`, MapEdit = `tests/GMaps.MapEdit.Tests`, Ui = `tests/GMaps.Ui.Tests`, Housing = `tests/GMaps.Housing.Tests`, m3d = `COORD\artifacts\map3d\tests`.

## 시나리오 표

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

## 고친 결함

### B-50 (D1) 다시 만드는 중인 영역을 지우면, 굽기가 끝난 뒤 지운 영역이 새 장면으로 되살아난다
- 원인: `Map3DAreaItem` 메뉴가 `cancelBake != null`(다시 만드는 중)이어도 「지우기…」를 늘 넣었고(Ironwall.Dotnet.Libraries.GMaps.Ui/Services/Map3D/Map3DAreaItem.cs:56), 지우기 확인 `HandleAsync(CallDeleteMap3DAreaProcessMessageModel)`(ViewModels/Maps/MapViewModel.Map3DAreas.cs:307~)가 굽기 상태를 보지 않고 DELETE · `RetireMap3DJob` 했다. 굽기가 끝나면 `BakeMap3DAsync` 가 새 job 을 `_map3DScenes` 맨 앞에 넣고(MapViewModel.Map3D.cs:587) 목록을 새로 받아 같은 자리에 영역이 다시 나타난다(옛 job DELETE 는 404 → 무시).
- 수정(지시문 결정대로): ① `Map3DAreaItem.cs` — 다시 만드는 중이면 「다시 만들기」처럼 「지우기…」도 넣지 않는다(「만들기 취소」가 맨 위). ② `MapViewModel.Map3DAreas.cs` — private `RefuseDeleteWhileRebaking(item)`(굽는 중 && item.Id == `_map3DBakeSourceId`)를 두고 `RequestDeleteMap3DAreaAsync`(확인 창 열기 전) · `HandleAsync`(확인 뒤)에서 막는다 — 상태 줄 "만드는 중에는 지울 수 없습니다 — 「만들기 취소」 뒤 지우세요" + 로그 `[Map3D] 다시 만드는 중 — 영역 지우기 막음 id={id}`. 공개 형식 변경 없음.
- 증거: 수정 전 `Map3DAreaScenarioTests.should_refuse_delete_of_area_being_rebaked_and_allow_after_bake` 실패 — "Assert.DoesNotContain() Failure: Filter matched in collection … Header = 지우기…" / `should_not_open_delete_confirm_when_area_being_rebaked` 실패 — "Assert.DoesNotContain() Failure: Filter matched in collection [OpenConfirmPopupMessageModel { … Title = "3D 영역 지우기" }]". 추가로 `HandleAsync` 막기만 잠깐 끈 채(메뉴 수정은 둔 채) 다시 돌려 "Assert.Equal() Failure: Values differ Expected: 0 Actual: 1"(DELETE /api/jobs/area-x 가 나감)을 확인하고 되돌림. 수정 후 두 시험 통과.

### B-34 「다시 만들기」 판 [취소] 뒤 다시 만들기 표지가 남는다(잠재 상태 누수)
- 원인: `RebakeMap3DArea` 가 `_map3DBakeReplaces` · `_map3DBakeOptionsSourceId` 를 정하는데, 판 [취소](`CancelMap3DBakeOptionsCommand` → `CloseMap3DBakeOptions`, MapViewModel.Map3DBakeOptions.cs:74~78)는 이 둘을 비우지 않는다. 지금은 다음 굽기 경로(TakeMap3DArea · RequestMap3DBake)가 시작 전에 비워 주므로 사용자에게 보이는 결과는 없다(같은 시험 뒷부분 — 새 영역 굽기가 옛 영역을 지우지 않음 — 은 수정 전에도 성립).
- 수정: [취소] 처리기에서 두 표지를 비운다(한 줄, MapViewModel.Map3DBakeOptions.cs). `CloseMap3DBakeOptions` 는 [만들기] 확정 경로도 부르므로(확정 뒤 `BakeMap3DAsync` 가 표지를 읽음) 거기서는 비우지 않았다.
- 증거: 수정 전 `Map3DBakeOptionsScenarioTests.should_post_nothing_and_clear_rebake_marks_when_bake_panel_cancelled` 실패 — "Assert.Null() Failure: Value is not null Expected: null Actual: "x"" / 수정 후 통과.

## 결정

1. **D1 수정 방식**(지시문 결정): 다시 만드는 중인 영역은 「지우기…」를 메뉴에서 빼고, 어떤 경로로 와도 지우지 않고 안내. 「만들기 취소」 뒤 지운다. 다른 영역 지우기는 그대로 허용(B-51).
2. **B-35 의도된 근사 — 고치지 않음**: 회전 영역의 겹치는 이미지는 감싸는 bbox 로 고른다(`RequestMap3DBake` 주석). 사각형 밖 bbox 모서리에만 있는 이미지도 판이 세고 라디오가 보이며 본문에 실린다(map-3d 가 회전 사각형 밖은 굽지 않는다). 지금 동작을 시험으로 기록.
3. **B-60 D6 — 결정 필요**: 보던 3D 영역을 지우면 DELETE 를 보내기 **전에** 2D 로 나간다(`HandleAsync` 서두). 서버에 닿지 않아 지우지 못해도 사용자는 이미 2D 이고 영역은 남는다(「3D 로 보기」로 다시 들어갈 수 있음). 고치지 않았고 시험이 지금 동작을 기록(`Assert.Equal("Off", …)` 주석 "D6: 지금 동작"). 바꾸려면 "지운 뒤에만 나가기(실패 시 3D 유지)" — 사용자 결정 필요.
4. **B-28 PostJson 대체**: WebView2 없이는 `Map3DHost.PostJson`(ready · CoreWebView2 필요)을 가로챌 수 없다 — 실제 「이 영역 3D 로 보기」 → 펼침까지 VM 경로로 간 뒤 `Reveal3D` 가 방위를 셈하는 입력(`_map3DAreaViewJob == _map3DJob` · 장면 캐시 rect)과 같은 식 `Map3DMath.EnterBearing(MainMap.Bearing, rect)` = 300 을 확인(+ 순수 함수 시험). 실제 메시지는 헤디드 H-14.
5. **B-06 가운데 허용치**: 디지털 줌 배율 ≠1 이면 `VisibleScreenCorners` 의 반폭이 정수가 아니고 `(int)` 로 px 를 자르므로 가운데가 최대 ~1 px 어긋난다(16.5 · −30° 에서 위경도 5자리 단언이 약 1 m 차로 실패). 1 px 미만 화면 오차라 결함 아님 — 기존 시험과 같은 "2 px 안" 허용치로 단언.
6. **설정 저장 회피**: 판 [만들기]에서 고른 방식이 설정의 마지막 값과 다르면 `MapSettingsHelper.SaveMap3DAsync` 가 실행 폴더 Config\appsettings.json 을 쓴다(AppContext.BaseDirectory — 경로를 바꿀 길 없음). 새 시험은 설정의 마지막 값을 고를 방식과 같게 두어 저장 경로를 타지 않는다(정규화는 기존 Ui/Map3DBakeTests 몫). 영역 줌 로컬 파일은 기존 장치대로 임시 경로(`Map3DAreaZoomStore(%TEMP%\map3d-areas-*.json)`, 끝에 지움). 시험 이미지는 `%TEMP%\map3d-bake-*` 에 만들고 끝에 지움.
7. **B-12 관찰**: ESC 뒤 늦게 온 `OnAnchorAreaDrawn` 은 3D 판은 열지 않지만 앵커(사이트 고정 영역) 입력 칸을 채우는 쪽으로 떨어진다. 실제 앱에서는 ESC 가 `CancelRubberBand` 로 진행 중 러버밴드 · 캡처를 내려 놓기 이벤트 자체가 안 오므로(VF-17) 문제 아님 — 기록만.
8. **작업 환경**: 받은 worktree 가 COORD(0d732769)보다 옛 커밋(f7643f9e, main 병합)이었고 `git switch` 는 권한 거부 — `git diff --binary HEAD 0d732769 | git apply` 로 **작업 트리 내용만** COORD 와 같게 맞춘 뒤 작업(커밋 · 체크아웃 · stash 없음). Onvif 서브모듈(`Ironwall.Dotnet.Libraries.OnvifSolution`)이 비어 있어 COORD 에서 소스만 복사(빌드용, 패치에 없음). 패치는 COORD 파일과 직접 비교해 만들었다(경로 a/ b/ = 저장소 상대). 하네스 가드가 worktree 밖 쓰기를 막아 이 보고서 · 패치 · 새 파일은 작업 worktree 의 `.harness-out\`(같은 구조)에 두었다 — worktree 루트 `BLOCKED.md` 참고.
9. **시험 중 WebView2 방지**: 줌 15 이상 · 영역 안이면 게이트가 미리 불러오기(WebView2 생성)를 한다 — 새 시험은 모두 그 장면을 호스트에 "열어 둔" 상태로 꽂거나(HostReady) 미리 준비 줌 밑(15.5 < 16)에서 돈다. 모든 시나리오 끝에 `Map3DHost.HostMode == "none"` 단언(기존 장치).

## 3. 헤디드 전용(HEADED 용) — 단계 · AutomationId · 기대 · 판정

공통: 판정 로그는 앱 로그(UTF-8)의 `[Map3D]` 줄. 상태 줄 = 지도 아래 조준 상태(AimStatusMessage). 3D/2D 는 지도 루트 ItemStatus "3D"/"2D"(컴포지션 WebView2 는 UIA 트리에 없다).
「만들기 취소」 id `GMaps.Map3D.CancelBake` 는 3곳 — 진행 판(`GMaps.Map3D.Progress` 아래), 배지 우클릭 ContextMenu, 지도 메뉴 `GMaps.Map3D.AreaList` 하위 메뉴 — 부모 범위로 구분한다.

| # | 단계 | AutomationId | 기대 | 판정(UIA 값 / 로그 줄 / 캡처) |
|---|---|---|---|---|
| H-1 | 지도 30° 로 돌림 → 「3D 영역 그리기」 → 실제 마우스로 400×200 px 러버밴드 | `GMaps.Map3D.DrawButton` | 마퀴 옆 크기 라벨(가로 × 세로 m), 놓으면 만들기 판 | `GMaps.Map3D.BakeOptions` 보임 · 로그 `[Map3D] 만들기 패널 — 영역 안 오버레이 이미지 {n}개, 기본={mode}, 줌={z}` · 캡처(마퀴 = 화면 사각형) |
| H-2 | 그리기 모드에서 3×3 px 드래그(4 px 미만) | `GMaps.Map3D.DrawButton` | 무시 — 판 안 열림 · 그리기 모드 유지(십자 커서) | `GMaps.Map3D.BakeOptions` 없음 · `만들기 패널` 로그 없음 · 상태 줄 "지도에서 드래그해 3D 로 만들 영역을 그리세요 (Enter = 지금 화면 · ESC = 취소)" 그대로 |
| H-3 | 2 km 넘게 그림 | — | 다시 그리기 모드 + "영역이 너무 큽니다 …" | 로그 `[Map3D] 영역이 너무 큼 {가로 × 세로} → 다시 그리기` |
| H-4 | 회전 영역이 있는 지도를 키 · 나침반 · 3D 동기로 돌림(팬 없이) | `GMaps.Map3D.AreaBadge.{id}` | 테두리 · 배지가 다음 프레임에 새 방위로(옛 방위로 남지 않음) | 캡처 2장(전/후, 팬 없음) — 꼭짓점이 영역 모서리에 붙음 |
| H-5 | 영역 안 이미지가 있는 곳에 영역 그리기 → 판 라디오 4개를 차례로 누름 → [만들기] | `GMaps.Map3D.BakeOptions.Modes` · `.Extract` · `.Photo` · `.Mask` · `.Tiles` · `.EnterZoom` · `.Create` · `.Title` · `.Summary` | 라디오 하나만 선택 · Summary "영역 안 오버레이 이미지 n개" · Title "3D 만들기" | 로그 `[Map3D] 굽기 요청 bbox=[…] rect={…|-} overlay={mode} images={n} enterZoom={z}` |
| H-6 | 이미지 없는 곳 영역 → 판 | `GMaps.Map3D.BakeOptions.Modes` | 라디오 숨김, 줌만 | Summary "영역 안 오버레이 이미지 0개" · 로그 `overlay=tiles images=0` |
| H-7 | 판 [취소] | `GMaps.Map3D.BakeOptions.Cancel` | 판 닫힘 · 요청 없음 | 로그 `[Map3D] 굽기 방식 고르기 취소` · `굽기 요청` 줄 없음 |
| H-8 | 굽는 중 진행 판 「만들기 취소」 | `GMaps.Map3D.Progress`(.Title/.Detail) 아래 `GMaps.Map3D.CancelBake` | 진행 판 닫힘 · "3D 만들기를 취소했습니다 — 이전에 만든 3D 는 그대로입니다" | 로그 `[Map3D] 만들기 취소 요청 job={job}` → `[Map3D] 만들기 취소 응답 job={job} result=…` → `[Map3D] 굽기 진행 조회 끊음(만들기 취소)` |
| H-9 | 영역 배지 우클릭(다시 만드는 중 아님) | `GMaps.Map3D.AreaBadge.{id}` | 메뉴: 이 영역 3D 로 보기 · 3D 줌 바꾸기… · (직접 입력 높이 n개 — 모두 자동으로…) · 다시 만들기 · 지우기… | UIA 메뉴 항목 이름 |
| H-10 | 「다시 만들기」 → [만들기] → 굽는 중 같은 배지 우클릭 · 「3D 영역 목록」 | `GMaps.Map3D.AreaBadge.{id}` · `GMaps.Map3D.AreaList` | 맨 위 「만들기 취소」(`GMaps.Map3D.CancelBake`), **「다시 만들기」 · 「지우기…」 없음**(D1), 목록 줄 끝 "· 다시 만드는 중" | UIA 메뉴 항목 · 로그 `[Map3D] 영역 다시 만들기 id={id}` |
| H-11 | H-10 전에 열어 둔 옛 메뉴의 「지우기…」를 굽기 시작 뒤 누름(가능하면) | — | 확인 창 없이 "만드는 중에는 지울 수 없습니다 — 「만들기 취소」 뒤 지우세요" | 로그 `[Map3D] 다시 만드는 중 — 영역 지우기 막음 id={id}` |
| H-12 | 다시 만들기 끝 | — | 새 장면 · 옛 영역 사라짐 | 로그 `[Map3D] 다시 만들기 — 옛 장면 지움 {old} → {new}` · `[Map3D] 장면 내려놓음(rebake) job={old} 보던중=False` · 상태 줄 "3D 준비됨 — …" |
| H-13 | scene-missing 실제 흐름: map-3d 웹 화면에서 영역을 지운 뒤 이 GIS 에서 그 영역 「이 영역 3D 로 보기」 | 지도 루트 ItemStatus | 2D 로 돌아오고 "이 3D 장면은 지워졌습니다 — 2D 지도로 돌아왔고 3D 영역 목록을 새로 고칩니다", 목록에서 빠짐 | 로그 `[Map3D] 3D 화면 불러오기 실패 job={id} reason=scene-missing` → `[Map3D] 장면 내려놓음(scene-missing) job={id} 보던중=…` → `[Map3D] 영역 목록 {n}개` |
| H-14 | 회전 영역 「이 영역 3D 로 보기」 | 지도 루트 ItemStatus | 3D 가 영역 회전각으로 열림(사각형이 화면에 반듯) | 로그 `[Map3D] 영역 보기 id={id} → ({lat},{lon}) zoom={z}` · `[Map3D] 3D 표시 job={id} host=…` · 캡처 |
| H-15 | map-3d 를 끈 채 보던 3D 영역 지우기 — D6 확인 | — | 2D 로 나가고 "3D 서버(map-3d)가 꺼져 있습니다", 영역 남음 | 로그 `[Map3D] 2D 복귀(area-delete) …` · `[Map3D] 영역 지우기 — 서버 닿지 않음: …` |

## 시험 결과 — 프로젝트별 통과/실패 수(명령 그대로)

기준선(수정 전, 받은 worktree 를 COORD 내용으로 맞춘 뒤): `dotnet test tests/GMaps.MapEdit.Tests --nologo --filter "FullyQualifiedName~Map3D"` → 통과 352 / 실패 0.

수정 전 새 시험(결함 증명): `dotnet test tests/GMaps.MapEdit.Tests --nologo --filter "FullyQualifiedName~Map3DAreaScenarioTests|FullyQualifiedName~Map3DBakeOptionsScenarioTests|FullyQualifiedName~Map3DCancelBakeScenarioTests"` → 실패 5 / 통과 34 — 결함 3(B-50 ×2 · B-34) + 시험 장치 2(줌 15 이상에서 미리 불러오기가 3D 호스트를 만들어 `HostMode == "none"` 단언 실패 → 시험에 HostReady 를 꽂아 고침, 제품 무관).

최종(수정 후, 손댄 프로젝트 전체):
- `dotnet test tests/GMaps.MapEdit.Tests --nologo` → **통과 537 / 실패 0**
- `dotnet test tests/GMaps.MapNav.Tests --nologo` → **통과 67 / 실패 0**
- `dotnet test tests/GMaps.Ui.Tests --nologo` → **통과 1600 / 실패 0**

새 시험 수: MapEdit 34(Map3DAreaScenarioTests 21 · Map3DBakeOptionsScenarioTests 9 · Map3DCancelBakeScenarioTests 추가 4), MapNav 7, Ui 19 — 합계 60(Theory 행 포함).
돌리지 않음: Sounds · Libraries.Db(규칙 8), Housing(손대지 않음).
정리: 시험이 만든 임시 파일(`%TEMP%\map3d-areas-*.json` · `%TEMP%\map3d-bake-*`)은 각 시험 끝에 지움(이미지 파일 잠금이 늦게 풀려 빈 `map3d-bake-*` 폴더 1개가 남은 것을 손으로 지움). 남은 프로세스 없음(dotnet test 끝남).

## 바꾼 파일 목록

제품:
- `Ironwall.Dotnet.Libraries.GMaps.Ui/Services/Map3D/Map3DAreaItem.cs` — 다시 만드는 중 「지우기…」 뺌(D1)
- `Ironwall.Dotnet.Libraries.GMaps.Ui/ViewModels/Maps/MapViewModel.Map3DAreas.cs` — `RefuseDeleteWhileRebaking` + 두 곳 호출(D1)
- `Ironwall.Dotnet.Libraries.GMaps.Ui/ViewModels/Maps/MapViewModel.Map3DBakeOptions.cs` — 판 [취소]에서 다시 만들기 표지 비움(B-34)

시험(기존 파일에 더함 — 기존 시험 무변경):
- `tests/GMaps.MapEdit.Tests/Map3DCancelBakeScenarioTests.cs` — B-45 · B-46 · B-49 · B-54

새 파일(UTF-8 BOM · CRLF, `patches/B-new/` 에 같은 상대 경로로 복사):
- `tests/GMaps.MapEdit.Tests/Map3DAreaScenarioTests.cs` — `Map3DFakeServer`(본문 기록 가짜 map-3d) + B-14 · 19 · 23 · 28 · 36/53 · 50 · 51 · 59 · 60 · 61 · 66 · 67 · 68 · 73~75
- `tests/GMaps.MapEdit.Tests/Map3DBakeOptionsScenarioTests.cs` — B-32 · 33 · 34 · 35 · 41 · 42 · 43
- `tests/GMaps.MapNav.Tests/Map3DAreaDrawScenarioTests.cs` — B-05 · 06 · 08 · 10 · 12 · 13
- `tests/GMaps.Ui.Tests/Map3DAreaEdgeTests.cs` — B-10 · 27 · 28

패치: `patches/B.patch`(제품 3 + 기존 시험 1, COORD 기준 diff).
