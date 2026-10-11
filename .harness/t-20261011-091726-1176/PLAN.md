# PLAN — 결선 · 조립기 시나리오 남은 항목 + 순서 의존 실패 (t-20261011-091726-1176)

## 목표
1. 첨부 `rem-lib-b.txt` 의 결선(WIR) · 조립기(ASM) 항목(‘시험 없음’ · ‘미실행’)마다 결론: (가) 헤드리스 시험 + `[Trait("Scenario", id)]` · (나) 헤디드 필요 — 이유 · (다) 관찰 항목 — 이유. 최대한 (가).
2. 순서 의존 실패: `dotnet test <Devices.Ui> --filter "Scenario~-"` 에서 AssemblyNarrowLayoutTests ASM304(1000×700) 가 XamlParseException(줄 1427 · KeyNotFoundException 'SelectedTimeFormat') — 원인 증거 → 수정 → 5회 연속 실패 0.

## 규칙(요약)
창 금지(Show · 화면 밖 창 금지) · 지도 DB/운영 서버/실제 NATS 금지 · 새 시험마다 Scenario 표지 · 기존 시험 본문 불변(표지 한 줄만) · 공개 형식은 더하기만 · 새 .cs UTF-8 BOM · x:Name 불변 · 대외비 명칭 금지.
셸 주의: Bash 에서 `cd`+git, 파이프 뒤 sed/grep -v, 환경변수 접두, 루프는 자동 거절 → 명령 하나씩, 환경변수는 `dotnet test -e K=V`.

## 진행
- [x] 첨부 2개 읽기(카탈로그 §6 WIR · ASM 전부)
- [x] 순서 의존 실패 원인 규명(아래 "원인 — ASM-304 순서 의존")
- [x] 수정: `Tests/XamlSchemaWarmup.cs`(시험 호스트에서만 도는 모듈 초기화기) + 회귀 시험 `Tests/XamlSchemaWarmupTests.cs` 2건 통과
- [x] WIR 항목 시험 — 완료: 002 · 003(DeviceConsoleEntryScenarioTests) · 004 · 005 · 006 · 009 · 010 · 011 · 012 · 106 · 163 · 300 · 307 · 308 · 311 · 400~406 · 413 · 415 · 416(WiringScenarioRound3Tests 21) · 117(FenceChipPlateTests 기존 이론 시험에 표지 한 줄).
      완료: WiringCanvasRound3Tests 23(100 · 101 · 113 · 115 · 122~126 · 130 · 134 · 138 · 143 · 150 · 152 · 153 · 158 · 167 · 205 · 206 · 213 · 214 · 219 · 220).
        함정: 부모 없는 캔버스는 어도너 층이 없어 덧그림(빨강 점 · 띠)이 안 그려진다 → AdornerDecorator 로 감쌈 · 줌 상한 1.6(12대로 맞춤 배율을 낮춤) · GridSplitter 는 놓기 전 Layout 필요.
      완료: WiringHelpLayoutRound3Tests 19(010 뷰 · 160 · 600 · 601 · 602 · 603 · 604 · 605 · 606 · 607). 판정: 129 · 610 = 관찰 항목. 503 = Events.Ui 쪽(작업자에게 맡김 — 아래 결과표).
      (옛 메모) 남음: 160 및 Tests/WiringHelpLayoutRound3Tests.cs(느슨한 WiringView = WiringNarrowLayoutTests.LooseWiringView): 600~607 · 010(뷰). 판정만: 129(다) · 503 · 610(다)
- [x] ASM 항목 시험 — AssemblyScenarioRound3Tests 27 · DeviceConsoleEntryScenarioTests(ASM 2) · DetailComponentTableScenarioTests 4 · AssemblyLayoutScenarioTests 19. ASM-305 = 관찰 항목
- [x] `--filter "Scenario~-"` 5회 연속 — 아래 "실행 기록"
- [x] 손댄 시험 프로젝트 전부 실행(Devices.Ui 3,972 · Events.Ui 1,989 실패 0) + sln 빌드
- [x] id 별 결과표(맨 끝)

