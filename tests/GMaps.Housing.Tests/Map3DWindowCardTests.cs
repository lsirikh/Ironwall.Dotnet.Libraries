using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Adorners;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Components;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using Xunit;

namespace GMaps.Housing.Tests;

/// <summary>
/// 3D 화면 위 조립 카드 — WPF 안 3D 가 펼쳐진 동안 카드를 3D 위 층(Map3DViewHost.OverlayLayer)에 2D 와 같은 배치 규칙으로 띄우고,
/// 3D ↔ 2D 전환 때 닫지 않고 같은 VM 으로 옮긴다. 창을 띄우지 않는다(지도 · 층을 부모 없이 배치만).
/// </summary>
[Collection(MapCatalogStaticCollection.Name)]
public class Map3DWindowCardTests
{
    private static GMapPidsMarker Camera(int id)
    {
        var marker = new GMapPidsMarker(Mock.Of<ILogService>(), new PidsSymbolModel { Title = $"cam{id}", DeviceType = EnumDeviceType.IpCamera, Zoom = 1 });
        marker.Width = 40; marker.Height = 40; marker.IsLayerEnabled = true; marker.IsVisible = true;
        marker.LinkedDevice = new CameraDeviceModel
        {
            Id = id, DeviceType = EnumDeviceType.IpCamera, Status = EnumDeviceStatus.ACTIVATED,
            Axes = ComponentHealthSummaryTests.Axes(true, ("ptz", "PTZ_UNIT", null, null, "IDLE", "OK", null, true)),
        };
        return marker;
    }

    private static (GMapCustomControl Map, Canvas Layer) Scene()
    {
        var map = new GMapCustomControl { Width = 800, Height = 600, Zoom = 18 };
        _ = new AdornerDecorator { Child = map };   // 2D 카드 어도너 층(배치는 하지 않는다 — 지도 템플릿은 다른 시험 스레드와 공유된다)
        var layer = new Map3DViewHost().OverlayLayer;
        var host = (FrameworkElement)layer.Parent;
        host.Measure(new Size(800, 600));
        host.Arrange(new Rect(0, 0, 800, 600));
        return (map, layer);
    }

    private static ComponentCardControl? CardIn(Canvas layer) => layer.Children.OfType<ComponentCardControl>().SingleOrDefault();

    private static ComponentCardAdorner? AdornerOf(GMapCustomControl map)
        => AdornerLayer.GetAdornerLayer(map)?.GetAdorners(map)?.OfType<ComponentCardAdorner>().SingleOrDefault();

    [Fact]
    public void should_place_card_beside_3d_marker_with_2d_rule_when_overlay_layer_active() => HousingTests.Sta(() =>
    {
        var (map, layer) = Scene();
        using var service = new ComponentCardService(map);
        using var marker = Camera(1);
        var at = new Point(300, 200);
        service.UseOverlay(layer, _ => at);

        service.Show(marker);

        var card = CardIn(layer);
        Assert.NotNull(card);
        Assert.Same(marker, service.Current);
        Assert.Null(AdornerOf(map));                                         // 2D 어도너에는 없다
        Assert.Equal(Visibility.Visible, card!.Visibility);
        var expected = ComponentCardPlacement.Place(at, marker.Width / 2, card.DesiredSize, new Size(800, 600));
        Assert.Equal(expected.X, Canvas.GetLeft(card), 6);
        Assert.Equal(expected.Y, Canvas.GetTop(card), 6);
        Assert.True(Canvas.GetLeft(card) > at.X);                             // 아이콘 오른쪽
    });

    [Fact]
    public void should_follow_3d_marker_when_screen_point_moves() => HousingTests.Sta(() =>
    {
        var (map, layer) = Scene();
        using var service = new ComponentCardService(map);
        using var marker = Camera(2);
        var at = new Point(200, 300);
        service.UseOverlay(layer, _ => at);
        service.Show(marker);
        double before = Canvas.GetLeft(CardIn(layer)!);

        at = new Point(260, 300);                                            // 3D 카메라를 돌렸다 — overlay.screen
        service.Relayout();

        Assert.Equal(before + 60, Canvas.GetLeft(CardIn(layer)!), 6);
    });

    [Fact]
    public void should_hide_card_until_3d_screen_point_known_when_anchor_unknown() => HousingTests.Sta(() =>
    {
        var (map, layer) = Scene();
        using var service = new ComponentCardService(map);
        using var marker = Camera(3);
        Point? at = null;
        service.UseOverlay(layer, _ => at);

        service.Show(marker);
        Assert.Equal(Visibility.Hidden, CardIn(layer)!.Visibility);         // 마커가 카메라 뒤 · 아직 좌표 없음

        at = new Point(100, 100);
        service.Relayout();
        Assert.Equal(Visibility.Visible, CardIn(layer)!.Visibility);
    });

    [Fact]
    public void should_keep_same_card_view_model_when_switching_between_3d_and_2d() => HousingTests.Sta(() =>
    {
        var (map, layer) = Scene();
        using var service = new ComponentCardService(map);
        using var marker = Camera(4);
        service.UseOverlay(layer, _ => new Point(300, 200));
        service.Show(marker);
        var vm = CardIn(layer)!.DataContext;

        service.UseOverlay(null, null);                                       // 3D → 2D

        Assert.Null(CardIn(layer));
        Assert.Same(vm, AdornerOf(map)?.ViewModel);                           // 닫지 않고 같은 VM 을 2D 어도너로
        Assert.Same(marker, service.Current);
        Assert.True(marker.IsComponentCardOpen);

        service.UseOverlay(layer, _ => new Point(300, 200));                  // 2D → 3D

        Assert.Null(AdornerOf(map));
        Assert.Same(vm, CardIn(layer)?.DataContext);
        Assert.True(marker.IsComponentCardOpen);
    });

    [Fact]
    public void should_remove_card_from_3d_layer_when_closed() => HousingTests.Sta(() =>
    {
        var (map, layer) = Scene();
        using var service = new ComponentCardService(map);
        using var marker = Camera(5);
        service.UseOverlay(layer, _ => new Point(300, 200));
        service.Show(marker);

        service.Hide();

        Assert.Null(CardIn(layer));
        Assert.Null(service.Current);
        Assert.False(marker.IsComponentCardOpen);
    });
}
