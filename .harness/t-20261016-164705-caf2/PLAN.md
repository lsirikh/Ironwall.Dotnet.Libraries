# PLAN — 로그인 설정 창(SSO 연동 켜기 · 끄기)

## 목표
(사용자 2026-10-16) 로그인 화면에서 SSO 연동을 켜고 끄는 '로그인 설정' 창. 로그인 전에도 연다.
이미 된 것: appsettings `AppSettings:IsSsoEnabled` → `SetupModel.IsSsoEnabled` → `AccountUiModule(..., isSsoEnabled)` (a747b4b2 · 호스트 2be39ea). 시작할 때 읽으므로 바꾸면 재시작해야 적용.

## 진행
- [x] 창구 `Accounts.Ui/Services/ILoginSettingsPort.cs` — `ReadIsSsoEnabled()` · `WriteIsSsoEnabled(bool)` · `CanRestart` · `Restart()`
- [x] 창 VM `Accounts.Ui/ViewModels/Dialogs/LoginSettingsDialogViewModel.cs` — 고치기([저장]·[취소]) → 저장 뒤 재시작 안내([지금 다시 시작]·[나중에], 재시작 길이 없으면 [닫기]만). 바꾼 것 없으면 [저장] 꺼짐. 실패는 버튼 줄 왼쪽 글(Critical).
- [x] 창 뷰 `Accounts.Ui/Views/Dialogs/LoginSettingsDialogView.xaml(.cs)` — 커널 틀 T4 S, DialogKey/HelpKey `Accounts.LoginSettings`, 스크림 `ScrimModalBrush`, `Console.ToggleSwitch`, 식별자 AutomationId 만(x:Name 없음), Tab 은 창 안에서만.
- [x] 로그인 패널 `LoginPanelViewModel` — 선택 인자 `loginSettings`(ILoginSettingsPort) · `ssoAgent`(ISsoAgentGateway) 추가(끝에, 기본 null). `IsLoginSettingsAvailable` · `LoginSettings` · `ClickLoginSettings()` · `SsoStatusText(bool,bool)`. 패널이 닫히면 설정 창도 닫는다.
- [x] 로그인 패널 뷰 `LoginPanelView.xaml` — 틀 머리 오른쪽(HeaderExtra) 톱니(`Accounts.Login.SettingsButton`, 툴팁 '로그인 설정', 창구 없으면 숨김, 강제 로그인이어도 보임) + 덮개 ContentControl(`Accounts.Login.SettingsLayer`, 암시적 DataTemplate).
- [x] "?" 설명 `AccountsHelp` — `Accounts.LoginSettings`(SSO 연동 사용 · 지금 상태 세 가지).
- [x] 시험 `Accounts.Ui.Tests/LoginSettingsDialogTests.cs`(창 안 띄움) + `AccountHeadedLeftoverScenarioTests.AccountHelpKeys` 에 새 키 한 줄.
- [ ] 호스트 배선(이 저장소 밖 — 아래 그대로 따로 고친다)

## 창에 넣은 항목(무엇 · 왜)
- **SSO 연동 사용**(켜기/끄기) — 코드가 실제로 읽는 SSO 설정은 이것 하나(`AppSettings:IsSsoEnabled`).
- **지금 상태** 한 줄(읽기 전용) — 지금 실행 중인 구성 기준: 조정자(`SsoSessionCoordinator`) 없음 → 'SSO 연동 꺼짐' / 있음 + `ISsoAgentGateway.IsAgentPresent()` 참 → 'SSO 에이전트 연결됨' / 거짓 → 'SSO 에이전트 없음'.
- 넣지 않은 것: clientId(`SsoAgentGateway.AppClientId` 상수 — 설정이 아님), SSO 서버 주소(GIS 가 읽지 않음 — 에이전트가 안다). 새 설정을 만들지 않는다.