## 원인 — ASM-304 순서 의존 (증거)
1. **실패 스택**(이전 회차 증거 `docs/tests/evidence/20261011-091030-all-scenarios-3/Ironwall.Dotnet.Libraries.Devices.Ui.trx`):
   `KeyNotFoundException: The given key 'SelectedTimeFormat'` ← `ConcurrentDictionary.get_Item` ← `System.Windows.Baml2006.WpfXamlType.FindMember` ← `XamlType.GetMember` ← `SetterTriggerConditionValueConverter.ResolveValue` ← `Setter.EndInit`.
   Setter 는 Styles.Console.xaml 의 `Console.DateTimePicker` 트리거 안 `<Setter Property="SelectedTimeFormat" …/>`(mah:DateTimePicker).
2. **WPF 내부 IL 확인**(임시 진단 시험, 리플렉션 — 지금은 지움): `WpfXamlType.Members` 게터 = `ldfld _members → (null 이면) newobj ConcurrentDictionary → stfld _members → ldfld _members`(잠금 · Interlocked 없음).
   `CacheAndReturnXamlMember` = `get_Members().TryAdd(name, m)` 다음 **`get_Members()[name]` 을 다시 읽음**.
   → 두 스레드가 같은 형식을 처음 동시에 찾으면 A 가 사전1 에 넣은 뒤 B 가 `_members` 를 사전2 로 덮고, A 가 사전2 에서 읽어 KeyNotFound.
3. **강제 재현**(같은 임시 진단): 공용 문맥의 DateTimePicker `_members` 를 매 판 null 로 비우고 STA 4개가 Barrier 로 동시에 `FindMember("SelectedTimeFormat")` →
   20000판 중 **114판 KeyNotFound**(메시지 동일: "The given key 'SelectedTimeFormat' was not present in the dictionary.") · 대조(매 판 빈 사전을 **미리 넣음**) → **0판**.
4. **왜 순서 의존인가**: 스키마 문맥(`XamlReader.GetWpfSchemaContext()` · BAML 공용 문맥)은 프로세스 공용이고 형식별 사전은 처음 한 번만 만들어진다.
   Styles.Console.xaml 을 느슨하게 읽는 시험이 5개 클래스(AssemblyNarrowLayout · ComponentTableHostView · WiringFenceEditorView · WiringHeadlessRound2 · WiringNarrowLayout)이고 xUnit 이 클래스별로 병렬 → 누가 먼저 DateTimePicker 를 처음 보느냐에 따라 드물게 난다(단독 실행은 경쟁 상대가 없어 3/3 통과).
   미리 만들기를 끈 채(`-e IRONWALL_XAML_WARMUP=off`) Scenario 묶음 4회는 모두 통과 — 자연 발생은 드물다(위 TRX 가 실제 발생 1건).
5. **제품 영향 없음**: 라이브러리 제품 코드 · 호스트(Dotnet.Monitoring.Solution) 제품 코드 어디에도 둘째 UI 스레드(SetApartmentState · Dispatcher.Run)가 없다(Grep — 호스트의 유일한 일치 WatchdogSetupViewModel 은 DispatcherTimer). XAML 은 UI 스레드 하나에서만 읽혀 이 경쟁이 없다 → 시험 격리 쪽에서 고친다(결정 D1).

## 수정 — 시험 격리
- `Ironwall.Dotnet.Libraries.Devices.Ui/Tests/XamlSchemaWarmup.cs`: `[ModuleInitializer]`, `TestCrashLog.IsTestHost` 일 때만(이 프로젝트는 시험 코드가 제품 DLL 에 같이 실림 — 선례 TestCrashLog).
  UI 어셈블리(PresentationFramework/Core · WindowsBase · System.Xaml · MahApps · ControlzEx · MaterialDesign · Behaviors · Ironwall.*; 참조 사슬 + 출력 폴더 DLL — MahApps 는 XAML 에서만 쓰여 참조 사슬에 없었다)의 공개 형식마다
  공용 문맥 둘(느슨한 XAML · BAML)에서 `WpfXamlType.Members/AttachableMembers` 게터를 한 번 불러 사전을 **한 스레드에서 미리** 만든다. 모듈 초기화기라 어떤 시험 코드보다 먼저 돈다(형식 초기화 잠금). 9ms · 5,691형식 · 22,764 사전.
  진단용 끄기: `dotnet test … -e IRONWALL_XAML_WARMUP=off`.
