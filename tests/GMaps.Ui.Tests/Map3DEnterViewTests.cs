using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Newtonsoft.Json.Linq;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>
/// 3D 진입 · 나가기 7차(2026-10-18) — 모든 진입은 영역 전체 시점, 3D 에서는 줌으로 나가지 않음, 나가면 들어오기 직전 2D 자리,
/// 「나가기」 단추는 "3D" 표시에서만 파생.
/// </summary>
public class Map3DEnterViewTests
{
    private static readonly Map3DCamera Before = new(127.10, 37.36, 18.0, 30.0, 0.0);
    private static readonly Map3DCamera Last3D = new(127.11, 37.37, 13.1, 120.0, 60.0);

    [Theory]
    [InlineData(0.0)]
    [InlineData(30.0)]
    [InlineData(275.5)]
    public void should_open_whole_area_home_view_tilted_with_2d_bearing_when_entering(double bearing)
    {
        var o = JObject.Parse(Map3DMath.EnterViewMessageJson(bearing));
        Assert.Equal("home", o.Value<string>("type"));
        Assert.Equal(bearing, o.Value<double>("bearing"));
        Assert.Equal(Map3DMath.DefaultPitchDeg, o.Value<double>("pitch"));
        Assert.True(o.Value<bool>("animate"));
    }

    [Fact]
    public void should_send_same_enter_view_as_area_view_home()
        => Assert.Equal(Map3DMath.HomeMessageJson(45.0, Map3DMath.DefaultPitchDeg, animate: true), Map3DMath.EnterViewMessageJson(45.0));

    [Theory]
    [InlineData(10.0)]
    [InlineData(13.1)]   // 영역 전체 시점(실측)
    [InlineData(16.49)]  // 옛 이탈 줌 바로 밑
    [InlineData(19.0)]
    public void should_keep_following_3d_camera_in_2d_and_not_leave_at_any_zoom(double zoom)
        => Assert.True(Map3DMath.ShouldSync2DToCamera("composition", Last3D with { Zoom = zoom }));

    [Theory]
    [InlineData("hwnd")]
    [InlineData("none")]
    public void should_not_move_2d_under_native_window_3d(string hostMode)
        => Assert.False(Map3DMath.ShouldSync2DToCamera(hostMode, Last3D));

    [Fact]
    public void should_not_move_2d_when_camera_zoom_is_not_finite()
        => Assert.False(Map3DMath.ShouldSync2DToCamera("composition", Last3D with { Zoom = double.NaN }));

    [Fact]
    public void should_return_to_2d_place_just_before_entering_when_exiting()
        => Assert.Equal(Before, Map3DMath.ExitCamera(Before, Last3D));

    [Fact]
    public void should_fall_back_to_last_3d_camera_when_no_place_before_entering()
        => Assert.Equal(Last3D, Map3DMath.ExitCamera(null, Last3D));

    [Fact]
    public void should_keep_2d_as_is_when_neither_place_is_known()
        => Assert.Null(Map3DMath.ExitCamera(null, null));

    [Theory]
    [InlineData(true, "3D")]
    [InlineData(false, "2D")]
    public void should_show_3d_status_only_while_3d_is_shown(bool shown, string expected)
        => Assert.Equal(expected, Map3DMath.StatusText(shown));

    [Theory]
    [InlineData("3D", true)]
    [InlineData("2D", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void should_show_exit_button_only_when_status_is_3d(string? status, bool expected)
        => Assert.Equal(expected, Map3DMath.IsExitButtonVisible(status));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void should_derive_exit_button_from_status_text_alone(bool shown)
        => Assert.Equal(shown, Map3DMath.IsExitButtonVisible(Map3DMath.StatusText(shown)));
}
