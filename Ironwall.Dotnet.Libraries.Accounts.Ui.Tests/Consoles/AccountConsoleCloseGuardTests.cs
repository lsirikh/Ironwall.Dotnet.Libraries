using Ironwall.Dotnet.Libraries.Accounts.Ui.Consoles;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Consoles;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Tests.Consoles;

/// <summary>
/// 계정 · 권한 창 닫기 — 적용하지 않은 사용자 정보 · 저장하지 않은 권한 · 보내지 않은 그룹 배정이 있으면 ✕ 가 먼저 묻는다
/// (2026-09-27 전수 조사: 묻지 않고 버렸다). 확인은 이벤트 창과 같은 확인 팝업(<see cref="OpenConfirmPopupMessageModel"/>)이다.
/// 호스트의 좌측 메뉴 전환도 같은 <c>CanCloseAsync</c> 를 묻는다.
/// </summary>
public class AccountConsoleCloseGuardTests
{
    private static IEnumerable<Ironwall.Dotnet.Monitoring.Models.Accounts.AccountModel> Users() => new[]
    {
        ConsoleFixtures.User(1, "op1", "김운영", department: "경비과"),
        ConsoleFixtures.User(2, "op2", "이감시", department: "상황실"),
    };

    [Fact]
    public async Task should_close_the_account_console_without_asking_when_nothing_is_pending()
    {
        var (console, _, _, _, events) = ConsoleFixtures.Build(Users());
        await console.ActivateForTestAsync();

        await console.ClickClose();

        Assert.Empty(events.OfType<OpenConfirmPopupMessageModel>());
        Assert.Single(events.OfType<ClosePanelMessageModel>());
    }

    [Fact]
    public async Task should_ask_instead_of_closing_when_the_user_form_has_unapplied_changes()
    {
        var (console, _, _, _, events) = ConsoleFixtures.Build(Users());
        await console.ActivateForTestAsync();
        console.OnUsersSelected(new List<object> { console.AccountManagerPanelViewModel.ViewModelProvider[0] });
        console.Form.Fields.Single(f => f.Key == "department").Text = "경비1과";

        await console.ClickClose();

        var confirm = Assert.Single(events.OfType<OpenConfirmPopupMessageModel>());
        Assert.Contains("적용하지 않은 사용자 정보 변경", confirm.Explain);
        Assert.IsType<CallCloseAccountConsoleMessageModel>(confirm.MessageModel);
        Assert.Empty(events.OfType<ClosePanelMessageModel>());      // 묻기만 하고 닫지 않는다
        Assert.False(await console.CanCloseAsync());                 // 좌측 메뉴 전환도 막힌다
    }

    [Fact]
    public async Task should_ask_about_unsaved_permissions_without_calling_them_user_changes()
    {
        var groups = new[]
        {
            new UserGroupDto { Id = 10, Name = "관제 운영", IsActive = true, Permissions = new PermissionsDto() },
        };
        var (console, _, _, _, events) = ConsoleFixtures.Build(groups: groups);
        await console.ActivateForTestAsync();
        await console.SelectRailAsync(AccountConsoleKeys.Permissions);
        console.Matrix.SelectedGroup = console.Matrix.Groups.First();
        console.Matrix.ToggleCell(0, 1);
        Assert.True(console.Matrix.IsDirty);

        Assert.False(await console.CanCloseAsync());

        var explain = Assert.Single(events.OfType<OpenConfirmPopupMessageModel>()).Explain;
        Assert.Contains("저장하지 않은 권한 변경", explain);
        Assert.DoesNotContain("사용자 정보", explain);
    }

    [Fact]
    public async Task should_ask_about_queued_group_assignments()
    {
        var (console, _, _, _, events) = ConsoleFixtures.Build(Users());
        await console.ActivateForTestAsync();
        console.DraftTray.Add(new DraftEntry("user:1:group:10", "PATCH group_id", "김운영 → 관제 운영",
            _ => Task.FromResult(DraftOutcome.Applied)));

        Assert.False(await console.CanCloseAsync());

        Assert.Contains("보내지 않은 그룹 배정 1건", Assert.Single(events.OfType<OpenConfirmPopupMessageModel>()).Explain);
    }

    [Fact]
    public async Task should_close_without_asking_again_when_the_account_console_close_was_confirmed()
    {
        var (console, _, _, _, events) = ConsoleFixtures.Build(Users());
        await console.ActivateForTestAsync();
        console.OnUsersSelected(new List<object> { console.AccountManagerPanelViewModel.ViewModelProvider[0] });
        console.Form.Fields.Single(f => f.Key == "department").Text = "경비1과";

        await console.HandleAsync(new CallCloseAccountConsoleMessageModel(), CancellationToken.None);

        Assert.Single(events.OfType<ClosePopupMessageModel>());
        Assert.Single(events.OfType<ClosePanelMessageModel>());      // [확인] — 닫기를 다시 청한다
        Assert.True(await console.CanCloseAsync());                  // 호스트가 닫기 전에 다시 물어도 통과한다
        Assert.Empty(events.OfType<OpenConfirmPopupMessageModel>());

        await console.DeactivateForTestAsync();
        await console.ActivateForTestAsync();
        console.OnUsersSelected(new List<object> { console.AccountManagerPanelViewModel.ViewModelProvider[0] });
        console.Form.Fields.Single(f => f.Key == "department").Text = "경비2과";
        Assert.False(await console.CanCloseAsync());                 // 다시 연 창에서는 다시 묻는다
    }
}
