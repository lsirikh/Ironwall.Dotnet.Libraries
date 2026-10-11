# W3(PROP3D) 보고 — 본 세션이 작업자 보고를 옮겨 적음

## 파일(tests/GMaps.PropertyPanel.Tests 새 파일 4 · BOM 확인 · 기존 시험 · 제품 · Scenario.Shared · csproj 무변경, Housing 은 실행만)
- Property3DRig.cs — 생성자 없는 MapViewModel + 실제 지도 · 실제 속성창 공장 · 실제 Undo, DB 가짜(Update 때 모델 값 스냅샷), 필요할 때만 지도 코어(타일 캐시 끔 · 빈 공급자)
- PidsHousingPropertyScenarioTests.cs — MAP-122 · 123
- InfraShapeFencePropertyScenarioTests.cs — MAP-126 · 127 · 129 + P3D-D1 두 번째 재현
- ComponentCardZoomTiltScenarioTests.cs — MAP-149

## 결론(6개 모두 가)
| id | 결론 | 시험 | 결과 |
|---|---|---|---|
| MAP-122 | 가 | PidsHousingPropertyScenarioTests::should_lift_3d_housing_and_save_altitude_to_db_when_install_height_slider_moves · ::should_swap_3d_model_and_save_variant_to_db_when_model_variant_combo_changes · ::should_retint_3d_housing_body_without_db_write_when_tint_strength_reaches_symbol_shape · ::should_retint_3d_housing_body_without_db_write_when_color_intensity_slider_moves [P3D-D1] | 통과(D1 본 세션 수정 뒤) |
| MAP-123 | 가 | ::should_show_recolor_and_fade_fov_wedge_and_save_each_to_db_when_fov_rows_change · ::should_redraw_wedge_geometry_when_detection_range_angle_and_bearing_sliders_move · ::should_save_detection_value_to_db_when_detection_slider_moves(×3) [P3D-D2] | 통과(D2 본 세션 수정 뒤) |
| MAP-126 | 가 | InfraShapeFencePropertyScenarioTests::should_disable_floor_sliders_and_hide_floor_text_when_building_type_has_no_floors · ::should_gate_floor_rows_and_marker_floor_text_by_building_type_when_selected_in_panel(×12) | 통과 |
| MAP-127 | 가 | ::should_draw_distinct_silhouette_for_every_palette_shape_when_shape_symbols_are_created · ::should_fade_symbol_alpha_and_save_opacity_to_db_when_opacity_slider_moves · ::should_hide_and_restore_shape_and_save_show_shape_to_db_when_shape_checkbox_toggles | 통과 |
| MAP-129 | 가(반) | ::should_rebuild_3d_fence_posts_and_summary_and_save_to_db_when_fence_section_edited — 시험 bin 에 appsettings 가 없어 Symbol3D.IsEnabled=false(프로세스당 1회) → 플래그 게이트 뒤 본문(ApplyFrame)을 직접 구동, 표시 판정은 앱과 같은 FenceLod.Show3D | 통과 |
| MAP-149 | 가 | ComponentCardZoomTiltScenarioTests::should_keep_card_screen_size_and_stay_inside_map_when_zoom_steps_half_and_whole · ::should_keep_card_upright_same_size_and_inside_map_when_map_tilt_toggles · ::should_show_full_intent_mismatch_state_text_in_card_when_heater_set_off_but_observed_on | 통과 |

## 결함(작업자 보고)
- P3D-D1(MAP-122/126): 속성창 '색상 강도' 슬라이더가 3D 하우징 · 건물에 안 먹음 — 바인딩 PathError(`SelectedMarker.Shape.(house:HousingAppearance.TintStrength)` 의 house: 접두사 미해석). PidsPropertyStyle.xaml:5 · InfraPropertyStyle.xaml:6 의 xmlns:house 에 assembly 생략. 실패 시험 2(PIDS · 건물).
- P3D-D2(MAP-123): 탐지범위 · 각도 · 방향 변경이 DB 저장 요청을 안 냄(UpdatePidsSymbolAsync 0건) — DB 층은 저장하게 바뀌었는데(GMapDbSymbolService.cs:804-807 · :1996 · :4220) 속성창(GMapPropertyPidsControl.cs:643/648 · 660/665 · 677/682)이 "런타임 전용"으로 막음. 실패 시험 3.

## 관찰(결함 단정 아님)
- GeometricPropertyStyle.xaml:21-35 도형 종류 콤보가 주석 처리 — 속성창에서 도형 모양 변경 불가(의도 확인 권장).
- ComponentCardPlacement.cs:30-31 아이콘이 지도 오른쪽 밖이면 카드를 끌어오지 않음(세로는 끌어옴) — 팬 추종 설계일 수 있음.

## 실행(작업자 시점)
- PropertyPanel(`FullyQualifiedName!~ComponentCardScreenSizeTests`) `실패: 5, 통과: 274, 전체: 279` (실패 5 = D1 2 · D2 3)
- Housing(`FullyQualifiedName!~MapLocateFitTests`) `통과: 300/300`
- 새 시험 30건 3회 동일(실패 5 · 통과 25)
