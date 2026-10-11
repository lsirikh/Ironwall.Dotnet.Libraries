# PLAN — 지도 헤드리스 검증 남은 일 마무리 (t-20261011-081026-0754)

## 목표
선행 작업 t-20261011-073817-787e(첨부 `map-plan-787e.md`)가 예산 때문에 남긴 일을 끝낸다.
1. 결함 7건(LAY-D1~D3 · AUX-D1~D4) 제품 수정 + 결함 증명 시험 빨강→초록 기록(단언은 약하게 바꾸지 않음).
2. 의심 3건(EDT-S1~S3) + MAP-066 손잡이 사유 툴팁 판단 → 결함이면 수정(빨강→초록), 의도면 근거.
3. 미작성 항목 시험: MAP-089 · 124 · 128 · 151 · 152 · 155 · 156 · 164 · 169 · 175 · 176 · 180 · 188.
4. 카탈로그 §1 MAP · §3.5(라이브러리 쪽) 중 "헤디드 남김"이 아닌데 시험이 없는 줄 전부 덮기.
5. tests/GMaps.* 7개 프로젝트 전부 실행(창 시험 MapLocateFitTests · ComponentCardScreenSizeTests 만 --filter 제외) 실패 0 기록 + sln 검사.

## 규칙(요약)
창 금지 · 지도 DB/운영 서버/실제 NATS 금지 · 새 시험마다 `[Trait("Scenario", id)]` · 기존 시험 본문 불변(표지 한 줄만) · 새 .cs UTF-8 BOM · 공개 형식은 더하기만.
셸: Bash 도구에서 `$변수`·for 루프는 자동 거절된다 → 명령을 하나씩 나열.

## 진행
- [x] 첨부 2개 읽기, 시험 프로젝트 상태 확인
- [x] 수정 전 기준 실행(빨강 확인, 아래 "결함" 표의 실패 메시지는 이 세션에서 다시 받은 것)
  - MapLayer 23/26(실패 3 = LAY-D1~D3) · MapAux 23/27(실패 4 = AUX-D1~D4) · MapEdit 22/22 · MapNav 29/29
- [x] 결함 7건 수정 → 초록(MapLayer 26/26 · MapAux 27/27)
- [x] 의심 4건 판단 → 넷 다 결함으로 판정, 판정 시험 먼저 써서 빨강 확인 → 수정 → 초록(MapEdit 27/27 · MapLayer 28/28)
- [x] 미작성 13항목 + 훑어서 찾은 2항목(MAP-117 · 118) 시험 — 작업자 둘(MapEdit: 089 · 117 · 118 · 124 · 128 / MapAux: 151 · 152 · 155 · 156 · 164 · 169 · 175 · 176 · 180 · 188 + MAP-083 최소 줌 스윕 재판정). 새 결함 1건(EDT2-D1) → 수정
- [x] LAY-D2 · LAY-D3 수정의 나머지 갈래 대조 시험(LockWhileSelectedTests 3 · LayerPanelDragClampTests 2)
- [x] 카탈로그 남은 줄 훑기(결정 D3 · D4)
- [x] 전체 회귀 + sln — 아래 "최종 회귀"

## 최종 회귀(모든 제품 수정 · 새 시험 반영 뒤, 2026-10-11)
GMaps.Ui 빌드 오류 0 → 시험 프로젝트는 `--no-dependencies` 빌드(오류 0) → `--no-build` 실행:
| 프로젝트 | 결과 | 비고 |
|---|---|---|
| tests/GMaps.Housing.Tests | 292/292 통과 | `--filter "FullyQualifiedName!~MapLocateFitTests"`(창 시험) |
| tests/GMaps.PropertyPanel.Tests | 249/249 통과 | `--filter "FullyQualifiedName!~ComponentCardScreenSizeTests"`(창 시험) |
| tests/GMaps.MapNav.Tests | 29/29 통과 | |
| tests/GMaps.MapEdit.Tests | 34/34 통과 | 787e 22 + 이번 12 |
| tests/GMaps.MapLayer.Tests | 33/33 통과 | 787e 26 + 이번 7 |
| tests/GMaps.MapAux.Tests | 45/45 통과 | 787e 27 + 이번 18 |
| tests/GMaps.Ui.Tests | 757/757 통과 | |
| `dotnet test Ironwall.Dotnet.Libraries.sln` | 10개 어셈블리 2,533개 실패 0 | 하네스 검사 |
새 .cs 11개 전부 BOM(EF BB BF) 확인. 기존 시험 파일은 하나도 고치지 않았다(git status 에 tests/ 의 M 없음).

