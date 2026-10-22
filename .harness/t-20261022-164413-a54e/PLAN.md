# t-20261022-164413-a54e — 「건물 만들기 / 고치기」 · 「건물 높이」 판: 스크롤 · 높이 바꾸면 다른 속성 바뀜 · 여러 개 고르기 · 속성 복제 · 복사/붙여넣기

## 목표(사용자 원문 2026-10-22)
1. 판이 너무 길다 → 판 안 스크롤
2. 높이를 고치면 다른 속성이 바뀐다
3. 건물 여러 개 고르기 · 속성 복제 · 건물 복사/붙여넣기(Ctrl+C/V, 붙여넣은 건물은 겹치지 않게 옆에) — 3D 판에서

## 코드 위치(새로 온 사람용)
- 판 공용 바탕: `Ironwall.Dotnet.Libraries.GMaps.Ui/GMapControls/HeightPanelControlBase.cs` (「건물 높이」 · 「건물 만들기」 · 「꾸미기」 판이 상속, 폭 320 · 머리 띠 38 끌기)
- 판 템플릿: `Themes/UserBuildingPanelStyle.xaml` · `Themes/BuildingHeightPanelStyle.xaml` · `Themes/DecorationPanelStyle.xaml`
- 판 자리: `Views/Maps/MapView.xaml` 의 Canvas(ContentPresenter Canvas.Left=620 · Top=90) — 머리 띠 끌기는 `Helpers/OverlayHeaderDrag.cs`
- 직접 그린 건물 VM: `ViewModels/Maps/MapViewModel.UserBuilding.cs`(2D · 3D 공통) · `MapViewModel.Edit3D.cs`(3D 위 판 · draw.* · edit.*)
- 「건물 높이」 판 VM: `MapViewModel.BuildingHeight.cs` · `MapViewModel.BuildingLook.cs` · `MapViewModel.BuildingErase.cs`
- 3D 메시지 해석: `Utils/Map3DMath.cs`(ParseMessage) · 건물 줄 해석 `Utils/Map3DBuilding.cs`(Parse) · 직접 그린 건물 `Utils/Map3DUserBuilding.cs`
- 3D 엔진(별도 저장소, 여기서 고치지 않음): `C:\workspace_python\map-3d` — `map3d/heights.py`(높이 저장 응답 = scene.json 건물 줄 그대로) · `map3d/looks.py`(외관은 줄의 `look:{…}` 안에) · `map3d/static/view.js`(userbuilding.pick · select)

## 진행
- [x] 원인 조사(코드 · 엔진 소스 증거)
- [x] 1 판 스크롤 — `HeightPanelControlBase.MeasureOverride/FitHeight`(지도 Canvas 의 판 자리 Canvas.Top 아래 남은 높이 − 8, 최소 200, Canvas SizeChanged · 머리 띠 끌기 때 다시 잼) + 세 템플릿(건물 만들기 · 건물 높이 · 꾸미기) 본문 `ScrollViewer`(`Map.UserBuilding.Scroll` · `Map.BuildingHeight.Scroll` · `Map.Decoration.Scroll`, 둘째 행 `*`)
- [x] 2-A 「건물 높이」 판 높이 저장 뒤 외관 「자동」 — `Map3DBuilding.Parse` 가 `look:{…}` 안을 읽는다
- [x] 2-B 직접 그린 건물 판 층고 = 저장된 높이 ÷ 층수(`ResetUserBuildingDraft`)
- [x] 3a 3D 여러 개 고르기 — `Map3DMath.ParseMessage` userbuilding.pick 의 ctrl · shift, `OnMap3DUserBuildingPicked`(Ctrl · Shift · 호스트 키), `PostUserBuildingSelection3D`(userbuilding.select uids), building.none = 고름 풀기
- [x] 3b · 3c 복사 · 붙여넣기 — 새 `MapViewModel.UserBuildingClipboard.cs`(명령 4 · `HandleUserBuildingClipboardKey`), `Map3DUserBuilding.PasteOutlines` · 글, 판 아래 단추 4개(`Map.UserBuilding.Copy` · `.Paste` · `.CopyStyle` · `.PasteStyle`), 키 = `HandleUserBuildingKey`(판 · 2D) + `OnMapPreviewKeyDownForGroup`(3D 화면), 여러 건물 저장 공용 `SaveUserBuildingBatchAsync`(함께 고치기도 이것으로)
- [x] 도움말(`Map.UserBuilding.Panel` 「여러 개 고르기 · 복사 · 붙여넣기」) · CHANGELOG
- [x] 시험 작성 · 고치기 전 실패 증명 · 전체 시험(아래 검증)

