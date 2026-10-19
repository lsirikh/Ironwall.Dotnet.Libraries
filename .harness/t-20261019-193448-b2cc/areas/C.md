# C 3D 상호작용 시나리오 · 시험 결과

> 영역 C = MapViewModel.Map3D.cs(OnMap3DMessage · OnMap3DPick · TryHandleMap3DKey · LeaveMap3DForPanel) · MapViewModel.BuildingHeight.cs ·
> BuildingHeightPanelControl · BuildingHeightPanelStyle.xaml · Utils/Map3DBuilding · Services/Map3D/Map3DClient(건물 높이 창구) ·
> MapViewModel.Map3DWindows.cs · Utils/Map3DScreen · MapViewModel.cs 우클릭 메뉴 3D 자리.
> 뷰어 JS(map-3d)는 F 작업자 — 여기서는 연결만(F 열).

## 작업 환경 메모(코디네이터용)
- 받은 git worktree(`.claude\worktrees\agent-ae26240ef4e88d882`)가 COORD 커밋(0d732769)이 아니라 옛 main(f7643f9e)이었다. 영역 C 코드(MapViewModel.BuildingHeight.cs 등)가 없었다.
  git checkout 은 금지 · 거부되어, **COORD 소스를 robocopy 로 이 worktree 에 그대로 덮어쓴 뒤(.git 제외) 그 위에서 작업**했다. 그래서 이 worktree 의 `git diff` 는 옛 main 대비라 쓸 수 없다.
- `patches\C.patch` 는 **COORD 원본 ↔ 내 파일**을 `git diff --no-index --no-prefix` 로 만든 것(6 파일). COORD 원본 사본에 `git apply` 로 깨끗이 적용되고 결과가 내 파일과 같음을 확인했다.
- 하네스 가드가 COORD 쪽 쓰기를 막아 보고서 · 패치를 **이 worktree 의 같은 상대 경로**(`.harness\t-20261019-193448-b2cc\areas\C.md`, `...\patches\C.patch`, `...\patches\C-new\...`)에 두었다. 코디네이터가 COORD 로 복사해 주세요.

## 시나리오 표
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

## 고친 결함

### C-59/C-60(D1) 1층보다 낮은 건물의 −1층 · ↓ 가 높이를 3.0 m 로 올림(짝: 200층 상한을 넘는 건물의 +1층 · ↑ 가 400 m 로 내림)
- 원인: `Map3DBuilding.StepFloors`(Ironwall.Dotnet.Libraries.GMaps.Ui/Utils/Map3DBuilding.cs:101-102 원본) — `FloorsFromHeight` 반올림 · `Clamp(…,1,200)` 뒤 `HeightFromFloors` 를 그대로 돌려줘, 2.0 m(=1층) −1 → 1층 → 3.0 m. 층고 2 m · 500 m(=250층 → 200층) +1 → 200층 → 400 m. `KeyStep`(↑/↓) · `StepBuildingFloorCommand` · 판 키가 모두 이 함수를 쓴다.
- 수정: 내리기는 지금보다 높아지면, 올리기는 지금보다 낮아지면 지금 높이 그대로(`ClampHeight(height)`) 돌려준다(Map3DBuilding.cs). 공개 형식 변화 없음.
- 증거: 수정 전 `Map3DBuildingStepEdgeTests` 13 중 9 실패 — "Assert.Equal() Failure: Values differ / Expected: 2 / Actual: 3", "+1층이 높이를 내렸다: 500 → 400";
  `BuildingHeightEdgeScenarioTests.should_keep_low_building_height_when_minus_floor_button_or_down_key_pressed(2, 1.2)` 실패 — "Expected: 2 / Actual: 3", "Expected: 1.2 / Actual: 3". 수정 후 모두 통과.
- 비고: 지시는 "내리기"만이었으나 같은 함수 · 같은 원인의 반대쪽(+1층이 높이를 깎음)을 같은 한 줄로 막았다(결정 1).

