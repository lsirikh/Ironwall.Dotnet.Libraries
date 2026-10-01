using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using GMap.NET;
using GMap.NET.WindowsPresentation;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Adorners;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Monitoring.Models.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using Xunit;

namespace GMaps.PropertyPanel.Tests;

/// <summary>
/// 조립 카드(L3)는 화면에 뜬 창이다 — 지도 줌 · 디지털 줌 · 틸트(지도의 보기 변환)와 무관하게 같은 DIU 크기로 그려지고,
/// 아이콘의 화면 자리에 붙어 지도 보기 영역 안에 갇힌다. 실제 지도 컨트롤 + 어도너 층을 화면 밖 창에 띄워 화면 사각형을 잰다.
/// </summary>
/// <remarks>
/// 결함(2026-10-01 사용자 보고): 카드를 지도 어도너 층에 띄우는데 어도너 층은 지도의 <c>RenderTransform</c>(디지털 줌 s · 틸트 s·cosφ)을
/// 어도너에도 걸어, 디지털 줌 2.0 에서 카드가 두 배로 · 틸트에서 납작하게 그려졌다.
/// 환경 변수 <c>IRONWALL_CARD_RENDER_DIR</c> 가 있으면 그 폴더에 PNG 를 남긴다(<c>IRONWALL_CARD_RENDER_TAG</c> = 파일 이름 앞말).
/// </remarks>
public class ComponentCardScreenSizeTests
{
    private const double Lat = 37.39423226055614, Lng = 126.96720778942108;

    private static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private static DeviceAxesModel Axes()
    {
        var spec = new HardwareSpecModel();
        spec.Components.Add(new ComponentDefinitionModel { Key = "ptz", Type = "PTZ_UNIT" });
        spec.Components.Add(new ComponentDefinitionModel { Key = "tracker", Type = "TRACKER" });
        spec.Components.Add(new ComponentDefinitionModel { Key = "ir", Type = "IR_LED", Label = "IR 조명" });
        spec.Components.Add(new ComponentDefinitionModel { Key = "wiper", Type = "WIPER" });
        var status = new DeviceStatusModel();
        status.Components["ptz"] = new ComponentStatusModel { State = "IDLE", Health = "OK" };
        status.Components["tracker"] = new ComponentStatusModel { State = "ACTIVE", Health = "OK" };
        status.Components["ir"] = new ComponentStatusModel { State = "ON", Health = "FAULT", FaultReason = "OVER_CURRENT" };
        status.Components["wiper"] = new ComponentStatusModel { State = "ON", Health = "OK" };
        return new DeviceAxesModel
        {
            HardwareSpec = spec,
            DeviceStatus = status,
            // 설정과 관측이 어긋난 줄 — 상태 칸에서 가장 긴 글("설정 끔 / 관측 켜짐")
            DeviceConfig = new DeviceConfigModel { ComponentOverrides = Newtonsoft.Json.Linq.JObject.Parse("""{ "wiper": { "enabled": false } }""") },
            Meta = new ResponseMeta("full", new[] { "hardware_spec", "device_status", "device_config" }),
        };
    }

    private static GMapPidsMarker Marker()
    {
        var marker = new GMapPidsMarker(Mock.Of<ILogService>(), new PidsSymbolModel
        {
            Title = "외곽1", DeviceType = EnumDeviceType.IpCamera, Latitude = Lat, Longitude = Lng, Width = 40, Height = 40, Zoom = 16,
        });
        marker.LinkedDevice = new CameraDeviceModel
        {
            Id = 301, DeviceNumber = 1, DeviceName = "외곽1", DeviceType = EnumDeviceType.IpCamera,
            Status = EnumDeviceStatus.ACTIVATED, IsEnable = true, Axes = Axes(),
        };
        return marker;
    }

    /// <summary>벤더 GMapControl 의 정적 판 템플릿 · 항목 템플릿 · 항목 스타일을 이 스레드에서 다시 만든다(MapLocateFitTests 와 같은 하네스 함정).</summary>
    private static GMapCustomControl Map()
    {
        var map = new GMapCustomControl { MinZoom = 1, MaxZoom = 21, Position = new PointLatLng(Lat, Lng) };
        map.Zoom = 18;
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
        return map;
    }

    private sealed record Shot(Rect Card, Rect Icon, Rect Viewport, Point Placement);

