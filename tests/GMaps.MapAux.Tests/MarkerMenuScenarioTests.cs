using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Xml.Linq;
using Caliburn.Micro;
using GMap.NET;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Units;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.Managers;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Dialogs;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using ScenarioShared;
using Xunit;
using ApiDetectionList = Ironwall.Dotnet.Libraries.Messages.Defines.Apis.ApiListResponse<Ironwall.Dotnet.Libraries.Messages.Dto.Events.DetectionEventDto>;

namespace GMaps.MapAux.Tests;

/// <summary>
/// 카탈로그 §1 MAP-155(우클릭 · 소속 부대 줄) · MAP-156(우클릭 · 탐지 이력) · MAP-169(등록 센서 정보 · 행 이력/끌기/닫기).
/// 우클릭 메뉴는 ShowMarkerContextMenu 안에서 지역 ContextMenu 를 만들어 곧바로 IsOpen(팝업 창) 하므로 메뉴 자체는 띄우지 않는다 —
/// 메뉴 항목을 만드는 메서드(AddUnitLineMenuItem)를 열지 않은 ContextMenu 에 직접 부르거나, 항목 Click 이 부르는 메서드를 부르고
/// 그 배선은 소스로 확인한다. 부대 창 · 이력 창 자체는 호스트가 메시지를 받아 연다(실제 창 = 헤디드 몫).
/// </summary>
public class MarkerMenuScenarioTests
{
    private static (MapViewModel vm, GMapCustomControl map, Sink sink) Vm()
    {
        var vm = Scenario.Uninit<MapViewModel>();
        var map = new GMapCustomControl { Width = 640, Height = 480, MinZoom = 1, MaxZoom = 21, Zoom = 17, Position = new PointLatLng(37.5, 127.0) };
        Scenario.SetField(vm, "<MainMap>k__BackingField", map);
        var ea = new EventAggregator(); var sink = new Sink(); ea.SubscribeOnPublishedThread(sink);
        Scenario.SetField(vm, "_eventAggregator", ea);
        Scenario.SetField(vm, "_log", Mock.Of<ILogService>());
        return (vm, map, sink);
    }

    private static string MapViewModelSource() => File.ReadAllText(Path.Combine(RepoPaths.GMapsUi, "ViewModels", "Maps", "MapViewModel.cs"));

    /// <summary>ShowMarkerContextMenu 본문(메뉴 열기 직전까지).</summary>
    private static string ContextMenuBody()
    {
        var src = MapViewModelSource();
        int start = src.IndexOf("public void ShowMarkerContextMenu(IEditableMarker marker, Point screenPosition)", StringComparison.Ordinal);
        Assert.True(start > 0, "ShowMarkerContextMenu 없음");
        int open = src.IndexOf("menu.IsOpen = true", start, StringComparison.Ordinal);
        Assert.True(open > start, "ShowMarkerContextMenu 의 menu.IsOpen 을 찾지 못함");
        return src[start..open];
    }

    // ───────────────────────── MAP-155 ─────────────────────────

    /// <remarks>헤디드 몫: 부대 창이 실제로 열리고 관계도 레일(Console.Units.Rail)에서 그 부대가 선택되는 것 — 받는 쪽(UnitConsoleLauncher)은 Devices.Ui(호스트).</remarks>
    [Fact]
    [Trait("Scenario", "MAP-155")]
    public void should_publish_open_unit_console_request_with_that_unit_id_when_unit_line_menu_item_clicked() => OneSta.Sta(() =>
    {
        var (vm, _, sink) = Vm();
        var directory = new Mock<IUnitDirectory>();
        directory.SetupGet(d => d.IsAvailable).Returns(true);
        directory.Setup(d => d.Describe(7)).Returns("7중대 · 2대대 › 1연대");
        Scenario.SetField(vm, "_unitDirectory", directory.Object);
        Scenario.SetField(vm, "_unitDirectoryResolved", true);
        using var marker = Parts.Marker(new SensorDeviceModel { Id = 61, DeviceType = EnumDeviceType.SmartSensor, UnitId = 7 }, EnumDeviceType.SmartSensor, "북측 5구간");

        var menu = new ContextMenu();   // 열지 않는다(IsOpen 안 건드림)
        Scenario.Call(vm, "AddUnitLineMenuItem", menu, marker);
        var item = menu.Items.OfType<MenuItem>().Single(i => AutomationProperties.GetAutomationId(i) == "GMaps.MarkerMenu.OpenUnitMap");
        Assert.Equal("소속 부대 7중대 · 2대대 › 1연대  ›  관계도에서 보기", item.Header);
        Assert.True(item.IsEnabled);
        Assert.False(menu.IsOpen);

        Parts.Press(item);   // MenuItem.OnClick = Click 처리기
        var request = Assert.Single(sink.Messages.OfType<OpenUnitConsoleRequest>());
        Assert.Equal(7, request.UnitId);   // 그 부대 id
        Assert.True(request.OpenMap);      // 관계도 레일로(트리 레일 아님)

        // 실제 메뉴 경로가 이 항목을 붙이는지(메뉴를 띄우지 않으려 소스 판독)
        Assert.Contains("AddUnitLineMenuItem(menu, marker);", ContextMenuBody());
    });

