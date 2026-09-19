using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Dashboards;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Consoles;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 장비 콘솔 뷰모델(device-console-redesign FR-02 · FR-03 · FR-10 ~ FR-12) — 진짜 패널 뷰모델 8개를 가짜 API 위에 세워 본다.
/// 계약은 6.3(컨테이너 미구성의 기본값)이라 레일은 8개다.
/// </summary>
[Collection("CaliburnIoC")]
public class DeviceDashboardConsoleTests : IDisposable
{
    private readonly TestIoCScope _ioc = new();
    public void Dispose() => _ioc.Dispose();

    private static readonly string LampRail = DeviceDashboardViewModel.RailKeyOf(EnumDeviceCategory.Lamp);

    private static async Task<(DeviceDashboardViewModel Console, LampDevicePanelViewModel Lamps)> OpenAsync()
    {
        var log = new MockLogService();
        var events = new EventAggregator();
        var api = new MockDeviceApiService();
        var providerService = new MockDeviceProviderService();

        var devices = new DeviceProvider();
        var groups = new DeviceGroupProvider(log);
        var controllers = new ControllerDeviceProvider(log, devices);
        var lamps = new LampDevicePanelViewModel(events, log, api, new LampDeviceProvider(log, devices), providerService);

        var console = new DeviceDashboardViewModel(
            events, log, new DeviceTabControlViewModel(events, log),
            new ControllerDevicePanelViewModel(events, log, api, controllers, providerService),
            new SensorDevicePanelViewModel(events, log, api, new SensorDeviceProvider(log, devices), controllers, providerService),
            new CameraDevicePanelViewModel(events, log, api, new CameraDeviceProvider(log, devices), providerService),
            new SpeakerDevicePanelViewModel(events, log, api, new SpeakerDeviceProvider(log, devices), providerService),
            new EnclosureDevicePanelViewModel(events, log, api, new EnclosureDeviceProvider(log, devices), providerService),
            lamps,
            new GateDevicePanelViewModel(events, log, api, new GateDeviceProvider(log, devices), providerService),
            new DeviceGroupPanelViewModel(events, log, api, groups, devices),
            devices, groups, controllers, new ServerProvider(log), api, new StubCatalog());

        // 카테고리별 프로바이더는 만들어진 뒤의 추가만 따라간다.
        groups.Add(new DeviceGroupModel { Id = 1, Name = "정문" });
        devices.Add(new LampDeviceModel { Id = 11, DeviceNumber = 1, DeviceName = "경광등 1", Status = EnumDeviceStatus.ACTIVATED });
        devices.Add(new LampDeviceModel { Id = 12, DeviceNumber = 2, DeviceName = "경광등 2", Status = EnumDeviceStatus.ERROR });
        devices.Add(new ControllerDeviceModel { Id = 21, DeviceNumber = 1, DeviceName = "제어기 1" });

        await ((IActivate)console).ActivateAsync();
        return (console, lamps);
    }

    private static List<object> RowsOf(DeviceDashboardViewModel console) => console.Rows!.Cast<object>().ToList();

    [Fact]
    public async Task should_list_groups_and_seven_categories_with_live_counts_when_opened()
    {
        var (console, _) = await OpenAsync();

        Assert.Equal(8, console.RailEntries.Count);   // 6.3 계약 — 부품으로 찾기는 나오지 않는다
        Assert.Equal(DeviceDashboardViewModel.GroupsRailKey, console.RailEntries[0].Key);
        Assert.Equal(DeviceDashboardViewModel.GroupsRailKey, console.SelectedRail!.Key);

        var lamps = console.RailEntries.Single(e => e.Key == LampRail);
        Assert.Equal((2, 1), (lamps.Count, lamps.BadCount));
        Assert.Contains("전체 3대", console.RailFooterText);
        Assert.Contains("장애 1대", console.RailFooterText);
    }

    [Fact]
    public async Task should_update_rail_badge_immediately_when_a_device_is_added()
    {
        var (console, _) = await OpenAsync();

        console.DeviceProvider.Add(new LampDeviceModel { Id = 13, DeviceNumber = 3, DeviceName = "경광등 3" });

        Assert.Equal(3, console.RailEntries.Single(e => e.Key == LampRail).Count);
    }

    [Fact]
    public async Task should_show_rows_and_columns_of_the_category_when_rail_is_switched()
    {
        var (console, _) = await OpenAsync();

        Assert.True(await console.SelectRailAsync(LampRail));

        Assert.Equal(EnumDeviceCategory.Lamp, console.Category);
        Assert.Equal(2, RowsOf(console).Count);
        Assert.NotEmpty(console.Columns);
        Assert.True(console.CanDragToGroup);
        Assert.Equal(ConsoleDetailState.None, console.Detail.State);
    }

    [Fact]
    public async Task should_filter_rows_by_number_or_name_when_search_text_is_set()
    {
        var (console, _) = await OpenAsync();
        await console.SelectRailAsync(LampRail);

        console.SearchText = "등 2";

        Assert.Single(RowsOf(console));
        Assert.Contains("전체 2", console.ListStatusText);
    }

    [Fact]
    public async Task should_load_form_when_a_row_is_selected()
    {
        var (console, _) = await OpenAsync();
        await console.SelectRailAsync(LampRail);

        Assert.True(console.OnRowsSelected(RowsOf(console).Take(1).ToList()));

        Assert.Equal(ConsoleDetailState.Single, console.Detail.State);
        Assert.Equal("경광등 1", console.Form.Fields.Single(f => f.Key == "name_device").Text);
        Assert.True(console.CanDelete);
    }

