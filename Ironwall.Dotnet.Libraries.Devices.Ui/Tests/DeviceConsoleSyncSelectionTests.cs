using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Dashboards;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Consoles;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 장비 콘솔 — 고른 장비가 아닌 다른 장비의 변경 알림(SYNC_DEVICE)으로 목록이 바뀌어도 고른 장비 · 상세를 지킨다.
/// </summary>
/// <remarks>
/// 알림은 실제 경로(<see cref="DeviceProviderService.FetchDeviceByIdAsync"/> · <see cref="DeviceProviderService.RemoveDeviceByIdAsync"/>)로 흘린다 —
/// 새 장비는 캐시에 추가, 있는 장비는 <b>같은 모델을 제자리에서</b> 고치고, 삭제는 캐시에서 뺀다. 패널 · 콘솔은 진짜 뷰모델이다.
/// </remarks>
[Collection("CaliburnIoC")]
public class DeviceConsoleSyncSelectionTests : IDisposable
{
    private readonly TestIoCScope _ioc = new();
    public void Dispose() => _ioc.Dispose();

    private static readonly string ControllerRail = DeviceDashboardViewModel.RailKeyOf(EnumDeviceCategory.Controller);

    private sealed record Rig(DeviceDashboardViewModel Console, DeviceProviderService Sync, MockDeviceApiService Api);

    private static async Task<Rig> OpenOnControllersAsync()
    {
        var log = new MockLogService();
        var events = new EventAggregator();
        var api = new MockDeviceApiService();
        var panelService = new MockDeviceProviderService();

        var devices = new DeviceProvider();
        var groups = new DeviceGroupProvider(log);
        var controllers = new ControllerDeviceProvider(log, devices);
        var sensors = new SensorDeviceProvider(log, devices);

        var console = new DeviceDashboardViewModel(
            events, log, new DeviceTabControlViewModel(events, log),
            new ControllerDevicePanelViewModel(events, log, api, controllers, panelService),
            new SensorDevicePanelViewModel(events, log, api, sensors, controllers, panelService),
            new CameraDevicePanelViewModel(events, log, api, new CameraDeviceProvider(log, devices), panelService),
            new SpeakerDevicePanelViewModel(events, log, api, new SpeakerDeviceProvider(log, devices), panelService),
            new EnclosureDevicePanelViewModel(events, log, api, new EnclosureDeviceProvider(log, devices), panelService),
            new LampDevicePanelViewModel(events, log, api, new LampDeviceProvider(log, devices), panelService),
            new GateDevicePanelViewModel(events, log, api, new GateDeviceProvider(log, devices), panelService),
            new DeviceGroupPanelViewModel(events, log, api, groups, devices),
            devices, groups, controllers, new ServerProvider(log), api, new StubCatalog());

        // 알림을 받아 캐시를 고치는 진짜 서비스 — 콘솔과 같은 캐시를 본다.
        var sync = new DeviceProviderService(log, new MockEventAggregator(), api, devices, controllers, sensors,
            new CameraDeviceProvider(log, devices), groups, new MockServerApiService(), new ServerProvider(log));

        devices.Add(new ControllerDeviceModel { Id = 21, DeviceNumber = 1, DeviceName = "제어기 1", IpAddress = "10.0.0.1", Port = 9001 });
        devices.Add(new ControllerDeviceModel { Id = 22, DeviceNumber = 2, DeviceName = "제어기 2", IpAddress = "10.0.0.2", Port = 9002 });

        await ((IActivate)console).ActivateAsync();
        Assert.True(await console.SelectRailAsync(ControllerRail));
        return new Rig(console, sync, api);
    }

