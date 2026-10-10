# PLAN — 결선 창 · 조립기 헤드리스 확인 끝까지(카탈로그 §6 WIR-* · ASM-*)

## 목표
(사용자) "헤드리스로 확인작업을 끝까지 해줘" — 카탈로그 §6 의 남은 GAP · 헤디드 전용 항목 중 창 없이 확인되는 것을 **전부** 시험으로 만든다.
D3(장애 고장 구간 → 링 A/B 센서 이름) 원인 조사 + 작으면 구현, D5(좁은 창 표 열 잘림) 부모 없는 렌더로 재고 고칠 수 있으면 고친다.
새 시험 메서드마다 `[Trait("Scenario", "WIR-nnn")]` 표지(증거 규칙 2026-10-11).

## 시작 상태(2026-10-11)
- 이 브랜치에 커밋된 진행분 · PLAN.md 가 없었다(HEAD = v2.6 d8a4d5e7). 형제 브랜치(t-20261011-0731xx · 0738xx)도 같은 커밋 — 처음부터 진행.
- 2026-10-08 작업(t-20261008-150647-8f68)의 PLAN 은 커밋 7e69d60b^ 에서 읽었다: 4파일 107건 · D1 · D2 · D4 수정 · D3 · D5 보고만.
- `tools/scenario-evidence/collect.py` 는 이 브랜치에 없다(본 저장소 작업 폴더에만 있음) — 읽기만 했다(표지 정규식 `[Trait("Scenario","…")]`, 메서드 줄 바로 앞 속성).

## 결정
- 시험 위치: `Ironwall.Dotnet.Libraries.Devices.Ui/Tests/`(같은 csproj · sln 밖) — 하네스 검사(`dotnet test sln`)는 이 프로젝트를 돌리지 않으므로 직접 `dotnet test <csproj> --filter` 로 돌린다.
- 창 금지: Window.Show · 화면 밖 창 모두 금지. 뷰모델 · 순수 함수 · 부모 없는 Measure/Arrange · RenderTargetBitmap 만.
- 2026-10-08 의 4파일(WiringFenceGapTests · WiringCanvasGapTests · WiringSaveGapTests · AssemblyGapTests) 메서드에도 표지를 붙인다(본문 불변, 기대를 실제 단언하는 것만).

- 같은 csproj 의 기존 시험 파일에는 **표지 한 줄만** 더했다(본문 불변): 10-08 4파일 + WiringLauncherFenceTests · WiringFenceReviewFixTests · WiringFenceEditorTests ·
  FenceGestureTests · FenceChipPlateTests · FenceDistanceTests · WiringFenceLanesTests · ConceptLanesLayoutTests · WiringTableRowsTests · AssemblyViewModelTests ·
  AssemblyBoardTests · DevicePresetStoreTests · ComponentDisplayUnifyTests · DeviceAxisEditTests · DeviceDashboardConsoleTests · PresetRegisterTests.
  기대 칸을 실제 단언하지 않는 것(도움말 키 등록만 보는 시험 → WIR-605 · ASM-301 등)에는 붙이지 않았다.
- 실제 마우스 끌기 상태(끄는 중 빨간 점 · 알약 · 개념도 C 끌기 · 끌기 중 Esc) · F1 · 말풍선 열고 닫기는 창이 있어야 한다 → 헤디드 남김.
  단, 끌기의 **처리기 길**(IDragDropHandler.Drop)과 키 판정(`HandleKeyDown`)은 창 없이 시험했다.
- 전체 `dotnet test Ironwall.Dotnet.Libraries.sln` 은 창을 띄우는 시험이 섞여 있을 수 있어 직접 돌리지 않았다(하네스가 돈다). 이번 변경은 sln 밖 프로젝트(Devices.Ui · Events.Ui)뿐이고,
  sln 안에서 닿는 것은 Utils.Tests 의 저장소 전 XAML 계약 시험이라 그것만 골라 돌렸다(75건 통과).
- 새 .cs 7개는 UTF-8 BOM(쓰기 도구가 BOM 을 안 붙여 Edit 로 U+FEFF 를 넣고 첫 바이트 239 확인).

## 진행 — 끝
- [x] 카탈로그 §6 · 10-08 PLAN(7e69d60b^) · collect.py 읽기
- [x] 10-08 4파일 + 기존 헤드리스 시험 대조 → 표지(에이전트 대조표 확인 후)
- [x] D3 조사 · 구현(작음) — 빨강→초록
- [x] D5 결선 표 · 조립기 렌더 측정 → 수정 — 빨강→초록
- [x] 새 시험: `WiringHeadlessRound2Tests`(27) · `AssemblyHeadlessRound2Tests`(3) · `WiringNarrowLayoutTests`(5) · `AssemblyNarrowLayoutTests`(5) · `Events.Ui/Tests/EventFaultSectionRingTests`(5)
- [x] 회귀: Devices.Ui 결선 · 펜스 · 개념도 · 조립기 · 프리셋 · 부품 · 장비 창 헤드리스 1486건 통과(창 시험 5클래스 --filter 제외) · Events.Ui 상세 · 장애 · 대시보드 238건 통과(창 시험 2클래스 제외) · Utils.Tests XAML/도움말 계약 75건 통과
- [x] 결과표

