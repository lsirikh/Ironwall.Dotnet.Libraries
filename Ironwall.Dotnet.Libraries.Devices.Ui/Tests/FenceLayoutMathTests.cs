using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Fences;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 펜스 배치 순수 함수(fence-wiring-editor FR-01 · FR-07 · FR-08 · FR-09 · NFR-01) — 망 목록 → 기둥 · 망(m), 자리 → 좌표 · 순서,
/// 체인이 바뀔 때 자리 맞추기, 제안, 기준 적용 차이.
/// </summary>
public class FenceLayoutMathTests
{
    private static FencePanelSpec P(double span, EnumFenceStyle style = EnumFenceStyle.ChainLink, double? height = null)
        => new(style, null, height ?? FencePanelSpec.DefaultHeight(style), span);

    #region - Geometry -
    [Fact]
    public void should_lay_twelve_panels_of_mixed_spans_end_to_end_when_the_geometry_is_built()
    {
        // Arrange — 12칸: 6m × 7 · 3m × 5
        var panels = Enumerable.Repeat(P(6), 7).Concat(Enumerable.Repeat(P(3, EnumFenceStyle.ChainLinkRazor), 5)).ToList();

        // Act
        var g = FenceLayoutMath.Geometry(panels);

        // Assert
        Assert.Equal(12, g.Panels.Count);
        Assert.Equal(13, g.Posts.Count);
        Assert.Equal(57, g.LengthM, 6);
        Assert.Equal(42, g.Posts[7].XM, 6);                    // 7칸 × 6m
        Assert.Equal(45, g.Posts[8].XM, 6);
        Assert.Equal(43.5, g.Panels[7].CenterM, 6);
        Assert.False(g.Posts[6].HasRazor);
        Assert.True(g.Posts[7].HasRazor);                      // 윤형 망 옆 기둥
        Assert.All(g.Posts, p => Assert.True(p.Exists));
    }

    [Fact]
    public void should_not_stand_a_post_between_two_walls_but_keep_the_post_next_to_a_fence_panel_when_walls_are_mixed()
    {
        var panels = new[] { P(6), P(4, EnumFenceStyle.Brick), P(4, EnumFenceStyle.Concrete), P(6, EnumFenceStyle.DesignFence, 3.0) };

        var g = FenceLayoutMath.Geometry(panels);

        Assert.Equal(new[] { true, true, false, true, true }, g.Posts.Select(p => p.Exists));
        Assert.Equal(3.0, g.Posts[3].HeightM, 6);              // 담 옆 기둥 = 펜스 망 높이
        Assert.Equal(3.0, g.Posts[4].HeightM, 6);
    }

    [Fact]
    public void should_clamp_height_and_span_into_range_when_a_panel_is_normalized()
    {
        var spec = new FencePanelSpec(EnumFenceStyle.ChainLink, "abc", 9, 0.1).Normalized();

        Assert.Equal(FencePanelSpec.MAX_HEIGHT_M, spec.HeightM);
        Assert.Equal(FencePanelSpec.MIN_SPAN_M, spec.SpanM);
        Assert.Equal("#AABBCC", spec.Color);
        Assert.Null(FencePanelSpec.NormalizeColor("not a colour"));
    }
    #endregion

    #region - Seats · order · points -
    [Fact]
    public void should_order_post_top_before_the_panel_center_when_both_are_on_the_same_panel()
    {
        var mounts = new[]
        {
            (Key: 1, Mount: new SensorMountSpec(0, FenceMountSpot.PanelCenter)),     // 망 0 가운데
            (Key: 2, Mount: new SensorMountSpec(1, FenceMountSpot.PostTop)),         // 기둥 1
            (Key: 3, Mount: new SensorMountSpec(0, FenceMountSpot.PostTop)),         // 기둥 0
            (Key: 4, Mount: new SensorMountSpec(0, FenceMountSpot.PostMiddle)),      // 기둥 0 중간
        };

        var order = FenceLayoutMath.PositionOrder(mounts);

        Assert.Equal(new[] { 3, 4, 1, 2 }, order);
    }

    [Fact]
    public void should_keep_the_chain_order_for_sensors_on_the_same_spot_when_ordering()
    {
        var spot = new SensorMountSpec(2, FenceMountSpot.PostTop);

        var order = FenceLayoutMath.PositionOrder(new[] { (7, spot), (5, spot), (6, spot) }, tieOrder: new[] { 6, 7, 5 });

        Assert.Equal(new[] { 6, 7, 5 }, order);
    }

