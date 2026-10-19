# 고친 결함 상세(영역 보고서에서 모음 — 원인 · 수정 · 수정 전 실패 / 수정 후 통과)

## A 3D 진입/이탈

### D-1 거절된 「이 영역 3D 로 보기」가 돌아갈 자리 · 영역 보기 표지를 남김
- 원인: `ViewMap3DArea` 가 `_map3DReturn` · `_map3DAreaViewJob` 을 먼저 잡고 `EnterMap3DDirect` 를 부르는데, 편집 · 그리기 · 만드는 중 · 비행 중으로 거절돼도 지우지 않는다. 다음 진입은 `_map3DReturn ??=`(EnterMap3DDirect) · `Reveal3D` 의 `??=` 로 그 옛 자리를 그대로 쓰고, 자동이면 같은 영역의 게이트 진입도 `_map3DAreaViewJob == scene.Id` 라 옛 자리를 유지한다 (Ironwall.Dotnet.Libraries.GMaps.Ui/ViewModels/Maps/MapViewModel.Map3DAreas.cs:204-215, MapViewModel.Map3D.cs:448 · 764 · 799).
- 수정: `ViewMap3DArea` — 장면이 있어 `EnterMap3DDirect` 를 불렀는데 상태가 그대로 Off(거절)면 `_map3DReturn = null; _map3DAreaViewJob = null;` (MapViewModel.Map3DAreas.cs).
- 증거: 수정 전(수정 줄만 주석 처리해 다시 돌림)
  - `More::should_return_to_button_press_spot_when_area_view_was_refused_before_in_edit_mode` 실패 — "「3D 로 보기」를 누른 자리로 돌아와야 한다: 줌 18 이어야 하는데 17"
  - `More::should_return_to_gate_entry_spot_when_area_view_was_refused_then_edit_mode_ended_in_auto` 실패 — "게이트가 들어온 직전 자리로 돌아와야 한다: 줌 17.5 이어야 하는데 17"
  - `More::should_refuse_area_view_with_reason_and_leave_no_return_spot_when_baking` 실패 — "Assert.Null() Failure: Value of type 'Nullable<Map3DCamera>' has a value … Zoom = 17"
  - 수정 후 세 시험 통과.

### D-3 3D 를 나갈 때 남은 벤더 지도 끌기(_core.IsDragging)를 정리하지 않음
- 원인: `GMapCustomControl.ResetTransientInput` 은 이미지 끌기 · 마퀴 · 선 눌림 · 팬 억제 · 캡처만 비우고 벤더 코어 끌기(`GMapControl._core.IsDragging`, 지도 `IsDragging`)는 비우지 않았다(GMapCustomControl.cs:1025-1034). 3D 가 펼쳐질 때 끌던 중이면 뗌을 3D 화면이 받아 남는다 — 벤더 코어는 끌기 중 정수 줌(Core.Zoom setter `!IsDragging`, Core.cs:107) · 위치 이동(GoToCurrentPosition, Core.cs:168)을 받지 않고, 벤더 휠 줌도 `!_core.IsDragging` 일 때만 한다(GMapControl.cs:1884). `_core` 는 벤더 private 이라 파생 컨트롤에서 볼 수 없었다.
- 수정: 벤더 `GMapControl` 에 seam 두 개 추가(공개 형식 더하기만, 기존 `IsCoreStarted` seam 과 같은 방식) — `public bool IsCoreDragging => _core.IsDragging;` · `public void EndCoreDrag()`(OnMouseUp 의 끌기 끝과 같은 정리: 지도 IsDragging · 커서 · `_core.EndDrag()`). `ResetTransientInput` 에서 `if (IsDragging || IsCoreDragging) { EndCoreDrag(); cleared.Add("coreDrag"); }`, 진단 `InputGateText` 의 "coreDrag" 도 코어 끌기를 함께 본다 (GMap.NET/GMap.NET/GMap.NET.WindowsPresentation/GMapControl.cs, Ironwall.Dotnet.Libraries.GMaps.Ui/GMapCustoms/GMapCustomControl.cs).
- 증거: 수정 전 `More::should_clear_vendor_core_drag_when_exited_after_drag_left_over(controlDraggingToo: False|True)` 둘 다 실패 — "나간 뒤에도 벤더 코어 끌기가 남았다" / 수정 후 통과(정리 경고에 coreDrag · 휠 · 끌기 · 클릭 · 정수 줌 18 을 코어가 받음).

