# PLAN — 편집 모드가 돼도 3D 를 떠나지 않기(t-20261021-220337-6df5)

## 목표(사용자 원문)
"그리고 편집 모드가 돼도 굳이 3D 모드를 떠날 필요가 없어 사실 따지고 보면."

1. 편집 모드 켜기 · 끄기만으로는 3D 를 나가지 않고, 편집 모드 중에도 3D 에 들어간다(게이트 · 진입 보류 포함).
2. 편집 모드 안 기능 전수 조사 → (a) 3D 에서 이미 됨 (b) WPF 판이라 3D 위에서도 됨 (c) 2D 캔버스 · 어도너 필요. (c) 는 시작할 때만 2D 로.
3. 네이티브 창 3D(hwnd)는 예전대로 2D.
4. 3D 에 머무는 동안 편집 모드 표시가 맞고, 3D 나가기 · 편집 모드 끄기 때 3D 편집 세션이 꼬이지 않는다.
5. 시험: 옛 단정 고치기 + 새 시나리오, tests/GMaps.* 7개 프로젝트 직접 실행.

## 진행
- [x] 게이트 바꾸기 — `MapViewModel.Map3D.cs` `IsMap3DBlockedByInputMode`: 편집 모드는 `Map3DGuide.EditModeLeaves3D(Map3DHost.HostMode)`(= "hwnd")일 때만 막는다
- [x] `Map3DGuide.EditModeLeaves3D` 신설(순수 함수), `MapViewModel.Edit3D.cs` `ShouldEditIn3D` 도 같은 함수로
- [x] (c) 묶음 회전 — 회전 세션 열림을 막힘 판정에 더하고(`IsGroupRotationPanelOpen`), 열리고 닫히는 순간 `SyncMap3DWithInputModes`(`MapViewModel.GroupRotation.cs` `OnGroupRotationChanged`)
- [x] 3D 동안 창 키 후킹이 방향키(2D 화소 이동) · Ctrl+V(2D 커서 붙여넣기)를 먹지 않게(`MapViewModel.cs` `OnMapPreviewKeyDownForGroup`)
- [x] 주석 갱신 — `Map3DMath.IsBlockedByInputMode` 문서, `OnMap3DPick` 요약(편집 모드 클릭 = 편집 선택), `MapViewModel.BuildingHeight.cs` 머리 주석
- [x] 도움말 "?" — `MapsHelp.cs` `Map.Toolbar.EditMode` 에 한 줄(3D 그대로 · 2D 도구는 2D 로)
- [x] 옛 동작 시험 12건 수정(DECISIONS 4) · 새 시나리오 13건 + 순수 함수 3건
- [x] 고치기 전 실패 → 고친 뒤 통과 증명(아래 "검증")
- [x] tests/GMaps.* 7개 프로젝트 실행(아래 "검증")

## 편집 모드 기능 전수 표(요청 2)
| 기능 | 구분 | 3D 위에서 |
|---|---|---|
| 편집 모드 스위치 · 편집 스트립(툴바 행, 지도 칸 밖) | (b) | 그대로 보인다(바인딩 `IsEditModeEnabled`) |
| 속성 창(단일 · 그룹) | (b) | 3D 클릭 = `OnMapMarkerClicked` → `SelectMarkerForEditing` → 속성 창(새 시험으로 확인) |
| 레이어 판(고르기 · 잠금 · 보이기) | (b) | 그대로. 판에서 고른 것이 지도 선택으로 이어짐(`PushLayerSelectionToMap`) |
| 격자 붙기 켜기 · Undo/Redo 단추 · Ctrl+Z/Y · Delete · Ctrl+C · Ctrl+A | (b) | 선택 · 기록 기준이라 그대로 |
| 이미지 자르기 판(`ImageCropPanelControl`) | (b) | 판 안 자체 캔버스 — 그대로 |
| 건물 그리기 · 꾸미기(3D 위 판, `_ubIn3D` · `_decoIn3D`) | (a) | 3D 위 판 + 엔진 손잡이(draw.* · edit.*) — 편집 모드와 무관하게 유지(새 시험) |
| 심볼 배치(팔레트 → `IsSymbolPlacementMode`) | (c) | 시작 = 2D(기존 `ExclusiveInputModeGate`) |
| 선 · 구역 · 펜스 · PIDS 묶음 그리기(`IsLineDrawing`) | (c) | 시작 = 2D(기존) |
| 측정(`IsMeasuring`) · 앵커 그리기(`IsAnchorDrawMode`) · 홈 배치(`IsHomePlacementMode`) · 조준(`IsTargetAimMode`) | (c) | 시작 = 2D(기존) |
| 건물 그리기 · 꾸미기 2D(`[2D 에서 윤곽 고치기]`) | (c) | 시작 = 2D(기존 `IsUserBuildingDrawing && !_ubIn3D`) |
| 묶음 회전(「회전」 · ] [ · 손잡이) | (c) | **이번에 추가** — 세션이 열리면 2D |
| 방향키 선택 이동 · Ctrl+V 붙여넣기 | (c) | **이번에** 3D 동안은 받지 않음(방향키 = 3D 링) |
| 심볼 끌기 · 편집 손잡이 · 라벨 끌기 · 그룹 끌기 · 이미지 핸들 · Shift 러버밴드 · 마커 우클릭(레이어 순서) | (c) | 2D 마커 · 어도너를 눌러야만 시작 — 3D 위에서는 시작할 길이 없다(2D 지도는 3D 동안 입력 막힘) |

