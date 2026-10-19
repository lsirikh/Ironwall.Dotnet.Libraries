# F(+ B · C · E 의 map-3d 쪽) 시나리오 · 시험 결과

작업 위치: `COORD\artifacts\map3d`(복사본, commit 없음). 원본 `C:\workspace_python\map-3d` 는 건드리지 않았다. 패치: `COORD\.harness\t-20261019-193448-b2cc\map3d.patch`(git diff + 새 파일 4개를 `git diff --no-index /dev/null` 형식으로 붙임).
시험 파일 약칭: coord = tests/test_scenarios_coord.py · bc = tests/scenario_bc_check.mjs(coord 의 node 시험이 부름) · E = tests/test_scenarios_e.py · F = tests/test_scenarios_f.py.

## 시나리오 표

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

## 고친 결함

### B-D4 영역 밖 추적 표식이 허공에 뜸
- 원인: `map3d/static/overlay3d.js` refreshVisibility(183~193줄)가 `this.nodes` 만 itemInArea 로 숨기고 `trackNodes` 는 보지 않음.
- 수정: applyTrack 에서 표식을 만들 때 같은 판정(`_inArea` = itemInArea + areaOf)으로 `inArea` · `group.visible` 설정(overlay3d.js).
- 증거: 수정 전 bc 실패 — "B-72 track outside area -> hidden (true)", "B-72 track leaves area again -> hidden (true)" / 수정 후 "scenario bc ok".

### B-D5 숨긴 마커 자리를 GIS 창이 따라감
- 원인: `overlay3d.js` anchorOf(254~258줄)가 inArea=false 노드 자리도 돌려줌. 또 view.js watchPoint(625줄)는 anchorOf 가 null 이면 GIS 가 준 위경도로 자리를 셈해, anchorOf 만 null 로 바꾸면 여전히 따라감.
- 수정: anchorOf 는 `n.inArea !== false` 일 때만 자리. 새 공개 메서드 `isOutside(kind, id)`(더하기만) · view.js watchPoint 첫 줄에서 isOutside 면 null → reportScreens 가 `{x:null, y:null, visible:false}`(화면 밖과 같은 처리, 받는 쪽이 이미 처리).
- 증거: 수정 전 bc 실패 — "B-D5 anchorOf of marker hidden outside area -> null (GIS box must not follow an undrawn marker)", "B-D5 view.js watchPoint: a marker hidden outside the scene area must not fall back to its lon/lat" / 수정 후 통과.

### C-D6 마커 A → 건물 → 마커 A 가 더블클릭
- 원인: `view.js` 왼쪽 pointerup(377~397줄) — lastUp 이 `if (ov)` 안에서만 갱신, 오버레이가 아닌 클릭에서 지워지지 않음.
- 수정: 판정을 순수 함수 `clickStep(last, ov, now)`(overlay-pick.js, `DBLCLICK_MS` 도 그리로 옮겨 export)로 빼고 view.js 처리기는 `if (ov)` 앞에서 `lastUp = step.last`(오버레이 아님 → null). 제품 동작은 그 외 같음.
- 증거: 수정 전 bc 실패 — "C-99 overlay-pick.js has no clickStep …", "C-99 view.js left pointerup: lastUp must be updated (cleared on non-overlay click) by clickStep before `if (ov)`"(옛 view.js 에 같은 단언을 돌려 false 확인) / 수정 후 통과. 한계: 처리기 배선 자체는 소스 문자열 단언(기존 *_check.mjs 방식), 판정 논리는 순수 함수 시험.

### F-D8 고위도에서 가정 해 고도 음수 → shadow_k 음수
- 원인: `pipeline.py:313~315` · `extract.py:1467~1468` 이 default_sun 고도를 자르지 않고 tan → 위도 80° 에서 −7.1° · shadow_k −0.215 → 모든 그림자 높이가 하한 3 m. 굽기(shade.py max(el,3)) · 뷰어(light.js max(3,…))와 불일치.
- 수정: `extract.SUN_EL_MIN = 3.0`(새 상수) 추가, 두 곳에서 `el0 = max(el0, SUN_EL_MIN)`. default_sun 자체(천문 값)는 그대로.
- 증거: 수정 전 coord 3건 실패 — "AssertionError: {'elevation_deg': -7.1, … 'source': 'default' …}", "{'elevation_deg': 0.0, …}", "{'elevation_deg': -7.1, 'shadow_k': -0.215, …}" / 수정 후 통과(test_quality.py 함께 17 passed).

