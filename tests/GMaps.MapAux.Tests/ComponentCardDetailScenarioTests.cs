using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using GMap.NET;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Adorners;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Detail;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Monitoring.Models.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using ScenarioShared;
using Xunit;

namespace GMaps.MapAux.Tests;

/// <summary>헤드리스 시험 공용 — 부품 축 · 심볼 · 템플릿 단추 찾기/누르기(창 없음).</summary>
internal static class Parts
{
    /// <summary>부품 축 묶음(선언 + 관측). 튜플 = (key, 유형, 사용 여부, 상태, 건강, 사유).</summary>
    internal static DeviceAxesModel Axes(params (string Key, string Type, bool? InService, string? State, string? Health, string? Reason)[] parts)
    {
        var spec = new HardwareSpecModel();
        var status = new DeviceStatusModel();
        foreach (var p in parts)
        {
            spec.Components.Add(new ComponentDefinitionModel { Key = p.Key, Type = p.Type, InService = p.InService });
            status.Components[p.Key] = new ComponentStatusModel { State = p.State, Health = p.Health, FaultReason = p.Reason, ObservedAt = "2026-10-11T09:00:00.000000+09:00" };
        }
        return new DeviceAxesModel { HardwareSpec = spec, DeviceStatus = status, Meta = new ResponseMeta("full", new[] { "hardware_spec", "device_status" }) };
    }

    /// <summary>선언 순서 heater(정상) · fan(고장) · ir(저하) · ptz(정상).</summary>
    internal static DeviceAxesModel Mixed() => Axes(
        ("heater", "HEATER", null, "ON", "OK", null),
        ("fan", "FAN", null, "OFF", "FAULT", "STALL"),
        ("ir", "IR_LED", null, "ON", "DEGRADED", null),
        ("ptz", "PTZ_UNIT", null, "IDLE", "OK", null));

    internal static GMapPidsMarker Marker(IBaseDeviceModel device, EnumDeviceType type, string title, EnumDoorState door = EnumDoorState.Unknown)
    {
        var marker = new GMapPidsMarker(Mock.Of<ILogService>(), new PidsSymbolModel
        {
            Title = title, DeviceType = type, Latitude = 37.5, Longitude = 127.0, LinkedDeviceId = device.Id, DoorState = door, Zoom = 1,
        });
        marker.Width = 60; marker.Height = 60; marker.IsLayerEnabled = true; marker.IsVisible = true;
        marker.LinkedDevice = device;
        return marker;
    }

    internal static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var d in Descendants(child)) yield return d;
        }
    }

    internal static T ById<T>(DependencyObject root, string automationId) where T : DependencyObject
        => Descendants(root).OfType<T>().SingleOrDefault(e => AutomationProperties.GetAutomationId(e) == automationId)
           ?? throw new InvalidOperationException($"AutomationId '{automationId}' 요소가 템플릿에 없다");

    /// <summary>단추 누름 = ButtonBase.OnClick(Click 이벤트 + Command 실행). 창 · 마우스 없이 부른다.</summary>
    internal static void Press(System.Windows.Controls.Primitives.ButtonBase button) => Scenario.Call(button, "OnClick");

    /// <summary>메뉴 항목 누름 = MenuItem.OnClick(Click 이벤트 + Command 실행). 메뉴는 열지 않는다.</summary>
    internal static void Press(MenuItem item)
    {
        Scenario.Call(item, "OnClick");
        Scenario.Pump();   // MenuItem 은 Click 을 Render 우선순위로 미뤄 올린다(InvokeClickAfterRender) — 큐를 비워 처리기까지 돌린다
    }
}

/// <summary>
/// 카탈로그 §1 MAP-151(부품 카드 단추) · MAP-152(PIDS 심볼 툴팁) · MAP-164(상세 보기 부품 탭 행 · 정렬).
/// 카드는 실제 <see cref="ComponentCardService"/> + 어도너 층(부모 = AdornerDecorator, 창 없음)에 띄우고, 템플릿 단추를 AutomationId 로 찾아 누른다.
/// 장비 콘솔 · 결선 창 자체는 호스트(좌측 메뉴)가 연다 — 여기서는 열기 요청(<see cref="IDeviceConsoleNavigator"/>)까지 본다(실제 창 = 헤디드 몫).
/// </summary>
public class ComponentCardDetailScenarioTests
{
    private static (GMapCustomControl map, AdornerDecorator deco) MapInDecorator()
    {
        var map = new GMapCustomControl { Width = 640, Height = 480, MinZoom = 1, MaxZoom = 21, Zoom = 17, Position = new PointLatLng(37.5, 127.0) };
        var deco = new AdornerDecorator { Child = map };
        Scenario.Layout(deco, 640, 480);
        return (map, deco);
    }

