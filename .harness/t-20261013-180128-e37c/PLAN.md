# PLAN — 실창 4회차 결선 · 프리셋 결함(t-20261013-180128-e37c)

## 목표
round4-results.txt 의 MNT-R28-400~455 · WIR-R27-135 · SC-FEN24-004 — 원인을 코드로 확정 → 고침 → 헤드리스 시험(빨강 → 초록).
제약: 공개 형식 더하기만 · 새 .cs UTF-8 BOM · x:Name 불변 · 창 금지 · Devices.Ui 는 `dotnet test <csproj> --filter`.

## 조사 결과(근거)
- 실창 헤디드 시험 원본은 이 저장소에 없다(접근 불가) — 결과 줄과 코드로만 판단.
- 헤드리스 탐침(ZzProbeTests, 지움): 적용(403)·모델 바꾸기(401)는 뷰모델 · 부모 없는 캔버스에서 칩이 옮겨지고 그림이 바뀐다 → 계산은 맞다.
- 400: 절 뿌리 `StackPanel AutomationId="Devices.Wiring.Fence.Preset"` 는 UIA peer 가 없다(StackPanel) → 자동화가 절을 못 찾는다.
  같은 절 안에서 찾는 425(설치 높이) · 444(간격 알림 글) 가 함께 빈 값인 것과 맞는다. 고침: peer 있는 패널.
- 444: 알림 글은 TextBlock — 자동화 노출이 불안정(Utils `ConsoleText` 선례) → `ConsoleText`.
- 455 · 440: 센서 모델 창(모달)에서 프리셋을 저장해도 결선은 창이 **닫힌 뒤에만** 다시 읽는다(`OpenPresetManagerAsync`) → 저장 즉시 다시 읽게.
- 440: `HasRangeSensors` 가 프리셋 다시 읽기(`RaisePresetPane`) 알림에 없다 → 더함.
- 425: Alt+↑/↓ 는 고른 프리셋 센서도 9자리 한 단계(프리셋을 놓는다) → PRD FR-07 v1.1 "Alt+↑/↓ 높이 10 cm(Shift 1 cm)" 대로 **고른(Chosen) 프리셋 센서만** 프리셋 높이. 이관 센서(SC-FEN-020 · WIR-R27-135 계약)는 그대로.
  여러 대 골랐을 때 설치 높이 칸이 비던 것 → 모두 같으면 그 값.
- 451: 칩 목록이 문자열 → 같은 글(번호 같은 센서)은 UIA 항목이 하나로 합쳐진다(N=10 · 칩 9). N(쓰임 수)이 맞다 → 항목을 쓰임 객체로.
- 453: 틀 규칙은 Enter = 주 동작(저장) — 포커스가 [취소]여도. W5 계약(Enter 실수 = 취소) → 이 창에서 Enter = 포커스 단추(기본 [취소]).

## 결정
- 헤디드 원본이 없으니 결과 줄의 단서와 코드 근거가 맞는 곳만 고친다. 재현 못 한 것은 아래 "남은 일" 에 적는다.

## 진행
- [x] 400 · 444 — `Consoles/Wiring/WiringSectionPanel.cs`(Group peer) 로 절 뿌리 교체 · 알림 글 `c:ConsoleText`
- [x] 455 · 440 — `Consoles/SensorPresets/ChangeNotifyingSensorPresetRepository.cs`(쓰기 끝나면 결선 다시 읽기) · `WiringLauncher` 가 센서 모델 창에 이것을 건넴 · `RaisePresetPane` 에 `HasRangeSensors`
- [x] 425 — `FenceCanvas.HandleKeyDown` Alt+↑/↓: 고른 프리셋 센서(모두)면 `FenceNudgePresetHeight`(10 cm · Shift 1 cm) · `PresetHeightText` 여러 대 공통 값
- [x] 451 — 칩 ItemsSource = `Usages`(쓰임 객체) · 안내 글을 몸통 칩 아래 `ConsoleDialogText`(`Devices.SensorPresets.SaveConfirm.Note`)로, 틀 바닥 Message 는 비움(좁은 창에서 단추에 밀려 안 보였다)
- [x] 453 — `PresetSaveConfirmView` Enter = 포커스 단추(기본 [취소]) · 포커스 단추 없으면 취소 · 창 PreviewKeyDown 에도 건다
- [x] WIR-R27-135 — `FenceKeyModifiers.IsNumpadFakeShift` 같은 시각 판정 1 ms → 시계 한 눈금 16 ms(`FAKE_SHIFT_WINDOW_MS`). 근거: 사건 시각은 약 15.6 ms 눈금 → 걸치면 ↑ 만 놓치는 실창 증상과 맞음(20 ms 는 여전히 사람 입력)
- [x] 시험 `Tests/RealInputRound4Tests.cs` 11건(Scenario MNT-R28-400·401·403·425·426·440·444·451·453·455 · WIR-R27-135) 통과
- [x] Devices.Ui `--filter "FullyQualifiedName~Wiring|Fence|SensorPreset|RealInput|SensorMount|Help"` 1232건 통과
- [x] `dotnet test Ironwall.Dotnet.Libraries.sln` 전부 통과(처음엔 Utils `HelpContractTests` 가 새 안내 글 바인딩 `Note` 를 설명 문구로 잡음 → 기존 `KeepText` 로 바인딩해 해결)

## 남은 일(헤드리스로 재현 못 함 — 실창 5회차에서 확인 필요)
- MNT-R28-401 · 403: 뷰모델 · 부모 없는 캔버스 · 렌더 픽셀에서는 모델 바꾸기 · 적용 뒤 칩이 바뀌고 옮겨진다(시험이 지킴). 실창만의 차이를 찾지 못함 —
  실창 시험 원본(저장소 밖)에서 측정 칩이 적용 대상(같은 모델)인지 · 프리셋 자리가 원래 자리와 다른지 확인 필요.
- MNT-R28-426: 철조망 칸 위로 끌면 코일 점만 나오고 그 칸에 놓인다(시험이 지킴). 실창의 '담 위 · 담 앞면' 은 포인터 아래 칸이 담 칸이었거나 모델이 PIR(벽 허용)였을 가능성 — 원본 확인 필요.
- SC-FEN24-004: ff5c8293 의 헤드리스 시험은 통과. 실창 끌기 목표 점(점유 여부)을 원본에서 확인 필요.
- 451 의 "N" 은 쓰임 수(10)가 맞다고 확정 — 칩을 쓰임마다 하나로.
