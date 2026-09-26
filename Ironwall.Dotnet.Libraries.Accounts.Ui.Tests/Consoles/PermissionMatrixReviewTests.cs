using Ironwall.Dotnet.Libraries.Accounts.Ui.Consoles;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Consoles.Matrix;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Tests.Consoles;

/// <summary>
/// 적대 검토(N-06 A1 · A2 · A3 · A13 · A15)에서 나온 결함의 회귀망 —
/// 실패한 저장은 미적용을 지우지 않는다 · 재조회가 선택을 놓지 않는다 · 칠해 둔 변경은 이동을 막는다 ·
/// 만질 수 없는 칸은 뷰모델도 막는다 · 그 사이 바뀐 권한은 덮지 않는다.
/// </summary>
public class PermissionMatrixReviewTests
{
    // ── A1: 실패한 저장 ────────────────────────────────────────────────
    [Fact]
    public async Task should_leave_the_matrix_dirty_when_the_server_refuses_the_permission_save()
    {
        var (console, api) = await OpenAsync();
        api.FailPermissionSave = "권한이 없습니다(403)";

        console.Matrix.ToggleCell(0, 1);
        var dirtyBefore = console.Matrix.DirtyCount;

        await console.ApplyAsync();

        Assert.True(console.Matrix.IsDirty);                       // 미적용 표시가 사라지지 않는다
        Assert.Equal(dirtyBefore, console.Matrix.DirtyCount);
        Assert.True(console.DetailIsDirty);
        Assert.True(console.DetailCanApply);                       // 다시 시도할 수 있다
        Assert.Equal(Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels.PermissionMatrixPanelViewModel.SaveRejectedShort, console.DetailMessage);   // 서버 원문(403 · 영문)은 싣지 않는다(A-38)
    }

    [Fact]
    public async Task should_clear_the_dirty_state_when_the_permission_save_succeeds()
    {
        var (console, api) = await OpenAsync();

        console.Matrix.ToggleCell(0, 1);
        await console.ApplyAsync();

        Assert.False(console.Matrix.IsDirty);
        Assert.Single(api.PermissionSaves);
    }

    // ── A15: 그 사이 다른 관리자가 바꿨다 ──────────────────────────────
    [Fact]
    public async Task should_send_nothing_when_the_group_changed_since_it_was_opened()
    {
        var (console, api) = await OpenAsync();
        console.Matrix.ToggleCell(0, 1);

        // 다른 관리자가 같은 그룹을 고쳤다.
        api.Groups[0].Permissions!.Modules["reports"] = new ModulePermissionDto { View = true, Edit = true };

        await console.ApplyAsync();

        Assert.Empty(api.PermissionSaves);                         // 전체 교체라 덮으면 남의 변경이 사라진다
        Assert.True(console.Matrix.IsDirty);
        Assert.Contains("다른 곳에서 먼저 바뀌어", console.DetailMessage);
    }

    // ── A2: 재조회가 선택을 놓지 않는다 ────────────────────────────────
    [Fact]
    public async Task should_keep_the_selected_group_when_the_list_is_reloaded()
    {
        var (console, _) = await OpenAsync();
        var chosen = console.Matrix.SelectedGroup;
        var moduleCount = console.Matrix.Modules.Count;

        await console.ReloadAsync();

        Assert.Same(chosen, console.Matrix.SelectedGroup);         // 행 인스턴스도 그대로다
        Assert.Equal(moduleCount, console.Matrix.Modules.Count);   // 매트릭스가 비지 않는다
    }

    [Fact]
    public async Task should_keep_the_selected_group_after_a_successful_save()
    {
        var (console, _) = await OpenAsync();
        var chosen = console.Matrix.SelectedGroup;

        console.Matrix.ToggleCell(0, 1);
        await console.ApplyAsync();

        Assert.Same(chosen, console.Matrix.SelectedGroup);
        Assert.NotEmpty(console.Matrix.Modules);
    }

    // ── A3: 칠해 둔 변경은 이동을 막는다 ───────────────────────────────
    [Fact]
    public async Task should_block_the_rail_switch_when_the_matrix_has_unapplied_changes()
    {
        var (console, _) = await OpenAsync();
        console.Matrix.ToggleCell(0, 1);

        var moved = await console.SelectRailAsync(AccountConsoleKeys.Users);

        Assert.False(moved);
        Assert.True(console.IsPermissionsRail);
        Assert.True(console.Matrix.IsDirty);
    }

    [Fact]
    public async Task should_block_the_group_switch_when_the_matrix_has_unapplied_changes()
    {
        var (console, _) = await OpenAsync();
        var first = console.Matrix.SelectedGroup;
        console.Matrix.ToggleCell(0, 1);

        console.Matrix.SelectedGroup = console.Matrix.Groups.Last();

        Assert.Same(first, console.Matrix.SelectedGroup);
        Assert.True(console.Matrix.IsDirty);
    }

