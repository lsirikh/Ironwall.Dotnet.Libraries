# PLAN — 3D 고르기 서로 배타 + 외관(외벽 · 색) 끝까지 시험 · 수정 (t-20261022-093356-2dae)

## 목표(사용자 원문 요약)
- "내가 만든 굴뚝이 있고 3D 엔진이 만든 굴뚝이 있는데 왜 선택하면 이렇게 되는지" — 직접 그린 원형 굴뚝(uid 8601415628e9, 18 m)을 3D 에서 누르자
  1.4초 뒤 같은 자리의 구운 건물(id=67, 29.6 m, oblique) 고름도 와서 직접 그린 건물 판 · 손잡이와 건물 67 점선 강조가 함께 떠 있었다.
- "꾸미는데 외벽 안 바뀌고 색깔 안 바뀌고 이런 거 테스트 하나도 안 했지?" — 판에서 고른 값 → 보내는 메시지(키 · 값) → 엔진 반영 끝까지 시험이 없었다.

## 관련 코드 위치
- 3D → GIS 메시지 분기: `Ironwall.Dotnet.Libraries.GMaps.Ui/ViewModels/Maps/MapViewModel.Map3D.cs` OnMap3DMessage(building · userbuilding.pick · decoration.pick)
- 구운 건물 고름 → 「건물 높이」 판: `MapViewModel.BuildingHeight.cs` OnMap3DBuildingPicked · **OnMap3DBuildingPickedOverEdit3D(새)** · CloseBuildingHeightPanel
- 구운 건물 외관: `MapViewModel.BuildingLook.cs`(PUT …/buildings/{bid}/look → building.look, **WithSavedLook(새)**)
- 직접 그린 건물: `MapViewModel.UserBuilding.cs` OnMap3DUserBuildingPicked · SaveUserBuildingAsync(PUT → userbuilding.preview)
- 꾸미기: `MapViewModel.Decoration.cs` OnMap3DDecorationPicked · SaveDecorationAsync(PUT → decorations.reload)
- 3D 위 판 · 손잡이 · 3D 그리기: `MapViewModel.Edit3D.cs`
- 메시지 · 본문 순수 함수: `Utils/Map3DLook.cs` · `Utils/Map3DUserBuilding.cs`(PreviewMessageJson) · `Utils/Map3DDecoration.cs` · `Utils/Map3DBuilding.cs`
- 시험: `tests/GMaps.MapEdit.Tests/PickExclusiveAndLookScenarioTests.cs`(새, TwinContractScenarioTests partial — FakeEngine · Posts 재사용) ·
  `tests/GMaps.MapEdit.Tests/TwinContractScenarioTests.cs`(FakeEngine.RejectLook 추가 · Render 도우미 분리) · `tests/GMaps.Ui.Tests/Map3DTwinContractTests.cs`(preview use 키)
- 계약: `C:\Users\gh\.claude\projects\c--workspace-app-Ironwall-Dotnet-Libraries\handoff-2026-10-21\twin-contract.md` · `edit3d-contract.md`
- 엔진(읽기만 — 다른 작업이 고치는 중): `C:\workspace_python\map-3d\map3d\static\view.js` · `userbuilding.js` · `buildingedit.js` · `decor3d.js` · `decorations.js`

## 진행
- [x] 엔진 뷰어가 읽는 메시지 키 조사(읽기만, 아래 「발견」)
- [x] 1. 고르기 서로 배타 — OnMap3DBuildingPickedOverEdit3D
- [x] 2a. 구운 건물 외관 끝까지 시험 + 다시 고를 때 저장된 외관 유지(WithSavedLook)
- [x] 2b. 직접 그린 건물 외관 → PUT → userbuilding.preview 에 `use` 키 추가
- [x] 2c. 꾸미기 색(바닥 · 담장) 끝까지 시험 — 결함 없음
- [x] 고치기 전 실패 → 고친 뒤 통과 증명(아래 「검증」)
- [x] tests/GMaps.* 7개 · .sln 시험(고친 뒤) — 결과는 「검증」

## 발견(증거)
1. **클릭 한 번 = 3D → GIS 메시지 하나**(view.js:463-525 pointerup — 오버레이 > 꾸미기 > 직접 그린 상자 > 구운 건물 > 땅, 분기마다 return).
   1.4초 뒤 건물 67 고름은 **두 번째 클릭**이다. 엔진 굴뚝(29.6 m)이 직접 그린 굴뚝(18 m)보다 높아 윗부분을 누르면 건물 67 이 맞는다.
   뷰어는 고른 쪽 강조를 스스로 그린다(selectBuilding · selectUserBox — GIS 응답과 무관). GIS 는 구운 건물 고름이 직접 그린 건물 판을 닫지 않아 두 판이 함께 떴다.
