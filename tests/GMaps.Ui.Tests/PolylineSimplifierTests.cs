using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Fence;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>PRD FR-01/D7 — 드래그 스트로크 단순화(ε=2px)와 최소 점 간격(1m).</summary>
public class PolylineSimplifierTests
{
    [Fact]
    public void should_keep_endpoints_and_drop_collinear_when_douglas_peucker()
    {
        var pts = new List<(double X, double Y)>();
        for (int i = 0; i <= 100; i++) pts.Add((i, 0.3 * ((i % 2 == 0) ? 1 : -1)));   // 직선 + 0.3px 지터
        var r = PolylineSimplifier.DouglasPeucker(pts, 2.0);
        Assert.Equal(2, r.Count);
        Assert.Equal(pts[0], r[0]); Assert.Equal(pts[^1], r[^1]);
    }

    [Fact]
    public void should_keep_corner_when_deviation_exceeds_epsilon()
    {
        var pts = new List<(double X, double Y)>();
        for (int i = 0; i <= 50; i++) pts.Add((i, 0));
        for (int i = 1; i <= 50; i++) pts.Add((50, i));
        var r = PolylineSimplifier.DouglasPeucker(pts, 2.0);
        Assert.Equal(3, r.Count);
        Assert.Equal((50.0, 0.0), r[1]);
    }

    [Fact]
    public void should_remove_duplicates_when_epsilon_zero()
    {
        var r = PolylineSimplifier.DouglasPeucker(new[] { (0.0, 0.0), (0.0, 0.0), (1.0, 1.0), (1.0, 1.0), (2.0, 0.0) }, 0);
        Assert.Equal(3, r.Count);
    }

    [Fact]
    public void should_return_empty_or_single_when_input_tiny()
    {
        Assert.Empty(PolylineSimplifier.DouglasPeucker(Array.Empty<(double, double)>(), 2));
        Assert.Single(PolylineSimplifier.DouglasPeucker(new[] { (1.0, 1.0) }, 2));
    }

    [Fact]
    public void should_enforce_min_spacing_and_always_keep_last_point()
    {
        // 0, 0.4, 0.8, 1.2, 5, 5.3 (m) — 1m 간격 → 0, 1.2, 5.3(마지막이 5 를 대체)
        var pts = new[] { 0.0, 0.4, 0.8, 1.2, 5.0, 5.3 };
        var r = PolylineSimplifier.EnforceMinSpacing(pts, 1.0, (a, b) => Math.Abs(a - b));
        Assert.Equal(new[] { 0.0, 1.2, 5.3 }, r);
    }

    [Fact]
    public void should_keep_two_points_when_all_too_close()
    {
        var r = PolylineSimplifier.EnforceMinSpacing(new[] { 0.0, 0.2, 0.4 }, 1.0, (a, b) => Math.Abs(a - b));
        Assert.Equal(new[] { 0.0, 0.4 }, r);   // 라인 성립을 위해 2점 유지
    }

    [Fact]
    public void should_measure_perpendicular_distance_with_segment_clamping()
    {
        Assert.Equal(1.0, PolylineSimplifier.PerpendicularDistance((5, 1), (0, 0), (10, 0)), 9);
        Assert.Equal(Math.Sqrt(2), PolylineSimplifier.PerpendicularDistance((-1, -1), (0, 0), (10, 0)), 9);   // 끝점 밖은 끝점 거리
    }
}
