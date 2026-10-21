using System.Windows;
using System.Windows.Controls;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Ui.Managers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Detail;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using ScenarioShared;
using Xunit;

namespace GMaps.MapAux.Tests;

/// <summary>
/// 카탈로그 §1 MAP-153(우클릭 · 운영) · MAP-154(우클릭 · 편집 모드) · MAP-157(우클릭 · 외부 브라우저) · MAP-168(센서 정보 · 그룹 메뉴).
/// 실제 <c>MapViewModel.ShowMarkerContextMenu</c> 를 부르고 만들어진 메뉴를 <see cref="MenuTrap"/> 로 열지 않고 잡아 항목 · 켜짐 · Click 결과를 본다(창 없음).
/// 웹 페이지 · 홈페이지는 제품의 URL 생성기(<see cref="DeviceDetailUrlService.BuildUrl"/>)를 그대로 쓰고, 브라우저를 여는 자리(<see cref="IDeviceDetailUrlService.OpenInChrome"/>)만 가짜로 받아 URL 을 단언한다.
/// </summary>
public class MarkerMenuOpsScenarioTests
{
    private static readonly string[] ZOrder = { "맨 위로", "한 칸 위로", "한 칸 아래로", "맨 아래로" };

    private static SensorDeviceModel Sensor() => new()
    {
        Id = 61, DeviceType = EnumDeviceType.SmartSensor, DeviceName = "Sensor-A-7", DeviceNumber = 7, UnitId = 7, Status = EnumDeviceStatus.ACTIVATED, Axes = Parts.Mixed(),
    };

    private static GMapPidsMarker SensorMarker() => Parts.Marker(Sensor(), EnumDeviceType.SmartSensor, "북측 5구간");

    // ───────────────────────── MAP-153 ─────────────────────────

    [Fact]
    [Trait("Scenario", "MAP-153")]
    public void should_list_detail_components_unit_device_pages_and_history_in_order_when_sensor_symbol_right_clicked_in_operation_mode() => OneSta.Sta(() =>
    {
        var rig = MapVmRig.Create(editMode: false);
        using var marker = SensorMarker();

        var menu = rig.RightClick(marker);
        Assert.Equal(new[]
        {
            "상세 보기", "상세 보기 › 부품", "소속 부대 7중대 · 2대대 › 1연대  ›  관계도에서 보기",
            "감지센서페이지", "감지센서상세", "감지센서수정", "탐지 이력",
        }, MenuTrap.Headers(menu));
        Assert.Equal(FontWeights.Bold, MenuTrap.Item(menu, "상세 보기").FontWeight);           // 맨 위 굵게
        Assert.All(menu.Items.OfType<MenuItem>(), i => Assert.True(i.IsEnabled, $"{i.Header} 가 꺼져 있다"));
        Assert.DoesNotContain(MenuTrap.Headers(menu), h => ZOrder.Contains(h));                // 운영 모드 = 순서 항목 없음
        Assert.False(menu.IsOpen && Scenario.GetField<System.Windows.Controls.Primitives.Popup?>(menu, "_parentPopup")?.IsOpen == true);

        // 「상세 보기」 → 상세 창(겹창) 열림 · 그 심볼
        Parts.Press(MenuTrap.Item(menu, "상세 보기"));
        Assert.True(rig.Vm.IsSymbolDetailPanelVisible);
        var detail = Assert.IsType<SymbolDetailViewModel>(rig.Vm.SymbolDetailPanel!.DataContext);
        Assert.Equal("북측 5구간", detail.SymbolName);
        Assert.Equal(SymbolDetailTab.Basic, detail.SelectedTab!.Tab);

        // 「상세 보기 › 부품」 → 같은 창 · 부품 탭으로
        rig.Vm.HideSymbolDetail();
        Parts.Press(MenuTrap.Item(rig.RightClick(marker), "상세 보기 › 부품"));
        Assert.True(rig.Vm.IsSymbolDetailPanelVisible);
        Assert.Equal(SymbolDetailTab.Components, detail.SelectedTab!.Tab);
        Assert.Equal("GMaps.Symbol.ContextMenu.DetailComponents", System.Windows.Automation.AutomationProperties.GetAutomationId(MenuTrap.Item(menu, "상세 보기 › 부품")));

        // 「탐지 이력」 → 이력 창 열기 요청(그 장비)
        Parts.Press(MenuTrap.Item(menu, "탐지 이력"));
        var history = Assert.Single(rig.Sink.Messages.OfType<OpenDetectionHistoryDialogMessageModel>());
        Assert.Equal(61, history.DeviceId);
        Assert.Equal("Sensor-A-7", history.DeviceName);
    });

