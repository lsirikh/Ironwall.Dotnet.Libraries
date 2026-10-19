# E 오버레이 이미지(GIS) 시나리오 · 시험 결과

작업 위치 주의: 받은 worktree(`.claude\worktrees\agent-a33a9fc5e48972c8e`)는 `main`(f7643f9e)이라 자르기 코드가 없었고, `git switch` 는 권한으로 거부됐다.
그래서 COORD(0d732769) 소스를 worktree 안 `_e_mirror\`(bin/obj/.git 제외 복사)로 옮겨 거기서 빌드 · 시험 · 수정했다.
패치는 COORD 파일 ↔ `_e_mirror` 파일의 `diff -u`(git 헤더 형식, `-p1`)이고 COORD 에 `patch --dry-run -p1` 로 깨끗이 붙는 것을 확인했다.
COORD 의 `.harness\…\patches` · `areas` 쓰기는 하네스 가드가 막아(BLOCKED.md) 같은 상대 경로로 이 worktree 안에 두었다.

## 시나리오 표

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

## 고친 결함

### D-1 / E-18 자르기 판 안의 Delete · 방향키 · Ctrl+Z/Y 를 지도 창 처리기가 먼저 가로챔
- 원인: 창 수준 터널 `OnMapPreviewKeyDownForGroup` 은 묶음 회전 판만 빼 주고(`_groupRotationPanel.IsAncestorOf`) 자르기 판은 안 뺌 → 판의 PreviewKeyDown 보다 먼저 선택 그림 삭제 확인 · 방향키 이동 · 전역 Undo 가 돈다. (MapViewModel.cs:1254 부근)
- 수정: 같은 자리에 `if (IsImageCropPanelKey(e)) return;` — 포커스 또는 키 원본이 판 안이면 지도 처리기는 손대지 않는다(Esc 포함 전부 판 것). 판정 함수는 MapViewModel.ImageCrop.cs(`IsImageCropPanelKey` · `IsInImageCropPanel`, ContentElement 는 IsAncestorOf 예외를 피하려고 Visual/Visual3D 만).
- 증거: 수정 전 `should_leave_delete_and_arrow_keys_to_panel_when_key_comes_from_image_crop_panel` 실패 — "판에서 온 Delete 를 지도 처리기가 소비했다" / 수정 후 통과. (Ctrl 조합은 Keyboard.Modifiers 를 위조할 수 없어 Delete · 방향키로 시험 — Ctrl+Z/Y 는 같은 줄에서 같이 빠진다, 헤디드 확인 항목)

### D-2 / E-17 판이 떠 있는데 편집 모드를 끄면 판이 남고, 적용하면 편집 모드 꺼진 채 DB 수정
- 원인: `IsEditModeEnabled` 꺼짐 정리(선택 · 팔레트 · 배치 · 선 그리기)에 자르기 판이 없음. (MapViewModel.cs:8635 부근)
- 수정: 꺼짐 정리에 `CancelImageCropPanel()` — 판 VM 의 취소 명령을 실행(처음 불러오기 = 추가 안 함, 다시 자르기 = 그대로). DECISIONS 7 (b) 그대로.
- 증거: 수정 전 `should_cancel_open_crop_panel_and_add_nothing_when_edit_mode_is_turned_off` 실패 — "편집 모드를 껐는데 자르기 판이 남아 있다" / 수정 후 통과.
- 3D 진입: 3D 는 편집 모드에서 이미 막힌다(`IsMap3DBlockedByInputMode`). 단 아래 "결정" ② 참고 — Ctrl+I 는 편집 모드가 아니어도 판을 연다.

### D-3 / E-33 다시 자르기 판이 떠 있는 동안 그림이 바뀌면 원본 · 크기와 지금 경계가 섞임
- 원인: `RecropImageAsync` 가 판을 열 때 잡은 `path`/원본 픽셀 크기로 자르고, 적용 때의 `marker.ImageBounds`/`Width` 로 경계를 옮긴다 — 그 사이 Undo 로 그림이 바뀌어도 그대로 적용. (MapViewModel.ImageCrop.cs:105~113)
- 수정: 적용 직전 `marker.FilePath != path` 면 적용하지 않고 "그림이 바뀌어 자르기를 취소했습니다" + 로그.
- 증거: 수정 전 실패 — `Assert.Equal() Failure: Strings differ … Expected: "Images/Overlays/발전소_…_0.png" Actual: "…_crop_…"` / 수정 후 통과.

### D-4 / E-07 · E-08 넓이 검사는 통과하지만 남는 픽셀이 0개 → 완전 투명 PNG 저장
- 원인: `ImageCropMath.Validate` 는 넓이(≥0.5)만 본다. 픽셀은 중심이 안에 들 때만 남으므로 1×1 삼각형 · 0.8 px 띠는 0개. (ImageCropMath.cs:25)
- 수정: `Validate` 끝에 `HasAnyPixel(polygon)`(새 public 정적 메서드 — 더하기만) 검사 → "남는 픽셀이 없습니다 — 다각형을 더 크게 그려 주세요". 행마다 교차점 구간만 보고 경계는 `Contains`(MaskAndCrop 과 같은 판정)로 다시 확인. 판 VM 의 적용이 Validate 를 쓰므로 판도 막힌다.
- 증거: 수정 전 `should_block_apply_with_reason_when_polygon_keeps_no_pixel` — `Assert.Contains() Failure: Sub-string not found / String: "(통과)" / Not found: "픽셀"`, `should_keep_panel_open_with_reason_when_first_load_polygon_keeps_no_pixel` — "남는 픽셀이 없는데 적용됐다(완전 투명 PNG)", `should_agree_with_mask_…` — `MissingMethodException: ImageCropMath.HasAnyPixel 없음`(수정 전 실행 때만 리플렉션으로 바꿔 컴파일) / 수정 후 3건 통과(무작위 600개에서 MaskAndCrop 과 일치).

### D-5 / E-34 자르기 · 되돌리기 DB 저장 실패를 삼키고 Undo 를 쌓고 "잘랐습니다"
- 원인: `ApplyImageCropStateAsync` 가 `DbUpdateProcess` 예외를 로그만 하고 끝 → 호출부가 Undo Push · 성공 안내. (MapViewModel.ImageCrop.cs:160~166) `UpdateImageAsync` 는 실패 시 예외(반환 null 은 실패 아님 — 기존 시험 목이 null 을 돌려줌).
- 수정: `TryApplyImageCropStateAsync(id, state, restoreOnFailure)`(private, bool) — 그림 행 저장 실패면 false 이고 마커를 이전 상태로 되돌림. 자르기 · 되돌리기는 false 면 Undo 를 쌓지 않고 "저장하지 못해 자르지 않았습니다" / "저장하지 못해 되돌리지 않았습니다". `ApplyImageCropStateAsync`(Undo/Redo 경로, 시그니처 그대로)는 되돌림 없이 예전과 같다. 그림 행은 저장됐는데 MapLayers 경로만 실패하면 예전처럼 로그만.
- 증거: 수정 전 실패 — `Assert.Equal() Failure: Strings differ … Expected: "Images/Overlays/발전소_…_0.png" Actual: "…_crop_…"` / 수정 후 통과.

### D-6 / E-16 판이 떠 있을 때 Ctrl+I 로 또 불러오기 → 파일까지 고른 뒤 아무 안내 없이 사라짐
- 원인: `PromptImageCropAsync` 가 이미 열려 있으면 조용히 Cancel → `CopyOverlayImageWithCropAsync` null. (MapViewModel.ImageCrop.cs:41)
- 수정: `RejectWhileImageCropPanelOpen()` — `ExecuteLoadImageOverlay` 에서 권한 확인 뒤 · 파일 고르기 창 앞에, `CopyOverlayImageWithCropAsync` 첫 줄에도. "자르기 판을 먼저 닫으세요"(autoHide) + 로그. CanExecute 는 그대로.
- 증거: 수정 전 `should_tell_to_close_panel_first_…` — `Assert.Equal() Failure: Strings differ / Expected: "자르기 판을 먼저 닫으세요" / Actual: null`, `should_check_open_crop_panel_before_opening_file_picker_…` — "자르기 판 가드가 파일 고르기 창보다 앞에 없다" / 수정 후 통과. (명령 실행은 모달 파일 창이 떠서 시험하지 않음 — 소스 순서 + CopyOverlay 단계로)

### D-7 / E-36 「다각형으로 자르기…」 · 「자르기 되돌리기」 메뉴에 AutomationId 없음
- 수정: 같은 메서드의 규칙(`GMaps.Symbol.ContextMenu.DetailComponents`)을 따라 `GMaps.Symbol.ContextMenu.ImageCrop` · `GMaps.Symbol.ContextMenu.ImageUncrop`. (MapViewModel.cs ShowMarkerContextMenu)
- 증거: 수정 전 `should_show_crop_items_with_automation_ids_…` — `Expected: "GMaps.Symbol.ContextMenu.ImageCrop" / Actual: ""` / 수정 후 통과(MenuTrap 으로 실제 메뉴를 열지 않고 잡음).

### E-11 저장 폴더에 쓰지 못하면 안내 없이 실패(처음 불러오기) · 예외가 새어 나감(다시 자르기)
- 원인: 처음 불러오기 — `CopyImageToLocal` · `ImageCropStore.Save` 예외가 `CopyOverlayImageWithCropAsync` 밖으로 나가 호출부(`ExecuteLoadImageOverlay` catch)가 Error 로그만, 사용자 안내 없음. 다시 자르기 — `Save` 예외가 fire-and-forget(`_ = RecropImageAsync`) 밖으로 나가 관찰되지 않는 예외 · 안내 없음. (MapViewModel.ImageCrop.cs:80 · 109)
- 수정: IOException/UnauthorizedAccessException 만 잡아 처음 불러오기 = null + "그림을 저장하지 못해 추가하지 않았습니다 — 저장 폴더를 확인하세요", 다시 자르기 = 그대로 + "자른 그림을 저장하지 못했습니다 — 저장 폴더를 확인하세요".
- 증거: 수정 전 `should_tell_and_add_nothing_without_throwing_…` — `System.UnauthorizedAccessException : 쓰기 금지`, `should_tell_and_leave_placed_image_when_cropped_png_cannot_be_written_on_recrop` — `System.IO.IOException : Cannot create '…\blocker' because a file or directory with the same name already exists.` / 수정 후 통과.

## 결정
1. 작업 위치 — 받은 worktree 가 main 이고 브랜치 전환이 거부돼 worktree 안 `_e_mirror`(COORD 복사)에서 작업, 패치는 COORD 대비 diff. 합칠 때 `patch -p1`(또는 `git apply`)로 붙이면 된다.
2. (결정 필요) **Ctrl+I 는 편집 모드가 아니어도 동작**한다 — `LoadImageOverlayCommand` CanExecute = `MainMap != null`, 실행은 권한만 본다(MapView.xaml:34 KeyBinding). 그래서 운영 모드에서 처음 불러오기 판이 뜰 수 있고 그때는 편집 모드 끄기 경로가 없으며 3D 진입도 막히지 않는다(`IsMap3DBlockedByInputMode` 는 편집 모드 · 배타 입력만 본다). 정적 확인만 — 고치지 않음. 선택지: (a) 불러오기를 편집 모드에서만(안내) (b) 판이 떠 있으면 3D 게이트에 넣기 (c) 그대로.
3. D-1 은 판 안 키를 Esc 까지 전부 판에 맡긴다(묶음 회전 판은 Esc 만 지도로 보내지만, 자르기 판은 Esc 가 판의 2단계 닫기라 지도로 보내면 안 됨).
4. D-5 실패 때 이미 쓴 `_crop_` PNG 와 `.crop.json` 은 지우지 않는다(마커는 원래 그림으로 돌아가 쓰이지 않는 파일만 남음 — 원본은 절대 지우지 않는 기존 규칙과 같은 쪽).
5. D-6 은 CanExecute 를 바꾸지 않고 실행 시 안내(지시대로).

## 3. 헤디드 전용(HEADED 용)
판정은 UIA 값 · 앱 로그 줄 · 캡처 PNG. 안내 문구는 `SetAimStatus` 가 `[CameraAim] <문구>` Info 로그를 남기므로 그 줄로도 판정한다.

| 단계 | AutomationId / 조작 | 기대 | 판정 |
|---|---|---|---|
| H-E1 편집 모드 → 「이미지 불러오기」 | `GMaps.EditStrip.LoadImage` 클릭(또는 Ctrl+I) → 파일 고르기 대화상자에서 PNG 선택 | `Map.ImageCrop.Panel` 이 뜸 · 제목 "이미지 자르기" · `Map.ImageCrop.Skip` 보임 | UIA 존재 · 캡처 |
| H-E2 다각형 실제 그리기 | `Map.ImageCrop.Canvas` 위 클릭 4번 → 첫 점 클릭(닫기) | `Map.ImageCrop.PointCount` = "꼭짓점 4개 · 닫힘" | UIA Name/Text |
| H-E3 꼭짓점 끌기 · 가운데 손잡이 끌기 | 캔버스에서 꼭짓점을 10 DIU 넘게 끌기 / 선분 가운데 네모 끌기 | 점 이동 / 점 하나 늘어남(PointCount +1) | UIA · 캡처 |
| H-E4 휠 · 이동 · 맞추기 | 캔버스 위 휠(확대) · 가운데 단추 끌기 · Space 누른 채 왼쪽 끌기 · `Map.ImageCrop.Fit` | 커서 아래 그림 고정 확대 · 이동 · 처음 맞춤 | 캡처 전/후 |
| H-E5 더블클릭 닫기 | 3점 찍고 캔버스 더블클릭 | "· 닫힘" | UIA |
| H-E6 판 안 Ctrl+Z/Ctrl+Y | 캔버스에 포커스 두고 Ctrl+Z · Ctrl+Y · `Map.ImageCrop.Undo`/`.Redo` | 판 안 다각형만 되돌림 — **지도 Undo 가 돌지 않음**(D-1) | PointCount 변화 · 지도 그림 그대로 · 로그에 Undo 줄 없음 |
| H-E7 판 안 Delete · 방향키 | 지도에서 그림을 선택한 채 우클릭 「다각형으로 자르기…」 → 판에서 꼭짓점 누르고 Delete · ←/→ | 꼭짓점만 지워짐 · 삭제 확인 팝업 없음 · 지도 그림 안 움직임(D-1) | 팝업 UIA 없음 · 캡처 |
| H-E8 Esc 두 번 · 닫기 확인 | Esc(그리던 점 취소) → Esc → `Map.ImageCrop.CloseConfirm` 보임 → `Map.ImageCrop.KeepEditing` / `Map.ImageCrop.ConfirmClose` | 확인 띠 → 계속 / 닫힘 | UIA |
| H-E9 이유 줄 | 2점만 찍고 `Map.ImageCrop.Apply` / 1×1 그림 삼각형 적용 | `Map.ImageCrop.Error` 보임("3개" / "남는 픽셀이 없습니다") | UIA Text |
| H-E10 적용 · 그대로 · 취소 · ✕ | `Map.ImageCrop.Apply` / `.Skip` / `.Cancel` / 머리 띠 `.Close` | 자른 그림 / 원본 / 추가 안 함 | 로그 `[ImageCrop] 처음 불러올 때 자름: … 상자=(…) 꼭짓점=N` · `[ImageCrop] 오버레이 이미지 추가 취소: …` |
| H-E11 머리 띠 끌기 | 판 머리 띠(38 DIU) 끌기 | 판이 움직이고 캔버스는 그대로 | 캡처 |
| H-E12 우클릭 메뉴 | 편집 모드에서 놓인 그림 우클릭 | `GMaps.Symbol.ContextMenu.ImageCrop`, 자른 그림이면 바로 아래 `GMaps.Symbol.ContextMenu.ImageUncrop` · 운영 모드엔 없음 | UIA |
| H-E13 다시 자르기 · 되돌리기 · 지도 Ctrl+Z | 메뉴 → 적용 → 지도 Ctrl+Z → Ctrl+Y → 「자르기 되돌리기」 | 같은 자리 · 회전 유지 | 로그 `[ImageCrop] 다시 자름 id=… 회전=…` · `[ImageCrop] 자르기 되돌림 id=…` · 캡처 |
| H-E14 판 떠 있는데 Ctrl+I | 판 열린 채 Ctrl+I | 파일 창 안 뜸 · "자르기 판을 먼저 닫으세요" | 로그 `[ImageCrop] 자르기 판이 떠 있어 이미지 불러오기 막음` |
| H-E15 판 떠 있는데 편집 모드 끄기 | 편집 토글 끔 | 판 사라짐 · 그림 추가 안 됨 | 로그 `[ImageCrop] 편집 모드가 꺼져 자르기 판 취소` |
| H-E16 운영 모드 Ctrl+I(결정 ②) | 편집 모드 끈 채 Ctrl+I → 판 띄운 채 줌 15+ 3D 영역 | 현재: 판이 뜨고 3D 진입 가능 — 관찰만 | 캡처 |
| H-E17 자른 뒤 3D | 자른 그림이 있는 영역 3D 진입 | 바깥 투명(사진 텍스처가 다각형 모양) | 캡처 · map-3d 쪽은 F |

판정 로그 줄(코드 문자열 그대로):
- `[ImageCrop] 자르기 판이 떠 있어 이미지 불러오기 막음`
- `[ImageCrop] 편집 모드가 꺼져 자르기 판 취소`
- `[ImageCrop] 그림을 읽지 못해 자르기 없이 추가: {ex.Message}` (Warning)
- `[ImageCrop] 오버레이 이미지 추가 취소: {title}`
- `[ImageCrop] 처음 불러올 때 자름: {copied} → {cropped} 상자=({x},{y},{w}x{h}) 꼭짓점={n}`
- `[ImageCrop] 저장 폴더에 쓰지 못해 추가 안 함: {ex.Message}` (Error)
- `[ImageCrop] 그림 읽기 실패 {path}: {ex.Message}` (Warning)
- `[ImageCrop] 판이 떠 있는 동안 그림이 바뀌어 자르기 취소 id={id} {path} → {marker.FilePath}`
- `[ImageCrop] 자른 그림 저장 실패 id={id}: {ex.Message}` (Error)
- `[ImageCrop] 다시 자름 id={id} {path} → {newPath} 상자=({x},{y},{w}x{h}) 회전={bearing:F1}`
- `[ImageCrop] 자르기 되돌림 id={id} {FilePath} ← {record.Source}`
- `[ImageCrop] 저장 실패 id={id}: {ex.Message}` (Error)
- 안내 문구(`[CameraAim] …`): "자르기 판을 먼저 닫으세요" · "그림이 바뀌어 자르기를 취소했습니다" · "저장하지 못해 자르지 않았습니다" · "저장하지 못해 되돌리지 않았습니다" · "그림을 저장하지 못해 추가하지 않았습니다 — 저장 폴더를 확인하세요" · "자른 그림을 저장하지 못했습니다 — 저장 폴더를 확인하세요" · "잘랐습니다 — 되돌리려면 Ctrl+Z 또는 우클릭 「자르기 되돌리기」" · "자르기 전 그림으로 되돌렸습니다" · "자르기 전 그림 파일이 없어 되돌릴 수 없습니다" · "자른 그림이 아니라 되돌릴 것이 없습니다" · "그림 파일을 읽을 수 없어 자를 수 없습니다"

## 시험 결과 — 프로젝트별(수정 후, `_e_mirror` 에서)
- `dotnet test tests/GMaps.MapEdit.Tests --nologo` → 통과! 실패 0, 통과 525(기준선 503 + 새 22)
- `dotnet test tests/GMaps.MapAux.Tests --nologo` → 통과! 실패 0, 통과 79(기준선 76 + 새 3)
- `dotnet test tests/GMaps.Ui.Tests --nologo` → 통과! 실패 0, 통과 1581
- `dotnet test tests/GMaps.Housing.Tests --nologo` → 통과! 실패 0, 통과 353
- `dotnet test tests/GMaps.MapLayer.Tests --nologo` → 통과! 실패 0, 통과 97
- `dotnet test tests/GMaps.MapNav.Tests --nologo` → 통과! 실패 0, 통과 60
- `dotnet test tests/GMaps.PropertyPanel.Tests --nologo` → 통과! 실패 0, 통과 436
- 수정 전(COORD 원본 제품 파일 3개로 되돌려 같은 새 시험 실행): MapEdit `--filter FullyQualifiedName~ImageCropEdgeScenarioTests` 실패 10 / 통과 12(실패 = 정확히 결함 시험 10개), MapAux `~ImageCropMenuScenarioTests` 실패 2 / 통과 1.
- 기존 시험은 하나도 고치지 않았다. Sounds · Libraries.Db 시험은 돌리지 않았다. 창 · 브라우저 없음(메뉴는 MenuTrap, 파일 고르기 창은 소스 순서로).

## 바꾼 파일 목록
- 제품(패치 `patches/E.patch`, COORD 기준 `-p1`):
  - `Ironwall.Dotnet.Libraries.GMaps.Ui/Utils/ImageCropMath.cs` — Validate 에 남는 픽셀 검사 · `HasAnyPixel`(새 public 정적, 더하기만)
  - `Ironwall.Dotnet.Libraries.GMaps.Ui/ViewModels/Maps/MapViewModel.ImageCrop.cs` — 판 가드 · 판 취소 · 판 키 판정 · 저장 실패 처리 · 그림 바뀜 확인 · `TryApplyImageCropStateAsync`/`SetImageCropState`(private)
  - `Ironwall.Dotnet.Libraries.GMaps.Ui/ViewModels/Maps/MapViewModel.cs` — 좁게 5곳: OnMapPreviewKeyDownForGroup 한 줄(1257), ExecuteLoadImageOverlay 한 줄(3791), 메뉴 AutomationId 두 줄(7015 · 7025), IsEditModeEnabled 꺼짐 한 줄(8643) — D 작업자와 겹치면 손으로 맞출 것
- 새 시험(UTF-8 BOM · CRLF, `patches/E-new/` 에 같은 상대 경로):
  - `tests/GMaps.MapEdit.Tests/ImageCropEdgeScenarioTests.cs`(22개)
  - `tests/GMaps.MapAux.Tests/ImageCropMenuScenarioTests.cs`(3개)
