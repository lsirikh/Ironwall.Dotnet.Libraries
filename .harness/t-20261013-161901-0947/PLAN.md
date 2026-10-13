# PLAN — 실창 3회차 잔여 제품 결함 (t-20261013-161901-0947)

## 목표
실창 3회차(round3-results.txt)에서 남은 MNT-R28-211 · 216 · WIR-R27-135 · SC-FEN24-004 · MNT-R28-610 을, 실창과 같은 입력 길을 타는 헤드리스 시험(빨강 → 초록)으로 고친다.
시험: `Ironwall.Dotnet.Libraries.Devices.Ui/Tests/RealInputRound3Tests.cs`(7건) — `dotnet test Ironwall.Dotnet.Libraries.Devices.Ui/Ironwall.Dotnet.Libraries.Devices.Ui.csproj --filter "FullyQualifiedName~RealInputRound3Tests"`.

## 진행
- [x] 원인 규명(증거)
- [x] 216 · 135 Shift 빠짐 — `FenceKeyModifiers` 에 NumLock 가짜 Shift 떼기 판정(`NoteKeyUp` · `IsNumpadFakeShift`) · MountBayCanvas/FenceCanvas `OnPreviewKeyUp`
- [x] 211 누른 채 Esc — `MountBayCanvas.IsDragging` 이 누름(데드존 안) · 손잡이 돌리기도 포함, Esc 가 그때 원위치(`PressAt` 로 누름 분리)
- [x] 004 한 점의 두 대 함께 끌기 — `FenceLayoutMath.PlanGridMove` 가 같은 점에서 떠난 센서끼리 같은 점으로 가는 것을 허용
- [x] 610 칩 겹침 — 붙은(프리셋) 칩끼리 세로로 겹치는 무리만 따로 나란히 벌림(`FenceCanvas.OverlappingRows`)
- [x] FenceCanvas 키 판정의 포커스 요소 = `e.OriginalSource`(실창에서는 포커스 칩과 같다 · 헤드리스도 같은 길)
- [x] Devices.Ui 전체 4405 통과
- [ ] sln 시험(하네스 검사)

## 발견(증거)
- FlaUI 5.0 `Keyboard.Press` IL(시험 bin 의 FlaUI.Core.dll 반사): `PressVirtualKeyCode → SendInput(code, down, isScanCode:false, isExtended:false, …)` — 화살표도 확장 키 표지 없이 보낸다.
  NumLock 이 켜진 PC 에서 Windows 는 그런 화살표(숫자판 화살표와 같다)를 Shift 와 함께 받으면 화살표 바로 앞에 Shift 떼기를 끼워 넣는다 → WPF 의 어떤 수정키 상태에도 Shift 가 없다.
  실창 결과와 맞는다: Shift+R(R23) 은 통과, Shift+↓(216) · Shift+Alt+↑(135) 만 Shift 빠짐. 메모리의 "Alt+Shift 언어 전환 때문"(r21)도 같은 원인일 가능성이 크다.
- 211: 실창 시험은 아이콘(높이 1.10)에서 기둥 A 높이 H×0.5(=1.1~1.2)로 끈다 — 앞 그림 배율(약 50 px/m)로 0~5 px, 데드존 8 안이라 끌기가 시작되지 않았고 `IsDragging=false` → 틀이 Esc 를 닫기로 먹었다. 앞 시험은 `vm.BeginDrag()` 를 바로 불러 이 길을 못 봤다.
- 004: 실창 시험은 키보드가 아니라 **마우스 끌기**(`StepDrag`)다(목표 문구와 다름). A · B 모두 "망 3 · 담 앞면"(가운데 열) = 같은 격자 점 → `PlanGridMove` 가 서로를 "이미 차지한 점" 으로 막아 "놓지 않았습니다" · 둘 다 제자리.
- 610: 5183 · 5184 칩 사각형이 똑같다 = 같은 붙는 점의 프리셋 센서 둘. b999 작업이 붙은 칩을 화면 벌리기에서 통째로 뺐기 때문.

## 결정
- 가짜 Shift 떼기 판정 = "이동 키(화살표 · Home · End · PgUp · PgDn · Ins · Del) 바로 앞 Shift 떼기의 시각이 같음(≤1 ms)". 공개 API(`KeyEventArgs.Timestamp`)만 쓴다. 사람은 1 ms 안에 떼고 누를 수 없다.
- 211 누름만(데드존 안)에서의 Esc 도 "원래 자리로 돌렸습니다." 로 끝맺는다(뷰모델의 끌기 끝맺음 하나를 재사용). 손잡이 돌리기 중 Esc 는 창을 닫지 않고 놓기만 한다(돌린 각도 되돌리기는 범위 밖).
- 610 = 제품 결함으로 처리: 붙은 칩끼리 겹치면 그 점을 가운데로 나란히(화면만 · 세계 자리 불변 · 위아래로 비켜 선 칩은 밀지 않는다). "저장 막기" 는 하지 않았다.
- 216 '자유 위치' 표시는 맞다(화살표로 붙는 점을 떠나도 물체에는 붙어 있다 — 상태 줄 "…(자유 위치)에 붙음").

## 남은 일
- 헤디드 재확인: MNT-R28-211 · 216 · WIR-R27-135 · SC-FEN24-004 · MNT-R28-610.
- (참고) UI 시험 쪽에서 화살표를 확장 키로 보내면 실사용자 키보드와 같아진다 — 제품은 두 경우 모두 맞게 동작한다.
