using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media.Animation;
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
   Purpose      : "낫츠 메시지로 통문 · 함체 열림" 실창(2026-10-13 15:21) 결함 재현 · 회귀 막이 — 창 없음
                  - DOOR-001 · 002: 장비 목록이 마커보다 늦게 와 LinkedDevice 바인딩이 처음에 실패(MarkerFactory 경고)한 뒤
                    지도 재등록(MapViewModel.InitializeDeviceSymbolIntegration) → NATS OPERATION_EVENT 열림/닫힘 →
                    마커 DoorState · 알림용 DoorStatus · UIA 이름 「문 열림/닫힘」 · 툴팁 · (3D) 문짝 끝 자세. 통문 · 함체 × Symbol3D 켬/끔.
                    실창 결함: 3D 하우징은 배지(DoorIndicator)를 문짝에 맡기며 None 이 됐고, UIA 이름 · 툴팁이 같은 값을 써 문 절이 사라졌다.
                  - DOOR-003: 부팅 복원(RestoreDoorStateFromDevice — 장비 door_status/gate_status) 만으로도 이름에 문 절.
                  - DOOR-004: 운영 모드(마커 HitTest=False 는 의도 — 팬/줌 통과) 우클릭 수동 스캔(GetMarkerAtScreen)이
                    구역선 bbox 안의 3D 함체를 구역보다 먼저 고른다(실창: 운영 모드에서 구역 메뉴가 떴다).
                  - EVM-047: 함체 접점(ContactOn/Off) → 3D 마커 이름의 문 절.
                  부모 없는 비주얼을 Measure/Arrange 로만 본다(Window · HwndSource 없음). 렌더 시계가 돌지 않으므로 문짝 스토리보드는 끝 시각으로 맞춘다.
   Created By   : Claude
   Created On   : 2026-10-13
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public class DoorMarkerStatusScenarioTests
{
    // ───────────────────────── 장치 ─────────────────────────

    /// <summary>NATS(탐지 · 운영) → 큐 → 심볼 관리자 · 지도 VM(재등록 · 부팅 복원) — 호스트 배선과 같다. 지도는 레이아웃하지 않는다.</summary>
    private sealed class Rig : IDisposable
    {
        private readonly List<Func<MessageArgsModel, Task>> _handlers = new();
        private int _nextId = 7000;
        public Mock<ILogService> Log { get; } = new();
        public EventQueueManager Queue { get; } = new();
        public SymbolEventManager Symbols { get; }
        public DeviceProvider Devices { get; } = new();
        public GMapCustomControl Map { get; } = new() { Width = 640, Height = 480 };
        public MapViewModel Vm { get; }
        public List<string> Warnings { get; } = new();

        public Rig()
        {
            Log.Setup(l => l.Warning(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
               .Callback<string, string, string, int>((m, _, _, _) => Warnings.Add(m));
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
            Map.SetEditMode(false);   // 운영 모드 — 마커 HitTest=False(의도)

            // 생성자 없는 지도 VM — 재등록(InitializeDeviceSymbolIntegration) · 부팅 복원(RestoreDoorStateFromDevice)이 쓰는 필드만 꽂는다
            Vm = (MapViewModel)RuntimeHelpers.GetUninitializedObject(typeof(MapViewModel));
            typeof(MapViewModel).GetProperty(nameof(MapViewModel.MainMap))!.SetValue(Vm, Map);
            SetField(Vm, "_log", Log.Object);
            SetField(Vm, "<DeviceProvider>k__BackingField", Devices);
            SetField(Vm, "_symbolEventManager", Symbols);
            SetField(Vm, "_eventQueueManager", Queue);
        }

        public void Dispose() => Queue.Dispose();

        /// <summary>DB 에서 막 읽은 문 심볼 — 장비 목록이 아직 비어 있어 LinkedDevice 바인딩이 실패한다(실창 MarkerFactory.cs:101 경고).</summary>
        public GMapPidsMarker LoadSymbolBeforeDevices(EnumDeviceType type, int deviceId, string title)
        {
            var center = Map.Position;
            var model = new PidsSymbolModel
            {
                Id = deviceId + 100000, Title = title, DeviceType = type, LinkedDeviceId = deviceId,
                Latitude = center.Lat, Longitude = center.Lng, Width = 48, Height = 48, ShowShape = true,
            };
            var marker = (GMapPidsMarker)new MarkerFactory(Log.Object, Devices).CreateMarker(model);
            Assert.Null(marker.LinkedDevice);
            Assert.Contains(Warnings, w => w.Contains("LinkedDevice 바인딩 실패") && w.Contains($"DeviceId={deviceId}"));
            marker.IsVisible = true;
            return marker;
        }

        /// <summary>제품 <c>GMapPidsMarker.CreateMarkerControl</c> 와 같은 갈래로 컨트롤을 만들고 그 템플릿을 입힌다(Symbol3D 켬=3D 하우징 / 끔=2D 폴백).</summary>
        public GMapMarkerPidsControl Attach(GMapPidsMarker marker, bool symbol3D)
        {
            GMapMarkerPidsControl control = symbol3D && HousingModels.DeviceKey(marker.DeviceType) != null
                ? new GMapMarker3DHousingControl(marker)
                : GMapMarkerPidsFallbackControl.NeedsFallback(marker.DeviceType) ? new GMapMarkerPidsFallbackControl(marker) : new GMapMarkerPidsControl(marker);
            control.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.Theme;component/Themes/Tokens.Light.xaml", UriKind.Relative) });
            control.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri(control is GMapMarkerPidsFallbackControl or GMapMarker3DHousingControl
                    ? "/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/Housing3DMarkerStyle.xaml"
                    : "/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/PidsMarkerStyle.xaml", UriKind.Relative),
            });
            control.Style = (Style)control.Resources[control.GetType()];
            control.Width = 48; control.Height = 48;
            marker.Shape = control;
            HousingTests.Layout(control, 48, 48);
            Map.Markers.Add(marker);
            return control;
        }

        /// <summary>장비 목록이 뒤늦게 도착 → 지도 재등록(AllDevicesLoadedMessage 처리기가 부르는 것과 같은 메서드).</summary>
        public void DevicesArrive(params IBaseDeviceModel[] devices)
        {
            foreach (var d in devices) Devices.Add(d);
            var task = (Task)typeof(MapViewModel).GetMethod("InitializeDeviceSymbolIntegration", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(Vm, null)!;
            task.GetAwaiter().GetResult();
            Pump();
        }

        private string Category(int deviceId)
            => Ironwall.Dotnet.Libraries.Messages.Helpers.DeviceTypeResolver.CategoryOf(Devices.First(d => d.Id == deviceId).DeviceType).ToString().ToLowerInvariant();

        /// <summary>서버 OPERATION_EVENT(component-status PATCH 뒤) — 실창과 같은 reason 원문.</summary>
        public void Operation(int deviceId, string reason, string state)
            => Publish($$"""
               { "id": "op-{{Interlocked.Increment(ref _nextId)}}", "m_type": "PUB", "cmd": "OPERATION_EVENT", "from": "server",
                 "body": { "id": 1, "reason": "{{reason}}", "device": { "id": {{deviceId}}, "category_device": "{{Category(deviceId)}}" },
                           "detail": { "state": "{{state}}" } } }
               """);

        /// <summary>접점 DETECT(ContactOn/Off).</summary>
        public void Contact(int deviceId, string typeEvent)
        {
            var id = Interlocked.Increment(ref _nextId);
            Publish($$"""
               { "id": "env-{{id}}", "m_type": "PUB", "cmd": "DETECT", "from": "PidsProxy",
                 "body": { "id": {{id}}, "category_event": "detection", "type_event": "{{typeEvent}}", "action_reported": false,
                           "device": { "id": {{deviceId}}, "category_device": "{{Category(deviceId)}}" }, "result": "SENSOR" } }
               """);
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

    private static string Name(Control control) => UIElementAutomationPeer.CreatePeerForElement(control).GetName();

    private static HousingVisual Housing(Control control) => (HousingVisual)control.Template.FindName("PART_Housing3D", control);

    /// <summary>트리거가 시작한 문짝 스토리보드를 끝 시각으로 맞춘다(창이 없어 렌더 시계가 돌지 않는다 — EVM-108 과 같은 방식).</summary>
    private static void FinishDoor(Control control, string beginName)
    {
        var begin = control.Template.Triggers.SelectMany(t => t.EnterActions.Concat(t.ExitActions)).OfType<BeginStoryboard>().Single(b => b.Name == beginName);
        var animation = Assert.IsType<DoubleAnimation>(Assert.Single(begin.Storyboard.Children));
        Assert.True(Housing(control).HasAnimatedProperties, $"{beginName}: 문짝에 애니메이션이 걸리지 않았다");
        begin.Storyboard.SeekAlignedToLastTick(control, animation.Duration.TimeSpan, TimeSeekOrigin.BeginTime);
    }

    private static IBaseDeviceModel Device(EnumDeviceType type, int id, string? serverDoor = null)
        => type == EnumDeviceType.Gate
            ? new GateDeviceModel { Id = id, DeviceType = type, CategoryDevice = Ironwall.Dotnet.Libraries.Messages.Helpers.DeviceTypeResolver.CategoryOf(type), Status = EnumDeviceStatus.ACTIVATED, GateStatus = serverDoor! }
            : new EnclosureDeviceModel { Id = id, DeviceType = type, CategoryDevice = Ironwall.Dotnet.Libraries.Messages.Helpers.DeviceTypeResolver.CategoryOf(type), Status = EnumDeviceStatus.ACTIVATED, DoorStatus = serverDoor! };   // null = 서버 미지값(복원 없음)

    // ───────────────────────── DOOR-001 · 002 ─────────────────────────

    [Theory]
    [Trait("Scenario", "DOOR-001")]
    [Trait("Scenario", "EVM-108")]
    [InlineData(EnumDeviceType.Enclosure, "ENCLOSURE_DOOR_OPEN", "ENCLOSURE_DOOR_CLOSED", true)]
    [InlineData(EnumDeviceType.Gate, "GATE_OPEN", "GATE_CLOSED", true)]
    [InlineData(EnumDeviceType.Enclosure, "ENCLOSURE_DOOR_OPEN", "ENCLOSURE_DOOR_CLOSED", false)]
    [InlineData(EnumDeviceType.Gate, "GATE_OPEN", "GATE_CLOSED", false)]
    public void should_name_marker_door_open_then_closed_when_operation_event_arrives_after_late_device_binding_door001(
        EnumDeviceType type, string openReason, string closeReason, bool symbol3D) => HousingTests.Sta(() =>
    {
        using var rig = new Rig();
        var title = type == EnumDeviceType.Gate ? "LRT-E2EDOOR-GATE" : "LRT-E2EDOOR-ENC";
        var marker = rig.LoadSymbolBeforeDevices(type, 5151, title);
        var control = rig.Attach(marker, symbol3D);
        Assert.Equal(symbol3D, control is GMapMarker3DHousingControl);

        // 문 상태를 아직 모르는 동안 — 이름에 문 절이 '문 상태 모름'
        Assert.Contains("문 상태 모름", Name(control));

        // 장비 목록 도착 → 재바인딩 · 조회표 등록(서버 문 상태 미지 → 복원 없음)
        rig.DevicesArrive(Device(type, 5151));
        Assert.NotNull(marker.LinkedDevice);
        Assert.Equal(EnumDoorState.Unknown, marker.DoorState);

        // ── OPERATION_EVENT 열림 ──
        rig.Operation(5151, openReason, "OPEN");
        Assert.Equal(EnumDoorState.Open, marker.DoorState);
        Assert.Equal(EnumDoorState.Open, control.DoorState);
        Assert.Equal(DoorIndicatorKind.Open, control.DoorStatus);
        // 배지 규칙은 그대로: 3D 는 문짝이 위치를 그리므로 배지 없음, 2D 는 배지 표시
        Assert.Equal(symbol3D ? DoorIndicatorKind.None : DoorIndicatorKind.Open, control.DoorIndicator);
        var name = Name(control);
        Assert.StartsWith($"{title} · 이벤트 정상 · 장비 활성 · 문 열림", name);
        Assert.Equal("문 열림", control.SymbolToolTip!.Split(Environment.NewLine)[1]);
        Assert.Equal(control.SymbolToolTip, UIElementAutomationPeer.CreatePeerForElement(control).GetHelpText());
        if (symbol3D)
        {
            FinishDoor(control, "HousingDoorOpen");
            Assert.Equal(1, Housing(control).DoorOpen, 3);
            Assert.NotEqual((0d, 0d), Housing(control).DoorAngles);
        }

        // ── OPERATION_EVENT 닫힘 ──
        rig.Operation(5151, closeReason, "CLOSED");
        Assert.Equal(EnumDoorState.Closed, control.DoorState);
        Assert.Equal(DoorIndicatorKind.Closed, control.DoorStatus);
        Assert.StartsWith($"{title} · 이벤트 정상 · 장비 활성 · 문 닫힘", Name(control));
        Assert.DoesNotContain("문 열림", Name(control));
        Assert.Equal("문 닫힘", control.SymbolToolTip!.Split(Environment.NewLine)[1]);
        if (symbol3D)
        {
            FinishDoor(control, "HousingDoorClose");
            Assert.Equal(0, Housing(control).DoorOpen, 3);
            Assert.Equal((0d, 0d), Housing(control).DoorAngles);
        }
        Assert.Equal(EnumEventStatus.Normal, marker.EventStatus);   // 형태 축만 — 색 축 그대로
        Assert.Equal(0, rig.Queue.GetTotalQueueCount());
    });

    [Fact]
    [Trait("Scenario", "DOOR-002")]
    public void should_keep_both_gate_and_enclosure_markers_in_step_when_late_bound_and_driven_by_nats_door002() => HousingTests.Sta(() =>
    {
        // 실창 시드 그대로: 통문 5150 · 함체 5151 이 한 지도에 함께 있다(3D).
        using var rig = new Rig();
        var gate = rig.LoadSymbolBeforeDevices(EnumDeviceType.Gate, 5150, "LRT-E2EDOOR-GATE");
        var enclosure = rig.LoadSymbolBeforeDevices(EnumDeviceType.Enclosure, 5151, "LRT-E2EDOOR-ENC");
        var gateControl = rig.Attach(gate, symbol3D: true);
        var encControl = rig.Attach(enclosure, symbol3D: true);
        rig.DevicesArrive(Device(EnumDeviceType.Gate, 5150), Device(EnumDeviceType.Enclosure, 5151));

        rig.Operation(5151, "ENCLOSURE_DOOR_OPEN", "OPEN");
        Assert.Contains("문 열림", Name(encControl));
        Assert.DoesNotContain("문 열림", Name(gateControl));   // 다른 장비는 그대로

        rig.Operation(5150, "GATE_OPEN", "OPEN");
        Assert.Contains("문 열림", Name(gateControl));

        rig.Operation(5151, "ENCLOSURE_DOOR_CLOSED", "CLOSED");
        Assert.Contains("문 닫힘", Name(encControl));
        Assert.Contains("문 열림", Name(gateControl));

        rig.Operation(5150, "GATE_CLOSED", "CLOSED");
        Assert.Contains("문 닫힘", Name(gateControl));
    });

    // ───────────────────────── DOOR-003 ─────────────────────────

    [Theory]
    [Trait("Scenario", "DOOR-003")]
    [InlineData(EnumDeviceType.Enclosure, true)]
    [InlineData(EnumDeviceType.Gate, true)]
    [InlineData(EnumDeviceType.Enclosure, false)]
    [InlineData(EnumDeviceType.Gate, false)]
    public void should_name_restored_door_state_when_devices_arrive_late_with_server_door_status_door003(EnumDeviceType type, bool symbol3D) => HousingTests.Sta(() =>
    {
        using var rig = new Rig();
        var marker = rig.LoadSymbolBeforeDevices(type, 5160, "부팅 복원 문");
        var control = rig.Attach(marker, symbol3D);

        rig.DevicesArrive(Device(type, 5160, serverDoor: "OPEN"));   // 장비 정보 door_status/gate_status = OPEN

        Assert.Equal(EnumDoorState.Open, control.DoorState);
        Assert.Contains("문 열림", Name(control));
        if (symbol3D)
        {
            FinishDoor(control, "HousingDoorOpen");
            Assert.Equal(1, Housing(control).DoorOpen, 3);
        }
    });

    // ───────────────────────── EVM-047 ─────────────────────────

    [Fact]
    [Trait("Scenario", "EVM-047")]
    public void should_name_3d_enclosure_door_from_contact_events_evm047() => HousingTests.Sta(() =>
    {
        using var rig = new Rig();
        var marker = rig.LoadSymbolBeforeDevices(EnumDeviceType.Enclosure, 5170, "접점 함체");
        var control = rig.Attach(marker, symbol3D: true);
        rig.DevicesArrive(Device(EnumDeviceType.Enclosure, 5170));

        rig.Contact(5170, "ContactOn");
        Assert.Contains("문 열림", Name(control));
        Assert.Equal(0, rig.Queue.GetTotalQueueCount());   // 접점은 카드 · 큐 없이 형태 축만

        rig.Contact(5170, "ContactOff");
        Assert.Contains("문 닫힘", Name(control));
    });

    // ───────────────────────── DOOR-004 ─────────────────────────

    [Fact]
    [Trait("Scenario", "DOOR-004")]
    public void should_pick_3d_door_symbol_over_enclosing_zone_when_right_clicked_in_operation_mode_door004() => HousingTests.Sta(() =>
    {
        using var rig = new Rig();
        var enclosure = rig.LoadSymbolBeforeDevices(EnumDeviceType.Enclosure, 5151, "LRT-E2EDOOR-ENC");
        var encControl = rig.Attach(enclosure, symbol3D: true);
        rig.DevicesArrive(Device(EnumDeviceType.Enclosure, 5151));

        // 함체를 감싸는 구역선 — 화면 bbox 300×300, ZIndex 가 함체보다 높다(실창 조건)
        var center = rig.Map.Position;
        var zone = new GMapPidsGroupMarker(Mock.Of<ILogService>(), new PidsGroupSymbolModel { Id = 9001, Title = "구역-02", Latitude = center.Lat, Longitude = center.Lng });
        var zoneShape = new Border { Width = 300, Height = 300 };
        HousingTests.Layout(zoneShape, 300, 300);
        zone.Shape = zoneShape;
        zone.IsVisible = true;
        rig.Map.Markers.Add(zone);
        Panel.SetZIndex(zoneShape, 500);
        Panel.SetZIndex(encControl, 10);

        // 운영 모드: 마커는 WPF 히트를 받지 않는다(의도 — 팬/줌 통과). 우클릭은 GMapCustomControl.OnMouseRightButtonDown 의 수동 스캔이 받는다.
        rig.Map.SetEditMode(true);
        rig.Map.SetEditMode(false);
        Assert.False(rig.Map.IsEditMode);
        Assert.False(encControl.IsHitTestVisible);
        Assert.False(zoneShape.IsHitTestVisible);

        var scan = typeof(GMapCustomControl).GetMethod("GetMarkerAtScreen", BindingFlags.NonPublic | BindingFlags.Instance)!;
        IEditableMarker? At(double dx, double dy)
        {
            var p = rig.Map.FromLatLngToLocal(center);
            return (IEditableMarker?)scan.Invoke(rig.Map, new object[] { new Point(p.X + dx, p.Y + dy) });
        }

        Assert.Same(enclosure, At(0, 0));      // 함체 위 — 구역보다 ZIndex 가 낮아도 점 심볼이 이긴다
        Assert.Same(enclosure, At(20, -20));   // 함체 상자(48×48) 안
        Assert.Same(zone, At(100, 100));       // 함체 밖 · 구역 안 — 구역은 종전대로 잡힌다
        Assert.Null(At(200, 200));             // 둘 다 밖
    });
}
