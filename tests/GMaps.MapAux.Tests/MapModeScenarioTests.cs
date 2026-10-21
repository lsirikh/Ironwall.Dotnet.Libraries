using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Linq;
using Caliburn.Micro;
using GMap.NET;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Adorners;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Args;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using MaterialDesignThemes.Wpf;
using Moq;
using ScenarioShared;
using Xunit;

namespace GMaps.MapAux.Tests;

/// <summary>키 이벤트용 가짜 PresentationSource(창 없음).</summary>
internal sealed class FakeSource : PresentationSource
{
    private Visual? _root;
    public override Visual? RootVisual { get => _root; set => _root = value; }
    public override bool IsDisposed => false;
    protected override CompositionTarget? GetCompositionTargetCore() => null;
    internal static KeyEventArgs Key(Key key) => new(Keyboard.PrimaryDevice, new FakeSource(), 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
}

/// <summary>EventAggregator 로 나간 메시지를 모두 모은다.</summary>
public sealed class Sink : IHandle<object>
{
    public readonly List<object> Messages = new();
    public Task HandleAsync(object message, CancellationToken cancellationToken) { Messages.Add(message); return Task.CompletedTask; }
}

/// <summary>
/// 카탈로그 §1 MAP-158/159 · 170~174 · 189 · 192 · §3.5 AUX-019/020/022 — 생성자 없는 MapViewModel 에 지도 컨트롤(부모=AdornerDecorator, 창 없음)을 꽂아
/// 측정 · 조준 · Esc · 방송/TTS 패널 판정 로직을 본다. 메뉴는 열지 않는다(아래 MenuProbe 참고).
/// </summary>
public class MapModeScenarioTests
{
    private static (MapViewModel vm, GMapCustomControl map, Sink sink) Vm()
    {
        var vm = Scenario.Uninit<MapViewModel>();
        var map = new GMapCustomControl { Width = 640, Height = 480, MinZoom = 1, MaxZoom = 21, Zoom = 17, Position = new PointLatLng(37.5, 127.0) };
        var deco = new AdornerDecorator { Child = map };
        Scenario.Layout(deco, 640, 480);
        if (Scenario.GetField<object?>(map, "_measureController") == null) Scenario.Call(map, "InitializeMeasure");
        Scenario.SetField(vm, "<MainMap>k__BackingField", map);
        var ea = new EventAggregator(); var sink = new Sink(); ea.SubscribeOnPublishedThread(sink);
        Scenario.SetField(vm, "_eventAggregator", ea);
        Scenario.Call(vm, "InitializeMeasureCommands");
        Scenario.Call(vm, "AttachMeasureEvents");
        return (vm, map, sink);
    }

    private static MeasureController Measure(GMapCustomControl map) => Scenario.GetField<MeasureController>(map, "_measureController");
    private static void Press(MapViewModel vm, string handler, Key key) => Scenario.Call(vm, handler, vm, FakeSource.Key(key));

    // ───────────────────────── 측정 ─────────────────────────

    [Fact]
    [Trait("Scenario", "MAP-170")]
    public void should_turn_on_read_distance_and_turn_off_with_escape_when_length_measure_used() => OneSta.Sta(() =>
    {
        var (vm, map, _) = Vm();
        vm.MeasureLengthCommand!.Execute(null);
        Assert.True(vm.IsMeasureLengthActive); Assert.False(vm.IsMeasureAreaActive); Assert.True(map.IsMeasuring);
        Assert.True(vm.IsMeasureReadoutVisible);
        Assert.Equal("지도를 클릭해 시작", vm.MeasureStep);

        Measure(map).AddPoint(new PointLatLng(37.5, 127.0));
        Measure(map).AddPoint(new PointLatLng(37.5, 127.01));   // 동서 0.01° ≈ 885 m
        Assert.Equal("거리", vm.MeasurePrimaryLabel);
        var expected = MeasureFormat.DistanceParts(MeasureMath.TotalLengthMeters(new[] { new PointLatLng(37.5, 127.0), new PointLatLng(37.5, 127.01) }));
        Assert.Equal(expected.Item1, vm.MeasurePrimaryValue); Assert.Equal(expected.Item2, vm.MeasurePrimaryUnit);
        Assert.False(vm.MeasureHasSecondary);

        Press(vm, "OnMeasureKeyDown", Key.Escape);
        Assert.False(map.IsMeasuring); Assert.False(vm.IsMeasureLengthActive); Assert.False(vm.IsMeasureReadoutVisible);
    });

