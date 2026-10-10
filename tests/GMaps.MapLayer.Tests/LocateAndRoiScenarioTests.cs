using System.Windows.Input;
using GMap.NET;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapRoi;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Detail;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Views.Maps;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Monitoring.Models.Maps;
using Moq;
using ScenarioShared;
using Xunit;

namespace GMaps.MapLayer.Tests;

/// <summary>
/// 카탈로그 §1 위치 찾기(MAP-043 · 045 · 046 · 050) · 관심지역(MAP-047 · 048) 헤드리스 짝.
/// 실제 MapViewModel 처리기(LocateDevices · 해제 트리거 · 상세 액션 · 센서 행 · ROI 패널 이벤트)를 가짜 DB · 창 없는 지도로 돌린다.
/// </summary>
public class LocateAndRoiScenarioTests
{
    private static int Rings(MapVm f) => f.Map.Markers.OfType<LocateRingMarker>().Count();

    // ── MAP-043 — 고리 지우기(Esc / 빈 곳 클릭) ──

    [Trait("Scenario", "MAP-043")]
    [Fact]
    public void should_remove_locate_ring_when_escape_is_pressed_but_keep_it_for_other_keys() => Scenario.Sta(() =>
    {
        var f = new MapVm();
        f.Map.Markers.Add(MapVm.Pids(1, 12, 37.391, 126.961));
        f.Call("LocateDevices", MapLocateRequest.For("7중대", new[] { 12 }));
        Assert.Equal(1, Rings(f));

        f.Call("OnLocatePreviewKeyDown", f.Map, FakeSource.Key(Key.A));
        Assert.Equal(1, Rings(f));                                   // 다른 키는 강조를 내리지 않는다

        f.Call("OnLocatePreviewKeyDown", f.Map, FakeSource.Key(Key.Escape));
        Assert.Equal(0, Rings(f));                                   // Esc → 고리 0
    });

    [Trait("Scenario", "MAP-043")]
    [Fact]
    public void should_remove_locate_ring_on_empty_click_but_not_after_a_pan_drag() => Scenario.Sta(() =>
    {
        var f = new MapVm();
        f.Map.Markers.Add(MapVm.Pids(1, 12, 37.391, 126.961));
        f.Call("LocateDevices", MapLocateRequest.For("7중대", new[] { 12 }));

        // 팬: 누른 자리에서 데드존(8 DIU)보다 멀리 떼면 클릭이 아니다 — 고리 유지
        f.Call("OnLocatePreviewMouseDown", f.Map, new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left));
        Scenario.SetField(f.Vm, "_locatePressPoint", (System.Windows.Point?)new System.Windows.Point(400, 300));
        f.Call("OnLocatePreviewMouseUp", f.Map, new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left));
        Assert.Equal(1, Rings(f));

