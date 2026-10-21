# DECISIONS

## 1. 포커스를 뺏지 않게 하는 수단 — 창마다 WS_EX_NOACTIVATE vs 시험 스레드 한정 CBT 훅
- options: (a) 시험 창마다 WS_EX_NOACTIVATE · (b) 시험 STA 스레드에 스레드 한정 WH_CBT 훅을 걸어 HCBT_ACTIVATE 를 거절 · (c) ShowActivated=false 등 속성만
- chosen: (b) — `tests/Shared/QuietWindow.Show` 가 창을 띄울 때 그 스레드에 훅을 건다
- why: 실측에서 포커스를 뺏는 것은 시험 창 자체보다 `Focus()` · 제품 코드의 `Activate()` · 확인 대화상자 `ShowDialog()` 였다. (a) 는 시험이 만든 창에만 붙어 제품 코드가 여는 창을 못 막는다. (c) 는 이미 대부분 적용돼 있었는데도 기준선에서 전경을 91회 가져갔다. 실험에서 (b) 는 Focus() · ShowDialog 모두 활성화 0회.
- effect: 이 스레드의 창은 키보드 포커스를 못 받는다(논리 포커스만). 되돌리려면 도우미의 GuardThread 호출 한 줄을 빼면 된다.

## 2. 키보드 포커스가 시험 대상인 반 — 화면 밖 활성 창 vs 숨은 데스크톱
- options: (a) 그 창만 활성화 허락(화면 밖) · (b) 그 STA 스레드를 보이지 않는 데스크톱(CreateDesktop + SetThreadDesktop)으로 옮김 · (c) 헤드리스
- chosen: (b) `QuietWindow.UseHiddenDesktop()` — 스레드 첫 줄에서 부르고, 그 스레드는 활성화를 막지 않는다
- why: (a) 는 실측에서 여전히 전경을 3회 가져갔다(시험 프로세스가 전경 권한을 가진 순간). (c) 는 HWND 없는 가짜 PresentationSource 에서 키보드 포커스가 아예 잡히지 않아 같은 것을 확인할 수 없다. (b) 는 실험 · 실행에서 키보드 포커스 · 활성화 · 입력 라우팅이 그대로 되고 사용자 데스크톱 전경 변화 0.
- effect: 숨은 데스크톱 반 = Utils 4(CaptureDragDataGridSelection · DialogPlacement · ConsoleRailRefusedSwitch · HelpTip) · Devices 4(RealWindowR28 · WiringFenceView · WiringFenceHeightView · UnitConsoleNonModal) · Events 1(MappingBoardRealList) · AppHost(Accounts.Ui.ViewTests + Consoles.ViewTests 공용 UI 스레드). 되돌리려면 각 반의 UseHiddenDesktop 한 줄을 빼면 된다.

## 3. WindowStyle 을 None/ToolWindow 로 강제할지
- options: 강제 · 그대로 둠
- chosen: 그대로 둠(화면 밖 · 작업표시줄 없음 · 활성화 차단으로 충분)
- why: 틀 두께가 바뀌면 클라이언트 크기를 재는 시험(크롬 · 크기 시험)의 조건이 바뀐다 — 시험 의미를 바꾸지 않는다.
- effect: 도우미 Prepare 에 한 줄 넣으면 강제할 수 있다.

## 4. 시험의 `Focus()` · `Activate()` 호출
- options: 둘 다 지움 · 둘 다 둠 · Activate 만 지움
- chosen: `window.Activate()` 5곳은 지우고, 요소 `Focus()` 는 둔다
- why: 훅 아래에서 `Focus()` 는 창을 활성화하지 못하고(무해) 논리 포커스(FocusManager)는 그대로 잡아 준다 — 일부 시험(RealWindowR28Round8 의 RealKey 등)이 그 논리 포커스를 읽는다. `Activate()` 는 시험 대상이 아니다(숨은 데스크톱 반에서는 Focus() 가 필요한 활성화를 스스로 한다).
- effect: 필요하면 개별 Focus() 를 나중에 지울 수 있다.

## 5. 화면 밖에 둘 수 없는 WPF 팝업(말풍선 · 드롭다운 · 툴팁)
- options: 그대로 둠(보임) · 팝업 시험을 지움 · 팝업 창에 빈 창 영역(SetWindowRgn)
- chosen: 같은 CBT 훅에서 WS_POPUP+WS_EX_NOACTIVATE 창이 만들어질 때 빈 창 영역을 건다
- why: WPF 팝업은 어디에 두든 모니터 안으로 붙잡혀 [-1920,1] 에 실제로 보였다. 창 영역은 OS 화면 표시만 자르고 IsOpen · 배치 · 자동화는 그대로다(실험으로 확인). 시험을 지우는 것은 금지.
- effect: 훅의 HCBT_CREATEWND 분기를 빼면 원래대로.

## 6. 헤드리스 전환 범위
- options: 후보 3개 반 모두 전환 · 근거가 서는 것만
- chosen: DialogTextKoreanWrapTests 만 전환. DragHitTestTests · ConsoleRailSelectionBarTests 는 창(비활성)으로 남김
- why: 창 판 vs 헤드리스 판을 정상 + 돌연변이 2세트로 비교 — DialogTextKoreanWrap 3개는 모든 실행에서 결과가 같았다. 나머지 둘은 헤드리스에서 정상 코드로도 실패(행 드롭존 4개 · 레일 항목 컨테이너 null) — 창 수명 주기(Loaded · 컨테이너 생성)에 기대므로 같은 것을 확인하지 못한다 → 지시대로 바꾸지 않음.
- effect: 다른 반도 같은 방법($TEMP 의 mutrun 방식)으로 근거를 만든 뒤 옮길 수 있다.

## 7. 확인 창이 화면 가운데 뜨던 시험의 화면 밖 설정을 고칠지
- options: 가짜 창 관리자에 화면 밖 설정을 덧씌움 · 숨은 데스크톱으로 해결
- chosen: 숨은 데스크톱(AppHost · UnitConsoleNonModalTests). 가짜 창 관리자 코드는 그대로
- why: Consoles.ViewTests 의 `OffscreenWindowManager` 는 EnsureWindow 에서 자리를 정하지만 그 뒤 설정(CenterOwner)이 덮어써 「서버 창 닫기」가 화면 가운데 떴고, UnitConsoleNonModalTests 는 최소화 창 막대가 [0,1004] 에 떴다 — 최소화 막대는 자리로 못 숨긴다. 숨은 데스크톱이면 둘 다 보이지 않고 코드도 한 줄.
- effect: 숨은 데스크톱 줄을 빼면 그 창들이 다시 보인다.
