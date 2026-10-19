# REPORT — 머지 전 마지막 점검 결과

각 항목은 먼저 재현 시험을 만들어 **수정 전 실패**를 확인한 뒤 고쳤습니다. 결함이 아닌 것으로 판정된 항목은 없습니다.

## 결과 표

| # | 항목 | 판정 | 재현 시험(수정 전 실패) | 수정 |
|---|---|---|---|---|
| A1 | 이미지 자르기 Undo/Redo 가 저장 실패를 숨김 | 결함 · 수정 | MapEdit `ImageCropEdgeScenarioTests.should_keep_crop_on_undo_stack_and_screen_equal_db_when_undo_or_redo_save_fails` — "DB 저장 실패인데 Undo 가 성공으로 처리됐다" | `ApplyImageCropStateAsync`: 저장 실패 시 화면 · 모델을 DB 상태로 두고 상태 줄 안내 후 예외 → UndoService 가 칸을 스택에 남김(BUG-04 관용구). 그림이 없으면 기존처럼 넘어감 |
| A2 | 묶음 회전 미확정 중 Ctrl+Z/Y 순서 꼬임 | 결함 · 수정 | MapEdit `GroupRotationEdgeScenarioTests.should_undo_pending_rotation_not_earlier_edit_…`(기대 6/실제 5) · `should_confirm_preview_and_drop_old_redo_…`(기대 2/실제 1) | `OnGroupRotateCompleted` 가 저장 Task(`_groupRotateSaveTask`)를 남기고, Undo/Redo 명령은 확정 뒤 그 Task 를 기다린 다음 되돌림 |
| A9 | 회전 확정 처리 예외가 디스패처로 샘 | 결함 · 수정 | MapEdit `should_log_and_not_crash_dispatcher_when_recording_rotation_undo_throws` — InvalidOperationException 이 STA 디스패처로 샘 | 확정 처리 전체 try/catch + Error 로그 |
| A3 | 직접 그린 건물 Undo 가 옛 job id 를 잡음 | 결함 · 수정 | MapEdit `UserBuildingScenarioTests.should_undo_and_redo_new_building_in_same_area_after_rebake_replaced_old_job`(옛 job 으로 DELETE → 404 를 "이미 없음"으로 받아 조용히 실패) · `should_undo_edit_of_saved_building_after_rebake_without_repeating_failure`(옛 job PUT 404 → 같은 실패 반복) | `UserBuildingCommand` 에 job 찾기 함수를 받는 생성자 오버로드(기존 그대로) → 적용 때마다 `ResolveUserBuildingJob`: 옛 job 이 목록에 있으면 그대로, 없으면 건물 가운데를 덮는 지금 영역(가장 새것). map-3d 수정 불필요 |
| A4 | 건물 그리기 중 전역 키 · 확인 팝업 Enter | 결함 · 수정(두 방향 모두) | MapEdit `should_leave_delete_and_arrow_keys_to_drawing_while_symbol_stays_selected`(Delete 가 선택 심볼 삭제로) · `should_give_escape_to_drawing_not_selection_release_while_symbol_selected`(Esc 가 선택 해제로) · `should_leave_enter_and_delete_to_confirm_popup_shown_over_drawing`(확인 창 Enter 로 저장) + 회귀 방지 1건 | `IsUserBuildingKey`(IsImageCropPanelKey 관용구: 그리는 중 + 지도 화면(MapView) · 판에서 온 키) → 지도 키 처리기 · 조준 Esc 처리기는 넘기고, 그리기 처리기는 이 키만 받음. 그리기 시작 · 지도 누름 때 지도로 초점. Ctrl+Z 는 실제 키보드 상태를 읽어 헤드리스로 넣지 못함(같은 앞단 가드 — 코드로 확인) |
| A5 | SwitchToHwnd ↔ 옛 LoadAsync 경쟁 | 결함 · 수정 | MapEdit `Map3DRaceCleanupScenarioTests.should_not_fail_new_load_when_discarded_control_core_fails_after_switch_to_hwnd`(불러오기 · 미리 데우기) — 버린 컨트롤 실패가 LoadFailed · _failed | LoadAsync · PrewarmAsync 가 시작 때 컨트롤을 잡아 두고 바뀌었으면 실패를 알리지 않음. (실제 WebView2 기동은 금지라 SwitchToHwnd 가 남기는 상태를 필드로 재현) |
| A6 | 지도 VM 정리에 3D 해제 없음 | 결함 · 수정 | MapEdit `should_leave_3d_and_release_subscriptions_and_timers_when_map_view_deactivates`(비활성화 뒤에도 "3D") · `should_dispose_client_and_be_repeatable_when_map3d_cleanup_closes` | `CleanupMap3D(close)` 신설 → OnDeactivateAsync 에서 격리 호출: 3D 접기 · 타이머 · Rendering · Map3DSaved · LoginSucceeded · 지도 구독 해제, close 때만 굽기 조회 끊기 + 클라이언트 Dispose. Leave3D 의 Rendering 해제를 MainMap==null 반환 앞으로 |
| A7 | 영역 목록 응답 순서 뒤바뀜 | 결함 · 수정 | MapEdit `should_keep_new_area_when_older_area_list_answer_arrives_after_newer_one` — 새 영역이 list-missing 으로 내려감 | 요청 순번 · 단 순번 — 오래된 응답 버림 |
| A8 | 높이 판 저장 중 취소해도 PUT 계속 | 결함 · 수정 | MapEdit `should_block_cancel_close_and_escape_while_building_height_is_saving` — 판은 닫혔는데 저장 반영 | 저장 중 [취소] · ✕ · Esc 막음(단추 흐림 + "저장하는 중… 끝날 때까지 닫을 수 없습니다") |
| A13 | 굽기 시작 10 초 제한 → 중복 굽기 | 결함 · 수정 | Ui `Map3DClientBakeUploadTests.should_get_job_when_bake_start_upload_takes_longer_than_ten_seconds`("응답 시간 초과") · MapEdit `should_not_send_same_area_bake_again_…`(POST 2회) · `should_send_again_when_previous_start_timed_out_but_server_has_nothing`(틀린 "꺼져 있습니다") | 요청마다 시간 제한(보통 10 초, 굽기 시작 3 분 `BakeStartTimeout`), `IsTimeout`, `ListJobsAsync`. 시작 답이 없으면 그 영역을 기억 → 다시 누르면 서버 작업 목록으로 "이미 끝남" / "아직 하는 중" 이면 막음 |
| A10 | map-3d set_height 뒤 GLB 실패 시 파일 어긋남 · levels 옛 값 | 결함 · 수정 | pytest `should_leave_user_file_scene_and_glb_unchanged_when_glb_rewrite_fails_*` · `should_recount_levels_from_height_when_floors_omitted` | 장면 파일 먼저(스냅숏 · 실패 시 되돌림) → 저장 파일 맨 끝, 층수 없으면 levels 를 높이로 다시 셈 |
| A11 | editable() 이 error/cancelled 통과 → 500 · /api/scenes/at 매번 전부 읽음 | 결함 · 수정 | pytest `should_answer_404_when_job_has_no_scene_after_error_or_cancel` · `should_not_reread_unchanged_scenes_and_refresh_on_new_changed_deleted_or_finished` | scene.json 없으면 404, 장면 줄 캐시(scene.json mtime_ns · 크기) |
| A12 | 깨진 저장 파일 → 빈 목록 → 덮어씀 | 결함 · 수정 | pytest `should_keep_corrupt_store_files_and_refuse_writes` | 깨진 파일 보존 · 쓰기 거절(500 + 파일 이름), 읽기 · 굽기는 건너뛰고 경고 |
| B1 | LaMa(및 SAM) 첫 로드 중 굽기 취소 안 먹음 | 결함 · 수정(SAM 도 같았음) | pytest `should_cancel_quickly_while_lama_session_loads` · `…_sam_sessions_load` | 세션 생성을 기존 `cancel.run`(별도 스레드 + 0.2 초 취소 확인)으로, 끝난 로드는 캐시에 |
| B2 | 원형+돔 · 원뿔 입력 높이 = 꼭대기 | 수정(12 → 14.4 m 재현) | pytest `should_put_round_roof_top_at_input_height_only_for_drawn_buildings[dome/cone]` · `should_keep_round_roof_top_at_new_height_when_3d_panel_changes_it` | 직접 그린 건물만 박공과 같은 기준(벽 = H − 솟음). 사진 · 지도 탱크 규칙은 그대로 |
| B3 | 박공 불가 윤곽 → 조용히 평지붕 · roof_shape 어긋남 | 결함 · 수정(map-3d + GIS) | pytest `should_record_built_roof_shape_when_chosen_roof_cannot_be_built`(ㄱ자인데 roof_shape="gable") · MapEdit `should_say_flat_only_when_gable_is_chosen_for_l_shaped_outline` · Ui `Map3DUserBuildingTests` 4개(사례 8) | map-3d: scene.json roof_shape = 실제 지은 모양 + `roof_shape_requested`, 목록에 `roof_built`. GIS: `Map3DUserBuilding.CanGable · CanRoundRoof · CanBuildRoof`(map-3d 와 같은 판정) → 판 지붕 줄 밑 경고색 "이 모양은 평지붕만 됩니다", "?" 도움말에 규칙 한 줄. 원 도구 돔 자동 제안도 둥근 판정이 될 때만 |

