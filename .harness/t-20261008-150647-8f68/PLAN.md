# PLAN — 결선 창 · 조립기 헤드리스 시험(카탈로그 §6 WIR-* · ASM-*)

## 목표
첨부 카탈로그 §6 의 GAP / 헤디드 전용 항목 중 창을 띄우지 않고 확인할 수 있는 것을 헤드리스 시험으로 만들고, 찾은 결함을 고친다.
창(Window.Show · 화면 밖 창 포함) 금지 — 뷰모델 · 순수 함수 · 부모 없는 비주얼 Measure/Arrange · RenderTargetBitmap 만.

## 결정
- 시험 위치: `Ironwall.Dotnet.Libraries.Devices.Ui/Tests/` (기존 결선 · 조립기 시험과 같은 프로젝트). 이 프로젝트는 `Ironwall.Dotnet.Libraries.sln` 밖이라 하네스 검사(`dotnet test sln`)가 돌리지 않는다 → 직접 `dotnet test <csproj> --filter` 로 돌렸다.
- 기존 시험(화면 밖 창을 쓰는 것 포함)은 손대지 않았다. 새 시험은 화면 밖 창도 쓰지 않는다. 전체 Devices.Ui 실행은 창을 띄우는 옛 시험이 섞여 있어 하지 않고, 바뀐 코드에 닿는 헤드리스 클래스만 골라 돌렸다.
- 이미 헤드리스 시험이 있는 항목은 건너뛰었다(아래 "기존").

## 진행 — 끝
- [x] 카탈로그 §6 읽기 · 빌드 확인
- [x] 정적 판독 D1 · D2 · D4 확인 → 고침 / D3 · D5 · D6 · D7 은 보고
- [x] 새 시험 4파일(107건): `WiringFenceGapTests.cs` · `WiringCanvasGapTests.cs` · `WiringSaveGapTests.cs` · `AssemblyGapTests.cs`
- [x] 회귀: 관련 기존 헤드리스 클래스 327건 통과 · Utils.Tests XAML/도움말 계약 78건 통과

- [x] 하네스 검사 1회차 실패 1건 = `CameraPopup.Tests.SupervisorSurvivalTests.should_suspend_after_four_crashes...` — 부하로 첫 기동 연결이 시간 초과돼 충돌 1회로 세어진 시간 의존 실패(로그 `connect failed: OperationCanceledException`). 이번 변경과 무관(CameraPopup 미수정 · Devices.Ui 는 sln 밖). 단독 12/12 · `dotnet test sln` 재실행 전부 통과.

## 고친 결함
- **D1** `WiringViewModel.ShowBandPicker` 가 접힌 속성 칸을 펴지 않음 → `IsDetailPaneOpen = true`.
- **D2** 표 보기 줄 · 팔레트에 Ctrl+Z 없음(알림은 「Ctrl+Z 로 취소」) → `WiringView.HandleLineKey` · `OnPalettePreviewKeyDown` 에 `vm.Undo()`.
- **D4** 조립기 제조사 · 모델 · 일련번호 칸 자동화 id 없음 → `Devices.Assembly.Field.manufacturer/model/serial`.
- **도움말** 「고른 센서 · 제어기」 `Ctrl+←/→ = 앞 · 뒤 망으로 한 칸` → 실제 처리기와 같은 「다음 격자 점」.
- (시험 가능하게) `AssemblyView.ShouldRemoveOnDelete` — 보드 Delete 판정을 순수 함수로 뺌(동작 같음).

## 남은 일(보고 대상 · 이 작업 범위 밖)
- D3 장애 구간 → 링 A/B 센서 이름 연결 없음(제품 경로 없음 · 큰 일).
- D5 좁은 창 표 열 잘림 — 레이아웃 변경이라 실창 확인 필요(헤디드).
- 헤디드 남김 목록은 최종 보고에.