## 결정
- C-D6b(보고만): 3D 빈 땅 클릭은 건물 선택(윤곽)만 지우고 `{type:"building"}` 류 알림이 없음 → GIS 건물 높이 판이 열린 채 남음. embed 에선 `pick` click(kind null)은 이미 보냄(view.js 398줄) — GIS 가 이것으로 판을 닫을지, 3D 가 `building` null 을 새로 보낼지 결정 필요(GIS 판 동작 변경 → 범위 밖).
- B-D5 수정 방식: anchorOf 만 null 로 하면 watchPoint 가 위경도로 되살려 효과가 없어, 공개 메서드 `isOutside` 를 더해 view.js 에서 막음(공개 형식 더하기만).
- F-D8: 하한은 가정 해에만(맞춘 해는 이미 SUN_EL_RANGE 15~75°). scene.json `sun.elevation_deg` 도 3 으로 기록됨.
- 결정 필요(E 작업자 보고): 1×1 · 한 색 불투명 그림을 extract 모드로 구우면 오버레이 범위 전체(18 878 m²) 크기 건물 1채가 나오고 1×1 이면 높이 0.0 m. 확인 시험은 "assert 1 == 0" 으로 실패해 파일에 넣지 않음(합성 지도 한정일 수 있어 결함으로 단정 안 함).
- 결정 필요(F 작업자 보고): F-33 목 폭 < 0.6 m(2×PARAPET_T)면 난간 전체가 빠짐(`buildings.py:512`) — 지금 동작으로 고정. F-54 MBTiles 없으면 요청 줌이 경고 없이 16 으로 낮아짐(`pipeline.py:186`).

## 3. 헤디드 전용(HEADED 용)
map-3d 장면 폴더(`scene.json` · `log.txt` · `scene.glb`)와 GIS 3D 를 눈으로 본다. 판정 문구 · 키는 코드 그대로.
| 단계 | 기대 | 판정 |
|---|---|---|
| 투명으로 자른 PNG 오버레이(composite) 굽기 | 불투명 모양 안만 사진, 밖은 위성 | scene.json `sources.overlay.items[].region`(투명 사용 · 꼭짓점 수 · 면적 m² · 조각 · 구멍), `removed_osm` · log.txt "오버레이 N장을 지형 텍스처에 합성(composite)" |
| 완전 투명 PNG | 아무것도 안 구움 | log.txt 경고 "불투명 부분이 너무 작음(… m²) — 투명으로 자른 모양을 확인", "… 가 영역 밖이라 굽지 않음" · `overlays_baked` == [] |
| extract 굽기 · 사진 텍스처 | 사진과 맞는 건물 벽 · 지붕에 사진 | log.txt "오버레이 N: 그림 성격 …, 건물 윤곽 …개", "사진 텍스처: 건물 N동" · `sources.overlay.items[].extract.sources`(matched · map_removed · map_kept_blur …) · `buildings[].origin`(map/national/photo) · `height_from` |
| 흐린 자리 바닥 | 흐린 곳 바닥이 사진 땅(건물 지운 것) | log.txt "오버레이 N: 흐린 자리 바닥 = 사진 땅(건물 지움 · …)" · `extract.ground`(pixels · inpaint) |
| 그림자 · 해 | 구운 그림자가 해 반대쪽 · 뷰어 해와 같은 방향 | `sun{source, elevation_deg, azimuth_deg, azimuth_source, shadow_k, baked}` · 고위도(>~75°) 장면은 `elevation_deg` 3 |
| 난간 | 4 m 이상 · 40 m² 이상 평지붕만, 중정은 안팎 | `buildings[].parapet` |
| 공장색 | 탱크 · 굴뚝 근처 낮은 건물 외벽 공업색 · 사진 벽 없는 건물은 사진색 물든 벽 | 눈 확인 |
| 축소 한계 | 영역 전체가 꽉 차는 거리 × 1.3 이상 멀어지지 않음 | 휠 끝까지 · 사방 돌려 확인 |
| 장면 밖 오버레이 · 추적 | 영역 밖 마커 · 선 · 추적 표식이 허공에 안 뜸 · 그 마커의 카메라 상자도 안 따라옴 | GIS 가 받은 `overlay.screen` 항목 `visible:false`(x · y null) |
| 마커 A → 건물 → 마커 A 빠르게 | 더블클릭(카메라 팝업) 아님, 클릭 두 번 | GIS 로그의 `pick` action "click" |

## 시험 결과
- 기준선: `& C:\workspace_python\map-3d\.venv\Scripts\python.exe -m pytest -q -p no:cacheprovider` → 234 passed(81.5 s).
- 최종: 같은 명령 → 286 passed, 0 failed(59.2 s) = 기준 234 + coord 6 + E 23 + F 23
- 부분: coord 6(+parametrize 포함) · E 23 passed(7.99 s) · F 23 passed(9.67 s) · `node tests/scenario_bc_check.mjs` → "scenario bc ok".

## 바꾼 파일 목록
- 제품: `map3d/static/overlay3d.js`(추적 영역 판정 · anchorOf · isOutside) · `map3d/static/view.js`(clickStep 사용 · watchPoint) · `map3d/static/overlay-pick.js`(clickStep · DBLCLICK_MS) · `map3d/extract.py`(SUN_EL_MIN · k0 하한) · `map3d/pipeline.py`(가정 해 하한)
- 새 시험: `tests/scenario_bc_check.mjs` · `tests/test_scenarios_coord.py` · `tests/test_scenarios_e.py` · `tests/test_scenarios_f.py`
- 패치: `COORD\.harness\t-20261019-193448-b2cc\map3d.patch`
