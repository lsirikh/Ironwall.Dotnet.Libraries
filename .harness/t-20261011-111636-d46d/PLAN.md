# PLAN — t-20261011-111636-d46d (라이브러리: 호스트 시나리오 보고 결함 R2 · R6 · R10)

## 목표
호스트 시나리오 작업이 "라이브러리 결함"으로 보고만 한 것을 고친다. 각 건 빨강→초록(수정 전 실패하는 헤드리스 시험 → 수정 뒤 통과), 시험에 `[Trait("Scenario", "<id>")]`.
규칙: 창 금지(새 시험은 Window.Show · 화면 밖 창 없음) · 공개 형식은 더하기만 · 기존 시험 본문 불변 · 새 .cs UTF-8 BOM · x:Name 불변.

## 진행
- [x] R2 (SET-058) 배치 미리보기 표지 잘림
- [x] R6 (SET-020 · SET-025) 꺼진 상자 안 칸의 오류가 저장을 막음
- [x] R10 (시험 환경) KoreanWordWrap 전역 설치를 되돌릴 길
- [x] 4. scenario-status.md 실패 — 이 저장소에 `docs/tests/scenario-status.md` 가 없다(호스트 저장소 문서). MAP-056 은 다른 작업 몫이라 손대지 않음
- [x] 손댄 프로젝트 시험 실행 결과 기록(아래 "최종 시험")

## R2 — '↘ 다음 창 +24 px' 가 칸(78)보다 길다(필요 80.6)
- **원인(실측)**: 글이 아니라 '↘' 글리프. 셸 글꼴(MaterialDesignFont=Roboto · Segoe UI 등 라틴 글꼴)에 없어 WPF 가 대체 글꼴을 **언어(xml:lang)별로** 고른다.
  10.5pt 실측: '↘' en-US 7.70 · ko-KR 9.98 DIU, 전체 글 en-US 80.20 · ko-KR 82.48. 화살표를 뺀 '다음 창 +24 px' 는 두 문화권 모두 69.63(같음), 배율(1.0/1.25)도 무관.
  TextBlock 은 제 Language(기본 en-US)로 칸을 잡고, 호스트 감사(FormattedText)는 CurrentUICulture(ko-KR)로 재서 78 < 80.6 이 나왔다. 앱은 설정 문화권으로 Language 를 바꾸므로(호스트 Bootstrapper) 어느 쪽에서든 잘리지 않아야 한다.
- **결정**: 칸 폭을 늘리거나 줄바꿈하는 대신 화살표를 **벡터 아이콘(md:PackIcon ArrowBottomRight)** 으로 그린다 — 같은 뷰의 끌기 손잡이도 PackIcon 이라 디자인 계통이 같고, 글 폭이 문화권 · 배율과 무관해진다.
  - VM 에 더함(공개 모양 불변): `CascadeCaptionText`(화살표 뺀 글) · `ShowCascadeArrow`(간격 0 이면 숨김). 기존 `CascadeCaption`("↘ 다음 창 +24 px" · "간격 0 — …")은 그대로 — 호스트 SET-024 가 단언한다. 이제 그 값은 표지의 UIA 이름(AutomationProperties.Name)으로 쓰인다.
  - 뷰: 표지 TextBlock → StackPanel(AutomationId `Settings.CameraPopup.Event.CascadeCaption`) 안 PackIcon + TextBlock. x:Name 은 건드리지 않음.
- **시험**: `tests/Consoles.ViewTests/CameraPopupCascadeCaptionViewTests.cs` (Trait Scenario=SET-058). 창 없이 부모 없는 렌더(AppHost 의 Application 디스패처에서 Measure/Arrange, `VisualTreeHelper.SetRootDpi`),
  라이트/다크 × 100/125/150 % × Language en-US/ko-KR × 간격 24/200/0 에서 미리보기 안 한 줄 글마다 FormattedText(en-US · ko-KR · CurrentUICulture 중 최대, pixelsPerDip=배율) ≤ ActualWidth + 0.5.
  아이콘 · 글 · UIA 이름 확인 시험 2건 추가.
  - 빨강(수정 전): `다크 100%: [en-US · 간격 24] '↘ 다음 창 +24 px' 필요 80.64 > 칸 78 / [en-US · 간격 200] '↘ 다음 창 +200 px' 필요 86.54 > 칸 84` — 6건 모두 실패(호스트 보고 수치 80.6 > 78 과 같음)
  - 초록(수정 뒤): 8건 통과

## R6 — 꺼진 상자 안 칸의 입력 오류가 블록 저장을 막는다
- **원인**: `CameraPopupSettingsViewModel.HasError` 가 네 글 칸 오류를 상자 상태와 무관하게 OR. "자동 닫기 [사용]" · "타이머로 닫기" 를 끄면 뷰는 그 초 칸을 꺼 버리는데(IsEnabled), 남은 오류가 [저장]을 막고 고칠 길도 없다.
- **수정**: 상자가 꺼진 동안 그 칸 오류는 판정에서 뺀다(`ActiveAutoCloseError` · `ActiveTimerError`). `HasError` · `Has…Error` · `…Error`(뷰 오류 줄) · `IsGroupTouched` 가 같은 값을 쓴다.
  친 글과 오류는 남겨 두고(값 보존), 상자를 다시 켜면 그대로 다시 검사된다. 저장 · 다시 읽기(`ResetTexts(keepSwitchedOff: true)`)를 거쳐도 꺼진 상자 칸의 틀린 글은 남는다. [되돌리기]는 모두 버린다(기존대로).
  저장되는 숫자는 마지막으로 맞았던 값(초안은 틀린 글을 받은 적이 없다).
