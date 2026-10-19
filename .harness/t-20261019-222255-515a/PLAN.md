# PLAN — 직접 그린 건물(윤곽 그리기 → 들어올리기 → map-3d 꾸미기)

## 목표
3D 영역 안에서 지도 위에 위에서 본 건물 윤곽을 그리고 높이를 정해 들어올리면,
map-3d 가 그 건물을 출처 "user" 로 우선 굽고 옥상 · 외벽을 사진 기반으로 꾸민다.

## 작업 자리
- map-3d: `artifacts/map3d`(원본 C:\workspace_python\map-3d 의 git clone, vendor 는 원본에서 복사, git 무시) → 끝에 `.harness/t-20261019-222255-515a/map3d.patch`
- 안양 CLI 굽기: `artifacts/anyang/run.py`(캐시는 원본 data/cache 의 복사본, 원본 data/ 는 읽기만) → `artifacts/anyang/report.json`
- 시험 명령: map-3d `cd artifacts/map3d; & C:\workspace_python\map-3d\.venv\Scripts\python.exe -m pytest -q -p no:cacheprovider` · `node tests/userbuilding_check.mjs`
  GIS `dotnet test tests/GMaps.Ui.Tests` · `tests/GMaps.MapEdit.Tests` · `tests/GMaps.PropertyPanel.Tests` · `dotnet test Ironwall.Dotnet.Libraries.sln`

## 진행
- [x] 탐색(GIS 건물 높이 판 · 3D 영역 · ImageCrop · CanEditMap · Undo / map-3d heights · pipeline · server · view.js)
- [x] map-3d: `map3d/userbuildings.py`(저장 · 검증 · 겹친 자동 건물 빼기 · 사양) · pipeline 반영(ORIGIN drawn→user, user_uid, user_buildings 집계, 높이 직접 입력이 붙지 않게) ·
      server REST(GET/PUT/DELETE · DELETE 모두) · jobs 목록 user_buildings 수 · heights.set_height 직접 그린 건물 분기 · 뷰어 `static/userbuilding.js` + view.js 상자 미리보기
- [x] map-3d 시험: `tests/test_userbuildings.py` 12개 + `tests/userbuilding_check.mjs` — 전체 298 passed(기준선 286)
- [x] GIS: `Utils/Map3DUserBuilding.cs`(상태 기계 · 검사 · 본문 · 메시지) · `Map3DClient` 4 메서드 · `Map3DSceneInfo.UserBuildings` · `Map3DAreaItem` 「건물 그리기」 · 「모두 지우기…」 ·
      `MapViewModel.UserBuilding.cs` · `UserBuildingAdorner` · `UserBuildingPanelControl` + `Themes/UserBuildingPanelStyle.xaml` · `UserBuildingCommand`(편집 기록) · MapView 툴바 · 판 · MapsHelp · 3D 진입 막기 · 영역 사라짐
- [x] GIS 시험: GMaps.Ui.Tests `Map3DUserBuildingTests`(20) · MapEdit `UserBuildingScenarioTests`(21)
- [x] 수정 전 실패 → 수정 후 통과 증명: logs/proof-before.txt · proof-after.txt(Esc 끌기 취소 줄)
- [x] GMaps 3종 · sln 전체 통과 — Ui 1633 · MapEdit 688 · PropertyPanel 436 · sln 10 dll 2888(logs/after-* · final-*)
      (sln 첫 재실행에서 Utils.Tests PerfSamplerTests 멈춤 시나리오가 호스트를 죽임 — 손대지 않은 시간 의존 시험, 다시 돌리니 886 통과)
- [x] 안양 CLI 굽기 — anyang-report.json · anyang-run.py(캐시 복사본 · 결과물은 지움)
- [x] map3d.patch 저장(원본 map-3d 에 git apply --check 통과)
- [x] 3D 「건물 높이」 판으로 직접 그린 건물을 올린 뒤 되돌리기 = 앞 높이로 다시 저장(자동으로 X) — MapViewModel.BuildingHeight 한 줄 + 시험

## 남은 것
- 실기(헤디드 · 실제 WebView2 3D 상자 · 실제 마우스) 미확인.

## 발견
- GMaps.PropertyPanel 기준선의 1건 실패는 판 XAML 의 HelpKey 를 먼저 넣고 MapsHelp 항목을 나중에 넣은 사이에 잰 것 — 항목 추가 뒤 확인.
- Copy-Item 으로 소스를 되돌리면 옛 수정 시각이라 빌드가 건너뛴다 — 되돌린 뒤 수정 시각을 갱신해야 한다.
