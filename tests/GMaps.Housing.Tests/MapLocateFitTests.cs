using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using GMap.NET;
using GMap.NET.WindowsPresentation;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Xunit;

namespace GMaps.Housing.Tests;

/// <summary>
/// [지도에서 보기](FR-45) 맞춤 — <see cref="MapLocateResolver"/> 가 낸 경계로 실제 지도 컨트롤이 움직이는가(헤디드 EVT-E2E-12 의 헤드리스 짝).
/// 화면 밖 창(Loaded 가 돌아야 벤더 <c>_lazyEvents</c> 가 풀린다)에 컨트롤을 띄우고, 멀리 있는 심볼 하나를 요청해 중심 · 화면 위치를 본다.
/// </summary>
public class MapLocateFitTests
{
    private const double HomeLat = 37.39423226055614, HomeLng = 126.96720778942108;   // bin appsettings HomePosition(줌 18)

    private static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    [Fact]
    public void should_center_far_symbol_when_locate_plan_fit_applied() => HousingTests.Sta(() =>
    {
        // Arrange — 1200×800 지도 · 줌 18 · 심볼은 북동쪽 약 7 km(줌 18 에서 화면 밖 수천 px)
        var map = new GMapCustomControl { MinZoom = 1, MaxZoom = 21, Position = new PointLatLng(HomeLat, HomeLng) };
        map.Zoom = 18;
        // 하네스 함정(MapViewTiltOverscanBindingTests 와 같음): 벤더 GMapControl 은 판 템플릿 · 항목 템플릿 · 항목 스타일을 정적 필드로 공유한다(GMapControl.cs:794-796).
        // 다른 STA 스레드의 선행 시험이 먼저 만들면 Seal() · ApplyItemContainerStyle 이 "다른 스레드가 이 개체를 소유" 로 실행 순서에 따라 실패한다.
        // 벤더와 같은 모양(GMapControl.cs:826-852)을 이 스레드에서 다시 만들어 결정적으로 만든다.
        var itemsHost = new FrameworkElementFactory(typeof(Canvas));
        itemsHost.SetValue(Panel.IsItemsHostProperty, true);
        map.ItemsPanel = new ItemsPanelTemplate(itemsHost);
        var itemPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
        itemPresenter.SetBinding(ContentPresenter.ContentProperty, new System.Windows.Data.Binding("Shape"));
        map.ItemTemplate = new DataTemplate(typeof(GMapMarker)) { VisualTree = itemPresenter };
        var itemStyle = new Style();
        itemStyle.Setters.Add(new Setter(Canvas.LeftProperty, new System.Windows.Data.Binding("LocalPositionX")));
        itemStyle.Setters.Add(new Setter(Canvas.TopProperty, new System.Windows.Data.Binding("LocalPositionY")));
        itemStyle.Setters.Add(new Setter(Panel.ZIndexProperty, new System.Windows.Data.Binding("ZIndex")));
        map.ItemContainerStyle = itemStyle;
        var window = new Window
        {
            Width = 1200, Height = 800, Left = -20000, Top = -20000,
            ShowActivated = false, ShowInTaskbar = false, WindowStyle = WindowStyle.None,
            Content = map,
        };
        window.Show();
        try
        {
            Pump();
            var target = new PointLatLng(HomeLat + 0.045, HomeLng + 0.05);
            var shape = new Border { Width = 20, Height = 20, Background = Brushes.Red };
            var marker = new GMapMarker(target) { Shape = shape, Offset = new Point(-10, -10) };
            map.Markers.Add(marker);
            Pump();
            var before = CenterDistance(map, shape);
            var peer = new System.Windows.Automation.Peers.FrameworkElementAutomationPeer(shape);   // 심볼 peer(PidsMarkerAutomationPeer)의 기반 — 사각형 계산이 같다
            var peerBefore = peer.GetBoundingRectangle();

            var request = new MapLocateRequest(Guid.NewGuid(), new[] { 2259 }, "probe");
            var plan = MapLocateResolver.Plan(request, new[] { new MapLocateSymbol(2259, target.Lat, target.Lng, true) }, anchorSite: null);
            Assert.NotNull(plan.Fit);
            var fit = plan.Fit!.Value;

            // Act — MapViewModel.LocateDevices 와 같은 호출
            var moved = map.SetZoomToFitRect(RectLatLng.FromLTRB(fit.West, fit.North, fit.East, fit.South));
            Pump();

            // Assert — 중심이 심볼로 오고, 심볼이 화면 한가운데 근처에 그려진다
            Assert.True(moved, "SetZoomToFitRect 가 false — 맞춤이 적용되지 않았다");
            Assert.InRange(map.Position.Lat, target.Lat - 0.0005, target.Lat + 0.0005);
            Assert.InRange(map.Position.Lng, target.Lng - 0.0005, target.Lng + 0.0005);
            var after = CenterDistance(map, shape);
            var peerAfter = peer.GetBoundingRectangle();
            Assert.True(before > 1000, $"전제: 맞춤 전 심볼이 화면 밖이어야 한다(중심 거리 {before:F0}px)");
            Assert.True(after < 60, $"맞춤 뒤 심볼-중심 거리 {after:F0}px (전 {before:F0}px · 줌 {map.Zoom})");

            // UIA 가 보는 사각형도 따라온다 — 헤디드 EVT-E2E-12 는 이 값(화면 좌표)으로 판정한다. 화면 밖 심볼도 잘리지 않은 실제 자리로 보고된다.
            var screenCenter = map.PointToScreen(new Point(map.ActualWidth / 2, map.ActualHeight / 2));
            var peerMid = new Point(peerAfter.X + peerAfter.Width / 2, peerAfter.Y + peerAfter.Height / 2);
            Assert.NotEqual(peerBefore, peerAfter);
            Assert.True((peerMid - screenCenter).Length < 60, $"peer 사각형 {peerBefore} → {peerAfter} · 지도 중심(화면) {screenCenter}");
        }
        finally { window.Close(); }
    });

    private static double CenterDistance(GMapCustomControl map, FrameworkElement shape)
    {
        var p = shape.TransformToAncestor(map).Transform(new Point(shape.ActualWidth / 2, shape.ActualHeight / 2));
        return Math.Sqrt(Math.Pow(p.X - map.ActualWidth / 2, 2) + Math.Pow(p.Y - map.ActualHeight / 2, 2));
    }
}
