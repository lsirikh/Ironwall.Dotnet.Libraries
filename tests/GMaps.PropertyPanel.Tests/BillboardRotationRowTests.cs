using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapProperties;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using Xunit;

namespace GMaps.PropertyPanel.Tests;

/// <summary>
/// map-tilt-25d FR-12 — 속성창 '회전' 행(BasePropertyStyle)은 선택 마커 Shape 가 빌보드(<c>IsBillboard=True</c>)이면 Collapsed,
/// 비빌보드(Geometric)·3D(IsBillboard=False)는 종전대로 Visible. SIM-C002.
/// </summary>
public class BillboardRotationRowTests
{
    private static ResourceDictionary Tokens(string name) => new() { Source = new Uri($"/Ironwall.Dotnet.Libraries.Theme;component/Themes/Tokens.{name}.xaml", UriKind.Relative) };
    private static ResourceDictionary Theme(string styleFile) => new()
    {
        MergedDictionaries =
        {
            Tokens("Shared"), Tokens("Light"),
            new ResourceDictionary { Source = new Uri($"/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/{styleFile}", UriKind.Relative) },
        }
    };

    private static IEnumerable<FrameworkElement> Descendants(DependencyObject root)
    {
        int n = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < n; i++)
        {
            var c = VisualTreeHelper.GetChild(root, i);
            if (c is FrameworkElement fe) yield return fe;
            foreach (var d in Descendants(c)) yield return d;
        }
    }

    private static FrameworkElement RotationRow(FrameworkElement panel)
    {
        var label = Descendants(panel).OfType<TextBlock>().First(t => t.Text == "회전");
        return (FrameworkElement)VisualTreeHelper.GetParent(label);   // BasePropertyStyle 의 회전 Grid
    }

    [Fact]
    public void should_collapse_rotation_row_when_selected_shape_is_billboard_pids2d() => AppHost.Run(() =>
    {
        using var marker = new GMapPidsMarker(Mock.Of<ILogService>(), new PidsSymbolModel { Title = "cam", DeviceType = EnumDeviceType.IpCamera, Bearing = 30, Latitude = 37.5, Longitude = 127, Width = 80, Height = 40 });
        marker.Shape = new GMapMarkerPidsControl(marker);
        Assert.True(((IMapRotationAwareShape)marker.Shape).IsBillboard);
        var panel = new GMapPropertyPidsControl();
        panel.Resources.MergedDictionaries.Add(Theme("PidsPropertyStyle.xaml"));
        panel.Style = (Style)panel.Resources[typeof(GMapPropertyPidsControl)];
        panel.SelectedMarker = marker;
        AppHost.Layout(panel, 300, 520);

        Assert.Equal(Visibility.Collapsed, RotationRow(panel).Visibility);
        Assert.Equal(30, marker.Bearing);   // 행 숨김은 모델을 건드리지 않는다(R-35)

        // 같은 마커를 3D(IsBillboard=False)로 바꾸면 행이 돌아온다 — RotatesIn2D 가 아니라 IsBillboard 로 분기(REFAC-I1)
        marker.Shape = new GMapMarker3DHousingControl(marker);
        Assert.False(((IMapRotationAwareShape)marker.Shape).IsBillboard);
        panel.SelectedMarker = null!; panel.SelectedMarker = marker;
        AppHost.Layout(panel, 300, 520);
        Assert.Equal(Visibility.Visible, RotationRow(panel).Visibility);
    });

    [Fact]
    public void should_keep_rotation_row_visible_when_selected_shape_is_geometric() => AppHost.Run(() =>
    {
        using var marker = new GMapGeometricMarker(Mock.Of<ILogService>(), new GeometricSymbolModel { Title = "geo", Bearing = 30, Latitude = 37.5, Longitude = 127, Width = 80, Height = 40 });
        marker.Shape = new GMapGeometricMarkerControl(marker);
        Assert.False(((IMapRotationAwareShape)marker.Shape).IsBillboard);
        var panel = new GMapPropertyGeometricControl();
        panel.Resources.MergedDictionaries.Add(Theme("GeometricPropertyStyle.xaml"));
        panel.Style = (Style)panel.Resources[typeof(GMapPropertyGeometricControl)];
        panel.SelectedMarker = marker;
        AppHost.Layout(panel, 300, 520);
        Assert.Equal(Visibility.Visible, RotationRow(panel).Visibility);
    });
}
