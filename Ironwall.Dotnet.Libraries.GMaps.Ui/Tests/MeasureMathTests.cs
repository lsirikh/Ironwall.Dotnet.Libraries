using System.Collections.Generic;
using GMap.NET;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Tests;

/// <summary>
/// 측정 툴 지오데식 수학·단위 포맷 회귀 테스트 (Measure_Tools FR-01/02, NFR-01).
/// 위경도 도메인 계산이 줌·위도 무관 정확한지 known-value로 검증.
/// </summary>
public class MeasureMathTests
{
    // ── 거리 (Haversine, R=6378137) ──

    [Fact(DisplayName = "적도 위도 1도 거리 ≈ 111,319.9 m (R=6378137 known-value)")]
    public void should_return_known_meridian_distance_when_one_degree_lat()
    {
        double d = MeasureMath.DistanceMeters(new PointLatLng(0, 0), new PointLatLng(1, 0));
        Assert.InRange(d, 111300, 111340);   // 1°(적도) ≈ 111,319.9 m (±20m)
    }

    [Fact(DisplayName = "폴리라인 총 길이 = 구간 합, 점 2개 미만 = 0")]
    public void should_sum_segments_when_polyline()
    {
        var pts = new List<PointLatLng> { new(0, 0), new(0, 1), new(0, 2) };
        double one = MeasureMath.DistanceMeters(new PointLatLng(0, 0), new PointLatLng(0, 1));
        Assert.Equal(one * 2, MeasureMath.TotalLengthMeters(pts), 3);
        Assert.Equal(0d, MeasureMath.TotalLengthMeters(new List<PointLatLng> { new(37, 127) }));
        Assert.Equal(0d, MeasureMath.TotalLengthMeters(null));
    }

    // ── 면적 (지오데식 구면초과 shoelace) ──

    [Fact(DisplayName = "적도 소형 사각형 면적 ≈ 평면 근사와 0.5% 이내 일치")]
    public void should_match_planar_approx_when_small_equatorial_square()
    {
        // 0.01° × 0.01° 정사각(적도). 변 길이 ≈ 0.01·π/180·R
        const double side = 0.01;
        var sq = new List<PointLatLng> { new(0, 0), new(side, 0), new(side, side), new(0, side) };
        double sideM = side * (System.Math.PI / 180d) * MeasureMath.EarthRadius;
        double planar = sideM * sideM;
        double a = MeasureMath.AreaSquareMeters(sq);
        Assert.InRange(a, planar * 0.995, planar * 1.005);
    }

    [Fact(DisplayName = "면적은 와인딩(정점 순서 역전) 무관 — 절대값")]
    public void should_be_winding_independent_when_reversed()
    {
        var cw = new List<PointLatLng> { new(37.0, 127.0), new(37.0, 127.01), new(37.01, 127.01), new(37.01, 127.0) };
        var ccw = new List<PointLatLng> { new(37.01, 127.0), new(37.01, 127.01), new(37.0, 127.01), new(37.0, 127.0) };
        double a1 = MeasureMath.AreaSquareMeters(cw);
        double a2 = MeasureMath.AreaSquareMeters(ccw);
        Assert.True(a1 > 0);
        Assert.Equal(a1, a2, 3);
    }

    [Fact(DisplayName = "면적: 점 3개 미만 = 0, null = 0")]
    public void should_return_zero_when_degenerate_polygon()
    {
        Assert.Equal(0d, MeasureMath.AreaSquareMeters(new List<PointLatLng> { new(0, 0), new(1, 1) }));
        Assert.Equal(0d, MeasureMath.AreaSquareMeters(null));
    }

    [Fact(DisplayName = "둘레는 닫는 변 포함 = 폴리라인 길이 + 마지막→첫")]
    public void should_include_closing_edge_when_perimeter()
    {
        var tri = new List<PointLatLng> { new(0, 0), new(0, 0.01), new(0.01, 0.01) };
        double open = MeasureMath.TotalLengthMeters(tri);
        double closing = MeasureMath.DistanceMeters(tri[2], tri[0]);
        Assert.Equal(open + closing, MeasureMath.PerimeterMeters(tri), 3);
    }

    // ── 단위 포맷 (임계·소수·소문자) ──

    [Theory(DisplayName = "거리 포맷: <1000 m→'F1 m', ≥1000→'F2 km'")]
    [InlineData(742.3, "742.3 m")]
    [InlineData(999.9, "999.9 m")]
    [InlineData(1000.0, "1.00 km")]
    [InlineData(1234.5, "1.23 km")]
    public void should_format_distance_by_threshold(double m, string expected)
        => Assert.Equal(expected, MeasureFormat.Distance(m));

    [Theory(DisplayName = "면적 포맷: <1만 m², <100만 ha, ≥100만 km²")]
    [InlineData(850.0, "850.0 m²")]
    [InlineData(10000.0, "1.00 ha")]
    [InlineData(34500.0, "3.45 ha")]
    [InlineData(1000000.0, "1.00 km²")]
    [InlineData(2100000.0, "2.10 km²")]
    public void should_format_area_by_threshold(double m2, string expected)
        => Assert.Equal(expected, MeasureFormat.Area(m2));

    [Fact(DisplayName = "값/단위 분리 — 칩 이색 렌더용")]
    public void should_split_value_and_unit()
    {
        var (v, u) = MeasureFormat.DistanceParts(1234.5);
        Assert.Equal("1.23", v);
        Assert.Equal("km", u);
        var (av, au) = MeasureFormat.AreaParts(34500);
        Assert.Equal("3.45", av);
        Assert.Equal("ha", au);
    }
}
