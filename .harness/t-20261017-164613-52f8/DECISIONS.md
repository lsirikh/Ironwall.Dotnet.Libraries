# DECISIONS

## 1. 이미 만든 3D 화면을 다시 펼칠 때 최소 0.6 s 를 어떻게 지킬지
- options: (a) 3D 를 바로 펼치고 안내 패널을 3D 위에 0.6 s 남김 / (b) 2D 위에 안내 패널을 0.6 s 보인 뒤 3D 를 펼침
- chosen: (b) — 패널을 보이는 동안 막대를 0 → 100 % 로 채우고, 다 차면 펼친다. 처음 받는 경우도 ready 가 0.6 s 전에 오면 남은 시간만큼 기다린 뒤 펼친다.
- why: (a) 는 네이티브 창 방식(hwnd 폴백)에서 WebView2 가 패널을 가려 안내가 안 보인다. (b) 는 두 방식 모두에서 반드시 보인다.
- effect: 재진입이 최대 0.6 s 늦어진다. 바꾸려면 `Map3DGuide.MinEnterShow` 값을 줄이거나 `Begin3D` 의 reuse 분기에서 바로 `Reveal3D()` 를 부르면 된다.

## 2. 3D → 2D 안내 시간 · 로그인 직후 안내 시간
- options: 복귀 0.3~0.6 s / 로그인 직후 0.6~2 s
- chosen: 복귀 0.5 s(`Map3DGuide.LeaveShow`), 로그인 직후 1.5 s(`Map3DGuide.AfterLoginShow`)
- why: 명세는 복귀 "짧게", 로그인은 "한 번 안내"만 정함. 로그인 직후에는 사용자가 화면을 막 보기 시작하므로 조금 길게.
- effect: 상수 두 개만 바꾸면 된다.

## 3. "로그인 화면 뒤"를 어떻게 알지
- options: (a) ITokenStorageService.IsAuthenticated == false 를 3D 펼칠 때 기록 + ISessionLifecycle.LoginSucceeded 에서 안내 / (b) 창 활성 · 가림막 상태를 살핌
- chosen: (a) — 둘 다 이미 MapViewModel 이 IoC 로 lazy 해석하는 창구(미등록이면 null → 로그인 게이트 없음으로 보고 안내하지 않음).
- why: 셸의 가림막은 메인 솔루션 몫이라 라이브러리에서 볼 수 없다. 토큰 상태가 로그인 화면 여부와 같은 뜻이다.
- effect: 가림막만 있고 토큰 저장소가 없는 셸에서는 로그인 뒤 안내가 나오지 않는다. `Reveal3D` 의 `_map3DRevealedBehindLogin` 계산만 바꾸면 된다.

## 4. 2D 지도의 3D 영역 표시 방식
- options: (a) GMapMarker 파생 비편집 마커(테두리 + 배지) / (b) 지도 OnRender 에 직접 그리기(MGRS 격자 방식) / (c) 지도 밖 WPF 캔버스
- chosen: (a) `Views/Maps/Map3DAreaMarker.cs` — LocateRingMarker 와 같은 부류(IEditableMarker 아님 → DB 저장 · 선택 · 3D 오버레이 대상 아님).
- why: 배지에 툴팁 · 우클릭 메뉴가 필요하다(OnRender 는 입력을 못 받는다). 마커는 회전 · 팬을 GMap 이 처리한다.
- effect: `SetEditMode` 가 모든 마커 Shape 의 IsHitTestVisible 을 덮어써서, Shape 캔버스는 IsHitTestVisible 을 늘 true 로 강제했다(테두리 자체는 입력 투과).

## 5. 영역 이름 · 지우기 대상
- options: 이름 = 서버 name 필드 / id / "3D 영역 n"
- chosen: 서버 목록에 `name` 문자열이 있으면 그것, 없으면 id. 견본(samples) 장면도 목록에 보이며 지우기는 서버가 405 로 거절하면 이유를 배너로 알린다.
- why: map-3d 서버 소스(C:\workspace_python\map-3d)는 이 세션 권한 밖이라 scenes 형식을 확인할 수 없었다 — 명세(id · bbox · created · counts · kind)대로 관대하게 해석했다(created = ISO 문자열 또는 유닉스 초, 건물 수 = counts.buildings).
- effect: counts 의 키 이름이 다르면 툴팁 건물 수가 "-" 로 나온다 — `Map3DClient.ParseScenes` 한 곳만 고치면 된다.

## 6. "3D 가능" 칩 자리
- options: 줌 컨트롤 안에 넣기(템플릿 수정) / 옆에 별도 테두리
- chosen: MapView.xaml 에 별도 Border(줌 컨트롤 폭 250 · 오른쪽 여백 300 → 오른쪽 여백 558), AutomationId `GMaps.Map3D.Chip`.
- why: 줌 컨트롤 템플릿(MapZoomStyle.xaml)과 x:Name 을 건드리지 않는다.
- effect: 줌 컨트롤 위치가 바뀌면 칩 Margin 도 함께 바꿔야 한다.

## 7. 2 km 넘는 영역을 놓았을 때
- options: (a) 이유만 알리고 그리기 모드 종료 / (b) 이유를 알리고 그리기 모드 유지(다시 그리기)
- chosen: (b) — 배너 "영역이 너무 큽니다(… × …) — 한 변 2 km 이하로 다시 그리세요 (ESC = 취소)". Enter(지금 화면)도 같은 판정.
- why: 사용자가 곧바로 다시 그릴 수 있고 ESC 로 언제든 나갈 수 있다. 서버에 400 을 받으러 가지 않는다.
- effect: `TakeMap3DArea` 한 곳.
