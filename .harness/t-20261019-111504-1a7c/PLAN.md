# PLAN — 3D 건물 높이 직접 입력(보안 구역 층수) (t-20261019-111504-1a7c)

## 목표
사용자 요청(2026-10-19): 보안 구역이라 공개 건물 자료(층수)를 못 구할 수 있다 → 운영자가 GIS 3D 에서 건물을 골라 높이(층수)를 직접 넣는다.
끝의 정의 = 작업 지시 1~7(3D 건물 고르기 · 건물 높이 판 · map-3d 저장/유지 · 되돌리기 · 3D 목록 · 도움말 · 시험).

## 설계
- map-3d 사본: `claude-skill-harness-map3d/`(작업) · `claude-skill-harness-map3d-base/`(기준, .gitignore `claude-skill-harness*/`).
  python 은 원본 venv(`C:\workspace_python\map-3d\.venv\Scripts\python.exe`)를 사본 폴더에서 돌리면 사본 map3d 를 읽는다(확인함).
  - `map3d/heights.py` — 덮어쓰기 저장(data/overrides/area-<bbox 해시>.json, 항목 = uid · center · area_m2 · footprint · height · floors),
    IoU(0.4) 다시 붙이기(`apply_to_specs`), 지금 장면 반영(`set_height` — GLB 그 건물 정점만 `stretch_y`, scene.json 줄 · 집계), `reset_all`, 잠금(동시 요청).
  - pipeline: building_overrides 뒤에 직접 입력 적용(height_source=manual), 건물 줄에 `footprint`(다시 붙이는 자리) · `manual` · `auto`(되돌릴 값), scene.json `height_overrides`{applied, unmatched[uid]}.
  - server: `PUT/DELETE /api/jobs/{id}/buildings/{bid}/height`, `GET/DELETE /api/jobs/{id}/height-overrides`, jobs 목록에 `manual_heights` · `manual_unmatched`.
  - 뷰어: `static/buildingedit.js`(순수 — isClick 데드존 · wallTop · stretchPositions · buildingMessage · parseHostBuilding),
    view.js — 건물 클릭(마커가 먼저) → `{type:"building",…}`, 윤곽 = EdgesGeometry 검은 실선 + 흰 점선(TOP_LAYER), `building.preview/select/commit`.
- GIS(GMaps.Ui)
  - 순수: `Utils/Map3DBuilding.cs`(메시지 해석 · 보내는 메시지 · 판 규칙 — 층수↔높이 · 층고 · 범위 · ±층 · 키 · 끌기 · 출처 쉬운 말).
  - `Map3DMath.ParseMessage` "building" → `Map3DMessage.Building`.
  - `Map3DClient` — SetBuildingHeightAsync · ResetBuildingHeightAsync · ResetAllBuildingHeightsAsync, 장면 목록 manual_heights/unmatched.
  - 판: `GMapControls/BuildingHeightPanelControl.cs` + `Themes/BuildingHeightPanelStyle.xaml`(GroupRotationPanelStyle 크롬 답습).
  - VM: `ViewModels/Maps/MapViewModel.BuildingHeight.cs`.
  - 3D 목록: Map3DAreaItem 「직접 입력한 건물 높이 n개」 · 「모두 자동으로…」(확인 팝업).
  - 도움말 MapsHelp "Map.BuildingHeight.Panel".