2. 강조 지우기 메시지: `building.select {id:null}` 은 구운 건물 · 상자 공용 윤곽을 지운다. `userbuilding.select {uid}` 는 구운 건물 윤곽을 지우고 상자를 그린다.
   `userbuilding.select {uid:null}` 은 구운 건물이 골라져 있으면 그 윤곽을 남긴다. `decoration.select` 는 꾸미기 강조만. **셋을 한 번에 지우는 메시지는 없다**(계약 안에서 두 메시지로 충분).
3. **userbuilding.preview 에 `use`(외벽 성격)가 빠져 있었다** — 뷰어 userbuilding.js lookKeys 는 `use` 를 읽고, boxLook 은 `STYLE_OF[facade] || STYLE_OF[use]`. 외벽 무늬가 「자동」일 때 외벽 성격을 바꿔 저장해도 상자 무늬가 그대로였다.
4. **뷰어 building 메시지에 외관 키가 없다**(buildingedit.js buildingMessage — scene.json 줄의 외관은 `b.look` 안). GIS 는 최상위 키만 읽어(Map3DBuilding.Parse → Map3DLook.StateOf)
   외관을 저장한 건물을 다시 고르면 판이 「자동」. 그 상태에서 지붕 색만 바꿔 [외관 적용] 하면 PutBody 가 세 키를 모두 보내 **저장된 외벽 무늬 · 색이 자동으로 지워졌다**.
5. building.look 키(id · hidden · wall_color · roof_color · facade)는 뷰어 parseLook 과 같다. decoration props.color 는 뷰어 decor3d.js 가 재질 색보다 먼저 쓴다. 엔진 user-buildings PUT 응답(item)에는 외관 키가 다 있다(server.py:387).
6. (추정 — 엔진 쪽) 직접 그린 상자가 같은 자리의 더 큰 자동 건물 안에 들어가 있으면(18 m 굴뚝 ⊂ 29.6 m 엔진 굴뚝) 다시 만들기 전까지 상자 색 · 무늬가 밖에서 안 보인다.
   뷰어 syncBaked 는 같은 uid 의 구운 직접 그린 건물만 숨긴다(view.js:1175). 사용자 "외벽 안 바뀜"의 화면 쪽 원인일 가능성이 높다 → 엔진 후보로 보고.

## 고친 것
- `MapViewModel.BuildingHeight.cs` — OnMap3DBuildingPicked 에서 OnMap3DBuildingPickedOverEdit3D: ① 3D 그리기 중 무시(강조만 지움) ② 같은 자리의 안 구운 직접 그린 건물 우선
  (building.select null → userbuilding.select uid, 다른 건물이면 그것을 연다) ③ 아니면 직접 그린 건물 · 꾸미기 판을 닫고(userbuilding.select null · decoration.select null · edit.stop) 「건물 높이」 판.
  고를 때 이 장면 외관 목록이 있으면 WithSavedLook.
- `MapViewModel.BuildingLook.cs` — WithSavedLook · LoadBuildingLooksAsync 가 목록을 받으면 판의 저장된 값(_bhBuilding.Look)을 맞춤(고친 값 _bhLookEdit 는 덮지 않음).
- `Utils/Map3DUserBuilding.cs` — PreviewMessageJson(item) 에 `use`.

