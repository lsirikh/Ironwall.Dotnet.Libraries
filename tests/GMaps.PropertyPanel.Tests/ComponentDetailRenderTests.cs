using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Detail;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Monitoring.Models.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using Xunit;

namespace GMaps.PropertyPanel.Tests;

/// <summary>
/// 상세 보기 "부품" 탭(FR-06) · 조립 카드(FR-05) 오프스크린 렌더 — component-display-unify.
/// 라이트/다크 · 행 자동화 식별자(Symbol.Detail.Components.Row.{key}) · 6.3 무회귀 · 항법 단추 숨김 · "부품 정보 없음".
/// </summary>
public class ComponentDetailRenderTests
{
    private static ResourceDictionary Tokens(string name)
        => new() { Source = new Uri($"/Ironwall.Dotnet.Libraries.Theme;component/Themes/Tokens.{name}.xaml", UriKind.Relative) };

    private static ResourceDictionary Styles(string themeName, string style) => new()
    {
        MergedDictionaries =
        {
            Tokens("Shared"), Tokens(themeName),
            new ResourceDictionary { Source = new Uri($"/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/{style}.xaml", UriKind.Relative) },
        }
    };

    private static DeviceAxesModel Axes(bool statusReceived = true)
    {
        var spec = new HardwareSpecModel();
        spec.Components.Add(new ComponentDefinitionModel { Key = "ptz", Type = "PTZ_UNIT" });
        spec.Components.Add(new ComponentDefinitionModel { Key = "tracker", Type = "TRACKER" });
        spec.Components.Add(new ComponentDefinitionModel { Key = "ir", Type = "IR_LED", Label = "IR 조명" });
        spec.Components.Add(new ComponentDefinitionModel { Key = "wiper", Type = "WIPER" });
        var status = new DeviceStatusModel();
        status.Components["ptz"] = new ComponentStatusModel { State = "IDLE", Health = "OK", ObservedAt = "2026-10-01T09:40:11.000000+09:00" };
        status.Components["tracker"] = new ComponentStatusModel { State = "ACTIVE", Health = "OK", ObservedAt = "2026-10-01T09:41:02.000000+09:00" };
        status.Components["ir"] = new ComponentStatusModel { State = "ON", Health = "DEGRADED", FaultReason = "OVER_CURRENT", ObservedAt = "2026-10-01T09:22:40.000000+09:00" };
        return new DeviceAxesModel
        {
            HardwareSpec = spec,
            DeviceStatus = statusReceived ? status : null,
            Meta = new ResponseMeta("full", statusReceived ? new[] { "hardware_spec", "device_status" } : new[] { "hardware_spec" }),
        };
    }

    private static GMapPidsMarker Marker(DeviceAxesModel? axes)
    {
        var marker = new GMapPidsMarker(Mock.Of<ILogService>(), new PidsSymbolModel
        {
            Title = "북측 PTZ-07", DeviceType = EnumDeviceType.IpCamera, Latitude = 37.5, Longitude = 127.0, Width = 40, Height = 40, Zoom = 16,
        });
        marker.LinkedDevice = new CameraDeviceModel
        {
            Id = 307, DeviceNumber = 7, DeviceName = "북측 PTZ-07", DeviceType = EnumDeviceType.IpCamera,
            Status = EnumDeviceStatus.ACTIVATED, IsEnable = true, Axes = axes,
        };
        return marker;
    }

    private static SymbolDetailControl DetailPanel(GMapPidsMarker marker, string themeName, SymbolDetailTab? initialTab, out SymbolDetailViewModel vm)
    {
        vm = new SymbolDetailViewModel();
        var context = new SymbolDetailContext(marker.DeviceType, HasDevice: true, WebServerEnabled: true);
        vm.Load(marker, in context, layerName: "카메라", initialTab: initialTab);
        var panel = new SymbolDetailControl { DataContext = vm };
        panel.Resources.MergedDictionaries.Add(Styles(themeName, "SymbolDetailStyle"));
        panel.Style = (Style)panel.Resources[typeof(SymbolDetailControl)];
        return panel;
    }