## 발견
- (결함 2 원인 A) 엔진 `PUT …/buildings/{bid}/height` 응답 `building` = scene.json 건물 줄 그대로(heights.py `_set_height` 가 `b` 를 돌려준다).
  그 줄의 외관은 `look:{uid, hidden, wall_color, roof_color, facade}` 안에 있다(looks.py `_set_row_look`). GIS `Map3DBuilding.Parse` 는 바깥 키(wall_color …)만 읽어
  → `OnBuildingHeightSaved` 가 `_bhBuilding = saved.Pick` 으로 판의 외관을 「자동」으로 바꿨다. 그 뒤 지붕 색만 바꿔 [외관 적용]하면 외벽 무늬 · 색까지 자동으로 지워진다.
- (결함 2 원인 B) 직접 그린 건물 판은 층고를 늘 3.0 으로 시작(`StartUserBuildingDraw`)하고 고칠 건물을 열 때도 그 건물의 높이 ÷ 층수로 맞추지 않는다 →
  「건물 높이」 판에서 층고 4 m 로 20 m · 5층을 넣은 건물을 열면 7층으로 보이고, 이름만 바꿔 저장해도 floors=7 로 저장된다.
- 3D 의 아직 안 구운 상자 클릭(userbuilding.pick)에는 엔진이 ctrl · shift 를 붙이지 않고, userbuilding.select 는 uid 하나만 강조한다(view.js 527 · userbuilding.js 137).
  GIS 는 Ctrl+클릭을 호스트 키로만 알아채고, 넣은 뒤에도 누른 건물 하나만 강조를 보내 3D 에서는 하나만 골라진 것처럼 보였다.

## 결정
DECISIONS.md 참고.

- (결함 2 — 실측 경로 재확인) 직접 그린 건물 판의 높이 칸(`UserBuildingHeightText` → `SetUserBuildingHeight`) · 손잡이(`UserBuildingPanelHost.DragHeight`) · ±1층은
  `_ubHeight` 만 바꾸고 다른 칸은 건드리지 않는다(사용자 실측과 같음). 다른 칸이 바뀌는 길은 위 A · B 둘.
- (함께 본 것 · 좌표) GIS 는 엔진 `cursor` 의 lon · lat 을 그대로 보인다(`Map3DMath.ParseMessage` cursor 분기 · `SetMap3DCursor` — 변환 없음).
  엔진은 `lonLatOf(sceneInfo.origin, x, z)`(view.js 422 · camsync.js 26). 보인 값 37.39xx, 126.96xx 는 개발 엔진 data 의 다른 job `20261019-183014-f366a1` origin(37.3941, 126.9670)과 거의 같다
  → 뷰어의 sceneInfo(origin)가 보고 있는 장면과 다른 job 것으로 보인다. 설치본 엔진 data 는 못 봐 확정 못 함 — 고치지 않고 보고(DECISIONS 9).
- (함께 본 것 · 그리기 도구 클릭) 3D 그리기 모드(draw.start)에서는 클릭을 엔진이 점으로 먹어 GIS 에 고름이 오지 않는다 → 엔진 쪽 계약 제안(DECISIONS 10).

## 엔진(map-3d)에 필요한 계약 — 이 저장소에서는 고치지 않음
1. `userbuilding.pick` 에 `ctrl` · `shift`(참 · 거짓) — `building` 메시지와 같은 키. 없으면 GIS 는 호스트 키 상태로 본다(WebView2 에 초점이 있으면 못 볼 수 있다).
2. `userbuilding.select {uid, uids?}` — `uids`(문자열 배열)가 있으면 그 상자 모두 강조, 없으면 지금처럼 `uid` 하나. Ctrl+클릭한 상자로 강조를 스스로 옮기지 않아도 GIS 가 곧 uids 로 다시 보낸다.
3. 그리기 모드 첫 점이 직접 그린 상자(또는 구운 직접 그린 건물) 위면 점 대신 `userbuilding.pick` / `building` 을 보내기(지금은 새 사각형 첫 점이 된다).
4. cursor 좌표 — 지금 불러온 장면 job 과 sceneInfo.origin 이 같은지 확인(장면을 바꿀 때 sceneInfo 를 그 job 것으로).

