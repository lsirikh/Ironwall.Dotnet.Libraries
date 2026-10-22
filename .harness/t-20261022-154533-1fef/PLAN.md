# PLAN — 3D 건물 고르기를 2D 마커처럼 · 「건물 높이」 판 글자 잘림 (t-20261022-154533-1fef)

## 목표(사용자 원문, 2026-10-22, 판 캡처)
"좀 잘리네 글자가 그리고 건물을 고르는게 힘들다 우리 2D에서 Marker 수정하고 선택할때 와 같은 방식으로 할 수 있도록 해줘"
- 캡처: [지우고 이 자리에 새로 그리기] 가 "지우고 이 자리에 새로 ㄱ" 로 잘림, [여러 개 고르기 끝] 모드 단추.
- 범위: 이 저장소(GIS) 쪽만. 엔진(map-3d)은 다른 세션이 같은 계약으로 고친다.

## 진행
- [x] 2D 정본 규칙 읽기 — `GMapCustomControl.cs:1165`(편집 모드 Shift+좌드래그 러버밴드) · `:1204`(Ctrl+클릭 = MarkerToggleRequested, Shift 없을 때) ·
      `MapViewModel.cs:1184 OnMarkersRubberBandSelected`(2D 는 겹치면 토글) · `:1197 OnMapMarkerToggleRequested` · 빈 곳 클릭 = OnMapClicked → 해제.
- [x] 계약 해석(엔진 → GIS): `building` 에 `ctrl` · `shift`, 새 `building.box {items, add}`(최대 500), `building.none`. GIS → 엔진: `building.select {id, ids?}`.
- [x] 고르기 규칙 구현(아래 「코드 위치」).
- [x] [여러 개 고르기] 모드 단추 · `ToggleBuildingMultiSelectCommand` · `BuildingMultiButtonText` 제거 → 2개 이상이면 [고른 n개 지우기] · 「고른 건물 n개」.
- [x] 판 글자 잘림 — 긴 단추 · 외벽 무늬 콤보는 한 줄 전체 폭, 색 둘은 두 칸.
- [x] "?" 말풍선(`MapsHelp.cs` `Map.BuildingHeight.Panel`) 「여러 개 고르기」 절 · 판 안내 글(상태만 "고른 건물 n개").
- [x] 시험 — 새 시험 파일 + 기존 시험 새 규칙으로, 고치기 전 실패 → 고친 뒤 통과 증명.
- [x] CHANGELOG.md 항목.
- [ ] (accept 때 하네스가) `dotnet test Ironwall.Dotnet.Libraries.sln` — 아래 「검증」 참고.

## 정한 규칙(GIS 쪽)
| 입력 | 결과 |
|---|---|
| 클릭(키 없음) | 그 건물 하나만 고름, 판 = 그 건물. 앞에 2개 이상 골랐으면 `building.select {id, ids:[id]}` 로 강조를 하나로(하나 → 하나는 엔진이 스스로 옮김 — 보내지 않음) |
| Ctrl+클릭 (메시지 ctrl ∨ 호스트 Ctrl), Shift 만 눌린 클릭도 | 넣기 · 빼기. 넣으면 판 = 넣은 건물, 판의 건물을 빼면 남은 것 중 마지막, 모두 빼면 판 닫힘(고름 풀림). 늘 `building.select {id, ids}` |
| `building.box add=false` | 상자 안 건물로 새로(빈 상자 = 풀기). 판 = 지금 건물이 남아 있으면 그대로, 아니면 첫 건물 |
| `building.box add=true` | 지금 고름에 더함(합집합, 겹친 것 그대로) |
| `building.none` · Esc | 판 닫기 = 고름 풀기(`building.select {id:null}`) |
| 편집 모드 꺼짐 | 클릭 · 상자 모두 무시, 엔진이 그린 윤곽은 `building.select null` 로 지움 |
| 직접 그린 건물(user_uid) | 기존 길(고치기 판) — 고름에 넣지 않음. origin user · 숨긴 건물 · 모르는 id 도 고름에서 뺌 |
| 3D 그리기 중(draw.start) | 클릭 · 상자 무시(점 찍기 몫) |
| [이 건물 지우기] · 여러 개 지우기 | 지운 건물은 고름에서도 뺌(남은 고름의 강조는 유지) |

## 코드 위치
- `Ironwall.Dotnet.Libraries.GMaps.Ui/Utils/Map3DBuilding.cs` — `SelectMessageJson(id, ids)`(ids = 문자열 배열), `ParseBox`(최대 `MaxBoxItems`=500), `ParseModifiers`.
- `Utils/Map3DMath.cs` — `Map3DMessage.Ctrl/Shift/BuildingBox/BoxAdd`, `ParseMessage` 의 `building` · `building.box`.
- `ViewModels/Maps/MapViewModel.Map3D.cs` — `building`(키 전달) · `building.box` 처리 분기(예외 격리).
- `ViewModels/Maps/MapViewModel.BuildingHeight.cs` — `OnMap3DBuildingPicked(pick, ctrl, shift)` · `CanPickMap3DBuildings`(편집 모드 · 3D 그리기 배타 · 네이티브 창) · `OpenBuildingHeightPanel` · `CloseBuildingHeightPanel` 이 고름도 비움.
- `ViewModels/Maps/MapViewModel.BuildingErase.cs` — 고름 `BhSelection`, `ToggleBuildingSelection`, `OnMap3DBuildingBox`, `PostBuildingSelection`, `BuildingSelectionIds`(시험 · 계측), [고른 n개 지우기].
- `ViewModels/Maps/MapViewModel.BuildingLook.cs` — 단일 숨기기 뒤 그 건물만 고름에서 빼고 남은 강조 유지.
- `Themes/BuildingHeightPanelStyle.xaml` — 외관 칸(무늬 한 줄 + 색 두 칸) · 지우기 칸([이 건물 지우기]|[고른 n개 지우기] / [지우고 이 자리에 새로 그리기] / [이 영역 자동 건물 모두 지우기…]).
- `Help/MapsHelp.cs` — `Map.BuildingHeight.Panel` 「여러 개 고르기」 절.

