using System;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Tracking;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>
/// TrackingMath(추적 속도·베어링·역전·좌표검증) 단위 테스트.
/// 실제 소스를 &lt;Compile Include&gt;로 링크해 검증(net8.0 격리, WPF 무의존).
/// 관련 FR: P2-03(속도) · P2-04(베어링) · P1-09(역전) · P1-10(좌표방어).
/// </summary>
public class TrackingMathTests
{
    [Fact(DisplayName = "should_return_about_111km_when_one_degree_latitude")]
    public void Haversine_OneDegreeLatitude_IsAbout111Km()
    {
        var d = TrackingMath.HaversineMeters(38.0, 127.0, 39.0, 127.0);
        Assert.InRange(d, 110_000d, 112_000d); // 위도 1° ≈ 111km
    }

    [Fact(DisplayName = "should_return_zero_when_same_point")]
    public void Haversine_SamePoint_IsZero()
        => Assert.Equal(0d, TrackingMath.HaversineMeters(38.1, 127.5, 38.1, 127.5), 6);

    [Fact(DisplayName = "should_keep_prev_when_delta_nonpositive")]
    public void ComputeSpeed_NonPositiveDelta_ReturnsNull()
    {
        Assert.Null(TrackingMath.ComputeSpeedMps(100d, 0d, 50d));
        Assert.Null(TrackingMath.ComputeSpeedMps(100d, -2d, 50d));
    }

    [Fact(DisplayName = "should_keep_prev_when_speed_exceeds_max")]
    public void ComputeSpeed_AboveMax_ReturnsNull()
        => Assert.Null(TrackingMath.ComputeSpeedMps(1000d, 1d, 50d)); // 1000 m/s > 50

    [Fact(DisplayName = "should_return_speed_when_valid")]
    public void ComputeSpeed_Valid_ReturnsValue()
    {
        var s = TrackingMath.ComputeSpeedMps(100d, 10d, 50d);
        Assert.NotNull(s);
        Assert.Equal(10d, s!.Value, 6); // 100m / 10s = 10 m/s
    }

    [Fact(DisplayName = "should_return_about_90_when_due_east")]
    public void Bearing_DueEast_IsAbout90()
    {
        var b = TrackingMath.BearingDegrees(38.0, 127.0, 38.0, 127.001);
        Assert.InRange(b, 89d, 91d);
    }

    [Fact(DisplayName = "should_return_about_0_when_due_north")]
    public void Bearing_DueNorth_IsAbout0()
    {
        var b = TrackingMath.BearingDegrees(38.0, 127.0, 38.001, 127.0);
        Assert.True(b < 1d || b > 359d);
    }

    [Fact(DisplayName = "should_skip_when_observed_at_is_older_or_equal")]
    public void IsReversed_OlderOrEqual_IsTrue()
    {
        var t0 = new DateTime(2026, 2, 5, 10, 30, 0, DateTimeKind.Utc);
        Assert.True(TrackingMath.IsReversed(t0.AddSeconds(-1), t0));   // 과거 → skip
        Assert.True(TrackingMath.IsReversed(t0, t0));                  // 동일 → skip
        Assert.False(TrackingMath.IsReversed(t0.AddSeconds(1), t0));   // 미래 → 수용
        Assert.False(TrackingMath.IsReversed(t0, null));              // 첫 관측 → 수용
    }

    [Theory(DisplayName = "should_validate_lat_lng_range_and_nan")]
    [InlineData(38.1, 127.5, true)]
    [InlineData(-90d, 180d, true)]
    [InlineData(91d, 127d, false)]
    [InlineData(38d, 181d, false)]
    [InlineData(double.NaN, 127d, false)]
    [InlineData(38d, double.PositiveInfinity, false)]
    public void IsValidLatLng_RangeAndNan(double lat, double lng, bool expected)
        => Assert.Equal(expected, TrackingMath.IsValidLatLng(lat, lng));
}