    [Fact]
    [Trait("Scenario", "MAP-153")]
    public void should_add_home_page_aim_and_sound_items_by_type_and_open_their_panels_when_controller_camera_speaker_right_clicked() => OneSta.Sta(() =>
    {
        var rig = MapVmRig.Create(editMode: false);
        Scenario.SetField(rig.Vm, "_cameraAimControlService", Mock.Of<ICameraAimControlService>(MockBehavior.Strict));

        using var controller = Parts.Marker(new ControllerDeviceModel { Id = 31, DeviceType = EnumDeviceType.Controller, IpAddress = "192.168.0.31", Port = 4001 }, EnumDeviceType.Controller, "제어기-1");
        var cm = rig.RightClick(controller);
        Assert.Equal(new[] { "상세 보기", SymbolDetailUnitLine.NoUnitText, "제어기페이지", "제어기상세", "제어기수정", "제어기 홈페이지" }, MenuTrap.Headers(cm));
        Assert.False(MenuTrap.Item(cm, SymbolDetailUnitLine.NoUnitText).IsEnabled);   // 소속 부대 없음 = 누를 수 없는 줄
        Assert.True(MenuTrap.Item(cm, "제어기 홈페이지").IsEnabled);

        using var camera = Parts.Marker(new CameraDeviceModel { Id = 41, DeviceType = EnumDeviceType.IpCamera, IpAddress = "192.168.0.41", IpPort = 80, Category = EnumCameraType.PTZ }, EnumDeviceType.IpCamera, "PTZ-07");
        camera.DetectionRange = 120;
        var am = rig.RightClick(camera);
        Assert.Equal(new[] { "상세 보기", SymbolDetailUnitLine.NoUnitText, "감시카메라페이지", "감시카메라상세", "감시카메라수정", "카메라 홈페이지", "특정 위치 확인" }, MenuTrap.Headers(am));
        Parts.Press(MenuTrap.Item(am, "특정 위치 확인"));
        Assert.True(rig.Map.IsTargetAimMode);                                    // 조준 모드 진입(발행은 지도 클릭 뒤 — 여기서는 없음)
        Assert.StartsWith("특정 위치 확인 — 탐지범위 120m", rig.Vm.AimStatusMessage);
        rig.Map.IsTargetAimMode = false;

        using var speaker = Parts.Marker(new SpeakerDeviceModel { Id = 51, DeviceType = EnumDeviceType.IpSpeaker }, EnumDeviceType.IpSpeaker, "SPK-3");
        var sm = rig.RightClick(speaker);
        Assert.Equal(new[] { "상세 보기", SymbolDetailUnitLine.NoUnitText, "스피커페이지", "스피커상세", "스피커수정", "음원 실행", "TTS 실행", "방송 정지" }, MenuTrap.Headers(sm));
        Parts.Press(MenuTrap.Item(sm, "음원 실행"));
        Assert.True(rig.Vm.IsBroadcastPlayPanelVisible);
        Assert.Equal(51, rig.Vm.BroadcastPlayPanel!.LinkedDeviceId);
        Parts.Press(MenuTrap.Item(sm, "TTS 실행"));
        Assert.True(rig.Vm.IsTtsBroadcastPanelVisible);
        Assert.Equal(51, rig.Vm.TtsBroadcastPanel!.LinkedDeviceId);
        rig.Broadcast.Verify(b => b.PublishPlayAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never());   // 패널만 연다 — 전송 없음
        rig.Broadcast.Verify(b => b.PublishTtsAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never());
        rig.Broadcast.Verify(b => b.PublishPlayAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never());   // 음량 겹(8.0.15)도 전송 없음
        rig.Broadcast.Verify(b => b.PublishTtsAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never());
    });

    [Fact]
    [Trait("Scenario", "MAP-153")]
    public void should_keep_device_page_items_visible_but_disabled_and_hide_menu_for_locked_symbol_when_web_server_off() => OneSta.Sta(() =>
    {
        var rig = MapVmRig.Create(editMode: false, webServer: false);
        using var marker = SensorMarker();
        var menu = rig.RightClick(marker);
        foreach (var h in new[] { "감지센서페이지", "감지센서상세", "감지센서수정" })
            Assert.False(MenuTrap.Item(menu, h).IsEnabled, $"웹서버 꺼짐인데 {h} 가 켜져 있다");   // 항상 보이되 꺼짐(disable 모델)
        Assert.True(MenuTrap.Item(menu, "탐지 이력").IsEnabled);                                  // 이력은 웹서버와 무관
        Assert.True(MenuTrap.Item(menu, "상세 보기").IsEnabled);

        // 잠긴 심볼 = 메뉴 자체가 없다(ShowMarkerContextMenu 조기 반환 — 덫이 메뉴를 받지 못한다)
        marker.Model.IsLocked = true;
        var ex = Assert.Throws<InvalidOperationException>(() => rig.RightClick(marker));
        Assert.Contains("메뉴가 만들어지지 않았다", ex.Message);
    });