    [Theory]
    [InlineData(FenceMountSpot.PostTop, 1, FenceMountSpot.WallTop, 1)]       // 벽돌 사이 기둥 1 은 서지 않는다 → 망 1 담 위
    [InlineData(FenceMountSpot.PostMiddle, 2, FenceMountSpot.WallFace, 2)]
    [InlineData(FenceMountSpot.PanelCenter, 1, FenceMountSpot.WallFace, 1)]  // 담 위의 망 가운데 = 담 앞면
    [InlineData(FenceMountSpot.WallTop, 3, FenceMountSpot.PanelCenter, 3)]   // 펜스 망의 담 자리 = 망 가운데
    [InlineData(FenceMountSpot.PostTop, 99, FenceMountSpot.PostTop, 4)]      // 범위 밖 = 끝 기둥
    public void should_fit_the_spot_to_the_panel_style_when_normalized(FenceMountSpot spot, int panel, FenceMountSpot expectedSpot, int expectedPanel)
    {
        var panels = new[] { P(6, EnumFenceStyle.Brick), P(6, EnumFenceStyle.Brick), P(6, EnumFenceStyle.Concrete), P(6), };

        var m = FenceLayoutMath.Normalize(new SensorMountSpec(panel, spot), panels);

        Assert.Equal(expectedSpot, m.Spot);
        Assert.Equal(expectedPanel, m.Panel);
    }

    [Fact]
    public void should_place_sensors_at_post_top_middle_and_panel_center_heights_plus_offset_when_pointed()
    {
        var g = FenceLayoutMath.Geometry(new[] { P(6, height: 2.4), P(4, height: 3.0) });

        var top = FenceLayoutMath.PointOf(new SensorMountSpec(1, FenceMountSpot.PostTop, -0.3), g);
        var middle = FenceLayoutMath.PointOf(new SensorMountSpec(0, FenceMountSpot.PostMiddle), g);
        var center = FenceLayoutMath.PointOf(new SensorMountSpec(1, FenceMountSpot.PanelCenter), g);

        Assert.Equal(6, top.XM, 6);
        Assert.Equal(2.7, top.HeightM, 6);                    // 기둥 1 = 두 망 중 높은 3.0 − 0.3
        Assert.Equal(1.2, middle.HeightM, 6);
        Assert.Equal(8, center.XM, 6);
        Assert.Equal(1.5, center.HeightM, 6);
    }

    [Fact]
    public void should_find_the_nearest_post_and_the_containing_panel_when_a_drag_points_at_x()
    {
        var g = FenceLayoutMath.Geometry(new[] { P(6), P(6), P(3) });

        Assert.Equal(1, FenceLayoutMath.PostIndexNear(g, 7.9));
        Assert.Equal(2, FenceLayoutMath.PostIndexNear(g, 10.0));
        Assert.Equal(1, FenceLayoutMath.PanelIndexAt(g, 7.9));
        Assert.Equal(2, FenceLayoutMath.PanelIndexAt(g, 40));   // 끝 밖 = 끝 망
    }
    #endregion

    #region - Chain ↔ seats -
    private static readonly IReadOnlyList<FencePanelSpec> Six = Enumerable.Repeat(P(6), 6).ToList();

    private static Dictionary<int, SensorMountSpec> OnPosts(params int[] keys)
        => keys.Select((k, i) => (k, i)).ToDictionary(t => t.k, t => new SensorMountSpec(t.i, FenceMountSpot.PostTop));

    [Fact]
    public void should_let_sensors_trade_seats_in_the_new_order_when_the_chain_is_reordered()
    {
        // Arrange — 기둥 0…4 에 센서 1…5
        var mounts = OnPosts(1, 2, 3, 4, 5);

        // Act — 개념도에서 1 을 4 뒤로
        var (after, _) = FenceLayoutMath.Reconcile(new[] { 1, 2, 3, 4, 5 }, new[] { 2, 3, 4, 1, 5 }, mounts, Six, _ => FenceSensorCategory.Smart);

        // Assert — 자리 묶음은 그대로, 1 이 기둥 3 으로 가고 2 · 3 · 4 가 한 칸씩 당겨진다
        Assert.Equal(3, after[1].Panel);
        Assert.Equal(new[] { 0, 1, 2, 4 }, new[] { 2, 3, 4, 5 }.Select(k => after[k].Panel));
        Assert.Equal(new[] { 2, 3, 4, 1, 5 }, FenceLayoutMath.PositionOrder(after.Select(p => (p.Key, p.Value))));
    }