    [Fact]
    [Trait("Scenario", "MAP-171")]
    public void should_show_area_and_perimeter_values_when_three_points_are_placed_in_area_measure() => OneSta.Sta(() =>
    {
        var (vm, map, _) = Vm();
        vm.MeasureAreaCommand!.Execute(null);
        Assert.True(vm.IsMeasureAreaActive);
        var pts = new[] { new PointLatLng(37.5, 127.0), new PointLatLng(37.5, 127.001), new PointLatLng(37.501, 127.001) };
        Measure(map).AddPoint(pts[0]); Measure(map).AddPoint(pts[1]);
        Assert.Equal("최소 3점을 찍으세요", vm.MeasureStep);
        Measure(map).AddPoint(pts[2]);

        var area = MeasureFormat.AreaParts(MeasureMath.AreaSquareMeters(pts));
        var perim = MeasureFormat.DistanceParts(MeasureMath.PerimeterMeters(pts));
        Assert.Equal("면적", vm.MeasurePrimaryLabel);
        Assert.Equal(area.Item1, vm.MeasurePrimaryValue); Assert.Equal(area.Item2, vm.MeasurePrimaryUnit);
        Assert.True(vm.MeasureHasSecondary);
        Assert.Equal("둘레", vm.MeasureSecondaryLabel);
        Assert.Equal(perim.Item1, vm.MeasureSecondaryValue); Assert.Equal(perim.Item2, vm.MeasureSecondaryUnit);
        Assert.InRange(MeasureMath.AreaSquareMeters(pts), 4000, 5000);   // ≈ 88.5 m × 111 m / 2
    });

    [Fact]
    [Trait("Scenario", "MAP-172")]
    public void should_remove_last_point_finish_and_start_new_measure_when_backspace_enter_then_click() => OneSta.Sta(() =>
    {
        var (vm, map, _) = Vm();
        var counts = new List<int>(); map.MeasureReadoutChanged += r => counts.Add(r.PointCount);
        vm.MeasureLengthCommand!.Execute(null);
        Measure(map).AddPoint(new PointLatLng(37.5, 127.0));
        Measure(map).AddPoint(new PointLatLng(37.5, 127.001));
        Measure(map).AddPoint(new PointLatLng(37.5, 127.002));
        Assert.Equal(3, counts[^1]);

        Press(vm, "OnMeasureKeyDown", Key.Back);       // 마지막 점 제거
        Assert.Equal(2, counts[^1]);
        Press(vm, "OnMeasureKeyDown", Key.Enter);      // 끝
        Assert.Equal("완료 — 클릭하여 새 측정", vm.MeasureStep);
        Assert.True(map.IsMeasuring);                  // 끝나도 도구는 켜진 채(결과 고정)

        Measure(map).AddPoint(new PointLatLng(37.6, 127.1));   // 끝난 뒤 클릭 = 새 측정 첫 점
        Assert.Equal(1, counts[^1]);
        Assert.Equal(CompletedHintNot(vm.MeasureStep), true);
        static bool CompletedHintNot(string s) => s != "완료 — 클릭하여 새 측정";
    });

