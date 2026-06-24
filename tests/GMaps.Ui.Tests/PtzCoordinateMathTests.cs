using System;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>
/// PTZ 좌표 변환(PtzCoordinateMath) 단위 테스트.
///
/// 실제 구현은 OnvifSolution.Helpers.PtzCoordinateMath(net8.0-windows)이라 net8.0 격리 테스트에서
/// 직접 호출 불가 → 순수 수식을 로컬 복제하여 동치 검증(RtspMapPopupTests/DigitalZoomCoordinateTests 패턴).
///
/// 불변식(PRD NFR-SAFE-01): 카메라 GetNode space [min,max] 진실원, 고정 ±1.0 클램프 금지, tilt Y축 반전.
/// </summary>
public class PtzCoordinateMathTests
{
    // ── PtzCoordinateMath 복제 ──────────────────────────────────────────────
    private static double Clamp(double v, double min, double max)
    {
        if (min > max) (min, max) = (max, min);
        return v < min ? min : (v > max ? max : v);
    }

    private static (double pan, double tilt) PixelDeltaToRelative(
        double dx, double dy, double imageW, double imageH,
        double xMin, double xMax, double yMin, double yMax, double sensitivity = 1.0)
    {
        if (imageW <= 0 || imageH <= 0) return (0d, 0d);
        var nx = (dx / imageW) * sensitivity;
        var ny = -(dy / imageH) * sensitivity;
        return (Clamp(nx, xMin, xMax), Clamp(ny, yMin, yMax));
    }

    private static bool IsDrag(double dx, double dy, double deadZonePx = 8d)
        => (dx * dx + dy * dy) > (deadZonePx * deadZonePx);

    // ── Y축 반전: 화면 아래로 드래그(dy>0) → 틸트 내림(tilt<0) ──────────────
    [Fact]
    public void should_invert_tilt_when_dragging_down()
    {
        var (_, tilt) = PixelDeltaToRelative(0, 50, 300, 200, -1, 1, -1, 1);
        Assert.True(tilt < 0, "화면 아래 드래그는 tilt 음수(틸트 내림)여야 한다");
    }

    [Fact]
    public void should_invert_tilt_when_dragging_up()
    {
        var (_, tilt) = PixelDeltaToRelative(0, -50, 300, 200, -1, 1, -1, 1);
        Assert.True(tilt > 0, "화면 위 드래그는 tilt 양수(틸트 올림)여야 한다");
    }

    // ── 카메라 range 진실원: 고정 ±1.0 아니라 GetNode 범위로 클램프 ──────────
    [Fact]
    public void should_clamp_to_camera_range_not_fixed_unit_when_range_is_narrow()
    {
        // 상대 space가 [-0.5, 0.5]인 카메라 — 전폭 드래그라도 0.5를 넘으면 안 됨
        var (pan, _) = PixelDeltaToRelative(900, 0, 300, 200, -0.5, 0.5, -0.5, 0.5, sensitivity: 1.0);
        Assert.Equal(0.5, pan, 6);
    }

    [Fact]
    public void should_clamp_to_camera_range_when_range_is_wide()
    {
        // [-180,180] 카메라 — 작은 정규화 값이 클램프에 안 걸리고 비례 유지
        var (pan, _) = PixelDeltaToRelative(150, 0, 300, 200, -180, 180, -180, 180, sensitivity: 1.0);
        Assert.Equal(0.5, pan, 6); // 0.5 정규화는 [-180,180] 안이라 그대로
    }

    // ── 감도 스케일 ─────────────────────────────────────────────────────────
    [Fact]
    public void should_scale_by_sensitivity_when_provided()
    {
        var (panLow, _) = PixelDeltaToRelative(30, 0, 300, 200, -1, 1, -1, 1, sensitivity: 1.0);
        var (panHigh, _) = PixelDeltaToRelative(30, 0, 300, 200, -1, 1, -1, 1, sensitivity: 2.0);
        Assert.Equal(0.1, panLow, 6);
        Assert.Equal(0.2, panHigh, 6);
    }

    // ── 0 치수 방어 ─────────────────────────────────────────────────────────
    [Fact]
    public void should_return_zero_when_image_dimension_is_zero()
    {
        var (pan, tilt) = PixelDeltaToRelative(50, 50, 0, 0, -1, 1, -1, 1);
        Assert.Equal(0, pan, 6);
        Assert.Equal(0, tilt, 6);
    }

    // ── 중앙 무드래그 → 0 ───────────────────────────────────────────────────
    [Fact]
    public void should_return_zero_when_no_drag()
    {
        var (pan, tilt) = PixelDeltaToRelative(0, 0, 300, 200, -1, 1, -1, 1);
        Assert.Equal(0, pan, 6);
        Assert.Equal(0, tilt, 6);
    }

    // ── 데드존(8px): 드래그/짧은클릭 구분 ──────────────────────────────────
    [Theory]
    [InlineData(6, 0, false)]   // 6px < 8 → 클릭
    [InlineData(0, 7, false)]   // 7px < 8 → 클릭
    [InlineData(8, 0, false)]   // 정확히 8 → 미초과(클릭)
    [InlineData(9, 0, true)]    // 9px > 8 → 드래그
    [InlineData(6, 6, true)]    // sqrt(72)=8.49 > 8 → 드래그
    public void should_classify_drag_vs_click_by_deadzone(double dx, double dy, bool expected)
        => Assert.Equal(expected, IsDrag(dx, dy, 8d));

    // ── 절대 좌표 클램프(프리셋) ────────────────────────────────────────────
    [Theory]
    [InlineData(0.4, -0.5, 0.5, 0.4)]    // 범위 내
    [InlineData(0.9, -0.5, 0.5, 0.5)]    // 상한 초과
    [InlineData(-0.9, -0.5, 0.5, -0.5)]  // 하한 초과
    public void should_clamp_absolute_to_range(double v, double min, double max, double expected)
        => Assert.Equal(expected, Clamp(v, min, max), 6);
}
