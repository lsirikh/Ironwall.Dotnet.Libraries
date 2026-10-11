using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using System.Xml.Linq;
using Caliburn.Micro;
using GMap.NET;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Detail;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Symbols3D;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Moq;
using ScenarioShared;
using Xunit;

namespace GMaps.MapAux.Tests;

/// <summary>
/// 카탈로그 §1 MAP-160(상세 열기/닫기) · MAP-161(탭) · MAP-162(3D 무대) · MAP-163(행동 띠) · MAP-165(관계도) · MAP-166(상세 "?").
/// 상세 창은 Window 가 아니라 지도 겹창 컨트롤(<see cref="SymbolDetailControl"/>)이다 — 실제 MapViewModel.ShowSymbolDetail 로 열고,
/// 실제 템플릿(SymbolDetailStyle.xaml)을 부모 없이 Measure/Arrange 해서 AutomationId 로 단추를 찾아 누른다(창 없음).
/// "?" 는 말풍선 창 대신 몸을 제자리에 두는 템플릿(<see cref="NoPopupHelp"/>)으로 열고, Esc 는 HWND 없는 입력 원천(<see cref="AuxHeadlessSource"/>)으로 보낸다.
/// </summary>
public class SymbolDetailScenarioTests
{
    private static SymbolDetailControl Templated(MapVmRig rig)
    {
        var panel = rig.Vm.SymbolDetailPanel ?? throw new InvalidOperationException("상세 겹창이 없다");
        if (panel.Template == null || panel.Style == null)
        {
            NoPopupHelp.Install(panel);
            panel.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/SymbolDetailStyle.xaml", UriKind.Relative) });
            panel.Style = (Style)panel.Resources[typeof(SymbolDetailControl)];
        }
        Scenario.Layout(panel, 720, 430);
        return panel;
    }

    private static SymbolDetailViewModel Detail(MapVmRig rig) => (SymbolDetailViewModel)rig.Vm.SymbolDetailPanel!.DataContext;

    private static List<Button> ButtonsWithIdPrefix(DependencyObject root, string prefix)
        => Parts.Descendants(root).OfType<Button>().Where(b => (AutomationProperties.GetAutomationId(b) ?? "").StartsWith(prefix, StringComparison.Ordinal)).ToList();

    private static string Id(DependencyObject e) => AutomationProperties.GetAutomationId(e);

    private static GMapPidsMarker SensorMarker(bool axes = true) => Parts.Marker(new SensorDeviceModel
    {
        Id = 61, DeviceType = EnumDeviceType.SmartSensor, DeviceName = "Sensor-A-7", DeviceNumber = 7, UnitId = 7, Status = EnumDeviceStatus.ACTIVATED, Axes = axes ? Parts.Mixed() : null,
    }, EnumDeviceType.SmartSensor, "북측 5구간");

    // ───────────────────────── MAP-160 ─────────────────────────

