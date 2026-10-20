using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>
/// 3D HUD(2026-10-18) — 3D 동안 줌 컨트롤 숨김("3D" 표시에서 파생), 좌표 칩 = 3D 마우스가 가리키는 땅(cursor 메시지),
/// 축척 막대 = 3D 화면 가운데 1 px 의 실제 거리, 3D 위 안내 글에 줌 표기 없음.
/// </summary>
public class Map3DHudTests
{
    [Fact]
    public void should_hide_zoom_control_when_3d_is_shown()
    {
        Assert.False(Map3DMath.IsZoomControlVisible(Map3DMath.StatusText(true)));
        Assert.True(Map3DMath.IsZoomControlVisible(Map3DMath.StatusText(false)));
        Assert.True(Map3DMath.IsZoomControlVisible(null));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void should_show_zoom_control_exactly_when_exit_button_is_hidden(bool shown)
    {
        var status = Map3DMath.StatusText(shown);
        Assert.NotEqual(Map3DMath.IsExitButtonVisible(status), Map3DMath.IsZoomControlVisible(status));
    }

    [Fact]
    public void should_parse_cursor_message_with_altitude()
    {
        var msg = Map3DMath.ParseMessage("{\"type\":\"cursor\",\"lon\":127.1081235,\"lat\":37.3671235,\"alt\":58.3}");
        Assert.NotNull(msg);
        Assert.Equal("cursor", msg!.Type);
        Assert.Equal(new Map3DCursor(127.1081235, 37.3671235, 58.3), msg.Cursor);
        Assert.Null(msg.Camera);
        Assert.Null(msg.Pick);
    }

    [Fact]
    public void should_parse_cursor_message_without_altitude_when_alt_is_null()
    {
        var msg = Map3DMath.ParseMessage("{\"type\":\"cursor\",\"lon\":127.1,\"lat\":37.3,\"alt\":null}");
        Assert.Equal(new Map3DCursor(127.1, 37.3, null), msg!.Cursor);
    }

    [Theory]
    [InlineData("{\"type\":\"cursor\",\"lat\":37.3}")]
    [InlineData("{\"type\":\"cursor\",\"lon\":\"x\",\"lat\":37.3}")]
    [InlineData("{\"type\":\"cursor\",\"lon\":127.1}")]
    public void should_drop_cursor_message_when_lon_or_lat_is_missing(string json)
        => Assert.Null(Map3DMath.ParseMessage(json));

    [Fact]
    public void should_format_altitude_with_one_decimal_and_hide_when_unknown()
    {
        Assert.Equal("58.3 m", Map3DMath.AltitudeText(58.26));
        Assert.Equal("-2.0 m", Map3DMath.AltitudeText(-2));
        Assert.Null(Map3DMath.AltitudeText(null));
        Assert.Null(Map3DMath.AltitudeText(double.NaN));
    }

    [Fact]
    public void should_compute_meters_per_pixel_like_web_mercator()
    {
        // map-3d camsync 시험과 같은 값 — 줌 17 · 위도 37.367 ≈ 0.9497 m/px
        Assert.InRange(Map3DMath.MetersPerPixel(17, 37.367), 0.9497 - 0.001, 0.9497 + 0.001);
        Assert.Equal(Map3DMath.MetersPerPixel(17, 37.367) / 2, Map3DMath.MetersPerPixel(18, 37.367), 9);
        Assert.True(double.IsNaN(Map3DMath.MetersPerPixel(double.NaN, 37.0)));
    }

    [Theory]
    [InlineData(0.3, "30m", 100.0)]       // 30 m 가 정확히 100 px
    [InlineData(0.9497, "50m", 52.648)]
    [InlineData(1.2, "100m", 83.333)]
    [InlineData(4.0, "300m", 75.0)]
    [InlineData(12.0, "1Km", 83.333)]
    [InlineData(25.0, "2Km", 80.0)]
    [InlineData(0.02, "2m", 100.0)]
    [InlineData(0.004, "0.3m", 75.0)]
    public void should_pick_round_scale_bar_length_that_fits_100px(double mpp, string label, double px)
    {
        var bar = Map3DMath.ScaleBar(mpp);
        Assert.NotNull(bar);
        Assert.Equal(label, bar!.Value.Label);
        Assert.Equal(px, bar.Value.Px, 2);
    }

    [Theory]
    [InlineData(0.05)]
    [InlineData(0.7)]
    [InlineData(3.3)]
    [InlineData(47.0)]
    [InlineData(800.0)]
    public void should_keep_scale_bar_between_half_and_full_width(double mpp)
    {
        var bar = Map3DMath.ScaleBar(mpp)!.Value;
        Assert.InRange(bar.Px, 50.0 - 1e-9, 100.0 + 1e-9);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void should_not_build_scale_bar_when_meters_per_pixel_is_invalid(double mpp)
        => Assert.Null(Map3DMath.ScaleBar(mpp));

    [Fact]
    public void should_scale_bar_follow_3d_camera_distance()
    {
        // 3D 에서 물러나면(줌이 작아지면) 1 px 가 길어져 같은 막대 자리에 더 긴 거리가 표시된다
        var near = Map3DMath.ScaleBar(Map3DMath.MetersPerPixel(18.2, 37.367))!.Value;
        var far = Map3DMath.ScaleBar(Map3DMath.MetersPerPixel(13.4, 37.367))!.Value;
        Assert.Equal("30m", near.Label);
        Assert.Equal("1Km", far.Label);
    }

    // 기대값 변경(2026-10-19 사용자 결정): WPF 안 3D(composition)는 Esc 로 나가지 않고 「나가기」 단추로만 나간다 —
    // 옛 기대("ESC" 포함)는 실제와 다른 안내를 고정하고 있었다. Esc 는 Esc 로 나가는 네이티브 창 3D(hwnd)에서만 말한다.
    [Fact]
    public void should_guide_shown_3d_without_zoom_wording()
    {
        var text = Map3DGuide.StageText("shown");
        Assert.DoesNotContain("줌", text);
        Assert.DoesNotContain("ESC", text);
        Assert.Contains("나가기", text);
        Assert.Equal(text, Map3DGuide.ShownText("composition"));
        Assert.Contains("ESC", Map3DGuide.ShownText("hwnd"));
        Assert.DoesNotContain("줌", Map3DGuide.ShownText("hwnd"));
    }
}
