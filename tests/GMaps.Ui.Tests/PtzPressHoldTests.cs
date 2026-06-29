using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Ptz;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>
/// 줌·포커스 Press-Hold(CameraPopup_PressHold_PtzZoomFocus) 순수 로직 단위 테스트.
/// 실코드(WPF 무의존 헬퍼) 직접 링크: PtzGestureTag(Tag 파싱, FR-PH-06) / PtzFocusMath(포커스 클램프, FR-PH-10).
/// </summary>
public class PtzPressHoldTests
{
    // ── PtzGestureTag.TryParse (FR-PH-06) ──────────────────────────────────────
    [Theory]
    [InlineData("zoom:1", PtzHoldGesture.Zoom, 1, 0)]
    [InlineData("zoom:-1", PtzHoldGesture.Zoom, -1, 0)]
    [InlineData("focus:1", PtzHoldGesture.Focus, 1, 0)]
    [InlineData("focus:-1", PtzHoldGesture.Focus, -1, 0)]
    [InlineData("-1,-1", PtzHoldGesture.PanTilt, -1, -1)]
    [InlineData("0,-1", PtzHoldGesture.PanTilt, 0, -1)]
    [InlineData("1,1", PtzHoldGesture.PanTilt, 1, 1)]
    public void should_parse_gesture_when_tag_is_valid(string tag, PtzHoldGesture expected, int edx, int edy)
    {
        var ok = PtzGestureTag.TryParse(tag, out var g, out var dx, out var dy);

        Assert.True(ok);
        Assert.Equal(expected, g);
        Assert.Equal(edx, dx);
        Assert.Equal(edy, dy);
    }

    [Theory]
    [InlineData("stop")]      // 정지 버튼 — 호출부가 별도 처리(제스처 아님)
    [InlineData("")]
    [InlineData(null)]
    [InlineData("zoom:")]     // 방향 숫자 없음
    [InlineData("zoom:x")]    // 숫자 아님
    [InlineData("focus:")]
    [InlineData("1")]         // 콤마 단일
    [InlineData("1,2,3")]     // 토큰 과다
    [InlineData("a,b")]       // 숫자 아님
    public void should_return_false_when_tag_is_not_a_gesture(string? tag)
    {
        var ok = PtzGestureTag.TryParse(tag, out var g, out _, out _);

        Assert.False(ok);
        Assert.Equal(PtzHoldGesture.None, g);
    }

    // ── PtzFocusMath.ClampMagnitude (FR-PH-10) ─────────────────────────────────
    [Fact]
    public void should_clamp_down_when_requested_exceeds_camera_max()
    {
        // 카메라 ContinuousFocus 범위 [-0.5, 0.5] → 0.7 요청은 0.5로 상한 클램프(F-항목 해소).
        var v = PtzFocusMath.ClampMagnitude(0.7, -0.5, 0.5);
        Assert.Equal(0.5, v, 6);
    }

    [Fact]
    public void should_keep_value_when_within_camera_range()
    {
        // 범위 [-1,1] 안의 0.7은 그대로.
        var v = PtzFocusMath.ClampMagnitude(0.7, -1.0, 1.0);
        Assert.Equal(0.7, v, 6);
    }

    [Fact]
    public void should_fallback_to_requested_when_range_unknown()
    {
        // GetMoveOptions 미제공(null) → 폴백(원값 유지).
        Assert.Equal(0.7, PtzFocusMath.ClampMagnitude(0.7, null, null), 6);
        Assert.Equal(0.7, PtzFocusMath.ClampMagnitude(0.7, 0.5, null), 6);
        Assert.Equal(0.7, PtzFocusMath.ClampMagnitude(0.7, null, 0.5), 6);
    }

    [Fact]
    public void should_use_absolute_max_when_range_is_asymmetric()
    {
        // [-0.9, 0.3] → maxAbs=0.9 → 0.7 유지(상한 0.9 이내).
        Assert.Equal(0.7, PtzFocusMath.ClampMagnitude(0.7, -0.9, 0.3), 6);
        // [-0.2, 0.2] → maxAbs=0.2 → 0.7→0.2.
        Assert.Equal(0.2, PtzFocusMath.ClampMagnitude(0.7, -0.2, 0.2), 6);
    }
}
