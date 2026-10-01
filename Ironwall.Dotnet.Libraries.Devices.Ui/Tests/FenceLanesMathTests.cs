using Ironwall.Dotnet.Monitoring.Models.Fences;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 두 줄(레인) 사슬 · 번호 방향 · 문서 옮기기 · VBus 기본(fence-wiring-editor v0.3 §1-0b · FR-18 ~ FR-21) — 순수 함수.
/// 그림 ①(4차 두 줄 · 제어기 왼쪽) · ②(한 줄 + 리턴선) × 제어기 왼쪽 · 오른쪽.
/// </summary>
public class FenceLanesMathTests
{
    /// <summary>아래 줄 키 1…n(기둥 0…n−1 위) · 위 줄 키 101…(기둥 0… 위).</summary>
    private static List<(int Key, SensorMountSpec Mount)> TwoLanes(int lower, int upper)
        => Enumerable.Range(0, lower).Select(i => (i + 1, new SensorMountSpec(i, FenceMountSpot.PostTop)))
            .Concat(Enumerable.Range(0, upper).Select(i => (101 + i, new SensorMountSpec(i, FenceMountSpot.PostTop, Lane: FenceLane.Upper))))
            .ToList();

    [Fact]
    public void should_run_the_lower_lane_away_from_the_controller_then_the_upper_lane_back_when_the_controller_is_left()
    {
        var order = FenceLayoutMath.ChainOrder(TwoLanes(3, 3), FenceControllerEnd.Left);

        Assert.Equal(new[] { 1, 2, 3, 103, 102, 101 }, order);
    }

    [Fact]
    public void should_run_from_the_right_end_when_the_controller_is_right()
    {
        var order = FenceLayoutMath.ChainOrder(TwoLanes(3, 3), FenceControllerEnd.Right);

        Assert.Equal(new[] { 3, 2, 1, 101, 102, 103 }, order);
    }

    [Theory]
    [InlineData(FenceControllerEnd.Left, new[] { 1, 2, 3, 4 })]
    [InlineData(FenceControllerEnd.Right, new[] { 4, 3, 2, 1 })]
    public void should_order_a_single_lane_from_the_controller_end_when_the_upper_lane_is_only_the_return(FenceControllerEnd end, int[] expected)
    {
        var order = FenceLayoutMath.ChainOrder(TwoLanes(4, 0), end);

        Assert.Equal(expected, order);
    }

    [Theory]
    [InlineData(FenceControllerEnd.Left)]
    [InlineData(FenceControllerEnd.Right)]
    public void should_number_each_lane_away_from_the_controller_when_the_default_direction_is_used(FenceControllerEnd end)
    {
        // Arrange
        var mounts = TwoLanes(3, 3);
        var lanes = mounts.ToDictionary(t => t.Key, t => t.Mount.Lane);
        var chain = FenceLayoutMath.ChainOrder(mounts, end);

        // Act
        var away = FenceLayoutMath.NumberingOrder(chain, k => lanes[k]);
        var along = FenceLayoutMath.NumberingOrder(chain, k => lanes[k], FenceNumberingDirection.AlongChain);

        // Assert — 왼쪽 제어기: 아래 1→3 · 위 101→103 모두 왼쪽 → 오른쪽(그림 ①). 오른쪽 제어기면 오른쪽부터.
        var expected = end == FenceControllerEnd.Left ? new[] { 1, 2, 3, 101, 102, 103 } : new[] { 3, 2, 1, 103, 102, 101 };
        Assert.Equal(expected, away);
        Assert.Equal(chain, along);
    }

