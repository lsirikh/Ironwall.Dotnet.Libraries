# DECISIONS

## 1. 원인이 제품 결함인가, 시험 환경인가
- options: (a) 제품 결함 — 3D 나가기 뒤 2D 입력 복구를 고친다 · (b) 시험 환경 — 시험 스레드가 사용자의 보조키(Shift · Ctrl) 상태를 읽는다
- chosen: (b)
- why: 시험 스레드에만 Shift 를 얹으면(`SetKeyboardState`) 휠 단정이 정확히 `17.5 ≠ 17` 로 깨지고 Ctrl 만으로는 안 깨진다. 제품 `OnMouseWheel` 은 Shift+휠을 회전으로 쓰도록 설계됐다(의도된 동작). 실패 2건이 0.2초 안에 몰렸고(45초 STA timeout 아님), 이 프로젝트는 `DisableTestParallelization = true` 라 같은 프로세스 안 다른 시험과의 경합은 원인이 될 수 없다. 실패 직후(≈24초 뒤) 사용자가 메인 세션에 메시지를 입력해 보냈다. pattern: verify.
- effect: 제품 코드는 바꾸지 않았다. 나중에 제품 결함 증거가 나오면 `MapViewModel.Map3D.cs` `Leave3D`/`RestoreMap2DInput` 부터 본다.

## 2. 시험을 사용자 키보드와 떼는 방법
- options: (a) 시험 스레드를 숨은 데스크톱으로(`QuietWindow.UseHiddenDesktop`, 이미 쓰는 공용 도우미) · (b) 단정 직전 `SetKeyboardState` 로 키 상태 비우기(OS 가 다시 맞추면 소용없음) · (c) 휠 단정 빼기(금지 — 단정 약화)
- chosen: (a) — `Scenario.StaOnHiddenDesktop` · `Scenario.StaAsyncOnHiddenDesktop` 변형을 공용 `ScenarioHarness.cs` 에 두고 두 클래스만 쓴다
- why: 목표가 정한 방법이고(QuietWindow.UseHiddenDesktop) 기존 도우미를 그대로 쓴다. 단정은 그대로. pattern: reuse · follow-recommended.
- effect: `ScenarioHarness.cs` 가 `QuietWindow` 를 쓰므로 ScenarioHarness 를 링크하는 4개 프로젝트(MapAux · MapEdit · MapLayer · MapNav) csproj 에 `../Shared/QuietWindow.cs` 링크를 더했다. 되돌리려면 두 클래스의 호출을 `Scenario.Sta`/`StaAsync` 로 바꾸면 된다.

## 3. 숨은 데스크톱을 어디까지 쓸지
- options: (a) 흔들림이 관찰된 두 클래스(Map3DExitInput · GroupRotationEdge)만 · (b) `Scenario.Sta` 전체(지도 시나리오 시험 전부)
- chosen: (a)
- why: 목표가 "포커스가 필요한 그 시험만" 이라고 정했다. 전체로 바꾸면 숨은 데스크톱 고유 문제(예: TSF 초점 크래시 — QuietWindow 주석)가 수백 개 시험에 번질 수 있다.
- effect: 다른 클래스도 보조키를 읽으면 같은 흔들림이 날 수 있다(시험 스레드에 Shift/Ctrl 을 얹으면 이 두 클래스 밖에서도 깨지는 것이 있는지는 보지 않았다). 넓히려면 그 클래스의 `Scenario.Sta(` 를 `Scenario.StaOnHiddenDesktop(` 로 바꾼다.
