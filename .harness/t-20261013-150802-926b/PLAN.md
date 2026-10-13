# PLAN — round-r28 제품 결함 수정 (t-20261013-150802-926b)

## 목표
2026-10-13 통합 실창 회차(round-r28)가 잡은 제품 결함을 고친다. 각 건: 실패하는 헤드리스 시험 → 수정 → 통과, `[Trait("Scenario","<id>")]`.
명세: 첨부 PRD(sensor-mount-preset v1.1 + v1.2 FR-22). Devices.Ui 시험은 sln 밖 → `dotnet test Ironwall.Dotnet.Libraries.Devices.Ui/Ironwall.Dotnet.Libraries.Devices.Ui.csproj --filter ...`.

## 진행
- [x] W1 MNT-R28-102 [+ 모델] 뒤 목록 새 줄 · 선택 · 스크롤 (목록 가상화 끔 + ScrollIntoView + 행 id `Models.Row.{id}`)
- [x] W1 새 모델 이름 고치기 칸 `Devices.SensorPresets.Models.Name`(기본 7종 읽기 전용) — `RenameModelAsync`
- [x] W1 MNT-R28-104 숨기기/보이기 뒤 같은 모델을 고른 채 · `SelectedModelName`
- [x] W2 MNT-R28-207 수치 칸 범위 — 잘라 맞추고 안내(`FieldNotice` · `MountEditor.FieldNotice`)
- [x] W2 MNT-R28-214/216/217 그림 MouseDown 처리(포커스 유지) · 상태 줄 `PlaceText` · 포커스 점선
- [x] W2 MNT-R28-211 `IDialogDragSource`(Utils) + `DialogKeyRules.Decide(..., innerDragActive)` — 틀이 끄는 중 Esc/Enter 를 넘김
- [x] W1 MNT-R28-224 프리셋 행 AutomationId `Devices.SensorPresets.Models.Mount.Row.{id}`
- 시험: `Devices.Ui/Tests/SensorPresetR28Tests.cs` 13건 통과 · `--filter "Scenario~MNT"` 68건 통과
- [x] WIR-R27-152/153 줌 글 — FenceView 가 시각 트리로만 캔버스를 찾아, 접힌 채 Loaded 되면 ViewChanged 미구독(원인 확정) → 논리 트리도 찾고 생성자 · 보이게 될 때 구독
- [x] SC-FEN24-001 선택 윤곽 — 칩 사각형 테두리 위 2.2 펜이 칩 밖 절반 잘림 → 안쪽 선 `FenceInk.SelectInner`(+2.2 들임) 더함(기존 Select 사각형 = 적중 계약 유지)
- [x] SC-FEN24-004 함께 끌기 — 몸을 잡고 끌면 포인터가 점에서 24 px 넘게 비켜 제자리 → 포인터 근처에 점이 없으면 "자기 점 + 끈 만큼" 으로 다시 찾음(`_grabOffset`)
- [x] WIR-R27-109 망 2칸 선택 — 망 칩 가운데에 센서 칩이 얹혀 가운데 클릭이 센서를 고름(유력) → 망 칩 UIA `GetClickablePointCore` = 가리지 않는 점
- [x] SC-HLP-wir-f1 — 원인 미확정(실창 포커스 실패 가능). 개념도가 F1 을 직접 받아 자기 "?"(Devices.Wiring.Fence.Concept)를 연다(범위 등록이 늦어도)
- [ ] WIR-R27-135 Shift+Alt+↑/↓ — 현재 코드 경로 정상 · 헤드리스 `WiringPresetKeyContractTests` 통과. r28 앱 bin 이 이 수정 전 판이었을 가능성 → 헤디드 재확인 필요(코드 수정 없음)
- 시험: `Devices.Ui/Tests/WiringR28Tests.cs` 5건 · Devices.Ui 전체 4367 통과 · sln 은 기존 실패 1건(CameraPopup SupervisorSurvivalTests)만

## 결정
- (207) 범위 밖 수치는 **잘라 맞추고 상태 줄에 잘랐다고 안내**한다(저장 끄기 대신) — FR-22 "끌어 붙이고 까닭을 상태 줄에" 와 같은 규칙. 숫자가 아닌 글은 지금처럼 값을 받지 않고 안내.
  높이: 모델이 지중을 허용하지 않으면 0.05 m 아래로 내려가지 않는다(땅속 금지 · 붙는 곳 불변), 지중만 허용하면 0 m 위로 안 올라간다.
- (211) 틀(ConsoleDialogFrame)은 마우스 캡처가 틀 안 다른 요소에 있으면(끄는 중) Esc 를 소비하지 않는다 → 그림이 받아 원위치.

## 발견
- 편집기 Tab 이 입력 칸으로 빠지는 원인: 그림의 빈 곳을 누르면 MouseDown 이 처리되지 않아 바깥 ScrollViewer 가 포커스를 가져감(Tab×3 = 이름 → 이름 제안 → 칸 폭 = 실측과 일치).
- 높이 −1 이 받아지던 원인: `MoveFreeAttached` 가 높이 부호로 지중(바닥)으로 갈래를 바꿈 — 지중을 허용하지 않는 모델에도.

## 남은 일
- 정본 PRD(저장소 밖 docs) §10 에 v1.3 결정 옮기기: 207 = "잘라 맞추고 안내" · 211 = `IDialogDragSource`.
- 헤디드 재확인: MNT-R28-100/102/104/207/211/214/216/217/224 · WIR-R27-109/135/152/153 · SC-FEN24-001/004 · SC-HLP-wir-f1.