    private static ComponentCardAdorner CardAdorner(GMapCustomControl map)
        => AdornerLayer.GetAdornerLayer(map)!.GetAdorners(map)?.OfType<ComponentCardAdorner>().SingleOrDefault()
           ?? throw new InvalidOperationException("부품 카드 어도너가 지도 층에 없다");

    private static bool HasCard(GMapCustomControl map)
        => AdornerLayer.GetAdornerLayer(map)!.GetAdorners(map)?.OfType<ComponentCardAdorner>().Any() == true;

    // ───────────────────────── MAP-151 ─────────────────────────

    [Fact]
    [Trait("Scenario", "MAP-151")]
    public void should_request_device_console_and_wiring_then_close_card_when_component_card_buttons_pressed() => OneSta.Sta(() =>
    {
        var (map, deco) = MapInDecorator();
        var nav = new Mock<IDeviceConsoleNavigator>(MockBehavior.Strict);
        nav.Setup(n => n.CanOpenDevice(11)).Returns(true);
        nav.Setup(n => n.CanOpenWiring(11)).Returns(true);
        nav.Setup(n => n.OpenDevice(11));
        nav.Setup(n => n.OpenWiring(11));
        var service = new ComponentCardService(map, Mock.Of<ILogService>(), () => nav.Object);
        var sensor = Parts.Marker(new SensorDeviceModel { Id = 11, DeviceType = EnumDeviceType.SmartSensor, Status = EnumDeviceStatus.ACTIVATED, Axes = Parts.Mixed() },
            EnumDeviceType.SmartSensor, "북측 5구간");
        map.Markers.Add(sensor);

        service.Show(sensor);
        Assert.Same(sensor, service.Current);
        Assert.True(sensor.IsComponentCardOpen);
        var adorner = CardAdorner(map);
        Scenario.Layout(deco, 640, 480);   // 어도너 층 배치 → 카드 템플릿 적용

        var card = adorner.Card;
        Assert.StartsWith("북측 5구간 · ", adorner.ViewModel.Title);   // 머리 = 심볼 이름 · 종류
        var openConsole = Parts.ById<Button>(card, "GMaps.ComponentCard.OpenConsole");
        var openWiring = Parts.ById<Button>(card, "GMaps.ComponentCard.OpenWiring");
        var close = Parts.ById<Button>(card, "GMaps.ComponentCard.Close");
        Assert.Equal(Visibility.Visible, openConsole.Visibility);
        Assert.Equal(Visibility.Visible, openWiring.Visibility);   // 센서 = 결선 단추 보임

        Parts.Press(openConsole);
        nav.Verify(n => n.OpenDevice(11), Times.Once());   // 장비 창 열기 요청 — 그 장비 id
        Parts.Press(openWiring);
        nav.Verify(n => n.OpenWiring(11), Times.Once());   // 결선 창 열기 요청 — 그 센서 id
        Assert.Same(sensor, service.Current);              // 항법 단추는 카드를 닫지 않는다

        Parts.Press(close);
        Assert.Null(service.Current);
        Assert.False(sensor.IsComponentCardOpen);
        Assert.False(HasCard(map), "닫기 뒤에도 카드 어도너가 지도 층에 남아 있다");
    });

    [Fact]
    [Trait("Scenario", "MAP-151")]
    public void should_hide_wiring_for_non_sensor_and_hide_both_links_when_navigator_is_not_registered() => OneSta.Sta(() =>
    {
        var (map, deco) = MapInDecorator();
        var nav = new Mock<IDeviceConsoleNavigator>();
        nav.Setup(n => n.CanOpenDevice(It.IsAny<int>())).Returns(true);
        nav.Setup(n => n.CanOpenWiring(It.IsAny<int>())).Returns(true);
        var service = new ComponentCardService(map, Mock.Of<ILogService>(), () => nav.Object);
        var camera = Parts.Marker(new CameraDeviceModel { Id = 12, DeviceType = EnumDeviceType.IpCamera, Status = EnumDeviceStatus.ACTIVATED, Axes = Parts.Mixed() },
            EnumDeviceType.IpCamera, "PTZ-07");
        map.Markers.Add(camera);

        service.Show(camera);
        Scenario.Layout(deco, 640, 480);
        var card = CardAdorner(map).Card;
        Assert.Equal(Visibility.Visible, Parts.ById<Button>(card, "GMaps.ComponentCard.OpenConsole").Visibility);
        Assert.Equal(Visibility.Collapsed, Parts.ById<Button>(card, "GMaps.ComponentCard.OpenWiring").Visibility);   // 카메라 = 결선 없음
        Assert.False(CardAdorner(map).ViewModel.OpenWiringCommand.CanExecute(null) && CardAdorner(map).ViewModel.CanOpenWiring);
        CardAdorner(map).ViewModel.OpenWiringCommand.Execute(null);
        nav.Verify(n => n.OpenWiring(It.IsAny<int>()), Times.Never());   // 숨은 단추를 우회 호출해도 요청이 나가지 않는다

        // 호스트가 입구를 등록하지 않았으면 두 단추 모두 숨김(누를 수 없는 단추를 두지 않는다)
        service.Hide();
        var bare = new ComponentCardService(map, Mock.Of<ILogService>(), () => null);
        bare.Show(camera);
        Scenario.Layout(deco, 640, 480);
        Assert.False(CardAdorner(map).ViewModel.HasActions);
        Assert.Equal(Visibility.Collapsed, Parts.ById<Button>(CardAdorner(map).Card, "GMaps.ComponentCard.OpenConsole").Visibility);
    });

