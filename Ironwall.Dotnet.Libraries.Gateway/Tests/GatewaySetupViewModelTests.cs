using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Gateway.Providers;
using Ironwall.Dotnet.Libraries.Gateway.ViewModels;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.GatewayEvents;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Gateway.Tests;

/// <summary>
/// GatewaySetupViewModel 단위 테스트 — DeviceGroup ComboBox 연동
/// </summary>
public class GatewaySetupViewModelTests
{
    #region - Test Helpers -

    private static DeviceGroupProvider CreateDeviceGroupProvider(params (int Id, string Name)[] groups)
    {
        var log = new LogService();
        var provider = new DeviceGroupProvider(log);
        foreach (var (id, name) in groups)
        {
            provider.Add(new DeviceGroupModel { Id = id, Name = name });
        }
        return provider;
    }

    #endregion

    #region - Test 2.1: DeviceGroups 로드 -

    [Fact]
    [Trait("Category", "GatewaySetupVM")]
    public void LoadAllDeviceGroups_PopulatesCollection()
    {
        // Arrange
        var dgProvider = CreateDeviceGroupProvider((1, "A구역"), (2, "B구역"));
        var vm = CreateViewModel(dgProvider);

        // Act
        vm.LoadAllDeviceGroups();

        // Assert — "미지정" + 2개 = 3개
        Assert.Equal(3, vm.AllDeviceGroups.Count);
        Assert.Contains(vm.AllDeviceGroups, g => g.Id == 1 && g.Name == "A구역");
        Assert.Contains(vm.AllDeviceGroups, g => g.Id == 2 && g.Name == "B구역");
    }

    #endregion

    #region - Test 2.2: "미지정" 항목 -

    [Fact]
    [Trait("Category", "GatewaySetupVM")]
    public void LoadAllDeviceGroups_FirstItemIsUnassigned()
    {
        // Arrange
        var dgProvider = CreateDeviceGroupProvider((1, "A구역"), (2, "B구역"));
        var vm = CreateViewModel(dgProvider);

        // Act
        vm.LoadAllDeviceGroups();

        // Assert
        Assert.Equal(0, vm.AllDeviceGroups[0].Id);
        Assert.Equal("미지정", vm.AllDeviceGroups[0].Name);
    }

    #endregion

    #region - Test 2.3: 신규 이벤트 기본 Group=0 -

    [Fact]
    [Trait("Category", "GatewaySetupVM")]
    public void InsertButton_NewEvent_GroupIsZero()
    {
        // Arrange
        var dgProvider = CreateDeviceGroupProvider((1, "A구역"));
        var vm = CreateViewModel(dgProvider);

        // Act — OnClickInsertButton은 WPF 인프라 의존 → CreateNewEvent() 분리 후 검증
        var newEvent = vm.CreateNewEvent();

        // Assert — 신규 이벤트 기본 DeviceGroups는 빈 목록 (미지정)
        Assert.Empty(newEvent.DeviceGroups);
    }

    #endregion

    #region - Test 3.2: GroupName 읽기 전용 프로퍼티 -

    [Fact]
    [Trait("Category", "GatewaySetupVM")]
    public void GroupName_ReturnsDeviceGroupName()
    {
        // Arrange
        EnsureIoC();
        var deviceGroups = new ObservableCollection<IDeviceGroupModel>
        {
            new DeviceGroupModel { Id = 0, Name = "미지정" },
            new DeviceGroupModel { Id = 1, Name = "A구역" },
            new DeviceGroupModel { Id = 2, Name = "B구역" }
        };
        var model = new GatewayEventModel { DeviceGroups = new List<int> { 1 } };
        var vm = new GatewayEventViewModel(model) { AllGroups = deviceGroups };

        // Assert
        Assert.Equal("A구역", vm.GroupNames);
    }

    [Fact]
    [Trait("Category", "GatewaySetupVM")]
    public void GroupName_UnknownGroup_FallsBackToNumber()
    {
        // Arrange — DeviceGroups가 없으면 숫자 표시
        EnsureIoC();
        var model = new GatewayEventModel { DeviceGroups = new List<int> { 99 } };
        var vm = new GatewayEventViewModel(model);

        // Assert
        Assert.Equal("99", vm.GroupNames);
    }

    // TEST-03: GroupNames 빈 목록 → "미지정"
    [Fact]
    [Trait("Category", "GatewaySetupVM")]
    public void should_return_미지정_when_device_groups_empty()
    {
        EnsureIoC();
        var model = new GatewayEventModel { DeviceGroups = new List<int>() };
        var vm = new GatewayEventViewModel(model);

        Assert.Equal("미지정", vm.GroupNames);
    }

    // TEST-03: GroupNames 다중 그룹 콤마 조인
    [Fact]
    [Trait("Category", "GatewaySetupVM")]
    public void should_return_comma_joined_names_for_multiple_groups()
    {
        EnsureIoC();
        var deviceGroups = new ObservableCollection<IDeviceGroupModel>
        {
            new DeviceGroupModel { Id = 1, Name = "A구역" },
            new DeviceGroupModel { Id = 2, Name = "B구역" }
        };
        var model = new GatewayEventModel { DeviceGroups = new List<int> { 1, 2 } };
        var vm = new GatewayEventViewModel(model) { AllGroups = deviceGroups };

        Assert.Equal("A구역, B구역", vm.GroupNames);
    }

