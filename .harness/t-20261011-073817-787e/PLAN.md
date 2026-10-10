# PLAN — 지도(§1 MAP-*) · §3.5 라이브러리 쪽 헤드리스 시나리오 검사 (t-20261011-073817-787e)

## 목표
첨부 카탈로그 `gis-map-event-scenario-catalog.md` §1 지도(MAP-001~196)의 남은 항목 전부와 §3.5 중 라이브러리 쪽(추적 설정 · 재생 · 방송/TTS · 측정 · 그리기 · 팔레트 · 지도 "?")을
창 없이(뷰모델 · 서비스 · 순수 함수 · 부모 없는 Measure/Arrange · RenderTargetBitmap) 시험으로 만든다. 시험마다 `[Trait("Scenario", "MAP-xxx")]` 표지.
끝에 항목 id 별 결과표(통과 · 결함→수정 · 결함 보고만 · 헤디드 남김 · 범위 밖)를 남긴다.

## 이어받은 상태
- 목표 문구는 "이 브랜치에서 한 번 진행되다 멈췄다"고 하지만, 시작 시점 브랜치 `harness/t-20261011-073817-787e` 는 v2.6(d8a4d5e7)과 같고 `.harness/t-20261011-073817-787e/` 도 없었다.
  같은 시각 형제 브랜치(t-20261011-0731xx · 0738xx)도 모두 d8a4d5e7 — 커밋된 진행분 없음 → 처음부터 시작(결정 D0).
- 상태표 도구 `tools/scenario-evidence/{collect.py,run.ps1}` 는 메인 체크아웃에만 있다(git 밖). 이 작업은 표지만 단다.

## 결정
- D0: 진행분 없음 → 새로 시작.
- D1: 대상 범위 = 카탈로그 마지막 칸이 GAP-A/GAP-N 이거나, 헤디드(r27/호스트) 시험만 있는 줄의 빈 부분. 2026-10-08 하네스 시험(MapSymbolReflectionTests 등)이 덮는 줄은 "있음"으로 친다.
  이미 시험이 있는 줄은 결과표에 `범위 밖(기존 시험)` 으로 적는다.
- D2: 새 시험은 새 시험 프로젝트 4개에 나눠 둔다 — `tests/GMaps.MapNav.Tests`(위치 · 줌 · 회전 · 기울이기 · 고정 · 좌표 · 보기 토글) ·
  `tests/GMaps.MapLayer.Tests`(위치 찾기 · 관심지역 · 레이어 · 오버레이 이미지) · `tests/GMaps.MapEdit.Tests`(편집 · 팔레트 · 심볼 CRUD · 선택 · 속성) ·
  `tests/GMaps.MapAux.Tests`(부품 카드 · 우클릭 · 상세 · 센서 정보 · 측정 · 그리기 · 재생 · 추적 · 방송 · 허브 · 겹창 · Esc · "?").
  이유: 병렬 작업 시 같은 프로젝트를 동시에 빌드하면 obj 충돌. 각 프로젝트는 `--no-dependencies` 로 자기만 빌드. 공용 도구는 `tests/GMaps.Scenario.Shared/ScenarioHarness.cs`(링크).
  GMaps.Ui.csproj 에 InternalsVisibleTo 4줄 추가(시험 전용 · 제품 동작 무관). 이 프로젝트들은 .sln 밖(기존 tests/GMaps.* 와 같음) — 실행은 `dotnet test tests/GMaps.MapNav.Tests` 등.
- D3: 창 금지 — Window.Show · 화면 밖 창 금지. 기존 창 띄우는 시험 클래스(`MapLocateFitTests` · `ComponentCardScreenSizeTests`)는 회귀 실행에서 --filter 로 뺀다.
- D4: MapViewModel 은 GetUninitializedObject + 필요한 필드만 가짜(SymbolCopyLabelStylePersistenceTests 선례). 지도 DB · 서버 · NATS 실접속 없음.
- D5: 결함은 시험 작성자가 증거(실패 출력 + 코드 줄)로 보고 → 제품 수정은 한 곳(본 세션)에서 하고 빨강→초록을 아래 "결함" 절에 적는다.

