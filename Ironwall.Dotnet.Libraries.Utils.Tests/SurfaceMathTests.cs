using Ironwall.Dotnet.Libraries.Utils.Consoles;
using System;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Utils.Tests;

/****************************************************************************
   Purpose      : 셸 표면 이동 · 크기 · 되살리기 산술 (화면 없음)
   Created By   : Claude (N-14 셸 표면 · D-09/D-10 라이브러리 승격, MSTest → xUnit 포팅)
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 드래그 제스처 자체는 UIA 로 단언할 수 없다(<c>.claude/rules/common/drag-first-ux.md</c>).
/// 그래서 <b>판정을 전부 순수 함수로 빼고</b> 여기서 잠근다 — 화면은 이 함수들을 부르기만 한다.
///
/// <para>자리 치수는 호스트(Dotnet.Monitoring.Solution) 설계 정본 window-surface-kernel-storyboard.html
/// L1613 을 따른다: 좌측 스트립 54 · 머리 잔류 120×28 · 이벤트 드로어 300. 이 숫자는 시험 픽스처의
/// 값일 뿐이다 — <see cref="SurfaceArea"/> 자신은 어떤 상수도 모른다(호출부가 실측 · 설계값 무엇이든 넣는다).</para>
/// </summary>
public class SurfaceMathTests
{
    // 셸 1250×850 에서 타이틀바를 뺀 패널 층 — 좌측 스트립 54, 이벤트 드로어 300.
    private static SurfaceArea Shell(double w = 1250, double h = 800, double left = 54, double right = 300)
        => SurfaceArea.Of(w, h, left, right);

    private static SurfaceBounds Console(double x = 200, double y = 100, double w = 800, double h = 500)
        => new(x, y, w, h);

    // ─────────────── 기억해 둔 값 검증 ───────────────

    [Fact]
    public void should_accept_the_remembered_bounds_when_every_number_is_finite()
        => Assert.True(Console().IsUsable);

    [Theory]
    [InlineData(double.NaN, 100.0, 800.0, 500.0)]
    [InlineData(200.0, double.NaN, 800.0, 500.0)]
    [InlineData(200.0, 100.0, double.NaN, 500.0)]
    [InlineData(200.0, 100.0, 800.0, double.NaN)]
    [InlineData(double.PositiveInfinity, 100.0, 800.0, 500.0)]
    [InlineData(200.0, 100.0, double.PositiveInfinity, 500.0)]
    [InlineData(200.0, 100.0, 800.0, double.NegativeInfinity)]
    public void should_reject_the_remembered_bounds_when_a_number_is_not_finite(double x, double y, double w, double h)
        => Assert.False(new SurfaceBounds(x, y, w, h).IsUsable);

    [Theory]
    [InlineData(0.0, 500.0)]
    [InlineData(-800.0, 500.0)]
    [InlineData(800.0, 0.0)]
    [InlineData(800.0, -500.0)]
    [InlineData(10.0, 10.0)]        // 최소보다 작다
    public void should_reject_the_remembered_bounds_when_the_size_is_not_usable(double w, double h)
        => Assert.False(new SurfaceBounds(200, 100, w, h).IsUsable);

    // ─────────────── 금지 구역 클램프 ───────────────

    [Fact]
    public void should_stop_at_the_hamburger_strip_when_dragged_left()
    {
        var moved = SurfaceMath.Move(Console(x: 200), -500, 0, Shell());

        Assert.Equal(54, moved.X);   // 좌측 스트립 위로는 갈 수 없다 — 메뉴가 가려진다
        Assert.Equal(100, moved.Y);
        Assert.Equal(800, moved.Width);   // 이동은 크기를 건드리지 않는다
    }

    [Fact]
    public void should_keep_the_header_reachable_when_dragged_right_past_the_drawer()
    {
        var moved = SurfaceMath.Move(Console(x: 200), 5000, 0, Shell());

        // 1250 − 드로어 300 − 머리 120 = 830
        Assert.Equal(830, moved.X);
        Assert.True(SurfaceMath.IsReachable(moved, Shell()));
    }

    [Fact]
    public void should_keep_one_header_row_when_dragged_to_the_bottom()
    {
        var moved = SurfaceMath.Move(Console(), 0, 5000, Shell());

        Assert.Equal(800 - 28, moved.Y);   // 머리 한 줄(28)은 남는다
    }

    [Fact]
    public void should_stop_at_the_top_when_dragged_up()
        => Assert.Equal(0, SurfaceMath.Move(Console(), 0, -5000, Shell()).Y);

    [Fact]
    public void should_let_the_surface_hang_off_the_right_when_the_header_still_shows()
    {
        // 머리 120 만 남기고 오른쪽으로 걸쳐 있는 것은 허용 — 그래야 넓은 콘솔도 옮길 수 있다.
        var hanging = new SurfaceBounds(830, 100, 800, 500);

        Assert.True(SurfaceMath.IsReachable(hanging, Shell()));
        Assert.Equal(830, SurfaceMath.Clamp(hanging, Shell()).X);
    }

    // ─────────────── 크기 조절 ───────────────

    [Fact]
    public void should_keep_the_opposite_corner_still_when_the_left_edge_is_dragged()
    {
        var resized = SurfaceMath.Resize(Console(x: 200, w: 800), SurfaceEdge.Left, 100, 0, Shell());

        Assert.Equal(300, resized.X);
        Assert.Equal(700, resized.Width);
        Assert.Equal(1000, resized.Right);   // 오른쪽 모서리는 제자리
    }

    [Fact]
    public void should_keep_the_opposite_corner_still_when_the_top_edge_is_dragged()
    {
        var resized = SurfaceMath.Resize(Console(y: 100, h: 500), SurfaceEdge.Top, 0, 60, Shell());

        Assert.Equal(160, resized.Y);
        Assert.Equal(440, resized.Height);
        Assert.Equal(600, resized.Bottom);
    }

    [Fact]
    public void should_grow_from_the_corner_when_the_bottom_right_is_dragged()
    {
        var resized = SurfaceMath.Resize(Console(x: 100, y: 50, w: 400, h: 300), SurfaceEdge.BottomRight, 120, 80, Shell());

        Assert.Equal(100, resized.X);
        Assert.Equal(50, resized.Y);
        Assert.Equal(520, resized.Width);
        Assert.Equal(380, resized.Height);
    }

    [Fact]
    public void should_refuse_to_shrink_below_the_minimum_when_a_corner_is_dragged_inwards()
    {
        var resized = SurfaceMath.Resize(Console(x: 200, w: 800), SurfaceEdge.Right, -5000, 0, Shell());

        Assert.Equal(190, resized.Width);   // 상수와 비교하면 상수를 바꿔도 통과한다 — 설계가 적은 숫자를 박는다
        Assert.Equal(200, resized.X);       // 오른쪽을 끌었으니 왼쪽은 제자리
    }

    [Fact]
    public void should_honour_the_console_minimum_when_it_is_larger_than_the_absolute_one()
    {
        var resized = SurfaceMath.Resize(Console(w: 800), SurfaceEdge.Right, -5000, 0, Shell(), minWidth: 600);

        Assert.Equal(600, resized.Width);
    }

    [Fact]
    public void should_not_grow_over_the_drawer_when_the_right_edge_is_dragged()
    {
        var resized = SurfaceMath.Resize(Console(x: 100, w: 400), SurfaceEdge.Right, 5000, 0, Shell());

        Assert.Equal(1250 - 300, resized.Right);   // 드로어 왼쪽 경계에서 멈춘다
    }

    [Fact]
    public void should_not_grow_over_the_strip_when_the_left_edge_is_dragged()
    {
        var resized = SurfaceMath.Resize(Console(x: 200, w: 400), SurfaceEdge.Left, -5000, 0, Shell());

        Assert.Equal(54, resized.X);
        Assert.Equal(600, resized.Right);   // 오른쪽은 제자리
    }

    [Fact]
    public void should_do_nothing_when_no_edge_is_being_dragged()
    {
        var same = SurfaceMath.Resize(Console(), SurfaceEdge.None, 100, 100, Shell());

        Assert.Equal(Console(), same);
    }

    [Fact]
    public void should_let_a_moved_surface_overhang_the_drawer_but_never_grow_into_it()
    {
        // 의도된 비대칭이다. 옮기는 중에 크기를 깎으면 손이 잡고 있는 지점이 달아나므로 이동은 위치만 자른다
        // (설계 목업의 판정과 같다: nx = min(nx, 폭 − 드로어 − 120)). 반면 크기 조절은 '이만큼 크게' 라는
        // 명시적 의사라서 금지 구역 안으로 자라지 않는다.
        var moved = SurfaceMath.Move(new SurfaceBounds(200, 100, 800, 500), 5000, 0, Shell());
        Assert.Equal(830, moved.X);
        Assert.True(moved.Right > 1250 - 300);   // 옮긴 표면은 드로어 위로 걸칠 수 있다

        var grown = SurfaceMath.Resize(new SurfaceBounds(200, 100, 400, 500), SurfaceEdge.Right, 5000, 0, Shell());
        Assert.Equal(1250 - 300, grown.Right);   // 키운 표면은 드로어 경계에서 멈춘다
    }

    // ─────────────── 화면 밖 되살리기 ───────────────

    [Fact]
    public void should_bring_the_surface_back_when_the_shell_got_smaller()
    {
        // 어제 1920 폭 셸에서 x=1500 에 뒀다. 오늘 셸은 1250 이다.
        var remembered = new SurfaceBounds(1500, 200, 800, 500);
        var recovered = SurfaceMath.Recover(remembered, Shell(), SurfaceMath.Center(800, 500, Shell()));

        Assert.True(SurfaceMath.IsReachable(recovered, Shell()));
        Assert.Equal(830, recovered.X);
    }

    [Fact]
    public void should_shrink_the_surface_when_it_no_longer_fits_the_shell()
    {
        var remembered = new SurfaceBounds(60, 10, 1600, 1200);
        var recovered = SurfaceMath.Recover(remembered, Shell(), SurfaceMath.Center(800, 500, Shell()));

        Assert.Equal(1250 - 54 - 300, recovered.Width);   // 쓸 수 있는 폭까지만
        Assert.Equal(800, recovered.Height);
        Assert.True(SurfaceMath.IsReachable(recovered, Shell()));
    }

    [Fact]
    public void should_fall_back_to_the_default_place_when_the_remembered_value_is_garbage()
    {
        var fallback = SurfaceMath.Center(800, 500, Shell());
        var recovered = SurfaceMath.Recover(new SurfaceBounds(double.NaN, 0, 800, 500), Shell(), fallback);

        Assert.Equal(fallback, recovered);
    }

    [Fact]
    public void should_call_a_surface_unreachable_when_its_header_is_off_the_area()
    {
        Assert.False(SurfaceMath.IsReachable(new SurfaceBounds(-900, 100, 800, 500), Shell()));   // 머리가 스트립 왼쪽으로 완전히 빠졌다
        Assert.False(SurfaceMath.IsReachable(new SurfaceBounds(200, 900, 800, 500), Shell()));    // 머리가 아래로 빠졌다
        Assert.False(SurfaceMath.IsReachable(new SurfaceBounds(1000, 100, 800, 500), Shell()));   // 머리가 드로어 아래로 들어갔다
    }

    [Fact]
    public void should_survive_a_degenerate_area_when_the_shell_has_not_been_measured_yet()
    {
        // 첫 레이아웃 전에는 ActualWidth 가 0 이다 — 그때 기억해 둔 값을 망가뜨리면 안 된다.
        var empty = SurfaceArea.Of(0, 0);
        var kept = SurfaceMath.Recover(Console(), empty, Console(x: 0, y: 0));

        Assert.Equal(Console(), kept);
    }

    // ─────────────── 처음 자리 ───────────────

    [Fact]
    public void should_open_in_the_middle_of_the_usable_area_when_there_is_nothing_remembered()
    {
        var centered = SurfaceMath.Center(800, 500, Shell());

        // 쓸 수 있는 폭 1250−54−300 = 896 → (896−800)/2 = 48, 스트립 54 를 더해 102
        Assert.Equal(102, centered.X);
        Assert.Equal(150, centered.Y);
        Assert.Equal(800, centered.Width);
    }

    [Fact]
    public void should_shrink_the_first_place_when_the_console_is_wider_than_the_shell()
    {
        var centered = SurfaceMath.Center(1340, 800, Shell());

        Assert.Equal(896, centered.Width);
        Assert.Equal(54, centered.X);
        Assert.True(SurfaceMath.IsReachable(centered, Shell()));
    }

    // ─────────────── 키보드는 드래그와 같은 일을 한다 ───────────────

    [Fact]
    public void should_move_the_same_way_when_the_arrow_key_is_used_instead_of_a_drag()
    {
        var dragged = SurfaceMath.Move(Console(), 1, 0, Shell());
        var typed = SurfaceMath.KeyboardMove(Console(), 1, 0, coarse: false, Shell());

        Assert.Equal(dragged, typed);
    }

    [Fact]
    public void should_take_a_bigger_step_when_the_modifier_is_held()
    {
        var fine = SurfaceMath.KeyboardMove(Console(), 1, 0, coarse: false, Shell());
        var coarse = SurfaceMath.KeyboardMove(Console(), 1, 0, coarse: true, Shell());

        Assert.Equal(201, fine.X);
        Assert.Equal(210, coarse.X);
    }

    [Fact]
    public void should_stop_at_the_same_edge_when_the_arrow_key_is_held_against_it()
    {
        var bounds = Console(x: 60);
        for (var i = 0; i < 50; i++)
            bounds = SurfaceMath.KeyboardMove(bounds, -1, 0, coarse: true, Shell());

        Assert.Equal(54, bounds.X);
    }

    [Fact]
    public void should_resize_the_same_way_when_the_arrow_key_is_used_instead_of_a_corner_drag()
    {
        var dragged = SurfaceMath.Resize(Console(), SurfaceEdge.BottomRight, 10, 10, Shell());
        var typed = SurfaceMath.KeyboardResize(Console(), 1, 1, coarse: true, Shell());

        Assert.Equal(dragged, typed);
    }
}
