using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>
/// 제어 허브 순수 계산(<see cref="CameraPopupHubMath"/>) 단위 테스트 — 실코드 소스링크.
/// (CameraPopup_ControlHub NFR-05: 뱃지 상한·경계 clamp·드래그 데드존)
/// </summary>
public class CameraPopupHubMathTests
{
    // ── BadgeText ──
    [Fact] public void should_be_empty_when_count_zero_or_negative()
    {
        Assert.Equal("", CameraPopupHubMath.BadgeText(0));
        Assert.Equal("", CameraPopupHubMath.BadgeText(-3));
    }

    [Fact] public void should_show_count_when_within_max()
    {
        Assert.Equal("1", CameraPopupHubMath.BadgeText(1));
        Assert.Equal("9", CameraPopupHubMath.BadgeText(9));
    }

    [Fact] public void should_cap_when_over_max()
    {
        Assert.Equal("9+", CameraPopupHubMath.BadgeText(10));
        Assert.Equal("9+", CameraPopupHubMath.BadgeText(250));
    }

    // ── IsDrag (deadzone 8px) ──
    [Fact] public void should_not_be_drag_when_within_deadzone()
    {
        Assert.False(CameraPopupHubMath.IsDrag(0, 0));
        Assert.False(CameraPopupHubMath.IsDrag(8, 0));   // ==8 not > 8
        Assert.False(CameraPopupHubMath.IsDrag(5, 5));   // 50 < 64
    }

    [Fact] public void should_be_drag_when_beyond_deadzone()
    {
        Assert.True(CameraPopupHubMath.IsDrag(9, 0));
        Assert.True(CameraPopupHubMath.IsDrag(6, 6));    // 72 > 64
        Assert.True(CameraPopupHubMath.IsDrag(0, -20));
    }

    // ── ClampToBounds ──
    [Fact] public void should_keep_position_when_inside_bounds()
    {
        var (x, y) = CameraPopupHubMath.ClampToBounds(100, 80, 60, 40, 800, 600);
        Assert.Equal(100, x); Assert.Equal(80, y);
    }

    [Fact] public void should_clamp_to_right_bottom_edge_when_beyond()
    {
        var (x, y) = CameraPopupHubMath.ClampToBounds(900, 700, 60, 40, 800, 600);
        Assert.Equal(740, x);   // 800-60
        Assert.Equal(560, y);   // 600-40
    }

    [Fact] public void should_clamp_negative_to_zero()
    {
        var (x, y) = CameraPopupHubMath.ClampToBounds(-50, -30, 60, 40, 800, 600);
        Assert.Equal(0, x); Assert.Equal(0, y);
    }

    [Fact] public void should_pin_to_zero_when_canvas_smaller_than_hub()
    {
        var (x, y) = CameraPopupHubMath.ClampToBounds(10, 10, 900, 700, 800, 600);
        Assert.Equal(0, x); Assert.Equal(0, y);
    }

    [Fact] public void should_default_nan_to_zero()
    {
        var (x, y) = CameraPopupHubMath.ClampToBounds(double.NaN, double.NaN, 60, 40, 800, 600);
        Assert.Equal(0, x); Assert.Equal(0, y);
    }
}
