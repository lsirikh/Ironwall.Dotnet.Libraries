using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Tests.Grants;   // RecordingEventAggregator/RecordingLog
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Tests;

/****************************************************************************
   Purpose      : G05 (GOP API 8.0.15 대조) — 그룹 권한 저장 뼈대 = available_modules ∪ 원본 ∪ 편집분.
                  칸이 없는 6.3.2 는 종전(원본 ∪ 세대 어휘). 422 MISSING_FIELD 면 빠진 키를 끈 채로 1회 재시도.
   Created By   : Claude
   Created On   : 2026-10-21
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public class PermissionMatrixAvailableModulesTests
{
    private static Dictionary<string, ModulePermissionDto> Origin() => new()
    {
        ["devices"] = new ModulePermissionDto { View = true },
        ["events"] = new ModulePermissionDto { View = true, Edit = true },
        ["legacy_only"] = new ModulePermissionDto { View = true },   // 원본에만 있는 키 — 그대로 보존
    };

    private static (PermissionMatrixPanelViewModel vm, List<PermissionsDto> captured, Mock<IAccountApiService> api) Build(
        List<string>? available, params ApiResponse<UserGroupDto>[] saveResults)
    {
        var api = new Mock<IAccountApiService>();
        var captured = new List<PermissionsDto>();
        var group = new UserGroupDto { Id = 1, Name = "야간조", IsActive = true, Permissions = new PermissionsDto { Modules = Origin() } };

        api.Setup(a => a.GetUserGroupsAsync(It.IsAny<CancellationToken>()))
           .ReturnsAsync(ApiListResponse<UserGroupDto>.CreateSuccess(new List<UserGroupDto> { group }));
        api.Setup(a => a.GetAllUserGroupsAsync(It.IsAny<CancellationToken>()))
           .ReturnsAsync(ApiListResponse<UserGroupDto>.CreateSuccess(new List<UserGroupDto> { group }));
        api.Setup(a => a.GetAllUsersAsync(It.IsAny<CancellationToken>()))
           .ReturnsAsync(ApiListResponse<AuthUserDto>.CreateSuccess(new List<AuthUserDto>()));
        api.Setup(a => a.GetMyPermissionsAsync(It.IsAny<CancellationToken>()))
           .ReturnsAsync(ApiResponse<PermissionsSnapshotDto>.CreateSuccess(new PermissionsSnapshotDto { AvailableModules = available }));

        var call = 0;
        api.Setup(a => a.UpdateGroupPermissionsAsync(It.IsAny<int>(), It.IsAny<PermissionsDto>(), It.IsAny<CancellationToken>()))
           .Callback<int, PermissionsDto, CancellationToken>((_, dto, _) =>
               captured.Add(new PermissionsDto { Modules = new Dictionary<string, ModulePermissionDto>(dto.Modules) }))
           .ReturnsAsync(() => saveResults.Length == 0 ? ApiResponse<UserGroupDto>.CreateSuccess(group) : saveResults[System.Math.Min(call++, saveResults.Length - 1)]);

        return (new PermissionMatrixPanelViewModel(new RecordingEventAggregator(), new RecordingLog(), api.Object), captured, api);
    }

    private static async Task OpenAsync(PermissionMatrixPanelViewModel vm)
    {
        await vm.OnClickReloadButton();
        vm.SelectedGroup = vm.Groups.Single();
        vm.OnClickGroupDetail();
    }

    [Fact]
    public async Task should_send_every_available_module_including_a_17th_one_the_client_does_not_know()
    {
        var available = new List<string> { "devices", "events", "reports", "cameras", "users", "user_groups", "audit_logs", "servers",
            "map", "broadcast", "setup_system", "setup_feature", "action_report_templates", "files", "integrations", "units", "future_module" };
        var (vm, captured, _) = Build(available);
        await OpenAsync(vm);

        await vm.OnClickSave();

        var sent = Assert.Single(captured).Modules;
        foreach (var key in available) Assert.True(sent.ContainsKey(key), key);
        var future = sent["future_module"];                   // 원본에 없는 키는 4동작 모두 끈 채로
        Assert.False(future.View || future.Edit || future.Delete || future.Control);
        Assert.True(sent["events"].Edit);                     // 원본 값은 그대로
        Assert.True(sent.ContainsKey("legacy_only"));         // 원본에만 있는 키도 떨어뜨리지 않는다
    }

    [Fact]
    public async Task should_keep_the_old_union_when_the_snapshot_has_no_available_modules_like_6_3()
    {
        var (vm, captured, _) = Build(available: null);
        await OpenAsync(vm);

        await vm.OnClickSave();

        var sent = Assert.Single(captured).Modules;
        Assert.False(sent.ContainsKey("future_module"));
        Assert.True(sent.ContainsKey("legacy_only"));
        Assert.True(sent.ContainsKey("devices"));
    }

    [Fact]
    public async Task should_fill_the_missing_module_with_false_and_retry_once_on_422_missing_field()
    {
        var rejected = ApiResponse<UserGroupDto>.CreateError(ApiErrorCodes.ValidationError, "요청 검증에 실패했습니다");
        rejected.StatusCode = 422;
        rejected.Error!.DetailsToken = JArray.Parse("[{\"field\":\"modules.patrol\",\"code\":\"MISSING_FIELD\",\"message\":\"'modules.patrol' 가 없습니다\"}]");
        var ok = ApiResponse<UserGroupDto>.CreateSuccess(new UserGroupDto { Id = 1 });
        var (vm, captured, api) = Build(available: null, rejected, ok);
        await OpenAsync(vm);

        var outcome = await vm.SaveGroupPermissionsAsync();

        Assert.True(outcome.Success);
        Assert.Equal(2, captured.Count);
        Assert.False(captured[0].Modules.ContainsKey("patrol"));
        var patrol = captured[1].Modules["patrol"];
        Assert.False(patrol.View || patrol.Edit || patrol.Delete || patrol.Control);
    }

    [Fact]
    public async Task should_not_retry_more_than_once_when_the_server_keeps_saying_missing_field()
    {
        var rejected = ApiResponse<UserGroupDto>.CreateError(ApiErrorCodes.ValidationError, "요청 검증에 실패했습니다");
        rejected.StatusCode = 422;
        rejected.Error!.DetailsToken = JArray.Parse("[{\"field\":\"modules.patrol\",\"code\":\"MISSING_FIELD\"}]");
        var (vm, captured, _) = Build(available: null, rejected);
        await OpenAsync(vm);

        var outcome = await vm.SaveGroupPermissionsAsync();

        Assert.False(outcome.Success);
        Assert.Equal(2, captured.Count);
    }
}
