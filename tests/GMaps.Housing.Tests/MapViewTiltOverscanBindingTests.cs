using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Threading;
using GMap.NET;
using Ironwall.Dotnet.Libraries.GMaps.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Xunit;

namespace GMaps.Housing.Tests;

/// <summary>
/// map-tilt-25d PRD v1.1 FR-04 — MapView.xaml 의 MainMap Height/Margin MultiBinding(플랜 IMPL-G3)을 리소스 로드 없이
/// 같은 구조(Grid ★ &gt; AdornerDecorator(ClipToBounds) &gt; GMapCustomControl + TiltOverscanConverter, RelativeSource
/// AncestorType=AdornerDecorator / Self.TiltLayoutDeg)로 코드에서 재현해 STA 로 검증한다.
/// 검증: φ_layout=0 → Height Auto·Margin 0(해제) · 게이트 ON → Height=H/cosφ·Margin=(0,−Δ,0,−Δ) · 컨트롤 중심=뷰포트 중심(VER-01 배치)
/// · 리사이즈(F11 동형) 재계산 · OFF → 해제. 렌더 타깃 없는 STA 는 Tier0 이라 SoftwareTierOverride=false 로 하드웨어를 가정.
/// 실기 픽셀·RDP 는 V-01/V-04 실기 항목(미검증).
/// </summary>
public class MapViewTiltOverscanBindingTests
{
    private const double Cos20 = 0.93969262078590838;

    private static (Grid Host, AdornerDecorator Viewport, GMapCustomControl Map) BuildMainMapLikeXaml(double w, double h)
    {
        var map = new GMapCustomControl { MinZoom = 1, MaxZoom = 19, Position = new PointLatLng(37.5, 127) };
        map.Zoom = 18;
        map.SoftwareTierOverride = false;

        // 하네스 함정: 벤더 GMapControl 은 ItemsPanel(ItemsPanelTemplate)을 정적 필드로 공유한다(GMapControl.cs:796 — 최초 생성 STA 스레드 소유).
        // 이 클래스는 ~Tilt 배치에서 선행 클래스와 다른 STA 스레드에서 돌고, 첫 Measure 의 ItemsPresenter.OnTemplateChanged → Seal() 이
        // 미봉인 외부 스레드 템플릿을 만나면 "다른 스레드가 이 개체를 소유" 로 간헐 실패(실측 3회 중 1회). 오버스캔 바인딩과 무관한
        // 아이템 패널이므로 이 스레드에서 만든 동형(Canvas IsItemsHost) 템플릿으로 대체해 결정적으로 만든다.
        var itemsHost = new FrameworkElementFactory(typeof(Canvas));
        itemsHost.SetValue(Panel.IsItemsHostProperty, true);
        map.ItemsPanel = new ItemsPanelTemplate(itemsHost);

        var converter = new TiltOverscanConverter();
        map.SetBinding(FrameworkElement.HeightProperty, BuildOverscanBinding(converter, "Height"));
        map.SetBinding(FrameworkElement.MarginProperty, BuildOverscanBinding(converter, "Margin"));

        var viewport = new AdornerDecorator { ClipToBounds = true, Child = map };
        var host = new Grid { Width = w, Height = h };
        host.Children.Add(viewport);
        HousingTests.Layout(host, w, h);
        Pump();
        return (host, viewport, map);
    }

