using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Threading;
using System.Xml.Linq;
using GMap.NET;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Xunit;

namespace GMaps.Housing.Tests;

/// <summary>
/// 지도 기울이기 제거(2026-10-19, 사용자 요청) — 예전엔 MapView.xaml 의 MainMap Height/Margin 오버스캔 MultiBinding(map-tilt-25d FR-04,
/// TiltOverscanConverter)을 같은 구조로 재현해 검증했다. 기울이기가 없어져 그 바인딩 · 컨버터를 지웠으므로, 지금은
/// ① MainMap 에 Height/Margin 바인딩이 없고 ② 같은 구조(Grid &gt; AdornerDecorator &gt; GMapCustomControl)에서 지도가 뷰포트를
/// 그대로 채운다(Auto 높이 · 0 마진 · 오프셋 0, 리사이즈해도 같음)를 단언한다.
/// </summary>
public class MapViewTiltOverscanBindingTests
{
    private static string MapViewPath([CallerFilePath] string? thisFile = null)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", "Ironwall.Dotnet.Libraries.GMaps.Ui", "Views", "Maps", "MapView.xaml"));

    private static (Grid Host, AdornerDecorator Viewport, GMapCustomControl Map) BuildMainMapLikeXaml(double w, double h)
    {
        var map = new GMapCustomControl { MinZoom = 1, MaxZoom = 19, Position = new PointLatLng(37.5, 127) };
        map.Zoom = 18;
        map.SoftwareTierOverride = false;
        // 벤더 GMapControl 의 정적 ItemsPanel 은 최초 생성 STA 스레드 소유 — 이 스레드에서 만든 동형 템플릿으로 대체(간헐 실패 방지)
        var itemsHost = new FrameworkElementFactory(typeof(Canvas));
        itemsHost.SetValue(Panel.IsItemsHostProperty, true);
        map.ItemsPanel = new ItemsPanelTemplate(itemsHost);

        var viewport = new AdornerDecorator { ClipToBounds = true, Child = map };
        var host = new Grid { Width = w, Height = h };
        host.Children.Add(viewport);
        HousingTests.Layout(host, w, h);
        Pump();
        return (host, viewport, map);
    }

    private static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    [Fact]
    public void should_have_no_height_or_margin_overscan_binding_on_main_map_when_tilt_removed()
    {
        var mainMap = XDocument.Load(MapViewPath()).Descendants().Single(e => e.Name.LocalName == "GMapCustomControl");
        Assert.DoesNotContain(mainMap.Elements(), e => e.Name.LocalName is "GMapCustomControl.Height" or "GMapCustomControl.Margin");
        Assert.DoesNotContain("TiltOverscanConverter", File.ReadAllText(MapViewPath()), StringComparison.Ordinal);
    }

    [Fact]
    public void should_fill_viewport_with_auto_height_and_zero_margin_when_tilt_removed() => HousingTests.Sta(() =>
    {
        var (_, viewport, map) = BuildMainMapLikeXaml(640, 480);
        Assert.Equal(0.0, map.TiltLayoutDeg);
        Assert.True(double.IsNaN(map.Height), "Height 는 Auto(NaN) 여야 한다");
        Assert.Equal(new Thickness(0), map.Margin);
        Assert.Equal(480, map.ActualHeight, 6);
        Assert.Equal(480, viewport.ActualHeight, 6);
        Assert.Equal(0.0, map.ViewportOffsetY, 9);
    });

    [Fact]
    public void should_keep_filling_viewport_when_viewport_is_resized_without_tilt() => HousingTests.Sta(() =>
    {
        var (host, viewport, map) = BuildMainMapLikeXaml(640, 480);
        host.Height = 600;                                                // F11/리사이즈 동형
        HousingTests.Layout(host, 640, 600);
        Pump();
        host.UpdateLayout();

        Assert.Equal(600, viewport.ActualHeight, 6);
        Assert.Equal(600, map.ActualHeight, 6);
        Assert.Equal(new Thickness(0), map.Margin);
        var center = map.TransformToAncestor(viewport).Transform(new Point(map.ActualWidth / 2, map.ActualHeight / 2));
        Assert.Equal(300, center.Y, 6);
        Assert.Equal(320, center.X, 6);
    });
}
