using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Fence;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>PRD FR-01 — 드래그 스트로크 축약(ε=2px, 최소 1 m, 기존 정점 seed).</summary>
public class StrokeReducerTests
{
    // 1 px = 0.5 m 인 가상 지도: 투영은 항등, 거리는 px·0.5
    private static (double X, double Y) Proj((double X, double Y) p) => p;
    private static double Dist((double X, double Y) a, (double X, double Y) b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y)) * .5;

    [Fact]
    public void should_reduce_jittery_straight_stroke_to_two_vertices()
    {
        var pts = Enumerable.Range(0, 200).Select(i => ((double)i, .4 * (i % 2 == 0 ? 1 : -1))).ToList();
        var r = StrokeReducer.Reduce(pts, 2.0, Proj, Dist, 1.0);
        Assert.Equal(2, r.Count); Assert.Equal((0.0, .4), r[0]); Assert.Equal((199.0, -.4), r[^1]);
    }

    [Fact]
    public void should_keep_corner_and_enforce_min_spacing()
    {
        var pts = new List<(double, double)>();
        for (int i = 0; i <= 40; i++) pts.Add((i, 0));
        for (int i = 1; i <= 40; i++) pts.Add((40, i));
        pts.Add((40.5, 40.5));   // 코너 뒤 0.35 m 지터
        var r = StrokeReducer.Reduce(pts, 2.0, Proj, Dist, 1.0);
        Assert.Equal(3, r.Count);
        Assert.Equal((40.0, 0.0), r[1]);
        Assert.Equal((40.5, 40.5), r[^1]);   // 마지막 점은 항상 유지(1 m 미만이면 앞 점을 대체)
    }

    [Fact]
    public void should_drop_first_vertex_when_too_close_to_existing_last_vertex()
    {
        var pts = new List<(double, double)> { (0.5, 0), (10, 0), (20, 0) };
        var r = StrokeReducer.Reduce(pts, 2.0, Proj, Dist, 1.0, seed: (0.0, 0.0), hasSeed: true);
        Assert.Equal(new[] { (20.0, 0.0) }, r);   // (0.5,0) 는 seed 와 0.25 m → 제거, 직선이라 중간점도 제거
    }

    [Fact]
    public void should_return_nothing_when_stroke_stays_at_seed()
    {
        var r = StrokeReducer.Reduce(new[] { (0.2, 0.1), (0.6, 0.2) }, 2.0, Proj, Dist, 1.0, seed: (0.0, 0.0), hasSeed: true);
        Assert.Empty(r);
    }

    [Fact]
    public void should_return_empty_when_input_empty()
        => Assert.Empty(StrokeReducer.Reduce(Array.Empty<(double, double)>(), 2.0, Proj, Dist, 1.0));
}
