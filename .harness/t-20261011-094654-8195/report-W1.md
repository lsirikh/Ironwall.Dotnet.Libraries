# W1(NAV) 보고 — 본 세션이 작업자 보고를 옮겨 적음

## 파일(tests/GMaps.MapNav.Tests 새 파일 · BOM 확인 · 기존 시험 · NavXaml.cs · Scenario.Shared · 제품 코드 무변경)
- MapNavRemainingScenarioTests.cs — MAP-001 · 013 · 014 · 021 · 022 · 023 · 029 · 039 + 도우미 NavRemainingKit
- MapToolbarScenarioTests.cs — MAP-194 · 195 · 196 · 040 + NAV-D1 결함 시험 2
- 실행 시 bin 아래 MAP-040 용 mbtiles-fixture/w1-*.mbtiles 생성(있으면 재사용)

## 결론
| id | 결론 | 시험 또는 이유 | 결과 |
|---|---|---|---|
| MAP-001 | 가 | MapNavRemainingScenarioTests::should_expose_map_root_and_only_known_marker_types_when_symbols_loaded — 부팅 적재 루프로 심볼 7종 → 지도 자동화 피어 DataItem 이름 전부 아는 종류 · 7개 · 적재 오류 로그 0, GMaps.Map.Root · MainMap 은 XAML 원문 | 통과 |
| MAP-011 | 나 | 메뉴는 대상 없는 OpenSetupPanelMessageModel 만 발행(MapViewModel.cs:9267). 설정 패널 · 지도 카드는 호스트(ConductorControlViewModel.HandleAsync:331 → SetupPanelViewModel)라 이 저장소에서 창 없이 못 띄움. 메뉴 → 발행은 기존 ZoomViewScenarioTests::should_publish_setup_panel_open_when_zoom_range_menu_chosen 이 증명 | — |
| MAP-013 | 가 | ::should_move_center_and_return_when_empty_spot_dragged_and_dragged_back_in_operation_mode — 120px 끌면 가운데 서쪽 120px, 되돌리면 복귀(누름 · 뗌 실제 이벤트, 포인터 이동만 벤더 코어 BeginDrag/Drag) | 통과 |
| MAP-014 | 가 | ::should_pan_without_rubber_band_or_selection_when_empty_spot_dragged_in_edit_mode_without_shift — 러버밴드 0 · 팬 · 선택 없음 | 통과 |
| MAP-021 | 가 | ::should_show_tilt_badge_and_foreshorten_map_when_tilt_toggle_clicked_above_gate_zoom — 18.5 · 토글 클릭 → ToggleState On · 「기울임 20°」 · 세로/가로 = cos20° | 통과 |
| MAP-022 | 가 | ::should_hold_then_flatten_then_reenter_tilt_when_zoom_goes_18_5_17_5_17_0_18_5 | 통과 |
| MAP-023 | 가 | ::should_show_35_degrees_in_badge_and_angle_text_when_angle_slider_set_to_35 — AngleText "35°" · 「기울임 35°」 · cos35° | 통과 |
| MAP-029 | 가 | ::should_hide_badge_and_restore_identical_view_state_when_tilt_turned_off — 배지 · 각도 판 닫힘, 그림을 정하는 상태(변환 · 각 · 오버스캔 · 범위 · 가운데 · 줌 · Margin)가 켜기 전과 같음 | 통과 |
| MAP-039 | 가(부분) | ::should_keep_compass_position_after_round_trip_and_clamp_display_in_small_window — 실제 템플릿 · MapView 결선, 왕복 뒤 설정 파일 300,200, 320×240 표시 클램프 · 원본 보존 · 복원. 끌기 포인터 이동 자체는 실창 몫 | 통과 |
| MAP-040 | 가 | MapToolbarScenarioTests::should_switch_base_tiles_and_keep_combo_and_menu_radio_in_sync_when_map_changed_and_restored(시험 mbtiles 2 + 지도 DB 가짜 · 콤보 전환 → 타일 원천 바뀜 · 콤보/「지도 전환」 라디오 일치 · 자리 유지 · 라디오 UIA Invoke 로 복귀) · ::should_persist_selected_map_name_to_settings_file_when_map_switched [NAV-D1] · ::should_write_map_name_into_existing_app_settings_section_when_map_settings_saved [NAV-D1] | 통과(NAV-D1 본 세션 수정 뒤) |
| MAP-194 | 가 | ::should_expose_map_combo_four_operational_buttons_and_mode_toggles_in_order_when_toolbar_rendered — 실제 MD ToolBar 스타일, 콤보 "지도 선택" · 비토글 4 HelpText 순서(홈 · 관심지역 · 레이어 · 추적 재생) · 토글 길이 · 넓이 · 회전 · 편집(카탈로그 '토글 4' 는 기울이기 추가 전 표기 → 그 밖 토글이 기울이기 한 쌍뿐임을 단언) | 통과 |
| MAP-195 | 가 | ::should_move_hidden_items_into_overflow_popup_body_and_show_overflow_button_when_toolbar_narrowed — 330px: OverflowButton 보임 · 숨은 항목이 ToolBarOverflowPanel(팝업 몸만 측정)로, 넓히면 복귀. Esc 닫기는 팝업 HWND 필요로 제외 | 통과 |
| MAP-196 | 가 | ::should_run_measure_and_edit_from_overflow_items_when_toolbar_narrowed — 230px 넘친 「길이 측정」 → 측정 On/Off, 편집 스위치 → 「편집 중」 | 통과 |

## 결함 NAV-D1(MAP-040)
- 증상: 지도를 바꿔도 「현재 지도 설정 저장 완료」 로그만 남고 appsettings.json AppSettings 에 MapName · MapType · MapMode · HomePosition 이 안 써짐 → 재시작하면 이전 지도. 운영 파일엔 AppSettings 절이 늘 있으므로 늘 재현.
- 원인: MapSettingsHelper.SaveMultipleSettingsAsync(:337-338)가 이미 있는 절을 같은 키에 다시 대입 → Json.NET 이 복제본을 넣어 쓰기가 떨어진 원본에만 들어감. 단일 키 SaveSettingAsync 는 null 일 때만 만들어 정상.
- 실패 시험 2(3회 동일).

## 시험 장치 메모
- 창 없이 만든 ItemsControl(지도 · Menu)은 테마 기본 템플릿을 못 받아 `Border > ItemsPresenter` 템플릿을 직접 붙임(주석).
- NavXaml.Load 조각은 md:PackIcon 안 DynamicResource 에서 파싱이 깨져 프로젝트 안 NavRemainingKit.Load 로 대신.
- 생성자 없는 VM 은 필드 초기화식이 비어 MAP-040 은 _mapSwitchLock 을 직접 넣음.

## 실행(작업자 시점)
- `실패! - 실패: 2, 통과: 45, 전체: 47 - GMaps.MapNav.Tests.dll` (실패 2 = NAV-D1), 새 시험 14개 3회 동일