    // ───────────────────────── MAP-154 ─────────────────────────

    [Fact]
    [Trait("Scenario", "MAP-154")]
    public void should_omit_detail_and_unit_line_and_append_order_items_when_symbol_right_clicked_in_edit_mode() => OneSta.Sta(() =>
    {
        var rig = MapVmRig.Create(editMode: true);
        using var marker = SensorMarker();

        var menu = rig.RightClick(marker);
        var headers = MenuTrap.Headers(menu);
        Assert.DoesNotContain("상세 보기", headers);
        Assert.DoesNotContain("상세 보기 › 부품", headers);
        Assert.DoesNotContain(headers, h => h.StartsWith(SymbolDetailUnitLine.Prefix, StringComparison.Ordinal));   // 부대 줄 없음
        Assert.Equal(new[] { "감지센서페이지", "감지센서상세", "감지센서수정", "탐지 이력" }.Concat(ZOrder), headers);
        Assert.IsType<Separator>(menu.Items[4]);                                                                 // 장비 항목 | 순서 항목
        rig.Units.Verify(d => d.Describe(It.IsAny<int>()), Times.Never());                                         // 편집 모드는 부대 사전을 보지도 않는다

        // 운영 모드로 돌아가면 다시 상세 보기 · 부대 줄
        Scenario.SetField(rig.Vm, "_isEditModeEnabled", false);
        var op = MenuTrap.Headers(rig.RightClick(marker));
        Assert.Equal("상세 보기", op[0]);
        Assert.Contains(op, h => h.StartsWith(SymbolDetailUnitLine.Prefix, StringComparison.Ordinal));
        Assert.DoesNotContain(op, h => ZOrder.Contains(h));
    });

    // ───────────────────────── MAP-157 ─────────────────────────

    /// <remarks>관찰 몫: 실제 Chrome 프로세스 시작(<c>DeviceDetailUrlService.OpenInChrome</c> 의 Process.Start)은 시험에서 띄우지 않는다 — 여는 자리까지 같은 URL 이 오는지만 본다.</remarks>
    [Fact]
    [Trait("Scenario", "MAP-157")]
    public void should_hand_device_page_and_home_page_urls_to_browser_opener_when_external_menu_items_clicked() => OneSta.Sta(() =>
    {
        var rig = MapVmRig.Create(editMode: false);
        const string Base = "https://10.20.30.40:8443/ssw-svms?node=svms-device-sensor&device=PIDS_SENSOR";
        using var sensor = SensorMarker();
        var menu = rig.RightClick(sensor);

        Parts.Press(MenuTrap.Item(menu, "감지센서페이지"));
        Parts.Press(MenuTrap.Item(menu, "감지센서상세"));
        Parts.Press(MenuTrap.Item(menu, "감지센서수정"));
        Assert.Equal(new[] { Base, Base + "&panel=detail&panelId=61", Base + "&panel=edit&panelId=61" }, rig.OpenedUrls);

        using var controller = Parts.Marker(new ControllerDeviceModel { Id = 31, DeviceType = EnumDeviceType.Controller, IpAddress = "192.168.0.31", Port = 4001 }, EnumDeviceType.Controller, "제어기-1");
        Parts.Press(MenuTrap.Item(rig.RightClick(controller), "제어기 홈페이지"));
        using var camera = Parts.Marker(new CameraDeviceModel { Id = 41, DeviceType = EnumDeviceType.IpCamera, IpAddress = "192.168.0.41", IpPort = 8080, Category = EnumCameraType.FIXED }, EnumDeviceType.IpCamera, "CAM-1");
        var cam = rig.RightClick(camera);
        Assert.DoesNotContain("특정 위치 확인", MenuTrap.Headers(cam));   // 고정 카메라 = 조준 없음
        Parts.Press(MenuTrap.Item(cam, "카메라 홈페이지"));
        Assert.Equal(new[] { "http://192.168.0.31:4001", "http://192.168.0.41:8080" }, rig.OpenedUrls.Skip(3));

        // 같은 동작을 상세 창 행동 띠에서 눌러도 같은 여는 자리 · 같은 URL(단일 출처, FR-21)
        rig.Vm.ShowSymbolDetail(controller);
        var detail = (SymbolDetailViewModel)rig.Vm.SymbolDetailPanel!.DataContext;
        var home = detail.Actions.Single(a => a.Action == SymbolDetailAction.ControllerHome);
        Assert.True(home.IsEnabled);
        home.Command.Execute(home);
        Assert.Equal("http://192.168.0.31:4001", rig.OpenedUrls[^1]);
        rig.Urls.Verify(u => u.OpenInChrome(It.IsAny<string>()), Times.Exactly(6));
    });

    // ───────────────────────── MAP-168 ─────────────────────────