        // 빈 곳 클릭: 같은 자리에서 누르고 뗌 → 고리 0
        f.Call("OnLocatePreviewMouseDown", f.Map, new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left));
        f.Call("OnLocatePreviewMouseUp", f.Map, new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left));
        Assert.Equal(0, Rings(f));
    });

    // ── MAP-045 — 숨긴 층의 장비 위치 찾기 ──

    [Trait("Scenario", "MAP-045")]
    [Fact]
    public void should_count_hidden_and_draw_no_ring_when_the_kind_layer_is_hidden() => Scenario.Sta(() =>
    {
        var f = new MapVm();
        var hidden = MapVm.Pids(1, 12, 37.391, 126.961);
        hidden.IsLayerEnabled = false;                               // 레이어 패널에서 종류를 끈 상태(유효 IsLayerEnabled=false)
        f.Map.Markers.Add(hidden);
        var request = MapLocateRequest.For("7중대", new[] { 12 })!;

        f.Call("LocateDevices", request);

        Assert.Equal(0, Rings(f));
        Assert.Contains(f.Log.Lines, l => l.Contains("숨김 1") && l.Contains("강조 0"));
        var result = Assert.Single(f.Of<MapLocateResult>());
        Assert.Equal((request.RequestId, 0, 1), (result.RequestId, result.Shown, result.Hidden));
    });

    // ── MAP-046 — 상세 「지도에서 보기」 ──

    [Trait("Scenario", "MAP-046")]
    [Fact]
    public void should_center_map_on_symbol_when_detail_show_on_map_action_runs() => Scenario.Sta(() =>
    {
        var f = new MapVm();
        var marker = MapVm.Pids(1, 12, 37.4512, 127.0123);
        f.Map.Markers.Add(marker);
        Scenario.SetField(f.Vm, "_symbolDetailMarker", marker);      // 상세 창이 연 심볼(ShowSymbolDetail 이 담는 자리)

        f.Call("OnSymbolDetailAction", SymbolDetailAction.ShowOnMap);

        Assert.Equal(37.4512, f.Map.Position.Lat, 6);
        Assert.Equal(127.0123, f.Map.Position.Lng, 6);
    });

    // ── MAP-050 — 등록 센서 정보 행 「지도에서 위치 확인」 ──

    [Trait("Scenario", "MAP-050")]
    [Fact]
    public void should_center_map_on_sensor_row_and_ignore_rows_without_coordinates() => Scenario.Sta(() =>
    {
        var f = new MapVm();
        var group = new Mock<IPidsGroupEditableMarker>();
        group.SetupGet(g => g.LinkedDeviceGroup).Returns(3);
        group.SetupGet(g => g.Title).Returns("구역 3");
        f.Vm.ShowSensorInfoPanel(group.Object);                       // 오버레이 VM 생성 + LocateRequested 배선(제품 경로)
        var panel = f.Get<Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps.SensorInfoPanelViewModel>("_sensorInfoVm");
        Assert.NotNull(panel);

        panel.LocateCommand.Execute(new SensorInfoRowModel { DeviceId = 7, Latitude = 37.5011, Longitude = 127.0222 });
        Assert.Equal(37.5011, f.Map.Position.Lat, 6);
        Assert.Equal(127.0222, f.Map.Position.Lng, 6);

        panel.LocateCommand.Execute(new SensorInfoRowModel { DeviceId = 8, Latitude = 0, Longitude = 0 });   // 미좌표 = 오폭 방지
        Assert.Equal(37.5011, f.Map.Position.Lat, 6);
    });

    // ── MAP-047 — 관심지역 「이동」 ──

    [Trait("Scenario", "MAP-047")]
    [Fact]
    public void should_move_map_to_roi_position_and_half_step_zoom_when_move_is_pressed() => Scenario.Sta(() =>
    {
        var f = new MapVm();
        f.Vm.ShowMapRoiPanel();
        var panel = f.Vm.MapRoiPanel!;
        panel.SelectedRoi = new MapRoiModel { Id = 4, Title = "안양발전소", Latitude = 37.4021, Longitude = 126.9301, Zoom = 17.5 };

        panel.MoveCommand.Execute(null);

        Assert.Equal(37.4021, f.Map.Position.Lat, 6);
        Assert.Equal(126.9301, f.Map.Position.Lng, 6);
        Assert.Equal(17.5, f.Map.EffectiveZoom, 3);                  // 하프 줌까지 복원(FR-09/14)
    });

    // ── MAP-048 — 관심지역 등록 → 삭제 ──

    [Trait("Scenario", "MAP-048")]
    [Fact]
    public void should_add_roi_row_then_remove_it_after_confirm_with_db_insert_and_delete() => Scenario.StaAsync(async () =>
    {
        var f = new MapVm();
        f.Db.Setup(d => d.InsertMapRoiAsync(It.IsAny<IMapRoiModel>(), It.IsAny<CancellationToken>())).ReturnsAsync(77);
        f.Db.Setup(d => d.DeleteMapRoiAsync(77, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        f.Vm.ShowMapRoiPanel();
        var panel = f.Vm.MapRoiPanel!;

        panel.RegisterCommand.Execute(null);
        var row = Assert.Single(panel.RoiItems);                     // 목록 +1
        Assert.Equal(77, row.Id);
        Assert.Equal(f.Map.Position.Lat, row.Latitude, 9);
        f.Db.Verify(d => d.InsertMapRoiAsync(It.IsAny<IMapRoiModel>(), It.IsAny<CancellationToken>()), Times.Once);

        panel.SelectedRoi = row;
        panel.DeleteCommand.Execute(null);
        var confirm = Assert.Single(f.Of<OpenConfirmPopupMessageModel>());   // 확인 창을 거친다
        Assert.IsType<CallDeleteMapRoiProcessMessageModel>(confirm.MessageModel);
        Assert.Single(panel.RoiItems);                               // 확인 전에는 그대로

        await f.Vm.HandleAsync(new CallDeleteMapRoiProcessMessageModel(), CancellationToken.None);   // 「확인」
        Assert.Empty(panel.RoiItems);                                // 목록 −1
        f.Db.Verify(d => d.DeleteMapRoiAsync(77, It.IsAny<CancellationToken>()), Times.Once);
    });

    [Trait("Scenario", "MAP-048,MAP-066")]
    [Fact]
    public void should_block_roi_register_and_show_reason_when_map_edit_permission_is_missing() => Scenario.Sta(() =>
    {
        var f = new MapVm(canEdit: false);
        f.Vm.ShowMapRoiPanel();

        f.Vm.MapRoiPanel!.RegisterCommand.Execute(null);

        Assert.Empty(f.Vm.MapRoiPanel.RoiItems);
        f.Db.Verify(d => d.InsertMapRoiAsync(It.IsAny<IMapRoiModel>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Contains(f.Of<OpenInfoPopupMessageModel>(), m => m.Title == "권한 없음");
    });
}
