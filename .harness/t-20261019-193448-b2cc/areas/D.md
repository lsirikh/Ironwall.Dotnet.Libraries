# D 시나리오 · 시험 결과 (2D 편집 — 묶음 회전 · 레이어 패널 다중 선택 · 그룹 속성 편집 · 장비 재할당 · 기울이기 제거)

> 작업 위치: 받은 worktree(`.claude/worktrees/agent-a0ed58d30f570acbf`)의 HEAD 가 main(f7643f9e)이라 영역 D 기능이 없었다. git 쓰기 금지 규칙 때문에 checkout 대신
> COORD(0d732769) 소스를 `agent-a0ed58d30f570acbf\_D\`(bin/obj/.git 제외 복사)에서 빌드 · 시험했다. 하네스 가드가 COORD 쓰기를 막아
> 이 보고서와 패치는 내 worktree 의 같은 상대 경로(`agent-a0ed58d30f570acbf\.harness\t-20261019-193448-b2cc\areas\D.md` · `patches\D.patch` · `patches\D-new\`)에 두었다.
> D.patch 는 COORD 원본 대비 diff(경로 `a/`·`b/` 정규화) — COORD 에서 `git apply --check` 통과 확인.

## 시나리오 표

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

## 고친 결함

### D-35/D-36 ① 회전 미리보기 중 잠금 → 잠긴 멤버가 계속 돌고, 그룹 잠금이 미리보기 위치를 DB 에 저장 · Esc 는 화면만 되돌림
- 원인: `ExecuteGroupLock`(MapViewModel.cs ~1605)이 열린 회전을 닫지 않고 멤버마다 `DbUpdateProcess`(위치 포함 전체 행) 저장. `OnSymbolLockChanged`/`OnLayerLockChanged`(~10220/~10245)도 회전을 안 닫음. `GroupRotationSession` 은 IsLocked 를 시작 때만 본다(GroupRotationSession.cs:41).
- 수정: 세 처리기 권한 확인 뒤 `EndRotation(commit: true)`(MapViewModel.cs).
- 증거: 수정 전 `GRE::should_confirm_open_rotation_before_group_lock_so_escape_cannot_split_screen_and_db` — "Assert.False() Failure Expected: False Actual: True"(IsRotating), `GRE::should_confirm_open_rotation_and_stop_turning_member_when_it_is_locked_from_layer_panel` 같은 실패 / 수정 후 둘 다 통과.

### D-37 ② 회전 미리보기 중 속성 판 변경 → 미리보기 위치가 확정 없이 DB 에 저장
- 원인: `OnMarkerPropertyChanged`(MapViewModel.cs ~9026) → `ApplyGroupPropertyChangeAsync`/단일 `DbUpdateProcess` 가 열린 회전을 모름.
- 수정: 권한 확인 · 로그 뒤 열린 회전이면 먼저 확정, 그룹 경로는 적용 뒤 판 다시 채움(확정이 판을 회전 값으로 채운 뒤 이번 값까지 보이게).
- 증거: 수정 전 `GRE::should_confirm_open_rotation_before_group_property_change_is_saved` — "Assert.False() Failure Expected: False Actual: True" / 수정 후 통과.

### D-41 ③ 회전 확정 · Undo 뒤 그룹 판 회전 · 감시 방향 칸 낡음
- 원인: 그룹 판은 `EnterGroupMode`(GMapPropertyBaseControl.cs:1063) 때 한 번만 채움.
- 수정: `MapViewModel.GroupRotation.cs` 에 private `RefreshGroupPropertyPanelValues()`(열린 그룹 판이면 `panel.EnterGroupMode(panel.GroupMarkers)` 재사용) → `OnGroupRotateCompleted`(기록 · 어도너 갱신 직후) · Undo/Redo 명령 끝(MapViewModel.UndoContext.cs).
- 증거: 수정 전 `GRE::should_refill_group_panel_bearing_and_view_direction_after_rotation_confirm_and_undo` — "Values are not within 9 decimal places Expected: 20 Actual: 0" / 수정 후 통과.

### D-77 ④ 그룹 속성 저장 실패해도 Undo 기록
- 원인: `ApplyGroupPropertyChangeAsync`(MapViewModel.cs ~9015) catch 뒤에도 `RecordPropertyChange`.
- 수정: 저장 성공 멤버만 기록(단일 경로와 같게). 전원 실패면 빈 배치 = 기록 0.
- 증거: 수정 전 `GPE::should_not_record_undo_for_member_whose_group_property_save_failed` — "Collections differ Expected: [731] Actual: [732, 731]"(Undo 가 실패 멤버를 또 저장) / 수정 후 통과.

### D-63 ⑤ 레이어 패널 → 지도 선택이 줌으로 안 보이는 심볼도 묶음에 넣음
- 원인: `PushLayerSelectionToMap`(MapViewModel.LayerSelection.cs:37)이 `ZoomLadder.IsVisibleAtEffectiveZoom` 을 안 봄.
- 수정: 심볼은 실효 줌에서 보일 때만, 오버레이 이미지 행은 그대로 포함. 패널은 기존처럼 실제 지도 선택을 비춰 풀림.
- 증거: 수정 전 `LPE::should_leave_out_symbols_hidden_by_zoom_and_unmark_their_rows_when_layer_rows_go_to_map` — "Collections differ Expected: [601, 602] Actual: [601, 602, 603]" / 수정 후 통과.

### D-92 ⑥ 같은 장비를 세 마커가 가진 자료에서 재할당이 한 마커만 비움
- 원인: `OtherDeviceHolders(...).FirstOrDefault()` 하나만 대기 · 확인(MapViewModel.DeviceReassign.cs:67 · 104).
- 수정: 확인(`HandleAsync`) 때 `OtherDeviceHolders(target, device)` 전부(+대기 holder) 비움 저장 → B 연결. 중간 실패 시 비운 것까지 원복 저장. Undo 배치에 모든 holder + target(1단위). 팝업 문구 · 기존 실패 순서 그대로.
- 증거: 수정 전 `DRE::should_clear_every_other_holder_and_undo_all_together_when_three_markers_share_device` — "Values differ Expected: 0 Actual: 501"(정문2 LinkedDeviceId) / 수정 후 통과 · 기존 DRS 16건 통과.

## 결정
1. **D-26 손잡이(권한 없음)**: 손잡이 끌기 시작(`GroupSelectionService.OnAdornerRotationDragStarted`)은 권한을 보지 않아 미리보기는 된다. 확정 때 `OnGroupRotateCompleted` 가 원위치 · 안내(저장 · Undo 0)하고, 실제로는 권한 상실 = 편집 모드 강제 종료(D-33) → 선택 · 손잡이가 사라져 도달 경로가 없다 → 고치지 않음(고치려면 GroupSelectionService 에 권한 질의를 더해야 함).
2. **D-42 회전 확정 저장 실패 — 결정 필요**: Undo 를 먼저 동기 기록(설계 의도 주석)한 뒤 저장, 실패는 Error 로그 "그룹 회전 영속 실패: …" + 상태 글 "{저장 수}개 회전 {각} 저장" 뿐 — 화면 ≠ DB 인 채 Undo 남음. 선택지 (a) 그대로 (b) 실패 멤버 원위치 + Undo 제외 (c) 실패 안내 글 추가. 시험은 고정하지 않으려고 쓰지 않음.
3. **D-93 권한 없는 판 선택값**: 단일 판은 콜백에서 마커에 먼저 쓰고(`GMapPropertyPidsControl.OnLinkedDeviceChanged` 685행) VM 은 저장만 막아, 마커 · 콤보에 고른 장비가 저장 안 된 채 남는다(재시작 시 원복). 모든 단일 속성 RBAC 백스톱 공통이고 도달은 "세션 중 강등"뿐(곧 편집 모드가 꺼져 판이 닫힘) → 고치지 않음. 원하면 차단 때 `e.OldValue` 로 되돌리기.
4. ③ 다시 채우기는 그룹 판이 열려 있을 때 **모든** Undo/Redo 뒤에 돈다 → 그룹 속성 Undo 뒤에도 판이 따라온다(D-74 · D-75, 이전엔 낡은 값). 기존 시험 영향 없음.
5. ② "속성 변경 = 확정"은 그룹 · 단일 두 경로 공통(회전 세션은 묶음 선택일 때만 열림).
6. 기존 시험이 틀린 동작을 고정한 사례: 없음.

## 3. 헤디드 전용(HEADED 용) — 단계 · AutomationId · 기대 · 판정
공통: 편집 모드 켜고 도형 3개 이상. 판정 로그 `[CameraAim] …` 은 `SetAimStatus` 가 남기는 줄(MapViewModel.cs:1133), 앱 로그 UTF-8.

| # | 단계 | AutomationId | 기대 | 판정 |
|---|---|---|---|---|
| H-D1 | 러버밴드 2개 → 손잡이 실제 끌기 시계 ~90° → 놓음 | Map.GroupSelection.RotateHandle → Map.GroupRotation.Panel | 끄는 동안 미리보기 · 놓아도 판 유지 · Angle ≈ 90.0 | UIA Map.GroupRotation.Angle · 로그 "그룹 선택 2개" |
| H-D2 | Shift 누른 채 손잡이 끌기 | Map.GroupSelection.RotateHandle | 15° 단위만 | UIA Angle 15.0/30.0… |
| H-D3 | 중심 십자 끌기 | Map.GroupRotation.CenterHandle · .CenterLat · .CenterLon | 위경도 칸 따라 바뀜 · 새 중심 기준 회전 | UIA 값 |
| H-D4 | 「지도에서 고르기」 → 지도 클릭 / 다시 켜고 Esc | Map.GroupRotation.PickCenter | 십자 커서 → 클릭 자리 중심 / Esc = 고르기만 그만 | 로그 "[CameraAim] 지도를 눌러 회전 중심 고르기 — (ESC = 그만)" |
| H-D5 | 각도 칸 · 막대 포커스 중 ←/→ · Esc | Map.GroupRotation.Angle · .Slider | ←/→ 가 묶음 이동 · 확정으로 안 샘 · Esc 만 취소 | 로그 "[CameraAim] 회전 취소" · 위치 원래대로 |
| H-D6 | ±1 · ±0.1 · 확정 · 취소 · ✕ | Map.GroupRotation.Plus1/.Minus1/.Plus01/.Minus01/.Confirm/.Cancel/.Close | 확인 글(.Summary) · 종류 요약(.Kinds) 갱신 · 확정 = 저장 · 취소/✕ = 원위치 | 로그 "[CameraAim] 2개 회전 +N.N° 저장" / "[CameraAim] 회전 취소" |
| H-D7 | 각도 "abc" | Map.GroupRotation.Angle · .Error | 오류 글 · 지도 그대로 | UIA Error 글 |
| H-D8 | ] [ Shift+] Enter Esc 실제 키 | (지도 포커스) | ±1° · ±0.1° · 확정 · 취소 | 로그 "[CameraAim] 회전 +N.N° · Enter 확정 · Esc 취소" |
| H-D9 | 미리보기 중 묶음 끌기 · 방향키 · 묶음 메뉴 잠금 · 속성 판 값 변경 | — | 각각 먼저 확정 후 그 조작 · Esc 로 회전 안 돌아감(①②) | 로그 "[CameraAim] …회전 … 저장" 이 그 조작 앞 |
| H-D10 | 레이어 패널 → 행 실제 클릭 + Ctrl / Shift / Ctrl+Shift | GMaps.Toolbar.LayerPanelButton · GMaps.LayerPanel.Symbol.{Id} · GMaps.LayerPanel.Row.{Id} | 지도 선택 = 같은 집합(1 = 단일, 2+ = 묶음 · 공통 속성창) | 로그 "그룹 선택 {n}개" · "그룹 공통 속성창 표시 — {n}개, …" · UIA SelectionItem |
| H-D11 | 패널 행 초점 ↑↓ · Space · Ctrl+A · Esc | GMaps.LayerPanel.Symbol.{Id} | 패널 선택만 · 지도 묶음 안 움직임 | 로그에 "격자 이동" 없음 |
| H-D12 | 줌 낮춰 안 보이는 심볼 섞인 범위 Shift(⑤) | GMaps.LayerPanel.Symbol.{Id} | 안 보이는 심볼 빠짐 | 로그 "그룹 선택 {n}개" 의 n |
| H-D13 | 같은 종류 2개 → 그룹 판 칸 실제 입력 · 빈칸 확인 → Ctrl+Z | GMaps.Property.*(예 GMaps.Property.Title · .Altitude · .FenceHeight) | 전원 적용 · Ctrl+Z 한 번 · 판 따라옴(③) | 로그 "속성창 변경에 의한 마커 속성 변경: Width = …" |
| H-D14 | 묶음 회전 확정 후 그룹 판 방향 · 감시 방향 칸 | GMaps.Property.* | 새 값(③) | UIA 값 |
| H-D15 | 다른 마커가 가진 장비를 콤보에서 고름 → 예 / 아니오 · 닫기 | (속성 판 장비 콤보) | 예: 옛 마커(들) 비움 · 연결 / 아니오: 원래 장비 | 로그 "[장비 재할당] '…'(…) 은 '…' 에 연결됨 — '…' 로 옮길지 확인" → "[장비 재할당] '…' : '…' → '…'" |
| H-D16 | Ctrl+↑ · Ctrl+↓ (지도 포커스) | — | 변화 없음(기울이기 제거) · Ctrl+←/→ 는 회전 모드일 때만 회전 | 화면 캡처 |

## 시험 결과 — 프로젝트별 통과/실패 수(명령 그대로, 작업 복사본 `_D` 에서)
- `dotnet test tests/GMaps.MapEdit.Tests --nologo` → 통과 537 · 실패 0 (기준선 503 + 새 34)
- `dotnet test tests/GMaps.MapLayer.Tests --nologo` → 통과 97 · 실패 0
- `dotnet test tests/GMaps.PropertyPanel.Tests --nologo` → 통과 436 · 실패 0
- `dotnet test tests/GMaps.MapNav.Tests --nologo` → 통과 60 · 실패 0
- `dotnet test tests/GMaps.Housing.Tests --nologo` → 통과 353 · 실패 0
- `dotnet test tests/GMaps.Ui.Tests --nologo` → 통과 1581 · 실패 0
- 수정 전 실패 확인: `--filter "FullyQualifiedName~GroupRotationEdgeScenarioTests"` 5 실패(①×2 ②③ + D-33 시험 장치 NRE — 장치 고친 뒤 통과), `~LayerPanelSelectionEdgeTests` 1 실패(⑤), `~GroupPropertyEditEdgeTests` 1 실패(④), `~DeviceReassignEdgeTests` 1 실패(⑥).

## 바꾼 파일 목록
- 제품(patches/D.patch): `Ironwall.Dotnet.Libraries.GMaps.Ui/ViewModels/Maps/MapViewModel.cs`(ExecuteGroupLock · OnGroupRotateCompleted · ApplyGroupPropertyChangeAsync · OnMarkerPropertyChanged · OnSymbolLockChanged · OnLayerLockChanged — E 의 OnMapPreviewKeyDownForGroup 은 건드리지 않음) · `MapViewModel.GroupRotation.cs`(RefreshGroupPropertyPanelValues 추가) · `MapViewModel.UndoContext.cs`(Undo/Redo 명령 끝 1줄씩) · `MapViewModel.LayerSelection.cs`(PushLayerSelectionToMap 조건) · `MapViewModel.DeviceReassign.cs`(HandleAsync)
- 새 시험(patches/D-new/tests/GMaps.MapEdit.Tests/, UTF-8 BOM): `GroupRotationEdgeScenarioTests.cs`(18) · `LayerPanelSelectionEdgeTests.cs`(4) · `GroupPropertyEditEdgeTests.cs`(4) · `DeviceReassignEdgeTests.cs`(8, Theory 3 포함)
- 기존 시험 · 공개 형식: 바꾸지 않음(private 메서드 1개 추가뿐).
