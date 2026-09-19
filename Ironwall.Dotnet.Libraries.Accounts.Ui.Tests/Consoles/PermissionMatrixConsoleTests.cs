using Ironwall.Dotnet.Libraries.Accounts.Ui.Consoles;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Consoles.Matrix;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Tests.Consoles;

/// <summary>
/// 권한 설정 — 세 뷰 교대를 없애고 목록 + 상세 한 화면으로(L1238 · L-D7 L1648).
/// 저장은 <b>전체 교체 1회</b>이고 본문은 원본 ∪ 화면이다(L1220 · L1233 · 422 함정).
/// </summary>
public class PermissionMatrixConsoleTests
{
    [Fact]
    public async Task should_list_the_groups_in_the_middle_and_load_the_matrix_into_the_detail()
    {
        var console = await OpenAsync();

        Assert.True(console.IsPermissionsRail);
        Assert.Equal(2, console.Matrix.Groups.Count);

        console.Matrix.SelectedGroup = console.Matrix.Groups.First();

        Assert.NotEmpty(console.Matrix.Modules);
        Assert.True(console.Matrix.ShowMatrix);
        Assert.False(console.Matrix.ShowMembers);
        Assert.Equal("관제 운영", console.Matrix.GroupTitle);
    }

    [Fact]
    public async Task should_save_once_with_every_module_when_applying_the_matrix()
    {
        var console = await OpenAsync();
        var api = ApiOf(console);
        console.Matrix.SelectedGroup = console.Matrix.Groups.First();
        var moduleCount = console.Matrix.Modules.Count;

        console.Matrix.ToggleCell(0, 1);                 // 편집 하나를 켠다
        Assert.True(console.DetailIsDirty);

        await console.ApplyAsync();

        Assert.Single(api.PermissionSaves);              // 전체 교체 1회
        Assert.Equal(moduleCount, api.PermissionSaves[0].Dto.Modules.Count);
    }

    [Fact]
    public async Task should_keep_a_server_module_we_do_not_know_in_the_saved_body()
    {
        var console = await OpenAsync(withUnknownModule: true);
        var api = ApiOf(console);
        console.Matrix.SelectedGroup = console.Matrix.Groups.First();

        Assert.Contains(console.Matrix.Modules, m => m.ModuleKey == "quantum_shield");

        console.Matrix.ToggleCell(0, 0);
        await console.ApplyAsync();

        Assert.True(api.PermissionSaves[0].Dto.Modules.ContainsKey("quantum_shield"));
    }

    [Fact]
    public async Task should_warn_before_saving_when_an_unknown_server_module_is_present()
    {
        var console = await OpenAsync(withUnknownModule: true);
        console.Matrix.SelectedGroup = console.Matrix.Groups.First();

        Assert.True(console.Matrix.HasCatalogWarning);
        Assert.Contains("사전에 없는 서버 모듈", console.DetailBanner);
    }

    [Fact]
    public async Task should_count_every_changed_cell_as_one_unapplied_change()
    {
        var console = await OpenAsync();
        console.Matrix.SelectedGroup = console.Matrix.Groups.First();

        console.Matrix.ToggleRow(0);
        var afterRow = console.Matrix.DirtyCount;

        Assert.True(afterRow >= 1);
        Assert.Contains($"변경 {afterRow}건 미적용", console.DetailFooter);
    }

    [Fact]
    public async Task should_restore_the_loaded_values_when_reverting_the_matrix()
    {
        var console = await OpenAsync();
        var api = ApiOf(console);
        console.Matrix.SelectedGroup = console.Matrix.Groups.First();

        console.Matrix.ToggleVerb(0);
        Assert.True(console.Matrix.IsDirty);

        console.Revert();

        Assert.False(console.Matrix.IsDirty);
        Assert.Empty(api.PermissionSaves);        // 되돌리기는 서버를 부르지 않는다
    }

    [Fact]
    public async Task should_toggle_the_whole_column_when_the_header_button_is_used()
    {
        var console = await OpenAsync();
        console.Matrix.SelectedGroup = console.Matrix.Groups.First();

        Assert.True(console.Matrix.ToggleVerb(0));

        Assert.All(console.Matrix.Modules.Where(m => m.ViewEnabled), m => Assert.True(m.View));
    }

    [Fact]
    public async Task should_show_the_full_replacement_notice_in_the_status_bar()
    {
        var console = await OpenAsync();
        console.Matrix.SelectedGroup = console.Matrix.Groups.First();

        Assert.Contains("전체 교체 저장", console.MatrixStatusText);
        Assert.Contains("모듈", console.MatrixStatusText);
    }

    [Fact]
    public async Task should_switch_the_detail_to_members_when_the_chip_is_pressed()
    {
        var console = await OpenAsync();
        console.Matrix.SelectedGroup = console.Matrix.Groups.First();

        console.Matrix.ShowMembers = true;

        Assert.False(console.Matrix.ShowMatrix);
        Assert.True(console.Matrix.ShowMembers);
    }

    private static FakeAccountApi ApiOf(TestAccountConsole console)
        => (FakeAccountApi)console.PermissionMatrixPanelViewModel.GetType()
            .GetField("_api", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(console.PermissionMatrixPanelViewModel)!;

    private static async Task<TestAccountConsole> OpenAsync(bool withUnknownModule = false)
    {
        var permissions = new PermissionsDto();
        permissions.Modules["devices"] = new ModulePermissionDto { View = true };
        if (withUnknownModule) permissions.Modules["quantum_shield"] = new ModulePermissionDto { View = true, Edit = true };

        var groups = new[]
        {
            new UserGroupDto { Id = 10, Name = "관제 운영", IsActive = true, Permissions = permissions },
            new UserGroupDto { Id = 11, Name = "정비", IsActive = true, Permissions = new PermissionsDto() },
        };

        var (console, _, _, _, _) = ConsoleFixtures.Build(groups: groups);
        await console.ActivateForTestAsync();
        await console.SelectRailAsync(AccountConsoleKeys.Permissions);
        return console;
    }
}