### C-81(D2) 저장 중 3D 를 나가면 늦은 응답이 되돌리기 칸을 다시 쌓아, 다음 3D 진입에서 앞 세션 저장을 되돌릴 수 있음
- 원인: `SaveBuildingHeightAsync` 가 await 뒤 무조건 `BhUndo.Add(undo)`(MapViewModel.BuildingHeight.cs:285 원본). `Leave3D → CloseBuildingHeightPanel(clearUndo:true)` 가 칸을 비운 뒤에 응답이 와도 쌓이고, `_bhJob` 이 그대로라 같은 영역에 다시 들어가 건물을 고르면 [되돌리기] 가 켜진다. `UndoBuildingHeightAsync` 의 실패 시 `BhUndo.Insert` 도 같은 경쟁.
- 수정: 3D 세션 번호 `_bhSession`(int — 생성자 없는 VM 에서도 0) 을 두고 3D 를 나갈 때(clearUndo) 올린다. 저장 · 되돌리기 시작 때 번호를 잡아 두고, 응답 뒤 번호가 바뀌었으면 칸에 쌓지 · 되살리지 않는다(로그 `[Map3D] 건물 높이 저장 응답이 3D 를 나간 뒤 도착 — 되돌리기 칸에 쌓지 않음 id= job=`). 서버 저장 · 3D commit 은 그대로.
- 증거: 수정 전 `should_not_refill_undo_nor_offer_previous_session_undo_when_save_response_arrives_after_leaving_3d` 실패 — "Assert.Equal() Failure: Values differ / Expected: 0 / Actual: 1"(BuildingHeightEdgeScenarioTests.cs:230), `should_not_put_back_undo_step_when_undo_fails_after_leaving_3d` 실패 — "Expected: 0 / Actual: 1". 수정 후 통과.

### C-101(D3) 네이티브 창(hwnd) 3D 에서 건물을 누르면 보이지 않는 판이 열려 첫 Esc 를 가로챔
- 원인: `OnMap3DBuildingPicked` 가 호스트 방식과 상관없이 판을 연다(MapViewModel.BuildingHeight.cs:157-172 원본). hwnd 3D 는 WPF 판을 덮고, `TryHandleMap3DKey` 는 `TryCancelBuildingHeightByEscape` 를 `EscExitsMap3D` 보다 먼저 부른다(MapViewModel.Map3D.cs:403).
- 수정(지시된 선택): `Map3DHost.HostMode == "hwnd"` 이면 판을 열지 않고 3D 의 윤곽을 지우며(`building.select` null) 안내 `네이티브 창 3D 에서는 건물 높이 판을 쓸 수 없습니다`(AimStatus, 자동 숨김) + Info 로그 `[Map3D] 건물 고름 — 네이티브 창 3D 라 「건물 높이」 판을 열지 않음 id= job=`. composition(기본)은 그대로.
- 증거: 수정 전 `should_not_open_panel_and_tell_reason_and_let_esc_exit_when_building_clicked_in_native_window_3d` 실패 — "WPF 판은 네이티브 창 3D 에 가려 보이지 않는다 — 열면 안 된다". 수정 후 통과(첫 Esc 로 2D).

### C-105(D7) 잘못된 입력("abc")이 남은 채 [적용] → 앞 미리보기 값이 저장됨
- 원인: 입력 오류는 `_bhError` 만 세우고 미리보기는 그대로라 `ApplyBuildingHeightCommand.CanExecute`(IsEditable ∧ IsChanged)가 켜진 채(MapViewModel.BuildingHeight.cs:137-138 원본).
- 수정: 입력칸 오류를 따로 표시하는 `_bhInputError`(bool) — 층수 · 높이 · 층고 칸 해석 실패에서 세우고(`SetBuildingHeightInputError`, CanExecute 다시 묻기), 바른 값 · 다른 건물 · 판 닫기 · 새 저장 시도에서 내린다. CanExecute 와 `ApplyBuildingHeightAsync` 둘 다 거절(오류 줄 유지). 서버 실패 이유 줄은 이 표시를 세우지 않아 다시 [적용] 할 수 있다(C-75, 기존 시험 `should_keep_preview_and_show_reason_when_server_refuses_or_is_off` 그대로 통과).
- 증거: 수정 전 `should_refuse_apply_and_keep_error_when_invalid_input_remains` 5 경우 모두 실패 — "잘못된 입력 'abc' 이 남았는데 [적용] 이 켜져 있다"(0 · 3.5 · 600.1 · 층고 11 도 같은 글). 수정 후 통과.

