using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Newtonsoft.Json.Linq;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>3D 나가기(2026-10-18) — 「영역 보기」 진입은 줌으로 나가지 않고, 나가면 들어오기 전 2D 자리 · 줌 진입은 3D 마지막 카메라.</summary>
public class Map3DExitTests
{
    private static readonly Map3DCamera Before = new(127.10, 37.36, 15.0, 30.0, 0.0);
    private static readonly Map3DCamera Last3D = new(127.11, 37.37, 15.8, 120.0, 55.0);

    [Theory]
    [InlineData(14.0)]   // 영역 전체를 보려고 멀리 뺐다
    [InlineData(16.49)]
    [InlineData(10.0)]
    public void should_not_leave_by_zoom_when_entered_by_area_view(double reportedZoom)
        => Assert.False(Map3DMath.ShouldLeave3D(reportedZoom, 16.5, areaView: true));

    [Theory]
    [InlineData(16.49, true)]
    [InlineData(16.5, false)]
    [InlineData(18.0, false)]
    public void should_leave_by_zoom_below_exit_when_entered_by_zoom_gate(double reportedZoom, bool expected)
        => Assert.Equal(expected, Map3DMath.ShouldLeave3D(reportedZoom, 16.5, areaView: false));

    [Fact]
    public void should_return_to_the_2d_place_before_area_view_when_exiting_area_view()
        => Assert.Equal(Before, Map3DMath.ExitCamera(areaView: true, Before, Last3D));

    [Fact]
    public void should_apply_the_last_3d_camera_when_exiting_a_zoom_gate_entry()
        => Assert.Equal(Last3D, Map3DMath.ExitCamera(areaView: false, Before, Last3D));

    [Fact]
    public void should_fall_back_to_the_last_3d_camera_when_area_view_has_no_saved_place()
        => Assert.Equal(Last3D, Map3DMath.ExitCamera(areaView: true, null, Last3D));

    [Fact]
    public void should_keep_2d_as_is_when_zoom_gate_entry_has_no_3d_camera_yet()
        => Assert.Null(Map3DMath.ExitCamera(areaView: false, Before, null));

    [Fact]
    public void should_send_home_type_with_bearing_pitch_animate_when_asking_whole_area_view()
    {
        var o = JObject.Parse(Map3DMath.HomeMessageJson(30.0, 60.0, animate: true));
        Assert.Equal("home", o.Value<string>("type"));
        Assert.Equal(30.0, o.Value<double>("bearing"));
        Assert.Equal(60.0, o.Value<double>("pitch"));
        Assert.True(o.Value<bool>("animate"));
    }
}
