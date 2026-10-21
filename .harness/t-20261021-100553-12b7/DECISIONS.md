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

## 5. 판 단추(옮기기 · 꼭짓점)를 어떻게 3D 에 반영할지 — 계약에 "단추 → 뷰어" 메시지가 없음
- options: (a) 새 메시지를 만들어 뷰어가 옮기게 함 (b) GIS 가 직접 새 모양을 셈해 PUT 저장 → userbuilding.preview / decorations.reload + edit.start 로 손잡이를 다시 세움
- chosen: (b)
- why: 계약 메시지를 늘리지 않고, 저장 · 편집 기록 · 실패 처리를 edit.commit 과 같은 길로 탄다(단추 한 번 = 편집 기록 1단위).
- effect: 판 단추로 고른 꼭짓점 번호는 GIS 판에만 보인다(뷰어의 Tab 고름과 따로). 뷰어와 맞추려면 edit.start 에 선택 키를 더하는 계약 변경이 필요 — 보고에 적음.

## 6. 소품 돌리기 단추(↺↻)
- options: (a) 3D 에서는 누를 때마다 바로 저장 (b) 지금처럼 값 칸을 바꾸고 [적용]
- chosen: (b) 그대로
- why: 이미 판에 있는 동작을 바꾸지 않는다. 3D 의 원호 손잡이 · 대괄호 키는 edit.commit(heading_deg)로 바로 저장된다.
- effect: 바꾸려면 `StepDecorationHeadingCommand` 에서 3D 일 때 CommitEdit3DAsync 를 부르면 된다.

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