    [Fact]
    [Trait("Scenario", "MAP-155")]
    public void should_disable_or_hide_unit_line_and_publish_nothing_when_unit_unknown_or_edit_mode() => OneSta.Sta(() =>
    {
        var (vm, _, sink) = Vm();
        var directory = new Mock<IUnitDirectory>();
        directory.SetupGet(d => d.IsAvailable).Returns(true);
        directory.Setup(d => d.Describe(It.IsAny<int>())).Returns((string?)null);
        directory.Setup(d => d.EnsureLoadedAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        Scenario.SetField(vm, "_unitDirectory", directory.Object);
        Scenario.SetField(vm, "_unitDirectoryResolved", true);
        using var marker = Parts.Marker(new SensorDeviceModel { Id = 62, DeviceType = EnumDeviceType.SmartSensor, UnitId = 99 }, EnumDeviceType.SmartSensor, "외곽-02");

        var menu = new ContextMenu();
        Scenario.Call(vm, "AddUnitLineMenuItem", menu, marker);
        var item = menu.Items.OfType<MenuItem>().Single();
        Assert.Equal(SymbolDetailUnitLine.NoUnitText, item.Header);   // 편제에 없는 부대 — 이름을 지어내지 않는다
        Assert.False(item.IsEnabled);   // 비활성 = 사용자는 누를 수 없다(WPF 가 입력을 막는다 — OnClick 직접 호출은 그 차단을 우회하므로 부르지 않는다)
        Assert.Empty(sink.Messages.OfType<OpenUnitConsoleRequest>());

        // 편집 모드 = 부대 줄 없음(MAP-154 와 같은 규칙)
        Scenario.SetField(vm, "_isEditModeEnabled", true);
        var editMenu = new ContextMenu();
        Scenario.Call(vm, "AddUnitLineMenuItem", editMenu, marker);
        Assert.Empty(editMenu.Items);
    });

    // ───────────────────────── MAP-156 ─────────────────────────

    /// <remarks>헤디드 몫: 호스트 DialogShell 이 메시지를 받아 이력 창을 실제로 띄우는 것과 창 글.</remarks>
    [Fact]
    [Trait("Scenario", "MAP-156")]
    public void should_publish_detection_history_request_and_open_read_only_history_view_model_when_sensor_history_menu_clicked() => OneSta.StaAsync(async () =>
    {
        var (vm, _, sink) = Vm();
        using var marker = Parts.Marker(new SensorDeviceModel { Id = 71, DeviceType = EnumDeviceType.SmartSensor, DeviceName = "Sensor-A-7", DeviceNumber = 7 },
            EnumDeviceType.SmartSensor, "외곽-07");

        Scenario.Call(vm, "OpenDetectionHistory", marker);   // 「탐지 이력」 Click 처리기가 부르는 메서드
        var message = Assert.Single(sink.Messages.OfType<OpenDetectionHistoryDialogMessageModel>());
        Assert.Equal(71, message.DeviceId);
        Assert.Equal("Sensor-A-7", message.DeviceName);
        Assert.Equal(7, message.DeviceNumber);

        // 메뉴 항목 배선(소스) — 감지센서만 · 장비 없으면 비활성 · Click = OpenDetectionHistory
        var body = ContextMenuBody();
        int history = body.IndexOf("Header = \"탐지 이력\"", StringComparison.Ordinal);
        Assert.True(history > 0, "우클릭 메뉴에 「탐지 이력」 항목이 없다");
        Assert.True(body.LastIndexOf("if (SymbolDetailRules.IsSensor(pidsMarker.DeviceType))", history, StringComparison.Ordinal) > 0);
        Assert.Contains("historyItem.Click += (s, e) => OpenDetectionHistory(pidsMarker);", body[history..]);

        // 받은 쪽 이력 뷰모델 — 읽기 전용: 조회(GET)만 하고 쓰기 호출은 0(Strict 가짜)
        var api = new Mock<IEventApiService>(MockBehavior.Strict);
        api.Setup(a => a.GetDetectionEventsAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(new ApiDetectionList
           {
               Success = true,
               Data = new List<DetectionEventDto>
               {
                   new() { Id = 1, CreatedAt = "2026-10-11T09:00:00", TypeEvent = "Intrusion", ActionReported = "False", Result = "PIR_SENSOR",
                           Device = new Ironwall.Dotnet.Libraries.Messages.Dto.Devices.BaseDeviceDto { Id = 71 }, Detail = new DetectionDetailDto { Signal = 1200 } },
               },
           });
        var history2 = new DetectionHistoryDialogViewModel(new EventAggregator(), Mock.Of<ILogService>(), api.Object, new DeviceProvider());
        history2.Initialize(message);
        await ((IActivate)history2).ActivateAsync(CancellationToken.None);

        Assert.Equal("탐지 신호 이력 — Sensor-A-7 (No.7)", history2.HeaderText);
        Assert.False(history2.HasLoadError);
        Assert.Single(history2.FilteredItems);
        api.Verify(a => a.GetDetectionEventsAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int?>(), 71, It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<string?>(), 1, It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once());
        api.VerifyNoOtherCalls();
        Assert.All(typeof(SignalHistoryItemViewModel).GetProperties().Where(p => p.DeclaringType == typeof(SignalHistoryItemViewModel)), p => Assert.False(p.CanWrite && p.SetMethod!.IsPublic, $"이력 행 {p.Name} 에 공개 setter"));

        // 이력 표 = 읽기 전용 · 행 추가 없음
        var grid = XDocument.Load(Path.Combine(RepoPaths.Root, "Ironwall.Dotnet.Libraries.Events.Ui", "Views", "Dialogs", "DetectionHistoryDialogView.xaml"))
            .Descendants().Single(e => e.Name.LocalName == "DataGrid" && (string?)e.Attribute(XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml") + "Name") == "HistoryGrid");
        Assert.Equal("True", (string?)grid.Attribute("IsReadOnly"));
        Assert.Equal("False", (string?)grid.Attribute("CanUserAddRows"));
    });

    // ───────────────────────── MAP-169 ─────────────────────────

    private static (MapViewModel vm, Sink sink, GMapPidsGroupMarker group) SensorInfoRig()
    {
        var (vm, _, sink) = Vm();
        var devices = new DeviceProvider();
        devices.Add(new SensorDeviceModel { Id = 82, DeviceType = EnumDeviceType.Fence, DeviceName = "FN-02", DeviceNumber = 2, DeviceGroups = new List<int> { 7 }, Latitude = 37.51, Longitude = 127.01 });
        devices.Add(new SensorDeviceModel { Id = 81, DeviceType = EnumDeviceType.Fence, DeviceName = "FN-01", DeviceNumber = 1, DeviceGroups = new List<int> { 7 }, Latitude = 37.52, Longitude = 127.02 });
        devices.Add(new SensorDeviceModel { Id = 89, DeviceType = EnumDeviceType.Fence, DeviceName = "다른 구역", DeviceNumber = 3, DeviceGroups = new List<int> { 8 } });
        Scenario.SetField(vm, "<DeviceProvider>k__BackingField", devices);
        var eqm = new Mock<IEventQueueManager>();
        eqm.Setup(q => q.GetDeviceState(82, It.IsAny<EnumDeviceType>())).Returns(EnumCompositeEventStatus.Detecting);
        Scenario.SetField(vm, "_eventQueueManager", eqm.Object);
        var group = new GMapPidsGroupMarker(Mock.Of<ILogService>(), new PidsGroupSymbolModel { Title = "북측 구역", LinkedDeviceGroup = 7, Latitude = 37.5, Longitude = 127.0 });
        return (vm, sink, group);
    }

    /// <remarks>헤디드 몫: 머리 끌기로 실제 이동 — 끌기는 실제 마우스 위치(e.GetPosition)와 캡처를 쓰므로 창 없이 흉내 낼 수 없다. 여기서는 끌기 전제(부모 ContentPresenter 가 Canvas 자식)만 본다.</remarks>
    [Fact]
    [Trait("Scenario", "MAP-169")]
    public void should_request_sensor_history_from_row_menu_and_close_overlay_when_sensor_info_rows_used() => OneSta.Sta(() =>
    {
        var (vm, sink, group) = SensorInfoRig();
        vm.ShowSensorInfoPanel(group);
        Assert.True(vm.IsSensorInfoPanelVisible);
        var panel = vm.SensorInfoPanel!;
        var info = Assert.IsType<SensorInfoPanelViewModel>(panel.DataContext);
        Assert.Equal(new[] { 81, 82 }, info.Rows.Select(r => r.DeviceId));   // 그 구역 센서만 · 번호 순
        Assert.Equal(1, info.DetectCount);

        // 템플릿 행의 우클릭 메뉴 「센서 탐지 이력」(메뉴는 열지 않고 PlacementTarget 만 행에 둔다)
        panel.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/SensorInfoPanelStyle.xaml", UriKind.Relative) });
        panel.Style = (Style)panel.Resources[typeof(SensorInfoPanelControl)];
        Scenario.Layout(panel, 520, 560);
        var rowBorder = Parts.Descendants(panel).OfType<Border>().First(b => b.ContextMenu != null && b.DataContext is SensorInfoRowModel { DeviceId: 82 });
        var menu = rowBorder.ContextMenu!;
        menu.PlacementTarget = rowBorder;
        var historyItem = menu.Items.OfType<MenuItem>().Single(i => (string)i.Header == "센서 탐지 이력");
        Assert.False(menu.IsOpen);
        Assert.NotNull(historyItem.Command);
        Parts.Press(historyItem);
        var message = Assert.Single(sink.Messages.OfType<OpenDetectionHistoryDialogMessageModel>());
        Assert.Equal(82, message.DeviceId);
        Assert.Equal("FN-02", message.DeviceName);
        Assert.Equal(2, message.DeviceNumber);

        // Draft 행(DeviceId 0)은 보내지 않는다
        info.SensorHistoryCommand.Execute(new SensorInfoRowModel { DeviceId = 0, DeviceName = "새 센서" });
        Assert.Single(sink.Messages.OfType<OpenDetectionHistoryDialogMessageModel>());

        // 머리 [✕] 닫기 → 겹창 숨김, 다시 열면 같은 인스턴스(단일)
        var close = Parts.Descendants(panel).OfType<Button>().First(b => b.Name == "PART_CloseButton");
        Parts.Press(close);
        Assert.False(vm.IsSensorInfoPanelVisible);
        vm.ShowSensorInfoPanel(group);
        Assert.True(vm.IsSensorInfoPanelVisible);
        Assert.Same(panel, vm.SensorInfoPanel);

        // 발 [닫기] 도 같은 명령
        var footerClose = Parts.Descendants(panel).OfType<Button>().First(b => b.Content as string == "닫기");
        Parts.Press(footerClose);
        Assert.False(vm.IsSensorInfoPanelVisible);

        // 끌기 전제 — 겹창 자리(ContentPresenter)가 Canvas 의 직계 자식(SensorInfoPanelControl.OnDragDown 이 이 조건에서만 끈다)
        var doc = XDocument.Load(Path.Combine(RepoPaths.GMapsUi, "Views", "Maps", "MapView.xaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var presenter = doc.Descendants().Single(e => (string?)e.Attribute(x + "Name") == "SensorInfoPresenter");
        Assert.Equal("Canvas", presenter.Parent!.Name.LocalName);
    });
}