### 박공 · 둥근 지붕 판정(map-3d `map3d/buildings.py` build_building 385~392 · is_round 146~149 와 같음)
- 윤곽을 미터로(경도 × 111320 · cos(위도), 위도 × 110540) → 감싸는 가장 작은 돌린 직사각형(rr).
- 박공(모임 · 외쪽도 같음) = rr 넓이 > 4 m² 이고 윤곽 넓이 ÷ rr 넓이 ≥ 0.85 이고 구멍 없음. ㄱ자는 보통 0.56 으로 걸림.
- 원뿔 · 돔 = 꼭짓점 16개 이상 이고 4π·넓이 ÷ 둘레² ≥ 0.9(원 도구 32각 통과).
- g2 가 map-3d 의 shapely 로 같은 모양을 직접 계산해 GIS 판정과 일치 확인(네모 1.00 · ㄱ자 0.556 · 원 32각 0.788 · 홈 7×4 0.86 / 8×4 0.84 · 3.8 m² 상자 거절). 0.85 경계 ±0.001 에서는 근사 차이로 드물게 다를 수 있음.

## 그 밖의 수정
- `Utils.Tests/Perf/PerfSamplerTests` — 기존 시험 코드의 경쟁(부하 중 테스트 호스트 전체 중단)을 판정 변경 없이 바로잡음(DECISIONS 2).