## 진행
- [x] 카탈로그 §1 · §3.5 읽기, 대상 항목 추리기
- [x] 기준 실행(수정 전): Housing 292/292 · PropertyPanel 249/249(창 시험 2클래스 제외) · GMaps.Ui.Tests 757/757 통과
- [x] 새 시험 프로젝트 4개 + 공용 도구 + 연기 시험(1/1 통과)
- [ ] 묶음별 시험 작성(NAV · LAYER · EDIT · AUX)
- [ ] 결함 수정 + 빨강→초록 기록
- [x] 기존 2026-10-08 시험에 표지 한 줄 추가(본문 불변) — 기대 칸을 실제로 단언하는 메서드만:
  MapSymbolReflectionTests(MAP-111~116 · 119 · 115/119 픽셀 · 137 · 139) · MapOverlayPanelThemeLayoutTests(MAP-080 · 190) ·
  LayerPanelMapStateTests(MAP-052 · 055 · 057 · 058 · 067×2) · SymbolCopyLabelStylePersistenceTests(MAP-104 · 105 · 106).
  표지를 달지 않은 것: 빈칸 누수 시험 · 스냅샷 복원(MAP-106/139 의 "되돌아감" 전체를 단언하지 않음) · 저장된 잠금→트리(MAP-059 는 반대 방향) · 빈 섹션(MAP-053 「등록된 항목이 없습니다」 글은 단언 안 함).
- [ ] 회귀 실행(전체 GMaps 시험 프로젝트 + sln) · 결과표

## 예산 메모(중요 — 다음 세션)
- 이번 세션 예산이 거의 바닥나 EDIT 묶음만 결과를 받았다. NAV · LAYER · AUX 묶음 시험은 각 프로젝트 폴더에 일부 쓰였을 수 있으나 **검증 · 결과 회수 전**.
  다음 세션: `dotnet build tests/GMaps.Map{Nav,Layer,Aux}.Tests/*.csproj --no-dependencies` → `dotnet test` 로 상태 확인 → 표지(`Trait Scenario`) 확인 → 아래 표 채우기.
- 결함 수정(D5)은 아직 하나도 안 했다 — 아래 "결함 의심"은 모두 보고만.

## 결함(보고만 — 수정 안 함)
- EDT-S1(MAP-107 · 138): 편집 모드 Esc 가 선택을 풀지 않는다. MapViewModel.cs:1042-1071 Esc 처리기는 조준 · 배치 · 홈 · 고정 모드만 끔, GMapCustomControl.cs:1874-1878 은 끌기 편집 취소(CancelAllEditing)만. 주석상 의도일 수 있어 의심으로 둠.
- EDT-S2(MAP-095): 팔레트에서 Esc → CancelPalettePlacement(MapViewModel.SymbolPalette.cs:28) 는 띠를 숨기기만 해 「배치를 취소했습니다.」 가 안 뜬다(지도 Esc 는 뜸, MapViewModel.cs:1056). 경미.
- EDT-S3(MAP-136): 잠긴 심볼을 확인 전에 미리 빼서(MapViewModel.cs:1381) 「잠금 N개 제외」 완료 글(:1421/:1443)이 절대 안 나온다. 카탈로그 기대 밖 · 경미.