    [Fact]
    public void should_seat_an_appended_sensor_after_the_last_and_grow_the_fence_when_there_is_no_room()
    {
        var mounts = OnPosts(1, 2, 3, 4, 5, 6, 7);             // 기둥 0…6 = 6칸 펜스의 끝 기둥까지

        var (after, panels) = FenceLayoutMath.Reconcile(new[] { 1, 2, 3, 4, 5, 6, 7 }, new[] { 1, 2, 3, 4, 5, 6, 7, 8 },
            mounts, Six, _ => FenceSensorCategory.Smart);

        Assert.Equal(new SensorMountSpec(7, FenceMountSpot.PostTop), after[8]);
        Assert.Equal(7, panels.Count);                          // 끝 망을 본떠 한 칸 늘었다
    }

    [Fact]
    public void should_seat_it_between_its_neighbours_when_a_fence_sensor_is_inserted()
    {
        var mounts = OnPosts(1, 2, 3);

        var (after, panels) = FenceLayoutMath.Reconcile(new[] { 1, 2, 3 }, new[] { 1, 9, 2, 3 }, mounts, Six,
            k => k == 9 ? FenceSensorCategory.Fence : FenceSensorCategory.Smart);

        Assert.Equal(new SensorMountSpec(0, FenceMountSpot.PanelCenter), after[9]);  // 기둥 0 과 기둥 1 사이 망 0
        Assert.Equal(6, panels.Count);
        Assert.Equal(new[] { 1, 9, 2, 3 }, FenceLayoutMath.PositionOrder(after.Select(p => (p.Key, p.Value)), new[] { 1, 9, 2, 3 }));
    }

    [Fact]
    public void should_share_the_next_seat_when_no_free_spot_lies_between_the_neighbours()
    {
        var mounts = new Dictionary<int, SensorMountSpec>
        {
            [1] = new(0, FenceMountSpot.PostTop),
            [2] = new(0, FenceMountSpot.PanelCenter),
        };

        var (after, _) = FenceLayoutMath.Reconcile(new[] { 1, 2 }, new[] { 1, 9, 2 }, mounts, Six, _ => FenceSensorCategory.Smart);

        Assert.Equal(mounts[2], after[9]);                      // 사이에 칸이 없다 → 뒤 이웃과 같은 자리(체인 순서로 가른다)
        Assert.Equal(new[] { 1, 9, 2 }, FenceLayoutMath.PositionOrder(after.Select(p => (p.Key, p.Value)), new[] { 1, 9, 2 }));
    }

    [Fact]
    public void should_empty_the_seat_of_a_removed_sensor_and_keep_the_others_when_a_sensor_leaves_the_chain()
    {
        var mounts = OnPosts(1, 2, 3, 4);

        var (after, _) = FenceLayoutMath.Reconcile(new[] { 1, 2, 3, 4 }, new[] { 1, 3, 4 }, mounts, Six, _ => FenceSensorCategory.Smart);

        Assert.False(after.ContainsKey(2));
        Assert.Equal(new[] { 0, 2, 3 }, new[] { 1, 3, 4 }.Select(k => after[k].Panel));
    }

    [Fact]
    public void should_keep_every_seat_when_the_new_chain_already_follows_the_seats()
    {
        var mounts = OnPosts(1, 2, 3);
        mounts[2] = new SensorMountSpec(1, FenceMountSpot.PanelCenter);

        var (after, _) = FenceLayoutMath.Reconcile(new[] { 1, 2, 3 }, new[] { 1, 2, 3 }, mounts, Six, _ => FenceSensorCategory.Smart);

        Assert.Equal(mounts, after);
    }
    #endregion

    #region - Proposal (FR-01) -
    [Fact]
    public void should_stand_posts_at_every_smart_sensor_when_proposing_a_six_metre_ring()
    {
        var chain = Enumerable.Range(0, 13).Select(i => (Key: 100 + i, Category: FenceSensorCategory.Smart, Metres: i * 6.0)).ToList();

        var (panels, mounts) = FenceLayoutMath.Propose(chain, 3);

        Assert.Equal(12, panels.Count);
        Assert.All(panels, p => Assert.Equal(6, p.SpanM, 6));
        Assert.All(panels, p => Assert.Equal(EnumFenceStyle.ChainLink, p.Style));
        Assert.Equal(Enumerable.Range(0, 13), chain.Select(s => mounts[s.Key].Panel));
        Assert.All(mounts.Values, m => Assert.Equal(FenceMountSpot.PostTop, m.Spot));
    }

