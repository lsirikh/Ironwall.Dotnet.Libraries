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

## 추가 지시(형상 도구 · 「건물 만들기」 전용 판)
- [x] 품질 지적 중복 → `GMapControls/HeightPanelControlBase.cs` 공용 바탕(건물 높이 · 건물 만들기 판 둘 다 상속)
- [x] 상태 기계: 도구 4종(사각형 끌기/두 클릭 · Shift 정사각형 / 원 끌기/두 클릭 · 꼭짓점 수 8~128 기본 32 / 타원 끌기 · 세 클릭 / 다각형 · 처음 점 붙는 거리 10 px · Shift 직각 맞춤),
      완성 뒤 공통 편집(꼭짓점 · 선분 가운데 · 지우기 · 몸통 끌기 · 회전 손잡이)
- [x] 판: 도구 아이콘 줄(VectorRectangle · VectorCircle · VectorEllipse · VectorPolygon — dll 메타데이터로 실존 확인, 고른 도구 = 아래 막대 모양) · R/C/E/P · 지붕 모양 · 외벽 성격 · 원 꼭짓점 · 「적용」「취소」「지우기」
      참조 스타일 키: PanelSecondaryButtonStyle · PanelPrimaryButtonStyle · PanelCloseButtonStyle · PanelHeaderHelpDisc · BuildingHeightSectionLabel · BuildingHeightInputBox · BuildingHeightStepButton · BuildingHeightHandleThumb · 토큰 PrimaryBrush · SurfaceBrush · BorderBrush · DividerBrush · TextMutedBrush · TextPrimaryBrush · StatusCriticalBrush · OnPrimaryFixedBrush
- [x] map-3d: roof(flat · gable · cone · dome) · use(auto · commercial · industrial · residential) 저장 · 검증 · 굽기(원형 + 돔 = 탱크 규칙)
- [x] 시험: 순수 32 · VM 시나리오(형상 · 지붕 · 키 · 회전 지도 · 이동/회전) · pytest 13
- [x] 판에서 도구 쓰는 법 줄은 뺐다 — HelpContractTests(설명 문구는 "?" 로) 규약. 쓰는 법은 단추 풍선 · 판 "?"(MapsHelp)
- [x] 원본 map-3d 가 다른 작업 커밋 2개(25cb147)로 앞서 나가 `artifacts/map3d` 를 최신 원본으로 다시 복제해 변경을 다시 얹음 — map3d.patch 는 25cb147 기준, git apply --check 통과
- [x] 최종: map-3d 300 passed · node ok · GMaps.Ui 1645 · MapEdit 694 · PropertyPanel 436 · sln 종료 코드 0(logs/r3-* · r4-sln)
      (r3 sln 에서 CameraPopup 의 50 ms 시간 예산 시험 1건이 부하로 넘침 — 손대지 않은 시험, r4 재실행 334 통과)
- 주의: 이번 차례에 PowerShell 에서 .NET 작업 폴더가 달라 네 파일을 빈 파일로 쓴 사고가 있었고, 마지막 커밋본(git show HEAD)에서 되살린 뒤 수정을 다시 적용했다(시험 수 · 결과 동일).

## 남은 것
- (있으면 좋음) 지도의 기존 선 · 도형 마커를 윤곽으로 쓰기 — 미룸(DECISIONS 11).
- 실기(헤디드 · 실제 WebView2 3D 상자 · 실제 마우스) 미확인.

## 발견
- GMaps.PropertyPanel 기준선의 1건 실패는 판 XAML 의 HelpKey 를 먼저 넣고 MapsHelp 항목을 나중에 넣은 사이에 잰 것 — 항목 추가 뒤 확인.
- Copy-Item 으로 소스를 되돌리면 옛 수정 시각이라 빌드가 건너뛴다 — 되돌린 뒤 수정 시각을 갱신해야 한다.