- 회귀 시험 `XamlSchemaWarmupTests`:
  - `should_precreate_wpf_member_caches_in_both_shared_contexts_before_any_test_runs` — 두 공용 문맥에서 DateTimePicker · TimePickerBase · RangeSlider(어떤 시험도 안 쓰는 형식) · Setter · Trigger · Style · ListBox · TextBox · PackIcon · AssemblyView 의 `_members` 가 이미 있다.
    (첫 시도 빨강: "WpfSharedXamlSchemaContext · DateTimePicker 멤버 사전이 아직 없다" — MahApps 가 참조 사슬에 없어 빠졌던 것 → 출력 폴더 DLL 씨앗 추가 → 초록)
  - `should_resolve_the_trigger_setter_member_from_many_sta_threads_at_once_when_the_context_is_warmed_first` — **이 시험만 쓰는 새 WpfSharedXamlSchemaContext** 를 매 판 만들어(공용 문맥은 안 건드림) STA 4개 동시 첫 조회:
    미리 안 만듦 KeyNotFound 2~4/3000(같은 메시지, 기록만 — 확률적이라 단언 안 함) · 미리 만듦 0/3000(단언).

## 결함 — 빨강→초록
| 결함 | 시험 | 수정 전(빨강) | 수정 | 수정 뒤 |
|---|---|---|---|---|
| ASM-D1(ASM-021) 채널 칸 「1a」 인데 [등록/적용] 이 켜져 있다 — 화면은 1a, 나가는 값은 옛 채널 | AssemblyScenarioRound3Tests::should_disable_the_primary_button_with_the_reason_as_its_tooltip_when_the_channel_text_is_not_a_number_ASM021 | `Assert.False() Failure Expected: False Actual: True`(vm.CanCommit) | AssemblyViewModel: `InputError`(보드 항목의 ChannelError) → CanCommit · CanSaveAsPreset 막음 · CommitBlockedReason(툴팁)에 그 문장. 채널 글은 슬롯을 안 바꿔 보드가 안 울리므로 항목 PropertyChanged 를 받아 RefreshAll(DetachItem 으로 구독 해제) | 통과(조립기 묶음 163/163) |

결함 판정 근거(ASM-D1, 증거 2): ① 명세 둘 — 첨부 카탈로그 ASM-021 「빈 key · 채널 1a → 주 단추 꺼짐 + 툴팁」, docs/tests/gis-full-scenario-catalog.md SC-ASM-009 「주 단추 비활성」(호스트 헤디드 ConsoleGapDevicesAssemblyTests 도 `!enabledBadCh` 를 단언) ② 코드 — `CanCommit` 이 `_board.HasErrors`(key 오류만)만 보고 `BoardSlotViewModel.ChannelError` 를 보지 않음, 위 시험이 True 로 재현.

## 결정
- D1: 순서 의존 실패는 WPF(System.Windows.Baml2006.WpfXamlType)의 잠금 없는 늦은 초기화 경쟁 — 제품은 UI 스레드 하나라 영향 없음 → 시험 격리(모듈 초기화기 미리 만들기)로 고친다. 기존 시험 본문은 그대로.
  다른 안(어셈블리 병렬 끄기 `CollectionBehavior(DisableTestParallelization)`)은 전체 시험을 느리게 하고 원인을 가리므로 택하지 않음.

- D2: 창 금지 — Devices.Ui 기존 시험 중 화면 밖 창(Window.Show)을 쓰는 클래스 11개(ComponentTableHostViewTests · ConsoleWindowChromeShownTests · StaCleanupTests · UnitConsoleNonModalTests · UnitConsoleTreeSelectionReloadTests · UnitMapCanvasInputTests · UnitMapCanvasRenderTests · UnitMapNodeRenderCostTests · WiringFenceEditorViewTests · WiringFenceHeightViewTests · WiringFenceViewTests)는 이 작업에서 돌리지 않는다(최종 실행은 --filter 로 제외). 새 시험은 전부 창 없이: VM · 부모 없는 캔버스(Measure/Arrange) · 느슨한 XAML · HWND 없는 PresentationSource.
- D3: 레이아웃 '잘림' 판정(ASM-302/303 · WIR-606/607) — WPF 는 좁은 칸에서도 요소에 제 크기를 다 주고 부모 배치 클립으로 자르므로 ActualWidth 비교는 헛돈다(자기 점검 시험이 빨강으로 잡음). 그래서 "글 · 조상의 렌더 폭(+여백) > LayoutSlot 폭" 으로 판정하고, 판정기 자체의 대조 시험(should_flag_a_one_line_text_that_is_wider_than_its_cell_…)을 둔다.
- D4: ASM-021 수정 범위 — 명세는 주 단추만 말하지만 [프리셋으로 저장]도 같은 까닭(화면 ≠ 저장값)이라 함께 막았다(CanSaveAsPreset).