    /// <summary>STA 스레드에 디스패처 동기화 문맥을 깔고 본문을 끝까지 돌린다 — 목록 보기(ListCollectionView)는 만든 스레드에서만 원천 변경을 받는다.</summary>
    private static void RunOnSta(Func<Task> body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            var frame = new DispatcherFrame();
            dispatcher.BeginInvoke(new Func<Task>(async () =>
            {
                try { await body(); }
                catch (Exception ex) { failure = ex; }
                finally { frame.Continue = false; }
            }));
            Dispatcher.PushFrame(frame);
            dispatcher.InvokeShutdown();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "STA 스레드가 제시간에 끝나지 않았다");
        if (failure is not null) throw new Xunit.Sdk.XunitException(failure.ToString());
    }

    private static int IdOf(object row) => ((ControllerDeviceViewModel)row).Model.Id;
    private static object RowOf(DeviceDashboardViewModel console, int id) => console.Rows!.Cast<object>().Single(r => IdOf(r) == id);
    private static string NameField(DeviceDashboardViewModel console) => console.Form.Fields.Single(f => f.Key == "name_device").Text;

    private static object SelectController(DeviceDashboardViewModel console, int id)
    {
        var row = RowOf(console, id);
        Assert.True(console.OnRowsSelected(new List<object> { row }));
        Assert.Equal(ConsoleDetailState.Single, console.Detail.State);
        return row;
    }

    private static ControllerDeviceDto ControllerDto(int id, string name, int number) => new()
    {
        Id = id,
        NameDevice = name,
        NumberDevice = number,
        IpAddress = $"10.0.0.{id}",
        IpPort = 9000 + id,
    };

    [Fact]
    public void should_keep_the_selected_controller_and_its_detail_when_other_devices_are_created_by_sync() => RunOnSta(async () =>
    {
        var rig = await OpenOnControllersAsync();
        var selected = SelectController(rig.Console, 21);

        // 남이 센서 12대 + 제어기 1대를 만들었다 — 알림이 하나씩 온다.
        for (var i = 0; i < 12; i++)
        {
            rig.Api.SensorByIdDto = new SensorDeviceDto { Id = 100 + i, NumberDevice = 100 + i, NameDevice = $"센서 {i}", ControllerId = 22 };
            Assert.NotNull(await rig.Sync.FetchDeviceByIdAsync("SENSOR", 100 + i));
        }
        rig.Api.ControllerById = ControllerDto(23, "제어기 3", 3);
        Assert.NotNull(await rig.Sync.FetchDeviceByIdAsync("CONTROLLER", 23));

        Assert.Equal(3, rig.Console.Rows!.Cast<object>().Count());
        Assert.Same(selected, Assert.Single(rig.Console.Form.Rows));
        Assert.Equal("제어기 1", NameField(rig.Console));
        Assert.Equal("제어기 1", rig.Console.Detail.SingleTitle);
        Assert.Equal(ConsoleDetailState.Single, rig.Console.Detail.State);
    });

    [Fact]
    public void should_keep_the_selection_and_show_new_values_when_the_selected_controller_is_updated_by_sync() => RunOnSta(async () =>
    {
        var rig = await OpenOnControllersAsync();
        SelectController(rig.Console, 21);

        rig.Api.ControllerById = ControllerDto(21, "제어기 1 (남이 고침)", 1);
        Assert.NotNull(await rig.Sync.FetchDeviceByIdAsync("CONTROLLER", 21));

        Assert.Equal(21, IdOf(Assert.Single(rig.Console.Form.Rows)));
        Assert.Equal("제어기 1 (남이 고침)", NameField(rig.Console));
        Assert.Equal("제어기 1 (남이 고침)", rig.Console.Detail.SingleTitle);
        Assert.False(rig.Console.Detail.IsDirty);
    });

    [Fact]
    public void should_keep_the_unapplied_edit_and_say_so_when_the_selected_controller_is_updated_by_sync() => RunOnSta(async () =>
    {
        var rig = await OpenOnControllersAsync();
        SelectController(rig.Console, 21);
        rig.Console.Form.Fields.Single(f => f.Key == "name_device").Text = "내가 고치던 이름";
        Assert.True(rig.Console.Detail.IsDirty);

        rig.Api.ControllerById = ControllerDto(21, "제어기 1 (남이 고침)", 1);
        Assert.NotNull(await rig.Sync.FetchDeviceByIdAsync("CONTROLLER", 21));

        Assert.Equal(21, IdOf(Assert.Single(rig.Console.Form.Rows)));
        Assert.Equal("내가 고치던 이름", NameField(rig.Console));      // 고치던 글은 덮지 않는다
        Assert.True(rig.Console.Detail.IsDirty);
        Assert.Contains("다른 곳에서 바뀌었습니다", rig.Console.StatusText);   // 대신 알린다
    });

    [Fact]
    public void should_clear_the_selection_with_a_message_when_the_selected_controller_is_deleted_by_sync() => RunOnSta(async () =>
    {
        var rig = await OpenOnControllersAsync();
        SelectController(rig.Console, 21);

        await rig.Sync.RemoveDeviceByIdAsync("CONTROLLER", 21);

        Assert.Single(rig.Console.Rows!.Cast<object>());
        Assert.Empty(rig.Console.Form.Rows);
        Assert.Equal(ConsoleDetailState.None, rig.Console.Detail.State);
        Assert.Contains("사라졌습니다", rig.Console.StatusText);
        Assert.False(rig.Console.CanDelete);               // 안 보이는 장비를 지우지 않는다
    });

    [Fact]
    public void should_follow_the_same_controller_when_its_row_is_rebuilt_as_a_new_instance_with_the_same_id() => RunOnSta(async () =>
    {
        var rig = await OpenOnControllersAsync();
        var old = SelectController(rig.Console, 21);

        // 목록이 다시 만들어져 같은 Id 의 행이 새 인스턴스로 바뀌었다(패널의 재구성 · 교체).
        var rows = rig.Console.ControllerPanelViewModel.ViewModelProvider;
        var fresh = new ControllerDeviceViewModel(new ControllerDeviceModel { Id = 21, DeviceNumber = 1, DeviceName = "제어기 1 (새 행)", IpAddress = "10.0.0.1", Port = 9001 });
        rows[rows.IndexOf((ControllerDeviceViewModel)old)] = fresh;

        Assert.Same(fresh, Assert.Single(rig.Console.Form.Rows));
        Assert.Equal("제어기 1 (새 행)", NameField(rig.Console));
        Assert.Equal(ConsoleDetailState.Single, rig.Console.Detail.State);
    });

    [Fact]
    public void should_rebind_and_keep_the_unapplied_edit_when_the_selected_row_is_rebuilt_with_the_same_id() => RunOnSta(async () =>
    {
        var rig = await OpenOnControllersAsync();
        var old = SelectController(rig.Console, 21);
        rig.Console.Form.Fields.Single(f => f.Key == "name_device").Text = "내가 고치던 이름";

        var rows = rig.Console.ControllerPanelViewModel.ViewModelProvider;
        var fresh = new ControllerDeviceViewModel(new ControllerDeviceModel { Id = 21, DeviceNumber = 1, DeviceName = "제어기 1", IpAddress = "10.0.0.1", Port = 9001 });
        rows[rows.IndexOf((ControllerDeviceViewModel)old)] = fresh;

        Assert.Same(fresh, Assert.Single(rig.Console.Form.Rows));   // 저장은 새 인스턴스에 쓴다
        Assert.Equal("내가 고치던 이름", NameField(rig.Console));
        Assert.True(rig.Console.Detail.IsDirty);
    });

    [Fact]
    public void should_load_the_changed_values_when_reverting_after_the_selected_controller_was_updated_elsewhere() => RunOnSta(async () =>
    {
        var rig = await OpenOnControllersAsync();
        SelectController(rig.Console, 21);
        rig.Console.Form.Fields.Single(f => f.Key == "name_device").Text = "내가 고치던 이름";
        rig.Api.ControllerById = ControllerDto(21, "제어기 1 (남이 고침)", 1);
        Assert.NotNull(await rig.Sync.FetchDeviceByIdAsync("CONTROLLER", 21));

        rig.Console.Revert();

        Assert.Equal("제어기 1 (남이 고침)", NameField(rig.Console));   // 옛 글이 아니라 지금 값
        Assert.False(rig.Console.Detail.IsDirty);
        Assert.DoesNotContain("다른 곳에서 바뀌었습니다", rig.Console.StatusText);
    });

    [Fact]
    public void should_not_drop_the_detail_when_the_grid_reports_the_lost_row_before_the_list_is_matched_again() => RunOnSta(async () =>
    {
        var saved = PlatformProvider.Current;
        PlatformProvider.Current = new XamlPlatformProvider();   // 호스트처럼 맞추기를 UI 스레드 뒤로 미룬다
        try
        {
            var rig = await OpenOnControllersAsync();
            var old = SelectController(rig.Console, 21);
            var rows = rig.Console.ControllerPanelViewModel.ViewModelProvider;
            var fresh = new ControllerDeviceViewModel(new ControllerDeviceModel { Id = 21, DeviceNumber = 1, DeviceName = "제어기 1 (새 행)", IpAddress = "10.0.0.1", Port = 9001 });
            rows[rows.IndexOf((ControllerDeviceViewModel)old)] = fresh;

            // 그리드는 원천에서 빠진 고른 행을 선택에서 떼며 먼저 알린다 — 이때 폼을 비우면 뒤이은 맞추기가 고를 것이 없다.
            Assert.True(rig.Console.OnRowsSelected(new List<object>()));
            Assert.Same(old, Assert.Single(rig.Console.Form.Rows));

            await Dispatcher.Yield(DispatcherPriority.Background);

            Assert.Same(fresh, Assert.Single(rig.Console.Form.Rows));
            Assert.Equal("제어기 1 (새 행)", NameField(rig.Console));
            Assert.Equal(ConsoleDetailState.Single, rig.Console.Detail.State);
        }
        finally { PlatformProvider.Current = saved; }
    });

    private sealed class StubCatalog : ICatalogService
    {
        public bool IsLoaded => true;
        public event EventHandler? CatalogChanged { add { } remove { } }
        public Task<bool> EnsureLoadedAsync(System.Threading.CancellationToken token = default) => Task.FromResult(true);
        public Task<bool> RefreshAsync(System.Threading.CancellationToken token = default) => Task.FromResult(true);
        public CatalogTypeAxis? TypeAxis(EnumDeviceCategory category) => null;
        public IReadOnlyList<CatalogOption> TypeAxisValues(EnumDeviceCategory category) => Array.Empty<CatalogOption>();
        public bool IsTypeAxisValue(EnumDeviceCategory category, string? code) => false;
        public string TypeAxisLabel(EnumDeviceCategory category, string? code) => code ?? string.Empty;
        public IReadOnlyList<CatalogExtraAxis> ExtraAxes(EnumDeviceCategory category) => Array.Empty<CatalogExtraAxis>();
        public IReadOnlyList<CatalogOption> Vocabulary(string name, bool includeDeprecated = false, EnumDeviceCategory? appliesTo = null) => Array.Empty<CatalogOption>();
        public string LabelOf(string vocabularyName, string? code) => code ?? string.Empty;
    }
}