### D-4 만들기 취소 · 실패로 끝나면 게이트를 다시 판정하지 않음
- 원인: 굽는 동안 게이트는 진입을 막는다. 다 만든 경우만 `QueueMap3DGate()` 를 부르고(MapViewModel.Map3D.cs:593), 취소(CancelMap3DBakeAsync → EndMap3DBake) · 실패(job.IsError) · 다른 곳에서 취소(job.IsCancelled) 길은 부르지 않아(MapViewModel.Map3D.cs:506-508 · 575-584 · EndMap3DBake 529-540) 자동 · 영역 안 · 진입 줌 이상이어도 지도를 움직이기 전까지 3D 로 안 들어갔다(「다시 만들기」는 ReapplyMap3DAreas 가 게이트를 불러 우연히 피함).
- 수정: `EndMap3DBake` 끝에 `QueueMap3DGate()`(굽기가 끝나는 모든 길 — 프레임당 1회로 모여 중복 무해) (MapViewModel.Map3D.cs).
- 증거: 수정 전 `More::should_reenter_3d_without_moving_map_when_bake_ends_without_new_scene_inside_area_in_auto(cancel|error|cancelled-elsewhere)` 셋 다 실패 — "만들기가 끝났는데(cancel) 지도를 움직이기 전까지 3D 로 들어가지 않는다"(error · cancelled-elsewhere 같은 문구) / 수정 후 통과.
  - 참고: 처음엔 cancel 변형이 우연히 통과했다 — 영역 표시의 첫 방위 통지(OnMap3DAreaBearingChanged → 게이트 큐)가 취소 직후에 들어왔기 때문. 시험에서 그 통지가 만드는 동안 지나가게 1.2 s 기다린 뒤 다시 돌려 실패를 확인했다.

### D-6 준비 중 다른 영역 「이 영역 3D 로 보기」가 처음 자리를 잃음(새로 찾음)
- 원인: `ViewMap3DArea` 는 3D 표시 중(Shown)일 때만 처음 돌아갈 자리를 이어 받고(`_map3DState == Shown ? _map3DReturn : null`), 준비 중(Pending)이면 `CancelMap3DPending` 이 `_map3DReturn` 을 지운 뒤 지금 2D(앞 「영역 보기」가 옮긴 자리)를 새로 잡았다 (MapViewModel.Map3DAreas.cs:200).
- 수정: `var before = _map3DState != Map3DState.Off ? _map3DReturn : null;` — 준비 중에도 처음 자리를 이어 받는다(게이트로 준비 중이면 `_map3DReturn` 이 null 이라 지금 2D — 그대로) (MapViewModel.Map3DAreas.cs).
- 증거: 수정 전 `More::should_open_other_area_and_return_to_spot_before_first_area_view_when_chosen_while_pending` 실패 — "처음 「영역 보기」를 누르기 전 자리로: 줌 12 이어야 하는데 17.5" / 수정 후 통과.


## B 3D 영역