## 검증(명령 · 결과)
- 새 시험 + 기존 TwinContract: `dotnet test tests/GMaps.MapEdit.Tests/GMaps.MapEdit.Tests.csproj --filter "FullyQualifiedName~TwinContractScenarioTests"`
  - 고친 뒤: 통과 34 / 실패 0
  - 고치기 전(고친 소스 3개를 `git show HEAD:<경로>` 판으로 잠시 바꿔 같은 명령): **실패 7** / 통과 27 — 실패한 새 시험
    should_keep_drawn_building_and_drop_engine_highlight_when_baked_building_at_same_spot_is_picked ·
    should_close_drawn_building_panel_and_handles_when_other_baked_building_is_picked_and_back ·
    should_close_decoration_panel_when_baked_building_is_picked_and_close_height_panel_when_decoration_is_picked ·
    should_ignore_baked_building_pick_while_drawing_in_3d_and_clear_its_highlight ·
    should_send_look_chosen_in_height_panel_combos_and_keep_it_when_picked_again ·
    should_send_look_chosen_in_drawn_building_panel_in_put_and_preview_and_keep_it_when_picked_again ·
    should_send_use_only_change_of_drawn_building_to_3d_box.
    고치기 전에도 통과한 새 시험 3개(원래 맞던 동작을 끝까지 고정): 외관 저장 거절(422) · 직접 그린 건물 저장 거절(400) · 꾸미기 바닥 · 담장 색.
  - 그 뒤 백업(`$TEMP/fixed`)에서 고친 판으로 되돌림(git diff --stat 로 확인). ★ 함정: Copy-Item 은 옛 수정 시각을 유지해 증분 빌드가 고치기 전 DLL 을 그대로 썼다
    (첫 전수 실행에서 같은 7개 실패) → `touch` 로 시각을 갱신한 뒤 다시 돌림.
- 고친 뒤 전수(`dotnet test tests/<프로젝트>/<프로젝트>.csproj`, 하나씩 차례로):
  GMaps.Ui.Tests 1844/1844 · GMaps.MapEdit.Tests 899/899 · GMaps.Housing.Tests 362/362 · GMaps.MapAux.Tests 122/122 · GMaps.MapLayer.Tests 102/102 ·
  GMaps.MapNav.Tests 70/70 · GMaps.PropertyPanel.Tests 436/436 — 실패 0.
- `dotnet test Ironwall.Dotnet.Libraries.sln`: 11개 테스트 어셈블리 모두 통과(SystemResources 22 · Messages 504 · Monitoring.Models 250 · Watchdog 15 · Accounts.Api 261 ·
  CameraPopup.Host 197 · Reports.Api 3 · Sso 106 · Reports.Ui 377 · Utils 886 · CameraPopup 334), 실패 0.
- 헤디드(창 띄우는) 시험은 하지 않았다 — 판 템플릿은 헤드리스로 실제 테마를 입혀 콤보 · 단추 바인딩까지 만졌다. 실앱 확인은 남은 일.

## 추가 지시(2026-10-22, 작업 중 받음) — 진행
- [x] A. 위로 갈수록 좁게(+) · 넓게(−) `taper_deg`(−30~+30, 1°, 기본 0) — 모델(Map3DUserBuildingItem.TaperDeg · UserBuildingState.TaperDeg) ·
      본문(PutBody(..., taperDeg)) · 해석(ParseItem, 옛 항목 0) · 미리보기(PreviewMessageJson 에 taper_deg) · 판(막대 Map.UserBuilding.Taper · 숫자 칸 TaperDeg · [0] TaperReset · 상태 줄 TaperStatus) ·
      "?" 말풍선(Map.UserBuilding.Panel 「건물 설정」) · 되돌리기 · 다시 하기(UserBuildingCommand 상태에 포함) · 400 거절 = 문구 + 기울기만 저장된 값으로(3D 상자도).
- [x] B. 「6층으로 했는데 그대로 3층」 — 3D 위 판에서 층수 · 높이 · 층고 · 지붕 · 외관 · 기울기를 바꾸는 즉시 userbuilding.preview(PreviewUserBuildingDraft), 같은 내용은 다시 안 보냄,
      [취소] · 다른 건물 · 끝내기 · [2D 에서 윤곽 고치기] = 저장된 모양으로(RevertUserBuildingPreview), 새 윤곽은 미리 만든 uid 로 미리보기 · 저장. 문구 "3D 에 18.0 m(6층)로 바로 보입니다 — [저장] 해야 남습니다".
- [x] B-2. 겹친 원래 지도 건물 숨기기 — 계약에 그 키가 없다(userbuilding.preview 는 상자만). GIS 는 보내지 않고 엔진 후보 B 로 보고(다른 작업이 맡음).
      단, 「지우고 이 자리에 새로 그리기」 로 시작하면 그 원래 건물은 실제로 지운다(숨김 저장).
- [x] C. 지도 건물 「지우기」 — [이 건물 숨기기] → [이 건물 지우기](새 칸 「지우기 · 다시 만들기」), 확인 제목 · 문구 · 상태 글 · "?" 말풍선 「지우기 · 다시 만들기」.
      경로(판 단추 → 확인 → PUT hidden → building.look → 숨긴 건물 목록 → 되살리기)를 실제 판 템플릿 단추로 끝까지 시험. 동작 결함은 찾지 못함(DECISIONS 9).
