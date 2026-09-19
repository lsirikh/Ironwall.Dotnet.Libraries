using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Tests.Consoles;

/// <summary>
/// 끝남 처리 — 패널의 재조회는 행 인스턴스를 <b>새로 만든다</b>(Clear + Add). 콘솔은 같은 Id 의 새 행으로
/// 선택을 맞추고, 손댄 칸이 있으면 글은 두고 행만 바꿔 끼운다(옛 인스턴스에 쓰면 저장 경로가 그 값을 못 본다).
/// </summary>
public class AccountConsoleCompletionTests
{
    [Fact]
    public async Task should_reselect_the_same_account_when_the_list_is_rebuilt()
    {
        var (console, _, _, _, _) = ConsoleFixtures.Build(Users());
        await console.ActivateForTestAsync();

        var before = console.AccountManagerPanelViewModel.ViewModelProvider.Single(r => r.Username == "op2");
        console.OnUsersSelected(new List<object> { before });

        await RebuildListAsync(console);

        var after = console.AccountManagerPanelViewModel.ViewModelProvider.Single(r => r.Username == "op2");
        Assert.NotSame(before, after);                       // 인스턴스는 새것이다
        Assert.Same(after, console.SelectedRows.Single());   // 선택은 같은 계정을 따라간다
    }

    [Fact]
    public async Task should_keep_the_edited_text_and_swap_the_row_when_the_list_is_rebuilt_while_dirty()
    {
        var (console, _, _, _, _) = ConsoleFixtures.Build(Users());
        await console.ActivateForTestAsync();

        var before = console.AccountManagerPanelViewModel.ViewModelProvider.Single(r => r.Username == "op2");
        console.OnUsersSelected(new List<object> { before });
        console.Form.Fields.Single(f => f.Key == "department").Text = "고친 부서";

        await RebuildListAsync(console);

        var after = console.AccountManagerPanelViewModel.ViewModelProvider.Single(r => r.Username == "op2");
        Assert.Same(after, console.Form.Rows.Single());
        Assert.Equal("고친 부서", console.Form.Fields.Single(f => f.Key == "department").Text);
        Assert.True(console.DetailIsDirty);
    }

    [Fact]
    public async Task should_drop_a_selection_that_disappeared_from_the_list()
    {
        var (console, _, gateway, _, _) = ConsoleFixtures.Build(Users());
        await console.ActivateForTestAsync();

        var gone = console.AccountManagerPanelViewModel.ViewModelProvider.Single(r => r.Username == "op3");
        console.OnUsersSelected(new List<object> { gone });

        gateway.Accounts.RemoveAll(a => a.Username == "op3");
        await RebuildListAsync(console);

        Assert.Empty(console.SelectedRows);
        Assert.Equal("선택한 항목 없음", console.DetailTitle);
    }

    [Fact]
    public async Task should_publish_a_refresh_request_after_a_successful_apply()
    {
        var (console, _, _, _, events) = ConsoleFixtures.Build(Users());
        await console.ActivateForTestAsync();

        var row = console.AccountManagerPanelViewModel.ViewModelProvider.First();
        console.OnUsersSelected(new List<object> { row });
        console.Form.Fields.Single(f => f.Key == "position").Text = "과장";
        await console.ApplyAsync();

        Assert.Single(events.OfType<RefreshAccountsMessageModel>());
        Assert.Contains("적용했습니다", console.DetailFooter);
    }

    [Fact]
    public async Task should_leave_the_fields_dirty_when_the_server_refuses_the_apply()
    {
        var (console, _, gateway, _, _) = ConsoleFixtures.Build(Users());
        await console.ActivateForTestAsync();
        gateway.FailUpdate = true;

        var row = console.AccountManagerPanelViewModel.ViewModelProvider.First();
        console.OnUsersSelected(new List<object> { row });
        console.Form.Fields.Single(f => f.Key == "position").Text = "과장";
        await console.ApplyAsync();

        Assert.Contains("반영되지 않았습니다", console.DetailMessage);
        Assert.True(console.DetailIsDirty);
    }

    [Fact]
    public async Task should_count_the_visible_rows_when_a_search_filter_is_on()
    {
        var (console, _, _, _, _) = ConsoleFixtures.Build(Users());
        await console.ActivateForTestAsync();

        console.SearchText = "op2";

        Assert.Contains("목록 1건(전체 3)", console.ListStatusText);
    }

    private static IEnumerable<Ironwall.Dotnet.Monitoring.Models.Accounts.AccountModel> Users() => new[]
    {
        ConsoleFixtures.User(1, "op1", "김운영", department: "경비과"),
        ConsoleFixtures.User(2, "op2", "이감시", department: "상황실"),
        ConsoleFixtures.User(3, "op3", "박점검", department: "정비과"),
    };

    /// <summary>패널의 재조회와 같은 일을 시킨다 — 서버에서 다시 읽고 행 뷰모델을 새로 만든다.</summary>
    private static async Task RebuildListAsync(TestAccountConsole console)
    {
        await console.AccountManagerPanelViewModel.HandleAsync(new RefreshAccountsMessageModel(), CancellationToken.None);
        await Task.Delay(700);      // 패널의 DataInitialize 가 500ms 를 기다린 뒤 목록을 새로 채운다
    }
}
