# REPORT — 전수 시험 「결정 필요」 추천 적용(t-20261019-221052-8a7a)

| # | 결정 | 바꾼 곳 | 시험 |
|---|---|---|---|
| 1 | 로그인 직후 3D 안내에서 "ESC = 2D" 제거 — WPF 안 3D 는 「나가기」만, 네이티브 창 3D(hwnd)만 Esc 언급 | `Map3DGuide.StageText("shown")` 글 · 새 `Map3DGuide.ShownText(hostMode)` · `MapViewModel.Map3D.OnMap3DLoginSucceeded` | `Map3DHudTests.should_guide_shown_3d_without_zoom_wording` **기대값 변경** — 옛 기대("ESC" 포함)는 WPF 안 3D 가 Esc 로 안 나가는 실제와 다른 문구를 고정하고 있었다(사용자 결정). 시험 주석에 이유 |
| 2 | 안 쓰는 옛 3D 나가기 규칙 삭제 | `Map3DMath.ShouldLeave3D`(3 오버로드) · `Map3DMath.ExitCamera(bool,…)` · `Map3DGuide.ShouldLeave3DForPanel` 삭제. 라이브러리 · 메인 앱(Dotnet.Monitoring.Solution) grep 호출 0건 확인 | 그것만 시험하던 시험 삭제: Map3DExitTests 6건(home 메시지 시험만 남김) · Map3DMathTests 1건 · Map3DAreaZoomTests 1건 · Map3DGuideTests 1 Theory(6건) · Map3DSettingsTests 안 두 줄(ShouldStay3DAfterMove 는 유지) |
| 3 | 수동 모드 미리 불러오기 — 그대로 | 변경 없음 | 기존 시험 그대로 |
| 4 | 자동 모드 3D 중 편집 · 측정 등을 켰다 끄면 그 자리에서 재진입 안 함(「나가기」와 같은 진입 보류) | `SyncMap3DWithInputModes` 에서 3D 를 접을 때 `_map3DEntrySuppressed = true` | `Map3DEntryExitMoreScenarioTests.should_not_reenter_at_same_place_in_auto_until_gate_is_satisfied_again_when_input_mode_ends` (측정 · 표적 · 심볼 배치 · 영역 그리기 · 편집 모드 5건, 줌을 내렸다 올리면 다시 3D) |
| 5 | 묶음 회전 저장 일부 실패 → 실패 멤버만 원위치 · Undo 는 성공 멤버만 · "n개 저장 실패 — 원위치" | `MapViewModel.OnGroupRotateCompleted` | `GroupRotationScenarioTests.should_restore_only_failed_members_and_undo_only_saved_when_rotation_save_fails` (0 · 1 · 3개 실패) |
| 6 | Ctrl+I 는 편집 모드에서만 — 아니면 "편집 모드에서 쓸 수 있습니다" | `ExecuteLoadImageOverlay` 첫머리(권한 확인 뒤, 파일창 앞). 단추 · Ctrl+I 같은 명령 | `MapRegistrationCommandScenarioTests.should_tell_edit_mode_needed_and_not_open_file_dialog_when_image_load_runs_outside_edit_mode` |
| 7 | 3D 빈 땅 클릭 → 「건물 높이」 판도 닫기(미리보기 취소) | map-3d 가 건물 아닌 땅이면 `{type:"building.none"}` 전송(map3d.patch), GIS `Map3DMath.ParseMessage` · `OnMap3DMessage` 가 판을 닫음 | `BuildingHeightScenarioTests.should_close_panel_and_cancel_preview_when_empty_ground_is_clicked_in_3d` · map-3d `tests/building_check.mjs` |
| 8 | 보고서 6 · 10 · 11 · 12 | 바꾸지 않음 | — |

## 수정 전 실패 → 수정 후 통과(4 · 5 · 6 · 7)
`prove_before.py` 가 제품 파일 4개를 기준 커밋(cdc1238c) 판으로 잠시 되돌려 새 시험을 돌린 뒤 복원했다(`prove_before.log`).
- 수정 전: 4번 5건 모두 실패(다시 3D 로 들어감) · 5번 1 · 3개 실패 경우 실패(실패 멤버가 회전된 채) — 전부 성공 경우는 통과(정상) · 7번 실패(`building.none` 를 버림) · 6번 실패(운영 모드에서 파일 고르기 창이 떠 STA 45 s 시간 초과).
- map-3d: 옛 view.js 로 `node tests/building_check.mjs` → 실패, 새 view.js → `building ok`.
- 수정 후: 아래 전체 시험 통과.

## map-3d
`map3d.patch` — `C:\workspace_python\map-3d` 깨끗한 원본에 `git apply --check` 통과. 작업 사본 `artifacts/map3d`(git 무시)에서 pytest 286 통과 · node 시험 통과. 적용(커밋)은 사람이 한다.
