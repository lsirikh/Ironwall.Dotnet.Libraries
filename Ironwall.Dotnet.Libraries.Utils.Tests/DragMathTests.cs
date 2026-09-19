using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using System.Windows;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Utils.Tests;

/// <summary>
/// console-kernel FR-15 · FR-20 · FR-22 · FR-24 — 드래그 판정은 UI 에서 떼어 낸 순수 함수다.
/// UIA 에 드래그 패턴이 없어 이 테스트와 키보드 폴백 경로가 유일한 회귀망이다.
/// </summary>
public class DragMathTests
{
    #region 데드존
    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(8, 0, false)]        // 정확히 8 은 아직 클릭
    [InlineData(0, -8, false)]
    [InlineData(5.6, 5.6, false)]    // 7.92
    [InlineData(5.7, 5.7, true)]     // 8.06
    [InlineData(8.01, 0, true)]
    [InlineData(-9, 0, true)]
    public void should_treat_movement_as_drag_only_beyond_deadzone(double dx, double dy, bool expected)
    {
        Assert.Equal(expected, DragMath.IsDrag(dx, dy));
    }

    [Fact]
    public void should_keep_deadzone_at_8_diu()
    {
        Assert.Equal(8.0, DragMath.DeadZone);
    }
    #endregion

    #region 삽입 인덱스
    private static readonly Rect[] ThreeRows =
    {
        new(0, 0, 100, 38), new(0, 38, 100, 38), new(0, 76, 100, 38),
    };

    [Theory]
    [InlineData(-50, 0)]    // 목록 위
    [InlineData(0, 0)]
    [InlineData(18.9, 0)]   // 첫 행 중점(19) 위
    [InlineData(19, 1)]     // 중점부터는 그 행 아래
    [InlineData(56.9, 1)]
    [InlineData(57, 2)]
    [InlineData(95, 3)]
    [InlineData(500, 3)]    // 목록 아래 = 맨 끝
    public void should_pick_insertion_index_by_row_midpoint(double y, int expected)
    {
        Assert.Equal(expected, DragMath.InsertionIndex(ThreeRows, y));
    }

    [Fact]
    public void should_insert_at_zero_when_list_is_empty()
    {
        Assert.Equal(0, DragMath.InsertionIndex(Array.Empty<Rect>(), 40));
    }

    [Fact]
    public void should_use_measured_rects_not_uniform_height()
    {
        // 행 높이가 제각각이어도(패널마다 38, 일부 가변) 실측 rect 로만 판정한다.
        var rows = new[] { new Rect(0, 0, 100, 20), new Rect(0, 20, 100, 80), new Rect(0, 100, 100, 38) };

        Assert.Equal(1, DragMath.InsertionIndex(rows, 30));    // 둘째 행 중점 60 위
        Assert.Equal(2, DragMath.InsertionIndex(rows, 61));
    }

    [Theory]
    [InlineData(0, 0.0)]
    [InlineData(1, 38.0)]
    [InlineData(3, 114.0)]
    public void should_place_insertion_line_between_rows(int index, double expectedY)
    {
        Assert.Equal(expectedY, DragMath.InsertionLineY(ThreeRows, index));
    }
    #endregion

    #region 재배열
    [Theory]
    [InlineData(0, 3, "BCAD")]   // A 를 D 앞으로 (삽입 인덱스 3 = C 와 D 사이)
    [InlineData(0, 4, "BCDA")]   // 맨 끝
    [InlineData(3, 0, "DABC")]
    [InlineData(2, 1, "ACBD")]
    [InlineData(1, 1, "ABCD")]   // 제자리
    [InlineData(1, 2, "ABCD")]   // 바로 아래 = 제자리
    public void should_move_item_to_insertion_index(int from, int insertionIndex, string expected)
    {
        var list = new List<char> { 'A', 'B', 'C', 'D' };

        DragMath.Move(list, from, insertionIndex);

        Assert.Equal(expected, new string(list.ToArray()));
    }

    [Theory]
    [InlineData(1, 1, false)]
    [InlineData(1, 2, false)]
    [InlineData(1, 0, true)]
    [InlineData(1, 3, true)]
    public void should_report_whether_move_changes_order(int from, int insertionIndex, bool expected)
    {
        Assert.Equal(expected, DragMath.IsRealMove(from, insertionIndex));
    }

    [Fact]
    public void should_move_many_items_keeping_their_relative_order()
    {
        var list = new List<char> { 'A', 'B', 'C', 'D', 'E' };

        DragMath.MoveMany(list, new[] { 3, 0 }, insertionIndex: 5);   // 고른 순서가 아니라 목록 순서를 지킨다

        Assert.Equal("BCEAD", new string(list.ToArray()));
    }

    [Fact]
    public void should_move_many_items_into_the_middle_of_the_selection()
    {
        var list = new List<char> { 'A', 'B', 'C', 'D', 'E' };

        DragMath.MoveMany(list, new[] { 0, 4 }, insertionIndex: 2);

        Assert.Equal("BAECD", new string(list.ToArray()));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(4, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 5)]
    public void should_throw_when_index_is_out_of_range(int from, int insertionIndex)
    {
        var list = new List<char> { 'A', 'B', 'C', 'D' };

        Assert.Throws<ArgumentOutOfRangeException>(() => DragMath.Move(list, from, insertionIndex));
    }

    [Theory]
    [InlineData(0, -1, 0)]   // Alt+↑ 맨 위 = 그대로
    [InlineData(2, -1, 1)]
    [InlineData(2, 1, 3)]
    [InlineData(3, 1, 3)]    // Alt+↓ 맨 아래 = 그대로
    public void should_step_index_for_keyboard_fallback(int index, int direction, int expected)
    {
        Assert.Equal(expected, DragMath.StepIndex(index, direction, count: 4));
    }
    #endregion

    #region 오토스크롤
    [Theory]
    [InlineData(200, 0)]      // 가운데 — 멈춤
    [InlineData(32, 0)]       // 경계선 위 — 아직 멈춤
    [InlineData(16, -240)]    // 위 경계 절반 깊이 → 절반 속도, 위로
    [InlineData(0, -480)]
    [InlineData(-40, -480)]   // 밖으로 나가도 최대 속도에서 멈춘다
    [InlineData(384, 240)]    // 아래 경계(400-32=368) 절반 깊이
    [InlineData(400, 480)]
    public void should_scale_autoscroll_speed_by_depth_into_edge(double y, double expectedPxPerSec)
    {
        Assert.Equal(expectedPxPerSec, DragMath.AutoScrollVelocity(y, viewportLength: 400), 3);
    }

    [Fact]
    public void should_scroll_by_elapsed_time_not_by_frame()
    {
        // 프레임레이트와 무관 — RDP · 저사양에서 같은 조작이 같은 속도여야 한다.
        var at60 = DragMath.AutoScrollDelta(0, 400, TimeSpan.FromSeconds(1.0 / 60)) * 60;
        var at15 = DragMath.AutoScrollDelta(0, 400, TimeSpan.FromSeconds(1.0 / 15)) * 15;

        // TimeSpan 은 100ns 틱으로 반올림된다 — 초당 환산 오차는 0.01px 미만.
        Assert.Equal(at60, at15, 2);
        Assert.Equal(-480, at60, 2);
    }

    [Fact]
    public void should_not_autoscroll_when_viewport_is_too_small_for_two_edges()
    {
        Assert.Equal(0, DragMath.AutoScrollVelocity(10, viewportLength: 40));
    }
    #endregion
}
