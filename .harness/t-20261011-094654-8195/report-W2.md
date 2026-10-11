# W2(EDIT) 보고 — 본 세션이 작업자 보고를 옮겨 적음

## 파일(tests/GMaps.MapEdit.Tests 새 파일 · BOM 확인 · 기존 시험 무변경)
- WiredMapRig.cs — 실제 지도를 AdornerDecorator 안에 두고 VM 실제 배선(SetupAdornerIntegration) 호출, AssertWired 로 구독 확인. 마커 · 속성창 팩토리 실제, DB · 권한 · 발행기 가짜(Moq)
- PropertyStyleLoader.cs — 속성창 실제 스타일을 Application 없이 앱 병합 순서대로 한 사전에 적재(스타일 · 템플릿 · x:Name 원본, 병합 블록 제거 · clr-namespace 어셈블리명만 보정)
- SymbolCreateScenarioTests.cs — MAP-093 · 094
- PropertyPanelScenarioTests.cs — MAP-109 · 110
- DoorAndMultiSelectScenarioTests.cs — MAP-132 · 134

## 결론
| id | 결론 | 시험 | 결과 |
|---|---|---|---|
| MAP-093 | 가 | SymbolCreateScenarioTests::should_create_symbol_at_drop_point_and_insert_db_row_when_palette_cell_is_dropped_on_empty_map(실제 DragOver/Drop → 지도 OnDrop → VM, DB Insert 인자 = 놓은 점 위경도 · 종류 · 제목, 마커 ±1.5px, 실행 취소) · ::should_insert_pids_row_with_device_type_and_variant_at_drop_point_when_pids_cell_is_dropped · ::should_not_insert_row_when_drop_lands_outside_map_or_placement_was_not_started | 통과 |
| MAP-094 | 가 | ::should_create_symbol_at_clicked_point_and_insert_db_row_when_cell_is_selected_then_map_is_clicked(팔레트 셀 누름/뗌 → 배치 모드 · 안내 → SymbolPlacementClicked → 클릭 점 DB 행 · 마커, 한 번만) · ::should_place_one_symbol_per_click_at_each_clicked_point_when_cell_is_reselected(가장자리 포함 3점) | 통과 |
| MAP-109 | 가 | PropertyPanelScenarioTests::should_open_property_panel_with_title_field_and_zorder_buttons_when_symbol_is_clicked_in_edit_mode(실제 BasePropertyStyle 템플릿 제목 칸 값 · UIA Value · PART_ZOrderTop/Up/Down/Bottom 존재 · 켜짐 · 도움말, [맨 위로] → DB ZOrder) · ::should_save_edited_title_from_title_field_to_marker_and_db_after_typing_pause | 통과 |
| MAP-110 | 가(일부 다) | ::should_close_property_panel_and_release_selection_when_close_button_is_clicked(PART_CloseButton → 닫기 · 선택 해제 · Collapsed · 다시 열림) · ::should_move_property_panel_by_header_drag_delta_inside_property_canvas · ::should_not_start_drag_when_panel_is_not_draggable. 남는 관찰: 창 없이 마우스 좌표가 (0,0) 이라 "누른 점"을 비공개 필드로 넣음 — 헤더 띠 45px 판정 · 캡처 · 실제 좌표는 헤디드 몫 | 통과 |
| MAP-132 | 가 | DoorAndMultiSelectScenarioTests::should_publish_gate_door_set_to_nats_and_follow_reported_state_when_door_buttons_are_pressed · ::should_release_pending_and_notify_when_nats_publish_fails · ::should_keep_door_buttons_off_and_publish_nothing_without_device_control_permission · ::should_show_reported_door_state_and_enable_buttons_as_soon_as_gate_property_panel_opens [Defect EDT-D1] | 통과(EDT-D1 은 본 세션 수정 뒤) |
| MAP-134 | 가 | ::should_toggle_members_and_show_group_adorner_only_for_two_or_more_when_symbols_are_ctrl_clicked · ::should_group_unlocked_symbols_inside_rubber_band_and_attach_group_adorner_when_shift_dragged · ::should_ignore_ctrl_click_toggle_when_edit_mode_is_off | 통과 |

MAP-132 발행 단언(실제 VM 배선 + 실제 DoorControlService, 발행기만 가짜 INatsService): subject `sensorway.unit001.all.gate-door` · m_type PUB · cmd GATE_DOOR_SET · from GIS · body device_id 77 · command OPEN/CLOSE · device_description · requested_by · 오프셋 붙은 requested_at · 누른 뒤 "명령 보냄 · 응답 대기" + 단추 잠금 + 심볼 문 상태 불변 · ApplyDoorState 보고로 '열림'/'닫힘' 추종.
공통 헤디드 몫: Keyboard.Modifiers(Ctrl/Shift) 판정 · 지도 위 마우스 좌표 히트 · OLE 끌기 손동작 · 커서 · 그림.

## 결함 EDT-D1(MAP-132)
- 증상: 권한 · 장비 연결 · 전송기가 다 있어도 통문(함체 동일) 속성창을 막 연 직후 문 상태 글 '권한 없음(devices:control)' · [열림]/[닫힘] 꺼짐 — 다음 문 상태 보고가 와야 바로잡힘.
- 원인: MapViewModel 팩토리가 SelectedMarker 를 먼저 넣어 GMapPropertyPidsControl.SyncGateFromMarker → RefreshDoorUi 가 판정기 없이 계산, 이어 WireDoorControl 이 CanControlDevice · DoorCommandSender 를 넣지만 단순 자동 속성이라 재계산 없음.
- 실패 시험: DoorAndMultiSelectScenarioTests::should_show_reported_door_state_and_enable_buttons_as_soon_as_gate_property_panel_opens(3회 모두 같은 실패).

## 실행(작업자 시점)
- `실패! - 실패: 1, 통과: 54, 전체: 55 - GMaps.MapEdit.Tests.dll` (실패 1 = EDT-D1), 새 시험 17개 3회 반복 동일
