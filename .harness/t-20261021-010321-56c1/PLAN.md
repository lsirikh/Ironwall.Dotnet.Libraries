# PLAN — 3D 지도 꾸미기(GIS 쪽) · twin-contract.md B · C · D + A 의 GIS 호출

## 목표
1. 3D 에서 이상하게 생긴 건물을 숨길 수 있게(A 호출 + D 판 단추 · 숨긴 목록 · 되살리기).
2. 직접 그린 건물 외관(wall_color · roof_color · facade · hip/shed 지붕) + 3D 에서 고르면 2D 고치기 판(B).
3. 꾸미기 항목(나무 · 바닥 · 소품 · 담장) 판 · 2D 표시 · 3D 메시지(C).
엔진(C:\workspace_python\map-3d)은 손대지 않음 — 시험은 가짜 엔진(HttpMessageHandler).

## 설계
- Utils/Map3DLook.cs — 색 · facade 값 목록 · look PUT 본문 · 응답/목록 해석 · building.look 메시지.
- Utils/Map3DDecoration.cs — 꾸미기 항목 해석 · 본문 · 범위 검사 · 메시지(decorations.reload · decoration.select).
- Map3DClient — looks 4개 · decorations 4개 호출(404 = 옛 엔진 → null/false).
- Map3DBuildingPick 에 UserUid · Hidden · WallColor · RoofColor · Facade(init).
- Map3DMessage 에 UserUid(userbuilding.pick) · DecorationUid(decoration.pick).
- MapViewModel.BuildingHeight — 외관 칸 + [외관 적용] + [이 건물 숨기기]/[이 건물 지우기] + 숨긴 건물 목록.
- MapViewModel.UserBuilding — 외관 칸, 3D pick → 2D 고치기 판.
- MapViewModel.Decoration.cs + DecorationAdorner + DecorationPanelControl/Style + MapView.xaml 툴바 · 판 + 도움말.
- 편집 기록: DecorationCommand(앞/뒤 항목, null = 없음, 설명 앞머리 "꾸미기").

## 진행
- [x] A 클라이언트(PutBuildingLookAsync · ListLooksAsync · ResetAllLooksAsync) · Utils/Map3DLook.cs
- [x] D 건물 높이 판 외관 · 숨기기(확인) · 숨긴 목록 · 되살리기 · 모두 되살리기 · 판 안 되돌리기 (MapViewModel.BuildingLook.cs, BuildingHeightPanelStyle.xaml)
- [x] B 직접 그린 건물 외관(facade · 색 · hip/shed) · 3D pick(userbuilding.pick / user_uid) → 2D 고치기 판 · userbuilding.select · 미리보기 키
- [x] C 꾸미기(Utils/Map3DDecoration.cs · 클라이언트 4개 · MapViewModel.Decoration.cs · DecorationAdorner · DecorationPanelControl/Style · 툴바 Map.Decoration.Start · 도움말 · DecorationCommand)
- [x] 시험: tests/GMaps.Ui.Tests/Map3DTwinContractTests.cs(16) · tests/GMaps.MapEdit.Tests/TwinContractScenarioTests.cs(10)
- [x] GMaps 시험 프로젝트 전체(Ui 1740 · MapEdit 860 · MapAux 80 · MapNav 70 · MapLayer 102 · PropertyPanel 436 · Housing 353) · dotnet test Ironwall.Dotnet.Libraries.sln 통과

## 남은 일
- 실제 엔진(map-3d 새 판)과 붙인 실기 확인 — 엔진은 다른 작업이 같은 계약으로 고치는 중이라 이번엔 가짜 엔진으로만 시험했다.

## 발견
- tests/GMaps.* 는 .sln 밖 — 직접 돌린다. GMaps.Ui.Tests 는 Utils 파일을 링크해 컴파일 → 새 Utils 파일은 csproj 에 Compile Include 추가 필요.
- `new JArray(someJArray)` 는 감싸지 않고 복사한다 — GeoJSON 다각형 고리는 `(object)` 로 넘겨야 한다.
- JObject.Merge 는 기본으로 null 값을 버린다 — 색 null(자동)은 Map3DLook.KeepNulls 로 합친다.
- 기존 시험이 옛 PutUserBuildingAsync 본문 키를 고정 → 외관 없는 호출은 옛 본문 그대로(결정 9).
