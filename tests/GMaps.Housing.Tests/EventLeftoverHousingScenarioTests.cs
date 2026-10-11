using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.Managers;
using Ironwall.Dotnet.Libraries.Events.Ui.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Components;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Symbols3D;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;
using Ironwall.Dotnet.Monitoring.Models.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using Xunit;
using DeviceProvider = Ironwall.Dotnet.Libraries.Devices.Providers.DeviceProvider;

namespace GMaps.Housing.Tests;

/****************************************************************************
   Purpose      : 카탈로그 §2 EVM 의 남은 지도 그림 항목을 창 없이(2026-10-11)
                  - EVM-048 · 108: 접점 · OPERATION_EVENT → 실제 수신 서비스 → 심볼 관리자 → 마커 → 3D 하우징 템플릿의 문짝 스토리보드(0.4 s)
                    → HousingVisual.DoorOpen · 문 관절 각도. 창이 없어 렌더 시계가 돌지 않으므로 스토리보드를 끝 시각으로 맞춰(SeekAlignedToLastTick)
                    문짝의 마지막 자세를 읽는다 — 중간 프레임 픽셀은 헤디드 몫.
                  - EVM-112 · 113: SYNC_DEVICE 재조회로 바뀐 장비 축(부품 상태) → 마커 부품 요약 → 배지 · 이름표 · 자동화 이름.
                  부모 없는 비주얼을 Measure/Arrange + RenderTargetBitmap 으로만 본다(Window · HwndSource 없음).
   Created By   : Claude
   Created On   : 2026-10-11
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public class EventLeftoverHousingScenarioTests
{
    /// <summary>NATS(탐지 · 운영) → 큐 → 심볼 관리자. 호스트 배선(EventUiModule)과 같다.</summary>
    private sealed class DoorPipeline : IDisposable
    {
        private readonly List<Func<MessageArgsModel, Task>> _handlers = new();
        private int _nextId = 6000;
        public EventQueueManager Queue { get; } = new();
        public SymbolEventManager Symbols { get; }
        public DeviceProvider Devices { get; } = new();

        public DoorPipeline()
        {
            var log = Mock.Of<ILogService>();
            var setup = new Mock<IEventSetupModel>();
            setup.SetupAllProperties();
            setup.Object.TimeDiscardSec = 3600;
            Symbols = new SymbolEventManager(Mock.Of<IEventAggregator>(), log, new EventSetupModel(setup.Object), Queue);
            Queue.OnDeviceStateChanged += Symbols.HandleDeviceStateChanged;
            Queue.OnGroupStateChanged += Symbols.HandleGroupStateChanged;
            var nats = new Mock<INatsService>();
            nats.SetupAdd(n => n.NatsSubscribeEventAsync += It.IsAny<Func<MessageArgsModel, Task>>())
                .Callback<Func<MessageArgsModel, Task>>(h => _handlers.Add(h));
            new DetectionNatsSyncService(log, nats.Object, Symbols, Queue, setup.Object, null, null, Devices).StartService();
            new OperationEventNatsSyncService(log, nats.Object, Symbols).StartService();
        }

        public void Dispose() => Queue.Dispose();

        public (GMapPidsMarker Marker, GMapMarker3DHousingControl Control) Place3D(EnumDeviceType type, int id)
        {
            var device = new BaseDeviceModel { Id = id, DeviceType = type, CategoryDevice = Ironwall.Dotnet.Libraries.Messages.Helpers.DeviceTypeResolver.CategoryOf(type), Status = EnumDeviceStatus.ACTIVATED };
            Devices.Add(device);
            var model = new PidsSymbolModel { Title = $"{type} {id}", DeviceType = type };
            Symbols.RegisterDeviceSymbol(device, model);
            var marker = new GMapPidsMarker(Mock.Of<ILogService>(), model) { Width = 64, Height = 64, IsVisible = true };
            var control = new GMapMarker3DHousingControl(marker);
            control.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.Theme;component/Themes/Tokens.Light.xaml", UriKind.Relative) });
            control.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/Housing3DMarkerStyle.xaml", UriKind.Relative) });
            control.Style = (Style)control.Resources[typeof(GMapMarker3DHousingControl)];
            marker.Shape = control;
            HousingTests.Layout(control, 64, 64);
            return (marker, control);
        }

        private string Category(int deviceId)
            => Ironwall.Dotnet.Libraries.Messages.Helpers.DeviceTypeResolver.CategoryOf(Devices.First(d => d.Id == deviceId).DeviceType).ToString().ToLowerInvariant();

        /// <summary>서버 OPERATION_EVENT(component-status PATCH 뒤) — 함체 · 통문 개폐.</summary>
        public void Operation(int deviceId, string reason)
            => Publish($$"""
               { "id": "op-{{Interlocked.Increment(ref _nextId)}}", "m_type": "PUB", "cmd": "OPERATION_EVENT", "from": "server",
                 "body": { "id": 1, "reason": "{{reason}}", "device": { "id": {{deviceId}}, "category_device": "{{Category(deviceId)}}" }, "detail": null } }
               """);

        /// <summary>가짜 서버에 탐지 기록(번호 발급) → 같은 번호의 DETECT(접점 ContactOn/Off).</summary>
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

    private static void Pump()
    {
        for (int i = 0; i < 3; i++) Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }

    private static BeginStoryboard Door(Control control, string name)
        => control.Template.Triggers
               .SelectMany(t => t.EnterActions.Concat(t.ExitActions))
               .OfType<BeginStoryboard>()
               .FirstOrDefault(b => b.Name == name)
           ?? throw new InvalidOperationException($"{name} 없음");

    /// <summary>
    /// 트리거가 시작한 문짝 스토리보드를 끝 시각(0.4 s)으로 맞춘다. 창이 없으면 렌더 시계가 돌지 않아 새 시계가 첫 틱 전(Stopped)에 머문다 —
    /// SeekAlignedToLastTick 은 그 자리에서 값을 들인다. 이 컨트롤에서 시작되지 않은 스토리보드는 맞춰지지 않으므로(값 그대로)
    /// 뒤따르는 DoorOpen 단언이 "트리거가 그 스토리보드를 시작했는가" 까지 함께 본다.
    /// </summary>
    private static void FinishDoor(Control control, BeginStoryboard begin, double expectedTo)
    {
        var animation = Assert.IsType<DoubleAnimation>(Assert.Single(begin.Storyboard.Children));
        Assert.Equal(TimeSpan.FromSeconds(0.4), animation.Duration.TimeSpan);   // 0.4 초 문짝
        Assert.Equal(expectedTo, animation.To);
        Assert.True(Housing(control).HasAnimatedProperties, $"{begin.Name}: 문짝에 애니메이션이 걸리지 않았다");
        begin.Storyboard.SeekAlignedToLastTick(control, animation.Duration.TimeSpan, TimeSeekOrigin.BeginTime);
    }

    private static HousingVisual Housing(Control control) => (HousingVisual)control.Template.FindName("PART_Housing3D", control);

    // ───────────────────────── G. OPERATION_EVENT → 3D 문짝 ─────────────────────────

    [Theory]
    [Trait("Scenario", "EVM-108")]
    [InlineData(EnumDeviceType.Gate, "GATE_OPEN", "GATE_CLOSED", -80d, 80d)]          // 통문 두 짝이 마주 열린다
    [InlineData(EnumDeviceType.Enclosure, "DOOR_OPEN", "DOOR_CLOSED", -75d, 75d)]     // 함체 한 짝(왼쪽 관절) — 오른쪽 관절 각은 거울값이지만 그 관절에 달린 부품이 없다
    public void should_swing_3d_housing_door_open_in_04s_and_back_when_operation_event_opens_and_closes_evm108(
        EnumDeviceType type, string open, string close, double left, double right) => HousingTests.Sta(() =>
    {
        using var p = new DoorPipeline();
        var (marker, control) = p.Place3D(type, 811);
        var housing = Housing(control);
        Assert.Equal(0, housing.DoorOpen);

        p.Operation(811, open);

        Assert.Equal(EnumDoorState.Open, marker.DoorState);
        Assert.Equal(EnumDoorState.Open, control.DoorState);
        Assert.Equal(0, housing.DoorOpen);                          // 시계가 돌기 전 — 순간 이동이 아니다
        FinishDoor(control, Door(control, "HousingDoorOpen"), expectedTo: 1);
        Assert.Equal(1, housing.DoorOpen, 3);
        Assert.Equal((left, right), housing.DoorAngles);
        Assert.Equal(EnumEventStatus.Normal, marker.EventStatus);   // 색 축은 그대로(형태 축만)

        p.Operation(811, close);

        Assert.Equal(EnumDoorState.Closed, control.DoorState);
        FinishDoor(control, Door(control, "HousingDoorClose"), expectedTo: 0);
        Assert.Equal(0, housing.DoorOpen, 3);
        Assert.Equal((0d, 0d), housing.DoorAngles);
    });

    [Fact]
    [Trait("Scenario", "EVM-048")]
    public void should_swing_3d_gate_leaves_on_contact_without_touching_the_status_color_evm048() => HousingTests.Sta(() =>
    {
        using var p = new DoorPipeline();
        var (marker, control) = p.Place3D(EnumDeviceType.Gate, 812);
        var housing = Housing(control);
        var statusBefore = housing.StatusBrush;

        p.Contact(812, "ContactOn");

        Assert.Equal(EnumDoorState.Open, control.DoorState);
        Assert.Equal(0, p.Queue.GetTotalQueueCount());              // 접점은 카드 · 큐 없이 형태 축만
        FinishDoor(control, Door(control, "HousingDoorOpen"), expectedTo: 1);
        Assert.Equal((-80d, 80d), housing.DoorAngles);
        Assert.Equal(EnumEventStatus.Normal, marker.EventStatus);   // 색 축 그대로
        Assert.Equal(statusBefore, housing.StatusBrush);

        p.Contact(812, "ContactOff");

        Assert.Equal(EnumDoorState.Closed, control.DoorState);
        FinishDoor(control, Door(control, "HousingDoorClose"), expectedTo: 0);
        Assert.Equal((0d, 0d), housing.DoorAngles);
    });

    // ───────────────────────── G. 부품 상태 → 배지 · 이름표 ─────────────────────────

    private static DeviceAxesModel PtzAxes(string health, string? reason)
        => ComponentHealthSummaryTests.Axes(true,
            ("ptz", "PTZ_UNIT", null, null, "IDLE", health, reason, true),
            ("wiper", "WIPER", null, null, "OFF", "OK", null, true));

    private static (RenderTargetBitmap Bitmap, ComponentStatusOverlay Overlay) Render(Canvas host, GMapMarkerPidsControl control)
    {
        HousingTests.Layout(host, 160, 160);
        var overlay = FindOverlay(control);
        var bitmap = new RenderTargetBitmap(160, 160, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(host);
        return (bitmap, overlay);
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

    [Fact]
    [Trait("Scenario", "EVM-112,EVM-113")]
    public void should_show_fault_then_degraded_badge_and_plate_and_clear_them_when_ptz_component_status_changes_evm112_113() => HousingTests.Sta(() =>
    {
        var host = new Canvas { Width = 160, Height = 160, Background = Brushes.Transparent };
        host.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.Theme;component/Themes/Tokens.Light.xaml", UriKind.Relative) });
        using var marker = new GMapPidsMarker(Mock.Of<ILogService>(), new PidsSymbolModel { Title = "PTZ 카메라", DeviceType = EnumDeviceType.IpCamera });
        marker.Width = 40; marker.Height = 40; marker.IsVisible = true;
        var device = new BaseDeviceModel { Id = 7, DeviceType = EnumDeviceType.IpCamera, Status = EnumDeviceStatus.ACTIVATED, Axes = PtzAxes("FAULT", "OVER_CURRENT") };
        marker.LinkedDevice = device;                                   // 부팅 재바인딩 — component-status PATCH(고장) 뒤 상태
        var control = new GMapMarkerPidsControl(marker);
        control.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/PidsMarkerStyle.xaml", UriKind.Relative) });
        control.Style = (Style)control.Resources[control.GetType()];
        control.Width = 40; control.Height = 40;
        control.MarkerScreenScale = 1.5;                                // 60px — 이름표가 그려지는 크기
        Canvas.SetLeft(control, 20); Canvas.SetTop(control, 20);
        host.Children.Add(control);

        // EVM-112 — PTZ 고장: 「부품 고장 1」 · 배지 · 이름표
        var (_, overlay) = Render(host, control);
        Assert.Equal(1, marker.ComponentSummary.FaultCount);
        Assert.True(overlay.IsBadgeDrawn, "고장 배지 없음");
        Assert.True(overlay.IsStripDrawn, "고장 이름표 없음");
        Assert.EndsWith("고장", overlay.DrawnPlateText);
        Assert.Contains("부품 고장 1", UIElementAutomationPeer.CreatePeerForElement(control).GetName());

        // EVM-113 — 서버가 저하로 바꿈(SYNC_DEVICE 재조회 = 축 참조 교체 → SyncFromDevice → SetUpdate)
        device.Axes = PtzAxes("DEGRADED", null);
        marker.Model.SetUpdate();
        Pump();
        (_, overlay) = Render(host, control);
        Assert.Equal(ComponentHealthLevel.Degraded, marker.ComponentSummary.Health);
        Assert.Equal(1, marker.ComponentSummary.DegradedCount);
        Assert.True(overlay.IsStripDrawn, "저하 이름표 없음");
        Assert.EndsWith("저하", overlay.DrawnPlateText);
        Assert.Contains("저하 1", UIElementAutomationPeer.CreatePeerForElement(control).GetName());

        // 정상으로 돌아오면 이름표 · 배지가 사라진다
        device.Axes = PtzAxes("OK", null);
        marker.Model.SetUpdate();
        Pump();
        (_, overlay) = Render(host, control);
        Assert.Equal(ComponentHealthLevel.Ok, marker.ComponentSummary.Health);
        Assert.False(overlay.IsBadgeDrawn, "정상인데 배지가 남았다");
        Assert.False(overlay.IsStripDrawn, "정상인데 이름표가 남았다");
    });
}