    // TEST-03: 알 수 없는 그룹 Id는 숫자 문자열로 표시
    [Fact]
    [Trait("Category", "GatewaySetupVM")]
    public void should_return_id_string_when_group_not_found_in_all_groups()
    {
        EnsureIoC();
        var model = new GatewayEventModel { DeviceGroups = new List<int> { 42 } };
        var vm = new GatewayEventViewModel(model);

        Assert.Equal("42", vm.GroupNames);
    }

    #endregion

    #region - Test 4: GatewayEventEquals —HashSet.SetEquals 비교 -

    // TEST-02: 그룹 목록이 다르면 변경 감지
    [Fact]
    [Trait("Category", "GatewaySetupVM")]
    public void should_detect_change_when_groups_differ()
    {
        var vm = CreateViewModel(CreateDeviceGroupProvider());
        var a = new GatewayEventModel { Id = 1, EventName = "X", IsEnable = true, Description = "", DeviceGroups = new List<int> { 1 } };
        var b = new GatewayEventModel { Id = 1, EventName = "X", IsEnable = true, Description = "", DeviceGroups = new List<int> { 2 } };

        Assert.False(vm.GatewayEventEquals(a, b));
    }

    // TEST-02: 순서가 다른 동일 그룹은 변경 없음으로 판단
    [Fact]
    [Trait("Category", "GatewaySetupVM")]
    public void should_not_detect_change_when_groups_same_different_order()
    {
        var vm = CreateViewModel(CreateDeviceGroupProvider());
        var a = new GatewayEventModel { Id = 1, EventName = "X", IsEnable = true, Description = "", DeviceGroups = new List<int> { 1, 2, 3 } };
        var b = new GatewayEventModel { Id = 1, EventName = "X", IsEnable = true, Description = "", DeviceGroups = new List<int> { 3, 1, 2 } };

        Assert.True(vm.GatewayEventEquals(a, b));
    }

    // TEST-02: 빈 목록과 비어있지 않은 목록은 변경으로 감지
    [Fact]
    [Trait("Category", "GatewaySetupVM")]
    public void should_detect_change_when_groups_empty_vs_nonempty()
    {
        var vm = CreateViewModel(CreateDeviceGroupProvider());
        var a = new GatewayEventModel { Id = 1, EventName = "X", IsEnable = true, Description = "", DeviceGroups = new List<int>() };
        var b = new GatewayEventModel { Id = 1, EventName = "X", IsEnable = true, Description = "", DeviceGroups = new List<int> { 1 } };

        Assert.False(vm.GatewayEventEquals(a, b));
    }

    #endregion

    #region - Helper -

    private static void EnsureIoC()
    {
        IoC.GetInstance = (type, _) =>
        {
            if (type == typeof(IEventAggregator)) return new EventAggregator();
            if (type == typeof(ILogService)) return new LogService();
            return null!;
        };
    }

    private static GatewaySetupViewModel CreateViewModel(DeviceGroupProvider dgProvider)
    {
        var log = new LogService();
        var ea = new Caliburn.Micro.EventAggregator();
        var eventProvider = new GatewayEventProvider(log);
        // GatewayDbService requires DB — pass null stub for unit test
        return new GatewaySetupViewModel(ea, log, new StubGatewayDbService(), eventProvider, dgProvider);
    }

    #endregion
}

/// <summary>DB 불필요한 단위 테스트용 스텁</summary>
file class StubGatewayDbService : Services.IGatewayDbService
{
    public bool IsConnected => false;
    public Task ExecuteAsync(CancellationToken token = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken token = default) => Task.CompletedTask;
    public Task BuildSchemeAsync(CancellationToken token = default) => Task.CompletedTask;
    public Task Connect(CancellationToken token = default) => Task.CompletedTask;
    public Task<bool> DeleteGatewayEventAsync(IGatewayEventModel model, CancellationToken token = default) => Task.FromResult(true);
    public Task Disconnect(CancellationToken token = default) => Task.CompletedTask;
    public Task<IGatewayEventModel?> FetchGatewayEventAsync(int id, CancellationToken token = default) => Task.FromResult<IGatewayEventModel?>(null);
    public Task<List<IGatewayEventModel>?> FetchGatewayEventsAsync(CancellationToken token = default) => Task.FromResult<List<IGatewayEventModel>?>(new());
    public Task FetchInstanceAsync(CancellationToken token = default) => Task.CompletedTask;
    public Task<IGatewayEventModel?> InsertGatewayEventAsync(IGatewayEventModel model, CancellationToken token = default) => Task.FromResult<IGatewayEventModel?>(model);
    public Task StartService(CancellationToken token = default) => Task.CompletedTask;
    public Task StopService(CancellationToken token = default) => Task.CompletedTask;
    public Task<IGatewayEventModel?> UpdateGatewayEventAsync(IGatewayEventModel model, CancellationToken token = default) => Task.FromResult<IGatewayEventModel?>(model);
}