    private static byte[] Pixels(FrameworkElement view, int width, int height)
    {
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(view);
        var buffer = new byte[width * height * 4];
        bitmap.CopyPixels(buffer, width * 4, 0);
        return buffer;
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T hit) yield return hit;
            foreach (var deeper in Descendants<T>(child)) yield return deeper;
        }
    }

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void should_open_components_tab_with_row_automation_ids_when_detail_opens_on_parts(string themeName) => AppHost.Run(() =>
    {
        using var marker = Marker(Axes());
        var panel = DetailPanel(marker, themeName, SymbolDetailTab.Components, out var vm);
        AppHost.Layout(panel, 720, 430);

        Assert.Equal(new[] { SymbolDetailTab.Basic, SymbolDetailTab.Components, SymbolDetailTab.Symbol }, vm.Tabs.Select(t => t.Tab));
        Assert.Equal(SymbolDetailTab.Components, vm.SelectedTab!.Tab);
        var table = vm.SelectedTab.ComponentTable!;
        Assert.Equal("저하 1 · 정상 2 · 미상 1", table.SummaryText);             // 장비 콘솔과 같은 요약 줄
        Assert.Equal(new[] { "ptz", "tracker", "ir", "wiper" }, table.Rows.Select(r => r.Key));   // 기본 = 선언 순서

        var ids = Descendants<ListBoxItem>(panel).Select(AutomationProperties.GetAutomationId).ToList();
        Assert.Equal(new[] { "Symbol.Detail.Components.Row.ptz", "Symbol.Detail.Components.Row.tracker", "Symbol.Detail.Components.Row.ir", "Symbol.Detail.Components.Row.wiper" }, ids);
        var texts = Descendants<TextBlock>(panel).Select(t => t.Text).ToList();
        Assert.Contains("추적 중", texts);
        Assert.Contains("과전류", texts);
        Assert.Contains("미상", texts);
        Assert.DoesNotContain("DEGRADED", texts);                                // 영문 코드 없음

        var pixels = Pixels(panel, 720, 430);
        Assert.True(pixels.Where((_, i) => i % 4 == 3).Count(a => a > 0) > 720 * 430 / 2, $"{themeName}: 창이 비었다");
        vm.Dispose();
    });

    [Fact]
    public void should_keep_tabs_unchanged_when_device_has_no_component_axes() => AppHost.Run(() =>
    {
        using var marker = Marker(axes: null);   // 6.3 서버
        var panel = DetailPanel(marker, "Light", SymbolDetailTab.Components, out var vm);
        AppHost.Layout(panel, 720, 430);

        Assert.Equal(new[] { SymbolDetailTab.Basic, SymbolDetailTab.Symbol }, vm.Tabs.Select(t => t.Tab));
        Assert.Equal(SymbolDetailTab.Basic, vm.SelectedTab!.Tab);   // 없는 탭을 요청하면 기본 규칙대로
        Assert.Empty(Descendants<ListBoxItem>(panel));
        vm.Dispose();
    });

    [Fact]
    public void should_follow_component_changes_when_detail_window_is_open() => AppHost.Run(() =>
    {
        using var marker = Marker(Axes());
        var panel = DetailPanel(marker, "Light", SymbolDetailTab.Components, out var vm);
        AppHost.Layout(panel, 720, 430);

        var axes = Axes();
        axes.DeviceStatus!.Components["ptz"] = new ComponentStatusModel { State = "IDLE", Health = "FAULT", FaultReason = "COMM_ERROR" };
        marker.LinkedDevice!.Axes = axes;
        marker.RefreshComponentSummary(force: true);

        Assert.Equal("고장 1 · 저하 1 · 정상 1 · 미상 1", vm.SelectedTab!.ComponentTable!.SummaryText);
        vm.Dispose();
    });

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void should_render_card_fault_first_without_navigation_when_host_has_no_navigator(string themeName) => AppHost.Run(() =>
    {
        using var marker = Marker(Axes());
        var vm = new ComponentCardViewModel(marker, navigator: null, today: null);
        var card = new ComponentCardControl { DataContext = vm };
        card.Resources.MergedDictionaries.Add(Styles(themeName, "ComponentCardStyle"));
        card.Style = (Style)card.Resources[typeof(ComponentCardControl)];
        AppHost.Layout(card, 320, 400);

        Assert.True(vm.HasTable);
        Assert.Equal("ir", vm.Table.Rows[0].Key);                                    // 고장 먼저(저하가 맨 앞)
        Assert.StartsWith("북측 PTZ-07 · ", vm.Title);
        Assert.StartsWith("마지막 변화 ", vm.SubTitle);
        Assert.False(vm.HasActions);                                                 // 항법 입구가 없으면 단추를 숨긴다
        var buttons = Descendants<Button>(card).Where(b => b.Visibility == Visibility.Visible).Select(AutomationProperties.GetAutomationId).ToList();
        Assert.Contains("GMaps.ComponentCard.Close", buttons);
        Assert.DoesNotContain("GMaps.ComponentCard.OpenConsole", buttons);

        var pixels = Pixels(card, 320, (int)Math.Ceiling(card.ActualHeight));
        Assert.True(pixels.Where((_, i) => i % 4 == 3).Count(a => a > 0) > 320 * 60, $"{themeName}: 카드가 비었다");
    });

    [Fact]
    public void should_show_navigation_only_for_registered_navigator_and_wiring_only_for_sensors() => AppHost.Run(() =>
    {
        using var camera = Marker(Axes());
        var navigator = new FakeNavigator();
        var cameraCard = new ComponentCardViewModel(camera, navigator, today: null);
        Assert.True(cameraCard.CanOpenConsole);
        Assert.False(cameraCard.CanOpenWiring);                                      // 카메라 — 결선 없음
        cameraCard.OpenConsoleCommand.Execute(null);
        Assert.Equal(307, navigator.OpenedDevice);
    });

    [Fact]
    public void should_say_no_info_when_status_axis_is_not_received() => AppHost.Run(() =>
    {
        using var marker = Marker(Axes(statusReceived: false));
        var vm = new ComponentCardViewModel(marker, navigator: null, today: null);
        Assert.False(vm.HasTable);
        Assert.Equal("부품 정보 없음", vm.NoInfoText);
        Assert.True(ComponentCardService.CanShow(marker));                          // 축은 있다 — 카드는 열리되 "정보 없음"
        using var legacy = Marker(axes: null);
        Assert.False(ComponentCardService.CanShow(legacy));                          // 6.3 — 카드를 열지 않는다
    });

    private sealed class FakeNavigator : IDeviceConsoleNavigator
    {
        public int? OpenedDevice { get; private set; }
        public bool CanOpenDevice(int deviceId) => true;
        public void OpenDevice(int deviceId) => OpenedDevice = deviceId;
        public bool CanOpenWiring(int deviceId) => true;
        public void OpenWiring(int deviceId) { }
    }
}