### (c) → 엔진 draw.* · edit.* 이전 후보(이번엔 엔진 안 고침)
- 앵커 그리기 → `draw.start(mode="rect")` + `draw.done`(이미 `Map3DEdit.ModeRect` 있음 — 가장 쉬움)
- 심볼 배치 · 홈 배치 → `draw.start(mode="point")` + `draw.point` 1회
- 선 · 구역 · 펜스 그리기 → `draw.start(mode="line"/"polygon")` + `draw.done`(새 kind 필요)
- 단일 심볼 옮기기 · 돌리기 → `edit.start(kind="symbol")` + `edit.commit(Point, heading_deg)`(엔진에 symbol 대상 추가 필요)
- 낮은 순위: 묶음 이동 · 회전(edit.* 가 단일 대상 계약), 라벨 끌기(화소 오프셋), 러버밴드(3D 는 다중 선택 개념 없음), 이미지 자르기(픽셀 처리)

## 코드 위치(새로 온 사람용)
- 게이트: `Ironwall.Dotnet.Libraries.GMaps.Ui/ViewModels/Maps/MapViewModel.Map3D.cs` — `IsMap3DBlockedByInputMode` · `SyncMap3DWithInputModes` · `EnterMap3DDirect` · `EvaluateMap3DGate` · `HostFallback` 처리기
- 규칙: `Utils/Map3DGuide.cs` `EditModeLeaves3D` · `ShouldLeave3DForWindow`, `Utils/Map3DMath.cs` `IsBlockedByInputMode`
- 3D 편집 세션: `MapViewModel.Edit3D.cs`(`EndEdit3DSessions` 는 `Leave3D` 에서만)
- 묶음 회전: `MapViewModel.GroupRotation.cs` `OnGroupRotationChanged`
- 키 후킹: `MapViewModel.cs` `OnMapPreviewKeyDownForGroup`
- 독점 입력 판정: `Helpers/ExclusiveInputModeGate.cs`

## 검증(이 세션에서 실행)
1. 수정 직후 3D 관련 MapEdit 시험: `dotnet test tests/GMaps.MapEdit.Tests --filter "...Map3D|Edit3D|UserBuilding|TwinContract|BuildingHeight"` → 실패 12 / 통과 632 — 실패 12건 = 옛 동작 단정(DECISIONS 4 로 수정)
2. 고치기 전 실패 증명: 제품 파일 6개를 `git show HEAD:…` 로 잠시 되돌리고(백업 → 복원)
   `dotnet test tests/GMaps.MapEdit.Tests --filter "Map3DEditMode|should_keep_3d_panel_and_handles|should_refuse_group_rotation_in_3d_and_stay_in_3d"` → **실패 14 / 통과 3**(통과 3 = 옛 동작에서도 참인 네이티브 창 · 나가기 · ] 키 시험 — 뒤 두 개는 3D 유지 단정을 더해 강화)
3. 부분 증명: 회전(`|| IsGroupRotationPanelOpen` + GroupRotation.cs)과 키 후킹(MapViewModel.cs)만 되돌리고 `--filter Map3DEditMode` → **실패 4**(회전 단추 auto/manual · ] 키 · 방향키) / 통과 11
4. 전체 복원 뒤 tests/GMaps.* 7개 프로젝트 — 결과는 아래 "최종 실행"

## 최종 실행
(아래에 채움)

## 남은 일 · 참고
- 헤디드(실앱) 확인은 하지 않았다 — WPF 안 3D(composition)는 WebView2CompositionControl 이 있어야 해 헤드리스로 못 본다. 시험은 HostMode "none"(= 기본 composition 으로 열림)과 "hwnd"(가짜 Border)로 갈래를 본다.
- 3D 중 Ctrl+V 는 이제 아무 일도 안 한다(안내 없음). 필요하면 후속으로 "3D 에서는 붙여넣기를 2D 에서" 안내 추가.