## 결함 — 빨강→초록
| 결함 | 시험 | 수정 전(빨강) 실패 메시지 | 수정 | 수정 뒤 |
|---|---|---|---|---|
| LAY-D1 | LayerPanelScenarioTests::should_not_write_image_row_to_db_when_overlay_image_is_toggled_without_edit_permission | Expected invocation on the mock should never have been performed, but was 1 times: d => d.UpdateImageAsync(...) | MapViewModel.OnLayerVisibilityChanged — 이미지 행 쓰기에 `CanEditMap()` 관문(투명도 경로와 같은 규칙) | 통과 |
| LAY-D2 | LayerPanelScenarioTests::should_not_offer_delete_confirm_when_selected_symbol_is_then_locked_from_layer_panel | Assert.Empty() Failure: Collection was not empty | ① OnSymbolLockChanged · OnLayerLockChanged: 고른 채 잠그면 ClearAllSelections ② ExecuteDeleteSelected 백스톱: 잠긴 선택이면 확인 창 없이 「잠긴 항목은 삭제할 수 없습니다.」 | 통과 |
| LAY-D3 | LayerPanelScenarioTests::should_keep_panel_header_inside_map_canvas_when_header_is_dragged_far_outside | 머리(폭 250 × 36)가 지도(1000×700) 밖: left=-2950, top=-1940 | LayerPanelControl.OnMouseMove → ClampHeaderInside(패널 폭 · 머리 36px 이 Canvas 안, 크기 조절 · 팔레트와 같은 규칙) | 통과 |
| AUX-D1 | TrackingPlaybackScenarioTests::should_hide_tracking_markers_when_min_visible_zoom_is_above_current_zoom | 최소 줌 16 > 지금 줌 12 인데 추적 마커 1개가 보인다(MinVisibleZoom 미적용) | TrackingOverlayManager: ZoomLadder.IsVisibleAtEffectiveZoom(실효줌, MinVisibleZoom) 로 마커 · 꼬리 Visibility — 새 항목 · 갱신 · 줌 변경 · 스윕 틱(판정 바뀐 때)에서 적용(도움말 "바로 반영") | 통과 |
| AUX-D2 | MapModeScenarioTests::should_notify_user_and_keep_panel_open_when_tts_send_fails | TTS 전송 실패인데 사용자 안내(OpenInfoPopupMessageModel)가 하나도 나가지 않았다 | OnTtsSendRequested catch → ShowBrokerControlInfo("TTS 실행 실패", …) · 패널 유지 | 통과 |
| AUX-D3 | MapModeScenarioTests::should_send_tts_once_when_send_pressed_twice_while_first_send_is_pending | Expected invocation on the mock once, but was 2 times: s => s.PublishTtsAsync(5, "경고") | OnTtsSendRequested 에 `_isBroadcastRequestInFlight` 재진입 차단(음원 실행 · 방송 정지와 공용) | 통과 |
| AUX-D4 | MapModeScenarioTests::should_label_speaker_menu_items_in_korean_when_speaker_symbol_right_clicked | 스피커 메뉴 셋째 항목이 한국어가 아니다: "Stop" | 머리글 "방송 정지"(상세 창 행동 BroadcastStop 과 같은 이름) | 통과 |