## 실행한 회귀 명령(창 띄우는 클래스는 뺌)
```
dotnet test Ironwall.Dotnet.Libraries.Devices.Ui/Ironwall.Dotnet.Libraries.Devices.Ui.csproj --filter "(FullyQualifiedName~Wiring|FullyQualifiedName~Fence|FullyQualifiedName~Concept|FullyQualifiedName~Assembly|FullyQualifiedName~Preset|FullyQualifiedName~Component|FullyQualifiedName~DeviceAxisEditTests|FullyQualifiedName~DeviceDashboardConsoleTests|FullyQualifiedName~DeviceLeftoverRequestTests|FullyQualifiedName~DevicesHelpTests)&FullyQualifiedName!~WiringFenceViewTests&FullyQualifiedName!~WiringFenceEditorViewTests&FullyQualifiedName!~WiringFenceHeightViewTests&FullyQualifiedName!~ComponentTableHostViewTests&FullyQualifiedName!~ConsoleWindowChromeShownTests"
dotnet test Ironwall.Dotnet.Libraries.Events.Ui/Ironwall.Dotnet.Libraries.Events.Ui.csproj --filter "(FullyQualifiedName~EventDetail|FullyQualifiedName~EventFaultSection|FullyQualifiedName~EventConsole|FullyQualifiedName~Malfunction|FullyQualifiedName~EventDashboard|FullyQualifiedName~EventMapScenarioTests)&FullyQualifiedName!~EventConsoleSingleSelectionDetailTests&FullyQualifiedName!~EventToolbarChipAutomationTests"
dotnet test Ironwall.Dotnet.Libraries.Utils.Tests/Ironwall.Dotnet.Libraries.Utils.Tests.csproj --filter "FullyQualifiedName~HelpContractTests|FullyQualifiedName~ConsoleStyleContractTests|FullyQualifiedName~ConsoleAutomationExposureTests"
```
Devices.Ui 의 창 시험 파일(`.Show()`)은 11개 — 위 필터에 걸리는 5개를 뺐고 나머지(UnitMap* · UnitConsole* · StaCleanupTests)는 필터에 들지 않는다.
TRX: `Ironwall.Dotnet.Libraries.Devices.Ui/obj/scenario-trx/devices-ui-wiring-asm.trx` · `Ironwall.Dotnet.Libraries.Events.Ui/obj/scenario-trx/events-ui-detail.trx`(obj — git 밖. 증거 폴더 수집은 `tools/scenario-evidence/run.ps1` 로 다시 돌려야 한다).

## 남은 일 · 알림
- collect.py 의 `ROW_RE` 는 `| ASM-100~116 |` · `| WIR-315~322 |` 같은 **범위 id 행을 읽지 못한다**(id 뒤에 바로 `|` 가 와야 함) — 그 행은 상태표에 안 나온다. 표지는 `ASM-100` 으로 달아 두었다.
- ASM-214(제어기 부품 상태 요약)는 이번에 새 시험을 만들지 못했다(부분: ComponentDisplayUnifyTests 의 요약 경로가 일반 장비로 덮음).
- D3 의 "구간 정수 = 순번" 해석은 서버팀 확인 항목(PRD W-D4) 그대로.

## 발견

### D3 — 장애 고장 구간 → 링 A/B 센서 이름(WIR-504) · 작아서 구현함
- 원인(증거): 이벤트 상세 `EventDetailViewModel.BuildMalfunction`(Events.Ui) 은 `row.FirstStart → FirstEnd` 숫자만 찍는다. 링 셈법(1차 n = A 쪽 n번째 · 2차 n = N+1−n)은
  결선 창 안 `WiringValidation.DescribeFaultSection(WiringBoard …)` 에만 있고, 결선 창 밖에서 쓸 길(보드 없이 장비 캐시로)이 없었다.
- 실마리: 장비 캐시(`DeviceProvider`)의 센서 모델은 `Axes.HardwareSpec.Spec` 에 서버 `spec.wiring` 을 그대로 들고 있고(`DeviceAxesMapper.BuildAxes`),
  `EventDashboardViewModel` 은 이미 `DeviceProvider` 를 선택 주입으로 받는다(Autofac 이 채움 — 억제 레일이 같은 값을 씀). Events.Ui 는 Devices.Ui 를 참조한다.