## 발견
- `Ironwall.Dotnet.Libraries.sln` 에 Devices.Ui 가 없다 → 하네스 sln 검사는 Devices.Ui 시험을 돌리지 않는다. Devices.Ui 는 따로 돌려 기록한다.

## 실행 기록(2026-10-11, 모든 제품 수정 · 새 시험 반영 뒤)
- 빌드: `dotnet build Ironwall.Dotnet.Libraries.Devices.Ui/Ironwall.Dotnet.Libraries.Devices.Ui.csproj` 오류 0(세션 사이 참조 프로젝트 bin 이 지워져 의존까지 다시 빌드).
- `dotnet test Ironwall.Dotnet.Libraries.Devices.Ui/Ironwall.Dotnet.Libraries.Devices.Ui.csproj --no-build --filter "Scenario~-"` **5회 연속**:
  1회 실패 0 · 통과 330/330 · 2회 330/330 · 3회 330/330 · 4회 330/330 · 5회 330/330(시작 때 207건 → 새 시험 포함 330건).
- Devices.Ui 전체(창 쓰는 11개 클래스만 --filter 제외 — 결정 D2): **실패 0 · 통과 3,972/3,972**.
- 새 .cs 9개 BOM(EF BB BF) 확인: XamlSchemaWarmup · XamlSchemaWarmupTests · AssemblyScenarioRound3Tests · DeviceConsoleEntryScenarioTests · DetailComponentTableScenarioTests · AssemblyLayoutScenarioTests · WiringScenarioRound3Tests · WiringCanvasRound3Tests · WiringHelpLayoutRound3Tests.
- 기존 시험 파일 변경은 FenceChipPlateTests 의 `[Trait("Scenario", "WIR-117")]` 한 줄뿐(본문 불변).

- Events.Ui(WIR-503 시험을 더해 손댄 프로젝트): `dotnet build Ironwall.Dotnet.Libraries.Events.Ui/...csproj` 오류 0 → 새 시험 3/3 통과 → 전체(창 쓰는 6개 클래스 SuppressionRowSelectBindingTests · SuppressionRecurrenceRadioTests · SuppressionDayChipAutomationTests · MappingDragInteractionTests · EventToolbarChipAutomationTests · EventConsoleSingleSelectionDetailTests 만 제외) **실패 0 · 통과 1,989/1,989**. 새 파일 BOM 확인.
- `dotnet build Ironwall.Dotnet.Libraries.sln` 오류 0. sln 의 시험 실행(`dotnet test sln`)은 Utils.Tests 등의 화면 밖 창 시험이 섞여 있어 이 세션에서는 돌리지 않았다(창 금지) — 하네스 검사가 돈다. sln 에는 Devices.Ui · Events.Ui 가 없어 이번 변경의 시험은 위에서 따로 돌렸다.

## 남은 일
- 없음. 헤디드 몫: WIR-503 의 실제 화면 그림 · 호스트 라우터. 관찰 항목: WIR-129 · WIR-610 · ASM-305.

## id 별 결과표
가 = 헤드리스 시험으로 기대를 단언(표지 달림) · 나 = 헤디드 필요 · 다 = 관찰 항목. 클래스 약호: ASM3 = AssemblyScenarioRound3Tests · ENT = DeviceConsoleEntryScenarioTests · DCT = DetailComponentTableScenarioTests · ALY = AssemblyLayoutScenarioTests · W3 = WiringScenarioRound3Tests · WC3 = WiringCanvasRound3Tests · WH3 = WiringHelpLayoutRound3Tests.