| EDT2-D1(이번 세션 새로 찾음, MAP-117) | SnapGridAndLabelTests::should_store_title_width_clamped_to_40_800_when_out_of_range_value_is_typed_in_panel | 「제목폭」 10 입력 → DB 제목폭 10 (기대: 40..800 으로 묶임) | GMapPropertyBaseControl.OnTitleMaxWidthPxChanged: Math.Clamp(40, 800) 로 저장 + 칸에 묶은 값 되돌림(재진입 막음) — 결정 D6 | 통과 |

## 의심 4건 — 판정 · 빨강→초록 (새 판정 시험을 먼저 쓰고, 제품 수정을 잠시 끈 상태로 빨강을 받은 뒤 수정)
| 의심 | 판정 근거 | 시험(새로 씀) | 수정 전(빨강) | 수정 | 수정 뒤 |
|---|---|---|---|---|---|
| EDT-S1(MAP-107 · 138) 편집 모드 Esc 가 선택을 안 풂 | 결함. 카탈로그 기대 「편집 모드 Esc → 선택 풀림 · 속성창 닫힘」, 지도 "?" 도움말 전부 "{Esc} = 취소", 다른 모드(조준 · 배치 · 홈 · 영역)는 Esc 로 취소. 코드 · PRD 어디에도 "Esc 는 선택 유지" 의도 기록 없음(GMapCustomControl.OnKeyDown 의 Esc 는 끌기 편집 취소만) | EscapeAndLockedDeleteTests::should_release_single_and_group_selection_and_close_property_panel_when_escape_is_pressed_on_map_in_edit_mode · ::should_select_every_symbol_with_ctrl_a_then_release_all_with_escape · (대조) ::should_keep_selection_when_escape_comes_from_outside_map_or_edit_mode_is_off | 둘 다 `Assert.True() Failure`(e.Handled — Esc 를 아무도 처리 안 함, :40 · :87) | MapViewModel.OnMapPreviewKeyDownForAim 마지막 분기 TryReleaseSelectionOnEscape — 편집 모드 ∧ 지도 안에서 난 Esc ∧ 글 칸 포커스 아님 ∧ 선택 있음 ∧ 끌기 편집 중 아님 → ClearAllSelections + 「선택 해제」 | 통과 |
| EDT-S2(MAP-095) 팔레트 Esc 취소 안내 없음 | 결함(경미). 지도 Esc 경로는 「배치를 취소했습니다.」, 배치 띠도 "(ESC = 취소)" 라 안내하는데 포커스가 팔레트에 남으면 띠만 조용히 사라짐 | EscapeAndLockedDeleteTests::should_say_placement_cancelled_without_row_when_escape_is_pressed_in_palette_list | `Assert.True() Failure`(IsAimStatusVisible, :123) | 새 공개 메서드 MapViewModel.EscapePalettePlacement()(배치 중일 때만 안내) — SymbolPaletteView.ItemKeyDown Esc 가 부름. 끌어 놓기 뒤 정리 · 다른 도구 전환은 기존 CancelPalettePlacement 그대로(안내 없음) | 통과 |
| EDT-S3(MAP-136) 「잠금 N개 제외」 안 나옴 | 결함(경미). 완료 글 분기(:1443)가 잠금 제외 수를 말하려 했으나 확인 전에 미리 빼서 도달 불가 → 3개 골랐는데 "2개" 라고만 묻고 까닭 없음. 그룹 이동은 「잠금 N개 제외」를 말함 | EscapeAndLockedDeleteTests::should_tell_locked_exclusion_in_confirm_and_result_when_group_with_locked_member_is_deleted | `Assert.Contains() Failure: Sub-string not found` String: "선택한 2개 심볼을 삭제하시겠습니까?" | ExecuteGroupDelete: 잠긴 수를 확인 글에 "\n(잠금 N개 제외)" · `_pendingGroupDeleteLockedSkipped` 로 완료 글 「N개 삭제(잠금 M개 제외)」 | 통과 |
| MAP-066 끌기 손잡이 꺼짐 사유 툴팁 없음 | 결함. 같은 행 자물쇠 단추는 꺼지면 「편집 권한이 없습니다」(ShowOnDisabled), 손잡이는 꺼지기만 함 — 카탈로그 기대 "꺼짐 + 사유" | LayerHandleReasonTests::should_show_no_permission_reason_on_disabled_drag_handle_when_map_is_not_editable · (대조) ::should_keep_keyboard_fallback_tooltip_on_enabled_drag_handle_when_map_is_editable | `Assert.Equal() Failure` Expected: 편집 권한이 없습니다 / Actual: 순서 바꾸기 (Alt+↑ / Alt+↓) | LayerPanelStyle.xaml 손잡이: ShowOnDisabled + 스타일 트리거(IsEnabled=False → 「편집 권한이 없습니다」) | 통과 |

