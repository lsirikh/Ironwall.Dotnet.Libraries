using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Consoles.Lists;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Tests.Consoles;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Tests.Grants;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Moq;
using System.Windows.Controls;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Tests.Scenarios;

/****************************************************************************
   Purpose   : 시나리오 카탈로그 §3.3(ACC) 중 열 · 권한 그룹 · 한시 부여 항목을 창 없이 확인한다.
   Notes     : 가짜 API(Moq · FakeGopServer)만 쓴다. 열 메뉴(ContextMenu)를 실제로 여는 일은 헤디드 몫이다.
****************************************************************************/
public class AccountPanelScenarioTests
{
    // ── ACC-008: 열 — 추가 열을 켰다가 되돌린다 ─────────────────────────────────
    [Trait("Scenario", "ACC-008")]
    [Fact]
    public void should_return_to_the_default_six_columns_when_an_added_column_is_turned_off_again()
    {
        var columns = new List<DataGridColumn>();
        foreach (var spec in AccountColumns.All)
        {
            var column = new DataGridTextColumn { Header = spec.Header };
            ConsoleColumns.SetKey(column, spec.Key);
            ConsoleColumns.SetIsDefault(column, spec.IsDefault);
            columns.Add(column);
        }
        var prefs = new ConsolePrefEntry();

        ConsoleColumns.Toggle(columns, prefs, "phone");
        Assert.Equal("열 7/11", ConsoleColumns.Apply(columns, prefs.ShowAllColumns, prefs.HiddenColumns));

        ConsoleColumns.Toggle(columns, prefs, "phone");                // 되돌림
        Assert.Equal("열 6/11", ConsoleColumns.Apply(columns, prefs.ShowAllColumns, prefs.HiddenColumns));
        Assert.Equal(System.Windows.Visibility.Collapsed,
                     columns.Single(c => ConsoleColumns.GetKey(c) == "phone").Visibility);
    }

    // ── ACC-019: 권한 그룹 CRUD — 만들기 · 중복 거부 ─────────────────────────────
    private static (PermissionMatrixPanelViewModel Panel, Mock<IAccountApiService> Api, CapturingEventAggregator Events, List<UserGroupDto> Groups) GroupPanel()
    {
        var groups = new List<UserGroupDto> { new() { Id = 10, Name = "관제", IsActive = true, Permissions = new PermissionsDto() } };
        var api = new Mock<IAccountApiService>();
        api.Setup(a => a.GetAllUserGroupsAsync(It.IsAny<CancellationToken>()))
           .ReturnsAsync(() => new ApiListResponse<UserGroupDto> { Success = true, Data = groups.ToList() });
        api.Setup(a => a.GetUserGroupsAsync(It.IsAny<CancellationToken>()))
           .ReturnsAsync(() => new ApiListResponse<UserGroupDto> { Success = true, Data = groups.ToList() });
        api.Setup(a => a.GetAllUsersAsync(It.IsAny<CancellationToken>()))
           .ReturnsAsync(new ApiListResponse<AuthUserDto> { Success = true, Data = new List<AuthUserDto>() });
        api.Setup(a => a.CreateUserGroupAsync(It.IsAny<UserGroupCreateDto>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync((UserGroupCreateDto dto, CancellationToken _) =>
           {
               if (groups.Any(g => g.Name == dto.Name))
               {
                   var conflict = ApiResponse<UserGroupDto>.CreateError("CONFLICT", "group name already exists");
                   conflict.StatusCode = 409;
                   return conflict;
               }
               var created = new UserGroupDto { Id = groups.Max(g => g.Id) + 1, Name = dto.Name, IsActive = true, Permissions = new PermissionsDto() };
               groups.Add(created);
               return ApiResponse<UserGroupDto>.CreateSuccess(created);
           });
        var events = new CapturingEventAggregator();
        return (new PermissionMatrixPanelViewModel(events, new SilentLog(), api.Object), api, events, groups);
    }

    [Trait("Scenario", "ACC-019")]
    [Fact]
    public async Task should_create_the_group_and_show_it_as_a_row_when_a_new_group_is_saved()
    {
        var (panel, _, events, _) = GroupPanel();
        await panel.OnClickReloadButton();

        panel.OnClickNewGroup();
        panel.FormName = "야간조";
        await panel.ClickSaveGroupForm();

        Assert.Equal("새 권한 그룹을 생성했습니다.", events.OfType<OpenInfoPopupMessageModel>().Last().Explain);
        Assert.Contains(panel.Groups, g => g.GroupName == "야간조");
        Assert.False(panel.IsGroupFormOpen);
    }

    [Trait("Scenario", "ACC-019")]
    [Fact]
    public async Task should_refuse_a_duplicate_group_name_in_plain_korean_and_keep_the_form_open()
    {
        var (panel, _, events, groups) = GroupPanel();
        await panel.OnClickReloadButton();

        panel.OnClickNewGroup();
        panel.FormName = "관제";
        await panel.ClickSaveGroupForm();

        Assert.Equal("같은 이름의 권한 그룹이 이미 있습니다. 다른 이름을 입력해 주세요.", events.OfType<OpenInfoPopupMessageModel>().Last().Explain);
        Assert.True(panel.IsGroupFormOpen);                            // 고칠 수 있게 폼을 둔다
        Assert.Single(groups);
    }

    // ── ACC-027: 부여 — 만들기 → 행 · 이름, 회수 ─────────────────────────────────
    [Trait("Scenario", "ACC-027")]
    [Fact]
    public async Task should_list_the_new_grant_with_names_and_revoke_it_after_confirmation()
    {
        var server = GrantFixtures.NewServer();
        var (vm, ea, _) = GrantFixtures.NewVm(server);
        await vm.ActivateForTestAsync();
        vm.SelectedAccount = vm.Accounts.First(a => a.LoginId == "op1");
        vm.SelectedGroup = vm.Groups.First(g => g.Name == "야간조");
        vm.ValidUntil = GrantFixtures.T0.AddDays(1);

        await vm.ClickCreateGrant();

        var row = Assert.Single(vm.Grants);
        Assert.Equal("op1", row.UserLogin);
        Assert.Equal("야간조", row.GroupName);

        await vm.OnClickRevoke(row);
        var confirm = ea.Last<OpenConfirmPopupMessageModel>()!;
        var call = Assert.IsType<CallRevokeGrantMessageModel>(confirm.MessageModel);
        Assert.Null(server.GrantRows[0].RevokedAt);                    // 묻기만 했다

        await vm.HandleAsync(call, CancellationToken.None);

        Assert.NotNull(server.GrantRows[0].RevokedAt);
    }

    // ── ACC-028: 부여 날짜 — 끝 < 시작 ──────────────────────────────────────────
    [Trait("Scenario", "ACC-028")]
    [Fact]
    public async Task should_refuse_a_grant_that_ends_before_it_starts()
    {
        var server = GrantFixtures.NewServer();
        var (vm, ea, _) = GrantFixtures.NewVm(server);
        await vm.ActivateForTestAsync();
        vm.SelectedAccount = vm.Accounts.First(a => a.LoginId == "op1");
        vm.SelectedGroup = vm.Groups.First(g => g.Name == "야간조");
        vm.ValidFrom = GrantFixtures.T0.AddDays(2);
        vm.ValidUntil = GrantFixtures.T0.AddDays(1);                   // 시작보다 앞

        await vm.ClickCreateGrant();

        Assert.Equal(0, server.CreateCallCount);
        Assert.Equal("종료 일시는 시작 일시보다 뒤여야 합니다.", ea.Last<OpenInfoPopupMessageModel>()!.Explain);
    }
}
