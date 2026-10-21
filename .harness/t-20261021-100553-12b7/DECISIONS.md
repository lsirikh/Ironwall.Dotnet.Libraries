# DECISIONS — 3D 를 떠나지 않고 편집(edit3d-contract GIS 쪽)

## 1. 「3D 위 판」을 새로 만들지, 기존 판을 3D 위에 그대로 쓸지
- options: (a) 3D 전용 새 판 두 개(직접 그린 건물 · 꾸미기) (b) 기존 「건물 만들기」 · 「꾸미기」 판을 3D 위에서 그대로 열고 3D 일 때만 보이는 칸을 더함
- chosen: (b)
- why: 두 판은 이미 「건물 높이」 판과 같은 캔버스층(3D 위)에 같은 크롬 · 토큰 · "?" 로 떠 있어서, 2D 로 풀리던 원인은 판이 아니라 "고르면 2D 그리기 모드를 켜고 3D 를 접는" 분기였다. 값 칸 · 외관 칸 · 지우기 · 되돌리기를 두 벌로 만들지 않는다.
- effect: 3D 에서만 보이는 공용 칸(`Edit3DSectionTemplate` — 옮기기 ↑↓←→ · 꼭짓점 이전/다음/지우기 · [2D 에서 윤곽 고치기], AutomationId `Map.Edit3D.*`)을 두 판에 넣었다. 따로 떼려면 그 템플릿을 새 판 스타일로 옮기면 된다.

## 2. 네이티브 창 3D(hwnd)에서도 3D 에 머물지
- options: (a) 모든 방식에서 3D 에 머묾 (b) hwnd 는 예전처럼 2D 로
- chosen: (b)
- why: 네이티브 창 3D 는 WPF 판을 덮어 판이 보이지 않는다(「건물 높이」 판도 hwnd 에서는 열지 않는 같은 이유).
- effect: `MapViewModel.Edit3D.ShouldEditIn3D` 한 곳. WebView2 composition(실제 기본)과 시험 호스트("none")는 3D 에 머문다.

## 3. 3D 를 나가면 3D 위에서 연 판을 어떻게 할지
- options: (a) 판을 닫음 (b) 판을 2D 판으로 바꿔 이어 감
- chosen: (a) 닫음 — 2D 로 이어 고치려면 [2D 에서 윤곽 고치기]
- why: 계약이 "[2D 에서 윤곽 고치기] 로만 2D" 라고 정했고, 「나가기」 뒤 2D 지도 표면이 갑자기 입력을 가로채는 것을 막는다.
- effect: `EndEdit3DSessions`(Leave3D 에서 호출). 바꾸려면 거기서 Exit 대신 EditIn2D 와 같은 전환을 하면 된다.

## 4. 사각형 · 원 밖의 형상 도구(삼각형 · 타원)를 3D 에서 어떻게 그릴지
- options: (a) 계약에 없는 mode("triangle" · "ellipse")를 보냄 (b) 다각형(polygon)으로 그림 (c) 3D 에서 단추를 숨김
- chosen: (b)
- why: 계약의 mode 는 point · line · polygon · rect · circle 뿐 — 키 이름 · 값을 바꾸지 말라는 지시. 다각형이면 어떤 모양이든 그릴 수 있다.
- effect: `Map3DEdit.ModeOf(UbTool)`. 엔진이 삼각형 · 타원을 받게 되면 거기서 값만 바꾼다.

