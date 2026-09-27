using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Tests.Consoles;

/// <summary>
/// GIS 실창 육안 검토 2회차(2026-09-27, visual-review.md 담당 C) — 계정 콘솔 #34 · #35 · #36 · #37 의 회귀 시험.
/// </summary>
public class AccountVisualReviewFixTests
{
    // ── #35: 세션 설정 바닥 막대에 [되돌리기] ──────────────────────────────────────
    [Fact]
    public async Task should_restore_the_loaded_values_when_revert_is_pressed_on_the_session_policy()
    {
        var setup = new AccountSetupPanelViewModel(new CapturingEventAggregator(), new SilentLog());
        await setup.ClickReload();                 // 서버가 없으면 기본값을 "불러온 값" 으로 삼는다
        setup.ServerSettingsAvailable = true;
        var hours = setup.TimeoutHours;
        var threshold = setup.LockoutThreshold;

        setup.TimeoutHours = hours + 5;
        setup.LockoutThreshold = 0;
        setup.ConcurrencyPolicy = "evict_all";
        Assert.True(setup.CanClickRevert);

        setup.ClickRevert();

        Assert.Equal(hours, setup.TimeoutHours);
        Assert.Equal(threshold, setup.LockoutThreshold);
        Assert.Equal("allow", setup.ConcurrencyPolicy);
        Assert.Equal(0, setup.ChangedCount);
        Assert.False(setup.CanClickRevert);
        Assert.False(setup.CanClickSave);
    }

    [Fact]
    public async Task should_disable_revert_when_nothing_changed_on_the_session_policy()
    {
        var setup = new AccountSetupPanelViewModel(new CapturingEventAggregator(), new SilentLog());
        await setup.ClickReload();

        Assert.False(setup.CanClickRevert);
    }

    // ── #37: [적용] 뒤 재조회가 편집하던 행 선택을 풀지 않는다 ───────────────────────────
    [Fact]
    public async Task should_keep_the_selection_when_the_grid_reports_an_empty_selection_while_the_list_is_rebuilt()
    {
        var (console, _, _, _, _) = ConsoleFixtures.Build(Users());
        await console.ActivateForTestAsync();

        var before = console.AccountManagerPanelViewModel.ViewModelProvider.Single(r => r.Username == "op2");
        console.OnUsersSelected(new List<object> { before });

        // 패널의 재조회는 Clear() 로 시작한다 — 그 순간 그리드는 고른 행이 사라져 "선택 없음" 을 알린다(실창 결함의 원인).
        console.AccountManagerPanelViewModel.ViewModelProvider.Clear();
        console.OnUsersSelected(new List<object>());

        Assert.NotEqual("선택한 항목 없음", console.DetailTitle);   // 상세가 초기화되지 않는다

        await console.AccountManagerPanelViewModel.HandleAsync(new RefreshAccountsMessageModel(), CancellationToken.None);
        await Task.Delay(700);      // 패널의 DataInitialize 가 500ms 를 기다린 뒤 목록을 새로 채운다

        var after = console.AccountManagerPanelViewModel.ViewModelProvider.Single(r => r.Username == "op2");
        Assert.Same(after, console.SelectedRows.Single());          // 같은 계정을 다시 고른다
        Assert.Same(after, console.Form.Rows.Single());
    }

    [Fact]
    public async Task should_still_clear_the_selection_when_the_person_deselects_a_row_that_is_still_listed()
    {
        var (console, _, _, _, _) = ConsoleFixtures.Build(Users());
        await console.ActivateForTestAsync();

        var row = console.AccountManagerPanelViewModel.ViewModelProvider.Single(r => r.Username == "op1");
        console.OnUsersSelected(new List<object> { row });

        console.OnUsersSelected(new List<object>());      // 행은 목록에 그대로 있다 — 사람이 푼 것이다

        Assert.Empty(console.SelectedRows);
        Assert.Equal("선택한 항목 없음", console.DetailTitle);
    }

    // ── #34 · #36: 운영자 문구 · 등록 다이얼로그 규격 ─────────────────────────────────
    [Fact]
    public void should_not_call_the_input_area_a_form_when_the_session_setup_detail_speaks()
    {
        foreach (var relative in new[] { @"Views\Panels\AccountConsolePanelView.xaml", @"Views\Panels\AccountSetupPanelView.xaml" })
        {
            foreach (var text in VisibleTexts(Path.Combine(UiRoot(), relative)))
                Assert.DoesNotContain("폼", text);
        }
    }

    [Fact]
    public void should_follow_the_console_dialog_layout_when_the_register_dialog_is_declared()
    {
        var xaml = Strip(File.ReadAllText(Path.Combine(UiRoot(), @"Views\Dialogs\RegisterDialogView.xaml")));

        // 옛 청록 머리 띠 · 전폭 두 칸 버튼이 아니다.
        Assert.DoesNotContain("ModernDialogHeader", xaml);
        Assert.DoesNotContain("ColumnDefinition Width=\"5*\"", xaml);
        // 머리(SurfaceAlt) · 버튼 줄은 커널 틀이 그린다(B3 — V-36 의 손 베낌을 진짜 틀로). 버튼 줄은 보조([취소]) → 주 동작([확인]) 순서.
        Assert.Contains("<dlg:ConsoleDialogFrame", xaml);
        Assert.Contains("<dlg:ConsoleDialogFrame.FooterActions>", xaml);
        var cancel = xaml.IndexOf("x:Name=\"ClickCancel\"", StringComparison.Ordinal);
        var ok = xaml.IndexOf("x:Name=\"ClickOk\"", StringComparison.Ordinal);
        Assert.True(cancel > 0 && ok > cancel, "버튼 순서는 [취소] → [확인] 이어야 한다");
        // 호스트 UiTests 가 짚는 식별자는 그대로다.
        Assert.Contains("AutomationProperties.AutomationId=\"Accounts.Register.NameInput\"", xaml);
        Assert.Contains("x:Name=\"Username\"", xaml);
        Assert.Contains("x:Name=\"RegisterPass\"", xaml);
        Assert.Contains("x:Name=\"PasswordConfirm\"", xaml);
    }

    private static IEnumerable<Ironwall.Dotnet.Monitoring.Models.Accounts.AccountModel> Users() => new[]
    {
        ConsoleFixtures.User(1, "op1", "김운영", department: "경비과"),
        ConsoleFixtures.User(2, "op2", "이감시", department: "상황실"),
        ConsoleFixtures.User(3, "op3", "박점검", department: "정비과"),
    };

    private static string UiRoot([CallerFilePath] string? thisFile = null)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", "Ironwall.Dotnet.Libraries.Accounts.Ui"));

    private static string Strip(string xaml) => Regex.Replace(xaml, "<!--.*?-->", string.Empty, RegexOptions.Singleline);

    private static IEnumerable<string> VisibleTexts(string path)
        => Regex.Matches(Strip(File.ReadAllText(path)), "\\b(?:Text|ToolTip|Content|Header|Title|Hint|md:HintAssist\\.Hint)=\"([^\"{][^\"]*)\"")
                .Select(m => m.Groups[1].Value);
}
