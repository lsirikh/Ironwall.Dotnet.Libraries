using System.IO;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.Managers;
using Ironwall.Dotnet.Libraries.Events.Ui.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using Xunit;
using DeviceProvider = Ironwall.Dotnet.Libraries.Devices.Providers.DeviceProvider;

namespace GMaps.Housing.Tests;

/****************************************************************************
   Purpose      : 이벤트 → 지도 마커 · 구역선의 "그림" 헤드리스 확인(카탈로그 §2 EVM-006 · 007 · 054 · 055 · 056 · 065 · 066 · 074 · 078 ·
                  084 · 085 · 086 · 104 · 114 · 115 · 024 다크, r27 EVM-R27-01/02/04/05 의 헤드리스 판)
                  - NATS 봉투(가짜 서버 기록 뒤 같은 번호) → 실제 수신 서비스 → 실제 큐 → 실제 심볼 관리자 → 실제 마커(GMapPidsMarker)
                    → 실제 템플릿(PidsMarkerStyle · PidsGroupMarkerStyle). 부모 없는 비주얼을 Measure/Arrange + RenderTargetBitmap 으로만 본다(창 없음).
                  - 표시 점 색은 템플릿 부품 배경 + 그 자리 픽셀 둘 다 본다. 깜빡임 · 펄스는 애니메이션이 걸렸는지(HasAnimatedProperties)로 본다
                    (창이 없으면 렌더 시계가 돌지 않아 프레임 간 변화는 볼 수 없다 — 헤디드 EVM-R27-01 이 맡는다).
                  - 구역선은 지도 컨트롤이 있어야 점(위경도 → 화면)을 계산한다 — 템플릿 세 선에 같은 점을 직접 넣고 색 · 보임 · 깜빡임 트리거를 본다.
   Created By   : Claude
   Created On   : 2026-10-08
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public class EventMarkerPaintScenarioTests
{
    private static readonly string ArtifactDir = Environment.GetEnvironmentVariable("WP2_ARTIFACTS")
        ?? System.IO.Path.Combine(System.IO.Path.GetTempPath(), "evm-marker-artifacts");

    private static readonly Color Green = (Color)ColorConverter.ConvertFromString("#4CAF50");
    private static readonly Color Red = (Color)ColorConverter.ConvertFromString("#F44336");
    private static readonly Color Orange = (Color)ColorConverter.ConvertFromString("#FF9800");
    private static readonly Color Gray = Colors.Gray;
    private static readonly Color Black = Colors.Black;

    // ───────────────────────── 장치 ─────────────────────────

    /// <summary>NATS → 큐 → 심볼 관리자(호스트 배선과 같게) + 가짜 서버 번호. STA 스레드 안에서 만든다.</summary>
    private sealed class Pipeline : IDisposable
    {
        private readonly List<Func<MessageArgsModel, Task>> _handlers = new();
        private int _nextId = 3000;
        public EventQueueManager Queue { get; } = new();
        public SymbolEventManager Symbols { get; }
        public DeviceProvider Devices { get; } = new();

        public Pipeline()
        {
            var log = Mock.Of<ILogService>();
            var setup = new Mock<IEventSetupModel>();
            setup.SetupAllProperties();
            setup.Object.TimeDiscardSec = 3600;
            setup.Object.MalfunctionTimeDiscardSec = 3600;
            Symbols = new SymbolEventManager(Mock.Of<IEventAggregator>(), log, new EventSetupModel(setup.Object), Queue);
            Queue.OnDeviceStateChanged += Symbols.HandleDeviceStateChanged;   // EventUiModule 과 같다
            Queue.OnGroupStateChanged += Symbols.HandleGroupStateChanged;

            var nats = new Mock<INatsService>();
            nats.SetupAdd(n => n.NatsSubscribeEventAsync += It.IsAny<Func<MessageArgsModel, Task>>())
                .Callback<Func<MessageArgsModel, Task>>(h => _handlers.Add(h));
            new DetectionNatsSyncService(log, nats.Object, Symbols, Queue, setup.Object, null, null, Devices).StartService();
            new MalfunctionNatsSyncService(log, nats.Object, Symbols, Queue, setup.Object, null, null, Devices).StartService();
        }

        public void Dispose() => Queue.Dispose();

        public (GMapPidsMarker Marker, GMapMarkerPidsControl Control) Place(IBaseDeviceModel device, bool dark = false)
        {
            Devices.Add(device);
            var model = new PidsSymbolModel { Title = $"{device.DeviceType} {device.Id}", DeviceType = device.DeviceType };
            Symbols.RegisterDeviceSymbol(device, model);
            var marker = new GMapPidsMarker(Mock.Of<ILogService>(), model) { Width = 40, Height = 40, IsVisible = true };
            var control = new GMapMarkerPidsControl(marker) { Width = 40, Height = 40 };
            control.Resources.MergedDictionaries.Add(Tokens(dark));
            control.Resources.MergedDictionaries.Add(PidsStyles());
            control.Style = (Style)control.Resources[typeof(GMapMarkerPidsControl)];
            HousingTests.Layout(control, 40, 40);
            return (marker, control);
        }

        public (GMapPidsGroupMarker Marker, GMapMarkerPidsGroupControl Control) Zone(int groupId)
        {
            var model = new PidsGroupSymbolModel { Title = $"구역 {groupId}", LinkedDeviceGroup = groupId, StrokeThickness = 8 };
            Symbols.RegisterGroupSymbol(groupId, new BaseDeviceModel(), model);
            var marker = new GMapPidsGroupMarker(Mock.Of<ILogService>(), model) { IsVisible = true };
            var control = new GMapMarkerPidsGroupControl(marker) { Width = 200, Height = 60 };
            control.Resources.MergedDictionaries.Add(Tokens(false));
            control.Resources.MergedDictionaries.Add(GroupStyles());
            control.Style = (Style)control.Resources[typeof(GMapMarkerPidsGroupControl)];
            HousingTests.Layout(control, 200, 60);
            // 지도 없이 선 모양만 — 세 층(기본 · 장애 · 탐지)에 같은 점(컨트롤 로컬 px)
            var points = new PointCollection { new(10, 30), new(190, 30) };
            foreach (var part in new[] { "PART_MainPolyline", "PART_EventPolyline", "PART_DetectionPolyline" })
                Part<Polyline>(control, part).Points = points;
            HousingTests.Layout(control, 200, 60);
            return (marker, control);
        }

        public int Detect(int deviceId) => Publish("DETECT", "detection", "Intrusion", deviceId, null);
        public int Malfunction(int deviceId, string reason = "FAULT_FENCE") => Publish("MALFUNCTION", "malfunction", "Fault", deviceId, reason);

        /// <summary>가짜 서버에 기록(번호 발급) → 같은 번호로 NATS 봉투. 끝나면 UI 줄을 비운다.</summary>
        private int Publish(string cmd, string category, string typeEvent, int deviceId, string? reason)
        {
            var id = Interlocked.Increment(ref _nextId);
            var device = Devices.First(d => d.Id == deviceId);
            var json = $$"""
            { "id": "env-{{id}}", "m_type": "PUB", "cmd": "{{cmd}}", "from": "PidsProxy",
              "body": { "id": {{id}}, "category_event": "{{category}}", "type_event": "{{typeEvent}}", "reason": {{(reason is null ? "null" : $"\"{reason}\"")}},
                        "device": { "id": {{deviceId}}, "category_device": "{{Ironwall.Dotnet.Libraries.Messages.Helpers.DeviceTypeResolver.CategoryOf(device.DeviceType).ToString().ToLowerInvariant()}}" } } }
            """;
            var args = new MessageArgsModel("sensorway.unit001.all.event", null, json);
            foreach (var h in _handlers.ToList()) h(args).GetAwaiter().GetResult();
            Pump();
            return id;
        }

        /// <summary>원격 조치보고의 라이브러리 길(카드 없음): 번호 + 유형으로 큐 엔트리를 찾아 뺀다(EventCardListPanelViewModel.CloseRemotely).</summary>
        public void Report(int eventId, EnumEventType type)
        {
            var entry = Queue.FindEntryByEventId(eventId, type) ?? throw new InvalidOperationException($"엔트리 없음 {type} {eventId}");
            Queue.Dequeue(entry.EntryId!);
            Pump();
        }

        public void SyncDevice(int deviceId, EnumDeviceType type, EnumDeviceStatus status)
        {
            Symbols.SyncDeviceStatus(deviceId, type, status);
            Pump();
        }
    }

    private static ResourceDictionary PidsStyles() => new() { Source = new Uri("/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/PidsMarkerStyle.xaml", UriKind.Relative) };
    private static ResourceDictionary GroupStyles() => new() { Source = new Uri("/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/PidsGroupMarkerStyle.xaml", UriKind.Relative) };
    private static ResourceDictionary Tokens(bool dark) => new() { Source = new Uri($"/Ironwall.Dotnet.Libraries.Theme;component/Themes/Tokens.{(dark ? "Dark" : "Light")}.xaml", UriKind.Relative) };

    private static void Pump()
    {
        for (int i = 0; i < 3; i++) Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }

    private static T Part<T>(Control control, string name) where T : FrameworkElement
        => control.Template.FindName(name, control) as T ?? throw new InvalidOperationException($"{name} 없음");

    private static SensorDeviceModel Fence(int id, params int[] groups)
        => new() { Id = id, DeviceType = EnumDeviceType.Fence, CategoryDevice = EnumDeviceCategory.Sensor, Status = EnumDeviceStatus.ACTIVATED, DeviceGroups = groups.ToList() };

    // ── 그림 읽기 ──

    /// <summary>
    /// 템플릿 트리거의 이름 붙은 스토리보드(펄스 · 깜빡임)가 지금 도는가. <c>HasAnimatedProperties</c> 는 못 쓴다 —
    /// StopStoryboard 는 클록을 멈출 뿐 떼지 않아, 멈춘 뒤에도 true 로 남는다(2026-10-08 실측).
    /// </summary>
    private static bool IsRunning(Control control, string beginStoryboardName)
    {
        var begin = control.Template.Triggers
            .SelectMany(t => t.EnterActions)
            .OfType<System.Windows.Media.Animation.BeginStoryboard>()
            .FirstOrDefault(b => b.Name == beginStoryboardName)
            ?? throw new InvalidOperationException($"{beginStoryboardName} 없음");
        try { return begin.Storyboard.GetCurrentState(control) == System.Windows.Media.Animation.ClockState.Active; }
        catch (InvalidOperationException) { return false; }   // 한 번도 시작하지 않았다
    }

    private const string Pulse = "PulseStoryboard_Begin";          // PidsMarkerStyle 탐지 3겹 펄스
    private const string ErrorBlink = "ErrorBlinkStoryboard_Begin"; // PidsMarkerStyle ERROR 0.3초 깜빡임
    private const string LineDetecting = "BS_Detecting";            // PidsGroupMarkerStyle 탐지 선 1초 깜빡임
    private const string LineAlternate = "BS_FaultedDetecting";     // PidsGroupMarkerStyle 빨강/주황 번갈아

    private static Color MainStroke(GMapMarkerPidsGroupControl line) => ((SolidColorBrush)Part<Polyline>(line, "PART_MainPolyline").Stroke).Color;

    private static Color IndicatorBrush(GMapMarkerPidsControl control)
        => ((SolidColorBrush)Part<Border>(control, "PART_EventStatusIndicator").Background).Color;

    /// <summary>마커를 배경 위에 렌더해 표시 점 가운데 픽셀(3×3 평균)을 읽는다.</summary>
    private static Color IndicatorPixel(GMapMarkerPidsControl control, bool dark, string? save = null)
    {
        var host = new Canvas { Width = 120, Height = 120, Background = new SolidColorBrush(dark ? Color.FromRgb(0x0E, 0x14, 0x1B) : Color.FromRgb(0xE8, 0xEC, 0xF1)) };
        var parent = VisualTreeHelper.GetParent(control) as Panel;
        parent?.Children.Remove(control);
        Canvas.SetLeft(control, 40); Canvas.SetTop(control, 40);
        host.Children.Add(control);
        HousingTests.Layout(host, 120, 120);
        var indicator = Part<Border>(control, "PART_EventStatusIndicator");
        var center = indicator.TransformToAncestor(host).Transform(new Point(indicator.ActualWidth / 2, indicator.ActualHeight / 2));
        var bitmap = new RenderTargetBitmap(120, 120, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(host);
        if (save != null) Save(bitmap, save);
        host.Children.Remove(control);
        return Average(bitmap, (int)Math.Round(center.X), (int)Math.Round(center.Y));
    }

    /// <summary>구역선 가운데(100, 30) 픽셀.</summary>
    private static Color LinePixel(GMapMarkerPidsGroupControl control, string? save = null)
    {
        var host = new Canvas { Width = 200, Height = 60, Background = new SolidColorBrush(Color.FromRgb(0xE8, 0xEC, 0xF1)) };
        Canvas.SetLeft(control, 0); Canvas.SetTop(control, 0);
        host.Children.Add(control);
        HousingTests.Layout(host, 200, 60);
        var bitmap = new RenderTargetBitmap(200, 60, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(host);
        if (save != null) Save(bitmap, save);
        host.Children.Remove(control);
        return Average(bitmap, 100, 30);
    }

    private static Color Average(BitmapSource bitmap, int cx, int cy)
    {
        var rect = new Int32Rect(cx - 1, cy - 1, 3, 3);
        var pixels = new byte[3 * 3 * 4];
        bitmap.CopyPixels(rect, pixels, 12, 0);
        int b = 0, g = 0, r = 0;
        for (int i = 0; i < pixels.Length; i += 4) { b += pixels[i]; g += pixels[i + 1]; r += pixels[i + 2]; }
        return Color.FromRgb((byte)(r / 9), (byte)(g / 9), (byte)(b / 9));
    }

    private static void AssertNear(Color expected, Color actual, string what, int tolerance = 40)
    {
        var d = Math.Abs(expected.R - actual.R) + Math.Abs(expected.G - actual.G) + Math.Abs(expected.B - actual.B);
        Assert.True(d <= tolerance, $"{what}: 기대 {expected} · 실제 {actual} (차 {d})");
    }

    private static void Save(BitmapSource bitmap, string name)
    {
        try
        {
            Directory.CreateDirectory(ArtifactDir);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(new TransformedBitmap(bitmap, new ScaleTransform(4, 4))));
            using var stream = File.Create(System.IO.Path.Combine(ArtifactDir, name));
            encoder.Save(stream);
        }
        catch (IOException) { }   // 산출물은 눈 확인용 — 저장 실패가 판정을 바꾸지 않는다
    }

    // ───────────────────────── 시험 ─────────────────────────

    [Theory]
    [InlineData(false)]
    [InlineData(true)]   // EVM-024 · EVM-R27-05 다크
    public void should_paint_red_dot_with_pulse_on_detect_and_return_green_without_pulse_on_report_evm006_084_085(bool dark) => HousingTests.Sta(() =>
    {
        using var p = new Pipeline();
        var (marker, control) = p.Place(Fence(11), dark);
        var theme = dark ? "dark" : "light";
        AssertNear(Green, IndicatorPixel(control, dark), $"{theme} 처음");
        Assert.False(IsRunning(control, Pulse));

        var id = p.Detect(11);

        Assert.Equal(EnumEventStatus.Detecting, marker.EventStatus);
        Assert.Equal(Red, IndicatorBrush(control));
        AssertNear(Red, IndicatorPixel(control, dark, $"evm006-detecting-{theme}.png"), $"{theme} 탐지 점");
        Assert.True(IsRunning(control, Pulse), "탐지 3겹 펄스");
        Assert.True(Part<FrameworkElement>(control, "PulseRing1").Opacity > 0, "펄스 고리가 보인다");
        Assert.Equal("탐지 중", UIElementAutomationPeer.CreatePeerForElement(control).GetItemStatus());

        p.Report(id, EnumEventType.Intrusion);

        Assert.Equal(Green, IndicatorBrush(control));
        AssertNear(Green, IndicatorPixel(control, dark, $"evm085-reported-{theme}.png"), $"{theme} 조치 뒤 점");
        Assert.False(IsRunning(control, Pulse), "조치 뒤 펄스 멈춤");
        Assert.Equal(0, Part<FrameworkElement>(control, "PulseRing1").Opacity, 3);   // 고리 사라짐(기본값)
    });

    [Fact]
    public void should_paint_steady_orange_dot_without_blink_on_malfunction_evm054() => HousingTests.Sta(() =>
    {
        using var p = new Pipeline();
        var (_, control) = p.Place(Fence(12));

        var id = p.Malfunction(12);

        Assert.Equal(Orange, IndicatorBrush(control));
        AssertNear(Orange, IndicatorPixel(control, false, "evm054-fault.png"), "장애 점");
        Assert.False(IsRunning(control, ErrorBlink), "장애만으로는 깜빡이지 않는다(깜빡임은 ERROR 축)");
        Assert.False(IsRunning(control, Pulse));

        p.Report(id, EnumEventType.Fault);
        Assert.Equal(Green, IndicatorBrush(control));
    });

    [Fact]
    public void should_blink_icon_orange_when_server_marks_device_error_and_stop_on_activated_evm056_114() => HousingTests.Sta(() =>
    {
        using var p = new Pipeline();
        var (marker, control) = p.Place(Fence(13));
        var id = p.Malfunction(13);

        p.SyncDevice(13, EnumDeviceType.Fence, EnumDeviceStatus.ERROR);

        Assert.Equal(EnumOperationState.ERROR, marker.OperationState);
        Assert.Equal(Orange, IndicatorBrush(control));
        Assert.True(IsRunning(control, ErrorBlink), "ERROR 0.3초 깜빡임");

        p.Report(id, EnumEventType.Fault);
        p.SyncDevice(13, EnumDeviceType.Fence, EnumDeviceStatus.ACTIVATED);   // EVM-093 조치 → 서버 ACTIVATED

        Assert.Equal(Green, IndicatorBrush(control));
        Assert.False(IsRunning(control, ErrorBlink), "ACTIVATED 뒤 깜빡임 멈춤");
        Assert.Equal(1.0, Part<FrameworkElement>(control, "PART_ViewboxFenceContainer").Opacity, 3);
    });

    [Fact]
    public void should_gray_dot_and_dim_icon_when_sync_device_deactivated_evm115() => HousingTests.Sta(() =>
    {
        using var p = new Pipeline();
        var (_, control) = p.Place(Fence(14));

        p.SyncDevice(14, EnumDeviceType.Fence, EnumDeviceStatus.DEACTIVATED);

        Assert.Equal(Gray, IndicatorBrush(control));
        AssertNear(Gray, IndicatorPixel(control, false, "evm115-deactivated.png"), "비활성 점");
        Assert.Equal(0.3, Part<FrameworkElement>(control, "PART_FenceIconContainer").Opacity, 3);
    });

    [Fact]
    public void should_keep_red_pulse_over_error_and_deactivated_axes_when_detecting_evm104() => HousingTests.Sta(() =>
    {
        // 탐지 트리거가 템플릿 맨 뒤 — 운영 축(ERROR 주황 · DEACTIVATED 회색)보다 이벤트가 이긴다.
        using var p = new Pipeline();
        var (_, control) = p.Place(Fence(15));
        p.SyncDevice(15, EnumDeviceType.Fence, EnumDeviceStatus.DEACTIVATED);

        var id = p.Detect(15);

        Assert.Equal(Red, IndicatorBrush(control));
        Assert.True(IsRunning(control, Pulse));
        p.Report(id, EnumEventType.Intrusion);
        Assert.Equal(Gray, IndicatorBrush(control));   // 조치 뒤 운영 축 색으로 돌아간다
    });

    [Fact]
    public void should_show_orange_dot_without_pulse_when_sensor_both_detects_and_faults_evm074_075() => HousingTests.Sta(() =>
    {
        using var p = new Pipeline();
        var (marker, control) = p.Place(Fence(16));
        var detection = p.Detect(16);
        var fault = p.Malfunction(16);

        Assert.Equal(EnumCompositeEventStatus.FaultedDetecting, marker.CompositeStatus);
        Assert.Equal(Orange, IndicatorBrush(control));                                   // 마커 점은 장애 우선(설계)
        Assert.False(IsRunning(control, Pulse));  // 펄스 없음 — 마커만 보면 탐지가 가려진다(구역선은 번갈아 깜빡임, 보고에 적음)

        p.Report(detection, EnumEventType.Intrusion);
        Assert.Equal(Orange, IndicatorBrush(control));
        p.Report(fault, EnumEventType.Fault);
        Assert.Equal(Green, IndicatorBrush(control));
    });

    [Fact]
    public void should_paint_black_dot_on_controller_when_controller_faults_evm066() => HousingTests.Sta(() =>
    {
        using var p = new Pipeline();
        var controller = new ControllerDeviceModel { Id = 20, DeviceType = EnumDeviceType.Controller, CategoryDevice = EnumDeviceCategory.Controller, Status = EnumDeviceStatus.ACTIVATED };
        var (_, control) = p.Place(controller);

        var id = p.Malfunction(20, "FAULT_CONTROLLER");

        Assert.Equal(Black, IndicatorBrush(control));
        AssertNear(Black, IndicatorPixel(control, false, "evm066-blackout.png"), "제어기 무통신 점");
        p.Report(id, EnumEventType.Fault);
        Assert.Equal(Green, IndicatorBrush(control));
    });

    [Fact]
    public void should_blink_red_line_on_detect_and_collapse_on_report_evm007_008_086() => HousingTests.Sta(() =>
    {
        using var p = new Pipeline();
        p.Place(Fence(31, 5));
        var (_, line) = p.Zone(5);
        var normal = MainStroke(line);   // 모델 선 색(Lime → 팔레트 색)
        AssertNear(normal, LinePixel(line), "처음 선");

        var id = p.Detect(31);

        var detection = Part<Polyline>(line, "PART_DetectionPolyline");
        Assert.Equal(Visibility.Visible, detection.Visibility);
        Assert.True(IsRunning(line, LineDetecting), "탐지 선 1초 깜빡임");
        Assert.Equal(Red, ((SolidColorBrush)Part<Border>(line, "PART_EventStatusIndicator").Background).Color);   // EVM-008 표시점
        AssertNear(Red, LinePixel(line, "evm007-zone-detecting.png"), "탐지 선");

        p.Report(id, EnumEventType.Intrusion);

        Assert.Equal(Visibility.Collapsed, detection.Visibility);
        Assert.False(IsRunning(line, LineDetecting), "조치 뒤 깜빡임 멈춤");
        AssertNear(normal, LinePixel(line, "evm086-zone-reported.png"), "조치 뒤 선");
    });

    [Fact]
    public void should_paint_steady_orange_line_on_malfunction_evm055() => HousingTests.Sta(() =>
    {
        using var p = new Pipeline();
        p.Place(Fence(32, 6));
        var (_, line) = p.Zone(6);

        var id = p.Malfunction(32);

        var fault = Part<Polyline>(line, "PART_EventPolyline");
        Assert.Equal(Visibility.Visible, fault.Visibility);
        Assert.False(IsRunning(line, LineDetecting) || IsRunning(line, LineAlternate), "장애 선은 깜빡이지 않는다");
        AssertNear(Orange, LinePixel(line, "evm055-zone-fault.png"), "장애 선");
        p.Report(id, EnumEventType.Fault);
        AssertNear(MainStroke(line), LinePixel(line), "조치 뒤 선");
    });

    [Fact]
    public void should_alternate_red_and_orange_line_when_zone_has_detection_and_fault_evm078() => HousingTests.Sta(() =>
    {
        using var p = new Pipeline();
        p.Place(Fence(33, 7));
        var (_, line) = p.Zone(7);
        var detection = p.Detect(33);
        var fault = p.Malfunction(33);

        Assert.Equal(Visibility.Visible, Part<Polyline>(line, "PART_DetectionPolyline").Visibility);
        Assert.Equal(Visibility.Visible, Part<Polyline>(line, "PART_EventPolyline").Visibility);
        Assert.True(IsRunning(line, LineAlternate), "빨강/주황 반대 위상 깜빡임");

        p.Report(detection, EnumEventType.Intrusion);   // EVM-075 장애로 내려감
        Assert.Equal(Visibility.Collapsed, Part<Polyline>(line, "PART_DetectionPolyline").Visibility);
        Assert.False(IsRunning(line, LineAlternate), "번갈아 깜빡임 멈춤");
        Assert.Equal(1.0, Part<Polyline>(line, "PART_EventPolyline").Opacity, 3);   // 주황 층이 0 위상에 멈춰 사라지지 않는다
        AssertNear(Orange, LinePixel(line), "탐지 조치 뒤 선");
        p.Report(fault, EnumEventType.Fault);
    });

    [Fact]
    public void should_paint_black_line_for_member_zone_when_controller_blacks_out_evm065() => HousingTests.Sta(() =>
    {
        using var p = new Pipeline();
        var controller = new ControllerDeviceModel { Id = 40, DeviceType = EnumDeviceType.Controller, CategoryDevice = EnumDeviceCategory.Controller, Status = EnumDeviceStatus.ACTIVATED };
        p.Place(controller);
        p.Place(new SensorDeviceModel { Id = 41, DeviceType = EnumDeviceType.Fence, CategoryDevice = EnumDeviceCategory.Sensor, Controller = controller, DeviceGroups = new() { 8 } });
        var (_, line) = p.Zone(8);

        var id = p.Malfunction(40, "FAULT_CONTROLLER");

        AssertNear(Black, LinePixel(line, "evm065-zone-blackout.png"), "블랙아웃 선");
        Assert.False(IsRunning(line, LineDetecting) || IsRunning(line, LineAlternate), "블랙아웃은 깜빡이지 않는다");
        p.Report(id, EnumEventType.Fault);
        AssertNear(MainStroke(line), LinePixel(line), "복구 뒤 선");
    });

    [Theory]
    [InlineData(0, 0, false, false, true)]
    [InlineData(1, 0, true, false, false)]
    [InlineData(0, 1, false, true, false)]
    [InlineData(2, 1, true, true, false)]
    public void should_light_traffic_lamps_from_queue_counts_evm009_057_087(int detections, int faults, bool yellow, bool red, bool green) => HousingTests.Sta(() =>
    {
        // 상단 신호등은 큐 건수(OnActiveCountChanged)만 본다 — 같은 큐에서 건수를 받아 램프 규칙(TrafficLampLogic)을 돌린다.
        using var p = new Pipeline();
        (int D, int F) last = (-1, -1);
        p.Queue.OnActiveCountChanged += (d, f) => last = (d, f);
        var ids = new List<(int Id, EnumEventType Type)>();
        for (int i = 0; i < detections; i++) { p.Place(Fence(50 + i)); ids.Add((p.Detect(50 + i), EnumEventType.Intrusion)); }
        for (int i = 0; i < faults; i++) { p.Place(Fence(60 + i)); ids.Add((p.Malfunction(60 + i), EnumEventType.Fault)); }
        if (ids.Count == 0) last = p.Queue.GetActiveCounts();

        Assert.Equal((detections, faults), last);
        Assert.Equal(yellow, TrafficLampLogic.DetectionOn(true, last.D));
        Assert.Equal(red, TrafficLampLogic.FaultOn(true, last.F));
        Assert.Equal(green, TrafficLampLogic.GreenOn(true, last.D, last.F));

        foreach (var (id, type) in ids) p.Report(id, type);   // 모든 시나리오는 조치로 닫는다 → 0/0 이면 초록
        Assert.Equal((0, 0), p.Queue.GetActiveCounts());
        Assert.Equal((0, 0), last);
        Assert.True(TrafficLampLogic.GreenOn(true, last.D, last.F));
    });
}