    [Fact]
    [Trait("Scenario", "MAP-174")]
    public void should_keep_only_one_measure_mode_on_and_cancel_other_map_modes_when_measure_toggled() => OneSta.Sta(() =>
    {
        var (vm, map, _) = Vm();
        vm.MeasureLengthCommand!.Execute(null);
        vm.MeasureAreaCommand!.Execute(null);          // 다른 종류 = 전환
        Assert.False(vm.IsMeasureLengthActive); Assert.True(vm.IsMeasureAreaActive);
        Assert.Equal(MeasureKind.Area, map.ActiveMeasureKind);
        vm.MeasureAreaCommand!.Execute(null);          // 같은 종류 재클릭 = 끔
        Assert.False(vm.IsMeasureAreaActive); Assert.False(map.IsMeasuring);

        // 배치 · 홈 · 조준 모드 중 측정을 켜면 그 모드는 꺼진다(측정과 배타)
        map.IsSymbolPlacementMode = true; map.IsHomePlacementMode = true;
        vm.MeasureLengthCommand!.Execute(null);
        Assert.False(map.IsSymbolPlacementMode); Assert.False(map.IsHomePlacementMode);
        Assert.True(vm.IsMeasureLengthActive);

        // 반대로 조준 모드에 들어가면 측정이 꺼진다
        Scenario.Call(vm, "EnterTargetAimMode", PtzMarker());
        Assert.True(map.IsTargetAimMode);
        Assert.False(map.IsMeasuring); Assert.False(vm.IsMeasureLengthActive);
    });

    // ───────────────────────── 조준 · Esc ─────────────────────────

    private static GMapPidsMarker PtzMarker(EnumCameraType category = EnumCameraType.PTZ)
    {
        var cam = new Mock<ICameraDeviceModel>();
        cam.SetupGet(c => c.Id).Returns(11);
        cam.SetupGet(c => c.Category).Returns(category);
        var marker = new GMapPidsMarker(Mock.Of<ILogService>(), new PidsSymbolModel { DeviceType = EnumDeviceType.IpCamera, Latitude = 37.5, Longitude = 127.0, LinkedDeviceId = 11 });
        marker.LinkedDevice = cam.Object;
        marker.DetectionRange = 100;
        return marker;
    }

    [Fact]
    [Trait("Scenario", "MAP-158")]
    public void should_show_aim_strip_then_cancel_message_without_publishing_when_escape_pressed_in_aim_mode() => OneSta.Sta(() =>
    {
        var (vm, map, _) = Vm();
        var aim = new Mock<ICameraAimControlService>(MockBehavior.Strict);
        Scenario.SetField(vm, "_cameraAimControlService", aim.Object);

        Scenario.Call(vm, "EnterTargetAimMode", PtzMarker());
        Assert.True(map.IsTargetAimMode);
        Assert.True(vm.IsAimStatusVisible);
        Assert.StartsWith("특정 위치 확인 — 탐지범위 100m", vm.AimStatusMessage);

        Press(vm, "OnMapPreviewKeyDownForAim", Key.Escape);
        Assert.False(map.IsTargetAimMode);
        Assert.Equal("취소했습니다.", vm.AimStatusMessage);

        // 반경 밖 클릭도 취소(보내지 않음)
        Scenario.Call(vm, "EnterTargetAimMode", PtzMarker());
        Scenario.Call(vm, "OnTargetAimClicked", new PointLatLng(37.6, 127.1), new Point(1, 1));
        Assert.False(map.IsTargetAimMode);
        Assert.Equal("반경 밖을 클릭하여 취소했습니다.", vm.AimStatusMessage);
        aim.VerifyNoOtherCalls();   // Strict — 발행 호출 0
    });

    [Fact]
    [Trait("Scenario", "MAP-192")]
    public void should_cancel_one_map_mode_per_escape_in_fixed_order_when_several_modes_are_on() => OneSta.Sta(() =>
    {
        var (vm, map, _) = Vm();
        map.IsTargetAimMode = true; map.IsSymbolPlacementMode = true; map.IsHomePlacementMode = true; map.IsAnchorDrawMode = true;

        var expected = new[] { "취소했습니다.", "배치를 취소했습니다.", "홈 설정을 취소했습니다.", "영역 그리기를 취소했습니다." };
        var flags = new Func<bool>[] { () => map.IsTargetAimMode, () => map.IsSymbolPlacementMode, () => map.IsHomePlacementMode, () => map.IsAnchorDrawMode };
        for (int i = 0; i < expected.Length; i++)
        {
            var e = FakeSource.Key(Key.Escape);
            Scenario.Call(vm, "OnMapPreviewKeyDownForAim", vm, e);
            Assert.True(e.Handled);
            Assert.Equal(expected[i], vm.AimStatusMessage);
            for (int j = 0; j < flags.Length; j++) Assert.Equal(j > i, flags[j]());   // 한 번에 한 모드만 꺼진다
        }
        var last = FakeSource.Key(Key.Escape);
        Scenario.Call(vm, "OnMapPreviewKeyDownForAim", vm, last);
        Assert.False(last.Handled);   // 남은 모드 없으면 Esc 를 가로채지 않는다
    });

