using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Utils.Tests;

/// <summary>
/// 칩 줄의 접힌 한 줄 판정(<see cref="ConsoleChipStripMath"/>) — 화면 없이.
/// </summary>
/// <remarks>2026-09-28 사용자 보고 "권한 필터 상단 칩 줄이 짤린다 … 아래 꺽쇠로 더 보기" 의 커널 규칙.</remarks>
public class ConsoleChipStripMathTests
{
    private static readonly double[] FiveChips = { 100, 100, 100, 100, 100 };

    [Fact]
    public void should_show_every_chip_without_the_toggle_when_all_chips_fit_on_one_line()
    {
        var fit = ConsoleChipStripMath.Fit(500, FiveChips, toggleWidth: 80, selectedIndex: -1);

        Assert.False(fit.ShowsToggle);
        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, fit.Visible);
        Assert.Equal(0, fit.HiddenCount);
        Assert.True(fit.SelectedKept);
    }

    [Fact]
    public void should_show_every_chip_when_the_available_width_is_unbounded()
    {
        var fit = ConsoleChipStripMath.Fit(double.PositiveInfinity, FiveChips, 80, 4);

        Assert.False(fit.ShowsToggle);
        Assert.Equal(5, fit.Visible.Count);
    }

    [Fact]
    public void should_reserve_the_toggle_slot_and_hide_the_rest_when_chips_overflow()
    {
        // 400 - 80 = 320 → 100 × 3
        var fit = ConsoleChipStripMath.Fit(400, FiveChips, toggleWidth: 80, selectedIndex: -1);

        Assert.True(fit.ShowsToggle);
        Assert.Equal(new[] { 0, 1, 2 }, fit.Visible);
        Assert.Equal(2, fit.HiddenCount);
    }

    [Fact]
    public void should_keep_a_visible_selection_in_place_when_the_selected_chip_already_fits()
    {
        var fit = ConsoleChipStripMath.Fit(400, FiveChips, 80, selectedIndex: 1);

        Assert.Equal(new[] { 0, 1, 2 }, fit.Visible);
        Assert.True(fit.SelectedKept);
    }

    [Fact]
    public void should_place_the_selected_chip_before_the_toggle_when_it_would_be_hidden()
    {
        // 고른 칩(4)이 밀려날 차례 — 앞 칩을 하나 덜 세우고 [더 보기] 바로 앞에 세운다
        var fit = ConsoleChipStripMath.Fit(400, FiveChips, 80, selectedIndex: 4);

        Assert.Equal(new[] { 0, 1, 4 }, fit.Visible);
        Assert.Equal(2, fit.HiddenCount);
        Assert.True(fit.SelectedKept);
    }

    [Fact]
    public void should_make_room_for_a_wide_selected_chip_by_dropping_more_leading_chips()
    {
        var widths = new double[] { 100, 100, 100, 100, 250 };

        var fit = ConsoleChipStripMath.Fit(400, widths, 80, selectedIndex: 4);   // 320 - 250 = 70 → 앞 칩 0 개

        Assert.Equal(new[] { 4 }, fit.Visible);
        Assert.Equal(4, fit.HiddenCount);
        Assert.True(fit.SelectedKept);
    }

    [Fact]
    public void should_still_keep_the_selected_chip_when_it_is_wider_than_the_whole_budget()
    {
        var widths = new double[] { 100, 100, 600 };

        var fit = ConsoleChipStripMath.Fit(300, widths, 80, selectedIndex: 2);

        Assert.Equal(new[] { 2 }, fit.Visible);
        Assert.True(fit.SelectedKept);
        Assert.Equal(2, fit.HiddenCount);
    }

    [Fact]
    public void should_show_only_the_toggle_when_no_chip_fits_and_nothing_is_selected()
    {
        var fit = ConsoleChipStripMath.Fit(120, FiveChips, 80, selectedIndex: -1);

        Assert.True(fit.ShowsToggle);
        Assert.Empty(fit.Visible);
        Assert.Equal(5, fit.HiddenCount);
    }

    [Fact]
    public void should_treat_an_out_of_range_selection_as_no_selection_when_fitting()
    {
        var fit = ConsoleChipStripMath.Fit(400, FiveChips, 80, selectedIndex: 9);

        Assert.Equal(new[] { 0, 1, 2 }, fit.Visible);
        Assert.True(fit.SelectedKept);
    }

    [Fact]
    public void should_not_flip_the_last_chip_when_the_width_differs_by_rounding_only()
    {
        // 측정 폭의 소수점 오차(0.3) — 다 들어가는 것으로 본다
        var fit = ConsoleChipStripMath.Fit(499.7, FiveChips, 80, -1);

        Assert.False(fit.ShowsToggle);
    }

    [Fact]
    public void should_wrap_cells_to_new_lines_when_the_line_is_full()
    {
        var lines = ConsoleChipStripMath.Wrap(250, new double[] { 100, 100, 100, 100, 60 });

        Assert.Equal(new[] { 0, 0, 1, 1, 2 }, lines);
    }

    [Fact]
    public void should_keep_an_oversized_cell_on_its_own_line_when_wrapping()
    {
        var lines = ConsoleChipStripMath.Wrap(250, new double[] { 400, 100 });

        Assert.Equal(new[] { 0, 1 }, lines);
    }

    [Fact]
    public void should_return_no_lines_when_there_are_no_cells()
    {
        Assert.Empty(ConsoleChipStripMath.Wrap(250, Array.Empty<double>()));
        Assert.Empty(ConsoleChipStripMath.Fit(250, Array.Empty<double>(), 80, -1).Visible);
    }
}