### B-50 (D1) 다시 만드는 중인 영역을 지우면, 굽기가 끝난 뒤 지운 영역이 새 장면으로 되살아난다
- 원인: `Map3DAreaItem` 메뉴가 `cancelBake != null`(다시 만드는 중)이어도 「지우기…」를 늘 넣었고(Ironwall.Dotnet.Libraries.GMaps.Ui/Services/Map3D/Map3DAreaItem.cs:56), 지우기 확인 `HandleAsync(CallDeleteMap3DAreaProcessMessageModel)`(ViewModels/Maps/MapViewModel.Map3DAreas.cs:307~)가 굽기 상태를 보지 않고 DELETE · `RetireMap3DJob` 했다. 굽기가 끝나면 `BakeMap3DAsync` 가 새 job 을 `_map3DScenes` 맨 앞에 넣고(MapViewModel.Map3D.cs:587) 목록을 새로 받아 같은 자리에 영역이 다시 나타난다(옛 job DELETE 는 404 → 무시).
- 수정(지시문 결정대로): ① `Map3DAreaItem.cs` — 다시 만드는 중이면 「다시 만들기」처럼 「지우기…」도 넣지 않는다(「만들기 취소」가 맨 위). ② `MapViewModel.Map3DAreas.cs` — private `RefuseDeleteWhileRebaking(item)`(굽는 중 && item.Id == `_map3DBakeSourceId`)를 두고 `RequestDeleteMap3DAreaAsync`(확인 창 열기 전) · `HandleAsync`(확인 뒤)에서 막는다 — 상태 줄 "만드는 중에는 지울 수 없습니다 — 「만들기 취소」 뒤 지우세요" + 로그 `[Map3D] 다시 만드는 중 — 영역 지우기 막음 id={id}`. 공개 형식 변경 없음.
- 증거: 수정 전 `Map3DAreaScenarioTests.should_refuse_delete_of_area_being_rebaked_and_allow_after_bake` 실패 — "Assert.DoesNotContain() Failure: Filter matched in collection … Header = 지우기…" / `should_not_open_delete_confirm_when_area_being_rebaked` 실패 — "Assert.DoesNotContain() Failure: Filter matched in collection [OpenConfirmPopupMessageModel { … Title = "3D 영역 지우기" }]". 추가로 `HandleAsync` 막기만 잠깐 끈 채(메뉴 수정은 둔 채) 다시 돌려 "Assert.Equal() Failure: Values differ Expected: 0 Actual: 1"(DELETE /api/jobs/area-x 가 나감)을 확인하고 되돌림. 수정 후 두 시험 통과.

### B-34 「다시 만들기」 판 [취소] 뒤 다시 만들기 표지가 남는다(잠재 상태 누수)
- 원인: `RebakeMap3DArea` 가 `_map3DBakeReplaces` · `_map3DBakeOptionsSourceId` 를 정하는데, 판 [취소](`CancelMap3DBakeOptionsCommand` → `CloseMap3DBakeOptions`, MapViewModel.Map3DBakeOptions.cs:74~78)는 이 둘을 비우지 않는다. 지금은 다음 굽기 경로(TakeMap3DArea · RequestMap3DBake)가 시작 전에 비워 주므로 사용자에게 보이는 결과는 없다(같은 시험 뒷부분 — 새 영역 굽기가 옛 영역을 지우지 않음 — 은 수정 전에도 성립).
- 수정: [취소] 처리기에서 두 표지를 비운다(한 줄, MapViewModel.Map3DBakeOptions.cs). `CloseMap3DBakeOptions` 는 [만들기] 확정 경로도 부르므로(확정 뒤 `BakeMap3DAsync` 가 표지를 읽음) 거기서는 비우지 않았다.
- 증거: 수정 전 `Map3DBakeOptionsScenarioTests.should_post_nothing_and_clear_rebake_marks_when_bake_panel_cancelled` 실패 — "Assert.Null() Failure: Value is not null Expected: null Actual: "x"" / 수정 후 통과.


## C 3D 상호작용

