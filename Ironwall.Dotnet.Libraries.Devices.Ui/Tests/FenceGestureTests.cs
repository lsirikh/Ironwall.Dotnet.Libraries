using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Signals;
using System.Linq;
using System.Windows;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>제스처 판정 · 러버밴드 적중 · 신호등 판정(fence-wiring-editor FR-04 ~ FR-06 · FR-14 · NFR-01) — 순수 표 시험.</summary>
public class FenceGestureTests
{
    #region - Gesture table -
    [Theory]
    // 오른쪽: 데드존 안에서 뗌 = 메뉴 · 넘으면 이동(PRD §5 "오른쪽 5px 뗌 = 메뉴 · 12px = 이동")
    [InlineData(FencePointerButton.Right, false, false, FenceTargetKind.Sensor, 5, FenceGestureAction.ContextMenu)]
    [InlineData(FencePointerButton.Right, false, false, FenceTargetKind.Sensor, 12, FenceGestureAction.Pan)]
    [InlineData(FencePointerButton.Right, false, false, FenceTargetKind.Empty, 3, FenceGestureAction.ContextMenu)]
    // 가운데: 끌면 이동 · 클릭은 없음
    [InlineData(FencePointerButton.Middle, false, false, FenceTargetKind.Panel, 20, FenceGestureAction.Pan)]
    [InlineData(FencePointerButton.Middle, false, false, FenceTargetKind.Panel, 2, FenceGestureAction.None)]
    // 왼쪽 끌기
    [InlineData(FencePointerButton.Left, false, false, FenceTargetKind.Empty, 30, FenceGestureAction.RubberSensors)]
    [InlineData(FencePointerButton.Left, false, false, FenceTargetKind.Panel, 30, FenceGestureAction.RubberSensors)]
    [InlineData(FencePointerButton.Left, false, true, FenceTargetKind.Panel, 30, FenceGestureAction.RubberPanels)]
    [InlineData(FencePointerButton.Left, false, true, FenceTargetKind.Sensor, 30, FenceGestureAction.RubberPanels)]
    [InlineData(FencePointerButton.Left, false, false, FenceTargetKind.Sensor, 30, FenceGestureAction.MoveSensors)]
    [InlineData(FencePointerButton.Left, false, false, FenceTargetKind.Enclosure, 30, FenceGestureAction.MoveEnclosure)]
    // 왼쪽 클릭
    [InlineData(FencePointerButton.Left, false, false, FenceTargetKind.Sensor, 4, FenceGestureAction.SelectOne)]
    [InlineData(FencePointerButton.Left, true, false, FenceTargetKind.Sensor, 4, FenceGestureAction.ToggleOne)]
    [InlineData(FencePointerButton.Left, false, false, FenceTargetKind.Panel, 0, FenceGestureAction.SelectOne)]
    [InlineData(FencePointerButton.Left, true, false, FenceTargetKind.Panel, 0, FenceGestureAction.ToggleOne)]
    [InlineData(FencePointerButton.Left, false, false, FenceTargetKind.Empty, 0, FenceGestureAction.ClearSelection)]
    [InlineData(FencePointerButton.Left, true, false, FenceTargetKind.Empty, 0, FenceGestureAction.None)]
    public void should_classify_into_one_action_when_given_button_modifiers_target_and_travel(FencePointerButton button, bool ctrl, bool shift,
        FenceTargetKind target, double travel, FenceGestureAction expected)
    {
        var isDrag = FenceGesture.IsDrag(new Point(100, 100), new Point(100 + travel, 100));

        Assert.Equal(expected, FenceGesture.Classify(button, ctrl, shift, target, isDrag));
    }

    [Fact]
    public void should_use_the_app_wide_eight_dip_dead_zone_when_judging_a_drag()
    {
        Assert.Equal(8.0, FenceGesture.DeadZone);
        Assert.False(FenceGesture.IsDrag(new Point(0, 0), new Point(8, 0)));    // 넘어야(초과) 끌기
        Assert.True(FenceGesture.IsDrag(new Point(0, 0), new Point(6, 6)));     // 대각선 8.49
    }
    #endregion

