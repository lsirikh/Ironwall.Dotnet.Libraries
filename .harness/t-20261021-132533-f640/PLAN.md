# PLAN — 시험 창이 사용자 화면 · 포커스를 건드리지 않게

## 목표
시험(저장소 .sln · Devices.Ui · Events.Ui · Accounts.Ui.Tests · tests/*)이 띄우는 WPF 창이 사용자의 키보드 포커스 · 전경 창 · 화면을 건드리지 않게 한다.
헤드리스로 같은 것을 확인할 수 있고 신뢰 근거(전/후 같은 결과 + 돌연변이)를 낼 수 있는 것만 헤드리스로 바꾼다.

## 조사 · 실측으로 알게 된 것
- 38개 파일 대부분은 이미 화면 밖 · ShowActivated=false. 그런데도 포커스를 뺏는 원인 = 시험의 `Focus()` · `Activate()` · 제품 코드의 `Activate()` · 확인 대화상자 `ShowDialog()`.
- 기준선(전경 감시 30ms): 바꾸기 전 실행에서 testhost 가 전경을 91회 가져감(0.2~0.4초 간격 연속 — 사용자 증상과 같음).
- 기준선 Devices: `UnitConsoleNonModalTests` 의 진짜 확인 창 「부대 편제 닫기」가 왼쪽 모니터 [-1920,51] 에 실제로 떠서 멈춤.
- WPF 팝업(HelpTip 말풍선 등)은 화면 밖 창에 딸려도 모니터 안 [-1920,1] 로 붙잡혀 보인다. 최소화 창은 [0,1004] 막대로 보인다. Consoles 의 「서버 창 닫기」는 CenterOwner 설정이 시험의 화면 밖 자리를 덮어써 화면 가운데 떴다.
- HWND 없는 가짜 PresentationSource 에선 키보드 포커스 불가. 별도 데스크톱에선 키보드 포커스 · 활성화 · 입력 라우팅이 되고 사용자 전경은 그대로.
- 숨은 데스크톱 함정 2개: ① TextBox 가 진짜 초점을 받은 채 창이 닫히면 TSF 가 죽은 TextStore 를 불러 호스트가 죽는다 → 닫기 전 Keyboard.ClearFocus. ② 기본 데스크톱 스레드의 UIA 클라이언트는 숨은 데스크톱 창의 WindowPattern 을 못 얻는다 → RunOnHiddenDesktop.
- 같은 PC 의 다른 세션 testhost(EdgeVoice 등)도 전경을 가져간다(감시 기록 other-testhost 17회) — 이 저장소 밖.
- UnitConsoleNonModalTests 는 단독 19/19 통과, 여러 반과 함께 돌 때만 시간 초과(기준선 10건, 지금 2건) — 포커스와 무관한 기존 문제.

## 방법(구현됨)
- 공용 도우미 `tests/Shared/QuietWindow.cs`(8개 프로젝트에 링크):
  - `Show(w)`: 화면 밖 · 비활성 · 작업표시줄 없음 · Topmost 끔 + 그 스레드에 스레드 한정 WH_CBT 훅(활성화 거절 · 팝업 빈 창 영역).
  - `UseHiddenDesktop()`: 키보드 포커스가 시험 대상인 반의 STA 스레드 첫 줄 — 숨은 데스크톱, 거기선 활성화 허용.
  - `RunOnHiddenDesktop(...)`: 숨은 데스크톱 창을 읽는 UIA 클라이언트 스레드.
- Show() 56곳 → 도우미(37개 파일), Activate() 5곳 제거, 헤드리스 1개 반(DialogTextKoreanWrapTests).

## 진행
- [x] 조사 · 실험 · 도우미
- [x] 기준선 실행(전경 감시)
- [x] 교체 · 링크 · 숨은 데스크톱 · TSF/UIA 함정 해결
- [x] 헤드리스 전환 + 돌연변이 근거(DialogTextKoreanWrap 확정, DragHitTest · ConsoleRailSelectionBar 는 근거 불충분 → 창 유지)
- [ ] 최종 실행(.sln + tests/GMaps.* + 바꾼 프로젝트, 전경 감시) — $TEMP/final.sh
- [ ] 보고

## 남은 일
최종 실행 결과 확인 · 보고.