### C-59/C-60(D1) 1층보다 낮은 건물의 −1층 · ↓ 가 높이를 3.0 m 로 올림(짝: 200층 상한을 넘는 건물의 +1층 · ↑ 가 400 m 로 내림)
- 원인: `Map3DBuilding.StepFloors`(Ironwall.Dotnet.Libraries.GMaps.Ui/Utils/Map3DBuilding.cs:101-102 원본) — `FloorsFromHeight` 반올림 · `Clamp(…,1,200)` 뒤 `HeightFromFloors` 를 그대로 돌려줘, 2.0 m(=1층) −1 → 1층 → 3.0 m. 층고 2 m · 500 m(=250층 → 200층) +1 → 200층 → 400 m. `KeyStep`(↑/↓) · `StepBuildingFloorCommand` · 판 키가 모두 이 함수를 쓴다.
- 수정: 내리기는 지금보다 높아지면, 올리기는 지금보다 낮아지면 지금 높이 그대로(`ClampHeight(height)`) 돌려준다(Map3DBuilding.cs). 공개 형식 변화 없음.
- 증거: 수정 전 `Map3DBuildingStepEdgeTests` 13 중 9 실패 — "Assert.Equal() Failure: Values differ / Expected: 2 / Actual: 3", "+1층이 높이를 내렸다: 500 → 400";
  `BuildingHeightEdgeScenarioTests.should_keep_low_building_height_when_minus_floor_button_or_down_key_pressed(2, 1.2)` 실패 — "Expected: 2 / Actual: 3", "Expected: 1.2 / Actual: 3". 수정 후 모두 통과.
- 비고: 지시는 "내리기"만이었으나 같은 함수 · 같은 원인의 반대쪽(+1층이 높이를 깎음)을 같은 한 줄로 막았다(결정 1).

### C-81(D2) 저장 중 3D 를 나가면 늦은 응답이 되돌리기 칸을 다시 쌓아, 다음 3D 진입에서 앞 세션 저장을 되돌릴 수 있음
- 원인: `SaveBuildingHeightAsync` 가 await 뒤 무조건 `BhUndo.Add(undo)`(MapViewModel.BuildingHeight.cs:285 원본). `Leave3D → CloseBuildingHeightPanel(clearUndo:true)` 가 칸을 비운 뒤에 응답이 와도 쌓이고, `_bhJob` 이 그대로라 같은 영역에 다시 들어가 건물을 고르면 [되돌리기] 가 켜진다. `UndoBuildingHeightAsync` 의 실패 시 `BhUndo.Insert` 도 같은 경쟁.
- 수정: 3D 세션 번호 `_bhSession`(int — 생성자 없는 VM 에서도 0) 을 두고 3D 를 나갈 때(clearUndo) 올린다. 저장 · 되돌리기 시작 때 번호를 잡아 두고, 응답 뒤 번호가 바뀌었으면 칸에 쌓지 · 되살리지 않는다(로그 `[Map3D] 건물 높이 저장 응답이 3D 를 나간 뒤 도착 — 되돌리기 칸에 쌓지 않음 id= job=`). 서버 저장 · 3D commit 은 그대로.
- 증거: 수정 전 `should_not_refill_undo_nor_offer_previous_session_undo_when_save_response_arrives_after_leaving_3d` 실패 — "Assert.Equal() Failure: Values differ / Expected: 0 / Actual: 1"(BuildingHeightEdgeScenarioTests.cs:230), `should_not_put_back_undo_step_when_undo_fails_after_leaving_3d` 실패 — "Expected: 0 / Actual: 1". 수정 후 통과.

### C-101(D3) 네이티브 창(hwnd) 3D 에서 건물을 누르면 보이지 않는 판이 열려 첫 Esc 를 가로챔
- 원인: `OnMap3DBuildingPicked` 가 호스트 방식과 상관없이 판을 연다(MapViewModel.BuildingHeight.cs:157-172 원본). hwnd 3D 는 WPF 판을 덮고, `TryHandleMap3DKey` 는 `TryCancelBuildingHeightByEscape` 를 `EscExitsMap3D` 보다 먼저 부른다(MapViewModel.Map3D.cs:403).
- 수정(지시된 선택): `Map3DHost.HostMode == "hwnd"` 이면 판을 열지 않고 3D 의 윤곽을 지우며(`building.select` null) 안내 `네이티브 창 3D 에서는 건물 높이 판을 쓸 수 없습니다`(AimStatus, 자동 숨김) + Info 로그 `[Map3D] 건물 고름 — 네이티브 창 3D 라 「건물 높이」 판을 열지 않음 id= job=`. composition(기본)은 그대로.
- 증거: 수정 전 `should_not_open_panel_and_tell_reason_and_let_esc_exit_when_building_clicked_in_native_window_3d` 실패 — "WPF 판은 네이티브 창 3D 에 가려 보이지 않는다 — 열면 안 된다". 수정 후 통과(첫 Esc 로 2D).