    [Fact]
    public async Task should_block_selection_and_rail_change_when_form_has_unapplied_changes()
    {
        var (console, _) = await OpenAsync();
        await console.SelectRailAsync(LampRail);
        var rows = RowsOf(console);
        console.OnRowsSelected(rows.Take(1).ToList());
        console.Form.Fields.Single(f => f.Key == "name_device").Text = "바꾼 이름";

        IReadOnlyList<object>? restored = null;
        console.SelectionRestoreRequested += (_, r) => restored = r;

        Assert.False(console.OnRowsSelected(rows.Skip(1).Take(1).ToList()));
        Assert.False(await console.SelectRailAsync(DeviceDashboardViewModel.GroupsRailKey));

        Assert.Equal(LampRail, console.SelectedRail!.Key);
        Assert.Same(rows[0], console.Form.Rows.Single());
        Assert.Same(rows[0], restored!.Single());            // 화면은 이 행으로 선택을 되돌린다
        Assert.Equal("바꾼 이름", console.Form.Fields.Single(f => f.Key == "name_device").Text);
    }

    [Fact]
    public async Task should_keep_list_untouched_until_register_when_add_is_pressed()
    {
        var (console, lamps) = await OpenAsync();
        await console.SelectRailAsync(LampRail);

        console.Add();

        Assert.Equal(ConsoleDetailState.Create, console.Detail.State);
        Assert.Equal(2, lamps.ViewModelProvider.Count);      // Draft 는 폼에만 물려 있다
        Assert.Equal("3", console.Form.Fields.Single(f => f.Key == "number_device").Text);   // 번호는 패널의 자동 배정을 그대로 쓴다

        console.Revert();

        Assert.Equal(ConsoleDetailState.None, console.Detail.State);
        Assert.Empty(console.Form.Sections);
        Assert.Equal(2, lamps.ViewModelProvider.Count);      // [취소] = 아무것도 안 남는다
    }

    [Fact]
    public async Task should_not_write_or_save_when_apply_fails_validation()
    {
        var (console, _) = await OpenAsync();
        await console.SelectRailAsync(LampRail);
        var row = (LampDeviceViewModel)RowsOf(console)[0];
        console.OnRowsSelected(new List<object> { row });
        console.Form.Fields.Single(f => f.Key == "name_device").Text = "  ";

        console.Apply();

        Assert.Equal("경광등 1", row.DeviceName);
        Assert.Equal(ConsoleDetailState.Dirty, console.Detail.State);   // 손댄 칸은 남는다
        Assert.True(console.Form.Fields.Single(f => f.Key == "name_device").HasError);
    }

    [Fact]
    public async Task should_write_touched_field_into_row_and_settle_when_applied()
    {
        var (console, _) = await OpenAsync();
        await console.SelectRailAsync(LampRail);
        var row = (LampDeviceViewModel)RowsOf(console)[0];
        console.OnRowsSelected(new List<object> { row });
        console.Form.Fields.Single(f => f.Key == "name_device").Text = "정문 경광등";

        console.Apply();

        Assert.Equal("정문 경광등", row.DeviceName);   // 패널의 저장은 이 행을 서버 목록과 비교해 보낸다
        Assert.False(console.Detail.Tracker.IsDirty);
    }

    [Fact]
    public async Task should_drop_selection_and_unapplied_changes_when_closed()
    {
        var (console, _) = await OpenAsync();
        await console.SelectRailAsync(LampRail);
        console.OnRowsSelected(RowsOf(console).Take(1).ToList());
        console.Form.Fields.Single(f => f.Key == "name_device").Text = "바꾼 이름";

        await ((IDeactivate)console).DeactivateAsync(close: false);

        Assert.Empty(console.Form.Sections);
        Assert.Equal(ConsoleDetailState.None, console.Detail.State);
        Assert.Null(console.Rows);
    }

    private sealed class StubCatalog : Ironwall.Dotnet.Libraries.Devices.Ui.Services.ICatalogService
    {
        public bool IsLoaded => true;
        public event EventHandler? CatalogChanged { add { } remove { } }
        public Task<bool> EnsureLoadedAsync(System.Threading.CancellationToken token = default) => Task.FromResult(true);
        public Task<bool> RefreshAsync(System.Threading.CancellationToken token = default) => Task.FromResult(true);
        public Ironwall.Dotnet.Libraries.Devices.Ui.Services.CatalogTypeAxis? TypeAxis(EnumDeviceCategory category) => null;
        public IReadOnlyList<Ironwall.Dotnet.Libraries.Devices.Ui.Services.CatalogOption> TypeAxisValues(EnumDeviceCategory category) => Array.Empty<Ironwall.Dotnet.Libraries.Devices.Ui.Services.CatalogOption>();
        public bool IsTypeAxisValue(EnumDeviceCategory category, string? code) => false;
        public string TypeAxisLabel(EnumDeviceCategory category, string? code) => code ?? string.Empty;
        public IReadOnlyList<Ironwall.Dotnet.Libraries.Devices.Ui.Services.CatalogExtraAxis> ExtraAxes(EnumDeviceCategory category) => Array.Empty<Ironwall.Dotnet.Libraries.Devices.Ui.Services.CatalogExtraAxis>();
        public IReadOnlyList<Ironwall.Dotnet.Libraries.Devices.Ui.Services.CatalogOption> Vocabulary(string name, bool includeDeprecated = false, EnumDeviceCategory? appliesTo = null) => Array.Empty<Ironwall.Dotnet.Libraries.Devices.Ui.Services.CatalogOption>();
        public string LabelOf(string vocabularyName, string? code) => code ?? string.Empty;
    }
}
