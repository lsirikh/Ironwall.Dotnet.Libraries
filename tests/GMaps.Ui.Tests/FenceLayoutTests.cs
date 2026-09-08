using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Fence;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>PRD pidsgroup-3d-fence-gate FR-02 — 기둥/패널/센서 노드 배치 규칙(스토리보드 §A-4). 헤드리스, WPF 무의존.</summary>
public class FenceLayoutTests
{
    private static FencePoint P(double x, double z) => new(x, z);

    [Theory]
    [InlineData(100, 3, 33)]
    [InlineData(66, 3, 22)]
    [InlineData(2, 3, 1)]     // 변이 간격보다 짧아도 패널 1
    [InlineData(4.4, 3, 1)]   // round(1.47)=1
    [InlineData(4.6, 3, 2)]   // round(1.53)=2
    public void should_place_round_l_over_s_panels_when_post_mode(double lengthM, double spacing, int expectedPanels)
    {
        var r = FenceLayout.Compute(new[] { P(0, 0), P(lengthM, 0) }, spacing, EnumFenceMode.Posts, false);
        Assert.Equal(expectedPanels, r.Panels.Count);
        Assert.Equal(expectedPanels + 1, r.Posts.Count);   // 양 끝 코너 + 중간 기둥
        Assert.Equal(0, r.RemainderM, 6);
    }

    [Fact]
    public void should_distribute_remainder_evenly_when_post_mode()
    {
        var r = FenceLayout.Compute(new[] { P(0, 0), P(10, 0) }, 3, EnumFenceMode.Posts, false);
        Assert.Equal(3, r.Panels.Count);
        Assert.All(r.Panels, p => Assert.Equal(10.0 / 3, p.LengthM, 6));
    }

    [Theory]
    [InlineData(10, 3, 3, 1.0)]    // floor(3.33)=3 노드, 잔여 1.0
    [InlineData(9, 3, 2, 0.0)]     // 3·3=9 = L → 마지막 노드는 코너 위라 제외, 잔여 0
    [InlineData(2, 3, 0, 2.0)]     // 노드 0, 잔여 패널 1
    public void should_floor_nodes_and_keep_exact_spacing_when_sensor_mode(double lengthM, double spacing, int expectedNodes, double expectedRemainder)
    {
        var r = FenceLayout.Compute(new[] { P(0, 0), P(lengthM, 0) }, spacing, EnumFenceMode.SensorMount, false);
        Assert.Equal(expectedNodes, r.Nodes.Count);
        Assert.Equal(expectedRemainder, r.RemainderM, 6);
        for (int k = 0; k < r.Nodes.Count; k++)
        {
            Assert.Equal((k + 1) * spacing, r.Nodes[k].Position.X, 6);   // 정확 간격
            Assert.Equal(k, r.Nodes[k].GlobalIndex);
        }
        Assert.Equal(2, r.Posts.Count);   // 센서 모드는 코너 기둥만
    }

    [Fact]
    public void should_put_corner_post_at_every_vertex()
    {
        var r = FenceLayout.Compute(new[] { P(0, 0), P(30, 0), P(30, 30), P(60, 30) }, 3, EnumFenceMode.Posts, false);
        var corners = r.Posts.Where(p => p.IsCorner).Select(p => p.Position).ToList();
        Assert.Equal(4, corners.Count);
        Assert.Contains(P(30, 0), corners);
        Assert.Contains(P(30, 30), corners);
    }

    [Theory]
    [InlineData(90, true)]     // 직각 → 내각 90 < 100 → 굵게
    [InlineData(135, false)]   // 둔각 → 얇게
    public void should_thicken_corner_when_interior_angle_lt_100(double interiorDeg, bool expectThick)
    {
        // A(0,0) → B(30,0) → C: B 에서 내각 interiorDeg 가 되도록 C 배치
        double rad = interiorDeg * Math.PI / 180.0;
        var c = P(30 - 30 * Math.Cos(rad), 30 * Math.Sin(rad));
        var r = FenceLayout.Compute(new[] { P(0, 0), P(30, 0), c }, 3, EnumFenceMode.Posts, false);
        var atB = r.Posts.Single(p => p.IsCorner && p.Position == P(30, 0));
        Assert.Equal(expectThick, atB.IsThick);
    }