### C-105(D7) 잘못된 입력("abc")이 남은 채 [적용] → 앞 미리보기 값이 저장됨
- 원인: 입력 오류는 `_bhError` 만 세우고 미리보기는 그대로라 `ApplyBuildingHeightCommand.CanExecute`(IsEditable ∧ IsChanged)가 켜진 채(MapViewModel.BuildingHeight.cs:137-138 원본).
- 수정: 입력칸 오류를 따로 표시하는 `_bhInputError`(bool) — 층수 · 높이 · 층고 칸 해석 실패에서 세우고(`SetBuildingHeightInputError`, CanExecute 다시 묻기), 바른 값 · 다른 건물 · 판 닫기 · 새 저장 시도에서 내린다. CanExecute 와 `ApplyBuildingHeightAsync` 둘 다 거절(오류 줄 유지). 서버 실패 이유 줄은 이 표시를 세우지 않아 다시 [적용] 할 수 있다(C-75, 기존 시험 `should_keep_preview_and_show_reason_when_server_refuses_or_is_off` 그대로 통과).
- 증거: 수정 전 `should_refuse_apply_and_keep_error_when_invalid_input_remains` 5 경우 모두 실패 — "잘못된 입력 'abc' 이 남았는데 [적용] 이 켜져 있다"(0 · 3.5 · 600.1 · 층고 11 도 같은 글). 수정 후 통과.


## D 2D 편집

### D-35/D-36 ① 회전 미리보기 중 잠금 → 잠긴 멤버가 계속 돌고, 그룹 잠금이 미리보기 위치를 DB 에 저장 · Esc 는 화면만 되돌림
- 원인: `ExecuteGroupLock`(MapViewModel.cs ~1605)이 열린 회전을 닫지 않고 멤버마다 `DbUpdateProcess`(위치 포함 전체 행) 저장. `OnSymbolLockChanged`/`OnLayerLockChanged`(~10220/~10245)도 회전을 안 닫음. `GroupRotationSession` 은 IsLocked 를 시작 때만 본다(GroupRotationSession.cs:41).
- 수정: 세 처리기 권한 확인 뒤 `EndRotation(commit: true)`(MapViewModel.cs).
- 증거: 수정 전 `GRE::should_confirm_open_rotation_before_group_lock_so_escape_cannot_split_screen_and_db` — "Assert.False() Failure Expected: False Actual: True"(IsRotating), `GRE::should_confirm_open_rotation_and_stop_turning_member_when_it_is_locked_from_layer_panel` 같은 실패 / 수정 후 둘 다 통과.

### D-37 ② 회전 미리보기 중 속성 판 변경 → 미리보기 위치가 확정 없이 DB 에 저장
- 원인: `OnMarkerPropertyChanged`(MapViewModel.cs ~9026) → `ApplyGroupPropertyChangeAsync`/단일 `DbUpdateProcess` 가 열린 회전을 모름.
- 수정: 권한 확인 · 로그 뒤 열린 회전이면 먼저 확정, 그룹 경로는 적용 뒤 판 다시 채움(확정이 판을 회전 값으로 채운 뒤 이번 값까지 보이게).
- 증거: 수정 전 `GRE::should_confirm_open_rotation_before_group_property_change_is_saved` — "Assert.False() Failure Expected: False Actual: True" / 수정 후 통과.

### D-41 ③ 회전 확정 · Undo 뒤 그룹 판 회전 · 감시 방향 칸 낡음
- 원인: 그룹 판은 `EnterGroupMode`(GMapPropertyBaseControl.cs:1063) 때 한 번만 채움.
- 수정: `MapViewModel.GroupRotation.cs` 에 private `RefreshGroupPropertyPanelValues()`(열린 그룹 판이면 `panel.EnterGroupMode(panel.GroupMarkers)` 재사용) → `OnGroupRotateCompleted`(기록 · 어도너 갱신 직후) · Undo/Redo 명령 끝(MapViewModel.UndoContext.cs).
- 증거: 수정 전 `GRE::should_refill_group_panel_bearing_and_view_direction_after_rotation_confirm_and_undo` — "Values are not within 9 decimal places Expected: 20 Actual: 0" / 수정 후 통과.

