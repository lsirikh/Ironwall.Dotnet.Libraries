using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using Xunit;

namespace GMaps.Housing.Tests;

/// <summary>
/// 마커 쪽 — 갱신 통지 스레드(A4) · UIA peer(A6) · 부품 배지 · 문 표시 렌더(Part B, 라이트/다크).
/// </summary>
public class MarkerComponentBadgeTests
{
    private static readonly string ArtifactDir = Environment.GetEnvironmentVariable("WP2_ARTIFACTS")
        ?? Path.Combine(Path.GetTempPath(), "wp2-marker-artifacts");

    private static ResourceDictionary Housing() => new() { Source = new Uri("/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/Housing3DMarkerStyle.xaml", UriKind.Relative) };
    private static ResourceDictionary Pids2D() => new() { Source = new Uri("/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/PidsMarkerStyle.xaml", UriKind.Relative) };
    private static ResourceDictionary Tokens(bool dark) => new() { Source = new Uri($"/Ironwall.Dotnet.Libraries.Theme;component/Themes/Tokens.{(dark ? "Dark" : "Light")}.xaml", UriKind.Relative) };

    private static void Pump() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

    private static BaseDeviceModel FaultyEnclosure(int id, string actuatorState = "CLOSED")
        => new()
        {
            Id = id,
            DeviceType = EnumDeviceType.Enclosure,
            Status = EnumDeviceStatus.ACTIVATED,
            Axes = ComponentHealthSummaryTests.Axes(true,
                ("heater_1", "HEATER", null, null, "ON", "FAULT", "OVER_TEMP", true),
                ("fan_1", "FAN", null, null, "OFF", "FAULT", "OVER_CURRENT", true),
                ("door", "DOOR_ACTUATOR", null, null, actuatorState, "OK", null, true)),
        };

    private static T Attach<T>(T control, ResourceDictionary styles, bool dark, double size = 40) where T : GMapMarkerPidsControl
    {
        control.Resources.MergedDictionaries.Add(Tokens(dark));
        control.Resources.MergedDictionaries.Add(styles);
        control.Style = (Style)control.Resources[control.GetType()];
        HousingTests.Layout(control, size, size);
        return control;
    }

    [Fact]
    public void should_raise_marker_notifications_on_ui_thread_when_model_updates_from_nats_thread() => HousingTests.Sta(() =>
    {
        // A4: SyncFromDevice 는 NATS 스레드에서 SetUpdate 를 부른다 — 속성창(GMapPropertyPidsControl)이 교차 스레드 예외로 죽던 것.
        using var marker = new GMapPidsMarker(Mock.Of<ILogService>(), new PidsSymbolModel { DeviceType = EnumDeviceType.Enclosure });
        var control = Attach(new GMapMarkerPidsFallbackControl(marker), Housing(), dark: false);
        marker.Shape = control;
        var uiThread = Environment.CurrentManagedThreadId;
        var threads = new List<int>();
        ((INotifyPropertyChanged)marker).PropertyChanged += (_, e) => { if (e.PropertyName == nameof(GMapPidsMarker.DoorState)) threads.Add(Environment.CurrentManagedThreadId); };

        var worker = new Thread(() => { for (int i = 0; i < 5; i++) marker.Model.SetUpdate(); });
        worker.Start(); worker.Join();
        Assert.Empty(threads);            // 아직 UI 로 넘어가지 않았다(작업 스레드에서 바로 통지하지 않는다)
        Pump();

        Assert.Single(threads);           // 5번이 한 번으로 합쳐졌다
        Assert.Equal(uiThread, threads[0]);
    });

    [Fact]
    public void should_build_component_summary_when_device_links_and_axes_swap_on_sync() => HousingTests.Sta(() =>
    {
        using var marker = new GMapPidsMarker(Mock.Of<ILogService>(), new PidsSymbolModel { DeviceType = EnumDeviceType.Enclosure });
        Assert.Same(ComponentHealthSummary.None, marker.ComponentSummary);

        var device = FaultyEnclosure(21);
        marker.LinkedDevice = device;                                    // 부팅 재바인딩
        Assert.Equal(ComponentHealthLevel.Fault, marker.ComponentSummary.Health);
        Assert.Equal(2, marker.ComponentSummary.FaultCount);

        device.Axes = ComponentHealthSummaryTests.Axes(true, ("heater_1", "HEATER", null, null, "ON", "OK", null, true));   // SYNC_DEVICE 재조회 = 참조 교체
        marker.Model.SetUpdate();                                        // SyncFromDevice → SetUpdate(같은 스레드면 즉시)
        Assert.Equal(ComponentHealthLevel.Ok, marker.ComponentSummary.Health);

        marker.ResetLiveState();                                         // 장비 삭제(A3)
        Assert.Same(ComponentHealthSummary.None, marker.ComponentSummary);
        Assert.Equal(EnumOperationState.NONE, marker.OperationState);
        Assert.Equal(21, marker.LinkedDeviceId);                          // 저장된 연결은 남긴다
    });

