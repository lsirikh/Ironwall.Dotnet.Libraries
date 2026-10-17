using Ironwall.Dotnet.Libraries.Accounts.Ui.Consoles.Groups;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Consoles.Matrix;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Consoles;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Tests.Consoles;

/// <summary>
/// 적대 검토(N-06 A5 · A9 · A10 · A14) — 재시도 뒤의 되돌리기 · 캡처 상실은 취소 ·
/// 재조회 뒤 소속 다시 찍기 · 자기 계정과 계정 관리 그룹 보호.
/// </summary>
public class DragReviewTests
{
    // ── A5: 부분 실패를 다시 [적용] 해도 되돌리기는 전부를 되돌린다 ──────
    [Fact]
    public async Task should_undo_every_user_of_the_batch_after_a_retry_of_the_failed_ones()
    {
        var api = new FakeAccountApi();
        var tray = new DraftTrayViewModel();
        var handler = new UserGroupDropHandler(api, tray, () => true);

        // 다섯 명 — 각자 다른 '이전 그룹' 을 갖는다(되돌리기는 각자의 그룹으로 가야 한다).
        var rows = new[]
        {
            Row(1, "u1", groupId: 91), Row(2, "u2", groupId: 92), Row(3, "u3", groupId: null),
            Row(4, "u4", groupId: 93), Row(5, "u5", groupId: null),
        };
        foreach (var row in rows) api.Users.Add(new AuthUserDto { Id = row.Id, LoginId = row.Username, GroupId = row.GroupId });

        var chip = new AccountGroupChipViewModel(7, "야간조", 0);
        api.FailAssignForUsers.Add(2);
        api.FailAssignForUsers.Add(4);

        handler.Enqueue(chip, rows);
        var first = await handler.ApplyAsync();
        Assert.Equal(3, first.Applied);
        Assert.Equal(2, first.Failed);

        // 서버가 회복됐다 — 남은 둘만 재시도한다.
        api.FailAssignForUsers.Clear();
        var retry = await handler.ApplyAsync();
        Assert.Equal(2, retry.Applied);
        Assert.False(tray.HasEntries);

        api.AssignCalls.Clear();
        await handler.UndoAsync();

        // ★ 다섯 명 전부가, 각자의 이전 그룹으로 되돌아간다.
        Assert.Equal(5, api.AssignCalls.Count);
        Assert.Equal(91, api.AssignCalls.Single(c => c.UserId == 1).GroupId);
        Assert.Equal(92, api.AssignCalls.Single(c => c.UserId == 2).GroupId);
        Assert.Null(api.AssignCalls.Single(c => c.UserId == 3).GroupId);
        Assert.Equal(93, api.AssignCalls.Single(c => c.UserId == 4).GroupId);
        Assert.Null(api.AssignCalls.Single(c => c.UserId == 5).GroupId);
    }

    [Fact]
    public async Task should_start_a_new_undo_ledger_when_a_settled_batch_is_followed_by_a_new_drop()
    {
        var api = new FakeAccountApi();
        var tray = new DraftTrayViewModel();
        var handler = new UserGroupDropHandler(api, tray, () => true);
        var a = Row(1, "u1", groupId: 91);
        var b = Row(2, "u2", groupId: 92);
        foreach (var row in new[] { a, b }) api.Users.Add(new AuthUserDto { Id = row.Id, LoginId = row.Username, GroupId = row.GroupId });
        var chip = new AccountGroupChipViewModel(7, "야간조", 0);

        handler.Enqueue(chip, new[] { a });
        await handler.ApplyAsync();

        handler.Enqueue(chip, new[] { b });          // 끝난 묶음 뒤의 새 묶음
        await handler.ApplyAsync();

        api.AssignCalls.Clear();
        await handler.UndoAsync();

        Assert.Single(api.AssignCalls);              // 두 번째 묶음만 되돌린다
        Assert.Equal(2, api.AssignCalls[0].UserId);
    }

    // ── A14: 자기 계정 · 계정 관리 그룹 보호 ───────────────────────────
    [Fact]
    public void should_refuse_to_drag_your_own_account_into_another_group()
    {
        var me = Row(7, "admin", groupId: 10);
        var context = new GroupDropContext(SelfUserId: 7, AdminGroupId: 10, AdminGroupMemberCount: 3);

        var plan = UserGroupDrop.Plan(11, "정비", new[] { me }, context);

        Assert.False(plan.CanSend);
        Assert.Equal(UserGroupDrop.SelfBlocked, plan.BlockReason);
        Assert.Equal(1, plan.SelfExcluded);
    }

    [Fact]
    public void should_move_the_others_but_leave_your_own_account_where_it_is()
    {
        var me = Row(7, "admin", groupId: 10);
        var other = Row(8, "op1", groupId: 10);
        var context = new GroupDropContext(SelfUserId: 7, AdminGroupId: 0, AdminGroupMemberCount: 0);

        var plan = UserGroupDrop.Plan(11, "정비", new[] { me, other }, context);

        Assert.True(plan.CanSend);
        Assert.Equal(new[] { 8 }, plan.Targets.Select(t => t.Id));
        Assert.Equal(1, plan.SelfExcluded);
        Assert.Contains("자기 계정은 뺐습니다", UserGroupDrop.DropLine(plan, plan.Targets.Count));
    }