    // ───────────────────────── MAP-152 ─────────────────────────

    [Fact]
    [Trait("Scenario", "MAP-152")]
    public void should_compute_title_door_and_component_lines_in_tooltip_when_hovering_pids_symbol() => OneSta.Sta(() =>
    {
        var gate = new GateDeviceModel { Id = 21, DeviceType = EnumDeviceType.Gate, Status = EnumDeviceStatus.ACTIVATED,
            Axes = Parts.Axes(("heater", "HEATER", null, "ON", "FAULT", "OVERHEAT"), ("fan", "FAN", null, "ON", "OK", null)) };
        using var marker = Parts.Marker(gate, EnumDeviceType.Gate, "북문 통문", EnumDoorState.Open);
        var control = new GMapMarkerPidsControl(marker);

        var lines = control.SymbolToolTip!.Split(Environment.NewLine);
        Assert.Equal("북문 통문", lines[0]);                            // 제목
        Assert.Equal("문 열림", lines[1]);                              // 문
        Assert.StartsWith("부품 상태 — 고장 1 · 저하 0 / 2", lines[2]);   // 부품 절 머리
        Assert.Contains(lines.Skip(3), l => l.Contains("고장"));        // 고장 부품 줄

        // UIA 도움말(HelpText) = 같은 글 — 헤디드가 읽는 단언점
        var peer = UIElementAutomationPeer.CreatePeerForElement(control);
        Assert.Equal(control.SymbolToolTip, peer.GetHelpText());

        // 템플릿의 실제 툴팁 요소에 같은 글이 실린다(TemplateBinding)
        control.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/PidsMarkerStyle.xaml", UriKind.Relative) });
        control.Style = (Style)control.Resources[control.GetType()];
        control.Width = 60; control.Height = 60;
        Scenario.Layout(control, 60, 60);
        Assert.Contains(Parts.Descendants(control).OfType<FrameworkElement>(), e => e.ToolTip is string s && s == control.SymbolToolTip);

        // 문이 닫히고(형태 축) 부품이 정상으로 돌아오면 툴팁도 따라간다
        marker.Model.DoorState = EnumDoorState.Closed;   // 이벤트 경로(ApplyDoorState)는 모델에 쓰고 SetUpdate 로 알린다
        gate.Axes = Parts.Axes(("heater", "HEATER", null, "ON", "OK", null), ("fan", "FAN", null, "ON", "OK", null));
        marker.Model.SetUpdate();
        lines = control.SymbolToolTip!.Split(Environment.NewLine);
        Assert.Equal("문 닫힘", lines[1]);
        Assert.DoesNotContain("고장 1", lines[2]);
    });

    [Fact]
    [Trait("Scenario", "MAP-152")]
    public void should_show_title_only_tooltip_when_symbol_has_no_door_and_no_component_axes() => OneSta.Sta(() =>
    {
        using var marker = Parts.Marker(new SensorDeviceModel { Id = 31, DeviceType = EnumDeviceType.Fence, Status = EnumDeviceStatus.ACTIVATED, Axes = null },
            EnumDeviceType.Fence, "외곽-07");
        var control = new GMapMarkerPidsControl(marker);
        Assert.Equal("외곽-07", control.SymbolToolTip);   // 6.3(축 없음) · 문 없는 종류 = 종전처럼 제목만
    });

    // ───────────────────────── MAP-164 ─────────────────────────

    private static ListBoxItem RowItem(ComponentTableRow row)
    {
        var dict = new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/ComponentTableStyle.xaml", UriKind.Relative) };
        return new ListBoxItem { DataContext = row, Style = (Style)dict["Gm.Component.RowItem"] };
    }