    [Fact]
    public async Task should_block_the_refresh_when_the_matrix_has_unapplied_changes()
    {
        var (console, api) = await OpenAsync();
        console.Matrix.ToggleCell(0, 1);
        var before = console.Matrix.DirtyCount;

        await console.ReloadAsync();

        Assert.Equal(before, console.Matrix.DirtyCount);
        Assert.Empty(api.PermissionSaves);
    }

    [Fact]
    public async Task should_allow_the_group_switch_after_reverting()
    {
        var (console, _) = await OpenAsync();
        console.Matrix.ToggleCell(0, 1);
        console.Revert();

        var other = console.Matrix.Groups.Last();
        console.Matrix.SelectedGroup = other;

        Assert.Same(other, console.Matrix.SelectedGroup);
    }

    [Fact]
    public async Task should_drop_unapplied_matrix_changes_when_the_console_closes()
    {
        var (console, api) = await OpenAsync();
        console.Matrix.ToggleCell(0, 1);

        await console.DeactivateForTestAsync();

        Assert.Empty(api.PermissionSaves);                          // 닫을 때는 보내지 않는다
        Assert.False(console.Matrix.IsDirty);
    }

    // ── A13: 만질 수 없는 칸 ──────────────────────────────────────────
    [Fact]
    public async Task should_refuse_to_set_a_cell_the_module_does_not_support()
    {
        var (console, _) = await OpenAsync();
        var row = console.Matrix.Modules.First(m => !m.ControlEnabled);
        var index = console.Matrix.Modules.IndexOf(row);

        console.Matrix.Set(new PermissionCell(index, 3), true);

        Assert.False(row.Control);
        Assert.False(console.Matrix.IsDirty);
    }

    [Fact]
    public async Task should_refuse_every_cell_when_the_account_may_not_edit()
    {
        var (console, _) = await OpenAsync();
        var permission = PermissionOf(console);
        permission.Role = Ironwall.Dotnet.Libraries.Enums.EnumUserRole.USER;   // ADMIN 아님 → users:edit 없음

        console.Matrix.Set(new PermissionCell(0, 0), true);

        Assert.False(console.Matrix.IsDirty);
        Assert.False(console.Matrix.CanEdit);
    }

    // ── 목업 대응(실집행 꼬리표) ───────────────────────────────────────
    [Fact]
    public async Task should_mark_only_devices_and_users_as_server_enforced_control()
    {
        var (console, _) = await OpenAsync();

        Assert.True(console.Matrix.Modules.Single(m => m.ModuleKey == "devices").IsControlServerEnforced);
        Assert.True(console.Matrix.Modules.Single(m => m.ModuleKey == "users").IsControlServerEnforced);
        Assert.False(console.Matrix.Modules.Single(m => m.ModuleKey == "reports").IsControlServerEnforced);
    }

    // 저장 방식 안내("모듈 N종 전부를 한 번에 보냅니다" · "전체 교체 저장")는 운영자 화면에서 뺐다
    // (원장 D-2026-09-26-648420 · 감사 A-2 · A-3) — 상태 띠는 켜진 모듈 수와 사선 칸의 뜻만 말한다.
    [Fact]
    public async Task should_show_enabled_module_count_without_transport_notes_when_a_group_is_open()
    {
        var (console, _) = await OpenAsync();

        Assert.StartsWith("켜진 모듈 ", console.MatrixStatusText);
        Assert.DoesNotContain("전체 교체", console.MatrixStatusText);
        Assert.DoesNotContain("키", console.MatrixStatusText);
    }

    private static FakePermissionService PermissionOf(TestAccountConsole console)
        => (FakePermissionService)console.GetType().BaseType!
            .GetField("_permission", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(console)!;

    private static async Task<(TestAccountConsole Console, FakeAccountApi Api)> OpenAsync()
    {
        var permissions = new PermissionsDto();
        permissions.Modules["devices"] = new ModulePermissionDto { View = true };

        var groups = new[]
        {
            new UserGroupDto { Id = 10, Name = "관제 운영", IsActive = true, Permissions = permissions },
            new UserGroupDto { Id = 11, Name = "정비", IsActive = true, Permissions = new PermissionsDto() },
        };

        var (console, api, _, _, _) = ConsoleFixtures.Build(groups: groups);
        await console.ActivateForTestAsync();
        await console.SelectRailAsync(AccountConsoleKeys.Permissions);
        console.Matrix.SelectedGroup = console.Matrix.Groups.First();
        return (console, api);
    }
}