## 결과표(EDIT 묶음 — tests/GMaps.MapEdit.Tests, 22/22 통과)
| id | 상태 | 시험 |
|---|---|---|
| MAP-087 | 통과 | SymbolSelectionEditTests::should_hide_edit_switch_and_refuse_edit_mode_when_account_cannot_edit_map |
| MAP-089 | 헤디드 남김(미작성 — 헤드리스로 이어 쓸 수 있음) | — |
| MAP-090 | 통과 | PaletteAndPropertyTests::should_hide_palette_when_close_button_is_clicked |
| MAP-092 | 통과 | PaletteAndPropertyTests::should_filter_cells_and_show_no_result_text_when_search_matches_nothing |
| MAP-095 | 결함 보고만(EDT-S2) · 지도 Esc 경로는 통과 | PaletteAndPropertyTests::should_enter_placement_on_enter_and_cancel_without_row_on_escape |
| MAP-096 | 헤디드 남김(실제 머리 끌기) | — |
| MAP-097 | 통과 | PaletteAndPropertyTests::should_write_symbol3d_key_and_say_restart_needed_when_3d_housing_is_toggled |
| MAP-098 | 통과(부착만, 말풍선 열기는 헤디드) | PaletteAndPropertyTests::should_attach_placement_and_symbol3d_help_tips_inside_palette |
| MAP-099 | 통과 | SymbolSelectionEditTests::should_persist_dragged_position_and_restore_it_on_undo_when_symbol_is_moved |
| MAP-100 | 통과(Shift 5px 는 헤디드) | SymbolSelectionEditTests::should_move_selected_symbol_one_pixel_per_arrow_and_one_grid_cell_when_snap_is_on |
| MAP-101 | 통과 | SymbolSelectionEditTests::should_ask_with_symbol_title_then_delete_db_row_when_delete_button_is_confirmed |
| MAP-102 | 통과 | SymbolSelectionEditTests::should_route_delete_key_to_same_confirmation_then_delete |
| MAP-103 | 통과 | SymbolSelectionEditTests::should_keep_symbol_and_db_row_when_delete_confirmation_is_cancelled |
| MAP-107 | 결함 보고만(EDT-S1) · 「선택 해제」 단추 경로는 통과 | SymbolSelectionEditTests::should_release_selection_and_close_property_panel_when_clear_selection_is_pressed |
| MAP-108 | 통과 | SymbolSelectionEditTests::should_disable_clear_and_delete_buttons_when_nothing_is_selected |
| MAP-110 | 헤디드 남김(실제 머리 끌기) | — |
| MAP-114 | 범위 밖(기존 시험) | BillboardRotationRowTests::should_collapse_rotation_row_when_selected_shape_is_billboard_pids2d |
| MAP-120 | 통과 | PropertyOrderZoomTests::should_hide_symbol_until_zoomed_in_when_min_zoom_exceeds_current_zoom |
| MAP-121 | 통과 | PropertyOrderZoomTests::should_raise_draw_order_and_persist_db_zorder_when_to_top_is_pressed |
| MAP-124 | 헤디드 남김(미작성 — 가짜 서버로 이어 쓸 수 있음) | — |
| MAP-125 | 통과 | PaletteAndPropertyTests::should_disable_device_link_combo_and_apply_button_when_two_symbols_are_selected |
| MAP-128 | 헤디드 남김(미작성 — 헤드리스로 이어 쓸 수 있음) | — |
| MAP-130 | 통과 | PaletteAndPropertyTests::should_set_post_spacing_to_2_3_5_m_by_presets_and_return_to_default_on_reset |
| MAP-131 | 통과 | PaletteAndPropertyTests::should_return_gate_width_to_default_when_reset_is_pressed |
| MAP-132 | 헤디드 남김(GATE_DOOR_SET 현장 장비) | — |
| MAP-133 | 통과(부착만) | PaletteAndPropertyTests::should_attach_label_housing_door_device_fence_help_tips_in_property_panel_styles |
| MAP-135 | 통과 | SymbolSelectionEditTests::should_move_every_group_member_except_locked_and_persist_each_when_group_is_moved |
| MAP-136 | 통과(EDT-S3 경미 보고) | SymbolSelectionEditTests::should_delete_every_unlocked_group_row_after_one_confirmation_when_group_delete_is_confirmed |
| MAP-138 | 통과(해제는 전체 해제 경로, Esc 는 EDT-S1) | SymbolSelectionEditTests::should_select_every_symbol_then_release_all_when_ctrl_a_then_clear |
| MAP-139 | 범위 밖(기존 시험) · 앱 재시작은 헤디드 남김 | MapSymbolReflectionTests::should_show_same_values_when_property_panel_is_reopened_for_same_symbol |

기존 시험 표지로 "범위 밖(기존 시험)": MAP-052 · 055 · 057 · 058 · 067 · 080 · 104 · 105 · 106 · 111~116 · 119 · 137 · 190.

## 결과표(NAV 묶음 — tests/GMaps.MapNav.Tests, 29/29 통과 · 결함 없음)
통과: MAP-002 · 003 · 004 · 005 · 008 · 009 · 015 · 016 · 017 · 018 · 019 · 020 · 024 · 025 · 026 · 027 · 028 · 030 · 031 · 032 · 033 · 034 · 035 · 036 · 037 · 038 · 193 · AUX-008 · AUX-027
(시험: HomeAnchorScenarioTests · RotationTiltScenarioTests · ZoomViewScenarioTests — 메서드별 Trait 표지 있음.)
헤디드 남김: MAP-011(설정 패널 열기 요청까지만 확인, 「지도 카드」 선택은 호스트) · 014(실제 끌기 팬/러버밴드) · 039(나침반 끌기) · 040(타일 공급자 필요) · 196(좁은 실창 넘침) · 편집 띠 접힘 화면 밖.
범위 밖(기존 시험): MAP-195. 추가 · Ctrl+E 종료 경로는 ZoomViewScenarioTests::should_route_ctrl_e_and_map_menu_exit_to_confirm_popup 통과(카탈로그 id 없음 — 표지 없음).

## 결과표(LAYER 묶음 — tests/GMaps.MapLayer.Tests, 26 중 23 통과 · 3 은 결함 증명 시험)
통과: MAP-043 · 045 · 046 · 047 · 048 · 050 · 059 · 063 · 064 · 065 · 068 · 069 · 070 · 071 · 072 · 073 · 077 · 081
(시험: LocateAndRoiScenarioTests · LayerPanelScenarioTests · LayerPanelXamlTests.)
결함 보고만(수정 안 함 — 예산 소진, 다음 세션 1순위):
- LAY-D1(MAP-066): 편집 권한 없이 오버레이 이미지 보이기 토글 시 Images 행을 DB 에 씀 — MapViewModel.cs:10111 `UpdateImageAsync` 권한 검사 없음(투명도 :10147 은 `CanEditMap()` 으로 막음).
  실패 시험 LayerPanelScenarioTests::should_not_write_image_row_to_db_when_overlay_image_is_toggled_without_edit_permission — "Expected invocation on the mock should never have been performed, but was 1 times: UpdateImageAsync". 제안: `if (CanEditMap()) await ...UpdateImageAsync(...)`.
