using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Detail;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using Xunit;

namespace GMaps.PropertyPanel.Tests;

/// <summary>
/// 상세 보기의 부품 표 자리(SdComponentTable) — 표가 없는 탭에서는 아무것도 그리지 않고, 부품이 하나도 없으면 요약 한 줄만 그린다.
/// </summary>
/// <remarks>
/// 결함(2026-10-01 사용자 보고, 장비 콘솔과 같은 뿌리): <c>ContentTemplate</c> 을 직접 건 ContentControl 은 Content 가 null 이어도
/// 템플릿을 세우고 DataContext 가 바깥(탭)으로 이어져, 기본 탭 아래에 빈 머리줄 · 회색 점 · 글 없는 정렬 단추가 그려졌다.
/// </remarks>
public class ComponentTableHostRenderTests
{
    private static ResourceDictionary Styles(string themeName) => new()
    {
        MergedDictionaries =
        {
            new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.Theme;component/Themes/Tokens.Shared.xaml", UriKind.Relative) },
            new ResourceDictionary { Source = new Uri($"/Ironwall.Dotnet.Libraries.Theme;component/Themes/Tokens.{themeName}.xaml", UriKind.Relative) },
            new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/SymbolDetailStyle.xaml", UriKind.Relative) },
        }
    };

    private static DeviceAxesModel Axes(bool withComponents)
    {
        var spec = new HardwareSpecModel();
        var status = new DeviceStatusModel();
        if (withComponents)
        {
            spec.Components.Add(new ComponentDefinitionModel { Key = "ptz", Type = "PTZ_UNIT" });
            status.Components["ptz"] = new ComponentStatusModel { State = "IDLE", Health = "OK" };
        }
        return new DeviceAxesModel { HardwareSpec = spec, DeviceStatus = status, Meta = new ResponseMeta("full", new[] { "hardware_spec", "device_status" }) };
    }

    private static GMapPidsMarker Marker(DeviceAxesModel axes)
    {
        var marker = new GMapPidsMarker(Mock.Of<ILogService>(), new PidsSymbolModel
        {
            Title = "외곽1", DeviceType = EnumDeviceType.IpCamera, Latitude = 37.5, Longitude = 127.0, Width = 40, Height = 40, Zoom = 16,
        });
        marker.LinkedDevice = new CameraDeviceModel
        {
            Id = 301, DeviceNumber = 1, DeviceName = "외곽1", DeviceType = EnumDeviceType.IpCamera,
            Status = EnumDeviceStatus.ACTIVATED, IsEnable = true, Axes = axes,
        };
        return marker;
    }

    private static SymbolDetailControl Panel(GMapPidsMarker marker, SymbolDetailTab tab, string themeName, out SymbolDetailViewModel vm)
    {
        vm = new SymbolDetailViewModel();
        var context = new SymbolDetailContext(marker.DeviceType, HasDevice: true, WebServerEnabled: true);
        vm.Load(marker, in context, layerName: "카메라", initialTab: tab);
        var panel = new SymbolDetailControl { DataContext = vm };
        panel.Resources.MergedDictionaries.Add(Styles(themeName));
        panel.Style = (Style)panel.Resources[typeof(SymbolDetailControl)];
        AppHost.Layout(panel, 720, 430);
        return panel;
    }

    /// <summary>화면에 실제로 보이는(조상까지 Visible) 것만.</summary>
    private static IEnumerable<T> Shown<T>(DependencyObject root) where T : UIElement
        => Descendants<T>(root).Where(e => e.IsVisible || IsVisibleUpTo(e, root));

    private static bool IsVisibleUpTo(DependencyObject element, DependencyObject root)
    {
        // 창에 붙지 않은 오프스크린 배치에서는 IsVisible 이 false 라 Visibility 를 조상까지 직접 본다
        for (var current = element; current != null && !ReferenceEquals(current, root); current = VisualTreeHelper.GetParent(current))
            if (current is UIElement { Visibility: not Visibility.Visible }) return false;
        return true;
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
    public void should_draw_no_component_table_visuals_when_tab_has_no_table(string themeName) => AppHost.Run(() =>
    {
        using var marker = Marker(Axes(withComponents: true));
        var panel = Panel(marker, SymbolDetailTab.Basic, themeName, out var vm);

        Assert.Equal(SymbolDetailTab.Basic, vm.SelectedTab!.Tab);
        Assert.Null(vm.SelectedTab.ComponentTable);
        var texts = Shown<TextBlock>(panel).Select(t => t.Text).ToList();
        Assert.DoesNotContain("마지막 변화", texts);                                   // 표 머리줄이 없다
        Assert.DoesNotContain("사유", texts);
        Assert.DoesNotContain(Shown<ToggleButton>(panel), t => AutomationProperties.GetAutomationId(t) == "Symbol.Detail.Components.Sort");
        Assert.DoesNotContain(Descendants<ToggleButton>(panel), t => AutomationProperties.GetAutomationId(t) == "Symbol.Detail.Components.Sort");   // 세우지도 않는다
        vm.Dispose();
    });

    [Fact]
    public void should_draw_table_once_when_components_tab_is_selected() => AppHost.Run(() =>
    {
        using var marker = Marker(Axes(withComponents: true));
        var panel = Panel(marker, SymbolDetailTab.Components, "Light", out var vm);

        Assert.Single(Shown<ToggleButton>(panel), t => AutomationProperties.GetAutomationId(t) == "Symbol.Detail.Components.Sort");
        Assert.Single(Shown<TextBlock>(panel), t => t.Text == "마지막 변화");
        vm.Dispose();
    });

    [Fact]
    public void should_show_one_summary_line_without_header_dot_or_sort_when_device_has_no_components() => AppHost.Run(() =>
    {
        using var marker = Marker(Axes(withComponents: false));
        var panel = Panel(marker, SymbolDetailTab.Components, "Light", out var vm);

        var table = vm.SelectedTab!.ComponentTable!;
        Assert.False(table.HasRows);
        var texts = Shown<TextBlock>(panel).Select(t => t.Text).ToList();
        Assert.Contains("부품 없음", texts);                                             // 한 줄 안내
        Assert.DoesNotContain("마지막 변화", texts);                                     // 빈 머리줄 없음
        Assert.DoesNotContain(Shown<ToggleButton>(panel), t => AutomationProperties.GetAutomationId(t) == "Symbol.Detail.Components.Sort");
        var summaryRow = Shown<TextBlock>(panel).Single(t => t.Text == "부품 없음").Parent as Panel;
        Assert.NotNull(summaryRow);
        Assert.DoesNotContain(summaryRow!.Children.OfType<Ellipse>(), e => IsVisibleUpTo(e, panel));   // 외톨이 점 없음
        vm.Dispose();
    });
}