## 호스트 배선 (Dotnet.Monitoring.Solution — 따로 고친다)
1. **새 파일** `Dotnet.Monitoring.Solution/Services/AppSettingsLoginSettingsPort.cs` — `AppSettingsThemeStore.cs`(같은 폴더) 와 같은 읽기 · 원자 저장 경로:
   ```csharp
   public sealed class AppSettingsLoginSettingsPort : ILoginSettingsPort   // using Ironwall.Dotnet.Libraries.Accounts.Ui.Services;
   {
       private const string SECTION = "AppSettings";
       private const string KEY = "IsSsoEnabled";
       private readonly object _gate = new();
       private readonly SetupModel _setup;
       private readonly ILogService? _log;
       public AppSettingsLoginSettingsPort(SetupModel setup, ILogService? log = null) { _setup = setup; _log = log; }

       public bool ReadIsSsoEnabled()
       {
           var path = AppSettingsLocation.AppSettingsReadPath;                       // AppSettingsThemeStore.cs:36 와 같다
           if (!File.Exists(path)) return _setup.IsSsoEnabled;
           var root = JsonConvert.DeserializeObject<JObject>(SettingsFileReader.ReadAllText(path));   // :40
           return root?[SECTION]?[KEY]?.Value<bool?>() ?? true;                    // 키 없음 = 기본 켜짐(SetupModel.cs:116)
       }

       public void WriteIsSsoEnabled(bool value)                                   // 실패는 예외로 — 창이 '저장하지 못했습니다' 를 보인다
       {
           lock (_gate)
           {
               var path = AppSettingsLocation.PrepareAppSettingsWrite();            // AppSettingsThemeStore.cs:62
               var root = File.Exists(path)
                   ? JsonConvert.DeserializeObject<JObject>(SettingsFileReader.ReadAllText(path)) ?? new JObject()
                   : new JObject();
               if (root[SECTION] is not JObject section) { section = new JObject(); root[SECTION] = section; }
               section[KEY] = value;
               AtomicFile.WriteAllText(path, JsonConvert.SerializeObject(root, Formatting.Indented));   // :75 원자 저장
               _setup.IsSsoEnabled = value;                                          // SetupModel 갱신(적용은 재시작 뒤 — Bootstrapper 가 시작 때 읽는다)
               _log?.Info($"[LoginSettings] IsSsoEnabled={value} 저장");
           }
       }

       public bool CanRestart => false;   // 지금 앱 재시작 길이 없다(호스트에서 Restart 검색 0건) → 창은 안내 + [닫기]만
       public void Restart() => throw new NotSupportedException();
   }
   ```
   ※ ThemeStore 와 달리 예외를 삼키지 않는다(창이 실패를 알려야 한다).
2. **`Dotnet.Monitoring.Solution/Bootstrapper.cs:604`**(`builder.RegisterModule(new AccountUiModule(... isSsoEnabled: setup.IsSsoEnabled));`) **바로 아래 한 줄**:
   ```csharp
   builder.Register(_ => new AppSettingsLoginSettingsPort(setup, _log)).As<ILoginSettingsPort>().SingleInstance();   // 로그인 화면 톱니(로그인 설정)
   ```
   using 추가: `using Ironwall.Dotnet.Libraries.Accounts.Ui.Services;`(이미 있으면 생략). 등록하지 않으면 톱니가 숨는다(무회귀).
3. 나중에 재시작 길을 만들면 `CanRestart => true` · `Restart()` 만 채운다(창이 [지금 다시 시작] · [나중에] 를 보인다).
4. 호스트 UI 시험 앵커: 톱니 `Accounts.Login.SettingsButton` · 창 `Dialog.Accounts.LoginSettings.Root/.Primary/.Secondary/.Message` · 스위치 `Accounts.LoginSettings.SsoEnabledToggle` · 상태 `Accounts.LoginSettings.SsoStatusText`.

## 발견
- MDIX `DialogHost` 팝업은 제 흰 카드 크롬을 한 겹 더 그린다 → 로그인 패널 안 덮개 방식(DECISIONS 1).
- 커널 틀 `HeaderExtra` 는 자기 속성이라 Caliburn 이름 관례가 닿지 않는다 → 톱니는 `cal:Message.Attach`.
- `AccountHeadedLeftoverScenarioTests.AccountHelpKeys` 는 계정 뷰의 "?" 키 전수 목록 — 새 키는 여기 한 줄 더해야 한다.
- Caliburn.Micro 에 `Action` 형이 있어 `System.Action` 과 겹친다 → VM 에 `using Action = System.Action;`.

## 검증
- `dotnet test Ironwall.Dotnet.Libraries.Accounts.Ui.Tests` — 442/442
- `dotnet test Ironwall.Dotnet.Libraries.Utils.Tests` — 886/886
- `dotnet test tests/Accounts.Ui.ViewTests` — 90/90
- `dotnet test Ironwall.Dotnet.Libraries.sln` — 시험 어셈블리 10개 모두 0 실패(sln-test.log). Accounts.Ui.Tests · Accounts.Ui.ViewTests 는 sln 에 없어 위에서 따로 돌렸다.

## 남은 일
- 호스트 배선(위 1~2) — 이 저장소 밖.
