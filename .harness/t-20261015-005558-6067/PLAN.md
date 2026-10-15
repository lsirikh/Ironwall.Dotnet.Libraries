# PLAN — MNT-R28-426 윤형 칸 위로 끌 때 코일 점이 없다(실창 5~8회차)

## 목표
실창 조건 그대로(멀리 있는 센서 FenceSensorIds[9] · 402~425 프리셋 차례를 거친 '새 모델' · [적용]/[프리셋으로] 뒤 실제 Tab 라우팅 · 26걸음/24걸음째 Tab)
실제 WiringView 를 화면 밖 창에 띄워 재현하고, 제품 결함인지 실창 시험 쪽인지 확정 → 제품이면 고치고, 시험 쪽이면 근거 · 고칠 내용 보고.

## 진행
- [x] 첨부 3종 · PRD 확인
- [x] 정적 분석 → 가설: 24걸음째(2/26 남음 ≈ 30 px 오른쪽) 포인터는 아직 망 3 위 → Tab 이 망 3(철망) 점만 돌았다
- [x] 재현 시험 `Tests/RealWindowR28Round8Tests.cs`(3건 · Trait MNT-R28-426) + 진단 기록 켜기 `Tests/TestDragTrace.cs`
      Round7 시험 도우미 6개(OnSta · Pump · ById · InWindow · Press · SnapPointOnScreen) private → internal(재사용 · 단언 불변)
- [x] 결과(확정): 실창과 **같은 9 레이블 · 같은 첫 레이블 '기둥 A · 레일 높이' · 같은 놓음 '망 2 · 상단 레일 · 가운데 · 2.4 m'**
      진단 기록: `bay=9 … bay=2 style=ChainLink`(코일 점 없음 — 철망 칸이니 맞다) → `Tab 이 캔버스에 닿음 · 끄는 중 True` × 20 · `Tab bay=2 → …` →
      남은 2걸음에 `bay=1 style=ChainLinkRazor points=[… Razor:철조망 위 · 25% … (허용)]` → 가장 가까운 점 = 상단 레일(망 칩 위 1/12 는 코일 아래 · 레일 곁)
- [x] 제품은 맞다: 윤형 칸에 닿은 뒤 Tab → '철조망 위 · 25%' · 그려진 코일(razor.mid) 위에 마우스만으로 놓기 → '망 2 · 철조망 · 가운데'
      포커스: 누르기 전 [프리셋으로] 단추 → 누를 때 칩으로 옮겨져(OnPointerPressed → chip.Focus) Tab 은 캔버스가 받는다(처리 True)
- [x] Devices.Ui csproj 전체 — 실패 5건(WiringSignalThread · StaCleanup · ConsoleWindowChromeShown ×2 · AssemblyLayoutScenario ASM303 RepeatExpandView)은 모두 STA 시간 초과 · 다크 픽셀 0(부하)
      → 그 5건 + Round8 를 따로 돌리니 20/20 통과 = STA 흔들림(이 변경과 무관). 전체 실행은 10분 넘어 세션 끝에 끊겨 최종 합계 없음.
- [x] 솔루션 dotnet test(하네스 1회차): 2753 통과 / 1 실패 — CameraPopup.Tests `EventWindowManagerTests.should_return_within_budget_and_not_throw_when_supervisor_is_unavailable`
      '가장 느린 호출 94 ms — 50 ms 예산 초과'(시간 예산 시험 · 솔루션 병렬 실행 부하). 이 작업은 CameraPopup 을 건드리지 않았다.
      `--filter EventWindowManagerTests` 로 따로 돌리니 23/23 통과(180 ms) → 부하 흔들림. 코드 변경 없이 다음 검사에 맡긴다.
- [x] 하네스 2회차: 솔루션 시험 시간 초과(출력 없음 · 0 통과 0 실패 보고). 코드는 1회차(2753 통과 · 같은 TestDragTrace 포함)와 같다 → 실행 환경 부하로 판단.
      진단 기록 쓰기는 끌기 중에만(FenceCanvas 칸 바뀜 · Tab, CaptureDragBehavior 끌기 단계) 짧은 한 줄이라 전체 시간에 주는 영향은 작다.

## 결정
- 제품 코드는 바꾸지 않는다 — 후보 · 허용 · Tab 라우팅 모두 정상(증거: 위 기록). 칩 배치 코드(FLT-020) 무관.
- DragTrace 는 정적 readonly 로 경로를 처음 한 번 읽는다 → 시험 호스트일 때만 [ModuleInitializer] 로 IRONWALL_DRAG_TRACE 를 (비어 있으면) %TEMP%\devices-ui-test-drag-trace-{pid}.log 로 켠다.
- Seed 는 [+ 모델] 단추 그대로(SmartSensor · Smart 모양) · 403~406 적용 차례 · 410 높이 고침/[프리셋으로]. 420~425 끌기는 다른 센서만 건드려 생략.

## 실창 시험(호스트 저장소 · 고치지 않음) 고칠 내용
- `R28FenceRazorDrag`: `R28Drag(from, to, W, 26, 24, …)` → Tab 을 **마지막 걸음 뒤**(midStep = 26)에. 24걸음째는 포인터가 망 3 위라 망 3 점을 돈다.
- 마우스만으로 코일을 확인하려면 `to` 를 망 칩 사각형 위 1/12(레일 곁)가 아니라 그 위 코일 그림(망 칩 위쪽 바깥)으로.
- 로그 문구 '망 1' → '망 2'(Panel.1 은 0 부터 센 번호).