### D-77 ④ 그룹 속성 저장 실패해도 Undo 기록
- 원인: `ApplyGroupPropertyChangeAsync`(MapViewModel.cs ~9015) catch 뒤에도 `RecordPropertyChange`.
- 수정: 저장 성공 멤버만 기록(단일 경로와 같게). 전원 실패면 빈 배치 = 기록 0.
- 증거: 수정 전 `GPE::should_not_record_undo_for_member_whose_group_property_save_failed` — "Collections differ Expected: [731] Actual: [732, 731]"(Undo 가 실패 멤버를 또 저장) / 수정 후 통과.

### D-63 ⑤ 레이어 패널 → 지도 선택이 줌으로 안 보이는 심볼도 묶음에 넣음
- 원인: `PushLayerSelectionToMap`(MapViewModel.LayerSelection.cs:37)이 `ZoomLadder.IsVisibleAtEffectiveZoom` 을 안 봄.
- 수정: 심볼은 실효 줌에서 보일 때만, 오버레이 이미지 행은 그대로 포함. 패널은 기존처럼 실제 지도 선택을 비춰 풀림.
- 증거: 수정 전 `LPE::should_leave_out_symbols_hidden_by_zoom_and_unmark_their_rows_when_layer_rows_go_to_map` — "Collections differ Expected: [601, 602] Actual: [601, 602, 603]" / 수정 후 통과.

### D-92 ⑥ 같은 장비를 세 마커가 가진 자료에서 재할당이 한 마커만 비움
- 원인: `OtherDeviceHolders(...).FirstOrDefault()` 하나만 대기 · 확인(MapViewModel.DeviceReassign.cs:67 · 104).
- 수정: 확인(`HandleAsync`) 때 `OtherDeviceHolders(target, device)` 전부(+대기 holder) 비움 저장 → B 연결. 중간 실패 시 비운 것까지 원복 저장. Undo 배치에 모든 holder + target(1단위). 팝업 문구 · 기존 실패 순서 그대로.
- 증거: 수정 전 `DRE::should_clear_every_other_holder_and_undo_all_together_when_three_markers_share_device` — "Values differ Expected: 0 Actual: 501"(정문2 LinkedDeviceId) / 수정 후 통과 · 기존 DRS 16건 통과.


## E 오버레이 이미지(GIS)

### D-1 / E-18 자르기 판 안의 Delete · 방향키 · Ctrl+Z/Y 를 지도 창 처리기가 먼저 가로챔
- 원인: 창 수준 터널 `OnMapPreviewKeyDownForGroup` 은 묶음 회전 판만 빼 주고(`_groupRotationPanel.IsAncestorOf`) 자르기 판은 안 뺌 → 판의 PreviewKeyDown 보다 먼저 선택 그림 삭제 확인 · 방향키 이동 · 전역 Undo 가 돈다. (MapViewModel.cs:1254 부근)
- 수정: 같은 자리에 `if (IsImageCropPanelKey(e)) return;` — 포커스 또는 키 원본이 판 안이면 지도 처리기는 손대지 않는다(Esc 포함 전부 판 것). 판정 함수는 MapViewModel.ImageCrop.cs(`IsImageCropPanelKey` · `IsInImageCropPanel`, ContentElement 는 IsAncestorOf 예외를 피하려고 Visual/Visual3D 만).
- 증거: 수정 전 `should_leave_delete_and_arrow_keys_to_panel_when_key_comes_from_image_crop_panel` 실패 — "판에서 온 Delete 를 지도 처리기가 소비했다" / 수정 후 통과. (Ctrl 조합은 Keyboard.Modifiers 를 위조할 수 없어 Delete · 방향키로 시험 — Ctrl+Z/Y 는 같은 줄에서 같이 빠진다, 헤디드 확인 항목)