    /// <summary>지도 + 카드를 띄우고 <paramref name="arrange"/> 로 보기를 바꾼 뒤 카드 · 아이콘의 화면 사각형을 잰다.</summary>
    private static Shot Measure(Action<GMapCustomControl> arrange, string? pngName = null, double windowWidth = 900, double windowHeight = 600)
    {
        var map = Map();
        var decorator = new AdornerDecorator { ClipToBounds = true, Child = map };
        var root = new Grid { Background = new SolidColorBrush(Color.FromRgb(0xE8, 0xE4, 0xDA)) };
        root.Children.Add(decorator);
        var window = new Window
        {
            Width = windowWidth, Height = windowHeight, Left = -20000, Top = -20000,
            ShowActivated = false, ShowInTaskbar = false, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize,
            Content = root,
        };
        window.Show();
        using var marker = Marker();
        ComponentCardAdorner? adorner = null;
        try
        {
            Pump();
            // 아이콘 대역(40×40) — 지도 좌표의 것이라 지도 변환을 따라 커진다(카드와 대조)
            var icon = new Border { Width = 40, Height = 40, Background = Brushes.SteelBlue, BorderBrush = Brushes.Black, BorderThickness = new Thickness(1) };
            map.Markers.Add(new GMapMarker(new PointLatLng(Lat, Lng)) { Shape = icon, Offset = new Point(-20, -20) });
            Pump();

            var layer = AdornerLayer.GetAdornerLayer(map);
            Assert.NotNull(layer);
            adorner = new ComponentCardAdorner(map, new ComponentCardViewModel(marker, navigator: null, today: null));
            layer!.Add(adorner);
            Pump();

            arrange(map);
            Pump();
            adorner.InvalidateArrange();
            Pump();

            var card = adorner.Card;
            Assert.True(card.ActualWidth > 0 && card.ActualHeight > 0, "카드가 배치되지 않았다");
            var cardRect = card.TransformToAncestor(root).TransformBounds(new Rect(card.RenderSize));
            var iconRect = icon.TransformToAncestor(root).TransformBounds(new Rect(icon.RenderSize));
            var viewport = decorator.TransformToAncestor(root).TransformBounds(new Rect(decorator.RenderSize));

            if (pngName != null && Environment.GetEnvironmentVariable("IRONWALL_CARD_RENDER_DIR") is { Length: > 0 } dir)
            {
                var tag = Environment.GetEnvironmentVariable("IRONWALL_CARD_RENDER_TAG") ?? "after";
                Directory.CreateDirectory(dir);
                var bitmap = new RenderTargetBitmap((int)windowWidth, (int)windowHeight, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(root);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var file = File.Create(Path.Combine(dir, $"{tag}-{pngName}.png"));
                encoder.Save(file);
            }
            return new Shot(cardRect, iconRect, viewport, adorner.LastPlacement);
        }
        finally
        {
            if (adorner != null) { AdornerLayer.GetAdornerLayer(map)?.Remove(adorner); adorner.Dispose(); }
            window.Close();
        }
    }

    private static void AssertSameSize(Rect expected, Rect actual, string because)
    {
        Assert.True(Math.Abs(expected.Width - actual.Width) < 0.5 && Math.Abs(expected.Height - actual.Height) < 0.5,
            $"{because}: 카드 화면 크기 {expected.Width:F1}×{expected.Height:F1} → {actual.Width:F1}×{actual.Height:F1}");
    }

    [Fact]
    public void should_keep_card_screen_size_when_digital_zoom_doubles_the_map() => AppHost.Run(() =>
    {
        var flat = Measure(_ => { }, "dz0");
        var zoomed = Measure(map => map.DigitalZoomLevel = 3, "dz3");     // 디지털 줌 2.0×

        Assert.True(zoomed.Icon.Width > flat.Icon.Width * 1.9, $"시험 전제: 지도가 확대되지 않았다({flat.Icon.Width:F1} → {zoomed.Icon.Width:F1})");
        AssertSameSize(flat.Card, zoomed.Card, "디지털 줌 2.0");
        Assert.Equal(360, zoomed.Card.Width, 1);                           // 스타일의 DIU 폭 그대로
    });

    [Fact]
    public void should_keep_card_screen_size_when_integer_map_zoom_changes() => AppHost.Run(() =>
    {
        var z18 = Measure(_ => { });
        var z16 = Measure(map => map.Zoom = 16);

        AssertSameSize(z18.Card, z16.Card, "정수 줌 18 → 16");
    });

    [Fact]
    public void should_keep_card_upright_and_unsquashed_when_map_is_tilted() => AppHost.Run(() =>
    {
        var flat = Measure(_ => { });
        // 틸트 + 디지털 줌의 보기 변환(ApplyViewTransform 과 같은 모양: ScaleTransform(s, s·cosφ) — 중심 기준)
        var tilted = Measure(map => map.RenderTransform = new ScaleTransform(1.5, 1.5 * Math.Cos(35 * Math.PI / 180), map.ActualWidth / 2, map.ActualHeight / 2), "tilt35");

        Assert.True(tilted.Icon.Height < tilted.Icon.Width * 0.9, "시험 전제: 지도가 눌리지 않았다");
        AssertSameSize(flat.Card, tilted.Card, "틸트 35° · 1.5×");
    });

    [Fact]
    public void should_anchor_card_beside_icon_screen_position_and_clamp_inside_viewport_when_zoomed() => AppHost.Run(() =>
    {
        var zoomed = Measure(map => map.DigitalZoomLevel = 3);

        // 아이콘 화면 오른쪽 가장자리 + 틈(12) 에 붙고, 세로는 아이콘 화면 중심에 맞춘다
        // (UseLayoutRounding — 반 픽셀 반올림은 허용)
        Assert.InRange(zoomed.Card.Left - (zoomed.Icon.Right + 12), -1.0, 1.0);
        Assert.InRange((zoomed.Card.Top + zoomed.Card.Bottom) / 2 - (zoomed.Icon.Top + zoomed.Icon.Bottom) / 2, -1.0, 1.0);
        Assert.True(zoomed.Viewport.Contains(zoomed.Card), $"카드가 지도 밖으로 나갔다: {zoomed.Card} ⊄ {zoomed.Viewport}");
    });

    [Fact]
    public void should_flip_card_left_and_stay_inside_viewport_when_icon_is_near_right_edge() => AppHost.Run(() =>
    {
        // 좁은 지도(500px) — 아이콘(가운데 250) 오른쪽에는 카드(360)가 들어가지 않는다 → 왼쪽으로, 그래도 모자라면 지도 안으로 민다
        var narrow = Measure(map => map.DigitalZoomLevel = 2, windowWidth: 500, windowHeight: 400);

        Assert.True(narrow.Viewport.Contains(narrow.Card), $"카드가 지도 밖으로 나갔다: {narrow.Card} ⊄ {narrow.Viewport}");
        AssertSameSize(new Rect(0, 0, 360, narrow.Card.Height), narrow.Card, "좁은 지도");
    });

    [Fact]
    public void should_not_trim_long_state_text_when_card_shows_intent_mismatch() => AppHost.Run(() =>
    {
        using var marker = Marker();
        var vm = new ComponentCardViewModel(marker, navigator: null, today: null);
        var card = new ComponentCardControl { DataContext = vm };
        var window = new Window
        {
            Width = 500, Height = 600, Left = -20000, Top = -20000, ShowActivated = false, ShowInTaskbar = false,
            WindowStyle = WindowStyle.None, Content = card,
        };
        window.Show();
        try
        {
            Pump();
            var wiper = vm.Table.Rows.Single(r => r.Key == "wiper");
            Assert.Equal("설정 끔 / 관측 켜짐", wiper.StateText);
            var cells = Descendants<TextBlock>(card).Where(t => t.Text == wiper.StateText).ToList();
            Assert.NotEmpty(cells);
            foreach (var cell in cells)
            {
                // 한 줄에 다 들어간다(말줄임 · 줄바꿈 없이) — 글의 자연 폭이 칸 폭 안
                var natural = new TextBlock { Text = cell.Text, FontSize = cell.FontSize, FontFamily = cell.FontFamily, FontWeight = cell.FontWeight };
                natural.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Assert.True(natural.DesiredSize.Width <= cell.ActualWidth + 0.5, $"상태 칸이 좁다: 글 {natural.DesiredSize.Width:F1} > 칸 {cell.ActualWidth:F1}");
            }
        }
        finally { window.Close(); }
    });

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T hit) yield return hit;
            foreach (var deeper in Descendants<T>(child)) yield return deeper;
        }
    }
}
