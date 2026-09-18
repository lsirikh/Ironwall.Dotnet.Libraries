using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Tests.Grants;   // RecordingEventAggregator/RecordingLog (동일 어셈블리 internal)
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Moq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Tests;

/****************************************************************************
   Purpose      : API 계약 결함 회귀 방지 — FR-03(권한 저장 병합, BuildMergedModules).
                  권한 저장은 전체 교체 계약이라 서버 모듈이 하나라도 빠지면 422 다.
                  서버 enum(15) ≠ 우리 카탈로그(12) ≠ 그룹 저장분(12, 다른 12) 이라
                  "원본 ∪ 카탈로그" 로 병합해 보내야만 양쪽 다 만족한다.
                  BuildMergedModules 는 private 이므로 OnClickGroupDetail(로드) →
                  화면 편집 → OnClickSave(저장) 의 공개 경로로 관찰하고,
                  IAccountApiService.UpdateGroupPermissionsAsync 호출 인자(PermissionsDto)를
                  Moq Callback 으로 캡처해 단언한다.
   Created By   : GHLee
   Created On   : 2026-09-18
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class PermissionMatrixMergedModulesTests
{
    // 카탈로그(PermissionCatalog.Modules) 12종: devices/events/reports/cameras/users/
    // user_groups/audit_logs/servers/map/broadcast/setup_system/setup_feature.
    // 원본(서버 저장분) 12종을 설계 — 9종은 카탈로그와 겹치고 3종(billing/licenses/units)은
    // 우리가 모르는 서버 전용 모듈. 카탈로그 전용 3종(broadcast/setup_system/setup_feature)은
    // 원본에 없다. → 합집합 = 9 + 3 + 3 = 15.
    private static Dictionary<string, ModulePermissionDto> BuildOriginModules() => new()
    {
        ["devices"] = new ModulePermissionDto { View = true, Edit = false, Delete = false, Control = false },
        ["events"] = new ModulePermissionDto { View = true, Edit = true, Delete = false, Control = false },
        ["reports"] = new ModulePermissionDto { View = true, Edit = false, Delete = false, Control = false },
        ["cameras"] = new ModulePermissionDto { View = true, Edit = false, Delete = false, Control = true },
        ["users"] = new ModulePermissionDto { View = true, Edit = false, Delete = false, Control = false },
        ["user_groups"] = new ModulePermissionDto { View = true, Edit = false, Delete = false, Control = false },
        ["audit_logs"] = new ModulePermissionDto { View = true, Edit = false, Delete = false, Control = false },
        ["servers"] = new ModulePermissionDto { View = true, Edit = false, Delete = false, Control = false },
        ["map"] = new ModulePermissionDto { View = true, Edit = false, Delete = false, Control = false },
        // 서버 전용 — 우리 카탈로그가 모르는 모듈(백필). 원본 값 그대로 보존돼야 한다.
        ["billing"] = new ModulePermissionDto { View = true, Edit = true, Delete = true, Control = false },
        ["licenses"] = new ModulePermissionDto { View = true, Edit = false, Delete = false, Control = false },
        ["units"] = new ModulePermissionDto { View = false, Edit = false, Delete = false, Control = false },
    };

    private static (PermissionMatrixPanelViewModel vm, List<PermissionsDto> captured) BuildViewModel(
        Dictionary<string, ModulePermissionDto> originModules, int groupId = 1)
    {
        var api = new Mock<IAccountApiService>();
        var captured = new List<PermissionsDto>();

        var group = new UserGroupDto
        {
            Id = groupId,
            Name = "TestGroup",
            IsActive = true,
            Permissions = new PermissionsDto { Modules = originModules, DeviceGroups = null },
        };

        api.Setup(a => a.GetUserGroupsAsync(It.IsAny<CancellationToken>()))
           .ReturnsAsync(ApiListResponse<UserGroupDto>.CreateSuccess(new List<UserGroupDto> { group }));
        api.Setup(a => a.GetUsersAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(ApiListResponse<AuthUserDto>.CreateSuccess(new List<AuthUserDto>()));
        api.Setup(a => a.UpdateGroupPermissionsAsync(It.IsAny<int>(), It.IsAny<PermissionsDto>(), It.IsAny<CancellationToken>()))
           .Callback<int, PermissionsDto, CancellationToken>((_, dto, _) => captured.Add(dto))
           .ReturnsAsync(ApiResponse<UserGroupDto>.CreateSuccess(group));

        var vm = new PermissionMatrixPanelViewModel(new RecordingEventAggregator(), new RecordingLog(), api.Object);
        return (vm, captured);
    }

    /// <summary>목록 로드 → 그룹 상세 진입까지 공통 절차(저장 전 상태 준비).</summary>
    private static async Task LoadAndOpenDetailAsync(PermissionMatrixPanelViewModel vm)
    {
        await vm.OnClickReloadButton();
        vm.SelectedGroup = vm.Groups.Single();
        vm.OnClickGroupDetail();
    }

    [Fact]
    public async Task should_preserve_server_only_modules_when_saving_merged_permissions()
    {
        // Arrange — 원본에만 있는 billing/licenses/units (우리 카탈로그 미인지 모듈)
        var origin = BuildOriginModules();
        var (vm, captured) = BuildViewModel(origin);
        await LoadAndOpenDetailAsync(vm);

        // Act — 화면 편집 없이 그대로 저장
        await vm.OnClickSave();

        // Assert — 원본 전용 모듈이 원본 값 그대로 보존된다.
        var merged = Assert.Single(captured).Modules;
        Assert.True(merged.ContainsKey("billing"));
        Assert.Equal(origin["billing"].Edit, merged["billing"].Edit);
        Assert.Equal(origin["billing"].Delete, merged["billing"].Delete);
        Assert.True(merged.ContainsKey("licenses"));
        Assert.True(merged.ContainsKey("units"));
    }

    [Fact]
    public async Task should_overwrite_catalog_module_with_screen_edit_when_saving()
    {
        // Arrange
        var origin = BuildOriginModules();
        var (vm, captured) = BuildViewModel(origin);
        await LoadAndOpenDetailAsync(vm);

        var devicesRow = vm.Modules.Single(m => m.ModuleKey == "devices");
        Assert.False(devicesRow.Edit);   // 로드 시점 = 원본 값(Edit=false) 그대로 반영됐는지 선확인

        // Act — 화면에서 devices.Edit 를 켠다
        devicesRow.Edit = true;
        await vm.OnClickSave();

        // Assert — 화면 편집분이 저장 본문에 반영된다(원본 Edit=false 를 덮어씀).
        var merged = Assert.Single(captured).Modules;
        Assert.True(merged["devices"].Edit);
        Assert.True(merged["devices"].View);   // 편집하지 않은 필드는 로드된 값 유지
    }

    [Fact]
    public async Task should_produce_union_of_15_modules_when_origin_and_catalog_differ()
    {
        // Arrange — 원본 12종(9 중복 + billing/licenses/units) ∪ 카탈로그 12종(9 중복 + broadcast/setup_system/setup_feature)
        var origin = BuildOriginModules();
        var (vm, captured) = BuildViewModel(origin);
        await LoadAndOpenDetailAsync(vm);

        // Act
        await vm.OnClickSave();

        // Assert — 합집합 15종(422 MISSING_FIELD 회귀 방지 핵심 단언)
        var merged = Assert.Single(captured).Modules;
        Assert.Equal(15, merged.Count);
        Assert.True(merged.ContainsKey("broadcast"));
        Assert.True(merged.ContainsKey("setup_system"));
        Assert.True(merged.ContainsKey("setup_feature"));
    }

    [Fact]
    public async Task should_output_full_catalog_when_origin_modules_is_empty()
    {
        // Arrange — 서버 원본이 빈 그룹(신규 생성 직후 등)
        var origin = new Dictionary<string, ModulePermissionDto>();
        var (vm, captured) = BuildViewModel(origin);
        await LoadAndOpenDetailAsync(vm);

        // Act
        await vm.OnClickSave();

        // Assert — 카탈로그 12종 전량이 채워진다(빈 본문으로 422 나지 않음).
        var merged = Assert.Single(captured).Modules;
        Assert.Equal(12, merged.Count);
    }
}
