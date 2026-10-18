# DECISIONS

## 1. 구역 선 위 "탐지 센서 구간"을 어디서 알아내나
- options: ① `ActiveSensorDeviceIds`(모델 값) ② 소속 센서를 장비 번호 순서로 선을 등분해 추정 ③ 이벤트가 살아 있는 소속 장비의 실제 자리(지도 심볼, 없으면 장비 좌표)를 선에 투영
- chosen: ③ — 선에서 50 m(`MapFlyToMath.MaxSensorOffsetM`) 밖이거나 좌표가 없으면 구간 없음 → 구역 경계 가운데 + 구역이 보이는 줌
- why: ① 은 아무 곳에서도 쓰지 않는 값(Phase 2 미구현)이고 3D 철망 컨트롤은 그것을 노드 순번으로 읽어 장비 Id 를 넣으면 2D 표시가 틀어진다. ② 는 실제 설치 간격을 모르는 추정이라 긴 펜스에서 엉뚱한 곳으로 갈 수 있다. ③ 은 아는 자리만 쓰고, 모르면 구역 전체를 보여 준다.
- effect: FenceGroup 전용 센서(심볼 · 좌표 없음)는 구역 전체로 이동한다. 센서 순번 → 선 위 자리 매핑이 생기면 `ActiveZoneSensorPoints`(MapViewModel.EventFlyTo.cs) 한 곳만 바꾸면 이동 · 3D activeSection 둘 다 따라간다.

## 2. 3D 표시 중 목표가 장면 밖일 때 2D 로 돌아오는 시점
- options: ① 지금처럼 3D 아래에서 2D 비행 → 끝나고 2D 로(3D 가 1 s 멈춘 뒤 목표에서 2D 가 보임) ② 먼저 2D 로 돌아와 2D 비행
- chosen: ② — `Leave3D(null, "event-flyto")` 뒤 2D FlyTo. 비행 중에는 3D 게이트가 출발 자리에서 다시 3D 로 들어가지 않게 보류(`_flyAnimation == null` 조건)하고 끝난 자리에서 판정
- why: 목표 문구("2D 로 돌아와 2D FlyTo")와 같고, 사용자가 이동을 눈으로 따라갈 수 있다.
- effect: 2D 비행(이벤트 이동 · 0 ms 이동은 영향 없음) 중에는 3D 로딩이 시작되지 않고 로딩 중이던 것은 취소된다. 되돌리려면 FlyToEventEntry 의 `Leave3DThenTwoD` 분기와 EvaluateMap3DGate 의 `_flyAnimation == null` 을 빼면 ① 로 돌아간다.

## 3. 구역 전체가 목표일 때 줌
- options: ① 설정 최대 줌(장비와 같음) ② min(설정 최대 줌, 구역이 화면 80 % 안에 드는 줌) — 이미 더 확대돼 있어도 줄임
- chosen: ②
- why: 목표 문구 "구역이 보이는 줌". 구역이 커서 줌이 3D 이탈 줌보다 낮으면 3D 를 접고 2D 로 보인다(기존 규칙).
- effect: 큰 구역 탐지는 3D 에서 2D 로 돌아올 수 있다. `ResolveZoneZoom` 한 곳에서 바꾼다.