### 결선(WIR)
| id | 결론 | 시험 메서드 |
|---|---|---|
| WIR-002 | 가 | ENT::should_enable_setup_wiring_for_one_controller_and_disable_it_with_a_reason_for_two_WIR002 |
| WIR-003 | 가 | ENT::should_show_the_selected_controller_in_the_detail_before_wiring_is_opened_WIR003 |
| WIR-004 | 가 | W3::should_title_the_window_setup_wiring_map_and_name_the_controller_as_the_subject_WIR004 |
| WIR-005 · 006 | 가 | W3::should_open_a_seed_without_wiring_in_the_fence_view_suggesting_all_twelve_by_number_with_no_changed_rows_WIR005_WIR006 |
| WIR-009 | 가 | W3::should_move_the_step_strip_between_the_sensor_and_wiring_steps_WIR009 |
| WIR-010 | 가 | W3::should_show_exactly_one_of_fence_view_and_table_view_WIR010 · WH3::should_show_only_the_fence_view_or_only_the_table_view_in_the_window_WIR010 |
| WIR-011 | 가 | W3::should_count_the_unsaved_changes_and_stay_open_when_closing_is_declined_WIR011 |
| WIR-012 | 가 | W3::should_close_without_asking_after_everything_was_saved_WIR012 |
| WIR-100 · 101 | 가 | WC3::should_start_tilted_and_keep_chip_order_and_names_while_showing_the_flat_note_until_tilt_is_chosen_again_WIR100_WIR101(컴파일된 FenceView 의 [평면]/[입체] 단추 Click) |
| WIR-106 | 가 | W3::should_apply_a_height_of_2_4_and_refuse_7_with_the_apply_button_off_WIR106(2.4 · 7) |
| WIR-113 | 가 | WC3::should_keep_the_selection_and_say_so_when_a_rubber_band_is_cancelled_with_esc_WIR113 |
| WIR-115 | 가 | WC3::should_title_the_pane_with_two_sensors_after_ctrl_clicking_two_chips_WIR115 |
| WIR-117 | 가 | FenceChipPlateTests::should_put_the_number_plate_below_the_sensor_body_and_keep_it_out_of_the_selection_and_hit_rect(기존 이론 시험 7판 — 표지 한 줄 추가) |
| WIR-122 | 가 | WC3::should_show_red_snap_dots_while_dragging_a_post_sensor_hide_them_on_release_and_seat_it_on_a_far_panel_centre_WIR122(빨강 = SnapDot 잉크 · Critical 붓) |
| WIR-123 | 가 | WC3::should_put_the_sensor_back_and_say_drop_near_a_red_dot_when_released_away_from_every_dot_WIR123 |
| WIR-124 | 가 | WC3::should_refuse_a_group_drop_and_count_the_sensors_without_a_point_WIR124 |
| WIR-125 | 가 | WC3::should_move_both_selected_sensors_and_keep_their_spacing_when_one_of_them_is_dragged_WIR125 |
| WIR-126 | 가 | WC3::should_put_everything_back_and_say_no_server_call_when_a_drag_is_cancelled_with_esc_WIR126 |
| WIR-129 | 다 | 관찰 항목 — Alt 먼저 누른 Alt+Shift 는 한국어 Windows 입력 언어 전환이 Shift 를 가로채는 OS 입력기 동작(카탈로그도 "관측 · 예상"). 앱은 수정키를 받은 대로만 판정(키 경로는 WIR-128 시험) — 실제 입력기 경합은 앱 단언 대상이 아니다 |
| WIR-130 | 가 | WC3::should_step_the_selected_sensor_to_the_next_previous_and_typed_panel_with_the_mount_buttons_WIR130 |
| WIR-134 | 가 | WC3::should_stop_a_smart_multisensor_2_on_a_razor_panel_at_the_top_of_the_panel_and_never_reach_the_coil_WIR134 |
| WIR-138 | 가 | WC3::should_draw_three_sensors_on_a_brick_panel_without_overlapping_chips_WIR138 |
| WIR-143 | 가 | WC3::should_offer_apply_this_mount_to_every_sensor_on_right_click_and_change_nothing_when_the_menu_is_dismissed_WIR143 |
| WIR-150 | 가 | WC3::should_pan_without_opening_a_menu_when_the_canvas_is_dragged_with_the_right_button_WIR150 |
| WIR-152 | 가 | WC3::should_zoom_around_the_pointer_and_update_the_zoom_text_on_wheel_steps_WIR152(실제 휠 장치 이벤트는 아님 — 휠 처리기가 부르는 ZoomAt 과 FenceView 의 `.ZoomText`) |
| WIR-153 | 가 | WC3::should_change_the_percent_with_the_zoom_buttons_and_come_back_to_the_fitted_view_with_fit_WIR153 |
| WIR-158 | 가 | WC3::should_grow_or_shrink_the_canvas_by_the_dragged_distance_and_remember_the_ratio_WIR158(±80) |
| WIR-160 | 가 | WH3::should_hide_the_detail_pane_widen_the_canvas_and_restore_it_with_the_detail_toggle_WIR160 |
| WIR-163 | 가 | W3::should_change_the_port_of_the_third_sensor_from_3_10_to_4_9_with_alt_right_WIR163 |
| WIR-167 | 가 | WC3::should_name_the_concept_cell_upper_lane_after_the_lane_upper_button_WIR167 |
| WIR-205 | 가 | WC3::should_say_where_the_controller_goes_while_dragging_it_past_the_middle_and_flip_it_on_release_WIR205 |
| WIR-206 · 213 | 가 | WC3::should_leave_the_concept_as_it_was_when_a_controller_or_cell_drag_is_cancelled_with_esc_WIR206_WIR213 |
| WIR-214 | 가 | WC3::should_move_a_focused_concept_cell_between_lanes_with_alt_up_and_alt_down_WIR214 |
| WIR-219 | 가 | WC3::should_say_normal_on_the_concept_signal_line_when_the_controller_at_127_0_0_1_answers_WIR219(가짜 ping — 실제 ICMP 아님) |
| WIR-220 | 가 | WC3::should_say_no_response_after_the_down_streak_of_failed_pings_to_192_0_2_10_within_15_to_40_seconds_WIR220(연속 3회 실패 · 5초 간격 · 시간 제한 1초 → 15~18초) |
| WIR-300 | 가 | W3::should_head_the_table_with_the_controller_name_address_and_a_chain_summary_WIR300 |
| WIR-307 | 가 | W3::should_attach_an_unplaced_chip_at_the_chain_end_and_say_so_when_enter_is_pressed_in_the_palette_WIR307 |
| WIR-308 | 가 | W3::should_insert_the_fourth_sensor_into_the_second_place_with_two_alt_left_and_restore_it_with_undo_WIR308 |
| WIR-311 | 가 | W3::should_lay_every_sensor_out_by_number_in_one_line_and_leave_nothing_unplaced_WIR311 |
| WIR-400 | 가 | W3::should_turn_all_twelve_suggestions_into_changed_rows_when_the_suggestion_is_accepted_WIR400 |
| WIR-401 · 404 | 가 | W3::should_patch_twelve_rows_with_wiring_v3_on_line_1_in_chain_order_facing_front_and_leave_no_changes_WIR401_WIR404 |
| WIR-402 | 가 | W3::should_send_nothing_when_saving_is_answered_no_WIR402 |
| WIR-403 | 가 | W3::should_create_four_new_sensors_with_their_wiring_when_saved_WIR403 |
| WIR-405 | 가 | W3::should_refuse_to_save_and_keep_the_api_value_when_another_user_renamed_the_sensor_meanwhile_WIR405 |
| WIR-406 | 가 | W3::should_preview_chip_numbers_one_to_twelve_with_the_tier4_band_WIR406 |
| WIR-413 · 415 | 가 | W3::should_show_the_number_change_table_with_heading_warning_and_rows_then_write_the_previewed_numbers_and_wiring_v3_WIR413_WIR415 |
| WIR-416 | 가 | W3::should_reopen_with_the_turned_and_flipped_sensor_kept_and_write_back_and_yaw_to_the_api_WIR416 |
| WIR-503 | 가 | Events.Ui/Tests/EventFaultArrivalScenarioTests(가짜 API 기록 먼저 → 같은 번호 MALFUNCTION NATS 한 번 → 카드 · 상세 「1차 선 4 → 5」 → 조치보고로 닫힘): ::should_raise_one_card_and_show_first_section_in_event_detail_when_controller_fault_arrives_after_api_record_WIR503 · ::should_close_card_and_report_dialog_and_write_one_action_when_the_fault_card_is_reported_WIR503 · ::should_close_card_and_disable_open_report_dialog_when_another_operator_reports_the_fault_WIR503. 지도 카드 · 모달 창이 실제 화면에 그려지는 모습과 호스트 라우터(NatsDomainService)의 ACTION_REPORT 전달은 헤디드(호스트) 몫 |
| WIR-600 · 601 | 가 | WH3::should_explain_shift_drag_in_the_fence_question_mark_under_its_alias_ids_and_close_it_with_esc_WIR600_WIR601 |
| WIR-602 | 가 | WH3::should_list_open_one_at_a_time_close_on_esc_and_outside_and_open_the_concept_one_with_f1_in_the_wiring_window_WIR602 |
| WIR-603 | 가 | WH3::should_open_the_question_mark_of_the_section_in_focus_with_f1_in_the_3d_view_table_and_panel_pane_WIR603 |
| WIR-604 | 가 | WH3::should_close_only_the_callout_with_the_first_esc_in_the_make_sensors_dialog_and_let_the_second_esc_through_WIR604(둘째 Esc = 대화창 틀의 취소 SecondaryInvoked) |
| WIR-605 | 가 | WH3::should_open_and_close_the_other_question_marks_of_the_wiring_window_WIR605(대역 · 망 칸 · 선택 칸 · 미배치 · 표 · 센서 · 붙여넣기 7판) |
| WIR-606 · 607 | 가 | WH3::should_keep_every_button_input_and_one_line_text_of_the_wiring_window_unclipped_in_fence_and_table_view_in_both_themes_WIR606_WIR607(4판) · WH3::should_repaint_the_wiring_window_with_the_dark_tokens_WIR607(2판) |
| WIR-610 | 다 | 관찰 항목 — 갤러리(창 사진)의 판정은 사람이 보는 사진 비교라 자동 단언이 무의미(잘림 · 다시 칠은 WIR-606/607 이 단언) |