### D-2 / E-17 판이 떠 있는데 편집 모드를 끄면 판이 남고, 적용하면 편집 모드 꺼진 채 DB 수정
- 원인: `IsEditModeEnabled` 꺼짐 정리(선택 · 팔레트 · 배치 · 선 그리기)에 자르기 판이 없음. (MapViewModel.cs:8635 부근)
- 수정: 꺼짐 정리에 `CancelImageCropPanel()` — 판 VM 의 취소 명령을 실행(처음 불러오기 = 추가 안 함, 다시 자르기 = 그대로). DECISIONS 7 (b) 그대로.
- 증거: 수정 전 `should_cancel_open_crop_panel_and_add_nothing_when_edit_mode_is_turned_off` 실패 — "편집 모드를 껐는데 자르기 판이 남아 있다" / 수정 후 통과.
- 3D 진입: 3D 는 편집 모드에서 이미 막힌다(`IsMap3DBlockedByInputMode`). 단 아래 "결정" ② 참고 — Ctrl+I 는 편집 모드가 아니어도 판을 연다.

### D-3 / E-33 다시 자르기 판이 떠 있는 동안 그림이 바뀌면 원본 · 크기와 지금 경계가 섞임
- 원인: `RecropImageAsync` 가 판을 열 때 잡은 `path`/원본 픽셀 크기로 자르고, 적용 때의 `marker.ImageBounds`/`Width` 로 경계를 옮긴다 — 그 사이 Undo 로 그림이 바뀌어도 그대로 적용. (MapViewModel.ImageCrop.cs:105~113)
- 수정: 적용 직전 `marker.FilePath != path` 면 적용하지 않고 "그림이 바뀌어 자르기를 취소했습니다" + 로그.
- 증거: 수정 전 실패 — `Assert.Equal() Failure: Strings differ … Expected: "Images/Overlays/발전소_…_0.png" Actual: "…_crop_…"` / 수정 후 통과.

### D-4 / E-07 · E-08 넓이 검사는 통과하지만 남는 픽셀이 0개 → 완전 투명 PNG 저장
- 원인: `ImageCropMath.Validate` 는 넓이(≥0.5)만 본다. 픽셀은 중심이 안에 들 때만 남으므로 1×1 삼각형 · 0.8 px 띠는 0개. (ImageCropMath.cs:25)
- 수정: `Validate` 끝에 `HasAnyPixel(polygon)`(새 public 정적 메서드 — 더하기만) 검사 → "남는 픽셀이 없습니다 — 다각형을 더 크게 그려 주세요". 행마다 교차점 구간만 보고 경계는 `Contains`(MaskAndCrop 과 같은 판정)로 다시 확인. 판 VM 의 적용이 Validate 를 쓰므로 판도 막힌다.
- 증거: 수정 전 `should_block_apply_with_reason_when_polygon_keeps_no_pixel` — `Assert.Contains() Failure: Sub-string not found / String: "(통과)" / Not found: "픽셀"`, `should_keep_panel_open_with_reason_when_first_load_polygon_keeps_no_pixel` — "남는 픽셀이 없는데 적용됐다(완전 투명 PNG)", `should_agree_with_mask_…` — `MissingMethodException: ImageCropMath.HasAnyPixel 없음`(수정 전 실행 때만 리플렉션으로 바꿔 컴파일) / 수정 후 3건 통과(무작위 600개에서 MaskAndCrop 과 일치).