- LAY-D2(MAP-060): 고른 심볼을 레이어 패널에서 잠가도 Delete → 삭제 확인 창(확인하면 지워짐) — ExecuteDeleteSelected(:4275-4296) 잠금 검사 없음 · OnSymbolLockChanged(:9910-9930) 선택 안 풂.
  실패 시험 LayerPanelScenarioTests::should_not_offer_delete_confirm_when_selected_symbol_is_then_locked_from_layer_panel — "Assert.Empty() Failure … '카메라-5'을(를) 삭제하시겠습니까?".
- LAY-D3(MAP-078): 레이어 패널 머리 끌기가 지도 밖으로 무제한 — LayerPanelControl.cs:342-346 클램프 없음(크기 조절 :404-419 · 다른 겹창은 클램프).
  실패 시험 LayerPanelScenarioTests::should_keep_panel_header_inside_map_canvas_when_header_is_dragged_far_outside — "머리(폭 250 × 36)가 지도(1000×700) 밖: left=-2950, top=-1940".
- 결함 의심(시험 없음, MAP-066): 끌기 손잡이 꺼짐 사유 툴팁 없음(LayerPanelStyle.xaml:247-254, ShowOnDisabled 없음).
헤디드 남김: MAP-074 · 075(Win32 파일창) · 076(생성 대화상자).
MAP-060 · 066 · 078 상태 = 결함 보고만.

## 결과표(AUX 묶음 — tests/GMaps.MapAux.Tests)
27개 중 23 통과 · 4 는 결함 증명 시험(TrackingPlaybackScenarioTests · MapModeScenarioTests, 전용 STA 스레드 OneSta.cs).
통과: MAP-082 · 084 · 158 · 159 · 167 · 170 · 171 · 172 · 174 · 181 · 182 · 189 · 192 · AUX-013 · 014 · 015(쓰는 쪽이 TTL 3초 하한 — TrackingOverlayManager.cs:80, 결함 아님) · 016(31일 초과는 31일로 자름 — 의도 주석) · 020
결함 보고만(수정 안 함):
- AUX-D1(MAP-083): 추적 「최소 줌」 무효 — MinVisibleZoom 을 읽는 곳 없음(TrackingOverlayManager.cs:75-90). 실패: should_hide_tracking_markers_when_min_visible_zoom_is_above_current_zoom "최소 줌 16 > 지금 줌 12 인데 추적 마커 1개가 보인다". 「추적 표시」 끔은 통과.
- AUX-D2(AUX-022): TTS 실패 안내 없음(MapViewModel.cs:9637-9650 catch 로그만). 실패: should_notify_user_and_keep_panel_open_when_tts_send_fails.
- AUX-D3(AUX-022): TTS 이중 전송 막음 없음(_isBroadcastRequestInFlight 미사용). 실패: should_send_tts_once_when_send_pressed_twice_while_first_send_is_pending "once, but was 2 times".
- AUX-D4(AUX-019): 스피커 메뉴 Header = "Stop" 영문(MapViewModel.cs:6648). 실패: should_label_speaker_menu_items_in_korean_when_speaker_symbol_right_clicked(소스 판독 시험). MapModeScenarioTests.cs 끝 `MenuProbe` 는 안 쓰는 잔재.
범위 밖(기존 시험): AUX-018 · 지도 "?" 4키(MapsHelpTests) · MAP-178(DrawingKeyRouterTests) · MAP-190(MapOverlayPanelThemeLayoutTests).
헤디드 남김(진짜 실창 필요): AUX-012 · 017 · 021 · MAP-157 · 191.
**미작성(헤디드 필요 아님 — 다음 세션 추가 대상)**: MAP-151 · 152 · 155 · 156 · 164(Sort) · 169 · 175 · 176 · 180 · 188(2→1→0 · 위치 기억; 수 표시 · 클램프는 CameraPopupHubMathTests).

## 다음 세션 할 일(순서)
1. 결함 7건 수정 + 빨강→초록 기록: LAY-D1~D3 · AUX-D1~D4(제안 수정은 위 · 실패 출력은 이미 기록). 의심 3건(EDT-S1~S3) 판단.
2. 미작성 항목: MAP-089 · 124 · 128 · 151 · 152 · 155 · 156 · 164 · 169 · 175 · 176 · 180 · 188.
3. 회귀: GMaps.Housing/PropertyPanel(창 시험 2클래스 제외)/Ui.Tests + 새 4 프로젝트 + sln.