    [Fact]
    [Trait("Scenario", "MAP-164")]
    public void should_list_rows_in_declared_order_and_reorder_fault_first_when_sort_toggled_in_detail_components_tab() => OneSta.Sta(() =>
    {
        var device = new SensorDeviceModel { Id = 41, DeviceType = EnumDeviceType.SmartSensor, Status = EnumDeviceStatus.ACTIVATED, Axes = Parts.Mixed() };
        using var marker = Parts.Marker(device, EnumDeviceType.SmartSensor, "북측 5구간");
        var vm = new SymbolDetailViewModel();
        vm.Load(marker, new SymbolDetailContext(EnumDeviceType.SmartSensor, HasDevice: true), initialTab: SymbolDetailTab.Components);

        var tab = vm.Tabs.Single(t => t.Tab == SymbolDetailTab.Components);
        Assert.Same(tab, vm.SelectedTab);                         // 우클릭 "상세 보기 › 부품" = 부품 탭으로 연다
        Assert.Equal("부품", tab.Header);
        var table = tab.ComponentTable!;
        Assert.Equal(ComponentSortMode.Declared, table.SortMode); // 상세 탭 첫 정렬 = 선언 순서
        Assert.Equal("선언 순서", table.SortText);
        Assert.Equal(new[] { "heater", "fan", "ir", "ptz" }, table.Rows.Select(r => r.Key));

        // 행 id = Symbol.Detail.Components.Row.{key}(ListBoxItem 스타일 바인딩)
        var ids = table.Rows.Select(r => AutomationProperties.GetAutomationId(RowItem(r))).ToList();
        Assert.Equal(new[] { "Symbol.Detail.Components.Row.heater", "Symbol.Detail.Components.Row.fan", "Symbol.Detail.Components.Row.ir", "Symbol.Detail.Components.Row.ptz" }, ids);

        // Sort 단추(토글 IsChecked ↔ IsFaultFirst) = 고장 먼저 → 저하 → 나머지(선언 순)
        var changed = new List<string?>(); table.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        table.IsFaultFirst = true;
        Assert.Equal("고장 먼저", table.SortText);
        Assert.Equal(new[] { "fan", "ir", "heater", "ptz" }, table.Rows.Select(r => r.Key));
        Assert.Contains(nameof(ComponentTableModel.Rows), changed);   // 화면 목록이 다시 그려지도록 통지
        Assert.Equal("Symbol.Detail.Components.Row.fan", AutomationProperties.GetAutomationId(RowItem(table.Rows[0])));

        // SYNC_DEVICE 로 부품 관측이 바뀌어도 고른 정렬은 유지 — ptz 가 새로 고장
        device.Axes = Parts.Axes(
            ("heater", "HEATER", null, "ON", "OK", null), ("fan", "FAN", null, "OFF", "FAULT", "STALL"),
            ("ir", "IR_LED", null, "ON", "DEGRADED", null), ("ptz", "PTZ_UNIT", null, "IDLE", "FAULT", "COMM"));
        marker.Model.SetUpdate();
        var refreshed = vm.Tabs.Single(t => t.Tab == SymbolDetailTab.Components).ComponentTable!;
        Assert.True(refreshed.IsFaultFirst);
        Assert.Equal(new[] { "fan", "ptz", "ir", "heater" }, refreshed.Rows.Select(r => r.Key));

        // 다시 누르면 선언 순서
        refreshed.IsFaultFirst = false;
        Assert.Equal(new[] { "heater", "fan", "ir", "ptz" }, refreshed.Rows.Select(r => r.Key));
    });

    [Fact]
    [Trait("Scenario", "MAP-164")]
    public void should_bind_sort_toggle_and_row_list_automation_ids_in_detail_components_template()
    {
        // 정렬 단추 · 행 목록 id 와 바인딩(헤디드 단언점) — 상세 창 템플릿을 정적으로 읽는다
        var xaml = System.IO.File.ReadAllText(System.IO.Path.Combine(RepoPaths.GMapsUi, "Themes", "SymbolDetailStyle.xaml"));
        int at = xaml.IndexOf("x:Key=\"SdComponentTable\"", StringComparison.Ordinal);
        Assert.True(at > 0);
        var body = xaml[at..xaml.IndexOf("</DataTemplate>", at, StringComparison.Ordinal)];
        int sort = body.IndexOf("AutomationProperties.AutomationId=\"Symbol.Detail.Components.Sort\"", StringComparison.Ordinal);
        Assert.True(sort > 0, "정렬 단추 id 없음");
        var sortTag = body[body.LastIndexOf('<', sort)..body.IndexOf('>', sort)];
        Assert.Contains("IsChecked=\"{Binding IsFaultFirst, Mode=TwoWay}\"", sortTag);
        Assert.Contains("Content=\"{Binding SortText}\"", sortTag);
        int rows = body.IndexOf("AutomationProperties.AutomationId=\"Symbol.Detail.Components.Rows\"", StringComparison.Ordinal);
        Assert.True(rows > 0, "행 목록 id 없음");
        Assert.Contains("ItemsSource=\"{Binding Rows}\"", body[rows..body.IndexOf('>', rows)]);
    }
}