- [x] C-2. [지우고 이 자리에 새로 그리기] — 장면 파일(GET …/files/scene.json, Map3DClient.GetSceneBuildingsAsync)의 바닥 윤곽 → 숨김 → 3D 위 「건물 만들기」 판(그 윤곽 · 그 높이 · 바로 미리보기). 못 받으면 빈 그리기.
- [x] C-3. 지도 건물 높이 저장 뒤 3D — 이미 바로 반영(building.commit, 뷰어 view.js onBuildingMessage 가 그 높이로 늘임). 기존 시험 BuildingHeightEdgeScenarioTests 가 단정. [지금 다시 만들기] 는 필요 없음.
- [x] D. [여러 개 고르기](3D 에서 차례로 · Ctrl+클릭) → [고른 n개 지우기](확인) · [이 영역 자동 건물 모두 지우기…](확인, 직접 그린 건물 · 이미 지운 것 제외) · 되돌리기 1칸 · 모두 되살리기.
      새 파일 `MapViewModel.BuildingErase.cs`, 확인 콜백 `CallHideMap3DBuildingsProcessMessageModel`. 사각형 끌기는 엔진 메시지가 없어 후보(DECISIONS 11).
- 시험: `tests/GMaps.MapEdit.Tests/BuildingEraseAndTaperScenarioTests.cs`(10개) · `tests/GMaps.Ui.Tests/Map3DTwinContractTests.cs`(2개 추가) · 렌더 시험에 새 AutomationId.
  고친 기존 시험(사양이 바뀐 것만): 단추 글 "이 건물 숨기기" → "이 건물 지우기" 1건, 내가 이번에 만든 새 시험 3건의 미리보기 개수(바로 미리보기로 늘어남).
  ★ 함정: 시험 VM 은 생성자 없이 만들어 Caliburn 알림(IsNotifying)이 꺼져 있다 — 막대 ↔ 숫자 칸 따라오기는 그 시험에서 `IsNotifying = true` 로 켜서 본다.
  ★ 함정: Slider 를 시험에서 움직일 때는 `SetCurrentValue`(손잡이 끌기와 같은 길).
- 증명: PreviewUserBuildingDraft 첫 줄에 잠시 `return` → TwinContract 43개 중 **7개 실패**(바로 미리보기 · 기울기 · 지우고 새로 그리기 · 저장 uid) → 되돌려 43/43 통과.
- 추가 지시 반영 뒤 전수(차례로): GMaps.MapEdit 908/908 · GMaps.Ui 1846/1846 · Housing 362 · MapAux 122 · MapLayer 102 · MapNav 70 · PropertyPanel 436 — 실패 0.
  `dotnet test Ironwall.Dotnet.Libraries.sln` 11개 어셈블리 모두 통과(실패 0).

## 남은 일 · 엔진 후보(이번에 엔진은 건드리지 않음)
- 엔진 후보 D: 고름 메시지(building)에 바닥 윤곽(footprint) — 지금은 장면 파일에서 찾는다.
- 엔진 후보 E: 여러 건물 강조(`building.select {ids:[…]}`) · 3D 사각형 끌기 고르기 메시지.
- 엔진 후보 F: user-buildings 항목 `taper_deg` 로 상자 미리보기 · 굽기(계약 — 다른 작업이 같은 키로 만든다). 지금 뷰어는 키를 무시하고 수직 상자.
- 엔진 후보 A: building 메시지에 외관 키(hidden · wall_color · roof_color · facade)를 최상위로 — 지금은 GIS 가 GET looks 로 메움.
- 엔진 후보 B: 아직 안 구운 직접 그린 상자가 있는 동안 겹친 자동 건물(다시 만들기의 OVERLAP 규칙)을 숨기기 — 큰 자동 건물 안에 든 상자의 색 · 무늬가 보이게(발견 6).
- 엔진 후보 C: 강조를 한 번에 모두 지우는 메시지(필요 없음 — building.select null + decoration.select null 로 충분, 참고만).
- 실앱 확인(헤디드 · 사용자 PC 가 비었을 때 한 번): 직접 그린 굴뚝 고름 → 엔진 굴뚝 윗부분 클릭 → 판 하나 · 강조 하나인지, 외관을 저장한 구운 건물을 다시 골라 판 값이 남는지.
- GIS 후보: 직접 그린 건물 판 콤보를 바꾸는 즉시 3D 상자 값 미리보기(DECISIONS 5 — 이번 범위 밖).
