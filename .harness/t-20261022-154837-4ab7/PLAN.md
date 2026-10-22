# PLAN — 3D 에서 지도 심볼(오버레이 마커) 고르기 · 고치기 (t-20261022-154837-4ab7)

## 목표
사용자 보고(2026-10-22): "3D 상태에서 OverlayMarker들 우리 심볼들 이거 선택을 하면 Adorner가 다 안보이고 사라진다. 선택은 되는데 이것도 문제인것 같다."
원인(대화 세션 조사): 3D pick → `OnMap3DPick` → `MainMap.TriggerMarkerClicked` 로 2D 마커를 고르지만, 2D 편집 어도너는 2D 지도에 붙고
3D 호스트(WebView2, 별도 창 핸들)가 같은 칸을 덮어 어도너가 그 밑에 가린다(에어스페이스).
해결: 고름 표시 · 손잡이를 3D 엔진이 그리게 GIS 가 메시지를 보내고, 3D 손잡이로 고친 결과를 2D 어도너와 같은 저장 · 되돌리기 경로로 받는다.
건물 쪽(t-20261022-154533-1fef)의 파일(MapViewModel.BuildingHeight.cs · BuildingErase.cs · BuildingHeightPanelStyle.xaml)은 고치지 않았다(BuildingHeight.cs 의 `PostBuilding3D` 는 부르기만).

## 진행
- [x] 원인 · 기존 경로 읽기: 2D 고름(SelectedMarker · GroupSelectionService), 2D 어도너 끝(OnMarkerEditCompleted → DbUpdateProcess · EditRecorder.RecordTransform/RecordLineGeometry), 3D 메시지(Map3DMath.ParseMessage · Map3DEdit), 시험 장치(Map3DEntryScenarioTests.Rig · `_map3DBuildingPost`)
- [x] 계약(순수 함수) — `Utils/Map3DEdit.cs` · `Utils/Map3DMath.cs`
  - GIS → 3D: `OverlaySelectJson` (overlay.select), `OverlayEditStartJson` (edit.start target {kind:"overlay", overlayKind, id, rotatable}), `OverlayEditRevertJson`
  - 3D → GIS: pick 의 `ctrl` · `shift`(`Map3DPick.Ctrl/Shift`, 없으면 false), edit.commit overlay(`Map3DMessage.OverlayEdit` = `Map3DOverlayEdit` — 점 lon/lat(+heading_deg), 선 · 구역 points), overlay.box(`Map3DMessage.OverlayBox` = `Map3DOverlayBox`)
- [x] VM — 새 파일 `ViewModels/Maps/MapViewModel.Overlay3D.cs`
  - `QueueMap3DSelectionSync` / `SyncMap3DSelection` / `ResyncMap3DSelection`: 2D 고름(묶음 ∪ 단일) → overlay.select(같으면 다시 안 보냄), 손잡이 대상(편집 모드 · 단일 · 잠기지 않음 · 3D 위 판 없음 · 이미지 아님)이면 edit.start, 아니면 edit.stop
  - rotatable = 선 · 구역 아님 && Shape 가 빌보드 아님(2D `MarkerEditAdorner.IsBillboardTarget` 과 같은 판정)
  - `CommitOverlayEdit3D`: 마커 값 바꾼 뒤 `OnMarkerEditCompleted`(2D 와 같은 DB 저장 · 편집 기록). 편집 모드 꺼짐 · 잠김 · 권한 없음 · 점 부족 → edit.revert. 모르는 id → 무시
  - `OnMap3DOverlayBox`: 편집 모드에서만, 잠김 · 이미지 빼고 add → `OnMarkersRubberBandSelected`(2D 러버밴드), add=false → `ApplyGroupSelectionByIds`
- [x] 배선
  - `MapViewModel.cs` `SelectedMarker` setter → `QueueMap3DSelectionSync()`
  - `MapViewModel.LayerSelection.cs` `OnGroupSelectionChanged` → `QueueMap3DSelectionSync()`
  - `MapViewModel.Map3D.cs`: `Reveal3D` → `ResyncMap3DSelection(newPage: false)`(바뀐 고름만 · 손잡이 다시), ready(새 페이지) → `ResyncMap3DSelection(newPage: true)`(기록 비움). 고른 것이 없으면 아무것도 보내지 않는다(DECISIONS 10); `OnMap3DMessage` 에 overlay.box 갈래; `OnMap3DPick` — Ctrl+클릭 = `OnMapMarkerToggleRequested`(2D Ctrl+클릭과 같은 처리기), 편집 모드 빈 곳 클릭 = `ClearAllSelections()`, 마커 찾기는 `FindMap3DMarker`
  - `MapViewModel.Edit3D.cs` `OnMap3DEditMessage`: edit.commit overlay → `CommitOverlayEdit3D`, edit.end(심볼 손잡이) → 고름 풀기(edit.stop 되보내지 않음)
- [x] 도움말 `Help/MapsHelp.cs` 「편집 모드」(Map.Toolbar.EditMode) 항목의 3D 심볼 줄만
- [x] 시험(헤드리스 · 창 없음)
  - `tests/GMaps.Ui.Tests/Map3DOverlayEditTests.cs` (순수 7개)
  - `tests/GMaps.MapEdit.Tests/Overlay3DScenarioTests.cs` (실제 VM 경로 13개 — 새 API 를 부르지 않고 JSON · VM 처리기만 써서, 고치기 전 코드로도 컴파일되어 실패를 보인다)

