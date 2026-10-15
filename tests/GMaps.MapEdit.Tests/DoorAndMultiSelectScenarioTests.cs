using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using GMap.NET;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Adorners;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Factories;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapProperties;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Door;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using Newtonsoft.Json.Linq;
using ScenarioShared;
using Xunit;

namespace GMaps.MapEdit.Tests;

/// <summary>
/// 카탈로그 §1 MAP-132(속성 · 문 — 명령 · 상태 따라감) · MAP-134(다중 선택 — 그룹 어도너 · Ctrl+클릭 토글).
/// MAP-132: 문 개폐는 클라 → 매니저 NATS 직행(GATE_DOOR_SET, subject {domain}.{부대}.all.gate-door). 실제 VM 배선(WireDoorControl → SendDoorCommandAsync)
///   · 실제 DoorControlService(봉투 · subject 조립) 를 쓰고 NATS 발행기만 가짜(Moq INatsService)로 두어 <b>발행 내용</b>을 단언한다(현장 장비에 닿지 않음).
///   상태 전이는 제품 이벤트 경로(DeviceSymbolLookupModel.ApplyDoorState → 모델 SetUpdate → 마커 DoorState 통지 → 속성창)로 낸다.
///   속성창은 VM 이 실제 팩토리로 만든 것에 실제 스타일(PidsPropertyStyle.xaml)을 입혀 GMaps.Property.DoorOpen/DoorClose/DoorState 를 본다.
/// MAP-134: 지도가 내는 Ctrl+클릭 통지(MarkerToggleRequested) · 러버밴드 통지(RubberBandStarted/MarkersRubberBandSelected)를 제품 배선으로 받아
///   GroupSelectionService 가 지도 AdornerLayer 에 붙이는 GroupSelectionAdorner 를 본다. Ctrl/Shift 키 판정(Keyboard.Modifiers) 자체는 실키보드 — 헤디드 몫.
/// </summary>
public class DoorAndMultiSelectScenarioTests
{
    // ───────────────────────── 공용 ─────────────────────────

    private static T ById<T>(DependencyObject root, string automationId) where T : DependencyObject
        => PropertyPanelScenarioTests.Tree(root).OfType<T>().Single(d => AutomationProperties.GetAutomationId(d) == automationId);

    private sealed class Door
    {
        public WiredMapRig Rig = null!;
        public GMapPidsMarker Marker = null!;
        public GateDeviceModel Device = null!;
        public List<(string subject, string json)> Published = new();
        public bool NatsOk = true;
    }