## 결정
- D1: 시작 시 worktree 는 v2.6 최신(674bf321)에 787e 의 시험 4 프로젝트가 이미 있다 → 그대로 이어 쓴다.
- D2: 미작성 항목은 작업자 2명에게 프로젝트별로 나눈다(같은 프로젝트 동시 빌드 = obj 충돌). 작업자는 `--no-dependencies` 빌드만, 제품 코드 수정 금지 — 결함은 증거로 보고받아 제품 수정은 본 세션 한 곳에서(787e D5 와 같음).
- D3: 카탈로그 §1 훑기 — 787e 결과표 · 기존 시험 칸 · 이번 목록을 맞대어 보니 "헤디드 남김"도 시험도 아닌 줄이 MAP-117(제목폭) · MAP-118(라벨 위치) 두 개 더 있었다 → 시험 대상에 넣음.
- D4: §3.5 "라이브러리 쪽" 범위는 787e 정의(추적 설정 · 재생 · 방송/TTS · 측정 · 그리기 · 팔레트 · 지도 "?" + 지도 단축키 AUX-008/027)를 따른다.
  AUX-001~007(WINDY) · 009~011(통합웹)은 호스트 코드(이 저장소에 WindyPanelViewModel · LeftMenuSectionViewModel 없음) → 범위 밖.
  AUX-023~026(말풍선 규칙 · F1) · 028(대화상자 키보드 이동)은 지도가 아닌 커널이고 라이브러리 기존 시험이 이미 덮는다
  (Utils.Tests HelpTipTests: Esc · 바깥 클릭 · 하나씩 · F1 섹션/창 루트 · DialogPlacementTests::should_move_with_arrows_when_the_handle_has_keyboard_focus) → 범위 밖(기존 시험).
  AUX-029~035(갤러리 · 레이아웃 스윕 · 죽은 화면 · 종료 게이트)는 호스트 헤디드 도구 → 범위 밖.
- D5: MAP-128 「닫힌 경로 · 화살표」 속성 칸 부재는 의도로 판정 — Themes/LinePropertyStyle.xaml 139~169 「라인 옵션」 칸 전체가 Line/Area 심볼을 처음 넣은 커밋 b5a1dc2a 부터 주석,
  닫힘은 팔레트 종류로 정한다(Area=닫힘 · Line=열림, MapViewModel.cs 5858~5870). 마커 값(IsClosed · 화살표)은 그대로 동작함을 시험으로 확인. 기능을 살릴지는 사용자 판단(카탈로그 문구 갱신 필요).
- D6: EDT2-D1(MAP-117) 결함으로 판정·수정 — 속성창 「제목폭」 입력은 묶지 않고 저장(GMapPropertyBaseControl.cs OnTitleMaxWidthPxChanged), 라벨 끝 끌기는 40~800 으로 묶음(LabelAdorner.cs:362),
  칸 툴팁 · 도움말 · FR-13 은 "40~800" → 입력도 묶고 칸에 묶은 값을 되돌려 보이게 함.

## 발견
- sln 검사(`dotnet test Ironwall.Dotnet.Libraries.sln`)는 시험 어셈블리 10개만 돈다(SystemResources 22 · Watchdog 4 · Models 163 · Sso 91 · Accounts.Api 246 · Messages 421 · Reports.Ui 341 · Utils.Tests 818 · CameraPopup.Host.Tests 161 · CameraPopup.Tests 266) — tests/GMaps.* 는 sln 밖이라 따로 돌린다.
- 중간 회귀(제품 수정 11건 반영 뒤, 작업자 시험 추가 전): Housing 292/292(MapLocateFitTests 제외) · PropertyPanel 249/249(ComponentCardScreenSizeTests 제외) · GMaps.Ui.Tests 757/757 · MapNav 29/29 · sln 10개 어셈블리 실패 0.

