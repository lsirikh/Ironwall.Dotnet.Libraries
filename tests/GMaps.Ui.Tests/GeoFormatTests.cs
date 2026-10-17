using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Detail;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>
/// 상세 창 GPS 좌표 표기 — 장비 <c>geolocation{location,latitude,longitude,altitude,heading}</c> 를 사람이 읽는 형태로.
/// <para>핵심 계약 둘: <b>(0,0) 은 좌표가 아니라 미등록</b>이고, <b>DMS 초 반올림이 60 을 만들면 자리올림</b>해야 한다.</para>
/// </summary>
public class GeoFormatTests
{
    [Theory]
    [InlineData(37.392779, 126.967959, true)]
    [InlineData(-33.8688, 151.2093, true)]     // 남반구·동경
    [InlineData(0, 0, false)]                   // 미등록(서버 geolocation 없음 → 모델 0)
    [InlineData(1e-12, -1e-12, false)]          // 사실상 0
    [InlineData(double.NaN, 127, false)]
    [InlineData(91, 127, false)]                // 위도 범위 밖
    [InlineData(37, 181, false)]                // 경도 범위 밖
    public void should_treat_origin_and_invalid_as_unregistered(double lat, double lon, bool expected)
        => Assert.Equal(expected, GeoFormat.HasPosition(lat, lon));

    [Fact]
    public void should_render_decimal_and_dms_together()
    {
        // 십진수는 붙여넣기용, 도분초는 눈으로 읽기용 — 둘 다 필요하다.
        Assert.Equal("37.392779 (37°23'34.0\"N)", GeoFormat.Latitude(37.392779, 126.967959));
        Assert.Equal("126.967959 (126°58'04.7\"E)", GeoFormat.Longitude(37.392779, 126.967959));
    }

    [Fact]
    public void should_return_null_when_position_is_unregistered()
    {
        Assert.Null(GeoFormat.Latitude(0, 0));
        Assert.Null(GeoFormat.Longitude(0, 0));
    }

    [Theory]
    [InlineData(37.5, true, "37°30'00.0\"N")]
    [InlineData(-37.5, true, "37°30'00.0\"S")]
    [InlineData(126.25, false, "126°15'00.0\"E")]
    [InlineData(-126.25, false, "126°15'00.0\"W")]
    public void should_pick_hemisphere_from_sign(double deg, bool isLat, string expected)
        => Assert.Equal(expected, GeoFormat.Dms(deg, isLat));

    [Fact]
    public void should_carry_when_seconds_round_to_sixty()
    {
        // 59.98" 를 그대로 반올림하면 60.0" 가 되어 존재하지 않는 좌표가 찍힌다.
        var dms = GeoFormat.Dms(37.0 + 59.9999 / 3600.0, isLatitude: true);
        Assert.DoesNotContain("60.0\"", dms);
        Assert.Equal("37°01'00.0\"N", dms);
    }

    [Fact]
    public void should_carry_minutes_into_degrees_at_boundary()
    {
        var dms = GeoFormat.Dms(37.0 - 0.00001 / 3600.0 + 1.0, isLatitude: true);   // 37.99999…° ≈ 38°
        Assert.StartsWith("38°00'", dms);
    }

    [Theory]
    [InlineData(12.5, "12.5 m")]
    [InlineData(0d, "0 m")]
    [InlineData(null, null)]
    public void should_render_altitude(double? meters, string? expected)
        => Assert.Equal(expected, GeoFormat.Altitude(meters));

    [Theory]
    [InlineData(90d, "90°")]
    [InlineData(370d, "10°")]      // 정규화
    [InlineData(-30d, "330°")]
    [InlineData(null, null)]
    public void should_normalize_heading(double? deg, string? expected)
        => Assert.Equal(expected, GeoFormat.Heading(deg));

    [Fact]
    public void should_measure_distance_between_registered_points()
    {
        // 위도 1도 ≈ 111 km
        var d = GeoFormat.DistanceMeters(37.0, 127.0, 38.0, 127.0);
        Assert.NotNull(d);
        Assert.InRange(d!.Value, 110_000, 112_000);
    }

    [Fact]
    public void should_return_zero_distance_for_same_point()
        => Assert.Equal(0, GeoFormat.DistanceMeters(37.5, 127.5, 37.5, 127.5)!.Value, 3);

    [Fact]
    public void should_not_measure_distance_when_either_side_is_unregistered()
    {
        Assert.Null(GeoFormat.DistanceMeters(0, 0, 37.5, 127.5));
        Assert.Null(GeoFormat.DistanceMeters(37.5, 127.5, 0, 0));
    }

    [Theory]
    [InlineData(12.34, "12.3 m")]
    [InlineData(999d, "999 m")]
    [InlineData(1000d, "1 km")]
    [InlineData(2540, "2.54 km")]
    public void should_switch_unit_at_one_kilometre(double meters, string expected)
        => Assert.Equal(expected, GeoFormat.Distance(meters));
}
