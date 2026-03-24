using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Gateway.Providers;
using Ironwall.Dotnet.Libraries.Gateway.ViewModels;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.GatewayEvents;
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
    public void LoadDeviceGroups_PopulatesCollection()
    {
        // Arrange
        var dgProvider = CreateDeviceGroupProvider((1, "A구역"), (2, "B구역"));
        var vm = CreateViewModel(dgProvider);

        // Act
        vm.LoadDeviceGroups();

        // Assert — "미지정" + 2개 = 3개
        Assert.Equal(3, vm.DeviceGroups.Count);
        Assert.Contains(vm.DeviceGroups, g => g.Id == 1 && g.Name == "A구역");
        Assert.Contains(vm.DeviceGroups, g => g.Id == 2 && g.Name == "B구역");
    }

    #endregion

    #region - Test 2.2: "미지정" 항목 -

    [Fact]
    [Trait("Category", "GatewaySetupVM")]
    public void LoadDeviceGroups_FirstItemIsUnassigned()
    {
        // Arrange
        var dgProvider = CreateDeviceGroupProvider((1, "A구역"), (2, "B구역"));
        var vm = CreateViewModel(dgProvider);

        // Act
        vm.LoadDeviceGroups();

        // Assert
        Assert.Equal(0, vm.DeviceGroups[0].Id);
        Assert.Equal("미지정", vm.DeviceGroups[0].Name);
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

        // Assert — 신규 이벤트 기본 Group은 0 (미지정)
        Assert.Equal(0, newEvent.Group);
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
        var model = new GatewayEventModel { Group = 1 };
        var vm = new GatewayEventViewModel(model) { DeviceGroups = deviceGroups };

        // Assert
        Assert.Equal("A구역", vm.GroupName);
    }

    [Fact]
    [Trait("Category", "GatewaySetupVM")]
    public void GroupName_UnknownGroup_FallsBackToNumber()
    {
        // Arrange — DeviceGroups가 없으면 숫자 표시
        EnsureIoC();
        var model = new GatewayEventModel { Group = 99 };
        var vm = new GatewayEventViewModel(model);

        // Assert
        Assert.Equal("99", vm.GroupName);
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
