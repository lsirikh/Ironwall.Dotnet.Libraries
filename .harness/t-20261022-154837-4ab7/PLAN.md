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

## 추가 범위(2026-10-22 사용자 추가 지시) — 3D 이름표가 2D 와 어긋나는 다리 결함
사용자 보고: "구역이나 영역 잠금 처리한게 Label이 3D에서 안보이는거 같아. 이런버그가 또 있는지 분석해줘". 대화 세션 분석: 잠금은 원인이 아님(설치본 엔진이 이름표 수정 전 판).
그러나 GIS → 3D 다리가 2D 와 어긋나 함께 고쳤다. 엔진(map-3d)은 다른 세션이 같은 계약으로 고쳤다(f45f073 · e83ce39 · 4dec227).
- [x] 1 선 · 구역 label(show = ShowTitle) — `Map3DOverlayLine.Label/ShowLabel`, `Map3DOverlayBridge.LineLabelOf`
- [x] 2 이름표 자리: 점 `label.offset{x,y}`(LabelOffsetX/Y DIP), 선/구역 `label.anchor{u,v}` + `label.lon/lat`(`Map3DOverlay.LineLabelAt`, 2D LabelAdorner 규칙 — DECISIONS 13)
- [x] 3 `showShape`(점, 늘 실음 — DECISIONS 12)
- [x] 4 투명 배경 `bg:"transparent"`(`Map3DOverlay.ChipBackground`, 기존 시험 한 줄 계약에 맞춰 바꿈: `Map3DOverlayV2Tests` InlineData · `Map3DOverlayPixelTests` 단정)
- [x] 5 `label.font` · `label.italic` · `label.maxWidth`(2D 유효값 — DECISIONS 14)
- [x] 6 오버레이 이미지 label(+ anchor U/V — DECISIONS 15)
- [x] 7 도형 `opacity` — 실측: PNG 가 루트 투명도를 굽는다(0.5 → 알파 128) → 도형 아이콘은 투명도 없이 굽고 숫자로 보냄(DECISIONS 11)
- [x] 8 2D 꼭짓점 → 3D pts patch — 이미 동작(시험만 추가, DECISIONS 19)
- [x] 엔진 알림 반영: 3D 손잡이 저장 뒤 `overlay.patch`(지금 값 전체)를 반드시 — `Map3DOverlayBridge.PatchNow` ← `CommitOverlayEdit3D`(DECISIONS 18). 상자 고름 섞임 정책 = 각자(DECISIONS 16). building.select ids · building.none = 건물 작업 몫(DECISIONS 17)
- 시험: `tests/GMaps.Ui.Tests/Map3DOverlayLabelTests.cs`(순수 6) · `tests/GMaps.Housing.Tests/Map3DOverlayLabelBridgeTests.cs`(다리 JSON 4) · `Map3DOverlayPatchNowTests.cs`(1) · `Map3DOverlayPixelTests` 투명도 실측 1 · `Overlay3DScenarioTests` 저장 직후 patch 단정
- **고치기 전 실패 → 고친 뒤 통과**: Map3DOverlay.cs · Map3DOverlayBridge.cs 를 `git show HEAD:` 판으로, Overlay3D 의 PatchNow 호출을 빼고, 새 API 를 쓰는 Housing 시험 2파일을 잠시 빼서(백업 $TEMP/after2) `dotnet test tests/GMaps.Housing.Tests --filter FullyQualifiedName~Map3DOverlayLabelBridgeTests` → 실패 3 / 통과 1(통과 1 = 꼭짓점 시험 — 이미 되던 것). 되돌린 뒤 `--filter FullyQualifiedName~Map3DOverlay` → 통과 17 / 실패 0. `git status` 로 복원 확인.
- 고친 뒤: `dotnet test tests/GMaps.Ui.Tests --filter FullyQualifiedName~Map3DOverlay` → 77/0, `dotnet test tests/GMaps.MapEdit.Tests --filter FullyQualifiedName~Overlay3DScenarioTests` → 13/0
- 추가 범위 뒤 전체 회귀(창 띄우는 2클래스 뺌): GMaps.Ui.Tests 1872/0 · MapEdit 929/0 · MapLayer 102/0 · MapNav 70/0 · MapAux 130/0.
  PropertyPanel · Housing 은 렌더 시험(RenderTargetBitmap — 카메라 상자 모서리 · 문 깜빡임 · 펜스 거울 · 아이콘 PNG 등)이 무더기로 실패했다 —
  이 회차 도중 사용자 세션이 끊김 상태였다(`query user` → 상태 「디스크」(Disc), 유휴 8분). 끊긴 세션에서는 WPF 렌더 시험이 실패하는 것이 알려진 현상이고,
  이번에 손대지 않은 `FenceMirrorTests` 도 다시 돌리니 실패했으며, 같은 코드의 `Map3DOverlayPixelTests` 는 몇 분 전(세션 연결 중) 17/0 으로 통과했다 → 환경 탓으로 판단.
  재접속 뒤 `dotnet test tests/GMaps.PropertyPanel.Tests --filter "FullyQualifiedName!~ComponentCardScreenSizeTests"` · `dotnet test tests/GMaps.Housing.Tests --filter "FullyQualifiedName!~MapLocateFitTests"` 를 다시 돌려 확인할 것(이전 회차에는 430/0 · 361/0).
