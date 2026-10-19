using System.Reflection;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using Newtonsoft.Json.Linq;
using ScenarioShared;
using Xunit;

namespace GMaps.MapAux.Tests;

/// <summary>
/// 3D 위 우클릭 메뉴 자리(영역 C 전수 시나리오 C-19, 2026-10-19) — 3D 의 pick context(화면 좌표 포함)가 실제 지도 우클릭 통지 →
/// <c>ShowMarkerContextMenu</c> 로 가서 메뉴를 마우스 자리(MousePoint)가 아니라 pick 좌표(지도 기준 Relative)에 놓는지.
/// 3D 호스트(Map3DViewHost — WebView2 는 만들지 않는다)를 지도와 같은 칸에 두어 좌표 변환이 실제로 돈다. 메뉴는 <see cref="MenuTrap"/> 로 열지 않고 잡는다.
/// </summary>
public class Map3DContextMenuScenarioTests
{
    private static GMapPidsMarker Sensor(int id)
    {
        var device = new SensorDeviceModel { Id = 61, DeviceType = EnumDeviceType.SmartSensor, DeviceName = "Sensor-A-7", DeviceNumber = 7, UnitId = 7, Status = EnumDeviceStatus.ACTIVATED };
        var marker = new GMapPidsMarker(Mock.Of<ILogService>(), new PidsSymbolModel
        {
            Id = id, Title = "북측 5구간", DeviceType = EnumDeviceType.SmartSensor, Latitude = 37.5, Longitude = 127.0, LinkedDeviceId = device.Id, Zoom = 1,
        });
        marker.Width = 60; marker.Height = 60; marker.IsLayerEnabled = true; marker.IsVisible = true;
        marker.LinkedDevice = device;
        return marker;
    }

    private static void SetShown(MapViewModel vm, bool shown)
    {
        var field = typeof(MapViewModel).GetField("_map3DState", BindingFlags.Instance | BindingFlags.NonPublic)!;
        field.SetValue(vm, Enum.Parse(field.FieldType, shown ? "Shown" : "Off"));
    }

    private static void PickContext(MapViewModel vm, int id, double x, double y)
    {
        var msg = Map3DMath.ParseMessage(new JObject { ["type"] = "pick", ["action"] = "context", ["kind"] = "pids", ["id"] = id,
                                                       ["screen"] = new JObject { ["x"] = x, ["y"] = y } }.ToString());
        Assert.NotNull(msg?.Pick);
        Scenario.Call(vm, "OnMap3DMessage", msg);
    }

    [Fact]
    public void should_place_menu_at_pick_point_relative_to_map_when_marker_right_clicked_in_3d() => OneSta.Sta(() =>
    {
        var rig = MapVmRig.Create(editMode: false);
        var infos = new List<string>();
        var log = new Mock<ILogService>();
        log.Setup(l => l.Info(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
           .Callback<string, string, string, int>((m, _, _, _) => infos.Add(m));
        Scenario.SetField(rig.Vm, "_log", log.Object);

        // 지도 칸 = 지도 + 그 위 3D 호스트(MapView.xaml 과 같은 겹침) — pick 화면 좌표(3D 호스트 DIP)가 지도 좌표로 바뀐다
        var host = rig.Vm.Map3DHost;
        rig.Deco.Child = null;
        var cell = new Grid();
        cell.Children.Add(rig.Map);
        cell.Children.Add(host);
        rig.Deco.Child = cell;
        Scenario.Layout(rig.Deco, 900, 600);
        Assert.Equal("none", host.HostMode);

        using var marker = Sensor(5);
        rig.Map.Markers.Add(marker);
        rig.Map.OnMarkerRightClicked += m => Scenario.Call(rig.Vm, "OnMapMarkerRightClicked", m);   // 제품 배선(SetupAdornerIntegration)과 같은 처리기

        // 3D 가 아니면 pick 은 무시 — 메뉴가 만들어지지 않는다
        Assert.Throws<InvalidOperationException>(() => MenuTrap.Capture(() => PickContext(rig.Vm, 5, 320, 210)));

        SetShown(rig.Vm, true);
        var menu = MenuTrap.Capture(() => PickContext(rig.Vm, 5, 320, 210));
        Assert.Equal(PlacementMode.Relative, menu.Placement);
        Assert.Same(rig.Map, menu.PlacementTarget);
        Assert.Equal(320, menu.HorizontalOffset, 3);
        Assert.Equal(210, menu.VerticalOffset, 3);
        Assert.Contains("상세 보기", MenuTrap.Headers(menu));                    // 2D 우클릭과 같은 항목
        Assert.Contains(infos, l => l == $"[Map3D] 3D 위 우클릭 메뉴 열기 — 항목 {menu.Items.Count}개 @(320,210)");
        Assert.Contains("[Map3D] pick context kind=pids id=5 → 마커", infos);
        Assert.Null(Scenario.GetField<System.Windows.Point?>(rig.Vm, "_map3DMenuAt"));   // 3D 자리는 그 메뉴 한 번만

        // 같은 마커를 2D 에서 우클릭하면 마우스 자리
        SetShown(rig.Vm, false);
        var menu2D = rig.RightClick(marker);
        Assert.Equal(PlacementMode.MousePoint, menu2D.Placement);

        // 3D 에서 좌표 없는 pick(옛 3D 페이지) — 마우스 자리로 떨어진다(오류 없음)
        SetShown(rig.Vm, true);
        var noScreen = MenuTrap.Capture(() => Scenario.Call(rig.Vm, "OnMap3DMessage",
            Map3DMath.ParseMessage(new JObject { ["type"] = "pick", ["action"] = "context", ["kind"] = "pids", ["id"] = 5 }.ToString())));
        Assert.Equal(PlacementMode.MousePoint, noScreen.Placement);
        SetShown(rig.Vm, false);
    });
}
