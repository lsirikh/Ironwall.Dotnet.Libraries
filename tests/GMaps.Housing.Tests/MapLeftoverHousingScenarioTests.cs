using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using System.Windows.Threading;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.Managers;
using Ironwall.Dotnet.Libraries.Events.Ui.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Components;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Symbols3D;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Monitoring.Models.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Ironwall.Dotnet.Monitoring.Models.Symbols.Defines;
using Moq;
using Xunit;
using DeviceProvider = Ironwall.Dotnet.Libraries.Devices.Providers.DeviceProvider;

namespace GMaps.Housing.Tests;

/****************************************************************************
   Purpose      : 시나리오 상태표의 남은 지도 항목을 창 없이(2026-10-11, W4)
                  - MAP-146: 3D 하우징(통문 · 함체) 탐지/장애 — NATS 봉투 → 실제 수신 서비스 → 큐 → 심볼 관리자 → 마커 → Housing3DMarkerStyle
                    트리거 → 표시 점 색 · 상태 LED 재질(mat_status) 색 · 펄스/깜빡임 스토리보드 시작 여부.
                  - MAP-147: 부팅 때 빈 그룹의 구역선 → 콘솔이 장비를 그 그룹에 넣음(DeviceGroupMembershipChangedMessage)
                    → 지도 뷰모델이 다시 등록 → 탐지가 구역선을 빨갛게(픽셀).
                  - MAP-148: PTZ 고장 → 히터 저하 → 정상 — 부품 이름표 글 · 자동화 이름 · 경고색 픽셀.
                  창 · HwndSource 없이 부모 없는 Measure/Arrange + RenderTargetBitmap 만 쓴다. 창이 없으면 렌더 시계가 돌지 않으므로
                  깜빡임 · 펄스는 "스토리보드가 시작됐는가(Active)" 로만 본다 — 프레임 사이 변화는 실창 몫.
   Created By   : Claude
   Created On   : 2026-10-11
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public class MapLeftoverHousingScenarioTests
{
    private static readonly Color Red = (Color)ColorConverter.ConvertFromString("#F44336");
    private static readonly Color Amber = (Color)ColorConverter.ConvertFromString("#F59E0B");
    private static readonly Color IndicatorGreen = (Color)ColorConverter.ConvertFromString("#48C9A0");
    private static readonly Color LedDefault = Color.FromRgb(74, 222, 213);   // HousingVisual.StatusBrush 기본값

    // ───────────────────────── 장치 ─────────────────────────

    /// <summary>NATS(탐지 · 장애) → 큐 → 심볼 관리자 — 호스트 배선(EventUiModule)과 같다. 가짜 서버가 번호를 발급한다.</summary>
    private sealed class Pipeline : IDisposable
    {
        private readonly List<Func<MessageArgsModel, Task>> _handlers = new();
        private int _nextId = 9100;
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
            Queue.OnDeviceStateChanged += Symbols.HandleDeviceStateChanged;
            Queue.OnGroupStateChanged += Symbols.HandleGroupStateChanged;
            var nats = new Mock<INatsService>();
            nats.SetupAdd(n => n.NatsSubscribeEventAsync += It.IsAny<Func<MessageArgsModel, Task>>())
                .Callback<Func<MessageArgsModel, Task>>(h => _handlers.Add(h));
            new DetectionNatsSyncService(log, nats.Object, Symbols, Queue, setup.Object, null, null, Devices).StartService();
            new MalfunctionNatsSyncService(log, nats.Object, Symbols, Queue, setup.Object, null, null, Devices).StartService();
        }

        public void Dispose() => Queue.Dispose();

        public int Detect(int deviceId) => Publish("DETECT", "detection", "Intrusion", deviceId, null);
        public int Malfunction(int deviceId) => Publish("MALFUNCTION", "malfunction", "Fault", deviceId, "FAULT_FENCE");

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

        /// <summary>원격 조치보고의 라이브러리 길 — 번호 + 유형으로 큐 엔트리를 찾아 뺀다.</summary>
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

        /// <summary>3D 하우징 컨트롤로 그린 장비 심볼(통문 · 함체 — 상태 LED 가 있는 모델).</summary>
        public (GMapPidsMarker Marker, GMapMarker3DHousingControl Control) Place3D(EnumDeviceType type, int id)
        {
            var device = new BaseDeviceModel { Id = id, DeviceType = type, CategoryDevice = Ironwall.Dotnet.Libraries.Messages.Helpers.DeviceTypeResolver.CategoryOf(type), Status = EnumDeviceStatus.ACTIVATED };
            Devices.Add(device);
            var model = new PidsSymbolModel { Title = $"{type} {id}", DeviceType = type };
            Symbols.RegisterDeviceSymbol(device, model);
            var marker = new GMapPidsMarker(Mock.Of<ILogService>(), model) { Width = 64, Height = 64, IsVisible = true };
            var control = new GMapMarker3DHousingControl(marker);
            control.Resources.MergedDictionaries.Add(Tokens());
            control.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/Housing3DMarkerStyle.xaml", UriKind.Relative) });
            control.Style = (Style)control.Resources[typeof(GMapMarker3DHousingControl)];
            marker.Shape = control;
            HousingTests.Layout(control, 64, 64);
            return (marker, control);
        }
    }

    private static ResourceDictionary Tokens() => new() { Source = new Uri("/Ironwall.Dotnet.Libraries.Theme;component/Themes/Tokens.Light.xaml", UriKind.Relative) };

    private static void Pump()
    {
        for (int i = 0; i < 3; i++) Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }

    private static T Part<T>(Control control, string name) where T : FrameworkElement
        => control.Template.FindName(name, control) as T ?? throw new InvalidOperationException($"{name} 없음");

    /// <summary>템플릿 트리거의 이름 붙은 스토리보드가 지금 도는가(StopStoryboard 뒤에도 HasAnimatedProperties 는 남으므로 시계 상태로 본다).</summary>
    private static bool IsRunning(Control control, string beginStoryboardName)
    {
        var begin = control.Template.Triggers
            .SelectMany(t => t.EnterActions)
            .OfType<BeginStoryboard>()
            .FirstOrDefault(b => b.Name == beginStoryboardName)
            ?? throw new InvalidOperationException($"{beginStoryboardName} 없음");
        try { return begin.Storyboard.GetCurrentState(control) == ClockState.Active; }
        catch (InvalidOperationException) { return false; }   // 한 번도 시작하지 않았다
    }

    private static Color ColorOf(Brush? brush) => Assert.IsAssignableFrom<SolidColorBrush>(brush).Color;

    private static Color Indicator(Control control) => ColorOf(Part<Border>(control, "PART_EventStatusIndicator").Background);

    /// <summary>하우징 3D 장면의 상태 LED(mat_status) 재질 색 — 발광 재질(EmissiveMaterial) 중 고정색 LED(mat_led)가 아닌 것.</summary>
    private static IReadOnlyList<Color> StatusLedColors(HousingVisual housing)
    {
        var viewport = Assert.IsType<Viewport3D>(housing.Children[0]);
        var scene = Assert.IsType<Model3DGroup>(viewport.Children[0].GetValue(ModelVisual3D.ContentProperty));
        var objects = Assert.IsType<Model3DGroup>(scene.Children.Last());
        return objects.Children.OfType<GeometryModel3D>()
            .Select(g => g.Material).OfType<MaterialGroup>()
            .Where(m => m.Children.OfType<EmissiveMaterial>().Any())
            .Select(m => ColorOf(m.Children.OfType<DiffuseMaterial>().First().Brush))
            .ToList();
    }

    private static Color Average(BitmapSource bitmap, int cx, int cy)
    {
        var pixels = new byte[3 * 3 * 4];
        bitmap.CopyPixels(new Int32Rect(cx - 1, cy - 1, 3, 3), pixels, 12, 0);
        int b = 0, g = 0, r = 0;
        for (int i = 0; i < pixels.Length; i += 4) { b += pixels[i]; g += pixels[i + 1]; r += pixels[i + 2]; }
        return Color.FromRgb((byte)(r / 9), (byte)(g / 9), (byte)(b / 9));
    }

    private static void AssertNear(Color expected, Color actual, string what, int tolerance = 40)
    {
        var d = Math.Abs(expected.R - actual.R) + Math.Abs(expected.G - actual.G) + Math.Abs(expected.B - actual.B);
        Assert.True(d <= tolerance, $"{what}: 기대 {expected} · 실제 {actual} (차 {d})");
    }

    /// <summary>3D 컨트롤을 밝은 바탕에 렌더해 우상단 표시 점 가운데 픽셀(3×3 평균)을 읽는다.</summary>
    private static Color IndicatorPixel(Control control)
    {
        var host = new Canvas { Width = 120, Height = 120, Background = new SolidColorBrush(Color.FromRgb(0xE8, 0xEC, 0xF1)) };
        Canvas.SetLeft(control, 28); Canvas.SetTop(control, 28);
        host.Children.Add(control);
        HousingTests.Layout(host, 120, 120);
        var indicator = Part<Border>(control, "PART_EventStatusIndicator");
        var center = indicator.TransformToAncestor(host).Transform(new Point(indicator.ActualWidth / 2, indicator.ActualHeight / 2));
        var bitmap = new RenderTargetBitmap(120, 120, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(host);
        host.Children.Remove(control);
        return Average(bitmap, (int)Math.Round(center.X), (int)Math.Round(center.Y));
    }

    // ───────────────────────── MAP-146 3D PIDS 탐지 / 장애 프레임 ─────────────────────────

    [Theory]
    [Trait("Scenario", "MAP-146")]
    [InlineData(EnumDeviceType.Gate)]
    [InlineData(EnumDeviceType.Enclosure)]
    public void should_paint_3d_housing_red_with_pulse_on_detect_and_amber_led_on_fault_when_events_arrive(EnumDeviceType type) => HousingTests.Sta(() =>
    {
        using var p = new Pipeline();
        var (marker, control) = p.Place3D(type, 4601);
        var housing = Part<HousingVisual>(control, "PART_Housing3D");
        Assert.Equal(IndicatorGreen, Indicator(control));
        Assert.Contains(LedDefault, StatusLedColors(housing));                     // 평소 LED 는 청록
        Assert.False(IsRunning(control, "PulseStoryboard_Begin"));

        // 탐지 — 빨강 점 · 빨강 LED · 3겹 펄스 시작
        var detection = p.Detect(4601);
        Assert.Equal(EnumEventStatus.Detecting, marker.EventStatus);
        Assert.Equal(EnumEventStatus.Detecting, control.EventStatus);
        Assert.Equal(Red, Indicator(control));
        Assert.Equal(Red, ColorOf(housing.StatusBrush));
        Assert.Contains(Red, StatusLedColors(housing));                            // 상태 → 재질 색까지 내려왔다(재생성 없이 재채색)
        Assert.True(IsRunning(control, "PulseStoryboard_Begin"), "탐지 펄스가 시작되지 않았다");
        AssertNear(Red, IndicatorPixel(control), $"{type} 탐지 점 픽셀");

        // 조치 — 원래 색 · 펄스 멈춤
        p.Report(detection, EnumEventType.Intrusion);
        Assert.Equal(IndicatorGreen, Indicator(control));
        Assert.Equal(LedDefault, ColorOf(housing.StatusBrush));
        Assert.DoesNotContain(Red, StatusLedColors(housing));
        Assert.False(IsRunning(control, "PulseStoryboard_Begin"), "조치 뒤에도 펄스가 돈다");

        // 장애 — 주황 점 · 주황 LED · 펄스 없음
        var fault = p.Malfunction(4601);
        Assert.Equal(EnumEventStatus.Fault, marker.EventStatus);
        Assert.Equal(Amber, Indicator(control));
        Assert.Equal(Amber, ColorOf(housing.StatusBrush));
        Assert.Contains(Amber, StatusLedColors(housing));
        Assert.False(IsRunning(control, "PulseStoryboard_Begin"), "장애에 탐지 펄스가 걸렸다");
        AssertNear(Amber, IndicatorPixel(control), $"{type} 장애 점 픽셀");

        // 서버가 장비를 ERROR 로 — 하우징 깜빡임(불투명도 스토리보드) 시작, ACTIVATED 로 멈춤
        p.SyncDevice(4601, type, EnumDeviceStatus.ERROR);
        Assert.Equal(EnumOperationState.ERROR, marker.OperationState);
        Assert.True(IsRunning(control, "HousingError"), "ERROR 깜빡임이 시작되지 않았다");
        p.Report(fault, EnumEventType.Fault);
        p.SyncDevice(4601, type, EnumDeviceStatus.ACTIVATED);
        Assert.False(IsRunning(control, "HousingError"), "ACTIVATED 뒤에도 깜빡인다");
        Assert.Equal(IndicatorGreen, Indicator(control));
        Assert.Equal(LedDefault, ColorOf(housing.StatusBrush));
    });

    // ───────────────────────── MAP-147 빈 그룹 구역선 → 장비 편입 → 탐지 ─────────────────────────

    private static Color MainStroke(GMapMarkerPidsGroupControl line) => ColorOf(Part<Polyline>(line, "PART_MainPolyline").Stroke);

    private static Color LinePixel(GMapMarkerPidsGroupControl control)
    {
        var host = new Canvas { Width = 200, Height = 60, Background = new SolidColorBrush(Color.FromRgb(0xE8, 0xEC, 0xF1)) };
        host.Children.Add(control);
        HousingTests.Layout(host, 200, 60);
        var bitmap = new RenderTargetBitmap(200, 60, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(host);
        host.Children.Remove(control);
        return Average(bitmap, 100, 30);
    }

    [Fact]
    [Trait("Scenario", "MAP-147")]
    public void should_turn_zone_line_red_when_empty_group_at_boot_gets_device_and_detection_arrives()
    {
        HousingTests.Sta(() =>
        {
            const int GroupId = 47;
            using var p = new Pipeline();

            // 부팅: 장비 31 은 어느 그룹에도 없다 — 그룹 47 의 구역선은 빈 그룹
            var sensor = new SensorDeviceModel { Id = 31, DeviceType = EnumDeviceType.Fence, CategoryDevice = EnumDeviceCategory.Sensor, Status = EnumDeviceStatus.ACTIVATED, DeviceGroups = new List<int>() };
            p.Devices.Add(sensor);
            p.Symbols.RegisterDeviceSymbol(sensor, new PidsSymbolModel { Title = "펜스 31", DeviceType = EnumDeviceType.Fence });
            var lineModel = new PidsGroupSymbolModel { Title = "구역 47", LinkedDeviceGroup = GroupId, StrokeThickness = 8, Latitude = 37.5, Longitude = 127.0 };
            lineModel.LinePoints = new List<GeoPoint> { new(37.5, 127.0, 0), new(37.5, 127.001, 0) };
            using var lineMarker = new GMapPidsGroupMarker(Mock.Of<ILogService>(), lineModel) { IsVisible = true };
            var line = new GMapMarkerPidsGroupControl(lineMarker) { Width = 200, Height = 60 };
            line.Resources.MergedDictionaries.Add(Tokens());
            line.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/PidsGroupMarkerStyle.xaml", UriKind.Relative) });
            line.Style = (Style)line.Resources[typeof(GMapMarkerPidsGroupControl)];
            HousingTests.Layout(line, 200, 60);
            var points = new PointCollection { new(10, 30), new(190, 30) };   // 지도 없이 선 모양만 — 세 층에 같은 점
            foreach (var part in new[] { "PART_MainPolyline", "PART_EventPolyline", "PART_DetectionPolyline" }) Part<Polyline>(line, part).Points = points;
            HousingTests.Layout(line, 200, 60);
            var normal = MainStroke(line);

            // 지도 뷰모델(생성자 없이) — 지도에는 이 구역선, 장비 공급자 · 심볼 관리자는 같은 파이프라인 것
            var map = new GMapCustomControl();
            map.Markers.Add(lineMarker);
            var vm = (MapViewModel)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(MapViewModel));
            typeof(MapViewModel).GetProperty(nameof(MapViewModel.MainMap))!.SetValue(vm, map);
            typeof(MapViewModel).GetField("_symbolEventManager", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(vm, p.Symbols);
            typeof(MapViewModel).GetField("<DeviceProvider>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(vm, p.Devices);
            vm.HandleAsync(new DeviceGroupMembershipChangedMessage(new[] { GroupId }), CancellationToken.None).GetAwaiter().GetResult();   // 부팅 동기 — 빈 그룹이라 미등록

            // 빈 그룹일 때의 탐지 — 구역선에 닿지 않는다(대조)
            var before = p.Detect(31);
            AssertNear(normal, LinePixel(line), "빈 그룹 탐지인데 구역선이 바뀌었다");
            Assert.Equal(EnumCompositeEventStatus.Normal, lineModel.CompositeStatus);
            p.Report(before, EnumEventType.Intrusion);

            // 콘솔이 장비 31 을 그룹 47 에 넣는다(공용 모델 제자리 수정 → 소속 변경 알림)
            sensor.DeviceGroups!.Add(GroupId);
            vm.HandleAsync(DeviceGroupMembershipChangedMessage.For(new[] { GroupId })!, CancellationToken.None).GetAwaiter().GetResult();
            Pump();

            // 이제 탐지가 오면 구역선이 빨개진다(탐지 층 보임 + 픽셀)
            var detection = p.Detect(31);
            Assert.Equal(EnumCompositeEventStatus.Detecting, lineModel.CompositeStatus);
            Assert.Equal(Visibility.Visible, Part<Polyline>(line, "PART_DetectionPolyline").Visibility);
            AssertNear(Red, LinePixel(line), "편입 뒤 탐지 구역선");

            // 조치 — 원색
            p.Report(detection, EnumEventType.Intrusion);
            Assert.Equal(Visibility.Collapsed, Part<Polyline>(line, "PART_DetectionPolyline").Visibility);
            AssertNear(normal, LinePixel(line), "조치 뒤 구역선");
        });
    }

    // ───────────────────────── MAP-148 부품 이름표: PTZ 고장 → 히터 저하 → 정상 ─────────────────────────

    private static DeviceAxesModel CameraParts(string ptz, string? ptzReason, string heater)
        => ComponentHealthSummaryTests.Axes(true,
            ("ptz", "PTZ_UNIT", null, null, "IDLE", ptz, ptzReason, true),
            ("heater", "HEATER", null, null, "ON", heater, null, true));

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

    /// <summary>아이콘 아래(이름표 자리) 영역에서 경고색에 가까운 픽셀 수. 우하단 배지(아이콘 박스 + 걸침 4px) 아래만 센다.</summary>
    private static int WarningPixelsBelowIcon(Canvas host, GMapMarkerPidsControl control, Color warning)
    {
        HousingTests.Layout(host, 200, 200);
        var bitmap = new RenderTargetBitmap(200, 200, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(host);
        var origin = control.TransformToAncestor(host).Transform(new Point(0, 0));
        int top = (int)Math.Ceiling(origin.Y + control.ActualHeight + ComponentStatusOverlay.Overhang + 1);
        var rect = new Int32Rect(0, top, 200, 200 - top);
        var px = new byte[rect.Width * rect.Height * 4];
        bitmap.CopyPixels(rect, px, rect.Width * 4, 0);
        int count = 0;
        for (int i = 0; i < px.Length; i += 4)
        {
            if (px[i + 3] < 200) continue;
            var d = Math.Abs(px[i + 2] - warning.R) + Math.Abs(px[i + 1] - warning.G) + Math.Abs(px[i] - warning.B);
            if (d <= 60) count++;
        }
        return count;
    }

    [Fact]
    [Trait("Scenario", "MAP-148")]
    public void should_show_ptz_fault_plate_then_heater_degraded_plate_then_nothing_when_component_health_recovers() => HousingTests.Sta(() =>
    {
        var host = new Canvas { Width = 200, Height = 200, Background = Brushes.White };
        host.Resources.MergedDictionaries.Add(Tokens());
        using var marker = new GMapPidsMarker(Mock.Of<ILogService>(), new PidsSymbolModel { Title = "북측 PTZ-07", DeviceType = EnumDeviceType.IpCamera });
        marker.Width = 40; marker.Height = 40; marker.IsVisible = true;
        var device = new BaseDeviceModel { Id = 48, DeviceType = EnumDeviceType.IpCamera, Status = EnumDeviceStatus.ACTIVATED, Axes = CameraParts("FAULT", "OVER_CURRENT", "OK") };
        marker.LinkedDevice = device;
        var control = new GMapMarkerPidsControl(marker);
        control.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/PidsMarkerStyle.xaml", UriKind.Relative) });
        control.Style = (Style)control.Resources[control.GetType()];
        control.Width = 40; control.Height = 40;
        control.MarkerScreenScale = 1.5;                                  // 화면 60px — 이름표가 그려지는 크기(48px 이상)
        Canvas.SetLeft(control, 80); Canvas.SetTop(control, 20);
        host.Children.Add(control);
        HousingTests.Layout(host, 200, 200);
        var overlay = FindOverlay(control);
        var warning = ColorOf(overlay.AlertBrush);                         // 테마 StatusWarningBrush(라이트)
        string Name() => UIElementAutomationPeer.CreatePeerForElement(control).GetName();

        // ① PTZ 고장 — 「PTZ 구동부 고장」(카탈로그의 「PTZ 고장」) · 경고색 테두리 · 채운 공구
        Assert.Equal("PTZ 구동부 고장", overlay.DrawnPlateText);
        Assert.StartsWith("PTZ", overlay.DrawnPlateText);
        Assert.Equal(ComponentPlateSeverity.Fault, overlay.Strip!.Severity);
        Assert.Contains("부품 고장 1 · 저하 0 / 2", Name());
        var faultWarm = WarningPixelsBelowIcon(host, control, warning);
        Assert.True(faultWarm >= 10, $"고장 이름표의 경고색 픽셀이 {faultWarm}개뿐");

        // ② 서버 재조회: PTZ 복구 · 히터 저하 — 「히터 저하」 · 빈 공구
        device.Axes = CameraParts("OK", null, "DEGRADED");
        marker.Model.SetUpdate();
        Pump();
        HousingTests.Layout(host, 200, 200);
        Assert.Equal("히터 저하", overlay.DrawnPlateText);
        Assert.Equal(ComponentPlateSeverity.Degraded, overlay.Strip!.Severity);
        Assert.Contains("부품 고장 0 · 저하 1 / 2", Name());
        var degradedWarm = WarningPixelsBelowIcon(host, control, warning);
        Assert.True(degradedWarm >= 5, $"저하 이름표의 경고색 픽셀이 {degradedWarm}개뿐");

        // ③ 정상 — 이름표 · 배지 없음 · 경고색 0
        device.Axes = CameraParts("OK", null, "OK");
        marker.Model.SetUpdate();
        Pump();
        HousingTests.Layout(host, 200, 200);
        Assert.Null(overlay.DrawnPlateText);
        Assert.False(overlay.IsStripDrawn, "정상인데 이름표가 남았다");
        Assert.False(overlay.IsBadgeDrawn, "정상인데 배지가 남았다");
        Assert.Contains("부품 정상 2", Name());
        Assert.Equal(0, WarningPixelsBelowIcon(host, control, warning));
    });
}
