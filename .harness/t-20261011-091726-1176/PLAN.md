# PLAN — 결선 · 조립기 시나리오 남은 항목 + 순서 의존 실패 (t-20261011-091726-1176)

## 목표
1. 첨부 `rem-lib-b.txt` 의 결선(WIR) · 조립기(ASM) 항목(‘시험 없음’ · ‘미실행’)마다 결론: (가) 헤드리스 시험 + `[Trait("Scenario", id)]` · (나) 헤디드 필요 — 이유 · (다) 관찰 항목 — 이유. 최대한 (가).
2. 순서 의존 실패: `dotnet test <Devices.Ui> --filter "Scenario~-"` 에서 AssemblyNarrowLayoutTests ASM304(1000×700) 가 XamlParseException(줄 1427 · KeyNotFoundException 'SelectedTimeFormat') — 원인 증거 → 수정 → 5회 연속 실패 0.

## 규칙(요약)
창 금지(Show · 화면 밖 창 금지) · 지도 DB/운영 서버/실제 NATS 금지 · 새 시험마다 Scenario 표지 · 기존 시험 본문 불변(표지 한 줄만) · 공개 형식은 더하기만 · 새 .cs UTF-8 BOM · x:Name 불변 · 대외비 명칭 금지.
셸 주의: Bash 에서 `cd`+git, 파이프 뒤 sed/grep -v, 환경변수 접두, 루프는 자동 거절 → 명령 하나씩, 환경변수는 `dotnet test -e K=V`.

## 진행
- [x] 첨부 2개 읽기(카탈로그 §6 WIR · ASM 전부)
- [x] 순서 의존 실패 원인 규명(아래 "원인 — ASM-304 순서 의존")
- [x] 수정: `Tests/XamlSchemaWarmup.cs`(시험 호스트에서만 도는 모듈 초기화기) + 회귀 시험 `Tests/XamlSchemaWarmupTests.cs` 2건 통과
- [ ] WIR 항목 시험 (WIR-002 · 003 완료 — DeviceConsoleEntryScenarioTests)
- [x] ASM 항목 시험 — AssemblyScenarioRound3Tests 27 · DeviceConsoleEntryScenarioTests(ASM 2) · DetailComponentTableScenarioTests 4 · AssemblyLayoutScenarioTests 19. ASM-305 = 관찰 항목
- [ ] `--filter "Scenario~-"` 5회 연속
- [ ] 손댄 시험 프로젝트 전부 실행 + sln 검사
- [ ] id 별 결과표

## 원인 — ASM-304 순서 의존 (증거)
1. **실패 스택**(이전 회차 증거 `docs/tests/evidence/20261011-091030-all-scenarios-3/Ironwall.Dotnet.Libraries.Devices.Ui.trx`):
   `KeyNotFoundException: The given key 'SelectedTimeFormat'` ← `ConcurrentDictionary.get_Item` ← `System.Windows.Baml2006.WpfXamlType.FindMember` ← `XamlType.GetMember` ← `SetterTriggerConditionValueConverter.ResolveValue` ← `Setter.EndInit`.
   Setter 는 Styles.Console.xaml 의 `Console.DateTimePicker` 트리거 안 `<Setter Property="SelectedTimeFormat" …/>`(mah:DateTimePicker).
2. **WPF 내부 IL 확인**(임시 진단 시험, 리플렉션 — 지금은 지움): `WpfXamlType.Members` 게터 = `ldfld _members → (null 이면) newobj ConcurrentDictionary → stfld _members → ldfld _members`(잠금 · Interlocked 없음).
   `CacheAndReturnXamlMember` = `get_Members().TryAdd(name, m)` 다음 **`get_Members()[name]` 을 다시 읽음**.
   → 두 스레드가 같은 형식을 처음 동시에 찾으면 A 가 사전1 에 넣은 뒤 B 가 `_members` 를 사전2 로 덮고, A 가 사전2 에서 읽어 KeyNotFound.