- **바꾸지 않은 곳(근거)**: 같은 판정 함수의 나머지 두 칸 — "다음 창 간격"(CascadeStep) · "응답 기다림"(BrokerTimeout) — 은 켜고 끄는 상자가 없어 늘 저장 대상이므로 그대로 막는다(의도가 다름).
  다른 라이브러리 블록의 `HasError`(Suppression 서랍 · Accounts/Devices 폼 칸 · Wiring/Assembly 대화상자)는 이 함수를 쓰지 않고 상자-칸 구조도 없어 범위 밖.
- **발견(범위 밖, 손대지 않음)**: 모드를 바꿔 숨겨진 묶음(예: 브로커 모드에서 '응답 기다림'을 틀리게 친 뒤 자체 모드로)의 오류도 보이지 않는 채 저장을 막는다. 이번 명세는 "꺼진 상자" 만이라 기록만 한다.
- **시험**: `Ironwall.Dotnet.Libraries.Events.Ui/Tests/CameraPopupDisabledBoxErrorTests.cs` (Trait SET-020 · SET-025, 8건)
  - 빨강: `CameraPopupDisabledBoxErrorTests.cs:line 66 Assert.False() Failure Expected: False Actual: True` (상자를 끈 뒤 `HasError`) — 8건 중 6건 실패
  - 초록: 8건 통과, `FullyQualifiedName~CameraPopup` 116건 모두 통과

## R10 — KoreanWordWrap.Install() 를 되돌릴 길이 없다
- **수정**: `KoreanWordWrap.Uninstall()` · `IsInstalled` (internal) 추가. WPF 는 클래스 처리기를 떼는 API 가 없으므로 처리기는 둔 채 `_installed` 표지로 **일을 멈춘다** —
  새 TextBlock 을 붙잡지 않고, 이미 붙잡은 TextBlock 의 글 변경 · 다시 Loaded 에도 손대지 않는다. 다시 `Install()` 하면 원래대로.
  - `_installed` 이름 유지(volatile 만 붙임): 호스트 시험 `KoreanWrapGuard` 가 반사로 읽고 써서. 호스트가 반사로 처리기를 떼고 표지를 끈 뒤 `Install()` 해도 다시 걸리게, `Install()` 은 표지가 꺼져 있으면 처리기를 다시 건다(겹쳐도 `IsTracked` 로 한 번만 처리).
  - `InternalsVisibleTo`: Utils.Tests · Consoles.ViewTests · Accounts.Ui.ViewTests · Dotnet.Monitoring.Solution.Tests(호스트 — 반사 대신 쓸 수 있게).
  - 앱 동작 불변: 앱은 `Install()` 만 부르고 `_installed` 는 내내 true.
- **시험**: `Ironwall.Dotnet.Libraries.Utils.Tests/KoreanWordWrapUninstallTests.cs` (Trait Scenario=R10, 2건, 창 없음 — 부모 없는 레이아웃에서도 SizeChanged 가 올라와 전제 확인 가능)
  - 빨강: `error CS0117: 'KoreanWordWrap'에는 'Uninstall'에 대한 정의가 포함되어 있지 않습니다.` (해제 장치 부재 = 결함 그 자체)
  - 초록: 2건 통과(+ 기존 KoreanWordWrap 시험 7건 함께 통과)

## 결정 기록
- 새 시험은 모두 창을 띄우지 않는다(부모 없는 렌더 · STA 스레드). 내가 돌린 것은 해당 시험 클래스 필터뿐, 마지막에 하네스 검사와 같은 `dotnet test Ironwall.Dotnet.Libraries.sln` 1회.
- 측정용 임시 시험(`Utils.Tests/TmpCaptionProbeTests.cs`)은 원인 실측 뒤 지웠다(이 작업에서 만든 파일).

## 최종 시험 (2026-10-11)
- `dotnet build Ironwall.Dotnet.Libraries.sln` — 오류 0
- `dotnet test Ironwall.Dotnet.Libraries.sln --no-build`(하네스 검사와 같음) — 종료 코드 0. 그중 Utils.Tests 867/867 통과, Reports.Ui 359, CameraPopup.Host.Tests 161, Messages 421, Accounts.Api 246, Models 163, Sso 91, SystemResources 22, Watchdog.Tests 4 — 모두 실패 0
- Events.Ui(솔루션 밖) — 2016/2016 통과. 창을 띄우는 6개 반(EventConsoleSingleSelectionDetailTests · EventToolbarChipAutomationTests · MappingDragInteractionTests ·
  SuppressionDayChipAutomationTests · SuppressionRowSelectBindingTests · SuppressionRecurrenceRadioTests)은 "창 금지(사용자 PC 사용 중)" 규칙으로 돌리지 않았다 — 이번 변경(카메라 팝업 설정 블록 · KoreanWordWrap)과 무관한 화면.
- Consoles.ViewTests(솔루션 밖) — 창 없는 관련 반(CameraPopupCascadeCaptionViewTests 8 · CameraPopupMonitorIdentifyViewTests 1) 9/9 통과.
  나머지 반은 AppHost.Show(화면 밖 창)를 써서 같은 규칙으로 돌리지 않았다. 그중 HelpPilotViewTests 가 바뀐 CameraPopupSettingsView 를 띄우지만, 보는 것은 "?" 말풍선 · 설명 줄 부재라 표지 구조 변경과 겹치지 않는다.
- 남은 일: 없음. (사용자 PC 가 비었을 때 Consoles.ViewTests 전체 · Events.Ui 창 반을 한 번 돌려 보면 좋다)