    [Fact]
    public void should_number_the_upper_lane_left_to_right_like_reference_one_when_tier4_bands_are_assigned()
    {
        var mounts = TwoLanes(6, 6);
        var lanes = mounts.ToDictionary(t => t.Key, t => t.Mount.Lane);
        var chain = FenceLayoutMath.ChainOrder(mounts, FenceControllerEnd.Left);
        var order = FenceLayoutMath.NumberingOrder(chain, k => lanes[k]);

        var numbers = NumberingMath.Assign(order.Select(k => (k, k > 100 ? FenceSensorCategory.Fence : FenceSensorCategory.Smart)), NumberBandSet.Tier4);

        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, Enumerable.Range(1, 6).Select(k => numbers[k]));
        Assert.Equal(new[] { 101, 102, 103, 104, 105, 106 }, Enumerable.Range(101, 6).Select(k => numbers[k]));   // 위 줄도 왼쪽부터
    }

    [Fact]
    public void should_read_a_schema_one_document_as_all_lower_lane_with_the_controller_left_when_migrating()
    {
        // Arrange — 줄 칸이 없던 옛 본문
        const string json = """{"schema":1,"controller_id":7,"panels":[{"style":"ChainLink","height_m":2.4,"span_m":6}],"mounts":{"11":{"panel":0,"spot":"PostTop"},"12":{"panel":1,"spot":"PostTop"}}}""";

        // Act
        var document = FenceLayoutJson.Deserialize(json)!;

        // Assert
        Assert.Equal(FenceLayoutDocument.SCHEMA, document.Schema);
        Assert.All(document.Mounts.Values, m => Assert.Equal(FenceLane.Lower, m.Lane));
        Assert.Equal(FenceControllerEnd.Left, document.ControllerEnd);
        Assert.Null(document.VbusGap);
    }

    [Fact]
    public void should_round_trip_lane_controller_end_and_vbus_gap_as_names_when_serialized()
    {
        var document = new FenceLayoutDocument
        {
            ControllerId = 7,
            Panels = new[] { FencePanelSpec.Default() },
            Mounts = new Dictionary<int, SensorMountSpec> { [11] = new(0, FenceMountSpot.PostTop, Lane: FenceLane.Upper) },
            ControllerEnd = FenceControllerEnd.Right,
            VbusGap = 3,
        };

        var json = FenceLayoutJson.Serialize(document);
        var back = FenceLayoutJson.Deserialize(json)!;

        Assert.Contains("\"lane\":\"Upper\"", json);
        Assert.Contains("\"controller_end\":\"Right\"", json);
        Assert.Equal(FenceLane.Upper, back.Mounts[11].Lane);
        Assert.Equal(FenceControllerEnd.Right, back.ControllerEnd);
        Assert.Equal(3, back.VbusGap);
    }

    [Theory]
    [InlineData(null, 6, 3)]
    [InlineData(null, 7, 3)]
    [InlineData(9, 6, 5)]
    [InlineData(0, 6, 1)]
    [InlineData(null, 1, 0)]
    public void should_put_the_vbus_between_the_middle_sensors_by_default_and_keep_it_inside_the_chain_when_stored(int? stored, int count, int expected)
        => Assert.Equal(expected, FenceLayoutMath.VbusGapOf(stored, count));

    [Theory]
    [InlineData(NumberBandSet.PRESET_TIER4, FenceSensorCategory.Fence, FenceLane.Upper)]
    [InlineData(NumberBandSet.PRESET_TIER4, FenceSensorCategory.Smart, FenceLane.Lower)]
    [InlineData(NumberBandSet.PRESET_TIER3, FenceSensorCategory.Fence, FenceLane.Lower)]
    public void should_choose_the_default_lane_by_band_preset_and_category(string preset, FenceSensorCategory category, FenceLane expected)
    {
        var bands = NumberBandSet.Presets.Single(p => p.Preset == preset);

        Assert.Equal(expected, FenceLayoutMath.DefaultLane(category, bands));
        Assert.Equal(FenceLane.Lower, FenceLayoutMath.DefaultLane(category, null));
    }

    [Fact]
    public void should_hand_over_the_lane_with_the_position_but_keep_spot_and_height_when_two_sensors_swap_across_the_turn()
    {
        // Arrange — 아래 줄 끝(기둥 2) 1 · 위 줄 끝(기둥 2 · 기둥 중간 +0.3m) 101. 사슬 1 → 101(꺾이는 곳)
        var panels = Enumerable.Repeat(FencePanelSpec.Default(), 3).ToList();
        var mounts = new Dictionary<int, SensorMountSpec>
        {
            [1] = new(2, FenceMountSpot.PostTop),
            [101] = new(2, FenceMountSpot.PostMiddle, 0.3, false, FenceLane.Upper),
        };

        // Act — 사슬에서 둘을 바꾼다
        var (result, _) = FenceLayoutMath.Reconcile(new[] { 1, 101 }, new[] { 101, 1 }, mounts, panels, _ => FenceSensorCategory.Other);

        // Assert
        Assert.Equal(FenceLane.Lower, result[101].Lane);
        Assert.Equal(FenceMountSpot.PostMiddle, result[101].Spot);
        Assert.Equal(0.3, result[101].HeightOffsetM);
        Assert.Equal(FenceLane.Upper, result[1].Lane);
        Assert.Equal(FenceMountSpot.PostTop, result[1].Spot);
    }

    [Fact]
    public void should_keep_the_upper_lane_order_when_a_new_sensor_is_appended_after_it_with_the_controller_left()
    {
        // Arrange — 위 줄(먼 끝 → 제어기 쪽) 103 · 102 다음에 새 센서
        var panels = Enumerable.Repeat(FencePanelSpec.Default(), 4).ToList();
        var mounts = new Dictionary<int, SensorMountSpec>
        {
            [1] = new(0, FenceMountSpot.PostTop),
            [103] = new(3, FenceMountSpot.PostTop, Lane: FenceLane.Upper),
            [102] = new(2, FenceMountSpot.PostTop, Lane: FenceLane.Upper),
        };

        // Act
        var (result, _) = FenceLayoutMath.Reconcile(new[] { 1, 103, 102 }, new[] { 1, 103, 102, 999 }, mounts, panels, _ => FenceSensorCategory.Smart);

        // Assert — 새 센서는 위 줄에서 102 의 왼쪽(제어기 쪽)
        Assert.Equal(FenceLane.Upper, result[999].Lane);
        Assert.Equal(new[] { 1, 103, 102, 999 }, FenceLayoutMath.ChainOrder(result.Select(p => (p.Key, p.Value)), FenceControllerEnd.Left, new[] { 1, 103, 102, 999 }));
    }
}