    #region - Rubber band -
    [Fact]
    public void should_pick_everything_the_band_touches_in_input_order_when_dragged_in_any_direction()
    {
        var items = new[]
        {
            (Key: 1, Bounds: new Rect(0, 0, 10, 10)),
            (Key: 2, Bounds: new Rect(20, 0, 10, 10)),
            (Key: 3, Bounds: new Rect(40, 0, 10, 10)),
            (Key: 4, Bounds: new Rect(60, 50, 10, 10)),
        };

        var band = FenceRubberBand.FromPoints(new Point(45, 8), new Point(15, -5));    // 오른쪽 아래 → 왼쪽 위
        var hits = FenceRubberBand.Hits(items, band);

        Assert.Equal(new[] { 2, 3 }, hits);
    }

    [Fact]
    public void should_add_to_the_selection_when_ctrl_is_held_and_replace_it_otherwise()
    {
        Assert.Equal(new[] { 5, 1, 2 }, FenceRubberBand.Merge(new[] { 5, 1 }, new[] { 1, 2 }, additive: true));
        Assert.Equal(new[] { 1, 2 }, FenceRubberBand.Merge(new[] { 5 }, new[] { 1, 2 }, additive: false));
    }
    #endregion

    #region - Signal lights (FR-14) -
    private static PingSample Ok(double ms) => new(true, ms);
    private static readonly PingSample Fail = new(false, 0);

    [Fact]
    public void should_be_unknown_when_no_sample_has_arrived()
        => Assert.Equal(SignalLevel.Unknown, SignalMath.Classify(System.Array.Empty<PingSample>()));

    [Fact]
    public void should_be_ok_when_the_last_five_answered_quickly()
        => Assert.Equal(SignalLevel.Ok, SignalMath.Classify(new[] { Fail, Ok(12), Ok(14), Ok(11), Ok(9), Ok(13) }));   // 여섯 번째 전 실패는 창 밖

    [Theory]
    [InlineData(new[] { 12.0, -1, 12, 12, 12 })]          // 손실 1
    [InlineData(new[] { -1.0, 12, -1, 12, 12 })]          // 손실 2
    [InlineData(new[] { 250.0, 240, 230, 210, 190 })]     // 평균 ≥ 200ms
    [InlineData(new[] { -1.0, 12, -1, 12, -1 })]          // 손실 3 이지만 연속이 아니다
    public void should_be_slow_when_some_are_lost_or_the_average_is_slow(double[] rtts)
        => Assert.Equal(SignalLevel.Slow, SignalMath.Classify(rtts.Select(r => r < 0 ? Fail : Ok(r)).ToList()));

    [Fact]
    public void should_be_down_when_three_pings_fail_in_a_row()
        => Assert.Equal(SignalLevel.Down, SignalMath.Classify(new[] { Ok(10), Ok(10), Fail, Fail, Fail }));

    [Theory]
    [InlineData("OK", SignalLevel.Ok)]
    [InlineData("degraded", SignalLevel.Slow)]
    [InlineData("FAULT", SignalLevel.Down)]
    [InlineData(null, SignalLevel.Unknown)]
    [InlineData("UNKNOWN", SignalLevel.Unknown)]
    public void should_map_to_a_signal_level_when_network_interface_health_is_read(string? health, SignalLevel expected)
        => Assert.Equal(expected, SignalMath.FromHealth(health));

    [Fact]
    public void should_light_left_middle_right_or_nothing_when_the_level_changes()
    {
        Assert.Equal(new[] { -1, 0, 1, 2 }, new[] { SignalLevel.Unknown, SignalLevel.Down, SignalLevel.Slow, SignalLevel.Ok }.Select(SignalMath.LitIndex));
    }
    #endregion
}