## 검증(이 세션에서 돌린 명령 · 결과)
1. 기준선(고치기 전): `dotnet test tests/GMaps.MapEdit.Tests/GMaps.MapEdit.Tests.csproj` → 실패 2 · 통과 914 · 전체 916.
   (이미 있던 실패 — `Map3DCompositeScenarioTests.should_keep_key_held_move_in_3d_when_esc_pressed_and_stop_only_when_exit_pressed` 등, 키 누름 상태 관련. 이번 변경과 무관)
2. 글자 잘림 시험을 옛 XAML 로: `--filter should_fit_every_button_line_and_combo_text` → 실패
   `["지정 색 #123456 @ TextBlock(91)", "지우고 이 자리에 새로 그리기 @ TextBlock(161)"]` (외벽 색 칸 91 px · 두 칸 단추에 161 px 필요).
3. 고친 뒤: `--filter "FullyQualifiedName~TwinContractScenarioTests|FullyQualifiedName~BuildingHeight|FullyQualifiedName~UserBuilding"` → 통과 161/161.
4. (아래 「결과 채움」 참고) MapEdit 전체 · GMaps.Ui.Tests · 고치기 전 사본.

### 글자 잘림 측정 방법(창 없음)
`ClippedTexts` — 판을 실제 테마 템플릿으로 320×1400 배치한 뒤, 보이는 TextBlock 과 그 조상(판까지)에 `LayoutInformation.GetLayoutClip` 이 붙었으면 잘림
(WPF 는 들어가지 않는 요소에만 배치 잘림을 붙인다), 줄임표(TextTrimming) 글은 FormattedText 폭 > 실제 폭이면 잘림. 단추마다 제약 없이 잰 DesiredSize(바깥 여백 뺌) ≤ 실제 폭.
콤보 세 개는 값마다(외벽 무늬 22 + 색 13 + 지정 색) 다시 배치해 잰다.

### 고치기 전 증명 방법
제품 코드 8개 파일만 `git show HEAD:<path>` 로 되돌린 사본을 `$TEMP/before-proof` 에 만들고(시험은 새것), 새 고름 목록 속성 대신 옛 필드(`_bhMultiList`)를 읽는
보조 `SelIds` 를 사본 시험에만 넣어 같은 시험을 돌렸다.

## 결과 채움
5. 고친 뒤 MapEdit 전체: `dotnet test tests/GMaps.MapEdit.Tests/GMaps.MapEdit.Tests.csproj` → 통과 921 · 실패 0(기준선 916 + 새 5. 기준선의 2건 실패도 이번엔 통과 — 키 누름 상태에 따라 흔들리는 시험).
6. `dotnet test tests/GMaps.Ui.Tests/GMaps.Ui.Tests.csproj` → 통과 1862 · 실패 0(새 3건 포함).
7. 고치기 전 사본(`$TEMP/before-proof`, 제품 코드 8개만 HEAD)에서 새 · 고친 시험 6건:
   `--filter "should_select_one_on_click|should_select_buildings_in_shift_drag_box|should_ignore_click_ctrl_click_and_box|should_keep_drawn_building_routes|should_fit_every_button_line|should_pick_several_by_ctrl_click_like_2d"`
   → 6/6 실패: 클릭 뒤 고름 "8|"(옛 코드는 클릭이 고름에 넣지 않음) · building.box 해석 안 됨(null) 2건 · 편집 모드 꺼짐 상자 null · Ctrl 메시지 키 무시([] ≠ [8,9]) ·
   [여러 개 고르기] 단추가 남아 있음 · 단추 폭 부족. 고친 뒤 같은 6건 통과(5 · 6).
8. 저장소 검사 `dotnet test Ironwall.Dotnet.Libraries.sln` → 11개 어셈블리 모두 통과(2,974건, 실패 0). Sounds · Libraries.Db 시험은 .sln 밖이라 돌지 않는다.
- 창을 띄우는 시험은 돌리지 않았다(사용자 PC 사용 중) — 모두 창 없는 배치 · 가짜 엔진.
- 정리: 띄운 프로세스 없음. `$TEMP/before-proof` · `$TEMP/src.tar` 는 이 작업의 임시 폴더(하네스가 지운다 — worktree 밖이라 직접 지우지 않음).

## 남은 일 · 주의
- 엔진(map-3d) 쪽: `ctrl/shift` · `building.box` 를 보내고 `building.select.ids` 로 여러 건물을 강조해야 3D 에서 보인다(다른 세션). 엔진이 옛 판이면: Ctrl+클릭은 호스트 키로 동작, Shift+끌기는 엔진이 상자를 보내지 않아 안 됨, 강조는 판의 건물 하나.
- UserBuildingPanelStyle.xaml 의 같은 세 칸 외관 콤보도 이름이 길면 잘릴 수 있다 — 이번 목표(「건물 높이」 판) 밖이라 손대지 않았다.