## 검증(명령 · 결과)
- 기준선(고치기 전): `dotnet test tests\GMaps.MapEdit.Tests\GMaps.MapEdit.Tests.csproj` → 통과 927 / 실패 0.
- 새 시나리오 시험 `tests/GMaps.MapEdit.Tests/BuildingEditorScenarioTests.cs`(11, TwinContractScenarioTests partial — 가짜 map-3d 엔진의 높이 응답은 실제 엔진처럼 `look` 을 싣게 고침)
  - 고치기 전 증명: 제품 소스 13개를 `git show HEAD:…` 로 잠시 되돌리고 같은 필터로 실행 → **11건 실패**
    (판 스크롤 4: `Map.*.Scroll 없음`/판 아래 끝 > 지도 · 외관 ("auto","") ≠ ("glass","#336699") · 층고 ("3.0","7") ≠ ("4.0","5") ·
     3D Ctrl+클릭이 고치기로 열림 · Ctrl+C/V · Ctrl+Shift+C/V 처리 안 됨 · 묶음 붙여넣기 0개) → 고친 소스 복구 후 **11건 통과**.
- `dotnet test tests\GMaps.MapEdit.Tests\GMaps.MapEdit.Tests.csproj` → 통과 938 / 실패 0(927 + 11).
- `dotnet test tests\GMaps.Ui.Tests\GMaps.Ui.Tests.csproj` → 통과 1868 / 실패 0(새 `Map3DBuildingEditorTests` 7 포함).
- `dotnet test Ironwall.Dotnet.Libraries.sln --filter "FullyQualifiedName!~Ironwall.Dotnet.Libraries.Sounds&FullyQualifiedName!~Ironwall.Dotnet.Libraries.Db."`
  → 11개 시험 어셈블리 중 9개 실패 0. **실패 3건은 이 작업과 무관한 렌더 시험**: Utils.Tests `FrameBorderTests` 2(픽셀이 #FF000000) ·
  CameraPopup.Host.Tests `EventWindowRenderTests` 1(PNG "looks empty"). 그 두 프로젝트는 변경 0(`git diff --stat HEAD -- …` 빈 결과)이고 단독으로 다시 돌려도 같다.
  `quser` = 세션 `Disc`(원격 끊김) — 끊긴 세션에서는 WPF 비트맵 렌더가 검게 나온다(메모리 uia_needs_connected_session 과 같은 부류). 세션이 붙으면 다시 돌려 볼 것.
- 도움말 수정 뒤 `dotnet test tests\GMaps.MapEdit.Tests\…` 다시 → 통과 938 / 실패 0.
- 턴 2(하네스 검사 실패 3건 = 같은 렌더 시험): `FrameBorderTests` 를 읽어 보니 실제 창을 띄우고(`Show(root)`) 화면 픽셀을 읽는 시험이다 —
  세션이 끊기면(`Disc`) 화면이 그려지지 않아 검정 픽셀이 된다. 지금 `quser` = `rdp-tcp#0 Active`(다시 붙음). 이 작업은 창을 띄우는 시험을 직접 돌리지 않는 규칙이라
  다시 돌리지 않고, 세션이 붙은 상태의 하네스 검사에 맡긴다. 제품 코드 변경은 없다(이 시험들은 Utils · CameraPopup.Host 만 쓰고 두 프로젝트는 변경 0).
- 창을 띄우는 시험 · 헤디드 시험은 하지 않았다(사용자 PC 사용 중). 실제 앱(설치본)에서 눌러 보지 않았다.

## 남은 일
- 실제 앱에서 확인: 1920×1040 · 1366×768 에서 판 스크롤, 3D Ctrl+클릭 강조(엔진이 uids 를 알아야 여러 강조 — 위 계약 2), 3D 화면 Ctrl+C/V.
- 엔진 계약 1~4 는 map-3d 담당이 반영해야 한다(설치본 엔진 5cd00ec 는 더 뒤처짐).
- 좌표 어긋남(DECISIONS 9) — 설치본에서 job · scene.json origin 대조로 원인 확정.