    [Fact]
    [Trait("Scenario", "MAP-160")]
    public void should_open_one_detail_overlay_and_close_it_with_close_button_then_reuse_it_when_detail_shown_again() => OneSta.Sta(() =>
    {
        var rig = MapVmRig.Create();
        using var marker = SensorMarker();
        Assert.False(rig.Vm.IsSymbolDetailPanelVisible);

        rig.Vm.ShowSymbolDetail(marker);
        Assert.True(rig.Vm.IsSymbolDetailPanelVisible);
        var panel = Templated(rig);
        var vm = Detail(rig);
        Assert.Equal("북측 5구간", vm.SymbolName);
        Assert.StartsWith("상세 보기 — ", vm.HeaderText);
        Assert.Contains(Parts.Descendants(panel).OfType<TextBlock>(), t => t.Text == vm.HeaderText);   // 머리 제목 줄

        var close = Parts.ById<Button>(panel, "GMaps.SymbolDetail.CloseButton");
        Parts.Press(close);
        Assert.False(rig.Vm.IsSymbolDetailPanelVisible);
        Assert.False(vm.IsUnitLineVisible);                       // 닫으면 마커 · 부대 사전 구독을 놓는다(Unload)

        // 다른 심볼로 다시 열면 같은 겹창 · 같은 VM 을 갈아 끼운다(창을 겹쳐 띄우지 않는다)
        using var other = Parts.Marker(new ControllerDeviceModel { Id = 31, DeviceType = EnumDeviceType.Controller, IpAddress = "192.168.0.31", Port = 4001 }, EnumDeviceType.Controller, "제어기-1");
        rig.Vm.ShowSymbolDetail(other);
        Assert.True(rig.Vm.IsSymbolDetailPanelVisible);
        Assert.Same(panel, rig.Vm.SymbolDetailPanel);
        Assert.Same(vm, Detail(rig));
        Assert.Equal("제어기-1", vm.SymbolName);

        // 겹창 자리 = 지도 위 Canvas 의 ContentPresenter, 표시 = IsSymbolDetailPanelVisible(MapView.xaml)
        var doc = XDocument.Load(Path.Combine(RepoPaths.GMapsUi, "Views", "Maps", "MapView.xaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var presenter = doc.Descendants().Single(e => (string?)e.Attribute(x + "Name") == "SymbolDetailPresenter");
        Assert.Contains("SymbolDetailPanel", (string?)presenter.Attribute("Content"));
        Assert.Contains("IsSymbolDetailPanelVisible", (string?)presenter.Attribute("Visibility"));
    });

    /// <summary>
    /// 결함 AUX-D5: 카탈로그 MAP-160 「상세 보기 → CloseButton/Esc → 닫힘」 중 Esc 가 상세 겹창을 닫지 않는다.
    /// SymbolDetailControl · SymbolDetailViewModel 에 Esc 처리기가 없고, 지도 Esc 경로(MapViewModel.OnMapPreviewKeyDownForAim)는 조준 · 배치 · 홈 · 영역 모드만 끈다.
    /// </summary>
    [Fact]
    [Trait("Scenario", "MAP-160")]
    [Trait("Defect", "AUX-D5")]
    public void should_close_detail_overlay_when_escape_pressed_inside_it() => OneSta.Sta(() =>
    {
        var rig = MapVmRig.Create();
        using var marker = SensorMarker();
        rig.Vm.ShowSymbolDetail(marker);
        var panel = Templated(rig);
        var source = new AuxHeadlessSource { RootVisual = panel };
        try
        {
            var target = Parts.ById<Button>(panel, "GMaps.SymbolDetail.Tab.Basic");   // 포커스가 상세 창 안에 있을 때
            source.Press(target, Key.Escape, preview: true);
            source.Press(target, Key.Escape, preview: false);
            Scenario.Call(rig.Vm, "OnMapPreviewKeyDownForAim", rig.Vm, new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent });   // 지도 Esc 경로까지
            Assert.False(rig.Vm.IsSymbolDetailPanelVisible,
                "상세 겹창에서 Esc(PreviewKeyDown · KeyDown · 지도 Esc 경로)를 눌러도 닫히지 않는다 — 닫기는 CloseButton 뿐");
        }
        finally { source.RootVisual = null!; }
    });

    // ───────────────────────── MAP-161 ─────────────────────────

    [Fact]
    [Trait("Scenario", "MAP-161")]
    public void should_show_basic_components_symbol_tabs_with_ids_and_switch_fields_when_tab_buttons_pressed() => OneSta.Sta(() =>
    {
        var rig = MapVmRig.Create();
        using var sensor = SensorMarker(axes: true);
        rig.Vm.ShowSymbolDetail(sensor);
        var panel = Templated(rig);
        var vm = Detail(rig);

        var tabs = ButtonsWithIdPrefix(panel, "GMaps.SymbolDetail.Tab.");
        Assert.Equal(new[] { "GMaps.SymbolDetail.Tab.Basic", "GMaps.SymbolDetail.Tab.Components", "GMaps.SymbolDetail.Tab.Symbol" }, tabs.Select(Id));
        Assert.Equal(new[] { "기본", "부품", "심볼" }, tabs.Select(b => b.Content as string));
        Assert.All(tabs, b => Assert.True(b.IsEnabled));
        Assert.Equal(SymbolDetailTab.Basic, vm.SelectedTab!.Tab);                      // 장비 연결 = 기본 탭부터
        Assert.Contains(vm.SelectedTab.Fields, f => f.Label == "장비명" && f.Value == "Sensor-A-7");

        Parts.Press(tabs[2]);                                                         // 「심볼」
        Assert.Equal(SymbolDetailTab.Symbol, vm.SelectedTab!.Tab);
        Assert.Contains(vm.SelectedTab.Fields, f => f.Label == "심볼 제목" && f.Value == "북측 5구간");
        Scenario.Layout(panel, 720, 430);
        var fields = Parts.ById<ItemsControl>(panel, "GMaps.SymbolDetail.Fields");
        Assert.Same(vm.SelectedTab.Fields, fields.ItemsSource);                       // 화면 목록이 고른 탭을 따른다

        // 부품 축 없는 종류(스피커 · 통문) = 기본 · 심볼 두 탭(유형별 통신/방송/상태 탭은 2026-09-09 결정으로 내림 — SymbolDetailRules.VisibleTabs)
        foreach (var (device, type) in new (IBaseDeviceModel, EnumDeviceType)[]
                 { (new SpeakerDeviceModel { Id = 51, DeviceType = EnumDeviceType.IpSpeaker }, EnumDeviceType.IpSpeaker), (new GateDeviceModel { Id = 21, DeviceType = EnumDeviceType.Gate }, EnumDeviceType.Gate) })
        {
            using var m = Parts.Marker(device, type, type.ToString());
            rig.Vm.ShowSymbolDetail(m);
            Scenario.Layout(panel, 720, 430);
            Assert.Equal(new[] { "GMaps.SymbolDetail.Tab.Basic", "GMaps.SymbolDetail.Tab.Symbol" }, ButtonsWithIdPrefix(panel, "GMaps.SymbolDetail.Tab.").Select(Id));
        }
    });

    [Fact]
    [Trait("Scenario", "MAP-161")]
    public void should_disable_device_tabs_with_reason_and_start_on_symbol_tab_when_symbol_has_no_linked_device() => OneSta.Sta(() =>
    {
        var rig = MapVmRig.Create();
        using var marker = Parts.Marker(new SensorDeviceModel { Id = 99, DeviceType = EnumDeviceType.SmartSensor }, EnumDeviceType.SmartSensor, "외곽-99");
        marker.LinkedDevice = null;                       // 연결 Id 는 있으나 장비 목록에 없음
        marker.Model.LinkedDeviceId = 99;
        Assert.Equal(99, marker.LinkedDeviceId);
        rig.Vm.ShowSymbolDetail(marker);
        var panel = Templated(rig);
        var vm = Detail(rig);

        var basic = Parts.ById<Button>(panel, "GMaps.SymbolDetail.Tab.Basic");
        var symbol = Parts.ById<Button>(panel, "GMaps.SymbolDetail.Tab.Symbol");
        Assert.False(basic.IsEnabled);
        Assert.Equal("장비 미연결", basic.ToolTip);
        Assert.Equal("장비 미연결", UIElementAutomationPeer.CreatePeerForElement(basic).GetHelpText());   // 꺼진 탭의 사유 = UIA 도움말
        Assert.True(symbol.IsEnabled);
        Assert.Equal(SymbolDetailTab.Symbol, vm.SelectedTab!.Tab);
        vm.SelectTabCommand.Execute(vm.Tabs.Single(t => t.Tab == SymbolDetailTab.Basic));   // 꺼진 탭은 우회해도 고르지 않는다
        Assert.Equal(SymbolDetailTab.Symbol, vm.SelectedTab!.Tab);
        Assert.Contains("#99", vm.NoDeviceReason);                                            // 왜 비었는지(연결 Id 는 있음)
    });

    // ───────────────────────── MAP-162 ─────────────────────────

    /// <remarks>
    /// 3D/2D 는 프로세스 전역 설정(Symbol3DFeature — appsettings)이라 시험이 바꾸지 않는다. 대신 템플릿의 실제 무대(PART_Preview)에 하우징 모델을 직접 넣어
    /// [정면] · [회전 멈춤] 단추 → VM → 무대 배선과 자동 회전 · 끌어 돌리기 · 재개 유예를 무대의 실제 처리기로 확인한다.
    /// 자동 회전 타이머(DispatcherTimer) 대신 같은 처리기(OnTick)를 부른다. 끌기는 마우스 캡처를 못 하므로 눌림 상태만 주입한다. 그림(픽셀) 변화는 헤디드 몫.
    /// </remarks>
    [Fact]
    [Trait("Scenario", "MAP-162")]
    public void should_auto_rotate_in_45_degree_steps_pause_with_toggle_reset_to_front_and_resume_after_drag_when_3d_stage_used() => OneSta.Sta(() =>
    {
        var rig = MapVmRig.Create();
        using var marker = Parts.Marker(new ControllerDeviceModel { Id = 31, DeviceType = EnumDeviceType.Controller }, EnumDeviceType.Controller, "제어기-1");
        rig.Vm.ShowSymbolDetail(marker);
        var panel = Templated(rig);
        var vm = Detail(rig);
        Assert.Equal(Ironwall.Dotnet.Libraries.GMaps.Ui.Utils.Symbol3DFeature.IsEnabled, vm.Is3D);              // 지도와 같은 3D 판정
        Assert.Equal(vm.Is3D ? "3D 심볼" : "2D 심볼 · 회전 없음", vm.KindText);

        var preview = Scenario.GetField<SymbolPreview3DControl?>(panel, "_preview") ?? throw new InvalidOperationException("PART_Preview 없음");
        preview.Visibility = Visibility.Visible;
        preview.ModelKey = HousingModels.DeviceKey(EnumDeviceType.Controller);
        Scenario.Layout(panel, 720, 430);
        Assert.True(preview.ActualWidth > 0);
        var camera = Scenario.GetField<OrthographicCamera>(preview, "_camera");
        void Tick(int n) { for (int i = 0; i < n; i++) Scenario.Call(preview, "OnTick", null, EventArgs.Empty); }

        // 자동 회전 = 2초마다 45° 한 칸(타이머 0.25 s × 8)
        Assert.Equal(0, preview.YawDeg, 6);
        var front = camera.Position;
        Tick(7);
        Assert.Equal(0, preview.YawDeg, 6);                                         // 아직 한 칸 전
        Tick(1);
        Assert.Equal(45, preview.YawDeg, 6);
        Assert.NotEqual(front, camera.Position);                                    // 카메라가 실제로 옮겨졌다
        Tick(8);
        Assert.Equal(90, preview.YawDeg, 6);

        // [회전 멈춤] → VM · 무대 끔(TwoWay) · 타이머 정지 · 단추 글
        var toggle = Parts.ById<Button>(panel, "GMaps.SymbolDetail.ToggleRotate");
        Assert.Equal("회전 멈춤", toggle.Content);
        Parts.Press(toggle);
        Assert.False(vm.IsAutoRotating);
        Assert.False(preview.IsAutoRotating);
        Assert.False(Scenario.GetField<DispatcherTimer>(preview, "_timer").IsEnabled);
        Scenario.Layout(panel, 720, 430);
        Assert.Equal("회전 시작", toggle.Content);
        Parts.Press(toggle);
        Assert.True(vm.IsAutoRotating && preview.IsAutoRotating);

        // [정면] → yaw 0 · 기본 피치 · 자동 회전 처음부터
        Parts.Press(Parts.ById<Button>(panel, "GMaps.SymbolDetail.ResetView"));
        Assert.Equal(0, preview.YawDeg, 6);
        Assert.Equal(OrbitCameraMath.DefaultPitchDeg, preview.PitchDeg, 6);
        Assert.Equal(front, camera.Position);
        Tick(7);
        Assert.Equal(0, preview.YawDeg, 6);                                         // 기준 시각도 처음부터

        // 끌어 돌리기 — 오른쪽으로 60 px 끈 것과 같은 눌림 상태(데드존 8 px 통과)
        var cur = Mouse.PrimaryDevice.GetPosition(preview);
        Scenario.SetField(preview, "_pressed", true);
        Scenario.SetField(preview, "_pressPoint", new Point(cur.X - 60, cur.Y));
        Scenario.SetField(preview, "_dragStartYaw", preview.YawDeg);
        Scenario.SetField(preview, "_dragStartPitch", preview.PitchDeg);
        preview.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = UIElement.MouseMoveEvent });
        var dragged = OrbitCameraMath.ApplyDrag(0, OrbitCameraMath.DefaultPitchDeg, -60, 0).YawDeg;   // 화면 오른쪽 = 카메라는 반대
        Assert.Equal(dragged, preview.YawDeg, 6);
        Tick(16);
        Assert.Equal(dragged, preview.YawDeg, 6);                                   // 끄는 동안 자동 회전이 끼어들지 않는다

        // 놓기 → 1.5 s 유예 뒤 놓은 자리에서 이어 돈다
        preview.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = UIElement.LostMouseCaptureEvent });
        Tick(6);                                                                    // 유예 1.5 s
        Assert.Equal(dragged, preview.YawDeg, 6);
        Tick(8);
        Assert.Equal(OrbitCameraMath.Normalize360(dragged + 45), preview.YawDeg, 6);
    });

    // ───────────────────────── MAP-163 ─────────────────────────

    private static Dictionary<string, Button> Actions(SymbolDetailControl panel)
        => ButtonsWithIdPrefix(panel, "GMaps.SymbolDetail.Action.").ToDictionary(b => Id(b)["GMaps.SymbolDetail.Action.".Length..]);

    [Fact]
    [Trait("Scenario", "MAP-163")]
    public void should_show_type_specific_actions_with_enable_rules_and_reason_help_text_when_detail_opened_for_sensor_speaker_gate() => OneSta.Sta(() =>
    {
        var rig = MapVmRig.Create(webServer: false);
        using var sensor = SensorMarker();
        rig.Vm.ShowSymbolDetail(sensor);
        var panel = Templated(rig);

        var a = Actions(panel);
        Assert.Equal(new[] { "DevicePage", "DeviceDetail", "DeviceEdit", "DetectionHistory", "ShowOnMap" }, a.Keys);
        Assert.False(a["DevicePage"].IsEnabled);
        Assert.Equal("웹서버 연동 꺼짐", UIElementAutomationPeer.CreatePeerForElement(a["DevicePage"]).GetHelpText());
        Assert.True(a["DetectionHistory"].IsEnabled);                               // events:view 권한 있음
        Assert.True(a["ShowOnMap"].IsEnabled);                                      // 지도에서 보기 = 항상
        Assert.Equal("", UIElementAutomationPeer.CreatePeerForElement(a["ShowOnMap"]).GetHelpText());

        // 켜진 행동은 컨텍스트 메뉴와 같은 메서드로 간다
        rig.Map.Position = new PointLatLng(37.6, 127.2);
        Parts.Press(a["ShowOnMap"]);
        Assert.Equal(37.5, rig.Map.Position.Lat, 6);                                // 그 심볼로 지도 이동
        Parts.Press(a["DetectionHistory"]);
        Assert.Equal(61, Assert.Single(rig.Sink.Messages.OfType<OpenDetectionHistoryDialogMessageModel>()).DeviceId);

        // 스피커 — 음원 · TTS · 방송 정지 · 마이크(규격 미확정이면 꺼짐)
        rig.Broadcast.SetupGet(b => b.IsMicCommandSupported).Returns(false);
        using var speaker = Parts.Marker(new SpeakerDeviceModel { Id = 51, DeviceType = EnumDeviceType.IpSpeaker }, EnumDeviceType.IpSpeaker, "SPK-3");
        rig.Vm.ShowSymbolDetail(speaker);
        Scenario.Layout(panel, 720, 430);
        a = Actions(panel);
        Assert.Equal(new[] { "DevicePage", "DeviceDetail", "DeviceEdit", "SoundPlay", "Tts", "BroadcastStop", "MicPtt", "ShowOnMap" }, a.Keys);
        Assert.True(a["SoundPlay"].IsEnabled);
        Assert.False(a["MicPtt"].IsEnabled);
        Assert.Equal("마이크 방송 규격 미확정", a["MicPtt"].ToolTip);
        Parts.Press(a["SoundPlay"]);
        Assert.True(rig.Vm.IsBroadcastPlayPanelVisible);
        Assert.Equal(51, rig.Vm.BroadcastPlayPanel!.LinkedDeviceId);

        // 통문 — 문 열기/닫기: 문 상태에 따라(닫힘이면 열기만, 열기 이미 그 상태면 사유)
        using var gate = Parts.Marker(new GateDeviceModel { Id = 21, DeviceType = EnumDeviceType.Gate }, EnumDeviceType.Gate, "북문", EnumDoorState.Closed);
        rig.Vm.ShowSymbolDetail(gate);
        Scenario.Layout(panel, 720, 430);
        a = Actions(panel);
        Assert.Equal(new[] { "DevicePage", "DeviceDetail", "DeviceEdit", "DoorOpen", "DoorClose", "ShowOnMap" }, a.Keys);
        Assert.True(a["DoorOpen"].IsEnabled);
        Assert.False(a["DoorClose"].IsEnabled);
        Assert.Equal("이미 그 상태", a["DoorClose"].ToolTip);

        // 권한이 빠지면 개폐 · 방송이 사유와 함께 꺼진다(같은 규칙)
        rig.Permission.Setup(p => p.CanControl(It.IsAny<string>())).Returns(false);
        rig.Vm.ShowSymbolDetail(gate);
        Scenario.Layout(panel, 720, 430);
        Assert.Equal("권한 없음(devices:control)", Actions(panel)["DoorOpen"].ToolTip);
        Assert.False(Actions(panel)["DoorOpen"].IsEnabled);
    });

    [Fact]
    [Trait("Scenario", "MAP-163")]
    public void should_disable_every_device_action_except_page_and_show_on_map_when_symbol_unlinked() => OneSta.Sta(() =>
    {
        var rig = MapVmRig.Create(webServer: true);
        using var marker = Parts.Marker(new CameraDeviceModel { Id = 41, DeviceType = EnumDeviceType.IpCamera }, EnumDeviceType.IpCamera, "CAM-?");
        marker.LinkedDevice = null;
        rig.Vm.ShowSymbolDetail(marker);
        var a = Actions(Templated(rig));
        Assert.Equal(new[] { "DevicePage", "DeviceDetail", "DeviceEdit", "CameraHome", "AimLocation", "ShowOnMap" }, a.Keys);
        Assert.True(a["DevicePage"].IsEnabled);
        Assert.True(a["ShowOnMap"].IsEnabled);
        foreach (var k in new[] { "DeviceDetail", "DeviceEdit", "CameraHome", "AimLocation" })
        {
            Assert.False(a[k].IsEnabled, $"{k} 가 미연결인데 켜져 있다");
            Assert.Equal("장비 미연결", a[k].ToolTip);
        }
    });

    // ───────────────────────── MAP-165 ─────────────────────────

    /// <remarks>헤디드 몫: 부대 창이 실제로 열려 관계도 레일에서 그 부대가 선택되는 것(받는 쪽 UnitConsoleLauncher = Devices.Ui).</remarks>
    [Fact]
    [Trait("Scenario", "MAP-165")]
    public void should_request_unit_map_and_show_block_reason_until_launcher_reports_shown_when_open_unit_map_pressed() => OneSta.Sta(() =>
    {
        var rig = MapVmRig.Create();
        using var marker = SensorMarker();
        rig.Vm.ShowSymbolDetail(marker);
        var panel = Templated(rig);
        var vm = Detail(rig);
        Assert.True(vm.IsUnitLineVisible);
        Assert.Equal("소속 부대 7중대 · 2대대 › 1연대", vm.UnitLineText);

        var open = Parts.ById<Button>(panel, "GMaps.SymbolDetail.OpenUnitMap");
        Assert.True(open.IsEnabled);
        Parts.Press(open);
        var request = Assert.Single(rig.Sink.Messages.OfType<OpenUnitConsoleRequest>());
        Assert.Equal(7, request.UnitId);
        Assert.True(request.OpenMap);                                              // 관계도 레일로

        var ea = Scenario.GetField<Caliburn.Micro.IEventAggregator>(rig.Vm, "_eventAggregator");
        ea.PublishOnCurrentThreadAsync(new OpenUnitConsoleResult(7, OpenUnitConsoleOutcome.BlockedByUnsavedEdit)).GetAwaiter().GetResult();
        Assert.Equal(SymbolDetailUnitLine.BlockedByUnsavedEditText, vm.UnitNoticeText);   // 막힘 사유(말없는 실패 0)
        Scenario.Layout(panel, 720, 430);
        var notice = Parts.Descendants(panel).OfType<TextBlock>().Single(t => t.Text == SymbolDetailUnitLine.BlockedByUnsavedEditText);
        Assert.Equal(Visibility.Visible, notice.Visibility);

        ea.PublishOnCurrentThreadAsync(new OpenUnitConsoleResult(8, OpenUnitConsoleOutcome.Unavailable)).GetAwaiter().GetResult();
        Assert.Equal(SymbolDetailUnitLine.BlockedByUnsavedEditText, vm.UnitNoticeText);   // 다른 부대 회신은 무시
        ea.PublishOnCurrentThreadAsync(new OpenUnitConsoleResult(7, OpenUnitConsoleOutcome.Shown)).GetAwaiter().GetResult();
        Assert.Null(vm.UnitNoticeText);                                            // 편집 정리 뒤 선택됨 → 안내 걷힘
        Assert.False(vm.HasUnitNotice);

        // 다시 눌렀는데 편제에서 사라졌다고 회신
        Parts.Press(open);
        ea.PublishOnCurrentThreadAsync(new OpenUnitConsoleResult(7, OpenUnitConsoleOutcome.Unavailable)).GetAwaiter().GetResult();
        Assert.Equal(SymbolDetailUnitLine.UnavailableText, vm.UnitNoticeText);
    });

    [Fact]
    [Trait("Scenario", "MAP-165")]
    public void should_disable_open_unit_map_for_unknown_unit_and_hide_line_when_unit_directory_unavailable() => OneSta.Sta(() =>
    {
        var rig = MapVmRig.Create();
        using var marker = Parts.Marker(new SensorDeviceModel { Id = 62, DeviceType = EnumDeviceType.SmartSensor, UnitId = 99 }, EnumDeviceType.SmartSensor, "외곽-02");
        rig.Vm.ShowSymbolDetail(marker);
        var panel = Templated(rig);
        var vm = Detail(rig);
        vm.PendingUnitLoad.GetAwaiter().GetResult();                               // 사전 적재 한 번 기다린 뒤
        Assert.Equal(SymbolDetailUnitLine.NoUnitText, vm.UnitLineText);           // 이름을 지어내지 않는다
        Scenario.Layout(panel, 720, 430);
        var open = Parts.ById<Button>(panel, "GMaps.SymbolDetail.OpenUnitMap");
        Assert.False(open.IsEnabled);
        Parts.Press(open);                                                          // 꺼진 단추 우회 → 명령이 막는다
        Assert.Empty(rig.Sink.Messages.OfType<OpenUnitConsoleRequest>());

        rig.Units.SetupGet(d => d.IsAvailable).Returns(false);                      // 서버 8.0 미만 등
        rig.Vm.ShowSymbolDetail(marker);
        Assert.False(vm.IsUnitLineVisible);
    });

    // ───────────────────────── MAP-166 ─────────────────────────

    [Fact]
    [Trait("Scenario", "MAP-166")]
    public void should_close_only_the_detail_callout_and_keep_detail_overlay_when_escape_pressed_with_question_mark_open() => OneSta.Sta(() =>
    {
        var rig = MapVmRig.Create();
        using var marker = SensorMarker();
        rig.Vm.ShowSymbolDetail(marker);
        var panel = Templated(rig);
        var source = new AuxHeadlessSource { RootVisual = panel };
        try
        {
            var tip = Parts.Descendants(panel).OfType<HelpTip>().Single();
            Assert.Equal("Map.SymbolDetail.Window", tip.HelpKey);
            Assert.Equal("Help.Map.SymbolDetail.Window", Id(tip));
            Assert.True(tip.IsAvailable);
            Assert.Same(panel, tip.Scope);                                         // 창 전체 범위의 "?"

            tip.Open();
            Assert.True(tip.IsChecked);
            Assert.Same(tip, HelpTip.Current);
            var body = tip.Callout!;
            Assert.Equal("Help.Map.SymbolDetail.Window.Body", Id(body));
            Assert.Equal("상세 보기", body.Entry!.Title);
            Assert.Contains(body.Entry.Sections.SelectMany(s => s.Items), i => i.Contains("[정면]"));

            Assert.True(source.Press(tip, Key.Escape));                             // 열린 말풍선만 닫고 Esc 를 먹는다
            Assert.NotEqual(true, tip.IsChecked);
            Assert.Null(HelpTip.Current);
            Assert.True(rig.Vm.IsSymbolDetailPanelVisible);                         // 상세 겹창은 그대로
            Assert.False(source.Press(tip, Key.Escape));                            // 닫혀 있으면 Esc 를 가로채지 않는다
        }
        finally { HelpTip.CloseCurrent(); source.RootVisual = null!; }
    });
}