3. **강제 재현**(같은 임시 진단): 공용 문맥의 DateTimePicker `_members` 를 매 판 null 로 비우고 STA 4개가 Barrier 로 동시에 `FindMember("SelectedTimeFormat")` →
   20000판 중 **114판 KeyNotFound**(메시지 동일: "The given key 'SelectedTimeFormat' was not present in the dictionary.") · 대조(매 판 빈 사전을 **미리 넣음**) → **0판**.
4. **왜 순서 의존인가**: 스키마 문맥(`XamlReader.GetWpfSchemaContext()` · BAML 공용 문맥)은 프로세스 공용이고 형식별 사전은 처음 한 번만 만들어진다.
   Styles.Console.xaml 을 느슨하게 읽는 시험이 5개 클래스(AssemblyNarrowLayout · ComponentTableHostView · WiringFenceEditorView · WiringHeadlessRound2 · WiringNarrowLayout)이고 xUnit 이 클래스별로 병렬 → 누가 먼저 DateTimePicker 를 처음 보느냐에 따라 드물게 난다(단독 실행은 경쟁 상대가 없어 3/3 통과).
   미리 만들기를 끈 채(`-e IRONWALL_XAML_WARMUP=off`) Scenario 묶음 4회는 모두 통과 — 자연 발생은 드물다(위 TRX 가 실제 발생 1건).
5. **제품 영향 없음**: 라이브러리 제품 코드 · 호스트(Dotnet.Monitoring.Solution) 제품 코드 어디에도 둘째 UI 스레드(SetApartmentState · Dispatcher.Run)가 없다(Grep — 호스트의 유일한 일치 WatchdogSetupViewModel 은 DispatcherTimer). XAML 은 UI 스레드 하나에서만 읽혀 이 경쟁이 없다 → 시험 격리 쪽에서 고친다(결정 D1).

## 수정 — 시험 격리
- `Ironwall.Dotnet.Libraries.Devices.Ui/Tests/XamlSchemaWarmup.cs`: `[ModuleInitializer]`, `TestCrashLog.IsTestHost` 일 때만(이 프로젝트는 시험 코드가 제품 DLL 에 같이 실림 — 선례 TestCrashLog).
  UI 어셈블리(PresentationFramework/Core · WindowsBase · System.Xaml · MahApps · ControlzEx · MaterialDesign · Behaviors · Ironwall.*; 참조 사슬 + 출력 폴더 DLL — MahApps 는 XAML 에서만 쓰여 참조 사슬에 없었다)의 공개 형식마다
  공용 문맥 둘(느슨한 XAML · BAML)에서 `WpfXamlType.Members/AttachableMembers` 게터를 한 번 불러 사전을 **한 스레드에서 미리** 만든다. 모듈 초기화기라 어떤 시험 코드보다 먼저 돈다(형식 초기화 잠금). 9ms · 5,691형식 · 22,764 사전.
  진단용 끄기: `dotnet test … -e IRONWALL_XAML_WARMUP=off`.
- 회귀 시험 `XamlSchemaWarmupTests`:
  - `should_precreate_wpf_member_caches_in_both_shared_contexts_before_any_test_runs` — 두 공용 문맥에서 DateTimePicker · TimePickerBase · RangeSlider(어떤 시험도 안 쓰는 형식) · Setter · Trigger · Style · ListBox · TextBox · PackIcon · AssemblyView 의 `_members` 가 이미 있다.
    (첫 시도 빨강: "WpfSharedXamlSchemaContext · DateTimePicker 멤버 사전이 아직 없다" — MahApps 가 참조 사슬에 없어 빠졌던 것 → 출력 폴더 DLL 씨앗 추가 → 초록)
  - `should_resolve_the_trigger_setter_member_from_many_sta_threads_at_once_when_the_context_is_warmed_first` — **이 시험만 쓰는 새 WpfSharedXamlSchemaContext** 를 매 판 만들어(공용 문맥은 안 건드림) STA 4개 동시 첫 조회:
    미리 안 만듦 KeyNotFound 2~4/3000(같은 메시지, 기록만 — 확률적이라 단언 안 함) · 미리 만듦 0/3000(단언).