## 결정
1. **C-60 함께 고침** — D1 지시는 "내리기"였으나 같은 함수의 올리기 쪽도 같은 원인으로 높이를 깎는다(층고 2 m · 450 m 이상). 같은 한 줄 조건으로 "올리기는 낮아지지 않게"를 넣었다. 되돌리려면 Map3DBuilding.StepFloors 의 `delta > 0 && next < height` 조건만 빼면 된다.
2. **C-81 기준 = 3D 세션** — "판을 닫은 뒤"를 판 닫기(취소)가 아니라 3D 나가기(칸 비움) 기준으로 잡았다. 같은 3D 안에서 저장 중 판을 취소하거나 다른 건물을 골라도 그 저장은 칸에 남아 [되돌리기] 로 되돌릴 수 있다(C-82/83 이 그 동작을 고정). 판 닫기까지 넓히려면 `_bhSession++` 를 CloseBuildingHeightPanel 의 clearUndo 조건 밖으로 옮기면 된다.
3. **C-101** — DECISIONS.md 4번(b) 그대로. 3D 가 스스로 그린 윤곽(view.js 가 클릭 때 selectBuilding)을 남기지 않게 select null 을 보낸다.
4. **C-105 범위** — 층고 칸 오류("11")도 [적용] 을 막는다(층수 환산이 이전 층고로 저장되는 것을 막음). 층고는 보기만 권한에서도 바꿀 수 있으나 [적용] 자체가 권한 필요라 영향 없음.
5. **C-56 결정 필요** — 층고 10 × 200층 = 2000 m 를 안내 없이 600 m 로 자르고 판에는 60층으로 보인다. 기존 시험 `Map3DBuildingTests.should_turn_floors_into_height_with_one_decimal_inside_limits` 가 "2000 m → 600 m 로 자름"을 고정 — 고치지 않았다. 선택지: (a) 그대로 (b) 600 m 를 넘는 층수 입력을 이유 줄로 거절 (c) 자른 뒤 "600 m 로 맞췄습니다" 안내.
6. **D5(보고만)** — `Map3DGuide.ShouldLeave3DForPanel` 은 제품 코드가 부르지 않는다(LeaveMap3DForPanel 은 `ShouldLeave3DForWindow` 사용). 그런데 `Map3DGuideTests.should_leave_3d_when_panel_needs_2d_symbol_position` 이 그 옛 규칙을 덮는다. 지우지 않았다 — 정리 여부 결정 필요.
7. **C-93 기록** — 「모두 자동으로」는 요청 전에 그 장면 판을 닫는다. 실패해도 판은 닫힌 채(되돌릴 미리보기 없음)라 그대로 두었다.

