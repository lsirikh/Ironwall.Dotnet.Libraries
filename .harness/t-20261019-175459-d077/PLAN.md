# PLAN — 3D 나간 뒤 지도 좌 · 우 클릭 · 휠 무반응(2026-10-19 17:41 사용자 보고)

## 목표
3D 에서 나온 뒤 2D 지도 입력(휠 · 드래그 · 클릭)이 반드시 살아 있게 한다. 원인 규명(증거) → 고치기 → 창 없는 시험.

## 진행
- [x] 원인 규명(앱 로그 · 코드 · WebView2CompositionControl 디컴파일 · 메인 앱 OpenEventPanel · 장치 실험) — 아래 "발견"
- [x] 기준선(작업 전): GMaps.Ui 1497 · MapEdit 451 · MapNav 48 모두 통과
- [x] 고치기(MapViewModel.Map3D.cs · MapViewModel.BuildingHeight.cs · GMapCustomControl.cs 진단 메서드)
- [x] 시험 20개(tests/GMaps.MapEdit.Tests/Map3DExitInputScenarioTests.cs) — 수정 전 실패 → 수정 후 통과 증명
- [ ] sln 시험 + GMaps 3개 직접 통과(결과는 이 파일 맨 아래)

## 발견(증거)
1. 앱 로그 17:41:10 「2D 복귀(exit-button)」 뒤 17:46 종료까지 지도 입력 흔적 0. 이 지도에서 휠은 어느 줌에서든 GMapCustomControl.OnMouseWheel 이
   반 단계(StepDigitalZoom → "[DigitalZoom] level=" 로그)나 정수 줌("줌 변경됨")을 남긴다 — 나간 뒤 그 줄이 하나도 없다
   ⇒ 휠 처리기 자체가 불리지 않았다(처리기 안 조기 return 은 linePress/imageDrag 뿐, 둘 다 3D 와 무관). 좌 · 우 클릭 · 휠이 함께 죽은 것도 같은 결론:
   지도까지 WPF 입력이 라우팅되지 않았다(누름 판정 · 사용 가능 · 덮는 층 · 캡처 중 하나).
2. 캡처 제외: 관찰 Win32 캡처 hwnd=0 — WPF Mouse.Capture 는 SetCapture 를 동반하고, 창이 캡처를 잃으면 WPF 도 캡처를 푼다.
3. 관찰 "지도 가운데 FromPoint = GMap 지도" 는 누름 판정 후보를 배제하지 못한다 — WPF UIA FromPoint 는 자동화 트리의
   경계 사각형으로 찾는다(IsHitTestVisible · 어도너 무관). 지도 IsHitTestVisible=false 여도, 피어 없는 어도너가 덮어도 같은 답이 나온다.
4. 메인 앱 ShellViewModel.OpenEventPanel 은 IsEventPanelOpen 만 바꾼다(셸 그리드 · 오른쪽 250 px 서랍 스토리보드). 지도 VM 에 보내는 메시지 없음,
   지도 조상 IsHitTestVisible/IsEnabled 를 바꾸는 코드 없음(메인 앱 전수 grep) ⇒ 메인 앱 변경 불필요(host.patch 없음).
5. GMaps 안에서 지도 IsHitTestVisible 을 바꾸는 곳은 Reveal3D(false) · Leave3D(true) 둘뿐. WebView2CompositionControl(1.0.2957.106 디컴파일)은
   마우스 캡처 · 전역 입력 후킹 없음(마우스 → SendMouseInput 전달만).
6. 실제로 "2D 인데 지도 입력이 죽는" 경로를 장치로 재현: Leave3D 는 SetMap3DState(Off) 직후 「건물 높이」 판 닫기(3D 로 PostWebMessageAsJson)를
   부르는데, 이것이 예외를 던지면(WebView2 dispose · 렌더 프로세스 사망 등) 그 자리에서 끊겨 IsHitTestVisible=true · Park 에 닿지 못한다
   → 상태 "2D" · 「나가기」 없음 · 지도 누름 판정 꺼짐 = 관찰과 같은 모양. (단, 17:41 로그에는 「2D 복귀」 줄이 찍혔으므로 그 회차는 이 경로가 아니다.)
7. 17:41 회차의 정확한 막힘 주체는 증거로 확정하지 못했다 — 창 없는 시험에서 WPF 누름 판정 자체가 돌지 않아(InputHitTest · VisualTreeHelper.HitTest 모두 null)
   덮는 층은 재현할 수 없다. 그래서 나간 직후 · 한가해진 뒤 두 번 「[Map3D] 2D 입력 상태(...)」 진단 줄을 남긴다:
   hit · enabled · visible · 막는조상(첫 하나) · 가운데(창 루트에서 지도 가운데를 InputHitTest 한 요소 — "지도" 가 아니면 덮은 요소 이름#AutomationId) ·
   캡처 · 포커스 · 3D펼침 · 지도상태(일찍 끝내는 상태 목록). 다음 재발 때 이 한 줄로 주체가 갈린다.

## 고친 것
- Reveal3D: `IsHitTestVisible=false` → `HoldMap2DInput()` — 막기 전 로컬 값을 한 번만 적고 막는다(두 번 펼쳐도 처음 값).
- Leave3D: 상태를 내린 직후 **먼저** `Map3DHost.Park()` + `RestoreMap2DInput()`(적은 값으로 되돌림 — 3D 중 다른 코드가 바꿨어도, 3D 화면 안에 남은 마우스 캡처 해제).
  옛 위치의 Park · IsHitTestVisible=true 두 줄 삭제. 끝에 `LogMap2DInputState(cause)` 진단 줄.
- CloseBuildingHeightPanel: 3D 로 보내기 실패를 격리(Error 로그) — 판은 늘 닫히고 나머지 복귀가 끊기지 않는다.
- GMapCustomControl.InputGateText()(internal) — 진단 줄용 조기 return 상태 목록.

## 시험(창 · WebView2 없음, 실제 VM 경로)
Map3DExitInputScenarioTests 20개: 사용자 흐름(3D 회전 · 줌 · 레이어 판 → 나가기) · 3D 중 레이어 판 · 카메라 상자 · 빈 곳 클릭(조립 카드 닫기 경로) ·
건물 높이 판(열림 · 취소) · ★3D 로 보내기 예외 중 나가기 · 나가는 길 6가지(단추 · 편집 모드(묶음 회전 판 포함) · 로딩 실패 · 설정 끔 · 네이티브 창 전환 · 이벤트 이동) ·
ESC(WPF 3D 는 안 나감) · 다른 코드가 바꿈/두 번 펼침 · 5회 반복 · 진단 줄(now/idle · 막는 조상 이름).
매 시나리오 나간 뒤: 조건(IsHitTestVisible · IsEnabled · 조상 · 3D 접힘 · 조기 return 상태 없음) + 휠(반 단계 오르내림) · 드래그(위치 바뀜) · 클릭(OnMapClicked 1회).
- 수정 전(Map3D.cs · BuildingHeight.cs 를 HEAD 로 잠시 되돌림) → 3개 실패: ★예외 시나리오 "2D 인데 지도 누름 판정이 꺼진 채 — 예외=CoreWebView2 members cannot be accessed…",
  진단 줄 2개(줄 없음). 수정 후 20/20 통과.

## 남은 일 / 실기
- 실기 미확인. 재발하면 앱 로그의 「[Map3D] 2D 입력 상태」 두 줄(now · idle)로 막는 주체를 확정할 것.