- 창 없는 시험 함정(작업자 보고): 생성자 없이 만든 VM 은 Caliburn IsNotifying 이 꺼져 바인딩이 변경을 못 받음 · 창 없는 지도는 판 템플릿이 없어 심볼이 시각 트리에 안 들어감 · Loaded 가 안 와서 직접 보냄 ·
  마우스 위치는 늘 (0,0) · 창 없는 지도는 Zoom 을 바꿔도 줌 변경 통지가 안 옴 · MapViewModel 선 그리기 처리기가 Application.Current.Dispatcher 를 직접 써서 그 시험 동안만 Application 을 넣었다 뺌(DrawingPaletteScenarioTests 의 AppScope).
- 결함 증명 시험의 빨강 출처: 787e 7건 · 의심 4건은 이 세션에서 직접 받음, EDT2-D1 은 MapEdit 작업자가 수정 전 코드에서 받음.

## 남은 일
없음. 헤디드 남김 항목(결과표)은 호스트 헤디드 회차 몫. 사용자 판단 거리: MAP-128 「라인 옵션」(닫힌 경로 · 화살표) 칸을 살릴지(결정 D5).

## 항목 id 결과표(최종 — 787e 결과표를 이어받아 갱신)
상태 약호: 통과 · 결함→수정(빨강→초록) · 헤디드 남김 · 범위 밖(기존 시험) · 범위 밖. 시험 이름은 787e 결과표(첨부 map-plan-787e.md)에 있는 것은 반복하지 않고 바뀐 것만 적는다.