    /// <summary>통문 장비(id 77) · 연결된 통문 심볼 · 실제 DoorControlService(가짜 NATS 발행기)를 꽂은 장치.</summary>
    private static Door GateRig(EnumDoorState initial, bool canControl = true)
    {
        var d = new Door { Rig = WiredMapRig.Create(canControl: canControl) };
        d.Device = new GateDeviceModel { Id = 77, DeviceName = "북문 통문", DeviceNumber = 3, DeviceType = EnumDeviceType.Gate, Status = EnumDeviceStatus.ACTIVATED };
        d.Rig.Devices.CollectionEntity.Add(d.Device);

        var nats = new Mock<INatsService>();
        nats.Setup(n => n.PublishAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((s, j) => d.Published.Add((s, j)))
            .Returns(() => d.NatsOk ? Task.CompletedTask : Task.FromException(new InvalidOperationException("브로커 끊김")));
        var setup = new Mock<INatsSetupModel>();
        setup.Setup(s => s.DomainNats).Returns("sensorway");
        setup.Setup(s => s.GroupNats).Returns("unit001");
        Scenario.SetField(d.Rig.Vm, "_doorControlService", new DoorControlService(nats.Object, setup.Object, Mock.Of<ILogService>()));
        Scenario.SetField(d.Rig.Vm, "_doorControlResolved", true);

        var center = d.Rig.Map.FromLatLngToLocal(d.Rig.Map.Position);
        var geo = d.Rig.Map.FromLocalToLatLng((int)center.X, (int)center.Y);
        var model = new PidsSymbolModel
        {
            Id = 1321, Title = "북문 통문", DeviceType = EnumDeviceType.Gate, LinkedDeviceId = 77, DoorState = initial,
            Latitude = geo.Lat, Longitude = geo.Lng, Width = 50, Height = 50, ShowShape = true,
        };
        d.Marker = (GMapPidsMarker)new MarkerFactory(Mock.Of<ILogService>(), d.Rig.Devices).CreateMarker(model);
        d.Rig.Map.Markers.Add(d.Marker);
        Scenario.Layout(d.Rig.Deco, 1200, 800);
        return d;
    }

    /// <summary>편집 모드 · 통문 심볼 클릭 → 속성창(제품 배선) → 실제 스타일로 올림.</summary>
    private static async Task<(GMapPropertyPidsControl panel, PropertyPanelScenarioTests.Host host)> OpenGatePanel(Door d)
    {
        d.Rig.AssertWired("OnMarkerClicked", "OnMapMarkerClicked");
        d.Rig.Raise("OnMarkerClicked", d.Marker);
        await Task.Delay(40);
        var panel = Assert.IsType<GMapPropertyPidsControl>(d.Rig.Vm.PropertyPanel);
        var host = PropertyPanelScenarioTests.Mount(panel, "PidsPropertyStyle.xaml");
        return (panel, host);
    }

    /// <summary>제품 이벤트 경로 — OPERATION_EVENT/접점 이벤트가 조회표 모델에 문 상태를 쓰는 것(DeviceSymbolLookupModel.ApplyDoorState).</summary>
    private static void ReportDoor(Door d, EnumDoorState state)
    {
        var lookup = new DeviceSymbolLookupModel(Mock.Of<ILogService>()) { SymbolModel = (IPidsEventCapable)d.Marker.Model };
        lookup.ApplyDoorState(state);
        Scenario.Pump();
    }

    private static JObject Body(JObject envelope) => (JObject)envelope["body"]!;

    /// <summary>발행 원문 그대로 읽기 — 날짜 문자열을 DateTime 으로 바꾸지 않는다(오프셋 표기 확인용).</summary>
    private static JObject Parse(string json)
    {
        using var reader = new Newtonsoft.Json.JsonTextReader(new System.IO.StringReader(json)) { DateParseHandling = Newtonsoft.Json.DateParseHandling.None };
        return JObject.Load(reader);
    }

    // ───────────────────────── MAP-132 ─────────────────────────

    [Fact]
    [Trait("Scenario", "MAP-132")]
    public void should_publish_gate_door_set_to_nats_and_follow_reported_state_when_door_buttons_are_pressed() => Scenario.StaAsync(async () =>
    {
        var d = GateRig(EnumDoorState.Unknown);
        var (panel, host) = await OpenGatePanel(d);
        var open = ById<Button>(panel, "GMaps.Property.DoorOpen");
        var close = ById<Button>(panel, "GMaps.Property.DoorClose");
        var state = ById<TextBlock>(panel, "GMaps.Property.DoorState");
        Assert.True(open.IsVisible || open.ActualWidth > 0, "통문 속성창에 문 절이 보여야 한다");

        // 현장이 지금 상태(닫힘)를 보고 → 화면이 따라간다
        ReportDoor(d, EnumDoorState.Closed);
        Assert.Equal("닫힘", state.Text);
        Assert.Equal(DoorUiState.Closed, panel.DoorDisplayState);
        Assert.True(open.IsEnabled, "닫힘이면 [열림] 이 켜져야 한다");
        Assert.False(close.IsEnabled, "이미 닫힘이면 [닫힘] 은 꺼진다");

        // [열림] → NATS GATE_DOOR_SET 발행(서버 REST 아님)
        PropertyPanelScenarioTests.Click(open);
        await Task.Delay(40);
        var (subject, json) = Assert.Single(d.Published);
        Assert.Equal("sensorway.unit001.all.gate-door", subject);
        var env = Parse(json);
        Assert.Equal("PUB", (string?)env["m_type"]);
        Assert.Equal("GATE_DOOR_SET", (string?)env["cmd"]);
        Assert.Equal("GIS", (string?)env["from"]);
        Assert.Equal(77, (int)Body(env)["device_id"]!);
        Assert.Equal("OPEN", (string?)Body(env)["command"]);
        Assert.Equal("[gate] 북문 통문 (number: 3, id: 77)", (string?)Body(env)["device_description"]);
        Assert.Equal("test-admin", (string?)Body(env)["requested_by"]);   // login_id(표시이름 아님 — v2.0.12 §7 머리)        Assert.True(DateTimeOffset.TryParse((string?)Body(env)["requested_at"], out _), "requested_at 은 ISO 8601");
        Assert.Matches(@"[+-]\d\d:\d\d$", (string?)Body(env)["requested_at"]);   // 오프셋 필수

        // 명령은 상태를 바꾸지 않는다 — 대기 표시, 두 단추 잠김, 심볼 문 상태는 그대로
        Assert.Equal("명령 보냄 · 응답 대기", state.Text);
        Assert.Equal(DoorUiState.Pending, panel.DoorDisplayState);
        Assert.False(open.IsEnabled);
        Assert.False(close.IsEnabled);
        Assert.Equal(EnumDoorState.Closed, d.Marker.DoorState);

        // 매니저 보고(열림) → 화면이 따라간다
        ReportDoor(d, EnumDoorState.Open);
        Assert.Equal(EnumDoorState.Open, d.Marker.DoorState);
        Assert.Equal("열림", state.Text);
        Assert.Equal(DoorUiState.Open, panel.DoorDisplayState);
        Assert.False(open.IsEnabled);
        Assert.True(close.IsEnabled);

        // [닫힘] → CLOSE 발행 → 보고(닫힘) → 따라감
        PropertyPanelScenarioTests.Click(close);
        await Task.Delay(40);
        Assert.Equal(2, d.Published.Count);
        Assert.Equal("CLOSE", (string?)Body(Parse(d.Published[1].json))["command"]);
        Assert.Equal("명령 보냄 · 응답 대기", state.Text);
        ReportDoor(d, EnumDoorState.Closed);
        Assert.Equal("닫힘", state.Text);
        Assert.True(open.IsEnabled);
        Assert.False(close.IsEnabled);
        Assert.Empty(d.Rig.Errors);
        GC.KeepAlive(host);
    });

    [Fact]
    [Trait("Scenario", "MAP-132")]
    public void should_release_pending_and_notify_when_nats_publish_fails() => Scenario.StaAsync(async () =>
    {
        var d = GateRig(EnumDoorState.Unknown);
        var (panel, host) = await OpenGatePanel(d);
        ReportDoor(d, EnumDoorState.Closed);
        var open = ById<Button>(panel, "GMaps.Property.DoorOpen");
        var state = ById<TextBlock>(panel, "GMaps.Property.DoorState");
        var notices = new List<string>();
        var notify = panel.NotifyDoorIssue;
        panel.NotifyDoorIssue = m => { notices.Add(m); notify?.Invoke(m); };

        d.NatsOk = false;   // 브로커 끊김
        PropertyPanelScenarioTests.Click(open);
        await Task.Delay(60);

        Assert.Single(d.Published);                         // 시도는 했다
        Assert.Contains("개폐 명령을 보내지 못했습니다.", notices);   // 조용한 무동작 금지
        Assert.Equal("닫힘", state.Text);                    // 대기 풀림 — 보고된 상태로 복귀
        Assert.Equal(DoorUiState.Closed, panel.DoorDisplayState);
        Assert.True(open.IsEnabled);                        // 다시 시도할 수 있다
        GC.KeepAlive(host);
    });

    [Fact]
    [Trait("Scenario", "MAP-132")]
    public void should_keep_door_buttons_off_and_publish_nothing_without_device_control_permission() => Scenario.StaAsync(async () =>
    {
        var d = GateRig(EnumDoorState.Unknown, canControl: false);
        var (panel, host) = await OpenGatePanel(d);
        ReportDoor(d, EnumDoorState.Closed);
        var open = ById<Button>(panel, "GMaps.Property.DoorOpen");
        var state = ById<TextBlock>(panel, "GMaps.Property.DoorState");

        Assert.False(open.IsEnabled);
        Assert.Equal("권한 없음(devices:control)", state.Text);
        panel.OpenDoorCommand.Execute(null);   // 단추를 우회해도
        await Task.Delay(30);
        Assert.Empty(d.Published);             // 발행 없음(fail-closed)
        GC.KeepAlive(host);
    });

    [Fact]
    [Trait("Scenario", "MAP-132")]
    [Trait("Defect", "EDT-D1")]
    public void should_show_reported_door_state_and_enable_buttons_as_soon_as_gate_property_panel_opens() => Scenario.StaAsync(async () =>
    {
        // 심볼의 문 상태가 이미 '닫힘'(이전 OPERATION_EVENT 로 받아 둠)인 통문을 편집 모드에서 눌러 속성창을 연다.
        var d = GateRig(EnumDoorState.Closed);
        var (panel, host) = await OpenGatePanel(d);
        var open = ById<Button>(panel, "GMaps.Property.DoorOpen");
        var state = ById<TextBlock>(panel, "GMaps.Property.DoorState");

        Assert.True(state.Text == "닫힘" && open.IsEnabled,
            $"EDT-D1: 권한 · 연결 · 배선이 모두 있는데 속성창을 막 연 직후 문 상태 글이 '{state.Text}', [열림] IsEnabled={open.IsEnabled} — " +
            "속성창이 SelectedMarker 설정 때(팩토리) 문 UI 를 계산하고, VM 이 그 뒤에 WireDoorControl 로 권한 판정기 · 전송기를 꽂지만 다시 계산하지 않아 " +
            "다음 문 상태 보고가 올 때까지 '권한 없음(devices:control)' · 단추 꺼짐으로 남는다");
        GC.KeepAlive(host);
    });

    // ───────────────────────── MAP-134 ─────────────────────────

    private static IEditableMarker AddGeometric(WiredMapRig rig, int id, string title, int dx, int dy = 0, bool locked = false)
    {
        var center = rig.Map.FromLatLngToLocal(rig.Map.Position);
        var geo = rig.Map.FromLocalToLatLng((int)center.X + dx, (int)center.Y + dy);
        var marker = new MarkerFactory(Mock.Of<ILogService>(), rig.Devices).CreateMarker(new GeometricSymbolModel
        {
            Id = id, Title = title, Latitude = geo.Lat, Longitude = geo.Lng, Width = 40, Height = 40, Opacity = 0.7, ShowShape = true, IsLocked = locked,
        });
        rig.Map.Markers.Add((GMap.NET.WindowsPresentation.GMapMarker)marker);
        return marker;
    }

    private static GroupSelectionAdorner[] GroupAdorners(WiredMapRig rig)
        => AdornerLayer.GetAdornerLayer(rig.Map)?.GetAdorners(rig.Map)?.OfType<GroupSelectionAdorner>().ToArray() ?? Array.Empty<GroupSelectionAdorner>();

    private static IReadOnlyList<IEditableMarker> AdornerMarkers(GroupSelectionAdorner a) => Scenario.GetField<IReadOnlyList<IEditableMarker>>(a, "_markers");

    [Fact]
    [Trait("Scenario", "MAP-134")]
    public void should_toggle_members_and_show_group_adorner_only_for_two_or_more_when_symbols_are_ctrl_clicked() => Scenario.StaAsync(async () =>
    {
        var rig = WiredMapRig.Create();
        var a = AddGeometric(rig, 1341, "가", -150);
        var b = AddGeometric(rig, 1342, "나", 0);
        var c = AddGeometric(rig, 1343, "다", 150);
        Scenario.Layout(rig.Deco, 1200, 800);
        rig.AssertWired("MarkerToggleRequested", "OnMapMarkerToggleRequested");

        // Ctrl+클릭 ① 가 → 1개 = 단일 선택(파란 편집 어도너 + 속성창), 그룹 어도너 없음
        rig.Raise("MarkerToggleRequested", a);
        await Task.Delay(40);
        Assert.Same(a, rig.Vm.SelectedMarker);
        Assert.Empty(rig.Vm.SelectedMarkers);
        Assert.Empty(GroupAdorners(rig));
        Assert.Equal("1개 선택", rig.Vm.AimStatusMessage);

        // ② 나 → 2개 = 그룹(하늘색 그룹 어도너 하나, 공통 속성창)
        rig.Raise("MarkerToggleRequested", b);
        await Task.Delay(40);
        Assert.Null(rig.Vm.SelectedMarker);
        Assert.Equal(new[] { 1341, 1342 }, rig.Vm.SelectedMarkers.Select(m => m.Id).OrderBy(x => x));
        var adorner = Assert.Single(GroupAdorners(rig));
        Assert.Equal(new[] { 1341, 1342 }, AdornerMarkers(adorner).Select(m => m.Id).OrderBy(x => x));
        Assert.Equal("2개 선택", rig.Vm.AimStatusMessage);
        Assert.True(rig.Vm.HasSelectedItem);
        Assert.True(rig.Vm.IsPropertyPanelVisible);
        Assert.True(rig.Vm.PropertyPanel!.IsGroupEditing);
        Assert.Equal(new[] { "그룹 삭제", "그룹 잠금", "그룹 잠금 해제", "그룹 표시", "그룹 숨김", "맨 위로", "맨 아래로" },
            adorner.ContextMenu!.Items.OfType<MenuItem>().Select(m => (string)m.Header));   // 그룹 공통 메뉴

        // ③ 다 → 3개
        rig.Raise("MarkerToggleRequested", c);
        await Task.Delay(30);
        Assert.Equal(new[] { 1341, 1342, 1343 }, rig.Vm.SelectedMarkers.Select(m => m.Id).OrderBy(x => x));
        Assert.Equal(new[] { 1341, 1342, 1343 }, AdornerMarkers(Assert.Single(GroupAdorners(rig))).Select(m => m.Id).OrderBy(x => x));

        // ④ 나 다시 → 토글 해제(나머지 유지)
        rig.Raise("MarkerToggleRequested", b);
        await Task.Delay(30);
        Assert.Equal(new[] { 1341, 1343 }, rig.Vm.SelectedMarkers.Select(m => m.Id).OrderBy(x => x));
        Assert.Equal(new[] { 1341, 1343 }, AdornerMarkers(Assert.Single(GroupAdorners(rig))).Select(m => m.Id).OrderBy(x => x));

        // ⑤ 가 해제 → 1개 남음 = 단일 선택으로 강등(그룹 어도너 사라짐)
        rig.Raise("MarkerToggleRequested", a);
        await Task.Delay(40);
        Assert.Empty(GroupAdorners(rig));
        Assert.Same(c, rig.Vm.SelectedMarker);
        Assert.Empty(rig.Vm.SelectedMarkers);

        // ⑥ 다 해제 → 아무것도 선택 안 됨
        rig.Raise("MarkerToggleRequested", c);
        await Task.Delay(40);
        Assert.Null(rig.Vm.SelectedMarker);
        Assert.Empty(rig.Vm.SelectedMarkers);
        Assert.Empty(GroupAdorners(rig));
        Assert.Equal("선택 해제", rig.Vm.AimStatusMessage);
        Assert.Empty(rig.Errors);
    });

    [Fact]
    [Trait("Scenario", "MAP-134")]
    public void should_group_unlocked_symbols_inside_rubber_band_and_attach_group_adorner_when_shift_dragged() => Scenario.StaAsync(async () =>
    {
        var rig = WiredMapRig.Create();
        var a = AddGeometric(rig, 1351, "안-가", -100, -40);
        var b = AddGeometric(rig, 1352, "안-나", 100, 40);
        var locked = AddGeometric(rig, 1353, "안-잠김", 0, 0, locked: true);
        var outside = AddGeometric(rig, 1354, "밖", 450, 300);
        Scenario.Layout(rig.Deco, 1200, 800);
        rig.AssertWired("RubberBandStarted", "OnRubberBandStarted");
        rig.AssertWired("MarkersRubberBandSelected", "OnMarkersRubberBandSelected");

        // 먼저 하나를 단일 선택해 둔 상태에서 Shift+끌기 시작 → 단일이 그룹 후보로 승격
        rig.Raise("OnMarkerClicked", outside);
        await Task.Delay(40);
        Assert.Same(outside, rig.Vm.SelectedMarker);
        rig.Raise("RubberBandStarted");
        Assert.Null(rig.Vm.SelectedMarker);

        // 놓음: 지도가 사각형 안 마커를 고른다(GetMarkersInRect — 잠김 · 이미지 제외) → VM 이 기존 선택과 병합
        var center = rig.Map.FromLatLngToLocal(rig.Map.Position);
        var rect = new Rect(center.X - 160, center.Y - 90, 320, 180);
        var hits = (IReadOnlyList<IEditableMarker>)Scenario.Call(rig.Map, "GetMarkersInRect", rect)!;
        Assert.Equal(new[] { 1351, 1352 }, hits.Select(m => m.Id).OrderBy(x => x));   // 잠김 · 사각형 밖 제외
        rig.Raise("MarkersRubberBandSelected", hits);
        await Task.Delay(30);

        Assert.Equal(new[] { 1351, 1352, 1354 }, rig.Vm.SelectedMarkers.Select(m => m.Id).OrderBy(x => x));   // 승격된 단일 + 사각형 2개
        var adorner = Assert.Single(GroupAdorners(rig));
        Assert.Equal(new[] { 1351, 1352, 1354 }, AdornerMarkers(adorner).Select(m => m.Id).OrderBy(x => x));
        Assert.Equal("3개 선택", rig.Vm.AimStatusMessage);
        Assert.DoesNotContain(locked, rig.Vm.SelectedMarkers);

        // 같은 사각형을 다시 끌면 겹치는 것만 토글(해제) → 1개 남음 = 단일 강등
        rig.Raise("RubberBandStarted");
        rig.Raise("MarkersRubberBandSelected", hits);
        await Task.Delay(40);
        Assert.Empty(GroupAdorners(rig));
        Assert.Same(outside, rig.Vm.SelectedMarker);
        Assert.Empty(rig.Errors);
    });

    [Fact]
    [Trait("Scenario", "MAP-134")]
    public void should_ignore_ctrl_click_toggle_when_edit_mode_is_off() => Scenario.StaAsync(async () =>
    {
        var rig = WiredMapRig.Create();
        var a = AddGeometric(rig, 1361, "가", -80);
        var b = AddGeometric(rig, 1362, "나", 80);
        rig.Vm.IsEditModeEnabled = false;
        rig.Raise("MarkerToggleRequested", a);
        rig.Raise("MarkerToggleRequested", b);
        await Task.Delay(30);
        Assert.Empty(rig.Vm.SelectedMarkers);
        Assert.Null(rig.Vm.SelectedMarker);
        Assert.Empty(GroupAdorners(rig));
    });
}
