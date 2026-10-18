# DECISIONS — t-20261018-211956-86f9

## 1. 3D 마우스가 장면 밖(하늘 · 빈 곳)을 가리킬 때 좌표 칩
- options: ① 마지막 값 유지 ② 빈칸
- chosen: ① — 뷰어는 땅에 맞지 않으면 cursor 를 보내지 않고, GIS 는 마지막 값을 둔다
- why: 2D 도 마우스가 지도 밖으로 나가면 마지막 값을 그대로 둔다(같은 동작). 빈칸 처리용 메시지 · 상태가 필요 없다.
- effect: 바꾸려면 map-3d view.js `sendCursor` 에서 맞지 않을 때 `{type:"cursor"}`(lon 없음)를 보내고, GIS 에서 그 경우 칩을 비우면 된다.

## 2. 커서 던지기 간격
- options: ① 33 ms(≈30 Hz) ② 50 ms(20 Hz) ③ 기존 단독 보기의 120 ms
- chosen: ② 50 ms — 기다리는 동안 온 마지막 위치를 그 차례에 고른다(멈춘 자리도 보냄), 끄는(드래그) 중엔 고르지 않음, 휠 뒤에는 한 번 고름
- why: 목표 상한(30 Hz 이하) 안에서, 장면 전체 광선 고르기(가속 구조 없음)가 저사양 · 원격에서 프레임을 밀었던 기록(view.js 주석)이 있어 조금 여유를 뒀다.
- effect: camsync.js `CURSOR_MIN_MS` 한 값.

## 3. 축척 막대의 m/px 를 누가 셈하나
- options: ① 뷰어가 카메라 바뀔 때 m/px 를 새 메시지로 보냄 ② GIS 가 기존 camera 메시지(zoom · lat)로 셈
- chosen: ② — `Map3DMath.MetersPerPixel(cam.Zoom, cam.Lat)` → `ScaleBar`(1 · 2 · 3 · 5 × 10ⁿ, 막대 ≤ 100 DIP)
- why: 3D 가 보내는 zoom 은 이미 화면 가운데(바라보는 점)까지 거리 · 시야각 · 화면 높이로 셈한 값(camsync.mapFromCamera)이라 그 역산이 곧 화면 가운데 가로 1 px 의 실제 거리다(비스듬해도 가운데 줄 기준). 새 메시지 없이 옛 3D 서버와도 맞는다.
- effect: 뷰어 쪽 m/px 계산 · 시험은 따로 없다(camsync 의 mapFromCamera 왕복 시험이 그 값을 지킨다). 다른 기준(막대 자리 근처 땅)이 필요하면 뷰어가 그 줄로 레이캐스트해 보내는 ①로 바꾼다.

## 4. 3D 축소 한계(최대 거리) 셈법
- options: ① 지금 방위의 영역 전체 거리 × 1.3(돌릴 때마다 한계가 바뀜) ② 방위 0/45/90/135 중 가장 먼 영역 전체 거리 × 1.3(고정) ③ 영역을 감싸는 구 기준
- chosen: ② — 처음 기울기(60°) · 여백 없이 꽉 차는 거리, 화면 크기가 바뀔 때만 다시 셈. 끼워 넣기(GIS)에만 적용(단독 보기는 그대로 size × 4)
- why: ①은 돌리기만 해도 카메라가 끌려 들어와 어색하고, ③은 꽉 차는 거리보다 한참 멀다. ②는 어느 방향으로 돌려도 영역 전체가 들어오고 처음 시점(여백 8 %)보다 약 1.2~1.4 배만 더 빠진다.
- effect: camsync.js `maxOrbitDistance`(factor 1.3) · view.js `updateMaxDistance`. 최소 거리(15 m)는 그대로(장비를 가까이 볼 수 있는 값).

## 5. 3D 위 로그인 직후 안내 글의 줌 표기
- options: ① 그대로("줌 16.5 밑 · ESC = 2D" — 3D 는 이제 줌으로 나가지 않아 틀린 안내) ② 새 단계 글 "shown"("나가기 · ESC = 2D")
- chosen: ② — 기존 "login" 글 · 함수는 시험이 지키므로 두고, 3D 위 안내만 새 글로
- why: 목표 4(3D 화면 위 줌 표기 제거). 3D 영역 목록 · 배지의 "3D 로 바뀌는 줌"(2D 진입 기준)은 그대로.
- effect: Map3DGuide.StageText "shown" 한 줄 · VM 호출 한 곳.

## 6. 고도 표시 자리
- options: ① WGS-84 줄 끝에 붙임(열 폭 고정이라 칩이 넓어짐) ② 칩 아래 "고도:" 줄(값이 있을 때만)
- chosen: ② — MapCoordinateControl 에 `AltitudeText` 의존 속성(더하기), null 이면 줄을 접는다(2D)
- why: MGRS · UTM 줄과 같은 꼴이라 디자인이 맞고, 2D 는 바뀌지 않는다.
- effect: Themes/MapCoordinateStyle.xaml 의 고도 Grid 하나.