### §1 지도
| id | 상태 | 비고 / 시험 |
|---|---|---|
| MAP-001 | 범위 밖(기존 시험) | GMapScenarioTests(호스트) |
| MAP-002 · 003 · 004 · 005 · 008 · 009 | 통과 | NAV(787e) |
| MAP-006 · 007 · 010 · 012 · 013 | 범위 밖(기존 시험) | 호스트 헤디드 |
| MAP-011 | 통과(열기 요청까지) · 「지도 카드」 선택은 헤디드 남김 | NAV(787e) |
| MAP-014 | 헤디드 남김(실제 끌기 팬/러버밴드) | |
| MAP-015 ~ 020 | 통과 | NAV(787e) |
| MAP-021 ~ 023 · 029 | 범위 밖(기존 시험) | TiltRuntimeDiagTests(호스트) |
| MAP-024 ~ 028 · 030 ~ 038 | 통과 | NAV(787e) |
| MAP-039 · 040 | 헤디드 남김(나침반 끌기 · 타일 공급자) | |
| MAP-041 · 042 · 044 · 047 · 049 · 051 · 053 · 054 · 056 · 061 · 062 · 079 | 범위 밖(기존 시험) | 호스트 헤디드 · AppSweepTests 등 |
| MAP-043 · 045 · 046 · 048 · 050 | 통과 | LAYER(787e) |
| MAP-052 · 055 · 057 · 058 · 067 · 080 | 범위 밖(기존 시험 — 787e 가 표지만 담) | |
| MAP-059 · 063 · 064 · 065 · 068 ~ 073 · 077 · 081 | 통과 | LAYER(787e) |
| MAP-060 | 결함→수정(LAY-D2) | LayerPanelScenarioTests 2건 + LockWhileSelectedTests::should_refuse_delete_with_reason_and_allow_it_again_after_unlock_when_locked_symbol_stays_selected |
| MAP-066 | 결함→수정(LAY-D1 · 손잡이 사유 툴팁) | LayerPanelScenarioTests · LayerHandleReasonTests 통과 |
| MAP-069 | 통과 + LAY-D2 이미지 갈래 | LockWhileSelectedTests::should_release_selection_and_offer_no_delete_when_selected_overlay_image_is_then_locked_from_layer_panel |
| MAP-074 ~ 076 | 헤디드 남김(Win32 파일창 · 생성 대화상자) | |
| MAP-078 | 결함→수정(LAY-D3) | + LayerPanelDragClampTests 2건(지도 안 보통 끌기는 그대로 · 오른쪽 아래 끝 클램프) |
| MAP-082 · 084 | 통과 | AUX(787e) |
| MAP-083 | 결함→수정(AUX-D1) | + TrackingMinZoomScenarioTests 2건(패널 값만 바꿔도 다음 스윕 · 줌 변경 즉시 재판정) |
| MAP-085 · 086 · 088 · 091 · 093 · 094 · 109 · 111 ~ 116 · 119 · 122 · 123 · 126 · 127 · 129 · 134 · 137 · 139 | 범위 밖(기존 시험) | 호스트 헤디드 + 표지 |
| MAP-104 · 105 · 106 | 범위 밖(기존 시험 — SymbolCopyLabelStylePersistenceTests 표지) | |
| MAP-087 · 090 · 092 · 097 · 098 · 099 ~ 103 · 108 · 120 · 121 · 125 · 130 · 131 · 133 · 135 | 통과 | EDIT(787e) |
| MAP-089 | 통과(팝업 실제 표시 · 격자선 그림은 헤디드) | SnapGridAndLabelTests::should_open_grid_settings_and_snap_moved_symbol_to_grid_and_persist_snapped_coords_when_grid_is_on |
| MAP-095 | 결함→수정(EDT-S2) | EscapeAndLockedDeleteTests::should_say_placement_cancelled_without_row_when_escape_is_pressed_in_palette_list |
| MAP-096 · 110(끌기) | 헤디드 남김(실제 머리 끌기) | |
| MAP-107 | 결함→수정(EDT-S1) | EscapeAndLockedDeleteTests 2건 |
| MAP-117 | 통과 + 결함→수정(EDT2-D1) | SnapGridAndLabelTests::should_ellipsize_label_at_title_width_clamp_40_to_800_and_persist_width_from_panel_and_edge_drag · ::should_store_title_width_clamped_to_40_800_when_out_of_range_value_is_typed_in_panel |
| MAP-118 | 통과(실제 마우스 끌기 · 리더선 그림은 헤디드) | SnapGridAndLabelTests::should_persist_label_offset_x_y_to_db_when_label_is_dragged_in_edit_mode |
| MAP-124 | 통과(실제 서버 PATCH 는 헤디드 — VM 경계 IDeviceLocationGateway 가짜) | DeviceLinkAndLineTests::should_link_device_from_combo_then_apply_current_location_to_server_and_show_toast |
| MAP-128 | 통과(패턴 · 투명도 · 포인트 수 · 거리 글 · DB · 닫힌 선 그림 · 화살촉) · 「닫힌 경로 · 화살표」 칸은 의도로 판정(결정 D5) | DeviceLinkAndLineTests::should_reshape_line_and_persist_pattern_opacity_closed_path_and_show_point_count_and_distance · (설계 고정, 표지 없음) ::should_not_offer_closed_path_or_arrow_checkboxes_in_line_property_panel_by_design |
| MAP-132 | 헤디드 남김(GATE_DOOR_SET 현장 장비) | |
| MAP-136 | 결함→수정(EDT-S3) | EscapeAndLockedDeleteTests::should_tell_locked_exclusion_in_confirm_and_result_when_group_with_locked_member_is_deleted |
| MAP-138 | 결함→수정(EDT-S1) | EscapeAndLockedDeleteTests::should_select_every_symbol_with_ctrl_a_then_release_all_with_escape |
| MAP-140 ~ 150 · 153 · 154 · 160 ~ 163 · 165 · 166 · 168 · 173 · 177 · 178 · 179 · 183 ~ 187 · 190 · 194 · 195 | 범위 밖(기존 시험) | 호스트 헤디드 · DrawingKeyRouterTests · MapOverlayPanelThemeLayoutTests 등 |
| MAP-151 | 통과(호스트가 실제 창을 여는 부분은 헤디드) | ComponentCardDetailScenarioTests::should_request_device_console_and_wiring_then_close_card_when_component_card_buttons_pressed · ::should_hide_wiring_for_non_sensor_and_hide_both_links_when_navigator_is_not_registered |
| MAP-152 | 통과 | ComponentCardDetailScenarioTests::should_compute_title_door_and_component_lines_in_tooltip_when_hovering_pids_symbol · ::should_show_title_only_tooltip_when_symbol_has_no_door_and_no_component_axes |
| MAP-155 | 통과(요청 메시지에 부대 id 까지) · 부대 창 레일 선택은 호스트 받는 쪽이라 헤디드 남김 | MarkerMenuScenarioTests::should_publish_open_unit_console_request_with_that_unit_id_when_unit_line_menu_item_clicked · ::should_disable_or_hide_unit_line_and_publish_nothing_when_unit_unknown_or_edit_mode |
| MAP-156 | 통과(요청 · 이력 VM 읽기 전용 · 표 IsReadOnly) · 실제 창 글은 헤디드 | MarkerMenuScenarioTests::should_publish_detection_history_request_and_open_read_only_history_view_model_when_sensor_history_menu_clicked |
| MAP-164 | 통과 | ComponentCardDetailScenarioTests::should_list_rows_in_declared_order_and_reorder_fault_first_when_sort_toggled_in_detail_components_tab · ::should_bind_sort_toggle_and_row_list_automation_ids_in_detail_components_template |
| MAP-169 | 통과(행 「센서 탐지 이력」 · ✕/[닫기]) · 머리 끌기는 헤디드 남김(실제 마우스 캡처) | MarkerMenuScenarioTests::should_request_sensor_history_from_row_menu_and_close_overlay_when_sensor_info_rows_used |
| MAP-175 | 통과 | DrawingPaletteScenarioTests::should_create_open_line_symbol_and_insert_db_row_when_palette_line_button_points_then_hud_complete · ::should_not_write_db_when_line_completed_with_single_point |
| MAP-176 | 통과 | DrawingPaletteScenarioTests::should_create_closed_area_symbol_and_insert_db_row_when_palette_boundary_button_polygon_then_hud_complete |
| MAP-180 | 통과 | DrawingPaletteScenarioTests::should_open_register_panel_cancel_without_db_then_register_own_military_symbol_with_db_row |
| MAP-188 | 통과(알약 실제 마우스 끌기만 헤디드) | CameraPopupHubScenarioTests::should_count_two_then_one_then_zero_and_hide_hub_when_one_closed_then_close_all_confirmed · ::should_save_dragged_hub_position_and_restore_it_on_next_map_view |
| MAP-157 · 191 · 196 | 헤디드 남김(외부 브라우저 · 실창 클램프 · 좁은 실창) | |
| MAP-158 · 159 · 167 · 170 · 171 · 172 · 174 · 181 · 182 · 189 · 192 · 193 | 통과 | AUX/NAV(787e) |

### §3.5 (라이브러리 쪽 — 결정 D4)
| id | 상태 | 비고 |
|---|---|---|
| AUX-008 · 027 | 통과 | NAV(787e) |
| AUX-012 · 017 · 021 | 헤디드 남김 | |
| AUX-013 · 014 · 015 · 016 · 020 | 통과 | AUX(787e) |
| AUX-018 | 범위 밖(기존 시험) | MapsHelpTests |
| AUX-019 | 결함→수정(AUX-D4) | |
| AUX-022 | 결함→수정(AUX-D2 · D3) | |
| AUX-023 ~ 026 · 028 | 범위 밖(지도 아님 · 라이브러리 기존 시험) | 결정 D4 |
| AUX-001 ~ 007 · 009 ~ 011 · 029 ~ 035 | 범위 밖(호스트) | 결정 D4 |