## 3. 헤디드 전용(HEADED 용) — 단계 · AutomationId · 기대 · 판정
| # | 단계 | AutomationId | 기대 | 판정(UIA 값 / 로그 줄 / 캡처) |
|---|---|---|---|---|
| H-C1 | 3D(composition)에서 카메라 상자를 연 채 지도를 관성으로 크게 돌리고 줌 | GMaps.Map3D.View · GMaps.Map3D.OverlayLayer · GMaps.CameraPopup.Video | 상자가 마커를 따라가며 앞뒤로 떨리지 않음(C-38/C-40) | 캡처 연속 3장 · 체감. 로그 `[Map3D][perf]` 줄 |
| H-C2 | 관성 회전 중 상자 머리 띠를 잡고 끌기 → 놓기 | GMaps.CameraPopup.* | 끄는 동안 마우스만 따름, 놓은 뒤 3D 자리 규칙으로(C-40) | 캡처 · 2D 로 나가도 같은 자리 관계(C-34) |
| H-C3 | 3D 카메라 마커 실제 더블클릭(400 ms 안 두 번) | GMaps.Map3D.View | 상자 1개만 열림 · click 두 번으로 카드가 먼저 열리지 않음(C-25) | 로그 `[Map3D] pick dblclick kind=pids id=…` 1줄 |
| H-C4 | 3D 에서 오른쪽 버튼으로 끌기(회전) 후 놓기 | GMaps.Map3D.View | 우클릭 메뉴가 뜨지 않음(끌기는 context 아님) | 로그에 `3D 위 우클릭 메뉴 열기` 없음 |
| H-C5 | 3D 마커 우클릭 | GMaps.Map3D.View | 메뉴가 누른 자리에 뜸(DPI 150% 포함, C-27) | 로그 `[Map3D] 3D 위 우클릭 메뉴 열기 — 항목 n개 @(x,y)` · `3D 위 우클릭 메뉴 닫힘 — 열린 지 … ms` · 캡처 |
| H-C6 | 원격(RDP) = hwnd 3D 에서 카메라 더블클릭 | GMaps.Map3D.View(네이티브 창) | 2D 로 돌아와 상자가 보임(C-24) | 로그 `[Map3D] 3D 표시 job=… host=hwnd` → `[Map3D] 2D 복귀(camera-popup)` |
| H-C7 | hwnd 3D 에서 건물 클릭 → Esc | — | 판 안 뜸 · 안내 글 · 첫 Esc 로 2D(C-101) | 로그 `[Map3D] 건물 고름 — 네이티브 창 3D 라 「건물 높이」 판을 열지 않음` → `[Map3D] 2D 복귀(esc)` |
| H-C8 | 판 손잡이를 끄는 중 Esc | Map.BuildingHeight.Handle · Map.BuildingHeight.Height | 끌기만 취소(끌기 전 높이), 판 · 3D 유지 | UIA Map.BuildingHeight.Height 값 = 끌기 전 · 지도 ItemStatus "3D" |
| H-C9 | 판 층수 칸에서 Ctrl+Z · Shift+↑ · Shift+↓ | Map.BuildingHeight.Floors · .Undo · .Height | Ctrl+Z = 되돌리기(서버 요청), Shift+↑ = +0.1 m(C-102) | Map.BuildingHeight.Height 값 · 로그 `[Map3D] 건물 높이 저장 id=…` |
| H-C10 | 층수 칸에 "10" 입력 직후(포커스 그대로) [적용] 클릭 | Map.BuildingHeight.Floors · .Apply · .Summary | (D4, 고치지 않음) [적용] 이 비활성이라 클릭이 LostFocus 를 못 일으키는지 확인 — Enter 로는 반영(C-103) | Map.BuildingHeight.Apply IsEnabled · Summary 글 |
| H-C11 | 판 열린 채 3D 빈 땅 클릭 | Map.BuildingHeight.Panel | 판 유지, 3D 윤곽만 사라짐(C-12) — 어긋남 체감 기록 | 캡처 |
| H-C12 | 판에서 층수 · 높이 · 손잡이를 바꿈 | Map.BuildingHeight.Floors · .Height · .Handle · .Summary | 3D 의 그 건물만 즉시 높이 바뀜(미리보기), [취소] 로 원래대로 | 캡처 전/후 · Summary "적용하면 a m → b m(n층)" |
| H-C13 | 낮은 건물(2 m 미만) −1층 · ↓ | Map.BuildingHeight.MinusFloor · .Height | 높이 그대로(C-59 수정 확인) | Map.BuildingHeight.Height 값 불변 |
| H-C14 | 잘못된 값("abc") 남긴 채 [적용] | Map.BuildingHeight.Height · .Error · .Apply | [적용] 비활성 · 오류 줄 유지(C-105 수정 확인) | Map.BuildingHeight.Apply IsEnabled=false · Error 글 |
| H-C15 | 저장 누르고 곧바로 「나가기」 → 다시 3D → 같은 건물 | GMaps.Map3D.ExitButton · Map.BuildingHeight.Undo | 다시 들어온 판의 [되돌리기] 비활성(C-81 수정 확인) | 로그 `[Map3D] 건물 높이 저장 응답이 3D 를 나간 뒤 도착 — 되돌리기 칸에 쌓지 않음`(응답이 늦을 때만) |

### AutomationId 목록
- 판: `Map.BuildingHeight.Panel` · `.Close` · `.Current` · `.Source` · `.ReadOnly` · `.FloorHeight` · `.MinusFloor` · `.Floors` · `.PlusFloor` · `.Height` · `.Handle` · `.Error` · `.Summary` · `.Apply` · `.Cancel` · `.Reset` · `.Undo` · 도움말 `Help.Map.BuildingHeight.Panel`
- 3D: `GMaps.Map3D.View`(Map3DViewHost.cs:232) · `GMaps.Map3D.OverlayLayer` · `GMaps.Map3D.ExitButton` · `GMaps.Map3D.Exit` · `GMaps.Map3D.Enter` · `GMaps.Map3D.Progress` · `GMaps.Map3D.Chip` · `GMaps.Map3D.AreaList` · 지도 루트 ItemStatus = "3D"/"2D"(Map3DStatusText)
- 카메라 상자: `GMaps.CameraPopup.Video` · `.VideoPlaying` · `.VideoStatus` · `.VideoRetry` · `.HostRestart` · `.PtzMoving` · `.PtzReason`

