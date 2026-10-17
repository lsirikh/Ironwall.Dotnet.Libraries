using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using System.Linq;
using System.Windows;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>펜스 드래그 판정(wiring-fence-view FR-08 · FR-09) — 데드존 · 삽입 틈 · 함체 틈 · 드롭 대상.</summary>
public class FenceDropMathTests
{
    private static readonly double[] Xs = { 100, 200, 300 };

    #region - Dead zone -
    [Fact]
    public void should_reuse_app_wide_dead_zone_when_judging_drag()
        => Assert.Equal(DragMath.DeadZone, FenceDropMath.DeadZone);

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(5, 5, false)]        // 50 < 64
    [InlineData(8, 0, false)]        // 경계는 클릭
    [InlineData(0, -8, false)]
    [InlineData(8.1, 0, true)]
    [InlineData(6, 6, true)]         // 72 > 64
    [InlineData(-20, 3, true)]
    public void should_start_drag_only_beyond_eight_dip_when_pointer_moves(double dx, double dy, bool expected)
        => Assert.Equal(expected, FenceDropMath.IsDrag(new Point(50, 50), new Point(50 + dx, 50 + dy)));
    #endregion

    #region - Insertion gap -
    [Theory]
    [InlineData(10, 0)]
    [InlineData(99.9, 0)]
    [InlineData(100, 1)]             // 센서 x 와 같으면 그 뒤
    [InlineData(150, 1)]
    [InlineData(199.9, 1)]
    [InlineData(250, 2)]
    [InlineData(300, 3)]
    [InlineData(1000, 3)]
    public void should_map_pointer_x_to_gap_between_sensors_when_nothing_is_excluded(double x, int expected)
        => Assert.Equal(expected, FenceDropMath.InsertionGap(Xs, x));

    [Fact]
    public void should_return_zero_when_chain_is_empty()
        => Assert.Equal(0, FenceDropMath.InsertionGap(System.Array.Empty<double>(), 42));

    [Fact]
    public void should_collapse_gaps_around_dragged_sensor_to_a_no_op_when_dropped_beside_itself()
    {
        var dragged = new[] { 1 };

        var before = FenceDropMath.InsertionGap(Xs, 150, dragged);
        var after = FenceDropMath.InsertionGap(Xs, 250, dragged);

        Assert.Equal(2, before);
        Assert.Equal(2, after);
        Assert.True(FenceDropMath.IsNoOpMove(dragged, before));
        Assert.Equal(0, FenceDropMath.InsertionGap(Xs, 50, dragged));
        Assert.False(FenceDropMath.IsNoOpMove(dragged, 0));
    }

    [Theory]
    [InlineData(new[] { 1 }, 0, false)]
    [InlineData(new[] { 1 }, 1, true)]
    [InlineData(new[] { 1 }, 2, true)]
    [InlineData(new[] { 1, 2 }, 3, true)]
    [InlineData(new[] { 1, 2 }, 4, false)]
    [InlineData(new[] { 1, 3 }, 2, false)]     // 떨어진 무리는 모이는 것 자체가 옮김
    [InlineData(new int[0], 2, true)]
    public void should_detect_no_op_when_gap_touches_contiguous_dragged_run(int[] from, int gap, bool expected)
        => Assert.Equal(expected, FenceDropMath.IsNoOpMove(from, gap));

    [Fact]
    public void should_pick_gap_containing_pointer_when_enclosure_is_dropped()
    {
        Assert.Equal(0, FenceDropMath.EnclosureGap(Xs, 20));
        Assert.Equal(2, FenceDropMath.EnclosureGap(Xs, 260));
        Assert.Equal(3, FenceDropMath.EnclosureGap(Xs, 900));
    }
    #endregion

    #region - Classify -
    private static readonly Rect ChainZone = new(0, 0, 400, 100);
    private static readonly Rect PaletteZone = new(0, 120, 400, 60);
    private static readonly Rect RemoveZone = new(300, 60, 100, 60);          // 체인 영역과 겹친다

    [Fact]
    public void should_insert_into_chain_gap_when_dropped_on_chain()
    {
        var d = FenceDropMath.Classify(FenceDragSource.Palette, new Point(150, 50), ChainZone, PaletteZone, RemoveZone, Xs);

        Assert.Equal(FenceDropTarget.ChainGap, d.Target);
        Assert.Equal(1, d.Gap);
        Assert.False(d.RemovesFromChain);
    }

    [Fact]
    public void should_prefer_remove_zone_when_zones_overlap()
    {
        var d = FenceDropMath.Classify(FenceDragSource.Chain, new Point(350, 80), ChainZone, PaletteZone, RemoveZone, Xs, new[] { 0 });

        Assert.Equal(FenceDropTarget.Remove, d.Target);
        Assert.True(d.RemovesFromChain);
    }

    [Fact]
    public void should_return_to_palette_when_chain_sensor_dropped_on_palette()
        => Assert.Equal(FenceDropTarget.Palette,
            FenceDropMath.Classify(FenceDragSource.Chain, new Point(50, 150), ChainZone, PaletteZone, RemoveZone, Xs).Target);

    [Fact]
    public void should_do_nothing_when_palette_sensor_dropped_on_palette_or_remove()
    {
        Assert.Equal(FenceDropDecision.Nothing,
            FenceDropMath.Classify(FenceDragSource.Palette, new Point(50, 150), ChainZone, PaletteZone, RemoveZone, Xs));
        Assert.Equal(FenceDropDecision.Nothing,
            FenceDropMath.Classify(FenceDragSource.Palette, new Point(350, 80), ChainZone, PaletteZone, RemoveZone, Xs));
    }

    [Fact]
    public void should_do_nothing_when_dropped_outside_every_zone()
        => Assert.Equal(FenceDropTarget.None,
            FenceDropMath.Classify(FenceDragSource.Chain, new Point(900, 900), ChainZone, PaletteZone, RemoveZone, Xs).Target);

    [Fact]
    public void should_ignore_missing_zone_when_it_is_empty()
        => Assert.Equal(FenceDropTarget.ChainGap,
            FenceDropMath.Classify(FenceDragSource.Chain, new Point(350, 80), ChainZone, Rect.Empty, Rect.Empty, Xs, new[] { 0 }).Target);
    #endregion

    [Fact]
    public void should_reorder_chain_consistently_when_layout_drop_and_chain_move_combine()
    {
        // 체인 [1..5] 에서 1번을 끌어 4·5 사이(틈 4)에 놓는다 — 레이아웃 x → 틈 → 체인 옮기기.
        var chain = WiringChain.Create(WiringShape.Ring, new[] { 1, 2, 3, 4, 5 });
        var layout = FenceSlotLayout.Build(chain, _ => EnumDeviceType.SmartSensor2);
        var pointer = (layout.SensorXs[3] + layout.SensorXs[4]) / 2;

        var gap = FenceDropMath.InsertionGap(layout.SensorXs, pointer, new[] { 0 });
        var moved = chain.Move(1, gap);

        Assert.Equal(4, gap);
        Assert.Equal(new[] { 2, 3, 4, 1, 5 }, moved.Keys);
        Assert.Equal(layout.GapX(4), FenceDropMath.InsertionMarkerX(layout, 4));
        Assert.Equal(pointer, layout.GapX(4), 6);
    }
}
