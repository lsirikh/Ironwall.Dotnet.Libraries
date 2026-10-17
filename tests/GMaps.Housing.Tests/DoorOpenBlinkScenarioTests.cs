using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Caliburn.Micro;
using GMap.NET;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.Managers;
using Ironwall.Dotnet.Libraries.Events.Ui.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Factories;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Components;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Symbols3D;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using Xunit;
using DeviceProvider = Ironwall.Dotnet.Libraries.Devices.Providers.DeviceProvider;

namespace GMaps.Housing.Tests;

/****************************************************************************
   Purpose      : "깜빡임 애니메이션도 넣어줘"(2026-10-13) — 통문 · 함체 문이 열려 있는 동안 지도 심볼의 문 표지(PART_DoorOpenMark)가 깜빡인다. 창 없음.
                  - DOOR-101: NATS OPERATION_EVENT 열림 → 깜빡임 시작 · 닫힘 → 멈춤 + 원래 픽셀. 통문 · 함체 × 2D(폴백) · 3D × 라이트/다크.
                  - DOOR-102: 부팅 복원(장비 door_status/gate_status=OPEN)만으로 깜빡임.
                  - DOOR-103: 탐지 · 장애 · ERROR 가 함께면 그 깜빡임이 이기고 문 표지는 고정으로 계속 보인다 — 풀리면 다시 깜빡인다.
                  - DOOR-104: 시스템 "애니메이션 줄이기" → 깜빡이지 않고 고정 표지.
                  - DOOR-105: 공용 시계 하나(같은 박자) · 숨김 · 화면 밖이면 멈춤 · 모두 닫히면 시계가 사라진다.
                  깜빡임 판독 = DoorOpenMark.IsBlinking(공용 시계에 물렸는가) + 마크 Opacity 의 애니메이션 시계 상태.
                  창이 없으면 렌더 시계가 돌지 않아 프레임 간 변화는 볼 수 없다 — 그림은 기본 Opacity(1)로 렌더된다.
   Created By   : Claude
   Created On   : 2026-10-13
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public class DoorOpenBlinkScenarioTests
{
    // ───────────────────────── 장치 ─────────────────────────

    /// <summary>NATS(탐지 · 운영) → 큐 → 심볼 관리자 · 지도 VM(재등록 · 부팅 복원) — DoorMarkerStatusScenarioTests 의 장치와 같은 배선.</summary>
    private sealed class Rig : IDisposable
    {
        private readonly List<Func<MessageArgsModel, Task>> _handlers = new();
        private int _nextId = 8000;
        public Mock<ILogService> Log { get; } = new();
        public EventQueueManager Queue { get; } = new();
        public SymbolEventManager Symbols { get; }
        public DeviceProvider Devices { get; } = new();
        public GMapCustomControl Map { get; } = new() { Width = 640, Height = 480 };
        public MapViewModel Vm { get; }

        public Rig()
        {
            DoorOpenMark.ClockWithoutWindow = true;   // 창 없이도 공용 시계를 걸어 본다(이 STA 스레드에만)
            // PC 의 'Windows 애니메이션 표시'(SystemParameters.ClientAreaAnimation)에 기대지 않는다 — 꺼진 PC 에서 깜빡임 시험 17건이 실패했다(10-16).
            // 애니메이션 줄이기 시험(DOOR-104)처럼 먼저 정한 값은 지킨다.
            DoorOpenMark.ReduceMotionOverride ??= false;
            var setup = new Mock<IEventSetupModel>();
            setup.SetupAllProperties();
            setup.Object.TimeDiscardSec = 3600;
            Symbols = new SymbolEventManager(Mock.Of<IEventAggregator>(), Log.Object, new EventSetupModel(setup.Object), Queue);
            Queue.OnDeviceStateChanged += Symbols.HandleDeviceStateChanged;
            Queue.OnGroupStateChanged += Symbols.HandleGroupStateChanged;
            var nats = new Mock<INatsService>();
            nats.SetupAdd(n => n.NatsSubscribeEventAsync += It.IsAny<Func<MessageArgsModel, Task>>())
                .Callback<Func<MessageArgsModel, Task>>(h => _handlers.Add(h));
            new DetectionNatsSyncService(Log.Object, nats.Object, Symbols, Queue, setup.Object, null, null, Devices).StartService();
            new OperationEventNatsSyncService(Log.Object, nats.Object, Symbols).StartService();

            Map.Position = new PointLatLng(37.3936, 126.9665);
            Map.Zoom = 18;
            Map.SetEditMode(false);

            Vm = (MapViewModel)RuntimeHelpers.GetUninitializedObject(typeof(MapViewModel));
            typeof(MapViewModel).GetProperty(nameof(MapViewModel.MainMap))!.SetValue(Vm, Map);
            SetField(Vm, "_log", Log.Object);
            SetField(Vm, "<DeviceProvider>k__BackingField", Devices);
            SetField(Vm, "_symbolEventManager", Symbols);
            SetField(Vm, "_eventQueueManager", Queue);
        }

        public void Dispose()
        {
            Queue.Dispose();
            DoorOpenMark.ReduceMotionOverride = null;   // 같은 스레드의 다음 시험에 새지 않게
        }

        /// <summary>문 심볼 하나를 지도에 올린다(제품 CreateMarkerControl 과 같은 갈래 · 테마 토큰 병합) → 장비 목록 도착(재등록 · 부팅 복원).</summary>
        public (GMapPidsMarker Marker, GMapMarkerPidsControl Control) Place(EnumDeviceType type, int deviceId, bool symbol3D, bool dark, string? serverDoor = null)
        {
            var center = Map.Position;
            var model = new PidsSymbolModel
            {
                Id = deviceId + 100000, Title = $"문 {deviceId}", DeviceType = type, LinkedDeviceId = deviceId,
                Latitude = center.Lat, Longitude = center.Lng, Width = 48, Height = 48, ShowShape = true,
            };
            var marker = (GMapPidsMarker)new MarkerFactory(Log.Object, Devices).CreateMarker(model);
            marker.IsVisible = true;
            GMapMarkerPidsControl control = symbol3D && HousingModels.DeviceKey(type) != null
                ? new GMapMarker3DHousingControl(marker)
                : new GMapMarkerPidsFallbackControl(marker);
            Assert.True(GMapMarkerPidsFallbackControl.NeedsFallback(type));   // 통문 · 함체의 2D 는 폴백 템플릿
            control.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri($"/Ironwall.Dotnet.Libraries.Theme;component/Themes/Tokens.{(dark ? "Dark" : "Light")}.xaml", UriKind.Relative) });
            control.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/Housing3DMarkerStyle.xaml", UriKind.Relative) });
            control.Style = (Style)control.Resources[control.GetType()];
            control.Width = 48; control.Height = 48;
            marker.Shape = control;
            HousingTests.Layout(control, 48, 48);
            Map.Markers.Add(marker);

            Devices.Add(type == EnumDeviceType.Gate
                ? new GateDeviceModel { Id = deviceId, DeviceType = type, CategoryDevice = Ironwall.Dotnet.Libraries.Messages.Helpers.DeviceTypeResolver.CategoryOf(type), Status = EnumDeviceStatus.ACTIVATED, GateStatus = serverDoor! }
                : new EnclosureDeviceModel { Id = deviceId, DeviceType = type, CategoryDevice = Ironwall.Dotnet.Libraries.Messages.Helpers.DeviceTypeResolver.CategoryOf(type), Status = EnumDeviceStatus.ACTIVATED, DoorStatus = serverDoor! });
            var task = (Task)typeof(MapViewModel).GetMethod("InitializeDeviceSymbolIntegration", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(Vm, null)!;
            task.GetAwaiter().GetResult();
            Pump();
            Assert.NotNull(marker.LinkedDevice);
            return (marker, control);
        }

        private string Category(int deviceId)
            => Ironwall.Dotnet.Libraries.Messages.Helpers.DeviceTypeResolver.CategoryOf(Devices.First(d => d.Id == deviceId).DeviceType).ToString().ToLowerInvariant();

        /// <summary>서버 OPERATION_EVENT — 열림/닫힘.</summary>
        public void Door(int deviceId, bool open)
        {
            var type = Devices.First(d => d.Id == deviceId).DeviceType;
            var reason = (type == EnumDeviceType.Gate ? "GATE_" : "ENCLOSURE_DOOR_") + (open ? "OPEN" : "CLOSED");
            Publish($$"""
               { "id": "op-{{Interlocked.Increment(ref _nextId)}}", "m_type": "PUB", "cmd": "OPERATION_EVENT", "from": "server",
                 "body": { "id": 1, "reason": "{{reason}}", "device": { "id": {{deviceId}}, "category_device": "{{Category(deviceId)}}" },
                           "detail": { "state": "{{(open ? "OPEN" : "CLOSED")}}" } } }
               """);
        }

        /// <summary>침입 탐지 DETECT — 번호를 돌려준다.</summary>
        public int Detect(int deviceId)
        {
            var id = Interlocked.Increment(ref _nextId);
            Publish($$"""
               { "id": "env-{{id}}", "m_type": "PUB", "cmd": "DETECT", "from": "PidsProxy",
                 "body": { "id": {{id}}, "category_event": "detection", "type_event": "Intrusion", "action_reported": false,
                           "device": { "id": {{deviceId}}, "category_device": "{{Category(deviceId)}}" } } }
               """);
            return id;
        }

        /// <summary>조치보고(카드 없음 길) — 큐에서 뺀다.</summary>
        public void Report(int eventId)
        {
            var entry = Queue.FindEntryByEventId(eventId, EnumEventType.Intrusion) ?? throw new InvalidOperationException($"엔트리 없음 {eventId}");
            Queue.Dequeue(entry.EntryId!);
            Pump();
        }

        private void Publish(string json)
        {
            var args = new MessageArgsModel("sensorway.unit001.all.event", null, json);
            foreach (var h in _handlers.ToList()) h(args).GetAwaiter().GetResult();
            Pump();
        }
    }

    private static void SetField(object target, string name, object? value)
    {
        for (var t = target.GetType(); t != null; t = t.BaseType)
        {
            var f = t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (f != null) { f.SetValue(target, value); return; }
        }
        throw new MissingFieldException(target.GetType().Name, name);
    }

    private static void Pump()
    {
        for (int i = 0; i < 3; i++) Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }

    private static DoorOpenMark Mark(Control control) => (DoorOpenMark)control.Template.FindName("PART_DoorOpenMark", control);

    private static string Name(Control control) => UIElementAutomationPeer.CreatePeerForElement(control).GetName();

    /// <summary>깜빡임 판독 — 공용 시계에 물렸고 그 시계가 마크 Opacity 를 실제로 움직이고 있다.</summary>
    private static void AssertBlinking(Control control, string what)
    {
        var mark = Mark(control);
        Assert.True(mark.IsBlinking, $"{what}: 깜빡여야 한다");
        Assert.True(mark.HasAnimatedProperties, $"{what}: Opacity 에 시계가 없다");
        Assert.NotNull(DoorOpenMark.SharedClock?.Controller);   // 뿌리 시계(멈추고 뗄 수 있다)
    }

    private static void AssertSteady(Control control, string what, bool drawn)
    {
        var mark = Mark(control);
        Assert.False(mark.IsBlinking, $"{what}: 깜빡이면 안 된다");
        Assert.False(mark.HasAnimatedProperties, $"{what}: Opacity 에 시계가 남았다");
        Assert.Equal(1.0, mark.Opacity, 3);   // 원래 모습
        Assert.Equal(drawn, mark.IsDrawn);
    }

    /// <summary>마커를 배경 위(120×120, 마커는 40,40)에 렌더해 좌상단 표지 자리(32~52)의 픽셀을 읽는다. 마커를 옮기지 않는다(VisualBrush).</summary>
    private static byte[] MarkRegion(Control control, bool dark)
    {
        Mark(control).InvalidateVisual();   // 창이 없으면 표지는 스스로 다시 그리기를 요청하지 않는다(렌더 패스를 일으키지 않게) — 시험이 부른다
        HousingTests.Layout(control, 48, 48);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(dark ? Color.FromRgb(0x0E, 0x14, 0x1B) : Color.FromRgb(0xE8, 0xEC, 0xF1)), null, new Rect(0, 0, 120, 120));
            dc.DrawRectangle(new VisualBrush(control)
            {
                Stretch = Stretch.None, ViewboxUnits = BrushMappingMode.Absolute, Viewbox = new Rect(-40, -40, 120, 120),
                ViewportUnits = BrushMappingMode.Absolute, Viewport = new Rect(0, 0, 120, 120), AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top,
            }, null, new Rect(0, 0, 120, 120));
        }
        var bitmap = new RenderTargetBitmap(120, 120, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var pixels = new byte[20 * 20 * 4];
        bitmap.CopyPixels(new Int32Rect(32, 32, 20, 20), pixels, 20 * 4, 0);
        return pixels;
    }

    /// <summary>영역 안에 표지 색(토큰)과 가까운 픽셀 수.</summary>
    private static int CountNear(byte[] pixels, Color color, int tolerance = 60)
    {
        int n = 0;
        for (int i = 0; i < pixels.Length; i += 4)
            if (Math.Abs(pixels[i + 2] - color.R) + Math.Abs(pixels[i + 1] - color.G) + Math.Abs(pixels[i] - color.B) <= tolerance) n++;
        return n;
    }

    private static double Luminance(Color c)
    {
        static double Lin(byte v) { var s = v / 255.0; return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4); }
        return 0.2126 * Lin(c.R) + 0.7152 * Lin(c.G) + 0.0722 * Lin(c.B);
    }

    private static double Contrast(Color a, Color b)
    {
        double l1 = Luminance(a), l2 = Luminance(b);
        return (Math.Max(l1, l2) + 0.05) / (Math.Min(l1, l2) + 0.05);
    }

    // ───────────────────────── DOOR-101 ─────────────────────────

    [Theory]
    [Trait("Scenario", "DOOR-101")]
    [InlineData(EnumDeviceType.Gate, false, false)]
    [InlineData(EnumDeviceType.Gate, false, true)]
    [InlineData(EnumDeviceType.Gate, true, false)]
    [InlineData(EnumDeviceType.Gate, true, true)]
    [InlineData(EnumDeviceType.Enclosure, false, false)]
    [InlineData(EnumDeviceType.Enclosure, false, true)]
    [InlineData(EnumDeviceType.Enclosure, true, false)]
    [InlineData(EnumDeviceType.Enclosure, true, true)]
    public void should_blink_door_mark_while_open_and_restore_original_pixels_when_closed_door101(EnumDeviceType type, bool symbol3D, bool dark) => HousingTests.Sta(() =>
    {
        using var rig = new Rig();
        var (_, control) = rig.Place(type, 6101, symbol3D, dark);
        Assert.Equal(symbol3D, control is GMapMarker3DHousingControl);
        var mark = Mark(control);
        rig.Door(6101, open: false);
        AssertSteady(control, "처음 닫힘", drawn: false);
        var closedBefore = MarkRegion(control, dark);
        var ink = ((SolidColorBrush)mark.MarkBrush!).Color;
        Assert.Equal(dark ? Color.FromRgb(0x4F, 0xB8, 0xFF) : Color.FromRgb(0x15, 0x58, 0x9F), ink);   // StatusInfoBrush 토큰(DynamicResource)

        // ── 열림 → 깜빡임 시작 ──
        rig.Door(6101, open: true);
        Assert.Equal(DoorIndicatorKind.Open, control.DoorStatus);
        AssertBlinking(control, "열림");
        Assert.True(mark.IsDrawn);
        Assert.StartsWith($"문 6101 · 이벤트 정상 · 장비 활성 · 문 열림", Name(control));   // UIA 이름은 깜빡임과 무관
        Assert.Equal("문 열림", control.SymbolToolTip!.Split(Environment.NewLine)[1]);
        var open = MarkRegion(control, dark);
        Assert.True(CountNear(open, ink) >= 12, $"표지 색 픽셀 {CountNear(open, ink)}개");
        Assert.True(CountNear(closedBefore, ink) < CountNear(open, ink));
        Assert.True(Contrast(ink, ((SolidColorBrush)mark.SurfaceFill!).Color) >= 4.5, "표지 글리프 · 바탕 대비");
        if (symbol3D)
        {
            // 문짝 그림은 그대로 — 열림 스토리보드가 끝까지 가면 문짝이 열린다(깜빡임은 표지 Opacity 만)
            var begin = control.Template.Triggers.SelectMany(t => t.EnterActions).OfType<BeginStoryboard>().Single(b => b.Name == "HousingDoorOpen");
            begin.Storyboard.SeekAlignedToLastTick(control, TimeSpan.FromSeconds(0.4), TimeSeekOrigin.BeginTime);
            var housing = (HousingVisual)control.Template.FindName("PART_Housing3D", control);
            Assert.Equal(1, housing.DoorOpen, 3);
            Assert.Equal(1.0, housing.Opacity, 3);   // 깜빡임은 하우징 Opacity 를 건드리지 않는다
            Assert.True(mark.IsBlinking);
        }

        // ── 닫힘 → 즉시 멈춤 + 원래 모습 ──
        rig.Door(6101, open: false);
        AssertSteady(control, "닫힘", drawn: false);
        Assert.Null(DoorOpenMark.SharedClock);   // 깜빡일 표지가 없으면 시계도 없다
        if (!symbol3D) Assert.Equal(closedBefore, MarkRegion(control, dark));   // 2D: 원래 픽셀 그대로
        else Assert.Equal(CountNear(closedBefore, ink), CountNear(MarkRegion(control, dark), ink));   // 3D: 문짝 닫힘 애니메이션은 렌더 시계 없이 멈춰 있어 표지 자리만 본다
    });

    // ───────────────────────── DOOR-102 ─────────────────────────

    [Theory]
    [Trait("Scenario", "DOOR-102")]
    [InlineData(EnumDeviceType.Gate, false)]
    [InlineData(EnumDeviceType.Gate, true)]
    [InlineData(EnumDeviceType.Enclosure, false)]
    [InlineData(EnumDeviceType.Enclosure, true)]
    public void should_blink_immediately_when_door_restored_open_at_boot_door102(EnumDeviceType type, bool symbol3D) => HousingTests.Sta(() =>
    {
        using var rig = new Rig();
        var (marker, control) = rig.Place(type, 6102, symbol3D, dark: false, serverDoor: "OPEN");   // 장비 정보 door_status/gate_status = OPEN

        Assert.Equal(EnumDoorState.Open, marker.DoorState);
        AssertBlinking(control, "부팅 복원 열림");
        Assert.Contains("문 열림", Name(control));

        rig.Door(6102, open: false);
        AssertSteady(control, "복원 뒤 닫힘", drawn: false);
    });

    // ───────────────────────── DOOR-103 ─────────────────────────

    [Theory]
    [Trait("Scenario", "DOOR-103")]
    [InlineData(EnumDeviceType.Gate, false)]
    [InlineData(EnumDeviceType.Gate, true)]
    [InlineData(EnumDeviceType.Enclosure, false)]
    [InlineData(EnumDeviceType.Enclosure, true)]
    public void should_hold_steady_door_mark_under_detection_and_fault_blink_and_resume_after_door103(EnumDeviceType type, bool symbol3D) => HousingTests.Sta(() =>
    {
        using var rig = new Rig();
        var (marker, control) = rig.Place(type, 6103, symbol3D, dark: true);
        rig.Door(6103, open: true);
        AssertBlinking(control, "열림");

        // 탐지 — 빨강 펄스가 이긴다, 문 표지는 고정으로 남는다
        var detection = rig.Detect(6103);
        Assert.Equal(EnumEventStatus.Detecting, control.EventStatus);
        AssertSteady(control, "탐지 중", drawn: true);
        Assert.Contains("문 열림", Name(control));
        rig.Report(detection);
        Assert.Equal(EnumEventStatus.Normal, control.EventStatus);
        AssertBlinking(control, "탐지 조치 뒤");

        // 장애(색 축) — 같은 규칙
        marker.EventStatus = EnumEventStatus.Fault;
        Pump();
        AssertSteady(control, "장애", drawn: true);
        marker.EventStatus = EnumEventStatus.Normal;
        Pump();
        AssertBlinking(control, "장애 해제 뒤");
    });

    // ───────────────────────── DOOR-104 ─────────────────────────

    [Theory]
    [Trait("Scenario", "DOOR-104")]
    [InlineData(EnumDeviceType.Gate, false)]
    [InlineData(EnumDeviceType.Enclosure, true)]
    public void should_show_steady_mark_without_blink_when_system_reduces_animation_door104(EnumDeviceType type, bool symbol3D) => HousingTests.Sta(() =>
    {
        DoorOpenMark.ReduceMotionOverride = true;   // SystemParameters.ClientAreaAnimation == false 와 같다(이 STA 스레드에만)
        using var rig = new Rig();
        var (_, control) = rig.Place(type, 6104, symbol3D, dark: false);

        rig.Door(6104, open: true);

        AssertSteady(control, "애니메이션 줄이기", drawn: true);
        Assert.True(CountNear(MarkRegion(control, false), ((SolidColorBrush)Mark(control).MarkBrush!).Color) >= 12, "고정 표지는 그려진다");
        Assert.Contains("문 열림", Name(control));
    });

    // ───────────────────────── DOOR-105 ─────────────────────────

    [Fact]
    [Trait("Scenario", "DOOR-105")]
    public void should_share_one_clock_and_stop_hidden_or_offscreen_marks_door105() => HousingTests.Sta(() =>
    {
        using var rig = new Rig();
        var (gateMarker, gate) = rig.Place(EnumDeviceType.Gate, 6150, symbol3D: true, dark: false);
        var (enclosureMarker, enclosure) = rig.Place(EnumDeviceType.Enclosure, 6151, symbol3D: false, dark: false);
        rig.Door(6150, open: true);
        rig.Door(6151, open: true);

        // 같은 박자 — 시계 하나
        AssertBlinking(gate, "통문");
        AssertBlinking(enclosure, "함체");
        var clock = DoorOpenMark.SharedClock;

        // 모양 끔(숨김) → 멈춤, 다시 켜면 같은 시계로 다시 깜빡임
        gate.ShowShape = false;
        Assert.Equal(Visibility.Collapsed, Mark(gate).Visibility);
        AssertSteady(gate, "모양 끔", drawn: false);
        AssertBlinking(enclosure, "다른 심볼은 계속");
        Assert.Same(clock, DoorOpenMark.SharedClock);
        gate.ShowShape = true;
        AssertBlinking(gate, "모양 켬");

        // 화면 밖 — 지도가 뷰포트 판정(ViewArea)으로 내려 준다. 창 없는 지도의 영역은 실제 화면과 달라 심볼을 그 영역 밖/안으로 옮겨 본다.
        HousingTests.Layout(rig.Map, 640, 480);
        var area = rig.Map.ViewArea;
        Assert.True(area.WidthLng > 0 && area.HeightLat > 0, $"지도 영역 {area}");
        var outside = new PointLatLng(area.Bottom - 1, area.Left + area.WidthLng / 2);
        gateMarker.Position = outside;
        enclosureMarker.Position = outside;
        rig.Map.RefreshComponentStripDensity();
        Assert.False(DoorOpenMark.GetIsInView(gate));
        AssertSteady(gate, "화면 밖", drawn: true);
        AssertSteady(enclosure, "화면 밖", drawn: true);
        Assert.Null(DoorOpenMark.SharedClock);   // 깜빡일 표지가 없으면 시계를 뗀다
        gateMarker.Position = area.LocationMiddle;
        enclosureMarker.Position = area.LocationMiddle;
        rig.Map.RefreshComponentStripDensity();
        AssertBlinking(gate, "화면 안 복귀");
        AssertBlinking(enclosure, "화면 안 복귀");

        // 닫힘 — 하나씩
        rig.Door(6150, open: false);
        AssertSteady(gate, "통문 닫힘", drawn: false);
        AssertBlinking(enclosure, "함체는 아직 열림");
        rig.Door(6151, open: false);
        Assert.Null(DoorOpenMark.SharedClock);
    });
}