## 진행
- [x] map-3d 사본 · 기준선 pytest 169 통과
- [x] heights.py · pipeline · jobs · server
- [x] tests/test_heights.py(7) — 저장/되돌리기 · 입력 검증/경로 · 동시 요청 · 다시 굽기 다시 붙이기 · 못 붙임 · IoU
- [x] buildingedit.js · view.js · tests/building_check.mjs
- [x] GIS 순수 규칙(Utils/Map3DBuilding.cs) + 메시지 해석(Map3DMath "building") + 클라이언트(Map3DClient 3개 · 목록 manual_heights/unmatched)
- [x] 판 컨트롤(GMapControls/BuildingHeightPanelControl.cs — Thumb 끌기 · 키 · Esc) · 스타일(Themes/BuildingHeightPanelStyle.xaml) · VM(MapViewModel.BuildingHeight.cs) · MapView.xaml · Generic.xaml
- [x] 3D 목록(Map3DAreaItem 「직접 입력한 건물 높이 n개」 · 「… 모두 자동으로…」 확인 팝업 CallResetMap3DHeightsProcessMessageModel) · 못 붙임 알림(영역마다 수가 바뀔 때 한 번)
- [x] 도움말 MapsHelp "Map.BuildingHeight.Panel"(쓰는 법 · 층수/층고 · 저장/다시 만들기 · 왜 직접 넣나)
- [x] GIS 시험 — tests/GMaps.Ui.Tests/Map3DBuildingTests.cs(100) · tests/GMaps.MapEdit.Tests/BuildingHeightScenarioTests.cs(13)
- [x] map3d.patch — `claude-skill-harness-map3d-tools/make_patch.py`(difflib, 줄 끝 그대로; `git -c` 가 막혀서), 원본 map-3d 에 `git apply --check` 통과
- [x] 전체 pytest 177 통과 · node 시험 전부 · dotnet test Ironwall.Dotnet.Libraries.sln 통과

## 참조한 스타일 키(판 크롬)
`fb:FrameBorder`(Surface · Border · 반지름 10 · 그림자) · 머리 띠 `PrimaryBrush` 38 + `OnPrimaryFixedBrush` 글 · `PanelHeaderHelpDisc` + `fb:HelpTip` ·
`PanelCloseButtonStyle` · `PanelPrimaryButtonStyle` · `PanelSecondaryButtonStyle` · 입력칸 = GroupRotationInputBox 값(`SurfaceSunkenBrush` · `BorderBrush` · `PrimaryBrush` 캐럿) ·
섹션 라벨 `TextMutedBrush` · 오류 `StatusCriticalBrush` · 권한 없음 `StatusWarningBrush` · 구분선 `DividerBrush` — 전부 DynamicResource(라이트/다크 시험).

## 수정 전 실패 → 수정 후 통과(증명)
- 기준 사본(`claude-skill-harness-map3d-base`)으로 새 시험을 돌림: pytest `tests/test_heights.py` → `ImportError: cannot import name 'heights' from 'map3d'`, node `building_check.mjs` → `ERR_MODULE_NOT_FOUND`.
  작업 사본: test_heights 8 통과 · building_check "building ok".
- GIS: 시나리오 시험이 잡은 실제 결함 — 저장 실패 뒤 성공해도 오류 줄이 남음 → 저장 시작 때 지우게 고침(should_keep_preview_and_show_reason_when_server_refuses_or_is_off).

## 발견
- GIS 의 Ctrl+Z(UndoCommand)는 편집 모드 ∧ 지도 편집 권한에서만, 3D 는 편집 모드에서 열리지 않는다 → 판 안 「되돌리기」로 대체(DECISIONS 2).
- scene.json 건물 줄에는 원래 바닥 윤곽이 없었다(center · area_m2 만) → 새 굽기부터 footprint 를 남기고, 옛 장면은 중심 · 면적 원으로 겹침을 잰다.
- 건물 top_z 는 난간 · 옥탑(roof_details) 전 값이라 GLB 최대 높이와 다를 수 있다(시험은 정점을 직접 비교).
- 원본 map-3d 는 대부분 CRLF 지만 map3d/jobs.py 는 LF — 패치는 파일마다 원래 줄 끝을 지킨다.
- 처음엔 `_bhUndo = new()` 를 3D 나가기 경로에서 써서 기존 Map3DEntryScenarioTests 34개가 깨졌다(생성자 없는 VM → null) → 지연 초기화(BhUndo · BhFloorHeight · Map3DUnmatchedNotified)로 고쳐 389 통과.
- 생성자 없이 만든 VM(시험 장치)은 Caliburn `IsNotifying` 이 꺼져 있어 바인딩 칸 글이 바뀌지 않는다 — 템플릿 시험에서만 켠다(제품 무관).
- map-3d 원본 venv 를 사본 폴더에서 돌리면 사본 map3d 를 읽는다(pyproject pythonpath ".").

## 남은 일
- 실기 확인(창 · WebView2 시험 금지 — 실기 미확인)
- map-3d 원본에 map3d.patch 적용(코디네이터/사용자 몫)