- 변경: ① `Devices.Ui/Consoles/Wiring/Model/WiringFaultLocator.cs`(새) — `RingOf(장비들, 제어기 id)` · `Describe(링, 선, 시작, 끝)` 순수 함수
  ② `EventDetailViewModel` 생성자에 선택 인자 `ringOf` — 있으면 「고장 구간」 절에 「1차 센서」 · 「2차 센서」 줄을 더한다(없으면 예전 그대로 두 줄)
  ③ `EventDashboardViewModel` 이 `DeviceProvider` 로 `ringOf` 를 넘긴다.
- 빨강→초록: 제품 경로 시험 `EventFaultSectionRingTests.should_show_the_ring_sensor_names_in_the_event_console_detail_from_the_device_cache_WIR504` —
  대시보드 배선을 빼고(`ringOf` = null) 돌리면 실패 `Assert.Equal() Failure: Strings differ · Expected: "Ch1(A) 쪽 4~5 → 북측 4구간과 북측 5구간 사이" · Actual: null`,
  배선을 되돌리면 통과(5/5).
- 남는 것: 구간 정수가 순번인지 버스 주소인지는 서버팀 확인 항목(PRD W-D4) — 결선 창 예시와 같이 순번으로 읽는다. 실제 장애 도착(NATS) · 카드는 헤디드(WIR-503 r27).

### D5 — 좁은 결선 창 표 열 잘림(WIR-608) · 결함 → 수정
- 측정(부모 없는 WiringView 느슨한 읽기 + Measure/Arrange, 창 없음 · `WiringNarrowLayoutTests`): 표 ScrollViewer `Devices.Wiring.TableView` 가 `HorizontalScrollBarVisibility="Disabled"`.
  - 1000×700 속성 칸 펼침: Viewport 664 · Extent 664 · Scrollable 0 · 상태 열 오른쪽 끝 789 · 빼기 ✕ 816 → **잘린 채 닿을 수 없음**
  - 800×600 펼침: Viewport 454 · 상태 789 · 빼기 816 / 460×400(창 최소): 같은 실패 / 1000 접힘 · 1440 은 통과
- 빨강: `should_keep_every_table_column_reachable_in_a_narrow_wiring_window_WIR608(1000,700,True)` 실패 「상태 열이 잘렸다 — TableMeasure { Width = 1000, DetailOpen = True, Viewport = 664, Extent = 664, Scrollable = 0, StatusRight = 789, RemoveRight = 816 }」 (800 · 460 도 실패)
- 수정: `WiringView.xaml` 표 ScrollViewer `HorizontalScrollBarVisibility="Disabled"` → `"Auto"`(주석 갱신). x:Name 불변.
- 초록: 같은 시험 5건 통과 + 1440 기본 폭은 가로 스크롤 0(`should_need_no_horizontal_scroll_at_the_1440_wide_default_WIR608`).

### D5 후반 — 좁은 조립기 창(ASM-304) · 결함 → 수정
- 측정(`AssemblyNarrowLayoutTests`, AssemblyView.xaml 느슨한 읽기 · 부모 없음): 세 칸 280 · * · 340 고정 + 보드 블록 340 고정 + 보드 목록 가로 스크롤 꺼짐(Console.ReorderList).
  - 800×600: 보드 보이는 폭 136 · 블록 오른쪽 끝 370 → 블록 잘림 / 420×500(창 최소): 보드 0 · 속성 라벨 칸 끝 596 > 420 → 속성 칸 잘림 / 1000×700: 블록 잘림 / 1280 통과
- 빨강: `should_keep_board_blocks_inspector_fields_and_the_commit_button_reachable_in_a_narrow_assembly_window_ASM304(800,600)` 실패
  「보드 블록이 잘렸다 — AssemblyMeasure { Width = 800, BoardViewport = 136, BlockRight = 370, BoardScrolls = False, OuterViewport = 800, OuterExtent = 800, LabelRight = 776, CommitRight = 784 }」(420 · 1000 도 실패)
- 수정: `AssemblyView.xaml` 세 칸 Grid 를 `ScrollViewer`(AutomationId `Devices.Assembly.Body` · 가로 Auto · 세로 Disabled)로 감싸고 Grid 폭 = 보이는 폭(`ViewportWidth` 바인딩) · `MinWidth="1060"`.
  → 1060 이상(기본 1280)은 예전 배치 그대로(가로 밀기 0), 그보다 좁을 때만 본문이 가로로 밀린다. 바닥 줄(주 단추)은 창 폭을 따른다.
- 초록: 5건 통과(4 폭 + 1280 가로 밀기 없음).

## 결과표(항목 id → 상태 · 시험 메서드)

상태: **통과**(헤드리스 시험이 기대를 단언 · Scenario 표지 있음) · **결함→수정** · **결함 보고만** · **헤디드 남김** · **범위 밖**(카탈로그에 이미 헤디드 시험이 있는 항목 · 사진만 · 관측).
약호: R2 = `WiringHeadlessRound2Tests` · AR2 = `AssemblyHeadlessRound2Tests` · FG = `WiringFenceGapTests` · CG = `WiringCanvasGapTests` · SG = `WiringSaveGapTests` · AG = `AssemblyGapTests`(FG~AG 는 10-08 판, 이번에 표지만).

