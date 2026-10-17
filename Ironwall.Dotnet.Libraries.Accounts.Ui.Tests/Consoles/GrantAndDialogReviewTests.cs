using Ironwall.Dotnet.Libraries.Accounts.Ui.Tests.Grants;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Tests.Consoles;

/// <summary>
/// 적대 검토(N-06 A6 · A7) — 한시 부여는 <b>시각</b>까지 고른다 · 사용자 변경 창은 미적용 변경 중에 열리지 않는다.
/// </summary>
public class GrantAndDialogReviewTests
{
    // ── A6: 같은 날 몇 시부터 몇 시까지 ────────────────────────────────
    [Fact]
    public async Task should_accept_a_same_day_grant_that_ends_later_today()
    {
        var server = GrantFixtures.NewServer();
        var (vm, _, _) = GrantFixtures.NewVm(server);
        await vm.ActivateForTestAsync();

        vm.SelectedAccount = vm.Accounts.First(a => a.LoginId == "op1");
        vm.SelectedGroup = vm.Groups.First(g => g.Name == "야간조");
        vm.ValidFrom = GrantFixtures.T0;                    // 오늘 12:00
        vm.ValidUntil = GrantFixtures.T0.AddHours(6);       // 오늘 18:00

        await vm.ClickCreateGrant();

        Assert.Equal(1, server.CreateCallCount);            // 날짜만 고르는 칸이면 둘 다 자정이라 여기 못 온다
        Assert.Single(server.GrantRows);
        Assert.Equal(GrantFixtures.T0.AddHours(6), server.GrantRows[0].ValidUntil);
    }

    [Fact]
    public async Task should_still_refuse_a_same_day_grant_that_already_ended()
    {
        var server = GrantFixtures.NewServer();
        var (vm, _, _) = GrantFixtures.NewVm(server);
        await vm.ActivateForTestAsync();

        vm.SelectedAccount = vm.Accounts.First(a => a.LoginId == "op1");
        vm.SelectedGroup = vm.Groups.First(g => g.Name == "야간조");
        vm.ValidFrom = GrantFixtures.T0.AddHours(-4);
        vm.ValidUntil = GrantFixtures.T0.AddHours(-1);       // 이미 지난 시각

        await vm.ClickCreateGrant();

        Assert.Equal(0, server.CreateCallCount);
    }

    // ── A7: 사용자 변경 창은 미적용 변경을 덮지 않는다 ─────────────────
    [Fact]
    public async Task should_refuse_to_open_the_user_dialog_while_the_detail_has_unapplied_changes()
    {
        var (console, _, _, _, events) = ConsoleFixtures.Build(new[]
        {
            ConsoleFixtures.User(1, "op1", "김운영", department: "경비과"),
        });
        await console.ActivateForTestAsync();

        var row = console.AccountManagerPanelViewModel.ViewModelProvider.Single();
        console.OnUsersSelected(new List<object> { row });
        console.Form.Fields.Single(f => f.Key == "department").Text = "바뀐 부서";

        Assert.False(console.CanOpenUserDialog);
        Assert.Contains("적용하거나 되돌린", console.UserDialogBlockedReason);

        events.Published.Clear();
        await console.OpenUserDialogAsync();

        Assert.Empty(events.OfType<OpenEditAccountDialogMessageModel>());   // 열리지 않는다
        Assert.True(console.DetailIsDirty);                                 // 손댄 칸은 그대로다
    }

    [Fact]
    public async Task should_open_the_user_dialog_when_nothing_is_unapplied()
    {
        var (console, _, _, _, events) = ConsoleFixtures.Build(new[]
        {
            ConsoleFixtures.User(1, "op1", "김운영"),
        });
        await console.ActivateForTestAsync();

        var row = console.AccountManagerPanelViewModel.ViewModelProvider.Single();
        console.OnUsersSelected(new List<object> { row });

        Assert.True(console.CanOpenUserDialog);
        await console.OpenUserDialogAsync();

        Assert.Single(events.OfType<OpenEditAccountDialogMessageModel>());
    }

    [Fact]
    public async Task should_never_put_a_password_into_the_visible_lines()
    {
        var (console, _, _, _, _) = ConsoleFixtures.Build(new[]
        {
            ConsoleFixtures.User(1, "op1", "김운영"),
        });
        await console.ActivateForTestAsync();
        var row = console.AccountManagerPanelViewModel.ViewModelProvider.Single();
        console.OnUsersSelected(new List<object> { row });

        // 상세 칸의 명세에 비밀번호 칸 자체가 없다(아이디 · 비밀번호 읽기 전용 규칙 — 목업 L1242).
        Assert.DoesNotContain(console.Form.Fields, f => f.Key.Contains("password"));
        Assert.DoesNotContain("12345678", console.DetailFooter + console.StatusText + console.UserDialogBlockedReason);
    }
}
