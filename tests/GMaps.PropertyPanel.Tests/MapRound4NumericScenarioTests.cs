using System.Windows.Controls;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapProperties;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using Moq;
using Xunit;

namespace GMaps.PropertyPanel.Tests;

/// <summary>
/// 실창 4회차(2026-10-13) NUM-R29 지도 쪽 결함 헤드리스 짝 — 앱 스타일(PidsPropertyStyle)의 진짜 슬라이더를
/// 키보드 Home/End 와 같은 명령(Slider.MinimizeValue · MaximizeValue)으로 움직이고, 속성창을 닫았다 다시 열어 같은 값인지 본다. 창은 띄우지 않는다.
/// </summary>
public class MapRound4NumericScenarioTests
{
    private static GMapPidsMarker PlaceCamera(Property3DRig rig, int id)
    {
        var model = new PidsSymbolModel
        {
            Id = id, Title = $"R29-SYM-B-{id}", DeviceType = EnumDeviceType.IpCamera, Latitude = Property3DRig.HomeLat, Longitude = Property3DRig.HomeLng,
            Width = 40, Height = 40, Visible = true, DetectionRange = 30, DetectionAngle = 90, DetectionBearing = 0, FOVOpacity = 0.8,
        };
        var marker = new GMapPidsMarker(Mock.Of<ILogService>(), model) { Width = 40, Height = 40, IsVisible = true };
        rig.Map.Markers.Add(marker);
        rig.LayoutMap();
        return marker;
    }

    [Fact]
    [Trait("Scenario", "NUM-R29-B1")]
    public void should_reach_both_ends_when_detection_range_slider_gets_home_and_end() => AppHost.Run(() =>
    {
        var rig = Property3DRig.Create();
        var marker = PlaceCamera(rig, 4101);
        var panel = rig.OpenPanel<GMapPropertyPidsControl>(marker, "PidsPropertyStyle.xaml");
        var slider = Property3DRig.ByLabel<Slider>(panel, "탐지범위");

        Slider.MinimizeValue.Execute(null, slider); Property3DRig.Pump();
        Assert.Equal(10, slider.Value); Assert.Equal(10, marker.DetectionRange);
        Slider.MaximizeValue.Execute(null, slider); Property3DRig.Pump();
        Assert.Equal(1000, slider.Value); Assert.Equal(1000, marker.DetectionRange);
        slider.Value = 297.4; Property3DRig.Pump();
        Assert.Equal(297, marker.DetectionRange);
    });

    [Fact]
    [Trait("Scenario", "NUM-R29-B6")]
    public void should_show_same_detection_values_when_property_panel_is_reopened() => AppHost.Run(() =>
    {
        var rig = Property3DRig.Create();
        var marker = PlaceCamera(rig, 4102);
        var panel = rig.OpenPanel<GMapPropertyPidsControl>(marker, "PidsPropertyStyle.xaml");
        Property3DRig.ByLabel<Slider>(panel, "탐지범위").Value = 297;
        Property3DRig.ByLabel<Slider>(panel, "탐지각도").Value = 98;
        Property3DRig.Pump();

        Property3DRig.Call(rig.Vm, "HidePropertyPanel");
        rig.Vm.SelectedMarker = null;
        var again = rig.OpenPanel<GMapPropertyPidsControl>(marker, "PidsPropertyStyle.xaml");

        Assert.Equal(297, again.DetectionRange);
        Assert.Equal(297, Property3DRig.ByLabel<Slider>(again, "탐지범위").Value);
        Assert.Equal(98, Property3DRig.ByLabel<Slider>(again, "탐지각도").Value);
        Assert.False(double.IsNaN(marker.DetectionRange));
    });
}
