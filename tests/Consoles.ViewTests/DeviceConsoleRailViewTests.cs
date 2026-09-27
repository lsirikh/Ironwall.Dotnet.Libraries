using Accounts.Ui.ViewTests;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Tests;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Dashboards;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Devices.Ui.Views.Dashboards;
using Ironwall.Dotnet.Libraries.Enums;
using Xunit;

namespace Consoles.ViewTests;

/// <summary>
/// 장비 콘솔 <b>실제 뷰</b> — 미적용 변경으로 레일 전환이 막히면 화면의 레일도 앞 레일로 돌아와야 한다.
/// </summary>
/// <remarks>
/// GIS 실창 WP-2 SC-DEV-008: 콘솔은 앞 레일 그대로인데 화면 레일은 새 레일에 남아, 운영자가 '경광등' 레일을 보며 [추가]했는데 함체가 만들어졌다.
/// 헤드리스 시험(DeviceConsoleRailRefusalAndNamesTests)은 SelectedItem 만 봐서 통과했다 — 항목 컨테이너는 새 레일에 남아 있었다.
/// </remarks>
public class DeviceConsoleRailViewTests
{
    private static readonly string LampRail = DeviceDashboardViewModel.RailKeyOf(EnumDeviceCategory.Lamp);
    private static readonly string EnclosureRail = DeviceDashboardViewModel.RailKeyOf(EnumDeviceCategory.Enclosure);

    [Fact]
    public void should_put_the_rail_selection_back_to_lamps_when_the_switch_is_refused_in_the_real_console_view() => AppHost.Run(() =>
    {
        var (view, window, console) = HostConsole();
        try
        {
            Assert.True(RailProbe.Wait(console.SelectRailAsync(LampRail)));
            AppHost.Pump();
            console.Detail.Tracker.Touch("name_device", "경광등 1", "고친 이름");
            Assert.True(console.Detail.Tracker.IsDirty);

            var rail = RailProbe.Rail(view, "Console.Devices.Rail");
            Assert.Equal(LampRail, RailProbe.SelectedKey(rail));

            RailProbe.Select(rail, EnclosureRail);
            AppHost.Pump(System.Windows.Threading.DispatcherPriority.ApplicationIdle);

            Assert.Equal(LampRail, console.SelectedRail?.Key);
            Assert.True(console.Detail.Tracker.IsDirty, "막힌 뒤에도 고친 칸은 남아야 한다");
            RailProbe.AssertShows(rail, LampRail, refused: EnclosureRail);
        }
        finally { window.Close(); }
    });

    [Fact]
    public void should_move_the_rail_when_there_are_no_unapplied_changes_in_the_real_console_view() => AppHost.Run(() =>
    {
        var (view, window, console) = HostConsole();
        try
        {
            Assert.True(RailProbe.Wait(console.SelectRailAsync(LampRail)));
            AppHost.Pump();
            var rail = RailProbe.Rail(view, "Console.Devices.Rail");

            RailProbe.Select(rail, EnclosureRail);
            Assert.True(RailProbe.ItemSelected(rail, EnclosureRail), "누른 즉시 새 레일이 선택으로 보여야 한다 · " + RailProbe.State(rail));
            AppHost.Pump(System.Windows.Threading.DispatcherPriority.ApplicationIdle);

            Assert.Equal(EnclosureRail, console.SelectedRail?.Key);
            RailProbe.AssertShows(rail, EnclosureRail, refused: LampRail);
        }
        finally { window.Close(); }
    });

    private static (DeviceDashboardView View, System.Windows.Window Window, DeviceDashboardViewModel Console) HostConsole()
    {
        var events = new EventAggregator();
        RailProbe.UseIoC(type => type == typeof(IEventAggregator) ? events : null);

        var log = new MockLogService();
        var api = new MockDeviceApiService();
        var providerService = new MockDeviceProviderService();
        var devices = new DeviceProvider();
        var groups = new DeviceGroupProvider(log);
        var controllers = new ControllerDeviceProvider(log, devices);

        var console = new DeviceDashboardViewModel(
            events, log, new DeviceTabControlViewModel(events, log),
            new ControllerDevicePanelViewModel(events, log, api, controllers, providerService),
            new SensorDevicePanelViewModel(events, log, api, new SensorDeviceProvider(log, devices), controllers, providerService),
            new CameraDevicePanelViewModel(events, log, api, new CameraDeviceProvider(log, devices), providerService),
            new SpeakerDevicePanelViewModel(events, log, api, new SpeakerDeviceProvider(log, devices), providerService),
            new EnclosureDevicePanelViewModel(events, log, api, new EnclosureDeviceProvider(log, devices), providerService),
            new LampDevicePanelViewModel(events, log, api, new LampDeviceProvider(log, devices), providerService),
            new GateDevicePanelViewModel(events, log, api, new GateDeviceProvider(log, devices), providerService),
            new DeviceGroupPanelViewModel(events, log, api, groups, devices),
            devices, groups, controllers, new ServerProvider(log), api, new EmptyCatalog());
        RailProbe.Wait(((IActivate)console).ActivateAsync());

        var view = new DeviceDashboardView { Width = 1400, Height = 900 };
        var window = RailProbe.Host(console, view);
        return (view, window, console);
    }

    /// <summary>종류 축 카탈로그가 없는 서버(6.3) — 레일 전환에는 관여하지 않는다.</summary>
    private sealed class EmptyCatalog : ICatalogService
    {
        public bool IsLoaded => true;
        public event EventHandler? CatalogChanged { add { } remove { } }
        public Task<bool> EnsureLoadedAsync(CancellationToken token = default) => Task.FromResult(true);
        public Task<bool> RefreshAsync(CancellationToken token = default) => Task.FromResult(true);
        public CatalogTypeAxis? TypeAxis(EnumDeviceCategory category) => null;
        public IReadOnlyList<CatalogOption> TypeAxisValues(EnumDeviceCategory category) => Array.Empty<CatalogOption>();
        public bool IsTypeAxisValue(EnumDeviceCategory category, string? code) => false;
        public string TypeAxisLabel(EnumDeviceCategory category, string? code) => code ?? string.Empty;
        public IReadOnlyList<CatalogExtraAxis> ExtraAxes(EnumDeviceCategory category) => Array.Empty<CatalogExtraAxis>();
        public IReadOnlyList<CatalogOption> Vocabulary(string name, bool includeDeprecated = false, EnumDeviceCategory? appliesTo = null) => Array.Empty<CatalogOption>();
        public string LabelOf(string vocabularyName, string? code) => code ?? string.Empty;
    }
}