## 남긴 것(보고만)
- 묶음 회전 미리보기 중 Ctrl+Z 를 눌렀는데 확정 저장이 전 멤버에서 실패하면, 이어지는 Undo 가 그 앞 편집을 되돌림(수정 전과 같음 · 별도 동작 결정).
- 다른 회전 확정 지점(잠금 · 묶음 끌기 시작 · 선택 바뀜)은 저장 끝을 기다리지 않음(Undo 순서 문제는 Undo/Redo 경로에서만 생김).
- A13 의 중복 판정은 map-3d 작업 목록에 영역 정보가 없어 "만드는 중인 작업이 있다"로 막음 — map-3d 가 running 항목에 bbox 를 넣거나 POST /api/jobs 멱등 키를 받으면 정확해짐.

## 시험(최종 재검증 — 모두 이 작업 트리에서, logs/r2-* · r3-sln)
| 대상 | 결과 | 기준선(작업 전) |
|---|---|---|
| `dotnet test Ironwall.Dotnet.Libraries.sln` | EXIT 0 — 실패 0(Utils 886 · CameraPopup 334 · Host 197 · Messages 496 · Reports.Ui 365 · Accounts.Api 259 · Models 208 · Sso 106 · SystemResources 22 · Watchdog 15) | r0: PerfSamplerTests 경쟁으로 테스트 호스트 중단 |
| tests/GMaps.Ui.Tests | 1691/1691 통과 | 1675 |
| tests/GMaps.MapEdit.Tests | 766/766 통과 | 747 |
| tests/GMaps.MapNav.Tests | 67/67 통과 | 67 |
| tests/GMaps.PropertyPanel.Tests | 436/436 통과 | 436 |
| tests/GMaps.MapLayer.Tests | 99/99 통과 | 99 |
| map-3d pytest(복사본에 패치 적용 상태) | 313 passed | 300 |
| map-3d node tests/*.mjs | 15 ok(extract_view_check.mjs 는 인자 필요 — pytest 가 인자를 넣어 돌림, 기준선과 같음) | 15 |
| map3d.patch → 원본 `git apply --check` | exit 0(원본 6102407, 적용은 안 함) | |

### 재검증 중 만난 흔들림(이번 변경과 무관 — 근거)
- 직전 하네스 검사 7건 실패(Utils.Tests HelpContractTests 등): 복사본이 `artifacts/gN` 에 있어 소스 스캔이 같은 파일을 두 번 읽음 → 복사본을 `artifacts/obj/` 로 옮겨 해소, 지금은 복사본 삭제(DECISIONS 1 · 3).
- r1 MapEdit `Map3DNavComboScenarioTests` 키 시험 4건: 0.7 초 안에 몰려 실패, 처리기(`Map3DNavControl.xaml.cs:99`)가 실제 키보드 `Keyboard.Modifiers` 를 읽음, 코드 그대로 단독 2회 8/8 · 최종 766/766 통과.
- r2 sln CameraPopup.Tests 7건(호스트 재시작 "3 초 안" 시간 기준): 같은 시간에 map-3d pytest · node 를 함께 돌린 부하. CameraPopup 변경 0줄, r1 · r3 에서 334/334 통과.

## 정리
- GIS 복사본 `artifacts/obj/g1~g3` · `artifacts/base` · map-3d clone `artifacts/map3d` 삭제(작업 트리 안). 패치 · 보고는 `.harness/t-20261020-010420-f207/` 에 보관(patches/ · map3d.patch · map3d-agent-report.txt · map3d-before-fix.txt).
- 서버 · 창 · WebView2 · 브라우저는 띄우지 않음. 원본 `C:\workspace_python\map-3d` 는 읽기 · `apply --check` 만.
- map-3d 패치 적용(원본 커밋)은 하지 않음 — 머지 때 `git -C C:\workspace_python\map-3d apply <map3d.patch>` 후 커밋 필요.