    [Fact]
    public void should_expose_symbol_identity_and_status_through_uia_peer() => HousingTests.Sta(() =>
    {
        // A6: 지도 심볼이 UIA 트리에서 장비별로 식별되고 이름에 상태가 실린다.
        using var marker = new GMapPidsMarker(Mock.Of<ILogService>(), new PidsSymbolModel { Title = "함체 동문", DeviceType = EnumDeviceType.Enclosure });
        marker.LinkedDevice = FaultyEnclosure(31, "RUNNING");
        marker.CompositeStatus = EnumCompositeEventStatus.Detecting;
        marker.EventStatus = EnumEventStatus.Detecting;
        var control = Attach(new GMapMarkerPidsFallbackControl(marker), Housing(), dark: false);

        var peer = UIElementAutomationPeer.CreatePeerForElement(control);
        Assert.IsType<PidsMarkerAutomationPeer>(peer);
        Assert.Equal("GMaps.Symbol.Enclosure.31", peer.GetAutomationId());
        Assert.Equal("함체 동문 · 이벤트 탐지 중 · 장비 상태 없음 · 문 동작 중 · 부품 고장 2 · 저하 0 / 3", peer.GetName());
        Assert.Equal("탐지 중", peer.GetItemStatus());
        Assert.Null(peer.GetChildren());                                   // 템플릿을 걷지 않는다(수백 개 아이콘)
        Assert.Equal(AutomationControlType.Custom, peer.GetAutomationControlType());
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void should_draw_badge_and_door_by_shape_when_component_faults_in_light_and_dark(bool dark) => HousingTests.Sta(() =>
    {
        var theme = dark ? "dark" : "light";
        var healthy = RenderGallery(dark, faulty: false, out _);
        var faulty = RenderGallery(dark, faulty: true, out var overlays);

        foreach (var overlay in overlays)
        {
            Assert.True(overlay.IsBadgeDrawn, $"{theme}: 배지 미표시");
            Assert.Equal(ComponentHealthLevel.Fault, overlay.Health);
            Assert.Equal(2, overlay.BadgeCount);
        }
        Assert.True(overlays[0].IsDoorDrawn, "2D 폴백(함체) 문 표시 없음");     // 2D: 위치까지 표시
        Assert.Equal(DoorIndicatorKind.Running, overlays[0].Door);
        Assert.Equal(DoorIndicatorKind.Running, overlays[1].Door);               // 3D: 구동 중은 표시
        Assert.Equal(DoorIndicatorKind.None, overlays[2].Door);                  // 2D 카메라: 문 없음

        // 모양 검증 — 우하단(배지)만 달라지고 우상단(이벤트 점)은 그대로다.
        for (int cell = 0; cell < 3; cell++)
        {
            Assert.True(DiffCount(healthy, faulty, CellRect(cell, BadgeCorner.BottomRight)) > 20, $"{theme} cell{cell}: 배지 픽셀 없음");
            Assert.Equal(0, DiffCount(healthy, faulty, CellRect(cell, BadgeCorner.TopRight)));
        }
        Save(faulty, $"marker-component-badge-{theme}.png");
        Save(healthy, $"marker-component-healthy-{theme}.png");
    });

    [Fact]
    public void should_hide_component_layer_when_shape_hidden_or_marker_small() => HousingTests.Sta(() =>
    {
        using var marker = new GMapPidsMarker(Mock.Of<ILogService>(), new PidsSymbolModel { DeviceType = EnumDeviceType.Enclosure });
        marker.LinkedDevice = FaultyEnclosure(41);
        marker.Width = 20; marker.Height = 20;
        var control = Attach(new GMapMarkerPidsFallbackControl(marker), Housing(), dark: false, size: 20);
        var overlay = FindOverlay(control);
        Assert.False(overlay.IsBadgeDrawn);                 // 20px < 24px
        control.MarkerScreenScale = 1.5; HousingTests.Layout(control, 20, 20);
        Assert.True(overlay.IsBadgeDrawn);                  // 디지털 줌 30px
        control.ShowShape = false;
        Assert.False(overlay.IsBadgeDrawn);
    });

    // ── helpers ──

    private enum BadgeCorner { BottomRight, TopRight }

    private const int Cell = 96, Pad = 28, Size = 40;

    private static Int32Rect CellRect(int cell, BadgeCorner corner)
        => corner == BadgeCorner.BottomRight
            ? new Int32Rect(cell * Cell + Pad + Size - 10, Pad + Size - 10, 16, 16)
            : new Int32Rect(cell * Cell + Pad + Size - 8, Pad - 6, 12, 10);

    private static RenderTargetBitmap RenderGallery(bool dark, bool faulty, out List<ComponentStatusOverlay> overlays)
    {
        var host = new Canvas { Width = Cell * 3, Height = Cell, Background = dark ? new SolidColorBrush(Color.FromRgb(0x0E, 0x14, 0x1B)) : new SolidColorBrush(Color.FromRgb(0xE8, 0xEC, 0xF1)) };
        host.Resources.MergedDictionaries.Add(Tokens(dark));
        overlays = new List<ComponentStatusOverlay>();
        var specs = new (EnumDeviceType Type, Func<GMapPidsMarker, GMapMarkerPidsControl> Make, ResourceDictionary Styles)[]
        {
            (EnumDeviceType.Enclosure, m => new GMapMarkerPidsFallbackControl(m), Housing()),    // 2D 폴백
            (EnumDeviceType.Enclosure, m => new GMapMarker3DHousingControl(m), Housing()),       // 3D
            (EnumDeviceType.IpCamera, m => new GMapMarkerPidsControl(m), Pids2D()),              // 2D 레거시 템플릿
        };
        for (int i = 0; i < specs.Length; i++)
        {
            var marker = new GMapPidsMarker(Mock.Of<ILogService>(), new PidsSymbolModel { Title = $"s{i}", DeviceType = specs[i].Type });
            marker.Width = Size; marker.Height = Size; marker.IsVisible = true;
            var device = FaultyEnclosure(50 + i, "RUNNING");
            device.DeviceType = specs[i].Type;
            if (!faulty) device.Axes = ComponentHealthSummaryTests.Axes(true, ("door", "DOOR_ACTUATOR", null, null, "RUNNING", "OK", null, true));
            marker.LinkedDevice = device;
            var control = specs[i].Make(marker);
            control.Resources.MergedDictionaries.Add(specs[i].Styles);
            control.Style = (Style)control.Resources[control.GetType()];
            control.Width = Size; control.Height = Size;
            Canvas.SetLeft(control, i * Cell + Pad); Canvas.SetTop(control, Pad);
            host.Children.Add(control);
        }
        HousingTests.Layout(host, Cell * 3, Cell);
        foreach (GMapMarkerPidsControl c in host.Children) overlays.Add(FindOverlay(c));
        var bitmap = new RenderTargetBitmap(Cell * 3, Cell, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(host);
        return bitmap;
    }

    private static ComponentStatusOverlay FindOverlay(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is ComponentStatusOverlay found) return found;
            try { return FindOverlay(child); } catch (InvalidOperationException) { }
        }
        throw new InvalidOperationException("ComponentStatusOverlay 없음");
    }

    private static int DiffCount(BitmapSource a, BitmapSource b, Int32Rect rect)
    {
        int stride = rect.Width * 4;
        var pa = new byte[stride * rect.Height]; var pb = new byte[pa.Length];
        a.CopyPixels(rect, pa, stride, 0); b.CopyPixels(rect, pb, stride, 0);
        int diff = 0;
        for (int i = 0; i < pa.Length; i += 4)
            if (Math.Abs(pa[i] - pb[i]) + Math.Abs(pa[i + 1] - pb[i + 1]) + Math.Abs(pa[i + 2] - pb[i + 2]) > 24) diff++;
        return diff;
    }

    private static void Save(BitmapSource bitmap, string name)
    {
        Directory.CreateDirectory(ArtifactDir);
        // 4배 확대본을 함께 남긴다 — 12px 배지를 눈으로 확인하기 위해.
        var scaled = new TransformedBitmap(bitmap, new ScaleTransform(4, 4));
        foreach (var (src, file) in new[] { ((BitmapSource)bitmap, name), (scaled, name.Replace(".png", "@4x.png")) })
        {
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(src));
            using var stream = File.Create(Path.Combine(ArtifactDir, file));
            encoder.Save(stream);
        }
    }
}
