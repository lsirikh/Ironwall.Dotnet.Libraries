using Ironwall.Dotnet.Libraries.Accounts.Ui.Consoles;
using Ironwall.Dotnet.Libraries.Enums;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Tests.Consoles;

/// <summary>
/// 계정 콘솔 — 상단 탭 6 → 레일 6(설계 정본 L1149 · L1196-L1204) · 권한별 표시 규칙 이관(L1150) ·
/// 미적용 변경이 있을 때 이동 차단(L928).
/// </summary>
public class AccountConsoleRailTests
{
    [Fact]
    public async Task should_list_six_rails_in_the_design_order_when_an_admin_opens_the_console()
    {
        var (console, _, _, _, _) = ConsoleFixtures.Build();
        await console.ActivateForTestAsync();

        Assert.Equal(
            new[]
            {
                AccountConsoleKeys.Users, AccountConsoleKeys.Permissions, AccountConsoleKeys.Sessions,
                AccountConsoleKeys.Grants, AccountConsoleKeys.Audit, AccountConsoleKeys.SessionSetup,
            },
            console.RailEntries.Select(e => e.Key));

        Assert.Equal("사용자", console.RailEntries[0].Label);
        Assert.Equal(AccountConsoleKeys.Users, console.SelectedRail!.Key);
    }

    [Fact]
    public async Task should_put_a_separator_above_session_setup_only()
    {
        var (console, _, _, _, _) = ConsoleFixtures.Build();
        await console.ActivateForTestAsync();

        Assert.True(console.RailEntries.Single(e => e.Key == AccountConsoleKeys.SessionSetup).HasSeparatorAbove);
        Assert.All(console.RailEntries.Where(e => e.Key != AccountConsoleKeys.SessionSetup),
                   entry => Assert.False(entry.HasSeparatorAbove));
    }

    [Fact]
    public async Task should_hide_count_badges_on_audit_and_session_setup()
    {
        var (console, _, _, _, _) = ConsoleFixtures.Build();
        await console.ActivateForTestAsync();

        Assert.False(console.RailEntries.Single(e => e.Key == AccountConsoleKeys.Audit).ShowCount);
        Assert.False(console.RailEntries.Single(e => e.Key == AccountConsoleKeys.SessionSetup).ShowCount);
        Assert.True(console.RailEntries.Single(e => e.Key == AccountConsoleKeys.Users).ShowCount);
    }

    [Fact]
    public async Task should_show_only_the_audit_rail_when_the_account_is_a_maintainer()
    {
        var (console, _, _, permission, _) = ConsoleFixtures.Build();
        permission.Role = EnumUserRole.MAINTAINER;      // ADMIN 이 아니다 — 나머지 다섯은 ADMIN 전용
        permission.AuditAllowed = true;

        await console.ActivateForTestAsync();

        Assert.Equal(new[] { AccountConsoleKeys.Audit }, console.RailEntries.Select(e => e.Key));
        Assert.Equal(AccountConsoleKeys.Audit, console.SelectedRail!.Key);
    }

    [Fact]
    public async Task should_leave_the_rail_empty_when_the_account_may_see_nothing()
    {
        var (console, _, _, permission, _) = ConsoleFixtures.Build();
        permission.Role = EnumUserRole.USER;
        permission.AuditAllowed = false;

        await console.ActivateForTestAsync();

        Assert.Empty(console.RailEntries);
    }

    [Fact]
    public async Task should_rebuild_the_rail_when_permissions_change_while_open()
    {
        var (console, _, _, permission, _) = ConsoleFixtures.Build();
        permission.Role = EnumUserRole.USER;
        permission.AuditAllowed = false;
        await console.ActivateForTestAsync();
        Assert.Empty(console.RailEntries);

        permission.Role = EnumUserRole.ADMIN;
        permission.AuditAllowed = true;
        permission.RaiseChanged();

        Assert.Equal(6, console.RailEntries.Count);
    }

    [Fact]
    public async Task should_block_the_rail_switch_when_there_are_unapplied_changes()
    {
        var (console, _, _, _, _) = ConsoleFixtures.Build(
            new[] { ConsoleFixtures.User(1, "op1", "김운영") });
        await console.ActivateForTestAsync();

        var row = console.AccountManagerPanelViewModel.ViewModelProvider.Single();
        Assert.True(console.OnUsersSelected(new List<object> { row }));
        console.Form.Fields.Single(f => f.Key == "name").Text = "고친 이름";
        Assert.True(console.DetailIsDirty);

        var moved = await console.SelectRailAsync(AccountConsoleKeys.Sessions);

        Assert.False(moved);
        Assert.True(console.IsUsersRail);
        Assert.Contains("적용하거나 되돌린", console.DetailFooter);
    }

    [Fact]
    public async Task should_allow_the_rail_switch_when_the_changes_were_reverted()
    {
        var (console, _, _, _, _) = ConsoleFixtures.Build(
            new[] { ConsoleFixtures.User(1, "op1", "김운영") });
        await console.ActivateForTestAsync();

        var row = console.AccountManagerPanelViewModel.ViewModelProvider.Single();
        console.OnUsersSelected(new List<object> { row });
        console.Form.Fields.Single(f => f.Key == "name").Text = "고친 이름";
        console.Revert();

        Assert.True(await console.SelectRailAsync(AccountConsoleKeys.Sessions));
        Assert.True(console.IsSessionsRail);
    }

    [Fact]
    public async Task should_clear_selection_and_drafts_when_the_console_closes()
    {
        var (console, _, _, _, _) = ConsoleFixtures.Build(
            new[] { ConsoleFixtures.User(1, "op1", "김운영") });
        await console.ActivateForTestAsync();
        var row = console.AccountManagerPanelViewModel.ViewModelProvider.Single();
        console.OnUsersSelected(new List<object> { row });

        await console.DeactivateForTestAsync();

        Assert.Empty(console.SelectedRows);
        Assert.False(console.DraftTray.HasEntries);
        Assert.Equal(string.Empty, console.SearchText);
    }
}
