# DECISIONS — t-20261018-151659-8d03

## 1. 영역 전체 시점을 누가 셈하나
- options: GIS 가 카메라(줌)를 셈해 camera 메시지로 보냄 / 뷰어가 화면 비율 · 시야각으로 셈하는 새 메시지 `{type:"home", bearing, pitch, animate}`
- chosen: 뷰어 셈(home 메시지) — camsync.fitAreaView, view.js homeView
- why: 목표가 뷰어 homeView · camsync 순수 함수를 지정했고, 시야각 · 실제 화면 크기는 뷰어만 정확히 안다(접혀 있으면 펼친 뒤 셈)
- effect: 「처음 시점」 단추 · 가운데 더블클릭도 같은 시점이 된다. GIS 는 혹시 옛 뷰어일 때를 위해 눕히기 camera 도 먼저 보낸다(옛 뷰어 = 눕히기만). 바꾸려면 Reveal3D 의 HomeMessageJson 한 줄.

## 2. 처음 시점의 기울기 · 방위 · 여백
- options: 기울기 45/55/60° · 방위 북 고정/2D 방위 · 여백 5~15 %
- chosen: 60°(줌 진입 때 눕히는 값 DefaultPitchDeg 와 같게) · 들어오기 전 2D 방위 · 여백 8 %(fitAreaView 기본값)
- why: 두 진입 방식의 느낌을 맞추고, 사용자가 보던 방향을 유지
- effect: HomeMessageJson 인자 · fitAreaView 기본값(margin)만 바꾸면 된다

## 3. 「나가기」 단추 위치와 네이티브 창 3D
- options: 지도 오른쪽 위 / 위 가운데(조준 배너 · 측정 띠 자리) / 아래 가운데(토스트 자리) · 네이티브 창 3D 를 위해 Popup(별도 창)
- chosen: 오른쪽 위 지도 HUD(MapFloatingPanelStyle + 외곽선 단추), Popup 은 쓰지 않음
- why: 위 · 아래 가운데는 다른 배너와 겹치고, 기본 3D 호스트(WPF 안)에선 일반 HUD 가 3D 위에 보인다. Popup 은 창 이동 · 겹침 문제가 새로 생긴다
- effect: CompositionControl 을 못 써 네이티브 창으로 대체된 PC 에선 단추가 3D 에 가려지고 ESC 만 남는다. MapView.xaml 의 "GMaps.Map3D.Exit" Border 의 정렬 · Margin 으로 옮긴다

## 4. 「영역 보기」 들어오기 전 자리로 돌아가는 경우
- options: 모든 2D 복귀 / 사용자가 나갈 때(단추 · ESC)만
- chosen: 단추 · ESC(ExitMap3D)만. 편집 모드 진입 · 카드 열기 · 영역 지우기 · 설정 끄기 · 로딩 실패는 지금처럼 3D 마지막 카메라
- why: 마커를 눌러 카드를 여는 등은 지금 보는 자리가 이어져야 한다 — 다른 곳으로 튀면 마커가 화면 밖으로 나간다
- effect: Map3DMath.ExitCamera 를 Leave3D 다른 호출부에도 쓰면 넓힐 수 있다

## 5. 2D 이동 뒤 3D 유지 판정(Sync3DAfter2DMove)
- options: 영역 보기 중이면 줌 조건도 빼기 / 그대로
- chosen: 그대로(장면 밖 · 2D 줌 < 이탈 줌이면 2D 로)
- why: 목표 3 은 3D 카메라 줌 이야기이고, 이벤트 이동은 이동 목표 줌을 2D 에 직접 세운다 — 범위 밖 변경을 피함
- effect: Map3DStaysAt 에 IsMap3DAreaView 를 넘기면 바꿀 수 있다