### 판정 로그 줄(코드의 정확한 문자열, `…` = 값)
- `[Map3D] 건물 고름 id=… 높이=… 출처=… job=…`
- `[Map3D] 건물 고름 — 네이티브 창 3D 라 「건물 높이」 판을 열지 않음 id=… job=…`(새)
- `[Map3D] 건물 높이 저장 id=… → … (…) job=…`
- `[Map3D] 건물 높이 저장 응답이 3D 를 나간 뒤 도착 — 되돌리기 칸에 쌓지 않음 id=… job=…`(새)
- `[Map3D] 건물 높이 저장 — 서버 닿지 않음: …`(Warning) · `[Map3D] 건물 높이 저장 실패: …`(Error)
- `[Map3D] 「건물 높이」 판 닫기 — 3D 로 보내기 실패(격리): …`(Error) · `[Map3D] 건물 고르기 처리 실패(격리): …`(Error)
- `[Map3D] 건물 높이 모두 자동으로 id=… 되돌림=…` · `[Map3D] 건물 높이 모두 자동으로 — 서버 닿지 않음: …` · `[Map3D] 다시 붙이지 못한 직접 입력 id=… n=…`
- `[Map3D] pick click|dblclick|context kind=…|- id=…|- → 마커|빈 곳` · `[Map3D] pick … kind=track id=… → 동작 없음(2D 와 같음)` · `[Map3D] pick … kind=image id=… → 오버레이 이미지|없음` · `[Map3D] pick 처리 실패(격리): …`
- `[Map3D] 3D 위 우클릭 메뉴 열기 — 항목 n개 @(x,y)` · `[Map3D] 3D 위 우클릭 메뉴 닫힘 — 열린 지 … ms`
- `[Map3D] 2D 복귀(camera-popup|camera-stream|component-card|symbol-detail|esc|exit-button) zoom=… bearing=…` · `[Map3D] 3D 표시 job=… host=composition|hwnd|none`
- `[Map3D] 창 자리 처리 실패(격리): …` · `[Map3D] 지켜볼 목록 보내기 실패(격리): …` · `[Map3D] 3D 창 따라가기 시작 실패(격리 — 창만): …`

## 시험 결과 — 프로젝트별 통과/실패 수(명령 그대로, 작업 worktree 에서)
- `dotnet test tests/GMaps.Ui.Tests --nologo` → 통과! 실패 0, 통과 1594(기준선 1581 + 새 13)
- `dotnet test tests/GMaps.MapEdit.Tests --nologo` → 통과! 실패 0, 통과 530(기준선 503 + 새 27)
- `dotnet test tests/GMaps.MapAux.Tests --nologo` → 통과! 실패 0, 통과 77(기준선 76 + 새 1)
- 수정 전 실패 확인: `dotnet test tests/GMaps.Ui.Tests --nologo --filter "FullyQualifiedName~Map3DBuildingStepEdgeTests"` → 실패 9 · 통과 4 / `dotnet test tests/GMaps.MapEdit.Tests --nologo --filter "FullyQualifiedName~BuildingHeightEdgeScenarioTests|FullyQualifiedName~Map3DPickScenarioTests"` → 실패 10 · 통과 16
- Sounds · Libraries.Db 시험은 돌리지 않았다. 창 · WebView2 · 브라우저를 띄우지 않았다(우클릭 메뉴는 MenuTrap 으로 HwndSource 0 단언).

## 바꾼 파일 목록
- 제품
  - `Ironwall.Dotnet.Libraries.GMaps.Ui/Utils/Map3DBuilding.cs` — StepFloors(C-59/C-60)
  - `Ironwall.Dotnet.Libraries.GMaps.Ui/ViewModels/Maps/MapViewModel.BuildingHeight.cs` — hwnd 거절(C-101) · `_bhSession`(C-81) · `_bhInputError` · `SetBuildingHeightInputError`(C-105)
- 시험(새 파일, UTF-8 BOM · CRLF)
  - `tests/GMaps.Ui.Tests/Map3DBuildingStepEdgeTests.cs`
  - `tests/GMaps.MapEdit.Tests/BuildingHeightEdgeScenarioTests.cs`
  - `tests/GMaps.MapEdit.Tests/Map3DPickScenarioTests.cs`
  - `tests/GMaps.MapAux.Tests/Map3DContextMenuScenarioTests.cs`
- 기존 시험 파일은 바꾸지 않았다.