- `dotnet build Ironwall.Dotnet.Libraries.sln` → 오류 0. CameraPopup.Tests 는 이전 회차 346/0(이번 추가 범위는 다리 · Utils 만 — 다시 돌리지 않음, 끊긴 세션이라 렌더 시험이 섞이면 거짓 실패가 난다).

## 새로 온 사람을 위한 맥락
- 3D 메시지 해석: `Utils/Map3DMath.cs` `ParseMessage` → 편집 계열은 `Utils/Map3DEdit.cs` `Parse`. VM 분기: `MapViewModel.Map3D.cs` `OnMap3DMessage`.
- 3D 로 보내기: 지도 심볼 메시지는 장면(job)에 매이지 않아 `PostBuilding3D`(시험은 `_map3DBuildingPost` 로 가로챈다)로 바로. 꾸미기 · 직접 그린 건물은 job 이 맞을 때만(`PostUserBuilding3D`).
- 시험 장치: `Map3DEntryScenarioTests.Rig`(WiredMapRig + 3D 필드) · `Map3DExitInputScenarioTests.Enter/From3D`. GMaps 시험 프로젝트는 .sln 밖이라 저장소 검사(`dotnet test Ironwall.Dotnet.Libraries.sln`)가 돌리지 않는다 — 직접 돌린다.
- 창을 띄우는 시험 클래스(Housing `MapLocateFitTests` · PropertyPanel `ComponentCardScreenSizeTests`)는 사용자 PC 사용 중이라 이번에 빼고 돌렸다.

## 남은 일 · 이유
- 엔진(map-3d) 쪽 구현은 다른 세션 몫: overlay.select 표시, edit.start overlay 손잡이, pick 의 ctrl/shift, edit.commit overlay, overlay.box. 실기(실제 3D 화면) 확인은 엔진이 이 계약으로 고친 뒤 헤디드 1회로.
- 되돌리기(Ctrl+Z) 뒤 심볼이 옛 자리로 가면 오버레이 patch 가 엔진에 간다 — 엔진 알림(저장 뒤 patch 로 고치는 중 모양을 맞춘다)대로면 같은 patch 로 맞춰진다. 실기 확인은 헤디드 몫(DECISIONS 9).
- 건물 작업(t-20261022-154533-1fef) 몫: building.select ids 를 고름이 바뀔 때마다 돌려주기(Ctrl+클릭 확정), building.none(하늘 클릭 포함), building.box(DECISIONS 16 · 17).
- 선 이름표 지리 자리는 2D 의 8 px 간격을 넣지 않는다(DECISIONS 13) — 실기에서 어긋남이 크면 엔진이 anchor 로 다시 세면 된다.
- 설치본 엔진이 이름표 수정 전 판(5cd00ec)이라는 분석 — 현장 엔진 판 교체는 우리 일이 아니다(배포 쪽).