## 검증(이 세션에서 실행한 명령 · 결과)
- 기준선(고치기 전): `dotnet test tests/GMaps.MapEdit.Tests --filter "FullyQualifiedName~Map3D|FullyQualifiedName~TwinContract|FullyQualifiedName~SymbolSelection|FullyQualifiedName~DoorAndMultiSelect"` → 통과 603 / 실패 0
- 빌드: `dotnet build Ironwall.Dotnet.Libraries.GMaps.Ui/...csproj` → 오류 0
- 순수: `dotnet test tests/GMaps.Ui.Tests --filter "FullyQualifiedName~Map3DOverlayEditTests|FullyQualifiedName~Map3DEditTests|FullyQualifiedName~Map3DMathTests"` → 통과 87 / 실패 0, `--filter FullyQualifiedName~Map3DOverlayEditTests` → 7 / 0
- GMaps.Ui.Tests 전체 → 통과 1866 / 실패 0

## 회귀 · 증명
- 첫 전체 회귀에서 MapEdit 3개 실패(BuildingHeightScenarioTests 2 · TwinContractScenarioTests 1 — 「고르기만으로는 3D 를 바꾸지 않는다」 = `Assert.Empty(rig.Posts)`): 3D 진입 때 빈 overlay.select 를 보낸 것이 원인 → DECISIONS 10 으로 고침 → 다시 통과.
- 고친 뒤: `dotnet test tests/GMaps.MapEdit.Tests --filter "...Overlay3DScenarioTests|...BuildingHeight|...TwinContract|...Map3D|...Edit3D"` → 통과 634 / 실패 0
- 고친 뒤 MapEdit 전체 → 통과 928 / 실패 1(`GroupRotationEdgeScenarioTests.should_confirm_rotation_when_group_move_drag_starts_during_preview` — ] 4번이 4° 대신 0.4°, 즉 Shift 가 눌린 것처럼 돌았다. 같은 클래스만 3번 다시 돌리니 실패 1 · 통과 2. 숨은 데스크톱이어도 실제 Shift 키 상태를 읽는 시험이라 사용자가 PC 사용 중 생긴 간헐 실패로 보고, 이번 변경과 무관한 경로(묶음 회전 키)다. 앞선 MapEdit 전체 실행에서는 통과했다)
- MapLayer 102/0 · MapNav 70/0 · MapAux 130/0 · PropertyPanel 430/0(창 띄우는 `ComponentCardScreenSizeTests` 뺌) · Housing 361/0(창 띄우는 `MapLocateFitTests` 뺌)
- 저장소 검사 쪽: `dotnet build Ironwall.Dotnet.Libraries.sln` → 오류 0. .sln 안에서 GMaps.Ui 를 참조하는 시험은 CameraPopup.Tests 하나 → `dotnet test Ironwall.Dotnet.Libraries.CameraPopup.Tests --no-build` 346/0. 나머지 sln 시험 프로젝트(Watchdog · Utils · CameraPopup.Host)는 바꾼 코드를 참조하지 않아 돌리지 않았다.
- **고치기 전 실패 → 고친 뒤 통과**: 바꾼 원본 7개를 `git show HEAD:<경로>` 판으로 잠시 바꾸고 새 파일 MapViewModel.Overlay3D.cs 를 빼서(백업 $TEMP/after-backup) `dotnet test tests/GMaps.MapEdit.Tests --filter FullyQualifiedName~Overlay3DScenarioTests` → **실패 13 / 통과 0**(시나리오 시험은 새 API 없이 JSON · VM 처리기만 써서 옛 코드로도 컴파일된다). 백업으로 되돌린 뒤 같은 명령 → **통과 13 / 실패 0**. `git diff --stat` 로 복원 확인(7 파일, +115/−12).
- 정리: 이 worktree 의 testhost · dotnet 프로세스 없음 확인. 남아 있던 testhost(PID 206404)는 다른 작업(t-20261022-154533-1fef) 것이라 건드리지 않았다. 임시 파일은 $TEMP 안에만.

## 새로 온 사람을 위한 맥락
- 3D 메시지 해석: `Utils/Map3DMath.cs` `ParseMessage` → 편집 계열은 `Utils/Map3DEdit.cs` `Parse`. VM 분기: `MapViewModel.Map3D.cs` `OnMap3DMessage`.
- 3D 로 보내기: 지도 심볼 메시지는 장면(job)에 매이지 않아 `PostBuilding3D`(시험은 `_map3DBuildingPost` 로 가로챈다)로 바로. 꾸미기 · 직접 그린 건물은 job 이 맞을 때만(`PostUserBuilding3D`).
- 시험 장치: `Map3DEntryScenarioTests.Rig`(WiredMapRig + 3D 필드) · `Map3DExitInputScenarioTests.Enter/From3D`. GMaps 시험 프로젝트는 .sln 밖이라 저장소 검사(`dotnet test Ironwall.Dotnet.Libraries.sln`)가 돌리지 않는다 — 직접 돌린다.
- 창을 띄우는 시험 클래스(Housing `MapLocateFitTests` · PropertyPanel `ComponentCardScreenSizeTests`)는 사용자 PC 사용 중이라 이번에 빼고 돌렸다.

## 남은 일 · 이유
- 엔진(map-3d) 쪽 구현은 다른 세션 몫: overlay.select 표시, edit.start overlay 손잡이, pick 의 ctrl/shift, edit.commit overlay, overlay.box. 실기(실제 3D 화면) 확인은 엔진이 이 계약으로 고친 뒤 헤디드 1회로.
- 되돌리기(Ctrl+Z) 뒤 심볼이 옛 자리로 가면 오버레이 patch 가 엔진에 간다 — 엔진이 그때 손잡이 자리도 맞추는지 엔진 세션이 확인해야 한다(DECISIONS 9).