    // ───────────────────────── 방송 · TTS ─────────────────────────

    private static void Raise(object target, string evt, EventArgs args)
    {
        var f = target.GetType().GetField(evt, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        ((Delegate?)f.GetValue(target))?.DynamicInvoke(target, args);
    }

    private static Mock<IBroadcastControlService> Broadcast(MapViewModel vm)
    {
        var svc = new Mock<IBroadcastControlService>();
        Scenario.SetField(vm, "_broadcastControlService", svc.Object);
        var perm = new Mock<IPermissionService>();
        perm.Setup(p => p.CanControl(It.IsAny<string>())).Returns(true);
        Scenario.SetField(vm, "_permissionService", perm.Object);
        Scenario.SetField(vm, "_permissionResolved", true);
        return svc;
    }

    [Fact]
    [Trait("Scenario", "MAP-159,AUX-020")]
    public void should_open_and_close_broadcast_panels_without_sending_when_cancelled() => OneSta.Sta(() =>
    {
        var (vm, _, _) = Vm();
        var svc = Broadcast(vm);

        vm.ShowBroadcastPlayPanel(5);
        Assert.True(vm.IsBroadcastPlayPanelVisible);
        Assert.Equal(5, vm.BroadcastPlayPanel!.LinkedDeviceId);
        Raise(vm.BroadcastPlayPanel!, "CancelRequested", EventArgs.Empty);
        Assert.False(vm.IsBroadcastPlayPanelVisible); Assert.Null(vm.BroadcastPlayPanel);

        vm.ShowTtsBroadcastPanel(5);
        Assert.True(vm.IsTtsBroadcastPanelVisible);
        Assert.Equal(5, vm.TtsBroadcastPanel!.LinkedDeviceId);
        Raise(vm.TtsBroadcastPanel!, "CancelRequested", EventArgs.Empty);
        Assert.False(vm.IsTtsBroadcastPanelVisible); Assert.Null(vm.TtsBroadcastPanel);

        svc.VerifyNoOtherCalls();   // 열기 · 취소만으로는 아무것도 보내지 않는다
    });

    /// <summary>결함 AUX-D2: TTS 전송 실패(예외)가 로그에만 남고 사용자 안내가 없다 — 음원 실행 실패는 표준 안내 팝업(MapViewModel.cs:9606).</summary>
    [Fact]
    [Trait("Scenario", "AUX-022")]
    [Trait("Defect", "AUX-D2")]
    public void should_notify_user_and_keep_panel_open_when_tts_send_fails() => OneSta.StaAsync(async () =>
    {
        var (vm, _, sink) = Vm();
        var svc = Broadcast(vm);
        svc.Setup(s => s.PublishTtsAsync(5, "경고", 100)).ThrowsAsync(new InvalidOperationException("NATS 끊김"));

        vm.ShowTtsBroadcastPanel(5);
        Raise(vm.TtsBroadcastPanel!, "SendRequested", new TtsSendEventArgs("경고", 5));
        await Task.Delay(50);

        Assert.True(vm.IsTtsBroadcastPanelVisible);   // 실패면 다시 보낼 수 있게 남는다
        Assert.True(sink.Messages.Any(m => m.GetType().Name == "OpenInfoPopupMessageModel"),
            "TTS 전송 실패인데 사용자 안내(OpenInfoPopupMessageModel)가 하나도 나가지 않았다");
    });

    /// <summary>결함 AUX-D3: TTS 전송 대기 중 「전송」을 다시 누르면 같은 TTS 가 두 번 나간다 — 음원 실행은 _isBroadcastRequestInFlight 로 막는다(:9592).</summary>
    [Fact]
    [Trait("Scenario", "AUX-022")]
    [Trait("Defect", "AUX-D3")]
    public void should_send_tts_once_when_send_pressed_twice_while_first_send_is_pending() => OneSta.StaAsync(async () =>
    {
        var (vm, _, _) = Vm();
        var svc = Broadcast(vm);
        var pending = new TaskCompletionSource();
        svc.Setup(s => s.PublishTtsAsync(5, "경고", 100)).Returns(pending.Task);

        vm.ShowTtsBroadcastPanel(5);
        var panel = vm.TtsBroadcastPanel!;
        Raise(panel, "SendRequested", new TtsSendEventArgs("경고", 5));
        Raise(panel, "SendRequested", new TtsSendEventArgs("경고", 5));
        pending.SetResult();
        await Task.Delay(50);

        svc.Verify(s => s.PublishTtsAsync(5, "경고", 100), Times.Once());
    });

    [Fact]
    [Trait("Scenario", "MAP-159")]
    public void should_close_play_panel_only_after_success_and_block_resend_while_waiting_when_play_sent() => OneSta.StaAsync(async () =>
    {
        var (vm, _, sink) = Vm();
        var svc = Broadcast(vm);
        var pending = new TaskCompletionSource<Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Brokers.BrokerRequestResult>();
        svc.Setup(s => s.PublishPlayAsync(5, 2, 1, 100)).Returns(pending.Task);

        vm.ShowBroadcastPlayPanel(5);
        Raise(vm.BroadcastPlayPanel!, "SendRequested", new BroadcastSendEventArgs(2, 1, 5));
        Raise(vm.BroadcastPlayPanel!, "SendRequested", new BroadcastSendEventArgs(2, 1, 5));
        Assert.True(vm.IsBroadcastPlayPanelVisible);   // 응답 전에는 닫지 않는다
        pending.SetResult(Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Brokers.BrokerRequestResult.Fail(
            Enum.GetValues<Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Brokers.EnumBrokerFailure>().Last(), "응답 없음"));
        await Task.Delay(50);
        svc.Verify(s => s.PublishPlayAsync(5, 2, 1, 100), Times.Once());
        Assert.True(vm.IsBroadcastPlayPanelVisible);   // 실패 = 패널 유지 + 안내
        Assert.Contains(sink.Messages, m => m.GetType().Name == "OpenInfoPopupMessageModel");
    });

    // ───────────────────────── 스피커 우클릭 메뉴 ─────────────────────────

    /// <summary>
    /// 결함 AUX-D4: 스피커 우클릭 메뉴 셋째 항목만 영문 "Stop"(MapViewModel.cs:6648) — 나머지는 「음원 실행」 · 「TTS 실행」.
    /// 메뉴 창(Popup)을 띄우지 않으려고 MenuProbe 가 마지막 항목 아이콘(PackIconKind.Stop)이 만들어지는 순간 예외로 끊는다
    /// (ShowMarkerContextMenu 의 try/catch 가 받아 menu.IsOpen 까지 가지 않는다).
    /// </summary>
    [Fact]
    [Trait("Scenario", "AUX-019")]
    [Trait("Defect", "AUX-D4")]
    public void should_label_speaker_menu_items_in_korean_when_speaker_symbol_right_clicked()
    {
        // 메뉴는 ShowMarkerContextMenu 안에서 지역 ContextMenu 로 만들어 곧바로 IsOpen=true(팝업 창) 하므로 창 없이 가로챌 수 없다
        // (MenuItem.Header 메타데이터 재정의는 WPF 가 이미 등록해 막힌다) → 스피커 분기의 머리글 3개를 소스에서 순서대로 읽는다.
        var src = File.ReadAllText(Path.Combine(RepoPaths.GMapsUi, "ViewModels", "Maps", "MapViewModel.cs"));
        int branch = src.IndexOf("if (pidsMarker.DeviceType == EnumDeviceType.IpSpeaker)", StringComparison.Ordinal);
        Assert.True(branch > 0);
        var headers = new List<string>();
        for (int at = branch; headers.Count < 3;)
        {
            at = src.IndexOf("Header = \"", at, StringComparison.Ordinal) + "Header = \"".Length;
            headers.Add(src[at..src.IndexOf('"', at)]);
        }
        Assert.Equal("음원 실행", headers[0]);
        Assert.Equal("TTS 실행", headers[1]);
        Assert.True(headers[2].Any(c => c >= '가' && c <= '힣'), $"스피커 메뉴 셋째 항목이 한국어가 아니다: \"{headers[2]}\"");
    }

    // ───────────────────────── 겹창 Z 순서 ─────────────────────────

    [Fact]
    [Trait("Scenario", "MAP-189,MAP-167")]
    public void should_stack_overlay_windows_in_documented_z_order_when_map_view_is_built()
    {
        var doc = XDocument.Load(Path.Combine(RepoPaths.GMapsUi, "Views", "Maps", "MapView.xaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var z = doc.Descendants()
            .Where(e => e.Attribute(x + "Name") != null && e.Attribute("Panel.ZIndex") != null)
            .ToDictionary(e => e.Attribute(x + "Name")!.Value, e => int.Parse(e.Attribute("Panel.ZIndex")!.Value));
        var property = doc.Descendants().Single(e => e.Name.LocalName == "ContentPresenter" && (string?)e.Attribute("Canvas.Left") == "300" && (string?)e.Attribute("Canvas.Top") == "100");
        int propertyZ = int.Parse(property.Attribute("Panel.ZIndex")!.Value);

        Assert.Equal(20, propertyZ);
        Assert.Equal(100, z["LayerPanelPresenter"]);
        foreach (var name in new[] { "SymbolPalettePresenter", "MapAnchorPresenter", "TrackingSettingsPresenter", "SensorInfoPresenter" })
            Assert.Equal(120, z[name]);
        Assert.Equal(130, z["SymbolDetailPresenter"]);
        Assert.Equal(210, z["CameraPopupHub"]);
        Assert.True(z["SymbolDetailPresenter"] > propertyZ);   // MAP-167 상세 > 속성
        Assert.True(z["SymbolPalettePresenter"] > z["LayerPanelPresenter"]);
        var toasts = doc.Descendants().Select(e => (string?)e.Attribute("Panel.ZIndex")).Where(v => v == "300").Count();
        Assert.True(toasts > 0);
        Assert.Equal(300, doc.Descendants().Select(e => (string?)e.Attribute("Panel.ZIndex")).Where(v => v != null).Max(v => int.Parse(v!)));
    }
}

/// <summary>
/// 우클릭 메뉴를 창 없이 읽는다: MenuItem.Header 설정을 모두 기록하고, 지정한 아이콘(PackIcon.Kind)이 만들어지는 순간 예외를 던져
/// ShowMarkerContextMenu 를 menu.IsOpen 전에 끊는다. 먼저 같은 경로로 예외가 실제로 나는지 자가 확인한 뒤에만 대상 메서드를 부른다.
/// </summary>
internal static class MenuProbe
{
    [ThreadStatic] private static List<string>? _headers;
    [ThreadStatic] private static string? _abortAfter;
    [ThreadStatic] private static bool _armed;
    private sealed class Abort : Exception { }

    static MenuProbe()
    {
        HeaderedItemsControl.HeaderProperty.OverrideMetadata(typeof(MenuItem), new FrameworkPropertyMetadata(null, (_, e) =>
        {
            if (_headers == null) return;
            var h = e.NewValue?.ToString() ?? "";
            _headers.Add(h);
            if (_armed) throw new Abort();          // 기준 항목 다음 항목의 머리글까지 읽고 끊는다
            if (h == _abortAfter) _armed = true;
        }));
    }

    /// <summary><paramref name="abortAfter"/> 머리글 바로 다음 MenuItem 머리글이 설정되는 순간 끊는다.</summary>
    internal static List<string> Capture(string abortAfter, System.Action showMenu)
    {
        _headers = new List<string>(); _abortAfter = abortAfter; _armed = false;
        try
        {
            // 자가 확인 — 같은 경로로 실제로 끊기는지 본 뒤에만 메뉴 코드를 부른다(끊기지 않으면 여기서 실패, 메뉴는 열지 않음)
            _ = new MenuItem { Header = abortAfter };
            Assert.Throws<Abort>(() => new MenuItem { Header = "probe" });
            _headers.Clear(); _armed = false;
            showMenu();
            Assert.True(_armed, "메뉴가 기준 항목 뒤에서 끊기지 않았다");
            return _headers;
        }
        finally { _abortAfter = null; _headers = null; _armed = false; }
    }
}