    /// <summary>MapView.xaml 과 동일 — values[0]=AdornerDecorator.ActualHeight, values[1]=Self.TiltLayoutDeg, ConverterParameter Height|Margin.</summary>
    private static MultiBinding BuildOverscanBinding(TiltOverscanConverter converter, string target)
    {
        var mb = new MultiBinding { Converter = converter, ConverterParameter = target, Mode = BindingMode.OneWay };
        mb.Bindings.Add(new Binding("ActualHeight") { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(AdornerDecorator), 1) });
        mb.Bindings.Add(new Binding("TiltLayoutDeg") { RelativeSource = RelativeSource.Self });
        return mb;
    }

    private static void EnableTilt(GMapCustomControl map, double angle = 20)
    {
        map.TiltSettings = new MapTiltModel { MinZoom = 18, HysteresisSteps = 1, MaxAngleDeg = 35 };
        map.RequestedTiltDeg = angle;
        map.IsTiltFeatureEnabled = true;
        map.FlushTiltReevaluation();
        Pump();
    }

    private static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    [Fact]
    public void should_release_height_and_margin_when_tilt_is_off() => HousingTests.Sta(() =>
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
    public void should_overscan_height_and_symmetric_margin_when_gate_is_active() => HousingTests.Sta(() =>
    {
        var (host, viewport, map) = BuildMainMapLikeXaml(640, 480);
        EnableTilt(map);
        host.UpdateLayout();

        Assert.Equal(20.0, map.TiltLayoutDeg, 6);                       // 게이트 전이 = 즉시 정착(G8)
        double expectedHeight = 480 / Cos20;
        double expectedDelta = (expectedHeight - 480) / 2;
        Assert.Equal(expectedHeight, map.Height, 6);
        Assert.Equal(-expectedDelta, map.Margin.Top, 6);
        Assert.Equal(-expectedDelta, map.Margin.Bottom, 6);
        Assert.Equal(0.0, map.Margin.Left, 9);
        Assert.Equal(0.0, map.Margin.Right, 9);
        Assert.Equal(expectedHeight, map.ActualHeight, 6);
        Assert.Equal(480, viewport.ActualHeight, 6);                      // 뷰포트(AdornerDecorator)는 커지지 않는다(레이아웃 루프 없음)
        Assert.Equal(-expectedDelta, map.ViewportOffsetY, 6);            // FR-05 Δ 는 Margin 에서 읽힌다

        // VER-01 레이아웃: 음수 Margin 으로 컨트롤이 −Δ 에 배치(레이아웃 오프셋, RenderTransform 제외) — 상하 대칭
        var layoutOffset = System.Windows.Media.VisualTreeHelper.GetOffset(map);
        Assert.Equal(-expectedDelta, layoutOffset.Y, 6);
        Assert.Equal(0.0, layoutOffset.X, 6);

        // PRD §3 불변식(렌더 포함, TransformToAncestor = 레이아웃 오프셋 ∘ ScaleY=cosφ 중심변환):
        // 상단 −Δ + cy·(1−cosφ) = 0 · 하단 = H_view (φ_layout=φ 일 때 화면을 정확히 채움) · 컨트롤 중심 = 뷰포트 중심
        var toViewport = map.TransformToAncestor(viewport);
        var topLeft = toViewport.Transform(new Point(0, 0));
        var bottomLeft = toViewport.Transform(new Point(0, map.ActualHeight));
        var center = toViewport.Transform(new Point(map.ActualWidth / 2, map.ActualHeight / 2));
        Assert.Equal(0.0, topLeft.Y, 6);
        Assert.Equal(480, bottomLeft.Y, 6);
        Assert.Equal(240, center.Y, 6);
        Assert.Equal(320, center.X, 6);
    });

    [Fact]
    public void should_recompute_overscan_when_viewport_is_resized() => HousingTests.Sta(() =>
    {
        var (host, viewport, map) = BuildMainMapLikeXaml(640, 480);
        EnableTilt(map);
        host.UpdateLayout();

        host.Height = 600;                                                // F11/리사이즈 동형 — ActualHeight 변경만으로 재계산
        HousingTests.Layout(host, 640, 600);
        Pump();
        host.UpdateLayout();

        double expectedHeight = 600 / Cos20;
        double expectedDelta = (expectedHeight - 600) / 2;
        Assert.Equal(600, viewport.ActualHeight, 6);
        Assert.Equal(expectedHeight, map.Height, 6);
        Assert.Equal(-expectedDelta, map.Margin.Top, 6);
        var center = map.TransformToAncestor(viewport).Transform(new Point(map.ActualWidth / 2, map.ActualHeight / 2));
        Assert.Equal(300, center.Y, 6);
    });

    [Fact]
    public void should_release_overscan_when_feature_is_toggled_off() => HousingTests.Sta(() =>
    {
        var (host, viewport, map) = BuildMainMapLikeXaml(640, 480);
        EnableTilt(map);
        host.UpdateLayout();
        Assert.False(double.IsNaN(map.Height));

        map.ToggleTiltFeature();                                          // OFF → 즉시 φ=0(G10) → φ_layout 즉시 정착
        map.FlushTiltReevaluation();
        Pump();
        host.UpdateLayout();

        Assert.Equal(0.0, map.TiltLayoutDeg, 6);
        Assert.True(double.IsNaN(map.Height));
        Assert.Equal(new Thickness(0), map.Margin);
        Assert.Equal(480, map.ActualHeight, 6);
        Assert.Equal(480, viewport.ActualHeight, 6);
    });
}