    [Fact]
    public void should_refuse_to_empty_the_group_that_can_manage_accounts()
    {
        var lastAdmin = Row(8, "op1", groupId: 10);
        var context = new GroupDropContext(SelfUserId: 99, AdminGroupId: 10, AdminGroupMemberCount: 1);

        var plan = UserGroupDrop.Plan(11, "정비", new[] { lastAdmin }, context);

        Assert.False(plan.CanSend);
        Assert.Equal(UserGroupDrop.LastAdminBlocked, plan.BlockReason);
    }

    [Fact]
    public void should_allow_moving_an_admin_group_member_when_someone_stays_behind()
    {
        var leaving = Row(8, "op1", groupId: 10);
        var context = new GroupDropContext(SelfUserId: 99, AdminGroupId: 10, AdminGroupMemberCount: 2);

        var plan = UserGroupDrop.Plan(11, "정비", new[] { leaving }, context);

        Assert.True(plan.CanSend);
        Assert.Single(plan.Targets);
    }

    [Fact]
    public void should_not_guard_anything_when_the_context_is_unknown()
    {
        var plan = UserGroupDrop.Plan(11, "정비", new[] { Row(8, "op1", groupId: 10) }, GroupDropContext.Unknown);

        Assert.True(plan.CanSend);
        Assert.Equal(0, plan.SelfExcluded);
        Assert.Equal(0, plan.AdminGuardExcluded);
    }

    // ── A9: 캡처 상실은 취소다 ────────────────────────────────────────
    [Fact]
    public void should_restore_the_cells_when_the_capture_is_lost_mid_sweep()
    {
        var matrix = new ToyMatrix(rows: 3);
        var painter = new PermissionPainter(matrix);

        painter.Begin(new PermissionCell(0, 0));
        painter.MoveTo(new PermissionCell(2, 0));
        painter.Cancel();                           // = 캡처 상실 · Esc 가 가는 길

        Assert.All(Enumerable.Range(0, 3), r => Assert.False(matrix.Get(new PermissionCell(r, 0))));
        Assert.False(painter.IsPainting);
    }

    // ── A10: 재조회 뒤 소속 · 칩 인원 ─────────────────────────────────
    [Fact]
    public async Task should_stamp_group_membership_again_after_the_user_list_is_rebuilt()
    {
        var users = new[]
        {
            ConsoleFixtures.User(1, "op1", "김운영"),
            ConsoleFixtures.User(2, "op2", "이감시"),
        };
        var groups = new[] { new UserGroupDto { Id = 10, Name = "야간조", IsActive = true, Permissions = new PermissionsDto() } };
        var (console, api, _, _, _) = ConsoleFixtures.Build(users, groups);
        api.Users[0].GroupId = 10;
        await console.ActivateForTestAsync();

        Assert.Equal(10, console.AccountManagerPanelViewModel.ViewModelProvider.Single(r => r.Id == 1).GroupId);

        await console.AccountManagerPanelViewModel.HandleAsync(new RefreshAccountsMessageModel(), CancellationToken.None);
        await Task.Delay(700);

        var row = console.AccountManagerPanelViewModel.ViewModelProvider.Single(r => r.Id == 1);
        Assert.Equal(10, row.GroupId);                 // 새 행 인스턴스에도 다시 찍힌다
        Assert.Equal("야간조", row.GroupText);
    }

    [Fact]
    public async Task should_count_chip_members_from_the_current_rows_after_applying_a_draft()
    {
        var users = new[]
        {
            ConsoleFixtures.User(1, "op1", "김운영"),
            ConsoleFixtures.User(2, "op2", "이감시"),
        };
        var groups = new[]
        {
            new UserGroupDto { Id = 10, Name = "야간조", IsActive = true, Permissions = new PermissionsDto() },
            new UserGroupDto { Id = 11, Name = "주간조", IsActive = true, Permissions = new PermissionsDto() },
        };
        var (console, api, _, _, _) = ConsoleFixtures.Build(users, groups);
        api.Users[0].GroupId = 10;      // op1 은 야간조
        await console.ActivateForTestAsync();

        var night = console.GroupChips.Single(c => c.Id == 10);
        var day = console.GroupChips.Single(c => c.Id == 11);
        Assert.Equal(1, night.UserCount);
        Assert.Equal(0, day.UserCount);

        var row = console.AccountManagerPanelViewModel.ViewModelProvider.Single(r => r.Id == 1);
        console.OnUsersSelected(new List<object> { row });
        console.AssignSelectionToGroup(day);
        await console.ApplyDraftAsync();

        // 옮긴 쪽만 ++ 하고 원래 쪽을 -- 하지 않으면 두 칩이 동시에 1 이 된다.
        Assert.Equal(0, night.UserCount);
        Assert.Equal(1, day.UserCount);
    }

    private static AccountViewModel Row(int id, string username, int? groupId = null)
        => new(new CapturingEventAggregator(), new SilentLog(), ConsoleFixtures.User(id, username, username)) { GroupId = groupId };

    private sealed class ToyMatrix : IPermissionMatrix
    {
        private readonly bool[,] _values;
        public ToyMatrix(int rows) { RowCount = rows; _values = new bool[rows, PermissionPaintMath.VerbCount]; }
        public int RowCount { get; }
        public bool IsEnabled(PermissionCell cell) => true;
        public bool Get(PermissionCell cell) => _values[cell.Row, cell.Verb];
        public void Set(PermissionCell cell, bool value) => _values[cell.Row, cell.Verb] = value;
    }
}
