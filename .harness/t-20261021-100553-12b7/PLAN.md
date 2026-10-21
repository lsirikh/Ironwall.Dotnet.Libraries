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

## 2차(엔진 dac1e69 맞춤, 사용자 추가 지시)
- [x] draw.done rect/circle = polygon 처럼(이미 그렇게 처리 — 시험 추가)
- [x] edit.commit Polygon 닫지 않은 고리 · heading 은 소품만(시험 추가)
- [x] 판 단추 → edit.key(화면 기준 ↑↓←→ · ↺↻ · Tab/Shift+Tab · Delete · ✓ 확정=Enter), GIS 쪽 옮기기 · 꼭짓점 계산 삭제
- [x] edit.end 받기 → 손잡이 · 고름 끄기(edit.stop 되보내지 않음), 판 안 Esc 는 edit.key Escape 로 넘김
- [x] 도움말 갱신, Map3DEditTests 26 · TwinContract+Composite 53 통과
- [x] GMaps.Ui 전수 1767 통과 · sln 검사 = CameraPopup 생존 시험 4건만 실패(이 작업과 무관한 프로세스 재시작 타이밍) → 그 두 클래스만 다시 돌려 14/14 통과

## 남은 일
- 실기(WebView2 + 새 map-3d) 확인은 엔진 판이 GIS 개발 환경에 들어온 뒤.

## 결정 · 발견
- DECISIONS.md 참고.
