# DECISIONS — 3D 방향 링 · 이동 컨트롤러

## 1. 링을 끌 때 도는 방향(잡아 돌리기 vs 고개 돌리기)
- options: ① 링(N)이 손가락을 따라 돈다 = 지도를 잡아 돌리기(시계로 끌면 방위 감소) ② 끄는 쪽으로 고개를 돌린다(시계로 끌면 방위 증가, N 은 손과 반대로 돈다)
- chosen: ① 잡아 돌리기
- why: 둥근 판을 잡고 돌리는 손잡이는 손을 따라 도는 것이 자연스럽고(기존 2D 나침반 링 회전과 같은 느낌), N 이 손가락 밑을 벗어나지 않는다
- effect: 링 끌기만 해당. 바꾸려면 `Map3DNavMath.BearingDeltaForRing` 의 부호 하나

## 2. 키보드 ←/→ 의 방향
- options: ① 링 끌기와 같은 잡아 돌리기 ② 고개 돌리기(→ = 오른쪽을 본다 = 방위 +)
- chosen: ② 고개 돌리기
- why: 화살표 키는 "오른쪽으로 돌아본다"로 읽히는 것이 일반적(게임 · 지도 앱)
- effect: `Map3DNavMath.KeyNudge` 의 부호

## 3. 지구본(기울기) 손잡이 끌기 방향
- options: ① 위로 끌면 눕히기(수평 쪽) · 아래로 끌면 위에서 보기 ② 반대
- chosen: ① (1 DIU = 0.5°)
- why: 지구본 아랫부분을 끌어내리면 북극이 앞으로 와 위에서 내려다보는 모양 — 손을 아래로 = 위에서 보기
- effect: `Map3DNavMath.PitchDeltaForTilt` 부호 · `TiltDegPerDiu`

## 4. 이동 · 확대 속도 단위
- options: ① GIS 가 m 로 보냄 ② 카메라 거리 비율로 보내고 3D 가 거리를 곱함
- chosen: ② forward · right = 카메라 거리 비율(초당 0.5), zoom = 줌 단계(초당 2), 누르자마자 0.15 초어치 한 걸음
- why: 기획의 "카메라 거리에 비례"를 그대로 — 3D 만 거리를 안다. 시간 기반이라 타이머 간격(40 ms)이 흔들려도 속도 같음(한 번에 0.2 초 상한)
- effect: `Map3DNavMath` 상수 · map-3d `camsync.nudgeView`

## 5. 이동 키보드 폴백이 듣는 초점
- options: ① 3D 화면(WebView2) 초점일 때 W/A/S/D ② 방향 판(링 · 단추)에 초점일 때
- chosen: ② 판에 초점일 때(링을 한 번 누르거나 Tab)
- why: WebView2 에 초점이 있으면 키가 브라우저 프로세스로 가 WPF 창 키 처리로 오지 않는다 — 뷰어(view.js)에 키 처리를 새로 넣어야 하고 기존 3D 키 처리와 겹칠 수 있다. 판 초점이면 충돌이 없다(Ctrl/Alt 조합은 건드리지 않는다)
- effect: 3D 화면 초점에서도 받고 싶으면 view.js 에 keydown → 같은 nudge 를 넣으면 된다

## 6. 장면 범위 한계
- options: ① 한계 없음(OrbitControls 그대로) ② 이동 단추로 옮긴 바라볼 점을 장면 영역(회전 영역 포함) 안으로 잡기
- chosen: ② (이동 단추 · 키 경로에만 — 마우스 끌기 이동은 그대로)
- why: 기획 "장면 범위 밖으로 너무 나가지 않게". 기존 뷰어에는 바라볼 점 한계가 없어 이 경로에서만 `clampToArea` 로 잡는다. 기울기(0~85) · 거리(min/maxDistance)는 기존 한계 그대로
- effect: `camsync.nudgeView` 의 area 인자

## 7. 링 위치
- options: ① 2D 나침반이 저장된 자리(끌어 옮긴 자리) ② 지도 오른쪽 위 고정(나침반 기본 자리)
- chosen: ② 오른쪽 위 고정(여백 16 — 나침반 기본 도킹과 같은 값), 3D 동안 나침반은 숨김
- why: 나침반은 사용자가 옮길 수 있는 계기층(캔버스) 컨트롤이라 그 좌표를 따라가려면 더 많은 배선이 필요. 기획의 "지금 2D 나침반 자리 — 3D 에서는 대체"를 기본 자리로 해석
- effect: MapView.xaml 의 `local:Map3DNavControl` Margin · 정렬
