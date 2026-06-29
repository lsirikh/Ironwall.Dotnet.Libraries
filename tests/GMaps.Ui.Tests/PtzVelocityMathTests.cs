using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Ptz;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>
/// ContinuousMove 속도 스케일(CameraPopup_PTZ_Responsiveness_Speed FR-PTR-02) 순수 로직 단위 테스트.
/// PtzVelocityMath.ScaleToRange: 정규화 속도(방향×사용자속도, [-1,1], 0=정지) → 카메라 연속속도 범위.
/// </summary>
public class PtzVelocityMathTests
{
    // ── [-1,1] 표준 범위 = 항등(기존 동작 유지) ────────────────────────────────
    [Theory]
    [InlineData(0.5, 0.5)]
    [InlineData(-0.5, -0.5)]
    [InlineData(1.0, 1.0)]
    [InlineData(-1.0, -1.0)]
    [InlineData(0.1, 0.1)]
    public void should_be_identity_when_range_is_minus1_to_1(double normalized, double expected)
        => Assert.Equal(expected, PtzVelocityMath.ScaleToRange(normalized, -1d, 1d), 6);

    // ── 0=정지 보존 (어떤 범위에서도) ──────────────────────────────────────────
    [Theory]
    [InlineData(-1d, 1d)]
    [InlineData(-0.5d, 0.5d)]
    [InlineData(-8d, 8d)]
    [InlineData(0d, 1d)]
    public void should_preserve_zero_when_normalized_is_zero(double min, double max)
        => Assert.Equal(0d, PtzVelocityMath.ScaleToRange(0d, min, max), 6);

    // ── 좁은 범위 [-0.5,0.5] → 비례 축소 (속도 슬라이더 실반영) ─────────────────
    [Theory]
    [InlineData(1.0, 0.5)]
    [InlineData(0.5, 0.25)]
    [InlineData(-1.0, -0.5)]
    [InlineData(-0.4, -0.2)]
    public void should_scale_down_when_range_is_narrow(double normalized, double expected)
        => Assert.Equal(expected, PtzVelocityMath.ScaleToRange(normalized, -0.5d, 0.5d), 6);

    // ── 넓은 범위 [-8,8] → 비례 확대 ───────────────────────────────────────────
    [Theory]
    [InlineData(0.5, 4.0)]
    [InlineData(1.0, 8.0)]
    [InlineData(-1.0, -8.0)]
    [InlineData(0.1, 0.8)]
    public void should_scale_up_when_range_is_wide(double normalized, double expected)
        => Assert.Equal(expected, PtzVelocityMath.ScaleToRange(normalized, -8d, 8d), 6);

    // ── 비대칭 범위 [-2,8] → 부호별 풀스케일(0 중심 보존) ──────────────────────
    [Theory]
    [InlineData(1.0, 8.0)]    // 양수 → max
    [InlineData(-1.0, -2.0)]  // 음수 → min
    [InlineData(0.5, 4.0)]    // 0.5 * 8
    [InlineData(-0.5, -1.0)]  // -0.5 * 2
    public void should_use_signed_fullscale_when_range_is_asymmetric(double normalized, double expected)
        => Assert.Equal(expected, PtzVelocityMath.ScaleToRange(normalized, -2d, 8d), 6);

    // ── 무효 범위(maxAbs<=0) → 원값 폴백 ──────────────────────────────────────
    [Fact]
    public void should_fallback_to_normalized_when_range_invalid()
    {
        Assert.Equal(0.5d, PtzVelocityMath.ScaleToRange(0.5d, 0d, 0d), 6);
        Assert.Equal(-0.3d, PtzVelocityMath.ScaleToRange(-0.3d, 0d, 0d), 6);
    }

    // ── 범위 초과 입력 방어 클램프 ─────────────────────────────────────────────
    [Fact]
    public void should_clamp_when_scaled_exceeds_range()
    {
        Assert.Equal(1d, PtzVelocityMath.ScaleToRange(2d, -1d, 1d), 6);    // 2*1=2 → clamp 1
        Assert.Equal(-1d, PtzVelocityMath.ScaleToRange(-2d, -1d, 1d), 6);  // -2 → clamp -1
    }

    // ── 단방향 범위 [0,1] → 역방향은 0(카메라 한계, 안전) ─────────────────────
    [Fact]
    public void should_zero_reverse_when_range_is_unsigned()
    {
        Assert.Equal(0.5d, PtzVelocityMath.ScaleToRange(0.5d, 0d, 1d), 6);   // 양수 정상
        Assert.Equal(0d, PtzVelocityMath.ScaleToRange(-0.5d, 0d, 1d), 6);    // 음수 → 0(역방향 불가)
    }
}