    private static (MapVmRig rig, GMapPidsGroupMarker group) GroupRig(int linkedGroup = 7, bool withMembers = true)
    {
        var rig = MapVmRig.Create(editMode: false);
        if (withMembers)
        {
            rig.Devices.Add(new SensorDeviceModel { Id = 82, DeviceType = EnumDeviceType.Fence, DeviceName = "FN-02", DeviceNumber = 2, DeviceGroups = new List<int> { 7 } });
            rig.Devices.Add(new SensorDeviceModel { Id = 81, DeviceType = EnumDeviceType.Fence, DeviceName = "FN-01", DeviceNumber = 1, DeviceGroups = new List<int> { 7 } });
        }
        rig.Devices.Add(new SensorDeviceModel { Id = 89, DeviceType = EnumDeviceType.Fence, DeviceName = "다른 구역", DeviceNumber = 3, DeviceGroups = new List<int> { 8 } });
        var eqm = new Mock<IEventQueueManager>();
        eqm.Setup(q => q.GetDeviceState(It.IsAny<int>(), It.IsAny<EnumDeviceType>())).Returns(EnumCompositeEventStatus.Normal);
        Scenario.SetField(rig.Vm, "_eventQueueManager", eqm.Object);
        var group = new GMapPidsGroupMarker(Mock.Of<ILogService>(), new PidsGroupSymbolModel { Title = "북측 구역", LinkedDeviceGroup = linkedGroup, Latitude = 37.5, Longitude = 127.0 });
        return (rig, group);
    }

    /// <remarks>헤디드 몫: 그룹 이력 창 자체는 호스트(ConductorControl)가 메시지를 받아 연다.</remarks>
    [Fact]
    [Trait("Scenario", "MAP-168")]
    public void should_open_sensor_info_overlay_and_request_group_history_when_pidsgroup_menu_items_clicked() => OneSta.Sta(() =>
    {
        var (rig, group) = GroupRig();
        var menu = rig.RightClick(group);
        Assert.Equal(new[] { "구역 — 북측 구역", "등록 센서 정보", "그룹 탐지 이력" }, MenuTrap.Headers(menu));
        var caption = MenuTrap.Item(menu, "구역 — 북측 구역");
        Assert.False(caption.IsEnabled);                                   // 누를 수 없는 캡션 줄
        Assert.Equal(FontWeights.Bold, caption.FontWeight);
        Assert.True(MenuTrap.Item(menu, "등록 센서 정보").IsEnabled);
        Assert.True(MenuTrap.Item(menu, "그룹 탐지 이력").IsEnabled);
        Assert.DoesNotContain(MenuTrap.Headers(menu), h => h == "상세 보기");   // 구역은 장비 상세가 없다

        Parts.Press(MenuTrap.Item(menu, "등록 센서 정보"));
        Assert.True(rig.Vm.IsSensorInfoPanelVisible);                     // 겹창
        var info = Assert.IsType<Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps.SensorInfoPanelViewModel>(rig.Vm.SensorInfoPanel!.DataContext);
        Assert.Equal(new[] { 81, 82 }, info.Rows.Select(r => r.DeviceId));   // 그 구역 센서만 · 번호 순

        Parts.Press(MenuTrap.Item(menu, "그룹 탐지 이력"));
        var request = Assert.Single(rig.Sink.Messages.OfType<OpenGroupDetectionHistoryDialogMessageModel>());
        Assert.Equal(7, request.GroupId);
        Assert.Equal("북측 구역", request.GroupName);                     // 그룹 이름 사전이 없으면 심볼 제목
    });

    [Theory]
    [InlineData(0, true, "연결된 장비그룹이 없습니다", "연결된 장비그룹이 없습니다")]
    [InlineData(7, false, null, "등록된 센서가 없습니다")]
    [Trait("Scenario", "MAP-168")]
    public void should_disable_group_items_with_reason_tooltip_when_group_unlinked_or_has_no_sensors(int linkedGroup, bool withMembers, string? infoTip, string historyTip) => OneSta.Sta(() =>
    {
        var (rig, group) = GroupRig(linkedGroup, withMembers);
        var menu = rig.RightClick(group);
        var sensorInfo = MenuTrap.Item(menu, "등록 센서 정보");
        var history = MenuTrap.Item(menu, "그룹 탐지 이력");
        Assert.Equal(linkedGroup > 0, sensorInfo.IsEnabled);
        Assert.Equal(infoTip, sensorInfo.ToolTip as string);
        Assert.False(history.IsEnabled);
        Assert.Equal(historyTip, history.ToolTip as string);
        Assert.True(ToolTipService.GetShowOnDisabled(history));            // 꺼진 항목에도 사유가 보인다
        Assert.Empty(rig.Sink.Messages.OfType<OpenGroupDetectionHistoryDialogMessageModel>());
    });
}