    [Fact]
    public void should_put_fence_sensors_in_the_middle_of_their_own_panel_and_split_long_empty_spans_when_proposing()
    {
        // 복합(0m) · 펜스 3대(3 · 6 · 9m) · 복합(20m)
        var chain = new List<(int Key, FenceSensorCategory Category, double Metres)>
        {
            (1, FenceSensorCategory.Multi, 0), (2, FenceSensorCategory.Fence, 3), (3, FenceSensorCategory.Fence, 6),
            (4, FenceSensorCategory.Fence, 9), (5, FenceSensorCategory.Multi, 20),
        };

        var (panels, mounts) = FenceLayoutMath.Propose(chain, 3);
        var g = FenceLayoutMath.Geometry(panels);
        var order = FenceLayoutMath.PositionOrder(mounts.Select(p => (p.Key, p.Value)));

        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, order);                       // 제안도 체인 순서를 지킨다
        Assert.Equal(FenceMountSpot.PanelCenter, mounts[3].Spot);
        Assert.Equal(6, FenceLayoutMath.PointOf(mounts[3], g).XM, 6);       // 펜스센서는 제 칸 가운데
        Assert.Equal(20, FenceLayoutMath.PointOf(mounts[5], g).XM, 6);
        Assert.All(panels, p => Assert.True(p.SpanM <= FenceLayoutMath.PROPOSE_MAX_SPAN_M + 1e-6, $"{p.SpanM}"));
    }

    [Fact]
    public void should_cover_the_ground_with_six_metre_panels_when_only_underground_sensors_exist()
    {
        var chain = Enumerable.Range(0, 3).Select(i => (Key: i + 1, Category: FenceSensorCategory.Underground, Metres: i * 12.0)).ToList();

        var (panels, mounts) = FenceLayoutMath.Propose(chain, 3);

        Assert.True(panels.Count >= 2);
        Assert.All(mounts.Values, m => Assert.Equal(FenceMountSpot.PanelCenter, m.Spot));
        Assert.Equal(new[] { 1, 2, 3 }, FenceLayoutMath.PositionOrder(mounts.Select(p => (p.Key, p.Value))));
    }
    #endregion

    #region - Apply to all (FR-08) -
    [Fact]
    public void should_count_only_the_sensors_that_change_when_a_mount_style_is_applied_to_all()
    {
        // 13대 중 3대는 이미 기둥 위 −0.3
        var mounts = Enumerable.Range(0, 13).ToDictionary(i => i, i => new SensorMountSpec(i % 6, FenceMountSpot.PostTop, i < 3 ? -0.3 : 0));

        var (after, changed) = FenceLayoutMath.ApplyMountStyle(mounts, mounts.Keys, FenceMountSpot.PostTop, -0.3, Six);

        Assert.Equal(10, changed.Count);                                     // "13대 중 10대가 바뀝니다"
        Assert.All(after.Values, m => Assert.Equal(-0.3, m.HeightOffsetM, 6));
        Assert.Equal(mounts.Select(p => p.Value.Panel), after.Select(p => p.Value.Panel));   // 제 기둥에 그대로
    }

    [Fact]
    public void should_use_the_same_meaning_spot_on_a_wall_when_a_post_style_is_applied()
    {
        var panels = new[] { P(6), P(6, EnumFenceStyle.Brick), P(6, EnumFenceStyle.Brick) };
        var mounts = new Dictionary<int, SensorMountSpec>
        {
            [1] = new(0, FenceMountSpot.PanelCenter),
            [2] = new(2, FenceMountSpot.WallFace),
        };

        var (after, changed) = FenceLayoutMath.ApplyMountStyle(mounts, new[] { 1, 2 }, FenceMountSpot.PostTop, 0, panels);

        Assert.Equal(new SensorMountSpec(0, FenceMountSpot.PostTop), after[1]);    // 망 0 의 왼쪽 기둥
        Assert.Equal(new SensorMountSpec(2, FenceMountSpot.WallTop), after[2]);    // 담에서는 같은 뜻의 "담 위"
        Assert.Equal(2, changed.Count);
    }
    #endregion
}
