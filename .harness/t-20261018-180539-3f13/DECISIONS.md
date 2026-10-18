# DECISIONS — t-20261018-180539-3f13

## 1. 3D 에서 글자 이름표를 어떻게 줄일까(minZoom 규칙 vs 화면 거리 겹침 정리)
- options: ① 이름표만 지금처럼 2D 최소 표시 줌(minZoom) 규칙 ② 화면 거리 기준으로 겹치는 이름표를 숨기는 새 정리
- chosen: ① — 몸체(모형 · 상태 점 · 아이콘 · 선 · 펜스 · 도형 · 이미지)는 visible=false 만 따르고, 이름표만 isShown(item, zoom)
- why: 2D 와 같은 규칙이라 사용자가 이미 아는 동작이고, 새 겹침 계산(매 프레임 화면 투영 · 정렬)이 필요 없다. 목표가 허용한 두 안 중 코드가 가장 적다.
- effect: 영역 전체 시점(줌 ≈13)에서는 장비 모형 · 점 · 선은 다 보이고 이름표는 대부분 숨는다. 바꾸려면 map-3d `overlay-store.js` 의 `shown3D` 의 label 식만 바꾸면 된다(겹침 정리를 넣으려면 overlay3d.js `refreshVisibility`/`tick` 에서 label.visible 을 다시 정한다).

## 2. 「나가기」를 왼쪽 위 어디에 둘까
- options: ① 좌표 칩 왼쪽(왼쪽 위 맨 앞, 3D 동안 좌표 칩이 오른쪽으로 밀림) ② 좌표 칩 오른쪽(좌표 칩은 그대로) ③ 고정 여백 숫자(Margin)로 좌표 칩 옆
- chosen: ① — 좌표 칩과 같은 가로 StackPanel(Margin 50,5 = 좌측 메뉴 오른쪽) 맨 앞
- why: 사용자 요구 "나가기 버튼도 왼쪽으로" — 왼쪽 위 맨 앞이 '뒤로/나가기' 자리로 가장 찾기 쉽다. StackPanel 이라 좌표 칩 폭(WGS · MGRS · UTM)이 바뀌어도 겹치지 않고, 줄 높이(≈40)가 바람 위젯 기본 자리(y=56) 위라 겹치지 않는다.
- effect: 3D 동안만 좌표 칩이 「나가기」 폭만큼 오른쪽으로 밀린다. 순서를 바꾸려면 MapView.xaml 의 그 StackPanel 안 두 자식 순서만 바꾸면 된다.

## 3. 「나가기」 디자인
- options: ① 기존 MaterialDesignOutlinedButton(테두리 버튼) 유지 ② 패널 주 단추(PanelPrimaryButtonStyle, 채운 Primary) + 키 힌트 칩
- chosen: ② — MapFloatingPanelStyle 판 + PanelPrimaryButtonStyle + PackIcon ArrowLeft + "나가기" + "Esc" 키 칩 + "?" 말풍선
- why: 3D 위에서 유일한 행동 단추라 눈에 띄어야 하고, 패널들의 주 단추와 같은 토큰(PrimaryBrush · OnPrimaryFixedBrush · PrimaryHoverBrush)이라 라이트/다크가 자동으로 맞는다. 왼쪽 자리라 화살표를 왼쪽(뒤로) 모양으로.
- effect: 스타일만 바꾸면 되돌릴 수 있다(MapView.xaml "GMaps.Map3D.Exit" 블록).

## 4. 나가기 말고 다른 이유로 3D 를 접을 때 돌아갈 2D 자리
- options: ① 나가기 · ESC 만 들어오기 직전 자리, 나머지(입력 모드 · 설정 끔 · 로딩 실패 · 영역 지움 · 호스트 대체)는 3D 마지막 카메라 ② 모두 들어오기 직전 자리
- chosen: ② (단, 이벤트 이동 · 「영역 보기」 재진입은 원래대로 2D 를 건드리지 않고, 네이티브 창 3D 에서 조립 카드를 열 때는 그 마커를 보던 3D 마지막 카메라)
- why: 3D 는 이제 늘 영역 전체 시점(줌 ≈13)으로 열려 3D 마지막 카메라는 대개 너무 멀다 — 편집 모드 등으로 2D 에 돌아올 때 들어오기 전 자리가 자연스럽다.
- effect: 바꾸려면 MapViewModel.Map3D.cs 의 각 Leave3D 호출 인자(Map3DExitCamera ↔ _map3DLastCamera)만 바꾼다.

## 5. 끼워 넣기(embed) 3D 휠 축소 한계
- options: ① 그대로(최대 거리 밖에서도 화면 가운데 기준으로 계속 물러남 — 예전엔 GIS 가 줌으로 2D 로 나가서 필요했다) ② 최대 거리에서 멈춤
- chosen: ②
- why: 3D 가 줌으로 나가지 않게 되어, 그대로 두면 끝없이 물러나 장면이 점이 된다.
- effect: map-3d view.js 휠 처리 한 줄. 최대 거리(controls.maxDistance = size × 10)는 그대로.