## 5. 판 단추(옮기기 · 돌리기 · 꼭짓점)를 어떻게 3D 에 반영할지 — (엔진 dac1e69 뒤 다시 정함)
- options: (a) GIS 가 직접 새 모양을 셈해 PUT(처음 판) (b) 엔진이 덧붙인 edit.key 로 뷰어 키보드와 똑같이 보내고, 저장은 [✓ 확정](Enter) 뒤 뷰어의 edit.commit 으로
- chosen: (b) — `Edit3DKeyCommand` 하나(매개변수 = 키 이름, "Shift+Tab" = 이전 꼭짓점)
- why: 엔진이 방향키를 정북이 아닌 화면 기준으로 정했고 미리보기 → Enter 확정 흐름이라, GIS 가 따로 셈하면 키보드와 결과가 달라진다. 꼭짓점 고름도 뷰어 한 곳에만 있게 된다(처음 판의 "GIS 판에만 보이는 꼭짓점 번호" 문제가 사라짐).
- effect: GIS 쪽 미터 옮기기 · 꼭짓점 계산(Map3DEdit.Move · StepVertex · RemoveVertex 등)과 꼭짓점 번호 줄은 지웠다. 판에 [✓ 확정] · 소품용 [↺↻](뷰어 '[' · ']')을 더했다. 판 안 Esc 도 손잡이가 떠 있으면 edit.key Escape 로 넘긴다.

## 6. 소품 값 칸의 ↺↻(방향 값)
- options: (a) 3D 에서는 값 칸 ↺↻ 도 edit.key 로 (b) 값 칸은 그대로(값 + [적용]), 3D 칸에 따로 ↺↻(edit.key)
- chosen: (b)
- why: 기존 값 칸 동작을 바꾸지 않는다. 3D 칸의 ↺↻ 은 뷰어 키보드와 같다.
- effect: 바꾸려면 `StepDecorationHeadingCommand` 에서 3D 일 때 Edit3DKeyCommand 로 넘기면 된다.

## 9. 엔진의 edit.end(바뀐 것 없이 Esc)를 받았을 때 판을 어떻게 할지
- options: (a) 손잡이 표시만 끔(고름 유지) (b) 고름까지 풀기(꾸미기 = 목록 고름 해제 · 건물 = 빈 윤곽)
- chosen: (b)
- why: 3D 에서 손잡이가 사라졌는데 판만 고른 채로 있으면 판 단추가 대상 없이 남는다. Esc 두 번째 = "편집 끝" 이라는 엔진 뜻과 맞다. edit.stop 은 되보내지 않는다.
- effect: `OnMap3DEditMessage` 의 edit.end 분기. 고름을 남기려면 거기서 SelectDecoration · ResetUserBuildingDraft 를 빼면 된다.

## 7. 기존 시나리오 시험 두 건의 "2D 로 바뀐다" 단정
- options: (a) 그대로 둠(새 동작과 충돌해 실패) (b) 새 계약에 맞춰 "3D 에 머문다" 로 바꿈
- chosen: (b) — `should_open_edit_panel_over_3d_for_user_building_picked_in_3d_and_save_look` · `should_select_decoration_picked_in_3d_in_panel_over_3d`(이름 · 그 한 줄만)
- why: 사용자가 고치라고 한 바로 그 동작(3D 에서 고르면 2D 로 풀림)을 단정하던 줄이다. 나머지 단정은 그대로 둔다.
- effect: 이 두 줄이 "고치기 전 실패 → 고친 뒤 통과" 증명이 된다(옛 코드에서는 Map3DStatusText 가 "3D" 가 아니다).

## 8. 툴바 「건물 그리기」 · 「꾸미기」를 3D 중에 눌렀을 때
- options: (a) 예전처럼 2D 로 바꿔 그리기 (b) 3D 위 판으로 열고 3D 에 머묾
- chosen: (b) — 3D 중이면 보고 있는 영역에서 3D 위 판
- why: 사용자 문제 "새로 놓기 · 그리기도 2D 에서만 된다" 그대로. 계약 2단계의 도구 단추(draw.start)를 쓰려면 판이 3D 에서 열려야 한다.
- effect: `Map3DCompositeScenarioTests.should_refuse_3d_while_drawing_building_started_in_3d…` 의 첫 단정(툴바 → 2D)을 "3D 에 머묾 → [2D 에서 윤곽 고치기] 로 2D 그리기" 로 바꿨다. 그 뒤의 "2D 그리기 중 3D 거절 · 끝낸 뒤 규칙대로만 다시 들어감" 단정은 그대로 남겼다.
