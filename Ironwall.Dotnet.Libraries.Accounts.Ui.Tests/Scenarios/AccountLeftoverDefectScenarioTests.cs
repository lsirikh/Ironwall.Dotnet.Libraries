using System.Collections.Specialized;
using System.IO;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Providers;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Consoles;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Tests.Consoles;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Dialogs;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Tests.Scenarios;

/****************************************************************************
   Purpose   : 앞 헤드리스 작업(t-20261011-073820-4f76)이 "보고만" 한 계정 결함 3건을 빨강→초록으로 닫는다.
               ACC-026 세션 자동 새로 고침 뒤 선택 · ACC-033 가입 창 아이디/성명 길이 · ACC-031 세션 정책 칸 자동화 id.
   Notes     : 진짜 콘솔 · 대화상자 뷰모델 + 가짜 API/게이트웨이(ConsoleFixtures) — 서버 · DB · 창 없음.
               실제 그리드(SelectedItem TwoWay)가 목록을 비울 때 선택을 null 로 밀어 넣는 동작은 아래 GridLikeSelection 이 흉내 낸다.
****************************************************************************/
public class AccountLeftoverDefectScenarioTests
{
    private static string RepoRoot([CallerFilePath] string? thisFile = null)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));

    private static string SetupViewPath()
        => Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Accounts.Ui", "Views", "Panels", "AccountSetupPanelView.xaml");

    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    #region - ACC-026 세션 자동 새로 고침 -
    /// <summary>세션 두 개 — 부를 때마다 새 인스턴스(실서버 응답처럼). <paramref name="expires"/> 로 새로 고침 뒤 값이 바뀐 것을 본다.</summary>
    private static List<UserSessionDto> Page(string expires, params int[] ids)
        => ids.Select(id => new UserSessionDto
        {
            Id = id, UserId = id, LoginId = $"op{id}", Role = "USER", IpAddress = $"10.0.0.{id}",
            IsActive = true, ExpiresAt = expires, CreatedAt = "2026-10-11T08:00:00+09:00",
        }).ToList();

    /// <summary>실제 DataGrid 의 선택 바인딩 흉내 — 목록이 비워지거나 고른 행이 빠지면 SelectedItem 을 null 로 밀어 넣는다.</summary>
    private static void GridLikeSelection(TestAccountConsole console)
    {
        console.UserSessionPanelViewModel.Items.CollectionChanged += (_, e) =>
        {
            if (console.SelectedSession is null) return;
            if (e.Action == NotifyCollectionChangedAction.Reset
                || (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems!.Contains(console.SelectedSession)))
                console.SelectedSession = null;
        };
    }

    private static async Task<(TestAccountConsole Console, FakeAccountApi Api)> OpenSessionsAsync()
    {
        var (console, api, _, _, _) = ConsoleFixtures.Build();
        api.SessionPage = () => Page("2026-10-11T09:00:00+09:00", 11, 12);
        await console.ActivateForTestAsync();
        Assert.True(await console.SelectRailAsync(AccountConsoleKeys.Sessions));
        Assert.Equal(2, console.UserSessionPanelViewModel.Items.Count);
        return (console, api);
    }

    [Trait("Scenario", "ACC-026")]
    [Fact]
    public async Task should_keep_the_same_session_selected_after_the_auto_refresh_replaces_the_rows()
    {
        var (console, api) = await OpenSessionsAsync();
        try
        {
            GridLikeSelection(console);
            console.SelectedSession = console.UserSessionPanelViewModel.Items.Single(s => s.Id == 12);
            api.SessionPage = () => Page("2026-10-11T10:00:00+09:00", 11, 12);   // 20초 뒤 — 만료 시각이 늘었다

            await console.UserSessionPanelViewModel.TryAutoRefresh();

            Assert.NotNull(console.SelectedSession);                              // 선택이 풀리면 상세가 「선택한 세션 없음」으로 돌아간다
            Assert.Equal(12, console.SelectedSession!.Id);
            Assert.Same(console.UserSessionPanelViewModel.Items.Single(s => s.Id == 12), console.SelectedSession);
            Assert.Equal("op12", console.DetailTitle);
        }
        finally { await console.DeactivateForTestAsync(); }
    }

    [Trait("Scenario", "ACC-026")]
    [Fact]
    public async Task should_show_the_refreshed_session_values_in_the_detail_after_the_auto_refresh()
    {
        var (console, api) = await OpenSessionsAsync();
        try
        {
            console.SelectedSession = console.UserSessionPanelViewModel.Items.Single(s => s.Id == 12);
            api.SessionPage = () => Page("2026-10-11T10:00:00+09:00", 11, 12);

            await console.UserSessionPanelViewModel.TryAutoRefresh();

            // 옛 인스턴스를 붙들고 있으면 상세가 새로 고침 전 값(09:00)을 계속 보인다
            Assert.Equal("2026-10-11T10:00:00+09:00", console.SelectedSession?.ExpiresAt);
            Assert.Contains(console.SelectedSession!, console.UserSessionPanelViewModel.Items);
        }
        finally { await console.DeactivateForTestAsync(); }
    }

    [Trait("Scenario", "ACC-026")]
    [Fact]
    public async Task should_clear_the_selection_when_the_selected_session_is_gone_after_the_auto_refresh()
    {
        var (console, api) = await OpenSessionsAsync();
        try
        {
            GridLikeSelection(console);
            console.SelectedSession = console.UserSessionPanelViewModel.Items.Single(s => s.Id == 12);
            api.SessionPage = () => Page("2026-10-11T10:00:00+09:00", 11);       // 12 는 로그아웃해 활성 목록에서 빠졌다

            await console.UserSessionPanelViewModel.TryAutoRefresh();

            Assert.Null(console.SelectedSession);                                 // 사라진 세션을 붙들고 있지 않는다
        }
        finally { await console.DeactivateForTestAsync(); }
    }

    [Trait("Scenario", "ACC-026")]
    [Fact]
    public async Task should_not_bring_back_an_old_session_selection_after_the_rail_was_switched_away()
    {
        var (console, _) = await OpenSessionsAsync();
        try
        {
            GridLikeSelection(console);
            console.SelectedSession = console.UserSessionPanelViewModel.Items.Single(s => s.Id == 12);
            Assert.True(await console.SelectRailAsync(AccountConsoleKeys.Users));
            Assert.True(await console.SelectRailAsync(AccountConsoleKeys.Sessions));

            await console.UserSessionPanelViewModel.TryAutoRefresh();

            Assert.Null(console.SelectedSession);                                 // 레일을 떠나며 버린 선택은 되살아나지 않는다
        }
        finally { await console.DeactivateForTestAsync(); }
    }
    #endregion

    #region - ACC-033 가입 창 아이디 · 성명 길이 -
    private static RegisterDialogViewModel OpenRegister()
    {
        var events = new CapturingEventAggregator();
        var log = new SilentLog();
        var gateway = new FakeDirectoryGateway { CanInferFirstAccountAdmin = false };
        return new RegisterDialogViewModel(events, log, new RegisterViewModel(events, log, new AccountModel()),
            new AccountProvider(), gateway, new FakeProfileImageService());
    }

    /// <summary>
    /// 가입 창 안내는 「아이디 5~20자」(RegisterDialogView.xaml Message · StringLengthValidationRule Min=5 Max=20) ·
    /// 성명 검증 2~10자(Min=2 Max=10)다. [확인] 판정(CanClickOk)이 같은 규칙을 봐야 한다 — 서버가 더 엄격한 경우는
    /// 아래 별도 시험처럼 서버 거부 경로가 그대로 실패를 알린다.
    /// </summary>
    [Trait("Scenario", "ACC-033")]
    [Theory]
    [InlineData("abcd", "신규", false)]                        // 아이디 4자
    [InlineData("abcde", "신규", true)]                        // 아이디 5자(하한)
    [InlineData("abcdefghijklmnopqrst", "신규", true)]         // 아이디 20자(상한)
    [InlineData("abcdefghijklmnopqrstu", "신규", false)]       // 아이디 21자
    [InlineData("fresh01", "김", false)]                       // 성명 1자
    [InlineData("fresh01", "김운", true)]                      // 성명 2자(하한)
    [InlineData("fresh01", "가나다라마바사아자차", true)]       // 성명 10자(상한)
    [InlineData("fresh01", "가나다라마바사아자차카", false)]    // 성명 11자
    public async Task should_follow_the_id_and_name_length_rules_shown_on_the_register_dialog_when_ok_is_evaluated(
        string username, string name, bool expected)
    {
        var dialog = OpenRegister();
        await ScreenExtensions.TryActivateAsync(dialog);
        dialog.Username = username;
        dialog.Name = name;
        dialog.RegisterPass = "abcdefgh1";
        dialog.PasswordConfirm = "abcdefgh1";

        Assert.True(expected == dialog.CanClickOk,
            $"아이디 {username.Length}자 · 성명 {name.Length}자에 가입 [확인] 이 {(dialog.CanClickOk ? "켜졌다" : "꺼졌다")} — 안내는 아이디 5~20자 · 성명 2~10자");
    }

    [Trait("Scenario", "ACC-033")]
    [Fact]
    public async Task should_still_report_the_failure_when_the_server_refuses_an_id_the_dialog_accepted()
    {
        var events = new CapturingEventAggregator();
        var log = new SilentLog();
        var gateway = new FakeDirectoryGateway { CanInferFirstAccountAdmin = false, CreateResult = _ => throw new InvalidOperationException("서버 거부") };   // 서버가 더 엄격하다(거부)
        var dialog = new RegisterDialogViewModel(events, log, new RegisterViewModel(events, log, new AccountModel()),
            new AccountProvider(), gateway, new FakeProfileImageService());
        await ScreenExtensions.TryActivateAsync(dialog);
        dialog.Username = "abcde";
        dialog.Name = "김운";
        dialog.RegisterPass = "abcdefgh1";
        dialog.PasswordConfirm = "abcdefgh1";
        Assert.True(dialog.CanClickOk);

        await dialog.ClickOk();

        Assert.Equal("계정 등록이 실패하였습니다.", events.OfType<OpenInfoPopupMessageModel>().Last().Explain);
        Assert.Empty(events.OfType<CloseDialogMessageModel>());                 // 창은 닫지 않는다 — 고쳐서 다시 누를 수 있다
    }
    #endregion

    #region - ACC-031 세션 정책 칸 자동화 id -
    /// <summary>세션 정책의 입력 칸마다 자동화 id 가 있어야 헤디드가 라벨 폴백 · 트리 순서 없이 찾는다(카탈로그 ACC-031 「LockoutThreshold 외 id 없음」).</summary>
    [Trait("Scenario", "ACC-031")]
    [Theory]
    [InlineData("TextBox", "TimeoutHours")]
    [InlineData("TextBox", "RefreshDays")]
    [InlineData("TextBox", "LockoutThreshold")]
    [InlineData("TextBox", "LockoutDurationMinutes")]
    [InlineData("ToggleButton", "SessionPolicyEnabled")]
    [InlineData("ComboBox", "ConcurrencyPolicy")]
    [InlineData("TextBox", "MaxConcurrentSessions")]
    [InlineData("ToggleButton", "SessionSelfReplaceEnabled")]
    [InlineData("TextBox", "SessionHistoryRetentionDays")]
    public void should_give_each_session_policy_input_an_automation_id(string element, string bindingPath)
    {
        var doc = XDocument.Load(SetupViewPath());
        var input = doc.Descendants().Single(e => e.Name.LocalName == element
            && e.Attributes().Any(a => a.Value.StartsWith($"{{Binding {bindingPath}", StringComparison.Ordinal)
                                    && (a.Value.Length == bindingPath.Length + 9 || a.Value[bindingPath.Length + 9] is ',' or '}')));

        var id = (string?)input.Attribute("AutomationProperties.AutomationId");
        Assert.True(id == $"Console.Accounts.SessionSetup.{bindingPath}",
            $"{bindingPath} 칸의 자동화 id 가 '{id ?? "(없음)"}' — Console.Accounts.SessionSetup.{bindingPath} 이어야 한다");
    }

    [Trait("Scenario", "ACC-031")]
    [Fact]
    public void should_give_the_reload_button_an_automation_id_and_keep_its_caliburn_name()
    {
        var doc = XDocument.Load(SetupViewPath());
        var reload = doc.Descendants().Single(e => e.Name.LocalName == "Button" && (string?)e.Attribute(X + "Name") == "ClickReload");

        Assert.Equal("Console.Accounts.SessionSetup.Reload", (string?)reload.Attribute("AutomationProperties.AutomationId"));
        // x:Name 은 Caliburn 지시자 · 호스트 앵커 — 그대로 둔다
        Assert.Single(doc.Descendants(), e => (string?)e.Attribute(X + "Name") == "ClickSave");
    }

    [Trait("Scenario", "ACC-031")]
    [Fact]
    public void should_not_repeat_an_automation_id_on_the_session_policy_view()
    {
        var ids = XDocument.Load(SetupViewPath()).Descendants()
            .Select(e => (string?)e.Attribute("AutomationProperties.AutomationId"))
            .Where(id => id is not null)
            .ToList();

        Assert.True(ids.Count >= 13, $"자동화 id {ids.Count}개");
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
    }
    #endregion
}