### D-5 / E-34 자르기 · 되돌리기 DB 저장 실패를 삼키고 Undo 를 쌓고 "잘랐습니다"
- 원인: `ApplyImageCropStateAsync` 가 `DbUpdateProcess` 예외를 로그만 하고 끝 → 호출부가 Undo Push · 성공 안내. (MapViewModel.ImageCrop.cs:160~166) `UpdateImageAsync` 는 실패 시 예외(반환 null 은 실패 아님 — 기존 시험 목이 null 을 돌려줌).
- 수정: `TryApplyImageCropStateAsync(id, state, restoreOnFailure)`(private, bool) — 그림 행 저장 실패면 false 이고 마커를 이전 상태로 되돌림. 자르기 · 되돌리기는 false 면 Undo 를 쌓지 않고 "저장하지 못해 자르지 않았습니다" / "저장하지 못해 되돌리지 않았습니다". `ApplyImageCropStateAsync`(Undo/Redo 경로, 시그니처 그대로)는 되돌림 없이 예전과 같다. 그림 행은 저장됐는데 MapLayers 경로만 실패하면 예전처럼 로그만.
- 증거: 수정 전 실패 — `Assert.Equal() Failure: Strings differ … Expected: "Images/Overlays/발전소_…_0.png" Actual: "…_crop_…"` / 수정 후 통과.

### D-6 / E-16 판이 떠 있을 때 Ctrl+I 로 또 불러오기 → 파일까지 고른 뒤 아무 안내 없이 사라짐
- 원인: `PromptImageCropAsync` 가 이미 열려 있으면 조용히 Cancel → `CopyOverlayImageWithCropAsync` null. (MapViewModel.ImageCrop.cs:41)
- 수정: `RejectWhileImageCropPanelOpen()` — `ExecuteLoadImageOverlay` 에서 권한 확인 뒤 · 파일 고르기 창 앞에, `CopyOverlayImageWithCropAsync` 첫 줄에도. "자르기 판을 먼저 닫으세요"(autoHide) + 로그. CanExecute 는 그대로.
- 증거: 수정 전 `should_tell_to_close_panel_first_…` — `Assert.Equal() Failure: Strings differ / Expected: "자르기 판을 먼저 닫으세요" / Actual: null`, `should_check_open_crop_panel_before_opening_file_picker_…` — "자르기 판 가드가 파일 고르기 창보다 앞에 없다" / 수정 후 통과. (명령 실행은 모달 파일 창이 떠서 시험하지 않음 — 소스 순서 + CopyOverlay 단계로)

### D-7 / E-36 「다각형으로 자르기…」 · 「자르기 되돌리기」 메뉴에 AutomationId 없음
- 수정: 같은 메서드의 규칙(`GMaps.Symbol.ContextMenu.DetailComponents`)을 따라 `GMaps.Symbol.ContextMenu.ImageCrop` · `GMaps.Symbol.ContextMenu.ImageUncrop`. (MapViewModel.cs ShowMarkerContextMenu)
- 증거: 수정 전 `should_show_crop_items_with_automation_ids_…` — `Expected: "GMaps.Symbol.ContextMenu.ImageCrop" / Actual: ""` / 수정 후 통과(MenuTrap 으로 실제 메뉴를 열지 않고 잡음).

### E-11 저장 폴더에 쓰지 못하면 안내 없이 실패(처음 불러오기) · 예외가 새어 나감(다시 자르기)
- 원인: 처음 불러오기 — `CopyImageToLocal` · `ImageCropStore.Save` 예외가 `CopyOverlayImageWithCropAsync` 밖으로 나가 호출부(`ExecuteLoadImageOverlay` catch)가 Error 로그만, 사용자 안내 없음. 다시 자르기 — `Save` 예외가 fire-and-forget(`_ = RecropImageAsync`) 밖으로 나가 관찰되지 않는 예외 · 안내 없음. (MapViewModel.ImageCrop.cs:80 · 109)
- 수정: IOException/UnauthorizedAccessException 만 잡아 처음 불러오기 = null + "그림을 저장하지 못해 추가하지 않았습니다 — 저장 폴더를 확인하세요", 다시 자르기 = 그대로 + "자른 그림을 저장하지 못했습니다 — 저장 폴더를 확인하세요".
- 증거: 수정 전 `should_tell_and_add_nothing_without_throwing_…` — `System.UnauthorizedAccessException : 쓰기 금지`, `should_tell_and_leave_placed_image_when_cropped_png_cannot_be_written_on_recrop` — `System.IO.IOException : Cannot create '…\blocker' because a file or directory with the same name already exists.` / 수정 후 통과.


## F map-3d(E · F 의 map-3d 쪽 · B/C 뷰어)

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