    [Fact]
    public void should_include_closing_edge_when_closed_path()
    {
        var open = FenceLayout.Compute(new[] { P(0, 0), P(30, 0), P(30, 30) }, 3, EnumFenceMode.Posts, false);
        var closed = FenceLayout.Compute(new[] { P(0, 0), P(30, 0), P(30, 30) }, 3, EnumFenceMode.Posts, true);
        Assert.Equal(2, open.EdgeCount);
        Assert.Equal(3, closed.EdgeCount);
        Assert.True(closed.TotalLengthM > open.TotalLengthM);
        Assert.Equal(3, closed.Posts.Count(p => p.IsCorner));   // 닫힌 경로: 정점 3 = 코너 3(끝점 중복 없음)
    }

    [Fact]
    public void should_cut_panels_inside_gate_width_when_gate_within_1_5m()
    {
        var gate = new GateCut(P(15, 1.0), 4.0);   // 변에서 1.0m 떨어짐 → 스냅
        var r = FenceLayout.Compute(new[] { P(0, 0), P(30, 0) }, 3, EnumFenceMode.Posts, false, new[] { gate });
        // 절개 구간 [13,17] 안에는 패널이 없어야 한다
        Assert.DoesNotContain(r.Panels, p => (p.A.X + p.B.X) / 2 > 13 && (p.A.X + p.B.X) / 2 < 17);
        Assert.True(r.Panels.Sum(p => p.LengthM) < 30 - 4 + 1e-6);
        Assert.Equal(30 - 4, r.Panels.Sum(p => p.LengthM), 6);   // 잘린 만큼만 줄어든다
    }

    [Fact]
    public void should_ignore_gate_when_farther_than_1_5m()
    {
        var gate = new GateCut(P(15, 2.0), 4.0);
        var r = FenceLayout.Compute(new[] { P(0, 0), P(30, 0) }, 3, EnumFenceMode.Posts, false, new[] { gate });
        Assert.Equal(30, r.Panels.Sum(p => p.LengthM), 6);
    }

    [Fact]
    public void should_merge_cuts_when_two_gates_adjacent()
    {
        var gates = new[] { new GateCut(P(10, 0), 4.0), new GateCut(P(13, 0), 4.0) };   // [8,12] ∪ [11,15] = [8,15]
        var r = FenceLayout.Compute(new[] { P(0, 0), P(30, 0) }, 3, EnumFenceMode.Posts, false, gates);
        Assert.Equal(30 - 7, r.Panels.Sum(p => p.LengthM), 6);
    }

    [Fact]
    public void should_return_empty_when_less_than_two_points()
    {
        Assert.Equal(0, FenceLayout.Compute(new[] { P(0, 0) }, 3, EnumFenceMode.Posts, false).EdgeCount);
        Assert.Equal(0, FenceLayout.Compute(Array.Empty<FencePoint>(), 3, EnumFenceMode.Posts, false).EdgeCount);
    }

    [Fact]
    public void should_fallback_to_default_spacing_when_spacing_invalid()
    {
        var r = FenceLayout.Compute(new[] { P(0, 0), P(30, 0) }, double.NaN, EnumFenceMode.Posts, false);
        Assert.Equal(10, r.Panels.Count);   // 30 / 3.0
    }

    [Fact]
    public void should_convert_geo_to_local_meters_with_east_north_axes()
    {
        var local = FenceLayout.ToLocalMeters(new[] { (37.0, 127.0), (37.0009, 127.0), (37.0, 127.00112) });
        Assert.Equal(0, local[0].X, 6); Assert.Equal(0, local[0].Z, 6);
        Assert.Equal(100.0, local[1].Z, 0.5);    // 위도 +0.0009° ≈ 100m 북
        Assert.Equal(0, local[1].X, 1e-6);
        Assert.Equal(99.5, local[2].X, 1.0);     // 경도 +0.00112° ≈ 99.5m 동(위도 37°)
    }

    [Fact]
    public void should_build_summary_label_for_both_modes()
    {
        var pts = new[] { P(0, 0), P(10, 0) };
        var posts = FenceLayout.Compute(pts, 3, EnumFenceMode.Posts, false);
        var sensors = FenceLayout.Compute(pts, 3, EnumFenceMode.SensorMount, false);
        Assert.Equal("기둥 4개 · 실간격 3.33 m", FenceLayout.Summary(posts, 3, EnumFenceMode.Posts));
        Assert.Equal("센서 3개 · 간격 3.0 m · 잔여 1.0 m", FenceLayout.Summary(sensors, 3, EnumFenceMode.SensorMount));
    }
}