### 조립기(ASM)
| id | 결론 | 시험 메서드 |
|---|---|---|
| ASM-001 | 가 | ENT::should_offer_the_assembly_menu_only_on_category_rails_and_never_on_the_groups_rail_ASM001 |
| ASM-002 | 가 | ENT::should_list_exactly_assembler_register_from_preset_and_preset_manager_under_the_menu_and_open_each_for_the_rail_ASM002 |
| ASM-003 · 004 | 가 | ASM3::should_title_a_new_assembly_with_the_category_and_offer_only_its_parts_ASM003_ASM004(2판) |
| ASM-005 | 가 | ASM3::should_filter_the_palette_by_code_or_label_and_show_all_again_when_the_search_is_cleared_ASM005 |
| ASM-006 | 가 | ASM3::should_add_the_part_at_the_board_end_say_so_and_show_its_slot_in_the_view_when_enter_is_pressed_ASM006 |
| ASM-007 | 가 | ASM3::should_build_the_same_board_when_palette_blocks_are_dropped_as_when_enter_is_pressed_ASM007 |
| ASM-008 | 가 | ASM3::should_treat_five_units_as_a_click_and_twelve_as_a_drag_on_both_drag_sources_of_the_assembler_ASM008 |
| ASM-009 | 가 | ASM3::should_reorder_the_same_way_whether_a_slot_handle_is_dropped_on_a_row_half_or_alt_arrow_is_pressed_ASM009(3판 — 보드에 붙은 실제 ReorderKeyboardBehavior) |
| ASM-010 | 가 | ASM3::should_move_the_last_slot_one_up_with_alt_up_and_stay_put_at_the_top_ASM010 |
| ASM-011 · 016 | 가 | ASM3::should_say_removed_and_then_undone_and_bring_back_the_same_slots_ASM011_ASM016 |
| ASM-012 | 가 | ASM3::should_remove_the_same_way_and_say_the_same_when_slots_are_dropped_on_the_bin_as_with_the_remove_button_ASM012 |
| ASM-014 | 가 | ASM3::should_leave_the_board_untouched_while_a_drag_only_hovers_and_is_then_cancelled_with_esc_ASM014(조립기 쪽: 끄는 동안의 CanDrop 은 무부작용 · Esc 취소는 Drop 을 부르지 않는다 — 커널 Esc 자체는 Utils.Tests CaptureDragLifecycleTests 몫) |
| ASM-015 | 가 | ASM3::should_ignore_a_drop_outside_the_board_and_bin_such_as_back_on_the_palette_ASM015 |
| ASM-017 | 가 | ASM3::should_add_three_numbered_slots_and_say_expanded_and_refuse_a_colliding_second_round_ASM017 |
| ASM-018 | 가 | ASM3::should_preview_the_keys_and_channels_mark_collisions_and_leave_the_board_alone_when_the_repeat_dialog_is_cancelled_ASM018 |
| ASM-019 | 가 | ASM3::should_renumber_the_channels_from_one_in_board_order_and_say_so_ASM019 |
| ASM-020 | 가 | ASM3::should_swap_the_palette_with_the_category_keep_the_board_and_name_parts_that_do_not_fit_then_come_back_ASM020 |
| ASM-021 | 가 · 결함→수정(ASM-D1) | ASM3::should_disable_the_primary_button_with_the_reason_as_its_tooltip_when_the_key_is_blank_ASM021 · ::…_when_the_channel_text_is_not_a_number_ASM021 |
| ASM-022 · 201 | 가 | ASM3::should_patch_one_more_part_with_its_label_after_confirming_the_diff_and_say_applied_ASM201_ASM022 |
| ASM-200 | 가 | ASM3::should_keep_apply_off_when_editing_device_components_only_reorders_them_with_alt_up_ASM200 |
| ASM-202 | 가 | ASM3::should_refuse_and_send_nothing_when_the_server_parts_changed_after_the_assembler_was_opened_ASM202 |
| ASM-203 | 가 | ASM3::should_bring_back_keys_types_and_labels_after_apply_refetch_and_reopen_for_a_controller_ASM203 |
| ASM-204 | 가 | ASM3::should_bring_back_keys_types_and_labels_after_apply_refetch_and_reopen_for_a_sensor_ASM204 |
| ASM-209 | 가 | DCT::should_show_no_parts_without_a_sort_chip_or_header_row_for_a_seed_controller_without_parts_ASM209 |
| ASM-210 | 가 | DCT::should_draw_one_header_row_per_table_and_one_sort_chip_for_a_ptz_and_ir_camera_ASM210 |
| ASM-211 | 가 | DCT::should_summarize_one_ptz_fault_in_korean_when_the_injector_reports_it_ASM211(주입기 보고와 같은 모양의 부품 상태를 모델에 싣는다 — 실제 주입기 · NATS 아님) |
| ASM-214 | 가 | DCT::should_summarize_and_list_the_component_states_of_a_controller_ASM214 |
| ASM-300 | 가 | ASM3::should_list_open_one_at_a_time_close_on_esc_and_outside_and_answer_f1_by_the_nearest_section_in_the_assembler_ASM300 |
| ASM-301 | 가 | ASM3::should_open_and_close_every_inspector_question_mark_of_the_assembler_ASM301 |
| ASM-302 · 303 | 가 | ALY::should_keep_every_button_input_and_one_line_text_of_the_assembler_windows_unclipped_in_both_themes_ASM302_ASM303(창 6종 × 2테마 = 12판) · ALY::should_repaint_the_assembler_windows_with_the_dark_tokens_ASM303(6판) · 판정기 대조 ALY::should_flag_a_one_line_text_that_is_wider_than_its_cell_so_the_layout_audit_is_not_vacuous |
| ASM-305 | 다 | 관찰 항목 — 갤러리(창 3종 사진) 판정은 사람이 보는 사진 비교(잘림 · 다크는 ASM-302/303 이 단언) |

### 순서 의존 실패
| 항목 | 결론 | 시험 |
|---|---|---|
| ASM-304 순서 의존(KeyNotFound 'SelectedTimeFormat') | 원인 = WPF WpfXamlType 늦은 초기화 경쟁(시험의 여러 STA 스레드) · 시험 격리로 수정 | XamlSchemaWarmupTests 2건 · Scenario 묶음 5회 연속 330/330 |