## 결함 — 빨강→초록
| 결함 | 시험 | 수정 전(빨강) | 수정 | 수정 뒤 |
|---|---|---|---|---|
| ASM-D1(ASM-021) 채널 칸 「1a」 인데 [등록/적용] 이 켜져 있다 — 화면은 1a, 나가는 값은 옛 채널 | AssemblyScenarioRound3Tests::should_disable_the_primary_button_with_the_reason_as_its_tooltip_when_the_channel_text_is_not_a_number_ASM021 | `Assert.False() Failure Expected: False Actual: True`(vm.CanCommit) | AssemblyViewModel: `InputError`(보드 항목의 ChannelError) → CanCommit · CanSaveAsPreset 막음 · CommitBlockedReason(툴팁)에 그 문장. 채널 글은 슬롯을 안 바꿔 보드가 안 울리므로 항목 PropertyChanged 를 받아 RefreshAll(DetachItem 으로 구독 해제) | 통과(조립기 묶음 163/163) |

결함 판정 근거(ASM-D1, 증거 2): ① 명세 둘 — 첨부 카탈로그 ASM-021 「빈 key · 채널 1a → 주 단추 꺼짐 + 툴팁」, docs/tests/gis-full-scenario-catalog.md SC-ASM-009 「주 단추 비활성」(호스트 헤디드 ConsoleGapDevicesAssemblyTests 도 `!enabledBadCh` 를 단언) ② 코드 — `CanCommit` 이 `_board.HasErrors`(key 오류만)만 보고 `BoardSlotViewModel.ChannelError` 를 보지 않음, 위 시험이 True 로 재현.

## 결정
- D1: 순서 의존 실패는 WPF(System.Windows.Baml2006.WpfXamlType)의 잠금 없는 늦은 초기화 경쟁 — 제품은 UI 스레드 하나라 영향 없음 → 시험 격리(모듈 초기화기 미리 만들기)로 고친다. 기존 시험 본문은 그대로.
  다른 안(어셈블리 병렬 끄기 `CollectionBehavior(DisableTestParallelization)`)은 전체 시험을 느리게 하고 원인을 가리므로 택하지 않음.

- D2: 창 금지 — Devices.Ui 기존 시험 중 화면 밖 창(Window.Show)을 쓰는 클래스 11개(ComponentTableHostViewTests · ConsoleWindowChromeShownTests · StaCleanupTests · UnitConsoleNonModalTests · UnitConsoleTreeSelectionReloadTests · UnitMapCanvasInputTests · UnitMapCanvasRenderTests · UnitMapNodeRenderCostTests · WiringFenceEditorViewTests · WiringFenceHeightViewTests · WiringFenceViewTests)는 이 작업에서 돌리지 않는다(최종 실행은 --filter 로 제외). 새 시험은 전부 창 없이: VM · 부모 없는 캔버스(Measure/Arrange) · 느슨한 XAML · HWND 없는 PresentationSource.
- D3: 레이아웃 '잘림' 판정(ASM-302/303 · WIR-606/607) — WPF 는 좁은 칸에서도 요소에 제 크기를 다 주고 부모 배치 클립으로 자르므로 ActualWidth 비교는 헛돈다(자기 점검 시험이 빨강으로 잡음). 그래서 "글 · 조상의 렌더 폭(+여백) > LayoutSlot 폭" 으로 판정하고, 판정기 자체의 대조 시험(should_flag_a_one_line_text_that_is_wider_than_its_cell_…)을 둔다.
- D4: ASM-021 수정 범위 — 명세는 주 단추만 말하지만 [프리셋으로 저장]도 같은 까닭(화면 ≠ 저장값)이라 함께 막았다(CanSaveAsPreset).

## 발견
- `Ironwall.Dotnet.Libraries.sln` 에 Devices.Ui 가 없다 → 하네스 sln 검사는 Devices.Ui 시험을 돌리지 않는다. Devices.Ui 는 따로 돌려 기록한다.

## 남은 일
- WIR · ASM 항목 시험 작성, 5회 연속, 전체 실행, 결과표.
