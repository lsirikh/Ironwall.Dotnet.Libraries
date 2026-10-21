# PLAN — 3D 를 떠나지 않고 편집(edit3d-contract 1·2·3단계, GIS 쪽)

## 목표
3D 에서 직접 그린 건물 · 꾸미기 항목을 고르면 2D 로 풀리던 것을 고친다. 3D 위 판에서 값 고치기(1단계), draw.* 로 놓기 · 그리기(2단계),
edit.* 로 손잡이 옮기기 · 꼭짓점 · 판 단추 폴백 · 실패 시 edit.revert · 되돌리기(3단계). 엔진(map-3d)은 손대지 않는다.

## 설계
- 판은 새로 만들지 않고 기존 「건물 만들기」 · 「꾸미기」 판을 3D 위에 그대로 띄운다(이미 3D 위 캔버스층 · 「건물 높이」 판과 같은 크롬).
  세션에 3D 표시(`_ubIn3D` · `_decoIn3D`)를 두고, 그동안은 2D 지도 표면(Adorner)을 붙이지 않고 3D 를 막지 않는다(IsMap3DBlockedByInputMode).
- 네이티브 창 3D(hwnd)는 WPF 판을 덮으므로 예전대로 2D 로 간다.
- 메시지 해석 · 생성 · 이동/꼭짓점 계산 = `Utils/Map3DEdit.cs`(순수 함수). VM 배선 = `ViewModels/Maps/MapViewModel.Edit3D.cs`.
- 판 공용 칸(옮기기 ↑↓←→ · 꼭짓점 이전/다음/지우기 · [2D 에서 윤곽 고치기]) = UserBuildingPanelStyle.xaml 의 `Edit3DSectionTemplate`(AutomationId Map.Edit3D.*).
- 3D 를 나가면(Leave3D) 3D 세션은 끝난다. [2D 에서 윤곽 고치기] 만 세션을 2D 로 옮긴다.

## 진행
- [x] Map3DEdit 순수 함수 + Map3DMessage 확장(draw.point/done/end · edit.commit)
- [x] VM: 3D 세션 · draw/edit 배선 · 판 단추 · 다시 하기(직접 그린 건물) · 툴바도 3D 중이면 3D 위 판
- [x] XAML 판 칸 · 도움말
- [x] 시험: GMaps.Ui.Tests(Map3DEditTests 25) · GMaps.MapEdit.Tests(Edit3DScenarioTests 8) · 옛 "2D 로 바뀐다" 단정 3건 갱신
- [x] tests/GMaps.* 7개 프로젝트 전수(창 띄우는 2 클래스 제외) 통과
- [x] dotnet test sln(하네스 검사) — 전부 통과

## 남은 일 · 엔진 쪽에 알릴 것
- 판 단추로 고른 꼭짓점은 GIS 판에만 보인다(뷰어 Tab 고름과 따로) — 맞추려면 edit.start 에 선택 키 추가 계약 필요.
- 실기(WebView2 + 새 map-3d) 확인은 엔진 작업이 들어온 뒤.

## 결정 · 발견
- DECISIONS.md 참고.