| id | 상태 | 시험 메서드(파일::메서드) |
|---|---|---|
| WIR-001 | 통과 | DeviceAxisEditTests::should_show_wiring_only_on_the_controller_rail_when_wiring_is_available |
| WIR-002 · 003 · 004 · 005 · 006 | 범위 밖(기존 헤디드 SC-WIR · SC-FEN) | — |
| WIR-007 | 통과(GAP-N → 헤드리스) | R2::should_hide_the_wiring_entry_and_open_nothing_when_the_server_is_older_than_7_WIR007 |
| WIR-008 | 통과(GAP-N → 헤드리스 · 3초 기준 자체는 미단언) | WiringLauncherFenceTests::should_open_the_window_with_a_proposed_fence_and_no_local_saving_when_the_store_cannot_be_created · ::should_still_open_the_window_when_loading_the_stored_fence_fails · WiringFenceReviewFixTests::should_offer_an_explicit_overwrite_with_the_row_revision_when_the_stored_layout_was_unreadable · ::should_not_write_the_local_layout_when_the_overwrite_is_declined |
| WIR-009 · 010 · 011 · 012 | 범위 밖(기존 헤디드 · r27) | — |
| WIR-100 · 101 | 범위 밖(기존 헤디드) | — |
| WIR-102 · 103 | 통과 | CG::should_draw_each_of_the_five_fence_styles_differently_WIR102_WIR103 |
| WIR-104 | 통과(막대 값 + XAML 묶음) | R2::should_light_the_bar_under_only_the_chosen_style_swatch_WIR104 |
| WIR-105 | 통과 | FG::should_refuse_a_malformed_direct_colour_and_take_a_well_formed_one_when_the_colour_is_typed_WIR105 |
| WIR-106 | 범위 밖(r27) | — |
| WIR-107 | 통과 | FG::should_light_the_chosen_span_chip_and_change_the_total_length_when_a_span_preset_is_applied_WIR107 · ::should_accept_a_typed_span_only_inside_half_to_twenty_metres_WIR107 |
| WIR-108 | 통과 | WiringFenceEditorTests::should_apply_only_touched_fields_and_undo_in_one_step_when_a_keyboard_selected_panel_range_is_edited · ::should_show_many_values_and_refuse_an_out_of_range_height_when_panels_differ |
| WIR-109 | 통과 | FG::should_title_the_pane_with_the_panel_count_when_panels_are_ctrl_clicked_WIR109 |
| WIR-110 | 통과(판정 · 띠 계산) | FenceGestureTests::should_classify_into_one_action_when_given_button_modifiers_target_and_travel · ::should_pick_everything_the_band_touches_in_input_order_when_dragged_in_any_direction |
| WIR-111 | 통과 | FG::should_extend_toggle_and_select_all_panels_the_way_the_keyboard_does_WIR111 |
| WIR-112 | 통과 | R2::should_title_the_pane_with_the_band_count_and_add_with_ctrl_WIR112 · FenceGestureTests 3건(위) · ::should_add_to_the_selection_when_ctrl_is_held_and_replace_it_otherwise |
| WIR-113 | 범위 밖(r27) | — |
| WIR-114 | 통과 | CG::should_unplace_with_delete_restore_with_ctrl_z_and_clear_with_esc_on_the_canvas_WIR162_WIR114 · FenceGestureTests::should_classify_into_one_action_… |
| WIR-115 · 117 | 범위 밖(기존 헤디드) | — |
| WIR-116 | 통과 | FG::should_grow_and_shrink_the_sensor_selection_with_ctrl_toggle_and_shift_range_WIR116 · CG::should_build_a_selection_with_ctrl_space_shift_arrow_and_ctrl_a_from_the_keyboard_WIR116 |
| WIR-118 | 통과 | FenceChipPlateTests::should_flip_the_plate_above_the_body_when_below_would_go_under_the_ground · ::should_put_the_plate_above_the_body_when_asked_so_it_cannot_reach_a_razor_coil_below |
| WIR-119 | 통과 | CG::should_add_distance_labels_to_the_3d_view_only_while_the_distance_toggle_is_on_WIR119 · ::should_draw_lane_distances_in_the_concept_only_while_the_toggle_is_on_WIR119 |
| WIR-120 | 통과(GAP-N → 헤드리스 값 계산) | FenceDistanceTests::should_measure_same_lane_neighbours_over_mixed_panel_spans_* · ::should_measure_razor_coil_fence_sensors_on_the_upper_lane · ::should_round_to_a_tenth_and_drop_a_trailing_zero |
| WIR-121 | 통과 | FenceDistanceTests::should_start_off_remember_the_toggle_and_not_save_while_applying_the_stored_value |
| WIR-122 · 125 | 범위 밖(기존 헤디드) | — |
| WIR-123 | 헤디드 남김(실제 마우스 끌기 중 상태 — 캔버스 `_snapPlan`) | — |
| WIR-124 | 헤디드 남김(같음 — 알약 「놓을 수 없음 — n대 갈 점 없음」은 끄는 중에만) | — |
| WIR-126 | 범위 밖(r27) | — |
| WIR-127 | 통과 | FG::should_say_the_bin_hint_while_dragging_and_send_sensors_to_the_palette_when_dropped_on_the_bin_WIR127 |
| WIR-128 | 통과 | CG::should_step_to_the_next_grid_point_with_ctrl_arrow_and_back_and_move_a_panel_with_alt_shift_arrow_WIR128 |
| WIR-129 | 범위 밖(관측 · 한국어 Windows 입력 언어) | — |
| WIR-130 · 134 · 138 | 범위 밖(기존 헤디드) | — |
| WIR-131 | 통과 | FG::should_seat_a_panel_sensor_on_each_of_the_nine_grid_points_WIR131 · ::should_put_nine_different_grid_points_inside_one_panel_WIR131 |
| WIR-132 · 133 · 135 | 통과 | CG::should_climb_to_the_razor_coil_with_alt_up_and_nudge_the_height_with_shift_alt_WIR132_WIR133_WIR135 |
| WIR-136 | 통과 | FG::should_apply_an_offset_only_between_minus_three_and_plus_three_metres_WIR136 |
| WIR-137 | 통과 | FG::should_seat_every_mount_spot_button_and_name_it_in_the_place_text_WIR137 |
| WIR-139 | 통과 | FG::should_turn_counter_clockwise_and_pick_each_of_the_four_yaws_WIR139 · CG::should_turn_with_r_and_shift_r_flip_with_f_and_leave_text_boxes_alone_WIR139_WIR140_WIR141 |
| WIR-140 · 141 | 통과 | CG::should_turn_with_r_and_shift_r_flip_with_f_and_leave_text_boxes_alone_WIR139_WIR140_WIR141 |
| WIR-142 | 통과 | FG::should_leave_fence_sensors_unturned_and_disable_the_turn_menu_when_the_kind_has_no_facing_WIR142 |
| WIR-143 | 범위 밖(기존 헤디드 · r27) | — |
| WIR-144 | 통과 | R2::should_spread_the_mount_style_to_every_sensor_after_confirmation_and_undo_it_in_one_step_WIR144 |
| WIR-145 | 통과 | FG::should_spread_the_mount_style_only_to_the_same_kind_when_apply_same_type_is_run_WIR145 · ::should_spread_the_mount_style_only_to_the_selection_and_count_it_in_the_menu_text_WIR145 |
| WIR-146 | 통과 | FG::should_turn_and_flip_through_the_menu_and_confirm_the_all_facing_entry_WIR146 |
| WIR-147 | 통과 | FG::should_move_lanes_and_unplace_through_the_sensor_menu_WIR147 |
| WIR-148 | 통과 | FG::should_copy_a_panel_and_spread_it_to_all_panels_through_the_panel_menu_WIR148 |
| WIR-149 | 통과 | FG::should_select_all_sensors_or_all_panels_from_the_empty_ground_menu_WIR149 |
| WIR-150 · 152 · 153 | 범위 밖(r27) | — |
| WIR-151 | 통과(판정: 가운데 끌기 = 팬 · 가운데 클릭 = 없음) | FenceGestureTests::should_classify_into_one_action_when_given_button_modifiers_target_and_travel |
| WIR-154 | 통과 | R2::should_pan_the_fence_view_with_arrow_keys_when_the_canvas_itself_has_focus_WIR154 |
| WIR-155 | 통과 | FG::should_keep_the_range_toggle_off_without_range_sensors_and_on_with_a_multi_sensor_WIR155 · CG::should_draw_range_and_cable_overlays_only_when_toggled_WIR155_WIR156 |
| WIR-156 | 통과 | CG::should_draw_range_and_cable_overlays_only_when_toggled_WIR155_WIR156 |
| WIR-157 | 통과(키 · 되돌리기 — 끌기 중 Esc 는 헤디드) | FG::should_move_the_enclosure_only_as_a_marker_and_undo_it_WIR157 |
| WIR-158 · 160 · 163 · 167 | 범위 밖(기존 헤디드) | — |
| WIR-159 | 통과 | FG::should_clamp_the_split_ratio_to_the_range_and_the_minimum_heights_WIR159 · ::should_start_from_the_remembered_split_and_save_each_move_WIR159 |
| WIR-161 | 통과 | FG::should_order_notices_action_first_then_warnings_then_information_and_drop_blank_ones_WIR161 |
| WIR-162 | 통과 | CG::should_unplace_with_delete_restore_with_ctrl_z_and_clear_with_esc_on_the_canvas_WIR162_WIR114 |
| WIR-164 | 통과 | FG::should_jump_to_either_end_of_the_chain_with_alt_home_and_alt_end_WIR164 · CG::should_move_to_the_chain_ends_with_alt_home_and_alt_end_on_the_canvas_WIR164 |
| WIR-165 | 통과 | FG::should_offer_the_spacing_combo_only_with_fence_sensors_and_not_count_it_as_a_server_change_WIR165 |
| WIR-166 | 통과 | FG::should_show_the_kind_counts_only_in_the_fence_view_of_the_wiring_step_WIR166 |
| WIR-200 | 통과 | CG::should_title_the_concept_with_both_lanes_or_the_return_line_WIR200_WIR201 |
| WIR-201 | 통과 | CG::should_title_the_concept_with_both_lanes_or_the_return_line_WIR200_WIR201 · ::should_draw_the_return_line_differently_from_a_second_lane_in_the_concept_WIR201_WIR202 |
| WIR-202 | 통과 | CG::should_draw_the_return_line_differently_from_a_second_lane_in_the_concept_WIR201_WIR202 |
| WIR-203 · 204 | 통과 | CG::should_put_the_controller_chip_on_the_chosen_end_of_the_concept_WIR203_WIR204 |
| WIR-205 | 헤디드 남김(개념도 C 칩 실제 끌기) | — |
| WIR-206 | 헤디드 남김(끌기 중 Esc) | — |
| WIR-207 | 통과 | WiringFenceLanesTests::should_say_how_many_fence_sensors_moved_into_the_coil_when_a_band_is_first_chosen_on_razor_panels |
| WIR-208 · 209 · 210 | 통과 | CG::should_name_the_vbus_chip_by_its_gap_and_hide_it_on_mixed_rings_WIR208_WIR210 |
| WIR-211 | 통과(처리기 길) | WiringFenceEditorTests::should_move_the_fence_seat_along_when_a_sensor_is_reordered_in_the_concept_diagram |
| WIR-212 | 통과(처리기 길) | WiringFenceLanesTests::should_put_a_dropped_sensor_between_the_upper_lane_neighbours_when_dragged_across_lanes |
| WIR-213 | 헤디드 남김(끌기 중 Esc) | — |
| WIR-214 · 219 · 220 | 범위 밖(기존 헤디드) | — |
| WIR-215 | 통과 | WiringFenceLanesTests::should_reorder_within_the_lane_when_a_concept_chip_steps_right_with_alt_arrow |
| WIR-216 | 통과(논리 포커스) | R2::should_move_focus_along_the_lane_with_arrows_home_and_end_in_the_concept_WIR216 · ::should_move_focus_to_the_nearest_chip_of_the_other_lane_with_up_WIR216 |
| WIR-217 | 통과 | CG::should_unplace_with_delete_and_restore_with_ctrl_z_in_the_concept_WIR217 |
| WIR-218 | 통과 | R2::should_open_the_same_menu_as_the_3d_view_from_the_concept_with_shift_f10_and_the_menu_key_WIR218 |
| WIR-221 | 통과 | FG::should_say_unknown_without_an_address_and_without_a_ping_probe_WIR221 |
| WIR-222 | 통과 | R2::should_put_the_signal_level_into_the_controller_lamp_name_WIR222 |
| WIR-223 | 통과(건강 → 등 판정 · 주입기 실송신은 헤디드) | FenceGestureTests::should_map_to_a_signal_level_when_network_interface_health_is_read |
| WIR-224 | 통과(GAP-N → 헤드리스) | R2::should_stop_pinging_the_controller_when_the_window_closes_WIR224 |
| WIR-225 | 통과 | CG::should_mark_the_concept_node_selected_when_the_3d_view_selects_it_WIR225 |
| WIR-226 | 통과 | WiringFenceEditorTests::should_share_one_selection_when_fence_table_or_concept_selects |
| WIR-227 | 통과 | ConceptLanesLayoutTests::should_keep_chips_and_thin_labels_and_ticks_when_sixty_five_sensors_share_one_lane · ::should_draw_dot_chips_without_labels_when_two_hundred_fifty_sensors_are_squeezed |
| WIR-300 | 범위 밖(r27) | — |
| WIR-301 | 통과 | R2::should_fill_every_column_of_a_table_row_WIR301 |
| WIR-302 · 303 | 통과 | SG::should_tag_every_row_as_suggested_before_apply_and_as_unsaved_after_apply_WIR302_WIR303 |
| WIR-304 | 통과 | SG::should_mark_both_rows_when_two_sensors_share_a_number_WIR304 |
| WIR-305 | 통과 | SG::should_show_the_not_equal_mark_when_the_bus_address_differs_from_the_position_WIR305 |
| WIR-306 | 통과 | WiringTableRowsTests::should_put_the_enclosure_split_band_before_the_row_after_the_controller_gap |
| WIR-307 · 308 · 311 | 범위 밖(기존 헤디드) | — |
| WIR-309 | 통과 | SG::should_unplace_a_row_when_its_remove_button_is_pressed_WIR309 |
| WIR-310 | 통과 | WiringTableRowsTests::should_mark_the_row_unsaved_and_flip_facing_when_f_is_pressed_in_the_table |
| WIR-312 | 통과(처리기 길 · 제스처는 커널) | R2::should_insert_a_dragged_row_at_the_dropped_slot_like_alt_arrows_WIR312 |
| WIR-313 | 통과(처리기 길) | R2::should_put_a_palette_sensor_at_the_dropped_slot_WIR313 |
| WIR-314 | 통과(D2 · 10-08 수정) | SG::should_undo_a_table_edit_with_ctrl_z_typed_on_the_table_WIR314_D2 |
| WIR-315~322 | 범위 밖(기존 헤디드 SUR) | — |
| WIR-400 · 401 · 402 · 403 · 404 · 405 · 406 · 413 · 415 · 416 | 범위 밖(기존 헤디드) | — |
| WIR-407 | 통과 | SG::should_number_each_kind_inside_its_tier2_or_tier3_band_WIR407 · ::should_keep_the_numbers_of_a_kind_without_a_band_and_say_so_when_the_preset_lacks_it_WIR407 |
| WIR-408 | 통과 | SG::should_refuse_a_malformed_custom_band_and_keep_the_numbers_WIR408 · ::should_renumber_from_a_valid_custom_band_and_clear_the_error_WIR408 |
| WIR-409 | 통과 | SG::should_block_saving_when_twelve_sensors_do_not_fit_under_255_WIR409 |
| WIR-410 | 통과 | R2::should_block_saving_when_a_custom_band_is_smaller_than_the_number_of_sensors_WIR410 |
| WIR-411 | 통과 | SG::should_keep_the_numbers_and_say_so_when_bands_are_cleared_WIR411 |
| WIR-412 | 통과(D1 · 10-08 수정) | SG::should_open_the_collapsed_detail_pane_when_the_band_picker_is_asked_for_WIR412_D1 |
| WIR-414 | 통과 | SG::should_write_nothing_anywhere_when_the_number_table_is_cancelled_WIR414 |
| WIR-417 | 통과 | SG::should_reopen_with_the_same_panels_mounts_spacing_and_bands_after_saving_WIR417 |
| WIR-418 | 통과 | SG::should_count_ch1_and_ch2_positions_from_opposite_ends_of_the_ring_WIR418 |
| WIR-419 | 통과 | SG::should_save_only_on_this_pc_and_write_nothing_to_the_server_when_only_the_fence_shape_changed_WIR419 |
| WIR-420 | 통과 | SG::should_count_a_local_only_change_as_one_unsaved_change_when_closing_WIR420 |
| WIR-421 | 통과 | SG::should_say_nothing_changed_and_disable_saving_when_nothing_was_touched_WIR421 |
| WIR-422 | 통과(GAP-N → 가짜 서버 실패) | WiringFenceReviewFixTests::should_skip_the_local_layout_and_say_so_when_a_server_write_fails |
| WIR-500 | 통과 | SG::should_name_the_sensors_of_a_primary_and_secondary_fault_section_on_a_ring_WIR500 |
| WIR-501 | 통과 | SG::should_ask_for_five_sensors_before_showing_a_fault_example_WIR501 |
| WIR-502 | 통과 | SG::should_count_the_fault_section_from_the_saved_ring_while_a_suggestion_is_pending_WIR502 |
| WIR-503 | 범위 밖(r27 · NATS 실송신) | — |
| WIR-504 | **결함→수정**(D3) | Events.Ui/Tests/EventFaultSectionRingTests::should_name_the_ring_sensors_on_both_ends_of_a_controller_fault_section_WIR504 · ::should_use_the_ring_of_the_sensors_controller_and_say_when_the_section_is_outside_the_saved_ring_WIR504 · ::should_keep_only_the_numbers_when_the_controller_has_no_saved_ring_or_the_section_is_empty_WIR504 · ::should_read_only_saved_primary_placements_of_that_controller_in_chain_order_WIR504 · ::should_show_the_ring_sensor_names_in_the_event_console_detail_from_the_device_cache_WIR504 |
| WIR-505 | 통과 | R2::should_explain_how_the_fault_section_maps_onto_the_ring_in_its_help_WIR505 |
| WIR-600 · 601 · 602 · 604 | 범위 밖(기존 헤디드) | — |
| WIR-603 | 헤디드 남김(F1 = 실제 키보드 포커스 · KeyEventArgs 에 PresentationSource 필요) | — |
| WIR-605 | 헤디드 남김(말풍선 열고 닫기 — 키 등록만 SG::should_register_every_help_key_used_by_the_wiring_and_assembly_windows… 가 봄, 표지 안 담) | — |
| WIR-606 · 607 | 범위 밖(r27) | — |
| WIR-608 | **결함→수정**(D5) | WiringNarrowLayoutTests::should_keep_every_table_column_reachable_in_a_narrow_wiring_window_WIR608 · ::should_need_no_horizontal_scroll_at_the_1440_wide_default_WIR608 |
| WIR-609 | 통과(5 대화창 · 결함 없음) | R2::should_fit_every_button_and_input_of_the_wiring_dialogs_inside_the_window_WIR609 |
| WIR-610 | 범위 밖(사진만) | — |
| ASM-001 ~ 012 · 014 ~ 022 | 범위 밖(기존 헤디드 ASMT · DRAG) | — |
| ASM-013 | 통과 | AG::should_remove_on_delete_only_from_the_board_and_never_inside_a_text_box_or_mid_drag_ASM013 |
| ASM-023 | 통과 | AG::should_carry_position_in_service_and_identity_fields_into_the_definition_and_count_one_change_ASM023_ASM026 |
| ASM-024 | 통과 | AR2::should_send_an_edited_override_under_its_component_key_and_keep_the_others_ASM024 |
| ASM-025 | 통과 | AssemblyViewModelTests::should_flag_a_type_missing_from_the_catalog_instead_of_dropping_it · AssemblyBoardTests::should_report_the_catalog_problem_when_a_loaded_type_is_gone_from_the_catalog |
| ASM-026 | 통과(D4 · 10-08 수정) | AG::should_carry_position_in_service_and_identity_fields_into_the_definition_and_count_one_change_ASM023_ASM026 |
| ASM-027 | 통과 | AssemblyBoardTests::should_report_the_catalog_problem_when_a_loaded_type_is_gone_from_the_catalog · ::should_flag_every_slot_sharing_a_key_when_a_key_is_duplicated |
| ASM-028 | 통과(픽셀 차) | AR2::should_draw_an_out_of_service_block_differently_from_an_active_one_ASM028 |
| ASM-100~116 | 일부 통과(내보내기/가져오기 · 깨진 파일) · 나머지 범위 밖(기존 헤디드) | DevicePresetStoreTests::should_import_with_new_ids_and_clash_suffix_when_exported_file_is_read · ::should_refuse_import_without_touching_store_when_file_is_corrupt_or_newer(표지 ASM-100 — 범위 행이라 collect.py 미집계) |
| ASM-200 ~ 204 · 209 · 210 · 211 · 300 | 범위 밖(기존 헤디드) | — |
| ASM-205 | 통과 | AG::should_keep_the_sensor_wiring_and_bus_address_when_components_are_applied_to_a_wired_sensor_ASM205 |
| ASM-206 | 통과 | PresetRegisterTests::should_not_write_enabled_false_when_a_new_fan_is_added_by_apply |
| ASM-207 | 통과 | AR2::should_send_null_for_the_override_of_a_removed_component_ASM207 |
| ASM-208 | 통과 | DeviceDashboardConsoleTests::should_keep_the_component_button_in_place_but_disabled_when_a_field_is_touched |
| ASM-212 | 통과 | ComponentDisplayUnifyTests::should_switch_between_declared_and_fault_first_when_sort_button_is_toggled |
| ASM-213 | 통과 | ComponentDisplayUnifyTests::should_draw_status_table_with_summary_and_korean_when_one_device_is_selected |
| ASM-214 | 헤디드 남김(주입기 상태 보고 뒤 제어기 요약 — 헤드리스 시험은 이번에 못 만듦) | — |
| ASM-301 | 헤디드 남김(속성 "?" 말풍선 열고 닫기) | — |
| ASM-302 · 303 | 범위 밖(r27) | — |
| ASM-304 | **결함→수정**(D5 후반) | AssemblyNarrowLayoutTests::should_keep_board_blocks_inspector_fields_and_the_commit_button_reachable_in_a_narrow_assembly_window_ASM304 · ::should_keep_the_wide_default_layout_without_a_horizontal_scroll_ASM304 |
| ASM-305 | 범위 밖(사진만) | — |
| ASM-306 | 통과(GAP-N → 헤드리스) | PresetRegisterTests::should_send_nothing_and_fail_when_applying_against_a_legacy_contract · ::should_send_nothing_and_fail_when_registering_against_a_legacy_contract |

요약: 결함→수정 3(WIR-504 · WIR-608 · ASM-304) · 헤디드 남김 9(WIR-123 · 124 · 205 · 206 · 213 · 603 · 605 · ASM-214 · ASM-301 — 그 밖에 WIR-157 의 「끌기 중 Esc」 부분) · 결함 보고만 0 ·
나머지 GAP/GAP-N 행은 위 표의 시험으로 통과. 「범위 밖」 행은 카탈로그에 이미 헤디드 시험이 있거나(SC-* · R2x · r27) 사진 · 관측 항목이다.
