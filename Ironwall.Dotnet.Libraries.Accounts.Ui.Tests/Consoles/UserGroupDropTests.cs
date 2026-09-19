using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Consoles.Groups;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Consoles;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Tests.Consoles;

/// <summary>
/// 사용자 N명 → 권한 그룹 드래그. 서버에 <b>벌크 배정 입구가 없어</b> 호출이 N회로 번진다 →
/// 곧바로 보내지 않고 Draft + [적용](설계 정본 all-windows-drag-wireframe.html L339-L340 · L421 · L432).
/// </summary>
public class UserGroupDropTests
{
    // ── 판정(순수 함수) ────────────────────────────────────────────────
    [Fact]
    public void should_refuse_the_plan_when_the_group_is_not_saved_yet()
    {
        var plan = UserGroupDrop.Plan(0, "새 그룹", new[] { Row(1, "op1") });

        Assert.False(plan.CanSend);
        Assert.Contains("저장되지 않은 그룹", plan.BlockReason);
    }

    [Fact]
    public void should_refuse_the_plan_when_nothing_was_dragged()
    {
        var plan = UserGroupDrop.Plan(7, "야간조", Array.Empty<AccountViewModel>());

        Assert.False(plan.CanSend);
        Assert.Contains("계정이 없", plan.BlockReason);
    }

    [Fact]
    public void should_refuse_the_plan_when_every_user_already_belongs_to_the_group()
    {
        var plan = UserGroupDrop.Plan(7, "야간조", new[] { Row(1, "op1", groupId: 7), Row(2, "op2", groupId: 7) });

        Assert.False(plan.CanSend);
        Assert.Equal("이미 이 그룹에 들어 있습니다.", plan.BlockReason);
        Assert.Equal(2, plan.AlreadyIn);
    }

    [Fact]
    public void should_exclude_members_and_unsaved_rows_but_keep_the_rest()
    {
        var plan = UserGroupDrop.Plan(7, "야간조", new[]
        {
            Row(1, "op1", groupId: 7),   // 이미 그 그룹
            Row(2, "op2"),               // 보낸다
            Row(0, "draft"),             // 저장 전
            Row(3, "op3", groupId: 9),   // 다른 그룹 → 보낸다
        });

        Assert.True(plan.CanSend);
        Assert.Equal(new[] { 2, 3 }, plan.Targets.Select(t => t.Id));
        Assert.Equal(1, plan.AlreadyIn);
        Assert.Equal(1, plan.Unsaved);
    }

    [Fact]
    public void should_say_how_many_calls_the_draft_will_make_in_the_result_line()
    {
        var plan = UserGroupDrop.Plan(7, "야간조", new[] { Row(1, "op1"), Row(2, "op2") });

        var line = UserGroupDrop.DropLine(plan, plan.Targets.Count);

        Assert.Contains("Draft 2건", line);
        Assert.Contains("2회로 번지므로 [적용] 때 모아 보냅니다", line);
    }

    // ── 트레이 · 전송 ──────────────────────────────────────────────────
    [Fact]
    public async Task should_queue_one_draft_per_user_when_dropping_on_a_group()
    {
        var (handler, api, tray, rows, chip) = Build();

        var queued = handler.Enqueue(chip, rows);

        Assert.Equal(3, queued);
        Assert.Equal(3, tray.Count);
        Assert.Empty(api.AssignCalls);       // 드롭만으로는 서버를 때리지 않는다
        await Task.CompletedTask;
    }

    [Fact]
    public async Task should_call_the_server_once_per_user_when_applying_the_draft()
    {
        var (handler, api, tray, rows, chip) = Build();
        handler.Enqueue(chip, rows);

        var summary = await handler.ApplyAsync();

        Assert.Equal(3, api.AssignCalls.Count);
        Assert.All(api.AssignCalls, call => Assert.Equal(7, call.GroupId));
        Assert.Equal(3, summary.Applied);
        Assert.False(tray.HasEntries);
        Assert.All(rows, row => Assert.Equal(7, row.GroupId));
        Assert.Equal(3 + 2, chip.UserCount);   // 칩의 인원이 따라 올라간다
    }

    [Fact]
    public async Task should_keep_the_failed_row_in_the_tray_when_one_call_is_refused()
    {
        var (handler, api, tray, rows, chip) = Build();
        api.FailAssignForUsers.Add(2);
        handler.Enqueue(chip, rows);

        var summary = await handler.ApplyAsync();

        Assert.Equal(2, summary.Applied);
        Assert.Equal(1, summary.Failed);
        Assert.Single(tray.Entries);
        Assert.Equal("user:2", tray.Entries[0].TargetKey);
    }

    [Fact]
    public async Task should_skip_a_user_that_joined_the_group_before_the_draft_was_applied()
    {
        var (handler, api, tray, rows, chip) = Build();
        handler.Enqueue(chip, rows);
        rows[1].GroupId = 7;                 // 다른 곳에서 먼저 들어갔다

        var summary = await handler.ApplyAsync();

        Assert.Equal(2, summary.Applied);
        Assert.Equal(1, summary.Skipped);
        Assert.Equal(2, api.AssignCalls.Count);
    }

    [Fact]
    public async Task should_not_call_the_server_when_the_draft_is_discarded()
    {
        var (handler, api, tray, rows, chip) = Build();
        handler.Enqueue(chip, rows);

        handler.Revert();

        Assert.False(tray.HasEntries);
        Assert.Empty(api.AssignCalls);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task should_send_reverse_calls_when_undoing_an_applied_draft()
    {
        var (handler, api, _, rows, chip) = Build();
        rows[0].GroupId = 9;                 // 되돌릴 이전 그룹이 있다
        handler.Enqueue(chip, rows);
        await handler.ApplyAsync();
        api.AssignCalls.Clear();

        await handler.UndoAsync();

        Assert.Equal(3, api.AssignCalls.Count);
        Assert.Equal(9, api.AssignCalls.Single(c => c.UserId == rows[0].Id).GroupId);
        Assert.Null(api.AssignCalls.Single(c => c.UserId == rows[1].Id).GroupId);
        Assert.False(handler.CanUndo);
    }

    [Fact]
    public async Task should_refuse_to_queue_when_the_account_may_not_edit_users()
    {
        var api = new FakeAccountApi();
        var tray = new DraftTrayViewModel();
        var handler = new UserGroupDropHandler(api, tray, () => false);

        var queued = handler.Enqueue(new AccountGroupChipViewModel(7, "야간조", 0), new[] { Row(1, "op1") });

        Assert.Equal(0, queued);
        Assert.False(tray.HasEntries);
        await Task.CompletedTask;
    }

    private static (UserGroupDropHandler Handler, FakeAccountApi Api, DraftTrayViewModel Tray,
                    IReadOnlyList<AccountViewModel> Rows, AccountGroupChipViewModel Chip) Build()
    {
        var api = new FakeAccountApi();
        var tray = new DraftTrayViewModel();
        var handler = new UserGroupDropHandler(api, tray, () => true);
        var rows = new[] { Row(1, "op1"), Row(2, "op2"), Row(3, "op3") };
        foreach (var row in rows) api.Users.Add(new AuthUserDto { Id = row.Id, LoginId = row.Username, GroupId = row.GroupId });
        return (handler, api, tray, rows, new AccountGroupChipViewModel(7, "야간조", 2));
    }

    private static AccountViewModel Row(int id, string username, int? groupId = null)
    {
        var model = ConsoleFixtures.User(id, username, username);
        var vm = new AccountViewModel(new CapturingEventAggregator(), new SilentLog(), model) { GroupId = groupId };
        return vm;
    }
}
